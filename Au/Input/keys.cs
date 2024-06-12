//TODO3: on exception release modifiers.

namespace Au;

/// <summary>
/// Keyboard functions. Send virtual keystrokes and text to the active window, get key state, wait for key.
/// </summary>
/// <remarks>
/// The main function is <see cref="send"/>. Most documentation is there. See also <see cref="sendt"/>. These functions use <see cref="opt.key"/>. Alternatively can be used <b>keys</b> variables, see <see cref="keys(OKey)"/>.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// keys.send("Ctrl+Shift+Left"); //press Ctrl+Shift+Left
/// 
/// opt.key.KeySpeed = 300; //set options for static functions
/// keys.send("Ctrl+A Del Tab*3", "!text", "Enter", 500); //press Ctrl+A, Del, Tab 3 times, send text, Enter, wait 500 ms
/// 
/// keys.sendt("text\r\n"); //send text that ends with newline
/// ]]></code>
/// </example>
public partial class keys {
	/// <param name="cloneOptions">Options to be copied to <see cref="Options"/> of this variable. If <c>null</c>, uses default options.</param>
	/// <example>
	/// <code><![CDATA[
	/// var k = new keys(opt.init.key);
	/// k.Options.KeySpeed = 50;
	/// k.AddKeys("Tab // Space").AddRepeat(3).AddText("text").AddKey(KKey.Enter).AddSleep(500);
	/// k.SendNow(); //sends and clears the variable
	/// k.Add("Tab // Space*3", "!text", KKey.Enter, 500); //the same as the above k.AddKeys... line
	/// for(int i = 0; i < 5; i++) k.SendNow(true); //does not clear the variable
	/// ]]></code>
	/// </example>
	public keys(OKey cloneOptions) { Options = new OKey(cloneOptions); }

	/// <summary>
	/// Options used by this variable.
	/// </summary>
	public OKey Options { get; }

	//KEYEVENTF_ flags for API SendInput.
	[Flags]
	enum _KFlags : byte {
		Extended = 1,
		Up = 2,
		Unicode = 4,
		Scancode = 8,
	};

	//_KEvent type - key, text, sleep, etc.
	enum _KType : byte {
		KeyEvent, //send key down or up event, depending on _KFlags.Up. In _KEvent used vk and scan.
		KeyPair, //send key down and up events. In _KEvent used vk and scan.
		Char, //send character using keys. In _KEvent used ch.
		Text, //send text. In _KEvent used data, it is _data or _data element index.
		Callback, //call callback function. In _KEvent used data, it is _data or _data element index.
		Repeat, //repeat previous key. In _KEvent used repeat.
		Sleep, //sleep. In _KEvent used sleep.
	}

	[StructLayout(LayoutKind.Explicit)]
	struct _KEvent {
		[FieldOffset(0)] internal KKey vk; //byte
		[FieldOffset(1)] byte _flags; //_KFlags in 0x0F and _KType in 0xF0
		[FieldOffset(2)] internal ushort scan; //scan code if IsKey
		[FieldOffset(2)] internal ushort data; //_data or _data index if IsText or IsCallback
		[FieldOffset(2)] internal ushort repeat; //repeat count if IsRepeat
		[FieldOffset(2)] internal ushort sleep; //milliseconds if IsSleep
		[FieldOffset(2)] internal ushort ch; //character if IsChar

		//Event type KeyEvent or KeyPair.
		internal _KEvent(bool pair, KKey vk, _KFlags siFlags, ushort scan = 0) {
			this.vk = vk;
			var f = (byte)siFlags; if (pair) f |= 16; _flags = f;
			this.scan = scan;
		}

		//Event of any type except KeyEvent and KeyPair.
		internal _KEvent(_KType type, ushort data) {
			Debug.Assert(type > _KType.KeyPair);
			_flags = (byte)((byte)type << 4);
			this.data = data;
		}

		internal _KType Type => (_KType)(_flags >> 4);
		internal bool IsPair => Type is _KType.KeyPair;
		internal bool IsKey => Type <= _KType.KeyPair;
		internal bool IsChar => Type == _KType.Char;
		internal bool IsKeyOrChar => Type <= _KType.Char;
		internal bool IsText => Type == _KType.Text;
		internal bool IsCallback => Type == _KType.Callback;
		internal bool IsRepeat => Type == _KType.Repeat;
		internal bool IsSleep => Type == _KType.Sleep;
		internal bool IsUp => 0 != (_flags & 2);
		internal _KFlags SIFlags => (_KFlags)(_flags & 15);
		internal void MakeDown() => _flags &= 9;
		internal void MakeUp() => _flags = (byte)((_flags & 9) | 2);

#if DEBUG
		public override string ToString() {
			if (IsText) { Debug.Assert(SIFlags == 0); return $"text " + data; }
			if (IsCallback) { Debug.Assert(SIFlags == 0); return $"callback " + data; }
			if (IsSleep) { Debug.Assert(SIFlags == 0); return "sleep " + sleep; }
			if (IsRepeat) { Debug.Assert(SIFlags == 0); return "repeat " + repeat; }
			if (IsChar) { Debug.Assert(SIFlags == 0); return "char " + ch; }
			return $"{vk,-12} scan={scan,-4} flags={_flags}";
		}
#endif
	}

	//This struct is used to separate parsing-only fields from other fields.
	struct _KParsingState {
		public Stack<_KEvent> mod; //pushed on "+" or "+(". Then popped on key not preceded by +, and also in Send().
		public bool paren; //we are between "+(" and ")"
		public bool plus; //we are between "+" and key or text
	}

	//This struct is used to separate sending-only fields from other fields.
	struct _KSendingState {
		public wnd wFocus;
		public OKey options;

		public void Clear() {
			wFocus = default;
		}
	}

	readonly List<_KEvent> _a = new(); //all key events and elements for each text/callback/repeat/sleep
	object _data; //text and callback parts. If there is 1 such part, it is string or Action; else it is List<object>.
	_KParsingState _pstate; //parsing state
	_KSendingState _sstate; //sending state
	bool _sending; //while sending, don't allow to add or send
	bool? _antiCapsLock;

	/// <summary>
	/// Adds keystrokes to the internal collection. They will be sent by <see cref="SendNow"/>.
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="keys_">
	/// Key names and operators, like with <see cref="send"/>. Can be <c>null</c> or <c>""</c>.
	/// Example: <c>"Tab Ctrl+V Alt+(E P) Left*3 Space a , 5 #5"</c>.
	/// If has prefix <c>"!"</c> or <c>"%"</c>, calls <see cref="AddText(string, string)"/>; use <c>"!"</c> for text, <c>"%"</c> for HTML.
	/// </param>
	/// <exception cref="ArgumentException">Error in <i>keys_</i> string, for example an unknown key name.</exception>
	public keys AddKeys([ParamString(PSFormat.Keys)] string keys_) {
		_ThrowIfSending();
		var k = keys_;
		if (k.NE()) return this;
		if (k[0] == '!') return AddText(k[1..]);
		if (k[0] == '%') return AddText(null, k[1..]);
		int i = 0, len = 0;
		foreach (var g in _SplitKeysString(k)) {
			//print.it($"<><c #C000>{g.Value}</c>"); //continue;
			i = g.Start; len = g.Length;
			char c = k[i]; _KEvent e;
			switch (c) {
			case '*':
				if (len == 1 || _a.Count == 0) goto ge;
				e = _a[^1];
				char cLast = k[i + len - 1];
				switch (cLast) {
				case 'n': //down
				case 'p': //up
					if (e.IsPair) {
						//make the last key down-only or up-only
						if (cLast == 'p') e.MakeUp(); else e.MakeDown();
						_a[^1] = e;
					} else if (cLast == 'p' && _FindLastKey(out e)) {
						//allow eg Key("A*down*3*up") or Key("A*down", 500, "*up")
						e.MakeUp();
						_a.Add(e);
					} else goto ge;
					break;
				default: //repeat
					if (!e.IsKeyOrChar) goto ge;
					AddRepeat(k.ToInt(i + 1));
					break;
				}
				break;
			case '+':
				if (_pstate.paren || _a.Count == 0) goto ge;
				e = _a[^1];
				if (!e.IsPair) goto ge;
				e.MakeDown();
				_a[^1] = e;
				e.MakeUp();
				_pstate.mod ??= new Stack<_KEvent>();
				_pstate.mod.Push(e);
				if (len > 1) _pstate.paren = true; //"*("
				else _pstate.plus = true;
				break;
			case ')':
				if (!_pstate.paren) goto ge;
				_pstate.paren = false;
				_AddModUp();
				break;
			case '_' when len == 2:
				AddChar(k[i + 1]);
				break;
			case '^':
				if (_pstate.paren) goto ge;
				if (++i == g.End) break;
				if (g.End - i == 1 || _pstate.plus) {
					while (i < g.End) AddChar(k[i++]);
				} else { //avoid eg Shift up/down between AB
					_AddTextAndHow(k[i..], OKeyText.KeysOrChar, true);
				}
				break;
			//case '!': //rejected. Too many rules. Better slightly longer code than 2 ways to do the same.
			//	AddText(k[++i..]);
			//	break;
			default:
				//rejected: if non-ASCII, use AddChar. Why to add yet another rule for something rarely used.
				var vk = _KeynameToKey(k, i, len);
				if (vk == 0) goto ge;
				AddKey(vk);
				//print.it(vk);
				break;
			}
		}
		return this;
		ge: throw _ArgumentException_ErrorInKeysString(k, i, len);

		bool _FindLastKey(out _KEvent e) {
			for (int j = _a.Count; --j >= 0;) {
				var t = _a[j];
				if (t.IsKey) { e = t; return true; }
			}
			e = default; return false;
		}
	}

	//Adds mod up events if need: if _parsing.mod is not empty and + is not active and ( is not active.
	void _AddModUp() {
		if (!_pstate.plus && !_pstate.paren && _pstate.mod != null) {
			while (_pstate.mod.Count != 0) _a.Add(_pstate.mod.Pop());
		}
	}

	//Adds key or other event. Calls _ModUp(). Not used fo sleep and repeat.
	keys _AddKEvent(_KEvent e) {
		_AddModUp();
		_pstate.plus = false;
		_a.Add(e);
		return this;
	}

	/// <summary>
	/// Adds single key, specified as <see cref="KKey"/>, to the internal collection. It will be sent by <see cref="SendNow"/>.
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="key">Virtual-key code, like <c>KKey.Tab</c> or <c>(KKey)200</c>. Valid values are 1-255.</param>
	/// <param name="down"><c>true</c> - key down; <c>false</c> - key up; <c>null</c> (default) - key down-up.</param>
	/// <exception cref="ArgumentException">Invalid <i>key</i> (0).</exception>
	public keys AddKey(KKey key, bool? down = null) {
		_ThrowIfSending();
		if (key == 0) throw new ArgumentException("Invalid value.", nameof(key));

		_KFlags f = 0;
		if (down == false) f |= _KFlags.Up;
		if (KeyTypes_.IsExtended(key)) f |= _KFlags.Extended;

		return _AddKEvent(new _KEvent(down == null, key, f));
	}

	/// <summary>
	/// Adds single key to the internal collection. Allows to specify scan code and whether it is an extended key. It will be sent by <see cref="SendNow"/>.
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="key">Virtual-key code, like <c>KKey.Tab</c> or <c>(KKey)200</c>. Valid values are 1-255. Can be 0.</param>
	/// <param name="scanCode">Scan code of the physical key. Scan code values are 1-127, but this function allows 1-0xffff. Can be 0.</param>
	/// <param name="extendedKey"><c>true</c> if the key is an extended key.</param>
	/// <param name="down"><c>true</c> - key down; <c>false</c> - key up; <c>null</c> (default) - key down-up.</param>
	/// <exception cref="ArgumentException">Invalid scan code.</exception>
	public keys AddKey(KKey key, ushort scanCode, bool extendedKey, bool? down = null) {
		_ThrowIfSending();
		_KFlags f = 0;
		if (key == 0) f = _KFlags.Scancode;
		else {
			//don't: if extendedKey false, set true if need. Don't do it because this func is 'raw'.
		}

		if (down == false) f |= _KFlags.Up;
		if (extendedKey) f |= _KFlags.Extended;

		return _AddKEvent(new _KEvent(down == null, key, f, scanCode));
	}

	/// <summary>
	/// Adds single character to the internal collection. It will be sent like text with option <see cref="OKeyText.KeysOrChar"/>.
	/// </summary>
	/// <returns>This.</returns>
	public keys AddChar(char c) {
		_ThrowIfSending();
		return _AddKEvent(new _KEvent(_KType.Char, c));
	}


	/// <summary>
	/// Adds key down or up event.
	/// </summary>
	/// <param name="vk"></param>
	/// <param name="scan"></param>
	/// <param name="siFlags"><b>SendInput</b> flags.</param>
	internal keys AddRaw_(KKey vk, ushort scan, byte siFlags) {
		_ThrowIfSending();
		return _AddKEvent(new _KEvent(false, vk, (_KFlags)(siFlags & 0xf), scan));
	}

	/// <summary>
	/// Sends key events added by <b>AddRaw_</b> (called by <b>inputBlocker</b>).
	/// Simply calls <b>Api.SendInput</b>. No options, no sleep, etc.
	/// If new events added while sending, sends them too, until there are no new events added.
	/// </summary>
	/// <param name="onlyUp">Send only "up" events.</param>
	internal unsafe void SendBlocked_(bool onlyUp) {
		for (int ii = 0; ii < 5; ii++) {
			int n = 0;
			var a = new Api.INPUTK[_a.Count];
			for (int i = 0; i < _a.Count; i++) {
				var k = _a[i];
				if (onlyUp && !k.IsUp) continue;
				a[n++].Set(k.vk, k.scan, (uint)k.SIFlags);
			}
			_a.Clear();
			if (n == 0) return;
			fixed (Api.INPUTK* p = a) Api.SendInput(p, n);
			//wait.doEvents(); //sometimes catches one more event, but not necessary

			if (_a.Count == 0) break;
			Debug_.PrintIf(ii == 4, "loop?");
			//The hook proc is called while in SendInput. If we don't retry, new blocked keys are lost.
			//	But don't retry forever, because in some cases OS injects keys and the hook recives them not marked as injected,
			//		eg on Shift if it is set to turn off CapsLock.
		}
	}

	/// <summary>
	/// Adds text or HTML. It will be sent by <see cref="SendNow"/>.
	/// </summary>
	/// <returns>This.</returns>
	/// <remarks>
	/// To send text can use keys, characters or clipboard, depending on <see cref="opt.key"/> and text. If <i>html</i> not <c>null</c>, uses clipboard.
	/// </remarks>
	/// <inheritdoc cref="sendt" path="/param"/>
	public keys AddText(string text, string html = null) {
		_ThrowIfSending();
		if (!html.NE()) {
			var data = new clipboardData().AddHtml(html).AddText(text ?? html);
			var ke = new _KEvent(_KType.Text, _SetData(data));
			_AddKEvent(ke);
		} else if (!text.NE()) {
			var ke = new _KEvent(_KType.Text, _SetData(text));
			_AddKEvent(ke);
		}
		return this;
	}

	/// <summary>
	/// Adds text with explicitly specified sending method (keys, characters or paste).
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="text">Text. Can be <c>null</c>.</param>
	/// <param name="how">Overrides <see cref="OKey.TextHow"/>.</param>
	public keys AddText(string text, OKeyText how) {
		_ThrowIfSending();
		_AddTextAndHow(text, how, false);
		return this;
	}

	void _AddTextAndHow(string text, OKeyText how, bool keysArg) {
		if (!text.NE()) {
			int flags = (int)how | 0x80; if (keysArg) flags |= 0x40;
			var ke = new _KEvent(_KType.Text, _SetData(text)) { vk = (KKey)flags };
			_AddKEvent(ke);
		}
	}

	/// <summary>
	/// Adds clipboard data, for example several formats. It will be pasted by <see cref="SendNow"/>.
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="cd">Clipboard data.</param>
	public keys AddClipboardData(clipboardData cd) {
		Not_.Null(cd);
		_ThrowIfSending();
		_AddKEvent(new _KEvent(_KType.Text, _SetData(cd)));
		return this;
	}

	//Adds text (string) or clipboardData or callback (Action) to _data.
	ushort _SetData(object x) {
		int i;
		if (_data == null) {
			i = 0;
			_data = x;
		} else if (_data is List<object> a) {
			i = a.Count;
			a.Add(x);
		} else {
			i = 1;
			_data = new List<object>() { _data, x };
		}
		return checked((ushort)i);
	}

	//Gets text (string) or clipboardData or callback (Action) from _data.
	object _GetData(ushort i) {
		if (_data is List<object> a) return a[i];
		return _data;
	}

	/// <summary>
	/// Adds a callback function.
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="a"></param>
	/// <remarks>
	/// The callback function will be called by <see cref="SendNow"/> and can do anything except sending keys and copy/paste.
	/// </remarks>
	public keys AddAction(Action a) {
		Not_.Null(a);
		_ThrowIfSending();
		return _AddKEvent(new _KEvent(_KType.Callback, _SetData(a)));
	}

	/// <summary>
	/// Adds the repeat operator. Then <see cref="SendNow"/> will send the last added key or character <i>count</i> times.
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="count">The repeat count.</param>
	/// <exception cref="ArgumentOutOfRangeException"><i>count</i> &gt;10000 or &lt;0.</exception>
	/// <exception cref="ArgumentException">The last added item is not a key or single character.</exception>
	public keys AddRepeat(int count) {
		_ThrowIfSending();
		if ((uint)count > 10000) throw new ArgumentOutOfRangeException(nameof(count), "Max repeat count is 10000.");
		int i = _a.Count; if (i == 0 || !_a[i - 1].IsKeyOrChar) throw new ArgumentException("No key to repeat.");
		_a.Add(new _KEvent(_KType.Repeat, (ushort)count));
		return this;
	}

	/// <summary>
	/// Adds a short pause. Then <see cref="SendNow"/> will sleep (wait).
	/// </summary>
	/// <returns>This.</returns>
	/// <param name="timeMS">Time to sleep, milliseconds.</param>
	/// <exception cref="ArgumentOutOfRangeException"><i>timeMS</i> &gt;10000 (1 minute) or &lt;0.</exception>
	public keys AddSleep(int timeMS) {
		_ThrowIfSending();
		if ((uint)timeMS > 10000) throw new ArgumentOutOfRangeException(nameof(timeMS), "Max sleep time is 10000.");
		_a.Add(new _KEvent(_KType.Sleep, (ushort)timeMS));
		return this;
	}

	/// <summary>
	/// Adds keystrokes, text, sleep and other events to the internal collection. They will be sent/executed by <see cref="SendNow"/>.
	/// </summary>
	/// <returns>This.</returns>
	/// <inheritdoc cref="keys.send" path="/param"/>
	public keys Add([ParamString(PSFormat.Keys)] params KKeysEtc[] keysEtc) {
		_ThrowIfSending();
		if (keysEtc != null) {
			for (int i = 0; i < keysEtc.Length; i++) {
				var o = keysEtc[i].Value ?? "";
				switch (o) {
				case string s:
					AddKeys(s);
					break;
				case clipboardData cd:
					AddClipboardData(cd);
					break;
				case KKey k:
					AddKey(k);
					break;
				case char c:
					AddChar(c);
					break;
				case int ms:
					AddSleep(ms);
					break;
				case Action g:
					AddAction(g);
					break;
				case KKeyScan t:
					AddKey(t.vk, t.scanCode, t.extendedKey);
					break;
				}
			}
		}
		return this;
	}

	/// <summary>
	/// Sends keys, text and executes other events added with the <b>AddX</b> functions.
	/// </summary>
	/// <param name="canSendAgain">Don't clear the internal collection. If <c>true</c>, this function then can be called again (eg in loop) to send/execute the same keys etc. If <c>false</c> (default), clears the added keys etc; then you can call <b>AddX</b> functions and <b>Send</b> again.</param>
	/// <exception cref="ArgumentException"><i>canSendAgain</i> is <c>true</c> and <i>keys_</i> end with <c>+</c> or <c>(</c>.</exception>
	/// <exception cref="AuException">Failed. For example there is no focused window when sending text.</exception>
	/// <exception cref="InputDesktopException"></exception>
	public void SendNow(bool canSendAgain = false) {
		//note: the "Now" in the name is just to make it different from the static function send(). If named Send, problems with DocFX etc.

		_ThrowIfSending();
		if (_a.Count == 0) return;
		if (canSendAgain) {
			if (_pstate.paren || _pstate.plus) throw new ArgumentException("canSendAgain cannot be true if keys_ ends with + or (");
		}

		//print.it("-- _parsing.mod --");
		//print.it(_parsing.mod);

		_AddModUp(); //add mod-up events if need, eg Ctrl-up after "Ctrl+A"

		//print.it("-- _a --");
		//print.it(_a);

		//perf.first();
		int sleepFinally = 0;
		var bi = new inputBlocker() { ResendBlockedKeys = true };
		try {
			_sending = true;
			_antiCapsLock = Options.NoCapsOff || !isCapsLock ? false : null;
			//print.it("{");
			if (!Options.NoBlockInput) bi.Start(BIEvents.Keys);
			if (!Options.NoModOff) Internal_.ReleaseModAndDisableModMenu();
			//perf.next();
			for (int i = 0; i < _a.Count; i++) {
				var k = _a[i];
				switch (k.Type) {
				case _KType.Sleep:
					if (i == _a.Count - 1) sleepFinally = k.sleep;
					else Internal_.Sleep(k.sleep);
					break;
				case _KType.Repeat:
					Debug.Assert(i > 0 && _a[i - 1].IsKeyOrChar);
					break;
				case _KType.Callback:
					(_GetData(k.data) as Action)();
					break;
				case _KType.Char:
					_SendChar(k, i);
					break;
				case _KType.Text:
					_SendText(k);
					break;
				default:
					_SendKey(k, i);
					break;
				}
			}
			//perf.next();
			sleepFinally += GetOptionsAndWndFocused_(getWndAlways: false).optk.SleepFinally;
		}
		finally {
			if (_antiCapsLock == true && !isCapsLock) Internal_.SendKey(KKey.CapsLock);
			_antiCapsLock = null;
			_sending = false;
			bi.Dispose();
			//perf.nw();
			//print.it("}");

			//if canSendAgain, can be used like: AddX(); for(...) Send();
			//else can be used like: AddX(); Send(); AddX(); Send();
			if (!canSendAgain) {
				_a.Clear();
				_data = null;
				_sstate.Clear();
				//and don't clear _pstate
			}
		}

		if (sleepFinally > 0) Internal_.Sleep(sleepFinally);

		//_SyncWait();
		//CONSIDER: instead of SleepFinally use TimeSyncFinally, default 100 ms. Eg send a sync key and wait max TimeSyncFinally ms.
		//	Don't sync after each (or some) sent key. Usually it does not make sense. The final sync/sleep is useful if next statement is not an input function.
		//Sync problems:
		//	Tried many ways, nothing is good enough. The test code now is in the "Unused" project.
		//	The best would be non-LL keyboard hook that sets event when receives our sent special key-up. Especially when combined with 'get thread CPU usage' while waiting for the event. However these hooks don't work eg in Store apps.
		//Better add a Sync function (keys.sync) or/and special key name, let users do it explicitly where need.
	}

	/// <summary>
	/// Deprecated. Use <see cref="SendNow"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void Send(bool canSendAgain = false) => SendNow(canSendAgain);

	unsafe void _SendKey(_KEvent k, int i) {
		bool needScanCode = k.scan == 0 && !k.SIFlags.HasAny(_KFlags.Scancode | _KFlags.Unicode);
		var (optk, wFocus) = GetOptionsAndWndFocused_(getWndAlways: needScanCode);
		if (needScanCode) {
			var hkl = Api.GetKeyboardLayout(wFocus.ThreadId); //most layouts have the same standard scancodes, but eg dvorak different
			k.scan = Internal_.VkToSc(k.vk, hkl);
		}

		if (_antiCapsLock == null
			&& !k.SIFlags.Has(_KFlags.Unicode)
			&& k.vk is (>= KKey.A and <= KKey.Z) or (>= KKey.D0 and <= KKey.D9) or (>= KKey.OemSemicolon and <= KKey.OemTilde) or (>= KKey.OemOpenBrackets and <= KKey.OemQuotes)
			//CONSIDER: not if with a modifier
			) _AntiCapsLock();

		bool isLast = i == _a.Count - 1;
		_SendKey2(k, isLast ? default : _a[i + 1], isLast, optk);
	}

	//Caller should set k.scan; this func doesn't.
	unsafe static void _SendKey2(_KEvent k, _KEvent kNext, bool isLast, OKey optk) {
		var ki = new Api.INPUTK(k.vk, k.scan, (uint)k.SIFlags);

		int count = 1, sleep = optk.KeySpeed;
		if (isLast) {
			if (!k.IsPair) sleep = Internal_.LimitSleepTime(sleep) - optk.SleepFinally;
		} else {
			if (kNext.IsRepeat) count = kNext.repeat;
			else if (!k.IsPair) {
				//If this is pair, sleep between down and up, and don't sleep after up.
				//Else if repeat, sleep always.
				//Else in most cases don't need to sleep. In some cases need, but can limit the time.
				//	For example, in Ctrl+C normally would not need to sleep after Ctrl down and Ctrl up.
				//	However some apps/controls then may not work. Maybe they process mod and nonmod keys somehow async.
				//	For example, Ctrl+C in IE address bar often does not work if there is no sleep after Ctrl down. Always works if 1 ms.

				sleep = Internal_.LimitSleepTime(sleep);
				if (kNext.IsKey) {
					bool thisMod = KeyTypes_.IsMod(k.vk), nextMod = KeyTypes_.IsMod(kNext.vk);
					if (!k.IsUp) {
						if (kNext.IsUp) sleep = optk.KeySpeed;
						else if (thisMod == nextMod) sleep = 0;
					} else {
						if (!thisMod || nextMod) sleep = 0;
					}
				} else if (kNext.IsSleep) sleep -= kNext.sleep;
			}
		}
		if (sleep < 0) sleep = 0;

		//var s = (k.vk).ToString();
		//if (k.IsPair) print.it($"{s}<{sleep}>");
		//else { var ud = k.IsUp ? '-' : '+'; if (sleep > 0) print.it($"{s}{ud} {sleep}"); else print.it($"{s}{ud}"); }

		for (int r = 0; r < count; r++) {
			//perf.first();
			Api.SendInput(&ki);
			//perf.next();
			if (sleep > 0) {
				Internal_.Sleep(sleep);
			}
			if (k.IsPair) {
				ki.dwFlags |= Api.KEYEVENTF_KEYUP;
				Api.SendInput(&ki);
				ki.dwFlags &= ~Api.KEYEVENTF_KEYUP;
			}
			//perf.nw();
			//speed: min 400 mcs for each event. Often > 1000. Does not depend on whether all events sent by single SendInput call.
		}
	}

	unsafe void _SendChar(_KEvent ke, int i) {
		var (optk, wFocus) = GetOptionsAndWndFocused_(getWndAlways: true, requireFocus: true);
		nint hkl = Api.GetKeyboardLayout(wFocus.ThreadId);
		if (_antiCapsLock == null) _AntiCapsLock();
		int count = 1; if (i < _a.Count - 1 && _a[i + 1].IsRepeat) count = _a[i + 1].repeat;
		int speed = optk.KeySpeed; if (count > 4) speed = Math.Min(speed, optk.TextSpeed + 2);
		KMod prevMod = 0;
		try {
			_SendChar2((char)ke.ch, OKeyText.KeysOrChar, speed, count, hkl, ref prevMod, false); //note: don't use optk.TextShiftEnter
		}
		finally {
			Internal_.ModPressRelease(false, prevMod);
		}
	}

	static unsafe void _SendChar2(char c, OKeyText textHow, int sleep, int count, nint hkl, ref KMod prevMod, bool shiftEnter) {
		KKey vk = 0; KMod mod = 0;
		if (c is '\n' or '\r') { //many apps don't support these as VK_PACKET
			vk = KKey.Enter;
			if (shiftEnter) mod = KMod.Shift;
		} else if (c is ' ' or '\t') { //some apps don't support these as VK_PACKET
			vk = (KKey)c;
		} else if (textHow != OKeyText.Characters) {
			(vk, mod) = _CharToKey(c, hkl);
			//print.it(c, vk, mod, (ushort)km);
		}

		if (vk == 0) { //use vk_packet
			if (prevMod != 0) { Internal_.ModPressRelease(false, prevMod); prevMod = 0; }

			//note: need key-up event for VK_PACKET too.
			//	Known controls that need it: Qt edit controls; Office 2003 'type question' field.
		} else if (mod != prevMod) {
			var md = mod ^ prevMod;
			if (0 != (md & KMod.Ctrl)) Internal_.SendCtrl(0 != (mod & KMod.Ctrl));
			if (0 != (md & KMod.Alt)) Internal_.SendAlt(0 != (mod & KMod.Alt));
			if (0 != (md & KMod.Shift)) Internal_.SendShift(0 != (mod & KMod.Shift));
			prevMod = mod;
			if (sleep > 0) Internal_.Sleep(Internal_.LimitSleepTime(sleep)); //need for apps that process mod-nonmod keys async
		}

		var ki = new _INPUTKEY2(vk, vk == 0 ? c : Internal_.VkToSc(vk, hkl), vk == 0 ? Api.KEYEVENTF_UNICODE : 0);
		for (int r = 0; r < count; r++) {
			Api.SendInput(&ki.k0, sleep > 0 ? 1 : 2);
			if (sleep > 0) {
				Internal_.Sleep(sleep);
				Api.SendInput(&ki.k1, 1);
			}
		}
		//rejected: try to synchronize somehow. To work better with slow and badly synchronized apps.
		//1. SendTimeout(WM_NULL). Although makes slower, usually does not make sync.
		//2. Sleep if the process uses CPU eg >50% of time. Tooo slow, even with Notepad. Tried GetProcessTimes and QueryProcessCycleTime (precise).
		//Eg UWP input processing is so slow and chaotic, impossible to sync.
		//rejected: option to sleep 1 ms every n-th char (eg use float 0...1 or negative value). Nothing good.

		//using var ph = Handle_.OpenProcess(wFocus.ProcessId);
		//static int _CpuPercent(IntPtr ph) {
		//	Api.QueryProcessCycleTime(ph, out long ctime1);
		//	long t1 = perf.mcs;
		//	1.ms();
		//	long time = perf.mcs - t1;
		//	Api.QueryProcessCycleTime(ph, out long ctime2);
		//	long speedCyclesMS = 2600; //to get this can be used QueryThreadCycleTime(thisThread)->_Spin(1000)->QueryThreadCycleTime(thisThread)->/1000
		//	long cycles = ctime2 - ctime1;
		//	return (int)(cycles * 100 / time / speedCyclesMS);
		//}

		//static void _Spin(long mcs) {
		//	for (long t = perf.mcs; perf.mcs - t < mcs;) { }
		//}
	}

	static (KKey vk, KMod mod) _CharToKey(char c, nint hkl) {
		short km = Api.VkKeyScanEx(c, hkl); //note: call for non-ASCII char too; depending on keyboard layout it can succeed
		if (0 != (km & 0xf800)) return default; //-1 if failed, mod flag 8 Hankaku key, 16/32 reserved for driver
		return ((KKey)(km & 0xff), (KMod)(km >> 8));
	}

	unsafe void _SendText(_KEvent ke) {
		var (optk, wFocus) = GetOptionsAndWndFocused_(getWndAlways: true, requireFocus: true);
		object data = _GetData(ke.data); //string or clipboardData
		string s = data as string;

		OKeyText textHow; int flags = (byte)ke.vk;
		bool textHowSpecified = 0 != (flags & 0x80); //"^text" or AddText(string, OKeyText)
		if (textHowSpecified) textHow = (OKeyText)(flags & 0xf);
		else if (s != null && s.Length < optk.PasteLength) textHow = optk.TextHow;
		else textHow = OKeyText.Paste;

		if (textHow != OKeyText.Paste) {
			//use paste if there are Unicode surrogate pairs, because some apps/controls/frameworks don't support surrogates with WM_PACKET.
			//known apps that support: standard Edit and RichEdit controls, Chrome, Firefox, IE, WPF, WinForms, new Scintilla, Dreamweaver, LibreOffice.
			//known apps that don't: Office 2003, OpenOffice, old Scintilla.
			//known apps that don't if 0 sleep: QT edit controls in VirtualBox.
			//known apps that don't support these chars even when pasting: Java (tested the old and new frameworks).
			//tested: the same if SendInput(arrayOfAllChars).
			for (int i = 0; i < s.Length; i++) if ((s[i] & 0xf800) == 0xd800) { textHow = OKeyText.Paste; break; }
		}

		nint hkl = 0;
		if (textHow is OKeyText.KeysOrChar or OKeyText.KeysOrPaste) {
			hkl = Api.GetKeyboardLayout(wFocus.ThreadId);
			if (textHow == OKeyText.KeysOrPaste) {
				foreach (char c in s) {
					if (c is '\r' or '\n') continue;
					if (_CharToKey(c, hkl).vk == default) { textHow = OKeyText.Paste; break; }
				}
			}
		}
		//print.it(optk.TextHow, textHow);

		if (textHow == OKeyText.Paste) {
			Pasting?.Invoke(this, new PastingEventArgs { Text = s, Options = optk, WndFocus = wFocus });
			clipboard.Paste_(data, optk, wFocus);
			return;
		}

		if (_antiCapsLock == null && textHow is OKeyText.KeysOrChar or OKeyText.KeysOrPaste) _AntiCapsLock();

		KMod prevMod = 0;
		int sleep = 0 != (flags & 0x40) ? optk.KeySpeed : optk.TextSpeed; //0x40 if ^text

		try {
			for (int i = 0; i < s.Length; i++) {
				char c = s[i];

				if (c == '\r' && s.Eq(i + 1, '\n')) continue; //\r\n -> \n -> key Enter

				_SendChar2(c, textHow, sleep, 1, hkl, ref prevMod, textHowSpecified ? false : optk.TextShiftEnter);
			}
		}
		finally {
			Internal_.ModPressRelease(false, prevMod);
		}

		//rejected: throw if changed the focused window.
		//	Possible false positives, because everything is async.
	}

	void _AntiCapsLock(/*OKey optk*/) {
		//if (/*&& !optk.NoCapsOff*/) {
		if (!isCapsLock) {
			_antiCapsLock = false;
			return;
		}
		if (isPressed(KKey.CapsLock)) Internal_.SendKey(KKey.CapsLock, false); //never mind: in this case later may not restore CapsLock because of auto-repeat
		Internal_.SendKey(KKey.CapsLock, true);
		bool ok = isPressed(KKey.CapsLock); //the send can fail because of UAC or the Windows setting
		Internal_.SendKey(KKey.CapsLock, false);
		//note: don't call isCapsLock again here. It is unreliable because GetKeyState is sync.
		//	Eg in some cases ignores the new key state until this UI thread removes all messages from queue.
		if (!ok && IsCapsLockShiftOff_()) {
			//Shift is set to turn off CapsLock in Settings > Time & Language > Language > Keyboard > Input method > Hot keys.
			WindowsHook.IgnoreLShiftCaps_(2000);
			Internal_.SendKey(KKey.Shift);
			WindowsHook.IgnoreLShiftCaps_(0);

			//note: need IgnoreLShiftCaps_, because when we send Shift, the BlockInput hook receives these events:
			//Left Shift down, not injected //!!
			//Caps Lock down, not injected
			//Caps Lock up, not injected
			//Left Shift up, injected

			//speed: often ~15 ms. Without Shift max 5 ms.
		}
		_antiCapsLock = true;
		//}

		//note: don't make _restoreCapsLock false if still isCapsLock true, because isCapsLock unreliable.
		//	If SendKey(CapsLock) did not work now, it probably will not work afterwards.

		//CONSIDER: remove this feature, or set non-default.
		//	Instead, when sending text as keys, if CapsLock, invert Shift. Eg PAD uses this.
		//	But what then should do when sending keys (not text)? Probably should ignore CapsLock.
		//	Probably safer with CapsLock off. Eg some target apps may interpret text differently when with an unexpected Shift.
	}

	/// <summary>
	/// Returns <c>true</c> if <c>Shift</c> is set to turn off <c>CapsLock</c> (system setting).
	/// </summary>
	internal static bool IsCapsLockShiftOff_() => s_isCapsLockShiftOff ??= Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Keyboard Layout", "Attributes", 0) is int r1 && 0 != (r1 & 0x10000);
	static bool? s_isCapsLockShiftOff;

	/// <summary>
	/// Before pasting text through clipboard.
	/// </summary>
	public event EventHandler<PastingEventArgs> Pasting;
}
