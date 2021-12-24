using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Au.Controls;
using Au.Compiler;
using Microsoft.Win32;

class DProperties : KDialogWindow
{
	readonly FileNode _f;
	readonly MetaCommentsParser _meta;
	readonly bool _isClass;
	Au.Compiler.ERole _role;

	//controls
	readonly KSciInfoBox info;
	readonly ComboBox role, ifRunning, uac, warningLevel;
	readonly TextBox testScript, outputPath, icon, manifest, sign, define, noWarnings, testInternal, preBuild, postBuild, findInLists;
	readonly KCheckBox bit32, xmlDoc, console, optimize;
	readonly Expander gRun, gAssembly, gCompile;
	readonly Button addAssembly, addComRegistry, addComBrowse, addProject, addClassFile, addResource, outputPathB;

	public DProperties(FileNode f) {
		_f = f;
		_isClass = f.IsClass;

		Owner = App.Wmain;
		Title = "Properties of " + _f.Name;

		var b = new wpfBuilder(this).Columns(440.., 0);
		b.WinProperties(WindowStartupLocation.CenterOwner, showInTaskbar: false);
		b.R.Add(out info).Height(80).Margin("B8").Span(-1);
		b.R.StartStack(vertical: true); //left column
		b.StartGrid().Columns(0, -1, 20, 0, -1.15)
			.R.Add("role", out role).Skip()
			.Add("testScript", out testScript).Validation(o => _ValidateFile(o, "testScript", FNFind.CodeFile));
		b.End();

		b.StartStack(out gRun, "Run", vertical: true);
		b.StartGrid().Columns(0, 120, -1, 0, 80)
			.Add("ifRunning", out ifRunning).Skip()
			.Add("uac", out uac);
		b.End();
		b.End().Brush(Brushes.OldLace);

		b.StartGrid(out gCompile, "Compile").Columns(0, 50, 20, 0, -1);
		b.R.Add(out optimize, "optimize").Skip(2)
			.Add("define", out define);
		b.R.Add("warningLevel", out warningLevel).Editable().Skip()
			.Add("noWarnings", out noWarnings);
		b.R.Add("testInternal", out testInternal);
		b.R.StartGrid().Columns(0, -1, 20, 0, -1)
			.Add("preBuild", out preBuild).Skip().Validation(o => _ValidateFile(o, "preBuild", FNFind.CodeFile))
			.Add("postBuild", out postBuild).Validation(o => _ValidateFile(o, "postBuild", FNFind.CodeFile));
		b.End();
		b.End().Brush(Brushes.OldLace);

		b.StartStack(out gAssembly, "Assembly", vertical: true);
		b.StartGrid().Columns(0, -1, 30)
			.Add("outputPath", out outputPath)
			.AddButton(out outputPathB, "...", _ButtonClick_outputPath)
			.End();
		b.StartGrid().Columns(0, -1, 20, 0, -1);
		b.R.Add("icon", out icon).Skip().Validation(o => _ValidateFile(o, "icon", FNFind.Any))
			.Add("manifest", out manifest).Validation(o => _ValidateFile(o, "manifest", FNFind.File));
		b.R.Add("sign", out sign).Skip().Validation(o => _ValidateFile(o, "sign", FNFind.File));
		b.StartStack()
			.Add(out console, "console")
			.Add(out bit32, "bit32").Margin(15)
			.Add(out xmlDoc, "xmlDoc").Margin(15)
			.End();
		b.End();
		b.End().Brush(Brushes.OldLace);

		b.End();
		b.StartStack(vertical: true).Margin("L20"); //right column
		b.StartGrid<GroupBox>("Add reference");
		b.R.AddButton(out addAssembly, "Assembly...", _ButtonClick_addNet);
		b.R.AddButton(out addComRegistry, "COM ▾", _bAddComRegistry_Click).AddButton(out addComBrowse, "...", _bAddComBrowse_Click).Width(30);
		b.AddButton(out addProject, "Project ▾", _ButtonClick_addProject);
		b.End();
		b.StartStack<GroupBox>("Add file", vertical: true);
		b.AddButton(out addClassFile, "Class file ▾", _ButtonClick_addClass);
		b.AddButton(out addResource, "Resource ▾", _ButtonClick_addResource);
		b.End();
		b.StartStack(vertical: true).Add("Find in lists", out findInLists).Tooltip("In button drop-down lists show only items containing this text").End();
		//b.AddButton("Change icon", _ => DIcons.ZShow(true, _f.CustomIconName)).Margin("T8B8"); //rejected
		b.End();
		b.R.AddOkCancel();
		b.End();

		_meta = new MetaCommentsParser(_f);

		_role = _meta.role switch {
			"miniProgram" => Au.Compiler.ERole.miniProgram,
			"exeProgram" => Au.Compiler.ERole.exeProgram,
			"editorExtension" => Au.Compiler.ERole.editorExtension,
			"classLibrary" when _isClass => Au.Compiler.ERole.classLibrary,
			"classFile" when _isClass => Au.Compiler.ERole.classFile,
			_ => _isClass ? Au.Compiler.ERole.classFile : Au.Compiler.ERole.miniProgram,
		};
		_InitCombo(role, _isClass ? "miniProgram|exeProgram|editorExtension|classLibrary|classFile" : "miniProgram|exeProgram|editorExtension", null, (int)_role);
		testScript.Text = _f.TestScript?.ItemPath;
		//Run
		_InitCombo(ifRunning, "warn_restart|warn|cancel_restart|cancel|wait_restart|wait|run_restart|run|restart", _meta.ifRunning);
		_InitCombo(uac, "inherit|user|admin", _meta.uac);
		//Assembly
		outputPath.Text = _meta.outputPath;
		void _ButtonClick_outputPath(WBButtonClickArgs e) {
			var m = new popupMenu();
			m[_GetOutputPath(getDefault: true)] = o => outputPath.Text = o.ToString();
			m["Browse..."] = o => {
				var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDD}") {
					InitFolderNow = _GetOutputPath(getDefault: false, expandEnvVar: true),
				};
				if (d.ShowOpen(out string s, this, selectFolder: true)) outputPath.Text = s;
			};
			m.Show();
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
		_InitCombo(warningLevel, "5|4|3|2|1|0", _meta.warningLevel);
		noWarnings.Text = _meta.noWarnings;
		testInternal.Text = _meta.testInternal;
		preBuild.Text = _meta.preBuild;
		postBuild.Text = _meta.postBuild;

		static void _InitCombo(ComboBox c, string items, string meta, int index = -1) {
			var a = items.Split('|');
			if (index < 0 && meta != null) index = Array.IndexOf(a, meta);
			foreach (var v in a) c.Items.Add(v);
			c.SelectedIndex = Math.Max(0, index);
		}

		gRun.IsExpanded = true;
#if true //rejected: initially collapsed expanders. Annoying.
		gAssembly.IsExpanded = true;
		gCompile.IsExpanded = true;
#else
		gAssembly.IsExpanded = _role is Au.Compiler.ERole.exeProgram or Au.Compiler.ERole.classLibrary;
#endif

		_ChangedRole();
		role.SelectionChanged += (_, _) => {
			_role = (Au.Compiler.ERole)role.SelectedIndex;
			_ChangedRole();
		};
		void _ChangedRole() {
			_ShowHide(testScript, _role is Au.Compiler.ERole.classLibrary or Au.Compiler.ERole.classFile);
			_ShowCollapse(_role is Au.Compiler.ERole.miniProgram or Au.Compiler.ERole.exeProgram, gRun, console, icon);
			_ShowCollapse(_role is Au.Compiler.ERole.exeProgram or Au.Compiler.ERole.classLibrary, outputPath, outputPathB, xmlDoc);
			_ShowCollapse(_role == Au.Compiler.ERole.exeProgram, manifest, bit32);
			_ShowCollapse(_role != Au.Compiler.ERole.classFile, gAssembly, gCompile);
			addProject.IsEnabled = _role != Au.Compiler.ERole.classFile;
		}

		string _ValidateFile(FrameworkElement e, string name, FNFind kind) {
			return (_Get(e as TextBox) is string s && null == _f.FindRelative(s, kind, orAnywhere: true)) ? name + " file not found" : null;
		}

		//b.Loaded += () => {
		//	
		//};

		b.OkApply += _OkApply;
		_InitInfo();
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
		if (_role != Au.Compiler.ERole.classFile) {
			if (_isClass || _role != Au.Compiler.ERole.miniProgram) _meta.role = _role.ToString();
			switch (_role) {
			case Au.Compiler.ERole.exeProgram:
			case Au.Compiler.ERole.classLibrary:
				_meta.outputPath = _GetOutputPath(getDefault: false);
				break;
			}
		}
	}

	private void _OkApply(WBButtonClickArgs e) {
		if (App.Model.CurrentFile != _f && !App.Model.SetCurrentFile(_f)) return;

		_GetMeta();

		var doc = Panels.Editor.ZActiveDoc;
		var code = doc.zText;
		var meta = MetaComments.FindMetaComments(code);
		string prepend = null, append = null;
		if (meta.end == 0) {
			if (code.RxMatch(@"(?s)^(\s*///\N*\R|\s*/\*\*.*?\*/\R)+", 0, out RXGroup g)) { //description
				meta = (g.End, g.End);
				prepend = "\r\n";
			}
			append = (_f.IsScript && code.Eq(meta.end, "//.")) ? " " : "\r\n";
		}
		var s = _meta.Format(prepend, append);

		if (s.Length == 0) {
			if (meta.end == 0) return;
			while (meta.end < code.Length && code[meta.end] <= ' ') meta.end++;
		} else if (s.Length == meta.end - meta.start) {
			if (s == doc.zRangeText(true, meta.start, meta.end)) return; //did not change
		}

		doc.zReplaceRange(true, meta.start, meta.end, s);
	}

	private void _ButtonClick_addNet(WBButtonClickArgs e) {
		var dir = folders.ThisApp + "Libraries"; if (!filesystem.exists(dir).isDir) dir = folders.ThisApp;
		var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDB}") {
			InitFolderNow = dir,
			FileTypes = "Dll|*.dll|All files|*.*"
		};
		if (!d.ShowOpen(out string[] a, this)) return;

		foreach (var v in a) {
			if (MetaReferences.IsNetAssembly(v)) continue;
			dialog.showError("Not a .NET assembly.", v, owner: this);
			return;
		}

		//remove path and ext if need
		var thisApp = folders.ThisAppBS;
		if (a[0].Starts(thisApp, true)) {
			for (int i = 0; i < a.Length; i++) a[i] = a[i][thisApp.Length..];
		}

		_meta.r.AddRange(a);
		_ShowInfo_Added(e.Button, _meta.r);
	}

	private void _ButtonClick_addProject(WBButtonClickArgs e)
		=> _AddFromWorkspace(
			f => (f != _f && f.GetClassFileRole() == FileNode.EClassFileRole.Library) ? f : null,
			_meta.pr, e.Button);

	private void _ButtonClick_addClass(WBButtonClickArgs e) {
		FileNode prFolder1 = null;
		if (_f.IsScript && _f.FindProject(out prFolder1, out var prMain1, ofAnyScript: true) && _f == prMain1) prFolder1 = null;

		bool _Include(FileNode f) {
			if (!f.IsClass || f == _f) return false;
			if (f.FindProject(out var prFolder, out var prMain) && !prFolder.Name.Starts("@@")) { //exclude class files that are in projects, except if project name starts with @@
				if (prFolder != prFolder1) return false; //but if _f is a non-project script in a project folder, include local classes
			}
			return f.GetClassFileRole() == FileNode.EClassFileRole.Class;
		}

		_AddFromWorkspace(f => _Include(f) ? f : null, _meta.c, e.Button);
	}

	private void _ButtonClick_addResource(WBButtonClickArgs e) {
		_AddFromWorkspace(
			f => {
				if (f.IsCodeFile) return null;
				if (f.IsFolder) { //add if contains non-code files and does not contain code files
					bool hasRes = false;
					foreach (var v in f.Descendants()) { if (v.IsCodeFile) return null; hasRes |= !v.IsFolder; }
					if (!hasRes) return null;
				}
				return f;
			},
			_meta.resource, e.Button);
	}

	void _AddFromWorkspace(Func<FileNode, FileNode> filter, List<string> metaList, Button button) {
		var sFind = findInLists.Text;
		var a = new List<string>();
		foreach (var f in App.Model.Root.Descendants()) {
			var f2 = filter(f);
			if (f2 == null) continue;
			var path = f2.ItemPath;
			if (sFind.Length > 0 && path.Find(sFind, true) < 0) continue;
			if (!metaList.Contains(path, StringComparer.OrdinalIgnoreCase)) a.Add(path);
		}
		if (a.Count == 0) { _ShowInfo_ListEmpty(button, sFind); return; }
		a.Sort();
		var p = new KPopupListBox { PlacementTarget = button };
		p.Control.ItemsSource = a;
		p.OK += o => {
			metaList.Add(o as string);
			_ShowInfo_Added(button, metaList);
		};
		p.IsOpen = true;
	}

	#region COM

	private void _bAddComBrowse_Click(WBButtonClickArgs e) {
		var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDC}") {
			FileTypes = "Type library|*.dll;*.tlb;*.olb;*.ocx;*.exe|All files|*.*"
		};
		if (d.ShowOpen(out string s, this)) _ConvertTypeLibrary(s, e.Button);
	}

	private void _bAddComRegistry_Click(WBButtonClickArgs e) {
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
						a.Add(new _RegTypelib { guid = sGuid, text = description + ", " + sVer, version = sVer });
					} //else print.it(sGuid); //some Microsoft typelibs. VS does not show these too.
				}
			}
		}
		if (a.Count == 0) { _ShowInfo_ListEmpty(e.Button, sFind); return; }
		a.Sort((x, y) => string.Compare(x.text, y.text, true));

		var p = new KPopupListBox { PlacementTarget = e.Button };
		p.Control.ItemsSource = a;
		p.OK += o => {
			_ConvertTypeLibrary(o as _RegTypelib, e.Button);
		};
		p.IsOpen = true;
	}

	//To convert a COM type library we use TypeLibConverter class. However .NET Core+ does not have it.
	//Workaround: the code is in Au.Net45.exe. It uses .NET Framework 4.8. We call it through run.console.
	//We don't use tlbimp.exe:
	//	1. If some used interop assemblies are in GAC (eg MS Office PIA), does not create files for them. But we cannot use GAC in a Core+ app.
	//	2. Does not tell what files created.
	//	3. My PC somehow has MS Office PIA installed and there is no uninstaller. After deleting the GAC files tlbimp.exe created all files, but it took several minutes.
	//Tested: impossible to convert .NET Framework TypeLibConverter code. Part of it is in extern methods.
	//Tested: cannot use .NET Framework dll for it. Fails at run time because uses Core+ assemblies, and they don't have the class. Need exe.

	class _RegTypelib
	{
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

	static string s_comConvertedDir;

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
			if (comDll == null || !filesystem.exists(comDll).isFile) {
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
				if (s_comConvertedDir == null) {
					s_comConvertedDir = folders.Workspace + @".interop\";
					filesystem.createDirectory(s_comConvertedDir);
				}
				void _Callback(string s) {
					print.it(s);
					if (s.Starts("Converted: ")) {
						s.RxMatch(@"""(.+?)"".$", 1, out s);
						converted.Add(s);
					}
				}
				rr = run.console(_Callback, folders.ThisAppBS + "Au.Net45.exe", $"/typelib \"{s_comConvertedDir}|{comDll}\"", encoding: Encoding.UTF8);
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

	static void _Show(FrameworkElement e, Visibility vis) {
		e.Visibility = vis;
		if (System.Windows.Automation.AutomationProperties.GetLabeledBy(e) is UIElement label) label.Visibility = vis;
	}

	static void _ShowHide(FrameworkElement e, bool show) => _Show(e, show ? Visibility.Visible : Visibility.Hidden);

	static void _ShowCollapse(FrameworkElement e, bool show) => _Show(e, show ? Visibility.Visible : Visibility.Collapsed);

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

	void _ShowInfo_ListEmpty(Button button, string sFind) {
		var s = "The list is empty";
		if (sFind.Length > 0) s = "The list contains 0 items containing " + sFind;
		_ShowInfoTooltip(button, s);
	}

	void _ShowInfo_Added(Button button, List<string> metaList) {
		_ShowInfoTooltip(button, string.Join("\r\n", metaList) + "\r\n\r\nFinally click OK to save.");
	}

	void _ShowInfoTooltip(Button button, string s) {
		Au.Tools.TUtil.InfoTooltip(ref _tt, button, s, Dock.Right);
	}
	KPopup _tt;

	#endregion

	#region info

	void _InitInfo() {
		info.zText = "This file is a C# <help editor/" + (_isClass ? @"Class files, projects>class file" : "Scripts>script") + @"<>.

C# file properties here are similar to C# project properties in Visual Studio.
Saved in <c green>/*/ meta comments /*/<> at the start of code, and can be edited there too.
";

		info.ZAddElem(role,
@"<b>role</b> - purpose of this C# code file. What type of assembly to create and how to execute.
 • <i>miniProgram</i> - execute in a separate host process started from editor.
 • <i>exeProgram</i> - create/execute .exe file. It can run on any computer, without editor installed.
 • <i>editorExtension</i> - execute in the editor's UI thread. Dangerous, unstoppable. Rarely used.
 • <i>classLibrary</i> - create .dll file. It can be used as a reference assembly anywhere.
 • <i>classFile</i> - don't create/execute. Compile together with other C# code files in the project or using meta comment c. Inherits meta comments of the main file of the compilation.

Default role for scripts is miniProgram; cannot be the last two. Default for class files is classFile.
");
		info.ZAddElem(testScript,
@"<b>testScript</b> - a script to run when you click the Run button.
Can be path relative to this file (examples: Script5.cs, Folder\Script5.cs, ..\Folder\Script5.cs) or path in the workspace (examples: \Script5.cs, \Folder\Script5.cs).

Usually it is used to test this class file or class library. It can contain meta comment <c green>c this file<> that adds this file to the compilation, or <c green>pr this file<> that adds the output dll file as a reference assembly. The recommended way to add this option correctly and easily is to try to run this file and click a link that is then printed in the output.

This option is saved in current workspace, not in meta comments.
");
		info.ZAddElem(ifRunning,
@"<b>ifRunning</b> - when trying to start this script, what to do if it is already running.
 • <i>warn</i> - write warning in output and don't run.
 • <i>cancel</i> - don't run.
 • <i>wait</i> - run later, when it ends.
 • <i>run</i> - run simultaneously.
 • <i>restart</i> - end it and run.

Suffix _restart means restart if starting the script with the Run button/menu.
Default is warn_restart.

This option is ignored when the task runs as .exe program started not from editor; instead use code: script.single(""unique string"");.
");
		info.ZAddElem(uac,
@"<b>uac</b> - <help articles/UAC>UAC<> integrity level (IL) of the task process.
 • <i>inherit</i> (default) - the same as of the editor process. Normally High IL if installed on admin account, else Medium IL.
 • <i>user</i> - Medium IL, like most applications. The task cannot automate high IL process windows, write some files, change some settings, etc.
 • <i>admin</i> - High IL, aka ""administrator"", ""elevated"". The task has many rights, but cannot automate some apps through COM, etc.

This option is ignored when the task runs as .exe program started not from editor.
");
		info.ZAddElem(outputPath,
@"<b>outputPath</b> - directory for the output assembly file and related files (used dlls, etc).
Full path. Can start with %environmentVariable% or %folders.SomeFolder%. Can be path relative to this file or workspace, like with other options. Default if role exeProgram: <link>%folders.Workspace%\bin\filename<>. Default if role classLibrary: <link>%folders.ThisApp%\Libraries<>. The compiler creates the folder if does not exist.

If role exeProgram, the exe file is named like the script. The 32-bit version has suffix ""-32"". If optimize true (checked in Properties), creates both 64-bit and 32-bit versions. Else creates only 32-bit if bit32 true (checked in Properties) or 32-bit OS, else only 64-bit.
If role classLibrary, the dll file is named like the class file. It can be used by 64-bit and 32-bit processes.
");
		info.ZAddElem(icon,
@"<b>icon</b> - icon of the output exe file.
The .ico file must be in this workspace. Can be path relative to this file (examples: App.ico, Folder\App.ico, ..\Folder\App.ico) or path in the workspace (examples: \App.ico, \Folder\App.ico).

The icon will be added as a native resource and displayed in File Explorer etc. If role exeProgram, can add all .ico and .xaml icons from folder. Resource ids start from IDI_APPLICATION (32512). Native resources can be used with icon.ofThisApp etc and dialog functions.

If not specified, uses custom icon of the main C# file. See menu Tools -> Icons.
");
		info.ZAddElem(manifest,
@"<b>manifest</b> - <google manifest file site:microsoft.com>manifest<> of the output exe or dll file.
The .manifest file must be in this workspace. Can be path relative to this file (examples: App.manifest, Folder\App.manifest, ..\Folder\App.manifest) or path in the workspace (examples: \App.manifest, \Folder\App.manifest).

The manifest will be added as a native resource.
");
		//		info.AddElem(resFile,
		//@"<b>resFile</b> - .res file containing resources to add to the output exe or dll file as native resources.
		//The .res file must be in this workspace. Can be path relative to this file or path in the workspace.

		//.res files contain compiled native resources of any type, including icons and manifest.
		//This option is rarely used. Instead you use managed resources (button ""Resource..."").
		//");
		info.ZAddElem(sign,
@"<b>sign</b> - strong-name signing key file, to sign the output assembly.
The file must be in this workspace. Can be path relative to this file (examples: App.snk, Folder\App.snk, ..\Folder\App.snk) or path in the workspace (examples: \App.snk, \Folder\App.snk).
");
		info.ZAddElem(console,
@"<b>console</b> - let the program run with console.
");
		info.ZAddElem(bit32,
@"<b>bit32</b> - whether the exe process must be 32-bit everywhere.
 • <i>false</i> (default) - the process is 64-bit or 32-bit, the same as Windows on that computer.
 • <i>true</i> (checked in Properties) - the process is 32-bit on all computers.
");
		info.ZAddElem(xmlDoc,
@"<b>xmlDoc</b> - create XML documentation file from /// XML comments of classes, functions, etc.
Creates in the 'outputPath' folder.

XML documentation files are used by code editors to display class/function/parameter info. Also can be used to create online documentation or help file, for example with <google>Sandcastle Help File Builder<> or <google>DocFX<>.
");
		info.ZAddElem(optimize,
@"<b>optimize</b> - whether to make the compiled code as fast as possible.
 • <i>false</i> (default) - don't optimize. Define DEBUG and TRACE. Aka ""Debug configuration"".
 • <i>true</i> (checked in Properties) - optimize. Aka ""Release configuration"".

Default is false, because optimization makes difficult to debug. It makes noticeably faster only some types of code, for example processing of text and byte arrays. Before deploying class libraries and exe programs always compile with optimize true.

This option is also applied to class files compiled together, eg as part of project. Use true (checked in Properties) if they contain code that must be as fast as possible.
");
		info.ZAddElem(define,
@"<b>define</b> - symbols that can be used with #if.
Example: ONE,TWO,d:THREE,r:FOUR
Can be used prefix r: or d: to define the symbol only if optimize true (checked in Properties) or false (unchecked).
If no optimize true, DEBUG and TRACE are added implicitly.
These symbols also are visible in class files compiled together, eg as part of project.
See also <google C# #define>#define<>.
");
		info.ZAddElem(warningLevel,
@"<b>warningLevel</b> - how many warnings to show.
0 - no warnings.
1 - only severe warnings.
2 - level 1 warnings plus certain, less-severe warnings.
3 - most warnings.
4 - all warnings except warnings added in C# 9+.
5 (default) - all warnings.
6 to 9999 - warnings added in future C# versions.

This option is also applied to class files compiled together, eg as part of project.
");
		info.ZAddElem(noWarnings,
@"<b>noWarnings</b> - don't show these warnings.
Example: 151,3001,120

This option is also applied to class files compiled together, eg as part of project.
See also <google C# #pragma warning>#pragma warning<>.
");
		info.ZAddElem(testInternal,
@"<b>testInternal</b> - access internal symbols of these assemblies, like with InternalsVisibleToAttribute.
Example: Assembly1,Assembly2

This option is also applied to class files compiled together, eg as part of project.
");
		info.ZAddElem(preBuild,
@"<b>preBuild</b> - a script to run before compiling this code file.
Can be path relative to this file (examples: Script5.cs, Folder\Script5.cs, ..\Folder\Script5.cs) or path in the workspace (examples: \Script5.cs, \Folder\Script5.cs).

The script must have role editorExtension. It runs synchronously in compiler's thread. To stop compilation, let it throw an exception.
By default it receives full path of the output exe or dll file in args[0]. If need more info, specify command line arguments, like in this example: Script5.cs /$(outputPath) $(optimize). The script will receive real values in args[0], args[1] and so on. Variables:
 • $(outputFile) - full path of the output exe or dll file.
 • $(outputPath) - meta comment 'outputPath'.
 • $(source) - path of this C# code file in the workspace.
 • $(role) - meta comment 'role'.
 • $(optimize) - meta comment 'optimize'.
 • $(bit32) - meta comment 'bit32'.
");
		info.ZAddElem(postBuild,
@"<b>postBuild</b> - a script to run after compiling this code file successfully.
Everything else is like with preBuild.
");
		info.ZAddElem(addAssembly,
@"<b>Assembly<> - add one or more .NET assemblies (.dll files) as references.
Adds meta comment <c green>r FileName<>.

Don't need to add Au.dll and .NET runtime dlls.
To use 'extern alias', edit in the code editor like this: <c green>r Alias=Assembly<>
To remove this meta comment, edit the code.

If the file is in <link>%folders.ThisApp%<> or its subfolders, use file name or relative path, else need full path. If role of the script is not miniProgram, at run time the file must be directly in folders.ThisApp or folders.ThisApp\Libraries. If role is editorExtension, may need to restart editor.
");
		const string c_com = @" COM component's type library to an <i>interop assembly<>.
Adds meta comment <c green>com FileName.dll<>. Saves the assembly file in <link>%folders.Workspace%\.interop<>.

An interop assembly is a .NET assembly without real code. Not used at run time. At run time is used the COM component (registered native dll or exe file). Check bit32 if 64-bit dll unavailable.

To remove this meta comment, edit the code. Optionally delete unused interop assemblies.
";
		info.ZAddElem(addComRegistry, @"<b>COM<> - convert a registered" + c_com);
		info.ZAddElem(addComBrowse, @"<b>...<> - convert a" + c_com);
		info.ZAddElem(addProject,
@"<b>Project<> - add a reference to a class library created in this workspace.
Adds meta comment <c green>pr File.cs<>. The compiler will compile it if need and use the created dll file as a reference.

The recommended outputPath of the library project is <link>%folders.ThisApp%\Libraries<>. Else may not find the dll at run time.

To remove this meta comment, edit the code. Optionally delete unused dll files.
");
		info.ZAddElem(addClassFile,
@"<b>Class file<> - add a C# code file that contains some classes/functions used by this file.
Adds meta comment <c green>c File.cs<>. The compiler will compile all code files and create single assembly.

If this file is in a project, don't add class files that are in the project folder.
Can be added only files that are in this workspace. Import files if need, for example drag-drop.
Can be path relative to this file (examples: Class5.cs, Folder\Class5.cs, ..\Folder\Class5.cs) or path in the workspace (examples: \Class5.cs, \Folder\Class5.cs).
If folder, adds all its descendant class files.
To remove this meta comment, edit the code.
");
		info.ZAddElem(addResource,
@"<b>Resource<> - add image etc file(s) as managed resources.
Adds meta comment <c green>resource File<>. If folder, the compiler will add all files in it and subfolders.

Default resource type is stream. In editor you can append space and <c green>/byte[]<>, <c green>/string<>, <c green>/strings<> or <c green>/embedded<>. Example: <c green>resource file.txt /string<>.
/strings - CSV text file containing multiple strings as 2-column CSV (name, value).
/embedded - separate stream. All others are in stream AssemblyName.g.resources.

Can be added only files that are in this workspace. Import files if need, for example drag/drop.
Can be path relative to this file (examples: File.png, Folder\File.png, ..\Folder\File.png) or path in the workspace (examples: \File.png, \Folder\File.png).
To remove this meta comment, edit the code.

To load resources directly, use <help>Au.More.ResourceUtil<>, like <code>var s = Au.More.ResourceUtil.GetString(""file.txt"");</code>. Or <google>ResourceManager<>. To load WPF resources can be used ""pack:..."" URI.
To load embedded resources, use <google>Assembly.GetManifestResourceStream<>.
Compiled names of non-embedded resource files are lowercase, like ""file.png"" or ""subfolder/file.png"".
To browse .NET assembly resources, types, etc can be used for example <google>ILSpy<>.
");

		//		info.AddElem(,
		//");
	}

	#endregion
}
