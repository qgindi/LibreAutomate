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
		readonly KCheckBox _captureCheckbox;
		readonly Func<POINT, (RECT? r, string s)> _dGetRect;
		readonly (string hotkey, Action a) _dCapture, _dInsert, _dSmaller;
		HwndSource _hs;
		timer _timer;
		internal osdRect _osr;
		internal osdText _ost; //TODO3: draw rect and text in same OsdWindow
		bool _capturing;
		const string c_propName = "Au.Capture";
		readonly static int s_stopMessage = Api.RegisterWindowMessage(c_propName);
		const int c_hotkeyCapture = 1623031890, c_hotkeyInsert = 1623031891, c_hotkeySmaller = 1623031892;
		
		/// <param name="captureCheckbox">Checkbox that turns on/off capturing.</param>
		/// <param name="getRect">Called to get rectangle of object from mouse. Receives mouse position. Can return default to hide the rectangle.</param>
		public CapturingWithHotkey(KCheckBox captureCheckbox, Func<POINT, (RECT? r, string s)> getRect, (string hotkey, Action a) capture, (string hotkey, Action a) insert = default, (string hotkey, Action a) smaller = default) {
			_captureCheckbox = captureCheckbox;
			_dGetRect = getRect;
			_dCapture = capture;
			_dInsert = insert;
			_dSmaller = smaller;
		}
		
		/// <summary>
		/// Starts or stops capturing.
		/// Does nothing if already in that state.
		/// </summary>
		public bool Capturing {
			get => _capturing;
			set {
				if (value == _capturing) return;
				if (value) {
					var wDialog = _captureCheckbox.Hwnd();
					Debug.Assert(!wDialog.Is0);
					
					//let other dialogs stop capturing
					//could instead use a static collection, but this code allows to have such tools in multiple processes, although currently it not used
					wDialog.Prop.Set(c_propName, 1);
					wnd.find(null, "HwndWrapper[*", flags: WFlags.HiddenToo | WFlags.CloakedToo, also: o => {
						if (o != wDialog && o.Prop[c_propName] == 1) o.Send(s_stopMessage);
						return false;
					});
					
					bool _RegisterHotkey(int id, string hotkey) {
						string es = null;
						try {
							var (mod, key) = RegisteredHotkey.Normalize_(hotkey);
							if (Api.RegisterHotKey(wDialog, id, mod, key)) return true;
							es = "Failed to register.";
						}
						catch (Exception e1) { es = e1.Message; }
						dialog.showError("Hotkey " + hotkey, es + "\nClick the hotkey link to set another hotkey.", owner: wDialog);
						return false;
					}
					if (!_RegisterHotkey(c_hotkeyCapture, _dCapture.hotkey)) return;
					if (_dInsert.hotkey != null) _RegisterHotkey(c_hotkeyInsert, _dInsert.hotkey);
					if (_dSmaller.hotkey != null) _RegisterHotkey(c_hotkeySmaller, _dSmaller.hotkey);
					_capturing = true;
					
					if (_hs == null) {
						_hs = PresentationSource.FromDependencyObject(_captureCheckbox) as HwndSource;
						_hs.Disposed += (_, _) => {
							Capturing = false;
							_osr?.Dispose();
							_ost?.Dispose();
						};
					}
					_hs.AddHook(_WndProc);
					
					//set timer to show rectangles of UI element from mouse
					if (_timer == null) {
						_osr = CreateOsdRect(2);
						_timer = new timer(t => {
							int t1 = Environment.TickCount;
							
							POINT p = mouse.xy;
							wnd w = wnd.fromXY(p, WXYFlags.NeedWindow);
							RECT? r = default; string text = null;
							if (!(w.Is0 || w == wDialog || (w.ThreadId == wDialog.ThreadId && w.ZorderIsAbove(wDialog)))) {
								(r, text) = _dGetRect(p);
								
								//F3 does not work if this process has lower UAC IL than the foreground process.
								//	Normally editor is admin, but if portable etc...
								//	Shift+F3 too. But Ctrl+F3 works.
								//if (w!=wPrev && w.IsActive) {
								//	w = wPrev;
								//	if(w.UacAccessDenied)print.it("F3 ");
								//}
							}
							if (r.HasValue && !t_hideCapturingRect) {
								var rr = r.Value;
								rr.Inflate(1, 1); //1 pixel inside, 1 outside
								_LimitInsaneRect(ref rr);
								_osr.Rect = rr;
								_osr.Show();
								if (!text.NE()) {
									_ost ??= new() { Font = new(8), Shadow = false, ShowMode = OsdMode.ThisThread, SecondsTimeout = -1 };
									_ost.Text = text;
									var ro = _ost.Measure();
									var rs = screen.of(rr).Rect;
									int x = rr.left, y = rr.top + 8;
									if (rr.top - rs.top >= ro.Height) y = rr.top - ro.Height; else if (rr.Height < 200) y = rr.bottom; else x += 8;
									_ost.XY = new(x, y, false);
									_ost.Show();
								} else if (_ost != null) _ost.Visible = false;
							} else {
								_osr.Visible = false;
								if (_ost != null) _ost.Visible = false;
							}
							
							_timer.After(Math.Min(Environment.TickCount - t1 + 200, 2000)); //normally the timer priod is ~250 ms
						});
					}
					_timer.After(250);
				} else {
					_capturing = false;
					_hs.RemoveHook(_WndProc);
					wnd wDialog = (wnd)_hs.Handle;
					Debug.Assert(wDialog.IsAlive);
					Api.UnregisterHotKey(wDialog, c_hotkeyCapture);
					if (_dInsert.hotkey != null) Api.UnregisterHotKey(wDialog, c_hotkeyInsert);
					if (_dSmaller.hotkey != null) Api.UnregisterHotKey(wDialog, c_hotkeySmaller);
					wDialog.Prop.Remove(c_propName);
					_timer.Stop();
					_osr.Hide();
					_ost?.Hide();
				}
			}
		}
		
		nint _WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) {
			if (msg == s_stopMessage) {
				handled = true;
				_captureCheckbox.IsChecked = false;
			} else if (msg == Api.WM_HOTKEY && (wParam is c_hotkeyCapture or c_hotkeyInsert or c_hotkeySmaller)) {
				handled = true;
				if (wParam == c_hotkeyInsert) _dInsert.a();
				else if (wParam == c_hotkeySmaller) _dSmaller.a();
				else _dCapture.a();
			}
			return default;
		}
		
		/// <summary>
		/// Adds link +hotkey that shows dialog "Hotkeys" and updates LA.App.Settings.delm.hk_x.
		/// </summary>
		public static void RegisterLink_DialogHotkey(KSciInfoBox sci) {
			if (sci.AaTags.HasLinkTag("+hotkey")) return;
			sci.AaTags.AddLinkTag("+hotkey", _ => {
				var b = new wpfBuilder("Hotkey");
				b.R.xAddGroupSeparator("In wnd and elm tools");
				b.R.Add("Capture", out TextBox capture, LA.App.Settings.delm.hk_capture).xValidateHotkey(errorIfEmpty: true).Focus();
				b.R.xAddGroupSeparator("In elm tool");
				b.R.Add("Insert code", out TextBox insert, LA.App.Settings.delm.hk_insert).xValidateHotkey();
				b.R.Add("Capturing method", out TextBox smaller, LA.App.Settings.delm.hk_smaller).xValidateHotkey();
				b.R.AddSeparator(false);
				b.R.xAddInfoBlockT("After changing hotkeys please restart the tool window.");
				if (!uacInfo.isAdmin) b.R.xAddInfoBlockT("Hotkeys don't work when the active window is admin,\nbecause this process isn't admin.");
				b.R.AddOkCancel();
				b.End();
				if (b.ShowDialog(Window.GetWindow(sci))) {
					LA.App.Settings.delm.hk_capture = capture.Text;
					LA.App.Settings.delm.hk_insert = insert.TextOrNull();
					LA.App.Settings.delm.hk_smaller = smaller.TextOrNull();
				}
			});
		}
	}
}
