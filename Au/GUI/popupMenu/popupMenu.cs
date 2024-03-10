//SHOULDDO: winevents EEvent.SYSTEM_MENUSTART, EEvent.SYSTEM_MENUEND, EEvent.SYSTEM_MENUPOPUPSTART, EEvent.SYSTEM_MENUPOPUPEND

namespace Au;

/// <summary>
/// Popup menu.
/// </summary>
/// <remarks>
/// Can be used everywhere: in automation scripts, WPF apps, other apps, etc.
/// Also can be used as a popup list and supports many items with scrollbar.
/// 
/// Menu item text can include hotkey after <c>'\t'</c> character and/or tooltip after <c>'|'</c> or <c>'\0'</c> character. Examples: <c>"Text\t Hotkey"</c>, <c>"Text|Tooltip"</c>, <c>"Text\t Hotkey\0 Tooltip"</c>. Character with prefix <c>&amp;</c> (eg <c>'A'</c> in <c>"Save &amp;As"</c>) will be underlined (depends on Windows settings and <see cref="PMFlags"/>) and can be used to select the item with keyboard.
/// 
/// Keyboard, mouse:
/// - <c>Enter</c>, <c>Tab</c>, <c>Space</c> - close the menu and execute the focused item. Or show the submenu.
/// - <c>Esc</c> - close the menu or current submenu.
/// - <c>Left</c> - close current submenu.
/// - <c>Right</c> - open submenu.
/// - <c>Down</c>, <c>Up</c>, <c>PageDown</c>, <c>PageUp</c>, <c>End</c>, <c>Home</c> - focus other item.
/// - underlined menu item character - close the menu and execute the item. Or show the submenu. See <see cref="PMFlags.Underline"/>.
/// - <c>Alt</c>, <c>Win</c>, <c>F10</c>, <c>Apps</c>, <c>Back</c> - close menus.
/// - click outside - close the menu.
/// - middle click - close the menu.
/// - right click - show context menu (if used constructor with parameters).
/// 
/// While a menu is open, it captures many keyboard keys, even when its thread isn't the foreground thread.
/// 
/// Not thread-safe. All functions must be called in same thread, unless documented otherwise.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// var m = new popupMenu("example");
/// m["One"] = o => print.it(o);
/// m["Two\0Tooltip", image: icon.stock(StockIcon.DELETE)] = o => { print.it(o); dialog.show(o.ToString()); };
/// m.Submenu("Submenu", m => {
/// 	m["Three"] = o => print.it(o);
/// 	m["Four"] = o => print.it(o);
/// });
/// m["notepad"] = o => run.itSafe(folders.System + "notepad.exe");
/// m.Show();
/// ]]></code>
/// </example>
public unsafe partial class popupMenu : MTBase {
	readonly List<PMItem> _a = new();
	int _lastId; //to auto-generate item ids
	bool _addedNewItems;
	(SIZE window, SIZE client, int border) _size;
	NativeScrollbar_ _scroll;
	(popupMenu child, popupMenu parent, PMItem item, timer timer) _sub;
	(POINT p, bool track, bool left, bool right, bool middle) _mouse;
	int _iHot = -1;
	PMFlags _flags;
	PMItem _result;

	static popupMenu() {
		WndUtil.RegisterWindowClass("Au.popupMenu", etc: new() { style = Api.CS_HREDRAW | Api.CS_VREDRAW | Api.CS_DROPSHADOW, mCursor = MCursor.Arrow });
	}

	/// <summary>
	/// Use this constructor for various context menus of your app.
	/// </summary>
	/// <remarks>
	/// Users cannot right-click a menu item and open/select it in editor.
	/// </remarks>
	public popupMenu() { }

	/// <summary>
	/// Use this constructor in scripts.
	/// </summary>
	/// <param name="name">Menu name. Must be a unique valid filename. Currently not used. Can be <c>null</c>.</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <remarks>
	/// This overload sets <see cref="MTBase.ExtractIconPathFromCode"/> = <c>true</c>.
	/// 
	/// Users can right-click an item to open/select it in editor, unless <i>f_</i> is explicitly set = <c>null</c>.
	/// </remarks>
	public popupMenu(string name, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0) : base(name, f_, l_) {
		ExtractIconPathFromCode = true;
	}

	#region add

	PMItem _Add(PMItem mi, string text, MTImage image, int l_, string f_, Delegate click = null) {
		_ThreadTrap();
		_OpenTrap("cannot add items while the menu is open. To add to submenu, use the submenu variable.");
		if (!mi.IsSeparator) mi.Set_(this, text, click, image, l_, _sourceFile == null ? null : f_);
		_a.Add(mi);
		_addedNewItems = true;
		Last = mi;
		return mi;
	}

	/// <summary>
	/// Adds menu item with explicitly specified id.
	/// </summary>
	/// <param name="id">Item id that <see cref="Show"/> will return if clicked this item.</param>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	public PMItem Add(int id, string text, MTImage image = default, bool disable = false, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null)
		=> _Add(new PMItem(this, disable) { Id = _lastId = id }, text, image, l_, f_);

	/// <summary>
	/// Adds menu item with auto-generated id.
	/// </summary>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <remarks>
	/// Assigns id = the last specified or auto-generated id + 1. If not using explicitly specified ids, auto-generated ids are 1, 2, 3... Submenu-items, separators and items with action don't auto-generate ids.
	/// </remarks>
	public PMItem Add(string text, MTImage image = default, bool disable = false, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null)
		=> _Add(new PMItem(this, disable) { Id = ++_lastId }, text, image, l_, f_);

	/// <summary>
	/// Adds menu item with action (callback function) that is executed on click.
	/// </summary>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="click">Action executed on click.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <remarks>
	/// This function is the same as the indexer. The difference is, <b>Add</b> returns <b>PMItem</b> object of the added item. When using the indexer, to access the item use <see cref="Last"/>. These codes are the same: <c>var v=m.Add("text", o=>{});"</c> and <c>m["text"]=o=>{}; var v=m.Last;</c>.
	/// </remarks>
	public PMItem Add(string text, Action<PMItem> click, MTImage image = default, bool disable = false, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null)
		=> _Add(new PMItem(this, disable), text, image, l_, f_, click);

	/// <summary>
	/// Adds menu item with action (callback function) that is executed on click.
	/// </summary>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <value>Action executed on click. Can be <c>null</c>.</value>
	/// <remarks>
	/// This function is the same as <see cref="Add(string, Action{PMItem}, MTImage, bool, int, string)"/>. The difference is, <b>Add</b> returns <b>PMItem</b> object of the added item. When using the indexer, to access the item use <see cref="Last"/>. These codes are the same: <c>var v=m.Add("text", o=>{});"</c> and <c>m["text"]=o=>{}; var v=m.Last;</c>.
	/// </remarks>
	public Action<PMItem> this[string text, MTImage image = default, bool disable = false, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null] {
		set { Add(text, value, image, disable, l_, f_); }
	}

	/// <summary>
	/// Adds menu item to be used as a checkbox.
	/// </summary>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="check">Checked state.</param>
	/// <param name="click">Action executed on click.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <remarks>
	/// When clicked, <see cref="PMItem.IsChecked"/> state is changed.
	/// </remarks>
	public PMItem AddCheck(string text, bool check = false, Action<PMItem> click = null, bool disable = false, MTImage image = default, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null)
		=> _Add(new PMItem(this, disable, check) { checkType = 1 }, text, image, l_, f_, click);

	/// <summary>
	/// Adds menu item to be used as a radio button in a group of such items.
	/// </summary>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="check">Checked state.</param>
	/// <param name="click">Action executed on click.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <remarks>
	/// When clicked an unchecked radio item, its <see cref="PMItem.IsChecked"/> state becomes <c>true</c>; <b>IsChecked</b> of other group items become <c>false</c>.
	/// </remarks>
	public PMItem AddRadio(string text, bool check = false, Action<PMItem> click = null, bool disable = false, MTImage image = default, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null)
		=> _Add(new PMItem(this, disable, check) { checkType = 2 }, text, image, l_, f_, click);

	/// <summary>
	/// Adds menu item that opens a submenu.
	/// Used like <c>m.Submenu("Example", m => { /* add submenu items */ });</c>.
	/// </summary>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="opening">Action called whenever opening the submenu and should add items to it.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <remarks>
	/// The submenu is other <b>popupMenu</b> object. It inherits many properties of this menu; see property documentation.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// m.Submenu("Example", m => {
	/// 	m["A"] = o => { print.it(o); };
	/// 	m["B"] = o => { print.it(o); };
	/// });
	/// ]]></code>
	/// This code shows dynamically created menu of files in a folder and subfolders. Subfolder files are retrieved when opening the submenu.
	/// <code><![CDATA[
	/// var m=new popupMenu();
	/// _Dir(m, new DirectoryInfo(@"C:\"));
	/// m.Show();
	/// 
	/// static void _Dir(popupMenu m, DirectoryInfo dir) {
	/// 	foreach (var v in dir.EnumerateFileSystemInfos()) {
	/// 		if(v.Attributes.Has(FileAttributes.System|FileAttributes.Hidden)) continue;
	/// 		if(v.Attributes.Has(FileAttributes.Directory)) {
	/// 			m.Submenu(v.Name, m=> _Dir(m, v as DirectoryInfo));
	/// 		} else {
	/// 			m[v.Name]=o=>print.it(v.FullName);
	/// 		}
	/// 		m.Last.File = v.FullName;
	/// 	}
	/// }
	/// ]]></code>
	/// </example>
	public PMItem Submenu(string text, Action<popupMenu> opening, MTImage image = default, bool disable = false, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null)
		=> _Add(new PMItem(this, disable) { IsSubmenu = true }, text, image, l_, f_, opening);

	/// <summary>
	/// Adds menu item that opens a reusable submenu.
	/// </summary>
	/// <param name="text">Item text. Can include hotkey, tooltip and underlined character, like <c>"Te&amp;xt\t Hotkey\0 Tooltip"</c>; more info: <see cref="popupMenu"/>.</param>
	/// <param name="opening">Func called whenever opening the submenu and should return the submenu object. Can return <c>null</c>.</param>
	/// <param name="image">Item image. Read here: <see cref="MTBase"/>.</param>
	/// <param name="disable">Disabled state.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <remarks>
	/// The caller creates the submenu (creates the <see cref="popupMenu"/> object and adds items) and can reuse it many times. Other overload does not allow to create <b>popupMenu</b> and reuse same object.
	/// The submenu does not inherit properties of this menu.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var m2 = new popupMenu(); m2.AddCheck("C1"); m2.AddCheck("C2");
	/// m.Submenu("Submenu", () => m2);
	/// ]]></code>
	/// </example>
	public PMItem Submenu(string text, Func<popupMenu> opening, MTImage image = default, bool disable = false, [CallerLineNumber] int l_ = 0, [CallerFilePath] string f_ = null)
		=> _Add(new PMItem(this, disable) { IsSubmenu = true }, text, image, l_, f_, opening);

	/// <summary>
	/// Adds separator.
	/// </summary>
	public void Separator()
		=> _Add(new PMItem(this, isDisabled: true) { IsSeparator = true }, null, default, 0, null);

	/// <summary>
	/// Gets the last added menu item.
	/// </summary>
	public PMItem Last { get; private set; }

	/// <summary>
	/// Gets added items, except separators and items in submenus.
	/// </summary>
	/// <remarks>
	/// Allows to set properties of multiple items in single place instead of after each "add item" code line.
	/// 
	/// Does not get items in submenus. Submenus are separate <b>popupMenu</b> objects and you can use their <b>Items</b> property.
	/// </remarks>
	public IEnumerable<PMItem> Items {
		get {
			_ThreadTrap();
			foreach (var v in _a) {
				if (!v.IsSeparator) yield return v;
			}
		}
	}

	/// <summary>
	/// Gets added items and separators, except items in submenus.
	/// </summary>
	public IReadOnlyList<PMItem> ItemsAndSeparators {
		get {
			_ThreadTrap();
			return _a;
		}
	}

	/// <summary>
	/// Don't use: <c>&amp;</c> character for keyboard shortcut; tab character for hotkey; <c>|</c> character for tooltip (but use <c>\0</c>).
	/// This property is applied to items added afterwards; submenus inherit it.
	/// </summary>
	public bool RawText { get; set; }

	/// <summary>
	/// Adds enum members as checkbox-items (if it's a <c>[Flags]</c> enum) or radio-items.
	/// </summary>
	/// <returns>Object for getting result later. See <see cref="EnumUI{TEnum}.Result"/>.</returns>
	/// <param name="init">Initial value.</param>
	/// <param name="items">Enum members and their text/tooltip. Optional. Text can be: <c>null</c>, <c>"text"</c>, <c>"text|tooltip"</c>, <c>"|tooltip"</c>.</param>
	/// <example>
	/// <code><![CDATA[
	/// var m = new popupMenu();
	/// var e1 = m.AddEnum<KMod>(KMod.Ctrl|KMod.Alt); //a [Flags] enum
	/// m.Separator();
	/// var e2 = m.AddEnum<DayOfWeek>(DateTime.Today.DayOfWeek); //a non-[Flags] enum
	/// m.Show();
	/// print.it(e1.Result);
	/// print.it(e2.Result);
	/// ]]></code>
	/// </example>
	/// <seealso cref="EnumUI{TEnum}"/>
	public EnumUI<TEnum> AddEnum<TEnum>(TEnum init = default, (TEnum value, string text)[] items = null) where TEnum : unmanaged, Enum {
		return new EnumUI<TEnum>(this, init, items);
	}

	#endregion

	#region show, close

	/// <summary>
	/// Shows the menu and waits until closed.
	/// </summary>
	/// <returns>
	/// id of the selected item, or 0 if canceled.
	/// See also: <see cref="Result"/>.
	/// </returns>
	/// <param name="flags"></param>
	/// <param name="xy">Menu position in screen. If <c>null</c> (default), uses mouse position by default. It depends on <i>flags</i>.</param>
	/// <param name="excludeRect">The menu should not overlap this rectangle in screen.</param>
	/// <param name="owner">Owner window. The menu will be automatically closed when destroying its owner window.</param>
	/// <exception cref="InvalidOperationException">The menu is open or is submenu.</exception>
	public int Show(PMFlags flags = 0, POINT? xy = null, RECT? excludeRect = null, AnyWnd owner = default) {
		_ThreadTrap();
		_OpenTrap("this menu is already open");
		if (_sub.parent != null) throw new InvalidOperationException("this is a submenu");
		if (_a.Count == 0) return 0;

		Api.ReleaseCapture(); //winforms still capturing on MouseClick etc, and menu would be like disabled

		if (!flags.Has(PMFlags.Underline)) if (0 != Api.SystemParametersInfo(Api.SPI_GETKEYBOARDCUES, 0)) flags |= PMFlags.Underline;

		_Show(flags, xy, excludeRect, owner.Hwnd);

		int R = 0;

		WindowsHook hKey = null, hMouse = null;
		timer timer = null;
		try {
			var wFore = wnd.active;
			bool foreground = wFore.IsOfThisThread;

			//to close with mouse use timer. Mouse hook may not work because of UAC.
			int mouseState = _GetMouseState();
			timer = new(t => { //close if mouse clicked a non-menu window or if activated another window
				int ms = _GetMouseState();
				bool clicked = ms != mouseState;
				mouseState = ms;
				if (clicked) _CloseIfClickedNotMenu(wnd.fromMouse(WXYFlags.Raw));
				else if (wnd.active != wFore) Close();
			});
			timer.Every(30);

			static int _GetMouseState() =>
				(keys.gui.getKeyState(KKey.MouseLeft) & 0x1)
				| ((keys.gui.getKeyState(KKey.MouseRight) & 0x1) << 1)
				| ((keys.gui.getKeyState(KKey.MouseMiddle) & 0x1) << 2)
				;
			//note: use only toggled state. Pressed state may change to "no" when mouse is already in a non-menu window although was in a menu window at the time of the mouse event.
			//note: in some cases toggled state may not change when clicked. Eg when clicked a taskbar button that activates another window. Then helps if (wnd.active!=wFore) Close();.

			void _CloseIfClickedNotMenu(wnd w) {
				//if(!w.Get.Owners(andThisWindow: true).Contains(_w)) Close(); //no, user may want nested root menus, although it is rare
				if (!_IsMenuWindow(w)) Close();
			}

			bool _IsMenuWindow(wnd w) => w == _w || w.ClassNameIs("Au.popupMenu");

			if (!foreground) {
				//never mind: hooks don't work if the active window has higher UAC IL. Then use timer and mouse/Esc toggle state.
				hKey = WindowsHook.Keyboard(h => {
					var k = h.Key;
					if (KeyboardHook != null && !h.IsUp) {
						switch (KeyboardHook(this, h)) {
						case PMKHook.None: return;
						case PMKHook.Close:
							_w.Post(Api.WM_CLOSE);
							return;
						case PMKHook.ExecuteFocused when FocusedItem != null:
							_w.Post(Api.WM_USER + 50, _a.IndexOf(FocusedItem));
							h.BlockEvent();
							return;
						}
					}
#if true
					if (!_IsCancelKey(k)) {
						if (_IsPassKey(k)) return;
						h.BlockEvent();
					}
					if (!h.IsUp) _w.Post(Api.WM_KEYDOWN, (int)k, 0); //else _w.Post(Api.WM_KEYUP, (int)k, 0xC0000001);
#else //unfinished. The idea was to call TranslateMessage, and if then PeekMessage gets wm_char...
					if(_IsCancelKey(k)) {
						_w.Post(Api.WM_CLOSE);
						return;
					}
					if (!h.IsUp) {
						var ok=Api.TranslateMessage(new() { hwnd = _w, message = Api.WM_KEYDOWN, wParam = (int)k });
						//print.it(ok);
						if(Api.PeekMessage(out var v, _w, Api.WM_CHAR, Api.WM_CHAR, Api.PM_NOREMOVE)) print.it("peek", v);
					}
#endif
				});

				//If the active app is showing a menu, it captures the mouse.
				//	If this menu is there, a click goes to that app instead of this menu.
				//	Workaround: mouse hook. We cannot SetCapture in this background thread.
				if (_IsCapturingMouse()) {
					hMouse = WindowsHook.Mouse(h => {
						if (h.IsInjected) return;
						if (!_IsCapturingMouse()) {
							h.hook.Unhook();
							return;
						}
						//tested: mouse move and wheel works without this.
						if (h.IsButton && h.Button is MButton.Left or MButton.Right or MButton.Middle) {
							var p = mouse.xy;
							var w = wnd.fromXY(p, WXYFlags.Raw);
							if (!_IsMenuWindow(w)) return;
							h.BlockEvent();
							int m = h.Button switch { MButton.Left => Api.WM_LBUTTONDOWN, MButton.Right => Api.WM_RBUTTONDOWN, _ => Api.WM_MBUTTONDOWN };
							if (h.IsButtonUp) m++;
							w.MapScreenToClient(ref p);
							w.Post(m, 0, Math2.MakeLparam(p));
						}
					});
				}

				static bool _IsCapturingMouse() => miscInfo.getGUIThreadInfo(out var g) && !g.hwndCapture.Is0;
			}

			//var pmo = new PrintMsgOptions(Api.WM_TIMER, Api.WM_MOUSEMOVE, Api.WM_NCMOUSEMOVE, Api.WM_PAINT, 0x138a /*SC_WORK_IDLE*/, Api.WM_USER, int.MinValue) { WindowProperties = true };
			//print.it("in");
			_MessageLoop();
			//print.it("out");
			void _MessageLoop() {
				do {
					for (; ; ) {
						if (_w.Is0) return;
						if (!Api.PeekMessage(out var m, default, 0, 0, Api.PM_NOREMOVE)) break;
						if (m.message == Api.WM_QUIT) return; //let outer loop get the message (tested)
						bool handled = false;
						if (m.message is Api.WM_LBUTTONDOWN or Api.WM_RBUTTONDOWN or Api.WM_MBUTTONDOWN or Api.WM_NCLBUTTONDOWN or Api.WM_NCRBUTTONDOWN or Api.WM_NCMBUTTONDOWN) {
							_CloseIfClickedNotMenu(m.hwnd);
							if (_w.Is0) return; //let outer loop get the message
						} else if (m.message is Api.WM_KEYDOWN or Api.WM_SYSKEYDOWN) {
							handled = _WmKeydown(m);
							if (!handled && _w.Is0) return; //let outer loop get the message. Used for keys that close the menu but must be passed to the app, eg Alt. If Esc, handled is true.
						}
						if (!Api.PeekMessage(out m)) break;
						if (handled) continue;
						//WndUtil.PrintMsg(m, pmo);
						if (m.message == Api.WM_CHAR) {
							_TopMenu()._WmChar((char)m.wParam);
							continue;
						}
						Api.TranslateMessage(m);
						Api.DispatchMessage(m);
					}
				}
				while (Api.WaitMessage());
				//why this strange loop?
				//	If the click or keydown closed the menu and wants to pass the message to the app, before passing it need to exit the loop, else bad things may happen.
				//	Or could use GetMessage + PostMessage, but it is probably more dirty; need to repost all queued messages, to avoid eg lbuttondown after lbuttonup.
			}
		}
		finally {
			hKey?.Dispose();
			hMouse?.Dispose();
			timer?.Stop();
			if (!_w.Is0) Api.DestroyWindow(_w);
		}

		var b = _result;
		if (b != null) {
			b.InvokeAction_();
			R = b.Id;
		}

		return R;
	}

	/// <summary>
	/// Returns <c>true</c> if the menu window is open.
	/// </summary>
	public bool IsOpen => !_w.Is0;

	/// <summary>
	/// After closing the menu gets the selected item, or <c>null</c> if canceled.
	/// </summary>
	public PMItem Result => _result;

	void _OpenTrap(string error = null) {
		if (IsOpen) throw new InvalidOperationException(error);
	}

	void _ShowSubmenu(PMItem b, bool focusFirst = false, popupMenu m = null) {
		if (b == _sub.item) return;
		_sub.child?.Close();

		bool contextMenu = m != null;
		if (!contextMenu) {
			if (b.clicked == null) return;
			if (b.clicked is Action<popupMenu> menu) {
				m = b.sourceFile == null ? new() : new popupMenu(null, b.sourceFile, b.sourceLine);
				base._CopyProps(m);
				m.CheckDontClose = CheckDontClose;
				m.RawText = RawText;
				menu(m);
			} else if (b.clicked is Func<popupMenu> func) {
				m = func();
			}
			if (m == null || m._a.Count == 0) return;
		}

		_sub.child = m;
		_sub.item = b;
		m._sub.parent = this;
		if (focusFirst && m._iHot < 0) m._iHot = 0;

		if (contextMenu) {
			m._Show(0, null, null, _w);
		} else {
			var r = _ItemRect(b, inScreen: true);
			r.Inflate(-_size.border, _size.border);

			m._Show(_flags & ~(PMFlags)0xffffff, new(r.right, r.top), r, _w);
		}
	}

	void _Show(PMFlags flags, POINT? xy, RECT? excludeRect, wnd owner) { //CONSIDER: _Show(PMPosition pos = null) //then could precisely specify offsets etc
		if (_a.Count == 0) return;

		_result = null;
		_flags = flags;

		RECT cr = default;
		bool byCaret = flags.Has(PMFlags.ByCaret) && miscInfo.getTextCursorRect(out cr, out _);
		POINT p = byCaret ? new(cr.left, cr.bottom) : xy ?? mouse.xy;

		var scrn = screen.of(p);
		_dpi = scrn.Dpi;
		var rs = scrn.WorkArea;
		rs.Inflate(-8, -8);

		if (byCaret) {
			if (excludeRect == null) {
				cr.Inflate(50, 1);
				excludeRect = cr;
				flags |= PMFlags.AlignRectBottomTop;
			}
		} else {
			if (flags.Has(PMFlags.WindowCenter) && wnd.active is wnd wa && wa.GetRect(out var rw)) {
				p = new(rw.CenterX, rw.CenterY);
				flags |= PMFlags.AlignCenterH | PMFlags.AlignCenterV;
			} else if (flags.Has(PMFlags.ScreenCenter)) {
				p = new(rs.CenterX, rs.CenterY);
				flags |= PMFlags.AlignCenterH | PMFlags.AlignCenterV;
			} else if (excludeRect == null && !flags.HasAny(PMFlags.AlignCenterH | PMFlags.AlignCenterV)) {
				excludeRect = new(p.x, p.y, 1, 1);
			}
		}

		if (_addedNewItems) {
			_addedNewItems = false;
			_Images();
		}

		_scroll = new(true, i => _a[i].rect.top, i => _a[i].rect.bottom);

		WS style = WS.POPUP | WS.DLGFRAME; //3-pixel frame
		WSE estyle = WSE.TOOLWINDOW | WSE.NOACTIVATE | WSE.TOPMOST;
		SIZE z = _Measure(rs.Width * 19 / 20);

		bool needScroll = z.height > rs.Height;
		if (needScroll) {
			z.height = rs.Height;
			style |= WS.VSCROLL;
		}

		if (byCaret && !flags.HasAny(PMFlags.AlignRight | PMFlags.AlignCenterH))
			p.x = Math.Max(p.x - _met.image - _met.check - _met.paddingLeft - _met.textPaddingX - 3, rs.left);

		RECT r = new(0, 0, z.width, z.height);
		Dpi.AdjustWindowRectEx(_dpi, ref r, style, estyle);
		_size.window = r.Size; _size.client = z; _size.border = -r.top;
		Api.CalculatePopupWindowPosition(p, r.Size, (uint)flags & 0xffffff, excludeRect.GetValueOrDefault(), out r);
		
		if (r.bottom > rs.bottom && r.top > rs.top - 4) r.Move(r.left, r.top - 4); //let the bottom edge not touch the bottom edge of the screen

		_w = WndUtil.CreateWindow(_WndProc, true, "Au.popupMenu", null, style, estyle, r.left, r.top, r.Width, r.Height, owner);
		_SetScrollbar(needScroll);

		_w.ShowL(true);

		_mouse.p = _w.MouseClientXY;
	}

	/// <summary>
	/// Closes the menu and its submenus.
	/// </summary>
	/// <param name="ancestorsToo">If this is a submenu, close the root menu with all submenus.</param>
	/// <remarks>
	/// Can be called from any thread.
	/// Does nothing if not open.
	/// </remarks>
	public void Close(bool ancestorsToo = false) {
		if (!IsOpen) return;
		if (_IsOtherThread) {
			_w.Post(Api.WM_CLOSE, ancestorsToo ? 1 : 0);
		} else {
			var w = _w;
			if (ancestorsToo) {
				for (var pm = _sub.parent; pm != null; pm = pm._sub.parent) {
					w = pm._w;
					pm._result = _result;
				}
			}
			Api.DestroyWindow(w);
		}
	}

	private protected override void _WmNcdestroy() {
		//print.it("destroy", _name);
		_sub.timer?.Stop();
		var pa = _sub.parent;
		if (pa != null) {
			pa._sub.child = null;
			pa._sub.item = null;
		} else {
			_w.Post(0);
		}
		_met?.Dispose(); _met = null;
		_font?.Dispose(); _font = null;
		_fontBold?.Dispose(); _fontBold = null;
		_scroll = null;
		_sub = default;
		_size = default;
		_mouse = default;
		_iHot = -1;
		base._WmNcdestroy();
	}

	#endregion

	nint _WndProc(wnd w, int msg, nint wParam, nint lParam) {
		//var pmo = new PrintMsgOptions(Api.WM_NCHITTEST, Api.WM_SETCURSOR, Api.WM_MOUSEMOVE, Api.WM_NCMOUSEMOVE, 0x10c1);
		//if (WndUtil.PrintMsg(out string s, w, msg, wParam, lParam, pmo)) print.it("<><c green>" + s + "<>");
		//WndUtil.PrintMsg(w, msg, wParam, lParam);

		if (_scroll.WndProc(w, msg, wParam, lParam)) return default;

		switch (msg) {
		case Api.WM_NCCREATE:
			_WmNccreate(w);
			break;
		case Api.WM_NCDESTROY:
			_WmNcdestroy();
			break;
		case Api.WM_CLOSE:
			Close(ancestorsToo: 0 != (wParam & 1));
			return default;
		//case Api.WM_THEMECHANGED: //don't need for a menu window
		//	_z?.Dispose();
		//	_z = new _Metrics(this);
		//	Api.InvalidateRect(w);
		//	break;
		case Api.WM_ERASEBKGND:
			return default;
		case Api.WM_PAINT:
			using (var bp = new BufferedPaint(w, true)) _Render(bp.DC, bp.UpdateRect);
			return default;
		case Api.WM_MOUSEACTIVATE:
			return Api.MA_NOACTIVATE;
		case Api.WM_MOUSEMOVE:
			_WmMousemove(lParam, fake: false);
			return default;
		case Api.WM_MOUSELEAVE:
			_WmMouseleave();
			return default;
		case >= Api.WM_LBUTTONDOWN and <= Api.WM_MBUTTONUP:
			_WmMousebutton(msg, lParam);
			return default;
		case Api.WM_GETOBJECT:
			if (_WmGetobject(wParam, lParam, out var r1)) return r1;
			break;
		case Api.WM_USER + 50: //posted by acc dodefaultaction or PMKHook.ExecuteFocused
			if (IsOpen) {
				int i = (int)wParam;
				if ((uint)i < _a.Count) _Click(i);
			}
			return default;
		}

		var R = Api.DefWindowProc(w, msg, wParam, lParam);

		switch (msg) {
		case Api.WM_NCPAINT:
			_WmNcpaint();
			break;
		}

		return R;
	}

	void _SetScrollbar(bool needScroll) {
		if (needScroll) {
			_scroll.SetRange(_a.Count);

			_scroll.PosChanged += (sb, part) => {
				_sub.child?.Close();

				int pos = _scroll.Pos;
				Api.InvalidateRect(_w);

				if (part <= -2) { //if mouse wheel, update hot item, submenu, tooltip
					var p = _w.MouseClientXY;
					if (_w.ClientRect.Contains(p)) _WmMousemove(Math2.MakeLparam(p), fake: true);
				}
			};
			_scroll.Visible = true;
		} else {
			_scroll.NItems = _a.Count;
		}
	}

	int _HitTest(POINT p, bool failIfDisabled = false) {
		p.y += _scroll.Offset;
		for (int i = 0; i < _a.Count; i++) {
			if (_a[i].rect.Contains(p)) return (failIfDisabled && _a[i].IsDisabled) ? -1 : i;
		}
		return -1;
	}

	RECT _ItemRect(PMItem k, bool inScreen = false) {
		var r = k.rect;
		r.Offset(0, -_scroll.Offset);
		if (inScreen) _w.MapClientToScreen(ref r);
		return r;
	}

	void _WmMousemove(nint lParam, bool fake) {
		var p = Math2.NintToPOINT(lParam);

		//prevent selecting item when mouse position does not change. It would interfere with keyboard navigation.
		if (!fake && p == _mouse.p) return; _mouse.p = p;

		int i = _HitTest(p, failIfDisabled: true);
		if (i != _iHot) {
			_SetHotItem(i);
			if (i >= 0) {
				var b = _a[i];
				int submenuDelay = _SubmenuTimer(b.IsSubmenu ? b : null);
				_SetTooltip(b, _ItemRect(b), lParam, submenuDelay);
			} else {
				_HideTooltip();
				_SubmenuTimer();
			}
		}

		if (_iHot >= 0 != _mouse.track) _mouse.track = Api.TrackMouseLeave(_w, _iHot >= 0) && _iHot >= 0;

		_sub.parent?._SubmenuMouseMove();
	}

	int _SubmenuTimer(PMItem item = null) {
		if (item == null && _sub.child == null) return 0;

		_sub.timer ??= new(t => {
			if (t.Tag is PMItem mi) {
				if (FocusedItem == mi) _ShowSubmenu(mi);
			} else if (_sub.child != null && !_sub.child._w.Rect.Contains(mouse.xy)) {
				_sub.child.Close();
			}
		});
		int R = Api.SystemParametersInfo(Api.SPI_GETMENUSHOWDELAY, 400);
		_sub.timer.Tag = item;
		_sub.timer.After(R);
		return R;
	}

	void _SubmenuMouseMove() {
		if (FocusedItem != _sub.item) {
			_SetHotItem(_a.IndexOf(_sub.item));
			_sub.timer.Stop();
		}
	}

	void _WmMouseleave() {
		_mouse.track = false;
		if (_iHot < 0) return;
		if (_sub.child?._w.Rect.Contains(mouse.xy) ?? false) return;
		_SetHotItem(-1);
		_SubmenuTimer();
	}

	void _WmMousebutton(int msg, nint lParam) {
		switch (msg) {
		case Api.WM_LBUTTONDOWN: _mouse.left = true; return;
		case Api.WM_LBUTTONUP: if (!_mouse.left) return; _mouse.left = false; break;
		case Api.WM_RBUTTONDOWN: _mouse.right = true; return;
		case Api.WM_RBUTTONUP: if (!_mouse.right) return; _mouse.right = false; break;
		case Api.WM_MBUTTONDOWN: _mouse.middle = true; return;
		case Api.WM_MBUTTONUP: if (_mouse.middle) Close(ancestorsToo: true); return;
		default: return;
		}
		var p = Math2.NintToPOINT(lParam);
		int i = _HitTest(p);
		if (i < 0) return;
		if (msg == Api.WM_LBUTTONUP) _Click(i);
		else _ContextMenu(_a[i]);
	}

	void _Click(int i, bool keyboard = false) {
		var b = _a[i];
		if (b.IsDisabled) return;

		if (b.checkType > 0) {
			if (b.checkType == 1) {
				b.IsChecked ^= true;
			} else if (!b.IsChecked) {
				for (int j = i; --j >= 0 && _Uncheck(j);) { }
				for (int j = i; ++j < _a.Count && _Uncheck(j);) { }
				b.IsChecked = true;

				bool _Uncheck(int j) {
					var v = _a[j];
					if (v.checkType != 2) return false;
					v.IsChecked = false;
					return true;
				}
			}
			if (b.checkDontClose) {
				b.InvokeAction_();
				return;
			}
		}

		if (b.IsSubmenu) {
			if (keyboard) _SetHotItem(i, ensureVisible: true);
			_ShowSubmenu(b, focusFirst: keyboard);
		} else {
			_result = b;
			Close(ancestorsToo: true);
		}
	}

	/// <summary>
	/// Don't close menu when clicked a checkbox or radio item.
	/// This property is applied to items added afterwards; submenus inherit it.
	/// </summary>
	public bool CheckDontClose { get; set; }

	void _ContextMenu(PMItem b) {
		if (b.IsSeparator) return;
		var (canEdit, canGo, goText) = MTItem.CanEditOrGoToFile_(b.sourceFile, b);
		if (canEdit || canGo) {
			var m = new popupMenu();
			if (canEdit) m["Edit menu item"] = _ => ScriptEditor.Open(b.sourceFile, b.sourceLine);
			if (canGo) m[goText] = _ => b.GoToFile_();
			_ShowSubmenu(b, m: m);
		}
	}

	popupMenu _TopMenu() {
		var m = this; while (m._sub.child != null) m = m._sub.child;
		return m;
	}

	bool _TopHot(out (popupMenu m, int i, PMItem b) r) {
		var m = this; while (m._sub.child != null) m = m._sub.child;
		int i = m._iHot;
		r = (m, i, i < 0 ? null : m._a[i]);
		return i >= 0;
	}

	/// <summary>
	/// Gets or sets the focused menu item.
	/// </summary>
	/// <remarks>
	/// The focused item visually shows the menu item that would be executed if clicked or pressed <c>Enter</c>, <c>Tab</c> or <c>Space</c> key. It changes when the user moves the mouse or presses navigation keys (arrows, <c>End</c>, <c>Home</c>, <c>PageDown</c>, <c>PageUp</c>).
	/// This property can be set before showing the menu or when it is open.
	/// </remarks>
	public PMItem FocusedItem {
		get => _iHot >= 0 ? _a[_iHot] : null;
		set {
			_ThreadTrap();
			int i = -1;
			if (value != null) {
				i = _a.IndexOf(value);
				if (i < 0) throw new ArgumentException();
			}
			if (_w.Is0) {
				_iHot = i;
			} else {
				_SetHotItem(i, ensureVisible: true);
			}
		}
	}

	void _SetHotItem(int i, bool ensureVisible = false) {
		if (i != _iHot) {
			if (_iHot >= 0) _Invalidate(_iHot);
			if ((_iHot = i) >= 0) _Invalidate(_iHot);
		}
		if (ensureVisible && i >= 0) {
			int pos = _scroll.Pos, y = _scroll.Offset, hei = _size.client.height;
			var r = _a[i].rect;
			if (r.top < y) {
				while (pos > 0 && r.top < y) y -= _a[--pos].rect.Height;
			} else if (r.bottom > y + hei) {
				while (pos < _scroll.Max && r.bottom > y + hei) y += _a[++pos].rect.Height;
			} else return;
			_scroll.Pos = pos;
		}
	}

	bool _WmKeydown(in MSG msg) { //called for root menu
		KKey k = (KKey)(int)msg.wParam;
		if (_IsCancelKey(k)) {
			Close();
			return false;
		} else if (_IsOkKey(k)) {
			if (_TopHot(out var v)) v.m._Click(v.i, keyboard: true);
		} else if (k == KKey.Escape) {
			_TopMenu().Close();
		} else if (k == KKey.Left) {
			var m = _TopMenu();
			if (m != this) m.Close();
		} else if (k == KKey.Right) {
			if (_TopHot(out var v) && v.b.IsSubmenu && !v.b.IsDisabled) v.m._ShowSubmenu(v.b, focusFirst: true);
		} else if (k is KKey.Down or KKey.Up or KKey.PageDown or KKey.PageUp or KKey.End or KKey.Home) {
			_TopMenu()._KeyNavigate(k);
		} else if (_IsPassKey(k)) {
			return false;
		} else { //eg a char key. Translate to get wm_char (we'll eat it), but eat this wm_keydown.
			Api.TranslateMessage(msg);
		}
		return true;
	}

	void _KeyNavigate(KKey k) { //called for top menu
		int i = _scroll.KeyNavigate(_iHot, k); if (i == _iHot) return;
		while ((uint)i < _a.Count && _a[i].IsSeparator) i += k is KKey.Home or KKey.Down or KKey.PageDown ? 1 : -1;
		_SetHotItem(Math.Clamp(i, 0, _a.Count - 1), ensureVisible: true);
	}

	void _WmChar(char c) { //called for top menu
		if (c <= ' ') return;
		char cl = char.ToLowerInvariant(c), cu = char.ToUpperInvariant(c);
		int iUnderlined = -1; List<int> aUnderlined = null;
		for (int i = 0; i < _a.Count; i++) {
			var v = _a[i];
			if (v.rawText || v.IsDisabled) continue;
			string s = v.Text;
			int j = StringUtil.FindUnderlineChar(s);
			if (j >= 0 && (s[j] == cu || s[j] == cl)) {
				if (iUnderlined < 0) iUnderlined = i;
				else (aUnderlined ??= new() { iUnderlined }).Add(i);
			}
		}
		if (aUnderlined != null) {
			int fi = 0;
			if (_iHot >= 0) {
				for (int i = 0; i < aUnderlined.Count; i++) if (aUnderlined[i] > _iHot) { fi = i; break; }
			}
			FocusedItem = _a[aUnderlined[fi]];
		} else if (iUnderlined >= 0) {
			_Click(iUnderlined, keyboard: true);
		}
	}

	static bool _IsOkKey(KKey k) => k is KKey.Enter or KKey.Tab or KKey.Space;
	static bool _IsCancelKey(KKey k) => k is KKey.Alt or KKey.Win or KKey.RWin or KKey.F10 or KKey.Apps or KKey.Back;
	static bool _IsPassKey(KKey k)
		=> k is KKey.Ctrl or KKey.Shift or KKey.CapsLock or KKey.NumLock or KKey.ScrollLock
		or KKey.PrintScreen or KKey.Pause or KKey.Insert
		or (>= KKey.F1 and <= KKey.F24) || keys.isMod(KMod.Ctrl | KMod.Alt | KMod.Win);

	/// <summary>
	/// Creates and shows a simple popup menu. Without images, actions, submenus. Returns item id or 0.
	/// </summary>
	/// <returns>id of the selected item when closed, or 0 if canceled.</returns>
	/// <param name="items">
	/// Menu items, like <c>"One|Two|Three"</c> or <c>new("One", "Two", "Three")</c> or string array or <b>List</b>.
	/// Item id can be optionally specified like <c>"1 One|2 Two|3 Three"</c>. If missing, uses id of previous non-separator item + 1. Example: <c>"One|Two|100 Three Four"</c> (1|2|100|101).
	/// For separators use <c>null</c> or empty strings: <c>"One|Two||Three|Four"</c>.
	/// </param>
	/// <param name="flags"></param>
	/// <param name="xy">Menu position in screen. If <c>null</c> (default), uses mouse position by default. It depends on <i>flags</i>.</param>
	/// <param name="excludeRect">The menu should not overlap this rectangle in screen.</param>
	/// <param name="owner">Owner window. The menu will be automatically closed when destroying its owner window.</param>
	/// <remarks>
	/// The function adds menu items and calls <see cref="Show"/>. Returns when menu closed. All parameters except <i>items</i> are same as of <b>Show</b>.
	/// </remarks>
	/// <seealso cref="dialog.showList"/>
	public static int showSimple(Strings items, PMFlags flags = 0, POINT? xy = null, RECT? excludeRect = null, AnyWnd owner = default) {
		var a = items.ToArray();
		var m = new popupMenu();
		foreach (var v in a) {
			var s = v;
			if (s.NE()) {
				m.Separator();
			} else {
				if (s.ToInt(out int id, 0, out int end)) {
					if (s.Eq(end, ' ')) end++;
					s = s[end..];
					m.Add(id, s);
				} else {
					m.Add(s);
				}
			}
		}
		return m.Show(flags, xy, excludeRect, owner);
	}

	/// <summary>
	/// Gets or sets callback function that decides how to respond to pressed keys (default, close, ignore, block).
	/// </summary>
	/// <remarks>
	/// The function is called on each key down event while the menu is open. Only if current thread is not in the foreground.
	/// To block a key, call <see cref="HookData.Keyboard.BlockEvent"/>.
	/// The function must be as fast as possible.
	/// </remarks>
	public Func<popupMenu, HookData.Keyboard, PMKHook> KeyboardHook { get; set; }
}
