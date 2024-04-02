namespace Au;

/// <summary>
/// Timer that calls callback function in other thread (thread pool) and can be used in any thread.
/// </summary>
/// <remarks>
/// Uses <see cref="Timer"/>.
/// Unlike <see cref="timer"/>, the thread that sets the timer does not have to retrieve/dispatch messages.
/// The callback function is called in a random thread of the thread pool, therefore its code is not thread-safe (may need to lock etc).
/// The actual minimal time interval/period is 10-20 ms, because the system timer period usually is 15.25 ms.
/// Timer action delegates are protected from GC.
/// </remarks>
/// <example>
/// This example sets 3 timers.
/// <code><![CDATA[
/// timer2.after(500, _ => print.it("after 500 ms"));
/// timer2.every(1000, _ => print.it("every 1000 ms"));
/// var t3 = new timer2(_ => print.it("after 3000 ms")); t3.After(3000); //the same as timer2.after
/// 5.s();
/// ]]></code>
/// </example>
public class timer2 {
	readonly Timer _tim;
	readonly Action<timer2> _action;
	
	/// <summary>
	/// Sets callback function.
	/// </summary>
	public timer2(Action<timer2> timerAction) {
		_action = timerAction;
		_tim = new Timer(o => {
			var t = o as timer2;
			try { t._action(t); }
			catch (Exception ex) { print.warning(ex); }
		}, this, -1, -1);
		//note: don't pass dueTime/period to Timer ctor. Could call callback before _tim is assigned.
		
		//s_cwt.Add(_tim, new());
		//_tim = null;
	}
	
	//GC can collect either when explicitly stopped or when 'after' timer completes (somehow). Only after the callback returns.
	//~timer2() { print.it("fin"); }
	//static ConditionalWeakTable<Timer, gc> s_cwt=new();
	//class gc { ~gc() { print.it("gc"); } }
	
	/// <summary>
	/// Stops the timer, and by default disposes.
	/// </summary>
	/// <param name="canReuse">Just stop but don't dispose. If <c>false</c> (default), can't use the timer again.</param>
	public void Stop(bool canReuse = false) {
		if (canReuse) _tim.Change(-1, -1);
		else _tim.Dispose();
	}
	
	/// <summary>
	/// Starts one-time timer or changes timeout/period.
	/// </summary>
	/// <param name="milliseconds">Time interval after which to call the callback function. Valid values are 0 - <b>uint.MaxValue</b> - 2. If -1, stops without disposing.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ObjectDisposedException">Called <see cref="Stop"/> (unless <i>canReuse</i> true).</exception>
	/// <remarks>
	/// Calls <see cref="Timer.Change(long, long)"/>.
	/// </remarks>
	public void After(long milliseconds) {
		_tim.Change(milliseconds, -1);
	}
	
	/// <summary>
	/// Starts periodic timer or changes timeout/period.
	/// </summary>
	/// <param name="milliseconds">Time interval (period) of calling the callback function. Valid values are 0 - <b>uint.MaxValue</b> - 2.</param>
	/// <param name="firstAfter"><c>null</c> (default) or time interval after which to call the callback function first time. Valid values are 0 - <b>uint.MaxValue</b> - 2.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ObjectDisposedException">Called <see cref="Stop"/> (unless <i>canReuse</i> true).</exception>
	/// <remarks>
	/// Calls <see cref="Timer.Change(long, long)"/>.
	/// </remarks>
	public void Every(long milliseconds, long? firstAfter = null) {
		_tim.Change(firstAfter ?? milliseconds, milliseconds);
	}
	
	/// <summary>
	/// Something to attach to this variable.
	/// </summary>
	public object Tag { get; set; }
	
	/// <summary>
	/// Creates and starts new one-time timer.
	/// </summary>
	/// <param name="milliseconds">Time interval after which to call the callback function. Valid values are 0 - <b>uint.MaxValue</b> - 2. If -1, stops without disposing.</param>
	/// <param name="timerAction">Callback function.</param>
	/// <param name="tag">Something to pass to the callback function as <see cref="Tag"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <remarks>
	/// Calls <see cref="Timer.Change(long, long)"/>.
	/// </remarks>
	public static timer2 after(long milliseconds, Action<timer2> timerAction, object tag = null) {
		var t = new timer2(timerAction) { Tag = tag };
		t.After(milliseconds);
		return t;
	}
	
	/// <summary>
	/// Creates and starts new periodic timer.
	/// </summary>
	/// <param name="milliseconds">Time interval (period) of calling the callback function. Valid values are 0 - <b>uint.MaxValue</b> - 2.</param>
	/// <param name="timerAction">Callback function.</param>
	/// <param name="tag">Something to pass to the callback function as <see cref="Tag"/>.</param>
	/// <param name="firstAfter"><c>null</c> (default) or time interval after which to call the callback function first time. Valid values are 0 - <b>uint.MaxValue</b> - 2.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <remarks>
	/// Calls <see cref="Timer.Change(long, long)"/>.
	/// </remarks>
	public static timer2 every(long milliseconds, Action<timer2> timerAction, object tag = null, long? firstAfter = null) {
		var t = new timer2(timerAction) { Tag = tag };
		t.Every(milliseconds, firstAfter);
		return t;
	}
}
