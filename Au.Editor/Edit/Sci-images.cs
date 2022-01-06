//#define SMALLER_SCREENSHOTS //smaller if /*image:...*/

using System.Linq;
using static Au.Controls.Sci;
using System.Drawing;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Shared.Extensions;
using static Au.Controls.KImageUtil;
using System.Buffers;

partial class SciCode
{
	struct _Image
	{
		public Bitmap image;
		public bool isImage; //draw frame; else icon, draw without frame
		public bool isComment; /*image:...*/
	}

	//fields for drawing images in margin
	struct _Images
	{
		public List<_Image> a; //images retrieved by _ImagesGet on styling
		public Dictionary<string, Bitmap> cache; //image cache of this document. Used only for non-icon images; for icons we use the common cache.
		public Sci_MarginDrawCallback callback;
		public IntPtr callbackPtr;
	}
	_Images _im;

	//Called by CiStyling._StylingAndFolding.
	internal void _ImagesGet(CodeInfo.Context cd, IEnumerable<ClassifiedSpan> list, in Sci_VisibleRange vr) {
		if (App.Settings.edit_noImages) return;
		//using var p1 = perf.local(); //fast when bitmaps loaded/cached

		//remove StaticSymbol. It is added for each static symbol, randomly before or after. Makes code difficult.
		var a = list.Where(o => o.ClassificationType != ClassificationTypeNames.StaticSymbol).ToArray();

		if (a.Length > 0) zIndicatorClear(true, c_indicImages, a[0].TextSpan.Start..a[^1].TextSpan.End);

		string code = cd.code;
		int maxWidth = 0;
		int nextLineStart = 0;

		//CONSIDER: prefer /*image:...*/? Now, if before is eg "file path", displays file icon. Or draw both somehow.

		for (int i = 0; i < a.Length; i++) {
			if (a[i].TextSpan.Start < nextLineStart) continue; //max 1 image/line
			string s;
			ImageType imType = 0;
			bool isComment = false;
			if (_IsString(a[i], out var sr)) {
				imType = _ImageTypeFromString(false, code.AsSpan(sr.start, sr.Length));
				if (imType == 0) continue;
				s = sr.ToString();
			} else if (null != (s = _IsFolders(a[i], ref i))) {
				imType = _ImageTypeFromString(true, s);
			} else if (i < a.Length && a[i].ClassificationType == ClassificationTypeNames.Comment) {
				var ts = a[i].TextSpan;
				if (!code.Eq(ts.Start, "/*")) continue;
				int j = ts.Start + 2;
				while (j < ts.End && code[j] <= ' ') j++;
				if (!code.Eq(j, "image:")) continue;
				int k = code.Find("*/", j..ts.End); if (k <= j) continue;
				s = code[j..k];
				imType = ImageType.Base64Image;
				isComment = true;
			}
			if (imType == 0) continue;
			Bitmap b;
			bool isImage = imType is ImageType.Base64Image or ImageType.PngGifJpg or ImageType.Bmp or ImageType.Xaml;
			if (isImage) {
				if (!(_im.cache ??= new()).TryGetValue(s, out b)) {
					try { b = ImageUtil.LoadGdipBitmap(s, (_dpi, null)); }
					catch { b = null; }
					_im.cache[s] = b;
				}
			} else {
				b = IconImageCache.Common.Get(s, _dpi, imType == ImageType.XamlIconName);
			}
			if (b == null) continue;

			nextLineStart = code.IndexOf('\n', a[i].TextSpan.End) + 1;
			if (nextLineStart == 0) nextLineStart = code.Length;

			int start = a[i].TextSpan.Start;
			int line = zLineFromPos(true, start), vi = Call(SCI_VISIBLEFROMDOCLINE, line);
			if (vi >= vr.vlineFrom && vi < vr.vlineTo && 0 != Call(SCI_GETLINEVISIBLE, line))
				maxWidth = Math.Max(maxWidth, _ImageDisplaySize(b, isComment).Width);

			if (_im.a == null) {
				_im.a = new();
				Call(SCI_INDICSETSTYLE, c_indicImages, INDIC_HIDDEN);
				int descent = 16 - Call(SCI_TEXTHEIGHT) + Call(SCI_GETEXTRADESCENT);
				if (descent > 0) {
					bool caretVisible = Hwnd.ClientRect.Contains(0, Call(SCI_POINTYFROMPOSITION, 0, zCurrentPos8));
					Call(SCI_SETEXTRADESCENT, descent); //note: later don't set = 0 when no visible images. Then bad scrolling and can start to repeat.
					if (caretVisible) Call(SCI_SCROLLCARET);
				}
				if (_im.callback == null) _im.callbackPtr = Marshal.GetFunctionPointerForDelegate(_im.callback = _ImagesMarginDrawCallback);
				Call(SCI_SETMARGINDRAWCALLBACK, 1 << c_marginImages, _im.callbackPtr);
			}
			var ab = _im.a;
			int ii;
			for (ii = 0; ii < ab.Count; ii++) if (ab[ii].image == b) break;
			if (ii == ab.Count) ab.Add(new() { image = b, isImage = isImage, isComment = isComment });
			//print.it(ii, s);

			zIndicatorAdd(true, c_indicImages, start..(start + 1), ii + 1);
		}

		//maxWidth is 0 if no images or if all images are in folded regions.
		if (maxWidth > 0) maxWidth = Math.Min(maxWidth, Dpi.Scale(100, _dpi)) + 8;
		var (left, right) = zGetMarginX(c_marginImages);
		_ImagesMarginAutoWidth(right - left, maxWidth);
		if (maxWidth > 0) Api.InvalidateRect(Hwnd, new RECT(left, 0, maxWidth, short.MaxValue));
		//SHOULDDO: draw only when need, ie when new indicators are different than old.
		//	Now draws on each text change, eg added character, unless changes are frequent. But not too slow.
		//	And probably then also draws all other margins.

		#region local util

		bool _Eq(int i, string ctype, string text = null)
			=> (uint)i < a.Length && a[i].ClassificationType == ctype && (text == null || code.Eq(a[i].TextSpan, text));

		bool _IsString(ClassifiedSpan v, out CiStringRange r) {
			r = default;
			bool verbatim = false;
			var ct = v.ClassificationType;
			if (!(ct == ClassificationTypeNames.StringLiteral || (verbatim = ct == ClassificationTypeNames.VerbatimStringLiteral))) return false;
			//skip short strings and $"string" parts
			int start = v.TextSpan.Start, end = v.TextSpan.End - 1;
			if (verbatim && code[start++] != '@') return false;
			if (end - start < 3 || code[end] != '\"' || code[start++] != '\"') return false;
			r = new(code, start, end, verbatim);
			return true;
		}

		string _IsFolders(ClassifiedSpan v, ref int i) {
			if (_Eq(i, ClassificationTypeNames.ClassName, "folders") && _Eq(++i, ClassificationTypeNames.Operator, ".")) {
				int i1 = ++i;
				if (_Eq(i, ClassificationTypeNames.PropertyName)
					|| (_Eq(i, ClassificationTypeNames.ClassName, "shell") && _Eq(++i, ClassificationTypeNames.Operator, ".") && _Eq(++i, ClassificationTypeNames.PropertyName))
					) {
					var fp = folders.getFolder(code[a[i1].TextSpan.Start..a[i].TextSpan.End]);
					if (!fp.IsNull) {
						//print.it("FOLDERS", fp.Path);
						if (i < a.Length - 2 && _Eq(i + 1, ClassificationTypeNames.OperatorOverloaded, "+") && _IsString(a[i + 2], out var r)) {
							i += 2;
							return fp + r.ToString();
						}
						return fp.Path;
					}
				}
			}
			return null;
		}

		static ImageType _ImageTypeFromString(bool folders, ReadOnlySpan<char> s/*, out int prefixLength*/) {
			//prefixLength = 0;
			if (s.Length < 2) return default;

			//special strings
			switch (s[0]) {
			case 'i' when s.StartsWith("image:"):
				//prefixLength = 6;
				return !folders && s.Length >= 10 ? ImageType.Base64Image : default;
			case 'i' when s.StartsWith("imagefile:"):
				if (folders) return default;
				s = s[10..];
				//prefixLength = 10;
				break;
			case '<':
				if (s.Contains("xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'", StringComparison.Ordinal)) {
					if (s.Contains("<Path ", StringComparison.Ordinal) || s.Contains("<GeometryDrawing ", StringComparison.Ordinal)) return ImageType.Xaml;
				}
				return default;
			case '*':
				if (s.Length is > 10 and < 80) {
					int i = s.IndexOf('.') + 1;
					if (i > 3 && s[i..].Contains(' ')) return ImageType.XamlIconName;
				}
				return default;
			case '.':
				return pathname.IsExtension_(s) ? ImageType.ShellIcon : default;
			case ':':
				return s[1] == ':' ? ImageType.ShellIcon : default;
			}

			//file path or URL
			if (s.Length < 8) return default;
			//string expanded = null;
			//if (s[0] == '%') {
			//	expanded = pathname.expand(s.ToString(), strict: false);
			//	if (expanded.Length < 8 || expanded[0] == '%') return default;
			//	s = expanded;
			//}
			if (pathname.isFullPath(s, orEnvVar: true)) { //is image file path?
				if (s[^4] == '.') {
					var ext = s[^3..];
					if (ext.Eqi("png") || ext.Eqi("gif") || ext.Eqi("jpg")) return ImageType.PngGifJpg;
					if (ext.Eqi("bmp")) return ImageType.Bmp;
					if (ext.Eqi("ico")) return ImageType.Ico;
					if (ext.Eqi("cur") || ext.Eqi("ani")) return ImageType.Cur;
				} else if (s[^1].IsAsciiDigit() && s.Contains(',')) { //can be like C:\x.dll,10
					if (icon.parsePathIndex(s.ToString(), out _, out _)) return ImageType.IconLib;
				}
			} else if (pathname.isUrl(s)) {
				//display icon only if it is a known frequently used protocol that can be used with run.it(). Avoid non-protocol prefixes such as "web:LINK".
				//if (!(s.Starts("http:") || s.Starts("https:") || s.Starts("mailto:") || s.Starts("shell:"))) return default;
				if (!s.Starts("shell:")) return default; //don't display http etc, it's not useful
			} else if (!s.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return default;

			return ImageType.ShellIcon;
		}

		#endregion
	}

	unsafe void _ImagesMarginDrawCallback(ref Sci_MarginDrawCallbackData c) {
		//print.it(c.rect, c.firstLine, c.lastLine);
		//using var p1 = perf.local();

		int pos = zLineStart(false, c.firstLine) - 1, posEnd = zLineEnd(false, c.lastLine);
		int topVisibleLine = Call(SCI_GETFIRSTVISIBLELINE), lineH = Call(SCI_TEXTHEIGHT);
		int maxWidth = 0;
		Graphics g = null;
		try {
			for (; ; pos++) {
				pos = Call(SCI_INDICATOREND, c_indicImages, pos); //skip non-indicator range
				if (pos <= 0 || pos >= posEnd) break; //after the visible range or at the end of text
				int i = Call(SCI_INDICATORVALUEAT, c_indicImages, pos) - 1;
				if ((uint)i >= _im.a.Count) break; //should never
				int line = zLineFromPos(false, pos);
				if (0 == Call(SCI_GETLINEVISIBLE, line)) continue; //folded?

				//print.it(pos, i, line);
				var v = _im.a[i];
#if SMALLER_SCREENSHOTS
				bool smaller = v.isComment;
				var z = _ImageDisplaySize(v.image, smaller);
#else
				var z = v.image.Size;
#endif
				maxWidth = Math.Max(maxWidth, z.Width);
				int x = c.rect.CenterX - z.Width / 2;
				int y = (Call(SCI_VISIBLEFROMDOCLINE, line) - topVisibleLine) * lineH;

				RECT r = new(x, y, z.Width, z.Height);
				if (!c.rect.IntersectsWith(r)) continue;
				if (g == null) {
					g = Graphics.FromHdc(c.hdc);
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				}

				g.IntersectClip(c.rect); //limit image width, because not clipped

				if (v.isImage) g.DrawRectangleInset(Color.Green, 1, r, outset: true);
#if SMALLER_SCREENSHOTS
				if (smaller) g.DrawImage(v.image, r); else
#endif
				g.DrawImageUnscaled(v.image, x, y);
			}
		}
		finally {
			g?.Dispose();
		}

		//auto-correct margin width, in case _ImagesGet not called because styling is valid.
		//	Need it eg after expanding/collapsing a folding containing images. Also sometimes after resizing the control or after zoom changed.
		if (maxWidth > 0) maxWidth = Math.Min(maxWidth, Dpi.Scale(100, _dpi)) + 8;
		int oldWidth = c.rect.Width;
		if (maxWidth != oldWidth)
			if (maxWidth > oldWidth || (c.rect.top == 0 && c.rect.bottom == Hwnd.ClientRect.bottom))
				_ImagesMarginAutoWidth(oldWidth, maxWidth);
	}

	void _ImagesMarginAutoWidth(int oldWidth, int width) {
		if (width == oldWidth) return;
		//when shrinking, in wrap mode could start autorepeating, when makes less lines wrapped and it uncovers wider images at the bottom and need to expand again.
		//	Tried to delay or to not change if changed recently, but not good. Never mind.
		if (width < oldWidth && App.Settings.edit_wrap) return;
		Hwnd.Post(SCI_SETMARGINWIDTHN, c_marginImages, width);
	}

	void _ImagesOnOff() {
		if (App.Settings.edit_noImages == (_im.a == null)) return;
		if (_im.a != null) {
			Call(SCI_SETMARGINDRAWCALLBACK);
			Call(SCI_SETMARGINWIDTHN, c_marginImages, 0);
			Call(SCI_SETEXTRADESCENT, 1);
			zIndicatorClear(c_indicImages);
			_im.a = null;
		} else {
			if (this == Panels.Editor.ZActiveDoc) CodeInfo._styling.Update();
		}
	}

	static Size _ImageDisplaySize(Bitmap b, bool smaller) {
		var z = b.Size;
#if SMALLER_SCREENSHOTS
		if (smaller) return new(z.Width * 3 / 4, z.Height * 3 / 4);
#endif
		return z;
	}

	/// <summary>
	/// Finds all /*image:Base64*/ and @"image:Base64" in scintilla text range from8..to8 (UTF-8) and sets style STYLE_HIDDEN for the Base64.
	/// If <i>styles</i> != null, writes STYLE_HIDDEN in <i>styles</i>, else uses SCI_STARTSTYLING/SCI_SETSTYLING.
	/// </summary>
	/// <remarks>
	/// Called on SCN_STYLENEEDED (to avoid bad things like briefly visible and added horizontal scrollbar) and then by CiStyling._Work (async).
	/// </remarks>
	internal unsafe void HideImages_(int from8, int to8, byte[] styles = null) {
		if (styles == null) from8 = zLineStartFromPos(false, from8);
		if (to8 - from8 < 40) return;
		//print.it("HI", from8, to8, styles != null);
		int from0 = from8; from8 += 2;
		Call(SCI_SETSEARCHFLAGS, SCFIND_MATCHCASE | SCFIND_WORDSTART); //with SCFIND_REGEXP|SCFIND_CXX11REGEX simpler but slow, 3000 mcs/SCI_SEARCHINTARGET
		for (int j = 0; from8 < to8; from8 = j) {
			Call(SCI_SETTARGETRANGE, from8, to8);
			long li = 0x3A6567616D69; //print.it((ulong)BitConverter.ToInt64(Encoding.UTF8.GetBytes("image:\0\0")));
			int i = Call(SCI_SEARCHINTARGET, 6, &li);
			if (i < 0) break;
			j = i + 6;
			var dr = new Au.Controls.SciDirectRange(this, i - 2, to8);
			char c1 = dr[i - 1], c2 = dr[i - 2];
			if (!((c1 == '*' && c2 == '/') || (c1 == '\"' && c2 == '@'))) continue;
			for (int j2 = to8 - (c1 == '\"' ? 1 : 2); j < j2; j++) {
				if (dr[j] is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/' or '=')) break;
			}
			if (j - i < 40 || dr[j] != c1 || (c1 == '*' && '/' != dr[j + 1])) continue;
			if (c1 == '\"') i += 6; else { i -= 2; j += 2; }
			if (styles != null) {
				styles.AsSpan(i - from0, j - i).Fill(STYLE_HIDDEN);
			} else {
				Call(SCI_STARTSTYLING, i);
				Call(SCI_SETSTYLING, j - i, STYLE_HIDDEN);
			}
		}
	}
	//Not easy to use hidden style because:
	//	1. Scintilla bug: in wrap mode sometimes draws as many lines as with big font. Even caret is large and spans all lines.
	//		Plus other anomalies, eg when scrolling.
	//		Workaround: at first hide all on SCN_STYLENEEDED.
	//	2. User cannot delete text containing hidden text.
	//		Workaround: modify scintilla source in Editor::RangeContainsProtected.
}
