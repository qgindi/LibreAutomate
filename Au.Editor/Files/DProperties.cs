//TODO: UI to set r etc alias and noCopy

using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Microsoft.Win32;
using System.Drawing;
using System.Windows.Documents;
using UnsafeTools;

namespace LA;

class DProperties : KDialogWindow {
	public static void ShowFor(FileNode f) {
		f.SingleDialog(() => new DProperties(f));
	}
	
	readonly FileNode _f;
	readonly MetaCommentsParser _meta;
	readonly bool _isClass;
	MCRole _role;
	int _miscFlags;
	
	//controls
	readonly KSciInfoBox info;
	readonly ComboBox role, ifRunning, uac, warningLevel, nullable, platform;
	readonly TextBox testScript, define, noWarnings, testInternal, preBuild, postBuild, findInLists;
	readonly ComboBox outputPath, icon, manifest, sign;
	readonly KCheckBox xmlDoc, console, optimize, cMultiline;
	readonly KGroupBoxSeparator gAssembly, gCompile;
	readonly Panel pRun;
	readonly Button addNuget, addLibrary, addComRegistry, addComBrowse, addProject, addClassFile, addResource, addFile;
	
	DProperties(FileNode f) {
		_f = f;
		_isClass = f.IsClass;
		_meta = new MetaCommentsParser(_f);
		
		InitWinProp("Properties of " + _f.Name, App.Wmain);
		
		var b = new wpfBuilder(this).WinSize(800).Columns(-1, 20, -1, 20, 0);
		b.Options(bindLabelVisibility: true);
		b.R.Add(out info).Height(60).Margin("B8");
		
		//. left column
		b.R.StartGrid();
		
		b.R.StartGrid<KGroupBoxSeparator>("Role");
		b.R.Add("role", out role);
		b.End();
		
		b.R.StartGrid(out gCompile, "Compile").Columns(0, -1, 20, 0, -2);
		b.R.Add(out optimize, "optimize");
		b.R.Add("define", out define);
		b.R.Add("warningLevel", out warningLevel).Editable();
		b.Skip().Add("nullable", out nullable);
		b.R.Add("noWarnings", out noWarnings);
		b.R.Add("testInternal", out testInternal);
		b.R.Add("preBuild", out preBuild);
		b.R.Add("postBuild", out postBuild);
		b.End();
		
		b.End();
		//..
		
		//. center column
		b.Skip(1).StartGrid();
		
		b.R.StartGrid<KGroupBoxSeparator>("Run");
		b.StartGrid(); pRun = b.Panel;
		b.R.Add("ifRunning", out ifRunning);
		b.R.Add("uac", out uac);
		b.End();
		b.R.Add("testScript", out testScript)
			.Validation(_ => testScript.IsVisible && testScript.IsEnabled && testScript.Text is string s1 && s1.Length > 0 && null == _f.FindRelative(true, s1, FNFind.CodeFile) ? "testScript not found" : null);
		b.End();
		
		b.R.StartGrid(out gAssembly, "Assembly");
		b.Add("outputPath", out outputPath).Editable();
		outputPath.DropDownOpened += _OutputPath_DropDownOpened;
		b.R.Add("icon", out icon).Editable()
			.Add("manifest", out manifest).Editable();
		b.R.Add("sign", out sign).Editable();
		icon.DropDownOpened += _IconManifestSign_DropDownOpened;
		manifest.DropDownOpened += _IconManifestSign_DropDownOpened;
		sign.DropDownOpened += _IconManifestSign_DropDownOpened;
		b.R.Add(out console, "console")
			.And(0).Add(out xmlDoc, "xmlDoc");
		b.R.Add("platform", out platform).Width(100, "L");
		b.End();
		
		b.End();
		//..
		
		//. right column
		b.Skip(1).StartStack(vertical: true);
		
		b.StartGrid<KGroupBoxSeparator>("Add reference");
		b.R.AddButton(out addLibrary, "Library...", _ButtonClick_addLibrary);
		b.R.AddButton(out addNuget, "NuGet ▾", _ButtonClick_addNuget);
		b.R.AddButton(out addComRegistry, "COM ▾", _bAddComRegistry_Click)
			.AddButton(out addComBrowse, "...", _bAddComBrowse_Click).Width(30);
		b.AddButton(out addProject, "Project ▾", _ButtonClick_addProject);
		b.End();
		
		b.StartStack<KGroupBoxSeparator>("Add file", vertical: true);
		b.AddButton(out addClassFile, "Class file ▾", _ButtonClick_addClass);
		b.AddButton(out addResource, "Resource ▾", _ButtonClick_addFile);
		b.AddButton(out addFile, "Other file ▾", _ButtonClick_addFile);
		b.End();
		
		b.Add<AdornerDecorator>().Child().Add(out findInLists).Watermark("Find in lists")
			.Tooltip("In button drop-down lists show only items containing this text.\n\nTip: to hide garbage files, put them in folder(s) named \"Garbage\".");
		
		b.End();
		//..
		
		b.R.AddSeparator();
		
		b.R.StartGrid().Columns(-1, 0, 0);
		b.Add(out cMultiline, "/*/ multiple lines /*/").Checked(_meta.Multiline);
		b.Options(modifyPadding: false); //workaround for: OK/Cancel text incorrectly vcentered. Only on Win11, only in this dialog, only when this dialog is not in the secondary screen with DPI 125%.
		b.AddOkCancel();
		b.xAddDialogHelpButtonAndF1("editor/File properties"); //TODO: test
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
		void _OutputPath_DropDownOpened(object sender, EventArgs e) {
			outputPath.IsDropDownOpen = false;
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
		if (_meta.console is "true" or "!false") console.IsChecked = true;
		_InitCombo(platform, "Default|x64|arm64|x86", _meta.platform);
		if (_meta.xmlDoc == "true") xmlDoc.IsChecked = true;
		//Compile
		if (_meta.optimize is "true" or "!false") optimize.IsChecked = true;
		define.Text = _meta.define;
		_InitCombo(warningLevel, "8|7|6|5|4|3|2|1|0", _meta.warningLevel, 0);
		noWarnings.Text = _meta.noWarnings;
		_InitCombo(nullable, "disable|enable|warnings|annotations", _meta.nullable);
		testInternal.Text = _meta.testInternal;
		preBuild.Text = _meta.preBuild;
		postBuild.Text = _meta.postBuild;
		
		static void _InitCombo(ComboBox c, string items, string meta, int index = 0) {
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
			bool? ts = _role is MCRole.classLibrary or MCRole.classFile ? (_f.FindProject(out _, out var pmain) && _f != pmain ? null : true) : false;
			_ShowCollapse(testScript, ts != false);
			if (ts == null) { testScript.IsEnabled = false; testScript.Text = "<the first C# file of this project>"; }
			_ShowCollapse(_role is MCRole.miniProgram or MCRole.exeProgram, pRun, console, icon);
			_ShowCollapse(_role is MCRole.exeProgram or MCRole.classLibrary, outputPath);
			_ShowCollapse(_role is MCRole.exeProgram, manifest, platform);
			_ShowCollapse(_role == MCRole.classLibrary, xmlDoc);
			_ShowCollapse(_role != MCRole.classFile, gAssembly, gCompile);
		}
		
		//rejected. Will display error in code editor. Rarely used. For some would need to remove /suffix.
		//string _ValidateFile(FrameworkElement e, string name, FNFind kind) =>
		
		b.OkApply += _OkApply;
	}
	
	protected override void OnSourceInitialized(EventArgs e) {
		_InitInfo();
		base.OnSourceInitialized(e);
	}
	
	void _GetMeta() {
		//info: _Get returns null if hidden
		
		_f.TestScript = _Get(testScript) is string sts && testScript.IsEnabled ? _f.FindRelative(true, sts, FNFind.CodeFile) : null; //validated
		
		_meta.ifRunning = _Get(ifRunning, defaultIndex: 0);
		_meta.uac = _Get(uac, defaultIndex: 0);
		_meta.platform = _Get(platform, defaultIndex: 0);
		
		_meta.console = _Get(console);
		_meta.icon = _Get(icon);
		_meta.manifest = _Get(manifest);
		//_meta.resFile = _Get(resFile);
		_meta.sign = _Get(sign);
		_meta.xmlDoc = _Get(xmlDoc);
		
		_meta.optimize = _Get(optimize);
		_meta.define = _Get(define);
		_meta.warningLevel = _Get(warningLevel, defaultIndex: 0);
		_meta.noWarnings = _Get(noWarnings);
		_meta.nullable = _Get(nullable, defaultIndex: 0);
		_meta.testInternal = _Get(testInternal);
		_meta.preBuild = _Get(preBuild);
		_meta.postBuild = _Get(postBuild);
		
		var oldRole = _meta.role;
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
		
		if (_isClass && oldRole is null or "classLibrary" && !(_meta.role is null or "classLibrary")) {
			print.it($$"""
<>Info: Now <help editor/Class files, projects>class file<> '{{_f.Name}}' can be executed directly. Just add code that calls a class function. Example:
<code>/*/ role {{_meta.role}}; define TEST; /*/
#if TEST //allows to use this class file elsewhere via /*/ c {{_f.Name}}; /*/
Class1.Function1();
#endif
class Class1 {
	public static void Function1() {
		print.it(1);
	}
}
</code>
""");
		}
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
			var m = new popupMenu { CheckDontClose = true };
			if (exists1) m.Add(1, @"%folders.Workspace%\dll");
			if (exists2) m.Add(2, @"%folders.ThisApp%\Libraries");
			m.Add(3, "Last used folder");
			m.Separator();
			m.AddCheck("Unexpand path (option)", App.Settings.tools_pathUnexpand, o => App.Settings.tools_pathUnexpand ^= true);
			int r = m.Show(owner: this); if (r == 0) return;
			if (r == 1) initDir = dir1; else if (r == 2) initDir = dir2;
		}
		var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-47AC-8C12-41DDA5C51CDB}") {
			FileTypes = "Dll|*.dll|All files|*.*",
			InitFolderNow = initDir
		};
		if (!d.ShowOpen(out string[] a, this)) return;
		
		if (!TUtil2.UnexpandPathsMetaR(a, this)) return;
		
		_meta.r.AddRange(a);
		_ShowInfo_Added(e.Button, _meta.r);
	}
	
	void _ButtonClick_addNuget(WBButtonClickArgs e) {
		var a = DNuget.GetInstalledPackages();
		if (a == null) {
			dialog.showInfo(null, "There are no NuGet packages installed in this workspace.\nTo install NuGet packages, use menu Tools > NuGet.", owner: this);
			return;
		}
		var sFind = findInLists.Text;
		if (!sFind.NE()) {
			a = a.Where(s => s.Contains(sFind, StringComparison.OrdinalIgnoreCase)).ToArray();
			if (!a.Any()) return;
		}
		int i = popupMenu.showSimple(a, owner: this, rawText: true) - 1; if (i < 0) return;
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
		List<(FileNode f, string s, bool near)> a = new();
		folder ??= App.Model.Root;
		foreach (var f in folder.DescendantsExceptGarbage()) {
			if (filter(f) is not FileNode f2) continue;
			
			var path = f2.ItemPath;
			if (sFind.Length > 0 && path.Find(sFind, true) < 0) continue;
			
			//if (_f.Parent.Parent != null && f.IsDescendantOf(_f.Parent)) path = "." + f.ItemPathIn(_f.Parent); //rejected. Bad with export-import.
			
			if (!metaList.Contains(path, StringComparer.OrdinalIgnoreCase)) {
				bool near = _f.Parent.Parent != null && f.IsDescendantOf(_f.Parent);
				a.Add((f2, path, near));
			}
		}
		if (a.Count == 0) {
			if (pm == null) _ShowInfo_ListEmpty(clicked, sFind);
			return;
		}
		
		if (sortByType) {
			a = a.OrderBy(o => o.f.FileExt).ThenBy(o => !o.near).ThenBy(o => o.s).ToList();
		} else {
			a = a.OrderBy(o => !o.near).ThenBy(o => o.s).ToList();
		}
		
		var m = pm ?? new popupMenu();
		string prevExt = null;
		foreach (var (f, s, near) in a) {
			if (sortByType) {
				var ext = f.FileExt;
				if (prevExt != null && !ext.Eqi(prevExt)) m.Separator();
				prevExt = ext;
			}
			var v = m.Add(s.Limit(80, middle: true), o => {
				//metaList.Add(s[0] == '.' ? s : f.ItemPathOrName());
				metaList.Add(f.ItemPathOrName(relativeTo: _f));
				if (!noInfo) _ShowInfo_Added(clicked, metaList);
			}, withIcons ? f.FilePath : null);
			//if (s[0] == '.') v.TextColor = 0x0080ff;
			if (near) v.TextColor = 0x0080ff;
		}
		if (pm == null) m.Show(owner: this);
	}
	
	void _IconManifestSign_DropDownOpened(object sender, EventArgs e) {
		var cb = sender as ComboBox;
		cb.IsDropDownOpen = false;
		
		var ext = cb == icon ? ".ico" : cb == manifest ? ".manifest" : ".snk";
		List<string> r = new();
		_AddFromWorkspace(f => f.FileType == FNType.Other && f.FileExt.Eqi(ext) ? f : null, r, cb == icon, cb, noInfo: true);
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
			m["Delete converted..."] = _ => run.itSafe(dir);
		}
		m.Show(owner: this);
	}
	
	void _bAddComRegistry_Click(WBButtonClickArgs e) {
		//HKCU\TypeLib\typelibGuid\version\
		var sFind = findInLists.Text;
		var rx = new regexp(@"(?i) (?:Type |Object )?Library[ \d\.]*$");
		var a = new List<EdComUtil.RegTypelib>(1000);
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
						a.Add(new(description.Limit(80, middle: true) + ", " + sVer, sGuid, sVer));
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
	}
	
	async void _ConvertTypeLibrary(object tlDef, Button button) {
		if (await EdComUtil.ConvertTypeLibrary(tlDef, this) is { } converted) {
			foreach (var v in converted) if (!_meta.com.Contains(v)) _meta.com.Add(v);
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
		return true;
	}
	
	static string _Get(TextBox t, bool nullIfHidden = true) {
		if (nullIfHidden && _IsHidden(t)) return null;
		var r = t.Text.Trim();
		return r == "" ? null : r;
	}
	
	static string _Get(ComboBox t, bool nullIfHidden = true, int? defaultIndex = null) {
		if (defaultIndex.HasValue && t.SelectedIndex == defaultIndex.Value) return null;
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
		UnsafeTools.TUtil.InfoTooltip(ref _tt, by, s, Dock.Right);
	}
	KPopup _tt;
	
	#endregion
	
	#region info
	
	void _InitInfo() {
		info.AaTags.AddStyleTag(".c", new() { backColor = 0xF0F0F0, monospace = true }); //inline code
		info.AaTags.AddLinkTag("+changeFileType", _ => {
			if (!dialog.showOkCancel($"Change file type to: {(_f.IsScript ? "class" : "script")}", "This also will close the Properties dialog as if clicked Cancel.", owner: this)) return;
			_f.FileType = _f.IsScript ? FNType.Class : FNType.Script;
			Close();
		});
		
		var dialogInfo = $"""
File type: <help editor/{(_isClass ? "Class files, projects>C# class file" : "Scripts>C# script")}<>  (<+changeFileType>change...<>)
File path: <explore>{_f.FilePath}<>
""";
		info.aaaText = dialogInfo;
		info.AaAddElem(this, dialogInfo);
		
		info.AaAddElem(role, """
<b>role<> - the purpose of this C# code file. What type of assembly to create and how to execute.
 • <.c>miniProgram<> - execute in a separate host process started from editor.
 • <.c>exeProgram<> - create/execute .exe file, which can run on any computer, without editor installed.
 • <.c>editorExtension<> - execute in the editor's UI thread. Rarely used. Incorrect code can kill editor.
 • <.c>classLibrary<> - create .dll file, which can be used in C# scripts and other .NET-based programs.
 • <.c>classFile<> - don't create/execute. The file can be used by other C# files, either as part of the project or added explicitly.
""");
		
		#region Run
		info.AaAddElem(testScript, """
<b>testScript<> - a script to run when you click the <b>Run<> button.

Press F1 for more info.
""");
		info.AaAddElem(ifRunning, """
<b>ifRunning<> - when trying to start this script, what to do if it is already running.
 • <.c>warn<> - print warning and don't run.
 • <.c>cancel<> - don't run.
 • <.c>wait<> - run later, when it ends.
 • <.c>run<> - run simultaneously.
 • <.c>restart<> - end it and run.
 • <.c>end<> - end it and don't run.

Suffix <.c>_restart<> means restart if starting the script with the <b>Run<> button.
""");
		info.AaAddElem(uac, """
<b>uac<> - <help articles/UAC>UAC<> integrity level (IL) of the task process.
 • <.c>inherit<> (default) - the same as of the editor process.
 • <.c>user<> - Medium IL, like most applications. The task cannot automate some windows etc.
 • <.c>admin<> - High IL, aka "administrator", "elevated".
""");
		#endregion
		
		#region Compile
		info.AaAddElem(optimize, """
<b>optimize<> - whether to make the compiled code as fast as possible.
 • <.c>false<> (default) - don't optimize. Define <.c>DEBUG<> and <.c>TRACE<>. Aka "Debug configuration".
 • <.c>true<> (checked) - optimize. Aka "Release configuration".
""");
		info.AaAddElem(define, """
<b>define<> - symbols that can be used with <.c>#if<>.

Example: <.c>ONE,TWO,r:THREE,d:FOUR<>
Prefix <.c>r:<> - if <.c>optimize true<>. Prefix <.c>d:<> - if no <.c>optimize true<>.

See also <google C# #define>#define<>.
""");
		info.AaAddElem(warningLevel, $"""
<b>warningLevel<> - <google C# Compiler Options, WarningLevel>warning level<>.
0 - no warnings.
1 - only severe warnings.
2 - level 1 plus some less-severe warnings.
3 - most warnings.
4 - all warnings of C# 1-8.
5-9999 - level 4 plus warnings added in C# 9+.
""");
		info.AaAddElem(noWarnings, $"""
<b>noWarnings<> - don't show these warnings.

Example: <.c>151,3001,CS1234<>

See also <google C# #pragma warning>#pragma warning<>.
""");
		info.AaAddElem(nullable, """
<b>nullable<> - <google C# Nullable reference types>nullable context<>.
disable - no warnings; code does not use nullable syntax (<.c>Type? variable<>).
enable - print warnings; code uses nullable syntax.
warnings - print warnings; code does not use nullable syntax.
annotations - no warnings; code uses nullable syntax.
""");
		info.AaAddElem(testInternal, """
<b>testInternal<> - can use internal members of these assemblies, like with <.c>InternalsVisibleToAttribute<>.

Example: <.c>Assembly1,Assembly2<>
""");
		info.AaAddElem(preBuild, """
<b>preBuild<> - a script to run before compiling.

To create new preBuild script: menu <b>File > New > More<>.
Press F1 for more info.
""");
		info.AaAddElem(postBuild, """
<b>postBuild<> - a script to run after compiling successfully.

To create new postBuild script: menu <b>File > New > More<>.
Press F1 for more info.
""");
		#endregion
		
		#region Assembly
		info.AaAddElem(outputPath, """
<b>outputPath<> - directory for the output files (exe, dll etc).

Press F1 for more info.
""");
		info.AaAddElem(icon, """
<b>icon<> - icon(s) of the output exe file.

Press F1 for more info.
""");
		info.AaAddElem(manifest, """
<b>manifest<> - <google manifest file site:microsoft.com>manifest<> of the output exe file.

Press F1 for more info.
""");
		info.AaAddElem(sign, """
<b>sign<> - strong-name signing key file, to sign the output assembly.

Press F1 for more info.
""");
		info.AaAddElem(console, """
<b>console<> - let the program run with console.
""");
		info.AaAddElem(platform, $"""
<b>platform<> - CPU instruction set.

Default on this computer: {(osVersion.isArm64Process ? "arm64" : "x64")}.
Press F1 for more info.
""");
		
		info.AaAddElem(xmlDoc, """
<b>xmlDoc<> - create XML documentation file from <.c>/// comments<>. And print errors in <.c>/// comments<>.
""");
		#endregion
		
		#region Add reference
		info.AaAddElem(addLibrary, """
<b>Library<> - add a .NET assembly reference.
Adds meta comment <c green>r DllFile<>.

Press F1 for more info.
""");
		info.AaAddElem(addNuget, """
<b>NuGet<> - use a NuGet package reference (see menu <b>Tools > NuGet</b>).
Adds meta comment <c green>nuget Folder\Package<>.

Press F1 for more info.
""");
		
		const string c_com = """
<b>COM<>, <b>...<> - convert (now) a COM component's type library to an <i>interop assembly<>, and use it.
Adds meta comment <c green>com FileName.dll<>.
Saves the assembly file in <link>%folders.Workspace%\.interop<>.

Press F1 for more info.
""";
		info.AaAddElem(addComRegistry, c_com);
		info.AaAddElem(addComBrowse, c_com);
		info.AaAddElem(addProject, """
<b>Project<> - add a reference to a class library created in this workspace.
Adds meta comment <c green>pr File.cs<>.

Press F1 for more info.
""");
		#endregion
		
		#region Add file
		info.AaAddElem(addClassFile, """
<b>Class file<> - add a C# code file that contains some classes/functions used by this file.
Adds meta comment <c green>c File.cs<>.

Press F1 for more info.
""");
		//FUTURE: add UI to append resource suffix
		info.AaAddElem(addResource, """
<b>Resource<> - add image etc file(s) as managed resources.
Adds meta comment <c green>resource File<>.

Press F1 for more info.
""");
		info.AaAddElem(addFile, """
<b>Other file<> - make a file available at run time. Eg an unmanaged dll.
Adds meta comment <c green>file File<>.

Press F1 for more info.
""");
		#endregion
	}
	
	#endregion
}
