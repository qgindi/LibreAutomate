using System.Text.Json.Serialization;

/// <summary>
/// Program settings.
/// folders.ThisAppDocuments + @".settings\Settings.json"
/// </summary>
record AppSettings : JSettings {
	//This is loaded at startup and therefore must be fast.
	//	NOTE: Don't use types that would cause to load UI dlls (WPF etc). Eg when it is a nested type and its parent class is a WPF etc control.
	//	Speed tested with .NET 5: first time 40-60 ms. Mostly to load/jit/etc dlls used in JSON deserialization, which then is fast regardless of data size.
	//	CONSIDER: Jit_ something in other thread. But it isn't good when runs at PC startup.
	
	public static AppSettings Load() => Load<AppSettings>(DirBS + "Settings.json");
	
#if IDE_LA
	public static readonly string DirBS = folders.ThisAppDocuments + @".settings_\";
#else
	public static readonly string DirBS = folders.ThisAppDocuments + @".settings\";
#endif
	
	public bool runHidden;
	public string user, workspace;
	public string[] recentWS;
	public bool checkForUpdates;
	public int checkForUpdatesDay;
	
	//When need a nested type, use record struct. Everything works well; can add/remove members like in main type.
	//	If using default field values, need ctor eg `hotkeys_t()` (else error) and `=new()`.
	//	Note: .NET always creates new object, even if default object created, even if record class. Avoid custom ctors etc.
	//	Can use record class too, but then usually creates new object 2 times: 1. explicit new(); 2. when deserializing.
	//	Tuple does not work well. New members are null. Also item names in file are like "Item1".
	
	//Options -> Hotkeys
	public record struct hotkeys_t() {
		public string
			tool_quick = "Ctrl+Shift+Q",
			tool_wnd = "Ctrl+Shift+W",
			tool_elm = "Ctrl+Shift+E",
			tool_uiimage
			;
	}
	public hotkeys_t hotkeys = new();
	
	//font of various UI parts
	public record struct font_t(string name, double size = 9);
	public font_t font_output = new("Consolas");
	
	//Options -> Templates
	public int templ_use;
	//public int templ_flags;
	
	//code editor
	public bool edit_wrap, edit_noImages;
	
	//code info
	public bool ci_complGroup = true, ci_formatCompact = true, ci_formatTabIndent = true;
	public int ci_complParen; //0 spacebar, 1 always, 2 never
	public int ci_rename;
	
	//CONSIDER: option to specify completion keys/chars. See https://www.quickmacros.com/forum/showthread.php?tid=7263
	//public byte ci_complOK = 15; //1 Enter, 2 Tab, 4 Space, 8 other
	//public byte ci_complArgs = 4; //1 Enter, 2 Tab, 4 Space (instead of ci_complParen)
	//maybe option to use Tab for Space, and disable Space. Tab would add space or/and () like now Space does.
	
	//public byte ci_formatBraceNewline; //0 never, 1 always, 2 type/function
	//public byte ci_formatIndentation; //0 tab, 1 4 spaces, 2 2 spaces
	
	//panel Files
	public bool files_multiSelect;
	
	//file type icons
	public record struct icons_t {
		public string
			ft_script,
			ft_class,
			ft_folder,
			ft_folderOpen
			;
	}
	public icons_t icons;
	
	//panel Output
	public bool output_wrap, output_white, output_topmost;
	
	//panel Outline
	public byte outline_flags;
	
	//panel Open
	public byte openFiles_flags;
	
	//panel Recipe
	public sbyte recipe_zoom;
	
	//settings common to various tools
	public bool tools_pathUnexpand = true, tools_pathLnk;
	
	//saved positions of various windows
	public record struct wndpos_t {
		public string main, wnd, elm, uiimage, ocr, recorder, icons, symbol;
	}
	public wndpos_t wndpos;
	
	//Delm
	public record struct delm_t() {
		public string hk_capture = "F3", hk_insert = "F4"; //for all tools
		public string wait, actionn; //named actionn because once was int action
		public int flags;
	}
	public delm_t delm = new();
	
	//DInputRecorder
	public record struct recorder_t() {
		public bool keys = true, text = true, text2 = true, mouse = true, wheel, drag, move;
		public int xyIn;
		public string speed = "10";
	}
	public recorder_t recorder = new();
	
	//DIcons
	public int dicons_listColor;
	public bool dicons_contrastUse;
	public string dicons_contrastColor = "#E0E000";
	
	//DPortable
	public string portable_dir;
	
	//DOcr
	public Au.Tools.OcrEngineSettings ocr;
	
	//WPF preview
	public int wpfpreview_xy;
	
	//DSnippets
	public Dictionary<string, HashSet<string>> ci_hiddenSnippets;
	
	//CiGoTo
	public Dictionary<string, CiGoTo.AssemblySett> ci_gotoAsm;
	public int ci_gotoTab;
	
	//CiFindGo
	public bool ci_findgoDclick;
	
	//panel Find
	public string find_skip;
	public int find_searchIn, find_printSlow = 50;
	public bool find_parallel;
	public FRRecentItem[] find_recent, find_recentReplace; //big arrays should be at the end //CONSIDER: save in separate file. Then the size limit can be bigger.
}

/// <summary>
/// Workspace settings.
/// WorkspaceDirectory + @"\settings.json"
/// </summary>
record WorkspaceSettings : JSettings {
	public static WorkspaceSettings Load(string jsonFile) => Load<WorkspaceSettings>(jsonFile);
	
	public FilesModel.UserData[] users;
	
	public string ci_skipFolders;
}
