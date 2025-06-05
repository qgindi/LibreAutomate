//From netcoredbg we need only netcoredbg.exe, dbgshim.dll, ManagedPart.dll, Microsoft.CodeAnalysis.dll and Microsoft.CodeAnalysis.CSharp.dll.
//	The default netcoredbg also contains 2 unused Roslyn scripting dlls.
//	Also these Roslyn dlls are very old.
//	Now debugger files are in the Debugger folder. Need just netcoredbg.exe, dbgshim.dll and ManagedPart.dll. They use the new Roslyn dlls.
//	Using modified netcoredbg and ManagedPart.
//	Using newer dbgshim.dll: https://www.nuget.org/packages/Microsoft.Diagnostics.DbgShim.win-x64 and Microsoft.Diagnostics.DbgShim.win-arm64
//	More info in netcoredbg solution > readme.md.

partial class PanelDebug {
	class _Debugger {
		consoleProcess _p;
		FileStream _fs;
		Action<string> _events;
		bool _ignoreEvents;
		Action _readEvents;
		
		public _Debugger(Action<string> events) {
			_events = events;
			_readEvents = _ReadEvents;
		}
		
		public bool Init(bool arm64) {
			//var log = @"C:\Test\debugger-log.txt"; filesystem.delete(log); Environment.SetEnvironmentVariable("LOG_OUTPUT", log);
			_p = new($@"{folders.ThisAppBS}Debugger\{(arm64 ? "arm64" : "x64")}\netcoredbg.exe", $"--interpreter=mi");
			
			if (SendSync(0, $"-handshake") != "^done") { //waits until the debugger is ready to process commands. Then we can measure the speed of other sync commands at startup.
				_Print("Failed to start debugger.");
				return false;
			}
			
			_fs = new(new Microsoft.Win32.SafeHandles.SafeFileHandle(_p.OutputHandle_, ownsHandle: false), FileAccess.Read);
			timer.after(200, _ => _readEvents());
			
			return true;
		}
		
		void _ReadEvents() {
			//print.it(">>>");
			while (!_ignoreEvents && _p != null) {
				int canRead = _p.CanReadNow_;
				if (canRead == 0) break;
				if (canRead < 0 || !_p.ReadLine(out var k)) {
					_Print("Debugger crashed.");
					_events("^exit");
					return;
				}
				//print.qm2.write($"_ReadEvents:  {k}");
				if (k is not ("(gdb)" or "")) {
					try { _events(k); }
					catch (Exception e1) {
						print.it(e1);
						_events("^exit");
						return;
					}
				}
			}
			//print.it("<<<");
			if (_p == null) return;
			_fs.BeginRead([], 0, 0, ar => {
				while (_ignoreEvents) Thread.Sleep(10);
				App.Dispatcher.InvokeAsync(_readEvents);
			}, null);
		}
		
		public void Dispose() {
			if (_p == null) return;
			_p.Dispose();
			_p = null;
			_fs?.Dispose();
		}
		
		bool _Write(string s) {
			try { _p.Write(s); }
			catch (AuException e1) {
				_Print("Debugger crashed.");
				Debug_.Print(e1);
				Dispose();
				App.Dispatcher.InvokeAsync(() => _events("^exit"));
				return false;
			}
			return true;
		}
		
		/// <summary>
		/// Writes <i>s</i>. Does not read.
		/// </summary>
		/// <param name="s">Command, optionally with a token &gt;=1000, like "1020-command".</param>
		public void Send(string s) {
			_Write(s);
		}
		
		/// <summary>
		/// Writes token+s, like "5-command".
		/// Then synchronously reads until received a line that starts with the token followed by '^'. For other received lines calls the events callback.
		/// </summary>
		/// <param name="token">A number 1-999. Token 0 is used by this class.</param>
		/// <param name="noEvent">Called on events. Return true to not call the events callback.</param>
		/// <returns>The received line without token. Returns null if the debugger process ended.</returns>
		public string SendSync(int token, string s, Func<string, bool> noEvent = null) {
			var st = token.ToS();
			if (!_Write(st + s)) return null;
			_ignoreEvents = true;
			try {
				while (_p.ReadLine(out var k)) {
					//print.qm2.write($"SendSync:  {k}");
					if (k.Starts(st) && k.Eq(st.Length, '^')) return k[st.Length..];
					if (k is not ("(gdb)" or "")) {
						if (noEvent?.Invoke(k) == true) continue;
						_events(k);
					}
				}
			}
			finally { _ignoreEvents = false; }
			return null;
		}
	}
}
