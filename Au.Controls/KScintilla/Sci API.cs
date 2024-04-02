namespace Au.Controls;

public static unsafe class Sci {
	#region au modifications

	public const int SCI_MARGINSTYLENEXT = 9502;
	public delegate void Sci_NotifyCallback(void* cbParam, ref SCNotification n);
	public const int SCI_SETNOTIFYCALLBACK = 9503;
	public delegate int Sci_AnnotationDrawCallback(void* cbParam, ref Sci_AnnotationDrawCallbackData d);
	public const int SCI_SETANNOTATIONDRAWCALLBACK = 9504;
	public delegate void Sci_MarginDrawCallback(ref Sci_MarginDrawCallbackData d);
	public const int SCI_SETMARGINDRAWCALLBACK = 9505;
	public const int SCI_ISXINMARGIN = 9506;
	public const int SCI_DRAGDROP = 9507;

	[DllImport("Scintilla", EntryPoint = "Scintilla_DirectFunction")]
	public static extern nint Sci_Call(nint sci, int message, nint wParam = 0, nint lParam = 0);

	[DllImport("Scintilla")]
	public static extern int Sci_Range(nint sci, int start8, int end8, out byte* p1, out byte* p2, int* length = null);

	[DllImport("Scintilla")]
	public static extern void Sci_SetFoldLevels(nint sci, int line, int lastLine, int len, int* a);

	public record struct Sci_VisibleRange {
		public int dlineFrom, dlineTo, vlineFrom, vlineTo, posFrom, posTo;
	}

	/// <summary>
	/// flags: 1 need pos
	/// </summary>
	[DllImport("Scintilla")]
	public static extern void Sci_GetVisibleRange(nint sci, out Sci_VisibleRange r);

	//[DllImport("Lexilla", EntryPoint = "CreateLexer")]
	//public static extern nint Sci_CreateLexer(byte[] lexer);

#pragma warning disable 649
	public unsafe struct Sci_AnnotationDrawCallbackData {
		public int step;
		public IntPtr hdc;
		public RECT rect;
		public byte* text;
		public int textLen, line, annotLine;
	};
	public unsafe struct Sci_MarginDrawCallbackData {
		public IntPtr hdc;
		public RECT rect;
		public int margin, firstLine, lastLine;
	};
	public struct Sci_DragDropData {
		public int x, y;
		public byte* text;
		public int len;
		public int copy; //bool
	}
#pragma warning restore 649

	//[DllImport("Scintilla")]
	//public static extern bool Sci_CanUndoRedoContainer(nint sci, bool redo, int token);

	[DllImport("Scintilla")]
	public static extern void Sci_SetUndoMark(nint sci, int mark);

	[DllImport("Scintilla")]
	public static extern int Sci_GetUndoMark(nint sci, bool redo);

	public const int STYLE_HIDDEN = 31; //DEFAULT-1

	#endregion

	public delegate nint SciFnDirectStatus(nint ptr, int iMessage, nint wParam, nint lParam, out int pStatus);

	public const int INVALID_POSITION = -1;
	public const int SCI_START = 2000;
	public const int SCI_OPTIONAL_START = 3000;
	public const int SCI_LEXER_START = 4000;
	public const int SCI_ADDTEXT = 2001;
	public const int SCI_ADDSTYLEDTEXT = 2002;
	public const int SCI_INSERTTEXT = 2003;
	public const int SCI_CHANGEINSERTION = 2672;
	public const int SCI_CLEARALL = 2004;
	public const int SCI_DELETERANGE = 2645;
	public const int SCI_CLEARDOCUMENTSTYLE = 2005;
	public const int SCI_GETLENGTH = 2006;
	public const int SCI_GETCHARAT = 2007;
	public const int SCI_GETCURRENTPOS = 2008;
	public const int SCI_GETANCHOR = 2009;
	public const int SCI_GETSTYLEAT = 2010;
	public const int SCI_REDO = 2011;
	public const int SCI_SETUNDOCOLLECTION = 2012;
	public const int SCI_SELECTALL = 2013;
	public const int SCI_SETSAVEPOINT = 2014;
	public const int SCI_GETSTYLEDTEXT = 2015;
	public const int SCI_CANREDO = 2016;
	public const int SCI_MARKERLINEFROMHANDLE = 2017;
	public const int SCI_MARKERDELETEHANDLE = 2018;
	public const int SCI_MARKERHANDLEFROMLINE = 2732;
	public const int SCI_MARKERNUMBERFROMLINE = 2733;
	public const int SCI_GETUNDOCOLLECTION = 2019;
	public const int SCWS_INVISIBLE = 0;
	public const int SCWS_VISIBLEALWAYS = 1;
	public const int SCWS_VISIBLEAFTERINDENT = 2;
	public const int SCWS_VISIBLEONLYININDENT = 3;
	public const int SCI_GETVIEWWS = 2020;
	public const int SCI_SETVIEWWS = 2021;
	public const int SCTD_LONGARROW = 0;
	public const int SCTD_STRIKEOUT = 1;
	public const int SCI_GETTABDRAWMODE = 2698;
	public const int SCI_SETTABDRAWMODE = 2699;
	public const int SCI_POSITIONFROMPOINT = 2022;
	public const int SCI_POSITIONFROMPOINTCLOSE = 2023;
	public const int SCI_GOTOLINE = 2024;
	public const int SCI_GOTOPOS = 2025;
	public const int SCI_SETANCHOR = 2026;
	public const int SCI_GETCURLINE = 2027;
	public const int SCI_GETENDSTYLED = 2028;
	public const int SC_EOL_CRLF = 0;
	public const int SC_EOL_CR = 1;
	public const int SC_EOL_LF = 2;
	public const int SCI_CONVERTEOLS = 2029;
	public const int SCI_GETEOLMODE = 2030;
	public const int SCI_SETEOLMODE = 2031;
	public const int SCI_STARTSTYLING = 2032;
	public const int SCI_SETSTYLING = 2033;
	public const int SCI_GETBUFFEREDDRAW = 2034;
	public const int SCI_SETBUFFEREDDRAW = 2035;
	public const int SCI_SETTABWIDTH = 2036;
	public const int SCI_GETTABWIDTH = 2121;
	public const int SCI_CLEARTABSTOPS = 2675;
	public const int SCI_ADDTABSTOP = 2676;
	public const int SCI_GETNEXTTABSTOP = 2677;
	public const int SC_CP_UTF8 = 65001;
	public const int SCI_SETCODEPAGE = 2037;
	public const int SCI_SETFONTLOCALE = 2760;
	public const int SCI_GETFONTLOCALE = 2761;
	public const int SC_IME_WINDOWED = 0;
	public const int SC_IME_INLINE = 1;
	public const int SCI_GETIMEINTERACTION = 2678;
	public const int SCI_SETIMEINTERACTION = 2679;
	public const int MARKER_MAX = 31;
	public const int SC_MARK_CIRCLE = 0;
	public const int SC_MARK_ROUNDRECT = 1;
	public const int SC_MARK_ARROW = 2;
	public const int SC_MARK_SMALLRECT = 3;
	public const int SC_MARK_SHORTARROW = 4;
	public const int SC_MARK_EMPTY = 5;
	public const int SC_MARK_ARROWDOWN = 6;
	public const int SC_MARK_MINUS = 7;
	public const int SC_MARK_PLUS = 8;
	public const int SC_MARK_VLINE = 9;
	public const int SC_MARK_LCORNER = 10;
	public const int SC_MARK_TCORNER = 11;
	public const int SC_MARK_BOXPLUS = 12;
	public const int SC_MARK_BOXPLUSCONNECTED = 13;
	public const int SC_MARK_BOXMINUS = 14;
	public const int SC_MARK_BOXMINUSCONNECTED = 15;
	public const int SC_MARK_LCORNERCURVE = 16;
	public const int SC_MARK_TCORNERCURVE = 17;
	public const int SC_MARK_CIRCLEPLUS = 18;
	public const int SC_MARK_CIRCLEPLUSCONNECTED = 19;
	public const int SC_MARK_CIRCLEMINUS = 20;
	public const int SC_MARK_CIRCLEMINUSCONNECTED = 21;
	public const int SC_MARK_BACKGROUND = 22;
	public const int SC_MARK_DOTDOTDOT = 23;
	public const int SC_MARK_ARROWS = 24;
	public const int SC_MARK_PIXMAP = 25;
	public const int SC_MARK_FULLRECT = 26;
	public const int SC_MARK_LEFTRECT = 27;
	public const int SC_MARK_AVAILABLE = 28;
	public const int SC_MARK_UNDERLINE = 29;
	public const int SC_MARK_RGBAIMAGE = 30;
	public const int SC_MARK_BOOKMARK = 31;
	public const int SC_MARK_VERTICALBOOKMARK = 32;
	public const int SC_MARK_CHARACTER = 10000;
	public const int SC_MARKNUM_FOLDEREND = 25;
	public const int SC_MARKNUM_FOLDEROPENMID = 26;
	public const int SC_MARKNUM_FOLDERMIDTAIL = 27;
	public const int SC_MARKNUM_FOLDERTAIL = 28;
	public const int SC_MARKNUM_FOLDERSUB = 29;
	public const int SC_MARKNUM_FOLDER = 30;
	public const int SC_MARKNUM_FOLDEROPEN = 31;
	public const int SC_MASK_FOLDERS = unchecked((int)0xFE000000);
	public const int SCI_MARKERDEFINE = 2040;
	public const int SCI_MARKERSETFORE = 2041;
	public const int SCI_MARKERSETBACK = 2042;
	public const int SCI_MARKERSETBACKSELECTED = 2292;
	public const int SCI_MARKERSETFORETRANSLUCENT = 2294;
	public const int SCI_MARKERSETBACKTRANSLUCENT = 2295;
	public const int SCI_MARKERSETBACKSELECTEDTRANSLUCENT = 2296;
	public const int SCI_MARKERSETSTROKEWIDTH = 2297;
	public const int SCI_MARKERENABLEHIGHLIGHT = 2293;
	public const int SCI_MARKERADD = 2043;
	public const int SCI_MARKERDELETE = 2044;
	public const int SCI_MARKERDELETEALL = 2045;
	public const int SCI_MARKERGET = 2046;
	public const int SCI_MARKERNEXT = 2047;
	public const int SCI_MARKERPREVIOUS = 2048;
	public const int SCI_MARKERDEFINEPIXMAP = 2049;
	public const int SCI_MARKERADDSET = 2466;
	public const int SCI_MARKERSETALPHA = 2476;
	public const int SCI_MARKERGETLAYER = 2734;
	public const int SCI_MARKERSETLAYER = 2735;
	public const int SC_MAX_MARGIN = 4;
	public const int SC_MARGIN_SYMBOL = 0;
	public const int SC_MARGIN_NUMBER = 1;
	public const int SC_MARGIN_BACK = 2;
	public const int SC_MARGIN_FORE = 3;
	public const int SC_MARGIN_TEXT = 4;
	public const int SC_MARGIN_RTEXT = 5;
	public const int SC_MARGIN_COLOUR = 6;
	public const int SCI_SETMARGINTYPEN = 2240;
	public const int SCI_GETMARGINTYPEN = 2241;
	public const int SCI_SETMARGINWIDTHN = 2242;
	public const int SCI_GETMARGINWIDTHN = 2243;
	public const int SCI_SETMARGINMASKN = 2244;
	public const int SCI_GETMARGINMASKN = 2245;
	public const int SCI_SETMARGINSENSITIVEN = 2246;
	public const int SCI_GETMARGINSENSITIVEN = 2247;
	public const int SCI_SETMARGINCURSORN = 2248;
	public const int SCI_GETMARGINCURSORN = 2249;
	public const int SCI_SETMARGINBACKN = 2250;
	public const int SCI_GETMARGINBACKN = 2251;
	public const int SCI_SETMARGINS = 2252;
	public const int SCI_GETMARGINS = 2253;
	public const int STYLE_DEFAULT = 32;
	public const int STYLE_LINENUMBER = 33;
	public const int STYLE_BRACELIGHT = 34;
	public const int STYLE_BRACEBAD = 35;
	public const int STYLE_CONTROLCHAR = 36;
	public const int STYLE_INDENTGUIDE = 37;
	public const int STYLE_CALLTIP = 38;
	public const int STYLE_FOLDDISPLAYTEXT = 39;
	public const int STYLE_LASTPREDEFINED = 39;
	public const int STYLE_MAX = 255;
	public const int SC_CHARSET_ANSI = 0;
	public const int SC_CHARSET_DEFAULT = 1;
	public const int SC_CHARSET_BALTIC = 186;
	public const int SC_CHARSET_CHINESEBIG5 = 136;
	public const int SC_CHARSET_EASTEUROPE = 238;
	public const int SC_CHARSET_GB2312 = 134;
	public const int SC_CHARSET_GREEK = 161;
	public const int SC_CHARSET_HANGUL = 129;
	public const int SC_CHARSET_MAC = 77;
	public const int SC_CHARSET_OEM = 255;
	public const int SC_CHARSET_RUSSIAN = 204;
	public const int SC_CHARSET_OEM866 = 866;
	public const int SC_CHARSET_CYRILLIC = 1251;
	public const int SC_CHARSET_SHIFTJIS = 128;
	public const int SC_CHARSET_SYMBOL = 2;
	public const int SC_CHARSET_TURKISH = 162;
	public const int SC_CHARSET_JOHAB = 130;
	public const int SC_CHARSET_HEBREW = 177;
	public const int SC_CHARSET_ARABIC = 178;
	public const int SC_CHARSET_VIETNAMESE = 163;
	public const int SC_CHARSET_THAI = 222;
	public const int SC_CHARSET_8859_15 = 1000;
	public const int SCI_STYLECLEARALL = 2050;
	public const int SCI_STYLESETFORE = 2051;
	public const int SCI_STYLESETBACK = 2052;
	public const int SCI_STYLESETBOLD = 2053;
	public const int SCI_STYLESETITALIC = 2054;
	public const int SCI_STYLESETSIZE = 2055;
	public const int SCI_STYLESETFONT = 2056;
	public const int SCI_STYLESETEOLFILLED = 2057;
	public const int SCI_STYLERESETDEFAULT = 2058;
	public const int SCI_STYLESETUNDERLINE = 2059;
	public const int SC_CASE_MIXED = 0;
	public const int SC_CASE_UPPER = 1;
	public const int SC_CASE_LOWER = 2;
	public const int SC_CASE_CAMEL = 3;
	public const int SCI_STYLEGETFORE = 2481;
	public const int SCI_STYLEGETBACK = 2482;
	public const int SCI_STYLEGETBOLD = 2483;
	public const int SCI_STYLEGETITALIC = 2484;
	public const int SCI_STYLEGETSIZE = 2485;
	public const int SCI_STYLEGETFONT = 2486;
	public const int SCI_STYLEGETEOLFILLED = 2487;
	public const int SCI_STYLEGETUNDERLINE = 2488;
	public const int SCI_STYLEGETCASE = 2489;
	public const int SCI_STYLEGETCHARACTERSET = 2490;
	public const int SCI_STYLEGETVISIBLE = 2491;
	public const int SCI_STYLEGETCHANGEABLE = 2492;
	public const int SCI_STYLEGETHOTSPOT = 2493;
	public const int SCI_STYLESETCASE = 2060;
	public const int SC_FONT_SIZE_MULTIPLIER = 100;
	public const int SCI_STYLESETSIZEFRACTIONAL = 2061;
	public const int SCI_STYLEGETSIZEFRACTIONAL = 2062;
	public const int SC_WEIGHT_NORMAL = 400;
	public const int SC_WEIGHT_SEMIBOLD = 600;
	public const int SC_WEIGHT_BOLD = 700;
	public const int SCI_STYLESETWEIGHT = 2063;
	public const int SCI_STYLEGETWEIGHT = 2064;
	public const int SCI_STYLESETCHARACTERSET = 2066;
	public const int SCI_STYLESETHOTSPOT = 2409;
	public const int SCI_STYLESETCHECKMONOSPACED = 2254;
	public const int SCI_STYLEGETCHECKMONOSPACED = 2255;
	public const int SC_ELEMENT_LIST = 0;
	public const int SC_ELEMENT_LIST_BACK = 1;
	public const int SC_ELEMENT_LIST_SELECTED = 2;
	public const int SC_ELEMENT_LIST_SELECTED_BACK = 3;
	public const int SC_ELEMENT_SELECTION_TEXT = 10;
	public const int SC_ELEMENT_SELECTION_BACK = 11;
	public const int SC_ELEMENT_SELECTION_ADDITIONAL_TEXT = 12;
	public const int SC_ELEMENT_SELECTION_ADDITIONAL_BACK = 13;
	public const int SC_ELEMENT_SELECTION_SECONDARY_TEXT = 14;
	public const int SC_ELEMENT_SELECTION_SECONDARY_BACK = 15;
	public const int SC_ELEMENT_SELECTION_INACTIVE_TEXT = 16;
	public const int SC_ELEMENT_SELECTION_INACTIVE_BACK = 17;
	public const int SC_ELEMENT_CARET = 40;
	public const int SC_ELEMENT_CARET_ADDITIONAL = 41;
	public const int SC_ELEMENT_CARET_LINE_BACK = 50;
	public const int SC_ELEMENT_WHITE_SPACE = 60;
	public const int SC_ELEMENT_WHITE_SPACE_BACK = 61;
	public const int SC_ELEMENT_HOT_SPOT_ACTIVE = 70;
	public const int SC_ELEMENT_HOT_SPOT_ACTIVE_BACK = 71;
	public const int SC_ELEMENT_FOLD_LINE = 80;
	public const int SC_ELEMENT_HIDDEN_LINE = 81;
	public const int SCI_SETELEMENTCOLOUR = 2753;
	public const int SCI_GETELEMENTCOLOUR = 2754;
	public const int SCI_RESETELEMENTCOLOUR = 2755;
	public const int SCI_GETELEMENTISSET = 2756;
	public const int SCI_GETELEMENTALLOWSTRANSLUCENT = 2757;
	public const int SCI_GETELEMENTBASECOLOUR = 2758;
	public const int SCI_SETSELFORE = 2067;
	public const int SCI_SETSELBACK = 2068;
	public const int SCI_GETSELALPHA = 2477;
	public const int SCI_SETSELALPHA = 2478;
	public const int SCI_GETSELEOLFILLED = 2479;
	public const int SCI_SETSELEOLFILLED = 2480;
	public const int SC_LAYER_BASE = 0;
	public const int SC_LAYER_UNDER_TEXT = 1;
	public const int SC_LAYER_OVER_TEXT = 2;
	public const int SCI_GETSELECTIONLAYER = 2762;
	public const int SCI_SETSELECTIONLAYER = 2763;
	public const int SCI_GETCARETLINELAYER = 2764;
	public const int SCI_SETCARETLINELAYER = 2765;
	public const int SCI_GETCARETLINEHIGHLIGHTSUBLINE = 2773;
	public const int SCI_SETCARETLINEHIGHLIGHTSUBLINE = 2774;
	public const int SCI_SETCARETFORE = 2069;
	public const int SCI_ASSIGNCMDKEY = 2070;
	public const int SCI_CLEARCMDKEY = 2071;
	public const int SCI_CLEARALLCMDKEYS = 2072;
	public const int SCI_SETSTYLINGEX = 2073;
	public const int SCI_STYLESETVISIBLE = 2074;
	public const int SCI_GETCARETPERIOD = 2075;
	public const int SCI_SETCARETPERIOD = 2076;
	public const int SCI_SETWORDCHARS = 2077;
	public const int SCI_GETWORDCHARS = 2646;
	public const int SCI_SETCHARACTERCATEGORYOPTIMIZATION = 2720;
	public const int SCI_GETCHARACTERCATEGORYOPTIMIZATION = 2721;
	public const int SCI_BEGINUNDOACTION = 2078;
	public const int SCI_ENDUNDOACTION = 2079;
	public const int INDIC_PLAIN = 0;
	public const int INDIC_SQUIGGLE = 1;
	public const int INDIC_TT = 2;
	public const int INDIC_DIAGONAL = 3;
	public const int INDIC_STRIKE = 4;
	public const int INDIC_HIDDEN = 5;
	public const int INDIC_BOX = 6;
	public const int INDIC_ROUNDBOX = 7;
	public const int INDIC_STRAIGHTBOX = 8;
	public const int INDIC_DASH = 9;
	public const int INDIC_DOTS = 10;
	public const int INDIC_SQUIGGLELOW = 11;
	public const int INDIC_DOTBOX = 12;
	public const int INDIC_SQUIGGLEPIXMAP = 13;
	public const int INDIC_COMPOSITIONTHICK = 14;
	public const int INDIC_COMPOSITIONTHIN = 15;
	public const int INDIC_FULLBOX = 16;
	public const int INDIC_TEXTFORE = 17;
	public const int INDIC_POINT = 18;
	public const int INDIC_POINTCHARACTER = 19;
	public const int INDIC_GRADIENT = 20;
	public const int INDIC_GRADIENTCENTRE = 21;
	//deprecated
	//public const int INDIC_IME = 32;
	//public const int INDIC_IME_MAX = 35;
	//public const int INDIC_MAX = 35;
	//public const int INDIC_CONTAINER = 8;
	//public const int INDIC0_MASK = 0x20;
	//public const int INDIC1_MASK = 0x40;
	//public const int INDIC2_MASK = 0x80;
	//public const int INDICS_MASK = 0xE0;
	public const int SCI_INDICSETSTYLE = 2080;
	public const int SCI_INDICGETSTYLE = 2081;
	public const int SCI_INDICSETFORE = 2082;
	public const int SCI_INDICGETFORE = 2083;
	public const int SCI_INDICSETUNDER = 2510;
	public const int SCI_INDICGETUNDER = 2511;
	public const int SCI_INDICSETHOVERSTYLE = 2680;
	public const int SCI_INDICGETHOVERSTYLE = 2681;
	public const int SCI_INDICSETHOVERFORE = 2682;
	public const int SCI_INDICGETHOVERFORE = 2683;
	public const int SC_INDICVALUEBIT = 0x1000000;
	public const int SC_INDICVALUEMASK = 0xFFFFFF;
	public const int SC_INDICFLAG_NONE = 0;
	public const int SC_INDICFLAG_VALUEFORE = 1;
	public const int SCI_INDICSETFLAGS = 2684;
	public const int SCI_INDICGETFLAGS = 2685;
	public const int SCI_INDICSETSTROKEWIDTH = 2751;
	public const int SCI_INDICGETSTROKEWIDTH = 2752;
	public const int SCI_SETWHITESPACEFORE = 2084;
	public const int SCI_SETWHITESPACEBACK = 2085;
	public const int SCI_SETWHITESPACESIZE = 2086;
	public const int SCI_GETWHITESPACESIZE = 2087;
	//deprecated
	//public const int SCI_SETSTYLEBITS = 2090;
	//public const int SCI_GETSTYLEBITS = 2091;
	public const int SCI_SETLINESTATE = 2092;
	public const int SCI_GETLINESTATE = 2093;
	public const int SCI_GETMAXLINESTATE = 2094;
	public const int SCI_GETCARETLINEVISIBLE = 2095;
	public const int SCI_SETCARETLINEVISIBLE = 2096;
	public const int SCI_GETCARETLINEBACK = 2097;
	public const int SCI_SETCARETLINEBACK = 2098;
	public const int SCI_GETCARETLINEFRAME = 2704;
	public const int SCI_SETCARETLINEFRAME = 2705;
	public const int SCI_STYLESETCHANGEABLE = 2099;
	public const int SCI_AUTOCSHOW = 2100;
	public const int SCI_AUTOCCANCEL = 2101;
	public const int SCI_AUTOCACTIVE = 2102;
	public const int SCI_AUTOCPOSSTART = 2103;
	public const int SCI_AUTOCCOMPLETE = 2104;
	public const int SCI_AUTOCSTOPS = 2105;
	public const int SCI_AUTOCSETSEPARATOR = 2106;
	public const int SCI_AUTOCGETSEPARATOR = 2107;
	public const int SCI_AUTOCSELECT = 2108;
	public const int SCI_AUTOCSETCANCELATSTART = 2110;
	public const int SCI_AUTOCGETCANCELATSTART = 2111;
	public const int SCI_AUTOCSETFILLUPS = 2112;
	public const int SCI_AUTOCSETCHOOSESINGLE = 2113;
	public const int SCI_AUTOCGETCHOOSESINGLE = 2114;
	public const int SCI_AUTOCSETIGNORECASE = 2115;
	public const int SCI_AUTOCGETIGNORECASE = 2116;
	public const int SCI_USERLISTSHOW = 2117;
	public const int SCI_AUTOCSETAUTOHIDE = 2118;
	public const int SCI_AUTOCGETAUTOHIDE = 2119;
	public const int SC_AUTOCOMPLETE_NORMAL = 0;
	public const int SC_AUTOCOMPLETE_FIXED_SIZE = 1;
	public const int SCI_AUTOCSETOPTIONS = 2638;
	public const int SCI_AUTOCGETOPTIONS = 2639;
	public const int SCI_AUTOCSETDROPRESTOFWORD = 2270;
	public const int SCI_AUTOCGETDROPRESTOFWORD = 2271;
	public const int SCI_REGISTERIMAGE = 2405;
	public const int SCI_CLEARREGISTEREDIMAGES = 2408;
	public const int SCI_AUTOCGETTYPESEPARATOR = 2285;
	public const int SCI_AUTOCSETTYPESEPARATOR = 2286;
	public const int SCI_AUTOCSETMAXWIDTH = 2208;
	public const int SCI_AUTOCGETMAXWIDTH = 2209;
	public const int SCI_AUTOCSETMAXHEIGHT = 2210;
	public const int SCI_AUTOCGETMAXHEIGHT = 2211;
	public const int SCI_SETINDENT = 2122;
	public const int SCI_GETINDENT = 2123;
	public const int SCI_SETUSETABS = 2124;
	public const int SCI_GETUSETABS = 2125;
	public const int SCI_SETLINEINDENTATION = 2126;
	public const int SCI_GETLINEINDENTATION = 2127;
	public const int SCI_GETLINEINDENTPOSITION = 2128;
	public const int SCI_GETCOLUMN = 2129;
	public const int SCI_COUNTCHARACTERS = 2633;
	public const int SCI_COUNTCODEUNITS = 2715;
	public const int SCI_SETHSCROLLBAR = 2130;
	public const int SCI_GETHSCROLLBAR = 2131;
	public const int SC_IV_NONE = 0;
	public const int SC_IV_REAL = 1;
	public const int SC_IV_LOOKFORWARD = 2;
	public const int SC_IV_LOOKBOTH = 3;
	public const int SCI_SETINDENTATIONGUIDES = 2132;
	public const int SCI_GETINDENTATIONGUIDES = 2133;
	public const int SCI_SETHIGHLIGHTGUIDE = 2134;
	public const int SCI_GETHIGHLIGHTGUIDE = 2135;
	public const int SCI_GETLINEENDPOSITION = 2136;
	public const int SCI_GETCODEPAGE = 2137;
	public const int SCI_GETCARETFORE = 2138;
	public const int SCI_GETREADONLY = 2140;
	public const int SCI_SETCURRENTPOS = 2141;
	public const int SCI_SETSELECTIONSTART = 2142;
	public const int SCI_GETSELECTIONSTART = 2143;
	public const int SCI_SETSELECTIONEND = 2144;
	public const int SCI_GETSELECTIONEND = 2145;
	public const int SCI_SETEMPTYSELECTION = 2556;
	public const int SCI_SETPRINTMAGNIFICATION = 2146;
	public const int SCI_GETPRINTMAGNIFICATION = 2147;
	public const int SC_PRINT_NORMAL = 0;
	public const int SC_PRINT_INVERTLIGHT = 1;
	public const int SC_PRINT_BLACKONWHITE = 2;
	public const int SC_PRINT_COLOURONWHITE = 3;
	public const int SC_PRINT_COLOURONWHITEDEFAULTBG = 4;
	public const int SC_PRINT_SCREENCOLOURS = 5;
	public const int SCI_SETPRINTCOLOURMODE = 2148;
	public const int SCI_GETPRINTCOLOURMODE = 2149;
	public const int SCFIND_WHOLEWORD = 0x2;
	public const int SCFIND_MATCHCASE = 0x4;
	public const int SCFIND_WORDSTART = 0x00100000;
	public const int SCFIND_REGEXP = 0x00200000;
	public const int SCFIND_POSIX = 0x00400000;
	public const int SCFIND_CXX11REGEX = 0x00800000;
	public const int SCI_FINDTEXT = 2150;
	public const int SCI_FORMATRANGE = 2151;
	public const int SCI_GETFIRSTVISIBLELINE = 2152;
	public const int SCI_GETLINE = 2153;
	public const int SCI_GETLINECOUNT = 2154;
	public const int SCI_ALLOCATELINES = 2089;
	public const int SCI_SETMARGINLEFT = 2155;
	public const int SCI_GETMARGINLEFT = 2156;
	public const int SCI_SETMARGINRIGHT = 2157;
	public const int SCI_GETMARGINRIGHT = 2158;
	public const int SCI_GETMODIFY = 2159;
	public const int SCI_SETSEL = 2160;
	public const int SCI_GETSELTEXT = 2161;
	public const int SCI_GETTEXTRANGE = 2162;
	public const int SCI_HIDESELECTION = 2163;
	public const int SCI_POINTXFROMPOSITION = 2164;
	public const int SCI_POINTYFROMPOSITION = 2165;
	public const int SCI_LINEFROMPOSITION = 2166;
	public const int SCI_POSITIONFROMLINE = 2167;
	public const int SCI_LINESCROLL = 2168;
	public const int SCI_SCROLLCARET = 2169;
	public const int SCI_SCROLLRANGE = 2569;
	public const int SCI_REPLACESEL = 2170;
	public const int SCI_SETREADONLY = 2171;
	public const int SCI_NULL = 2172;
	public const int SCI_CANPASTE = 2173;
	public const int SCI_CANUNDO = 2174;
	public const int SCI_EMPTYUNDOBUFFER = 2175;
	public const int SCI_UNDO = 2176;
	public const int SCI_CUT = 2177;
	public const int SCI_COPY = 2178;
	public const int SCI_PASTE = 2179;
	public const int SCI_CLEAR = 2180;
	public const int SCI_SETTEXT = 2181;
	public const int SCI_GETTEXT = 2182;
	public const int SCI_GETTEXTLENGTH = 2183;
	public const int SCI_GETDIRECTFUNCTION = 2184;
	public const int SCI_GETDIRECTSTATUSFUNCTION = 2772;
	public const int SCI_GETDIRECTPOINTER = 2185;
	public const int SCI_SETOVERTYPE = 2186;
	public const int SCI_GETOVERTYPE = 2187;
	public const int SCI_SETCARETWIDTH = 2188;
	public const int SCI_GETCARETWIDTH = 2189;
	public const int SCI_SETTARGETSTART = 2190;
	public const int SCI_GETTARGETSTART = 2191;
	public const int SCI_SETTARGETEND = 2192;
	public const int SCI_GETTARGETEND = 2193;
	public const int SCI_SETTARGETRANGE = 2686;
	public const int SCI_GETTARGETTEXT = 2687;
	public const int SCI_TARGETFROMSELECTION = 2287;
	public const int SCI_TARGETWHOLEDOCUMENT = 2690;
	public const int SCI_REPLACETARGET = 2194;
	public const int SCI_REPLACETARGETRE = 2195;
	public const int SCI_SEARCHINTARGET = 2197;
	public const int SCI_SETSEARCHFLAGS = 2198;
	public const int SCI_GETSEARCHFLAGS = 2199;
	public const int SCI_CALLTIPSHOW = 2200;
	public const int SCI_CALLTIPCANCEL = 2201;
	public const int SCI_CALLTIPACTIVE = 2202;
	public const int SCI_CALLTIPPOSSTART = 2203;
	public const int SCI_CALLTIPSETPOSSTART = 2214;
	public const int SCI_CALLTIPSETHLT = 2204;
	public const int SCI_CALLTIPSETBACK = 2205;
	public const int SCI_CALLTIPSETFORE = 2206;
	public const int SCI_CALLTIPSETFOREHLT = 2207;
	public const int SCI_CALLTIPUSESTYLE = 2212;
	public const int SCI_CALLTIPSETPOSITION = 2213;
	public const int SCI_VISIBLEFROMDOCLINE = 2220;
	public const int SCI_DOCLINEFROMVISIBLE = 2221;
	public const int SCI_WRAPCOUNT = 2235;
	public const int SC_FOLDLEVELNONE = 0x0;
	public const int SC_FOLDLEVELBASE = 0x400;
	public const int SC_FOLDLEVELWHITEFLAG = 0x1000;
	public const int SC_FOLDLEVELHEADERFLAG = 0x2000;
	public const int SC_FOLDLEVELNUMBERMASK = 0x0FFF;
	public const int SCI_SETFOLDLEVEL = 2222;
	public const int SCI_GETFOLDLEVEL = 2223;
	public const int SCI_GETLASTCHILD = 2224;
	public const int SCI_GETFOLDPARENT = 2225;
	public const int SCI_SHOWLINES = 2226;
	public const int SCI_HIDELINES = 2227;
	public const int SCI_GETLINEVISIBLE = 2228;
	public const int SCI_GETALLLINESVISIBLE = 2236;
	public const int SCI_SETFOLDEXPANDED = 2229;
	public const int SCI_GETFOLDEXPANDED = 2230;
	public const int SCI_TOGGLEFOLD = 2231;
	public const int SCI_TOGGLEFOLDSHOWTEXT = 2700;
	public const int SC_FOLDDISPLAYTEXT_HIDDEN = 0;
	public const int SC_FOLDDISPLAYTEXT_STANDARD = 1;
	public const int SC_FOLDDISPLAYTEXT_BOXED = 2;
	public const int SCI_FOLDDISPLAYTEXTSETSTYLE = 2701;
	public const int SCI_FOLDDISPLAYTEXTGETSTYLE = 2707;
	public const int SCI_SETDEFAULTFOLDDISPLAYTEXT = 2722;
	public const int SCI_GETDEFAULTFOLDDISPLAYTEXT = 2723;
	public const int SC_FOLDACTION_CONTRACT = 0;
	public const int SC_FOLDACTION_EXPAND = 1;
	public const int SC_FOLDACTION_TOGGLE = 2;
	public const int SCI_FOLDLINE = 2237;
	public const int SCI_FOLDCHILDREN = 2238;
	public const int SCI_EXPANDCHILDREN = 2239;
	public const int SCI_FOLDALL = 2662;
	public const int SCI_ENSUREVISIBLE = 2232;
	public const int SC_AUTOMATICFOLD_NONE = 0x0000;
	public const int SC_AUTOMATICFOLD_SHOW = 0x0001;
	public const int SC_AUTOMATICFOLD_CLICK = 0x0002;
	public const int SC_AUTOMATICFOLD_CHANGE = 0x0004;
	public const int SCI_SETAUTOMATICFOLD = 2663;
	public const int SCI_GETAUTOMATICFOLD = 2664;
	public const int SC_FOLDFLAG_NONE = 0x0000;
	public const int SC_FOLDFLAG_LINEBEFORE_EXPANDED = 0x0002;
	public const int SC_FOLDFLAG_LINEBEFORE_CONTRACTED = 0x0004;
	public const int SC_FOLDFLAG_LINEAFTER_EXPANDED = 0x0008;
	public const int SC_FOLDFLAG_LINEAFTER_CONTRACTED = 0x0010;
	public const int SC_FOLDFLAG_LEVELNUMBERS = 0x0040;
	public const int SC_FOLDFLAG_LINESTATE = 0x0080;
	public const int SCI_SETFOLDFLAGS = 2233;
	public const int SCI_ENSUREVISIBLEENFORCEPOLICY = 2234;
	public const int SCI_SETTABINDENTS = 2260;
	public const int SCI_GETTABINDENTS = 2261;
	public const int SCI_SETBACKSPACEUNINDENTS = 2262;
	public const int SCI_GETBACKSPACEUNINDENTS = 2263;
	public const int SC_TIME_FOREVER = 10000000;
	public const int SCI_SETMOUSEDWELLTIME = 2264;
	public const int SCI_GETMOUSEDWELLTIME = 2265;
	public const int SCI_WORDSTARTPOSITION = 2266;
	public const int SCI_WORDENDPOSITION = 2267;
	public const int SCI_ISRANGEWORD = 2691;
	public const int SC_IDLESTYLING_NONE = 0;
	public const int SC_IDLESTYLING_TOVISIBLE = 1;
	public const int SC_IDLESTYLING_AFTERVISIBLE = 2;
	public const int SC_IDLESTYLING_ALL = 3;
	public const int SCI_SETIDLESTYLING = 2692;
	public const int SCI_GETIDLESTYLING = 2693;
	public const int SC_WRAP_NONE = 0;
	public const int SC_WRAP_WORD = 1;
	public const int SC_WRAP_CHAR = 2;
	public const int SC_WRAP_WHITESPACE = 3;
	public const int SCI_SETWRAPMODE = 2268;
	public const int SCI_GETWRAPMODE = 2269;
	public const int SC_WRAPVISUALFLAG_NONE = 0x0000;
	public const int SC_WRAPVISUALFLAG_END = 0x0001;
	public const int SC_WRAPVISUALFLAG_START = 0x0002;
	public const int SC_WRAPVISUALFLAG_MARGIN = 0x0004;
	public const int SCI_SETWRAPVISUALFLAGS = 2460;
	public const int SCI_GETWRAPVISUALFLAGS = 2461;
	public const int SC_WRAPVISUALFLAGLOC_DEFAULT = 0x0000;
	public const int SC_WRAPVISUALFLAGLOC_END_BY_TEXT = 0x0001;
	public const int SC_WRAPVISUALFLAGLOC_START_BY_TEXT = 0x0002;
	public const int SCI_SETWRAPVISUALFLAGSLOCATION = 2462;
	public const int SCI_GETWRAPVISUALFLAGSLOCATION = 2463;
	public const int SCI_SETWRAPSTARTINDENT = 2464;
	public const int SCI_GETWRAPSTARTINDENT = 2465;
	public const int SC_WRAPINDENT_FIXED = 0;
	public const int SC_WRAPINDENT_SAME = 1;
	public const int SC_WRAPINDENT_INDENT = 2;
	public const int SC_WRAPINDENT_DEEPINDENT = 3;
	public const int SCI_SETWRAPINDENTMODE = 2472;
	public const int SCI_GETWRAPINDENTMODE = 2473;
	public const int SC_CACHE_NONE = 0;
	public const int SC_CACHE_CARET = 1;
	public const int SC_CACHE_PAGE = 2;
	public const int SC_CACHE_DOCUMENT = 3;
	public const int SCI_SETLAYOUTCACHE = 2272;
	public const int SCI_GETLAYOUTCACHE = 2273;
	public const int SCI_SETSCROLLWIDTH = 2274;
	public const int SCI_GETSCROLLWIDTH = 2275;
	public const int SCI_SETSCROLLWIDTHTRACKING = 2516;
	public const int SCI_GETSCROLLWIDTHTRACKING = 2517;
	public const int SCI_TEXTWIDTH = 2276;
	public const int SCI_SETENDATLASTLINE = 2277;
	public const int SCI_GETENDATLASTLINE = 2278;
	public const int SCI_TEXTHEIGHT = 2279;
	public const int SCI_SETVSCROLLBAR = 2280;
	public const int SCI_GETVSCROLLBAR = 2281;
	public const int SCI_APPENDTEXT = 2282;
	public const int SC_PHASES_ONE = 0;
	public const int SC_PHASES_TWO = 1;
	public const int SC_PHASES_MULTIPLE = 2;
	public const int SCI_GETPHASESDRAW = 2673;
	public const int SCI_SETPHASESDRAW = 2674;
	public const int SC_EFF_QUALITY_MASK = 0xF;
	public const int SC_EFF_QUALITY_DEFAULT = 0;
	public const int SC_EFF_QUALITY_NON_ANTIALIASED = 1;
	public const int SC_EFF_QUALITY_ANTIALIASED = 2;
	public const int SC_EFF_QUALITY_LCD_OPTIMIZED = 3;
	public const int SCI_SETFONTQUALITY = 2611;
	public const int SCI_GETFONTQUALITY = 2612;
	public const int SCI_SETFIRSTVISIBLELINE = 2613;
	public const int SC_MULTIPASTE_ONCE = 0;
	public const int SC_MULTIPASTE_EACH = 1;
	public const int SCI_SETMULTIPASTE = 2614;
	public const int SCI_GETMULTIPASTE = 2615;
	public const int SCI_GETTAG = 2616;
	public const int SCI_LINESJOIN = 2288;
	public const int SCI_LINESSPLIT = 2289;
	public const int SCI_SETFOLDMARGINCOLOUR = 2290;
	public const int SCI_SETFOLDMARGINHICOLOUR = 2291;
	public const int SC_ACCESSIBILITY_DISABLED = 0;
	public const int SC_ACCESSIBILITY_ENABLED = 1;
	public const int SCI_SETACCESSIBILITY = 2702;
	public const int SCI_GETACCESSIBILITY = 2703;
	public const int SCI_LINEDOWN = 2300;
	public const int SCI_LINEDOWNEXTEND = 2301;
	public const int SCI_LINEUP = 2302;
	public const int SCI_LINEUPEXTEND = 2303;
	public const int SCI_CHARLEFT = 2304;
	public const int SCI_CHARLEFTEXTEND = 2305;
	public const int SCI_CHARRIGHT = 2306;
	public const int SCI_CHARRIGHTEXTEND = 2307;
	public const int SCI_WORDLEFT = 2308;
	public const int SCI_WORDLEFTEXTEND = 2309;
	public const int SCI_WORDRIGHT = 2310;
	public const int SCI_WORDRIGHTEXTEND = 2311;
	public const int SCI_HOME = 2312;
	public const int SCI_HOMEEXTEND = 2313;
	public const int SCI_LINEEND = 2314;
	public const int SCI_LINEENDEXTEND = 2315;
	public const int SCI_DOCUMENTSTART = 2316;
	public const int SCI_DOCUMENTSTARTEXTEND = 2317;
	public const int SCI_DOCUMENTEND = 2318;
	public const int SCI_DOCUMENTENDEXTEND = 2319;
	public const int SCI_PAGEUP = 2320;
	public const int SCI_PAGEUPEXTEND = 2321;
	public const int SCI_PAGEDOWN = 2322;
	public const int SCI_PAGEDOWNEXTEND = 2323;
	public const int SCI_EDITTOGGLEOVERTYPE = 2324;
	public const int SCI_CANCEL = 2325;
	public const int SCI_DELETEBACK = 2326;
	public const int SCI_TAB = 2327;
	public const int SCI_BACKTAB = 2328;
	public const int SCI_NEWLINE = 2329;
	public const int SCI_FORMFEED = 2330;
	public const int SCI_VCHOME = 2331;
	public const int SCI_VCHOMEEXTEND = 2332;
	public const int SCI_ZOOMIN = 2333;
	public const int SCI_ZOOMOUT = 2334;
	public const int SCI_DELWORDLEFT = 2335;
	public const int SCI_DELWORDRIGHT = 2336;
	public const int SCI_DELWORDRIGHTEND = 2518;
	public const int SCI_LINECUT = 2337;
	public const int SCI_LINEDELETE = 2338;
	public const int SCI_LINETRANSPOSE = 2339;
	public const int SCI_LINEREVERSE = 2354;
	public const int SCI_LINEDUPLICATE = 2404;
	public const int SCI_LOWERCASE = 2340;
	public const int SCI_UPPERCASE = 2341;
	public const int SCI_LINESCROLLDOWN = 2342;
	public const int SCI_LINESCROLLUP = 2343;
	public const int SCI_DELETEBACKNOTLINE = 2344;
	public const int SCI_HOMEDISPLAY = 2345;
	public const int SCI_HOMEDISPLAYEXTEND = 2346;
	public const int SCI_LINEENDDISPLAY = 2347;
	public const int SCI_LINEENDDISPLAYEXTEND = 2348;
	public const int SCI_HOMEWRAP = 2349;
	public const int SCI_HOMEWRAPEXTEND = 2450;
	public const int SCI_LINEENDWRAP = 2451;
	public const int SCI_LINEENDWRAPEXTEND = 2452;
	public const int SCI_VCHOMEWRAP = 2453;
	public const int SCI_VCHOMEWRAPEXTEND = 2454;
	public const int SCI_LINECOPY = 2455;
	public const int SCI_MOVECARETINSIDEVIEW = 2401;
	public const int SCI_LINELENGTH = 2350;
	public const int SCI_BRACEHIGHLIGHT = 2351;
	public const int SCI_BRACEHIGHLIGHTINDICATOR = 2498;
	public const int SCI_BRACEBADLIGHT = 2352;
	public const int SCI_BRACEBADLIGHTINDICATOR = 2499;
	public const int SCI_BRACEMATCH = 2353;
	public const int SCI_BRACEMATCHNEXT = 2369;
	public const int SCI_GETVIEWEOL = 2355;
	public const int SCI_SETVIEWEOL = 2356;
	public const int SCI_GETDOCPOINTER = 2357;
	public const int SCI_SETDOCPOINTER = 2358;
	public const int SCI_SETMODEVENTMASK = 2359;
	public const int EDGE_NONE = 0;
	public const int EDGE_LINE = 1;
	public const int EDGE_BACKGROUND = 2;
	public const int EDGE_MULTILINE = 3;
	public const int SCI_GETEDGECOLUMN = 2360;
	public const int SCI_SETEDGECOLUMN = 2361;
	public const int SCI_GETEDGEMODE = 2362;
	public const int SCI_SETEDGEMODE = 2363;
	public const int SCI_GETEDGECOLOUR = 2364;
	public const int SCI_SETEDGECOLOUR = 2365;
	public const int SCI_MULTIEDGEADDLINE = 2694;
	public const int SCI_MULTIEDGECLEARALL = 2695;
	public const int SCI_GETMULTIEDGECOLUMN = 2749;
	public const int SCI_SEARCHANCHOR = 2366;
	public const int SCI_SEARCHNEXT = 2367;
	public const int SCI_SEARCHPREV = 2368;
	public const int SCI_LINESONSCREEN = 2370;
	public const int SC_POPUP_NEVER = 0;
	public const int SC_POPUP_ALL = 1;
	public const int SC_POPUP_TEXT = 2;
	public const int SCI_USEPOPUP = 2371;
	public const int SCI_SELECTIONISRECTANGLE = 2372;
	public const int SCI_SETZOOM = 2373;
	public const int SCI_GETZOOM = 2374;
	public const int SC_DOCUMENTOPTION_DEFAULT = 0;
	public const int SC_DOCUMENTOPTION_STYLES_NONE = 0x1;
	public const int SC_DOCUMENTOPTION_TEXT_LARGE = 0x100;
	public const int SCI_CREATEDOCUMENT = 2375;
	public const int SCI_ADDREFDOCUMENT = 2376;
	public const int SCI_RELEASEDOCUMENT = 2377;
	public const int SCI_GETDOCUMENTOPTIONS = 2379;
	public const int SCI_GETMODEVENTMASK = 2378;
	public const int SCI_SETCOMMANDEVENTS = 2717;
	public const int SCI_GETCOMMANDEVENTS = 2718;
	public const int SCI_SETFOCUS = 2380;
	public const int SCI_GETFOCUS = 2381;
	public const int SC_STATUS_OK = 0;
	public const int SC_STATUS_FAILURE = 1;
	public const int SC_STATUS_BADALLOC = 2;
	public const int SC_STATUS_WARN_START = 1000;
	public const int SC_STATUS_WARN_REGEX = 1001;
	public const int SCI_SETSTATUS = 2382;
	public const int SCI_GETSTATUS = 2383;
	public const int SCI_SETMOUSEDOWNCAPTURES = 2384;
	public const int SCI_GETMOUSEDOWNCAPTURES = 2385;
	public const int SCI_SETMOUSEWHEELCAPTURES = 2696;
	public const int SCI_GETMOUSEWHEELCAPTURES = 2697;
	public const int SC_CURSORNORMAL = -1;
	public const int SC_CURSORARROW = 2;
	public const int SC_CURSORWAIT = 4;
	public const int SC_CURSORREVERSEARROW = 7;
	public const int SCI_SETCURSOR = 2386;
	public const int SCI_GETCURSOR = 2387;
	public const int SCI_SETCONTROLCHARSYMBOL = 2388;
	public const int SCI_GETCONTROLCHARSYMBOL = 2389;
	public const int SCI_WORDPARTLEFT = 2390;
	public const int SCI_WORDPARTLEFTEXTEND = 2391;
	public const int SCI_WORDPARTRIGHT = 2392;
	public const int SCI_WORDPARTRIGHTEXTEND = 2393;
	public const int VISIBLE_SLOP = 0x01;
	public const int VISIBLE_STRICT = 0x04;
	public const int SCI_SETVISIBLEPOLICY = 2394;
	public const int SCI_DELLINELEFT = 2395;
	public const int SCI_DELLINERIGHT = 2396;
	public const int SCI_SETXOFFSET = 2397;
	public const int SCI_GETXOFFSET = 2398;
	public const int SCI_CHOOSECARETX = 2399;
	public const int SCI_GRABFOCUS = 2400;
	public const int CARET_SLOP = 0x01;
	public const int CARET_STRICT = 0x04;
	public const int CARET_JUMPS = 0x10;
	public const int CARET_EVEN = 0x08;
	public const int SCI_SETXCARETPOLICY = 2402;
	public const int SCI_SETYCARETPOLICY = 2403;
	public const int SCI_SETPRINTWRAPMODE = 2406;
	public const int SCI_GETPRINTWRAPMODE = 2407;
	public const int SCI_SETHOTSPOTACTIVEFORE = 2410;
	public const int SCI_GETHOTSPOTACTIVEFORE = 2494;
	public const int SCI_SETHOTSPOTACTIVEBACK = 2411;
	public const int SCI_GETHOTSPOTACTIVEBACK = 2495;
	public const int SCI_SETHOTSPOTACTIVEUNDERLINE = 2412;
	public const int SCI_GETHOTSPOTACTIVEUNDERLINE = 2496;
	public const int SCI_SETHOTSPOTSINGLELINE = 2421;
	public const int SCI_GETHOTSPOTSINGLELINE = 2497;
	public const int SCI_PARADOWN = 2413;
	public const int SCI_PARADOWNEXTEND = 2414;
	public const int SCI_PARAUP = 2415;
	public const int SCI_PARAUPEXTEND = 2416;
	public const int SCI_POSITIONBEFORE = 2417;
	public const int SCI_POSITIONAFTER = 2418;
	public const int SCI_POSITIONRELATIVE = 2670;
	public const int SCI_POSITIONRELATIVECODEUNITS = 2716;
	public const int SCI_COPYRANGE = 2419;
	public const int SCI_COPYTEXT = 2420;
	public const int SC_SEL_STREAM = 0;
	public const int SC_SEL_RECTANGLE = 1;
	public const int SC_SEL_LINES = 2;
	public const int SC_SEL_THIN = 3;
	public const int SCI_SETSELECTIONMODE = 2422;
	public const int SCI_GETSELECTIONMODE = 2423;
	public const int SCI_GETMOVEEXTENDSSELECTION = 2706;
	public const int SCI_GETLINESELSTARTPOSITION = 2424;
	public const int SCI_GETLINESELENDPOSITION = 2425;
	public const int SCI_LINEDOWNRECTEXTEND = 2426;
	public const int SCI_LINEUPRECTEXTEND = 2427;
	public const int SCI_CHARLEFTRECTEXTEND = 2428;
	public const int SCI_CHARRIGHTRECTEXTEND = 2429;
	public const int SCI_HOMERECTEXTEND = 2430;
	public const int SCI_VCHOMERECTEXTEND = 2431;
	public const int SCI_LINEENDRECTEXTEND = 2432;
	public const int SCI_PAGEUPRECTEXTEND = 2433;
	public const int SCI_PAGEDOWNRECTEXTEND = 2434;
	public const int SCI_STUTTEREDPAGEUP = 2435;
	public const int SCI_STUTTEREDPAGEUPEXTEND = 2436;
	public const int SCI_STUTTEREDPAGEDOWN = 2437;
	public const int SCI_STUTTEREDPAGEDOWNEXTEND = 2438;
	public const int SCI_WORDLEFTEND = 2439;
	public const int SCI_WORDLEFTENDEXTEND = 2440;
	public const int SCI_WORDRIGHTEND = 2441;
	public const int SCI_WORDRIGHTENDEXTEND = 2442;
	public const int SCI_SETWHITESPACECHARS = 2443;
	public const int SCI_GETWHITESPACECHARS = 2647;
	public const int SCI_SETPUNCTUATIONCHARS = 2648;
	public const int SCI_GETPUNCTUATIONCHARS = 2649;
	public const int SCI_SETCHARSDEFAULT = 2444;
	public const int SCI_AUTOCGETCURRENT = 2445;
	public const int SCI_AUTOCGETCURRENTTEXT = 2610;
	public const int SC_CASEINSENSITIVEBEHAVIOUR_RESPECTCASE = 0;
	public const int SC_CASEINSENSITIVEBEHAVIOUR_IGNORECASE = 1;
	public const int SCI_AUTOCSETCASEINSENSITIVEBEHAVIOUR = 2634;
	public const int SCI_AUTOCGETCASEINSENSITIVEBEHAVIOUR = 2635;
	public const int SC_MULTIAUTOC_ONCE = 0;
	public const int SC_MULTIAUTOC_EACH = 1;
	public const int SCI_AUTOCSETMULTI = 2636;
	public const int SCI_AUTOCGETMULTI = 2637;
	public const int SC_ORDER_PRESORTED = 0;
	public const int SC_ORDER_PERFORMSORT = 1;
	public const int SC_ORDER_CUSTOM = 2;
	public const int SCI_AUTOCSETORDER = 2660;
	public const int SCI_AUTOCGETORDER = 2661;
	public const int SCI_ALLOCATE = 2446;
	public const int SCI_TARGETASUTF8 = 2447;
	public const int SCI_SETLENGTHFORENCODE = 2448;
	public const int SCI_ENCODEDFROMUTF8 = 2449;
	public const int SCI_FINDCOLUMN = 2456;
	public const int SCI_GETCARETSTICKY = 2457;
	public const int SCI_SETCARETSTICKY = 2458;
	public const int SC_CARETSTICKY_OFF = 0;
	public const int SC_CARETSTICKY_ON = 1;
	public const int SC_CARETSTICKY_WHITESPACE = 2;
	public const int SCI_TOGGLECARETSTICKY = 2459;
	public const int SCI_SETPASTECONVERTENDINGS = 2467;
	public const int SCI_GETPASTECONVERTENDINGS = 2468;
	public const int SCI_REPLACERECTANGULAR = 2771;
	public const int SCI_SELECTIONDUPLICATE = 2469;
	public const int SC_ALPHA_TRANSPARENT = 0;
	public const int SC_ALPHA_OPAQUE = 255;
	public const int SC_ALPHA_NOALPHA = 256;
	public const int SCI_SETCARETLINEBACKALPHA = 2470;
	public const int SCI_GETCARETLINEBACKALPHA = 2471;
	public const int CARETSTYLE_INVISIBLE = 0;
	public const int CARETSTYLE_LINE = 1;
	public const int CARETSTYLE_BLOCK = 2;
	public const int CARETSTYLE_OVERSTRIKE_BAR = 0;
	public const int CARETSTYLE_OVERSTRIKE_BLOCK = 16;
	public const int CARETSTYLE_CURSES = 0x20;
	public const int CARETSTYLE_INS_MASK = 0xF;
	public const int SCI_SETCARETSTYLE = 2512;
	public const int SCI_GETCARETSTYLE = 2513;
	public const int SCI_SETINDICATORCURRENT = 2500;
	public const int SCI_GETINDICATORCURRENT = 2501;
	public const int SCI_SETINDICATORVALUE = 2502;
	public const int SCI_GETINDICATORVALUE = 2503;
	public const int SCI_INDICATORFILLRANGE = 2504;
	public const int SCI_INDICATORCLEARRANGE = 2505;
	public const int SCI_INDICATORALLONFOR = 2506;
	public const int SCI_INDICATORVALUEAT = 2507;
	public const int SCI_INDICATORSTART = 2508;
	public const int SCI_INDICATOREND = 2509;
	public const int SCI_SETPOSITIONCACHE = 2514;
	public const int SCI_GETPOSITIONCACHE = 2515;
	public const int SCI_COPYALLOWLINE = 2519;
	public const int SCI_GETCHARACTERPOINTER = 2520;
	public const int SCI_GETRANGEPOINTER = 2643;
	public const int SCI_GETGAPPOSITION = 2644;
	public const int SCI_INDICSETALPHA = 2523;
	public const int SCI_INDICGETALPHA = 2524;
	public const int SCI_INDICSETOUTLINEALPHA = 2558;
	public const int SCI_INDICGETOUTLINEALPHA = 2559;
	public const int SCI_SETEXTRAASCENT = 2525;
	public const int SCI_GETEXTRAASCENT = 2526;
	public const int SCI_SETEXTRADESCENT = 2527;
	public const int SCI_GETEXTRADESCENT = 2528;
	public const int SCI_MARKERSYMBOLDEFINED = 2529;
	public const int SCI_MARGINSETTEXT = 2530;
	public const int SCI_MARGINGETTEXT = 2531;
	public const int SCI_MARGINSETSTYLE = 2532;
	public const int SCI_MARGINGETSTYLE = 2533;
	public const int SCI_MARGINSETSTYLES = 2534;
	public const int SCI_MARGINGETSTYLES = 2535;
	public const int SCI_MARGINTEXTCLEARALL = 2536;
	public const int SCI_MARGINSETSTYLEOFFSET = 2537;
	public const int SCI_MARGINGETSTYLEOFFSET = 2538;
	public const int SC_MARGINOPTION_NONE = 0;
	public const int SC_MARGINOPTION_SUBLINESELECT = 1;
	public const int SCI_SETMARGINOPTIONS = 2539;
	public const int SCI_GETMARGINOPTIONS = 2557;
	public const int SCI_ANNOTATIONSETTEXT = 2540;
	public const int SCI_ANNOTATIONGETTEXT = 2541;
	public const int SCI_ANNOTATIONSETSTYLE = 2542;
	public const int SCI_ANNOTATIONGETSTYLE = 2543;
	public const int SCI_ANNOTATIONSETSTYLES = 2544;
	public const int SCI_ANNOTATIONGETSTYLES = 2545;
	public const int SCI_ANNOTATIONGETLINES = 2546;
	public const int SCI_ANNOTATIONCLEARALL = 2547;
	public enum AnnotationsVisible {
		ANNOTATION_HIDDEN = 0,
		ANNOTATION_STANDARD = 1,
		ANNOTATION_BOXED = 2,
		ANNOTATION_INDENTED = 3,
	}
	public const int SCI_ANNOTATIONSETVISIBLE = 2548;
	public const int SCI_ANNOTATIONGETVISIBLE = 2549;
	public const int SCI_ANNOTATIONSETSTYLEOFFSET = 2550;
	public const int SCI_ANNOTATIONGETSTYLEOFFSET = 2551;
	public const int SCI_RELEASEALLEXTENDEDSTYLES = 2552;
	public const int SCI_ALLOCATEEXTENDEDSTYLES = 2553;
	public const int UNDO_MAY_COALESCE = 1;
	public const int SCI_ADDUNDOACTION = 2560;
	public const int SCI_CHARPOSITIONFROMPOINT = 2561;
	public const int SCI_CHARPOSITIONFROMPOINTCLOSE = 2562;
	public const int SCI_SETMOUSESELECTIONRECTANGULARSWITCH = 2668;
	public const int SCI_GETMOUSESELECTIONRECTANGULARSWITCH = 2669;
	public const int SCI_SETMULTIPLESELECTION = 2563;
	public const int SCI_GETMULTIPLESELECTION = 2564;
	public const int SCI_SETADDITIONALSELECTIONTYPING = 2565;
	public const int SCI_GETADDITIONALSELECTIONTYPING = 2566;
	public const int SCI_SETADDITIONALCARETSBLINK = 2567;
	public const int SCI_GETADDITIONALCARETSBLINK = 2568;
	public const int SCI_SETADDITIONALCARETSVISIBLE = 2608;
	public const int SCI_GETADDITIONALCARETSVISIBLE = 2609;
	public const int SCI_GETSELECTIONS = 2570;
	public const int SCI_GETSELECTIONEMPTY = 2650;
	public const int SCI_CLEARSELECTIONS = 2571;
	public const int SCI_SETSELECTION = 2572;
	public const int SCI_ADDSELECTION = 2573;
	public const int SCI_DROPSELECTIONN = 2671;
	public const int SCI_SETMAINSELECTION = 2574;
	public const int SCI_GETMAINSELECTION = 2575;
	public const int SCI_SETSELECTIONNCARET = 2576;
	public const int SCI_GETSELECTIONNCARET = 2577;
	public const int SCI_SETSELECTIONNANCHOR = 2578;
	public const int SCI_GETSELECTIONNANCHOR = 2579;
	public const int SCI_SETSELECTIONNCARETVIRTUALSPACE = 2580;
	public const int SCI_GETSELECTIONNCARETVIRTUALSPACE = 2581;
	public const int SCI_SETSELECTIONNANCHORVIRTUALSPACE = 2582;
	public const int SCI_GETSELECTIONNANCHORVIRTUALSPACE = 2583;
	public const int SCI_SETSELECTIONNSTART = 2584;
	public const int SCI_GETSELECTIONNSTART = 2585;
	public const int SCI_SETSELECTIONNEND = 2586;
	public const int SCI_GETSELECTIONNEND = 2587;
	public const int SCI_SETRECTANGULARSELECTIONCARET = 2588;
	public const int SCI_GETRECTANGULARSELECTIONCARET = 2589;
	public const int SCI_SETRECTANGULARSELECTIONANCHOR = 2590;
	public const int SCI_GETRECTANGULARSELECTIONANCHOR = 2591;
	public const int SCI_SETRECTANGULARSELECTIONCARETVIRTUALSPACE = 2592;
	public const int SCI_GETRECTANGULARSELECTIONCARETVIRTUALSPACE = 2593;
	public const int SCI_SETRECTANGULARSELECTIONANCHORVIRTUALSPACE = 2594;
	public const int SCI_GETRECTANGULARSELECTIONANCHORVIRTUALSPACE = 2595;
	public const int SCVS_NONE = 0;
	public const int SCVS_RECTANGULARSELECTION = 1;
	public const int SCVS_USERACCESSIBLE = 2;
	public const int SCVS_NOWRAPLINESTART = 4;
	public const int SCI_SETVIRTUALSPACEOPTIONS = 2596;
	public const int SCI_GETVIRTUALSPACEOPTIONS = 2597;
	public const int SCI_SETRECTANGULARSELECTIONMODIFIER = 2598;
	public const int SCI_GETRECTANGULARSELECTIONMODIFIER = 2599;
	public const int SCI_SETADDITIONALSELFORE = 2600;
	public const int SCI_SETADDITIONALSELBACK = 2601;
	public const int SCI_SETADDITIONALSELALPHA = 2602;
	public const int SCI_GETADDITIONALSELALPHA = 2603;
	public const int SCI_SETADDITIONALCARETFORE = 2604;
	public const int SCI_GETADDITIONALCARETFORE = 2605;
	public const int SCI_ROTATESELECTION = 2606;
	public const int SCI_SWAPMAINANCHORCARET = 2607;
	public const int SCI_MULTIPLESELECTADDNEXT = 2688;
	public const int SCI_MULTIPLESELECTADDEACH = 2689;
	public const int SCI_CHANGELEXERSTATE = 2617;
	public const int SCI_CONTRACTEDFOLDNEXT = 2618;
	public const int SCI_VERTICALCENTRECARET = 2619;
	public const int SCI_MOVESELECTEDLINESUP = 2620;
	public const int SCI_MOVESELECTEDLINESDOWN = 2621;
	public const int SCI_SETIDENTIFIER = 2622;
	public const int SCI_GETIDENTIFIER = 2623;
	public const int SCI_RGBAIMAGESETWIDTH = 2624;
	public const int SCI_RGBAIMAGESETHEIGHT = 2625;
	public const int SCI_RGBAIMAGESETSCALE = 2651;
	public const int SCI_MARKERDEFINERGBAIMAGE = 2626;
	public const int SCI_REGISTERRGBAIMAGE = 2627;
	public const int SCI_SCROLLTOSTART = 2628;
	public const int SCI_SCROLLTOEND = 2629;
	public const int SC_TECHNOLOGY_DEFAULT = 0;
	public const int SC_TECHNOLOGY_DIRECTWRITE = 1;
	public const int SC_TECHNOLOGY_DIRECTWRITERETAIN = 2;
	public const int SC_TECHNOLOGY_DIRECTWRITEDC = 3;
	public const int SCI_SETTECHNOLOGY = 2630;
	public const int SCI_GETTECHNOLOGY = 2631;
	public const int SCI_CREATELOADER = 2632;
	public const int SCI_FINDINDICATORSHOW = 2640;
	public const int SCI_FINDINDICATORFLASH = 2641;
	public const int SCI_FINDINDICATORHIDE = 2642;
	public const int SCI_VCHOMEDISPLAY = 2652;
	public const int SCI_VCHOMEDISPLAYEXTEND = 2653;
	public const int SCI_GETCARETLINEVISIBLEALWAYS = 2654;
	public const int SCI_SETCARETLINEVISIBLEALWAYS = 2655;
	public const int SC_LINE_END_TYPE_DEFAULT = 0;
	public const int SC_LINE_END_TYPE_UNICODE = 1;
	public const int SCI_SETLINEENDTYPESALLOWED = 2656;
	public const int SCI_GETLINEENDTYPESALLOWED = 2657;
	public const int SCI_GETLINEENDTYPESACTIVE = 2658;
	public const int SCI_SETREPRESENTATION = 2665;
	public const int SCI_GETREPRESENTATION = 2666;
	public const int SCI_CLEARREPRESENTATION = 2667;
	public const int SCI_CLEARALLREPRESENTATIONS = 2770;
	public const int SC_REPRESENTATION_PLAIN = 0;
	public const int SC_REPRESENTATION_BLOB = 1;
	public const int SC_REPRESENTATION_COLOUR = 0x10;
	public const int SCI_SETREPRESENTATIONAPPEARANCE = 2766;
	public const int SCI_GETREPRESENTATIONAPPEARANCE = 2767;
	public const int SCI_SETREPRESENTATIONCOLOUR = 2768;
	public const int SCI_GETREPRESENTATIONCOLOUR = 2769;
	public const int SCI_EOLANNOTATIONSETTEXT = 2740;
	public const int SCI_EOLANNOTATIONGETTEXT = 2741;
	public const int SCI_EOLANNOTATIONSETSTYLE = 2742;
	public const int SCI_EOLANNOTATIONGETSTYLE = 2743;
	public const int SCI_EOLANNOTATIONCLEARALL = 2744;
	public const int EOLANNOTATION_HIDDEN = 0x0;
	public const int EOLANNOTATION_STANDARD = 0x1;
	public const int EOLANNOTATION_BOXED = 0x2;
	public const int EOLANNOTATION_STADIUM = 0x100;
	public const int EOLANNOTATION_FLAT_CIRCLE = 0x101;
	public const int EOLANNOTATION_ANGLE_CIRCLE = 0x102;
	public const int EOLANNOTATION_CIRCLE_FLAT = 0x110;
	public const int EOLANNOTATION_FLATS = 0x111;
	public const int EOLANNOTATION_ANGLE_FLAT = 0x112;
	public const int EOLANNOTATION_CIRCLE_ANGLE = 0x120;
	public const int EOLANNOTATION_FLAT_ANGLE = 0x121;
	public const int EOLANNOTATION_ANGLES = 0x122;
	public const int SCI_EOLANNOTATIONSETVISIBLE = 2745;
	public const int SCI_EOLANNOTATIONGETVISIBLE = 2746;
	public const int SCI_EOLANNOTATIONSETSTYLEOFFSET = 2747;
	public const int SCI_EOLANNOTATIONGETSTYLEOFFSET = 2748;
	public const int SC_SUPPORTS_LINE_DRAWS_FINAL = 0;
	public const int SC_SUPPORTS_PIXEL_DIVISIONS = 1;
	public const int SC_SUPPORTS_FRACTIONAL_STROKE_WIDTH = 2;
	public const int SC_SUPPORTS_TRANSLUCENT_STROKE = 3;
	public const int SC_SUPPORTS_PIXEL_MODIFICATION = 4;
	public const int SCI_SUPPORTSFEATURE = 2750;
	public const int SC_LINECHARACTERINDEX_NONE = 0;
	public const int SC_LINECHARACTERINDEX_UTF32 = 1;
	public const int SC_LINECHARACTERINDEX_UTF16 = 2;
	public const int SCI_GETLINECHARACTERINDEX = 2710;
	public const int SCI_ALLOCATELINECHARACTERINDEX = 2711;
	public const int SCI_RELEASELINECHARACTERINDEX = 2712;
	public const int SCI_LINEFROMINDEXPOSITION = 2713;
	public const int SCI_INDEXPOSITIONFROMLINE = 2714;
	public const int SCI_STARTRECORD = 3001;
	public const int SCI_STOPRECORD = 3002;
	public const int SCI_SETILEXER = 4033;
	public const int SCI_GETLEXER = 4002;
	public const int SCI_COLOURISE = 4003;
	public const int SCI_SETPROPERTY = 4004;
	public const int KEYWORDSET_MAX = 8;
	public const int SCI_SETKEYWORDS = 4005;
	public const int SCI_GETPROPERTY = 4008;
	public const int SCI_GETPROPERTYEXPANDED = 4009;
	public const int SCI_GETPROPERTYINT = 4010;
	public const int SCI_GETLEXERLANGUAGE = 4012;
	public const int SCI_PRIVATELEXERCALL = 4013;
	public const int SCI_PROPERTYNAMES = 4014;
	public const int SC_TYPE_BOOLEAN = 0;
	public const int SC_TYPE_INTEGER = 1;
	public const int SC_TYPE_STRING = 2;
	public const int SCI_PROPERTYTYPE = 4015;
	public const int SCI_DESCRIBEPROPERTY = 4016;
	public const int SCI_DESCRIBEKEYWORDSETS = 4017;
	public const int SCI_GETLINEENDTYPESSUPPORTED = 4018;
	public const int SCI_ALLOCATESUBSTYLES = 4020;
	public const int SCI_GETSUBSTYLESSTART = 4021;
	public const int SCI_GETSUBSTYLESLENGTH = 4022;
	public const int SCI_GETSTYLEFROMSUBSTYLE = 4027;
	public const int SCI_GETPRIMARYSTYLEFROMSTYLE = 4028;
	public const int SCI_FREESUBSTYLES = 4023;
	public const int SCI_SETIDENTIFIERS = 4024;
	public const int SCI_DISTANCETOSECONDARYSTYLES = 4025;
	public const int SCI_GETSUBSTYLEBASES = 4026;
	public const int SCI_GETNAMEDSTYLES = 4029;
	public const int SCI_NAMEOFSTYLE = 4030;
	public const int SCI_TAGSOFSTYLE = 4031;
	public const int SCI_DESCRIPTIONOFSTYLE = 4032;
	[Flags]
	public enum MOD {
		SC_MOD_INSERTTEXT = 0x1,
		SC_MOD_DELETETEXT = 0x2,
		SC_MOD_CHANGESTYLE = 0x4,
		SC_MOD_CHANGEFOLD = 0x8,
		SC_PERFORMED_USER = 0x10,
		SC_PERFORMED_UNDO = 0x20,
		SC_PERFORMED_REDO = 0x40,
		SC_MULTISTEPUNDOREDO = 0x80,
		SC_LASTSTEPINUNDOREDO = 0x100,
		SC_MOD_CHANGEMARKER = 0x200,
		SC_MOD_BEFOREINSERT = 0x400,
		SC_MOD_BEFOREDELETE = 0x800,
		SC_MULTILINEUNDOREDO = 0x1000,
		SC_STARTACTION = 0x2000,
		SC_MOD_CHANGEINDICATOR = 0x4000,
		SC_MOD_CHANGELINESTATE = 0x8000,
		SC_MOD_CHANGEMARGIN = 0x10000,
		SC_MOD_CHANGEANNOTATION = 0x20000,
		SC_MOD_CONTAINER = 0x40000,
		SC_MOD_LEXERSTATE = 0x80000,
		SC_MOD_INSERTCHECK = 0x100000,
		SC_MOD_CHANGETABSTOPS = 0x200000,
		SC_MOD_CHANGEEOLANNOTATION = 0x400000,
		SC_MODEVENTMASKALL = 0x7FFFFF,
	}
	[Flags]
	public enum UPDATE {
		SC_UPDATE_CONTENT=1,
		SC_UPDATE_SELECTION=2,
		SC_UPDATE_V_SCROLL=4,
		SC_UPDATE_H_SCROLL=8,
	}
	
	public const int SCEN_CHANGE = 768;
	public const int SCEN_SETFOCUS = 512;
	public const int SCEN_KILLFOCUS = 256;
	public const int SCK_DOWN = 300;
	public const int SCK_UP = 301;
	public const int SCK_LEFT = 302;
	public const int SCK_RIGHT = 303;
	public const int SCK_HOME = 304;
	public const int SCK_END = 305;
	public const int SCK_PRIOR = 306;
	public const int SCK_NEXT = 307;
	public const int SCK_DELETE = 308;
	public const int SCK_INSERT = 309;
	public const int SCK_ESCAPE = 7;
	public const int SCK_BACK = 8;
	public const int SCK_TAB = 9;
	public const int SCK_RETURN = 13;
	public const int SCK_ADD = 310;
	public const int SCK_SUBTRACT = 311;
	public const int SCK_DIVIDE = 312;
	public const int SCK_WIN = 313;
	public const int SCK_RWIN = 314;
	public const int SCK_MENU = 315;
	public const int SCMOD_NORM = 0;
	public const int SCMOD_SHIFT = 1;
	public const int SCMOD_CTRL = 2;
	public const int SCMOD_ALT = 4;
	public const int SCMOD_SUPER = 8;
	public const int SCMOD_META = 16;
	public const int SC_AC_FILLUP = 1;
	public const int SC_AC_DOUBLECLICK = 2;
	public const int SC_AC_TAB = 3;
	public const int SC_AC_NEWLINE = 4;
	public const int SC_AC_COMMAND = 5;
	public enum NOTIF {
		SCN_STYLENEEDED = 2000,
		SCN_CHARADDED = 2001,
		SCN_SAVEPOINTREACHED = 2002,
		SCN_SAVEPOINTLEFT = 2003,
		SCN_MODIFYATTEMPTRO = 2004,
		SCN_KEY = 2005,
		SCN_DOUBLECLICK = 2006,
		SCN_UPDATEUI = 2007,
		SCN_MODIFIED = 2008,
		SCN_MACRORECORD = 2009,
		SCN_MARGINCLICK = 2010,
		SCN_NEEDSHOWN = 2011,
		SCN_PAINTED = 2013,
		SCN_USERLISTSELECTION = 2014,
		SCN_URIDROPPED = 2015,
		SCN_DWELLSTART = 2016,
		SCN_DWELLEND = 2017,
		SCN_ZOOM = 2018,
		SCN_HOTSPOTCLICK = 2019,
		SCN_HOTSPOTDOUBLECLICK = 2020,
		SCN_CALLTIPCLICK = 2021,
		SCN_AUTOCSELECTION = 2022,
		SCN_INDICATORCLICK = 2023,
		SCN_INDICATORRELEASE = 2024,
		SCN_AUTOCCANCELLED = 2025,
		SCN_AUTOCCHARDELETED = 2026,
		SCN_HOTSPOTRELEASECLICK = 2027,
		SCN_FOCUSIN = 2028,
		SCN_FOCUSOUT = 2029,
		SCN_AUTOCCOMPLETED = 2030,
		SCN_MARGINRIGHTCLICK = 2031,
		SCN_AUTOCSELECTIONCHANGE = 2032,
	}
	public const int SC_BIDIRECTIONAL_DISABLED = 0;
	public const int SC_BIDIRECTIONAL_L2R = 1;
	public const int SC_BIDIRECTIONAL_R2L = 2;
	public const int SCI_GETBIDIRECTIONAL = 2708;
	public const int SCI_SETBIDIRECTIONAL = 2709;

	public struct Sci_CharacterRange {
		public int cpMin;
		public int cpMax;
	}

	public struct Sci_TextRange {
		public int cpMin;
		public int cpMax;
		public byte* lpstrText;
	}

	public struct Sci_TextToFind {
		public int cpMin;
		public int cpMax;
		public byte* lpstrText;
		public Sci_CharacterRange chrgText;
	}

	public struct Sci_Rectangle {
		public int left;
		public int top;
		public int right;
		public int bottom;
	}

	public struct Sci_RangeToFormat {
		public IntPtr hdc;
		public IntPtr hdcTarget;
		public Sci_Rectangle rc;
		public Sci_Rectangle rcPage;
		public Sci_CharacterRange chrg;
	}

	public struct Sci_NotifyHeader {
		public wnd hwndFrom;
		public nint idFrom;
		public NOTIF code;
	}

	public struct SCNotification {
#pragma warning disable 649 //field never assigned
		public Sci_NotifyHeader nmhdr;
		/// <summary>Returns <c>nmhdr.code</c>.</summary>
		public NOTIF code => nmhdr.code;
		nint _position;
		/// <summary>Raw UTF-8 position.</summary>
		public int position => (int)_position;
		public int ch;
		public int modifiers;
		public MOD modificationType;
		public byte* textUTF8;
		nint _length;
		public int length => (int)_length;
		nint _linesAdded;
		public int linesAdded => (int)_linesAdded;
		public int message;
		public nint wParam;
		public nint lParam;
		nint _line;
		public int line => (int)_line;
		public int foldLevelNow;
		public int foldLevelPrev;
		public int margin;
		public int listType;
		public int x;
		public int y;
		public int token;
		nint _annotationLinesAdded;
		public int annotationLinesAdded => (int)_annotationLinesAdded;
		public UPDATE updated;
		public int listCompletionMethod;
#pragma warning restore 649 //field never assigned

		/// <summary>
		/// Returns position, UTF-8. If SCN_MODIFIED(SC_MOD_INSERTTEXT|SC_MOD_BEFOREINSERT|SC_MOD_INSERTCHECK), adds length, because position then is old position.
		/// </summary>
		public int FinalPosition {
			get {
				int r = position;
				if (length > 0 && nmhdr.code == NOTIF.SCN_MODIFIED
					&& modificationType.HasAny(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_BEFOREINSERT | MOD.SC_MOD_INSERTCHECK)
					) r += length;
				return r;
			}
		}

		/// <summary>
		/// Converts textUTF8 to C# string.
		/// Returns null if textUTF8 is null.
		/// Don't call this property multiple times for the same notification. Store the return value in a variable and use it.
		/// </summary>
		public string Text {
			get {
				if (textUTF8 == null) return null;
				if (textUTF8[0] == 0) return "";
				return new string((sbyte*)textUTF8, 0, length, Encoding.UTF8);
			}
		}
	}

	//from Scintilla.h

	public enum LexCppStyles {
		SCE_C_DEFAULT = 0,
		SCE_C_COMMENT = 1,
		SCE_C_COMMENTLINE = 2,
		SCE_C_COMMENTDOC = 3,
		SCE_C_NUMBER = 4,
		SCE_C_WORD = 5,
		SCE_C_STRING = 6,
		SCE_C_CHARACTER = 7,
		SCE_C_UUID = 8,
		SCE_C_PREPROCESSOR = 9,
		SCE_C_OPERATOR = 10,
		SCE_C_IDENTIFIER = 11,
		SCE_C_STRINGEOL = 12,
		SCE_C_VERBATIM = 13,
		SCE_C_REGEX = 14,
		SCE_C_COMMENTLINEDOC = 15,
		SCE_C_WORD2 = 16,
		SCE_C_COMMENTDOCKEYWORD = 17,
		SCE_C_COMMENTDOCKEYWORDERROR = 18,
		SCE_C_GLOBALCLASS = 19,
		SCE_C_STRINGRAW = 20,
		SCE_C_TRIPLEVERBATIM = 21,
		SCE_C_HASHQUOTEDSTRING = 22,
		SCE_C_PREPROCESSORCOMMENT = 23,
		SCE_C_PREPROCESSORCOMMENTDOC = 24,
		SCE_C_USERLITERAL = 25,
		SCE_C_TASKMARKER = 26,
		SCE_C_ESCAPESEQUENCE = 27,
	}
}
