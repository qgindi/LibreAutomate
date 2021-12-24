//Windows API constants common to multiple API functions, such as WM_, WS_, errors.

namespace Au.Types
{
	static unsafe partial class Api
	{
		#region Errors

		internal const int S_OK = 0;
		internal const int S_FALSE = 1;
		internal const int ERROR_FILE_NOT_FOUND = 2;
		internal const int ERROR_PATH_NOT_FOUND = 3;
		internal const int ERROR_ACCESS_DENIED = 5;
		internal const int ERROR_INVALID_HANDLE = 6;
		internal const int ERROR_NOT_SAME_DEVICE = 17;
		internal const int ERROR_NO_MORE_FILES = 18;
		internal const int ERROR_NOT_READY = 21;
		internal const int ERROR_SHARING_VIOLATION = 32;
		internal const int ERROR_LOCK_VIOLATION = 33;
		internal const int ERROR_HANDLE_EOF = 38;
		internal const int ERROR_BAD_NETPATH = 53;
		internal const int ERROR_BAD_NET_NAME = 67;
		internal const int ERROR_FILE_EXISTS = 80;
		internal const int ERROR_INVALID_PARAMETER = 87;
		internal const int ERROR_BROKEN_PIPE = 109;
		internal const int ERROR_SEM_TIMEOUT = 121;
		internal const int ERROR_INSUFFICIENT_BUFFER = 122;
		internal const int ERROR_INVALID_NAME = 123;
		internal const int ERROR_DIR_NOT_EMPTY = 145;
		internal const int ERROR_ALREADY_EXISTS = 183;
		internal const int ERROR_MORE_DATA = 234;
		internal const int ERROR_DIRECTORY = 267;
		internal const int ERROR_PIPE_CONNECTED = 535;
		internal const int ERROR_IO_PENDING = 997;
		internal const int ERROR_UNABLE_TO_REMOVE_REPLACED = 1175;
		internal const int ERROR_USER_MAPPED_FILE = 1224;
		internal const int ERROR_PRIVILEGE_NOT_HELD = 1314;
		internal const int ERROR_INVALID_WINDOW_HANDLE = 1400;
		internal const int ERROR_TIMEOUT = 1460;
		internal const int E_NOTIMPL = unchecked((int)0x80004001);
		internal const int E_NOINTERFACE = unchecked((int)0x80004002);
		internal const int E_FAIL = unchecked((int)0x80004005);
		internal const int E_INVALIDARG = unchecked((int)0x80070057);
		internal const int E_ACCESSDENIED = unchecked((int)0x80070005);
		internal const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
		internal const int DISP_E_MEMBERNOTFOUND = unchecked((int)0x80020003);
		internal const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);
		internal const int RPC_E_SERVER_CANTMARSHAL_DATA = unchecked((int)0x8001000D);
		internal const int E_POINTER = unchecked((int)0x80004003);

		#endregion

		#region WM_

		internal const int WM_NULL = 0;
		internal const int WM_CREATE = 0x0001;
		internal const int WM_DESTROY = 0x0002;
		internal const int WM_MOVE = 0x0003;
		internal const int WM_SIZE = 0x0005;
		internal const int WM_ACTIVATE = 0x0006;
		internal const int WM_SETFOCUS = 0x0007;
		internal const int WM_KILLFOCUS = 0x0008;
		internal const int WM_ENABLE = 0x000A;
		internal const int WM_SETREDRAW = 0x000B;
		internal const int WM_SETTEXT = 0x000C;
		internal const int WM_GETTEXT = 0x000D;
		internal const int WM_GETTEXTLENGTH = 0x000E;
		internal const int WM_PAINT = 0x000F;
		internal const int WM_CLOSE = 0x0010;
		internal const int WM_QUERYENDSESSION = 0x0011;
		internal const int WM_QUERYOPEN = 0x0013;
		internal const int WM_ENDSESSION = 0x0016;
		internal const int WM_QUIT = 0x0012;
		internal const int WM_ERASEBKGND = 0x0014;
		internal const int WM_SYSCOLORCHANGE = 0x0015;
		internal const int WM_SHOWWINDOW = 0x0018;
		internal const int WM_SETTINGCHANGE = 0x001A;
		internal const int WM_DEVMODECHANGE = 0x001B;
		internal const int WM_ACTIVATEAPP = 0x001C;
		internal const int WM_FONTCHANGE = 0x001D;
		internal const int WM_TIMECHANGE = 0x001E;
		internal const int WM_CANCELMODE = 0x001F;
		internal const int WM_SETCURSOR = 0x0020;
		internal const int WM_MOUSEACTIVATE = 0x0021;
		internal const int WM_CHILDACTIVATE = 0x0022;
		internal const int WM_QUEUESYNC = 0x0023;
		internal const int WM_GETMINMAXINFO = 0x0024;
		internal const int WM_PAINTICON = 0x0026;
		internal const int WM_ICONERASEBKGND = 0x0027;
		internal const int WM_NEXTDLGCTL = 0x0028;
		internal const int WM_SPOOLERSTATUS = 0x002A;
		internal const int WM_DRAWITEM = 0x002B;
		internal const int WM_MEASUREITEM = 0x002C;
		internal const int WM_DELETEITEM = 0x002D;
		internal const int WM_VKEYTOITEM = 0x002E;
		internal const int WM_CHARTOITEM = 0x002F;
		internal const int WM_SETFONT = 0x0030;
		internal const int WM_GETFONT = 0x0031;
		internal const int WM_SETHOTKEY = 0x0032;
		internal const int WM_GETHOTKEY = 0x0033;
		internal const int WM_QUERYDRAGICON = 0x0037;
		internal const int WM_COMPAREITEM = 0x0039;
		internal const int WM_GETOBJECT = 0x003D;
		internal const int WM_COMPACTING = 0x0041;
		internal const int WM_WINDOWPOSCHANGING = 0x0046;
		internal const int WM_WINDOWPOSCHANGED = 0x0047;
		internal const int WM_COPYDATA = 0x004A;
		internal const int WM_CANCELJOURNAL = 0x004B;
		internal const int WM_NOTIFY = 0x004E;
		internal const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
		internal const int WM_INPUTLANGCHANGE = 0x0051;
		internal const int WM_TCARD = 0x0052;
		internal const int WM_HELP = 0x0053;
		internal const int WM_USERCHANGED = 0x0054;
		internal const int WM_NOTIFYFORMAT = 0x0055;
		internal const int WM_CONTEXTMENU = 0x007B;
		internal const int WM_STYLECHANGING = 0x007C;
		internal const int WM_STYLECHANGED = 0x007D;
		internal const int WM_DISPLAYCHANGE = 0x007E;
		internal const int WM_GETICON = 0x007F;
		internal const int WM_SETICON = 0x0080;
		internal const int WM_NCCREATE = 0x0081;
		internal const int WM_NCDESTROY = 0x0082;
		internal const int WM_NCCALCSIZE = 0x0083;
		internal const int WM_NCHITTEST = 0x0084;
		internal const int WM_NCPAINT = 0x0085;
		internal const int WM_NCACTIVATE = 0x0086;
		internal const int WM_GETDLGCODE = 0x0087;
		internal const int WM_SYNCPAINT = 0x0088;
		internal const int WM_NCMOUSEMOVE = 0x00A0;
		internal const int WM_NCLBUTTONDOWN = 0x00A1;
		internal const int WM_NCLBUTTONUP = 0x00A2;
		internal const int WM_NCLBUTTONDBLCLK = 0x00A3;
		internal const int WM_NCRBUTTONDOWN = 0x00A4;
		internal const int WM_NCRBUTTONUP = 0x00A5;
		internal const int WM_NCRBUTTONDBLCLK = 0x00A6;
		internal const int WM_NCMBUTTONDOWN = 0x00A7;
		internal const int WM_NCMBUTTONUP = 0x00A8;
		internal const int WM_NCMBUTTONDBLCLK = 0x00A9;
		internal const int WM_NCXBUTTONDOWN = 0x00AB;
		internal const int WM_NCXBUTTONUP = 0x00AC;
		internal const int WM_NCXBUTTONDBLCLK = 0x00AD;
		internal const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
		internal const int WM_INPUT = 0x00FF;
		internal const int WM_KEYDOWN = 0x0100;
		internal const int WM_KEYUP = 0x0101;
		internal const int WM_CHAR = 0x0102;
		internal const int WM_DEADCHAR = 0x0103;
		internal const int WM_SYSKEYDOWN = 0x0104;
		internal const int WM_SYSKEYUP = 0x0105;
		internal const int WM_SYSCHAR = 0x0106;
		internal const int WM_SYSDEADCHAR = 0x0107;
		internal const int WM_UNICHAR = 0x0109;
		internal const int WM_IME_STARTCOMPOSITION = 0x010D;
		internal const int WM_IME_ENDCOMPOSITION = 0x010E;
		internal const int WM_IME_COMPOSITION = 0x010F;
		internal const int WM_INITDIALOG = 0x0110;
		internal const int WM_COMMAND = 0x0111;
		internal const int WM_SYSCOMMAND = 0x0112;
		internal const int WM_TIMER = 0x0113;
		internal const int WM_HSCROLL = 0x0114;
		internal const int WM_VSCROLL = 0x0115;
		internal const int WM_INITMENU = 0x0116;
		internal const int WM_INITMENUPOPUP = 0x0117;
		internal const int WM_MENUSELECT = 0x011F;
		internal const int WM_MENUCHAR = 0x0120;
		internal const int WM_ENTERIDLE = 0x0121;
		internal const int WM_MENURBUTTONUP = 0x0122;
		internal const int WM_MENUDRAG = 0x0123;
		internal const int WM_MENUGETOBJECT = 0x0124;
		internal const int WM_UNINITMENUPOPUP = 0x0125;
		internal const int WM_MENUCOMMAND = 0x0126;
		internal const int WM_CHANGEUISTATE = 0x0127;
		internal const int WM_UPDATEUISTATE = 0x0128;
		internal const int WM_QUERYUISTATE = 0x0129;
		internal const int WM_CTLCOLORMSGBOX = 0x0132;
		internal const int WM_CTLCOLOREDIT = 0x0133;
		internal const int WM_CTLCOLORLISTBOX = 0x0134;
		internal const int WM_CTLCOLORBTN = 0x0135;
		internal const int WM_CTLCOLORDLG = 0x0136;
		internal const int WM_CTLCOLORSCROLLBAR = 0x0137;
		internal const int WM_CTLCOLORSTATIC = 0x0138;
		internal const int WM_MOUSEFIRST = 0x0200;
		internal const int WM_MOUSEMOVE = 0x0200;
		internal const int WM_LBUTTONDOWN = 0x0201;
		internal const int WM_LBUTTONUP = 0x0202;
		internal const int WM_LBUTTONDBLCLK = 0x0203;
		internal const int WM_RBUTTONDOWN = 0x0204;
		internal const int WM_RBUTTONUP = 0x0205;
		internal const int WM_RBUTTONDBLCLK = 0x0206;
		internal const int WM_MBUTTONDOWN = 0x0207;
		internal const int WM_MBUTTONUP = 0x0208;
		internal const int WM_MBUTTONDBLCLK = 0x0209;
		internal const int WM_MOUSEWHEEL = 0x020A;
		internal const int WM_XBUTTONDOWN = 0x020B;
		internal const int WM_XBUTTONUP = 0x020C;
		internal const int WM_XBUTTONDBLCLK = 0x020D;
		internal const int WM_MOUSEHWHEEL = 0x020E;
		internal const int WM_MOUSELAST = 0x020E;
		internal const int WM_PARENTNOTIFY = 0x0210;
		internal const int WM_ENTERMENULOOP = 0x0211;
		internal const int WM_EXITMENULOOP = 0x0212;
		internal const int WM_NEXTMENU = 0x0213;
		internal const int WM_SIZING = 0x0214;
		internal const int WM_CAPTURECHANGED = 0x0215;
		internal const int WM_MOVING = 0x0216;
		internal const int WM_POWERBROADCAST = 0x0218;
		internal const int WM_DEVICECHANGE = 0x0219;
		internal const int WM_MDICREATE = 0x0220;
		internal const int WM_MDIDESTROY = 0x0221;
		internal const int WM_MDIACTIVATE = 0x0222;
		internal const int WM_MDIRESTORE = 0x0223;
		internal const int WM_MDINEXT = 0x0224;
		internal const int WM_MDIMAXIMIZE = 0x0225;
		internal const int WM_MDITILE = 0x0226;
		internal const int WM_MDICASCADE = 0x0227;
		internal const int WM_MDIICONARRANGE = 0x0228;
		internal const int WM_MDIGETACTIVE = 0x0229;
		internal const int WM_MDISETMENU = 0x0230;
		internal const int WM_ENTERSIZEMOVE = 0x0231;
		internal const int WM_EXITSIZEMOVE = 0x0232;
		internal const int WM_DROPFILES = 0x0233;
		internal const int WM_MDIREFRESHMENU = 0x0234;
		internal const int WM_IME_SETCONTEXT = 0x0281;
		internal const int WM_IME_NOTIFY = 0x0282;
		internal const int WM_IME_CONTROL = 0x0283;
		internal const int WM_IME_COMPOSITIONFULL = 0x0284;
		internal const int WM_IME_SELECT = 0x0285;
		internal const int WM_IME_CHAR = 0x0286;
		internal const int WM_IME_REQUEST = 0x0288;
		internal const int WM_IME_KEYDOWN = 0x0290;
		internal const int WM_IME_KEYUP = 0x0291;
		internal const int WM_MOUSEHOVER = 0x02A1;
		internal const int WM_MOUSELEAVE = 0x02A3;
		internal const int WM_NCMOUSEHOVER = 0x02A0;
		internal const int WM_NCMOUSELEAVE = 0x02A2;
		internal const int WM_WTSSESSION_CHANGE = 0x02B1;
		internal const int WM_DPICHANGED = 0x2E0;
		internal const int WM_DPICHANGED_BEFOREPARENT = 0x2E2;
		internal const int WM_DPICHANGED_AFTERPARENT = 0x02E3;
		internal const int WM_GETDPISCALEDSIZE = 0x02E4;
		internal const int WM_CUT = 0x0300;
		internal const int WM_COPY = 0x0301;
		internal const int WM_PASTE = 0x0302;
		internal const int WM_CLEAR = 0x0303;
		internal const int WM_UNDO = 0x0304;
		internal const int WM_RENDERFORMAT = 0x0305;
		internal const int WM_RENDERALLFORMATS = 0x0306;
		internal const int WM_DESTROYCLIPBOARD = 0x0307;
		internal const int WM_DRAWCLIPBOARD = 0x0308;
		internal const int WM_PAINTCLIPBOARD = 0x0309;
		internal const int WM_VSCROLLCLIPBOARD = 0x030A;
		internal const int WM_SIZECLIPBOARD = 0x030B;
		internal const int WM_ASKCBFORMATNAME = 0x030C;
		internal const int WM_CHANGECBCHAIN = 0x030D;
		internal const int WM_HSCROLLCLIPBOARD = 0x030E;
		internal const int WM_QUERYNEWPALETTE = 0x030F;
		internal const int WM_PALETTEISCHANGING = 0x0310;
		internal const int WM_PALETTECHANGED = 0x0311;
		internal const int WM_HOTKEY = 0x0312;
		internal const int WM_PRINT = 0x0317;
		internal const int WM_PRINTCLIENT = 0x0318;
		internal const int WM_APPCOMMAND = 0x0319;
		internal const int WM_THEMECHANGED = 0x031A;
		internal const int WM_CLIPBOARDUPDATE = 0x031D;
		internal const int WM_DWMCOMPOSITIONCHANGED = 0x031E;
		internal const int WM_DWMNCRENDERINGCHANGED = 0x031F;
		internal const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
		internal const int WM_DWMWINDOWMAXIMIZEDCHANGE = 0x0321;
		internal const int WM_GETTITLEBARINFOEX = 0x033F;
		internal const int WM_APP = 0x8000;
		internal const int WM_USER = 0x0400;

		internal const int WM_CPL_LAUNCH = WM_USER + 0x1000;
		internal const int WM_CPL_LAUNCHED = WM_USER + 0x1001;
		internal const int WM_SYSTIMER = 0x118;
		internal const int WM_HSHELL_ACCESSIBILITYSTATE = 11;
		internal const int WM_HSHELL_ACTIVATESHELLWINDOW = 3;
		internal const int WM_HSHELL_APPCOMMAND = 12;
		internal const int WM_HSHELL_GETMINRECT = 5;
		internal const int WM_HSHELL_LANGUAGE = 8;
		internal const int WM_HSHELL_REDRAW = 6;
		internal const int WM_HSHELL_TASKMAN = 7;
		internal const int WM_HSHELL_WINDOWCREATED = 1;
		internal const int WM_HSHELL_WINDOWDESTROYED = 2;
		internal const int WM_HSHELL_WINDOWACTIVATED = 4;
		internal const int WM_HSHELL_WINDOWREPLACED = 13;

		internal const int WM_REFLECT = 0x2000;

		#endregion

		#region control styles, messages etc

		//ES_, EM_, EN_
		internal const int ES_MULTILINE = 0x4;
		internal const int ES_PASSWORD = 0x20;
		internal const int ES_AUTOVSCROLL = 0x40;
		internal const int ES_AUTOHSCROLL = 0x80;
		internal const int ES_WANTRETURN = 0x1000;
		internal const int ES_NUMBER = 0x2000;

		internal const int EM_SETSEL = 0xB1;
		internal const int EM_SETCUEBANNER = 0x1501;

		//CBS_, CB_, CBN_
		internal const uint CBS_SIMPLE = 1;
		internal const uint CBS_DROPDOWN = 2;
		//internal const uint CBS_DROPDOWNLIST = 3;
		internal const uint CBS_AUTOHSCROLL = 0x40;

		internal const int CB_INSERTSTRING = 330;
		internal const int CB_SETCUEBANNER = 0x1703;

		internal const int MN_GETHMENU = 0x1E1;

		#endregion

		#region CS_

		internal const uint CS_VREDRAW = 0x1;
		internal const uint CS_HREDRAW = 0x2;
		internal const uint CS_DBLCLKS = 0x8;
		internal const uint CS_OWNDC = 0x20;
		internal const uint CS_CLASSDC = 0x40;
		internal const uint CS_PARENTDC = 0x80;
		internal const uint CS_NOCLOSE = 0x200;
		internal const uint CS_SAVEBITS = 0x800;
		internal const uint CS_BYTEALIGNCLIENT = 0x1000;
		internal const uint CS_BYTEALIGNWINDOW = 0x2000;
		internal const uint CS_GLOBALCLASS = 0x4000;
		internal const uint CS_IME = 0x10000;
		internal const uint CS_DROPSHADOW = 0x20000;

		#endregion

		#region HT (hit-test)

		internal const int HTERROR = -2;
		internal const int HTTRANSPARENT = -1;
		internal const int HTNOWHERE = 0;
		internal const int HTCLIENT = 1;
		internal const int HTCAPTION = 2;
		internal const int HTSYSMENU = 3;
		internal const int HTSIZE = 4;
		internal const int HTMENU = 5;
		internal const int HTHSCROLL = 6;
		internal const int HTVSCROLL = 7;
		internal const int HTMINBUTTON = 8;
		internal const int HTMAXBUTTON = 9;
		internal const int HTLEFT = 10;
		internal const int HTRIGHT = 11;
		internal const int HTTOP = 12;
		internal const int HTTOPLEFT = 13;
		internal const int HTTOPRIGHT = 14;
		internal const int HTBOTTOM = 15;
		internal const int HTBOTTOMLEFT = 16;
		internal const int HTBOTTOMRIGHT = 17;
		internal const int HTBORDER = 18;
		internal const int HTOBJECT = 19;
		internal const int HTCLOSE = 20;
		internal const int HTHELP = 21;
		internal const int HTSIZEFIRST = HTLEFT;
		internal const int HTSIZELAST = HTBOTTOMRIGHT;

		#endregion

		#region SC_
		internal const int SC_SIZE = 0xF000;
		internal const int SC_MOVE = 0xF010;
		internal const int SC_MINIMIZE = 0xF020;
		internal const int SC_MAXIMIZE = 0xF030;
		internal const int SC_NEXTWINDOW = 0xF040;
		internal const int SC_PREVWINDOW = 0xF050;
		internal const int SC_CLOSE = 0xF060;
		internal const int SC_VSCROLL = 0xF070;
		internal const int SC_HSCROLL = 0xF080;
		internal const int SC_MOUSEMENU = 0xF090;
		internal const int SC_KEYMENU = 0xF100;
		internal const int SC_ARRANGE = 0xF110;
		internal const int SC_RESTORE = 0xF120;
		internal const int SC_TASKLIST = 0xF130;
		internal const int SC_SCREENSAVE = 0xF140;
		internal const int SC_HOTKEY = 0xF150;
		internal const int SC_DEFAULT = 0xF160;
		internal const int SC_MONITORPOWER = 0xF170;
		internal const int SC_CONTEXTHELP = 0xF180;
		internal const int SC_SEPARATOR = 0xF00F;
		#endregion

		#region COLOR_

		internal const int COLOR_SCROLLBAR = 0;
		internal const int COLOR_BACKGROUND = 1;
		internal const int COLOR_ACTIVECAPTION = 2;
		internal const int COLOR_INACTIVECAPTION = 3;
		internal const int COLOR_MENU = 4;
		internal const int COLOR_WINDOW = 5;
		internal const int COLOR_WINDOWFRAME = 6;
		internal const int COLOR_MENUTEXT = 7;
		internal const int COLOR_WINDOWTEXT = 8;
		internal const int COLOR_CAPTIONTEXT = 9;
		internal const int COLOR_ACTIVEBORDER = 10;
		internal const int COLOR_INACTIVEBORDER = 11;
		internal const int COLOR_APPWORKSPACE = 12;
		internal const int COLOR_HIGHLIGHT = 13;
		internal const int COLOR_HIGHLIGHTTEXT = 14;
		internal const int COLOR_BTNFACE = 15;
		internal const int COLOR_BTNSHADOW = 16;
		internal const int COLOR_GRAYTEXT = 17;
		internal const int COLOR_BTNTEXT = 18;
		internal const int COLOR_INACTIVECAPTIONTEXT = 19;
		internal const int COLOR_BTNHIGHLIGHT = 20;
		internal const int COLOR_3DDKSHADOW = 21;
		internal const int COLOR_3DLIGHT = 22;
		internal const int COLOR_INFOTEXT = 23;
		internal const int COLOR_INFOBK = 24;
		internal const int COLOR_HOTLIGHT = 26;
		internal const int COLOR_GRADIENTACTIVECAPTION = 27;
		internal const int COLOR_GRADIENTINACTIVECAPTION = 28;
		internal const int COLOR_MENUHILIGHT = 29;
		internal const int COLOR_MENUBAR = 30;
		internal const int COLOR_DESKTOP = 1;
		internal const int COLOR_3DFACE = 15;
		internal const int COLOR_3DSHADOW = 16;
		internal const int COLOR_3DHIGHLIGHT = 20;
		internal const int COLOR_3DHILIGHT = 20;
		internal const int COLOR_BTNHILIGHT = 20;

		#endregion

		#region QS_

		internal const uint QS_KEY = 0x1;
		internal const uint QS_MOUSEMOVE = 0x2;
		internal const uint QS_MOUSEBUTTON = 0x4;
		internal const uint QS_POSTMESSAGE = 0x8;
		internal const uint QS_TIMER = 0x10;
		internal const uint QS_PAINT = 0x20;
		internal const uint QS_SENDMESSAGE = 0x40;
		internal const uint QS_HOTKEY = 0x80;
		internal const uint QS_ALLPOSTMESSAGE = 0x100;
		internal const uint QS_RAWINPUT = 0x400;
		internal const uint QS_TOUCH = 0x800;
		internal const uint QS_POINTER = 0x1000;
		internal const uint QS_MOUSE = 0x6;
		internal const uint QS_INPUT = 0x1C07;
		internal const uint QS_ALLEVENTS = 0x1CBF;
		internal const uint QS_ALLINPUT = 0x1CFF;

		#endregion

		#region WAIT_

		internal const int WAIT_FAILED = -1;
		internal const int WAIT_OBJECT_0 = 0x0;
		internal const int WAIT_ABANDONED = 0x80;
		internal const int WAIT_ABANDONED_0 = 0x80;
		internal const int WAIT_IO_COMPLETION = 0xC0;
		internal const int WAIT_TIMEOUT = 0x102;

		#endregion

		#region LR_, IMAGE_

		internal const int IMAGE_BITMAP = 0;
		internal const int IMAGE_ICON = 1;
		internal const int IMAGE_CURSOR = 2;
		internal const uint LR_MONOCHROME = 0x1;
		internal const uint LR_COLOR = 0x2;
		internal const uint LR_COPYRETURNORG = 0x4;
		internal const uint LR_COPYDELETEORG = 0x8;
		internal const uint LR_LOADFROMFILE = 0x10;
		internal const uint LR_LOADTRANSPARENT = 0x20;
		internal const uint LR_DEFAULTSIZE = 0x40;
		internal const uint LR_VGACOLOR = 0x80;
		internal const uint LR_LOADMAP3DCOLORS = 0x1000;
		internal const uint LR_CREATEDIBSECTION = 0x2000;
		internal const uint LR_COPYFROMRESOURCE = 0x4000;
		internal const uint LR_SHARED = 0x8000;

		#endregion

		#region SFGAO_

		internal const uint SFGAO_CANCOPY = 1;
		internal const uint SFGAO_CANMOVE = 2;
		internal const uint SFGAO_CANLINK = 4;
		internal const uint SFGAO_STORAGE = 0x00000008;
		internal const uint SFGAO_CANRENAME = 0x00000010;
		internal const uint SFGAO_CANDELETE = 0x00000020;
		internal const uint SFGAO_HASPROPSHEET = 0x00000040;
		internal const uint SFGAO_DROPTARGET = 0x00000100;
		internal const uint SFGAO_CAPABILITYMASK = 0x00000177;
		internal const uint SFGAO_SYSTEM = 0x00001000;
		internal const uint SFGAO_ENCRYPTED = 0x00002000;
		internal const uint SFGAO_ISSLOW = 0x00004000;
		internal const uint SFGAO_GHOSTED = 0x00008000;
		internal const uint SFGAO_LINK = 0x00010000;
		internal const uint SFGAO_SHARE = 0x00020000;
		internal const uint SFGAO_READONLY = 0x00040000;
		internal const uint SFGAO_HIDDEN = 0x00080000;
		internal const uint SFGAO_DISPLAYATTRMASK = 0x000FC000;
		internal const uint SFGAO_FILESYSANCESTOR = 0x10000000;
		internal const uint SFGAO_FOLDER = 0x20000000;
		internal const uint SFGAO_FILESYSTEM = 0x40000000;
		internal const uint SFGAO_HASSUBFOLDER = 0x80000000;
		internal const uint SFGAO_CONTENTSMASK = 0x80000000;
		internal const uint SFGAO_VALIDATE = 0x01000000;
		internal const uint SFGAO_REMOVABLE = 0x02000000;
		internal const uint SFGAO_COMPRESSED = 0x04000000;
		internal const uint SFGAO_BROWSABLE = 0x08000000;
		internal const uint SFGAO_NONENUMERATED = 0x00100000;
		internal const uint SFGAO_NEWCONTENT = 0x00200000;
		internal const uint SFGAO_CANMONIKER = 0x00400000;
		internal const uint SFGAO_HASSTORAGE = 0x00400000;
		internal const uint SFGAO_STREAM = 0x00400000;
		internal const uint SFGAO_STORAGEANCESTOR = 0x00800000;
		internal const uint SFGAO_STORAGECAPMASK = 0x70C50008;
		internal const uint SFGAO_PKEYSFGAOMASK = 0x81044000;

		#endregion

		#region STGM_

		internal const uint STGM_DIRECT = 0x0;
		internal const uint STGM_TRANSACTED = 0x10000;
		internal const uint STGM_SIMPLE = 0x8000000;
		internal const uint STGM_READ = 0x0;
		internal const uint STGM_WRITE = 0x1;
		internal const uint STGM_READWRITE = 0x2;
		internal const uint STGM_SHARE_DENY_NONE = 0x40;
		internal const uint STGM_SHARE_DENY_READ = 0x30;
		internal const uint STGM_SHARE_DENY_WRITE = 0x20;
		internal const uint STGM_SHARE_EXCLUSIVE = 0x10;
		internal const uint STGM_PRIORITY = 0x40000;
		internal const uint STGM_DELETEONRELEASE = 0x4000000;
		internal const uint STGM_NOSCRATCH = 0x100000;
		internal const uint STGM_CREATE = 0x1000;
		internal const uint STGM_CONVERT = 0x20000;
		internal const uint STGM_FAILIFTHERE = 0x0;
		internal const uint STGM_NOSNAPSHOT = 0x200000;
		internal const uint STGM_DIRECT_SWMR = 0x400000;

		#endregion

		#region MA_
		internal const int MA_ACTIVATE = 1;
		internal const int MA_ACTIVATEANDEAT = 2;
		internal const int MA_NOACTIVATE = 3;
		internal const int MA_NOACTIVATEANDEAT = 4;
		#endregion

		#region MK_

		internal const int MK_LBUTTON = 0x1;
		internal const int MK_RBUTTON = 0x2;
		internal const int MK_SHIFT = 0x4;
		internal const int MK_CONTROL = 0x8;
		internal const int MK_MBUTTON = 0x10;

		#endregion

		#region CF_

		internal const int CF_TEXT = 1;
		internal const int CF_BITMAP = 2;
		internal const int CF_METAFILEPICT = 3;
		internal const int CF_SYLK = 4;
		internal const int CF_DIF = 5;
		internal const int CF_TIFF = 6;
		internal const int CF_OEMTEXT = 7;
		internal const int CF_DIB = 8;
		internal const int CF_PALETTE = 9;
		//internal const int CF_PENDATA = 10; //obsolete
		internal const int CF_RIFF = 11;
		internal const int CF_WAVE = 12;
		internal const int CF_UNICODETEXT = 13;
		internal const int CF_ENHMETAFILE = 14;
		internal const int CF_HDROP = 15;
		internal const int CF_LOCALE = 16;
		internal const int CF_DIBV5 = 17;
		internal const int CF_MAX = 18;
		//internal const int CF_OWNERDISPLAY = 0x80; //these are rare and not supported by this library
		//internal const int CF_DSPTEXT = 0x81;
		//internal const int CF_DSPBITMAP = 0x82;
		//internal const int CF_DSPMETAFILEPICT = 0x83;
		//internal const int CF_DSPENHMETAFILE = 0x8E;
		//internal const int CF_PRIVATEFIRST = 0x200;
		//internal const int CF_PRIVATELAST = 0x2FF;
		//internal const int CF_GDIOBJFIRST = 0x300;
		//internal const int CF_GDIOBJLAST = 0x3FF;

		#endregion

		#region misc

		internal const int IDI_APPLICATION = 32512;
		internal const int PBT_APMSUSPEND = 0x4;

		#endregion






		#region ENUM

		[Flags]
		internal enum VARENUM : ushort
		{
			VT_EMPTY,
			VT_NULL,
			VT_I2,
			VT_I4,
			VT_R4,
			VT_R8,
			VT_CY,
			VT_DATE,
			VT_BSTR,
			VT_DISPATCH,
			VT_ERROR,
			VT_BOOL,
			VT_VARIANT,
			VT_UNKNOWN,
			VT_DECIMAL,
			VT_I1 = 16,
			VT_UI1,
			VT_UI2,
			VT_UI4,
			VT_I8,
			VT_UI8,
			VT_INT,
			VT_UINT,
			VT_VOID,
			VT_HRESULT,
			VT_PTR,
			VT_SAFEARRAY,
			VT_CARRAY,
			VT_USERDEFINED,
			VT_LPSTR,
			VT_LPWSTR,
			VT_RECORD = 36,
			VT_INT_PTR,
			VT_UINT_PTR,
			VT_FILETIME = 64,
			VT_BLOB,
			VT_STREAM,
			VT_STORAGE,
			VT_STREAMED_OBJECT,
			VT_STORED_OBJECT,
			VT_BLOB_OBJECT,
			VT_CF,
			VT_CLSID,
			VT_VERSIONED_STREAM,
			VT_BSTR_BLOB = 0xFFF,
			VT_VECTOR,
			VT_ARRAY = 0x2000,
			VT_BYREF = 0x4000,
			VT_RESERVED = 0x8000,
			VT_ILLEGAL = 0xFFFF,
			VT_ILLEGALMASKED = 0xFFF,
			VT_TYPEMASK = 0xFFF
		}

		#endregion

		#region strings

		internal const string string_IES = "Internet Explorer_Server";


		#endregion
	}
}
