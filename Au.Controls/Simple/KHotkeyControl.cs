using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;

namespace Au.Controls;

public class KHotkeyControl : UserControl {
	TextBox _tHotkey;
	CheckBox _cCtrl, _cShift, _cAlt, _cWin;
	WindowsHook _hook;
	KKey _key;
	bool _noEvents;
	bool _forTrigger, _onlyMod;
	
	public KHotkeyControl(bool forTrigger = false, bool onlyMod = false) {
		_forTrigger = forTrigger;
		_onlyMod = onlyMod;
		
		var b = new wpfBuilder(this);
		if (_onlyMod) b.Columns(0, 0, 0, 0, -1); else if (_forTrigger) b.Columns(-1, 0, 0, 0, 0); else b.Columns(-1);
		b.Options(margin: _forTrigger || _onlyMod ? new(0, 0, 10, 0) : new());
		
		if (!_onlyMod) {
			b.R.Add<AdornerDecorator>().Add(out _tHotkey, flags: WBAdd.ChildOfLast).Readonly(caretVisible: true)
				.Watermark("Hotkey")
				.Tooltip("Focus this field and press a key with any combination of Ctrl, Shift, Alt, Win");
		}
		
		if (_forTrigger || _onlyMod) {
			b.Add(out _cCtrl, "Ctrl").Add(out _cShift, "Shift").Add(out _cAlt, "Alt").Add(out _cWin, "Win");
			foreach (var c in b.Panel.Children.OfType<CheckBox>()) (c.IsThreeState, c.IsEnabled) = (_forTrigger, _onlyMod);
			RoutedEventHandler modCheck = (_, _) => {
				if (_noEvents) return;
				KMod mod = 0, modAny = 0;
				_Mod(KMod.Ctrl, _cCtrl);
				_Mod(KMod.Shift, _cShift);
				_Mod(KMod.Alt, _cAlt);
				_Mod(KMod.Win, _cWin);
				_Format(mod, modAny);
				
				void _Mod(KMod m, CheckBox cb) {
					bool? c = cb.IsChecked;
					if (c != false) { mod |= m; if (c == null) modAny |= m; }
				}
			};
			_cCtrl.Checked += modCheck;
			_cCtrl.Unchecked += modCheck;
			_cCtrl.Indeterminate += modCheck;
			_cShift.Checked += modCheck;
			_cShift.Unchecked += modCheck;
			_cShift.Indeterminate += modCheck;
			_cAlt.Checked += modCheck;
			_cAlt.Unchecked += modCheck;
			_cAlt.Indeterminate += modCheck;
			_cWin.Checked += modCheck;
			_cWin.Unchecked += modCheck;
			_cWin.Indeterminate += modCheck;
		}
		
		if (!_onlyMod) {
			_tHotkey.GotKeyboardFocus += (_, _) => {
				_hook = WindowsHook.Keyboard(k => {
					if (k.IsUp) return;
					KMod mod = 0;
					if (k.Mod == 0) {
						_key = k.Key;
						mod = keys.getMod();
						k.BlockEvent();
					} else {
						if (_key == 0) return;
						_key = 0;
					}
					
					_Format(mod);
					
					if (_forTrigger) {
						_noEvents = true;
						_cCtrl.IsChecked = mod.Has(KMod.Ctrl);
						_cShift.IsChecked = mod.Has(KMod.Shift);
						_cAlt.IsChecked = mod.Has(KMod.Alt);
						_cWin.IsChecked = mod.Has(KMod.Win);
						_noEvents = false;
						bool en = _key != 0;
						_cCtrl.IsEnabled = en;
						_cShift.IsEnabled = en;
						_cAlt.IsEnabled = en;
						_cWin.IsEnabled = en;
					}
				});
			};
			
			_tHotkey.LostKeyboardFocus += (_, _) => {
				_hook?.Dispose();
				_hook = null;
			};
		}
	}
	
	void _Format(KMod mod, KMod modAny = 0) {
		if (_onlyMod ? mod != 0 : _key != 0) {
			StringBuilder b = new();
			if (modAny == (KMod)15) {
				b.Append("?+");
			} else {
				_Mod(KMod.Ctrl);
				_Mod(KMod.Shift);
				_Mod(KMod.Alt);
				_Mod(KMod.Win);
				
				void _Mod(KMod m) {
					if (!mod.Has(m)) return;
					b.Append(m);
					if (modAny.Has(m)) b.Append('?');
					b.Append('+');
				}
			}
			if (_onlyMod) b.Remove(b.Length - 1, 1); else b.Append(_key);
			Result = b.ToString();
			if (!_onlyMod) {
				_tHotkey.Text = Result;
				_tHotkey.CaretIndex = Result.Length;
			}
		} else {
			Result = null;
			_tHotkey?.Clear();
		}
	}
	
	/// <summary>
	/// Returns the hotkey string or null.
	/// </summary>
	public string Result { get; private set; }
	
	///
	public new void Focus() {
		_tHotkey?.Focus();
	}
}
