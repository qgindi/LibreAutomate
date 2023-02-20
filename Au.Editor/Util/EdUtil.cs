
/// <summary>
/// Misc util functions.
/// </summary>
static class EdUtil {

}

/// <summary>
/// Can be used to return bool and error text. Has implicit conversions from/to bool.
/// </summary>
record struct BoolError(bool ok, string error) {
	public static implicit operator bool(BoolError x) => x.ok;
	public static implicit operator BoolError(bool ok) => new(ok, ok ? null : "Failed.");
}

#if DEBUG

static class EdDebug {
}

#endif

/// <summary>
/// Temporarily disables window redrawing.
/// Ctor sends WM_SETREDRAW(0) if visible.
/// If was visible, Dispose sends WM_SETREDRAW(1) and calls RedrawWindow.
/// </summary>
struct WndSetRedraw : IDisposable {
	wnd _w;

	public WndSetRedraw(wnd w) {
		_w = w;
		if (_w.IsVisible) _w.Send(Api.WM_SETREDRAW, 0); else _w = default;
	}

	public unsafe void Dispose() {
		if (_w.Is0) return;
		_w.Send(Api.WM_SETREDRAW, 1);
		Api.RedrawWindow(_w, flags: Api.RDW_ERASE | Api.RDW_FRAME | Api.RDW_INVALIDATE | Api.RDW_ALLCHILDREN);
		_w = default;
	}
}
