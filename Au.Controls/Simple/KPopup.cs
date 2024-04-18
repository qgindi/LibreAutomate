using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Au.Controls;

/// <summary>
/// HwndSource-based window to use for various temporary tool/info popup windows.
/// For example, in editor used for code info windows (completion list, parameters, "Regex" etc) and for some tooltips.
/// Unlike Window and Popup, you can set any window style, easily show at any position at any DPI, etc.
/// Like Popup, can show by rectangle. Unlike Popup, can be resizable.
/// Like Popup, can be click-closed, and does not eat mouse events like Popup.
/// </summary>
public class KPopup {
	WS _style;
	WSE _exStyle;
	SizeToContent _sizeToContent;
	bool _shadow;
	HwndSource _hs;
	wnd _w;
	bool _inSizeMove;
	
	///
	public KPopup(WS style = WS.POPUP | WS.THICKFRAME, WSE exStyle = WSE.TOOLWINDOW | WSE.NOACTIVATE, bool shadow = false, SizeToContent sizeToContent = default) {
		_style = style;
		_exStyle = exStyle;
		_shadow = shadow;
		_sizeToContent = sizeToContent;
	}
	
	HwndSource _Create() {
		if (_hs == null) {
			var p = new HwndSourceParameters {
				WindowStyle = (int)_style,
				ExtendedWindowStyle = (int)_exStyle,
				WindowClassStyle = _shadow && !_style.Has(WS.THICKFRAME) ? (int)Api.CS_DROPSHADOW : 0,
				WindowName = _windowName,
				//AcquireHwndFocusInMenuMode = false,
				//RestoreFocusMode = System.Windows.Input.RestoreFocusMode.None,
				//TreatAsInputRoot = false,
				HwndSourceHook = _Hook
			};
			_hs = new _HwndSource(p) { kpopup = this, RootVisual = Border, SizeToContent = default };
			//print.it(_hs.AcquireHwndFocusInMenuMode, _hs.RestoreFocusMode, p.TreatAsInputRoot); //True, Auto, True
			
			OnHandleCreated();
		}
		return _hs;
	}
	
	class _HwndSource : HwndSource {
		public _HwndSource(HwndSourceParameters p) : base(p) { }
		public KPopup kpopup;
	}
	
	protected virtual void OnHandleCreated() { HandleCreated?.Invoke(); }
	public event Action HandleCreated;
	
	public static KPopup FromHwnd(wnd w) {
		if (w.IsAlive && HwndSource.FromHwnd(w.Handle) is _HwndSource hs) return hs.kpopup;
		return null;
	}
	
	/// <summary>
	/// Gets popup window handle. Returns default(wnd) if not created (also after destroying).
	/// </summary>
	public wnd Hwnd => _w;
	
	//public HwndSource HwndSource => _hs ?? _Create();
	
	/// <summary>
	/// Gets or sets window name.
	/// </summary>
	public string WindowName {
		get => _windowName;
		set {
			_windowName = value;
			if (!_w.Is0) _w.SetText(value);
		}
	}
	string _windowName;
	
	/// <summary>
	/// Gets or sets this <b>KPopup</b> object name. It is not window name.
	/// </summary>
	public string Name { get; set; }
	
	/// <summary>
	/// Gets or sets WPF content. It is child of <see cref="Border"/>.
	/// </summary>
	public UIElement Content {
		get => _content;
		set {
			_content = value;
			if (_border != null) _border.Child = value;
		}
	}
	UIElement _content;
	
	/// <summary>
	/// Gets the WPF root object (<see cref="HwndSource.RootVisual"/>) of the popup window. Its child is <see cref="Content"/>.
	/// </summary>
	public Border Border => _border ??= new Border { Child = _content, SnapsToDevicePixels = true };
	Border _border;
	//workaround for: if content is eg FlowDocumentScrollViewer, it has focus problems if it is RootVisual.
	//	Eg context menu items disabled. Need a container, eg Border or Panel.
	
	/// <summary>
	/// Desired window size. WPF logical pixels.
	/// Actual size can be smaller if would not fit in screen.
	/// </summary>
	public SIZE Size {
		get => _size;
		set {
			_size = value;
			if (IsVisible) _w.ResizeL_(Dpi.Scale(_size, _w));
		}
	}
	SIZE _size;
	
	/// <summary>
	/// Set <see cref="HwndSource.SizeToContent"/>.
	/// If false (default), this class calculates content size when showing, and does not update while showing. If true, may align incorrectly.
	/// </summary>
	public bool WpfSizeToContent { get; set; }
	
	/// <summary>
	/// Shows the popup window by a window, WPF element or rectangle.
	/// </summary>
	/// <param name="owner">Provides owner window and optionally rectangle. Can be <b>FrameworkElement</b>, <b>KPopup</b>, <b>wnd</b> or null.</param>
	/// <param name="side">Show at this side of rectangle, or opposite side if does not fit in screen. If null, shows in rectangle.</param>
	/// <param name="rScreen">Rectangle in screen (physical pixels). If null, uses owner's rectangle. Cannot be both null.</param>
	/// <param name="exactSize">If does not fit in screen, cover part of rectangle but don't make smaller.</param>
	/// <param name="exactSide">Never show at opposite side.</param>
	/// <exception cref="NotSupportedException">Unsupported <i>owner</i> type.</exception>
	/// <exception cref="ArgumentException">Both owner and rScreen are null. Or owner handle not created.</exception>
	/// <remarks>
	/// If <see cref="Size"/> not set, uses <see cref="SizeToContent.WidthAndHeight"/>.
	/// </remarks>
	public void ShowByRect(object owner, Dock? side, RECT? rScreen = null, bool exactSize = false, bool exactSide = false) {
		//CloseHides = true; //print.it(_w); //18 -> 5 ms
		//perf.first(); if(owner is FrameworkElement test) test.Dispatcher.InvokeAsync(() => perf.nw());
		
		wnd ow = default;
		switch (owner) {
		case null:
			if (rScreen == null) throw new ArgumentException("owner and rScreen are null");
			break;
		case FrameworkElement e:
			ow = e.Hwnd().Window;
			rScreen ??= e.RectInScreen();
			break;
		case KPopup p:
			ow = p._w;
			rScreen ??= ow.Rect;
			break;
		case wnd w:
			ow = w;
			rScreen ??= ow.Rect;
			break;
		default: throw new NotSupportedException("owner type");
		}
		if (owner != null && ow.Is0) throw new ArgumentException("owner window not created");
		if (_size == default) _sizeToContent = SizeToContent.WidthAndHeight;
		
		_Create();
		
		//could use API CalculatePopupWindowPosition instead of this code, but it is not exactly what need here.
		RECT r = rScreen.Value;
		var scrn = screen.of(r);
		var rs = scrn.WorkArea;
		int dpi = scrn.Dpi;
		SIZE size = Dpi.Scale(Size, dpi);
		
		if (_sizeToContent != default) {
			RECT nc = default;
			Dpi.AdjustWindowRectEx(dpi, ref nc, _style, _exStyle);
			rs.left -= nc.left; rs.right -= nc.right; rs.top -= nc.top; rs.bottom -= nc.bottom;
			
			if (_sizeToContent.Has(SizeToContent.Width)) size.width = rs.Width; else size.width = Math.Min(size.width, rs.Width);
			if (_sizeToContent.Has(SizeToContent.Height)) size.height = rs.Height; else size.height = Math.Min(size.height, rs.Height);
			_border.Measure(Dpi.Unscale(size, dpi));
			//never mind: it seems FlowDocument measures only height.
			//	If need width, could instead use TextBlock. It does not support paragraph etc, but we can use multiple TextBlock etc in StackPanel.
			//	But then no select/copy, not so easy scrolling, etc.
			_border.UpdateLayout();
			var ds = _border.DesiredSize;
			ds.Width = Math.Ceiling(ds.Width + .5); ds.Height = Math.Ceiling(ds.Height + .5); //avoid scrollbars
			size = Dpi.Scale(ds, dpi);
			size.width += nc.Width; size.height += nc.Height;
		}
		size.width = Math.Min(size.width, rs.Width);
		size.height = Math.Min(size.height, rs.Height);
		
		_hs.SizeToContent = WpfSizeToContent ? _sizeToContent : default;
		
		if (side != null) {
			int spaceT = r.top - rs.top, spaceB = rs.bottom - r.bottom, spaceL = r.left - rs.left, spaceR = rs.right - r.right;
			if (!exactSide) {
				switch (side) {
				case Dock.Left:
					if (size.width > spaceL && spaceR > spaceL) side = Dock.Right;
					break;
				case Dock.Right:
					if (size.width > spaceR && spaceL > spaceR) side = Dock.Left;
					break;
				case Dock.Top:
					if (size.height > spaceT && spaceB > spaceT) side = Dock.Bottom;
					break;
				case Dock.Bottom:
					if (size.height > spaceB && spaceT > spaceB) side = Dock.Top;
					break;
				}
			}
			if (!exactSize) {
				switch (side) {
				case Dock.Left:
					if (size.width > spaceL) size.width = Math.Max(spaceL, size.width / 2);
					break;
				case Dock.Right:
					if (size.width > spaceR) size.width = Math.Max(spaceR, size.width / 2);
					break;
				case Dock.Top:
					if (size.height > spaceT) size.height = Math.Max(spaceT, size.height / 2);
					break;
				case Dock.Bottom:
					if (size.height > spaceB) size.height = Math.Max(spaceB, size.height / 2);
					break;
				}
			}
			switch (side) {
			case Dock.Left: r.left -= size.width; break;
			case Dock.Right: r.left = r.right; break;
			case Dock.Top: r.top -= size.height; break;
			case Dock.Bottom: r.top = r.bottom; break;
			}
		}
		
		r.left = Math.Clamp(r.left, rs.left, rs.right - size.width);
		r.top = Math.Clamp(r.top, rs.top, rs.bottom - size.height);
		r.Width = size.width; r.Height = size.height;
		
		_inSizeMove = false;
		_w.MoveL(r);
		if (_w.Get.Owner != ow) WndUtil.SetOwnerWindow(_w, ow);
		if (!IsVisible) _w.SetWindowPos(SWPFlags.SHOWWINDOW | SWPFlags.NOMOVE | SWPFlags.NOSIZE | SWPFlags.NOACTIVATE | SWPFlags.NOOWNERZORDER);
	}
	
	/// <summary>
	/// Destroys or hides the popup window, depending on <see cref="CloseHides"/>.
	/// </summary>
	public void Close() {
		if (_hs == null) return;
		if (CloseHides) _w.ShowL(false);
		else _hs.Dispose();
	}
	
	/// <summary>
	/// Don't destroy the popup window when closing, but just hide.
	/// In any case, if destroyed, <b>ShowX</b> will create new window.
	/// </summary>
	public bool CloseHides { get; set; }
	
	/// <summary>
	/// true if closed (or hidden if <see cref="CloseHides"/>) when the user clicked the x button.
	/// </summary>
	public bool UserClosed { get; set; }
	
	/// <summary>
	/// Whether the popup window is currently visible.
	/// </summary>
	public bool IsVisible => _w.IsVisible;
	
	/// <summary>
	/// When the popup window becomes invisible. It also happend when destroying.
	/// </summary>
	public event EventHandler Hidden;
	
	/// <summary>
	/// When destroying the popup window (WM_NCDESTROY).
	/// </summary>
	public event EventHandler Destroyed;
	
	/// <summary>
	/// Close when mouse clicked.
	/// </summary>
	public CC ClickClose { get; set; }
	
	void _ClickCloseTimer(bool? start) {
		if (start == null) {
			int state = (keys.gui.getKeyState(KKey.MouseLeft) & 1)
				| (keys.gui.getKeyState(KKey.MouseRight) & 1) << 1
				| (keys.gui.getKeyState(KKey.MouseMiddle) & 1) << 2;
			if (_ccState < 0) _ccState = state; //first time
			if (state == _ccState) return;
			_ccState = state;
			if (ClickClose != CC.Anywhere) {
				var wm = wnd.fromMouse(WXYFlags.NeedWindow);
				if (ClickClose == CC.Inside) {
					if (wm != _w) return;
				} else {
					if (wm == _w) return;
					if (wm.IsOfThisThread && wm.ZorderIsAbove(_w)) return;
					//SHOULDDO: not always closes when need.
					// Example: in a KTreeView edit label, then activate a floating panel (eg Cookbook).
				}
			}
			Close();
		} else if (start == true) {
			if (!ClickClose.HasAny(CC.Anywhere)) return;
			_ccTimer = 0 != Api.SetTimer(_w, c_ccTimer, 100, null);
			_ccState = -1; //GetKeyState may not work if called from WM_WINDOWPOSCHANGED. Call from WM_TIMER.
		} else if (_ccTimer) {
			_ccTimer = false;
			Api.KillTimer(_w, c_ccTimer);
		}
	}
	const int c_ccTimer = 10;
	bool _ccTimer;
	int _ccState;
	
	[Flags]
	public enum CC { Inside = 1, Outside = 2, Anywhere = 3 };
	
	unsafe nint _Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) {
		var r = _Hook((wnd)hwnd, msg, wParam, lParam);
		handled = r != null;
		return r ?? 0;
	}
	
	unsafe nint? _Hook(wnd w, int msg, nint wParam, nint lParam) {
		//WndUtil.PrintMsg(w, msg, wParam, lParam);
		//if(WndUtil.PrintMsg(out string s1, w, msg, wParam, lParam)) print.qm2.write(s1);
		//if (msg == Api.WM_ACTIVATE && wParam != 0) print.it("ACTIVATE");
		
		switch (msg) {
		case Api.WM_NCCREATE:
			_w = w;
			break;
		case Api.WM_NCDESTROY:
			Destroyed?.Invoke(this, EventArgs.Empty);
			_hs.RootVisual = null;
			_hs = null;
			_w = default;
			break;
		case Api.WM_WINDOWPOSCHANGED:
			var wp = (Api.WINDOWPOS*)lParam;
			//print.it(wp->flags & SWPFlags._KNOWNFLAGS, IsVisible);
			if (!wp->flags.Has(SWPFlags.NOSIZE) && _inSizeMove) _size = SIZE.From(Dpi.Unscale((wp->cx, wp->cy), w), true);
			if (wp->flags.Has(SWPFlags.SHOWWINDOW)) {
				UserClosed = false;
				_ClickCloseTimer(true);
			}
			if (wp->flags.Has(SWPFlags.HIDEWINDOW)) {
				_ClickCloseTimer(false);
				if (Border.IsKeyboardFocusWithin) Keyboard.ClearFocus();
				Hidden?.Invoke(this, EventArgs.Empty);
			}
			break;
		case Api.WM_ENTERSIZEMOVE:
			_inSizeMove = true;
			break;
		case Api.WM_EXITSIZEMOVE:
			_inSizeMove = false;
			break;
		case Api.WM_MOUSEACTIVATE:
			//OS ignores WS_EX_NOACTIVATE if the active window is of this thread. Workaround: on WM_MOUSEACTIVATE return MA_NOACTIVATE.
			switch (Math2.HiShort(lParam)) {
			case Api.WM_MBUTTONDOWN:
				Close(); //never mind: we probably don't receive this message if our thread is inactive
				return Api.MA_NOACTIVATEANDEAT;
			}
			if (_exStyle.Has(WSE.NOACTIVATE)) {
				return Api.MA_NOACTIVATE;
			}
			break;
		case Api.WM_NCLBUTTONDOWN:
			if (_exStyle.Has(WSE.NOACTIVATE)) {
				//OS activates when clicked in non-client area, eg when moving or resizing. Workaround: on WM_NCLBUTTONDOWN suppress activation with a CBT hook.
				//When moving or resizing, WM_NCLBUTTONDOWN returns when moving/resizing ends. On resizing would activate on mouse button up.
				var wa = wnd.thisThread.active;
				if (wa != default && wa != w) {
					using (WindowsHook.ThreadCbt(d => d.code == HookData.CbtEvent.ACTIVATE && d.Hwnd == w))
						Api.DefWindowProc(w, msg, wParam, lParam);
					return 0;
				}
			}
			break;
		case Api.WM_SYSCOMMAND when (wParam & 0xFFF0) == Api.SC_CLOSE:
			UserClosed = true;
			break;
		case Api.WM_CLOSE:
			if (CloseHides) { _w.ShowL(false); return 0; }
			break;
		case Api.WM_DPICHANGED:
			if (!_inSizeMove) *(RECT*)lParam = _w.Rect; //prevent changing rect, it's already calculated for the DPI
			_hs.DpiChangedWorkaround();
			break;
		case Api.WM_TIMER when wParam == c_ccTimer:
			_ClickCloseTimer(null);
			break;
		case Api.WM_SHOWWINDOW:
			if (wParam == 0) {
				if (lParam == 1) _w.ShowL(false); //minimizing owner. Prevent showing again when restoring.
				else if (w.IsActive) {
					var ow = w.Get.Owner;
					_hs.Dispatcher.InvokeAsync(() => { if(w.IsActive && ow.IsVisible && !ow.IsMinimized) ow.ActivateL(); });
				}
			}
			break;
		}
		return null;
	}
}
