//Key/mouse/autotext triggers use low-level keyboard and mouse hooks. The hooks are in a separate thread, because:
//	1. Safer when user code is slow or incorrect.
//	2. Works well with COM. In LL hook procedure some COM functions fail, eg find elm with some windows.
//		Error "An outgoing call cannot be made since the application is dispatching an input-synchronous call". Like when using SendMessage for IPC.
//		It is important because scripts often use UI elements in scope context functions etc that run in the main thread.

//Low-level key/mouse hooks also have other problems, but not too big:
//	1. UAC. Hooks of non-admin processes don't work when an admin window is active.
//		Workaround: let users don't use triggers in non-admin processes. Editor normally is admin, and by default creates task processes of same UAC IL.
//	2. Scripts may use raw input or directX, and Windows has this bug: then low-level keyboard hook does not work in that process.
//		Workaround: let users run such scripts in separate processes.
//	3. Each LL hook uses CPU on key/mouse events.
//		Workaround: let users put all triggers in single script. But it's OK to use another script temporarily eg for testing.
//		Actually in real conditions even 10 hooks don't use a significant part of CPU compared to CPU used by the target app and OS.
//Rejected: single hook server in editor process. It would mitigate some of these problems. Tested. Much code and little benefit.

//For window triggers we use winevent hooks. They use less CPU for IPC.
//	Tested: renaming a captionless toolwindow every 1-2 ms in loop:
//		CPU usage with no winevent hooks is 4%. With 1 hook - 7%. With 10 hooks in different processes - 7%.

namespace Au.Triggers;

/// <summary>
/// Thread containing low-level keyboard and mouse hooks.
/// </summary>
class HooksThread : IDisposable {
	[Flags]
	public enum UsedEvents {
		Keyboard = 1, //Hotkey and Autotext triggers
		Mouse = 2, //Mouse and Autotext triggers. Just sets the hook and resets autotext; to receive events, also add other MouseX flags.
		MouseClick = 0x10, //Mouse click triggers
		MouseWheel = 0x20, //Mouse wheel triggers
		MouseEdgeMove = 0x40, //Mouse edge and move triggers
	}
	
	int _tid;
	UsedEvents _usedEvents;
	wnd _wMsg;
	MouseTriggers.EdgeMoveDetector_ _emDetector;
	Handle_ _eventStartStop = Api.CreateEvent(false);
	
	public HooksThread(UsedEvents usedEvents, wnd wMsg) {
		_usedEvents = usedEvents;
		_wMsg = wMsg;
		run.thread(_Thread, sta: false); //important: not STA, because we use lock, which dispatches sent messages if STA
		Api.WaitForSingleObject(_eventStartStop, -1);
	}
	
	public void Dispose() {
		Api.PostThreadMessage(_tid, Api.WM_QUIT, 0, 0);
		Api.WaitForSingleObject(_eventStartStop, -1);
		_eventStartStop.Dispose();
		_eventSendData.Dispose();
	}
	
	void _Thread() {
		_tid = Api.GetCurrentThreadId();
		
		WindowsHook hookK = null, hookM = null;
		if (_usedEvents.Has(UsedEvents.Keyboard)) {
			hookK = WindowsHook.Keyboard(_KeyboardHookProc); //note: if lambda, very slow JIT on first hook event
		}
		if (_usedEvents.Has(UsedEvents.Mouse)) {
			hookM = WindowsHook.MouseRaw_(_MouseHookProc);
		}
		if (_usedEvents.Has(UsedEvents.MouseEdgeMove)) {
			_emDetector = new MouseTriggers.EdgeMoveDetector_();
		}
		//tested: don't need JIT-compiling.
		
		nint idTimer = (hookK != null || hookM != null) ? Api.SetTimer(default, 0, 10_000, null) : 0;
		
		Api.SetEvent(_eventStartStop);
		
		while (Api.GetMessage(out var m)) {
			if (m.message == Api.WM_TIMER && m.wParam == idTimer) {
				if (Debugger.IsAttached) continue;
				hookK?.Restore();
				hookM?.Restore();
				continue;
			}
			Api.DispatchMessage(m);
		}
		
		//print.it("hooks thread ended");
		hookK?.Dispose();
		hookM?.Dispose();
		_emDetector = null;
		Api.SetEvent(_eventStartStop);
	}
	
	unsafe void _KeyboardHookProc(HookData.Keyboard k) {
		_keyData = *k.NativeStructPtr_;
		if (_Send(UsedEvents.Keyboard)) k.BlockEvent();
	}
	
	unsafe bool _MouseHookProc(nint wParam, nint lParam) {
		int msg = (int)wParam;
		if (msg == Api.WM_MOUSEMOVE) {
			if (_usedEvents.Has(UsedEvents.MouseEdgeMove)) {
				var mll = (Api.MSLLHOOKSTRUCT*)lParam;
				if (_emDetector.Detect(mll->pt)) {
					_emData = _emDetector.result;
					_Send(UsedEvents.MouseEdgeMove);
				}
			}
		} else {
			bool wheel = msg is Api.WM_MOUSEWHEEL or Api.WM_MOUSEHWHEEL;
			if ((_usedEvents.Has(UsedEvents.MouseWheel) && wheel) || (_usedEvents.Has(UsedEvents.MouseClick) && !wheel)) {
				_mouseMessage = (int)wParam;
				_mouseData = *(Api.MSLLHOOKSTRUCT*)lParam;
				return _Send(wheel ? UsedEvents.MouseWheel : UsedEvents.MouseClick);
			}
		}
		return false;
	}
	
	/// <summary>
	/// Sends key/mouse event data (copied to _keyData etc) to the main thread.
	/// Returns <c>true</c> to eat (block, discard) the event.
	/// On 1100 ms timeout returns <c>false</c>.
	/// </summary>
	bool _Send(UsedEvents eventType) {
		//using var p1 = perf.local();
		bool ok = _wMsg.SendNotify(Api.WM_USER + 1, _messageId, (int)eventType);
		bool timeout = Api.WaitForSingleObject(_eventSendData, 1100) == Api.WAIT_TIMEOUT;
		lock (this) {
			if (timeout) timeout = Api.WaitForSingleObject(_eventSendData, 0) == Api.WAIT_TIMEOUT; //other thread may SetEvent between WaitForSingleObject and lock
			_messageId++;
			return _eat && !timeout;
		}
		//info: HookWin._HookProcLL will print warning if > LowLevelHooksTimeout-50. Max LowLevelHooksTimeout is 1000.
	}
	
	//fields for passing key/mouse event data to the main thread and getting its return value
	Handle_ _eventSendData = Api.CreateEvent(false); //sync
	int _messageId; //sync
	bool _eat; //return value
	Api.KBDLLHOOKSTRUCT _keyData;
	Api.MSLLHOOKSTRUCT _mouseData;
	int _mouseMessage;
	MouseTriggers.EdgeMoveDetector_.Result _emData;
	
	/// <summary>
	/// Called by the main thread to resume the hooks thread (_Send) and pass the return value (eat).
	/// Returns <c>false</c> on timeout.
	/// </summary>
	public bool Return(int messageId, bool eat) {
		lock (this) {
			if (messageId != _messageId) return false;
			_eat = eat;
			Api.SetEvent(_eventSendData);
		}
		return true;
	}
	
	/// <summary>
	/// Called by the main thread to get key event data sent by _Send.
	/// Returns <c>false</c> on timeout.
	/// </summary>
	public bool GetKeyData(int messageId, out Api.KBDLLHOOKSTRUCT data) {
		data = _keyData;
		return messageId == _messageId;
	}
	
	/// <summary>
	/// Called by the main thread to get mouse click/wheel event data sent by _Send.
	/// Returns <c>false</c> on timeout.
	/// </summary>
	public bool GetClickWheelData(int messageId, out Api.MSLLHOOKSTRUCT data, out int message) {
		data = _mouseData;
		message = _mouseMessage;
		return messageId == _messageId;
	}
	
	/// <summary>
	/// Called by the main thread to get mouse edge/move event data sent by _Send.
	/// Returns <c>false</c> on timeout.
	/// </summary>
	public bool GetEdgeMoveData(int messageId, out MouseTriggers.EdgeMoveDetector_.Result data) {
		data = _emData;
		return messageId == _messageId;
	}
}
