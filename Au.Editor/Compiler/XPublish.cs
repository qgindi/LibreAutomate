using Au.Compiler;
using Au.Controls;
using System.Xml.Linq;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class XPublish {
	MetaComments _meta;
	string _csprojFile;
	
	public async void Publish() {
		var cmd = App.Commands[nameof(Menus.Run.Publish)];
		if (!cmd.Enabled) return;
		cmd.Enable(false);
		try {
			var b = new wpfBuilder("Publish").WinProperties(resizeMode: ResizeMode.NoResize, showInTaskbar: false);
			b.R.Add(out KCheckBox cSingle, "Single file").Checked(0 == (1 & App.Settings.publish));
			b.R.Add(out KCheckBox cNet, "Add .NET Runtime").Checked(0 != (2 & App.Settings.publish));
			b.R.Add(out KCheckBox cR2R, "ReadyToRun").Checked(0 != (4 & App.Settings.publish));
			b.R.StartOkCancel().AddOkCancel().AddButton("Help", _ => HelpUtil.AuHelp("editor/Publish")).Width(70).End();
			b.End();
			if (!b.ShowDialog(App.Wmain)) return;
			App.Settings.publish = (cSingle.IsChecked ? 0 : 1) | (cNet.IsChecked ? 2 : 0) | (cR2R.IsChecked ? 4 : 0);
			//FUTURE: maybe support publish profiles like in VS: <PublishProfileFullPath>
			
			if (!CreateCsproj(singleFile: cSingle.IsChecked, selfContained: cNet.IsChecked, readyToRun: cR2R.IsChecked)) return;
			
			var outFile = $@"{_meta.OutputPath}\csproj\bin\Release\win-x{(_meta.Bit32 ? "86" : "64")}\publish\{_meta.Name}.exe";
			CompilerUtil.DeleteExeFile(outFile);
			if (_meta.PreBuild.f != null && !CompilerUtil.RunPrePostBuildScript(_meta, false, outFile, true)) return;
			bool ok = await Task.Run(() => {
				static void _Print(string s) {
					if (s.RxMatch(@"^(.+? -> )(.+)$", out var m) && filesystem.exists(m[2].Value).Directory) {
						string dir = m[2].Value;
						s = $"<>{m[1]}<link>{dir}<>";
						
						//delete empty subdirs like "en", "fr". Created when selfContained=true and singleFile=false, and not deleted when changed these options.
						foreach (var v in filesystem.enumDirectories(dir)) Api.RemoveDirectory(v.FullPath);
					}
					print.it(s);
				}
				
				var cl = $"publish \"{_csprojFile}\" -c Release";
				print.it($"<><c blue>dotnet {cl}<>");
				if (0 != run.console(_Print, "dotnet.exe", cl)) { print.it("Failed"); return false; }
				return true;
			});
			if (!ok) return;
			if (_meta.PostBuild.f != null && !CompilerUtil.RunPrePostBuildScript(_meta, true, outFile, true)) return;
			print.it("==== DONE ====");
		}
		catch (Exception e1) { print.it(e1); }
		finally {
			filesystem.delete(folders.ThisAppTemp + "publish", FDFlags.CanFail);
			cmd.Enable(true);
		}
	}
	
	public bool CreateCsproj(bool singleFile = false, bool selfContained = false, bool readyToRun = false) {
		App.Model.Save.TextNowIfNeed(onlyText: true);
		
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return false;
		var f = doc.EFile;
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		if (!f.IsCodeFile) return false;
		
		_meta = new MetaComments(MCFlags.Publish | MCFlags.PrintErrors);
		if (!_meta.Parse(f, projFolder)) return false;
		if (_meta.Role is not (MCRole.miniProgram or MCRole.exeProgram)) return _Err("expected role exeProgram or miniProgram");
		if (_meta.TestInternal != null) return _Err("testInternal not supported");
		
		var outDir = $@"{_meta.OutputPath ?? MetaComments.GetDefaultOutputPath(f, _meta.Role, withEnvVar: false)}\csproj";
		filesystem.createDirectory(outDir);
		
		var xroot = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));
		var xpg = new XElement("PropertyGroup");
		xroot.Add(xpg);
		
		_Add(xpg, "TargetFramework", "net8.0-windows");
#if !NET8_0
#error please update TargetFramework string
#endif
		_Add(xpg, "LangVersion", "preview");
		_Add(xpg, "AllowUnsafeBlocks", "true");
		_Add(xpg, "UseWindowsForms", "true");
		_Add(xpg, "UseWPF", "true");
		_Add(xpg, "EnableDefaultItems", "false"); //https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop#wpf-default-includes-and-excludes
		_Add(xpg, "CopyLocalLockFileAssemblies", "true");
		_Add(xpg, "ProduceReferenceAssembly", "false");
		_Add(xpg, "GenerateAssemblyInfo", "false");
		_Add(xpg, "PublishReferencesDocumentationFiles", "false");
		_Add(xpg, "DebugType", "embedded");
		_Add(xpg, "CopyDebugSymbolFilesFromPackages", "false");
		_Add(xpg, "AppendTargetFrameworkToOutputPath", "false");
		_Add(xpg, "NuGetAudit", "false"); //don't connect to nuget.org unless using nuget references. If no internet connection, waits several s and prints: warning NU1900: Error occurred while getting package vulnerability data: No such host is known. (api.nuget.org:443). But sometimes hangs. Error if command line contains --no-restore. 
		
		_Add(xpg, "AssemblyName", _meta.Name);
		_Add(xpg, "OutputType", _meta.Console ? "Exe" : "WinExe");
		
		_Add(xpg, "DisableImplicitFrameworkDefines", "true");
		_Add(xpg, "DefineConstants", string.Join(';', _meta.Defines));
		
		_Add(xpg, "WarningLevel", _meta.WarningLevel);
		_Add(xpg, "NoWarn", string.Join(';', _meta.NoWarnings) + ";WFAC010");
		if (_meta.Nullable != 0) _Add(xpg, "Nullable", _meta.Nullable);
		
		if (_meta.Bit32) _Add(xpg, "PlatformTarget", "x86");
		if (!_Icon()) return false;
		_Add(xpg, "ApplicationManifest", _Path(_meta.ManifestFile) ?? folders.ThisAppBS + "default.exe.manifest");
		if (_meta.CodeFiles.Any(o => o.f.Name.Eqi("AssemblyInfo.cs"))) _Add(xpg, "GenerateAssemblyInfo", "false");
		
		if (_meta.SignFile != null) {
			_Add(xpg, "SignAssembly", "true");
			_Add(xpg, "AssemblyOriginatorKeyFile", _Path(_meta.SignFile));
		}
		
		//if (singleFile || readyToRun) //no, we always add only 64 or 32 bit dlls
		_Add(xpg, "RuntimeIdentifier", _meta.Bit32 ? "win-x86" : "win-x64");
		if (singleFile) {
			_Add(xpg, "PublishSingleFile", "true");
			if (selfContained) _Add(xpg, "EnableCompressionInSingleFile", "true"); //else compression not supported
			_Add(xpg, "IncludeNativeLibrariesForSelfExtract", "true");
		}
		_Add(xpg, "SelfContained", selfContained ? "true" : "false");
		if (readyToRun) _Add(xpg, "PublishReadyToRun", "true");
		
		var xig = new XElement("ItemGroup");
		xroot.Add(xig);
		
		_AddAttr(xig, "Compile", "Include", _ModuleInit());
		foreach (var v in _meta.CodeFiles) _AddAttr(xig, "Compile", "Include", _Path(v.f));
		
		var trees = CompilerUtil.CreateSyntaxTrees(_meta);
		
		_AddAuNativeDll("AuCpp.dll", both64and32: true);
		if (_NeedSqlite()) _AddAuNativeDll("sqlite3.dll");
		CompilerUtil.CopyMetaFileFilesOfAllProjects(_meta, outDir, (from, to) => _AddContentFile(from, to));
		
		if (_meta.References.DefaultRefCount != MetaReferences.DefaultReferences.Count) return _Err("noRef not supported"); //FUTURE: try <DisableImplicitFrameworkReferences>
		_AddAttr(xig, "Reference", "Include", _meta.References.Refs[_meta.References.DefaultRefCount - 1].FilePath); //Au
		_References();
		
		if (!_Nuget()) return false;
		
		if (!_ManagedResources()) return false;
		
		//print.it(xroot);
		xroot.SaveElem(_csprojFile = $@"{outDir}\{_meta.Name}.csproj");
		return true;
		
		static bool _Err(string s) { print.it("Error: " + s); return false; }
		
		static string _Path(FileNode f) => f?.FilePath;
		
		static XElement _Add(XElement parent, string tag, object value) {
			XElement x = new(tag, value);
			parent.Add(x);
			return x;
		}
		
		static XElement _AddAttr(XElement parent, string tag, string attr, object value) {
			XElement x = new(tag, new XAttribute(attr, value));
			parent.Add(x);
			return x;
		}
		
		void _AddContentFile(string path, string to) {
			var x = _AddAttr(xig, "Content", "Include", path);
			_Add(x, "CopyToOutputDirectory", "PreserveNewest");
			if (to.PathStarts(outDir)) to = to[(outDir.Length + 1)..];
			_Add(x, "Link", to); //note: TargetPath should be the same, but somehow ignores files in subfolders (meta file x /sub) if single-file, unless the folder exists.
		}
		
		void _AddAuNativeDll(string filename, bool both64and32 = false) {
			var s = (_meta.Bit32 ? @"32\" : @"64\") + filename;
			//_AddContentFile(folders.ThisAppBS + s, s); //no, NativeLibrary.TryLoad fails if single-file
			_AddContentFile(folders.ThisAppBS + s, filename);
			if (both64and32) { //also need AuCpp.dll of different bitness. For elm to load into processes of different bitness. See func RunDll in in-proc.cpp.
				s = (!_meta.Bit32 ? @"32\" : @"64\") + filename;
				_AddContentFile(folders.ThisAppBS + s, s);
			}
		}
		
		string _ModuleInit() {
			var path = outDir + @"\ModuleInit__.cs";
			filesystem.saveText(path, @"class ModuleInit__ { [System.Runtime.CompilerServices.ModuleInitializer] internal static void Init() { Au.script.AppModuleInit_(auCompiler: false); }}"); //like in Compiler._AddAttributesEtc but with `auCompiler: false`
			return path;
		}
		
		bool _Icon() {
			if (_meta.IconFile is { } fIco) {
				if (fIco.IsFolder) return _Err("icons folder not supported");
				_Add(xpg, "ApplicationIcon", _Path(fIco));
			} else if (DIcons.TryGetIconFromBigDB(f.CustomIconName, out string xaml)) {
				var file = outDir + @"\icon.ico";
				try {
					Au.Controls.KImageUtil.XamlImageToIconFile(file, xaml, 16, 24, 32, 48, 64);
					_Add(xpg, "ApplicationIcon", file);
				}
				catch (Exception e1) { print.it(e1); }
			}
			return true;
		}
		
		bool _ManagedResources() {
			var resourcesFile = outDir + @"\g.resources";
			return CompilerUtil.CreateManagedResources(_meta, _meta.Name, trees, _ResourceException, o => {
				var x = _AddAttr(xig, "EmbeddedResource", "Include", o.file);
				_Add(x, "LogicalName", o.name);
			}, resourcesFile);
			
			static void _ResourceException(Exception e, FileNode curFile) {
				var em = e.ToStringWithoutStack();
				if (curFile == null) _Err("Failed to add resources. " + em);
				else _Err($"Failed to add resource '{curFile.Name}'. " + em);
			}
		}
		
		void _References() {
			var noDup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			_Ref2(_meta);
			void _Ref2(MetaComments m) {
				foreach (var v in m.References.Refs.Skip(_meta.References.DefaultRefCount)) { //r, pr, com
					if (MetaReferences.IsNuget(v)) continue;
					var x = _AddAttr(xig, "Reference", "Include", v.FilePath);
					if (v.Properties.EmbedInteropTypes) _Add(x, "EmbedInteropTypes", "true");
					if (!v.Properties.Aliases.IsDefaultOrEmpty) _Add(x, "Aliases", v.Properties.Aliases[0]);
					if (!noDup.Add(x.ToString())) x.Remove();
				}
				if (m.ProjectReferences is { } pr) {
					foreach (var v in pr) _Ref2(v.m);
				}
			}
		}
		
		bool _Nuget() {
			var noDup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			return _Nuget2(_meta);
			bool _Nuget2(MetaComments m) {
				foreach (var (p, alias) in m.NugetPackages) {
					var dir = pathname.getDirectory(p);
					var path = $@"{App.Model.NugetDirectoryBS}{dir}\{dir}.csproj";
					if (XmlUtil.LoadElemIfExists(path) is not { } xproj || xproj.Desc("PackageReference", "Include", pathname.getName(p), true) is not { } x) return _Err("NuGet package not installed: " + p);
					if (alias != null) _Add(x, "Aliases", alias);
					if (!noDup.Add(x.ToString())) continue;
					xig.Add(x);
				}
				if (m.ProjectReferences is { } pr) {
					foreach (var v in pr) if (!_Nuget2(v.m)) return false;
				}
				return true;
			}
		}
		
		bool _NeedSqlite() {
			var cOpt = new CSharpCompilationOptions(OutputKind.WindowsApplication, allowUnsafe: true, warningLevel: 0);
			var compilation = CSharpCompilation.Create(_meta.Name, trees, _meta.References.Refs, cOpt);
			var asmStream = new MemoryStream(16000);
			var emitResult = compilation.Emit(asmStream);
			asmStream.Position = 0;
			if (emitResult.Success && CompilerUtil.UsesSqlite(asmStream)) return true;
			
			foreach (var v in _meta.References.Refs.Skip(_meta.References.DefaultRefCount)) {
				if (v.Properties.EmbedInteropTypes) continue; //com; and no nuget refs when publishing
				if (CompilerUtil.UsesSqlite(v.FilePath, recursive: true)) return true;
			}
			
			return false;
		}
	}
}
