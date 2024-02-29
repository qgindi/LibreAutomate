namespace Au;

public partial struct wnd {
	/// <summary>
	/// Waits until window exists or is active.
	/// </summary>
	/// <returns>Window handle. On timeout returns <c>default(wnd)</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="active">The window must be the active window (<see cref="active"/>), and not minimized.</param>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <exception cref="ArgumentException" />
	/// <remarks>
	/// Parameters etc are the same as <see cref="find"/>.
	/// By default ignores invisible and cloaked windows. Use <i>flags</i> if need.
	/// If you have a window's <b>wnd</b> variable, to wait until it is active/visible/etc use <see cref="WaitFor"/> instead.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// wnd w = wnd.wait(10, false, "* Notepad");
	/// print.it(w);
	/// ]]></code>
	/// Using in a WPF window with async/await.
	/// <code><![CDATA[
	/// using System.Windows;
	/// var b = new wpfBuilder("Window").WinSize(250);
	/// b.R.AddButton("Wait", async _ => {
	/// 	  print.it("waiting for Notepad...");
	/// 	  wnd w = await Task.Run(() => wnd.wait(-10, false, "* Notepad"));
	/// 	  if(w.Is0) print.it("timeout"); else print.it(w);
	/// });
	/// if (!b.ShowDialog()) return;
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="find"/>
	public static wnd wait(Seconds timeout, bool active,
		[ParamString(PSFormat.Wildex)] string name = null,
		[ParamString(PSFormat.Wildex)] string cn = null,
		[ParamString(PSFormat.Wildex)] WOwner of = default,
		WFlags flags = 0, Func<wnd, bool> also = null, WContains contains = default
		) => new wndFinder(name, cn, of, flags, also, contains).Wait(timeout, active);
	
	/// <summary>
	/// Waits until any of specified windows exists or is active.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="active">The window must be the active window (<see cref="active"/>), and not minimized.</param>
	/// <param name="windows">Specifies windows, like <c>new("Window1"), new("Window2")</c>.</param>
	/// <returns>1-based index and window handle. On timeout returns <c>(0, default(wnd))</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <remarks>
	/// By default ignores invisible and cloaked windows. Use <b>wndFinder</b> flags if need.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var (i, w) = wnd.waitAny(10, true, "* Notepad", new wndFinder("* Word"));
	/// print.it(i, w);
	/// ]]></code>
	/// </example>
	public static (int index, wnd w) waitAny(Seconds timeout, bool active, params wndFinder[] windows) {
		foreach (var f in windows) f.Result = default;
		WFCache cache = active && windows.Length > 1 ? new WFCache() : null;
		var loop = new WaitLoop(timeout);
		for (; ; ) {
			if (active) {
				wnd w = wnd.active;
				for (int i = 0; i < windows.Length; i++) {
					if (windows[i].IsMatch(w, cache) && !w.IsMinimized) return (i + 1, w);
				}
			} else {
				for (int i = 0; i < windows.Length; i++) {
					var f = windows[i];
					if (f.Exists()) return (i + 1, f.Result);
				}
				//FUTURE: optimization: get list of windows once (Lib.EnumWindows2).
				//	Problem: list filtering depends on wndFinder flags. Even if all finders have same flags, its easy to make bugs.
			}
			if (!loop.Sleep()) return default;
		}
	}
	
	//rejected. Not useful. Use the non-static WaitForClosed.
	//		/// <summary>
	//		/// Waits until window does not exist.
	//		/// </summary>
	//		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	//		/// <returns>Returns true. On timeout returns false if <i>timeout</i> is negative; else exception.</returns>
	//		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	//		/// <exception cref="Exception">Exceptions of <see cref="Find"/>.</exception>
	//		/// <remarks>
	//		/// Parameters etc are the same as <see cref="Find"/>.
	//		/// By default ignores invisible and cloaked windows. Use flags if need.
	//		/// If you have a window's wnd variable, to wait until it is closed use <see cref="WaitForClosed"/> instead.
	//		/// Examples: <see cref="Wait"/>.
	//		/// </remarks>
	//		public static bool waitNot(Seconds timeout,
	//			[ParamString(PSFormat.wildex)] string name = null,
	//			[ParamString(PSFormat.wildex)] string cn = null,
	//			[ParamString(PSFormat.wildex)] WOwner of = default,
	//			WFlags flags = 0, Func<wnd, bool> also = null, WContents contains = default)
	//		{
	//			var f = new wndFinder(name, cn, of, flags, also, contains);
	//			return WaitNot(timeout, out _, f);
	//		}
	
	//		/// <summary>
	//		/// Waits until window does not exist.
	//		/// </summary>
	//		/// <param name="timeout"></param>
	//		/// <param name="wFound">On timeout receives the first found matching window that exists.</param>
	//		/// <param name="f">Window properties etc. Can be string, see <see cref="wndFinder.op_Implicit(string)"/>.</param>
	//		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	//		public static bool waitNot(Seconds timeout, out wnd wFound, wndFinder f)
	//		{
	//			wFound = default;
	//			var to = new WaitLoop(timeout);
	//			wnd w = default;
	//			for(; ; ) {
	//				if(!w.IsAlive || !f.IsMatch(w)) { //if first time, or closed (!IsAlive), or changed properties (!IsMatch)
	//					if(!f.Exists()) { wFound = default; return true; }
	//					wFound = w = f.Result;
	//				}
	//				if(!to.Sleep()) return false;
	//			}
	//		}
	
	//rejected. Cannot use implicit conversion string to wndFinder.
	//public static bool waitNot(Seconds timeout, wndFinder f)
	//	=> WaitNot(timeout, out _, f);
	
	//Not often used. It's easy with await Task.Run. Anyway, need to provide an example of similar size.
	//public static async Task<wnd> waitAsync(Seconds timeout, string name)
	//{
	//	return await Task.Run(() => wait(timeout, name));
	//}
	
	/// <summary>
	/// Waits for a user-defined state/condition of this window. For example active, visible, enabled, closed, contains something.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="condition">Callback function (eg lambda). It is called repeatedly, until returns a value other than <c>default(T)</c>, for example <c>true</c>.</param>
	/// <param name="dontThrowIfClosed">
	/// Do not throw exception when the window handle is invalid or the window was closed while waiting.
	/// In such case the callback function must return a non-default value, like in examples with <see cref="IsAlive"/>. Else exception is thrown (with a small delay) to prevent infinite waiting.
	/// </param>
	/// <returns>Returns the value returned by the callback function. On timeout returns <c>default(T)</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <exception cref="AuWndException">The window handle is invalid or the window was closed while waiting.</exception>
	/// <example>
	/// <code><![CDATA[
	/// wnd w = wnd.find("* Notepad");
	/// 
	/// //wait max 30 s until window w is active. Exception on timeout or if closed.
	/// w.WaitFor(30, t => t.IsActive);
	/// print.it("active");
	/// 
	/// //wait max 30 s until window w is enabled. Exception on timeout or if closed.
	/// w.WaitFor(30, t => t.IsEnabled);
	/// print.it("enabled");
	/// 
	/// //wait until window w is closed
	/// w.WaitFor(0, t => !t.IsAlive, true); //same as w.WaitForClosed()
	/// print.it("closed");
	/// 
	/// //wait until window w is minimized or closed
	/// w.WaitFor(0, t => t.IsMinimized || !t.IsAlive, true);
	/// if(!w.IsAlive) { print.it("closed"); return; }
	/// print.it("minimized");
	/// 
	/// //wait until window w contains focused control classnamed "Edit"
	/// var c = new wndChildFinder(cn: "Edit");
	/// w.WaitFor(10, t => c.Exists(t) && c.Result.IsFocused);
	/// print.it("control focused");
	/// ]]></code>
	/// </example>
	public T WaitFor<T>(Seconds timeout, Func<wnd, T> condition, bool dontThrowIfClosed = false) {
		bool wasInvalid = false;
		var loop = new WaitLoop(timeout);
		for (; ; ) {
			if (!dontThrowIfClosed) ThrowIfInvalid();
			T r = condition(this);
			if (!EqualityComparer<T>.Default.Equals(r, default)) return r;
			if (dontThrowIfClosed) {
				if (wasInvalid) ThrowIfInvalid();
				wasInvalid = !IsAlive;
			}
			if (!loop.Sleep()) return default;
		}
	}
	
	/// <summary>
	/// Waits until this window has the specified name.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="name">
	/// Window name. Usually it is the title bar text.
	/// String format: [wildcard expression](xref:wildcard_expression).
	/// </param>
	/// <param name="not">Wait until this window does not have the specified name.</param>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <exception cref="AuWndException">The window handle is invalid or the window was closed while waiting.</exception>
	/// <exception cref="ArgumentException">Invalid wildcard expression.</exception>
	public bool WaitForName(Seconds timeout, [ParamString(PSFormat.Wildex)] string name, bool not = false) {
		wildex x = name; //ArgumentNullException, ArgumentException
		return WaitFor(timeout, t => x.Match(t.Name) != not);
	}
	
	/// <summary>
	/// Waits until this window is closed/destroyed or until its process ends.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="waitUntilProcessEnds">Wait until the process of this window ends.</param>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <exception cref="AuException">Failed to open process handle when <i>waitUntilProcessEnds</i> is <c>true</c>.</exception>
	/// <remarks>
	/// If the window is already closed, immediately returns <c>true</c>.
	/// </remarks>
	public bool WaitForClosed(Seconds timeout, bool waitUntilProcessEnds = false) {
		if (!waitUntilProcessEnds) return WaitFor(timeout, t => !t.IsAlive, true);
		
		//SHOULDDO: if window of this thread or process...
		
		if (!IsAlive) return true;
		using var ph = Handle_.OpenProcess(this, Api.SYNCHRONIZE);
		if (ph.Is0) {
			var e = new AuException(0, "*open process handle"); //info: with SYNCHRONIZE can open process of higher IL
			if (!IsAlive) return true;
			throw e;
		}
		return 0 != Au.wait.forHandle(timeout, 0, ph);
	}
}
