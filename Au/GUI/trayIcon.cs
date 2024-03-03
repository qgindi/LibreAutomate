namespace Au {
	/// <summary>
	/// Shows tray icon.
	/// </summary>
	/// <remarks>
	/// Wraps API <msdn>Shell_NotifyIconW</msdn>, <msdn>NOTIFYICONDATAW</msdn>. More info there.
	/// 
	/// This thread must dispatch messages.
	/// 
	/// Can be used by multiple threads (eg one thread adds tray icon and other thread later changes its tooltip).
	/// 
	/// Creates a hidden window that receives tray icon events (click etc).
	/// </remarks>
	public class trayIcon : IDisposable {
		readonly int _id;
		readonly bool _disposeOnExit;
		wnd _w;
		
		//rejected. Various problems, eg the program file cannot be moved. Unclear documentation.
		//	Guid _guid;
		
		static trayIcon() {
			s_msgTaskbarCreated = WndUtil.RegisterMessage("TaskbarCreated", uacEnable: true);
			WndUtil.RegisterWindowClass("trayIcon");
		}
		static int s_msgTaskbarCreated;
		
		/// <summary>
		/// Tray icon notification message.
		/// </summary>
		protected const int MsgNotify = Api.WM_USER + 145;
		
		/// <param name="id">An id that helps Windows to distinguish multiple tray icons added by same program. Use 0, 1, 2, ... or all 0.</param>
		/// <param name="disposeOnExit">
		/// Remove tray icon when process exits (<see cref="process.thisProcessExit"/>).
		/// Note: can't remove if the process terminated or called <see cref="Environment.FailFast"/> or API <msdn>ExitProcess</msdn>.
		/// </param>
		public trayIcon(int id = 0, bool disposeOnExit = true) {
			_disposeOnExit = disposeOnExit;
			_id = Hash.Fnv1(script.name) + id;
			///// <param name="guid">A GUID that identifies the tray icon.</param>
			//_guid=guid;
		}
		
		/// <summary>
		/// Removes tray icon and disposes other resources.
		/// </summary>
		public void Dispose() { Dispose(true); }
		
		///
		protected virtual void Dispose(bool disposing) { _Delete(disposing: true); }
		
		void _Delete(bool disposing = false) {
			lock (this) {
				if (_visible) {
					Api.Shell_NotifyIcon(Api.NIM_DELETE, _NewND());
					_visible = false;
				}
				if (disposing && !_w.Is0) {
					if (!Api.DestroyWindow(_w)) _w.Post(Api.WM_CLOSE);
					_w = default;
				}
			}
		}
		
		/// <summary>
		/// Gets or sets whether the tray icon is visible.
		/// </summary>
		/// <exception cref="InvalidOperationException">Icon not set.</exception>
		public bool Visible {
			get => _visible;
			set {
				lock (this) {
					if (value != _visible) {
						if (value && _icon == null) throw new InvalidOperationException("Icon not set"); //but allow ShowNotification without icon. This is to prevent using slow code like new trayIcon() { Visible=true, Icon=... }.
						if (value) _Update();
						else _Delete();
					}
				}
			}
		}
		bool _visible;
		
		/// <summary>
		/// Gets or sets icon.
		/// </summary>
		/// <remarks>
		/// To display nice icon at any DPI, the icon should be loaded with <see cref="icon.trayIcon"/> or API <msdn>LoadIconMetric</msdn>, either from a native resource in your app or from an <c>.ico</c> file, which should contain icons of sizes 16, 32 and also recommended 20, 24.
		/// </remarks>
		public icon Icon {
			get => _icon;
			set {
				lock (this) {
					if (value != _icon) {
						_icon = value;
						if (_visible) _Update(icon: true);
					}
				}
			}
		}
		icon _icon;
		
		/// <summary>
		/// Gets or sets tooltip text.
		/// </summary>
		/// <remarks>
		/// Max length of displayed tooltip text is 127.
		/// </remarks>
		public string Tooltip {
			get => _tooltip;
			set {
				lock (this) {
					if (value != _tooltip) {
						_tooltip = value;
						if (_visible) _Update(tooltip: true);
					}
				}
			}
		}
		string _tooltip;
		
		Api.NOTIFYICONDATA _NewND(uint nifFlags = 0, bool setTT = false) {
			var d = new Api.NOTIFYICONDATA(_w, nifFlags) { uID = _id, uCallbackMessage = MsgNotify, uVersion = Api.NOTIFYICON_VERSION_4 };
			//if (_guid!=default) { d.uFlags|=Api.NIF_GUID; d.guidItem=_guid; }
			if (!_customPopup) d.uFlags |= Api.NIF_SHOWTIP;
			if (setTT) {
				d.uFlags |= Api.NIF_TIP;
				d.szTip = _customPopup ? " " : _tooltip?.Limit(127);
			}
			return d;
		}
		
		bool _Update(bool icon = false, bool tooltip = false, _Notification n = null, bool taskbarCreated = false) {
			if (script.Exiting_) return false;
			lock (this) {
				if (_w.Is0) {
					if (_disposeOnExit) process.thisProcessExit += _ => _Delete();
					_w = WndUtil.CreateWindow(WndProc, true, "trayIcon", script.name, WS.POPUP, WSE.NOACTIVATE);
				}
				
				if (taskbarCreated) _visible = false;
				
				if (!_visible) { icon = _icon != null; tooltip = !_tooltip.NE(); }
				var d = _NewND(Api.NIF_MESSAGE, setTT: tooltip || _customPopup);
				if (icon) { d.uFlags |= Api.NIF_ICON; d.hIcon = _icon; }
				
				if (n != null) {
					d.uFlags |= Api.NIF_INFO;
					if (n.flags.Has(TINFlags.Realtime)) d.uFlags |= Api.NIF_REALTIME;
					d.dwInfoFlags = (uint)(n.flags & ~TINFlags.Realtime);
					if (n.icon != null) { d.dwInfoFlags |= 0x24; d.hBalloonIcon = n.icon; } //user icon, large
					d.szInfoTitle = n.title?.Limit(63);
					d.szInfo = n.text?.Limit(255);
					//if(!_visible) if(_icon==null && _tooltip.NE()) { d.uFlags|=Api.NIF_STATE; d.dwState=d.dwStateMask=1; } //hidden icon. But then no notification.
				}
				
				int nim = _visible ? Api.NIM_MODIFY : Api.NIM_ADD;
				bool ok = Api.Shell_NotifyIcon(nim, d);
				if (!ok && 4 == (d.dwInfoFlags & 15)) {
					//fails if size of custom icon of notification != XM_CXSMICON (if no NIIF_LARGE_ICON) or < SM_CXICON (if NIIF_LARGE_ICON).
					//	tested: Even if now taskbar DPI is different (changed since this process started), need icon of these sizes (they didn't change since this process started).
					//	Workaround: Remove the user icon flag and print warning.
					//tested: Win7 displays XM_CXSMICON or SM_CXICON depending on NIIF_LARGE_ICON. Win8.1 too. Win10 displays SM_CXICON*1.5 regardless of NIIF_LARGE_ICON.
					d.dwInfoFlags &= ~15u;
					ok = Api.Shell_NotifyIcon(nim, d);
					if (ok) print.warning("Custom icon size must be >= trayIcon.notificationIconSize");
				}
				if (!_visible) {
					if (ok || taskbarCreated) ok = Api.Shell_NotifyIcon(Api.NIM_SETVERSION, d);
					_visible = true;
					//note: Shell_NotifyIcon(NIM_ADD) fails if already added, eg when "TaskbarCreated" message received when taskbar DPI changed.
					//note: Win10 sets last error, but Win7 doesn't.
				}
				//if(!ok) print.it("Shell_NotifyIcon: ", lastError.message);
				return ok;
			}
		}
		
		/// <summary>
		/// Shows temporary notification window by the tray icon.
		/// </summary>
		/// <param name="title">Title, max 63 characters.</param>
		/// <param name="text">Text, max 255 characters.</param>
		/// <param name="flags">Standard icon and other flags.</param>
		/// <param name="icon">Custom icon. Important: use icon of size returned by <see cref="notificationIconSize"/>.</param>
		/// <remarks>
		/// If the tray icon isn't visible, makes it visible.
		/// 
		/// No more than one notification at a time can be displayed. If an application attempts to display a notification when one is already being displayed, the new notification is queued and displayed when the older notification goes away.
		/// 
		/// Users may choose to not show notifications, depending on various conditions. Look in Windows Settings app, Notifications &amp; actions.
		/// </remarks>
		public void ShowNotification(string title, string text, TINFlags flags = 0, icon icon = default) {
			if (!_Update(n: new(title, text, flags, icon))) print.warning("ShowNotification() failed. " + lastError.message);
		}
		
		record class _Notification(string title, string text, TINFlags flags, icon icon);
		
		/// <summary>
		/// Hides notification.
		/// </summary>
		public void HideNotification() {
			lock (this) {
				if (_visible) Api.Shell_NotifyIcon(Api.NIM_MODIFY, _NewND(Api.NIF_INFO));
			}
		}
		
		/// <summary>
		/// Gets icon size for <see cref="ShowNotification"/>.
		/// </summary>
		public static int notificationIconSize {
			get {
				int r = Api.GetSystemMetrics(Api.SM_CXICON);
				if (osVersion.minWin10) r = r * 3 / 2;
				return r;
			}
		}
		
		/// <summary>
		/// Activates taskbar and makes the tray icon focused for keyboard.
		/// </summary>
		/// <remarks>
		/// If the tray icon is in hidden overflow area, makes the area button focused.
		/// </remarks>
		public void Focus() {
			lock (this) {
				Api.Shell_NotifyIcon(Api.NIM_SETFOCUS, _NewND());
			}
		}
		
		/// <summary>
		/// Together with <see cref="Hwnd"/> identifies this tray icon.
		/// </summary>
		protected int Id => _id;
		
		/// <summary>
		/// A hidden window automatically created for this tray icon to receive its notifications.
		/// </summary>
		protected internal wnd Hwnd => _w;
		
		/// <summary>
		/// Window procedure of the hidden window that receives tray icon notifications (<see cref="MsgNotify"/>) in version 4 format.
		/// If you override it, call the base function.
		/// </summary>
		protected virtual nint WndProc(wnd w, int msg, nint wParam, nint lParam) {
			if (_visible) {
				if (msg == MsgNotify) {
					int m = Math2.LoWord(lParam); POINT p = Math2.NintToPOINT(wParam);
					//if(m!=Api.WM_MOUSEMOVE) print.it(m, p);
					Message?.Invoke(new(m, p));
					switch (m) {
					case Api.WM_CONTEXTMENU:
						RightClick?.Invoke(new(m, p));
						break;
					case Api.NIN_SELECT: //mouse click or double click. After WM_LBUTTONUP.
					case Api.NIN_KEYSELECT: //Space, Enter (never mind bug: 2 NIN_KEYSELECT on Enter), DoDefaultAction
						Click?.Invoke(new(m, p));
						break;
					case Api.WM_MBUTTONUP:
						MiddleClick?.Invoke(new(m, p));
						break;
					case Api.NIN_BALLOONUSERCLICK:
						NotificationClick?.Invoke(new(m, p));
						break;
					case Api.NIN_POPUPOPEN: //tested: on Win10 only if no NIF_SHOWTIP, but on Win7 always
						if (_customPopup) _popupOpen?.Invoke(new(m, p));
						break;
					case Api.NIN_POPUPCLOSE:
						if (_customPopup) PopupClose?.Invoke(new(m, p));
						break;
					}
				} else if (msg == s_msgTaskbarCreated) { //explorer restarted or taskbar DPI changed
					_Update(taskbarCreated: true);
				}
			}
			
			var R = Api.DefWindowProc(w, msg, wParam, lParam);
			
			if (msg == Api.WM_NCDESTROY) {
				_Delete();
			}
			
			return R;
		}
		
		/// <summary>
		/// When received any message from the tray icon.
		/// </summary>
		/// <remarks>
		/// Receives mouse messages, <b>NIN_</b> messages and some other. See <msdn>Shell_NotifyIconW</msdn>.
		/// </remarks>
		public event Action<TIEventArgs> Message;
		
		/// <summary>
		/// When default action should be invoked (on click, Space/Enter, automation/accessibility API).
		/// </summary>
		/// <remarks>
		/// If clicked, the parameter contains message <b>NIN_SELECT</b> (1024) and mouse coordinates. Else <b>NIN_KEYSELECT</b> (1025) and top-left of the tray icon.
		/// On double click there are two <b>Click</b> events. To distinguish click and double click events, use <see cref="Message"/> instead.
		/// </remarks>
		public event Action<TIEventArgs> Click;
		
		/// <summary>
		/// When a context menu should be shown (on right click or Apps key).
		/// </summary>
		public event Action<TIEventArgs> RightClick;
		
		/// <summary>
		/// When the tray icon clicked with the middle button.
		/// </summary>
		[Obsolete("Does not work on Windows 11.")]
		public event Action<TIEventArgs> MiddleClick;
		
		/// <summary>
		/// When clicked the notification window.
		/// </summary>
		public event Action<TIEventArgs> NotificationClick;
		
		/// <summary>
		/// When it's time to close custom tooltip etc shown on <see cref="PopupOpen"/>.
		/// </summary>
		public event Action<TIEventArgs> PopupClose;
		
		/// <summary>
		/// When it's time to open custom tooltip or some temporary popup window.
		/// If this event is used, does not show standard tooltip.
		/// </summary>
		public event Action<TIEventArgs> PopupOpen {
			add => _SetPopupOpen(true, value);
			remove => _SetPopupOpen(false, value);
		}
		
		event Action<TIEventArgs> _popupOpen;
		bool _customPopup;
		
		void _SetPopupOpen(bool add, Action<TIEventArgs> a) {
			if (add) _popupOpen += a; else _popupOpen -= a;
			bool customPopup = _popupOpen != null;
			if (customPopup != _customPopup) {
				_customPopup = customPopup;
				if (_visible) Api.Shell_NotifyIcon(Api.NIM_MODIFY, _NewND(setTT: true));
			}
		}
		
		/// <summary>
		/// Gets tray icon rectangle in screen.
		/// </summary>
		/// <returns><c>false</c> if failed, for example if the icon is in hidden overflow area. Supports <see cref="lastError"/>.</returns>
		public unsafe bool GetRect(out RECT r) {
			var x = new Api.NOTIFYICONIDENTIFIER { cbSize = sizeof(Api.NOTIFYICONIDENTIFIER), hWnd = _w, uID = _id };
			//if (_guid!=default) x.guidItem=_guid;
			int hr = Api.Shell_NotifyIconGetRect(x, out r);
			if (hr == 0) return true;
			lastError.code = hr;
			return false;
		}
		
		//	public bool GetPopupRect(int width, int height) {
		//		if(!_visible) return false;
		//		//what if hidden in overflow area? Then should not show popup. Then OS does not show notifications.
		//		//CalculatePopupWindowPosition
		//	}
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="trayIcon.ShowNotification"/>. See <b>NIIF_</b> flags of API <msdn>NOTIFYICONDATAW</msdn>.
	/// </summary>
	[Flags]
	public enum TINFlags {
#pragma warning disable 1591 //no doc
		InfoIcon = 1,
		WarningIcon,
		ErrorIcon,
		//UserIcon,
		NoSound = 0x10,
		//LargeIcon=0x20,
		//RespectQuietTime=0x80,
#pragma warning restore
		
		/// <summary>
		/// Flag <b>NIF_REALTIME</b>.
		/// </summary>
		Realtime = 0x10000000,
	}
	
#pragma warning disable 1591
	public record class TIEventArgs(int Message, POINT XY);
#pragma warning restore
	
}