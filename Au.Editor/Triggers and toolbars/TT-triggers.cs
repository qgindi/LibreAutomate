using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Au.Tools;
using Au.Triggers;

partial class TriggersAndToolbars {
	public enum TriggersType { None, Hotkey, Autotext, Mouse, Window }
	
	/// <summary>
	/// Tool dialog "New trigger".
	/// </summary>
	/// <param name="selectTriggersType">If not 0, selects trigger type.</param>
	/// <param name="windowTriggerWnd">Window for window trigger.</param>
	public static void NewTrigger(TriggersType selectTriggersType = 0, wnd windowTriggerWnd = default) {
		var owner = selectTriggersType > 0 ? null : App.Wmain;
		var w = new KDialogWindow { Title = "New trigger", ShowInTaskbar = owner == null, Topmost = owner == null };
		var b = new wpfBuilder(w).WinSize(500, 400);
		b.Options(bindLabelVisibility: true);
		
		UserControl[][] pages = [new UserControl[2], new UserControl[2], new UserControl[2], new UserControl[3]];
		int iPage = 0;
		Button bBack = null, bNext = null;
		_HotkeyTriggerPage hotkeyTriggerPage = null;
		_AutotextTriggerPage autotextTriggerPage = null;
		_MouseTriggerPage mouseTriggerPage = null;
		_WindowTriggerSettingsPage windowTriggerSettingsPage = null;
		DPwnd windowTriggerWindowPage = null;
		
		b.Row(-1).Add(out ContentControl ccPages).Margin("0");
		b.Add(out UserControl firstPage, WBAdd.ChildOfLast).StartGrid(childOfLast: true);
		for (int i = 0; i < 4; i++) pages[i][0] = firstPage;
		
		TriggersType tType = selectTriggersType != 0 ? selectTriggersType : _CodeAnalysis.GetTriggersType();
		var fn = App.Model.CurrentFile;
		bool canRunThisScript = _IsExecutableCodeFile(fn) && !fn.ItemPath.Eqi(@"\@Triggers and toolbars\Triggers and toolbars.cs");
		
		b.R.Add("Trigger", out ToolBar tb).Margin("LRT").Brush(SystemColors.ControlBrush);
		tb.HideGripAndOverflow();
		string[] aTriggersTypeStrings = { "Hotkey", "Autotext", "Mouse", "Window" };
		var abc = new RadioButton[aTriggersTypeStrings.Length];
		for (int i = 0; i < abc.Length; i++) {
			tb.Items.Add(abc[i] = new RadioButton { Content = aTriggersTypeStrings[i], Width = 60, Margin = new(0, 0, 3, 0), BorderBrush = SystemColors.ActiveBorderBrush });
		}
		
		var bMore = new Button { Content = "...", Width = 24, BorderBrush = SystemColors.ActiveBorderBrush };
		bMore.Click += (_, _) => {
			var m = new popupMenu();
			m["Command line, shortcut, scheduler"] = o => { b.Window.Close(); Menus.TT.Script_triggers(); };
			m["Open file \"Other triggers\""] = o => { b.Window.Close(); Menus.TT.Other_triggers(); };
			m.Show(owner: b.Window);
		};
		tb.Items.Add(bMore);
		
		b.Row(-1).Add("Action", out ListBox lbAction);
		ScrollViewer.SetHorizontalScrollBarVisibility(lbAction, ScrollBarVisibility.Disabled);
		
		b.Row(80).Add(out KSciInfoBox info);
		
		b.End(); //of firstPage
		
		b.R.StartOkCancel();
		b.AddButton(out bBack, "", k => {
			ccPages.Content = pages[(int)tType - 1][--iPage];
			bBack.IsEnabled = iPage > 0;
			bNext.Content = "Next";
		}).Disabled();
		bBack.FontFamily = new("Segoe UI Symbol");
		
		b.AddButton(out bNext, "Next", k => { k.Cancel = _NextPage(); }, WBBFlags.OK);
		
		bool _NextPage() {
			int lastPage = tType == TriggersType.Window ? 2 : 1;
			if (iPage == lastPage) return false;
			iPage++;
			if (tType == TriggersType.Hotkey) {
				if (iPage == 1) pages[(int)tType - 1][iPage] ??= hotkeyTriggerPage = new();
				if (iPage == 1) timer.after(1, _ => { hotkeyTriggerPage.FocusHotkey(); }); //does not work now (after `ccPages.Content = ...`), and even with Dispatcher
			} else if (tType == TriggersType.Autotext) {
				if (iPage == 1) pages[(int)tType - 1][iPage] ??= autotextTriggerPage = new();
				if (iPage == 1) timer.after(1, _ => { autotextTriggerPage.FocusText(); });
			} else if (tType == TriggersType.Mouse) {
				if (iPage == 1) pages[(int)tType - 1][iPage] ??= mouseTriggerPage = new();
			} else if (tType == TriggersType.Window) {
				if (iPage == 1) {
					if (windowTriggerWindowPage == null) {
						pages[(int)tType - 1][iPage] = windowTriggerWindowPage = new();
						if (windowTriggerWnd.IsAlive) windowTriggerWindowPage.SetWnd(windowTriggerWnd);
					}
				}
				if (iPage == 2) pages[(int)tType - 1][iPage] ??= windowTriggerSettingsPage = new();
			}
			ccPages.Content = pages[(int)tType - 1][iPage];
			bBack.IsEnabled = true;
			bNext.Content = iPage < lastPage ? "Next" : "OK";
			return true;
		}
		
		b.AddButton("Cancel", null, WBBFlags.Cancel);
		b.End();
		
		b.End();
		
		if (tType > 0) abc[(int)tType - 1].IsChecked = true;
		if (selectTriggersType == 0) _SetTriggerType(tType, true);
		
		for (int i = 0; i < abc.Length; i++) {
			int tt = i + 1;
			abc[i].Checked += (o, _) => _SetTriggerType((TriggersType)tt, false);
		}
		
		if (selectTriggersType > 0) {
			_SetTriggerType(tType, false);
			//_NextPage();
		}
		
		void _SetTriggerType(TriggersType tt, bool startup) {
			if (!startup) {
				if (!_CodeAnalysis.IsNonStandardFileWithTriggers())
					TriggersAndToolbars.Edit($@"Triggers\{aTriggersTypeStrings[(int)tt - 1]} triggers.cs");
				tType = tt;
			}
			bool enable = tType > 0;
			if (enable) {
				info.aaaText = tType switch {
					TriggersType.Window => $"""
Now in code click where to insert the new trigger.
Then click Next, capture window, Next, set trigger properties, OK.
""",
					TriggersType.Autotext => $"""
Now in code click where to insert the new trigger.
Then click Next, set trigger properties, OK.
In code edit the replacement text. Can use [[|]] to move the caret.
To set trigger scope window can be used {App.Settings.hotkeys.tool_quick}.
""",
					_ => $"""
Now in code click where to insert the new trigger.
Then click Next, set trigger properties, OK.
To set trigger scope window can be used {App.Settings.hotkeys.tool_quick}.
"""
				} + "\r\nFinally click Run to [re]start the triggers script.";
				
				var scriptRun = $"o => script.run(@\"{(canRunThisScript ? fn.ItemPathOrName() : "script.cs")}\")";
				string[] a = tType switch {
					TriggersType.Autotext => [
						"""o => o.Replace("replacement")""",
						""""o => o.Replace("""multiline replacement""")"""",
						//"\"replacement\"", "\"\"\"multiline replacement\"\"\"", //rejected. Just another way to do the same, which is better to avoid. It has sense only when users manually write code.
						"""o => o.Menu("one", "two", new("Label3", "three"))""",
						"o => { print.it(o); }",
						scriptRun],
					TriggersType.Window => [
						"""o => { print.it("Trigger", o.Window); }""",
						"""o => { <close window> }""",
						scriptRun],
					_ => [
						"o => { print.it(o); }",
						scriptRun]
				};
				lbAction.ItemsSource = a;
				lbAction.SelectedIndex = 0;
			}
			info.Visibility = enable ? Visibility.Visible : Visibility.Hidden;
			lbAction.Visibility = enable ? Visibility.Visible : Visibility.Hidden;
			bNext.IsEnabled = enable;
		}
		
		if (!w.ShowAndWait(owner)) return;
		
		_CodeAnalysis.MoveCaretForNewTriggerOrScope();
		string ttVar = _CodeAnalysis.GetTriggersVar(tType);
		
		string s = null, sAction = ((string)lbAction.SelectedItem)[5..];
		
		if (tType == TriggersType.Hotkey) {
			s = hotkeyTriggerPage.FormatCode(sAction, ttVar);
		} else if (tType == TriggersType.Autotext) {
			sAction = sAction.Replace("multiline replacement", "\r\n\r\n");
			s = autotextTriggerPage.FormatCode(sAction, ttVar);
		} else if (tType == TriggersType.Mouse) {
			s = mouseTriggerPage.FormatCode(sAction, ttVar);
		} else if (tType == TriggersType.Window) {
			sAction = sAction.Replace(" <close window> ", """

	print.it("Closing window", o.Window);
	//examples:
	//o.Window.Close();
	//o.Window.ButtonClick("Save");
	//keys.send("Enter");
	//keys.send("Esc");
	//keys.send("Alt+S");

""");
			s = windowTriggerWindowPage.AaResultCode ?? "\"`|`\"";
			s = windowTriggerSettingsPage.FormatCode(s, sAction, ttVar);
		}
		
		InsertCode.Statements(s, ICSFlags.GoTo);
		if (s.Contains("`|`")) {
			CodeInfo.ShowSignature();
			if (tType == TriggersType.Mouse) CodeInfo.ShowCompletionList();
		}
	}
	
	class _HotkeyTriggerPage : UserControl {
		KHotkey _hk;
		EnumUI<TKFlags> _eFlags;
		KCheckBox _cAlwaysEnabled;
		
		public _HotkeyTriggerPage() {
			var b = new wpfBuilder(this);
			b.R.Add(_hk = new(true));
			b.R.StartStack<KGroupBox>(EdWpf.TextAndHelp<TKFlags>("<b>Flags</b>"), true);
			_eFlags = new(b.Panel, items: [
				(TKFlags.ShareEvent, "ShareEvent - let other apps receive the key"),
				(TKFlags.KeyModUp, "KeyModUp - delay until the trigger keys are released"),
				(TKFlags.NoModOff, "NoModOff - don't release modifier keys"),
				(TKFlags.LeftMod, "LeftMod - left-side modifier keys"),
				(TKFlags.RightMod, "RightMod - right-side modifier keys"),
				(TKFlags.ExtendedYes, "ExtendedYes - it is an \"extended key\""),
				(TKFlags.ExtendedNo, "ExtendedNo - it is not an \"extended key\""),
			]);
			b.End();
			b.R.Add(out _cAlwaysEnabled, "Always enabled");
			
			//rejected. Can be confusing. Despite all info, probably often woud be used incorrectly. Let use the scope tool separately.
			//b.R.AddButton("Scope...", o => {
			//	if (_TriggerScopeDialog(out var s, true, Window.GetWindow(this))) _scope = s;
			//}).Width(70);
		}
		
		public string FormatCode(string actionCode, string ttVar) {
			StringBuilder b = new();
			b.Append($"{ttVar}[\"{(_hk.Result ?? "`|`")}\"");
			
			var flags = _eFlags.Result;
			if (flags.Has(TKFlags.ExtendedYes | TKFlags.ExtendedNo)) flags &= ~(TKFlags.ExtendedYes | TKFlags.ExtendedNo);
			if (flags.Has(TKFlags.LeftMod | TKFlags.RightMod)) flags &= ~(TKFlags.LeftMod | TKFlags.RightMod);
			if (flags != 0) b.Append(", flags: ").Append(TUtil.FormatFlags(flags));
			
			b.Append($"] = o => {actionCode};");
			if (_cAlwaysEnabled.IsChecked) b.Append($"\r\n{ttVar}.Last.EnabledAlways = true;");
			return b.ToString();
		}
		
		public void FocusHotkey() {
			_hk.Focus();
		}
	}
	
	class _AutotextTriggerPage : UserControl {
		TextBox _tText;
		EnumUI<TAFlags> _eFlags;
		EnumUI<TAPostfix> _ePostfix;
		ComboBox _useFlags;
		(ComboBox use, ComboBox cb) _postfixType;
		(ComboBox use, TextBox t) _postfixChars;
		KCheckBox _cAlwaysEnabled;
		
		public _AutotextTriggerPage() {
			var b = new wpfBuilder(this).Columns(180, -1);
			b.R.Add<System.Windows.Documents.AdornerDecorator>().Add(out _tText, WBAdd.ChildOfLast).Watermark("Trigger text");
			
			b.R.Add(out _useFlags).Span(1).Items("Use DefaultFlags|Use flags parameter|Set DefaultFlags");
			b.R.StartStack(out KGroupBox gFlags, EdWpf.TextAndHelp<TAFlags>("<b>Flags</b>"), true);
			_useFlags.SelectionChanged += (_, _) => { gFlags.Visibility = _useFlags.SelectedIndex > 0 ? Visibility.Visible : Visibility.Collapsed; };
			_eFlags = new EnumUI<TAFlags>(b.Panel, items: [
				(TAFlags.MatchCase, "MatchCase - case-sensitive"),
				(TAFlags.DontErase, "DontErase - don't erase the text when replacing"),
				(TAFlags.ReplaceRaw, "ReplaceRaw - don't modify the replacement text"),
				(TAFlags.RemovePostfix, "RemovePostfix - erase the postfix character when replacing"),
				(TAFlags.Confirm, "Confirm - show confirmation UI when replacing"),
				(TAFlags.ShiftLeft, "ShiftLeft - select text with Shift+Left when replacing"),
			]);
			b.End().Margin("L20 T").Hidden(null);
			
			b.R.Add(out _postfixType.use).Items("Use DefaultPostfixType|Use postfixType parameter|Set DefaultPostfixType");
			var help2 = EdWpf.TextAndHelp<TAPostfix>(null);
			b.Add(out _postfixType.cb).Hidden().And(16).Add(help2).Hidden();
			_postfixType.use.SelectionChanged += (_, _) => { var v = _postfixType.use.SelectedIndex > 0 ? Visibility.Visible : Visibility.Hidden; _postfixType.cb.Visibility = help2.Visibility = v; };
			_ePostfix = new(_postfixType.cb);
			
			b.R.Add(out _postfixChars.use).Items("Use DefaultPostfixChars|Use postfixChars parameter|Set DefaultPostfixChars");
			b.Add(out _postfixChars.t).Hidden().Tooltip("Postfix characters used when postfix type is Char or CharOrKey (default). Default - non-word characters.\nUse \\t and \\r for Tab and Enter.");
			_postfixChars.use.SelectionChanged += (_, _) => { _postfixChars.t.Visibility = _postfixChars.use.SelectedIndex > 0 ? Visibility.Visible : Visibility.Hidden; };
			
			b.R.Add(out _cAlwaysEnabled, "Always enabled").Span(-1);
			
			b.R.xAddInfoBlockF($"""
<a href='https://www.libreautomate.com/api/Au.Triggers.AutotextTriggers.Item.html'>Trigger help</a>
Also in code you can set PostfixKey, WordCharsPlus and MenuOptions.
""").Span(-1);
		}
		
		public string FormatCode(string actionCode, string ttVar) {
			StringBuilder b = new($"{ttVar}[\"");
			var s = _tText.Text;
			if (s.Length == 0) s = "`|`"; else s = s.Escape();
			b.Append(s).Append('"');
			
			if (_useFlags.SelectedIndex is int useFlags && useFlags > 0) {
				s = TUtil.FormatFlags(_eFlags.Result);
				if (useFlags == 2) b.Insert(0, $"{ttVar}.DefaultFlags = {s};\r\n");
				else if (s == "0") b.Append(", flags: ").Append(s);
				else b.Append(", ").Append(s);
			}
			
			if (_postfixType.use.SelectedIndex is int usePT && usePT > 0) {
				s = "TAPostfix." + _postfixType.cb.Text;
				if (usePT == 1) b.Append(", postfixType: ").Append(s);
				else b.Insert(0, $"{ttVar}.DefaultPostfixType = {s};\r\n");
			}
			
			if (_postfixChars.use.SelectedIndex is int usePC && usePC > 0) {
				s = _postfixChars.t.Text;
				if (s.Contains("\\n") && !s.Contains("\\r")) s = s.Replace("\\n", "\\r"); //for newline need \r, not \n
				s = s.Unescape().Escape(quote: true);
				if (usePC == 1) b.Append(", postfixChars: ").Append(s);
				else b.Insert(0, $"{ttVar}.DefaultPostfixChars = {s};\r\n");
			}
			
			b.Append($"] = o => {actionCode};");
			if (_cAlwaysEnabled.IsChecked) b.Append($"\r\n{ttVar}.Last.EnabledAlways = true;");
			return b.ToString();
		}
		
		public void FocusText() {
			_tText.Focus();
		}
	}
	
	class _MouseTriggerPage : UserControl {
		int _kind;
		EnumUI<TMKind> _eKind;
		ComboBox[] _acb = new ComboBox[4];
		KScreenComboBox _cbScreen;
		KHotkey _hkMod;
		EnumUI<TMFlags> _eFlags;
		KCheckBox _cAlwaysEnabled;
		
		public _MouseTriggerPage() {
			var b = new wpfBuilder(this).Columns(100, 150, 0, 10, -1);
			
			b.R.Add(out ComboBox cbKind);
			_eKind = new(cbKind);
			cbKind.SelectedIndex = _kind = -1;
			
			for (int i = 0; i < 4; i++) {
				Type t = i switch { 0 => typeof(TMClick), 1 => typeof(TMWheel), 2 => typeof(TMEdge), _ => typeof(TMMove) };
				_acb[i] = new() { Padding = cbKind.Padding, ItemsSource = t.GetEnumNames() };
			}
			b.Add(out ContentControl cc);
			var tHelp1 = EdWpf.TextAndHelp(null, () => (_kind switch { /*0 => typeof(TMClick), 1 => typeof(TMWheel),*/ 2 => typeof(TMEdge), _ => typeof(TMMove) }).FullName);
			b.Add(tHelp1).Margin(0).Hidden();
			b.Skip().Add(out _cbScreen).Hidden();
			_cbScreen.Items.Add("screen.ofMouse");
			
			cbKind.SelectionChanged += (_, _) => {
				_kind = cbKind.SelectedIndex;
				cc.Content = _acb[_kind];
				var vis = _kind >= 2 ? Visibility.Visible : Visibility.Hidden;
				tHelp1.Visibility = vis; //don't need help for click and wheel enums
				_cbScreen.Visibility = vis;
			};
			
			b.R.Add(_hkMod = new(true, true));
			b.R.StartStack<KGroupBox>(EdWpf.TextAndHelp<TMFlags>("<b>Flags</b>"), true);
			_eFlags = new EnumUI<TMFlags>(b.Panel, items: [
				(TMFlags.ShareEvent, "ShareEvent - let other apps receive the click/wheel event"),
				(TMFlags.ButtonModUp, "ButtonModUp - delay until the button and keys are released"),
				(TMFlags.LeftMod, "LeftMod - left-side modifier keys"),
				(TMFlags.RightMod, "RightMod - right-side modifier keys"),
			]);
			b.End();
			
			b.R.Add(out _cAlwaysEnabled, "Always enabled").Span(2);
		}
		
		public string FormatCode(string actionCode, string ttVar) {
			StringBuilder b = new($"{ttVar}[TM");
			if (_kind < 0) {
				b.Append("`|`");
			} else {
				var kind = _eKind.Result;
				b.Append(kind).Append('.');
				var s = _acb[_kind].Text;
				b.Append(s.NE() ? "`|`" : s);
			}
			
			if (_hkMod.Result is string sMod) b.Append(", \"").Append(sMod).Append('"');
			
			var flags = _eFlags.Result;
			if (flags.Has(TMFlags.LeftMod | TMFlags.RightMod)) flags &= ~(TMFlags.LeftMod | TMFlags.RightMod);
			if (flags != 0) b.Append(", flags: ").Append(TUtil.FormatFlags(flags));
			
			if (_kind >= 2 && _cbScreen.Result(true) is string sScreen) b.Append(", screen: ").Append(sScreen);
			
			b.Append($$"""] = o => {{actionCode}};""");
			if (_cAlwaysEnabled.IsChecked) b.Append($"\r\n{ttVar}.Last.EnabledAlways = true;");
			return b.ToString();
		}
	}
	
	class _WindowTriggerSettingsPage : UserControl {
		ListBox _lbEvent, _lbWhen;
		KCheckBox _cAtStartup, _cAlwaysEnabled;
		Panel _pLater;
		EnumUI<TWLater> _later;
		
		public _WindowTriggerSettingsPage() {
			var b = new wpfBuilder(this).Columns(0, -1, 10, 0, -1);
			
			b.R.Add("Event", out _lbEvent).Items("Window active|Window visible");
			b.Skip().Add("When", out _lbWhen).Items("New window|Once per window|Every time").Select(0);
			b.R.Add(out _cAtStartup);
			_lbEvent.SelectionChanged += (o, e) => _cAtStartup.Content = $"Run the action at startup if the window then is {(_lbEvent.SelectedIndex == 0 ? "active" : "visible")}";
			_lbEvent.SelectedIndex = 0;
			b.R.Add(out _cAlwaysEnabled, "Always enabled");
			
			b.R.StartGrid<KGroupBox>(EdWpf.TextAndHelp<TWLater>("<b>Events later in that window</b>")).Columns(0, 0, 0, -1);
			_later = new EnumUI<TWLater>(_pLater = b.Panel);
			b.End();
			
			b.End();
		}
		
		public string FormatCode(string wndFindArgs, string actionCode, string ttVar) {
			StringBuilder b = new($"{ttVar}[TWEvent.");
			b.Append(_lbEvent.SelectedIndex == 0 ? "Active" : "Visible").Append(_lbWhen.SelectedIndex switch { 0 => "New", 1 => "Once", _ => null });
			b.Append(", ").Append(wndFindArgs);
			if (_cAtStartup.IsChecked) b.Append(", flags: TWFlags.RunAtStartup");
			var later = _later.Result;
			if (later != 0) b.Append(", later: ").Append(TUtil.FormatFlags(later));
			b.Append("] = o => ");
			
			if (later == 0) {
				b.Append(actionCode).Append(';');
			} else {
				b.Append($$"""
{
	if (o.Later is 0) {
		{{actionCode.Trim(" {};")}};
	}
""");
				foreach (CheckBox c in _pLater.Children) {
					if (c.IsChecked == true) b.Append($$"""
 else if (o.Later is TWLater.{{c.Content}}) {
		print.it("later: {{c.Content}}");
	}
""");
				}
				b.Append("\r\n};");
			}
			
			if (_cAlwaysEnabled.IsChecked) b.Append($"\r\n{ttVar}.Last.EnabledAlways = true;");
			return b.ToString();
		}
	}
	
	static bool _TriggerScopeDialog(out string result, wnd wnd) {
		result = null;
		
		var tt = _CodeAnalysis.GetTriggersType();
		if (tt == TriggersType.Window) {
			dialog.showInfo("Trigger scope", "Scopes are not used with window triggers. Only with hotkey, autotext and mouse triggers.");
			return false;
		}
		
		var w = new KDialogWindow() { Title = "Trigger scope", Topmost = true };
		var b = new wpfBuilder(w).WinSize(500, 350).Columns(0, 0, -1);
		b.R.Add(out DPwnd pwnd).Margin("L T R B6");
		List<string> a = new();
		b.R.AddButton("Add more windows", _ => {
			if (pwnd.HasResult) a.Add(pwnd.AaResultCode);
			pwnd.Clear();
		}).Tooltip("Add another window to the scope");
		b.Add(out KCheckBox cNot, "Not").Tooltip("Triggers will NOT work when the window is active");
		b.StartOkCancel();
		if (tt == 0) {
			b.Add("File", out ComboBox cbOpen).Width(80).Items("|Hotkey|Autotext|Mouse");
			cbOpen.SelectionChanged += (_, _) => {
				int i = cbOpen.SelectedIndex;
				if (i > 0) {
					TriggersAndToolbars.Edit($@"Triggers\{(TriggersType)i} triggers.cs");
					b.Window.Hwnd().ActivateL();
				}
			};
		}
		b.AddOkCancel();
		b.End();
		b.Row(-1).Add(out KSciInfoBox info).Margin("T6");
		info.aaaText = """
This tool will add a new <+recipe>trigger scope<> statement in code. Triggers defined below it will work only when the window is active.

Please make sure the text cursor is in correct place. The new scope statement will be inserted there. Or you can move it afterwards.
""";
		b.End();
		
		if (!wnd.Is0) pwnd.SetWnd(wnd);
		
		if (!w.ShowAndWait()) return false;
		
		var triggersVar = _CodeAnalysis.GetActionTriggersVar();
		var s = pwnd.HasResult ? pwnd.AaResultCode : null;
		if (s == null && a.Count == 0) {
			result = $"{triggersVar}.Of.AllWindows();";
		} else {
			if (a.Count > 0) {
				if (s != null) a.Add(s);
				a = a.Distinct().ToList();
				if (a.Count == 1) { s = a[0]; a.Clear(); }
			}
			StringBuilder f = new($"{triggersVar}.Of.");
			if (cNot.IsChecked) f.Append("Not");
			if (a.Count > 0) {
				f.Append("Windows([");
				for (int i = 0; i < a.Count; i++) {
					f.Append("\r\n\t").Append("new(").Append(a[i]).Append("),");
				}
				f.Append("\r\n]);");
			} else {
				f.Append("Window(").Append(s).Append(");");
			}
			result = f.ToString();
		}
		return true;
	}
	
	/// <summary>
	/// Inserts code statement for a window trigger or trigger scope.
	/// </summary>
	/// <param name="w"></param>
	/// <param name="action">0 trigger tool, 1 window scope, 2 program scope, 3 scope tool.</param>
	public static void QuickWindowTrigger(wnd w, int action) {
		if (action == 0) {
			NewTrigger(TriggersType.Window, w);
		} else {
			string s;
			if (action is 1 or 2) {
				var triggersVar = _CodeAnalysis.GetActionTriggersVar();
				if (action == 1) s = $"{triggersVar}.Of.Window({_WndFindArgs(w)});";
				else s = $"{triggersVar}.Of.Window(of: \"{w.ProgramName.Escape()}\");";
			} else {
				if (!_TriggerScopeDialog(out s, w)) return;
			}
			_CodeAnalysis.MoveCaretForNewTriggerOrScope();
			InsertCode.Statements(s);
		}
	}
	
	static string _WndFindArgs(wnd w) {
		var f = new TUtil.WindowFindCodeFormatter();
		f.RecordWindowFields(w, 0, false);
		return TUtil.ArgsFromWndFindCode(f.Format());
	}
}
