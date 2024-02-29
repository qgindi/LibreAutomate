namespace Au.More;

/// <summary>
/// Wraps a waitable timer handle.
/// </summary>
/// <remarks>
/// More info: API <msdn>CreateWaitableTimer</msdn>.
/// </remarks>
public class WaitableTimer : WaitHandle {
	WaitableTimer(IntPtr h) => SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(h, true);

	/// <summary>
	/// Calls API <msdn>CreateWaitableTimer</msdn> and creates a <b>WaitableTimer</b> object that wraps the timer handle.
	/// </summary>
	/// <param name="manualReset"></param>
	/// <param name="timerName">Timer name. If a timer with this name already exists, opens it if possible. If <c>null</c>, creates unnamed timer.</param>
	/// <exception cref="AuException">Failed. For example, a non-timer kernel object with this name already exists.</exception>
	public static WaitableTimer Create(bool manualReset = false, string timerName = null) {
		var h = Api.CreateWaitableTimer(Api.SECURITY_ATTRIBUTES.ForLowIL, manualReset, timerName);
		if (h.Is0) throw new AuException(0, "*create timer");
		return new WaitableTimer(h);
	}

	/// <summary>
	/// Calls API <msdn>OpenWaitableTimer</msdn> and creates a <b>WaitableTimer</b> object that wraps the timer handle.
	/// </summary>
	/// <param name="timerName">Timer name. Fails if it does not exist; to open-or-create use <see cref="Create"/>.</param>
	/// <param name="access">.See <msdn>Synchronization Object Security and Access Rights</msdn>. The default value <c>TIMER_MODIFY_STATE|SYNCHRONIZE</c> allows to set and wait.</param>
	/// <param name="inheritHandle"></param>
	/// <param name="noException">If fails, return <c>null</c>, don't throw exception. Supports <see cref="lastError"/>.</param>
	/// <exception cref="AuException">Failed. For example, the timer does not exist.</exception>
	public static WaitableTimer Open(string timerName, uint access = Api.TIMER_MODIFY_STATE | Api.SYNCHRONIZE, bool inheritHandle = false, bool noException = false) {
		var h = Api.OpenWaitableTimer(access, inheritHandle, timerName);
		if (h.Is0) {
			var e = lastError.code;
			if (noException) {
				lastError.code = e;
				return null;
			}
			throw new AuException(e, "*open timer");
		}
		return new WaitableTimer(h);
	}

	/// <summary>
	/// Calls API <msdn>SetWaitableTimer</msdn>.
	/// </summary>
	/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
	/// <param name="dueTime">
	/// The time after which the state of the timer is to be set to signaled. It is relative time (from now).
	/// If positive, in milliseconds. If negative, in 100 nanosecond intervals (microseconds*10), see <msdn>FILETIME</msdn>.
	/// Also can be 0, to set minimal time.</param>
	/// <param name="period">The period of the timer, in milliseconds. If 0, the timer is signaled once. If greater than 0, the timer is periodic.</param>
	/// <exception cref="OverflowException"><c>dueTime*10000</c> is greater than <b>long.MaxValue</b>.</exception>
	public bool Set(long dueTime, int period = 0) {
		if (dueTime > 0) dueTime = -checked(dueTime * 10000);
		return Api.SetWaitableTimer(this.SafeWaitHandle.DangerousGetHandle(), ref dueTime, period, default, default, false);
	}

	/// <summary>
	/// Calls API <msdn>SetWaitableTimer</msdn>.
	/// </summary>
	/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
	/// <param name="dueTime">The UTC date/time at which the state of the timer is to be set to signaled.</param>
	/// <param name="period">The period of the timer, in milliseconds. If 0, the timer is signaled once. If greater than 0, the timer is periodic.</param>
	public bool SetAbsolute(DateTime dueTime, int period = 0) {
		var t = dueTime.ToFileTimeUtc();
		return Api.SetWaitableTimer(this.SafeWaitHandle.DangerousGetHandle(), ref t, period, default, default, false);
	}
}
