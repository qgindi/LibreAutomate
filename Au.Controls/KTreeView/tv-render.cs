using System.Drawing;

namespace Au.Controls;

public unsafe partial class KTreeView {
	bool _dontMeasure;
	
	//Called on resize, scroll, set visible items (replace, clear, expand/collapse, add/remove/move).
	void _Measure(bool onScroll = false) {
		if (_dontMeasure) return; //this func is adding/removing scrollbars
		EndEditLabel();
		_labeltip?.Hide();
		
		int sbV = More.Dpi.ScrollbarV_(_dpi), sbH = More.Dpi.ScrollbarH_(_dpi);
		var rw = _w.Rect; //never mind: minus border. Currently we don't use border. OK even if used, if just 1-pixel border and no caption.
		int width = rw.Width, height = rw.Height;
		if (width <= sbV || height <= sbH || _avi.NE_()) {
			NativeScrollbar_.ShowVH(_vscroll, false, _hscroll, false);
			return;
		}
		var range = _GetViewRange(onScroll ? _height : height);
		
		//print.it("_Measure", range.from, range.to);
		int maxWidth = _itemsWidth;
		GdiTextRenderer tr = null;
		for (int i = range.from; i < range.to; i++) {
			if (_avi[i].measured > 0) continue;
			tr ??= new GdiTextRenderer(_dpi);
			var item = _avi[i].item;
			bool bold = item.IsBold; if (bold) tr.FontBold();
			int textWidth = item.MesureTextWidth(tr);
			if (textWidth < 0) textWidth = tr.MeasureText(item.DisplayText).width;
			if (bold) tr.FontNormal();
			int wid = ++textWidth + _imageSize * (_avi[i].level + 1) + _imageMarginX * 2 + _marginLeft + _marginRight;
			if (HasCheckboxes) wid += _itemLineHeight;
			if (wid > maxWidth) maxWidth = wid;
			_avi[i].measured = (ushort)Math.Clamp(textWidth, 1, ushort.MaxValue);
		}
		tr?.Dispose();
		if (maxWidth > _itemsWidth) _itemsWidth = maxWidth; else if (onScroll) return;
		
		//set scrollbars
		int itemsHeight = _avi.Length * _itemHeight;
		bool needH = _itemsWidth > width && height >= _imageSize + sbH; if (needH) height -= sbH;
		bool needV = itemsHeight > height && _avi.Length > 1;
		if (needV) { width -= sbV; if (!needH) needH = _itemsWidth > width && height >= _imageSize + sbH; }
		//print.it(needH, needV);
		if (_scrollCorrection = (needH && onScroll && _inScrollbarScroll && !_hscroll.Visible)) needH = false;
		_dontMeasure = true;
		NativeScrollbar_.ShowVH(_vscroll, needV, _hscroll, needH);
		_dontMeasure = false;
		if (needV) _vscroll.SetRange(_avi.Length); else _vscroll.NItems = _avi.Length;
		if (needH) _hscroll.SetRange(_itemsWidth / _imageSize); else _hscroll.NItems = 0;
	}
	bool _inScrollbarScroll;
	bool _scrollCorrection;
	
	void _ScrollEnded() {
		//workaround for: if we add other scrollbar while SB_THUMBTRACK-scrolling, does not scroll to the very bottom. Would need to scroll 2 times.
		//	Also, if while SB_LINEDOWN-scrolling, the scroll box arrow remains not erased at the bottom-right corner.
		//	Alas scintilla has these problems too.
		if (_scrollCorrection) {
			_scrollCorrection = false;
			int max = _vscroll.Max, pos = _vscroll.Pos;
			_hscroll.Visible = true;
			_hscroll.SetRange(_itemsWidth / _imageSize);
			if (_vscroll.Max > max && pos == max) EnsureVisible(_avi.Length - 1);
		}
	}
	
	void _MeasureClear(bool updateNow) {
		if (!_hasHwnd) return;
		if (_avi != null) for (int i = 0; i < _avi.Length; i++) _avi[i].measured = 0;
		_itemsWidth = 0;
		if (updateNow) {
			_Measure();
			_Invalidate();
		}
	}
	
	(int from, int to) _GetViewRange(int height) {
		int len = _avi.Lenn_(); if (len == 0) return default;
		int i = _vscroll.Pos + (height + _itemHeight - 1) / _itemHeight;
		return (_vscroll.Pos, Math.Min(i, len));
	}
	
	(int from, int to) _GetViewRange() => _GetViewRange(_height);
	
	void _Invalidate(RECT* r = null) {
		if (_hasHwnd) Api.InvalidateRect(_w, r, false);
	}
	
	void _Invalidate(int index) {
		if (!_hasHwnd) return;
		var r = GetRectPhysical(index, clampX: true);
		if (r.bottom > 0 && r.top < _height) Api.InvalidateRect(_w, &r, false);
	}
	
	/// <summary>
	/// Asynchronously redraws item.
	/// Does nothing if the control is not created.
	/// </summary>
	/// <param name="index"></param>
	/// <param name="remeasure">Remeasure item width. My need this when changed text or text style.</param>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public void Redraw(int index, bool remeasure = false) {
		if (!_hasHwnd) return;
		if (!_IsValid(index)) throw new IndexOutOfRangeException();
		if (remeasure) {
			//if this was the widest measured item, need to remeasure all, else would not update horz scrollbar if this item become narrower
			int max = 0; for (int i = 0; i < _avi.Length; i++) max = Math.Max(max, _avi[i].measured);
			if (max == _avi[index].measured) {
				_MeasureClear(updateNow: true);
				return;
			}
			_avi[index].measured = 0;
			_Measure();
		}
		_Invalidate(index);
	}
	
	/// <summary>
	/// Asynchronously redraws item.
	/// Does nothing if the control is not created or <i>item</i> is not a visible item in this control.
	/// </summary>
	/// <param name="item"></param>
	/// <param name="remeasure">Remeasure item width. My need this when changed text or text style.</param>
	public void Redraw(ITreeViewItem item, bool remeasure = false) {
		if (!_hasHwnd) return;
		int i = IndexOf(item);
		if (i >= 0) Redraw(i, remeasure);
	}
	
	/// <summary>
	/// Asynchronously redraws all items.
	/// Does nothing if the control is not created.
	/// </summary>
	/// <param name="remeasure">Remeasure item width. My need this when changed text or text style.</param>
	public void Redraw(bool remeasure = false) {
		if (!_hasHwnd) return;
		if (remeasure) _MeasureClear(updateNow: true);
		else _Invalidate();
	}
	
	//	/// <summary>
	//	/// Remeasure item widths and redraws. My need this when changed text or text style.
	//	/// Does nothing if the control is not created.
	//	/// </summary>
	//	/// <param name="indices"></param>
	//	/// <exception cref="ArgumentOutOfRangeException"></exception>
	//	public void Remeasure(Range indices) {
	//		if(_border==null) return;
	//		var (i, to)=indices.GetStartEnd(_avi.Lenn_());
	//		while(i<to) _avi[i++].measured=0;
	//		_Measure();
	//		_Invalidate();
	//	}
	
	/// <summary>
	/// Image background brush.
	/// Does not redraw.
	/// </summary>
	public Brush ImageBrush { get; set; }
	
	/// <summary>
	/// Background color.
	/// Ignored if high contrast.
	/// </summary>
	public ColorInt? BackgroundColor { get; set; }
	
	//public ColorInt? TextColor { get; set; }
	
	public const int ColorSelectedItemFocused = 0xc4d5ff;
	
	public const int ColorSelectedItemNonfocused = 0xe0e0e0;
	
	public const int ColorHotItem = 0xe8f0ff;
	
	void _Render(IntPtr dc, RECT rUpdate) {
		Graphics graphics = null;
		try {
			TVColorInfo colorInfo = new() { isHighContrast = System.Windows.SystemParameters.HighContrast };
			
			bool haveBackColor = BackgroundColor != null && !colorInfo.isHighContrast;
			if (haveBackColor) {
				graphics = Graphics.FromHdc(dc);
				graphics.Clear((Color)BackgroundColor.Value);
			} else {
				Api.FillRect(dc, rUpdate, (IntPtr)(Api.COLOR_WINDOW + 1));
			}
			if (_avi.NE_()) return;
			
			var range = _GetViewRange();
			int nDraw = range.to - range.from;
			if (nDraw > 0) {
				int backColor = haveBackColor ? BackgroundColor.Value.ToBGR() : Api.GetSysColor(Api.COLOR_WINDOW);
				int textColor = Api.GetSysColor(Api.COLOR_WINDOWTEXT);
				
				colorInfo.isTextBlack = textColor == 0;
				if (colorInfo.isHighContrast) colorInfo.isHighContrastDark = WpfUtil_.IsHighContrastDark;
				colorInfo.isFocusedControl = this.IsKeyboardFocused;
				
				int xLefts = -_hscroll.Offset;
				if (HasCheckboxes) xLefts += _itemLineHeight;
				int xImages = xLefts + _imageMarginX + _marginLeft;
				int yyImages = (_itemLineHeight + 1 - _imageSize) / 2;
				int yyText = _itemLineHeight <= 22 ? 1 : _itemLineHeight <= 28 ? 0 : -1;
				
				graphics ??= Graphics.FromHdc(dc);
				var tr = new GdiTextRenderer(dc, _dpi);
				IntPtr checkTheme = HasCheckboxes ? Api.OpenThemeData(_w, "Button", _dpi) : default;
				try {
					graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					SIZE cSize = default;
					if (HasCheckboxes)
						if (checkTheme == default || 0 != Api.GetThemePartSize(checkTheme, dc, 3, 1, null, Api.THEMESIZE.TS_TRUE, out cSize))
							cSize.width = cSize.height = More.Dpi.Scale(13, _dpi);
					
					var cd = CustomDraw;
					var cdi = cd == null ? null : new TVDrawInfo(this, dc, graphics, _dpi) {
						marginLeft = _marginLeft,
						marginRight = _marginRight,
						checkSize = cSize
					};
					cd?.Begin(cdi, tr);
					
					for (int i = 0; i < nDraw; i++) {
						int y = i * _itemHeight;
						var r = new RECT(0, y, _width, _itemHeight);
						if (!r.IntersectsWith(rUpdate)) continue;
						
						int index = i + range.from;
						var v = _avi[index];
						var item = v.item;
						int indent = _imageSize * v.level, xLeft = indent + xLefts;
						int xImage = indent + xImages, yImage = y + yyImages;
						int xText = xImage + _imageSize + _imageMarginX, yText = y + yyText;
						
						colorInfo.isSelected = v.isSelected;
						colorInfo.isHot = index == _hotIndex;
						colorInfo.isFocusedItem = index == _focusedIndex;
						
						if (cdi != null) {
							cdi.index = index;
							cdi.item = item;
							cdi.rect = r;
							cdi.imageRect = new RECT(xImage, yImage, _imageSize, _imageSize);
							cdi.xText = xText;
							cdi.yText = yText;
							cdi.xLeft = xLeft;
							cdi.xRight = xText + v.measured;
							cdi.lineHeight = _itemLineHeight;
							cdi.colorInfo = colorInfo;
						}
						
						//background
						bool hiliteSysColor = false;
						if (cd == null || !cd.DrawBackground()) {
							int color = item.Color(colorInfo);
							if (color != -1 || (backColor & 0xe0e0e0) == 0xe0e0e0) { //custom color, or default color is [almost] white
								if (color != -1) //draw custom color
									using (var b1 = GdiObject_.ColorBrush(color)) b1.BrushFill(dc, r);
								int alpha = color >>> 24;
								if (color == -1 || alpha is >= 1 and <= 3) { //if no custom color or if custom color's alpha is 1 - 3, draw selection or hot background
									color = -1;
									if (v.isSelected) {
										if (0 != (alpha & 1)) {
											color = item.SelectedColor(colorInfo);
											if (color == -1) color = colorInfo.isFocusedControl ? ColorSelectedItemFocused : ColorSelectedItemNonfocused;
										}
									} else if (HotTrack && index == _hotIndex) {
										if (0 != (alpha & 2)) color = ColorHotItem;
									}
									if (color != -1) {
										var r2 = r; r2.left = xText - _imageMarginX / 2; //don't draw selection background under icon and checkbox
										using (var b2 = GdiObject_.ColorBrush(color)) b2.BrushFill(dc, r2);
									}
								}
							} else { //probably high contrast or dark BackgroundColor
								if (hiliteSysColor = v.isSelected) Api.FillRect(dc, r, (IntPtr)(Api.COLOR_HIGHLIGHT + 1));
							}
							
							color = item.BorderColor(colorInfo);
							if (color != -1) {
								int alpha = color >>> 24;
								var r2 = r; if (alpha == 1) r2.left = xText - _imageMarginX / 2 - 1;
								using (var b3 = GdiObject_.ColorBrush(color)) b3.BrushRect(dc, r2);
							}
						}
						
						//checkboxes
						if (HasCheckboxes && item.CheckState != TVCheck.None) {
							if (cd == null || !cd.DrawCheckbox()) {
								//if(1==(i&3)) item.CheckState=TVCheck.Checked; if(2==(i&3)) item.CheckState=TVCheck.Mixed; if(3==(i&3)) item.CheckState=TVCheck.Excluded; if(0!=(i&4)) v.IsDisabled=true; //test
								var rr = new RECT(xLeft - cSize.height, y + (_itemLineHeight - cSize.height) / 2, cSize.width, cSize.height);
								var ch = item.CheckState;
								if (checkTheme != default) {
									int state = ch switch { TVCheck.Checked => 5, TVCheck.RadioChecked => 5, TVCheck.Mixed => 9, TVCheck.Excluded => 17, _ => 1 }; //CBS_x,RBS_x
									if (item.IsDisabled) state += 3; else if (index == _hotIndex) state += 1;
									Api.DrawThemeBackground(checkTheme, dc, (ch == TVCheck.RadioChecked || ch == TVCheck.RadioUnchecked) ? 2 : 3, state, rr); //BP_RADIOBUTTON,BP_CHECKBOX
								} else if (ch != TVCheck.Excluded) {
									int state = ch switch { TVCheck.Checked => 0x400, TVCheck.Mixed => 0x408, TVCheck.RadioUnchecked => 0x4, TVCheck.RadioChecked => 0x404, _ => 0 }; //DFCS_x
									if (item.IsDisabled) state |= 0x100; else if (index == _hotIndex) state |= 0x1000;
									KApi.DrawFrameControl(dc, rr, 4, state); //DFC_BUTTON
								}
								//cannot use .NET CheckBoxRenderer etc because no per-monitor DPI.
							}
						}
						
						//image background
						if (ImageBrush != null) {
							int imm = (_imageMarginX + 1) / 2;
							graphics.FillRectangle(ImageBrush, xImage - imm, y, _imageSize + imm * 2, _itemLineHeight);
						}
						
						//image
						_DrawImage(item.Image);
						
						void _DrawImage(object imo) {
							Bitmap b = null;
							switch (imo) {
							case Bitmap v:
								b = v;
								break;
							case string v:
								if (v == "link:") { v = s_stockLinkIcon.Value; if (v == null) break; }
								b = ImageCache.Get(v, _dpi, ImageUtil.HasImageOrResourcePrefix(v));
								break;
							case IEnumerable<object> v:
								foreach (var o in v) _DrawImage(o);
								break;
							}
							
							if (b != null) {
								if (cd == null || !cd.DrawImage(b)) {
									graphics.DrawImage(b, new Rectangle(xImage, yImage, _imageSize, _imageSize));
								}
							}
						}
						
						//text
						if (cd == null || !cd.DrawText()) {
							bool bold = item.IsBold; if (bold) tr.FontBold();
							int color = item.TextColor(colorInfo);
							if (color == -1) color = item.IsDisabled ? Api.GetSysColor(Api.COLOR_GRAYTEXT) : hiliteSysColor ? Api.GetSysColor(Api.COLOR_HIGHLIGHTTEXT) : textColor;
							else color = ColorInt.SwapRB(color);
							tr.DrawText(item.DisplayText, (xText, yText), color);
							if (bold) tr.FontNormal();
						}
						
						if (cd != null) {
							cd.DrawMarginLeft();
							cd.DrawMarginRight();
						}
						
						//drag & drop insertion mark
						if (_dd != null && _dd.insertIndex == index) {
							int thick = More.Dpi.Scale(3, _dpi);
							using var pen = new Pen(SystemColors.WindowText, thick);
							y += thick / 2; int h1 = _itemHeight - thick;
							if (_dd.insertFolder) {
								graphics.DrawRectangle(pen, xImage, y, _imageSize, h1);
							} else {
								if (_dd.insertAfter) y += h1;
								graphics.DrawLine(pen, indent, y, _width, y);
							}
						}
					}
					
					cd?.End();
				}
				finally {
					tr.Dispose();
					if (checkTheme != default) Api.CloseThemeData(checkTheme);
				}
			}
		}
		finally { graphics?.Dispose(); }
	}
	
	static string _StockLinkLocation() {
		if (!icon.GetStockIconLocation_(StockIcon.LINK, out var ipath, out int iindex)) return null;
		return ipath + "," + iindex.ToS();
	}
	static Lazy<string> s_stockLinkIcon = new(_StockLinkLocation);
	
	/// <summary>
	/// Gets or sets image cache.
	/// If not set, uses <see cref="IconImageCache.Common"/>.
	/// </summary>
	public IconImageCache ImageCache {
		get => _imageCache ??= IconImageCache.Common;
		set { _imageCache = value; }
	}
	IconImageCache _imageCache;
	
	/// <summary>
	/// Width of custom-draw area before image. For example for state images.
	/// Use WPF logical units, not physical pixels.
	/// </summary>
	public int ItemMarginLeft {
		get => _itemMarginLeft_;
		set {
			if (value != _itemMarginLeft_) {
				_itemMarginLeft_ = value;
				_marginLeft = _DpiScale(value);
				_MeasureClear(true);
			}
		}
	}
	int _itemMarginLeft_;
	
	/// <summary>
	/// Width of custom-draw area after item text.
	/// Use WPF logical units, not physical pixels.
	/// </summary>
	public int ItemMarginRight {
		get => _itemMarginRight_;
		set {
			if (value != _itemMarginRight_) {
				_itemMarginRight_ = value;
				_marginRight = _DpiScale(value);
				_MeasureClear(true);
			}
		}
	}
	int _itemMarginRight_;
	
	/// <summary>
	/// Custom-draw interface.
	/// </summary>
	public ITVCustomDraw CustomDraw {
		get => _customDraw_;
		set {
			if (value != _customDraw_) {
				_customDraw_ = value;
				_Invalidate();
			}
		}
	}
	ITVCustomDraw _customDraw_;
	
	/// <summary>
	/// Add this % to the item height. The area is below the main text/icon/checkbox. The control does not draw in it. Let your custom draw class draw there.
	/// </summary>
	public int CustomItemHeightAddPercent { get; set; }
	
	///// <summary>
	///// If not null, fills background with this color instead of the system window background color.
	///// Set this property at startup (does not update control when changed).
	///// </summary>
	//public ColorInt? BackgroundColor { get; set; }
#pragma warning restore 1591
}
