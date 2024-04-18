//#define USE_WTS

//FUTURE: GetCpuUsage.

namespace Au {
	/// <summary>
	/// Process functions. Find, enumerate, get basic info, terminate, triggers, etc. Also includes properties and events of current process and thread.
	/// </summary>
	/// <seealso cref="run"/>
	/// <seealso cref="script"/>
	/// <seealso cref="Process"/>
	public static unsafe class process {
		/// <summary>
		/// Gets process executable file name (like <c>"notepad.exe"</c>) or full path.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="processId">Process id.</param>
		/// <param name="fullPath">
		/// Get full path.
		/// Note: Fails to get full path if the process belongs to another user session, unless current process is running as administrator; also fails to get full path of some system processes.
		/// </param>
		/// <param name="noSlowAPI">When the fast API <msdn>QueryFullProcessImageName</msdn> fails, don't try to use another much slower API <msdn>WTSEnumerateProcesses</msdn>. Not used if <i>fullPath</i> is <c>true</c>.</param>
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
			} else if (!noSlowAPI && !fullPath) {
				//the slow way. Can get only names, not paths.
				using (var p = new AllProcesses_(false)) {
					for (int i = 0; i < p.Count; i++)
						if (p.Id(i) == processId) {
							R = p.Name(i, cannotOpen: true);
							break;
						}
				}
				//TEST: NtQueryInformationProcess, like in getCommandLine.
			}
			
			return R;
			
			//Would be good to cache process names here. But process id can be reused quickly. Use GetNameCached_ instead.
			//	tested: a process id is reused after creating ~100 processes (and waiting until exits). It takes ~2 s.
			//	The window finder is optimized to call this once for each process and not for each window.
		}
		
		/// <summary>
		/// Same as <b>GetName</b>, but faster when called several times for same window, like <c>if(w.ProgramName=="A" || w.ProgramName=="B")</c>.
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
		
		class _LastWndProps {
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
#else //the .NET Process class uses this. But it creates about 0.4 MB of garbage (last time tested was 0.2 MB).
		internal unsafe struct AllProcesses_ : IDisposable {
			readonly _ProcessInfo* _p;
			readonly int _count;
			static int s_bufferSize = 500_000;
			
			public AllProcesses_(bool ofThisSession) {
				int sessionId = ofThisSession ? thisProcessSessionId : 0;
				Api.SYSTEM_PROCESS_INFORMATION* b = null;
				try {
					for (int na = s_bufferSize; ;) {
						MemoryUtil.FreeAlloc(ref b, na);
						int status = Api.NtQuerySystemInformation(5, b, na, out na);
						//print.it(na); //~300_000, Win10, year 2021
						if (status == 0) { s_bufferSize = na + 100_000; break; }
						if (status != Api.STATUS_INFO_LENGTH_MISMATCH) throw new AuException(status);
					}
					
					int nProcesses = 0, nbNames = 0;
					for (var p = b; ; p = (Api.SYSTEM_PROCESS_INFORMATION*)((byte*)p + p->NextEntryOffset)) {
						if (!ofThisSession || p->SessionId == sessionId) {
							nProcesses++;
							nbNames += p->NameLength; //bytes, not chars
						}
						if (p->NextEntryOffset == 0) break;
					}
					_count = nProcesses;
					_p = (_ProcessInfo*)MemoryUtil.Alloc(nProcesses * sizeof(_ProcessInfo) + nbNames);
					_ProcessInfo* r = _p;
					char* names = (char*)(_p + nProcesses);
					for (var p = b; ; p = (Api.SYSTEM_PROCESS_INFORMATION*)((byte*)p + p->NextEntryOffset)) {
						if (!ofThisSession || p->SessionId == sessionId) {
							r->processID = (int)p->UniqueProcessId;
							r->sessionID = (int)p->SessionId;
							int len = p->NameLength / 2;
							r->nameLen = len;
							if (len > 0) {
								//copy name to _p memory because it's in the huge buffer that will be released in this func
								r->nameOffset = (int)(names - (char*)_p);
								MemoryUtil.Copy((char*)p->NamePtr, names, len * 2);
								names += len;
							} else r->nameOffset = 0; //Idle
							r++;
						}
						if (p->NextEntryOffset == 0) break;
					}
				}
				finally { MemoryUtil.Free(b); }
			}
			
			public void Dispose() {
				MemoryUtil.Free(_p);
			}
			
			public int Count => _count;
			
			//public ProcessInfo this[int i] => new(ProcessName(i), _p[i].processID, _p[i].sessionID); //rejected, could be used where shouldn't, making code slower etc
			public ProcessInfo Info(int i) => new(Name(i), _p[i].processID, _p[i].sessionID);
			
			public int Id(int i) => (uint)i < _count ? _p[i].processID : throw new IndexOutOfRangeException();
			
			public int SessionId(int i) => (uint)i < _count ? _p[i].sessionID : throw new IndexOutOfRangeException();
			
			public string Name(int i, bool cannotOpen = false) => (uint)i < _count ? _p[i].GetName(_p, cannotOpen) : throw new IndexOutOfRangeException();
			
			struct _ProcessInfo {
				public int sessionID;
				public int processID;
				public int nameOffset;
				public int nameLen;
				
				public string GetName(void* p, bool cannotOpen) {
					if (nameOffset == 0) {
						if (processID == 0) return "Idle";
						return null;
					}
					string R = new((char*)p + nameOffset, 0, nameLen);
					if (!cannotOpen && pathname.IsPossiblyDos_(R)) {
						using var ph = Handle_.OpenProcess(processID);
						if (!ph.Is0 && _QueryFullProcessImageName(ph, false, out var s)) {
							R = _GetFileName(pathname.ExpandDosPath_(s));
						}
					}
					return R;
				}
			}
		}
#endif
		
		/// <summary>
		/// Gets basic info of all processes: name, id, session id.
		/// </summary>
		/// <param name="ofThisSession">Get processes only of this user session (skip services etc).</param>
		/// <exception cref="AuException">Failed. Unlikely.</exception>
		public static ProcessInfo[] allProcesses(bool ofThisSession = false) {
			using (var p = new AllProcesses_(ofThisSession)) {
				var a = new ProcessInfo[p.Count];
				for (int i = 0; i < a.Length; i++) a[i] = p.Info(i);
				return a;
			}
		}
		
		/// <summary>
		/// Gets process ids of all processes of the specified program.
		/// </summary>
		/// <returns>Array containing zero or more elements.</returns>
		/// <param name="processName">
		/// Process executable file name, like <c>"notepad.exe"</c>.
		/// String format: [wildcard expression](xref:wildcard_expression).
		/// </param>
		/// <param name="fullPath">
		/// <i>processName</i> is full path.
		/// If <c>null</c>, calls <see cref="pathname.isFullPathExpand"/>.
		/// Note: Fails to get full path if the process belongs to another user session, unless current process is running as administrator; also fails to get full path of some system processes.
		/// </param>
		/// <param name="ofThisSession">Get processes only of this user session.</param>
		/// <exception cref="ArgumentException">
		/// - <i>processName</i> is <c>""</c> or <c>null</c>.
		/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
		/// </exception>
		public static int[] getProcessIds([ParamString(PSFormat.Wildex)] string processName, bool? fullPath = false, bool ofThisSession = false) {
			List<int> a = null;
			bool fp = _NameOrPath(ref processName, fullPath);
			GetProcessesByName_(ref a, processName, fp, ofThisSession);
			return a?.ToArray() ?? Array.Empty<int>();
		}
		
		static bool _NameOrPath(ref string processName, bool? fullPath) {
			if (processName.NE()) throw new ArgumentException();
			return fullPath switch {
				false => false,
				true => pathname.isFullPathExpand(ref processName, strict: false) | true,
				_ => pathname.isFullPathExpand(ref processName, strict: false)
			};
		}
		
		/// <summary>
		/// Gets process id of the first found process of the specified program.
		/// </summary>
		/// <returns>0 if not found.</returns>
		/// <inheritdoc cref="getProcessIds"/>
		public static int getProcessId([ParamString(PSFormat.Wildex)] string processName, bool? fullPath = false, bool ofThisSession = false) {
			List<int> a = null;
			bool fp = _NameOrPath(ref processName, fullPath);
			return GetProcessesByName_(ref a, processName, fp, ofThisSession, true);
		}
		
		/// <summary>
		/// Returns <c>true</c> if a process of the specified program is running.
		/// </summary>
		/// <inheritdoc cref="getProcessIds"/>
		public static bool exists([ParamString(PSFormat.Wildex)] string processName, bool? fullPath = false, bool ofThisSession = false)
			=> 0 != getProcessId(processName, fullPath, ofThisSession);
		
		internal static int GetProcessesByName_(ref List<int> a, wildex processName, bool fullPath = false, bool ofThisSession = false, bool first = false) {
			a?.Clear();
			using (var p = new AllProcesses_(ofThisSession)) {
				for (int i = 0; i < p.Count; i++) {
					string s = fullPath ? getName(p.Id(i), true) : p.Name(i);
					if (s != null && processName.Match(s)) {
						int pid = p.Id(i);
						if (first) return pid;
						a ??= new List<int>();
						a.Add(pid);
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
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
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
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="processId">Process id.</param>
		/// <remarks>
		/// Calls <see cref="getVersionInfo"/> and <see cref="FileVersionInfo.FileDescription"/>.
		/// </remarks>
		public static string getDescription(int processId) => getVersionInfo(processId)?.FileDescription;
		
		/// <summary>
		/// Gets process id from handle (API <msdn>GetProcessId</msdn>).
		/// </summary>
		/// <returns>0 if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="processHandle">Process handle.</param>
		public static int processIdFromHandle(IntPtr processHandle) => Api.GetProcessId(processHandle); //fast
		
		//public static Process processObjectFromHandle(IntPtr processHandle)
		//{
		//	int pid = GetProcessId(processHandle);
		//	if(pid == 0) return null;
		//	return Process.GetProcessById(pid); //slow, makes much garbage, at first gets all processes just to throw exception if pid not found...
		//}
		
		/// <summary>
		/// Gets user session id of a process (API <msdn>ProcessIdToSessionId</msdn>).
		/// </summary>
		/// <returns>Returns -1 if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="processId">Process id.</param>
		public static int getSessionId(int processId) {
			if (!Api.ProcessIdToSessionId(processId, out var R)) return -1;
			return R;
		}
		
		/// <summary>
		/// Gets process creation and execution times (API <msdn>GetProcessTimes</msdn>).
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="processId">Process id.</param>
		/// <param name="created">Creation time. As absolute <msdn>FILETIME</msdn>, UTC. If you need <b>DateTime</b>, use <see cref="DateTime.FromFileTimeUtc"/>.</param>
		/// <param name="executed">Amount of time spent executing code (using CPU). As <msdn>FILETIME</msdn>. If you need <b>TimeSpan</b>, use <see cref="TimeSpan.FromTicks"/>.</param>
		public static bool getTimes(int processId, out long created, out long executed) {
			created = 0; executed = 0;
			using var ph = Handle_.OpenProcess(processId);
			if (ph.Is0 || !Api.GetProcessTimes(ph, out created, out _, out long tk, out long tu)) return false;
			executed = tk + tu;
			return true;
		}
		
		/// <summary>
		/// Returns <c>true</c> if the process is 32-bit, <c>false</c> if 64-bit.
		/// Also returns <c>false</c> if failed. Supports <see cref="lastError"/>.
		/// </summary>
		/// <remarks>
		/// <note>If you know it is current process, instead use <see cref="osVersion"/> functions or <c>IntPtr.Size==4</c>. This function is much slower.</note>
		/// </remarks>
		public static bool is32Bit(int processId) {
			bool is32bit = osVersion.is32BitOS;
			if (!is32bit) {
				using var ph = Handle_.OpenProcess(processId);
				if (ph.Is0 || !Api.IsWow64Process(ph, out is32bit)) return false;
			}
			lastError.clear();
			return is32bit;
			
			//info: don't use Process.GetProcessById, it does not have a desiredAccess parameter and fails with higher IL processes.
		}
		
		/// <summary>
		/// Returns <c>true</c> if the process is 32-bit, <c>false</c> if 64-bit.
		/// Also returns <c>false</c> if failed. Supports <see cref="lastError"/>.
		/// </summary>
		public static bool is32Bit(IntPtr processHandle) {
			bool is32bit = osVersion.is32BitOS;
			if (!is32bit) {
				if (!Api.IsWow64Process(processHandle, out is32bit)) return false;
			}
			lastError.clear();
			return is32bit;
		}
		
		/// <summary>
		/// Gets the command line string used to start the specified process.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="processId">Process id.</param>
		/// <param name="removeProgram">Remove program path. Return only arguments, or empty string if there is no arguments.</param>
		/// <remarks>
		/// The string starts with program file path or name, often enclosed in <c>""</c>, and may be followed by arguments. Some processes may modify it; then this function gets the modified string.
		/// Fails if the specified process is admin and this process isn't. May fail with some system processes. Fails if this is a 32-bit process.
		/// </remarks>
		public static unsafe string getCommandLine(int processId, bool removeProgram = false) {
			if (osVersion.is32BitProcess) return null; //can't get PEB address of 64-bit processes. Never mind 32-bit OS.
			using var pm = new ProcessMemory(processId, 0, noException: true);
			if (pm.ProcessHandle == default) return null;
			Api.PROCESS_BASIC_INFORMATION pbi;
			if (0 == Api.NtQueryInformationProcess(pm.ProcessHandle, 0, &pbi, sizeof(Api.PROCESS_BASIC_INFORMATION), out _)) {
				long upp; Api.RTL_USER_PROCESS_PARAMETERS up;
				if (pm.Read((IntPtr)pbi.PebBaseAddress + 32, &upp, 8) && pm.Read((IntPtr)upp, &up, sizeof(Api.RTL_USER_PROCESS_PARAMETERS))) {
					pm.Mem = (IntPtr)up.CommandLine.Buffer;
					var s = pm.ReadCharString(up.CommandLine.Length / 2)
						?.Trim() //many end with space, usually when without commandline args
						?.Replace('\0', ' '); //sometimes '\0' instead of spaces before args
					if (removeProgram) s = s.RxReplace(@"(?i)^(?:"".+?""|\S+)(?:\s+(.*))?", "$1");
					return s;
				}
			}
			return null;
			
			//speed: ~25 mcs cold. WMI Win32_Process ~50 ms (2000 times slower).
		}
		
		/// <summary>
		/// Terminates (ends) the specified process.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="processId">Process id.</param>
		/// <param name="exitCode">Process exit code.</param>
		/// <remarks>
		/// Uses API <msdn>WTSTerminateProcess</msdn> or <msdn>TerminateProcess</msdn>. They are async; usually the process ends after 2 - 200 ms, depending on program etc.
		/// 
		/// Does not try to end process "softly" (close main window). Unsaved data will be lost.
		/// 
		/// Alternatives: run <c>taskkill.exe</c> or <c>pskill.exe</c> (download). See <see cref="run.console"/>. More info on the internet.
		/// </remarks>
		/// <example>
		/// Restart the shell process (explorer).
		/// <code><![CDATA[
		/// process.terminate(wnd.getwnd.shellWindow.ProcessId, 1);
		/// if (!dialog.showYesNo("Restart explorer?")) return;
		/// run.it(folders.Windows + @"explorer.exe", flags: RFlags.InheritAdmin);
		/// ]]></code>
		/// </example>
		public static bool terminate(int processId, int exitCode = 0) {
			if (Api.WTSTerminateProcess(default, processId, exitCode)) return true;
			if (lastError.code != Api.ERROR_INVALID_PARAMETER) {
				using var h = Handle_.OpenProcess(processId, Api.SYNCHRONIZE | Api.PROCESS_TERMINATE);
				if (!h.Is0) {
					if (!Api.TerminateProcess(h, exitCode))
						return 0 == Api.WaitForSingleObject(h, 500); //ERROR_ACCESS_DENIED when the process is ending
					return true;
				}
			}
			return false;
			//note: TerminateProcess and WTSTerminateProcess are async. Tested programs ended after 3 - 150 ms, depending on program.
		}
		
		/// <summary>
		/// Terminates (ends) all processes of the specified program or programs.
		/// </summary>
		/// <returns>The number of successfully terminated processes.</returns>
		/// <param name="processName">
		/// Process executable file name (like <c>"notepad.exe"</c>) or full path.
		/// String format: [wildcard expression](xref:wildcard_expression).
		/// </param>
		/// <param name="allSessions">Processes of any user session. If <c>false</c> (default), only processes of this user session.</param>
		/// <param name="exitCode">Process exit code.</param>
		/// <exception cref="ArgumentException">
		/// - <i>processName</i> is <c>""</c> or <c>null</c>.
		/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
		/// </exception>
		public static int terminate(string processName, bool allSessions = false, int exitCode = 0) {
			int n = 0;
			foreach (int pid in getProcessIds(processName, fullPath: null, ofThisSession: !allSessions)) {
				if (terminate(pid, exitCode)) n++;
			}
			return n;
		}
		
		/// <summary>
		/// Suspends or resumes the specified process.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="suspend"><c>true</c> suspend, <c>false</c> resume.</param>
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
		/// </summary>
		/// <returns>The number of successfully suspended/resumed processes.</returns>
		/// <param name="suspend"><c>true</c> suspend, <c>false</c> resume.</param>
		/// <param name="processName">
		/// Process executable file name (like <c>"notepad.exe"</c>) or full path.
		/// String format: [wildcard expression](xref:wildcard_expression).
		/// </param>
		/// <param name="allSessions">Processes of any user session. If <c>false</c> (default), only processes of this user session.</param>
		/// <exception cref="ArgumentException">
		/// - <i>processName</i> is <c>""</c> or <c>null</c>.
		/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
		/// </exception>
		/// <remarks>
		/// If suspended multiple times, must be resumed the same number of times.
		/// </remarks>
		public static int suspend(bool suspend, string processName, bool allSessions = false) {
			int n = 0;
			foreach (int pid in getProcessIds(processName, fullPath: null, ofThisSession: !allSessions)) {
				if (process.suspend(suspend, pid)) n++;
			}
			return n;
		}
		
		/// <summary>
		/// Waits until the process ends.
		/// </summary>
		/// <returns><c>true</c> when the process ended. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="processId">Process id. If invalid but not 0, the function returns <c>true</c> and sets <c>exitCode = int.MinValue</c>; probably the process is already ended.</param>
		/// <param name="exitCode">Receives the exit code.</param>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <exception cref="ArgumentException"><i>processId</i> is 0.</exception>
		public static bool waitForExit(Seconds timeout, int processId, out int exitCode) {
			if (processId == 0) throw new ArgumentException("processId 0", nameof(processId));
			using var h = Handle_.OpenProcess(processId, Api.SYNCHRONIZE | Api.PROCESS_QUERY_LIMITED_INFORMATION);
			if (h.Is0) {
				var e = lastError.code;
				if (e == Api.ERROR_INVALID_PARAMETER) { exitCode = int.MinValue; return true; };
				throw new AuException(e);
			}
			exitCode = 0;
			if (0 == wait.forHandle(timeout, 0, h)) return false;
			Api.GetExitCodeProcess(h, out exitCode);
			return true;
		}
		
		/// <summary>
		/// Provides process started/ended triggers in <c>foreach</c> loop. See examples.
		/// </summary>
		/// <returns>
		/// An object that retrieves process trigger info (started/ended, name, id, session id) when used with <c>foreach</c>.
		/// If need more process properties, your code can call <see cref="process"/> class functions with the process id.
		/// </returns>
		/// <param name="started">Trigger events: <c>true</c> - started, <c>false</c> - ended, <c>null</c> (default) - both.</param>
		/// <param name="processName">
		/// Process executable file name, like <c>"notepad.exe"</c>.
		/// String format: [wildcard expression](xref:wildcard_expression).
		/// <c>null</c> matches all.
		/// </param>
		/// <param name="ofThisSession">Watch processes only of this user session.</param>
		/// <param name="period">
		/// The period in milliseconds of retrieving the list of processes for detecting new and ended processes. Default 100, min 10, max 1000.
		/// Smaller = smaller average delay and less missing triggers (when process lifetime is very short) but more CPU usage.
		/// </param>
		/// <exception cref="ArgumentException">Invalid wildcard expression (<c>"**options "</c> or regular expression).</exception>
		/// <example>
		/// <code><![CDATA[
		/// //all started and ended processes
		/// foreach (var v in process.triggers()) {
		/// 	print.it(v);
		/// }
		/// 
		/// //started notepad processes in current user session
		/// foreach (var v in process.triggers(started: <c>true</c>, "notepad.exe", ofThisSession: true)) {
		/// 	print.it(v);
		/// }
		/// ]]></code>
		/// </example>
		public static IEnumerable<ProcessTriggerInfo> triggers(bool? started = null, [ParamString(PSFormat.Wildex)] string processName = null, bool ofThisSession = false, int period = 100) {
			wildex wild = processName;
			period = Math.Clamp(period, 10, 1000) - 2;
			var comparer = new _PiComparer();
			var hs = new HashSet<ProcessInfo>(comparer);
			var ap = allProcesses(ofThisSession);
			for (; ; ) {
				period.ms();
				//Debug_.MemorySetAnchor_();
				//perf.first();
				using (var p = new AllProcesses_(ofThisSession)) {
					//perf.next();
					bool eq = p.Count == ap.Length;
					if (eq) for (int i = 0; i < ap.Length; i++) if (!(eq = ap[i].Id == p.Id(i))) break;
					if (!eq) {
						var a = new ProcessInfo[p.Count];
						for (int i = 0; i < a.Length; i++) a[i] = p.Info(i);
						for (int i = 0; i < 2; i++) {
							ProcessInfo[] a1, a2;
							if (i == 0) {
								if (started == true) continue;
								a1 = a; a2 = ap;
							} else {
								if (started == false) continue;
								a1 = ap; a2 = a;
							}
							hs.Clear(); hs.UnionWith(a1);
							foreach (var v in a2) {
								if (hs.Add(v)) {
									if (wild == null || wild.Match(v.Name))
										yield return new(i == 1, v.Name, v.Id, v.SessionId);
								}
							}
						}
						ap = a;
					}
				}
				//perf.nw();
				//Debug_.MemoryPrint_();
			}
		}
		
		class _PiComparer : IEqualityComparer<ProcessInfo> {
			//public bool Equals(ProcessInfo x, ProcessInfo y) => x.Id == y.Id;
			public bool Equals(ProcessInfo x, ProcessInfo y) => x.Id == y.Id && x.Name == y.Name;
			public int GetHashCode(ProcessInfo obj) => obj.Id;
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
		/// Gets full path of the program file of this process.
		/// </summary>
		[SkipLocalsInit]
		public static unsafe string thisExePath => Environment.ProcessPath;
		
		/// <summary>
		/// Gets file name of the program file of this process, like <c>"name.exe"</c>.
		/// </summary>
		public static string thisExeName => s_exeName ??= pathname.getName(thisExePath);
		static string s_exeName;
		
		/// <summary>
		/// Gets user session id of this process.
		/// </summary>
		public static int thisProcessSessionId => getSessionId(Api.GetCurrentProcessId());
		
		/// <summary>
		/// Gets or sets whether <see cref="CultureInfo.DefaultThreadCurrentCulture"/> and <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> are <see cref="CultureInfo.InvariantCulture"/>.
		/// </summary>
		/// <remarks>
		/// If your app doesn't want to use current culture (default in .NET apps), it can set these properties = <see cref="CultureInfo.InvariantCulture"/> or set this property = <c>true</c>.
		/// It prevents potential bugs when app/script/components don't specify invariant culture in string functions and "number to/from string" functions.
		/// Also, there is a bug in "number to/from string" functions in some .NET versions with some cultures: they use wrong minus sign, not ASCII <c>'-'</c> which is specified in Control Panel.
		/// The default compiler sets this property = <c>true</c>; as well as <see cref="script.setup"/>.
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
		/// After <i>afterMS</i> milliseconds invokes GC and calls API <b>SetProcessWorkingSetSize</b>.
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
		/// The event handler is called on <see cref="AppDomain.ProcessExit"/> (then the parameter is <c>null</c>) and <see cref="AppDomain.UnhandledException"/> (then the parameter is <b>Exception</b>).
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
		
		static void _ThisProcessExit(object sender, EventArgs ea) { //sender: AppDomain on process exit, null on unhandled exception
			Exception e;
			if (ea is UnhandledExceptionEventArgs u) {
				if (!u.IsTerminating) return; //never seen, but anyway
				e = (Exception)u.ExceptionObject; //probably non-Exception object is impossible in C#
			} else {
				e = script.s_unhandledException;
			}
			var k = _eventExit;
			if (k != null) {
				try { _eventExit = null; k(e); }
				catch (Exception e1) { print.qm2.writeD("_ThisProcessExit", e1); }
			}
			thisProcessExitDone_?.Invoke();
		}
		
		internal static event Action thisProcessExitDone_;
		
		/// <summary>
		/// Calls and removes all <see cref="thisProcessExit"/> event handlers.
		/// </summary>
		/// <remarks>
		/// Call this if <b>thisProcessExit</b> event handlers don't run because this process is terminated before it. For example when current session is ending (shutdown, restart, logoff); to detect it can be used <b>Application.SessionEnding</b>, <b>Application.OnSessionEnding</b> or <b>WM_QUERYENDSESSION</b>.
		/// </remarks>
		public static void thisProcessExitInvoke() {
			var k = _eventExit;
			if (k != null) try { _eventExit = null; k(null); } catch { }
		}
		
		#endregion
		
		#region this thread
		
		/// <summary>
		/// Gets native thread id of this thread (API <msdn>GetCurrentThreadId</msdn>).
		/// </summary>
		/// <remarks>
		/// It is not the same as <see cref="Environment.CurrentManagedThreadId"/>.
		/// </remarks>
		/// <seealso cref="wnd.ThreadId"/>
		public static int thisThreadId => Api.GetCurrentThreadId();
		//speed: fast, but several times slower than Environment.CurrentManagedThreadId. Caching in a ThreadStatic variable makes even slower.
		
		/// <summary>
		/// Returns native thread handle of this thread (API <msdn>GetCurrentThread</msdn>).
		/// </summary>
		public static IntPtr thisThreadHandle => Api.GetCurrentThread();
		
		/// <summary>
		/// Returns <c>true</c> if this thread has a .NET message loop (winforms or WPF).
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
		static bool _HML_Wpf() {
			if (SynchronizationContext.Current is System.Windows.Threading.DispatcherSynchronizationContext) {
				var d = System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread);
				if (d != null) {
					var f = typeof(System.Windows.Threading.Dispatcher).GetField("_frameDepth", BindingFlags.Instance | BindingFlags.NonPublic);
					Debug_.PrintIf(f == null);
					return f == null || f.GetValue(d) is not int i || i > 0;
				}
			}
			return false;
		}
		//static bool _HML_Wpf() => System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread) != null; //no. Not null after a loop ends or even after XamlReader.Parse.
		//static bool _HML_Wpf() => SynchronizationContext.Current is System.Windows.Threading.DispatcherSynchronizationContext; //no. True eg in Dispatcher.Invoke callback.
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void ThisThreadSetComApartment_(ApartmentState state) {
			var t = Thread.CurrentThread;
			t.TrySetApartmentState(ApartmentState.Unknown);
			t.TrySetApartmentState(state);
			//CONSIDER: use OleInitialize instead of t.TrySetApartmentState(state).
			//	Somehow RegisterDragDrop in UacDragDrop fails if ThisThreadSetComApartment_.
			//	But RDD in SciCode works with this.
			
			//This is undocumented, but works if we set ApartmentState.Unknown at first.
			//With [STAThread] slower, and the process initially used to have +2 threads.
			//Speed when called to set STA at startup: 1.7 ms. If apphost calls OleInitialize, 1.5 ms.
			//tested: OleUninitialize in apphost does not make GetApartmentState return MTA.
		}
		
		#endregion
	}
}

namespace Au.Types {
	/// <summary>
	/// Contains process name (like <c>"notepad.exe"</c>), id and user session id.
	/// </summary>
	public record struct ProcessInfo(string Name, int Id, int SessionId);
	//use record to auto-implement ==, eg for code like var a=process.allProcesses(); 5.s(); print.it(process.allProcesses().Except(a));
	
	/// <summary>
	/// Contains process trigger info retrieved by <see cref="process.triggers"/>.
	/// </summary>
	public record class ProcessTriggerInfo(bool Started, string Name, int Id, int SessionId);
}
