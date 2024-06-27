namespace Au.Types;

//#pragma warning disable 649 //field never assigned

static unsafe partial class Api {
	[DllImport("user32.dll", EntryPoint = "SendMessageW", SetLastError = true)]
	internal static extern nint SendMessage(wnd hWnd, int msg, nint wParam, nint lParam);
	
	[DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
	internal static extern nint SendMessageTimeout(wnd hWnd, int Msg, nint wParam, nint lParam, SMTFlags flags, int uTimeout, out nint lpdwResult);
	
	[DllImport("user32.dll", EntryPoint = "SendNotifyMessageW", SetLastError = true)]
	internal static extern bool SendNotifyMessage(wnd hWnd, int Msg, nint wParam, nint lParam);
	
	[DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
	internal static extern bool PostMessage(wnd hWnd, int Msg, nint wParam, nint lParam);
	
	[DllImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
	internal static extern bool PostThreadMessage(int idThread, int Msg, nint wParam, nint lParam);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint GetWindowLongW(wnd hWnd, int nIndex);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint GetWindowLongPtrW(wnd hWnd, int nIndex);
	
	//info: 32-bit user32.dll does not have GetWindowLongPtrW etc. In C++, in x86 config it is defined as GetWindowLongW etc.
	internal static nint GetWindowLongPtr(wnd w, int nIndex)
		=> IntPtr.Size == 8 ? GetWindowLongPtrW(w, nIndex) : GetWindowLongW(w, nIndex);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint SetWindowLongW(wnd hWnd, int nIndex, nint dwNewLong);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint SetWindowLongPtrW(wnd hWnd, int nIndex, nint dwNewLong);
	
	internal static nint SetWindowLongPtr(wnd w, int nIndex, nint dwNewLong)
		=> IntPtr.Size == 8 ? SetWindowLongPtrW(w, nIndex, dwNewLong) : SetWindowLongW(w, nIndex, dwNewLong);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint GetClassLongW(wnd hWnd, int nIndex);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint GetClassLongPtrW(wnd hWnd, int nIndex);
	
	internal static nint GetClassLongPtr(wnd w, int nIndex)
		=> IntPtr.Size == 8 ? GetClassLongPtrW(w, nIndex) : GetClassLongW(w, nIndex);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint SetClassLongW(wnd hWnd, int nIndex, nint dwNewLong);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern nint SetClassLongPtrW(wnd hWnd, int nIndex, nint dwNewLong);
	
	internal static nint SetClassLongPtr(wnd w, int nIndex, nint dwNewLong)
		=> IntPtr.Size == 8 ? SetClassLongPtrW(w, nIndex, dwNewLong) : SetClassLongW(w, nIndex, dwNewLong);
	
	[DllImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
	internal static extern int GetClassName(wnd hWnd, char* lpClassName, int nMaxCount);
	
	[DllImport("user32.dll", EntryPoint = "InternalGetWindowText", SetLastError = true)]
	internal static extern int InternalGetWindowText(wnd hWnd, char* pString, int cchMaxCount);
	
	[DllImport("user32.dll")]
	internal static extern bool IsWindow(wnd hWnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsWindowVisible(wnd hWnd);
	
	internal const int SW_HIDE = 0;
	internal const int SW_SHOWNORMAL = 1;
	internal const int SW_SHOWMINIMIZED = 2;
	internal const int SW_SHOWMAXIMIZED = 3;
	internal const int SW_SHOWNOACTIVATE = 4; //restores min/max window
	internal const int SW_SHOW = 5;
	internal const int SW_MINIMIZE = 6;
	internal const int SW_SHOWMINNOACTIVE = 7;
	internal const int SW_SHOWNA = 8;
	internal const int SW_RESTORE = 9;
	internal const int SW_SHOWDEFAULT = 10;
	internal const int SW_FORCEMINIMIZE = 11;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern void ShowWindow(wnd hWnd, int SW_X);
	//note: the return value does not say succeeded/failed.
	//	It is non-zero if was visible, 0 if was hidden.
	//	Declared void to avoid programming errors.
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsWindowEnabled(wnd hWnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern void EnableWindow(wnd hWnd, bool bEnable);
	//note: the returns value does not say succeeded/failed.
	//	It is non-zero if was disabled, 0 if was enabled.
	//	Declared void to avoid programming errors.
	
	[DllImport("user32.dll", EntryPoint = "FindWindowExW", SetLastError = true)]
	internal static extern wnd _FindWindowEx(wnd hWndParent, wnd hWndChildAfter, string lpszClass, string lpszWindow);
	
	internal static wnd FindWindowEx(wnd wParent = default, wnd wAfter = default, string cn = null, string name = null) {
		//Windows 11 bug: FindWindow[Ex] occasionally returns 0 for an existing window.
		//	Second or third call usually succeeds.
		//	More often fails when there is some activity, eg when Visual Studio compiles/launches an app, or when opening/closing Notepad++.
		//	GetLastError 0.
		//	To reproduce, call every 2 ms, eg to find Notepad by classname.
		//	EnumWindows does not fail.
		//	On Win10 never noticed and cannot reproduce.
		//	Maybe FindWindowEx uses an unsafe loop like with GetWindow. When Z order changes, skips part of windows.
		//	FUTURE: remove this workaround when API fixed. Also in wn::FindWndEx.
		if (osVersion.minWin11) {
			for (int i = 5; --i >= 0;) {
				var w = _FindWindowEx(wParent, wAfter, cn, name);
				if (!w.Is0) return w;
			}
			return default;
		} else {
			return _FindWindowEx(wParent, wAfter, cn, name);
		}
	}
	
	internal struct WNDCLASSEX {
		public int cbSize;
		public uint style;
		public IntPtr lpfnWndProc; //not WNDPROC to avoid auto-marshaling where don't need. Use Marshal.GetFunctionPointerForDelegate/GetDelegateForFunctionPointer.
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public nint hbrBackground;
#pragma warning disable 169
		IntPtr lpszMenuName;
#pragma warning restore 169
		public char* lpszClassName; //not string because CLR would call CoTaskMemFree
		public IntPtr hIconSm;
		
		/// <summary>
		/// If ex <c>null</c>, sets arrow cursor and style <c>CS_VREDRAW | CS_HREDRAW</c>.
		/// </summary>
		public WNDCLASSEX(RWCEtc ex) {
			cbSize = sizeof(WNDCLASSEX);
			if (ex == null) {
				hCursor = LoadCursor(default, MCursor.Arrow);
				style = CS_VREDRAW | CS_HREDRAW;
			} else {
				style = ex.style;
				cbClsExtra = ex.cbClsExtra;
				cbWndExtra = ex.cbWndExtra;
				//hInstance = ex.hInstance;
				hIcon = ex.hIcon;
				if (ex.hCursor != default) hCursor = ex.hCursor; else if (ex.mCursor != default) hCursor = LoadCursor(default, ex.mCursor);
				hbrBackground = ex.hbrBackground;
				hIconSm = ex.hIconSm;
			}
		}
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern ushort RegisterClassEx(in WNDCLASSEX lpwcx);
	
	[DllImport("user32.dll", EntryPoint = "GetClassInfoExW", SetLastError = true)]
	internal static extern ushort GetClassInfoEx(IntPtr hInstance, string lpszClass, ref WNDCLASSEX lpwcx);
	
	[DllImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true)]
	internal static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);
	
	[DllImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true)]
	internal static extern bool UnregisterClass(uint classAtom, IntPtr hInstance);
	
	[DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
	internal static extern wnd CreateWindowEx(WSE dwExStyle, string lpClassName, string lpWindowName, WS dwStyle, int x, int y, int nWidth, int nHeight, wnd hWndParent = default, nint hMenu = 0, IntPtr hInstance = default, nint lpParam = 0);
	
	internal const int CW_USEDEFAULT = 1 << 31;
	
	[DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
	internal static extern nint DefWindowProc(wnd hWnd, int msg, nint wParam, nint lParam);
	
	[DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
	internal static extern nint CallWindowProc(nint lpPrevWndFunc, wnd hWnd, int Msg, nint wParam, nint lParam);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool DestroyWindow(wnd hWnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern void PostQuitMessage(int nExitCode);
	
	[DllImport("user32.dll", EntryPoint = "GetMessage", SetLastError = true)]
	internal static extern int _GetMessage(out MSG lpMsg, wnd hWnd, int wMsgFilterMin, int wMsgFilterMax);
	
	internal static bool GetMessage(out MSG m, wnd hWnd = default, int wMsgFilterMin = 0, int wMsgFilterMax = 0) {
		int r = _GetMessage(out m, hWnd, wMsgFilterMin, wMsgFilterMax);
		if (r == -1) throw new Win32Exception();
		return r != 0;
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool TranslateMessage(in MSG lpMsg);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint DispatchMessage(in MSG lpmsg);
	
	internal const uint PM_NOREMOVE = 0x0;
	internal const uint PM_REMOVE = 0x1;
	internal const uint PM_NOYIELD = 0x2;
	internal const uint PM_QS_SENDMESSAGE = 0x400000;
	internal const uint PM_QS_POSTMESSAGE = 0x980000;
	internal const uint PM_QS_PAINT = 0x200000;
	internal const uint PM_QS_INPUT = 0x1C070000;
	
	[DllImport("user32.dll", EntryPoint = "PeekMessageW", SetLastError = true)]
	internal static extern bool PeekMessage(out MSG lpMsg, wnd hWnd = default, int wMsgFilterMin = 0, int wMsgFilterMax = 0, uint wRemoveMsg = PM_REMOVE);
	
	[DllImport("user32.dll")]
	internal static extern bool WaitMessage();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool ReplyMessage(nint lResult);
	
	internal const int GA_PARENT = 1;
	internal const int GA_ROOT = 2;
	internal const int GA_ROOTOWNER = 3;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd GetAncestor(wnd hwnd, uint GA_X);
	
	[DllImport("user32.dll")]
	internal static extern wnd GetForegroundWindow();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool SetForegroundWindow(wnd hWnd);
	
	internal const int ASFW_ANY = -1;
	
#if true
	[DllImport("user32.dll", EntryPoint = "AllowSetForegroundWindow", SetLastError = true)]
	static extern bool _AllowSetForegroundWindow(int dwProcessId);
	
	internal static bool AllowSetForegroundWindow(int dwProcessId = ASFW_ANY) {
		return _AllowSetForegroundWindow(ASFW_ANY);
		//Ignore dwProcessId, because AllowSetForegroundWindow doc says:
		//"The process specified by the dwProcessId parameter loses the ability to set the foreground window the next time that either the user generates input, unless the input is directed at that process, or the next time a process calls AllowSetForegroundWindow, unless the same process is specified as in the previous call to AllowSetForegroundWindow."
		//If it's true (not tested), if we call AllowSetForegroundWindow with a process id, we disable setforegroundwindow in all other processes.
	}
#else
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool AllowSetForegroundWindow(int dwProcessId = ASFW_ANY);
#endif
	
	internal const uint LSFW_LOCK = 1;
	internal const uint LSFW_UNLOCK = 2;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool LockSetForegroundWindow(uint LSFW_X);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd SetFocus(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern wnd GetFocus();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd SetActiveWindow(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern wnd GetActiveWindow();
	
	internal struct WINDOWPOS {
		public wnd hwnd;
		public wnd hwndInsertAfter;
		public int x;
		public int y;
		public int cx;
		public int cy;
		public SWPFlags flags;
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool SetWindowPos(wnd hWnd, wnd hWndInsertAfter, int X, int Y, int cx, int cy, SWPFlags swpFlags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr BeginDeferWindowPos(int nNumWindows);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, wnd hWnd, wnd hWndInsertAfter, int x, int y, int cx, int cy, SWPFlags uFlags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);
	
	internal struct FLASHWINFO {
		public int cbSize;
		public wnd hwnd;
		public uint dwFlags;
		public int uCount;
		public int dwTimeout;
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool FlashWindowEx(ref FLASHWINFO pfwi);
	
	internal const int GW_OWNER = 4;
	internal const int GW_HWNDPREV = 3;
	internal const int GW_HWNDNEXT = 2;
	internal const int GW_HWNDLAST = 1;
	internal const int GW_HWNDFIRST = 0;
	internal const int GW_ENABLEDPOPUP = 6;
	internal const int GW_CHILD = 5;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd GetWindow(wnd hWnd, int GW_X);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd GetTopWindow(wnd hWnd);
	
	//rejected. Obsolete, confusing.
	//	Returns owner for top-level windows with WS_POPUP style.
	//	Returns 0 for child windows without WS_CHILD style, eg message-only windows and QM2 toolbar owners.
	//[DllImport("user32.dll", SetLastError = true)]
	//internal static extern wnd GetParent(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern wnd GetDesktopWindow();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd GetShellWindow();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd GetLastActivePopup(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern bool IntersectRect(out RECT lprcDst, in RECT lprcSrc1, in RECT lprcSrc2);
	
	[DllImport("user32.dll")]
	internal static extern bool UnionRect(out RECT lprcDst, in RECT lprcSrc1, in RECT lprcSrc2);
	
	/// <summary>
	/// <b>GetPhysicalCursorPos</b>.
	/// </summary>
	/// <remarks>
	/// Gets DPI physical cursor pos, ie always in pixels.
	/// The classic <b>GetCursorPos</b> API behavior is undefined. Sometimes physical, sometimes logical.
	/// Make sure the process is fully DPI-aware.
	/// </remarks>
	[DllImport("user32.dll", EntryPoint = "GetPhysicalCursorPos", SetLastError = true)]
	internal static extern bool GetCursorPos(out POINT lpPoint);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool SetCursorPos(int X, int Y);
	
	[DllImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
	internal static extern IntPtr LoadImage(IntPtr hInst, string name, int type, int cx, int cy, uint LR_X);
	[DllImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true)]
	internal static extern IntPtr LoadImage(IntPtr hInst, nint resId, int type, int cx, int cy, uint LR_X);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr CopyImage(IntPtr h, int type, int cx, int cy, uint flags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool DestroyIcon(IntPtr hIcon);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetWindowRect(wnd hWnd, out RECT lpRect);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetClientRect(wnd hWnd, out RECT lpRect);
	
	internal const uint WPF_SETMINPOSITION = 0x1;
	internal const uint WPF_RESTORETOMAXIMIZED = 0x2;
	internal const uint WPF_ASYNCWINDOWPLACEMENT = 0x4;
	
	internal struct WINDOWPLACEMENT {
		public int length;
		/// <summary><b>WPF_x</b></summary>
		public uint flags;
		public int showCmd;
		public POINT ptMinPosition;
		public POINT ptMaxPosition;
		public RECT rcNormalPosition;
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetWindowPlacement(wnd hWnd, ref WINDOWPLACEMENT lpwndpl);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool SetWindowPlacement(wnd hWnd, in WINDOWPLACEMENT lpwndpl);
	
	internal struct WINDOWINFO {
		public int cbSize;
		public RECT rcWindow;
		public RECT rcClient;
		public WS dwStyle;
		public WSE dwExStyle;
		public uint dwWindowStatus;
		public int cxWindowBorders;
		public int cyWindowBorders;
		public ushort atomWindowType;
		public ushort wCreatorVersion;
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetWindowInfo(wnd hwnd, ref WINDOWINFO pwi);
	
	//[DllImport("user32.dll", SetLastError = true)]
	//internal static extern bool GetTitleBarInfo(wnd hwnd, ref TITLEBARINFO pti);
	
	//internal struct TITLEBARINFO {
	//	public int cbSize;
	//	public RECT r;
	//	public fixed uint rgstate[6];
	//}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsZoomed(wnd hWnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsIconic(wnd hWnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetWindowThreadProcessId(wnd hWnd, int* lpdwProcessId);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsWindowUnicode(wnd hWnd);
	
	[DllImport("user32.dll", EntryPoint = "GetPropW", SetLastError = true)]
	internal static extern nint GetProp(wnd hWnd, string lpString);
	
	[DllImport("user32.dll", EntryPoint = "GetPropW", SetLastError = true)]
	//internal static extern nint GetProp(wnd hWnd, [MarshalAs(UnmanagedType.SysInt)] ushort atom); //exception, must be U2
	internal static extern nint GetProp(wnd hWnd, nint atom);
	
	[DllImport("user32.dll", EntryPoint = "SetPropW", SetLastError = true)]
	internal static extern bool SetProp(wnd hWnd, string lpString, nint hData);
	
	[DllImport("user32.dll", EntryPoint = "SetPropW", SetLastError = true)]
	internal static extern bool SetProp(wnd hWnd, nint atom, nint hData);
	
	[DllImport("user32.dll", EntryPoint = "RemovePropW", SetLastError = true)]
	internal static extern nint RemoveProp(wnd hWnd, string lpString);
	
	[DllImport("user32.dll", EntryPoint = "RemovePropW", SetLastError = true)]
	internal static extern nint RemoveProp(wnd hWnd, nint atom);
	
	internal delegate bool PROPENUMPROCEX(wnd hwnd, IntPtr lpszString, nint hData, nint dwData);
	
	[DllImport("user32.dll", EntryPoint = "EnumPropsExW", SetLastError = true)]
	internal static extern int EnumPropsEx(wnd hWnd, PROPENUMPROCEX lpEnumFunc, nint lParam);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd GetDlgItem(wnd hDlg, int nIDDlgItem);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetDlgCtrlID(wnd hWnd);
	
	internal delegate int WNDENUMPROC(wnd w, void* p);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool EnumWindows(WNDENUMPROC lpEnumFunc, void* p = null);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool EnumThreadWindows(int dwThreadId, WNDENUMPROC lpEnumFunc, void* p = null);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool EnumChildWindows(wnd hWndParent, WNDENUMPROC lpEnumFunc, void* p = null);
	
	[DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true)]
	internal static extern int RegisterWindowMessage(string lpString);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsChild(wnd hWndParent, wnd hWnd);
	
	//rejected. As slow as GetAncestor(GA_PARENT).
	//[DllImport("user32.dll", SetLastError = true), Obsolete("Undocumented API")]
	//internal static extern bool IsTopLevelWindow(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern IntPtr MonitorFromPoint(POINT pt, SODefault dwFlags);
	
	[DllImport("user32.dll")]
	internal static extern IntPtr MonitorFromRect(in RECT lprc, SODefault dwFlags);
	
	[DllImport("user32.dll")]
	internal static extern IntPtr MonitorFromWindow(wnd hwnd, SODefault dwFlags);
	
	internal struct MONITORINFO {
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}
	
	[DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
	static extern bool _GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
	
	internal static bool GetMonitorInfo(IntPtr hMonitor, out MONITORINFO lpmi) {
		lpmi = new MONITORINFO { cbSize = sizeof(MONITORINFO) };
		return _GetMonitorInfo(hMonitor, ref lpmi);
	}
	
	internal delegate bool MONITORENUMPROC(IntPtr hmon, IntPtr hdc, IntPtr r, GCHandle dwData);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MONITORENUMPROC lpfnEnum, GCHandle dwData);
	
	#region GetSystemMetrics, SystemParametersInfo
	
	internal const int SM_YVIRTUALSCREEN = 77;
	internal const int SM_XVIRTUALSCREEN = 76;
	internal const int SM_TABLETPC = 86;
	internal const int SM_SWAPBUTTON = 23;
	internal const int SM_STARTER = 88;
	internal const int SM_SLOWMACHINE = 73;
	internal const int SM_SHUTTINGDOWN = 8192;
	internal const int SM_SHOWSOUNDS = 70;
	internal const int SM_SERVERR2 = 89;
	internal const int SM_SECURE = 44;
	internal const int SM_SAMEDISPLAYFORMAT = 81;
	internal const int SM_RESERVED4 = 27;
	internal const int SM_RESERVED3 = 26;
	internal const int SM_RESERVED2 = 25;
	internal const int SM_RESERVED1 = 24;
	internal const int SM_REMOTESESSION = 4096;
	internal const int SM_REMOTECONTROL = 8193;
	internal const int SM_PENWINDOWS = 41;
	internal const int SM_NETWORK = 63;
	internal const int SM_MOUSEWHEELPRESENT = 75;
	internal const int SM_MOUSEPRESENT = 19;
	internal const int SM_MIDEASTENABLED = 74;
	internal const int SM_MENUDROPALIGNMENT = 40;
	internal const int SM_MEDIACENTER = 87;
	internal const int SM_IMMENABLED = 82;
	internal const int SM_DEBUG = 22;
	internal const int SM_DBCSENABLED = 42;
	internal const int SM_CYVTHUMB = 9;
	internal const int SM_CYVSCROLL = 20;
	internal const int SM_CYVIRTUALSCREEN = 79;
	internal const int SM_CYSMSIZE = 53;
	internal const int SM_CYSMICON = 50;
	internal const int SM_CYSMCAPTION = 51;
	internal const int SM_CYSIZEFRAME = SM_CYFRAME;
	internal const int SM_CYSIZE = 31;
	internal const int SM_CYSCREEN = 1;
	internal const int SM_CYMINTRACK = 35;
	internal const int SM_CYMINSPACING = 48;
	internal const int SM_CYMINIMIZED = 58;
	internal const int SM_CYMIN = 29;
	internal const int SM_CYMENUSIZE = 55;
	internal const int SM_CYMENUCHECK = 72;
	internal const int SM_CYMENU = 15;
	internal const int SM_CYMAXTRACK = 60;
	internal const int SM_CYMAXIMIZED = 62;
	internal const int SM_CYKANJIWINDOW = 18;
	internal const int SM_CYICONSPACING = 39;
	internal const int SM_CYICON = 12;
	internal const int SM_CYHSCROLL = 3;
	internal const int SM_CYFULLSCREEN = 17;
	internal const int SM_CYFRAME = 33;
	internal const int SM_CYFOCUSBORDER = 84;
	internal const int SM_CYFIXEDFRAME = SM_CYDLGFRAME;
	internal const int SM_CYEDGE = 46;
	internal const int SM_CYDRAG = 69;
	internal const int SM_CYDOUBLECLK = 37;
	internal const int SM_CYDLGFRAME = 8;
	internal const int SM_CYCURSOR = 14;
	internal const int SM_CYCAPTION = 4;
	internal const int SM_CYBORDER = 6;
	internal const int SM_CXVSCROLL = 2;
	internal const int SM_CXVIRTUALSCREEN = 78;
	internal const int SM_CXSMSIZE = 52;
	internal const int SM_CXSMICON = 49;
	internal const int SM_CXSIZEFRAME = SM_CXFRAME;
	internal const int SM_CXSIZE = 30;
	internal const int SM_CXSCREEN = 0;
	internal const int SM_CXMINTRACK = 34;
	internal const int SM_CXMINSPACING = 47;
	internal const int SM_CXMINIMIZED = 57;
	internal const int SM_CXMIN = 28;
	internal const int SM_CXMENUSIZE = 54;
	internal const int SM_CXMENUCHECK = 71;
	internal const int SM_CXMAXTRACK = 59;
	internal const int SM_CXMAXIMIZED = 61;
	internal const int SM_CXICONSPACING = 38;
	internal const int SM_CXICON = 11;
	internal const int SM_CXHTHUMB = 10;
	internal const int SM_CXHSCROLL = 21;
	internal const int SM_CXFULLSCREEN = 16;
	internal const int SM_CXFRAME = 32;
	internal const int SM_CXFOCUSBORDER = 83;
	internal const int SM_CXFIXEDFRAME = SM_CXDLGFRAME;
	internal const int SM_CXEDGE = 45;
	internal const int SM_CXDRAG = 68;
	internal const int SM_CXDOUBLECLK = 36;
	internal const int SM_CXDLGFRAME = 7;
	internal const int SM_CXCURSOR = 13;
	internal const int SM_CXBORDER = 5;
	internal const int SM_CMOUSEBUTTONS = 43;
	internal const int SM_CMONITORS = 80;
	internal const int SM_CMETRICS = 90;
	internal const int SM_CLEANBOOT = 67;
	internal const int SM_CARETBLINKINGENABLED = 8194;
	internal const int SM_ARRANGE = 56;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetSystemMetrics(int nIndex);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetSystemMetricsForDpi(int nIndex, int dpi);
	
	internal const int SPI_SETWORKAREA = 47;
	internal const int SPI_SETWHEELSCROLLLINES = 105;
	internal const int SPI_SETUIEFFECTS = 4159;
	internal const int SPI_SETTOOLTIPFADE = 4121;
	internal const int SPI_SETTOOLTIPANIMATION = 4119;
	internal const int SPI_SETTOGGLEKEYS = 53;
	internal const int SPI_SETSTICKYKEYS = 59;
	internal const int SPI_SETSOUNDSENTRY = 65;
	internal const int SPI_SETSNAPTODEFBUTTON = 96;
	internal const int SPI_SETSHOWSOUNDS = 57;
	internal const int SPI_SETSHOWIMEUI = 111;
	internal const int SPI_SETSERIALKEYS = 63;
	internal const int SPI_SETSELECTIONFADE = 4117;
	internal const int SPI_SETSCREENSAVETIMEOUT = 15;
	internal const int SPI_SETSCREENSAVERRUNNING = 97;
	internal const int SPI_SETSCREENSAVEACTIVE = 17;
	internal const int SPI_SETSCREENREADER = 71;
	internal const int SPI_SETPOWEROFFTIMEOUT = 82;
	internal const int SPI_SETPOWEROFFACTIVE = 86;
	internal const int SPI_SETPENWINDOWS = 49;
	internal const int SPI_SETNONCLIENTMETRICS = 42;
	internal const int SPI_SETMOUSEVANISH = 4129;
	internal const int SPI_SETMOUSETRAILS = 93;
	internal const int SPI_SETMOUSESPEED = 113;
	internal const int SPI_SETMOUSESONAR = 4125;
	internal const int SPI_SETMOUSEKEYS = 55;
	internal const int SPI_SETMOUSEHOVERWIDTH = 99;
	internal const int SPI_SETMOUSEHOVERTIME = 103;
	internal const int SPI_SETMOUSEHOVERHEIGHT = 101;
	internal const int SPI_SETMOUSECLICKLOCKTIME = 8201;
	internal const int SPI_SETMOUSECLICKLOCK = 4127;
	internal const int SPI_SETMOUSEBUTTONSWAP = 33;
	internal const int SPI_SETMOUSE = 4;
	internal const int SPI_SETMINIMIZEDMETRICS = 44;
	internal const int SPI_SETMENUUNDERLINES = SPI_SETKEYBOARDCUES;
	internal const int SPI_SETMENUSHOWDELAY = 107;
	internal const int SPI_SETMENUFADE = 4115;
	internal const int SPI_SETMENUDROPALIGNMENT = 28;
	internal const int SPI_SETMENUANIMATION = 4099;
	internal const int SPI_SETLOWPOWERTIMEOUT = 81;
	internal const int SPI_SETLOWPOWERACTIVE = 85;
	internal const int SPI_SETLISTBOXSMOOTHSCROLLING = 4103;
	internal const int SPI_SETLANGTOGGLE = 91;
	internal const int SPI_SETKEYBOARDSPEED = 11;
	internal const int SPI_SETKEYBOARDPREF = 69;
	internal const int SPI_SETKEYBOARDDELAY = 23;
	internal const int SPI_SETKEYBOARDCUES = 4107;
	internal const int SPI_SETICONTITLEWRAP = 26;
	internal const int SPI_SETICONTITLELOGFONT = 34;
	internal const int SPI_SETICONS = 88;
	internal const int SPI_SETICONMETRICS = 46;
	internal const int SPI_SETHOTTRACKING = 4111;
	internal const int SPI_SETHIGHCONTRAST = 67;
	internal const int SPI_SETHANDHELD = 78;
	internal const int SPI_SETGRIDGRANULARITY = 19;
	internal const int SPI_SETGRADIENTCAPTIONS = 4105;
	internal const int SPI_SETFOREGROUNDLOCKTIMEOUT = 8193;
	internal const int SPI_SETFOREGROUNDFLASHCOUNT = 8197;
	internal const int SPI_SETFONTSMOOTHINGTYPE = 8203;
	internal const int SPI_SETFONTSMOOTHINGORIENTATION = 8211;
	internal const int SPI_SETFONTSMOOTHINGCONTRAST = 8205;
	internal const int SPI_SETFONTSMOOTHING = 75;
	internal const int SPI_SETFOCUSBORDERWIDTH = 8207;
	internal const int SPI_SETFOCUSBORDERHEIGHT = 8209;
	internal const int SPI_SETFLATMENU = 4131;
	internal const int SPI_SETFILTERKEYS = 51;
	internal const int SPI_SETFASTTASKSWITCH = 36;
	internal const int SPI_SETDROPSHADOW = 4133;
	internal const int SPI_SETDRAGWIDTH = 76;
	internal const int SPI_SETDRAGHEIGHT = 77;
	internal const int SPI_SETDRAGFULLWINDOWS = 37;
	internal const int SPI_SETDOUBLECLKWIDTH = 29;
	internal const int SPI_SETDOUBLECLKHEIGHT = 30;
	internal const int SPI_SETDOUBLECLICKTIME = 32;
	internal const int SPI_SETDESKWALLPAPER = 20;
	internal const int SPI_SETDESKPATTERN = 21;
	internal const int SPI_SETDEFAULTINPUTLANG = 90;
	internal const int SPI_SETCURSORSHADOW = 4123;
	internal const int SPI_SETCURSORS = 87;
	internal const int SPI_SETCOMBOBOXANIMATION = 4101;
	internal const int SPI_SETCARETWIDTH = 8199;
	internal const int SPI_SETBORDER = 6;
	internal const int SPI_SETBLOCKSENDINPUTRESETS = 4135;
	internal const int SPI_SETBEEP = 2;
	internal const int SPI_SETANIMATION = 73;
	internal const int SPI_SETACTIVEWNDTRKZORDER = 4109;
	internal const int SPI_SETACTIVEWNDTRKTIMEOUT = 8195;
	internal const int SPI_SETACTIVEWINDOWTRACKING = 4097;
	internal const int SPI_SETACCESSTIMEOUT = 61;
	internal const int SPI_LANGDRIVER = 12;
	internal const int SPI_ICONVERTICALSPACING = 24;
	internal const int SPI_ICONHORIZONTALSPACING = 13;
	internal const int SPI_GETWORKAREA = 48;
	internal const int SPI_GETWINDOWSEXTENSION = 92;
	internal const int SPI_GETWHEELSCROLLLINES = 104;
	internal const int SPI_GETUIEFFECTS = 4158;
	internal const int SPI_GETTOOLTIPFADE = 4120;
	internal const int SPI_GETTOOLTIPANIMATION = 4118;
	internal const int SPI_GETTOGGLEKEYS = 52;
	internal const int SPI_GETSTICKYKEYS = 58;
	internal const int SPI_GETSOUNDSENTRY = 64;
	internal const int SPI_GETSNAPTODEFBUTTON = 95;
	internal const int SPI_GETSHOWSOUNDS = 56;
	internal const int SPI_GETSHOWIMEUI = 110;
	internal const int SPI_GETSERIALKEYS = 62;
	internal const int SPI_GETSELECTIONFADE = 4116;
	internal const int SPI_GETSCREENSAVETIMEOUT = 14;
	internal const int SPI_GETSCREENSAVERRUNNING = 114;
	internal const int SPI_GETSCREENSAVEACTIVE = 16;
	internal const int SPI_GETSCREENREADER = 70;
	internal const int SPI_GETPOWEROFFTIMEOUT = 80;
	internal const int SPI_GETPOWEROFFACTIVE = 84;
	internal const int SPI_GETNONCLIENTMETRICS = 41;
	internal const int SPI_GETMOUSEVANISH = 4128;
	internal const int SPI_GETMOUSETRAILS = 94;
	internal const int SPI_GETMOUSESPEED = 112;
	internal const int SPI_GETMOUSESONAR = 4124;
	internal const int SPI_GETMOUSEKEYS = 54;
	internal const int SPI_GETMOUSEHOVERWIDTH = 98;
	internal const int SPI_GETMOUSEHOVERTIME = 102;
	internal const int SPI_GETMOUSEHOVERHEIGHT = 100;
	internal const int SPI_GETMOUSECLICKLOCKTIME = 8200;
	internal const int SPI_GETMOUSECLICKLOCK = 4126;
	internal const int SPI_GETMOUSE = 3;
	internal const int SPI_GETMINIMIZEDMETRICS = 43;
	internal const int SPI_GETMENUUNDERLINES = SPI_GETKEYBOARDCUES;
	internal const int SPI_GETMENUSHOWDELAY = 106;
	internal const int SPI_GETMENUFADE = 4114;
	internal const int SPI_GETMENUDROPALIGNMENT = 27;
	internal const int SPI_GETMENUANIMATION = 4098;
	internal const int SPI_GETLOWPOWERTIMEOUT = 79;
	internal const int SPI_GETLOWPOWERACTIVE = 83;
	internal const int SPI_GETLISTBOXSMOOTHSCROLLING = 4102;
	internal const int SPI_GETKEYBOARDSPEED = 10;
	internal const int SPI_GETKEYBOARDPREF = 68;
	internal const int SPI_GETKEYBOARDDELAY = 22;
	internal const int SPI_GETKEYBOARDCUES = 4106;
	internal const int SPI_GETICONTITLEWRAP = 25;
	internal const int SPI_GETICONTITLELOGFONT = 31;
	internal const int SPI_GETICONMETRICS = 45;
	internal const int SPI_GETHOTTRACKING = 4110;
	internal const int SPI_GETHIGHCONTRAST = 66;
	internal const int SPI_GETGRIDGRANULARITY = 18;
	internal const int SPI_GETGRADIENTCAPTIONS = 4104;
	internal const int SPI_GETFOREGROUNDLOCKTIMEOUT = 8192;
	internal const int SPI_GETFOREGROUNDFLASHCOUNT = 8196;
	internal const int SPI_GETFONTSMOOTHINGTYPE = 8202;
	internal const int SPI_GETFONTSMOOTHINGORIENTATION = 8210;
	internal const int SPI_GETFONTSMOOTHINGCONTRAST = 8204;
	internal const int SPI_GETFONTSMOOTHING = 74;
	internal const int SPI_GETFOCUSBORDERWIDTH = 8206;
	internal const int SPI_GETFOCUSBORDERHEIGHT = 8208;
	internal const int SPI_GETFLATMENU = 4130;
	internal const int SPI_GETFILTERKEYS = 50;
	internal const int SPI_GETFASTTASKSWITCH = 35;
	internal const int SPI_GETDROPSHADOW = 4132;
	internal const int SPI_GETDRAGFULLWINDOWS = 38;
	internal const int SPI_GETDESKWALLPAPER = 115;
	internal const int SPI_GETDEFAULTINPUTLANG = 89;
	internal const int SPI_GETCURSORSHADOW = 4122;
	internal const int SPI_GETCOMBOBOXANIMATION = 4100;
	internal const int SPI_GETCARETWIDTH = 8198;
	internal const int SPI_GETBORDER = 5;
	internal const int SPI_GETBLOCKSENDINPUTRESETS = 4134;
	internal const int SPI_GETBEEP = 1;
	internal const int SPI_GETANIMATION = 72;
	internal const int SPI_GETACTIVEWNDTRKZORDER = 4108;
	internal const int SPI_GETACTIVEWNDTRKTIMEOUT = 8194;
	internal const int SPI_GETACTIVEWINDOWTRACKING = 4096;
	internal const int SPI_GETACCESSTIMEOUT = 60;
	internal const int SPI_GETMOUSEWHEELROUTING = 0x201C;
	
	internal const uint SPIF_UPDATEINIFILE = 0x1;
	internal const uint SPIF_SENDCHANGE = 0x2;
	
	/// <summary>
	/// Gets or sets any value. This is the direct API call.
	/// </summary>
	[DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
	internal static extern bool SystemParametersInfo(uint uiAction, int uiParam, void* pvParam, uint fWinIni = 0);
	
	/// <summary>
	/// Gets 32-bit integer value. Returns <i>def</i> if failed.
	/// </summary>
	internal static int SystemParametersInfo(uint uiAction, int def) {
		int r = 0;
		return SystemParametersInfo(uiAction, 0, &r) ? r : def;
	}
	
	/// <summary>
	/// Gets <b>BOOL</b> value. Returns <c>false</c> if failed.
	/// </summary>
	internal static bool SystemParametersInfo(uint uiAction) {
		int r = 0;
		return SystemParametersInfo(uiAction, 0, &r) && r != 0;
	}
	
	//internal static bool SystemParametersInfo<T>(uint uiAction, ref T pvParam) where T : unmanaged {
	//	fixed (T* p = &pvParam) return SystemParametersInfo(uiAction, sizeof(T), p, 0);
	//}
	
	/// <summary>
	/// Sets value.
	/// </summary>
	internal static bool SystemParametersInfo(uint uiAction, int uiParam, void* pvParam, bool save, bool notify) {
		uint f = 0;
		if (save) f |= 1; //SPIF_UPDATEINIFILE
		if (notify) f |= 2; //SPIF_SENDCHANGE
		return SystemParametersInfo(uiAction, uiParam, pvParam, f);
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool SystemParametersInfoForDpi(uint uiAction, int uiParam, void* pvParam, uint fWinIni, int dpi);
	
	#endregion
	
	/// <summary>
	/// <b>WindowFromPhysicalPoint</b>. On Win8.1+ it is the same as <b>WindowFromPoint</b>.
	/// </summary>
	[DllImport("user32.dll", EntryPoint = "WindowFromPhysicalPoint")]
	internal static extern wnd WindowFromPoint(POINT pt);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool ScreenToClient(wnd hWnd, ref POINT lpPoint);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool ClientToScreen(wnd hWnd, ref POINT lpPoint);
	
	//internal static bool ClientToScreenIgnoreRtl(wnd w, ref POINT p)
	//{
	//	if(!w.HasExStyle(WSE.LAYOUTRTL)) return ClientToScreen(w, ref p);
	//	if(!GetClientRect(w, out var r) || !MapWindowPoints(w, default, ref r, out _)) return false;
	//	p.Offset(r.left, r.top);
	//	return true;
	//}
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern int MapWindowPoints(wnd hWndFrom, wnd hWndTo, void* lpPoints, int cPoints);
	
	internal static bool MapWindowPoints(wnd wFrom, wnd wTo, void* points, int cPoints, out int ret) {
		lastError.clear();
		ret = MapWindowPoints(wFrom, wTo, points, cPoints);
		return ret != 0 ? true : lastError.code == 0;
	}
	
	internal static bool MapWindowPoints(wnd wFrom, wnd wTo, ref RECT r, out int ret) {
		fixed (void* u = &r) return MapWindowPoints(wFrom, wTo, u, 2, out ret);
	}
	
	internal static bool MapWindowPoints(wnd wFrom, wnd wTo, ref POINT p, out int ret) {
		fixed (void* u = &p) return MapWindowPoints(wFrom, wTo, u, 1, out ret);
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetGUIThreadInfo(int idThread, ref GUITHREADINFO pgui);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);
	
	internal const uint KEYEVENTF_EXTENDEDKEY = 0x1;
	internal const uint KEYEVENTF_KEYUP = 0x2;
	internal const uint KEYEVENTF_UNICODE = 0x4;
	internal const uint KEYEVENTF_SCANCODE = 0x8;
	
	internal struct INPUTK {
		nint _type;
		public ushort wVk;
		public ushort wScan;
		public uint dwFlags;
		public int time;
		public nint dwExtraInfo;
#pragma warning disable 414 //never used
		int _u1, _u2; //need INPUT size
#pragma warning restore 414
		
		public INPUTK(KKey vk, ushort sc, uint flags = 0) {
			_type = INPUT_KEYBOARD; dwExtraInfo = AuExtraInfo;
			wVk = (ushort)vk; wScan = sc; dwFlags = flags;
			time = 0; _u2 = _u1 = 0;
			Debug.Assert(sizeof(INPUTK) == sizeof(INPUTM));
		}
		
		public void Set(KKey vk, ushort sc, uint flags = 0) {
			_type = INPUT_KEYBOARD; dwExtraInfo = AuExtraInfo;
			wVk = (ushort)vk; wScan = sc; dwFlags = flags;
		}
		
		//public void InitCommonFields()
		//{
		//	_type = INPUT_KEYBOARD; dwExtraInfo = AuExtraInfo;
		//}
		
		const int INPUT_KEYBOARD = 1;
	}
	
	[Flags]
	internal enum IMFlags : uint {
		Move = 1,
		LeftDown = 2, LeftUp = 4,
		RightDown = 8, RightUp = 16,
		MiddleDown = 32, MiddleUp = 64,
		XDown = 0x80, XUp = 0x100,
		Wheel = 0x0800, HWheel = 0x01000,
		NoCoalesce = 0x2000,
		VirtualdDesktop = 0x4000,
		Absolute = 0x8000,
		//not API
		X1 = 0x1000000,
		X2 = 0x2000000,
	};
	
	internal struct INPUTM {
		nint _type;
		public int dx;
		public int dy;
		public int mouseData;
		public IMFlags dwFlags;
		public int time;
		public nint dwExtraInfo;
		
		public INPUTM(IMFlags flags, int x = 0, int y = 0, int data = 0) {
			_type = INPUT_MOUSE;
			dx = x; dy = y; dwFlags = flags; mouseData = data;
			time = 0; dwExtraInfo = AuExtraInfo;
		}
		
		const int INPUT_MOUSE = 0;
	}
	
	/// <summary>
	/// Extra info value of key and mouse events sent by functions of this library.
	/// </summary>
	internal const int AuExtraInfo = 0x71427fa5;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int SendInput(int cInputs, void* pInputs, int cbSize);
	//tested: Returns 0 when: invalid argument; other desktop active.
	//	Does not return 0 when: UAC (documented); BlockInput; ClipCursor.
	
	/// <exception cref="InputDesktopException"></exception>
	internal static void SendInput(INPUTK* ip, int n = 1) {
		if (n != SendInput(n, ip, sizeof(INPUTK))) {
			InputDesktopException.ThrowIfBadDesktop("*send keyboard input.");
			throw new AuException("*send keyboard input.");
			//tested: if bad input desktop, GetLastError returns 'access denied'.
		}
	}
	
	/// <exception cref="InputDesktopException"></exception>
	internal static void SendInput(INPUTM* ip, int n = 1) {
		if (n != SendInput(n, ip, sizeof(INPUTM))) {
			InputDesktopException.ThrowIfBadDesktop("*send mouse input.");
			throw new AuException("*send mouse input.");
		}
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsHungAppWindow(wnd hwnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool SetLayeredWindowAttributes(wnd hwnd, uint crKey, byte bAlpha, uint dwFlags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetLayeredWindowAttributes(wnd hwnd, out uint pcrKey, out byte pbAlpha, out uint pdwFlags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr CreateIcon(IntPtr hInstance, int nWidth, int nHeight, byte cPlanes, byte cBitsPixel, byte[] lpbANDbits, byte[] lpbXORbits);
	
	[DllImport("user32.dll", EntryPoint = "LoadCursorW", SetLastError = true)]
	internal static extern IntPtr LoadCursor(IntPtr hInstance, MCursor cursorId);
	
	internal delegate void TIMERPROC(wnd param1, int param2, nint param3, uint param4);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint SetTimer(wnd hWnd, nint nIDEvent, int uElapse, TIMERPROC lpTimerFunc);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool KillTimer(wnd hWnd, nint uIDEvent);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd SetParent(wnd hWndChild, wnd hWndNewParent);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool AdjustWindowRectEx(ref RECT lpRect, WS dwStyle, bool bMenu, WSE dwExStyle);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool AdjustWindowRectExForDpi(ref RECT lpRect, WS dwStyle, bool bMenu, WSE dwExStyle, int dpi);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool ChangeWindowMessageFilter(int message, uint dwFlag);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern short GetKeyState(int nVirtKey); //not KKey because it is :byte
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern short GetAsyncKeyState(int vKey); //not KKey because it is :byte
	
	internal const uint MOD_ALT = 0x1;
	internal const uint MOD_CONTROL = 0x2;
	internal const uint MOD_SHIFT = 0x4;
	internal const uint MOD_WIN = 0x8;
	internal const uint MOD_NOREPEAT = 0x4000;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool RegisterHotKey(wnd hWnd, int id, uint fsModifiers, KKey vk);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool UnregisterHotKey(wnd hWnd, int id);
	
	internal const uint MWMO_WAITALL = 0x1;
	internal const uint MWMO_ALERTABLE = 0x2;
	internal const uint MWMO_INPUTAVAILABLE = 0x4;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int MsgWaitForMultipleObjectsEx(int nCount, IntPtr* pHandles, int dwMilliseconds, uint dwWakeMask, uint MWMO_Flags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool InvalidateRect(wnd hWnd, RECT* lpRect, bool bErase);
	internal static bool InvalidateRect(wnd hWnd, bool bErase = false) => InvalidateRect(hWnd, null, bErase);
	internal static bool InvalidateRect(wnd hWnd, RECT r, bool bErase = false) => InvalidateRect(hWnd, &r, bErase);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool ValidateRect(wnd hWnd, RECT* lpRect);
	internal static bool ValidateRect(wnd hWnd) => ValidateRect(hWnd, null);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetUpdateRect(wnd hWnd, out RECT lpRect, bool bErase);
	
	internal const int ERROR = 0;
	internal const int NULLREGION = 1;
	internal const int SIMPLEREGION = 2;
	internal const int COMPLEXREGION = 3;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetUpdateRgn(wnd hWnd, IntPtr hRgn, bool bErase);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool InvalidateRgn(wnd hWnd, IntPtr hRgn, bool bErase);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool DragDetect(wnd hwnd, POINT pt);
	
	[DllImport("user32.dll")]
	internal static extern IntPtr GetCursor();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr SetCursor(IntPtr hCursor);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern wnd SetCapture(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern wnd GetCapture();
	
	[DllImport("user32.dll")]
	internal static extern bool ReleaseCapture();
	
	//[DllImport("user32.dll", EntryPoint = "CharLowerBuffW")]
	//internal static unsafe extern int CharLowerBuff(char* lpsz, int cchLength);
	
	//[DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int wsprintfW(char* lpOut1024, string lpFmt, __arglist);
	//note: with __arglist always returns 0. Could instead use void*, but then much work to properly pack arguments.
	
	//tested speed (time %) of various formatting functions, with two int and one string arg, with converting to string:
	//	StringBuilder.Append + int.ToString(CultureInfo.InvariantCulture): 85% - FASTEST
	//	StringBuilder.Append: 100%
	//	StringBuilder.AppendFormat + int.ToString() (avoid int boxing): 140%
	//	StringBuilder.AppendFormat: 150%
	//	$"{var} string": 160% (probably uses StringBuilder)
	//	wsprintfW (user32.dll): 150%
	//	_snwprintf (msvcrt.dll): 500% - SLOWEST
	
	[DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
	internal static extern int wsprintfA(byte* lpOut1024, string lpFmt, __arglist);
	
	[DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int wsprintfW(char* lpOut1024, string lpFmt, __arglist);
	
	internal struct PAINTSTRUCT {
		public IntPtr hdc;
		public bool fErase;
		public RECT rcPaint;
		public bool fRestore;
		public bool fIncUpdate;
		fixed byte rgbReserved[32];
	}
	
	[DllImport("user32.dll")]
	internal static extern IntPtr BeginPaint(wnd hWnd, out PAINTSTRUCT lpPaint);
	
	[DllImport("user32.dll")]
	internal static extern bool EndPaint(wnd hWnd, in PAINTSTRUCT lpPaint);
	
	[DllImport("user32.dll")]
	internal static extern bool UpdateWindow(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern nint GetKeyboardLayout(int idThread);
	
	[DllImport("user32.dll", EntryPoint = "MapVirtualKeyExW")]
	internal static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, nint dwhkl);
	
	[DllImport("user32.dll", EntryPoint = "VkKeyScanExW")]
	internal static extern short VkKeyScanEx(char ch, nint dwhkl);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool OpenClipboard(wnd hWndNewOwner);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool CloseClipboard();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool EmptyClipboard();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr SetClipboardData(int uFormat, IntPtr hMem);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr GetClipboardData(int uFormat);
	
	//[DllImport("user32.dll", SetLastError = true)]
	//internal static extern wnd SetClipboardViewer(wnd hWndNewViewer);
	
	//[DllImport("user32.dll")]
	//internal static extern bool ChangeClipboardChain(wnd hWndRemove, wnd hWndNewNext);
	
	[DllImport("user32.dll")]
	internal static extern wnd GetOpenClipboardWindow();
	
	[DllImport("user32.dll")]
	internal static extern uint GetClipboardSequenceNumber();
	
	[DllImport("user32.dll", EntryPoint = "RegisterClipboardFormatW")]
	internal static extern int RegisterClipboardFormat(string lpszFormat);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool AddClipboardFormatListener(wnd hwnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool RemoveClipboardFormatListener(wnd hwnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int EnumClipboardFormats(int format);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool IsClipboardFormatAvailable(int format);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetPriorityClipboardFormat(int[] paFormatPriorityList, int cFormats);
	
	[DllImport("user32.dll", EntryPoint = "GetClipboardFormatNameW", SetLastError = true)]
	internal static unsafe extern int GetClipboardFormatName(int format, char* lpszFormatName, int cchMaxCount);
	
	[DllImport("user32.dll")]
	internal static extern int GetDoubleClickTime();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, int istepIfAniCur = 0, IntPtr hbrFlickerFreeDraw = default, uint diFlags = 3); //DI_NORMAL
	
	internal const uint CURSOR_SHOWING = 0x1;
	
	internal struct CURSORINFO {
		public int cbSize;
		public uint flags;
		public IntPtr hCursor;
		public POINT ptScreenPos;
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetCursorInfo(ref CURSORINFO pci);
	
	internal struct ICONINFO : IDisposable {
		public bool fIcon;
		public int xHotspot;
		public int yHotspot;
		public IntPtr hbmMask;
		public IntPtr hbmColor;
		
		public ICONINFO(IntPtr hIcon) {
			GetIconInfo(hIcon, out this);
			//never mind if failed. Then hbm members are default, and caller can either check it or simply let other API fail.
		}
		
		public void Dispose() {
			if (hbmMask != default) DeleteObject(hbmMask);
			if (hbmColor != default) DeleteObject(hbmColor);
		}
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
	//tested: GetIconInfoEx gets resource info only for icons loaded from a module loaded in this process.
	
	internal struct BITMAP {
		public int bmType;
		public int bmWidth;
		public int bmHeight;
		public int bmWidthBytes;
		public ushort bmPlanes;
		public ushort bmBitsPixel;
		public IntPtr bmBits;
	}
	
	internal const int WH_MSGFILTER = -1;
	internal const int WH_KEYBOARD = 2;
	internal const int WH_GETMESSAGE = 3;
	internal const int WH_CALLWNDPROC = 4;
	internal const int WH_CBT = 5;
	//internal const int WH_SYSMSGFILTER = 6; //hook proc must be in dll
	internal const int WH_MOUSE = 7;
	internal const int WH_DEBUG = 9;
	internal const int WH_SHELL = 10;
	internal const int WH_FOREGROUNDIDLE = 11;
	internal const int WH_CALLWNDPROCRET = 12;
	internal const int WH_KEYBOARD_LL = 13;
	internal const int WH_MOUSE_LL = 14;
	
	internal delegate nint HOOKPROC(int code, nint wParam, nint lParam);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr SetWindowsHookEx(int WH_X, HOOKPROC lpfn, IntPtr hMod, int dwThreadId);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool UnhookWindowsHookEx(IntPtr hhk);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint CallNextHookEx(IntPtr hhk, int nCode, nint wParam, nint lParam);
	
	internal const uint LLKHF_EXTENDED = 0x1;
	internal const uint LLKHF_INJECTED = 0x10;
	internal const uint LLKHF_ALTDOWN = 0x20;
	internal const uint LLKHF_UP = 0x80;
	
	internal struct KBDLLHOOKSTRUCT {
		public uint vkCode;
		public uint scanCode;
		public uint flags;
		public int time;
		public nint dwExtraInfo;
		
		public bool IsUp => 0 != (flags & LLKHF_UP);
		
		/// <summary>
		/// <c>true</c> if the event was generated by software.
		/// </summary>
		public bool IsInjected => 0 != (flags & LLKHF_INJECTED);
		
		/// <summary>
		/// <c>true</c> if the event was generated by functions of this library.
		/// </summary>
		public bool IsInjectedByAu => 0 != (flags & LLKHF_INJECTED) && dwExtraInfo == AuExtraInfo;
		//CONSIDER: also add IsInjectedByAuOrQM2. And let QM2 recognize the extra info too. Or let they use the same extra info; probably bad idea.
		
		/// <summary>
		/// The <c>set</c> function adds or removes flag 0x80000000.
		/// The <c>get</c> function returns <c>true</c> if flag 0x80000000 is set.
		/// </summary>
		public bool BlockEvent {
			get => 0 != (flags & 0x80000000);
			set { if (value) flags |= 0x80000000; else flags &= ~0x80000000; }
		}
	}
	
	internal const uint LLMHF_INJECTED = 0x1;
	
	internal struct MSLLHOOKSTRUCT {
		public POINT pt;
		public uint mouseData;
		public uint flags;
		public int time;
		public nint dwExtraInfo;
		
		/// <summary>
		/// <c>true</c> if the event was generated by software.
		/// </summary>
		public bool IsInjected => 0 != (flags & LLMHF_INJECTED);
		
		/// <summary>
		/// <c>true</c> if the event was generated by functions of this library.
		/// </summary>
		public bool IsInjectedByAu => 0 != (flags & LLMHF_INJECTED) && dwExtraInfo == AuExtraInfo;
		
		/// <summary>
		/// The <c>set</c> function adds or removes flag 0x80000000.
		/// The <c>get</c> function returns <c>true</c> if flag 0x80000000 is set.
		/// </summary>
		public bool BlockEvent {
			get => 0 != (flags & 0x80000000);
			set { if (value) flags |= 0x80000000; else flags &= ~0x80000000; }
		}
	}
	
	internal const int HC_NOREMOVE = 3;
	
	internal delegate void WINEVENTPROC(IntPtr hWinEventHook, EEvent event_, wnd hwnd, EObjid idObject, int idChild, int idEventThread, int dwmsEventTime);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr SetWinEventHook(EEvent eventMin, EEvent eventMax, IntPtr hmodWinEventProc, WINEVENTPROC pfnWinEventProc, int idProcess, int idThread, EHookFlags dwFlags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);
	
	[Flags]
	internal enum AnimationFlags : uint {
		Roll = 0x0000, // Uses a roll animation.
		HorizontalPositive = 0x00001, // Animates the window from left to right. This flag can be used with roll or slide animation.
		HorizontalNegative = 0x00002, // Animates the window from right to left. This flag can be used with roll or slide animation.
		VerticalPositive = 0x00004, // Animates the window from top to bottom. This flag can be used with roll or slide animation.
		VerticalNegative = 0x00008, // Animates the window from bottom to top. This flag can be used with roll or slide animation.
		Center = 0x00010, // Makes the window appear to collapse inward if Hide is used or expand outward if the Hide is not used.
		Hide = 0x10000, // Hides the window. By default, the window is shown.
		Activate = 0x20000, // Activates the window.
		Slide = 0x40000, // Uses a slide animation. By default, roll animation is used.
		Blend = 0x80000, // Uses a fade effect. This flag can be used only with a top-level window.
		Mask = 0xfffff,
	}
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool AnimateWindow(wnd hWnd, int dwTime, AnimationFlags dwFlags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool GetCaretPos(out POINT lpPoint);
	
	internal static bool GetCaretPosInScreen_(out POINT p) => GetCaretPos(out p) && GetFocus().MapClientToScreen(ref p);
	
	[DllImport("user32.dll")]
	internal static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte* lpKeyState, char* pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
	
	internal const uint PW_CLIENTONLY = 0x1;
	internal const uint PW_RENDERFULLCONTENT = 0x2;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool PrintWindow(wnd hwnd, IntPtr hdcBlt, uint nFlags);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr GetDC(wnd hWnd);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr GetWindowDC(wnd hWnd);
	
	[DllImport("user32.dll")] //note: no SetLastError = true
	internal static extern int ReleaseDC(wnd hWnd, IntPtr hDC);
	
	[DllImport("user32.dll")]
	internal static extern int FillRect(IntPtr hDC, in RECT lprc, nint hbr);
	
	[DllImport("user32.dll")]
	internal static extern int FrameRect(IntPtr hDC, in RECT lprc, IntPtr hbr);
	
	internal const uint RDW_INVALIDATE = 0x1;
	internal const uint RDW_ERASE = 0x4;
	internal const uint RDW_ALLCHILDREN = 0x80;
	internal const uint RDW_FRAME = 0x400;
	
	[DllImport("user32.dll")]
	internal static extern bool RedrawWindow(wnd hWnd, RECT* lprcUpdate = null, IntPtr hrgnUpdate = default, uint flags = 0);
	
	/// <param name="flags"><b>Au.Controls.PopupAlignment</b></param>
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool CalculatePopupWindowPosition(in POINT anchorPoint, in SIZE windowSize, uint flags, in RECT excludeRect, out RECT popupWindowPosition);
	
	[DllImport("user32.dll")]
	internal static extern int MenuItemFromPoint(wnd hWnd, IntPtr hMenu, POINT ptScreen);
	
	[DllImport("user32.dll")]
	internal static extern int GetMenuItemID(IntPtr hMenu, int nPos);
	
	internal struct MENUITEMINFO {
		public int cbSize;
		public uint fMask;
		public uint fType;
		public uint fState;
		public int wID;
		public IntPtr hSubMenu;
		public IntPtr hbmpChecked;
		public IntPtr hbmpUnchecked;
		public nint dwItemData;
		public char* dwTypeData;
		public int cch;
		public IntPtr hbmpItem;
		
		public MENUITEMINFO(uint miim) {
			cbSize = sizeof(MENUITEMINFO);
			fMask = miim;
		}
	}
	
	[DllImport("user32.dll", EntryPoint = "GetMenuItemInfoW")]
	internal static extern bool GetMenuItemInfo(IntPtr hmenu, int item, bool fByPosition, ref MENUITEMINFO lpmii);
	
	internal const uint MIIM_STRING = 0x40;
	
	[DllImport("user32.dll")]
	internal static extern IntPtr GetSystemMenu(wnd hWnd, bool bRevert);
	
	[DllImport("user32.dll")]
	internal static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
	
	internal const uint MF_GRAYED = 0x1;
	
	internal const uint SIF_RANGE = 0x1;
	internal const uint SIF_PAGE = 0x2;
	internal const uint SIF_POS = 0x4;
	internal const uint SIF_TRACKPOS = 0x10;
	internal const int SB_LINEUP = 0;
	//internal const int SB_LINELEFT = 0;
	internal const int SB_LINEDOWN = 1;
	//internal const int SB_LINERIGHT = 1;
	internal const int SB_PAGEUP = 2;
	//internal const int SB_PAGELEFT = 2;
	internal const int SB_PAGEDOWN = 3;
	//internal const int SB_PAGERIGHT = 3;
	//internal const int SB_THUMBPOSITION = 4;
	internal const int SB_THUMBTRACK = 5;
	internal const int SB_TOP = 6;
	//internal const int SB_LEFT = 6;
	internal const int SB_BOTTOM = 7;
	//internal const int SB_RIGHT = 7;
	//internal const int SB_ENDSCROLL = 8;
	internal const int SB_HORZ = 0;
	internal const int SB_VERT = 1;
	//internal const int SB_CTL = 2;
	internal const int SB_BOTH = 3;
	
	internal struct SCROLLINFO {
		public int cbSize;
		public uint fMask;
		public int nMin;
		public int nMax;
		public int nPage;
		public int nPos;
		public int nTrackPos;
		
		public SCROLLINFO(uint mask) {
			cbSize = sizeof(SCROLLINFO);
			fMask = mask;
		}
		
		public int Set(wnd w, bool vertical, bool redraw = true)
			=> SetScrollInfo(w, vertical ? SB_VERT : SB_HORZ, this, redraw);
		
		public bool Get(wnd w, bool vertical)
			=> GetScrollInfo(w, vertical ? SB_VERT : SB_HORZ, ref this);
		
		public static SCROLLINFO Get(wnd w, bool vertical, uint mask) {
			SCROLLINFO v = new(mask);
			v.Get(w, vertical);
			return v;
		}
		
		public static int GetTrackPos(wnd w, bool vertical) => Get(w, vertical, SIF_TRACKPOS).nTrackPos;
		
		public static void SetPos(wnd w, bool vertical, int pos, bool redraw = true) {
			new SCROLLINFO(SIF_POS) { nPos = pos }.Set(w, vertical, redraw);
		}
		
		public static void SetRange(wnd w, bool vertical, int max, int page, bool redraw = true) {
			new SCROLLINFO(SIF_RANGE | SIF_PAGE) { nMax = max, nPage = page }.Set(w, vertical, redraw);
		}
	}
	
	[DllImport("user32.dll")]
	internal static extern int SetScrollInfo(wnd hwnd, int nBar, in SCROLLINFO lpsi, bool redraw);
	
	[DllImport("user32.dll")]
	internal static extern bool GetScrollInfo(wnd hwnd, int nBar, ref SCROLLINFO lpsi);
	
	[DllImport("user32.dll")]
	internal static extern bool ShowScrollBar(wnd hWnd, int wBar, bool bShow);
	
	//[DllImport("user32.dll", SetLastError = true)]
	//internal static extern bool GetScrollBarInfo(wnd hwnd, EObjid idObject, ref SCROLLBARINFO psbi);
	
	//internal struct SCROLLBARINFO
	//{
	//	public int cbSize;
	//	public RECT rcScrollBar;
	//	public int dxyLineButton;
	//	public int xyThumbTop;
	//	public int xyThumbBottom;
	//	public int reserved;
	//	//public fixed uint rgstate[6];
	//	public uint stateScrollbar, stateArrowTopRight, statePageUpRight, stateThumb, statePageDownLeft, stateArrowBottomLeft;
	//}
	//internal const uint STATE_SYSTEM_INVISIBLE = 0x8000;
	//internal const uint STATE_SYSTEM_OFFSCREEN = 0x10000;
	//internal const uint STATE_SYSTEM_PRESSED = 0x8;
	//internal const uint STATE_SYSTEM_UNAVAILABLE = 0x1;
	
	[DllImport("user32.dll", EntryPoint = "MessageBoxW")]
	internal static extern int MessageBox(wnd hWnd, string lpText, string lpCaption, uint uType);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetWindowRgn(wnd hWnd, IntPtr hRgn);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern int GetDpiForWindow(wnd hWnd);
	
	[DllImport("user32.dll")]
	internal static extern IntPtr GetWindowDpiAwarenessContext(wnd hwnd);
	
	[DllImport("user32.dll")]
	internal static extern Dpi.Awareness GetAwarenessFromDpiAwarenessContext(IntPtr value);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint SetThreadDpiAwarenessContext(nint dpiContext);
	
	[DllImport("shcore.dll", PreserveSig = true)]
	internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out int dpiX, out int dpiY);
	
	[DllImport("shcore.dll", PreserveSig = true)]
	internal static extern int GetProcessDpiAwareness(IntPtr hprocess, out Dpi.Awareness value); //Dpi.Awareness is PROCESS_DPI_AWARENESS
	
	[DllImport("user32.dll")]
	internal static extern bool PhysicalToLogicalPointForPerMonitorDPI(wnd hWnd, ref POINT lpPoint);
	
	//[DllImport("user32.dll")]
	//internal static extern bool PhysicalToLogicalPoint(wnd hWnd, ref POINT lpPoint);
	
	//internal static bool PhysicalToLogicalPoint_AnyOS(wnd w, ref POINT p) => osVersion.minWin8_1 ? PhysicalToLogicalPointForPerMonitorDPI(w, ref p) : PhysicalToLogicalPoint(w, ref p);
	
	//[DllImport("user32.dll")]
	//internal static extern bool LogicalToPhysicalPointForPerMonitorDPI(wnd hWnd, ref POINT lpPoint);
	
	[DllImport("user32.dll")]
	internal static extern bool LogicalToPhysicalPoint(wnd hWnd, ref POINT lpPoint);
	
	//internal static bool LogicalToPhysicalPoint_AnyOS(wnd w, ref POINT p) => osVersion.minWin8_1 ? LogicalToPhysicalPointForPerMonitorDPI(w, ref p) : LogicalToPhysicalPoint(w, ref p);
	
	[DllImport("user32.dll")]
	internal static extern int GetSysColor(int nIndex);
	
	[DllImport("user32.dll")]
	internal static extern IntPtr GetSysColorBrush(int nIndex);
	
	//internal struct DRAWTEXTPARAMS
	//{
	//	public int cbSize;
	//	public int iTabLength;
	//	public int iLeftMargin;
	//	public int iRightMargin;
	//	public int uiLengthDrawn;
	//}
	
	//[DllImport("user32.dll", SetLastError = true)]
	//static extern int DrawTextExW(IntPtr hdc, char* lpchText, int cchText, ref RECT lprc, TFFlags format, DRAWTEXTPARAMS* lpdtp);
	
	[DllImport("user32.dll", SetLastError = true)]
	static extern int DrawTextExW(IntPtr hdc, char* lpchText, int cchText, ref RECT lprc, TFFlags format, void* lpdtp);
	
	internal static int DrawText(IntPtr hdc, RStr s, ref RECT lprc, TFFlags format) {
		if (format.Has(TFFlags.MODIFYSTRING)) throw new NotSupportedException("MODIFYSTRING");
		fixed (char* p = s) return DrawTextExW(hdc, p, s.Length, ref lprc, format, null);
		
		//DRAWTEXTPARAMS doc incorrect. Left nad right margin fields are in pixels, not average char widths. Not tested tab width.
	}
	
	internal const WS TTS_ALWAYSTIP = (WS)0x1;
	internal const WS TTS_NOPREFIX = (WS)0x2;
	internal const WS TTS_BALLOON = (WS)0x40;
	internal const int TTM_ACTIVATE = 0x401;
	internal const int TTM_SETMAXTIPWIDTH = 0x418;
	internal const int TTM_ADDTOOL = 0x432;
	internal const int TTM_DELTOOL = 0x433;
	internal const int TTM_RELAYEVENT = 0x407;
	//internal const uint TTF_SUBCLASS = 0x10;
	
	internal struct TTTOOLINFO {
		public int cbSize;
		public uint uFlags;
		public wnd hwnd;
		public nint uId;
		public RECT rect;
		public IntPtr hinst;
		public char* lpszText;
		public nint lParam;
		public void* lpReserved;
	}
	
	[DllImport("user32.dll")]
	internal static extern bool DrawEdge(IntPtr hdc, ref RECT qrc, uint edge, uint grfFlags);
	
	internal const uint EDGE_ETCHED = 0x6;
	internal const uint BF_LEFT = 0x1;
	internal const uint BF_TOP = 0x2;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern IntPtr GetThreadDesktop(int dwThreadId);
	
	[DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true)]
	internal static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, void* pvInfo, int nLength, out int lpnLengthNeeded);
	
	internal const int UOI_IO = 6;
	//internal const int UOI_NAME = 2;
	
	//[DllImport("user32.dll", SetLastError = true)]
	//internal static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);
	
	//[DllImport("user32.dll")]
	//internal static extern bool CloseDesktop(IntPtr hDesktop);
	
	internal const uint ISMEX_SEND = 0x1;
	internal const uint ISMEX_REPLIED = 0x8;
	
	[DllImport("user32.dll")]
	internal static extern uint InSendMessageEx(nint lpReserved = 0);
	
	public static bool InSendMessageBlocked => (InSendMessageEx() & (ISMEX_SEND | ISMEX_REPLIED)) == ISMEX_SEND;
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool ExitWindowsEx(int uFlags, uint dwReason);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool LockWorkStation();
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern nint RegisterSuspendResumeNotification(IntPtr hRecipient, uint Flags);
	
	internal struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS {
		public delegate* unmanaged<void*, int, void*, int> Callback;
		public void* Context;
	}
	
	//internal const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x0;
	//internal const int DEVICE_NOTIFY_CALLBACK = 2;
	
	[DllImport("user32.dll")]
	internal static extern bool UnregisterSuspendResumeNotification(nint Handle);
	
	//internal struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS {
	//	public IntPtr Callback; //DEVICE_NOTIFY_CALLBACK_ROUTINE
	//	public nint Context;
	//}
	
	//internal delegate uint DEVICE_NOTIFY_CALLBACK_ROUTINE(nint context, uint type, nint setting);
	
	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool ClipCursor(RECT* lpRect);
	
	[DllImport("user32.dll")]
	static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
	
	struct LASTINPUTINFO {
		public int cbSize;
		public int dwTime;
	}
	
	internal static int GetLastInputTime() {
		var r = new LASTINPUTINFO { cbSize = 8 };
		return GetLastInputInfo(ref r) ? r.dwTime : 0;
	}
}

