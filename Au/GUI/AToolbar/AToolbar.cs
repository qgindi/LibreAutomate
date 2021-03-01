using Au.Types;
using Au.Util;
using Au.Triggers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Au
{

	/// <summary>
	/// Floating toolbar.
	/// Can be attached to windows of other programs.
	/// </summary>
	/// <remarks>
	/// To create toolbar code can be used snippet toolbarSnippet. In code editor type "toolbar" and selext the snippet from the list.
	/// 
	/// Not thread-safe. All functions must be called from the same thread that created the <b>AToolbar</b> object, except where documented otherwise. Note: item actions by default run in other threads; see <see cref="MTBase.ActionThread"/>.
	/// </remarks>
	public partial class AToolbar : MTBase
	{
		AWnd _w;
		readonly _Settings _sett;
		readonly string _name;
		readonly List<ToolbarItem> _a = new();
		bool _created;
		bool _closed;
		bool _topmost; //no owner, or owner is topmost
		readonly int _threadId;
		double _dpiF; //DPI scaling factor, eg 1.5 for 144 dpi

		static int s_treadId;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name">
		/// Toolbar name. Must be valid filename.
		/// Used for toolbar window name and for settings file name. Also used by <see cref="Find"/> and some other functions.
		/// </param>
		/// <param name="flags"></param>
		/// <param name="f"><see cref="CallerFilePathAttribute"/></param>
		/// <param name="l"><see cref="CallerLineNumberAttribute"/></param>
		/// <exception cref="ArgumentException">Empty or invalid name.</exception>
		/// <remarks>
		/// Each toolbar has a settings file, where are saved its position, size and context menu settings. This function reads the file if exists, ie if settings changed in the past. See <see cref="GetSettingsFilePath"/>. If fails, writes warning to the output and uses default settings.
		/// 
		/// Sets properties:
		/// - <see cref="MTBase.ActionThread"/> = true.
		/// - <see cref="MTBase.ExtractIconPathFromCode"/> = true.
		/// </remarks>
		public AToolbar(string name, TBCtor flags = 0, [CallerFilePath] string f = null, [CallerLineNumber] int l = 0) : base(name, f, l) {
			_threadId = AThread.Id;
			if (s_treadId == 0) s_treadId = _threadId; else if (_threadId != s_treadId) AWarning.Write("All toolbars should be in single thread. Multiple threads use more CPU. If using triggers, insert this code before adding toolbar triggers: <code>Triggers.Options.ThreadMain();</code>");

			//rejected: [CallerMemberName] string name = null. Problem: if local func or lambda, it is parent method's name. And can be eg ".ctor" if directly in script.
			_name = name;
			var path = flags.Has(TBCtor.DontSaveSettings) ? null : GetSettingsFilePath(name);
			_sett = _Settings.Load(path, flags.Has(TBCtor.ResetSettings));

			_offsets = _sett.offsets; //SHOULDDO: don't use saved offsets if this toolbar was free and now is owned or vice versa. Because the position is irrelevent and may be far/offscreen. It usually happens when testing.

			ActionThread = true;
			ExtractIconPathFromCode = true;
		}

		/// <summary>
		/// Gets the toolbar window.
		/// </summary>
		public AWnd Hwnd => _w;

		/// <summary>
		/// Returns true if the toolbar is open. False if closed or <see cref="Show"/> still not called.
		/// </summary>
		public bool IsAlive => _created && !_closed;

		/// <summary>
		/// Gets the name of the toolbar.
		/// </summary>
		public string Name => _name;

		///
		public override string ToString() => _IsSatellite ? "    " + Name : Name; //the indentation is for the list in the Toolbars dialog

		/// <summary>
		/// True if this toolbar started with default settings. False if loaded saved settings from file.
		/// </summary>
		/// <seealso cref="GetSettingsFilePath"/>
		/// <seealso cref="TBCtor"/>
		public bool IsFresh => !_sett.LoadedFile;

		#region static functions

		/// <summary>
		/// Gets full path of toolbar's settings file. The file may exist or not.
		/// </summary>
		/// <param name="toolbarName">Toolbar name.</param>
		/// <remarks>
		/// Path: <c>AFolders.Workspace + $@"\.toolbars\{toolbarName}.json"</c>. If <see cref="AFolders.Workspace"/> is null, uses <see cref="AFolders.ThisAppDocuments"/>.
		/// </remarks>
		public static string GetSettingsFilePath(string toolbarName) {
			if (toolbarName.NE()) throw new ArgumentException("Empty name");
			string s = AFolders.Workspace; if (s == null) s = AFolders.ThisAppDocuments;
			return s + @"\.toolbars\" + toolbarName + ".json";
		}

		/// <summary>
		/// Finds an open toolbar by <see cref="Name"/>.
		/// Returns null if not found or closed or never shown (<see cref="Show"/> not called).
		/// </summary>
		/// <remarks>
		/// Finds only toolbars created in the same script and thread.
		/// 
		/// Does not find satellite toolbars. Use this code: <c>AToolbar.Find("owner toolbar").Satellite</c>
		/// </remarks>
		public static AToolbar Find(string name) => _Manager._atb.Find(o => o.Name == name);

		#endregion

		#region add item

		void _Add(ToolbarItem item, string text, Delegate click, MTImage image, int l) {
			item.Set_(this, text, click, image, l);
			_a.Add(item);
			Last = item;
			if (ButtonAdded != null && !item.IsSeparatorOrGroup_) ButtonAdded(item);
		}

		/// <summary>
		/// Adds button.
		/// Same as <see cref="this[string, MTImage, int]"/>.
		/// </summary>
		/// <param name="text">Text. Or "Text\0 Tooltip".</param>
		/// <param name="click">Action called when the button clicked.</param>
		/// <param name="image"></param>
		/// <param name="l"><see cref="CallerLineNumberAttribute"/></param>
		/// <remarks>
		/// More properties can be specified later (set properties of the returned <see cref="ToolbarItem"/>) or before (set <see cref="MTBase.ActionThread"/>, <see cref="MTBase.ActionException"/>, <see cref="MTBase.ExtractIconPathFromCode"/>, <see cref="ButtonAdded"/>).
		/// </remarks>
		public ToolbarItem Add(string text, Action<ToolbarItem> click, MTImage image = default, [CallerLineNumber] int l = 0) {
			var item = new ToolbarItem();
			_Add(item, text, click, image, l);
			return item;
		}

		/// <summary>
		/// Adds button.
		/// Same as <see cref="Add(string, Action{ToolbarItem}, MTImage, int)"/>.
		/// </summary>
		/// <param name="text">Text. Or "Text\0 Tooltip".</param>
		/// <param name="image"></param>
		/// <param name="l"><see cref="CallerLineNumberAttribute"/></param>
		/// <value>Action called when the button clicked.</value>
		/// <remarks>
		/// More properties can be specified later (set properties of <see cref="Last"/> <see cref="ToolbarItem"/>) or before (set <see cref="MTBase.ActionThread"/>, <see cref="MTBase.ActionException"/>, <see cref="MTBase.ExtractIconPathFromCode"/>, <see cref="ButtonAdded"/>).
		/// </remarks>
		/// <example>
		/// These two are the same.
		/// <code><![CDATA[
		/// tb.Add("Example", o => AOutput.Write(o));
		/// tb["Example"] = o => AOutput.Write(o);
		/// ]]></code>
		/// These four are the same.
		/// <code><![CDATA[
		/// var b = tb.Add("Example", o => AOutput.Write(o)); b.Tooltip="tt";
		/// tb.Add("Example", o => AOutput.Write(o)).Tooltip="tt";
		/// tb["Example"] = o => AOutput.Write(o); var b=tb.Last; b.Tooltip="tt";
		/// tb["Example"] = o => AOutput.Write(o); tb.Last.Tooltip="tt";
		/// ]]></code>
		/// </example>
		public Action<ToolbarItem> this[string text, MTImage image = default, [CallerLineNumber] int l = 0] {
			set { Add(text, value, image, l); }
		}

		//CONSIDER: AddCheck, AddRadio.

		/// <summary>
		/// Adds button with drop-down menu.
		/// </summary>
		/// <param name="text">Text. Or "Text\0 Tooltip".</param>
		/// <param name="menu">Action that adds menu items. Called whenever the button clicked.</param>
		/// <param name="image"></param>
		/// <param name="l"><see cref="CallerLineNumberAttribute"/></param>
		/// <example>
		/// <code><![CDATA[
		/// tb.Menu("Menu", m => {
		/// 	m["M1"]=o=>AOutput.Write(o);
		/// 	m["M2"]=o=>AOutput.Write(o);
		/// });
		/// ]]></code>
		/// </example>
		public ToolbarItem Menu(string text, Action<AMenu> menu, MTImage image = default, [CallerLineNumber] int l = 0) {
			var item = new ToolbarItem { type = TBItemType.Menu };
			_Add(item, text, menu, image, l);
			return item;
		}

		/// <summary>
		/// Adds button with drop-down menu.
		/// </summary>
		/// <param name="text">Text. Or "Text\0 Tooltip".</param>
		/// <param name="menu">Action that returns the menu. Called whenever the button clicked.</param>
		/// <param name="image"></param>
		/// <param name="l"><see cref="CallerLineNumberAttribute"/></param>
		/// <example>
		/// <code><![CDATA[
		/// var m = new AMenu(); m.AddCheck("C1"); m.AddCheck("C2");
		/// t.Menu("", () => m);
		/// ]]></code>
		/// </example>
		public ToolbarItem Menu(string text, Func<AMenu> menu, MTImage image = default, [CallerLineNumber] int l = 0) {
			var item = new ToolbarItem { type = TBItemType.Menu };
			_Add(item, text, menu, image, l);
			return item;
		}

		/// <summary>
		/// Adds new vertical separator. Horizontal if vertical toolbar.
		/// </summary>
		public ToolbarItem Separator() {
			int i = _a.Count - 1;
			if (i < 0 || _a[i].IsGroup_) throw new InvalidOperationException("first item is separator");
			var item = new ToolbarItem { type = TBItemType.Separator };
			_Add(item, null, null, default, 0);
			return item;
		}

		/// <summary>
		/// Adds new horizontal separator, optionally with text.
		/// </summary>
		/// <param name="text">Text. Or "Text\0 Tooltip".</param>
		public ToolbarItem Group(string text = null) {
			var item = new ToolbarItem { type = TBItemType.Group };
			_Add(item, text, null, default, 0);
			return item;
		}

		/// <summary>
		/// Gets the last added item.
		/// </summary>
		public ToolbarItem Last { get; private set; }

		/// <summary>
		/// When added a button.
		/// </summary>
		/// <remarks>
		/// Allows to set item properties in single place instead of after each 'add button' code line.
		/// For example, the event handler can set item properties common to all buttons.
		/// Not used for separators and group separators.
		/// </remarks>
		public event Action<ToolbarItem> ButtonAdded;

		#endregion

		#region show, close, owner

		/// <summary>
		/// Shows the toolbar.
		/// </summary>
		/// <param name="screen">
		/// Attach to this screen. For example a screen index (0 the primary, 1 the first non-primary, and so on). Example: <c>AScreen.Index(1)</c>.
		/// If not specified, the toolbar will be attached to the screen where it is now or where will be moved later.
		/// Don't use this parameter if this toolbar was created by <see cref="AutoHideScreenEdge"/>, because then screen is already known.
		/// </param>
		/// <exception cref="ArgumentException">The toolbar was created by <b>AutoHideScreenEdge</b>, and now screen specified again.</exception>
		/// <exception cref="InvalidOperationException"><b>Show</b> already called.</exception>
		/// <remarks>
		/// The toolbar will be moved when the screen moved or resized.
		/// </remarks>
		public void Show(AScreen screen = default) {
			if (!_screenAHSE.IsEmpty) { if (screen.IsEmpty) screen = _screenAHSE; else throw new ArgumentException(); }
			_Show(false, default, null, screen);
		}

		/// <summary>
		/// Shows the toolbar and attaches to a window.
		/// </summary>
		/// <param name="ownerWindow">Window or control. Can belong to any process.</param>
		/// <param name="clientArea">Let the toolbar position be relative to the client area of the window.</param>
		/// <exception cref="InvalidOperationException"><b>Show</b> already called.</exception>
		/// <exception cref="ArgumentException"><b>ownerWindow</b> is 0.</exception>
		/// <remarks>
		/// The toolbar will be above the window in the Z order; moved when the window moved or resized; hidden when the window hidden, cloaked or minimized; destroyed when the window destroyed.
		/// </remarks>
		public void Show(AWnd ownerWindow, bool clientArea = false) {
			_followClientArea = clientArea;
			_Show(true, ownerWindow, null, default);
		}

		/// <summary>
		/// Shows the toolbar and attaches to an object in a window.
		/// </summary>
		/// <param name="ownerWindow">Window that contains the object. Can be control. Can belong to any process.</param>
		/// <param name="oo">A variable of a user-defined class that implements <see cref="ITBOwnerObject"/> interface. It provides object location, visibility, etc.</param>
		/// <exception cref="InvalidOperationException"><b>Show</b> already called.</exception>
		/// <exception cref="ArgumentException"><b>ownerWindow</b> is 0.</exception>
		/// <remarks>
		/// The toolbar will be above the window in the Z order; moved when the object or window moved or resized; hidden when the object or window hidden, cloaked or minimized; destroyed when the object or window destroyed.
		/// </remarks>
		public void Show(AWnd ownerWindow, ITBOwnerObject oo) => _Show(true, ownerWindow, oo, default);

		/// <summary>
		/// Shows the toolbar.
		/// If ta is <b>WindowTriggerArgs</b>, attaches the toolbar to the trigger window.
		/// Else if ta != null, calls <see cref="TriggerArgs.DisableTriggerUntilClosed(AToolbar)"/>.
		/// </summary>
		public void Show(TriggerArgs ta) {
			if (ta is WindowTriggerArgs wta) {
				Show(wta.Window);
			} else {
				Show();
				ta?.DisableTriggerUntilClosed(this);
			}
		}

		//used for normal toolbars, not for satellite toolbars
		void _Show(bool owned, AWnd owner, ITBOwnerObject oo, AScreen screen) {
			_CheckThread();
			if (_created) throw new InvalidOperationException();

			AWnd c = default;
			if (owned) {
				if (owner.Is0) throw new ArgumentException();
				var w = owner.Window; if (w.Is0) return;
				if (w != owner) { c = owner; owner = w; }
			}

			_CreateWindow(owned, owner, screen);
			_Manager.Add(this, owner, c, oo);
		}

		//used for normal and satellite toolbars
		void _CreateWindow(bool owned, AWnd owner, AScreen screen = default) {
			_topmost = !owned || owner.IsTopmost;
			if (!owned || Anchor.OfScreen()) _os = new _OwnerScreen(this, screen);

			_RegisterWinclass();
			_SetDpi();
			_Images();
			_MeasureText();
			var size = _Measure();
			var style = WS.POPUP | WS.CLIPCHILDREN | _BorderStyle(_sett.border);
			var estyle = WS2.TOOLWINDOW | WS2.NOACTIVATE;
			if (_topmost) estyle |= WS2.TOPMOST;
			var r = owned ? owner.Rect : screen.Rect; //create in center of owner window or screen, to minimize possibility of DPI change when setting final position
			r = new(r.CenterX - size.width / 2, r.CenterY - size.height / 2, size.width, size.height);
			ADpi.AdjustWindowRectEx(_dpi, ref r, style, estyle);
			AWnd.More.CreateWindow(_WndProc, "AToolbar", _name, style, estyle, r.left, r.top, r.Width, r.Height);
			//		_w.ShowL(true);
			_created = true;
		}

		/// <summary>
		/// Destroys the toolbar window.
		/// </summary>
		public void Close() {
			Api.DestroyWindow(_w);
		}

		/// <summary>
		/// When the toolbar window destroyed.
		/// </summary>
		public event Action Closed;

		/// <summary>
		/// Adds or removes a reason to temporarily hide the toolbar. The toolbar is hidden if at least one reason exists. See also <seealso cref="Close"/>.
		/// </summary>
		/// <param name="hide">true to hide (add <i>reason</i>), false to show (remove <i>reason</i>).</param>
		/// <param name="reason">An user-defined reason to hide/unhide. Can be <see cref="TBHide.User"/> or a bigger value, eg (TBHide)0x20000, (TBHide)0x40000.</param>
		/// <exception cref="InvalidOperationException">
		/// - The toolbar was never shown (<see cref="Show"/> not called).
		/// - It is a satellite toolbar.
		/// - Wrong thread. Must be called from the same thread that created the toolbar. See <see cref="MTBase.ActionThread"/>.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException"><i>reason</i> is less than <see cref="TBHide.User"/>.</exception>
		/// <remarks>
		/// Toolbars are automatically hidden when the owner window is hidden, minimized, etc. This function can be used to hide toolbars for other reasons.
		/// </remarks>
		public void Hide(bool hide, TBHide reason) {
			_CheckThread();
			if (!_created || _IsSatellite) throw new InvalidOperationException();
			if (0 != ((int)reason & 0xffff)) throw new ArgumentOutOfRangeException();
			_SetVisible(!hide, reason);
		}

		/// <summary>
		/// Gets current reasons why the toolbar is hidden. Returns 0 if not hidden.
		/// </summary>
		/// <remarks>
		/// Not used with satellite toolbars.
		/// </remarks>
		public TBHide Hidden => _hide;

		void _SetVisible(bool show, TBHide reason) {
			//AOutput.Write(show, reason);
			if (show) {
				if (_hide == 0) return;
				_hide &= ~reason;
				if (_hide != 0) return;
			} else {
				var h = _hide;
				_hide |= reason;
				if (h != 0) return;
			}
			_SetVisibleL(show);
		}
		TBHide _hide;

		void _SetVisibleL(bool show) => _w.ShowL(show);

		/// <summary>
		/// Returns true if the toolbar is attached to a window or an object in a window.
		/// </summary>
		public bool IsOwned => _ow != null;

		/// <summary>
		/// Returns the owner top-level window.
		/// Returns default(AWnd) if the toolbar is not owned. See <see cref="IsOwned"/>.
		/// </summary>
		public AWnd OwnerWindow => _ow?.w ?? default;

		#endregion

		#region wndproc, context menu

		static void _RegisterWinclass() {
			if (0 == Interlocked.Exchange(ref s_winclassRegistered, 1)) {
				AWnd.More.RegisterWindowClass("AToolbar"/*, etc: new() { style = Api.CS_HREDRAW | Api.CS_VREDRAW, mCursor = MCursor.Arrow }*/);
			}
		}
		static int s_winclassRegistered;

		unsafe LPARAM _WndProc(AWnd w, int msg, LPARAM wParam, LPARAM lParam) {
			//		AWnd.More.PrintMsg(w, msg, wParam, lParam);

			switch (msg) {
			case Api.WM_LBUTTONDOWN:
			case Api.WM_RBUTTONDOWN:
			case Api.WM_MBUTTONDOWN:
				var tb1 = _SatPlanetOrThis;
				if (tb1.IsOwned && tb1.MiscFlags.Has(TBFlags.ActivateOwnerWindow)) {
					tb1.OwnerWindow.ActivateL();
					//never mind: sometimes flickers. Here tb1._Zorder() does not help. The OBJECT_REORDER hook zorders when need. This feature is rarely used.
				}
				break;
			case Api.WM_MOUSEMOVE:
			case Api.WM_NCMOUSEMOVE:
				_SatMouse();
				break;
			}

			switch (msg) {
			case Api.WM_NCCREATE:
				_w = w;
				ACursor.SetArrowCursor_();
				if (_transparency != default) _w.SetTransparency(true, _transparency.opacity, _transparency.colorKey);
				break;
			case Api.WM_NCDESTROY:
				_closed = true;
				_Manager.Remove(this);
				_SatDestroying();
				if (!_tt.tt.Is0) { Api.DestroyWindow(_tt.tt); _tt = default; } //maybe auto-destroyed because owned by this, but anyway
				_sett.Dispose();
				_w = default;
				Closed?.Invoke();

				//PROBLEM: not called if thread ends without closing the toolbar window.
				//	Then saves settings only on process exit. Not if process terminated, eg by the 'end task' command.
				//	In most cases it isn't a problem because saves settings every 2 s (IIRC).
				//	SHOULDDO: the 'end task' command should be more intelligent. At least save settings.
				break;
			case Api.WM_ERASEBKGND:
				return 0;
			case Api.WM_PAINT:
				//			APerf.First();
				using (ABufferedPaint bp = new(w, true)) {
					var dc = bp.DC;
					using var g = System.Drawing.Graphics.FromHdc(dc);
					_WmPaint(dc, g, bp.ClientRect, bp.UpdateRect);
				}
				//			APerf.NW();
				return default;
			case Api.WM_MOUSEACTIVATE:
				return Api.MA_NOACTIVATE;
			case Api.WM_MOUSEMOVE:
				_WmMousemove(lParam);
				return default;
			case Api.WM_MOUSELEAVE:
				_WmMouseleave();
				return default;
			case Api.WM_LBUTTONDOWN:
				_WmMouselbuttondown(lParam);
				return default;
			case Api.WM_CONTEXTMENU:
			case Api.WM_NCRBUTTONUP:
				_WmContextmenu();
				break;
			case Api.WM_NCHITTEST:
				if (_WmNchittest(lParam, out int hitTest)) return hitTest; //returns a hittest code to move or resize if need
				break;
			case Api.WM_NCLBUTTONDOWN:
				int ht = (int)wParam;
				if (ht == Api.HTCAPTION || (ht >= Api.HTSIZEFIRST && ht <= Api.HTSIZELAST)) {
					//workaround for: Windows tries to activate this window when moving or sizing it, unless this process is not allowed to activate windows.
					//	This window then may not become the foreground window, but it receives wm_activateapp, wm_activate, wm_setfocus, and is moved to the top of Z order.
					//	tested: LockSetForegroundWindow does not work.
					//	This code better would be under WM_SYSCOMMAND, but then works only when sizing. When moving, activates before WM_SYSCOMMAND.
					using (AHookWin.ThreadCbt(d => d.code == HookData.CbtEvent.ACTIVATE)) {
						//also let arrow keys move/resize by 1 pixel.
						//	In active window Ctrl+arrow work automatically, but toolbars aren't active.
						//	Never mind: does not work if active window has higher UAC IL. Even with Ctrl.
						using var k1 = new ARegisteredHotkey(); k1.Register(100, KKey.Left, _w);
						using var k2 = new ARegisteredHotkey(); k2.Register(101, KKey.Right, _w);
						using var k3 = new ARegisteredHotkey(); k3.Register(102, KKey.Up, _w);
						using var k4 = new ARegisteredHotkey(); k4.Register(103, KKey.Down, _w);

						Api.DefWindowProc(w, msg, wParam, lParam);
					}
					return default;
				}
				break;
			case Api.WM_ENTERSIZEMOVE:
				_InMoveSize(true);
				break;
			case Api.WM_EXITSIZEMOVE:
				_InMoveSize(false);
				break;
			case Api.WM_WINDOWPOSCHANGING:
				_WmWindowPosChanging(ref *(Api.WINDOWPOS*)lParam);
				break;
			case ARegisteredHotkey.WM_HOTKEY:
				int hkid = (int)wParam - 100;
				if (hkid >= 0 && hkid <= 3) {
					POINT p = AMouse.XY;
					Api.SetCursorPos(p.x + hkid switch { 0 => -1, 1 => 1, _ => 0 }, p.y + hkid switch { 2 => -1, 3 => 1, _ => 0 });
					return default;
				}
				break;
			case Api.WM_GETOBJECT:
				if (_WmGetobject(wParam, lParam, out var r1)) return r1;
				break;
			case Api.WM_USER + 50: //posted by acc dodefaultaction
				if (!_closed) {
					int i = (int)wParam;
					if ((uint)i < _a.Count) _Click(i, false);
				}

				return default;
			}

			var R = Api.DefWindowProc(w, msg, wParam, lParam);

			switch (msg) {
			case Api.WM_WINDOWPOSCHANGED:
				_WmWindowPosChanged(in *(Api.WINDOWPOS*)lParam);
				break;
			case Api.WM_DPICHANGED:
				_WmDpiChanged(wParam, lParam);
				break;
			case Api.WM_DISPLAYCHANGE:
				_WmDisplayChange();
				break;
				//		case Api.WM_SETTINGCHANGE:
				//			_WmSettingChange(wParam, lParam);
				//			break;
			}

			return R;
		}

		#endregion

		#region input, context menu

		int _HitTest(POINT p) {
			for (int i = 0; i < _a.Count; i++) {
				if (_a[i].rect.Contains(p)) return i;
			}
			return -1;
		}

		unsafe void _WmMousemove(LPARAM lParam) {
			//		AOutput.Write("mm");
			if (_iClick < 0) {
				var p = _LparamToPoint(lParam);
				int i = _HitTest(p);
				if (i != _iHot) {
					if (_iHot >= 0) _Invalidate(_iHot);
					if ((_iHot = i) >= 0) {
						_Invalidate(_iHot);

						//tooltip
						var item = _a[i];
						var s = item.Tooltip;
						if (!DisplayText) { if (s.NE()) s = item.Text; else if (!item.Text.NE() && !item.IsGroup_) s = item.Text + "\n" + s; }
						var sf = item.File; if (!(sf.NE() || sf.Starts("::") || sf.Starts("shell:"))) s = s.NE() ? sf : s + "\n" + sf;
						bool setTT = !s.NE() && item != _tt.item;
						if (!setTT && (setTT = _tt.item != null && _tt.item.rect != _tt.rect)) item = _tt.item; //update tooltip tool rect
						if (setTT) {
							_tt.item = item;
							_tt.rect = item.rect;
							if (!_tt.tt.IsAlive) {
								_tt.tt = Api.CreateWindowEx(WS2.TOPMOST | WS2.TRANSPARENT, "tooltips_class32", null, Api.TTS_ALWAYSTIP | Api.TTS_NOPREFIX, 0, 0, 0, 0, _w);
								_tt.tt.Send(Api.TTM_ACTIVATE, true);
								_tt.tt.Send(Api.TTM_SETMAXTIPWIDTH, 0, AScreen.Of(_w).WorkArea.Width / 3);
							}
							fixed (char* ps = s) {
								var g = new Api.TTTOOLINFO { cbSize = sizeof(Api.TTTOOLINFO), hwnd = _w, uId = 1, lpszText = ps, rect = item.rect };
								_tt.tt.Send(Api.TTM_DELTOOL, 0, &g);
								_tt.tt.Send(Api.TTM_ADDTOOL, 0, &g);
							}
						}

						if (_tt.item != null) {
							var v = new Native.MSG { hwnd = _w, message = Api.WM_MOUSEMOVE, lParam = lParam };
							_tt.tt.Send(Api.TTM_RELAYEVENT, 0, &v);
						}
					}
				}
				if (_iHot >= 0 != _trackMouseEvent) {
					var t = new Api.TRACKMOUSEEVENT(_w, _iHot >= 0 ? Api.TME_LEAVE : Api.TME_LEAVE | Api.TME_CANCEL);
					_trackMouseEvent = Api.TrackMouseEvent(ref t) && _iHot >= 0;
				}
			}
		}
		int _iHot = -1, _iClick = -1;
		bool _trackMouseEvent, _noHotClick;

		(AWnd tt, ToolbarItem item, RECT rect) _tt;

		void _WmMouseleave() {
			_trackMouseEvent = false;
			if (_iHot >= 0) { _Invalidate(_iHot); _iHot = -1; }
		}

		void _WmMouselbuttondown(LPARAM lParam) {
			var mod = AKeys.UI.GetMod(); if (mod != 0 && mod != KMod.Shift) return;
			if (mod == 0) { //click button
				var p = _LparamToPoint(lParam);
				int i = _HitTest(p);
				if (i >= 0) _Click(i, true);
				//		} else if(mod==KMod.Shift) { //move toolbar
				//			var p=AMouse.XY;
				//			ADragDrop.SimpleDragDrop(_w, MButtons.Left, d => {
				//				if (d.Msg.message != Api.WM_MOUSEMOVE) return;
				//				var v=AMouse.XY; if(v==p) return;
				//				int dx=v.x-p.x, dy=v.y-p.y;
				//				p=v;
				//				var r=_w.Rect;
				//				_w.MoveL(r.left+dx, r.top+dy);
				//			});
			}
		}
		int _menuClosedIndex; long _menuClosedTime;

		void _Click(int i, bool real) {
			var b = _a[i];
			if (b.clicked == null) return;
			if (b.IsMenu_) {
				if (i == _menuClosedIndex && ATime.PerfMilliseconds - _menuClosedTime < 100) return;
				AMenu m = null;
				if (b.clicked is Action<AMenu> menu) {
					m = new AMenu(this.Name + " + " + b.Text, _sourceFile, b.sourceLine);
					menu(m);
				} else if (b.clicked is Func<AMenu> func) {
					m = func();
				}
				if (m == null) return;
				m.ActionThread = this.ActionThread;
				m.ActionException = this.ActionException;

				var r = b.rect; _w.MapClientToScreen(ref r);
				m.Show(_w, MSFlags.AlignRectBottomTop, new(r.left, r.bottom), r);
				_menuClosedIndex = i; _menuClosedTime = ATime.PerfMilliseconds;
				//info: OS before wm_lbuttondown closes menu automatically. Previous Show returns before new Show.
			} else {
				if (real) {
					bool ok = false;
					try {
						_Invalidate(_iClick = i);
						ok = ADragDrop.SimpleDragDrop(_w, MButtons.Left, d => {
							if (d.Msg.message != Api.WM_MOUSEMOVE) return;
							int j = _HitTest(_LparamToPoint(d.Msg.lParam));
							if ((j == i) == _noHotClick) {
								_noHotClick ^= true;
								_Invalidate(i);
							}
						}) && !_noHotClick;
					}
					finally {
						_iClick = -1; _noHotClick = false;
						_Invalidate(i);
					}
					if (!ok) return;
				}
				//			AOutput.Write("click", b);
				if (b.actionThread) AThread.Start(() => _ExecItem(), background: false); else _ExecItem();
				void _ExecItem() {
					var action = b.clicked as Action<ToolbarItem>;
					try { action(b); }
					catch (Exception ex) when (!b.actionException) { AWarning.Write(ex.ToString(), -1); }
				}
			}
		}

		void _WmContextmenu() {
			var no = NoContextMenu;
			if (no.Has(TBNoMenu.Menu)) return;
			if (_a.Count == 0 && _satellite != null) no |= TBNoMenu.Layout | TBNoMenu.AutoSize | TBNoMenu.Text;

			var p = _w.MouseClientXY;
			int i = _HitTest(p);
			var item = i < 0 ? null : _a[i];

			var m = new AMenu();

			if (!no.Has(TBNoMenu.Edit | TBNoMenu.File)) {
				var (canEdit, canGo, goText) = MTItem.CanEditOrGoToFile_(_sourceFile, item);
				if (!no.Has(TBNoMenu.Edit) && canEdit) m["Edit toolbar"] = o => AScriptEditor.GoToEdit(_sourceFile, item?.sourceLine ?? _sourceLine);
				if (!no.Has(TBNoMenu.File) && canGo) m[goText] = o => item.GoToFile_();
			}
			if (!no.Has(TBNoMenu.Close)) m.Add("Close", o => _SatPlanetOrThis.Close());
			if (m.Last != null) m.Separator();

			if (!no.Has(TBNoMenu.Anchor)) {
				using (m.Submenu("Anchor")) {
					_AddAnchor(TBAnchor.TopLeft);
					_AddAnchor(TBAnchor.TopRight);
					_AddAnchor(TBAnchor.BottomLeft);
					_AddAnchor(TBAnchor.BottomRight);
					_AddAnchor(TBAnchor.TopLR);
					_AddAnchor(TBAnchor.BottomLR);
					_AddAnchor(TBAnchor.LeftTB);
					_AddAnchor(TBAnchor.RightTB);
					_AddAnchor(TBAnchor.All);
					m.Separator();
					_AddAnchor(TBAnchor.OppositeEdgeX);
					_AddAnchor(TBAnchor.OppositeEdgeY);
					if (IsOwned) _AddAnchor(TBAnchor.Screen);

					void _AddAnchor(TBAnchor an) {
						var k = an <= TBAnchor.All
							? m.AddRadio(an.ToString(), Anchor.WithoutFlags() == an, _ => Anchor = (Anchor & ~TBAnchor.All) | an)
							: m.AddCheck(an.ToString(), Anchor.Has(an), _ => Anchor ^= an, disable: _GetInvalidAnchorFlags(Anchor).Has(an));
						if (_IsSatellite) k.Tooltip = "Note: You may want to set anchor of the owner toolbar instead. Anchor of this auto-hide toolbar is relative to the owner toolbar.";
					}
				}
			}
			if (!no.Has(TBNoMenu.Layout)) {
				using (m.Submenu("Layout")) {
					_AddLayout(TBLayout.HorizontalWrap);
					_AddLayout(TBLayout.Vertical);
					//				_AddLayout(TBLayout.Horizontal);

					void _AddLayout(TBLayout tl) {
						m.AddRadio(tl.ToString(), tl == Layout, _ => Layout = tl);
					}
				}
			}
			if (!no.Has(TBNoMenu.Border)) {
				using (m.Submenu("Border")) {
					_AddBorder(TBBorder.None);
					_AddBorder(TBBorder.Width1);
					_AddBorder(TBBorder.Width2);
					_AddBorder(TBBorder.Width3);
					_AddBorder(TBBorder.Width4);
					_AddBorder(TBBorder.ThreeD);
					_AddBorder(TBBorder.Thick);
					_AddBorder(TBBorder.Caption);
					_AddBorder(TBBorder.CaptionX);

					void _AddBorder(TBBorder b) {
						m.AddRadio(b.ToString(), b == Border, _ => Border = b);
					}
				}
			}
			if (!no.Has(TBNoMenu.Sizable | TBNoMenu.AutoSize | TBNoMenu.Text | TBNoMenu.MiscFlags)) {
				using (m.Submenu("More")) {
					if (!no.Has(TBNoMenu.Sizable)) m.AddCheck("Sizable", Sizable, _ => Sizable ^= true);
					if (!no.Has(TBNoMenu.AutoSize)) m.AddCheck("Auto-size", AutoSize, _ => AutoSize ^= true);
					if (!no.Has(TBNoMenu.Text)) m.AddCheck("Display text", DisplayText, _ => DisplayText ^= true);
					if (!no.Has(TBNoMenu.MiscFlags)) {
						_AddFlag(TBFlags.HideWhenFullScreen);
						if (_SatPlanetOrThis.IsOwned) _AddFlag(TBFlags.ActivateOwnerWindow);

						void _AddFlag(TBFlags f) {
							var tb = _SatPlanetOrThis;
							m.AddCheck(_EnumToString(f), tb.MiscFlags.Has(f), _ => tb.MiscFlags ^= f);
						}

						static string _EnumToString(Enum e) {
							var s = e.ToString().RegexReplace(@"(?<=[^A-Z])[A-Z]", m => " " + m.Value.Lower());
							//						s = s.Replace("Dont", "Don't");
							return s;
						}
					}
				}
			}

			if (!no.Has(TBNoMenu.Toolbars | TBNoMenu.Help) && m.Last != null && !m.Last.separator) m.Separator();
			if (!no.Has(TBNoMenu.Toolbars)) m.Add("Toolbars", o => ToolbarsDialog().Show());
			if (!no.Has(TBNoMenu.Help)) m["How to"] = _ => ADialog.ShowInfo("How to",
	@"Move toolbar: Shift+drag.
Resize toolbar: drag border. Cannot resize if in context menu is unchecked or unavailable More -> Sizable; or if checked Border -> None.
Move or resize precisely: start to move or resize but don't move the mouse. Instead release Shift and press arrow keys. Finally release the mouse button.
");

			if (m.Last != null) m.Show(_w);
		}

		bool _WmNchittest(LPARAM xy, out int ht) {
			ht = 0;
			if (AKeys.UI.GetMod() == KMod.Shift) { //move
				ht = Api.HTCAPTION;
			} else { //resize?
				if (Border == TBBorder.None || (!Sizable && Border < TBBorder.Thick)) return false;
				int x = AMath.LoShort(xy), y = AMath.HiShort(xy);
				if (Sizable) {
					_w.GetWindowInfo_(out var k);
					RECT r = k.rcWindow;
					int b = Border >= TBBorder.ThreeD ? k.cxWindowBorders : (_a.Count > 0 ? _BorderPadding() : ADpi.Scale(6, _dpi)); //make bigger if no buttons. Eg if auto-hide-at-screen-edge, border 1 is difficult to resize.
					int bx = Math.Min(b, r.Width / 2), by = Math.Min(b, r.Height / 2);
					//				AOutput.Write(bx, by);
					int x1 = r.left + bx, x2 = --r.right - bx, y1 = r.top + by, y2 = --r.bottom - by;
					if (r.Width > bx * 8 && r.Height > by * 8) { //if toolbar isn't small, in corners allow to resize both width and height at the same time
						if (x < x1) {
							ht = y < y1 ? Api.HTTOPLEFT : (y > y2 ? Api.HTBOTTOMLEFT : Api.HTLEFT);
						} else if (x > x2) {
							ht = y < y1 ? Api.HTTOPRIGHT : (y > y2 ? Api.HTBOTTOMRIGHT : Api.HTRIGHT);
						} else if (y < y1) {
							ht = Api.HTTOP;
						} else if (y > y2) {
							ht = Api.HTBOTTOM;
						} else return false;
					} else if (r.Width >= r.Height) { //in corners prefer width
						if (x < x1) ht = Api.HTLEFT; else if (x > x2) ht = Api.HTRIGHT; else if (y < y1) ht = Api.HTTOP; else if (y > y2) ht = Api.HTBOTTOM; else return false;
					} else { //in corners prefer height
						if (y < y1) ht = Api.HTTOP; else if (y > y2) ht = Api.HTBOTTOM; else if (x < x1) ht = Api.HTLEFT; else if (x > x2) ht = Api.HTRIGHT; else return false;
					}
				} else { //disable resizing if border is natively sizable
					if (Border < TBBorder.Thick) return false;
					_w.GetWindowInfo_(out var k);
					k.rcWindow.Inflate(-k.cxWindowBorders, -k.cyWindowBorders);
					if (k.rcWindow.Contains(x, y)) return false;
					ht = Api.HTBORDER;
				}
			}
			return true;
		}

		#endregion

		#region properties

		/// <summary>
		/// Whether to DPI-scale toolbar size and offsets.
		/// Default: scale size; scale offsets if anchor is not screen.
		/// </summary>
		/// <remarks>
		/// The unit of measurement of <see cref="Size"/>, <see cref="Offsets"/> and some other properties depends on whether scaling is used for that property. If scaling is used, the unit is logical pixels; it is 1/96 inch regardless of screen DPI. If scaling not used, the unit is physical pixels. Screen DPI can be changed in Windows Settings; when it is 100%, logical and physical pixels are equal.
		/// </remarks>
		public TBScaling DpiScaling { get; set; }

		/// <summary>
		/// Toolbar width and height without non-client area when <see cref="AutoSize"/> false.
		/// </summary>
		/// <remarks>
		/// Non-client area is border and caption when <see cref="Border"/> is <b>ThreeD</b>, <b>Thick</b>, <b>Caption</b> or <b>CaptionX</b>.
		/// 
		/// The unit of measurement depends on <see cref="DpiScaling"/>.
		/// 
		/// This property is updated when resizing the toolbar. It is saved.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// t.Size = new(300, 40);
		/// ]]></code>
		/// </example>
		public System.Windows.Size Size {
			get => _sett.size;
			set {
				if (value != _sett.size) {
					value = new(_Limit(value.Width), _Limit(value.Height));
					_sett.size = value;
					if (IsAlive && !AutoSize) _Resize(_Scale(value));
				}
				if (!_followedOnce) _preferSize = true;
			}
		}

		/// <summary>
		/// Whether the border can be used to resize the toolbar.
		/// Default true.
		/// </summary>
		/// <remarks>
		/// This property is in the context menu and is saved.
		/// </remarks>
		public bool Sizable {
			get => _sett.sizable;
			set => _sett.sizable = value;
		}

		/// <summary>
		/// Automatically resize the toolbar to make all buttons visible.
		/// Default true.
		/// </summary>
		/// <remarks>
		/// This property is in the context menu and is saved.
		/// </remarks>
		public bool AutoSize {
			get => _sett.autoSize;
			set {
				if (value != _sett.autoSize) {
					_sett.autoSize = value;
					_AutoSizeNow();
				}
			}
		}

		/// <summary>
		/// When <see cref="AutoSize"/> is true, this is the preferred width at which buttons are moved to the next row. Unlimited if 0.
		/// </summary>
		/// <remarks>
		/// The unit of measurement depends on <see cref="DpiScaling"/>.
		/// 
		/// This property is updated when the user resizes the toolbar while <see cref="AutoSize"/> is true. It is saved.
		/// 
		/// If layout of this toolbar is vertical, just sets max width.
		/// </remarks>
		public double AutoSizeWrapWidth {
			get => _sett.wrapWidth;
			set {
				value = value > 0 ? _Limit(value) : 0;
				if (value != _sett.wrapWidth) {
					_sett.wrapWidth = value;
					_AutoSizeNow();
				}
			}
		}

		/// <summary>
		/// Specifies to which owner's edges the toolbar keeps constant distance when moving or resizing the owner.
		/// </summary>
		/// <remarks>
		/// The owner can be a window, screen, control or other object. It is specified when calling <see cref="Show"/>.
		/// This property is in the context menu and is saved.
		/// </remarks>
		/// <seealso cref="Offsets"/>
		public TBAnchor Anchor {
			get => _sett.anchor;
			set {
				value &= ~_GetInvalidAnchorFlags(value);
				if (value.WithoutFlags() == 0) value |= TBAnchor.TopLeft;
				if (value == _sett.anchor) return;
				var prev = _sett.anchor;
				_sett.anchor = value;
				if (IsOwned) {
					_os = value.OfScreen() ? new _OwnerScreen(this, default) : null; //follow toolbar's screen
					if (prev.OfScreen() && _followedOnce) {
						if (_oc != null) _oc.UpdateRect(out _); else _ow.UpdateRect(out _);
					}
				}
				if (_followedOnce) {
					var r = _w.Rect;
					_UpdateOffsets(r.left, r.top, r.Width, r.Height);
				}
			}
		}

		/// <summary>
		/// Specifies distances between edges of the toolbar and edges of its owner, depending on <see cref="Anchor"/>.
		/// </summary>
		/// <remarks>
		/// Owner is specified when calling <see cref="Show"/>. It can be a window, screen, control or other object.
		/// 
		/// The <see cref="TBOffsets"/> type has 4 properties - <b>Top</b>, <b>Bottom</b>, <b>Left</b> and <b>Right</b>, but used are only those included in <see cref="Anchor"/>. For example, if <b>Anchor</b> is <b>TopLeft</b>, used are only <b>Top</b> and <b>Left</b>.
		/// 
		/// The unit of measurement depends on <see cref="DpiScaling"/> and whether anchor is screen.
		/// 
		/// This property is updated when moving or resizing the toolbar. It is saved.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// t.Offsets = new(150, 5, 0, 0);
		/// ]]></code>
		/// </example>
		public TBOffsets Offsets {
			get => _offsets;
			set {
				_preferSize = false;
				if (value.Equals(_offsets)) return;
				_sett.offsets = _offsets = value;
				if (_followedOnce) _FollowRect();
				//CONSIDER: add ScreenIndex property or something. Now if screen is auto-selected, this sets xy in that screen, but caller may want in primary screen.
			}
		}
		TBOffsets _offsets;

		//rejected. Would be rarely used, unless default 0. Avoid default limitations like this. We have a dialog to find lost toolbars.
		//public int MaxDistanceFromOwner { get; set; } = int.MaxValue;

		//rejected
		//public bool HideTextIfSmall { get; set; } //like ribbon UI

		/// <summary>
		/// Miscellaneous options.
		/// </summary>
		/// <remarks>
		/// This property is in the context menu (submenu "More") and is saved.
		/// </remarks>
		public TBFlags MiscFlags {
			get => _sett.miscFlags;
			set {
				_sett.miscFlags = value;
			}
		}

		/// <summary>
		/// Opacity and transparent color.
		/// </summary>
		/// <seealso cref="AWnd.SetTransparency(bool, int?, ColorInt?)"/>
		/// <example>
		/// <code><![CDATA[
		/// t.Transparency = (64, null);
		/// ]]></code>
		/// </example>
		public (int? opacity, ColorInt? colorKey) Transparency {
			get => _transparency;
			set {
				if (value != _transparency) {
					_transparency = value;
					if (_created) _w.SetTransparency(value != default, value.opacity, value.colorKey);
				}
			}
		}
		(int? opacity, ColorInt? colorKey) _transparency;

		/// <summary>
		/// Hides some context menu items or menu itself.
		/// </summary>
		public TBNoMenu NoContextMenu { get; set; }

		#endregion
	}
}