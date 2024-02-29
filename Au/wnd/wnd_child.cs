namespace Au {
	public unsafe partial struct wnd {
		/// <summary>
		/// Finds a child control and returns its handle as <see cref="wnd"/>.
		/// </summary>
		/// <returns>Returns <c>default(wnd)</c> if not found. See also: <see cref="Is0"/>.</returns>
		/// <param name="name">
		/// Control name.
		/// String format: [wildcard expression](xref:wildcard_expression).
		/// <c>null</c> means "can be any". <c>""</c> means "no name".
		/// 
		/// By default to get control names this function uses <see cref="Name"/>.
		/// Can start with these prefix strings:
		/// <br/>• <c>"***text "</c> - use <see cref="ControlText"/>. Slower and less reliable because can get editable text. If a character can be underlined with Alt, insert <c>'&amp;'</c> before it.
		/// <br/>• <c>"***elmName "</c> - use <see cref="NameElm"/>. Slower.
		/// <br/>• <c>"***wfName "</c> - use .NET Forms control name (see <see cref="WinformsControlNames"/>). Slower and can fail because of [](xref:uac).
		/// </param>
		/// <param name="cn">
		/// Control class name.
		/// String format: [wildcard expression](xref:wildcard_expression).
		/// <c>null</c> means "can be any". Cannot be <c>""</c>.
		/// </param>
		/// <param name="flags"></param>
		/// <param name="id">Control id. See <see cref="ControlId"/>. Not used if <c>null</c> (default).</param>
		/// <param name="also">
		/// Callback function. Called for each matching control.
		/// It can evaluate more properties of the control and return <c>true</c> when they match.
		/// Example: <c>also: t =&gt; t.IsEnabled</c>
		/// </param>
		/// <param name="skip">
		/// 0-based index of matching control.
		/// For example, if 1, the function skips the first matching control and returns the second.
		/// </param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <exception cref="ArgumentException">
		/// - <i>name</i> starts with <c>"***"</c>, but the prefix is invalid.
		/// - <i>cn</i> is <c>""</c>. To match any, use <c>null</c>.
		/// - Invalid wildcard expression (<c>"**options "</c> or regular expression).
		/// </exception>
		/// <remarks>
		/// To create code for this function, use dialog "Find window".
		/// </remarks>
		public wnd Child(
			[ParamString(PSFormat.Wildex)] string name = null,
			[ParamString(PSFormat.Wildex)] string cn = null,
			WCFlags flags = 0, int? id = null, Func<wnd, bool> also = null, int skip = 0
			) => new wndChildFinder(name, cn, flags, id, also, skip).Find(this);

		/// <summary>
		/// Finds a child control and returns its handle as <see cref="wnd"/>. Can wait and throw <b>NotFoundException</b>.
		/// </summary>
		/// <returns>Child control handle. If not found, throws exception or returns <c>default(wnd)</c> (if <i>wait</i> negative).</returns>
		/// <param name="wait">The wait timeout, seconds. If 0, does not wait. If negative, does not throw exception when not found.</param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc). Or closed while waiting.</exception>
		/// <exception cref="NotFoundException" />
		/// <inheritdoc cref="Child(string, string, WCFlags, int?, Func{wnd, bool}, int)"/>
		public wnd Child(
			Seconds wait,
			[ParamString(PSFormat.Wildex)] string name = null,
			[ParamString(PSFormat.Wildex)] string cn = null,
			WCFlags flags = 0, int? id = null, Func<wnd, bool> also = null, int skip = 0
			) => new wndChildFinder(name, cn, flags, id, also, skip).Find(this, wait);

		/// <summary>
		/// Finds all matching child controls.
		/// </summary>
		/// <returns>Array containing zero or more <b>wnd</b>.</returns>
		/// <remarks>
		/// Everything except the return type is the same as with <see cref="Child"/>.
		/// 
		/// In the returned array, hidden controls (when using <see cref="WCFlags.HiddenToo"/>) are always after visible controls.
		/// </remarks>
		/// <seealso cref="getwnd.Children"/>
		/// <inheritdoc cref="Child(string, string, WCFlags, int?, Func{wnd, bool}, int)"/>
		public wnd[] ChildAll(
			[ParamString(PSFormat.Wildex)] string name = null,
			[ParamString(PSFormat.Wildex)] string cn = null,
			WCFlags flags = 0, int? id = null, Func<wnd, bool> also = null) {
			//ThrowIfInvalid(); //will be called later
			var f = new wndChildFinder(name, cn, flags, id, also);
			return f.FindAll(this);
		}

		/// <summary>
		/// Returns <c>true</c> if this window contains the specified control.
		/// Calls <see cref="wndChildFinder.Exists"/>.
		/// </summary>
		/// <exception cref="AuWndException"/>
		/// <example>
		/// Find window that contains certain control, and get the control too.
		/// <code><![CDATA[
		/// var cf = new wndChildFinder("Password*", "Static");
		/// wnd w = wnd.find(cn: "#32770", also: t => t.HasChild(cf));
		/// print.it(w);
		/// print.it(cf.Result);
		/// ]]></code>
		/// The same with parameter <i>contains</i>.
		/// <code><![CDATA[
		/// var cf = new wndChildFinder("Password*", "Static");
		/// wnd w = wnd.find(cn: "#32770", contains: cf);
		/// print.it(w);
		/// print.it(cf.Result);
		/// ]]></code>
		/// </example>
		public bool HasChild(wndChildFinder f) => f.Exists(this);

		//rejected. Rare. Can use w.HasChild(new("Apply", "Button")) or !w.Child(...).Is0 etc. Or would also need HasElm(string...).
		///// <summary>
		///// Returns <c>true</c> if this window contains the specified control.
		///// Calls <see cref="Child"/>.
		///// <para>NOTE: Calling this function many times with same arguments is inefficient. Instead create new <see cref="wndChildFinder"/> and call <see cref="wndChildFinder.Exists"/> or <see cref="HasChild(wndChildFinder)"/>. See example.</para>
		///// </summary>
		///// <remarks></remarks>
		///// <example></example>
		///// <inheritdoc cref="Child(string, string, WCFlags, int?, Func{wnd, bool}, int)"/>
		//public bool HasChild(
		//	[ParamString(PSFormat.Wildex)] string name = null,
		//	[ParamString(PSFormat.Wildex)] string cn = null,
		//	WCFlags flags = 0, int? id = null, Func<wnd, bool> also = null, int skip = 0) {
		//	return default != Child(name, cn, flags, id, also, skip);
		//}

		/// <summary>
		/// Returns <c>true</c> if this window contains the specified UI element.
		/// Calls <see cref="elmFinder.Exists"/>.
		/// </summary>
		/// <exception cref="AuWndException"/>
		/// <example>
		/// Find window that contains certain UI element, and get the UI element too.
		/// <code><![CDATA[
		/// var f = new elmFinder("BUTTON", "OK");
		/// wnd w = wnd.find(cn: "#32770", also: t => t.HasElm(f));
		/// print.it(w);
		/// print.it(f.Result);
		/// ]]></code>
		/// The same with parameter <i>contains</i>.
		/// <code><![CDATA[
		/// var f = new elmFinder("BUTTON", "OK");
		/// wnd w = wnd.find(cn: "#32770", contains: f);
		/// print.it(w);
		/// print.it(f.Result);
		/// ]]></code>
		/// </example>
		public bool HasElm(elmFinder f) => f.Find_(false, this, null);

		//rejected. Use Child. Don't need 2 function for the same. Also the wait parameter is confusing. Also there is no finder.
		//	This is faster and less garbage, but it's not so important. Also there is ChildFast(id) for direct children.
		///// <summary>
		///// Finds a child control by its id and returns its handle as <see cref="wnd"/>.
		///// </summary>
		///// <returns>Child control handle, or <c>default(wnd)</c> if not found. See also: <see cref="Is0"/>.</returns>
		///// <param name="id">Control id.</param>
		///// <param name="flags">This function supports flags <b>DirectChild</b> and <b>HiddenToo</b>. If both are set, it is much faster because uses API <msdn>GetDlgItem</msdn>. Else uses API <msdn>EnumChildWindows</msdn>, like <see cref="Child"/>.</param>
		///// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		///// <remarks>
		///// To create code for this function, use dialog "Find window".
		///// 
		///// Not all controls have a useful id. If control id is not unique or is different in each window instance, this function is not useful.
		///// </remarks>
		//public wnd ChildById(int id, WCFlags flags = 0) {
		//	ThrowIfInvalid();
		//	if (flags.Has(WCFlags.DirectChild | WCFlags.HiddenToo)) return Api.GetDlgItem(this, id); //fast

		//	var d = new _KidEnumData() { wThis = this, id = id }; //info: to avoid garbage delegates, we use _KidEnumData instead of captured variables
		//	var wParent = this;
		//	Api.EnumChildWindows(this, (c, p) => {
		//		ref var x = ref *(_KidEnumData*)p;
		//		if (c.ControlId == x.id) {
		//			if (x.flags.Has(WCFlags.DirectChild) && c.ParentGWL_ != x.wThis) return 1;
		//			if (c.IsVisibleIn_(wParent)) { x.cVisible = c; return 0; }
		//			if (x.flags.Has(WCFlags.HiddenToo) && x.cHidden.Is0) x.cHidden = c;
		//		}
		//		return 1;
		//	}, &d);
		//	return d.cVisible.Is0 ? d.cHidden : d.cVisible;
		//}

		///// <summary>
		///// Finds a child control by its id and returns its handle as <see cref="wnd"/>. Can wait and throw <b>NotFoundException</b>.
		///// </summary>
		///// <returns>Child control handle. If not found, throws exception or returns <c>default(wnd)</c> (if <i>wait</i> negative).</returns>
		///// <param name="wait">The wait timeout, seconds. If 0, does not wait. If negative, does not throw exception when not found.</param>
		///// <param name="id"></param>
		///// <param name="flags"></param>
		///// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc). Or closed while waiting.</exception>
		///// <exception cref="NotFoundException" />
		//public wnd ChildById(Seconds wait, int id, WCFlags flags = 0) {
		//	wnd r;
		//	if (wait.Exists_()) {
		//		r = ChildById(id, flags);
		//	} else {
		//		var to = new WaitLoop(wait);
		//		do { r = ChildById(id, flags); if (!r.Is0) break; } while (to.Sleep());
		//	}
		//	return !r.Is0 || wait.Time < 0 ? r : throw new NotFoundException();
		//}

		//struct _KidEnumData
		//{
		//	public wnd wThis, cVisible, cHidden;
		//	public int id;
		//	public WCFlags flags;
		//}

		/// <summary>
		/// Finds a direct child control by name and/or class name and returns its handle as <see cref="wnd"/>.
		/// </summary>
		/// <returns>Returns <c>default(wnd)</c> if not found. See also: <see cref="Is0"/>. Supports <see cref="lastError"/>.</returns>
		/// <param name="name">
		/// Name.
		/// Full, case-insensitive. Wildcard etc not supported.
		/// <c>null</c> means "can be any". <c>""</c> means "no name".
		/// Must include the invisible <c>'&amp;'</c> characters that are used to underline keyboard shortcuts with the Alt key.
		/// </param>
		/// <param name="cn">
		/// Class name.
		/// Full, case-insensitive. Wildcard etc not supported.
		/// <c>null</c> means "can be any". Cannot be <c>""</c>.
		/// </param>
		/// <param name="wAfter">If used, starts searching from the next control in the Z order.</param>
		/// <remarks>
		/// Calls API <msdn>FindWindowEx</msdn>.
		/// Faster than <see cref="Child"/>, which uses API <msdn>EnumChildWindows</msdn>.
		/// Can be used only when you know full name and/or class name.
		/// Finds hidden controls too. Finds only direct children, not other descendants.
		/// </remarks>
		public wnd ChildFast(string name, string cn, wnd wAfter = default) {
			//ThrowIfInvalid(); //no, it can be HWND_MESSAGE
			if (Is0) {
				Api.SetLastError(Api.ERROR_INVALID_WINDOW_HANDLE);
				return default;
			}
			return Api.FindWindowEx(this, wAfter, cn, name);
		}

		/// <summary>
		/// Finds a direct child control by its id and returns its handle as <see cref="wnd"/>.
		/// </summary>
		/// <returns>Returns <c>default(wnd)</c> if not found. See also: <see cref="Is0"/>. Supports <see cref="lastError"/>.</returns>
		/// <param name="id">Control id.</param>
		/// <remarks>
		/// Calls API <msdn>GetDlgItem</msdn>.
		/// Faster than <see cref="Child"/>, which uses API <msdn>EnumChildWindows</msdn>.
		/// Finds only direct children, not other descendants. Finds hidden controls too.
		/// Not all controls have a useful id. If control id is not unique or is different in each window instance, this function is not useful.
		/// </remarks>
		public wnd ChildFast(int id) {
			//ThrowIfInvalid(); //no, let it be same as other overload
			return Api.GetDlgItem(this, id);
		}

		public partial struct getwnd {
			/// <summary>
			/// Gets child controls, including all descendants.
			/// </summary>
			/// <returns>Array containing zero or more <b>wnd</b>.</returns>
			/// <param name="onlyVisible">Need only visible controls.</param>
			/// <param name="sortFirstVisible">Place all array elements of hidden controls at the end of the array.</param>
			/// <param name="directChild">Need only direct children, not all descendants.</param>
			/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
			/// <remarks>
			/// Calls API <msdn>EnumChildWindows</msdn>.
			/// </remarks>
			/// <seealso cref="ChildAll"/>
			public wnd[] Children(bool onlyVisible = false, bool sortFirstVisible = false, bool directChild = false) {
				_w.ThrowIfInvalid();
				return Internal_.EnumWindows(Internal_.EnumAPI.EnumChildWindows, onlyVisible, sortFirstVisible, _w, directChild);
			}

			//rejected
			///// <summary>
			///// Gets child controls, including all descendants.
			///// </summary>
			///// <param name="a">Receives results. If <c>null</c>, this function creates new <b>List</b>, else clears before adding items.</param>
			///// <param name="onlyVisible">Need only visible controls.</param>
			///// <param name="sortFirstVisible">Place all array elements of hidden controls at the end of the array.</param>
			///// <param name="directChild">Need only direct children, not all descendants.</param>
			///// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
			///// <remarks>
			///// Use this overload to avoid much garbage when calling frequently with the same <b>List</b> variable. Other overload always allocates new array. This overload in most cases reuses memory allocated for the list variable.
			///// </remarks>
			//public void Children(ref List<wnd> a, bool onlyVisible = false, bool sortFirstVisible = false, bool directChild = false) {
			//	_w.ThrowIfInvalid();
			//	Internal_.EnumWindows2(Internal_.EnumAPI.EnumChildWindows, onlyVisible, sortFirstVisible, _w, directChild, list: a ??= new List<wnd>());
			//}

			//rejected: unreliable.
			///// <summary>
			///// Gets list of direct child controls.
			///// Faster than API EnumChildWindows.
			///// Should be used only with windows of current thread. Else it is unreliable because, if some controls are zordered or destroyed while enumerating, some controls can be skipped or retrieved more than once.
			///// </summary>
			//public wnd[] DirectChildrenFastUnsafe(string cn = null)
			//{
			//	wildex wild = cn;
			//	var a = new List<wnd>();
			//	for(wnd c = FirstChild; !c.Is0; c = c.Next) {
			//		if(wild != null && !c._ClassNameIs(wild)) continue;
			//		a.Add(c);
			//	}
			//	return a.ToArray();
			//}
		}

		///// <summary>
		///// Casts this to <see cref="WButton"/>.
		///// </summary>
		//public WButton AsButton => new(this);

		/// <summary>
		/// Finds and clicks a button in this window. Does not use the mouse.
		/// </summary>
		/// <param name="id">Control id of the button. See <see cref="Child"/>.</param>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <exception cref="NotFoundException">Button not found.</exception>
		/// <remarks>
		/// This function just calls <see cref="Child"/> and <see cref="mouse.postClick"/>.
		/// Uses this code: <c>mouse.postClick(this.Child(1, id: id));</c>
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// wnd.find("Options").ButtonClick(2);
		/// ]]></code>
		/// </example>
		public void ButtonClick(int id) => mouse.postClick(this.Child(1, id: id));

		/// <summary>
		/// Finds and clicks a button in this window. Does not use the mouse.
		/// </summary>
		/// <param name="name">Button name. String format: [wildcard expression](xref:wildcard_expression).</param>
		/// <param name="asControl">
		/// If <c>true</c>, finds/clicks as child control: <c>mouse.postClick(this.Child(1, name, roleCN ?? "*Button*"));</c>.
		/// If <c>false</c>, finds/clicks as UI element: <c>this.Elm[roleCN ?? "BUTTON", name].Find(1).Invoke();</c>.
		/// Default is <c>false</c>; it's slower but works with more windows.
		/// </param>
		/// <param name="roleCN">UI element role or control class name (if <i>asControl</i> <c>true</c>). String format: [wildcard expression](xref:wildcard_expression). Default role is <c>"BUTTON"</c>, class name <c>"*Button*"</c>.</param>
		/// <remarks>
		/// This function is just a shorter way to call other functions that have more options but require more code to call. If <i>asControl</i> <c>true</c>, it calls <see cref="Child"/> and <see cref="mouse.postClick"/>. Else <see cref="Elm"/>, <see cref="elmFinder.this"/>, <see cref="elmFinder.Find"/> and <see cref="elm.Invoke"/>.
		/// </remarks>
		/// <exception cref="ArgumentException">Invalid <i>name</i> (when starts with <c>"***"</c>). See <see cref="elmFinder.this"/>, <see cref="Child"/>.</exception>
		/// <exception cref="AuWndException">This variable is invalid (window not found, closed, etc).</exception>
		/// <exception cref="NotFoundException">Button not found.</exception>
		/// <exception cref="AuException">Failed to click. For example need to activate the window. No exception if <i>asControl</i> <c>true</c>.</exception>
		/// <example>
		/// <code><![CDATA[
		/// wnd.find("Options").ButtonClick("Cancel");
		/// ]]></code>
		/// </example>
		public void ButtonClick([ParamString(PSFormat.Wildex)] string name, bool asControl = false, string roleCN = null) {
			if (asControl) {
				mouse.postClick(this.Child(1, name, roleCN ?? "*Button*"));
			} else {
				this.Elm[roleCN ?? "BUTTON", name].Find(1).Invoke();
			}
		}

		/// <summary>
		/// Posts a "menu item clicked" notification (<msdn>WM_COMMAND</msdn>) as if that menu item has been clicked. Does not use the mouse.
		/// </summary>
		/// <param name="itemId">Menu item id. Must be in range 1 to 0xffff.</param>
		/// <param name="systemMenu">The menu item is in the title bar's context menu, not in the menu bar. Posts <msdn>WM_SYSCOMMAND</msdn> instead.</param>
		/// <exception cref="AuWndException">Invalid window.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <i>itemId</i>.</exception>
		/// <remarks>
		/// Works only with classic menus. The drop-down menu window class name must be "#32768". Works with menu items in window menu bar, system menu and some context menus.
		/// Does not use the menu itself. Just posts <b>WM_COMMAND</b> or <b>WM_SYSCOMMAND</b> message. Even if a menu item with this id does not exist.
		/// This variable is the window that contains the menu bar or system menu. Or the drop-down menu window (class "#32768") that contains the menu item.
		/// </remarks>
		public void MenuClick(int itemId, bool systemMenu = false) {
			if ((uint)(itemId - 1) >= 0xffff) throw new ArgumentOutOfRangeException();
			ThrowIfInvalid();
			var w = this;
			if (ClassNameIs("#32768") && miscInfo.getGUIThreadInfo(out var g, ThreadId) && !g.hwndMenuOwner.Is0) w = g.hwndMenuOwner;
			w.Post(systemMenu ? Api.WM_SYSCOMMAND : Api.WM_COMMAND, itemId);
			w.MinimalSleepIfOtherThread_();
		}

		//rejected: use elm functions instead.
		///// <summary>
		///// Finds a menu item by name and posts a "menu item clicked" notification as if that menu item was clicked. Does not use the mouse.
		///// Works with all standard menus and some non-standard menus.
		///// </summary>
		///// <param name="itemName">
		///// Menu item name.
		///// String format: [wildcard expression](xref:wildcard_expression).
		///// </param>
		///// <param name="systemMenu">The menu item is in the title bar's context menu, not in the menu bar.</param>
		//public void Click([ParamString(PSFormat.wildex)] string itemName, bool systemMenu = false)
		//{
		//	
		//}

		//rejected: need just 1 function. To get state, use elm.
		///// <summary>
		///// Click standard (classic) menu items, get state.
		///// </summary>
		//public static class menu
		//{

		//}
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="wnd.Child"/>.
	/// </summary>
	[Flags]
	public enum WCFlags {
		/// <summary>Can find hidden controls.</summary>
		HiddenToo = 1,

		/// <summary>Skip indirect descendant controls (children of children and so on).</summary>
		DirectChild = 2,
	}

#if !true //rejected. Nobody would use this when there is elm. Eg BM_CLICK is the same as elm.Invoke or elm.PostClick. For Check can use code if(!e.IsChecked) e.Invoke();.
	/// <summary>
	/// Like <see cref="wnd"/>, but has only button, check box and radio button functions - <b>Click</b>, <b>Check</b> etc.
	/// See also <see cref="wnd.AsButton"/>.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// wnd.find("Options").Child("Cancel").AsButton.Click();
	/// ]]></code>
	/// </example>
	public struct WButton
	{
		/// <summary>
		/// Button handle as wnd.
		/// </summary>
		public wnd W { get; }

		internal WButton(wnd w) { W = w; }

		///
		public static implicit operator wnd(WButton b) => b.W;

		/////
		//public static explicit operator WButton(wnd w) => new(w);

		///
		public override string ToString() => W.ToString();

		/// <summary>
		/// Posts a "click" message to this button control. Does not use the mouse.
		/// </summary>
		/// <param name="useElm">Use <see cref="elm.Invoke"/>. If <c>false</c> (default), posts <msdn>BM_CLICK</msdn> message.</param>
		/// <exception cref="AuWndException">This window is invalid.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Works not with all button controls. Sometimes does not work if the window is inactive.
		/// Check boxes and radio buttons also are buttons. This function can click them.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// wnd.find("Options").Child("Cancel").AsButton.Click();
		/// ]]></code>
		/// </example>
		public void Click(bool useElm = false) {
			W.ThrowIfInvalid();
			if (useElm) {
				using var e = elm.fromWindow(W, EObjid.CLIENT); //exception if failed
				e.Invoke();
			} else {
				_PostBmClick(); //async if other thread, because may show a dialog.
			}
			W.MinimalSleepIfOtherThread_();
			//FUTURE: sync better
		}

		void _PostBmClick() {
			var w = W.Window;
			bool workaround = !w.IsActive;
			if (workaround) w.Post(Api.WM_ACTIVATE, 1); //workaround for the documented BM_CLICK bug
			W.Post(BM_CLICK); //it sends WM_LBUTTONDOWN/UP
			if (workaround) w.Post(Api.WM_ACTIVATE, 0);
		}

		/// <summary>
		/// Checks or unchecks this check box. Does not use the mouse.
		/// Calls <see cref="SetCheckState"/> with state 0 or 1.
		/// </summary>
		/// <param name="on">Checks if <c>true</c>, unchecks if <c>false</c>.</param>
		/// <param name="useElm"></param>
		/// <exception cref="AuWndException">This window is invalid.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Works not with all button controls. Sometimes does not work if the window is inactive.
		/// If this is a radio button, does not uncheck other radio buttons in its group.
		/// </remarks>
		public void Check(bool on, bool useElm = false) {
			SetCheckState(on ? 1 : 0, useElm);
		}

		/// <summary>
		/// Sets checkbox state. Does not use the mouse.
		/// </summary>
		/// <param name="state">0 unchecked, 1 checked, 2 indeterminate.</param>
		/// <param name="useElm">Use <see cref="elm.Invoke"/>. If <c>false</c> (default), posts <msdn>BM_SETCHECK</msdn> message and also BN_CLICKED notification to the parent window; if that is not possible, instead uses <msdn>BM_CLICK</msdn> message.</param>
		/// <exception cref="ArgumentOutOfRangeException">Invalid state.</exception>
		/// <exception cref="AuWndException">This window is invalid.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Does nothing if the check box already has the specified check state (if can get it).
		/// Works not with all button controls. Sometimes does not work if the window is inactive.
		/// If this is a radio button, does not uncheck other radio buttons in its group.
		/// </remarks>
		public void SetCheckState(int state, bool useElm = false) {
			if (state < 0 || state > 2) throw new ArgumentOutOfRangeException();
			W.ThrowIfInvalid();
			int id;
			if (useElm || !_IsCheckbox() || (uint)((id = W.ControlId) - 1) >= 0xffff) {
				using var e = elm.fromWindow(W, EObjid.CLIENT); //exception if failed
				int k = _GetElmCheckState(e);
				if (k == state) return;
				if (useElm) e.Invoke(); else _PostBmClick();
				bool clickAgain = false;
				switch (state) {
				case 0:
					if (k == 1) {
						W.MinimalSleepIfOtherThread_();
						if (GetCheckState(true) == 2) clickAgain = true;
						else return;
					}
					break;
				case 1:
					if (k == 2) clickAgain = true;
					break;
				case 2:
					if (k == 0) clickAgain = true;
					break;
				}
				if (clickAgain) {
					if (useElm) e.Invoke(); else _PostBmClick();
				}
			} else {
				if (state == W.Send(BM_GETCHECK)) return;
				W.Post(BM_SETCHECK, state);
				W.Get.DirectParent.Post(Api.WM_COMMAND, id, (nint)W);
			}
			W.MinimalSleepIfOtherThread_();
		}

		/// <summary>
		/// Gets check state of this check box or radio button.
		/// Calls <see cref="GetCheckState"/> and returns <c>true</c> if it returns 1.
		/// </summary>
		public bool IsChecked(bool useElm = false) {
			return 1 == GetCheckState(useElm);
		}

		/// <summary>
		/// Gets check state of this check box or radio button.
		/// Returns 0 if unchecked, 1 if checked, 2 if indeterminate. Also returns 0 if this is not a button or if failed to get state.
		/// </summary>
		/// <param name="useElm">Use <see cref="elm.State"/>. If <c>false</c> (default) and this button has a standard checkbox style, uses API <msdn>BM_GETCHECK</msdn>.</param>
		public int GetCheckState(bool useElm = false) {
			if (useElm || !_IsCheckbox()) {
				//info: Windows Forms controls are user-drawn and don't have one of the styles, therefore BM_GETCHECK does not work.
				try { //avoid exception in property-get functions
					using var e = elm.fromWindow(W, EObjid.CLIENT, flags: EWFlags.NoThrow);
					if (e == null) return 0;
					return _GetElmCheckState(e);
				}
				catch (Exception ex) { Debug_.Print(ex); } //CONSIDER: if fails, show warning. In all wnd property-get functions.
				return 0;
			} else {
				return (int)W.Send(BM_GETCHECK);
			}
		}

		int _GetElmCheckState(elm e) {
			var state = e.State;
			if (state.Has(EState.MIXED)) return 2;
			if (state.Has(EState.CHECKED)) return 1;
			return 0;
		}

		bool _IsCheckbox() {
			switch ((uint)W.Style & 15) {
			case BS_CHECKBOX:
			case BS_AUTOCHECKBOX:
			case BS_RADIOBUTTON:
			case BS_3STATE:
			case BS_AUTO3STATE:
			case BS_AUTORADIOBUTTON:
				return true;
			}
			return false;
		}

		internal const int BM_CLICK = 0xF5;
		internal const int BM_GETCHECK = 0xF0;
		internal const int BM_SETCHECK = 0xF1;

		internal const uint BS_CHECKBOX = 0x2;
		internal const uint BS_AUTOCHECKBOX = 0x3;
		internal const uint BS_RADIOBUTTON = 0x4;
		internal const uint BS_3STATE = 0x5;
		internal const uint BS_AUTO3STATE = 0x6;
		internal const uint BS_AUTORADIOBUTTON = 0x9;

	}
#endif
}
