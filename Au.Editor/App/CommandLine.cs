using Au.Compiler;

static class CommandLine {
	/// <summary>
	/// Processes command line of this program. Called before any initialization.
	/// Returns true if this instance must exit.
	/// </summary>
	public static bool ProgramStarted1(string[] args, out int exitCode) {
		//print.it(args);
		exitCode = 0; //note: Environment.ExitCode bug: the setter's set value is ignored and the process returns 0.
		int i = args.Length > 0 && args[0] is "/n" or "-n" ? 1 : 0;
		if (args.Length > i) {
			var s = args[i];
			if (s.Starts('/')) {
				switch (s) {
				case "/s":
					exitCode = _RunEditorAsAdmin();
					return true;
				case "/dd":
					UacDragDrop.NonAdminProcess.MainDD(args);
					return true;
				}
			} else if (!pathname.isFullPath(s)) {
				exitCode = _LetEditorRunScript(args, i);
				return true;
			}
		}
		return false;
	}
	
	/// <summary>
	/// Processes command line of this program. Called after partial initialization.
	/// Returns true if this instance must exit:
	/// 	1. If finds previous program instance; then sends the command line to it if need.
	/// 	2. If incorrect command line.
	/// </summary>
	public static bool ProgramStarted2(string[] args) {
		string s = null;
		int cmd = 0; //1 open workspace, 2 import workspace, 3 import files, -5 reload workspace
		bool restarting = false;
		if (args.Length > 0) {
			//print.it(args);
			s = args[0];
			if (s is ['/' or '-', ..]) {
				for (int i = 0; i < args.Length; i++) {
					s = args[i];
					bool good = s is ['/' or '-', ..];
					if (good) {
						switch (s.AsSpan(1)) {
						case "v":
							StartVisible = true;
							break;
						case "reload":
							cmd = -5;
							break;
						case "restart":
							restarting = true;
							break;
						case "raa":
							Raa = true;
							break;
						case "test":
							if (++i < args.Length) TestArg = args[i];
							break;
						default:
							good = false;
							break;
						}
					}
					if (!good) {
						dialog.showError("Unknown command line parameter", s);
						return true;
					}
				}
				s = null;
			} else { //one or more files
				if (args.Length == 1 && FilesModel.IsWorkspaceDirectoryOrZip_ShowDialogOpenImport(s, out cmd)) {
					switch (cmd) {
					case 1: WorkspaceDirectory = s; break;
					case 2: _importWorkspace = s; break;
					default: return true;
					}
				} else {
					cmd = 3;
					_importFiles = args;
				}
				StartVisible = true;
			}
		}
		
		//single instance
#if IDE_LA
		s_mutex = new Mutex(true, "Au.Editor.Mutex.m3gVxcTJN02pDrHiQ00aSQ_IDE_LA", out bool createdNew);
#else
		s_mutex = new Mutex(true, "Au.Editor.Mutex.m3gVxcTJN02pDrHiQ00aSQ", out bool createdNew);
#endif
		if (createdNew) return false;
		if (restarting) return Api.WaitForSingleObject(s_mutex.SafeWaitHandle.DangerousGetHandle(), 5000) is Api.WAIT_TIMEOUT or Api.WAIT_FAILED;
		
		var w = wnd.findFast(null, ScriptEditor.c_msgWndClassName, true);
		if (!w.Is0) {
			w.Send(Api.WM_USER, 0, 1); //auto-creates, shows and activates main window
			
			if (cmd != 0) {
				Thread.Sleep(100);
				
				if (cmd > 0) { //pass string
					if (cmd == 3) s = string.Join("\0", args); //import files
					WndCopyData.Send<char>(w, cmd, s);
				} else {
					w.Send(Api.WM_USER, -cmd);
				}
			}
		}
		return true;
	}
	static Mutex s_mutex; //GC
	
	/// <summary>
	/// null or argument after "/test".
	/// </summary>
	public static string TestArg;
	
	/// <summary>
	/// true if /v
	/// </summary>
	public static bool StartVisible;
	
	/// <summary>
	/// true if /raa
	/// </summary>
	public static bool Raa;
	
	/// <summary>
	/// Called after loading workspace. Before executing startup scripts, adding tray icon and creating UI.
	/// </summary>
	public static void ProgramLoaded() {
		WndUtil.UacEnableMessages(Api.WM_COPYDATA, /*Api.WM_DROPFILES, 0x0049,*/ Api.WM_USER, Api.WM_CLOSE);
		//WM_COPYDATA, WM_DROPFILES and undocumented WM_COPYGLOBALDATA=0x0049 should enable drag-drop from lower UAC IL processes, but only through WM_DROPFILES/DragAcceptFiles, not OLE D&D.
		
		WndUtil.RegisterWindowClass(ScriptEditor.c_msgWndClassName, _WndProc);
		_msgWnd = WndUtil.CreateMessageOnlyWindow(ScriptEditor.c_msgWndClassName);
		script.s_wndEditorMsg = _msgWnd;
	}
	
	/// <summary>
	/// Called from MainWindow.Loaded.
	/// </summary>
	public static void UILoaded() {
		if (_importWorkspace != null || _importFiles != null) {
			App.Dispatcher.InvokeAsync(() => {
				try {
					if (_importWorkspace != null) App.Model.ImportWorkspace(_importWorkspace);
					else App.Model.ImportFiles(_importFiles);
				}
				catch (Exception ex) { print.warning(ex); }
			}, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
		}
	}
	
	/// <summary>
	/// null or workspace folder specified in command line.
	/// </summary>
	public static string WorkspaceDirectory;
	
	static string _importWorkspace;
	static string[] _importFiles;
	
	static wnd _msgWnd;
	
	/// <summary>
	/// The message-only window.
	/// Available after loading the first workspace.
	/// </summary>
	public static wnd MsgWnd => _msgWnd;
	
	static nint _WndProc(wnd w, int message, nint wparam, nint lparam) {
		try {
			switch (message) {
			case Api.WM_USER:
				if (App.Loaded >= AppState.Unloading) return 0;
				return _WmUser(wparam, lparam);
			case Api.WM_COPYDATA:
				if (App.Loaded >= AppState.Unloading) return 0;
				return _WmCopyData(wparam, lparam);
			case RunningTasks.WM_TASK_ENDED: //WM_USER+900
				App.Tasks.TaskEnded2(wparam, lparam);
				return 0;
			}
		}
		catch (Exception ex) { print.warning(ex); return 0; }
		
		return Api.DefWindowProc(w, message, wparam, lparam);
	}
	
	static nint _WmUser(nint wparam, nint lparam) {
		switch (wparam) {
		case 0: //ScriptEditor.MainWindow, etc
			if (lparam == 1) App.ShowWindow(); //else returns default(wnd) if never was visible
			return App.Hmain.Handle;
		case 1: //ScriptEditor.ShowMainWindow
			if (lparam == 0) lparam = App.Wmain.IsVisible ? 2 : 1; //toggle
			if (lparam == 1) App.ShowWindow();
			else App.Wmain.Hide_();
			return 0;
		case 3: //get wpf preview window saved xy
			return App.Settings.wpfpreview_xy;
		case 4: //save wpf preview window xy
			App.Settings.wpfpreview_xy = (int)lparam;
			break;
		case 5:
			Menus.File.Workspace.Reload_this_workspace();
			break;
		case 10:
			UacDragDrop.AdminProcess.OnTransparentWindowCreated((wnd)lparam);
			break;
		case 20: //Triggers.DisabledEverywhere
			TriggersAndToolbars.OnDisableTriggers();
			break;
		case 30: //script.debug()
			if (!App.Wmain.IsVisible) App.ShowWindow(); else if (!App.Hmain.IsActive) { App.Hmain.TaskbarButton.Flash(3); /*timer.after(10000, _ => App.Hmain.TaskbarButton.Flash(0));*/ }
			return Panels.Debug.Attach((int)lparam) ? 1 : 0;
		}
		return 0;
	}
	
	static nint _WmCopyData(nint wparam, nint lparam) {
		var c = new WndCopyData(lparam);
		int action = Math2.LoWord(c.DataId), action2 = Math2.HiWord(c.DataId);
		bool isString = action < 100;
		string s = isString ? c.GetString() : null;
		byte[] b = isString ? null : c.GetBytes();
		switch (action) {
		case 1: //command line (ProgramStarted2)
			FilesModel.LoadWorkspace(s);
			break;
		case 2: //command line (ProgramStarted2)
			App.Model.ImportWorkspace(s);
			break;
		case 3: //command line (ProgramStarted2)
			Api.ReplyMessage(1); //avoid 'wait' cursor while we'll show dialog
			App.Model.ImportFiles(s.Split('\0'));
			break;
		case 4: //ScriptEditor.Open
			Api.ReplyMessage(1);
			_OpenFile();
			break;
		case 5 or 6: //script.end(name) or script.isRunning(name)
			return _ScriptAction(action);
		case 10: //ScriptEditor.GetIcon
			s = DIcons.GetIconString(s, (EGetIcon)action2);
			return s == null ? 0 : WndCopyData.Return<char>(s, wparam);
		case 11: //ScriptEditor.InvokeCommand
			return Menus.Invoke(s, false, (int)wparam);
		case 12: //ScriptEditor.GetCommandState
			return Menus.Invoke(s, true, (int)wparam);
		//case 13: //ScriptEditor.Folders (rejected)
		//	s = string.Join('|', (string)folders.ThisAppDocuments, (string)folders.ThisAppDataLocal, (string)folders.ThisAppTemp);
		//	return WndCopyData.Return<char>(s, wparam);
		case 14: //ScriptEditor.GetFileInfo
			return _GetFileInfo(s) is byte[] r1 ? WndCopyData.Return<byte>(r1, wparam) : 0;
		case 100: //script.run/runWait
		case 101: //run script from command line
			return _RunScript();
		case 110: //received from our non-admin drop-target process on OnDragEnter
			return UacDragDrop.AdminProcess.DragEvent((int)wparam, b);
		default:
			Debug_.Print("bad action");
			return 0;
		}
		return 1;
		
		nint _RunScript() {
			int mode = (int)wparam; //1 - wait, 3 - wait and get script.writeResult output, 4 restarting (set meta ifRunning run)
			var d = Serializer_.Deserialize(b);
			string file = d[0]; string[] args = d[1]; string pipeName = d[2];
			
			var f = App.Model?.FindCodeFile(file);
			if (f == null) {
				if (action == 101) print.it($"Command line: script '{file}' not found."); //else the caller script will throw exception
				return (int)script.RunResult_.notFound;
			}
			
			//options can be specified in args[0] like "[[name1=value1|name2=value2]]"
			//MCIfRunning? ifRunning = null;
			//if (args.FirstOrDefault() is string k && k.Like("[[*]]")) {
			//	foreach (var v in k.Segments("|", SegFlags.NoEmpty, 2..^2)) {
			//		bool good = false;
			//		if (k.IndexOf('=', v.start, v.Length) is int i && i > 0) {
			//			string sn = k[v.start..i], sv = k[++i..v.end];
			//			switch (sn) {
			//			case "ifRunning":
			//				if (good = Enum.TryParse(sv, out MCIfRunning ir)) ifRunning = ir;
			//				break;
			//			}
			//		}
			//		if (!good) { print.it($"<>Cannot start script {f.SciLink()}. Error in args[0]: {k}."); return (int)script.RunResult_.failed; }
			//	}
			//	args = args.RemoveAt(0);
			//}
			
			return CompileRun.CompileAndRun(true, f, args, noDefer: 0 != (mode & 1), wrPipeName: pipeName, ifRunning: 0 != (mode & 4) ? MCIfRunning.run : null);
		}
		
		nint _ScriptAction(int action) {
			if (App.Model.Find(s) is FileNode f) {
				return action switch {
					5 => App.Tasks.EndTasksOf(f) ? 1 : 2,
					6 => App.Tasks.IsRunning(f) ? 1 : 0,
					_ => 0
				};
			}
			print.warning($"File not found: '{s}'.", -1);
			return 0;
		}
		
		void _OpenFile() {
			var a = s.Split('|'); //"file|line|offset". line and/or offset is empty if was null.
			s = a[0];
			if (App.Model.Find(s) is FileNode f1) {
				int line = a[1].NE() ? -1 : a[1].ToInt() - 1;
				int offset = a[2].NE() ? -1 : a[2].ToInt();
				App.Model.OpenAndGoTo(f1, line, offset);
			} else print.warning($"File not found: '{s}'.", -1);
		}
		
		byte[] _GetFileInfo(string s) {
			int flags = s.ToInt(0, out int end);
			var f = end == s.Length ? Panels.Editor.ActiveDoc?.FN : App.Model.Find(s[++end..], FNFind.File);
			if (f != null) {
				var kind = f.FileType switch { FNType.Script => EFileKind.Script, FNType.Class => EFileKind.Class, _ => EFileKind.Other };
				string text = null; if (0 != (flags & 1)) f.GetCurrentText(out text, null);
				return Serializer_.Serialize(f.ItemPath, text, (int)kind, (int)f.Id, f.FilePath, App.Model.WorkspaceDirectory);
			}
			return null;
		}
	}
	
	//Called when command line starts with "/s". This process is running as SYSTEM in session 0.
	//This process is started by the Task Scheduler task installed by the setup program. The task started by App._RestartAsAdmin.
	[MethodImpl(MethodImplOptions.NoOptimization)]
	static unsafe int _RunEditorAsAdmin() {
		var s1 = _Api.GetCommandLine();
		//_MBox(new string(s1));
		//Normally it is like "C:\...\Au.Editor.exe /s sessionId" or "C:\...\Au.Editor.exe /s sessionId arguments",
		//	but if started from Task Scheduler it is "C:\...\Au.Editor.exe /s $(Arg0)".
		
		int len = CharPtr_.Length(s1) + 1;
		var span = new Span<char>(s1, len);
		var s2 = span.ToArray();
		int i = span.IndexOf("/s") + 1; //info: it's safe. Can't be "C:/s/..." because the scheduled task wasn't created like this.
		s2[i++] = 'n'; // /n - don't try to restart as admin
		
		//get session id
		char* se = null;
		int sesId = Api.strtoi(s1 + i, &se);
		if (se != s1 + i) { //remove the session id argument
			if (*se == 0) s2[i] = '\0'; //no more arguments
			else for (int j = (int)(se - s1); j < len;) s2[i++] = s2[j++];
		} else { //$(Arg0) not replaced. Probably started from Task Scheduler.
			s2[i] = '\0';
			sesId = _Api.WTSGetActiveConsoleSessionId();
			if (sesId < 1) return 1;
		}
		//_MBox(new string(s2));
		
		if (!_Api.WTSQueryUserToken(sesId, out var hToken)) return 2;
		if (_Api.GetTokenInformation(hToken, Api.TOKEN_INFORMATION_CLASS.TokenLinkedToken, out var hToken2, sizeof(nint), out _)) { //fails if non-admin user or if UAC turned off
			Api.CloseHandle(hToken);
			hToken = hToken2;
			
			//rejected: add uiAccess.
			//DWORD uiAccess=1; Api.SetTokenInformation(hToken, TokenUIAccess, &uiAccess, 4);
			
			//With uiAccess works better in some cases, eg on Win8 can set a window on top of metro.
			//Cannot use it because of SetParent API bug: fails if the new parent window is topmost and the old parent isn't (or is 0).
			//	SetParent is extensively used by winforms and WPF, to move parked controls from the parking window (message-only) to the real parent window.
			//	Then, if the window is topmost, SetParent fails and the control remains hidden on the parking window.
			//	Also, if it is the first control in form, form is inactive and like disabled. Something activates the parking window instead.
			//It seems uiAccess mode is little tested by Microsoft and little used by others. I even did not find anything about this bug.
			//I suspect this mode also caused some other anomalies.
			//	Eg sometimes activating the editor window stops working normally: it becomes active but stays behind other windows.
			
		} //else MBox(L"GetTokenInformation failed");
		
		if (!_Api.CreateEnvironmentBlock(out var eb, hToken, false)) return 3;
		
		var si = new Api.STARTUPINFO { cb = sizeof(Api.STARTUPINFO), dwFlags = Api.STARTF_FORCEOFFFEEDBACK };
		var desktop = stackalloc char[] { 'w', 'i', 'n', 's', 't', 'a', '0', '\\', 'd', 'e', 'f', 'a', 'u', 'l', 't', '\0' }; //"winsta0\\default"
		si.lpDesktop = desktop;
		
		if (!_Api.CreateProcessAsUser(hToken, null, s2, null, null, false, Api.CREATE_UNICODE_ENVIRONMENT, eb, null, si, out var pi)) {
			_MBox("CreateProcessAsUserW: " + lastError.message);
			return 4;
		}
		
		Api.CloseHandle(pi.hThread);
		Api.CloseHandle(pi.hProcess);
		//Api.AllowSetForegroundWindow(pi.dwProcessId); //fails
		
		_Api.DestroyEnvironmentBlock(eb);
		
		Api.CloseHandle(hToken);
		return 0;
	}
	
	/// <summary>
	/// Shows message box in interactive session. Called from session 0.
	/// </summary>
	[Conditional("DEBUG")]
	static void _MBox(object o) {
#if DEBUG
		var s = o.ToString();
		var title = "Debug";
		_Api.WTSSendMessage(default, _Api.WTSGetActiveConsoleSessionId(), title, title.Length * 2, s, s.Length * 2, _Api.MB_TOPMOST | _Api.MB_SETFOREGROUND, 0, out _, true);
#endif
	}
	
	/// <summary>
	/// Finds the message-only window. Starts editor if not running. In any case waits for the window max 15 s.
	/// </summary>
	/// <param name="wMsg"></param>
	static bool _EnsureEditorRunningAndGetMsgWindow(out wnd wMsg, string args) {
		wMsg = default;
		for (int i = 0; i < 1000; i++) { //if we started editor process, wait until it fully loaded, then it creates the message-only window
			wMsg = wnd.findFast(null, ScriptEditor.c_msgWndClassName, true);
			if (!wMsg.Is0) return true;
			if (i == 0) {
				var ps = new ProcessStarter_(process.thisExePath, args, rawExe: true);
				if (!ps.StartL(out var pi)) break;
				Api.AllowSetForegroundWindow(pi.dwProcessId);
				pi.Dispose();
				//note: the process will restart as admin if started from non-admin process, unless the program isn't installed correctly
			}
			Thread.Sleep(15);
		}
		return false;
	}
	
	//Initially for this was used native exe. Rejected because of AV false positives.
	//	Speed with native exe 50 ms, now 85 ms. Never mind.
	static unsafe int _LetEditorRunScript(string[] args, int iArg) {
		if (!_EnsureEditorRunningAndGetMsgWindow(out wnd w, iArg > 0 ? args[0] : null)) return (int)script.RunResult_.noEditor;
		
		//If script name has prefix *, need to wait until script process ends.
		//	Also auto-detect whether need to write script.writeResult to stdout.
		var file = args[iArg];
		args = args.RemoveAt(0, iArg + 1);
		int mode = 0; //1 - wait, 3 - wait and get script.writeResult output
		if (file.Starts('*')) {
			file = file[1..];
			mode |= 1;
			if ((default != Api.GetStdHandle(Api.STD_OUTPUT_HANDLE)) //redirected stdout
				|| _Api.AttachConsole(_Api.ATTACH_PARENT_PROCESS) //parent process is console
				) mode |= 2;
		}
		
		if (0 == (mode & 2)) return script.RunCL_(w, mode, file, args, null);
		
		return script.RunCL_(w, mode, file, args, static o => {
			var a = Encoding.UTF8.GetBytes(o);
			bool ok = Api.WriteFile2(Api.GetStdHandle(Api.STD_OUTPUT_HANDLE), a, out int n);
			if (!ok || n != a.Length) throw new AuException(0);
			//tested: 100_000_000 bytes OK.
		});
		//note: Console.Write does not write UTF8 if redirected. Console.OutputEncoding and SetConsoleOutputCP fail.
		//note: in cmd execute this to change cmd console code page to UTF-8: chcp 65001
	}
	
	static unsafe class _Api {
		[DllImport("kernel32.dll")]
		internal static extern int WTSGetActiveConsoleSessionId();
		
		[DllImport("wtsapi32.dll")]
		internal static extern bool WTSQueryUserToken(int SessionId, out IntPtr phToken);
		
		[DllImport("advapi32.dll")]
		internal static extern bool GetTokenInformation(IntPtr TokenHandle, Api.TOKEN_INFORMATION_CLASS TokenInformationClass, out IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);
		
		[DllImport("userenv.dll")]
		internal static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);
		
		[DllImport("userenv.dll")]
		internal static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);
		
		[DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUserW", SetLastError = true)]
		internal static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, char[] lpCommandLine, void* lpProcessAttributes, void* lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, in Api.STARTUPINFO lpStartupInfo, out Api.PROCESS_INFORMATION lpProcessInformation);
		
		[DllImport("kernel32.dll", EntryPoint = "GetCommandLineW")]
		internal static extern char* GetCommandLine();
		
#if DEBUG
		[DllImport("wtsapi32.dll", EntryPoint = "WTSSendMessageW")]
		internal static extern bool WTSSendMessage(IntPtr hServer, int SessionId, string pTitle, int TitleLength, string pMessage, int MessageLength, uint Style, int Timeout, out int pResponse, bool bWait);
		internal const uint MB_TOPMOST = 0x40000;
		internal const uint MB_SETFOREGROUND = 0x10000;
#endif
		
		[DllImport("kernel32.dll")]
		internal static extern bool AttachConsole(uint dwProcessId);
		
		internal const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
	}
}
