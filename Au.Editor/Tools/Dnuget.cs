//For NuGet management we use dotnet.exe and full or minimal .NET SDK.
//Also some API from the NuGet client SDK. But it does not have API for installing dlls etc.
//We don't install packages for each script that uses them. We install all in current workspace, and let scripts use them.
//	To avoid conflicts, packages can be installed in separate folders.
//	More info in nuget.md.
//Rejected: UI to search for packages and display package info. Why to duplicate the NuGet website.

using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.XPath;
using Au.Compiler;
using Au.Controls;
using NGC = NuGet.Configuration;
using System.Collections.ObjectModel;

class DNuget : KDialogWindow {
	/// <param name="package">null or package name or folder\name.</param>
	public static void ShowSingle(string package = null) {
		var d = ShowSingle(() => new DNuget());
		if (package != null) {
			if (package.Split('\\', 2) is var a && a.Length == 2 && a[0].Length > 0 && a[1].Length > 0) { //folder\name
				package = a[1];
				d._cbFolder.Text = a[0];
			}
			d._tPackage.Text = package;
		}
	}
	
	readonly TextBox _tPackage;
	readonly ComboBox _cbFolder, _cbSource;
	readonly KTreeView _tv;
	readonly Panel _panelManage;
	readonly KGroupBox _groupInstall, _groupInstalled;
	readonly Button _bMenu;
	readonly TextBlock _tStatus;
	
	readonly string _nugetDir = App.Model.NugetDirectory;
	readonly ObservableCollection<string> _folders;
	readonly WindowDisabler _disabler;
	
	DNuget() {
		InitWinProp("NuGet packages", App.Wmain);
		var b = new wpfBuilder(this).WinSize(550, 600).Columns(-1, 30, 0);
		
		b.R.StartGrid(out _groupInstall, "Install").Columns(0, -1, 0);
		b.R.Add(wpfBuilder.formattedText($"<a href='https://www.nuget.org'>NuGet</a> package"), out _tPackage)
			.Tooltip("Examples:\nPackageName (will get the latest version)\nPackageName --version 1.2.3\ndotnet add package PackageName --version 1.2.3 (copied from the package's web page)")
			.Focus();
		b.xAddButtonIcon(EdIcons.Paste, _ => { _tPackage.SelectAll(); _tPackage.Paste(); }, "Paste");
		
		b.R.StartGrid().Columns(76, 0, -1, 20, 0, -1);
		
		b.R.AddButton(out var bInstall, "Install", _ => _Install()).Disabled();
		bInstall.IsDefault = true;
		
		b.Add("into folder", out _cbFolder).Editable().Tooltip(@"Press F1 if need help with this. Or just use the default selected folder.");
		_cbFolder.MaxDropDownHeight = 600;
		_cbFolder.ShouldPreserveUserEnteredPrefix = true;
		filesystem.createDirectory(_nugetDir);
		_folders = new(filesystem.enumDirectories(_nugetDir).Select(o => o.Name));
		if (_folders.Count == 0) _folders.Add("-");
		_cbFolder.ItemsSource = _folders;
		_cbFolder.SelectedIndex = 0; //probably "-"
		
		b.Window.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, e) => {
			bInstall.IsEnabled = !_tPackage.Text.Trim().NE() && !pathname.isInvalidName(_cbFolder.Text);
			if (e.Source == _tPackage) _PackageFieldTextChanged();
		}));
		
		b.Skip().Add("Source", out _cbSource).Tooltip("Package source.\nThe list contains nuget.org and sources specified in nuget.config files.");
		b.End();
		b.End();
		
		b.Row(-1).StartGrid(out _groupInstalled, "Installed").Columns(-1, 76);
		
		b.Row(-1).xAddInBorder(out _tv);
		
		b.StartGrid().Columns(-1); //right
		b.Row(-1).StartStack(vertical: true).Disabled(); //buttons
		b.AddButton("Add code", _ => _AddMeta()).Margin("B20").Tooltip("Use the package in the current C# file.\nAdds /*/ nuget Package; /*/.");
		b.AddButton("→ NuGet", _ => run.itSafe("https://www.nuget.org/packages/" + _Selected.Name)).Tooltip("Open the package's NuGet webpage");
		b.AddButton("→ Folder", _ => run.itSafe(_FolderPath(_Selected.Parent.Name))).Margin("B20").Tooltip("Open the folder");
		b.AddButton(out var bUpdate, "Update", _ => _Update(false)).Tooltip("Replace this version with the latest version");
		b.AddButton(out var bUpdateTo, "Update ▾", _ => _Update(true)).Tooltip("Replace this version with another version");
		bUpdate.ContextMenuOpening += (_, e) => _Update(true);
		b.AddButton("Move to ▾", _ => _Move()).Tooltip("Uninstall from this folder and install in another folder");
		b.AddButton("Uninstall", _ => _Uninstall()).Tooltip("Remove the package and its files from the folder");
		
		_panelManage = b.Panel;
		_tv.SelectionChanged += (_, _) => _panelManage.IsEnabled = !_tv.SelectedItem?.IsFolder ?? false;
		_tv.ItemClick += _tv_ItemClick;
		
		b.End(); //buttons
		b.AddButton("Updates...", _ => _CheckForUpdates()).Tooltip("Check for updates");
		b.End(); //right
		b.End(); //group "Installed"
		
		b.R.Add(out _tStatus)
			.xAddButtonIcon(out _bMenu, "*MaterialDesign.MoreHorizRound" + EdIcons.black, _ => _Menu(), "Menu")
			.xAddDialogHelpButtonAndF1("editor/NuGet");
		
		b.End();
		
		bool missingSdk = DotnetUtil.MissingSdkUI(b.Window, [_groupInstall, _groupInstalled, _bMenu], () => { _InitSources(); _FillTree(); });
		if (!missingSdk) _InitSources();
		
		Loaded += (_, _) => {
			App.Model.UnloadingThisWorkspace += Close;
			_FillTree(); //note: fill now even if missingSdk, or the user might have a heart attack when seeing an empty list. When installed, _FillTree will be called again, because need to set _TreeItem.Source.
		};
		
		_disabler = new(this);
	}
	
	protected override void OnClosed(EventArgs e) {
		App.Model.UnloadingThisWorkspace -= Close;
		base.OnClosed(e);
	}
	
	async void _Install(string folder = null) {
		folder ??= _cbFolder.Text.Trim();
		var package = _tPackage.Text.Trim();
		_NormalizeCopiedPackageString(ref package);
		
		if (!App.Settings.nuget_noPrerelease) if (!package.RxIsMatch("(?i) --version | -v | --prerelease\b")) package += " --prerelease";
		if (_cbSource.SelectedItem is _Source source && !package.RxIsMatch("(?i) --source | -s ")) package += $" --source \"{source.UrlList}\"";
		
		using var _ = _disabler.Disable();
		if (!await _InstallWhenInstallingUpdatingOrMoving(package, folder)) return;
		print.it("========== Finished ==========");
		CodeInfo.StopAndUpdateStyling();
	}
	
	/// <param name="packageString">Package name, possibly with --version and other options.</param>
	async Task<bool> _InstallWhenInstallingUpdatingOrMoving(string packageString, string folder, _TreeItem updating = null, bool moving = false) {
		var proj = _ProjPath(folder);
		
		if (!_CreateProjectFileIfNeed(proj)) return false;
		
		var sAdd = $@"add ""{proj}"" package {packageString}";
		//var sAdd = $@"package add {package} --project ""{proj}"""; //new syntax in .NET SDK 10
		
		//now need only package name
		var package = packageString.RxReplace(@"^\s*(\S+).*", "$1");
		
		bool _CanAddPrereleaseOption() => !sAdd.Contains(" --prerelease") && !sAdd.Contains(" --version");
		
		List<string> errorsAndWarnings = [];
		bool retryPrerelease = false, cancel = false;
		gRetry1:
		if (!await _RunDotnet(_Operation.Add, sAdd, errorsAndWarnings: errorsAndWarnings)) {
			retryPrerelease = errorsAndWarnings.Any(o => o.Like("error: There are no stable versions*--prerelease*")) && _CanAddPrereleaseOption();
			if (!retryPrerelease) return false;
		} else if (errorsAndWarnings.Any(o => o.Starts("error:")) && updating is null) {
			switch (dialog.show("Errors detected", "Try to install anyway?\n\nMore info in the Output.", "1 No|2 Yes", icon: DIcon.Warning, owner: this)) {
			case 1: cancel = true; break;
			}
		} else if (errorsAndWarnings.Any(o => o.Starts("warn : NU1701:")) && updating is null && !moving) {
			var buttons = "1 Cancel|2 OK" + (_CanAddPrereleaseOption() ? "|3 Retry with option --prerelease\nMaybe a compatible prerelease version exists..." : null);
			switch (dialog.show("This package may be incompatible", "Install anyway?\n\nMore info in the Output.", buttons, DFlags.CommandLinks, DIcon.Warning, owner: this)) {
			case 1: cancel = true; break;
			case 3: retryPrerelease = true; break;
			}
		}
		
		if (retryPrerelease) {
			print.it("info : Added option --prerelease");
			sAdd += " --prerelease";
			goto gRetry1;
		}
		if (cancel) {
			if (await _RunDotnet(_Operation.Other, $@"remove ""{proj}"" package {package}")) {
				filesystem.delete(_FolderPath(folder) + @"\obj", FDFlags.CanFail);
				print.it("========== Canceled ==========");
			}
			return false;
		}
		
		if (updating != null) {
			_DeleteInstalledFiles(updating, true);
		}
		
		bool ok = await _Build(folder, package);
		_AddToTreeOrUpdate(folder, package, updating);
		return ok;
		
		bool _CreateProjectFileIfNeed(string path) {
			try {
				string writeProjText = null;
				string c_targetFramework = $"<TargetFramework>net{Environment.Version.ToString(2)}-windows</TargetFramework>";
				if (!filesystem.exists(path, true)) {
					writeProjText = $"""
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		{c_targetFramework}
		<UseWPF>true</UseWPF>
		<UseWindowsForms>true</UseWindowsForms>
		<ProduceReferenceAssembly>False</ProduceReferenceAssembly>
		<DebugType>none</DebugType>
		<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
		<NuGetAudit>False</NuGetAudit>
		<AssemblyName>___</AssemblyName>
	</PropertyGroup>

	<!-- Copy XML files -->
	<Target Name="_ResolveCopyLocalNuGetPkgXmls" AfterTargets="ResolveReferences">
		<ItemGroup>
			<ReferenceCopyLocalPaths Include="@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')" Condition="'%(ReferenceCopyLocalPaths.NuGetPackageId)'!='' and Exists('%(RootDir)%(Directory)%(Filename).xml')" />
		</ItemGroup>
	</Target>

</Project>
""";
					if (!_folders.Any(o => o.Eqi(folder))) _folders.Add(folder);
				} else { //may need to update something
					string s = filesystem.loadText(path), s0 = s;
					if (!s.Contains(c_targetFramework)) s = s.RxReplace(@"<TargetFramework>.+?</TargetFramework>", c_targetFramework, 1);
					
					//previously was no <AssemblyName>___</AssemblyName>. Then usually error if the folder name == a dll name, because the main dll name was the same.
					if (!s.Contains("<AssemblyName>___</AssemblyName>") && s.RxMatch(@"\R\h*</PropertyGroup>", 0, out RXGroup g1)) s = s.Insert(g1.Start, "\r\n		<AssemblyName>___</AssemblyName>");
					
					if (s != s0) writeProjText = s;
				}
				if (writeProjText != null) filesystem.saveText(path, writeProjText);
			}
			catch (Exception e1) {
				print.warning(e1);
				return false;
			}
			return true;
		}
	}
	
	async Task<bool> _Build(string folder, string package = null) {
		var folderPath = _FolderPath(folder);
		var proj = _ProjPath(folder);
		bool installing = package != null;
		bool building = true;
		
		try {
			var noRestore = installing ? "--no-restore " : null; //`package add` restores, `package remove` doesn't
			var sBuild = $@"build ""{proj}"" {noRestore}--nologo -v m -o ""{folderPath}""";
			if (!await _RunDotnet(_Operation.Build, sBuild)) return false;
			//TODO3: if fails, uninstall the package immediately.
			//	Else in the future will fail to install any package.
			//	Also may delete dll files and leave garbage.
			//	But problem: may fail because of ANOTHER package. How to know which package is bad?
			//	Now just prints info in the finally block.
			
			//dialog.show("nuget 2");
			
			if (installing) {
				//we need a list of installed files (managed dll, unmanaged dll, maybe more).
				//	When compiling miniProgram or editorExtension, will need dll paths to resolve at run time.
				//	When compiling exeProgram, will need to copy them to the output directory.
				
				//at first create a copy of the csproj file with only this PackageReference (remove others)
				var dirProj2 = folderPath + @"\single";
				filesystem.createDirectory(dirProj2);
				var proj2 = dirProj2 + @"\~.csproj";
				var dirBin2 = dirProj2 + @"\bin";
				var xp = XElement.Load(proj);
				var axp = xp.XPathSelectElements($"/ItemGroup/PackageReference[@Include]").ToArray();
				foreach (var v in axp) if (!v.Attr("Include").Eqi(package)) v.Remove();
				xp.Save(proj2);
				
				//then build it, using a temp output directory
				sBuild = $@"build ""{proj2}"" --nologo -v m -o ""{dirBin2}"""; //note: no --no-restore
				if (!await _RunDotnet(_Operation.Build, sBuild, s => { })) { //try silent, but print errors if fails (unlikely)
					Debug_.Print("FAILED");
					if (!await _RunDotnet(_Operation.Build, sBuild)) return false;
				}
				//#if DEBUG
				//run.it(dirBin2);
				//dialog.show("Debug", "single build done"); //to inspect files before deleting
				//#endif
				
				//delete runtimes of unsupported OS or CPU. It seems cannot specify it in project file.
				_DeleteOtherRuntimes(folderPath);
				_DeleteOtherRuntimes(dirBin2);
				void _DeleteOtherRuntimes(string dir) {
					dir += @"\runtimes";
					if (filesystem.exists(dir)) {
						foreach (var v in filesystem.enumDirectories(dir)) {
							var n = v.Name;
							if (!n.Starts("win", true) || (n.Contains('-') && 0 == n.Ends(true, "-x64", "-x86", "-arm64"))) {
								filesystem.delete(v.FullPath);
							}
						}
					}
				}
				
				//save relative paths etc of output files in file "nuget.xml"
				//	Don't use ___.deps.json. It contains only used dlls, but may also need other files, eg exe.
				//	For testing can be used NuGet package Microsoft.PowerShell.SDK. It has dlls for testing almost all cases.
				
				var npath = _nugetDir + @"\nuget.xml";
				var xn = XmlUtil.LoadElemIfExists(npath, "nuget");
				var packagePath = folder + "\\" + package;
				xn.Elem("package", "path", packagePath, true)?.Remove();
				var xx = new XElement("package", new XAttribute("path", packagePath), new XAttribute("format", "1"));
				xn.AddFirst(xx);
				
				var dCompile = _GetCompileAssembliesFromAssetsJson(dirProj2 + @"\obj\project.assets.json", folderPath);
				
				//get lists of .NET dlls, native dlls and other files
				List<(FEFile f, int r)> aDllNet = new(); //r: 0 r (ref and run time), 1 ro (ref only), 2 rt (run time only)
				List<FEFile> aDllNative = new(), aOther = new();
				var feFlags = FEFlags.AllDescendants | FEFlags.OnlyFiles | FEFlags.UseRawPath | FEFlags.NeedRelativePaths;
				foreach (var f in filesystem.enumFiles(dirBin2, flags: feFlags).OrderBy(o => o.Level)) {
					var s = f.Name; //like @"\file" or @"\dir\file"
					bool runtimes = false;
					if (f.Level == 0) {
						if (s.Starts(@"\___.")) continue;
					} else {
						runtimes = s.Starts(@"\runtimes\win", true);
						Debug_.PrintIf(!(runtimes || s.Ends(".resources.dll") || 0 != s.Starts(false, @"\ref\", @"\.playwright\")), s); //ref is used by Microsoft.PowerShell.SDK as data files
					}
					if (s.Ends(".dll", true) && (f.Level == 0 || runtimes)) {
						if (CompilerUtil.IsNetAssembly(f.FullPath, out bool refOnly)) {
							aDllNet.Add((f, refOnly ? 1 : runtimes ? 2 : 0));
						} else {
							aDllNative.Add(f);
						}
					} else {
						aOther.Add(f);
					}
				}
				
				//.NET dlls
				HashSet<string> hsLib = new(StringComparer.OrdinalIgnoreCase);
				foreach (var group in aDllNet.ToLookup(o => pathname.getName(o.f.Name), StringComparer.OrdinalIgnoreCase)) {
					//print.it($"<><lc #BBE3FF>{group.Key}<>");
					var filename = group.Key;
					int count = group.Count();
					bool haveRO = dCompile.Remove(filename);
					if (haveRO) xx.Add(new XElement("ro", @"\_ref\" + filename));
					XElement xGroup = null;
					foreach (var (f, r) in group) {
						var s = f.Name; //like @"\file" or @"\dir\file"
						hsLib.Add(s);
						bool refOnly = r == 1 || (r == 0 && f.Level == 0 && count > 1); //if count>1, this is X.dll from [X.dll, sub\X.dll, ...]
						if (refOnly) {
							if (haveRO) continue;
							xx.Add(new XElement("ro", s));
						} else {
							if (r == 2 && s[13] != '\\') { //\runtimes\win... but not \runtimes\win\...
								if (xGroup == null) xx.Add(xGroup = new("group"));
								xGroup.Add(new XElement("rt", s));
							} else if (!haveRO && r == 0) {
								xx.Add(new XElement("r", s));
							} else {
								xx.Add(new XElement("rt", s));
							}
						}
						//print.it(s, f.Size, refOnly, haveRO);
					}
					
					//XML tags:
					//	"r" - .NET dll used at compile time and run time. Not ref-only.
					//	"ro" - .NET dll used only at compile time. Can be ref-only or not.
					//	"rt" - .NET dll used only at run time.
					//	"native" - unmanaged dll
					//	"other" - all other (including dlls in folders other than root and runtimes)
					//	"group" - group of "rt" dlls. Same dll for different OS versions/platforms.
					//	"natives" - group of "native" dlls. Same dll for different OS versions/platforms.
					//native dlls usually are in \runtimes\win-x64\native\x.dll etc, but also can be in \runtimes\win10-x64\native\x.dll etc.
				}
				
				if (dCompile.Any()) { //ref-only dlls that were not copied to dirBin2
					foreach (var (k, v) in dCompile) {
						xx.Add(new XElement("ro", @"\_ref\" + k));
						hsLib.Add(k);
					}
				}
				
				//native dlls
				foreach (var group in aDllNative.ToLookup(o => pathname.getName(o.Name), StringComparer.OrdinalIgnoreCase)) {
					XElement xGroup = null;
					foreach (var f in group) {
						var s = f.Name;
						if (f.Level > 0 && s[13] != '\\') {
							if (xGroup == null) xx.Add(xGroup = new("natives"));
							xGroup.Add(new XElement("native", s));
						} else {
							xx.Add(new XElement("native", s));
						}
					}
				}
				
				//print.it(xx);
				
				//other files
				foreach (var f in aOther) {
					var s = f.Name;
					
					//skip XML doc. When compiling exeProgram, other xml files will be copied to the output.
					if (s.Ends(".xml", true) && hsLib.Contains(s.ReplaceAt(^3..^1, "dl"))) continue;
					
					xx.Add(new XElement("other", s));
				}
				
				xn.SaveElem(npath, backup: true);
				
				//finally delete temp files
				try { filesystem.delete(dirProj2); }
				catch (Exception e1) { Debug_.Print(e1); }
			}
			
			try {
				filesystem.delete($@"{folderPath}\___.dll");
				foreach (var v in Directory.GetFiles(folderPath, "*.json")) filesystem.delete(v);
				//filesystem.delete($@"{folderPath}\obj\Debug");
				filesystem.delete($@"{folderPath}\obj");
			}
			catch (Exception e1) { Debug_.Print(e1); }
			
			building = false;
		}
		finally {
			if (building) //failed to build
				print.it($@"<><c red>IMPORTANT: Please uninstall the package that causes the error.
	Until then will fail to install or use packages in this folder ({folder}).
	If two packages can't coexist, try to move it to a new folder (see the combo box).<>");
		}
		return true;
		
		static Dictionary<string, string> _GetCompileAssembliesFromAssetsJson(string file, string folderPath) {
			string refDir = null;
			var hsDotnet = _GetDotnetAssemblies();
			Dictionary<string, string> d = new(StringComparer.OrdinalIgnoreCase);
			var j = JsonNode.Parse(File.ReadAllBytes(file));
			var packages = j["packageFolders"].AsObject().First().Key;
			foreach (var (nameVersion, v1) in j["targets"].AsObject().First().Value.AsObject()) {
				var k = v1.AsObject();
				if (!k.TryGetPropertyValue("compile", out var v2)) continue;
				foreach (var (s, _) in v2.AsObject()) {
					if (s.NE() || s.Ends("/_._")) continue;
					var name = pathname.getName(s);
					if (hsDotnet.Contains(name)) continue;
					var path = (packages + nameVersion + "\\" + s).Replace('/', '\\');
					if (!filesystem.exists(path)) {
						Debug_.Print($"<c red>{path}<>");
						continue;
					}
					if (d.TryGetValue(name, out var ppath)) {
#if DEBUG
						filesystem.GetProp_(ppath, out var u1);
						filesystem.GetProp_(path, out var u2);
						if (u2.size != u1.size) Debug_.Print($"<c orange>\t{name}<>\n\t\t{u1.size}  {ppath}\n\t\t{u2.size}  {path}");
						bool e1 = filesystem.exists(ppath.ReplaceAt(^3..^1, "xm")), e2 = filesystem.exists(path.ReplaceAt(^3..^1, "xm"));
						Debug_.PrintIf(e2 != e1, "no xml");
#endif
						continue;
					}
					var path2 = folderPath + "\\" + name;
					if (filesystem.GetProp_(path2, out var p1) && filesystem.GetProp_(path, out var p2) && p1 == p2) continue;
					d.Add(name, path);
					if (refDir == null) filesystem.createDirectory(refDir = folderPath + @"\_ref");
					filesystem.copyTo(path, refDir);
					var docPath = path.ReplaceAt(^3..^1, "xm");
					if (filesystem.exists(docPath, useRawPath: true)) filesystem.copyTo(docPath, refDir);
				}
			}
			return d;
		}
		
		static HashSet<string> _GetDotnetAssemblies() {
			var s = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
			var a = s.Split(';', StringSplitOptions.RemoveEmptyEntries);
			var h = new HashSet<string>(a.Length, StringComparer.OrdinalIgnoreCase);
			string net1 = folders.NetRuntimeBS, net2 = folders.NetRuntimeDesktopBS;
			foreach (var v in a) {
				if (v.Starts(net1, true) || v.Starts(net2, true)) h.Add(pathname.getName(v));
			}
			return h;
		}
	}
	
	async void _Uninstall() {
		using var _ = _disabler.Disable();
		if (!await _UninstallWhenUninstallingOrMoving(_Selected)) return;
		print.it("========== Finished ==========");
		CodeInfo.StopAndUpdateStyling();
	}
	
	async Task<bool> _UninstallWhenUninstallingOrMoving(_TreeItem t) {
		var folder = t.Parent.Name;
		var package = t.Name;
		//if (uninstalling) if (!dialog.showOkCancel("Uninstall package", package, owner: this)) return; //more annoying than useful
		
		if (!await _RunDotnet(_Operation.Other, $@"remove ""{_ProjPath(folder)}"" package {package}")) return false;
		//if (!await _RunDotnet($@"package remove {package} --project ""{_ProjPath()}""")) return false; //new syntax in .NET SDK 10
		
		//Which installed files should be deleted? Let's delete all files (except .csproj) from the folder and rebuild.
		_DeleteInstalledFiles(t, false);
		
		var npath = _nugetDir + @"\nuget.xml";
		if (filesystem.exists(npath)) {
			var xn = XmlUtil.LoadElem(npath);
			var xx = xn.Elem("package", "path", folder + "\\" + package, true);
			if (xx != null) {
				xx.Remove();
				xn.SaveElem(npath);
			}
		}
		
		t.Remove();
		_tv.SetItems(_tvroot.Children(), true);
		if (_Selected is null) _panelManage.IsEnabled = false;
		
		return await _Build(folder);
	}
	
	void _DeleteInstalledFiles(_TreeItem t, bool updating) {
		foreach (var v in filesystem.enumerate(_FolderPath(t.Parent.Name))) {
			if (v.Attributes.Has(FileAttributes.ReadOnly)) continue; //don't delete user-added files
			if (v.IsDirectory) {
				if (updating && v.Name.Eqi("obj")) continue;
			} else {
				if (v.Name.Ends(".csproj", true)) continue;
			}
			filesystem.delete(v.FullPath, FDFlags.CanFail);
		}
	}
	
	async void _Update(bool showVersionsMenu) {
		var t = _Selected;
		var s = t.Name;
		
		if (showVersionsMenu) {
			if (await _GetVersions(t) is not { } versions) return;
			int i = popupMenu.showSimple(versions, owner: this, rawText: true) - 1; if (i < 0) return;
			s = $"{s} --version {versions[i]}";
		} else {
			if (!App.Settings.nuget_noPrerelease || t.Version.Contains('-')) s += " --prerelease";
		}
		if (t.Source is { } so) s += $" --source \"{so.UrlList}\"";
		
		using var _ = _disabler.Disable();
		if (!await _InstallWhenInstallingUpdatingOrMoving(s, t.Parent.Name, t)) return;
		print.it("========== Finished ==========");
		CodeInfo.StopAndUpdateStyling();
	}
	
	async void _Move() {
		var t = _Selected;
		
		var otherFolders = _folders.ToList();
		if (_cbFolder.Text.Trim() is var newFolder && !pathname.isInvalidName(newFolder) && !otherFolders.Contains(newFolder, StringComparer.OrdinalIgnoreCase)) otherFolders.Add(newFolder);
		if (otherFolders.FindIndex(o => o.Eqi(t.Parent.Name)) is int i1 && i1 >= 0) otherFolders.RemoveAt(i1);
		
		int i = popupMenu.showSimple(otherFolders, owner: this, rawText: true) - 1; if (i < 0) return;
		var folder = otherFolders[i];
		
		//can't move to a folder that already contains a package with this name
		foreach (var f in _tvroot.Children()) {
			if (f.Name == folder) {
				if (f.Children().Any(o => o.Name.Eqi(t.Name))) {
					print.it("Can't move to a folder that contains a package with this name.");
					return;
				}
				break;
			}
		}
		
		var s = $"{t.Name} --version {t.Version}";
		if (t.Source is { } so) s += $" --source \"{so.UrlList}\"";
		
		using var _ = _disabler.Disable();
		if (!await _InstallWhenInstallingUpdatingOrMoving(s, folder, moving: true)) return;
		await _UninstallWhenUninstallingOrMoving(t);
		print.it("========== Finished ==========");
		CodeInfo.StopAndUpdateStyling();
	}
	
	void _AddMeta() {
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return;
		var meta = new MetaCommentsParser(doc.aaaText);
		var t = _Selected;
		meta.nuget.Add($@"{t.Parent.Name}\{t.Name}");
		meta.Apply();
	}
	
	public static string[] GetInstalledPackages() {
		var xn = XmlUtil.LoadElemIfExists(App.Model.NugetDirectoryBS + "nuget.xml");
		if (xn == null) return null;
		var a = xn.Elements("package").Select(o => o.Attr("path")).ToArray();
		if (!a.Any()) return null;
		Array.Sort(a, StringComparer.OrdinalIgnoreCase);
		return a;
	}
	
	void _Menu() {
		var m = new popupMenu();
		m.AddCheck("Avoid prerelease versions", App.Settings.nuget_noPrerelease, o => { App.Settings.nuget_noPrerelease = o.IsChecked; });
		m.Last.Tooltip = "When installing or updating, if version not specified, prefer the latest stable version";
		m.Submenu("nuget.config", m => {
			foreach (var v in _Config.GetConfigFilePaths()) m[v.Limit(150, middle: true)] = o => { run.selectInExplorer(v); };
		});
		m.Submenu("NuGet cache", m => {
			m["Open packages folder"] = o => { if (_GetCacheDir() is { } s) run.itSafe(s); };
			m.Submenu("Clear all caches", m => {
				m["Clear"] = o => { _ = _RunDotnet(_Operation.Other, "nuget locals all --clear"); };
			});
		});
		m.Show();
	}
	
	void _tv_ItemClick(TVItemEventArgs e) {
		if (e.Button != MouseButton.Right) return;
		var t = e.Item as _TreeItem;
		_tv.Select(t, focus: true);
		if (t.IsFolder) {
			var path = _FolderPath(t.Name);
			var m = new popupMenu();
			m["Install into this folder", disable: _tPackage.Text.Trim().NE()] = o => { _Install(t.Name); };
			m["Rename folder"] = o => { _tv.EditLabel(t); };
			m["Delete unused folder", disable: !_CanDeleteFolder()] = o => {
				if (false == filesystem.delete(path, FDFlags.CanFail)) return;
				t.Remove();
				_tv.SetItems(_tvroot.Children(), true);
				_folders.Remove(t.Name);
			};
			m.Show();
			
			bool _CanDeleteFolder() {
				if (t.HasChildren) return false;
				if (!filesystem.exists(path).Directory) return false;
				var a = filesystem.enumerate(path, FEFlags.UseRawPath).ToArray();
				if (a.Length > 1 || (a.Length == 1 && (a[0].IsDirectory || !a[0].Name.Ends(".csproj", true)))) return false;
				return true;
			}
		}
	}
	
	enum _Operation { Add, Build, Other }
	
	async Task<bool> _RunDotnet(_Operation op, string cl, Action<string> printer = null, List<string> errorsAndWarnings = null) {
		errorsAndWarnings?.Clear();
		try {
			if (printer == null) {
				var clPrint = cl.RxReplace(@" ""[^""]+\.csproj""", "", 1).Replace(" --nologo -v m", ""); //try to print shorter command line
				print.it($"<><c blue>dotnet {clPrint}<>");
				bool skip = false;
				printer = s => {
					if (!skip) skip = s.Starts("Usage:");
					if (skip) return;
					if (s.Starts("error") || s.Contains(": error ")) {
						errorsAndWarnings?.Add(s);
						s = $"<><c red>{s}<>";
					} else if (s.Starts("warn") || s.Contains(": warning ")) {
						errorsAndWarnings?.Add(s);
						s = $"<><c DarkOrange>{s}<>";
					} else if (op == _Operation.Add) {
						if (s.Like(false,
							"info :   OK http*",
							"info : Generating MSBuild file*",
							"info : Writing assets file*",
							"info : X.* certificate chain validation will*",
							"  Determining projects to restore*",
							"log  : Restored*",
							"  Writing *",
							"info : PackageReference for package*",
							"*is compatible with all the specified frameworks in project*"
							) > 0) s = null;
					} else if (op == _Operation.Build) {
						if (s.Like(false,
							"  Determining projects to restore*",
							"Time Elapsed *",
							"    0 Warning*",
							"    0 Error*",
							@"*\___.dll"
							) > 0) s = null;
					}
					if (!s.NE()) print.it(s);
				};
			}
			
			return await Task.Run(() => 0 == run.console(printer, DotnetUtil.DotnetExe, cl));
		}
		catch (Exception e1) {
			var s = e1.ToStringWithoutStack();
			dialog.showError("Failed to run dotnet.exe", s, owner: this);
		}
		return false;
	}
	
	/// <summary>
	/// Gets our <b>NGC.ISettings</b>.
	/// </summary>
	/// <exception cref="Exception"></exception>
	NGC.ISettings _Config => field ??= NGC.Settings.LoadDefaultSettings(_nugetDir);
	
	string _GetCacheDir() {
		try { return NGC.SettingsUtility.GetGlobalPackagesFolder(_Config)?.TrimEnd('\\'); }
		catch { return null; }
	}
	
	class _Source {
		public _Source() {
			Url = UrlList = "https://api.nuget.org/v3/index.json";
			Name = "nuget.org";
			PackageBaseUrl = "https://api.nuget.org/v3-flatcontainer/";
			_packageBaseUrlInited = true;
		}
		
		public _Source(NGC.PackageSource ps) {
			Url = UrlList = ps.Source;
			Name = ps.Name;
			IsLocal = ps.IsLocal;
			if (ps.Credentials is { } cred) _auth = $"{cred.Username}:{cred.Password}"; //tested: auto-decrypts password
		}
		
		public _Source(string url) {
			Url = UrlList = Name = url;
			IsLocal = !url.Starts("http", true);
		}
		
		public string Url { get; }
		public string Name { get; }
		public bool IsLocal { get; }
		readonly string _auth;
		public override string ToString() => Name;
		
		/// <summary>
		/// Gets the base URL of packages, or null if local or <see cref="InitPackageBaseUrl"/> not called or failed.
		/// </summary>
		public string PackageBaseUrl { get; private set; }
		
		/// <summary>
		/// List of URLs for <c>dotnet add package --sources</c>, like <c>"Url;other_url"</c>.
		/// </summary>
		public string UrlList { get; set; }
		
		/// <summary>
		/// Downloads <c>index.json</c> of the source and gets the base URL of packages.
		/// Does nothing if local or already called.
		/// Not thread-safe.
		/// </summary>
		public void InitPackageBaseUrl() {
			if (_packageBaseUrlInited || IsLocal) return;
			try {
				if (!Url.Ends(".json", true)) return;
				var j = internet.http.Get(Url, auth: _auth).Json();
				var ver = (string)j["version"];
				if (!ver.Starts("3.0.")) { Debug_.Print(ver); return; }
				foreach (var v in j["resources"].AsArray()) {
					if (((string)v["@type"])?.Starts("PackageBaseAddress/3.0.") == true) {
						if ((string)v["@id"] is string s1) {
							if (!s1.Ends('/')) s1 += "/";
							PackageBaseUrl = s1;
						}
						break;
					}
				}
			}
			catch (Exception ex) { Debug_.Print(ex); }
			finally { _packageBaseUrlInited = true; }
		}
		bool _packageBaseUrlInited;
		
		/// <summary>
		/// Downloads <c>index.json</c> of a package.
		/// </summary>
		/// <returns>null if local or failed or <see cref="InitPackageBaseUrl"/> not called or failed.</returns>
		public JsonNode GetPackageIndexJson(string package) {
			if (PackageBaseUrl is string pbu) {
				var url = $"{pbu}{package.Lower()}/index.json";
				try {
					var r = internet.http.Get(url, auth: _auth);
					if (r.IsSuccessStatusCode) return r.Json();
					Debug_.PrintIf(r.StatusCode != System.Net.HttpStatusCode.NotFound, r);
				}
				catch (Exception ex) { Debug_.Print(ex); }
			}
			return null;
		}
		
		public string[] GetPackageVersions(string package) {
			if (GetPackageIndexJson(package) is JsonNode j) {
				if (j["versions"]?.AsArray() is { } versions) {
					var a = versions.Select(o => (string)o).ToArray();
					if (a.Length > 1 && NuGet.Versioning.NuGetVersion.Parse(a[0]) > NuGet.Versioning.NuGetVersion.Parse(a[^1])) Array.Reverse(a); //eg in GitHub Packages the list is in reverse order than in nuget.org
					return a;
				}
			}
			return null;
		}
		
		public byte[] GetPackageIcon(_TreeItem t) {
			if (PackageBaseUrl is string pbu) {
				var url = $"{pbu}{t.Name.Lower()}/{t.Version.Lower()}/icon";
				try {
					var r = internet.http.Get(url, auth: _auth);
					if (r.IsSuccessStatusCode) return r.Bytes();
					if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
					Debug_.Print(r);
				}
				catch (Exception ex) { Debug_.Print(ex); }
			}
			return null;
		}
	}
	
	void _InitSources() {
		_sources = [new()]; //nuget.org
		_dSources = new(StringComparer.OrdinalIgnoreCase) { { _sources[0].Url, _sources[0] } };
		int nNonlocal = 0;
		try {
			var provider = new NGC.PackageSourceProvider(_Config);
			foreach (var v in provider.LoadPackageSources()) {
				if (!v.IsEnabled) continue;
				if (v.Source == _sources[0].Url) continue;
				_Source so = new(v);
				if (!_dSources.TryAdd(so.Url, so)) continue; //note: no warning
				_sources.Add(so);
				if (!v.IsLocal) nNonlocal++;
			}
		}
		catch (Exception ex) { Debug_.Print(ex); }
		
		if (nNonlocal > 0) { //create UrlList for each
			var b = new StringBuilder();
			foreach (var so in _sources.Skip(1)) {
				if (so.IsLocal) continue;
				b.Clear();
				b.Append(so.Url);
				foreach (var k in _sources) if (k != so && !k.IsLocal) b.Append(';').Append(k.Url);
				so.UrlList = b.ToString();
			}
		}
		
		foreach (var v in _sources) _cbSource.Items.Add(v);
		_cbSource.Items.Add("Try all listed sources");
		_cbSource.SelectedIndex = 0;
	}
	
	List<_Source> _sources;
	Dictionary<string, _Source> _dSources;
	
	_Source _SourceFromUrl(string url) {
		if (_sources is null) return null; //SDK not installed
		if (url.NE()) return null;
		if (_dSources.TryGetValue(url, out var r)) return r;
		Debug_.Print(url);
		return new(url);
	}
	
	async Task<string[]> _GetVersions(_TreeItem t) {
		List<_Source> sources;
		if (t.Source is { } so) {
			if (so.IsLocal) {
				dialog.show("Not supported", "Cannot get package versions from local sources.\nSource: " + so.Name, owner: this);
				return null;
			}
			sources = [so];
		} else {
			sources = _sources;
		}
		var r = await Task.Run(() => {
			foreach (var source in sources) {
				source.InitPackageBaseUrl();
				if (source.GetPackageVersions(t.Name) is { } a) {
					Array.Reverse(a);
					//remove prereleases older than the latest stable version
					int i = 0; while (i < a.Length) if (!a[i++].Contains('-')) break;
					for (; i < a.Length; i++) if (a[i].Contains('-')) a[i] = null;
					return a.Where(o => o != null).ToArray();
				}
			}
			return null;
		});
		return r;
	}
	
	async void _CheckForUpdates() {
		using var _ = _disabler.Disable();
		_tStatus.Text = "Checking for updates...";
		
		var a = _tvroot.Descendants().Where(o => o.Level == 2).ToArray();
		int nStable = 0, nPrerelease = 0;
		
		await Task.Run(() => {
			foreach (var v in _sources) v.InitPackageBaseUrl();
			
			Parallel.ForEach(a, t => {
				t.Updates = null;
				try {
#if true
					string[] versions = null;
					if (t.Source is { } so) {
						versions = so.GetPackageVersions(t.Name);
					} else {
						foreach (var v in _sources) {
							versions = v.GetPackageVersions(t.Name);
							if (versions != null) break;
						}
					}
#else
					var so = t.Source ?? _sources[0];
					var versions = so.GetPackageVersions(t.Name);
#endif
					if (versions.NE_()) return;
					
					string latest = versions[^1], latestPrerelease = null, s;
					if (latest.Contains('-')) {
						latestPrerelease = latest;
						latest = versions.LastOrDefault(o => !o.Contains('-'));
					}
					
					if (latestPrerelease != null) {
						if (t.Version == latestPrerelease) return;
						bool onlyPR = latest is null || t.Version == latest;
						s = onlyPR ? latestPrerelease : $"{latest},  {latestPrerelease}";
						nPrerelease++; if (!onlyPR) nStable++;
					} else {
						if (t.Version == latest) return;
						s = latest;
						nStable++;
					}
					t.Updates = s;
				}
				catch (Exception ex) {
					Debug_.Print($"Failed to check {t.Name}: {ex}");
				}
			});
		});
		
		wpfBuilder.formatTextOf(_tStatus, $"Updates: <s c='green'>{nStable} stable</s>, <s c='blue'>{nPrerelease} prerelease</s>");
		_tv.Redraw(remeasure: true);
	}
	
	static bool _NormalizeCopiedPackageString(ref string s) {
		if (s.Starts(true, "dotnet add package ", "dotnet package add ") > 0) s = s[19..];
		else if (s.RxReplace(@"^.+?\bInstall-Package ", "", out s) > 0) s = s.Replace("-Version ", "--version ");
		else return false;
		return true;
	}
	
	void _PackageFieldTextChanged() {
		_TreeItem select = null;
		var s = _tPackage.Text;
		if (s.Length > 1) {
			if (_NormalizeCopiedPackageString(ref s)) { _tPackage.Text = s; return; } //calls this func again when text changed
			int i = s.IndexOf(' ');
			if (i > 0) s = s[..i];
			select = _tvroot.Descendants().FirstOrDefault(o => !o.IsFolder && o.Name.Find(s, true) >= 0);
		}
		
		if (select != null) _tv.SelectSingle(select);
		else _tv.UnselectAll();
	}
	
	bool _RenameFolder(_TreeItem t, string name) {
		if (name != t.Name) {
			try {
				var dir = _FolderPath(t.Name);
				filesystem.createDirectory(dir); //ensure exists
				filesystem.rename(dir, name);
				_folders[_folders.IndexOf(t.Name)] = name;
				return true;
			}
			catch (Exception ex) { print.it(ex); }
		}
		return false;
	}
	
	_TreeItem _Selected => _tv.SelectedItem as _TreeItem;
	
	string _FolderPath(string folder) => $@"{_nugetDir}\{folder}";
	
	string _ProjPath(string folder) => $@"{_nugetDir}\{folder}\{folder}.csproj";
	
	XElement _LoadProject(string folder) {
		var path = _ProjPath(folder);
		if (filesystem.exists(path))
			try { return XElement.Load(path); }
			catch (Exception ex) { print.warning(ex); }
		return null;
	}
	
	IEnumerable<(string name, string version, string source)> _GetProjectPackages(XElement xr) {
		foreach (var x in xr.XPathSelectElements("/ItemGroup/PackageReference[@Include]")) {
			yield return (x.Attr("Include"), x.Attr("Version"), x.Attr("la-source"));
		}
	}
	
	(XElement x, string name, string version, string source) _GetProjectPackage(XElement xr, string name) {
		foreach (var x in xr.XPathSelectElements("/ItemGroup/PackageReference[@Include]")) {
			string s = x.Attr("Include");
			if (s.Eqi(name)) return (x, s, x.Attr("Version"), x.Attr("la-source"));
		}
		return default;
	}
	
	void _FillTree() {
		List<_TreeItem> a = [];
		_tvroot = new(this, null);
		foreach (var folder in _folders) {
			var k = new _TreeItem(this, folder);
			if (_LoadProject(folder) is { } xr) {
				foreach (var (name, version, source) in _GetProjectPackages(xr)) {
					var t = new _TreeItem(this, name, version, _SourceFromUrl(source));
					k.AddChild(t);
					a.Add(t);
				}
			}
			_tvroot.AddChild(k);
		}
		_tv.SetItems(_tvroot.Children());
		if (_sources != null && a.Count > 0) _DisplayIcons(a, useCache: true); //if SDK installed
	}
	//CONSIDER: display transitive packages too, as child nodes.
	
	async void _DisplayIcons(List<_TreeItem> a, bool useCache) {
		const string c_defaultIcon = "*SimpleIcons.NuGet #55A7DD";
		var cacheDir = folders.ThisAppDataLocal + "nugetIcons";
		
		try {
			if (filesystem.exists(cacheDir, true).Directory) {
				if (useCache) {
					var df = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (var v in filesystem.enumFiles(cacheDir, "**m *.png||*.jpg||*.ico||*.a")) df[pathname.getNameNoExt(v.Name)] = v.FullPath;
					for (int i = a.Count; --i >= 0;) {
						if (df.TryGetValue(a[i].Name, out string file)) {
							var ext = pathname.getExtension(file);
							a[i].Icon = ext.Eqi(".a") ? c_defaultIcon : ext.Eqi(".ico") ? file : "imagefile:" + file;
							a.RemoveAt(i);
						}
					}
				}
			} else {
				filesystem.createDirectory(cacheDir);
			}
		}
		catch { return; }
		
		if (a.Count > 0) await Task.Run(_GetIcons);
		_tv.Redraw();
		
		void _GetIcons() {
			foreach (var v in _sources) v.InitPackageBaseUrl();
			
			Parallel.ForEach(a, t => {
				try {
					t.Icon = c_defaultIcon;
					var so = t.Source ?? _sources[0];
					if (so.GetPackageIcon(t) is { } bytes) {
						var fileType = bytes switch { //note: Content-Type unreliable, eg sometimes "application/octet-stream"
							[0x89, 0x50, 0x4E, 0x47, ..] => ".png",
							[0xFF, 0xD8, 0xFF, ..] => ".jpg",
							[0, 0, 1, 0, ..] => ".ico",
							_ => ".a" //no icon or unsupported filetype. Why ".a": if will be "file.a" and "file.png", the `df[pathname.getNameNoExt(v.Name)] = v.FullPath` will choose "file.png".
						};
						var file = $@"{cacheDir}\{t.Name}{fileType}";
						filesystem.saveBytes(file, bytes);
						if (fileType != ".a") t.Icon = fileType == ".ico" ? file : "imagefile:" + file;
					}
				}
				catch (Exception ex) {
					Debug_.Print($"Failed to get icon of {t.Name}: {ex}");
				}
			});
		}
	}
	
	void _AddToTreeOrUpdate(string folder, string package, _TreeItem updating) {
		if (_LoadProject(folder) is not { } xr) return;
		var pr = _GetProjectPackage(xr, package);
		
		//from nuget cache get source and correct-cased name
		if (_GetCacheDir() is string dir) {
			dir = $@"{dir}\{package}\{pr.version}\";
			string name0 = pr.name, source0 = pr.source;
			try { pr.source = (string)JsonNode.Parse(filesystem.loadText(dir + ".nupkg.metadata"))["source"]; } catch { }
			try { pr.name = XmlUtil.LoadElem(out var ns, dir + package + ".nuspec").Element(ns + "metadata").Element(ns + "id").Value; } catch { }
			if (pr.name != name0 || pr.source != source0) {
				if (pr.name != name0) pr.x.SetAttributeValue("Include", pr.name);
				if (pr.source != source0) pr.x.SetAttributeValue("la-source", pr.source);
				try { xr.SaveElem(_ProjPath(folder)); } catch { }
			}
		}
		
		bool isNew = false;
		_TreeItem t = updating;
		if (t is null) {
			var tFolder = _tvroot.Children().FirstOrDefault(o => o.Name == folder);
			if (tFolder is null) {
				tFolder = new(this, folder);
				var tBefore = _tvroot.Children().FirstOrDefault(o => o.Name.CompareTo(folder) > 0);
				if (tBefore != null) tBefore.AddSibling(tFolder, false); else _tvroot.AddChild(tFolder);
			}
			t = tFolder.Children().FirstOrDefault(o => o.Name.Eqi(pr.name));
			if (isNew = t is null) {
				t = new(this, pr.name, pr.version, _SourceFromUrl(pr.source));
				tFolder.AddChild(t);
				_tv.SetItems(_tvroot.Children(), true);
			}
		}
		if (!isNew) {
			t.Update(pr.name, pr.version, _SourceFromUrl(pr.source));
			_tv.Redraw(t, true);
		}
		_tv.SelectSingle(t);
		_DisplayIcons([t], useCache: false);
	}
	
	_TreeItem _tvroot;
	
	class _TreeItem : TreeBase<_TreeItem>, ITreeViewItem {
		DNuget _d;
		bool _isExpanded;
		
		public _TreeItem(DNuget d, string folder) {
			_d = d;
			IsFolder = _isExpanded = true;
			Name = NameVersion = folder;
		}
		
		public _TreeItem(DNuget d, string name, string version, _Source source) {
			_d = d;
			Update(name, version, source);
		}
		
		public string Name { get; private set; }
		public string Version { get; private set; }
		public _Source Source { get; private set; }
		public string NameVersion { get; private set; }
		public string Updates { get; set; }
		public object Icon { get; set; }
		public override string ToString() => NameVersion;
		
		public void Update(string name, string version, _Source source) {
			Name = name;
			Version = version;
			Source = source;
			NameVersion = version == null ? name : $"{name} {version}";
			Updates = null;
		}
		
		#region ITreeViewItem
		
		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }
		bool ITreeViewItem.IsExpanded => _isExpanded;
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		public bool IsFolder { get; }
		string ITreeViewItem.DisplayText => Updates is null ? NameVersion : NameVersion + "  ->  " + Updates;
		object ITreeViewItem.Image => IsFolder ? EdIcons.FolderIcon(_isExpanded) : Icon;
		
		int ITreeViewItem.TextColor(TVColorInfo ci) {
			if (Updates is string s) {
				if (s.Contains('-') && !s.Contains(", ")) return 0xFF;
				return 0x9000;
			}
			return -1;
		}
		
		void ITreeViewItem.SetNewText(string text) { if (_d._RenameFolder(this, text)) Name = NameVersion = text; }
		
		#endregion
	}
}
