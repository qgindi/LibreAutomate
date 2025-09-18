using Au.Controls;
using Au.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;

#if CONTROLS
using App = Au.Tools.Editor;
namespace Au.Tools;
#endif

/// <summary>
/// Program settings.
/// folders.ThisAppDocuments + @".settings\Settings.json"
/// </summary>
record AppSettings : JSettings {
	//This is loaded at startup and therefore must be fast.
	//	NOTE: Don't use types that would cause to load UI dlls (WPF etc). Eg when it is a nested type and its parent class is a WPF etc control.
	//	Speed: first time 80 ms. Mostly to load/jit/etc dlls used by JsonSerializer. Later fast regardless of data size.
	
	public static JsonSerializerOptions SerializerOptions2 { get; } = new(JSettings.SerializerOptions) { PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate, IgnoreReadOnlyFields = false };
	//note: don't modify JSettings.SerializerOptions. Would be unexpected for editorExtension scripts.
	
	public static void Load() { //in TP thread
		var r = Load<AppSettings>(DirBS + "Settings.json", jsOpt: SerializerOptions2);
		r._Loaded();
		App.Settings = r;
		
		IEditor.Editor = new _KInterface();
	}
	
	public static void SetReloadModifiedExternally() { //in main thread
		App.Settings.ModifiedExternally += static () => {
			if (!App.Settings.Reload(out AppSettings r)) return;
			r._Loaded();
			Debug_.PrintIf(r.session.user != App.Settings.session.user);
			r.session.user = App.Settings.session.user;
			App.Settings = r;
		};
		
		//Never mind: Currently only Settings.json is synced. There are many other settings/snippets/etc files. Some are separate in PiP. It's important to sync Settings.json if using PiP.
	}
	
	void _Loaded() {
		if (session_main == null) _InitSessionSettings();
		session = miscInfo.isChildSession ? session_pip ??= new() : session_main;
		_NE(ref session.user) ??= Guid.NewGuid().ToString();
		session.hotkeys ??= new();
		(font_output ??= new()).Normalize("Consolas", 9);
		(font_recipeText ??= new()).Normalize("Calibri", 10.5);
		(font_recipeCode ??= new()).Normalize("Consolas", 9);
		(font_find ??= new()).Normalize("Consolas", 9);
	}
	
	static ref string _NE(ref string s) {
		if (s == "") s = null;
		return ref s;
	}
	
#if IDE_LA
	public static readonly string DirBS = folders.ThisAppDocuments + @".settings_\";
#else
	public static readonly string DirBS = folders.ThisAppDocuments + @".settings\";
#endif
	
	#region settings that are different in main and child (PiP) session
	
	public record session_t {
		public string user;
		public bool runHidden, startVisibleIfNotAutoStarted;
		public bool checkForUpdates;
		public int checkForUpdatesDay;
		public int wpfpreview_xy;
		public wndpos_t wndpos;
		public hotkeys_t hotkeys;
	}
	public session_t session_main, session_pip;
	[JsonIgnore] public session_t session;
	
	[JsonIgnore] public ref bool runHidden => ref session.runHidden;
	[JsonIgnore] public ref bool startVisibleIfNotAutoStarted => ref session.startVisibleIfNotAutoStarted;
	[JsonIgnore] public string userGuid => session.user;
	[JsonIgnore] public ref bool checkForUpdates => ref session.checkForUpdates;
	[JsonIgnore] public ref int checkForUpdatesDay => ref session.checkForUpdatesDay;
	[JsonIgnore] public ref int wpfpreview_xy => ref session.wpfpreview_xy;
	
	//saved positions of various windows
	public record struct wndpos_t {
		public string main, wnd, elm, uiimage, ocr, recorder, icons, symbol;
	}
	[JsonIgnore] public ref wndpos_t wndpos => ref session.wndpos;
	
	//Options > Hotkeys
	public record hotkeys_t {
		public string
			tool_quick = "Ctrl+Shift+Q",
			tool_wnd = "Ctrl+Shift+W",
			tool_elm = "Ctrl+Shift+E",
			tool_uiimage
			;
	}
	[JsonIgnore] public hotkeys_t hotkeys => session.hotkeys;
	
	void _InitSessionSettings() {
		//previously there were no session settings. Copy from the JSON root if need. Else existing users would lose their settings.
		if (user != null) {
			try {
				var file = DirBS + "Settings.json";
				if (filesystem.exists(file)) {
					session_main = JsonSerializer.Deserialize<session_t>(filesystem.loadBytes(file), SerializerOptions2);
				}
				user = null;
			}
			catch (Exception ex) { Debug_.Print(ex); }
		}
		
		session_main ??= new();
	}
	[JsonInclude] string user; //it's one of fields that previoulsy were in the JSON root but now are in a nested record. Now this field is used to detect the old format.
	
	#endregion
	
	public string workspace;
	public string[] recentWS;
	
	//When need a nested type, use record class. Everything works well; later can add/remove members like in main type.
	//Don't use record struct when need to set init values (now or in the future), because:
	//	1. Older .NET versions don't support it, or have bugs.
	//		Eg .NET 6.0.3 throws "InvalidCastException, Unable to cast object of type 'System.String' to type 'hotkeys_t'" when `public record hotkeys_t()` (need the `()` when using default field values).
	//			Works if `public record hotkeys_t(string a, string b)`, but then need 'new("a", "b")'; I don't like it etc.
	//	2. If `public record hotkeys_t(string a, string b)`, and later added a new member like `, c = "value"`, the value is null/0. The deserializer uses the default ctor.
	//		Also the same happens if somebody deletes an existing member from JSON.
	//		Works well with `public record hotkeys_t()`, but cannot use it because of 1.
	//Note: deserializer always creates new object, even if default object created. Avoid custom ctors etc.
	//If like `public hotkeys_t hotkeys = new()`, creates new object 2 times: 1. explicit new(); 2. when deserializing. Also in JSON can be `= null`. Move the `new()` to _Loaded.
	//Tuple does not work well. New members are null/0. Also item names in file are like "Item1".
	
	//font of various UI parts
	public record font_t {
		public string name;
		public double size;
		
		string _defName;
		double _defSize;
		
		internal void Normalize(string defName, double defSize) {
			_defName = defName;
			_defSize = defSize;
			Normalize();
		}
		
		public void Normalize() {
			if (name.NE()) name = _defName;
			if (size == 0) size = _defSize; else size = Math.Clamp(size, 6, 30);
		}
	}
	public font_t font_output, font_recipeText, font_recipeCode, font_find;
	
	//Options > Templates
	public int templ_use;
	//public int templ_flags;
	
	//Options > Other
	public string internetSearchUrl { get => field ?? "https://www.google.com/search?q="; set { field = value.NullIfEmpty_(); } }
	public bool localDocumentation;
	public bool? comp_printCompiled = false;
	
	//code editor
	public bool edit_wrap, edit_noImages;
	public string edit_theme;
	
	//code info, autocorrection, formatting
	public bool ci_complGroup = true, ci_formatCompact = true, ci_formatTabIndent = true, ci_formatAuto = true, ci_semicolon = true;
	public bool ci_enterBeforeParen = true, ci_enterBeforeSemicolon = true, ci_tempRawEnter;
	public int ci_complParen { get => field; set { field = value.EnsureValid_(0, 2); } }
	public int ci_enterWith { get => field; set { field = value.EnsureValid_(0, 2); } }
	public int ci_rename;
	
	//AI
	public string ai_modelDocSearch, ai_modelDocChat, ai_modelIconSearch, ai_modelIconImprove;
	public readonly DictionaryI_<string> ai_ak = new();
	
	//panel Files
	public bool files_multiSelect;
	
	//file type icons
	public record struct icons_t {
		public string ft_script, ft_class, ft_folder, ft_folderOpen;
	}
	public icons_t icons;
	
	//panel Output
	public bool output_wrap, output_white;
	
	//panel Outline
	public byte outline_flags;
	
	//panel Open
	public byte openFiles_flags;
	
	//panel Recipe
	public sbyte recipe_zoom;
	
	//panel Mouse
	public bool mouse_singleLine;
	public int mouse_limitText = 100;
	
	//panel Debug
	public record debug_t {
		public bool stepIntoAll, noJMC, printVarCompact, activateLA;
		public double hVar = 150, hStack = 100;
		public byte breakT = 15; //flags: 1 enabled, 2 the exceptions list is active, 4 not exceptions in the list, 8 when caught
		public byte breakU = 1; //flags: 1 enabled
		const string c_defNotExc = """
System.OperationCanceledException
System.Threading.Tasks.TaskCanceledException

""";
		public string breakListT = c_defNotExc, breakListU = c_defNotExc;
		public byte printEvents;
	}
	public readonly debug_t debug = new();
	
	//settings common to various tools
	public bool tools_pathUnexpand = true, tools_pathLnk;
	
	//DIcons
	public int dicons_listColor;
	public bool dicons_contrastUse;
	public string dicons_contrastColor = "#E0E000";
	
	//DPortable
	public string portable_dir;
	public int portable_check = -1;
	public string[] portable_skip;
	
	//DSnippets
	public Dictionary<string, HashSet<string>> ci_hiddenSnippets;
	
	//CiGoTo
	public record gotoAsm_t {
		public string repo, path, context;
		public bool csharp;
		public gotoAsm_t() { csharp = true; }
	}
	public Dictionary<string, gotoAsm_t> ci_gotoAsm;
	public int ci_gotoTab;
	
	//CiFindGo
	public bool ci_findgoDclick;
	
	//DInputRecorder
	public record recorder_t {
		public bool keys = true, text = true, text2 = true, mouse = true, wheel, drag, move;
		public int xyIn;
		public string speed = "10";
	}
	public readonly recorder_t recorder = new();
	
	//Delm
	public record delm_t {
		public string hk_capture = "F3", hk_insert = "F4", hk_smaller = "Shift+F3"; //for all tools
		public string def_wait, def_action;
		public bool? def_UIA;
		public int flags;
	}
	public readonly delm_t delm = new();
	
	//DOcr
	public record ocr_t {
		public string wLang, tLang, tCL, gKey, gFeat, gIC, mUrl, mKey;
	}
	public ocr_t ocr;
	
	//panel Find
	public string find_skip;
	public int find_searchIn, find_printSlow = 50;
	public bool find_parallel, find_case, find_word;
	
	//DNuget
	public bool nuget_noPrerelease;
	
	//other
	public int publish, export;
	public bool? minimalSDK;
	public string nilesoftShellDir;
	
	class _KInterface : IEditor {
		string IEditor.SettingsDirBS => DirBS;
		
		string IEditor.ThemeName { get => App.Settings.edit_theme; set { App.Settings.edit_theme = value; } }

		string IEditor.InternetSearchUrl => App.Settings.internetSearchUrl;
    }
}

/// <summary>
/// Workspace settings.
/// WorkspaceDirectory + @"\settings.json"
/// </summary>
record WorkspaceSettings : JSettings {
	public static WorkspaceSettings Load(string jsonFile) => Load<WorkspaceSettings>(jsonFile);
	
	public record User(string guid) {
		public string startupScripts, gitUrl;
		public bool gitBackup;
	}
	public User[] users;
	
	public User CurrentUser {
		get {
			if (_cu == null) {
				_cu = users?.FirstOrDefault(o => o.guid == App.Settings.userGuid);
				if (_cu == null) {
					_cu = new User(App.Settings.userGuid);
					users = users == null ? new[] { _cu } : users.InsertAt(0, _cu);
				}
			}
			return _cu;
		}
	}
	User _cu;
	
	public string ci_skipFolders;
	
	public string syncfs_skip = """
*\.git
*\.vs
*\bin
*\obj

""";
}
