//CONSIDER: disable hotkeys when editor hidden.

namespace Au.Tools;

static class QuickCapture {
	static popupMenu _m;

	public static void Info() {
		print.it($@"Hotkeys for quick capturing (Options > Hotkeys):
	{App.Settings.hotkeys.tool_quick} - capture window from mouse and show menu to insert code to find it etc.
	{App.Settings.hotkeys.tool_wnd} - capture window from mouse and show tool 'Find window'.
	{App.Settings.hotkeys.tool_elm} - capture UI element from mouse and show tool 'Find UI element'.
	{App.Settings.hotkeys.tool_uiimage} - capture image in window from mouse and show tool 'Find image'.
");
	}

	//CONSIDER: while showing menu, show on-screen rectangles of the window, control and elm.
	//CONSIDER: the 'click' items could show a dialog to select Coord format. Or add tool in editor.

	public static void Menu() {
		_m?.Close();

		var p = mouse.xy;
		wnd w0 = wnd.fromXY(p), w = w0.Window, c = w == w0 ? default : w0;
		uint color = CaptureScreen.Pixel(p) & 0xFFFFFF;
		var screenshot = TUtil.MakeScreenshot(p);

		var m = new popupMenu();
		m["Find window"] = _ => _Insert(_Wnd_Find(w));
		m.Submenu("Find+", m => {
			m["Find and activate"] = _ => _Insert(_Wnd_Find(w, activate: true));
			m["Find or run"] = _ => {
				var k = TUtil.PathInfo.FromWindow(w);
				if (k != null) _Insert(_Wnd_Find(w, activate: true, orRun: k.FormatCode(TUtil.PathCode.Run)));
			};
			m["Run and find"] = _ => {
				var k = TUtil.PathInfo.FromWindow(w);
				if (k != null) _Insert(_Wnd_Find(w, activate: true, andRun: k.FormatCode(TUtil.PathCode.Run)));
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
			//CONSIDER: somehow allow to select "program" and "contains".
			//	Or show 'Find window' tool in trigger mode. Also the tool should allow to set scope.
			m["Window trigger"] = _ => TriggersAndToolbars.QuickWindowTrigger(w, 0);
			m["Window scope for triggers"] = _ => TriggersAndToolbars.QuickWindowTrigger(w, 1);
			m.Last.Tooltip = "Hotkey/autotext/mouse triggers added afterwards will work only when this window is active";
			m["Program scope for triggers"] = _ => TriggersAndToolbars.QuickWindowTrigger(w, 2);
			m.Last.Tooltip = "Hotkey/autotext/mouse triggers added afterwards will work only when a window of this program is active";
		});
		m.Submenu("Program", m => {
			var k = TUtil.PathInfo.FromWindow(w);
			if (k != null) {
				TUtil.PathInfo.QuickCaptureMenu(m, o => _Insert(k.FormatCode(o)));
				m.Separator();
				m["Copy path"] = _ => clipboard.text = k.fileRaw;
				m["Copy @\"path\""] = _ => clipboard.text = k.fileString;
			}
			if (w.ProgramName is string pn) m["Copy filename"] = _ => clipboard.text = pn;
			if (k != null) {
				m.Separator();
				m["Select in Explorer"] = _ => run.selectInExplorer(k.fileRaw);
			}
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
		InsertCode.Statements(s, ICSFlags.MakeVarName1);
	}

	public static void AoolDwnd() {
		Dwnd.Dialog(wnd.fromMouse());
	}

	public static void ToolDelm() {
		Delm.Dialog(mouse.xy);
	}

	public static void ToolDuiimage() {
		Duiimage.Dialog(wnd.fromMouse(WXYFlags.NeedWindow));
	}
}
