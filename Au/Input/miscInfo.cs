namespace Au;

/// <summary>
/// Contains functions to get miscellaneous info not found in other classes of this library and .NET.
/// </summary>
/// <seealso cref="osVersion"/>
/// <seealso cref="folders"/>
/// <seealso cref="process"/>
/// <seealso cref="screen"/>
/// <seealso cref="script"/>
/// <seealso cref="perf"/>
/// <seealso cref="uacInfo"/>
/// <seealso cref="Dpi"/>
/// <seealso cref="Environment"/>
/// <seealso cref="System.Windows.Forms.SystemInformation"/>
/// <seealso cref="System.Windows.SystemParameters"/>
public static class miscInfo {
	/// <summary>
	/// Calls API <msdn>GetGUIThreadInfo</msdn>. It gets info about mouse capturing, menu mode, move/size mode, focus, caret, etc.
	/// </summary>
	/// <param name="g">API <msdn>GUITHREADINFO</msdn>.</param>
	/// <param name="idThread">Thread id. If 0 - the foreground (active window) thread. See <see cref="process.thisThreadId"/>, <see cref="wnd.ThreadId"/>.</param>
	public static unsafe bool getGUIThreadInfo(out GUITHREADINFO g, int idThread = 0) {
		g = new GUITHREADINFO { cbSize = sizeof(GUITHREADINFO) };
		return Api.GetGUIThreadInfo(idThread, ref g);
	}
	
	/// <summary>
	/// Gets caret rectangle.
	/// </summary>
	/// <returns><c>false</c> if failed.</returns>
	/// <param name="r">Receives the rectangle, in screen coordinates.</param>
	/// <param name="w">Receives the caret owner control or the focused control.</param>
	/// <param name="orMouse">If fails, get mouse pointer coordinates.</param>
	/// <remarks>
	/// Some apps use non-standard caret; then may fail.
	/// </remarks>
	public static bool getTextCursorRect(out RECT r, out wnd w, bool orMouse = false) {
		//rejected. Too few controls support it.
		///// <param name="preferSelection">Get text selection rectangle if possible.</param>
		
		if (getGUIThreadInfo(out var g)) {
			if (!g.hwndCaret.Is0) {
				if (g.rcCaret.bottom <= g.rcCaret.top) g.rcCaret.bottom = g.rcCaret.top + 16;
				r = g.rcCaret;
				g.hwndCaret.MapClientToScreen(ref r);
				w = g.hwndCaret;
				return true;
			}
			
			if (!g.hwndFocus.Is0) {
				w = g.hwndFocus;
				try {
					var e = elm.fromWindow(g.hwndFocus, EObjid.CARET, EWFlags.NoThrow | EWFlags.NotInProc);
					if (e?.GetRect(out r) == true) return true;
					
					if (g.hwndFocus.ClassNameIs("HwndWrapper[powershell_ise.exe;*")) {
						if (UiaUtil.GetCaretRectInPowerShell(out r)) return true;
					} else if (UiaUtil.ElementFocused() is { } ef) {
						if (ef.GetCaretRect(out r)) return true;
					}
					
					//GetGUIThreadInfo and MSAA don't work with winstore, winui3, Windows Terminal.
					//Most winstore and winui3 apps support IUIAutomationTextPattern2, and it gives correct caret rect.
					//Terminal supports only IUIAutomationTextPattern, and it gives correct caret rect when there is no selection.
					//Bad: some apps give client coordinates (bug, eg PowerShell). We can convert to screen easily, but can't know the coordinate type in unknown apps.
					//Win+; works well with all tested winstore apps and terminal, although some apps don't support even IUIAutomationTextPattern. What API it uses?
					//IME works everywhere. What API it uses?
					//PhraseExpress doesn't work.
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}
		}
		
		if (orMouse) {
			Api.GetCursorPos(out var p);
			r = new RECT(p.x, p.y, 0, 16);
		} else r = default;
		
		w = default;
		return false;
		
		//note: in Word, after changing caret pos, GetGUIThreadInfo and MSAA get pos 0 0. After 0.5 s gets correct. After typing always correct.
	}
	
	/// <summary>
	/// Returns <c>true</c> if current thread is on the input desktop and therefore can use mouse, keyboard, clipboard and window functions.
	/// </summary>
	/// <param name="detectLocked">Return <c>false</c> if the active window is a full-screen window of <c>LockApp.exe</c> on Windows 10+. For example when computer has been locked but still not displaying the password field. Slower.</param>
	/// <remarks>
	/// Usually this app is running on default desktop. Examples of other desktops: the <c>Ctrl+Alt+Delete</c> screen, the PC locked screen, screen saver, UAC consent, custom desktops. If one of these is active, this process cannot use many mouse, keyboard, clipboard and window functions. They either throw exception or do nothing.
	/// </remarks>
	/// <seealso cref="InputDesktopException"/>
	public static unsafe bool isInputDesktop(bool detectLocked = false) {
		var w = wnd.active;
		if (w.Is0) { //tested: last error code 0
			int i = 0;
			if (!Api.GetUserObjectInformation(Api.GetThreadDesktop(Api.GetCurrentThreadId()), Api.UOI_IO, &i, 4, out _)) return true; //slow
			return i != 0;
			//also tested several default screensavers on Win10 and 7. Goes through this branch. When closed, works like when locked (goes through other branch until next input).
		} else {
			if (detectLocked && osVersion.minWin10) {
				var rw = w.Rect;
				if (rw.left == 0 && rw.top == 0) {
					var rs = screen.primary.Rect;
					if (rw == rs) {
						var s = w.ProgramName;
						if (s.Eqi("LockApp.exe")) return false;
					}
				}
			}
			return true;
			//info: in lock screen SHQueryUserNotificationState returns QUNS_NOT_PRESENT.
			//	Also documented but not tested: screen saver.
			//	tested: QUNS_ACCEPTS_NOTIFICATIONS (normal) when Ctrl+Alt+Delete.
			//	However it is too slow, eg 1300 mcs.
		}
	}
	
	//public static unsafe string GetInputDesktopName() {
	//	var hd = Api.OpenInputDesktop(0, false, Api.GENERIC_READ); //error "Access is denied" when this process is admin. Need SYSTEM.
	//	//if (hd == default) throw new AuException(0);
	//	if (hd == default) return null;
	//	string s = null;
	//	var p = stackalloc char[300];
	//	if (Api.GetUserObjectInformation(hd, Api.UOI_NAME, p, 600, out int len) && len >= 4) s = new(p, 0, len / 2 - 1);
	//	Api.CloseDesktop(hd);
	//	return s;
	//}
}
