namespace Au.Triggers;

/// <summary>
/// Flags of window triggers.
/// </summary>
[Flags]
public enum TWFlags : byte {
	/// <summary>
	/// Run the action when <see cref="ActionTriggers.Run"/> called, if the window then is active (for <b>ActiveOnce</b> etc triggers) or visible (for <b>VisibleOnce</b> etc triggers).
	/// </summary>
	RunAtStartup = 1,

	/// <summary>
	/// When using the <i>later</i> parameter, call the currently active <b>Triggers.FuncOf</b> functions on "later" events too.
	/// If the function returns <c>false</c>, the action will not run.
	/// The function runs synchronously in the same thread that called <see cref="ActionTriggers.Run"/>. The action runs asynchronously in another thread, which is slower to start.
	/// As always, <b>Triggers.FuncOf</b> functions must not contain slow code; should take less than 10 ms.
	/// </summary>
	LaterCallFunc = 2,
}

/// <summary>
/// Events for window triggers.
/// </summary>
/// <remarks>
/// Cloaked windows are considered invisible. See <see cref="wnd.IsCloaked"/>.
/// </remarks>
public enum TWEvent {
	/// <summary>
	/// When the specified window becomes active (each time).
	/// </summary>
	Active,

	/// <summary>
	/// When the specified window becomes active the first time in the trigger's life.
	/// </summary>
	ActiveOnce,

	/// <summary>
	/// When the specified window is created and then becomes active.
	/// The same as <see cref="ActiveOnce"/>, but windows created before calling <see cref="ActionTriggers.Run"/> are ignored.
	/// </summary>
	ActiveNew,

	/// <summary>
	/// When the specified window becomes visible (each time).
	/// </summary>
	Visible,

	/// <summary>
	/// When the specified window becomes visible the first time in the trigger's life.
	/// </summary>
	VisibleOnce,

	/// <summary>
	/// When the specified window is created and then becomes visible.
	/// The same as <see cref="VisibleOnce"/>, but windows created before calling <see cref="ActionTriggers.Run"/> are ignored.
	/// </summary>
	VisibleNew,
}

/// <summary>
/// Window events for the <i>later</i> parameter of window triggers.
/// </summary>
[Flags]
public enum TWLater : ushort {
	/// <summary>
	/// Name changed.
	/// This event occurs only when the window is active. If name changed while inactive - when activated.
	/// </summary>
	Name = 1,

	/// <summary>
	/// Destroyed (closed).
	/// </summary>
	Destroyed = 2,

	/// <summary>
	/// Activated (became the foreground window).
	/// </summary>
	Active = 4,

	/// <summary>
	/// Deactivated (lose the foreground window status).
	/// This event also occurs when closing the window, if it was active; then the window possibly is already destroyed, and the handle is invalid.
	/// </summary>
	Inactive = 8,

	/// <summary>
	/// Became visible.
	/// The window can be new or was temporarily hidden.
	/// This event occurs when changed the <see cref="wnd.IsVisible"/> property, and not when changed the <see cref="wnd.IsCloaked"/> property, therefore the window is not actually visible if cloaked.
	/// </summary>
	Visible = 16,

	/// <summary>
	/// Became invisible.
	/// This event also occurs when closing the window, if it was visible; then the window possibly is already destroyed, and the handle is invalid.
	/// This event occurs when changed the <see cref="wnd.IsVisible"/> property, and not when changed the <see cref="wnd.IsCloaked"/> property.
	/// </summary>
	Invisible = 32,

	/// <summary>
	/// The window has been cloaked. See <see cref="wnd.IsCloaked"/>.
	/// </summary>
	Cloaked = 64,

	/// <summary>
	/// The window has been uncloaked. See <see cref="wnd.IsCloaked"/>.
	/// </summary>
	Uncloaked = 128,

	/// <summary>
	/// The window has been minimized.
	/// </summary>
	Minimized = 0x100,

	/// <summary>
	/// The window has been restored from the minimized state.
	/// </summary>
	Unminimized = 0x200,

	//rejected. Not useful when we don't have notifications for each location change.
	///// <summary>
	///// The user started to move or resize the window.
	///// This event does not occur when maximizing/restoring and when the window is moved/resized not by the user.
	///// </summary>
	//MoveSizeStart = 0x400,

	///// <summary>
	///// The user finished (or canceled) to move or resize the window.
	///// This event does not occur when maximizing/restoring and when the window is moved/resized not by the user.
	///// </summary>
	//MoveSizeEnd = 0x800,

	//rejected: Location. Use timer or thread-specific EEvent.OBJECT_LOCATIONCHANGE.
	//	Probably it should not be a trigger. If timer, too slow in many cases. If hook, too frequent trigger when drag-moving etc.
	//	If a script wants to track window location, it can easily set WinEventHook(EEvent.OBJECT_LOCATIONCHANGE) instead.
	//rejected: Focus (when eg a child control focused). Use timer or EEvent.OBJECT_FOCUSED.
	//	Rarely used. A script can easily use WinEventHook(EEvent.OBJECT_FOCUSED).
	//rejected: Timer.
}

/// <summary>
/// Represents a window trigger.
/// </summary>
/// <example>
/// <code><![CDATA[
/// Triggers.Window[TWEvent.ActiveNew, "Window name"] = o => print.it(o.Window);
/// var v = Triggers.Window.Last; //v is the new WindowTrigger. Rarely used.
/// ]]></code>
/// </example>
public class WindowTrigger : ActionTrigger {
	internal readonly wndFinder finder;
	internal readonly TWLater later;
	internal readonly TWFlags flags;
	internal readonly TWEvent ev;
	string _typeString, _paramsString;

	internal WindowTrigger(ActionTriggers triggers, Action<WindowTriggerArgs> action, TWEvent ev, wndFinder finder, TWFlags flags, TWLater later, (string, int) source)
		: base(triggers, action, false, source) {
		this.ev = ev;
		this.finder = finder;
		this.flags = flags;
		this.later = later;
	}

	internal override void Run(TriggerArgs args) => RunT(args as WindowTriggerArgs);

	/// <summary>
	/// Returns a trigger type string, like <c>"Window.ActiveNew"</c>.
	/// </summary>
	public override string TypeString => _typeString ??= "Window." + (IsVisible ? "Visible" : "Active") + (IsNew ? "New" : (IsOnce ? "Once" : ""));

	/// <summary>
	/// Returns a string containing trigger parameters.
	/// </summary>
	public override string ParamsString => _paramsString ??= finder.ToString();

	internal bool IsVisible => ev >= TWEvent.Visible;

	internal bool IsOnce => ev == TWEvent.ActiveOnce || ev == TWEvent.VisibleOnce;

	internal bool IsNew => ev == TWEvent.ActiveNew || ev == TWEvent.VisibleNew;

	internal bool IsAlways => ev == TWEvent.Active || ev == TWEvent.Visible;
}

/// <summary>
/// Window triggers.
/// </summary>
/// <example>
/// Note: the <b>Triggers</b> in examples is a field or property like <c>readonly ActionTriggers Triggers = new();</c>.
/// <code><![CDATA[
/// var wt = Triggers.Window; //wt is a WindowTriggers instance
/// wt[TWEvent.ActiveNew, "Window name"] = o => print.it(o.Window);
/// wt[TWEvent.Visible, "Window2 name"] = o => print.it(o.Window);
/// Triggers.Run();
/// ]]></code>
/// More examples: <see cref="ActionTriggers"/>.
/// </example>
public class WindowTriggers : ITriggers, IEnumerable<WindowTrigger> {
	ActionTriggers _triggers;
	bool _win10, _win8;

	internal WindowTriggers(ActionTriggers triggers) {
		_triggers = triggers;
		_win10 = osVersion.minWin10;
		_win8 = osVersion.minWin8;
	}

	/// <summary>
	/// Adds a window trigger and its action.
	/// </summary>
	/// <param name="winEvent">Trigger event.</param>
	/// <param name="flags">Trigger flags.</param>
	/// <param name="later">
	/// Can optionally specify one or more additional events.
	/// This starts to work when the primary trigger is activated, and works only for that window.
	/// For example, to be notified when the window is closed or renamed, specify <c>later: TWLater.Destroyed | TWLater.Name</c>.
	/// When a "later" event occurs, the trigger action is executed. The <see cref="WindowTriggerArgs.Later"/> property then is that event; it is 0 when it is the primary trigger.
	/// The "later" triggers are not disabled when primary triggers are disabled.
	/// </param>
	/// <param name="f_">[](xref:caller_info)</param>
	/// <param name="l_">[](xref:caller_info)</param>
	/// <exception cref="InvalidOperationException">Cannot add triggers after <see cref="ActionTriggers.Run"/> was called, until it returns.</exception>
	/// <exception cref="ArgumentException">See <see cref="wnd.find"/>.</exception>
	/// <seealso cref="Last"/>
	/// <inheritdoc cref="wnd.find(string, string, WOwner, WFlags, Func{wnd, bool}, WContains)" path="/param"/>
	public Action<WindowTriggerArgs> this[TWEvent winEvent,
			[ParamString(PSFormat.Wildex)] string name = null,
			[ParamString(PSFormat.Wildex)] string cn = null,
			[ParamString(PSFormat.Wildex)] WOwner of = default,
			Func<wnd, bool> also = null,
			WContains contains = default,
			TWFlags flags = 0,
			TWLater later = 0,
			[CallerFilePath] string f_ = null,
			[CallerLineNumber] int l_ = 0
			] {
		set {
			var f = new wndFinder(name, cn, of, 0, also, contains);
			this[winEvent, f, flags, later, f_, l_] = value;
		}
	}

	/// <summary>
	/// Adds a window trigger and its action.
	/// </summary>
	/// <exception cref="InvalidOperationException">Cannot add triggers after <see cref="ActionTriggers.Run"/> was called, until it returns.</exception>
	/// <inheritdoc cref="this[TWEvent, string, string, WOwner, Func{wnd, bool}, WContains, TWFlags, TWLater, string, int]" path="/param"/>
	public Action<WindowTriggerArgs> this[TWEvent winEvent, wndFinder f, TWFlags flags = 0, TWLater later = 0, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0] {
		set {
			_triggers.ThrowIfRunning_();
			if (f.Props.contains.Value is uiimageFinder) print.warning("Window triggers with 'contains image' are unreliable.");
			else if (f.Props.contains.Value is ocrFinder) print.warning("Window triggers with 'contains OCR text' are slow.");

			var t = new WindowTrigger(_triggers, value, winEvent, f, flags, later, (f_, l_));
			ref var last = ref _tActive; if (t.IsVisible) last = ref _tVisible;
			if (last == null) {
				last = t;
				last.next = last;
			} else {
				t.next = last.next; //first
				last.next = t;
				last = t;
			}
			_laterEvents |= later;
			_lastAdded = t;
		}
	}

	/// <summary>
	/// The last added trigger.
	/// </summary>
	public WindowTrigger Last => _lastAdded;
	WindowTrigger _lastAdded;

	bool ITriggers.HasTriggers => _lastAdded != null;

	WindowTrigger _tActive, _tVisible; //null or last trigger in linked list
	TWLater _laterEvents, _allEvents;
	bool _usesVisibleArray; //0 != (_allEvents & (TWLater.Visible | TWLater.Invisible))

	unsafe void ITriggers.StartStop(bool start) {
		if (start) {
			if (_enumWinProc == null) {
				_enumWinProc = _EnumWinProc;
				_aTriggered.a = new wnd[100];
				_aTriggeredData = new _TriggeredData[100];
				_aVisible.a = new wnd[100];
				_aVisibleOld.a = new wnd[100];
				_hsOld = new HashSet<wnd>(1000);
				_hsSeenActivating = new HashSet<wnd>(50);
				_winPropCache = new WFCache { CacheName = true, NoTimeout = true, IgnoreVisibility = true };
			} else {
				Array.Clear(_aTriggeredData, 0, _aTriggered.len);
				_aTriggered.len = _aVisible.len = 0;
				_hsOld.Clear();
				_hsSeenActivating.Clear();
				_winPropCache.Clear();
			}

			_allEvents = _laterEvents | TWLater.Active | TWLater.Name;
			if (_tVisible != null) _allEvents |= TWLater.Visible | TWLater.Uncloaked;
			_usesVisibleArray = 0 != (_allEvents & (TWLater.Visible | TWLater.Invisible));

			var ah = new EEvent[5];
			ah[0] = EEvent.SYSTEM_FOREGROUND;
			if (_win8) {
				if (0 != (_allEvents & TWLater.Uncloaked)) ah[1] = EEvent.OBJECT_UNCLOAKED;
				if (0 != (_allEvents & TWLater.Cloaked)) ah[2] = EEvent.OBJECT_CLOAKED;
			}
			if (0 != (_allEvents & TWLater.Minimized)) ah[3] = EEvent.SYSTEM_MINIMIZESTART;
			if (0 != (_allEvents & TWLater.Unminimized)) ah[4] = EEvent.SYSTEM_MINIMIZEEND;
			//if(0 != (_allEvents & TWLater.MoveSizeStart)) ah[5] = EEvent.SYSTEM_MOVESIZESTART;
			//if(0 != (_allEvents & TWLater.MoveSizeEnd)) ah[6] = EEvent.SYSTEM_MOVESIZEEND;
			_hooks = new WinEventHook(ah, _HookProc);
			_hookEventQueue = new Queue<(EEvent, int)>();

			_triggers.WinTimerPeriod_ = 250;

			Api.IVirtualDesktopManager dm = null;
			Api.EnumWindows((w, param) => {
				if (w.IsVisible) {
					if (_usesVisibleArray) _aVisible.Add(w);
					//skip empty winstore hosts that later probably will be used as new windows. Speed: ~100 mcs, first time ~10 ms.
					if (_win10 && w.HasExStyle(WSE.NOREDIRECTIONBITMAP) && w.IsCloaked && w.ClassNameIs("ApplicationFrameWindow")) {
						//is it a window in an inactive virtual desktop? In both cases it does not have a child Windows.UI.Core.CoreWindow.
						dm ??= new Api.VirtualDesktopManager() as Api.IVirtualDesktopManager;
						if (0 == dm.GetWindowDesktopId(w.Get.RootOwnerOrThis(), out var guid) && guid == default) {
							//print.it(w);
							return 1;
						}
					}
				}
				_hsOld.Add(w);
				return 1;
			});
			Api.ReleaseComObject(dm);

			_wActive = default; _nameActive = null;

			//run trigers that have flag TWFlags.RunAtStartup
			for (int i = 0; i < _aVisible.len; i++) _Proc(TWLater.Visible, _aVisible.a[i], _ProcCaller.Startup);
			var wa = wnd.active;
			if (!wa.Is0) _Proc(TWLater.Active, wa, _ProcCaller.Startup);
		} else {
			_hooks?.Dispose();
			_hooks = null;
			_hookEventQueue = null;
		}
	}

	WinEventHook _hooks;
	Queue<(EEvent, int)> _hookEventQueue;
	WFCache _winPropCache;

	struct _TriggeredData {
		public object triggered; //WindowTrigger or List<WindowTrigger>
		public string name; //for Name triggers
	}

	_WndArray _aTriggered, _aVisible, _aVisibleOld;
	_TriggeredData[] _aTriggeredData;
	HashSet<wnd> _hs1, _hs2; //to find added and removed visible windows
	HashSet<wnd> _hsOld, _hsSeenActivating;
	wnd _wActive;
	string _nameActive;
	int _timerCounter10;

	/// <summary>
	/// Called from the message loop every 250 or less ms.
	/// </summary>
	internal unsafe void Timer_() {
		//bool print = !keys.isNumLock;
		//if(print) print.it(perf.ms % 10000);

		int period = _triggers.WinTimerPeriod_;
		if (period < 250) _triggers.WinTimerPeriod_ = Math.Min(period += period / 10 + 1, 250);

		//bool verbose = !keys.isNumLock;
		//if(keys.isNumLock) return;
		//perf.first();
		//var a = wnd.getwnd.allWindows(true);

		//wnd.getwnd.allWindows(ref _listVisible, true);

		//Array.Sort(a);

		//using(var a=wnd.Internal_.EnumWindows2(wnd.Internal_.EnumAPI.EnumWindows, true)) {

		//}

		//if(_aVisibleOld != null) {
		//	//var added = a.Except(_aVisibleOld);
		//	//var removed = _aVisibleOld.Except(a);
		//	//int n1 = added.Count(), n2 = removed.Count();



		//	//if(n1 + n2 > 0) print.it(n1, n2);
		//}
		//_aVisibleOld = a;
		//Debug_.MemoryPrint_();

		//bool needEW = false;
		//if(Visible.HasTriggers) needEW = true;
		//else if(Active.HasTriggers) { if(++_timerCounterActiveEW >= 20) { _timerCounterActiveEW = 0; needEW = true; } }

		if (_usesVisibleArray) {

			//print.it(perf.ms % 10000);

			var t1 = _aVisibleOld; _aVisibleOld = _aVisible; _aVisible = t1;
			_aVisible.len = 0;
			Api.EnumWindows(_enumWinProc);
			//perf.next();
			_VisibleAddedRemoved();

			//perf.nw();
			//speed with  3 main windows: 200, +IsVisible 270, +IsCloaked 450, +Name-IsCloaked 444
			//speed with 20 main windows (79 visible, 541 total): 320, +IsVisible 430 (CPU 0.05). With 480 CPU 0.06, sometimes 0.05.
			//print.it(n);
			//print.it(_listVisible.Count);
		}

		var a = _aTriggered.a;
		for (int i = 0; i < _aTriggered.len; i++) {
			if (!Api.IsWindow(a[i])) {
				_ProcLater(TWLater.Destroyed, a[i], i);
				int last = --_aTriggered.len;
				a[i] = a[last];
				_aTriggeredData[i] = _aTriggeredData[last];
				_aTriggeredData[last] = default;
			}
		}

		if (++_timerCounter10 >= 10) { //every 2.5 s
			_timerCounter10 = 0;
			_hsOld.RemoveWhere(o => !Api.IsWindow(o));
			_hsSeenActivating.RemoveWhere(o => !Api.IsWindow(o));
			_winPropCache.Clear();
		}

		var w = wnd.active;
		if (w.Is0) {
			if (!_wActive.Is0) _ProcLater(TWLater.Inactive, _wActive);
			_wActive = default; _nameActive = null;
		} else if (w != _wActive) {
			_Proc(TWLater.Active, w);
		} else {
			var name = w.NameTL_;
			if (name != null && name != _nameActive) {
				_nameActive = name;
				_Proc(TWLater.Name, w, name: name);
			}
		}
	}

	/// <summary>
	/// Callback of EnumWindows used by Timer_ to get visible windows.
	/// </summary>
	Api.WNDENUMPROC _enumWinProc;
	unsafe int _EnumWinProc(wnd w, void* _) {
		if (w.IsVisible) _aVisible.Add(w);
		return 1;
	}

	/// <summary>
	/// Called by Timer_ when it swaps _aVisible with _aVisibleOld and calls EnumWindows to populate _aVisible with visible windows.
	/// Finds what windows became visible or invisible and runs Visible/Invisible triggers for them.
	/// </summary>
	void _VisibleAddedRemoved() {
		//perf.first();
		wnd[] aNew = _aVisible.a, aOld = _aVisibleOld.a;
		int nNew = _aVisible.len, nOld = _aVisibleOld.len;
		int to = Math.Min(nOld, nNew), diffFrom;
		for (diffFrom = 0; diffFrom < to; diffFrom++) if (aNew[diffFrom] != aOld[diffFrom]) break;
		if (nNew == nOld && diffFrom == nNew) return;
		int diffTo1 = nOld, diffTo2 = nNew;
		while (diffTo1 > 0 && diffTo2 > 0) if (aNew[--diffTo2] != aOld[--diffTo1]) { diffTo1++; diffTo2++; break; }
		//print.it(diffFrom, diffTo1, diffTo2, Math.Max(diffTo1 - diffFrom, diffTo2 - diffFrom));
		int n1 = diffTo1 - diffFrom, n2 = diffTo2 - diffFrom;
		//print.it($"from={diffFrom} to1={diffTo1} to2={diffTo2}    n1={n1} n2={n2}  n1*n2={n1*n2}");
		if (n1 == 0) { //only added
			if (0 != (_allEvents & TWLater.Visible)) {
				for (int i = diffFrom; i < diffTo2; i++) _Added(i);
			}
		} else if (n2 == 0) { //only removed
			if (0 != (_allEvents & TWLater.Invisible)) {
				for (int i = diffFrom; i < diffTo1; i++) _Removed(i);
			}
		} else { //reordered or/and added/removed
				 //print.it($"n1={n1} n2={n2}  n1*n2={n1 * n2}");
				 //perf.next();
				 //SHOULDDO: optimize. Now slow when large array reordered. Eg divide the changed range into two.
				 //n1 = n2 = 0;
			if (0 != (_allEvents & TWLater.Invisible)) {
				if (_hs2 == null) _hs2 = new HashSet<wnd>(500); else _hs2.Clear();
				for (int i = diffFrom; i < diffTo2; i++) _hs2.Add(aNew[i]);
				for (int i = diffFrom; i < diffTo1; i++) if (!_hs2.Remove(aOld[i])) _Removed(i);
			}
			if (0 != (_allEvents & TWLater.Visible)) {
				if (_hs1 == null) _hs1 = new HashSet<wnd>(500); else _hs1.Clear();
				for (int i = diffFrom; i < diffTo1; i++) _hs1.Add(aOld[i]);
				for (int i = diffFrom; i < diffTo2; i++) if (!_hs1.Remove(aNew[i])) _Added(i);
			}
			//perf.nw();
		}
		//if(n1 + n2 > 0) print.it($"<><bc yellow>added {n2}, removed {n1}<>");

		void _Added(int i) {
			//n2++; //print.it("added", aNew[i]);
			_Proc(TWLater.Visible, aNew[i]);
		}

		void _Removed(int i) {
			//n1++; //print.it("removed", aOld[i]);
			_ProcLater(TWLater.Invisible, aOld[i]);
		}
	}

	/// <summary>
	/// WinEventHook hook procedure.
	/// </summary>
	/// <param name="k"></param>
	void _HookProc(HookData.WinEvent k) {
		//if(!keys.isNumLock) print.it(k.event_, k.idObject, k.idChild, k.w);

		if (k.idObject != EObjid.WINDOW) return;
		if (k.idChild != 0 || k.w.Is0) return;

		var event_ = k.event_;
		var w = k.w;

		if (w.IsChild) {
			//if(event_ == EEvent.OBJECT_UNCLOAKED) { } //rejected: wait for winstore app host child window
			return;
		}

		if (_inProc) {
			_hookEventQueue.Enqueue((event_, (int)w));
			//print.it(_hookEventQueue.Count);
			Debug_.PrintIf(_hookEventQueue.Count > 4, "_hookEventQueue.Count=" + _hookEventQueue.Count);
			return;
		}
		_inProc = true;
		try {
			//wait.doEvents(300); //test queue
			//perf.cpu();
			for (; ; ) {
				TWLater e = 0;
				switch (event_) {
				case EEvent.SYSTEM_FOREGROUND:
					//Debug_.PrintIf(!w.IsActive, $"{perf.ms % 10000}, SYSTEM_FOREGROUND but not active: {w}"); //it is normal. The window either will be active soon (and timer will catch it), or will not be activated (eg prevented by Windows, hooks, etc), or another window became active.
					if (w != _wActive && w.IsActive) e = TWLater.Active;
					break;
				case EEvent.OBJECT_UNCLOAKED: e = TWLater.Uncloaked; break;
				case EEvent.OBJECT_CLOAKED: e = TWLater.Cloaked; break;
				case EEvent.SYSTEM_MINIMIZESTART: e = TWLater.Minimized; break;
				case EEvent.SYSTEM_MINIMIZEEND: e = TWLater.Unminimized; break;
					//case EEvent.SYSTEM_MOVESIZESTART: e = TWLater.MoveSizeStart; break;
					//case EEvent.SYSTEM_MOVESIZEEND: e = TWLater.MoveSizeEnd; break;
				}

				if (e != 0) _Proc(e, w, _ProcCaller.Hook);

				//Normal timer period is 255. It is optimized for quite small CPU usage and quite good response time.
				//To improve response time, we temporarily set smaller period on SYSTEM_FOREGROUND. Need it when the window is not active too.
				//If activating a new window first time, set period = 15, to quickly catch Visible and Name events.
				//Else set period = 105, for Name events after closing an "Open File" dialog (then usually its owner becomes active and then changes name).
				if (event_ == EEvent.SYSTEM_FOREGROUND) {
					bool fast = _hsSeenActivating.Add(w) && !_hsOld.Contains(w);
					//print.it("fast timer", fast);
					_triggers.WinTimerPeriod_ = fast ? 1 : 100;
				}

				if (_hookEventQueue.Count == 0) break;
				var q = _hookEventQueue.Dequeue();
				event_ = q.Item1;
				w = (wnd)q.Item2;
			}
			_inProc = false;
		}
		finally {
			if (_inProc) { _inProc = false; _hookEventQueue.Clear(); } //exception. Unlikely, because we handle script exceptions, except thread abort.
		}
	}
	bool _inProc;

	enum _ProcCaller { Timer, Hook, Run, Startup }

	/// <summary>
	/// Processes events for main triggers (active, visible) and most "later" triggers.
	/// Called from hook (_HookProc), timer (Timer_), at startup (StartStop) and SimulateActiveNew/SimulateVisibleNew.
	/// </summary>
	void _Proc(TWLater e, wnd w, _ProcCaller caller = _ProcCaller.Timer, string name = null) {
		//e can be:
		//Used for main triggers (Active, Visible) and other triggers:
		//	Active - from SYSTEM_FOREGROUND hook or from timer. Now the window is active and not 0.
		//	Name - from timer, and only when the window is active.
		//	Visible - from timer.
		//	Uncloaked - from OBJECT_UNCLOAKED hook.
		//Used only for other triggers:
		//	Cloaked - from OBJECT_CLOAKED hook.
		//	Minimized from SYSTEM_MINIMIZESTART hook.
		//	Unminimized from SYSTEM_MINIMIZEEND hook.
		//	rejected: MoveSizeStart, MoveSizeEnd.
		Debug.Assert(e == TWLater.Active || e == TWLater.Name || e == TWLater.Visible || e == TWLater.Uncloaked || e == TWLater.Cloaked || e == TWLater.Minimized || e == TWLater.Unminimized/* || e == TWLater.MoveSizeStart || e == TWLater.MoveSizeEnd*/);

		bool callerHT = caller == _ProcCaller.Hook || caller == _ProcCaller.Timer;

		//Ignore Active if invisible. Triggering while invisible creates more problems than is useful. The timer will call us later.
		//	It makes the trigger slower in many cases. Usually the speed is important when closing unwanted messageboxes etc.
		//	If closed while invisible, the user usually never sees the window. It seems like it would be better, but can create problems:
		//	1. If the trigger uses 'contains', in some windows we cannot find the object because it is still invisible etc; example - TaskDialog windows.
		//	2. If the trigger is not perfectly specified and its action closes wrong windows, the user does not know what is going on.
		//	3. It temporarily deactivates the current window anyway, and often the user is unaware about it.
		if (e == TWLater.Active && callerHT && !w.IsVisibleAndNotCloaked) return;
		//if(e == TWLater.Active && callerHT) { if(!w.IsVisibleAndNotCloaked) { _perf.First(); return; } else if(caller== _ProcCaller.Timer) _perf.NW('A'); }

		int iTriggered = _aTriggered.Find(w);

		bool detectedVisibleNow = e != TWLater.Visible && _usesVisibleArray && callerHT && w.IsVisible && _aVisible.Find(w) < 0;
		if (detectedVisibleNow) {
			_aVisible.Add(w);
			_ProcLater(TWLater.Visible, w, iTriggered);
		}

		bool runActive = false;
		if (e == TWLater.Active) {
			name = w.NameTL_;
			if (callerHT) {
				_ProcLater(TWLater.Name, w, iTriggered, name); //maybe name changed while inactive
				if (!_wActive.Is0) _ProcLater(TWLater.Inactive, _wActive);
			}
			_wActive = w; _nameActive = name;
			if (callerHT) _ProcLater(e, w, iTriggered);
			runActive = _tActive != null;
		} else {
			if (callerHT) _ProcLater(e, w, iTriggered, name);
			switch (e) {
			case TWLater.Name: //from timer, when active window name changed
				runActive = _tActive != null;
				break;
			case TWLater.Visible:
			case TWLater.Uncloaked:
				name = w.NameTL_;
				break;
			default: return;
			}
		}

		bool runVisible = _tVisible != null && w.IsVisibleAndNotCloaked;
		if (!(runVisible | runActive)) return;

		bool oldWindow = callerHT && _hsOld.Contains(w);

		if (_log && callerHT && (_logSkip == null || !_logSkip(w))) {
			if (detectedVisibleNow) _LogEvent(TWLater.Visible, w, caller, oldWindow);
			_LogEvent(e, w, caller, oldWindow);
		}

		object triggered = iTriggered >= 0 ? _aTriggeredData[iTriggered].triggered : null, triggeredOld = triggered;

		_winPropCache.Begin(w); //info: don't need to call Clear now. Timer calls it every 2.5 s.
		_winPropCache.Name = name;

		//perf.first();
		WindowTriggerArgs args = null;

		//print.it(runVisible,runActive,w.IsActive, oldWindow);

		if (runVisible) _Do(true, runActive && !detectedVisibleNow);
		if (runActive && w.IsActive) _Do(false, e != TWLater.Active);

		void _Do(bool visible, bool secondaryEvent) {
			ActionTrigger last = visible ? _tVisible : _tActive, v = last;
			if (last == null) return;

			do {
				v = v.next;
				if (v.DisabledThisOrAll) continue;
				var t = v as WindowTrigger;

				if (caller == _ProcCaller.Startup && 0 == (t.flags & TWFlags.RunAtStartup)) continue;
				if (oldWindow && (t.IsNew || secondaryEvent)) continue;
				if (triggered != null && (!t.IsAlways || secondaryEvent)) {
					switch (triggered) {
					case WindowTrigger wt1: if (wt1 == t) continue; break;
					case List<WindowTrigger> awt1: if (awt1.Contains(t)) continue; break;
					}
				}

				try {
					if (!t.finder.IsMatch(w, _winPropCache)) continue;

					//rejected: if 'contains' is image and it does not match, wait several seconds until it matches.
					//	Now such triggers sometimes don't work because need more time to show the image.
					//	Rare and too difficult. Cannot wait now. Would need to use timer or threadpool.
				}
				catch (Exception ex) {
					print.it(ex);
					continue;
				}

				if (args == null) args = new WindowTriggerArgs(t, w, 0); else args.Trigger = t;

				if (!t.CallFunc(args)) continue; //info: handles exceptions

				switch (triggered) {
				case null: triggered = t; break;
				case WindowTrigger wt1: if (wt1 != t) triggered = new List<WindowTrigger> { wt1, t }; break;
				case List<WindowTrigger> awt1: if (!awt1.Contains(t)) awt1.Add(t); break;
				}

				//if(!visible && !w.IsActive) break; //no

				if (_log) print.it($"<><c red>{t.TypeString}<>");

				_triggers.RunAction_(t, args);
			} while (v != last);
			//perf.nw(); //speed ms/triggers when cold CPU: ~1/1000, 10/10000, 50/100000, 130/1000000
		}

		if (triggered != triggeredOld) {
			if (iTriggered < 0) {
				iTriggered = _aTriggered.len;
				_aTriggered.Add(w);
				if (iTriggered == _aTriggeredData.Length) Array.Resize(ref _aTriggeredData, iTriggered * 2);
				_aTriggeredData[iTriggered].name = name; //later managed by _Proc2
			}
			_aTriggeredData[iTriggered].triggered = triggered;
		}
	}

	/// <summary>
	/// Called to process "later" events from _Proc and timer.
	/// iTriggered is w index in _aTriggered, or -1 if not found, or -2 (default) to let this func find.
	/// </summary>
	void _ProcLater(TWLater e, wnd w, int iTriggered = -2, string name = null) {
		if (0 == (_laterEvents & e)) return;
		if (iTriggered < -1) iTriggered = _aTriggered.Find(w);
		if (iTriggered < 0) return;

		if (e == TWLater.Name) { //called in 2 cases: 1. From timer, when name changed. 2. From _Proc on Active event (hook or timer).
			if (name == null || _aTriggeredData[iTriggered].name == name) return;
			_aTriggeredData[iTriggered].name = name;
		}

		if (_log) print.it($"\t{perf.ms % 10000,4}, {e,-11}, {w}");

		WindowTriggerArgs args = null;
		var triggered = _aTriggeredData[iTriggered].triggered;
		var a = triggered as List<WindowTrigger>;
		int n = a?.Count ?? 1;
		for (int i = 0; i < n; i++) {
			var t = a?[i] ?? (triggered as WindowTrigger);
			if (0 == (t.later & e)) continue;
			//if(t.DisabledThisOrAll) continue; //no
			if (args == null) args = new WindowTriggerArgs(t, w, e); else args.Trigger = t;
			if (0 != (t.flags & TWFlags.LaterCallFunc)) {
				if (!t.CallFunc(args)) continue;
			}

			if (_log) print.it($"<><c red>\t{e}<>");

			_triggers.RunAction_(t, args);
		}
	}

	/// <summary>
	/// Simulates event "activated new window" as if the the specified window is that window.
	/// </summary>
	/// <exception cref="InvalidOperationException">Cannot be before or after <see cref="ActionTriggers.Run"/>.</exception>
	/// <remarks>
	/// This function usually is used to run <b>ActiveNew</b> triggers for a window created before calling <see cref="ActionTriggers.Run"/>. Here "run triggers" means "compare window properties etc with those specified in triggers and run actions of triggers that match". Normally such triggers don't run because the window is considered old. This function runs triggers as it was a new window. Triggers like <b>ActiveNew</b> and <b>ActiveOnce</b> will run once, as usually.
	/// This function must be called while the main triggers thread is in <see cref="ActionTriggers.Run"/>, for example from another trigger action. It is asynchronous (does not wait).
	/// If you call this function from another trigger action (hotkey etc), make sure the window trigger action runs in another thread or can be queued. Else both actions cannot run simultaneously. See example.
	/// </remarks>
	/// <example>
	/// Note: the <b>Triggers</b> in examples is a field or property like <c>readonly ActionTriggers Triggers = new();</c>.
	/// <code><![CDATA[
	/// Triggers.Options.ThreadNew(true);
	/// Triggers.Window[TWEvent.ActiveNew, "* Notepad"] = o => o.Window.Resize(500, 200);
	/// Triggers.Hotkey["Ctrl+T"] = o => Triggers.Window.SimulateActiveNew(wnd.active);
	/// Triggers.Hotkey["Ctrl+Alt+T"] = o => Triggers.Stop();
	/// Triggers.Run();
	/// ]]></code>
	/// </example>
	public void SimulateActiveNew(wnd w) => _SimulateNew(TWLater.Active, w);

	/// <summary>
	/// Simulates event "visible new window" as if the specified window is that window.
	/// Similar to <see cref="SimulateActiveNew"/>.
	/// </summary>
	/// <param name="w"></param>
	/// <exception cref="InvalidOperationException">Cannot be before or after <see cref="ActionTriggers.Run"/>.</exception>
	public void SimulateVisibleNew(wnd w) => _SimulateNew(TWLater.Visible, w);

	void _SimulateNew(TWLater e, wnd w) {
		_triggers.ThrowIfNotRunning_();
		_triggers.Notify_(Api.WM_USER + 20, (int)e, w.Handle); //calls SimulateNew_ in correct thread
	}

	//Called in correct thread.
	internal void SimulateNew_(nint wParam, nint lParam) {
		var w = (wnd)lParam;
		if (w.IsAlive) _Proc((TWLater)wParam, w, _ProcCaller.Run);
	}

	bool _log;
	Func<wnd, bool> _logSkip;

	/// <summary>
	/// Starts or stops to log (write in output) window events that can help to create or debug window triggers.
	/// </summary>
	/// <param name="on">Start (<c>true</c>) or stop.</param>
	/// <param name="skip">An optional callback function that can be used to reduce noise, eg skip tooltip windows. Return <c>true</c> to skip that window.</param>
	/// <remarks>
	/// For primary trigger events is logged this info:
	/// <ol>
	/// <li>Time milliseconds. Shows only the remainder of dividing by 10 seconds, therefore it starts from 0 again when reached 9999 (9 seconds and 999 milliseconds).</li>
	/// <li>Event (see <see cref="TWLater"/>).</li>
	/// <li>Letters for window state etc:
	/// <ul>
	/// <li>A - the window is active.</li>
	/// <li>H - the window is invisible (!<see cref="wnd.IsVisible"/>).</li>
	/// <li>C - the window is cloaked (<see cref="wnd.IsCloaked"/>).</li>
	/// <li>O - the window is considered old, ie created before calling <see cref="ActionTriggers.Run"/>.</li>
	/// <li>T - the even has been detected using a timer, which means slower response time. Else detected using a hook.</li>
	/// </ul>
	/// </li>
	/// <li>Window (handle, class, name, program, rectangle).</li>
	/// </ol>
	/// 
	/// Colors are used for window event types used for primary triggers: blue if activated; green if became visible; yellow if name changed.
	/// For "later" events is logged time, event and window. Black, tab-indented. Only events that are specified in triggers.
	/// When a trigger is activated, the event type is red.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// Triggers.Window.LogEvents(true, o => 0 != o.ClassNameIs("*tooltip*", "SysShadow", "#32774", "TaskList*"));
	/// ]]></code>
	/// </example>
	public void LogEvents(bool on, Func<wnd, bool> skip = null) {
		_log = on;
		_logSkip = skip;
	}

	/// <summary>
	/// Called by _Proc.
	/// </summary>
	void _LogEvent(TWLater e, wnd w, _ProcCaller caller, bool oldWindow) {
		string col = "0";
		switch (e) {
		case TWLater.Active: col = "0x0000ff"; break;
		case TWLater.Visible: case TWLater.Uncloaked: col = "0x00c000"; break;
		case TWLater.Name: col = "0xC0C000"; break;
		}
		var A = w.IsActive ? "A" : " ";
		var H = w.IsVisible ? " " : "H";
		var C = w.IsCloaked ? "C" : " ";
		var O = oldWindow ? "O" : " ";
		var T = caller == _ProcCaller.Timer ? "T" : " ";
		print.it($"<><c {col}>{perf.ms % 10000,4}, {e,-11}, {A}{H}{C}{O}{T}, {w}</c>");
	}

	/// <summary>
	/// For _aTriggered, _aVisible and _aVisibleOld we use resizable array, not List.
	/// We access elements by index in time-critical code. With List it is much slower.
	/// </summary>
	struct _WndArray {
		public wnd[] a;
		public int len;

		public void Add(wnd w) {
			if (len == a.Length) Array.Resize(ref a, len * 2);
			a[len++] = w;
		}

		public unsafe int Find(wnd w) {
			fixed (wnd* p = a) {
				for (int i = 0, n = len; i < n; i++) if (p[i] == w) return i;
			}
			return -1;
		}
	}

	/// <summary>
	/// Used by foreach to enumerate added triggers.
	/// </summary>
	public IEnumerator<WindowTrigger> GetEnumerator() {
		ActionTrigger last, v;
		last = v = _tActive;
		do {
			v = v.next;
			var x = v as WindowTrigger;
			yield return x;
		} while (v != last);
		last = v = _tVisible;
		do {
			v = v.next;
			var x = v as WindowTrigger;
			yield return x;
		} while (v != last);
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Arguments for actions of window triggers.
/// </summary>
public class WindowTriggerArgs : TriggerArgs {
	/// <summary>
	/// The trigger.
	/// </summary>
	public WindowTrigger Trigger { get; internal set; }

	///
	[EditorBrowsable(EditorBrowsableState.Never)]
	public override ActionTrigger TriggerBase => Trigger;

	/// <summary>
	/// The window.
	/// </summary>
	public wnd Window { get; }

	/// <summary>
	/// The "later" event, or 0 if it is the primary trigger (<b>ActiveNew</b> etc). See example.
	/// </summary>
	/// <example>
	/// Note: the <b>Triggers</b> in examples is a field or property like <c>readonly ActionTriggers Triggers = new();</c>.
	/// <code><![CDATA[
	/// Triggers.Window[TWEvent.ActiveOnce, "*- Notepad", later: TWLater.Active | TWLater.Inactive] = o => print.it(o.Later, o.Window);
	/// Triggers.Run();
	/// ]]></code>
	/// </example>
	public TWLater Later { get; }

	///
	public WindowTriggerArgs(WindowTrigger trigger, wnd w, TWLater later) {
		Trigger = trigger;
		Window = w;
		Later = later;
	}

	///
	public override string ToString() => "Trigger: " + Trigger;

	/// <summary>
	/// Shows or hides a window-attached toolbar depending on window name.
	/// Use in window trigger action, like in examples.
	/// </summary>
	/// <param name="tbFunc">Toolbar function. Let it create a window-attached toolbar and returns the <see cref="toolbar"/> object.</param>
	/// <param name="windowName">Window name when the toolbar should be visible. String format: [wildcard expression](xref:wildcard_expression).</param>
	/// <exception cref="ArgumentException">Invalid wildcard expression (<c>"**options "</c> or regular expression).</exception>
	/// <remarks>
	/// The trigger must have argument <c>, later: TWLater.Name</c> (see <see cref="TWLater"/>) and match the window with any name.
	/// </remarks>
	/// <example>
	/// Show toolbar Toolbar_Chrome1 on a Chrome window when window name starts with <c>"NuGet"</c>.
	/// <code><![CDATA[
	/// Triggers.Window[TWEvent.ActiveOnce, "*Google Chrome", "Chrome_WidgetWin_1", later: TWLater.Name] =
	/// 	ta => ta.ShowToolbarWhenWindowName(Toolbar_Chrome1, "NuGet*");
	/// ]]></code>
	/// Toolbar Toolbar_Chrome1 for the above example.
	/// <code><![CDATA[
	/// toolbar Toolbar_Chrome1(WindowTriggerArgs ta) { var t = new toolbar(); /* add buttons */ t.Show(ta); return t; }
	/// ]]></code>
	/// Show Toolbar_A when window name starts with <c>"A -"</c>, Toolbar_B when <c>"B -"</c>, and Toolbar_C always.
	/// <code><![CDATA[
	/// Triggers.Window[TWEvent.ActiveOnce, "*Google Chrome", "Chrome_WidgetWin_1", later: TWLater.Name] = ta => {
	/// 	ta.ShowToolbarWhenWindowName(Toolbar_A, "A -*");
	/// 	ta.ShowToolbarWhenWindowName(Toolbar_B, "B -*");
	/// 	if (ta.Later == 0) Toolbar_C(ta);
	/// };
	/// ]]></code>
	/// </example>
	public void ShowToolbarWhenWindowName(Func<WindowTriggerArgs, toolbar> tbFunc, [ParamString(PSFormat.Wildex)] string windowName) {
		var wild = new wildex(windowName);
		ShowToolbarWhenWindowName(tbFunc, o => wild.Match(o.Name));
	}
	//rejected: shorter syntax. Limited, eg can't use the same trigger for multiple toolbars. Examples:
	//	Triggers.Window[TWEvent.ActiveOnce, "*Google Chrome", "Chrome_WidgetWin_1", whenName: "NuGet*"] = Toolbar_Chrome1;
	//	Triggers.Window.Toolbar(Toolbar_Chrome1, "*Google Chrome", "Chrome_WidgetWin_1", whenName: "NuGet*");

	/// <summary>
	/// Shows or hides a window-attached toolbar depending on window name.
	/// Use in window trigger action, like in examples.
	/// </summary>
	/// <param name="tbFunc">Toolbar function. Let it create a window-attached toolbar and returns the <see cref="toolbar"/> object.</param>
	/// <param name="windowName">Callback function that returns <c>true</c> if the toolbar should be visible.</param>
	/// <example>
	/// Show toolbar Toolbar_Chrome2 on a Chrome window when web page URL starts with <c>"https://www.youtube.com/"</c>.
	/// <code><![CDATA[
	/// Triggers.Window[TWEvent.ActiveOnce, "*Google Chrome", "Chrome_WidgetWin_1", later: TWLater.Name] = ta => ta.ShowToolbarWhenWindowName(Toolbar_Chrome2, w => {
	/// 	var e = w.Elm["web:DOCUMENT"].Find(-1);
	/// 	return e != null && e.Value.Starts("https://www.youtube.com/");
	/// });
	/// ]]></code>
	/// </example>
	public void ShowToolbarWhenWindowName(Func<WindowTriggerArgs, toolbar> tbFunc, Func<wnd, bool> windowName) {
		var mi = tbFunc.Method;
		(t_mt ??= new()).TryGetValue(mi, out var v);
		if (!windowName(Window)) { v?.Close(); return; }
		if (v != null) return;
		v = tbFunc(this);
		if (v != null) {
			t_mt[mi] = v;
			v.Closed += () => { t_mt.Remove(mi); };
		} else {
			t_mt.Remove(mi);
		}
	}

	[ThreadStatic] static Dictionary<MethodInfo, toolbar> t_mt;
}
