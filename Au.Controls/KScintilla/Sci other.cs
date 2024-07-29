namespace Au.Controls;

using static Sci;

public partial class KScintilla {
	
	#region markers
	
	/// <summary>
	/// Sets marker style and colors.
	/// </summary>
	/// <param name="marker">Marker index, 0-31. Indices 25-31 are used for folding markers (SC_MARKNUM_FOLDERx). Indices 21-24 are used for change history markers. Scintilla draws markers from smaller to bigger index.</param>
	/// <param name="style">SC_MARK_.</param>
	/// <param name="foreColor">SCI_MARKERSETFORE.</param>
	/// <param name="backColor">SCI_MARKERSETBACK.</param>
	public void aaaMarkerDefine(int marker, int style, ColorInt? foreColor = null, ColorInt? backColor = null) {
		if ((uint)marker > 31) throw new ArgumentOutOfRangeException(nameof(marker));
		Call(SCI_MARKERDEFINE, marker, style);
		if (foreColor != null) Call(SCI_MARKERSETFORE, marker, foreColor.Value.ToBGR());
		if (backColor != null) Call(SCI_MARKERSETBACK, marker, backColor.Value.ToBGR());
	}
	
	/// <summary>
	/// SCI_MARKERADD.
	/// </summary>
	/// <returns>Marker handle, or -1 if failed.</returns>
	public int aaaMarkerAdd(int marker, int line) {
		return Call(SCI_MARKERADD, line, marker);
	}
	
	/// <summary>
	/// SCI_MARKERADD in line containing <i>pos</i>.
	/// </summary>
	/// <returns>Marker handle, or -1 if failed.</returns>
	public int aaaMarkerAdd(int marker, bool utf16, int pos) {
		return aaaMarkerAdd(marker, aaaLineFromPos(utf16, pos));
	}
	
	/// <summary>
	/// SCI_MARKERDELETE.
	/// </summary>
	public void aaaMarkerDelete(int marker, int line) {
		Call(SCI_MARKERDELETE, line, marker);
	}
	
	/// <summary>
	/// SCI_MARKERDELETE in line containing <i>pos</i>.
	/// </summary>
	public void aaaMarkerDelete(int marker, bool utf16, int pos) {
		aaaMarkerDelete(marker, aaaLineFromPos(utf16, pos));
	}
	
	/// <summary>
	/// SCI_MARKERDELETEALL.
	/// </summary>
	/// <param name="marker">If -1, delete all markers from all lines.</param>
	public void aaaMarkerDeleteAll(int marker) {
		Call(SCI_MARKERDELETEALL, marker);
	}
	
	/// <summary>
	/// SCI_MARKERDELETEHANDLE.
	/// </summary>
	public void aaaMarkerDeleteHandle(int handle) {
		Call(SCI_MARKERDELETEHANDLE, handle);
	}
	
	#endregion
	
	#region indicators
	
	/// <summary>
	/// Sets indicator style, color, etc.
	/// </summary>
	/// <param name="indic">Indicator index, 0-31. Scintilla draws indicators from smaller to bigger index.</param>
	/// <param name="style">Eg Sci.INDIC_FULLBOX.</param>
	/// <param name="color">SCI_INDICSETFORE.</param>
	/// <param name="alpha">SCI_INDICSETALPHA. Valid for some styles.</param>
	/// <param name="borderAlpha">SCI_INDICSETOUTLINEALPHA. Valid for some styles.</param>
	/// <param name="strokeWidth">SCI_INDICSETSTROKEWIDTH (%). Valid for some styles.</param>
	/// <param name="underText">SCI_INDICSETUNDER (under text).</param>
	/// <param name="hoverColor">SCI_INDICSETHOVERFORE.</param>
	/// <param name="hoverStyle">SCI_INDICSETHOVERSTYLE</param>
	public void aaaIndicatorDefine(int indic, int style, ColorInt? color = null, int? alpha = null, int? borderAlpha = null, int? strokeWidth = null, bool underText = false, ColorInt? hoverColor = null, int? hoverStyle = null) {
		if ((uint)indic > 31) throw new ArgumentOutOfRangeException(nameof(indic));
		Call(SCI_INDICSETSTYLE, indic, style);
		if (style != INDIC_HIDDEN) {
			if (color != null) Call(SCI_INDICSETFORE, indic, color.Value.ToBGR());
			if (alpha != null) Call(SCI_INDICSETALPHA, indic, alpha.Value);
			if (borderAlpha != null) Call(SCI_INDICSETOUTLINEALPHA, indic, borderAlpha.Value);
			if (strokeWidth != null) Call(SCI_INDICSETSTROKEWIDTH, indic, strokeWidth.Value);
			if (underText) Call(SCI_INDICSETUNDER, indic, underText);
		}
		if (hoverColor != null) Call(SCI_INDICSETHOVERFORE, indic, hoverColor.Value.ToBGR());
		if (hoverStyle != null) Call(SCI_INDICSETHOVERSTYLE, indic, hoverStyle.Value);
	}
	
	public void aaaIndicatorClear(int indic) => aaaIndicatorClear(indic, false, ..);
	
	public void aaaIndicatorClear(int indic, bool utf16, Range r) {
		var (from, to) = aaaNormalizeRange(utf16, r);
		Call(SCI_SETINDICATORCURRENT, indic);
		Call(SCI_INDICATORCLEARRANGE, from, to - from);
	}
	
	public void aaaIndicatorAdd(int indic, bool utf16, Range r, int value = 1) {
		var (from, to) = aaaNormalizeRange(utf16, r);
		Call(SCI_SETINDICATORCURRENT, indic);
		Call(SCI_SETINDICATORVALUE, value);
		Call(SCI_INDICATORFILLRANGE, from, to - from);
	}
	
	/// <summary>
	/// SCI_INDICATORVALUEAT.
	/// </summary>
	public int aaaIndicatorGetValue(int indic, int pos, bool utf16 = false) {
		if (utf16) pos = aaaPos8(pos);
		return Call(SCI_INDICATORVALUEAT, indic, pos);
	}
	
	/// <summary>
	/// SCI_INDICATORALLONFOR. Returns a bitmap value representing which indicators are non-zero at <i>pos</i>.
	/// </summary>
	public int aaaIndicatorGetAll(int pos, bool utf16 = false) {
		if (utf16) pos = aaaPos8(pos);
		return Call(SCI_INDICATORALLONFOR, pos);
	}
	
	#endregion
	
	#region margins
	
	/// <summary>
	/// SCI_SETMARGINTYPEN. Optionally SCI_SETMARGINSENSITIVEN, SCI_SETMARGINCURSORN, SCI_SETMARGINMASKN.
	/// </summary>
	/// <param name="margin"></param>
	/// <param name="type">SC_MARGIN_.</param>
	/// <param name="markersMask">SCI_SETMARGINMASKN.</param>
	/// <param name="sensitive">SCI_SETMARGINSENSITIVEN.</param>
	/// <param name="cursorArrow">SCI_SETMARGINCURSORN. True SC_CURSORARROW, false SC_CURSORREVERSEARROW.</param>
	public void aaaMarginSetType(int margin, int type, int? markersMask = null, bool? sensitive = null, bool? cursorArrow = null) {
		Call(SCI_SETMARGINTYPEN, margin, type);
		if (markersMask.HasValue) Call(SCI_SETMARGINMASKN, margin, markersMask.Value);
		if (sensitive.HasValue) Call(SCI_SETMARGINSENSITIVEN, margin, sensitive.Value);
		if (cursorArrow.HasValue) Call(SCI_SETMARGINCURSORN, margin, cursorArrow == true ? SC_CURSORARROW : SC_CURSORREVERSEARROW);
	}
	
	/// <summary>
	/// SCI_SETMARGINWIDTHN or SCI_SETMARGINLEFT or SCI_SETMARGINRIGHT.
	/// Dpi-scales, and will scale when DPI changed.
	/// </summary>
	/// <param name="margin">Margin index for SCI_SETMARGINWIDTHN, or -1 to use SCI_SETMARGINLEFT, or -2 to use SCI_SETMARGINRIGHT.</param>
	/// <param name="pixels">Logical pixels or 0.</param>
	/// <param name="chars">Number of character '8' widths or 0. If both <i>pixels</i> and <i>chars</i> are not 0, uses their sum.</param>
	public void aaaMarginSetWidth(int margin, int pixels, int chars = 0) {
		_marginDpi ??= new (short, short)[Call(SCI_GETMARGINS) + 2];
		_marginDpi[margin + 2] = ((short)pixels, (short)chars);
		_MarginSetWidth(margin, pixels, chars);
	}
	(short pixels, short chars)[] _marginDpi; //logical pixels
	
	void _MarginWidthsDpiChanged() {
		if (_marginDpi is { } a)
			for (int i = a.Length; --i >= 0;) if (a[i] != default) _MarginSetWidth(i - 2, a[i].pixels, a[i].chars);
	}
	
	void _MarginSetWidth(int i, int pixels, int chars) {
		if (pixels != 0) pixels = Dpi.Scale(pixels, _dpi);
		if (chars != 0) pixels += chars * aaaStyleMeasureStringWidth(STYLE_LINENUMBER, "8");
		if (i == -2) Call(SCI_SETMARGINRIGHT, 0, pixels); else if (i == -1) Call(SCI_SETMARGINLEFT, 0, pixels); else Call(SCI_SETMARGINWIDTHN, i, pixels);
	}
	
	/// <returns>Margin index, or -1 if not in a margin.</returns>
	public int aaaMarginFromPoint(POINT p, bool screenCoord = false) {
		if (screenCoord) _w.MapScreenToClient(ref p);
		if (_w.ClientRect.Contains(p)) {
			for (int i = 0, n = Call(SCI_GETMARGINS), w = 0; i < n; i++) { w += Call(SCI_GETMARGINWIDTHN, i); if (w >= p.x) return i; }
		}
		return -1;
	}
	
	/// <summary>
	/// SCI_GETMARGINWIDTHN.
	/// </summary>
	public (int left, int right) aaaMarginGetX(int margin, bool dpiUnscale = false) {
		int x = 0;
		for (int i = 0; i < margin; i++) x += Call(SCI_GETMARGINWIDTHN, i);
		var r = (left: x, right: x + Call(SCI_GETMARGINWIDTHN, margin));
		if (dpiUnscale) {
			r.left = Dpi.Unscale(r.left, _dpi).ToInt();
			r.right = Dpi.Unscale(r.right, _dpi).ToInt();
		}
		return r;
	}
	
	/// <summary>
	/// Initializes folding margin and optionally separator marker.
	/// </summary>
	/// <param name="foldMargin">Margin index.</param>
	/// <param name="separatorMarker">Separator marker index, or -1 if never using seperators in this control.</param>
	/// <param name="autoFold">Set SC_AUTOMATICFOLD_CLICK.</param>
	public void aaaFoldingInit(int foldMargin = 0, int separatorMarker = -1, bool autoFold = false) {
		Call(SCI_SETMARGINTYPEN, foldMargin, SC_MARGIN_SYMBOL);
		Call(SCI_SETMARGINMASKN, foldMargin, SC_MASK_FOLDERS);
		Call(SCI_SETMARGINSENSITIVEN, foldMargin, 1);
		
		Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEROPEN, SC_MARK_BOXMINUS);
		Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDER, SC_MARK_BOXPLUS);
		Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERSUB, SC_MARK_VLINE);
		Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERTAIL, SC_MARK_LCORNER);
		Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEREND, SC_MARK_BOXPLUSCONNECTED);
		Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEROPENMID, SC_MARK_BOXMINUSCONNECTED);
		Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERMIDTAIL, SC_MARK_TCORNER);
		for (int i = 25; i < 32; i++) {
			Call(SCI_MARKERSETFORE, i, 0xffffff);
			Call(SCI_MARKERSETBACK, i, 0x808080);
			Call(SCI_MARKERSETBACKSELECTED, i, i == SC_MARKNUM_FOLDER ? 0xFF : 0x808080);
		}
		//Call(SCI_MARKERENABLEHIGHLIGHT, 1); //red [+]
		
		int fflags = SC_AUTOMATICFOLD_SHOW //show hidden lines when header line deleted. Also when hidden text modified, and it is not always good.
									| SC_AUTOMATICFOLD_CHANGE; //show hidden lines when header line modified like '#region' -> '//#region'
		if (autoFold) fflags |= SC_AUTOMATICFOLD_CLICK;
		Call(SCI_SETAUTOMATICFOLD, fflags);
		Call(SCI_SETFOLDFLAGS, SC_FOLDFLAG_LINEAFTER_CONTRACTED);
		Call(SCI_FOLDDISPLAYTEXTSETSTYLE, SC_FOLDDISPLAYTEXT_STANDARD);
		aaaStyleForeColor(STYLE_FOLDDISPLAYTEXT, 0x808080);
		
		Call(SCI_SETMARGINCURSORN, foldMargin, SC_CURSORARROW);
		
		aaaMarginSetWidth(foldMargin, 12);
		
		//separator lines below functions, types etc
		if (separatorMarker >= 0) aaaMarkerDefine(separatorMarker, SC_MARK_UNDERLINE, backColor: 0xa0a0a0);
	}
	
	/// <summary>
	/// Adds or updates fold points and optionally separators.
	/// </summary>
	/// <param name="af">Fold points. If null or empty, just clears old.</param>
	/// <param name="separatorMarker">Separator marker index, or -1 if never using seperators in this control. The marker should have an underline style.</param>
	public void aaaFoldingApply(List<SciFoldPoint> af, int separatorMarker = -1) {
		int underlinedLine = 0;
		
		int[] a = null;
		if (af != null) {
			a = new int[af.Count];
			for (int i = 0; i < a.Length; i++) {
				var v = af[i];
				int pos8 = aaaPos8(v.pos);
				a[i] = pos8 | (v.start ? 0 : unchecked((int)0x80000000));
				
				if (separatorMarker >= 0 && v.separator != 0) {
					//add separator below, or above if start
					if (v.start) { //above
						int k = v.pos - v.separator; if (k <= 0) continue;
						pos8 = aaaPos8(k);
					}
					int li = aaaLineFromPos(false, pos8);
					_DeleteUnderlinedLineMarkers(li);
					//if(underlinedLine != li) print.it("add", li + 1);
					if (underlinedLine != li) Call(SCI_MARKERADD, li, separatorMarker);
					else underlinedLine++;
				}
			}
		}
		
		if (separatorMarker >= 0) _DeleteUnderlinedLineMarkers(int.MaxValue);
		
		void _DeleteUnderlinedLineMarkers(int beforeLine) {
			if ((uint)underlinedLine > beforeLine) return;
			int marker = 1 << separatorMarker;
			for (; ; underlinedLine++) {
				underlinedLine = Call(SCI_MARKERNEXT, underlinedLine, marker);
				if ((uint)underlinedLine >= beforeLine) break;
				//print.it("delete", underlinedLine + 1);
				do Call(SCI_MARKERDELETE, underlinedLine, separatorMarker);
				while (0 != (marker & Call(SCI_MARKERGET, underlinedLine)));
			}
		}
		
		unsafe { //we implement folding in Scintilla. Calling many SCI_SETFOLDLEVEL here would be slow.
			fixed (int* ip = a) Sci_SetFoldLevels(AaSciPtr, 0, -1, a.Lenn_(), ip);
		}
		//p1.NW('F');
	}
	
	/// <summary>
	/// SCI_GETFOLDLEVEL.
	/// </summary>
	/// <returns>0-based level (never less than 0) and whether it has SC_FOLDLEVELHEADERFLAG.</returns>
	public (int level, bool isHeader) aaaFoldingLevel(int line) {
		int r = Call(SCI_GETFOLDLEVEL, line);
		return (Math.Max(0, (r & SC_FOLDLEVELNUMBERMASK) - SC_FOLDLEVELBASE), 0 != (r & SC_FOLDLEVELHEADERFLAG));
	}
	
	#endregion
}

/// <summary>
/// See <see cref="KScintilla.aaaFoldingApply"/>
/// </summary>
/// <param name="pos">A position anywhere in the line.</param>
/// <param name="start">Start or end.</param>
/// <param name="separator">If not 0, adds separator. If <i>start</i> true, adds above, at <c>pos-separator</c>; else adds below, and let it be 1.</param>
public record struct SciFoldPoint(int pos, bool start, ushort separator = 0);
