namespace Au.Triggers;

/// <summary>
/// Flags of mouse triggers.
/// </summary>
[Flags]
public enum TMFlags : byte {
	/// <summary>
	/// Allow other apps to receive the mouse button or wheel message too.
	/// Used only with the click and wheel triggers.
	/// To receive and block mouse messages is used a low-level hook. Other hooks may receive blocked messages or not, depending on when they were set. 
	/// </summary>
	ShareEvent = 1,
	
	/// <summary>
	/// Run the action when the mouse button and modifier keys are released.
	/// </summary>
	ButtonModUp = 2,
	
	/// <summary>
	/// The trigger works only with left-side modifier keys.
	/// </summary>
	LeftMod = 4,
	
	/// <summary>
	/// The trigger works only with right-side modifier keys.
	/// </summary>
	RightMod = 8,
	
	//rejected. We always mod-off and eat auto-repeated and up events with a temp hook. Because OS does not disable auto-repeating like for hotkeys.
	///// <summary>
	///// Don't release modifier keys.
	///// More info: <see cref="TKFlags.NoModOff"/>.
	///// </summary>
	//NoModOff = 16,
}

/// <summary>
/// Represents a mouse trigger.
/// </summary>
public class MouseTrigger : ActionTrigger {
	internal readonly KMod modMasked, modMask;
	readonly TMFlags _flags;
	readonly TMKind _kind;
	readonly byte _data;
	readonly screen _screen;
	readonly string _paramsString;
	
	internal MouseTrigger(ActionTriggers triggers, Action<MouseTriggerArgs> action,
		TMKind kind, byte data, KMod mod, KMod modAny, TMFlags flags, screen screen,
		string paramsString, (string, int) source) : base(triggers, action, true, source) {
		const KMod csaw = KMod.Ctrl | KMod.Shift | KMod.Alt | KMod.Win;
		modMask = ~modAny & csaw;
		modMasked = mod & modMask;
		_flags = flags;
		_kind = kind;
		_data = data;
		_screen = screen;
		_paramsString = paramsString;
	}
	
	internal override void Run(TriggerArgs args) => RunT(args as MouseTriggerArgs);
	
	/// <summary>
	/// Returns <c>"Mouse"</c>.
	/// </summary>
	public override string TypeString => "Mouse";
	
	/// <summary>
	/// Returns a string containing trigger parameters.
	/// </summary>
	public override string ParamsString => _paramsString;
	
	///
	public TMKind Kind => _kind;
	
	///
	public TMClick Button => _kind == TMKind.Click ? (TMClick)_data : default;
	
	///
	public TMWheel Wheel => _kind == TMKind.Wheel ? (TMWheel)_data : default;
	
	///
	public TMEdge Edge => _kind == TMKind.Edge ? (TMEdge)_data : default;
	
	///
	public TMMove Move => _kind == TMKind.Move ? (TMMove)_data : default;
	
	///
	public screen Screen => _screen;
	
	///
	public TMFlags Flags => _flags;
}

/// <summary>
/// Mouse triggers.
/// </summary>
/// <example> See <see cref="ActionTriggers"/>.</example>
public class MouseTriggers : ITriggers, IEnumerable<MouseTrigger> {
	ActionTriggers _triggers;
	Dictionary<int, ActionTrigger> _d = new Dictionary<int, ActionTrigger>();
	
	internal MouseTriggers(ActionTriggers triggers) {
		_triggers = triggers;
	}
	
	/// <summary>
	/// Adds a mouse click trigger.
	/// </summary>
	/// <param name="button"></param>
	/// <param name="modKeys">
	/// Modifier keys, like with the <see cref="keys.send"/> function.
	/// Examples: <c>"Ctrl"</c>, <c>"Ctrl+Shift+Alt+Win"</c>.
	/// To ignore modifiers: <c>"?"</c>. Then the trigger works with any combination of modifiers.
	/// To ignore a modifier: <c>"Ctrl?"</c>. Then the trigger works with or without the modifier. More examples: <c>"Ctrl?+Shift?"</c>, <c>"Ctrl+Shift?"</c>.
	/// </param>
	/// <param name="flags"></param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <exception cref="ArgumentException">Invalid <i>modKeys</i> string or <i>flags</i>.</exception>
	/// <exception cref="InvalidOperationException">Cannot add triggers after <see cref="ActionTriggers.Run"/> was called, until it returns.</exception>
	/// <example>See <see cref="ActionTriggers"/>.</example>
	public Action<MouseTriggerArgs> this[TMClick button, [ParamString(PSFormat.TriggerMod)] string modKeys = null, TMFlags flags = 0, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0] {
		set {
			_Add(value, TMKind.Click, (byte)button, modKeys, flags, default, button.ToString(), (f_, l_));
		}
	}
	
	/// <summary>
	/// Adds a mouse wheel trigger.
	/// </summary>
	/// <inheritdoc cref="this[TMClick, string, TMFlags, string, int]"/>
	public Action<MouseTriggerArgs> this[TMWheel direction, [ParamString(PSFormat.TriggerMod)] string modKeys = null, TMFlags flags = 0, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0] {
		set {
			_Add(value, TMKind.Wheel, (byte)direction, modKeys, flags, default, direction.ToString(), (f_, l_));
		}
	}
	
	/// <summary>
	/// Adds a mouse screen edge trigger.
	/// </summary>
	/// <param name="screen">
	/// The trigger will work in this screen (display monitor). Default: the primary screen.
	/// Should be lazy or default; else the function calls <see cref="print.warning"/>.
	/// Examples: <c>screen.at.left(true)</c>, <c>screen.index(1, true)</c>.
	/// If <c>screen.ofMouse</c>, the trigger will work in any screen.
	/// </param>
	/// <param name="a1_">[](xref:caller_info)</param>
	/// <inheritdoc cref="this[TMClick, string, TMFlags, string, int]"/>
	public Action<MouseTriggerArgs> this[TMEdge edge, [ParamString(PSFormat.TriggerMod)] string modKeys = null, TMFlags flags = 0, screen screen = default, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0, [CallerArgumentExpression("screen")] string a1_ = null] {
		set {
			_Add(value, TMKind.Edge, (byte)edge, modKeys, flags, screen, edge.ToString(), (f_, l_), a1_);
		}
	}
	
	/// <summary>
	/// Adds a mouse move trigger.
	/// </summary>
	/// <inheritdoc cref="this[TMEdge, string, TMFlags, screen, string, int, string]"/>
	public Action<MouseTriggerArgs> this[TMMove move, [ParamString(PSFormat.TriggerMod)] string modKeys = null, TMFlags flags = 0, screen screen = default, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0, [CallerArgumentExpression("screen")] string a1_ = null] {
		set {
			_Add(value, TMKind.Move, (byte)move, modKeys, flags, screen, move.ToString(), (f_, l_), a1_);
		}
	}
	
	MouseTrigger _Add(Action<MouseTriggerArgs> f, TMKind kind, byte data, string modKeys, TMFlags flags, screen screen, string sData, (string, int) source, string a1_ = null) {
		_triggers.ThrowIfRunning_();
		
		if (screen.Handle != default) print.warning("screen should be lazy or default");
		//CONSIDER: store device id or xy, and repair when handle becomes invalid.
		
		bool noMod = modKeys.NE();
		
		string ps;
		using (new StringBuilder_(out var b)) {
			b.Append(kind.ToString()).Append(' ').Append(sData);
			b.Append(" + ").Append(noMod ? "none" : (modKeys == "?" ? "any" : modKeys));
			if (flags != 0) b.Append(" (").Append(flags.ToString()).Append(')');
			if (kind == TMKind.Edge || kind == TMKind.Move) {
				if (screen.IsEmpty) b.Append(", primary screen");
				else if (screen.IsOfMouse_) b.Append(", any screen");
				else b.Append(", ").Append(a1_);
			}
			ps = b.ToString(); //print.it(ps);
		}
		
		KMod mod = 0, modAny = 0;
		if (noMod) {
			if (flags.HasAny(kind == TMKind.Click ? TMFlags.LeftMod | TMFlags.RightMod : TMFlags.LeftMod | TMFlags.RightMod | TMFlags.ButtonModUp)) throw new ArgumentException("Invalid flags.");
		} else {
			if (!keys.more.parseTriggerString(modKeys, out mod, out modAny, out _, true)) throw new ArgumentException("Invalid modKeys string.");
		}
		var t = new MouseTrigger(_triggers, f, kind, data, mod, modAny, flags, screen, ps, source);
		t.DictAdd(_d, _DictKey(kind, data));
		_lastAdded = t;
		UsedHookEvents_ |= HooksThread.UsedEvents.Mouse; //just sets the hook
		UsedHookEvents_ |= kind switch {
			TMKind.Click => HooksThread.UsedEvents.MouseClick,
			TMKind.Wheel => HooksThread.UsedEvents.MouseWheel,
			_ => HooksThread.UsedEvents.MouseEdgeMove,
		};
		return t;
	}
	
	static int _DictKey(TMKind kind, byte data) => (data << 8) | (byte)kind;
	
	/// <summary>
	/// The last added trigger.
	/// </summary>
	public MouseTrigger Last => _lastAdded;
	MouseTrigger _lastAdded;
	
	bool ITriggers.HasTriggers => _lastAdded != null;
	
	internal HooksThread.UsedEvents UsedHookEvents_ { get; private set; }
	
	void ITriggers.StartStop(bool start) {
		if (start) {
		} else {
			_ResetUpAndUnhookTempKeybHook();
			_eatUp = 0;
		}
	}
	
	internal bool HookProcClickWheel(HookData.Mouse k, TriggerHookContext thc) {
		//print.it(k.Event, k.pt);
		Debug.Assert(!k.IsInjectedByAu); //server must ignore
		
		TMKind kind;
		byte data;
		
		if (k.IsButton) {
			if (k.IsButtonUp) {
				if (k.Event == _upEvent) {
					_upEvent = 0;
					if (_upMod == 0 && _upTrigger != null) {
						thc.args = _upArgs;
						thc.trigger = _upTrigger;
						_ResetUp();
					}
				}
				if (k.Event == _eatUp) {
					_eatUp = 0;
					return true;
					//To be safer, could return false if mouse.isPressed(k.Button), but then can interfere with the trigger action.
				}
				return false;
				//CONSIDER: _upTimeout.
			}
			if (k.Event == _eatUp) _eatUp = 0;
			
			kind = TMKind.Click;
			var b = k.Event switch {
				HookData.MouseEvent.LeftButton => TMClick.Left,
				HookData.MouseEvent.RightButton => TMClick.Right,
				HookData.MouseEvent.MiddleButton => TMClick.Middle,
				HookData.MouseEvent.X1Button => TMClick.X1,
				_ => TMClick.X2,
			};
			data = (byte)b;
		} else { //wheel
			kind = TMKind.Wheel;
			var b = k.Event switch {
				HookData.MouseEvent.WheelForward => TMWheel.Forward,
				HookData.MouseEvent.WheelBackward => TMWheel.Backward,
				HookData.MouseEvent.WheelLeft => TMWheel.Left,
				_ => TMWheel.Right,
			};
			data = (byte)b;
		}
		
		return _HookProc2(thc, false, kind, k.Event, k.pt, data, 0);
	}
	
	internal void HookProcEdgeMove(EdgeMoveDetector_.Result d, TriggerHookContext thc) //d not in
	{
		TMKind kind;
		byte data, dataAnyPart;
		
		if (d.edgeEvent != 0) {
			kind = TMKind.Edge;
			data = (byte)d.edgeEvent; dataAnyPart = (byte)d.edgeEventAnyPart;
		} else {
			kind = TMKind.Move;
			data = (byte)d.moveEvent; dataAnyPart = (byte)d.moveEventAnyPart;
		}
		
		_HookProc2(thc, true, kind, HookData.MouseEvent.Move, d.pt, data, dataAnyPart);
	}
	
	bool _HookProc2(TriggerHookContext thc, bool isEdgeMove, TMKind kind, HookData.MouseEvent mEvent, POINT pt, byte data, byte dataAnyPart) {
		var mod = TrigUtil.GetModLR(out var modL, out var modR) | _eatMod;
		MouseTriggerArgs args = null;
		g1:
		if (_d.TryGetValue(_DictKey(kind, data), out var v)) {
			if (!isEdgeMove) thc.UseWndFromPoint(pt);
			for (; v != null; v = v.next) {
				var x = v as MouseTrigger;
				if ((mod & x.modMask) != x.modMasked) continue;
				
				switch (x.Flags & (TMFlags.LeftMod | TMFlags.RightMod)) {
				case TMFlags.LeftMod: if (modL != mod) continue; break;
				case TMFlags.RightMod: if (modR != mod) continue; break;
				}
				
				if (isEdgeMove && !x.Screen.IsOfMouse_) {
					var sh = x.Screen.Now;
					if (!sh.Rect.Contains(pt)) continue;
				}
				
				if (v.DisabledThisOrAll) continue;
				
				if (args == null) thc.args = args = new MouseTriggerArgs(x, thc.Window, mod); //may need for scope callbacks too
				else args.Trigger = x;
				
				if (!x.MatchScopeWindowAndFunc(thc)) continue;
				
				if (x.action == null) {
					_ResetUp();
				} else if (0 != (x.Flags & TMFlags.ButtonModUp) && (mod != 0 || kind == TMKind.Click)) {
					_upTrigger = x;
					_upArgs = args;
					_upEvent = kind == TMKind.Click ? mEvent : 0;
					_upMod = mod;
				} else {
					thc.trigger = x;
					_ResetUp();
				}
				
				_eatMod = mod;
				if (mod != 0) {
					_SetTempKeybHook();
					if (thc.trigger != null) thc.muteMod = TriggerActionThreads.c_modRelease;
					else ThreadPool.QueueUserWorkItem(_ => keys.Internal_.ReleaseModAndDisableModMenu());
				}
				
				//print.it(mEvent, pt, mod);
				if (isEdgeMove || 0 != (x.Flags & TMFlags.ShareEvent)) return false;
				if (kind == TMKind.Click) _eatUp = mEvent;
				return true;
			}
		}
		if (dataAnyPart != 0) {
			data = dataAnyPart;
			dataAnyPart = 0;
			goto g1;
		}
		return false;
	}
	
	//these are used to activate trigger when button and modifiers released
	MouseTrigger _upTrigger;
	MouseTriggerArgs _upArgs;
	HookData.MouseEvent _upEvent;
	KMod _upMod;
	//this is used to eat modifier keys, regardless when the trigger is activated
	KMod _eatMod;
	//these are used to eat modifier keys and to activate trigger when modifiers released
	WindowsHook _keyHook;
	long _keyHookTimeout;
	//this is used to eat button-up event, regardless when the trigger is activated
	HookData.MouseEvent _eatUp;
	
	void _ResetUp() {
		_upTrigger = null;
		_upArgs = null;
		_upEvent = 0;
		_upMod = 0;
	}
	
	void _UnhookTempKeybHook() {
		if (_keyHook != null) {
			//print.it(". unhook");
			_keyHook.Unhook();
			_keyHookTimeout = _keyHook.DontBlockModInOtherHooks_(0);
		}
	}
	
	void _ResetUpAndUnhookTempKeybHook() {
		_ResetUp();
		_eatMod = 0;
		_UnhookTempKeybHook();
	}
	
	void _SetTempKeybHook() {
		//print.it(". hook");
		if (_keyHook == null) {
			_keyHook = WindowsHook.Keyboard(k => {
				if (Environment.TickCount64 >= _keyHookTimeout) {
					_ResetUpAndUnhookTempKeybHook();
					Debug_.Print("hook timeout");
				} else {
					var mod = k.Mod;
					if (0 != (mod & _upMod) && k.IsUp) {
						_upMod &= ~mod;
						if (_upMod == 0 && _upEvent == 0 && _upTrigger != null) {
							_triggers.RunAction_(_upTrigger, _upArgs);
							_ResetUp();
						}
					}
					if (0 != (mod & _eatMod)) {
						//print.it(k);
						if (k.IsUp) _eatMod &= ~mod;
						else k.BlockEvent();
					}
					if (0 == (_upMod | _eatMod)) _UnhookTempKeybHook();
				}
			}, setNow: false);
		}
		if (!_keyHook.IsSet) _keyHook.Hook();
		_keyHookTimeout = _keyHook.DontBlockModInOtherHooks_(5000);
	}
	
	internal static void JitCompile() {
		Jit_.Compile(typeof(MouseTriggers), nameof(HookProcClickWheel), nameof(HookProcEdgeMove), nameof(_HookProc2));
		wnd.fromXY(default, WXYFlags.NeedWindow);
	}
	
	/// <summary>
	/// Detects trigger events of types <b>Edge</b> and <b>Move</b>.
	/// </summary>
	/// <remarks>
	/// Used in the hook server, to avoid sending all mouse move events to clients, which would use 2 or more times more CPU, eg 0.9% instead of 0.45%. Tested: raw input uses slightly less CPU.
	/// </remarks>
	internal class EdgeMoveDetector_ {
		int _x, _y; //mouse position. Relative to the primary screen.
		int _xmin, _ymin, _xmax, _ymax; //min and max possible mouse position in current screen. Relative to the primary screen.
		_State _prev;
		int _sens;
		public Result result;
		
		internal struct Result {
			public POINT pt;
			public TMEdge edgeEvent, edgeEventAnyPart;
			public TMMove moveEvent, moveEventAnyPart;
		}
		
		/// <summary>
		/// State data set by previous events.
		/// </summary>
		struct _State {
			public int xx, yy; //previous coords
			public long time; //previous time
			
			//these used for Move events
			public int mx1, my1, mx2, my2;
			public TMMove mDirection;
			public long mTimeout;
			
			//these used for Edge events
			public long eTimeout;
			
#if DEBUG
			//public int debug;
#endif
		}
		
		public bool Detect(POINT pt) {
			//get normal x y. In pt can be outside screen when cursor moved fast and was stopped by a screen edge. Tested: never for click/wheel events.
			//print.it(pt, mouse.xy);
			//var scrn = screen.of(pt); //problem with empty corners between 2 unaligned screens: when mouse tries to quickly diagonally cut such a corner, may activate a wrong trigger
			var scrn = screen.of(mouse.xy); //smaller problem: mouse.xy gets previous coordinates
			var r = scrn.Rect;
			_xmin = r.left; _ymin = r.top; _xmax = r.right - 1; _ymax = r.bottom - 1;
			_x = Math.Clamp(pt.x, _xmin, _xmax);
			_y = Math.Clamp(pt.y, _ymin, _ymax);
			//print.it(pt, _x, _y, r);
			_sens = scrn.Dpi / 4;
			
			result = default;
			result.pt = (_x, _y);
			
			_Detect();
			_prev.xx = _x; _prev.yy = _y;
			
#if DEBUG
			//print.it(++_prev.debug, edgeEvent, moveEvent, (_x, _y));
#endif
			return result.edgeEvent != 0 || result.moveEvent != 0;
		}
		
		void _Detect() {
			if (mouse.isPressed(MButtons.Left | MButtons.Right | MButtons.Middle)) {
				_prev.mDirection = 0;
				return;
			}
			
			int x = _x, y = _y;
			if (x == _prev.xx && y == _prev.yy) { /*print.it("same x y");*/ return; }
			
			long time = perf.ms;
			int dt = (int)(time - _prev.time);
			_prev.time = time;
			if (dt <= 0) return; //never noticed
			
			//print.it((x, y), mouse.xy, time%10000);
			
			if (y == _ymin || y == _ymax || x == _xmin || x == _xmax) {
				_prev.mDirection = 0;
				if (time < _prev.eTimeout) return; //prevent double trigger when OS sometimes gives strange coords if some hook blocks the event
				if (y == _ymin) { //top
					if (_prev.yy <= _ymin) return;
					if (screen.isInAnyScreen((x, y - 1))) return;
					result.edgeEvent = (TMEdge)((int)(result.edgeEventAnyPart = TMEdge.Top) + _PartX(x));
				} else if (y == _ymax) { //bottom
					if (_prev.yy >= _ymax) return;
					if (screen.isInAnyScreen((x, y + 1))) return;
					result.edgeEvent = (TMEdge)((int)(result.edgeEventAnyPart = TMEdge.Bottom) + _PartX(x));
				} else if (x == _xmin) { //left
					if (_prev.xx <= _xmin) return;
					if (screen.isInAnyScreen((x - 1, y))) return;
					result.edgeEvent = (TMEdge)((int)(result.edgeEventAnyPart = TMEdge.Left) + _PartY(y));
				} else /*if(x == _xmax)*/ { //right
					if (_prev.xx >= _xmax) return;
					if (screen.isInAnyScreen((x + 1, y))) return;
					result.edgeEvent = (TMEdge)((int)(result.edgeEventAnyPart = TMEdge.Right) + _PartY(y));
				}
				_prev.eTimeout = time + 100;
			} else {
				if (_prev.mDirection == 0) {
					if (time < _prev.mTimeout) return;
					
					int dx = (x - _prev.xx) * 16 / dt, dy = (y - _prev.yy) * 21 / dt;
					TMMove e = 0;
					if (dx > 0) {
						if (dy > 0) {
							if (dx > dy) {
								if (dx > _sens) e = TMMove.RightLeft;
							} else {
								if (dy > _sens) e = TMMove.DownUp;
							}
						} else {
							if (dx > -dy) {
								if (dx > _sens) e = TMMove.RightLeft;
							} else {
								if (-dy > _sens) e = TMMove.UpDown;
							}
						}
					} else {
						if (dy > 0) {
							if (-dx > dy) {
								if (-dx > _sens) e = TMMove.LeftRight;
							} else {
								if (dy > _sens) e = TMMove.DownUp;
							}
						} else {
							if (-dx > -dy) {
								if (-dx > _sens) e = TMMove.LeftRight;
							} else {
								if (-dy > _sens) e = TMMove.UpDown;
							}
						}
					}
					if (e != 0) {
						//print.it(e);
						_prev.mDirection = e;
						_prev.mTimeout = time + 250;
						_prev.mx1 = _prev.xx; _prev.my1 = _prev.yy; _prev.mx2 = x; _prev.my2 = y;
					}
				} else {
					if (time > _prev.mTimeout) { _prev.mDirection = 0; return; }
					int part = 0;
					var e = _prev.mDirection;
					switch (e) {
					case TMMove.RightLeft:
						if (x < (_prev.mx1 + _prev.mx2) >> 1) {
							_prev.mDirection = 0;
							int dy = (_prev.my1 + _prev.my2) >> 1, dx = (_prev.mx2 - _prev.mx1) >> 2;
							if (Math.Abs(y - dy) > dx || Math.Abs(_prev.my1 - _prev.my2) > dx << 1) break;
							part = _PartY(y);
						} else if (x > _prev.mx2) { _prev.mx2 = x; _prev.my2 = y; }
						break;
					case TMMove.LeftRight:
						if (x > (_prev.mx1 + _prev.mx2) >> 1) {
							_prev.mDirection = 0;
							int dy = (_prev.my1 + _prev.my2) >> 1, dx = (_prev.mx1 - _prev.mx2) >> 2;
							if (Math.Abs(y - dy) > dx || Math.Abs(_prev.my1 - _prev.my2) > dx << 1) break;
							part = _PartY(y);
						} else if (x < _prev.mx2) { _prev.mx2 = x; _prev.my2 = y; }
						break;
					case TMMove.DownUp:
						if (y < (_prev.my1 + _prev.my2) >> 1) {
							_prev.mDirection = 0;
							int dx = (_prev.mx1 + _prev.mx2) >> 1, dy = (_prev.my2 - _prev.my1) >> 2;
							if (Math.Abs(x - dx) > dy || Math.Abs(_prev.mx1 - _prev.mx2) > dy << 1) break;
							part = _PartX(x);
						} else if (y > _prev.my2) { _prev.mx2 = x; _prev.my2 = y; }
						break;
					case TMMove.UpDown:
						if (y > (_prev.my1 + _prev.my2) >> 1) {
							_prev.mDirection = 0;
							int dx = (_prev.mx1 + _prev.mx2) >> 1, dy = (_prev.my1 - _prev.my2) >> 2;
							if (Math.Abs(x - dx) > dy && Math.Abs(_prev.mx1 - _prev.mx2) > dy << 1) break;
							part = _PartX(x);
						} else if (y < _prev.my2) { _prev.mx2 = x; _prev.my2 = y; }
						break;
					}
					if (part != 0) {
						result.moveEventAnyPart = e;
						result.moveEvent = (TMMove)((int)e + part);
					}
				}
			}
		}
		
		int _PartX(int x) {
			int q = (_xmax - _xmin) / 4;
			if (x < _xmin + q) return 2;
			if (x >= _xmax - q) return 3;
			return 1;
		}
		
		int _PartY(int y) {
			int q = (_ymax - _ymin) / 4;
			if (y < _ymin + q) return 2;
			if (y >= _ymax - q) return 3;
			return 1;
		}
		
		///// <summary>
		///// Sensitivity of <see cref="TMMove"/> triggers.
		///// Default: 5. Can be 0 (less sensitive) to 10 (more sensitive).
		///// </summary>
		//public int MoveSensitivity {
		//	get => _sensPublic;
		//	set {
		//		if((uint)value > 10) throw new ArgumentOutOfRangeException(null, "0-10");
		//		_sensPublic = value;
		//		_sens = (int)(Math.Pow(1.414, 14 - value) * 1.3);
		//		print.it(_sens); //29 when _sensPublic 5 (default)
		//	}
		//}
		//int _sensPublic;
	}
	
	/// <summary>
	/// Used by <c>foreach</c> to enumerate added triggers.
	/// </summary>
	public IEnumerator<MouseTrigger> GetEnumerator() {
		foreach (var kv in _d) {
			for (var v = kv.Value; v != null; v = v.next) {
				var x = v as MouseTrigger;
				yield return x;
			}
		}
	}
	
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Arguments for actions of mouse triggers.
/// </summary>
public class MouseTriggerArgs : TriggerArgs {
	///
	public MouseTrigger Trigger { get; internal set; }
	
	///
	[EditorBrowsable(EditorBrowsableState.Never)]
	public override ActionTrigger TriggerBase => Trigger;
	
	/// <summary>
	/// The active window (<b>Edge</b> and <b>Move</b> triggers) or the mouse window (<b>Click</b> and <b>Wheel</b> triggers).
	/// </summary>
	public wnd Window { get; }
	
	/// <summary>
	/// The pressed modifier keys.
	/// </summary>
	/// <remarks>
	/// Can be useful when the trigger ignores modifiers. For example <i>modKeys</i> is <c>"?"</c> or <c>"Shift?"</c>.
	/// </remarks>
	public KMod Mod { get; }
	
	///
	public MouseTriggerArgs(MouseTrigger trigger, wnd w, KMod mod) {
		Trigger = trigger;
		Window = w; Mod = mod;
	}
	
	///
	public override string ToString() => "Trigger: " + Trigger;
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// What kind of mouse triggers.
/// </summary>
public enum TMKind : byte { Click, Wheel, Edge, Move }

/// <summary>
/// Button for mouse click triggers.
/// </summary>
public enum TMClick : byte { Left = 1, Right, Middle, X1, X2 }

/// <summary>
/// Mouse wheel direction for mouse wheel triggers.
/// </summary>
public enum TMWheel : byte { Forward = 1, Backward, Left, Right }

/// <summary>
/// Screen edge for mouse edge triggers.
/// </summary>
/// <remarks>
/// To activate a screen edge trigger, the user touches a screen edge with the mouse pointer.
/// Each screen edge is divided into 3 parts: 1 - center 50%; 2 - left or top 25%; 3 - right or bottom 25%. Constants like <b>TopInCenter50</b> specify an edge and part; the trigger works only in that part of that edge. Constants like <b>Top</b> specify just an edge; the trigger works in all parts of that edge.
/// </remarks>
public enum TMEdge : byte {
	Top = 1, TopInCenter50, TopInLeft25, TopInRight25,
	Bottom, BottomInCenter50, BottomInLeft25, BottomInRight25,
	Left, LeftInCenter50, LeftInTop25, LeftInBottom25,
	Right, RightInCenter50, RightInTop25, RightInBottom25,
}

/// <summary>
/// Mouse movement directions for mouse move triggers.
/// </summary>
/// <remarks>
/// To activate a mouse move trigger, the user quickly moves the mouse pointer to the specified direction and back.
/// The screen is divided into 3 parts: 1 - center 50%; 2 - left or top 25%; 3 - right or bottom 25%. Constants like <b>UpDownInCenter50</b> specify a direction and screen part; the trigger works only in that screen part. Constants like <b>UpDown</b> specify just a direction; the trigger works in whole screen.
/// </remarks>
public enum TMMove : byte {
	RightLeft = 1, RightLeftInCenter50, RightLeftInTop25, RightLeftInBottom25,
	LeftRight, LeftRightInCenter50, LeftRightInTop25, LeftRightInBottom25,
	UpDown, UpDownInCenter50, UpDownInLeft25, UpDownInRight25,
	DownUp, DownUpInCenter50, DownUpInLeft25, DownUpInRight25,
}

//FUTURE: remove.
///
[Obsolete("use code like screen.at.left(true) or screen.index(1, true) or screen.ofMouse"), EditorBrowsable(EditorBrowsableState.Never)]
public static class TMScreen {
	public static screen Primary => default;
	public static screen Any => screen.ofMouse;
	public static screen OfActiveWindow => screen.ofActiveWindow;
	public static screen NonPrimary1 => screen.index(1, true);
	public static screen NonPrimary2 => screen.index(2, true);
	public static screen NonPrimary3 => screen.index(3, true);
	public static screen NonPrimary4 => screen.index(4, true);
	public static screen NonPrimary5 => screen.index(5, true);
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
