//FUTURE: ChildFromXY. Like wnd.ChildFromXY. Use IAccessible.accHitTest.

namespace Au {
	public unsafe partial class elm {
		/// <summary>
		/// Gets the container window or control of this UI element.
		/// </summary>
		/// <returns><c>default(wnd)</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// All UI elements must support this property, but some have bugs and can fail or return a wrong window.
		/// Uses API <msdn>WindowFromAccessibleObject</msdn>.
		/// </remarks>
		public wnd WndContainer {
			get {
				ThrowIfDisposed_();
				_Hresult(_FuncId.container_window, _GetWnd(out var w));
				return w;
			}
		}
		
		/// <summary>
		/// Low-level version of <see cref="WndContainer"/>. Does not call <b>ThrowIfDisposed_</b> and <b>_Hresult</b> (<b>lastError</b>).
		/// </summary>
		/// <returns><b>HRESULT</b></returns>
		int _GetWnd(out wnd w) {
			int hr = Cpp.Cpp_AccGetInt(this, 'w', out var i);
			GC.KeepAlive(this);
			w = (wnd)i;
			return hr;
		}
		
		/// <summary>
		/// Gets the top-level window that contains this UI element.
		/// </summary>
		/// <returns><c>default(wnd)</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// All UI elements must support this property, but some have bugs and can return <c>default(wnd)</c>.
		/// Uses API <msdn>WindowFromAccessibleObject</msdn> and API <msdn>GetAncestor</msdn>.
		/// </remarks>
		public wnd WndTopLevel => WndContainer.Window;
		//note: named not WndWindow, to avoid using accidentally instead of WndContainer.
		
		/// <summary>
		/// Gets location of this UI element in screen.
		/// </summary>
		/// <returns>Empty rectangle if failed or this property is unavailable. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Calls <see cref="GetRect(out RECT, bool)"/>.
		/// Most but not all UI elements support this property.
		/// </remarks>
		public RECT Rect { get { GetRect(out var r); return r; } }
		
		internal RECT RectRawDpi_ { get { GetRect(out var r, true); return r; } }
		
		/// <summary>
		/// Gets location of this UI element in screen.
		/// </summary>
		/// <returns><c>false</c> if failed or this property is unavailable. Supports <see cref="lastError"/>.</returns>
		/// <param name="r">Rectangle in screen coordinates.</param>
		/// <param name="raw">
		/// Don't DPI-scale. When the element is in a DPI-scaled/virtualized window (see <see cref="Dpi.IsWindowVirtualized"/>), the raw rectangle may not match the visible rectangle.
		/// This parameter is ignored on Windows 7 and 8.0 or if this element was retrieved not in-process.
		/// </param>
		/// <remarks>
		/// Most but not all UI elements support this property.
		/// </remarks>
		public bool GetRect(out RECT r, bool raw = false) {
			ThrowIfDisposed_();
			if (!raw && MiscFlags.Has(EMiscFlags.InProc) && osVersion.minWin8_1) {
				if (!GetProperties("D", out var p)) { r = default; return false; }
				r = p.Rect;
			} else {
				var hr = _Hresult(_FuncId.rectangle, Cpp.Cpp_AccGetRect(this, out r));
				GC.KeepAlive(this);
				if (hr != 0) return false;
			}
			return true;
		}
		
		/// <summary>
		/// Gets location of this UI element in the client area of window <i>w</i>.
		/// </summary>
		/// <returns><c>false</c> if failed or this property is unavailable. Supports <see cref="lastError"/>.</returns>
		/// <param name="r">Receives rectangle in <i>w</i> client area coordinates.</param>
		/// <param name="w">Window or control.</param>
		/// <param name="intersect">Intersect the rectangle with the <i>w</i> client area, possibly making it smaller or empty.</param>
		/// <remarks>
		/// Most but not all UI elements support this property.
		/// Uses <see cref="GetRect(out RECT, bool)"/> and <see cref="wnd.MapScreenToClient(ref RECT)"/>.
		/// </remarks>
		public bool GetRect(out RECT r, wnd w, bool intersect = false) {
			if (!(GetRect(out r) && w.MapScreenToClient(ref r))) return false;
			if (intersect) r.Intersect(_GetContainerClientRect(w));
			return true;
			
			RECT _GetContainerClientRect(wnd w) {
				var rc = w.ClientRect;
				//if w is a classic listview in report view, exclude header
				if (Item > 0 && RoleInt == ERole.LISTITEM) {
					var h = w.ChildFast(null, "SysHeader32");
					if (h.IsVisible) rc.top = h.Rect.Height;
				}
				return rc;
			}
		}
		
		/// <summary>
		/// Gets role as enum <see cref="ERole"/>.
		/// </summary>
		/// <returns>0 (<b>ERole.None</b>) if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Most UI elements have a standard role, defined in enum <see cref="ERole"/> (except <b>None</b> and <b>Custom</b>). Some UI elements have a custom role, usually as string; then returns <b>ERole.Custom</b>.
		/// All UI elements must support this property. If failed, probably the <b>elm</b> is invalid, for example the window is closed.
		/// </remarks>
		public ERole RoleInt {
			get {
				ThrowIfDisposed_();
				if (_misc.roleByte != 0) return (ERole)_misc.roleByte;
				_Hresult(_FuncId.role, _GetRole(out var role, out _, dontNeedStr: true));
				//Debug_.Print("roleByte 0 -> " + role + ", " + Role); //it's OK in some cases, eg when retrieved not inproc, or after navigating or changing simpleelementid
				return role;
			}
		}
		
		/// <summary>
		/// Gets standard or custom role, as string.
		/// </summary>
		/// <returns><c>""</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Most UI elements have a standard role, defined in enum <see cref="ERole"/> (except <b>None</b> and <b>Custom</b>). Some UI elements have a custom role, usually as string.
		/// For standard roles this function returns enum <see cref="ERole"/> member name. For string roles - the string. For unknown non-string roles - the <b>int</b> value like <c>"0"</c> or <c>"500"</c>.
		/// All UI elements must support this property. If failed, probably the <b>elm</b> is invalid, for example the window is closed.
		/// </remarks>
		public string Role {
			get {
				ThrowIfDisposed_();
				var role = (ERole)_misc.roleByte;
				if (role is 0 or ERole.Custom) {
					if (0 != _Hresult(_FuncId.role, _GetRole(out role, out var roleStr, dontNeedStr: false))) return "";
					if (roleStr != null) return roleStr;
				}
				var a = s_roles; uint u = (uint)role;
				return (u < a.Length) ? a[u] : ((int)role).ToString();
			}
		}
		
		static readonly string[] s_roles = { "0", "TITLEBAR", "MENUBAR", "SCROLLBAR", "GRIP", "SOUND", "CURSOR", "CARET", "ALERT", "WINDOW", "CLIENT", "MENUPOPUP", "MENUITEM", "TOOLTIP", "APPLICATION", "DOCUMENT", "PANE", "CHART", "DIALOG", "BORDER", "GROUPING", "SEPARATOR", "TOOLBAR", "STATUSBAR", "TABLE", "COLUMNHEADER", "ROWHEADER", "COLUMN", "ROW", "CELL", "LINK", "HELPBALLOON", "CHARACTER", "LIST", "LISTITEM", "TREE", "TREEITEM", "PAGETAB", "PROPERTYPAGE", "INDICATOR", "IMAGE", "STATICTEXT", "TEXT", "BUTTON", "CHECKBOX", "RADIOBUTTON", "COMBOBOX", "DROPLIST", "PROGRESSBAR", "DIAL", "HOTKEYFIELD", "SLIDER", "SPINBUTTON", "DIAGRAM", "ANIMATION", "EQUATION", "BUTTONDROPDOWN", "BUTTONMENU", "BUTTONDROPDOWNGRID", "WHITESPACE", "PAGETABLIST", "CLOCK", "SPLITBUTTON", "IPADDRESS", "TREEBUTTON" };
		
		//Returns HRESULT.
		int _GetRole(out ERole roleInt, out string roleStr, bool dontNeedStr) {
			roleStr = null;
			Debug.Assert((ERole)_misc.roleByte is 0 or ERole.Custom);
			var hr = Cpp.Cpp_AccGetRole(this, out roleInt, out var b);
			if (hr == 0) {
				if (!b.Is0) {
					roleInt = ERole.Custom;
					if (dontNeedStr) b.Dispose(); else roleStr = b.ToStringAndDispose();
				}
				_misc.SetRole(roleInt);
			}
			return hr;
		}
		
		int _GetState(out EState state) {
			int hr = Cpp.Cpp_AccGetInt(this, 's', out int i);
			GC.KeepAlive(this);
			state = (EState)i;
			return hr;
		}
		
		/// <summary>
		/// Gets UI element state (flags).
		/// </summary>
		/// <returns>0 if failed. Supports <see cref="lastError"/>.</returns>
		/// <example>
		/// <code><![CDATA[
		/// if(e.State.Has(EState.INVISIBLE)) print.it("has state INVISIBLE");
		/// if(e.IsInvisible) print.it("invisible");
		/// ]]></code>
		/// </example>
		public EState State {
			get {
				ThrowIfDisposed_();
				_Hresult(_FuncId.state, _GetState(out var state));
				return state;
			}
		}
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>CHECKED</b>.</summary>
		public bool IsChecked => State.Has(EState.CHECKED);
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>CHECKED</b>, <c>null</c> if has state <b>MIXED</b>, else <c>false</c>. Use this function with 3-state checkboxes.</summary>
		public bool? IsChecked2 => (State & (EState.CHECKED | EState.MIXED)) switch { EState.CHECKED => true, 0 => false, _ => null };
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>UNAVAILABLE</b>.</summary>
		/// <remarks>Does not check whether this UI element is in a disabled parent/ancestor UI element.</remarks>
		public bool IsDisabled => State.Has(EState.DISABLED);
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>FOCUSED</b>.</summary>
		public bool IsFocused => State.Has(EState.FOCUSED);
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>INVISIBLE</b> and does not have state <b>OFFSCREEN</b>.</summary>
		/// <remarks>
		/// If the UI element has both <b>INVISIBLE</b> and <b>OFFSCREEN</b> states, it is either invisible or just offscreen, depending on application etc. Then this function works like <b>Find</b> and similar functions: for most UI elements returns <c>false</c> (is visible), but for UI elements that have these roles returns <c>true</c> (invisible): <b>WINDOW</b>, <b>DOCUMENT</b>, <b>PROPERTYPAGE</b>, <b>GROUPING</b>, <b>ALERT</b>, <b>MENUPOPUP</b>.
		/// Does not check whether this UI element is in an invisible parent/ancestor UI element.
		/// </remarks>
		public bool IsInvisible => IsInvisible_(State);
		
		internal bool IsInvisible_(EState state) {
			if (!state.Has(EState.INVISIBLE)) return false;
			if (!state.Has(EState.OFFSCREEN)) return true;
			switch (RoleInt) {
			case ERole.WINDOW:
			case ERole.DOCUMENT:
			case ERole.PROPERTYPAGE:
			case ERole.GROUPING:
			case ERole.ALERT:
			case ERole.MENUPOPUP:
				return true;
				//note: these roles must be the same as in _IsRoleToSkipIfInvisible in "acc find.cpp"
			}
			return false;
		}
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>OFFSCREEN</b>.</summary>
		public bool IsOffscreen => State.Has(EState.OFFSCREEN);
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>PROTECTED</b>.</summary>
		/// <remarks>This state is used for password fields.</remarks>
		public bool IsPassword => State.Has(EState.PROTECTED);
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>PRESSED</b>.</summary>
		public bool IsPressed => State.Has(EState.PRESSED);
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>READONLY</b>.</summary>
		public bool IsReadonly => State.Has(EState.READONLY);
		
		/// <summary>Calls <see cref="State"/> and returns <c>true</c> if has state <b>SELECTED</b>.</summary>
		public bool IsSelected => State.Has(EState.SELECTED);
		
		/// <summary>
		/// Converts <b>BSTR</b> to string and disposes the <b>BSTR</b>.
		/// If hr is not 0, returns <c>""</c> (never <c>null</c>).
		/// </summary>
		static string _BstrToString(int hr, BSTR b) {
			if (hr == 0) return b.ToStringAndDispose() ?? "";
			return "";
		}
		
		string _GetStringProp(char prop) {
			ThrowIfDisposed_();
			int hr = Cpp.Cpp_AccGetStringProp(this, prop, out var b);
			GC.KeepAlive(this);
			var s = _BstrToString(hr, b);
			_Hresult((_FuncId)prop, hr);
			return s;
		}
		
		static string _GetStringPropL(Cpp.Cpp_Acc a, char prop) {
			int hr = Cpp.Cpp_AccGetStringProp(a, prop, out var b);
			return _BstrToString(hr, b);
		}
		
		/// <summary>
		/// Gets name.
		/// </summary>
		/// <returns><c>""</c> if name is unavailable or if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// UI element name usually is its read-only text (eg button text, link text), or its adjacent read-only text (eg text label by this edit box). It usually does not change, therefore can be used to find or identify the UI element.
		/// </remarks>
		public string Name {
			get => _GetStringProp('n');
			
			//note: bug of standard toolbar buttons: fails to get name if all these are true:
			//	This process is 64-bit and the target process is 32-bit.
			//	Button Name property comes from its tooltip.
			//	Found not inproc, eg with flag NotInProc.
		}
		
		/// <summary>
		/// Gets <see cref="Name"/> of window/control <i>w</i>.
		/// Returns <c>null</c> if <i>w</i> invalid. Returns <c>""</c> if failed to get name.
		/// </summary>
		internal static string NameOfWindow_(wnd w) {
			if (!w.IsAlive) return null;
			var hr = Cpp.Cpp_AccFromWindow(1 | 2, w, 0, out _, out var b);
			return _BstrToString(hr, b);
			
			//speed: inproc ~10% faster. But first time slower, especially if process of different bitness.
		}
		
		/// <summary>
		/// Gets or sets value.
		/// </summary>
		/// <exception cref="AuException">Failed to set value.</exception>
		/// <remarks>
		/// Usually it is editable text or some other value that can be changed at run time, therefore in most cases it cannot be used to find or identify the UI element reliably.
		/// The <c>get</c> function returns <c>""</c> if this property is unavailable or if failed. Supports <see cref="lastError"/>.
		/// Most UI elements don't support <c>set</c>.
		/// </remarks>
		public string Value {
			get => _GetStringProp('v');
			set {
				ThrowIfDisposed_();
				AuException.ThrowIfHresultNot0(_InvokeL('v', value));
			}
		}
		
		/// <summary>
		/// Gets description.
		/// </summary>
		/// <returns><c>""</c> if this property is unavailable or if failed. Supports <see cref="lastError"/>.</returns>
		public string Description {
			get => _GetStringProp('d');
		}
		
		/// <summary>
		/// Gets help text.
		/// </summary>
		/// <returns><c>""</c> if this property is unavailable or if failed. Supports <see cref="lastError"/>.</returns>
		public string Help {
			get => _GetStringProp('h');
		}
		
		/// <summary>
		/// Gets the UI Automation <b>AutomationId</b> property.
		/// </summary>
		/// <returns><c>""</c> if this property is unavailable or if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Only UI elements found with flag <see cref="EFFlags.UIA"/> can have this property.
		/// </remarks>
		public string UiaId {
			get => _GetStringProp('u');
		}
		
		/// <summary>
		/// Gets the UI Automation <b>ClassName</b> property.
		/// </summary>
		/// <returns><c>""</c> if this property is unavailable or if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Only UI elements found with flag <see cref="EFFlags.UIA"/> can have this property.
		/// </remarks>
		public string UiaCN {
			get => _GetStringProp('U');
		}
		
		/// <summary>
		/// Gets keyboard shortcut.
		/// </summary>
		/// <returns><c>""</c> if this property is unavailable or if failed. Supports <see cref="lastError"/>.</returns>
		public string KeyboardShortcut {
			get => _GetStringProp('k');
		}
		
		/// <summary>
		/// Gets default action of <see cref="Invoke"/>.
		/// </summary>
		/// <returns><c>""</c> if this property is unavailable or if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// If this is a Java UI element, returns all actions that can be used with <see cref="JavaInvoke"/>, like <c>"action1, action2, action3"</c>, from which the first is considered default and is used by <see cref="Invoke"/>.
		/// Note: the string is supposed to be localized, ie depends on UI language; except Java.
		/// </remarks>
		public string DefaultAction {
			get => _GetStringProp('a');
		}
		
		/// <summary>
		/// Performs the UI element's default action (see <see cref="DefaultAction"/>). Usually it is "click", "press" or similar.
		/// </summary>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Fails if the UI element does not have a default action. Then you can use <see cref="MouseClick"/>, or try <see cref="PostClick"/>, <see cref="Check"/>, <see cref="SendKeys"/>, other functions.
		/// The action can take long time, for example show a dialog. This function normally does not wait. It allows the caller to automate the dialog. If it waits, try <see cref="JavaInvoke"/> or one of the above functions (<b>MouseClick</b> etc).
		/// </remarks>
		public void Invoke() {
			ThrowIfDisposed_();
			_Invoke();
		}
		
		/// <summary>
		/// Calls <b>_InvokeL</b>, with <b>ButtonPostClickWorkaround_</b> if need. Exception if failed.
		/// </summary>
		void _Invoke(char action = 'a', string param = null, string errMsg = null) {
			int hr;
			if (!MiscFlags.HasAny(EMiscFlags.UIA | EMiscFlags.Java) && RoleInt is ERole.BUTTON or ERole.SPLITBUTTON or ERole.CHECKBOX or ERole.RADIOBUTTON) {
				using var workaround = new mouse.ButtonPostClickWorkaround_(WndContainer);
				hr = _InvokeL(action, param);
			} else {
				hr = _InvokeL(action, param);
			}
			AuException.ThrowIfHresultNot0(hr, errMsg);
			//_MinimalSleep(); //don't need. It does not make more reliable.
		}
		
		/// <summary>
		/// Calls <c>EnableActivate(-1)</c> and <c>Cpp_AccAction</c>.
		/// </summary>
		/// <returns><b>HRESULT</b></returns>
		int _InvokeL(char action = 'a', string param = null) {
			//UIA bug: if window inactive, in some cases tries to activate, and waits ~10 s if failed.
			//	Eg ExpandCollapse pattern (always) and Invoke/Toggle patterns (buttons/checkboxes, not always).
			//	Non-UIA servers also could try to activate, although now I don't remember such cases.
			WndUtil.EnableActivate(-1); //usually quite fast, and often faster than WndContainer
			
			int hr = Cpp.Cpp_AccAction(this, action, param);
			GC.KeepAlive(this);
			return hr;
		}
		
		//void _MinimalSleep()
		//{
		//	Thread.Sleep(15);
		//	//if(0 == _iacc.GetWnd(out var w)) w.MinimalSleepIfOtherThread_(); //better don't call getwnd
		//}
		
		/// <summary>
		/// Performs one of actions supported by this Java UI element.
		/// </summary>
		/// <param name="action">
		/// Action name. See <see cref="DefaultAction"/>.
		/// If <c>null</c> (default), performs default action (like <see cref="Invoke"/>) or posts <c>Space</c> key message. More info in Remarks.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Read more about Java UI elements in <see cref="elm"/> topic.
		/// 
		/// Problem: if the action opens a dialog, <b>Invoke</b>/<b>JavaInvoke</b> do not return until the dialog is closed (or fail after some time). The caller then waits and cannot automate the dialog. Also then this process cannot exit until the dialog is closed. If the action parameter is <c>null</c> and the UI element is focusable, this function tries a workaround: it makes the UI element (button etc) focused and posts <c>Space</c> key message, which should press the button; then this function does not wait.
		/// </remarks>
		public void JavaInvoke(string action = null) {
			//problem: if the button click action opens a modal dialog, doAccessibleActions waits until closed.
			//	Waits 8 s and then returns true. Somehow in QM2 returns false.
			//	During that time any JAB calls (probably from another thread) are blocked and fail. Tried various combinations.
			//	Also then releaseJavaObject does not return until the dialog closed. It even does not allow our process to exit. In QM2 the same.
			//	Previously (with old Java version?) then whole JAB crashed. Now doesn't. Or crashes only in some conditions that I now cannot reproduce.
			
			ThrowIfDisposed_();
			
			if (action == null
				&& 0 == _GetState(out var state)
				&& (state & (EState.FOCUSABLE | EState.SELECTABLE)) == EState.FOCUSABLE //must be only focusable. If SELECTABLE, probably don't need this workaround.
				&& 0 == _GetWnd(out var w)
				&& 0 == Cpp.Cpp_AccSelect(this, ESelect.TAKEFOCUS)
				&& 0 == _GetState(out state) && state.Has(EState.FOCUSED) //avoid sending keys to another control
				) {
				GC.KeepAlive(this);
				w.Post(Api.WM_KEYDOWN, (byte)KKey.Space, 0);
				w.Post(Api.WM_KEYUP, (byte)KKey.Space, 0);
				//tested: works even if the window is inactive.
				w.MinimalSleepNoCheckThread_();
				return;
			}
			
			_Invoke('a', action);
			//_MinimalSleep(); //don't need. JAB doAccessibleActions is sync, which is bad.
		}
		
		/// <summary>
		/// Calls <see cref="Invoke"/> or <i>action</i> and waits until changes the web page (window name and page name).
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).
		/// Default 60 seconds.
		/// </param>
		/// <param name="action">If used, calls it instead of <see cref="Invoke"/>.</param>
		/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> &lt; 0; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <exception cref="AuException">Failed. For example, when this UI element is invalid, or its top-level window does not contain a web page.</exception>
		/// <exception cref="AuWndException">The window was closed while waiting.</exception>
		/// <exception cref="Exception">Exceptions thrown by <see cref="Invoke"/> or by the <i>action</i> function.</exception>
		/// <remarks>
		/// This function is used to click a link in a web page and wait until current web page is gone. It prevents a following "wait for UI element" function from finding a matching UI element in the old page, which would be bad.
		/// Does not wait until the new page is completely loaded. There is no reliable/universal way for it. Instead, after calling it you can call a "wait for UI element" function which waits for a known UI element that must be in the new page.
		/// This function cannot be used when the new page has the same title as current page. Then it waits until <i>timeout</i> time or forever. The same if the invoked action does not open a web page.
		/// </remarks>
		public bool WebInvoke(Seconds? timeout = null, Action<elm> action = null) {
			wnd w = WndTopLevel; if (w.Is0) throw new AuException("*get window");
			elm doc = w.Elm["web:"].Find(-1) ?? throw new AuException("*find web page");
			
			string wndName = w.NameTL_, docName = doc.Name; Debug.Assert(!wndName.NE() && !docName.NE());
			bool wndOK = false, docOK = false;
			elmFinder f = null;
			
			if (action == null) Invoke(); else action(this);
			
			//wait until window name and document name both are changed. They can change in any order.
			var seconds = timeout ?? new(60);
			seconds.Period ??= 25;
			var loop = new WaitLoop(seconds);
			while (loop.Sleep()) {
				w.ThrowIfInvalid();
				if (!wndOK) {
					var s = w.NameTL_;
					if (s == null) continue; //probably invalid, will throw in next loop
					wndOK = s != wndName;
				}
				if (wndOK && !docOK) {
					f ??= new elmFinder("web:") { ResultGetProperty = 'n' };
					if (!w.HasElm(f)) continue; //eg in Firefox for some time there is no DOCUMENT
					if (f.ResultProperty is not string s) continue;
					docOK = s != docName;
					//if(!docOK) print.it("doc is late");
				}
				if (wndOK && docOK) {
					w.ThrowIfInvalid();
					return true;
				}
			}
			return false;
		}
		
#if false
		//This function is finished and normally works well.
		//However web browsers not always fire the event. For some pages never, or only when not cached.
		//Also, some pages are like never finish loading (the browser waiting animation does not stop spinning). Or finish when the wanted UI element is there for long time, so why to wait. Or finish, then continue loading again...
		//Also, this function inevitably will stop working with some new web browser version with new bugs. Too unreliable.
		public bool InvokeAndWaitForWebPageLoaded(double secondsTimeout = 60, Action<elm> action = null, wnd w = default)
		{
			ThrowIfDisposed_();

			int timeout; bool throwTimeout = false;
			if(secondsTimeout == 0) timeout = Timeout.Infinite;
			else {
				if(secondsTimeout < 0) secondsTimeout = -secondsTimeout; else throwTimeout = true;
				timeout = checked((int)(secondsTimeout * 1000));
			}

			if(w.Is0) {
				w = WndContainer;
				if(w.Is0) throw new AuException("*get window");
			}

			var hookEvent = EEvent.IA2_DOCUMENT_LOAD_COMPLETE;
			int browser = w.ClassNameIs(Api.string_IES, "Mozilla*", "Chrome*");
			switch(browser) {
			case 0:
				wnd ies = w.Child(null, Api.string_IES); if(ies.Is0) break;
				w = ies; goto case 1;
			case 1: hookEvent = EEvent.OBJECT_CREATE; break;
			}

			int tid = w.ThreadId; if(tid == 0) w.ThrowUseNative();
			AutoResetEvent eventNotify = null;
			int debugIndex = 0;

			Api.WINEVENTPROC hook = (IntPtr hWinEventHook, EEvent ev, wnd hwnd, int idObject, int idChild, int idEventThread, int time) =>
			{
				if(eventNotify == null) { /*print.it("null 1");*/ return; }
				if(ev == EEvent.OBJECT_CREATE && hwnd != w) return; //note: in Chrome hwnd is Chrome_RenderWidgetHostHWND
				int di = ++debugIndex;
				using(var a = elm.fromEvent(hwnd, idObject, idChild)) {
					if(a == null) { /*Debug_.Print("elm.fromEvent null");*/ return; } //often IE, but these are not useful UI elements
					if(eventNotify == null) { /*print.it("null 2");*/ return; }
					if(ev == EEvent.IA2_DOCUMENT_LOAD_COMPLETE) { //Chrome, Firefox

						//filter out frame/iframe
						if(browser == 2) { //Firefox does not fire events for frame/iframe. But the Chrome code would work too.
						} else if(0 == a._iacc.get_accParent(out var a2)) { //bug in some Chrome versions: fails for main document
							using(a2) {
								if(eventNotify == null) { /*print.it("null 3");*/ return; }
								bool isFrame;
								var hr = a2.GetRole(0, out var role, out var roleStr); if(hr != 0) Debug_.Print((uint)hr);
								if(eventNotify == null) { /*print.it("null 4");*/ return; }
								if(hr != 0) isFrame = false;
								else if(roleStr != null) isFrame = roleStr.Ends("frame", true);
								else isFrame = !(role == ERole.WINDOW || role == ERole.CLIENT);
								//print.it(role, roleStr);
								if(isFrame) return;
								//browser    main        frame     iframe
								//Firefox    "browser"   "frame"   "iframe"
								//Chrome     WINDOW      "FRAME"   DOCUMENT
							}
						}
					} else { //IE (OBJECT_CREATE)
						if(a._elem != 0) return;
						if(0 != a._iacc.GetRole(0, out var role) || role != ERole.PANE) return;
						if(eventNotify == null) { /*print.it("null 3");*/ return; }

						//filter out frame/iframe
						if(a.IsInvisible) return;
						if(eventNotify == null) { /*print.it("null 4");*/ return; }
						using(var aCLIENT = _FromWindow(w, EObjid.CLIENT, noThrow: true)) {
							if(eventNotify == null) { /*print.it("null 5");*/ return; }
							if(aCLIENT != null) {
								var URL1 = a.Value; Debug.Assert(URL1.Length > 0); //print.it(URL1); //http:..., about:...
								aCLIENT.get_accName(0, out var URL2, 0); Debug.Assert(URL2.Length > 0);
								if(URL1 != URL2) return;
								if(eventNotify == null) { /*print.it("null 6");*/ return; }
							} else Debug_.Print("aCLIENT null");
						}
					}

					//print.it(di, ev, a);
					eventNotify.Set();
					eventNotify = null;
				}
			};

			var hh = Api.SetWinEventHook(hookEvent, hookEvent, default, hook, 0, tid, 0); if(hh == default) throw new AuException();
			try {
				eventNotify = new AutoResetEvent(false);
				if(action != null) action(this); else Invoke();
				if(eventNotify.WaitOne(timeout)) {
					//Thread.CurrentThread.Join(2000);
					return true;
				}
			}
			finally { Api.UnhookWinEvent(hh); eventNotify?.Dispose(); }
			GC.KeepAlive(hook);

			if(throwTimeout) throw new TimeoutException();
			return false;
		}
#endif
		
		/// <summary>
		/// Selects or deselects.
		/// </summary>
		/// <param name="how">Specifies whether to select, focus, add to selection etc. Can be two flags, for example <c>ESelect.TAKEFOCUS | ESelect.TAKESELECTION</c>. With flag <b>TAKEFOCUS</b> activates the window like <see cref="Focus(bool)"/>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <exception cref="AuWndException">Failed to activate the window (<see cref="wnd.Activate"/>) or focus the control (<see cref="wnd.Focus"/>).</exception>
		/// <remarks>
		/// Not all UI elements support it. Most UI elements support not all flags. It depends on <see cref="EState"/> <b>FOCUSABLE</b>, <b>SELECTABLE</b>, <b>MULTISELECTABLE</b>, <b>EXTSELECTABLE</b>, <b>DISABLED</b>.
		/// Many UI elements have bugs, especially with flag <b>TAKEFOCUS</b>. More bugs when the UI element has been found with flag <see cref="EFFlags.NotInProc"/>.
		/// </remarks>
		public void Select(ESelect how = ESelect.TAKESELECTION) {
			ThrowIfDisposed_();
			
			//Workaround for Windows controls bugs, part 1.
			wnd w = default, wTL = default; bool focusingControl = false;
			if (how.Has(ESelect.TAKEFOCUS)) {
				//Always activate/focus the window, because used by functions that then will send keys etc.
				//CONSIDER: option TAKEFOCUSNOACTIVATE to focus without activate/focus the window.
				w = WndContainer;
				//if(!w.IsEnabled(true)) throw new AuException("*set focus. Disabled"); //accSelect would not fail //rejected. In some cases the UI element may be focusable although window disabled, eg KTreeView.
				wTL = w.Window;
				wTL.Activate();
				if (focusingControl = (w != wTL))
					if (w.IsEnabled()) //see above. Would be exception if disabled.
						w.Focus();
				if (IsFocused) how &= ~ESelect.TAKEFOCUS;
				if (how == 0) return;
			} else {
				//same as with Invoke
				WndUtil.EnableActivate(-1);
			}
			
			for (int i = 0; i < 2; i++) {
				var hr = Cpp.Cpp_AccSelect(this, how);
				GC.KeepAlive(this);
				if (hr == 0) break;
				if (hr == 1) continue; //some UI elements return S_FALSE even if did what asked. Eg combobox (focuses the child Edit), slider. Or may need to retry, eg when trying to focus a listitem in a non-focused listbox.
				if (hr == Api.DISP_E_MEMBERNOTFOUND) throw new AuException("This UI element does not support this state");
				AuException.ThrowIfHresultNegative(hr);
			}
			
			if (!w.Is0) w.MinimalSleepIfOtherThread_(); //sleep only when focusing. Assume selection is sync. Also need for the bug, because the control may be activated a millisecond later.
			
			//Workaround for Windows controls bugs, part 2.
			if (focusingControl && w.IsActive) {
				//Debug_.Print("activated control");
				wTL.ActivateL();
			}
			
			//tested: IAccessible.accSelect(TAKEFOCUS):
			//	Most Windows controls have this bug: activates the control with SetForegroundWindow, which deactivates the top-level window.
			//		Especially if the control is already focused.
			//		If not already focused, fails if eg listbox item. But then works well with eg buttons.
			//		MSDN: If IAccessible::accSelect is called with the SELFLAG_TAKEFOCUS flag on a child UI element that has an HWND, the flag takes effect only if the UI element's parent has the focus.
			//		Tested, does not help: LockSetForegroundWindow, AttachThreadInput.
			//		Good news: works well if the UI element found inproc, ie without flag NotInproc.
			//			But then need to focus the control, else does not work.
			//			Use the same workaround. It focuses the control.
			//	Works well with web browsers, WinForms.
			//	With WPF initially almost does not work. After using a navigation key (Tab etc) starts to work well.
			//tested: UIA.IElement.SetFocus:
			//	In most cases works well with standard controls, all web browsers, WinForms.
			//	With WPF same as elm.
			//	Bug: If standard control is disabled, deactivates parent window and draws focus rectangle on the control.
		}
		
		/// <summary>
		/// Makes this UI element focused for keyboard input.
		/// </summary>
		/// <param name="andSelect">Add flag <b>TAKESELECTION</b>. Note: it is for selecting a list item, not for selecting text in a text box.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <exception cref="AuWndException">Failed to activate the window (<see cref="wnd.Activate"/>) or focus the control (<see cref="wnd.Focus"/>).</exception>
		/// <remarks>
		/// Calls <see cref="Select"/> with flag <b>TAKEFOCUS</b> and optionally <b>TAKESELECTION</b>.
		/// Not all UI elements support this action and not all work correctly. More info in <b>Select</b> documentation.
		/// </remarks>
		public void Focus(bool andSelect = false) {
			var how = ESelect.TAKEFOCUS;
			if (andSelect) how |= ESelect.TAKESELECTION;
			Select(how);
		}
		
		/// <summary>
		/// Gets selected direct child items.
		/// </summary>
		/// <returns>Empty array if there are no selected items of if failed. Supports <see cref="lastError"/>.</returns>
		public elm[] SelectedChildren {
			get {
				ThrowIfDisposed_();
				if (_elem != 0) { lastError.clear(); return Array.Empty<elm>(); }
				//return _iacc.get_accSelection();
				if (0 != _Hresult(_FuncId.selection, Cpp.Cpp_AccGetSelection(this, out var b)) || b.Is0) return Array.Empty<elm>();
				GC.KeepAlive(this);
				var p = (Cpp.Cpp_Acc*)b.Ptr; int n = b.Length / sizeof(Cpp.Cpp_Acc);
				var r = new elm[n];
				for (int i = 0; i < n; i++) r[i] = new elm(p[i]);
				b.Dispose();
				return r;
			}
		}
		
		/// <summary>
		/// Gets the number of direct child UI elements.
		/// </summary>
		public int ChildCount {
			get {
				ThrowIfDisposed_();
				if (_elem != 0) { lastError.clear(); return 0; }
				_Hresult(_FuncId.child_count, Cpp.Cpp_AccGetInt(this, 'c', out int cc));
				GC.KeepAlive(this);
				return cc;
			}
		}
		
		/// <summary>
		/// Gets multiple properties.
		/// </summary>
		/// <param name="props">
		/// String that specifies properties to get, for example <c>"nv"</c> for name and value.
		/// <br/>• <c>R</c> - <see cref="Role"/>.
		/// <br/>• <c>n</c> - <see cref="Name"/>.
		/// <br/>• <c>v</c> - <see cref="Value"/>.
		/// <br/>• <c>d</c> - <see cref="Description"/>.
		/// <br/>• <c>h</c> - <see cref="Help"/>.
		/// <br/>• <c>a</c> - <see cref="DefaultAction"/>.
		/// <br/>• <c>k</c> - <see cref="KeyboardShortcut"/>.
		/// <br/>• <c>u</c> - <see cref="UiaId"/>.
		/// <br/>• <c>U</c> - <see cref="UiaCN"/>.
		/// <br/>• <c>s</c> - <see cref="State"/>.
		/// <br/>• <c>r</c> - <see cref="GetRect(out RECT, bool)"/> with <i>raw</i> <c>true</c>.
		/// <br/>• <c>D</c> - <see cref="Rect"/> or <see cref="GetRect(out RECT, bool)"/> with <i>raw</i> <c>false</c>. Don't use with <c>r</c>.
		/// <br/>• <c>w</c> - <see cref="WndContainer"/>.
		/// <br/>• <c>o</c> - <see cref="Html"/> outer.
		/// <br/>• <c>i</c> - <see cref="Html"/> inner.
		/// <br/>• <c>@</c> - <see cref="HtmlAttributes"/>.
		/// </param>
		/// <param name="result">Receives results.</param>
		/// <returns><c>false</c> if failed, for example when the UI element's window is closed. Supports <see cref="lastError"/>.</returns>
		/// <exception cref="ArgumentException">Unknown property character.</exception>
		/// <remarks>
		/// The returned variable contains values of properties specified in <i>props</i>. When a property is empty or failed to get, the member variable is <c>""</c>, empty dictionary or default value of that type; never <c>null</c>.
		/// 
		/// Normally this function is faster than calling multiple property functions, because it makes single remote procedure call. But not if this UI element was found with flag <see cref="EFFlags.NotInProc"/> etc.
		/// </remarks>
		public bool GetProperties(string props, out EProperties result) {
			//CONSIDER: use cached role. Or not, because now can help to catch bugs where the cached role is incorrect.
			
			result = null;
			ThrowIfDisposed_();
			if (props.Length == 0) return true;
			int hr = Cpp.Cpp_AccGetProps(this, props, out var b);
			GC.KeepAlive(this);
			if (hr != 0) {
				if (hr == (int)Cpp.EError.InvalidParameter) throw new ArgumentException("Unknown property character.");
				lastError.code = hr;
				return false;
			}
			result = new();
			using (b) {
				var offsets = (int*)b.Ptr;
				for (int i = 0; i < props.Length; i++) {
					int offs = offsets[i], len = ((i == props.Length - 1) ? b.Length : offsets[i + 1]) - offs;
					var p = b.Ptr + offs;
					switch (props[i]) {
					case 'r' or 'D': result.Rect = len > 0 ? *(RECT*)p : default; break;
					case 's': result.State = len > 0 ? *(EState*)p : default; break;
					case 'w': result.WndContainer = len > 0 ? (wnd)(*(int*)p) : default; break;
					case '@': result.HtmlAttributes = AttributesToDictionary_(p, len); break;
					default:
						var s = (len == 0) ? "" : new string(p, 0, len);
						switch (props[i]) {
						case 'R': result.Role = s; break;
						case 'n': result.Name = s; break;
						case 'v': result.Value = s; break;
						case 'd': result.Description = s; break;
						case 'h': result.Help = s; break;
						case 'a': result.DefaultAction = s; break;
						case 'k': result.KeyboardShortcut = s; break;
						case 'u': result.UiaId = s; break;
						case 'U': result.UiaCN = s; break;
						case 'o': result.OuterHtml = s; break;
						case 'i': result.InnerHtml = s; break;
						}
						break;
					}
				}
			}
			return true;
		}
		
		internal static Dictionary<string, string> AttributesToDictionary_(char* p, int len) {
			var d = new Dictionary<string, string>();
			int ik = 0, iv = 0;
			for (int i = 0; i < len; i++) {
				var c = p[i];
				if (c == '\0' && iv > ik) {
					string sk = new string(p, ik, iv - ik - 1);
					string sv = new string(p, iv, i - iv);
					d[sk] = sv;
					ik = i + 1;
				} else if (c == '=' && iv <= ik) {
					iv = i + 1;
				}
			}
			//print.it(d);
			return d;
		}
		
		/// <summary>
		/// Gets an adjacent or related UI element - next, child, parent, etc.
		/// </summary>
		/// <returns><c>null</c> if not found.</returns>
		/// <param name="navig">
		/// String consisting of one or more navigation direction strings separated by space, like <c>"parent next child4 first"</c>.
		/// <br/>• <c>"next"</c> - next sibling UI element in the same parent UI element.
		/// <br/>• <c>"previous"</c> - previous sibling UI element in the same parent UI element.
		/// <br/>• <c>"first"</c> - first child UI element.
		/// <br/>• <c>"last"</c> - last child UI element.
		/// <br/>• <c>"parent"</c> - parent (container) UI element.
		/// <br/>• <c>"child"</c> - child UI element by 1-based index. Example: <c>"child3"</c> (3-th child). Negative index means from end, for example -1 is the last child.
		/// <br/>• <c>"#N"</c> - custom. More info in Remarks.
		/// <br/>• Some elements also support <c>"up"</c>, <c>"down"</c>, <c>"left"</c>, <c>"right"</c>.
		/// </param>
		/// <param name="waitS">Wait for the wanted UI element max this number of seconds. Default 0 (don't wait). If negative, waits forever.</param>
		/// <exception cref="ArgumentException">Invalid <i>navig</i> string.</exception>
		/// <remarks>
		/// Can be 2 letters, like <c>"pr"</c> for <c>"previous"</c>.
		/// A string like <c>"next3"</c> or <c>"next,3"</c> is the same as <c>"next next next"</c>. Except for <c>"child"</c>.
		/// Use string like <c>"#1000"</c> to specify a custom <i>navDir</i> value to pass to <msdn>IAccessible.accNavigate</msdn>.
		/// 
		/// For <c>"child"</c> the function calls API <msdn>AccessibleChildren</msdn>.
		/// For <c>"parent"</c> the function calls <msdn>IAccessible.get_accParent</msdn>. Few UI elements don't support. Some UI elements return a different parent than in the tree of UI elements.
		/// For others the function calls <msdn>IAccessible.accNavigate</msdn>. Not all UI elements support it. Some UI elements skip invisible siblings. Instead you can use <c>"parent childN"</c> or <c>"childN"</c>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// a = e.Navigate("parent next ch3");
		/// ]]></code>
		/// </example>
		public elm Navigate(string navig, double waitS = 0) {
			Not_.Null(navig);
			ThrowIfDisposed_();
			int hr; Cpp.Cpp_Acc ca;
			if (waitS == 0) {
				hr = Cpp.Cpp_AccNavigate(this, navig, out ca);
			} else {
				var loop = new WaitLoop(waitS > 0 ? -waitS : 0d);
				do hr = Cpp.Cpp_AccNavigate(this, navig, out ca);
				while (hr != 0 && hr != (int)Cpp.EError.InvalidParameter && loop.Sleep());
			}
			GC.KeepAlive(this);
			if (hr == (int)Cpp.EError.InvalidParameter) throw new ArgumentException("Invalid navig string.");
			lastError.code = hr;
			return hr == 0 ? new elm(ca) : null;
			
			//FUTURE: when fails, possibly this is disconnected etc. Retry find with same elmFinder.
		}
		
		/// <summary>
		/// Gets parent element. Same as <see cref="Navigate"/> with argument <c>"pa"</c>.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		public elm Parent => Navigate("pa");
		//info: Navigate("pa") is optimized in C++
		//Chrome bug: the parent element retrieved in this way has some incorrect properties.
		//	Eg Parent.WndContainer is the legacy control, whereas this.WndContainer is the top-level window.
		
		/// <summary>
		/// Gets HTML.
		/// </summary>
		/// <returns><c>""</c> if this is not a HTML element or if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="outer">If <c>true</c>, gets outer HTML (with tag and attributes), else inner HTML.</param>
		/// <remarks>
		/// Works with Chrome, Internet Explorer and apps that use their code (Edge, Opera, web browser controls...). This UI element must be found without flag <b>NotInProc</b>.
		/// If this is the root of web page (role <b>DOCUMENT</b> or <b>PANE</b>), gets web page body HTML.
		/// </remarks>
		public string Html(bool outer) {
			ThrowIfDisposed_();
			int hr = _Hresult(_FuncId.html, Cpp.Cpp_AccWeb(this, outer ? "'o" : "'i", out BSTR s));
			GC.KeepAlive(this);
			return _BstrToString(hr, s);
		}
		
		/// <summary>
		/// Gets a HTML attribute.
		/// </summary>
		/// <returns><c>""</c> if this is not a HTML element or does not have the specified attribute or failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="name">Attribute name, for example <c>"href"</c>, <c>"id"</c>, <c>"class"</c>. Full, case-sensitive.</param>
		/// <remarks>
		/// Works with Chrome, Internet Explorer and apps that use their code (Edge, Opera, web browser controls...). This UI element must be found without flag <b>NotInProc</b>.
		/// </remarks>
		/// <exception cref="ArgumentException"><i>name</i> is <c>null</c>/<c>""</c>/invalid.</exception>
		public string HtmlAttribute(string name) {
			ThrowIfDisposed_();
			if (name.NE() || name[0] == '\'') throw new ArgumentException("Invalid name.");
			int hr = _Hresult(_FuncId.html, Cpp.Cpp_AccWeb(this, name, out BSTR s));
			GC.KeepAlive(this);
			return _BstrToString(hr, s);
		}
		
		/// <summary>
		/// Gets all HTML attributes.
		/// </summary>
		/// <returns>Empty dictionary if this is not a HTML element or does not have attributes or failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Works with Chrome, Internet Explorer and apps that use their code (Edge, Opera, web browser controls...). This UI element must be found without flag <b>NotInProc</b>.
		/// </remarks>
		public Dictionary<string, string> HtmlAttributes() {
			ThrowIfDisposed_();
			int hr = Cpp.Cpp_AccWeb(this, "'a", out BSTR s);
			GC.KeepAlive(this);
			_Hresult(_FuncId.html, hr);
			if (hr != 0) return new();
			using (s) return AttributesToDictionary_(s.Ptr, s.Length);
		}
		
		/// <summary>
		/// Scrolls this UI element into view.
		/// </summary>
		/// <exception cref="AuException">Failed to scroll, or the UI element does not support scrolling.</exception>
		/// <remarks>
		/// This function works with these UI elements:
		/// - Web page elements in Chrome, Internet Explorer and apps that use their code (Edge, Opera, web browser controls...). To find UI element, use role prefix <c>"web:"</c>, <c>"firefox:"</c> or <c>"chrome:"</c>, and don't use flag <see cref="EFFlags.NotInProc"/>.
		/// - Standard treeview, listview and listbox controls.
		/// - Some other controls if found with flag <see cref="EFFlags.UIA"/>.
		/// 
		/// Some apps after scrolling update <see cref="Rect"/> with a delay. Some apps never update it for existing <b>elm</b> variables. This function does not wait.
		/// </remarks>
		public void ScrollTo() {
			ThrowIfDisposed_();
			AuException.ThrowIfHresultNot0(_ScrollTo(), "*scroll");
		}
		
		int _ScrollTo() {
			int hr = 1;
			if (MiscFlags.Has(EMiscFlags.UIA)) {
				hr = _InvokeL('s');
			} else if (Item == 0) {
				hr = Cpp.Cpp_AccWeb(this, "'s", out _);
				//tested: Chrome and Firefox don't support UI Automation scrolling (IUIAutomationScrollItemPattern).
			} else if (RoleInt is ERole.LISTITEM or ERole.TREEITEM) { //try messages of some standard controls
				var w = WndContainer;
				switch (w.CommonControlType) {
				case WControlType.Listview when RoleInt is ERole.LISTITEM:
					if (0 != w.Send(LVM_ENSUREVISIBLE, Item - 1, 1)) hr = 0;
					break;
				case WControlType.Listbox when RoleInt is ERole.LISTITEM:
					if (-1 != w.Send(LB_SETTOPINDEX, Item - 1)) hr = 0;
					break;
				case WControlType.Treeview when RoleInt is ERole.TREEITEM:
					if (osVersion.is32BitProcessAnd64BitOS && !w.Is32Bit) break; //cannot get 64-bit HTREEITEM
					nint hi = w.Send(TVM_MAPACCIDTOHTREEITEM, Item); if (hi == 0) break;
					w.Send(TVM_ENSUREVISIBLE, 0, hi);
					hr = 0; //the API returns nonzero only if actually scrolls, not if already visible
					break;
				}
			}
			GC.KeepAlive(this);
			return hr;
		}
		
		/// <summary>
		/// Waits for a user-defined state/condition of this UI element. For example enabled, checked, changed name.
		/// </summary>
		/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
		/// <param name="condition">Callback function (eg lambda). It is called repeatedly, until returns a value other than <c>default(T)</c>, for example <c>true</c>.</param>
		/// <returns>Returns the value returned by the callback function. On timeout returns <c>default(T)</c> if <i>timeout</i> is negative; else exception.</returns>
		/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
		/// <exception cref="AuWndException">Failed to get container window (<see cref="WndContainer"/>), or it was closed while waiting.</exception>
		public T WaitFor<T>(Seconds timeout, Func<elm, T> condition) {
			var w = WndContainer; //calls ThrowIfDisposed_
			var loop = new WaitLoop(timeout);
			for (; ; ) {
				w.ThrowIfInvalid();
				T r = condition(this);
				bool ok = !EqualityComparer<T>.Default.Equals(r, default);
				w.ThrowIfInvalid(); //eg when waiting for button enabled, if window closed while in callback, the DISABLED state may be removed
				if (ok) return r;
				if (!loop.Sleep()) return default;
			}
		}
		
		/// <summary>
		/// Moves the cursor (mouse pointer) to this UI element.
		/// </summary>
		/// <param name="x">X coordinate in the bounding rectangle of this UI element. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in the bounding rectangle of this UI element. Default - center.</param>
		/// <param name="scroll">If not 0, the function at first calls <see cref="ScrollTo"/>. If it succeeds, waits <i>scroll</i> number of milliseconds (let the target app update the UI element rectangle etc).</param>
		/// <exception cref="AuException">Failed to get UI element rectangle in container window (<see cref="elm.WndContainer"/>).</exception>
		/// <exception cref="Exception">Exceptions of <see cref="mouse.move(wnd, Coord, Coord, bool)"/>.</exception>
		/// <remarks>
		/// Calls <see cref="mouse.move(wnd, Coord, Coord, bool)"/>. To get rectangle in window, uses <see cref="GetRect(out RECT, wnd, bool)"/> with <i>intersect</i> <c>true</c>.
		/// </remarks>
		public void MouseMove(Coord x = default, Coord y = default, int scroll = 0)
			=> _ElmMouseAction(false, x, y, default, scroll);
		
		/// <summary>
		/// Clicks this UI element.
		/// </summary>
		/// <param name="x">X coordinate in the bounding rectangle of this UI element. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in the bounding rectangle of this UI element. Default - center.</param>
		/// <param name="button">Which button and how to use it.</param>
		/// <param name="scroll">If not 0, the function at first calls <see cref="ScrollTo"/>. If it succeeds, waits <i>scroll</i> number of milliseconds (let the target app update the UI element rectangle etc). Valid values are 0-5000. Tip: if does not scroll, try to find the UI element with flag <b>UIA</b>.</param>
		/// <exception cref="AuException">Failed to get UI element rectangle in container window (<see cref="elm.WndContainer"/>).</exception>
		/// <exception cref="Exception">Exceptions of <see cref="mouse.clickEx(MButton, wnd, Coord, Coord, bool)"/>.</exception>
		/// <remarks>
		/// Calls <see cref="mouse.clickEx(MButton, wnd, Coord, Coord, bool)"/>. To get rectangle in window, uses <see cref="GetRect(out RECT, wnd, bool)"/> with <i>intersect</i> <c>true</c>.
		/// </remarks>
		public MRelease MouseClick(Coord x = default, Coord y = default, MButton button = MButton.Left, int scroll = 0) {
			_ElmMouseAction(true, x, y, button, scroll);
			return button;
		}
		
		/// <summary>
		/// Double-clicks this UI element.
		/// </summary>
		/// <inheritdoc cref="MouseClick(Coord, Coord, MButton, int)"/>
		public void MouseClickD(Coord x = default, Coord y = default, int scroll = 0)
			=> MouseClick(x, y, MButton.DoubleClick, scroll);
		
		/// <summary>
		/// Right-clicks this UI element.
		/// </summary>
		/// <inheritdoc cref="MouseClick(Coord, Coord, MButton, int)"/>
		public void MouseClickR(Coord x = default, Coord y = default, int scroll = 0)
			=> MouseClick(x, y, MButton.Right, scroll);
		
		void _ElmMouseAction(bool click, Coord x, Coord y, MButton button, int scroll) {
			var (w, r) = _GetWndAndRectForClick(scroll);
			var p = Coord.NormalizeInRect(x, y, r, centerIfEmpty: true);
			//if (!w.Is0) {
			if (button == 0) mouse.move(w, p.x, p.y);
			else mouse.clickEx(button, w, p.x, p.y);
			//} else { //no. Unsafe, can click in another window.
			//	if (button == 0) mouse.move(p);
			//	else mouse.clickEx(button, p);
			//}
		}
		
		(wnd w, RECT r) _GetWndAndRectForClick(int scroll) {
			if (scroll != 0) {
				if ((uint)scroll > 5000) throw new ArgumentException("Valid values 0-5000", nameof(scroll));
				if (0 == _ScrollTo()) wait.ms(scroll);
			}
			
			if (!GetRect(out var r)) throw new AuException(0, "*get UI element rectangle");
			if (r.NoArea) throw new AuException(IsOffscreen ? "The UI element is offscreen. Try scroll." : "The UI element rectangle is empty");
			//var w = WndContainer; //need window for mouse functions, else could click another window etc
			var w = WndTopLevel; //direct parent control may be zero-size etc, eg in Win11 Paint when found with UIA
			bool retry = false; var r0 = r;
			g1:
			if (!w.GetRect(out var rw)) throw new AuException(0, "*get container window");
			if (!r.Intersect(rw)) {
				if (!retry && scroll != 0) { //workaround for: WndContainer of a popup item may be the popup's owner. Eg WPF combobox.
					w = w.Window.Get.EnabledOwned();
					if (retry = !w.Is0) { r = r0; goto g1; }
				}
				throw new AuException("The UI element rectangle is not in the container window." + (scroll != 0 ? null : " Try scroll."));
			}
			if (!w.MapScreenToClient(ref r)) throw new AuException(0);
			return (w, r);
		}
		
		//rejected: automatically scroll if need.
		//	Impossible to reliably detect whether need to scroll.
		//	This was an attempt, but it does not work well. And can't click non-client elements.
		//(wnd w, RECT r) _GetWndAndRectForClick() {
		//	//var w = WndContainer; //with window the mouse functions are more reliable, eg will not click another window
		//	var w = WndTopLevel; //direct parent control may be zero-size etc, eg in Win11 Paint when found with UIA
		//	if (w.Is0) throw new AuException(0, "*get container window");
		//	RECT r = _GetRect(), rr = r;
		//	print.it(r);
		//	bool retry = false;
		//	gRetry:
		//	bool bad = r.NoArea;
		//	if (!bad) {
		//		bad = (!retry && IsOffscreen) || !r.Intersect(_GetContainerClientRect(w));
		//	}
		//	print.it(bad);
		//	if (bad) {
		//		if (!retry) {
		//			if (0 == _ScrollTo())
		//				if (wait.until(-2, () => (r = _GetRect()) != rr)) {
		//					30.ms(); retry = true; goto gRetry;
		//				}
		//		}
		//		throw new AuException(0, "The UI element rectangle is " + (rr.NoArea ? "empty." : "offscreen."));
		//	}
		//	return (w, r);
		
		//	RECT _GetRect() => GetRect(out var r, w) ? r : throw new AuException(0, "*get UI element rectangle");
		
		//	//todo: now cannot click in nonclient area.
		
		//	//never mind: should intersect with all ancestor elements and windows.
		//	//	Now no OFFSCREEN state if partially clipped by an ancestor rect.
		//	//	Slow and unreliable, because visible children can be not in parent rect. Eg pagetab or treeitem.
		
		//	//problem: Firefox never updates Rect after ScrollTo. Need to find again.
		//	//	Chrome updates after several ms. Chrome does not update OFFSCREEN (or with a delay?).
		
		//	//problem: some fake container windows may be zero-size etc, and the element is drawn on another window.
		//	//	shoulddo: test more.
		//}
		
		/// <summary>
		/// Posts mouse-click messages to the container window, using coordinates in this UI element.
		/// </summary>
		/// <param name="x">X coordinate in the bounding rectangle of this UI element. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y coordinate in the bounding rectangle of this UI element. Default - center.</param>
		/// <param name="button">Can specify the left (default), right or middle button. Also flag for double-click, press or release.</param>
		/// <param name="scroll">If not 0, the function calls <see cref="ScrollTo"/>. If it succeeds, waits <i>scroll</i> number of milliseconds (let the target app update the UI element rectangle etc). Valid values are 0-5000. Tip: if does not scroll, try to find the UI element with flag <b>UIA</b>.</param>
		/// <exception cref="AuException">
		/// - Failed to get rectangle or container window.
		/// - The element is invisible/offscreen.
		/// </exception>
		/// <exception cref="ArgumentException">Unsupported button specified.</exception>
		/// <remarks>
		/// Does not move the mouse.
		/// Does not wait until the target application finishes processing the message.
		/// Works not with all elements.
		/// Try this function when <see cref="Invoke"/> does not work and you don't want to use <c>MouseClick</c>.
		/// </remarks>
		public void PostClick(Coord x = default, Coord y = default, MButton button = MButton.Left, int scroll = 0) {
			var (w, r) = _GetWndAndRectForClick(scroll);
			
			mouse.PostClick_(w, r, x, y, button);
		}
		
		/// <summary>
		/// Posts mouse-double-click messages to the container window, using coordinates in this UI element.
		/// </summary>
		/// <exception cref="AuException">
		/// - Failed to get rectangle or container window.
		/// - The element is invisible/offscreen.
		/// </exception>
		/// <inheritdoc cref="PostClick(Coord, Coord, MButton, int)" path="//param|//remarks"/>
		public void PostClickD(Coord x = default, Coord y = default) => PostClick(x, y, MButton.DoubleClick);
		
		/// <summary>
		/// Posts mouse-right-click messages to the container window, using coordinates in this UI element.
		/// </summary>
		/// <inheritdoc cref="PostClickD(Coord, Coord)"/>
		public void PostClickR(Coord x = default, Coord y = default) => PostClick(x, y, MButton.Right);
		
		/// <summary>
		/// Makes this UI element focused (<see cref="Focus"/>) and calls <see cref="keys.send"/>.
		/// </summary>
		/// <exception cref="Exception">Exceptions of <see cref="Focus"/> and <see cref="keys.send"/>.</exception>
		/// <inheritdoc cref="keys.send" path="/param"/>
		public void SendKeys([ParamString(PSFormat.Keys)] params KKeysEtc[] keysEtc) {
			bool andSelect = RoleInt is ERole.TREEITEM or ERole.LISTITEM;
			Focus(andSelect);
			keys.send(keysEtc);
		}
		
		//rejected: too simple and limited. No x y, scroll, button.
		///// <summary>
		///// Clicks this UI element and calls <see cref="keys.send"/>.
		///// </summary>
		///// <param name="doubleClick">If <c>true</c>, calls <see cref="MouseClickD"/>, else <see cref="MouseClick"/>.</param>
		///// <exception cref="Exception">Exceptions of <see cref="MouseClick"/> and <see cref="keys.send"/>.</exception>
		///// <inheritdoc cref="keys.send" path="/param"/>
		//public void SendKeys(bool doubleClick, [ParamString(PSFormat.keys)] params KKeysEtc[] keysEtc) {
		//	if (doubleClick) MouseClickD(); else MouseClick();
		//	keys.send(keysEtc);
		//}
		
		//rejected. Use SendKeys, it allows to specify keys to replace (Ctrl+A), append (Ctrl+End), etc.
		//public void SendText(string text, bool replace, [more options?]) {
		//	Focus();
		//	...
		//	keys.sendt(text);
		//}
		
		bool _CheckNeedToggle(bool check) {
			ThrowIfDisposed_();
			var state = State;
			//if (state.Has(EState.DISABLED)) throw new AuException("Disabled."); //can do more bad than good, eg if DISABLED state is when not actually disabled
			bool isChecked = state.Has(EState.CHECKED);
			if (!isChecked) isChecked = state.Has(EState.PRESSED) && !MiscFlags.HasAny(EMiscFlags.UIA | EMiscFlags.Java) && RoleInt == ERole.BUTTON /*&& WndContainer.ClassNameIs("Chrome*")*/; //eg the toggle buttons in Chrome settings have state PRESSED instead of CHECKED
			return isChecked != check;
		}
		
		/// <summary>
		/// Checks or unchecks this checkbox or toggle-button, or selects this radio button. Uses <see cref="Invoke"/> or <see cref="SendKeys"/>.
		/// </summary>
		/// <param name="check"><c>true</c> to check, <c>false</c> to uncheck.</param>
		/// <param name="keys">Keys for <see cref="SendKeys"/>. If <c>""</c>, uses <c>"Space"</c>. If <c>null</c> (default), uses <see cref="Invoke"/>.</param>
		/// <exception cref="Exception">Exceptions of <see cref="Invoke"/> or <see cref="SendKeys"/>.</exception>
		/// <remarks>
		/// Does nothing if the UI element already has the requested checked/unchecked state. Else tries to change the state and does not verify whether it actually worked.
		/// 
		/// Does not work with 3-state checkboxes and with elements that never have <b>CHECKED</b> state.
		/// </remarks>
		public void Check(bool check = true, [ParamString(PSFormat.Keys)] string keys = null) {
			if (!_CheckNeedToggle(check)) return;
			if (keys != null) {
				SendKeys(keys.Length == 0 ? "Space" : keys);
			} else {
				_Invoke(MiscFlags.Has(EMiscFlags.UIA) ? 'c' : 'a');
			}
		}
		
		/// <summary>
		/// Checks or unchecks this checkbox or toggle-button, or selects this radio button. To check/uncheck calls callback function.
		/// </summary>
		/// <param name="action">Callback function that should check or uncheck this UI element. Its parameter is this variable.</param>
		/// <exception cref="Exception">Exceptions of the callback function.</exception>
		/// <inheritdoc cref="Check(bool, string)"/>
		public void Check(bool check, Action<elm> action) {
			if (!_CheckNeedToggle(check)) return;
			action(this);
		}
		
		//rejected: Check and Expand overloads for mouse.
		//	Script code like 'e.Check(true, false) looks not good.
		//	Better 'e.Check(true, e => e.MouseClick())' or 'e.Check(true, e => e.PostClick())'.
		
		/// <summary>
		/// Expands or collapse this expandable UI element (tree item, combo box, expander, dropdown button).
		/// </summary>
		/// <param name="expand"><c>true</c> to expand, <c>false</c> to collapse.</param>
		/// <param name="keys">If not <c>null</c>, makes this element focused and presses these keys. See <see cref="keys.send"/>. If <c>""</c>, uses keys commonly used for that UI element type, for example <c>Right</c>/<c>Left</c> for <b>TREEITEM</b>, <c>Alt+Down</c> for <b>COMBOBOX</b>. If <c>null</c>, uses <see cref="Invoke"/> or similar functions, which often are available only if the element was found with flag <b>UIA</b>; if unavailable or fails, works like with <i>keys</i> <c>""</c>.</param>
		/// <param name="waitS">If not 0, waits for new expanded/collapsed state max this number of seconds; on timeout throws exception, unless negative.</param>
		/// <param name="ignoreState">Ignore initial <b>EXPANDED</b>/<b>COLLAPSED</b> state and always perform the expand/collapse action. Can be useful when <see cref="State"/> <b>EXPANDED</b>/<b>COLLAPSED</b> is incorrect. To ignore final state, use negative <i>waitS</i> instead, for example -0.001.</param>
		/// <exception cref="Exception">Exceptions of <see cref="SendKeys"/>.</exception>
		/// <exception cref="TimeoutException">The state didn't change in <i>waitS</i> seconds (if &gt; 0).</exception>
		/// <remarks>
		/// Does nothing if the UI element already has the requested expanded/collapsed state.
		/// 
		/// Works with UI elements that have <see cref="State"/> <b>EXPANDED</b> when expanded and <b>COLLAPSED</b> when collapsed. Also with UI elements that have state <b>CHECKED</b> or <b>PRESSED</b> when expanded and don't have this state when collapsed.
		/// </remarks>
		public void Expand(bool expand = true, [ParamString(PSFormat.Keys)] string keys = null, double waitS = 1, bool ignoreState = false) {
			_Expand(expand, keys, null, waitS, ignoreState);
		}
		
		/// <param name="action">Callback function that should expand or collapse this UI element. Its parameter is this variable.</param>
		/// <exception cref="Exception">Exceptions of the callback function.</exception>
		/// <inheritdoc cref="Expand(bool, string, double, bool)"/>
		public void Expand(bool expand, Action<elm> action, double waitS = 1, bool ignoreState = false) {
			_Expand(expand, null, action, waitS, ignoreState);
		}
		
		void _Expand(bool expand, string keys, Action<elm> action, double waitS, bool ignoreState) {
			ThrowIfDisposed_();
			
			bool _NeedToggle(bool expand) {
				var state = State;
				//print.it(RoleInt, Role, state);
				//note: ignore DISABLED state. Some non-disabled elements have it. Also probably would need to get state of parent TREEVIEW.
				bool isExpanded = !state.Has(EState.COLLAPSED) && state.HasAny(EState.EXPANDED | EState.CHECKED | EState.PRESSED);
				return isExpanded != expand;
			}
			if (!ignoreState) if (!_NeedToggle(expand)) return;
			
			int how = action != null ? 2 : keys != null ? 1 : 0;
			if (how == 0) {
				how = 1;
				if (MiscFlags.Has(EMiscFlags.UIA)) {
					if (0 == _InvokeL(expand ? 'E' : 'e')) how = 0;
				} else if (_ClassicTreeview()) {
					how = 0;
				} else {
					var da = DefaultAction;
					if (!da.NE()) {
						if (MiscFlags.Has(EMiscFlags.Java)) {
							//usually treeitem's default action is "toggleexpand", and it works. Other ways (mouse, focus+keys) are unreliable.
							//combobox action is "togglePopup", and requires active window. Does not wait until popup closed (good).
							if (da != "toggleexpand") WndTopLevel.Activate();
							if (0 == _InvokeL('a', da)) how = 0;
						} else {
							if (0 == _InvokeL()) how = 0;
							//never mind: not all actions Expand/Collapse, even if TREEITEM. Eg in Thunderbird.
							//	Could use keys etc if state didn't change eg in 0.5 s, but it can make less reliable.
							//	Let users choose another overload.
						}
					} else if (RoleInt == ERole.COMBOBOX) { //classic combobox?
						if (0 == _GetWnd(out wnd w) && w.CommonControlType == WControlType.Combobox) {
							w.SendNotify(0x014F, expand ? 1 : 0); //CB_SHOWDROPDOWN
							how = 0;
						}
					}
				}
			}
			
			if (how == 1) {
				Focus(andSelect: RoleInt is ERole.TREEITEM or ERole.LISTITEM or ERole.Custom); //exception if fails
				if (keys.NE()) {
					var role = MiscFlags.Has(EMiscFlags.Java) && Role == "combo box" ? ERole.COMBOBOX : RoleInt;
					keys = role switch {
						ERole.TREEITEM or ERole.LISTITEM or ERole.Custom => expand ? "Right" : "Left", //LISTITEM for treeviews made from listviews (not tested); Custom because we prefer treeviews
						ERole.COMBOBOX or ERole.DROPLIST => expand ? "Alt+Down" : "Esc", //DROPLIST used by the classic date/time picker, but does not work because state always 0
						ERole.BUTTON or ERole.CHECKBOX => "Space", //eg expander
						ERole.BUTTONDROPDOWN or ERole.BUTTONDROPDOWNGRID or ERole.BUTTONMENU => expand ? "Space" : "Esc",
						_ => expand ? "Down" : "Esc", //eg classic toolbar's SPLITBUTTON's dropdown part (MENUITEM); also classic SPLITBUTTON
					};
				}
				Au.keys.send(keys);
			} else if (how == 2) {
				action(this);
			}
			
			if (waitS != 0) {
				//10.ms();
				wait.until(waitS, () => !_NeedToggle(expand));
			}
			
			bool _ClassicTreeview() {
				if (Item == 0 || RoleInt != ERole.TREEITEM) return false;
				if (0 != _GetWnd(out wnd w) || w.CommonControlType != WControlType.Treeview) return false;
				if (osVersion.is32BitProcessAnd64BitOS && !w.Is32Bit) return false; //cannot get 64-bit HTREEITEM
				nint hi = w.Send(TVM_MAPACCIDTOHTREEITEM, Item); if (hi == 0) return false;
				_Expand_ClassicTreeview(w, hi, expand);
				return true;
			}
		}
		
		static void _Expand_ClassicTreeview(wnd w, nint hi, bool expand) {
#if false //like MSAA Invoke. Bad: no TVN_ITEMEXPANDED etc; eg does not change folder icon open/closed.
			w.Send(0x1102, expand ? 2 : 1, hi); //TVM_EXPAND(TVE_EXPAND:TVE_COLLAPSE)
			if (expand) w.Send(0x1114, 0, hi); //TVM_ENSUREVISIBLE
#else //like UIA expand/collapse. Bad: dirty; selects, which may do more than expand/collapse; for TVSI_NOSINGLEEXPAND need manifest.
			for (int i = 0; i < 5; i++) { //eg in old VS first time returns 0, although works. UIA sends 2 times.
				if (0 != w.Send(TVM_SELECTITEM, TVGN_CARET | TVSI_NOSINGLEEXPAND, hi)) break;
				Debug_.PrintIf(i > 0, "elm.Expand");
				//if (i > 0) wait.ms(i * 10);
			}
			int k = (int)(expand ? KKey.Right : KKey.Left);
			w.SendNotify(Api.WM_KEYDOWN, k);
			w.SendNotify(Api.WM_KEYUP, k);
			10.ms();
			//UIA posts, but then eg in old VS does not work if the control wasn't focused. Maybe that is why UIA always sets real focus.
			//CONSIDER: wnd.PostKey(), wnd.PostText().
#endif
		}
		
		/// <summary>
		/// Expands multiple treeview control items using a path string.
		/// </summary>
		/// <param name="path">
		/// String or array consisting of names (<see cref="Name"/>) of <b>TREEITEM</b> elements, like <c>"One|Two|Three"</c> or <c>["One", "Two", "Three"]</c>.
		/// Name string format: [wildcard expression](xref:wildcard_expression).
		/// </param>
		/// <param name="keys"><c>null</c> or keys to use to expand each element specified in <i>path</i>. See <see cref="Expand(bool, string, double, bool)"/>.</param>
		/// <param name="waitS">If not 0, after expanding each element waits for expanded state max this number of seconds; on timeout throws exception, unless negative. Also waits for each element this number of seconds; always exception if not found.</param>
		/// <param name="notLast">Find but don't expand the last element specified in <i>path</i>. For example if it's not a folder, and therefore can't expand it, but you need to find it (this function returns it).</param>
		/// <returns><b>elm</b> of the last element specified in <i>path</i>.</returns>
		/// <exception cref="ArgumentException"><i>path</i> contains an invalid wildcard expression (<c>"**options "</c> or regular expression).</exception>
		/// <exception cref="NotFoundException">Failed to find an element specified in <i>path</i>.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <exception cref="TimeoutException">The state didn't change in <i>waitS</i> seconds (if &gt; 0).</exception>
		/// <exception cref="NotSupportedException">The treeview control type is not supported when this is a 32-bit process running on 64-bit OS (unlikely).</exception>
		/// <exception cref="Exception">Exceptions of <see cref="SendKeys"/>.</exception>
		/// <remarks>
		/// This element can be a <b>TREE</b> or <b>TREEITEM</b>. If it is a collapsed <b>TREEITEM</b>, expands it. Then finds and expands elements specified in <i>path</i>.
		/// 
		/// Does not work if all <b>TREEITEM</b> elements in the <b>TREE</b> control are its direct children, unless it's the standard Windows treeview control.
		/// </remarks>
		public elm Expand(Strings path, [ParamString(PSFormat.Keys)] string keys = null, double waitS = 3, bool notLast = false) {
			return _ExpandPath(path, keys, null, waitS, notLast);
		}
		
		/// <param name="action">Callback function that should expand UI elements.</param>
		/// <exception cref="Exception">Exceptions of the callback function.</exception>
		/// <remarks></remarks>
		/// <inheritdoc cref="Expand(Au.Types.Strings, string, double, bool)"/>
		public elm Expand(Strings path, Action<elm> action, double waitS = 3, bool notLast = false) {
			return _ExpandPath(path, null, action, waitS, notLast);
		}
		
		elm _ExpandPath(Strings path, string keys, Action<elm> action, double waitS, bool notLast) {
			ThrowIfDisposed_();
			var a = path.ToArray();
			var e = this;
			//for classic treeview controls need special code if not UIA, because the MSAA tree is flat
			if (!MiscFlags.HasAny(EMiscFlags.UIA | EMiscFlags.Java) && 0 == _GetWnd(out wnd w) && w.CommonControlType == WControlType.Treeview) {
				if (osVersion.is32BitProcessAnd64BitOS && !w.Is32Bit) throw new NotSupportedException("32-bit process."); //cannot get 64-bit HTREEITEM
				
				nint hi = 0;
				if (Item > 0) {
					hi = w.Send(TVM_MAPACCIDTOHTREEITEM, Item); if (hi == 0) throw new AuException();
					_ExpandIfNeed(this, hi);
				}
				
				for (int i = 0; i < a.Length; i++) {
					var name = a[i];
					bool ok = waitS == 0 ? _Find(name) : wait.until(-Math.Abs(waitS), () => _Find(name));
					if (!ok) throw new NotFoundException("Not found elm expand path part: " + name);
					if (notLast && i == a.Length - 1) break;
					_ExpandIfNeed(e, hi);
				}
				
				bool _Find(wildex name) {
					elm temp = null;
					for (nint h = w.Send(TVM_GETNEXTITEM, hi == 0 ? TVGN_ROOT : TVGN_CHILD, hi); h != 0; h = w.Send(TVM_GETNEXTITEM, TVGN_NEXT, h)) {
						int item = (int)w.Send(TVM_MAPHTREEITEMTOACCID, h); if (item == 0) throw new AuException();
						var acc = new Cpp.Cpp_Acc(_iacc, item) { misc = _misc };
						if (Item == 0) { acc.misc.level++; acc.misc.roleByte = (byte)ERole.TREEITEM; }
						var s = _GetStringPropL(acc, 'n'); //the slowest part of this 'for'. FUTURE: if somewhere too slow, could make inproc.
						GC.KeepAlive(this);
						if (name.Match(s)) {
							if (temp == null) temp = new(acc, addRef: true); else temp.Item = item;
							e = temp;
							hi = h;
							return true;
						}
					}
					return false;
				}
				
				void _ExpandIfNeed(elm e, nint hi) {
					if (_IsExpanded(w, hi)) return;
					
					if (keys != null) e.SendKeys(keys.Length > 0 ? keys : "Right");
					else if (action != null) action(e);
					else _Expand_ClassicTreeview(w, hi, true);
					//p1.Next();
					
					if (waitS != 0) wait.until(waitS, () => _IsExpanded(w, hi));
					else _IsExpanded(w, hi); //usually waits
				}
				
				static bool _IsExpanded(wnd w, nint hi) => 0 != ((int)w.Send(TVM_GETITEMSTATE, hi, TVIS_EXPANDED) & TVIS_EXPANDED);
			} else {
				if (State.Has(EState.COLLAPSED))
					_Expand(true, keys, action, waitS, ignoreState: true);
				
				foreach (var name in a) {
					int level = e.Level;
					e = e.Elm[null, name, "level=0", also: o => o.State.HasAny(EState.EXPANDED | EState.COLLAPSED)].Find(-(Math.Abs(waitS) + .01));
					if (e == null) throw new NotFoundException("Not found elm expand path part: " + name);
					e.Level = level + 1;
					e._Expand(true, keys, action, waitS, ignoreState: false);
				}
			}
			return e;
		}
		
		/// <summary>
		/// Finds and selects an item in the drop-down list of this combo box or drop-down button.
		/// </summary>
		/// <param name="item">
		/// Item name (<see cref="Name"/>).
		/// String format: [wildcard expression](xref:wildcard_expression).
		/// </param>
		/// <param name="how">
		/// Try this parameter if the function fails to select the item etc.
		/// 
		/// <para>
		/// In the string can be used these characters to specify how to select the item and close the drop-down list:
		/// <br/>• <c>i</c> - call <see cref="Invoke"/>.
		/// <br/>• <c>s</c> - call <see cref="Select"/>.
		/// <br/>• <c>c</c> - close the list with <see cref="Expand"/>.
		/// <br/>• <c>m</c> - call <see cref="MouseClick"/>; often can't be used because fails to get correct rectangle or to scroll.
		/// <br/>• <c>k</c> - call <see cref="Focus"/> and <see cref="keys.send"/> (<c>Home</c>, <c>Down</c>, <c>Enter</c>).
		/// <br/>• space - nothing.
		/// </para>
		/// 
		/// If the string is <c>null</c> (default) or <c>""</c> or does not contain these characters, the function tries to detect and use what usually works for this UI element type, but it's impossible to detect always.
		/// 
		/// Usually need just a single character (string like <c>"i"</c> or <c>"m"</c>). If there are more characters, the functions are called in the specified order.
		/// 
		/// The string also can contain sleep times. For example <c>"300m"</c> will wait 300 ms and click; the first sleep will be between expanding and finding.
		/// 
		/// If the string starts with <c>"~"</c>, the function does not expand the drop-down list (it should be already expanded).
		/// 
		/// If the string isn't <c>null</c>/empty but does not contain characters <c>iscmk</c> (for example is <c>" "</c> or <c>"~ "</c> or <c>"200 "</c>), the function does not select/close. For example these codes do the same: <c>e.ComboSelect("Red", "i");</c> and <c>e.ComboSelect("Red", " ").Invoke();</c>.
		/// </param>
		/// <param name="waitS">Seconds to wait for expanded state (if not 0) and for the item. Can be negative to avoid timeout exceptions.</param>
		/// <returns>The item.</returns>
		/// <exception cref="ArgumentException">Error in <i>item</i> string (wildex options or regex) or <i>how</i>.</exception>
		/// <exception cref="NotFoundException">Item not found.</exception>
		/// <exception cref="Exception">Exceptions of used functions.</exception>
		/// <remarks>
		/// The function at first calls <see cref="Expand(bool, string, double, bool)"/> to show the drop-down list, unless <i>how</i> starts with <c>"~"</c>. Then finds the item by name, selects it and closes the drop-down list.
		/// </remarks>
		public elm ComboSelect(string item, string how = null, double waitS = 3) {
			//Works with most tested combobox types: native, web, WPF, UWP, ribbon, Office, Qt, Java, JavaFX.
			//	Does not work with OpenOffice: can't expand, need Invoke() but it isn't called because there is no DefaultAction. Didn't try to select items. Never mind, anyway no API in dialogs, only in main window.
			//	Not tested GTK. Don't have apps with working API.
			//Problems in Chrome:
			//	Select() does not work. Use Invoke(); it also collapses.
			//Problems in Firefox:
			//	Standard <select> CB don't change EXPANDED state. Eg in MyBB forum search.
			//	With some CB Select() does not work, and Invoke() works. With others vice versa.
			//	With some CB Invoke() collapses, with others not.
			//Problems in Chrome and Firefox:
			//	CB items may be not attached to the CB in the tree. Etc. More info below in _FindWebNotCOMBOBOX.
			//	With standard CB MouseClick() does not work, because gives item rect same as CB rect.
			//	With some CB selects but it does not work like when clicked. Eg does not refresh the page (eg aruodas).
			//		In FF can't click because of bad rect, but may work together with Select(). Sometimes Enter works.
			
			ThrowIfDisposed_();
			
			elm e = null;
			bool auto = how.NE() || how.FindNot("-1234567890") < 0;
			int h = how.NE() ? -1 : 0; //index in how
			bool invoke = false;
			
			#region detect window type
			int iw = 0; //1 Chrome, 2 Firefox, 3 ribbon, 4 Qt5, 10 Java, 11 JavaFX
			wnd w = default;
			if (MiscFlags.Has(EMiscFlags.Java)) {
				iw = 10;
			} else if (0 == _GetWnd(out w)) {
				if (MiscFlags.Has(EMiscFlags.UIA)) {
					if (w.ClassNameIs("GlassWndClass*")) iw = 11;
				} else {
					iw = w.ClassNameIs("Chrome*", "Mozilla*", "NetUIHWND"/*ribbon*/, "Qt5QWindow*");
					invoke = iw is 1 or 3;
				}
			}
			#endregion
			
			#region activate/focus if need
			if (!auto) {
				if (how.Contains('k')) {
					Focus();
				} else if (how.Contains('m')) {
					if (iw == 10) _GetWnd(out w);
					w.Window.Activate();
				}
			} else if (iw is 4 or 10) { //Qt, Java
				Focus();
			}
			#endregion
			
			#region expand
			bool noExpand = false, noState = false;
			if (RoleInt == ERole.COMBOBOX) {
				var state = State;
				noExpand = state.Has(EState.EXPANDED);
				noState = !state.HasAny(EState.COLLAPSED | EState.EXPANDED); //eg ribbon
			}
			if (h == 0 && how[h] == '~') {
				h++;
			} else if (!noExpand) {
				Expand(true, waitS: iw == 2 || noState ? -.1 : waitS);
				if (h == 0) _Sleep(); else 10.ms();
			}
			#endregion
			
			#region find item
			waitS = -Math.Abs(waitS);
			var f = Elm[null, item, flags: EFFlags.HiddenToo]; //usually CB item role is LISTITEM, sometimes MENUITEM, Java "label"; let's support any. May need HiddenToo for offscreen items, eg Java.
			if (iw is 1 or 2 && RoleInt is not ERole.COMBOBOX) {
				_FindWebNotCOMBOBOX();
			} else {
				var loop = new WaitLoop(waitS);
				for (; ; ) {
					e = f.Find();
					if (e == null) {
						if (iw == 11) { //JavaFX drop-down list isn't connected to the CB
							var wp = w.Get.EnabledOwned();
							//TODO3: now unreliable. Can detect a wrong window, eg a random tooltip.
							//	Try to detect more reliably. Eg must have certain styles and rect, contain LIST or MENUPOPUP, etc.
							//	Then could use with any window, not only JavaFX.
							//	Also then could detect expanded state even when there is no EXPANDED state.
							//	EnabledOwned isn't reliable in other places too.
							if (!wp.Is0) e = wp.Elm["LISTITEM", item, flags: EFFlags.UIA].Find();
						} else if (RoleInt != ERole.COMBOBOX && ChildCount == 0) { //eg ribbon dropdown button
							e = f.In(Parent).Find();
						}
					}
					if (e != null) break;
					if (!loop.Sleep()) break;
				}
			}
			if (e == null) throw new NotFoundException($"Can't find combo box item \"{item}\".");
			#endregion
			
			#region select and collapse
			if (!auto) {
				for (; h < how.Length; h++) {
					bool hasSelect = false;
					switch (how[h]) {
					case 's':
						e.Select();
						hasSelect = true;
						break;
					case 'i':
						e.Invoke();
						break;
					case 'm':
						if (iw == 2 && !hasSelect) e.Select(); //if bad rect, with this usually works anyway in FF
						e.MouseClick(scroll: iw == 2 ? 0 : 10);
						//FF bug: if scroll!=0, scrolls the combo (entire page), not the dropdown list item. Good: Select() scrolls.
						//note: WPF CB item rect changes because of dropdown animation
						break;
					case 'k':
						_Keys();
						break;
					case 'c':
						Expand(!true, waitS: -0.01, ignoreState: true);
						break;
					case ' ':
						break;
					default:
						_Sleep();
						continue;
					}
				}
			} else if (invoke) {
				e.Invoke();
			} else if (iw is 4 or 10) { //Qt, Java
				_Keys(); //nothing else works, only mouse if don't need to scroll
			} else {
				bool ignoreState = noState || (iw == 2 && !State.Has(EState.EXPANDED)); //standard <select> CB in FF does not change state
				e.Select();
				Expand(!true, waitS: -0.01, ignoreState: ignoreState);
			}
			#endregion
			return e;
			
			void _Keys() {
				var a = e.Parent.Elm[e.Role, prop: "level=0", flags: EFFlags.HiddenToo].FindAll(); //these are slow, but much faster than keys.send
				wildex wild = item;
				int i = Array.FindIndex(a, o => wild.Match(o.Name));
				if (i < 0) throw new AuException();
				Au.keys.send($"Home PgUp*{a.Length / 4} Down*{i} Enter"); //Home doesn't work with editable CB; PgUp may not work with some CB
			}
			
			void _Sleep() {
				if (!how[h].IsAsciiDigit()) { if (e == null) return; throw new ArgumentException("Invalid character " + how[h], "how"); }
				if (!how.ToInt(out int ms, h, out h)) throw new ArgumentException("Invalid number", "how");
				ms.ms();
			}
			
			void _FindWebNotCOMBOBOX() {
				//Web pages have multiple CB types. Sometimes the dropdown list isn't a descendant of the CB.
				//	In Google Advanced Search it is LIST with 2 children: MENUITEM and LISTITEM with same name (the selected).
				//		The list is a MENUPOPUP/MENUITEM near the bottom of the document. It may not contain the selected item.
				//	In Github profile Stars it is BUTTON with 0 children.
				//		The list is a MENUPOPUP/MENUITEM in its parent.
				
				elm pa = null, doc = null;
				var loop = new WaitLoop(waitS);
				for (; ; ) {
					e = f.Find(); //if the item is already selected, the dropdown list may not contain it, and this finds it
					if (e != null) break;
					pa ??= Parent; if (pa == null) break;
					var mp = pa.Elm["MENUPOPUP"].Find();
					if (mp != null) {
						e = mp.Elm["MENUITEM", item].Find();
					} else {
						if (doc == null) {
							for (var p = pa; p != null; p = p.Parent) if (p.RoleInt == ERole.DOCUMENT) { doc = p; break; }
						}
						if (doc != null) {
							e = doc.Elm["MENUPOPUP", flags: EFFlags.Reverse].Find()?.Elm["MENUITEM", item].Find();
						}
					}
					if (e != null) {
						invoke = true; //selects and closes in FF too; else difficult/unreliable.
						break;
					}
					if (!loop.Sleep()) break;
				}
			}
		}
		
		//wnd _GetOwnedPopupWindow(wnd w, RECT? r = null) {
		//	var p = w.Window.Get.EnabledOwned();
		//	if (!p.Is0) {
		//		var k = r ?? Rect;
		//		int i = k.Height / 2;
		//		k.Inflate(i, i);
		//		var pr = p.Rect;
		//		if (!pr.IntersectsWith(k) || pr.Height < k.Height * 2) p = default;
		//	}
		//	return p;
		//}
		
		const int TVM_MAPACCIDTOHTREEITEM = 0x112A;
		const int TVM_GETNEXTITEM = 0x110A;
		const int TVM_MAPHTREEITEMTOACCID = 0x112B;
		const int TVM_GETITEMSTATE = 0x1127;
		const int TVM_ENSUREVISIBLE = 0x1114;
		const int TVM_SELECTITEM = 0x110B;
		
		const int TVGN_ROOT = 0x0;
		const int TVGN_CHILD = 0x4;
		const int TVGN_NEXT = 0x1;
		const int TVGN_CARET = 0x9;
		const int TVSI_NOSINGLEEXPAND = 0x8000;
		
		const int TVIS_EXPANDED = 0x20;
		
		const int LVM_ENSUREVISIBLE = 0x1013;
		
		const int LB_SETTOPINDEX = 0x197;
		
	}
}

//rejected:
//	MenuSelect(path) - click, wait for popup menu, find/click, wait for another popup, .... Can check/uncheck.
//		Much easier and better to use keys.
//	ContextMenuSelect(path).
//		Let use MouseClickR and keys.

//TEST:
//	IUIAutomationScrollPattern: Scroll, SetScrollPercent.
//	IUIAutomationWindowPattern: Close, WaitForInputIdle, CurrentIsModal, CurrentWindowInteractionState.
