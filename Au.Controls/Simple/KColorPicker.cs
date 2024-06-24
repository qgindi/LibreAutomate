using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Au.Controls;

/// <summary>
/// WPF control for selecting color.
/// </summary>
public class KColorPicker : UserControl {
	TextBox _tColor;
	_Palette _pal;
	bool _hlsChanging, _palSelecting;
	
	public KColorPicker() {
		StackPanel p1 = new();
		StackPanel p2 = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
		p1.Children.Add(p2);
		StackPanel p3 = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 4, 0), Background = Brushes.White };
		p2.Children.Add(p3);
		
		TextBox tH = null, tL = null, tS = null;
		
		var fColor = new TextBlock { Text = "Color", ToolTip = "Color RRGGBB", Foreground = Brushes.Black };
		p3.Children.Add(fColor);
		
		var bColor = new Rectangle { Width = 9, Height = 9, Fill = Brushes.Black, Margin = new Thickness(4, 2, 4, 0) };
		p3.Children.Add(bColor);
		
		_tColor = new() { Width = 64, Text = "000000", ToolTip = fColor.ToolTip };
		_tColor.TextChanged += (o, e) => {
			int col = _GetColor(bgr: true);
			if (!_hlsChanging) {
				var (H, L, S) = ColorInt.ToHLS(col, bgr: true);
				_hlsChanging = true;
				if (S != 0) tH.Text = H.ToString();
				tL.Text = L.ToString();
				tS.Text = S.ToString();
				_hlsChanging = false;
			}
			
			if (!_palSelecting) _pal.SelectColor(col);
			
			var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)col, (byte)(col >> 8), (byte)(col >> 16)));
			fColor.Foreground = brush;
			bColor.Fill = brush;
			p3.Background = ColorInt.GetPerceivedBrightness(col, bgr: true) <= .8 ? Brushes.White : Brushes.DimGray;
			
			if (ColorChanged != null) {
				if (!BGR) col = ColorInt.SwapRB(col);
				ColorChanged(col);
			}
		};
		p2.Children.Add(_tColor);
		
		//hls: 0 H, 1 L, 2 S.
		TextBox _AddHLS(int hls, string label, string tooltip) {
			p2.Children.Add(new TextBlock { Text = label, Margin = new Thickness(hls == 0 ? 18 : 12, 0, 4, 0), ToolTip = tooltip });
			TextBox t = new() { Width = 34, Text = "0", ToolTip = tooltip };
			t.TextChanged += (o, e) => {
				if (_hlsChanging) return;
				var tb = o as TextBox;
				int i = tb.Text.ToInt();
				if (i < 0 || i > 240) {
					_hlsChanging = true;
					tb.Text = Math.Clamp(i, 0, 240).ToS();
					_hlsChanging = false;
				}
				
				int H = tH.Text.ToInt(), L = tL.Text.ToInt(), S = tS.Text.ToInt();
				int col = ColorInt.FromHLS(H, L, S, bgr: true);
				_hlsChanging = true;
				_SetColor(col, bgr: true);
				_hlsChanging = false;
			};
			t.MouseWheel += (o, e) => {
				int d = e.Delta / 15; //+-8
				if (o == tH) d /= 2; //hue is more sensitive; 8 would jump directly from box to box
				int i = t.Text.ToInt() + d;
				if (hls == 0) { if (i < 0) i += 240; else if (i > 240) i -= 240; }
				int v = Math.Clamp(i, 0, 240);
				t.Text = v.ToS();
			};
			p2.Children.Add(t);
			return t;
		}
		
		tH = _AddHLS(0, "H", "Hue 0-240. Change with mouse wheel.");
		tL = _AddHLS(1, "L", "Luminance 0-240. Change with mouse wheel.");
		tS = _AddHLS(2, "S", "Saturation 0-240. Change with mouse wheel.");
		
		_pal = new _Palette(this);
		p1.Children.Add(_pal);
		
		base.Content = p1;
	}
	
	class _Palette : HwndHost {
		KColorPicker _cp;
		wnd _w;
		int _dpi;
		int _cellSize;
		const int c_nHue = 30; //number of hue columns. Must be 240-divisible.
		const int c_nLum = 7; //number of luminance rows. Must be 240-divisible minus 1.
		
		public wnd Hwnd => _w;
		
		public _Palette(KColorPicker cp) {
			_cp = cp;
		}
		
		protected override HandleRef BuildWindowCore(HandleRef hwndParent) {
			var wParent = (wnd)hwndParent.Handle;
			_w = WndUtil.CreateWindow(_wndProc = _WndProc, false, "Static", null, WS.CHILD | WS.CLIPCHILDREN, 0, 0, 0, 10, 10, wParent);
			
			return new HandleRef(this, _w.Handle);
		}
		
		protected override void DestroyWindowCore(HandleRef hwnd) {
			Api.DestroyWindow(_w);
		}
		
		protected override Size MeasureOverride(Size constraint) => _Measure();
		
		WNDPROC _wndProc;
		nint _WndProc(wnd w, int msg, nint wParam, nint lParam) {
			//var pmo = new PrintMsgOptions(Api.WM_NCHITTEST, Api.WM_SETCURSOR, Api.WM_MOUSEMOVE, Api.WM_NCMOUSEMOVE, 0x10c1);
			//if (WndUtil.PrintMsg(out string s, _w, msg, wParam, lParam, pmo)) print.it("<><c green>" + s + "<>");
			
			switch (msg) {
			case Api.WM_NCDESTROY:
				_w = default;
				break;
			//case Api.WM_NCHITTEST: //never mind: if in Popup, probably click closes. Currently not using in popups.
			//	return Api.HTTRANSPARENT;
			case Api.WM_LBUTTONDOWN:
				_WmLbuttondown(lParam);
				break;
			case Api.WM_PAINT:
				using (var bp = new BufferedPaint(w, true)) _Paint(bp.DC);
				return default;
			}
			
			return Api.DefWindowProc(w, msg, wParam, lParam);
		}
		
		Size _Measure() {
			_dpi = Dpi.OfWindow(_w);
			int i = _cellSize = Dpi.Scale(10, _dpi);
			var z = new SIZE(i, i);
			z.width *= c_nHue; z.width++;
			z.height *= c_nLum + 1; z.height++; z.height += _cellSize / 4;
			return Dpi.Unscale(z, _dpi);
		}
		
		void _Paint(nint dc) {
			using var g = System.Drawing.Graphics.FromHdc(dc);
			using var penSel = new System.Drawing.Pen(System.Drawing.Color.Black) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
			g.Clear(System.Drawing.Color.White);
			
			var z = _cellSize;
			
			//colors. Hue in x axis, luminance in y axis.
			int y = 0;
			for (int i = 0, lum = 240; (lum -= 240 / (c_nLum + 1)) > 0; i++) {
				for (int j = 0, hue = 0; hue < 240; j++, hue += 240 / c_nHue) {
					_Draw(s_ac[j, i], j * z);
				}
				y += z;
			}
			
			//black...white
			y += _cellSize / 4;
			for (int j = 0, gray = 0; j < c_nHue; j++, gray += gray < 120 ? 10 : 8) {
				_Draw(s_ac[j, c_nLum], j * z);
			}
			
			//selection
			y = _select.lum * z; if (_select.lum == c_nLum) y += _cellSize / 4;
			g.DrawRectangle(penSel, _select.hue * z, y, z, z);
			
			void _Draw(int col, int x) {
				RECT r = (x + 1, y + 1, z - 1, z - 1);
				var brush = Api.CreateSolidBrush(col);
				Api.FillRect(dc, r, brush);
				Api.DeleteObject(brush);
			}
		}
		
		static int[,] _GenerateColors() {
			int[,] a = new int[c_nHue, c_nLum + 1];
			for (int i = 0, lum = 240; (lum -= 240 / (c_nLum + 1)) > 0; i++) {
				for (int j = 0, hue = 0; hue < 240; j++, hue += 240 / c_nHue) {
					//var col=_ColorHLSToRGB(hue, lum, 240);
					
					int lum2 = lum;
					if (lum >= 120) {
						int d = Math.Abs(80 - hue); //diff from green
						if (d <= 64) {
							d /= 4;
							//if(lum==120) print.it(Math.Max(d, 10), (240-lum)/Math.Max(d, 10));
							lum2 -= (240 - lum) / Math.Max(d, 8);
						}
					}
					var col = ColorInt.FromHLS(hue, lum2, 240, bgr: true);
					
					a[j, i] = col;
					
					//if(lum==120 /*&& hue<=180*/) {
					//	ColorInt k=ColorInt.FromBGR(col, true);
					//	var b=k.GetPerceivedBrightness();
					//	print.it(col.ToString("X6"), hue, lum, b);
					//}
				}
			}
			
			//black...white
			for (int j = 0, gray = 0; j < c_nHue; j++, gray += gray < 120 ? 10 : 8) {
				//print.it(gray);
				if (gray > 255) gray = 255;
				var col = (gray << 16) | (gray << 8) | gray;
				a[j, c_nLum] = col;
			}
			
			return a;
		}
		
		static readonly int[,] s_ac = _GenerateColors();
		
		(int hue, int lum) _select = (0, c_nLum);
		
		//public int SelectedColor => _ac[_select.hue, _select.lum];
		
		//public bool SelectedGray => _select.lum==c_nLum;
		
		public void SelectColor(int col) {
			//print.it((uint)col);
			var (H, L, S) = ColorInt.ToHLS(col, bgr: true);
			if (S == 0) { //gray
				int lum = L * 255 / 240;
				for (int j = 0; j < c_nHue; j++) {
					int gray = s_ac[j, c_nLum] & 255;
					if (gray >= lum) { _select = (j, c_nLum); break; }
				}
			} else {
				const int nLum = c_nLum + 1, hueStep = 240 / c_nHue, lumStep = 240 / nLum;
				double dif = 1000000;
				for (int hue = 0; hue < 240; hue += hueStep) {
					for (int lum = 240; (lum -= lumStep) > 0;) {
						int dH = hue - H, dL = lum - L;
						double d = Math.Sqrt(dH * dH + dL * dL);
						if (d < dif) { dif = d; _select = (hue / hueStep, c_nLum - lum / lumStep); }
					}
				}
			}
			Invalidate();
		}
		
		void _WmLbuttondown(nint xy) {
			var (x, y) = Math2.NintToPOINT(xy);
			x--; x /= _cellSize;
			y--; y /= _cellSize;
			x = Math.Clamp(x, 0, c_nHue - 1);
			y = Math.Clamp(y, 0, c_nLum);
			_select = (x, y);
			_cp._palSelecting = true;
			int col = s_ac[x, y];
			_cp._SetColor(col, bgr: true);
			_cp._palSelecting = false;
			Invalidate();
		}
		
		public unsafe void Invalidate() {
			var w = Hwnd;
			if (w.IsVisible) Api.InvalidateRect(w, null, false);
		}
	}
	
	void _SetColor(int color, bool bgr) {
		if (bgr) color = ColorInt.SwapRB(color);
		var s = color.ToS("X6");
		_tColor.Text = s;
	}
	
	int _GetColor(bool bgr) {
		var s = _tColor.Text;
		int col = s.ToInt(s.Starts('#') ? 1 : (s.Starts("0x") ? 2 : 0), STIFlags.IsHexWithout0x);
		if (bgr) col = ColorInt.SwapRB(col);
		return col;
	}
	
	/// <summary>
	/// With public functions and events use color format 0xBBGGRR. If false (default), uses 0xRRGGBB.
	/// </summary>
	public bool BGR { get; set; }
	
	public int Color {
		get => _GetColor(BGR);
		set => _SetColor(value, BGR);
	}
	
	public event Action<int> ColorChanged;
	
	public static void ColorTool(Action<string> result, Window owner, bool modal, bool add0xRgbButton, bool addBgrButton) {
		var w = new KDialogWindow { Title = "Color", ShowInTaskbar = owner == null, Owner = owner };
		var b = new wpfBuilder(w);
		w.ResizeMode = ResizeMode.NoResize;
		b.R.Add(out KColorPicker palette);
		b.R.StartGrid().Columns(-1, -1, -1);
		b.R.AddButton("#RRGGBB", _ => _Result($"#{palette.Color:X6}")).Span(1);
		if (add0xRgbButton) b.AddButton("0xRRGGBB", _ => _Result($"0x{palette.Color:X6}")).Span(1);
		if (addBgrButton) b.AddButton("0xBBGGRR", _ => _Result($"0x{ColorInt.SwapRB(palette.Color):X6}"));
		b.End();
		b.R.Add("Color name", out _ComboBox cbNamed).Brush(Brushes.White);
		b.End();
		
		List<(string name, ColorInt c, int H)> aNamed = new();
		//cbNamed.MaxDropDownHeight = 1000;
		cbNamed.DropDownOpened += _cbNet_DropDownOpened;
		void _cbNet_DropDownOpened(object sender, EventArgs e) {
			cbNamed.DropDownOpened -= _cbNet_DropDownOpened;
			
			foreach (var mi in typeof(Colors).GetProperties()) {
				if (mi.GetValue(0) is Color co) {
					var name = mi.Name;
					if (name == "Transparent") continue;
					var ci = (ColorInt)co;
					var (H, L, S) = ColorInt.ToHLS(ci.argb, false);
					if (S < 50) H = L - 1000;
					else H = (H + 240 - 43) % 240;
					aNamed.Add((name, ci, H));
				}
			}
			aNamed.Sort((x, y) => x.H - y.H);
			var darkBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 64, 64));
			for (int i = 0; i < aNamed.Count; i++) {
				var name = aNamed[i].name;
				var ci = aNamed[i].c;
				var brush = new SolidColorBrush((Color)ci);
				var k = new StackPanel { Orientation = Orientation.Horizontal };
				k.Children.Add(new Rectangle { Fill = brush, Stroke = Brushes.White, Width = 16, StrokeThickness = 3 });
				k.Children.Add(new TextBlock { Text = "Text", Foreground = brush, Background = Brushes.White, Padding = new(0, 0, 3, 0) });
				k.Children.Add(new TextBlock { Text = "Text", Foreground = brush, Background = darkBrush, Padding = new(3, 0, 3, 0) });
				k.Children.Add(new TextBlock { Text = name, Padding = new(3, 0, 3, 0) });
				cbNamed.Items.Add(k);
			}
			cbNamed.SelectionChanged += (_, e) => { if (cbNamed.SelectedIndex is int i && i >= 0) _Result(aNamed[i].name); };
		}
		
		if (modal) w.ShowDialog(); else w.Show();
		
		void _Result(string color) {
			result(color);
			w.Close();
		}
	}
	
	class _ComboBox : ComboBox {
		public _ComboBox() {
			//workaround for the ComboBox slowness
			ItemsPanel = new ItemsPanelTemplate();
			var stackPanelTemplate = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
			ItemsPanel.VisualTree = stackPanelTemplate;
		}
		
		//public override void OnApplyTemplate() {
		//	base.OnApplyTemplate();
		//	if (Template.FindName("PART_Popup", this) is Popup p) p.PopupAnimation = PopupAnimation.None;
		//}
	}
}
