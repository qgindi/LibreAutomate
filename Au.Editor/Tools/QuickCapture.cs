//CONSIDER: disable hotkeys when editor hidden.

namespace Au.Tools;

static class QuickCapture {
	static RegisteredHotkey _rk1, _rk2, _rk3;
	static popupMenu _m;

	public static void Info() {
		print.it($@"Hotkeys for quick capturing:
	{App.Settings.hotkeys.tool_quick} - capture window from mouse and show menu to insert code to find it etc.
	{App.Settings.hotkeys.tool_wnd} - capture window from mouse and show dialog 'Find window'.
	{App.Settings.hotkeys.tool_elm} - capture UI element from mouse and show dialog 'Find UI element'.
");
	}

	public static void RegisterHotkeys() {
		_Register(ref _rk1, ERegisteredHotkeyId.QuickCaptureMenu, App.Settings.hotkeys.tool_quick, nameof(Menus.Code.Quick_capturing));
		_Register(ref _rk2, ERegisteredHotkeyId.QuickCaptureDwnd, App.Settings.hotkeys.tool_wnd, nameof(Menus.Code.wnd));
		_Register(ref _rk3, ERegisteredHotkeyId.QuickCaptureDelm, App.Settings.hotkeys.tool_elm, nameof(Menus.Code.elm));

		static void _Register(ref RegisteredHotkey rk, ERegisteredHotkeyId id, string keys, string menu) { //ref, not in!
			if (!keys.NE()) {
				try {
					if (!rk.Register((int)id, keys, App.Hmain)) {
						print.warning($"Failed to register hotkey {keys}. Look in Options -> Hotkeys.", -1);
						keys = null;
					}
				}
				catch (Exception ex) { print.it(ex); keys = null; }
			}
			App.Commands[menu].MenuItem.InputGestureText = keys;
		}
	}

	public static void UnregisterHotkeys() {
		_rk1.Unregister();
		_rk2.Unregister();
		_rk3.Unregister();
	}

	//CONSIDER: while showing menu, show on-screen rectangles of the window, control and elm.
	//CONSIDER: the 'click' items could show a dialog to select Coord format. Or add tool in editor.

	public static void Menu() {
		_m?.Close();

		var p = mouse.xy;
		wnd w0 = wnd.fromXY(p), w = w0.Window, c = w == w0 ? default : w0;
		uint color = uiimage.getPixel(p) & 0xFFFFFF;
		var screenshot = TUtil.MakeScreenshot(p);

		var m = new popupMenu();
		m["Find window"] = _ => _Insert(_Wnd_Find(w));
		m.Submenu("Find+", m => {
			m["Find and activate"] = _ => _Insert(_Wnd_Find(w, activate: true));
			m["Find or run"] = _ => {
				var pi = TUtil.PathInfo.FromWindow(w);
				if (pi != null) _Insert(_Wnd_Find(w, activate: true, orRun: pi.FormatCode(1)));
			};
			m["Run and find"] = _ => {
				var pi = TUtil.PathInfo.FromWindow(w);
				if (pi != null) _Insert(_Wnd_Find(w, activate: true, andRun: pi.FormatCode(1)));
			};
			m["wndFinder"] = _ => _Insert("var f = new wndFinder(" + TUtil.ArgsFromWndFindCode(_Wnd_Find(w)) + ");");
			m["Find control"] = _ => _Insert(_Wnd_Find(w, c));
			m.Last.IsDisabled = c.Is0;
		});
		m.Submenu("Mouse", m => {
			string _Click(wnd w, string v) {
				w.MapScreenToClient(ref p);
				return $"\r\nmouse.click({v}, {p.x}, {p.y});{screenshot}";
			}
			m["Click window"] = _ => _Insert(_Wnd_Find(w) + _Click(w, "w"));
			m["Click control"] = _ => _Insert(_Wnd_Find(w, c) + _Click(c, "c"));
			m.Last.IsDisabled = c.Is0;
			//CONSIDER: UI element
			m["Click screen"] = _ => _Insert($"mouse.click({p.x}, {p.y});{screenshot}");
		});
		m.Submenu("Triggers", m => {
			m["Window trigger"] = _ => TriggersAndToolbars.QuickWindowTrigger(w, 0);
			m["Window scope for triggers"] = _ => TriggersAndToolbars.QuickWindowTrigger(w, 1);
			m.Last.Tooltip = "Hotkey/autotext/mouse triggers added afterwards will work only when this window is active";
			m["Program scope for triggers"] = _ => TriggersAndToolbars.QuickWindowTrigger(w, 2);
			m.Last.Tooltip = "Hotkey/autotext/mouse triggers added afterwards will work only when a window of this program is active";
		});
		m.Submenu("Program", m => {
			var pi = TUtil.PathInfo.FromWindow(w);
			if (pi != null) {
				m["string s = path;"] = _ => _Insert(pi.FormatCode(0));
				m["run.it(path);"] = _ => _Insert(pi.FormatCode(1));
				m["t[name] = o => run.it(path);"] = _ => _Insert(pi.FormatCode(2));
				m.Separator();
				m["Copy path"] = _ => clipboard.text = pi.filePath;
			}
			if (w.ProgramName is string pn) m["Copy filename"] = _ => clipboard.text = pn;
		});
		m.Submenu("Color", m => {
			string s0 = color.ToString("X6"), s1 = "#" + s0, s2 = $"0x" + s0, s3 = $"0x" + ColorInt.SwapRB(color).ToString("X6");
			m["Copy #RRGGBB:  " + s1] = _ => clipboard.text = s1;
			m["Copy 0xRRGGBB:  " + s2] = _ => clipboard.text = s2;
			m["Copy 0xBBGGRR:  " + s3] = _ => clipboard.text = s3;
		});
		//	m.Submenu("Get color", m => {
		//		m["Window"] = _=> {
		//			
		//		};
		//		m["Control"] = _=> {
		//			
		//		};
		//		m.Last.IsDisabled=c.Is0;
		//		m["Screen"] = _=> {
		//			
		//		};
		//	});
		m.Separator();
		m["About"] = _ => Info();
		m.Add("Cancel");

		_m = m;
		m.Show();
		_m = null;
	}

	static string _Wnd_Find(wnd w, wnd c = default, bool activate = false, string orRun = null, string andRun = null) {
		var f = new TUtil.WindowFindCodeFormatter();
		f.RecordWindowFields(w, andRun != null ? 30 : 1, activate);
		f.orRunW = orRun;
		f.andRunW = andRun;
		if (!c.Is0) f.RecordControlFields(w, c);
		return f.Format();
	}

	static void _Insert(string s) {
		//print.it(s);
		InsertCode.Statements(s);
	}

	public static void ToolDwnd() {
		Dwnd.Dialog(wnd.fromMouse());
	}

	public static void ToolDelm() {
		Delm.Dialog(mouse.xy);
	}
}
