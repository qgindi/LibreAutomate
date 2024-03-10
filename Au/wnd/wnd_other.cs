namespace Au {
	public partial struct wnd {
		/// <summary>
		/// Sets window transparency attributes (opacity and/or transparent color).
		/// </summary>
		/// <param name="allowTransparency">Set or remove <b>WS_EX_LAYERED</b> style that is required for transparency. If <c>false</c>, other parameters are not used.</param>
		/// <param name="opacity">Opacity from 0 (completely transparent) to 255 (opaque). Does not change if <c>null</c>. If less than 0 or greater than 255, makes 0 or 255.</param>
		/// <param name="colorKey">Make pixels of this color completely transparent. Does not change if <c>null</c>. The alpha byte is not used.</param>
		/// <param name="noException">Don't throw exception when fails.</param>
		/// <exception cref="AuWndException"/>
		/// <remarks>
		/// Uses API <msdn>SetLayeredWindowAttributes</msdn>.
		/// On Windows 7 works only with top-level windows, not with controls.
		/// Fails with WPF windows (class name starts with <c>"HwndWrapper"</c>).
		/// </remarks>
		public void SetTransparency(bool allowTransparency, int? opacity = null, ColorInt? colorKey = null, bool noException = false) {
			var est = ExStyle;
			bool layered = est.Has(WSE.LAYERED);
			
			if (allowTransparency) {
				uint col = 0, f = 0; byte op = 0;
				if (colorKey != null) { f |= 1; col = (uint)colorKey.Value.ToBGR(); }
				if (opacity != null) { f |= 2; op = (byte)Math.Clamp(opacity.Value, 0, 255); }
				
				if (!layered) SetExStyle(est | WSE.LAYERED, noException ? WSFlags.NoException : 0);
				if (!Api.SetLayeredWindowAttributes(this, col, op, f) && !noException) ThrowUseNative();
			} else if (layered) {
				SetExStyle(est & ~WSE.LAYERED, noException ? WSFlags.NoException : 0);
				//tested: resets attributes, ie after adding WSE.LAYERED the window will be normal
			}
		}
		
		/// <summary>
		/// Gets window transparency attributes (opacity and transparency color).
		/// </summary>
		/// <param name="opacity">If this function returns <c>true</c> and the window has an opacity attribute, receives the opacity value 0-255, else <c>null</c>.</param>
		/// <param name="colorKey">If this function returns <c>true</c> and the window has a transparency color attribute, receives the color, else <c>null</c>.</param>
		/// <returns>True if the window has transparency attributes set with <see cref="SetTransparency"/> or API <msdn>SetLayeredWindowAttributes</msdn>. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Uses API <msdn>GetLayeredWindowAttributes</msdn>.
		/// </remarks>
		public bool GetTransparency(out int? opacity, out ColorInt? colorKey) {
			opacity = default; colorKey = default;
			if (/*ExStyle.Has(WSE.LAYERED) && */Api.GetLayeredWindowAttributes(this, out uint col, out byte op, out uint f)) {
				if (0 != (f & 1)) colorKey = col;
				if (0 != (f & 2)) opacity = op;
				return true;
			}
			return false;
		}
		
		/// <summary>
		/// Returns <c>true</c> if this is a full-screen window and not desktop.
		/// </summary>
		public bool IsFullScreen => IsFullScreen_(out _);
		
		internal unsafe bool IsFullScreen_(out screen scrn) {
			scrn = default;
			if (Is0) return false;
			
			//is client rect equal to window rect (no border)?
			RECT r, rc, rm;
			r = Rect; //fast
			int cx = r.right - r.left, cy = r.bottom - r.top;
			if (cx < 400 || cy < 300) return false; //too small
			rc = ClientRect; //fast
			if (rc.right != cx || rc.bottom != cy) {
				if (cx - rc.right > 2 || cy - rc.bottom > 2) return false; //some windows have 1-pixel border
			}
			
			//covers whole screen rect?
			scrn = screen.of(this, SODefault.Zero); if (scrn.IsEmpty) return false;
			rm = scrn.Rect;
			
			if (r.left > rm.left || r.top > rm.top || r.right < rm.right || r.bottom < rm.bottom - 1) return false; //info: -1 for inactive Chrome
			
			//is it desktop?
			if (IsOfShellThread_) return false;
			if (this == getwnd.root) return false;
			
			return true;
			
			//This is the best way to test for fullscreen (FS) window. Fast.
			//Window and client rect was equal of almost all my tested FS windows. Except Winamp visualization.
			//Most FS windows are same size as screen, but some slightly bigger.
			//Don't look at window styles. For some FS windows they are not as should be.
			//Returns false if the active window is owned by a fullscreen window. This is different than appbar API interprets it. It's OK for our purposes.
		}
		
		/// <summary>
		/// Returns <c>true</c> if this belongs to <b>GetShellWindow</b>'s thread (usually it is the desktop window).
		/// </summary>
		internal bool IsOfShellThread_ {
			get => 1 == s_isShellWindow.IsShellWindow(this);
		}
		
		/// <summary>
		/// Returns <c>true</c> if this belongs to <b>GetShellWindow</b>'s process (eg a folder window, desktop, taskbar).
		/// </summary>
		internal bool IsOfShellProcess_ {
			get => 0 != s_isShellWindow.IsShellWindow(this);
		}
		
		struct _ISSHELLWINDOW {
			int _tidW, _tidD, _pidW, _pidD;
			IntPtr _w, _wDesk; //not wnd because then TypeLoadException
			
			public int IsShellWindow(wnd w) {
				if (w.Is0) return 0;
				wnd wDesk = getwnd.shellWindow; //fast
				if (w == wDesk) return 1; //Progman. Other window (WorkerW) may be active when desktop active.
				
				//cache because GetWindowThreadProcessId quite slow
				if (w.Handle != _w) { _w = w.Handle; _tidW = w.GetThreadProcessId(out _pidW); }
				if (wDesk.Handle != _wDesk) { _wDesk = wDesk.Handle; _tidD = wDesk.GetThreadProcessId(out _pidD); }
				
				if (_tidW == _tidD) return 1;
				if (_pidW == _pidD) return 2;
				return 0;
			}
		}
		static _ISSHELLWINDOW s_isShellWindow;
		
		/// <summary>
		/// Returns <c>true</c> if this window has Metro style, ie is not a classic desktop window.
		/// On Windows 8/8.1 most Windows Store app windows and many shell windows have Metro style.
		/// On Windows 10 few windows have Metro style.
		/// On Windows 7 there are no Metro style windows.
		/// </summary>
		/// <seealso cref="WndUtil.GetWindowsStoreAppId"/>
		public bool IsWindows8MetroStyle {
			get {
				if (!osVersion.minWin8) return false;
				if (!HasExStyle(WSE.TOPMOST | WSE.NOREDIRECTIONBITMAP) || (Style & WS.CAPTION) != 0) return false;
				if (ClassNameIs("Windows.UI.Core.CoreWindow")) return true;
				if (!osVersion.minWin10 && IsOfShellProcess_) return true;
				return false;
				//could use IsImmersiveProcess, but this is better
			}
		}
		
		/// <summary>
		/// On Windows 10 and later returns non-zero if this top-level window is a UWP app window: 1 if class name is <c>"ApplicationFrameWindow"</c>, 2 if <c>"Windows.UI.Core.CoreWindow"</c>.
		/// </summary>
		/// <seealso cref="WndUtil.GetWindowsStoreAppId"/>
		public int IsUwpApp {
			get {
				if (!osVersion.minWin10) return 0;
				if (!HasExStyle(WSE.NOREDIRECTIONBITMAP)) return 0;
				return ClassNameIs("ApplicationFrameWindow", "Windows.UI.Core.CoreWindow");
				//could use IsImmersiveProcess, but this is better
			}
		}
		
		//This is too litle tested to be public. Tested only with WinUI 3 Controls Gallery. Also WinUI3 is still kinda experimental and rare (2021).
		/// <summary>
		/// On Windows 10 and later returns <c>true</c> if this top-level window is a WinUI app window (class name <c>"WinUIDesktopWin32WindowClass"</c>).
		/// </summary>
		internal bool IsWinUI_ {
			get {
				if (!osVersion.minWin10) return false;
				//if (!HasExStyle(WSE.NOREDIRECTIONBITMAP)) return 0; //only the control (cn "Microsoft.UI.Content.ContentWindowSiteBridge") has this style
				return ClassNameIs("WinUIDesktopWin32WindowClass");
			}
		}
		
		/// <summary>
		/// If this control is (or is based on) a standard control provided by Windows, such as button or treeview, returns the control type. Else returns <b>None</b>.
		/// </summary>
		/// <remarks>
		/// Sends message <b>WM_GETOBJECT</b> <msdn>QUERYCLASSNAMEIDX</msdn>. Slower than <see cref="ClassName"/> or <see cref="ClassNameIs(string)"/>, but can detect the base type of controls based on standard Windows controls but with a different class name.
		/// </remarks>
		public WControlType CommonControlType => (WControlType)Send(Api.WM_GETOBJECT, 0, (nint)EObjid.QUERYCLASSNAMEIDX);
	}
}

namespace Au.Types {
	/// <summary>
	/// <see cref="wnd.CommonControlType"/>
	/// </summary>
	public enum WControlType {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		None,
		Listbox = 65536,
		Button = 65536 + 2,
		Static,
		Edit,
		Combobox,
		Scrollbar = 65536 + 10,
		Status,
		Toolbar,
		Progress,
		Animate,
		Tab,
		Hotkey,
		Header,
		Trackbar,
		Listview,
		Updown = 65536 + 22,
		ToolTips = 65536 + 24,
		Treeview,
		RichEdit = 65536 + 28
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}
}