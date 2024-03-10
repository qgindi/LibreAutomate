#pragma warning disable 649, 169 //field never assigned/used
#pragma warning disable 1591 //missing XML documentation

namespace Au.Types;

/// <summary>
/// Window styles.
/// </summary>
/// <remarks>
/// Reference: <msdn>Window Styles</msdn>.
/// Here names are without prefix <b>WS_</b>. For example, instead of <c>WS_BORDER</c> <c>use WS.BORDER</c>. Not included constants that are 0 (eg <c>WS_TILED</c>) or are duplicate (eg <c>WS_SIZEBOX</c> is same as <c>WS_THICKFRAME</c>) or consist of multiple other constants (eg <c>WS_TILEDWINDOW</c>).
/// </remarks>
[Flags]
public enum WS : uint
{
	POPUP = 0x80000000,
	CHILD = 0x40000000,
	MINIMIZE = 0x20000000,
	VISIBLE = 0x10000000,
	DISABLED = 0x08000000,
	CLIPSIBLINGS = 0x04000000,
	CLIPCHILDREN = 0x02000000,
	MAXIMIZE = 0x01000000,
	BORDER = 0x00800000,
	DLGFRAME = 0x00400000,
	VSCROLL = 0x00200000,
	HSCROLL = 0x00100000,
	SYSMENU = 0x00080000,
	THICKFRAME = 0x00040000,
	MINIMIZEBOX = 0x00020000,
	GROUP = 0x00020000,
	MAXIMIZEBOX = 0x00010000,
	TABSTOP = 0x00010000,
	CAPTION = BORDER | DLGFRAME,
	//these can cause bugs and confusion because consist of several styles. Not useful in C#, because used only to create native windows.
	//OVERLAPPEDWINDOW = CAPTION | SYSMENU | THICKFRAME | MINIMIZEBOX | MAXIMIZEBOX,
	//POPUPWINDOW = POPUP | BORDER | SYSMENU,
}

/// <summary>
/// Window extended styles.
/// </summary>
/// <remarks>
/// Reference: <msdn>Extended Window Styles</msdn>.
/// Here names are without prefix <b>WS_EX_</b>. For example, instead of <c>WS_EX_TOOLWINDOW</c> <c>use WSE.TOOLWINDOW</c>. Not included constants that are 0 (eg <c>WS_EX_LEFT</c>).
/// </remarks>
[Flags]
public enum WSE : uint
{
	DLGMODALFRAME = 0x00000001,
	NOPARENTNOTIFY = 0x00000004,
	TOPMOST = 0x00000008,
	ACCEPTFILES = 0x00000010,
	TRANSPARENT = 0x00000020,
	MDICHILD = 0x00000040,
	TOOLWINDOW = 0x00000080,
	WINDOWEDGE = 0x00000100,
	CLIENTEDGE = 0x00000200,
	CONTEXTHELP = 0x00000400,
	//LEFT = 0x00000000,
	RIGHT = 0x00001000,
	//LTRREADING = 0x00000000,
	RTLREADING = 0x00002000,
	//RIGHTSCROLLBAR = 0x00000000,
	LEFTSCROLLBAR = 0x00004000,
	CONTROLPARENT = 0x00010000,
	STATICEDGE = 0x00020000,
	APPWINDOW = 0x00040000,
	LAYERED = 0x00080000,
	NOINHERITLAYOUT = 0x00100000,
	NOREDIRECTIONBITMAP = 0x00200000,
	LAYOUTRTL = 0x00400000,
	COMPOSITED = 0x02000000,
	NOACTIVATE = 0x08000000,
}

/// <summary>API <msdn>MSG</msdn></summary>
public struct MSG //WinMSG
{
	public wnd hwnd;
	public int message;
	public nint wParam;
	public nint lParam;
	public int time;
	public POINT pt;

	public override string ToString() {
		WndUtil.PrintMsg(out string s, this, new() { Indent = false, Number = false });
		return s;
	}

	public static implicit operator MSG(in System.Windows.Forms.Message m)
		=> new MSG { hwnd = (wnd)m.HWnd, message = m.Msg, wParam = m.WParam, lParam = m.LParam };

	public static implicit operator MSG(in System.Windows.Interop.MSG m)
		=> new MSG { hwnd = (wnd)m.hwnd, message = m.message, wParam = m.wParam, lParam = m.lParam, time = m.time, pt = (m.pt_x, m.pt_y) };
}

/// <summary><see cref="GUITHREADINFO"/> flags.</summary>
[Flags]
public enum GTIFlags
{
	CARETBLINKING = 0x1,
	INMOVESIZE = 0x2,
	INMENUMODE = 0x4,
	SYSTEMMENUMODE = 0x8,
	POPUPMENUMODE = 0x10,
}

/// <summary>API <msdn>GUITHREADINFO</msdn></summary>
public struct GUITHREADINFO
{
	public int cbSize;
	public GTIFlags flags;
	public wnd hwndActive;
	public wnd hwndFocus;
	public wnd hwndCapture;
	public wnd hwndMenuOwner;
	public wnd hwndMoveSize;
	public wnd hwndCaret;
	public RECT rcCaret;
}

/// <summary>API <msdn>CREATESTRUCT</msdn></summary>
public unsafe struct CREATESTRUCT
{
	public nint lpCreateParams;
	public IntPtr hInstance;
	public nint hMenu;
	public wnd hwndParent;
	public int cy;
	public int cx;
	public int y;
	public int x;
	public WS style;
	public char* lpszName;
	public char* lpszClass;
	public WSE dwExStyle;

	public RStr Name => lpszName == default ? default
		: new RStr(lpszName, CharPtr_.Length(lpszName));
	//public string Name => lpszName == default ? null : new string(lpszName);

	/// <summary>
	/// If <b>lpszClass</b> is atom, returns string with <c>#</c> prefix and atom value, like <c>"#32770"</c>.
	/// </summary>
	public RStr ClassName => (nuint)lpszClass < 0x10000 ? "#" + ((int)lpszClass).ToS()
		: new RStr(lpszClass, CharPtr_.Length(lpszClass));
	//public string ClassName => (nuint)lpszClass < 0x10000 ? "#" + ((int)lpszClass).ToS() : new string(lpszClass);

	//tested and documented: CBT hook can change only x y cx cy.
}

/// <summary>API <msdn>SIGDN</msdn></summary>
public enum SIGDN : uint
{
	NORMALDISPLAY,
	PARENTRELATIVEPARSING = 0x80018001,
	DESKTOPABSOLUTEPARSING = 0x80028000,
	PARENTRELATIVEEDITING = 0x80031001,
	DESKTOPABSOLUTEEDITING = 0x8004C000,
	FILESYSPATH = 0x80058000,
	URL = 0x80068000,
	PARENTRELATIVEFORADDRESSBAR = 0x8007C001,
	PARENTRELATIVE = 0x80080001,
	PARENTRELATIVEFORUI = 0x80094001
}

/// <summary>API <msdn>SetWindowPos</msdn> flags. Can be used with <see cref="wnd.SetWindowPos"/>.</summary>
/// <remarks>The <b>_X</b> flags are undocumented.</remarks>
[Flags]
public enum SWPFlags : uint
{
	NOSIZE = 0x1,
	NOMOVE = 0x2,
	NOZORDER = 0x4,
	NOREDRAW = 0x8,
	NOACTIVATE = 0x10,
	FRAMECHANGED = 0x20,
	SHOWWINDOW = 0x40,
	HIDEWINDOW = 0x80,
	NOCOPYBITS = 0x100,
	NOOWNERZORDER = 0x200,
	NOSENDCHANGING = 0x400,
	_NOCLIENTSIZE = 0x800,
	_NOCLIENTMOVE = 0x1000,
	DEFERERASE = 0x2000,
	ASYNCWINDOWPOS = 0x4000,
	_STATECHANGED = 0x8000,
	//_10000000 = 0x10000000,
	_KNOWNFLAGS = 0xffff,

	//the undocumented flags would break ToString() if not defined
}

/// <summary>
/// Special window handle values. Can be used with <see cref="wnd.SetWindowPos"/>.
/// See API <msdn>SetWindowPos</msdn>.
/// </summary>
public enum SpecHWND
{
	TOP = 0,
	BOTTOM = 1,
	TOPMOST = -1,
	NOTOPMOST = -2,
	MESSAGE = -3,
	BROADCAST = 0xffff,
}

/// <summary>
/// Window long constants. Used with <see cref="wnd.GetWindowLong"/> and <see cref="wnd.SetWindowLong"/>.
/// See API <msdn>GetWindowLong</msdn>. See also API <msdn>SetWindowSubclass</msdn>.
/// </summary>
public static class GWL
{
	public const int WNDPROC = -4;
	public const int USERDATA = -21;
	public const int STYLE = -16;
	public const int ID = -12;
	public const int HWNDPARENT = -8;
	public const int HINSTANCE = -6;
	public const int EXSTYLE = -20;
	//info: also there are GWLP_, but their values are the same.

	public static class DWL
	{
		public static readonly int MSGRESULT = 0;
		public static readonly int DLGPROC = IntPtr.Size;
		public static readonly int USER = IntPtr.Size * 2;
	}
}

/// <summary>
/// Window class long constants. Used with <see cref="WndUtil.GetClassLong"/>.
/// See API <msdn>WNDCLASSEX</msdn>, <msdn>GetClassLong</msdn>.
/// </summary>
public static class GCL
{
	public const int ATOM = -32;
	public const int WNDPROC = -24;
	public const int STYLE = -26;
	public const int MENUNAME = -8;
	public const int HMODULE = -16;
	public const int HICONSM = -34;
	public const int HICON = -14;
	public const int HCURSOR = -12;
	public const int HBRBACKGROUND = -10;
	public const int CBWNDEXTRA = -18;
	public const int CBCLSEXTRA = -20;
	//info: also there are GCLP_, but their values are the same.
}

/// <summary>API <msdn>WNDPROC</msdn></summary>
public delegate nint WNDPROC(wnd w, int msg, nint wp, nint lp);

/// <summary>API <msdn>SendMessageTimeout</msdn> flags. Used with <see cref="wnd.SendTimeout"/>.</summary>
[Flags]
public enum SMTFlags : uint
{
	BLOCK = 0x0001,
	ABORTIFHUNG = 0x0002,
	NOTIMEOUTIFNOTHUNG = 0x0008,
	ERRORONEXIT = 0x0020,
}

/// <summary>API <msdn>DrawTextEx</msdn> format flags.</summary>
[Flags]
public enum TFFlags
{
	CENTER = 0x1,
	RIGHT = 0x2,
	VCENTER = 0x4,
	BOTTOM = 0x8,
	WORDBREAK = 0x10,
	SINGLELINE = 0x20,
	EXPANDTABS = 0x40,
	TABSTOP = 0x80,
	NOCLIP = 0x100,
	EXTERNALLEADING = 0x200,
	CALCRECT = 0x400,
	NOPREFIX = 0x800,
	INTERNAL = 0x1000,
	EDITCONTROL = 0x2000,
	PATH_ELLIPSIS = 0x4000,
	END_ELLIPSIS = 0x8000,
	MODIFYSTRING = 0x10000,
	RTLREADING = 0x20000,
	WORD_ELLIPSIS = 0x40000,
	NOFULLWIDTHCHARBREAK = 0x80000,
	HIDEPREFIX = 0x100000,
	PREFIXONLY = 0x200000
}
