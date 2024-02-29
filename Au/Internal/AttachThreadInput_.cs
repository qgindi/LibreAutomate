namespace Au.More;

/// <summary>
/// Calls API <msdn>AttachThreadInput</msdn> to attach/detach thread input.
/// Constructor attaches thread input of this thread to that of the specified thread. <b>Dispose</b> detaches.
/// </summary>
internal struct AttachThreadInput_ : IDisposable {
	int _tidThis, _tidAttach;
	
	/// <summary>
	/// Attaches thread input of this thread to that of the specified thread.
	/// </summary>
	public AttachThreadInput_(int idThreadAttachTo, out bool succeeded) {
		_tidThis = Api.GetCurrentThreadId();
		succeeded = Api.AttachThreadInput(_tidThis, idThreadAttachTo, true);
		_tidAttach = succeeded ? idThreadAttachTo : 0;
	}
	
	/// <summary>
	/// Detaches thread input.
	/// </summary>
	public void Dispose() {
		if (_tidAttach != 0) {
			Api.AttachThreadInput(_tidThis, _tidAttach, false);
			_tidAttach = 0;
		}
	}
	
	/// <summary>
	/// Returns <c>true</c> if AttachThreadInput succeeded and this variable is not disposed.
	/// </summary>
	public bool IsAttached => _tidAttach != 0;
}
