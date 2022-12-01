
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace Au
{
	public partial class uiimage
	{
		#region capture, etc

		/// <summary>
		/// Creates image from a rectangle of screen pixels.
		/// </summary>
		/// <param name="r">Rectangle in screen.</param>
		/// <exception cref="ArgumentException">Empty rectangle.</exception>
		/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of this size (<c>width*height*4</c> bytes).</exception>
		/// <remarks>
		/// <b>PixelFormat</b> is always <b>PixelFormat.Format32bppRgb</b>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var file = folders.Temp + "notepad.png";
		/// wnd w = wnd.find("* Notepad");
		/// w.Activate();
		/// wnd.active.GetRect(out var r, true);
		/// using(var b = uiimage.capture(r)) { b.Save(file); }
		/// run.it(file);
		/// ]]></code>
		/// </example>
		public static Bitmap capture(RECT r) => _Capture(r);

		/// <summary>
		/// Creates image from a rectangle of window client area pixels.
		/// </summary>
		/// <param name="w">Window or control.</param>
		/// <param name="r">Rectangle in <i>w</i> client area coordinates. Use <c>w.ClientRect</c> to get whole client area.</param>
		/// <param name="printWindow">Get pixels like with flag <see cref="IFFlags.PrintWindow"/>.</param>
		/// <exception cref="AuWndException">Invalid <i>w</i>.</exception>
		/// <exception cref="ArgumentException">Empty rectangle.</exception>
		/// <exception cref="AuException">Failed. For example there is not enough memory for bitmap of this size (<c>width*height*4</c> bytes).</exception>
		/// <remarks>
		/// This overload gets pixels directly from window, not from screen. Like with flag <see cref="IFFlags.WindowDC"/> or <see cref="IFFlags.PrintWindow"/>. The window can be under other windows. The captured image can be different than displayed on screen.
		/// If the window is partially or completely transparent, captures its non-transparent view.
		/// If the window is DPI-scaled, captures its non-scaled view. And <i>r</i> must contain non-scaled coordinates.
		/// </remarks>
		public static Bitmap capture(wnd w, RECT r, bool printWindow = false)
			=> _Capture(r, w.ThrowIfInvalid(), printWindow);

		static unsafe Bitmap _Capture(RECT r, wnd w = default, bool printWindow = false, GraphicsPath path = null) {
			//Transfer from screen/window DC to memory DC (does not work without this) and get pixels.

			//rejected: parameter includeNonClient (GetWindowDC).
			//	Nothing good. If in background, captures incorrect caption etc.
			//	If need nonclient part, better activate window and capture window rectangle from screen.

			//FUTURE: if w is DWM-scaled...

			using var mb = new MemoryBitmap(r.Width, r.Height);
			_CaptureToDC(mb, r, w, printWindow);

#if !true //with this code can allocate gigabytes without triggering GC
			var R = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
			try {
				var d = R.LockBits(new Rectangle(0, 0, r.Width, r.Height), ImageLockMode.ReadWrite, R.PixelFormat); //tested: fast, no copy
				try { _GetPixelsFromDC(mb, r, (uint*)d.Scan0, path); }
				finally { R.UnlockBits(d); } //tested: fast, no copy
				return R;
			}
			catch { R.Dispose(); throw; }
#elif !true //with this code GC should be OK, but isn't. Allocates large amount before starting to free. Also unsafe etc because uses Tag.
			var a = GC.AllocateUninitializedArray<uint>(r.Width * r.Height, pinned: true);
			fixed (uint* p = a) {
				_GetPixelsFromDC(mb, r, p, path);
				return new Bitmap(r.Width, r.Height, r.Width * 4, PixelFormat.Format32bppRgb, (IntPtr)p) { Tag = a };
			}
#else
			var R = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppRgb);
			GC_.AddObjectMemoryPressure(R, r.Width * r.Height * 4);
			var d = R.LockBits(new(0, 0, r.Width, r.Height), ImageLockMode.ReadWrite, R.PixelFormat); //tested: fast, no copy
			try { _GetPixelsFromDC(mb, r, (uint*)d.Scan0, path); }
			finally { R.UnlockBits(d); } //tested: fast, no copy
			return R;
#endif
		}

		static void _CaptureToDC(MemoryBitmap mb, RECT r, wnd w = default, bool printWindow = false) {
			if (printWindow) {
				if(!Api.PrintWindow(w, mb.Hdc, Api.PW_CLIENTONLY | (osVersion.minWin8_1 ? Api.PW_RENDERFULLCONTENT : 0)))
					w.ThrowNoNative("Failed to get pixels");
			} else {
				using var dc = new WindowDC_(w);
				if (dc.Is0) w.ThrowNoNative("Failed to get pixels");
				uint rop = !w.Is0 ? Api.SRCCOPY : Api.SRCCOPY | Api.CAPTUREBLT;
				bool ok = Api.BitBlt(mb.Hdc, 0, 0, r.Width, r.Height, dc, r.left, r.top, rop);
				Debug.Assert(ok); //the API fails only if a HDC is invalid
			}
		}

		static unsafe void _GetPixelsFromDC(MemoryBitmap mb, RECT r, uint* pixels, GraphicsPath path = null) {
			var bi = new Api.BITMAPINFO(r.Width, -r.Height, 32);
			var n = Api.GetDIBits(mb.Hdc, mb.Hbitmap, 0, r.Height, pixels, ref bi, 0); //DIB_RGB_COLORS
			if (n != r.Height) throw new AuException("GetDIBits");
			_SetAlpha(pixels, r, path);
		}

		static unsafe void _SetAlpha(uint* pixels, RECT r, GraphicsPath path = null) {
			//remove alpha. Will compress better.
			//perf.first();
			byte* p = (byte*)pixels, pe = p + r.Width * r.Height * 4;
			for (p += 3; p < pe; p += 4) *p = 0xff;
			//perf.nw(); //1100 for max window

			//if path used, set alpha=0 for outer points
			if (path != null) {
				uint* k = pixels;
				for (int y = r.top; y < r.bottom; y++)
					for (int x = r.left; x < r.right; x++, k++) {
						//print.it(x, y, path.IsVisible(x, y));
						if (!path.IsVisible(x, y)) *k = 0xFFFFFF; //white, 0 alpha
					}
			}
		}

		static GraphicsPath _CreatePath(List<POINT> outline) {
			int n = outline.Count;
			Debug.Assert(n > 1);

			var p = new Point[n];
			for (int i = 0; i < n; i++) p[i] = outline[i];

			var t = new byte[n];
			//t[0] = (byte)PathPointType.Start; //0
			for (int i = 1; i < n; i++) t[i] = (byte)PathPointType.Line;
			t[n - 1] |= (byte)PathPointType.CloseSubpath;

			return new GraphicsPath(p, t);
		}

		static Bitmap _Capture(List<POINT> outline, wnd w = default, bool printWindow = false) {
			int n = outline?.Count ?? 0;
			if (n == 0) throw new ArgumentException();
			if (n == 1) return _Capture(new RECT(outline[0].x, outline[0].y, 1, 1));

			using var path = _CreatePath(outline);
			RECT r = RECT.From(path.GetBounds(), false);
			if (r.NoArea) {
				path.Widen(Pens.Black); //will be transparent, but no exception. Difficult to make non-transparent line.
				r = RECT.From(path.GetBounds(), false);
			}
			return _Capture(r, w, printWindow, path);
		}

		/// <summary>
		/// Creates image from a non-rectangular area of screen pixels.
		/// </summary>
		/// <param name="outline">The outline (shape) of the area in screen. If single element, captures single pixel.</param>
		/// <exception cref="ArgumentException"><i>outline</i> is null or has 0 elements.</exception>
		/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of this size.</exception>
		/// <remarks>
		/// <b>PixelFormat</b> is always <b>PixelFormat.Format32bppRgb</b>.
		/// </remarks>
		public static Bitmap capture(List<POINT> outline) {
			return _Capture(outline);
		}

		/// <summary>
		/// Creates image from a non-rectangular area of window client area pixels.
		/// </summary>
		/// <param name="w">Window or control.</param>
		/// <param name="outline">The outline (shape) of the area in <i>w</i> client area coordinates. If single element, captures single pixel.</param>
		/// <param name="printWindow">Get pixels like with flag <see cref="IFFlags.PrintWindow"/>.</param>
		/// <exception cref="AuWndException">Invalid <i>w</i>.</exception>
		/// <exception cref="ArgumentException"><i>outline</i> is null or has 0 elements.</exception>
		/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of this size.</exception>
		public static Bitmap capture(wnd w, List<POINT> outline, bool printWindow = false) {
			w.ThrowIfInvalid();
			return _Capture(outline, w, printWindow);
		}

		/// <summary>
		/// Gets pixel colors from a rectangle in screen.
		/// </summary>
		/// <returns>2-dimensional array [row, column] containing pixel colors in 0xAARRGGBB format. Alpha 0xFF.</returns>
		/// <param name="r">Rectangle in screen.</param>
		/// <exception cref="ArgumentException">Empty rectangle.</exception>
		/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of this size (<c>width*height*4</c> bytes).</exception>
		/// <remarks>
		/// Getting pixels from screen usually is slow. If need faster, try <see cref="getPixels(wnd, RECT, bool)"/> (get pixels from window client area).
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// print.clear();
		/// var a = uiimage.getPixels(new(100, 100, 4, 10));
		/// for(int i = 0, nRows = a.GetLength(0); i < nRows; i++) print.it(a[i,0], a[i,1], a[i,2], a[i,3]);
		/// ]]></code>
		/// </example>
		public static uint[,] getPixels(RECT r) => _Pixels(r);

		/// <summary>
		/// Gets pixel colors from a rectangle in window client area.
		/// </summary>
		/// <returns>2-dimensional array [row, column] containing pixel colors in 0xAARRGGBB format. Alpha 0xFF.</returns>
		/// <param name="w">Window or control.</param>
		/// <param name="r">Rectangle in <i>w</i> client area coordinates. Use <c>w.ClientRect</c> to get whole client area.</param>
		/// <param name="printWindow">Get pixels like with flag <see cref="IFFlags.PrintWindow"/>.</param>
		/// <exception cref="AuWndException">Invalid <i>w</i>.</exception>
		/// <exception cref="ArgumentException">Empty rectangle.</exception>
		/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of this size (<c>width*height*4</c> bytes).</exception>
		/// <remarks>
		/// Unlike <see cref="getPixels(RECT)"/>, this overload gets pixels directly from window, not from screen. Like with flag <see cref="IFFlags.WindowDC"/> or <see cref="IFFlags.PrintWindow"/>. The window can be under other windows. The captured image can be different than displayed on screen.
		/// If the window is partially or completely transparent, captures its non-transparent view.
		/// If the window is DPI-scaled, captures its non-scaled view. And <i>r</i> must contain non-scaled coordinates.
		/// </remarks>
		public static uint[,] getPixels(wnd w, RECT r, bool printWindow = false)
			=> _Pixels(r, w.ThrowIfInvalid(), printWindow);

		static unsafe uint[,] _Pixels(RECT r, wnd w = default, bool printWindow = false) {
			using var mb = new MemoryBitmap(r.Width, r.Height);
			_CaptureToDC(mb, r, w, printWindow);
			var a = new uint[r.Height, r.Width];
			fixed (uint* p = a) { _GetPixelsFromDC(mb, r, p); }
			return a;
		}

		/// <summary>
		/// Gets color of a screen pixel.
		/// </summary>
		/// <param name="p">x y in screen.</param>
		/// <returns>Pixel color in 0xAARRGGBB format. Alpha 0xFF. Returns 0 if fails, eg if x y is not in screen.</returns>
		/// <remarks>
		/// Uses, API <msdn>GetPixel</msdn>. It is slow. If need faster, try <see cref="getPixels(wnd, RECT, bool)"/> (get 1 or more pixels from window client area).
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// print.clear();
		/// for (;;) {
		/// 	1.s();
		/// 	print.it(uiimage.getPixel(mouse.xy));
		/// }
		/// ]]></code>
		/// </example>
		public static unsafe uint getPixel(POINT p) {
			using var dc = new ScreenDC_();
			//using var dc = new WindowDC_(wnd.getwnd.root); //same speed. Same with printwindow.
			uint R = Api.GetPixel(dc, p.x, p.y);
			if (R == 0xFFFFFFFF) return 0; //it's better than exception
			return ColorInt.SwapRB(R) | 0xFF000000;
		}

		#endregion

		#region capture UI

		/// <summary>
		/// Creates image from a user-selected area of screen pixels. Or gets single pixel color, or just rectangle.
		/// </summary>
		/// <returns>false if canceled.</returns>
		/// <param name="result">Receives results.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window. Temporarily minimizes it.</param>
		/// <remarks>
		/// Gets all screen pixels and shows in a full-screen topmost window, where the user can select an area.
		/// </remarks>
		public static bool captureUI(out ICResult result, ICFlags flags = 0, AnyWnd owner = default) {
			result = default;

			switch (flags & (ICFlags.Image | ICFlags.Color | ICFlags.Rectangle)) {
			case 0: case ICFlags.Image: case ICFlags.Color: case ICFlags.Rectangle: break;
			default: throw new ArgumentException();
			}

			wnd wTool = default;
			try {
				if (!owner.IsEmpty) {
					wTool = owner.Hwnd;
					wTool.ShowMinimized(1);
					using (new inputBlocker(BIEvents.MouseClicks)) Au.wait.doEvents(300); //time for animations
				}

				g1:
				RECT rs = screen.virtualScreen;
				//RECT rs = screen.primary.Rect; //for testing, to see Write output in other screen
				Bitmap bs;
				bool windowPixels = flags.HasAny(ICFlags.WindowDC | ICFlags.PrintWindow);
				if (windowPixels) {
					if (!_WaitForHotkey("Press F3 to select window from mouse pointer. Or Esc.")) return false;
					var w = wnd.fromMouse(WXYFlags.NeedWindow);
					var rc = w.ClientRect;
					using var bw = capture(w, rc, flags.Has(ICFlags.PrintWindow));
					bs = new Bitmap(rs.Width, rs.Height);
					using var g = Graphics.FromImage(bs);
					g.Clear(Color.Black);
					w.MapClientToScreen(ref rc);
					g.DrawImage(bw, rc.left - rs.left, rc.top - rs.top);
				} else {
					bs = capture(rs);
				}

				var cw = new _CapturingWindow();
				switch (cw.Show(bs, flags, rs)) {
				case 1: break;
				case 2:
					if (!windowPixels && !_WaitForHotkey("Press F3 when ready for new screenshot. Or Esc.")) return false;
					goto g1;
				default: return false;
				}

				var r = cw.Result;
				r.w = _WindowFromRect(r);
				result = r;
			}
			finally {
				if (wTool.IsAlive) {
					wTool.ShowNotMinimized();
					wTool.ActivateL();
				}
			}
			return true;

			static bool _WaitForHotkey(string info) {
				using (osdText.showText(info, Timeout.Infinite)) {
					//try { keys.waitForHotkey(0, KKey.F3); }
					//catch(AuException) { dialog.showError("Failed to register hotkey F3"); return false; }

					return KKey.F3 == keys.waitForKeys(0, k => !k.IsUp && k.Key is KKey.F3 or KKey.Escape, block: true);
				}
			}

			static wnd _WindowFromRect(ICResult r) {
				Thread.Sleep(25); //after the window is closed, sometimes need several ms until OS sets correct Z order. Until that may get different w1 and w2.
				wnd w1 = wnd.fromXY((r.rect.left, r.rect.top));
				wnd w2 = (r.image == null) ? w1 : wnd.fromXY((r.rect.right - 1, r.rect.bottom - 1));
				if (w2 != w1 || !_IsInClientArea(w1)) {
					wnd w3 = w1.Window, w4 = w2.Window;
					w1 = (w4 == w3 && _IsInClientArea(w3)) ? w3 : default;
				}
				return w1;

				bool _IsInClientArea(wnd w) => w.GetClientRect(out var rc, true) && rc.Contains(r.rect);
			}
		}

		class _CapturingWindow
		{
			wnd _w;
			Bitmap _img;
			bool _paintedOnce;
			bool _magnMoved;
			bool _capturing;
			ICFlags _flags;
			MouseCursor _cursor;
			SIZE _textSize;
			int _dpi;
			int _res;

			public ICResult Result;

			/// <returns>0 Cancel, 1 OK, 2 Retry.</returns>
			public int Show(Bitmap img, ICFlags flags, RECT r) {
				_img = img;
				_flags = flags;
				_cursor = MouseCursor.Load(ResourceUtil.GetBytes("<Au>resources/red_cross_cursor.cur"), 32);
				_dpi = screen.primary.Dpi;
				_w = WndUtil.CreateWindow(_WndProc, true, WndUtil.WindowClassDWP_, "Au.uiimage.captureUI", WS.POPUP | WS.VISIBLE, WSE.TOOLWINDOW | WSE.TOPMOST, r.left, r.top, r.Width, r.Height);
				_w.ActivateL();

				try {
					while (Api.GetMessage(out var m) > 0 && m.message != Api.WM_APP) {
						switch (m.message) {
						case Api.WM_KEYDOWN when !_capturing:
							switch ((KKey)(int)m.wParam) {
							case KKey.Escape: return 0;
							case KKey.F3: return 2;
							}
							break;
						case Api.WM_RBUTTONUP when m.hwnd == _w:
							switch (popupMenu.showSimple("1 Retry\tF3|2 Cancel\tEsc", owner: _w)) {
							case 1: return 2;
							case 2: return 0;
							}
							break;
						}
						Api.DispatchMessage(m);
					}
				}
				finally {
					var w = _w; _w = default;
					Api.DestroyWindow(w);
				}
				return _res;
			}

			nint _WndProc(wnd w, int msg, nint wParam, nint lParam) {
				//WndUtil.PrintMsg(w, msg, wParam, lParam);

				switch (msg) {
				case Api.WM_NCDESTROY:
					_img.Dispose();
					_cursor?.Dispose();
					if (_w != default) {
						_w = default;
						_w.Post(Api.WM_APP);
					}
					break;
				case Api.WM_SETCURSOR:
					Api.SetCursor(_cursor.Handle);
					return 1;
				case Api.WM_ERASEBKGND:
					return default;
				case Api.WM_PAINT:
					var dc = Api.BeginPaint(w, out var ps);
					_WmPaint(dc);
					Api.EndPaint(w, ps);
					return default;
				case Api.WM_MOUSEMOVE:
					_WmMousemove(Math2.NintToPOINT(lParam));
					break;
				case Api.WM_LBUTTONDOWN:
					_WmLbuttondown(Math2.NintToPOINT(lParam));
					break;
				}

				return Api.DefWindowProc(w, msg, wParam, lParam);
			}

			void _WmPaint(IntPtr dc) {
				using var g = Graphics.FromHdc(dc);
				g.DrawImageUnscaled(_img, 0, 0);
				_paintedOnce = true;
			}

			void _WmMousemove(POINT pc) {
				if (!_paintedOnce) return;

				//format text to draw below magnifier
				string text;
				using (new StringBuilder_(out var s)) {
					var ic = _flags & (ICFlags.Image | ICFlags.Color | ICFlags.Rectangle);
					if (ic == 0) ic = ICFlags.Image | ICFlags.Color;
					bool canColor = ic.Has(ICFlags.Color);
					if (canColor) {
						var color = _img.GetPixel(pc.x, pc.y).ToArgb() & 0xffffff;
						s.Append("Color  #").Append(color.ToString("X6")).Append('\n');
					}
					if (ic == ICFlags.Color) {
						s.Append("Click to capture color.\n");
					} else if (ic == ICFlags.Rectangle) {
						s.Append("Mouse-drag to capture rectangle.\n");
					} else if (!canColor) {
						s.Append("Mouse-drag to capture image.\n");
					} else {
						s.Append("Mouse-drag to capture image,\nor Ctrl+click to capture color.\n");
					}
					s.Append("More:  right-click"); //"  cancel:  key Esc\n  retry:  key F3 ... F3"
					text = s.ToString();
				}

				var font = NativeFont_.RegularCached(_dpi);
				int magnWH = Dpi.Scale(200, _dpi) / 10 * 10; //width and height of the magnified image without borders etc
				if (_textSize == default) using (var tr = new FontDC_(font)) _textSize = tr.MeasureDT(text, TFFlags.NOPREFIX);
				int width = Math.Max(magnWH, _textSize.width) + 2, height = magnWH + 4 + _textSize.height;
				using var mb = new MemoryBitmap(width, height);
				var dc = mb.Hdc;
				using var wdc = new WindowDC_(_w);

				//draw frames and color background. Also erase magnifier, need when near screen edges.
				Api.FillRect(dc, (0, 0, width, height), Api.GetStockObject(4)); //BLACK_BRUSH

				//copy from captured screen image to magnifier image. Magnify 5 times.
				int k = magnWH / 10;
				Api.StretchBlt(dc, 1, 1, magnWH, magnWH, wdc, pc.x - k, pc.y - k, k * 2, k * 2, Api.SRCCOPY);

				//draw red crosshair
				k = magnWH / 2;
				using (var pen = new GdiPen_(0xff)) {
					pen.DrawLine(dc, (k, 1), (k, magnWH + 1));
					pen.DrawLine(dc, (1, k), (magnWH + 1, k));
				}

				//draw text below magnifier
				var rc = new RECT(1, magnWH + 2, _textSize.width, _textSize.height);
				Api.SetTextColor(dc, 0x32CD9A); //Color.YellowGreen
				Api.SetBkMode(dc, 1);
				var oldFont = Api.SelectObject(dc, font);
				Api.DrawText(dc, text, ref rc, TFFlags.NOPREFIX);
				Api.SelectObject(dc, oldFont);

				//set magninifier position far from cursor
				var pm = new POINT(4, 4); _w.MapScreenToClient(ref pm);
				int xMove = magnWH * 3;
				if (_magnMoved) pm.Offset(xMove, 0);
				var rm = new RECT(pm.x, pm.y, width, height); rm.Inflate(magnWH / 2, magnWH / 2);
				if (rm.Contains(pc)) {
					Api.InvalidateRect(_w, (pm.x, pm.y, width, height));
					_magnMoved ^= true;
					pm.Offset(_magnMoved ? xMove : -xMove, 0);
				}

				Api.BitBlt(wdc, pm.x, pm.y, width, height, dc, 0, 0, Api.SRCCOPY);
			}

			void _WmLbuttondown(POINT p0) {
				bool isColor = false;
				//bool isAnyShape = false; //rejected. Not useful.
				var ic = _flags & (ICFlags.Image | ICFlags.Color | ICFlags.Rectangle);
				if (ic == ICFlags.Color) {
					isColor = true;
				} else {
					var mod = keys.gui.getMod();
					if (mod != 0 && ic == ICFlags.Rectangle) return;
					switch (mod) {
					case 0: break;
					//case KMod.Shift: isAnyShape = true; break;
					case KMod.Ctrl when ic == 0: isColor = true; break;
					default: return;
					}
				}

				Result = new ICResult();
				var r = new RECT(p0.x, p0.y, 0, 0);
				if (isColor) {
					Result.color = (uint)_img.GetPixel(p0.x, p0.y).ToArgb();
					r.right++; r.bottom++;
				} else {
					//var a = isAnyShape ? new List<POINT>() { p0 } : null;
					var pen = Pens.Red;
					bool notFirstMove = false;
					_capturing = true;
					try {
						if (!WndUtil.DragLoop(_w, MButtons.Left, m => {
							if (m.msg.message != Api.WM_MOUSEMOVE) return;
							POINT p = m.msg.pt; _w.MapScreenToClient(ref p);
							using var g = Graphics.FromHwnd(_w.Handle);
							//if (isAnyShape) {
							//	a.Add(p);
							//	g.DrawLine(pen, p0, p);
							//	p0 = p;
							//} else {
							if (notFirstMove) { //erase prev rect
								r.right++; r.bottom++;
								g.DrawImage(_img, r, r, GraphicsUnit.Pixel);
								//FUTURE: prevent flickering. Also don't draw under magnifier.
							} else notFirstMove = true;
							r = RECT.FromLTRB(p0.x, p0.y, p.x, p.y);
							r.Normalize(true);
							g.DrawRectangle(pen, r);
							//}
						})) { //Esc key etc
							Api.InvalidateRect(_w);
							return;
						}
					}
					finally { _capturing = false; }

					//GraphicsPath path = null;
					//if (isAnyShape && a.Count > 1) {
					//	path = _CreatePath(a);
					//	r = RECT.From(path.GetBounds(), false);
					//} else {
					r.right++; r.bottom++;
					//}
					if (r.NoArea) {
						Api.DestroyWindow(_w);
						return;
					}

					if (ic != ICFlags.Rectangle) {
						var b = _img.Clone(r, PixelFormat.Format32bppArgb);
						var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, b.PixelFormat);
						try { unsafe { _SetAlpha((uint*)d.Scan0, r/*, path*/); } }
						finally { b.UnlockBits(d); /*path?.Dispose();*/ }
						Result.image = b;
					}

				}
				_w.MapClientToScreen(ref r);
				Result.rect = r;

				_res = 1;
				Api.DestroyWindow(_w);
			}
		}

		#endregion
	}
}

namespace Au.Types
{
	/// <summary>
	/// Flags for <see cref="uiimage.captureUI"/>.
	/// </summary>
	/// <remarks>
	/// Only one of flags <b>Image</b>, <b>Color</b> and <b>Rectangle</b> can be used. If none, can capture image or color.
	/// </remarks>
	[Flags]
	public enum ICFlags
	{
		/// <summary>Can capture only image, not color.</summary>
		Image = 1,

		/// <summary>Can capture only color, not image.</summary>
		Color = 2,

		/// <summary>Capture only rectangle, not image/color.</summary>
		Rectangle = 4,

		/// <summary>
		/// Get pixels from the client area device context (DC) of a user-selected window, not from screen DC.
		/// More info: <see cref="IFFlags.WindowDC"/>.
		/// </summary>
		WindowDC = 8,

		/// <summary>
		/// Get pixels from the client area of a user-selected window using API <msdn>PrintWindow</msdn>.
		/// More info: <see cref="IFFlags.PrintWindow"/>.
		/// </summary>
		PrintWindow = 16,
	}

	/// <summary>
	/// Results of <see cref="uiimage.captureUI"/>.
	/// </summary>
	public class ICResult
	{
		/// <summary>
		/// Captured image.
		/// null if captured single pixel color or used flag <see cref="ICFlags.Rectangle"/>.
		/// </summary>
		public Bitmap image;

		/// <summary>
		/// Captured color in 0xAARRGGBB format. Alpha 0xFF.
		/// </summary>
		public uint color;

		/// <summary>
		/// Location of the captured image or rectangle, in screen coordinates.
		/// </summary>
		public RECT rect;

		/// <summary>
		/// Window or control containing the captured image or rectangle, if whole image is in its client area.
		/// In some cases may be incorrect, for example if windows moved/opened/closed/etc while capturing.
		/// </summary>
		public wnd w;
	}
}
