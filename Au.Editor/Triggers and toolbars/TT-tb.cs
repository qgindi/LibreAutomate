extern alias CAW;

using System.Windows;
using System.Windows.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using CAW::Microsoft.CodeAnalysis.FindSymbols;

using Au.Triggers;
using Au.Tools;
using Au.Compiler;
using Au.Controls;

#if SCRIPT
namespace Script;
#else
#endif

partial class TriggersAndToolbars {
	_Toolbar[] _toolbars;
	MetaComments _meta;
	Solution _sln;
	Compilation _compilation;
	INamedTypeSymbol _programSym;
	Dictionary<FileNode, SyntaxTree> _fnToSt;
	Dictionary<SyntaxTree, FileNode> _fnFromSt;

	TriggersAndToolbars() {
		_Update();
	}

	void _NewToolbar() {
		var w = new KDialogWindow { Title = "New toolbar" };
		var b = new wpfBuilder(w).WinSize(450).Columns(50, -1);
		b.WinProperties(resizeMode: ResizeMode.NoResize, showInTaskbar: false);

		b.R.Add("Name", out TextBox tName, "Toolbar_").Focus()
			.Validation(_ => tName.Text is "" or "Toolbar_" ? "No name" : !SyntaxFacts.IsValidIdentifier(tName.Text) ? "Invalid function name" : null);

		b.R.Add("In file", out ComboBox cbFile);
		cbFile.ShouldPreserveUserEnteredPrefix = true;
		cbFile.Items.Add("<new file>");
		foreach (var st in _EnumToolbarTriggersFunctions().Select(o => o.Locations[0].SourceTree).Distinct()) {
			cbFile.Items.Add(_fnFromSt[st]);
		}
		cbFile.SelectedIndex = 0;

		b.R.Add("Code", out ComboBox cbTrigger).Items("Window trigger, attach to window|Window trigger, attach, auto-hide at screen edge|Show at startup, auto-hide at screen edge|Mouse trigger, auto-hide at screen edge|No trigger");
		b.R.Skip().Add(out KCheckBox cLaterName, "Show/hide whenever window name changes");
		b.R.StartGrid().Columns(50, -1, 20, 0, -1).Hidden(null)
			.R.Add("Edge", out ComboBox cbEdge).Items(typeof(TMEdge).GetEnumNames()).Select(1)
			.Skip();
		var cbScreen = _ScreenComboBoxInit(b);
		b.End();
		var pScreen = b.Last;
		cbTrigger.SelectionChanged += (_, _) => {
			int si = cbTrigger.SelectedIndex;
			cLaterName.Visibility = si is 0 or 1 ? Visibility.Visible : Visibility.Collapsed;
			pScreen.Visibility = si is 1 or 2 or 3 ? Visibility.Visible : Visibility.Collapsed;
		};

		b.R.AddOkCancel();
		b.End();

		b.Loaded += () => { tName.CaretIndex = tName.Text.Length; };

		if (!b.ShowDialog(App.Wmain)) return;

		string sName = _GetUniqueNameInProgram(tName.Text);

		int iTrigger = cbTrigger.SelectedIndex;
		string sAutoHide = null;
		if (iTrigger is 0 or 4) { //window, none
			sAutoHide = """
		
		////auto-hide. Above is the auto-hide part. Below is the always-visible part.
		//t = t.AutoHide();
		//if (t.FirstTime) {
			
		//}
		
""";
		} else if (iTrigger is 1 or 2) { //window+screen or screen
			string sScreen = _ScreenComboBoxResult(cbScreen, false) ?? "default";
			sAutoHide = $$"""
		
		//auto-hide at the specified screen edge. Above is the auto-hide part. Below is the always-visible part.
		t = t.AutoHideScreenEdge(TMEdge.{{cbEdge.SelectedItem}}, {{sScreen}}, 5, ^5, 2);
		t.BorderColor = System.Drawing.Color.Orange;
		//if (t.FirstTime) {
			
		//}
		
""";
		} else if (iTrigger == 3) { //screen+mouse
			sAutoHide = """
		
		//auto-hide at the screen edge of the mouse trigger. Above is the auto-hide part. Below is the always-visible part.
		t = t.AutoHideScreenEdge(ta, 5, ^5, 2);
		t.BorderColor = System.Drawing.Color.Orange;
		//if (t.FirstTime) {
			
		//}
		
""";
		}

		var sArg = iTrigger switch {
			0 or 1 => "WindowTriggerArgs ta",
			3 => "MouseTriggerArgs ta",
			_ => "TriggerArgs ta = null"
		};

		bool laterName = iTrigger is 0 or 1 && cLaterName.IsChecked;
		string sRetType = laterName ? "toolbar" : "void", sRetCode = laterName ? "\r\n\t\t\r\n\t\treturn t;" : null;

		var text = $$"""

	{{sRetType}} {{sName}}({{sArg}}) {
		var t = new toolbar();
		if (t.FirstTime) {
			//t.DisplayText = !true; //display only icon or 2 characters, unless button text is like "Text\a"
			
		}
		
		//Add buttons here, like in examples. Hints: toolbarButtonSnippet, drag-drop, Ctrl+Shift+Q.
		//More info: 1. Cookbook > Floating toolbars. 2. toolbar class help (click word toolbar above and press F1).
		
		t["Example"] = o => { print.it("button clicked"); };
		t["|Tooltip1", image: "*Modern.TreeLeaf #73BF00"] = o => {  }; //to set icon use the Icons dialog
		t["Text\a"] = o => {  }; //this button always displays text. The above button never.
		t.Menu("Menu1", t => {
			t["A"] = o => {  };
			t["B|Tooltip3"] = o => {  };
		});
		t.Separator();
		t["Text2\a|Tooltip2"] = o => {  };
{{sAutoHide}}
		t.Show(ta);
		
		////this code is the same as t.Show(ta), but you can specify more Show parameters, for example attach to a control
		//if (ta is WindowTriggerArgs wta) {
		//	t.Show(wta.Window); //attach to the trigger window
		//} else {
		//	t.Show();
		//	ta?.DisableTriggerUntilClosed(t); //single instance
		//}{{sRetCode}}
	}

""";

		if (cbFile.SelectedItem is not FileNode f) { //new file
			text = $$"""
using Au.Triggers;

partial class Program {
	[Toolbars]
	void {{_GetUniqueNameInProgram(sName + "_Triggers")}}() {
	}

"""
+ text
+ @"}
";
			var folder = App.Model.Find(@"\@Triggers and toolbars\Toolbars", FNFind.Folder);
			f = App.Model.NewItem("Class.cs", new(folder, FNInsert.Last), sName + ".cs", text: new(true, text));
		} else {
			var programNode = _ProgramClassNodeFromST(_fnToSt[f]); if (programNode == null) return;
			_OpenSourceFile(f).aaaInsertText(true, programNode.CloseBraceToken.SpanStart, text);
		}

		_Update();
		var t = _toolbars[Array.FindIndex(_toolbars, o => o.Name == sName)];

		//trigger
		if (iTrigger != 4) {
			wait.doEvents(30); //workaround for bad scrolling (mouse/screen) etc
			if (iTrigger is 0 or 1) { //window
				_AddTriggerWindow(t);
			} else if (iTrigger == 2) { //startup
				_AddTriggerStartup(t);
			} else { //mouse
				_AddTriggerMouse(t, cbEdge.SelectedItem as string, cbScreen);
			}
			_Update();
			if (!_StillExists(ref t)) return;
		}

		//go to the toolbar function
		int i = t.location.SourceSpan.Start;
		timer.after(iTrigger != 4 ? 500 : 10, _ => { //workaround for bad scrolling. Also briefly shows the trigger.
			_OpenSourceFile(f)?.aaaGoToPos(true, i);
		});

		//maybe a settings file exists with this name, probably orphaned
		var jsFolder = folders.Workspace + ".toolbars";
		var jsPath = jsFolder + "\\" + sName + ".json";
		if (filesystem.exists(jsPath)) {
			//rejected: show a dialog box.
			//CONSIDER: for new toolbar names use name+GUID.
			if (true == filesystem.delete(jsPath, FDFlags.RecycleBin | FDFlags.CanFail))
				print.it($"<>Note: A toolbar settings file with this name ({sName}) has been found, and moved to the Recycle Bin to avoid confusion.\r\n\tInfo: Each toolbar has a settings file with the same name, saved <link {jsFolder}>here<>. The program does not delete settings files of deleted or renamed toolbars. You can delete unused files. Deleting a used file resets the position, size and context menu settings of that toolbar.");
		}
	}

	void _SetToolbarTrigger(_Toolbar t, _Trigger tr) {
		var w = new KDialogWindow { Title = "New trigger for " + t.Name };
		var b = new wpfBuilder(w).WinSize(450);
		b.WinProperties(resizeMode: ResizeMode.NoResize, showInTaskbar: false);

		ComboBox cbReplace = null, cbEdge = null, cbScreen = null;

		if (t.triggers.Length > 0) {
			b.R.Add("Replace trigger", out cbReplace);
			cbReplace.Items.Add("Don't replace");
			foreach (var v in t.triggers) { int it = cbReplace.Items.Add(v); if (v == tr) cbReplace.SelectedIndex = it; }
			b.Validation(o => cbReplace.SelectedIndex < 0 ? "Empty 'Replace trigger'" : null);
		}

		int iTrigger = -1; //0 window, 1 startup, 2 mouse
		if (t.method.Parameters.Length > 0) {
			var pt = t.method.Parameters[0].Type;
			if (pt == _compilation.GetTypeByMetadataName("Au.Triggers.WindowTriggerArgs")) iTrigger = 0;
			else if (pt == _compilation.GetTypeByMetadataName("Au.Triggers.MouseTriggerArgs")) iTrigger = 2;
		}

		b.R.Add("Trigger type", out ComboBox cbTrigger).Disabled(iTrigger >= 0)
			.Items(iTrigger switch { 0 => "Window", 2 => "Mouse", _ => "Window|Show at startup" });
		if (iTrigger == 2) {
			b.R.Add("Screen edge", out cbEdge).Items(typeof(TMEdge).GetEnumNames()).Select(1)
				.And(170).StartGrid();
			cbScreen = _ScreenComboBoxInit(b);
			b.End();
		}
		//b.R.Add(out KCheckBox cRestart, "Restart TT script").Checked(); //rejected

		b.R.AddOkCancel();
		b.End();

		if (!b.ShowDialog(App.Wmain)) return;
		if (!_StillExists(ref t)) return;

		int pos = -1;
		if (cbReplace?.SelectedItem is _Trigger u && _GetTriggerStatementFullRange2(u, out var span, replacing: true)) {
			var doc = _OpenSourceFile(t.fn, span.Start);
			using var undo = doc.aaaNewUndoAction();
			doc.aaaDeleteRange(true, span.Start, span.End);
			pos = span.Start;
			_Add();
		} else {
			_Add();
		}
		_Update();

		void _Add() {
			if (iTrigger < 0) iTrigger = cbTrigger.SelectedIndex;
			if (iTrigger == 0) {
				_AddTriggerWindow(t, pos);
			} else if (iTrigger == 1) {
				_AddTriggerStartup(t, pos);
			} else if (iTrigger == 2) {
				_AddTriggerMouse(t, cbEdge.SelectedItem as string, cbScreen, pos);
			}
		}
	}

	void _SetToolbarTrigger() {
		var (t, tr) = _ToolbarFromCurrentPos();
		if (t != null) _SetToolbarTrigger(t, tr);
	}

	(_Toolbar tb, _Trigger tr) _ToolbarFromCurrentPos() {
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return default;
		int pos = doc.aaaCurrentPos16;
		var f = doc.EFile;
		//is pos in a toolbar function?
		var t = _toolbars.FirstOrDefault(o => o.fn == f && o.method.DeclaringSyntaxReferences[0].Span.ContainsOrTouches(pos));
		if (t != null) return (t, null);
		//is pos in a name of a toolbar function?
		foreach (var tb in _toolbars) {
			foreach (var tr in tb.triggers) if (tr.fn == f && tr.location.SourceSpan.ContainsOrTouches(pos)) return (tb, tr);
		}
		//get the first toolbar in the file
		return (_toolbars.FirstOrDefault(o => o.fn == f), null);
	}

	//void _EditToolbar(_Toolbar t) {
	//	_OpenSourceFile(t, t.location.SourceSpan);
	//}

	//rejected. It's better if the user reviews that code and deletes manually.
	//void _DeleteToolbar(_Toolbar t, bool commentOut) {}

	void _AddTriggerWindow(_Toolbar t, int pos = -1) {
		var d = new Dwnd(default, DwndFlags.ForTrigger, "Window trigger");
		if (!d.ShowAndWait()) return;
		string sTrigger = d.AaResultCode, sAction = t.Name, sSep = " ";
		if (!t.method.ReturnsVoid) {
			sTrigger += ", later: TWLater.Name";
			sAction = $"ta => ta.ShowToolbarWhenWindowName({sAction}, \"*window_name_when_the_toolbar_is_visible*\")";
			sSep = "\r\n\t";
			print.it("""
Please edit window name strings in the toolbar trigger code.
	The first should contain only the non-changing part, for example "*Program".
	The second - when the toolbar must be visible, for example "Document -*"
""");
		}
		_AddTrigger(t, $"Triggers.Window[TWEvent.ActiveOnce, {sTrigger}] ={sSep}{sAction};", pos);
	}

	void _AddTriggerStartup(_Toolbar t, int pos = -1) {
		_AddTrigger(t, $"{t.Name}();", pos);
	}

	void _AddTriggerMouse(_Toolbar t, string edge, ComboBox cbScreen, int pos = -1) {
		string s = _ScreenComboBoxResult(cbScreen, true);
		if (s != null) s = ", screen: " + s;
		_AddTrigger(t, $"Triggers.Mouse[TMEdge.{edge}{s}] = {t.Name};", pos);
	}

	void _AddTrigger(_Toolbar t, string s, int pos) {
		if (pos < 0) pos = _FindToolbarTriggersFunction(t).node.Body.CloseBraceToken.SpanStart;
		if (null == _OpenSourceFile(t.fn, pos)) return;
		InsertCode.Statements(s, ICSFlags.SelectNewCode);
	}

	static void _EditTrigger(_Trigger t) {
		_OpenSourceFile(t.fn, t.location.SourceSpan);
	}

	//bool _DeleteTrigger(_Trigger t/*, bool commentOut = false*/) {
	//	if (!_GetTriggerStatementFullRange2(t, out var span, replacing: false)) return false;
	//	var doc = _OpenSourceFile(t.fn, span.Start);
	//	//doc.aaaSelect(true, span.Start, span.End, true); return -1;
	//	//if (commentOut) {
	//	//	doc.aaaSelect(true, span.Start, span.End, true);
	//	//	ModifyCode.CommentLines(true);
	//	//} else {
	//	doc.aaaDeleteRange(true, span.Start, span.End);
	//	//}
	//	_Update();
	//	return true;
	//}

	ComboBox _ScreenComboBoxInit(wpfBuilder b) {
		b.Add("Screen", out ComboBox cb);
		cb.Items.Add("Primary");
		cb.SelectedIndex = 0;

		var a = screen.all;
		if (a.Length > 1) {
			//add functions of screen.at
			foreach (var v in typeof(screen.at).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)) {
				cb.Items.Add("screen.at." + v.Name);
			}

			//if defined type "screens", add its public static properties that return screen
			//	This code works, but probably this feature would be rarely used. Now undocumented.
			//if (_compilation.GetSymbolsWithName("screens", SymbolFilter.Type).FirstOrDefault() is INamedTypeSymbol screens) {
			//	foreach (var v in screens.GetMembers()) {
			//		if (v is not IPropertySymbol p || !v.IsStatic || v.DeclaredAccessibility is not Microsoft.CodeAnalysis.Accessibility.Public) continue;
			//		if (p.Type.ToString() != "Au.screen") continue;
			//		cb.Items.Add("screens." + v.Name);
			//	}
			//}

			//if (a.Length == 2) cb.Items.Add("screen.index(1)"); //no. More screens may be added in the future, and indices may change then.
		}

		return cb;
	}

	static string _ScreenComboBoxResult(ComboBox cbScreen, bool trigger) {
		int iScreen = cbScreen.SelectedIndex;
		if (iScreen == 0) return null;
		var s = cbScreen.Items[iScreen] as string;
		//if (s.Starts("screens.")) return s;
		//if (s.Starts("screen.at."))
			return s + (trigger ? "(true)" : "()");
		//return trigger ? s.Insert(^1, ", lazy: true") : s;
	}

	static bool _GetTriggerStatementFullRange2(_Trigger t, out TextSpan span, bool replacing) {
		if (_GetTriggerStatementFullRange(t, out span, replacing)) return true;
		print.it("This trigger should be deleted manually: " + t.text + "\r\n\tIt depends on other code which should be edited, deleted or reviewed.");
		if (!replacing) _EditTrigger(t);
		return false;
	}

	static bool _GetTriggerStatementFullRange(_Trigger t, out TextSpan span, bool replacing) {
		span = default;
		var node = t.location.FindNode(default);
	g1:
		var ss = node.GetAncestor<StatementSyntax>();
		if (ss == null) return false;
		//print.clear(); CiUtil.PrintNode(ss); CiUtil.PrintNode(ss.Parent);
		var pa = ss.Parent;
		if (pa is not BlockSyntax bs) return false;
		if (t.isTrigger && bs.Parent is SimpleLambdaExpressionSyntax) {
			if (bs.Statements.Count == 1 && bs.ToString().RxIsMatch(@"^\{\s*\w.+;\s*\}$")) { //lambda { single statement }
				node = bs; goto g1;
			}
			if (replacing) return false;
		}
		var from = ss.SpanStart;
		if (ss.HasLeadingTrivia) {
			var u = ss.GetLeadingTrivia()[^1];
			if (u.Kind() == SyntaxKind.WhitespaceTrivia) from = u.SpanStart;
		}
		span = TextSpan.FromBounds(from, ss.FullSpan.End);
		return true;
	}

	/// <summary>
	/// Finds the toolbar in current _toolbars.
	/// If _toolbars changed and does not contain t, tries to find in the new _toolbars and updates t; returns false if not found (the toolbar code has been deleted).
	/// </summary>
	bool _StillExists(ref _Toolbar t) {
		var tt = t;
		t = _toolbars.FirstOrDefault(o => o.EqualsMethodQName(tt));
		return t != null && !t.fn.IsDeleted;
	}

	static SciCode _OpenSourceFile(FileNode f, int pos = -1) {
		if (App.Model.OpenAndGoTo(f, columnOrPos: pos)) return Panels.Editor.ActiveDoc;
		return null;
	}

	static SciCode _OpenSourceFile(FileNode f, TextSpan span) {
		if (!App.Model.OpenAndGoTo(f)) return null;
		var doc = Panels.Editor.ActiveDoc;
		doc.aaaSelect(true, span.End, span.Start, true);
		return doc;
	}

	IEnumerable<IMethodSymbol> _EnumToolbarTriggersFunctions() {
		var at = _programSym.GetTypeMembers("ToolbarsAttribute")[0];
		foreach (var m in _programSym.GetMembers().OfType<IMethodSymbol>()) {
			foreach (var a in m.GetAttributes()) if (a.AttributeClass == at) yield return m;
		}
	}

	ClassDeclarationSyntax _ProgramClassNodeFromST(SyntaxTree tree)
		=> _programSym.Locations.FirstOrDefault(o => o.SourceTree == tree)?.FindNode(default) as ClassDeclarationSyntax;

	string _GetUniqueNameInProgram(string name) {
		if (_programSym.MemberNames.Contains(name)) {
			for (int i = 2; ; i++) {
				var n = name + i;
				if (!_programSym.MemberNames.Contains(n)) return n;
			}
		}
		return name;
	}

	(IMethodSymbol sym, MethodDeclarationSyntax node) _FindToolbarTriggersFunction(_Toolbar t) {
		bool retry = false;
	g1:
		foreach (var m in _EnumToolbarTriggersFunctions()) {
			var loc = m.Locations[0];
			if (loc.SourceTree == t.tree) return (m, loc.FindNode(default) as MethodDeclarationSyntax);
		}
		if (retry) return default; retry = true;

		//create the function
		string name = _GetUniqueNameInProgram("Toolbars");
		var programNode = _ProgramClassNodeFromST(t.tree);
		_OpenSourceFile(t.fn, programNode.OpenBraceToken.FullSpan.End);
		var s = $$"""

[Toolbars]
void {{name}}() {
	
}


""";
		InsertCode.TextSimply(s);
		_Update();
		t = _toolbars.First(o => o.EqualsMethodQName(t));
		goto g1;
	}

	void _Update() {
		var a = new List<_Toolbar>();
		var at = new List<_Trigger>();
		var proj = global::TriggersAndToolbars.GetProject(create: true);
		using var ws = new AdhocWorkspace();
		(_sln, _meta) = CiUtil.CreateSolutionFromFileNode(ws, proj);
		_compilation = _sln.Projects.First().GetCompilationAsync().Result;
		var ttoolbar = _compilation.GetTypeByMetadataName("Au." + nameof(toolbar));

		_fnToSt = new();
		_fnFromSt = new();
		int iTree = 0;
		foreach (var tree in _compilation.SyntaxTrees) {
			Debug.Assert(tree.FilePath == _meta.CodeFiles[iTree].f.ItemPath);
			var f = _meta.CodeFiles[iTree++].f;
			_fnToSt[f] = tree;
			_fnFromSt[tree] = f;

			var semo = _compilation.GetSemanticModel(tree);
			var cu = semo.SyntaxTree.GetCompilationUnitRoot();
			var k = semo.GetExistingSymbols(cu, default);
			IMethodSymbol mPrev = null;
			foreach (var v in k) {
				if (v is ILocalSymbol loc && loc.Type == ttoolbar) {
					if (loc.DeclaringSyntaxReferences[0].GetSyntax() is VariableDeclaratorSyntax vd && vd.Initializer is EqualsValueClauseSyntax evc && evc.Value is ObjectCreationExpressionSyntax or InvocationExpressionSyntax) {
						var m = loc.ContainingSymbol as IMethodSymbol;
						if (m == mPrev) continue; mPrev = m; //get single toolbar in function
						var t = new _Toolbar { method = m, location = m.Locations[0], Name = m.Name, fn = f, tree = tree, variable = loc, };

						at.Clear();
						foreach (var x in SymbolFinder.FindCallersAsync(m, _sln).Result) {
							foreach (var y in x.Locations) {
								var node = y.FindNode(default);
								string tt = "?";
								bool isTrigger = false;
								if (node.GetAncestor<AssignmentExpressionSyntax>()?.Left is ElementAccessExpressionSyntax ea) {
									var semo2 = y.SourceTree == tree ? semo : _compilation.GetSemanticModel(y.SourceTree);
									var ty = semo2.GetTypeInfo(ea.Expression).Type;
									if (isTrigger = ty.ContainingNamespace.ToString() == "Au.Triggers") {
										tt = ea.ArgumentList.ToString();
										if (ty.Name == nameof(WindowTriggers)) tt = tt.RxReplace(@"^\[\s*TWEvent\.\w+\s*,\s*", "[");
										else tt = ty.Name[..^8] + tt;
									}
								}
								if (!isTrigger) {
									foreach (var p in node.Ancestors()) {
										if (p is LocalFunctionStatementSyntax lf) { tt = /*"Called from " +*/ lf.Identifier.Text; break; }
										if (p is MethodDeclarationSyntax met) { tt = /*"Called from " +*/ met.Identifier.Text; break; }
										if (p is MemberDeclarationSyntax mem && p is not BaseTypeDeclarationSyntax) { tt = mem.ToString(); break; } //field, property, method
									}
								}
								tt = tt.Replace("\t", "").RxReplace(@"\R", " ");
								at.Add(new(_fnFromSt[y.SourceTree], y, tt, isTrigger));
							}
						}
						t.triggers = at.ToArray();
						if (at.Any()) t.TriggerText = string.Join('\n', at.Select(o => o.text));

						a.Add(t);
					}
				}
			}
		}

		_toolbars = a.ToArray();
		_programSym = _compilation.GlobalNamespace.GetTypeMembers("Program")[0];
	}

	class _Toolbar {
		public FileNode fn;
		public SyntaxTree tree;
		public IMethodSymbol method;
		public Location location;
		public ILocalSymbol variable; //currently not used. In the future may be used for adding toolbar buttons.
		public _Trigger[] triggers;

		public string Name { get; set; }
		public string TriggerText { get; set; }

		/// <summary>Equals method qualified name.</summary>
		public bool EqualsMethodQName(_Toolbar t) => Name == t.Name && method.ToString() == t.method.ToString();

		/// <summary>Equals SourceTree and SourceSpan.</summary>
		public bool EqualsMethodLocation(Location loc) => loc.SourceTree.FilePath == location.SourceTree.FilePath && loc.SourceSpan.Start == location.SourceSpan.Start;
	}

	record _Trigger(FileNode fn, Location location, string text, bool isTrigger) {
		public override string ToString() => text;
	}
}
