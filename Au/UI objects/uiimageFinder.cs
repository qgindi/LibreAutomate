//FUTURE: option to allow % of image completely different. Eg button with/without focus rectangle.
//	Or/and in the tool allow to erase some areas (make alpha 0).

//#define WI_TEST_NO_OPTIMIZATION

using System.Drawing;
using System.Drawing.Imaging;

namespace Au;

/// <summary>
/// Finds images displayed in user interface (UI). Contains data and parameters of image(s) or color(s) to find.
/// </summary>
/// <remarks>
/// Can be used instead of <see cref="uiimage.find"/>.
/// </remarks>
public unsafe class uiimageFinder {
	class _Image {
		public uint[] pixels;
		public int width, height;
		public _OptimizationData optim;

		public _Image(string file) {
			using var b = ImageUtil.LoadGdipBitmap(file);
			_BitmapToData(b);
		}

		public _Image(Bitmap b) {
			b = b ?? throw new ArgumentException("null Bitmap");
			_BitmapToData(b);
		}

		void _BitmapToData(Bitmap b) {
			var z = b.Size;
			width = z.Width; height = z.Height;
			pixels = new uint[width * height];
			fixed (uint* p = pixels) {
				var d = new BitmapData { Scan0 = (IntPtr)p, Height = height, Width = width, Stride = width * 4, PixelFormat = PixelFormat.Format32bppArgb };
				d = b.LockBits(new Rectangle(default, z), ImageLockMode.ReadOnly | ImageLockMode.UserInputBuffer, PixelFormat.Format32bppArgb, d);
				b.UnlockBits(d);
				if (d.Stride < 0) throw new ArgumentException("bottom-up Bitmap"); //Image.FromHbitmap used to create bottom-up bitmap (stride<0) from compatible bitmap. Now cannot reproduce.
			}
		}

		public _Image(ColorInt color) {
			width = height = 1;
			pixels = new uint[1] { (uint)color.argb | 0xff000000 };
		}

		public _Image() { }
	}

	//ctor parameters
	readonly List<_Image> _images; //support multiple images
	readonly IFFlags _flags;
	readonly uint _diff;
	readonly Func<uiimage, IFAlso> _also;

	Action_ _action;
	IFArea _area;
	CaptureScreenImage _ad; //area data
	POINT _resultOffset; //to map the found rectangle from the captured area coordinates to the specified area coordinates

	/// <summary>
	/// Returns <see cref="uiimage"/> object that contains the rectangle of the found image and can click it etc.
	/// </summary>
	public uiimage Result { get; private set; }

	/// <summary>
	/// Stores image/color data and search settings in this object. Loads images if need. See <see cref="uiimage.find"/>.
	/// </summary>
	/// <exception cref="ArgumentException">An argument is/contains a <c>null</c>/invalid value.</exception>
	/// <exception cref="FileNotFoundException">Image file does not exist.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="ImageUtil.LoadGdipBitmap"/>.</exception>
	/// <inheritdoc cref="uiimage.find(IFArea, IFImage, IFFlags, int, Func{uiimage, IFAlso})" path="/param"/>
	public uiimageFinder(IFImage image, IFFlags flags = 0, int diff = 0, Func<uiimage, IFAlso> also = null) {
		_flags = flags;
		uint d = (uint)diff; _diff = d switch { <= 30 => d, <= 60 => 30 + (d - 30) * 2, <= 100 => 90 + (d - 60) * 3, _ => throw new ArgumentOutOfRangeException("diff range: 0 - 100") }; //make slightly exponential, 0 - 210
		_also = also;

		_images = new List<_Image>();
		if (image.Value != null) _AddImage(image);

		void _AddImage(IFImage image) {
			switch (image.Value) {
			case string s:
				_images.Add(new _Image(s));
				break;
			case Bitmap b:
				_images.Add(new _Image(b));
				break;
			case ColorInt c:
				_images.Add(new _Image(c));
				break;
			case IFImage[] a:
				foreach (var v in a) _AddImage(v);
				break;
			}
		}
	}

	/// <summary>
	/// Finds the first image displayed in the specified window or other area.
	/// See <see cref="uiimage.find"/>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>, else <c>null</c>.</returns>
	/// <exception cref="AuWndException">Invalid window handle.</exception>
	/// <exception cref="ArgumentException">An argument of this function or of constructor is invalid.</exception>
	/// <exception cref="AuException">Something failed.</exception>
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	/// <inheritdoc cref="uiimage.find" path="/param"/>
	public uiimage Find(IFArea area) => Exists(area) ? Result : null;

	/// <summary>
	/// Finds the first image displayed in the specified window or other area. Can wait and throw <b>NotFoundException</b>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>. Else throws exception or returns <c>null</c> (if <i>wait</i> negative).</returns>
	/// <param name="wait">The wait timeout, seconds. If 0, does not wait. If negative, does not throw <b>NotFoundException</b>.</param>
	/// <exception cref="AuWndException">Invalid window handle.</exception>
	/// <exception cref="ArgumentException">An argument of this function or of constructor is invalid.</exception>
	/// <exception cref="AuException">Something failed.</exception>
	/// <exception cref="NotFoundException" />
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	/// <inheritdoc cref="uiimage.find" path="/param"/>
	public uiimage Find(IFArea area, Seconds wait) => Exists(area, wait) ? Result : null;

	/// <returns>If found, sets <see cref="Result"/> and returns <c>true</c>, else false.</returns>
	/// <inheritdoc cref="Find(IFArea)"/>
	public bool Exists(IFArea area) {
		_Before(area, Action_.Find);
		return _Find();
	}

	/// <returns>If found, sets <see cref="Result"/> and returns <c>true</c>. Else throws exception or returns <c>false</c> (if <i>wait</i> negative).</returns>
	/// <inheritdoc cref="Find(IFArea, Seconds)"/>
	public bool Exists(IFArea area, Seconds wait) {
		bool r = wait.Exists_() ? Exists(area) : Wait_(Action_.Wait, wait, area);
		return r || wait.ReturnFalseOrThrowNotFound_();
	}

	/// <summary>
	/// See <see cref="uiimage.wait"/>.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <exception cref="Exception">Exceptions of <see cref="uiimage.wait"/>, except those of the constructor.</exception>
	/// <remarks>
	/// Same as <see cref="Find(IFArea, Seconds)"/>, except:
	/// - 0 timeout means infinite.
	/// - on timeout throws <b>TimeoutException</b>, not <b>NotFoundException</b>.
	/// </remarks>
	/// <inheritdoc cref="Find(IFArea, Seconds)" path="/param"/>
	public uiimage Wait(Seconds timeout, IFArea area)
		=> Wait_(Action_.Wait, timeout, area) ? Result : null;
	//SHOULDDO: suspend waiting while a mouse button is pressed.
	//	Now, eg if finds while scrolling, although MouseMove waits until buttons released, but moves to the old (wrong) place.

	/// <summary>
	/// See <see cref="uiimage.waitNot"/>.
	/// </summary>
	/// <exception cref="Exception">Exceptions of <see cref="uiimage.waitNot"/>, except those of the constructor.</exception>
	/// <inheritdoc cref="Wait(Seconds, IFArea)" path="/param"/>
	public bool WaitNot(Seconds timeout, IFArea area)
		=> Wait_(Action_.WaitNot, timeout, area);

	internal bool Wait_(Action_ action, Seconds timeout, IFArea area) {
		if (area.Type == IFArea.AreaType.Bitmap) throw new ArgumentException("Bitmap and wait");
		_Before(area, action);
		try { return wait.until(timeout, () => _Find() ^ (action > Action_.Wait)); }
		finally { _After(); }

		//tested: does not create garbage while waiting.
	}

	internal enum Action_ { Find, Wait, WaitNot, WaitChanged }

	//called at the start of _Find and Wait_
	void _Before(IFArea area, Action_ action) {
		Not_.Null(area);
		_action = action;
		_area = area;
		_ad ??= new();

		if (_action == Action_.WaitChanged) {
			Debug.Assert(_images.Count == 0 && _also == null); //the first _Find will capture the area and add to _images
		} else {
			if (_images.Count == 0) throw new ArgumentException("no image");
		}

		_area.Before_(_flags.HasAny(IFFlags.WindowDC | IFFlags.PrintWindow));
	}

	//called at the end of _Find (if not waiting) and Wait_
	void _After() {
		_ad.Dispose();
		_area = null;
		if (_action == Action_.WaitChanged) _images.Clear();
	}

	bool _Find() {
		//using var p1 = perf.local();
		Result = null;

		if (!_area.GetRect_(out var r, out _resultOffset, _flags)) return false;

		//If WaitChanged, first time just get area pixels into _images[0].
		if (_action == Action_.WaitChanged && _images.Count == 0) {
			return _GetAreaPixels(r, true);
		}

		//Return false if all images are bigger than the search area.
		for (int i = _images.Count; --i >= 0;) {
			var v = _images[i];
			if (v.width <= r.Width && v.height <= r.Height) goto g1;
		}
		return false; g1:

		BitmapData bitmapBD = null;
		try {
			//Get area pixels.
			bool havePixels = _area.Type == IFArea.AreaType.Bitmap;
			if (havePixels) {
				var pf = (_area.B.PixelFormat == PixelFormat.Format32bppArgb) ? PixelFormat.Format32bppArgb : PixelFormat.Format32bppRgb; //if possible, use PixelFormat of _area, to avoid conversion/copying. Both these formats are ok, we don't use alpha.
				bitmapBD = _area.B.LockBits(r, ImageLockMode.ReadOnly, pf);
				if (bitmapBD.Stride < 0) throw new ArgumentException("bottom-up Bitmap");
				_ad.SetExternalData_((uint*)bitmapBD.Scan0, bitmapBD.Width, bitmapBD.Height);
				//note: don't support rect in Bitmap. LockBits does not copy bits if same pixelformat. Would need to create new Bitmap from that rect, or use stride when searching.
			} else {
				havePixels = _GetAreaPixels(r);
			}
			//p1.Next();

			if (havePixels) {
				//Find image(s) in area.
				uiimage alsoResult = null;
				_DpiScaling dpiScaling = null;
				if (_action == Action_.WaitChanged)
					return _FindImage(0, ref alsoResult, ref dpiScaling);
				if (_flags.Has(IFFlags.Parallel) && _images.Count > 1) {
					Parallel.For(0, _images.Count, (i, pls) => {
						_FindImage(i, ref alsoResult, ref dpiScaling, pls);
					});
				} else {
					for (int i = 0; i < _images.Count; i++) {
						if (_FindImage(i, ref alsoResult, ref dpiScaling)) break;
					}
				}

				Result ??= alsoResult;
				if (Result != null) return true;
			}

			return false;
		}
		finally {
			if (bitmapBD != null) _area.B.UnlockBits(bitmapBD);
			if (_action == Action_.Find) _After();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	bool _FindImage(int listIndex, ref uiimage alsoResult, ref _DpiScaling dpiScaling, ParallelLoopState pls = null) {
		//note: can run in multiple threads simultaneously. Don't modify this fields and ref params without lock.

		var image = _images[listIndex];
		var alsoAction = IFAlso.FindOtherOfList;
		int matchIndex = 0;

		int imageWidth = image.width, imageHeight = image.height;
		if (_ad.Width < imageWidth || _ad.Height < imageHeight) return false;
		fixed (uint* imagePixels = image.pixels) {
			uint* imagePixelsTo = imagePixels + imageWidth * imageHeight;
			uint* areaPixels = _ad.Pixels;

			//rejected: if image is of same size as area, simply compare. For example, when action is WaitChanged.
			//	Does not make faster, just adds more code.

			if (!image.optim.Init(image, _ad.Width)) return false;
			var optim = image.optim; //copy struct, size = 9*int
			int o_pos0 = optim.v0.pos;
			var o_a1 = &optim.v1; var o_an = o_a1 + (optim.N - 1);

			//find first pixel. This part is very important for speed.
			//int nTimesFound = 0; //debug

			var areaWidthMinusImage = _ad.Width - imageWidth;
			var pFirst = areaPixels + o_pos0;
			var pLast = pFirst + _ad.Width * (_ad.Height - imageHeight) + areaWidthMinusImage;

			//this is a workaround for compiler not using registers for variables in fast loops (part 1)
			var f = new _FindData {
				color = (optim.v0.color & 0xffffff) | (_diff << 24),
				p = pFirst - 1,
				pLineLast = pFirst + areaWidthMinusImage
			};

			#region fast_code

			//This for loop must be as fast as possible.
			//	There are too few 32-bit registers. Must be used a many as possible registers. See comments below.
			//	No problems if 64-bit.

			gContinue:
			if (pls?.IsStopped ?? false) goto gNotFound;
			{
				var f_ = &f; //part 2 of the workaround
				var p_ = f_->p + 1; //register
				var color_ = f_->color; //register
				var pLineLast_ = f_->pLineLast; //register
				for (; ; ) { //lines
					if (color_ < 0x1000000) {
						for (; p_ <= pLineLast_; p_++) {
							if (color_ == (*p_ & 0xffffff)) goto gPixelFound;
						}
					} else {
						//all variables except f.pLineLast are in registers
						//	It is very sensitive to other code. Compiler can take some registers for other code and not use here.
						//	Then still not significantly slower, but I like to have full speed.
						//	Code above fast_code region should not contain variables that are used in loops below this block.
						//	Also don't use class members in fast_code region, because then compiler may take a register for 'this' pointer.
						//	Here we use f.pLineLast instead of pLineLast_, else d2_ would be in memory (it is used 3 times).
						var d_ = color_ >> 24; //register
						var d2_ = d_ * 2; //register
						for (; p_ <= f.pLineLast; p_++) {
							if ((color_ & 0xff) - ((byte*)p_)[0] + d_ > d2_) continue;
							if ((color_ >> 8 & 0xff) - ((byte*)p_)[1] + d_ > d2_) continue;
							if ((color_ >> 16 & 0xff) - ((byte*)p_)[2] + d_ > d2_) continue;
							goto gPixelFound;
						}
					}
					if (p_ > pLast) goto gNotFound;
					p_--; p_ += imageWidth;
					f.pLineLast = pLineLast_ = p_ + areaWidthMinusImage;
				}
				gPixelFound:
				f.p = p_;
			}

			//nTimesFound++;
			var ap = f.p - o_pos0; //the first area pixel of the top-left of the image

			//compare other 0-3 selected pixels
			for (var op = o_a1; op < o_an; op++) {
				uint aPix = ap[op->pos], iPix = op->color;
				var colorDiff = f.color >> 24;
				if (colorDiff == 0) {
					if (!_MatchPixelExact(aPix, iPix)) goto gContinue;
				} else {
					if (!_MatchPixelDiff(aPix, iPix, colorDiff)) goto gContinue;
				}
			}

			//now compare all pixels of the image
			//perf.first();
			uint* ip = imagePixels, ipLineTo = ip + imageWidth;
			for (; ; ) { //lines
				if (f.color < 0x1000000) {
					do {
						if (!_MatchPixelExact(*ap, *ip)) goto gContinue;
						ap++;
					}
					while (++ip < ipLineTo);
				} else {
					var colorDiff = f.color >> 24;
					do {
						if (!_MatchPixelDiff(*ap, *ip, colorDiff)) goto gContinue;
						ap++;
					}
					while (++ip < ipLineTo);
				}
				if (ip == imagePixelsTo) break;
				ap += areaWidthMinusImage;
				ipLineTo += imageWidth;
			}
			//perf.nw();
			//print.it(nTimesFound);

			#endregion

			if (_action != Action_.WaitChanged) {
				int iFound = (int)(f.p - o_pos0 - areaPixels);
				RECT r = new(iFound % _ad.Width, iFound / _ad.Width, imageWidth, imageHeight);

				lock (this) {
					if (pls?.IsStopped ?? false) goto gNotFound;

					if (_flags.HasAny(IFFlags.WindowDC | IFFlags.PrintWindow)) {
						dpiScaling ??= new(_area.W);
						dpiScaling.ScaleRect(ref r);
					}
					r.Offset(_resultOffset.x, _resultOffset.y);

					uiimage tempResult = new(_area) { Rect = r, MatchIndex = matchIndex, ListIndex = listIndex };

					if (_also != null) {
						alsoAction = _also(tempResult);
						if (alsoAction is IFAlso.OkFindMoreOfThis or IFAlso.FindOtherOfThis && pls != null) {
							//stop other threads, but not this thread
							pls.Stop();
							pls = null;
							//never mind: if later _also returns "continue to search in list" (unlikely), will not search.
							//	pls.Stop must be called while locked, to prevent other threads calling _also afterwards.
							//	Now using the simple way (lock).
							//	The complex way - use Monitor.Enter/Exit. Call Stop/Exit when returning. Bad: other threads would continue to search unnecessarily.
						}
						switch (alsoAction) {
						case IFAlso.OkFindMore or IFAlso.OkFindMoreOfThis:
							alsoResult = tempResult;
							matchIndex++;
							goto gContinue;
						case IFAlso.FindOther or IFAlso.FindOtherOfThis:
							matchIndex++;
							goto gContinue;
						case IFAlso.OkFindMoreOfList:
							alsoResult = tempResult;
							return false;
						case IFAlso.FindOtherOfList:
							return false;
						}
					}

					if (alsoAction != IFAlso.NotFound) Result = tempResult;
					pls?.Stop();
				}
			}
		} //fixed

		return true;
		gNotFound:
		return alsoAction is IFAlso.FindOtherOfThis or IFAlso.OkFindMoreOfThis;

		//returns true to stop seaching (skip other images in list)
	}

	struct _FindData {
		public uint color;
		public uint* p, pLineLast;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _MatchPixelExact(uint ap, uint ip) {
		if (ip == (ap | 0xff000000)) return true;
		return ip < 0xff000000; //transparent?
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	static bool _MatchPixelDiff(uint ap, uint ip, uint colorDiff) {
		//info: optimized. Don't modify.
		//	All variables are in registers.
		//	Only 3.5 times slower than _MatchPixelExact (when all pixels match), which is inline.

		if (ip >= 0xff000000) { //else transparent
			uint d = colorDiff, d2 = d * 2;
			if (((ip & 0xff) - (ap & 0xff) + d) > d2) goto gFalse;
			if (((ip >> 8 & 0xff) - (ap >> 8 & 0xff) + d) > d2) goto gFalse;
			if (((ip >> 16 & 0xff) - (ap >> 16 & 0xff) + d) > d2) goto gFalse;
		}
		return true;
		gFalse:
		return false;
	}

	//bool _CompareSameSize(uint* area, uint* image, uint* imageTo, uint colorDiff)
	//{
	//	if(colorDiff == 0) {
	//		do {
	//			if(!_MatchPixelExact(*area, *image)) break;
	//			area++;
	//		} while(++image < imageTo);
	//	} else {
	//		do {
	//			if(!_MatchPixelDiff(*area, *image, colorDiff)) break;
	//			area++;
	//		} while(++image < imageTo);
	//	}
	//	return image == imageTo;
	//}

	static bool _IsTransparent(uint color) => color < 0xff000000;

	struct _OptimizationData {
		internal struct POSCOLOR {
			public int pos; //the position in area (not in image) from which to start searching. Depends on where in the image is the color.
			public uint color;
		};

#pragma warning disable 649 //never assigned
		public POSCOLOR v0, v1, v2, v3; //POSCOLOR[] would be slower
#pragma warning restore 649
		public int N; //A valid count
		int _areaWidth;

		public bool Init(_Image image, int areaWidth) {
			if (areaWidth != _areaWidth) { _areaWidth = areaWidth; N = 0; }
			if (N != 0) return N > 0;

			int imageWidth = image.width, imageHeight = image.height;
			int imagePixelCount = imageWidth * imageHeight;
			var imagePixels = image.pixels;
			int i;

#if WI_TEST_NO_OPTIMIZATION
			_Add(image, 0, areaWidth);
#else

			//Find several unique-color pixels for first-pixel search.
			//This greatly reduces the search time in most cases.

			//find first nontransparent pixel
			for (i = 0; i < imagePixelCount; i++) if (!_IsTransparent(imagePixels[i])) break;
			if (i == imagePixelCount) { N = -1; return false; } //not found because all pixels in image are transparent

			//SHOULDDO:
			//1. Use colorDiff.
			//CONSIDER:
			//1. Start from center.
			//2. Prefer high saturation pixels.
			//3. If large area, find its dominant color(s) and don't use them. For speed, compare eg every 11-th.
			//4. Create a better algorithm. Maybe just shorter. This code is converted from QM2.

			//find first nonbackground pixel (consider top-left pixel is background)
			bool singleColor = false;
			if (i == 0) {
				i = _FindDifferentPixel(0);
				if (i < 0) { singleColor = true; i = 0; }
			}

			_Add(image, i, areaWidth);
			if (!singleColor) {
				//find second different pixel
				int i0 = i;
				i = _FindDifferentPixel(i);
				if (i >= 0) {
					_Add(image, i, areaWidth);
					//find other different pixels
					fixed (POSCOLOR* p = &v0) {
						while (N < 4) {
							for (++i; i < imagePixelCount; i++) {
								var c = imagePixels[i];
								if (_IsTransparent(c)) continue;
								int j = N - 1;
								for (; j >= 0; j--) if (c == p[j].color) break; //find new color
								if (j < 0) break; //found
							}
							if (i >= imagePixelCount) break;
							_Add(image, i, areaWidth);
						}
					}
				} else {
					for (i = imagePixelCount - 1; i > i0; i--) if (!_IsTransparent(imagePixels[i])) break;
					_Add(image, i, areaWidth);
				}
			}

			//fixed (POSCOLOR* o_pc = &v0) for(int j = 0; j < N; j++) print.it($"{o_pc[j].pos} 0x{o_pc[j].color:X}");
#endif
			return true;

			int _FindDifferentPixel(int iCurrent) {
				int m = iCurrent, n = imagePixelCount;
				uint notColor = imagePixels[m++];
				for (; m < n; m++) {
					var c = imagePixels[m];
					if (c == notColor || _IsTransparent(c)) continue;
					return m;
				}
				return -1;
			}
		}

		void _Add(_Image image, int i, int areaWidth) {
			fixed (POSCOLOR* p0 = &v0) {
				var p = p0 + N++;
				p->color = image.pixels[i];
				int w = image.width, x = i % w, y = i / w;
				p->pos = y * areaWidth + x;
			}
		}
	}

	bool _GetAreaPixels(RECT r, bool toImage0 = false) {
		//Transfer from screen/window DC to memory DC (does not work without this) and get pixels.
		//This is the slowest part of Find, especially BitBlt.
		//Speed depends on computer, driver, OS version, theme, size.
		//For example, with Aero theme 2-15 times slower (on Windows 8/10 cannot disable Aero).
		//With incorrect/generic video driver can be 10 times slower. Eg on vmware virtual PC.
		//Much faster when using window DC. Then same speed as without Aero.

		_ad.DontSetAlpha_ = !toImage0;
		bool ok = _area.Type == IFArea.AreaType.Screen
			? _ad.Capture(r, relaxed: true)
			: _ad.Capture(_area.W, r, _flags.ToCIFlags_() | CIFlags.Relaxed);
		if (!ok) return false; //r not in client area. Probably the window resized since r.Intersect(clientArea).

		if (toImage0) {
			var im = new _Image { width = r.Width, height = r.Height, pixels = _ad.ToArray1D() };
			_images.Add(im);
		}

		return true;
	}

#if false
	//r is relative to the search area
	void _DpiScaleRect(ref RECT r) {
		if (!_flags.HasAny(IFFlags.WindowDC | IFFlags.PrintWindow)) return;
		var w = _area.W.Window;
		if (!Dpi.IsWindowVirtualizedWin10_(w)) return; //makes faster on Win10+; don't scale on older OS
		int d1 = screen.of(w).Dpi, d2 = Dpi.OfWindow(w);
		r.left = Math2.MulDiv(r.left, d1, d2);
		r.top = Math2.MulDiv(r.top, d1, d2);
		r.right = Math2.MulDiv(r.right, d1, d2);
		r.bottom = Math2.MulDiv(r.bottom, d1, d2);
	}
#else
	//cache DPI scaling info. Getting it can make much slower if many matches found. Above is the non-cached version.
	class _DpiScaling {
		wnd _w;
		ushort _dScreen, _dWindow;
		bool _inited, _scaled;

		public _DpiScaling(wnd w) { _w = w; }

		//r is relative to the search area
		public void ScaleRect(ref RECT r) {
			if (!_inited) {
				_inited = true;
				if (_scaled = Dpi.IsWindowVirtualizedWin10_(_w = _w.Window)) {
					_dScreen = (ushort)screen.of(_w).Dpi;
					_dWindow = (ushort)Dpi.OfWindow(_w);
				}
			}
			if (_scaled) {
				r.left = Math2.MulDiv(r.left, _dScreen, _dWindow);
				r.top = Math2.MulDiv(r.top, _dScreen, _dWindow);
				r.right = Math2.MulDiv(r.right, _dScreen, _dWindow);
				r.bottom = Math2.MulDiv(r.bottom, _dScreen, _dWindow);
			}
		}
	}
#endif
}
