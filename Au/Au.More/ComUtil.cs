namespace Au.More;

/// <summary>
/// COM utility functions.
/// </summary>
public static class ComUtil {
	/// <summary>
	/// Gets a COM object existing in other process and registered in ROT.
	/// </summary>
	/// <param name="progID">ProgID of the COM class. Example: <c>"Excel.Application"</c>.</param>
	/// <param name="dontThrow">If fails, don't throw exception but return <c>null</c>.</param>
	/// <exception cref="COMException">
	/// <br/>• <i>progID</i> not found in the registry. Probably it is incorrect, or the program isn't installed.
	/// <br/>• An object of this type currently is unavailable. Probably the program is not running, or running with a different UAC integrity level.
	/// </exception>
	/// <remarks>
	/// Calls API <msdn>GetActiveObject</msdn>.
	///
	/// This process must have the same [](xref:uac) integrity level as the target process (of the COM object). In script Properties select uac user.
	/// </remarks>
	/// <seealso cref="Marshal.BindToMoniker(string)"/>
	/// <example>
	/// <code><![CDATA[
	/// /*/ uac user; com Outlook 9.6 #ed6988d3.dll; /*/
	/// using Outlook = Microsoft.Office.Interop.Outlook;
	/// var app = (Outlook.Application)ComUtil.GetActiveObject("Outlook.Application");
	/// print.it(app.ActiveExplorer().CurrentFolder.Name);
	/// ]]></code>
	/// </example>
	public static object GetActiveObject(string progID, bool dontThrow = false) {
		int hr = Api.CLSIDFromProgID(progID, out var clsid);
		if (hr < 0) return dontThrow ? null : throw Marshal.GetExceptionForHR(hr);
		return GetActiveObject(clsid, dontThrow);
	}

	/// <exception cref="COMException">An object of this type currently is unavailable. Probably its program is not running, or running with a different UAC integrity level.</exception>
	public static object GetActiveObject(in Guid clsid, bool dontThrow = false) {
		int hr = Api.GetActiveObject(clsid, default, out var r);
		if (hr < 0) return dontThrow ? null : throw Marshal.GetExceptionForHR(hr);
		return r;
	}

	/// <summary>
	/// Gets COM object from a window using API <msdn>AccessibleObjectFromWindow</msdn>(OBJID_NATIVEOM, IID_IDispatch).
	/// </summary>
	/// <param name="w">Window or control.</param>
	/// <param name="cnChild">Child window class name. Format: [wildcard expression](xref:wildcard_expression). If used, gets COM object from the first found child or descendant window where it succeeds. If <c>null</c>, gets COM object from <i>w</i>.</param>
	/// <param name="dontThrow">If fails to get COM object, don't throw exception but return <c>null</c>.</param>
	/// <exception cref="AuWndException"><i>w</i> 0 or invalid.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <example>
	/// Microsoft Excel.
	/// <code><![CDATA[
	/// /*/ com Excel 1.9 #9fdf46bf.dll; /*/
	/// using Excel = Microsoft.Office.Interop.Excel;
	/// var w = wnd.find(0, null, "XLMAIN", "EXCEL.EXE");
	/// Excel.Workbook book = ((Excel.Window)ComUtil.GetWindowObject(w, "EXCEL7")).Parent;
	/// print.it(book.Name);
	/// ]]></code>
	/// Microsoft Word.
	/// <code><![CDATA[
	/// /*/ com Word 8.7 #6a6b0205.dll; /*/
	/// using Word = Microsoft.Office.Interop.Word;
	/// var w = wnd.find(0, null, "OpusApp", "WINWORD.EXE");
	/// Word.Document doc = ((Word.Window)ComUtil.GetWindowObject(w, "_WwG")).Parent;
	/// print.it(doc.Name);
	/// ]]></code>
	/// Microsoft PowerPoint.
	/// <code><![CDATA[
	/// /*/ com PowerPoint 2.12 #fdf81915.dll; /*/
	/// using PowerPoint = Microsoft.Office.Interop.PowerPoint;
	/// var w = wnd.find(0, "*PowerPoint", null, "POWERPNT.EXE");
	/// PowerPoint.Presentation doc = ((PowerPoint.DocumentWindow)ComUtil.GetWindowObject(w, "**m mdiClass||paneClassDC")).Parent;
	/// print.it(doc.Name);
	/// ]]></code>
	/// Microsoft Access.
	/// <code><![CDATA[
	/// /*/ com Access 9.0 #cdda93ea.dll; /*/
	/// using Access = Microsoft.Office.Interop.Access;
	/// var w = wnd.find(0, null, "OMain", "MSACCESS.EXE");
	/// Access.Application app = (Access.Application)ComUtil.GetWindowObject(w);
	/// print.it(app.CurrentProject.Name);
	/// ]]></code>
	/// To get COM object from Microsoft Outlook or Publisher, use <see cref="GetActiveObject(string, bool)"/>.
	/// </example>
	public static object GetWindowObject(wnd w, string cnChild = null, bool dontThrow = false) {
		w.ThrowIfInvalid();
		if (cnChild != null) {
			foreach (var c in w.ChildAll(cn: cnChild)) {
				if (0 == Api.AccessibleObjectFromWindow(c, EObjid.NATIVEOM, IID_IDispatch, out var o)) return o;
			}
		} else {
			if (0 == Api.AccessibleObjectFromWindow(w, EObjid.NATIVEOM, IID_IDispatch, out var o)) return o;
		}
		if (!dontThrow) throw new AuException($"*get COM object from window {w}");
		return null;
	}
	static readonly Guid IID_IDispatch = new("00020400-0000-0000-C000-000000000046");

	/// <summary>
	/// Creates COM object using ProgID.
	/// </summary>
	/// <param name="progID">The programmatic identifier (ProgID) of the COM type.</param>
	/// <exception cref="Exception"></exception>
	/// <remarks>
	/// Use this function when you don't have the COM interface definition or the interop assembly. Else use code like <c>var x = new InterfaceType();</c> or <c>var x = new CoclassType() as InterfaceType</c>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// dynamic app = ComUtil.CreateObject("Excel.Application");
	/// app.Visible = true;
	/// 3.s();
	/// app.Quit();
	/// ]]></code>
	/// </example>
	public static object CreateObject(string progID) {
		return Activator.CreateInstance(Type.GetTypeFromProgID(progID, throwOnError: true));
	}

	/// <summary>
	/// Creates COM object using CLSID.
	/// </summary>
	/// <exception cref="Exception"></exception>
	public static object CreateObject(in Guid clsid) {
		return Activator.CreateInstance(Type.GetTypeFromCLSID(clsid, throwOnError: true));
	}

	/// <summary>
	/// Default value for optional parameters of type <b>VARIANT</b> (<b>object</b> in C#) of COM functions.
	/// The same as <see cref="System.Reflection.Missing.Value"/>.
	/// </summary>
	public static readonly System.Reflection.Missing Missing = System.Reflection.Missing.Value;
}
