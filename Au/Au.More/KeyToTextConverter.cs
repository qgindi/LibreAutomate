namespace Au.More;

/// <summary>
/// Converts virtual-key codes to text characters.
/// </summary>
/// <remarks>
/// To record user input, can be used <see cref="WindowsHook.Keyboard(Action{HookData.Keyboard}, bool, bool)"/>.
/// When recording user input, use same <b>KeyToTextConverter</b> variable for all keys.
/// </remarks>
public class KeyToTextConverter
{
	(KKey vk, KMod mod, uint sc, nint hkl) _deadKey;

	/// <summary>
	/// Converts a virtual-key code to text.
	/// </summary>
	/// <param name="text">Receives text. Can be 1 character <i>c</i>, or string <i>s</i> with 2 or more characters. Receives default if this function returns <c>false</c> or if the key is a dead key.</param>
	/// <param name="vk">Virtual-key code.</param>
	/// <param name="sc">Scan code.</param>
	/// <param name="mod">Modifier keys (Shift etc). See <see cref="keys.getMod(KMod)"/>.</param>
	/// <param name="threadId">Thread id of the focused or active window. Need for keyboard layout. If 0, uses this thread.</param>
	/// <returns><c>true</c> if it's a text key or dead key.</returns>
	public unsafe bool Convert(out (char c, string s) text, KKey vk, uint sc, KMod mod, int threadId) {
		text = default;
		if (vk == KKey.Packet) {
			text.c = (char)sc;
		} else {
			if (!IsPossiblyChar_(mod, vk)) return false;

			var hkl = Api.GetKeyboardLayout(threadId);
			var ks = stackalloc byte[256];
			_SetKS(mod);
			var c = stackalloc char[8];
			bool win10 = osVersion.minWin10_1607; //the API resets dead key etc, but on new OS flag 4 prevents it
			int n = Api.ToUnicodeEx((uint)vk, sc, ks, c, 8, win10 ? 4u : 0u, hkl);
			if (n == 1 && c[0] < ' ') {
				Debug_.Print($"{(int)c[0]}, {c[0]}");
				if (c[0] == '\r') c[n++] = '\n'; else if (c[0] is not ('\t' or '\n')) n = 0;
			}
			if (n == 1) text.c = c[0]; else if (n > 0) text.s = new(c, 0, n);
			if (!win10) { //if need, set dead key again
				if (_deadKey.vk != 0 && _deadKey.hkl == hkl) {
					_SetKS(_deadKey.mod);
					Api.ToUnicodeEx((uint)_deadKey.vk, _deadKey.sc, ks, c, 8, 0, hkl);
					_deadKey.vk = 0;
				} else if (n < 0) {
					_deadKey = (vk, mod, sc, hkl);
					Api.ToUnicodeEx((uint)vk, sc, ks, c, 8, 0, hkl);
				}
			}
			if (n == 0) return false; //non-char key

			void _SetKS(KMod m) {
				ks[(int)KKey.Shift] = (byte)((0 != (m & KMod.Shift)) ? 0x80 : 0);
				ks[(int)KKey.Ctrl] = (byte)((0 != (m & KMod.Ctrl)) ? 0x80 : 0);
				ks[(int)KKey.Alt] = (byte)((0 != (m & KMod.Alt)) ? 0x80 : 0);
				//ks[(int)KKey.Win] = (byte)((0 != (m & KMod.Win)) ? 0x80 : 0);
				ks[(int)KKey.CapsLock] = (byte)(keys.isCapsLock ? 1 : 0); //don't need this for num lock
			}
		}
		return true;

		//Notes:
		//1. Does not work with eg Chinese input method.
		//2. Catches everything that would later be changed by the app, or by a next hook, etc.
		//3. Don't know how to get Alt+numpad characters. Ignore them.
		//	On Alt up could call tounicodeex with sc with flag 0x8000. It gets the char, but resets keyboard state, and the char is not typed.
		//4. In console windows does not work with Unicode characters.

		//if(MapVirtualKeyEx(vk, MAPVK_VK_TO_CHAR, hkl)&0x80000000) { print.it("DEAD"); return -1; } //this cannot be used because resets dead key
	}

	/// <summary>
	/// Clears internal fields such as dead key state.
	/// </summary>
	public void Clear() {
		_deadKey = default;
	}

	/// <summary>
	/// Returns <c>true</c> if the key + modifiers could generate a character, including Enter and Tab but not other control characters.
	/// </summary>
	internal static bool IsPossiblyChar_(KMod m, KKey k) {
		if (m.Has(KMod.Win) || (m & ~KMod.Shift) == KMod.Ctrl || m == (KMod.Alt | KMod.Shift)) return false;
		if (k is KKey.Back or KKey.Escape) return false;
		return true;
	}
}
