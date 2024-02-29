namespace Au.More;

/// <summary>
/// Implements "single instance process" feature.
/// </summary>
/// <example>
/// <code><![CDATA[
/// /*/ role exeProgram; ifRunning run; /*/
/// if (script.testing) args = ["test", "args"];
/// 
/// if (AppSingleInstance.AlreadyRunning("unique-mutex-name", args)) {
/// 	print.it("already running");
/// 	return;
/// }
/// 
/// var b = new wpfBuilder("Window").WinSize(400);
/// b.R.AddOkCancel();
/// b.End();
/// AppSingleInstance.Notified += a => {
/// 	print.it("AppSingleInstance.Notified", a);
/// 	b.Window.Activate();
/// };
/// if (!b.ShowDialog()) return;
/// ]]></code>
/// </example>
/// <summary>
/// Implements "single instance process" feature.
/// </summary>
/// <seealso cref="script.single"/>
public static class AppSingleInstance {
	static Mutex _mutex;
	static wnd _wNotify;
	
	/// <summary>
	/// Detects whether a process of this app is already running.
	/// </summary>
	/// <param name="mutex">A unique string to use for mutex name (see <see cref="Mutex(bool, string, out bool)"/>). If prefix <c>@"Global\"</c> used, detects processes in all user sessions.</param>
	/// <param name="notifyArgs">
	/// If not <c>null</c>:
	/// <br/>• If already running, sends it to that process, which receives it in <see cref="Notified"/> event.
	/// <br/>• Else enables <b>Notified</b> event in this process.
	/// </param>
	/// <param name="waitMS">Milliseconds to wait until this process can run. No timeout if -1.</param>
	/// <returns>True if already running.</returns>
	/// <exception cref="InvalidOperationException">This function already called.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="Mutex(bool, string, out bool)"/>.</exception>
	public static bool AlreadyRunning(string mutex, IEnumerable<string> notifyArgs = null, int waitMS = 0) {
		var m = new Mutex(true, mutex, out bool createdNew);
		if (null != Interlocked.CompareExchange(ref _mutex, m, null)) { m.Dispose(); throw new InvalidOperationException(); }
		
		if (!createdNew && waitMS != 0) {
			try { createdNew = m.WaitOne(waitMS); }
			catch (AbandonedMutexException) { createdNew = true; }
		}
		
		if (notifyArgs != null) {
			if (createdNew) {
				WndUtil.RegisterWindowClass(mutex, _WndProc);
				_wNotify = WndUtil.CreateMessageOnlyWindow(mutex, "AppSingleInstance");
				WndCopyData.EnableReceivingWM_COPYDATA();
			} else {
				var w = wnd.findFast("AppSingleInstance", mutex, messageOnly: true);
				if (!w.Is0) WndCopyData.Send<char>(w, 1, string.Join('\0', notifyArgs));
			}
		}
		
		return !createdNew;
	}
	
	static nint _WndProc(wnd w, int msg, nint wp, nint lp) {
		if (msg == Api.WM_COPYDATA) {
			var x = new WndCopyData(lp);
			if (x.DataId == 1) {
				Notified?.Invoke(x.GetString().Split('\0'));
			}
		}
		return Api.DefWindowProc(w, msg, wp, lp);
	}
	
	/// <summary>
	/// When <see cref="AlreadyRunning"/> in new process detected that this process is running.
	/// Receives <i>notifyArgs</i> passed to it.
	/// </summary>
	/// <remarks>
	/// To enable this event, call <see cref="AlreadyRunning"/> with non-<c>null</c> <i>notifyArgs</i>. The event handler runs in the same thread. The thread must dispatch Windows messages (for example show a window or dialog, or call <see cref="wait.doEvents(int)"/>).
	/// </remarks>
	public static event Action<string[]> Notified;
}
