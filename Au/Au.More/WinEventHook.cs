namespace Au.More {
	/// <summary>
	/// Helps with UI element event hooks. See API <msdn>SetWinEventHook</msdn>.
	/// </summary>
	/// <remarks>
	/// The thread that uses hooks must process Windows messages. For example have a window/dialog/messagebox, or use a "wait-for" function that dispatches messages or has such option (see <see cref="Seconds.DoEvents"/>).
	/// 
	/// <note type="important">The variable should be disposed when don't need, or at least unhooked, either explicitly (call <b>Dispose</b> or <b>Unhook</b> in same thread) or with <c>using</c>. Can do it in hook procedure.</note>
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// bool stop = false;
	/// using var hook = new WinEventHook(EEvent.SYSTEM_FOREGROUND, 0, x => {
	/// 	print.it(x.w);
	/// 	var e = x.GetElm();
	/// 	print.it(e);
	/// 	if(x.w.ClassNameIs("Shell_TrayWnd")) stop = true;
	/// });
	/// dialog.show("hook");
	/// //or
	/// //wait.doEventsUntil(-10, () => stop); //wait max 10 s for activated taskbar
	/// //print.it("the end");
	/// ]]></code>
	/// </example>
	[DebuggerStepThrough]
	public sealed class WinEventHook : IDisposable {
		IntPtr[] _a;
		Api.WINEVENTPROC _proc1; //our intermediate hook proc that calls _proc2
		Action<HookData.WinEvent> _proc2; //caller's hook proc
		[ThreadStatic] static List<WinEventHook> t_antiGC;
		
		/// <summary>
		/// Sets a hook for an event or a range of events.
		/// </summary>
		/// <param name="eventMin">The lowest event constant value in the range of events. Can be <b>EEvent.MIN</b> to indicate the lowest possible event value. Events reference: <msdn>SetWinEventHook</msdn>. Value 0 is ignored.</param>
		/// <param name="eventMax">The highest event constant value in the range of events. Can be <b>EEvent.MAX</b> to indicate the highest possible event value. If 0, uses <i>eventMin</i>.</param>
		/// <param name="hookProc">The hook procedure (function that handles hook events).</param>
		/// <param name="idProcess">The id of the process from which the hook function receives events. If 0 - all processes on the current desktop.</param>
		/// <param name="idThread">The native id of the thread from which the hook function receives events. If 0 - all threads.</param>
		/// <param name="flags"></param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>See <see cref="WinEventHook"/>.</example>
		public WinEventHook(EEvent eventMin, EEvent eventMax, Action<HookData.WinEvent> hookProc, int idProcess = 0, int idThread = 0, EHookFlags flags = 0) {
			Not_.Null(hookProc);
			_proc1 = _HookProc;
			Hook(eventMin, eventMax, idProcess, idThread, flags);
			_proc2 = hookProc;
			(t_antiGC ??= new()).Add(this);
		}
		
		/// <summary>
		/// Sets multiple hooks.
		/// </summary>
		/// <param name="events">Events. Reference: API <msdn>SetWinEventHook</msdn>. Elements with value 0 are ignored.</param>
		/// <inheritdoc cref="WinEventHook(EEvent, EEvent, Action{HookData.WinEvent}, int, int, EHookFlags)"/>
		public WinEventHook(EEvent[] events, Action<HookData.WinEvent> hookProc, int idProcess = 0, int idThread = 0, EHookFlags flags = 0) {
			Not_.Null(hookProc);
			_proc1 = _HookProc;
			Hook(events, idProcess, idThread, flags);
			_proc2 = hookProc;
			(t_antiGC ??= new()).Add(this);
		}
		
		/// <exception cref="InvalidOperationException">Hooks are already set and <see cref="Unhook"/> not called.</exception>
		/// <inheritdoc cref="WinEventHook(EEvent, EEvent, Action{HookData.WinEvent}, int, int, EHookFlags)"/>
		public void Hook(EEvent eventMin, EEvent eventMax = 0, int idProcess = 0, int idThread = 0, EHookFlags flags = 0) {
			_Throw1();
			_a = new IntPtr[1];
			_SetHook(0, eventMin, eventMax, idProcess, idThread, flags);
		}
		
		/// <exception cref="InvalidOperationException">Hooks are already set and <see cref="Unhook"/> not called.</exception>
		/// <inheritdoc cref="WinEventHook(EEvent[], Action{HookData.WinEvent}, int, int, EHookFlags)"/>
		public void Hook(EEvent[] events, int idProcess = 0, int idThread = 0, EHookFlags flags = 0) {
			_Throw1();
			_a = new IntPtr[events.Length];
			for (int i = 0; i < events.Length; i++) _SetHook(i, events[i], 0, idProcess, idThread, flags);
		}
		
		void _SetHook(int i, EEvent eMin, EEvent eMax, int idProcess, int idThread, EHookFlags flags) {
			if (eMin == 0) return;
			if (eMax == 0) eMax = eMin;
			var hh = Api.SetWinEventHook(eMin, eMax, default, _proc1, idProcess, idThread, flags);
			if (hh == default) {
				var ec = lastError.code;
				Unhook();
				throw new AuException(ec, "*set hook for " + eMin.ToString());
			}
			_a[i] = hh;
		}
		
		void _Throw1() {
			if (_a != null) throw new InvalidOperationException();
			if (_proc1 == null) throw new ObjectDisposedException(nameof(WinEventHook));
		}
		
		/// <summary>
		/// Adds a hook for an event or a range of events.
		/// </summary>
		/// <returns>an int value greater than 0 that can be used with <see cref="Remove"/>.</returns>
		/// <remarks>
		/// Parameters are the same as of the constructor, but values can be different.
		/// 
		/// This function together with <see cref="Remove"/> can be used to temporarily add/remove one or more hooks while using the same <b>WinEventHook</b> variable and hook procedure. Don't need to call <b>Unhook</b> before.
		/// </remarks>
		/// <inheritdoc cref="WinEventHook(EEvent, EEvent, Action{HookData.WinEvent}, int, int, EHookFlags)"/>
		public int Add(EEvent eventMin, EEvent eventMax = 0, int idProcess = 0, int idThread = 0, EHookFlags flags = 0) {
			if (_proc1 == null) throw new ObjectDisposedException(nameof(WinEventHook));
			int i = 0;
			if (_a == null) {
				_a = new IntPtr[1];
			} else {
				for (; i < _a.Length; i++) if (_a[i] == default) goto g1;
				Array.Resize(ref _a, i + 1);
			}
			g1:
			_SetHook(i, eventMin, eventMax, idProcess, idThread, flags);
			return i + 1;
		}
		
		/// <summary>
		/// Removes a hook added by <see cref="Add"/>.
		/// </summary>
		/// <param name="addedId">A return value of <see cref="Add"/>.</param>
		/// <exception cref="ArgumentException"></exception>
		public void Remove(int addedId) {
			addedId--;
			if (_a == null || (uint)addedId >= _a.Length || _a[addedId] == default) throw new ArgumentException();
			if (!Api.UnhookWinEvent(_a[addedId])) print.warning("Failed to unhook WinEventHook.");
			_a[addedId] = default;
		}
		
		///// <summary>
		///// True if hooks are set.
		///// </summary>
		//public bool Installed => _a != null;
		
		/// <summary>
		/// Removes all hooks.
		/// </summary>
		/// <remarks>
		/// Does nothing if already removed or wasn't set.
		/// Must be called from the same thread that sets the hook.
		/// </remarks>
		public void Unhook() {
			if (_a != null) {
				foreach (var hh in _a) {
					if (hh == default) continue;
					if (!Api.UnhookWinEvent(hh)) print.warning("WinEventHook.Unhook() failed.");
				}
				_a = null;
			}
		}
		
		/// <summary>
		/// Calls <see cref="Unhook"/>.
		/// </summary>
		public void Dispose() {
			Unhook();
			_proc1 = null;
			t_antiGC.Remove(this);
			GC.SuppressFinalize(this);
		}
		
		/// <summary>
		/// Prints a warning if the variable is not disposed. Cannot dispose in finalizer.
		/// </summary>
		~WinEventHook() {
			//MSDN: UnhookWinEvent fails if called from a thread different from the call that corresponds to SetWinEventHook.
			if (_a != null) print.warning("Non-disposed WinEventHook variable.");
		}
		
		void _HookProc(IntPtr hHook, EEvent ev, wnd w, EObjid idObject, int idChild, int thread, int time) {
			try {
				_proc2(new HookData.WinEvent(this, ev, w, idObject, idChild, thread, time));
			}
			catch (Exception ex) { WindowsHook.OnException_(ex); }
		}
	}
}

namespace Au.Types {
	public static partial class HookData {
		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WinEventHook"/>.
		/// More info: API <msdn>WinEventProc</msdn>.
		/// </summary>
		public unsafe struct WinEvent {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WinEventHook hook;
			
			/// <summary>API <msdn>WinEventProc</msdn></summary>
			public readonly EEvent event_;
			
			/// <summary>API <msdn>WinEventProc</msdn></summary>
			public readonly wnd w;
			
			/// <summary>API <msdn>WinEventProc</msdn></summary>
			public readonly EObjid idObject;
			
			/// <summary>API <msdn>WinEventProc</msdn></summary>
			public readonly int idChild;
			
			/// <summary>API <msdn>WinEventProc</msdn></summary>
			public readonly int thread;
			
			/// <summary>API <msdn>WinEventProc</msdn></summary>
			public readonly int time;
			
			internal WinEvent(WinEventHook hook, EEvent event_, wnd hwnd, EObjid idObject, int idChild, int thread, int time) {
				this.hook = hook;
				this.event_ = event_;
				this.w = hwnd;
				this.idObject = idObject;
				this.idChild = idChild;
				this.thread = thread;
				this.time = time;
			}
			
			/// <summary>
			/// Calls <see cref="elm.fromEvent"/>.
			/// </summary>
			public elm GetElm() {
				return elm.fromEvent(w, idObject, idChild);
			}
		}
	}
}
