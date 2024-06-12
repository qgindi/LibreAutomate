extern alias CAW;

//#define SMALLER_SCREENSHOTS //smaller if /*image:...*/

using static Au.Controls.Sci;
using System.Drawing;
using System.Buffers;
using static Au.Controls.KImageUtil;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Classification;
using CAW::Microsoft.CodeAnalysis.Classification;
using CT = CAW::Microsoft.CodeAnalysis.Classification.ClassificationTypeNames;

partial class SciCode {
	struct _Image {
		public Bitmap image;
		public bool isImage; //draw frame; else icon, draw without frame
		public bool isComment; /*image:...*/
	}
	
	//fields for drawing images in margin
	struct _Images {
		public List<_Image> a; //images retrieved by _ImagesGet on styling
		public Dictionary<string, Bitmap> cache; //image cache of this document. Used only for non-icon images; for icons we use the common cache.
		public Sci_MarginDrawCallback callback;
		public IntPtr callbackPtr;
	}
	_Images _im;
	
	//Called by CiStyling._StylingAndFolding.
	internal void EImagesGet_(CodeInfo.Context cd, ClassifiedSpan[] a, in Sci_VisibleRange vr) {
		//if (App.Settings.edit_noImages) return; //caller does it, because it has some more work to do if need images
		//using var p1 = perf.local(); //fast when bitmaps loaded/cached
		
		if (a.Length > 0) aaaIndicatorClear(c_indicImages, true, a[0].TextSpan.Start..a[^1].TextSpan.End);
		
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
			} else if (i < a.Length && a[i].ClassificationType == CT.Comment) {
				var ts = a[i].TextSpan;
				if (!code.Eq(ts.Start, "/*")) continue;
				int j = ts.Start + 2;
				while (j < ts.End && code[j] <= ' ') j++;
				if (!code.Eq(j, "image:")) continue;
				int k = code.Find("*/", j..ts.End); if (k <= j) continue;
				s = code[j..k];
				imType = ImageType.Base64Image;
				isComment = true;
			} else if (null != (s = _IsFoldersEtc(a[i], ref i, out imType))) {
				if (imType == 0) imType = _ImageTypeFromString(true, s);
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
			int line = aaaLineFromPos(true, start), vi = Call(SCI_VISIBLEFROMDOCLINE, line);
			if (vi >= vr.vlineFrom && vi < vr.vlineTo && 0 != Call(SCI_GETLINEVISIBLE, line))
				maxWidth = Math.Max(maxWidth, _ImageDisplaySize(!isImage, b, isComment).Width);
			
			if (_im.a == null) {
				_im.a = new();
				Call(SCI_INDICSETSTYLE, c_indicImages, INDIC_HIDDEN);
				int descent = Dpi.Scale(16, _dpi) - Call(SCI_TEXTHEIGHT) + Call(SCI_GETEXTRADESCENT) + Call(SCI_GETEXTRAASCENT);
				if (descent > 0) {
					bool caretVisible = AaWnd.ClientRect.Contains(0, Call(SCI_POINTYFROMPOSITION, 0, aaaCurrentPos8));
					Call(SCI_SETEXTRAASCENT, descent / 2);
					Call(SCI_SETEXTRADESCENT, descent - descent / 2);
					//note: later don't restore when no visible images. Then bad scrolling and can start to repeat.
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
			
			aaaIndicatorAdd(c_indicImages, true, start..(start + 1), ii + 1);
		}
		
		//maxWidth is 0 if no images or if all images are in folded regions.
		if (maxWidth > 0) maxWidth = Math.Min(maxWidth, Dpi.Scale(100, _dpi)) + 8;
		var (left, right) = aaaMarginGetX(c_marginImages);
		_ImagesMarginAutoWidth(right - left, maxWidth);
		if (maxWidth > 0) Api.InvalidateRect(AaWnd, new RECT(left, 0, maxWidth, short.MaxValue));
		//TODO3: draw only when need, ie when new indicators are different than old.
		//	Now draws on each text change, eg added character, unless changes are frequent. But not too slow.
		//	And probably then also draws all other margins.
		
		#region local util
		
		bool _Eq(int i, string ctype, string text = null)
			=> (uint)i < a.Length && a[i].ClassificationType == ctype && (text == null || code.Eq(a[i].TextSpan, text));
		
		bool _IsString(ClassifiedSpan v, out CiStringRange r) {
			r = default;
			bool verbatim = false;
			var ct = v.ClassificationType;
			if (!(ct == CT.StringLiteral || (verbatim = ct == CT.VerbatimStringLiteral))) return false;
			//skip short strings and $"string" parts
			int start = v.TextSpan.Start, end = v.TextSpan.End - 1;
			if (verbatim && code[start++] != '@') return false;
			if (end - start < 3 || code[end] != '"' || code[start++] != '"') return false;
			r = new(code, start, end, verbatim);
			return true;
		}
		
		string _IsFoldersEtc(ClassifiedSpan v, ref int i, out ImageType imageType) {
			imageType = 0;
			if (_Eq(i, CT.ClassName, "folders")) {
				if (_Eq(++i, CT.Operator, ".")) {
					int i1 = ++i;
					if (_Eq(i, CT.PropertyName)
						|| (_Eq(i, CT.ClassName, "shell") && _Eq(++i, CT.Operator, ".") && _Eq(++i, CT.PropertyName))
						) {
						var fp = folders.getFolder(code[a[i1].TextSpan.Start..a[i].TextSpan.End]);
						if (!fp.IsNull) return _Plus(fp, ref i);
					} else if (_Eq(i = i1, CT.MethodName) && _Eq(i + 1, CT.Punctuation, "(") && _Eq(i + 2, CT.Punctuation, ")")) {
						i += 2;
						if (code[a[i1].TextSpan.ToRange()] == "sourceCode") return _Plus(folders.sourceCode(_fn.FilePath), ref i);
					}
				}
			} else if (_Eq(i, CT.EnumName, "StockIcon")) {
				if (_Eq(++i, CT.Operator, ".") && _Eq(++i, CT.EnumMemberName))
					if (Enum.TryParse(code.AsSpan(a[i].TextSpan.ToRange()), out StockIcon si))
						if (icon.GetStockIconLocation_(si, out string path, out int index)) {
							imageType = ImageType.IconLib;
							return path + "," + index.ToS();
						}
			}
			return null;
			
			string _Plus(FolderPath fp, ref int i) {
				//print.it("FOLDERS", fp.Path);
				if (i < a.Length - 2 && (_Eq(i + 1, CT.OperatorOverloaded, "+") || _Eq(i + 1, CT.Operator, "+")) && _IsString(a[i + 2], out var r)) {
					i += 2;
					return fp + r.ToString();
				}
				return fp.Path;
			}
		}
		
		static ImageType _ImageTypeFromString(bool folders, RStr s/*, out int prefixLength*/) {
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
				if (DIcons.PossiblyIconName(s)) return ImageType.XamlIconName;
				return default;
			case '.':
				return pathname.IsExtension_(s) ? ImageType.ShellIcon : default;
			case ':':
				return s[1] == ':' ? ImageType.ShellIcon : default;
			}
			
			//file path or URL
			//string expanded = null;
			//if (s[0] == '%') {
			//	expanded = pathname.expand(s.ToString(), strict: false);
			//	if (expanded.Length < 8 || expanded[0] == '%') return default;
			//	s = expanded;
			//}
			if (pathname.isFullPath(s, orEnvVar: true)) { //is image file path?
				if (s.Length >= 8) { //can be image file. Else can be eg "C:\" or "C:\A".
					if (s[^4] == '.') {
						var ext = s[^3..];
						if (ext.Eqi("png") || ext.Eqi("gif") || ext.Eqi("jpg")) return ImageType.PngGifJpg;
						if (ext.Eqi("bmp")) return ImageType.Bmp;
						if (ext.Eqi("ico")) return ImageType.Ico;
						if (ext.Eqi("cur") || ext.Eqi("ani")) return ImageType.Cur;
					} else if (s[^1].IsAsciiDigit() && s.Contains(',')) { //can be like C:\x.dll,10
						if (icon.parsePathIndex(s.ToString(), out _, out _)) return ImageType.IconLib;
					}
				} else if (s.Length < 4 && s[1] != ':') return default; //eg "\\"
			} else {
				if (s.Length < 4) return default;
				if (pathname.isUrl(s)) {
					//display icon only for known protocols that can be used with run.it(). Ignore "web:LINK" etc and protocols with common icon (eg "http:").
					if (!(s.Starts("shell:") || s.Starts("ms-settings:"))) return default;
				} else if (!s.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return default;
			}
			
			return ImageType.ShellIcon;
		}
		
		#endregion
	}
	
	unsafe void _ImagesMarginDrawCallback(ref Sci_MarginDrawCallbackData c) {
		//print.it(c.rect, c.firstLine, c.lastLine);
		//using var p1 = perf.local();
		
		int pos = aaaLineStart(false, c.firstLine) - 1, posEnd = aaaLineEnd(false, c.lastLine);
		int topVisibleLine = Call(SCI_GETFIRSTVISIBLELINE), lineH = Call(SCI_TEXTHEIGHT);
		int maxWidth = 0;
		Graphics g = null;
		try {
			for (; ; pos++) {
				pos = Call(SCI_INDICATOREND, c_indicImages, pos); //skip non-indicator range
				if (pos <= 0 || pos >= posEnd) break; //after the visible range or at the end of text
				int i = Call(SCI_INDICATORVALUEAT, c_indicImages, pos) - 1;
				if ((uint)i >= _im.a.Count) break; //should never
				int line = aaaLineFromPos(false, pos);
				if (0 == Call(SCI_GETLINEVISIBLE, line)) continue; //folded?
				
				//print.it(pos, i, line);
				var v = _im.a[i];
#if SMALLER_SCREENSHOTS
				bool smaller = v.isComment;
				var z = _ImageDisplaySize(!v.isImage, v.image, smaller);
#else
				var z = _ImageDisplaySize(!v.isImage, v.image, false);
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
				g.DrawImage(v.image, r);
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
			if (maxWidth > oldWidth || (c.rect.top == 0 && c.rect.bottom == AaWnd.ClientRect.bottom))
				_ImagesMarginAutoWidth(oldWidth, maxWidth);
	}
	
	void _ImagesMarginAutoWidth(int oldWidth, int width) {
		if (width == oldWidth) return;
		//when shrinking, in wrap mode could start autorepeating, when makes less lines wrapped and it uncovers wider images at the bottom and need to expand again.
		//	Tried to delay or to not change if changed recently, but not good. Never mind.
		if (width < oldWidth && App.Settings.edit_wrap) return;
		AaWnd.Post(SCI_SETMARGINWIDTHN, c_marginImages, width);
	}
	
	void _ImagesOnOff() {
		if (App.Settings.edit_noImages == (_im.a == null)) return;
		if (_im.a != null) {
			Call(SCI_SETMARGINDRAWCALLBACK);
			Call(SCI_SETMARGINWIDTHN, c_marginImages, 0);
			Call(SCI_SETEXTRAASCENT, 0);
			Call(SCI_SETEXTRADESCENT, 1);
			aaaIndicatorClear(c_indicImages);
			_im.a = null;
		} else {
			if (this == Panels.Editor.ActiveDoc) CodeInfo._styling.Update();
		}
	}
	
	Size _ImageDisplaySize(bool isIcon16, Bitmap b, bool smaller) {
		var z = b.Size;
		if (isIcon16) {
			var k = Dpi.Scale(16, _dpi);
			z = new(k, k);
		} else {
			int hr = b.HorizontalResolution.ToInt(), vr = b.VerticalResolution.ToInt();
			if (hr >= 96 && vr >= 96) z = new(Math2.MulDiv(z.Width, _dpi, hr), Math2.MulDiv(b.Height, _dpi, vr));
		}
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
	internal unsafe void EHideImages_(int from8, int to8, byte[] styles = null, [CallerMemberName] string caller = null) {
		if (styles == null) from8 = aaaLineStartFromPos(false, from8);
		if (to8 - from8 < 49) return;
		
		var r = new Au.Controls.SciDirectRange(this, from8, to8);
		for (int j = from8 + 2, to2 = to8 - 47; j <= to2;) {
			int i = j;
			for (; ; i++) {
				if (i > to2) return;
				if (r[i] == 'i' && r[i + 1] == 'm' && r[i + 2] == 'a' && r[i + 3] == 'g' && r[i + 4] == 'e' && r[i + 5] == ':') break;
			}
			j = i + 6;
			
			char c1 = r[i - 1], c2 = r[i - 2];
			if (!((c1 == '*' && c2 == '/') || (c1 == '"' && c2 == '@'))) continue;
			
			int j2 = to8 - (c1 == '"' ? 1 : 2);
			while (j < j2 && r[j] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/') j++;
			if (j - i < 46) continue; //match regex image:[A-Za-z0-9/+]{40,}
			while (j < j2 && r[j] == '=') j++;
			
			if (r[j] != c1 || (c1 == '*' && '/' != r[j + 1])) continue;
			if (c1 == '"') i += 6; else { i -= 2; j += 2; }
			
			if (styles != null) {
				styles.AsSpan(i - from8, j - i).Fill(STYLE_HIDDEN);
			} else {
				Call(SCI_STARTSTYLING, i);
				Call(SCI_SETSTYLING, j - i, STYLE_HIDDEN);
			}
		}
		
		//note: don't use SCI_SETTARGETRANGE here. In some cases scintilla does not work well with it. It may create bugs.
	}
	//Not easy to use hidden style because:
	//	1. Scintilla bug: in wrap mode sometimes draws as many lines as with big font. Even caret is large and spans all lines.
	//		Plus other anomalies, eg when scrolling.
	//		Workaround: at first hide all on SCN_STYLENEEDED.
	//	2. User cannot delete text containing hidden text.
	//		Workaround: modify scintilla source in Editor::RangeContainsProtected.
	
	bool _ImageDeleteKey(KKey key) {
		if (key is KKey.Delete or KKey.Back && !base.aaaHasSelection) {
			int pos = base.aaaCurrentPos8, to = pos;
			if (key == KKey.Back) {
				while (aaaStyleGetAt(pos - 1) == STYLE_HIDDEN) pos--;
			} else {
				while (aaaStyleGetAt(to) == STYLE_HIDDEN) to++;
			}
			if (to > pos) {
				bool ok = s_imageDeleteAlways;
				if (!ok) {
					var s = base.aaaRangeText(false, pos, to).Limit(50);
					var c = new DControls { Checkbox = "Remember" };
					ok = 1 == dialog.show("Delete hidden text?", s, "OK|Cancel", controls: c, owner: this);
					s_imageDeleteAlways = ok && c.IsChecked;
				}
				if (ok) {
					aaaDeleteRange(false, pos, to);
					aaaAddUndoPoint();
					return true;
				}
			}
		}
		return false;
	}
	static bool s_imageDeleteAlways;
	
	static string _ImageRemoveScreenshots(string s, bool onCopy) {
		//s = s.RxReplace(@"/\*image:[A-Za-z0-9/+]{40,}=*\*/", "");
		//var s2 = s.RxReplace(@"""image:[A-Za-z0-9/+]{40,}=*""", @"""image:""");
		//if (s2 != s)
		//	if (dialog.showYesNo("Remove all images?", "This will replace strings \"image:<hidden image data>\" with \"image:\" in the copied text.")) s = s2;
		
		int n = s.RxReplace(@"/\*image:[A-Za-z0-9/+]{40,}=*\*/", "", out s);
		if (onCopy && n > 0) print.it($"Info: in the copied code have been removed {n} images embedded in hidden comments. The script can run without them.");
		n = s.RxReplace(@"""image:[A-Za-z0-9/+]{40,}=*""", @"""image:""", out s);
		if (onCopy && n > 0) print.it($"Info: in the copied code have been removed {n} images embedded in strings \"image:<hidden image data>\". You can copy-paste the strings separately if necessary.");
		return s;
	}
	
	public void EImageRemoveScreenshots() {
		if (aaaIsReadonly || !_fn.IsCodeFile) return;
		bool isSel = aaaHasSelection;
		string s = isSel ? aaaSelectedText() : aaaText;
		var s2 = _ImageRemoveScreenshots(s, false);
		if (s2 == s) return;
		if (isSel) EReplaceTextGently(aaaSelectionStart8, aaaSelectionEnd8, s2);
		else EReplaceTextGently(s2);
	}
}
