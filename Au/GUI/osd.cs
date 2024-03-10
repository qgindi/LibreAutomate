using System.Drawing;

namespace Au.Types {
	/// <summary>
	/// Transparent window that can be used for on-screen display. Derived classes on it can draw non-transparent text, rectangle, image, anything.
	/// </summary>
	public abstract class OsdWindow : IDisposable {
		wnd _w;
		
		/// <summary>Destroys the OSD window.</summary>
		protected virtual void Dispose(bool disposing) {
			if (_w.Is0) return;
			Api.ShowWindow(_w, 0);
			_TopmostWorkaroundUndo();
			var w = _w; _w = default;
			if (!Api.DestroyWindow(w)) w.Post(Api.WM_CLOSE);
		}
		
		/// <summary>Destroys the OSD window.</summary>
		public void Dispose() => Dispose(true);
		
		/// <summary>Destroys the OSD window.</summary>
		public void Close() => Dispose(true);
		
		/// <summary>OSD window handle or <c>default(wnd)</c>.</summary>
		public wnd Hwnd => _w;
		
		/// <summary>
		/// Returns <c>true</c> if the OSD window is created.
		/// </summary>
		protected bool IsHandleCreated => !_w.Is0;
		
		/// <summary>
		/// Redraws the OSD window immediately.
		/// Does nothing it is not created or not visible.
		/// </summary>
		protected void Redraw() {
			if (!Visible) return;
			Api.InvalidateRect(_w);
			Api.UpdateWindow(_w);
		}
		
		/// <summary>
		/// Sets to redraw the OSD window later.
		/// Does nothing it is not created or not visible.
		/// </summary>
		protected void Invalidate() {
			if (!Visible) return;
			Api.InvalidateRect(_w);
		}
		
		/// <summary>
		/// Gets or sets the opacity of the OSD window, from 0 to 1.
		/// If 1, completely opaque. If 0, pixels of <see cref="TransparentColor"/> are transparent, others opaque. If between 0 and 1, partially transparent.
		/// </summary>
		/// <value>Default is 1. Default in <see cref="osdRect"/> is 0.</value>
		/// <remarks>
		/// This property can be changed after creating OSD window.
		/// </remarks>
		public double Opacity {
			get => _opacity;
			set {
				var v = Math.Min(Math.Max(value, 0.0), 1.0);
				if (v == _opacity) return;
				bool was0 = _opacity == 0;
				_opacity = v;
				if (!IsHandleCreated) return;
				_SetOpacity();
				if ((v == 0) != was0) Redraw();
			}
		}
		double _opacity = 1d;
		
		void _SetOpacity() {
			if (_opacity > 0) Api.SetLayeredWindowAttributes(_w, 0, (byte)(uint)(_opacity * 255), 2);
			else Api.SetLayeredWindowAttributes(_w, (uint)TransparentColor.ToBGR(), 0, 1);
			
			//never mind: when resizing an alpha-transparent window by moving the top-left corner, the opposite corner shakes.
			//	It's a Windows problem, and I could not find a workaround.
		}
		
		/// <summary>
		/// Gets or sets transparent color, default 0xF5F4F5. Pixels of this color will be transparent, unless <see cref="Opacity"/> is not 0.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// Note: when used for transparent text, text edges are blended with this color, and it can become visible if the color is not chosen carefully.
		/// </remarks>
		public ColorInt TransparentColor { get; set; } = 0xF5F4F5;
		
		/// <summary>
		/// Gets or sets OSD window size and position in screen.
		/// </summary>
		/// <remarks>
		/// This property can be changed after creating OSD window.
		/// </remarks>
		public virtual RECT Rect {
			get => _r;
			set {
				if (value == _r) return;
				_r = value;
				if (IsHandleCreated) {
					_w.SetWindowPos(SWPFlags.NOACTIVATE, _r.left, _r.top, _r.Width, _r.Height, SpecHWND.TOPMOST);
					_TopmostWorkaroundApply();
				}
			}
		}
		RECT _r;
		
		/// <summary>
		/// Gets or sets whether the OSD window is visible.
		/// The <c>set</c> function calls <see cref="Show"/> (it creates OSD window if need) or <see cref="Hide"/> (it does not destroy the OSD window).
		/// </summary>
		public bool Visible {
			get => _w.IsVisible;
			set { if (value) Show(); else Hide(); } //note: if overridden, calls the override func
		}
		
		/// <summary>
		/// Shows the OSD window. Creates if need.
		/// </summary>
		/// <remarks>
		/// In any case, also moves the window to the top of the Z order.
		/// </remarks>
		public virtual void Show() {
			if (Visible) {
				_w.ZorderTopmost();
				_TopmostWorkaroundApply();
			} else {
				if (_w.Is0) _CreateWindow();
				_w.ShowL(true);
				_w.ZorderTopmost();
				_TopmostWorkaroundApply();
				Api.UpdateWindow(_w);
			}
		}
		
		/// <summary>
		/// Hides the OSD window. Does not destroy; use <see cref="Close"/> or <see cref="Dispose"/> for it.
		/// Does nothing if not created or not visible.
		/// </summary>
		public virtual void Hide() {
			if (!Visible) return;
			_w.ShowL(false);
		}
		
		void _CreateWindow() {
			//register window class if need. Need another class if shadow.
			string cn; byte regMask;
			if (Shadow) { cn = "Au.OSD2"; regMask = 2; } else { cn = "Au.OSD"; regMask = 1; }
			if ((s_isWinClassRegistered & regMask) == 0) {
				var ce = new RWCEtc() { style = Api.CS_HREDRAW | Api.CS_VREDRAW, mCursor = MCursor.Arrow };
				if (Shadow) ce.style |= Api.CS_DROPSHADOW;
				WndUtil.RegisterWindowClass(cn, null, ce);
				s_isWinClassRegistered |= regMask;
			}
			
			var es = WSE.TOOLWINDOW | WSE.TOPMOST | WSE.LAYERED | WSE.TRANSPARENT | WSE.NOACTIVATE;
			if (ClickToClose) es &= ~WSE.TRANSPARENT;
			_w = WndUtil.CreateWindow(WndProc, true, cn, Name, WS.POPUP, es); //note: don't set rect here: can be painting problems when resizing
			_SetOpacity();
			if (!_r.Is0) _w.SetWindowPos(SWPFlags.NOACTIVATE, _r.left, _r.top, _r.Width, _r.Height, SpecHWND.TOPMOST);
		}
		static byte s_isWinClassRegistered;
		
		/// <summary>
		/// Called when the OSD window receives a message.
		/// If your derived class overrides this function, it must call <c>base.WndProc</c> and return its return value, except when don't need default processing.
		/// </summary>
		protected virtual nint WndProc(wnd w, int message, nint wParam, nint lParam) {
			switch (message) {
			case Api.WM_NCDESTROY:
				Api.PostMessage(default, 0, 0, 0); //stop waiting for a message. Never mind: not always need it.
				_w = default;
				break;
			case Api.WM_ERASEBKGND:
				return 1;
			case Api.WM_PAINT:
				using (var bp = new BufferedPaint(w, true)) {
					var dc = bp.DC;
					using var g = Graphics.FromHdc(dc);
					if (_opacity == 0) g.Clear((Color)TransparentColor);
					OnPaint(dc, g, bp.Rect);
				}
				return default;
			case Api.WM_MOUSEACTIVATE:
				return Api.MA_NOACTIVATE;
			case Api.WM_LBUTTONUP:
			case Api.WM_RBUTTONUP:
			case Api.WM_MBUTTONUP:
				if (ClickToClose) w.Post(Api.WM_CLOSE);
				break;
			}
			
			return Api.DefWindowProc(w, message, wParam, lParam);
		}
		
		/// <summary>
		/// Called when the OSD window must be drawn or redrawn.
		/// Derived classes should override this function and draw anything. Don't need to call <c>base.OnPaint</c> of <see cref="OsdWindow"/>, it does nothing.
		/// </summary>
		/// <remarks>
		/// If <see cref="Opacity"/> is 0 (default), <i>g</i> is filled with <see cref="TransparentColor"/>. Pixels of this color will be transparent. The base class draws only non-transparent areas.
		/// </remarks>
		protected virtual void OnPaint(IntPtr dc, Graphics g, RECT r) {
		}
		
		/// <summary>
		/// If <c>true</c>, the OSD window will have shadow.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		protected bool Shadow { get; set; }
		
		/// <summary>
		/// If <c>true</c>, the OSD window receive mouse messages. Only completely transparent areas don't. The user can click to close the OSD (left, right or middle button).
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		protected bool ClickToClose { get; set; }
		
		/// <summary>
		/// OSD window name. Optional, default <c>null</c>.
		/// </summary>
		/// <remarks>
		/// This text is invisible. Can be used to find OSD window. The class name is <c>"Au.OSD"</c>; if with shadow - <c>"Au.OSD2"</c>.
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public string Name { get; set; }
		
		/// <summary>
		/// Closes all OSD windows of this process.
		/// </summary>
		/// <param name="name">If not <c>null</c>, closes only OSD windows whose <see cref="Name"/> matches this [wildcard expression](xref:wildcard_expression).</param>
		public static void closeAll([ParamString(PSFormat.Wildex)] string name = null) {
			foreach (var w in wnd.findAll(name, "**m Au.OSD||Au.OSD2", WOwner.Process(Api.GetCurrentProcessId()))) w.Post(Api.WM_CLOSE);
		}
		
		#region topmost workaround
		
		//SHOULDDO: find a better workaround for: our topmost window is below some other topmost windows (Win8+).
		//	Window examples:
		//	A. Windows Start menu, search, TaskListThumbnailWnd.
		//	B. uiAccess apps, eg on-screen keyboard, Inspect, NVDA, QM2 uiAccess.
		//	C. Probably Win8 Store apps.
		//	Known workarounds:
		//	1. Temporarily make the window non-topmost. But it fails for A and probably C. Fails always if this process is not admin.
		//	2. Temporarily set partially transparent. But it is useful only for on-screen rect. Not useful for menus and toolbars. Fails if not admin. Fails with WPF windows.
		//	3. Set window region. Too crazy. Tested, does not work with eg OSK, although works with eg Notepad.
		//	4. Run tools in uiAccess processes. Too crazy and limited.
		
		internal bool TopmostWorkaround_ { get; set; }
		
		void _TopmostWorkaroundApply() {
			if (!TopmostWorkaround_ || !osVersion.minWin10) return; //never mind: on Win8 not tested. Would be bad to make full-screen windows transparent.
			var w = wnd.fromXY(new(_r.CenterX, _r.CenterY), WXYFlags.NeedWindow);
			bool apply = false;
			lock (s_twList) {
				List<OsdWindow> aosd = null;
				foreach (var v in s_twList) if (v.w == w) { if (v.a.Contains(this)) return; aosd = v.a; break; }
				if (w.HasExStyle(WSE.TOPMOST)) {
					RECT r = w.Rect; r.Inflate(10, 10);
					if (r.Width > _r.Width && r.Height > _r.Height) {
						if (!_w.ZorderIsAbove(w)) {
							if (apply = aosd == null) s_twList.Add((w, new() { this }));
							else aosd.Add(this);
							//print.it(apply, w);
						}
					}
				}
				if (apply && s_twTimer == null) {
					s_twTimer = new(_ => {
						List<wnd> aw = null;
						lock (s_twList) {
							foreach (var (w, _) in s_twList) {
								//print.it(w.GetTransparency(out var op, out var col), op, col);
								if (!w.GetTransparency(out var op, out var col) || op == null || op.Value > 200) {
									if (w.IsAlive) (aw ??= new()).Add(w);
								}
							}
						}
						if (aw != null) foreach (var w in aw) w.SetTransparency(true, 200, noException: true);
					});
					s_twTimer.Every(1000);
				}
			}
			if (apply) w.SetTransparency(true, 200, noException: true);
		}
		
		void _TopmostWorkaroundUndo() {
			if (!TopmostWorkaround_ || !osVersion.minWin10) return;
			List<wnd> aw = null;
			lock (s_twList) {
				for (int i = s_twList.Count; --i >= 0;) {
					var (w, a) = s_twList[i];
					if (a.Remove(this) && a.Count == 0) {
						s_twList.RemoveAt(i);
						if (w.IsAlive) (aw ??= new()).Add(w);
					}
				}
				if (s_twList.Count == 0 && s_twTimer != null) {
					s_twTimer.Stop();
					s_twTimer = null;
				}
			}
			if (aw != null) foreach (var w in aw) w.SetTransparency(!true, 255, noException: true);
		}
		
		static List<(wnd w, List<OsdWindow> a)> s_twList = new();
		static timer2 s_twTimer;
		
		#endregion
	}
	
	/// <summary>
	/// Used by <see cref="osdRect.SetRects"/>.
	/// </summary>
	/// <param name="Placement">Label placement relative to rectangle: <c>'L'</c> (left), <c>'R'</c> (right), <c>'T'</c> (top), <c>'B'</c> (bottom) or <c>'I'</c> (inside). Default: <c>'L'</c>.</param>
	public record class ORLabelOptions(char Placement, ColorInt? BackgroundColor = null, ColorInt? TextColor = null) {
		///
		public static implicit operator ORLabelOptions(char placement) => new(placement);
	}
}

namespace Au {
	/// <summary>
	/// Shows a mouse-transparent rectangle on screen. Just visible outline, or filled but partially transparent.
	/// </summary>
	/// <remarks>
	/// Creates a temporary partially transparent window, and draws rectangle in it. Can draw multiple rectangles.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// using (var x = new osdRect()) {
	/// 	x.Rect = (300, 300, 100, 100);
	/// 	x.Color = System.Drawing.Color.SlateBlue;
	/// 	x.Thickness = 4;
	/// 	x.Show();
	/// 	for (int i = 0; i < 5; i++) {
	/// 		250.ms();
	/// 		x.Visible = !x.Visible;
	/// 	}
	/// }
	/// ]]></code>
	/// </example>
	public class osdRect : OsdWindow {
		///
		public osdRect() {
			Opacity = 0d;
		}
		
		/// <summary>
		/// Gets or sets rectangle color.
		/// </summary>
		/// <remarks>
		/// This property can be changed after creating OSD window.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// x.Color = 0xFF0000; //red
		/// x.Color = System.Drawing.Color.Orange;
		/// ]]></code>
		/// </example>
		public ColorInt Color {
			get => _color;
			set { if (value != _color) { _color = value; Redraw(); } }
		}
		ColorInt _color = 0; //=0 adds alpha 0xFF
		
		/// <summary>
		/// Gets or sets rectangle frame width.
		/// Used only if <see cref="OsdWindow.Opacity"/> is 0 (default).
		/// </summary>
		/// <remarks>
		/// This property can be changed after creating OSD window.
		/// </remarks>
		public int Thickness {
			get => _thickness;
			set { if (value != _thickness) { _thickness = value; Redraw(); } }
		}
		int _thickness = 3;
		
		/// <summary>
		/// Called when the OSD window must be drawn or redrawn. Draws rectangle. More info: <see cref="OsdWindow.OnPaint"/>.
		/// </summary>
		protected override void OnPaint(IntPtr dc, Graphics g, RECT ra) {
			if (_rects == null) {
				if (Opacity > 0) {
					g.Clear((Color)_color);
				} else {
					g.DrawRectangleInset((Color)_color, _thickness, ra);
				}
			} else if (_rects.Length >= 0) {
				if (Opacity > 0) g.Clear((Color)TransparentColor);
				using var tr = _hasLabels ? new GdiTextRenderer(dc, Dpi.OfWindow(Hwnd)) : null;
				for (int i = 0; i < _rects.Length; i++) {
					var r = _rects[i].r; r.Offset(-Rect.left, -Rect.top);
					g.DrawRectangleInset((Color)Color, Thickness, r);
					if (_hasLabels) {
						var s = _rects[i].s;
						var z = tr.MeasureText(s);
						int pad = z.height / 2;
						z.width += pad;
						int x = _labelOptions.Placement switch { 'L' => r.left - z.width - 1, 'R' => r.right, 'I' => r.left + Thickness + 1, _ => r.left };
						int y = _labelOptions.Placement switch { 'T' => r.top - z.height - 1, 'B' => r.bottom, 'I' => r.top + Thickness + 1, _ => r.top };
						if (x < 0) { x = 0; if (z.width >= r.left) z.width = r.left - 1; }
						RECT rr = new(x, y, z.width, z.height);
						if (_labelOptions.BackgroundColor != null) g.FillRectangle((Color)_labelOptions.BackgroundColor.Value, rr); else g.FillRectangle(Brushes.PaleGoldenrod, rr);
						g.DrawRectangle(Pens.DarkGray, rr);
						rr.Inflate(-pad / 2, 0);
						tr.DrawText(s, rr, _labelOptions.TextColor?.ToBGR() ?? 0x404040);
					}
				}
			}
		}
		
		(RECT r, string s)[] _rects;
		bool _hasLabels;
		ORLabelOptions _labelOptions;
		
		/// <summary>
		/// Sets to draw multiple rectangles.
		/// </summary>
		/// <param name="rects">Rectangles.</param>
		/// <param name="indexLabels">Draw labels. The label text is the rectangle index in <i>rects</i>.</param>
		/// <param name="labelOptions">Label options. If <b>char</b> - label placement relative to rectangle: <c>'L'</c> (left), <c>'R'</c> (right), <c>'T'</c> (top), <c>'B'</c> (bottom) or <c>'I'</c> (inside). Default: <c>'L'</c>.</param>
		/// <remarks>
		/// If this function called, will draw multiple rectangles instead of single (unless <i>rects</i> is <c>null</c>). <b>Opacity</b> should be 0 (default).
		/// </remarks>
		public void SetRects(IEnumerable<RECT> rects, bool indexLabels = false, ORLabelOptions labelOptions = null) {
			SetRects(indexLabels ? rects?.Select((r, i) => (r, i.ToS())) : rects?.Select(r => (r, (string)null)), labelOptions);
		}
		
		/// <summary>
		/// Sets to draw multiple rectangles with labels.
		/// </summary>
		/// <param name="rects">Rectangles with label text. The text should be short, single line, else may draw clipped.</param>
		/// <inheritdoc cref="SetRects(IEnumerable{RECT}, bool, ORLabelOptions)"/>
		public void SetRects(IEnumerable<(RECT r, string s)> rects, ORLabelOptions labelOptions = null) {
			if (rects == null) {
				_rects = null;
				Rect = default;
			} else {
				_rects = rects.ToArray();
				var r = RECT.FromLTRB(_rects.Min(o => o.r.left), _rects.Min(o => o.r.top), _rects.Max(o => o.r.right), _rects.Max(o => o.r.bottom));
				if (_hasLabels = _rects.Any(o => o.s != null)) {
					int ii = screen.of(r).Dpi;
					r.Inflate(ii * 2, ii / 4);
				}
				Rect = r;
				_labelOptions = labelOptions ?? new('L');
				Redraw();
			}
		}
	}
	
	/// <summary>
	/// Shows text on screen, like a tooltip or with transparent background.
	/// </summary>
	/// <remarks>
	/// Creates a temporary partially transparent window, and draws text in it.
	/// Most properties cannot be changed after creating OSD window.
	/// </remarks>
	public class osdText : OsdWindow {
		NativeFont_ _font;
		
		///
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			_font?.Dispose(); _font = null;
		}
		
		/// <summary>
		/// Coordinates.
		/// Default: <c>null</c> (screen center).
		/// </summary>
		/// <remarks>
		/// Not used if <see cref="Rect"/> is set.
		/// This property can be changed after creating OSD window.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var m = new osdText { Text = "Text" };
		/// m.XY = new(Coord.Center, Coord.Max); //bottom-center of the work area of the primary screen
		/// m.Show();
		/// ]]></code>
		/// </example>
		public PopupXY XY {
			get => _xy;
			set {
				_xy = value;
				if (value != null) _rectIsSet = false;
				if (IsHandleCreated) base.Rect = Measure();
			}
		}
		PopupXY _xy;
		
		/// <summary>
		/// Gets or sets OSD window size and position in screen.
		/// </summary>
		/// <remarks>
		/// Normally don't need to use this property. If not used, the OSD window size depends on text etc, and position on <see cref="XY"/>.
		/// This property can be changed after creating OSD window.
		/// </remarks>
		/// <seealso cref="Measure"/>
		public override RECT Rect {
			get => base.Rect;
			set { _rectIsSet = true; base.Rect = value; }
		}
		bool _rectIsSet;
		
		void _ResizeOrInvalidate() {
			if (ResizeWhenContentChanged) base.Rect = Measure(); //info: invalidates if resized, because of the CS_ styles
			Invalidate();
		}
		
		/// <summary>
		/// When changing text, resize/move the OSD window if need.
		/// Default: <c>false</c>.
		/// </summary>
		public bool ResizeWhenContentChanged { get; set; }
		
		/// <summary>
		/// Text in OSD window.
		/// </summary>
		/// <remarks>
		/// This property can be changed after creating OSD window; then the window is not moved/resized, unless <see cref="ResizeWhenContentChanged"/> is <c>true</c>.
		/// </remarks>
		public string Text {
			get => _text;
			set { if (value != _text) { _text = value; _ResizeOrInvalidate(); } }
		}
		string _text;
		
		/// <summary>
		/// Font.
		/// Default: <see cref="defaultSmallFont"/>.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public FontNSS Font { get; set; }
		
		/// <summary>
		/// Text color.
		/// Default: <see cref="defaultTextColor"/>.
		/// </summary>
		/// <remarks>
		/// This property can be changed after creating OSD window.
		/// </remarks>
		public ColorInt TextColor {
			get => _textColor;
			set { if (value != _textColor) { _textColor = value; Invalidate(); } }
		}
		ColorInt _textColor;
		
		/// <summary>
		/// Background color.
		/// Default: <see cref="defaultBackColor"/>.
		/// </summary>
		/// <remarks>
		/// This property can be changed after creating OSD window.
		/// Not used for completely transparent OSD.
		/// </remarks>
		public ColorInt BackColor {
			get => _backColor;
			set { if (value != _backColor) { _backColor = value; Invalidate(); } }
		}
		ColorInt _backColor;
		
		/// <summary>
		/// Border color.
		/// Default: <see cref="defaultBorderColor"/>.
		/// </summary>
		/// <remarks>
		/// This property can be changed after creating OSD window.
		/// No border if <see cref="OsdWindow.Opacity"/>==0 or <b>BorderColor</b>==<see cref="BackColor"/>.
		/// </remarks>
		public ColorInt BorderColor {
			get => _borderColor;
			set { if (value != _borderColor) { _borderColor = value; Invalidate(); } }
		}
		private ColorInt _borderColor;
		
		/// <summary>
		/// Background image.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public Image BackgroundImage { get; set; }
		
		//public ImageLayout BackgroundImageLayout { get; set; } //FUTURE
		
		/// <summary>
		/// When used <see cref="BackgroundImage"/>, the OSD window has the same size as the image, plus borders.
		/// Else OSD window size is calculated from sizes of text and icon. Then image is displayed scaled or clipped if need.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public bool IsOfImageSize { get; set; }
		
		/// <summary>
		/// Maximal text width.
		/// Default: 0 - no limit (depends on screen width etc).
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public int WrapWidth { get; set; }
		
		/// <summary>
		/// Gets or sets text format flags.
		/// Default: <c>TFFlags.NOPREFIX | TFFlags.WORDBREAK | TFFlags.EXPANDTABS</c>.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public TFFlags TextFormatFlags { get; set; } = TFFlags.NOPREFIX | TFFlags.WORDBREAK | TFFlags.EXPANDTABS;
		
		/// <summary>
		/// Icon or image at the left. Can be <see cref="icon"/>, <b>System.Drawing.Icon</b> or <b>System.Drawing.Image</b>. Any size.
		/// For example <b>System.Drawing.SystemIcons.Information</b> or <c>icon.stock(StockIcon.INFO)</c> or <c>ImageUtil.LoadGdipBitmapFromXaml("XAML copied from the Icons tool. You can edit, Width, Height and Fill (color).", screen.primary.Dpi)</c>.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public object Icon { get; set; }
		SIZE _iconSize;
		
		/// <summary>
		/// If <c>true</c>, the OSD window will have shadow.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// Window shadows can be disabled. See <msdn>SPI_SETDROPSHADOW</msdn>.
		/// </remarks>
		public new bool Shadow { get => base.Shadow; set => base.Shadow = value; }
		
		/// <summary>
		/// If <c>true</c>, the OSD window receive mouse messages. Only completely transparent areas don't. The user can click to close the OSD (left, right or middle button).
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public new bool ClickToClose { get => base.ClickToClose; set => base.ClickToClose = value; }
		
		/// <summary>
		/// See <see cref="OsdMode"/>.
		/// </summary>
		/// <remarks>
		/// This property cannot be changed after creating OSD window.
		/// </remarks>
		public OsdMode ShowMode { get; set; }
		
		//CONSIDER: public AnyWnd Owner { get; set; }
		
		///
		public osdText() {
			Font = defaultSmallFont;
			_textColor = defaultTextColor;
			_backColor = defaultBackColor;
			_borderColor = defaultBorderColor;
		}
		
		/// <summary>
		/// Shows the OSD window. Creates if need.
		/// By default does not wait; the window will be closed after <see cref="SecondsTimeout"/>.
		/// </summary>
		/// <remarks>
		/// Depending on <see cref="ShowMode"/>, creates the OSD window in this or new thread.
		/// If the OSD window is already created, just shows it if hidden. Many properties can be changed only before creating OSD window; call <see cref="OsdWindow.Close"/> if need.
		/// </remarks>
		public override void Show() {
			if (!Hwnd.Is0) {
				base.Show();
				return;
			}
			bool thisThread = false, wait = false;
			switch (ShowMode) {
			case OsdMode.Auto: thisThread = process.thisThreadHasMessageLoop(); break;
			case OsdMode.ThisThread: thisThread = true; break;
			case OsdMode.Wait: thisThread = wait = true; break;
			}
			if (thisThread) {
				_Show(wait);
			} else {
				//Task.Run(() => ShowWait()); //works too, but cannot use StrongThread
				run.thread(() => _Show(true), ShowMode == OsdMode.WeakThread).Name = "Au.osdText";
				Au.wait.until(30, () => IsHandleCreated);
				
				//CONSIDER: make smaller timeout when main thread ended if OsdShowMode.Auto
			}
		}
		
		void _Show(bool sync) {
			if (!_rectIsSet) base.Rect = Measure();
			
			base.Show();
			_SetCloseTimer();
			if (sync) wait.doEventsUntil(0, () => !IsHandleCreated);
		}
		
		void _SetCloseTimer() {
			int t = SecondsTimeout;
			if (t == 0) t = Math.Min(Text.Lenn(), 1000) / 10 + 3; //calc time from text length
			
			if (t > 0) Api.SetTimer(Hwnd, 1, Math.Min(t, int.MaxValue / 1000) * 1000, null);
			else Api.KillTimer(Hwnd, 1);
		}
		
		/// <summary>
		/// Close the OSD window after this time, seconds.
		/// If 0 (default), depends on text length. Can be <see cref="Timeout.Infinite"/> (-1).
		/// </summary>
		public int SecondsTimeout {
			get => _secondsTimeout;
			set {
				_secondsTimeout = value;
				if (IsHandleCreated) Hwnd.SendNotify(Api.WM_USER + 100);
			}
		}
		int _secondsTimeout;
		
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		protected override nint WndProc(wnd w, int message, nint wParam, nint lParam) {
			switch (message) {
			case Api.WM_TIMER when wParam == 1:
				Close();
				return 0;
			case Api.WM_USER + 100:
				_SetCloseTimer();
				return 0;
			}
			
			return base.WndProc(w, message, wParam, lParam);
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
		
		/// <summary>
		/// Draws OSD text etc.
		/// </summary>
		protected override void OnPaint(IntPtr dc, Graphics g, RECT r) {
			//print.it(Api.GetCurrentThreadId());
			if (Opacity != 0) {
				g.Clear((Color)BackColor); //else OsdWindow cleared with TransparentColor
				
				if (BorderColor != BackColor) { //border
					g.DrawRectangleInset((Color)BorderColor, 1, r);
					r.Inflate(-1, -1);
				}
			}
			
			if (BackgroundImage != null) {
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.DrawImageUnscaledAndClipped(BackgroundImage, r); //info: if image smaller - scales; if bigger - clips.
			}
			
			_EnsureFontAndMargin(Dpi.OfWindow(Hwnd)); //eg if Measure not called because Rect is set
			
			r.Inflate(-_margin, -_margin);
			if (Font.Italic) r.Width += _margin / 2; //avoid clipping part of last char
			
			if (_iconSize != default) {
				int x = r.left + c_iconPadding, y = r.top + c_iconPadding;
				if (Icon is Image im) {
					g.DrawImageUnscaled(im, x, y);
				} else {
					var hi = Icon switch { icon k => k.Handle, Icon k => k.Handle, _ => default };
					Api.DrawIconEx(dc, x, y, hi, 0, 0);
				}
				r.left += _iconSize.width + c_iconPadding * 2;
			}
			
			if (!Text.NE()) {
				Api.SetTextColor(dc, TextColor.ToBGR());
				Api.SetBkMode(dc, 1);
				var tff = TextFormatFlags; if (WrapWidth > 0) tff |= TFFlags.WORDBREAK;
				using var soFont = new GdiSelectObject_(dc, _font);
				RECT rt = r;
				Api.DrawText(dc, Text, ref rt, tff);
			}
		}
		
		const int c_iconPadding = 5;
		int _margin;
		
		void _EnsureFontAndMargin(int dpi) {
			if (Text.NE()) {
				_margin = 0;
			} else if (_font == null) {
				var f = Font ?? defaultSmallFont;
				_font = f.CreateFont(dpi);
				_margin = Dpi.Scale(Math.Max(f.Size / 3, 3), dpi);
			}
		}
		
		/// <summary>
		/// Calculates OSD window size and position.
		/// Can be called before showing OSD.
		/// </summary>
		public RECT Measure() {
			Size z = default;
			if (IsOfImageSize && BackgroundImage != null) {
				z = BackgroundImage.Size;
			} else {
				Size zi = default;
				if (Icon != null) {
					switch (Icon) {
					case icon k: zi = k.Size; break;
					case Icon k: zi = k.Size; break;
					case Image k: zi = k.Size; break;
					}
					_iconSize = zi;
					if (zi != default) { zi.Width += c_iconPadding * 2; zi.Height += c_iconPadding * 2; }
				}
				
				var scrn = XY?.GetScreen() ?? defaultScreen.Now;
				int dpi = scrn.Dpi;
				_margin = 0;
				if (!Text.NE()) {
					_font?.Dispose(); _font = null;
					_EnsureFontAndMargin(dpi);
					var tff = TextFormatFlags;
					int maxWidth = scrn.WorkArea.Width - zi.Width - 10;
					int ww = WrapWidth; if (ww > 0) { maxWidth = Math.Min(maxWidth, ww); tff |= TFFlags.WORDBREAK; }
					using var dc = new FontDC_(_font);
					z = dc.MeasureDT(Text, tff, maxWidth);
				}
				
				z.Width += zi.Width;
				z.Height = Math.Max(z.Height, zi.Height);
				
				z.Width += _margin * 2; z.Height += _margin * 2;
			}
			
			if (Opacity != 0 && BorderColor != BackColor) { //border
				z.Width += 2; z.Height += 2;
			}
			
			RECT r = new(0, 0, z.Width, z.Height);
			//print.it(r);
			
			if (XY != null) {
				if (XY.inRect) r.MoveInRect(XY.rect, XY.x, XY.y, ensureInRect: false);
				else r.MoveInScreen(XY.x, XY.y, XY.screen, XY.workArea, ensureInScreen: false);
				r.EnsureInScreen(workArea: XY.workArea);
			} else {
				r.MoveInScreen(Coord.Center, Coord.Center, defaultScreen, workArea: true, ensureInScreen: true);
			}
			
			return r;
		}
		
		#region public static
		
		/// <summary>
		/// Shows a tooltip-like OSD window with text and optionally icon.
		/// </summary>
		/// <param name="text">Text in OSD window.<br/>Sets <see cref="Text"/>.</param>
		/// <param name="secondsTimeout"><inheritdoc cref="SecondsTimeout" path="/summary/node()"/>Sets <see cref="SecondsTimeout"/>.</param>
		/// <param name="xy"><inheritdoc cref="XY" path="/summary/node()"/>Sets <see cref="XY"/>.</param>
		/// <param name="icon"><inheritdoc cref="Icon" path="/summary/node()"/>Sets <see cref="Icon"/>.</param>
		/// <param name="textColor"><inheritdoc cref="TextColor" path="/summary/node()"/>Sets <see cref="TextColor"/>.</param>
		/// <param name="backColor"><inheritdoc cref="BackColor" path="/summary/node()"/>Sets <see cref="BackColor"/>.</param>
		/// <param name="font"><inheritdoc cref="Font" path="/summary/node()"/>Sets <see cref="Font"/>.</param>
		/// <param name="name">OSD window name.<br/>Sets <see cref="OsdWindow.Name"/>.</param>
		/// <param name="showMode">Sets <see cref="ShowMode"/>.</param>
		/// <param name="dontShow">Don't call <see cref="Show"/>. The caller can use the return value to set some other properties and call <b>Show</b>.</param>
		/// <returns>Returns an <see cref="osdText"/> object that can be used to change properties or close the OSD window.</returns>
		/// <remarks>
		/// Also sets these properties: <see cref="ClickToClose"/>=<c>true</c>, <see cref="Shadow"/>=<c>true</c>.
		/// </remarks>
		public static osdText showText(string text,
			int secondsTimeout = 0, PopupXY xy = null,
			object icon = null, ColorInt? textColor = null, ColorInt? backColor = null, FontNSS font = null,
			string name = null, OsdMode showMode = default, bool dontShow = false) {
			var o = new osdText {
				_text = text,
				SecondsTimeout = secondsTimeout,
				_xy = xy,
				Icon = icon,
				_textColor = textColor ?? defaultTextColor,
				_backColor = backColor ?? defaultBackColor,
				Font = font ?? defaultSmallFont,
				Name = name,
				ShowMode = showMode,
				ClickToClose = true,
				Shadow = true
			};
			
			if (!dontShow) o.Show();
			return o;
		}
		
		/// <summary>
		/// Shows a big-font text with transparent background.
		/// </summary>
		/// <param name="color">Sets <see cref="TextColor"/>. Default: <see cref="defaultTransparentTextColor"/>.</param>
		/// <param name="font">Sets <see cref="Font"/>. Default: <see cref="defaultBigFont"/>.</param>
		/// <remarks>
		/// Also sets these properties: <see cref="OsdWindow.Opacity"/>=0.
		/// </remarks>
		/// <inheritdoc cref="showText(string, int, PopupXY, object, ColorInt?, ColorInt?, FontNSS, string, OsdMode, bool)"/>
		public static osdText showTransparentText(string text,
			int secondsTimeout = 0, PopupXY xy = null,
			ColorInt? color = null, FontNSS font = null,
			string name = null, OsdMode showMode = default, bool dontShow = false) {
			var o = new osdText {
				_text = text,
				SecondsTimeout = secondsTimeout,
				_xy = xy,
				_textColor = color ?? defaultTransparentTextColor,
				Font = font ?? s_bigFont,
				Name = name,
				ShowMode = showMode,
				Opacity = 0d
			};
			
			if (!dontShow) o.Show();
			return o;
		}
		
		/// <summary>
		/// Shows on-screen image.
		/// </summary>
		/// <param name="image">Sets <see cref="BackgroundImage"/>.</param>
		/// <remarks>
		/// Also sets these properties: <see cref="IsOfImageSize"/>=<c>true</c>, <see cref="OsdWindow.Opacity"/>=0, <see cref="ClickToClose"/>=<c>true</c>.
		/// </remarks>
		/// <inheritdoc cref="showText(string, int, PopupXY, object, ColorInt?, ColorInt?, FontNSS, string, OsdMode, bool)"/>
		public static osdText showImage(Image image,
			int secondsTimeout = 0, PopupXY xy = null,
			string name = null, OsdMode showMode = default, bool dontShow = false) {
			var o = new osdText {
				BackgroundImage = image,
				SecondsTimeout = secondsTimeout,
				_xy = xy,
				Name = name,
				ShowMode = showMode,
				IsOfImageSize = true,
				Opacity = 0d,
				ClickToClose = true
			};
			o._backColor = o.TransparentColor;
			
			if (!dontShow) o.Show();
			return o;
		}
		
		/// <summary>Default font for <see cref="showText"/> and <b>osdText</b>. Default: standard GUI font (usually <b>Segoe UI</b>), size 12.</summary>
		/// <exception cref="ArgumentNullException"></exception>
		public static FontNSS defaultSmallFont { get => s_smallFont; set => s_smallFont = value ?? throw new ArgumentNullException(); }
		static FontNSS s_smallFont = new(12);
		
		/// <summary>Default font for <see cref="showTransparentText"/>. Default: standard GUI font (usually <b>Segoe UI</b>), size 24.</summary>
		/// <exception cref="ArgumentNullException"></exception>
		public static FontNSS defaultBigFont { get => s_bigFont; set => s_bigFont = value ?? throw new ArgumentNullException(); }
		static FontNSS s_bigFont = new(24);
		
		/// <summary>Default text color for <see cref="showText"/> and <b>osdText</b>. Default: 0x(dark gray).</summary>
		public static ColorInt defaultTextColor { get; set; } = 0x404040;
		
		/// <summary>Default border color for <see cref="showText"/> and <b>osdText</b>. Default: 0x404040 (dark gray).</summary>
		public static ColorInt defaultBorderColor { get; set; } = 0x404040;
		
		/// <summary>Default background color for <see cref="showText"/> and <b>osdText</b>. Default: 0xFFFFF0 (light yellow).</summary>
		public static ColorInt defaultBackColor { get; set; } = 0xFFFFF0;
		
		/// <summary>Default text color for <see cref="showTransparentText"/>. Default: 0x8A2BE2 (<b>Color.BlueViolet</b>).</summary>
		public static ColorInt defaultTransparentTextColor { get; set; } = 0x8A2BE2;
		
		/// <summary>
		/// Default screen when <see cref="XY"/> is not set.
		/// The <b>screen</b> must be lazy or empty.
		/// </summary>
		/// <exception cref="ArgumentException"><b>screen</b> with <b>Handle</b>. Must be lazy (with <b>LazyFunc</b>) or empty.</exception>
		/// <example>
		/// <code><![CDATA[
		/// osdText.defaultScreen = screen.index(1, lazy: true);
		/// ]]></code>
		/// </example>
		public static screen defaultScreen {
			get => _defaultScreen;
			set => _defaultScreen = value.ThrowIfWithHandle_;
		}
		static screen _defaultScreen;
		
		#endregion
	}
}

namespace Au.Types {
	/// <summary>
	/// Whether <see cref="osdText.Show"/> waits or shows the OSD window in this or new thread.
	/// </summary>
	/// <remarks>
	/// If this thread has windows, any value can be used, but usually <b>Auto</b> (default) or <b>ThisThread</b> is the best.
	/// </remarks>
	public enum OsdMode {
		/// <summary>Depends on <see cref="process.thisThreadHasMessageLoop"/>. If it is <c>true</c>, uses <b>ThisThread</b>, else <b>StrongThread</b>. Does not wait.</summary>
		Auto,
		
		/// <summary>
		/// Show the OSD window in this thread and don't wait.
		/// This thread must must be a UI thread (with windows etc).
		/// </summary>
		ThisThread,
		
		/// <summary>Show the OSD window in new thread and don't wait. Set <see cref="Thread.IsBackground"/>=<c>true</c>, so that the OSD is closed when other threads of this app end.</summary>
		WeakThread,
		
		/// <summary>Show the OSD window in new thread and don't wait. Set <see cref="Thread.IsBackground"/>=<c>false</c>, so that the OSD is not closed when other threads of this app end.</summary>
		StrongThread,
		
		/// <summary>
		/// Show the OSD window in this thread and wait until it disappears.
		/// Waits <see cref="osdText.SecondsTimeout"/> seconds. While waiting, dispatches messages etc; see <see cref="wait.doEvents(int)"/>.
		/// </summary>
		Wait,
	}
}