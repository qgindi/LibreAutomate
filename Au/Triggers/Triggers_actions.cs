namespace Au.Triggers;

class TOptions {
	public Action<TOBAArgs> before;
	public Action<TOBAArgs> after;
	public sbyte thread; //>=0 dedicated or <0 TOThread
	public TOFlags flags;
	public int wait;
	public PostToThisThread_ thisThread;
	
	public TOptions Clone() => this.MemberwiseClone() as TOptions;
}

class TOThread { public const sbyte OfTriggers = -1, New = -2, Pool = -3, This = -4; }

[Flags]
enum TOFlags : byte {
	NoWarning = 1,
	Single = 2,
	//MtaThread = 4,
	//BackgroundThread=8, //rejected. Always background. Foreground has no sense here. If need, can easily set in code.
}

/// <summary>
/// Allows to set some options for multiple triggers and their actions.
/// </summary>
/// <remarks>
/// You set options through a thread-static property <see cref="ActionTriggers.Options"/>.
/// Changed options are applied to all triggers/actions added afterwards in this thread.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// Triggers.Options.ThreadNew();
/// Triggers.Options.BeforeAction = o => { opt.key.KeySpeed = 10; };
/// Triggers.Hotkey["Ctrl+K"] = o => print.it(opt.key.KeySpeed); //10
/// Triggers.Hotkey["Ctrl+Shift+K"] = o => print.it(opt.key.KeySpeed); //10
/// Triggers.Options.BeforeAction = o => { opt.key.KeySpeed = 20; };
/// Triggers.Hotkey["Ctrl+L"] = o => print.it(opt.key.KeySpeed); //20
/// Triggers.Hotkey["Ctrl+Shift+L"] = o => print.it(opt.key.KeySpeed); //20
/// ]]></code>
/// </example>
public class TriggerOptions {
	TOptions _new, _prev;
	
	TOptions _New() => _new ??= (_prev?.Clone() ?? new TOptions());
	
	/// <summary>
	/// Run actions always in the same dedicated thread that does not end when actions end.
	/// </summary>
	/// <param name="thread">A number that you want to use to identify the thread. Can be 0-127. Default 0.</param>
	/// <param name="wait">Defines when to start an action if an action (other or same) is currently running in this thread. If 0 (default), don't run. If -1 (<b>Timeout.Infinite</b>), run when that action ends (and possibly other queued actions). If &gt; 0, run when that action ends, if it ends within this time from now; the time is in milliseconds.</param>
	/// <param name="noWarning">No warning when cannot start an action because an action is running and <i>wait</i> == 0.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <remarks>
	/// Multiple actions in same thread cannot run simultaneously. Actions in different threads can run simultaneously.
	/// There is no "end old running action" feature. If need it, use other script. Example: <c>Triggers.Hotkey["Ctrl+M"] = o => script.runWait("Other Script");</c>.
	/// There is no "temporarily pause old running action to run new action" feature. As well as for scripts.
	/// The thread has <see cref="ApartmentState.STA"/>.
	/// There are several <b>ThreadX</b> functions. Only the last called function is active. If none called, it is the same as called this function without arguments.
	/// </remarks>
	public void Thread(int thread = 0, int wait = 0, bool noWarning = false) {
		_New();
		if ((uint)thread > 127) throw new ArgumentOutOfRangeException();
		_new.thread = (sbyte)thread;
		_new.wait = wait >= -1 ? wait : throw new ArgumentOutOfRangeException();
		_new.flags = noWarning ? TOFlags.NoWarning : 0;
	}
	//CONSIDER: make default ifRunningWaitMS = 1000 if it is another action.
	
	/// <summary>
	/// Run trigger actions in the same thread as <see cref="ActionTriggers.Run"/>. Dangerous, rarely used.
	/// </summary>
	/// <remarks>
	/// This should not be used without a good reason. Trigger actions must be programmed carefully, to not interfere with triggers. They must be as fast as possible, else will block triggers, hooks and user input.
	///
	/// Before v0.16 this was named <b>ThreadMain</b> and used in the <c>"Triggers and toolbars"</c> script. Problem: blocks hooks etc when need long time to get file icons. Now the script uses <see cref="ThreadThis"/> instead, and calls <c>Triggers.Run</c> in another thread. Your script possibly still uses the old code. You can replace it with the new version, which can be found in menu <b>File > New > Default > Triggers and toolbars</b>.
	/// </remarks>
	public void ThreadOfTriggers() {
		_New();
		_new.thread = TOThread.OfTriggers;
		_new.wait = 0;
		_new.flags = 0;
	}
	
	/// <summary>
	/// Alias of <see cref="ThreadOfTriggers"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)] //renamed
	public void ThreadMain() => ThreadOfTriggers();
	
	/// <summary>
	/// Run trigger actions in this thread (which called this function).
	/// </summary>
	/// <remarks>
	/// This function can be used only if <see cref="ActionTriggers.Run"/> runs in another thread. This thread must have a message loop (wait and dispatch messages). For it can be used <see cref="ActionTriggers.RunThread"/>.
	///
	/// Trigger actions should be fast, else other trigger actions may be delayed. If a trigger action dispatches messages, other trigger actions can run in the meantime.
	/// 
	/// Can be used to create and show toolbars (<see cref="toolbar"/>). Used in the default <c>"Triggers and toolbars"</c> script since v0.16.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// print.it(Environment.CurrentManagedThreadId);
	/// ActionTriggers Triggers = new();
	/// Triggers.Options.ThreadThis();
	/// Triggers.Hotkey["F11"] = o => { print.it(Environment.CurrentManagedThreadId); };
	/// Triggers.RunThread();
	/// ]]></code>
	/// </example>
	public void ThreadThis() {
		_New();
		_new.thread = TOThread.This;
		_new.wait = 0;
		_new.flags = 0;
		_new.thisThread = PostToThisThread_.OfThisThread;
	}
	
	/// <summary>
	/// Run trigger actions in new threads.
	/// </summary>
	/// <param name="single">Don't run if this action is already running. If <c>false</c>, multiple action instances can run parallelly in multiple threads.</param>
	/// <remarks>
	/// The action can run simultaneously with other actions. The thread is STA.
	/// </remarks>
	public void ThreadNew(bool single = false) {
		_New();
		_new.thread = TOThread.New;
		_new.wait = 0;
		TOFlags f = 0;
		if (single) f |= TOFlags.Single;
		_new.flags = f;
	}
	
	/// <summary>
	/// Run trigger actions in thread pool threads.
	/// </summary>
	/// <param name="single">Don't run if this action is already running. If <c>false</c>, multiple action instances can run parallelly in multiple threads.</param>
	/// <remarks>
	/// The action can run simultaneously with other actions. May start later if the pool is busy.
	/// You should know how to use thread pool correctly. The action runs in the .NET thread pool through <see cref="Task.Run"/>.
	/// </remarks>
	public void ThreadPool(bool single = false) {
		_New();
		_new.thread = TOThread.Pool;
		_new.wait = 0;
		_new.flags = single ? TOFlags.Single : 0;
	}
	
	/// <summary>
	/// A function to run before the trigger action.
	/// For example, it can set <see cref="opt"/> options.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// Triggers.Options.BeforeAction = o => { opt.key.KeySpeed = 20; opt.key.TextSpeed = 5; };
	/// ]]></code>
	/// </example>
	public Action<TOBAArgs> BeforeAction { set => _New().before = value; }
	
	/// <summary>
	/// A function to run after the trigger action.
	/// For example, it can log exceptions.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// Triggers.Options.AfterAction = o => { if(o.Exception!=null) print.it(o.Exception.Message); else print.it("completed successfully"); };
	/// ]]></code>
	/// </example>
	public Action<TOBAArgs> AfterAction { set => _New().after = value; }
	
	internal TOptions Current {
		get {
			if (_new != null) { _prev = _new; _new = null; }
			return _prev ?? (s_empty ??= new TOptions());
		}
	}
	static TOptions s_empty;
	
	/// <summary>
	/// If <c>true</c>, triggers added afterwards don't depend on <see cref="ActionTriggers.Disabled"/> and <see cref="ActionTriggers.DisabledEverywhere"/>.
	/// This property sets the <see cref="ActionTrigger.EnabledAlways"/> property of triggers added afterwards.
	/// </summary>
	public bool EnabledAlways { get; set; }
	
	/// <summary>
	/// Clears all options.
	/// </summary>
	public void Reset() {
		_new = null;
		_prev = null;
	}
}

/// <summary>
/// Arguments for <see cref="TriggerOptions.BeforeAction"/> and <see cref="TriggerOptions.AfterAction"/>.
/// </summary>
public struct TOBAArgs {
	internal TOBAArgs(TriggerArgs args) {
		ActionArgs = args;
		Exception = null;
	}
	
	/// <summary>
	/// Trigger event info. The same variable as passed to the trigger action.
	/// To access the info, cast to <b>HotkeyTriggerArgs</b> etc, depending on trigger type.
	/// </summary>
	public TriggerArgs ActionArgs { get; }
	
	/// <summary>
	/// If action ended with an exception, the exception. Else <c>null</c>.
	/// </summary>
	public Exception Exception { get; internal set; }
}

class TriggerActionThreads {
	public void Run(ActionTrigger trigger, TriggerArgs args, int muteMod) {
		//perf.first();
		Action actionWrapper = () => {
			var o = trigger.options;
			var oldOpt = o.thread == TOThread.New ? default : opt.scope.all(inherit: false);
			try {
				_MuteMod(ref muteMod);
				
				string sTrigger = null; long startTime = 0;
				//perf.next();
				if (script.role == SRole.MiniProgram) {
					sTrigger = trigger.ToString();
					Api.QueryPerformanceCounter(out startTime);
					print.TaskEvent_("AS " + sTrigger, startTime, trigger.sourceFile, trigger.sourceLine);
					//perf.next();
				}
				
				var baArgs = new TOBAArgs(args); //struct
				o.before?.Invoke(baArgs);
				try {
					//perf.nw();
					trigger.Run(args);
					
					if (sTrigger != null) print.TaskEvent_("AE", startTime, trigger.sourceFile, trigger.sourceLine);
				}
				catch (Exception e1) {
					if (sTrigger != null) print.TaskEvent_("AF", startTime, trigger.sourceFile, trigger.sourceLine);
					
					baArgs.Exception = e1;
					print.it(e1);
				}
				o.after?.Invoke(baArgs);
			}
			catch (Exception e2) {
				print.it(e2);
			}
			finally {
				oldOpt.Dispose();
				if (o.flags.Has(TOFlags.Single)) _d.TryRemove(trigger, out _);
				if (o.thread is not (TOThread.OfTriggers or TOThread.This)) toolbar.TriggerActionEndedInToolbarUnfriendlyThread_();
			}
		};
		//never mind: we should not create actionWrapper if cannot run. But such cases are rare. Fast and small, about 64 bytes.
		
		var opt1 = trigger.options;
		int threadId = opt1.thread;
		if (threadId >= 0) { //dedicated thread
			_Thread h = null; foreach (var v in _a) if (v.id == threadId) { h = v; break; }
			if (h == null) _a.Add(h = new _Thread(threadId));
			if (h.RunAction(actionWrapper, trigger)) return;
		} else if (threadId == TOThread.OfTriggers) {
			actionWrapper();
			return;
			//note: can reenter. Probably it is better than to cancel if already running.
		} else if (threadId == TOThread.This) {
			if (opt1.thisThread.ManagedThreadId == Environment.CurrentManagedThreadId) print.warning("If called ThreadThis, triggers should run in another thread.");
			opt1.thisThread.Post(actionWrapper);
			return;
			//note: can reenter.
		} else {
			bool canRun = true;
			bool single = opt1.flags.Has(TOFlags.Single);
			if (single) {
				_d ??= new();
				if (_d.TryGetValue(trigger, out var tt)) {
					switch (tt) {
					case Thread thread:
						if (thread.IsAlive) canRun = false;
						break;
					case Task task:
						//print.it(task.Status);
						switch (task.Status) {
						case TaskStatus.RanToCompletion: case TaskStatus.Faulted: case TaskStatus.Canceled: break;
						default: canRun = false; break;
						}
						break;
					}
				}
			}
			
			if (canRun) {
				if (threadId == TOThread.New) {
					var thread = new Thread(actionWrapper.Invoke) { IsBackground = true };
					//if (!opt1.flags.Has(TOFlags.MtaThread))
					thread.SetApartmentState(ApartmentState.STA);
					if (single) _d[trigger] = thread;
					try { thread.Start(); }
					catch (OutOfMemoryException) { //too many threads, probably 32-bit process
						if (single) _d.TryRemove(trigger, out _);
						_OutOfMemory();
						//SHOULDDO: before starting thread, warn if there are too many action threads.
						//	In 32-bit process normally fails at ~3000 threads.
						//	Unlikely to fail in 64-bit process, but at ~15000 threads starts to hang temporarily, which causes hook timeout, slow mouse, other anomalies.
					}
				} else { //thread pool
					var task = new Task(actionWrapper);
					if (single) _d[trigger] = task;
					task.Start();
				}
				return;
			}
		}
		
		if (muteMod != 0) ThreadPool.QueueUserWorkItem(_ => _MuteMod(ref muteMod));
	}
	
	public void Dispose() {
		foreach (var v in _a) v.Dispose();
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static void _OutOfMemory() {
		print.warning("There is not enough memory available to start the trigger action thread.", -1); //info: -1 because would need much memory for stack trace
	}
	
	List<_Thread> _a = new();
	ConcurrentDictionary<ActionTrigger, object> _d;
	
	class _Thread {
		record struct _Action(Action actionWrapper, long time);
		
		AutoResetEvent _event;
		Queue<_Action> _q;
		bool _running;
		bool _disposed;
		public readonly int id;
		
		public _Thread(int id) { this.id = id; }
		
		/// <summary>
		/// Adds the action to the queue and notifies the thread to execute it.
		/// If the thread is busy, returns <c>false</c>; if <i>ifRunning</i>!=0, the action possibly will run later.
		/// </summary>
		public bool RunAction(Action actionWrapper, ActionTrigger trigger) {
			if (_disposed) return false;
			if (_q == null) {
				_q = new Queue<_Action>();
				_event = new(false);
				try {
					run.thread(() => {
						try {
							while (!_disposed && _event.WaitOne()) {
								while (!_disposed) {
									_Action x;
									lock (_q) {
										g1:
										if (_q.Count == 0) { _running = false; break; }
										x = _q.Dequeue();
										if (x.time != 0 && perf.ms > x.time) goto g1;
										_running = true;
									}
									x.actionWrapper();
								}
							}
						}
						finally {
							_event.Dispose(); _event = null;
							_q = null; _running = false; //restart if aborted
							//print.it("thread ended");
						}
					});
				}
				catch (OutOfMemoryException) { //too many threads, probably 32-bit process
					_event.Dispose(); _event = null;
					_OutOfMemory();
				}
			}
			
			bool R = true;
			lock (_q) {
				int ifRunningWaitMS = trigger.options.wait;
				if (_running) {
					if (ifRunningWaitMS == 0) {
						if (!trigger.options.flags.Has(TOFlags.NoWarning))
							print.it($"<>Warning: can't run the trigger action because an action is running in this thread. <open {trigger.sourceFile}|{trigger.sourceLine}>Trigger<>: {trigger}."
								+ " <fold>\tTo run simultaneously or wait, use one of Triggers.Options.ThreadX functions.\r\n\tTo disable this warning: Triggers.Options.Thread(noWarning: true);.</fold>");
						return false;
					}
					R = false;
				} else {
					_running = true;
					//if(ifRunningWaitMS > 0 && ifRunningWaitMS < 1000000000) ifRunningWaitMS += 1000;
				}
				_q.Enqueue(new _Action(actionWrapper, ifRunningWaitMS <= 0 ? 0 : perf.ms + ifRunningWaitMS));
			}
			_event.Set();
			return R;
		}
		
		public void Dispose() {
			if (_disposed) return; _disposed = true;
			_event.Set();
		}
	}
	//This old version uses WaitForSingleObject which blocks COM etc, which may cause problems.
	//	In the above code, WaitOne dispatches COM etc. But still little tested.
	//class _Thread
	//{
	//	struct _Action { public Action actionWrapper; public long time; }
	
	//	Handle_ _event;
	//	Queue<_Action> _q;
	//	bool _running;
	//	bool _disposed;
	//	public readonly int id;
	
	//	public _Thread(int id) { this.id = id; }
	
	//	/// <summary>
	//	/// Adds the action to the queue and notifies the thread to execute it.
	//	/// If the thread is busy, returns false; if ifRunning!=0, the action possibly will run later.
	//	/// </summary>
	//	public bool RunAction(Action actionWrapper, ActionTrigger trigger)
	//	{
	//		if(_disposed) return false;
	//		if(_q == null) {
	//			_q = new Queue<_Action>();
	//			_event = Api.CreateEvent(false);
	//			try {
	//				run.thread(() => {
	//					try {
	//						while(!_disposed && 0 == Api.WaitForSingleObject(_event, -1)) {
	//							while(!_disposed) {
	//								_Action x;
	//								lock(_q) {
	//									g1:
	//									if(_q.Count == 0) { _running = false; break; }
	//									x = _q.Dequeue();
	//									if(x.time != 0 && perf.ms > x.time) goto g1;
	//									_running = true;
	//								}
	//								x.actionWrapper();
	//							}
	//						}
	//					}
	//					finally {
	//						_event.Dispose();
	//						_q = null; _running = false; //restart if aborted
	//													 //print.it("thread ended");
	//					}
	//				});
	//			}
	//			catch(OutOfMemoryException) { //too many threads, probably 32-bit process
	//				_event.Dispose();
	//				_OutOfMemory();
	//			}
	//		}
	
	//		bool R = true;
	//		lock(_q) {
	//			int ifRunningWaitMS = trigger.options.ifRunningWaitMS;
	//			if(_running) {
	//				if(ifRunningWaitMS == 0) {
	//					if(!trigger.options.flags.Has(TOFlags.NoWarning))
	//						print.it("Warning: can't run the trigger action because an action is running in this thread." +
	//							" To run simultaneously or wait, use one of Triggers.Options.ThreadX functions." +
	//							" To disable this warning: Triggers.Options.Thread(noWarning: true);." +
	//							" Trigger: " + trigger);
	//					return false;
	//				}
	//				R = false;
	//			} else {
	//				_running = true;
	//				//if(ifRunningWaitMS > 0 && ifRunningWaitMS < 1000000000) ifRunningWaitMS += 1000;
	//			}
	//			_q.Enqueue(new _Action { actionWrapper = actionWrapper, time = ifRunningWaitMS <= 0 ? 0 : perf.ms + ifRunningWaitMS });
	//		}
	//		Api.SetEvent(_event);
	//		return R;
	//	}
	
	//	public void Dispose()
	//	{
	//		if(_disposed) return; _disposed = true;
	//		Api.SetEvent(_event);
	//	}
	//}
	
	static void _MuteMod(ref int muteMod) {
		switch (Interlocked.Exchange(ref muteMod, 0)) {
		case c_modRelease:
			keys.Internal_.ReleaseModAndDisableModMenu();
			break;
		case c_modCtrl:
			keys.Internal_.SendKey(KKey.Ctrl); //disable Alt/Win menu
			break;
		}
	}
	
	public const int c_modRelease = 1, c_modCtrl = 2;
}
