using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;

namespace Au.Controls;

public unsafe partial class KTreeView : HwndHost {
	wnd _w;
	bool _hasHwnd;
	
	public wnd Hwnd => _w;
	
	//const string c_winClassName = "KTreeView";
	//static KTreeView() {
	//	WndUtil.RegisterWindowClass(c_winClassName);
	//}
	
	//bool _test;
	
	protected override HandleRef BuildWindowCore(HandleRef hwndParent) {
		//_test = Name == "Files_list";
		
		var wParent = (wnd)hwndParent.Handle;
		_w = WndUtil.CreateWindow(_wndProc = _WndProc, false, "Static", Name, WS.CHILD | WS.CLIPCHILDREN, 0, 0, 0, 10, 10, wParent);
		_hasHwnd = true;
		_SetDpiAndItemSize(More.Dpi.OfWindow(_w));
		
		return new HandleRef(this, _w.Handle);
	}
	
	protected override void DestroyWindowCore(HandleRef hwnd) {
		//print.it("DestroyWindowCore", (wnd)hwnd.Handle);
		Api.DestroyWindow(_w);
	}
	
	WNDPROC _wndProc;
	nint _WndProc(wnd w, int msg, nint wParam, nint lParam) {
		//var pmo = new PrintMsgOptions(Api.WM_NCHITTEST, Api.WM_SETCURSOR, Api.WM_MOUSEMOVE, Api.WM_NCMOUSEMOVE, 0x10c1);
		//if (WndUtil.PrintMsg(out string s, _w, msg, wParam, lParam, pmo)) print.it("<><c green>" + s + "<>");
		//if (_test) if (WndUtil.PrintMsg(out string s, _w, msg, wParam, lParam)) print.it("<><c green>" + s + "<>");
		
		if (_vscroll.WndProc(w, msg, wParam, lParam) || _hscroll.WndProc(w, msg, wParam, lParam)) return default;
		
		switch (msg) {
		//case Api.WM_NCCREATE:
		//	_w = w;
		//	_hasHwnd = true;
		//	_SetDpiAndItemSize(More.Dpi.OfWindow(_w));
		//	break;
		case Api.WM_NCDESTROY:
			_w = default;
			_hasHwnd = false;
			_acc?.Dispose(); _acc = null;
			break;
		case Api.WM_PAINT:
			using (var bp = new BufferedPaint(w, true)) _Render(bp.DC, bp.UpdateRect);
			return default;
		case Api.WM_SHOWWINDOW when wParam == 1:
			int iev = _ensureVisible.indexPlus1 - 1;
			if (iev > 0) EnsureVisible(iev, _ensureVisible.scrollTop);
			break;
		case Api.WM_SIZE:
			_width = Math2.LoWord(lParam);
			_height = Math2.HiWord(lParam);
			_Measure();
			break;
		case Api.WM_SYSCOMMAND when (wParam & 0xFFF0) is Api.SC_VSCROLL or Api.SC_HSCROLL: //note: Windows bug: swapped SC_VSCROLL and SC_HSCROLL
			try {
				_inScrollbarScroll = true;
				return Api.DefWindowProc(w, msg, wParam, lParam);
			}
			finally {
				_ScrollEnded();
				_inScrollbarScroll = false;
			}
		case >= Api.WM_MOUSEFIRST and <= Api.WM_MOUSELAST:
			switch (msg) {
			case Api.WM_LBUTTONDOWN:
				_OnMouseDown(System.Windows.Input.MouseButton.Left, wParam, lParam);
				break;
			case Api.WM_RBUTTONDOWN:
				_OnMouseDown(System.Windows.Input.MouseButton.Right, wParam, lParam);
				break;
			case Api.WM_MBUTTONDOWN:
				_OnMouseDown(System.Windows.Input.MouseButton.Middle, wParam, lParam);
				break;
			case Api.WM_LBUTTONDBLCLK:
				_OnMouseDown(System.Windows.Input.MouseButton.Left, wParam, lParam, true);
				break;
			case Api.WM_MBUTTONDBLCLK:
				_OnMouseDown(System.Windows.Input.MouseButton.Middle, wParam, lParam, true);
				break;
			case Api.WM_LBUTTONUP:
				_OnMouseUp(System.Windows.Input.MouseButton.Left);
				break;
			case Api.WM_RBUTTONUP:
				if (!_OnMouseUp(System.Windows.Input.MouseButton.Right))
					if (!HitTest(Math2.NintToPOINT(lParam), out _)) RightClickInEmptySpace?.Invoke();
				break;
			case Api.WM_MBUTTONUP:
				_OnMouseUp(System.Windows.Input.MouseButton.Middle);
				break;
			case Api.WM_MOUSEMOVE:
				_OnMouseMove(wParam);
				break;
			}
			return 0;
		case Api.WM_MOUSELEAVE:
			_OnMouseLeave();
			break;
		case Api.WM_CAPTURECHANGED:
			_mouse.active = false;
			break;
		}
		
		var R = Api.DefWindowProc(w, msg, wParam, lParam);
		
		switch (msg) {
		case Api.WM_NCHITTEST when R == Api.HTCLIENT: //let WPF manage drag-drop
			if (Api.GetCapture().ClassNameIs("CLIPBRDWNDCLASS")) R = Api.HTTRANSPARENT;
			break;
		}
		
		return R;
	}
	
	protected override nint WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		if (msg == Api.WM_GETOBJECT) { //not in _WndProc, because WPF steals it if passed to base.WndProc
			handled = true;
			return (_acc ??= new _Accessible(this)).WmGetobject(wParam, lParam);
		}
		return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
	}
	
	//protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => null; //removes unused object from MSAA tree, but then no UIA
	_Accessible _acc;
	
	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
		//print.it(e.Property);
		if (!_leEdit.Is0 && e.Property.Name == "IsVisible" && e.NewValue is bool y && !y) EndEditLabel(true);
		base.OnPropertyChanged(e);
	}
	
	///
	protected override bool TabIntoCore(TraversalRequest r) {
		Focus();
		return true;
	}
	
	///
	protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
		=> new PointHitTestResult(this, hitTestParameters.HitPoint);
	//need this for drag-drop. See case Api.WM_NCHITTEST above.
}
