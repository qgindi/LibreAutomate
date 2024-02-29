namespace Au.More {
	/// <summary>
	/// Wraps API <msdn>SetWindowsHookEx</msdn>.
	/// </summary>
	/// <remarks>
	/// Hooks are used to receive notifications about various system events. Keyboard and mouse input, window messages, various window events.
	/// 
	/// Threads that use hooks must process Windows messages. For example have a window/dialog/messagebox, or use a "wait-for" function that dispatches messages or has such option (see <see cref="Seconds.DoEvents"/>).
	/// 
	/// <note type="important">The variable should be disposed when don't need, or at least unhooked, either explicitly (call <b>Dispose</b> or <b>Unhook</b> in same thread) or with <c>using</c>. Can do it in hook procedure.</note>
	/// 
	/// <note type="warning">Avoid many hooks. Each low-level keyboard or mouse hook makes the computer slower, even if the hook procedure is fast. On each input event (key down, key up, mouse move, click, wheel) Windows sends a message to your thread.</note>
	/// 
	/// To receive hook events is used a callback function, aka hook procedure. Hook procedures of some hook types can block some events (call <b>BlockEvent</b> or return <c>true</c>). Blocked events are not sent to apps and older hooks.
	/// 
	/// Delegates of hook procedures are protected from GC until called <b>Dispose</b> or until the thread ends, even of unreferenced <b>WindowsHook</b> variables.
	/// 
	/// UI element functions may fail in hook procedures of low-level keyboard and mouse hooks. Workarounds exist.
	/// 
	/// Exists an alternative way to monitor keyboard or mouse events - raw input API. Good: less overhead; can detect from which device the input event came. Bad: cannot block events; incompatible with low-level keyboard hooks. This library does not have functions to make the API easier to use.
	/// </remarks>
	[DebuggerStepThrough]
	public sealed class WindowsHook : IDisposable {
		IntPtr _hh; //HHOOK
		readonly Api.HOOKPROC _proc1; //our intermediate dispatcher hook proc that calls _proc2
		Delegate _proc2; //caller's hook proc
		readonly string _hookTypeString; //"Keyboard" etc
		readonly int _hookType; //Api.WH_
		readonly bool _ignoreAuInjected;
		[ThreadStatic] static List<WindowsHook> t_antiGC;

		/// <summary>
		/// Sets a low-level keyboard hook (<b>WH_KEYBOARD_LL</b>).
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <returns>New <see cref="WindowsHook"/> object that manages the hook.</returns>
		/// <param name="hookProc">
		/// The hook procedure (function that handles hook events).
		/// Must return as soon as possible. More info: <see cref="LowLevelHooksTimeout"/>.
		/// If calls <see cref="HookData.Keyboard.BlockEvent"/> or <see cref="HookData.ReplyMessage"/><c>(true)</c>, the event is not sent to apps and other hooks.
		/// Event data cannot be modified.
		/// <para>NOTE: When the hook procedure returns, the parameter variable becomes invalid and unsafe to use. If you need the data for later use, copy its properties and not whole variable.</para>
		/// </param>
		/// <param name="ignoreAuInjected">Don't call the hook procedure for events sent by functions of this library. Default <c>true</c>.</param>
		/// <param name="setNow">Set hook now. Default <c>true</c>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>
		/// <code><![CDATA[
		/// var stop = false;
		/// using var hook = WindowsHook.Keyboard(x => {
		/// 	print.it(x);
		/// 	if(x.vkCode == KKey.Escape) { stop = true; x.BlockEvent(); }
		/// });
		/// dialog.show("hook");
		/// //or
		/// //wait.doEventsUntil(-10, () => stop); //wait max 10 s for Esc key
		/// //print.it("the end");
		/// ]]></code>
		/// </example>
		public static WindowsHook Keyboard(Action<HookData.Keyboard> hookProc, bool ignoreAuInjected = true, bool setNow = true)
			=> new(Api.WH_KEYBOARD_LL, hookProc, setNow, 0, ignoreAuInjected);

		/// <summary>
		/// Sets a low-level mouse hook (<b>WH_MOUSE_LL</b>).
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <returns>New <see cref="WindowsHook"/> object that manages the hook.</returns>
		/// <param name="hookProc">
		/// The hook procedure (function that handles hook events).
		/// Must return as soon as possible. More info: <see cref="LowLevelHooksTimeout"/>.
		/// If calls <see cref="HookData.Mouse.BlockEvent"/> or <see cref="HookData.ReplyMessage"/><c>(true)</c>, the event is not sent to apps and other hooks.
		/// Event data cannot be modified.
		/// <para>NOTE: When the hook procedure returns, the parameter variable becomes invalid and unsafe to use. If you need the data for later use, copy its properties and not whole variable.</para>
		/// </param>
		/// <param name="ignoreAuInjected">Don't call the hook procedure for events sent by functions of this library. Default <c>true</c>.</param>
		/// <param name="setNow">Set hook now. Default <c>true</c>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>
		/// <code><![CDATA[
		/// var stop = false;
		/// using var hook = WindowsHook.Mouse(x => {
		/// 	print.it(x);
		/// 	if(x.Event == HookData.MouseEvent.RightButton) { stop = x.IsButtonUp; x.BlockEvent(); }
		/// });
		/// dialog.show("hook");
		/// //or
		/// //wait.doEventsUntil(-10, () => stop); //wait max 10 s for right-click
		/// //print.it("the end");
		/// ]]></code>
		/// </example>
		public static WindowsHook Mouse(Action<HookData.Mouse> hookProc, bool ignoreAuInjected = true, bool setNow = true)
			=> new(Api.WH_MOUSE_LL, hookProc, setNow, 0, ignoreAuInjected);

		internal static WindowsHook MouseRaw_(Func<nint, nint, bool> hookProc, bool ignoreAuInjected = true, bool setNow = true)
			=> new(Api.WH_MOUSE_LL, hookProc, setNow, 0, ignoreAuInjected, "Mouse");

		/// <summary>
		/// Sets a <b>WH_CBT</b> hook for a thread of this process.
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <returns>New <see cref="WindowsHook"/> object that manages the hook.</returns>
		/// <param name="hookProc">
		/// Hook procedure (function that handles hook events).
		/// Must return as soon as possible.
		/// If returns <c>true</c>, the event is canceled. For some events you can modify some fields of event data.
		/// <para>NOTE: When the hook procedure returns, the parameter variable becomes invalid and unsafe to use. If you need the data for later use, copy its properties and not the variable.</para>
		/// </param>
		/// <param name="threadId">Native thread id, or 0 for this thread. The thread must belong to this process.</param>
		/// <param name="setNow">Set hook now. Default <c>true</c>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>
		/// <code><![CDATA[
		/// using var hook = WindowsHook.ThreadCbt(x => {
		/// 	print.it(x.code);
		/// 	switch(x.code) {
		/// 	case HookData.CbtEvent.ACTIVATE:
		/// 		print.it(x.Hwnd);
		/// 		break;
		/// 	case HookData.CbtEvent.CREATEWND:
		/// 		var c=x.CreationInfo->lpcs;
		/// 		print.it(x.Hwnd, c->x, c->lpszName);
		/// 		c->x=500;
		/// 		break;
		/// 	}
		/// 	return false;
		/// });
		/// dialog.showOkCancel("hook");
		/// //new Form().ShowDialog(); //to test MINMAX
		/// ]]></code>
		/// </example>
		public static WindowsHook ThreadCbt(Func<HookData.ThreadCbt, bool> hookProc, int threadId = 0, bool setNow = true)
			=> new(Api.WH_CBT, hookProc, setNow, threadId);

		/// <summary>
		/// Sets a <b>WH_GETMESSAGE</b> hook for a thread of this process.
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <returns>New <see cref="WindowsHook"/> object that manages the hook.</returns>
		/// <param name="hookProc">
		/// The hook procedure (function that handles hook events).
		/// Must return as soon as possible.
		/// The event cannot be canceled. As a workaround, you can set <c>msg->message=0</c>. Also can modify other fields.
		/// <para>NOTE: When the hook procedure returns, the pointer field of the parameter variable becomes invalid and unsafe to use.</para>
		/// </param>
		/// <param name="threadId">Native thread id, or 0 for this thread. The thread must belong to this process.</param>
		/// <param name="setNow">Set hook now. Default <c>true</c>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>
		/// <code><![CDATA[
		/// using var hook = WindowsHook.ThreadGetMessage(x => {
		/// 	print.it(x.msg->ToString(), x.PM_NOREMOVE);
		/// });
		/// dialog.show("hook");
		/// ]]></code>
		/// </example>
		public static WindowsHook ThreadGetMessage(Action<HookData.ThreadGetMessage> hookProc, int threadId = 0, bool setNow = true)
			=> new(Api.WH_GETMESSAGE, hookProc, setNow, threadId);

		/// <summary>
		/// Sets a <b>WH_GETMESSAGE</b> hook for a thread of this process.
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <returns>New <see cref="WindowsHook"/> object that manages the hook.</returns>
		/// <param name="hookProc">
		/// The hook procedure (function that handles hook events).
		/// Must return as soon as possible.
		/// If returns <c>true</c>, the event is canceled.
		/// </param>
		/// <param name="threadId">Native thread id, or 0 for this thread. The thread must belong to this process.</param>
		/// <param name="setNow">Set hook now. Default <c>true</c>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>
		/// <code><![CDATA[
		/// using var hook = WindowsHook.ThreadKeyboard(x => {
		/// 	print.it(x.key, 0 != (x.lParam & 0x80000000) ? "up" : "", x.lParam, x.PM_NOREMOVE);
		/// 	return false;
		/// });
		/// dialog.show("hook");
		/// ]]></code>
		/// </example>
		public static WindowsHook ThreadKeyboard(Func<HookData.ThreadKeyboard, bool> hookProc, int threadId = 0, bool setNow = true)
			=> new(Api.WH_KEYBOARD, hookProc, setNow, threadId);

		/// <summary>
		/// Sets a <b>WH_MOUSE</b> hook for a thread of this process.
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <returns>New <see cref="WindowsHook"/> object that manages the hook.</returns>
		/// <param name="hookProc">
		/// The hook procedure (function that handles hook events).
		/// Must return as soon as possible.
		/// If returns <c>true</c>, the event is canceled.
		/// <para>NOTE: When the hook procedure returns, the pointer field of the parameter variable becomes invalid and unsafe to use.</para>
		/// </param>
		/// <param name="threadId">Native thread id, or 0 for this thread. The thread must belong to this process.</param>
		/// <param name="setNow">Set hook now. Default <c>true</c>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>
		/// <code><![CDATA[
		/// using var hook = WindowsHook.ThreadMouse(x => {
		/// 	print.it(x.message, x.m->pt, x.m->hwnd, x.PM_NOREMOVE);
		/// 	return false;
		/// });
		/// dialog.show("hook");
		/// ]]></code>
		/// </example>
		public static WindowsHook ThreadMouse(Func<HookData.ThreadMouse, bool> hookProc, int threadId = 0, bool setNow = true)
			=> new(Api.WH_MOUSE, hookProc, setNow, threadId);

		/// <summary>
		/// Sets a <b>WH_CALLWNDPROC</b> hook for a thread of this process.
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <returns>A new <see cref="WindowsHook"/> object that manages the hook.</returns>
		/// <param name="hookProc">
		/// The hook procedure (function that handles hook events).
		/// Must return as soon as possible.
		/// The event cannot be canceled or modified.
		/// <para>NOTE: When the hook procedure returns, the pointer field of the parameter variable becomes invalid and unsafe to use.</para>
		/// </param>
		/// <param name="threadId">Native thread id, or 0 for this thread. The thread must belong to this process.</param>
		/// <param name="setNow">Set hook now. Default <c>true</c>.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <example>
		/// <code><![CDATA[
		/// using var hook = WindowsHook.ThreadCallWndProc(x => {
		/// 	ref var m = ref *x.msg;
		/// 	WndUtil.PrintMsg(out var s, m.hwnd, m.message, m.wParam, m.lParam);
		/// 	print.it(s, x.sentByOtherThread);
		/// });
		/// dialog.show("hook");
		/// ]]></code>
		/// </example>
		public static WindowsHook ThreadCallWndProc(Action<HookData.ThreadCallWndProc> hookProc, int threadId = 0, bool setNow = true)
			=> new(Api.WH_CALLWNDPROC, hookProc, setNow, threadId);

		/// <summary>
		/// Sets a <b>WH_CALLWNDPROCRET</b> hook for a thread of this process.
		/// See API <msdn>SetWindowsHookEx</msdn>.
		/// </summary>
		/// <inheritdoc cref="ThreadCallWndProc"/>
		public static WindowsHook ThreadCallWndProcRet(Action<HookData.ThreadCallWndProcRet> hookProc, int threadId = 0, bool setNow = true)
			=> new(Api.WH_CALLWNDPROCRET, hookProc, setNow, threadId);

		WindowsHook(int hookType, Delegate hookProc, bool setNow, int tid, bool ignoreAuInjected = false, [CallerMemberName] string m_ = null) {
			Not_.Null(hookProc);
			_proc2 = hookProc;
			_hookType = hookType;
			_hookTypeString = m_;
			_ignoreAuInjected = ignoreAuInjected;
			if (hookType is Api.WH_KEYBOARD_LL or Api.WH_MOUSE_LL) {
				_proc1 = _HookProcLL;
				//JIT-compile our hook proc and some functions it may call. OS gives us only 300 ms by default.
				if (!s_jit1) {
					s_jit1 = true;
					Jit_.Compile(typeof(WindowsHook), nameof(_HookProcLL));
					_ = perf.ms;
					_ = keys.KeyTypes_.IsMod(KKey.Shift) && _DontBlockMod;
				}
			} else {
				_proc1 = _HookProc;
			}
			if (setNow) Hook(tid);
			(t_antiGC ??= new()).Add(this);
		}
		static bool s_jit1;

		/// <summary>
		/// Sets the hook.
		/// </summary>
		/// <param name="threadId">If the hook type is a thread hook - thread id, or 0 for current thread. Else not used and must be 0.</param>
		/// <exception cref="AuException">Failed.</exception>
		/// <exception cref="InvalidOperationException">The hook is already set.</exception>
		/// <exception cref="ArgumentException"><i>threadId</i> not 0 and the hook type is not a thread hook.</exception>
		/// <remarks>
		/// Usually don't need to call this function, because the <b>WindowsHook</b> static methods that return a new <b>WindowsHook</b> object by default call it.
		/// </remarks>
		public void Hook(int threadId = 0) {
			if (_proc2 == null) throw new ObjectDisposedException(nameof(WindowsHook));
			if (_hh != default) throw new InvalidOperationException("The hook is already set.");
			if (_hookType is Api.WH_KEYBOARD_LL or Api.WH_MOUSE_LL) {
				if (threadId != 0) throw new ArgumentException("threadId must be 0");
			} else if (threadId == 0) {
				threadId = Api.GetCurrentThreadId();
			}
			_hh = Api.SetWindowsHookEx(_hookType, _proc1, default, threadId);
			if (_hh == default) throw new AuException(0, "*set hook");
		}

		/// <summary>
		/// Removes the hook.
		/// </summary>
		/// <remarks>
		/// Does nothing if already removed or wasn't set.
		/// Later you can call <see cref="Hook"/> to set hook again.
		/// Note: call <see cref="Dispose"/> instead if will not need to hook again.
		/// </remarks>
		public void Unhook() {
			if (_hh != default) {
				_Restore_UnhookOld();
				bool ok = Api.UnhookWindowsHookEx(_hh);
				if (!ok) print.warning($"WindowsHook.Unhook() failed ({_hookTypeString}). {lastError.message}");
				_hh = default;
			}
		}

		/// <summary>
		/// Rehooks this low-level keyboard or mouse hook.
		/// </summary>
		/// <remarks>
		/// Low level hooks may be occasionally disabled by the OS or other hooks. Workaround - call this function eg every 10 s in same thread. For example use <see cref="timer"/>. Don't call too frequently, eg every 1 s.
		/// This function unhooks current hook and sets new hook. Ensures that no events are missed or duplicate during it.
		/// </remarks>
		/// <exception cref="InvalidOperationException">The hook type isn't low-level keyboard or mouse.</exception>
		public void Restore() {
			if (_hookType is not (Api.WH_KEYBOARD_LL or Api.WH_MOUSE_LL)) throw new InvalidOperationException();
			if (_proc2 == null) throw new ObjectDisposedException(nameof(WindowsHook));

			//If we simply unhook/hook here, some events are missed.
			//	Restoring usually takes 0.2 - 0.5 ms. And it seems the new hook starts working with a delay.
			//	If restoring every 10 s, could miss maybe 1/10000 triggers.
			//	Tested: when restoring every 15 ms, missed 3/50 triggers.
			//	Solution: unhook the old hook after several ms. To avoid duplicate events, unhook it in the hook proc too.
#if false
			if (_hh != default) Api.UnhookWindowsHookEx(_hh);
			_hh = Api.SetWindowsHookEx(_hookType, _proc1, default, 0);
			if (_hh == default) throw new AuException(0, "*set hook");
#else
			_Restore_UnhookOld();
			var hh = Api.SetWindowsHookEx(_hookType, _proc1, default, 0);
			if (hh != default) {
				if (_hh != default) timer.after(10, _ => _Restore_UnhookOld());
				_oldHook = _hh;
				_hh = hh;
			} else {
				Debug_.Print("failed");
			}
#endif
		}
		IntPtr _oldHook;

		void _Restore_UnhookOld() {
			if (_oldHook != default) {
				bool ok = Api.UnhookWindowsHookEx(_oldHook);
				_oldHook = default;
				Debug_.PrintIf(!ok, "failed to unhook old");
			}
		}

		/// <summary>
		/// Returns <c>true</c> if the hook is set.
		/// </summary>
		public bool IsSet => _hh != default;

		///// <summary>
		///// Disable warning "Non-disposed WindowsHook variable".
		///// </summary>
		//public bool NoWarningNondisposed { get; set; }

		/// <summary>
		/// Calls <see cref="Unhook"/> and disposes this object.
		/// </summary>
		public void Dispose() {
			Unhook();
			_proc2 = null;
			t_antiGC.Remove(this);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Prints a warning if the variable is not disposed. Cannot dispose in finalizer.
		/// </summary>
		~WindowsHook() {
			//unhooking in finalizer thread makes no sense. Must unhook in same thread, else fails.
			if (_hh != default) print.warning($"Non-disposed WindowsHook ({_hookTypeString}) variable.");
			//ok if unhooked but not disposed. If we are here, the thread ended and therefore don't need to remove this from t_antiGC.
		}

		unsafe nint _HookProc(int code, nint wParam, nint lParam) {
			if (code >= 0) {
				try {
					bool eat = false;

					switch (_proc2) {
					case Func<HookData.ThreadCbt, bool> p:
						eat = p(new HookData.ThreadCbt(this, code, wParam, lParam));
						break;
					case Action<HookData.ThreadGetMessage> p:
						p(new HookData.ThreadGetMessage(this, wParam, lParam));
						break;
					case Func<HookData.ThreadKeyboard, bool> p:
						eat = p(new HookData.ThreadKeyboard(this, code, wParam, lParam));
						break;
					case Func<HookData.ThreadMouse, bool> p:
						eat = p(new HookData.ThreadMouse(this, code, wParam, lParam));
						break;
					case Action<HookData.ThreadCallWndProc> p:
						p(new HookData.ThreadCallWndProc(this, wParam, lParam));
						break;
					case Action<HookData.ThreadCallWndProcRet> p:
						p(new HookData.ThreadCallWndProcRet(this, wParam, lParam));
						break;
					}

					if (eat) return 1;
				}
				catch (Exception ex) { OnException_(ex); }
			}

			return Api.CallNextHookEx(default, code, wParam, lParam);
		}

		unsafe nint _HookProcLL(int code, nint wParam, nint lParam) {
			_Restore_UnhookOld();
			if (code >= 0) {
				try {
					//using var p1 = perf.local();
					bool eat = false;
					long t1 = 0;
					Action<HookData.Mouse> pm1;
					Func<nint, nint, bool> pm2;

					switch (_proc2) {
					case Action<HookData.Keyboard> p:
						var kll = (Api.KBDLLHOOKSTRUCT*)lParam;
						var vk = (KKey)kll->vkCode;
						if (kll->IsInjected) {
							if (kll->IsInjectedByAu) {
								if (kll->vkCode == 0) goto gr; //used to enable activating windows
								if (!kll->IsUp) Triggers.AutotextTriggers.ResetEverywhere = true;
								if (_ignoreAuInjected) goto gr;
							}
							if (vk == KKey.MouseX2 && kll->dwExtraInfo == 1354291109) goto gr; //QM2 sync code
						} else {
							//When keys.Internal_.ReleaseModAndCapsLock sends Shift to turn off CapsLock,
							//	hooks receive a non-injected LShift down, CapsLock down/up and injected LShift up.
							//	Our triggers would recover, but cannot auto-repeat. Better don't call the hookproc.
							if ((vk == KKey.CapsLock || vk == KKey.LShift) && _ignoreAuInjected && _IgnoreLShiftCaps) goto gr;

							//Test how our triggers recover when a modifier down or up event is lost. Or when triggers started while a modifier is down.
							//if(keys.isScrollLock) {
							//	//if(vk == KKey.LCtrl && !kll->IsUp) { print.it("lost Ctrl down"); goto gr; }
							//	if(vk == KKey.LCtrl && kll->IsUp) { print.it("lost Ctrl up"); goto gr; }
							//}
						}
						//if (keys.KeyTypes_.IsMod(vk) && _DontBlockMod) goto gr; //old version, creates problems
						t1 = perf.ms;
						//p1.Next();
						p(new HookData.Keyboard(this, lParam)); //info: wParam is message, but it is not useful, everything is in lParam
						if (eat = kll->BlockEvent) {
							kll->BlockEvent = false;
							if (keys.KeyTypes_.IsMod(vk) && _DontBlockMod && kll->IsUp) eat = false;
						}
						break;
					case Action<HookData.Mouse> p:
						pm1 = p; pm2 = null;
						gm1:
						var mll = (Api.MSLLHOOKSTRUCT*)lParam;
						switch ((int)wParam) {
						case Api.WM_LBUTTONDOWN: case Api.WM_RBUTTONDOWN: Triggers.AutotextTriggers.ResetEverywhere = true; break;
						}
						if (_ignoreAuInjected && mll->IsInjectedByAu) goto gr;

						//API bug workaround. In DPI-scaled windows on click mhsLL->pt is logical, although on move/wheel is physical. Must be always physical.
						//At first noticed only on Win10. But then noticed the same on Win7, although used to be correct. OK on Win8.1. Maybe depends on some other conditions, eg UAC IL, DPI, multimonitor.
						//Now it seems the bug is fixed on Win10. Found on SO: "Microsoft fixed it in 10.0.14393"; it is version 1607, August 2, 2016; but I cannot confirm it.
						//The wrong coords are the same as GetCursorPos. Only GetPhysicalCursorPos does not lie. Api.GetCursorPos is mapped to GetPhysicalCursorPos.
						//Note: on WM_MOUSEMOVE Get[Physical]CursorPos returns previous coords. On other messages same as hook.
						if (wParam != Api.WM_MOUSEMOVE /*&& osVersion.winVer < osVersion.win10*/) Api.GetCursorPos(out mll->pt);

						t1 = perf.ms;
						if (pm2 != null) {
							eat = pm2(wParam, lParam);
						} else {
							pm1(new HookData.Mouse(this, wParam, lParam));
							if (eat = mll->BlockEvent) mll->BlockEvent = false;
						}
						break;
					case Func<nint, nint, bool> p: //raw mouse
						pm2 = p; pm1 = null;
						goto gm1;
					}

					//Prevent Windows disabling the low-level key/mouse hook.
					//	Hook proc must return in HKEY_CURRENT_USER\Control Panel\Desktop:LowLevelHooksTimeout ms.
					//		Default 300. On Win10 max 1000 (bigger registry value is ignored and used 1000).
					//	On timeout Windows:
					//		1. Does not wait more. Passes the message to the next hook etc, and we cannot return 1 to block it.
					//		2. Kills the hook after several such cases. Usually 6 keys or 11 mouse events.
					//		3. Makes the hook useless: next times does not wait for it, and we cannot return 1 to block the event.
					//	Somehow does not apply 2 and 3 to some apps, eg C# apps created by Visual Studio, although applies to those created not by VS. I did not find why.
					if (t1 != 0 && (t1 = perf.ms - t1) > 200 && !Debugger.IsAttached) {
						if (t1 > LowLevelHooksTimeout - 50) {
							var s1 = _hookType == Api.WH_KEYBOARD_LL ? "key" : "mouse";
							var s2 = eat ? $" On timeout the {s1} message is passed to the active window, other hooks, etc." : null;
							//print.warning($"Possible hook timeout. Hook procedure time: {t1} ms. LowLevelHooksTimeout: {LowLevelHooksTimeout} ms.{s2}"); //too slow first time
							//print.it($"Warning: Possible hook timeout. Hook procedure time: {t1} ms. LowLevelHooksTimeout: {LowLevelHooksTimeout} ms.{s2}\r\n{new StackTrace(0, false)}"); //first Write() JIT 30 ms
							ThreadPool.QueueUserWorkItem(s3 => print.it(s3), $"Warning: Possible hook timeout. Hook procedure time: {t1} ms. LowLevelHooksTimeout: {LowLevelHooksTimeout} ms.{s2}\r\n{new StackTrace(0, false)}"); //fast if with false. But async print can be confusing.
						}
						//FUTURE: print warning if t1 is >25 frequently. Unhook and don't rehook if >LowLevelHooksTimeout frequently.

						Unhook();
						_hh = Api.SetWindowsHookEx(_hookType, _proc1, default, 0);
					}

					if (eat) return 1;
				}
				catch (Exception ex) { OnException_(ex); }
			}
			gr:
			return Api.CallNextHookEx(default, code, wParam, lParam);
		}

		/// <summary>
		/// Gets the max time in milliseconds allowed by Windows for low-level keyboard and mouse hook procedures.
		/// </summary>
		/// <remarks>
		/// Gets registry value <c>HKEY_CURRENT_USER\Control Panel\Desktop:LowLevelHooksTimeout</c>. If it is missing, returns 300; it is the default value used by Windows. If greater than 1000, returns 1000, because Windows 10 ignores bigger values.
		/// 
		/// If a hook procedure takes more time, Windows does not wait. Then its return value is ignored, and the event is passed to other apps, hooks, etc. After several such cases Windows may fully or partially disable the hook. This class detects such cases; then restores the hook and prints a warning. If the warning is rare, you can ignore it. If frequent, it means your hook procedure is too slow.
		/// 
		/// Callback functions of keyboard and mouse triggers are called in a hook procedure, therefore must be as fast as possible. More info: <see cref="Triggers.TriggerFuncs"/>.
		/// 
		/// More info: <msdn>registry LowLevelHooksTimeout</msdn>.
		/// 
		/// Note: After changing the timeout in registry, it is not applied immediately. Need to log off/on.
		/// </remarks>
		public static int LowLevelHooksTimeout {
			get {
				if (s_lowLevelHooksTimeout == 0) {
					//default 300, tested on Win10 and 7
					//max 1000 on Win10. On Win7 more. Not tested on Win8. On Win7/8 may be changed by a Windows update.
					s_lowLevelHooksTimeout =
						Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "LowLevelHooksTimeout", null) is int v
						? (int)Math.Min(1000u, (uint)v)
						: 300;
				}
				return s_lowLevelHooksTimeout;
			}
			internal set {
				int v = Math.Clamp(value, 0, 5000);
				Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "LowLevelHooksTimeout", v);
				s_lowLevelHooksTimeout = v;
			}
		}
		static int s_lowLevelHooksTimeout;

		internal static void OnException_(Exception e) {
			print.warning("Unhandled exception in hook procedure. " + e.ToString());
		}

		[StructLayout(LayoutKind.Sequential, Size = 32)] //note: this struct is in shared memory. Size must be same in all library versions.
		internal struct SharedMemoryData_ {
			public long dontBlockModUntil, dontBlocLShiftCapsUntil;
			//16 bytes reserved
		}

		/// <summary>
		/// Let other hooks (in all processes) don't block modifier key up events for <i>timeMS</i> milliseconds. If 0 - restore.
		/// Used by mouse triggers waiting for mod keys released, to prevent inputblockers blocking mod up events, eg when sending keys/text.
		/// Returns the timeout time (<c>Environment.TickCount64 + timeMS</c>) or 0.
		/// </summary>
		internal unsafe long DontBlockModInOtherHooks_(long timeMS) {
			_ignoreModExceptThisHook = timeMS > 0;
			var r = _ignoreModExceptThisHook ? Environment.TickCount64 + timeMS : 0;
			SharedMemory_.Ptr->winHook.dontBlockModUntil = r;
			return r;
		}

		unsafe bool _DontBlockMod => SharedMemory_.Ptr->winHook.dontBlockModUntil > Environment.TickCount64 && !_ignoreModExceptThisHook;
		bool _ignoreModExceptThisHook;

		/// <summary>
		/// Let all hooks (in all processes) ignore LShift and CapsLock for <i>timeMS</i> milliseconds. If 0 - restore.
		/// Returns the timeout time (<c>Environment.TickCount64 + timeMS</c>) or 0.
		/// Used when turning off CapsLock with Shift.
		/// </summary>
		internal static unsafe long IgnoreLShiftCaps_(long timeMS) {
			var r = timeMS > 0 ? Environment.TickCount64 + timeMS : 0;
			SharedMemory_.Ptr->winHook.dontBlocLShiftCapsUntil = r;
			return r;
		}

		static unsafe bool _IgnoreLShiftCaps => SharedMemory_.Ptr->winHook.dontBlocLShiftCapsUntil > Environment.TickCount64;
	}
}

namespace Au.Types {
	/// <summary>
	/// Contains types of hook data for hook procedures set by <see cref="WindowsHook"/> and <see cref="WinEventHook"/>.
	/// </summary>
	public static partial class HookData {
		/// <summary>
		/// Event data for the hook procedure set by <see cref="WindowsHook.Keyboard"/>.
		/// More info: API <msdn>LowLevelKeyboardProc</msdn>.
		/// </summary>
		public unsafe struct Keyboard {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			readonly Api.KBDLLHOOKSTRUCT* _x;

			internal Keyboard(WindowsHook hook, nint lParam) {
				this.hook = hook;
				_x = (Api.KBDLLHOOKSTRUCT*)lParam;
			}

			/// <summary>
			/// Call this function to steal this event from other hooks and apps.
			/// </summary>
			public void BlockEvent() => _x->BlockEvent = true;

			/// <summary>
			/// Is extended key.
			/// </summary>
			public bool IsExtended => 0 != (_x->flags & Api.LLKHF_EXTENDED);

			/// <summary>
			/// <c>true</c> if the event was generated by API such as <msdn>SendInput</msdn>.
			/// <c>false</c> if the event was generated by the keyboard.
			/// </summary>
			public bool IsInjected => 0 != (_x->flags & Api.LLKHF_INJECTED);

			/// <summary>
			/// <c>true</c> if the event was generated by functions of this library.
			/// </summary>
			public bool IsInjectedByAu => 0 != (_x->flags & Api.LLKHF_INJECTED) && _x->dwExtraInfo == Api.AuExtraInfo;

			/// <summary>
			/// Key Alt is pressed.
			/// </summary>
			public bool IsAlt => 0 != (_x->flags & Api.LLKHF_ALTDOWN);

			/// <summary>
			/// Is key-up event.
			/// </summary>
			public bool IsUp => 0 != (_x->flags & Api.LLKHF_UP);

			/// <summary>
			/// If the key is a modifier key (Shift, Ctrl, Alt, Win), returns the modifier flag. Else returns 0.
			/// </summary>
			public KMod Mod => keys.Internal_.KeyToMod((KKey)_x->vkCode);

			/// <summary>
			/// If <b>vkCode</b> is a left or right modifier key code (LShift, LCtrl, LAlt, RShift, RCtrl, RAlt, RWin), returns the common modifier key code (Shift, Ctrl, Alt, Win). Else returns <b>vkCode</b>.
			/// </summary>
			public KKey Key {
				get {
					var vk = (KKey)_x->vkCode;
					switch (vk) {
					case KKey.LShift: case KKey.RShift: return KKey.Shift;
					case KKey.LCtrl: case KKey.RCtrl: return KKey.Ctrl;
					case KKey.LAlt: case KKey.RAlt: return KKey.Alt;
					case KKey.RWin: return KKey.Win;
					}
					return vk;
				}
			}

			/// <summary>
			/// Returns <c>true</c> if <i>key</i> == <b>vkCode</b> or <i>key</i> is Shift, Ctrl, Alt or Win and <b>vkCode</b> is LShift/RShift, LCtrl/RCtrl, LAlt/RAlt or RWin.
			/// </summary>
			public bool IsKey(KKey key) {
				var vk = (KKey)_x->vkCode;
				if (key == vk) return true;
				switch (key) {
				case KKey.Shift: return vk == KKey.LShift || vk == KKey.RShift;
				case KKey.Ctrl: return vk == KKey.LCtrl || vk == KKey.RCtrl;
				case KKey.Alt: return vk == KKey.LAlt || vk == KKey.RAlt;
				case KKey.Win: return vk == KKey.RWin;
				}
				return false;
			}

			/// <summary>
			/// Converts flags to API SendInput flags KEYEVENTF_KEYUP and KEYEVENTF_EXTENDEDKEY.
			/// </summary>
			internal byte SendInputFlags_ {
				get {
					uint f = 0;
					if (IsUp) f |= Api.KEYEVENTF_KEYUP;
					if (IsExtended) f |= Api.KEYEVENTF_EXTENDEDKEY;
					return (byte)f;
				}
			}

			///
			public override string ToString() {
				return $"{vkCode.ToString()} {(IsUp ? "up" : "")}{(IsInjected ? " (injected)" : "")}";
			}

			/// <summary>API <msdn>KBDLLHOOKSTRUCT</msdn></summary>
			public KKey vkCode => (KKey)_x->vkCode;
			/// <summary>API <msdn>KBDLLHOOKSTRUCT</msdn></summary>
			public uint scanCode => _x->scanCode;
			/// <summary>API <msdn>KBDLLHOOKSTRUCT</msdn></summary>
			public uint flags => _x->flags;
			/// <summary>API <msdn>KBDLLHOOKSTRUCT</msdn></summary>
			public int time => _x->time;
			/// <summary>API <msdn>KBDLLHOOKSTRUCT</msdn></summary>
			public nint dwExtraInfo => _x->dwExtraInfo;

			internal Api.KBDLLHOOKSTRUCT* NativeStructPtr_ => _x;
		}

		/// <summary>
		/// Extra info value used by functions of this library that generate keyboard events. Low-level hooks receive it in <b>dwExtraInfo</b>.
		/// </summary>
		public const int AuExtraInfo = Api.AuExtraInfo;

		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WindowsHook.Mouse"/>.
		/// More info: API <msdn>LowLevelMouseProc</msdn>.
		/// </summary>
		public unsafe struct Mouse {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			readonly Api.MSLLHOOKSTRUCT* _x;
			readonly MouseEvent _event;

			internal Mouse(WindowsHook hook, nint wParam, nint lParam) {
				this.hook = hook;
				var p = (Api.MSLLHOOKSTRUCT*)lParam;
				_x = p;
				int e = (int)wParam;
				switch (e) {
				case Api.WM_MOUSEMOVE: IsMove = true; break;
				case Api.WM_LBUTTONDOWN: case Api.WM_RBUTTONDOWN: case Api.WM_MBUTTONDOWN: IsButtonDown = true; break;
				case Api.WM_LBUTTONUP: case Api.WM_RBUTTONUP: case Api.WM_MBUTTONUP: e--; IsButtonUp = true; break;
				case Api.WM_XBUTTONUP: e--; IsButtonUp = true; goto g1;
				case Api.WM_XBUTTONDOWN:
					IsButtonDown = true;
					g1:
					switch (p->mouseData >> 16) { case 1: e |= 0x1000; break; case 2: e |= 0x2000; break; }
					break;
				case Api.WM_MOUSEWHEEL:
				case Api.WM_MOUSEHWHEEL:
					IsWheel = true;
					int wheel = (short)(p->mouseData >> 16);
					if (wheel > 0) e |= 0x1000; else if (wheel < 0) e |= 0x2000;
					WheelValue = wheel;
					break;
				}
				_event = (MouseEvent)e;
			}

			/// <summary>
			/// Call this function to steal this event from other hooks and apps.
			/// </summary>
			public void BlockEvent() => _x->BlockEvent = true;

			/// <summary>
			/// What event it is (button, move, wheel).
			/// </summary>
			public MouseEvent Event => _event;

			/// <summary>
			/// Is mouse-move event.
			/// </summary>
			public bool IsMove { get; }

			/// <summary>
			/// Is button-down event.
			/// </summary>
			public bool IsButtonDown { get; }

			/// <summary>
			/// Is button-up event.
			/// </summary>
			public bool IsButtonUp { get; }

			/// <summary>
			/// Is button event (down or up).
			/// </summary>
			public bool IsButton => IsButtonDown | IsButtonUp;

			/// <summary>
			/// Converts <see cref="Event"/> to <see cref="MButton"/>.
			/// </summary>
			/// <value><b>Left</b>, <b>Right</b>, <b>Middle</b>, <b>X1</b>, <b>X2</b> or 0. Thow down/up/double flags not used.</value>
			public MButton Button {
				get {
					return _event switch {
						MouseEvent.LeftButton => MButton.Left,
						MouseEvent.RightButton => MButton.Right,
						MouseEvent.MiddleButton => MButton.Middle,
						MouseEvent.X1Button => MButton.X1,
						MouseEvent.X2Button => MButton.X2,
						_ => 0,
					};
				}
			}

			/// <summary>
			/// Is wheel event.
			/// </summary>
			public bool IsWheel { get; }

			/// <summary>
			/// <c>true</c> if the event was generated by API such as <msdn>SendInput</msdn>.
			/// <c>false</c> if the event was generated by the mouse.
			/// </summary>
			public bool IsInjected => 0 != (flags & Api.LLMHF_INJECTED);

			/// <summary>
			/// <c>true</c> if the event was generated by functions of this library.
			/// </summary>
			public bool IsInjectedByAu => IsInjected && dwExtraInfo == Api.AuExtraInfo;

			/// <summary>
			/// Wheel rotation amount, 120 for 1 full tick. Negative if backward.
			/// Usually 120 or -120, but some devices or software may produce smaller or bigger values.
			/// </summary>
			public int WheelValue { get; }

			///
			public override string ToString() {
				var ud = ""; if (IsButtonDown) ud = "down"; else if (IsButtonUp) ud = "up";
				return $"{Event.ToString()} {ud} {pt.ToString()}{(IsInjected ? " (injected)" : "")}";
			}

			/// <summary>API <msdn>MSLLHOOKSTRUCT</msdn></summary>
			public POINT pt => _x->pt;
			/// <summary>API <msdn>MSLLHOOKSTRUCT</msdn></summary>
			public uint mouseData => _x->mouseData;
			/// <summary>API <msdn>MSLLHOOKSTRUCT</msdn></summary>
			public uint flags => _x->flags;
			/// <summary>API <msdn>MSLLHOOKSTRUCT</msdn></summary>
			public int time => _x->time;
			/// <summary>API <msdn>MSLLHOOKSTRUCT</msdn></summary>
			public nint dwExtraInfo => _x->dwExtraInfo;

			internal Api.MSLLHOOKSTRUCT* NativeStructPtr_ => _x;
		}

		/// <summary>
		/// Mouse hook event types. See <see cref="Mouse.Event"/>.
		/// </summary>
		public enum MouseEvent {
#pragma warning disable 1591 //no XML doc
			Move = 0x0200, //WM_MOUSEMOVE
			LeftButton = 0x0201, //WM_LBUTTONDOWN
			RightButton = 0x0204, //WM_RBUTTONDOWN
			MiddleButton = 0x0207, //WM_MBUTTONDOWN
			X1Button = 0x120B, //WM_XBUTTONDOWN | 0x1000
			X2Button = 0x220B, //WM_XBUTTONDOWN | 0x2000
			WheelForward = 0x120A, //WM_WHEEL | 0x1000
			WheelBackward = 0x220A, //WM_WHEEL | 0x2000
			WheelRight = 0x120E, //WM_HWHEEL | 0x1000
			WheelLeft = 0x220E, //WM_HWHEEL | 0x2000
#pragma warning restore 1591
		}

		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WindowsHook.ThreadCbt"/>.
		/// More info: API <msdn>CBTProc</msdn>.
		/// </summary>
		public struct ThreadCbt {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			/// <summary>API <msdn>CBTProc</msdn></summary>
			public readonly CbtEvent code;

			/// <summary>API <msdn>CBTProc</msdn></summary>
			public readonly nint wParam;

			/// <summary>API <msdn>CBTProc</msdn></summary>
			public readonly nint lParam;

			/// <summary>Window handle.</summary>
			public wnd Hwnd => code switch { CbtEvent.ACTIVATE or CbtEvent.CREATEWND or CbtEvent.DESTROYWND or CbtEvent.MINMAX or CbtEvent.MOVESIZE or CbtEvent.SETFOCUS => (wnd)wParam, _ => default };

			internal ThreadCbt(WindowsHook hook, int code, nint wParam, nint lParam) {
				this.hook = hook;
				this.code = (CbtEvent)code;
				this.wParam = wParam;
				this.lParam = lParam;
			}

			/// <summary>
			/// Gets <see cref="CbtEvent.ACTIVATE"/> event info.
			/// </summary>
			/// <exception cref="InvalidOperationException"><b>code</b> is not <b>CbtEvent.ACTIVATE</b>.</exception>
			public unsafe CBTACTIVATESTRUCT* ActivationInfo => code == CbtEvent.ACTIVATE ? (CBTACTIVATESTRUCT*)lParam : throw new InvalidOperationException();

			/// <summary>
			/// API <msdn>CBTACTIVATESTRUCT</msdn>.
			/// </summary>
			public struct CBTACTIVATESTRUCT {
				///
				public bool fMouse;
				///
				public wnd hWndActive;
			}

			/// <summary>
			/// Gets <see cref="CbtEvent.CREATEWND"/> event info.
			/// You can modify <b>x</b>, <b>y</b>, <b>cx</b>, <b>cy</b>, and <b>hwndInsertAfter</b>.
			/// </summary>
			/// <exception cref="InvalidOperationException"><b>code</b> is not <b>CbtEvent.CREATEWND</b>.</exception>
			public unsafe CBT_CREATEWND* CreationInfo => code == CbtEvent.CREATEWND ? (CBT_CREATEWND*)lParam : throw new InvalidOperationException();

			/// <summary>
			/// API <msdn>CBT_CREATEWND</msdn>.
			/// </summary>
			public unsafe struct CBT_CREATEWND {
				///
				public CREATESTRUCT* lpcs;
				///
				public wnd hwndInsertAfter;
			}

			//rejected. Rarely used or too simple.
			///// <summary>
			///// Gets <see cref="CbtEvent.CLICKSKIPPED"/> event info. Returns the mouse message.
			///// </summary>
			///// <param name="m"><msdn>MOUSEHOOKSTRUCT</msdn>.</param>
			///// <exception cref="InvalidOperationException"><b>code</b> is not CbtEvent.CLICKSKIPPED.</exception>
			//public unsafe uint MouseInfo(out MOUSEHOOKSTRUCT* m) {
			//	if (code != CbtEvent.CLICKSKIPPED) throw new InvalidOperationException();
			//	m = (MOUSEHOOKSTRUCT*)lParam;
			//	return (uint)wParam;
			//}

			///// <summary>
			///// Gets <see cref="CbtEvent.KEYSKIPPED"/> event info. Returns the key code.
			///// </summary>
			///// <param name="lParam"><i>lParam</i> of the key message. Specifies the repeat count, scan code, etc. See API <msdn>WM_KEYDOWN</msdn>.</param>
			///// <exception cref="InvalidOperationException"><b>code</b> is not CbtEvent.KEYSKIPPED.</exception>
			//public KKey KeyInfo(out uint lParam) {
			//	if (code != CbtEvent.KEYSKIPPED) throw new InvalidOperationException();
			//	lParam = (uint)this.lParam;
			//	return (KKey)(uint)wParam;
			//}

			///// <summary>
			///// Gets <see cref="CbtEvent.SETFOCUS"/> event info. Returns the window handle.
			///// </summary>
			///// <param name="wLostFocus">The previously focused window, or <c>default(wnd)</c>.</param>
			///// <exception cref="InvalidOperationException"><b>code</b> is not CbtEvent.SETFOCUS.</exception>
			//public wnd FocusInfo(out wnd wLostFocus) {
			//	if (code != CbtEvent.SETFOCUS) throw new InvalidOperationException();
			//	wLostFocus = (wnd)lParam;
			//	return (wnd)wParam;
			//}

			///// <summary>
			///// Gets <see cref="CbtEvent.MOVESIZE"/> event info.
			///// </summary>
			///// <exception cref="InvalidOperationException"><b>code</b> is not CbtEvent.MOVESIZE.</exception>
			//public unsafe RECT* MoveSizeInfo => code == CbtEvent.MOVESIZE ? (RECT*)lParam : throw new InvalidOperationException();

			///// <summary>
			///// Gets <see cref="CbtEvent.MINMAX"/> event info.
			///// Returns the new show state. See API <msdn>ShowWindow</msdn>. Minimized 6, maximized 3, restored 9.
			///// <exception cref="InvalidOperationException"><b>code</b> is not CbtEvent.MINMAX.</exception>
			//public int MinMaxInfo => code == CbtEvent.MINMAX ? (int)lParam & 0xffff : throw new InvalidOperationException();
		}

		/// <summary>
		/// CBT hook event types. Used with <see cref="ThreadCbt"/>.
		/// More info: API <msdn>CBTProc</msdn>.
		/// </summary>
		public enum CbtEvent {
#pragma warning disable 1591 //no XML doc
			MOVESIZE = 0,
			MINMAX = 1,
			//QS = 2,
			CREATEWND = 3,
			DESTROYWND = 4,
			ACTIVATE = 5,
			CLICKSKIPPED = 6,
			KEYSKIPPED = 7,
			SYSCOMMAND = 8,
			SETFOCUS = 9,
#pragma warning restore 1591
		}

		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WindowsHook.ThreadGetMessage"/>.
		/// More info: API <msdn>GetMsgProc</msdn>.
		/// </summary>
		public unsafe struct ThreadGetMessage {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			/// <summary>
			/// The message has not been removed from the queue, because called API <msdn>PeekMessage</msdn> with flag <b>PM_NOREMOVE</b>.
			/// </summary>
			public readonly bool PM_NOREMOVE;

			/// <summary>
			/// Message parameters.
			/// API <msdn>MSG</msdn>.
			/// </summary>
			public readonly MSG* msg;

			internal ThreadGetMessage(WindowsHook hook, nint wParam, nint lParam) {
				this.hook = hook;
				PM_NOREMOVE = (uint)wParam == Api.PM_NOREMOVE;
				msg = (MSG*)lParam;
			}
		}

		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WindowsHook.ThreadKeyboard"/>.
		/// More info: API <msdn>KeyboardProc</msdn>.
		/// </summary>
		public struct ThreadKeyboard {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			/// <summary>
			/// The message has not been removed from the queue, because called API <msdn>PeekMessage</msdn> with flag <b>PM_NOREMOVE</b>.
			/// </summary>
			public readonly bool PM_NOREMOVE;

			/// <summary>
			/// The key code.
			/// </summary>
			public readonly KKey key;

			/// <summary>
			/// <i>lParam</i> of the key message. Specifies the key state, scan code, etc. See API <msdn>KeyboardProc</msdn>.
			/// </summary>
			public readonly uint lParam;

			/// <summary>
			/// Is key-up event.
			/// </summary>
			public bool IsUp => 0 != (lParam & 0x80000000);

			internal ThreadKeyboard(WindowsHook hook, int code, nint wParam, nint lParam) {
				this.hook = hook;
				PM_NOREMOVE = code == Api.HC_NOREMOVE;
				key = (KKey)(uint)wParam;
				this.lParam = (uint)lParam;
			}
		}

		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WindowsHook.ThreadMouse"/>.
		/// More info: API <msdn>MouseProc</msdn>.
		/// </summary>
		public unsafe struct ThreadMouse {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			/// <summary>
			/// The message has not been removed from the queue, because called API <msdn>PeekMessage</msdn> with flag <b>PM_NOREMOVE</b>.
			/// </summary>
			public readonly bool PM_NOREMOVE;

			/// <summary>
			/// The mouse message, for example <b>WM_MOUSEMOVE</b>.
			/// </summary>
			public readonly uint message;

			/// <summary>
			/// More info about the mouse message.
			/// API <msdn>MOUSEHOOKSTRUCT</msdn>.
			/// </summary>
			public readonly MOUSEHOOKSTRUCT* m;

			internal ThreadMouse(WindowsHook hook, int code, nint wParam, nint lParam) {
				this.hook = hook;
				PM_NOREMOVE = code == Api.HC_NOREMOVE;
				message = (uint)wParam;
				m = (MOUSEHOOKSTRUCT*)lParam;
			}
		}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		/// <summary>API <msdn>MOUSEHOOKSTRUCT</msdn></summary>
		public struct MOUSEHOOKSTRUCT {
			public POINT pt;
			public wnd hwnd;
			public int wHitTestCode;
			public nint dwExtraInfo;
		}

		/// <summary>API <msdn>CWPSTRUCT</msdn></summary>
		public struct CWPSTRUCT {
			public nint lParam;
			public nint wParam;
			public int message;
			public wnd hwnd;
		}

		/// <summary>API <msdn>CWPRETSTRUCT</msdn></summary>
		public struct CWPRETSTRUCT {
			public nint lResult;
			public nint lParam;
			public nint wParam;
			public int message;
			public wnd hwnd;
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WindowsHook.ThreadCallWndProc"/>.
		/// More info: API <msdn>CallWndProc</msdn>.
		/// </summary>
		public unsafe struct ThreadCallWndProc {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			/// <summary>
			/// True if the message was sent by another thread.
			/// </summary>
			public readonly bool sentByOtherThread; //note: incorrect info in MSDN

			/// <summary>
			/// Message parameters.
			/// API <msdn>CWPSTRUCT</msdn>.
			/// </summary>
			public readonly CWPSTRUCT* msg;

			internal ThreadCallWndProc(WindowsHook hook, nint wParam, nint lParam) {
				this.hook = hook;
				sentByOtherThread = 0 != wParam;
				msg = (CWPSTRUCT*)lParam;
			}
		}

		/// <summary>
		/// Hook data for the hook procedure set by <see cref="WindowsHook.ThreadCallWndProcRet"/>.
		/// More info: API <msdn>CallWndRetProc</msdn>.
		/// </summary>
		public unsafe struct ThreadCallWndProcRet {
			/// <summary>The caller object of your hook procedure. For example can be used to unhook.</summary>
			public readonly WindowsHook hook;

			/// <summary>
			/// True if the message was sent by another thread.
			/// </summary>
			public readonly bool sentByOtherThread; //note: incorrect info in MSDN

			/// <summary>
			/// Message parameters and the return value.
			/// API <msdn>CWPRETSTRUCT</msdn>.
			/// </summary>
			public readonly CWPRETSTRUCT* msg;

			internal ThreadCallWndProcRet(WindowsHook hook, nint wParam, nint lParam) {
				this.hook = hook;
				sentByOtherThread = 0 != wParam;
				msg = (CWPRETSTRUCT*)lParam;
			}
		}

		/// <summary>
		/// Calls API API <msdn>ReplyMessage</msdn>, which allows to use <see cref="elm"/> and COM in the hook procedure.
		/// </summary>
		/// <param name="cancelEvent">
		/// Don't notify the target window about the event, and don't call other hook procedures.
		/// This value is used instead of the return value of the hook procedure, which is ignored.
		/// </param>
		/// <remarks>
		/// It can be used as a workaround for this problem: in low-level hook procedure some functions don't work with some windows. For example cannot get a UI element or use a COM object. Error/exception "An outgoing call cannot be made since the application is dispatching an input-synchronous call (0x8001010D)".
		/// </remarks>
		public static void ReplyMessage(bool cancelEvent) => Api.ReplyMessage(cancelEvent ? 1 : 0);
	}
}
