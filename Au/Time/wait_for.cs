namespace Au {
	public static partial class wait {
		/// <summary>
		/// Waits for a user-defined condition. Until the callback function returns a value other than <c>default(T)</c>, for example <c>true</c>.
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="condition">Callback function (eg lambda). It is called repeatedly, until returns a value other than <c>default(T)</c>. Default period is 10, and can be changed like <c>wait.until(new(10) { Timeout = 100 }</c>.</param>
		/// <returns>Returns the value returned by the callback function. On timeout returns <c>default(T)</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <example>See <see cref="wait"/>.</example>
		public static T until<T>(Seconds timeout, Func<T> condition) {
			var loop = new WaitLoop(timeout);
			for (; ; ) {
				T r = condition();
				if (!EqualityComparer<T>.Default.Equals(r, default)) return r;
				if (!loop.Sleep()) return r;
			}
		}
		
		/// <summary>
		/// Obsolete. Use <b>wait.until</b>.
		/// Waits for a user-defined condition. Until the callback function returns a value other than <c>default(T)</c>, for example <c>true</c>.
		/// </summary>
		/// <param name="secondsTimeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="condition">Callback function (eg lambda). It is called repeatedly, until returns a value other than <c>default(T)</c>. The calling period depends on <i>options</i>.</param>
		/// <param name="options">Options. If <c>null</c>, uses <b>opt.wait</b>.</param>
		/// <returns>Returns the value returned by the callback function. On timeout returns <c>default(T)</c> if <i>secondsTimeout</i> is negative; else exception.</returns>
#if DEBUG
		[Obsolete]
#endif
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static T forCondition<T>(double secondsTimeout, Func<T> condition, OWait options = null) {
#pragma warning disable CS0612 // Type or member is obsolete
			var loop = new WaitLoop(secondsTimeout, options);
#pragma warning restore CS0612 // Type or member is obsolete
			for (; ; ) {
				T r = condition();
				if (!EqualityComparer<T>.Default.Equals(r, default)) return r;
				if (!loop.Sleep()) return r;
			}
		}
		
		/// <summary>
		/// Waits for a kernel object (event, mutex, etc).
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="flags"></param>
		/// <param name="handles">One or more handles of kernel objects. Max 63.</param>
		/// <returns>
		/// Returns 1-based index of the first signaled handle. Negative if abandoned mutex.
		/// On timeout returns 0 if <i>timeout</i> is negative; else exception.
		/// </returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <exception cref="AuException">Failed. For example a handle is invalid.</exception>
		/// <remarks>
		/// Uses API <msdn>WaitForMultipleObjectsEx</msdn> or <msdn>MsgWaitForMultipleObjectsEx</msdn>. Alertable.
		/// Does not use <see cref="WaitLoop"/> and <b>Seconds.Period/MaxPeriod</b>.
		/// </remarks>
		public static int forHandle(Seconds timeout, WHFlags flags, params IntPtr[] handles) {
			return WaitS_(timeout, flags, handles);
		}
		
		/// <summary>
		/// Waits for <i>handles</i>, or/and <i>msgCallback</i> returning <c>true</c>, or/and <i>stopVar</i> becoming <c>true</c>.
		/// Calls <see cref="Wait_"/>.
		/// </summary>
		/// <returns>
		/// <br/>• 0 if timeout (if <i>timeout</i> &lt; 0),
		/// <br/>• 1-handles.Length if signaled,
		/// <br/>• -(1-handles.Length) if abandoned mutex,
		/// <br/>• 1+handles.Length if msgCallback returned <c>true</c>,
		/// <br/>• 2+handles.Length if stop became <c>true</c>.
		/// </returns>
		internal static int WaitS_(Seconds timeout, WHFlags flags, IntPtr[] handles = null, Delegate msgCallback = null, WaitVariable_ stopVar = null) {
			if (timeout.Period != null || timeout.MaxPeriod != null) print.warning("This wait function does not use Seconds.Period/MaxPeriod.");
			if (flags.Has(WHFlags.DoEvents)) {
				if (timeout.DoEvents != null) print.warning("This wait function does not use Seconds.DoEvents. It always works like if it is true.");
			} else if (timeout.DoEvents == true) flags |= WHFlags.DoEvents;
			
			long timeMS = _TimeoutS2MS(timeout, out bool canThrow);
			
			int r = Wait_(timeMS, flags, handles, msgCallback, stopVar, timeout.Cancel);
			if (r < 0) throw new AuException(0);
			if (r == Api.WAIT_TIMEOUT) {
				if (canThrow) throw new TimeoutException();
				return 0;
			}
			r++; if (r > Api.WAIT_ABANDONED_0) r = -r;
			return r;
		}
		
		static long _TimeoutS2MS(Seconds timeout, out bool canThrow) {
			canThrow = false;
			var t = timeout.Time;
			if (t == 0) return -1;
			if (t < 0) t = -t; else canThrow = true;
			return checked((long)(t * 1000d));
		}
		
		/// <summary>
		/// Waits for <i>handles</i>, or/and <i>msgCallback</i> returning <c>true</c>, or/and <i>stopVar</i> becoming <c>true</c>. Or just sleeps, if <i>handles</i> etc are <c>null</c>/empty.
		/// If flag <b>DoEvents</b>, dispatches received messages etc.
		/// Calls API <msdn>WaitForMultipleObjectsEx</msdn> or <msdn>MsgWaitForMultipleObjectsEx</msdn> with QS_ALLINPUT. Alertable.
		/// </summary>
		/// <param name="msgCallback">
		/// Called when dispatching messages. If returns <c>true</c>, stops waiting and returns <c>handles.Length</c>.
		/// 	If it is WPMCallback, calls it before dispatching a posted message.
		/// 	If it is Func{bool}, calls it after dispatching one or more messages.
		/// </param>
		/// <param name="stopVar">When becomes <c>true</c>, stops waiting and returns <c>handles.Length + 1</c>.</param>
		/// <returns>
		/// When a handle becomes signaled, returns its 0-based index. If abandoned mutex, returns 0-based index + Api.WAIT_ABANDONED_0 (0x80).
		/// If timeMS>0, waits max timeMS and on timeout returns Api.WAIT_TIMEOUT.
		/// If failed, returns -1. Supports <see cref="lastError"/>.
		/// </returns>
		internal static unsafe int Wait_(long timeMS, WHFlags flags, ReadOnlySpan<IntPtr> handles = default, Delegate msgCallback = null, WaitVariable_ stopVar = null, CancellationToken cancel = default) {
			//rejected. Don't complicate code to implement rarely used features.
			//if (cancel.CanBeCanceled) {
			//	var ch = cancel.WaitHandle.SafeWaitHandle.DangerousGetHandle();
			//	handles = handles == null ? new[] { ch } : handles.InsertAt(-1, ch);
			//}
			
			bool doEvents = flags.Has(WHFlags.DoEvents);
			Debug.Assert(doEvents || (msgCallback == null && stopVar == null));
			int nHandles = handles.Length;
			bool all = flags.Has(WHFlags.All) && nHandles > 1;
			
			using var mp = new MessagePump_();
			fixed (IntPtr* ha = handles) {
				for (long timePrev = 0; ;) {
					cancel.ThrowIfCancellationRequested();
					if (stopVar != null && stopVar.waitVar) return nHandles + 1;
					
					int timeSlice = all && doEvents ? 50 : cancel.CanBeCanceled ? 250 : 5000;
					if (timeMS > 0) {
						long timeNow = computer.tickCountWithoutSleep;
						if (timePrev > 0) timeMS -= timeNow - timePrev;
						if (timeMS <= 0) return Api.WAIT_TIMEOUT;
						if (timeSlice > timeMS) timeSlice = (int)timeMS;
						timePrev = timeNow;
					} else if (timeMS == 0) timeSlice = 0;
					
					int k;
					if (doEvents && !all) {
						k = Api.MsgWaitForMultipleObjectsEx(nHandles, ha, timeSlice, Api.QS_ALLINPUT, Api.MWMO_ALERTABLE | Api.MWMO_INPUTAVAILABLE);
						if (k == nHandles) { //message, COM, hook, etc
							if (mp.PumpWithCallback(msgCallback)) return nHandles;
							continue;
						}
					} else {
						if (nHandles > 0) k = Api.WaitForMultipleObjectsEx(nHandles, ha, all, timeSlice, true);
						else { k = Api.SleepEx(timeSlice, true); if (k == 0) k = Api.WAIT_TIMEOUT; }
						if (doEvents) if (mp.PumpWithCallback(msgCallback)) return nHandles;
					}
					if (k is not (Api.WAIT_TIMEOUT or Api.WAIT_IO_COMPLETION)) return k; //signaled handle, abandoned mutex, WAIT_FAILED (-1)
				}
			}
		}
		
		/// <summary>
		/// Waits for a posted message received by this thread.
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="callback">Callback function that returns <c>true</c> to stop waiting. More info in Remarks.</param>
		/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <remarks>
		/// While waiting, dispatches Windows messages etc, like <see cref="doEvents(int)"/>. Before dispatching a posted message, calls the callback function. Stops waiting when it returns <c>true</c>. Does not dispatch the message if the function sets the message field = 0.
		/// Does not use <see cref="WaitLoop"/> and <b>Seconds.Period/MaxPeriod/DoEvents</b>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// timer.after(2000, t => { print.it("timer"); });
		/// wait.forPostedMessage(5, (ref MSG m) => { print.it(m); return m.message == 0x113; }); //WM_TIMER
		/// print.it("finished");
		/// ]]></code>
		/// </example>
		public static bool forPostedMessage(Seconds timeout, WPMCallback callback) {
			return 1 == WaitS_(timeout, WHFlags.DoEvents, msgCallback: callback);
		}
		
		/// <summary>
		/// Waits for a condition to be changed while processing messages or other events received by this thread.
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="condition">Callback function that returns <c>true</c> to stop waiting. More info in Remarks.</param>
		/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <remarks>
		/// While waiting, dispatches Windows messages etc, like <see cref="doEvents(int)"/>. After dispatching one or more messages or other events (posted messages, messages sent by other threads, hooks, COM, APC, etc), calls the callback function. Stops waiting when it returns <c>true</c>.
		/// Similar to <see cref="until"/>. Differences: 1. Always dispatches messages etc. 2. Does not call the callback function when there are no messages etc. 3. Does not use <see cref="WaitLoop"/> and <b>Seconds.Period/MaxPeriod/DoEvents</b>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// bool stop = false;
		/// timer.after(2000, t => { print.it("timer"); stop = true; });
		/// wait.doEventsUntil(5, () => stop);
		/// print.it(stop);
		/// ]]></code>
		/// </example>
		public static bool doEventsUntil(Seconds timeout, Func<bool> condition) {
			return 1 == WaitS_(timeout, WHFlags.DoEvents, msgCallback: condition);
		}
		
		/// <summary>
		/// Obsolete. Use <b>wait.doEventsUntil</b>.
		/// </summary>
		/// <inheritdoc cref="doEventsUntil"/>
#if DEBUG
		[Obsolete] //just renamed
#endif
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool forMessagesAndCondition(double secondsTimeout, Func<bool> condition) => doEventsUntil(secondsTimeout, condition);
		
		//rejected. Rarely used; type-limited. Let use wait.until.
		//public static bool forVariable(Seconds timeout, in bool variable, OWait options = null) { }
		
		//FUTURE: add misc wait functions implemented using WindowsHook and WinEventHook.
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="wait.forHandle"/>
	/// </summary>
	[Flags]
	public enum WHFlags {
		/// <summary>
		/// Wait until all handles are signaled.
		/// </summary>
		All = 1,
		
		/// <summary>
		/// While waiting, dispatch Windows messages, events, hooks etc. Like <see cref="wait.doEvents(int)"/>.
		/// </summary>
		DoEvents = 2,
	}
	
	/// <summary>
	/// Delegate type for <see cref="wait.forPostedMessage"/>.
	/// </summary>
	/// <param name="m">API <msdn>MSG</msdn>.</param>
	public delegate bool WPMCallback(ref MSG m);
	
	/// <summary>
	/// Used with <b>Wait_</b> etc instead of ref bool.
	/// </summary>
	internal class WaitVariable_ {
		public bool waitVar;
	}
}

//CONSIDER: in QM2 these functions are created:
//	WaitForFocus, WaitWhileWindowBusy,
//	WaitForFileReady, WaitForChangeInFolder,
//	ChromeWait, FirefoxWait
//	WaitForTime,
//	these are in System: WaitIdle, WaitForThreads,

//CONSIDER: WaitForFocusChanged
//	Eg when showing Open/SaveAs dialog, the file Edit control receives focus after 200 ms. Sending text to it works anyway, but the script fails if then it clicks OK not with keys (eg with elm).
