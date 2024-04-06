extern alias CAW;

using System.Resources;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System.Text.Json.Nodes;

namespace Au.Compiler;

/// <summary>
/// Compiles C# files.
/// </summary>
partial class Compiler {
	/// <summary>
	/// Compiles C# file or project if need.
	/// Returns false if fails (C# errors etc).
	/// </summary>
	/// <param name="reason">Whether to recompile if compiled, etc. See also Remarks.</param>
	/// <param name="r">Results.</param>
	/// <param name="f">C# file. If projFolder used, must be the main file of the project.</param>
	/// <param name="projFolder">null or project folder.</param>
	/// <param name="needMeta">Parse metacomments and set r.meta even if don't need to compile.</param>
	/// <param name="canCompile">Called after parsing meta, creating trees and compilation, but before emit. Return false to cancel.</param>
	/// <param name="addMetaFlags">Add these flags to the usual flags of MetaComments. Used by MetaComments._PR.</param>
	/// <remarks>
	/// Must be always called in the main UI thread (Environment.CurrentManagedThreadId == 1).
	/// 
	/// Adds <see cref="MetaReferences.DefaultReferences"/>.
	/// 
	/// If f role is classFile:
	/// 	If CompReason.Run, does not compile (just parses meta), sets r.role=classFile and returns false.
	/// 	Else compiles but does not create output files.
	/// </remarks>
	public static bool Compile(CCReason reason, out CompResults r, FileNode f, FileNode projFolder = null, bool needMeta = false, Func<CanCompileArgs, bool> canCompile = null, MCFlags addMetaFlags = 0) {
		Debug.Assert(Environment.CurrentManagedThreadId == 1);
		r = null;
		var cache = XCompiled.OfWorkspace;
		if (reason is not (CCReason.CompileAlways or CCReason.WpfPreview) && !addMetaFlags.Has(MCFlags.Publish) && cache.IsCompiled(f, out r, projFolder)) {
			//print.it("cached");
			if (needMeta) {
				var m = new MetaComments(MCFlags.PrintErrors | MCFlags.OnlyRef);
				if (!m.Parse(f, projFolder)) return false;
				r.meta = m;
				//FUTURE: save used dll etc paths in xcompiled, to avoid parsing meta of all pr.
			}
			return true;
		} else {
			//print.it("COMPILE");
			Action aFinally = null;
			try {
				var c = new Compiler();
				if (c._Compile(reason, f, out r, projFolder, out aFinally, canCompile, addMetaFlags)) return true;
			}
			catch (Exception ex) {
				if (reason != CCReason.WpfPreview) print.it($"Failed to compile '{f.Name}'. {ex}");
			}
			finally {
				aFinally?.Invoke();
			}
			
			cache.Remove(f, false);
			return false;
		}
	}
	
	/// <summary>_Compile() output assembly info.</summary>
	public record CompResults {
		/// <summary>C# file name without ".cs".</summary>
		public string name;
		
		/// <summary>Full path of assembly file.</summary>
		public string file;
		
		public MCRole role;
		public MCIfRunning ifRunning;
		public MCUac uac;
		public MiniProgram_.MPFlags flags;
		public bool bit32;
		
		/// <summary>The assembly is normal .exe or .dll file, not in cache. If exe, its dependencies were copied to its directory.</summary>
		public bool notInCache;
		
		/// <summary>May be null if not explicitly requested.</summary>
		public MetaComments meta;
	}
	
	MetaComments _meta;
	CSharpCompilation _compilation;
	Dictionary<string, string> _dr, _dn;
	string _tpa;
	
	bool _Compile(CCReason reason, FileNode f, out CompResults r, FileNode projFolder, out Action aFinally, Func<CanCompileArgs, bool> canCompile, MCFlags addMetaFlags) {
		//print.it("COMPILE");
		
		//var p1 = perf.local();
		r = new CompResults();
		aFinally = null;
		
		_meta = new((reason == CCReason.WpfPreview ? MCFlags.WpfPreview : MCFlags.PrintErrors) | addMetaFlags);
		if (!_meta.Parse(f, projFolder)) return false;
		var err = _meta.Errors;
		r.meta = _meta;
		//p1.Next('m');
		
		bool needOutputFiles = _meta.Role != MCRole.classFile;
		
		//if for run, don't compile if f role is classFile
		if (reason == CCReason.Run && !needOutputFiles) {
			r.role = MCRole.classFile;
			return false;
		}
		
		XCompiled cache = XCompiled.OfWorkspace;
		string outPath = null, outFile = null, fileName = null;
		bool notInCache = false;
		if (needOutputFiles) {
			if (notInCache = _meta.OutputPath != null) {
				outPath = _meta.OutputPath;
				fileName = _meta.Name + ".dll";
			} else {
				outPath = cache.CacheDirectory;
				if (reason == CCReason.WpfPreview) fileName = "WPFpreview.dll";
				else fileName = f.IdString + ".dll";
				//note: must have ".dll", else somehow don't work GetModuleHandle, Process.GetCurrentProcess().Modules etc
			}
			outFile = outPath + "\\" + fileName;
			filesystem.createDirectory(outPath);
		}
		
		if (_meta.PreBuild.f != null && !_RunPrePostBuildScript(false, outFile)) return false;
		
		var trees = CompilerUtil.CreateSyntaxTrees(_meta);
		//p1.Next('t');
		
		string asmName = _meta.Name;
		if (_meta.Role == MCRole.editorExtension) { //cannot load multiple assemblies with same name
			asmName = asmName + "|" + Guid.NewGuid().ToString();
			//use GUID, not counter, because may be loaded old assembly from cache with same counter value
		} else if (_meta.Role == MCRole.miniProgram) {
			//workaround for: coreclr_execute_assembly and even AssemblyLoadContext.Default.LoadFromAssemblyPath fail
			//	if asmName is the same as of a .NET etc assembly.
			//	It seems it at first ignores path and tries to find assembly by name.
			//	But be careful. It could break something. Eg with WPF resources use "pack:...~AssemblyName...".
			asmName = "~" + asmName;
		}
		
		if (_meta.TestInternal is string[] testInternal) {
			TestInternal.CompilerStart(asmName, testInternal);
			aFinally += TestInternal.CompilerEnd; //this func is called from try/catch/finally which calls aFinally
		}
		
		List<ResourceDescription> resMan = null;
		if (needOutputFiles) { //before creating compilation. May modify trees[] elements.
			resMan = _CreateManagedResources(asmName, trees);
			if (err.ErrorCount != 0) { err.PrintAll(); return false; }
			//p1.Next('y');
		}
		
		var cOpt = _meta.CreateCompilationOptions();
		_compilation = CSharpCompilation.Create(asmName, trees, _meta.References.Refs, cOpt);
		//p1.Next('c');
		
		if (canCompile != null && !canCompile(new(_meta, trees, _compilation))) return false;
		
		string xdFile = null;
		Stream xdStream = null;
		Stream resNat = null;
		EmitOptions eOpt = null;
		
		if (needOutputFiles) {
			r.flags |= _AddAttributesEtc();
			//p1.Next('a');
			
			//rejected: if empty script, add {} to avoid error "no Main". See AddErrorOrWarning.
			
			//Create debug info always. It is used for run-time error links.
			//Embed it in assembly. It adds < 1 KB. Almost the same compiling speed. Same loading speed.
			//Don't use classic pdb file. It is 14 KB, 2 times slower compiling, slower loading; error with .NET Core: Unexpected error writing debug information -- 'The version of Windows PDB writer is older than required: 'diasymreader.dll''.
#if DEBUG //temp test debugger with non-user-code dll
			if (!(_meta.Role == MCRole.classLibrary && (_meta.Optimize || _meta.Name == "Au"))) //TODO: delete
#endif
				eOpt = new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded);
			
			if (_meta.XmlDoc) //allowed if role is classLibrary or exeProgram, but in Properties hidden if exeProgram (why could need it?)
				xdStream = filesystem.waitIfLocked(() => File.Create(xdFile = outPath + "\\" + _meta.Name + ".xml"));
			
			resNat = _CreateNativeResources();
			if (err.ErrorCount != 0) { err.PrintAll(); return false; }
			
			//EmbeddedText.FromX //it seems we can embed source code in PDB. Not tested.
		}
		
		//p1.Next();
		var asmStream = new MemoryStream(20_000);
		var emitResult = _compilation.Emit(asmStream, null, xdStream, resNat, resMan, eOpt);
		
		if (needOutputFiles) {
			xdStream?.Dispose();
			resNat?.Dispose(); //info: compiler disposes resMan
		}
		//p1.Next('e');
		
		if (reason != CCReason.WpfPreview) {
			var diag = emitResult.Diagnostics;
			if (!diag.IsDefaultOrEmpty) {
				foreach (var d in diag) {
					if (d.Severity == DiagnosticSeverity.Hidden) continue;
					err.AddErrorOrWarning(d, f);
					if (d.Severity == DiagnosticSeverity.Error && d.Id == "CS0009") MetaReferences.RemoveBadRefFromCache(d.GetMessage());
				}
				err.PrintAll();
			}
		}
		if (!emitResult.Success) {
			if (needOutputFiles) {
				Api.DeleteFile(outFile);
				if (xdFile != null) Api.DeleteFile(xdFile);
			}
			return false;
		}
		
		if (needOutputFiles) {
			if (_meta.Role == MCRole.miniProgram) {
				//is Main with [MTAThread]? Default STA, even if Main without [STAThread].
				//FUTURE: C# 12 [assembly: MTAThread]
				if (_compilation.GetEntryPoint(default)?.GetAttributes().Any(o => o.ToString() == "System.MTAThreadAttribute") ?? false) r.flags |= MiniProgram_.MPFlags.MTA;
				
				if (_meta.Console) r.flags |= MiniProgram_.MPFlags.Console;
			}
			
			//create assembly file
			//p1.Next();
			gSave:
			var hf = Api.CreateFile(outFile, Api.GENERIC_WRITE, 0, Api.CREATE_ALWAYS);
			if (hf.Is0) {
				var ec = lastError.code;
				if (ec == Api.ERROR_SHARING_VIOLATION && _RenameLockedFile(outFile, notInCache: notInCache)) goto gSave;
				throw new AuException(ec, outFile);
			}
			var b = asmStream.GetBuffer();
			
			using (hf) if (!Api.WriteFile2(hf, b.AsSpan(0, (int)asmStream.Length), out _)) throw new AuException(0);
			//saving would be fast, but with AV can take half of time.
			//	With WD now fast, but used to be slow. Now on save WD scans async, and on load scans only if still not scanned, eg if loading soon after saving.
			//	With Avast now the same as with WD.
			//p1.Next('s');
			r.file = outFile;
			
			if (_meta.Role == MCRole.exeProgram) {
				_GetDllPaths();
				
				bool need64 = !_meta.Bit32 /*|| _meta.Optimize*/;
				bool need32 = _meta.Bit32 || _meta.Optimize;
				
				//copy app host template exe, add native resources, set assembly name, set console flag if need
				if (need64) _AppHost(outFile, fileName, bit32: false);
				if (need32) _AppHost(outFile, fileName, bit32: true);
				//p1.Next('h'); //very slow with AV. Eg with WD this part makes whole compilation several times slower.
				
				//copy dlls to the output directory
				_CopyDlls(asmStream, need64: need64, need32: need32);
				//p1.Next('d');
				
				//copy config file to the output directory
				//var configFile = exeFile + ".config";
				//if(_meta.ConfigFile != null) {
				//	r.hasConfig = true;
				//	_CopyFileIfNeed(_meta.ConfigFile.FilePath, configFile);
				//} else if(filesystem.exists(configFile, true).File) {
				//	filesystem.delete(configFile);
				//}
			} else if (!_meta.Console) {
				//if using assembly System.Console in miniProgram script, let it redirect Console.Write etc to print.it.
				//	Don't redirect always, it's slow. Console.Write etc rarely used when there is print.it.
				//Speed of this code: 50 mcs.
				asmStream.Position = 0;
				using var pr = new PEReader(asmStream, PEStreamOptions.LeaveOpen);
				var mr = pr.GetMetadataReader();
				foreach (var handle in mr.AssemblyReferences) {
					var name = mr.GetString(mr.GetAssemblyReference(handle).Name);
					if (name == "System.Console") { r.flags |= MiniProgram_.MPFlags.RedirectConsole; break; }
				}
			}
		}
		
		if (_meta.PostBuild.f != null && !_RunPrePostBuildScript(true, outFile)) return false;
		
		if (_meta.StartFaster) r.flags |= MiniProgram_.MPFlags.Preloaded;
		
		if (reason != CCReason.WpfPreview && !addMetaFlags.Has(MCFlags.Publish)) {
			if (needOutputFiles) {
				cache.AddCompiled(f, outFile, _meta, r.flags);
				
				if (_meta.Role == MCRole.classLibrary) {
					MetaReferences.UncacheOldFiles();
					if (MetaReferences.IsDefaultRef(_meta.Name)
						&& _meta.References.Refs.Any(o => _meta.Name.Eqi(o.Display.Contains('\\') ? pathname.getNameNoExt(o.Display) : o.Display))) //may be removed with meta noRef
						print.warning($"Library name '{_meta.Name}' should not be used. Rename the C# file.", -1);
				}
				
				if (notInCache) print.it($"<>Compiled {f.SciLink()} -> <explore>{outFile}<>");
			}
			
			if (App.Settings.comp_printCompiled != false && !notInCache)
				if (App.Settings.comp_printCompiled == true || reason == CCReason.CompileAlways)
					print.it($"<>Compiled {f.SciLink()}");
		}
		
		r.name = _meta.Name;
		r.role = _meta.Role;
		r.ifRunning = _meta.IfRunning;
		r.uac = _meta.Uac;
		r.bit32 = _meta.Bit32;
		r.notInCache = notInCache;
		
		//#if DEBUG
		//p1.NW('C');
		//#endif
		//print.it($"<><c red>compiled<> {f}");
		return true;
		
		//SHOULDDO: rebuild if missing apphost. Now rebuilds only if missing dll.
	}
	
	/// <summary>
	/// Adds some module/assembly attributes. Also adds module initializer for role exeProgram.
	/// </summary>
	MiniProgram_.MPFlags _AddAttributesEtc() {
		MiniProgram_.MPFlags rflags = 0;
		//bool needDefaultCharset = true;
		//foreach (var v in _compilation.SourceModule.GetAttributes()) {
		//	//print.it(v.AttributeClass.Name);
		//	if (v.AttributeClass.Name == "DefaultCharSetAttribute") { needDefaultCharset = false; break; }
		//}
		bool needTargetFramework = false, needAssemblyTitle = false;
		if (_meta.Role is MCRole.exeProgram or MCRole.classLibrary or MCRole.miniProgram) {
			needTargetFramework = true; //for AppContext.TargetFrameworkName, else it will return null
			needAssemblyTitle = _meta.Role is MCRole.exeProgram or MCRole.classLibrary; //displayed in various system UI as program description (else empty)
			foreach (var v in _compilation.Assembly.GetAttributes()) {
				//print.it(v.AttributeClass.Name);
				switch (v.AttributeClass.Name) {
				case "TargetFrameworkAttribute": needTargetFramework = false; break;
				case "AssemblyTitleAttribute": needAssemblyTitle = false; break;
				}
			}
		}
		
		using (new StringBuilder_(out var sb)) {
			//sb.AppendLine("using System.Reflection;using System.Runtime.InteropServices;");
			
			//rejected. Now in global.cs, #if !NO_DEFAULT_CHARSET_UNICODE
			//if (needDefaultCharset) sb.AppendLine("[module: System.Runtime.InteropServices.DefaultCharSet(System.Runtime.InteropServices.CharSet.Unicode)]");
			
			if (needTargetFramework) sb.AppendLine($"[assembly: System.Runtime.Versioning.TargetFramework(\"{AppContext.TargetFrameworkName}\")]");
			if (needAssemblyTitle) sb.AppendLine($"[assembly: System.Reflection.AssemblyTitle(\"{_meta.Name}\")]");
			
			if (_meta.Role is MCRole.miniProgram or MCRole.editorExtension) {
				_GetDllPaths();
				if (_dr != null) { //add RefPaths attribute to resolve paths of managed dlls at run time
					foreach (var v in _dr) {
						sb.Append(rflags.Has(MiniProgram_.MPFlags.RefPaths) ? "|" : $"[assembly: Au.Types.RefPaths(@\"");
						sb.Append(v.Value);
						rflags |= MiniProgram_.MPFlags.RefPaths;
					}
					sb.AppendLine("\")]");
				}
				if (_dn != null) { //add NativePaths attribute to resolve paths of native dlls at run time
					foreach (var v in _dn) {
						sb.Append(rflags.Has(MiniProgram_.MPFlags.NativePaths) ? "|" : $"[assembly: Au.Types.NativePaths(@\"");
						sb.Append(v.Value);
						rflags |= MiniProgram_.MPFlags.NativePaths;
					}
					sb.AppendLine("\")]");
				}
			}
			
			if (_meta.TestInternal != null) {
				//https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
				//IgnoresAccessChecksToAttribute is defined in Au assembly.
				//	Could define here, but then warning "already defined in assembly X" when compiling 2 projects (meta pr) with that attribute.
				//	never mind: Au.dll must exist by the compiled assembly, even if not used for other purposes.
				foreach (var v in _meta.TestInternal) sb.AppendLine($"[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"{v}\")]");
				//sb.Append(@"
				//namespace System.Runtime.CompilerServices {
				//[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
				//public class IgnoresAccessChecksToAttribute : Attribute {
				//	public IgnoresAccessChecksToAttribute(string assemblyName) { AssemblyName = assemblyName; }
				//	public string AssemblyName { get; }
				//}}");
			}
			
			var fm = _meta.MainFile.f;
			sb.AppendLine($"[assembly: Au.Types.PathInWorkspace(@\"{fm.ItemPath}\", @\"{fm.FilePath}\")]");
			if (_meta.Role == MCRole.exeProgram) {
				sb.AppendLine(@"class ModuleInit__ { [System.Runtime.CompilerServices.ModuleInitializer] internal static void Init() { Au.script.AppModuleInit_(auCompiler: true); }}");
			}
			
			string code = sb.ToString(); //print.it(code);
			var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview)) as CSharpSyntaxTree;
			//insert as first, else user's module initializers would run before. Same speed.
			//_compilation = _compilation.AddSyntaxTrees(tree);
			_compilation = _compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(_compilation.SyntaxTrees.Insert(0, tree));
		}
		return rflags;
	}
	
	//Gets paths of used managed and unmanaged dlls (meta nuget, r, etc).
	//Sets: _dr - managed dlls, _dn - other dlls. If compiling exeProgram, _dn also contains other files from nuget.
	//	Full paths are in dictionary values. Keys contain filenames or nuget relative paths, and for callers almost not useful.
	//Called when:
	//	Compiling exeProgram. The files will be copied to the output.
	//	Compiling miniProgram or editorExtension. Dll paths will be added to an assembly attribute. At run time will use it to find dlls.
	//Depending on meta properties, filters out unused dlls, eg 32-bit or dlls for other OS versions.
	void _GetDllPaths() {
		Dictionary<string, string> dr = null, dn = null, dr2 = null; //managed, native
		HashSet<FileNode> noDup = null;
		
		_Project(_meta);
		
		void _Project(MetaComments m) {
			if (m != _meta) if (!(noDup ??= new()).Add(m.MainFile.f)) return; //skip duplicates
			
			//managed dlls, except com and nuget
			{
				var refs = m.References.Refs;
				for (int k = m.References.DefaultRefCount; k < refs.Count; k++) {
					if (refs[k].Properties.EmbedInteropTypes || MetaReferences.IsNuget(refs[k])) continue; //com or nuget
					var path = refs[k].FilePath;
					_Add(ref dr, pathname.getName(path), path);
					
					//add managed dlls used by that dll but not specified in meta
					_Deps(path);
					void _Deps(string path) {
						string dir = null;
						using var peReader = new PEReader(filesystem.loadStream(path));
						var mr = peReader.GetMetadataReader();
						foreach (var arh in mr.AssemblyReferences) {
							var ar = mr.GetAssemblyReference(arh);
							var an = mr.GetString(ar.Name);
							if (MetaReferences.IsDefaultRef(an)) continue;
							if (dr2?.ContainsKey(an) ?? false) continue;
							dir ??= pathname.getDirectory(path, withSeparator: true);
							var path2 = dir + an + ".dll";
							if (!filesystem.exists(path2, useRawPath: true).File) continue;
							(dr2 ??= new()).Add(an, path2);
							_Deps(path2);
						}
					}
				}
			}
			
			//managed and native dlls from nuget. If exeProgram, also non-dll files.
			if (m.NugetXmlRoot is XElement xn) {
				foreach (var (package, _) in m.NugetPackages) {
					var xp = xn.Elem("package", "path", package, true);
					if (xp != null) {
						var dir = App.Model.NugetDirectoryBS + pathname.getDirectory(package);
						if (xp.Attr(out int format, "format")) {
							
							foreach (var f in xp.Elements()) {
								switch (f.Name.LocalName) { //see DNuget._Build
								case "r" or "rt":
									_Add2(ref dr, f.Value);
									break;
								case "native":
									_Add2(ref dn, f.Value);
									break;
								case "group":
									_AddGroup(f, false);
									break;
								case "natives":
									_AddGroup(f, true);
									break;
								case "other" when _meta.Role == MCRole.exeProgram:
									//copy all other files. Except XML doc, they are not in nuget.xml.
									_Add2(ref dn, f.Value, isDll: false);
									break;
								}
							}
							
							void _AddGroup(XElement x, bool native) {
								ref var d = ref (native ? ref dn : ref dr);
								
								int verPC = osVersion.minWin10 ? 100 : osVersion.minWin8_1 ? 81 : osVersion.minWin8 ? 80 : 70; //don't need Win11
								int verBest = -1;
								
								string skip = null;
								if (_meta.Role != MCRole.exeProgram) skip = @"-x86\";
								else if (_meta.Bit32) skip = @"-x64\";
								else if (!_meta.Optimize) skip = @"-x86\";
								
								string sBest = null;
								foreach (var f in x.Elements(native ? "native" : "rt")) {
									string s = f.Value; //like \runtimes\win\..., \runtimes\win-x64\..., \runtimes\win10-x64\...
									int i = 13, verDll = 0;
									if (s[i] != '-') verDll = s.ToInt(i, out i);
									
									if (skip != null && s.Eq(i, skip, true)) continue;
									
									if (_meta.Role == MCRole.exeProgram) {
										_Add2(ref d, s); //will select at run time
									} else {
										if (verDll != 81) verDll *= 10;
										if (verDll > verPC) continue;
										if (verDll > verBest) {
											verBest = verDll;
											sBest = s;
										}
									}
								}
								
								if (sBest != null) {
									_Add2(ref d, sBest);
								}
							}
						} else {
							//old format (the very first)
							//tags:
							//	"r" - .NET dll (including ref-only)
							//	"f" - all other (including unmanaged dll)
							//native dlls are in \64 and \32.
							
							foreach (var f in xp.Elements()) {
								string s = f.Value, tag = f.Name.LocalName;
								if (tag == "r") {
									_Add2(ref dr, s);
								} else if (tag == "f") {
									if (s.Ends(".dll", true)) {
										string skip = null;
										if (_meta.Role != MCRole.exeProgram) skip = @"\32";
										else if (_meta.Bit32) skip = @"\64";
										else if (!_meta.Optimize) skip = @"\32";
										if (skip != null && s.Starts(skip)) continue;
									} else if (_meta.Role != MCRole.exeProgram) continue;
									_Add2(ref dn, s);
								}
							}
						}
						
						void _Add2(ref Dictionary<string, string> d, string s, bool isDll = true)
							=> _Add(ref d, s, dir + s, isDll);
					}
				}
			}
			
			//unmanaged dlls specified in meta file
			if (m.OtherFiles != null && _meta.Role != MCRole.exeProgram) {
				foreach (var v in m.OtherFiles) {
					if (v.f.IsFolder) {
						foreach (var des in v.f.Descendants()) if (!des.IsFolder) _AddOther(des);
					} else {
						_AddOther(v.f);
					}
				}
				
				void _AddOther(FileNode f) {
					var path = f.FilePath;
					if (!path.Ends(".dll", true)) return;
					dn ??= new(StringComparer.OrdinalIgnoreCase);
					dn.TryAdd(path, path);
				}
			}
			
			if (m.ProjectReferences is { } pr)
				foreach (var v in pr) _Project(v.m);
		}
		
		void _Add(ref Dictionary<string, string> d, string s, string path, bool isDll = true) {
			if (isDll && _meta.Role == MCRole.exeProgram && _meta.Name.Eqi(pathname.getNameNoExt(s)))
				throw new InvalidOperationException($@"The program uses a dll file with the same name. Rename C# file {_meta.Name}");
			
			d ??= new(StringComparer.OrdinalIgnoreCase);
			if (d.TryAdd(s, path)) return;
			var existing = d[s]; if (existing.Eqi(path)) return;
			Debug_.Print($"compares file data of '{path}' and '{existing}'");
			if (filesystem.loadBytes(existing).AsSpan().SequenceEqual(filesystem.loadBytes(path))) return;
			
			throw new InvalidOperationException($@"Two different versions of file:
	{existing}
	{path}");
		}
		
		if (dr2 != null) {
			foreach (var (k, v) in dr2) {
				_Add(ref dr, k + ".dll", v);
			}
		}
		
		//print.it("dr");
		//print.it(dr);
		//print.it("dn");
		//print.it(dn);
		
		if (_meta.Role == MCRole.exeProgram) {
			_tpa = _GetDefaultTPA();
			if (dr != null) {
				//remove replaced defaults. Rare.
				foreach (var v in dr.Keys) {
					var fn = pathname.getNameNoExt(v);
					int i = _tpa.Find_(fn, static (s, from, to) => s[from - 1] == '|' && (to == s.Length || s[to] == '|'), true);
					if (i > 0) _tpa = _tpa.Remove(i - 1, fn.Length + 1);
				}
				
				StringBuilder b = null;
				foreach (var v in dr.Keys) {
					if (v.Starts(@"\runtimes\", true)) continue; //managed by ResolveNugetRuntimes_, because difficult in C++
					b ??= new(_tpa);
					int bs = v[0] == '\\' ? 1 : 0;
					b.Append('|').Append(v, bs, v.Length - 4 - bs);
				}
				if (b != null) _tpa = b.ToString();
			}
		}
		
		_dr = dr;
		_dn = dn;
	}
	
	static string _GetDefaultTPA() {
		if (s_sDefTPA == null) {
			var s = _NativeResources.LoadNativeResourceUtf8String_(220, 1);
			int i = s.LastIndexOf("|Au|") + 3; //remove Au.Controls etc
			s_sDefTPA = s[..i];
		}
		return s_sDefTPA;
	}
	static string s_sDefTPA;
	
	List<ResourceDescription> _CreateManagedResources(string asmName, CSharpSyntaxTree[] trees) {
		List<ResourceDescription> R = null;
		if (CompilerUtil.CreateManagedResources(_meta, asmName, trees, _ResourceException, o => { (R ??= new()).Add(new ResourceDescription(o.name, () => o.stream, true)); })) return R;
		return null;
	}
	
	void _ResourceException(Exception e, FileNode curFile) {
		var em = e.ToStringWithoutStack();
		var err = _meta.Errors;
		var f = _meta.MainFile.f;
		if (curFile == null) err.AddError(f, "Failed to add resources. " + em);
		else err.AddError(f, $"Failed to add resource '{curFile.Name}'. " + em);
	}
	
	Stream _CreateNativeResources() {
#if true
		if (_meta.Role is MCRole.exeProgram or MCRole.classLibrary) //add only version. If exe, will add icon and manifest to apphost exe. //rejected: support adding icons to dll; VS allows it.
			return _compilation.CreateDefaultWin32Resources(versionResource: true, noManifest: true, null, null);
		
		if (_meta.IconFile != null) { //add only icon. No version, no manifest.
			Stream icoStream = null;
			FileNode curFile = null;
			try {
				//if(_meta.ResFile != null) return File.OpenRead((curFile = _meta.ResFile).FilePath);
				icoStream = File.OpenRead((curFile = _meta.IconFile).FilePath);
				curFile = null;
				return _compilation.CreateDefaultWin32Resources(versionResource: false, noManifest: true, null, icoStream);
			}
			catch (Exception e) {
				icoStream?.Dispose();
				_ResourceException(e, curFile);
			}
		} else if (_GetMainFileIcon(out var stream)) {
			return _compilation.CreateDefaultWin32Resources(versionResource: false, noManifest: true, null, stream);
		}
		
		return null;
#else
			var manifest = _meta.ManifestFile;

			string manifestPath = null;
			if(manifest != null) manifestPath = manifest.FilePath;
			else if(_meta.Role == MetaRole.exeProgram /*&& _meta.ResFile == null*/) manifestPath = folders.ThisAppBS + "default.exe.manifest"; //don't: uac

			Stream manStream = null, icoStream = null;
			FileNode curFile = null;
			try {
				//if(_meta.ResFile != null) return File.OpenRead((curFile = _meta.ResFile).FilePath);
				if(manifestPath != null) { curFile = manifest; manStream = File.OpenRead(manifestPath); }
				if(_meta.IconFile != null) icoStream = File.OpenRead((curFile = _meta.IconFile).FilePath);
				curFile = null;
				return _compilation.CreateDefaultWin32Resources(versionResource: true, noManifest: manifestPath == null, manStream, icoStream);
			}
			catch(Exception e) {
				manStream?.Dispose();
				icoStream?.Dispose();
				_ResourceException(e, curFile);
				return null;
			}
#endif
	}
	
	string _AppHost(string outFile, string fileName, bool bit32) {
		//A .NET Core+ exe actually is a managed dll hosted by a native exe file known as apphost.
		//When creating an exe, VS copies template apphost from "C:\Program Files\dotnet\sdk\version\AppHostTemplate\apphost.exe"
		//	and modifies it, eg copies native resources from the dll.
		//We have own apphost exe created by the Au.AppHost project. This function copies it and modifies in a similar way like VS does.
		
		//var p1 = perf.local();
		string exeFile = DllNameToAppHostExeName(outFile, bit32);
		
		if (filesystem.exists(exeFile) && !Api.DeleteFile(exeFile)) {
			var ec = lastError.code;
			if (!(ec == Api.ERROR_ACCESS_DENIED && _RenameLockedFile(exeFile, notInCache: true))) throw new AuException(ec);
		}
		
		var appHost = folders.ThisAppBS + (bit32 ? "32" : "64") + @"\Au.AppHost.exe";
		bool done = false;
		try {
			var b = File.ReadAllBytes(appHost);
			//p1.Next();
			//write assembly name in placeholder memory. In AppHost.cpp: char s_asmName[800] = "\0hi7yl8kJNk+gqwTDFi7ekQ";
			int i = BytePtr_.AsciiFindString(b, "hi7yl8kJNk+gqwTDFi7ekQ") - 1;
			i += Encoding.UTF8.GetBytes(fileName, 0, fileName.Length, b, i);
			b[i] = 0;
			
			var res = new _NativeResources();
			if (_meta.IconFile != null) {
				_NativeResources.ICONCONTEXT ic = default;
				if (_meta.IconFile.IsFolder) {
					foreach (var des in _meta.IconFile.Descendants()) {
						if (des.IsFolder) continue;
						if (des.Name.Ends(".ico", true)) {
							res.AddIcon(des.FilePath, ref ic);
						} else if (des.Name.Ends(".xaml", true)) {
							_GetIconFromXaml(filesystem.loadText(des.FilePath), out var stream);
							res.AddIcon(stream.ToArray(), ref ic);
						}
					}
				} else {
					res.AddIcon(_meta.IconFile.FilePath, ref ic);
				}
			} else if (_GetMainFileIcon(out var stream)) {
				_NativeResources.ICONCONTEXT ic = default;
				res.AddIcon(stream.ToArray(), ref ic);
			}
			res.AddVersion(outFile);
			res.AddTpa(_tpa);
			
			string manifest = null;
			if (_meta.ManifestFile != null) manifest = _meta.ManifestFile.FilePath;
			else if (_meta.Role == MCRole.exeProgram) manifest = folders.ThisAppBS + "default.exe.manifest"; //don't: uac
			if (manifest != null) res.AddManifest(manifest);
			
			res.WriteAll(exeFile, b, bit32, _meta.Console);
			
			//speed: AV makes this slooow.
			//p1.NW();
			done = true;
		}
		finally { if (!done) Api.DeleteFile(exeFile); }
		
		return exeFile;
	}
	
	bool _GetMainFileIcon(out MemoryStream ms) {
		try {
			if (DIcons.TryGetIconFromBigDB(_meta.MainFile.f.CustomIconName, out string xaml)) {
				_GetIconFromXaml(xaml, out ms);
				return true;
			}
		}
		catch (Exception e1) { _ResourceException(e1, null); }
		ms = null;
		return false;
	}
	
	static void _GetIconFromXaml(string xaml, out MemoryStream ms) {
		ms = new MemoryStream();
		Au.Controls.KImageUtil.XamlImageToIconFile(ms, xaml, 16, 24, 32, 48, 64);
		ms.Position = 0;
	}
	
	void _CopyDlls(Stream asmStream, bool need64, bool need32) {
		asmStream.Position = 0;
		
		//note: need Au.dll and AuCpp.dll even if not used in code. It contains script.AppModuleInit_.
		CompilerUtil.CopyFileIfNeed(folders.ThisAppBS + @"Au.dll", _meta.OutputPath + @"\Au.dll");
		//note: always need both 64-bit and 32-bit AuCpp.dll, because maybe elm will have to load them into other processes.
		CompilerUtil.CopyFileIfNeed(folders.ThisAppBS + @"64\AuCpp.dll", _meta.OutputPath + @"\64\AuCpp.dll");
		CompilerUtil.CopyFileIfNeed(folders.ThisAppBS + @"32\AuCpp.dll", _meta.OutputPath + @"\32\AuCpp.dll");
		
		bool usesSqlite = CompilerUtil.UsesSqlite(asmStream);
		
		//copy managed dlls, including from nuget
		if (_dr != null) {
			foreach (var v in _dr) {
				CompilerUtil.CopyFileIfNeed(v.Value, _meta.OutputPath + "\\" + v.Key);
				
				if (!usesSqlite && !v.Value.Starts(App.Model.NugetDirectoryBS)) usesSqlite = CompilerUtil.UsesSqlite(v.Value);
			}
		}
		
		//copy other files from nuget
		if (_dn != null) {
			foreach (var v in _dn) {
				CompilerUtil.CopyFileIfNeed(v.Value, _meta.OutputPath + v.Key);
			}
		}
		
		//print.it(usesSqlite);
		if (usesSqlite) {
			if (need64) CompilerUtil.CopyFileIfNeed(folders.ThisAppBS + @"64\sqlite3.dll", _meta.OutputPath + @"\64\sqlite3.dll");
			if (need32) CompilerUtil.CopyFileIfNeed(folders.ThisAppBS + @"32\sqlite3.dll", _meta.OutputPath + @"\32\sqlite3.dll");
		}
		
		//copy meta 'file' files
		CompilerUtil.CopyMetaFileFilesOfAllProjects(_meta, _meta.OutputPath);
	}
	
	bool _RunPrePostBuildScript(bool post, string outFile) {
		if (_meta.Role == MCRole.exeProgram) outFile = DllNameToAppHostExeName(outFile, _meta.Bit32);
		return CompilerUtil.RunPrePostBuildScript(_meta, post, outFile, false);
	}
	
	/// <summary>
	/// Replaces ".dll" with "-32.exe" if bit32, else with ".exe".
	/// </summary>
	public static string DllNameToAppHostExeName(string dll, bool bit32)
		=> dll.ReplaceAt(^4.., bit32 ? "-32.exe" : ".exe");
	
	static bool _RenameLockedFile(string file, bool notInCache) {
		//If the assembly file is currently loaded, we get ERROR_SHARING_VIOLATION. But we can rename the file.
		//tested: can't rename if ERROR_USER_MAPPED_FILE or ERROR_LOCK_VIOLATION.
		string renamed = null;
		for (int i = 1; ; i++) {
			renamed = file + "'" + i.ToString();
			if (Api.MoveFileEx(file, renamed, 0)) goto g1;
			if (lastError.code != Api.ERROR_ALREADY_EXISTS) break;
			if (Api.MoveFileEx(file, renamed, Api.MOVEFILE_REPLACE_EXISTING)) goto g1;
		}
		return false;
		g1:
		if (notInCache) {
			if (s_renamedFiles == null) {
				s_renamedFiles = new List<string>();
				process.thisProcessExit += _ => _DeleteRenamedLockedFiles(null);
				s_rfTimer = new timer(_DeleteRenamedLockedFiles);
			}
			if (!s_rfTimer.IsRunning) s_rfTimer.Every(60_000);
			s_renamedFiles.Add(renamed);
		}
		return true;
	}
	static List<string> s_renamedFiles;
	static timer s_rfTimer;
	
	//SHOULDDO: remove this? Probably fails anyway. Will delete when this app starts next time.
	static void _DeleteRenamedLockedFiles(timer timer) {
		var a = s_renamedFiles;
		for (int i = a.Count; --i >= 0;) {
			if (Api.DeleteFile(a[i]) || lastError.code == Api.ERROR_FILE_NOT_FOUND) a.RemoveAt(i);
		}
		if (a.Count == 0) timer?.Stop();
	}
	
	public static void Warmup(CAW::Microsoft.CodeAnalysis.Document document) {
		//using var p1 = perf.local();
		var compilation = document.Project.GetCompilationAsync().Result;
		//compilation.GetDiagnostics(); //just makes Emit faster, and does not make the real GetDiagnostics faster first time
		//var eOpt = new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded);
		var asmStream = new MemoryStream(16000);
		compilation.Emit(asmStream);
		//compilation.Emit(asmStream, null, options: eOpt); //somehow makes slower later
		//compilation.Emit(asmStream, null, xdStream, resNat, resMan, eOpt);
	}
}

enum CCReason { Run, CompileAlways, CompileIfNeed, WpfPreview }

record CanCompileArgs(MetaComments m, CSharpSyntaxTree[] trees, CSharpCompilation compilation);
