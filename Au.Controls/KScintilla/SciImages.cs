namespace Au.Controls;

using static Sci;

/// <summary>
/// Gets image file paths etc from <see cref="KScintilla"/> control text and displays the images below that lines.
/// </summary>
/// <remarks>
/// Draws images in annotation areas.
/// Supports text annotations too, below images and in no-image lines. But it is limited:
/// 1. To set/get it use <see cref="KScintilla.aaaAnnotationText(int, string, bool)"/>, not direct Scintilla API.
/// 2. You cannot hide all annotations (SCI_ANNOTATIONSETVISIBLE). This class sets it to show always.
/// 3. You cannot clear all annotations (SCI_ANNOTATIONCLEARALL).
/// 4. Setting annotation styles is currently not supported.
/// </remarks>
public unsafe class SciImages {
	class _Image {
		public long nameHash, evictionTime;
		public byte[] data;
		public int width, height;
	}
	
	KScintilla _c;
	IntPtr _callbackPtr;
	
	class _ThreadSharedData {
		List<_Image> _a;
		int _dpi;
		timer _timer;
		
		public int CacheSize { get; private set; }
		
		public void AddImage(_Image im) {
			_a ??= new();
			_a.Add(im);
			CacheSize += im.data.Length;
			
			im.evictionTime = Environment.TickCount64 + 2000;
			//rejected: keep im in cache longer if the loading was slow. Eg AV scans exe files when we extract icons.
			//	Usually only the first time is slow. Later the file is in the OS file cache.
			
			_timer ??= new(t => {
				var now = Environment.TickCount64;
				for (int i = _a.Count; --i >= 0;)
					if (now - _a[i].evictionTime > 0) {
						CacheSize -= _a[i].data.Length;
						_a.RemoveAt(i);
					}
				
				if (_a.Count == 0) _timer.Stop();
			});
			if (!_timer.IsRunning) _timer.Every(500);
		}
		
		public _Image FindImage(long nameHash, int dpi) {
			if (!_a.NE_()) {
				if (dpi != _dpi) {
					ClearCache();
					_dpi = dpi;
				}
				
				for (int j = 0; j < _a.Count; j++) if (_a[j].nameHash == nameHash) return _a[j];
			}
			return null;
		}
		
		public void ClearCache() {
			if (_a == null) return;
			_a.Clear();
			CacheSize = 0;
		}
		
		/// <summary>
		/// If cache is large (at least MaxCacheSize and 4 images), removes about 3/4 of older cached images.
		/// Will auto-reload from files etc when need.
		/// </summary>
		public void CompactCache() {
			if (_a == null) return;
			//print.it(_cacheSize);
			if (CacheSize < MaxCacheSize || _a.Count < 4) return;
			CacheSize = 0;
			int n = _a.Count, max = MaxCacheSize / 4;
			while (CacheSize < max && n > 2) CacheSize += _a[--n].data.Length;
			_a.RemoveRange(0, n);
		}
		
		public int MaxCacheSize { get; set; } = 4 * 1024 * 1024;
	}
	
	//All SciImages of a thread share single cache etc.
	[ThreadStatic] static _ThreadSharedData t_data;
	
	/// <summary>
	/// Prepares this variable and the Scintilla control to display images.
	/// Calls SCI_ANNOTATIONSETVISIBLE(ANNOTATION_STANDARD). Need it because will draw images in annotation areas.
	/// </summary>
	/// <param name="c">The control.</param>
	internal SciImages(KScintilla c) {
		t_data ??= new();
		_c = c;
		_sci_AnnotationDrawCallback = _AnnotationDrawCallback;
		_callbackPtr = Marshal.GetFunctionPointerForDelegate(_sci_AnnotationDrawCallback);
		_c.Call(SCI_SETANNOTATIONDRAWCALLBACK, 0, _callbackPtr);
		_visible = AnnotationsVisible.ANNOTATION_STANDARD;
		_c.Call(SCI_ANNOTATIONSETVISIBLE, (int)_visible); //keep annotations always visible. Adding annotations while visible is slower, but much faster than getting images from files etc.
	}
	
	/// <summary>
	/// Sets image annotations for one or more lines of text.
	/// </summary>
	/// <param name="firstLine">First line index.</param>
	/// <param name="text">Text that starts at line firstLine.</param>
	/// <param name="allText">Added all text (not edited or appended).</param>
	/// <param name="textPos">Position where the text starts.</param>
	[SkipLocalsInit]
	public void _SetImagesForTextRange(int firstLine, RByte text, bool allText, int textPos) {
		if (text.Length < 10) return; //"C:\x.ico"
		
		bool annotAdded = false;
		int iLine = firstLine;
		for (int i = 0; i < text.Length; iLine++) {
			int maxHeight = 0, totalWidth = 0;
			_Image u; bool isMulti = false; int imStrStart = 0;
			while (null != (u = _GetImageInLine(text, ref i, ref isMulti, ref imStrStart))) {
				if (maxHeight < u.height) maxHeight = u.height;
				if (totalWidth > 0) totalWidth += 30;
				totalWidth += u.width;
				
				//if(!isMulti) {
				//	bool hide = true; int imStrEnd = i;
				//	if(imStrEnd < iTo && text[imStrEnd] == '>') imStrEnd++;
				//	//print.it(imStrStart, imStrEnd);
				//	if(hide) {
				//		int len = imStrEnd - imStrStart;
				//		_c.Call(SCI_STARTSTYLING, imStrStart + textPos);
				//		_c.Call(SCI_SETSTYLING, len, STYLE_HIDDEN);
				//	}
				//}
			}
			
			if (maxHeight == 0) continue;
			
			int annotLen = _c.Call(SCI_ANNOTATIONGETTEXT, iLine); //we'll need old annotation text later, and we'll get it into the same buffer after the new image info
			
			//calculate n annotation lines from image height
			int lineHeight = _c.aaaLineHeight(); if (lineHeight <= 0) continue;
			int nAnnotLines = Math.Min((maxHeight + (lineHeight - 1)) / lineHeight, 255);
			//print.it(lineHeight, maxHeight, nAnnotLines);
			
			using FastBuffer<byte> buffer = new(annotLen + nAnnotLines + 20);
			var p = buffer.p;
			*p++ = 3; Api._ltoa(totalWidth << 8 | nAnnotLines, p, 16); while (*(++p) != 0) { }
			while (nAnnotLines-- > 1) *p++ = (byte)'\n';
			*p = 0;
			
			//An annotation possibly already exists. Possible cases:
			//1. No annotation. Need to add our image annotation.
			//2. A text-only annotation. Need to add our image annotation + that text.
			//3. Different image, no text. Need to replace it with our image annotation.
			//4. Different image + text. Need to replace it with our image annotation + that text.
			//5. This image, with or without text. Don't need to change.
			if (annotLen > 0) {
				//get existing annotation into the same buffer after our image info
				var a = p + 1;
				_c.Call(SCI_ANNOTATIONGETTEXT, iLine, a);
				a[annotLen] = 0;
				//print.it($"OLD: '{new string((sbyte*)a)}'");
				
				//is it our image info?
				int imageLen = (int)(p - buffer.p);
				if (annotLen >= imageLen) {
					int j;
					for (j = 0; j < imageLen; j++) if (a[j] != buffer[j]) goto g1;
					if (annotLen == imageLen || a[imageLen] == '\n') continue; //case 5
				}
				g1:
				//contains image?
				if (a[0] == 3) {
					int j = _ParseAnnotText(a, annotLen, out var _);
					if (j < annotLen) { //case 4
						Api.memmove(a, a + j, annotLen - j + 1);
						p[0] = (byte)'\n';
					} //else case 3
				} else { //case 2
					p[0] = (byte)'\n';
				}
			} //else case 1
			
			//print.it($"NEW: '{new string((sbyte*)b0.p)}'");
			//perf.first();
			if (!annotAdded) {
				annotAdded = true;
				if (allText) _c.Call(SCI_ANNOTATIONSETVISIBLE, (int)AnnotationsVisible.ANNOTATION_HIDDEN);
			}
			_c.Call(SCI_ANNOTATIONSETTEXT, iLine, buffer.p);
			//perf.nw();
		}
		
		if (annotAdded && allText) {
			//perf.first();
			_c.Call(SCI_ANNOTATIONSETVISIBLE, (int)_visible);
			//perf.nw();
		}
		
		//never mind: scintilla prints without annotations, therefore without images too.
	}
	
	/// <summary>
	/// Parses annotation text.
	/// If it starts with image info string ("\x3NNN\n\n..."), returns its length. Else returns 0.
	/// </summary>
	/// <param name="s">Annotation text. Can start with image info string or not.</param>
	/// <param name="length">s length.</param>
	/// <param name="imageInfo">The NNN part of image info, or 0.</param>
	static int _ParseAnnotText(byte* s, int length, out int imageInfo) {
		imageInfo = 0;
		if (s == null || length < 4 || s[0] != '\x3') return 0;
		byte* s2;
		int k = Api.strtoi(s + 1, &s2, 16);
		int len = (int)(s2 - s); if (len < 4) return 0;
		int n = k & 0xff;
		len += (n - 1);
		if (n < 1 || length < len) return 0;
		if (length > len) len++; //\n between image info and visible annotation text
		imageInfo = k;
		return len;
	}
	
	/// <summary>
	/// Sets annotation text, preserving existing image info.
	/// </summary>
	/// <param name="line"></param>
	/// <param name="s">New text without image info.</param>
	[SkipLocalsInit]
	internal void AnnotationText_(int line, string s) {
		int n = _c.Call(SCI_ANNOTATIONGETTEXT, line);
		if (n > 0) {
			int lens = (s == null) ? 0 : s.Length;
			using FastBuffer<byte> buffer = new(n + 1 + lens * 3);
			var p = buffer.p;
			_c.Call(SCI_ANNOTATIONGETTEXT, line, p); p[n] = 0;
			int imageLen = _ParseAnnotText(p, n, out var _);
			if (imageLen > 0) {
				//info: now len<=n
				if (lens == 0) {
					if (imageLen == n) return; //no "\nPrevText"
					p[--imageLen] = 0; //remove "\nPrevText"
				} else {
					if (imageLen == n) p[imageLen++] = (byte)'\n'; //no "\nPrevText"
					//Convert2.Utf8FromString(s, p + imageLen, lens * 3);
					Encoding.UTF8.GetBytes(s, new Span<byte>(p + imageLen, lens * 3));
				}
				_c.Call(SCI_ANNOTATIONSETTEXT, line, p);
				return;
			}
		}
		_c.aaaAnnotationText_(line, s);
	}
	
	/// <summary>
	/// Gets annotation text without image info.
	/// </summary>
	[SkipLocalsInit]
	internal string AnnotationText_(int line) {
		int n = _c.Call(SCI_ANNOTATIONGETTEXT, line);
		if (n > 0) {
			using FastBuffer<byte> buffer = new(n);
			var p = buffer.p;
			_c.Call(SCI_ANNOTATIONGETTEXT, line, p); p[n] = 0;
			int imageLen = _ParseAnnotText(p, n, out var _);
			//info: now len<=n
			if (imageLen < n) {
				if (imageLen != 0) { p += imageLen; n -= imageLen; }
				return Encoding.UTF8.GetString(p, n);
			}
		}
		return "";
	}
	
	_Image _GetImageInLine(RByte s, ref int iFrom, ref bool isMulti, ref int imageStringStartPos) {
		g1:
		int i = iFrom;
		if (i >= s.Length - 4) { iFrom = s.Length; isMulti = false; return null; }
		//find next "string". If not found, return next line or the end of whole string.
		if (!isMulti) { //else i is at image2 in "image1|image2"
			i = _FindQuot(s, i);
			if (i == s.Length || s[i++] != '"') { iFrom = i; return null; }
		}
		iFrom = _FindQuot(s, i);
		if (iFrom == s.Length || s[iFrom++] != '"') { isMulti = false; return null; }
		int i2 = iFrom - 1;
		
		//skip if not <image "..."
		if (!isMulti) {
			if (i >= 8 && s[(i - 8)..].StartsWith("<image "u8)) imageStringStartPos = i - 8;
			else goto g1;
		}
		
		//support "image1|image2|..."
		int i3 = s[i..i2].IndexOf((byte)'|') + i;
		if (i3 >= i) { i2 = i3; iFrom = i3 + 1; isMulti = true; } else isMulti = false;
		
		s = s[i..i2];
		
		//is it an image string?
		var imType = KImageUtil.ImageTypeFromString(out int prefixLength, s);
		if (imType == KImageUtil.ImageType.None) goto g1;
		if (prefixLength == 10) { s = s[prefixLength..]; prefixLength = 0; } //"imagefile:"
		
		//FUTURE: support SVG. Tested several best SVG libraries.
		//	None of C# libs support >50% tested .svg files.
		//	Librsvg (C) supports all. Too big for Au; would be useful for toolbar/menu images. Not too big for editor, if very need.
		
		var d = t_data;
		
		//is already loaded?
		long hash = Hash.Fnv1Long(s);
		var im = d.FindImage(hash, _c._dpi);
		//print.qm2.write(im != null, s.ToString());
		if (im != null) return im;
		
		//var test = s.ToString();
		//print.it(test, EImageUtil.ImageToString(test));
		
		s = s[prefixLength..];
		string path = s.ToStringUTF8();
		
		//load
		long t1 = computer.tickCountWithoutSleep;
		byte[] b = KImageUtil.BmpFileDataFromString(path, imType, true, (_c._dpi, null));
		t1 = computer.tickCountWithoutSleep - t1; if (t1 > 1000) print.warning($"Time to load image '{path}' is {t1} ms.", -1, prefix: "<>Note: "); //eg if network path unavailable, may wait ~7 s
		if (b == null) goto g1;
		if (!KImageUtil.GetBitmapFileInfo_(b, out var q)) goto g1;
		
		//create _Image
		im = new _Image() {
			data = b,
			nameHash = hash,
			width = q.width,
			height = Math.Min(q.height + IMAGE_MARGIN_TOP + IMAGE_MARGIN_BOTTOM, 2000)
		};
		
		//add to cache
		//Compact cache to avoid memory problems when loaded many big images, eg showing all png in Program Files.
		//Will auto reload when need, it does not noticeably slow down.
		//Cache even very large images, because we draw each line separately, would need to load whole image for each line, which is VERY slow.
		d.CompactCache();
		d.AddImage(im);
		
		return im;
		
		static int _FindQuot(RByte s, int i) {
			int r = s[i..].IndexOfAny((byte)'"', (byte)'\n');
			return r < 0 ? s.Length : r + i;
		}
	}
	
	const int IMAGE_MARGIN_TOP = 2; //frame + 1
	const int IMAGE_MARGIN_BOTTOM = 1; //just for frame. It is minimal margin, in most cases will be more.
	
	Sci_AnnotationDrawCallback _sci_AnnotationDrawCallback;
	unsafe int _AnnotationDrawCallback(void* cbParam, ref Sci_AnnotationDrawCallbackData c) {
		//Function info:
		//Called for all annotations, not just for images.
		//Returns image width. Returns 0 if there is no image or when called for annotation text line below image.
		//Called for each line of annotation, not once for whole image. Draws each image slice separately.
		//Called 2 times for each line: step 0 - to get width; step 1 - to draw that image slice on that line.
		
		//Get image info from annotation text. Return 0 if there is no image info, ie no image.
		//Image info is at the start. Format "\x3XXX", where XXX is a hex number that contains image width and number of lines.
		byte* s = c.text;
		if (c.textLen < 4 || s[0] != '\x3') return 0;
		int k = Api.strtoi(++s, null, 16); if (k < 256) return 0;
		int nLines = k & 0xff, width = k >> 8;
		
		if (c.step == 0) return width + 1; //just get width
		if (c.annotLine >= nLines) return 0; //an annotation text line below the image lines
		
		//Get line text, to find image strings.
		//Cannot store array indices in annotation, because they may change.
		//Also cannot store image strings in annotation, because then boxed annotation would be too wide (depends on text length).
		//Getting/parsing text takes less than 20% time. Other time - drawing image.
		int from = _c.aaaLineStart(false, c.line), to = _c.aaaLineEnd(false, c.line);
		var text = _c.aaaRangeSpan(from, to);
		
		//find image strings and draw the images
		bool hasImages = false;
		var hdc = c.hdc;
		RECT r = c.rect;
		IntPtr pen = default, oldPen = default;
		try {
			//Handle exceptions because SetDIBitsToDevice may read more than need, like CreateDIBitmap, although I never noticed this.
			
			//Call _GetImageInLine repeatedly. It finds next image string, finds its cached image or loads and addd to the cache.
			//Then draw the image, and finally frame. Actually just single slice, for this line.
			
			_Image u; bool isMulti = false; int imStrStart = 0;
			int x = r.left + 1;
			for (int i = 0; null != (u = _GetImageInLine(text, ref i, ref isMulti, ref imStrStart));) {
				hasImages = true;
				
				//draw image
				if (!KImageUtil.GetBitmapFileInfo_(u.data, out var q)) { Debug.Assert(false); continue; }
				int isFirstLine = (c.annotLine == 0) ? 1 : 0, hLine = r.bottom - r.top;
				int currentTop = c.annotLine * hLine, currentBottom = currentTop + hLine, imageBottom = q.height + IMAGE_MARGIN_TOP;
				int y = r.top + isFirstLine * IMAGE_MARGIN_TOP, yy = Math.Min(currentBottom, imageBottom) - currentTop;
				
				if (imageBottom > currentTop && q.width > 0 && q.height > 0) {
					fixed (byte* bp = u.data) {
						KImageUtil.BITMAPFILEHEADER* f = (KImageUtil.BITMAPFILEHEADER*)bp;
						byte* pBits = bp + f->bfOffBits;
						int bytesInLine = Math2.AlignUp(q.width * q.bitCount, 32) / 8;
						int sizF = u.data.Length - f->bfOffBits, siz = bytesInLine * q.height;
						if (q.isCompressed) {
							//this is slow with big images. It seems processes current line + all remaining lines. Such bitmaps are rare.
							int yOffs = -c.annotLine * hLine; if (isFirstLine == 0) yOffs += IMAGE_MARGIN_TOP;
							var ok = Api.SetDIBitsToDevice(hdc, x, r.top + isFirstLine * IMAGE_MARGIN_TOP,
								q.width, q.height, 0, yOffs, 0, q.height,
								pBits, q.biHeader);
							Debug.Assert(ok > 0);
						} else if (siz <= sizF) {
							//this is fast, but cannot use with compressed bitmaps
							int hei = yy - y, bmY = q.height - (currentTop - ((isFirstLine ^ 1) * IMAGE_MARGIN_TOP) + hei);
							var ok = Api.SetDIBitsToDevice(hdc, x, r.top + isFirstLine * IMAGE_MARGIN_TOP,
								q.width, hei, 0, 0, 0, hei,
								pBits + bmY * bytesInLine, q.biHeader);
							Debug.Assert(ok > 0);
						} else Debug.Assert(false);
						
						//could use this instead, but very slow with big images. It seems always processes whole bitmap, not just current line.
						//int hei=yy-y, bmY=q.height-(currentTop-((isFirstLine ^ 1)*IMAGE_MARGIN_TOP)+hei);
						//StretchDIBits(hdc,
						//	x, y, q.width, hei,
						//	0, bmY, q.width, hei,
						//	pBits, h, 0, SRCCOPY);
					}
				}
				
				//draw frame
				if (pen == default) oldPen = Api.SelectObject(hdc, pen = Api.CreatePen(0, 1, 0x60C060)); //quite fast. Caching in a static or ThreadStatic var is difficult.
				int xx = x + q.width;
				if (isFirstLine != 0) y--;
				if (yy > y) {
					Api.MoveToEx(hdc, x - 1, y, out _); Api.LineTo(hdc, x - 1, yy); //left |
					Api.MoveToEx(hdc, xx, y, out _); Api.LineTo(hdc, xx, yy); //right |
					if (isFirstLine != 0) { Api.MoveToEx(hdc, x, y, out _); Api.LineTo(hdc, xx, y); } //top _
				}
				if (yy >= y && yy < hLine) { Api.MoveToEx(hdc, x - 1, yy, out _); Api.LineTo(hdc, xx + 1, yy); } //bottom _
				
				x += u.width + 30;
			}
		}
		catch (Exception ex) { Debug_.Print(ex); }
		finally { if (pen != default) Api.DeleteObject(Api.SelectObject(hdc, oldPen)); }
		//perf.nw();
		
		//If there are no image strings (text edited), delete the annotation or just its part containing image info and '\n's.
		if (!hasImages && c.annotLine == 0) {
			int line = c.line; var annot = AnnotationText_(line);
			//_c.aaaAnnotationText_(line, annot); //dangerous
			_c.Dispatcher.InvokeAsync(() => { _c.aaaAnnotationText_(line, annot); });
			return 1;
		}
		
		return width + 1;
		
		//speed: fast. The fastest way. Don't need bitmap handle, memory DC, etc.
		//tested: don't know what ColorUse param of SetDIBitsToDevice does, but DIB_RGB_COLORS works for any h_biBitCount.
		//speed if drawing frame: multiple LineTo is faster than single PolyPolyline.
		//tested: GDI+ much slower, particularly DrawImage().
		//tested: in QM2 was used LZO compression, now ZIP (DeflateStream). ZIP compresses better, but not so much. LZO is faster, but ZIP is fast enough. GIF and JPG in most cases compress less than ZIP and sometimes less than LZO.
		//tested: saving in 8-bit format in most cases does not make much smaller when compressed. For screenshots we reduce colors to 4-bit.
	}
	
	internal void OnTextChanged_(bool inserted, in SCNotification n) {
		if (_visible == AnnotationsVisible.ANNOTATION_HIDDEN) return;
		
		//info: maybe half of this code is to avoid SCI_GETTEXTRANGE when we can use n.textUTF8.
		//	Eg can use n.textUTF8 when added all text or appended lines. These cases are the most important for good performance.
		//	Cannot use n.textUTF8 eg when editing in the middle of a line, because we need whole line, not just the inserted part.
		
		RByte s = null;
		int from = n.position, to = from + (inserted ? n.length : 0), len = 0, firstLine = 0, textPos = 0;
		bool allText = false;
		if (from == 0) {
			len = _c.aaaLen8;
			if (len < 10) return; //eg 0 when deleted all text
			if (inserted && len == n.length) { //added all text
				allText = true;
				s = new(n.textUTF8, len);
			}
		}
		if (s.IsNull()) {
			firstLine = _c.aaaLineFromPos(false, from);
			int from2 = _c.aaaLineStart(false, firstLine);
			if (!inserted && from2 == from) return; //deleted whole lines or characters at line start, which cannot create new image string in text
			int to2 = (inserted && n.textUTF8[n.length - 1] == '\n') ? to : _c.aaaLineEndFromPos(false, to);
			len = to2 - from2;
			//print.it(inserted, from, to, from2, to2, len);
			if (len < 10) return;
			if (from2 == from && to2 == to) {
				s = new(n.textUTF8, len);
			} else {
				//Debug_.Print("need to get text");
				s = _c.aaaRangeSpan(from2, to2);
			}
			textPos = from2;
		}
		
		int r = s.IndexOf("<image \""u8);
		if (r < 0) return;
		//tested: all this is faster than SCI_FINDTEXT. Much faster when need to search in big text.
		
		_SetImagesForTextRange(firstLine, s, allText, textPos);
	}
	
	/// <summary>
	/// Hides/shows all images, or changes the display style of annotation areas.
	/// Default is ANNOTATION_STANDARD (images visible).
	/// When hiding, it just removes images, does not hide text annotations (SCI_ANNOTATIONGETVISIBLE remains unchanged).
	/// </summary>
	public AnnotationsVisible Visible {
		get => _visible;
		[SkipLocalsInit]
		set {
			if (value == _visible) return;
			if (value == AnnotationsVisible.ANNOTATION_HIDDEN) {
				_c.Call(SCI_SETANNOTATIONDRAWCALLBACK);
				int len = _c.Call(SCI_GETTEXTLENGTH);
				if (len >= 10) {
					bool tempHidden = false;
					for (int iLine = 0, nLines = _c.Call(SCI_GETLINECOUNT); iLine < nLines; iLine++) {
						len = _c.Call(SCI_ANNOTATIONGETTEXT, iLine); //fast
						if (len < 4) continue;
						//perf.first();
						if (!tempHidden) {
							tempHidden = true;
							_c.Call(SCI_ANNOTATIONSETVISIBLE, (int)AnnotationsVisible.ANNOTATION_HIDDEN); //makes many times faster
						}
						using FastBuffer<byte> buffer = new(len);
						var a = buffer.p;
						_c.Call(SCI_ANNOTATIONGETTEXT, iLine, a); a[len] = 0;
						var imageLen = _ParseAnnotText(a, len, out var _);
						if (imageLen > 0) {
							if (len > imageLen) a += imageLen; else a = null;
							_c.Call(SCI_ANNOTATIONSETTEXT, iLine, a);
						}
						//perf.nw(); //surprisingly fast
					}
					//perf.first();
					if (tempHidden) _c.Call(SCI_ANNOTATIONSETVISIBLE, (int)_visible);
					//perf.nw(); //fast
				}
			} else if (_visible == AnnotationsVisible.ANNOTATION_HIDDEN) {
				_c.Call(SCI_SETANNOTATIONDRAWCALLBACK, 0, _callbackPtr);
				int len = _c.Call(SCI_GETTEXTLENGTH);
				if (len >= 10) {
					var s = _c.aaaRangeSpan(0, len);
					_SetImagesForTextRange(0, s, true, 0);
				}
			}
			
			_visible = value;
			if (value != AnnotationsVisible.ANNOTATION_HIDDEN) _c.Call(SCI_ANNOTATIONSETVISIBLE, (int)value);
		}
	}
	AnnotationsVisible _visible;
}
