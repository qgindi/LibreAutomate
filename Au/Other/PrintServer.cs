//#define NEED_CALLER //rejected. Too slow and generates much garbage.

/*
Single global server is supported in this user session.
Single local server is supported in this process.

Server does not implement an output window etc. It just collects messages and notifies an output window. Asynchronously.

How global output server/client is implemented:
	Single server and multiple clients.
	Server receives messages sent by clients.
	Clients - processes that send text messages to the server. The server's process also can be client.
	For IPC is used mailslot, waitable timer and shared memory (SM).
	Server:
		Creates mailslot and timer. Sets a bool about it in SM.
		Waits for timer and reads messages from mailslot.
			For better reliability, also checks mailslot periodically.
			If messages available, notifies an output window.
		On exit clears the bool in SM.
	Client, when sending a text message:
		If the SM bool is not set - discards the message, and closes mailslot if was open. Else:
		If not still done, opens mailslot.
		Writes message to mailslot.
		Sets timer if not set. Uses another bool in SM to indicate that the timer is set; server clears it.

How local output server/client is implemented:
	Similar to global. Differences:
	Single server and single client (the same process).
	Uses waitable timer, but not mailslot/SM. Instead of mailslot, adds messages directly to _messages. Instead of SM, uses static variables.

*/

namespace Au.More {
	/// <summary>
	/// Receives messages sent by <see cref="print.it"/>.
	/// </summary>
	/// <remarks>
	/// If server is global, clients can be multiple processes, including this. Else only this process.
	/// Works asynchronously, to make writing messages faster.
	/// When a client writes a message, the message arrives to the server with some delay and is placed in a queue.
	/// You then can get/remove messages from the queue (call <see cref="GetMessage"/>) and display them in a window (for example).
	/// You can be notified about new messages.
	/// 
	/// Recommended setup (see example):
	/// 1. When your application starts, create a <b>PrintServer</b> instance and assign to a static variable. Call <see cref="Start"/>.
	/// 2. When your application creates its output window, call <see cref="SetNotifications"/> to set window/message for notifications.
	/// 3. In window procedure, when received the notification message, get/remove/display all new output messages.
	/// 4. Call <see cref="Stop"/> when closing the window.
	/// </remarks>
	/// <example>
	/// Simple program with output window.
	/// <code><![CDATA[
	/// using System.Windows.Forms;
	/// 
	/// class OutputFormExample : Form {
	/// 	TextBox _tb;
	/// 
	/// 	public OutputFormExample()
	/// 	{
	/// 		_tb = new TextBox();
	/// 		_tb.ReadOnly = true;
	/// 		_tb.Multiline = true;
	/// 		_tb.ScrollBars = ScrollBars.Both;
	/// 		_tb.WordWrap = false;
	/// 		_tb.Dock = DockStyle.Fill;
	/// 		_tb.TabStop = false;
	/// 		this.Controls.Add(_tb);
	/// 	}
	/// 
	/// 	protected override void OnHandleCreated(EventArgs e) {
	/// 		_os.SetNotifications(this.Hwnd(), WM_APP);
	/// 		base.OnHandleCreated(e);
	/// 	}
	/// 
	/// 	protected override void WndProc(ref Message m) {
	/// 		if(m.Msg == WM_APP) _ProcessMessages();
	/// 		base.WndProc(ref m);
	/// 	}
	/// 	
	/// 	internal const int WM_APP = 0x8000;
	/// 
	/// 	void _ProcessMessages()
	/// 	{
	/// 		while(_os.GetMessage(out var m)) {
	/// 			switch(m.Type) {
	/// 			case PrintServerMessageType.Clear:
	/// 				_tb.Clear();
	/// 				break;
	/// 			case PrintServerMessageType.Write:
	/// 				//_tb.AppendText(m.Text);
	/// 				_tb.AppendText($"{DateTime.FromFileTimeUtc(m.TimeUtc).ToLocalTime()}  {m.Caller}  {m.Text}");
	/// 				break;
	/// 			}
	/// 		}
	/// 	}
	/// 
	/// 	static PrintServer _os = new(isGlobal: false);
	/// 
	/// 	[STAThread]
	/// 	static void Main()
	/// 	{
	/// 		_os.Start();
	/// 
	/// 		//test Write and Clear, before and after creating window
	/// 		print.ignoreConsole = true;
	/// 		print.it("test before setting notifications");
	/// 		Task.Run(() => { 1.s(); print.it("test after"); 1.s(); print.clear(); 1.s(); print.it("test after Clear"); });
	/// 
	/// 		Application.Run(new OutputFormExample());
	/// 		_os.Stop();
	/// 	}
	/// }
	/// ]]></code>
	/// </example>
	public unsafe class PrintServer {
		//info:
		//Although global and local servers are implemented quite differently, the interface is almost the same. For this and other reasons I decided to use single class.
		//For local server, the thread and kernel timer would be not necessary. Instead could use just a user timer. But it has some limitations etc.
		
		readonly ConcurrentQueue<PrintServerMessage> _messages = new(); //all received and still not removed messages that were sent by clients when they call print.it etc
		Handle_ _mailslot; //used if global
		WaitableTimer _timer; //used always
		wnd _notifWnd;
		int _notifMsg;
		bool _isStarted;
		readonly bool _isGlobal;
		bool _isLocalTimer;
		
		/// <param name="isGlobal">
		/// If <c>true</c>, will receive output from all processes that don't have local server.
		/// </param>
		public PrintServer(bool isGlobal) => _isGlobal = isGlobal;
		
		/// <summary>
		/// Starts server.
		/// </summary>
		/// <returns><c>false</c> if server already exists (if global - in any process).</returns>
		/// <exception cref="AuException">Failed.</exception>
		public bool Start() {
			lock (this) {
				if (print.s_localServer != null) return false;
				if (_isGlobal) {
					var m = Api.CreateMailslot(MailslotName_, 0, 0, Api.SECURITY_ATTRIBUTES.ForLowIL);
					if (m.Is0) {
						var e = lastError.code;
						if (e == Api.ERROR_ALREADY_EXISTS) return false; //called not first time, or exists in another process
						throw new AuException(e, "*create mailslot");
					}
					
					_mailslot = m;
					_CreateTimerAndThread();
					SM_->isServer = 1;
				} else {
					_CreateTimerAndThread();
				}
				
				print.s_localServer = this; //if global server, will work as local when writes same process
				_isStarted = true;
			}
			return true;
		}
		
		void _CreateTimerAndThread() {
			try {
				if (_isGlobal) _timer = WaitableTimer.Create(false, TimerName_);
				else _timer = WaitableTimer.Create();
				
				run.thread(_Thread, sta: false).Name = "Au.PrintServer";
			}
			catch {
				if (_isGlobal) _mailslot.Dispose();
				_timer?.Close(); _timer = null;
				throw;
			}
		}
		
		/// <summary>
		/// Stops server.
		/// </summary>
		public void Stop() {
			lock (this) {
				if (!_isStarted) return;
				_isStarted = false;
				if (_isGlobal) {
					_mailslot.Dispose();
					SM_->isServer = 0;
				}
				print.s_localServer = null;
				_timer?.Set(0); //break thread loop; use minimal time. //info: the thread will dispose _timer and set=null
			}
		}
		
		/// <summary>
		/// Calls <b>Stop</b>.
		/// </summary>
		~PrintServer() => Stop();
		
		/// <summary>
		/// Sets window/message to be notified about server events.
		/// </summary>
		/// <param name="w">Your window that displays output, or any other window. Its window procedure on <i>message</i> should call <see cref="GetMessage"/> until it returns <c>false</c>. See example in class help.</param>
		/// <param name="message">Windows message to send to <i>w</i> when one or more output events are available. For example <b>WM_USER</b> or <b>WM_APP</b>.</param>
		public void SetNotifications(wnd w, int message) {
			_notifMsg = message;
			_notifWnd = w;
			if (!w.Is0) _timer?.Set(30);
		}
		
		void _Thread() {
			try {
				for (int period = 1000; ;) {
					bool isTimerEvent = _timer.WaitOne(period); //true if timer event, false if timeout
					if (isTimerEvent) {
						if (_isGlobal) SM_->isTimer = 0;
						_isLocalTimer = false;
					}
					
					lock (this) {
						if (!_isStarted) {
							_timer.Dispose(); _timer = null;
							break;
						}
						
						if (_isGlobal) { //read messages from mailslot and add to _messages. Else messages are added directly to _messages.
							while (Api.GetMailslotInfo(_mailslot, null, out int nextSize, out var msgCount) && msgCount > 0) {
								//note: GetMailslotInfo makes Process Hacker show constant 24 B/s I/O total rate. Does not depend on period.
								_ReadMailslotMessage(nextSize);
								if (msgCount == 1) break;
							}
						}
					}
					
					if (!_notifWnd.Is0 && !_messages.IsEmpty) {
						
						//print.qm2.write($"{_messages.Count}, {_ToMB(_memSize)}, {_ToMB(GC.GetTotalMemory(false))}");
						
						if (!_notifWnd.IsAlive) break;
						_notifWnd.Send(_notifMsg);
					}
					
					if (isTimerEvent) period = 50; //check after 50 ms, to avoid 1000 ms delay in case a client did not set timer because SM_->isTimer was still 1 although the timer was already signaled
					else period = 1000; //check every 1000 ms, for full reliability
					
					//Console.WriteLine($"{period}");
				}
			}
			catch (Exception ex) {
				Debug_.Dialog(ex);
			}
		}
		
		[SkipLocalsInit]
		void _ReadMailslotMessage(int size) {
			using FastBuffer<byte> b = new(size + 4); //+4 for "\r\n"
			var p = b.p;
			bool ok = Api.ReadFile(_mailslot, p, size, out var readSize) && readSize == size;
			if (ok) {
				long time = 0; string s = null, caller = null;
				var mtype = (PrintServerMessageType)(*p++);
				switch (mtype) {
				case PrintServerMessageType.Write or PrintServerMessageType.TaskEvent:
					if (size < 10) { ok = false; break; } //type, time(8), lenCaller
					time = *(long*)p; p += 8;
					int lenCaller = *p++;
					if (lenCaller > 0) {
						if (10 + lenCaller * 2 > size) { ok = false; break; }
						caller = new string((char*)p, 0, lenCaller);
						p += lenCaller * 2;
					}
					int len = (size - (int)(p - b.p)) / 2;
					if (!NoNewline && mtype == PrintServerMessageType.Write) {
						char* r = (char*)(b.p + size);
						r[0] = '\r'; r[1] = '\n';
						len += 2;
					}
					s = new string((char*)p, 0, len);
					break;
				case PrintServerMessageType.Clear or PrintServerMessageType.ScrollToTop when size == 1:
					break;
				default:
					ok = false;
					break;
				}
				Debug.Assert(ok);
				if (ok) _AddMessage(new PrintServerMessage(mtype, s, time, caller));
			}
		}
		
		//static string _ToMB(long n) => Math.Round(n / 1048576d, 3).ToS();
		
		/// <summary>
		/// Adds <i>s</i> directly to <b>_messages</b> and sets timer.
		/// If <i>s</i> is <c>null</c>, it is "Clear" command.
		/// Else if <c>!NoNewline</c>, appends <c>"\r\n"</c>.
		/// Used with local server; also with global server when writes the server's process.
		/// </summary>
		internal void LocalWrite_(string s, long time = 0, string caller = null) {
			//Debug.Assert(!_isGlobal);
			if (!NoNewline && s != null) s += "\r\n";
			var m = new PrintServerMessage(s == null ? PrintServerMessageType.Clear : PrintServerMessageType.Write, s, time, caller);
			_AddMessage(m, true);
		}
		
		/// <summary>
		/// Adds action directly to <b>_messages</b> and sets timer.
		/// Used with local server; also with global server when writes the server's process.
		/// </summary>
		internal void LocalAction_(PrintServerMessageType action) {
			//Debug.Assert(!_isGlobal);
			_AddMessage(new(action), true);
		}
		
		void _AddMessage(PrintServerMessage m, bool setTimer = false) {
			//_memSize += _GetMessageMemorySize(m);
			_messages.Enqueue(m);
			if (setTimer && !_isLocalTimer) { _timer?.Set(10); _isLocalTimer = true; }
		}
		
		//static int _GetMessageMemorySize(PrintServerMessage m) => 50 + m.Text.Lenn() * 2;
		//int _memSize;
		
		/// <summary>
		/// Gets next message and removes from the queue.
		/// </summary>
		/// <returns><c>false</c> if there are no messages.</returns>
		/// <remarks>
		/// Messages are added to an internal queue when clients call <see cref="print.it"/> etc. They contain the text, time, etc. This function gets the oldest message and removes it from the queue.
		/// </remarks>
		public bool GetMessage(out PrintServerMessage m) {
			if (!_messages.TryDequeue(out m)) return false;
			//_memSize -= _GetMessageMemorySize(m);
			return true;
		}
		
		/// <summary>
		/// Gets the count of messages in the queue.
		/// </summary>
		public int MessageCount => _messages.Count;
		
		/// <summary>
		/// Let messages don't end with <c>"\r\n"</c>.
		/// </summary>
		/// <remarks>
		/// This can be used for performance, to avoid string copying when using local server. Does not affect performance of global server.
		/// </remarks>
		public bool NoNewline { get; set; }
		
#if NEED_CALLER
		/// <summary>
		/// Let clients provide the caller method of "print" functions.
		/// Note: It makes these methods much slower, especially when thread stack is big. Also generates much garbage. To find caller method is used <see cref="StackTrace"/> class.
		/// See also: <see cref="IntroduceWriterClass"/>.
		/// </summary>
		public bool NeedCallerMethod
		{
			get => _isGlobal ? (SM_->needCaller != 0) : _localNeedCaller;
			set { if(_isGlobal) SM_->needCaller = (byte)(value ? 1 : 0); else _localNeedCaller = value; }
		}
		bool _localNeedCaller;

		internal static bool NeedCallerMethod_
		{
			get { var t = s_localServer; return (t != null) ? t.NeedCallerMethod : SM_->needCaller != 0; }
		}
#endif
		
		/// <summary>
		/// Gets mailslot name like <c>@"\\.\mailslot\Au.print\" + sessionId</c>.
		/// </summary>
		internal static string MailslotName_ {
			get {
				if (_mailslotName == null) {
					_mailslotName = @"\\.\mailslot\Au.print\" + process.thisProcessSessionId.ToString();
				}
				return _mailslotName;
			}
		}
		static string _mailslotName;
		
		/// <summary>
		/// Gets waitable timer name like <c>"timer.Au.print"</c>.
		/// </summary>
		internal static string TimerName_ => "timer.Au.print";
		
		/// <summary>
		/// Shared memory variables. Used with global server only.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, Size = 16)] //note: this struct is in shared memory. Size must be same in all library versions.
		internal struct SharedMemoryData_ {
			public byte isServer, isTimer;
#if NEED_CALLER
				public byte needCaller;
#endif
		}
		internal static SharedMemoryData_* SM_ => &SharedMemory_.Ptr->outp;
	}
}

namespace Au {
	public static partial class print {
		
		[MethodImpl(MethodImplOptions.NoInlining)] //for stack trace
		static void _ServerWrite(string s) {
			Debug.Assert(s != null);
			
			Api.GetSystemTimeAsFileTime(out var time);
			
			string caller = script.name;
#if NEED_CALLER
			if(PrintServer.NeedCallerMethod_) {
				//info: this func always called from directly, which is usually called through Writer, Write. But it is public and can be called directly.
				var k = new StackTrace(2); //skip this func and directly()
				lock(_writerTypes) {
					for(int i = 0, n = k.FrameCount; i < n; i++) {
						var m = k.GetFrame(i).GetMethod();
						var t = m.DeclaringType;
						if(_writerTypes.Contains(t)) continue;
						caller = caller + ":" + t.Name + "." + m.Name;
						break;
					}
				}
				//speed: with 'new StackFrame(i)' usually slower, regardless of stack size. Faster only when 1 loop, maybe 2.
				//info: here we don't optimize caller strings like PrintServer does, because StackTrace creates much more garbage.
			}
#endif
			
			var loc = s_localServer;
			if (loc != null) loc.LocalWrite_(s, time, caller);
			else s_client.WriteLine(s, PrintServerMessageType.Write, caller, time);
		}
		
		static void _ServerAction(PrintServerMessageType action) {
			var loc = s_localServer;
			if (loc == null) s_client.AddAction(action);
			else if (action == PrintServerMessageType.Clear) loc.LocalWrite_(null);
			else loc.LocalAction_(action);
		}
		
		static readonly _ClientOfGlobalServer s_client = new();
		internal static PrintServer s_localServer; //null if we don't have a local server
		
		/// <summary>
		/// Logs start/end/fail events of <b>miniProgram</b> trigger actions.
		/// Editor displays it in the <b>Recent tasks</b> window, not in the output panel.
		/// Could also log other events. For example at first used for task start/end/fail events, but now it is implemented in editor.
		/// </summary>
		internal static void TaskEvent_(string s, long id, string sourceFile = null, int sourceLine = 0) {
			Debug.Assert(script.role == SRole.MiniProgram);
			//if (s == null) s = "\0DNl08ISh30Kbt6ekJV3VvA"; //JIT //now not used
			//else {
			if (sourceFile == null) sourceFile = script.s_idMainFile.ToString(); //task started/ended/failed
			else sourceFile = sourceFile + "\0" + sourceLine.ToS(); //trigger action started/ended/failed
			sourceFile = id.ToS() + "\0" + sourceFile;
			//}
			s_client.WriteLine(s, PrintServerMessageType.TaskEvent, sourceFile);
		}
		
		unsafe class _ClientOfGlobalServer {
			//info: the mailslot/timer are implicitly disposed when process ends.
			
			Handle_ _mailslot;
			WaitableTimer _timer;
			long _sizeWritten;
			
			[SkipLocalsInit]
			public void WriteLine(string s, PrintServerMessageType mtype, string caller = null, long time = 0) {
				if (time == 0) Api.GetSystemTimeAsFileTime(out time);
				
				lock (_lockObj1) {
					if (!_Connect()) return;
					
					int lenS = s.Length, lenCaller = (caller != null) ? Math.Min(caller.Length, 255) : 0;
					int lenAll = 1 + 8 + 1 + lenCaller * 2 + lenS * 2; //type, time, lenCaller, caller, s
					using FastBuffer<byte> b = new(lenAll);
					byte* p = b.p;
					//type
					*p++ = (byte)mtype;
					//time
					*(long*)p = time; p += 8;
					//caller
					*p++ = (byte)lenCaller;
					if (lenCaller != 0) {
						fixed (char* k = caller) MemoryUtil.Copy(k, p, lenCaller * 2);
						p += lenCaller * 2;
					}
					//s
					if (lenS != 0) fixed (char* k = s) MemoryUtil.Copy(k, p, lenS * 2); //s
					
					//if (s == "\0DNl08ISh30Kbt6ekJV3VvA") { //JIT //now not used
					//									   //_SetTimer();
					//	Jit_.Compile(typeof(WaitableTimer), nameof(WaitableTimer.Set));
					//	Jit_.Compile(typeof(Api), nameof(Api.WriteFile), nameof(Api.SetWaitableTimer)); //slow JIT SetWaitableTimer
					//	return;
					//}
					
					g1:
					bool ok = Api.WriteFile(_mailslot, b.p, lenAll, out _);
					if (!ok && _ReopenMailslot()) goto g1;
					
					if (ok) {
						_SetTimer();
						
						//prevent overflow of mailslot and _messages
						_sizeWritten += lenAll;
						if (_sizeWritten > 1_000_000) {
							while (Api.GetFileSizeEx(_mailslot, out _sizeWritten) && _sizeWritten > 300_000) Thread.Sleep(15);
							//note: these numbers are carefully adjusted for best performance etc
						}
					}
				}
			}
			
			public void AddAction(PrintServerMessageType action) {
				lock (_lockObj1) {
					if (!_Connect()) return;
					
					g1:
					byte b = (byte)action;
					bool ok = Api.WriteFile(_mailslot, &b, 1, out _);
					if (!ok && _ReopenMailslot()) goto g1;
					Debug.Assert(ok);
					
					if (ok) _SetTimer();
				}
			}
			
			//If last error says that server's mailslot closed, closes client's mailsot/timer and tries to reopen. If reopened, returns true.
			bool _ReopenMailslot() {
				if (lastError.code == Api.ERROR_HANDLE_EOF) { //server's mailslot closed
					_Close();
					if (_Connect()) return true;
				} else {
					Debug.Assert(false);
				}
				return false;
			}
			
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void _SetTimer() {
				if (PrintServer.SM_->isTimer == 0) {
					if (_timer.Set(10)) PrintServer.SM_->isTimer = 1;
				}
			}
			
			void _Close() {
				if (!_mailslot.Is0) {
					_mailslot.Dispose();
					_timer.Close(); _timer = null;
				}
			}
			
			bool _Connect() {
				if (PrintServer.SM_->isServer == 0) {
					_Close();
					return false;
				}
				
				if (_mailslot.Is0) {
					_mailslot = CreateFile_(PrintServer.MailslotName_, true);
					if (_mailslot.Is0) return false;
					
					_timer = WaitableTimer.Open(PrintServer.TimerName_, noException: true);
					if (_timer == null) {
						_mailslot.Dispose();
						return false;
					}
				}
				
				return true;
			}
		}
		
#if NEED_CALLER
		static readonly List<Type> _writerTypes = new List<Type>() { typeof(print), typeof(_OutputWriter) };

		/// <summary>
		/// Introduces a class that contain methods designed to write to the output.
		/// Purpose - when server's <see cref="PrintServer.NeedCallerMethod"/> is <c>true</c>, skip methods of this class when searching for the caller method in the call stack.
		/// For example, if you created class <b>PrintColored</b> that contains methods <b>PrintRed</b>, <b>PrintGreen</b> and <b>PrintBlue</b>, you should execute this code in its static constructor: <c>print.introduceWriterClass(typeof(PrintColored));</c>.
		/// Also use this if you redirect output using a writer class that calls directly().
		/// Not used when writing to console or log file.
		/// </summary>
		public static void introduceWriterClass(Type t)
		{
			lock(_writerTypes) {
				if(!_writerTypes.Contains(t)) _writerTypes.Add(t);
			}
		}
#endif
	}
}

namespace Au.Types {
	/// <summary>
	/// See <see cref="PrintServerMessage.Type"/>.
	/// </summary>
	public enum PrintServerMessageType {
		/// <summary>
		/// Add line to the output window.
		/// All <see cref="PrintServerMessage"/> members can be used.
		/// </summary>
		Write,
		
		/// <summary>
		/// Clear the output window.
		/// Only <see cref="PrintServerMessage.Type"/> is used.
		/// </summary>
		Clear,
		
		/// <summary>
		/// Used internally to log events such as start/end of trigger actions.
		/// </summary>
		TaskEvent,
		
		/// <summary>
		/// Scroll the output window to the top.
		/// Only <see cref="PrintServerMessage.Type"/> is used.
		/// </summary>
		ScrollToTop,
	}
	
	/// <summary>
	/// Contains message text and/or related info.
	/// More info: <see cref="PrintServer"/>, <see cref="PrintServer.GetMessage"/>.
	/// </summary>
	public class PrintServerMessage {
		/// <summary>
		/// Message type (write, clear, etc).
		/// </summary>
		public PrintServerMessageType Type { get; }
		
		/// <summary>
		/// Message text.
		/// Used with <see cref="PrintServerMessageType.Write"/>.
		/// </summary>
		public string Text { get; set; }
		
		/// <summary>
		/// Message time in <b>FILETIME</b> format, UTC.
		/// Used with <see cref="PrintServerMessageType.Write"/>.
		/// To convert to string: <c>DateTime.FromFileTimeUtc(m.TimeUtc).ToLocalTime().ToString()</c>.
		/// </summary>
		public long TimeUtc { get; }
		
#if NEED_CALLER
		/// <summary>
		/// The <see cref="script.name"/> property value of the process that called <see cref="print.it"/>.
		/// Used with <see cref="PrintServerMessageType.Write"/>.
		/// If <see cref="NeedCallerMethod"/> is <c>true</c>, also includes the caller method. Format: <c>"scriptname:type.method"</c>.
		/// </summary>
		public string Caller { get; }

		internal PrintServerMessage(PrintServerMessageType type, string text, long time, string caller)
		{
			Type = type;
			Text = text;
			TimeUtc = time;
			Caller = caller;
		}
#else
		/// <summary>
		/// The <see cref="script.name"/> property value of the process that called <see cref="print.it"/>.
		/// Used with <see cref="PrintServerMessageType.Write"/>.
		/// </summary>
		public string Caller { get; }
		
		internal PrintServerMessage(PrintServerMessageType type, string text = null, long time = 0, string caller = null) {
			Type = type;
			Text = text;
			TimeUtc = time;
			Caller = caller;
		}
#endif
		
		///
		public override string ToString() {
			//in editor used for output history
			
			if (Type != PrintServerMessageType.Write) return "";
			var k = DateTime.FromFileTimeUtc(TimeUtc).ToLocalTime();
			return $"{k.ToString()}  |  {Caller}\r\n{Text}";
		}
	}
}
