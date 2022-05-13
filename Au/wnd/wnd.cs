namespace Au {
	/// <summary>
	/// A variable of <b>wnd</b> type represents a window or control. It is a window handle, also known as HWND.
	/// </summary>
	/// <remarks>
	/// <b>wnd</b> functions can be used with windows and controls of any process/thread. Also can be used with .NET form/control and WPF window class variables, like <c>wnd w=form.Hwnd(); w.Method(...);</c>.
	/// 
	/// There are two main types of windows - top-level windows and controls. Controls are child windows of top-level windows.
	/// 
	/// More window functions are in types named like <b>WndX</b>, in namespace <b>Au.More</b>. They are used mostly in programming, rarely in automation scripts.
	/// 
	/// What happens when a <b>wnd</b> member function fails:
	/// - Functions that get window properties don't throw exceptions. They return false/0/null/empty. Most of them support <see cref="lastError"/>, and it is mentioned in function documentation.
	/// - Many functions that change window properties throw exception. Exceptions are listed in function documentation. Almost all these functions throw only <see cref="AuWndException"/>.
	/// - Other functions that change window properties return false. They are more often used in programming than in automation scripts.
	/// - When a 'find' function does not find the window or control, it returns <c>default(wnd)</c> (window handle 0). Then <see cref="Is0"/> will return true.
	/// - If a function does not follow these rules, it is mentioned in function documentation.
	/// 
	/// Many functions fail if the window's process has a higher [](xref:uac) integrity level (aministrator, uiAccess) than this process, unless this process has uiAccess level. Especially the functions that change window properties. Some functions that still work: <b>Activate</b>, <b>ActivateL</b>, <b>ShowMinimized</b>, <b>ShowNotMinimized</b>, <b>ShowNotMinMax</b>, <b>Close</b>.
	/// 
	/// The <b>wnd</b> type can be used with native Windows API functions without casting. Use <b>wnd</b> for the parameter type in the declaration, like <c>[DllImport(...)] static extern bool NativeFunction(wnd hWnd, ...)</c>.
	/// 
	/// See also: MSDN article <msdn>Window Features</msdn>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// wnd w = wnd.find("* - Notepad");
	/// if(w.Is0) { print.it("window not found"); return; }
	/// w.Activate();
	/// wnd c = w.Child(cn: "Button");
	/// print.it(c.Name);
	/// ]]></code>
	/// </example>
	public unsafe partial struct wnd : IEquatable<wnd>, IComparable<wnd> {
#if false
		/// Why wnd is struct, not class:
		///		Advantages:
		///		- Lightweight. Same as nint (8 or 4 bytes).
		///		- Easier to create overloaded functions that have a window parameter. If it was a class, then a null argument could be ambiguous if eg could also be a string etc.
		///		- When a find-window function does not find the window, calling next function (without checking the return value) does not throw null-reference exception. Instead the function can throw a more specific exception or just return false etc.
		///		- The handle actually already is a reference (to a window object managed by the OS). We don't own the object; we usually don't need to destroy the window finally; it is more like a numeric window id.
		///		- Code where a window argument is default(wnd) is more clear. If it would be null, then it is unclear how the function interprets it: as a 0 handle or as "don't use it". Now if we want a "don't use it" behavior, we'll use an overload.
		///		- In my experience, it makes programming/scripting easier that if it would be a class. Because windows are not found so often (in automation scripts). A find-window function could throw a 'not found' exception, but it is not good (it's easier to check the return value than to use try/catch or throwing/nonthrowing overloads).
		///		- Probably it is not a "bad practice" to have a struct with many member functions, because eg the .NET DateTime is struct.
		///		
		///		Disadvantages:
		///		- Cannot be a base class of other classes. Workaround: Use it as a public field or property of the other class (or struct); in some cases it can be even better, because wnd has very many methods, and the non-inherited methods of that class would be difficult to find; now they are separated, and can be used like x.NewClassMethod() and x.w.WndMethod(); anyway, in most cases we'll need the new window classes only for the functions that they add, not for wnd functions, eg we would use a class ButtonWnd mostly only for button functions, not for general window functions.
		///		- In some cases C# does not allow to call a property-set function. wnd has few such functions, maybe none.
		///		
		//note: don't use :IWin32Window, because it loads System.Windows.Forms.dll always when wnd used.
#endif

		readonly nint _h;

		#region constructors, operators, overrides

#pragma warning disable 1591 //XML doc
		public wnd(nint hwnd) { _h = hwnd; }

		//note: don't need implicit conversions. It creates more problems than is useful.

		public static explicit operator wnd(nint hwnd) => new(hwnd);
		public static explicit operator nint(wnd w) => w.Handle;
		public static explicit operator wnd(int hwnd) => new(hwnd);
		public static explicit operator int(wnd w) => (int)w._h;

		/// <summary>
		/// Converts from a special handle value.
		/// </summary>
		/// <param name="hwnd">See API <msdn>SetWindowPos</msdn>.</param>
		public static implicit operator wnd(SpecHWND hwnd) => new((int)hwnd);

		/// <summary>Compares window handles.</summary>
		public static bool operator ==(wnd w1, wnd w2) => w1._h == w2._h;

		/// <summary>Compares window handles.</summary>
		public static bool operator !=(wnd w1, wnd w2) => w1._h != w2._h;

		//Prevent accidental usage wnd==null. The C# compiler used to allow it without a warning; level-5 warning since C# 9. Bad: disables wnd==wnd?.
		//[Obsolete("Replace wnd==wnd? with wnd.Equals(wnd?). Replace wnd==null with wnd.Is0.", true), NoDoc]
		//public static bool operator ==(wnd w1, wnd? w2) => false;
		//[Obsolete("Replace wnd==wnd? with wnd.Equals(wnd?). Replace wnd==null with wnd.Is0.", true), NoDoc]
		//public static bool operator !=(wnd w1, wnd? w2) => true;
		//[Obsolete("Replace wnd==wnd? with wnd.Equals(wnd?). Replace wnd==null with wnd.Is0.", true), NoDoc]
		//public static bool operator ==(wnd? w1, wnd w2) => false;
		//[Obsolete("Replace wnd==wnd? with wnd.Equals(wnd?). Replace wnd==null with wnd.Is0.", true), NoDoc]
		//public static bool operator !=(wnd? w1, wnd w2) => true;
#pragma warning restore 1591 //XML doc

		/// <summary>
		/// Returns true if w != null and w.Value == this.
		/// </summary>
		public bool Equals(wnd? w) => w != null && w.GetValueOrDefault() == this;

		/// <summary>
		/// Returns true if obj is wnd and contains the same window handle.
		/// </summary>
		public override bool Equals(object obj) => obj is wnd w && this == w;

		/// <summary>
		/// Returns true if other == this.
		/// </summary>
		public bool Equals(wnd other) => other == this; //IEquatable<wnd>.Equals, to avoid boxing with eg Dictionary<wnd, T2>

		/// <summary>
		/// Implements IComparable. It allows to sort a collection.
		/// </summary>
		public int CompareTo(wnd other) => _h.CompareTo(other);

		///
		public override int GetHashCode() => (int)_h;
		//window handles are always 32-bit int, although in a 64-bit process stored in 64-bit variables.

		/// <summary>
		/// Gets window handle as IntPtr.
		/// Code <c>w.Handle</c> is the same as <c>(IntPtr)w</c> .
		/// </summary>
		public IntPtr Handle => _h;

		/// <summary>
		/// Formats string <c>$"{handle}  {ClassName}  \"{Name}\"  {ProgramName}  {Rect}"</c>.
		/// </summary>
		public override string ToString() {
			if (Is0) return "0";
			var cn = ClassName;
			var sh = Handle.ToString();
			if (cn == null) return sh + " <invalid handle>";
			string s = Name;
			if (s != null) s = s.Escape(limit: 250);
			return $"{sh}  {cn}  \"{s}\"  {ProgramName}  {Rect.ToString()}";
		}

		#endregion

		#region send/post message

		/// <summary>
		/// Calls API <msdn>SendMessage</msdn>.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public nint Send(int message, nint wParam = 0, nint lParam = 0) {
			Debug.Assert(!Is0);
			return Api.SendMessage(this, message, wParam, lParam);
		}

		/// <summary>
		/// Calls API <msdn>SendMessage</msdn> where lParam is string.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public nint Send(int message, nint wParam, string lParam) {
			Debug.Assert(!Is0);
			fixed (char* p = lParam)
				return Api.SendMessage(this, message, wParam, (nint)p);
			//info: don't use overload, then eg ambiguous if null.
		}

		/// <summary>
		/// Calls API <msdn>SendMessage</msdn> where lParam is any pointer.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public nint Send(int message, nint wParam, void* lParam) {
			Debug.Assert(!Is0);
			return Api.SendMessage(this, message, wParam, (nint)lParam);
			//info: don't use overload, then eg ambiguous if null.
		}

		//rejected: overload without out result parameter. Confusing intellisense when bad types of other parameters. Not so often used, and not so hard to put ', out _'.
		///// <summary>
		///// Calls API <msdn>SendMessageTimeout</msdn>.
		///// Returns its return value (false if failed). Supports <see cref="lastError"/>.
		///// </summary>
		//public bool SendTimeout(int millisecondsTimeout, int message, nint wParam = 0, nint lParam = 0, SMTFlags flags = SMTFlags.ABORTIFHUNG) {
		//	Debug.Assert(!Is0);
		//	return 0 != Api.SendMessageTimeout(this, message, wParam, lParam, flags, millisecondsTimeout, out _);
		//}

		/// <summary>
		/// Calls API <msdn>SendMessageTimeout</msdn> and gets the result of the message processing.
		/// Returns its return value (false if failed). Supports <see cref="lastError"/>.
		/// </summary>
		public bool SendTimeout(int millisecondsTimeout, out nint result, int message, nint wParam = 0, nint lParam = 0, SMTFlags flags = SMTFlags.ABORTIFHUNG) {
			Debug.Assert(!Is0);
			return 0 != Api.SendMessageTimeout(this, message, wParam, lParam, flags, millisecondsTimeout, out result);
		}

		/// <summary>
		/// Calls API <msdn>SendMessageTimeout</msdn> where lParam is string.
		/// Returns its return value (false if failed). Supports <see cref="lastError"/>.
		/// </summary>
		public bool SendTimeout(int millisecondsTimeout, out nint result, int message, nint wParam, string lParam, SMTFlags flags = SMTFlags.ABORTIFHUNG) {
			Debug.Assert(!Is0);
			result = 0;
			fixed (char* p = lParam)
				return 0 != Api.SendMessageTimeout(this, message, wParam, (nint)p, flags, millisecondsTimeout, out result);
		}

		/// <summary>
		/// Calls API <msdn>SendMessageTimeout</msdn> and gets the result of the message processing.
		/// Returns its return value (false if failed). Supports <see cref="lastError"/>.
		/// </summary>
		public bool SendTimeout(int millisecondsTimeout, out nint result, int message, nint wParam, void* lParam, SMTFlags flags = SMTFlags.ABORTIFHUNG) {
			Debug.Assert(!Is0);
			return 0 != Api.SendMessageTimeout(this, message, wParam, (nint)lParam, flags, millisecondsTimeout, out result);
		}

		/// <summary>
		/// Calls API <msdn>SendNotifyMessage</msdn>.
		/// Returns its return value (false if failed). Supports <see cref="lastError"/>.
		/// </summary>
		public bool SendNotify(int message, nint wParam = 0, nint lParam = 0) {
			Debug.Assert(!Is0);
			return Api.SendNotifyMessage(this, message, wParam, lParam);
		}

		/// <summary>
		/// Calls API <msdn>PostMessage</msdn>.
		/// Returns its return value (false if failed). Supports <see cref="lastError"/>.
		/// </summary>
		/// <seealso cref="WndUtil.PostThreadMessage"/>
		public bool Post(int message, nint wParam = 0, nint lParam = 0) {
			//Debug.Assert(!Is0); //no, can be used for "post thread message"
			return Api.PostMessage(this, message, wParam, lParam);
		}

		#endregion

		#region throw, valid

		/// <summary>
		/// If <see cref="Is0"/>, throws <see cref="AuWndException"/>.
		/// </summary>
		/// <returns>This.</returns>
		/// <exception cref="AuWndException"></exception>
		public wnd ThrowIf0() {
			if (Is0) throw new AuWndException(this, Api.ERROR_INVALID_WINDOW_HANDLE);
			return this;
		}

		/// <summary>
		/// If <see cref="Is0"/> or !<see cref="IsAlive"/>, throws <see cref="AuWndException"/>.
		/// </summary>
		/// <returns>This.</returns>
		/// <exception cref="AuWndException"></exception>
		public wnd ThrowIfInvalid() {
			if (Is0 || !Api.IsWindow(this)) throw new AuWndException(this, Api.ERROR_INVALID_WINDOW_HANDLE);
			return this;
		}

		///// <summary>
		///// If <see cref="Is0"/>, throws <see cref="AuWndException"/>. Returns !<see cref="IsAlive"/>.
		///// </summary>
		///// <exception cref="AuWndException"></exception>
		//public bool IsInvalidThrowIf0()
		//{
		//	if(Is0) throw new AuWndException(this, Api.ERROR_INVALID_WINDOW_HANDLE);
		//	return !Api.IsWindow(this);
		//}
		//CONSIDER: in many places replace ThrowIfInvalid with ThrowIf0 or IsInvalidThrowIf0.

		/// <summary>
		/// Throws <see cref="AuWndException"/> that uses the last Windows API error (code and message).
		/// Also the message depends on whether the window handle is 0/invalid.
		/// </summary>
		/// <exception cref="AuWndException"></exception>
		public void ThrowUseNative() {
			throw new AuWndException(this, 0);
		}

		/// <summary>
		/// Throws <see cref="AuWndException"/> that uses the specified Windows API error code and its message.
		/// Also the message depends on whether the window handle is 0/invalid.
		/// </summary>
		/// <exception cref="AuWndException"></exception>
		public void ThrowUseNative(int errorCode) {
			throw new AuWndException(this, errorCode);
		}

		/// <summary>
		/// Throws <see cref="AuWndException"/> that uses mainMessage and the last Windows API error (code and message).
		/// Also the message depends on whether the window handle is 0/invalid.
		/// </summary>
		/// <exception cref="AuWndException"></exception>
		public void ThrowUseNative(string mainMessage) {
			throw new AuWndException(this, 0, mainMessage);
		}

		/// <summary>
		/// Throws <see cref="AuWndException"/> that uses mainMessage and the specified Windows API error code.
		/// Also the message depends on whether the window handle is 0/invalid.
		/// </summary>
		/// <exception cref="AuWndException"></exception>
		public void ThrowUseNative(int errorCode, string mainMessage) {
			throw new AuWndException(this, errorCode, mainMessage);
		}

		/// <summary>
		/// Throws <see cref="AuWndException"/> that uses mainMessage and does not use the last Windows API error.
		/// Also the message depends on whether the window handle is 0/invalid.
		/// </summary>
		/// <exception cref="AuWndException"></exception>
		public void ThrowNoNative(string mainMessage) {
			throw new AuWndException(this, mainMessage);
		}

		/// <summary>
		/// Returns true if the window handle is 0 (this variable == default(wnd)).
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// wnd w = wnd.find("Window*");
		/// if(w.Is0) { print.it("window not found"); return; }
		/// ]]></code>
		/// </example>
		/// <seealso cref="IsAlive"/>
		public bool Is0 => _h == default;

		/// <summary>
		/// Returns true if the window exists (the window handle is valid).
		/// Returns false if the handle is 0 or invalid.
		/// Invalid non-0 handle usually means that the window is closed/destroyed.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="Is0"/> and API <msdn>IsWindow</msdn>.
		/// Although a <b>wnd</b> variable holds a window handle, which is like a reference to a window, it does not prevent closing that window and making the handle invalid. After closing the window, the OS can even assign the same handle value to a new window, although normally it can happen only after long time.
		/// <note>Use this carefully with windows of other applications or threads. The window can be closed at any moment, even when your thread is still in this function.</note>
		/// </remarks>
		public bool IsAlive => !Is0 && Api.IsWindow(this);

		#endregion

		#region visible, enabled, cloaked

		/// <summary>
		/// Returns true if the window is visible.
		/// Returns false if is invisible or is a child of invisible parent.
		/// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// </summary>
		/// <remarks>
		/// Calls API <msdn>IsWindowVisible</msdn>. Does not call <see cref="IsCloaked"/>.
		/// 
		/// Even when this function returns true, the window may be actually invisible. It can be cloaked, on an inactive Windows 10 virtual desktop (cloaked), inactive Windows Store app (cloaked), transparent, zero-size, minimized, off-screen, covered by other windows or can have zero-size window region.
		/// </remarks>
		/// <seealso cref="IsCloaked"/>
		/// <seealso cref="IsVisibleAndNotCloaked"/>
		/// <seealso cref="Show"/>
		/// <seealso cref="Activate"/>
		public bool IsVisible => Api.IsWindowVisible(this);

		//rejected. Unreliable, eg then does not find Store apps in inactive desktops. Instead now Find skips all cloaked by default.
		///// <summary>
		///// Returns true if the window is visible.
		///// Returns false if is invisible or is a child of invisible parent.
		///// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		///// </summary>
		///// <remarks>
		///// Returns false if API <msdn>IsWindowVisible</msdn> returns false.
		///// Also returns false if <see cref="IsCloaked"/> returns true, but only for some popup windows that usually are useless and could cause problems if considered visible.
		///// Else returns true.
		///// 
		///// Even when this function returns true, the window may be actually invisible. It can be cloaked (excepth the above case), on an inactive Windows 10 virtual desktop (cloaked), inactive Windows Store app (cloaked), transparent, zero-size, minimized, off-screen, covered by other windows or can have zero-size window region.
		///// </remarks>
		///// <seealso cref="IsVisible"/>
		///// <seealso cref="IsCloaked"/>
		///// <seealso cref="IsVisibleAndNotCloaked"/>
		///// <seealso cref="Show"/>
		///// <seealso cref="Activate()"/>
		//public bool IsVisibleEx {
		//	get {
		//		if(!Api.IsWindowVisible(this)) return false;

		//		var style = Style;
		//		if((style & (WS.POPUP | WS.CHILD)) == WS.POPUP) {
		//			if((style & WS.CAPTION) != WS.CAPTION) return !IsCloaked;

		//			//is it a ghost ApplicationFrameWindow, like closed Calculator on Win10?
		//			if(osVersion.minWin10 && HasExStyle(WSE.NOREDIRECTIONBITMAP) && IsCloaked && ClassNameIs("ApplicationFrameWindow")) {
		//				var isGhost = default == Api.FindWindowEx(this, default, "Windows.UI.Core.CoreWindow", null);
		//				//print.it(isGhost, this);
		//				return !isGhost;
		//			}
		//		}
		//		return true;
		//	}
		//}


		/// <summary>
		/// Returns true if <see cref="IsVisible"/> returns true and <see cref="IsCloaked"/> returns false.
		/// </summary>
		public bool IsVisibleAndNotCloaked => IsVisible && !IsCloaked;

		/// <summary>
		/// Returns true if this window is visible in the specified parent or ancestor window.
		/// Like <see cref="IsVisible"/>, but does not check the visibility of the specified parent/ancestor window.
		/// </summary>
		/// <param name="wParent">Parent or ancestor window.</param>
		internal bool IsVisibleIn_(wnd wParent) {
			if (IsVisible) return true; //these two make faster in most cases (when wTL is visible)
			if (wParent.IsVisible) return false;
			var c = this;
			for (int i = 0; i < 10000; i++) {
				//print.it(c);
				if (!c.HasStyle(WS.VISIBLE)) return false;
				c = c.ParentGWL_;
				if (c == wParent) return true;
				if (c.Is0) break;
			}
			Debug.Assert(false);
			return false;
		}

		/// <summary>
		/// Shows (if hidden) or hides this window.
		/// </summary>
		/// <remarks>
		/// Does not activate/deactivate/zorder.
		/// This window can be of any thread. If you know it is of this thread, use <see cref="ShowL"/>. This function calls it.
		/// </remarks>
		/// <exception cref="AuWndException"/>
		public void Show(bool show) {
			if (!ShowL(show)) ThrowUseNative(show ? "*show*" : "*hide*");
			MinimalSleepIfOtherThread_();
		}

		/// <summary>
		/// Shows (if hidden) or hides this window.
		/// </summary>
		/// <returns>Returns false if fails. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Does not activate/deactivate/zorder.
		/// 
		/// There are two similar functions to show/hide a window: 
		/// - <see cref="Show"/> is better to use in automation scripts, with windows of any process/thread. It calls <b>ShowL</b>, and throws exception if it fails. Adds a small delay if the window is of another thread.
		/// - <b>ShowL</b> is better to use in programming, with windows of current thread. It is more lightweight. Does not throw exceptions. Does not add a delay. But both functions can be used with windows of any thread.
		/// </remarks>
		public bool ShowL(bool show) {
			if (show == HasStyle(WS.VISIBLE)) return true; //avoid messages and make much faster. Never mind: if show==false, returns true even if invalid hwnd.
			Send(Api.WM_SHOWWINDOW, show ? 1 : 0); //not necessary for most windows, but eg winforms would not update the Visible property without it. ShowWindow sends it but SetWindowPos doesn't.
			return SetWindowPos((show ? SWPFlags.SHOWWINDOW : SWPFlags.HIDEWINDOW)
				| SWPFlags.NOSIZE | SWPFlags.NOMOVE | SWPFlags.NOACTIVATE | SWPFlags.NOZORDER | SWPFlags.NOOWNERZORDER);

			//This code is similar to ShowWindow(SW_SHOWNA). Don't use it because:
			//	May change Z order. Eg makes above other windows of same thread.
			//	Also may not work first time in process. The documentation is unclear. Not tested.
		}

		/// <summary>
		/// Returns true if the window is enabled for mouse and keyboard input.
		/// Returns false if disabled. Also false if failed (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="ancestorsToo">Check whether all ancestors of this control are enabled too. If false (default), this function simply calls API <msdn>IsWindowEnabled</msdn>, which usualy returns true for controls in disabled windows.</param>
		public bool IsEnabled(bool ancestorsToo = false) {
			if (!ancestorsToo) return Api.IsWindowEnabled(this);
			for (var w = this; ;) {
				if (!Api.IsWindowEnabled(w)) return false;
				w = w.Get.DirectParent; if (w.Is0) break;
			}
			return true;
		}

		/// <summary>
		/// Enables or disables.
		/// Calls API <msdn>EnableWindow</msdn>.
		/// </summary>
		/// <param name="enable">Enable or disable.</param>
		/// <exception cref="AuWndException"/>
		public void Enable(bool enable) {
			lastError.clear();
			Api.EnableWindow(this, enable);
			if (lastError.code != 0) ThrowUseNative("*enable/disable*");
		}

		/// <summary>
		/// Gets the cloaked state.
		/// Returns 0 if not cloaked or if failed.
		/// Else returns flags: 1 cloaked by its application, 2 cloaked by Windows, 4 cloaked because its owner window is cloaked.
		/// On Windows 7 returns 0 because there is no "cloaked window" feature.
		/// </summary>
		/// <seealso cref="IsCloaked"/>
		public int IsCloakedGetState {
			get {
				if (!osVersion.minWin8) return 0;
				int cloaked = 0;
				int hr = Api.DwmGetWindowAttribute(this, Api.DWMWA.CLOAKED, &cloaked, 4);
				return cloaked;
			}
		}
		/// <summary>
		/// Returns true if the window is cloaked.
		/// Returns false if not cloaked or if failed.
		/// On Windows 7 returns false because there is no "cloaked window" feature.
		/// Windows 10 uses window cloaking mostly to hide windows on inactive desktops. Windows 8 - mostly to hide Windows Store app windows.
		/// </summary>
		/// <seealso cref="IsCloakedGetState"/>
		public bool IsCloaked => IsCloakedGetState != 0;

		#endregion

		#region minimized, maximized

		/// <summary>
		/// Returns true if minimized, false if not.
		/// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// Calls API <msdn>IsIconic</msdn>.
		/// </summary>
		public bool IsMinimized => Api.IsIconic(this);

		/// <summary>
		/// Returns true if maximized, false if not.
		/// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// Calls API <msdn>IsZoomed</msdn>.
		/// </summary>
		public bool IsMaximized => Api.IsZoomed(this);

		/// <summary>
		/// If not minimized, minimizes.
		/// Also unhides.
		/// </summary>
		/// <param name="noAnimation">Visually fast, without animation.</param>
		/// <exception cref="AuWndException">The API call failed. No exception if the window did not obey.</exception>
		public void ShowMinimized(bool noAnimation = false) {
			_MinMaxRes(Api.SW_MINIMIZE, noAnimation);
		}

		/// <summary>
		/// If not minimized, minimizes.
		/// Also unhides.
		/// </summary>
		/// <param name="noAnimation">Visually fast, without animation.</param>
		/// <exception cref="AuWndException">The API call failed. No exception if the window did not obey.</exception>
		public void ShowMaximized(bool noAnimation = false) {
			_MinMaxRes(Api.SW_SHOWMAXIMIZED, noAnimation);
		}

		/// <summary>
		/// If maximized or minimized, makes normal (not min/max).
		/// Also unhides.
		/// </summary>
		/// <param name="noAnimation">Visually fast, without animation.</param>
		/// <exception cref="AuWndException">The API call failed. No exception if the window did not obey.</exception>
		public void ShowNotMinMax(bool noAnimation = false) {
			_MinMaxRes(Api.SW_SHOWNORMAL, noAnimation);
		}

		/// <summary>
		/// If minimized, restores previous non-minimized state (maximized or normal).
		/// Also unhides.
		/// </summary>
		/// <param name="noAnimation">Visually fast, without animation.</param>
		/// <exception cref="AuWndException">The API call failed. No exception if the window did not obey.</exception>
		public void ShowNotMinimized(bool noAnimation = false) {
			_MinMaxRes(Api.SW_RESTORE, noAnimation);
		}

		/// <summary>
		/// Sets window min/max/normal/restore state.
		/// Also unhides.
		/// </summary>
		/// <param name="state">Must be SW_MINIMIZE, SW_RESTORE (restores to normal/max if minimized), SW_SHOWNORMAL or SW_SHOWMAXIMIZED.</param>
		/// <param name="noAnimation">Use SetWindowPlacement (no animation).</param>
		/// <exception cref="AuWndException"/>
		void _MinMaxRes(int state, bool noAnimation) {
			Debug.Assert(state == Api.SW_MINIMIZE || state == Api.SW_RESTORE || state == Api.SW_SHOWNORMAL || state == Api.SW_SHOWMAXIMIZED);
			ThrowIfInvalid();

			bool ok = false, wasMinimized = IsMinimized;

			switch (state) {
			case Api.SW_MINIMIZE:
				ok = wasMinimized;
				break;
			case Api.SW_RESTORE:
				ok = !wasMinimized;
				break;
			case Api.SW_SHOWMAXIMIZED:
				ok = IsMaximized;
				break;
			default: //SW_SHOWNORMAL
				ok = !wasMinimized && !IsMaximized; //info: if invalid handle, Show() will return false, don't need to check here.
				break;
			}

			if (ok) {
				if (IsVisible) return;
				Show(true);
			} else {
				if (!noAnimation) {
					Api.ShowWindow(this, state);
					//note: The API returns TRUE if was visible, not if succeeded. Tested: lastError can't be used.
					ok = _IsState(this, state);
				} else if (ok = GetWindowPlacement_(out var p, false)) {
					int state2 = state;
					switch (state) {
					case Api.SW_MINIMIZE:
						if (p.showCmd == Api.SW_SHOWMAXIMIZED) p.flags |= Api.WPF_RESTORETOMAXIMIZED; else p.flags &= ~Api.WPF_RESTORETOMAXIMIZED; //Windows forgets to remove the flag
						break;
					case Api.SW_RESTORE:
						if ((p.showCmd == Api.SW_SHOWMINIMIZED) && (p.flags & Api.WPF_RESTORETOMAXIMIZED) != 0) state2 = Api.SW_SHOWMAXIMIZED; //without this would make normal
						break;
					}

					//if(wasMinimized) p.flags|=Api.WPF_ASYNCWINDOWPLACEMENT; //Windows bug: if window of another thread, deactivates currently active window and does not activate this window. However then animates window. If we set this while the window is not minimized, it would set blinking caret in inactive window. Instead we use another workaround, see below.
					p.showCmd = state2;
					ok = SetWindowPlacement_(ref p, false);
				}

				static bool _IsState(wnd w, int state) => state switch {
					Api.SW_MINIMIZE => w.IsMinimized,
					Api.SW_RESTORE => !w.IsMinimized,
					Api.SW_SHOWMAXIMIZED => w.IsMaximized,
					_ => !(w.IsMinimized || w.IsMaximized)
				} && w.IsVisible;

				if (!ok) {
					if (UacAccessDenied) {
						var cmd = state switch {
							Api.SW_MINIMIZE => Api.SC_MINIMIZE,
							Api.SW_SHOWMAXIMIZED => Api.SC_MAXIMIZE, //fails, never mind
							_ => Api.SC_RESTORE,
						};
						Send(Api.WM_SYSCOMMAND, cmd);
						//if was minimized, now can be maximized, need to restore if SW_SHOWNORMAL
						if (state == Api.SW_SHOWNORMAL && IsMaximized) Send(Api.WM_SYSCOMMAND, cmd);
						//note: the Send return value is unreliable
						ok = _IsState(this, state);
					}

					if (!ok) ThrowNoNative("*minimize/maximize/restore*");
				}

				if (!IsOfThisThread) {
					if (wasMinimized) ActivateL(); //Windows bug: if window of another thread, deactivates currently active window and does not activate this window
					else if (state == Api.SW_MINIMIZE) WndUtil.WaitForAnActiveWindow();
				}
			}

			MinimalSleepIfOtherThread_();
		}

		/// <summary>
		/// Initializes a WINDOWPLACEMENT struct and calls API <msdn>GetWindowPlacement</msdn>.
		/// </summary>
		/// <param name="wp"></param>
		/// <param name="rectInScreen">Remove workarea thickness from wp.rcNormalPosition.</param>
		/// <param name="errStr">If not null, throws it if fails.</param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		/// <exception cref="AuWndException">Failed. Throws, only if errStr!=null, else returns false.</exception>
		internal bool GetWindowPlacement_(out Api.WINDOWPLACEMENT wp, bool rectInScreen, string errStr = null) {
			//initially this was public, but probably don't need.

			wp = new Api.WINDOWPLACEMENT(); wp.length = Api.SizeOf(wp);
			if (Api.GetWindowPlacement(this, ref wp)) {
				if (rectInScreen) _Wp1(ref wp, false);
				return true;
			}
			if (errStr != null) ThrowUseNative(errStr);
			return false;
		}

		/// <summary>
		/// Sets WINDOWPLACEMENT <b>length</b> field and calls API <msdn>SetWindowPlacement</msdn>.
		/// </summary>
		/// <param name="wp"></param>
		/// <param name="rectInScreen">wp.rcNormalPosition is without workarea thickness.</param>
		/// <param name="errStr">If not null, throws it if fails.</param>
		/// <exception cref="AuWndException">Failed. Throws, only if errStr!=null, else returns false.</exception>
		internal bool SetWindowPlacement_(ref Api.WINDOWPLACEMENT wp, bool rectInScreen, string errStr = null) {
			//initially this was public, but probably don't need.

			if (rectInScreen) _Wp1(ref wp, true);
			wp.length = Api.SizeOf(wp);
			if (Api.SetWindowPlacement(this, wp)) return true;
			if (errStr != null) ThrowUseNative(errStr);
			return false;
		}

		void _Wp1(ref Api.WINDOWPLACEMENT wp, bool set) {
			if (!IsToolWindow) {
				var s = set ? screen.of(wp.rcNormalPosition) : screen.of(this);
				var v = s.Info;
				int i = set ? -1 : 1;
				wp.rcNormalPosition.Offset((v.workArea.left - v.rect.left) * i, (v.workArea.top - v.rect.top) * i);
			}
		}

		#endregion

		#region activate, focus

		internal static partial class Internal_ {
			/// <summary>
			/// No exceptions.
			/// </summary>
			internal static bool EnableActivate(bool goingToActivateAWindow) {
				if (_EnableActivate_AllowSetFore()) return true; //not locked, or already successfully called ASF_Key

				_EnableActivate_SendKey(true);
				if (_EnableActivate_AllowSetFore()) return true;
				//First time fails if the foreground window is of higher IL. Then sending keys does not work.

				//_EnableActivate_MinRes() makes no active window...
				if (goingToActivateAWindow) {
					_EnableActivate_MinRes();
					return _EnableActivate_AllowSetFore();
				}

				var wFore = active; bool retry = false;
			g1: _EnableActivate_MinRes();
				if (!_EnableActivate_AllowSetFore()) return false;
				if (!wFore.Is0 && !retry) {
					Api.SetForegroundWindow(wFore);
					if (!_EnableActivate_AllowSetFore()) { retry = true; goto g1; } //didn't notice this but let's be safer
				}
				return true;

				//Other possible methods:
				//1. Instead of key can use attachthreadinput. But it is less reliable, eg works first time only, and does not allow our process to activate later easily. Does not work if foreground window is higher IL.
				//2. Call allowsetforegroundwindow from a hook from the foreground process (or from the shell process, not tested). Too dirty. Need 2 native dlls (32/64-bit). Cannot inject if higher IL.

				//tested: cannot disable the foreground lock timeout with SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT) when this process cannot activate windows.
			}

			/// <summary>
			/// Sends a key (VK_0 up). It allows to activate now.
			/// Later this process usually (but not always) can activate easily (without key etc). It works even with higher IL windows.
			/// Don't know why is this behavior. Tested on all OS from XP to 10.
			/// Does not work if the foreground process has higher UAC IL.
			/// </summary>
			static void _EnableActivate_SendKey(bool debugOut) {
				//if (debugOut) Debug_.Print("EnableActivate: need key"); //FUTURE: temporarily enable to see maybe there are too many calls

				var x = new Api.INPUTK(0, 128, Api.KEYEVENTF_KEYUP);
				Api.SendInput(&x);
				//info: works without waiting.
			}

			/// <summary>
			/// Creates a temporary minimized window and restores it. It activates the window and allows us to activate.
			/// Then sets 'no active window' to prevent auto-activating another window when destroying the temporary window.
			/// </summary>
			static void _EnableActivate_MinRes() {
				Debug_.Print("EnableActivate: need min/res");

				wnd t = WndUtil.CreateWindow(WndUtil.WindowClassDWP_, null, WS.POPUP | WS.MINIMIZE | WS.VISIBLE, WSE.TOOLWINDOW);
				//info: When restoring, the window must be visible, or may not work.
				try {
					var wp = new Api.WINDOWPLACEMENT { showCmd = Api.SW_RESTORE };
					t.SetWindowPlacement_(ref wp, false); //activates t; fast (no animation)
					_EnableActivate_SendKey(false); //makes so that later our process can always activate
					_EnableActivate_AllowSetFore();
					Api.SetForegroundWindow(getwnd.root); //set no foreground window, or may activate the higher IL window (maybe does not activate, but winevents hook gets events, in random order). Other way would be to destroy our window later, but more difficult to implement.
				}
				finally { Api.DestroyWindow(t); }

				//Another way:
				//	t.Send(Api.WM_SETHOTKEY, ...);
				//	Api.SendInputKey(...)
				//Works if UAC allows SendInput.
				//WM_SETHOTKEY works only if visible. No wm_syscommand if inactive, but activates. Unlike registerhotkey, not blocked by UAC even without modifiers. Need more testing.
				//This does not work: t.Send(WM_SYSCOMMAND, SC_HOTKEY);
			}

			/// <summary>
			/// Calls Api.AllowSetForegroundWindow(Api.GetCurrentProcessId()).
			/// </summary>
			static bool _EnableActivate_AllowSetFore() {
				return Api.AllowSetForegroundWindow(Api.GetCurrentProcessId());
			}

			internal static bool ActivateL(wnd w) {
				if (w.IsActiveOrNoActiveAndThisIsWndRoot_) return true;

				try {
					bool canAct = EnableActivate(true);

					if (!Api.SetForegroundWindow(w)) {
						if (!canAct || !w.IsAlive) return false;
						//It happens when foreground process called LockSetForegroundWindow.
						//Although AllowSetForegroundWindow returns true, SetForegroundWindow fails.
						//It happens only before this process sends keys. Eg after first _EnableActivate_SendKey this never happens again.
						//If it has higher IL (and this process is User), also need _EnableActivate_MinRes.
						_EnableActivate_SendKey(true);
						if (!Api.SetForegroundWindow(w)) {
							_EnableActivate_MinRes();
							if (!Api.SetForegroundWindow(w)) return false;
						}
					}

					//Sometimes after SetForegroundWindow there is no active window for several ms. Not if the window is of this thread.
					if (w == getwnd.root) return active.Is0;
					//CONSIDER: if GetForegroundWindow is not w, send WM_NULL. Info: https://blogs.msdn.microsoft.com/oldnewthing/20161118-00/?p=94745
					return WndUtil.WaitForAnActiveWindow();
				}
				//catch(AuWndException) { return false; }
				catch { return false; }
			}

			[Flags]
			internal enum ActivateFlags {
				/// <summary>
				/// Don't call ThrowIfInvalid (ie caller ensures it is valid).
				/// </summary>
				NoThrowIfInvalid = 1,

				/// <summary>
				/// Don't try to get top-level window (ie caller ensures it's a top-level window, not control).
				/// </summary>
				NoGetWindow = 2,

				/// <summary>
				/// Don't activate if has WS_EX_NOACTIVATE style or is toolwindow without caption, unless cloaked.
				/// Then just calls ZorderTop(), which in most cases does not work (inactive window).
				/// </summary>
				IgnoreIfNoActivateStyleEtc = 4,

				/// <summary>
				/// Wait for window animations to end. Eg when switching Win10 desktops.
				/// </summary>
				ForScreenCapture = 8,
			}
		}

		/// <summary>
		/// Activates this window (brings to the foreground).
		/// The same as <see cref="Activate"/>, but has some options.
		/// Returns false if does not activate because of flag IgnoreIfNoActivateStyleEtc.
		/// </summary>
		/// <exception cref="AuWndException"/>
		internal bool Activate_(Internal_.ActivateFlags flags) {
			if (!flags.Has(Internal_.ActivateFlags.NoThrowIfInvalid)) ThrowIfInvalid();
			if (flags.Has(Internal_.ActivateFlags.NoGetWindow)) Debug.Assert(!IsChild);
			else {
				var w = Window;
				if (w != this) {
					if (!w.Is0) print.warning("Child windows can't be activated. You may want to call Focus() instead.");
					return w.Activate_((flags | Internal_.ActivateFlags.NoGetWindow) & ~Internal_.ActivateFlags.NoThrowIfInvalid);
				}
			}

			bool R, noAct = false, isMinimized = false, ofThisThread = IsOfThisThread;
			bool forScreenCapture = 0 != (flags & Internal_.ActivateFlags.ForScreenCapture);

			if (IsMinimized) {
				ShowNotMinimized(true);
				isMinimized = IsMinimized;
				if (forScreenCapture && !isMinimized && !ofThisThread) Thread.Sleep(250); //although we use noAnimation, in some cases still restores with animation
			}
			if (!IsVisible) Show(true);

			R = IsActiveOrNoActiveAndThisIsWndRoot_;
			if (!R) {
				if (0 != (flags & Internal_.ActivateFlags.IgnoreIfNoActivateStyleEtc)) {
					if (IsNoActivateStyle_() && !IsCloaked) {
						ZorderTop();
						return false; //if cloaked, need to activate to uncloak
					}
				}

				for (int i = 0; i < 3; i++) {
					bool ok = ActivateL();

					if (!ofThisThread) {
						MinimalSleepNoCheckThread_();
						MinimalSleepNoCheckThread_();
					}

					if (ok) {
						wnd f = active;
						if (f == this) R = true;
						else if (this == getwnd.root) R = f.Is0; //activating GetDesktopWindow makes "no active window"
						else { //forgive if the target app instead activated another window of same thread
							int tid = ThreadId; if (tid == 0) break;
							if (f.ThreadId == tid) {
								//at first try to recognize such known windows, to avoid the hard way
								if (isMinimized || (f.Get.Owner == this && Rect.NoArea)) {
									R = true;
								} else {
									R = Api.SetForegroundWindow(getwnd.root) && ActivateL() && active.ThreadId == tid;
									if (R && !ofThisThread) {
										MinimalSleepNoCheckThread_();
										R = active.ThreadId == tid;
									}
								}
								//Example 1:
								//Excel creates a minimized window for each workbook opened in that excel process.
								//These windows just add taskbar buttons. Also it allows to find and activate workbooks.
								//When you activate such window, Excel instead activates its main window, where it displays all workbooks.
								//For this reason we would fail (not always, because this may be temporarily active).
								//Same with PowerPoint. Other Office apps no.
								//Example 2:
								//Inno Setup, SQLite Expert. They have a zero-size owner window that just adds taskbar button.
							}
						}
						if (R) break;
					}

					if (noAct) break;
					Thread.Sleep(30);
				}
			}

			if (R && !ofThisThread && this != getwnd.root) {
				//If we activate a window that is on an inactive Win10 desktop, its desktop becomes active.
				//Windows on inactive desktops are cloaked. They are uncloaked after ~15 ms.
				if (IsCloaked) {
					R = false;
					for (int i = 0; i < 50; i++) { Thread.Sleep(30); if (R = !IsCloaked) break; }
					if (R) {
						if (forScreenCapture) Thread.Sleep(800); //need minimum 600 for 'find image' functions, because of animation while switching Win10 desktops.
						MinimalSleepNoCheckThread_();
						R = IsActive;
						if (!R && ActivateL()) {
							MinimalSleepNoCheckThread_();
							R = IsActive;
						}
					}
				}
			}

			if (!R) {
				if (!miscInfo.isInputDesktop(detectLocked: true)) ThrowNoNative("*activate window. Other desktop is active");
				ThrowNoNative("*activate*");
			}
			if (forScreenCapture) MinimalSleepIfOtherThread_();

			return true;

			//tested: if the window is hung, activates the ghost window and fails (exception). It's OK.
		}

		internal bool IsNoActivateStyle_() {
			var es = ExStyle;
			if ((es & WSE.NOACTIVATE) != 0) return true;
			if ((es & (WSE.TOOLWINDOW | WSE.APPWINDOW)) == WSE.TOOLWINDOW) return !HasStyle(WS.CAPTION);
			return false;
		}

		/// <summary>
		/// Activates this window. Also makes it visible and not minimized.
		/// The active window is in the foreground and receives keyboard and mouse input.
		/// </summary>
		/// <returns>Self.</returns>
		/// <remarks>
		/// Activating a window usually also uncloaks it, for example switches to its virtual desktop on Windows 10.
		/// Fails (throws exception) if cannot activate this window, except:
		/// - When this is a control. Then activates its top-level parent window.
		/// - When the window's process instead activates another window of the same thread.
		/// - If this is <see cref="getwnd.root"/>, just deactivates the currently active window.
		/// </remarks>
		/// <exception cref="AuWndException">Failed to activate.</exception>
		/// <seealso cref="ActivateL"/>
		/// <seealso cref="IsActive"/>
		/// <seealso cref="active"/>
		/// <seealso cref="switchActiveWindow"/>
		public wnd Activate() {
			Activate_(0);
			return this;
		}
		//CONSIDER: if fails to activate:
		//dialog.show("Failed to activate window", w.ToString(), footer: The script will continue if you activate the window in {x} s.", timeout: 10);

		//rejected: Activate(double waitS = 0) /// <param name="waitS">Max time interval (seconds) to wait until the window naturally becomes active before activating it.</param>
		//	It would have sense if we somehow know that the window was created by previous action (or several actions).
		//	Would need an explicit function call before the action(s). Nobody would use it.

		/// <summary>
		/// Lightweight version of <see cref="Activate"/>. Does not throw exception if fails.
		/// </summary>
		/// <param name="full">
		/// Call <see cref="Activate"/> and handle exceptions; on exception return false.
		/// If false (default), just activates; does not show hidden, does not restore minimized, does not check whether it is a top-level window or control, does not check whether activated exactly this window, etc.
		/// </param>
		/// <returns>false if fails.</returns>
		public bool ActivateL(bool full = false) {
			if (!full) return Internal_.ActivateL(this);
			try {
				Activate();
				return true;
			}
			catch { return false; }
		}

		//Too unreliable.
		///// <summary>
		///// Calls API LockSetForegroundWindow, which temporarily prevents other applications from activating windows easily with SetForegroundWindow().
		///// If LockSetForegroundWindow() fails, calls EnableActivate() and retries.
		///// </summary>
		///// <param name="on">Lock or unlock.</param>
		//public static bool lockActiveWindow(bool on)
		//{
		//	uint f = on ? Api.LSFW_LOCK : Api.LSFW_UNLOCK;
		//	if(Api.LockSetForegroundWindow(f)) return true;
		//	return EnableActivate() && Api.LockSetForegroundWindow(f);
		//}

		/// <summary>
		/// Sets the keyboard input focus to this control.
		/// Also activetes its top-level parent window (see <see cref="Activate"/>).
		/// </summary>
		/// <remarks>
		/// The control can belong to any process/thread. With controls of this thread you can use the more lightweight function <see cref="thisThread.focus"/>.
		/// Works not with all windows. For example, does not work with Windows Store apps. Then use <see cref="elm.Focus"/>.
		/// Can instead focus a child control. For example, if this is a ComboBox, it will focus its child Edit control. Then does not throw exception.
		/// This can be control or top-level window. Top-level windows also can have focus.
		/// </remarks>
		/// <exception cref="AuWndException">
		/// - Invalid handle.
		/// - Disabled.
		/// - Failed to set focus.
		/// - Failed to activate parent window.
		/// Fails to set focus when the target process is admin or uiAccess and this process isn't. See [](xref:uac).
		/// </exception>
		/// <seealso cref="focused"/>
		/// <seealso cref="IsFocused"/>
		/// <seealso cref="elm.Focus"/>
		public void Focus() {
			ThrowIfInvalid();
			wnd wTL = Window;
			if (!wTL.IsActive) wTL.Activate_(Internal_.ActivateFlags.NoGetWindow);

			int tid = ThreadId;
			if (tid == Api.GetCurrentThreadId()) {
				if (!thisThread.focus(this)) {
					if (!IsEnabled(true)) goto gDisabled;
					goto gFailed;
				}
				return;
			}

			if (IsFocused) return;
			if (!IsEnabled(true)) goto gDisabled;

			bool ok = false;
			using (new AttachThreadInput_(tid, out bool atiOK)) {
				if (atiOK) { //FUTURE: if fails, try elm.Focus or UIA. AttachThreadInput is unreliable.
					for (int i = 0; i < 5; i++) {
						if (i > 0) Thread.Sleep(30);
						lastError.clear();
						if (thisThread.focus(this)) {
							wnd f = focused;
							if (f == this || f.IsChildOf(this)) { ok = true; break; }
						}
					}
				}
			}
			if (!ok) goto gFailed;

			MinimalSleepNoCheckThread_();
			return;
		gDisabled: //SetFocus fails if disabled
			ThrowIfInvalid();
			ThrowNoNative("*set focus. Disabled");
		gFailed:
			ThrowUseNative("*set focus");
		}

		/// <summary>
		/// Gets the control or window that has the keyboard input focus.
		/// </summary>
		/// <remarks>
		/// The control/window can belong to any process/thread. With controls/windows of this thread you can use the more lightweight function <see cref="thisThread.focused"/>.
		/// Calls API <msdn>GetGUIThreadInfo</msdn>.
		/// </remarks>
		/// <seealso cref="Focus"/>
		/// <seealso cref="IsFocused"/>
		public static wnd focused {
			get {
				miscInfo.getGUIThreadInfo(out var g);
				return g.hwndFocus;
			}
		}
		//FUTURE: need functions that wait eg max 1 s until a window is focused or active.
		//	Example: after activating a window using the taskbar, there is no active window for 100 ms or more.
		//	Example: after opening a common file dialog, the Edit control is focused after 200 ms, until that there is no focus.
		// For 'active' we already have More.WaitForAnActiveWindow.

		/// <summary>
		/// Returns true if this is the control or window that has the keyboard input focus.
		/// </summary>
		/// <remarks>
		/// This control/window can belong to any process/thread. With controls/windows of this thread you can use the more lightweight function <see cref="thisThread.isFocused"/>.
		/// Calls <see cref="focused"/>.
		/// </remarks>
		/// <seealso cref="Focus"/>
		public bool IsFocused => !this.Is0 && this == focused;

		/// <summary>
		/// Functions that can be used only with windows/controls of this thread.
		/// </summary>
		public static class thisThread {
			/// <summary>
			/// Calls API <msdn>SetFocus</msdn>. It sets the keyboard input focus to the specified control or window, which must be of this thread.
			/// Returns false if fails. Supports <see cref="lastError"/>.
			/// </summary>
			/// <remarks>
			/// Fails if the control/window belongs to another thread or is invalid or disabled.
			/// Can instead focus a child control. For example, if ComboBox, will focus its child Edit control. Then returns true.
			/// </remarks>
			public static bool focus(wnd w) {
				if (w.Is0) { Api.SetLastError(Api.ERROR_INVALID_WINDOW_HANDLE); return false; }
				var f = Api.GetFocus(); if (f == w) return true;
				if (!Api.SetFocus(w).Is0) return true;
				if (f.Is0) return !Api.GetFocus().Is0;
				return false;
			}

			/// <summary>
			/// Gets the focused control or window of this thread.
			/// </summary>
			/// <remarks>
			/// Calls API <msdn>GetFocus</msdn>.
			/// </remarks>
			public static wnd focused => Api.GetFocus();

			//rejected.
			///// <summary>
			///// Gets the focused control or form of this thread.
			///// </summary>
			///// <remarks>
			///// Calls API <msdn>GetFocus</msdn> and <see cref="System.Windows.Forms.Control.FromHandle"/>.
			///// </remarks>
			//public static System.Windows.Forms.Control focusedWinformsControl => System.Windows.Forms.Control.FromHandle(Api.GetFocus().Handle);

			/// <summary>
			/// Returns true if w is the focused control or window of this thread.
			/// </summary>
			/// <remarks>
			/// Calls API <msdn>GetFocus</msdn>.
			/// </remarks>
			public static bool isFocused(wnd w) => !w.Is0 && w == Api.GetFocus();

			/// <summary>
			/// Gets the active window of this thread.
			/// Calls API <msdn>GetActiveWindow</msdn>.
			/// </summary>
			public static wnd active => Api.GetActiveWindow();
		}

		#endregion

		#region rect

		/// <summary>
		/// Gets rectangle (position and size) in screen coordinates.
		/// </summary>
		/// <param name="r">Receives the rectangle. Will be default(RECT) if failed.</param>
		/// <param name="withoutExtendedFrame">Don't include the transparent part of window border. For it is used API <msdn>DwmGetWindowAttribute</msdn>(DWMWA_EXTENDED_FRAME_BOUNDS); it is less reliable.</param>
		/// <remarks>
		/// The same as the <see cref="Rect"/> property.
		/// Calls API <msdn>GetWindowRect</msdn> and returns its return value.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool GetRect(out RECT r, bool withoutExtendedFrame = false) {
			if (withoutExtendedFrame) {
				RECT t;
				if (0 == Api.DwmGetWindowAttribute(this, Api.DWMWA.EXTENDED_FRAME_BOUNDS, &t, 16)) {
					r = t;
					return true;
					//tested: on Win7 gets physical (not logical) coords of DPI-scaled window
				}
			}

			if (Api.GetWindowRect(this, out r)) return true;
			r = default;
			return false;
		}

		/// <summary>
		/// Gets rectangle (position and size) in screen coordinates.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="GetRect"/>. Returns default(RECT) if fails (eg window closed).
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public RECT Rect {
			get {
				GetRect(out RECT r);
				return r;
			}
		}

		///// <summary>
		///// Gets width and height.
		///// </summary>
		///// <param name="z">Receives width and height. Will be default(SIZE) if failed.</param>
		///// <remarks>
		///// The same as the <see cref="Size"/> property.
		///// Calls API <msdn>GetWindowRect</msdn> and returns its return value.
		///// Supports <see cref="lastError"/>.
		///// </remarks>
		//public bool GetSize(out SIZE z) {
		//	if (Api.GetWindowRect(this, out RECT r)) { z = new SIZE(r.Width, r.Height); return true; }
		//	z = default;
		//	return false;
		//}

		///// <summary>
		///// Gets width and height.
		///// </summary>
		///// <remarks>
		///// Calls <see cref="GetSize"/>. Returns default(SIZE) if fails (eg window closed).
		///// Supports <see cref="lastError"/>.
		///// </remarks>
		//public SIZE Size {
		//	get {
		//		GetSize(out SIZE z);
		//		return z;
		//	}
		//}

		///// <summary>
		///// Gets position in screen coordinates.
		///// </summary>
		///// <remarks>
		///// Calls <see cref="GetRect"/>. Returns default(POINT) if fails (eg window closed).
		///// Supports <see cref="lastError"/>.
		///// </remarks>
		//public POINT XY {
		//	get { var r = Rect; return new(r.left, r.top); }
		//}

		///// <summary>
		///// Gets horizontal position in screen coordinates.
		///// </summary>
		///// <remarks>Calls <see cref="GetRect"/>.</remarks>
		//public int X {
		//	get => Rect.left;
		//}

		///// <summary>
		///// Gets vertical position in screen coordinates.
		///// </summary>
		///// <remarks>Calls <see cref="GetRect"/>.</remarks>
		//public int Y {
		//	get => Rect.top;
		//}

		///// <summary>
		///// Gets width.
		///// </summary>
		///// <remarks>Calls <see cref="GetRect"/>.</remarks>
		//public int Width {
		//	get => Rect.Width;
		//}

		///// <summary>
		///// Gets height.
		///// </summary>
		///// <remarks>Calls <see cref="GetRect"/>.</remarks>
		//public int Height {
		//	get => Rect.Height;
		//}

		/// <summary>
		/// Gets client area rectangle.
		/// </summary>
		/// <param name="r">Receives the rectangle. Will be default(RECT) if failed.</param>
		/// <param name="inScreen">
		/// Get rectangle in screen coordinates; like <see cref="GetWindowAndClientRectInScreen"/> but faster.
		/// If false (default), calls API <msdn>GetClientRect</msdn>; the same as <see cref="ClientRect"/>.</param>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool GetClientRect(out RECT r, bool inScreen = false) {
#if true
			if (Api.GetClientRect(this, out r) && (!inScreen || Api.MapWindowPoints(this, default, ref r, out _))) return true;
			r = default; return false;
#else //>= 2 times slower
			if(inScreen) return GetWindowAndClientRectInScreen(out _, out r);
			if(Api.GetClientRect(this, out r)) return true;
			r = default;
			return false;
#endif
		}

		/// <summary>
		/// Gets client area rectangle (width and height).
		/// </summary>
		/// <remarks>
		/// The left and top fields are always 0.
		/// Calls <see cref="GetClientRect"/>. Returns default(RECT) if fails (eg window closed).
		/// </remarks>
		public RECT ClientRect {
			get {
				GetClientRect(out RECT r);
				return r;
			}
		}

		///// <summary>
		///// Gets client area width and height.
		///// </summary>
		///// <param name="z">Receives width and height. Will be default(RECT) if failed.</param>
		///// <remarks>
		///// The same as the <see cref="ClientSize"/> property.
		///// The same as <see cref="GetClientRect"/>, just the parameter type is different.
		///// Calls API <msdn>GetClientRect</msdn> and returns its return value.
		///// Supports <see cref="lastError"/>.
		///// </remarks>
		//public bool GetClientSize(out SIZE z) {
		//	if (Api.GetClientRect(this, out RECT r)) { z = new SIZE(r.right, r.bottom); return true; }
		//	z = default;
		//	return false;
		//}

		/// <summary>
		/// Gets client area rectangle (width and height) in screen.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="GetClientRect"/>. Returns default(RECT) if fails (eg window closed).
		/// </remarks>
		public RECT ClientRectInScreen {
			get {
				GetClientRect(out RECT r, true);
				return r;
			}
		}

		///// <summary>
		///// Gets client area width and height.
		///// </summary>
		///// <remarks>
		///// The same as <see cref="ClientRect"/>, just the return type is different.
		///// Calls <see cref="GetClientSize"/>. Returns default(SIZE) if fails (eg window closed).
		///// </remarks>
		//public SIZE ClientSize {
		//	get {
		//		GetClientSize(out SIZE z);
		//		return z;
		//	}
		//}

		///// <summary>
		///// Gets client area width.
		///// </summary>
		///// <remarks>Calls <see cref="GetClientSize"/>.</remarks>
		//public int ClientWidth {
		//	get => ClientSize.width;
		//}

		///// <summary>
		///// Gets client area height.
		///// </summary>
		///// <remarks>Calls <see cref="GetClientSize"/>.</remarks>
		//public int ClientHeight {
		//	get => ClientSize.height;
		//}

		/// <summary>
		/// Resizes this window to match specifient client area size.
		/// Calls <see cref="ResizeL"/>.
		/// </summary>
		/// <param name="width">Client area width. Use null to not change.</param>
		/// <param name="height">Client area height. Use null to not change.</param>
		/// <exception cref="AuWndException"/>
		public void ResizeClient(int? width, int? height) {
			if (GetWindowInfo_(out var u)) {
				int W = width ?? u.rcClient.Width; W += u.rcWindow.Width - u.rcClient.Width;
				int H = height ?? u.rcClient.Height; H += u.rcWindow.Height - u.rcClient.Height;

				if (ResizeL(W, H)) return;
			}

			ThrowUseNative();
		}

		/// <summary>
		/// Calls API <msdn>GetWindowInfo</msdn>.
		/// </summary>
		/// <param name="wi">Receives window/client rectangles, styles etc.</param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		internal bool GetWindowInfo_(out Api.WINDOWINFO wi) {
			//initially this was public, but probably don't need.

			wi = default; wi.cbSize = Api.SizeOf(wi);
			return Api.GetWindowInfo(this, ref wi);
		}

		/// <summary>
		/// Gets window rectangle and client area rectangle, both in screen coordinates.
		/// </summary>
		/// <param name="rWindow">Receives window rectangle.</param>
		/// <param name="rClient">Receives client area rectangle.</param>
		/// <remarks>Calls API <msdn>GetWindowInfo</msdn>. Supports <see cref="lastError"/>.</remarks>
		public bool GetWindowAndClientRectInScreen(out RECT rWindow, out RECT rClient) {
			if (GetWindowInfo_(out var u)) {
				rWindow = u.rcWindow;
				rClient = u.rcClient;
				return true;
			}
			rWindow = default;
			rClient = default;
			return false;
		}

		//rejected: because now GetClientRect has 'inScreen' parameter.
		///// <summary>
		///// Gets client area rectangle in screen coordinates.
		///// </summary>
		///// <remarks>
		///// Calls <see cref="GetWindowAndClientRectInScreen"/>. Returns default(RECT) if fails (eg window closed).
		///// </remarks>
		//public RECT ClientRectInScreen
		//{
		//	get
		//	{
		//		GetWindowAndClientRectInScreen(out var rw, out var rc);
		//		return rc;
		//	}
		//}

		/// <summary>
		/// Converts coordinates relative to the client area of this window to coordinates relative to the client area of window w.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapClientToClientOf(wnd w, ref RECT r) => !Is0 && !w.Is0 && Api.MapWindowPoints(this, w, ref r, out _);

		/// <summary>
		/// Converts coordinates relative to the client area of this window to coordinates relative to the client area of window w.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapClientToClientOf(wnd w, ref POINT p) => !Is0 && !w.Is0 && Api.MapWindowPoints(this, w, ref p, out _);

		/// <summary>
		/// Converts coordinates relative to the client area of this window to coordinates relative to the screen.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapClientToScreen(ref RECT r) => !Is0 && Api.MapWindowPoints(this, default, ref r, out _);

		/// <summary>
		/// Converts coordinates relative to the client area of this window to coordinates relative to the screen.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapClientToScreen(ref POINT p) => Api.ClientToScreen(this, ref p);

		/// <summary>
		/// Converts coordinates relative to the screen to coordinates relative to the client area of this window.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapScreenToClient(ref RECT r) => !Is0 && Api.MapWindowPoints(default, this, ref r, out _);

		/// <summary>
		/// Converts coordinates relative to the screen to coordinates relative to the client area of this window.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapScreenToClient(ref POINT p) => Api.ScreenToClient(this, ref p);

		/// <summary>
		/// Converts coordinates relative to the client area of this window to coordinates relative to the top-left corner of this window.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapClientToWindow(ref POINT p) {
			if (!GetWindowAndClientRectInScreen(out var rw, out var rc)) return false;
			p.x += rc.left - rw.left; p.y += rc.top - rw.top;
			return true;
		}

		/// <summary>
		/// Converts coordinates relative to the client area of this window to coordinates relative to the top-left corner of this window.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapClientToWindow(ref RECT r) {
			if (!GetWindowAndClientRectInScreen(out var rw, out var rc)) return false;
			r.Offset(rc.left - rw.left, rc.top - rw.top);
			return true;
		}

		/// <summary>
		/// Converts coordinates relative to the top-left corner of this window to coordinates relative to the client area of this window.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapWindowToClient(ref POINT p) {
			if (!GetWindowAndClientRectInScreen(out var rw, out var rc)) return false;
			p.x += rw.left - rc.left; p.y += rw.top - rc.top;
			return true;
		}

		/// <summary>
		/// Converts coordinates relative to the top-left corner of this window to coordinates relative to the client area of this window.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapWindowToClient(ref RECT r) {
			if (!GetWindowAndClientRectInScreen(out var rw, out var rc)) return false;
			r.Offset(rw.left - rc.left, rw.top - rc.top);
			return true;
		}

		/// <summary>
		/// Converts coordinates relative to the top-left corner of this window to screen coordinates.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapWindowToScreen(ref POINT p) {
			if (!GetRect(out var rw)) return false;
			p.x += rw.left; p.y += rw.top;
			return true;
		}

		/// <summary>
		/// Converts coordinates relative to the top-left corner of this window to screen coordinates.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool MapWindowToScreen(ref RECT r) {
			if (!GetRect(out var rw)) return false;
			r.Offset(rw.left, rw.top);
			return true;
		}

		/// <summary>
		/// Gets rectangle of this window (usually control) relative to the client area of another window (usually parent).
		/// </summary>
		/// <param name="w">Parent, ancestor or any other window or control. If <c>default(wnd)</c>, gets rectangle in screen.</param>
		/// <param name="r">Receives the rectangle.</param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		/// <seealso cref="RectInDirectParent"/>
		/// <seealso cref="RectInWindow"/>
		public bool GetRectIn(wnd w, out RECT r) {
			if (w.Is0) return GetRect(out r);
			return GetRect(out r) && w.MapScreenToClient(ref r);
		}

		/// <summary>
		/// Gets child window rectangle in the client area of the direct parent window.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="getwnd.DirectParent"/> and <see cref="GetRectIn"/>. Returns default(RECT) if fails (eg window closed).
		/// </remarks>
		public RECT RectInDirectParent => GetRectIn(Get.DirectParent, out var r) ? r : default;

		/// <summary>
		/// Gets child window rectangle in the client area of the top-level parent window.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="Window"/> and <see cref="GetRectIn"/>. Returns default(RECT) if fails (eg window closed).
		/// </remarks>
		public RECT RectInWindow => GetRectIn(Window, out var r) ? r : default;

		/// <summary>
		/// Gets rectangle of normal (restored) window even if currently it is minimized or maximized.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool GetRectNotMinMax(out RECT r) {
			if (!(IsMinimized || IsMaximized)) return GetRect(out r);
			if (!GetWindowPlacement_(out var p, true)) { r = default; return false; }
			r = p.rcNormalPosition;
			return true;
		}

		//TEST: undocumented API GetWindowMinimizeRect. Gets rect of taskbar button or where it would be minimized above the Start button. But probably not useful.

		/// <summary>
		/// Returns mouse pointer position relative to the client area of this window.
		/// </summary>
		public POINT MouseClientXY {
			get {
				Api.GetCursorPos(out var p);
				if (!MapScreenToClient(ref p)) p = default;
				return p;
			}
		}

		/// <summary>
		/// Returns true if this window (its rectangle) contains the specified point.
		/// </summary>
		/// <param name="x">X coordinate in screen. Not used if default(Coord). Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in screen. Not used if default(Coord).</param>
		public bool ContainsScreenXY(Coord x, Coord y) {
			POINT p = Coord.Normalize(x, y);
			if (!GetRect(out RECT r)) return false;
			if (!r.Contains(x.IsEmpty ? r.left : p.x, y.IsEmpty ? r.top : p.y)) return false;
			return true;

			//note: we don't use name ContainsXY and 2 overloads, mostly because of possible incorrect usage. Also now easier to read the code.
		}

		/// <summary>
		/// Returns true if this control (its rectangle) contains the specified point in parent window.
		/// </summary>
		/// <param name="parent">
		/// Direct or indirect parent window. The coordinates are relative to its client area.
		/// Actually this and parent can be any windows or controls, the function does not check whether this is a child of parent.
		/// </param>
		/// <param name="x">X coordinate. Not used if default(Coord). Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate. Not used if default(Coord).</param>
		public bool ContainsWindowXY(wnd parent, Coord x, Coord y) {
			if (!parent.IsAlive) return false;
			POINT p = Coord.NormalizeInWindow(x, y, parent);
			if (!GetRectIn(parent, out RECT r)) return false;
			if (!r.Contains(x.IsEmpty ? r.left : p.x, y.IsEmpty ? r.top : p.y)) return false;
			return true;
		}

		/// <summary>
		/// This overload calls <see cref="ContainsWindowXY(wnd, Coord, Coord)"/>(Window, x, y).
		/// </summary>
		public bool ContainsWindowXY(Coord x, Coord y) {
			return ContainsWindowXY(Window, x, y);
		}

		#endregion

		#region move, resize, SetWindowPos

		/// <summary>
		/// Calls API <msdn>SetWindowPos</msdn>.
		/// </summary>
		/// <param name="swpFlags"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="cx"></param>
		/// <param name="cy"></param>
		/// <param name="zorderAfter">A window or <see cref="SpecHWND"/>.<b>TOP</b>, <b>BOTTOM</b>, <b>TOPMOST</b>, <b>NOTOPMOST</b>.</param>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool SetWindowPos(SWPFlags swpFlags, int x = 0, int y = 0, int cx = 0, int cy = 0, wnd zorderAfter = default) {
			return Api.SetWindowPos(this, zorderAfter, x, y, cx, cy, swpFlags);
		}

		/// <summary>
		/// Moves and resizes.
		/// </summary>
		/// <remarks>
		/// See also <see cref="Move(Coord, Coord, Coord, Coord, bool, screen)"/>. It is better to use in automation scripts, with windows of any process/thread. It throws exceptions, supports optional/reverse/fractional/workarea coordinates, restores if min/max, does not support SWP flags.
		/// This function is more lightweight, it just calls API <msdn>SetWindowPos</msdn> with flags NOZORDER|NOOWNERZORDER|NOACTIVATE|swpFlagsToAdd. It is better to use in programming, with windows of current thread.
		/// Supports <see cref="lastError"/>.
		/// 
		/// For top-level windows use screen coordinates. For controls - direct parent client coordinates.
		/// </remarks>
		/// <seealso cref="SetWindowPos"/>
		public bool MoveL(int x, int y, int width, int height, SWPFlags swpFlagsToAdd = 0) {
			return SetWindowPos(SWPFlags.NOZORDER | SWPFlags.NOOWNERZORDER | SWPFlags.NOACTIVATE | swpFlagsToAdd, x, y, width, height);
		}

		/// <summary>
		/// Moves and resizes. Same as <see cref="MoveL(int, int, int, int, SWPFlags)"/>.
		/// </summary>
		public bool MoveL(RECT r, SWPFlags swpFlagsToAdd = 0) {
			return MoveL(r.left, r.top, r.Width, r.Height, swpFlagsToAdd);
		}

		/// <summary>
		/// Moves.
		/// </summary>
		/// <remarks>
		/// See also <see cref="Move(Coord, Coord, bool, screen)"/>. It is better to use in automation scripts, with windows of any process/thread. It throws exceptions, supports optional/reverse/fractional/workarea coordinates, restores if min/max.
		/// This function is more lightweight, it just calls API <msdn>SetWindowPos</msdn> with flags NOSIZE|NOZORDER|NOOWNERZORDER|NOACTIVATE. It is better to use in programming, with windows of current thread.
		/// Supports <see cref="lastError"/>.
		/// 
		/// For top-level windows use screen coordinates. For controls - direct parent client coordinates.
		/// </remarks>
		/// <seealso cref="SetWindowPos"/>
		public bool MoveL(int x, int y) {
			return MoveL(x, y, 0, 0, SWPFlags.NOSIZE);
		}

		internal bool MoveL_(POINT p) => MoveL(p.x, p.y);

		/// <summary>
		/// Resizes.
		/// </summary>
		/// <remarks>
		/// See also <see cref="Resize(Coord, Coord, bool, screen)"/>. It is better to use in automation scripts, with windows of any process/thread. It throws exceptions, supports optional/reverse/fractional/workarea coordinates, restores if min/max.
		/// This function is more lightweight, it just calls API <msdn>SetWindowPos</msdn> with flags NOMOVE|NOZORDER|NOOWNERZORDER|NOACTIVATE. It is better to use in programming, with windows of current thread.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		/// <seealso cref="SetWindowPos"/>
		public bool ResizeL(int width, int height) {
			return MoveL(0, 0, width, height, SWPFlags.NOMOVE);
		}

		internal bool ResizeL_(SIZE z) => ResizeL(z.width, z.height);

		/// <summary>
		/// Moves.
		/// </summary>
		/// <param name="x">Left. If default(Coord), does not move in X axis. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Top. If default(Coord), does not move in Y axis.</param>
		/// <param name="workArea"><i>x y</i> are relative to the work area. Not used when this is a child window.</param>
		/// <param name="screen"><i>x y</i> are relative to this screen or its work area. Default - primary. Not used when this is a child window. Example: <c>screen.index(1)</c>.</param>
		/// <exception cref="AuWndException"/>
		/// <remarks>
		/// Also restores the visible top-level window if it is minimized or maximized.
		/// For top-level windows use screen coordinates. For controls - direct parent client coordinates.
		/// With windows of current thread usually it's better to use <see cref="MoveL(int, int)"/>.
		/// </remarks>
		public void Move(Coord x, Coord y, bool workArea = false, screen screen = default) {
			Move(x, y, default, default, workArea, screen);
		}

		/// <summary>
		/// Moves and/or resizes.
		/// </summary>
		/// <param name="x">Left. If default(Coord), does not move in X axis. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Top. If default(Coord), does not move in Y axis.</param>
		/// <param name="width">Width. If default(Coord), does not change width.</param>
		/// <param name="height">Height. If default(Coord), does not change height.</param>
		/// <param name="workArea"><i>x y width height</i> are relative to the work area. Not used when this is a child window.</param>
		/// <param name="screen"><i>x y width height</i> are relative to this screen or its work area. Default - primary. Not used when this is a child window. Example: <c>screen.index(1)</c>.</param>
		/// <remarks>
		/// Also restores the visible top-level window if it is minimized or maximized.
		/// For top-level windows use screen coordinates. For controls - direct parent client area coordinates.
		/// With windows of current thread usually it's better to use <see cref="MoveL(int, int, int, int, SWPFlags)"/>.
		/// </remarks>
		/// <exception cref="AuWndException"/>
		public void Move(Coord x, Coord y, Coord width, Coord height, bool workArea = false, screen screen = default) {
			ThrowIfInvalid();

			wnd w = Get.DirectParent;
			POINT xy, wh;
			if (!w.Is0) {
				xy = Coord.NormalizeInWindow(x, y, w);
				wh = Coord.NormalizeInWindow(width, height, w);
			} else {
				xy = Coord.Normalize(x, y, workArea, screen);
				wh = Coord.Normalize(width, height, workArea, screen, widthHeight: true);
			}

			SWPFlags f = 0; uint getRect = 0;
			if (x.IsEmpty && y.IsEmpty) f |= SWPFlags.NOMOVE; else if (x.IsEmpty) getRect |= 1; else if (y.IsEmpty) getRect |= 2;
			if (width.IsEmpty && height.IsEmpty) f |= SWPFlags.NOSIZE; else if (width.IsEmpty) getRect |= 4; else if (height.IsEmpty) getRect |= 8;

			if (getRect != 0) {
				if (!GetRectIn(w, out RECT r)) ThrowUseNative("*move/resize*");
				if ((getRect & 1) != 0) xy.x = r.left;
				if ((getRect & 2) != 0) xy.y = r.top;
				if ((getRect & 4) != 0) wh.x = r.Width;
				if ((getRect & 8) != 0) wh.y = r.Height;
			}

			//restore min/max, except if child or hidden
			if (w.Is0 && (IsMinimized || IsMaximized) && IsVisible) {
				ShowNotMinMax(true);
				//info: '&& IsVisible' because ShowNotMinMax unhides
			}

			if (!MoveL(xy.x, xy.y, wh.x, wh.y, f)) ThrowUseNative("*move/resize*");

			MinimalSleepIfOtherThread_();
		}

		/// <summary>
		/// Resizes.
		/// </summary>
		/// <param name="width">Width. If default(Coord), does not change width.</param>
		/// <param name="height">Height. If default(Coord), does not change height.</param>
		/// <param name="workArea">For <see cref="Coord.Fraction"/> etc use width/height of the work area. Not used when this is a child window.</param>
		/// <param name="screen">For <b>Coord.Fraction</b> etc use width/height of this screen. Default - primary. Not used when this is a child window. Example: <c>screen.index(1)</c>.</param>
		/// <exception cref="AuWndException"/>
		/// <remarks>
		/// Also restores the visible top-level window if it is minimized or maximized.
		/// With windows of current thread usually it's better to use <see cref="ResizeL(int, int)"/>.
		/// </remarks>
		public void Resize(Coord width, Coord height, bool workArea = false, screen screen = default) {
			Move(default, default, width, height, workArea, screen);
		}

		#endregion

		#region MoveInScreen, EnsureInScreen, Screen

		internal static partial class Internal_ {
			/// <summary>
			/// Used directly by MoveInScreen, EnsureInScreen, RECT.MoveInScreen, RECT.EnsureInScreen. With inRect used by RECT.MoveInRect.
			/// </summary>
			internal static void MoveInScreen(bool bEnsureMethod,
				Coord left, Coord top, bool useWindow, wnd w, ref RECT r,
				screen screen, bool bWorkArea, bool bEnsureInScreen, RECT? inRect = default) {
				RECT rs;
				screen scr;
				if (inRect.HasValue) {
					Debug.Assert(!useWindow);
					rs = inRect.GetValueOrDefault();
					scr = default;
				} else {
					if (!screen.IsEmpty) scr = screen.Now;
					else if (useWindow) scr = screen.of(w);
					else if (bEnsureMethod) scr = screen.of(r);
					else scr = screen.primary;

					rs = scr.GetRect(bWorkArea);

					if (useWindow) {
						if (!w.GetRectNotMinMax(out r)) w.ThrowUseNative("*move*");
					}
				}

				int x, y, wid = r.Width, hei = r.Height;
				if (bEnsureMethod) {
					Debug.Assert(bEnsureInScreen == true && left.IsEmpty && top.IsEmpty); //left/top unused
					x = r.left;
					y = r.top;
				} else {
					if (left.IsEmpty) left = Coord.Center;
					if (top.IsEmpty) top = Coord.Center;
					var p = Coord.NormalizeInRect(left, top, rs);
					x = p.x; y = p.y;
					switch (left.Type) { case CoordType.Reverse: x -= wid; break; case CoordType.Fraction: x -= (int)(wid * left.FractionValue); break; }
					switch (top.Type) { case CoordType.Reverse: y -= hei; break; case CoordType.Fraction: y -= (int)(hei * top.FractionValue); break; }
				}

				if (bEnsureInScreen) {
					x = Math.Max(Math.Min(x, rs.right - wid), rs.left);
					y = Math.Max(Math.Min(y, rs.bottom - hei), rs.top);
					if (r.Width > rs.Width) r.Width = rs.Width;
					if (r.Height > rs.Height) r.Height = rs.Height;
				}

				r.Move(x, y);

				if (useWindow) { //move window
					w.GetWindowPlacement_(out var wp, false, "*move*");
					bool moveMaxWindowToOtherMonitor = wp.showCmd == Api.SW_SHOWMAXIMIZED && !scr.Equals(screen.of(w));
					if (r == wp.rcNormalPosition && !moveMaxWindowToOtherMonitor) return;

					wnd hto = default; bool visible = w.IsVisible;
					try {
						//Windows bug: before a dialog is first time shown, may fail to move if it has an owner window. Depends on coordinates and on don't know what.
						//There are several workarounds. The best of them - temporarily set owner window 0.
						if (!visible) {
							hto = w.Get.Owner;
							if (!hto.Is0 && !WndUtil.SetOwnerWindow(w, default)) hto = default;
						}

						wp.rcNormalPosition = r;
						wp.showCmd = visible ? Api.SW_SHOWNA : Api.SW_HIDE;
						w.SetWindowPlacement_(ref wp, true, "*move*");

						if (moveMaxWindowToOtherMonitor) {
							//I found this way of moving max window to other screen by experimenting.
							//When moved to screen's coordinates and sized to screen's work area size, OS adjusts window pos to be correct, ie border is outside screen, but invisible in adjacent screen.
							//Must call SetWindowPos twice, or it may refuse to move at all.
							//Another way - use SetWindowPlacement to temporarily restore, move to other screen, then maximize. But it unhides hidden window.
							rs = scr.WorkArea;
							if (!w.MoveL(rs.left, rs.top) || !w.ResizeL(rs.Width, rs.Height)) w.ThrowUseNative("*move*");
						}
					}
					finally {
						if (!hto.Is0) WndUtil.SetOwnerWindow(w, hto);
					}

					w.MinimalSleepIfOtherThread_();
				}
			}
		}

		/// <summary>
		/// Moves this window to coordinates x y in specified screen, and ensures that entire window is in screen.
		/// </summary>
		/// <param name="x">X coordinate in the specified screen. If default(Coord) - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in the specified screen. If default(Coord) - screen center.</param>
		/// <param name="screen">Move to this screen (see <see cref="screen"/>). If default, uses screen of this window. Example: <c>screen.index(1)</c>.</param>
		/// <param name="workArea">Use the work area, not whole screen. Default true.</param>
		/// <param name="ensureInScreen">If part of window is not in screen, move and/or resize it so that entire window would be in screen. Default true.</param>
		/// <exception cref="AuWndException"/>
		/// <remarks>
		/// If the window is maximized, minimized or hidden, it will have the new position and size when restored, not immediately, except when moving maximized to another screen.
		/// </remarks>
		/// <seealso cref="RECT.MoveInScreen"/>
		public void MoveInScreen(Coord x, Coord y, screen screen = default, bool workArea = true, bool ensureInScreen = true) {
			RECT r = default;
			Internal_.MoveInScreen(false, x, y, true, this, ref r, screen, workArea, ensureInScreen);
		}

		/// <summary>
		/// Moves this window if need, to ensure that entire window is in screen.
		/// </summary>
		/// <param name="screen">Move to this screen (see <see cref="screen"/>). If default, uses screen of this window.</param>
		/// <param name="workArea">Use the work area, not whole screen. Default true.</param>
		/// <exception cref="AuWndException"/>
		/// <remarks>
		/// If the window is maximized, minimized or hidden, it will have the new position and size when restored, not immediately.
		/// </remarks>
		/// <seealso cref="RECT.EnsureInScreen"/>
		public void EnsureInScreen(screen screen = default, bool workArea = true) {
			RECT r = default;
			Internal_.MoveInScreen(true, default, default, true, this, ref r, screen, workArea, true);
		}

		/// <summary>
		/// Moves this window to the center of the screen. Ensures that entire window is in screen.
		/// </summary>
		/// <param name="screen">Move to this screen (see <see cref="screen"/>). If default, uses screen of this window.</param>
		/// <exception cref="AuWndException"/>
		/// <remarks>Calls <c>ShowNotMinMax(true)</c> and <c>MoveInScreen(default, default, screen, true)</c>. See <see cref="MoveInScreen"/>.</remarks>
		/// <seealso cref="RECT.MoveInScreen"/>
		public void MoveToScreenCenter(screen screen = default) {
			ShowNotMinMax(true);
			MoveInScreen(default, default, screen, true);
		}

		/// <summary>
		/// Gets <see cref="screen"/> of the screen that contains this window (the biggest part of it) or is nearest to it.
		/// If this window handle is default(wnd) or invalid, gets the primary screen.
		/// Calls <see cref="screen.of"/>.
		/// </summary>
		public screen Screen => screen.of(this);

		#endregion

		#region Zorder
		const SWPFlags _SWP_ZORDER_OWNER = SWPFlags.NOMOVE | SWPFlags.NOSIZE | SWPFlags.NOACTIVATE,
			_SWP_ZORDER_NOOWNER = SWPFlags.NOMOVE | SWPFlags.NOSIZE | SWPFlags.NOACTIVATE | SWPFlags.NOOWNERZORDER;
		//info: with SWP_NOOWNERZORDER the API does not zorder owner and owned windows.
		//note: without SWP_NOOWNERZORDER the API is full of bugs. Eg fails if the window has a topmost owned window.

		/// <summary>
		/// Places this window above window <i>w</i> in the Z order.
		/// </summary>
		/// <param name="w">Another window.</param>
		/// <param name="ownerToo">Place owner windows below this window. If false (default), does it only if they otherwise would be on top of this window.</param>
		/// <remarks>
		/// This window and <i>w</i> can be both top-level windows or both controls of same parent.
		/// Can make this window topmost or non-topmost, depending on where <i>w</i> is in the Z order.
		/// Also affects owned and owner windows, but does not make them topmost/nontopmost if not necessary.
		/// 
		/// Uses API <msdn>SetWindowPos</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool ZorderAbove(wnd w, bool ownerToo = false)
			=> _ZorderAB(w, true, ownerToo);

		/// <summary>
		/// Places this window below window <i>w</i> in the Z order.
		/// </summary>
		/// <param name="w">Another window. If default(wnd), calls <see cref="ZorderTop"/>.</param>
		/// <param name="ownerToo">Place owner windows below this window. If false (default), does it only if they otherwise would be on top of this window.</param>
		/// <remarks>
		/// This window and <i>w</i> can be both top-level windows or both controls of same parent.
		/// Can make this window topmost or non-topmost, depending on where <i>w</i> is in the Z order.
		/// Also affects owned and owner windows, but does not make them topmost/nontopmost if not necessary.
		///
		/// Uses API <msdn>SetWindowPos</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool ZorderBelow(wnd w, bool ownerToo = false)
			=> _ZorderAB(w, false, ownerToo);

		bool _ZorderAB(wnd w, bool before, bool ownerToo, bool top = false) {
			if (!top) {
				if (before || !w.Is0) {
					if (!w.IsAlive || w == this) return false;
					if (before) {
					g1: var wp = w.Get.Previous();
						if (wp.Is0 && !w.IsAlive) return false; //w is at the very top?
						w = wp;
						for (wnd ow = Get.Owner; !ow.Is0; ow = ow.Get.Owner) if (w == ow) goto g1;
					}
				}
			}
			if (IsChild) return SetWindowPos(_SWP_ZORDER_NOOWNER, zorderAfter: w);

			//All ZorderX functions work almost like SetWindowPos without SWP_NOOWNERZORDER should work as documented.
			//	However cannot simply call it because of its bugs.
			//	To simulate it, we reposition this and each owned/owner window with SWP_NOOWNERZORDER.
			//	The flag isn't well documented. With it zorders only this window; ignores owner and owned windows.
			//	Speed: 10% slower than SetWindowPos without SWP_NOOWNERZORDER.
			//	Still can't overcome this API bug: can't zorder above a topmost uiAccess window; also Start menu.

			var od = new getwnd.OwnedDescendants_();
			var all = od.all;
			int iLastTopmost = -1; for (int i = 0; i < all.Length && all[i].IsTopmost; i++) iLastTopmost = i;
			int iThis = Array.IndexOf(all, this);
			if (iThis < 0) return false;
			int iW = -1;
			if (top) { //make this the top window in the topmost or nontopmost group
				if (iThis > iLastTopmost && iLastTopmost >= 0) w = all[iW = iLastTopmost];
			} else if (!w.Is0) {
				iW = Array.IndexOf(all, w);
				if (iW < 0) return false;
			}
			bool wasTopmost = iThis <= iLastTopmost;
			bool willBeTopmost = iW < iLastTopmost || (iW == iLastTopmost && wasTopmost);

			//descendants
			bool skipTopmostOwned = !willBeTopmost && !wasTopmost;
			List<int> ai = od.GetIndices(this, skipTopmostOwned ? i => i <= iLastTopmost || i == iW : i => i == iW);
			ai.Add(iThis);

			//owners
			int iSkip = iThis;
			for (wnd ow = Get.Owner; !ow.Is0; ow = ow.Get.Owner) {
				int i2 = Array.IndexOf(all, ow);
				if (i2 < 0 || i2 == iW) break;
				if (willBeTopmost && i2 > iLastTopmost) break; //ignore nontopmost owners
				if (!ownerToo && i2 > iW) break; //don't zorder owners unless they would be on top of this
				List<int> ai2 = od.GetIndices(ow, i => i == iSkip || (!willBeTopmost && i <= iLastTopmost));
				ai.AddRange(ai2);
				ai.Add(iSkip = i2);
			}

			if (ai.Count == 1) return SetWindowPos(_SWP_ZORDER_NOOWNER, zorderAfter: w);

			//With DeferWindowPos faster and no flickering.
			//	But it's unreliable. Eg can't change topmost/notopmost.
			//	If fails, retry with SetWindowPos.

			bool dwpWillFail = willBeTopmost; //will fail if need to move above topmost uiAccess etc windows
			if (!dwpWillFail) { //will fail if need to change topmost/notopmost
				for (int i = 0; i < ai.Count; i++) { if (dwpWillFail = ai[i] <= iLastTopmost) break; }
			}

			var wPrev = w;
			if (!dwpWillFail) {
				var hp = Api.BeginDeferWindowPos(ai.Count);
				for (int i = 0; i < ai.Count; i++) {
					var k = all[ai[i]];
					hp = Api.DeferWindowPos(hp, k, wPrev, 0, 0, 0, 0, _SWP_ZORDER_NOOWNER);
					if (hp == default) break;
					wPrev = k;
				}
				if (hp != default) {
					Api.EndDeferWindowPos(hp); //usually returns true when fails
					wPrev = w;
					for (int i = 0; ;) {
						var k = all[ai[i]];
						if (k.Get.Previous() != wPrev) break;
						if (++i == ai.Count) return true;
						wPrev = k;
					}
				}
				Debug_.Print("DeferWindowPos failed");
				wPrev = w;
			}

			for (int i = 0; i < ai.Count; i++) {
				var k = all[ai[i]];
				bool ok = k.SetWindowPos(_SWP_ZORDER_NOOWNER, zorderAfter: wPrev);
				if (!ok) {
					if (k == this) return false;
					continue;
				}
				wPrev = k;
			}
			return true;
		}

		/// <summary>
		/// Places this window or control at the top of the Z order. Does not activate.
		/// If the window was topmost, it will be at the top of topmost windows, else at the top of other windows.
		/// </summary>
		/// <param name="ownerToo">Place owner windows below this window. If false (default), does it only if they otherwise would be on top of this window.</param>
		/// <remarks>
		/// Also affects owner and owned windows.
		/// Uses API <msdn>SetWindowPos</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool ZorderTop(bool ownerToo = false)
			=> _ZorderAB(default, false, ownerToo, top: true);

		/// <summary>
		/// Calls SetWindowPos with HWND_TOP and SWP_NOOWNERZORDER (ignores owner and owned windows).
		/// </summary>
		internal bool ZorderTopRaw_() {
			Debug_.PrintIf(!IsChild && !Get.EnabledOwned().Is0, "has owned windows: " + ToString());
			if (IsChild || IsTopmost || IsActive) return SetWindowPos(_SWP_ZORDER_NOOWNER);
			getwnd.top2(out var lastTopmost);
			return SetWindowPos(_SWP_ZORDER_NOOWNER, zorderAfter: lastTopmost);
		}

		/// <summary>
		/// Places this window or control at the bottom of the Z order.
		/// </summary>
		/// <remarks>
		/// Also affects owner and owned windows.
		/// If the window was topmost, makes it and its owner and owned windows non-topmost.
		/// Uses API <msdn>SetWindowPos</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool ZorderBottom() {
			return SetWindowPos(_SWP_ZORDER_OWNER, zorderAfter: SpecHWND.BOTTOM);
			//Bug in older Windows 10: the first HWND_BOTTOM moves a topmost window to the top of non-topmost windows.
		}

		/// <summary>
		/// Makes this window topmost (always on top of non-topmost windows in the Z order). Does not activate.
		/// </summary>
		/// <remarks>
		/// This cannot be a control.
		/// Also affects owned windows. Does not affect owners.
		/// Uses API <msdn>SetWindowPos</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool ZorderTopmost() {
			if (!SetWindowPos(_SWP_ZORDER_NOOWNER, zorderAfter: SpecHWND.TOPMOST)) return false;
			var a = Get.AllOwned(allDescendants: true);
			for (int i = a.Length; --i >= 0;) {
				a[i].SetWindowPos(_SWP_ZORDER_NOOWNER, zorderAfter: SpecHWND.TOPMOST);
			}
			return true;
			//note: cannot use without SWP_NOOWNERZORDER because of API bugs.
		}

		///
		[Obsolete, EditorBrowsable(EditorBrowsableState.Never)]
		public bool ZorderTopmost(bool ownerToo)
			=> (ownerToo ? Get.RootOwnerOrThis() : this).ZorderTopmost();

		/// <summary>
		/// Makes this window non-topmost.
		/// </summary>
		/// <param name="afterActiveWindow">Also place this window below the active nontopmost window in the Z order, unless the active window is this or owner.</param>
		/// <remarks>
		/// This cannot be a control.
		/// Also affects owner and owned windows.
		/// Uses API <msdn>SetWindowPos</msdn>.
		/// Supports <see cref="lastError"/>.
		/// </remarks>
		public bool ZorderNoTopmost(bool afterActiveWindow = false) {
			if (!IsTopmost) return IsAlive;
			if (afterActiveWindow) {
				wnd wa = active;
				if (wa != this && !wa.Is0 && !wa.IsTopmost && !Get.Owners().Contains(wa)) {
					if (SetWindowPos(_SWP_ZORDER_OWNER, zorderAfter: wa) && !IsTopmost) return true;
				}
			}
			return SetWindowPos(_SWP_ZORDER_OWNER, zorderAfter: SpecHWND.NOTOPMOST);
		}

		//Windows 7-10 bug: cannot make a topmost uiAccess window non-topmost (with HWND_NOTOPMOST, HWND_BOTTOM or non-topmost hwndInsertAfter).
		//	Workaround: call SWP 2 times. With some Windows updates also need SWP_NOOWNERZORDER.
		//	It seems it's fixed in current Win10.
		//Bug with SWP_NOOWNERZORDER: if used with non-uiAccess windows, then later HWND_TOPMOST does not work.
		//	It seems it's fixed in current Win10. Can reproduce on Win8.1.
		//More problems with topmost uiAccess windows:
		//	Can't insert a uiAccess window after a normal window.
		//	Problems with HWND_BOTTOM and owned windows.
		//	And so on. Possibly some now fixed.

		/// <summary>
		/// Returns true if this is a topmost (always-on-top) window.
		/// </summary>
		public bool IsTopmost => HasExStyle(WSE.TOPMOST);

		/// <summary>
		/// Returns true if this window is above window <i>w</i> in the Z order.
		/// </summary>
		public bool ZorderIsAbove(wnd w) {
			if (w.Is0) return false;
			for (wnd t = this; !t.Is0;) {
				t = Api.GetWindow(t, Api.GW_HWNDNEXT);
				if (t == w) return true;
			}
			return false;
		}

		#endregion

		#region style, exStyle

		/// <summary>
		/// Gets window style.
		/// </summary>
		/// <value>One or more <see cref="WS"/> flags and/or class-specific style flags. Reference: <msdn>window styles</msdn>.</value>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		/// <seealso cref="HasStyle"/>
		/// <seealso cref="SetStyle"/>
		public WS Style {
			get => (WS)(uint)GetWindowLong(GWL.STYLE);
		}

		/// <summary>
		/// Gets window extended style.
		/// </summary>
		/// <value>One or more <see cref="WSE"/> flags. Reference: <msdn>extended window styles</msdn>.</value>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		/// <seealso cref="HasExStyle"/>
		/// <seealso cref="SetExStyle"/>
		public WSE ExStyle {
			get => (WSE)(uint)GetWindowLong(GWL.EXSTYLE);
		}

		/// <summary>
		/// Returns true if the window has all specified style flags (see <see cref="Style"/>).
		/// </summary>
		/// <param name="style">One or more styles.</param>
		/// <param name="any">
		/// Return true if has any (not necessary all) of the specified styles.
		/// Note: don't use <see cref="WS.CAPTION"/>, because it consists of two other styles - BORDER and DLGFRAME.
		/// </param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool HasStyle(WS style, bool any = false) {
			var k = Style & style;
			return any ? k != 0 : k == style;
		}

		/// <summary>
		/// Returns true if the window has all specified extended style flags (see <see cref="ExStyle"/>).
		/// </summary>
		/// <param name="exStyle">One or more extended styles.</param>
		/// <param name="any">Return true if has any (not necessary all) of the specified styles.</param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool HasExStyle(WSE exStyle, bool any = false) {
			var k = ExStyle & exStyle;
			return any ? k != 0 : k == exStyle;
		}

		/// <summary>
		/// Changes window style.
		/// </summary>
		/// <param name="style">One or more <see cref="WS"/> flags and/or class-specific style flags. Reference: <msdn>window styles</msdn>.</param>
		/// <param name="flags"></param>
		/// <returns>Previous value.</returns>
		/// <exception cref="AuWndException"/>
		/// <seealso cref="Style"/>
		public WS SetStyle(WS style, WSFlags flags = 0)
			=> (WS)_SetStyle(false, (int)style, flags);

		/// <summary>
		/// Changes window extended style.
		/// </summary>
		/// <param name="style">One or more <see cref="WSE"/> flags. Reference: <msdn>extended window styles</msdn>.</param>
		/// <param name="flags"></param>
		/// <returns>Previous value.</returns>
		/// <exception cref="AuWndException"/>
		/// <seealso cref="ExStyle"/>
		public WSE SetExStyle(WSE style, WSFlags flags = 0)
			=> (WSE)_SetStyle(true, (int)style, flags);

		nint _SetStyle(bool ex, int style, WSFlags flags) {
			var gwl = ex ? GWL.EXSTYLE : GWL.STYLE;
			switch (flags & (WSFlags.Add | WSFlags.Remove)) {
			case WSFlags.Add: style = (int)GetWindowLong(gwl) | style; break;
			case WSFlags.Remove: style = (int)GetWindowLong(gwl) & ~style; break;
			}
			nint R = SetWindowLong(gwl, style, noException: flags.Has(WSFlags.NoException));

			if (flags.Has(WSFlags.UpdateNonclient)) SetWindowPos(SWPFlags.FRAMECHANGED | SWPFlags.NOMOVE | SWPFlags.NOSIZE | SWPFlags.NOZORDER | SWPFlags.NOOWNERZORDER | SWPFlags.NOACTIVATE);
			if (flags.Has(WSFlags.UpdateClient)) Api.InvalidateRect(this, true);

			return R;
		}

		/// <summary>
		/// Returns true if has WS.POPUP style.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool IsPopupWindow => HasStyle(WS.POPUP);

		/// <summary>
		/// Returns true if has WSE.TOOLWINDOW style.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool IsToolWindow => HasExStyle(WSE.TOOLWINDOW);

		/// <summary>
		/// Returns true if has WS.THICKFRAME style.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool IsResizable => HasStyle(WS.THICKFRAME);

		#endregion

		#region window/class long, control id, prop

		/// <summary>
		/// Calls API <msdn>GetWindowLongPtr</msdn>.
		/// </summary>
		/// <param name="index">A constant from <see cref="GWL"/>, or an offset in window memory reserved when registering window class.</param>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// In 32-bit process actually calls <b>GetWindowLong</b>, because <b>GetWindowLongPtr</b> is unavailable.
		/// </remarks>
		public nint GetWindowLong(int index) => Api.GetWindowLongPtr(this, index);

		/// <summary>
		/// Calls API <msdn>SetWindowLongPtr</msdn>.
		/// </summary>
		/// <param name="index">A constant from <see cref="GWL"/>, or an offset in window memory reserved when registering window class.</param>
		/// <param name="newValue">New value.</param>
		/// <param name="noException">Don't throw exception when fails.</param>
		/// <returns>Previous value. If fails and <i>noException</i> true, returns 0, and can be used <see cref="lastError"/>.</returns>
		/// <exception cref="AuWndException"/>
		/// <remarks>
		/// See also API <msdn>SetWindowSubclass</msdn>.
		/// </remarks>
		public nint SetWindowLong(int index, nint newValue, bool noException = false) {
			lastError.clear();
			var prev = Api.SetWindowLongPtr(this, index, newValue);
			if (prev == 0 && !noException) {
				var e = lastError.code;
				if (e != 0 && (Api.GetWindowLongPtr(this, index) != newValue || !IsAlive)) ThrowUseNative(e);
				//if GWL_HWNDPARENT(0), last error "invalid window handle"
			}
			return prev;
		}

		/// <summary>
		/// Gets or sets id of this control.
		/// The 'get' function supports <see cref="lastError"/>.
		/// </summary>
		/// <exception cref="AuWndException">The 'set' function failed.</exception>
		public int ControlId {
			get => Api.GetDlgCtrlID(this);
			set { SetWindowLong(GWL.ID, value); }
		}

		/// <summary>
		/// Returns an object that manages window properties using API <msdn>SetProp</msdn> and co.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// var w = wnd.find("* Explorer");
		/// w.Prop.Set("example", 5);
		/// print.it(w.Prop["example"]);
		/// print.it(w.Prop); //all w properties
		/// w.Prop.Remove("example"); //you should always remove window properties if don't want to see unrelated applications crashing after some time. And don't use many unique property names.
		/// ]]></code>
		/// </example>
		public WProp Prop => new(this);

		/// <summary>
		/// Returns an object that manages the taskbar button of this window: flash, progress, add/delete.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// var w = wnd.find(0, "*Notepad", "Notepad");
		/// w.TaskbarButton.Delete();
		/// ]]></code>
		/// </example>
		public WTaskbarButton TaskbarButton => new(this);

		#endregion

		#region thread, process, is Unicode, is 64-bit, is hung/ghost, is console, UAC, is message-only

		/// <summary>
		/// Calls API <msdn>GetWindowThreadProcessId</msdn>.
		/// Returns mative thread id and also gets process id.
		/// Returns 0 if fails. Supports <see cref="lastError"/>.
		/// </summary>
		/// <remarks>
		/// It is not the same as <see cref="Thread.ManagedThreadId"/>.
		/// </remarks>
		public unsafe int GetThreadProcessId(out int processId) {
			int pid = 0, tid = Api.GetWindowThreadProcessId(this, &pid);
			processId = pid;
			return tid;
		}

		/// <summary>
		/// Gets native thread id of this window. Calls API <msdn>GetWindowThreadProcessId</msdn>.
		/// Returns 0 if fails. Supports <see cref="lastError"/>.
		/// </summary>
		/// <remarks>
		/// It is not the same as <see cref="Thread.ManagedThreadId"/>.
		/// </remarks>
		public int ThreadId => Api.GetWindowThreadProcessId(this, null); //2 times faster when pid null

		/// <summary>
		/// Gets native process id of this window. Calls API <msdn>GetWindowThreadProcessId</msdn>.
		/// Returns 0 if fails. Supports <see cref="lastError"/>.
		/// </summary>
		public int ProcessId { get { GetThreadProcessId(out var pid); return pid; } }

		/// <summary>
		/// Returns true if this window belongs to the current thread, false if to another thread.
		/// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// Calls API <msdn>GetWindowThreadProcessId</msdn>.
		/// </summary>
		public bool IsOfThisThread => Api.GetCurrentThreadId() == ThreadId;

		/// <summary>
		/// Returns true if this window belongs to the current process, false if to another process.
		/// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// Calls API <msdn>GetWindowThreadProcessId</msdn>.
		/// </summary>
		public bool IsOfThisProcess => Api.GetCurrentProcessId() == ProcessId;

		/// <summary>
		/// Returns true if the window is a Unicode window, false if ANSI.
		/// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// Calls API <msdn>IsWindowUnicode</msdn>.
		/// </summary>
		public bool IsUnicode => Api.IsWindowUnicode(this);

		/// <summary>
		/// Returns true if the window is of a 32-bit process, false if of a 64-bit process.
		/// Also returns false if fails. Supports <see cref="lastError"/>.
		/// </summary>
		/// <remarks>
		/// <note>If you know that the window belongs to current process, instead use <see cref="osVersion"/> functions or <c>IntPtr.Size==4</c>. This function is much slower.</note>
		/// </remarks>
		public bool Is32Bit => process.is32Bit(ProcessId);

		/// <summary>
		/// Returns true if thread of this window is considered hung (not responding).
		/// Calls API <msdn>IsHungAppWindow</msdn>.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool IsHung => Api.IsHungAppWindow(this);

		/// <summary>
		/// Returns true if the window is a ghost window that the system creates over a hung (not responding) window to allow the user to minimally interact with it.
		/// </summary>
		public bool IsHungGhost {
			get => IsHung && ClassNameIs("Ghost") && ProgramName.Eqi("DWM.exe");
			//Class is "Ghost", exe is "DWM" (even if no Aero), text sometimes ends with "(Not Responding)".
			//IsHungWindow returns true for ghost window, although it is not actually hung. It is the fastest.
			//TEST: undocumented API HungWindowFromGhostWindow
		}

		/// <summary>
		/// Returns true if this is a console window (class name "ConsoleWindowClass").
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool IsConsole => ClassNameIs("ConsoleWindowClass");

		//FUTURE: GetConsoleText. See QM2.

		internal void UacCheckAndThrow_(string prefix = null) {
			if (!Is0 && UacAccessDenied) {
				if (prefix == null) prefix = "Failed. The"; else if (prefix.Ends('.')) prefix += " The"; //this is to support prefix used by mouse.move: "The active"
				throw new AuException(Api.ERROR_ACCESS_DENIED, prefix + " window's process has a higher UAC integrity level (admin or uiAccess) than this process.");
			}
		}

		/// <summary>
		/// Gets UAC info of the process.
		/// </summary>
		public uacInfo Uac => uacInfo.ofProcess(ProcessId);

		/// <summary>
		/// Returns true if [](xref:uac) would not allow to automate the window.
		/// It happens when current process has lower UAC integrity level and is not uiAccess, unless UAC is turned off.
		/// </summary>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public bool UacAccessDenied {
			get {
				if (uacInfo.isAdmin) return false;
				lastError.clear();
				//Api.RemoveProp(this, 0); //documented ERROR_ACCESS_DENIED, and used to work, but on latest Win10 GetLastError always returns 0.
				Api.SetWindowLongPtr(this, -1111, 0); //ERROR_INVALID_INDEX or ERROR_ACCESS_DENIED
				return lastError.code == Api.ERROR_ACCESS_DENIED;
			}
		}

		//These are not useful. Use IsAccessDenied or class uacInfo.
		///// <summary>
		///// Gets UAC integrity level of window's process.
		///// Returns UacIL.Unknown if fails.
		///// This function considers UIAccess equal to High.
		///// See also: class uacInfo.
		///// </summary>
		//public UacIL UacIntegrityLevel
		//{
		//	get { var p = uacInfo.ofProcess(ProcessId); return p == null ? UacIL.Unknown : p.IntegrityLevel; }
		//}

		///// <summary>
		///// Returns true if window's process has higher UAC integrity level (IL) than current process.
		///// Returns true if fails to open process handle, which usually means that the process has higher integrity level.
		///// This function considers UIAccess equal to High.
		///// See also: class uacInfo.
		///// </summary>
		//public bool UacIntegrityLevelIsHigher
		//{
		//	get => UacIntegrityLevel > uacInfo.ofThisProcess.IntegrityLevel;
		//}

		/// <summary>
		/// Returns true if this is a <msdn>message-only window</msdn>.
		/// </summary>
		/// <remarks>
		/// To find message-only windows use <see cref="findFast"/>.
		/// </remarks>
		public bool IsMessageOnly {
			get {
				var t = HasStyle(WS.CHILD) ? Window : this;
				var v = t.ParentGWL_;
				if (v != default) {
					if (!s_messageOnlyParent.IsAlive) {
						for (int i = 0; i < 5; i++) {
							var w = findFast(null, null, true); //find a message-only window
							if (w.Is0) return false; //if there are no message-only windows, this window isn't message only
							w = w.ParentGWL_;
							if (!w.Is0) { s_messageOnlyParent = w; break; } //if 0, either w destroyed/reparented or GetWindowLong(GWL_HWNDPARENT) always returns 0 on this OS version
						}
						Debug.Assert(!s_messageOnlyParent.Is0);
					}
					return v == s_messageOnlyParent;
				}
				return false;
			}
		}
		static wnd s_messageOnlyParent;

		#endregion

		#region text, class, program

		/// <summary>
		/// Gets window class name.
		/// Returns null if fails, eg if the window is closed. Supports <see cref="lastError"/>.
		/// </summary>
		[SkipLocalsInit]
		public string ClassName {
			get {
				var b = stackalloc char[260];
				int n = Api.GetClassName(this, b, 260);
				if (n == 0) return null;
				return new string(b, 0, n);
			}
		}

		/// <summary>
		/// Returns true if the class name of this window matches cn. Else returns false.
		/// Also returns false when fails (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="cn">Class name. Case-insensitive wildcard. See <see cref="ExtString.Like(string, string, bool)"/>. Cannot be null.</param>
		/// <seealso cref="IsMatch"/>
		public bool ClassNameIs(string cn) => ClassName.Like(cn, true);

		/// <summary>
		/// If window class name matches one of strings in <i>classNames</i>, returns 1-based string index. Else returns 0.
		/// Also returns 0 if fails to get class name (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="classNames">Class names. Case-insensitive wildcard. See <see cref="ExtString.Like(string, string, bool)"/>. The array and strings cannot be null.</param>
		public int ClassNameIs(params string[] classNames) {
			//I expect this func will be often used instead of slow code like 'if (w.ClassName == "a" || w.ClassName == "b" || ...)'.

			string s = ClassName; if (s == null) return 0;
			return s.Like(true, classNames);
		}

		/// <summary>
		/// Gets name.
		/// Returns "" if no name. Returns null if fails, eg if the window is closed. Supports <see cref="lastError"/>.
		/// </summary>
		/// <remarks>
		/// Top-level window name usually its title bar text.
		/// Control name usually is its text that does not change, for example button or static (label) control text.
		/// Unlike <see cref="ControlText"/>, this function usually does not get variable text, for example Edit control editable text, ComboBox control selected item text, status bar text.
		/// Calls <see cref="GetText"/>(false, true).
		/// </remarks>
		/// <seealso cref="SetText"/>
		/// <seealso cref="ControlText"/>
		/// <seealso cref="NameElm"/>
		/// <seealso cref="NameWinforms"/>
		public string Name => GetText(false, true);

		/// <summary>
		/// If window name matches one of strings in <i>names</i>, returns 1-based string index. Else returns 0.
		/// Also returns 0 if fails to get name (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="names">Window names. Case-insensitive wildcard. See <see cref="ExtString.Like(string, string, bool)"/>. The array and strings cannot be null.</param>
		public int NameIs(params string[] names) {
			//I expect this func will be often used instead of slow code like 'if (w.Name == "a" || w.Name == "b" || ...)'.

			string s = Name; if (s == null) return 0;
			return s.Like(true, names);
		}

		/// <summary>
		/// Gets window name using API InternalGetWindowText. The same as GetText(false, false).
		/// This should be a top-level window, because does not process ampersands.
		/// </summary>
		internal string NameTL_ => _GetTextFast(false);

		/// <summary>
		/// Gets control text.
		/// Returns "" if no text. Returns null if fails, eg if the window is closed. Supports <see cref="lastError"/>.
		/// </summary>
		/// <remarks>
		/// Unlike <see cref="Name"/>, this function prefers variable text, for example Edit control editable text, ComboBox control selected item text, status bar text.
		/// For controls that cannot have such text (eg button, static), it usually gets the same text as <b>Name</b>. For example button and static (label) controls.
		/// Much slower than <b>Name</b>. Fails if the window is hung.
		/// Calls <see cref="GetText"/>(true, false).
		/// </remarks>
		/// <seealso cref="SetText"/>
		/// <seealso cref="Name"/>
		public string ControlText => GetText(true, false);

		/// <summary>
		/// Gets window/control name or control text.
		/// Returns "" if it is empty.
		/// Returns null if fails, eg if the window is closed. Supports <see cref="lastError"/>.
		/// This is a low-level function. You can instead use <see cref="Name"/> and <see cref="ControlText"/>.
		/// </summary>
		/// <param name="getText">
		/// How to get text:
		/// - false - use API <msdn>InternalGetWindowText</msdn>. This is used by <see cref="Name"/>.
		/// - true - use API <msdn>WM_GETTEXT</msdn>. It is slow and prefers editable text. This is used by <see cref="ControlText"/>. Fails if the window is hung.
		/// - null - try <b>InternalGetWindowText</b>. If it gets "" and this is a control, then try WM_GETTEXT.
		/// </param>
		/// <param name="removeUnderlineAmpersand">
		/// Remove the invisible '&amp;' characters that are used to underline keyboard shortcuts with the Alt key.
		/// Removes only if this is a control (has style WS.CHILD).
		/// Calls <see cref="StringUtil.RemoveUnderlineChar"/>.
		/// </param>
		/// <seealso cref="SetText"/>
		/// <seealso cref="NameElm"/>
		/// <seealso cref="NameWinforms"/>
		public string GetText(bool? getText = null, bool removeUnderlineAmpersand = true) {
			string R;

			if (getText == null) R = _GetTextFast(true);
			else if (getText.GetValueOrDefault()) R = _GetTextSlow();
			else R = _GetTextFast(false);

			if (removeUnderlineAmpersand
				&& !R.NE()
				//&& R.Contains('&') //slower than HasStyle if the string is longer than 20
				&& HasStyle(WS.CHILD)
				) R = StringUtil.RemoveUnderlineChar(R);

			return R;
		}

		/// <summary>
		/// Gets text.
		/// Returns "" if it is empty.
		/// Returns null if fails, eg if the control is destroyed or its thread is hung. Supports <see cref="lastError"/>.
		/// Calls API InternalGetWindowText. If it fails, and getControlTextIfEmpty==true, and this is a control, calls _GetTextSlow, which uses WM_GETTEXT.
		/// </summary>
		[SkipLocalsInit]
		string _GetTextFast(bool useSlowIfEmpty) {
			if (Is0) return null;
			using FastBuffer<char> b = new();
			for (; ; b.More()) {
				lastError.clear();
				int nr = Api.InternalGetWindowText(this, b.p, b.n);
				if (nr < b.n - 1) {
					if (nr > 0) return new string(b.p, 0, nr);
					if (lastError.code != 0) return null;
					if (useSlowIfEmpty && HasStyle(WS.CHILD)) return _GetTextSlow();
					return "";
				}
			}
		}

		/// <summary>
		/// Gets text.
		/// Returns "" if it is empty.
		/// Returns null if fails, eg if the control is destroyed or its thread is hung. Supports <see cref="lastError"/>.
		/// Uses WM_GETTEXT.
		/// </summary>
		[SkipLocalsInit]
		string _GetTextSlow() {
			if (!SendTimeout(5000, out nint n, Api.WM_GETTEXTLENGTH)) return null;
			if (n < 1) return "";

			using FastBuffer<char> b = new((int)n + 1);
			if (!SendTimeout(30000, out n, Api.WM_GETTEXT, b.n, b.p)) return null;
			if (n < 1) return "";
			b.p[n] = '\0';
			return b.GetStringFindLength(); //info: some controls return incorrect n, eg including '\0'

			//note: cannot do this optimization:
			//	At first allocate stack memory and send WM_GETTEXT without WM_GETTEXTLENGTH. Then use WM_GETTEXTLENGTH/WM_GETTEXT if returned size is buffer length - 1.
			//	It works with most controls, but some controls return 0 if buffer is too small. Eg SysLink. The WM_GETTEXT documentation does not say what should happen when buffer is too small.
			//	The speed is important for wnd.Child().
			//	It is ~30% faster when all controls have text, but not much if called for many controls that don't have text (then we don't use WM_GETTEXT).
		}

		/// <summary>
		/// Sets window/control name or control text.
		/// </summary>
		/// <param name="text">Text. Can be null, it is the same as "".</param>
		/// <remarks>
		/// Uses API <msdn>WM_SETTEXT</msdn>.
		/// Top-level window name usually its title bar text.
		/// For variable-text controls (edit, combo box, status bar, ...) this usually is the text that <see cref="ControlText"/> would get.
		/// For other controls (button, static, ...) and top-level windows this usually is the text that <see cref="Name"/> would get.
		/// </remarks>
		/// <exception cref="AuWndException">Failed, for example the window is closed.</exception>
		/// <seealso cref="GetText"/>
		/// <seealso cref="Name"/>
		/// <seealso cref="ControlText"/>
		public void SetText(string text) {
			if (!SendTimeout(30000, out var _, Api.WM_SETTEXT, 0, text ?? "", 0)) ThrowUseNative();
		}

		//rejected: faster but not better than NameElm. Or must be improved.
		//	Instead, let Child find label control, and then navigate with Get.Right() etc.
		///// <summary>
		///// Gets <see cref="Name"/> of previous (in Z order) sibling control.
		///// Returns null if there is no such control.
		///// </summary>
		///// <remarks>
		///// Can be used to identify controls that have no name but have a named control (label) at the left or above. For example Edit And ComboBox controls in dialogs.
		///// In such cases this function works like <see cref="NameElm"/>, but is faster and supports any sibling control type.
		///// </remarks>
		//public string NameLabel
		//{
		//	get
		//	{
		//		for(var w = this; ;) {
		//			var p = w.Get.DirectParent;
		//			if(p.Is0) return null;
		//			w = w.Get.Previous(); if(!w.Is0) return w.Name;
		//			w = p;
		//		}
		//		//_todo: if the label control is not at the left/above, find topmost control at the left/above
		//	}
		//}
#if false //artifacts of xml doc from various places. Maybe it will come back some day. The code is commented out there.
		//from wnd.Child comments:
		/// - "***label " - use <see cref="NameLabel"/>.
		/// <br/>Useful when the control itself does not have a name but an adjacent control is used as its name. Examples - Edit controls in dialogs.
#endif

		/// <summary>
		/// Gets <see cref="elm.Name"/> of the UI element (role WINDOW) of this window or control.
		/// Returns "" if the object has no name or failed to get it. Returns null if invalid window handle.
		/// </summary>
		public string NameElm => elm.NameOfWindow_(this);

		/// <summary>
		/// Gets Control.Name property of a .NET Windows Forms control.
		/// Returns null if it is not a Windows Forms control or if fails.
		/// </summary>
		/// <remarks>
		/// <note>Use this with controls of other processes. Don't use with your controls, when you have a Control object.</note>
		/// 
		/// <note>Slow when getting names of multiple controls in a window. Instead create a <see cref="WinformsControlNames"/> instance and call its <see cref="WinformsControlNames.GetControlName"/> method for each control.</note>
		/// </remarks>
		/// <seealso cref="WinformsControlNames.IsWinformsControl"/>
		public string NameWinforms => WinformsControlNames.GetSingleControlName(this);

		/// <summary>
		/// Gets filename of process executable file, like "notepad.exe".
		/// Return null if fails.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="ProcessId"/> and <see cref="process.getName"/>.
		/// This function is much slower than getting window name or class name. Don't use code like <c>if(w.ProgramName=="A" || w.ProgramName=="B")</c>. Instead use <c>var s=w.ProgramName; if(s=="A" || s=="B")</c>.
		/// </remarks>
		public string ProgramName => process.GetNameCached_(this, ProcessId);

		/// <summary>
		/// If window program name matches one of strings in <i>programNames</i>, returns 1-based string index. Else returns 0.
		/// Also returns 0 if fails to get program name (probably window closed or 0 handle). Supports <see cref="lastError"/>.
		/// </summary>
		/// <param name="programNames">Program names, like "notepad.exe". Case-insensitive wildcard. See <see cref="ExtString.Like(string, string, bool)"/>. The array and strings cannot be null.</param>
		public int ProgramNameIs(params string[] programNames) {
			//I expect this func will be often used instead of slow code like 'if (w.ProgramName == "a" || w.ProgramName == "b" || ...)'.

			string s = ProgramName; if (s == null) return 0;
			return s.Like(true, programNames);
		}

		/// <summary>
		/// Gets full path of process executable file.
		/// Return null if fails.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="ProcessId"/> and <see cref="process.getName"/>.
		/// This function is much slower than getting window name or class name. Don't use code like <c>if(w.ProgramPath=="A" || w.ProgramPath=="B")</c>. Instead use <c>var s=w.ProgramPath; if(s=="A" || s=="B")</c>.
		/// </remarks>
		public string ProgramPath => process.GetNameCached_(this, ProcessId, true);

		/// <summary>
		/// Gets description of process executable file.
		/// Return null if fails.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="ProcessId"/> and <see cref="process.getDescription"/>.
		/// This function is slow. Much slower than <see cref="ProgramName"/>.
		/// </remarks>
		public string ProgramDescription => process.getDescription(ProcessId);

		#endregion

		#region close, destroy

		/// <summary>
		/// Closes the window.
		/// </summary>
		/// <returns>true if successfuly closed or if it was already closed (the handle is 0 or invalid) or if <i>noWait</i>==true.</returns>
		/// <param name="noWait">
		/// If true, does not wait until the window is closed.
		/// If false, waits about 1 s (depends on window type etc) until the window is destroyed or disabled.
		/// </param>
		/// <param name="useXButton">
		/// If false (default), uses API message <msdn>WM_CLOSE</msdn>.
		/// If true, uses API message <msdn>WM_SYSCOMMAND SC_CLOSE</msdn>, like when the user clicks the X button in the title bar.
		/// Most windows can be closed with any of these messages, but some respond properly only to one of them. For example, some applications on WM_CLOSE don't exit, although the main window is closed. Some applications don't respond to WM_SYSCOMMAND if it is posted soon after opening the window, for example Internet Explorer.
		/// </param>
		/// <remarks>
		/// The window may refuse to be closed. For example, it may be hung, or hide itself instead, or display a "Save?" message box, or is a dialog without X button, or just need more time to close it.
		/// If the window is of this thread, just calls <see cref="Send"/> or <see cref="Post"/> (if <i>noWait</i>==true) and returns true.
		/// </remarks>
		/// <seealso cref="WaitForClosed"/>
		/// <example>
		/// <code><![CDATA[
		/// //close all Notepad windows
		/// foreach (var w in wnd.findAll("* Notepad", "Notepad")) w.Close();
		/// ]]></code>
		/// </example>
		public bool Close(bool noWait = false, bool useXButton = false) {
			if (!IsAlive) return true;

			int msg = Api.WM_CLOSE; int wparam = 0; if (useXButton) { msg = Api.WM_SYSCOMMAND; wparam = Api.SC_CLOSE; }

			if (IsOfThisThread) {
				if (noWait) Post(msg, wparam);
				else Send(msg, wparam);
				return true;
			}

			//Some windows cannot be properly closed using only WM_CLOSE. For example, VS 7 window closed, but the process not.
			//Some windows (eg IE), ignore SC_CLOSE if it is posted soon after starting the program. Only if using Post, not if SendNotify.
			//When the user closes a window, Windows sends SC_CLOSE. DefWindowProc on SC_CLOSE sends WM_CLOSE, except if disabled.

			//Other apps use either WM_CLOSE or SC_CLOSE.
			//	Task Manager and taskbar send (not post) SC_CLOSE.
			//	Process Explorer and taskkill post WM_CLOSE.

			//For our purposes WM_CLOSE is probably better.
			//	QM2 used SC_CLOSE or WM_CLOSE depending on window style.

			//note: Don't use SendX messages because of possible crashes and other anomalies.
			//	Eg it can hide (not close) some windows, and next app instance does not start (eg Firefox).
			//	Imagine if the target window's app is in SendMessage which dispatches foreign messages, and after it returns, the window is already died.

			bool ok = Post(msg, wparam);
			if (!ok) {
				//print.it(lastError.code); //0 when UAC access denied
				if (!useXButton) ok = Post(Api.WM_SYSCOMMAND, Api.SC_CLOSE); //UAC blocks WM_CLOSE but not WM_SYSCOMMAND
			}

			//if(!noWait.HasValue) noWait = this_thread_is_UI; //rejected
			if (noWait) return true;

			if (ok) {
				for (int i = 0; i < 100; i++) {
					Thread.Sleep(15);
					if (!IsEnabled(false)) break; //destroyed or has an owned modal dialog box, eg "Save?"

					//Wait less if hidden, eg has a tray icon.
					//Also if a popup, eg a Yes/No message box (disabled X button).
					//Also if child.
					//if(!IsVisible && !_IsBusy(2)) i += 4; //unreliable, too dirty
					if (!IsVisible || (Style & (WS.POPUP | WS.CHILD)) != 0) i += 2;

					if (i >= 50) {
						if (!SendTimeout(200, out _, 0)) {
							if (!IsAlive || IsHung) break;
						}
					}
				}
			}
			MinimalSleepNoCheckThread_();
			WndUtil.WaitForAnActiveWindow();

			return !IsAlive;
		}

		//bool _IsBusy(int milliseconds)
		//{
		//	//Need to measure time. Cannot use just 2 ms timeout and ST return value because of the system timer default period 15.6 ms etc.
		//	var t = perf.mcs;
		//	SendTimeout(5 + milliseconds, 0, flags: 0);
		//	var d = perf.mcs - t;
		//	//print.it(d);
		//	return (d >= milliseconds * 1000L);
		//}

		//Rarely used. It is easy, and there is example in Close() help: foreach (var w in wnd.findAll("* Notepad", "Notepad")) w.Close();
		///// <summary>
		///// Closes all matching windows.
		///// Calls <see cref="FindAll"/>. All parameters etc are the same. Then calls <see cref="Close"/> for each found window.
		///// Returns the number of found windows.
		///// </summary>
		//public static int closeAll(
		//	string name, string cn = null, WOwner of = default,
		//	WFlags flags = 0, Func<wnd, bool> f = null, WContains contains = default
		//	)
		//{
		//	var a = FindAll(name, cn, of, flags, f, contains);
		//	foreach(wnd w in a) w.Close();
		//	return a.Count;
		//}

		#endregion

	}

}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="wnd.SetStyle"/> and <see cref="wnd.SetExStyle"/>.
	/// </summary>
	[Flags]
	public enum WSFlags {
		/// <summary>Add the specified styles and don't change others.</summary>
		Add = 1,

		/// <summary>Remove the specified styles and don't change others.</summary>
		Remove = 2,

		/// <summary>Update non-client area (frame, title bar).</summary>
		UpdateNonclient = 4,

		/// <summary>Update client area.</summary>
		UpdateClient = 8,

		/// <summary>Don't throw exception when fails.</summary>
		NoException = 16,
	}
}
