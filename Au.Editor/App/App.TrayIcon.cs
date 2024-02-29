static partial class App {
	internal static class TrayIcon {
		static IntPtr[] _icons;
		static bool _disabled;
		static wnd _wNotify;
		
		const int c_msgNotify = Api.WM_APP + 1;
		static int s_msgTaskbarCreated;
		
		internal static void Update_() {
			if (_icons == null) {
				_icons = new IntPtr[2];
				
				s_msgTaskbarCreated = WndUtil.RegisterMessage("TaskbarCreated", uacEnable: true);
				
				WndUtil.RegisterWindowClass("Au.Editor.TrayNotify", _WndProc);
				_wNotify = WndUtil.CreateWindow("Au.Editor.TrayNotify", null, WS.POPUP, WSE.NOACTIVATE);
				//not message-only, because must receive s_msgTaskbarCreated and also used for context menu
				
				process.thisProcessExit += _ => {
					var d = new Api.NOTIFYICONDATA(_wNotify);
					Api.Shell_NotifyIcon(Api.NIM_DELETE, d);
				};
				
				_Add(false);
			} else {
				var d = new Api.NOTIFYICONDATA(_wNotify, Api.NIF_ICON) { hIcon = _GetIcon() };
				bool ok = Api.Shell_NotifyIcon(Api.NIM_MODIFY, d);
				Debug_.PrintIf(!ok, lastError.message);
			}
		}
		
		static void _Add(bool restore) {
			var d = new Api.NOTIFYICONDATA(_wNotify, Api.NIF_MESSAGE | Api.NIF_ICON | Api.NIF_TIP /*| Api.NIF_SHOWTIP*/) { //need NIF_SHOWTIP if called NIM_SETVERSION(NOTIFYICON_VERSION_4)
				uCallbackMessage = c_msgNotify,
				hIcon = _GetIcon(),
				szTip = App.AppNameShort
			};
			if (Api.Shell_NotifyIcon(Api.NIM_ADD, d)) {
				//d.uFlags = 0;
				//d.uVersion = Api.NOTIFYICON_VERSION_4;
				//Api.Shell_NotifyIcon(Api.NIM_SETVERSION, d);
				
				//timer.after(2000, _ => Update(TrayIconState.Disabled));
				//timer.after(3000, _ => Update(TrayIconState.Running));
				//timer.after(4000, _ => Update(TrayIconState.Normal));
			} else if (!restore) { //restore when "TaskbarCreated" message received. It is also received when taskbar DPI changed.
				Debug_.Print(lastError.message);
			}
		}
		
		static IntPtr _GetIcon() {
			int i = _disabled ? 1 : 0;
			ref IntPtr icon = ref _icons[i];
			if (icon == default) Api.LoadIconMetric(Api.GetModuleHandle(null), Api.IDI_APPLICATION + i, 0, out icon);
			return icon;
			//Windows 10 on DPI change automatically displays correct non-scaled icon if it is from native icon group resource.
			//	I guess then it calls GetIconInfoEx to get module/resource and extracts new icon from same resource.
		}
		
		static void _Notified(nint wParam, nint lParam) {
			int msg = Math2.LoWord(lParam);
			//if (msg != Api.WM_MOUSEMOVE) WndUtil.PrintMsg(default, msg, 0, 0);
			switch (msg) {
			case Api.WM_LBUTTONUP:
				ShowWindow();
				break;
			case Api.WM_RBUTTONUP:
				_ContextMenu();
				break;
			//case Api.WM_MBUTTONDOWN: //does not work on Win11
			//	TriggersAndToolbars.DisableTriggers(null);
			//	break;
			}
		}
		
		static unsafe nint _WndProc(wnd w, int m, nint wParam, nint lParam) {
			//WndUtil.PrintMsg(w, m, wParam, lParam);
			if (m == c_msgNotify) _Notified(wParam, lParam);
			else if (m == s_msgTaskbarCreated) _Add(true); //when explorer restarted or taskbar DPI changed
			else if (m == Api.WM_DESTROY) _Exit();
			else if (m == Api.WM_DISPLAYCHANGE) {
				Tasks.OnWM_DISPLAYCHANGE();
			} else if (m == Api.WM_SETTINGCHANGE) {
				if (lParam != 0) {
					string s = null;
					try { s = new((char*)lParam); }
					catch (Exception e1) { Debug_.Print(e1); }
					//print.it("WM_SETTINGCHANGE", wParam, s);
					if (s == "Environment") App._envVarUpdater.WmSettingchange();
				}
			}
			
			return Api.DefWindowProc(w, m, wParam, lParam);
		}
		
		static void _ContextMenu() {
			var m = new popupMenu();
			m.AddCheck("Disable triggers", check: _disabled, _ => TriggersAndToolbars.DisableTriggers(null));
			m.Separator();
			m.Add("Exit", _ => _Exit());
			m.Show(PMFlags.AlignBottom | PMFlags.AlignCenterH);
		}
		
		static void _Exit() {
			_app.Shutdown();
		}
		
		public static bool Disabled {
			get => _disabled;
			set { if (value == _disabled) return; _disabled = value; Update_(); }
		}
	}
}
