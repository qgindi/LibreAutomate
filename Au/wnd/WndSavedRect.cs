//SHOULDDO: it seems makes WPF window visible too early.
//SHOULDDO: if window not resizable, restore only position, not size.

namespace Au.More;

/// <summary>
/// Helps to save and restore window rectangle and state. Ensures in screen, per-monitor-DPI-aware, etc.
/// </summary>
/// <example>
/// WPF window created with <see cref="wpfBuilder"/>.
/// <code><![CDATA[
/// const string c_rkey = @"HKEY_CURRENT_USER\Software\Au\Test", c_rvalue = @"Wpf7.Rect";
/// var b = new wpfBuilder("Window").WinSize(400).R.AddOkCancel().End();
/// 	
/// WndSavedRect.Restore(b.Window, Registry.GetValue(c_rkey, c_rvalue, null) as string, s1 => Registry.SetValue(c_rkey, c_rvalue, s1));
/// 
/// //the same
/// //b.WinSaved(Registry.GetValue(c_rkey, c_rvalue, null) as string, s1 => Registry.SetValue(c_rkey, c_rvalue, s1));
/// 
/// if (!b.ShowDialog()) return;
/// ]]></code>
/// </example>
public struct WndSavedRect {
	/// <summary>
	/// Window rectangle in normal state (not maximized/minimized), as retrieved by API <msdn>GetWindowPlacement</msdn>.
	/// </summary>
	public RECT RawRect { get => _r; set => _r = value; }
	RECT _r;

	/// <summary>
	/// <see cref="Dpi.OfWindow"/>.
	/// </summary>
	public int Dpi { get; set; }

	/// <summary>
	/// The window should be maximized.
	/// </summary>
	public bool Maximize { get; set; }

	/// <summary>
	/// <see cref="wnd.IsToolWindow"/>. If <c>false</c>, <see cref="RawRect"/> may have an offset that depends on work area.
	/// </summary>
	public bool IsToolWindow { get; set; }

	/// <summary>
	/// Converts this object to string for saving.
	/// The string is very simple, like <c>"1 2 3 4 5 6"</c>.
	/// </summary>
	public override string ToString() {
		return $"{_r.left} {_r.top} {_r.Width} {_r.Height} {Dpi} {(Maximize ? 1 : 0) | (IsToolWindow ? 2 : 0)}";
	}

	/// <summary>
	/// Creates <b>WndSavedRect</b> from string created by <see cref="ToString"/>.
	/// </summary>
	/// <returns><c>false</c> if the string is <c>null</c> or invalid.</returns>
	/// <param name="saved">String created by <see cref="ToString"/>.</param>
	/// <param name="x">Result.</param>
	public static bool FromString(string saved, out WndSavedRect x) {
		x = default;
		if (saved == null) return false;
		var a = new int[6];
		for (int i = 0, j = 0; i < a.Length; i++) if (!saved.ToInt(out a[i], j, out j)) return false;
		x._r = (a[0], a[1], a[2], a[3]);
		x.Dpi = a[4];
		var flags = a[5];
		x.Maximize = 0 != (flags & 1);
		x.IsToolWindow = 0 != (flags & 2);
		return true;
	}

	/// <summary>
	/// Gets window rectangle and state for saving. Usually called when closing the window.
	/// See also <see cref="ToString"/>.
	/// </summary>
	/// <exception cref="AuWndException">Failed to get rectangle, probably invalid window handle.</exception>
	public WndSavedRect(wnd w) {
		if (!w.GetWindowPlacement_(out var p, false)) w.ThrowUseNative();
		_r = p.rcNormalPosition;
		Dpi = More.Dpi.OfWindow(w);
		Maximize = p.showCmd == Api.SW_SHOWMAXIMIZED || (p.showCmd == Api.SW_SHOWMINIMIZED && 0 != (p.flags & Api.WPF_RESTORETOMAXIMIZED));
		IsToolWindow = w.IsToolWindow;
	}

	/// <summary>
	/// Gets window rectangle and state for saving. Usually called when closing the window.
	/// See also <see cref="ToString"/>.
	/// </summary>
	/// <exception cref="AuWndException">Failed to get rectangle, probably invalid window handle.</exception>
	public WndSavedRect(System.Windows.Window w) : this(w.Hwnd()) { }

	/// <summary>
	/// Gets window rectangle and state for saving. Usually called when closing the window.
	/// See also <see cref="ToString"/>.
	/// </summary>
	/// <exception cref="AuWndException">Failed to get rectangle, probably invalid window handle.</exception>
	public WndSavedRect(System.Windows.Forms.Form form) : this(form.Hwnd()) { }

	/// <summary>
	/// Gets real rectangle for restoring saved window rectangle.
	/// </summary>
	/// <remarks>
	/// It is recommended to call this before creating window, and create window with the returned rectangle. Also set maximized state if <see cref="Maximize"/>.
	/// If it is not possible, can be called later, for example when window is created but still invisible. However then possible various problems, for example may need to set window rectangle two times, because the window may be for example DPI-scaled when moving to another screen etc.
	/// 
	/// This function ensures the window is in screen, ensures correct size when screen DPI changed, etc.
	/// </remarks>
	public RECT NormalizeRect() {
		var r = _r;
		var scr = screen.of(r);
		int dpi = scr.Dpi;
		if (dpi != this.Dpi) {
			r.Width = Math2.MulDiv(r.Width, dpi, this.Dpi);
			r.Height = Math2.MulDiv(r.Height, dpi, this.Dpi);
			//don't change xy. Anyway we cannot cover all cases, eg changed DPI of another screen that could affect xy of the window in this screen.
		}
		if (!IsToolWindow) {
			var v = scr.Info;
			r.Offset(v.workArea.left - v.rect.left, v.workArea.top - v.rect.top);
		}
		r.EnsureInScreen(scr, workArea: !IsToolWindow); //SHOULDDO: use simple rect adjust. Or add EnsureInRect.
		return r;
	}

	/// <summary>
	/// Calls <see cref="FromString"/>. If it returns <c>true</c>, calls <see cref="NormalizeRect"/>, <see cref="ExtWpf.SetRect"/>, maximizes if need and returns <c>true</c>.
	/// Call this function before showing window.
	/// </summary>
	/// <param name="w"></param>
	/// <param name="saved">String created by <see cref="ToString"/>.</param>
	/// <param name="save">If not <c>null</c>, called when closing the window. Receives string for saving. Can save it in registry, file, anywhere.</param>
	/// <exception cref="InvalidOperationException">Window is loaded.</exception>
	public static bool Restore(System.Windows.Window w, string saved, Action<string> save = null) {
		if (w.IsLoaded) throw new InvalidOperationException("Window is loaded.");
		bool ret = FromString(saved, out var v);
		if (ret) {
			var r = v.NormalizeRect();
			if (v.Maximize) w.WindowState = System.Windows.WindowState.Maximized;
			w.SetRect(r);
		}
		if (save != null) {
			w.Closing += (_, _) => {
				if (w.IsLoaded) save(new WndSavedRect(w).ToString());
			};
		}
		return ret;
	}

	/// <summary>
	/// Calls <see cref="FromString"/>. If it returns <c>true</c>, sets <i>form</i> bounds = <see cref="NormalizeRect"/>, maximizes if need, sets <b>StartPosition</b> = <b>Manual</b>, and returns <c>true</c>.
	/// Call this function before showing window.
	/// </summary>
	/// <param name="form"></param>
	/// <param name="saved">String created by <see cref="ToString"/>.</param>
	/// <param name="save">If not <c>null</c>, called when closing the window. Receives string for saving. Can save it in registry, file, anywhere.</param>
	public static bool Restore(System.Windows.Forms.Form form, string saved, Action<string> save = null) {
		bool ret = FromString(saved, out var v);
		if (ret) {
			form.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
			form.Bounds = v.NormalizeRect();
			if (v.Maximize) form.WindowState = System.Windows.Forms.FormWindowState.Maximized;
		}
		if (save != null) {
			form.FormClosing += (_, _) => {
				if (form.IsHandleCreated) save(new WndSavedRect(form).ToString());
			};
		}
		return ret;
	}

	//probably not useful. Unfinished. Or move to wnd.
	///// <summary>
	///// Calls <see cref="FromString"/>. If it returns <c>true</c>, sets <i>w</i> rectangle = <see cref="NormalizeRect"/>, maximizes if need, and returns <c>true</c>.
	///// </summary>
	///// <param name="w"></param>
	///// <param name="saved">String created by <see cref="ToString"/>.</param>
	///// <param name="allStates">If need to maximize and the window already is maximized, set the restored window rectangle too.</param>
	//public static unsafe bool Restore(wnd w, string saved, bool allStates = false) {
	//	w.ThrowIfInvalid();
	//	bool ret = FromString(saved, out var v);
	//	if (ret) {
	//		//var p = new Api.WINDOWPLACEMENT {
	//		//	rcNormalPosition = v.NormalizeRect(),
	//		//	showCmd = v.Maximize ? Api.SW_SHOWMAXIMIZED : Api.SW_RESTORE
	//		//};
	//		//w.SetWindowPlacement_(ref p, false, "Failed to restore window position");

	//		if (!allStates && v.Maximize && w.IsMaximized) return true;
	//		if (w.IsMaximized) w.ShowNotMinMax(true);
	//		if(!w.MoveL(v.NormalizeRect())) w.ThrowUseNative("Failed to restore window position");
	//		if (v.Maximize) w.ShowMaximized(true);
	//	}
	//	return ret;
	//}
}
