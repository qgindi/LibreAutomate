using System.Drawing;
using System.Drawing.Drawing2D;

namespace Au;

public partial class toolbar {
	/// <summary>
	/// Border style.
	/// Default <see cref="TBBorder.Width2"/>.
	/// </summary>
	/// <remarks>
	/// This property is in the context menu and is saved.
	/// </remarks>
	public TBBorder Border {
		get => _sett.border;
		set {
			_ThreadTrap();
			if (value == _sett.border) return;
			if (IsOpen) {
				RECT r = _w.ClientRectInScreen;
				var bp1 = _BorderPadding();
				_sett.border = value;
				var bpDiff = _BorderPadding() - bp1;
				if (bpDiff != 0) { r.Inflate(bpDiff, bpDiff); r.Normalize(false); }
				const WS mask = WS.CAPTION | WS.THICKFRAME | WS.SYSMENU;
				WS s1 = _w.Style, s2 = _BorderStyle(value);
				if (s2 != (s1 & mask)) _w.SetStyle(s1 = ((s1 & ~mask) | s2));
				Dpi.AdjustWindowRectEx(r, ref r, s1, _w.ExStyle);
				_w.MoveL(r, SWPFlags.FRAMECHANGED | SWPFlags.HIDEWINDOW);
				if (bpDiff != 0) _Measure(); //update button rectangles
				_w.ShowL(true);
			} else {
				_sett.border = value;
			}
			
		}
	}
	
	/// <summary>
	/// Layout of buttons (horizontal, vertical).
	/// </summary>
	/// <remarks>
	/// This property is in the context menu and is saved.
	/// </remarks>
	public TBLayout Layout {
		get => _sett.layout;
		set {
			_ThreadTrap();
			if (value != _sett.layout) {
				_sett.layout = value;
				_AutoSizeNow();
			}
		}
	}
	
	/// <summary>
	/// Display button text. Default <c>true</c>. If <c>false</c>, displays text in tooltips, and only displays first 2 characters for buttons without image.
	/// </summary>
	/// <remarks>
	/// A button can override this property:
	/// - To never display text, let its text be empty, like <c>""</c> or <c>"|Tooltip"</c>.
	/// - To always display text, append <c>"\a"</c>, like <c>"Text\a"</c> or <c>"Text\a|Tooltip"</c>.
	/// </remarks>
	public bool DisplayText {
		get => _sett.dispText;
		set {
			_ThreadTrap();
			if (value != _sett.dispText) {
				_sett.dispText = value;
				_MeasureText();
				_AutoSizeNow();
			}
		}
	}
	
	/// <summary>
	/// Font properties.
	/// </summary>
	/// <remarks>
	/// Cannot be changed after showing toolbar window.
	/// </remarks>
	public FontNSS Font {
		get => _font ??= new();
		set { _font = value; }
	}
	FontNSS _font;
	
	/// <summary>
	/// Text color.
	/// If <c>default</c>, uses system color.
	/// </summary>
	public ColorInt TextColor {
		get => _textColor;
		set {
			_textColor = value;
			_Invalidate();
		}
	}
	ColorInt _textColor;
	
	/// <summary>
	/// Background color or brush.
	/// </summary>
	/// <value>Can be <see cref="Color"/>, <see cref="ColorInt"/>, <b>int</b> (color 0xRRGGBB) or <see cref="Brush"/>. If <c>null</c> (default), uses system color.</value>
	public object Background {
		get => _background;
		set {
			if (value is not (null or Brush or Color or ColorInt or int)) throw new ArgumentException();
			_background = value;
			_Invalidate();
		}
	}
	object _background;
	
	/// <summary>
	/// Border color when <see cref="Border"/> is Width1 ... Width4.
	/// If <c>default</c>, uses system color.
	/// </summary>
	public ColorInt BorderColor {
		get => _borderColor;
		set {
			_borderColor = value;
			_Invalidate();
		}
	}
	ColorInt _borderColor;
	
	/// <summary>
	/// Don't display the default image (dot or triangle).
	/// </summary>
	public bool NoDefaultImage { get; set; }
	
	/// <summary>
	/// Sets some metrics of this toolbar, for example button padding.
	/// </summary>
	/// <remarks>
	/// Cannot be changed after showing toolbar window.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// t.Metrics = new(4, 2);
	/// ]]></code>
	/// </example>
	/// <seealso cref="DefaultMetrics"/>
	public TBMetrics Metrics { get; set; }
	
	/// <summary>
	/// Sets default metrics of toolbars.
	/// </summary>
	/// <seealso cref="Metrics"/>
	public static TBMetrics DefaultMetrics {
		get => s_defaultMetrics ??= new();
		set { s_defaultMetrics = value; }
	}
	static TBMetrics s_defaultMetrics;
	
	void _Images(bool onDpiChanged) {
		foreach (var v in _a) {
			if (onDpiChanged) {
				if (v.image2 == null || v.image is not string) continue;
				//will either find/return same bitmap in cache, or create new bitmap from XAML, or return found old XAML bitmap created for other DPI.
				//note: will not reload non-XAML images, eg those created from native icons. They will be drawn DPI-scaled, slightly blurry.
			}
			//var old = v.image2;
			v.image2 = _GetImage(v).image;
			//if (onDpiChanged) print.it(old == v.image2);
			if (!_hasCachedImages && !onDpiChanged && v.image2 != null && v.image is string s && s.Length > 0) _hasCachedImages = true;
		}
		if (_hasCachedImages) IconImageCache.Common.Cleared += _UpdateCachedImages;
	}
	bool _hasCachedImages;
	
	void _UpdateCachedImages() {
		foreach (var v in _a) {
			if (v.image2 != null && v.image is string s && s.Length > 0) {
				if (_GetImage(v).image is { } b) v.image2 = b;
			}
		}
	}
	
	//not used
	//internal void ChangeImage_(TBItem ti, Bitmap b) {
	//	if (_closed) return;
	//	ti.image2 = b;
	//	_Invalidate(ti);
	//}
	
	const TFFlags c_tff = TFFlags.NOPREFIX | TFFlags.EXPANDTABS;
	
	void _MeasureText() {
		NativeFont_ font = null;
		FontDC_ dc = null;
		try {
			foreach (var b in _a) {
				int len = _TextDispLen(b);
				if (len != 0) {
					dc ??= new FontDC_(font = Font.CreateFont(_dpi));
					b.textSize = dc.MeasureDT(b.Text.AsSpan(0, len), c_tff);
					b.textSize.height++;
				} else {
					b.textSize = default;
				}
			}
		}
		finally {
			dc?.Dispose();
			font?.Dispose();
		}
	}
	
	int _TextDispLen(TBItem b) {
		var s = b.Text;
		if (s.NE()) return 0;
		if (DisplayText || b.textAlways) return s.Length;
		if (b.HasImage_) return 0;
		return Math.Min(s.Length, 2); //info: 2 is ok for surrogate
	}
	
	struct _Metrics {
		public int bBorder, bPaddingX, bPaddingY, tbBorder, tbPadding, textPaddingR, textPaddingY, image, dot, triangle, imagePaddingX;
		
		public _Metrics(toolbar tb) {
			var k = tb.Metrics ?? DefaultMetrics;
			int dpi = tb._dpi;
			bBorder = dpi / 96;
			bPaddingX = Dpi.Scale(k.ButtonPaddingX, dpi);
			bPaddingY = Dpi.Scale(k.ButtonPaddingY, dpi);
			tbPadding = tb._BorderPadding();
			tbBorder = tbPadding > 0 ? bBorder : 0;
			textPaddingR = Dpi.Scale(4, dpi);
			textPaddingY = Dpi.Scale(1, dpi);
			image = Dpi.Scale(16, dpi);
			if (!tb.NoDefaultImage) {
				dot = Dpi.Scale(5, dpi);
				triangle = Dpi.Scale(8, dpi);
			} else dot = triangle = 0;
			imagePaddingX = Dpi.Scale(2, dpi);
			
			//tbBorder += 1; //test border thickness
			//bBorder += 1;
		}
		
		public int ImageEtc(TBItem b, bool vert) => b.HasImage_ ? image : (dot == 0 ? 0 : (vert ? image : (b.IsMenu_ ? triangle : dot)));
	}
	
	/// <summary>
	/// Measures toolbar size and sets button rectangles.
	/// Returns size of client area.
	/// </summary>
	SIZE _Measure(int? width = null) {
		//print.it("measure");
		SIZE R = default;
		bool autoSize = AutoSize && _a.Count > 0;
		bool vert = Layout == TBLayout.Vertical;
		var m = new _Metrics(this);
		int tbp = m.tbPadding,
			buttonPlusX = (m.bBorder + m.bPaddingX + m.imagePaddingX) * 2,
			buttonPlusY = (m.bBorder + m.bPaddingY + m.textPaddingY) * 2;
		
		int ww = int.MaxValue;
		if (width != null) ww = width.Value - tbp * 2;
		else if (!autoSize) ww = _Scale(_sett.size.Width, false) - tbp * 2;
		else if (AutoSizeWrapWidth > 0) ww = _Scale(AutoSizeWrapWidth, false);
		//ww = Math.Max(ww, m.image); //no, then can't have thin vertical screen edge toolbars
		
		for (int i = 0, x = 0, y = 0; i < _a.Count; i++) {
			var b = _a[i];
			SIZE z;
			if (b.IsGroup_ || (b.IsSeparator_ && vert)) {
				_NewRow();
				if (b.Text.NE()) z = new(2, 2 + m.imagePaddingX * 2);
				else z = new(b.textSize.width + m.image * 2, b.textSize.height + m.imagePaddingX * 2);
			} else {
				if (b.IsSeparator_) {
					z = new(2 + m.imagePaddingX * 2, _a[i - 1].rect.Height);
				} else {
					z = new(buttonPlusX + m.ImageEtc(b, vert),
						m.image + m.bPaddingY * 2 + m.bBorder * 4); //m.bBorder*4 for borders and image padding
					if (b.textSize.width > 0) {
						z.width += b.textSize.width + m.textPaddingR;
						z.height = Math.Max(z.height, b.textSize.height + buttonPlusY);
					}
				}
				if (!vert) if (x + z.width > ww && x > 0) _NewRow();
			}
			b.rect = new(x, y, z.width, z.height);
			b.rect.right = Math.Min(b.rect.right, ww);
			x += z.width;
			R.width = Math.Max(R.width, x);
			R.height = Math.Max(R.height, b.rect.bottom);
			if (vert || b.IsGroup_) _NewRow();
			
			void _NewRow() { x = 0; y = R.height; }
		}
		
		if (autoSize) {
			R.width = Math.Min(R.width, ww) + tbp * 2;
			R.height += tbp * 2;
		} else {
			R = new(ww + tbp * 2, _Scale(_sett.size.Height, false));
		}
		//print.it(R);
		
		foreach (var b in _a) {
			b.rect.Offset(tbp, tbp);
			if (vert || b.IsGroup_) b.rect.right = R.width - tbp;
		}
		
		return R;
	}
	
	void _WmPaint(IntPtr dc, Graphics g, RECT rClient, RECT rUpdate) {
		switch (Background) {
		case Brush bb: g.FillRectangle(bb, rUpdate); break;
		case Color bc: g.Clear(bc); break;
		case ColorInt bc: g.Clear((Color)bc); break;
		case int bc: g.Clear(bc.ToColor_()); break;
		default: g.Clear(SystemColors.Control); break;
		}
		
		var m = new _Metrics(this);
		bool vert = Layout == TBLayout.Vertical;
		
		Brush brushDot = null, brushTriangle = null;
		NativeFont_ font = null; IntPtr oldFont = default;
		int groupTextX = 0;
		int sysTextColor = Api.GetSysColor(Api.COLOR_BTNTEXT);
		try {
			for (int i = 0; i < _a.Count; i++) {
				var b = _a[i];
				if (!b.rect.IntersectsWith(rUpdate)) continue;
				
				//g.DrawRectangleInset(Pens.BlueViolet, b.rect);
				
				int textColor = b.TextColor != default ? b.TextColor.ToBGR() : (TextColor != default ? TextColor.ToBGR() : sysTextColor);
				
				int xImage = b.rect.left + m.bBorder + m.bPaddingX + m.imagePaddingX;
				
				if (b.IsSeparator_ && !vert) {
					var r = b.rect; r.Inflate(-m.imagePaddingX, -m.imagePaddingX - m.bBorder);
					Api.DrawEdge(dc, ref r, Api.EDGE_ETCHED, Api.BF_LEFT);
				} else if (b.IsSeparatorOrGroup_) {
					int pad = m.tbPadding + m.imagePaddingX;
					RECT r = new(pad, b.rect.CenterY - 1, rClient.Width - pad * 2, 2);
					if (b.Text.NE()) {
						Api.DrawEdge(dc, ref r, Api.EDGE_ETCHED, Api.BF_TOP);
					} else {
						int len = (r.Width - b.textSize.width) / 2 - m.textPaddingR; //length of left and right (from text) parts
						if (len > 0) {
							r.left = r.right - len;
							Api.DrawEdge(dc, ref r, Api.EDGE_ETCHED, Api.BF_TOP); //right
							r.left = pad; r.right = pad + len;
							Api.DrawEdge(dc, ref r, Api.EDGE_ETCHED, Api.BF_TOP); //left
							groupTextX = r.right + m.textPaddingR;
						} else groupTextX = pad;
					}
				} else {
					if (i == _iHot || i == _iClick) {
						if (!_noHotClick && (textColor == 0 || TextColor != default || b.TextColor != default))
							g.FillRectangle((i == _iClick ? 0xC0DCF3 : 0xD8E6F2).ToColor_(), b.rect);
						g.DrawRectangleInset((i == _iClick ? 0x90C8F6 : 0xC0DCF3).ToColor_(), m.bBorder, b.rect);
					}
					
					int x = xImage, y = b.rect.top;
					if (!b.HasImage_) {
						if (m.dot > 0) {
							g.SmoothingMode = SmoothingMode.HighQuality;
							y += (b.rect.Height - m.dot) / 2;
							if (vert) x += m.image / 4;
							if (b.IsMenu_) {
								y += m.triangle / 6;
								x -= m.triangle / 4; if (!vert) x++;
								brushTriangle ??= new SolidBrush(Color.YellowGreen);
								g.FillPolygon(brushTriangle, new Point[] { new(x, y), new(x + m.triangle, y), new(x + m.triangle / 2, y + m.triangle / 2) });
							} else {
								brushDot ??= new SolidBrush(Color.SkyBlue);
								g.FillEllipse(brushDot, x - .5f, y, m.dot, m.dot);
							}
							g.SmoothingMode = SmoothingMode.None;
						}
					} else if (b.image2 != null) {
						g.DrawImage(b.image2, x, y + (b.rect.Height - m.image) / 2, m.image, m.image);
					}
				}
				
				if (b.textSize.width > 0) {
					if (font == null) {
						font = Font.CreateFont(_dpi);
						oldFont = Api.SelectObject(dc, font.Handle);
						Api.SetBkMode(dc, 1);
					}
					
					RECT r;
					var tff = c_tff;
					if (b.IsGroup_) {
						r = new(groupTextX, b.rect.top + m.textPaddingY, b.textSize.width, b.textSize.height);
					} else {
						r = new(xImage + m.ImageEtc(b, vert) + m.imagePaddingX,
							b.rect.top + m.bBorder + m.bPaddingY + m.textPaddingY,
							b.textSize.width,
							b.textSize.height);
					}
					r.right = Math.Min(r.right, b.rect.right - m.bBorder * 2);
					//if(!b.Text.Contains('\n')) tff|=TFFlags.SINGLELINE|TFFlags.VCENTER;
					Api.SetTextColor(dc, textColor);
					Api.DrawText(dc, b.Text.AsSpan(0, _TextDispLen(b)), ref r, tff);
				}
			}
		}
		finally {
			brushDot?.Dispose();
			brushTriangle?.Dispose();
			if (font != null) {
				Api.SelectObject(dc, oldFont);
				font.Dispose();
			}
		}
		
		if (m.tbBorder > 0) {
			g.DrawRectangleInset(BorderColor != default ? (Color)BorderColor : SystemColors.ControlDark, m.tbBorder, rClient);
		}
	}
}
