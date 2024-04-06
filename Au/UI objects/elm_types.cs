namespace Au.Types {
	/// <summary>
	/// Flags for "find UI element" functions (<see cref="elmFinder"/>).
	/// </summary>
	[Flags]
	public enum EFFlags {
		/// <summary>
		/// Search in reverse order. It can make faster.
		/// When control class or id is specified in the <i>prop</i> argument, controls are searched not in reverse order. Only UI elements in them are searched in reverse order.
		/// </summary>
		Reverse = 1,

		/// <summary>
		/// The UI element can be invisible.
		/// Without this flag skips UI elements that are invisible (have state <b>INVISIBLE</b>) or are descendants of invisible <b>WINDOW</b>, <b>DOCUMENT</b>, <b>PROPERTYPAGE</b>, <b>GROUPING</b>, <b>ALERT</b>, <b>MENUPOPUP</b>.
		/// Regardless of this flag, always skips invisible standard UI elements of nonclient area: <b>TITLEBAR</b>, <b>MENUBAR</b>, <b>SCROLLBAR</b>, <b>GRIP</b>.
		/// </summary>
		HiddenToo = 2,

		/// <summary>
		/// Always search in <b>MENUITEM</b>.
		/// Without this flag skips <b>MENUITEM</b> descendant elements (for speed), unless <i>role</i> argument is <b>MENUITEM</b> or <b>MENUPOPUP</b> or searching in web page.
		/// </summary>
		MenuToo = 4,

		/// <summary>
		/// Search only in the client area of the window or control.
		/// Skips the title bar, standard menubars and scrollbars. Searches only in the client area root UI element (but will not find the UI element itself).
		/// When control class or id is specified in the <i>prop</i> argument, this flag is applied to these controls. Not applied to other controls.
		/// Don't use this flag when searching in elm or web page (role prefix <c>"web:"</c> etc) or with flag <b>UIA</b>.
		/// </summary>
		ClientArea = 8,

		/// <summary>
		/// Search without loading dll into the target process.
		/// Disadvantages: 1. Much slower. 2. Some properties are not supported, for example HTML attributes (while searching and later). 3. And more.
		/// Even without this flag, the default search method is not used with Windows Store app windows, console windows, most Java windows, windows of protected processes and processes of higher [](xref:uac) integrity level.
		/// Some windows have child controls that belong to a different process or thread than the window. For example the Windows Task Scheduler window. When searching in such windows, the default search method is not used when searching in these controls. Workaround - find the control (<see cref="wnd.Child"/> etc) and search in it.
		/// Don't need this flag when searching in elm (then it is inherited from the elm variable).
		/// See also: <see cref="elm.MiscFlags"/>.
		/// </summary>
		NotInProc = 0x100,

		/// <summary>
		/// Use UI Automation API.
		/// Need this flag to find UI elements in windows that don't support accessible objects but support UI Automation elements.
		/// UI elements found with this flag never have <b>HtmlX</b> properties, but can have <b>UiaX</b> properties.
		/// This flag can be used with most other windows too.
		/// Don't use this flag when searching in elm (then it is inherited from the elm variable) or web page (role prefix <c>"web:"</c> etc).
		/// See also: <see cref="elm.MiscFlags"/>.
		/// </summary>
		UIA = 0x200,

		//Internal. See Enum_.AFFlags_Mark.
		//Mark = 0x10000,
	}

	/// <summary>
	/// Adds internal members to public enums.
	/// </summary>
	internal static partial class Enum_ {
		/// <summary>
		/// Used by <b>Delm</b>, together with <b>ElmMiscFlags_Marked</b>.
		/// </summary>
		internal static EFFlags EFFlags_Mark = (EFFlags)0x10000;

		/// <summary>
		/// Used by <b>Delm</b>, together with <b>AFFlags_Mark</b>.
		/// </summary>
		internal static EMiscFlags EMiscFlags_Marked = (EMiscFlags)128;

		internal static EXYFlags EXYFlags_DpiScaled = (EXYFlags)0x10000;
		internal static EXYFlags EXYFlags_Fail = (EXYFlags)0x20000; //currently not used
	}

	/// <summary>
	/// Flags for <see cref="elm.fromWindow"/>.
	/// </summary>
	[Flags]
	public enum EWFlags {
		/// <summary>Don't throw exception when fails. Then returns <c>null</c>.</summary>
		NoThrow = 1,

		/// <summary>
		/// Don't load dll into the target process.
		/// More info: <see cref="EFFlags.NotInProc"/>.
		/// </summary>
		NotInProc = 2,
	}

	/// <summary>
	/// Flags for <see cref="elm.fromXY"/>.
	/// </summary>
	[Flags]
	public enum EXYFlags {
		/// <summary>
		/// Don't load dll into the target process.
		/// More info: <see cref="EFFlags.NotInProc"/>.
		/// </summary>
		NotInProc = 1,

		/// <summary>
		/// Use UI Automation API.
		/// More info: <see cref="EFFlags.UIA"/>.
		/// See also flag <b>TrySmaller</b>.
		/// </summary>
		UIA = 2,

		/// <summary>
		/// Get the direct parent UI element if probably it would be much more useful, for example if its role is <b>LINK</b> or <b>BUTTON</b>.
		/// Usually links have one or more children of type <b>TEXT</b>, <b>STATICTEXT</b>, <b>IMAGE</b> or other.
		/// </summary>
		PreferLink = 4,

		/// <summary>
		/// Try to get a smaller element.
		/// 1. Try to get a smaller UI Automation element from point.
		/// 2. Try to find a descendant that is smaller. Because some incorrectly implemented UI elements don't give a child element from point.
		/// 
		/// Note: this flag is for UI element capturing tools, not for automation scripts. Its behavior may change in the future.
		/// 
		/// UI element capturing tools should use this flag only with user consent, because:
		/// 1. It's impossible to get perfect UI element in all cases. Can get wrong element.
		/// 2. In some cases can be slow.
		/// 3. In some apps some elements have bugs and the process can crash. Rare.
		/// 
		/// The <b>Find UI element</b> tool uses this flag when checked <b>Smaller</b>.
		/// This flag is ignored when not in-process, because could be too slow. Also ignored in some windows.
		/// </summary>
		TrySmaller = 8,

		/// <summary>
		/// Use UI Automation API if the default API fails and in some other cases.
		/// The <b>Find UI element</b> tool uses this flag when <b>UIA</b> is in indeterminate state.
		/// </summary>
		OrUIA = 16,

		//note: don't change values. They are passed to the cpp function.
	}

	/// <summary>
	/// Flags for <see cref="elm.focused"/>.
	/// </summary>
	[Flags]
	public enum EFocusedFlags {
		/// <summary>
		/// Don't load dll into the target process.
		/// More info: <see cref="EFFlags.NotInProc"/>.
		/// </summary>
		NotInProc = 1,

		/// <summary>
		/// Use UI Automation API.
		/// Need this flag with some windows that don't support accessible objects but support UI Automation elements. Can be used with most other windows too.
		/// More info: <see cref="EFFlags.UIA"/>.
		/// </summary>
		UIA = 2,

		//note: don't change values. They are passed to the cpp function.
	}

	/// <summary>
	/// Flags returned by <see cref="elm.MiscFlags"/>.
	/// </summary>
	[Flags]
	public enum EMiscFlags : byte {
		/// <summary>
		/// This UI element was retrieved by the dll loaded into its process.
		/// More info: <see cref="EFFlags.NotInProc"/>.
		/// </summary>
		InProc = 1,

		/// <summary>
		/// This UI element was retrieved using UI Automation API.
		/// More info: <see cref="EFFlags.UIA"/>.
		/// </summary>
		UIA = 2,

		/// <summary>
		/// This UI element was retrieved using Java Access Bridge API.
		/// More info: <see cref="elm"/>.
		/// </summary>
		Java = 4,

		//Internal. See Enum_.ElmMiscFlags_Marked.
		//Marked = 128,
	}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

	/// <summary>
	/// Object ids of window parts and some special UI elements.
	/// Used with <see cref="elm.fromWindow"/>
	/// </summary>
	/// <remarks>
	/// Most names are as in API <msdn>AccessibleObjectFromWindow</msdn> documentation but without prefix <b>OBJID_</b>.
	/// </remarks>
	public enum EObjid {
		WINDOW = 0,
		SYSMENU = -1,
		TITLEBAR = -2,
		MENU = -3,
		CLIENT = -4,
		VSCROLL = -5,
		HSCROLL = -6,
		SIZEGRIP = -7,
		CARET = -8,
		CURSOR = -9,
		ALERT = -10,
		SOUND = -11,
		/// <summary>Not used with <see cref="elm.fromWindow"/>.</summary>
		QUERYCLASSNAMEIDX = -12,
		/// <summary>Not used with <see cref="elm.fromWindow"/>.</summary>
		NATIVEOM = -16,
		/// <summary>Not used with <see cref="elm.fromWindow"/>.</summary>
		UiaRootObjectId = -25,

		//ours

		/// <summary>
		/// The root Java UI element. Can be used when the window's class name starts with <c>"SunAwt"</c>.
		/// </summary>
		Java = -100,

		/// <summary>
		/// Use UI Automation API.
		/// More info: <see cref="EFFlags.UIA"/>.
		/// </summary>
		UIA = -101,
	}

	/// <summary>
	/// Standard roles of UI elements.
	/// Used with <see cref="elm.RoleInt"/>
	/// </summary>
	/// <remarks>
	/// Most names are as in API <msdn>IAccessible.get_accRole Object Roles</msdn> documentation but without prefix <b>ROLE_SYSTEM_</b>. These are renamed: <b>PUSHBUTTON</b> to <b>BUTTON</b>, <b>CHECKBUTTON</b> to <b>CHECKBOX</b>, <b>GRAPHIC</b> to <b>IMAGE</b>, <b>OUTLINE</b> to <b>TREE</b>, <b>OUTLINEITEM</b> to <b>TREEITEM</b>, <b>OUTLINEBUTTON</b> to <b>TREEBUTTON</b>.
	/// </remarks>
	public enum ERole {
		TITLEBAR = 0x1,
		MENUBAR = 0x2,
		SCROLLBAR = 0x3,
		GRIP = 0x4,
		SOUND = 0x5,
		CURSOR = 0x6,
		CARET = 0x7,
		ALERT = 0x8,
		WINDOW = 0x9,
		CLIENT = 0xA,
		MENUPOPUP = 0xB,
		MENUITEM = 0xC,
		TOOLTIP = 0xD,
		APPLICATION = 0xE,
		DOCUMENT = 0xF,
		PANE = 0x10,
		CHART = 0x11,
		DIALOG = 0x12,
		BORDER = 0x13,
		GROUPING = 0x14,
		SEPARATOR = 0x15,
		TOOLBAR = 0x16,
		STATUSBAR = 0x17,
		TABLE = 0x18,
		COLUMNHEADER = 0x19,
		ROWHEADER = 0x1A,
		COLUMN = 0x1B,
		ROW = 0x1C,
		CELL = 0x1D,
		LINK = 0x1E,
		HELPBALLOON = 0x1F,
		CHARACTER = 0x20,
		LIST = 0x21,
		LISTITEM = 0x22,
		TREE = 0x23, //OUTLINE
		TREEITEM = 0x24, //OUTLINEITEM
		PAGETAB = 0x25,
		PROPERTYPAGE = 0x26,
		INDICATOR = 0x27,
		IMAGE = 0x28, //GRAPHIC
		STATICTEXT = 0x29,
		TEXT = 0x2A,
		BUTTON = 0x2B, //PUSHBUTTON
		CHECKBOX = 0x2C, //CHECKBUTTON
		RADIOBUTTON = 0x2D,
		COMBOBOX = 0x2E,
		DROPLIST = 0x2F,
		PROGRESSBAR = 0x30,
		DIAL = 0x31,
		HOTKEYFIELD = 0x32,
		SLIDER = 0x33,
		SPINBUTTON = 0x34,
		DIAGRAM = 0x35,
		ANIMATION = 0x36,
		EQUATION = 0x37,
		BUTTONDROPDOWN = 0x38,
		BUTTONMENU = 0x39,
		BUTTONDROPDOWNGRID = 0x3A,
		WHITESPACE = 0x3B,
		PAGETABLIST = 0x3C,
		CLOCK = 0x3D,
		SPLITBUTTON = 0x3E,
		IPADDRESS = 0x3F,
		TREEBUTTON = 0x40, //OUTLINEBUTTON

		/// <summary>Failed to get role.</summary>
		None = 0,

		/// <summary>Not one of predefined roles. Usually string.</summary>
		Custom = 0xFF,
	}

	/// <summary>
	/// UI element state flags.
	/// Used by <see cref="elm.State"/>.
	/// </summary>
	/// <remarks>
	/// Most names are as in API <msdn>IAccessible.get_accState Object State Constants</msdn> documentation but without prefix <b>STATE_SYSTEM_</b>.
	/// </remarks>
	[Flags]
	public enum EState {
		//NORMAL = 0x0,
		DISABLED = 0x1, //original name UNAVAILABLE
		SELECTED = 0x2,
		FOCUSED = 0x4,
		PRESSED = 0x8,
		CHECKED = 0x10,
		MIXED = 0x20,
		READONLY = 0x40,
		HOTTRACKED = 0x80,
		DEFAULT = 0x100,
		EXPANDED = 0x200,
		COLLAPSED = 0x400,
		BUSY = 0x800,
		FLOATING = 0x1000,
		MARQUEED = 0x2000,
		ANIMATED = 0x4000,
		INVISIBLE = 0x8000,
		OFFSCREEN = 0x10000,
		SIZEABLE = 0x20000,
		MOVEABLE = 0x40000,
		SELFVOICING = 0x80000,
		FOCUSABLE = 0x100000,
		SELECTABLE = 0x200000,
		LINKED = 0x400000,
		TRAVERSED = 0x800000,
		MULTISELECTABLE = 0x1000000,
		EXTSELECTABLE = 0x2000000,
		ALERT_LOW = 0x4000000,
		ALERT_MEDIUM = 0x8000000,
		ALERT_HIGH = 0x10000000,
		PROTECTED = 0x20000000,
		HASPOPUP = 0x40000000,
	}

	/// <summary>
	/// UI element selection flags.
	/// Used by <see cref="elm.Select"/>.
	/// </summary>
	/// <remarks>
	/// The names are as in API <msdn>IAccessible.accSelect</msdn> documentation but without prefix <b>SELFLAG_</b>.
	/// </remarks>
	[Flags]
	public enum ESelect {
		TAKEFOCUS = 0x1,
		TAKESELECTION = 0x2,
		EXTENDSELECTION = 0x4,
		ADDSELECTION = 0x8,
		REMOVESELECTION = 0x10,
	}

	/// <summary>
	/// Event constants for <see cref="More.WinEventHook"/>.
	/// </summary>
	/// <remarks>
	/// The names are as in API <msdn>SetWinEventHook Event Constants</msdn> documentation but without prefix <b>EVENT_</b>.
	/// </remarks>
	public enum EEvent {
		MIN = 0x1,
		MAX = 0x7FFFFFFF,

		SYSTEM_SOUND = 0x1,
		SYSTEM_ALERT = 0x2,
		SYSTEM_FOREGROUND = 0x3,
		SYSTEM_MENUSTART = 0x4,
		SYSTEM_MENUEND = 0x5,
		SYSTEM_MENUPOPUPSTART = 0x6,
		SYSTEM_MENUPOPUPEND = 0x7,
		SYSTEM_CAPTURESTART = 0x8,
		SYSTEM_CAPTUREEND = 0x9,
		SYSTEM_MOVESIZESTART = 0xA,
		SYSTEM_MOVESIZEEND = 0xB,
		SYSTEM_CONTEXTHELPSTART = 0xC,
		SYSTEM_CONTEXTHELPEND = 0xD,
		SYSTEM_DRAGDROPSTART = 0xE,
		SYSTEM_DRAGDROPEND = 0xF,
		SYSTEM_DIALOGSTART = 0x10,
		SYSTEM_DIALOGEND = 0x11,
		SYSTEM_SCROLLINGSTART = 0x12,
		SYSTEM_SCROLLINGEND = 0x13,
		SYSTEM_SWITCHSTART = 0x14,
		SYSTEM_SWITCHEND = 0x15,
		SYSTEM_MINIMIZESTART = 0x16,
		SYSTEM_MINIMIZEEND = 0x17,
		SYSTEM_DESKTOPSWITCH = 0x20,
		SYSTEM_SWITCHER_APPGRABBED = 0x24,
		SYSTEM_SWITCHER_APPOVERTARGET = 0x25,
		SYSTEM_SWITCHER_APPDROPPED = 0x26,
		SYSTEM_SWITCHER_CANCELLED = 0x27,
		SYSTEM_IME_KEY_NOTIFICATION = 0x29,
		SYSTEM_END = 0xFF,

		OEM_DEFINED_START = 0x101,
		OEM_DEFINED_END = 0x1FF,

		UIA_EVENTID_START = 0x4E00,
		UIA_EVENTID_END = 0x4EFF,
		UIA_PROPID_START = 0x7500,
		UIA_PROPID_END = 0x75FF,

		CONSOLE_CARET = 0x4001,
		CONSOLE_UPDATE_REGION = 0x4002,
		CONSOLE_UPDATE_SIMPLE = 0x4003,
		CONSOLE_UPDATE_SCROLL = 0x4004,
		CONSOLE_LAYOUT = 0x4005,
		CONSOLE_START_APPLICATION = 0x4006,
		CONSOLE_END_APPLICATION = 0x4007,
		CONSOLE_END = 0x40FF,

		OBJECT_CREATE = 0x8000,
		OBJECT_DESTROY = 0x8001,
		OBJECT_SHOW = 0x8002,
		OBJECT_HIDE = 0x8003,
		OBJECT_REORDER = 0x8004,
		OBJECT_FOCUS = 0x8005,
		OBJECT_SELECTION = 0x8006,
		OBJECT_SELECTIONADD = 0x8007,
		OBJECT_SELECTIONREMOVE = 0x8008,
		OBJECT_SELECTIONWITHIN = 0x8009,
		OBJECT_STATECHANGE = 0x800A,
		OBJECT_LOCATIONCHANGE = 0x800B,
		OBJECT_NAMECHANGE = 0x800C,
		OBJECT_DESCRIPTIONCHANGE = 0x800D,
		OBJECT_VALUECHANGE = 0x800E,
		OBJECT_PARENTCHANGE = 0x800F,
		OBJECT_HELPCHANGE = 0x8010,
		OBJECT_DEFACTIONCHANGE = 0x8011,
		OBJECT_ACCELERATORCHANGE = 0x8012,
		OBJECT_INVOKED = 0x8013,
		OBJECT_TEXTSELECTIONCHANGED = 0x8014,
		OBJECT_CONTENTSCROLLED = 0x8015,
		SYSTEM_ARRANGMENTPREVIEW = 0x8016,
		OBJECT_CLOAKED = 0x8017,
		OBJECT_UNCLOAKED = 0x8018,
		OBJECT_LIVEREGIONCHANGED = 0x8019,
		OBJECT_HOSTEDOBJECTSINVALIDATED = 0x8020,
		OBJECT_DRAGSTART = 0x8021,
		OBJECT_DRAGCANCEL = 0x8022,
		OBJECT_DRAGCOMPLETE = 0x8023,
		OBJECT_DRAGENTER = 0x8024,
		OBJECT_DRAGLEAVE = 0x8025,
		OBJECT_DRAGDROPPED = 0x8026,
		OBJECT_IME_SHOW = 0x8027,
		OBJECT_IME_HIDE = 0x8028,
		OBJECT_IME_CHANGE = 0x8029,
		OBJECT_TEXTEDIT_CONVERSIONTARGETCHANGED = 0x8030,
		OBJECT_END = 0x80FF,

		AIA_START = 0xA000,
		AIA_END = 0xAFFF,

		IA2_ACTION_CHANGED = 0x101,
		IA2_ACTIVE_DESCENDANT_CHANGED = 0x102,
		IA2_DOCUMENT_ATTRIBUTE_CHANGED = 0x103,
		IA2_DOCUMENT_CONTENT_CHANGED = 0x104,
		IA2_DOCUMENT_LOAD_COMPLETE = 0x105,
		IA2_DOCUMENT_LOAD_STOPPED = 0x106,
		IA2_DOCUMENT_RELOAD = 0x107,
		IA2_HYPERLINK_END_INDEX_CHANGED = 0x108,
		IA2_HYPERLINK_NUMBER_OF_ANCHORS_CHANGED = 0x109,
		IA2_HYPERLINK_SELECTED_LINK_CHANGED = 0x10a,
		IA2_HYPERTEXT_LINK_ACTIVATED = 0x10b,
		IA2_HYPERTEXT_LINK_SELECTED = 0x10c,
		IA2_HYPERLINK_START_INDEX_CHANGED = 0x10d,
		IA2_HYPERTEXT_CHANGED = 0x10e,
		IA2_HYPERTEXT_NLINKS_CHANGED = 0x10f,
		IA2_OBJECT_ATTRIBUTE_CHANGED = 0x110,
		IA2_PAGE_CHANGED = 0x111,
		IA2_SECTION_CHANGED = 0x112,
		IA2_TABLE_CAPTION_CHANGED = 0x113,
		IA2_TABLE_COLUMN_DESCRIPTION_CHANGED = 0x114,
		IA2_TABLE_COLUMN_HEADER_CHANGED = 0x115,
		IA2_TABLE_MODEL_CHANGED = 0x116,
		IA2_TABLE_ROW_DESCRIPTION_CHANGED = 0x117,
		IA2_TABLE_ROW_HEADER_CHANGED = 0x118,
		IA2_TABLE_SUMMARY_CHANGED = 0x119,
		IA2_TEXT_ATTRIBUTE_CHANGED = 0x11a,
		IA2_TEXT_CARET_MOVED = 0x11b,
		IA2_TEXT_CHANGED = 0x11c,
		IA2_TEXT_COLUMN_CHANGED = 0x11d,
		IA2_TEXT_INSERTED = 0x11e,
		IA2_TEXT_REMOVED = 0x11f,
		IA2_TEXT_UPDATED = 0x120,
		IA2_TEXT_SELECTION_CHANGED = 0x121,
		IA2_VISIBLE_DATA_CHANGED = 0x122,
	}

	/// <summary>
	/// Flags for <see cref="More.WinEventHook"/>.
	/// </summary>
	/// <remarks>
	/// The names are as in API <msdn>SetWinEventHook</msdn> documentation but without prefix <b>WINEVENT_</b>.
	/// There are no flags for <b>OUTOFCONTEXT</b> and <b>INCONTEXT</b>. <b>OUTOFCONTEXT</b> is default (0). <b>INCONTEXT</b> cannot be used in managed code.
	/// </remarks>
	[Flags]
	public enum EHookFlags {
		None,
		/// <summary>Don't receive events generated by this thread.</summary>
		SKIPOWNTHREAD = 0x1,

		/// <summary>Don't receive events generated by threads of this process.</summary>
		SKIPOWNPROCESS = 0x2,

		//OUTOFCONTEXT = 0x0,
		//INCONTEXT = 0x4,
	}

	/// <summary>
	/// Used with <see cref="elm.GetProperties"/>.
	/// </summary>
	public class EProperties {
		public string Role, Name, Value, Description, Help, DefaultAction, KeyboardShortcut, UiaId, UiaCN, OuterHtml, InnerHtml;
		public EState State;
		public RECT Rect;
		public wnd WndContainer;
		public Dictionary<string, string> HtmlAttributes;
	}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
