using Au.Triggers;

namespace Au;

//rejected: warn if using common controls version 5. Eg tooltips don't work. The same with menus.
//	Too many problems: 1. How to warn? Message box? 2. Need to also warn if no supported OS, DPI awareness, etc. Load/parse manifest in module initializer? 3. Maybe the app does not want to use these features (eg high DPI). 4. Maybe not possible to use correct manifest, eg when used in a scripting app. 5. Etc.
//	Never mind. The required manifest is documented.

/// <summary>
/// Floating toolbar.
/// Can be attached to windows of other programs.
/// </summary>
/// <remarks>
/// To create toolbar code can be used menu TT -> New toolbar.
/// 
/// Not thread-safe. All functions must be called from the same thread that created the <b>toolbar</b> object, except where documented otherwise. Note: item actions by default run in other threads; see <see cref="MTBase.ActionThread"/>.
/// </remarks>
public partial class toolbar : MTBase {
	record class _Settings : JSettings {
		public static _Settings Load(string file, bool useDefault) => Load<_Settings>(file, useDefault);
		
		public TBAnchor anchor = TBAnchor.TopLeft;
		public TBLayout layout;
		public TBBorder border = TBBorder.Width2;
		public bool dispText = true, sizable = true, autoSize = true;
		public TBFlags miscFlags = TBFlags.HideWhenFullScreen | TBFlags.ActivateOwnerWindow;
		public System.Windows.Size size = new(150, 24);
		public double wrapWidth;
		public TBOffsets offsets; // = new(150, 5, 7, 7);
		public int screenx, screeny;
	}
	
	readonly _Settings _sett;
	readonly List<TBItem> _a = new();
	bool _created;
	bool _closed;
	bool _topmost; //no owner, or owner is topmost
	double _dpiF; //DPI scaling factor, eg 1.5 for 144 dpi
	
	static int s_treadId;
	
	/// <param name="name">
	/// Toolbar name. Must be valid filename.
	/// Used for: toolbar window name, settings file name, <see cref="find"/>, some other functions.
	/// If <c>null</c>, uses the caller function's name if available, else exception.
	/// </param>
	/// <param name="flags"></param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="m_">[](xref:caller_info)</param>
	/// <param name="settingsFile"><c>null</c> or full path of the settings file of this toolbar.</param>
	/// <exception cref="ArgumentException">Invalid <i>name</i>.</exception>
	/// <remarks>
	/// Each toolbar has a settings file, where are saved its position, size and context menu settings. This function reads the file if exists, ie if settings changed in the past. See <see cref="getSettingsFilePath"/>. If fails, prints a warning and uses default settings.
	/// 
	/// Sets properties:
	/// - <see cref="MTBase.ActionThread"/> = <c>true</c>.
	/// - <see cref="MTBase.ExtractIconPathFromCode"/> = <c>true</c>.
	/// </remarks>
	public toolbar(string name = null, TBCtor flags = 0,
		[CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0, [CallerMemberName] string m_ = null, string settingsFile = null)
		: base(name, f_, l_, m_) {
		
		if (s_treadId == 0) s_treadId = _threadId; else if (_threadId != s_treadId) print.warning("All toolbars should be in single thread. Multiple threads use more CPU. If using triggers, insert this code before adding toolbar triggers: <code>Triggers.Options.ThreadOfTriggers();</code> or <code>Triggers.Options.ThreadThis();</code>");
		
		settingsFile = flags.Has(TBCtor.DontSaveSettings) ? null : (settingsFile ?? getSettingsFilePath(_name));
		_sett = _Settings.Load(settingsFile, flags.Has(TBCtor.ResetSettings));
		
		_offsets = _sett.offsets; //SHOULDDO: don't use saved offsets if this toolbar was free and now is owned or vice versa. Because the position is irrelevent and may be far/offscreen. It usually happens when testing.
		
		ActionThread = true;
		ExtractIconPathFromCode = true;
	}
	
	/// <summary>
	/// Gets the toolbar window.
	/// </summary>
	public wnd Hwnd => _w;
	
	/// <summary>
	/// Returns <c>true</c> if the toolbar is open. False if closed or <see cref="Show"/> still not called.
	/// </summary>
	public bool IsOpen => _created && !_closed;
	
	/// <summary>
	/// Gets the name of the toolbar.
	/// </summary>
	public string Name => _name;
	
	///
	public override string ToString() => _IsSatellite ? "    " + Name : Name; //the indentation is for the list in the Active toolbars dialog
	
	/// <summary>
	/// True if this toolbar started with default settings. False if loaded saved settings from file.
	/// </summary>
	/// <seealso cref="getSettingsFilePath"/>
	/// <seealso cref="TBCtor"/>
	public bool FirstTime => !_sett.LoadedFile;
	
	#region static functions
	
	/// <summary>
	/// Gets full path of toolbar's settings file. The file may exist or not.
	/// </summary>
	/// <param name="toolbarName">Toolbar name. If this string is a full path, returns this string.</param>
	/// <remarks>
	/// Path: <c>folders.Workspace + $@"\.toolbars\{toolbarName}.json"</c>. If <see cref="folders.Workspace"/> is <c>null</c>, uses <see cref="folders.ThisAppDocuments"/>.
	/// </remarks>
	public static string getSettingsFilePath(string toolbarName) {
		if (toolbarName.NE()) throw new ArgumentException("Empty name");
		if (pathname.isFullPath(toolbarName)) return toolbarName;
		string s = folders.Workspace.Path ?? folders.ThisAppDocuments;
		return s + @"\.toolbars\" + toolbarName + ".json";
	}
	
	/// <summary>
	/// Finds an open toolbar by <see cref="Name"/>.
	/// </summary>
	/// <returns><c>null</c> if not found or closed or never shown (<see cref="Show"/> not called).</returns>
	/// <remarks>
	/// Finds only toolbars created in the same script and thread.
	/// 
	/// Does not find satellite toolbars. Use this code: <c>toolbar.find("owner toolbar").Satellite</c>
	/// </remarks>
	public static toolbar find(string name) => _Manager._atb.Find(o => o.Name == name);
	
	internal static void TriggerActionEndedInToolbarUnfriendlyThread_() {
		if (_Manager._atb.Count > 0)
			print.warning("Toolbars in wrong thread. Insert this code before adding toolbar triggers: <code>Triggers.Options.ThreadOfTriggers();</code> or <code>Triggers.Options.ThreadThis();</code>", -1);
	}
	
	#endregion
	
	#region add item
	
	void _Add(TBItem item, string text, Delegate click, MTImage image, int l_, string f_) {
		_ThreadTrap();
		_CreatedTrap(_closed ? null : "cannot add items while the toolbar is open. To add to submenu, use the submenu variable.");
		item.Set_(this, text, click, image, l_, _sourceFile == null ? null : f_);
		_a.Add(item);
		Last = item;
	}
	
	/// <summary>
	/// Adds button.
	/// Same as <see cref="this[string, MTImage, int, string]"/>.
	/// </summary>
	/// <param name="text">Text. Or <c>"Text|Tooltip"</c>, or <c>"|Tooltip"</c>, or <c>"Text|"</c>. Separator can be <c>"|"</c> or <c>"\0 "</c> (then <c>"|"</c> isn't a separator). To always display text regardless of <see cref="DisplayText"/>, append <c>"\a"</c>, like <c>"Text\a"</c> or <c>"Text\a|Tooltip"</c>.</param>
	/// <param name="click">Action called when the button clicked.</param>
	/// <param name="image">Image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <remarks>
	/// More properties can be specified later (set properties of the returned <see cref="TBItem"/> or use <see cref="Items"/>) or before (<see cref="MTBase.ActionThread"/>, <see cref="MTBase.ActionException"/>, <see cref="MTBase.ExtractIconPathFromCode"/>, <see cref="MTBase.PathInTooltip"/>).
	/// </remarks>
	public TBItem Add(string text, Action<TBItem> click, MTImage image = default, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null) {
		var item = new TBItem(TBItemType.Button);
		_Add(item, text, click, image, l_, f_);
		return item;
	}
	
	/// <summary>
	/// Adds button.
	/// Same as <see cref="Add(string, Action{TBItem}, MTImage, int, string)"/>.
	/// </summary>
	/// <value>Action called when the button clicked.</value>
	/// <remarks>
	/// More properties can be specified later (set properties of <see cref="Last"/> <see cref="TBItem"/> or use <see cref="Items"/>) or before (<see cref="MTBase.ActionThread"/>, <see cref="MTBase.ActionException"/>, <see cref="MTBase.ExtractIconPathFromCode"/>, <see cref="MTBase.PathInTooltip"/>).
	/// </remarks>
	/// <example>
	/// These two are the same.
	/// <code><![CDATA[
	/// tb.Add("Example", o => print.it(o));
	/// tb["Example"] = o => print.it(o);
	/// ]]></code>
	/// These four are the same.
	/// <code><![CDATA[
	/// var b = tb.Add("Example", o => print.it(o)); b.Tooltip="tt";
	/// tb.Add("Example", o => print.it(o)).Tooltip="tt";
	/// tb["Example"] = o => print.it(o); var b=tb.Last; b.Tooltip="tt";
	/// tb["Example"] = o => print.it(o); tb.Last.Tooltip="tt";
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="Add(string, Action{TBItem}, MTImage, int, string)"/>
	public Action<TBItem> this[string text, MTImage image = default, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null] {
		set { Add(text, value, image, l_, f_); }
	}
	
	//CONSIDER: AddCheck, AddRadio.
	
	/// <summary>
	/// Adds button with drop-down menu.
	/// </summary>
	/// <param name="menu">Action that adds menu items. Called whenever the button clicked.</param>
	/// <remarks>
	/// The submenu is a <see cref="popupMenu"/> object. It inherits these properties of this toolbar: <see cref="MTBase.ExtractIconPathFromCode"/>, <see cref="MTBase.ActionException"/>, <see cref="MTBase.ActionThread"/>, <see cref="MTBase.PathInTooltip"/>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// tb.Menu("Menu", m => {
	/// 	m["M1"]=o=>print.it(o);
	/// 	m["M2"]=o=>print.it(o);
	/// });
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="Add(string, Action{TBItem}, MTImage, int, string)"/>
	public TBItem Menu(string text, Action<popupMenu> menu, MTImage image = default, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null) {
		var item = new TBItem(TBItemType.Menu);
		_Add(item, text, menu, image, l_, f_);
		return item;
	}
	
	/// <summary>
	/// Adds button with drop-down menu.
	/// </summary>
	/// <param name="menu">Func that returns the menu. Called whenever the button clicked.</param>
	/// <remarks>
	/// The caller creates the menu (creates the <see cref="popupMenu"/> object and adds items) and can reuse it many times. Other overload does not allow to create <b>popupMenu</b> and reuse same object.
	/// The submenu does not inherit properties of this toolbar.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var m = new popupMenu(); m.AddCheck("C1"); m.AddCheck("C2");
	/// t.Menu("Menu", () => m);
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="Add(string, Action{TBItem}, MTImage, int, string)"/>
	public TBItem Menu(string text, Func<popupMenu> menu, MTImage image = default, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null) {
		var item = new TBItem(TBItemType.Menu);
		_Add(item, text, menu, image, l_, f_);
		return item;
	}
	
	/// <summary>
	/// Adds new vertical separator. Horizontal if vertical toolbar.
	/// </summary>
	public TBItem Separator() {
		int i = _a.Count - 1;
		if (i < 0 || _a[i].IsGroup_) throw new InvalidOperationException("first item is separator");
		var item = new TBItem(TBItemType.Separator);
		_Add(item, null, null, default, 0, null);
		return item;
	}
	
	/// <summary>
	/// Adds new horizontal separator, optionally with text.
	/// </summary>
	/// <param name="text">Text. Or <c>"Text|Tooltip"</c>, or <c>"|Tooltip"</c>, or <c>"Text|"</c>. Separator can be <c>"|"</c> or <c>"\0 "</c> (then <c>"|"</c> isn't a separator).</param>
	public TBItem Group(string text = null) {
		var item = new TBItem(TBItemType.Group);
		_Add(item, text, null, default, 0, null);
		return item;
	}
	
	/// <summary>
	/// Gets the last added item.
	/// </summary>
	public TBItem Last { get; private set; }
	
	/// <summary>
	/// Gets added buttons.
	/// </summary>
	/// <remarks>
	/// Allows to set properties of multiple buttons in single place instead of after each "add button" code line.
	/// Skips separators and groups.
	/// </remarks>
	public IEnumerable<TBItem> Items {
		get {
			_ThreadTrap();
			foreach (var v in _a) {
				if (!v.IsSeparatorOrGroup_) yield return v;
			}
		}
	}
	
	#endregion
	
	#region show, close, owner
	
	/// <summary>
	/// Shows the toolbar.
	/// </summary>
	/// <param name="screen">
	/// Attach to this screen. For example a screen index (0 the primary, 1 the first non-primary, and so on). Example: <c>screen.index(1)</c>.
	/// If not specified, the toolbar will be attached to the screen where it is now or where will be moved later.
	/// Don't use this parameter if this toolbar was created by <see cref="AutoHideScreenEdge"/>, because then screen is already known.
	/// </param>
	/// <exception cref="ArgumentException">The toolbar was created by <b>AutoHideScreenEdge</b>, and now screen specified again.</exception>
	/// <exception cref="InvalidOperationException"><b>Show</b> already called.</exception>
	/// <remarks>
	/// The toolbar will be moved when the screen moved or resized.
	/// </remarks>
	public void Show(screen screen = default) {
		if (!_screenAHSE.IsEmpty) { if (screen.IsEmpty) screen = _screenAHSE; else throw new ArgumentException(); }
		_Show(false, default, null, screen);
	}
	
	/// <summary>
	/// Shows the toolbar and attaches to a window.
	/// </summary>
	/// <param name="ownerWindow">Window or control. Can belong to any process.</param>
	/// <param name="clientArea">Let the toolbar position be relative to the client area of the window.</param>
	/// <exception cref="InvalidOperationException"><b>Show</b> already called.</exception>
	/// <exception cref="ArgumentException"><b>ownerWindow</b> is 0.</exception>
	/// <remarks>
	/// The toolbar will be above the window in the Z order; moved when the window moved or resized; hidden when the window hidden, cloaked or minimized; destroyed when the window destroyed.
	/// </remarks>
	public void Show(wnd ownerWindow, bool clientArea = false) {
		_followClientArea = clientArea;
		_Show(true, ownerWindow, null, _screenAHSE);
	}
	
	/// <summary>
	/// Shows the toolbar and attaches to an object in a window.
	/// </summary>
	/// <param name="ownerWindow">Window that contains the object. Can be control. Can belong to any process.</param>
	/// <param name="oo">A variable of a user-defined class that implements <see cref="ITBOwnerObject"/> interface. It provides object location, visibility, etc.</param>
	/// <exception cref="InvalidOperationException"><b>Show</b> already called.</exception>
	/// <exception cref="ArgumentException"><b>ownerWindow</b> is 0.</exception>
	/// <remarks>
	/// The toolbar will be above the window in the Z order; moved when the object or window moved or resized; hidden when the object or window hidden, cloaked or minimized; destroyed when the object or window destroyed.
	/// </remarks>
	public void Show(wnd ownerWindow, ITBOwnerObject oo) => _Show(true, ownerWindow, oo, default);
	
	/// <summary>
	/// Shows the toolbar.
	/// If <i>ta</i> is <b>WindowTriggerArgs</b>, attaches the toolbar to the trigger window.
	/// Else if <i>ta</i> != <c>null</c>, calls <see cref="TriggerArgs.DisableTriggerUntilClosed(toolbar)"/>.
	/// </summary>
	public void Show(TriggerArgs ta) {
		if (ta is WindowTriggerArgs wta) {
			Show(wta.Window);
		} else {
			Show();
			ta?.DisableTriggerUntilClosed(this);
		}
	}
	
	//used for normal toolbars, not for satellite toolbars
	void _Show(bool owned, wnd owner, ITBOwnerObject oo, screen screen) {
		_ThreadTrap();
		_CreatedTrap("this toolbar is already open");
		
		wnd c = default;
		if (owned) {
			if (owner.Is0) throw new ArgumentException();
			var w = owner.Window; if (w.Is0) return;
			if (w != owner) { c = owner; owner = w; }
			if (!_screenAHSE.IsEmpty) _sett.anchor |= TBAnchor.Screen;
		}
		
		_CreateWindow(owned, owner, screen);
		_Manager.Add(this, owner, c, oo);
	}
	
	//used for normal and satellite toolbars
	void _CreateWindow(bool owned, wnd owner, screen screen = default, bool isSatelite = false) {
		_topmost = !owned || owner.IsTopmost;
		if (!owned || Anchor.OfScreen()) _os = new _OwnerScreen(this, screen); else screen = screen.of(owner);
		
		_RegisterWinclass();
		if (_os != null) _SetDpi(); else _SetDpi(screen.Dpi); //OwnerWindow still not set
		_Images(false);
		_MeasureText();
		var size = _Measure();
		var style = WS.POPUP | WS.CLIPCHILDREN | _BorderStyle(_sett.border);
		var estyle = WSE.TOOLWINDOW | WSE.NOACTIVATE;
		if (_topmost) estyle |= WSE.TOPMOST;
		var r = screen.Rect; //create in center of screen, to minimize possibility of DPI change when setting final position
		r = new(r.CenterX - size.width / 2, r.CenterY - size.height / 2, size.width, size.height);
		Dpi.AdjustWindowRectEx(_dpi, ref r, style, estyle);
		WndUtil.CreateWindow(_WndProc, true, "Au.toolbar", _name, style, estyle, r.left, r.top, r.Width, r.Height, isSatelite ? owner : default);
		_created = true;
	}
	
	/// <summary>
	/// Destroys the toolbar window.
	/// </summary>
	/// <remarks>
	/// Can be called from any thread.
	/// Does nothing if not open.
	/// </remarks>
	public void Close() {
		if (_w.Is0) return;
		if (_IsOtherThread) _w.Post(Api.WM_CLOSE); else Api.DestroyWindow(_w);
		if (_hasCachedImages) IconImageCache.Common.Cleared -= _UpdateCachedImages;
	}
	
	/// <summary>
	/// When the toolbar window destroyed.
	/// </summary>
	public event Action Closed;
	
	private protected override void _WmNcdestroy() {
		_closed = true;
		_Manager.Remove(this);
		_SatDestroying();
		_sett.Dispose();
		base._WmNcdestroy();
		Closed?.Invoke();
	}
	
	/// <summary>
	/// Adds or removes a reason to temporarily hide the toolbar. The toolbar is hidden if at least one reason exists. See also <see cref="Close"/>.
	/// </summary>
	/// <param name="hide"><c>true</c> to hide (add <i>reason</i>), <c>false</c> to show (remove <i>reason</i>).</param>
	/// <param name="reason">A user-defined reason to hide/unhide. Can be <see cref="TBHide.User"/> or a bigger value, eg <c>(TBHide)0x20000</c>, <c>(TBHide)0x40000</c>.</param>
	/// <exception cref="InvalidOperationException">
	/// - The toolbar was never shown (<see cref="Show"/> not called).
	/// - It is a satellite toolbar.
	/// - Wrong thread. Must be called from the same thread that created the toolbar. See <see cref="MTBase.ActionThread"/>.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><i>reason</i> is less than <see cref="TBHide.User"/>.</exception>
	/// <remarks>
	/// Toolbars are automatically hidden when the owner window is hidden, minimized, etc. This function can be used to hide toolbars for other reasons.
	/// </remarks>
	public void Hide(bool hide, TBHide reason) {
		_ThreadTrap();
		if (!_created || _IsSatellite) throw new InvalidOperationException();
		if (0 != ((int)reason & 0xffff)) throw new ArgumentOutOfRangeException();
		_SetVisible(!hide, reason);
	}
	
	/// <summary>
	/// Gets current reasons why the toolbar is hidden. Returns 0 if not hidden.
	/// </summary>
	/// <remarks>
	/// Not used with satellite toolbars.
	/// </remarks>
	public TBHide Hidden => _hide;
	
	void _SetVisible(bool show, TBHide reason) {
		//print.it(show, reason);
		if (show) {
			if (_hide == 0) return;
			_hide &= ~reason;
			if (_hide != 0) return;
		} else {
			var h = _hide;
			_hide |= reason;
			if (h != 0) return;
		}
		_SetVisibleL(show);
	}
	TBHide _hide;
	
	void _SetVisibleL(bool show) => _w.ShowL(show);
	
	/// <summary>
	/// Returns <c>true</c> if the toolbar is attached to a window or an object in a window.
	/// </summary>
	public bool IsOwned => _ow != null;
	
	/// <summary>
	/// Returns the owner top-level window.
	/// Returns <c>default(wnd)</c> if the toolbar is not owned. See <see cref="IsOwned"/>.
	/// </summary>
	public wnd OwnerWindow => _ow?.w ?? default;
	
	#endregion
	
	#region wndproc, context menu
	
	static void _RegisterWinclass() {
		if (0 == Interlocked.Exchange(ref s_winclassRegistered, 1)) {
			WndUtil.RegisterWindowClass("Au.toolbar"/*, etc: new() { style = Api.CS_HREDRAW | Api.CS_VREDRAW, mCursor = MCursor.Arrow }*/);
		}
	}
	static int s_winclassRegistered;
	
	unsafe nint _WndProc(wnd w, int msg, nint wParam, nint lParam) {
		//WndUtil.PrintMsg(w, msg, wParam, lParam);
		
		switch (msg) {
		case Api.WM_LBUTTONDOWN:
		case Api.WM_RBUTTONDOWN:
		case Api.WM_MBUTTONDOWN:
			var tb1 = _SatPlanetOrThis;
			if (tb1.IsOwned && tb1.MiscFlags.Has(TBFlags.ActivateOwnerWindow)) {
				tb1.OwnerWindow.ActivateL();
				//never mind: sometimes flickers. Here tb1._Zorder() does not help. The OBJECT_REORDER hook zorders when need. This feature is rarely used.
			}
			break;
		case Api.WM_MOUSEMOVE:
		case Api.WM_NCMOUSEMOVE:
			_SatMouse();
			break;
		}
		
		switch (msg) {
		case Api.WM_NCCREATE:
			_WmNccreate(w);
			if (_transparency != default) _w.SetTransparency(true, _transparency.opacity, _transparency.colorKey);
			break;
		case Api.WM_NCDESTROY:
			_WmNcdestroy();
			//PROBLEM: not called if thread ends without closing the toolbar window.
			//	Then saves settings only on process exit. Not if process terminated, eg by the 'end task' command.
			//	In most cases it isn't a problem because saves settings every 2 s (IIRC).
			//	SHOULDDO: the 'end task' command should be more intelligent. At least save settings.
			break;
		case Api.WM_ERASEBKGND:
			return 0;
		case Api.WM_PAINT:
			using (BufferedPaint bp = new(w, true)) {
				var dc = bp.DC;
				using var g = System.Drawing.Graphics.FromHdc(dc);
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				_WmPaint(dc, g, bp.Rect, bp.UpdateRect);
			}
			return default;
		case Api.WM_MOUSEACTIVATE:
			return Api.MA_NOACTIVATE;
		case Api.WM_MOUSEMOVE:
			_WmMousemove(lParam);
			return default;
		case Api.WM_MOUSELEAVE:
			_WmMouseleave();
			return default;
		case Api.WM_LBUTTONDOWN:
			_WmMouselbuttondown(lParam);
			return default;
		case Api.WM_CONTEXTMENU:
		case Api.WM_NCRBUTTONUP:
			_WmContextmenu();
			break;
		case Api.WM_NCHITTEST:
			if (_WmNchittest(lParam, out int hitTest)) return hitTest; //returns a hittest code to move or resize if need
			break;
		case Api.WM_NCLBUTTONDOWN:
			int ht = (int)wParam;
			if (ht == Api.HTCAPTION || (ht >= Api.HTSIZEFIRST && ht <= Api.HTSIZELAST)) {
				//workaround for: Windows tries to activate this window when moving or sizing it, unless this process is not allowed to activate windows.
				//	This window then may not become the foreground window, but it receives wm_activateapp, wm_activate, wm_setfocus, and is moved to the top of Z order.
				//	tested: LockSetForegroundWindow does not work.
				//	This code better would be under WM_SYSCOMMAND, but then works only when sizing. When moving, activates before WM_SYSCOMMAND.
				using (WindowsHook.ThreadCbt(d => d.code == HookData.CbtEvent.ACTIVATE)) {
					//also let arrow keys move/resize by 1 pixel.
					//	In active window Ctrl+arrow work automatically, but toolbars aren't active.
					//	Never mind: does not work if active window has higher UAC IL. Even with Ctrl.
					using var k1 = new RegisteredHotkey(); k1.Register(100, KKey.Left, _w);
					using var k2 = new RegisteredHotkey(); k2.Register(101, KKey.Right, _w);
					using var k3 = new RegisteredHotkey(); k3.Register(102, KKey.Up, _w);
					using var k4 = new RegisteredHotkey(); k4.Register(103, KKey.Down, _w);
					
					Api.DefWindowProc(w, msg, wParam, lParam);
				}
				return default;
			}
			break;
		case Api.WM_ENTERSIZEMOVE:
			_SetInMoveSize(true);
			break;
		case Api.WM_EXITSIZEMOVE:
			_SetInMoveSize(false);
			break;
		case Api.WM_WINDOWPOSCHANGING:
			_WmWindowPosChanging(ref *(Api.WINDOWPOS*)lParam);
			break;
		case RegisteredHotkey.WM_HOTKEY:
			int hkid = (int)wParam - 100;
			if (hkid >= 0 && hkid <= 3) {
				POINT p = mouse.xy;
				Api.SetCursorPos(p.x + hkid switch { 0 => -1, 1 => 1, _ => 0 }, p.y + hkid switch { 2 => -1, 3 => 1, _ => 0 });
				return default;
			}
			break;
		case Api.WM_GETOBJECT:
			if (_WmGetobject(wParam, lParam, out var r1)) return r1;
			break;
		case Api.WM_USER + 50: //posted by acc dodefaultaction
			if (!_closed) {
				int i = (int)wParam;
				if ((uint)i < _a.Count) _Click(i, false);
			}
			return default;
		}
		
		var R = Api.DefWindowProc(w, msg, wParam, lParam);
		
		switch (msg) {
		case Api.WM_WINDOWPOSCHANGED:
			_WmWindowPosChanged(in *(Api.WINDOWPOS*)lParam);
			break;
		case Api.WM_GETDPISCALEDSIZE:
			return _WmGetDpiScaledSize(wParam, lParam);
		case Api.WM_DPICHANGED:
			_WmDpiChanged(wParam, lParam);
			break;
		case Api.WM_DISPLAYCHANGE:
			_WmDisplayChange();
			break;
			//case Api.WM_SETTINGCHANGE:
			//	_WmSettingChange(wParam, lParam);
			//	break;
		}
		
		return R;
	}
	
	#endregion
	
	#region input, context menu
	
	int _HitTest(POINT p) {
		for (int i = 0; i < _a.Count; i++) {
			if (_a[i].rect.Contains(p)) return i;
		}
		return -1;
	}
	
	unsafe void _WmMousemove(nint lParam) {
		if (_iClick < 0) {
			var p = Math2.NintToPOINT(lParam);
			int i = _HitTest(p);
			if (i != _iHot) {
				if (_iHot >= 0) _Invalidate(_iHot);
				if ((_iHot = i) >= 0) {
					_Invalidate(_iHot);
					var b = _a[i];
					_SetTooltip(b, b.rect, lParam);
				}
			}
			if (_iHot >= 0 != _trackMouseEvent) _trackMouseEvent = Api.TrackMouseLeave(_w, _iHot >= 0) && _iHot >= 0;
		}
	}
	int _iHot = -1, _iClick = -1;
	bool _trackMouseEvent, _noHotClick;
	
	void _WmMouseleave() {
		_trackMouseEvent = false;
		if (_iHot >= 0) { _Invalidate(_iHot); _iHot = -1; }
	}
	
	void _WmMouselbuttondown(nint lParam) {
		var mod = keys.gui.getMod(); if (mod != 0 && mod != KMod.Shift) return;
		if (mod == 0) { //click button
			var p = Math2.NintToPOINT(lParam);
			int i = _HitTest(p);
			if (i >= 0) _Click(i, true);
			//} else if(mod==KMod.Shift) { //move toolbar
			//	var p=mouse.xy;
			//	DragDropUtil.SimpleDragDrop(_w, MButtons.Left, d => {
			//		if (d.Msg.message != Api.WM_MOUSEMOVE) return;
			//		var v=mouse.xy; if(v==p) return;
			//		int dx=v.x-p.x, dy=v.y-p.y;
			//		p=v;
			//		var r=_w.Rect;
			//		_w.MoveL(r.left+dx, r.top+dy);
			//	});
		}
	}
	int _menuClosedIndex; long _menuClosedTime;
	
	void _Click(int i, bool real) {
		var b = _a[i];
		if (b.clicked == null) return;
		if (b.IsMenu_) {
			if (i == _menuClosedIndex && perf.ms - _menuClosedTime < 100) return;
			popupMenu m = null;
			if (b.clicked is Action<popupMenu> menu) {
				m = new popupMenu(null, NoContextMenu.Has(TBNoMenu.Edit) ? null : b.sourceFile, b.sourceLine);
				_CopyProps(m);
				menu(m);
			} else if (b.clicked is Func<popupMenu> func) {
				m = func();
			}
			if (m == null) return;
			
			var r = b.rect; _w.MapClientToScreen(ref r);
			m.Show(PMFlags.AlignRectBottomTop, new(r.left, r.bottom), r, owner: _w);
			_menuClosedIndex = i; _menuClosedTime = perf.ms;
			//info: before wm_lbuttondown the menu is already closed and its message loop ended. Previous Show returns before new Show.
		} else {
			if (real) {
				bool ok = false;
				try {
					_Invalidate(_iClick = i);
					ok = WndUtil.DragLoop(_w, MButtons.Left, d => {
						if (d.msg.message != Api.WM_MOUSEMOVE) return;
						int j = _HitTest(Math2.NintToPOINT(d.msg.lParam));
						if ((j == i) == _noHotClick) {
							_noHotClick ^= true;
							_Invalidate(i);
						}
					}) && !_noHotClick;
				}
				finally {
					_iClick = -1; _noHotClick = false;
					_Invalidate(i);
				}
				if (!ok) return;
			}
			//print.it("click", b);
			if (b.actionThread) run.thread(() => _ExecItem(), background: false); //thread start speed: 250 mcs
			else _ExecItem();
			void _ExecItem() {
				var action = b.clicked as Action<TBItem>;
				try { action(b); }
				catch (Exception ex) when (!b.actionException) { print.warning(ex.ToString(), -1); }
			}
		}
	}
	
	void _WmContextmenu() {
		var no = NoContextMenu;
		if (no.Has(TBNoMenu.Menu)) return;
		if (_a.Count == 0 && _satellite != null) no |= TBNoMenu.Layout | TBNoMenu.AutoSize | TBNoMenu.Text;
		
		var p = _w.MouseClientXY;
		int i = _HitTest(p);
		var item = i < 0 ? null : _a[i];
		
		var m = new popupMenu();
		
		if (!no.Has(TBNoMenu.Edit | TBNoMenu.File)) {
			string sf; int sl; if (item != null) { sf = item.sourceFile; sl = item.sourceLine; } else { sf = _sourceFile; sl = _sourceLine; }
			var (canEdit, canGo, goText) = MTItem.CanEditOrGoToFile_(sf, item);
			if (!no.Has(TBNoMenu.Edit) && canEdit) m["Edit toolbar"] = o => ScriptEditor.Open(sf, sl);
			if (!no.Has(TBNoMenu.File) && canGo) m[goText] = o => item.GoToFile_();
		}
		if (!no.Has(TBNoMenu.Close)) m.Add("Close", o => _SatPlanetOrThis.Close());
		if (m.Last != null) m.Separator();
		
		if (!no.Has(TBNoMenu.Anchor)) {
			m.Submenu("Anchor", m => {
				_AddAnchor(TBAnchor.TopLeft);
				_AddAnchor(TBAnchor.TopRight);
				_AddAnchor(TBAnchor.BottomLeft);
				_AddAnchor(TBAnchor.BottomRight);
				_AddAnchor(TBAnchor.TopLR);
				_AddAnchor(TBAnchor.BottomLR);
				_AddAnchor(TBAnchor.LeftTB);
				_AddAnchor(TBAnchor.RightTB);
				_AddAnchor(TBAnchor.All);
				m.Separator();
				_AddAnchor(TBAnchor.OppositeEdgeX);
				_AddAnchor(TBAnchor.OppositeEdgeY);
				if (IsOwned) _AddAnchor(TBAnchor.Screen);
				
				void _AddAnchor(TBAnchor an) {
					var k = an <= TBAnchor.All
						? m.AddRadio(an.ToString(), Anchor.WithoutFlags() == an, _ => Anchor = (Anchor & ~TBAnchor.All) | an)
						: m.AddCheck(an.ToString(), Anchor.Has(an), _ => Anchor ^= an, disable: _GetInvalidAnchorFlags(Anchor).Has(an));
					if (_IsSatellite) k.Tooltip = "Note: You may want to set anchor of the owner toolbar instead. Anchor of this auto-hide toolbar is relative to the owner toolbar.";
				}
			});
		}
		if (!no.Has(TBNoMenu.Layout)) {
			m.Submenu("Layout", m => {
				_AddLayout(TBLayout.HorizontalWrap);
				_AddLayout(TBLayout.Vertical);
				//_AddLayout(TBLayout.Horizontal);
				
				void _AddLayout(TBLayout tl) {
					m.AddRadio(tl.ToString(), tl == Layout, _ => Layout = tl);
				}
			});
		}
		if (!no.Has(TBNoMenu.Border)) {
			m.Submenu("Border", m => {
				_AddBorder(TBBorder.None);
				_AddBorder(TBBorder.Width1);
				_AddBorder(TBBorder.Width2);
				_AddBorder(TBBorder.Width3);
				_AddBorder(TBBorder.Width4);
				_AddBorder(TBBorder.ThreeD);
				_AddBorder(TBBorder.Thick);
				_AddBorder(TBBorder.Caption);
				_AddBorder(TBBorder.CaptionX);
				
				void _AddBorder(TBBorder b) {
					m.AddRadio(b.ToString(), b == Border, _ => Border = b);
				}
			});
		}
		if (!no.Has(TBNoMenu.Sizable | TBNoMenu.AutoSize | TBNoMenu.Text | TBNoMenu.MiscFlags)) {
			m.Submenu("More", m => {
				if (!no.Has(TBNoMenu.Sizable)) m.AddCheck("Sizable", Sizable, _ => Sizable ^= true);
				if (!no.Has(TBNoMenu.AutoSize)) m.AddCheck("Auto-size", AutoSize, _ => AutoSize ^= true);
				if (!no.Has(TBNoMenu.Text)) m.AddCheck("Display text", DisplayText, _ => DisplayText ^= true);
				if (!no.Has(TBNoMenu.MiscFlags)) {
					_AddFlag(TBFlags.HideWhenFullScreen);
					if (_SatPlanetOrThis.IsOwned) _AddFlag(TBFlags.ActivateOwnerWindow);
					
					void _AddFlag(TBFlags f) {
						var tb = _SatPlanetOrThis;
						m.AddCheck(_EnumToString(f), tb.MiscFlags.Has(f), _ => tb.MiscFlags ^= f);
					}
					
					static string _EnumToString(Enum e) {
						var s = e.ToString().RxReplace(@"(?<=[^A-Z])[A-Z]", m => " " + m.Value.Lower());
						//s = s.Replace("Dont", "Don't");
						return s;
					}
				}
			});
		}
		
		if (!no.Has(TBNoMenu.Toolbars | TBNoMenu.Help) && m.Last != null && !m.Last.IsSeparator) m.Separator();
		if (!no.Has(TBNoMenu.Toolbars)) m.Add("Toolbars", o => toolbarsDialog());
		if (!no.Has(TBNoMenu.Help)) m["How to"] = _ => dialog.showInfo("How to",
@"Move toolbar: Shift+drag.

Resize toolbar: drag border. Cannot resize if in context menu is unchecked or unavailable More -> Sizable; or if checked Border -> None.

Move or resize precisely: start to move or resize but don't move the mouse. Instead release Shift and press arrow keys. Finally release the mouse button.
");
		
		if (m.Last != null) m.Show();
	}
	
	bool _WmNchittest(nint xy, out int ht) {
		ht = 0;
		if (keys.gui.getMod() == KMod.Shift) { //move
			ht = Api.HTCAPTION;
		} else { //resize?
			if (Border == TBBorder.None || (!Sizable && Border < TBBorder.Thick)) return false;
			var (x, y) = Math2.NintToPOINT(xy);
			if (Sizable) {
				_w.GetWindowInfo_(out var k);
				RECT r = k.rcWindow;
				int b = Border >= TBBorder.ThreeD ? k.cxWindowBorders : (_a.Count > 0 ? _BorderPadding() : Dpi.Scale(6, _dpi)); //make bigger if no buttons. Eg if auto-hide-at-screen-edge, border 1 is difficult to resize.
				int bx = Math.Min(b, r.Width / 2), by = Math.Min(b, r.Height / 2);
				if (bx == 0) bx = 1;
				if (by == 0) by = 1;
				//print.it(bx, by);
				int x1 = r.left + bx, x2 = --r.right - bx, y1 = r.top + by, y2 = --r.bottom - by;
				if (r.Width > bx * 8 && r.Height > by * 8) { //if toolbar isn't small, in corners allow to resize both width and height at the same time
					if (x < x1) ht = y < y1 ? Api.HTTOPLEFT : (y > y2 ? Api.HTBOTTOMLEFT : Api.HTLEFT);
					else if (x > x2) ht = y < y1 ? Api.HTTOPRIGHT : (y > y2 ? Api.HTBOTTOMRIGHT : Api.HTRIGHT);
					else if (y < y1) ht = Api.HTTOP;
					else if (y > y2) ht = Api.HTBOTTOM;
					else return false;
				} else if (r.Width >= r.Height) { //in corners prefer width
					if (x < x1) ht = Api.HTLEFT;
					else if (x > x2) ht = Api.HTRIGHT;
					else if (y < y1) ht = Api.HTTOP;
					else if (y > y2) ht = Api.HTBOTTOM;
					else return false;
				} else { //in corners prefer height
					if (y < y1) ht = Api.HTTOP;
					else if (y > y2) ht = Api.HTBOTTOM;
					else if (x < x1) ht = Api.HTLEFT;
					else if (x > x2) ht = Api.HTRIGHT;
					else return false;
				}
			} else { //disable resizing if border is natively sizable
				if (Border < TBBorder.Thick) return false;
				_w.GetWindowInfo_(out var k);
				k.rcWindow.Inflate(-k.cxWindowBorders, -k.cyWindowBorders);
				if (k.rcWindow.Contains(x, y)) return false;
				ht = Api.HTBORDER;
			}
		}
		return true;
	}
	
	#endregion
	
	#region properties
	
	/// <summary>
	/// Whether to DPI-scale toolbar size and offsets.
	/// Default: scale size; scale offsets if anchor is not screen.
	/// </summary>
	/// <remarks>
	/// The unit of measurement of <see cref="Size"/>, <see cref="Offsets"/> and some other properties depends on whether scaling is used for that property. If scaling is used, the unit is logical pixels; it is 1/96 inch regardless of screen DPI. If scaling not used, the unit is physical pixels. Screen DPI can be changed in Windows Settings; when it is 100%, logical and physical pixels are equal.
	/// </remarks>
	public TBScaling DpiScaling { get; set; }
	
	/// <summary>
	/// Toolbar width and height without non-client area when <see cref="AutoSize"/> <c>false</c>.
	/// </summary>
	/// <remarks>
	/// Non-client area is border and caption when <see cref="Border"/> is <b>ThreeD</b>, <b>Thick</b>, <b>Caption</b> or <b>CaptionX</b>.
	/// 
	/// The unit of measurement depends on <see cref="DpiScaling"/>.
	/// 
	/// This property is updated when resizing the toolbar. It is saved.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// t.Size = new(300, 40);
	/// ]]></code>
	/// </example>
	public System.Windows.Size Size {
		get => _sett.size;
		set {
			_ThreadTrap();
			if (value != _sett.size) {
				value = new(_Limit(value.Width), _Limit(value.Height));
				_sett.size = value;
				if (IsOpen && !AutoSize) _Resize(_Scale(value));
			}
			if (!_followedOnce) _preferSize = true;
		}
	}
	
	/// <summary>
	/// Whether the border can be used to resize the toolbar.
	/// Default <c>true</c>.
	/// </summary>
	/// <remarks>
	/// This property is in the context menu and is saved.
	/// </remarks>
	public bool Sizable {
		get => _sett.sizable;
		set => _sett.sizable = value;
	}
	
	/// <summary>
	/// Automatically resize the toolbar to make all buttons visible.
	/// Default <c>true</c>.
	/// </summary>
	/// <remarks>
	/// This property is in the context menu and is saved.
	/// </remarks>
	public bool AutoSize {
		get => _sett.autoSize;
		set {
			_ThreadTrap();
			if (value != _sett.autoSize) {
				_sett.autoSize = value;
				_AutoSizeNow();
			}
		}
	}
	
	/// <summary>
	/// When <see cref="AutoSize"/> is <c>true</c>, this is the preferred width at which buttons are moved to the next row. Unlimited if 0.
	/// </summary>
	/// <remarks>
	/// The unit of measurement depends on <see cref="DpiScaling"/>.
	/// 
	/// This property is updated when the user resizes the toolbar while <see cref="AutoSize"/> is <c>true</c>. It is saved.
	/// 
	/// If layout of this toolbar is vertical, just sets max width.
	/// </remarks>
	public double AutoSizeWrapWidth {
		get => _sett.wrapWidth;
		set {
			_ThreadTrap();
			value = value > 0 ? _Limit(value) : 0;
			if (value != _sett.wrapWidth) {
				_sett.wrapWidth = value;
				_AutoSizeNow();
			}
		}
	}
	
	/// <summary>
	/// Specifies to which owner's edges the toolbar keeps constant distance when moving or resizing the owner.
	/// </summary>
	/// <remarks>
	/// The owner can be a window, screen, control or other object. It is specified when calling <see cref="Show"/>.
	/// This property is in the context menu and is saved.
	/// </remarks>
	/// <seealso cref="Offsets"/>
	public TBAnchor Anchor {
		get => _sett.anchor;
		set {
			_ThreadTrap();
			value &= ~_GetInvalidAnchorFlags(value);
			if (value.WithoutFlags() == 0) value |= TBAnchor.TopLeft;
			if (value == _sett.anchor) return;
			var prev = _sett.anchor;
			_sett.anchor = value;
			if (IsOwned) {
				_os = value.OfScreen() ? new _OwnerScreen(this, default) : null; //follow toolbar's screen
				if (prev.OfScreen() && _followedOnce) {
					if (_oc != null) _oc.UpdateRect(out _); else _ow.UpdateRect(out _);
				}
			}
			if (_followedOnce) {
				var r = _w.Rect;
				_UpdateOffsets(r.left, r.top, r.Width, r.Height);
			}
		}
	}
	
	/// <summary>
	/// Specifies distances between edges of the toolbar and edges of its owner, depending on <see cref="Anchor"/>.
	/// </summary>
	/// <remarks>
	/// Owner is specified when calling <see cref="Show"/>. It can be a window, screen, control or other object.
	/// 
	/// The <see cref="TBOffsets"/> type has 4 properties - <b>Top</b>, <b>Bottom</b>, <b>Left</b> and <b>Right</b>, but used are only those included in <see cref="Anchor"/>. For example, if <b>Anchor</b> is <b>TopLeft</b>, used are only <b>Top</b> and <b>Left</b>.
	/// 
	/// The unit of measurement depends on <see cref="DpiScaling"/> and whether anchor is screen.
	/// 
	/// This property is updated when moving or resizing the toolbar. It is saved.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// t.Offsets = new(150, 5, 0, 0);
	/// ]]></code>
	/// </example>
	public TBOffsets Offsets {
		get => _offsets;
		set {
			_ThreadTrap();
			_preferSize = false;
			if (value == _offsets) return;
			_sett.offsets = _offsets = value;
			if (_followedOnce) _FollowRect();
		}
	}
	TBOffsets _offsets;
	
	//rejected. Would be rarely used, unless default 0. Avoid default limitations like this. We have a dialog to find lost toolbars.
	//public int MaxDistanceFromOwner { get; set; } = int.MaxValue;
	
	//rejected
	//public bool HideTextIfSmall { get; set; } //like ribbon UI
	
	/// <summary>
	/// Miscellaneous options.
	/// </summary>
	/// <remarks>
	/// This property is in the context menu (submenu "More") and is saved.
	/// </remarks>
	public TBFlags MiscFlags {
		get => _sett.miscFlags;
		set {
			_sett.miscFlags = value;
		}
	}
	
	/// <summary>
	/// Opacity and transparent color.
	/// </summary>
	/// <seealso cref="wnd.SetTransparency(bool, int?, ColorInt?, bool)"/>
	/// <example>
	/// <code><![CDATA[
	/// t.Transparency = (64, null);
	/// ]]></code>
	/// </example>
	public (int? opacity, ColorInt? colorKey) Transparency {
		get => _transparency;
		set {
			if (value != _transparency) {
				_transparency = value;
				if (_created) _w.SetTransparency(value != default, value.opacity, value.colorKey);
			}
		}
	}
	(int? opacity, ColorInt? colorKey) _transparency;
	
	/// <summary>
	/// Gets or sets flags to hide some context menu items or menu itself.
	/// </summary>
	public TBNoMenu NoContextMenu { get; set; }
	
	#endregion
}
