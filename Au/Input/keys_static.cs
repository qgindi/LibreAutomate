namespace Au;

public partial class keys {
	#region get key state

	/// <summary>
	/// Gets key states for using in UI code (winforms, WPF, etc).
	/// </summary>
	/// <remarks>
	/// Use functions of this class in user interface code (winforms, WPF, etc). In other code (automation scripts, etc) usually it's better to use functions of <see cref="keys"/> class.
	/// 
	/// In Windows there are two API to get key state - <ms>GetKeyState</ms> and <ms>GetAsyncKeyState</ms>.
	/// 
	/// API <ms>GetAsyncKeyState</ms> is used by class <see cref="keys"/> and not by this class (<c>keys.gui</c>). When physical key state changes (pressed/released), <c>GetAsyncKeyState</c> sees the change immediately. It is good in automation scripts, but not good in UI code because the state is not synchronized with the message queue.
	/// 
	/// This class (<c>keys.gui</c>) uses API <ms>GetKeyState</ms>. In the foreground thread (of the active window), it sees key state changes not immediately but after the thread reads key messages from its queue. It is good in UI threads. In background threads this API usually works like <c>GetAsyncKeyState</c>, but it depends on API <ms>AttachThreadInput</ms> and in some cases is less reliable, for example may be unaware of keys pressed before the thread started.
	/// 
	/// The key state returned by these API is not always the same as of the physical keyboard. There is no API to get real physical state. Some cases when it is different:
	/// 1. The key is pressed or released by software, such as the <see cref="send"/> function of this library.
	/// 2. The key is blocked by a low-level hook. For example, hotkey triggers of this library use hooks.
	/// 3. The foreground window belongs to a process with higher UAC integrity level.
	/// 
	/// Also there is API <ms>GetKeyboardState</ms>. It gets states of all keys in single call. Works like <ms>GetKeyState</ms>.
	/// </remarks>
	public static class gui {
		//rejected: instead of class keys.gui add property keys.isUIThread. If true, let its functions work like now keys.gui.

		/// <summary>
		/// Calls API <ms>GetKeyState</ms> and returns its return value.
		/// </summary>
		/// <remarks>
		/// If returns &lt; 0, the key is pressed. If the low-order bit is 1, the key is toggled; it works only with <c>CapsLock</c>, <c>NumLock</c>, <c>ScrollLock</c> and several other keys, as well as mouse buttons.
		/// Can be used for mouse buttons too, for example <c>keys.gui.getKeyState(KKey.MouseLeft)</c>. When mouse left and right buttons are swapped, gets logical state, not physical.
		/// </remarks>
		public static short getKeyState(KKey key) => Api.GetKeyState((int)key);

		/// <summary>
		/// Returns <c>true</c> if the specified key or mouse button is pressed.
		/// </summary>
		/// <remarks>
		/// Can be used for mouse buttons too. Example: <c>keys.gui.isPressed(KKey.MouseLeft)</c>. When mouse left and right buttons are swapped, gets logical state, not physical.
		/// </remarks>
		public static bool isPressed(KKey key) => getKeyState(key) < 0;

		/// <summary>
		/// Returns <c>true</c> if the specified key or mouse button is toggled.
		/// </summary>
		/// <remarks>
		/// Works only with <c>CapsLock</c>, <c>NumLock</c>, <c>ScrollLock</c> and several other keys, as well as mouse buttons.
		/// </remarks>
		public static bool isToggled(KKey key) => 0 != (getKeyState(key) & 1);

		/// <summary>
		/// Returns <c>true</c> if the <c>Alt</c> key is pressed.
		/// </summary>
		public static bool isAlt => isPressed(KKey.Alt);

		/// <summary>
		/// Returns <c>true</c> if the <c>Ctrl</c> key is pressed.
		/// </summary>
		public static bool isCtrl => isPressed(KKey.Ctrl);

		/// <summary>
		/// Returns <c>true</c> if the <c>Shift</c> key is pressed.
		/// </summary>
		public static bool isShift => isPressed(KKey.Shift);

		/// <summary>
		/// Returns <c>true</c> if the <c>Win</c> key is pressed.
		/// </summary>
		public static bool isWin => isPressed(KKey.Win) || isPressed(KKey.RWin);

		/// <summary>
		/// Returns <c>true</c> if some modifier keys are pressed.
		/// </summary>
		/// <param name="mod">Return <c>true</c> if some of these keys are pressed. Default: <c>Ctrl</c>, <c>Shift</c> or <c>Alt</c>.</param>
		/// <remarks>
		/// By default does not check the <c>Win</c> key, as it is not used in UI, but you can include it in <i>mod</i> if need.
		/// </remarks>
		public static bool isMod(KMod mod = KMod.Ctrl | KMod.Shift | KMod.Alt) {
			if (0 != (mod & KMod.Ctrl) && isCtrl) return true;
			if (0 != (mod & KMod.Shift) && isShift) return true;
			if (0 != (mod & KMod.Alt) && isAlt) return true;
			if (0 != (mod & KMod.Win) && isWin) return true;
			return false;
		}

		/// <summary>
		/// Gets flags indicating which modifier keys are pressed.
		/// </summary>
		/// <param name="mod">Check only these keys. Default: <c>Ctrl</c>, <c>Shift</c>, <c>Alt</c>.</param>
		/// <remarks>
		/// By default does not check the <c>Win</c> key, as it is not used in UI, but you can include it in <i>mod</i> if need.
		/// </remarks>
		public static KMod getMod(KMod mod = KMod.Ctrl | KMod.Shift | KMod.Alt) {
			KMod R = 0;
			if (0 != (mod & KMod.Ctrl) && isCtrl) R |= KMod.Ctrl;
			if (0 != (mod & KMod.Shift) && isShift) R |= KMod.Shift;
			if (0 != (mod & KMod.Alt) && isAlt) R |= KMod.Alt;
			if (0 != (mod & KMod.Win) && isWin) R |= KMod.Win;
			return R;
		}

		/// <summary>
		/// Returns <c>true</c> if the <c>CapsLock</c> key is toggled.
		/// </summary>
		/// <remarks>
		/// The same as <see cref="keys.isCapsLock"/>.
		/// </remarks>
		public static bool isCapsLock => isToggled(KKey.CapsLock);

		/// <summary>
		/// Returns <c>true</c> if the <c>NumLock</c> key is toggled.
		/// </summary>
		/// <remarks>
		/// The same as <see cref="keys.isNumLock"/>.
		/// </remarks>
		public static bool isNumLock => isToggled(KKey.NumLock);

		/// <summary>
		/// Returns <c>true</c> if the <c>ScrollLock</c> key is toggled.
		/// </summary>
		/// <remarks>
		/// The same as <see cref="keys.isScrollLock"/>.
		/// </remarks>
		public static bool isScrollLock => isToggled(KKey.ScrollLock);
	}

	/// <summary>
	/// Returns <c>true</c> if the specified key or mouse button is pressed.
	/// In UI code use <see cref="keys.gui"/> instead.
	/// </summary>
	/// <remarks>
	/// Uses API <ms>GetAsyncKeyState</ms>.
	/// </remarks>
	public static bool isPressed(KKey key) {
		if ((key == KKey.MouseLeft || key == KKey.MouseRight) && 0 != Api.GetSystemMetrics(Api.SM_SWAPBUTTON)) key = (KKey)((int)key ^ 3); //makes this func 3 times slower, eg 2 -> 6 mcs when cold CPU. But much faster when called next time without a delay; for example mouse.isPressed(Left|Right) is not slower than mouse.isPressed(Left), although calls this func 2 times.
		return Api.GetAsyncKeyState((int)key) < 0;
	}

	/// <summary>
	/// Returns <c>true</c> if the <c>Alt</c> key is pressed. Calls <see cref="isPressed"/>.
	/// In UI code use <see cref="keys.gui"/> instead.
	/// </summary>
	public static bool isAlt => isPressed(KKey.Alt);

	/// <summary>
	/// Returns <c>true</c> if the <c>Ctrl</c> key is pressed. Calls <see cref="isPressed"/>.
	/// In UI code use <see cref="keys.gui"/> instead.
	/// </summary>
	public static bool isCtrl => isPressed(KKey.Ctrl);

	/// <summary>
	/// Returns <c>true</c> if the <c>Shift</c> key is pressed. Calls <see cref="isPressed"/>.
	/// In UI code use <see cref="keys.gui"/> instead.
	/// </summary>
	public static bool isShift => isPressed(KKey.Shift);

	/// <summary>
	/// Returns <c>true</c> if the <c>Win</c> key is pressed. Calls <see cref="isPressed"/>.
	/// In UI code use <see cref="keys.gui"/> instead.
	/// </summary>
	public static bool isWin => isPressed(KKey.Win) || isPressed(KKey.RWin);

	/// <summary>
	/// Returns <c>true</c> if some modifier keys are pressed: <c>Ctrl</c>, <c>Shift</c>, <c>Alt</c>, <c>Win</c>. Calls <see cref="isPressed"/>.
	/// In UI code use <see cref="keys.gui"/> instead.
	/// </summary>
	/// <param name="mod">Return <c>true</c> if some of these keys are pressed. Default - any.</param>
	/// <seealso cref="waitForNoModifierKeys"/>
	public static bool isMod(KMod mod = KMod.Ctrl | KMod.Shift | KMod.Alt | KMod.Win) {
		if (0 != (mod & KMod.Ctrl) && isCtrl) return true;
		if (0 != (mod & KMod.Shift) && isShift) return true;
		if (0 != (mod & KMod.Alt) && isAlt) return true;
		if (0 != (mod & KMod.Win) && isWin) return true;
		return false;
	}

	/// <summary>
	/// Gets flags indicating which modifier keys are pressed: <c>Ctrl</c>, <c>Shift</c>, <c>Alt</c>, <c>Win</c>. Calls <see cref="isPressed"/>.
	/// In UI code use <see cref="keys.gui"/> instead.
	/// </summary>
	/// <param name="mod">Check only these keys. Default - all four.</param>
	public static KMod getMod(KMod mod = KMod.Ctrl | KMod.Shift | KMod.Alt | KMod.Win) {
		KMod R = 0;
		if (0 != (mod & KMod.Ctrl) && isCtrl) R |= KMod.Ctrl;
		if (0 != (mod & KMod.Shift) && isShift) R |= KMod.Shift;
		if (0 != (mod & KMod.Alt) && isAlt) R |= KMod.Alt;
		if (0 != (mod & KMod.Win) && isWin) R |= KMod.Win;
		return R;
	}

	/// <summary>
	/// Returns <c>true</c> if the <c>CapsLock</c> key is toggled.
	/// </summary>
	public static bool isCapsLock => gui.isCapsLock;

	/// <summary>
	/// Returns <c>true</c> if the <c>NumLock</c> key is toggled.
	/// </summary>
	public static bool isNumLock => gui.isNumLock;

	/// <summary>
	/// Returns <c>true</c> if the <c>ScrollLock</c> key is toggled.
	/// </summary>
	public static bool isScrollLock => gui.isScrollLock;

	#endregion

	#region wait

	/// <summary>
	/// Waits while some modifier keys (<c>Ctrl</c>, <c>Shift</c>, <c>Alt</c>, <c>Win</c>) are pressed. See <see cref="isMod"/>.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="mod">Check only these keys. Default: all.</param>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if > 0).</exception>
	public static bool waitForNoModifierKeys(Seconds timeout = default, KMod mod = KMod.Ctrl | KMod.Shift | KMod.Alt | KMod.Win) {
		return waitForNoModifierKeysAndMouseButtons(timeout, mod, 0);
	}

	/// <summary>
	/// Waits while some modifier keys (<c>Ctrl</c>, <c>Shift</c>, <c>Alt</c>, <c>Win</c>) or mouse buttons are pressed.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout). Default 0.</param>
	/// <param name="mod">Check only these keys. Default: all.</param>
	/// <param name="buttons">Check only these buttons. Default: all.</param>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if > 0).</exception>
	/// <seealso cref="isMod"/>
	/// <seealso cref="mouse.isPressed"/>
	/// <seealso cref="mouse.waitForNoButtonsPressed"/>
	public static bool waitForNoModifierKeysAndMouseButtons(Seconds timeout = default, KMod mod = KMod.Ctrl | KMod.Shift | KMod.Alt | KMod.Win, MButtons buttons = MButtons.Left | MButtons.Right | MButtons.Middle | MButtons.X1 | MButtons.X2) {
		var loop = new WaitLoop(timeout);
		for (; ; ) {
			if (!isMod(mod) && !mouse.isPressed(buttons)) return true;
			if (!loop.Sleep()) return false;
		}
	}

	/// <summary>
	/// Waits while the specified keys or/and mouse buttons are pressed.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="keys_">One or more keys or/and mouse buttons. Waits until all are released.</param>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if > 0).</exception>
	public static bool waitForReleased(Seconds timeout, params KKey[] keys_) {
		return wait.until(timeout, () => {
			foreach (var k in keys_) if (isPressed(k)) return false;
			return true;
		});
	}

	/// <summary>
	/// Waits while the specified keys are pressed.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="keys_">One or more [keys](xref:key_names) without operators. Waits until all are released.</param>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="ArgumentException">Error in <i>keys_</i> string.</exception>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if > 0).</exception>
	public static bool waitForReleased(Seconds timeout, string keys_) {
		return waitForReleased(timeout, more.parseKeysString(keys_));
	}

	/// <summary>
	/// Waits for key-down or key-up event of the specified key.
	/// </summary>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="key">Wait for this key.</param>
	/// <param name="up">Wait for key-up event.</param>
	/// <param name="block">Make the event invisible to other apps. If <i>up</i> is <c>true</c>, makes the down event invisible too, if it comes while waiting for the up event.</param>
	/// <exception cref="ArgumentException"><i>key</i> is 0.</exception>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if > 0).</exception>
	/// <remarks>
	/// Waits for key event, not for key state.
	/// Uses low-level keyboard hook. Can wait for any single key. See also <see cref="waitForHotkey"/>.
	/// Ignores key events injected by functions of this library.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// keys.waitForKey(0, KKey.Ctrl, up: false, block: true);
	/// print.it("Ctrl");
	/// ]]></code>
	/// </example>
	public static bool waitForKey(Seconds timeout, KKey key, bool up = false, bool block = false) {
		if (key == 0) throw new ArgumentException();
		return 0 != _WaitForKey(timeout, key, up, block);
	}

	/// <param name="key">Wait for this key. A single-key string. See [key names](xref:key_names).</param>
	/// <exception cref="ArgumentException">Invalid <i>key</i> string.</exception>
	/// <example>
	/// <code><![CDATA[
	/// keys.waitForKey(0, "Ctrl", up: false, block: true);
	/// print.it("Ctrl");
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="waitForKey(Seconds, KKey, bool, bool)"/>
	public static bool waitForKey(Seconds timeout, string key, bool up = false, bool block = false) {
		return 0 != _WaitForKey(timeout, more.ParseKeyNameThrow_(key), up, block);
	}

	/// <summary>
	/// Waits for key-down or key-up event of any key, and gets the key code.
	/// </summary>
	/// <returns>
	/// Returns the key code. On timeout returns 0 if <i>timeout</i> is negative; else exception.
	/// For modifier keys returns the left or right key code, for example <c>LCtrl</c>/<c>RCtrl</c>, not <c>Ctrl</c>.
	/// </returns>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if > 0).</exception>
	/// <example>
	/// <code><![CDATA[
	/// var key = keys.waitForKey(0, up: true, block: true);
	/// print.it(key);
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="waitForKey(Seconds, KKey, bool, bool)" path="/param"/>
	public static KKey waitForKey(Seconds timeout, bool up = false, bool block = false) {
		return _WaitForKey(timeout, 0, up, block);
	}

	static KKey _WaitForKey(Seconds timeout, KKey key, bool up, bool block) {
		//TODO3: if up and block: don't block if was down when starting to wait. Also in the Mouse func.

		KKey R = 0;
		using (WindowsHook.Keyboard(x => {
			if (key != 0 && !x.IsKey(key)) return;
			if (x.IsUp != up) {
				if (up && block) { //key down when waiting for up. If block, now block down too.
					if (key == 0) key = x.vkCode;
					x.BlockEvent();
				}
				return;
			}
			R = x.vkCode; //info: for mod keys returns left/right
			if (block) x.BlockEvent();
		})) wait.doEventsUntil(timeout, () => R != 0);

		return R;
	}

	/// <summary>
	/// Waits for keyboard events using callback function.
	/// </summary>
	/// <returns>
	/// Returns the key code. On timeout returns 0 if <i>timeout</i> is negative; else exception.
	/// For modifier keys returns the left or right key code, for example <c>LCtrl</c>/<c>RCtrl</c>, not <c>Ctrl</c>.
	/// </returns>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="f">Callback function that receives key down and up events. Let it return <c>true</c> to stop waiting.</param>
	/// <param name="block">Make the key down event invisible to other apps (when the callback function returns <c>true</c>).</param>
	/// <remarks>
	/// Waits for key event, not for key state.
	/// Uses low-level keyboard hook.
	/// Ignores key events injected by functions of this library.
	/// </remarks>
	/// <example>
	/// Wait for <c>F3</c> or <c>Esc</c>.
	/// <code><![CDATA[
	/// var k = keys.waitForKeys(0, k => !k.IsUp && k.Key is KKey.F3 or KKey.Escape, block: true);
	/// print.it(k);
	/// ]]></code>
	/// </example>
	public static KKey waitForKeys(Seconds timeout, Func<HookData.Keyboard, bool> f, bool block = false) {
		KKey R = 0;
		using (WindowsHook.Keyboard(x => {
			if (!f(x)) return;
			R = x.vkCode; //info: for mod keys returns left/right
			if (block && !x.IsUp) x.BlockEvent();
		})) wait.doEventsUntil(timeout, () => R != 0);

		return R;
	}
	//CONSIDER: Same for mouse.

	/// <summary>
	/// Registers a temporary hotkey and waits for it.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="hotkey">Hotkey. Can be: string like <c>"Ctrl+Shift+Alt+Win+K"</c>, tuple <c>(KMod, KKey)</c>, enum <see cref="KKey"/>, enum <c>Keys</c>, struct <see cref="KHotkey"/>.</param>
	/// <param name="waitModReleased">Also wait until hotkey modifier keys released.</param>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <exception cref="ArgumentException">Error in hotkey string.</exception>
	/// <exception cref="AuException">Failed to register hotkey.</exception>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if > 0).</exception>
	/// <remarks>
	/// Uses <see cref="RegisteredHotkey"/> (API <ms>RegisterHotKey</ms>).
	/// Fails if the hotkey is currently registered by this or another application or used by Windows.
	/// <note>Most single-key and <c>Shift+key</c> hotkeys don't work when the active window has higher UAC integrity level than this process. Media keys may work.</note>
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// keys.waitForHotkey(0, "F11");
	/// keys.waitForHotkey(0, KKey.F11);
	/// keys.waitForHotkey(0, "Shift+A", true);
	/// keys.waitForHotkey(0, (KMod.Ctrl | KMod.Shift, KKey.P)); //Ctrl+Shift+P
	/// keys.waitForHotkey(5, "Ctrl+Win+K"); //exception after 5 s
	/// if(!keys.waitForHotkey(-5, "Left")) print.it("timeout"); //returns false after 5 s
	/// ]]></code>
	/// </example>
	public static bool waitForHotkey(Seconds timeout, [ParamString(PSFormat.Hotkey)] KHotkey hotkey, bool waitModReleased = false) {
		if (s_atomWFH == 0) s_atomWFH = Api.GlobalAddAtom("Au.WaitForHotkey");
		using (RegisteredHotkey rhk = default) {
			if (!rhk.Register(s_atomWFH, hotkey)) throw new AuException(0, "*register hotkey");
			if (!wait.forPostedMessage(timeout, (ref MSG m) => m.message == Api.WM_HOTKEY && m.wParam == s_atomWFH)) return false;
		}
		
		if (waitModReleased) waitForNoModifierKeys(0, hotkey.Mod);
		
		return true;
	}
	static ushort s_atomWFH;
	
	/// <summary>
	/// Sets a temporary keyboard hook and waits for a hotkey.
	/// </summary>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), >0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <param name="hotkeys">One or more hotkeys. Examples: <c>["Ctrl+M"]</c>, <c>["Win+M", "Ctrl+Shift+Left"]</c>.</param>
	/// <param name="block">Make the key down event of the non-modifier key invisible to other apps. Default <c>true</c>.</param>
	/// <param name="waitModReleased">Also wait until hotkey modifier keys released.</param>
	/// <returns>1-based index of the element in the array of hotkeys. On timeout returns 0 (if <i>timeout</i> negative; else exception).</returns>
	/// <exception cref="TimeoutException"></exception>
	/// <remarks>
	/// Uses <see cref="keys.waitForKeys"/>.
	/// Works even if the hotkey is used by Windows (except <c>Win+L</c> and <c>Ctrl+Alt+Del</c>) or an app as a hotkey or trigger.
	/// <note>Does not work when the active window has higher UAC integrity level than this process.</note>
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// int i = keys.waitForHotkeys(-10, ["Win+Left", "Win+Right"]);
	/// print.it(i);
	/// ]]></code>
	/// </example>
	public static int waitForHotkeys(Seconds timeout, [ParamString(PSFormat.Hotkey)] KHotkey[] hotkeys, bool block = true, bool waitModReleased = false) {
		int R = 0;
		keys.waitForKeys(timeout, k => {
			if (!k.IsUp && k.Mod == 0) {
				var mod = keys.getMod();
				for (int i = 0; i < hotkeys.Length; i++) {
					if (k.Key == hotkeys[i].Key && mod == hotkeys[i].Mod) {
						R = i + 1;
						return true;
					}
				}
			}
			return false;
		}, block);
		
		if (waitModReleased && R > 0) keys.waitForNoModifierKeys(0, hotkeys[R - 1].Mod);
		
		return R;
	}

	#endregion

	/// <summary>
	/// Generates virtual keystrokes (keys, text).
	/// </summary>
	/// <param name="keysEtc">
	/// Arguments of these types:
	/// <br/>• string - [key names and operators](xref:key_names), like <c>"Enter A Ctrl+A"</c>.\
	/// Tool: in <c>""</c> string press <c>Ctrl+Space</c>.
	/// <br/>• string with prefix <c>"!"</c> - literal text.\
	/// Example: <c>var p = "pass"; keys.send("!user", "Tab", "!" + p, "Enter");</c>
	/// <br/>• string with prefix <c>"%"</c> - HTML to paste. Full or fragment.
	/// <br/>• <see cref="clipboardData"/> - clipboard data to paste.
	/// <br/>• <see cref="KKey"/> - a single key.\
	/// Example: <c>keys.send("Shift+", KKey.Left, "*3");</c> is the same as <c>keys.send("Shift+Left*3");</c>
	/// <br/>• <c>int</c> - sleep milliseconds. Max 10000.\
	/// Example: <c>keys.send("Left", 500, "Right");</c>
	/// <br/>• <see cref="Action"/> - callback function.\
	/// Example: <c>Action click = () => mouse.click(); keys.send("Shift+", click);</c>
	/// <br/>• <see cref="KKeyScan"/> - a single key, specified using scan code and/or virtual-key code and extended-key flag.\
	/// Example: <c>keys.send(new KKeyScan(0x3B, false)); //key F1</c>\
	/// Example: <c>keys.send(new KKeyScan(KKey.Enter, true)); //numpad Enter</c>
	/// <br/>• <c>char</c> - a single character. Like text with <see cref="OKeyText.KeysOrChar"/> or operator <c>^</c>.
	/// </param>
	/// <exception cref="ArgumentException">An invalid value, for example an unknown key name.</exception>
	/// <exception cref="AuException">Failed. When sending text, fails if there is no focused window.</exception>
	/// <exception cref="InputDesktopException"></exception>
	/// <remarks>
	/// String syntax: [key names and operators](xref:key_names).
	/// 
	/// Uses <see cref="opt.key"/>:
	/// <table>
	/// <tr>
	/// <th>Option</th>
	/// <th>Default</th>
	/// <th>Changed</th>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.NoBlockInput"/></td>
	/// <td><c>false</c>.
	/// Blocks user-pressed keys. Sends them afterwards.
	/// <br/>If the last argument is "sleep", stops blocking before executing it; else stops blocking after executing all arguments.</td>
	/// <td><c>true</c>.
	/// Does not block user-pressed keys.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.NoCapsOff"/></td>
	/// <td><c>false</c>.
	/// If the <c>CapsLock</c> key is toggled, untoggles it temporarily (presses it before and after).</td>
	/// <td><c>true</c>.
	/// Does not touch the <c>CapsLock</c> key.
	/// <br/>Alphabetic keys of "keys" arguments can depend on <c>CapsLock</c>. Text of "text" arguments doesn't depend on <c>CapsLock</c>, unless <see cref="OKey.TextHow"/> is <c>KeysX</c>.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.NoModOff"/></td>
	/// <td><c>false</c>.
	/// Releases modifier keys (<c>Alt</c>, <c>Ctrl</c>, <c>Shift</c>, <c>Win</c>).
	/// <br/>Does it only at the start; later they cannot interfere, unless <see cref="OKey.NoBlockInput"/> is <c>true</c>.</td>
	/// <td><c>true</c>.
	/// Does not touch modifier keys.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.TextSpeed"/></td>
	/// <td>0 ms.</td>
	/// <td>0 - 1000.
	/// Changes the speed for "text" arguments.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.KeySpeed"/></td>
	/// <td>2 ms.</td>
	/// <td>0 - 1000.
	/// Changes the speed for "keys" arguments.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.KeySpeedClipboard"/></td>
	/// <td>5 ms.</td>
	/// <td>0 - 1000.
	/// Changes the speed of <c>Ctrl+V</c> keys when pasting text or HTML using clipboard.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.SleepFinally"/></td>
	/// <td>10 ms.</td>
	/// <td>0 - 10000.
	/// <br/>Tip: to sleep finally, also can be used code like this: <c>keys.send("keys", 1000);</c>.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.TextHow"/></td>
	/// <td><see cref="OKeyText.Characters"/>.</td>
	/// <td><c>KeysOrChar</c>, <c>KeysOrPaste</c> or <c>Paste</c>.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.TextShiftEnter"/></td>
	/// <td><c>false</c>.</td>
	/// <td><c>true</c>. When sending text, instead of <c>Enter</c> send <c>Shift+Enter</c>.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.PasteLength"/></td>
	/// <td>200.
	/// <br/>This option is used for "text" arguments. If text length >= this value, uses clipboard.</td>
	/// <td>>=0.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.PasteWorkaround"/></td>
	/// <td><c>false</c>.
	/// <br/>This option is used for "text" arguments when using clipboard.
	/// </td>
	/// <td><c>true</c>.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.RestoreClipboard"/></td>
	/// <td><c>true</c>.
	/// Restore clipboard data (by default only text).
	/// <br/>This option is used for "text" and "HTML" arguments when using clipboard.</td>
	/// <td><c>false</c>.
	/// Don't restore clipboard data.</td>
	/// </tr>
	/// <tr>
	/// <td><see cref="OKey.Hook"/></td>
	/// <td><c>null</c>.</td>
	/// <td>Callback function that can modify options depending on active window etc.</td>
	/// </tr>
	/// </table>
	/// 
	/// This function does not wait until the target app receives and processes sent keystrokes and text; there is no reliable way to know it. It just adds small delays depending on options (<see cref="OKey.SleepFinally"/> etc). If need, change options or add "sleep" arguments or wait after calling this function. Sending text through the clipboard normally does not have these problems.
	/// 
	/// Don't use this function to automate windows of own thread. Call it from another thread. See example with async/await.
	/// 
	/// Administrator and uiAccess processes don't receive keystrokes sent by standard user processes. See [](xref:uac).
	/// 
	/// Mouse button codes/names (eg <see cref="KKey.MouseLeft"/>) cannot be used to click. Instead use callback, like in the "Ctrl+click" example.
	/// 
	/// You can use a <see cref="keys"/> variable instead of this function. Example: <c>new keys(null).Add("keys", "!text").SendNow();</c>. More examples in <see cref="keys(OKey)"/> topic.
	/// 
	/// This function calls <see cref="Add(KKeysEtc[])"/>, which calls these functions depending on argument type: <see cref="AddKeys"/>, <see cref="AddText"/>, <see cref="AddChar"/>, <see cref="AddClipboardData"/>, <see cref="AddKey(KKey, bool?)"/>, <see cref="AddKey(KKey, ushort, bool, bool?)"/>, <see cref="AddSleep"/>, <see cref="AddAction"/>. Then calls <see cref="SendNow"/>.
	/// 
	/// Uses API <ms>SendInput</ms>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// //Press key Enter.
	/// keys.send("Enter");
	/// 
	/// //Press keys Ctrl+A.
	/// keys.send("Ctrl+A");
	/// 
	/// //Ctrl+Alt+Shift+Win+A.
	/// keys.send("Ctrl+Alt+Shift+Win+A");
	/// 
	/// //Alt down, E, P, Alt up.
	/// keys.send("Alt+(E P)");
	/// 
	/// //Alt down, E, P, Alt up.
	/// keys.send("Alt*down E P Alt*up");
	/// 
	/// //Send text "Example".
	/// keys.send("!Example");
	/// keys.sendt("Example"); //same
	/// 
	/// //Press key End, key Backspace 3 times, send text "Text".
	/// keys.send("End Back*3", "!Text");
	/// 
	/// //Press Tab n times, send text "user", press Tab, send text "password", press Enter.
	/// int n = 5; string pw = "password";
	/// keys.send($"Tab*{n}", "!user", "Tab", "!" + pw, "Enter");
	/// 
	/// //Press Ctrl+V, wait 500 ms, press Enter.
	/// keys.send("Ctrl+V", 500, "Enter");
	/// 
	/// //F2, Ctrl+K, Left 3 times, Space, A, comma, 5, numpad 5, BrowserBack.
	/// keys.send("F2 Ctrl+K Left*3 Space a , 5 #5", KKey.BrowserBack);
	/// 
	/// //Shift down, A 3 times, Shift up.
	/// keys.send("Shift+A*3");
	/// 
	/// //Shift down, A 3 times, Shift up.
	/// keys.send("Shift+", KKey.A, "*3");
	/// 
	/// //Shift down, A, wait 500 ms, B, Shift up.
	/// keys.send("Shift+(", KKey.A, 500, KKey.B, ")");
	/// 
	/// //Send keys and text slowly.
	/// opt.key.KeySpeed = opt.key.TextSpeed = 50;
	/// keys.send("keys Shift+: Space 123456789 Space 123456789 ,Space", "!text: 123456789 123456789\n");
	/// 
	/// //Ctrl+click
	/// Action click = () => mouse.click();
	/// keys.send("Ctrl+", click);
	/// 
	/// //Ctrl+click
	/// keys.send("Ctrl+", new Action(() => mouse.click()));
	/// ]]></code>
	/// Show window and send keys/text to it when button clicked.
	/// <code><![CDATA[
	/// var b = new wpfBuilder("Window").WinSize(250);
	/// b.R.AddButton("Keys", async _ => {
	/// 	//keys.send("Tab", "!text", 2000, "Esc"); //no
	/// 	await Task.Run(() => { keys.send("Tab", "!text", 2000, "Esc"); }); //use other thread
	/// });
	/// b.R.Add("Text", out TextBox text1);
	/// b.R.AddOkCancel();
	/// b.End();
	/// if (!b.ShowDialog()) return;
	/// ]]></code>
	/// </example>
	public static void send([ParamString(PSFormat.Keys)] params KKeysEtc[] keysEtc) {
		new keys(opt.key).Add(keysEtc).SendNow();
	}
	//CONSIDER: move most of Remarks to Articles. Also make the param doc smaller, and move the big list to Remarks.

	/// <summary>
	/// Generates virtual keystrokes. Like <see cref="send"/>, but without reliability features: delays, user input blocking, resetting modifiers/<c>CapsLock</c>.
	/// </summary>
	/// <remarks>
	/// Ignores <see cref="opt.key"/> and instead uses default options with these changes:
	/// - <c>SleepFinally</c> = 0.
	/// - <c>KeySpeed</c> = 0.
	/// - <c>NoBlockInput</c> = <c>true</c>.
	/// - <c>NoCapsOff</c> = <c>true</c>.
	/// - <c>NoModOff</c> = <c>true</c>.
	/// </remarks>
	/// <seealso cref="more.sendKey"/>
	/// <inheritdoc cref="keys.send" path="//param|//exception"/>
	public static void sendL([ParamString(PSFormat.Keys)] params KKeysEtc[] keysEtc) {
		var o = new OKey() { KeySpeed = 0, NoBlockInput = true, NoCapsOff = true, NoModOff = true, SleepFinally = 0 };
		new keys(o).Add(keysEtc).SendNow();
	}

	/// <summary>
	/// Sends text to the active window, using virtual keystrokes or clipboard.
	/// </summary>
	/// <param name="text">Text. Can be <c>null</c>.</param>
	/// <param name="html">
	/// HTML. Can be full HTML or fragment. See <see cref="clipboardData.AddHtml"/>.
	/// Can be specified only <i>text</i> or only <i>html</i> or both. If both, will paste <i>html</i> in apps that support it, elsewhere <i>text</i>. If only <i>html</i>, in apps that don't support HTML will paste <i>html</i> as text.
	/// </param>
	/// <exception cref="AuException">Failed. Fails if there is no focused window.</exception>
	/// <exception cref="InputDesktopException"></exception>
	/// <remarks>
	/// Calls <see cref="AddText(string, string)"/> and <see cref="SendNow"/>.
	/// To send text can use keys, characters or clipboard, depending on <see cref="opt.key"/> and text. If <i>html</i> not <c>null</c>, uses clipboard.
	/// </remarks>
	/// <seealso cref="clipboard.paste"/>
	/// <example>
	/// <code><![CDATA[
	/// keys.sendt("Text.\r\n");
	/// ]]></code>
	/// Or use function <see cref="send"/> and prefix <c>"!"</c>. For HTML use prefix <c>"%"</c>.
	/// <code><![CDATA[
	/// keys.send("!Send this text and press key", "Enter");
	/// keys.send("%<b>bold</b> <i>italic</i>", "Enter");
	/// ]]></code>
	/// </example>
	public static void sendt(string text, string html = null) {
		new keys(opt.key).AddText(text, html).SendNow();
	}
}

//FUTURE: instead of QM2 AutoPassword: FocusPasswordField(); keys.send("!password", "Shift+Tab", "user", "Enter");
//public static void FocusPasswordField()
