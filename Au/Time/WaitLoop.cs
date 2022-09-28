namespace Au.More;

/// <summary>
/// Can be used to easily implement 'wait for' functions with a timeout.
/// </summary>
/// <remarks>
/// See examples. The code works like most 'wait for' functions of this library: on timeout throws exception, unless <i>secondsTimeout</i> is negative.
/// Similar code is used by most 'wait for' functions of this library.
/// See also <see cref="wait.forCondition"/>; usually it's easier; internally it uses similar code too.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// public static bool WaitForMouseLeftButtonDown(double secondsTimeout) {
/// 	var x = new WaitLoop(secondsTimeout);
/// 	for(; ; ) {
/// 		if(mouse.isPressed(MButtons.Left)) return true;
/// 		if(!x.Sleep()) return false;
/// 	}
/// }
/// ]]></code>
/// The same with <b>wait.forCondition</b>.
/// <code><![CDATA[
/// static bool WaitForMouseLeftButtonDown2(double secondsTimeout) {
/// 	return wait.forCondition(secondsTimeout, () => mouse.isPressed(MButtons.Left));
/// }
/// ]]></code>
/// </example>
public struct WaitLoop {
	long _timeRemaining, _timePrev;
	bool _hasTimeout, _throw, _doEvents, _precisionIsSet;
	float _step;

	/// <summary>
	/// Current period (<see cref="Sleep"/> sleep time), milliseconds.
	/// Initially it is <see cref="OWait.Period"/>, optionally multiplied by constructor's <c>options.Period/10</c>. Default 10 ms. Then each <see cref="Sleep"/> increments it until <see cref="MaxPeriod"/>.
	/// </summary>
	public float Period { get; set; }

	/// <summary>
	/// Maximal period (<see cref="Sleep"/> sleep time), milliseconds.
	/// It is <see cref="Period"/>*50 (default 500).
	/// </summary>
	public float MaxPeriod { get; set; }

	/// <summary>
	/// Gets or sets the remaining time, milliseconds.
	/// </summary>
	public long TimeRemaining { get => _timeRemaining; set => _timeRemaining = value; }

	/// <param name="secondsTimeout">
	/// The maximal time to wait, seconds. If 0, waits infinitely. If &gt;0, after that time interval <see cref="Sleep"/> throws <see cref="TimeoutException"/>. If &lt;0, then <see cref="Sleep"/> returns false.
	/// </param>
	/// <param name="options">Options. If null, uses <see cref="opt.wait"/>, else combines with it.</param>
	public WaitLoop(double secondsTimeout, OWait options = null) {
		var to = opt.wait;
		Period = to.Period;
		_doEvents = to.DoEvents;
		if (options != null) {
			Period = Period * options.Period / 10f;
			if (options.DoEvents) _doEvents = true;
		}
		Period = Math.Max(Period, 1f);
		MaxPeriod = Period * 50f;
		_step = Period / 10f;

		if (secondsTimeout == 0d || secondsTimeout > 9223372036854775d || secondsTimeout < -9223372036854775d) { //long.MaxValue/1000 = 292_471_208 years
			_hasTimeout = _throw = false;
			_timeRemaining = _timePrev = 0;
		} else {
			_hasTimeout = true;
			if (secondsTimeout > 0) _throw = true; else { _throw = false; secondsTimeout = -secondsTimeout; }
			_timeRemaining = (long)(secondsTimeout * 1000d);
			_timePrev = computer.tickCountWithoutSleep;
		}
		_precisionIsSet = false;
	}

	/// <summary>
	/// Calls <see cref="IsTimeout"/>. If it returns true, returns false.
	/// Else sleeps for <see cref="Period"/> milliseconds, increments <b>Period</b> if it is less than <see cref="MaxPeriod"/>, and returns true.
	/// </summary>
	/// <exception cref="TimeoutException">The <i>secondsTimeout</i> time has expired (if &gt; 0).</exception>
	public bool Sleep() {
		if (IsTimeout()) return false;

		if (Period < 9.9f && !_precisionIsSet) { //default Period is 10
			_precisionIsSet = true;
			wait.SleepPrecision_.TempSet1();
		}

		int t = (int)Period;
		if (_doEvents) {
			wait.SleepDoEvents_(t, noSetPrecision: true);
		} else {
			Thread.Sleep(t);
		}

		if (Period < MaxPeriod) Period += _step;
		return true;
	}

	/// <summary>
	/// If the <i>secondsTimeout</i> time is not expired, returns false.
	/// Else if <i>secondsTimeout</i> is negative, returns true.
	/// Else throws <see cref="TimeoutException"/>.
	/// </summary>
	/// <exception cref="TimeoutException">The <i>secondsTimeout</i> time has expired (if &gt; 0).</exception>
	public bool IsTimeout() {
		if (!_hasTimeout) return false;
		var t = computer.tickCountWithoutSleep;
		_timeRemaining -= t - _timePrev;
		_timePrev = t;
		if (_timeRemaining > 0) return false;
		if (_throw) throw new TimeoutException();
		return true;
	}
}
