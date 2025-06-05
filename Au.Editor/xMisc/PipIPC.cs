using System.Security.AccessControl;
using System.Security.Principal;
using System.IO.Pipes;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using System.Text.Json;

//TODO: in PIP session don't save settings. Also maybe files etc. Make readonly. Add strong file-modified sync main -> PIP.

/// <summary>
/// Used by all PiP processes.
/// </summary>
/// <remarks>
/// When PiP is active, 3 Au.Editor.exe processes are running:
/// <br/>• 1. Normal process in main session. Calls <see cref="StartPip"/> or <see cref="RunScriptInPip"/>. They use the <b>SendX</b> functions to communicate with process 3.
/// <br/>• 2. Process in main session that shows PiP window and starts child session. Can use the <b>SendX</b> functions to communicate with process 3.
/// 		Normally started by process 1 (the above functions). Or can be started manually, using command line <c>/pip</c>. Can run without process 1 or/and 3.
/// <br/>• 3. Normal process in child session. Calls <see cref="StartPipeServerThread"/>, which starts scripts in child session and performs other tasks requested by processes 1 and 2.
/// 		Normally started via the registry key (process 2 sets RunOnce if need). Or can be [re] started manually. If not running, processes 1 and 2 can't start scripts etc in child session.
/// </remarks>
class PipIPC {
	const string c_pipeName = "Au.PiP-pipe";
	
	/// <summary>
	/// Sends a message from the main session to the LA pipe server running in the PiP session.
	/// </summary>
	/// <param name="message">A single-line message. Can be like <c>"message"</c> or <c>"message parameter"</c>.</param>
	/// <param name="timeoutMS">Timeout for <see cref="NamedPipeClientStream.Connect"/>.</param>
	/// <returns>Response of the pipe server. Single line. Returns null if failed (eg connection timeout).</returns>
	public static string SendSync(string message, int timeoutMS = 60_000) {
		try {
			using var client = new NamedPipeClientStream(".", c_pipeName, PipeDirection.InOut);
			client.Connect(timeoutMS);
			
			var writer = new StreamWriter(client) { AutoFlush = true };
			var reader = new StreamReader(client);
			
			writer.WriteLine(message);
			var r = reader.ReadLine();
			return r;
		}
		catch (Exception ex) { print.it(ex); return null; }
	}
	
	/// <inheritdoc cref="SendSync"/>
	public static async Task<string> SendAsync(string message, int timeoutMS = 60_000) {
		return await Task.Run(() => SendSync(message, timeoutMS));
	}
	
	public static void StartPipeServerThread() {
		run.thread(_PipeServer, sta: false);
	}
	
	static void _PipeServer() {
		try {
			var security = new PipeSecurity();
			security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
			using var server = NamedPipeServerStreamAcl.Create(c_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, 0, 0, 0, security);
			
			for (; ; ) {
				server.WaitForConnection();
				
				var reader = new StreamReader(server);
				var writer = new StreamWriter(server) { AutoFlush = true };
				
				string message = reader.ReadLine(), param = null, ret = "\a";
				int i = message.IndexOf(' ');
				if (i > 0) (param, message) = (message[(i + 1)..], message[..i]);
				//print.it(message, param);
				
				switch (message) {
				case "runScript":
					//ret = _RunScript(param);
					Task.Run(() => _RunScript(param));
					break;
				case "logoff":
					Task.Run(() => computer.logoff());
					break;
				//case "clipboard":
				//	Task.Run(() => Process.Start("rdpclip.exe"));
				//	break;
				case "syncFiles":
					if (App.Dispatcher is { } disp) {
						disp.InvokeAsync(() => {
							Panels.Editor.OnAppActivated_();
							App.Model?.SyncWithFilesystem();
						});
					}
					break;
				default:
					ret = "Bad: " + message;
					break;
				}
				
				writer.WriteLine(ret);
				server.Disconnect();
			}
		}
		catch (Exception ex) { print.it(ex); }
	}
	
	static void _RunScript(string param) {
		//TODO: sync files
		
		try {
			var j = JsonNode.Parse(param);
			string file = (string)j["file"];
			string[] args = j["args"] is JsonArray ja ? ja.Select(o => (string)o).ToArray() : [];
			
			if (!wait.until(-60, () => miscInfo.isInputDesktop(true))) throw new InputDesktopException("Now cannot run scripts in PiP session."); //1-2 s when connecting to existing session
			
			//script.run(file, args); //works, but I like more low-level code here
			App.Dispatcher.InvokeAsync(() => {
				if (App.Model?.FindCodeFile(file) is { } f) {
					CompileRun.CompileAndRun(true, f, args);
				}
			});
		}
		catch (Exception ex) { print.it(ex); }
	}
	
	///// <summary>
	///// Returns true if child sessions supported (Win8.1+, not Home edition, not in child session).
	///// </summary>
	//public static bool CanUsePip => s_canUsePip ??= osVersion.minWin8_1 && !miscInfo.isChildSession && !(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", null) is string sPN && sPN.Contains(" Home"));
	//static bool? s_canUsePip;
	
	static bool _CanUsePip() {
		if (!osVersion.minWin8_1) return _Error("Requires Windows 8.1 or later."); //tested: on 8.0 error "class not registered"
		if (miscInfo.isChildSession) return _Error("Can't start another PiP session from PiP session.");
		if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", null) is string sPN && sPN.Contains(" Home"))
			return _Error($"PiP does not work on Windows Home editions. Your OS is {sPN}.");
		return true;
		//the pip exe will auto-enable child sessions
		
		static bool _Error(string s) {
			dialog.showError("PiP error", s, owner: App.Hmain);
			return false;
		}
	}
	
	public static void StartPip() {
		App.Model.Save.AllNowIfNeed();
		if (!_CanUsePip()) return;
		_PipProcessStarter("/pip").Start();
	}
	
	static ProcessStarter_ _PipProcessStarter(string args) => new(process.thisExePath, args, rawExe: true);
	
	public static async void RunScriptInPip(FileNode f, params string[] args) {
		App.Model.Save.AllNowIfNeed();
		if (!_CanUsePip()) return;
		if (!CompileRun.Compile(true, ref f, out _)) return;
		string itemPath = f.ItemPath;
		
		string _Run() {
			lock ("9j8+It9PbUaiHCFoo9QaSA") {
				static bool _EnsurePipConnected() {
					wnd wMsg = _FindWndMsg();
					if (wMsg.Is0) {
						var v = _PipProcessStarter("/pip /noactivate").Start(ProcessStarter_.Result.Need.WaitHandle);
						wait.until(0, () => !(wMsg = _FindWndMsg()).Is0 || v.waitHandle.WaitOne(0)); //wait until the pip message-only window created or the pip process ended
						v.waitHandle.Dispose();
						if (wMsg.Is0) return false;
					}
					return wait.until(0, () => wMsg.GetWindowLong(GWL.DWL.USER) == 1 || !wMsg.IsAlive) && wMsg.IsAlive; //wait until connected
					
					static wnd _FindWndMsg() => wnd.findFast("Au.PiP-msg", "#32770", messageOnly: true); //created in pip process at startup, just after entering the single-instance mutex
				}
				
				for (int i = 2; --i >= 0;) { //retry if the pip process is closing etc
					if (_EnsurePipConnected()) break;
					if (i == 0) return "Failed to start PiP session.";
					Debug_.Print("retrying PiP-connect");
					2.s();
				}
				
				return SendSync("runScript " + JsonSerializer.Serialize(new { file = itemPath, args = args }));
			}
		}
		
		if (await Task.Run(_Run) is string r) {
			if (!r.Starts("\a")) print.warning($"Failed to run script {f.SciLink()} in PiP session. {r}", -1);
		} else {
			print.warning($"Failed to run script {f.SciLink()} in PiP session. Make sure LibreAutomate is running in PiP session too.", -1);
		}
	}
}
