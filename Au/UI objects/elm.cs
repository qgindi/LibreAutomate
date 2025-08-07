namespace Au;

/// <summary>
/// Represents a UI element. Clicks, gets properties, etc.
/// </summary>
/// <remarks>
/// <para>
/// UI elements are user interface (UI) parts that are accessible through programming interfaces (API). For example buttons, links, list items.
/// This class can find them, get properties, click, etc.
/// Web pages and most other windows support UI elements.
/// </para>
/// <para>
/// An <b>elm</b> variable contains a COM interface pointer (<msdn>IAccessible</msdn> or other) and uses methods of that interface or/and related API.
/// </para>
/// <para>
/// <b>elm</b> functions that get properties don't throw exception when the COM etc method failed (returned an error code of <b>HRESULT</b> type).
/// Then they return <c>""</c> (string properties), 0, <c>false</c>, <c>null</c> or empty collection, depending on return type.
/// Applications implement UI elements differently, often with bugs, and their COM interface functions return a variety of error codes.
/// It's impossible to reliably detect whether the error code means an error or the property is merely unavailable.
/// These <b>elm</b> functions also set the last error code of this thread = the return value (<b>HRESULT</b>) of the COM function, and callers can use <see cref="lastError"/> to get it.
/// If <b>lastError.code</b> returns 1 (<b>S_FALSE</b>), in most cases it's not an error, just the property is unavailable. On error it will probably be a negative error code.
/// </para>
/// <para>
/// You can dispose <b>elm</b> variables to release the COM object, but it is not necessary (GC will do it later).
/// </para>
/// <para>
/// An <b>elm</b> variable cannot be used in multiple threads. Only <b>Dispose</b> can be called in any thread.
/// </para>
/// <para>
/// UI elements are implemented and live in their applications. This class just communicates with them.
/// [Known UI element issues in various applications](xref:ui_element_issues)
/// </para>
/// </remarks>
/// <example>
/// Click link <c>"Example"</c> in Chrome.
/// <code><![CDATA[
/// var w = wnd.find(0, "* Chrome");
/// var e = w.Elm["web:LINK", "Example"].Find(5);
/// e.Invoke();
/// ]]></code>
/// Click a link, wait for new web page, click a link in it.
/// <code><![CDATA[
/// var w = wnd.find(0, "* Chrome");
/// var e = w.Elm["web:LINK", "Link 1"].Find(5);
/// e.WebInvoke();
/// w.Elm["web:LINK", "Link 2"].Find(5).WebInvoke();
/// ]]></code>
/// </example>
[StructLayout(LayoutKind.Sequential)]
public unsafe sealed partial class elm : IDisposable {
	//FUTURE: elm.more.EnableElmInChromeWebPagesWhenItStarts
	//FUTURE: elm.more.EnableElmInJavaWindows (see JavaEnableJAB in QM2)
	//FUTURE: add functions to marshal to another thread.

	internal struct Misc_ {
		public EMiscFlags flags;
		public byte roleByte; //for optimization. 0 if not set or failed to get. 0xFF (ERole.Custom) if VT_BSTR or not 1-ROLE_MAX.
		public ushort level; //for ToString. 0 if not set.
		
		public void SetRole(ERole role) { this.roleByte = (byte)(role <= 0 || role > ERole.TREEBUTTON ? ERole.Custom : role); }
		public void SetLevel(int level) { this.level = (ushort)Math.Clamp(level, 0, 0xffff); }
	}
	
	internal IntPtr _iacc;
	internal int _elem;
	internal Misc_ _misc;
	//Real elm object memory size with header: 32 bytes on 64-bit.
	//We don't use RCW<IAccessible>, which would add another 32 bytes.
	
	/// <summary>
	/// Creates elm from <b>IAccessible</b> and child id.
	/// By default does not <b>AddRef</b>.
	/// <i>iacc</i> must not be 0.
	/// </summary>
	internal elm(IntPtr iacc, int elem = 0, bool addRef = false) {
		_Set(iacc, elem, default, addRef);
	}
	
	/// <summary>
	/// Creates <b>elm</b> from <b>Cpp_Acc</b>.
	/// By default does not <b>AddRef</b>.
	/// <c>x.acc</c> must not be 0.
	/// </summary>
	internal elm(Cpp.Cpp_Acc x, bool addRef = false) {
		_Set(x.acc, x.elem, x.misc, addRef);
	}
	
	/// <summary>
	/// Sets fields.
	/// <b>_iacc</b> must be 0, <i>iacc</i> not 0.
	/// </summary>
	void _Set(IntPtr iacc, int elem = 0, Misc_ misc = default, bool addRef = false) {
		Debug.Assert(_iacc == default);
		Debug.Assert(iacc != default);
		if (addRef) Marshal.AddRef(iacc);
		_iacc = iacc;
		_elem = elem;
		_misc = misc;
		
		int mp = _MemoryPressure;
		GC.AddMemoryPressure(mp);
		//s_dmp += mp; if(s_dmp > DebugMaxMemoryPressure) DebugMaxMemoryPressure = s_dmp;
		//DebugMemorySum += mp;
	}
	
	int _MemoryPressure => _elem == 0 ? c_memoryPressure : c_memoryPressure / 10;
	const int c_memoryPressure = 1000; //assume this is the average UI element memory size in both processes
	
	//internal static int DebugMaxMemoryPressure;
	//static int s_dmp;
	//internal static int DebugMemorySum;
	
	///
	void Dispose(bool disposing) {
		//print.it(disposing);
		if (_iacc != default) {
			var t = _iacc; _iacc = default;
			//perf.first();
			Marshal.Release(t);
			//perf.nw();
			//print.it($"rel: {t}  {Marshal.Release(t)}");
			
			int mp = _MemoryPressure;
			GC.RemoveMemoryPressure(mp);
			//s_dmp -= mp;
		}
		_elem = 0;
		_misc = default;
	}
	
	/// <summary>
	/// Releases COM object and clears this variable.
	/// </summary>
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	
	///
	~elm() {
#if DEBUG
		try { Dispose(false); }
		catch (Exception ex) { print.it(ex); }
#else
		Dispose(false);
#endif
	}
	
	//internal void Debug1() {
	//	print.it(_iacc, Debug_.GetComObjRefCount_(_iacc));
	//}
	
	//not used in this library, but sometimes may need it for testing something in scripts.
	internal IntPtr Iacc_ => _iacc;
	
	/// <summary>
	/// Gets or changes simple element id, also known as child id.
	/// </summary>
	/// <remarks>
	/// Most UI elements are not simple elements. Then this property is 0.
	/// Often (but not always) this property is the 1-based item index in parent. For example <b>LISTITEM</b> in <b>LIST</b>.
	/// The <c>set</c> function sometimes can be used as a fast alternative to <see cref="Navigate"/>. It modifies only this variable. It does not check whether the value is valid.
	/// Simple elements cannot have child elements.
	/// </remarks>
	public int Item { get => _elem; set { _misc.roleByte = 0; _elem = value; } }
	
	/// <summary>
	/// Returns some additional info about this variable, such as how the UI element was retrieved (inproc, UIA, Java).
	/// </summary>
	public EMiscFlags MiscFlags => _misc.flags;
	
	/// <summary>
	/// Gets or sets indentation level for <see cref="ToString"/>.
	/// </summary>
	/// <remarks>
	/// When <b>find</b> or similar function finds a UI element, it sets this property of the <b>elm</b> variable. If <b>fromXY</b> etc, it is 0 (unknown).
	/// When searching in a window, at level 0 are direct children of the <b>WINDOW</b>. When searching in controls (specified class or id), at level 0 is the control. When searching in <b>elm</b>, at level 0 are its direct children. When searching in web page (role prefix <c>"web:"</c> etc), at level 0 is the web page (role <b>DOCUMENT</b> or <b>PANE</b>).
	/// </remarks>
	public int Level { get => _misc.level; set => _misc.SetLevel(value); }
	
	/// <summary>
	/// Returns <c>true</c> if this variable is disposed.
	/// </summary>
	bool _Disposed => _iacc == default;
	
	internal void ThrowIfDisposed_() {
		if (_Disposed) throw new ObjectDisposedException(nameof(elm));
		WarnInSendMessage_();
	}
	
	internal static void WarnInSendMessage_() {
		if (Api.InSendMessageBlocked) { //as fast as TickCount64
			long t = Environment.TickCount64;
			if (t - s_wismTime > 500) {
				s_wismTime = t;
				print.warning("elm functions may not work here. This code is called by another thread through SendMessage or is a low-level hook. Try workarounds: call ReplyMessage (Windows API); use an asynchronous way to call the elm function (timer, Dispatcher.InvokeAsync, etc); let that thread instead of SendMessage use an asynchronous function such as PostMessage.");
			}
		}
	}
	static long s_wismTime;
	
	/// <summary>
	/// Gets UI element of window or control. Or some its standard part - client area, titlebar etc.
	/// </summary>
	/// <param name="w">Window or control.</param>
	/// <param name="objid">Window part id. Default <b>EObjid.WINDOW</b>. Also can be a custom id supported by that window, cast <b>int</b> to <b>EObjid</b>.</param>
	/// <param name="flags">Flags.</param>
	/// <exception cref="AuWndException">Invalid window.</exception>
	/// <exception cref="AuException">Failed. For example, window of a higher [](xref:uac) integrity level process.</exception>
	/// <exception cref="ArgumentException"><i>objid</i> is <b>QUERYCLASSNAMEIDX</b> or <b>NATIVEOM</b>.</exception>
	/// <remarks>
	/// Uses API <msdn>AccessibleObjectFromWindow</msdn>.
	/// </remarks>
	public static elm fromWindow(wnd w, EObjid objid = EObjid.WINDOW, EWFlags flags = 0) {
		WarnInSendMessage_();
		
		bool spec = false;
		switch (objid) {
		case EObjid.QUERYCLASSNAMEIDX: //use WM_GETOBJECT
		case EObjid.NATIVEOM: //use API AccessibleObjectFromWindow
			throw new ArgumentException();
		case EObjid.CARET: //w should be 0
		case EObjid.CURSOR: //w should be 0
		case EObjid.ALERT: //only with AccessibleObjectFromEvent?
		case EObjid.SOUND: //only with AccessibleObjectFromEvent?
			spec = true; flags |= EWFlags.NotInProc;
			break;
		}
		
		var hr = Cpp.Cpp_AccFromWindow(flags.Has(EWFlags.NotInProc) ? 1 : 0, w, objid, out var a, out _);
		if (hr != 0) {
			if (flags.Has(EWFlags.NoThrow)) return null;
			if (spec && w.Is0) throw new AuException();
			w.ThrowIfInvalid();
			_WndThrow(hr, w, "*get UI element from window.");
		}
		return new elm(a);
	}
	
	static void _WndThrow(int hr, wnd w, string es) {
		w.UacCheckAndThrow_(es);
		throw new AuException(hr, es);
	}
	
	/// <summary>
	/// Gets UI element from point.
	/// </summary>
	/// <returns>Returns <c>null</c> if failed. Usually fails if the window is of a higher [](xref:uac) integrity level process. With some windows can fail occasionally.</returns>
	/// <param name="p">
	/// Coordinates.
	/// Tip: To specify coordinates relative to the right, bottom, work area or a non-primary screen, use <see cref="Coord.Normalize"/>, like in the example.
	/// </param>
	/// <param name="flags"></param>
	/// <example>
	/// Get UI element at 100 200.
	/// <code><![CDATA[
	/// var e = elm.fromXY((100, 200));
	/// print.it(e);
	/// ]]></code>
	/// 
	/// Get UI element at 50 from left and 100 from the bottom edge of the work area.
	/// <code><![CDATA[
	/// var e = elm.fromXY(Coord.Normalize(50, ^100, workArea: true));
	/// print.it(e);
	/// ]]></code>
	/// </example>
	public static elm fromXY(POINT p, EXYFlags flags = 0) {
		WarnInSendMessage_();
		
		int hr = Cpp.Cpp_AccFromPoint(p, flags, (flags, wFP, wTL) => {
			if (osVersion.minWin8_1 ? !flags.Has(EXYFlags.NotInProc) : flags.Has(EXYFlags.UIA)) {
				bool dpiV = Dpi.IsWindowVirtualized(wTL);
				if (dpiV) flags |= Enum_.EXYFlags_DpiScaled;
			}
			return flags;
		}, out var a);
		if (hr == 0) return new elm(a);
		return null;
	}
	
	/// <inheritdoc cref="fromXY(POINT, EXYFlags)"/>
	public static elm fromXY(int x, int y, EXYFlags flags = 0) => fromXY(new POINT(x, y), flags);
	
	//rejected: FromXY(Coord, Coord, ...). Coord makes no sense.
	
	/// <summary>
	/// Gets UI element from mouse cursor (pointer) position.
	/// </summary>
	/// <param name="flags"></param>
	/// <exception cref="AuException">Failed. For example, window of a higher [](xref:uac) integrity level process.</exception>
	/// <remarks>
	/// Uses API <msdn>AccessibleObjectFromPoint</msdn>.
	/// </remarks>
	public static elm fromMouse(EXYFlags flags = 0) {
		return fromXY(mouse.xy, flags);
	}
	
	/// <summary>
	/// Gets the keyboard-focused UI element.
	/// </summary>
	/// <returns><c>null</c> if failed.</returns>
	public static elm focused(EFocusedFlags flags = 0) {
		WarnInSendMessage_();
		
		var w = wnd.focused;
		g1:
		if (w.Is0) return null;
		int hr = Cpp.Cpp_AccGetFocused(w, flags, out var a);
		if (hr != 0) {
			var w2 = wnd.focused;
			if (w2 != w) { w = w2; goto g1; }
			return null;
		}
		return new elm(a);
	}
	
	/// <summary>
	/// Gets the UI element that generated the event that is currently being processed by the callback function used with API <msdn>SetWinEventHook</msdn> or <see cref="WinEventHook"/>.
	/// </summary>
	/// <returns><c>null</c> if failed. Supports <see cref="lastError"/>.</returns>
	/// <param name="w"></param>
	/// <param name="idObject"></param>
	/// <param name="idChild"></param>
	/// <remarks>
	/// The parameters are of the callback function.
	/// Uses API <msdn>AccessibleObjectFromEvent</msdn>.
	/// Often fails because the UI element already does not exist, because the callback function is called asynchronously, especially when the event is <b>OBJECT_DESTROY</b>, <b>OBJECT_HIDE</b>, <b>SYSTEM_xEND</b>.
	/// Returns <c>null</c> if failed. Always check the return value, to avoid <b>NullReferenceException</b>. An exception in the callback function kills this process.
	/// </remarks>
	public static elm fromEvent(wnd w, EObjid idObject, int idChild) {
		int hr = Api.AccessibleObjectFromEvent(w, idObject, idChild, out var iacc, out var v);
		if (hr == 0 && iacc == default) hr = Api.E_FAIL;
		if (hr != 0) { lastError.code = hr; return null; }
		int elem = v.vt == Api.VARENUM.VT_I4 ? v.ValueInt : 0;
		return new elm(iacc, elem);
	}
	
#if false //rejected: not useful. Maybe in the future.
	/// <summary>
	/// Gets UI element from a COM object of any type that supports it.
	/// </summary>
	/// <returns><c>null</c> if failed.</returns>
	/// <param name="x">Unmanaged COM object.</param>
	/// <remarks>
	/// The COM object type can be <b>IAccessible</b>, <b>IAccessible2</b>, <b>IHTMLElement</b>, <b>ISimpleDOMNode</b> or any other COM interface type that can give <msdn>IAccessible</msdn> interface pointer through API <msdn>IUnknown.QueryInterface</msdn> or <msdn>IServiceProvider.QueryService</msdn>.
	/// For <b>IHTMLElement</b> and <b>ISimpleDOMNode</b> returns <c>null</c> if the HTML element is not an accessible object. Then you can try to get UI element of its parent HTML element, parent's parent and so on, until succeeds.
	/// </remarks>
	public static elm fromComObject(IntPtr x)
	{
		if(x == default) return null;
		if(MarshalUtil.QueryInterface(x, out IntPtr iacc, Api.IID_IAccessible)
			|| MarshalUtil.QueryService(x, out iacc, Api.IID_IAccessible)
			) return new elm(iacc);
		return null;
	}

	/// <summary>
	/// Gets UI element from a COM object of any type that supports it.
	/// Returns <c>null</c> if failed.
	/// </summary>
	/// <param name="x">Managed COM object.</param>
	/// <remarks>
	/// The COM object type can be <b>IAccessible</b>, <b>IAccessible2</b>, <b>IHTMLElement</b>, <b>ISimpleDOMNode</b> or any other COM interface type that can give <msdn>IAccessible</msdn> interface pointer through API <msdn>IUnknown.QueryInterface</msdn> or <msdn>IServiceProvider.QueryService</msdn>.
	/// For <b>IHTMLElement</b> and <b>ISimpleDOMNode</b> returns <c>null</c> if the HTML element is not an accessible object. Then you can try to get UI element of its parent HTML element, parent's parent and so on, until succeeds.
	/// </remarks>
	public static elm fromComObject(object x)
	{
		if(x == null) return null;

		//FUTURE: support UIA. Don't use LegacyIAccessible, it work not with all windows. Instead wrap in UIAccessible.
		//if(x is UIA.IElement e) { //info: IElement2-7 are IElement too
		//	var pat = e.GetCurrentPattern(UIA.PatternId.LegacyIAccessible) as UIA.ILegacyIAccessiblePattern;
		//	x = pat?.GetIAccessible();
		//	if(x == null) return null;
		//}

		var ip = Marshal.GetIUnknownForObject(x);
		if(ip == default) return null;
		try { return FromComObject(ip); }
		finally { Marshal.Release(ip); }
	}
#endif
	
	/// <summary>
	/// Used only for debug.
	/// </summary>
	enum _FuncId { name = 1, value, description, default_action, role, state, rectangle, parent_object, child_object, container_window, child_count, child_objects, help_text, keyboard_shortcut, html, selection, uiaid, uiacn }
	
	/// <summary>
	/// Calls <b>SetLastError</b> and returns <i>hr</i>.
	/// In Debug config also outputs error in red.
	/// If hr looks like not an error but just the property or action is unavailable, changes it to <b>S_FALSE</b> and does not show error. These are: <b>S_FALSE</b>, <b>DISP_E_MEMBERNOTFOUND</b>, <b>E_NOTIMPL</b>.
	/// <b>_FuncId</b> also can be <b>char</b>, like <c>(_FuncId)'n'</c> for name.
	/// </summary>
	int _Hresult(_FuncId funcId, int hr) {
		if (hr != 0) {
			switch (hr) {
			case Api.DISP_E_MEMBERNOTFOUND: case Api.E_NOTIMPL: hr = Api.S_FALSE; break;
			case (int)Cpp.EError.InvalidParameter: throw new ArgumentException("Invalid argument value.");
			default: Debug.Assert(!Cpp.IsCppError(hr)); break;
			}
#if DEBUG
			if (hr != Api.S_FALSE) {
				_DebugPropGet(funcId, hr);
			}
#endif
		}
		lastError.code = hr;
		return hr;
	}
	
#if DEBUG
	void _DebugPropGet(_FuncId funcId, int hr) {
		if (t_debugNoRecurse || _Disposed) return;
		
		if (funcId >= (_FuncId)'A') {
			switch ((char)funcId) {
			case 'R': funcId = _FuncId.role; break;
			case 'n': funcId = _FuncId.name; break;
			case 'v': funcId = _FuncId.value; break;
			case 'd': funcId = _FuncId.description; break;
			case 'h': funcId = _FuncId.help_text; break;
			case 'a': funcId = _FuncId.default_action; break;
			case 'k': funcId = _FuncId.keyboard_shortcut; break;
			case 's': funcId = _FuncId.state; break;
			case 'r': funcId = _FuncId.rectangle; break;
			case 'u': funcId = _FuncId.uiaid; break;
			case 'U': funcId = _FuncId.uiacn; break;
			}
		}
		
		if (hr == Api.E_FAIL && funcId == _FuncId.default_action) return; //many in old VS etc
		t_debugNoRecurse = true;
		try {
			var s = ToString();
			print.it($"<><c #ff>-{funcId}, 0x{hr:X} - {lastError.messageFor(hr)}    {s}</c>");
		}
		finally { t_debugNoRecurse = false; }
	}
	[ThreadStatic] static bool t_debugNoRecurse;
#endif
	
	/// <summary>
	/// Formats string from main properties of this UI element.
	/// </summary>
	/// <remarks>
	/// The string starts with role. Other properties have format like <c>x="value"</c>, where <c>x</c> is a property character like with <see cref="GetProperties"/>; character <c>e</c> is <see cref="Item"/>. HTML attributes have format <c>@name="value"</c>. In string values are used C# escape sequences, for example <c>\r\n</c> for new line.
	/// Indentation depends on <see cref="Level"/>.
	/// </remarks>
	public override string ToString() {
		if (_Disposed) return "<disposed>";
		if (!GetProperties("Rnsvdarw@", out var k)) return "<failed>";
		
		using (new StringBuilder_(out var b)) {
			if (Level > 0) b.Append(' ', Level);
			b.Append(k.Role);
			_Add('n', k.Name);
			if (k.State != 0) _Add('s', k.State.ToString(), '(', ')');
			_Add('v', k.Value);
			_Add('d', k.Description);
			_Add('a', k.DefaultAction);
			if (!k.Rect.Is0) _Add('r', k.Rect.ToString(), '\0', '\0');
			if (Item != 0) b.Append(",  e=").Append(Item);
			foreach (var kv in k.HtmlAttributes) {
				b.Append(",  @").Append(kv.Key).Append('=').Append('"');
				b.Append(kv.Value.Escape(limit: 250)).Append('"');
			}
			_Add('w', k.WndContainer.ClassName ?? "");
			
			void _Add(char name, string value, char q1 = '"', char q2 = '"') {
				if (value.Length == 0) return;
				var t = value; if (q1 == '"') t = t.Escape(limit: 250);
				b.Append(",  ").Append(name).Append('=');
				if (q1 != '\0') b.Append(q1);
				b.Append(t);
				if (q1 != '\0') b.Append(q2);
			}
			
			return b.ToString();
		}
	}
}
