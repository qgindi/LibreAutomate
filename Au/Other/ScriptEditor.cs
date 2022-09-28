namespace Au.More;

/// <summary>
/// Contains functions to interact with the script editor, if available.
/// </summary>
public static class ScriptEditor {
	/// <summary>
	/// Finds editor's message-only window used with WM_COPYDATA etc.
	/// Uses <see cref="wnd.Cached_"/>.
	/// </summary>
	internal static wnd WndMsg_ => s_wndMsg.FindFast(null, c_msgWndClassName, true);
	static wnd.Cached_ s_wndMsg, s_wndMain;

	/// <summary>
	/// Class name of <see cref="WndMsg_"/> window.
	/// </summary>
	internal const string c_msgWndClassName = "Au.Editor.m3gVxcTJN02pDrHiQ00aSQ";

	internal static wnd WndMain_(bool show = false) {
		var w = WndMsg_;
		return w.Is0 ? default : s_wndMain.Get(() => (wnd)w.Send(Api.WM_USER, 0, show ? 1 : 0));
	}

	/// <summary>
	/// Returns true if editor is running.
	/// </summary>
	public static bool Available => !WndMsg_.Is0;

	/// <summary>
	/// The main editor window.
	/// </summary>
	/// <param name="show">Show the window (if the editor program is running).</param>
	/// <returns><c>default(wnd)</c> if the editor program isn't running or its main window still wasn't visible.</returns>
	public static wnd MainWindow(bool show = false) => WndMain_(show);

	/// <summary>
	/// Opens the specified source file (script etc).
	/// Does nothing if editor isn't running.
	/// </summary>
	/// <param name="file">Source file. Can be full path, or relative path in workspace, or file name with <c>".cs"</c>.</param>
	public static void Open(string file) => OpenAndGoToLine(file, 0);

	/// <summary>
	/// Opens the specified source file (script etc) and sets the editor's current position at the start of the specified line.
	/// Does nothing if editor isn't running.
	/// </summary>
	/// <param name="file">Source file. Can be full path, or relative path in workspace, or file name with <c>".cs"</c>.</param>
	/// <param name="line">1-based line index. If 0, just opens the file.</param>
	public static void OpenAndGoToLine(string file, int line) {
		var w = WndMsg_; if (w.Is0) return;
		Api.AllowSetForegroundWindow(w.ProcessId);
		WndCopyData.Send<char>(w, 4, file, line);
	}

	/// <summary>
	/// Gets icon string in specified format.
	/// </summary>
	/// <returns>Returns null if editor isn't running or if the file does not exist. Read more in Remarks.</returns>
	/// <param name="file">Script file/folder path etc, or icon name. See <see cref="EGetIcon"/>.</param>
	/// <param name="what">The format of input and output strings.</param>
	/// <remarks>
	/// If <i>what</i> is <b>IconNameToXaml</b> and <i>file</i> is literal string and using default compiler, the compiler adds XAML to assembly resources and this function gets it from there, not from editor, and this function works everywhere.
	/// </remarks>
	public static string GetIcon(string file, EGetIcon what) {
		var del = IconNameToXaml_;
		if (del != null) return del(file, what);

		if (what == EGetIcon.IconNameToXaml && script.role != SRole.EditorExtension) {
			if (file.Starts("*<")) file = file[1..]; //"*<library>*icon", else "*icon"
			var rr = ResourceUtil.TryGetString_(file);
			if (rr != null) return rr;
			//our compiler (_CreateManagedResources) adds XAML of icons to resources, but only from literal strings
		}

		var w = WndMsg_; if (w.Is0) return null;
		WndCopyData.SendReceive<char>(w, (int)Math2.MakeLparam(10, (int)what), file, out string r);
		return r;
		//rejected: add option to get serialized Bitmap instead. Now loads XAML in this process. It is 230 ms and +27 MB.
		//	Nothing good if the toolbar etc also uses XAML icons directly, eg for non-script items. And serializing is slow.
		//	Now not actual because of cache.
	}

	/// <summary>
	/// Editor sets this. Library uses it to avoid sendmessage.
	/// </summary>
	internal static Func<string, EGetIcon, string> IconNameToXaml_;

	//[StructLayout(LayoutKind.Sequential, Size = 64)] //note: this struct is in shared memory. Size must be same in all library versions.
	//internal struct SharedMemoryData_ {
	//	int _wndEditorMsg, _wndEditorMain;

	//	internal wnd wndEditorMsg {
	//		get {
	//			if (_wndEditorMsg != 0) {
	//				var w = (wnd)_wndEditorMsg;
	//				if (w.ClassNameIs(c_msgWndClassName)) return w;
	//				//_wndEditorMsg = 0; //no, unsafe
	//			}
	//			return default;
	//		}
	//		set { _wndEditorMsg = (int)value; }
	//	}
	//	internal wnd wndEditorMain {
	//		get => wndEditorMsg.Is0 ? default : (wnd)_wndEditorMain;
	//		set { _wndEditorMain = (int)value; }
	//	}

	//}
}
