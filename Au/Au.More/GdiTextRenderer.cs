//FUTURE: support columns. Maybe use \t of specified widths.

namespace Au.More;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Draws text using fastest GDI API such as TextOut and standard UI font.
/// Can easily draw string parts with different colors/styles without measuring.
/// Must be disposed.
/// </summary>
public unsafe class GdiTextRenderer : IDisposable {
	IntPtr _dc, _oldFont;
	uint _oldAlign;
	int _color, _oldColor;
	int _dpi;
	bool _releaseDC;
	
	/// <summary>Object created with this ctor can draw and measure.</summary>
	/// <param name="hdc">Device context handle. <b>Dispose</b> will not release it.</param>
	/// <param name="dpi"></param>
	public GdiTextRenderer(IntPtr hdc, int dpi) {
		_dpi = dpi;
		_dc = hdc;
		_oldFont = Api.SelectObject(_dc, NativeFont_.RegularCached(_dpi));
		_oldAlign = 0xffffffff;
		Api.SetBkMode(_dc, 1);
		_oldColor = Api.SetTextColor(_dc, _color = 0);
	}
	
	/// <summary>Object created with this ctor can measure only. Uses screen DC.</summary>
	public GdiTextRenderer(int dpi) {
		_dpi = dpi;
		_releaseDC = true;
		_dc = Api.GetDC(default);
		_oldFont = Api.SelectObject(_dc, NativeFont_.RegularCached(_dpi));
	}
	
	public void Dispose() {
		if (_dc != default) {
			Api.SelectObject(_dc, _oldFont);
			if (_releaseDC) Api.ReleaseDC(default, _dc);
			else {
				if (_oldAlign != 0xffffffff) Api.SetTextAlign(_dc, _oldAlign);
				if (_oldColor != _color) Api.SetTextColor(_dc, _oldColor);
			}
			_dc = default;
		}
		//never mind: we don't restore the old current position. Nobody later would draw at current position without movetoex. As well as bkmode.
	}
	
	/// <summary>
	/// Sets the current drawing position of the DC.
	/// Returns previous position.
	/// </summary>
	public POINT MoveTo(int x, int y) {
		Api.MoveToEx(_dc, x, y, out var p);
		return p;
	}
	
	/// <summary>
	/// Gets the current drawing position of the DC.
	/// </summary>
	public POINT GetCurrentPosition() {
		Api.GetCurrentPositionEx(_dc, out var p);
		return p;
	}
	
	/// <summary>
	/// Sets non-bold font.
	/// </summary>
	public void FontNormal() => Api.SelectObject(_dc, NativeFont_.RegularCached(_dpi));
	
	/// <summary>
	/// Sets bold font.
	/// </summary>
	public void FontBold() => Api.SelectObject(_dc, NativeFont_.BoldCached(_dpi));
	
	//public void FontItalic() => Api.SelectObject(_dc, NativeFont_.ItalicCached(_dpi));
	
	//public void FontBoldItalic() => Api.SelectObject(_dc, NativeFont_.BoldItalicCached(_dpi));
	
	/// <summary>
	/// Draws text at the current drawing position of the DC, and updates it.
	/// </summary>
	/// <param name="color">Text color 0xBBGGRR.</param>
	/// <param name="backColor">Background color 0xBBGGRR. Transparent if null.</param>
	public void DrawText(string s, int color = 0, Range? range = null, int? backColor = null) {
		var (from, len) = range.GetOffsetAndLength(s.Lenn()); if (len == 0) return;
		if (_oldAlign == 0xffffffff) _oldAlign = Api.SetTextAlign(_dc, 1); //TA_UPDATECP
		_DrawText(s, 0, 0, color, from, len, backColor);
	}
	
	/// <summary>
	/// Draws text at specified position. Does not use/update the current drawing position of the DC.
	/// </summary>
	/// <param name="color">Text color 0xBBGGRR.</param>
	/// <param name="backColor">Background color 0xBBGGRR. Transparent if null.</param>
	public void DrawText(string s, POINT p, int color = 0, Range? range = null, int? backColor = null) {
		var (from, len) = range.GetOffsetAndLength(s.Lenn()); if (len == 0) return;
		if (_oldAlign != 0xffffffff) { Api.SetTextAlign(_dc, _oldAlign); _oldAlign = 0xffffffff; }
		_DrawText(s, p.x, p.y, color, from, len, backColor);
	}
	
	/// <summary>
	/// Draws text clipped in specified rectangle. Does not use/update the current drawing position of the DC.
	/// </summary>
	/// <param name="color">Text color 0xBBGGRR.</param>
	/// <param name="backColor">Background color 0xBBGGRR. Transparent if null.</param>
	public void DrawText(string s, in RECT r, int color = 0, Range? range = null, int? backColor = null) {
		var (from, len) = range.GetOffsetAndLength(s.Lenn()); if (len == 0) return;
		if (_oldAlign != 0xffffffff) { Api.SetTextAlign(_dc, _oldAlign); _oldAlign = 0xffffffff; }
		_DrawText(s, r, color, from, len, backColor);
	}
	
	unsafe void _DrawText(string s, int x, int y, int color, int from, int len, int? backColor) {
		if (color != _color) Api.SetTextColor(_dc, _color = color);
		using var bc = new _BackColor(_dc, backColor);
		fixed (char* p = s) Api.TextOut(_dc, x, y, p + from, len);
	}
	
	unsafe void _DrawText(string s, in RECT r, int color, int from, int len, int? backColor) {
		if (color != _color) Api.SetTextColor(_dc, _color = color);
		using var bc = new _BackColor(_dc, backColor);
		fixed (char* p = s) Api.ExtTextOut(_dc, r.left, r.top, Api.ETO_CLIPPED, r, p + from, len);
	}
	
	public SIZE MeasureText(string s, Range? range = null) {
		var (from, len) = range.GetOffsetAndLength(s.Lenn()); if (len == 0) return default;
		fixed (char* p = s) {
			Api.GetTextExtentPoint32(_dc, p + from, len, out var z);
			return z;
		}
	}
	
	ref struct _BackColor {
		int _oldColor, _oldMode;
		IntPtr _dc;
		
		public _BackColor(IntPtr dc, int? color) {
			if (color != null) {
				_dc = dc;
				_oldMode = Api.SetBkMode(_dc, 2);
				_oldColor = Api.SetBkColor(_dc, color.Value);
			}
		}
		
		public void Dispose() {
			if (_dc != default) {
				Api.SetBkColor(_dc, _oldColor);
				Api.SetBkMode(_dc, _oldMode);
			}
		}
	}
}
