using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Au.Compiler;
using Microsoft.Win32;
using System.Drawing;

class DProperties : KDialogWindow {
	readonly FileNode _f;
	readonly MetaCommentsParser _meta;
	readonly bool _isClass;
	MCRole _role;
	int _miscFlags;
	
	//controls
	readonly KSciInfoBox info;
	readonly ComboBox role, ifRunning, uac, warningLevel;
	readonly TextBox testScript, outputPath, define, noWarnings, testInternal, preBuild, postBuild, findInLists;
	readonly ComboBox icon, manifest, sign;
	readonly KCheckBox bit32, xmlDoc, console, optimize, cMultiline;
	readonly GroupBox gRun, gAssembly, gCompile;
	readonly Button addNuget, addLibrary, addComRegistry, addComBrowse, addProject, addClassFile, addResource, addFile, bOutputPath, bVersion;
	
	public DProperties(FileNode f) {
		_f = f;
		_isClass = f.IsClass;
		_meta = new MetaCommentsParser(_f);
		
		Owner = App.Wmain;
		Title = "Properties of " + _f.Name;
		
		var b = new wpfBuilder(this).WinSize(640).Columns(-1, 0);
		b.WinProperties(WindowStartupLocation.CenterOwner, showInTaskbar: false);
		b.Options(bindLabelVisibility: true);
		b.R.Add(out info).Height(80).Margin("B8").Span(-1);
		b.R.StartStack(vertical: true); //left column
		b.StartGrid().Columns(0, -1, 20, 0, -1.15);
		b.R.Add("role", out role).Skip();
		b.Add("testScript", out testScript)
			.Validation(_ => testScript.Text is string s1 && s1.Length > 0 && null == _f.FindRelative(s1, FNFind.CodeFile, orAnywhere: true) ? "testScript not found" : null);
		b.End();
		
		b.StartStack(out gRun, "Run", vertical: true);
		b.StartGrid().Columns(0, 120, -1, 0, 80)
			.Add("ifRunning", out ifRunning).Skip()
			.Add("uac", out uac);
		b.End();
		b.End();
		
		b.StartGrid(out gCompile, "Compile").Columns(0, 50, 20, 0, -1);
		b.R.Add(out optimize, "optimize").Skip(2)
			.Add("define", out define);
		b.R.Add("warningLevel", out warningLevel).Editable().Skip()
			.Add("noWarnings", out noWarnings);
		b.R.Add("testInternal", out testInternal);
		b.R.StartGrid().Columns(0, -1, 20, 0, -1)
			.Add("preBuild", out preBuild).Skip()
			.Add("postBuild", out postBuild);
		b.End();
		b.End();
		
		b.StartStack(out gAssembly, "Assembly", vertical: true);
		b.StartGrid().Columns(0, -1, 30)
			.Add("outputPath", out outputPath)
			.AddButton(out bOutputPath, "...", _ButtonClick_outputPath)
			.End();
		b.StartGrid().Columns(0, -1, 20, 0, -1);
		b.R.Add("icon", out icon).Editable().Skip()
			.Add("manifest", out manifest).Editable();
		b.R.Add("sign", out sign).Editable().Skip();
		icon.DropDownOpened += _IconManifestSign_DropDownOpened;
		manifest.DropDownOpened += _IconManifestSign_DropDownOpened;
		sign.DropDownOpened += _IconManifestSign_DropDownOpened;
		b.StartDock()
			.Add(out console, "console")
			.Add(out bit32, "bit32").Margin(15)
			.Add(out xmlDoc, "xmlDoc").Margin(15)
			.AddButton(out bVersion, "Version", _ => _VersionInfo()).Align("R")
			.End();
		b.End();
		b.End();
		
		b.End();
		b.StartStack(vertical: true).Margin("L20"); //right column
		b.StartGrid<GroupBox>("Add reference");
		b.R.AddButton(out addLibrary, "Library...", _ButtonClick_addLibrary);
		b.R.AddButton(out addNuget, "NuGet ▾", _ButtonClick_addNuget);
		b.R.AddButton(out addComRegistry, "COM ▾", _bAddComRegistry_Click)
			.AddButton(out addComBrowse, "...", _bAddComBrowse_Click).Width(30);
		b.AddButton(out addProject, "Project ▾", _ButtonClick_addProject);
		b.End();
		b.StartStack<GroupBox>("Add file", vertical: true);
		b.AddButton(out addClassFile, "Class file ▾", _ButtonClick_addClass);
		b.AddButton(out addResource, "Resource ▾", _ButtonClick_addFile);
		b.AddButton(out addFile, "Other file ▾", _ButtonClick_addFile);
		b.End();
		b.StartStack(vertical: true)
			.Add("Find in lists", out findInLists).Tooltip("In button drop-down lists show only items containing this text")
			.End();
		b.End();
		b.R.AddSeparator();
		b.R.StartGrid().Columns(-1, 0);
		b.Add(out cMultiline, "/*/ multiple lines /*/").Checked(_meta.Multiline);
		b.Options(modifyPadding: false); //workaround for: OK/Cancel text incorrectly vcentered. Only on Win11, only in this dialog, only when this dialog is not in the secondary screen with DPI 125%.
		b.AddOkCancel();
		b.End();
		b.End();
		
		_role = _meta.role switch {
			"miniProgram" => MCRole.miniProgram,
			"exeProgram" => MCRole.exeProgram,
			"editorExtension" => MCRole.editorExtension,
			"classLibrary" when _isClass => MCRole.classLibrary,
			"classFile" when _isClass => MCRole.classFile,
			_ => _isClass ? MCRole.classFile : MCRole.miniProgram,
		};
		_InitCombo(role, _isClass ? "miniProgram|exeProgram|editorExtension|classLibrary|classFile" : "miniProgram|exeProgram|editorExtension", null, (int)_role);
		testScript.Text = _f.TestScript?.ItemPath;
		_miscFlags = _meta.miscFlags.ToInt();
		//Run
		_InitCombo(ifRunning, "warn_restart|warn|cancel_restart|cancel|wait_restart|wait|run_restart|run|restart|end|end_restart", _meta.ifRunning);
		_InitCombo(uac, "inherit|user|admin", _meta.uac);
		//Assembly
		outputPath.Text = _meta.outputPath;
		void _ButtonClick_outputPath(WBButtonClickArgs e) {
			var m = new popupMenu();
			m[_GetOutputPath(getDefault: true)] = o => outputPath.Text = o.ToString();
			bool isLibrary = _role == MCRole.classLibrary;
			if (isLibrary) m[@"%folders.ThisApp%\Libraries"] = o => outputPath.Text = o.ToString();
			m["Browse..."] = o => {
				var initf = _GetOutputPath(getDefault: false, expandEnvVar: true);
				if (!isLibrary && !filesystem.exists(initf)) initf = pathname.getDirectory(initf);
				filesystem.createDirectory(initf);
				var d = new FileOpenSaveDialog(isLibrary ? "{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDD}" : "{4D1F3AFB-DA1A-45AC-8C12-51DDA5C51CDD}") {
					InitFolderFirstTime = initf,
				};
				if (d.ShowOpen(out string s, this, selectFolder: true)) outputPath.Text = folders.unexpandPath(s);
			};
			m.Show(owner: this);
		}
		icon.Text = _meta.icon;
		manifest.Text = _meta.manifest;
		sign.Text = _meta.sign;
		if (_meta.console == "true") console.IsChecked = true;
		if (_meta.bit32 == "true") bit32.IsChecked = true;
		if (_meta.xmlDoc == "true") xmlDoc.IsChecked = true;
		//Compile
		if (_meta.optimize == "true") optimize.IsChecked = true;
		define.Text = _meta.define;
		_InitCombo(warningLevel, "6|5|4|3|2|1|0", _meta.warningLevel);
		noWarnings.Text = _meta.noWarnings;
		testInternal.Text = _meta.testInternal;
		preBuild.Text = _meta.preBuild;
		postBuild.Text = _meta.postBuild;
		
		static void _InitCombo(ComboBox c, string items, string meta, int index = -1) {
			var a = items.Split('|');
			if (meta != null) index = Array.IndexOf(a, meta);
			foreach (var v in a) c.Items.Add(v);
			c.SelectedIndex = Math.Max(0, index);
		}
		
		_ChangedRole();
		role.SelectionChanged += (_, _) => {
			_role = (MCRole)role.SelectedIndex;
			_ChangedRole();
		};
		void _ChangedRole() {
			_ShowHide(testScript, _role is MCRole.classLibrary or MCRole.classFile);
			_ShowCollapse(_role is MCRole.miniProgram or MCRole.exeProgram, gRun, console, icon);
			_ShowCollapse(_role is MCRole.exeProgram or MCRole.classLibrary, outputPath, bOutputPath, bVersion);
			_ShowCollapse(_role is MCRole.exeProgram, manifest, bit32);
			_ShowCollapse(_role == MCRole.classLibrary, xmlDoc);
			_ShowCollapse(_role != MCRole.classFile, gAssembly, gCompile);
		}
		
		//rejected. Will display error in code editor. Rarely used. For some would need to remove /suffix.
		//string _ValidateFile(FrameworkElement e, string name, FNFind kind) =>
		
		b.OkApply += _OkApply;
	}
	
	protected override void OnSourceInitialized(EventArgs e) {
		_InitInfo();
		App.Model.UnloadingThisWorkspace += Close;
		base.OnSourceInitialized(e);
	}
	
	protected override void OnClosed(EventArgs e) {
		App.Model.UnloadingThisWorkspace -= Close;
		base.OnClosed(e);
	}
	
	void _GetMeta() {
		//info: _Get returns null if hidden
		
		_f.TestScript = _Get(testScript) is string sts ? _f.FindRelative(sts, FNFind.CodeFile, orAnywhere: true) : null; //validated
		
		_meta.ifRunning = _Get(ifRunning, nullIfDefault: true);
		_meta.uac = _Get(uac, nullIfDefault: true);
		_meta.bit32 = _Get(bit32);
		
		_meta.console = _Get(console);
		_meta.icon = _Get(icon);
		_meta.manifest = _Get(manifest);
		//_meta.resFile = _Get(resFile);
		_meta.sign = _Get(sign);
		_meta.xmlDoc = _Get(xmlDoc);
		
		_meta.optimize = _Get(optimize);
		_meta.define = _Get(define);
		_meta.warningLevel = _Get(warningLevel, nullIfDefault: true);
		_meta.noWarnings = _Get(noWarnings);
		_meta.testInternal = _Get(testInternal);
		_meta.preBuild = _Get(preBuild);
		_meta.postBuild = _Get(postBuild);
		
		_meta.role = null;
		_meta.outputPath = null;
		if (_role != MCRole.classFile) {
			if (_isClass || _role != MCRole.miniProgram) _meta.role = _role.ToString();
			switch (_role) {
			case MCRole.exeProgram:
			case MCRole.classLibrary:
				_meta.outputPath = _GetOutputPath(getDefault: false);
				break;
			}
		}
		
		_meta.miscFlags = _miscFlags == 0 ? null : _miscFlags.ToS();
	}
	
	void _OkApply(WBButtonClickArgs e) {
		if (App.Model.CurrentFile != _f && !App.Model.SetCurrentFile(_f)) return;
		_GetMeta();
		_meta.Multiline = cMultiline.IsChecked;
		_meta.Apply();
	}
	
	void _ButtonClick_addLibrary(WBButtonClickArgs e) {
		string dir1 = App.Model.DllDirectory, dir2 = folders.ThisAppBS + "Libraries", initDir = null;
		bool exists1 = filesystem.exists(dir1).Directory, exists2 = filesystem.exists(dir2).Directory;
		if (exists1 || exists2) {
			var m = new popupMenu();
			if (exists1) m.Add(1, @"%folders.Workspace%\dll");
			if (exists2) m.Add(2, @"%folders.ThisApp%\Libraries");
			m.Add(3, "Last used folder");
			int r = m.Show(owner: this); if (r == 0) return;
			if (r == 1) initDir = dir1; else if (r == 2) initDir = dir2;
		}
		var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-47AC-8C12-41DDA5C51CDB}") {
			FileTypes = "Dll|*.dll|All files|*.*",
			InitFolderNow = initDir
		};
		if (!d.ShowOpen(out string[] a, this)) return;
		
		foreach (var v in a) {
			if (CompilerUtil.IsNetAssembly(v)) continue;
			dialog.showError("Not a .NET assembly.", v, owner: this);
			return;
		}
		
		string appDir = folders.ThisAppBS, dllDir = App.Model.DllDirectoryBS;
		if (a[0].Starts(appDir, true)) {
			for (int i = 0; i < a.Length; i++) a[i] = a[i][appDir.Length..];
		} else if (a[0].Starts(dllDir, true)) {
			for (int i = 0; i < a.Length; i++) a[i] = @"%dll%\" + a[i][dllDir.Length..];
		} else { //unexpand path
			for (int i = 0; i < a.Length; i++) a[i] = folders.unexpandPath(a[i]);
		}
		
		_meta.r.AddRange(a);
		_ShowInfo_Added(e.Button, _meta.r);
	}
	
	void _ButtonClick_addNuget(WBButtonClickArgs e) {
		var a = DNuget.GetInstalledPackages();
		if (a == null) {
			dialog.showInfo(null, "There are no NuGet packages installed in this workspace.\nTo install NuGet packages, use menu -> Tools -> NuGet.", owner: this);
			return;
		}
		var sFind = findInLists.Text;
		if (!sFind.NE()) {
			a = a.Where(s => s.Contains(sFind, StringComparison.OrdinalIgnoreCase)).ToArray();
			if (!a.Any()) return;
		}
		int i = popupMenu.showSimple(a, owner: this) - 1; if (i < 0) return;
		_meta.nuget.Add(a[i]);
		_ShowInfo_Added(e.Button, _meta.nuget);
	}
	
	void _ButtonClick_addProject(WBButtonClickArgs e)
		=> _AddFromWorkspace(
			f => (f != _f && f.GetClassFileRole() == FNClassFileRole.Library) ? f : null,
			_meta.pr, false, e.Button);
	
	void _ButtonClick_addClass(WBButtonClickArgs e) {
		FileNode prFolder1 = null;
		if (_f.IsScript && _f.FindProject(out prFolder1, out var prMain1, ofAnyScript: true) && _f == prMain1) prFolder1 = null;
		
		bool _Include(FileNode f) {
			if (!f.IsClass || f == _f) return false;
			if (f.FindProject(out var prFolder, out var prMain) && !prFolder.Name.Starts("@@")) { //exclude class files that are in projects, except if project name starts with @@
				if (prFolder != prFolder1) return false; //but if _f is a non-project script in a project folder, include local classes
			}
			return f.GetClassFileRole() == FNClassFileRole.Class;
		}
		
		_AddFromWorkspace(f => _Include(f) ? f : null, _meta.c, false, e.Button);
	}
	
	void _ButtonClick_addFile(WBButtonClickArgs e) {
		bool isResource = e.Button == addResource;
		var a = isResource ? _meta.resource : _meta.file;
		var m = new popupMenu();
		if (_f.FindProject(out var proj, out _, ofAnyScript: true)) m.Submenu("Project", m => _AddFW(m, proj));
		m.Submenu("All", m => _AddFW(m));
		m.Submenu("By type", m => _AddFW(m, sortByType: true));
		if (isResource) {
			m.Submenu("Options", m => {
				m.AddCheck("Add XAML icons from code strings like \"*name color\"",
					check: _role is not (MCRole.editorExtension or MCRole.classFile) && 0 == (_miscFlags & 1),
					click: o => { if (o.IsChecked) _miscFlags &= ~1; else _miscFlags |= 1; });
			});
		}
		m.Show(owner: this);
		
		void _AddFW(popupMenu m, FileNode proj = null, bool sortByType = false) {
			_AddFromWorkspace(
				f => {
					if (f.IsCodeFile) return null;
					if (f.IsFolder) { //add if contains non-code files and does not contain code files
						if (proj == null) return null;
						bool has = false;
						foreach (var v in f.Descendants()) { if (v.IsCodeFile) return null; has |= !v.IsFolder; }
						if (!has) return null;
					}
					return f;
				}, a, true, e.Button, proj, m, sortByType);
		}
	}
	
	void _AddFromWorkspace(Func<FileNode, FileNode> filter, List<string> metaList, bool withIcons, UIElement clicked, FileNode folder = null, popupMenu pm = null, bool sortByType = false, bool noInfo = false) {
		var sFind = findInLists.Text;
		List<(FileNode f, string s)> a = new();
		folder ??= App.Model.Root;
		foreach (var f in folder.Descendants()) {
			if (filter(f) is not FileNode f2) continue;
			
			var path = f2.ItemPath;
			if (sFind.Length > 0 && path.Find(sFind, true) < 0) continue;
			
			if (_f.Parent.Parent != null && f.IsDescendantOf(_f.Parent)) path = @".\" + f.ItemPathIn(_f.Parent);
			
			if (!metaList.Contains(path, StringComparer.OrdinalIgnoreCase)) a.Add((f2, path));
		}
		if (a.Count == 0) {
			if (pm == null) _ShowInfo_ListEmpty(clicked, sFind);
			return;
		}
		
		if (sortByType) {
			a = a.OrderBy(o => _GetFileExt(o.f)).ThenBy(o => o.s).ToList();
		} else {
			a = a.OrderBy(o => o.s).ToList();
		}
		
		var m = pm ?? new popupMenu();
		string prevExt = null;
		foreach (var (f, s) in a) {
			if (sortByType) {
				var ext = _GetFileExt(f);
				if (prevExt != null && !ext.Eqi(prevExt)) m.Separator();
				prevExt = ext;
			}
			var v = m.Add(s.Limit(80, middle: true), o => {
				metaList.Add(s);
				if (!noInfo) _ShowInfo_Added(clicked, metaList);
			}, withIcons ? f.FilePath : null);
			if (s[0] == '.') v.TextColor = 0x0080ff;
		}
		if (pm == null) m.Show(owner: this);
	}
	
	static string _GetFileExt(FileNode f) => pathname.getExtension(f.IsLink ? f.LinkTarget : f.Name);
	
	void _IconManifestSign_DropDownOpened(object sender, EventArgs e) {
		var cb = sender as ComboBox;
		cb.IsDropDownOpen = false;
		
		var ext = cb == icon ? ".ico" : cb == manifest ? ".manifest" : ".snk";
		List<string> r = new();
		_AddFromWorkspace(f => f.FileType == FNType.Other && _GetFileExt(f).Eqi(ext) ? f : null, r, cb == icon, cb, noInfo: true);
		if (r.Count > 0) cb.Text = r[0];
	}
	
	#region COM
	
	void _bAddComBrowse_Click(WBButtonClickArgs e) {
		var m = new popupMenu();
		m["Select and convert a COM library..."] = _ => {
			var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDC}") {
				FileTypes = "Type library|*.dll;*.tlb;*.olb;*.ocx;*.exe|All files|*.*"
			};
			if (d.ShowOpen(out string s, this))
				_ConvertTypeLibrary(s, e.Button);
		};
		var dir = folders.Workspace + @".interop";
		if (filesystem.exists(dir)) {
			m.Submenu("Use converted", m => {
				foreach (var f in filesystem.enumFiles(dir, "*.dll")) {
					m[f.Name] = o => {
						var s = o.Text;
						if (!_meta.com.Contains(s)) _meta.com.Add(s);
						_ShowInfo_Added(e.Button, _meta.com);
					};
				}
			});
		}
		m.Show(owner: this);
	}
	
	void _bAddComRegistry_Click(WBButtonClickArgs e) {
		//HKCU\TypeLib\typelibGuid\version\
		var sFind = findInLists.Text;
		var rx = new regexp(@"(?i) (?:Type |Object )?Library[ \d\.]*$");
		var a = new List<_RegTypelib>(1000);
		using (var tlKey = Registry.ClassesRoot.OpenSubKey("TypeLib")) { //guids
			foreach (var sGuid in tlKey.GetSubKeyNames()) {
				if (sGuid.Length != 38) continue;
				//print.it(sGuid);
				using var guidKey = tlKey.OpenSubKey(sGuid);
				foreach (var sVer in guidKey.GetSubKeyNames()) {
					using var verKey = guidKey.OpenSubKey(sVer);
					if (verKey.GetValue("") is string description) {
						if (rx.Match(description, 0, out RXGroup g)) description = description.Remove(g.Start);
						if (sFind.Length > 0 && description.Find(sFind, true) < 0) continue;
						a.Add(new _RegTypelib { guid = sGuid, text = description.Limit(80, middle: true) + ", " + sVer, version = sVer });
					} //else print.it(sGuid); //some Microsoft typelibs. VS does not show these too.
				}
			}
		}
		if (a.Count == 0) { _ShowInfo_ListEmpty(e.Button, sFind); return; }
		a.Sort((x, y) => string.Compare(x.text, y.text, true));
		
		var m = new popupMenu();
		foreach (var v in a) {
			m[v.text] = o => _ConvertTypeLibrary(v, e.Button);
		}
		m.Show(owner: this);
		
		//slow and with scrolling problems
		//	var p = new KPopupListBox { PlacementTarget = e.Button };
		//	p.Control.ItemsSource = a;
		//	p.OK += o => {
		//		_ConvertTypeLibrary(o as _RegTypelib, e.Button);
		//	};
		//	p.IsOpen = true;
		//}
	}
	
	//To convert a COM type library we use TypeLibConverter class. However .NET Core+ does not have it.
	//Workaround: the code is in Au.Net4.exe. It uses .NET Framework 4.8. We call it through run.console.
	//We don't use tlbimp.exe:
	//	1. If some used interop assemblies are in GAC (eg MS Office PIA), does not create files for them. But we cannot use GAC in a Core+ app.
	//	2. Does not tell what files created.
	//	3. My PC somehow has MS Office PIA installed and there is no uninstaller. After deleting the GAC files tlbimp.exe created all files, but it took several minutes.
	//Tested: impossible to convert .NET Framework TypeLibConverter code. Part of it is in extern methods.
	//Tested: cannot use .NET Framework dll for it. Fails at run time because uses Core+ assemblies, and they don't have the class. Need exe.
	
	class _RegTypelib {
		public string text, guid, version;
		
		public override string ToString() => text;
		
		public string GetPath(string locale) {
			var k0 = $@"TypeLib\{guid}\{version}\{locale}\win";
			for (int i = 0; i < 2; i++) {
				var bits = osVersion.is32BitProcess == (i == 1) ? "32" : "64";
				using var hk = Registry.ClassesRoot.OpenSubKey(k0 + bits);
				if (hk?.GetValue("") is string path) return path.Trim('\"');
			}
			return null;
		}
	}
	
	async void _ConvertTypeLibrary(object tlDef, Button button) {
		string comDll = null;
		switch (tlDef) {
		case string path:
			comDll = path;
			break;
		case _RegTypelib r:
			//can be several locales
			var aloc = new List<string>(); //registry keys like "0" or "409"
			var aloc2 = new List<string>(); //locale names for display in the list dialog
			using (var verKey = Registry.ClassesRoot.OpenSubKey($@"TypeLib\{r.guid}\{r.version}")) {
				foreach (var s1 in verKey.GetSubKeyNames()) {
					int lcid = s1.ToInt(0, out int iEnd, STIFlags.IsHexWithout0x);
					if (iEnd != s1.Length) continue; //"FLAGS" etc; must be hex number without 0x
					aloc.Add(s1);
					var s2 = "Neutral";
					if (lcid > 0) {
						try { s2 = new System.Globalization.CultureInfo(lcid).DisplayName; } catch { s2 = s1; }
					}
					aloc2.Add(s2);
				}
			}
			string locale;
			if (aloc.Count == 1) locale = aloc[0];
			else {
				int i = dialog.showList(aloc2, "Locale", owner: this);
				if (i == 0) return;
				locale = aloc[i - 1];
			}
			comDll = r.GetPath(locale);
			if (comDll == null || !filesystem.exists(comDll).File) {
				dialog.showError(comDll == null ? "Failed to get file path." : "File does not exist.", owner: this);
				return;
			}
			break;
		}
		
		print.it($"Converting COM type library to .NET assembly.");
		List<string> converted = new();
		int rr = -1;
		this.IsEnabled = false;
		try {
			await Task.Run(() => {
				var dir = folders.Workspace + @".interop\";
				filesystem.createDirectory(dir);
				void _Callback(string s) {
					print.it(s);
					if (s.Starts("Converted: ")) {
						s.RxMatch(@"""(.+?)"".$", 1, out s);
						converted.Add(s);
					}
				}
				rr = run.console(_Callback, folders.ThisAppBS + "Au.Net4.exe", $"/typelib \"{dir}|{comDll}\"", encoding: Encoding.UTF8);
			});
		}
		catch (Exception ex) { dialog.showError("Failed to convert type library", ex.ToStringWithoutStack(), owner: this); }
		this.IsEnabled = true;
		if (rr == 0) {
			foreach (var v in converted) if (!_meta.com.Contains(v)) _meta.com.Add(v);
			print.it(@"<>Converted and saved in <link>%folders.Workspace%\.interop<>.");
			_ShowInfo_Added(button, _meta.com);
		}
	}
	
	#endregion
	
	#region util
	
	static void _ShowHide(FrameworkElement e, bool show) => e.Visibility = show ? Visibility.Visible : Visibility.Hidden;
	
	static void _ShowCollapse(FrameworkElement e, bool show) => e.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
	
	static void _ShowHide(bool show, params FrameworkElement[] a) {
		foreach (var v in a) _ShowHide(v, show);
	}
	
	static void _ShowCollapse(bool show, params FrameworkElement[] a) {
		foreach (var v in a) _ShowCollapse(v, show);
	}
	
	static bool _IsHidden(FrameworkElement t) {
		if (t.IsVisible) return false;
		if (t.Visibility != Visibility.Visible) return true;
		//is in non-expanded Expander, or expander itself is hidden?
		while ((t = t.Parent as FrameworkElement) != null) if (t is Expander e) return !e.IsVisible;
		return true;
	}
	
	static string _Get(TextBox t, bool nullIfHidden = true) {
		if (nullIfHidden && _IsHidden(t)) return null;
		var r = t.Text.Trim();
		return r == "" ? null : r;
	}
	
	static string _Get(ComboBox t, bool nullIfHidden = true, bool nullIfDefault = false) {
		if (nullIfDefault && t.SelectedIndex == 0) return null;
		if (nullIfHidden && _IsHidden(t)) return null;
		return t.IsEditable ? t.Text : t.SelectedItem as string; //note: t.Text changes after t.SelectionChanged event
	}
	
	static string _Get(KCheckBox t, bool nullIfHidden = true) {
		if (nullIfHidden && _IsHidden(t)) return null;
		return t.IsChecked ? "true" : null;
	}
	
	static bool _IsChecked(KCheckBox t, bool falseIfHidden = true) {
		if (falseIfHidden && _IsHidden(t)) return false;
		return t.IsChecked;
	}
	
	string _GetOutputPath(bool getDefault, bool expandEnvVar = false) {
		if (!getDefault && _Get(outputPath) is string r) {
			if (expandEnvVar) r = pathname.expand(r);
		} else {
			r = MetaComments.GetDefaultOutputPath(_f, _role, withEnvVar: !expandEnvVar);
		}
		return r;
	}
	
	void _ShowInfo_ListEmpty(UIElement by, string sFind) {
		_ShowInfoTooltip(by, sFind.Length > 0 ? "There are no items containing " + sFind : "The list is empty");
	}
	
	void _ShowInfo_Added(UIElement by, List<string> metaList) {
		_ShowInfoTooltip(by, string.Join("\r\n", metaList) + "\r\n\r\nFinally click OK to save.");
	}
	
	void _ShowInfoTooltip(UIElement by, string s) {
		Au.Tools.TUtil.InfoTooltip(ref _tt, by, s, Dock.Right);
	}
	KPopup _tt;
	
	#endregion
	
	#region info
	
	void _InitInfo() {
		info.AaTags.AddLinkTag("+changeFileType", _ => {
			if (!dialog.showOkCancel($"Change file type to: {(_f.IsScript ? "class" : "script")}", "This also will close the Properties dialog as if clicked Cancel.", owner: this)) return;
			_f.FileType = _f.IsScript ? FNType.Class : FNType.Script;
			Close();
		});
		info.aaaText = $"""
Type: <help editor/{(_isClass ? "Class files, projects>C# class file" : "Scripts>C# script")}<>  (<+changeFileType>change...<>)
Path: <explore>{_f.FilePath}<>

C# file properties here are similar to C# project properties in Visual Studio.
Saved in <c green>/*/ meta comments /*/<> at the start of code, and can be edited there too.
""";
		
		info.AaAddElem(role, """
<b>role</b> - purpose of this C# code file. What type of assembly to create and how to execute.
 • <i>miniProgram</i> - execute in a separate host process started from editor.
 • <i>exeProgram</i> - create/execute .exe file. It can run on any computer, without editor installed.
 • <i>editorExtension</i> - execute in the editor's UI thread. Rarely used. Incorrect code can kill editor.
 • <i>classLibrary</i> - create .dll file. It can be used in C# scripts and other .NET-based programs.
 • <i>classFile</i> - don't create/execute. Compile together with other C# code files in the project or using meta comment c. Inherits meta comments of the main file of the compilation.

Default role for scripts is miniProgram; cannot be the last two. Default for class files is classFile.

Read more in Cookbook -> Script (classes, .exe) and online help -> Editor (scripts, class files).
""");
		info.AaAddElem(testScript, """
<b>testScript</b> - a script to run when you click the Run button.
Usually it is used to test this class file or class library. It can contain meta comment <c green>c this file<> that adds this file to the compilation, or <c green>pr this file<> that adds the output dll file as a reference assembly. The recommended way to add this option correctly and easily is to try to run this file and click a link that is then printed in the output.

Can be:
 • Path in the workspace. Examples: \Script5.cs, \Folder\Script5.cs.
 • Path relative to this file. Examples: Folder\Script5.cs, .\Script5.cs, ..\Folder\Script5.cs.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.

This option is saved not in meta comments.
""");
		info.AaAddElem(ifRunning, """
<b>ifRunning</b> - when trying to start this script, what to do if it is already running.
 • <i>warn</i> - print warning and don't run.
 • <i>cancel</i> - don't run.
 • <i>wait</i> - run later, when it ends.
 • <i>run</i> - run simultaneously.
 • <i>restart</i> - end it and run.
 • <i>end</i> - end it and don't run.

Suffix _restart means restart if starting the script with the Run button/menu.
Default is warn_restart.

This option is ignored when the task runs as .exe program started not from editor; instead use code: script.single("unique string");.
""");
		info.AaAddElem(uac, """
<b>uac</b> - <help articles/UAC>UAC<> integrity level (IL) of the task process.
 • <i>inherit</i> (default) - the same as of the editor process. Normally High IL if installed on admin account, else Medium IL.
 • <i>user</i> - Medium IL, like most applications. The task cannot automate high IL process windows, write some files, change some settings, etc.
 • <i>admin</i> - High IL, aka "administrator", "elevated". The task has many rights, but cannot automate some apps through COM, etc.

This option is ignored when the task runs as .exe program started not from editor.
""");
		info.AaAddElem(outputPath, """
<b>outputPath</b> - directory for the output assembly file and related files (used dlls, etc).
Full path. Can start with %environmentVariable% or %folders.SomeFolder%. Can be path relative to this file or workspace, like with other options.

Default if role exeProgram: <link>%folders.Workspace%\exe<>\filename. Default if role classLibrary: <link>%folders.Workspace%\dll<>. The compiler creates the folder if does not exist.

If role exeProgram, the exe file is named like the script. The 32-bit version has suffix "-32". If optimize true (checked), creates both 64-bit and 32-bit versions. Else creates only 32-bit if bit32 true (checked) or 32-bit OS, else only 64-bit.
If role classLibrary, the dll file is named like the class file. It can be used by 64-bit and 32-bit processes.
""");
		info.AaAddElem(icon, """
<b>icon</b> - icon of the output exe file.

The icon will be added as a native resource and displayed in File Explorer etc. If role exeProgram, can add all .ico and .xaml icons from folder. Resource ids start from IDI_APPLICATION (32512). Native resources can be used with icon.ofThisApp etc and dialog functions.

The file must be in this workspace. Import files if need, for example drag-drop. Can be a link.
Can be:
 • Path in the workspace. Examples: \App.ico, \Folder\App.ico.
 • Path relative to this file. Examples: Folder\App.ico, .\App.ico, ..\Folder\App.ico.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.

If not specified, uses custom icon of the main C# file. See menu Tools -> Icons.
""");
		info.AaAddElem(manifest, """
<b>manifest</b> - <google manifest file site:microsoft.com>manifest<> of the output exe file.

The file must be in this workspace. Import files if need, for example drag-drop. Can be a link.
Can be:
 • Path in the workspace. Examples: \App.manifest, \Folder\App.manifest.
 • Path relative to this file. Examples: Folder\App.manifest, .\App.manifest, ..\Folder\App.manifest.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.

The manifest will be added as a native resource.
""");
		info.AaAddElem(sign, """
<b>sign</b> - strong-name signing key file, to sign the output assembly.

The file must be in this workspace. Import files if need, for example drag-drop. Can be a link.
Can be:
 • Path in the workspace. Examples: \App.snk, \Folder\App.snk.
 • Path relative to this file. Examples: Folder\App.snk, .\App.snk, ..\Folder\App.snk.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.
""");
		info.AaAddElem(console, """
<b>console</b> - let the program run with console.
""");
		info.AaAddElem(bit32, """
<b>bit32</b> - whether the exe process must be 32-bit everywhere.
 • <i>false</i> (default) - the process is 64-bit or 32-bit, the same as Windows on that computer.
 • <i>true</i> (checked) - the process is 32-bit on all computers.
""");
		info.AaAddElem(xmlDoc, """
<b>xmlDoc</b> - create XML documentation file from /// comments. And print errors in /// comments.

XML documentation files are used by code editors to display class/function/parameter info. Also can be used to create HTML documentation.
""");
		info.AaAddElem(optimize, """
<b>optimize</b> - whether to make the compiled code as fast as possible.
 • <i>false</i> (default) - don't optimize. Define DEBUG and TRACE. Aka "Debug configuration".
 • <i>true</i> (checked) - optimize. Aka "Release configuration".

Default is false, because optimization makes difficult to debug. It makes noticeably faster only some types of code, for example processing of text and byte arrays. Before deploying class libraries and exe programs always compile with optimize true.

This option is also applied to class files compiled together, eg as part of project. Use true (checked) if they contain code that must be as fast as possible.
""");
		info.AaAddElem(define, """
<b>define</b> - symbols that can be used with #if.
Example: ONE,TWO,d:THREE,r:FOUR
Can be used prefix r: or d: to define the symbol only if optimize true (checked) or false (unchecked).
If no optimize true, DEBUG and TRACE are added implicitly.
These symbols also are visible in class files compiled together, eg as part of project.
See also <google C# #define>#define<>.
""");
		info.AaAddElem(warningLevel, """
<b>warningLevel</b> - <google C# Compiler Options, WarningLevel>warning level<>. Default 6.
0 - no warnings.
1 - only severe warnings.
2 - level 1 plus some less-severe warnings.
3 - most warnings.
4 - all warnings added in C# 1-8.
5-9999 - level 4 plus warnings added in C# 9+.

This option is also applied to class files compiled together, eg as part of project.
""");
		info.AaAddElem(noWarnings, """
<b>noWarnings</b> - don't show these warnings.
Example: 151,3001,120

This option is also applied to class files compiled together, eg as part of project.
See also <google C# #pragma warning>#pragma warning<>.
""");
		info.AaAddElem(testInternal, """
<b>testInternal</b> - access internal symbols of these assemblies, like with InternalsVisibleToAttribute.
Example: Assembly1,Assembly2

This option is also applied to class files compiled together, eg as part of project.
""");
		info.AaAddElem(preBuild, """
<b>preBuild</b> - a script to run before compiling this code file.

The script must have role editorExtension. It runs synchronously in the compiler's thread. To stop compilation, let it throw an exception.
By default it receives full path of the output exe or dll file in args[0]. If need more info, specify command line arguments, like in this example: Script5.cs /$(outputPath) $(optimize). The script will receive real values in args[0], args[1] and so on. Variables:
 • $(outputFile) - full path of the output exe or dll file.
 • $(outputPath) - meta comment 'outputPath'.
 • $(source) - path of this C# code file in the workspace.
 • $(role) - meta comment 'role'.
 • $(optimize) - meta comment 'optimize'.
 • $(bit32) - meta comment 'bit32'.

Can be:
 • Path in the workspace. Examples: \Script5.cs, \Folder\Script5.cs.
 • Path relative to this file. Examples: Folder\Script5.cs, .\Script5.cs, ..\Folder\Script5.cs.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.
""");
		info.AaAddElem(postBuild, """
<b>postBuild</b> - a script to run after compiling this code file successfully.
Everything else is like with preBuild.
""");
		info.AaAddElem(addLibrary, """
<b>Library<> - add a .NET assembly reference.
Adds meta comment <c green>r DllFile<>.

Don't need to add Au.dll and .NET runtime dlls.
To use 'extern alias', edit in the code editor like this: <c green>r Alias=Assembly<>
To remove this meta comment, edit the code.
If script role is editorExtension, may need to restart editor.
""");
		info.AaAddElem(addNuget, """
<b>NuGet<> - use a NuGet package installed by the NuGet tool (menu Tools -> NuGet).
Adds meta comment <c green>nuget folder\package<>.

To remove this meta comment, edit the code.
""");
		
		const string c_com = """
 COM component's type library to an <i>interop assembly<>, and use it.
Adds meta comment <c green>com FileName.dll<>. Saves the assembly file in <link>%folders.Workspace%\.interop<>.

An interop assembly is a .NET assembly without real code. Not used at run time. At run time is used the COM component (registered unmanaged dll or exe file). If 64-bit dll unavailable, can be used only in a 32-bit program (role exeProgram, bit32 checked).

To remove this meta comment, edit the code. Optionally delete unused interop assemblies.
""";
		info.AaAddElem(addComRegistry, "<b>COM<> - convert a registered" + c_com);
		info.AaAddElem(addComBrowse, "<b>...<> - convert a" + c_com);
		info.AaAddElem(addProject, """
<b>Project<> - add a reference to a class library created in this workspace.
Adds meta comment <c green>pr File.cs<>. The compiler will compile it if need and use the created dll file as a reference.

To remove this meta comment, edit the code. Optionally delete unused dll files.
""");
		info.AaAddElem(addClassFile, """
<b>Class file<> - add a C# code file that contains some classes/functions used by this file.
Adds meta comment <c green>c File.cs<>. The compiler will compile all code files and create single assembly.

The file must be in this workspace. Import files if need, for example drag-drop. Can be a link. If folder, adds all its descendant class files.
Can be:
 • Path in the workspace. Examples: \Class5.cs, \Folder\Class5.cs.
 • Path relative to this file. Examples: Folder\Class5.cs, .\Class5.cs, ..\Folder\Class5.cs.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.

If this file is in a project, don't add class files that are in the project folder.
To remove this meta comment, edit the code.
""");
		//FUTURE: add UI to append resource suffix
		info.AaAddElem(addResource, """
<b>Resource<> - add image etc file(s) as managed resources.
Adds meta comment <c green>resource File<>.

Default resource type is Stream. You can append <c green>/byte[]<> or <c green>/string<>, like <c green>resource file.txt /string<>. Or <c green>/strings<>, to add multiple strings from 2-column CSV file (name, value). Or <c green>/embedded<>, to add as a separate top-level stream that can be loaded with <google>Assembly.GetManifestResourceStream<> (others are in top-level stream "AssemblyName.g.resources").

The file must be in this workspace. Import files if need, for example drag-drop. Can be a link. If folder, will add all its descendant files.
Can be:
 • Path in the workspace. Examples: \File.png, \Folder\File.png.
 • Path relative to this file. Examples: Folder\File.png, .\File.png, ..\Folder\File.png.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.

To remove this meta comment, edit the code.

To load resources can be used <help>Au.More.ResourceUtil<>, like <code>var s = ResourceUtil.GetString("file.txt");</code>. Or <google>ResourceManager<>. To load WPF resources can be used "pack:..." URI; if role miniProgram, assembly name is like *ScriptName.

Resource names in assembly by default are like "file.png". When adding a folder with subfolders, may be path relative to that folder, like "subfolder/file.png". If need path relative to this C# file, append space and <c green>/path<>. Resource names are lowercase, except <c green>/embedded<> and <c green>/strings<>. This program does not URL-encode resource names; WPF "pack:..." URI does not work if resource name contains spaces, non-ASCII or other URL-unsafe characters. Also this program does not convert XAML to BAML.

To browse .NET assembly resources, types, etc can be used for example <google>ILSpy<>.
""");
		info.AaAddElem(addFile, """
<b>Other file<> - declare an unmanaged dll or other file used at run time.
Adds meta comment <c green>file File<>.

If role is exeProgram, the compiler will copy the file to the output folder. Or subfolder: <c green>file.dll /sub<>.
If role is miniProgram or editorExtension, will store the dll path in order to find it at run time.
If role of this file is classFile, the above actions will be used when compiling scripts that use it.
If role of this file is classLibrary, the above actions will be used when compiling scripts that use it as a project reference. The compiler never copies these files to the output folder of the library.

The file must be in this workspace. Import files if need, for example drag-drop. Can be a link.
Can be:
 • Path in the workspace. Examples: \File.png, \Folder\File.png.
 • Path relative to this file. Examples: Folder\File.png, .\File.png, ..\Folder\File.png.
 • Filename. The file can be anywhere; will be used the one in the same folder if exists.

If folder, will include all its descendant files. Will copy them into folders like in the workspace. If a folder name ends with -, will copy its contents only.

If an exeProgram script uses unmanaged 64-bit and 32-bit dll files, consider placing them in subfolders named "64" and "32". Then at run time will be loaded correct dll version.

To remove this meta comment, edit the code.
""");
		info.AaAddElem(bVersion, "<b>Version</b> - how to add version info.");
	}
	
	static void _VersionInfo() {
		print.it($$"""
<>To add assembly file version info, insert and edit this code near the start of any C# file of the compilation.

<code>using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]
//[assembly: AssemblyFileVersion("1.0.0.0")] //if missing, uses AssemblyVersion
//[assembly: AssemblyTitle("File description")]
//[assembly: AssemblyDescription("Comments")]
//[assembly: AssemblyCompany("Company name")]
//[assembly: AssemblyProduct("Product name")]
//[assembly: AssemblyInformationalVersion("1.0.0.0")] //product version
//[assembly: AssemblyCopyright("Copyright © {{DateTime.Now.Year}} ")]
//[assembly: AssemblyTrademark("Legal trademarks")]
</code>
""");
	}
	
	#endregion
}
