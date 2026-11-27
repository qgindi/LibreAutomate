using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
	
	/// <summary>
	/// Sets window style.
	/// </summary>
	/// <param name="style">Style. Always adds <c>POPUP</c>.</param>
	/// <param name="exStyle"></param>
	/// <param name="shadow"></param>
	/// <param name="sizeToContent"></param>
	public KPopup(WS style = WS.THICKFRAME, WSE exStyle = WSE.TOOLWINDOW | WSE.NOACTIVATE, bool shadow = false, SizeToContent sizeToContent = default) {
		_style = style | WS.POPUP;
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
			_hs = new _HwndSource(p) { kpopup = this, SizeToContent = 0 };
			_hs.RootVisual = Border; //after setting properties
			
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
	public FrameworkElement Content {
		get => _content;
		set {
			_content = value;
			_border?.Child = value;
		}
	}
	FrameworkElement _content;
	
	/// <summary>
	/// Gets the WPF root object (<see cref="HwndSource.RootVisual"/>) of the popup window. Its child is <see cref="Content"/>.
	/// </summary>
	public Border Border => _border ??= new Border { SnapsToDevicePixels = true, Child = _content };
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
			if (IsVisible) _w.ResizeL_(_DpiScale(_size));
		}
	}
	SIZE _size;
	
#if false //not used. If will use, test more.
	/// <summary>
	/// Set <see cref="HwndSource.SizeToContent"/>.
	/// If false (default), this class calculates content size when showing, and does not update while showing. If true, may align incorrectly.
	/// </summary>
	public bool WpfSizeToContent { get; set; }
#endif
	
	/// <summary>
	/// Calls <c>Border.Measure, Border.UpdateLayout, Border.DesiredSize</c>.
	/// </summary>
	protected virtual Size MeasureContent(Size availableSize) {
		_border.Measure(availableSize);
		_border.UpdateLayout();
		return _border.DesiredSize;
	}
	
	SIZE _DpiScale(Size size) {
		var m = _hs.CompositionTarget.TransformToDevice;
		return new((int)Math.Ceiling(size.Width * m.M11), (int)Math.Ceiling(size.Height * m.M22));
	}
	
	Size _DpiUnscale(SIZE size) {
		var m = _hs.CompositionTarget.TransformFromDevice;
		return new(size.width * m.M11, size.height * m.M22);
	}
	
	/// <summary>
	/// Shows the popup window by a window, WPF element or rectangle.
	/// </summary>
	/// <param name="owner">Provides owner window and optionally rectangle. Can be <b>FrameworkElement</b>, <b>KPopup</b>, <b>wnd</b> or null.</param>
	/// <param name="side">Show at this side of rectangle, or opposite side if does not fit in screen. If null, shows in rectangle.</param>
	/// <param name="rAnchor">Rectangle in screen (physical pixels). If null, uses owner's rectangle. Cannot be both null.</param>
	/// <param name="exactSize">If does not fit in screen, cover part of rectangle but don't make smaller.</param>
	/// <exception cref="NotSupportedException">Unsupported <i>owner</i> type.</exception>
	/// <exception cref="ArgumentException">Both owner and rScreen are null. Or owner handle not created.</exception>
	/// <remarks>
	/// If <see cref="Size"/> not set, uses <see cref="SizeToContent.WidthAndHeight"/>.
	/// </remarks>
	public void ShowByRect(object owner, Dock? side, RECT? rAnchor = null, bool exactSize = false) {
		_Create();
		
		wnd ow = owner switch { null => default, FrameworkElement e => e.Hwnd().Window, KPopup p => p._w, wnd w => w, _ => throw new NotSupportedException("owner type") };
		if (owner != null && ow.Is0) throw new ArgumentException("owner window not created");
		
		var r = rAnchor ?? owner switch { FrameworkElement e => e.RectInScreen(), KPopup or wnd => ow.Rect, _ => throw new ArgumentException("owner and rAnchor are null") };
		
		if (_size == default) _sizeToContent = SizeToContent.WidthAndHeight;
		
		var scrn = screen.of(r);
		RECT rwa = scrn.WorkArea;
		
		if (screen.of(_w) != scrn)
			_w.MoveL(rwa.left, rwa.top, 1, 1);
		
		SIZE size = _sizeToContent == SizeToContent.WidthAndHeight ? default : _DpiScale(_size);
		
		if (_sizeToContent != default) {
			RECT nc = default;
			int dpi = (int)VisualTreeHelper.GetDpi(Border).PixelsPerInchX;
			Dpi.AdjustWindowRectEx(dpi, ref nc, _style, _exStyle);
			var rwa2 = RECT.FromLTRB(rwa.left - nc.left, rwa.top - nc.top, rwa.right - nc.right, rwa.bottom - nc.bottom);
			
			RECT rSide = side == null ? r
				: _GetBiggestAreaByAnchorRect(side.Value, r, rwa2); //when measuring, as the available size use the bigger part of the work area at the specified or opposite side from the anchor rect
			
			SIZE area = new(Math.Max(rSide.Width, rwa2.Width / 4), Math.Max(rSide.Height, rwa2.Height / 3));
			area.width--; area.height--; //because DPI-scaling may add 1 pixel when rounding double to int
			
			if (_sizeToContent.Has(SizeToContent.Width)) size.width = area.width; else size.width = Math.Min(size.width, area.width);
			if (_sizeToContent.Has(SizeToContent.Height)) size.height = area.height; else size.height = Math.Min(size.height, area.height);
			
			var ds = MeasureContent(_DpiUnscale(size));
			
			size = _DpiScale(ds);
			size.width += nc.Width; size.height += nc.Height;
		}
		size.width = Math.Min(size.width, rwa.Width);
		size.height = Math.Min(size.height, rwa.Height);
		
		//_hs.SizeToContent = WpfSizeToContent ? _sizeToContent : 0;
		
		if (side != null) {
			int spaceT = r.top - rwa.top, spaceB = rwa.bottom - r.bottom, spaceL = r.left - rwa.left, spaceR = rwa.right - r.right;
			
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
		
		r.left = Math.Clamp(r.left, rwa.left, rwa.right - size.width);
		r.top = Math.Clamp(r.top, rwa.top, rwa.bottom - size.height);
		r.Width = size.width; r.Height = size.height;
		
		if (_sizeToContent != default && _RemeasureIfSameWindowSize(size)) {
			_w.MoveL(r.left, r.top);
		} else {
			_w.MoveL(r);
		}
		
		if (_w.Get.Owner != ow) WndUtil.SetOwnerWindow(_w, ow);
		if (!IsVisible) _w.SetWindowPos(SWPFlags.SHOWWINDOW | SWPFlags.NOMOVE | SWPFlags.NOSIZE | SWPFlags.NOACTIVATE | SWPFlags.NOOWNERZORDER);
		
		static RECT _GetBiggestAreaByAnchorRect(Dock side, RECT rAnchor, RECT r /*work area*/) {
			if (side is Dock.Left or Dock.Right) {
				int atLeft = rAnchor.left - r.left, atRight = r.right - rAnchor.right;
				if (atLeft > atRight) r.right = rAnchor.left; else r.left = rAnchor.right;
			} else {
				int atTop = rAnchor.top - r.top, atBottom = r.bottom - rAnchor.bottom;
				if (atTop > atBottom) r.bottom = rAnchor.top; else r.top = rAnchor.bottom;
			}
			return r;
		}
	}
	
	/// <summary>
	/// Resizes the window for new content. Does not move.
	/// Used when selecting overloads in signature info or completion item info. Not tested with others.
	/// </summary>
	public void ResizeForNewContent() {
		if (!_w.GetWindowAndClientRectInScreen(out var rw, out var rc)) return;
		var scrn = screen.of(_w);
		var rwa = scrn.WorkArea;
		
		var r = RECT.FromLTRB(rc.left, rc.top, rwa.right - (rw.right - rc.right), rwa.bottom - (rw.bottom - rc.bottom)); //workarea size available for resizing _w without moving
		if (r.NoArea) return;
		SIZE size = r.Size;
		
		var ds = MeasureContent(_DpiUnscale(size));
		size = _DpiScale(ds);
		
		size.width += rw.Width - rc.Width;
		size.height += rw.Height - rc.Height;
		
		if (_RemeasureIfSameWindowSize(size)) return;
		
		_w.ResizeL_(size);
	}
	
	//If FlowDocumentScrollViewer, need to measure second time with the final available size.
	//Else it's layout is not complete. Then does not display the scrollbar, does not set the separator width, etc.
	//If window size changed, WPF remeasures. Else let's do it now.
	bool _RemeasureIfSameWindowSize(SIZE size) {
		if (_w.Rect.Size == size && _w.GetClientRect(out var rc)) {
			_border.Measure(_DpiUnscale(new(rc.Width, rc.Height)));
			_border.UpdateLayout();
			return true;
		}
		return false;
	}
	
	/// <summary>
	/// Destroys or hides the popup window, depending on <see cref="CloseHides"/>.
	/// </summary>
	public void Close() {
		if (_hs == null) return;
		if (CloseHides) _w.SetWindowPos(SWPFlags.HIDEWINDOW | SWPFlags.NOSIZE | SWPFlags.NOMOVE | SWPFlags.NOACTIVATE | SWPFlags.NOZORDER | SWPFlags.NOOWNERZORDER);
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
	
	/// <summary>
	/// Activate the owner window when clicked, if this and owner both are inactive. Only if has <b>WS_EX_NOACTIVATE</b> style.
	/// </summary>
	public bool ClickInactiveActivateOwner { get; set; }
	
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
		case Api.WM_EXITSIZEMOVE:
			_size = SIZE.From(_DpiUnscale(w.Rect.Size), true);
			break;
		case Api.WM_MOUSEACTIVATE:
			//OS ignores WS_EX_NOACTIVATE if the active window is of this thread. Workaround: on WM_MOUSEACTIVATE return MA_NOACTIVATE.
			switch (Math2.HiShort(lParam)) {
			case Api.WM_MBUTTONDOWN:
				Close(); //never mind: we probably don't receive this message if our thread is inactive
				return Api.MA_NOACTIVATEANDEAT;
			}
			if (_exStyle.Has(WSE.NOACTIVATE)) {
				if (ClickInactiveActivateOwner && w.Get.Owner is var ow && ow.IsAlive && !ow.IsActive && !w.IsActive) {
					ow.ActivateL();
				}
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
					_hs.Dispatcher.InvokeAsync(() => { if (w.IsActive && ow.IsVisible && !ow.IsMinimized) ow.ActivateL(); });
				}
			}
			break;
		}
		return null;
	}
}
