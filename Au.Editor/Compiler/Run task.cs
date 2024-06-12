//#define TEST_STARTUP_SPEED

using Au.Compiler;
using Au.Controls;

//CONSIDER: for ifRunning use mutex. Release mutex as soon as script ends, ie before the process ends (need some time to unload .NET).
//	Maybe then could repeatedly start short tasks more frequently.

static class CompileRun {
	/// <summary>
	/// Compiles and/or executes C# file or its project.
	/// If <paramref name="run"/> is false, returns 1 if compiled, 0 if failed to compile.
	/// Else returns: process id if started now, 0 if failed, (int)script.RunResult_.deferred if scheduled to run later, (int)script.RunResult_.editorThread if runs in editor thread.
	/// </summary>
	/// <param name="run">If true, compiles if need and executes. If false, always compiles and does not execute.</param>
	/// <param name="f">C# file. Does nothing if null or not C# file.</param>
	/// <param name="args">To pass to Main.</param>
	/// <param name="noDefer">Don't schedule to run later.</param>
	/// <param name="wrPipeName">Pipe name for script.writeResult.</param>
	/// <param name="runFromEditor">Starting from the Run button or menu Run command. Can restart etc.</param>
	/// <param name="ifRunning">If not null, overrides meta ifRunning.</param>
	/// <param name="debugAttach">Will attach the debugger.</param>
	/// <remarks>
	/// Saves editor text if need.
	/// Calls <see cref="Compiler.Compile"/>.
	/// Must be always called in the main UI thread (Environment.CurrentManagedThreadId == 1), because calls its file model functions.
	/// </remarks>
	public static int CompileAndRun(bool run, FileNode f, string[] args = null, bool noDefer = false, string wrPipeName = null, bool runFromEditor = false, MCIfRunning? ifRunning = null, Func<int, bool> debugAttach = null) {
#if TEST_STARTUP_SPEED
		args = new string[] { perf.ms.ToString() }; //and in script use this code: print.it(perf.ms-Convert.ToInt64(args[0]));
#endif
		
		App.Model.Save.TextNowIfNeed(onlyText: true);
		App.Model.Save.WorkspaceNowIfNeed(); //because the script may kill editor, eg if runs in editor thread
		
		if (f == null) return 0;
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		
		//can be set to run other script instead.
		//	Useful for library projects. Single files have other alternatives - move to a script project or move code to a script file.
		if (run) {
			var f2 = f.TestScript;
			if (f2 != null) {
				if (!f2.FindProject(out projFolder, out projMain)) f = f2;
				else if (projMain != f) f = projMain;
				else { print.it($"<>The test script {f2.SciLink()} cannot be in the project folder {projFolder.SciLink()}"); return 0; }
			}
		}
		
		if (!f.IsCodeFile) return 0;
		
		bool ok = Compiler.Compile(run ? CCReason.Run : CCReason.CompileAlways, out var r, f, projFolder);
		
		if (run && r.role is MCRole.classFile or MCRole.classLibrary) { //info: if classFile, compiler sets r.role and returns false (does not compile)
			_OnRunClassFile(f, projFolder);
			return 0;
		}
		
		if (!ok) return 0;
		if (!run) return 1;
		
		if (r.role == MCRole.editorExtension) {
			if (debugAttach != null) { print.it("Cannot debug scripts with role editorExtension."); return 0; }
			
			EditorExtension.Run_(r.file, args, handleExceptions: true);
			return (int)script.RunResult_.editorThread;
		}
		
		if (ifRunning != null) r.ifRunning = ifRunning.Value;
		
		return App.Tasks.RunCompiled(f, r, args, noDefer, wrPipeName, runFromEditor: runFromEditor, debugAttach: debugAttach);
	}
	
	public static void RunWpfPreview(FileNode f, Func<CanCompileArgs, bool> canCompile) {
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		
		//If f is a class file, run it as a script.
		//	Ignore its test script. For a library it wouldn't work. For a simple projectless class file usually don't need.
		
		bool ok = Compiler.Compile(CCReason.WpfPreview, out var r, f, projFolder, canCompile: canCompile);
		if (!ok) return;
		
		int pid = App.Tasks.RunCompiled(f, r, new string[] { "WPF_PREVIEW", s_wpfPreview.pid.ToS(), s_wpfPreview.time.ToS() });
		Api.GetSystemTimeAsFileTime(out long time);
		s_wpfPreview = (pid, time);
	}
	static (int pid, long time) s_wpfPreview;
	
	static void _OnRunClassFile(FileNode f, FileNode projFolder) {
		if (!s_isRegisteredLinkRCF) { s_isRegisteredLinkRCF = true; SciTags.AddCommonLinkTag("+runClass", _SciLink_RunClassFile); }
		var ids = f.IdStringWithWorkspace;
		var s2 = projFolder != null || f.Name.Eqi("global.cs") ? "" : $" or project (<+runClass 2|{ids}>create<>). Or <+runClass 1|{ids}>change<> file type or role";
		print.it($"<>Cannot run '{f.Name}'. It is a <help editor/Class files, projects>class file<>. Need a test script (<+runClass 3|{ids}>create<>){s2}.");
	}
	
	static void _SciLink_RunClassFile(string s) {
		int action = s.ToInt(); //1 change role, 2 create Script project, 3 create new test script and set "run" attribute
		var f = App.Model.Find(s[2..]); if (f == null) return;
		if (action == 1) { //change role
			if (!App.Model.SetCurrentFile(f)) return;
			App.Model.Properties();
		} else {
			FileNode f2;
			if (action == 2) { //create project
				if (!_NewItem(out f2, @"New project\@Script")) return;
				f.FileMove(new(f2, FNInsert.After));
			} else { //create test script
				if (!_NewItem(out f2, "Script.cs", "test " + f.Name)) return;
				f.TestScript = f2;
			}
			
			//Creates new item above f or f's project folder.
			bool _NewItem(out FileNode ni, string template, string name = null) {
				bool isProject = f.FindProject(out var target, out _);
				if (!isProject) target = f;
				
				//extract code from /// <example>
				string example = null;
				if (f.GetCurrentText(out var s) && s.RxMatch(@"(?m)^\h*/// *<example>\h*\R((\h*/// ?.*\R)+?)^\h*/// *</example>", 1, out RXGroup g)) {
					if (s.RxMatch(@"(?m)^\h*/// *<code>(<!\[CDATA\[)?\s+((\h*/// ?.*\R)+?)^\h*/// *(?(1)\]\]>)</code>", 2, out g, range: g)) {
						example = g.Value.RxReplace(@"(?m)^\h*/// ?", "");
					}
				}
				
				var text = new NewFileText();
				if (action == 2) {
					text.text = example ?? "//Class1.Function1();\r\n";
				} else {
					text.meta = $"{(isProject ? "pr" : "c")} {f.ItemPath};";
					text.text = example ?? $"//{(isProject ? "Library." : "")}Class1.Function1();\r\n";
				}
				
				ni = App.Model.NewItem(template, new(target, FNInsert.Before), name, text: text);
				return ni != null;
			}
		}
	}
	static bool s_isRegisteredLinkRCF;
}

/// <summary>
/// A running script task.
/// Starts/ends task, watches/notifies when ended.
/// </summary>
class RunningTask : ITreeViewItem {
	volatile WaitHandle _process;
	public readonly FileNode f;
	public readonly int taskId;
	public readonly int processId;
	
	static int s_taskId;
	
	public RunningTask(FileNode f, WaitHandle hProcess, int processId) {
		taskId = ++s_taskId;
		this.f = f;
		_process = hProcess;
		this.processId = processId;
		
		RecentTT.TaskEvent(true, this);
		
		RegisteredWaitHandle rwh = null;
		rwh = ThreadPool.RegisterWaitForSingleObject(_process, (context, wasSignaled) => {
			rwh.Unregister(_process);
			var p = _process; _process = null;
			Api.GetExitCodeProcess(p.SafeWaitHandle.DangerousGetHandle(), out int ec);
			p.Dispose();
			App.Tasks.TaskEnded1(taskId, ec);
		}, null, -1, true);
	}
	
	/// <summary>
	/// False if task is already ended or still not started.
	/// </summary>
	public bool IsRunning => _process is { } p && Api.WaitForSingleObject(p.SafeWaitHandle.DangerousGetHandle(), 0) == Api.WAIT_TIMEOUT;
	
	/// <summary>
	/// Ends this task (kills process), if running.
	/// Returns false if fails, unlikely.
	/// </summary>
	/// <param name="onProgramExit">Called on program exit. Returns true even if fails. Does not wait.</param>
	public bool End(bool onProgramExit) {
		if (_process is { } p) {
			var h = p.SafeWaitHandle.DangerousGetHandle();
			
			int pid = process.processIdFromHandle(h);
			if (pid != 0) {
				if (Panels.Debug?.EndIfDebugging(pid) == true) return true;
				
				//let it call Environment.Exit. It removes tray icons etc.
				var w1 = wnd.findFast(pid.ToS(), script.c_auxWndClassName, messageOnly: true);
				if (!w1.Is0 && w1.Post(Api.WM_CLOSE)) {
					if (0 == Api.WaitForSingleObject(h, 1000)) return true;
				}
			}
			
			if (_process != null) {
				bool ok = Api.TerminateProcess(h, -1);
				if (onProgramExit) return true;
				if (ok) {
					//TerminateProcess is async. Usually the process ends after several ms.
					if (Api.WaitForSingleObject(h, 500) == Api.WAIT_TIMEOUT) Debug_.Print("process not terminated");
				} else {
					//ERROR_ACCESS_DENIED when the process is ending
					var s = lastError.message;
					if (Api.WaitForSingleObject(h, 500) == Api.WAIT_TIMEOUT) { Debug_.Print(s); return false; }
				}
			}
		}
		return true;
		//TODO3: release pressed keys.
	}
	
	#region ITreeViewItem
	
	string ITreeViewItem.DisplayText => f.DisplayName;
	
	object ITreeViewItem.Image => f.Image;
	
	//TVCheck ITreeViewItem.CheckState { get; }
	
	//bool ITreeViewItem.IsDisabled { get; }
	
	//bool ITreeViewItem.IsBold { get; }
	
	//bool ITreeViewItem.IsSelectable { get; }
	
	//int ITreeViewItem.Color { get; }
	
	//int ITreeViewItem.TextColor => 0xff0000;
	
	#endregion
}

/// <summary>
/// Manages running script tasks.
/// </summary>
class RunningTasks {
	class _WaitingTask {
		public readonly FileNode f;
		public readonly Compiler.CompResults r;
		public readonly string[] args;
		
		public _WaitingTask(FileNode f, Compiler.CompResults r, string[] args) {
			this.f = f; this.r = r; this.args = args;
		}
	}
	
	readonly List<RunningTask> _a = new();
	readonly List<_WaitingTask> _q = new(); //not Queue because may need to remove item at any index
	bool _updateUI;
	volatile bool _disposed;
	
	/// <summary>
	/// Gets running tasks.
	/// Order: the last started is the first in the list.
	/// </summary>
	public IReadOnlyList<RunningTask> Items => _a;
	
	public RunningTasks() {
		App.Timer1sOr025s += _TimerUpdateUI;
	}
	
	public void OnWorkspaceClosed() {
		bool onExit = App.Loaded >= AppState.Unloading;
		
		if (onExit) {
			_disposed = true;
			App.Timer1sOr025s -= _TimerUpdateUI;
		}
		
		for (int i = _a.Count; --i >= 0;) {
			_EndTask(_a[i], onExit: onExit);
		}
		
		if (onExit) _a.Clear();
		_q.Clear();
		
		if (!onExit) _UpdatePanels();
	}
	
	/// <summary>
	/// Adds a started task to the 'running' list.
	/// Must be called in the main thread.
	/// </summary>
	/// <param name="rt"></param>
	void _Add(RunningTask rt) {
		Debug.Assert(!_disposed);
		_a.Insert(0, rt);
		_updateUI = true;
	}
	
	/// <summary>
	/// Called in a threadpool thread when a task process exited.
	/// </summary>
	/// <param name="taskId"></param>
	internal void TaskEnded1(int taskId, int exitCode) {
		if (_disposed) return;
		CommandLine.MsgWnd.Post(WM_TASK_ENDED, taskId, exitCode);
	}
	
	/// <summary>
	/// When task ended, this message is posted to CommandLine.MsgWnd, with wParam=taskId.
	/// </summary>
	public const int WM_TASK_ENDED = Api.WM_USER + 900;
	
	/// <summary>
	/// Removes an ended task from the 'running' list. If a task is queued and can run, starts it.
	/// When task ended, TaskEnded1 posts message WM_TASK_ENDED with task id in wParam to the message window, which calls this function.
	/// </summary>
	internal void TaskEnded2(nint wParam, nint lParam) {
		if (_disposed) return;
		
		int taskId = (int)wParam;
		int i = _Find(taskId);
		if (i < 0) { Debug_.Print("not found. It's OK, but should be very rare, mostly with 1-core CPU."); return; }
		
		RecentTT.TaskEvent(false, _a[i], (int)lParam);
		_a.RemoveAt(i);
		
		for (int j = _q.Count; --j >= 0;) {
			var t = _q[j];
			if (_CanRunNow(t.f, t.r, out _)) {
				_q.RemoveAt(j);
				RunCompiled(t.f, t.r, t.args, ignoreLimits: true);
				break;
			}
		}
		
		_updateUI = true;
	}
	
	void _TimerUpdateUI() {
		if (!_updateUI) return;
		if (!App.Hmain.IsVisible) return;
		_UpdatePanels();
	}
	
	void _UpdatePanels() {
		_updateUI = false;
		Panels.Tasks.UpdateList();
	}
	
	/// <summary>
	/// Returns true if one or more tasks of file f are running.
	/// </summary>
	/// <param name="f">Can be null.</param>
	public bool IsRunning(FileNode f) => null != _GetRunning(f);
	
	RunningTask _GetRunning(FileNode f) {
		for (int i = 0; i < _a.Count; i++) {
			var r = _a[i];
			if (r.f == f && r.IsRunning) return r;
		}
		return null;
	}
	
	//currently not used
	///// <summary>
	///// Returns all running files.
	///// For files that have multiple tasks is added 1 item in the list.
	///// Each time creates new list; caller can modify it.
	///// </summary>
	//public List<FileNode> GetRunningFiles()
	//{
	//	var a = new List<FileNode>(_a.Count);
	//	for(int i = 0; i < _a.Count; i++) {
	//		var t = _a[i];
	//		if(!a.Contains(t.f)) a.Add(t.f);
	//	}
	//	return a;
	//}
	
	public FileNode FileFromProcessId(int processId) {
		foreach (var v in _a) if (v.processId == processId) return v.f;
		return null;
	}
	
	/// <summary>
	/// Ends all tasks of file f.
	/// Returns true if was running.
	/// </summary>
	/// <param name="f">Can be null.</param>
	public bool EndTasksOf(FileNode f) {
		bool wasRunning = false;
		for (int i = _a.Count; --i >= 0;) {
			var r = _a[i];
			if (r.f != f || !r.IsRunning) continue;
			_EndTask(r);
			wasRunning = true;
		}
		return wasRunning;
	}
	
	/// <summary>
	/// Ends a task, if still running.
	/// </summary>
	public void EndTask(RunningTask rt) {
		if (_a.Contains(rt)) _EndTask(rt);
	}
	
	bool _EndTask(RunningTask rt, bool onExit = false) {
		Debug.Assert(_a.Contains(rt));
		return rt.End(onExit);
	}
	
	bool _CanRunNow(FileNode f, Compiler.CompResults r, out RunningTask running, bool runFromEditor = false) {
		running = null;
		if (r.ifRunning == MCIfRunning.run || (r.ifRunning == MCIfRunning.run_restart && !runFromEditor)) return true;
		running = _GetRunning(f);
		return running == null;
	}
	
	//rejected: use shared memory instead of pipe. Tested, same speed.
	
	/// <summary>
	/// Executes the compiled assembly in new process.
	/// Returns: process id if started now, 0 if failed, (int)script.RunResult_.deferred if scheduled to run later.
	/// </summary>
	/// <param name="f"></param>
	/// <param name="r"></param>
	/// <param name="args"></param>
	/// <param name="noDefer">Don't schedule to run later. If cannot run now, just return 0.</param>
	/// <param name="wrPipeName">Pipe name for script.writeResult.</param>
	/// <param name="ignoreLimits">Don't check whether the task can run now.</param>
	/// <param name="runFromEditor">Starting from the Run button or menu Run command. Can restart etc.</param>
	/// <param name="debugAttach">Will attach the debugger.</param>
	public unsafe int RunCompiled(FileNode f, Compiler.CompResults r, string[] args,
		bool noDefer = false, string wrPipeName = null, bool ignoreLimits = false, bool runFromEditor = false, Func<int, bool> debugAttach = null) {
		
		g1:
		if (!ignoreLimits && !_CanRunNow(f, r, out var running, runFromEditor)) {
			var ifRunning = r.ifRunning;
			if (!ifRunning.Has(MCIfRunning._norestartFlag) && ifRunning != MCIfRunning.restart) {
				if (runFromEditor) ifRunning = MCIfRunning.restart;
				else if (ifRunning == MCIfRunning.end_restart) ifRunning = MCIfRunning.end;
				else ifRunning |= MCIfRunning._norestartFlag;
			}
			//print.it(same, ifRunning);
			switch (ifRunning) {
			case MCIfRunning.cancel:
				break;
			case MCIfRunning.wait when !noDefer:
				_q.Insert(0, new _WaitingTask(f, r, args));
				return (int)script.RunResult_.deferred;
			case MCIfRunning.restart when _EndTask(running):
				goto g1;
			case MCIfRunning.end:
				_EndTask(running);
				break;
			default: //warn
				print.it($"<>Cannot start {f.SciLink()} because it is running. You may want to <+properties {f.IdStringWithWorkspace}>change<> <c green>ifRunning<>.");
				break;
			}
			return 0;
		}
		
		_SpUac uac = _SpUac.normal; int preIndex = 0;
		if (!uacInfo.isUacDisabled) {
			//info: to completely disable UAC on Win7: gpedit.msc/Computer configuration/Windows settings/Security settings/Local policies/Security options/User Account Control:Run all administrators in Admin Approval Mode/Disabled. Reboot.
			//note: when UAC disabled, if our uac is System, IsUacDisabled returns false (we probably run as SYSTEM user). It's OK.
			var IL = uacInfo.ofThisProcess.IntegrityLevel;
			if (r.uac == MCUac.inherit) {
				switch (IL) {
				case UacIL.High: preIndex = 1; break;
				case UacIL.UIAccess: uac = _SpUac.uiAccess; preIndex = 2; break;
				}
			} else {
				switch (IL) {
				case UacIL.Medium:
				case UacIL.UIAccess:
					if (r.uac == MCUac.admin) uac = _SpUac.elevate;
					break;
				case UacIL.High:
					if (r.uac == MCUac.user) uac = _SpUac.userFromAdmin;
					break;
				case UacIL.Low:
				case UacIL.Untrusted:
				case UacIL.Unknown:
				//break;
				case UacIL.System:
				case UacIL.Protected:
					print.it($"<>Cannot run {f.SciLink()}. Meta comment option <c green>uac {r.uac}<> cannot be used when the UAC integrity level of this process is {IL}. Supported levels are Medium, High and uiAccess.");
					return 0;
					//info: cannot start Medium IL process from System process. Would need another function. Never mind.
				}
				if (r.uac == MCUac.admin) preIndex = 1;
			}
		}
		
		bool exeProgram = r.notInCache, usePreloaded = false, debugger = debugAttach != null;
		_Preloaded pre = null;
		string exeFile, argsString;
		
		//rejected: 32-bit miniProgram. The task exe has been removed because of AV false positives. And rarely used. Can use exeProgram instead.
		//	osVersion.is32BitOS - editor does not run on 32-bit OS. And never will.
		//bool bit32 = r.bit32 || osVersion.is32BitOS;
		
		if (exeProgram) { //meta role exeProgram
			exeFile = Compiler.DllNameToAppHostExeName(r.file, r.bit32);
			argsString = args == null ? null : StringUtil.CommandLineFromArray(args);
		} else {
			if (debugger) {
				if (uac == _SpUac.elevate) { print.it("Cannot debug this script. Remove /*/ uac admin; /*/, or run LA as admin."); return 0; }
				//Don't use preloaded. It would load some assemblies before 'attach debugger' and then debugger cannot disable JIT optimizations.
			} else if (usePreloaded = r.flags.Has(MiniProgram_.MPFlags.Preloaded) && uac != _SpUac.elevate) {
				//if (bit32 && !osVersion.is32BitOS) preIndex += 3;
				pre = s_preloaded[preIndex] ??= new _Preloaded(preIndex);
			}
			pre ??= new _Preloaded(100); //temporary, just to create pipe with unique name and reuse preloaded launch code
			
			//exeFile = folders.ThisAppBS + (bit32 ? "Au.Task32.exe" : "Au.Task.exe");
			exeFile = folders.ThisAppBS + "Au.Task.exe";
			argsString = pre.pipeName;
		}
		
		int pid = 0; WaitHandle hProcess = null; bool disconnectPipe = false;
		try {
			bool preloadedProcessExists = false;
			if (usePreloaded) {
				var pp = pre.hProcess;
				preloadedProcessExists = pp != null && Api.WAIT_TIMEOUT == Api.WaitForSingleObject(pp.SafeWaitHandle.DangerousGetHandle(), 0);
				if (preloadedProcessExists) {
					hProcess = pp; pid = pre.pid;
					pre.hProcess = null; pre.pid = 0;
				} else if (pp != null) { //preloaded process existed but somehow ended
					pp.Dispose();
					pre.hProcess = null; pre.pid = 0;
				}
			}
			if (!preloadedProcessExists) {
				(pid, hProcess) = _StartProcess(uac, exeFile, argsString, wrPipeName, r.notInCache, runFromEditor, f.Id, debugAttach);
				if (pid == 0) return 0; //failed to start debugging
			}
			Api.AllowSetForegroundWindow(pid);
			
			if (!exeProgram) {
				var f1 = r.flags;
				if (runFromEditor) f1 |= MiniProgram_.MPFlags.FromEditor;
				if (App.IsPortable) f1 |= MiniProgram_.MPFlags.IsPortable;
				if (!preloadedProcessExists) f1 &= ~MiniProgram_.MPFlags.Preloaded;
				byte[] taskParams = Serializer_.SerializeWithSize(r.name, r.file, (int)f1, args, wrPipeName, (string)folders.Workspace, (int)f.Id, process.thisProcessId, (int)CommandLine.MsgWnd);
				
				var o = new Api.OVERLAPPED { hEvent = pre.overlappedEvent };
				if (!Api.ConnectNamedPipe(pre.hPipe, &o)) {
					int e = lastError.code;
					if (e != Api.ERROR_PIPE_CONNECTED) {
						if (e != Api.ERROR_IO_PENDING) throw new AuException(e);
						var ha = stackalloc IntPtr[2] { pre.overlappedEvent, hProcess.SafeWaitHandle.DangerousGetHandle() };
						//perf.shared.Next('r');
						int wr = Api.WaitForMultipleObjectsEx(2, ha, false, -1, false);
						if (wr != 0) { Api.CancelIo(pre.hPipe); throw new AuException("*start task. Preloaded task process ended"); } //note: if fails when 32-bit process, rebuild solution with platform x86
						disconnectPipe = true;
						if (!Api.GetOverlappedResult(pre.hPipe, ref o, out _, false)) throw new AuException(0);
					}
				}
				
				if (!Api.WriteFile2(pre.hPipe, taskParams, out _)) throw new AuException(0);
				Api.DisconnectNamedPipe(pre.hPipe); disconnectPipe = false;
				
				//start preloaded process for next task. Let it wait for pipe connection.
				if (usePreloaded) {
					try { (pre.pid, pre.hProcess) = _StartProcess(uac, exeFile, argsString, null, r.notInCache, false, 0, null); }
					catch (Exception ex) { Debug_.Print(ex); }
				}
			}
		}
		catch (Exception ex) {
			print.it(ex);
			if (disconnectPipe) Api.DisconnectNamedPipe(pre.hPipe);
			hProcess?.Dispose();
			return 0;
		}
		finally {
			if (!usePreloaded) pre?.Dispose();
		}
		
		var rt = new RunningTask(f, hProcess, pid);
		_Add(rt);
		return pid;
	}
	
	class _Preloaded {
		public readonly string pipeName;
		public readonly Handle_ hPipe;
		public readonly Handle_ overlappedEvent;
		public WaitHandle hProcess;
		public int pid;
		
		public _Preloaded(int index) {
			pipeName = $@"\\.\pipe\Au.Task-{Api.GetCurrentProcessId()}-{index}";
			hPipe = Api.CreateNamedPipe(pipeName,
				Api.PIPE_ACCESS_OUTBOUND | Api.FILE_FLAG_OVERLAPPED, //use async pipe because editor would hang if task process exited without connecting. Same speed.
				Api.PIPE_TYPE_MESSAGE | Api.PIPE_REJECT_REMOTE_CLIENTS,
				1, 0, 0, 0, null);
			overlappedEvent = Api.CreateEvent(false);
		}
		
		public void Dispose() {
			hPipe.Dispose();
			overlappedEvent.Dispose();
		}
	}
	//static _Preloaded[] s_preloaded = new _Preloaded[6]; //user, admin, uiAccess, user32, admin32, uiAccess32
	static _Preloaded[] s_preloaded = new _Preloaded[3]; //user, admin, uiAccess
	
	/// <summary>
	/// How _StartProcess must start process.
	/// Note: it is not UAC IL of the process.
	/// </summary>
	enum _SpUac {
		normal, //start process of same IL as this process, but without uiAccess. It is how CreateProcess API works.
		elevate, //start admin process from this user or uiAccess process
		userFromAdmin, //start user process from this admin process
		uiAccess, //start uiAccess process from this uiAccess process
	}
	
	/// <summary>
	/// Starts task process.
	/// Returns (processId, processHandle). Throws if failed.
	/// </summary>
	static unsafe (int pid, WaitHandle hProcess) _StartProcess(_SpUac uac, string exeFile, string args, string wrPipeName, bool exeProgram, bool runFromEditor, uint idMain, Func<int, bool> debugAttach) {
		(int pid, WaitHandle hProcess) r;
		string cwd;
		
		if (exeProgram) {
			if (s_cwdExe == null) {
				//Pass this working directory to let the exe know it was launched from editor.
				//	Then its AppModuleInit_ will read from shared memory and set the event.
				//	Also AppHost uses this to find portable .NET runtime.
				//There are no other ways to pass data when using shellexecute(runas) when cannot use command line args.
				//This empty hidden directory is created by the setup program. This code creates it if missing.
				cwd = folders.ThisApp.Path + "\\Roslyn\\.exeProgram";
				if (!filesystem.exists(cwd).Directory) {
					filesystem.createDirectory(cwd);
					File.SetAttributes(cwd, FileAttributes.Directory | FileAttributes.Hidden);
				}
				s_cwdExe = cwd;
				s_event1 = Api.CreateEvent2(default, true, false, "Au.event.exeProgram.1");
			} else cwd = s_cwdExe;
			
			var p = &SharedMemory_.Ptr->script;
			p->pidEditor = process.thisProcessId;
			p->hwndMsg = (int)CommandLine.MsgWnd;
			p->idMainFile = idMain;
			int flags = 0;
			if (runFromEditor) flags |= 2;
			if (App.IsPortable) flags |= 4;
			if (wrPipeName != null) { flags |= 8; p->pipe = wrPipeName; }
			p->flags = flags;
			p->workspace = folders.Workspace;
		} else cwd = folders.ThisApp;
		
		if (uac == _SpUac.elevate) {
			var k = run.it(exeFile, args, RFlags.Admin | RFlags.NeedProcessHandle, cwd);
			r = (k.ProcessId, k.ProcessHandle);
			//note: don't try to start task without UAC consent. It is not secure.
			//	Normally editor runs as admin in admin user account, and don't need to go through this.
		} else {
			var ps = new ProcessStarter_(exeFile, args, cwd, rawExe: true);
			
			if (debugAttach != null) ps.si.dwXCountChars = 1703529821; //let the process wait for "debugger attached" event
			
			var need = ProcessStarter_.Result.Need.WaitHandle;
			var psr = uac == _SpUac.userFromAdmin
				? ps.StartUserIL(need)
				: ps.Start(need, inheritUiaccess: uac == _SpUac.uiAccess);
			r = (psr.pid, psr.waitHandle);
			
			if (debugAttach != null) {
				if (!debugAttach(psr.pid)) {
					Api.TerminateProcess(psr.waitHandle.SafeWaitHandle.DangerousGetHandle(), 0);
					psr.waitHandle.Dispose();
					return default;
				}
			}
		}
		
		if (exeProgram) {
			IntPtr* ha = stackalloc IntPtr[2] { s_event1, r.hProcess.SafeWaitHandle.DangerousGetHandle() };
			Api.WaitForMultipleObjectsEx(2, ha, false, -1, false);
			Api.ResetEvent(s_event1);
		}
		
		return r;
	}
	static IntPtr s_event1;
	static string s_cwdExe;
	
	int _Find(int taskId) {
		for (int i = 0; i < _a.Count; i++) {
			if (_a[i].taskId == taskId) return i;
		}
		return -1;
	}
	
	public void OnWM_DISPLAYCHANGE() {
		//End preloaded processes. Else they may use wrong DPI.
		foreach (var v in s_preloaded) {
			if (v?.hProcess == null) continue;
			if (!Api.TerminateProcess(v.hProcess.SafeWaitHandle.DangerousGetHandle(), 0)) return;
			v.hProcess.Dispose();
			v.hProcess = null;
			v.pid = 0;
		}
	}
}
