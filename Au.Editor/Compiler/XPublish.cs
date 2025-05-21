using Au.Compiler;
using Au.Controls;
using System.Xml.Linq;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Windows.Controls;

class XPublish {
	MetaComments _meta;
	string _csprojDir, _csprojFile;
	
	public async void Publish() {
		var cmd = App.Commands[nameof(Menus.Run.Publish)];
		if (!cmd.Enabled) return;
		cmd.Enable(false);
		try {
			var b = new wpfBuilder("Publish").WinProperties(resizeMode: ResizeMode.NoResize, showInTaskbar: false);
			b.R.Add(out KCheckBox cSingle, "Single file").Checked(0 == (1 & App.Settings.publish));
			b.R.Add(out CheckBox cSelfExtract, "Self-extract").Margin(20)
				.xBindCheckedEnabled(cSingle)
				.Tooltip("""
Checked - use <IncludeAllContentForSelfExtract>. Adds all files to exe. Will extract all (including .NET dlls) to a temporary directory.
Unchecked - adds only .NET dlls to exe. Native dlls and other files (if any) will live in the exe's directory. Will use .NET dlls without extracting.
Indeterminate - use <IncludeNativeLibrariesForSelfExtract>. Adds all dlls to exe. Will extract native dlls, and use .NET dlls without extracting.
""")
				.Checked((App.Settings.publish >>> 8 & 3) switch { 1 => false, 2 => true, _ => null }, threeState: true);
			b.R.Add(out KCheckBox cNet, "Add .NET Runtime").Checked(0 != (2 & App.Settings.publish));
			b.R.Add(out KCheckBox cR2R, "ReadyToRun").Checked(0 != (4 & App.Settings.publish));
			b.R.Add("Platform", out ComboBox cbPlatform).Width(100, "L").Items("x64|arm64|x86").Select(Math.Clamp(App.Settings.publish >>> 4 & 3, 0, 2));
			b.R.StartOkCancel().AddOkCancel().xAddDialogHelpButtonAndF1("editor/Publish").End();
			b.End();
			if (!b.ShowDialog(App.Wmain)) return;
			
			int platform = cbPlatform.SelectedIndex;
			App.Settings.publish = (cSingle.IsChecked ? 0 : 1) | (cNet.IsChecked ? 2 : 0) | (cR2R.IsChecked ? 4 : 0) | ((cSelfExtract.IsChecked switch { false => 1, true => 2, _ => 0 }) << 8) | (platform << 4);
			//TODO3: maybe support publish profiles like in VS: <PublishProfileFullPath>
			
			print.it($"<><lc YellowGreen>Building program files for publishing. Please wait until DONE.<>");
			
			bool singleFile = cSingle.IsChecked; bool? selfExtract = cSelfExtract.IsChecked;
			if (!_CreateCsproj(singleFile: singleFile, selfExtract, selfContained: cNet.IsChecked, readyToRun: cR2R.IsChecked, platform)) return;
			
			var outDir = $@"{_csprojDir}\release_{platform switch { 0 => "x64", 1 => "arm64", _ => "x86" }}";
			var outFile = $@"{outDir}\{_meta.Name}.exe";
			_PrepareOutDir(outDir, outFile);
			if (_meta.PreBuild.f != null && !CompilerUtil.RunPrePostBuildScript(_meta, false, outFile, true)) return;
			if (!await Task.Run(() => _DotnetPublish(outDir, singleFile, selfExtract))) return;
			if (_meta.PostBuild.f != null && !CompilerUtil.RunPrePostBuildScript(_meta, true, outFile, true)) return;
			
			_DeleteOldOutputDir("64");
			_DeleteOldOutputDir("32");
			void _DeleteOldOutputDir(string bit) {
				var s = $@"{_csprojDir}\release_{bit}";
				if (filesystem.exists(s)) print.it($"<>This LibreAutomate version uses different names for publish output folders. You may want to delete this old unused folder: <explore>{s}<>");
			}
			
			print.it("==== DONE ====");
		}
		catch (Exception e1) { print.it(e1); }
		finally {
			filesystem.delete(folders.ThisAppTemp + "publish", FDFlags.CanFail);
			cmd.Enable(true);
		}
	}
	
	static void _PrepareOutDir(string outDir, string outFile) {
		if (filesystem.exists(outDir).Directory) {
			CompilerUtil.DeleteExeFile(outFile);
			foreach (var v in filesystem.enumerate(outDir)) filesystem.delete(v.FullPath, FDFlags.CanFail);
		}
	}
	
	bool _DotnetPublish(string outDir, bool singleFile, bool? selfExtract) {
		bool dirExisted = filesystem.exists(outDir);
		
		var buildDir = _csprojDir + @"\.build";
		var cl = $@"publish ""{_csprojFile}"" -c Release -o ""{outDir}"" --artifacts-path ""{buildDir}"" -v q";
		print.it($"<><c blue>dotnet {cl}<>");
		if (0 != run.console("dotnet.exe", cl)) { print.it("Failed"); return false; }
		print.it($@"<>{_meta.Name} -> <explore>{outDir}\{_meta.Name}.exe<>");
		
		filesystem.delete(buildDir, FDFlags.CanFail);
		
		int nFiles = 0;
		foreach (var v in filesystem.enumerate(outDir)) {
			var path = v.FullPath;
			if (!v.IsDirectory && path.Ends(".pdb", true)) filesystem.delete(path, FDFlags.CanFail); //probably Au.pdb (at home only)
			else nFiles++;
		}
		
		//warn if singleFile with IncludeNativeLibrariesForSelfExtract produced more than single exe file. See comments in _CreateCsproj.
		if (singleFile && selfExtract != true) {
			if (selfExtract == false) print.it("<>Note: <b>Self-extract<> is unchecked, therefore only .NET dlls have been added to exe. Other files are located in the exe's directory.");
			else if (nFiles > 1) print.it("<>Warning: If <b>Self-extract<> is indeterminate, some files used by this script cannot be added to exe, and the program may be invalid. It should be either checked or unchecked.");
			//BAD: may not add some files even with selfExtract true. Eg from nuget Selenium.WebDriver.ChromeDriver.
		}
		
		//sometimes Explorer does not update the folder view
		if (dirExisted) filesystem.ShellNotify_(Api.SHCNE_UPDATEDIR, outDir);
		
		return true;
	}
	
	bool _CreateCsproj(bool singleFile, bool? selfExtract, bool selfContained, bool readyToRun, int platform) {
		App.Model.Save.TextNowIfNeed(onlyText: true);
		
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return false;
		var f = doc.EFile;
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		if (!f.IsCodeFile) return false;
		
		_meta = new MetaComments(MCFlags.Publish | MCFlags.PrintErrors);
		if (!_meta.Parse(f, projFolder)) return false;
		if (_meta.Role is not (MCRole.miniProgram or MCRole.exeProgram)) return _Err("expected role exeProgram or miniProgram");
		if (_meta.TestInternal != null) return _Err("testInternal not supported");
		
		_csprojDir = $@"{_meta.OutputPath ?? MetaComments.GetDefaultOutputPath(f, _meta.Role, withEnvVar: false)}\publish";
		filesystem.createDirectory(_csprojDir);
		
		var xroot = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));
		var xpg = new XElement("PropertyGroup");
		xroot.Add(xpg);
		
		_Add(xpg, "TargetFramework", "net9.0-windows");
#if !NET9_0
#error please update TargetFramework string
#endif
		_Add(xpg, "LangVersion", "preview");
		_Add(xpg, "AllowUnsafeBlocks", "true");
		_Add(xpg, "UseWindowsForms", "true");
		_Add(xpg, "UseWPF", "true");
		_Add(xpg, "EnableDefaultItems", "false"); //https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop#wpf-default-includes-and-excludes
		_Add(xpg, "CopyLocalLockFileAssemblies", "true");
		_Add(xpg, "ProduceReferenceAssembly", "false");
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
		_Add(xpg, "NoWarn", string.Join(';', _meta.NoWarnings) + ";WFAC010;WFO0003;CA1416");
		if (_meta.Nullable != 0) _Add(xpg, "Nullable", _meta.Nullable);
		
		var sPlatform = platform switch { 0 => "x64", 1 => "arm64", _ => "x86" };
		_Add(xpg, "PlatformTarget", sPlatform);
		_Add(xpg, "RuntimeIdentifier", "win-" + sPlatform);
		
		if (!_Icon()) return false;
		_Add(xpg, "ApplicationManifest", _Path(_meta.ManifestFile) ?? folders.ThisAppBS + "default.exe.manifest");
		
		//if (_meta.CodeFiles.Any(o => o.f.Name.Eqi("AssemblyInfo.cs"))) _Add(xpg, "GenerateAssemblyInfo", "false"); //no, users don't know it. See https://www.libreautomate.com/forum/showthread.php?tid=7591
		_Add(xpg, "GenerateAssemblyInfo", "false");
		
		if (_meta.SignFile != null) {
			_Add(xpg, "SignAssembly", "true");
			_Add(xpg, "AssemblyOriginatorKeyFile", _Path(_meta.SignFile));
		}
		
		if (singleFile) {
			_Add(xpg, "PublishSingleFile", "true");
			if (selfContained) _Add(xpg, "EnableCompressionInSingleFile", "true"); //else compression not supported (with IncludeAllContentForSelfExtract too)
			if (selfExtract == null) _Add(xpg, "IncludeNativeLibrariesForSelfExtract", "true"); else if (selfExtract == true) _Add(xpg, "IncludeAllContentForSelfExtract", "true");
			//About selfExtract:
			//IncludeNativeLibrariesForSelfExtract (selfExtract null) has problems:
			//	1. Does not add data files (of most types).
			//	2. Adds not only native dlls, but also exe etc. With this mess the program usually crashes. To repro, add nuget Microsoft.Playwright.
			//IncludeAllContentForSelfExtract (selfExtract true) solves both. Also solves the "no assembly location etc" problem. But has own issues.
			//selfExtract false solves #2.
			//rejected: instead use normal checkbox that adds IncludeAllContentForSelfExtract when checked.
			//	If unchecked, use IncludeNativeLibrariesForSelfExtract only it produces single file (else there is no sense to add dlls etc).
			//	To implement it probably would need to run dotnet publish twice. I don't know other ways.
		}
		_Add(xpg, "SelfContained", selfContained ? "true" : "false");
		if (readyToRun) _Add(xpg, "PublishReadyToRun", "true");
		
		var xig = new XElement("ItemGroup");
		xroot.Add(xig);
		
		_AddAttr(xig, "Compile", "Include", _ModuleInit());
		foreach (var v in _meta.CodeFiles) _AddAttr(xig, "Compile", "Include", _Path(v.f));
		
		var trees = CompilerUtil.CreateSyntaxTrees(_meta);
		
		_AddAuNativeDll("AuCpp.dll", 0);
		_AddAuNativeDll("Au.DllHost.exe", -1);
		//if (_NeedSqlite()) _AddAuNativeDll("sqlite3.dll", 1);
		CompilerUtil.CopyMetaFileFilesOfAllProjects(_meta, _csprojDir, (from, to) => _AddContentFile(from, to));
		
		if (_meta.References.DefaultRefCount != MetaReferences.DefaultReferences.Count) return _Err("noRef not supported"); //TODO3: try <DisableImplicitFrameworkReferences>
		_AddAttr(xig, "Reference", "Include", _meta.References.Refs[_meta.References.DefaultRefCount - 1].FilePath); //Au
		_References();
		
		if (!_Nuget()) return false;
		
		if (!_ManagedResources()) return false;
		
		//print.it(xroot);
		xroot.SaveElem(_csprojFile = $@"{_csprojDir}\{_meta.Name}.csproj");
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
			if (to.PathStarts(_csprojDir)) to = to[(_csprojDir.Length + 1)..];
			_Add(x, "Link", to); //note: TargetPath should be the same, but somehow ignores files in subfolders (meta file x /sub) if single-file, unless the folder exists.
		}
		
		//platforms: 0 all, 1 only this, -1 only other
		void _AddAuNativeDll(string filename, int platforms) {
			for (int i = 0; i < 3; i++) {
				if (platforms == 0 || (platforms > 0 ? i == platform : i != platform)) {
					var s = i switch { 0 => @"64\", 1 => @"64\ARM\", _ => @"32\" } + filename;
					//_AddContentFile(folders.ThisAppBS + s, s); //no, NativeLibrary.TryLoad fails if single-file
					_AddContentFile(folders.ThisAppBS + s, i == platform ? filename : s);
				}
			}
			//why need AuCpp.dll and Au.DllHost.exe for other platforms: for elm functions to load AuCpp.dll into processes of different platform. See func SwitchArchAndInjectDll in in-proc.cpp.
		}
		
		string _ModuleInit() {
			var path = _csprojDir + @"\ModuleInit__.cs";
			filesystem.saveText(path, @"class ModuleInit__ { [System.Runtime.CompilerServices.ModuleInitializer] internal static void Init() { Au.script.AppModuleInit_(auCompiler: false); }}"); //like in Compiler._AddAttributesEtc but with `auCompiler: false`
			return path;
		}
		
		bool _Icon() {
			if (_meta.IconFile is { } fIco) {
				if (fIco.IsFolder) return _Err("icons folder not supported");
				_Add(xpg, "ApplicationIcon", _Path(fIco));
			} else if (DIcons.TryGetIconFromDB(f.CustomIconName, out string xaml)) {
				var file = _csprojDir + @"\icon.ico";
				try {
					Au.Controls.KImageUtil.XamlImageToIconFile(file, xaml, 16, 24, 32, 48, 64);
					_Add(xpg, "ApplicationIcon", file);
				}
				catch (Exception e1) { print.it(e1); }
			}
			return true;
		}
		
		bool _ManagedResources() {
			var resourcesFile = _csprojDir + @"\g.resources";
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
		
		//bool _NeedSqlite() {
		//	var cOpt = new CSharpCompilationOptions(OutputKind.WindowsApplication, allowUnsafe: true, warningLevel: 0);
		//	var compilation = CSharpCompilation.Create(_meta.Name, trees, _meta.References.Refs, cOpt);
		//	var asmStream = new MemoryStream(16000);
		//	var emitResult = compilation.Emit(asmStream);
		//	asmStream.Position = 0;
		//	if (emitResult.Success && CompilerUtil.UsesSqlite(asmStream)) return true;
			
		//	foreach (var v in _meta.References.Refs.Skip(_meta.References.DefaultRefCount)) {
		//		if (v.Properties.EmbedInteropTypes) continue; //com; and no nuget refs when publishing
		//		if (CompilerUtil.UsesSqlite(v.FilePath, recursive: true)) return true;
		//	}
			
		//	return false;
		//}
	}
}
