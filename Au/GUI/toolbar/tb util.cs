namespace Au
{
	public partial class toolbar
	{
		bool _SetDpi() {
			int dpi = _os != null ? _os.Screen.Dpi : screen.of(OwnerWindow).Dpi;
			if (dpi == _dpi) return false;
			_dpi = dpi;
			_dpiF = _dpi / 96d;
			return true;
		}

		bool _NeedScaling(bool offsets) {
			if (_dpi == 96) return false;
			if (offsets) return DpiScaling.offsets ?? _os == null;
			return DpiScaling.size ?? _screenAHSE.IsEmpty;
		}

		int _Scale(double d, bool offsets) {
			if (_NeedScaling(offsets)) d *= _dpiF;
			return d.ToInt();
		}

		double _Unscale(int i, bool offsets) => _NeedScaling(offsets) ? i / _dpiF : i;

		SIZE _Scale(System.Windows.Size z) => _NeedScaling(false) ? Dpi.Scale(z, _dpi) : SIZE.From(z, true);

		System.Windows.Size _Unscale(SIZE z) => _NeedScaling(false) ? Dpi.Unscale(z, _dpi) : z;

		//	System.Windows.Size _Unscale(int width, int height) => _Unscale(new SIZE(width, height));

		double _Limit(double d) {
			if (double.IsNaN(d)) throw new ArgumentException();
			const int c_max = 2_000_000; //for max *1024 DPI scaling
			return Math.Clamp(d, -c_max, c_max);
		}

		/// <summary>
		/// Measures, resizes and invalidates the toolbar now if need.
		/// </summary>
		void _AutoSizeNow() {
			if (!IsOpen) return;
			_Resize(_Measure());
			Api.InvalidateRect(_w);
		}

		void _Resize(SIZE clientSize/*, bool ignoreAnchor=false*/) {
			//		print.it(_dpi);
			_w.GetWindowAndClientRectInScreen(out var rw, out var rc);
			int cx = clientSize.width + (rw.Width - rc.Width), cy = clientSize.height + (rw.Height - rc.Height);
			var a = Anchor.WithoutFlags();
			if (/*ignoreAnchor ||*/ a == TBAnchor.All) {
				_w.ResizeL(cx, cy);
			} else {
				//var old = rw;
				int dx = cx - rw.Width, dy = cy - rw.Height;
				if (!a.HasLeft()) rw.left -= dx; else if (!a.HasRight()) rw.right += dx;
				if (!a.HasTop()) rw.top -= dy; else if (!a.HasBottom()) rw.bottom += dy;
				//			print.it(dx, dy, old, rw);
				_w.MoveL(rw);
			}
		}

		void _Invalidate(ToolbarItem ti = null) {
			_ThreadTrap();
			if (!IsOpen) return;
			if (ti != null) Api.InvalidateRect(_w, ti.rect);
			else Api.InvalidateRect(_w);
		}

		void _Invalidate(int i) => _Invalidate(_a[i]);

		static WS _BorderStyle(TBBorder b) => b switch {
			TBBorder.ThreeD => WS.DLGFRAME,
			TBBorder.Thick => WS.THICKFRAME,
			TBBorder.Caption => WS.CAPTION | WS.THICKFRAME,
			TBBorder.CaptionX => WS.CAPTION | WS.THICKFRAME | WS.SYSMENU,
			_ => 0
		};

		/// <summary>
		/// Returns DPI-scaled border thickness in client area. Returns 0 if b is not TBBorder.Width1 ... TBBorder.Width4.
		/// </summary>
		static int _BorderPadding(TBBorder b, int dpi) => b >= TBBorder.Width1 && b <= TBBorder.Width4 ? Dpi.Scale((int)b, dpi) : 0;

		/// <summary>
		/// Returns DPI-scaled border thickness in client area. Returns 0 if b is not TBBorder.Width1 ... TBBorder.Width4.
		/// </summary>
		int _BorderPadding(TBBorder? b = null) => _BorderPadding(b ?? Border, _dpi);

		static TBAnchor _GetInvalidAnchorFlags(TBAnchor anchor) {
			switch (anchor.WithoutFlags()) {
			case TBAnchor.TopLeft: case TBAnchor.TopRight: case TBAnchor.BottomLeft: case TBAnchor.BottomRight: return 0;
			case TBAnchor.TopLR: case TBAnchor.BottomLR: return TBAnchor.OppositeEdgeX;
			case TBAnchor.LeftTB: case TBAnchor.RightTB: return TBAnchor.OppositeEdgeY;
			}
			return TBAnchor.OppositeEdgeX | TBAnchor.OppositeEdgeY;
		}

		void _CreatedTrap(string error = null) {
			if (_created) throw new InvalidOperationException(error);
		}
	}
}