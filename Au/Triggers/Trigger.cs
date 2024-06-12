namespace Au.Triggers;

/// <summary>
/// Base of classes of all action trigger types.
/// </summary>
public abstract class ActionTrigger {
	internal ActionTrigger next; //linked list when eg same hotkey is used in multiple scopes
	internal readonly Delegate action;
	internal readonly ActionTriggers triggers;
	internal readonly TOptions options; //Triggers.Options
	internal readonly TriggerScope scope; //Triggers.Of.WindowX. Used by hotkey, autotext and mouse triggers.
	readonly TriggerFunc[] _funcAfter, _funcBefore; //Triggers.FuncOf. _funcAfter used by all triggers; _funcBefore - like scope.
	internal readonly string sourceFile;
	internal readonly int sourceLine;
	
	internal ActionTrigger(ActionTriggers triggers, Delegate action, bool usesWindowScope, (string, int) source) {
		this.sourceFile = source.Item1 ?? throw new ArgumentNullException();
		this.sourceLine = source.Item2;
		this.action = action;
		this.triggers = triggers;
		var to = triggers.options_;
		options = to.Current;
		EnabledAlways = to.EnabledAlways;
		if (usesWindowScope) scope = triggers.scopes_.Current;
		var tf = triggers.funcs_;
		_funcBefore = _Func(tf.commonBefore, tf.nextBefore); tf.nextBefore = null;
		_funcAfter = _Func(tf.nextAfter, tf.commonAfter); tf.nextAfter = null;
		
		TriggerFunc[] _Func(TFunc f1, TFunc f2) {
			var f3 = f1 + f2; if (f3 == null) return null;
			var a1 = f3.GetInvocationList();
			var r1 = new TriggerFunc[a1.Length];
			for (int i = 0; i < a1.Length; i++) {
				var f4 = a1[i] as TFunc;
				if (!tf.perfDict.TryGetValue(f4, out var fs)) tf.perfDict[f4] = fs = new TriggerFunc { f = f4 };
				r1[i] = fs;
			}
			return r1;
		}
	}
	
	internal void DictAdd<TKey>(Dictionary<TKey, ActionTrigger> d, TKey key) {
		if (!d.TryGetValue(key, out var o)) d.Add(key, this);
		else { //append to the linked list
			while (o.next != null) o = o.next;
			o.next = this;
		}
	}
	
	/// <summary>
	/// Called through <see cref="TriggerActionThreads.Run"/> in action thread.
	/// Possibly runs later.
	/// </summary>
	internal abstract void Run(TriggerArgs args);
	
	/// <summary>
	/// Makes simpler to implement <see cref="Run"/>.
	/// </summary>
	private protected void RunT<T>(T args) => (action as Action<T>)(args);
	
	/// <summary>
	/// Returns a trigger type string, like <c>"Hotkey"</c>, <c>"Mouse"</c>, <c>"Window.ActiveNew"</c>.
	/// </summary>
	public abstract string TypeString { get; }
	
	/// <summary>
	/// Returns a string containing trigger parameters.
	/// </summary>
	public abstract string ParamsString { get; }
	
	/// <summary>
	/// Returns <c>TypeString + " " + ParamsString</c>.
	/// </summary>
	public override string ToString() => TypeString + " " + ParamsString;
	
	internal bool MatchScopeWindowAndFunc(TriggerHookContext thc) {
		try {
			for (int i = 0; i < 3; i++) {
				if (i == 1) {
					if (scope != null) {
						thc.PerfStart();
						bool ok = scope.Match(thc);
						thc.PerfEnd(false, ref scope.perfTime);
						if (!ok) return false;
					}
				} else {
					var af = i == 0 ? _funcBefore : _funcAfter;
					if (af != null) {
						foreach (var v in af) {
							thc.PerfStart();
							bool ok = v.f(thc.args);
							thc.PerfEnd(true, ref v.perfTime);
							if (!ok) return false;
						}
					}
				}
			}
		}
		catch (Exception ex) {
			print.it(ex);
			return false;
		}
		return true;
		
		//never mind: when same scope used several times (probably with different functions),
		//	should compare it once, and don't call 'before' functions again if did not match. Rare.
	}
	
	internal bool CallFunc(TriggerArgs args) {
#if true
		if (_funcAfter != null) {
			try {
				foreach (var v in _funcAfter) {
					var t1 = perf.ms;
					bool ok = v.f(args);
					var td = perf.ms - t1;
					if (td > 200) print.warning($"Too slow Triggers.FuncOf function of a window trigger. Should be < 10 ms, now {td} ms. Task name: {script.name}.", -1);
					if (!ok) return false;
				}
			}
			catch (Exception ex) {
				print.it(ex);
				return false;
			}
		}
#else
		for(int i = 0; i < 2; i++) {
			var af = i == 0 ? _funcBefore : _funcAfter;
			if(af != null) {
				foreach(var v in af) {
					bool ok = v.f(args);
					if(!ok) return false;
				}
			}
		}
#endif
		return true;
		//TODO3: measure time more intelligently, like in MatchScope, but maybe give more time.
	}
	
	internal bool HasFunc => _funcBefore != null || _funcAfter != null;
	
	//probably not useful. Or also need a property for eg HotkeyTriggers in derived classes.
	///// <summary>
	///// The <see cref="ActionTriggers"/> instance to which this trigger belongs.
	///// </summary>
	//public ActionTriggers Triggers => triggers;
	
	/// <summary>
	/// Gets or sets whether this trigger is disabled.
	/// Does not depend on <see cref="ActionTriggers.Disabled"/>, <see cref="ActionTriggers.DisabledEverywhere"/>, <see cref="EnabledAlways"/>.
	/// </summary>
	public bool Disabled { get; set; }
	
	/// <summary>
	/// Returns <c>true</c> if <see cref="Disabled"/>; also if <see cref="ActionTriggers.Disabled"/> or <see cref="ActionTriggers.DisabledEverywhere"/>, unless <see cref="EnabledAlways"/>.
	/// </summary>
	public bool DisabledThisOrAll => Disabled || (!EnabledAlways && (triggers.Disabled | ActionTriggers.DisabledEverywhere));
	
	/// <summary>
	/// Gets or sets whether this trigger ignores <see cref="ActionTriggers.Disabled"/> and <see cref="ActionTriggers.DisabledEverywhere"/>.
	/// </summary>
	/// <remarks>
	/// When adding the trigger, this property is set to the value of <see cref="TriggerOptions.EnabledAlways"/> at that time.
	/// </remarks>
	public bool EnabledAlways { get; set; }
	
	/// <summary>
	/// Starts the action like when its trigger is activated.
	/// </summary>
	/// <param name="args"></param>
	/// <exception cref="InvalidOperationException">Called in a wrong place or from a wrong thread. More info in Remarks.</exception>
	/// <remarks>
	/// Call while <see cref="ActionTriggers.Run"/> is running, from the same thread.
	/// </remarks>
	public void RunAction(TriggerArgs args) {
		triggers.ThrowIfNotRunning_();
		triggers.ThrowIfNotMainThread_();
		triggers.RunAction_(this, args);
	}
}

/// <summary>
/// Base of trigger action argument classes of all trigger types.
/// </summary>
public abstract class TriggerArgs {
	/// <summary>
	/// Gets the trigger as <see cref="ActionTrigger"/> (the base class of all trigger type classes).
	/// </summary>
	public abstract ActionTrigger TriggerBase { get; }
	
	/// <summary>
	/// Disables the trigger. Enables later when the toolbar is closed.
	/// Use to implement single-instance toolbars.
	/// </summary>
	public void DisableTriggerUntilClosed(toolbar t) {
		TriggerBase.Disabled = true;
		t.Closed += () => TriggerBase.Disabled = false;
	}
}

/// <summary>
/// Allows to specify working windows for multiple triggers of these types: hotkey, autotext, mouse.
/// </summary>
/// <example>
/// Note: the <b>Triggers</b> in examples is a field or property like <c>readonly ActionTriggers Triggers = new();</c>.
/// <code><![CDATA[
/// Triggers.Hotkey["Ctrl+K"] = o => print.it("this trigger works with all windows");
/// Triggers.Of.Window("* Notepad"); //specifies a working window for triggers added afterwards
/// Triggers.Hotkey["Ctrl+F11"] = o => print.it("this trigger works only when a Notepad window is active");
/// Triggers.Hotkey["Ctrl+F12"] = o => print.it("this trigger works only when a Notepad window is active");
/// var wordpad = Triggers.Of.Window("* WordPad"); //specifies another working window for triggers added afterwards
/// Triggers.Hotkey["Ctrl+F11"] = o => print.it("this trigger works only when a WordPad window is active");
/// Triggers.Hotkey["Ctrl+F12"] = o => print.it("this trigger works only when a WordPad window is active");
/// Triggers.Of.AllWindows(); //let triggers added afterwards work with all windows
/// Triggers.Mouse[TMEdge.RightInTop25] = o => print.it("this trigger works with all windows");
/// Triggers.Of.Again(wordpad); //sets a previously specified working window for triggers added afterwards
/// Triggers.Mouse[TMEdge.RightInBottom25] = o => print.it("this trigger works only when a WordPad window is active");
/// Triggers.Mouse[TMMove.DownUp] = o => print.it("this trigger works only when a WordPad window is active");
/// Triggers.Mouse[TMClick.Middle] = o => print.it("this trigger works only when the mouse is in a WordPad window");
/// Triggers.Mouse[TMWheel.Forward] = o => print.it("this trigger works only when the mouse is in a WordPad window");
/// Triggers.Run();
/// ]]></code>
/// </example>
public class TriggerScopes {
	internal TriggerScopes() { }
	
	internal TriggerScope Current { get; private set; }
	
	/// <summary>
	/// Sets scope "all windows" again. Hotkey, autotext and mouse triggers added afterwards will work with all windows.
	/// </summary>
	/// <remarks>
	/// Example in class help.
	/// </remarks>
	public void AllWindows() => Current = null;
	
	/// <summary>
	/// Sets (reuses) a previously specified scope.
	/// </summary>
	/// <remarks>
	/// Example in class help.
	/// </remarks>
	/// <param name="scope">The return value of function <b>Window</b>, <b>NotWindow</b>, <b>Windows</b> or <b>NotWindows</b>.</param>
	public void Again(TriggerScope scope) => Current = scope;
	
	/// <summary>
	/// Sets scope "only this window". Hotkey, autotext and mouse triggers added afterwards will work only when the specified window is active.
	/// </summary>
	/// <returns>Returns an object that can be later passed to <see cref="Again"/> to reuse this scope.</returns>
	/// <example><see cref="TriggerScopes"/></example>
	/// <inheritdoc cref="wnd.find" path="//param|//exception"/>
	public TriggerScope Window(
		[ParamString(PSFormat.Wildex)] string name = null,
		[ParamString(PSFormat.Wildex)] string cn = null,
		[ParamString(PSFormat.Wildex)] WOwner of = default,
		Func<wnd, bool> also = null, WContains contains = default)
		=> _Window(false, name, cn, of, also, contains);
	
	/// <summary>
	/// Sets scope "not this window". Hotkey, autotext and mouse triggers added afterwards will not work when the specified window is active.
	/// </summary>
	/// <inheritdoc cref="Window(string, string, WOwner, Func{wnd, bool}, WContains)"/>
	public TriggerScope NotWindow(
		[ParamString(PSFormat.Wildex)] string name = null,
		[ParamString(PSFormat.Wildex)] string cn = null,
		[ParamString(PSFormat.Wildex)] WOwner of = default,
		Func<wnd, bool> also = null, WContains contains = default)
		=> _Window(true, name, cn, of, also, contains);
	
	TriggerScope _Window(bool not, string name, string cn, WOwner of, Func<wnd, bool> also, WContains contains)
		=> _Add(not, new wndFinder(name, cn, of, 0, also, contains));
	
	/// <summary>
	/// Sets scope "only this window". Hotkey, autotext and mouse triggers added afterwards will work only when the specified window is active.
	/// </summary>
	/// <returns>Returns an object that can be later passed to <see cref="Again"/> to reuse this scope.</returns>
	public TriggerScope Window(wndFinder f)
		=> _Add(false, f);
	
	/// <summary>
	/// Sets scope "not this window". Hotkey, autotext and mouse triggers added afterwards will not work when the specified window is active.
	/// </summary>
	/// <returns>Returns an object that can be later passed to <see cref="Again"/> to reuse this scope.</returns>
	public TriggerScope NotWindow(wndFinder f)
		=> _Add(true, f);
	
	//rejected. May be used incorrectly. Rare. When really need, can use the 'also' parameter.
	///// <summary>
	///// Sets scope "only this window". Hotkey, autotext and mouse triggers added afterwards will work only when the specified window is active.
	///// </summary>
	///// <returns>Returns an object that can be later passed to <see cref="Again"/> to reuse this scope.</returns>
	///// <exception cref="AuWndException">Invalid window handle.</exception>
	//public TriggerScope Window(wnd w)
	//	=> _Add(false, w);
	
	///// <summary>
	///// Sets scope "not this window". Hotkey, autotext and mouse triggers added afterwards will not work when the specified window is active.
	///// </summary>
	///// <returns>Returns an object that can be later passed to <see cref="Again"/> to reuse this scope.</returns>
	///// <exception cref="AuWndException">Invalid window handle.</exception>
	//public TriggerScope NotWindow(wnd w)
	//	=> _Add(true, w);
	
	/// <summary>
	/// Sets scope "only these windows". Hotkey, autotext and mouse triggers added afterwards will work only when one of the specified windows is active.
	/// </summary>
	/// <returns>Returns an object that can be later passed to <see cref="Again"/> to reuse this scope.</returns>
	/// <param name="any">Specifies windows, like <c>new("Window1"), new("Window2")</c>.</param>
	public TriggerScope Windows(params wndFinder[] any)
		=> _Add(false, any);
	
	/// <summary>
	/// Sets scope "not these windows". Hotkey, autotext and mouse triggers added afterwards will not work when one of the specified windows is active.
	/// </summary>
	/// <returns>Returns an object that can be later passed to <see cref="Again"/> to reuse this scope.</returns>
	/// <param name="any">Specifies windows, like <c>new("Window1"), new("Window2")</c>.</param>
	public TriggerScope NotWindows(params wndFinder[] any)
		=> _Add(true, any);
	
	TriggerScope _Add(bool not, wndFinder f) {
		Not_.Null(f);
		Used = true;
		return Current = new TriggerScope(f, not);
	}
	
	TriggerScope _Add(bool not, wndFinder[] a) {
		if (a.Length == 1) return _Add(not, a[0]);
		foreach (var v in a) if (v == null) throw new ArgumentNullException();
		Used = true;
		return Current = new TriggerScope(a, not);
	}
	
	internal bool Used { get; private set; }
}

/// <summary>
/// A trigger scope returned by functions like <see cref="TriggerScopes.Window"/> and used with <see cref="TriggerScopes.Again"/>.
/// </summary>
/// <example>See <see cref="TriggerScopes"/>.</example>
public class TriggerScope {
	internal readonly object o; //wndFinder, wndFinder[]
	internal readonly bool not;
	internal int perfTime;
	
	internal TriggerScope(object o, bool not) {
		this.o = o;
		this.not = not;
	}
	
	/// <summary>
	/// Returns <c>true</c> if window matches.
	/// </summary>
	/// <param name="thc">This func uses the window handle (gets on demand) and <b>WFCache</b>.</param>
	internal bool Match(TriggerHookContext thc) {
		bool yes = false;
		var w = thc.Window;
		if (!w.Is0) {
			switch (o) {
			case wndFinder f:
				yes = f.IsMatch(w, thc);
				break;
			case wndFinder[] a:
				foreach (var v in a) {
					if (yes = v.IsMatch(w, thc)) break;
				}
				break;
			}
		}
		return yes ^ not;
	}
}

/// <summary>
/// Allows to define custom scopes/contexts/conditions for triggers.
/// </summary>
/// <remarks>
/// Similar to <see cref="TriggerScopes"/> (code like <c>Triggers.Of.Window(...);</c>), but allows to define any scope/condition/etc, not just the active window.
/// 
/// To define a scope, you create a callback function (CF) that checks some conditions and returns <c>true</c> to allow the trigger action to run or <c>false</c> to not allow. Assign the CF to some property of this class and then add the trigger, like in the examples below. The CF will be assigned to the trigger and called when need.
/// 
/// You may ask: why to use CF when the trigger action (TA) can do the same?
/// 1. CF runs synchronously; if it returns <c>false</c>, the trigger key or mouse button message is passed to other triggers, hooks and apps. TA cannot do it reliably; it runs asynchronously, and the message is already stealed from other apps/triggers/hooks.
/// 2. CF is faster to call. It is simply called in the same thread that processes trigger messages. TA usually runs in another thread.
/// 3. A CF can be assigned to multiple triggers with a single line of code. Don't need to add the same code in all trigger actions.
/// 
/// A trigger can have up to 4 CF delegates and a window scope (<c>Triggers.Of...</c>). They are called in this order: CF assigned through <see cref="FollowingTriggersBeforeWindow"/>, <see cref="NextTriggerBeforeWindow"/>, window scope, <see cref="NextTrigger"/>, <see cref="FollowingTriggers"/>. The <b>NextX</b> properties assign the CF to the next single trigger. The <b>FollowingX</b> properties assign the CF to all following triggers until you assign another CF or <c>null</c>. If several are assigned, the trigger action runs only if all CF return <c>true</c> and the window scope matches. The <b>XBeforeWindow</b> properties are used only with hotkey, autotext and mouse triggers.
/// 
/// All CF must be as fast as possible. Slow CF can make triggers slower (or even all keyboard/mouse input); also may cause warnings and trigger failures. A big problem is the low-level hooks timeout that Windows applies to trigger hooks; see <see cref="More.WindowsHook.LowLevelHooksTimeout"/>. A related problem - slow JIT and loading of assemblies, which can make the CF too slow the first time; in some rare cases may even need to preload assemblies or pre-JIT functions to avoid the timeout warning.
///
/// In CF never use functions that generate keyboard or mouse events or activate windows.
/// </remarks>
/// <example>
/// Note: the <b>Triggers</b> in examples is a field or property like <c>readonly ActionTriggers Triggers = new();</c>.
/// <code><![CDATA[
/// //examples of assigning a callback function (CF) to a single trigger
/// Triggers.FuncOf.NextTrigger = o => keys.isCapsLock; //o => keys.isCapsLock is the callback function (lambda)
/// Triggers.Hotkey["Ctrl+K"] = o => print.it("action: Ctrl+K while CapsLock is on");
/// Triggers.FuncOf.NextTrigger = o => { var v = o as HotkeyTriggerArgs; print.it($"func: mod={v.Mod}"); return mouse.isPressed(MButtons.Left); };
/// Triggers.Hotkey["Ctrl+Shift?+B"] = o => print.it("action: mouse left button + Ctrl+B or Ctrl+Shift+B");
/// 
/// //examples of assigning a CF to multiple triggers
/// Triggers.FuncOf.FollowingTriggers = o => { var v = o as HotkeyTriggerArgs; print.it("func", v); return true; };
/// Triggers.Hotkey["Ctrl+F8"] = o => print.it("action: " + o);
/// Triggers.Hotkey["Ctrl+F9"] = o => print.it("action: " + o);
/// Triggers.FuncOf.FollowingTriggers = null; //stop assigning the CF to triggers added afterwards
/// 
/// //sometimes all work can be done in CF and you don't need the trigger action
/// Triggers.FuncOf.NextTrigger = o => { var v = o as HotkeyTriggerArgs; print.it("func: " + v); return true; };
/// Triggers.Hotkey["Ctrl+F12"] = null;
/// 
/// Triggers.Run();
/// ]]></code>
/// </example>
public class TriggerFuncs {
	internal TriggerFuncs() { }
	
	internal Dictionary<TFunc, TriggerFunc> perfDict = new Dictionary<TFunc, TriggerFunc>();
	
	//internal bool Used { get; private set; }
	
	internal TFunc nextAfter, nextBefore, commonAfter, commonBefore;
	
	/// <summary>
	/// Sets callback function for the next added trigger.
	/// If the trigger has a window scope, the callback function is called after evaluating the window.
	/// This function is used with triggers of all types.
	/// </summary>
	public TFunc NextTrigger {
		get => nextAfter;
		set => nextAfter = _Func(value);
	}
	
	/// <summary>
	/// Sets callback function for the next added trigger.
	/// If the trigger has a window scope, the callback function is called before evaluating the window.
	/// This function is used with triggers of these types: hotkey, autotext, mouse.
	/// </summary>
	public TFunc NextTriggerBeforeWindow {
		get => nextBefore;
		set => nextBefore = _Func(value);
	}
	
	/// <summary>
	/// Sets callback function for multiple triggers added afterwards.
	/// If the trigger has a window scope, the callback function is called after evaluating the window.
	/// This function is used with triggers of all types.
	/// The value can be <c>null</c>.
	/// </summary>
	public TFunc FollowingTriggers {
		get => commonAfter;
		set => commonAfter = _Func(value);
	}
	
	/// <summary>
	/// Sets callback function for multiple triggers added afterwards.
	/// If the trigger has a window scope, the callback function is called before evaluating the window.
	/// This function is used with triggers of these types: hotkey, autotext, mouse.
	/// The value can be <c>null</c>.
	/// </summary>
	public TFunc FollowingTriggersBeforeWindow {
		get => commonBefore;
		set => commonBefore = _Func(value);
	}
	
	TFunc _Func(TFunc f) {
		//if(f != null) Used = true;
		return f;
	}
	
	/// <summary>
	/// Clears all properties (sets = <c>null</c>).
	/// </summary>
	public void Reset() {
		nextAfter = null;
		nextBefore = null;
		commonAfter = null;
		commonBefore = null;
	}
}

class TriggerFunc {
	internal TFunc f;
	internal int perfTime;
}

/// <summary>
/// Type of functions used with class <see cref="TriggerFuncs"/> to define custom scope for triggers.
/// </summary>
/// <param name="args">Trigger action arguments. Example: <see cref="TriggerFuncs"/>.</param>
/// <returns>Return <c>true</c> to run the trigger action, or <c>false</c> to not run.</returns>
public delegate bool TFunc(TriggerArgs args);
