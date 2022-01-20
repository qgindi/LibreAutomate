namespace Au
{
	public static partial class wait
	{
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
		/// <exception cref="ArgumentOutOfRangeException"><i>timeMilliseconds</i> is negative and not Timeout.Infinite (-1).</exception>
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
		/// <exception cref="ArgumentOutOfRangeException"><i>timeSeconds</i> is less than 0 or greater than 2147483 (int.MaxValue/1000, 24.8 days).</exception>
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
		/// <exception cref="ArgumentOutOfRangeException"><i>timeSeconds</i> is less than 0 or greater than 2147483 (int.MaxValue/1000, 24.8 days).</exception>
		/// <example>
		/// <code><![CDATA[
		/// wait.s(2.5);
		/// 2.5.s(); //the same (s is an extension method)
		/// 2500.ms(); //the same
		/// ]]></code>
		/// </example>
		public static void s(this double timeSeconds) {
			double t = timeSeconds * 1000d;
			if (t > int.MaxValue || t < 0) throw new ArgumentOutOfRangeException();
			ms((int)t);
		}
		//Maybe this should not be an extension method.
		//	Code like 0.5.s() looks weird and better should use 500.ms(). Rarely need non-integer time when > 1 s.
		//	But: 1. Symmetry. 2. Makes easier to convert QM2 code, like 0.5 to 0.5.s(); not 500.ms();.

		/// <summary>
		/// Waits <i>timeMS</i> milliseconds. While waiting, retrieves and dispatches Windows messages and other events.
		/// </summary>
		/// <param name="timeMS">Time to wait, milliseconds. Or <see cref="Timeout.Infinite"/> (-1).</param>
		/// <remarks>
		/// Unlike <see cref="ms"/>, this function retrieves and dispatches Windows messages, calls .NET event handlers, hook procedures, timer functions, COM/RPC, etc. Supports APC.
		/// This function can be used in threads with windows. However usually there are better ways, for example timer, other thread, async/await/Task. In some places this function does not work as expected, for example in Form/Control mouse event handlers .NET blocks other mouse events.
		/// Be careful, this function is as dangerous as <see cref="System.Windows.Forms.Application.DoEvents"/>.
		/// Calls API <msdn>MsgWaitForMultipleObjectsEx</msdn> and <see cref="doEvents()"/>.
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException"><i>timeMS</i> is negative and not Timeout.Infinite.</exception>
		/// <seealso cref="forMessagesAndCondition"/>
		/// <seealso cref="forPostedMessage"/>
		public static void doEvents(int timeMS) {
			SleepDoEvents_(timeMS);
		}

		/// <summary>
		/// Same as <b>doEvents(int)</b> but with parameter <i>noSetPrecision</i>.
		/// </summary>
		internal static void SleepDoEvents_(int timeMS, bool noSetPrecision = false) {
			if (timeMS < 0 && timeMS != -1) throw new ArgumentOutOfRangeException();

			if (timeMS == 0) {
				doEvents();
				return;
			}

			if (!noSetPrecision) SleepPrecision_.TempSet1_(timeMS);

			Wait_(timeMS, WHFlags.DoEvents, null, null);
		}

		/// <summary>
		/// Retrieves and dispatches events and Windows messages from the message queue of this thread.
		/// </summary>
		/// <remarks>
		/// Similar to <see cref="System.Windows.Forms.Application.DoEvents"/>, but more lightweight. Uses API functions <msdn>PeekMessage</msdn>, <msdn>TranslateMessage</msdn> and <msdn>DispatchMessage</msdn>.
		/// Be careful, this function is as dangerous as <b>Application.DoEvents</b>.
		/// </remarks>
		public static void doEvents() {
			while (Api.PeekMessage(out var m, default, 0, 0, Api.PM_REMOVE)) {
				//WndUtil.PrintMsg(m);
				if (m.message == Api.WM_QUIT) { Api.PostQuitMessage((int)m.wParam); break; }
				Api.TranslateMessage(m);
				Api.DispatchMessage(m);
			}
		}

		/// <summary>
		/// Temporarily changes the time resolution/precision of Thread.Sleep and some other functions.
		/// </summary>
		/// <remarks>
		/// Uses API <msdn>timeBeginPeriod</msdn>, which requests a time resolution for various system timers and wait functions. Actually it is the system thread scheduling timer period.
		/// Normal resolution on Windows 7-10 is 15.625 ms. It means that, for example, <c>Thread.Sleep(1);</c> sleeps not 1 but 1-15 ms. If you set resolution 1, it sleeps 1-2 ms.
		/// The new resolution is revoked (<msdn>timeEndPeriod</msdn>) when disposing the SleepPrecision_ variable or when this process ends. See example. See also <see cref="TempSet1"/>.
		/// The resolution is applied to all threads and processes. Other applications can change it too. For example, often web browsers temporarily set resolution 1 ms when opening a web page.
		/// The system uses the smallest period (best resolution) that currently is set by any application. You cannot make it bigger than current value.
		/// <note>It is not recommended to keep small period (high resolution) for a long time. It can be bad for power saving.</note>
		/// Don't need this for wait.SleepX and functions that use them (mouse.click etc). They call <see cref="TempSet1"/> when the sleep time is 1-99 ms.
		/// This does not change the minimal period of <see cref="timer"/> and System.Windows.Forms.Timer.
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
		internal sealed class SleepPrecision_ : IDisposable
		{
			//info: this class could be public, but probably not useful. wait.ms automatically sets 1 ms period if need.

			int _period;

			/// <summary>
			/// Calls API <msdn>timeBeginPeriod</msdn>.
			/// </summary>
			/// <param name="periodMS">
			/// New system timer period, milliseconds.
			/// Should be 1. Other values may stuck and later cannot be made smaller due to bugs in OS or some applications; this bug would impact many functions of this library.
			/// </param>
			/// <exception cref="ArgumentOutOfRangeException">periodMS &lt;= 0.</exception>
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
			/// The return value usually is between 0.5 and 15.625 milliseconds. Returns 0 if fails.
			/// </summary>
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
			//never mind: finalizer is not called on process exit. Not a problem, because OS clears our set value (tested). Or we could use process.thisProcessExit event.

			/// <summary>
			/// Calls TempSet1 if sleepTimeMS is 1-99.
			/// </summary>
			/// <param name="sleepTimeMS">milliseconds of the caller 'sleep' function.</param>
			internal static void TempSet1_(int sleepTimeMS) {
				if (sleepTimeMS < 100 && sleepTimeMS > 0) TempSet1(1111);
			}
		}
	}
}
