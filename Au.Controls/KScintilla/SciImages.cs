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
	
	class _ThreadSharedData {
		List<_Image> _a;
		int _dpi;
		timer _timer;
		
		public int CacheSize { get; private set; }
		
		public void AddImage(_Image im) {
			_a ??= [];
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
			if (dpi != _dpi) {
				ClearCache();
				_dpi = dpi;
			}
			if (!_a.NE_()) {
				for (int j = 0; j < _a.Count; j++) if (_a[j].nameHash == nameHash) return _a[j];
			}
			return null;
		}
		
		public void ClearCache() {
			_a?.Clear();
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
	[ThreadStatic] static _ThreadSharedData t_data; //all SciImages of a thread share single cache etc
	
	KScintilla _c;
	IntPtr _callbackPtr;
	const int c_indicImage = 7;
	
	/// <summary>
	/// Prepares this variable and the Scintilla control to display images.
	/// Note: will call SCI_ANNOTATIONSETVISIBLE(ANNOTATION_STANDARD) to draw images in annotation areas.
	/// Note: uses indicator 7.
	/// </summary>
	/// <param name="c">The control.</param>
	internal SciImages(KScintilla c) {
		t_data ??= new();
		_c = c;
		_sci_AnnotationDrawCallback = _AnnotationDrawCallback;
		_callbackPtr = Marshal.GetFunctionPointerForDelegate(_sci_AnnotationDrawCallback);
		_c.Call(SCI_SETANNOTATIONDRAWCALLBACK, 0, _callbackPtr);
		_c.aaaIndicatorDefine(c_indicImage, INDIC_HIDDEN);
	}
	
	_Image _GetImageFromText(RByte s) {
		//is it an image string?
		var imType = KImageUtil.ImageTypeFromString(out int prefixLength, s);
		if (imType == KImageUtil.ImageType.None) return null;
		if (prefixLength == 10) { s = s[prefixLength..]; prefixLength = 0; } //"imagefile:"
		
		var d = t_data;
		
		//is already loaded?
		long hash = Hash.Fnv1Long(s);
		var im = d.FindImage(hash, _c._dpi);
		//print.qm2.write(im != null, s.ToStringUTF8());
		if (im != null) return im;
		
		string path = s[prefixLength..].ToStringUTF8();
		
		//load
		long t1 = computer.tickCountWithoutSleep;
		byte[] b = KImageUtil.BmpFileDataFromString(path, imType, true, (_c._dpi, null));
		t1 = computer.tickCountWithoutSleep - t1; if (t1 > 1000) print.warning($"Time to load image '{path}' is {t1} ms.", -1, prefix: "<>Note: "); //eg if network path unavailable, may wait ~7 s
		if (b == null) return null;
		if (!KImageUtil.GetBitmapFileInfo_(b, out var q)) return null;
		
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
	}
	
	/// <summary>
	/// Sets image annotations for one or more lines of text.
	/// Called at the end of SciTags._AddText if the added text contains image tags.
	/// </summary>
	internal void SetImagesForTextRange_(RByte text, List<StartEnd> images, int prevLen) {
		bool allText = prevLen == 0;
		bool annotAdded = false;
		int iLine = -1, maxHeight = 0, totalWidth = 0;
		foreach (var v in images) {
			int line = _c.aaaLineFromPos(false, prevLen + v.start);
			if (line != iLine) {
				_AddLine();
				(iLine, maxHeight, totalWidth) = (line, 0, 0);
			}
			for (int start = v.start, end = 0; start < v.end; start = end + 1) { //foreach image in image1|image2|image3
				for (end = start; end < v.end && text[end] != '|';) end++;
				var s = text[start..end];
				if (_GetImageFromText(s) is not _Image u) continue;
				if (maxHeight < u.height) maxHeight = u.height;
				if (totalWidth > 0) totalWidth += 30;
				totalWidth += u.width;
				_c.aaaIndicatorAdd(c_indicImage, false, (start + prevLen)..(end + prevLen));
			}
		}
		_AddLine();
		
		[SkipLocalsInit]
		void _AddLine() {
			if (maxHeight == 0) return;
			int annotLen = _c.Call(SCI_ANNOTATIONGETTEXT, iLine); //we'll need old annotation text later, and we'll get it into the same buffer after the new image info
			
			//calculate n annotation lines from image height
			int lineHeight = _c.aaaLineHeight(); if (lineHeight <= 0) return;
			int nAnnotLines = Math.Min((maxHeight + (lineHeight - 1)) / lineHeight, 255);
			//print.it(lineHeight, maxHeight, nAnnotLines);
			
			using FastBuffer<byte> buffer = new(annotLen + nAnnotLines + 20);
			var p = buffer.p;
			*p++ = 3; Api._ltoa(totalWidth << 8 | nAnnotLines, p, 16); while (*(++p) != 0) { }
			while (nAnnotLines-- > 1) *p++ = (byte)'\n';
			*p = 0;
			
			//TODO2: code in this file can be simplified in several places. We don't use image+text annotations and images in editable text. Initially it was designed to support such images in code editor in editable mode.
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
					if (annotLen == imageLen || a[imageLen] == '\n') return; //case 5
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
			_c.Call(SCI_ANNOTATIONSETVISIBLE, (int)AnnotationsVisible.ANNOTATION_STANDARD);
		}
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
	
	const int IMAGE_MARGIN_TOP = 2; //frame + 1
	const int IMAGE_MARGIN_BOTTOM = 1; //just for frame. It is minimal margin, in most cases will be more.
	
	Sci_AnnotationDrawCallback _sci_AnnotationDrawCallback;
	unsafe int _AnnotationDrawCallback(void* cbParam, ref Sci_AnnotationDrawCallbackData c) {
		//Function info:
		//Called for all annotations, not just for images.
		//Returns image width. Returns 0 if there is no image or when called for annotation text line below image.
		//Called for each line of annotation, not once for whole image. Draws each image slice separately.
		//Called 2 times for each line: step 0 - to get width; step 1 - to draw that image slice on that line. Step 0 skipped if AnnotationsVisible.ANNOTATION_STANDARD (we don't use other styles).
		
		//Get image info from annotation text. Return 0 if there is no image info, ie no image.
		//Image info is at the start. Format "\x3XXX", where XXX is a hex number that contains image width and number of lines.
		byte* s = c.text;
		if (c.textLen < 4 || s[0] != '\x3') return 0;
		int k = Api.strtoi(++s, null, 16); if (k < 256) return 0;
		int nLines = k & 0xff, width = k >> 8;
		
		if (c.step == 0) return width + 1; //just get width
		if (c.annotLine >= nLines) return 0; //an annotation text line below the image lines
		
		//find image strings and draw the images
		bool hasImages = false;
		var hdc = c.hdc;
		IntPtr pen = default, oldPen = default;
		try { //Handle exceptions because SetDIBitsToDevice may read more than need, like CreateDIBitmap, although I never noticed this.
			int from = _c.aaaLineStart(false, c.line), to = _c.aaaLineEnd(false, c.line);
			RECT r = c.rect;
			int x = r.left + 1;
			for (int end = from; ;) { //for each `IMAGE` in this line, in text like `A <image "IMAGE"> b <image "IMAGE|IMAGE"> c`
				int start = _c.Call(SCI_INDICATOREND, c_indicImage, end); if (start <= 0 || start >= to) break;
				end = _c.Call(SCI_INDICATOREND, c_indicImage, start); if (end <= start) break;
				
				var u = _GetImageFromText(_c.aaaRangeSpan(start, end)); //find cached image or load and addd to the cache
				if (u is null) break;
				hasImages = true;
				
				//draw image (single slice, for this visual line)
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
}
