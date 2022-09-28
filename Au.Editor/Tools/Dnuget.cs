using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;
using System.Xml.XPath;
using Au.Compiler;
using Au.Controls;
using Au.Tools;

//For NuGet management we use dotnet.exe.
//	BAD: requires installed .NET SDK.
//Also tested the NuGet API, but it seems there is no API for installing dlls etc. Can't always detect what files need.
//We don't install packages for each script that uses them. We install all in current workspace, and let scripts use them.
//	To avoid conflicts, packages can be installed in separate folders.
//	More info in the _cbFolder tooltip.
//Rejected: UI to search for packages and display package info. Why to duplicate the NuGet website.

class DNuget : KDialogWindow {
	public static void ZShow(string package = null) {
		if (s_dialog == null) {
			s_dialog = new();
			s_dialog.Show();
		} else {
			s_dialog.Hwnd().ActivateL(true);
		}
		if (package != null) s_dialog._tPackage.Text = package;
	}
	static DNuget s_dialog;

	protected override void OnClosed(EventArgs e) {
		s_dialog = null;
		App.Model.UnloadingWorkspaceEvent -= Close;
		base.OnClosed(e);
	}

	readonly KTextBox _tPackage;
	readonly ComboBox _cbFolder;
	readonly CheckBox _cPrerelease;
	readonly KTreeView _tv;
	readonly Panel _panelManage;

	readonly string _nugetDir = App.Model.NugetDirectory;
	readonly List<string> _folders = new();

	///
	public DNuget() {
		Title = "NuGet packages";
#if !SCRIPT
		Owner = App.Wmain;
		ShowInTaskbar = false;
#endif

		var b = new wpfBuilder(this).WinSize(550, 500).Columns(-1, 0);

		b.R.StartGrid<GroupBox>("Install").Columns(0, 76, 0, -1, 0);
		Action gotoNuget = () => run.it("https://www.nuget.org");
		b.R.Add(out TextBlock _).Text("<a>NuGet", gotoNuget, " package name or PM text:");
		b.R.Add<AdornerDecorator>().Add(out _tPackage, flags: WBAdd.ChildOfLast).Focus();
		_tPackage.Watermark = "Package";

		b.R.xAddButtonIcon("*Material.ContentPaste #9F5300", _ => { _tPackage.SelectAll(); _tPackage.Paste(); }, "Paste");
		b.AddButton(out var bInstall, "Install", _ => _Install()).Disabled();
		b.Add<TextBlock>("into folder");

		b.Add(out _cbFolder).Tooltip(@"Packages are installed in current workspace and can be used by all its scripts.
Folders can be used to isolate incompatible packages if need (rarely).
For example, PackageX version 1 in FolderA, and PackageX version 2 in FolderB.
A script can use packages from multiple folders if they are compatible.");
		if (!Directory.Exists(_nugetDir)) {
			filesystem.createDirectory(_nugetDir);
			filesystem.createDirectory(_nugetDir + @"\-");
		}
		foreach (var v in filesystem.enumDirectories(_nugetDir)) _folders.Add(v.Name);
		foreach (var v in _folders) _cbFolder.Items.Add(v);
		if (_folders.Count > 0) _cbFolder.SelectedIndex = 0; //probably "-"
		var cbiAddFolder = new ComboBoxItem { Content = "New folder..." };
		_cbFolder.Items.Add(cbiAddFolder);
		cbiAddFolder.Selected += (_, e) => Dispatcher.InvokeAsync(_AddFolder);

		void _Enabled_Install() { bInstall.IsEnabled = (uint)_cbFolder.SelectedIndex < _cbFolder.Items.Count - 1 && !_tPackage.Text.Trim().NE(); }
		_tPackage.TextChanged += (_, e) => {
			_Enabled_Install();
			_SelectInTree();
		};
		_cbFolder.SelectionChanged += (_, e) => _Enabled_Install();

		b.Add(out _cPrerelease, "Prerelease").Margin("L20").Tooltip("Install prerelease version, if available.\nNot used if package version is specified in the Package field.");

		b.R.Add(out TextBlock info2).Text("To add a NuGet package reference to a script, click [Add /*/] or Properties -> NuGet.");
		info2.TextWrapping = TextWrapping.Wrap;

		b.End();

		b.Row(-1).StartGrid<GroupBox>("Installed").Columns(-1, 76);

		b.Row(-1).Add<Border>().Border().Add(out _tv, flags: WBAdd.ChildOfLast);

		b.StartStack(vertical: true).Disabled();
		b.AddButton("Add /*/", _ => _AddMeta()).Margin("B20").Tooltip(@"Use the package in current C# file. Adds /*/ nuget Package; /*/.");
		b.AddButton("→ NuGet", _ => run.it("https://www.nuget.org/packages/" + _Selected.Name)).Tooltip("Open the package's NuGet webpage");
		b.AddButton("→ Folder", _ => run.it(_FolderPath())).Margin("B20").Tooltip("Open the folder");
		b.AddButton("Update", _ => _Update()).Tooltip("Uninstall this version and install the newest version");
		b.AddButton("Move to ▾", _ => _Move()).Tooltip("Uninstall from this folder and install in another folder");
		b.AddButton("Uninstall", async _ => await _Uninstall(uninstalling: true)).Tooltip("Remove the package and its files from the folder");

		_panelManage = b.Panel;
		_tv.SelectionChanged += (_, _) => _panelManage.IsEnabled = !_tv.SelectedItem?.IsFolder ?? false;

		b.End();

		b.End();

		Action gotoSDK = () => run.it("https://dotnet.microsoft.com/en-us/download");
		b.R.Add(out TextBlock infoSDK).Text("<b>Need to install .NET SDK x64, version 6.0 or later. ", "<a>Download", gotoSDK).Hidden();
		b.AddButton("...", _ => _More()).Align(HorizontalAlignment.Right);

		Loaded += async (_, _) => {
			App.Model.UnloadingWorkspaceEvent += Close;
			_FillTree();

			bool sdkOK = false;
			await _RunDotnet("--list-sdks", s => { if (!sdkOK) sdkOK = s.ToInt() >= 6; }); //"6.0.2 [path]"
			if (!sdkOK) infoSDK.Visibility = Visibility.Visible;
		};
	}

	void _AddFolder() {
		_cbFolder.SelectedIndex = -1;
		if (!dialog.showInput(out string name, "New folder for NuGet packages", "Name", owner: this)) return;
		var path = _FolderPath(name);
		if (filesystem.exists(path)) { dialog.showError("Already exists", path, owner: this); return; }
		try { filesystem.createDirectory(path); }
		catch (Exception e1) { dialog.showError("Failed to create folder", e1.ToStringWithoutStack(), owner: this); return; }
		int i = _cbFolder.Items.Count - 1;
		_cbFolder.Items.Insert(i, name);
		_cbFolder.SelectedIndex = i;
		_folders.Add(name);
	}

	async void _Install() {
		var package = _tPackage.Text.Trim();
		if (package.Starts("dotnet add package ")) package = package[19..];
		else if (package.RxReplace(@"^.+?\bInstall-Package ", "", out package) > 0) package = package.Replace("-Version ", "--version ");

		await _Install(package, _cbFolder.SelectedItem as string);
	}

	/// <param name="package">Like "Package.Name" or "Package.Name --version x.y.z".</param>
	/// <param name="folder">Like "web".</param>
	async Task _Install(string package, string folder) {
		var proj = _ProjPath(folder);

		if (!File.Exists(proj)) {
			var s = @"
<Project Sdk=""Microsoft.NET.Sdk"">
	<PropertyGroup>
		<TargetFramework>net6.0-windows</TargetFramework>
		<UseWPF>true</UseWPF>
		<UseWindowsForms>true</UseWindowsForms>
		<ProduceReferenceAssembly>False</ProduceReferenceAssembly>
		<DebugType>none</DebugType>
		<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
	</PropertyGroup>

	<!-- Copy XML files -->
	<Target Name=""_ResolveCopyLocalNuGetPkgXmls"" AfterTargets=""ResolveReferences"">
		<ItemGroup>
			<ReferenceCopyLocalPaths Include=""@(ReferenceCopyLocalPaths->'%(RootDir)%(Directory)%(Filename).xml')"" Condition=""'%(ReferenceCopyLocalPaths.NuGetPackageId)'!='' and Exists('%(RootDir)%(Directory)%(Filename).xml')"" />
		</ItemGroup>
	</Target>

</Project>
";
			try { File.WriteAllText(proj, s); }
			catch (Exception e1) { dialog.showError("Failed", e1.ToStringWithoutStack(), owner: this); }
		}

		var sAdd = $@"add ""{proj}"" package {package}";
		if (_cPrerelease.IsChecked == true) sAdd += " --prerelease";

		//now need only package name
		package = package.RxReplace(@"^\s*(\S+).*", "$1");

		await _Build(folder, package, sAdd);
	}

	async Task<bool> _Build(string folder, string package = null, string sAdd = null) {
		var folderPath = _FolderPath(folder);
		var proj = _ProjPath(folder);
		bool installing = package != null;
		bool building = false;

		this.IsEnabled = false;
		try {
			if (installing) {
				if (!await _RunDotnet(sAdd)) return false;
			}
			building = true;

			//dialog.show("nuget 1");

			var sBuild = $@"build ""{proj}"" --nologo --output ""{folderPath}""";
			if (!await _RunDotnet(sBuild)) return false;
			//SHOULDDO: if fails, uninstall the package immediately.
			//	Else in the future will fail to install any package.
			//	Also may delete dll files and leave garbage.
			//	But problem: may fail because of ANOTHER package. How to know which package is bad?
			//	Now just prints info in the finally block.

			//dialog.show("nuget 2");

			if (installing) {
				//we need a list of installed files (managed dll, native dll, maybe more).
				//	When compiling miniProgram or editorExtension, will need dll paths to resolve at run time.
				//	When compiling exeProgram, will need to copy them to the output directory.

				//at first create a copy of the csproj file with only this PackageReference (remove others)
				var dirProj2 = folderPath + @"\single";
				filesystem.createDirectory(dirProj2);
				var proj2 = dirProj2 + @"\~.csproj";
				var dirBin2 = dirProj2 + @"\bin";
				var xp = XElement.Load(proj);
				var a = xp.XPathSelectElements($"/ItemGroup/PackageReference[@Include!='{package}']").ToArray();
				foreach (var v in a) v.Remove();
				xp.Save(proj2);

				//then build it, using a temp output directory
				sBuild = $@"build ""{proj2}"" --nologo --output ""{dirBin2}"""; //note: no --no-restore
				if (!await _RunDotnet(sBuild, s => { }) //try silent, but print errors if fails (unlikely)
					&& !await _RunDotnet(sBuild)) return false;
				//#if DEBUG
				//				if (keys.isScrollLock) dialog.show("Debug", "single build done"); //to inspect files before deleting
				//#endif

				//delete runtimes of unsupported OS or CPU. It seems cannot specify it in project file.
				_DeleteOtherRuntimes(folderPath);
				_DeleteOtherRuntimes(dirBin2);
				void _DeleteOtherRuntimes(string dir) {
					dir += @"\runtimes";
					if (filesystem.exists(dir)) {
						foreach (var v in filesystem.enumDirectories(dir)) {
							var n = v.Name;
							if (!n.Starts("win", true) || (n.Contains('-') && 0 == n.Ends(true, "-x64", "-x86"))) {
								filesystem.delete(v.FullPath);
							}
						}
					}
				}

				//save relative paths etc of output files in file "nuget.xml"
				//	Don't use ~.deps.json. It contains only used dlls, but may also need other files, eg exe.
				//	For testing can be used NuGet package Microsoft.PowerShell.SDK. It has dlls for testing all cases.

				var npath = _nugetDir + @"\nuget.xml";
				var xn = XmlUtil.LoadElemIfExists(npath, "nuget");
				var packagePath = folder + "\\" + package;
				xn.Elem("package", "path", packagePath, true)?.Remove();
				var xx = new XElement("package", new XAttribute("path", packagePath), new XAttribute("format", "1"));
				xn.AddFirst(xx);

				var dCompile = _GetCompileAssembliesFromAssetsJson(dirProj2 + @"\obj\project.assets.json", folderPath);

				#region copied from script "Create NuGet xml.cs"

				//get lists of .NET dlls, native dlls and other files
				List<(FEFile f, bool ro)> aDllNet = new();
				List<FEFile> aDllNative = new(), aOther = new();
				var feFlags = FEFlags.AllDescendants | FEFlags.OnlyFiles | FEFlags.UseRawPath | FEFlags.NeedRelativePaths;
				foreach (var f in filesystem.enumFiles(dirBin2, flags: feFlags).OrderBy(o => o.Level)) {
					var s = f.Name; //like @"\file" or @"\dir\file"
					bool runtimes = false;
					if (f.Level == 0) {
						if (s.Starts(@"\~.")) continue;
					} else {
						runtimes = s.Starts(@"\runtimes\win", true);
						Debug_.PrintIf(!(runtimes || s.Ends(".resources.dll") || (s.Starts(@"\ref\") && package == "Microsoft.PowerShell.SDK")), s);
					}
					if (s.Ends(".dll", true) && (f.Level == 0 || runtimes)) {
						if (Au.Compiler.CompilerUtil.IsNetAssembly(f.FullPath, out bool refOnly)) {
							aDllNet.Add((f, refOnly));
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
					//print.it($"<><Z #BBE3FF>{group.Key}<>");
					var filename = group.Key;
					int count = group.Count();
					bool haveRO = dCompile.ContainsKey(filename);
					if (haveRO) xx.Add(new XElement("ro", @"\_ref\" + filename));
					XElement xGroup = null;
					foreach (var (f, ro) in group) {
						var s = f.Name; //like @"\file" or @"\dir\file"
						hsLib.Add(s);
						bool refOnly = ro || f.Level == 0 && count > 1;
						if (refOnly) {
							if (haveRO || f.Level > 0) continue;
							xx.Add(new XElement("ro", s));
						} else {
							if (f.Level > 0 && s[13] != '\\') { //\runtimes\win... but not \runtimes\win\...
								if (xGroup == null) xx.Add(xGroup = new("group"));
								xGroup.Add(new XElement("rt", s));
							} else if (!haveRO && f.Level == 0) {
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
					//	"native" - native dll
					//	"other" - all other (including dlls in folders other than root and runtimes)
					//	"group" - group of "rt" dlls. Same dll for different OS versions/platforms.
					//	"natives" - group of "native" dlls. Same dll for different OS versions/platforms.
					//native dlls usually are in \runtimes\win-x64\native\x.dll etc, but also can be in \runtimes\win10-x64\native\x.dll etc.
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

				#endregion

				xn.SaveElem(npath, backup: true);

				//finally delete temp files
				try { filesystem.delete(dirProj2); }
				catch (Exception e1) { Debug_.Print(e1); }
			}

			try {
				filesystem.delete($@"{folderPath}\{folder}.dll");
				foreach (var v in Directory.GetFiles(folderPath, "*.json")) filesystem.delete(v);
				//filesystem.delete($@"{folderPath}\obj\Debug");
				filesystem.delete($@"{folderPath}\obj");
			}
			catch (Exception e1) { Debug_.Print(e1); }

			if (installing) print.it("========== Finished ==========");
			building = false;
		}
		finally {
			_FillTree(folder, package); //on error too, else users can't see and uninstall the new package now
			this.IsEnabled = true;
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
						Debug_.Print($"<><c red>{path}<>");
						continue;
					}
					if (d.TryGetValue(name, out var ppath)) {
#if DEBUG
						filesystem.getProperties(ppath, out var u1);
						filesystem.getProperties(path, out var u2);
						if (u2.Size != u1.Size) Debug_.Print($"<><c orange>\t{name}<>\n\t\t{u1.Size}  {ppath}\n\t\t{u2.Size}  {path}");
						bool e1 = filesystem.exists(ppath.ReplaceAt(^3..^1, "xm")), e2 = filesystem.exists(path.ReplaceAt(^3..^1, "xm"));
						Debug_.PrintIf(e2 != e1, "no xml");
#endif
						continue;
					}
					var path2 = folderPath + "\\" + name;
					if (filesystem.getProperties(path2, out var p1, FAFlags.UseRawPath)
						&& filesystem.getProperties(path, out var p2, FAFlags.UseRawPath)
						&& p1.Size == p2.Size
						&& p1.LastWriteTimeUtc == p2.LastWriteTimeUtc
						) continue;
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
			var a = s.Split(';');
			var h = new HashSet<string>(a.Length, StringComparer.OrdinalIgnoreCase);
			var thisApp = folders.ThisAppBS;
			foreach (var v in a) {
				if (v.Starts(thisApp, true)) break;
				h.Add(pathname.getName(v));
			}
			return h;
		}
	}

	async Task _Uninstall(bool uninstalling = false, bool updating = false) {
		var folder = _Selected.Parent.Name;
		var package = _Selected.Name;
		//if (uninstalling) if (!dialog.showOkCancel("Uninstall package", package, owner: this)) return; //more annoying than useful
		if (!await _RunDotnet($"remove {_ProjPath()} package {package}")) return;

		//Which installed files should be deleted?
		//	Let's delete all files (except .csproj) from the folder and rebuild.
		foreach (var v in filesystem.enumerate(_FolderPath())) {
			if (v.Name.Ends(".csproj", true)) continue;
			if (v.Attributes.Has(FileAttributes.ReadOnly)) continue; //don't delete user-added files
			try { filesystem.delete(v.FullPath); }
			catch (Exception e1) { print.it($"<><c DarkOrange>warning: {e1.Message}<>"); }
		}

		if (!updating) {
			var npath = _nugetDir + @"\nuget.xml";
			if (filesystem.exists(npath)) {
				var xn = XmlUtil.LoadElem(npath);
				var xx = xn.Elem("package", "path", folder + "\\" + package, true);
				if (xx != null) {
					xx.Remove();
					xn.SaveElem(npath);
				}
			}

			if (!await _Build(_Selected.Parent.Name)) return;
			if (uninstalling) print.it("========== Finished ==========");
		}
	}

	async void _Update() {
		var v = _Selected;
		await _Uninstall(updating: true);
		await _Install($"{v.Name}", v.Parent.Name);
	}

	async void _Move() {
		//never mind: the menu contains current folder too
		int i = popupMenu.showSimple(_folders, owner: this) - 1;
		if (i < 0) return;
		var folder = _folders[i];
		var v = _Selected;
		await _Uninstall();
		await _Install($"{v.Name} --version {v.Version}", folder);
	}

	void _AddMeta() {
#if !SCRIPT
		var doc = Panels.Editor.ZActiveDoc; if (doc == null) return;
		var meta = new MetaCommentsParser(doc.zText);
		meta.nuget.Add($@"{_Selected.Parent.Name}\{_Selected.Name}");
		meta.Apply();
#endif
	}

	public static string[] GetInstalledPackages() {
		var xn = XmlUtil.LoadElemIfExists(App.Model.NugetDirectoryBS + "nuget.xml");
		if (xn == null) return null;
		var a = xn.Elements("package").Select(o => o.Attr("path")).ToArray();
		if (!a.Any()) return null;
		Array.Sort(a, StringComparer.OrdinalIgnoreCase);
		return a;
	}

	void _More() {
		var m = new popupMenu();
		m[".NET info"] = async o => { await _RunDotnet("--info"); };
		m["Download .NET SDK"] = o => { run.it("https://dotnet.microsoft.com/en-us/download"); };
		m["About missing files"] = o => print.it(
			@"Some NuGet packages don't install all required files, for example used native dlls.
	Often other files are in other NuGet packages. Then simply install them, and that's all.
	Else you may have to download them. In some cases they are in the .nupkg file (it is a zip file) in the packages cache folder.
		Then click the [Folder] button and put these files there. Set read-only attribute to prevent deleting them when managing packages.
		If missing are managed assemblies, in scripts that use them will need /*/ r Dll; /*/ (use Properties -> Library).
		If used in scripts with role exeProgram, copy these files to the output folder.");
		m.Separator();
		m.Submenu("NuGet cache", m => {
			m["Open packages folder"] = o => {
				if (0 == run.console(out var s, "dotnet.exe", "nuget locals global-packages --list") && !s.NE())
					if (s.RxMatch(@"(?m)^global-packages: (.+)$", 1, out s)) run.it(s);
					else print.it(s);
			};
			m["Clear all caches"] = async o => { await _RunDotnet("nuget locals all --clear"); };
		});
		m.Show();
	}

	async Task<bool> _RunDotnet(string cl, Action<string> printer = null) {
		Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
		try {
			if (printer == null) {
				print.it($"<><c blue>dotnet {cl}<>");
				bool skip = false;
				printer = s => {
					if (!skip) skip = s.Starts("Usage:");
					if (skip) return;
					if (s.Starts("error") || s.Contains(": error ")) s = $"<><c red>{s}<>";
					else if (s.Starts("warn") || s.Contains(": warning ")) s = $"<><c DarkOrange>{s}<>";
					print.it(s);
				};
			}
			return await Task.Run(() => 0 == run.console(printer, "dotnet.exe", cl));
		}
		catch (Exception e1) { dialog.showError("Failed to run dotnet.exe", e1.ToStringWithoutStack(), owner: this); }
		return false;
	}

	void _FillTree(string selectFolder = null, string selectPackage = null) {
		_TreeItem select = null;
		_tvroot = new _TreeItem(true, null);
		foreach (var folder in _folders) {
			var proj = _ProjPath(folder);
			if (!File.Exists(proj)) continue;
			XElement xr; try { xr = XElement.Load(proj); } catch { continue; }
			var k = new _TreeItem(true, folder);
			foreach (var x in xr.XPathSelectElements("/ItemGroup/PackageReference[@Include]")) {
				var name = x.Attr("Include");
				var t = new _TreeItem(false, name, x.Attr("Version"));
				k.AddChild(t);
				if (selectPackage != null && name == selectPackage && (selectFolder == null || folder == selectFolder)) {
					selectPackage = null;
					select = t;
				}
			}
			if (k.HasChildren) _tvroot.AddChild(k);
		}
		_tv.SetItems(_tvroot.Children());
		if (select != null) _tv.SelectSingle(select, true);
		else _panelManage.IsEnabled = false;
	}

	void _SelectInTree() {
		_TreeItem select = null;
		var s = _tPackage.Text;
		if (s.Length > 1) {
			s = s.RxReplace(@"^(?:(?:PM> )?Install-Package )?(\w\S+)( -Version .+)?$", "$1");
			select = _tvroot.Descendants().Where(o => !o.IsFolder && o.Name.Find(s, true) >= 0).FirstOrDefault();
		}

		if (select != null) _tv.SelectSingle(select, true);
		else _tv.UnselectAll();
	}

	_TreeItem _tvroot;

	_TreeItem _Selected => _tv.SelectedItem as _TreeItem;

	string _FolderPath(string folder) => _nugetDir + "\\" + folder;
	string _FolderPath() => _FolderPath(_Selected.Parent.Name);
	string _ProjPath(string folder) => $@"{_nugetDir}\{folder}\{folder}.csproj";
	string _ProjPath() => _ProjPath(_Selected.Parent.Name);

	class _TreeItem : TreeBase<_TreeItem>, ITreeViewItem {
		bool _isExpanded;

		public _TreeItem(bool isFolder, string name, string version = null) {
			IsFolder = _isExpanded = isFolder;
			Name = name;
			Version = version;
			DisplayText = version == null ? name : $"{name}, {version}";
		}

		public string Name { get; }
		public string Version { get; }

		#region ITreeViewItem

		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }
		bool ITreeViewItem.IsExpanded => _isExpanded;
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		public bool IsFolder { get; }
		public string DisplayText { get; }
		string ITreeViewItem.ImageSource => _isExpanded ? @"resources/images/expanddown_16x.xaml" : (IsFolder ? @"resources/images/expandright_16x.xaml" : null);
		//public TVCheck CheckState { get; }

		#endregion
	}
}
