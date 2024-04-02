using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Au.Controls;
using System.Windows.Threading;

//FUTURE: record "wait for UAC consent and then for normal desktop".

#if SCRIPT
using Au.Tools;
namespace Au.Tools2;
#else
namespace Au.Tools;
#endif

class DInputRecorder : KDialogWindow {
	public static void ShowRecorder() {
		if (s_showing) return;

		int t = WindowsHook.LowLevelHooksTimeout;
		if (t < 300) { //default 300, max 1000
			print.it($"Warning: incorrect Windows settings. The keyboard/mouse hook timeout is {t} ms. Should be 1000. Set it in Options -> OS, and restart computer. Now recording and triggers are unreliable.");
		}

		var ir = new DInputRecorder();
#if SCRIPT
		ir.ShowDialog();
#else
		ir.Show();
#endif
	}
	static bool s_showing;

	wnd _wThis, _wFore;
	bool _recordKeys, _recordText, _recordText2, _recordMouse, _recordWheel, _recordDrag, _recordMove;
	int _xyIn;
	ListBox _list;
	ScrollViewer _scroller;
	TextBox _tSpeed;

	const string c_iconPause = "*Material.PauseCircleOutline" + Menus.blue,
		c_iconRetry = "*BoxIcons.RegularReset" + Menus.red,
		c_iconUndo = Menus.iconUndo;

	const int c_xyControl = 1, c_xyScreen = 2;

	DInputRecorder() {
		Title = "Input recorder";
		var b = new wpfBuilder(this).WinSize((300, 200..400), (284, 270..)).Columns(80, -1);
		b.WinProperties(WindowStartupLocation.Manual, showActivated: false, showInTaskbar: false, topmost: true, style: WindowStyle.ToolWindow);
		b.Window.SizeToContent = 0;

		b.Row(-1).StartGrid().Columns(-1);
		b.Options(margin: new(1));
		b.Add(out KCheckBox cKeys, "Keys").Tooltip("Record keyboard");
		b.Add(out KCheckBox cText, "Text").Margin("L8").Tooltip("Record text keys as text, not as key names");
		b.Add(out KCheckBox cText2, "Text+").Margin("L16").Tooltip("Record Alt+Ctrl+key as text if possible");
		b.Add(out KCheckBox cMouse, "Mouse").Tooltip("Record mouse clicks and optionally wheel and movements");
		b.Add(out KCheckBox cWheel, "Wheel").Margin("L8").Tooltip("Record mouse wheel");
		b.Add(out KCheckBox cDrag, "Drag").Margin("L8").Tooltip("Record mouse movements while a button is pressed");
		b.Add(out KCheckBox cMove, "Move").Margin("L8").Tooltip("Record mouse movements while buttons aren't pressed");
		b.Add(out ComboBox cbIn).Items("Window|Control|Screen").Tooltip("Mouse position relative to").Margin("2");
		cbIn.SelectionChanged += (_, _) => { _xyIn = cbIn.SelectedIndex; };
		cbIn.SelectedIndex = Math.Clamp(App.Settings.recorder.xyIn, 0, 2);
		b.Add<Label>("Speed").And(34).Add(out _tSpeed, App.Settings.recorder.speed).Tooltip("If not empty, adds code that sets mouse/keys/text speed = this value");
		b.Options(margin: new(2));

		cKeys.CheckChanged += (_, _) => {
			_recordKeys = cKeys.IsChecked;
			_recordText = _recordKeys ? cText.IsChecked : false;
			cText.IsEnabled = _recordKeys;
			_recordText2 = _recordKeys && _recordText ? cText2.IsChecked : false;
			cText2.IsEnabled = _recordKeys && _recordText;
		};
		cText.CheckChanged += (_, _) => {
			_recordText = _recordKeys && cText.IsChecked;
			cText2.IsEnabled = _recordKeys && _recordText;
		};
		cText2.CheckChanged += (_, _) => { _recordText2 = _recordKeys && _recordText && cText2.IsChecked; };
		cMouse.CheckChanged += (_, _) => {
			_recordMouse = cMouse.IsChecked;
			_recordWheel = _recordMouse ? cWheel.IsChecked : false;
			_recordDrag = _recordMouse ? cDrag.IsChecked : false;
			_recordMove = _recordMouse ? cMove.IsChecked : false;
			cWheel.IsEnabled = _recordMouse;
			cDrag.IsEnabled = _recordMouse;
			cMove.IsEnabled = _recordMouse;
		};
		cWheel.CheckChanged += (_, _) => { _recordWheel = _recordMouse && cWheel.IsChecked; };
		cDrag.CheckChanged += (_, _) => { _recordDrag = _recordMouse && cDrag.IsChecked; };
		cMove.CheckChanged += (_, _) => { _recordMove = _recordMouse && cMove.IsChecked; };

		if (App.Settings.recorder.keys) cKeys.IsChecked = true; else { cText.IsEnabled = false; cText2.IsEnabled = false; }
		if (App.Settings.recorder.text) cText.IsChecked = true; else cText2.IsEnabled = false;
		if (App.Settings.recorder.text2) cText2.IsChecked = true;
		if (App.Settings.recorder.mouse) cMouse.IsChecked = true; else { cWheel.IsEnabled = false; cDrag.IsEnabled = false; cMove.IsEnabled = false; }
		if (App.Settings.recorder.wheel) cWheel.IsChecked = true;
		if (App.Settings.recorder.drag) cDrag.IsChecked = true;
		if (App.Settings.recorder.move) cMove.IsChecked = true;
		this.Closing += (_, _) => {
			App.Settings.recorder.keys = cKeys.IsChecked;
			App.Settings.recorder.text = cText.IsChecked;
			App.Settings.recorder.text2 = cText2.IsChecked;
			App.Settings.recorder.mouse = cMouse.IsChecked;
			App.Settings.recorder.wheel = cWheel.IsChecked;
			App.Settings.recorder.drag = cDrag.IsChecked;
			App.Settings.recorder.move = cMove.IsChecked;
			App.Settings.recorder.xyIn = _xyIn;
			App.Settings.recorder.speed = _tSpeed.Text;
		};

		b.Row(-1).StartStack();
		b.xAddCheckIcon(c_iconPause, "Pause recording").CheckChanged += (_, _) => _Pause();
		b.xAddButtonIcon(c_iconRetry, _ => _Clear(), "Clear the recorded data");
		b.xAddButtonIcon(c_iconUndo, _ => _Undo(), "Remove the last recorded event, or restore cleared data");
		b.End();

		b.AddButton(out var bOK, "OK", _ => _OK(false)).Tooltip("Insert the code and close.\nRight-click to copy to the clipboard.");
		bOK.MouseRightButtonUp += (_, _) => _OK(true);
		//b.AddButton("Copy", _=>_OK(true)).Tooltip("Copy the code to the clipboard.\nStops recording and closes this window.");
		//b.AddButton("Cancel", _=>Close()); //can instead use x or Copy

		b.End();

		b.Add(out _list).Margin(5, 2, 2, 2);

		b.End();

		b.WinSaved(App.Settings.wndpos.recorder, o => App.Settings.wndpos.recorder = o);
	}

	static DInputRecorder() {
		//JIT some code that is called in hook proc
		var w = App.Hmain;
		var r1 = new _RecoWinFind { w = w, varName = 1, waitS = 1 };
		r1.FormatCode();
		var r2 = new _RecoWinChild { w = w.Get.FirstChild, varName = 1, windowVarName = 1 };
		r2.FormatCode(w);
		//print.it(r1.code); print.it(r2.code);
	}

	protected override void OnSourceInitialized(EventArgs e) {
		base.OnSourceInitialized(e);

		_wThis = this.Hwnd();
		_scroller = _list.FindVisualDescendant(o => o is ScrollViewer) as ScrollViewer;
		//_wThis.MoveInScreen(^1, ^1);
		s_showing = true;
		App.Hmain.ShowMinimized();

		Dispatcher.InvokeAsync(() => { //set hooks when fully loaded
			if (_closed) return;
			_keyHook = WindowsHook.Keyboard(_KeyHook);
			_mouseHook = WindowsHook.Mouse(_MouseHook);
			//_weHook=new WinEventHook(new EEvent[] { EEvent.OBJECT_SHOW, EEvent.OBJECT_HIDE }, _WeHook, flags: EHookFlags.SKIPOWNTHREAD);
		}, DispatcherPriority.ApplicationIdle);
	}

	protected override void OnClosed(EventArgs e) {
		base.OnClosed(e);

		_closed = true;
		_keyHook.Dispose();
		_mouseHook.Dispose();
		//_weHook.Dispose();
		s_showing = false;
		App.Hmain.ShowNotMinimized(); //BAD: flickers. With animation not so bad.
		App.Hmain.ActivateL();
	}
	bool _closed;

	protected override void OnActivated(EventArgs e) {
		if (wnd.active == _wThis) {
			if (_Last is _RecoMouseMoveBy) _Remove(^1);
		}
		base.OnActivated(e);
	}

	enum _Event { Key, MButton, MMove }

	abstract record _Reco {
		public wnd w;
		public int time;
	}

	record _RecoKey : _Reco {
		public KKey key;
		public KMod mod;
		public sbyte down; //1 down, -1 up, 0 down+up
		public int count;

		public override string ToString() {
			using (new StringBuilder_(out var b)) {
				keys.more.hotkeyToString(b, mod, key);
				if (down > 0) b.Append("*down"); else if (down < 0) b.Append("*up");
				if (count > 1) b.Append('*').Append(count);
				return b.ToString();
			}
		}
	}

	record _RecoChar : _Reco {
		public char c;
		public bool alt;
		public int count;
		public string s;

		public override string ToString() => (alt ? "Alt+" : null) + (s ?? c.ToString()) + (count > 1 ? ("*" + count) : null);

		public void FormatCode(StringBuilder b, bool first) {
			if (alt) {
				if (!first) b.Append("\", \"");
				b.Append("Alt+").Append(count > 1 ? '_' : '^');
			} else {
				if (first) b.Append(count > 1 ? '_' : '^');
				else if (count > 1) b.Append("\", \"_");
			}
			FormatCodeSimple(b);
		}

		public void FormatCodeSimple(StringBuilder b) {
			if (s != null) b.Append(s.Escape());
			else {
				if (c is '"' or '\\') b.Append('\\');
				b.Append(c);
				if (count > 1) b.Append('*').Append(count);
			}
		}
	}

	record _RecoMouse : _Reco //mouse.move() and base of _RecoMouseX
	{
		public POINT p, pw; //in screen and in window
		public int varName; //0 if in screen, >0 if in window (_RecoWinFind with this varName), <0 if in control (_RecoWinChild with minus this varName)

		protected string _VarName => varName == 0 ? null : (varName > 0 ? (" in w" + varName) : (" in c" + -varName));

		public override string ToString() => "Mouse move" + _VarName;

		public virtual void FormatCode(StringBuilder b) {
			b.Append("mouse.move(");
			_FormatMoveArgs(b);
			b.Append(");");
		}

		protected void _FormatMoveArgs(StringBuilder b) {
			if (varName != 0) {
				b.AppendFormat("{0}{1}, {2}, {3}", varName > 0 ? 'w' : 'c', Math.Abs(varName), pw.x, pw.y);
			} else {
				b.AppendFormat("{0}, {1}", p.x, p.y);
			}
		}
	}

	record _RecoMouseButton : _RecoMouse {
		public MButton button;
		public sbyte down; //1 down, -1 up, 0 down+up
		public bool two, noXY, activated;
		public string image, comment;

		public override string ToString() => $"Mouse {button.ToString().Lower()}{(down switch { 1 => " down", -1 => " up", _ => null })}{(two ? "*2" : null)}{_VarName}"; //not useful:, {p.x}, {p.y}

		public override void FormatCode(StringBuilder b) {
			b.Append("mouse.");
			bool ex = false; if (two) ex = button != MButton.Left; else ex = button is not (MButton.Left or MButton.Right);
			string s;
			if (ex) s = "clickEx";
			else if (two) s = "doubleClick";
			else if (down > 0) s = button == MButton.Left ? "leftDown" : "rightDown";
			else if (down < 0) s = button == MButton.Left ? "leftUp" : "rightUp";
			else s = button == MButton.Left ? "click" : "rightClick";
			b.Append(s).Append('(');
			if (ex) {
				b.Append("MButton.").Append(button);
				if (two) b.Append(" | MButton.DoubleClick"); else if (down > 0) b.Append(" | MButton.Down"); else if (down < 0) b.Append(" | MButton.Up");
				if (!noXY) b.Append(", ");
			}
			if (!noXY) _FormatMoveArgs(b);
			b.Append(");");
		}

		public void FormatDrag(StringBuilder b, object dxy/*, KMod mod*/) {
			b.Append("mouse.drag(");
			_FormatMoveArgs(b);
			switch (dxy) {
			case (int dx, int dy): b.AppendFormat(", {0}, {1}", dx, dy); break;
			case string s: b.AppendFormat(", \"{0}\"", s); break;
			}
			if (button != MButton.Left) b.Append(", MButton.").Append(button);
			//never mind: could also format modifier keys, but it's quite difficult, eg can be pressed before or/and after the mouse button.
			b.Append(");");
		}
	}

	record _RecoMouseWheel : _RecoMouse {
		public int ticks;
		public bool move;

		public override string ToString() => move && varName != 0 ? $"Mouse wheel {_TicksToS}{_VarName}" : $"Mouse wheel {_TicksToS}";

		string _TicksToS => (ticks / 120d).ToS();

		public override void FormatCode(StringBuilder b) {
			if (move) { base.FormatCode(b); b.AppendLine(); }
			b.AppendFormat("mouse.wheel({0});", _TicksToS);
		}
	}

	record _RecoMouseMoveBy : _RecoMouse {
		//p - the last move coord
		//a - all relative coords
		public List<uint> a;
		public bool drag;
		string _ostring;

		public override string ToString() => "Mouse " + (drag ? "drag" : "move");

		public string OffsetsString => _ostring ??= _GetOString();

		string _GetOString() {
			//remove some points to make shorter and faster
			//in n1=a.Count;
			double prevAngle = 400, dist = 0;
			for (int i = a.Count - 1; --i > 0;) {
				int dx = Math2.LoShort((nint)a[i]), dy = Math2.HiShort((nint)a[i]);
				double angle = Math2.AngleFromXY(dx, dy);
				dist += Math.Sqrt(dx * dx + dy * dy);
				//print.it(dx, dy, angle, dist);
				if (angle == prevAngle && dist < 30) {
					a.RemoveAt(i);
					int dx2 = Math2.LoShort((nint)a[i]), dy2 = Math2.HiShort((nint)a[i]);
					a[i] = (uint)Math2.MakeLparam(dx2 + dx, dy2 + dy);
				} else {
					prevAngle = angle;
					dist = 0;
				}
			}
			//print.it(n1, a.Count);
			return RecordingUtil.MouseToString(a, withSleepTimes: false);
		}

		public override void FormatCode(StringBuilder b) {
			b.AppendFormat("mouse.moveBy(\"{0}\");", OffsetsString);
		}
	}

	record _RecoWin : _Reco //wnd.Activate, wnd.WaitForName, base of _RecoWinFind
	{
		public int varName;
		public int waitS;
		public bool activate;
		public string code;

		public override string ToString() {
			using (new StringBuilder_(out var b)) {
				b.AppendFormat("wnd w{0}: ", varName);
				if (waitS != 0) {
					b.Append("new name");
					if (activate) b.Append(", activate");
				} else {
					b.Append("activate");
				}
				return b.ToString();
			}
		}

		public void FormatCode(string name = null) {
			using (new StringBuilder_(out var b)) {
				if (waitS != 0) {
					b.AppendFormat("w{0}.WaitForName({1}", varName, waitS).AppendStringArg(TUtil.EscapeWindowName(name, true)).Append(");");
					if (activate) b.AppendLine();
				}
				if (activate) b.AppendFormat("w{0}.Activate();", varName);
				code = b.ToString();
			}
		}
	}

	record _RecoWinFind : _RecoWin {
		public string name;

		public void FormatCode(int ownerVar) {
			var f = new TUtil.WindowFindCodeFormatter { VarWindow = "w" + varName };
			f.RecordWindowFields(w, waitS, activate, ownerVar > 0 ? "w" + ownerVar : null);
			code = f.Format();
			//print.it(code);
		}

		public override string ToString() {
			using (new StringBuilder_(out var b)) {
				b.AppendFormat("wnd w{0}: ", varName);
				b.Append("find"); if (waitS != 0) b.Append("/wait");
				if (activate) b.Append(", activate");
				return b.ToString();
			}
		}
	}

	record _RecoWinChild : _Reco {
		public string code;
		public int varName, windowVarName;

		public void FormatCode(wnd window) {
			var f = new TUtil.WindowFindCodeFormatter { NeedWindow = false, VarWindow = "w" + windowVarName, VarControl = "c" + varName, Throw = true, waitW = "1" };
			f.RecordControlFields(window, w);
			code = f.Format();
			//print.it(code);
		}

		public override string ToString() {
			using (new StringBuilder_(out var b)) {
				b.AppendFormat("Find control c{0} in w{1}", varName, windowVarName);
				return b.ToString();
			}
		}
	}

	record _RecoSleep : _Reco {
	}

	WindowsHook _keyHook, _mouseHook;
	//WinEventHook _weHook;
	List<_Reco> _a = new(), _aUndo;
	bool _paused;
	bool _canMove;

	void _Pause() {
		if (_paused ^= true) _keyToText.Clear();
	}

	void _Clear() {
		if (!_a.Any()) return;
		_aUndo = _a;
		_a = new();
		_keyToText.Clear();
		_list.Items.Clear();
	}

	void _Undo() {
		if (_a.Any()) {
			int i = _a.Count - 1;
			if (_a[i] is _RecoKey k && k.down < 0) { int j = _FindModDown(k.key, i); if (j >= 0) i = j; } //if last is Mod*up, remove all starting from Mod*down
			for (int u = _a.Count; --u >= i;) _list.Items.RemoveAt(u);
			_a.RemoveRange(i, _a.Count - i);
		} else if (_aUndo != null) {
			_a = _aUndo;
			_aUndo = null;
			foreach (var r in _a) _list.Items.Add(_NewListItem(r));
			_scroller.ScrollToEnd();
		}
	}

	void _Add(_Reco r, int time, wnd w, wnd wTL = default) {
		r.w = w;
		r.time = time != 0 ? time : Environment.TickCount;
		int varName = _AddWinIfNeed(r, w, wTL);

		if (r is _RecoMouse m) {
			m.varName = varName;
			var p = m.p;
			if (_xyIn != c_xyScreen && w.MapScreenToClient(ref p)) m.pw = p;
		}

		_a.Add(r);
		_list.Items.Add(_NewListItem(r));
		_scroller.ScrollToEnd();
		_canMove = true;
	}

	static ListBoxItem _NewListItem(_Reco r) {
		var v = new ListBoxItem { Content = r.ToString() };
		var b = r switch { _RecoMouse => Brushes.Blue, _RecoKey or _RecoChar { alt: true } => Brushes.Green, _RecoChar => Brushes.DarkOrange, _ => null };
		if (b != null) v.Foreground = b;
		return v;
	}

	int _AddWinIfNeed(_Reco r, wnd w, wnd wTL) { //if _RecoMouse, w may be control, and wTL is always the top-level window; else w is top-level window, and wTL not used
		if (r is not (_RecoKey or _RecoChar or _RecoMouse) || w.Is0) return 0;
		wnd wChild = default;
		bool activate = false;
		if (r is _RecoMouse) { //button, wheel or move
			if (_xyIn == c_xyScreen || (r is _RecoMouseMoveBy) || (r is _RecoMouseWheel mw && !mw.move)) return 0;
			if (r is _RecoMouseButton mb && mb.down < 0 && _Last is _RecoMouseMoveBy mm && mm.drag && _a[_a.Count - 2] is _RecoMouseButton mb2 && mb2.down > 0 && mb2.w == w) { mb.noXY = true; return 0; } //if was drag, just release button (no window, x, y)
			if (wTL != w) { wChild = w; w = wTL; }
			activate = (r is not _RecoMouseButton || !_a.Any()) && w.IsActive;
		} else { //key or char
			activate = true;
		}
		if (activate && _IsWinActivated(w)) activate = false;

		int varName = _AddWin(w, activate, r is not _RecoMouse, r.time);

		if (!wChild.Is0) {
			var rc = _FindWinChild(wChild);
			if (rc == null) {
				int vi = (_LastOfType<_RecoWinChild>()?.varName ?? 0) + 1;
				var z = new _RecoWinChild { varName = vi, windowVarName = varName };
				_Add(z, r.time, wChild);
				Task.Run(() => z.FormatCode(w)).Wait(30); //may use ***elmName (slow and may fail in hook proc)
				varName = -vi;
			} else {
				varName = -rc.varName;
			}
		}

		return varName;
	}

	/// <summary>
	/// If w is new, adds wnd.find and optionally Activate.
	/// Else if name changed, adds w.WaitForName and optionally Activate.
	/// Else if activate, adds Activate.
	/// Else just returns 0.
	/// If onKey, does nothing if !activate and name not changed.
	/// </summary>
	/// <returns>Variable name.</returns>
	int _AddWin(wnd w, bool activate, bool onKey = false, int time = 0) {
		var name = w.Name;
		_RecoWin g = null;
		var rw = _FindWinFind(w);
		int varName = rw?.varName ?? 0;
		if (rw == null) { //find
			if (!activate && onKey) return 0;
			varName = (_LastOfType<_RecoWinFind>()?.varName ?? 0) + 1;
			var z = new _RecoWinFind { w = w, name = name, varName = varName, activate = activate, waitS = _a.Any() ? 10 : 0 };
			int owner = 0; if (!w.Get.Owner.Is0 || w.IsTopmost) owner = _FindWinFind(o => w.IsOwnedBy(o, 2))?.varName ?? 0;
			z.FormatCode(owner);
			g = z;
		} else if (name.Trim('*') != rw.name.Trim('*')) { //wait for new name
			rw.name = name;
			g = new _RecoWin { activate = activate, varName = rw.varName, waitS = 30 };
			g.FormatCode(name);
		} else if (activate) { //just activate
			g = new _RecoWin { activate = true, varName = rw.varName };
			g.FormatCode();
		} else {
			if (onKey) return 0;
		}
		if (g != null) _Add(g, time, w);
		return varName;
	}

	void _Remove(Index i) {
		int j = i.GetOffset(_a.Count);
		_a.RemoveAt(j);
		_list.Items.RemoveAt(j);
	}

	/// <summary>
	/// Redraws _list.Items[i]. If r not null, replaces _a[i].
	/// </summary>
	void _Replace(Index i, _Reco r = null) {
		int j = i.GetOffset(_a.Count);
		if (r != null) _a[j] = r; else r = _a[j];
		_list.Items[j] = _NewListItem(r);
	}

	_Reco _Last => _a.Count > 0 ? _a[^1] : null;

	T _LastOfType<T>() where T : _Reco {
		for (int i = _a.Count; --i >= 0;) if (_a[i] is T r) return r;
		return null;
	}

	/// <summary>
	/// Finds the last modifier down event for modKey key. Searches in _a from i-1, reverse.
	/// Returns -1 if not found or if found modKey non-down event.
	/// </summary>
	int _FindModDown(KKey modKey, int i) {
		while (--i >= 0) if (_a[i] is _RecoKey rk && rk.key == modKey) return rk.down > 0 ? i : -1;
		return -1;
	}

	_RecoWinFind _FindWinFind(wnd w) {
		for (int i = _a.Count; --i >= 0;) if (_a[i] is _RecoWinFind f && f.w == w) return f;
		return null;
	}

	_RecoWinFind _FindWinFind(Func<wnd, bool> func) {
		for (int i = _a.Count; --i >= 0;) if (_a[i] is _RecoWinFind f && func(f.w)) return f;
		return null;
	}

	_RecoWinChild _FindWinChild(wnd w) {
		for (int i = _a.Count; --i >= 0;) if (_a[i] is _RecoWinChild f && f.w == w) return f;
		return null;
	}

	bool _IsWinActivated(wnd w) {
		for (int i = _a.Count; --i >= 0;) {
			switch (_a[i]) {
			case _RecoWin r:
				if (r.activate) return r.w == w;
				break;
			case _RecoMouseButton r:
				if (r.activated) return r.varName > 0 ? r.w == w : r.w.Window == w;
				break;
			}
		}
		return false;
	}

	KeyToTextConverter _keyToText = new();

	void _KeyHook(HookData.Keyboard k) {
		if (_paused || !_recordKeys) return;
		var w = wnd.active;
		if (w == _wThis) return;

		if (k.Mod != 0) {
			if (k.IsUp) {
				if (!keys.isPressed(k.Key)) return; //eg Ctrl up when switching windows with different keyboard layouts
				if (_RemoveMod(k.Key, k.Mod)) return;
			} else {
				if (keys.isPressed(k.Key)) return; //auto-repeated. If AltGr, auto-repeats pairs, like Alt Ctrl Alt Ctrl.
			}
			_Add(new _RecoKey { key = k.Key, down = (sbyte)(k.IsUp ? -1 : 1), count = 1 }, k.time, w);
			return;
		}

		if (k.IsUp) return;

		if (k.Key == KKey.Packet) {
			if (!_recordText) return;
			_Add(new _RecoChar { c = (char)k.scanCode, count = 1 }, k.time, w);
			return;
		}

		if (_recordText && _Text()) return;

		if (_Last is _RecoKey rk && rk.key == k.Key && rk.mod == 0 && rk.down == 0 && rk.count < 1000) { //*count
			rk.count++;
			_Replace(^1);
			return;
		}

		_Add(new _RecoKey { key = k.Key, count = 1 }, k.time, w);

		bool _Text() {
			var mod = keys.getMod();
			if (!KeyToTextConverter.IsPossiblyChar_(mod, k.Key) || k.Key is KKey.Enter or KKey.Tab) return false;
			var wf = wnd.focused; if (wf.Is0) wf = w;
			if (!_keyToText.Convert(out var t, k.Key, k.scanCode, mod, wf.ThreadId)) return false;
			if (t == default) return true; //dead-key
			if (t.c == ' ' && k.Key == KKey.Space && _Last is not _RecoChar) return false; //record Space as key if it isn't followed by a char
			var mod2 = mod & ~KMod.Shift;
			if (!_recordText2 && mod2 == (KMod.Alt | KMod.Ctrl)) if (!keys.isPressed(KKey.RAlt) || keys.isPressed(KKey.RCtrl)) return false; //AltGr = RAlt+LCtrl
			bool alt = mod2 == KMod.Alt;

			if (_a.Count > 0 && t.c != default) { //*count
				if (_IsSame(^1)) {
					var rc = _Last as _RecoChar;
					if (rc.count > 1) {
						rc.count++;
						_Replace(^1);
						return true;
					} else if (_a.Count > 3 && _IsSame(^2) && _IsSame(^3) && _IsSame(^4)) {
						_Remove(^2); _Remove(^2); _Remove(^2);
						rc.count = 5;
						_Replace(^1);
						return true;
					}
				}
				bool _IsSame(Index i) {
					return _a[i] is _RecoChar rc && rc.c == t.c && rc.alt == alt && rc.count < 1000;
				}
			}

			_Add(new _RecoChar { c = t.c, s = t.s, alt = alt, count = 1 }, k.time, w);

			//why Text+ checkbox exists when we can detect AltGr:
			//	1. OS bugs. Eg AltGr stops working after an Open/Save dialog etc.
			//	2. Possibly not all keyboards have RAlt.
			//	3. Somebody may want to use Ctrl+Alt even if AltGr exists and works.
			//	4. There must be a reason why Alt+Ctrl is an alternative for AltGr.
			return true;
		}
	}

	/// <summary>
	/// On modifier up converts sequence like {Ctrl*down, Alt*down, K, Alt*up, Ctrl*Up} to {Ctrl+Alt+K} or {text}.
	/// If returns false, the caller does not record the Mod*up event.
	/// </summary>
	bool _RemoveMod(KKey key, KMod mod) {
		//print.it("----", key, mod);
		//print.it(_a);
		int nKeys = 0, nChars = 0, iKey = -1;
		for (int i = _a.Count; --i >= 0;) {
			switch (_a[i]) {
			case _RecoKey d:
				//print.it(d);
				if (d.key == key) {
					if (d.down != 1) return false; //Mod*up without Mod*down
					if (nKeys + nChars == 0) { //modifier(s) pressed-released without a key etc in between. Replace Mod*down with Mod.
						if (i == _a.Count - 1) {
							d.down = 0;
							_Replace(i);
						} else {
							for (int j = _a.Count; --j > i;) if (_a[j] is _RecoKey u && u.down != -1) _AddMod(j);
							_Remove(i);
						}
					} else { //remove Mod*down if Mod+key or Mod+characters
						if (nKeys > 0) { //else all characters
							if (nKeys + nChars > 1) { //Mod + multiple keys or Mod + keys and chars. Now display {Mod*down, A, B, Mod*up}. Later will convert to {Mod+(A B)}.
								if (mod == KMod.Alt) while (++i < _a.Count && _a[i] is _RecoChar rc) { rc.alt = false; _Replace(i); }
								return false;
							}
							if (nKeys == 1 && iKey >= 0) _AddMod(iKey); //Mod + key
						}
						_Remove(i);
					}
					return true;
				}
				if (d.down == 0) {
					nKeys++; iKey = i;
				}
				break;
			case _RecoChar d:
				if (d.alt) nKeys++; else nChars++;
				break;
			default: return false;
			}
		}
		return false;

		void _AddMod(int i) {
			var r = _a[i] as _RecoKey;
			if (r.key != key) r.mod |= mod;
			_Replace(i);
		}
	}

	void _MouseHook(HookData.Mouse k) {
		//deactivate this window when mouse leaves it
		var wa = wnd.active;
		if (k.IsMove) {
			if (wa != _wThis) _wFore = wa;
			else if (_wFore.IsVisible && !_wThis.Rect.Contains(k.pt) && Api.GetCapture().Is0) {
				Api.SetForegroundWindow(_wFore);
				_wFore = default;
				_canMove = false; //don't record mouse move until something new recorded
			}
		}

		if (_paused || !_recordMouse || wa == _wThis) return;

		if (k.IsButton) {
			//using var p1=perf.local();

			if (!_WinFromXY(out wnd w, out wnd wTL)) return;

			if (k.IsButtonUp) {
				if (_Last is _RecoMouseButton m && m.button == k.Button && m.down > 0 && !_PointIsDrag(m.p, k.pt)) {
					if (_a.Count > 1 && _a[^2] is _RecoMouseButton u && u.button == m.button && u.down == 0 && !u.two && _PointIsDouble(u.p, m.p) && m.time - u.time <= Api.GetDoubleClickTime()) {
						u.two = true;
						_Remove(^1);
					} else {
						m.down = 0;

						//if clicked a taskbar and it activates a window, replace the mouse.click with wnd.Activate()
						if (m.button == MButton.Left && wTL.ClassNameIs("Shell_*TrayWnd") && wTL.ProgramName.Eqi("explorer.exe")) {
							timer.after(25, _ => {
								var w1 = wnd.active;
								if (w1 != wTL && !w1.Is0 && _Last == m) {
									_Remove(^1); //remove mouse.click(w)
									if (_Last is _RecoWinChild rc && rc.w == w) _Remove(^1); //remove wnd.Child(w)
									if (_Last is _RecoWin rw && rw.w == wTL) _Remove(^1); //remove wnd.find(wTL)
									_AddWin(w1, true);
								}
							});
						}
					}
					_Replace(^1);
					return;
				}
			}
			var r = new _RecoMouseButton { button = k.Button, down = (sbyte)(k.IsButtonDown ? 1 : -1), p = k.pt };
			_Add(r, k.time, w, wTL);

			if (k.IsButtonDown) {
				//don't record w.Activate() on next key etc if this click activates w
				var ww = w.Window;
				if (!ww.Is0 && !ww.IsActive) {
					timer.after(10, _ => r.activated = ww.IsActive);
				}

				//comments
				//p1.Next();
				var p = k.pt;
				var ta = new Task[2] {
					Task.Run(() => r.image=TUtil.MakeScreenshot(p)),
					Task.Run(_Comment)
				};
				Task.WaitAll(ta, Math.Clamp(WindowsHook.LowLevelHooksTimeout - 150, 30, 150));
				void _Comment() {
					try {
						var e = elm.fromXY(p, EXYFlags.PreferLink /*| EXYFlags.NotInProc*/); //with NotInProc in some cases works not as good, eg no Name of 32-bit classic toolbar buttons
						var role = e?.Role; if (role.NE()) return;
						var name = e.Name;
						if (!name.NE()) name = name.Escape(limit: 30, quote: true);
						else if (role is "CLIENT" or "WINDOW") return;
						r.comment = $" /* {role.Lower()} {name} */";
					}
					catch (Exception e1) { Debug_.Print(e1); }
				}
				//CONSIDER: also screenshot on button up, if not joined with down and not in same point in window.
				//	But then usually records single line (mouse.drag).
			}
		} else if (k.IsWheel) {
			if (!_recordWheel) return;
			if (!_WinFromXY(out wnd w, out wnd wTL)) return;

			var rm = _LastOfType<_RecoMouse>();
			bool move = rm == null || rm.p != k.pt || rm is _RecoMouseMoveBy;
			_Add(new _RecoMouseWheel { ticks = k.WheelValue, p = k.pt, move = move }, k.time, w, wTL);
		} else if (_canMove) { //IsMove
			bool drag = mouse.isPressed(MButtons.Left | MButtons.Right | MButtons.Middle);
			if (!(drag ? _recordDrag : _recordMove)) return;
			if (_a.LastOrDefault(o => o is _RecoMouse) is _RecoMouse rm) {
				if (rm.p == k.pt) return;
				if (_Last is _RecoMouseMoveBy mm) { //add to the mouse.moveBy
					mm.a.Add((uint)Math2.MakeLparam(k.pt.x - mm.p.x, k.pt.y - mm.p.y));
					mm.p = k.pt;
				} else { //start mouse.moveBy
					_Add(new _RecoMouseMoveBy { a = new() { (uint)Math2.MakeLparam(k.pt.x - rm.p.x, k.pt.y - rm.p.y) }, p = k.pt, drag = drag }, k.time, default);
				}
			} else { //this is the first mouse event, therefore before mouse.moveBy need mouse.move(x, y)
				if (!_WinFromXY(out wnd w, out wnd wTL)) return;
				_Add(new _RecoMouse { p = k.pt }, k.time, w, wTL);
			}
		}

		bool _WinFromXY(out wnd w, out wnd wTL) {
			bool ctl = _xyIn == c_xyControl;
			w = wnd.fromXY(k.pt, ctl ? 0 : WXYFlags.NeedWindow);
			wTL = ctl ? w.Window : w;
			if (wTL == _wThis || wTL.Is0) { w = wTL = default; return false; }
			return true;
		}

		//tested: SM_CXDOUBLECLK=4 and SM_CXDRAG=4 regardless of DPI (using Dpi.GetSystemMetrics), and can't be changed in Control Panel etc.
		//static bool _PointIsDrag(POINT p1, POINT p2, wnd w) {
		//	if(p1==p2) return false;
		//	int dpi=Dpi.OfWindow(w);
		//	return Math.Abs(p1.x-p2.x)>=Dpi.GetSystemMetrics(Api.SM_CXDRAG, dpi) || Math.Abs(p1.y-p2.y)>=Dpi.GetSystemMetrics(Api.SM_CYDRAG, dpi);
		//}
		static bool _PointIsDrag(POINT a, POINT b) => Math.Abs(a.x - b.x) >= 4 || Math.Abs(a.y - b.y) >= 4;

		static bool _PointIsDouble(POINT a, POINT b) => Math.Abs(a.x - b.x) <= 2 && Math.Abs(a.y - b.y) <= 2;
	}

	//void _WeHook(HookData.WinEvent k) {
	//	if (_paused) return;
	//	switch (k.event_) {
	//	case EEvent.OBJECT_SHOW:
	//		if (k.idObject != EObjid.WINDOW || k.idChild != 0 || k.w.IsChild) break;
	//		//	print.it(k.idObject, k.idChild, k.w);
	//		var w = k.w;
	//		break;
	//	case EEvent.OBJECT_HIDE:

	//		break;
	//		//default:
	//		//	print.it(k.event_);
	//		//	break;
	//	}
	//}

	string _GetCode() {
		if (_Last is _RecoMouseMoveBy) _a.RemoveAt(_a.Count - 1);
		if (!_a.Any()) return null;
		var b = new StringBuilder();
		string speed = _tSpeed.TextOrNull();
		if (speed != null) b.Append("using (opt.scope.all()) ");
		b.AppendLine("{ //recorded");
		if (speed != null) b.AppendLine($"opt.mouse.MoveSpeed = opt.key.KeySpeed = opt.key.TextSpeed = {speed};");
		//bool addEmptyLine = false;
		for (int i = 0, n = _a.Count; i < n; i++) {
			////rejected: if mouse button with screenshot, add empty lines before and after, and add comment and screenshot before. Better just add empty line after.
			//if(_a[i] is _RecoMouseButton mb0 && mb0.image != null) {
			//	b.AppendLine().Append(mb0.comment, 1, mb0.comment.Length - 1).AppendLine(mb0.image);
			//	addEmptyLine = true;
			//} else if (addEmptyLine) {
			//	if (_a[i] is not _RecoSleep) b.AppendLine();
			//	addEmptyLine = false;
			//}

			switch (_a[i]) {
			case _RecoWin r: //and _RecoWinFind
				b.Append(r.code);
				break;
			case _RecoWinChild r:
				b.Append(r.code);
				break;
			case _RecoMouse r:
				//if drag in same window, format mouse.drag relative
				if (r is _RecoMouseButton mb && mb.down > 0 && i < n - 1) {
					if (_a[i + 1] is _RecoMouseButton mb2 && mb2.down < 0 && !mb2.noXY && mb2.w == mb.w) {
						mb.FormatDrag(b, (mb2.p.x - mb.p.x, mb2.p.y - mb.p.y));
						i++;
						goto g1;
					} else if (_a[i + 1] is _RecoMouseMoveBy mm && _a[i + 2] is _RecoMouseButton mb3 && mb3.down < 0 && mb3.noXY) {
						mb.FormatDrag(b, mm.OffsetsString);
						i += 2;
						goto g1;
					}
				}
				r.FormatCode(b);
			g1:
				//if (r is _RecoMouseButton mbb && mbb.image == null) b.Append(mbb.comment);
				if (r is _RecoMouseButton mbb) {
					b.Append(mbb.comment);
					if (mbb.image != null) {
						b.Append(mbb.image);
						if (i < n - 1) b.AppendLine(); //make space for image
					}
				}
				break;
			case _RecoKey or _RecoChar:
				b.Append("keys.send(\"");
				for (int prev = 0; i < n; i++) { //prev: 0 none, 1 keys, 2 chars
					var r = _a[i];
					if (r is _RecoKey rk) {
						if (prev == 1) b.Append(' '); else if (prev == 2) b.Append("\", \"");
						prev = 1;

						//Mod*down A B Mod*up -> Mod+(A B)
						//	FUTURE: keys.send("Ctrl+", click);
						if (rk.down > 0) {
							KMod modDown = 0, modUp = 0;
							int j = i;
							for (; j < n && _a[j] is _RecoKey k2 && k2.down > 0; j++) modDown |= _KeyToMod(k2.key);
							if (modDown != 0) {
								int iKeys = j;
								while (j < n && _a[j] is _RecoKey { down: 0, mod: 0 } or _RecoChar { s: null }) j++;
								int iModUp = j;
								for (; j < n && _a[j] is _RecoKey k2 && k2.down < 0; j++) modUp |= _KeyToMod(k2.key);
								if (modUp == modDown) {
									keys.more.hotkeyToString(b, modDown, 0);
									b.Append('(');
									for (i = iKeys; i < iModUp; i++) {
										switch (_a[i]) {
										case _RecoKey k:
											if (i > iKeys) b.Append(' ');
											b.Append(k);
											break;
										case _RecoChar k:
											if (i > iKeys && _a[i - 1] is _RecoKey) b.Append(' ');
											b.Append('_');
											k.FormatCodeSimple(b);
											break;
										}
									}
									b.Append(')');
									i = j - 1;
									continue;
								}
							}

							static KMod _KeyToMod(KKey k) => k switch { KKey.Ctrl => KMod.Ctrl, KKey.Shift => KMod.Shift, KKey.Alt => KMod.Alt, KKey.Win => KMod.Win, _ => 0 };
						}

						b.Append(rk);
					} else if (r is _RecoChar rc) {
						if (prev == 1) b.Append(' ');
						rc.FormatCode(b, prev != 2);
						prev = rc.count > 1 ? 1 : 2;
					} else break;
				}
				b.Append("\");");
				i--;
				break;
			case _RecoSleep r:
				break;
			}
			if (i < n - 1) b.AppendLine();
		}
		//if (speed != null)
		b.Replace("\n", "\n\t");
		b.Append("\r\n}");

		return b.ToString();
	}

	void _OK(bool copy) {
		Close();
		var s = _GetCode(); if (s.NE()) return;
		if (copy) {
			clipboard.text = s;
		} else {
#if SCRIPT
			print.it("<><code>" + s + "</code>");
			//ScriptEditor.Open("recorded.cs");
			//keys.send("Ctrl+End");
			//clipboard.paste(s);
#else
			//InsertCode.Statements(s); //flickers
			//timer.after(100, _ => InsertCode.Statements(s));
			Dispatcher.InvokeAsync(() => InsertCode.Statements(s), DispatcherPriority.ApplicationIdle); //35 ms
#endif
		}
	}
}

