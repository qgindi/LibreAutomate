namespace Au.More;

/// <summary>
/// Helps to get and release screen DC with <c>using</c>.
/// Uses API GetDC and ReleaseDC.
/// </summary>
struct ScreenDC_ : IDisposable {
	IntPtr _dc;

	public ScreenDC_() => _dc = Api.GetDC(default);

	public static implicit operator IntPtr(ScreenDC_ dc) => dc._dc;

	public void Dispose() {
		if (_dc != default) {
			Api.ReleaseDC(default, _dc);
			_dc = default;
		}
	}
}

/// <summary>
/// Helps to get and release window DC with <c>using</c>.
/// Uses API GetDC and ReleaseDC.
/// </summary>
struct WindowDC_ : IDisposable {
	IntPtr _dc;
	wnd _w;

	public WindowDC_(IntPtr dc, wnd w) { _dc = dc; _w = w; }

	public WindowDC_(wnd w) => _dc = Api.GetDC(_w = w);

	public static implicit operator IntPtr(WindowDC_ dc) => dc._dc;

	public bool Is0 => _dc == default;

	public void Dispose() {
		if (_dc != default) {
			Api.ReleaseDC(_w, _dc);
			_dc = default;
		}
	}
}

/// <summary>
/// Helps to create and delete compatible DC (memory DC) with <c>using</c>.
/// Uses API CreateCompatibleDC and DeleteDC.
/// </summary>
class MemoryDC_ : IDisposable {
	protected IntPtr _dc;

	/// <summary>
	/// Creates memory DC compatible with screen.
	/// </summary>
	public MemoryDC_() : this(default) { }

	public MemoryDC_(IntPtr dc) => _dc = Api.CreateCompatibleDC(dc);

	public static implicit operator IntPtr(MemoryDC_ dc) => dc._dc;

	public bool Is0 => _dc == default;

	public void Dispose() => Dispose(true);

	protected virtual void Dispose(bool disposing) {
		if (_dc != default) {
			Api.DeleteDC(_dc);
			_dc = default;
		}
	}
}

/// <summary>
/// Memory DC with selected font.
/// Can be used for font measurement.
/// </summary>
sealed class FontDC_ : MemoryDC_ {
	IntPtr _oldFont;

	/// <summary>
	/// Selects specified font.
	/// The <b>Dispose</b> method will select it out but will not destroy it.
	/// </summary>
	/// <param name="font"></param>
	public FontDC_(IntPtr font) {
		_oldFont = Api.SelectObject(_dc, font);
	}

	/// <summary>
	/// Selects standard UI font for specified DPI.
	/// </summary>
	/// <param name="dpi"></param>
	public FontDC_(DpiOf dpi) : this(NativeFont_.RegularCached(dpi)) { }

	protected override void Dispose(bool disposing) {
		if (_oldFont != default) {
			Api.SelectObject(_dc, _oldFont);
			_oldFont = default;
		}
		base.Dispose(disposing);
	}

	/// <summary>
	/// Measures text with API <msdn>GetTextExtentPoint32</msdn>.
	/// Should be single line without tabs. For drawing with API <msdn>TextOut</msdn> or <msdn>ExtTextOut</msdn>.
	/// </summary>
	public SIZE MeasureEP(string s) {
		Api.GetTextExtentPoint32(_dc, s, s.Length, out var z);
		return z;
	}

	/// <summary>
	/// Measures text with API <msdn>DrawTextEx</msdn>.
	/// Can be multiline. For drawing with API <msdn>DrawTextEx</msdn>.
	/// </summary>
	public SIZE MeasureDT(RStr s, TFFlags format, int wrapWidth = 0) {
		if (s.Length == 0) return default;
		RECT r = new(0, 0, wrapWidth, 0);
		Api.DrawText(_dc, s, ref r, format | TFFlags.CALCRECT);
		return new(r.Width + 1, r.Height);
		//When drawing, may cut 1 pixel at the right, eg if text ends with T. Workaround: now add 1.
	}
}

struct GdiObject_ : IDisposable {
	IntPtr _h;

	public IntPtr Handle => _h;

	public GdiObject_(IntPtr handle) {
		_h = handle;
	}

	public void Dispose() {
		Api.DeleteObject(_h);
		_h = default;
	}

	public static implicit operator IntPtr(GdiObject_ g) => g._h;

	/// <summary>
	/// Calls API <b>CreateSolidBrush</b>.
	/// </summary>
	/// <param name="color">0xRRGGBB.</param>
	public static GdiObject_ ColorBrush(ColorInt color) {
		return new(Api.CreateSolidBrush(color.ToBGR()));
	}

	/// <summary>
	/// Calls API <b>GetThemeSysColorBrush</b>.
	/// </summary>
	/// <param name="hTheme">Theme handle. If default, gets non-themed color.</param>
	/// <param name="colorIndex">API <b>COLOR_x</b>.</param>
	public static GdiObject_ SysColorBrush(IntPtr hTheme, int colorIndex) {
		return new(Api.GetThemeSysColorBrush(hTheme, colorIndex));
	}

	public void BrushFill(IntPtr dc, RECT r) {
		Api.FillRect(dc, r, _h);
	}

	public void BrushRect(IntPtr dc, RECT r) {
		Api.FrameRect(dc, r, _h);
	}
}

struct GdiPen_ : IDisposable {
	IntPtr _pen;

	public IntPtr Handle => _pen;

	public GdiPen_(int color, int width = 1, int style = 0) {
		_pen = Api.CreatePen(style, width, color);
	}

	public void Dispose() {
		Api.DeleteObject(_pen);
		_pen = default;
	}

	/// <summary>
	/// Draws line and returns previous "current position".
	/// Don't need to select pen into DC.
	/// </summary>
	public POINT DrawLine(IntPtr dc, POINT start, POINT end) {
		var old = Api.SelectObject(dc, _pen); //fast
		Api.MoveToEx(dc, start.x, start.y, out var p); //fast
		Api.LineTo(dc, end.x, end.y);
		Api.SelectObject(dc, old); //fast
		return p;
	}
}

struct GdiSelectObject_ : IDisposable {
	IntPtr _dc, _old;

	public GdiSelectObject_(IntPtr dc, IntPtr obj) {
		_old = Api.SelectObject(_dc = dc, obj);
	}

	public void Dispose() {
		Api.SelectObject(_dc, _old);
		_dc = default;
	}
}
