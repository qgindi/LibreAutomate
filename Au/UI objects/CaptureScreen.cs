using System.Drawing;
using System.Drawing.Imaging;

namespace Au.More {
	/// <summary>
	/// Contains functions and tools to capture image, color, window, rectangle or point from screen.
	/// </summary>
	public static class CaptureScreen {
		#region capture image

		/// <summary>
		/// Creates image from a rectangle of screen pixels.
		/// </summary>
		/// <param name="r">Rectangle in screen.</param>
		/// <exception cref="ArgumentException">Empty rectangle.</exception>
		/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of this size (<c>width*height*4</c> bytes).</exception>
		/// <example>
		/// <code><![CDATA[
		/// var file = folders.Temp + "notepad.png";
		/// wnd w = wnd.find("* Notepad");
		/// w.GetRect(out var r, true);
		/// using(var b = CaptureScreen.Image(r)) { b.Save(file); }
		/// run.it(file);
		/// ]]></code>
		/// </example>
		public static Bitmap Image(RECT r) {
			using var c = new CaptureScreenImage();
			c.Capture(r);
			return c.ToBitmap();
		}

		/// <summary>
		/// Creates image from a rectangle of window client area pixels.
		/// </summary>
		/// <param name="w">Window or control.</param>
		/// <param name="r">Rectangle in <i>w</i> client area coordinates. If <c>null</c>, uses <c>w.ClientRect</c>.</param>
		/// <exception cref="AuWndException">Invalid <i>w</i>.</exception>
		/// <exception cref="ArgumentException">The rectangle is empty or does not intercect with the window's client area.</exception>
		/// <exception cref="AuException">Failed. For example there is not enough memory for bitmap of this size (<c>width*height*4</c> bytes).</exception>
		/// <remarks>
		/// If <i>flags</i> contains <b>WindowDC</b> (default) or <b>PrintWindow</b>:
		/// - If the window is partially or completely transparent, captures its non-transparent view.
		/// - If the window is DPI-scaled, captures its non-scaled view. However <i>r</i> must contain scaled coordinates.
		/// </remarks>
		public static Bitmap Image(wnd w, RECT? r = null, CIFlags flags = CIFlags.WindowDC) {
			using var c = new CaptureScreenImage();
			if (!c.Capture(w, r, flags)) return null;
			return c.ToBitmap();
		}

		/// <summary>
		/// Gets pixel colors from a rectangle in screen.
		/// </summary>
		/// <returns>2-dimensional array [row, column] containing pixel colors in 0xAARRGGBB format. Alpha 0xFF.</returns>
		/// <param name="r">Rectangle in screen.</param>
		/// <exception cref="ArgumentException">Empty rectangle.</exception>
		/// <exception cref="AuException">Failed. Probably there is not enough memory for bitmap of this size (<c>width*height*4</c> bytes).</exception>
		/// <example>
		/// <code><![CDATA[
		/// print.clear();
		/// var a = CaptureScreen.Pixels(new(100, 100, 4, 10));
		/// for(int i = 0, nRows = a.GetLength(0); i < nRows; i++) print.it(a[i,0], a[i,1], a[i,2], a[i,3]);
		/// ]]></code>
		/// </example>
		public static uint[,] Pixels(RECT r) {
			using var c = new CaptureScreenImage();
			c.Capture(r);
			return c.ToArray2D();
		}

		/// <summary>
		/// Gets pixel colors from a rectangle in window client area.
		/// </summary>
		/// <returns>2-dimensional array [row, column] containing pixel colors in 0xAARRGGBB format. Alpha 0xFF.</returns>
		/// <inheritdoc cref="Image(wnd, RECT?, CIFlags)"/>
		public static uint[,] Pixels(wnd w, RECT? r = null, CIFlags flags = CIFlags.WindowDC) {
			using var c = new CaptureScreenImage();
			if (!c.Capture(w, r, flags)) return null;
			return c.ToArray2D();
		}

		/// <summary>
		/// Gets color of a screen pixel.
		/// </summary>
		/// <param name="p">x y in screen.</param>
		/// <returns>Pixel color in 0xAARRGGBB format. Alpha 0xFF. Returns 0 if fails, eg if x y is not in screen.</returns>
		public static unsafe uint Pixel(POINT p) {
			using var dc = new ScreenDC_();
			uint R = Api.GetPixel(dc, p.x, p.y);
			if (R == 0xFFFFFFFF) return 0;
			return ColorInt.SwapRB(R) | 0xFF000000;
		}

		/// <summary>
		/// Gets color of a window pixel.
		/// </summary>
		/// <param name="p">x y in <i>w</i> client area.</param>
		/// <returns>Pixel color in 0xAARRGGBB format. Alpha 0xFF.</returns>
		/// <inheritdoc cref="Image(wnd, RECT?, CIFlags)"/>
		public static unsafe uint Pixel(wnd w, POINT p, CIFlags flags = CIFlags.WindowDC) {
			using var c = new CaptureScreenImage();
			if (!c.Capture(w, new(p.x, p.y, 1, 1), flags)) return 0;
			return c.Pixels[0];
		}

		#endregion

		#region capture image UI

		/// <summary>
		/// UI for capturing an image, color or rectangle on screen.
		/// </summary>
		/// <returns><c>false</c> if canceled.</returns>
		/// <param name="result">Receives results.</param>
		/// <param name="flags"></param>
		/// <param name="owner">A window to minimize temporarily.</param>
		/// <param name="wCapture">A window to capture immediately instead of waiting for F3 key. Used only with a "get window pixels" flag.</param>
		/// <remarks>
		/// Gets all screen pixels and shows in a full-screen topmost window, where the user can select an area.
		/// 
		/// Cannot capture windows that are always on top of normal topmost windows: 1. Start menu. 2. Topmost windows of UAC uiAccess processes (rare).
		/// </remarks>
		public static bool ImageColorRectUI(out CIUResult result, CIUFlags flags = 0, AnyWnd owner = default, wnd wCapture = default) {
			result = default;

			switch (flags & (CIUFlags.Image | CIUFlags.Color | CIUFlags.Rectangle)) {
			case 0 or CIUFlags.Image or CIUFlags.Color or CIUFlags.Rectangle: break;
			default: throw new ArgumentException();
			}

			List<wnd> amw = new();
			try {
				if (!owner.IsEmpty) {
					if (wCapture.Is0) {
						using (new inputBlocker(BIEvents.MouseClicks)) {
							var w = owner.Hwnd.Get.RootOwnerOrThis();
							if (!w.Is0) {
								w.ShowMinimized(1);
								amw.Add(w);

								//also minimize editor etc if need
								for (int i = 0; i < 7; i++) {
									wait.doEvents(10);
									w = wnd.active;
									if (!w.IsOfThisProcess || w.IsMinimized) break;
									w = w.Get.RootOwnerOrThis();
									w.ShowMinimized(1);
									amw.Add(w);
								}
							}

							wait.doEvents(300); //time for animations
						}
					} else {
						var w = owner.Hwnd;
						if (w.IsMinimized) amw.Add(w);
					}
				}

				bool windowPixels = flags.HasAny(CIUFlags.WindowDC | CIUFlags.PrintWindow);
				g1:
				RECT rs = screen.virtualScreen;
				//RECT rs = screen.primary.Rect; //for testing, to see print output in other screen
				Bitmap bs;
				wnd wTL = default;
				RECT rc = default;
				SIZE size = default;
				var avw = windowPixels ? null : wnd.getwnd.allWindows(onlyVisible: true)
					.Where(o => !(o.IsMinimized || o.IsCloaked))
					.Select(o => (w: o, r: o.ClientRectInScreen)).ToArray();

				if (windowPixels) {
					if (!wCapture.Is0) {
						wTL = wCapture;
						wCapture = default;
					} else {
						if (!_WaitForHotkey("Press F3 to select window from mouse pointer. Or Esc.")) return false;
						wTL = wnd.fromMouse(WXYFlags.NeedWindow);
					}
					rc = wTL.ClientRect;
					using var bw = Image(wTL, rc, flags.ToCIFlags_());
					bs = new Bitmap(rs.Width, rs.Height);
					using var g = Graphics.FromImage(bs);
					g.Clear(Color.Gray);
					wTL.MapClientToScreen(ref rc);
					g.DrawImage(bw, rc.left - rs.left, rc.top - rs.top);
					size = bw.Size;
				} else {
					bs = Image(rs);
				}

				var wui = new _ImageUIWindow();
				switch (wui.Show(bs, flags, rs)) {
				case 1: break;
				case 2:
					if (!windowPixels && !_WaitForHotkey("Press F3 when ready for new screenshot. Or Esc.")) return false;
					goto g1;
				default: return false;
				}

				var r = wui.Result;

				//if the window is DPI-scaled, scale r.rect
				if (windowPixels && (size.width != rc.Width || size.height != rc.Height) && Dpi.AwarenessContext.Available) { //Win10+, to match the unscaling code which can't support older OS
					int dpiw = Dpi.OfWindow(wTL), dpis = screen.of(wTL).Dpi;
					var rr = r.rect;
					r.rect = RECT.FromLTRB(
						rc.left + Math2.MulDiv(rr.left - rc.left, dpis, dpiw),
						rc.top + Math2.MulDiv(rr.top - rc.top, dpis, dpiw),
						rc.left + Math2.MulDiv(rr.right - rc.left, dpis, dpiw),
						rc.top + Math2.MulDiv(rr.bottom - rc.top, dpis, dpiw)
						);
					r.dpiScale = (double)dpis / dpiw;
				} else r.dpiScale = 1;

				r.w = _WindowFromRect(r, wTL);

				//if that window is eg a menu, it probably disappeared. We get correct screenshot, but wrong window.
				//	Workaround: try to detect it and set r.possiblyWrongWindow.
				//	Then the 'find image' tool will prompt the user to capture the window with the 'find window' tool.
				if (windowPixels) r.possiblyWrongWindow = r.w.Window != wTL;
				else if (!r.w.Is0) {
					var w1 = avw.FirstOrDefault(o => o.r.Contains(r.rect)).w;
					if (w1 != r.w.Window) r.possiblyWrongWindow = !w1.IsVisible || w1.IsCloaked || w1.IsMinimized;
				}

				result = r;
			}
			finally {
				if (amw.Count > 0) {
					for (int i = amw.Count; --i >= 0;) try { amw[i].ShowNotMinimized(); } catch { }
					amw[0].ActivateL();
				}
				Api.GetKeyState(1); //let OS update key states. Else the API later may not work eg in wndproc on some messages.
			}
			return true;

			static wnd _WindowFromRect(CIUResult r, wnd wTL) {
				//after closing our window, may need several ms until OS sets correct Z order. Until that may get different w1 and w2.
				Thread.Sleep(25);

				wnd w1, w2;
				var r1 = r.rect;
				if (!wTL.Is0 && wTL.MapScreenToClient(ref r1) && wTL.ClientRect.Contains(r1)) {
					w1 = wTL.ChildFromXY((r1.left, r1.top), WXYCFlags.OrThis);
					w2 = (r.image == null) ? w1 : wTL.ChildFromXY((r1.right - 1, r1.bottom - 1), WXYCFlags.OrThis);
				} else {
					w1 = wnd.fromXY((r.rect.left, r.rect.top));
					w2 = (r.image == null) ? w1 : wnd.fromXY((r.rect.right - 1, r.rect.bottom - 1));
				}

				if (w2 != w1 || !_IsInClientArea(w1)) {
					wnd w3 = w1.Window, w4 = w2.Window;
					w1 = (w4 == w3 && _IsInClientArea(w3)) ? w3 : default;
				}
				return w1;

				bool _IsInClientArea(wnd w) => w.GetClientRect(out var rc, true) && rc.Contains(r.rect);
			}

			static bool _WaitForHotkey(string info) {
				using (osdText.showText(info, Timeout.Infinite)) {
					//try { keys.waitForHotkey(0, KKey.F3); }
					//catch(AuException) { dialog.showError("Failed to register hotkey F3"); return false; }

					return KKey.F3 == keys.waitForKeys(0, k => !k.IsUp && k.Key is KKey.F3 or KKey.Escape, block: true);
				}
			}
		}

		class _ImageUIWindow {
			wnd _w;
			Bitmap _img;
			bool _paintedOnce;
			bool _magnMoved;
			bool _capturing;
			CIUFlags _flags;
			MouseCursor _cursor;
			SIZE _textSize;
			int _dpi;
			int _res;

			public CIUResult Result;

			/// <returns>0 Cancel, 1 OK, 2 Retry.</returns>
			public int Show(Bitmap img, CIUFlags flags, RECT r) {
				_img = img;
				_flags = flags;
				//SHOULDDO: cursor almost invisible on my 200% DPI tablet (somehow transparent). Test on true 200% DPI screen.
				_cursor = MouseCursor.Load(ResourceUtil.GetBytes("<Au>resources/red_cross_cursor.cur"), 32);
				_dpi = screen.primary.Dpi;
				_w = WndUtil.CreateWindow(_WndProc, true, WndUtil.WindowClassDWP_, "Au.CaptureScreen", WS.POPUP | WS.VISIBLE, WSE.TOOLWINDOW | WSE.TOPMOST, r.left, r.top, r.Width, r.Height);
				_w.ActivateL();

				try {
					while (Api.GetMessage(out var m) && m.message != Api.WM_APP) {
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

			unsafe void _WmPaint(IntPtr dc) {
#if true
				using var bd = _img.Data(ImageLockMode.ReadOnly);
				var bi = new Api.BITMAPINFO(bd.Width, -bd.Height);
				Api.SetDIBitsToDevice(dc, 0, 0, bd.Width, bd.Height, 0, 0, 0, bd.Height, (void*)bd.Scan0, &bi);
#else //very slow
				using var g = Graphics.FromHdc(dc);
				g.DrawImageUnscaled(_img, 0, 0);
#endif
				_paintedOnce = true;
			}

			void _WmMousemove(POINT pc) {
				if (!_paintedOnce) return;

				//format text to draw below magnifier
				string text;
				using (new StringBuilder_(out var s)) {
					var ic = _flags & (CIUFlags.Image | CIUFlags.Color | CIUFlags.Rectangle);
					if (ic == 0) ic = CIUFlags.Image | CIUFlags.Color;
					bool canColor = ic.Has(CIUFlags.Color);
					if (canColor) {
						var color = _img.GetPixel(pc.x, pc.y).ToArgb() & 0xffffff;
						s.Append("Color  #").Append(color.ToString("X6")).Append('\n');
					}
					if (ic == CIUFlags.Color) {
						s.Append("Click to capture color.\n");
					} else if (ic == CIUFlags.Rectangle) {
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
				if (Result != null) return;

				//bool isAnyShape = false; //rejected. Not useful.
				bool isColor = false;
				var ic = _flags & (CIUFlags.Image | CIUFlags.Color | CIUFlags.Rectangle);
				if (ic == CIUFlags.Color) {
					isColor = true;
				} else {
					var mod = keys.gui.getMod();
					if (mod != 0 && ic == CIUFlags.Rectangle) return;
					switch (mod) {
					case 0: break;
					case KMod.Ctrl when ic == 0: isColor = true; break;
					default: return;
					}
				}

				Result = new CIUResult();
				var r = new RECT(p0.x, p0.y, 0, 0);
				if (isColor) {
					Result.color = (uint)_img.GetPixel(p0.x, p0.y).ToArgb();
					r.right++; r.bottom++;
				} else {
					var pen = Pens.Red;
					bool notFirstMove = false;
					_capturing = true;
					try {
						if (!WndUtil.DragLoop(_w, MButtons.Left, m => {
							if (m.msg.message != Api.WM_MOUSEMOVE) return;
							POINT p = m.msg.pt; _w.MapScreenToClient(ref p);
							using var g = Graphics.FromHwnd(_w.Handle);
							if (notFirstMove) { //erase prev rect
								r.right++; r.bottom++;
								g.DrawImage(_img, r, r, GraphicsUnit.Pixel);
								//FUTURE: prevent flickering. Also don't draw under magnifier.
							} else notFirstMove = true;
							r = RECT.FromLTRB(p0.x, p0.y, p.x, p.y);
							r.Normalize(true);
							g.DrawRectangle(pen, r);
						})) { //Esc key etc
							Api.InvalidateRect(_w);
							return;
						}
					}
					finally { _capturing = false; }

					r.right++; r.bottom++;
					if (r.NoArea) {
						Api.DestroyWindow(_w);
						return;
					}

					if (ic != CIUFlags.Rectangle) {
						Result.image = _img.Clone(r, PixelFormat.Format32bppArgb);
					}

				}
				_w.MapClientToScreen(ref r);
				Result.rect = r;

				if (isColor) { //bad things may happen if this window closed while the mouse button or Ctrl pressed
					for (int i = 200; --i >= 0 && (Api.GetKeyState(1) < 0 || Api.GetKeyState(17) < 0);) wait.doEvents(15);
				}

				_res = 1;
				Api.DestroyWindow(_w);
			}
		}

		#endregion

		#region other

		/// <summary>
		/// UI for capturing a rectangle, point or/and window on screen with Shift key.
		/// </summary>
		/// <returns><c>true</c> if captured, <c>false</c> if pressed Esc.</returns>
		/// <param name="result"></param>
		/// <param name="type"></param>
		/// <param name="rectInClient">Get rectangle in window client area.</param>
		/// <param name="wxyFlags"></param>
		public static unsafe bool RectPointWindowUI(out CRUResult result, CRUType type, bool rectInClient = false, WXYFlags wxyFlags = 0) {
			result = default;
			var wxyFlags2 = wxyFlags & (WXYFlags.NeedWindow | WXYFlags.NeedControl);

			var s = type switch {
				CRUType.Window => "Press Shift to capture %",
				CRUType.Rect => "Shift+mouse move to capture rectangle on screen.",
				CRUType.Point => "Press Shift to capture mouse coordinates.",
				CRUType.WindowAndPoint => "Press Shift to capture mouse coordinates in a %.",
				CRUType.WindowAndRect => "Shift+mouse move to capture rectangle in a %.",
				_ => "Press Shift to capture %.\nOr Shift+mouse move to capture rectangle in a %."
			};
			s = s.Replace("%", wxyFlags2 switch { WXYFlags.NeedWindow => "window", WXYFlags.NeedControl => "control", _ => "window or control" });
			s += "\nOr press Esc to cancel.";
			using var osd = osdText.showText(s, -1);

			wnd w = default, wClip = default;
			bool needWindow = type is not (CRUType.Rect or CRUType.Point);
			if (needWindow) { //draw black rectangle around window or control from mouse
				using var osrw = new osdRect { };
				osrw.Show();
				for (; ; ) {
					wait.doEvents(12);
					if (keys.isPressed(KKey.Escape)) return false;
					w = wnd.fromMouse(wxyFlags);
					osrw.Rect = w.Rect;
					if (keys.isShift) break;
				}
				if (w == default) return false;
			} else {
				for (; ; ) {
					wait.doEvents(12);
					if (keys.isPressed(KKey.Escape)) return false;
					if (keys.isShift) break;
				}
			}

			var p = mouse.xy;
			RECT r = new(p.x, p.y, 0, 0);

			if (type is not (CRUType.Window or CRUType.Point or CRUType.WindowAndPoint)) { //draw red rectangle
				if (needWindow) {
					wClip = wxyFlags2 == WXYFlags.NeedControl ? w : w.Window;
					var rw = rectInClient ? wClip.ClientRectInScreen : wClip.Rect;
					Api.ClipCursor(&rw);
				}

				try {
					using var osrr = new osdRect { Color = 0xff0000, Thickness = 1, Rect = r };
					osrr.Show();
					for (var r1 = r; ;) {
						wait.doEvents(12);
						if (keys.isPressed(KKey.Escape)) return false;
						if (!keys.isShift) break;
						p = mouse.xy;
						r1.right = p.x; r1.bottom = p.y;
						r = r1; r.Normalize(true);
						osrr.Rect = r;
					}
				}
				finally { if (needWindow) Api.ClipCursor(null); }

				if (needWindow && w != wClip) { //if rect spans multiple controls, get top-level window
					var w2 = wnd.fromMouse(wxyFlags);
					if (w2 != w) w = wClip;
				}
			}

			if (needWindow && rectInClient) w.MapScreenToClient(ref r);

			result = new(w, !r.NoArea, r);
			return true;
		}

		#endregion
	}

	/// <summary>
	/// Captures image pixels from screen or window.
	/// </summary>
	/// <remarks>
	/// This class is used by <see cref="CaptureScreen"/>, <see cref="uiimage"/> and <see cref="uiimageFinder"/>. Also you can use it directly. For example it can get pixels directly without copying to a <b>Bitmap</b> or array.
	/// 
	/// How to use:
	/// 1. Create variable.
	/// 2. Call <b>Capture</b>.
	/// 3. Call other functions to get result in various formats.
	/// 4. If need, repeat 2-3 (for example when waiting for image).
	/// 5. Dispose (important).
	/// 
	/// Pixel format: <b>Format32bppArgb</b>, alpha 0xff.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var w = wnd.find("LibreAutomate").Child("document");
	/// 
	/// using var c = new CaptureScreenImage();
	/// if (!c.Capture(w, new(0, 0, 200, 200))) return;
	/// using var b = c.ToBitmap();
	/// 
	/// var f = folders.Temp + "test.png"; b.Save(f); run.it(f);
	/// ]]></code>
	/// </example>
	public unsafe sealed class CaptureScreenImage : IDisposable {
		MemoryBitmap _mb;
		uint* _pixels;
		int _width, _height;
		int _dibWidth, _dibHeight;
		bool _alphaOk;
		//_DwmThumbnail _dwm;

		/// <summary>
		/// Frees image memory.
		/// </summary>
		public void Dispose() {
			_mb?.Dispose(); _mb = null;
			_pixels = null;
			_width = _height = 0;
			//_dwm?.Dispose(); _dwm = null;
		}

		internal void SetExternalData_(uint* pixels, int width, int height) {
			Debug.Assert(_mb == null);
			_pixels = pixels;
			_width = width;
			_height = height;
		}

		/// <summary>
		/// Let <b>Capture</b> don't set alpha = 0xff. Slightly faster. Then pixelformat will be Rgb instead of Argb.
		/// Default <c>false</c>.
		/// </summary>
		internal bool DontSetAlpha_ { get; set; }

		/// <summary>
		/// Captures image from window client area into memory stored in this variable.
		/// </summary>
		/// <param name="w">Window or control.</param>
		/// <param name="r">Rectangle in <i>w</i> client area coordinates. If <c>null</c>, uses <c>w.ClientRect</c>.</param>
		/// <returns><c>false</c> if <i>r</i> empty or not in the client area and used flag <b>Relaxed</b> (else exception).</returns>
		public bool Capture(wnd w, RECT? r = null, CIFlags flags = CIFlags.WindowDC) => _Capture(w.ThrowIf0(), r, flags);

		/// <summary>
		/// Captures image from screen into memory stored in this variable.
		/// </summary>
		/// <param name="relaxed">If <i>r</i> empty, return <c>false</c> instead of exception.</param>
		/// <returns><c>false</c> if <i>r</i> empty and <i>relaxed</i> <c>true</c> (else exception).</returns>
		public bool Capture(RECT r, bool relaxed = false) => _Capture(default, r, flags: relaxed ? CIFlags.Relaxed : 0);

		bool _Capture(wnd w, RECT? rect, CIFlags flags) {
			const CIFlags c_howMask = CIFlags.WindowDC | CIFlags.PrintWindow /*| CIFlags.WindowDwm*/;
			if ((flags & c_howMask) is not (0 or CIFlags.WindowDC or CIFlags.PrintWindow /*or CIFlags.WindowDwm*/)) throw new ArgumentException();
			bool fromWindow = flags.HasAny(c_howMask), printWindow = flags.Has(CIFlags.PrintWindow);
			RECT r = rect ?? default, rc = default;
			if (!w.Is0) {
				//if (flags.Has(CIFlags.WindowDwm)) { //w must be top-level window
				//	var ww = w.Window;
				//	if (ww != w) {
				//		ww.ThrowIf0();
				//		if (rect == null) r = w.ClientRect;
				//		w.MapClientToClientOf(ww, ref r);
				//		rect = r;
				//		w = ww;
				//	}
				//}

				var rc2 = w.ClientRect;
				bool dpiScaled = fromWindow && Dpi.IsWindowVirtualizedWin10_(w);
				using var dac = dpiScaled ? new Dpi.AwarenessContext(w) : default;
				if (!w.GetClientRect(out rc)) w.ThrowUseNative();
				if (rect == null || r == rc2) {
					r = rc;
				} else {
					if (dpiScaled) { //unscale r
						dac.Dispose();
						var ww = w.Window;
						int dpiw = Dpi.OfWindow(ww), dpis = screen.of(ww).Dpi;
						r = RECT.FromLTRB(Math2.MulDiv(r.left, dpiw, dpis), Math2.MulDiv(r.top, dpiw, dpis), Math2.MulDiv(r.right, dpiw, dpis), Math2.MulDiv(r.bottom, dpiw, dpis));
					}
					if (!r.Intersect(rc)) return flags.Has(CIFlags.Relaxed) ? false : throw new ArgumentException("rectangle not in window");
				}

				//if (flags.Has(CIFlags.WindowDwm)) {
				//	_dwm ??= new();
				//	if (!_dwm.Init(w, r, dpiScaled)) return flags.Has(CIFlags.Relaxed) ? false : throw new ArgumentException("rectangle not in window");
				//	w = _dwm.WndThumbnail;
				//}
			}
			if (r.NoArea) return flags.Has(CIFlags.Relaxed) ? false : throw new ArgumentException("empty rectangle");

			int dibWidth = printWindow ? rc.Width : r.Width, dibHeight = printWindow ? rc.Height : r.Height;
			if (_mb == null || dibWidth != _dibWidth || dibHeight != _dibHeight) {
				var bi = new Api.BITMAPINFO(dibWidth, -dibHeight);
				var dib = Api.CreateDIBSection(default, bi, 0, out var pixels);
				if (dib == default) throw new AuException("*create memory bitmap of specified size");
				_pixels = pixels;
				_mb ??= new MemoryBitmap();
				_mb.Attach(dib); //and deletes old bitmap
				_dibWidth = dibWidth;
				_dibHeight = dibHeight;
			}
			_width = r.Width;
			_height = r.Height;

			if (printWindow) { //must capture entire client area
				var pw = Api.PW_CLIENTONLY;
				if (osVersion.minWin8_1) pw |= Api.PW_RENDERFULLCONTENT;
				//PW_RENDERFULLCONTENT is new in Win8.1. Undocumented in MSDN, but defined in h. Then works with windows like Chrome, winstore.
				//	Bug: from some controls randomly gets partially painted image. Eg classic toolbar, treeview.
				//	Rejected: if PrintClient|WindowDC, capture without PW_RENDERFULLCONTENT. Has no sense.

				if (!Api.PrintWindow(w, _mb.Hdc, pw)) w.ThrowNoNative("*get pixels");
				if (r.left != 0 || r.top != 0 || _width != dibWidth) { //move pixels to the start of the bitmap memory
					for (int y = r.top; y < r.bottom; y++) {
						var spanFrom = new Span<uint>(_pixels + y * dibWidth + r.left, _width);
						var spanTo = new Span<uint>(_pixels + (y - r.top) * _width, _width);
						spanFrom.CopyTo(spanTo);
					}
				}
				//} else if (flags.Has(CIFlags.WindowDwm)) {
				//	if (!Api.PrintWindow(w, _mb.Hdc, Api.PW_CLIENTONLY | Api.PW_RENDERFULLCONTENT)) w.ThrowNoNative("*get pixels");
				//	_alphaOk = true;
				//	return true;
			} else {
				if (!w.Is0 && !fromWindow) {
					w.MapClientToScreen(ref r);
					w = default;
				}
				using var dc = new WindowDC_(w);
				if (dc.Is0) w.ThrowNoNative("*get pixels");
				uint rop = !w.Is0 ? Api.SRCCOPY : Api.SRCCOPY | Api.CAPTUREBLT;
				Api.BitBlt(_mb.Hdc, 0, 0, _width, _height, dc, r.left, r.top, rop); //fails only if a HDC is invalid
			}

			if (_alphaOk = !DontSetAlpha_) {
				byte* p = (byte*)_pixels, pe = p + _width * _height * 4;
				for (p += 3; p < pe; p += 4) *p = 0xff;
			}

			return true;
		}

		/// <summary>
		/// Width of the captured image.
		/// </summary>
		public int Width => _width;

		/// <summary>
		/// Height of the captured image.
		/// </summary>
		public int Height => _height;

		/// <summary>
		/// Pixels of the captured image.
		/// </summary>
		public uint* Pixels => _pixels;

		/// <summary>
		/// Copies pixels of the captured image to new 1-D array.
		/// </summary>
		[SkipLocalsInit]
		public uint[] ToArray1D() {
			var a = GC.AllocateUninitializedArray<uint>(_height * _width);
			fixed (uint* p = a) { MemoryUtil.Copy(_pixels, p, _width * _height * 4); }
			return a;
		}

		/// <summary>
		/// Copies pixels of the captured image to new 2-D array [row, column].
		/// </summary>
		public uint[,] ToArray2D() {
			var a = new uint[_height, _width];
			fixed (uint* p = a) { MemoryUtil.Copy(_pixels, p, _width * _height * 4); }
			return a;
		}

		/// <summary>
		/// Creates new <b>Bitmap</b> from pixels of the captured image.
		/// </summary>
		public Bitmap ToBitmap() {
			var b = new Bitmap(_width, _height, _alphaOk ? PixelFormat.Format32bppArgb : PixelFormat.Format32bppRgb);
			GC_.AddObjectMemoryPressure(b, _width * _height * 4);
			using var d = b.Data(new(0, 0, _width, _height), ImageLockMode.ReadWrite);
			MemoryUtil.Copy(_pixels, (uint*)d.Scan0, _width * _height * 4);
			return b;
		}

		//rejected. Unreliable. Not all windows that require PW_RENDERFULLCONTENT have WS_EX_NOREDIRECTIONBITMAP. Eg Chrome on Win8.1.
		//static uint _GetPrintWindowFlags(CIFlags flags, wnd w, RECT r) {
		//	if (!flags.Has(CIFlags.PrintWindow)) return 0;
		//	var f = Api.PW_CLIENTONLY;
		//	if (osVersion.minWin8_1) {
		//		var wtl = w.Window;
		//		if (wtl.HasExStyle(WSE.NOREDIRECTIONBITMAP)) f |= Api.PW_RENDERFULLCONTENT;
		//		else
		//			api.EnumChildWindows(wtl, (c, _) => {
		//				if (c.HasExStyle(WSE.NOREDIRECTIONBITMAP) && (c == w || (c.IsVisible && c.GetRectIn(w, out var rr) && rr.IntersectsWith(r)))) {
		//					f |= Api.PW_RENDERFULLCONTENT;
		//					return false;
		//				}
		//				return true;
		//			}, 0);
		//	}
		//	if (flags.Has(CIFlags.WindowDC) && 0 == (f & Api.PW_RENDERFULLCONTENT)) f = 0;
		//	return f;
		//}
	}
}

namespace Au.Types {
	/// <summary>
	/// Used with <see cref="CaptureScreen"/> functions.
	/// </summary>
	[Flags]
	public enum CIFlags {
		/// <inheritdoc cref="IFFlags.WindowDC"/>
		WindowDC = 1,

		/// <inheritdoc cref="IFFlags.PrintWindow"/>
		PrintWindow = 2,

		///// <inheritdoc cref="IFFlags.WindowDwm"/>
		//WindowDwm = 4,

		//note: the above values must be the same in CIFlags, CIUFlags, IFFlags, OcrFlags.

		/// <summary>
		/// Flag: don't throw exception when the specified rectangle or point does not intersect with the window client area or when the rectangle is empty. Instead return <c>null</c> or 0.
		/// </summary>
		Relaxed = 0x100,

		//rejected. Or would need a tool to capture rect/pont in logical coord.
		///// <summary>
		///// Flag: the specified rectangle or point uses logical (non-scaled) coordinates when the window is DPI-scaled. Used only with flags <b>WindowDC</b> (default) or <b>PrintWindow</b>.
		///// </summary>
		//RectLogical = 0x200,
	}

	static partial class ExtMisc {
		internal static CIFlags ToCIFlags_(this CIUFlags t) => (CIFlags)t & (CIFlags.WindowDC | CIFlags.PrintWindow /*| CIFlags.WindowDwm*/);
		internal static CIFlags ToCIFlags_(this IFFlags t) => (CIFlags)t & (CIFlags.WindowDC | CIFlags.PrintWindow /*| CIFlags.WindowDwm*/);
		internal static CIFlags ToCIFlags_(this OcrFlags t) => (CIFlags)t & (CIFlags.WindowDC | CIFlags.PrintWindow /*| CIFlags.WindowDwm*/);
	}

	/// <summary>
	/// Flags for <see cref="CaptureScreen.ImageColorRectUI"/>.
	/// </summary>
	/// <remarks>
	/// Only one of flags <b>Image</b>, <b>Color</b> and <b>Rectangle</b> can be used. If none, can capture image or color.
	/// </remarks>
	[Flags]
	public enum CIUFlags {
		/// <inheritdoc cref="IFFlags.WindowDC"/>
		WindowDC = 1,

		/// <inheritdoc cref="IFFlags.PrintWindow"/>
		PrintWindow = 2,

		//could not make it work well with "glass" areas.
		///// <inheritdoc cref="IFFlags.WindowDwm"/>
		//WindowDwm = 4,

		//note: the above values must be the same in CIFlags, CIUFlags, IFFlags, OcrFlags.

		/// <summary>Can capture only image, not color.</summary>
		Image = 0x100,

		/// <summary>Can capture only color, not image.</summary>
		Color = 0x200,

		/// <summary>Capture only rectangle, not image/color.</summary>
		Rectangle = 0x400,
	}

#pragma warning disable 1591 //XML doc
	[Flags, Obsolete("Renamed to CIUFlags"), EditorBrowsable(EditorBrowsableState.Never)]
	public enum ICFlags {
		WindowDC = 1,
		PrintWindow = 2,
		Image = 0x100,
		Color = 0x200,
		Rectangle = 0x400,
	}

	[Obsolete("Renamed to CIUResult"), EditorBrowsable(EditorBrowsableState.Never)]
	public class ICResult : CIUResult { }
#pragma warning restore 1591 //XML doc

	/// <summary>
	/// Results of <see cref="CaptureScreen.ImageColorRectUI"/>.
	/// </summary>
	public class CIUResult {
		/// <summary>
		/// Captured image.
		/// <c>null</c> if captured single pixel color or used flag <see cref="CIUFlags.Rectangle"/>.
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

		/// <summary>
		/// If used flags to get window pixels and the window is DPI-scaled (smaller when capturing), on Windows 10 and later contains the scale factor. Else 1.
		/// </summary>
		public double dpiScale;

		/// <summary>
		/// If <c>true</c>, most likely <b>w</b> is incorrect window, because the window that was there before capturing disappeared while capturing, for example it was a popup menu.
		/// If captured from screen (without flags like <b>WindowDC</b>), <b>w</b> may be correct even if this is <c>true</c> (can't detect reliably), else certainly incorrect.
		/// </summary>
		public bool possiblyWrongWindow;
	}

	/// <summary>
	/// <see cref="CaptureScreen.RectPointWindowUI"/> UI type.
	/// </summary>
	public enum CRUType {
		/// <summary>Capture only window.</summary>
		Window,

		/// <summary>Capture only rectangle in screen.</summary>
		Rect,

		/// <summary>Capture rectangle in window.</summary>
		WindowAndRect,

		/// <summary>Capture window and optionally rectangle in it.</summary>
		WindowOrRect,

		/// <summary>Capture only point (coordinates) in screen.</summary>
		Point,

		/// <summary>Capture point in window.</summary>
		WindowAndPoint,
	}

	/// <summary>
	/// <see cref="CaptureScreen.RectPointWindowUI"/> results.
	/// </summary>
	public record struct CRUResult(wnd w, bool hasRect, RECT r);
}
