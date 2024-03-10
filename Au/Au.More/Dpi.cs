namespace Au.More {
	/// <summary>
	/// Functions for high-DPI screen support.
	/// </summary>
	/// <remarks>
	/// To find DPI % on Windows 10 and 11: <b>Settings > System > Display > Scale and layout</b>. If not 100%, it means high DPI.
	/// 
	/// This program must be per-monitor-DPI-aware. Else results are undefined.
	/// </remarks>
	public static class Dpi {
		/// <summary>
		/// Gets DPI of the primary screen at the time this process started.
		/// </summary>
		public static int System {
			get {
				if (_systemDPI == 0) {
					using var dcs = new ScreenDC_();
					_systemDPI = Api.GetDeviceCaps(dcs, 90); //LOGPIXELSY

					//could use GetDpiForSystem instead (Windows 10 1607).
					//	Normally the same result (tested), but probably not if thread awareness context is Unaware (not tested).
				}
				return _systemDPI;
			}
		}
		static int _systemDPI;

		/// <summary>
		/// Gets the DPI of a window, as used in the window's process. It never changes for that window instance.
		/// </summary>
		/// <returns>If failed, returns the system DPI (<see cref="System"/>).</returns>
		/// <param name="w">A top-level window or control. Can belong to any process.</param>
		/// <param name="supportWin81">
		/// If <c>true</c>, works on Windows 8.1 and later; however on Windows 8.1 slower and less reliable.
		/// If <c>false</c> (default), works on Windows 10 1607 and later.
		/// </param>
		/// <remarks>
		/// The result depends on the DPI awareness of the window:
		/// - per-monitor-DPI-aware - usually DPI of the windows's screen.
		/// - system aware - system DPI (DPI of the primary screen).
		/// - unaware - 96.
		/// 
		/// The result also depends on the Windows version:
		/// - Works best on Windows 10 1607 and later. Uses API <msdn>GetDpiForWindow</msdn>.
		/// - On Windows 8.1 works if <i>supportWin81</i> <c>true</c>. If <c>false</c> (default), returns <see cref="System"/>.
		/// - On Windows 7 and 8.0 always returns <see cref="System"/>, because there are no Windows API. Most apps are system-DPI-aware and the result is correct; for unaware apps the result is incorrect. These Windows versions don't support per-monitor DPI.
		/// </remarks>
		public static int OfWindow(wnd w, bool supportWin81 = false) {
			if (!osVersion.minWin8_1) return System;
			int R = 0;
			if (!w.Is0) {
				if (osVersion.minWin10_1607) R = Api.GetDpiForWindow(w);
				else if (supportWin81) {
					var v = WindowDpiAwareness(w); //info: quickly returns Awareness.PerMonitor if w.IsOfThisProcess
					if (v == Awareness.Unaware) R = 96;
					else if (v == Awareness.PerMonitor)
						if (0 != Api.GetDpiForMonitor(Api.MonitorFromWindow(w.Window, SODefault.Nearest), 0, out R, out _)) R = 0;
				}
			}
			return R != 0 ? R : System;
		}

		/// <summary>
		/// Returns <c>OfWindow(w.Hwnd())</c>.
		/// </summary>
		/// <param name="w">A Winforms window or control.</param>
		public static int OfWindow(System.Windows.Forms.Control w) => OfWindow(w.Hwnd());
		//rejected: supportWin81

		/// <summary>
		/// Returns <c>OfWindow(w.Hwnd())</c>.
		/// </summary>
		/// <param name="w">A WPF window or element.</param>
		public static int OfWindow(System.Windows.DependencyObject w) => OfWindow(w.Hwnd());

		/// <summary>
		/// Gets DPI of a screen.
		/// </summary>
		/// <returns><see cref="System"/> if fails or if not supported on this Windows version.</returns>
		/// <param name="hMonitor">Native screen handle (<b>HMONITOR</b>).</param>
		/// <param name="supportWin81">Support Windows 8.1 and later. If <c>false</c> (default), supports Windows 10 1607 and later.</param>
		/// <remarks>
		/// Uses API <msdn>GetDpiForMonitor</msdn>.
		/// </remarks>
		/// <seealso cref="screen.Dpi"/>
		public static int OfScreen(IntPtr hMonitor, bool supportWin81 = false) {
			bool os = supportWin81 ? osVersion.minWin8_1 : osVersion.minWin10_1607;
			return os && 0 == Api.GetDpiForMonitor(hMonitor, 0, out int R, out _) ? R : System;
		}

		/////
		//public static int OfScreen(screen s, bool supportWin81 = false) => OfScreen(s.Now, supportWin81);

		/// <summary>
		/// Scales <b>int</b> if the specified DPI isn't 96 (100%).
		/// </summary>
		public static int Scale(int i, DpiOf dpiOf) => Math2.MulDiv(i, dpiOf, 96);

		//no. Eg also would be used for uint, long... Or name eg ScaleD. Or add double extension method.
		///// <summary>
		///// Scales <b>int</b> if the specified DPI isn't 96 (100%).
		///// </summary>
		//public static int Scale(double i, DpiOf dpiOf) => (i*(int)dpiOf/96).ToInt();

		/// <summary>
		/// Unscales <b>int</b> if the specified DPI isn't 96 (100%).
		/// </summary>
		public static double Unscale(int i, DpiOf dpiOf) => i * (96d / dpiOf);
		//Unscaling sometimes useful with WPF. Unscale to double, not int, else result often incorrect.

		/// <summary>
		/// Scales <b>SIZE</b> if the specified DPI isn't 96 (100%).
		/// </summary>
		public static SIZE Scale(SIZE z, DpiOf dpiOf) {
			int dpi = dpiOf;
			z.width = Math2.MulDiv(z.width, dpi, 96);
			z.height = Math2.MulDiv(z.height, dpi, 96);
			return z;
		}

		/// <summary>
		/// Scales <b>System.Windows.Size</b> if the specified DPI isn't 96 (100%).
		/// </summary>
		public static SIZE Scale(System.Windows.Size z, DpiOf dpiOf) {
			double f = (int)dpiOf / 96d;
			z.Width *= f;
			z.Height *= f;
			return new(z.Width.ToInt(), z.Height.ToInt());
		}

		/// <summary>
		/// Unscales <b>SIZE</b> if the specified DPI isn't 96 (100%).
		/// </summary>
		public static System.Windows.Size Unscale(SIZE z, DpiOf dpiOf) {
			double f = 96d / dpiOf;
			return new(z.width * f, z.height * f);
		}

		/// <summary>
		/// Scales <b>RECT</b> if the specified DPI isn't 96 (100%).
		/// </summary>
		public static RECT Scale(RECT r, DpiOf dpiOf) {
			int dpi = dpiOf;
			r.left = Math2.MulDiv(r.left, dpi, 96);
			r.top = Math2.MulDiv(r.top, dpi, 96);
			r.right = Math2.MulDiv(r.right, dpi, 96);
			r.bottom = Math2.MulDiv(r.bottom, dpi, 96);
			return r;
		}

		/// <summary>
		/// Unscales <b>RECT</b> if the specified DPI isn't 96 (100%).
		/// </summary>
		public static System.Windows.Rect Unscale(RECT r, DpiOf dpiOf) {
			double f = 96d / dpiOf;
			return new(r.left * f, r.top * f, r.Width * f, r.Height * f);
		}

		/// <summary>
		/// Calls API <msdn>GetSystemMetricsForDpi</msdn> if available, else <msdn>GetSystemMetrics</msdn>.
		/// </summary>
		public static int GetSystemMetrics(int nIndex, DpiOf dpiOf)
			=> osVersion.minWin10_1607
			? Api.GetSystemMetricsForDpi(nIndex, dpiOf)
			: Api.GetSystemMetrics(nIndex);

		/// <summary>
		/// Calls API <msdn>SystemParametersInfoForDpi</msdn> if available, else <msdn>SystemParametersInfo</msdn>.
		/// Use only with <i>uiAction</i> = <b>SPI_GETICONTITLELOGFONT</b>, <b>SPI_GETICONMETRICS</b>, <b>SPI_GETNONCLIENTMETRICS</b>.
		/// </summary>
		public static unsafe bool SystemParametersInfo(uint uiAction, int uiParam, void* pvParam, DpiOf dpiOf)
			=> osVersion.minWin10_1607
			? Api.SystemParametersInfoForDpi(uiAction, uiParam, pvParam, 0, dpiOf)
			: Api.SystemParametersInfo(uiAction, uiParam, pvParam);

		/// <summary>
		/// Calls API <msdn>AdjustWindowRectExForDpi</msdn> if available, else <msdn>AdjustWindowRectEx</msdn>.
		/// </summary>
		/// <remarks>
		/// Also adds scrollbar width or/and height if need.
		/// </remarks>
		public static bool AdjustWindowRectEx(DpiOf dpiOf, ref RECT r, WS style, WSE exStyle, bool hasMenu = false) {
			int dpi = dpiOf;
			bool ok = osVersion.minWin10_1607
				? Api.AdjustWindowRectExForDpi(ref r, style, hasMenu, exStyle, dpi)
				: Api.AdjustWindowRectEx(ref r, style, hasMenu, exStyle);
			if (ok) {
				if (style.Has(WS.VSCROLL)) r.Width += ScrollbarV_(dpi);
				if (style.Has(WS.HSCROLL)) r.Width += ScrollbarH_(dpi);
			}
			return ok;
		}

		internal static int ScrollbarV_(int dpi) => GetSystemMetrics(Api.SM_CXVSCROLL, dpi);
		internal static int ScrollbarH_(int dpi) => GetSystemMetrics(Api.SM_CYHSCROLL, dpi);

		/// <summary>
		/// DPI awareness of a window or process.
		/// </summary>
		public enum Awareness { //== DPI_AWARENESS == PROCESS_DPI_AWARENESS
			///
			Invalid = -1,
			///
			Unaware,
			///
			System,
			///
			PerMonitor
		}

		/// <summary>
		/// Gets DPI awareness of a window.
		/// </summary>
		/// <returns><b>Awareness.Invalid</b> if failed.</returns>
		/// <param name="w">A top-level window or control. Can belong to any process.</param>
		/// <remarks>
		/// Works best on Windows 10 1607 and later; uses API <msdn>GetWindowDpiAwarenessContext</msdn>.
		/// On Windows 8.1 returns <b>Awareness.PerMonitor</b> if <i>w</i> is of this process; else uses API <msdn>GetProcessDpiAwareness</msdn>, which is slower and less reliable.
		/// On Windows 7 and 8.0 always returns <b>System</b>, because there are no Windows API.
		/// </remarks>
		public static Awareness WindowDpiAwareness(wnd w) {
			if (osVersion.minWin10_1607) {
				return Api.GetAwarenessFromDpiAwarenessContext(Api.GetWindowDpiAwarenessContext(w));
			} else if (osVersion.minWin8_1) {
				if (w.IsOfThisProcess) return Awareness.PerMonitor;
				using var hp = Handle_.OpenProcess(w);
				return (!hp.Is0 && 0 == Api.GetProcessDpiAwareness(hp, out var a)) ? a : Awareness.Invalid;
			} else {
				return Awareness.System;
				//could use, IsWindowVirtualized (except if of this process), but slow and unreliable.
			}
		}

		/// <summary>
		/// Detects whether the window is DPI-scaled/virtualized.
		/// </summary>
		/// <returns><c>false</c> if not DPI-scaled/virtualized or if fails to detect or if invalid window handle.</returns>
		/// <param name="w">A top-level window or control. Can belong to any process.</param>
		/// <remarks>
		/// Such windows are a little blurry (entire client area).
		/// 
		/// OS scales a window when it is on a high-DPI screen, depending on the DPI awareness of the window:
		/// - Unaware - always.
		/// - System - if the screen DPI is not equal to the system DPI of that process (which usually is of the primary screen, but not always).
		/// 
		/// Such windows have various problems for automation apps:
		/// - Often difficult or impossible to get correct rectangles of UI elements (and therefore cannot click etc) or get correct UI element from point. It depends on used API (UIA, MSAA, JAB), inproc/notinproc, OS version and application.
		/// - On Windows 7 and 8.0 cannot easily get correct rectangles of such windows and their controls. This library ignores it, because would be too much work to apply workarounds in so many places and just for legacy OS versions (it has been fixed in Windows 8.1).
		/// - If with <see cref="uiimage"/> want to use window pixels, need to capture image from window pixels, not from screen pixels.
		/// 
		/// This function is not completely reliable. And not very fast. This process must be per-monitor-DPI-aware.
		/// </remarks>
		public static bool IsWindowVirtualized(wnd w) {
			if (GetScalingInfo_(w, out bool scaled, out _, out _)) return scaled;
			return IsWindowVirtualizedLegacy_(w);

			//note: child windows can have different DPI awareness (minWin10_1607). See GetWindowDpiHostingBehavior. Not tested, not seen.

			//Also tested detecting with GDI. GDI functions return logical (not DPI-scaled) offsets/rectangles/etc. Works, but much slower.
		}

		/// <summary>
		/// On Win10+ returns <see cref="IsWindowVirtualized"/>. Else <c>false</c>.
		/// </summary>
		internal static bool IsWindowVirtualizedWin10_(wnd w) => osVersion.minWin10_1607 && IsWindowVirtualized(w);

		/// <summary>
		/// If possible, gets whether the window is DPI-scaled/virtualized, and gets physical and logical rects if scaled.
		/// Returns <c>false</c> if <c>!osVersion.minWin10_1607</c> or if cannot get that info.
		/// Gets that info in a fast and reliable way.
		/// </summary>
		internal static bool GetScalingInfo_(wnd w, out bool scaled, out RECT rPhysical, out RECT rLogical) {
			scaled = false; rPhysical = default; rLogical = default;
			if (!osVersion.minWin10_1607) return false;
			var awareness = WindowDpiAwareness(w); //fast on Win10
			if (awareness is Awareness.System or Awareness.Unaware) { //tested: unaware-gdi-scaled same as unaware
				if (awareness == Awareness.System && Api.GetDpiForWindow(w) != System) { /*fast*/
					//Cannot get rLogical. It's rare and temporary, ie when the user recently changed DPI of the primary screen.
					//Even if this func isn't used to get rects, without this fast code could be unreliable.
					Debug_.Print("w System DPI != our System DPI");
					return false;
				}
				for (; ; ) {
					RECT r1 = w.Rect, r2, r3; //note: with ClientRect 4 times faster, but unreliable if small rect. Now fast enough.
					bool rectWorkaround = false;
					using (var u = new AwarenessContext(awareness == Awareness.System ? -2 : -1)) {
						if (Api.GetAwarenessFromDpiAwarenessContext(u.Previous_) != Awareness.PerMonitor) { /*fast*/
							//cannot get rPhysical. But let's set PM awareness and get it. Works even if this process is Unaware.
							rectWorkaround = _GetRect(w, out r1);
							Debug_.Print("bad DPI awareness of this thread; workaround " + (rectWorkaround ? "OK" : "failed"));
							if (!rectWorkaround) return false; //unlikely. Then the caller probably will call the legacy func, it works with any DPI awareness.
						}
						r2 = w.Rect;
						if (r2 == r1) break;
					}
					if (!rectWorkaround) r3 = w.Rect; else _GetRect(w, out r3);
					if (r3 != r1) continue; //moved, resized or closed between Rect and Rect
					scaled = true;
					rPhysical = r1;
					rLogical = r2;
					break;
				}

				static bool _GetRect(wnd w, out RECT r) {
					using (var u2 = new AwarenessContext(-4)) { //per-monitor-v2
						if (u2.Previous_ == 0) { r = default; return false; } //API failed. Unlikely. Works even if this process is Unaware.
						r = w.Rect;
					}
					return true;
				}
			}
			return true;
		}

		internal static bool IsWindowVirtualizedLegacy_(wnd w) {
			//less reliable if control.
			//	LogicalToPhysicalPoint fails if not in top-level window rect.
			//	It seems PhysicalToLogicalPointForPerMonitorDPI doesn't fail, but it fails if not in that screen.
			w = w.Window;

			RECT rPrev = default;
			for (int i = 0; i < 5; i++) {
				if (!Api.GetWindowRect(w, out var r)) break; //Win10 1 mcs hot, 20 cold, old OS fast
				if (r != rPrev) { i = 0; rPrev = r; } //moved or resized
				POINT p = new(r.CenterX, r.CenterY); //p must be inside the window
				if (i == 1) p.y = r.bottom - 1; else if (i == 2) p.y = r.top; else if (i == 3) p.x = r.right - 1; else if (i == 4) p.x = r.left; //and p must be in correct screen, which is unknown and therefore we use this code to guess. Normally succeeds at i==0, but may fail when the window is more than in 1 screen.
				POINT k = p;
				if (osVersion.minWin8_1 ? Api.PhysicalToLogicalPointForPerMonitorDPI(w, ref p) : Api.LogicalToPhysicalPoint(w, ref p)) { //Win10 3 mcs hot, old OS fast

					//API bug: may scale the point although the window isn't scaled. Never mind.
					//	When window's center is between 2 screens and at the same time half of the window is offscreen. The area is several pixels wide.
					//if (p != k) print.it(k, p);

					if (p != k) return true;
					break;
				}
			}
			return false;
			//tested: the API works with hidden and off-screen top-level windows too. Minimized windows are off-screen.
			//tested: works even if this process is DPI System or Unaware. Tested on Win10.
			//speed on Win10 when not PM-aware: 4 mcs hot, 40 mcs cold. All API much faster on old OS (tested on Vmware).
		}

		//No. In some cases (window positions) screen.of(w) does not match scaling. Not much faster.
		//public static bool IsWindowVirtualized2(wnd w) {
		//	if (osVersion.minWin10_1607) {
		//		if (WindowDpiAwareness(w) is Awareness.PerMonitor or Awareness.Invalid) return false; //fast on Win10; slow on Win8.1, unavailable on Win7/8. Other API very slow on Win10, much faster on old OS (Vmware).
		//		w = w.Window; if (w.Is0) return false; //0.6 mcs hot
		//		var m = screen.of(w);
		//		int d1 = m.Dpi, d2 = OfWindow(w);
		//		//if (d1 == d2) print.it(d1);
		//		if (d1 == d2) return false;
		//		return w.IsAlive;
		//	} else {
		//		//same as now
		//		return false;
		//	}
		//}

		/// <summary>
		/// Can be used to temporarily change thread's DPI awareness context with API <msdn>SetThreadDpiAwarenessContext</msdn>.
		/// Does nothing if the API is unavailable (added in Windows 10 version 1607).
		/// </summary>
		/// <remarks>
		/// Programs that use this library should use manifest with <c>dpiAwareness = PerMonitorV2</c> and <c>dpiAware = True/PM</c>. Then default DPI awareness context is <b>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</b>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// using var dac = new Dpi.AwarenessContext(-1);
		/// ]]></code>
		/// </example>
		public struct AwarenessContext : IDisposable {
			nint _dac;

			/// <summary>
			/// If <see cref="Available"/>, calls API <msdn>SetThreadDpiAwarenessContext</msdn>.
			/// </summary>
			/// <param name="dpiContext">One of <msdn>DPI_AWARENESS_CONTEXT</msdn> constants: -1 <b>unaware</b>, -2 <b>system</b>, -3 <b>per-monitor</b>, -4 <b>per-monitor-v2</b>, -5 <b>unaware-gdiscaled</b>. Or a <b>DPI_AWARENESS_CONTEXT</b> handle.</param>
			public AwarenessContext(nint dpiContext) {
				_dac = osVersion.minWin10_1607 ? Api.SetThreadDpiAwarenessContext(dpiContext) : default;
			}
			//rejected: enum dpiContext.

			/// <summary>
			/// If <see cref="Available"/>, calls API <msdn>GetWindowDpiAwarenessContext</msdn> and <msdn>SetThreadDpiAwarenessContext</msdn>. Sets the awareness context of this thread equal to that of the window.
			/// </summary>
			public AwarenessContext(wnd w) : this(Available ? Api.GetWindowDpiAwarenessContext(w) : 0) { }

			/// <summary>
			/// Restores previous DPI awareness context.
			/// </summary>
			public void Dispose() {
				if (_dac == default) return;
				Api.SetThreadDpiAwarenessContext(_dac);
				_dac = default;
			}

			///
			internal nint Previous_ => _dac;

			/// <summary>
			/// Returns <c>true</c> if thread DPI awareness contexts are available on this Windows version (Windows 10 1607 and later).
			/// </summary>
			public static bool Available => osVersion.minWin10_1607;

			/////
			//public const nint DPI_AWARENESS_CONTEXT_UNAWARE = -1;
			/////
			//public const nint DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2;
			/////
			//public const nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = -3;
			/////
			//public const nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
			/////
			//public const nint DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED = -5;
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// Used for <i>dpiOf</i> parameter of functions.
	/// Has implicit conversions from <b>int</b> (DPI), <b>wnd</b> (DPI of window), <b>IntPtr</b> (DPI of screen handle), <b>POINT</b> (DPI of screen containing point), <b>RECT</b> (DPI of screen containing rectangle), forms <b>Control</b>, WPF <b>DependencyObject</b>. The conversion operators set the <see cref="Dpi"/> property and the function can use it.
	/// </summary>
	public struct DpiOf {
		readonly int _dpi;

		///
		public DpiOf(int dpi) { _dpi = dpi; }

		/// <exception cref="AuWndException">Invalid window handle.</exception>
		public DpiOf(wnd w) {
			w.ThrowIfInvalid();
			_dpi = More.Dpi.OfWindow(w);
		}
		//rejected: parameter supportWin81. Rarely used. Can use Dpi functions when need.

		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="AuWndException">Invalid window handle.</exception>
		public DpiOf(System.Windows.Forms.Control c) : this(Not_.NullRet(c).Hwnd()) { }

		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="AuWndException">Invalid window handle.</exception>
		public DpiOf(System.Windows.DependencyObject c) : this(Not_.NullRet(c).Hwnd()) { }

		///
		public DpiOf(IntPtr hMonitor) { _dpi = More.Dpi.OfScreen(hMonitor); }

		///
		public DpiOf(POINT screenOf) : this(screen.of(screenOf).Handle) { }

		///
		public DpiOf(RECT screenOf) : this(screen.of(screenOf).Handle) { }

		///
		public static implicit operator DpiOf(int dpi) => new(dpi);

		/// <exception cref="AuWndException">Invalid window handle.</exception>
		public static implicit operator DpiOf(wnd w) => new(w);

		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="AuWndException">Invalid window handle.</exception>
		public static implicit operator DpiOf(System.Windows.Forms.Control c) => new(c);

		/// <exception cref="ArgumentNullException"></exception>
		/// <exception cref="AuWndException">Invalid window handle.</exception>
		public static implicit operator DpiOf(System.Windows.DependencyObject c) => new(c);

		///
		public static implicit operator DpiOf(IntPtr hMonitor) => new(hMonitor);

		///
		public static implicit operator DpiOf(POINT screenOf) => new(screenOf);

		///
		public static implicit operator DpiOf(RECT screenOf) => new(screenOf);

		///
		public int Dpi => _dpi;

		///
		public static implicit operator int(DpiOf d) => d._dpi;
	}
}