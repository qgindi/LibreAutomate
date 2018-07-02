using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
//using System.Linq;
//using System.Xml.Linq;

using Au.Types;
using static Au.NoClass;

#pragma warning disable CS0282 //VS bug: shows warning "There is no defined ordering between fields in multiple declarations of partial struct 'Acc'. To specify an ordering, all instance fields must be in the same declaration."

namespace Au
{
	/// <summary>
	/// Accessible object.
	/// Finds controls and smaller objects in a window or web page. Clicks, gets properties, etc.
	/// </summary>
	/// <remarks>
	/// Accessible objects (AO) are various user interface (UI) objects in windows and controls. For example buttons, links, list items. This class can find them, get properties, click, etc. Especially useful with web pages, because there are no controls. And many other windows don't use controls but support AO. But not all UI objects are AO.
	/// 
	/// An Acc instance holds an AO COM pointer (<msdn>IAccessible</msdn>) and a simple element id (int). Most Acc functions wrap IAccessible interface functions or/and related API.
	/// 
	/// Acc functions that get properties don't throw exception when the wrapped IAccessible/etc function failed (returned an error code of HRESULT type). Then they return "" (string properties), 0, false, null or empty collection, dependin on return type. Applications implement AOs differently, often with bugs, and their IAccessible interface functions return a variety of error codes. It's impossible to reliably detect whether the error code means a serious error or the property is merely unavailable. These Acc functions also set the last error code of this thread = the return value (HRESULT) of the IAccessible function, and callers can use <see cref="Native.GetError"/> to get it. If Native.GetError returns 1 (S_FALSE), in most cases it's not an error, just the property is unavailable. On error it will probably be a negative error code.
	/// 
	/// You can dispose Acc variables to release the COM object, but it is not necessary (GC will do it later).
	/// 
	/// An Acc variable cannot be used in multiple threads. Only Dispose can be called in any thread.
	/// 
	/// AOs are implemented and live in their applications. This class just communicates with them.
	/// 
	/// Many applications have various problems with their AOs: bugs, incorrect/nonstandard/partial implementation, or initially disabled. This class implements workarounds for known problems, where possible.
	/// 
	/// Known problematic applications:
	/// <list type="bullet">
	/// <item>
	/// Chrome web browser. Also Opera and other apps that use Chrome code; window class name is like "Chrome_WidgetWin_1".
	/// 
	/// Web page AOs initially are disabled (missing). Workarounds:
	/// Functions Find, Wait and FindAll enable it if used role prefix "web:" or "chrome:". Functions FromXY, FromMouse and Focused enable it if window class name starts with "Chrome". However Chrome does it lazily, therefore first time the functions often get wrong AO. Note: this auto-enabing may fail with future Chrome versions.
	/// Other ways to enable Chrome AOs: 1. Start Chrome with command line --force-renderer-accessibility. 2. In the future the script editor will have an option to enable Chrome AOs when it starts.
	/// 
	/// Some new web browser versions add new features or bugs that break something. AOs are especially vulnerable, because they are considered second-class citizens.
	/// </item>
	/// <item>
	/// Firefox web browser.
	/// 
	/// By default, the Find function is 50-100 times slower than it could be. Also for this reason the Wait function consumes much CPU. And HTML attributes may be unavailable. See <see cref="AFFlags.NotInProc"/>. Workaround: disable the Firefox multiprocess feature: open URL about:config, find browser.tabs.remote.autostart, set it = false, restart Firefox. If there is no such option, right-click and create it, as Boolean. If there are more than one similar options, set them all = false. Note: Firefox may reset it when upgrading or reinstalling, or even remove it in the future. If this does not work, google how to disable Firefox multiprocess.
	/// 
	/// When Firefox starts, its web page AOs are unavailable. It creates them only when somebody asks (eg function Find), but does it lazily, and Find at first fails. Workaround: use Wait, not Find.
	/// 
	/// Ocassionally Firefox briefly turns off its web page AOs. Workaround: use Wait, not Find. With other web browsers also it's better to use Wait.
	/// 
	/// Some new web browser versions add new features or bugs that break something. AOs are especially vulnerable, because they are considered second-class citizens.
	/// </item>
	/// <item>
	/// Edge web browser, JavaFX and other applications that don't have true accessible objects but have UI Automation elements.
	/// 
	/// To find AOs in these applications, need flag <see cref="AFFlags.UIA"/>.
	/// </item>
	/// <item>
	/// Java applications that use AWT/Swing (window class name starts with "SunAwt").
	/// 
	/// Not supported on 32-bit OS.
	/// 
	/// Must be enabled Java Access Bridge (JAB).
	/// If JAB is disabled or does not work, the "Find accessible object" tool shows an "enable" link when you try to capture something in a Java window. The link calls Au.Tools.Form_Acc.Java.EnableDisableJabUI. Or you can enable JAB in Control Panel -> Ease of Access Center -> Use the computer without a display. Or use jabswitch.exe. Then restart Java apps. Also may need to restart apps that tried to use Java AOs.
	/// 
	/// Your process must have the same 32/64 bitness as the installed Java. To remove this limitation, install Java 32-bit and 64-bit (they coexist).
	/// </item>
	/// <item>
	/// OpenOffice.
	/// 
	/// Often crashes after using AOs, usually when closing. Noticed in OpenOffice 4.1.4; may be fixed in newer versions.
	/// </item>
	/// <item>
	/// LibreOffice.
	/// 
	/// AOs are unavailable unless this process is 32-bit (when LibreOffice is 64-bit). Also need flag <see cref="AFFlags.NotInProc"/>.
	/// </item>
	/// <item>
	/// In some windows, AO of some controls are not connected to AO of parent control. Then Find cannot find them if searches in whole window.
	/// 
	/// Workaround: search only in that control. For example, use prop "class" or id". If it's a web browser control, use role prefix "web:". Or find the control with <see cref="Wnd.Child"/> and search in it. Or use <see cref="Acc.Finder.Find(Wnd, Wnd.ChildFinder)"/>.
	/// </item>
	/// <item>
	/// AOs of many standard Windows controls have bugs when they are retrieved without loading dll into the target process (see <see cref="AFFlags.NotInProc"/>).
	/// Known bugs: 1. Toolbar buttons don't have Name in some cases. 2. <see cref="Focus"/> and <see cref="Select"/> often don't work properly.
	/// 
	/// Workaround: don't use <see cref="AFFlags.NotInProc"/>, or use <see cref="AFFlags.UIA"/>.
	/// </item>
	/// <item>
	/// Function Find is much slower when cannot load dll into the target process. More info: <see cref="AFFlags.NotInProc"/>. Function Wait then consumes much more CPU.
	/// </item>
	/// <item>
	/// If the process has different 32/64 bitness than this process, to load the dll is launched rundll32.exe, which makes slower by about 50 ms first time.
	/// </item>
	/// <item>
	/// Currently this library does not support auto-scaled windows when using high DPI (text size 125%, 150% or more).
	/// If the target process is auto-scaled and this process isn't (or vice versa, or they have a different scaling factor), most coordinate-related functions don't work properly. For example, they get wrong AO rectangles.
	/// </item>
	/// </list>
	/// </remarks>
	/// <example>
	/// Click link "Example" in Chrome.
	/// <code><![CDATA[
	/// var w = +Wnd.Find("* Chrome");
	/// var a = +Acc.Find(w, "web:LINK", "Example");
	/// a.DoAction();
	/// ]]></code>
	/// Click a link, wait for new web page, click a link in it.
	/// <code><![CDATA[
	/// var w = +Wnd.Find("* Chrome");
	/// var a = Acc.Wait(1, w, "web:LINK", "Link 1");
	/// a.DoActionAndWaitForNewWebPage();
	/// a = Acc.Wait(10, w, "web:LINK", "Link 2");
	/// a.DoActionAndWaitForNewWebPage();
	/// ]]></code>
	/// </example>
	[StructLayout(LayoutKind.Sequential)]
	public unsafe partial class Acc :IDisposable
	{
		//FUTURE: Acc.Misc.EnableAccInChromeWebPagesWhenItStarts
		//FUTURE: Acc.Misc.EnableAccInJavaWindows (see JavaEnableJAB in QM2)
		//FUTURE: add functions to marshal AO to another thread.

		internal struct _Misc
		{
			public AccMiscFlags flags;
			public byte role; //for optimization. 0 if not set or failed to get or VT_BSTR or does not fit in BYTE.
			public ushort level; //for ToString. 0 if not set.

			public void SetRole(AccROLE role) { this.role = (byte)((uint)role <= 0xff ? role : 0); }
			public void SetLevel(int level) { this.level = (ushort)Math_.MinMax(level, 0, 0xffff); }
		}

		internal IntPtr _iacc;
		internal int _elem;
		internal _Misc _misc;
		//Real Acc object memory size with header: 32 bytes on 64-bit.
		//We don't use RCW<IAccessible>, which would add another 32 bytes.

		/// <summary>
		/// Creates Acc from IAccessible and child id.
		/// By default does not AddRef.
		/// iacc must not be Is0.
		/// </summary>
		internal Acc(IntPtr iacc, int elem = 0, bool addRef = false)
		{
			_Set(iacc, elem, default, addRef);
		}

		/// <summary>
		/// Creates Acc from Cpp_Acc.
		/// By default does not AddRef.
		/// x.acc must not be Is0.
		/// </summary>
		internal Acc(Cpp.Cpp_Acc x, bool addRef = false)
		{
			_Set(x.acc, x.elem, x.misc, addRef);
		}

		/// <summary>
		/// Sets fields.
		/// _iacc must be 0, iacc not 0.
		/// </summary>
		void _Set(IntPtr iacc, int elem = 0, _Misc misc = default, bool addRef = false)
		{
			Debug.Assert(_iacc == default);
			Debug.Assert(iacc != default);
			if(addRef) Marshal.AddRef(iacc);
			_iacc = iacc;
			_elem = elem;
			_misc = misc;

			int mp = _MemoryPressure;
			GC.AddMemoryPressure(mp);
			//s_dmp += mp; if(s_dmp > DebugMaxMemoryPressure) DebugMaxMemoryPressure = s_dmp;
			//DebugMemorySum += mp;
		}

		int _MemoryPressure => _elem == 0 ? c_memoryPressure : c_memoryPressure / 4;
		const int c_memoryPressure = 128;
		//Ideally this should be the average AO memory size.
		//Actually much more, maybe 500, if counting both processes, but:
		//	Release() does not delete the object if refcount!=0. Usually refcount==0, unless we have an AO and its simple elements (_elem!=0).
		//	It seems GC is too frequent when using AddMemoryPressure/RemoveMemoryPressure. In this case it is frequent enough.

		//internal static int DebugMaxMemoryPressure;
		//static int s_dmp;
		//internal static int DebugMemorySum;

		///
		protected virtual void Dispose(bool disposing)
		{
			if(_iacc != default) {
				var t = _iacc; _iacc = default;
				//Perf.First();
				//int rc =
				Marshal.Release(t);
				//Perf.NW();
				//Print($"rel: {Marshal.Release(t)}");
				//Print(t, _elem, rc);

				int mp = _MemoryPressure;
				GC.RemoveMemoryPressure(mp);
				//s_dmp -= mp;
			}
			_elem = 0;
			_misc = default;
		}

		/// <summary>
		/// Releases IAccessible COM object and clears this variable.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		///
		~Acc()
		{
			Dispose(false);
		}

		/// <summary>
		/// If x is not null, returns x, else throws <see cref="NotFoundException"/>.
		/// Alternatively you can use <see cref="ExtensionMethods.OrThrow(Acc)" r=""/>.
		/// </summary>
		/// <exception cref="NotFoundException">x is null.</exception>
		/// <example><inheritdoc cref="ExtensionMethods.OrThrow(Acc)"/></example>
		public static Acc operator +(Acc x) => x ?? throw new NotFoundException("Not found (Acc).");

		/// <summary>
		/// Gets or changes simple element id, also known as child id.
		/// </summary>
		/// <remarks>
		/// Most accessible objects (AO) are not simple elements. Then this property is 0.
		/// Often (but not always) this property is the 1-based item index in parent AO. For example LISTITEM in LIST.
		/// The 'set' function sometimes can be used as a fast alternative to <see cref="Navigate"/>. It modifies only this variable. It does not check whether the value is valid.
		/// Simple elements cannot have child AOs.
		/// </remarks>
		public int SimpleElementId { get => _elem; set { _misc.role = 0; _elem = value; } }

		/// <summary>
		/// Returns some additional info about this variable, such as how the accessible object was retrieved (inproc, UIA, Java).
		/// </summary>
		public AccMiscFlags MiscFlags => _misc.flags;

		/// <summary>
		/// Gets or sets indentation level for <see cref="ToString"/>.
		/// </summary>
		/// <remarks>
		/// When Find or similar function finds an accessible object, it sets this property of the Acc variable.
		/// When searching in a window or control, at level 0 are direct children of the WINDOW object of the window or control. When searching in Acc, at level 0 are direct children of the Acc. When searching in web page (role prefix "web:" etc), at level 0 is the web page object (role DOCUMENT or PANE).
		/// </remarks>
		public int Level { get => _misc.level; set => _misc.SetLevel(value); }

		/// <summary>
		/// Returns true if this variable is disposed.
		/// </summary>
		bool _Disposed => _iacc == default;
		//note: named not 'IsDisposed' because can be easily confused with IsDisabled.

		internal void LibThrowIfDisposed()
		{
			if(_Disposed) throw new ObjectDisposedException(nameof(Acc));
		}

		/// <summary>
		/// Gets accessible object of window or control or its standard part - client area, titlebar etc.
		/// Uses API <msdn>AccessibleObjectFromWindow</msdn>.
		/// </summary>
		/// <param name="w">Window or control.</param>
		/// <param name="objid">Window part id. Default AccOBJID.WINDOW. Also can be a custom id supported by that window, cast int to AccOBJID.</param>
		/// <param name="flags">Flags.</param>
		/// <exception cref="WndException">Invalid window.</exception>
		/// <exception cref="AuException">Failed. For example, window of a higher <conceptualLink target="e2645f42-9c3a-4d8c-8bef-eabba00c92e9">UAC</conceptualLink> integrity level process.</exception>
		/// <exception cref="ArgumentException"><paramref name="objid"/> is QUERYCLASSNAMEIDX or NATIVEOM.</exception>
		public static Acc FromWindow(Wnd w, AccOBJID objid = AccOBJID.WINDOW, AWFlags flags = 0)
		{
			bool spec = false;
			switch(objid) {
			case AccOBJID.QUERYCLASSNAMEIDX: //use WM_GETOBJECT
			case AccOBJID.NATIVEOM: //use API AccessibleObjectFromWindow
				throw new ArgumentException();
			case AccOBJID.CARET: //w should be 0
			case AccOBJID.CURSOR: //w should be 0
			case AccOBJID.ALERT: //only with AccessibleObjectFromEvent?
			case AccOBJID.SOUND: //only with AccessibleObjectFromEvent?
				spec = true; flags |= AWFlags.NotInProc;
				break;
			}

			var hr = Cpp.Cpp_AccFromWindow(flags.Has_(AWFlags.NotInProc) ? 1 : 0, w, objid, out var a, out _);
			if(hr != 0) {
				if(flags.Has_(AWFlags.NoThrow)) return null;
				if(spec && w.Is0) throw new AuException();
				w.ThrowIfInvalid();
				_WndThrow(hr, w, "*get accessible object from window.");
			}
			return new Acc(a);
		}

		static void _WndThrow(int hr, Wnd w, string es)
		{
			w.LibUacCheckAndThrow(es);
			throw new AuException(hr, es);
		}

		/// <summary>
		/// Gets accessible object from point.
		/// </summary>
		/// <param name="p">
		/// Coordinates relative to the primary screen.
		/// Tip: When need coordinates relative to another screen or/and the work area, use <see cref="Coord.Normalize"/> or tuple (x, y, workArea) etc. Example: <c>var a = Acc.FromXY((x, y, true));</c>. Also when need <see cref="Coord.Reverse"/> etc.
		/// </param>
		/// <param name="flags"></param>
		/// <exception cref="AuException">Failed. For example, window of a higher <conceptualLink target="e2645f42-9c3a-4d8c-8bef-eabba00c92e9">UAC</conceptualLink> integrity level process.</exception>
		/// <remarks>
		/// Uses API <msdn>AccessibleObjectFromPoint</msdn>.
		/// </remarks>
		public static Acc FromXY(POINT p, AXYFlags flags = 0)
		{
			for(int i = 0; ; i++) {
				var hr = Cpp.Cpp_AccFromPoint(p, flags, out var a);
				if(hr == 0) return new Acc(a);
				if(i < 2) continue;
				if(flags.Has_(AXYFlags.NoThrow)) return null;
				_WndThrow(hr, Wnd.FromXY(p, WXYFlags.Raw), "*get accessible object from point.");
			}
		}
		//rejected: FromXY(Coord, Coord, ...). Coord makes no sense; could be int int, but it's easy to create POINT from it.

		/// <summary>
		/// Gets accessible object from mouse cursor (pointer) position.
		/// Uses API <msdn>AccessibleObjectFromPoint</msdn>.
		/// </summary>
		/// <param name="flags"></param>
		/// <exception cref="AuException">Failed. For example, window of a higher <conceptualLink target="e2645f42-9c3a-4d8c-8bef-eabba00c92e9">UAC</conceptualLink> integrity level process.</exception>
		public static Acc FromMouse(AXYFlags flags = 0)
		{
			return FromXY(Mouse.XY, flags);
		}

		/// <summary>
		/// Gets the accessible object that has the keyboard focus.
		/// Returns null if fails.
		/// </summary>
		/// <param name="useUIAutomation">
		/// Use UI Automation API.
		/// Need this with windows that don't support accessible objects but support UI Automation elements. Can be used with most other windows too.
		/// More info: <see cref="AFFlags.UIA"/>.
		/// </param>
		public static Acc Focused(bool useUIAutomation = false)
		{
			var w = Wnd.WndFocused;
			g1:
			if(w.Is0) return null;
			int hr = Cpp.Cpp_AccGetFocused(w, useUIAutomation ? 1 : 0, out var a);
			if(hr != 0) {
				var w2 = Wnd.WndFocused;
				if(w2 != w) { w = w2; goto g1; }
				return null;
			}
			return new Acc(a);
			//FUTURE: wait, like FromXY.
		}

		/// <summary>
		/// Gets the accessible object that generated the event that is currently being processed by the callback function used with API <msdn>SetWinEventHook</msdn> or <see cref="Util.AccHook"/>.
		/// Returns null if failed. Suports <see cref="Native.GetError"/>.
		/// </summary>
		/// <param name="w"></param>
		/// <param name="idObject"></param>
		/// <param name="idChild"></param>
		/// <remarks>
		/// The parameters are of the callback function.
		/// Uses API <msdn>AccessibleObjectFromEvent</msdn>.
		/// Often fails because the object already does not exist, because the callback function is called asynchronously, especially when the event is OBJECT_DESTROY, OBJECT_HIDE, SYSTEM_*END.
		/// Returns null if failed. Always check the return value, to avoid NullReferenceException. An exception in the callback function kills this process.
		/// </remarks>
		public static Acc FromEvent(Wnd w, int idObject, int idChild)
		{
			int hr = Api.AccessibleObjectFromEvent(w, idObject, idChild, out var iacc, out var v);
			if(hr == 0 && iacc == default) hr = Api.E_FAIL;
			if(hr != 0) { Native.SetError(hr); return null; }
			int elem = v.vt == Api.VARENUM.VT_I4 ? v.ValueInt : 0;
			return new Acc(iacc, elem);
		}

#if false //rejected: not useful. Maybe in the future.
		/// <summary>
		/// Gets accessible object from a COM object of any type that supports it.
		/// Returns null if fails.
		/// </summary>
		/// <param name="x">Unmanaged COM object.</param>
		/// <remarks>
		/// The COM object type can be IAccessible, IAccessible2, IHTMLElement, ISimpleDOMNode or any other COM interface type that can give <msdn>IAccessible</msdn> interface pointer through API <msdn>IUnknown.QueryInterface</msdn> or <msdn>IServiceProvider.QueryService</msdn>.
		/// For IHTMLElement and ISimpleDOMNode returns null if the HTML element is not an accessible object. Then you can try to get accessible object of its parent HTML element, parent's parent and so on, until succeeds.
		/// </remarks>
		public static Acc FromComObject(IntPtr x)
		{
			if(x == default) return null;
			if(Util.Marshal_.QueryInterface(x, out IntPtr iacc, Api.IID_IAccessible)
				|| Util.Marshal_.QueryService(x, out iacc, Api.IID_IAccessible)
				) return new Acc(iacc);
			return null;
		}

		/// <summary>
		/// Gets accessible object from a COM object of any type that supports it.
		/// Returns null if fails.
		/// </summary>
		/// <param name="x">Managed COM object.</param>
		/// <remarks>
		/// The COM object type can be IAccessible, IAccessible2, IHTMLElement, ISimpleDOMNode or any other COM interface type that can give <msdn>IAccessible</msdn> interface pointer through API <msdn>IUnknown.QueryInterface</msdn> or <msdn>IServiceProvider.QueryService</msdn>.
		/// For IHTMLElement and ISimpleDOMNode returns null if the HTML element is not an accessible object. Then you can try to get accessible object of its parent HTML element, parent's parent and so on, until succeeds.
		/// </remarks>
		public static Acc FromComObject(object x)
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
		enum _FuncId { name = 1, value, description, default_action, role, state, rectangle, parent_object, child_object, container_window, child_count, child_objects, help_text, keyboard_shortcut, html, selection, uiaid }

		/// <summary>
		/// Calls Native.SetError and returns hr.
		/// In Debug config also outputs error in red.
		/// If hr looks like not an error but just the property or action is unavailable, changes it to S_FALSE and does not show error. These are: S_FALSE, DISP_E_MEMBERNOTFOUND, E_NOTIMPL.
		/// _FuncId also can be char, like (_FuncId)'n' for name.
		/// </summary>
		int _Hresult(_FuncId funcId, int hr)
		{
			if(hr != 0) {
				switch(hr) {
				case Api.DISP_E_MEMBERNOTFOUND: case Api.E_NOTIMPL: hr = Api.S_FALSE; break;
				case (int)Cpp.EError.InvalidParameter: throw new ArgumentException("Invalid argument value.");
				default: Debug.Assert(!Cpp.IsCppError(hr)); break;
				}
#if DEBUG
				if(hr != Api.S_FALSE) {
					_DebugPropGet(funcId, hr);
				}
#endif
			}
			Native.SetError(hr);
			return hr;
		}

#if DEBUG
		void _DebugPropGet(_FuncId funcId, int hr)
		{
			if(t_debugNoRecurse || _Disposed) return;

			if(funcId >= (_FuncId)'A') {
				switch((char)funcId) {
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
				}
			}

			if(hr == Api.E_FAIL && funcId == _FuncId.default_action) return; //many in old VS etc
			t_debugNoRecurse = true;
			try {
				var s = ToString();
				Print($"<><c 0xff>-{funcId}, 0x{hr:X} - {Native.GetErrorMessage(hr)}    {s}</c>");
			}
			finally { t_debugNoRecurse = false; }
		}
		[ThreadStatic] static bool t_debugNoRecurse;
#endif

		/// <summary>
		/// Formats string from main properties of this accessible object.
		/// </summary>
		/// <remarks>
		/// The string starts with role. Other properties have format like x="value", where x is a property character like with <see cref="GetProperties"/>; character e is <see cref="SimpleElementId"/>. HTML attributes have format @name="value". In string values are used C# escape sequences, for example \r\n for new line.
		/// Indentation depends on <see cref="Level"/>.
		/// </remarks>
		/// <seealso cref="PrintAll"/>
		public override string ToString()
		{
			if(_Disposed) return "<disposed>";
			if(!GetProperties("Rnsvdarw@", out var k)) return "<failed>";

			using(new Util.LibStringBuilder(out var b)) {
				if(Level > 0) b.Append(' ', Level);
				b.Append(k.Role);
				_Add('n', k.Name);
				if(k.State != 0) _Add('s', k.State.ToString(), '(', ')');
				_Add('v', k.Value);
				_Add('d', k.Description);
				_Add('a', k.DefaultAction);
				if(!k.Rect.Is0) _Add('r', k.Rect.ToString(), '\0', '\0');
				if(SimpleElementId != 0) b.Append(",  e=").Append(SimpleElementId);
				foreach(var kv in k.HtmlAttributes) {
					b.Append(",  @").Append(kv.Key).Append('=').Append('\"');
					b.Append(kv.Value.Escape_(limit: 250)).Append('\"');
				}
				_Add('w', k.WndContainer.ClassName ?? "");

				void _Add(char name, string value, char q1 = '\"', char q2 = '\"')
				{
					if(value.Length == 0) return;
					var t = value; if(q1 == '\"') t = t.Escape_(limit: 250);
					b.Append(",  ").Append(name).Append('=');
					if(q1 != '\0') b.Append(q1);
					b.Append(t);
					if(q1 != '\0') b.Append(q2);
				}

				return b.ToString();
			}
		}

		/// <summary>
		/// Displays properties of all found accessible objects of window w.
		/// </summary>
		/// <remarks>
		/// Uses <see cref="ToString"/>.
		/// Catches exceptions. On exception prints $"!exception! exceptionType exceptionMessage".
		/// Parameters are of <see cref="Find(Wnd, string, string, string, AFFlags, Func{Acc, bool}, int, Wnd.ChildFinder)"/>.
		/// By default skips invisible objects and objects in menus. Use flags to include them.
		/// Chrome web page accessible objects normally are disabled (missing) when it starts. Use role prefix "web:" or "chrome:" to enable. See example.
		/// </remarks>
		/// <example>
		/// Displays visible accessible objects in Chrome web page.
		/// <code><![CDATA[
		/// Output.Clear();
		/// var w = +Wnd.Find("* Chrome");
		/// Print("---- all ----");
		/// Acc.PrintAll(w, "web:");
		/// Print("---- links ----");
		/// Acc.PrintAll(w, "web:LINK");
		/// ]]></code>
		/// </example>
		public static void PrintAll(Wnd w, string role = null, AFFlags flags = 0, string prop = null)
		{
			try {
				Find(w, role, null, prop, flags, also: o => { Print(o); return false; });
			}
			catch(Exception ex) { Print($"!exception! {ex.GetType().Name} {ex.Message}"); }
		}
	}
}