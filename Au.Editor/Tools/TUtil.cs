using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace ToolLand;

static partial class TUtil {
	#region text
	
	/// <summary>
	/// Appends `, ` and string argument.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="s">
	/// Argument value.
	/// If null, appends `null`.
	/// Else if like `@@expression`, appends `expression`.
	/// Else if matches `@"*"` or `$"*"` or `$@"*"` or `@$"*"`, appends `s`.
	/// Else appends `"escaped s"`; can make verbatim.
	/// </param>
	/// <param name="param">If not null, appends `param: s`. By default appends only `s`. If "null", appends `null, s`.</param>
	/// <param name="noComma">Don't append `, `. If false, does not append if b.Length is less than 2 or if b ends with one of: <c>,([{&lt;</c>.</param>
	public static StringBuilder AppendStringArg(this StringBuilder t, string s, string param = null, bool noComma = false) {
		_AppendArgPrefix(t, param, noComma);
		if (s == null) t.Append("null");
		else if (s.Starts("@@")) t.Append(s[2..]);
		else if (IsVerbatim(s, out _) || MakeVerbatim(ref s)) t.Append(s);
		else t.Append(s.Escape(quote: true));
		return t;
	}
	
	/// <summary>
	/// Appends `, ` and non-string argument.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="s">Argument value. Must not be empty.</param>
	/// <param name="param">If not null, appends `param: s`. By default appends only `s`. If "null", appends `null, s`.</param>
	/// <param name="noComma">Don't append `, `. Use for the first parameter. If false, does not append only if b.Length is less than 2.</param>
	public static StringBuilder AppendOtherArg(this StringBuilder t, string s, string param = null, bool noComma = false) {
		Debug.Assert(!s.NE());
		_AppendArgPrefix(t, param, noComma);
		t.Append(s);
		return t;
	}
	
	static void _AppendArgPrefix(StringBuilder t, string param, bool noComma) {
		if (!noComma && t.Length > 1 && t[^1] is not ('(' or '[' or '{' or '<' or ',')) t.Append(", ");
		if (param != null) t.Append(param).Append(param == "null" ? ", " : ": ");
	}
	
	/// <summary>
	/// Appends waitTime. If !orThrow, appends "-" if need.
	/// If !orThrow and !appendAlways and waitTime == "0", appends nothing and returns false.
	/// </summary>
	public static bool AppendWaitTime(this StringBuilder t, string waitTime, bool orThrow, bool appendAlways = false) {
		if (waitTime.NE()) waitTime = "8e88";
		if (!orThrow) {
			if (!appendAlways && waitTime == "0") return false;
			if (!waitTime.Starts('-') && waitTime != "0") t.Append('-');
		}
		t.Append(waitTime);
		return true;
	}
	
	/// <summary>
	/// If some <i>use</i> are true, formats a flags argument like "Enum.Flag1 | Enum.Flag2" and returns true.
	/// </summary>
	public static bool FormatFlags<T>(out string s, params (bool use, T flag)[] af) where T : unmanaged, Enum {
		if (!af.Any(o => o.use)) { s = null; return false; }
		s = string.Join(" | ", af.Where(o => o.use).Select(o => typeof(T).Name + "." + o.flag));
		return true;
	}
	
	/// <summary>
	/// If some <i>use</i> checkboxes are checked or indeterminate, formats a flags argument like "Enum.Flag1 | Enum.Flag2" and returns true. Ignores null controls.
	/// </summary>
	public static bool FormatFlags<T>(out string s, params (CheckBox use, T flag)[] af) where T : unmanaged, Enum {
		return FormatFlags(out s, af.Select(o => (o.use != null && o.use.IsChecked != false, o.flag)).ToArray());
	}
	
	/// <summary>
	/// Formats flags like "Enum.Flag1 | Enum.Flag2".
	/// </summary>
	/// <param name="flags"></param>
	public static string FormatFlags<T>(T flags) where T : unmanaged, Enum {
		var s = flags.ToString();
		if (!char.IsAsciiDigit(s[0])) s = s.Replace(", ", " | ").RxReplace(@"\b(?=\w)", typeof(T).Name + ".");
		return s;
	}
	
	/// <summary>
	/// Returns true if s is like `@"*"` or `$"*"` or `$@"*"` or `@$"*"`.
	/// s can be null.
	/// </summary>
	public static bool IsVerbatim(string s, out int prefixLength) {
		prefixLength = (s.Like(false, "@\"*\"", "$\"*\"", "$@\"*\"", "@$\"*\"") + 1) / 2;
		return prefixLength > 0;
	}
	
	/// <summary>
	/// If s contains \ and no newlines/controlchars: replaces all " with "", prepends @", appends " and returns true.
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static bool MakeVerbatim(ref string s) {
		if (!s.Contains('\\') || s.RxIsMatch(@"[\x00-\x1F\x85\x{2028}\x{2029}]")) return false;
		s = "@\"" + s.Replace("\"", "\"\"") + "\"";
		return true;
	}
	
	/// <summary>
	/// If s has *? characters, prepends "**t ".
	/// s can be null.
	/// </summary>
	public static string EscapeWildex(string s) {
		if (wildex.hasWildcardChars(s)) s = "**t " + s;
		return s;
	}
	
	/// <summary>
	/// If s has *? characters, prepends "**t ".
	/// If <i>canMakeVerbatim</i>, finally calls <see cref="MakeVerbatim"/>.
	/// s can be null.
	/// </summary>
	public static string EscapeWindowName(string s, bool canMakeVerbatim) {
		if (s != null) {
			if (wildex.hasWildcardChars(s)) s = "**t " + s;
			if (canMakeVerbatim) MakeVerbatim(ref s);
		}
		return s;
	}
	//rejected: if name has * at the start or end, make regex like @"**r \*?\QUntitled - Notepad\E", so that would find with or without *.
	//	Ugly. Most users will not know what it is. Anyway in most cases need to replace the document part with *.
	///// <summary>
	///// If s has *? characters, prepends "**t ".
	///// But if s has single * character, converts to "**r regex" that ignores it. Because single * often is used to indicate unsaved state.
	///// If canMakeVerbatim, finally calls <see cref="MakeVerbatim"/>.
	///// s can be null.
	///// </summary>
	//public static string EscapeWindowName(string s, bool canMakeVerbatim) {
	//	if (s == null) return s;
	//	if (wildex.hasWildcardChars(s)) {
	//		int i = s.IndexOf('*');
	//		if (i >= 0 && s.IndexOf('*', i + 1) < 0) {
	//			s = "**r " + regexp.escapeQE(s[..i]) + @"\*?" + regexp.escapeQE(s[++i..]);
	//		} else s = "**t " + s;
	//	}
	//	if (canMakeVerbatim) MakeVerbatim(ref s);
	//	return s;
	//}
	
	/// <summary>
	/// Returns true if newRawValue does not match wildex tbValue, unless contains is like $"..." or $@"...".
	/// </summary>
	/// <param name="tbValue">A wildex string, usually from a TextBox control. Can be raw or verbatim. Can be null.</param>
	/// <param name="newRawValue">New raw string, not wildex. Can be null.</param>
	public static bool ShouldChangeTextBoxWildex(string tbValue, string newRawValue) {
		tbValue ??= "";
		if (newRawValue == null) newRawValue = "";
		if (IsVerbatim(tbValue, out _)) {
			if (tbValue[0] == '$') return false;
			tbValue = tbValue[2..^1].Replace("\"\"", "\"");
		}
		var x = new wildex(tbValue, noException: true);
		return !x.Match(newRawValue);
	}
	
	/// <summary>
	/// Replaces known non-constant window class names with wildcard. Eg "WindowsForms10.EDIT..." with "*.EDIT.*".
	/// </summary>
	/// <param name="s">Can be null.</param>
	/// <param name="escapeWildex">If didn't replace, call <see cref="EscapeWildex"/>.</param>
	public static string StripWndClassName(string s, bool escapeWildex) {
		if (!s.NE()) {
			int n = s.RxReplace(@"^WindowsForms\d+(\..+?\.).+", "*$1*", out s);
			if (n == 0) n = s.RxReplace(@"^(HwndWrapper\[.+?;|Afx:).+", "$1*", out s);
			if (escapeWildex && n == 0) s = EscapeWildex(s);
		}
		return s;
	}
	
	public static string ArgsFromWndFindCode(string wndFind) {
		if (wndFind.RxMatch(@"\bwnd.find\((?:-?\d+, )?(.+)\);", 1, out RXGroup g)) return g.Value;
		return null;
	}
	
	#endregion
	
	#region misc
	
	/// <summary>
	/// Gets Keyboard.FocusedElement. If null, and a HwndHost-ed control is focused, returns the HwndHost.
	/// Slow if HwndHost-ed control.
	/// </summary>
	public static FrameworkElement FocusedElement {
		get {
			var v = System.Windows.Input.Keyboard.FocusedElement;
			if (v != null) return v as FrameworkElement;
			return wnd.Internal_.ToWpfElement(Api.GetFocus());
		}
	}
	
	/// <summary>
	/// Inserts text in specified or focused control.
	/// At current position, not as new line, replaces selection.
	/// </summary>
	/// <param name="c">If null, uses the focused control, else sets focus.</param>
	/// <param name="s">If contains <c>`|`</c>, removes it and moves caret there; must be single line.</param>
	public static void InsertTextIn(FrameworkElement c, string s) {
		if (c == null) {
			c = FocusedElement;
			if (c == null) return;
		} else {
			Debug.Assert(Environment.CurrentManagedThreadId == c.Dispatcher.Thread.ManagedThreadId);
			if (c != FocusedElement) //be careful with HwndHost
				c.Focus();
		}
		
		int i = s.Find("`|`");
		if (i >= 0) {
			Debug.Assert(!s.Contains('\n'));
			s = s.Remove(i, 3);
			i = s.Length - i;
		}
		
		if (c is KScintilla sci) {
			if (sci.aaaIsReadonly) return;
			sci.aaaReplaceSel(s);
			while (i-- > 0) sci.Call(Sci.SCI_CHARLEFT);
		} else if (c is TextBox tb) {
			if (tb.IsReadOnly) return;
			tb.SelectedText = s;
			tb.CaretIndex = tb.SelectionStart + tb.SelectionLength - Math.Max(i, 0);
		} else {
			Debug_.Print(c);
			if (!c.Hwnd().Window.ActivateL()) return;
			Task.Run(() => {
				var k = new keys(null);
				k.AddText(s);
				if (i > 0) k.AddKey(KKey.Left).AddRepeat(i);
				k.SendNow();
			});
		}
	}
	
	/// <summary>
	/// Gets control id. Returns true if it can be used to identify the control in window wWindow.
	/// </summary>
	public static bool GetUsefulControlId(wnd wControl, wnd wWindow, out int id) {
		id = wControl.ControlId;
		if (id == 0 || id == -1 || id > 0xffff || id < -0xffff) return false;
		//info: some apps use negative ids, eg FileZilla. Usually >= -0xffff. Id -1 is often used for group buttons and separators.
		//if(id == (int)wControl) return false; //.NET forms, delphi. //possible coincidence //rejected, because window handles are > 0xffff
		Debug.Assert((int)wControl > 0xffff);
		
		//if(wWindow.Child(id: id) != wControl) return false; //problem with combobox child Edit that all have id 1001
		if (wWindow.ChildAll(id: id).Length != 1) return false; //note: searching only visible controls; else could find controls with same id in hidden pages of tabbed dialog.
		return true;
	}
	
	/// <summary>
	/// Returns <b>wnd.Child</b> parameter <i>skip</i> if <i>c</i> is not the first found <i>w</i> child control with <i>name</i> and <i>cn</i>.
	/// </summary>
	public static string GetControlSkip(wnd w, wnd c, string name, string cn, bool hiddenToo) {
		if (!c.Is0) {
			var a = w.ChildAll(name, cn, hiddenToo ? WCFlags.HiddenToo : 0);
			if (a.Length > 1 && a[0] != c) {
				int skip = Array.IndexOf(a, c);
				if (skip > 0) return skip.ToS();
			}
		}
		return null;
	}
	
	/// <summary>
	/// Calls EventManager.RegisterClassHandler for CheckBox.CheckedEvent, CheckBox.UncheckedEvent, TextBox.TextChangedEvent and optionally ComboBox.SelectionChangedEvent.
	/// Call from static ctor of KDialogWindow-based or ContentControl-based classes.
	/// The specified event handler will be called on events of any of these controls in all dialogs/containers of T type.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="changed">Called on event.</param>
	/// <param name="comboToo">Register for ComboBox too.</param>
	public static void OnAnyCheckTextBoxValueChanged<T>(Action<T, object> changed, bool comboToo = false) where T : ContentControl {
		var h = new RoutedEventHandler((sender, e) => {
			//print.it(sender, e.Source, e.OriginalSource);
			var source = e.OriginalSource; //e.Source can't be used; it == e.OriginalSource if T is KDialogWindow, but == sender if T is UserControl.
			if (source is CheckBox or TextBox or ComboBox) changed(sender as T, source);
		});
		EventManager.RegisterClassHandler(typeof(T), ToggleButton.CheckedEvent, h);
		EventManager.RegisterClassHandler(typeof(T), ToggleButton.UncheckedEvent, h);
		EventManager.RegisterClassHandler(typeof(T), TextBoxBase.TextChangedEvent, h);
		if (comboToo) EventManager.RegisterClassHandler(typeof(T), Selector.SelectionChangedEvent, h);
	}
	
	/// <summary>
	/// Takes screenshot of standard size to display in editor's margin.
	/// Color-quantizes, compresses and converts to a comment string to embed in code.
	/// Returns null if <b>LA.App.Settings.edit_noImages</b>.
	/// </summary>
	/// <param name="p">Point in center of screenshot rectangle.</param>
	/// <param name="capt">If used, temporarily hides its on-screen rect etc.</param>
	public static string MakeScreenshot(POINT p, CapturingWithHotkey capt = null) {
		if (LA.App.Settings.edit_noImages) return null;
		bool v1 = false, v2 = false;
		if (capt != null) {
			if (v1 = capt._osr.Visible) capt._osr.Hwnd.ShowL(false);
			if (v2 = capt._ost.Visible) capt._ost.Hwnd.ShowL(false);
		}
		const int sh = 30;
		var s = ColorQuantizer.MakeScreenshotComment(new(p.x - sh, p.y - sh / 2, sh * 2, sh));
		if (capt != null) {
			if (v1) capt._osr.Hwnd.ShowL(true);
			if (v2) capt._ost.Hwnd.ShowL(true);
		}
		return s;
	}
	
	#endregion
	
	#region OnScreenRect
	
	/// <summary>
	/// Creates standard <see cref="osdRect"/>.
	/// </summary>
	public static osdRect CreateOsdRect(int thickness = 4) => new() { Color = 0xFF0000, Thickness = thickness, TopmostWorkaround_ = true }; //red
	
	/// <summary>
	/// Briefly shows standard blinking on-screen rectangle.
	/// If disp, shows async in its thread.
	/// </summary>
	public static void ShowOsdRect(RECT r, bool error = false, bool limitToScreen = false, Dispatcher disp = null) {
		if (disp != null) {
			disp.InvokeAsync(() => ShowOsdRect(r, error, limitToScreen));
		} else {
			int thick = error ? 6 : 2;
			var osr = new osdRect { Color = error ? 0xFF0000 : 0x0000FF, Thickness = thick * 2, TopmostWorkaround_ = true };
			r.Inflate(thick, thick); //2 pixels inside, 2 outside
			if (limitToScreen) {
				var k = screen.of(r).Rect;
				r.Intersect(k);
			} else _LimitInsaneRect(ref r);
			osr.Rect = r;
			_OsdRectShow(osr, error);
		}
	}
	
	static void _OsdRectShow(osdRect osr, bool error = false) {
		t_hideCapturingRect = true;
		osr.Show();
		int i = 0;
		timer.every(250, t => {
			if (i++ < 4) {
				osr.Hwnd.ZorderTopRaw_();
				osr.Color = (i & 1) != 0 ? 0xFFFF00 : error ? 0xFF0000 : 0x0000FF;
			} else {
				t.Stop();
				osr.Dispose();
				t_hideCapturingRect = false;
			}
		});
	}
	
	[ThreadStatic] static bool t_hideCapturingRect;
	
	//eg VS Code code editor and output are {W=1000000 H=1000000}. Then the rect drawing code would fail.
	static void _LimitInsaneRect(ref RECT r) {
		if (r.Width > 2000 || r.Height > 1200) {
			var rs = screen.virtualScreen; rs.Inflate(100, 100);
			r.Intersect(rs);
		}
	}
	
	/// <summary>
	/// Briefly shows multiple on-screen rectangles.
	/// If disp, shows async in its thread.
	/// Can modify <i>a</i> elements.
	/// </summary>
	public static void ShowOsdRects(RECT[] a, Dispatcher disp = null) {
		if (disp != null) {
			disp.InvokeAsync(() => ShowOsdRects(a));
		} else {
			var osr = new osdRect { Color = 0x0000FF, Thickness = 2, TopmostWorkaround_ = true };
			for (int i = 0; i < a.Length; i++) _LimitInsaneRect(ref a[i]);
			osr.SetRects(a);
			_OsdRectShow(osr);
		}
	}
	
	#endregion
	
	#region test
	
	public record RunTestFindResult(object obj, long speed, InfoStrings info);
	
	/// <summary>
	/// Executes test code that finds an object in window.
	/// Returns the found object, speed and info strings to display. On error speed negative.
	/// </summary>
	/// <param name="owner">Owner dialog.</param>
	/// <param name="code">
	/// Must start with one or more lines that find window or control and set wnd variable named <i>wndVar</i>. Can be any code.
	/// The last line must be a "find object" or "find all objects" function call, like <c>uiimage.find(...);</c>. No `var x = `, no "not found" exception, no wait, no action.
	/// If "find all objects", will display rectangles of all found objects and return the first found object.
	/// </param>
	/// <param name="wndVar">Name of wnd variable of the window or control in which to search.</param>
	/// <param name="w">Window or control in which to search. For a wnd tool can be 0.</param>
	/// <param name="getRect">Callback function that returns object's rectangle in screen. Called when object has been found.</param>
	/// <param name="activateWindow">Between finding window and object in it, activate the found window and wait 200 ms.</param>
	/// <param name="restoreOwner">If this func minimizes or deactivates the owner window, it sets a timer to restore it after eg ~2 seconds. If <i>restoreOwner</i> not null, the timer will delay restoring until restoreOwner[0] != 0, after restoreOwner[0] ms.</param>
	/// <param name="rectDisp">Use this dispatcher to show rectangles. For example if calling this in a non-UI thread and want to show in UI thread.</param>
	/// <example>
	/// <code><![CDATA[
	/// var rr = TUtil.RunTestFindObject(this, code, wndVar, _wnd, o => (o as elm).Rect);
	/// _info.InfoErrorOrInfo(rr.info);
	/// ]]></code>
	/// </example>
	public static RunTestFindResult RunTestFindObject(
		AnyWnd owner, string code, string wndVar, wnd w,
		Func<object, RECT> getRect = null,
		/*
		/// <param name="invoke">Callback that executes the code. Let it call/return MethodInfo.Invoke(null, null). For example if wants to execute in other thread. If null, the code is executed in this thread.</param>
		Func<MethodInfo, object> invoke = null
		*/
		bool activateWindow = false, int[] restoreOwner = null, Dispatcher rectDisp = null) {
		
		Debug.Assert(!code.NE());
		//print.it(code);
		//perf.first();
		
		var code0 = code;
		var b = new StringBuilder();
		b.AppendLine(@"static object[] __TestFunc__() {");
		if (activateWindow) b.Append("((wnd)").Append(w.Window.Handle).Append(").ActivateL(); 300.ms(); ");
		b.AppendLine("var _p_ = perf.local();");
		b.AppendLine("#line 1");
		var lines = code.Lines(true);
		int lastLine = lines.Length - 1;
		for (int i = 0; i < lastLine; i++) b.AppendLine(lines[i]);
		b.AppendLine("_p_.Next(); var _a_ =");
		b.AppendLine("#line " + (lastLine + 1));
		b.AppendLine(lines[lastLine]);
		b.AppendLine("_p_.Next();");
		b.AppendLine($"return new object[] {{ _p_.ToArray(), _a_, {wndVar} }};");
		b.AppendLine("\r\n}");
		code = b.ToString(); //print.it(code);
		
		(long[] speed, object obj, wnd w) r = default;
		wnd dlg = owner.Hwnd;
		bool dlgWasActive = dlg.IsActive, dlgMinimized = false;
		try {
			try {
				if (!Scripting.Compile(code, out var c, addUsings: true, addGlobalCs: true, wrapInClass: true, dll: true)) {
					Debug_.Print("---- CODE ----\r\n" + code + "--------------");
					//shows code too, because it may be different than in the code box
					return new(null, -1, new(true, "Errors:", $"{c.errors}\r\n\r\n<lc #C0C0C0><b>Code:<><>\r\n<code>{code0}</code>"));
				}
				//object ro = invoke?.Invoke(c.method) ?? c.method.Invoke(null, null);
				object ro = c.method.Invoke(null, null);
				var rr = (object[])ro; //use array because fails to cast tuple, probably because in that assembly it is new type
				r = ((long[])rr[0], rr[1], (wnd)rr[2]);
			}
			catch (Exception e) {
				if (e is TargetInvocationException tie) e = tie.InnerException;
				string s1, s2;
				if (e is NotFoundException) { //info: throws only when window not found
					s1 = "Window not found";
					s2 = "Tip: If part of window name changes, replace it with *";
				} else {
					s1 = e.GetType().Name;
					s2 = e.Message.RxReplace(@"^Exception of type '.+?' was thrown. ", "");
					if (e.StackTrace.RxMatch(@"(?m)^\s*( at .+?)\(.+\R\s+\Qat __script__.__TestFunc__()\E", 1, out string s3)) s1 += s3;
				}
				return new(null, -2, new(true, s1, s2));
			}
			
			//perf.nw();
			//print.it(r);
			
			static double _SpeedMcsToMs(long tn) => Math.Round(tn / 1000d, tn < 1000 ? 2 : (tn < 10000 ? 1 : 0));
			double t0 = _SpeedMcsToMs(r.speed[0]), t1 = _SpeedMcsToMs(r.speed[1]); //times of wnd.find and Object.Find
			string sSpeed;
			if (lastLine == 1 && lines[0] == "wnd w;") sSpeed = t1.ToS() + " ms"; //only wnd.find: "wnd w;\r\nw = wnd.find(...);"
			else sSpeed = t0.ToS() + " + " + t1.ToS() + " ms";
			
			if (r.obj is wnd w1 && w1.Is0) r.obj = null;
			
			//FindAll used instead of Find?
			var en = r.obj as IEnumerable<object>;
			if (en != null && !en.Any()) r.obj = null;
			
			if (r.obj != null) {
				RECT re;
				if (en != null) {
					var ar = en.Select(o => getRect(o)).ToArray();
					re = RECT.FromLTRB(ar.Min(o => o.left), ar.Min(o => o.top), ar.Max(o => o.right), ar.Max(o => o.bottom));
					r.obj = en.First();
					ShowOsdRects(ar, disp: rectDisp);
				} else {
					re = getRect(r.obj);
					ShowOsdRect(re, disp: rectDisp);
				}
				
				//if dlg covers the found object, temporarily minimize it (may be always-on-top) and activate object's window. Never mind owners.
				var wTL = r.w.Window;
				if (dlgMinimized = dlg.Rect.IntersectsWith(re) && !r.w.IsOfThisThread && !dlg.IsMinimized) {
					dlg.ShowMinimized(1);
					wTL.ActivateL();
					wait.doEvents(1000);
				}
			}
			
			if (r.w != w && !r.w.Is0 && !w.Is0) {
				ShowOsdRect(r.w.Rect, error: true, limitToScreen: true, disp: rectDisp);
				//FUTURE: show list of objects inside the wanted window, same as in the Dwnd 'contains' combo. Let user choose. Then update window code quickly.
				//string wndCode = null;
				//wndCode = "wnd w = wnd.find(\"Other\");";
				return new(null, -3, new(true, "Finds another " + (r.w.IsChild ? "control" : "window"), $"<i>Need:<>  {w}\r\n<i>Found:<>  {r.w}"));
			}
			
			return new(r.obj, r.speed[1], new(r.obj == null, r.obj != null ? "Found" : "Not found", null, ",  speed " + sSpeed));
		}
		finally {
			if (dlgWasActive || dlgMinimized) {
				int after = activateWindow && !dlgMinimized && r.w == w ? 1500 : 500;
				timer.after(after, t => {
					if (!dlg.IsAlive) return;
					if (restoreOwner == null) {
						if (dlgMinimized) dlg.ShowNotMinimized(1);
						if (dlgWasActive) dlg.ActivateL();
					} else if (restoreOwner[0] == 0) {
						t.After(100);
					} else {
						t.After(restoreOwner[0]);
						restoreOwner = null;
					}
				});
			}
		}
	}
	
	public record InfoStrings(bool isError, string header, string text, string headerSmall = null) {
		//public string text2 { get; set; }
	}
	
	/// <summary>
	/// Executes action code for a found UI element etc.
	/// </summary>
	/// <returns>Error strings to display, or null if no error.</returns>
	/// <param name="obj">The element. The function passes it to the test script.</param>
	/// <param name="code">Code like "Method(arguments)". The function prepends "obj." and appends ";".</param>
	public static InfoStrings RunTestAction(object obj, string code) {
		var code0 = code;
		code = $@"static void __TestFunc__({obj.GetType()} obj) {{
#line 1
obj.{code};
}}";
		//print.it(code);
		
		try {
			if (!Scripting.Compile(code, out var c, addUsings: true, addGlobalCs: true, wrapInClass: true, dll: true))
				return new(true, "Errors:", $"{c.errors}\r\n\r\n<b>Code:<>\r\n<code>obj.{code0};</code>");
			c.method.Invoke(null, new object[] { obj });
			return null;
		}
		catch (Exception e) {
			if (e is TargetInvocationException tie) e = tie.InnerException;
			return new(true, "Action failed", e.GetType().Name + ". " + e.Message.RxReplace(@"^Exception of type '.+?' was thrown. ", ""));
		}
	}
	
	public static async Task<InfoStrings> RunTestCodeAsync(string code, CancellationToken cancel = default) {
		var code0 = code;
		code = $@"static void __TestFunc__(CancellationToken cancel) {{
#line 1
{code}
}}";
		//print.it(code);
		
		try {
			if (!Scripting.Compile(code, out var c, addUsings: true, addGlobalCs: true, wrapInClass: true, dll: true)) {
				Debug_.Print("---- CODE ----\r\n" + code + "--------------");
				return new(true, "Errors:", $"{c.errors}\r\n\r\n<b>Code:<>\r\n<code>{code0};</code>");
			}
			await Task.Run(() => { c.method.Invoke(null, new object[] { cancel }); });
			return null;
		}
		catch (Exception e) {
			if (e is TargetInvocationException tie) e = tie.InnerException;
			return new(true, "Failed", e.GetType().Name + ". " + e.Message.RxReplace(@"^Exception of type '.+?' was thrown. ", ""));
		}
	}
	
	#endregion
	
	#region info
	
	public static void InfoError(this KSciInfoBox t, string header, string text, string headerSmall = null) {
		t.aaaText = $"<lc #F0E080><b>{header}<>{headerSmall}<>\r\n{text}";
		t.AaSuspendElems();
	}
	
	public static void InfoInfo(this KSciInfoBox t, string header, string text, string headerSmall = null) {
		t.aaaText = $"<lc #C0E0C0><b>{header}<>{headerSmall}<>\r\n{text}";
		t.AaSuspendElems();
	}
	
	public static void InfoErrorOrInfo(this KSciInfoBox t, InfoStrings info) {
		var text = info.text /*?? info.text2*/;
		if (info.isError) InfoError(t, info.header, text, info.headerSmall);
		else InfoInfo(t, info.header, text, info.headerSmall);
	}
	
	public static void Info(this KSciInfoBox t, FrameworkElement e, string name, string text) {
		text = CommonInfos.PrependName(name, text);
		t.AaAddElem(e, text);
	}
	
	public static void InfoC(this KSciInfoBox t, ContentControl k, string text) => Info(t, k, _ControlName(k), text);
	
	public static void InfoCT(this KSciInfoBox t, KCheckTextBox k, string text, bool isWildex = false, string wildexPart = null) {
		text = CommonInfos.PrependName(_ControlName(k.c), text);
		if (isWildex) text = CommonInfos.AppendWildexInfo(text, wildexPart);
		t.AaAddElem(k.c, text);
		t.AaAddElem(k.t, text);
	}
	
	public static void InfoCO(this KSciInfoBox t, KCheckComboBox k, string text) {
		text = CommonInfos.PrependName(_ControlName(k.c), text);
		t.AaAddElem(k.c, text);
		t.AaAddElem(k.t, text);
	}
	
	/// <summary>
	/// Returns k text without '_' character used for Alt+underline.
	/// </summary>
	static string _ControlName(ContentControl k) => StringUtil.RemoveUnderlineChar(k.Content as string, '_');
	
	/// <summary>
	/// Can be used by tool dialogs to display common info in <see cref="KSciInfoBox"/> control.
	/// </summary>
	public class CommonInfos {
		KSciInfoBox _control;
		RegexWindow _regexWindow;
		
		public CommonInfos(KSciInfoBox control) {
			_control = control;
			_control.AaTags.AddLinkTag("+regex", o => _Regex(o));
		}
		
		void _Regex(string _) {
			_regexWindow ??= new();
			if (_regexWindow.Hwnd.Is0) {
				_regexWindow.ShowByRect(_control.Hwnd().Window, Dock.Bottom);
			} else _regexWindow.Hwnd.ShowL(true);
		}
		
		/// <summary>
		/// Formats "name - text" string, where name is bold: <![CDATA["<b>" + name + "<> - " + text]]>.
		/// </summary>
		public static string PrependName(string name, string text) => "<b>" + name + "<> - " + text;
		
		public static string AppendWildexInfo(string s, string part = null) => s + "\r\n" + (part ?? "The text") +
@" is <help articles/Wildcard expression>wildcard expression<>. Can be <+regex>regex<>, like <mono>**rc regex<>.
Examples:
<mono>whole text
*end
start*
*middle*
time ??:??
**t literal text
**c case-sensitive text
**tc case-sensitive literal
**r regular expression
**rc case-sensitive regex
**n not this
**m this||or this||**r or this regex||**n and not this
**m(^^^) this^^^or this^^^or this
@""C# verbatim string""
<>";
		
		public const string c_alsoParameter = @"<i>also<> lambda.
Can be multiline.
Can use global usings and classes/functions from file ""global.cs"".";
	}
	
	/// <summary>
	/// Auto-creates and shows click-closed system-colored tooltip below element e.
	/// </summary>
	public static void InfoTooltip(ref KPopup p, UIElement e, string text, Dock side = Dock.Bottom) {
		if (p == null) {
			p = new(WS.POPUP | WS.BORDER, shadow: true) { Content = new Label(), ClickClose = KPopup.CC.Anywhere };
			p.Border.Background = SystemColors.InfoBrush;
		}
		(p.Content as Label).Content = text;
		p.ShowByRect(e, side);
	}
	
	///// <summary>
	///// Auto-creates and shows tooltip below element e. The tooltip has system colors, not WPF colors.
	///// </summary>
	//public static void InfoTooltip(ref ToolTip tt, UIElement e, string text) {
	//	tt ??= new ToolTip { StaysOpen = false, Placement = PlacementMode.Bottom, Background = SystemColors.InfoBrush, Foreground = SystemColors.InfoTextBrush };
	//	tt.PlacementTarget = e;
	//	tt.Content = text;
	//	tt.IsOpen = false;
	//	tt.IsOpen = true;
	//}
	
	public static void InfoRectCoord(wnd w, string rect) {
		if (!rect.RxMatch(@"^ *\( *(\d+) *, *(\d+) *, *(\d+) *, *(\d+) *\) *", out var m)) return;
		InfoRectCoord(w.ClientRect, new RECT(m[1].Value.ToInt(), m[2].Value.ToInt(), m[3].Value.ToInt(), m[4].Value.ToInt()));
	}
	
	public static void InfoRectCoord(RECT rOuter, RECT rInner) {
		var b = new StringBuilder();
		b.AppendFormat("<><help Au.Types.IFArea>IFArea<> <help Au.Types.Coord>Coord<> values equivalent to RECT ({0}, {1}, {2}, {3}) for window size ({4}, {5}): <fold>\r\n", rInner.left, rInner.top, rInner.Width, rInner.Height, rOuter.Width, rOuter.Height);
		b.AppendFormat("           {0,-8} {1,-8} {2,-8} {3}\r\n", "left", "top", "right", "bottom");
		for (int i = 0; i < 3; i++) {
			b.AppendFormat("{0,-11}", i switch { 0 => "Simple", 1 => "Reverse", _ => "Fraction" });
			for (int j = 0; j < 4; j++) {
				var (e, s1) = j switch { 0 => (rInner.left, "left"), 1 => (rInner.top, "top"), 2 => (rInner.right, "right"), _ => (rInner.bottom, "bottom") };
				int wh = ((j & 1) == 0 ? rOuter.Width : rOuter.Height);
				b.AppendFormat("{0,-9}", i switch { 0 => e.ToS(), 1 => "^" + (wh - e).ToS(), _ => wh == 0 ? "0" : ((double)e / wh).ToS("0.###") + "f" });
			}
			b.AppendLine();
		}
		b.Append("</fold>");
		print.it(b);
	}
	
	#endregion
}
