using Au.Controls;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;

namespace ToolLand;

static partial class TUtil {
	/// <summary>
	/// Common code for tools that capture UI objects with F3.
	/// </summary>
	public class CapturingWithHotkey {
		public record struct HotkeyActions(Action capture, Action insert = null, Action smaller = null);
		
		readonly bool _isElm;
		readonly wnd _wDialog;
		readonly KCheckBox _captureCheckbox;
		readonly Func<GetRectArgs, bool> _getRect;
		HotkeyActions _hkActions;
		List<int> _hkUnregister = [];
		HwndSource _hs;
		timer _timer;
		osdRect _osr;
		osdText _ost; //TODO3: draw rect and text in same OsdWindow
		bool _capturing;
		
		const string c_propName = "Au.Capture";
		readonly static int s_stopMessage = Api.RegisterWindowMessage(c_propName);
		const int c_hotkeyCapture = 1623031890, c_hotkeyInsert = 1623031891, c_hotkeySmaller = 1623031892;
		
		/// <param name="captureCheckbox">Checkbox that turns on/off capturing.</param>
		/// <param name="getRect">Called to get rectangle of object from mouse. Receives mouse position. Can return default to hide the rectangle.</param>
		public CapturingWithHotkey(KCheckBox captureCheckbox, Func<GetRectArgs, bool> getRect, HotkeyActions hkActions, bool isElm = false) {
			_wDialog = captureCheckbox.Hwnd();
			_captureCheckbox = captureCheckbox;
			_getRect = getRect;
			_hkActions = hkActions;
			_isElm = isElm;
		}
		
		/// <summary>
		/// Starts or stops capturing.
		/// Does nothing if already in that state.
		/// </summary>
		public bool Capturing {
			get => _capturing;
			set {
				if (value == _capturing) return;
				if (value) _StartCapturing(); else _StopCapturing();
			}
		}
		
		void _StopCapturing() {
			_capturing = false;
			_timer.Stop();
			_HideRect();
			_hs.RemoveHook(_WndProc);
			foreach (var v in _hkUnregister) Api.UnregisterHotKey(_wDialog, v);
			_hkUnregister.Clear();
			_wDialog.Prop.Remove(c_propName);
		}
		
		void _StartCapturing() {
			//let other tools stop capturing. Any process.
			wnd.find(null, "HwndWrapper[*", flags: WFlags.HiddenToo | WFlags.CloakedToo, also: o => {
				if (o != _wDialog && o.Prop[c_propName] == 1) o.SendTimeout(3000, out _, s_stopMessage);
				return false;
			});
			_wDialog.Prop.Set(c_propName, 1);
			
			//register hotkeys
			bool _RegisterHotkey(int id) {
				string hotkey = id switch {
					c_hotkeyCapture => LA.App.Settings.delm.hk_capture,
					c_hotkeyInsert => LA.App.Settings.delm.hk_insert,
					c_hotkeySmaller => LA.App.Settings.delm.hk_smaller,
					_ => null
				};
				if (hotkey.NE()) return false;
				string es = null;
				try {
					var (mod, key) = RegisteredHotkey.Normalize_(hotkey);
					if (Api.RegisterHotKey(_wDialog, id, mod, key)) {
						_hkUnregister.Add(id);
						return true;
					}
					es = "Failed to register.";
				}
				catch (Exception e1) { es = e1.Message; }
				
				dialog.showWarning("Hotkey " + hotkey, es + "\nYou can change hotkeys in Options > Hotkeys.", owner: _wDialog);
				return false;
			}
			if (!_RegisterHotkey(c_hotkeyCapture)) return;
			if (_hkActions.insert != null) _RegisterHotkey(c_hotkeyInsert);
			if (_hkActions.smaller != null) _RegisterHotkey(c_hotkeySmaller);
			
			//hook wndproc
			if (_hs == null) {
				_hs = PresentationSource.FromDependencyObject(_captureCheckbox) as HwndSource;
				_hs.Disposed += (_, _) => {
					Capturing = false;
					_osr?.Dispose();
					_ost?.Dispose();
				};
			}
			_hs.AddHook(_WndProc);
			
			//set timer to show rectangle of UI element from mouse
			if (_timer == null) {
				_osr = CreateOsdRect(2);
				_timer = new timer(_ => {
					if (!_capturing) return;
					POINT p = mouse.xy;
					if (_isElm) {
						_elmTimer.Timer(p);
					} else {
						_ShowRect(p, wnd.fromXY(p, WXYFlags.NeedWindow));
					}
				});
			}
			if (_isElm) _elmTimer = new(this);
			_timer.Every(_isElm ? 50 : 200);
			
			_capturing = true;
		}
		_ElmTimer _elmTimer;
		
		class _ElmTimer {
			CapturingWithHotkey _capt;
			POINT _pMM;
			bool _mouseMoved;
			long _timeMM, _timeUpdated;
			wnd _w;
			
			public _ElmTimer(CapturingWithHotkey capt) {
				_capt = capt;
				_pMM.x = int.MinValue;
			}
			
			public void Timer(POINT p) {
				if (mouse.isPressed()) {
					_capt._HideRect();
					if (p == _pMM) return;
				}
				
				//get rect when mouse stops. With a delay if stops in a new window.
				if (p != _pMM) {
					if (_capt._osr.Visible && !_capt._osr.Rect.Contains(p)) _capt._HideRect();
					_pMM = p;
					_mouseMoved = true;
					_timeMM = Environment.TickCount64;
				} else {
					wnd w = wnd.fromXY(p, WXYFlags.NeedWindow);
					bool sameWindow = !_w.Is0 && (w == _w || w.ThreadId == _w.ThreadId);
					bool update = false;
					bool idle = sameWindow && !_mouseMoved;
					if (idle) { //repeat _ShowRect anyway, because may scroll, close window, etc
						long timeNow = Environment.TickCount64;
						long liTime = Api.GetLastInputTime();
						if (timeNow - liTime > 700) {
							long timeNotUpdated = timeNow - _timeUpdated;
							update = timeNotUpdated > 1000 || (liTime > _timeUpdated && timeNotUpdated > 300);
						}
					} else {
						update = sameWindow || Environment.TickCount64 - _timeMM >= 230;
					}
					if (update) {
						//print.it("update rect");
						long t1 = Environment.TickCount64;
						_capt._ShowRect(p, _w = w);
						long t2 = Environment.TickCount64;
						_timeUpdated = t2 + (t2 - t1) * 2;
					}
					_mouseMoved = false;
				}
			}
		}
		
		void _ShowRect(POINT p, wnd w) {
			bool ok = false; RECT r = default; string text = null;
			if (!(w.Is0 || w == _wDialog || (w.IsOfThisThread && w.ZorderIsAbove(_wDialog)))) {
				var k = new GetRectArgs(p, w);
				if (ok = _getRect(k)) (r, text) = (k.resultRect, k.resultText);
				
				//F3 does not work if this process has lower UAC IL than the foreground process.
				//	Normally editor is admin, but if portable etc...
				//	Shift+F3 too. But Ctrl+F3 works.
				//if (w!=wPrev && w.IsActive) {
				//	w = wPrev;
				//	if(w.UacAccessDenied)print.it("F3 ");
				//}
			}
			if (ok && !t_hideCapturingRect) {
				r.Inflate(1, 1); //1 pixel inside, 1 outside
				_LimitInsaneRect(ref r);
				_osr.Rect = r;
				_osr.Show();
				if (!text.NE()) {
					_ost ??= new() { Font = new(8), Shadow = false, ShowMode = OsdMode.ThisThread, SecondsTimeout = -1 };
					_ost.Text = text;
					var ro = _ost.Measure();
					var rs = screen.of(r).Rect;
					int x = r.left, y = r.top + 8;
					if (r.top - rs.top >= ro.Height) y = r.top - ro.Height; else if (r.Height < 200) y = r.bottom; else x += 8;
					_ost.XY = new(x, y, false);
					_ost.Show();
				} else _ost?.Visible = false;
			} else {
				_HideRect();
			}
		}
		
		void _HideRect() {
			_osr.Hide();
			_ost?.Hide();
		}
		
		public record class GetRectArgs(POINT p, wnd wTL) {
			public RECT resultRect;
			public string resultText;
		}
		
		nint _WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) {
			if (msg == s_stopMessage) {
				handled = true;
				_captureCheckbox.IsChecked = false;
			} else if (msg == Api.WM_HOTKEY && (wParam is c_hotkeyCapture or c_hotkeyInsert or c_hotkeySmaller)) {
				handled = true;
				if (wParam == c_hotkeyInsert) _hkActions.insert();
				else if (wParam == c_hotkeySmaller) _hkActions.smaller();
				else _hkActions.capture();
			}
			return default;
		}

		public UsingEndAction TempHideRect() {
			bool v1, v2;
			if (v1 = _osr.Visible) _osr.Hwnd.ShowL(false);
			if (v2 = _ost?.Visible == true) _ost.Hwnd.ShowL(false);
			return new(() => {
				if (v1) _osr.Hwnd.ShowL(true);
				if (v2) _ost.Hwnd.ShowL(true);
			});
		}
	}
}
