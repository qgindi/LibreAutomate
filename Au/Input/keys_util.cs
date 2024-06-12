namespace Au;

public partial class keys {
	/// <summary>
	/// Converts part of string to <see cref="KKey"/>.
	/// The substring should contain single key name, eg <c>"Esc"</c>, <c>"A"</c>, <c>"="</c>.
	/// Returns 0 if invalid key name.
	/// </summary>
	static unsafe KKey _KeynameToKey(string s, int i, int len) {
		//print.it(s, i, len);
		if (len < 1) return 0;

		char c = s[i];

		//character keys, like K, 9, -
		if (len == 1) {
			return c switch {
				>= 'a' and <= 'z' => (KKey)(c - 32),
				>= 'A' and <= 'Z' => (KKey)c,
				>= '0' and <= '9' => (KKey)c,
				'-' => KKey.OemMinus,
				'=' => KKey.OemPlus,
				'`' or '~' => KKey.OemTilde,
				'[' or '{' => KKey.OemOpenBrackets,
				']' or '}' => KKey.OemCloseBrackets,
				'\\' or '|' => KKey.OemPipe,
				';' or ':' => KKey.OemSemicolon,
				'\'' or '"' => KKey.OemQuotes,
				',' or '<' => KKey.OemComma,
				'.' or '>' => KKey.OemPeriod,
				'/' or '?' => KKey.OemQuestion,
				_ => 0,
			};

			//special
			//+ //eg Ctrl+A
			//* //*nTimes, *down, *up
			//( //eg Alt+(A F)
			//)
			//# //numpad keys, eg #5, #*
			//_ //character
			//^ //characters

			//reserved
			//! @ $ % &
		}

		//numpad keys
		if (c == '#') {
			if (len != 2) return 0;
			c = s[i + 1];
			return c switch {
				>= '0' and <= '9' => (KKey)(c - '0' + (int)KKey.NumPad0),
				'.' => KKey.Decimal,
				'+' => KKey.Add,
				'/' => KKey.Divide,
				'*' => KKey.Multiply,
				'-' => KKey.Subtract,
				_ => 0,
			};
		}

		//F keys
		if (c == 'F' && s[i + 1].IsAsciiDigit()) {
			int n = s.ToInt(i + 1, out int e, STIFlags.NoHex);
			if (n > 0 && n <= 24 && e == i + len) return (KKey)(0x6F + n);
		}

		//named keys
		//names start with an uppercase letter and must have at least 2 other anycase letters, except: Up, AltG (RAl), PageU (PgU), PageD (PgD), some alternative names (PU, PD, PB, PS, HM, SL, CL, NL, BS).
		KKey k = 0;
		char c1 = char.ToLowerInvariant(s[i + 1]), //note: Tables_.LowerCase would make startup slow
			c2 = len > 2 ? char.ToLowerInvariant(s[i + 2]) : ' ',
			c3 = len > 3 ? char.ToLowerInvariant(s[i + 3]) : ' ',
			c4 = len > 4 ? char.ToLowerInvariant(s[i + 4]) : ' ';
		uint u = (uint)c1 << 16 | c2;
		switch (c) {
		case 'A':
			if (_U('l', 't')) k = c3 == 'g' ? KKey.RAlt : KKey.Alt;
			else if (_U('p', 'p')) k = KKey.Apps;
			break;
		case 'B':
			if (_U('a', 'c') || _U('s', ' ')) k = KKey.Back;
			break;
		case 'C':
			if (_U('t', 'r')) k = KKey.Ctrl;
			else if (_U('a', 'p') || _U('l', ' ')) k = KKey.CapsLock;
			break;
		case 'D':
			if (_U('e', 'l')) k = KKey.Delete;
			else if (_U('o', 'w')) k = KKey.Down;
			break;
		case 'E':
			if (_U('n', 't')) k = KKey.Enter;
			else if (_U('n', 'd')) k = KKey.End;
			else if (_U('s', 'c')) k = KKey.Escape;
			break;
		case 'H':
			if (_U('o', 'm') || _U('m', ' ')) k = KKey.Home;
			break;
		case 'I':
			if (_U('n', 's')) k = KKey.Insert;
			break;
		case 'L':
			if (_U('e', 'f')) k = KKey.Left;
			//don't need LShift etc
			break;
		case 'M':
			if (_U('e', 'n')) k = KKey.Apps;
			break;
		case 'N':
			if (_U('u', 'm') || _U('l', ' ')) k = KKey.NumLock;
			//for NumEnter use keys.send((KKey.Enter, 0, true))
			break;
		case 'P':
			if (_U('a', 'g') && c3 == 'e') k = c4 == 'u' ? KKey.PageUp : (c4 == 'd' ? KKey.PageDown : 0);
			else if (_U('g', 'u') || _U('u', ' ')) k = KKey.PageUp;
			else if (_U('g', 'd') || _U('d', ' ')) k = KKey.PageDown;
			else if (_U('a', 'u') || _U('b', ' ')) k = KKey.Pause;
			else if (_U('r', 'i') || _U('r', 't') || _U('s', ' ')) k = KKey.PrintScreen;
			break;
		case 'R':
			if (_U('i', 'g')) k = KKey.Right;
			else if (_U('a', 'l')) k = KKey.RAlt;
			else if (_U('c', 't')) k = KKey.RCtrl;
			else if (_U('s', 'h')) k = KKey.RShift;
			else if (_U('w', 'i')) k = KKey.RWin;
			break;
		case 'S':
			if (_U('h', 'i')) k = KKey.Shift;
			else if (_U('p', 'a')) k = KKey.Space;
			else if (_U('c', 'r') || _U('l', ' ')) k = KKey.ScrollLock;
			//SysRq not used on Windows
			break;
		case 'T':
			if (_U('a', 'b')) k = KKey.Tab;
			break;
		case 'U':
			if (c1 == 'p') k = KKey.Up;
			break;
		case 'V':
			if (c1 == 'k') {
				int v = s.ToInt(i + 2, out int end, STIFlags.DontSkipSpaces);
				if (end != i + len || (uint)v > 255) v = 0;
				return (KKey)v;
			}
			break;
		case 'W':
			if (_U('i', 'n')) k = KKey.Win;
			break;
		}
		if (k != 0) {
			for (int i2 = i + len; i < i2; i++) if (!s[i].IsAsciiAlpha()) return 0;
			return k;
		}

		if (c >= 'A' && c <= 'Z') {
			var s1 = s.Substring(i, len);
#if false
			if(Enum.TryParse(s1, true, out KKey r1)) return r1;
			//if(Enum.TryParse(s1, true, out System.Windows.Forms.Keys r2) && (uint)r2 <= 0xff) return (KKey)r2;
#else //20-50 times faster and less garbage. Good JIT speed.
			return _FindKeyInEnums(s1);
#endif
		}
		return 0;

		bool _U(char cc1, char cc2) { return u == ((uint)cc1 << 16 | cc2); }
	}

	static IEnumerable<RXGroup> _SplitKeysString(string keys_) =>
		(s_rxKeys ??= new regexp(@"(?s)[A-Z][[:alnum:]]*|#\S|\*\s*(?:\d+|down|up)\b|\+\s*\(|_.|\^.+|\S"))
		.FindAllG(keys_ ?? "", 0);
	//KeyName | #n | *r | *down | *up | +( | _char | ^chars | nonspace char
	static regexp s_rxKeys;

	static System.Collections.Hashtable s_htEnum; //with Dictionary much slower JIT
	[MethodImpl(MethodImplOptions.NoInlining)]
	static KKey _FindKeyInEnums(string key) {
		if (s_htEnum == null) {
			var t = new System.Collections.Hashtable(160 /*220*/, StringComparer.OrdinalIgnoreCase);
			var a1 = typeof(KKey).GetFields();
			for (int j = 1; j < a1.Length; j++) { //note: start from 1 to skip the default value__, it gives exception
				t.Add(a1[j].Name, a1[j].GetRawConstantValue());
			}

			//rejected. Better avoid loading Forms dll. All useful keys are in KKey. For others can use virtual-key codes.
			//var a2 = typeof(System.Windows.Forms.Keys).GetFields();
			//for(int j = 4; j < a2.Length; j++) { //skip value__, KeyCode, Modifiers, None
			//	var v = a2[j];
			//	//print.it(v.Name);
			//	if(t.ContainsKey(v.Name)) continue;
			//	var k = v.GetRawConstantValue();
			//	if((uint)(int)k >= 0xff) continue;
			//	print.it(v.Name, j);
			//	t.Add(v.Name, k);
			//}

			//print.it(a1.Length, /*a2.Length,*/ t.Count); //216 with Keys enum, 156 without
			s_htEnum = t;
		}
		var r = s_htEnum[key];
		if (r == null) return 0;
		if (r is byte) return (KKey)(byte)r;
		return (KKey)(int)r;
		//note: GetRawConstantValue gets byte for KKey, int for Keys. GetValue(null) gets of enum type.
	}

	static ArgumentException _ArgumentException_ErrorInKeysString(string keys_, int i, int len) {
		int end = i + len;
		return new ArgumentException($"Error in keys string: {keys_[..i]}■{keys_[i..end]}■{keys_[end..]}");
		//tested: all fonts on Win7 have ■.
	}

	/// <summary>
	/// Internal static functions.
	/// </summary>
	internal static class Internal_ {
		/// <summary>
		/// Calls <see cref="wait.doEvents(int)"/>.
		/// </summary>
		internal static void Sleep(int ms) {
			if (ms > 0) wait.doEventsPrecise_(ms);

			//see comments in mouse._Sleep.
		}

		/// <summary>
		/// If <c>t &gt; 10</c>, returns <c>(t / 4 + 8)</c>.
		/// </summary>
		internal static int LimitSleepTime(int t) => t <= 10 ? t : (t / 4 + 8);

		/// <summary>
		/// If <i>k</i> is <c>Shift</c>, <c>Ctrl</c>, <c>Alt</c> or <c>Win</c> or their left/right versions, returns it as modifier flag, eg <b>KMod.Shift</b>.
		/// Else returns 0.
		/// </summary>
		internal static KMod KeyToMod(KKey k) {
			return k switch {
				KKey.Shift or KKey.LShift or KKey.RShift => KMod.Shift,
				KKey.Ctrl or KKey.LCtrl or KKey.RCtrl => KMod.Ctrl,
				KKey.Alt or KKey.LAlt or KKey.RAlt => KMod.Alt,
				KKey.Win or KKey.RWin => KMod.Win,
				_ => 0,
			};
		}

		/// <summary>
		/// Gets scan code from virtual-key code.
		/// </summary>
		/// <param name="vk"></param>
		/// <param name="hkl">Keyboard layout. If 0, uses of current thread. If -1, uses current focus/active thread.</param>
		internal static ushort VkToSc(KKey vk, nint hkl = 0) {
			if (hkl == 0) hkl = Api.GetKeyboardLayout(0);
			else if (hkl == -1) {
				var w = wnd.focused; if (w.Is0) w = wnd.active;
				hkl = Api.GetKeyboardLayout(w.ThreadId);
			}

			uint sc = Api.MapVirtualKeyEx((uint)vk, 0, hkl); //MAPVK_VK_TO_VSC

			//fix Windows bugs
			if (vk == KKey.PrintScreen && sc == 0x54) sc = 0x37;
			if (vk == KKey.Pause && sc == 0) sc = 0x45;

			return (ushort)sc;

			//tested: LCtrl, RCtrl etc are correctly sent, although MSDN does not mention that SendInput supports it.
		}

		/// <summary>
		/// Sends one key event.
		/// Just calls API <b>SendInput</b> with raw parameters.
		/// </summary>
		internal static unsafe void SendKeyEventRaw(KKey vk, ushort scan, uint flags, int? extra = null) {
			var ki = new Api.INPUTK(vk, scan, flags);
			if (extra != null) ki.dwExtraInfo = extra.Value;
			Api.SendInput(&ki);
		}

		/// <inheritdoc cref="more.sendKey(KKey, bool?, nint, int?)"/>
		internal static void SendKey(KKey k, bool? down = null, nint hkl = 0, int? extra = null) {
			uint f = 0;
			if (KeyTypes_.IsExtended(k)) f |= Api.KEYEVENTF_EXTENDEDKEY;
			ushort scan = VkToSc(k, hkl);

			if (down != false) SendKeyEventRaw(k, scan, f, extra);
			if (down != true) SendKeyEventRaw(k, scan, f | Api.KEYEVENTF_KEYUP, extra);
		}

		internal static void SendCtrl(bool down) => SendKeyEventRaw(KKey.Ctrl, 0x1D, down ? 0 : Api.KEYEVENTF_KEYUP);
		internal static void SendAlt(bool down) => SendKeyEventRaw(KKey.Alt, 0x38, down ? 0 : Api.KEYEVENTF_KEYUP);
		internal static void SendShift(bool down) => SendKeyEventRaw(KKey.Shift, 0x2A, down ? 0 : Api.KEYEVENTF_KEYUP);
		internal static void SendRCtrlUp() => SendKeyEventRaw(KKey.Ctrl, 0x1D, Api.KEYEVENTF_KEYUP | Api.KEYEVENTF_EXTENDEDKEY);
		internal static void SendRAltUp() => SendKeyEventRaw(KKey.Alt, 0x38, Api.KEYEVENTF_KEYUP | Api.KEYEVENTF_EXTENDEDKEY);
		internal static void SendRShiftUp() => SendKeyEventRaw(KKey.Shift, 0x36, Api.KEYEVENTF_KEYUP);

		/// <summary>
		/// Presses or releases one or more modifier keys.
		/// Sends in this order: <c>Ctrl</c>, <c>Alt</c>, <c>Shift</c>, <c>Win</c>.
		/// </summary>
		/// <param name="press"></param>
		/// <param name="mod">Modifier keys. Does nothing if 0.</param>
		internal static unsafe void ModPressRelease(bool press, KMod mod) {
			if (mod == 0) return;
			var a = stackalloc Api.INPUTK[4];
			int n = 0; uint f = press ? 0 : Api.KEYEVENTF_KEYUP;
			if (0 != (mod & KMod.Ctrl)) a[n++].Set(KKey.Ctrl, 0x1D, f);
			if (0 != (mod & KMod.Alt)) a[n++].Set(KKey.Alt, 0x38, f);
			if (0 != (mod & KMod.Shift)) a[n++].Set(KKey.Shift, 0x2A, f);
			if (0 != (mod & KMod.Win)) a[n++].Set(KKey.Win, 0x5B, f);
			Api.SendInput(a, n);
		}

		/// <summary>
		/// Releases modifier keys if pressed.
		/// May also press-release some other keys to avoid menu mode etc.
		/// Does not use options, sleep, blockinput, etc.
		/// </summary>
		internal static void ReleaseModAndDisableModMenu() {
			int m = 0;
			if (isPressed(KKey.LShift)) m |= 1;
			if (isPressed(KKey.RShift)) m |= 0x10;
			if (isPressed(KKey.LCtrl)) m |= 2;
			if (isPressed(KKey.RCtrl)) m |= 0x20;
			if (isPressed(KKey.LAlt)) m |= 4;
			if (isPressed(KKey.RAlt)) m |= 0x40;
			if (isPressed(KKey.Win)) m |= 8;
			if (isPressed(KKey.RWin)) m |= 0x80;
			if (m == 0) return;

			//if Alt or Win pressed without Ctrl, send Ctrl to avoid menu mode or Start menu.
			//	For Alt works Ctrl-up, but maybe not everywhere. For Win need Ctrl-down-up.
			if (0 != (m & 0xCC) && 0 == (m & 0x22)) {
				SendCtrl(true);
				m |= 2;
			}

			//prevent invoking something when pressed-released only modifier keys.
			//	Examples: switch keyboard layout on Ctrl+Alt or Ctrl+Shift; invoke QTranslate on two Ctrl; MS Office ad on Ctrl+Alt+Shift+Win.
			//	The vk is unassigned. Tested: vk 0 does not work.
			SendKeyEventRaw((KKey)0xD8, 0, Api.KEYEVENTF_KEYUP);

			if (0 != (m & 2)) SendCtrl(false);
			if (0 != (m & 0x20)) SendRCtrlUp();
			if (0 != (m & 1)) SendShift(false);
			if (0 != (m & 0x10)) SendRShiftUp();
			if (0 != (m & 4)) SendAlt(false);
			if (0 != (m & 0x40)) SendRAltUp();
			if (0 != (m & 8)) SendKey(KKey.Win, false);
			if (0 != (m & 0x80)) SendKey(KKey.RWin, false);
		}

		/// <summary>
		/// Sends <c>Ctrl+V</c> or <c>Ctrl+C</c> or <c>Ctrl+X</c>, and/or optionally one or more keys.
		/// Caller gets <i>optk</i> and <i>wFocus</i> with <b>GetOptionsAndWndFocused_</b> (it may want to know some options too).
		/// Caller calls <b>Press</b>, waits until the target app gets clipboard data, then calls <b>Release</b>.
		/// </summary>
		internal unsafe struct SendCopyPaste {
			KHotkey _hk;
			ushort _scan;
			OKey _opt;
			List<KKey> _andKeys;

			/// <summary>
			/// Presses <c>Ctrl+key</c>. Does not release.
			/// If <i>andKeys</i> used, <b>Release</b> will press/release them.
			/// </summary>
			public void Press(KHotkey hk, OKey optk, wnd wFocus, List<KKey> andKeys = null) {
				_hk = hk;
				_scan = VkToSc(_hk.Key, Api.GetKeyboardLayout(wFocus.ThreadId));
				_opt = optk;
				_andKeys = andKeys;

				if (_hk.Mod.Has(KMod.Ctrl)) SendCtrl(true);
				if (_hk.Mod.Has(KMod.Alt)) SendAlt(true);
				if (_hk.Mod.Has(KMod.Shift)) SendShift(true);
				Internal_.Sleep(optk.KeySpeedClipboard); //need 1 ms for IE address bar, 100 ms for BlueStacks
				SendKeyEventRaw(_hk.Key, _scan, 0);
			}

			/// <summary>
			/// Releases keys.
			/// Does nothing if already released.
			/// </summary>
			public void Release() {
				if (_hk.Key == 0) return;
				var vk = _hk.Key; _hk.Key = 0;

				SendKeyEventRaw(vk, _scan, Api.KEYEVENTF_KEYUP);
				if (_hk.Mod.Has(KMod.Shift)) SendShift(false);
				if (_hk.Mod.Has(KMod.Alt)) SendAlt(false);
				if (_hk.Mod.Has(KMod.Ctrl)) SendCtrl(false);
				if (_andKeys != null) AndSendKeys(_andKeys, _opt);
			}

			/// <summary>
			/// Sends one or more keys.
			/// Not used for keys whose scancode can depend on keyboard layout. To get scancode, uses keyboard layout of current thread.
			/// </summary>
			public static void AndSendKeys(List<KKey> keys_, OKey optk) {
				foreach (var k in keys_) {
					var f = KeyTypes_.IsExtended(k) ? _KFlags.Extended : default;
					var e = new _KEvent(true, k, f, VkToSc(k));
					_SendKey2(e, default, true, optk);
				}
			}
		}

		/// <summary>
		/// Gets focused or active window. Waits for it max 20-40 ms (820 ms if <i>requireFocus</i>). On timeout returns default (throws exception if <i>requireFocus</i>).
		/// </summary>
		/// <param name="requireFocus">Wait for focused (and not just active) window longer, and throw exception on timeout. Used for clipboard copy/paste and send text.</param>
		/// <exception cref="AuException">No focused window when <i>requireFocus</i>.</exception>
		/// <exception cref="InputDesktopException"></exception>
		internal static wnd GetWndFocusedOrActive(bool requireFocus) {
			for (int i = 0; i < (requireFocus ? 100 : 20); i++) {
				miscInfo.getGUIThreadInfo(out var g);
				//print.it(i, g.hwndFocus, g.hwndActive);
				if (!g.hwndFocus.Is0) return g.hwndFocus;
				if (!requireFocus && !g.hwndActive.Is0) return g.hwndActive;
				wait.ms(i < 20 ? 1 : 10);
			}
			InputDesktopException.ThrowIfBadDesktop();
			if (requireFocus) throw new AuException("There is no focused window"); //TODO3: test various windows and data types, maybe somewhere could work without focus
			return default;

			//note: the purpose of this wait is not synchronization. It just makes getting the focused/active window more reliable.
			//	Cannot wait for a focused window. Users must program it explicitly.
			//	When creating or activating a window, often there is no focus for 200 ms or more. Eg when opening the Save As dialog.
			//	Also, focus is optional.
			//	Anyway, waiting for focus would not make more reliable, because keys are processed asynchronously.
			//	Usually the active window is OK. We use it to get keyboard layout and/or to avoid calling Hook too frequently.
			//	This func waits for active window max 20-40 ms. When switching apps, usually there is no active window for 1-5 ms.
		}
	}

	/// <summary>
	/// Returns:
	/// - <i>optk</i> - <b>OKey</b> of this variable or <b>OKey</b> cloned from this variable and possibly modified by <b>Hook</b>.
	/// - <i>wFocus</i> - the focused or active window.
	/// </summary>
	/// <param name="getWndAlways">if <c>false</c>, the caller does not need <i>wFocus</i>. Then <i>wFocus</i> will be <c>default(wnd)</c> if <b>Hook</b> is <c>null</c>.</param>
	/// <param name="requireFocus">Wait for focused (and not just active) window longer, and throw exception on timeout. Used for clipboard copy/paste and send text.</param>
	/// <exception cref="AuException">No focused window when <i>requireFocus</i>.</exception>
	/// <exception cref="InputDesktopException"></exception>
	internal (OKey optk, wnd wFocus) GetOptionsAndWndFocused_(bool getWndAlways, bool requireFocus = false) {
		if (Options.Hook == null && !getWndAlways) return (Options, default);
		var w = Internal_.GetWndFocusedOrActive(requireFocus);
		return (GetOptions_(w), w);
	}

	/// <summary>
	/// Returns <b>OKey</b> of this variable or <b>OptKey</b> cloned from this variable and possibly modified by <b>Hook</b>.
	/// </summary>
	/// <param name="wFocus">the focused or active window. The function uses it to avoid frequent calling of <b>Hook</b>. If you don't have it, use <b>GetOptionsAndWndFocused_</b> instead.</param>
	internal OKey GetOptions_(wnd wFocus) {
		var call = Options.Hook;
		if (call == null || wFocus.Is0) return Options;
		if (wFocus != _sstate.wFocus) {
			_sstate.wFocus = wFocus;
			if (_sstate.options == null) _sstate.options = new OKey(Options); else _sstate.options.CopyOrDefault_(Options);
			call(new OKeyHookData(_sstate.options, wFocus));
		}
		return _sstate.options;
	}

	void _ThrowIfSending() {
		if (_sending) throw new InvalidOperationException();
	}

	internal static class KeyTypes_ {
		[Flags]
		enum _KT : byte {
			Mod = 1,
			Extended = 2,
			Mouse = 4,
			GksReliable = 8,
		}

		/// <summary>
		/// <c>Ctrl</c>, <c>LCtrl</c>, etc.
		/// </summary>
		public static bool IsMod(KKey vk) => 0 != (_b[(byte)vk] & _KT.Mod);

		public static bool IsExtended(KKey vk) => 0 != (_b[(byte)vk] & _KT.Extended);

		public static bool IsMouse(KKey vk) => 0 != (_b[(byte)vk] & _KT.Mouse);

		/// <summary>
		/// API <b>GetKeyState</b> always works.
		/// For other keys returns 0 if pressed or toggled before starting current thread.
		/// Modifiers (left/right too), lock keys, mouse, some other.
		/// </summary>
		public static bool IsGetKeyStateReliable(KKey vk) => 0 != (_b[(byte)vk] & _KT.GksReliable);

		/// <summary>
		/// The same as <see cref="IsGetKeyStateReliable"/>.
		/// </summary>
		public static bool IsToggleable(KKey vk) => 0 != (_b[(byte)vk] & _KT.GksReliable);

		static _KT[] _b;

		static KeyTypes_() {
			_b = new _KT[256];

			_b[1] = _b[2] = _b[4] = _b[5] = _b[6]
				= _KT.Mouse | _KT.GksReliable;

			_b[16] = _b[17] = _b[18] = _b[(int)KKey.LShift] = _b[(int)KKey.RShift] = _b[(int)KKey.LCtrl] = _b[(int)KKey.LAlt]
				= _KT.Mod | _KT.GksReliable;

			_b[(int)KKey.PageUp] = _b[(int)KKey.PageDown] = _b[(int)KKey.End] = _b[(int)KKey.Home]
				= _b[(int)KKey.Left] = _b[(int)KKey.Up] = _b[(int)KKey.Right] = _b[(int)KKey.Down]
				= _b[(int)KKey.PrintScreen] = _b[(int)KKey.Insert] = _b[(int)KKey.Delete]
				= _b[(int)KKey.Sleep] = _b[(int)KKey.Apps] = _b[(int)KKey.Divide] = _b[(int)KKey.Break]
				= _KT.Extended;
			//and more, but undocumented, and cannot test. There is no API to get extended keys. MapVirtualKeyEx can get only of 50% keys.

			_b[(int)KKey.CapsLock] = _b[(int)KKey.ScrollLock]
				= _b[(int)KKey.Back] = _b[(int)KKey.Tab] = _b[(int)KKey.Enter] = _b[(int)KKey.Escape]
				= _KT.GksReliable; //also Home and maybe more

			_b[(int)KKey.NumLock]
				= _KT.Extended | _KT.GksReliable;

			_b[(int)KKey.Win] = _b[(int)KKey.RWin] = _b[(int)KKey.RCtrl] = _b[(int)KKey.RAlt]
				= _KT.Mod | _KT.Extended | _KT.GksReliable;

			for (int i = (int)KKey.BrowserBack; i <= (int)KKey.LaunchApp2; i++) _b[i] = _KT.Extended; //media/browser/launchapp keys

			//for(int i = 1; i < 256; i++) print.it((KKey)i, _b[i]);
		}
	}

	struct _INPUTKEY2 {
		public Api.INPUTK k0, k1;

		public _INPUTKEY2(KKey vk, ushort sc, uint flags = 0) {
			k0 = new Api.INPUTK(vk, sc, flags);
			k1 = new Api.INPUTK(vk, sc, flags | Api.KEYEVENTF_KEYUP);
		}
	}
}
