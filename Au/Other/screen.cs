namespace Au {
	/// <summary>
	/// Represents a screen device. Gets its rectangle etc.
	/// </summary>
	/// <remarks>
	/// A computer can have one or more screens (aka display devices, monitors). One of them is the <i>primary</i> screen; its top-left coordinate is 0 0.
	/// To show or find a window or some object in a particular screen, need to identify the screen somehow. At Windows API level each screen has a unique integer identifier, known as screen handle or <b>HMONITOR</b>. But it is a random variable value and therefore cannot be specified directly in script etc. Instead can be used screen index or some object on that screen (window, point, rectangle).
	/// 
	/// A <b>screen</b> variable can contain either a screen handle or a callback function that returns a screen handle. If empty, most functions interpret it as the primary screen.
	/// 
	/// To create <b>screen</b> variables use static functions (like <c>screen.index(1)</c> or <c>screen.primary</c>) or constructors (like <c>new screen(()=>screen.index(1))</c>) or <see cref="at"/>. Then call non-static functions to get screen properties.
	/// 
	/// A screen handle cannot be reliably used for a long time. Screen handles may change when changing the configuration of multiple screens. Consider a "lazy" variable, ie with callback function <see cref="LazyFunc"/>. Then, whenever a function needs a screen handle, it calls the callback function which returns a <b>screen</b> with fresh handle.
	/// </remarks>
	public struct screen : IEquatable<screen> {
		readonly IntPtr _h;
		readonly Func<screen> _func;

		/// <summary>
		/// Creates variable with screen handle, aka <b>HMONITOR</b>.
		/// </summary>
		public screen(IntPtr handle) { _h = handle; _func = null; }

		/// <summary>
		/// Creates "lazy" variable that calls your function to get screen when need.
		/// </summary>
		public screen(Func<screen> f) { _h = default; _func = f; }

		/// <summary>
		/// Gets the screen handle, aka <b>HMONITOR</b>. Returns <c>default(IntPtr)</c> if it wasn't set; see <see cref="Now"/>.
		/// </summary>
		public IntPtr Handle => _h;

		/// <summary>
		/// Gets the callback function that returns <b>screen</b> when need. Returns <c>null</c> if it wasn't set.
		/// </summary>
		public Func<screen> LazyFunc => _func;

		/// <summary>
		/// Returns <c>true</c> if this variable has no screen handle and no callback function.
		/// </summary>
		public bool IsEmpty => _h == default && _func == null;

		IntPtr _Handle() {
			if (_h != default) return _h;
			if (_func != null) return _func()._Handle();
			return primary._h;
		}

		/// <summary>
		/// Returns a copy of this variable with <see cref="Handle"/>.
		/// </summary>
		/// <remarks>
		/// If this variable has <see cref="Handle"/>, returns its clone. Else if has <see cref="LazyFunc"/>, calls it. Else gets the primary screen.
		/// </remarks>
		public screen Now => new(_Handle());

		/// <summary>
		/// Gets the primary screen.
		/// </summary>
		/// <remarks>
		/// The returned variable has <see cref="Handle"/>. To create lazy variable (with <see cref="LazyFunc"/>), use <c>screen.index(0, lazy: true)</c>.
		/// </remarks>
		public static screen primary => new(Api.MonitorFromWindow(default, SODefault.Primary)); //fast

		/// <summary>
		/// Returns a lazy <b>screen</b> variable that later will get the screen from the mouse cursor position at that time.
		/// </summary>
		/// <remarks>
		/// If need non-lazy: <c>screen.of(mouse.xy)</c> or <c>screen.ofMouse.Now</c>.
		/// </remarks>
		public static screen ofMouse => new(s_ofMouse);

		/// <summary>
		/// Returns a lazy <b>screen</b> variable that later will get the screen of the active window at that time.
		/// </summary>
		/// <remarks>
		/// If need non-lazy: <c>screen.of(wnd.active)</c> or <c>screen.ofActiveWindow.Now</c>.
		/// </remarks>
		public static screen ofActiveWindow => new(s_ofActiveWindow);

		static readonly Func<screen> s_ofMouse = () => of(mouse.xy);
		static readonly Func<screen> s_ofActiveWindow = () => of(wnd.active);

		internal bool IsOfMouse_ => ReferenceEquals(_func, s_ofMouse);
		internal bool IsOfActiveWindow_ => ReferenceEquals(_func, s_ofActiveWindow);

		/// <summary>
		/// Gets screen containing the biggest part of the specified window or nearest to it.
		/// </summary>
		/// <param name="w">Window or control. If <c>default(wnd)</c> or invalid, gets the primary screen.</param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">
		/// Create variable with <see cref="LazyFunc"/> that later will get screen handle.
		/// Other ways to create lazy:
		/// <br/>• use <b>wndFinder</b>. Example: <c>screen.of(new wndFinder("* Notepad"))</c>.
		/// <br/>• use constructor. Example: <c>new screen(() => screen.of(wnd.findFast(cn: "Notepad")))</c>.
		/// </param>
		public static screen of(wnd w, SODefault defaultScreen = SODefault.Nearest, bool lazy = false)
			=> lazy
			? new screen(() => of(w, defaultScreen))
			: new screen(Api.MonitorFromWindow(w, defaultScreen));

		/// <summary>
		/// Gets screen containing the biggest part of the specified window or nearest to it.
		/// </summary>
		/// <param name="f">Window finder. If window not found, gets the primary screen.</param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will find window and get screen handle. Default <c>true</c>.</param>
		public static screen of(wndFinder f, SODefault defaultScreen = SODefault.Nearest, bool lazy = true)
			=> lazy
			? new screen(() => of(f, defaultScreen, false))
			: of(f.Find(), defaultScreen);

		/// <summary>
		/// Gets screen containing the biggest part of the specified winforms window or control or nearest to it.
		/// </summary>
		/// <param name="c">Window or control. If handle not created, gets the primary screen. Cannot be <c>null</c>.</param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
		public static screen of(System.Windows.Forms.Control c, SODefault defaultScreen = SODefault.Nearest, bool lazy = false)
			=> lazy
			? new screen(() => of(c, defaultScreen))
			: of(c.Hwnd(), defaultScreen);

		/// <summary>
		/// Gets screen containing the biggest part of the specified WPF window or nearest to it.
		/// </summary>
		/// <param name="w">WPF window. If handle not created, gets the primary screen. Cannot be <c>null</c>.</param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
		public static screen of(System.Windows.Window w, SODefault defaultScreen = SODefault.Nearest, bool lazy = false)
			=> lazy
			? new screen(() => of(w, defaultScreen))
			: of(w.Hwnd(), defaultScreen);

		/// <summary>
		/// Gets screen containing the biggest part of the specified WPF element (of its rectangle) or nearest to it.
		/// </summary>
		/// <param name="elem">WPF element. If not loaded, gets the primary screen. Cannot be <c>null</c>.</param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
		public static screen of(System.Windows.FrameworkElement elem, SODefault defaultScreen = SODefault.Nearest, bool lazy = false)
			=> lazy
			? new screen(() => of(elem, defaultScreen))
			: of(elem.RectInScreen(), defaultScreen);

		/// <summary>
		/// Gets screen containing the specified point or nearest to it.
		/// </summary>
		/// <param name="p"></param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
		public static screen of(POINT p, SODefault defaultScreen = SODefault.Nearest, bool lazy = false)
			=> lazy
			? new screen(() => of(p, defaultScreen))
			: new screen(Api.MonitorFromPoint(p, defaultScreen));

		/// <summary>
		/// Gets screen containing the specified point or nearest to it.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
		public static screen of(int x, int y, SODefault defaultScreen = SODefault.Nearest, bool lazy = false)
			=> lazy
			? new screen(() => of(x, y, defaultScreen))
			: of((x, y), defaultScreen);

		/// <summary>
		/// Gets screen containing the biggest part of the specified rectangle or nearest to it.
		/// </summary>
		/// <param name="r"></param>
		/// <param name="defaultScreen"></param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
		public static screen of(RECT r, SODefault defaultScreen = SODefault.Nearest, bool lazy = false)
			=> lazy
			? new screen(() => of(r, defaultScreen))
			: new screen(Api.MonitorFromRect(r, defaultScreen));

		/// <summary>
		/// Gets screens at various positions relative to the primary screen.
		/// </summary>
		public static class at {
			/// <summary>Gets a screen nearest to the top edge of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen top(bool lazy = false) => _S(0, -700, lazy);

			/// <summary>Gets a screen nearest to the bottom edge of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen bottom(bool lazy = false) => _S(0, 700, lazy);

			/// <summary>Gets a screen nearest to the left edge of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen left(bool lazy = false) => _S(-1000, 0, lazy);

			/// <summary>Gets a screen nearest to the right edge of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen right(bool lazy = false) => _S(1000, 0, lazy);

			/// <summary>Gets a screen nearest to the top-left corner of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen topLeft(bool lazy = false) => _S(-1000, -700, lazy);

			/// <summary>Gets a screen nearest to the top-right corner of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen topRight(bool lazy = false) => _S(1000, -700, lazy);

			/// <summary>Gets a screen nearest to the bottom-left corner of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen bottomLeft(bool lazy = false) => _S(-1000, 700, lazy);

			/// <summary>Gets a screen nearest to the bottom-right corner of the primary screen.</summary>
			/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
			public static screen bottomRight(bool lazy = false) => _S(1000, 700, lazy);
		}

		/// <summary>
		/// Gets screen at (or nearest to) the specified offset from the primary screen (PS).
		/// </summary>
		/// <param name="dx">Horizontal offset. Negative is to the left from the left edge of PS. Positive is to the right from the right edge of PS. Zero is the horizontal center of PS.</param>
		/// <param name="dy">Vertical offset. Negative is up from the top edge of PS. Positive is down from the bottom edge of PS. Zero is the vertical center of PS.</param>
		static screen _S(int dx, int dy, bool lazy) {
			var r = primary.Rect;
			if (dx > 0) dx += r.right; else if (dx == 0) dx = r.CenterX;
			if (dy > 0) dy += r.bottom; else if (dy == 0) dy = r.CenterY;
			return of(dx, dy, lazy: lazy);
		}

		/// <summary>
		/// Gets all screens.
		/// </summary>
		/// <remarks>
		/// The order of array elements may be different than in Windows Settings (Control Panel). The primary screen is always at index 0.
		/// The array is not cached. Each time calls API <msdn>EnumDisplayMonitors</msdn>.
		/// </remarks>
		public static screen[] all => _All().ToArray();

		internal static List<screen> _All() {
			t_a ??= new();
			t_a.Clear();
			t_a.Add(primary); //fast

			static bool _Enum(IntPtr hmon, IntPtr hdc, IntPtr r, GCHandle gch) { //70-1000 mcs cold
				var a = gch.Target as List<screen>;
				if (hmon != a[0].Handle) a.Add(new screen(hmon));
				return true;
			};

			var gch = GCHandle.Alloc(t_a);
			if (!Api.EnumDisplayMonitors(default, default, _Enum, gch)) {
				//in certain conditions EDM fails.
				//	Where failed, it was used in incorrect code, maybe near stack overflow.
				//	Anyway, then call it in other thread, then works.
				//print.it(lastError.message); //0
				run.thread(() => { bool ok = Api.EnumDisplayMonitors(default, default, _Enum, gch); Debug.Assert(ok); }).Join();
				Debug_.Print(t_a.Count);
			}
			gch.Free();
			return t_a;
		}
		[ThreadStatic] static List<screen> t_a;

		/// <summary>
		/// Gets screen at the specified index of the <see cref="all"/> array.
		/// </summary>
		/// <param name="index">0-based screen index. Index 0 is the primary screen. If index too big, gets the primary screen.</param>
		/// <param name="lazy">Create variable with <see cref="LazyFunc"/> that later will get screen handle.</param>
		/// <exception cref="ArgumentOutOfRangeException">Negative <i>index</i>.</exception>
		public static screen index(int index, bool lazy = false) {
			if (index < 0) throw new ArgumentOutOfRangeException();
			if (lazy) return new screen(() => screen.index(index));
			if (index > 0) {
				var a = _All();
				if (index < a.Count) return a[index];
				//print.warning("Invalid screen index.");
			}
			return primary;
		}
		//We don't use a cached array that is updated like in Screen class code, eg on SystemEvents.DisplaySettingsChanging (wm_displaychanged).
		//	Then index functions are faster, but less reliable when changing display settings, because the array is updated with a delay.
		//	Now index functions are fast enough. Faster than EnumWindows which is used much more often.

		//At first there was an implicit conversion from int that called index()? I don't remember why removed, but probably for a good reason.

		/// <summary>
		/// Gets index of this screen in the <see cref="all"/> array.
		/// </summary>
		/// <remarks>
		/// Returns 0 (index of primary screen) if this variable is empty. Returns -1 if the screen handle is invalid; it can happen after changing display settings, but is rare.
		/// </remarks>
		public int ScreenIndex {
			get {
				if (_h == default && _func == null) return 0;
				var h = _Handle();
				var a = _All();
				for (int i = 0; i < a.Count; i++) if (a[i]._h == h) return i;
				return -1;
			}
		}

		/// <summary>
		/// Gets screen rectangle and other info.
		/// </summary>
		/// <returns>
		/// Tuple containing:
		/// <br/>• <b>rect</b> - screen rectangle.
		/// <br/>• <b>workArea</b> - work area rectangle.
		/// <br/>• <b>isPrimary</b> - <c>true</c> if it is the primary screen.
		/// <br/>• <b>isAlive</b> - <c>false</c> if the screen handle is invalid; then the function gets info of the primary screen.
		/// </returns>
		/// <remarks>
		/// If this variable holds a callback function, this function calls it to get screen handle. See also <see cref="Now"/>.
		/// </remarks>
		public unsafe (RECT rect, RECT workArea, bool isPrimary, bool isAlive) Info {
			get {
				var h = _func != null ? _Handle() : _h;
				for (int i = h != default ? 0 : 1; i < 10; i++, h = primary._h) { //retry if fails
					if (Api.GetMonitorInfo(h, out var m)) //fast
						return (m.rcMonitor, m.rcWork, 0 != (m.dwFlags & 1), i == 0);
				}
				return default;
			}
		}

		/// <summary>
		/// Calls <see cref="Info"/> and returns rectangle of the screen or its work area.
		/// </summary>
		/// <param name="workArea">Get work area rectangle.</param>
		public RECT GetRect(bool workArea = false) {
			var v = Info;
			return workArea ? v.workArea : v.rect;
		}

		/// <summary>
		/// Calls <see cref="Info"/> and returns screen rectangle.
		/// </summary>
		public RECT Rect => Info.rect;

		/// <summary>
		/// Calls <see cref="Info"/> and returns work area rectangle.
		/// </summary>
		public RECT WorkArea => Info.workArea;

		/// <summary>
		/// Gets DPI of this screen.
		/// Calls <see cref="Dpi.OfScreen"/>.
		/// </summary>
		public int Dpi => More.Dpi.OfScreen(_Handle());
		//public int Dpi(bool supportWin81 = false) => More.Dpi.OfScreen(_Handle(), supportWin81); //no, rarely need, don't complicate everything. When need, can use Dpi.OfScreen.

		/// <summary>
		/// True if the screen handle is valid.
		/// </summary>
		/// <remarks>
		/// Don't use with variables that hold a callback function. This function does not call it and returns <c>false</c>.
		/// </remarks>
		public unsafe bool IsAlive => _h != default && Api.GetMonitorInfo(_h, out _);

		///
		public override string ToString() => _h.ToString() + " " + Rect.ToString();

		///
		public override int GetHashCode() => (int)_Handle();

		///
		public override bool Equals(object obj) => obj is screen && Equals((screen)obj);

		///
		public bool Equals(screen other) => other._Handle() == _Handle();

		///
		public static bool operator ==(screen a, screen b) => a.Equals(b);
		///
		public static bool operator !=(screen a, screen b) => !a.Equals(b);

		//rejected. GetHashCode gets hmonitor but is undocumented. Rarely used.
		///// <summary>Converts from <see cref="Screen"/>.</summary>
		//public static implicit operator screen(Screen scrn) => new screen((IntPtr)scrn.GetHashCode());

		///// <summary>Converts to <see cref="Screen"/>. Returns <c>null</c> if failed.</summary>
		//public static implicit operator Screen(screen scrn) { int h=(int)scrn._Handle(); return Screen.AllScreens.FirstOrDefault(o => o.GetHashCode() == h);

		/// <summary>
		/// Returns <c>true</c> if point <i>p</i> is in some screen.
		/// </summary>
		public static bool isInAnyScreen(POINT p) => Api.MonitorFromPoint(p, SODefault.Zero) != default;

		/// <summary>
		/// Returns <c>true</c> if rectangle <i>r</i> intersects with some screen.
		/// </summary>
		public static bool isInAnyScreen(RECT r) => Api.MonitorFromRect(r, SODefault.Zero) != default;

		/// <summary>
		/// Returns <c>true</c> if rectangle of window <i>w</i> intersects with some screen.
		/// </summary>
		public static bool isInAnyScreen(wnd w) => Api.MonitorFromWindow(w, SODefault.Zero) != default;

		/// <summary>
		/// Gets bounding rectangle of all screens.
		/// </summary>
		public static RECT virtualScreen
			=> new(Api.GetSystemMetrics(Api.SM_XVIRTUALSCREEN), Api.GetSystemMetrics(Api.SM_YVIRTUALSCREEN), Api.GetSystemMetrics(Api.SM_CXVIRTUALSCREEN), Api.GetSystemMetrics(Api.SM_CYVIRTUALSCREEN));

		//20 times faster, but maybe less reliable
		//public static RECT virtualScreen2 => wnd.getwnd.shellWindow.Rect;

		internal screen ThrowIfWithHandle_ => _h == default ? this : throw new ArgumentException("screen with Handle. Must be lazy or empty.");
	}
}

namespace Au.Types {
	/// <summary>
	/// Used with <see cref="screen.of"/> to specify what screen to return if the window/point/etc is not in a screen or if the window handle is invalid etc.
	/// </summary>
	public enum SODefault {
		/// <summary>Create empty variable.</summary>
		Zero, //MONITOR_DEFAULTTONULL

		/// <summary>The primary screen.</summary>
		Primary, //MONITOR_DEFAULTTOPRIMARY

		/// <summary>The nearest screen. If window handle is invalid - the primary screen.</summary>
		Nearest, //MONITOR_DEFAULTTONEAREST
	}
}
