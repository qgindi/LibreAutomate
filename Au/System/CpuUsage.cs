namespace Au.More;

/// <summary>
/// Gets CPU usage.
/// </summary>
/// <remarks>
/// You can use the static functions (easier) or a <b>CpuUsage</b> instance. A <b>CpuUsage</b> instance must be disposed (use <c>using</c>).
/// </remarks>
/// <example>
/// <code><![CDATA[
/// using var u = new CpuUsage(); //all processes
/// //using var u = new CpuUsage(process.getProcessId("Au.Editor.exe")); //single process
/// //using var u = new CpuUsage(process.getProcessIds("chrome.exe")); //all Chrome processes
/// for (; ; ) {
/// 	if (!u.Start()) break;
/// 	Thread.Sleep(500);
/// 	print.it(u.Stop());
/// }
/// ]]></code>
/// </example>
public sealed class CpuUsage : IDisposable {
	bool _ofProcess, _started;
	Handle_ _ph;
	Handle_[] _aph;
	long _mcs, _cycles;
	
	/// <summary>
	/// Use this constructor to get CPU usage of all processes (sum).
	/// </summary>
	public CpuUsage() {
		
	}
	
	/// <summary>
	/// Use this constructor to get CPU usage of a process.
	/// </summary>
	/// <param name="processId">Process id.</param>
	public CpuUsage(int processId) {
		_ofProcess = true;
		_ph = Handle_.OpenProcess(processId);
	}
	
	/// <summary>
	/// Use this constructor to get CPU usage of multiple processes (sum).
	/// </summary>
	/// <param name="processes">Process ids.</param>
	public CpuUsage(IEnumerable<int> processes) {
		_ofProcess = true;
		_aph = processes.Select(o => Handle_.OpenProcess(o)).Where(o => !o.Is0).ToArray();
		if (_aph.Length == 0) _aph = null;
	}
	
	///
	public void Dispose() {
		if (_ofProcess) {
			if (_aph == null) _ph.Dispose();
			else for (int i = 0; i < _aph.Length; i++) _aph[i].Dispose();
		}
	}
	
	/// <summary>
	/// Starts measuring CPU usage.
	/// </summary>
	/// <returns><c>false</c> if <i>processId</i> is invalid.</returns>
	public bool Start() {
		_started = _GetCycles(out _cycles);
		_mcs = perf.mcs;
		return _started;
	}
	
	/// <summary>
	/// Ends measuring CPU usage, and gets result.
	/// Call this after calling <see cref="Start"/> and waiting at least 1 ms. Don't call if <b>Start</b> returned <c>false</c>.
	/// </summary>
	/// <returns>CPU usage 0 to 100 %.</returns>
	/// <exception cref="InvalidOperationException">Called without successful <see cref="Start"/>.</exception>
	public double Stop() {
		if (!_started) throw new InvalidOperationException();
		_started = false;
		
		if (!_GetCycles(out var cycles)) return 0;
		long mcs = perf.mcs - _mcs;
		
		var r = (cycles - _cycles) / _CyclesMcs() / mcs;
		Debug_.PrintIf(r > 1.1);
		r = Math.Min(r, 1);
		r = _ofProcess ? r : 1 - r;
		return Math.Round(r * 100, 2);
	}
	
	bool _GetCycles(out long r) {
		if (!_ofProcess) { r = _QIPCT(); return true; }
		
		if (_aph == null) return Api.QueryProcessCycleTime(_ph, out r); //tested: succeeds and gets cycles even if the process ended, if handle is valid
		
		r = 0;
		for (int i = _aph.Length; --i >= 0;) {
			Api.QueryProcessCycleTime(_aph[i], out var v);
			r += v;
		}
		return true;
		
		[SkipLocalsInit]
		static unsafe long _QIPCT() {
			var a = stackalloc long[64];
			long r = 0;
			for (ushort g = 0, ng = Api.GetActiveProcessorGroupCount(); g < ng; g++) {
				int size = 64 * 8;
				Api.QueryIdleProcessorCycleTimeEx(g, ref size, a);
				for (int i = 0; i < size / 8; i++) r += a[i];
			}
			return r;
		}
	}
	
	//Gets CPU cycles / microsecond * CPU count.
	static double _CyclesMcs() {
		if (s_cycles.timeMeasured == 0) { //JIT-compile
			_ = perf.mcs;
		}
		
		if (Environment.TickCount64 != s_cycles.timeMeasured) {
			nint th = process.thisThreadHandle;
			int tp0 = Api.GetThreadPriority(th);
			Api.SetThreadPriority(th, Api.THREAD_PRIORITY_TIME_CRITICAL);
			try {
				int nCpu = ProcessorCount;
				double r = 0;
				for (int i = 0; i < 3; i++) {
					Api.QueryThreadCycleTime(th, out long t1);
					long mcs = perf.mcs;
					while (perf.mcs < mcs + 300) { }
					Api.QueryThreadCycleTime(th, out long t2);
					mcs = perf.mcs - mcs;
					var v = (double)(t2 - t1) * nCpu / mcs;
					//if(i>0 && Math.Abs(v-r)>1000) print.it($"<><c red>{r}, {v}, {v-r}<>");
					if (v > r) r = v;
				}
				//if (Math.Abs(22436 - r) > 1000) print.it($"<><c red>cycles={r}<>"); //else print.it($"cycles={r}");
				s_cycles = (Environment.TickCount64, r);
			}
			finally { Api.SetThreadPriority(th, tp0); }
		}
		return s_cycles.result;
		
		//To avoid incorrect result when this thread interrupted etc, this func measures 3 times and gets max result. Also sets max thread priority.
	}
	static (long timeMeasured, double result) s_cycles;
	
	/// <summary>
	/// Gets CPU usage of all processes (sum).
	/// </summary>
	/// <param name="duration">How long to measure, milliseconds. Default 10. Min 1. Calls <c>Thread.Sleep(duration);</c>.</param>
	/// <returns>CPU usage 0 to 100 %.</returns>
	public static unsafe double OfAllProcesses(int duration = 10) {
		ArgumentOutOfRangeException.ThrowIfLessThan(duration, 1);
		using var u = new CpuUsage();
		u.Start();
		Thread.Sleep(duration);
		return u.Stop();
	}
	
	/// <summary>
	/// Gets CPU usage of a process.
	/// </summary>
	/// <param name="processId">Process id.</param>
	/// <param name="duration">How long to measure, milliseconds. Default 10. Min 1. Calls <c>Thread.Sleep(duration);</c>.</param>
	/// <returns>CPU usage 0 to 100 %. Returns 0 if <i>processId</i> invalid.</returns>
	public static double OfProcess(int processId, int duration = 10) {
		ArgumentOutOfRangeException.ThrowIfLessThan(duration, 1);
		using var u = new CpuUsage(processId);
		if (!u.Start()) return 0;
		Thread.Sleep(duration);
		return u.Stop();
	}
	
	/// <summary>
	/// Gets CPU usage of multiple processes (sum).
	/// </summary>
	/// <param name="processes">Process ids.</param>
	/// <param name="duration">How long to measure, milliseconds. Default 10. Min 1. Calls <c>Thread.Sleep(duration);</c>.</param>
	/// <returns>CPU usage 0 to 100 %. Returns 0 if all ids invalid.</returns>
	public static double OfProcesses(IEnumerable<int> processes, int duration = 10) {
		ArgumentOutOfRangeException.ThrowIfLessThan(duration, 1);
		using var u = new CpuUsage(processes);
		if (!u.Start()) return 0;
		Thread.Sleep(duration);
		return u.Stop();
	}
	
	/// <summary>
	/// Get the number of logical CPU in the system.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="Environment.ProcessorCount"/>, does not depend on process affinity etc.
	/// </remarks>
	public static unsafe int ProcessorCount {
		get {
			int r = 0;
			for (ushort g = 0, ng = Api.GetActiveProcessorGroupCount(); g < ng; g++) {
				int n = 0;
				Api.QueryIdleProcessorCycleTimeEx(g, ref n, null);
				r += n / 8;
			}
			return r;
		}
	}
}
