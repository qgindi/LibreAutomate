using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Resources;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis.Text;

namespace Au.Compiler
{
	/// <summary>
	/// Compiles C# files.
	/// </summary>
	static partial class Compiler
	{
		/// <summary>
		/// Compiles C# file or project if need.
		/// Returns false if fails (C# errors etc).
		/// </summary>
		/// <param name="reason">Whether to recompile if compiled, etc. See also Remarks.</param>
		/// <param name="r">Results.</param>
		/// <param name="f">C# file. If projFolder used, must be the main file of the project.</param>
		/// <param name="projFolder">null or project folder.</param>
		/// <remarks>
		/// Must be always called in the main UI thread (Thread.CurrentThread.ManagedThreadId == 1).
		/// 
		/// Adds <see cref="MetaReferences.DefaultReferences"/>.
		/// 
		/// If f role is classFile:
		///		If CompReason.Run, does not compile (just parses meta), sets r.role=classFile and returns false.
		///		Else compiles but does not create output files.
		/// </remarks>
		public static bool Compile(ECompReason reason, out CompResults r, FileNode f, FileNode projFolder = null) {
			Debug.Assert(Thread.CurrentThread.ManagedThreadId == 1);
			r = null;
			var cache = XCompiled.OfWorkspace(f.Model);
			bool isCompiled = reason != ECompReason.CompileAlways && cache.IsCompiled(f, out r, projFolder);

			//print.it("isCompiled=" + isCompiled);

			if (!isCompiled) {
				bool ok = false;
				Action aFinally = null;
				try {
					ok = _Compile(reason == ECompReason.Run, f, out r, projFolder, out aFinally);
				}
				catch (Exception ex) {
					//print.it($"Failed to compile '{f.Name}'. {ex.ToStringWithoutStack()}");
					print.it($"Failed to compile '{f.Name}'. {ex}");
				}
				finally {
					aFinally?.Invoke();
				}

				if (!ok) {
					cache.Remove(f, false);
					return false;
				}
			}

			return true;
		}

		/// <summary>_Compile() output assembly info.</summary>
		public record CompResults
		{
			/// <summary>C# file name without ".cs".</summary>
			public string name;

			/// <summary>Full path of assembly file.</summary>
			public string file;

			public ERole role;
			public EIfRunning ifRunning;
			public EUac uac;
			public MiniProgram_.EFlags flags;
			public bool bit32;

			/// <summary>The assembly is normal .exe or .dll file, not in cache. If exe, its dependencies were copied to its directory.</summary>
			public bool notInCache;
		}

		static bool _Compile(bool forRun, FileNode f, out CompResults r, FileNode projFolder, out Action aFinally) {
			//print.it("COMPILE");

			var p1 = perf.local();
			r = new CompResults();
			aFinally = null;

			var m = new MetaComments();
			if (!m.Parse(f, projFolder, EMPFlags.PrintErrors)) return false;
			var err = m.Errors;
			p1.Next('m');

			bool needOutputFiles = m.Role != ERole.classFile;

			//if for run, don't compile if f role is classFile
			if (forRun && !needOutputFiles) {
				r.role = ERole.classFile;
				return false;
			}

			XCompiled cache = XCompiled.OfWorkspace(f.Model);
			string outPath = null, outFile = null, fileName = null;
			bool notInCache = false;
			if (needOutputFiles) {
				if (notInCache = m.OutputPath != null) {
					outPath = m.OutputPath;
					fileName = m.Name + ".dll";
				} else {
					outPath = cache.CacheDirectory;
					fileName = f.IdString + ".dll"; //note: must have ".dll" extension, else somehow don't work GetModuleHandle, Process.GetCurrentProcess().Modules etc
				}
				outFile = outPath + "\\" + fileName;
				filesystem.createDirectory(outPath);
			}

			if (m.PreBuild.f != null && !_RunPrePostBuildScript(false, m, outFile)) return false;

			var pOpt = m.CreateParseOptions();
			var trees = new CSharpSyntaxTree[m.CodeFiles.Count];
			for (int i = 0; i < trees.Length; i++) {
				var f1 = m.CodeFiles[i];
				trees[i] = CSharpSyntaxTree.ParseText(f1.code, pOpt, f1.f.FilePath, Encoding.UTF8) as CSharpSyntaxTree;
				//info: file path is used later in several places: in compilation error messages, run time stack traces (from PDB), Visual Studio debugger, etc.
				//	Our print.Server.SetNotifications callback will convert file/line info to links. It supports compilation errors and run time stack traces.
			}
			p1.Next('t');

			string asmName = m.Name;
			if (m.Role == ERole.editorExtension) //cannot load multiple assemblies with same name
				asmName = asmName + "|" + Guid.NewGuid().ToString(); //use GUID, not counter, because may be loaded old assembly from cache with same counter value

			if (m.TestInternal is string[] testInternal) {
				InternalsVisible.Add(asmName, testInternal);
				aFinally += () => InternalsVisible.Remove(asmName); //this func is called from try/catch/finally which calls aFinally
			}

			var cOpt = m.CreateCompilationOptions();
			var compilation = CSharpCompilation.Create(asmName, trees, m.References.Refs, cOpt);
			p1.Next('c');

			string xdFile = null;
			Stream xdStream = null;
			Stream resNat = null;
			List<ResourceDescription> resMan = null;
			EmitOptions eOpt = null;

			if (needOutputFiles) {
				_AddAttributesEtc(ref compilation, m, out bool refPaths);
				p1.Next('a');
				if (refPaths) r.flags |= MiniProgram_.EFlags.RefPaths;

				//if empty script, error "no Main". Users would not understand why.
				if (m.Role != ERole.classLibrary) {
					var diag1 = compilation.GetDiagnostics();
					if (diag1.Any(o => o.Id == "CS5001")) { //no Main
						var st = trees[m.CodeFiles.IndexOf(m.MainFile)];
						var cu = st.GetCompilationUnitRoot();
						var code2 = m.MainFile.code;
						int i = cu.Members.Any() ? cu.Members.Span.Start : code2.Length;
						code2 = code2.Insert(i, "\r\n{}\r\n");
						compilation = compilation.ReplaceSyntaxTree(st, st.WithChangedText(SourceText.From(code2, Encoding.UTF8)) as CSharpSyntaxTree);
					}
					p1.Next('d');
				}

				//Create debug info always. It is used for run-time error links.
				//Embed it in assembly. It adds < 1 KB. Almost the same compiling speed. Same loading speed.
				//Don't use classic pdb file. It is 14 KB, 2 times slower compiling, slower loading; error with .NET Core: Unexpected error writing debug information -- 'The version of Windows PDB writer is older than required: 'diasymreader.dll''.
				eOpt = new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded);

				if (m.XmlDoc) xdStream = filesystem.waitIfLocked(() => File.Create(xdFile = outPath + "\\" + m.Name + ".xml"));

				resMan = _CreateManagedResources(m);
				if (err.ErrorCount != 0) { err.PrintAll(); return false; }

				resNat = _CreateNativeResources(m, compilation);
				if (err.ErrorCount != 0) { err.PrintAll(); return false; }

				//EmbeddedText.FromX //it seems we can embed source code in PDB. Not tested.
			}

			p1.Next();
			var asmStream = new MemoryStream(16000);
			var emitResult = compilation.Emit(asmStream, null, xdStream, resNat, resMan, eOpt);

			if (needOutputFiles) {
				xdStream?.Dispose();
				resNat?.Dispose(); //info: compiler disposes resMan
			}
			p1.Next('e');

			var diag = emitResult.Diagnostics;
			if (!diag.IsEmpty) {
				foreach (var d in diag) {
					if (d.Severity == DiagnosticSeverity.Hidden) continue;
					err.AddErrorOrWarning(d, f);
					if (d.Severity == DiagnosticSeverity.Error && d.Id == "CS0009") MetaReferences.RemoveBadReference(d.GetMessage());
				}
				err.PrintAll();
			}
			if (!emitResult.Success) {
				if (needOutputFiles) {
					Api.DeleteFile(outFile);
					if (xdFile != null) Api.DeleteFile(xdFile);
				}
				return false;
			}

			if (needOutputFiles) {
				if (m.Role == ERole.miniProgram) {
					//is Main with [MTAThread]? Default STA, even if Main without [STAThread].
					if (compilation.GetEntryPoint(default)?.GetAttributes().Any(o => o.ToString() == "System.MTAThreadAttribute") ?? false) r.flags |= MiniProgram_.EFlags.MTA;

					if (m.Console) r.flags |= MiniProgram_.EFlags.Console;
				}

				//create assembly file
				p1.Next();
				gSave:
#if true
				var hf = Api.CreateFile(outFile, Api.GENERIC_WRITE, 0, default, Api.CREATE_ALWAYS);
				if (hf.Is0) {
					var ec = lastError.code;
					if (ec == Api.ERROR_SHARING_VIOLATION && _RenameLockedFile(outFile, notInCache: notInCache)) goto gSave;
					throw new AuException(ec, outFile);
				}
				var b = asmStream.GetBuffer();

				//prevent AV full dll scan when loading using LoadFromStream (now not used). Will load bytes, unxor and load assembly from stream. Will fully scan once, when loading assembly.
				//if (m.Role == ERole.editorExtension) for (int i = 0, n = (int)asmStream.Length; i < n; i++) b[i] ^= 1;

				using (hf) if (!Api.WriteFile2(hf, b.AsSpan(0, (int)asmStream.Length), out _)) throw new AuException(0);
#else //same speed, but I like code without exceptions
				try {
						using var fileStream = File.Create(outFile, (int)asmStream.Length);
						asmStream.Position = 0;
						asmStream.CopyTo(fileStream);
					}
					catch(IOException e1) when((e1.HResult & 0xffff) == Api.ERROR_SHARING_VIOLATION) {
						if(!_RenameLockedFile(outFile)) throw;
						goto gSave;
					}
				}
#endif
				//saving would be fast, but with AV can take half of time.
				//	With WD now fast, but used to be slow. Now on save WD scans async, and on load scans only if still not scanned, eg if loading soon after saving.
				//	With Avast now the same as with WD.
				p1.Next('s');
				r.file = outFile;

				if (m.Role == ERole.exeProgram) {
					bool need32 = m.Bit32 || osVersion.is32BitOS;
					bool need64 = !need32 || m.Optimize;
					need32 |= m.Optimize;

					//copy app host template exe, add native resources, set assembly name, set console flag if need
					if (need64) _AppHost(outFile, fileName, m, bit32: false);
					if (need32) _AppHost(outFile, fileName, m, bit32: true);
					p1.Next('h'); //very slow with AV. Eg with WD this part makes whole compilation several times slower.

					//copy dlls to the output directory
					_CopyDlls(m, asmStream, need64: need64, need32: need32);
					p1.Next('d');

					//copy config file to the output directory
					//var configFile = exeFile + ".config";
					//if(m.ConfigFile != null) {
					//	r.hasConfig = true;
					//	_CopyFileIfNeed(m.ConfigFile.FilePath, configFile);
					//} else if(filesystem.exists(configFile, true).isFile) {
					//	filesystem.delete(configFile);
					//}
				}
			}

			if (m.PostBuild.f != null && !_RunPrePostBuildScript(true, m, outFile)) return false;

			if (needOutputFiles) {
				cache.AddCompiled(f, outFile, m, r.flags);

				if (m.Role == ERole.classLibrary) MetaReferences.UncacheOldFiles();

				if (notInCache) print.it($"<>Output folder: <link>{m.OutputPath}<>");
			}

			r.name = m.Name;
			r.role = m.Role;
			r.ifRunning = m.IfRunning;
			r.uac = m.Uac;
			r.bit32 = m.Bit32;
			r.notInCache = notInCache;

			//#if TRACE
			//p1.NW('C');
			//#endif
			//print.it("<><c red>compiling<>");
			return true;

			//SHOULDDO: rebuild if missing apphost. Now rebuilds only if missing dll.
		}

		public static void Warmup(Microsoft.CodeAnalysis.Document document) {
			//using var p1 = perf.local();
			var compilation = document.Project.GetCompilationAsync().Result;
			//compilation.GetDiagnostics(); //just makes Emit faster, and does not make the real GetDiagnostics faster first time
			//var eOpt = new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded);
			var asmStream = new MemoryStream(16000);
			compilation.Emit(asmStream);
			//compilation.Emit(asmStream, null, options: eOpt); //somehow makes slower later
			//compilation.Emit(asmStream, null, xdStream, resNat, resMan, eOpt);
		}

		/// <summary>
		/// Adds some module/assembly attributes. Also adds module initializer for role exeProgram.
		/// </summary>
		static void _AddAttributesEtc(ref CSharpCompilation compilation, MetaComments m, out bool refPaths) {
			refPaths = false;
			//bool needDefaultCharset = true;
			//foreach (var v in compilation.SourceModule.GetAttributes()) {
			//	//print.it(v.AttributeClass.Name);
			//	if (v.AttributeClass.Name == "DefaultCharSetAttribute") { needDefaultCharset = false; break; }
			//}
			bool needTargetFramework = false;
			if (m.Role is ERole.exeProgram or ERole.classLibrary) {
				needTargetFramework = true; //need [TargetFramework] for exeProgram, else AppContext.TargetFrameworkName will return null: => Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
				foreach (var v in compilation.Assembly.GetAttributes()) {
					//print.it(v.AttributeClass.Name);
					if (v.AttributeClass.Name == "TargetFrameworkAttribute") { needTargetFramework = false; break; }
				}
			}

			using (new StringBuilder_(out var sb)) {
				//sb.AppendLine("using System.Reflection;using System.Runtime.InteropServices;");

				//rejected. Now in global.cs, #if !NO_DEFAULT_CHARSET_UNICODE
				//if (needDefaultCharset) sb.AppendLine("[module: System.Runtime.InteropServices.DefaultCharSet(System.Runtime.InteropServices.CharSet.Unicode)]");

				if (needTargetFramework) sb.AppendLine($"[assembly: System.Runtime.Versioning.TargetFramework(\"{AppContext.TargetFrameworkName}\")]");

				if (m.Role is ERole.miniProgram) {
					var ta = folders.ThisAppBS;
					var refs = m.References.Refs;
					for (int k = MetaReferences.DefaultReferences.Count; k < refs.Count; k++) {
						var path = refs[k].FilePath;
						if (path.Starts(ta, true)) {
							int i = ta.Length, j = path.IndexOf('\\', i);
							if (j < 0) continue;
							if (j - i == 9 && path.Eq(i, "Libraries", true) && path.IndexOf('\\', j + 1) < 0) continue;
						}
						if (!path.Ends(".dll", true)) continue;
						sb.Append(refPaths ? "|" : $"[assembly: Au.Types.RefPaths(@\"").Append(path);
						refPaths = true;
					}
					if (refPaths) sb.AppendLine("\")]");
				}

				if (m.TestInternal != null) {
					//https://www.strathweb.com/2018/10/no-internalvisibleto-no-problem-bypassing-c-visibility-rules-with-roslyn/
					//IgnoresAccessChecksToAttribute is defined in Au assembly.
					//	Could define here, but then warning "already defined in assembly X" when compiling 2 projects (meta pr) with that attribute.
					//	never mind: Au.dll must exist by the compiled assembly, even if not used for other purposes.
					foreach (var v in m.TestInternal) sb.AppendLine($"[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo(\"{v}\")]");
					//sb.Append(@"
					//namespace System.Runtime.CompilerServices {
					//[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
					//public class IgnoresAccessChecksToAttribute : Attribute {
					//	public IgnoresAccessChecksToAttribute(string assemblyName) { AssemblyName = assemblyName; }
					//	public string AssemblyName { get; }
					//}}");
				}

				if (m.Role is ERole.miniProgram or ERole.exeProgram) {
					sb.AppendLine($"[assembly: Au.Types.PathInWorkspace(\"{m.MainFile.f.ItemPath.Escape()}\")]");
					if (m.Role == ERole.exeProgram) {
						sb.AppendLine(@"class ModuleInit__ { [System.Runtime.CompilerServices.ModuleInitializer] internal static void Init() { Au.script.AppModuleInit_(); }}");
					}
				}

				string code = sb.ToString(); //print.it(code);
				var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview)) as CSharpSyntaxTree;
				//insert as first, else user's module initializers would run before. Same speed.
				//compilation = compilation.AddSyntaxTrees(tree);
				compilation = compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(compilation.SyntaxTrees.Insert(0, tree));
			}
		}

		static List<ResourceDescription> _CreateManagedResources(MetaComments m) {
			var a = m.Resources;
			if (a.NE_()) return null;
			var R = new List<ResourceDescription>();
			ResourceWriter rw = null;
			MemoryStream stream = null;
			string resourcesName = m.Name + ".g.resources";
			FileNode curFile = null;

			void _End() {
				curFile = null;
				if (rw == null) return;
				rw.Generate();
				var st = stream; stream = null; //to create new lambda delegate
				st.Position = 0;
				R.Add(new ResourceDescription(resourcesName, () => st, true));
				rw = null;
			}

			void _Add(FileNode f, string resType, FileNode folder = null) {
				curFile = f;
				string name = f.Name, path = f.FilePath;
				if (folder != null) for (var pa = f.Parent; pa != folder; pa = pa.Parent) name = pa.Name + "/" + name;
				//print.it(f, resType, folder, name, path);
				if (resType == "embedded") {
					R.Add(new ResourceDescription(name, () => filesystem.loadStream(path), true));
				} else {
					name = name.Lower(); //else pack URI does not work
					rw ??= new ResourceWriter(stream = new MemoryStream());
					switch (resType) {
					case null:
						//rw.AddResource(name, File.OpenRead(path), closeAfterWrite: true); //no, would not close on error
						rw.AddResource(name, new MemoryStream(filesystem.loadBytes(path)));
						break;
					case "byte[]":
						rw.AddResource(name, filesystem.loadBytes(path));
						break;
					case "string":
						rw.AddResource(name, filesystem.loadText(path));
						break;
					case "strings":
						var csv = csvTable.load(path);
						if (csv.ColumnCount != 2) throw new ArgumentException("CSV must contain 2 columns separated with ,");
						foreach (var row in csv.Rows) rw.AddResource(row[0], row[1]);
						break;
					default: throw new ArgumentException("error in meta: Incorrect /suffix");
					}
				}
			}

			try {
				foreach (var v in a) {
					//if (v.f == null) { // /resources //rejected
					//	_End();
					//	resourcesName = v.s + ".resources";
					//} else
					if (v.f.IsFolder) {
						foreach (var des in v.f.Descendants()) if (!des.IsFolder) _Add(des, v.s, v.f);
					} else {
						_Add(v.f, v.s);
					}
				}
				_End();
			}
			catch (Exception e) {
				rw?.Dispose();
				_ResourceException(e, m, curFile);
				return null;
			}
			//note: don't Close/Dispose rw. It closes stream. Compiler will close it. There is no other disposable data in rw.
			return R;
		}

		static void _ResourceException(Exception e, MetaComments m, FileNode curFile) {
			var em = e.ToStringWithoutStack();
			var err = m.Errors;
			var f = m.MainFile.f;
			if (curFile == null) err.AddError(f, "Failed to add resources. " + em);
			else err.AddError(f, $"Failed to add resource '{curFile.Name}'. " + em);
		}

		static Stream _CreateNativeResources(MetaComments m, CSharpCompilation compilation) {
#if true
			if (m.Role == ERole.exeProgram || m.Role == ERole.classLibrary) //add only version. If exe, will add icon and manifest to apphost exe. //rejected: support adding icons to dll; VS allows it.
				return compilation.CreateDefaultWin32Resources(versionResource: true, noManifest: true, null, null);

			if (m.IconFile != null) { //add only icon. No version, no manifest.
				Stream icoStream = null;
				FileNode curFile = null;
				try {
					//if(m.ResFile != null) return File.OpenRead((curFile = m.ResFile).FilePath);
					icoStream = File.OpenRead((curFile = m.IconFile).FilePath);
					curFile = null;
					return compilation.CreateDefaultWin32Resources(versionResource: false, noManifest: true, null, icoStream);
				}
				catch (Exception e) {
					icoStream?.Dispose();
					_ResourceException(e, m, curFile);
				}
			} else if (_GetMainFileIcon(m, out var stream)) {
				return compilation.CreateDefaultWin32Resources(versionResource: false, noManifest: true, null, stream);
			}

			return null;
#else
			var manifest = m.ManifestFile;

			string manifestPath = null;
			if(manifest != null) manifestPath = manifest.FilePath;
			else if(m.Role == ERole.exeProgram /*&& m.ResFile == null*/) manifestPath = folders.ThisAppBS + "default.exe.manifest"; //don't: uac

			Stream manStream = null, icoStream = null;
			FileNode curFile = null;
			try {
				//if(m.ResFile != null) return File.OpenRead((curFile = m.ResFile).FilePath);
				if(manifestPath != null) { curFile = manifest; manStream = File.OpenRead(manifestPath); }
				if(m.IconFile != null) icoStream = File.OpenRead((curFile = m.IconFile).FilePath);
				curFile = null;
				return compilation.CreateDefaultWin32Resources(versionResource: true, noManifest: manifestPath == null, manStream, icoStream);
			}
			catch(Exception e) {
				manStream?.Dispose();
				icoStream?.Dispose();
				_ResourceException(e, m, curFile);
				return null;
			}
#endif
		}

		static unsafe string _AppHost(string outFile, string fileName, MetaComments m, bool bit32) {
			//A .NET Core+ exe actually is a managed dll hosted by a native exe file known as apphost.
			//When creating an exe, VS copies template apphost from "C:\Program Files\dotnet\sdk\version\AppHostTemplate\apphost.exe" and modifies it, eg copies native resources from the dll.
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
				fixed (byte* p = b) {
					int i = BytePtr_.AsciiFindString(p, b.Length, "hi7yl8kJNk+gqwTDFi7ekQ") - 1;
					i += Encoding.UTF8.GetBytes(fileName, 0, fileName.Length, b, i);
					b[i] = 0;
				}

#if true
				var res = new _Resources();
				if (m.IconFile != null) {
					_Resources.ICONCONTEXT ic = default;
					if (m.IconFile.IsFolder) {
						foreach (var des in m.IconFile.Descendants()) {
							if (des.IsFolder) continue;
							if (des.Name.Ends(".ico", true)) {
								res.AddIcon(des.FilePath, ref ic);
							} else if (des.Name.Ends(".xaml", true)) {
								_GetIconFromXaml(filesystem.loadText(des.FilePath), out var stream);
								res.AddIcon(stream.ToArray(), ref ic);
							}
						}
					} else {
						res.AddIcon(m.IconFile.FilePath, ref ic);
					}
				} else if (_GetMainFileIcon(m, out var stream)) {
					_Resources.ICONCONTEXT ic = default;
					res.AddIcon(stream.ToArray(), ref ic);
				}
				res.AddVersion(outFile);

				string manifest = null;
				if (m.ManifestFile != null) manifest = m.ManifestFile.FilePath;
				else if (m.Role == ERole.exeProgram) manifest = folders.ThisAppBS + "default.exe.manifest"; //don't: uac
				if (manifest != null) res.AddManifest(manifest);

				res.WriteAll(exeFile, b, bit32, m.Console);

				//speed: AV makes this slooow.
#else
				//if console, write IMAGE_SUBSYSTEM_WINDOWS_CUI
				if(m.Console) b[0x14c] = 3; //todo: different if bit32

				for(int i = 5; ; i += Math.Min(i, 100)) { //retry, because sometimes access violation, maybe is open by AV
					try {
						File.WriteAllBytes(exeFile, b);
						//p1.Next('c');
						//copy native resources from assembly dll. Don't need native resources in dll (only version), but this is how VS does.
						using var u = new Microsoft.NET.HostModel.ResourceUpdater(exeFile);
						u.AddResourcesFromPEImage(outFile).Update();
					}
					catch when(i < 700) { //WriteAllBytes usually throws IOException; ResourceUpdater undocumented.
						Thread.Sleep(i);
						continue;
					}
					break;
				}

				//speed: Windows Defender makes this the slowest part of the compilation.
				//	Whole compilation 4 times slower if creating only 64bit or 32bit exe, and 6 times slower if both.
				//	With the above code whole compilation in slowest cases is 2 times faster that with this, eg 180 -> 90 ms.
				//	Workaround: add the output folder to AV exclusions.

				//or
				//Microsoft.NET.HostModel.AppHost.HostWriter.CreateAppHost(appHost, exeFile, fileName, !m.Console, outFile); //nuget Microsoft.NET.HostModel
				//works, but: 1. Need nuget Microsoft.NET.HostModel, or copy more code from its source. 2. Apphost exe must be console; can't because used not only here.
				//	Most of the above code is like in HostWriter.CreateAppHost.
#endif
				//p1.NW();
				done = true;
			}
			finally { if (!done) Api.DeleteFile(exeFile); }

			return exeFile;
		}

		static bool _GetMainFileIcon(MetaComments m, out MemoryStream ms) {
			try {
				if (DIcons.TryGetIconFromBigDB(m.MainFile.f.CustomIconName, out string xaml)) {
					_GetIconFromXaml(xaml, out ms);
					return true;
				}
			}
			catch (Exception e1) { _ResourceException(e1, m, null); }
			ms = null;
			return false;
		}

		static void _GetIconFromXaml(string xaml, out MemoryStream ms) {
			ms = new MemoryStream();
			Au.Controls.KImageUtil.XamlImageToIconFile(ms, xaml, 16, 24, 32, 48, 64);
			ms.Position = 0;
		}

		static void _CopyDlls(MetaComments m, Stream asmStream, bool need64, bool need32) {
			asmStream.Position = 0;
			using var pr = new PEReader(asmStream, PEStreamOptions.LeaveOpen);
			var mr = pr.GetMetadataReader();
			var usedRefs = mr.AssemblyReferences.Select(handle => mr.GetString(mr.GetAssemblyReference(handle).Name)).ToArray();
			//print.it(usedRefs); print.it("---");

			bool _CopyRefIfNeed(string sFrom, string sTo) {
				if (!usedRefs.Contains(pathname.getNameNoExt(sFrom), StringComparer.OrdinalIgnoreCase)) return false;
				_CopyFileIfNeed(sFrom, sTo);
				return true;
			}

			if (_CopyRefIfNeed(typeof(wnd).Assembly.Location, m.OutputPath + @"\Au.dll")) {
				if (need64) _CopyFileIfNeed(folders.ThisAppBS + @"64\AuCpp.dll", m.OutputPath + @"\64\AuCpp.dll");
				if (need32) _CopyFileIfNeed(folders.ThisAppBS + @"32\AuCpp.dll", m.OutputPath + @"\32\AuCpp.dll");
			}

			var refs = m.References.Refs;
			for (int i = MetaReferences.DefaultReferences.Count; i < refs.Count; i++) {
				var s1 = refs[i].FilePath;
				var s2 = m.OutputPath + "\\" + pathname.getName(s1);
				//print.it(s1, s2);
				_CopyRefIfNeed(s1, s2);
			}

			//copy unmanaged dlls
			bool usesSqlite = false;
			foreach (var handle in mr.TypeReferences) {
				var tr = mr.GetTypeReference(handle);
				//print.it(mr.GetString(tr.Name), mr.GetString(tr.Namespace));
				string type = mr.GetString(tr.Name);
				if ((type.Starts("sqlite") && mr.GetString(tr.Namespace) == "Au") || (type.Starts("SL") && mr.GetString(tr.Namespace) == "Au.Types")) {
					usesSqlite = true;
					break;
				}
			}
			//print.it(usesSqlite);
			if (usesSqlite) {
				if (need64) _CopyFileIfNeed(folders.ThisAppBS + @"64\sqlite3.dll", m.OutputPath + @"\64\sqlite3.dll");
				if (need32) _CopyFileIfNeed(folders.ThisAppBS + @"32\sqlite3.dll", m.OutputPath + @"\32\sqlite3.dll");
			}
		}

		static void _CopyFileIfNeed(string sFrom, string sTo) {
			//print.it(sFrom);
			if (filesystem.getProperties(sTo, out var p2, FAFlags.UseRawPath) //if exists
				&& filesystem.getProperties(sFrom, out var p1, FAFlags.UseRawPath)
				&& p2.LastWriteTimeUtc == p1.LastWriteTimeUtc
				&& p2.Size == p1.Size) return;
			filesystem.copy(sFrom, sTo, FIfExists.Delete);
		}

		static bool _RunPrePostBuildScript(bool post, MetaComments m, string outFile) {
			var x = post ? m.PostBuild : m.PreBuild;
			string[] args;
			if (x.s == null) {
				args = new string[] { _OutputFile() };
			} else {
				args = StringUtil.CommandLineToArray(x.s);

				//replace variables like $(variable)
				var f = m.MainFile.f;
				if (s_rx1 == null) s_rx1 = new regexp(@"\$\((\w+)\)");
				string _ReplFunc(RXMatch k) {
					switch (k[1].Value) {
					case "outputFile": return _OutputFile();
					case "outputPath": return m.OutputPath;
					case "source": return f.ItemPath;
					case "role": return m.Role.ToString();
					case "optimize": return m.Optimize ? "true" : "false";
					case "bit32": return m.Bit32 ? "true" : "false";
					default: throw new ArgumentException("error in meta: unknown variable " + k.Value);
					}
				}
				for (int i = 0; i < args.Length; i++) args[i] = s_rx1.Replace(args[i], _ReplFunc);
			}

			string _OutputFile() => m.Role == ERole.exeProgram ? DllNameToAppHostExeName(outFile, m.Bit32 || osVersion.is32BitOS) : outFile;

			bool ok = Compile(ECompReason.Run, out var r, x.f);
			if (r.role != ERole.editorExtension) throw new ArgumentException($"meta of '{x.f.Name}' must contain role editorExtension");
			if (!ok) return false;

			RunAssembly.Run(r.file, args, handleExceptions: false);
			return true;
		}
		static regexp s_rx1;

		/// <summary>
		/// Replaces ".dll" with "-32.exe" if bit32, else with ".exe".
		/// </summary>
		public static string DllNameToAppHostExeName(string dll, bool bit32)
			=> dll.ReplaceAt(dll.Length - 4, 4, bit32 ? "-32.exe" : ".exe");

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
	}

	public enum ECompReason { Run, CompileAlways, CompileIfNeed }

	public static class InternalsVisible
	{
		static ConcurrentDictionary<string, string[]> _d = new();

		static InternalsVisible() {
			PEAssembly.AuInternalsVisible = _Callback;
		}

		//called from any thread
		static bool _Callback(string thisName, string toName, bool source) {
			if (_d.TryGetValue(toName, out var a)) {
				if (!source) {
					foreach (var v in a)
						if (v == thisName)
							return true;
				} else if (thisName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) {
					foreach (var v in a)
						if (v.Length == thisName.Length - 3 && thisName.StartsWith(v, StringComparison.Ordinal))
							return true;
				}

			}
			return false;
		}

		public static void Add(string asmName, string[] testInternals) {
			_d[asmName] = testInternals;
		}

		public static void Remove(string asmName) {
			_d.TryRemove(asmName, out _);
		}

		public static void Clear() {
			_d.Clear();
		}
	}
}
