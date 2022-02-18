//#define USE_WTS

//FUTURE: GetCpuUsage.

namespace Au
{
	/// <summary>
	/// Contains static functions to work with processes (find, enumerate, get basic info, etc), current process (get info), current thread (get info).
	/// </summary>
	/// <seealso cref="run"/>
	/// <seealso cref="script"/>
	/// <seealso cref="Process"/>
	public static unsafe class process
	{
		/// <summary>
		/// Gets process executable file name (like "notepad.exe") or full path.
		/// Returns null if fails.
		/// </summary>
		/// <param name="processId">Process id.</param>
		/// <param name="fullPath">
		/// Get full path.
		/// Note: Fails to get full path if the process belongs to another user session, unless current process is running as administrator; also fails to get full path of some system processes.
		/// </param>
		/// <param name="noSlowAPI">When the fast API QueryFullProcessImageName fails, don't try to use another much slower API WTSEnumerateProcesses. Not used if <i>fullPath</i> is true.</param>
		/// <remarks>
		/// This function is much slower than getting window name or class name.
		/// </remarks>
		/// <seealso cref="wnd.ProgramName"/>
		/// <seealso cref="wnd.ProgramPath"/>
		/// <seealso cref="wnd.ProcessId"/>
		public static string getName(int processId, bool fullPath = false, bool noSlowAPI = false) {
			if (processId == 0) return null;
			string R = null;

			//var t = perf.mcs;
			//if(s_time != 0) print.it(t - s_time);
			//s_time = t;

			using var ph = Handle_.OpenProcess(processId);
			if (!ph.Is0) {
				//In non-admin process fails if the process is of another user session.
				//Also fails for some system processes: nvvsvc, nvxdsync, dwm. For dwm fails even in admin process.

				//getting native path is faster, but it gets like "\Device\HarddiskVolume5\Windows\System32\notepad.exe" and I don't know API to convert to normal
				if (_QueryFullProcessImageName(ph, !fullPath, out var s)) {
					R = s;
					if (pathname.IsPossiblyDos_(R)) {
						if (fullPath || _QueryFullProcessImageName(ph, false, out s)) {
							R = pathname.ExpandDosPath_(s);
							if (!fullPath) R = _GetFileName(R);
						}
					}
				}
			} else if (!noSlowAPI && !fullPath) { //the slow way. Can get only names, not paths.
				using (new _AllProcesses(out var p, out int n)) {
					for (int i = 0; i < n; i++)
						if (p[i].processID == processId) {
							R = p[i].GetName(cannotOpen: true);
							break;
						}
				}
			}

			return R;

			//Would be good to cache process names here. But process id can be reused quickly. Use GetNameCached_ instead.
			//	tested: a process id is reused after creating ~100 processes (and waiting until exits). It takes ~2 s.
			//	The window finder is optimized to call this once for each process and not for each window.
		}

		/// <summary>
		/// Same as GetName, but faster when called several times for same window, like <c>if(w.ProgramName=="A" || w.ProgramName=="B")</c>.
		/// </summary>
		internal static string GetNameCached_(wnd w, int processId, bool fullPath = false) {
			if (processId == 0) return null;
			var cache = _LastWndProps.OfThread;
			cache.Begin(w);
			var R = fullPath ? cache.ProgramPath : cache.ProgramName;
			if (R == null) {
				R = getName(processId, fullPath);
				if (fullPath) cache.ProgramPath = R; else cache.ProgramName = R;
			}
			return R;
		}

		class _LastWndProps
		{
			wnd _w;
			long _time;
			internal string ProgramName, ProgramPath;

			internal void Begin(wnd w) {
				var t = Api.GetTickCount64();
				if (w != _w || t - _time > 300) { _w = w; ProgramName = ProgramPath = null; }
				_time = t;
			}

			[ThreadStatic] static _LastWndProps _ofThread;
			internal static _LastWndProps OfThread => _ofThread ??= new _LastWndProps();
		}

		[SkipLocalsInit]
		static bool _QueryFullProcessImageName(IntPtr hProcess, bool getFilename, out string s) {
			s = null;
			using FastBuffer<char> b = new();
			for (; ; b.More()) {
				int n = b.n;
				if (Api.QueryFullProcessImageName(hProcess, getFilename, b.p, ref n)) {
					s = getFilename ? _GetFileName(b.p, n) : new string(b.p, 0, n);
					return true;
				}
				if (lastError.code != Api.ERROR_INSUFFICIENT_BUFFER) return false;
			}
		}

#if USE_WTS //simple, safe, but ~2 times slower
		struct _AllProcesses :IDisposable
		{
			ProcessInfo_* _p;

			public _AllProcesses(out ProcessInfo_* p, out int count)
			{
				if(WTSEnumerateProcessesW(default, 0, 1, out p, out count)) _p = p; else _p = null;
			}

			public void Dispose()
			{
				if(_p != null) WTSFreeMemory(_p);
			}

			[DllImport("wtsapi32.dll", SetLastError = true)]
			static extern bool WTSEnumerateProcessesW(IntPtr serverHandle, uint reserved, uint version, out ProcessInfo_* ppProcessInfo, out int pCount);

			[DllImport("wtsapi32.dll", SetLastError = false)]
			static extern void WTSFreeMemory(ProcessInfo_* memory);
		}
#else //the .NET Process class uses this. But it creates about 0.4 MB of garbage.
		struct _AllProcesses : IDisposable
		{
			ProcessInfo_* _p;

			public _AllProcesses(out ProcessInfo_* pi, out int count) {
				_p = null;
				Api.SYSTEM_PROCESS_INFORMATION* b = null;
				try {
					for (int na = 500_000; ;) {
						MemoryUtil.FreeAlloc(ref b, na);
						int status = Api.NtQuerySystemInformation(5, b, na, out na);
						//print.it(na); //~300_000, Win10, year 2021
						if (status == 0) break;
						if (status != Api.STATUS_INFO_LENGTH_MISMATCH) throw new AuException(status);
					}

					int nProcesses = 0, nbNames = 0;
					for (var p = b; ; p = (Api.SYSTEM_PROCESS_INFORMATION*)((byte*)p + p->NextEntryOffset)) {
						nProcesses++;
						nbNames += p->NameLength; //bytes, not chars
						if (p->NextEntryOffset == 0) break;
					}
					count = nProcesses;
					_p = (ProcessInfo_*)MemoryUtil.Alloc(nProcesses * sizeof(ProcessInfo_) + nbNames);
					ProcessInfo_* r = _p;
					char* names = (char*)(_p + nProcesses);
					for (var p = b; ; p = (Api.SYSTEM_PROCESS_INFORMATION*)((byte*)p + p->NextEntryOffset), r++) {
						r->processID = (int)p->UniqueProcessId;
						r->sessionID = (int)p->SessionId;
						int len = p->NameLength / 2;
						r->nameLen = len;
						if (len > 0) {
							//copy name to _p memory because it's in the huge buffer that will be released in this func
							r->namePtr = names;
							MemoryUtil.Copy((char*)p->NamePtr, names, len * 2);
							names += len;
						} else r->namePtr = null; //Idle

						if (p->NextEntryOffset == 0) break;
					}
					pi = _p;
				}
				finally { MemoryUtil.Free(b); }
			}

			public void Dispose() {
				MemoryUtil.Free(_p);
			}
		}
#endif

		//Use ProcessInfo_ and ProcessInfo because with WTSEnumerateProcessesW _ProcessName must be IntPtr, and then WTSFreeMemory frees its memory.
		//	AllProcesses() converts ProcessInfo_ to ProcessInfo where Name is string. Almost same speed.
		internal unsafe struct ProcessInfo_
		{
#pragma warning disable 649 //never used
			public int sessionID;
			public int processID;
			public char* namePtr;
			public int nameLen;
#if USE_WTS
			public IntPtr userSid;
#endif
#pragma warning restore 649

			/// <summary>
			/// Gets process executable file name (like "notepad.exe"). Not full path.
			/// If contains looks like a DOS path and !cannotOpen, tries to unexpand DOS path.
			/// Don't call multiple times, because always converts from raw char*.
			/// </summary>
			public string GetName(bool cannotOpen = false) {
				if (namePtr == null) {
					if (processID == 0) return "Idle";
					return null;
				}
				string R = new(namePtr, 0, nameLen);
				if (!cannotOpen && pathname.IsPossiblyDos_(R)) {
					using var ph = Handle_.OpenProcess(processID);
					if (!ph.Is0 && _QueryFullProcessImageName(ph, false, out var s)) {
						R = _GetFileName(pathname.ExpandDosPath_(s));
					}
				}
				return R;
			}
		}

		/// <summary>
		/// Gets basic info of all processes: name, id, session id.
		/// </summary>
		/// <param name="ofThisSession">Get processes only of this user session (skip services etc).</param>
		/// <exception cref="AuException">Failed. Unlikely.</exception>
		public static ProcessInfo[] allProcesses(bool ofThisSession = false) {
			using (new _AllProcesses(out var p, out int n)) {
				if (n == 0) throw new AuException();
				int sessionId = 0, ns = n;
				if (ofThisSession) {
					sessionId = thisProcessSessionId;
					for (int i = 0; i < n; i++) if (p[i].sessionID != sessionId) ns--;
				}
				var a = new ProcessInfo[ns];
				for (int i = 0, j = 0; i < n; i++) {
					if (ofThisSession && p[i].sessionID != sessionId) continue;
					a[j++] = new ProcessInfo(p[i].GetName(), p[i].processID, p[i].sessionID);
				}
				return a;
			}
		}

		/// <summary>
		/// Gets process ids of all processes of the specified program.
		/// Returns array containing 0 or more elements.
		/// </summary>
		/// <param name="processName">
		/// Process executable file name, like "notepad.exe".
		/// String format: [](xref:wildcard_expression).
		/// </param>
		/// <param name="fullPath">
		/// <i>processName</i> is full path.
		/// Note: Fails to get full path if the process belongs to another user session, unless current process is running as administrator; also fails to get full path of some system processes.
		/// </param>
		/// <param name="ofThisSession">Get processes only of this user session.</param>
		/// <exception cref="ArgumentException">
		/// - <i>processName</i> is "" or null.
		/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
		/// </exception>
		public static int[] getProcessIds([ParamString(PSFormat.Wildex)] string processName, bool fullPath = false, bool ofThisSession = false) {
			if (processName.NE()) throw new ArgumentException();
			List<int> a = null;
			GetProcessesByName_(ref a, processName, fullPath, ofThisSession);
			return a?.ToArray() ?? Array.Empty<int>();
		}

		/// <summary>
		/// Gets process id of the first found process of the specified program.
		/// Returns 0 if not found.
		/// More info: <see cref="getProcessIds"/>.
		/// </summary>
		/// <exception cref="ArgumentException"/>
		public static int getProcessId([ParamString(PSFormat.Wildex)] string processName, bool fullPath = false, bool ofThisSession = false) {
			if (processName.NE()) throw new ArgumentException();
			List<int> a = null;
			return GetProcessesByName_(ref a, processName, fullPath, ofThisSession, true);
		}

		internal static int GetProcessesByName_(ref List<int> a, wildex processName, bool fullPath = false, bool ofThisSession = false, bool first = false) {
			a?.Clear();

			int sessionId = ofThisSession ? thisProcessSessionId : 0;

			using (new _AllProcesses(out var p, out int n)) {
				for (int i = 0; i < n; i++) {
					if (ofThisSession && p[i].sessionID != sessionId) continue;
					string s = fullPath ? getName(p[i].processID, true) : p[i].GetName();
					if (s == null) continue;

					if (processName.Match(s)) {
						if (first) return p[i].processID;
						a ??= new List<int>();
						a.Add(p[i].processID);
					}
				}
			}

			return 0;
		}

		static string _GetFileName(char* s, int len) {
			if (s == null) return null;
			char* ss = s + len;
			for (; ss > s; ss--) if (ss[-1] == '\\' || ss[-1] == '/') break;
			return new string(ss, 0, len - (int)(ss - s));
		}

		static string _GetFileName(string s) {
			fixed (char* p = s) return _GetFileName(p, s.Length);
		}

		/// <summary>
		/// Gets version info of process executable file.
		/// Return null if fails.
		/// </summary>
		/// <param name="processId">Process id.</param>
		public static FileVersionInfo getVersionInfo(int processId) {
			var s = getName(processId, true);
			if (s != null) {
				try { return FileVersionInfo.GetVersionInfo(s); } catch { }
			}
			return null;
		}

		/// <summary>
		/// Gets description of process executable file.
		/// Return null if fails.
		/// </summary>
		/// <param name="processId">Process id.</param>
		/// <remarks>
		/// Calls <see cref="getVersionInfo"/> and <see cref="FileVersionInfo.FileDescription"/>.
		/// </remarks>
		public static string getDescription(int processId) => getVersionInfo(processId)?.FileDescription;

		/// <summary>
		/// Gets process id from handle (API <msdn>GetProcessId</msdn>).
		/// Returns 0 if failed. Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="processHandle">Process handle.</param>
		public static int processIdFromHandle(IntPtr processHandle) => Api.GetProcessId(processHandle); //fast

		//public static Process processObjectFromHandle(IntPtr processHandle)
		//{
		//	int pid = GetProcessId(processHandle);
		//	if(pid == 0) return null;
		//	return Process.GetProcessById(pid); //slow, makes much garbage, at first gets all processes just to throw exception if pid not found...
		//}

		/// <summary>
		/// Terminates (ends) the specified process.
		/// Returns false if failed. Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="processId">Process id.</param>
		/// <param name="exitCode">Process exit code.</param>
		/// <remarks>
		/// This function does not try to end process "softly" (close main window). Unsaved data will be lost.
		/// Alternatives: run taskkill.exe or pskill.exe (download). See <see cref="run.console"/>. More info on the internet.
		/// </remarks>
		public static bool terminate(int processId, int exitCode = 0) {
			if (Api.WTSTerminateProcess(default, processId, exitCode)) return true;
			bool invalidParam = lastError.code == Api.ERROR_INVALID_PARAMETER;
			if (!invalidParam) {
				using var hp = Handle_.OpenProcess(processId, Api.PROCESS_TERMINATE);
				if (!hp.Is0) {
					return Api.TerminateProcess(hp, exitCode);
				}
			}
			return false;
		}

		/// <summary>
		/// Terminates (ends) all processes of the specified program or programs.
		/// Returns the number of successfully terminated processes.
		/// </summary>
		/// <param name="processName">
		/// Process executable file name, like "notepad.exe".
		/// String format: [](xref:wildcard_expression).
		/// </param>
		/// <param name="allSessions">Processes of any user session. If false (default), only processes of this user session.</param>
		/// <param name="exitCode">Process exit code.</param>
		/// <exception cref="ArgumentException">
		/// - <i>processName</i> is "" or null.
		/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
		/// </exception>
		public static int terminate(string processName, bool allSessions = false, int exitCode = 0) {
			int n = 0;
			foreach (int pid in getProcessIds(processName, ofThisSession: !allSessions)) {
				if (terminate(pid, exitCode)) n++;
			}
			return n;
		}

		/// <summary>
		/// Suspends or resumes the specified process.
		/// Returns false if failed. Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="suspend">true suspend, false resume.</param>
		/// <param name="processId">Process id.</param>
		/// <remarks>
		/// If suspended multiple times, must be resumed the same number of times.
		/// </remarks>
		public static bool suspend(bool suspend, int processId) {
			using var hp = Handle_.OpenProcess(processId, Api.PROCESS_SUSPEND_RESUME);
			if (!hp.Is0) {
				int status = suspend ? Api.NtSuspendProcess(hp) : Api.NtResumeProcess(hp);
				lastError.code = status;
				return status == 0;
			}
			return false;
		}

		/// <summary>
		/// Suspends or resumes all processes of the specified program or programs.
		/// Returns the number of successfully suspended/resumed processes.
		/// </summary>
		/// <param name="suspend">true suspend, false resume.</param>
		/// <param name="processName">
		/// Process executable file name, like "notepad.exe".
		/// String format: [](xref:wildcard_expression).
		/// </param>
		/// <param name="allSessions">Processes of any user session. If false (default), only processes of this user session.</param>
		/// <exception cref="ArgumentException">
		/// - <i>processName</i> is "" or null.
		/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
		/// </exception>
		/// <remarks>
		/// If suspended multiple times, must be resumed the same number of times.
		/// </remarks>
		public static int suspend(bool suspend, string processName, bool allSessions = false) {
			int n = 0;
			foreach (int pid in getProcessIds(processName, ofThisSession: !allSessions)) {
				if (process.suspend(suspend, pid)) n++;
			}
			return n;
		}

		/// <summary>
		/// Gets user session id of a process (API <msdn>ProcessIdToSessionId</msdn>).
		/// Returns -1 if failed. Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="processId">Process id.</param>
		public static int getSessionId(int processId) {
			if (!Api.ProcessIdToSessionId(processId, out var R)) return -1;
			return R;
		}

		#region this process

		/// <summary>
		/// Gets current process id.
		/// See API <msdn>GetCurrentProcessId</msdn>.
		/// </summary>
		public static int thisProcessId => Api.GetCurrentProcessId();

		/// <summary>
		/// Returns current process handle.
		/// See API <msdn>GetCurrentProcess</msdn>.
		/// Don't need to close the handle.
		/// </summary>
		public static IntPtr thisProcessHandle => Api.GetCurrentProcess();

		//rejected. Too simple and rare.
		///// <summary>
		///// Gets native module handle of the program file of this process.
		///// </summary>
		//public static IntPtr thisExeModuleHandle => Api.GetModuleHandle(null);

		/// <summary>
		/// Gets full path of the program file of this process (API <msdn>GetModuleFileName</msdn>).
		/// </summary>
		[SkipLocalsInit]
		public static unsafe string thisExePath {
			get {
				if (s_exePath == null) {
					var a = stackalloc char[500];
					int n = Api.GetModuleFileName(default, a, 500);
					s_exePath = new string(a, 0, n);
					//documented and tested: can be C:\SHORT~1\NAME~1.exe or \\?\C:\long path\name.exe.
					//tested: AppContext.BaseDirectory gets raw path, like above examples. Used by folders.ThisApp.
					//tested: CreateProcessW supports long paths in lpApplicationName, but my tested apps then crash.
					//tested: ShellExecuteW does not support long paths.
					//tested: Windows Explorer cannot launch exe if long path.
					//tested: When launched with path containing .\, ..\ or /, here we get normalized path.
				}
				return s_exePath;
			}
		}
		static string s_exePath, s_exeName;

		/// <summary>
		/// Gets file name of the program file of this process, like "name.exe".
		/// </summary>
		public static string thisExeName => s_exeName ??= pathname.getName(thisExePath);

		/// <summary>
		/// Gets user session id of this process.
		/// </summary>
		public static int thisProcessSessionId => getSessionId(Api.GetCurrentProcessId());

		/// <summary>
		/// Gets or sets whether <see cref="CultureInfo.DefaultThreadCurrentCulture"/> and <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> are <see cref="CultureInfo.InvariantCulture"/>.
		/// </summary>
		/// <remarks>
		/// If your app doesn't want to use current culture (default in .NET apps), it can set these properties = <see cref="CultureInfo.InvariantCulture"/> or set this property = true.
		/// It prevents potential bugs when app/script/components don't specify invariant culture in string functions and 'number to/from string' functions.
		/// Also, there is a bug in 'number to/from string' functions in some .NET versions with some cultures: they use wrong minus sign, not ASII '-' which is specified in Control Panel.
		/// The default compiler sets this property = true; as well as <see cref="script.setup"/>.
		/// </remarks>
		public static bool thisProcessCultureIsInvariant {
			get {
				var ic = CultureInfo.InvariantCulture;
				return CultureInfo.DefaultThreadCurrentCulture == ic && CultureInfo.DefaultThreadCurrentUICulture == ic;
			}
			set {
				if (value) {
					var ic = CultureInfo.InvariantCulture;
					CultureInfo.DefaultThreadCurrentCulture = ic;
					CultureInfo.DefaultThreadCurrentUICulture = ic;
				} else {
					CultureInfo.DefaultThreadCurrentCulture = null;
					CultureInfo.DefaultThreadCurrentUICulture = null;
				}
			}
		}

		/// <summary>
		/// After afterMS milliseconds invokes GC and calls API SetProcessWorkingSetSize.
		/// </summary>
		internal static void ThisProcessMinimizePhysicalMemory_(int afterMS) {
			Task.Delay(afterMS).ContinueWith(_ => {
				GC.Collect();
				GC.WaitForPendingFinalizers();
				Api.SetProcessWorkingSetSize(Api.GetCurrentProcess(), -1, -1);
			});
		}

		//internal static (long WorkingSet, long PageFile) ThisProcessGetMemoryInfo_()
		//{
		//	Api.PROCESS_MEMORY_COUNTERS m = default; m.cb = sizeof(Api.PROCESS_MEMORY_COUNTERS);
		//	Api.GetProcessMemoryInfo(ProcessHandle, ref m, m.cb);
		//	return ((long)m.WorkingSetSize, (long)m.PagefileUsage);
		//}

		/// <summary>
		/// Before this process exits, either normally or on unhandled exception.
		/// </summary>
		/// <remarks>
		/// The event handler is called on <see cref="AppDomain.ProcessExit"/> (then the parameter is null) and <see cref="AppDomain.UnhandledException"/> (then the parameter is <b>Exception</b>).
		/// </remarks>
		public static event Action<Exception> thisProcessExit {
			add {
				if (!_haveEventExit) {
					lock ("AVCyoRcQCkSl+3W8ZTi5oA") {
						if (!_haveEventExit) {
							var d = AppDomain.CurrentDomain;
							d.ProcessExit += _ThisProcessExit;
							d.UnhandledException += _ThisProcessExit; //because ProcessExit is missing on exception
							_haveEventExit = true;
						}
					}
				}
				_eventExit += value;
			}
			remove {
				_eventExit -= value;
			}
		}
		static Action<Exception> _eventExit;
		static bool _haveEventExit;

		static void _ThisProcessExit(object sender, EventArgs ea) //sender: AppDomain on process exit, null on unhandled exception
		{
			Exception e;
			if (ea is UnhandledExceptionEventArgs u) {
				if (!u.IsTerminating) return; //never seen, but anyway
				e = (Exception)u.ExceptionObject; //probably non-Exception object is impossible in C#
			} else {
				e = script.s_unhandledException;
			}
			var k = _eventExit;
			if (k != null) try { k(e); } catch { }
		}

		#endregion

		#region this thread

		/// <summary>
		/// Gets native thread id of this thread (API <msdn>GetCurrentThreadId</msdn>).
		/// </summary>
		/// <remarks>
		/// It is not the same as <see cref="Thread.ManagedThreadId"/>.
		/// </remarks>
		/// <seealso cref="wnd.ThreadId"/>
		public static int thisThreadId => Api.GetCurrentThreadId();
		//speed: fast, but several times slower than Thread.CurrentThread.ManagedThreadId. Caching in a ThreadStatic variable makes even slower.

		/// <summary>
		/// Returns native thread handle of this thread (API <msdn>GetCurrentThread</msdn>).
		/// </summary>
		public static IntPtr thisThreadHandle => Api.GetCurrentThread();

		/// <summary>
		/// Returns true if this thread has a .NET message loop (winforms or WPF).
		/// </summary>
		/// <param name="isWPF">Has WPF message loop and no winforms message loop.</param>
		/// <seealso cref="wnd.getwnd.threadWindows"/>
		public static bool thisThreadHasMessageLoop(out bool isWPF) {
			//info: we don't call .NET functions directly to avoid loading assemblies.

			isWPF = false;
			int f = AssemblyUtil_.IsLoadedWinformsWpf();
			if (0 != (f & 1) && _HML_Forms()) return true;
			if (0 != (f & 2) && _HML_Wpf()) return isWPF = true;
			return false;
		}

		///
		public static bool thisThreadHasMessageLoop() => thisThreadHasMessageLoop(out _);

		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool _HML_Forms() => System.Windows.Forms.Application.MessageLoop;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool _HML_Wpf() => System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread) != null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void ThisThreadSetComApartment_(ApartmentState state) {
			var t = Thread.CurrentThread;
			t.TrySetApartmentState(ApartmentState.Unknown);
			t.TrySetApartmentState(state);

			//This is undocumented, but works if we set ApartmentState.Unknown at first.
			//With [STAThread] slower, and the process initially used to have +2 threads.
			//Speed when called to set STA at startup: 1.7 ms. If apphost calls OleInitialize, 1.5 ms.
			//tested: OleUninitialize in apphost does not make GetApartmentState return MTA.
		}

		#endregion
	}
}

namespace Au.Types
{
	/// <summary>
	/// Contains process name (like "notepad.exe"), id, name and user session id.
	/// </summary>
	public record struct ProcessInfo(string Name, int Id, int SessionId);
	//use record to auto-implement ==, eg for code like var a=process.allProcesses(); 5.s(); print.it(process.allProcesses().Except(a));
}
