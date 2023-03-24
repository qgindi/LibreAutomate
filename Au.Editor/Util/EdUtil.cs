
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

record struct StartEndText(int start, int end, string text) {
	public int Length => end - start;
	public Range Range => start..end;
	
	/// <summary>
	/// Replaces all text ranges specified in <i>a</i> with strings specified in <i>a</i>.
	/// </summary>
	public static string ReplaceAll(string s, List<StartEndText> a) {
		StringBuilder b = null;
		ReplaceAll(s, a, ref b);
		return b.ToString();
	}
	
	/// <summary>
	/// Replaces all text ranges specified in <i>a</i> with strings specified in <i>a</i>.
	/// </summary>
	/// <param name="b">Receives new text. If null, the function creates new, else at first calls <c>b.Clear()</c>.</param>
	public static void ReplaceAll(string s, List<StartEndText> a, ref StringBuilder b) {
		int cap = s.Length - a.Sum(o => o.Length) + a.Sum(o => o.text.Length);
		if (b == null) b = new(cap);
		else {
			b.Clear();
			b.EnsureCapacity(cap);
		}
		
		int i = 0;
		foreach (var v in a) {
			b.Append(s, i, v.start - i).Append(v.text);
			i = v.end;
		}
		b.Append(s, i, s.Length - i);
	}
}
