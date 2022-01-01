//CONSIDER: disable hotkeys when editor hidden.

namespace Au.Tools
{
	static class QuickCapture
	{
		static keys.more.Hotkey _rk1, _rk2, _rk3;
		static popupMenu _m;

		public static void Info() {
			print.it($@"Hotkeys for quick capturing:
	{App.Settings.hotkeys.tool_quick} - capture window from mouse and show menu to insert code to find it etc.
	{App.Settings.hotkeys.tool_wnd} - capture window from mouse and show dialog 'Find window or control'.
	{App.Settings.hotkeys.tool_elm} - capture UI element from mouse and show dialog 'Find UI element'.
");
		}

		public static void RegisterHotkeys() {
			_Register(ref _rk1, ERegisteredHotkeyId.QuickCaptureMenu, App.Settings.hotkeys.tool_quick);
			_Register(ref _rk2, ERegisteredHotkeyId.QuickCaptureDwnd, App.Settings.hotkeys.tool_wnd);
			_Register(ref _rk3, ERegisteredHotkeyId.QuickCaptureDelm, App.Settings.hotkeys.tool_elm);

			void _Register(ref keys.more.Hotkey rk, ERegisteredHotkeyId id, string keys) { //ref, not in!
				if (keys.NE()) return;
				try {
					if (!rk.Register((int)id, keys, App.Hmain))
						print.warning($"Failed to register hotkey {keys}. Look in Options -> Hotkeys.", -1);
				}
				catch (Exception ex) { print.it(ex); }
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
			m["Find window"] = _ => _Insert(_Wnd_Find(w, default));
			m["Activate window"] = _ => _Insert(_Wnd_Find(w, default, activate: true));
			m["Find or run"] = _ => {
				if(TUtil.PathInfo.FromWindow(w)?.filePath is string path) {
					var s = _Wnd_Find(w, default).RxReplace(@"\bwnd\.find\K\(\d+, ", "OrRun(", 1);
					s = s.Insert(s.Length - 2, $", run: () => {{ run.it({path}); }}");
					_Insert(s);
				}
			};
			m.Separator();
			//m.Submenu("Click", m => {
			string _Click(wnd w, string v) {
				w.MapScreenToClient(ref p);
				return $"\r\nmouse.click({v}, {p.x}, {p.y});{screenshot}";
			}
			m["Click window"] = _ => _Insert(_Wnd_Find(w, default) + _Click(w, "w"), enclose: true);
			m["Click control"] = _ => _Insert(_Wnd_Find(w, c) + _Click(c, "c"), enclose: true);
			m.Last.IsDisabled = c.Is0;
			//CONSIDER: UI element
			m["Click screen"] = _ => _Insert($"mouse.click({p.x}, {p.y});{screenshot}");
			//});
			m.Separator();
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
			m.Submenu("Program", m => {
				var pi = TUtil.PathInfo.FromWindow(w);
				if (pi != null) {
					m["var s = path;"] = _ => _Path(0);
					m["run.it(path);"] = _ => _Path(1);
					m["t[name] = o => run.it(path);"] = _ => _Path(2);
					m.Separator();
					m["Copy path"] = _ => clipboard.text = pi.filePath;

					void _Path(int what) {
						var (path, name, args) = pi.GetCode();
						if (what == 2 && path.Starts("shell:")) name = w.Name;
						_Insert(what switch { 1 => $"run.it({path});", 2 => $"t[{_Str(name)}] = o => run.it({path});", _ => $"var s = {path};" });
					}
				}
				if (w.ProgramName is string pn) m["Copy filename"] = _ => clipboard.text = pn;
			});
			m.Separator();
			m["About"] = _ => Info();
			m.Add("Cancel");

			_m = m;
			m.Show();
			_m = null;
		}

		static string _Str(string s) {
			if (s == null) return "null";
			if (!TUtil.MakeVerbatim(ref s)) s = s.Escape(quote: true);
			return s;
		}

		static string _Wnd_Find(wnd w, wnd c, bool activate = false) {
			var f = new TUtil.WindowFindCodeFormatter();
			f.RecordWindowFields(w, 1, activate);
			if (!c.Is0) f.RecordControlFields(w, c);
			return f.Format();
		}

		static void _Insert(string s, bool enclose = false) {
			//print.it(s);
			if (enclose) s = "{\r\n" + s + "\r\n}";
			InsertCode.Statements(s/*, fold: s.Contains("/*image:\r\n")*/);
		}

		public static void ToolDwnd() {
			Dwnd.Dialog(wnd.fromMouse());
		}

		public static void ToolDelm() {
			Delm.Dialog(mouse.xy);
		}
	}
}
