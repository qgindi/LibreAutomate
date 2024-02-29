
using Microsoft.Win32.SafeHandles;

namespace Au.More;

/// <summary>
/// Manages a kernel handle.
/// Must be disposed.
/// Has static functions to open process handle.
/// </summary>
internal struct Handle_ : IDisposable {
	IntPtr _h;

	/// <summary>
	/// Attaches a kernel handle to this new variable.
	/// No exception when handle is invalid.
	/// If handle == -1, sets 0.
	/// </summary>
	/// <param name="handle"></param>
	public Handle_(nint handle) { _h = handle == -1 ? default : handle; }

	//public static explicit operator Handle_(IntPtr p) => new Handle_(p); //no

	public static implicit operator IntPtr(Handle_ p) => p._h;

	/// <summary>
	/// _h == default.
	/// Info: _h never is -1.
	/// </summary>
	public bool Is0 => _h == default;

	///
	public void Dispose() {
		if (!Is0) { Api.CloseHandle(_h); _h = default; }
	}

	/// <summary>
	/// Opens process handle.
	/// Calls API OpenProcess.
	/// </summary>
	/// <returns>default if failed. Supports <see cref="lastError"/>.</returns>
	/// <param name="processId">Process id.</param>
	/// <param name="desiredAccess">Desired access (Api.PROCESS_), as documented in MSDN -> OpenProcess.</param>
	public static Handle_ OpenProcess(int processId, uint desiredAccess = Api.PROCESS_QUERY_LIMITED_INFORMATION) {
		if (processId == 0) { lastError.code = Api.ERROR_INVALID_PARAMETER; return default; }
		return _OpenProcess(processId, desiredAccess);
	}

	/// <summary>
	/// Opens window's process handle.
	/// This overload is more powerful: if API OpenProcess fails, it tries API GetProcessHandleFromHwnd, which can open higher integrity level processes, but only if current process is uiAccess and desiredAccess includes only PROCESS_DUP_HANDLE, PROCESS_VM_OPERATION, PROCESS_VM_READ, PROCESS_VM_WRITE, SYNCHRONIZE.
	/// </summary>
	/// <returns>default if failed. Supports <see cref="lastError"/>.</returns>
	/// <param name="w"></param>
	/// <param name="desiredAccess">Desired access (Api.PROCESS_), as documented in MSDN -> OpenProcess.</param>
	public static Handle_ OpenProcess(wnd w, uint desiredAccess = Api.PROCESS_QUERY_LIMITED_INFORMATION) {
		int pid = w.ProcessId; if (pid == 0) return default;
		return _OpenProcess(pid, desiredAccess, w);
	}

	static Handle_ _OpenProcess(int processId, uint desiredAccess = Api.PROCESS_QUERY_LIMITED_INFORMATION, wnd processWindow = default) {
		Handle_ R = Api.OpenProcess(desiredAccess, false, processId);
		if (R.Is0 && !processWindow.Is0 && 0 == (desiredAccess & ~(Api.PROCESS_DUP_HANDLE | Api.PROCESS_VM_OPERATION | Api.PROCESS_VM_READ | Api.PROCESS_VM_WRITE | Api.SYNCHRONIZE))) {
			int e = lastError.code;
			if (uacInfo.ofThisProcess.IsUIAccess) R = Api.GetProcessHandleFromHwnd(processWindow);
			if (R.Is0) Api.SetLastError(e);
		}
		return R;
	}
}

/// <summary>
/// Kernel handle that is derived from WaitHandle.
/// When don't need to wait, use <see cref="Handle_"/>, it's more lightweight and has more creation methods.
/// </summary>
internal class WaitHandle_ : WaitHandle {
	public WaitHandle_(IntPtr nativeHandle, bool ownsHandle) {
		base.SafeWaitHandle = new SafeWaitHandle(nativeHandle, ownsHandle);
	}

	/// <summary>
	/// Opens process handle.
	/// Returns <c>null</c> if failed.
	/// </summary>
	/// <param name="pid"></param>
	/// <param name="desiredAccess"></param>
	public static WaitHandle_ FromProcessId(int pid, uint desiredAccess) {
		WaitHandle_ wh = null;
		try { wh = new WaitHandle_(Handle_.OpenProcess(pid, desiredAccess), true); }
		catch (Exception ex) { Debug_.Print(ex); }
		return wh;
	}
}
