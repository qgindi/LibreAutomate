namespace Au.More {
	/// <summary>
	/// Can be used to easily implement "wait for" functions with a timeout.
	/// </summary>
	/// <remarks>
	/// See examples. The code works like most "wait for" functions of this library: throws exception when timed out, unless the timeout value is negative.
	/// Similar code is used by <see cref="wait.until"/> and many other "wait for" functions of this library.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// public static bool WaitForMouseLeftButtonDown(Seconds timeout) {
	/// 	var x = new WaitLoop(timeout);
	/// 	for(; ; ) {
	/// 		if(mouse.isPressed(MButtons.Left)) return true;
	/// 		if(!x.Sleep()) return false;
	/// 	}
	/// }
	/// ]]></code>
	/// The same with <b>wait.until</b>.
	/// <code><![CDATA[
	/// static bool WaitForMouseLeftButtonDown2(Seconds timeout) {
	/// 	return wait.until(timeout, () => mouse.isPressed(MButtons.Left));
	/// }
	/// ]]></code>
	/// </example>
	public struct WaitLoop {
		long _timeRemaining, _timePrev;
		bool _hasTimeout, _throw, _doEvents, _precisionIsSet;
		float _step;
		
		/// <summary>
		/// Sets timeout and possibly more wait parameters.
		/// </summary>
		/// <param name="timeout">Timeout in seconds, like <c>3</c> or <c>0.5</c>. Or a <b>Seconds</b> variable containing timeout etc, like <c>new(3, period: 5)</c>. If timeout is 0, will wait indefinitely. If > 0, <see cref="Sleep"/> throws <see cref="TimeoutException"/> when timed out. If &lt; 0, <b>Sleep</b> then returns <c>false</c> instead.</param>
		public WaitLoop(Seconds timeout) {
			Period = timeout.Period ?? 10;
			_step = Period / 10f;
			MaxPeriod = timeout.MaxPeriod ?? Period * 50f;
			_doEvents = timeout.DoEvents ?? opt.wait.DoEvents; //use opt.wait fbc
			Cancel = timeout.Cancel;
			
			double t = timeout.Time;
			if (_hasTimeout = !(t is 0d or > 9223372036854775d or < -9223372036854775d)) { //long.MaxValue/1000 = 292_471_208 years
				_throw = t > 0 && !timeout.noException_;
				_timeRemaining = (long)(Math.Abs(t) * 1000d);
				_timePrev = computer.tickCountWithoutSleep;
			}
		}
		
		/// <param name="secondsTimeout">Timeout in seconds. If 0, will wait indefinitely. If > 0, <see cref="Sleep"/> throws <see cref="TimeoutException"/> when timed out. If &lt; 0, <b>Sleep</b> then returns <c>false</c> instead.</param>
		/// <param name="options">Options. If <c>null</c>, uses <b>opt.wait</b>.</param>
		[Obsolete, EditorBrowsable(EditorBrowsableState.Never)]
		public WaitLoop(double secondsTimeout, OWait options) : this(new(secondsTimeout) { Period = (options ?? opt.wait).Period, DoEvents = (options ?? opt.wait).DoEvents }) { }
		
		/// <summary>
		/// Current period (<see cref="Sleep"/> sleep time). Milliseconds.
		/// Initially it is <see cref="Seconds.Period"/>, or 10 ms if it was <c>null</c>. Then each <see cref="Sleep"/> increments it until <see cref="MaxPeriod"/>.
		/// </summary>
		public float Period { get; set; }
		
		/// <summary>
		/// Maximal period (<see cref="Sleep"/> sleep time). Milliseconds.
		/// Initially it is <see cref="Seconds.MaxPeriod"/>, or <see cref="Period"/>*50 if it is <c>null</c> (eg 10*50=500).
		/// </summary>
		public float MaxPeriod { get; set; }
		
		/// <summary>
		/// Gets or sets the remaining time. Milliseconds.
		/// </summary>
		public long TimeRemaining { get => _timeRemaining; set => _timeRemaining = value; }
		
		/// <summary>
		/// Calls <see cref="IsTimeout"/>. If it returns <c>true</c>, returns <c>false</c>.
		/// Else sleeps <see cref="Period"/> milliseconds, increments <b>Period</b> if it is less than <see cref="MaxPeriod"/>, and returns <c>true</c>.
		/// </summary>
		/// <exception cref="TimeoutException">The timeout time has expired (if &gt; 0).</exception>
		public unsafe bool Sleep() {
			if (IsTimeout()) return false;
			int t = (int)Period;
			
			if (t < 10 && !_precisionIsSet) { //default Period is 10
				_precisionIsSet = true;
				wait.SleepPrecision_.TempSet1();
			}
			
			if (Cancel.CanBeCanceled && t > 100) { //t > 100: avoid creating Cancel.WaitHandle while period is small. By default will create after ~5 s.
				var h = Cancel.WaitHandle.SafeWaitHandle.DangerousGetHandle();
				int r = wait.Wait_(t, _doEvents ? WHFlags.DoEvents : 0, new ReadOnlySpan<IntPtr>(&h, 1));
				if (r == 0) Cancel.ThrowIfCancellationRequested();
				if (r != Api.WAIT_TIMEOUT) throw new AuException(0);
			} else {
				Cancel.ThrowIfCancellationRequested();
				if (_doEvents) {
					wait.doEvents(t);
				} else {
					Thread.Sleep(t);
				}
				Cancel.ThrowIfCancellationRequested();
			}
			
			if (Period < MaxPeriod) Period += _step;
			return true;
		}
		
		/// <summary>
		/// If the timeout time is not expired, returns <c>false</c>.
		/// Else if the timeout was negative, returns <c>true</c>.
		/// Else throws <see cref="TimeoutException"/>.
		/// </summary>
		/// <exception cref="TimeoutException"></exception>
		public bool IsTimeout() {
			if (!_hasTimeout) return false;
			var t = computer.tickCountWithoutSleep;
			_timeRemaining -= t - _timePrev;
			_timePrev = t;
			if (_timeRemaining > 0) return false;
			if (_throw) throw new TimeoutException();
			return true;
		}
		
		/// <summary>
		/// Can be used to cancel the wait operation.
		/// </summary>
		public CancellationToken Cancel { get; set; }
	}
}

namespace Au.Types {
	/// <summary>
	/// Used with wait functions. Contains a wait timeout in seconds, and possibly wait options.
	/// </summary>
	/// <remarks>
	/// Many wait functions of this library internally use <see cref="WaitLoop"/>. They have a timeout parameter of <b>Seconds</b> type which allows to pass timeout and more options to <b>WaitLoop</b> in single parameter. You can pass a <b>Seconds</b> variable, like <c>new(3, period: 5)</c>. If don't need options etc, you can pass just timeout, like <c>3</c> or <c>0.5</c>.
	///
	/// Other wait functions have a timeout parameter of <b>Seconds</b> type but instead of <b>WaitLoop</b> use various hooks, events, Windows wait API, etc. They support only these <b>Seconds</b> properties: <b>Time</b>, <b>Cancel</b>, maybe <b>DoEvents</b>. Some always work like with <b>DoEvents</b> <c>true</c>.
	///
	/// More info: [](xref:wait_timeout).
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var w = wnd.find(new Seconds(3) { Period = 5, MaxPeriod = 50 }, "Name");
	/// ]]></code>
	/// <code><![CDATA[
	/// var to = new Seconds(0) { MaxPeriod = 50 };
	/// wait.until(to, () => keys.isCtrl );
	/// wait.until(to with { Time = 30 }, () => keys.isCtrl );
	/// ]]></code>
	/// </example>
	public struct Seconds {
		float _time;
		int _period, _maxPeriod;
		byte _doEvents;
		//bool _inited;
		internal bool noException_;
		//Now Unsafe.SizeOf is 24.
		//	Don't use bool? etc. Nullable<T> takes at least 8 bytes if the struct also has a managed-type field.
		
		/// <summary>
		/// Sets timeout.
		/// Example: <c>var w = wnd.find(new Seconds(3) { Period = 5, MaxPeriod = 50 }, "Name");</c>.
		/// </summary>
		/// <param name="time">
		/// Timeout, in seconds.
		/// Negative value means "don't throw exception when timed out".
		/// Value 0 means "wait indefinitely" when used with <b>WaitX</b> functions; with "FindX" functions it means "don't wait".
		/// More info: [](xref:wait_timeout).
		/// </param>
		public Seconds(double time) {
			_time = (float)time;
			//_inited = true;
		}
		
		///
		public static implicit operator Seconds(double time) => new(time);
		
		/// <summary>
		/// Timeout, in seconds.
		/// Negative value means "don't throw exception when timed out".
		/// Value 0 means "wait indefinitely" when used with <b>WaitX</b> functions; with "FindX" functions it means "don't wait".
		/// More info: [](xref:wait_timeout).
		/// </summary>
		public double Time {
			get => _time;
			set { _time = (float)value; }
		}
		
		/// <summary>
		/// The sleep time between checking the wait condition periodically. Milliseconds.
		/// If <c>null</c>, will be used a value that usually is best for that wait function, in most cases 10.
		/// </summary>
		/// <remarks>
		/// Most wait functions of this library use <see cref="WaitLoop"/>, which repeatedly checks the wait condition and sleeps (waits) several ms. This property sets the initial sleep time (<see cref="WaitLoop.Period"/>), which then is incremented by <c>Period/10</c> ms in each loop until reaches <see cref="WaitLoop.MaxPeriod"/>, which is <c>Period*50</c> by default.
		/// This property makes the response time shorter or longer. If less than the default value, makes it shorter (faster response), but increases CPU usage; if greater, makes it longer (slower response).
		/// </remarks>
		public int? Period {
			get => _period != 0 ? _period : null;
			set { _period = value.HasValue ? Math.Max(1, value.Value) : 0; }
		}
		
		/// <summary>
		/// Sets <see cref="WaitLoop.MaxPeriod"/>.
		/// If <c>null</c> (default), it will use <c>Period*50</c>.
		/// </summary>
		public int? MaxPeriod {
			get => _maxPeriod != 0 ? _maxPeriod : null;
			set { _maxPeriod = value.HasValue ? Math.Max(1, value.Value) : 0; }
		}
		
		/// <summary>
		/// Use <see cref="wait.doEvents(int)"/> instead of <see cref="wait.ms"/>.
		/// If <c>null</c>, will be used <c>false</c>.
		/// </summary>
		public bool? DoEvents {
			get => _doEvents != 0 ? _doEvents == 1 : null;
			set { _doEvents = value switch { true => 1, false => 2, _ => 0 }; }
		}
		
		/// <summary>
		/// Can be used to cancel the wait operation.
		/// </summary>
		public CancellationToken Cancel { get; set; }
		
		///// <summary>
		///// Returns <c>true</c> if ctor wasn't called.
		///// </summary>
		//internal bool IsNull_ => !_inited;
		
		/// <summary>
		/// Sets <c>noException_ = true</c> and returns <c>Time == 0d</c>.
		/// </summary>
		internal bool Exists_() {
			noException_ = true;
			return _time == 0d;
		}
		
		/// <summary>
		/// If <c>Time &lt; 0</c>, returns <c>false</c>, else throws <b>NotFoundException</b>.
		/// </summary>
		internal bool ReturnFalseOrThrowNotFound_() => Time < 0 ? false : throw new NotFoundException();
		//internal T ReturnOrThrowNotFound_<T>(T def) => Time < 0 ? def : throw new NotFoundException();
	}
}
