/// <summary>
/// COM utility functions.
/// </summary>
public static class ComUtil {
	/// <summary>
	/// Gets an interface pointer to a COM object existing in other process and registered in ROT.
	/// </summary>
	/// <param name="progID">The programmatic identifier (ProgID) of the object.</param>
	/// <exception cref="COMException">
	/// <br/>• <i>progID</i> not found in the registry. Probably it is incorrect, or the program isn't installed.
	/// <br/>• An object of this type currently is unavailable. Probably its program is not running, or running with a different UAC integrity level.
	/// </exception>
	/// <remarks>
	/// Calls API <msdn>GetActiveObject</msdn>.
	///
	/// This process must have the same [](xref:uac) integrity level as the target process (of the COM object). In Properties select uac user.
	/// </remarks>
	/// <seealso cref="Marshal.BindToMoniker(string)"/>
	public static object GetActiveObject(string progID) {
		Marshal.ThrowExceptionForHR(Api.CLSIDFromProgID(progID, out var clsid));
		return GetActiveObject(clsid);
	}
	
	/// <inheritdoc cref="GetActiveObject(string)"/>
	/// <exception cref="COMException">An object of this type currently is unavailable. Probably its program is not running, or running with a different UAC integrity level.</exception>
	public static object GetActiveObject(in Guid clsid) {
		Marshal.ThrowExceptionForHR(Api.GetActiveObject(clsid, default, out var r));
		return r;
	}
	
	/// <summary>
	/// Like <see cref="GetActiveObject(string)"/>, but does not throw exceptions.
	/// </summary>
	public static bool TryGetActiveObject<T>(string progID, out T r) where T : class {
		if (Api.CLSIDFromProgID(progID, out var clsid) != 0) { r = null; return false; };
		return TryGetActiveObject(clsid, out r);
	}
	
	/// <summary>
	/// Like <see cref="GetActiveObject(in Guid)"/>, but does not throw exceptions.
	/// </summary>
	public static bool TryGetActiveObject<T>(in Guid clsid, out T r) where T : class {
		if (Api.GetActiveObject(clsid, default, out var v) == 0) {
			if (v is T t) { r = t; return true; }
			Marshal.ReleaseComObject(v);
		}
		r = null; return false;
	}
}
