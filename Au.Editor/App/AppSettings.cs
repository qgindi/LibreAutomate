/// <summary>
/// Program settings.
/// folders.ThisAppDocuments + @".settings\Settings.json"
/// </summary>
record AppSettings : JSettings
{
	//This is loaded at startup and therefore must be fast.
	//	NOTE: Don't use types that would cause to load UI dlls (WPF etc). Eg when it is a nested type and its parent class is a WPF etc control.
	//	Speed tested with .NET 5: first time 40-60 ms. Mostly to load/jit/etc dlls used in JSON deserialization, which then is fast regardless of data size.
	//	CONSIDER: Jit_ something in other thread. But it isn't good when runs at PC startup.

	public static AppSettings Load() => Load<AppSettings>(DirBS + "Settings.json");

	public static readonly string DirBS = folders.ThisAppDocuments + @".settings\";

	public string user, workspace;
	public string[] recentWS;

	public bool runHidden, files_multiSelect;

	//When need a nested type, use record class. Everything works well; can add/remove members like in main type.
	//	Somehow .NET does not support struct and record struct, InvalidCastException.
	//	Tuple does not work well. New members are null. Also item names in file are like "Item1".
	public record hotkeys_t
	{
		public string
			tool_quick = "Ctrl+Shift+Q",
			tool_wnd = "Ctrl+Shift+W",
			tool_elm = "Ctrl+Shift+E"
			;
	}
	public hotkeys_t hotkeys = new();

	public record wndpos_t {
		public string main, wnd, elm, uiimage, recorder, icons;
	}
	public wndpos_t wndpos = new();

	public bool edit_wrap, edit_noImages, output_wrap, output_white, output_topmost;

	public int templ_use;
	public int templ_flags;

	//public byte ci_shiftEnterAlways, ci_shiftTabAlways;
	//public SIZE ci_sizeSignXaml, ci_sizeComplXaml, ci_sizeComplList;
	public bool ci_complGroup = true, ci_unexpandPath = true;
	public int ci_complParen; //0 spacebar, 1 always, 2 never

	public byte outline_flags;

	public record delm_t
	{
		public string hk_capture = "F3", hk_insert = "F4"; //for all tools
		public string wait, actionn; //named actionn because once was int action
		public int flags;
	}
	public delm_t delm = new();

	public record recorder_t
	{
		public bool keys = true, text = true, text2 = true, mouse = true, wheel, drag, move;
		public int xyIn;
		public string speed = "10";
	}
	public recorder_t recorder = new();

	public sbyte recipe_zoom;

	public string db_copy_ref, db_copy_doc, db_copy_winapi;

	public Dictionary<string, CiGoTo.AssemblySett> ci_gotoAsm;

	public string find_skip;
	public int find_searchIn, find_printSlow = 50;
	public FRRecentItem[] find_recent, find_recentReplace; //big arrays should be at the end
}

/// <summary>
/// Workspace settings.
/// WorkspaceDirectory + @"\settings.json"
/// </summary>
record WorkspaceSettings : JSettings
{
	public static WorkspaceSettings Load(string jsonFile) => Load<WorkspaceSettings>(jsonFile);

	public FilesModel.UserData[] users;

}
