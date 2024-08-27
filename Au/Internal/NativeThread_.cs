namespace Au.More;

/// <summary>
/// Represents a thread created using API <b>CreateThread</b>.
/// Use when need thread handle/id ASAP, or to call APC easier.
/// </summary>
unsafe class NativeThread_ {
	nint _handle;
	int _tid;
	bool _sta, _threadInited;
	Action _proc;
	nint _initEvent; //much faster than with ManualResetEvent or ManualResetEventSlim

	~NativeThread_() {
		Api.CloseHandle(_handle);
		if (_initEvent != 0) Api.CloseHandle(_initEvent);
	}

	/// <summary>
	/// Starts new background thread using API <b>CreateThread</b>.
	/// <para>Note: in thread proc use <see cref="OfThisThread"/>, not a static field inited like <c>s_thread = new NativeThread_(...);</c>, because <i>s_thread</i> may be still null.</para>
	/// </summary>
	/// <param name="proc">Thread procedure.</param>
	/// <param name="sta">Set <b>ApartmentState.STA</b>.</param>
	/// <param name="waitInited">Wait now until the thread procedure calls <see cref="ThreadInited"/>.</param>
	public NativeThread_(Action proc, bool sta = true, bool waitInited = false) {
		_proc = proc;
		_sta = sta;
		if (waitInited) _initEvent = Api.CreateEvent2(default, true, false, null);
		_handle = Api.CreateThread(default, 0, &_Thread, GCHandle.Alloc(this), 0, out _tid);
		if (waitInited) {
			Api.WaitForSingleObject(_initEvent, -1);
			Api.CloseHandle(_initEvent);
			_initEvent = 0;
		}
	}

	/// <summary>
	/// Gets thread handle.
	/// The finalizer closes the handle. The object is not garbage-collected while the thread procedure is running.
	/// </summary>
	public nint Handle => _handle;

	/// <summary>
	/// Gets thread id.
	/// </summary>
	public nint Id => _tid;

	public static NativeThread_ OfThisThread => t_ofThisThread;
	[ThreadStatic] static NativeThread_ t_ofThisThread;

	[UnmanagedCallersOnly]
	static uint _Thread(GCHandle param) {
		var t = param.Target as NativeThread_;
		param.Free();
		t_ofThisThread = t;
		if (t._sta) Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
		t._proc();
		return 0;
	}

	/// <summary>
	/// The thread procedure must call this when finished thread initialization and going to run an alertable message loop.
	/// If constructor was called with <i>waitInited</i> <c>true</c>, it will return (stop waiting).
	/// If actions were queued, executes them now.
	/// Example: <c>NativeThread_.OfThisThread.ThreadInited();</c>
	/// Note: don't use code like <c>s_thread.ThreadInited();</c>, because <i>s_thread</i> (inited like <c>s_thread = new NativeThread_(...);</c>) may be still null.
	/// </summary>
	public void ThreadInited() {
		if (_initEvent != 0) Api.SetEvent(_initEvent);
		_threadInited = true;
		if (t_queue is { } q) {
			t_queue = null;
			q.Invoke();
		}
	}

	public void QueueAPC(Action a) {
		Api.QueueUserAPC(&_Apc, _handle, GCHandle.Alloc(a));
	}
	
	[ThreadStatic] static Action t_queue;
	
	[UnmanagedCallersOnly]
	static void _Apc(GCHandle param) {
		var a = param.Target as Action;
		param.Free();
		if (t_ofThisThread is {  } t && t._threadInited) a(); else t_queue += a;
	}

	public static void AlertableMessageLoop() {
		for (; ; ) {
			var k = Api.MsgWaitForMultipleObjectsEx(0, null, -1, Api.QS_ALLINPUT, Api.MWMO_ALERTABLE | Api.MWMO_INPUTAVAILABLE);
			if (k == 0) {
				if (!wait.doEvents()) break;
			} else if (k != Api.WAIT_IO_COMPLETION) {
				Debug_.Print(k);
				break;
			}
		}
	}
}
