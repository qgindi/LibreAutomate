
namespace Au.Controls
{
	using static Sci;

	public unsafe partial class KScintilla
	{
		#region styles

		public void zStyleFont(int style, string name) {
			zSetString(SCI_STYLESETFONT, style, name);
		}

		//public string zStyleFont(int style)
		//{
		//	return zGetString(SCI_STYLEGETFONT, style, 100);
		//}

		public void zStyleFont(int style, string name, int size) {
			zStyleFont(style, name);
			zStyleFontSize(style, size);
		}

		/// <summary>Uses only font name and size. Not style etc.</summary>
		public void zStyleFont(int style, System.Windows.Controls.Control c) {
			zStyleFont(style, c.FontFamily.ToString(), c.FontSize.ToInt() * 72 / 96);
		}

		/// <summary>Segoe UI, 9.</summary>
		public void zStyleFont(int style) {
			zStyleFont(style, "Segoe UI", 9);
		}

		public void zStyleFontSize(int style, int value) {
			Call(SCI_STYLESETSIZE, style, value);
		}

		//public int zStyleFontSize(int style)
		//{
		//	return Call(SCI_STYLEGETSIZE, style);
		//}

		public void zStyleHidden(int style, bool value) {
			Call(SCI_STYLESETVISIBLE, style, !value);
		}

		//public bool zStyleHidden(int style)
		//{
		//	return 0 == Call(SCI_STYLEGETVISIBLE, style);
		//}

		public void zStyleBold(int style, bool value) {
			Call(SCI_STYLESETBOLD, style, value);
		}

		public void zStyleItalic(int style, bool value) {
			Call(SCI_STYLESETITALIC, style, value);
		}

		public void zStyleUnderline(int style, bool value) {
			Call(SCI_STYLESETUNDERLINE, style, value);
		}

		public void zStyleEolFilled(int style, bool value) {
			Call(SCI_STYLESETEOLFILLED, style, value);
		}

		public void zStyleHotspot(int style, bool value) {
			Call(SCI_STYLESETHOTSPOT, style, value);
		}

		public bool zStyleHotspot(int style) {
			return 0 != Call(SCI_STYLEGETHOTSPOT, style);
		}

		public void zStyleForeColor(int style, ColorInt color) {
			Call(SCI_STYLESETFORE, style, color.ToBGR());
		}

		public void zStyleBackColor(int style, ColorInt color) {
			Call(SCI_STYLESETBACK, style, color.ToBGR());
		}

		/// <summary>
		/// Measures string width.
		/// </summary>
		public int zStyleMeasureStringWidth(int style, string s) {
			return zSetString(SCI_TEXTWIDTH, style, s);
		}

		/// <summary>
		/// Calls SCI_STYLECLEARALL, which sets all styles to be the same as STYLE_DEFAULT.
		/// Then also sets some special styles, eg STYLE_HIDDEN and hotspot color.
		/// </summary>
		/// <param name="belowDefault">Clear only styles 0..STYLE_DEFAULT.</param>
		public void zStyleClearAll(bool belowDefault = false) {
			if (belowDefault) zStyleClearRange(0, STYLE_DEFAULT);
			else Call(SCI_STYLECLEARALL);
			zStyleHidden(STYLE_HIDDEN, true);
			Call(SCI_SETHOTSPOTACTIVEFORE, true, 0xFF0080); //inactive 0x0080FF

			//STYLE_HOTSPOT currently unused
			//zStyleHotspot(STYLE_HOTSPOT, true);
			//zStyleForeColor(STYLE_HOTSPOT, 0xFF8000);
		}

		/// <summary>
		/// Calls SCI_STYLECLEARALL(styleFrom, styleToNotIncluding), which sets range of styles to be the same as STYLE_DEFAULT.
		/// If styleToNotIncluding is 0, clears all starting from styleFrom.
		/// </summary>
		public void zStyleClearRange(int styleFrom, int styleToNotIncluding = 0) {
			Call(SCI_STYLECLEARALL, styleFrom, styleToNotIncluding);
		}

		/// <summary>
		/// Gets style at position.
		/// Uses SCI_GETSTYLEAT.
		/// Returns 0 if pos is invalid.
		/// </summary>
		public int zGetStyleAt(int pos) {
			return Call(SCI_GETSTYLEAT, pos);
		}

		#endregion

		#region margins

		public void zSetMarginType(int margin, int SC_MARGIN_) {
			Call(SCI_SETMARGINTYPEN, margin, SC_MARGIN_);
		}

		internal int[] _marginDpi;

		public void zSetMarginWidth(int margin, int value, bool dpiScale = true, bool chars = false) {
			if (dpiScale && value > 0) {
				var a = _marginDpi ??= new int[Call(SCI_GETMARGINS)];
				if (chars) {
					value *= zStyleMeasureStringWidth(STYLE_LINENUMBER, "8");
					a[margin] = Dpi.Unscale(value, _dpi).ToInt();
				} else {
					a[margin] = value;
					value = Dpi.Scale(value, _dpi);
				}
			} else {
				var a = _marginDpi;
				if (a != null) a[margin] = 0;
			}
			Call(SCI_SETMARGINWIDTHN, margin, value);
		}

		//public void zSetMarginWidth(int margin, string textToMeasureWidth) {
		//	int n = zStyleMeasureStringWidth(STYLE_LINENUMBER, textToMeasureWidth);
		//	Call(SCI_SETMARGINWIDTHN, margin, n + 4);
		//}

		//not used
		//public int zGetMarginWidth(int margin, bool dpiUnscale) {
		//	int R = Call(SCI_GETMARGINWIDTHN, margin);
		//	if (dpiUnscale && R > 0) {
		//		var a = _marginDpi;
		//		var v = a?[margin] ?? 0;
		//		if (v > 0) R = v;
		//	}
		//	return R;
		//}

		internal void zMarginWidthsDpiChanged_() {
			var a = _marginDpi; if (a == null) return;
			for (int i = a.Length; --i >= 0;) {
				if (a[i] > 0) Call(SCI_SETMARGINWIDTHN, i, Dpi.Scale(a[i], _dpi));
			}
		}

		public int zMarginFromPoint(POINT p, bool screenCoord) {
			if (screenCoord) _w.MapScreenToClient(ref p);
			if (_w.ClientRect.Contains(p)) {
				for (int i = 0, n = Call(SCI_GETMARGINS), w = 0; i < n; i++) { w += Call(SCI_GETMARGINWIDTHN, i); if (w >= p.x) return i; }
			}
			return -1;
		}

		/// <summary>
		/// SCI_GETMARGINWIDTHN. Not DPI-scaled.
		/// </summary>
		public (int left, int right) zGetMarginX(int margin) {
			int x = 0;
			for (int i = 0; i < margin; i++) x += Call(SCI_GETMARGINWIDTHN, i);
			return (x, x + Call(SCI_GETMARGINWIDTHN, margin));
		}

		#endregion
	}
}
