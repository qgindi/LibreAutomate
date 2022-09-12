//FUTURE: /portable
//	1. Set folders.ThisAppDocuments etc in folders.ThisApp\Portable.
//	2. Don't restart as admin.
//	3. Don't allow to set option to run at startup.
//	4. Etc.

static class CommandLine {
	/// <summary>
	/// Processes command line of this program. Called before any initialization.
	/// Returns true if this instance must exit.
	/// </summary>
	public static bool ProgramStarted1(string[] args, out int exitCode) {
		//print.it(args);
		exitCode = 0; //note: Environment.ExitCode bug: the setter's set value is ignored and the process returns 0.
		if (args.Length > 0) {
			var s = args[0];
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
				exitCode = _LetEditorRunScript(args);
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
		int cmd = 0;
		if (args.Length > 0) {
			//print.it(args);

			for (int i = 0; i < args.Length; i++) {
				if (args[i].Starts('-')) args[i] = args[i].ReplaceAt(0, 1, "/");
				//if (args[i].Starts('/')) args[i] = args[i].Lower();
			}

			s = args[0];
			if (s.Starts('/')) {
				for (int i = 0; i < args.Length; i++) {
					s = args[i];
					switch (s) {
					case "/test":
						if (++i < args.Length) TestArg = args[i];
						break;
					case "/v":
						StartVisible = true;
						break;
					default:
						dialog.showError("Unknown command line parameter", s);
						return true;
					}
				}
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
		s_mutex = new Mutex(true, "Au.Editor.Mutex.m3gVxcTJN02pDrHiQ00aSQ", out bool createdNew);
		if (createdNew) return false;

		var w = wnd.findFast(null, ScriptEditor.c_msgWndClassName, true);
		if (!w.Is0) {
			w.Send(Api.WM_USER, 0, 1); //auto-creates, shows and activates main window

			if (cmd != 0) {
				Thread.Sleep(100);

				if (cmd == 3) s = string.Join("\0", args); //import files
				WndCopyData.Send<char>(w, cmd, s);
			}
		}
		return true;
	}
	static object s_mutex; //GC

	/// <summary>
	/// null or argument after "/test".
	/// </summary>
	public static string TestArg;

	/// <summary>
	/// true if /v
	/// </summary>
	public static bool StartVisible;

	/// <summary>
	/// Called after loading workspace. Before executing startup scripts, adding tray icon and creating UI.
	/// </summary>
	public static void ProgramLoaded() {
		WndUtil.UacEnableMessages(Api.WM_COPYDATA, /*Api.WM_DROPFILES, 0x0049,*/ Api.WM_USER, Api.WM_CLOSE);
		//WM_COPYDATA, WM_DROPFILES and undocumented WM_COPYGLOBALDATA=0x0049 should enable drag/drop from lower UAC IL processes, but only through WM_DROPFILES/DragAcceptFiles, not OLE D&D.

		WndUtil.RegisterWindowClass(ScriptEditor.c_msgWndClassName, _WndProc);
		_msgWnd = WndUtil.CreateMessageOnlyWindow(ScriptEditor.c_msgWndClassName);
	}

	/// <summary>
	/// Called at the end of MainWindow.OnSourceInitialized.
	/// </summary>
	public static void UILoaded() {
		if (_importWorkspace != null || _importFiles != null) {
			App.Dispatcher.InvokeAsync(() => {
				try {
					if (_importWorkspace != null) App.Model.ImportWorkspace(_importWorkspace);
					else App.Model.ImportFiles(_importFiles);
				}
				catch (Exception ex) { print.it(ex.Message); }
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
	/// Don't call before the program is fully inited and OnMainFormLoaded called.
	/// </summary>
	public static wnd MsgWnd => _msgWnd;

	static nint _WndProc(wnd w, int message, nint wparam, nint lparam) {
		switch (message) {
		case Api.WM_COPYDATA:
			if (App.Loaded >= EProgramState.Unloading) return default;
			try { return _WmCopyData(wparam, lparam); }
			catch (Exception ex) { print.it(ex.Message); }
			return default;
		case Api.WM_USER:
			if (App.Loaded >= EProgramState.Unloading) return default;
			switch (wparam) {
			case 0:
				if (lparam == 1) App.ShowWindow(); //else returns default(wnd) if never was visible
				return App.Hmain.Handle;
			case 10:
				UacDragDrop.AdminProcess.OnTransparentWindowCreated((wnd)lparam);
				break;
			case 20: //from Triggers.DisabledEverywhere
				TriggersAndToolbars.OnDisableTriggers();
				break;
			case 30: //from script.debug()
				if (App.Model.DebuggerScript is string s && s.Length > 0) {
					if (App.Model.FindCodeFile(s) is FileNode f && f.IsScript) {
						return CompileRun.CompileAndRun(true, f, new[] { lparam.ToS() });
					}
					print.warning($"Debugger script not found. See Options -> General -> Debugger script.", -1);
				}
				break;
			}
			return 0;
		case RunningTasks.WM_TASK_ENDED: //WM_USER+900
			App.Tasks.TaskEnded2(wparam, lparam);
			return 0;
		}

		return Api.DefWindowProc(w, message, wparam, lparam);
	}

	static nint _WmCopyData(nint wparam, nint lparam) {
		var c = new WndCopyData(lparam);
		int action = Math2.LoWord(c.DataId), action2 = Math2.HiWord(c.DataId);
		bool isString = action < 100;
		string s = isString ? c.GetString() : null;
		byte[] b = isString ? null : c.GetBytes();
		switch (action) {
		case 1:
			FilesModel.LoadWorkspace(s);
			break;
		case 2:
			App.Model.ImportWorkspace(s);
			break;
		case 3:
			Api.ReplyMessage(1); //avoid 'wait' cursor while we'll show dialog
			App.Model.ImportFiles(s.Split('\0'));
			break;
		case 4:
			Api.ReplyMessage(1);
			if (App.Model.Find(s) is FileNode f1) App.Model.OpenAndGoTo(f1, (int)wparam - 1);
			else print.warning($"File not found: '{s}'.", -1);
			break;
		case 5:
			if (App.Model.Find(s) is FileNode f2) return App.Tasks.EndTasksOf(f2) ? 1 : 2;
			print.warning($"File not found: '{s}'.", -1);
			return 0;
		case 10:
			s = DIcons.GetIconString(s, (EGetIcon)action2);
			return s == null ? 0 : WndCopyData.Return<char>(s, wparam);
		case 100: //run script from script (script.run/runWait)
		case 101: //run script from command line
			return _RunScript();
		case 110: //received from our non-admin drop-target process on OnDragEnter
			return UacDragDrop.AdminProcess.DragEvent((int)wparam, b);
		default:
			Debug.Assert(false);
			return 0;
		}
		return 1;

		nint _RunScript() {
			int mode = (int)wparam; //1 - wait, 3 - wait and get script.writeResult output
			var d = Serializer_.Deserialize(b);
			string file = d[0]; string[] args = d[1]; string pipeName = d[2];
			var f = App.Model?.FindCodeFile(file);
			if (f == null) {
				if (action == 101) print.it($"Command line: script '{file}' not found."); //else the caller script will throw exception
				return (int)script.RunResult_.notFound;
			}
			return CompileRun.CompileAndRun(true, f, args, noDefer: 0 != (mode & 1), wrPipeName: pipeName);
		}
	}

	//Called when command line starts with "/s". This process is running as SYSTEM in session 0.
	//This process is started by the Task Scheduler task installed by the setup program. The task started by App._RestartAsAdmin.
	[MethodImpl(MethodImplOptions.NoOptimization)]
	static unsafe int _RunEditorAsAdmin() {
		var s1 = api.GetCommandLine();
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
			sesId = api.WTSGetActiveConsoleSessionId();
			if (sesId < 1) return 1;
		}
		//_MBox(new string(s2));

		if (!api.WTSQueryUserToken(sesId, out var hToken)) return 2;
		if (api.GetTokenInformation(hToken, Api.TOKEN_INFORMATION_CLASS.TokenLinkedToken, out var hToken2, sizeof(nint), out _)) { //fails if non-admin user or if UAC turned off
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

		if (!api.CreateEnvironmentBlock(out var eb, hToken, false)) return 3;

		var si = new Api.STARTUPINFO { cb = sizeof(Api.STARTUPINFO), dwFlags = Api.STARTF_FORCEOFFFEEDBACK };
		var desktop = stackalloc char[] { 'w', 'i', 'n', 's', 't', 'a', '0', '\\', 'd', 'e', 'f', 'a', 'u', 'l', 't', '\0' }; //"winsta0\\default"
		si.lpDesktop = desktop;

		if (!api.CreateProcessAsUser(hToken, null, s2, null, null, false, Api.CREATE_UNICODE_ENVIRONMENT, eb, null, si, out var pi)) {
			_MBox("CreateProcessAsUserW: " + lastError.message);
			return 4;
		}

		Api.CloseHandle(pi.hThread);
		Api.CloseHandle(pi.hProcess);
		//Api.AllowSetForegroundWindow(pi.dwProcessId); //fails

		api.DestroyEnvironmentBlock(eb);

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
		api.WTSSendMessage(default, api.WTSGetActiveConsoleSessionId(), title, title.Length * 2, s, s.Length * 2, api.MB_TOPMOST | api.MB_SETFOREGROUND, 0, out _, true);
#endif
	}

	/// <summary>
	/// Finds the message-only window. Starts editor if not running. In any case waits for the window max 15 s.
	/// </summary>
	/// <param name="wMsg"></param>
	static bool _EnsureEditorRunningAndGetMsgWindow(out wnd wMsg) {
		wMsg = default;
		for (int i = 0; i < 1000; i++) { //if we started editor process, wait until it fully loaded, then it creates the message-only window
			wMsg = wnd.findFast(null, ScriptEditor.c_msgWndClassName, true);
			if (!wMsg.Is0) return true;
			if (i == 0) {
				var ps = new ProcessStarter_(process.thisExePath, rawExe: true);
				if (!ps.StartL(out var pi)) break;
				Api.AllowSetForegroundWindow(pi.dwProcessId);
				pi.Dispose();
				//note: the process will restart as admin if started from non-admin process, unless the program isn't installed correctly
			}
			Thread.Sleep(15);
		}
		return false;
	}

	//Initially for this was used native exe. Rejected because of AV false positives, including WD.
	//	Speed with native exe 50 ms, now 85 ms. Never mind.
	static unsafe int _LetEditorRunScript(string[] args) {
		if (!_EnsureEditorRunningAndGetMsgWindow(out wnd w)) return (int)script.RunResult_.noEditor;

		//If script name has prefix *, need to wait until script process ends.
		//	Also auto-detect whether need to write script.writeResult to stdout.
		var file = args[0];
		args = args.RemoveAt(0);
		int mode = 0; //1 - wait, 3 - wait and get script.writeResult output
		if (file.Starts('*')) {
			file = file[1..];
			mode |= 1;
			if ((default != Api.GetStdHandle(Api.STD_OUTPUT_HANDLE)) //redirected stdout
				|| api.AttachConsole(api.ATTACH_PARENT_PROCESS) //parent process is console
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

	static unsafe class api {
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
