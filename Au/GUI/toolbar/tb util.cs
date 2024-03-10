namespace Au;

public partial class toolbar {
#if DEBUG
	/// <summary>
	/// This is a debug-only function. Returns the caller method name. Use like <c>print.it(caller(), ...)</c>.
	/// </summary>
	static string caller([CallerMemberName] string m_ = null) => m_;
#endif

	bool _SetDpi() {
		Debug.Assert(_os != null || !OwnerWindow.Is0);
		return _SetDpi(_os != null ? _os.Screen.Dpi : screen.of(OwnerWindow).Dpi);
	}

	bool _SetDpi(int dpi) {
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

	//System.Windows.Size _Unscale(int width, int height) => _Unscale(new SIZE(width, height));

	static double _Limit(double d) {
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
		var r = new RECT(0, 0, clientSize.width, clientSize.height);
		Dpi.AdjustWindowRectEx(_dpi, ref r, _w.Style, _w.ExStyle);
		int cx = r.Width, cy = r.Height;
		var a = Anchor.WithoutFlags();
		if (/*ignoreAnchor ||*/ a == TBAnchor.All) {
			_w.ResizeL(cx, cy);
		} else {
			var rw = _w.Rect;
			int dx = cx - rw.Width, dy = cy - rw.Height;
			if (!a.HasLeft()) rw.left -= dx; else if (!a.HasRight()) rw.right += dx;
			if (!a.HasTop()) rw.top -= dy; else if (!a.HasBottom()) rw.bottom += dy;
			_w.MoveL(rw);
		}
	}

	void _Invalidate(TBItem ti = null) {
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
	/// Returns DPI-scaled border thickness in client area. Returns 0 if <i>b</i> is not <b>TBBorder.Width1</b> ... <b>TBBorder.Width4</b>.
	/// </summary>
	static int _BorderPadding(TBBorder b, int dpi) => b >= TBBorder.Width1 && b <= TBBorder.Width4 ? Dpi.Scale((int)b, dpi) : 0;

	/// <summary>
	/// Returns DPI-scaled border thickness in client area. Returns 0 if <i>b</i> is not <b>TBBorder.Width1</b> ... <b>TBBorder.Width4</b>.
	/// </summary>
	int _BorderPadding(TBBorder? b = null, bool unscaled = false) => _BorderPadding(b ?? Border, unscaled ? 96 : _dpi);

	static TBAnchor _GetInvalidAnchorFlags(TBAnchor anchor) {
		return anchor.WithoutFlags() switch {
			TBAnchor.TopLeft or TBAnchor.TopRight or TBAnchor.BottomLeft or TBAnchor.BottomRight => 0,
			TBAnchor.TopLR or TBAnchor.BottomLR => TBAnchor.OppositeEdgeX,
			TBAnchor.LeftTB or TBAnchor.RightTB => TBAnchor.OppositeEdgeY,
			_ => TBAnchor.OppositeEdgeX | TBAnchor.OppositeEdgeY,
		};
	}

	void _CreatedTrap(string error = null) {
		if (_created) throw new InvalidOperationException(error);
	}
}