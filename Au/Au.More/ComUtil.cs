/// <summary>
/// COM utility functions.
/// </summary>
public static class ComUtil {
	/// <summary>
	/// Gets an interface pointer to a COM object existing in other process and registered in ROT.
	/// </summary>
	/// <param name="progID">The programmatic identifier (ProgID) of the COM type.</param>
	/// <param name="dontThrow">If fails, don't throw exception but return null.</param>
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
	/// using Excel = Microsoft.Office.Interop.Excel;
	/// var app = (Excel.Application)ComUtil.GetActiveObject("Excel.Application");
	/// ]]></code>
	/// <code><![CDATA[
	/// dynamic app2 = ComUtil.GetActiveObject("Excel.Application");
	/// ]]></code>
	/// </example>
	public static object GetActiveObject(string progID, bool dontThrow = false) {
		int hr = Api.CLSIDFromProgID(progID, out var clsid);
		if (hr < 0) return dontThrow ? null : throw Marshal.GetExceptionForHR(hr);
		return GetActiveObject(clsid, dontThrow);
	}
	
	/// <inheritdoc cref="GetActiveObject(string, bool)"/>
	/// <exception cref="COMException">An object of this type currently is unavailable. Probably its program is not running, or running with a different UAC integrity level.</exception>
	public static object GetActiveObject(in Guid clsid, bool dontThrow = false) {
		int hr = Api.GetActiveObject(clsid, default, out var r);
		if (hr < 0) return dontThrow ? null : throw Marshal.GetExceptionForHR(hr);
		return r;
	}
	
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
}
