//FUTURE: option to allow % of image completely different. Eg button with/without focus rectangle.

//#define WI_DEBUG_PERF
//#define WI_SIMPLE
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
public unsafe class uiimageFinder
{
	class _Image
	{
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

	//large unmanaged memory etc reused while waiting, to make slightly faster
	internal unsafe struct _AreaData
	{
		public MemoryBitmap mb;
		public int width, height, memSize;
		public uint* pixels;

		public void Free() {
			if (mb != null) { //null if IFArea.AreaType.Bitmap
				mb.Dispose();
				MemoryUtil.VirtualFree(pixels);
			}
		}
	}

	//ctor parameters
	readonly List<_Image> _images; //support multiple images
	readonly IFFlags _flags;
	readonly uint _diff;
	readonly Func<uiimage, IFAlso> _also;

	Action_ _action;
	IFArea _area;
	_AreaData _ad;
	POINT _resultOffset; //to map the found rectangle from the captured area coordinates to the specified area coordinates

	/// <summary>
	/// Returns <see cref="uiimage"/> object that contains the rectangle of the found image and can click it etc.
	/// </summary>
	public uiimage Result { get; private set; }

	/// <summary>
	/// Stores image/color data and search settings in this object. Loads images if need. See <see cref="uiimage.find"/>.
	/// </summary>
	/// <exception cref="ArgumentException">An argument is/contains a null/invalid value.</exception>
	/// <exception cref="FileNotFoundException">Image file does not exist.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="ImageUtil.LoadGdipBitmap"/>.</exception>
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
	/// <returns>If found, returns <see cref="Result"/>, else null.</returns>
	/// <param name="area">See <see cref="uiimage.find"/>.</param>
	/// <exception cref="AuWndException">Invalid window handle.</exception>
	/// <exception cref="ArgumentException">An argument of this function or of constructor is invalid.</exception>
	/// <exception cref="AuException">Something failed.</exception>
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	public uiimage Find(IFArea area) => Exists(area) ? Result : null;

	/// <summary>
	/// Finds the first image displayed in the specified window or other area. Can wait and throw <b>NotFoundException</b>.
	/// </summary>
	/// <returns>If found, returns <see cref="Result"/>. Else throws exception or returns null (if <i>waitS</i> negative).</returns>
	/// <param name="area">See <see cref="uiimage.find"/>.</param>
	/// <param name="waitS">The wait timeout, seconds. If 0, does not wait. If negative, does not throw <b>NotFoundException</b>.</param>
	/// <exception cref="AuWndException">Invalid window handle.</exception>
	/// <exception cref="ArgumentException">An argument of this function or of constructor is invalid.</exception>
	/// <exception cref="AuException">Something failed.</exception>
	/// <exception cref="NotFoundException" />
	/// <remarks>
	/// Functions <b>Find</b> and <b>Exists</b> differ only in their return types.
	/// </remarks>
	public uiimage Find(IFArea area, double waitS) => Exists(area, waitS) ? Result : null;

	/// <returns>If found, sets <see cref="Result"/> and returns true, else false.</returns>
	/// <inheritdoc cref="Find(IFArea)"/>
	public bool Exists(IFArea area) {
		_Before(area, Action_.Find);
		return Find_();
	}

	/// <returns>If found, sets <see cref="Result"/> and returns true. Else throws exception or returns false (if <i>waitS</i> negative).</returns>
	/// <inheritdoc cref="Find(IFArea, double)"/>
	public bool Exists(IFArea area, double waitS) {
		bool r = waitS == 0 ? Exists(area) : Wait_(Action_.Wait, waitS < 0 ? waitS : -waitS, area);
		return r || double.IsNegative(waitS) ? r : throw new NotFoundException();
	}

	/// <summary>
	/// See <see cref="uiimage.wait"/>.
	/// </summary>
	/// <exception cref="Exception">Exceptions of <see cref="uiimage.wait"/>, except those of the constructor.</exception>
	/// <remarks>
	/// Same as <see cref="Find(IFArea, double)"/>, except:
	/// - 0 timeout means infinite.
	/// - on timeout throws <b>TimeoutException</b>, not <b>NotFoundException</b>.
	/// </remarks>
	public uiimage Wait(double secondsTimeout, IFArea area)
		=> Wait_(Action_.Wait, secondsTimeout, area) ? Result : null;
	//SHOULDDO: suspend waiting while a mouse button is pressed.
	//	Now, eg if finds while scrolling, although MouseMove waits until buttons released, but moves to the old (wrong) place.

	/// <summary>
	/// See <see cref="uiimage.waitNot"/>.
	/// </summary>
	/// <exception cref="Exception">Exceptions of <see cref="uiimage.waitNot"/>, except those of the constructor.</exception>
	public bool WaitNot(double secondsTimeout, IFArea area)
		=> Wait_(Action_.WaitNot, secondsTimeout, area);

	internal enum Action_ { Find, Wait, WaitNot, WaitChanged }

	internal bool Wait_(Action_ action, double secondsTimeout, IFArea area) {
		_Before(area, action);
		try { return wait.forCondition(secondsTimeout, () => Find_() ^ (action > Action_.Wait)); }
		finally { _After(); }

		//tested: does not create garbage while waiting.
	}

	//called at the start of Find_ and Wait_
	void _Before(IFArea area, Action_ action) {
		_area = area ?? throw new ArgumentNullException();
		_action = action;

		if (_action == Action_.WaitChanged) {
			Debug.Assert(_images.Count == 0 && _also == null); //the first Find_ will capture the area and add to _images
		} else {
			if (_images.Count == 0) throw new ArgumentException("no image");
		}

		IFFlags badFlags = 0;
		switch (_area.Type) {
		case IFArea.AreaType.Screen:
			badFlags = IFFlags.WindowDC | IFFlags.PrintWindow;
			break;
		case IFArea.AreaType.Wnd:
			_area.W.ThrowIfInvalid();
			break;
		case IFArea.AreaType.Elm:
			if (_area.E == null) throw new ArgumentNullException(nameof(area));
			_area.W = _area.E.WndContainer;
			goto case IFArea.AreaType.Wnd;
		case IFArea.AreaType.Bitmap:
			if (_action != Action_.Find) throw new ArgumentException("wait", "image");
			if (_area.B == null) throw new ArgumentNullException(nameof(area));
			badFlags = IFFlags.WindowDC | IFFlags.PrintWindow;
			break;
		}
		if (_flags.HasAny(badFlags)) throw new ArgumentException("Invalid flags for this area type: " + badFlags);

		Result = new uiimage(_area);
	}

	//called at the end of Find_ (if not waiting) and Wait_
	void _After() {
		_ad.Free(); _ad = default;
		_area = null;
		if (_action == Action_.WaitChanged) _images.Clear();
	}

	internal bool Find_() {
		//using var p1 = perf.local();
		Result.Clear_();

		bool inScreen = !_flags.HasAny(IFFlags.WindowDC | IFFlags.PrintWindow);
		bool failedGetRect = false;

		var w = _area.W;
		if (!w.Is0) {
			if (!w.IsVisible || w.IsMinimized) return false;
			if (inScreen && w.IsCloaked) return false;
		}

		//Get area rectangle.
		RECT r;
		_resultOffset = default;
		switch (_area.Type) {
		case IFArea.AreaType.Wnd:
			failedGetRect = !w.GetClientRect(out r, inScreen);
			break;
		case IFArea.AreaType.Elm:
			failedGetRect = !(inScreen ? _area.E.GetRect(out r) : _area.E.GetRect(out r, w));
			break;
		case IFArea.AreaType.Bitmap:
			r = new RECT(0, 0, _area.B.Width, _area.B.Height);
			break;
		default: //Screen
			r = _area.R;
			if (!screen.isInAnyScreen(r)) r = default;
			_area.HasRect = false;
			_resultOffset.x = r.left; _resultOffset.y = r.top;
			break;
		}
		if (failedGetRect) {
			w.ThrowIfInvalid();
			throw new AuException("*get rectangle");
		}

		//r is the area from where to get pixels. If !inScreen, it is relative to the client area.
		//Intermediate results will be relative to r. Then will be added _resultOffset if a limiting rectangle is used.

		if (_area.HasRect) {
			RECT rr;
			if (_area.HasCoord) {
				RECT rc = new(0, 0, r.Width, r.Height);
				POINT p1 = Coord.NormalizeInRect(_area.cLeft, _area.cTop, rc), p2 = Coord.NormalizeInRect(_area.cRight, _area.cBottom, rc);
				rr = RECT.FromLTRB(p1.x, p1.y, p2.x, p2.y);
			} else {
				rr = _area.R;
			}
			_resultOffset.x = rr.left; _resultOffset.y = rr.top;
			rr.Offset(r.left, r.top);
			r.Intersect(rr);
		}

		if (_area.Type == IFArea.AreaType.Elm) {
			//adjust r and _resultOffset,
			//	because object rectangle may be bigger than client area (eg WINDOW object)
			//	or its part is not in client area (eg scrolled web page).
			//	If not adjusted, then may capture part of parent or sibling controls or even other windows...
			//	Never mind: should also adjust control rectangle in ancestors in the same way.
			//		This is not so important because usually whole control is visible (resized, not clipped).
			int x = r.left, y = r.top;
			w.GetClientRect(out var rw, inScreen);
			r.Intersect(rw);
			x -= r.left; y -= r.top;
			_resultOffset.x -= x; _resultOffset.y -= y;
		}
		if (r.NoArea) return false; //never mind: if WaitChanged and this is the first time, immediately returns 'changed'

		//If WaitChanged, first time just get area pixels into _images[0].
		if (_action == Action_.WaitChanged && _images.Count == 0) {
			_GetAreaPixels(r, true);
			return true;
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
			if (_area.Type == IFArea.AreaType.Bitmap) {
				var pf = (_area.B.PixelFormat == PixelFormat.Format32bppArgb) ? PixelFormat.Format32bppArgb : PixelFormat.Format32bppRgb; //if possible, use PixelFormat of _area, to avoid conversion/copying. Both these formats are ok, we don't use alpha.
				bitmapBD = _area.B.LockBits(r, ImageLockMode.ReadOnly, pf);
				if (bitmapBD.Stride < 0) throw new ArgumentException("bottom-up Bitmap");
				_ad.pixels = (uint*)bitmapBD.Scan0;
				_ad.width = bitmapBD.Width; _ad.height = bitmapBD.Height;
			} else {
				_GetAreaPixels(r);
			}
			//p1.Next();

			//Find image(s) in area.
			uiimage alsoResult = null;
			for (int i = 0, n = _images.Count; i < n; i++) {
				Result.ListIndex = i;
				Result.MatchIndex = 0;
				if (_FindImage(_images[i], out var alsoAction, ref alsoResult)) return true;
				if (alsoAction == IFAlso.NotFound || alsoAction == IFAlso.FindOtherOfThis || alsoAction == IFAlso.OkFindMoreOfThis) break;
			}

			if (alsoResult != null) {
				Result = alsoResult;
				return true;
			}
			Result.Clear_();
			return false;
		}
		finally {
			if (bitmapBD != null) _area.B.UnlockBits(bitmapBD);
			if (_action == Action_.Find) _After();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	bool _FindImage(_Image image, out IFAlso alsoAction, ref uiimage alsoResult) {
		alsoAction = IFAlso.FindOtherOfList;

		int imageWidth = image.width, imageHeight = image.height;
		if (_ad.width < imageWidth || _ad.height < imageHeight) return false;
		fixed (uint* imagePixels = image.pixels) {
			uint* imagePixelsTo = imagePixels + imageWidth * imageHeight;
			uint* areaPixels = _ad.pixels;

			//rejected. Does not make faster, just adds more code.
			//if image is of same size as area, simply compare. For example, when action is WaitChanged.
			//if(imageWidth == _areaWidth && imageHeight == _areaHeight) {
			//	//print.it("same size");
			//	if(_skip > 0) return false;
			//	if(!_CompareSameSize(areaPixels, imagePixels, imagePixelsTo, _colorDiff)) return false;
			//	_tempResults ??= new List<RECT>();
			//	_tempResults.Add(new RECT(0, 0, imageWidth, imageHeight));
			//	return true;
			//}
			//else if(imagePixelCount == 1) { ... } //eg when image is color

			if (!image.optim.Init(image, _ad.width)) return false;
			var optim = image.optim; //copy struct, size = 9*int
			int o_pos0 = optim.v0.pos;
			var o_a1 = &optim.v1; var o_an = o_a1 + (optim.N - 1);

			//find first pixel. This part is very important for speed.
			//int nTimesFound = 0; //debug

			var areaWidthMinusImage = _ad.width - imageWidth;
			var pFirst = areaPixels + o_pos0;
			var pLast = pFirst + _ad.width * (_ad.height - imageHeight) + areaWidthMinusImage;

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
				var r = new RECT(iFound % _ad.width, iFound / _ad.width, imageWidth, imageHeight);
				_DpiScaleRect(ref r);
				r.Offset(_resultOffset.x, _resultOffset.y);
				Result.Rect = r;

				if (_also != null) {
					var wi = new uiimage(Result); //create new uiimage object because the callback may add it to a list etc
					switch (alsoAction = _also(wi)) {
					case IFAlso.OkReturn:
						alsoResult = null;
						break;
					case IFAlso.OkFindMore:
					case IFAlso.OkFindMoreOfThis:
						alsoResult = wi;
						goto case IFAlso.FindOther;
					case IFAlso.OkFindMoreOfList:
						alsoResult = wi;
						goto gNotFound;
					case IFAlso.NotFound:
					case IFAlso.FindOtherOfList:
						goto gNotFound;
					case IFAlso.FindOther:
					case IFAlso.FindOtherOfThis:
						Result.MatchIndex++;
						goto gContinue;
					default: throw new InvalidEnumArgumentException();
					}
				}
			}
		} //fixed

		return true;
		gNotFound:
		return false;
	}

	struct _FindData
	{
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

	struct _OptimizationData
	{
		internal struct POSCOLOR
		{
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

	void _GetAreaPixels(RECT r, bool toImage0 = false) {
		//Transfer from screen/window DC to memory DC (does not work without this) and get pixels.
		//This is the slowest part of Find, especially BitBlt.
		//Speed depends on computer, driver, OS version, theme, size.
		//For example, with Aero theme 2-15 times slower (on Windows 8/10 cannot disable Aero).
		//With incorrect/generic video driver can be 10 times slower. Eg on vmware virtual PC.
		//Much faster when using window DC. Then same speed as without Aero.

		int areaWidth = r.Width, areaHeight = r.Height;
		//_Debug("start", 1);
		//create memory bitmap. When waiting, we reuse _ad.mb, it makes slightly faster.
		if (_ad.mb == null || areaWidth != _ad.width || areaHeight != _ad.height) {
			if (_ad.mb != null) { _ad.mb.Dispose(); _ad.mb = null; }
			_ad.mb = new MemoryBitmap(_ad.width = areaWidth, _ad.height = areaHeight);
			//_Debug("created MemBmp");
		}

		//copy from screen/window to memory bitmap
		if (0 != (_flags & IFFlags.PrintWindow)) {
			//PW_RENDERFULLCONTENT is new in Win8.1. Undocumented in MSDN, but defined in h. Then works with windows like Chrome, winstore.
			if (!Api.PrintWindow(_area.W, _ad.mb.Hdc, Api.PW_CLIENTONLY | (osVersion.minWin8_1 ? Api.PW_RENDERFULLCONTENT : 0)))
				_area.W.ThrowNoNative("Failed to get pixels");
		} else {
			//get DC of screen or window
			bool windowDC = 0 != (_flags & IFFlags.WindowDC);
			wnd w = windowDC ? _area.W : default;
			using var dc = new WindowDC_(w);
			if (dc.Is0) w.ThrowNoNative("Failed to get pixels");
			//_Debug("get DC");
			//copy from screen/window DC to memory bitmap
			uint rop = windowDC ? Api.SRCCOPY : Api.SRCCOPY | Api.CAPTUREBLT;
			bool bbOK = Api.BitBlt(_ad.mb.Hdc, 0, 0, areaWidth, areaHeight, dc, r.left, r.top, rop);
			if (!bbOK) throw new AuException("BitBlt"); //the API fails only if a HDC is invalid
		}

		//_Debug("captured to MemBmp");
		//get pixels
		var h = new Api.BITMAPINFOHEADER {
			biSize = sizeof(Api.BITMAPINFOHEADER),
			biWidth = areaWidth,
			biHeight = -areaHeight,
			biPlanes = 1,
			biBitCount = 32,
			//biCompression = 0, //BI_RGB
		};
		int memSize = areaWidth * areaHeight * 4; //7.5 MB for a max window in 1920*1080 screen
		if (toImage0) {
			var im = new _Image { width = areaWidth, height = areaHeight, pixels = new uint[areaWidth * areaHeight] };
			fixed (uint* p = im.pixels) _GetBits(p);
			_images.Add(im);
		} else {
			if (memSize > _ad.memSize) { //while waiting, we reuse the memory, it makes slightly faster.
				MemoryUtil.VirtualFree(_ad.pixels);
				_ad.pixels = (uint*)MemoryUtil.VirtualAlloc(memSize);
				_ad.memSize = memSize;
			}
			_GetBits(_ad.pixels);
		}
		//_Debug("_GetBitmapBits", 3);

		void _GetBits(uint* pixels) {
			var bi = new Api.BITMAPINFO(areaWidth, -areaHeight, 32);
			if (areaHeight != Api.GetDIBits(_ad.mb.Hdc, _ad.mb.Hbitmap, 0, areaHeight, pixels, ref bi, 0)) //DIB_RGB_COLORS
				throw new AuException("GetDIBits");

			//remove alpha. Don't need for area.
			if (toImage0) {
				byte* b = (byte*)pixels, be = b + memSize;
				for (b += 3; b < be; b += 4) *b = 0xff;
			}

			//var testFile = folders.Temp + @"uiimage\" + s_test++ + ".png";
			//filesystem.createDirectoryFor(testFile);
			//using(var areaBmp = new Bitmap(areaWidth, areaHeight, areaWidth * 4, PixelFormat.Format32bppRgb, (IntPtr)p)) {
			//	areaBmp.Save(testFile);
			//}
			////run.it(testFile);
		}
	}

	//r is relative to the search area
	void _DpiScaleRect(ref RECT r) {
		if (!_flags.HasAny(IFFlags.WindowDC | IFFlags.PrintWindow)) return;
		var w = _area.W;
		if (!Dpi.GetScalingInfo_(w, out bool scaled, out _, out _) || !scaled) return; //makes faster on Win10+; don't scale on older OS
		int d1 = screen.of(w.Window).Dpi, d2 = Dpi.OfWindow(w);
		r.left = Math2.MulDiv(r.left, d1, d2);
		r.top = Math2.MulDiv(r.top, d1, d2);
		r.right = Math2.MulDiv(r.right, d1, d2);
		r.bottom = Math2.MulDiv(r.bottom, d1, d2);
	}

	//[Conditional("WI_DEBUG_PERF")]
	//static void _Debug(string s, int perfAction = 2)
	//{
	//	//MessageBox.Show(s);
	//	switch(perfAction) {
	//	case 1: perf.first(); break;
	//	case 2: perf.next(); break;
	//	case 3: perf.nw(); break;
	//	}
	//}
}
//static int s_test;
