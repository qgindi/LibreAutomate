namespace Au;

/// <summary>
/// Contains functions to wait for a custom condition, handle, etc, or simply sleep.
/// </summary>
/// <remarks>
/// Specialized "wait for" functions are in other classes, for example <see cref="wnd.wait"/>.
/// 
/// All "wait for" functions have a timeout parameter. It is the maximal time to wait, in seconds. If 0, waits indefinitely. If > 0, throws <see cref="TimeoutException"/> when timed out. If &lt; 0, then stops waiting and returns default value of that type (<c>false</c>, etc).
/// 
/// While waiting, most functions by default don't dispatch Windows messages, events, hooks, timers, COM/RPC, etc. For example, if used in a <b>Window</b>/<b>Form</b>/<b>Control</b> event handler, the window would stop responding. Use another thread, for example async/await/<b>Task</b>, like in the example. Or <see cref="Seconds.DoEvents"/>.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// wait.until(0, () => keys.isScrollLock);
/// print.it("ScrollLock now is toggled");
/// ]]></code>
/// Using in a WPF window with async/await.
/// <code><![CDATA[
/// using System.Windows;
/// var b = new wpfBuilder("Window").WinSize(250);
/// b.R.AddButton("Wait", async _ => {
/// 	  print.it("waiting for ScrollLock...");
/// 	  var result = await Task.Run(() => wait.until(-10, () => keys.isScrollLock));
/// 	  print.it(result);
/// });
/// if (!b.ShowDialog()) return;
/// ]]></code>
/// </example>
public static partial class wait {
	/// <summary>
	/// Waits <i>timeMilliseconds</i> milliseconds.
	/// </summary>
	/// <param name="timeMilliseconds">Time to wait, milliseconds. Or <see cref="Timeout.Infinite"/> (-1).</param>
	/// <remarks>
	/// Calls <see cref="Thread.Sleep(int)"/>.
	/// Does not process Windows messages and other events, therefore should not be used in threads with windows, timers, hooks, events or COM, unless <i>timeMilliseconds</i> is small. Supports APC.
	/// If the computer goes to sleep or hibernate during that time, the real time is the specified time + the sleep/hibernate time.
	/// 
	/// Tip: the script editor replaces code like <c>100ms</c> with <c>100.ms();</c> when typing.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><i>timeMilliseconds</i> is negative and not -1 (<b>Timeout.Infinite</b>).</exception>
	/// <example>
	/// <code><![CDATA[
	/// wait.ms(500);
	/// 500.ms(); //the same (ms is an extension method)
	/// wait.s(0.5); //the same
	/// ]]></code>
	/// </example>
	public static void ms(this int timeMilliseconds) {
		SleepPrecision_.TempSet1_(timeMilliseconds);
		if (timeMilliseconds < 2000) {
			Thread.Sleep(timeMilliseconds);
		} else { //workaround for Thread.Sleep bug: if there are APC, returns too soon after sleep/hibernate.
			g1:
			long t = computer.tickCountWithoutSleep;
			Thread.Sleep(timeMilliseconds);
			t = timeMilliseconds - (computer.tickCountWithoutSleep - t);
			if (t >= 500) { timeMilliseconds = (int)t; goto g1; }
		}
	}
	
	/// <summary>
	/// Waits <i>timeSeconds</i> seconds.
	/// The same as <see cref="ms"/>, but the time is specified in seconds, not milliseconds.
	/// </summary>
	/// <param name="timeSeconds">Time to wait, seconds.</param>
	/// <exception cref="ArgumentOutOfRangeException"><i>timeSeconds</i> is less than 0 or greater than 2147483 (<b>int.MaxValue</b> / 1000, 24.8 days).</exception>
	/// <remarks>
	/// Tip: the script editor replaces code like <c>100ms</c> with <c>100.ms();</c> when typing.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// wait.s(5);
	/// 5.s(); //the same (s is an extension method)
	/// 5000.ms(); //the same
	/// ]]></code>
	/// </example>
	public static void s(this int timeSeconds) {
		if ((uint)timeSeconds > int.MaxValue / 1000) throw new ArgumentOutOfRangeException();
		ms(timeSeconds * 1000);
	}
	
	/// <summary>
	/// Waits <i>timeSeconds</i> seconds.
	/// The same as <see cref="ms"/>, but the time is specified in seconds, not milliseconds.
	/// </summary>
	/// <param name="timeSeconds">Time to wait, seconds. The smallest value is 0.001 (1 ms).</param>
	/// <exception cref="ArgumentOutOfRangeException"><i>timeSeconds</i> is less than 0 or greater than 2147483 (<b>int.MaxValue</b> / 1000, 24.8 days).</exception>
	/// <example>
	/// <code><![CDATA[
	/// wait.s(2.5);
	/// 2500.ms(); //the same
	/// ]]></code>
	/// </example>
	public static void s(this double timeSeconds) {
		double t = timeSeconds * 1000d;
		if (t > int.MaxValue || t < 0) throw new ArgumentOutOfRangeException();
		ms((int)t);
	}
	//Maybe this should not be an extension method.
	//	Code like 0.5.s() looks weird. Better 500.ms(). Rarely need non-integer time when > 1 s.
	//	But: 1. Symmetry. 2. Easier to convert QM code, like 0.5 to 0.5.s(); not 500.ms();.
	
	/// <summary>
	/// Waits <i>timeMS</i> milliseconds. While waiting, retrieves and dispatches Windows messages and other events.
	/// </summary>
	/// <param name="timeMS">Time to wait, milliseconds. Or <see cref="Timeout.Infinite"/> (-1).</param>
	/// <remarks>
	/// Unlike <see cref="ms"/>, this function retrieves and dispatches Windows messages, calls .NET event handlers, hook procedures, timer functions, COM, etc.
	/// This function can be used in threads with windows. However usually there are better ways, for example timer, other thread, async/await/<b>Task</b>.
	/// If <i>timeMS</i> is -1, returns when receives <msdn>WM_QUIT</msdn> message.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><i>timeMS</i> is negative and not -1 (<b>Timeout.Infinite</b>).</exception>
	public static unsafe void doEvents(int timeMS) {
		if (timeMS < -1) throw new ArgumentOutOfRangeException();
		if (timeMS == 0) {
			Api.SleepEx(0, true); //call APC
			doEvents();
		} else {
			using var mp = new MessagePump_();
			for (long time = timeMS, timePrev = 0; ;) {
				if (timeMS > 0) {
					long timeNow = computer.tickCountWithoutSleep;
					if (timePrev > 0) time -= timeNow - timePrev;
					if (time <= 0) return;
					timePrev = timeNow;
				}
				switch (Api.MsgWaitForMultipleObjectsEx(0, null, (int)time, Api.QS_ALLINPUT, Api.MWMO_ALERTABLE | Api.MWMO_INPUTAVAILABLE)) {
				case 0: if (!mp.Pump() && timeMS < 0) return; break;
				case Api.WAIT_TIMEOUT: return;
				case Api.WAIT_IO_COMPLETION: break;
				default: throw new Win32Exception();
				}
			}
		}
		//info: Thread.Sleep is alertable too. One reason I know is Thread.Interrupt; but with doEvents it does not work.
	}
	
	internal struct MessagePump_ : IDisposable {
		int? _quit;
		
		/// <summary>
		/// If <see cref="Pump"/> received <b>WM_QUIT</b>, calls <b>PostQuitMessage</b>, unless detects that it could cause infinite loop.
		/// </summary>
		public void Dispose() {
			if (_quit != null) {
				//prevent infinite loop when eg the caller uses a loop like in doEvents(int) and ignores WM_QUIT
				var stack = new StackTrace(false).FrameCount;
				if (t_doeventsStack == 0 || stack < t_doeventsStack) {
					t_doeventsStack = stack;
					Api.PostQuitMessage(_quit.Value);
				} else {
					print.warning("duplicate WM_QUIT");
				}
				_quit = null;
			}
		}
		[ThreadStatic] static int t_doeventsStack;
		
		/// <summary>
		/// Calls <b>PeekMessage</b>/<b>TranslateMessage</b>/<b>DispatchMessage</b> while there are messages.
		/// </summary>
		/// <returns><c>false</c> if received <b>WM_QUIT</b>.</returns>
		public bool Pump() {
			while (Api.PeekMessage(out var m)) {
				if (m.message == Api.WM_QUIT) { _quit = (int)m.wParam; return false; }
				Api.TranslateMessage(m);
				Api.DispatchMessage(m);
			}
			return true;
		}
		
		/// <summary>
		/// Like <b>Pump</b>, but can call a callback function. Used by <see cref="Wait_"/>.
		/// </summary>
		/// <param name="msgCallback">
		/// <c>null</c> or callback function of type:
		/// <br/>• <b>WPMCallback</b> - called before dispatching a message. If returns <c>true</c>, not called for other messages. Can modify the <b>MSG</b>. Can set <b>MSG.message</b> = 0 to prevent dispatching it.
		/// <br/>• <b>Func&lt;bool&gt;</b> - called after dispatching all messages.
		/// </param>
		/// <returns><c>true</c> if <i>msgCallback</i> returned <c>true</c>.</returns>
		public bool PumpWithCallback(Delegate msgCallback) {
			bool R = false;
			while (Api.PeekMessage(out var m)) {
				if (msgCallback is WPMCallback callback1) {
					if (callback1(ref m)) { msgCallback = null; R = true; }
					if (m.message == 0) continue;
				}
				if (m.message == Api.WM_QUIT) { _quit = (int)m.wParam; continue; } //now ignore, but finally repost
				Api.TranslateMessage(m);
				Api.DispatchMessage(m);
			}
			if (msgCallback is Func<bool> callback2) R = callback2();
			return R;
		}
		
		//note: never use "dispatch only sent messages". It's dangerous and not useful.
		//	If thread has windows, hangs if we don't get posted messages.
		//	Else PeekMessage usually does not harm.
		
		//note: with PeekMessage don't use |Api.PM_QS_SENDMESSAGE when don't need posted messages.
		//	Then setwineventhook hook does not work. Although setwindowshookex hook works. COM RPC not tested.
	}
	
	/// <summary>
	/// Retrieves and dispatches events and Windows messages from the message queue of this thread.
	/// </summary>
	/// <returns><c>false</c> if received <b>WM_QUIT</b> message.</returns>
	/// <remarks>
	/// Similar to <see cref="System.Windows.Forms.Application.DoEvents"/>, but more lightweight. Uses API functions <msdn>PeekMessage</msdn>, <msdn>TranslateMessage</msdn> and <msdn>DispatchMessage</msdn>.
	/// </remarks>
	public static bool doEvents() {
		using var mp = new MessagePump_();
		return mp.Pump();
	}
	
	/// <summary>
	/// Calls <see cref="SleepPrecision_.TempSet1_"/> and <see cref="doEvents(int)"/>.
	/// </summary>
	internal static unsafe void doEventsPrecise_(int timeMS) {
		SleepPrecision_.TempSet1_(timeMS);
		doEvents(timeMS);
	}
	
	/// <summary>
	/// Temporarily changes the time resolution/precision of <b>Thread.Sleep</b> and some other functions.
	/// </summary>
	/// <remarks>
	/// Uses API <msdn>timeBeginPeriod</msdn>, which requests a time resolution for various system timers and wait functions. Actually it is the system thread scheduling timer period.
	/// Normal resolution on Windows 7-10 is 15.625 ms. It means that, for example, <c>Thread.Sleep(1);</c> sleeps not 1 but 1-15 ms. If you set resolution 1, it sleeps 1-2 ms.
	/// The new resolution is revoked (<msdn>timeEndPeriod</msdn>) when disposing the <b>SleepPrecision_</b> variable or when this process ends. See example. See also <see cref="TempSet1"/>.
	/// The resolution is applied to all threads and processes. Other applications can change it too. For example, often web browsers temporarily set resolution 1 ms when opening a web page.
	/// The system uses the smallest period (best resolution) that currently is set by any application. You cannot make it bigger than current value.
	/// <note>It is not recommended to keep small period (high resolution) for a long time. It can be bad for power saving.</note>
	/// Don't need this for <b>wait.ms/s</b> and functions that use them (<b>mouse.click</b> etc). They call <see cref="TempSet1"/> when the sleep time is 1-89 ms.
	/// This does not change the minimal period of <see cref="timer"/> and <b>System.Windows.Forms.Timer</b>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// _Test("before");
	/// using(new wait.SleepPrecision_(2)) {
	/// 	_Test("in");
	/// }
	/// _Test("after");
	/// 
	/// void _Test(string name) {
	/// 	print.it(name);
	/// 	perf.first();
	/// 	for(int i = 0; i < 8; i++) { Thread.Sleep(1); perf.next(); }
	/// 	perf.write();
	/// }
	/// ]]></code>
	/// </example>
	internal sealed class SleepPrecision_ : IDisposable {
		//info: this class could be public, but probably not useful. wait.ms automatically sets 1 ms period if need.
		
		int _period;
		
		/// <summary>
		/// Calls API <msdn>timeBeginPeriod</msdn>.
		/// </summary>
		/// <param name="periodMS">
		/// New system timer period, milliseconds.
		/// Should be 1. Other values may stuck and later cannot be made smaller due to bugs in OS or some applications; this bug would impact many functions of this library.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException"><i>periodMS</i> &lt;= 0.</exception>
		public SleepPrecision_(int periodMS) {
			if (periodMS <= 0) throw new ArgumentOutOfRangeException();
			if (Api.timeBeginPeriod((uint)periodMS) != 0) return;
			//print.it("set");
			_period = periodMS;
			
			//Bug in OS or drivers or some apps:
			//	On my main PC often something briefly sets 0.5 ms resolution.
			//	If at that time this process already has set a resolution of more than 1 ms, then after that time this process cannot change resolution.
			//	It means that if this app eg has set 10 ms resolution, then wait.ms(1) will sleep 10 ms and not the normal 1-2 ms.
			//	Known workaround (but don't use, sometimes does not work, eg cannot end period that was set by another process):
			//		timeBeginPeriod(periodMS);
			//		var r=(int)Current; if(r>periodMS) { timeEndPeriod(periodMS); timeEndPeriod(r); timeBeginPeriod(r); timeBeginPeriod(periodMS); }
		}
		
		/// <summary>
		/// Calls API <msdn>timeEndPeriod</msdn>.
		/// </summary>
		public void Dispose() {
			_Dispose();
			GC.SuppressFinalize(this);
		}
		
		void _Dispose() {
			if (_period == 0) return;
			//print.it("revoke");
			Api.timeEndPeriod((uint)_period); _period = 0;
		}
		
		///
		~SleepPrecision_() { _Dispose(); }
		
		/// <summary>
		/// Gets current actual system time resolution (period).
		/// </summary>
		/// <returns>The return value usually is between 0.5 and 15.625 milliseconds. Returns 0 if failed.</returns>
		public static float Current {
			get {
				if (0 != Api.NtQueryTimerResolution(out _, out _, out var t)) return 0f;
				return (float)t / 10000;
			}
		}
		
		/// <summary>
		/// Temporarily sets the system wait precision to 1 ms. It will be revoked after the specified time or when this process ends.
		/// If already set, just updates the revoking time.
		/// </summary>
		/// <param name="endAfterMS">Revoke after this time, milliseconds.</param>
		/// <example>
		/// <code><![CDATA[
		/// print.it(wait.SleepPrecision_.Current); //probably 15.625
		/// wait.SleepPrecision_.TempSet1(500);
		/// print.it(wait.SleepPrecision_.Current); //1
		/// Thread.Sleep(600);
		/// print.it(wait.SleepPrecision_.Current); //probably 15.625 again
		/// ]]></code>
		/// </example>
		public static void TempSet1(int endAfterMS = 1111) {
			lock ("2KgpjPxRck+ouUuRC4uBYg") {
				s_TS1_EndTime = computer.tickCountWithoutSleep + endAfterMS;
				if (s_TS1_Obj == null) {
					s_TS1_Obj = new SleepPrecision_(1); //info: instead could call the API directly, but may need to auto-revoke using the finalizer
					ThreadPool.QueueUserWorkItem(endAfterMS2 => {
						Thread.Sleep((int)endAfterMS2); //note: don't use captured variables. It creates new garbage all the time.
						for (; ; ) {
							int t;
							lock ("2KgpjPxRck+ouUuRC4uBYg") {
								t = (int)(s_TS1_EndTime - computer.tickCountWithoutSleep);
								if (t <= 0) {
									s_TS1_Obj.Dispose();
									s_TS1_Obj = null;
									break;
								}
							}
							Thread.Sleep(t);
						}
					}, endAfterMS);
					//performance (old info): single QueueUserWorkItem adds 3 threads, >=2 adds 5. But Thread.Start is too slow etc.
					//QueueUserWorkItem speed first time is similar to Thread.Start, then ~8.
					//Task.Run and Task.Delay are much much slower first time. Single Delay adds 5 threads.
				}
			}
			//tested: Task Manager shows 0% CPU. If we set/revoke period for each Sleep(1) in loop, shows ~0.5% CPU.
		}
		static SleepPrecision_ s_TS1_Obj;
		static long s_TS1_EndTime;
		
		//never mind: finalizer is not called on process exit.
		//	Not a problem, because OS clears our set value (tested). Or we could use process.thisProcessExit event.
		
		/// <summary>
		/// Calls TempSet1 if <i>sleepTimeMS</i> is 1-89.
		/// </summary>
		/// <param name="sleepTimeMS">milliseconds of the caller "sleep" function.</param>
		internal static void TempSet1_(int sleepTimeMS) {
			if (sleepTimeMS is < 90 and > 0) TempSet1(1111);
		}
	}
}
