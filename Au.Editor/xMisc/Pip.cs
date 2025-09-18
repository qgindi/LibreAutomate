//AxMSTSCLib.dll and MSTSCLib.dll were created with this cmd in Visual Studio: aximp.exe c:\windows\system32\mstscax.dll.
//	See https://learn.microsoft.com/en-us/windows/win32/termserv/calling-non-scriptable-interfaces.

using AxMSTSCLib;
using MSTSCLib;
using System.Windows.Forms;
using System.IO.Pipes;
using Microsoft.Win32;
using System.Runtime.Loader;
using wpfc = System.Windows.Controls;

namespace LA;//TODO

static class Pip {
	public static bool noActivate;
	static wnd _wMsg;
	
	/// <summary>
	/// Called at startup in non-admin process when command line starts with /pip.
	/// </summary>
	/// <param name="args"><c>args</c> without the first.</param>
	/// <returns>Process exit code (non-0 on error).</returns>
	public static int Run(ReadOnlySpan<string> args) {
		process.ThisThreadSetComApartment_(ApartmentState.STA);
		process.thisProcessCultureIsInvariant = true;
		App.InitThisAppFoldersEtc_();
		
		if (args.Length > 0) {
			switch (args[0].Lower()) {
			case "/enablecs":
				return api.WTSEnableChildSessions(true) ? 0 : 1;
			case "/disablecs":
				return api.WTSEnableChildSessions(false) ? 0 : 1;
			case "/settings":
				PipWindow.SettingsDialog();
				return 0;
			case "/noactivate":
				noActivate = true;
				break;
			default: return 2;
			}
		}
		
		if (!script.TrySingle_(@"Global\Au.PiP-mutex")) {
			if (!noActivate) {
				var w = wnd.find("PiP session", "*.Window.*", process.thisExeName);
				if (!w.Is0) w.ActivateL(true);
				else print.it("A PiP already exists on this computer. Single PiP allowed.");
			}
			return 3;
		}
		
		_wMsg = WndUtil.CreateMessageOnlyWindow("#32770", "Au.PiP-msg");
		
		if (!osVersion.minWin8_1) return _Error("Requires Windows 8.1 or later."); //tested: on 8.0 error "class not registered"
		if (miscInfo.isChildSession) return _Error("Can't start another PiP session from PiP session.");
		if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", null) is string sPN && sPN.Contains(" Home"))
			return _Error($"PiP does not work on Windows Home editions. Your OS is {sPN}.");
		
		if (api.WTSIsChildSessionsEnabled(out bool y) && !y) {
			bool isAdmin = uacInfo.isAdmin;
			string sInfo = $"PiP uses Windows feature \"Child Sessions\". Click OK to enable it.{(isAdmin ? "" : "\n\nAdministrator rights required to enable this feature.")}";
			if (!dialog.showOkCancel("Picture-in-picture setup", sInfo, icon: isAdmin ? 0 : DIcon.Shield)) return 1;
			bool enabled = isAdmin ? api.WTSEnableChildSessions(true) : run.it(process.thisExePath, "/pip /enablecs", RFlags.Admin | RFlags.WaitForExit).ProcessExitCode == 0;
			if (!enabled) return _Error("Failed to enable Child Sessions.");
		}
		
		static int _Error(string s) {
			dialog.showError("PiP error", s);
			return 1;
		}
		
		AssemblyLoadContext.Default.Resolving += static (alc, an) => alc.LoadFromAssemblyPath(folders.ThisAppBS + an.Name + ".dll");
		Application.Run(new PipWindow());
		return 0;
	}
	
	internal static void SetConnected_(int connected) {
		_wMsg.SetWindowLong(GWL.DWL.USER, connected);
	}
}

file class PipWindow : Form {
	AxMsRdpClient9NotSafeForScripting _axRdp;
	IMsRdpClient9 _rdp;
	wnd _w;
	FullScreenWindow _fullScreen;
	long _reconnecting;
	
	public PipWindow() {
		Text = "PiP session";
		Icon = icon.trayIcon(Api.IDI_APPLICATION + 2).ToGdipIcon();
		/*
*PhosphorIcons.RectangleFill #BBE3FF %.5,2.5,.5,2.5,f
*PhosphorIcons.PictureInPictureLight #909090
		*/
		
		Controls.Add(_axRdp = new() { Dock = DockStyle.Fill });
		
		StartPosition = FormStartPosition.Manual;
		if (_sett.wndPos == null) {
			var rm = screen.primary.WorkArea;
			base.SetDesktopBounds(rm.left + rm.Width / 3, rm.top, rm.Width * 2 / 3, rm.Height * 3 / 4);
		}
		WndSavedRect.Restore(this, _sett.wndPos, o => _sett.wndPos = o);
	}
	
	protected override void OnHandleCreated(EventArgs e) {
		_w = this.Hwnd();
		
		if (osVersion.minWin11) unsafe {
				var rbp = api.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
				api.DwmSetWindowAttribute(_w, api.DWMWA_WINDOW_CORNER_PREFERENCE, &rbp, 4);
			}
		
		base.OnHandleCreated(e);
	}
	
	protected override void OnLoad(EventArgs e) {
		_rdp = (IMsRdpClient9)_axRdp.GetOcx();
		var advSett = _rdp.AdvancedSettings9;
		var exSett = (IMsRdpExtendedSettings)_rdp;
		
		_rdp.Server = "localhost";
		exSett.set_Property("ConnectToChildSession", true);
		advSett.EnableCredSspSupport = true;
		
		//_rdp.UserName = Environment.UserName;
		//advSett.ClearTextPassword = "?";
		
		try {
			int scale = Dpi.Scale(100, _w);
			if (scale > 100) {
				exSett.set_Property("DesktopScaleFactor", (uint)Math.Min(500, scale));
				exSett.set_Property("DeviceScaleFactor", 100u);
			}
			
			//advSett.SmartSizing = true; //does not work; not useful
			advSett.ContainerHandledFullScreen = 1; //less trouble
			advSett.PinConnectionBar = false;
			((IMsRdpClientNonScriptable3)_rdp).ConnectionBarText = this.Text; //tested: other members of "NonScriptable" interfaces are not interesting.
			if (!_sett.redirectClipboard) advSett.RedirectClipboard = false;
			//if (osVersion.minWin10_1803) _exSett.set_Property("ManualClipboardSyncEnabled", true); //exception when trying to get the `Clipboard` property. Bug in native method. Code: if (_rdp is IMsRdpClientNonScriptable7 rdpns7) timer.every(2000, _=> { print.it(rdpns7.Clipboard); });
			//advSett.EnableWindowsKey = 1; //default 1, but does not work
			advSett.Compress = 0;
			advSett.RedirectSmartCards = true;
			//timer.after(3000, _ => { es.set_Property("ShowSessionDiagnostics", true); }); //does not work
			
			if (Api.WTSGetChildSessionId(out int csid)) {
				//print.it(csid);
			} else {
				//if editor not set to run at startup, set to run at startup once
				bool editorAutoRun = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "Au.Editor", null) is string s1 && s1.RxIsMatch($"(?i)^\".+?Au.Editor.exe\"");
				if (!editorAutoRun) Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunOnce", "Au.Editor (PiP)", $"\"{process.thisExePath}\" /a");
			}
		}
		catch (Exception ex) { print.warning(ex); }
		
		_Events();
		
		_rdp.Connect();
		
		_axRdp.SizeChanged += (_, _) => _UpdateDesktopSize();
		AutoScaleMode = AutoScaleMode.Dpi; //not in ctor, to avoid DPI-scaling of the window size at startup
		
		if (Pip.noActivate) {
			_w.SetWindowPos(SWPFlags.NOACTIVATE | SWPFlags.NOMOVE | SWPFlags.NOSIZE, zorderAfter: SpecHWND.BOTTOM);
			//var wa = wnd.active;
			//if (!wa.Is0 && wa != _w) {
			//	_w.SetWindowPos(SWPFlags.NOACTIVATE | SWPFlags.NOMOVE | SWPFlags.NOSIZE, zorderAfter: wa);
			//	//_w.TaskbarButton.Flash(1); //no
			//}
		} else {
			_w.ActivateL(); //window in background after UAC consent
		}
		
		base.OnLoad(e);
	}
	
	protected override bool ShowWithoutActivation => Pip.noActivate;
	
	protected override CreateParams CreateParams {
		get {
			var p = base.CreateParams;
			if (Pip.noActivate) p.ExStyle |= (int)WSE.NOACTIVATE; //workaround for: the ActiveX control activates the window, although ShowWithoutActivation returns true
			return p;
		}
	}
	
	protected override void OnClosing(CancelEventArgs e) {
		_fullScreen?.SetFullScreen(false);
		Pip.SetConnected_(0);
		base.OnClosing(e);
	}
	
	void _Events() {
		_axRdp.OnConnected += (sender, e) => {
			_PrintEvent("OnConnected");
			Pip.SetConnected_(1);
			_reconnecting = 0;
			if (Pip.noActivate) _w.SetExStyle(WSE.NOACTIVATE, WSFlags.Remove);
		};
		_axRdp.OnDisconnected += (sender, e) => {
			_PrintEvent("OnDisconnected", _rdp.ExtendedDisconnectReason);
			Pip.SetConnected_(0);
			if (_reconnecting != 0 && Environment.TickCount64 - _reconnecting < 5000) {
				timer.after(100, _ => {
					//print.it("reconnecting"); //tested: succeeds after 400 ms
					_rdp.AdvancedSettings9.RedirectClipboard = _sett.redirectClipboard;
					_rdp.Connect();
				});
			} else {
				Close();
			}
		};
		_axRdp.OnRequestGoFullScreen += (sender, e) => {
			_PrintEvent("OnRequestGoFullScreen");
			_SetFullScreen(true);
		};
		_axRdp.OnRequestLeaveFullScreen += (sender, e) => {
			_PrintEvent("OnRequestLeaveFullScreen");
			_SetFullScreen(false);
		};
		_axRdp.OnRequestContainerMinimize += (sender, e) => {
			_PrintEvent("OnRequestContainerMinimize");
			_w.ShowMinimized();
		};
		_axRdp.OnConnectionBarPullDown += (sender, e) => {
			_PrintEvent("OnConnectionBarPullDown");
			_rdp.FullScreen = false;
			if (mouse.isPressed(MButtons.Left) && _w.GetWindowAndClientRectInScreen(out var r, out var rc) && rc.top > r.top) {
				r.bottom = rc.top;
				Api.SetCursorPos(r.CenterX, r.CenterY);
				_w.Send(Api.WM_SYSCOMMAND, Api.SC_MOVE);
			}
		};
		_axRdp.OnAuthenticationWarningDisplayed += (sender, e) => {
			_PrintEvent("OnAuthenticationWarningDisplayed");
			print.it("Info: If PiP prompts for credentials every time: sign out of the PiP session, then sign out of your main session and sign in again.");
			if (Pip.noActivate) {
				_w.SetExStyle(WSE.NOACTIVATE, WSFlags.Remove);
				_w.ActivateL();
			}
		};
#if DEBUG
		_axRdp.OnConnecting += (sender, e) => { _PrintEvent("OnConnecting"); };
		_axRdp.OnLoginComplete += (sender, e) => { _PrintEvent("OnLoginComplete"); };
		_axRdp.OnChannelReceivedData += (sender, e) => { _PrintEvent("OnChannelReceivedData"); };
		_axRdp.OnFatalError += (sender, e) => { _PrintEvent("OnFatalError", e.errorCode); };
		_axRdp.OnWarning += (sender, e) => { _PrintEvent("OnWarning", e.warningCode); };
		_axRdp.OnEnterFullScreenMode += (sender, e) => { _PrintEvent("OnEnterFullScreenMode "); };
		_axRdp.OnLeaveFullScreenMode += (sender, e) => { _PrintEvent("OnLeaveFullScreenMode "); };
		_axRdp.OnRemoteDesktopSizeChange += (sender, e) => { _PrintEvent("OnRemoteDesktopSizeChange", e.width, e.height); };
		_axRdp.OnIdleTimeoutNotification += (sender, e) => { _PrintEvent("OnIdleTimeoutNotification"); };
		_axRdp.OnConfirmClose += (sender, e) => { _PrintEvent("OnConfirmClose"); e.pfAllowClose = true; };
		//_axRdp.OnReceivedTSPublicKey += (sender, e) => { _PrintEvent("OnReceivedTSPublicKey"); };
		_axRdp.OnAutoReconnecting += (sender, e) => { _PrintEvent("OnAutoReconnecting"); };
		_axRdp.OnAuthenticationWarningDismissed += (sender, e) => { _PrintEvent("OnAuthenticationWarningDismissed"); };
		//_axRdp.OnRemoteProgramResult += (sender, e) => { _PrintEvent("OnRemoteProgramResult"); };
		//_axRdp.OnRemoteProgramDisplayed += (sender, e) => { _PrintEvent("OnRemoteProgramDisplayed"); };
		//_axRdp.OnRemoteWindowDisplayed += (sender, e) => { _PrintEvent("OnRemoteWindowDisplayed"); };
		_axRdp.OnLogonError += (sender, e) => { _PrintEvent("OnLogonError", e.lError); };
		_axRdp.OnFocusReleased += (sender, e) => { _PrintEvent("OnFocusReleased"); };
		//_axRdp.OnUserNameAcquired += (sender, e) => { _PrintEvent("OnUserNameAcquired", e.bstrUserName); };
		//_axRdp.OnMouseInputModeChanged += (sender, e) => { _PrintEvent("OnMouseInputModeChanged"); };
		_axRdp.OnServiceMessageReceived += (sender, e) => { _PrintEvent("OnServiceMessageReceived"); };
		//_axRdp.OnNetworkStatusChanged += (sender, e) => { _PrintEvent("OnNetworkStatusChanged"); };
		_axRdp.OnDevicesButtonPressed += (sender, e) => { _PrintEvent("OnDevicesButtonPressed"); };
		_axRdp.OnAutoReconnected += (sender, e) => { _PrintEvent("OnAutoReconnected"); };
		_axRdp.OnAutoReconnecting2 += (sender, e) => { _PrintEvent("OnAutoReconnecting2"); };
#endif
	}
	
	[Conditional("DEBUG")]
	static void _PrintEvent(string s) {
		//print.it($"<><c green>event <_>{s}</_><>");
	}
	
	[Conditional("DEBUG")]
	static void _PrintEvent(object value1, object value2, params object[] more) {
		//print.it($"<><c green>event <_>{print.util.toList(", ", value1, value2, more)}</_><>");
	}
	
	void _UpdateDesktopSize() {
		if (_dontUpdateDesktopSize) return;
		var cs = _axRdp.ClientSize; int wid = cs.Width, hei = cs.Height;
		if (wid < 200 || hei < 200) return; //eg minimized
		if (wid == _rdp.DesktopWidth && hei == _rdp.DesktopHeight) return; //eg restored from minimized
		
		//print.it("_UpdateDesktopSize", wid, hei, _rdp.DesktopWidth, _rdp.DesktopHeight);
		
		int scale = Dpi.Scale(100, _w);
		try { _rdp.UpdateSessionDisplaySettings((uint)wid, (uint)hei, 0, 0, 0, (uint)scale, 100); }
		catch { } //eg soon after starting. Never mind.
	}
	
	void _SetFullScreen(bool on) {
		_fullScreen ??= new(_w);
		_dontUpdateDesktopSize = true; //because the control resized 2 times: when changing window style and size; it makes PiP desktop resizing slow.
		_fullScreen.SetFullScreen(on);
		_dontUpdateDesktopSize = false;
		_UpdateDesktopSize();
	}
	bool _dontUpdateDesktopSize;
	
	bool IsConnected => _rdp?.Connected is 1;
	//bool IsConnectedOrConnecting => _rdp?.Connected is 1 or 2;
	
	protected override void WndProc(ref Message m) {
		switch (m.Msg) {
		case Api.WM_SYSCOMMAND:
			switch (m.WParam.ToInt32() & 0xFFF0) {
			case Api.SC_MAXIMIZE:
				_rdp.FullScreen = true;
				return;
			case Api.SC_MOUSEMENU:
				_SystemIconMenu();
				return;
				//case Api.SC_CLOSE: //rejected. Inconsistent with the full-screen Close button behavior (and the button cannot be intercepted or removed).
				//	switch (popupMenu.showSimple("1 Disconnect|2 Sign out", owner: this)) {
				//	case 1: _rdp.Disconnect(); break;
				//	case 2: _ = PipIPC.SendAsync("logoff"); break;
				//	}
				//	return;
			}
			break;
		case Api.WM_ACTIVATE when IsConnected:
			_ = PipIPC.SendAsync(Math2.LoWord(m.WParam) != 0 ? "WM_ACTIVATE 1" : "WM_ACTIVATE 0", 1000);
			break;
		}
		
		base.WndProc(ref m);
	}
	
	void _SystemIconMenu() {
		if (!IsConnected) return;
#if true
		var m = new popupMenu();
		m.Add("SETTINGS (will reconnect to change)", disable: true);
		m.AddCheck("Clipboard sync enabled", _sett.redirectClipboard, o => {
			_sett.redirectClipboard = o.IsChecked;
			_reconnecting = Environment.TickCount64;
			_rdp.Disconnect();
			//note: Reconnect() does not work.
		});
#else
		if (!api.WTSGetChildSessionId(out int csid)) return;
		int rdpclip = process.allProcesses().FirstOrDefault(o => o.Name.Eqi("rdpclip.exe") && o.SessionId == csid).Id;
		var m = new popupMenu();
		m.AddCheck("Enable clipboard sync", rdpclip != 0, o => {
			if (o.IsChecked) PipAgent.Send("clipboard"); //run rdpclip.exe in PiP session
			else process.terminate(rdpclip);
		});
#endif
		m.Separator();
		m["Help"] = o => HelpUtil.AuHelp("editor/PiP session");
#if DEBUG
		m.Separator();
		m["Test 1"] = o => {
			print.it(PipIPC.SendSync("test"));
		};
		m["Test 2"] = async o => {
			if (await PipIPC.SendAsync("test") is string r) {
				print.it(r);
			}
		};
#endif
		m.Show(owner: this);
	}
	
	public static void SettingsDialog() {
		var b = new wpfBuilder("PiP settings").WinSize(400);
		b.R.Add(out wpfc.CheckBox cSepClip, "Clipboard sync enabled").Checked(_sett.redirectClipboard);
		b.R.AddOkCancel();
		b.End();
		b.Loaded += () => { b.Window.Hwnd().ActivateL(); };
		if (!b.ShowDialog()) return;
		
		_sett.redirectClipboard = cSepClip.IsChecked.Value;
	}
	
	record class _Settings : JSettings {
		public static readonly string File = AppSettings.DirBS + @"PiP.json";
		
		public static _Settings Load() => Load<_Settings>(File);
		
		public string wndPos;
		public bool redirectClipboard = true;
	}
	static _Settings _sett = _Settings.Load();
}

file class FullScreenWindow {
	wnd _w;
	RECT _r;
	WS _style;
	//WSE _exStyle; //never mind
	bool _isFS, _maximized;
	
	public FullScreenWindow(wnd w) {
		_w = w;
	}
	
	public void SetFullScreen(bool on) {
		if (on) {
			if (_isFS) return;
			_maximized = _w.IsMaximized;
			_w.ShowNotMinMax();
			_r = _w.Rect;
			_style = _w.Style;
			_w.SetStyle(_style & ~(WS.CAPTION | WS.THICKFRAME));
			_isFS = true; //before setting the final rect; then eg on wm_size IsFullScreen will return true
			_w.MoveL(screen.of(_w).Rect, SWPFlags.FRAMECHANGED); //info: it automatically makes taskbar non-topmost
		} else if (_isFS) {
			_isFS = false;
			_w.MoveL(_r, SWPFlags.FRAMECHANGED);
			_w.SetStyle(_style);
			if (_maximized) _w.ShowMaximized();
		}
	}
	
	public bool IsFullScreen => _isFS;
}

#pragma warning disable 649, 169 //field never assigned/used
unsafe file class api : NativeApi {
	[DllImport("wtsapi32.dll")]
	internal static extern bool WTSIsChildSessionsEnabled(out bool pbEnabled);
	
	[DllImport("wtsapi32.dll", SetLastError = true)]
	internal static extern bool WTSEnableChildSessions(bool bEnable);
	
	internal enum DWM_WINDOW_CORNER_PREFERENCE {
		DWMWCP_DEFAULT,
		DWMWCP_DONOTROUND,
		DWMWCP_ROUND,
		DWMWCP_ROUNDSMALL
	}
	
	[DllImport("dwmapi.dll")]
	internal static extern int DwmSetWindowAttribute(wnd hwnd, uint dwAttribute, void* pvAttribute, uint cbAttribute);
	
	internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
}
