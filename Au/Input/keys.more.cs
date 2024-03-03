namespace Au;

partial class keys
{
	/// <summary>
	/// Miscellaneous rarely used keyboard-related functions.
	/// </summary>
	public static partial class more
	{
		/// <summary>
		/// Converts key name to <see cref="KKey"/>.
		/// </summary>
		/// <returns>0 if unknown key name.</returns>
		/// <param name="keyName">Key name, like with <see cref="keys.send"/>.</param>
		public static KKey parseKeyName(string keyName) {
			keyName ??= "";
			return _KeynameToKey(keyName, 0, keyName.Length);
		}

		/// <summary>
		/// Calls <see cref="parseKeyName"/> and throws <b>ArgumentException</b> if invalid key string.
		/// </summary>
		/// <param name="keyName"></param>
		internal static KKey ParseKeyNameThrow_(string keyName) {
			var k = parseKeyName(keyName);
			if (k == 0) throw new ArgumentException("Unknown key name or error in key string.");
			return k;
		}

		/// <summary>
		/// Converts key name to <see cref="KKey"/>.
		/// </summary>
		/// <returns>0 if unknown key name.</returns>
		/// <param name="s">String containing key name, like with <see cref="keys.send"/>.</param>
		/// <param name="startIndex">Key name start index in <i>s</i>.</param>
		/// <param name="length">Key name length.</param>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <i>startIndex</i> or <i>length</i>.</exception>
		public static KKey parseKeyName(string s, int startIndex, int length) {
			s ??= "";
			if ((uint)startIndex > s.Length || (uint)length > s.Length - startIndex) throw new ArgumentOutOfRangeException();
			return _KeynameToKey(s, startIndex, length);
		}

		/// <summary>
		/// Converts keys string to <see cref="KKey"/> array.
		/// </summary>
		/// <param name="keys_">String containing one or more key names, like with <see cref="keys.send"/>. Operators are not supported.</param>
		/// <exception cref="ArgumentException">Error in <i>keys_</i> string.</exception>
		public static KKey[] parseKeysString(string keys_) {
			var a = new List<KKey>();
			foreach (var g in _SplitKeysString(keys_ ?? "")) {
				KKey k = _KeynameToKey(keys_, g.Start, g.Length);
				if (k == 0) throw _ArgumentException_ErrorInKeysString(keys_, g.Start, g.Length);
				a.Add(k);
			}
			return a.ToArray();
		}

		/// <summary>
		/// Converts string to <see cref="KKey"/> and <see cref="KMod"/>.
		/// For example, if <i>s</i> is <c>"Ctrl+Left"</c>, sets <c>mod = KMod.Ctrl</c>, <c>key = KKey.Left</c>.
		/// </summary>
		/// <returns><c>false</c> if the string is invalid.</returns>
		/// <remarks>
		/// Key names are like with <see cref="keys.send"/>.
		/// Must be single non-modifier key, preceded by zero or more of modifier keys Ctrl, Shift, Alt, Win, all joined with +.
		/// Valid hotkey examples: <c>"A"</c>, <c>"a"</c>, <c>"7"</c>, <c>"F12"</c>, <c>"."</c>, <c>"End"</c>, <c>"Ctrl+D"</c>, <c>"Ctrl+Alt+Shift+Win+Left"</c>, <c>" Ctrl + U "</c>.
		/// Invalid hotkey examples: <c>null</c>, <c>""</c>, <c>"A+B"</c>, <c>"Ctrl+A+K"</c>, <c>"A+Ctrl"</c>, <c>"Ctrl+Shift"</c>, <c>"Ctrl+"</c>, <c>"NoSuchKey"</c>, <c>"tab"</c>.
		/// </remarks>
		public static bool parseHotkeyString(string s, out KMod mod, out KKey key) {
			key = 0; mod = 0;
			if (s == null) return false;
			int i = 0;
			foreach (var g in _SplitKeysString(s)) {
				if (key != 0) return false;
				if ((i++ & 1) == 0) {
					KKey k = _KeynameToKey(s, g.Start, g.Length);
					if (k == 0) return false;
					var m = Internal_.KeyToMod(k);
					if (m != 0) {
						if ((m & mod) != 0) return false;
						mod |= m;
					} else key = k;
				} else if (g.Length != 1 || s[g.Start] != '+') return false;
			}
			return key != 0 && key != KKey.Packet;
		}

		/// <summary>
		/// Converts string to winforms <see cref="System.Windows.Forms.Keys"/>.
		/// For example, if <i>s</i> is <c>"Ctrl+Left"</c>, sets <c>hotkey = Keys.Control | Keys.Left</c>.
		/// </summary>
		/// <returns><c>false</c> if the string is invalid or contains <c>"Win"</c>.</returns>
		public static bool parseHotkeyString(string s, out System.Windows.Forms.Keys hotkey) {
			if (!parseHotkeyString(s, out var m, out var k)) { hotkey = 0; return false; }
			hotkey = KModToWinforms(m) | (System.Windows.Forms.Keys)k;
			if (m.Has(KMod.Win)) return false;
			return true;
			//return Enum.IsDefined(typeof(System.Windows.Forms.Keys), (System.Windows.Forms.Keys)k); //not too slow
			//tested: enum Keys has all KKey values + some extinct.
		}

		/// <summary>
		/// Converts string to WPF <see cref="System.Windows.Input.ModifierKeys"/> and <see cref="System.Windows.Input.Key"/> or <see cref="System.Windows.Input.MouseAction"/>.
		/// For example, if <i>s</i> is <c>"Ctrl+Left"</c>, sets <c>mod = ModifierKeys.Control</c> and <c>key = Key.Left</c>.
		/// Supported mouse button strings: <c>"Click"</c>, <c>"D-click"</c>, <c>"R-click"</c>, <c>"M-click"</c>, <c>"Wheel"</c>. Example: <c>"Ctrl+R-click"</c>. The first character of a mouse word is case-insensitive.
		/// </summary>
		/// <returns><c>false</c> if the string is invalid or contains incorrectly specified mouse buttons.</returns>
		public static bool parseHotkeyString(string s, out System.Windows.Input.ModifierKeys mod, out System.Windows.Input.Key key, out System.Windows.Input.MouseAction mouse) {
			mod = 0; key = 0; mouse = 0;
			if (s.Ends("lick") || s.Ends("heel")) {
				int i = s.LastIndexOf('+') + 1;
				var v = s.AsSpan(i); var co = StringComparison.OrdinalIgnoreCase;
				if (v.Equals("Click", co)) mouse = System.Windows.Input.MouseAction.LeftClick;
				else if (v.Equals("D-click", co)) mouse = System.Windows.Input.MouseAction.LeftDoubleClick;
				else if (v.Equals("R-click", co)) mouse = System.Windows.Input.MouseAction.RightClick;
				else if (v.Equals("M-click", co)) mouse = System.Windows.Input.MouseAction.MiddleClick;
				else if (v.Equals("Wheel", co)) mouse = System.Windows.Input.MouseAction.WheelClick;
				if (mouse != default) {
					if (i == 0) return true;
					s = s.ReplaceAt(i.., "A"); //replace the mouse word with a key name, else can't parse
				}
			}
			if (!parseHotkeyString(s, out var m, out var k)) return false;
			mod = KModToWpf(m);
			return mouse != default || (key = KKeyToWpf(k)) != default;
			//tested: enum Key has all KKey values except mouse buttons and packet.
		}

		/// <summary>
		/// Parses hotkey trigger string or mouse trigger modifiers string.
		/// Like <see cref="parseHotkeyString"/>, but supports "any mod" (like <c>"Shift?+K"</c> or <c>"?+K"</c>) and <i>noKey</i>.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="mod"></param>
		/// <param name="modAny"></param>
		/// <param name="key"></param>
		/// <param name="noKey">Modifiers only. If <c>true</c>, s must be "modifiers" or <c>null</c>/<c>""</c>. If <c>false</c>, <i>s</i> must be "key" or "modifiers+key".</param>
		public static bool parseTriggerString(string s, out KMod mod, out KMod modAny, out KKey key, bool noKey) {
			key = 0; mod = 0; modAny = 0;
			if (s.NE()) return noKey;
			int i = 0; bool ignore = false;
			foreach (var g in _SplitKeysString(s)) {
				if (ignore) { ignore = false; continue; }
				if (key != 0) return false;
				if ((i++ & 1) == 0) {
					KKey k = _KeynameToKey(s, g.Start, g.Length);
					if (k == 0) return false;
					var m = Internal_.KeyToMod(k);
					if (m != 0) {
						if ((m & (mod | modAny)) != 0) return false;
						if (ignore = g.End < s.Length && s[g.End] == '?') modAny |= m; //eg "Shift?+K"
						else mod |= m;
					} else {
						if (i == 1 && g.Length == 1 && s[g.Start] == '?') modAny = (KMod)15; //eg "?+K"
						else key = k;
					}
				} else if (g.Length != 1 || s[g.Start] != '+') return false;
			}
			if (noKey) return (mod | modAny) != 0 && key == 0;
			return key != 0;
		}

		/// <summary>
		/// Formats hotkey string like <c>"Ctrl+Shift+K"</c>.
		/// </summary>
		/// <param name="b">Append to this <b>StringBuilder</b>.</param>
		/// <param name="mod"></param>
		/// <param name="key"></param>
		public static void hotkeyToString(StringBuilder b, KMod mod, KKey key) {
			if (mod.Has(KMod.Ctrl)) b.Append("Ctrl+");
			if (mod.Has(KMod.Alt)) b.Append("Alt+");
			if (mod.Has(KMod.Shift)) b.Append("Shift+");
			if (mod.Has(KMod.Win)) b.Append("Win+");
			b.Append(keyToString(key)).ToString();
		}

		/// <summary>
		/// Formats hotkey string like <c>"Ctrl+Shift+K"</c>.
		/// </summary>
		public static string hotkeyToString(KMod mod, KKey key) {
			if (mod == 0) return keyToString(key);
			using (new StringBuilder_(out var b)) {
				hotkeyToString(b, mod, key);
				return b.ToString();
			}
		}

		/// <summary>
		/// Gets key name that can be used in keys strings with <see cref="keys.send"/> etc.
		/// </summary>
		public static string keyToString(KKey key) => c_keyNames[(int)key] ?? ("VK" + (int)key);

		static readonly string[] c_keyNames = {
	"",
	"MouseLeft",
	"MouseRight",
	"Break",
	"MouseMiddle",
	"MouseX1",
	"MouseX2",
	null,
	"Back",
	"Tab",
	null,
	null,
	"Clear",
	"Enter",
	null,
	null,
	"Shift",
	"Ctrl",
	"Alt",
	"Pause",
	"CapsLock",
	"IMEKanaMode",
	null,
	"IMEJunjaMode",
	"IMEFinalMode",
	"IMEKanjiMode",
	null,
	"Esc",
	"IMEConvert",
	"IMENonconvert",
	"IMEAccept",
	"IMEModeChange",
	"Space",
	"PgUp",
	"PgDn",
	"End",
	"Home",
	"Left",
	"Up",
	"Right",
	"Down",
	null,
	null,
	null,
	"PrtSc",
	"Ins",
	"Del",
	null,
	"0",
	"1",
	"2",
	"3",
	"4",
	"5",
	"6",
	"7",
	"8",
	"9",
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	"A",
	"B",
	"C",
	"D",
	"E",
	"F",
	"G",
	"H",
	"I",
	"J",
	"K",
	"L",
	"M",
	"N",
	"O",
	"P",
	"Q",
	"R",
	"S",
	"T",
	"U",
	"V",
	"W",
	"X",
	"Y",
	"Z",
	"Win",
	"RWin",
	"Apps",
	null,
	"Sleep",
	"#0",
	"#1",
	"#2",
	"#3",
	"#4",
	"#5",
	"#6",
	"#7",
	"#8",
	"#9",
	"#*",
	"#+",
	null,
	"#-",
	"#.",
	"#/",
	"F1",
	"F2",
	"F3",
	"F4",
	"F5",
	"F6",
	"F7",
	"F8",
	"F9",
	"F10",
	"F11",
	"F12",
	"F13",
	"F14",
	"F15",
	"F16",
	"F17",
	"F18",
	"F19",
	"F20",
	"F21",
	"F22",
	"F23",
	"F24",
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	"NumLock",
	"ScrollLock",
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	"LShift",
	"RShift",
	"LCtrl",
	"RCtrl",
	"LAlt",
	"RAlt",
	"BrowserBack",
	"BrowserForward",
	"BrowserRefresh",
	"BrowserStop",
	"BrowserSearch",
	"BrowserFavorites",
	"BrowserHome",
	"VolumeMute",
	"VolumeDown",
	"VolumeUp",
	"MediaNextTrack",
	"MediaPrevTrack",
	"MediaStop",
	"MediaPlayPause",
	"LaunchMail",
	"LaunchMediaSelect",
	"LaunchApp1",
	"LaunchApp2",
	null,
	null,
	";",
	"=",
	",",
	"-",
	".",
	"/",
	"`",
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	"[",
	"|",
	"]",
	"'",
	null,
	null,
	null,
	null,
	null,
	null,
	"IMEProcessKey",
	null,
	"Packet",
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
	null,
};
		//this script creates the array:
		/*
print.clear();
var b=new StringBuilder("static readonly string[] c_keyNames = {");
for (int i = 0; i < 256; i++) {
var k=(KKey)i;
var s=k switch {
	0 => "",
	>= KKey.D0 and <= KKey.D9 => ((char)k).ToString(),
	>= KKey.NumPad0 and <= KKey.NumPad9 => "#" + ((char)(k-KKey.NumPad0+'0')).ToString(),
	KKey.OemMinus => "-",
	KKey.OemPlus => "=",
	KKey.OemTilde => "`",
	KKey.OemOpenBrackets => "[",
	KKey.OemCloseBrackets => "]",
	KKey.OemPipe => "|",
	KKey.OemSemicolon => ";",
	KKey.OemQuotes => "'",
	KKey.OemComma => ",",
	KKey.OemPeriod => ".",
	KKey.OemQuestion => "/",
	KKey.Decimal => "#.",
	KKey.Add => "#+",
	KKey.Divide => "#/",
	KKey.Multiply => "#*",
	KKey.Subtract => "#-",
	KKey.Escape => "Esc",
	KKey.PageUp => "PgUp",
	KKey.PageDown => "PgDn",
	KKey.PrintScreen => "PrtSc",
	KKey.Insert => "Ins",
	KKey.Delete => "Del",
	_ => Enum.IsDefined<KKey>(k) ? k.ToString() : null
};
//	print.it(k, s);
b.Append("\r\n\t");
if(s==null) b.Append("null"); else b.Append('"').Append(s).Append('"');
b.Append(',');
}
b.Append("\r\n};");
print.it(b.ToString());
		*/

		/// <summary>
		/// Converts modifier key flags from <b>KMod</b> to winforms <b>Keys</b>.
		/// </summary>
		/// <remarks>
		/// For Win returns flag (Keys)0x80000.
		/// </remarks>
		public static System.Windows.Forms.Keys KModToWinforms(KMod mod) => (System.Windows.Forms.Keys)((int)mod << 16);

		/// <summary>
		/// Converts modifier key flags from winforms <b>Keys</b> to <b>KMod</b>.
		/// </summary>
		/// <remarks>
		/// For Win can be used flag (Keys)0x80000.
		/// </remarks>
		public static KMod KModFromWinforms(System.Windows.Forms.Keys mod) => (KMod)((int)mod >> 16);

		/// <summary>
		/// Converts modifier key flags from <b>KMod</b> to WPF <b>ModifierKeys</b>.
		/// </summary>
		public static System.Windows.Input.ModifierKeys KModToWpf(KMod mod) => (System.Windows.Input.ModifierKeys)_SwapMod((int)mod);

		/// <summary>
		/// Converts modifier key flags from WPF <b>ModifierKeys</b> to <b>KMod</b>.
		/// </summary>
		public static KMod KModFromWpf(System.Windows.Input.ModifierKeys mod) => (KMod)_SwapMod((int)mod);

		static int _SwapMod(int m) => (m & 0b1010) | (m << 2 & 4) | (m >> 2 & 1);

		/// <summary>
		/// Converts key from <b>KKey</b> to WPF <b>Key</b>.
		/// </summary>
		public static System.Windows.Input.Key KKeyToWpf(KKey k) => System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)k);

		/// <summary>
		/// Converts key from WPF <b>Key</b> to <b>KKey</b>.
		/// </summary>
		public static KKey KKeyFromWpf(System.Windows.Input.Key k) => (KKey)System.Windows.Input.KeyInterop.VirtualKeyFromKey(k);

		/// <summary>
		/// Sends single key.
		/// </summary>
		/// <param name="k">Virtual-key code.</param>
		/// <param name="down"><c>true</c> down, <c>false</c> up, <c>null</c> down and up.</param>
		/// <param name="hkl">
		/// Keyboard layout handle for scan code. See API <msdn>GetKeyboardLayout</msdn>.
		/// If 0 (default), uses keyboard layout of this thread; don't use 0 for keys whose scancode depends on keyboard layout.
		/// If -1, uses keyboard layout of the focused or active window.
		/// </param>
		/// <param name="extra">An "extra info" value that can be used for example by keyboard hooks to recognize the key sender. If <c>null</c> (default), uses the same value as other functions of this library.</param>
		/// <remarks>
		/// This is a low-level function. Does nothing more (sleep, block input, etc). Does not use <b>opt</b> options. Just gets missing info (scan code etc) and calls API <msdn>SendInput</msdn>.
		/// </remarks>
		public static void sendKey(KKey k, bool? down = null, nint hkl = 0, int? extra = null)
			=> Internal_.SendKey(k, down, hkl, extra);

		//FUTURE: RemapKeyboardKeys. See QM2.

	}
}
