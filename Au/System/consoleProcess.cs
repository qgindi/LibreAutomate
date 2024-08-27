namespace Au;

/// <summary>
/// Runs a console program in hidden mode. Gets its output text and can write input text.
/// </summary>
/// <remarks>
/// Must be disposed. In the example the <c>using</c> statement does it.
/// </remarks>
/// <seealso cref="run.console(Action{string}, string, string, string, Encoding, bool)"/>
/// <example>
/// <code><![CDATA[
/// using var c = new consoleProcess(folders.Workspace + @"exe\console1\console1.exe");
/// //c.Encoding = Console.OutputEncoding;
/// while (c.Read(out var s)) {
/// 	if (c.IsLine) {
/// 		print.it($"<><c green><_>{s}</_><>");
/// 	} else {
/// 		if (s == "User: ") c.Write("A");
/// 		else if (s == "Password: ") c.Write("B");
/// 		//else if (c.Wait()) continue; //let next Read wait for more text and get old + new text. Use this if other prompts are not possible.
/// 		else if (c.Wait(500)) continue; //wait for more text max 500 ms. If received, let next Read get old + new text.
/// 		else if (dialog.showInput(out var s1, null, s)) c.Write(s1);
/// 		//else print.it($"<><c blue><_>{s}</_><><nonl>");
/// 		else throw new OperationCanceledException();
/// 	}
/// }
/// if (c.ExitCode is int ec && ec != 0) throw new Exception($"Failed. Exit code: {ec}");
/// ]]></code>
/// <code><![CDATA[
/// using var c = new consoleProcess("example.exe");
/// c.Prompt("User: ", "A");
/// c.Prompt("Password: ", "B");
/// while (c.Read(out var s)) print.it(s);
/// print.it(c.ExitCode);
/// ]]></code>
/// </example>
public sealed unsafe class consoleProcess : IDisposable {
	Handle_ _hProcess, _hOutRead, _hInWrite;
	Decoder _decoder;
	byte[] _b;
	char[] _c;
	int _n, _i;
	bool _skipN;
	StringBuilder _sb;
	
	/// <summary>
	/// Starts the console program.
	/// </summary>
	/// <inheritdoc cref="run.console(string, string, string, Encoding)" path="/param"/>
	/// <exception cref="AuException">Failed, for example file not found.</exception>
	public consoleProcess(string exe, string args = null, string curDir = null) {
		//rejected: bool separateError (separete stderr from stdout). Why:
		//	Impossible to make it sync with stdout.
		//	Complicated library code.
		//	Comlicated user code.
		//	Rarely used. And because of the above reasons would be very very rarely used.
		
		exe = run.NormalizeFile_(true, exe, out _, out _);
		var ps = new ProcessStarter_(exe, args, curDir, rawExe: true);
		
		Handle_ hInRead = default, hOutWrite = default/*, hErrWrite = default*/;
		Api.PROCESS_INFORMATION pi = default;
		try {
			var sa = new Api.SECURITY_ATTRIBUTES(null) { bInheritHandle = 1 };
			if (!Api.CreatePipe(out hInRead, out _hInWrite, sa, 0)) throw new AuException(0);
			if (!Api.CreatePipe(out _hOutRead, out hOutWrite, sa, 0)) throw new AuException(0);
			//if (!Api.CreatePipe(out _hErrRead, out hErrWrite, sa, 0)) throw new AuException(0);
			Api.SetHandleInformation(_hInWrite, 1, 0); //remove HANDLE_FLAG_INHERIT
			Api.SetHandleInformation(_hOutRead, 1, 0);
			//Api.SetHandleInformation(_hErrRead, 1, 0);
			
			ps.si.dwFlags |= Api.STARTF_USESTDHANDLES | Api.STARTF_USESHOWWINDOW;
			ps.si.hStdInput = hInRead;
			ps.si.hStdOutput = hOutWrite;
			//ps.si.hStdError = hErrWrite;
			ps.si.hStdError = hOutWrite;
			ps.flags |= Api.CREATE_NEW_CONSOLE;
			
			if (!ps.StartL(out pi, inheritHandles: true)) throw new AuException(0);
			
			_hProcess = pi.hProcess;
			TerminateFinally = true;
			process.thisProcessExit += _OnExit;
		}
		finally {
			hInRead.Dispose();
			hOutWrite.Dispose();
			//hErrWrite.Dispose();
			pi.hThread.Dispose();
			Ended = _hProcess.Is0;
		}
	}
	
	///
	public void Dispose() {
		//print.it(Api.WaitForSingleObject(_hProcess, 0)); //info: all tested processes ended after 0-5 ms after ERROR_BROKEN_PIPE
		if (TerminateFinally && !Ended && Api.WaitForSingleObject(_hProcess, 100) != Api.WAIT_TIMEOUT) TerminateFinally = false;
		_Dispose();
		GC.SuppressFinalize(this);
	}
	
	void _Dispose() {
		process.thisProcessExit -= _OnExit;
		if (TerminateFinally && !Ended) TerminateNow();
		_hProcess.Dispose();
		_hInWrite.Dispose();
		_hOutRead.Dispose();
		//_hErrRead.Dispose();
	}
	
	///
	~consoleProcess() {
		print.warning("consoleProcess not disposed", -1);
		_Dispose();
	}
	
	void _OnExit(Exception e) {
		if (TerminateFinally && !Ended) TerminateNow();
	}
	
	/// <summary>
	/// Console's text encoding.
	/// Default is <see cref="Encoding.UTF8"/>.
	/// </summary>
	/// <remarks>
	/// If wrong encoding, the received text may contain garbage. Try <see cref="Console.OutputEncoding"/> or <see cref="Encoding.Unicode"/>.
	/// </remarks>
	public Encoding Encoding {
		get => _encoding ??= Encoding.UTF8;
		set {
			if (value != _encoding) {
				_encoding = value;
				_decoder = null;
			}
		}
	}
	Encoding _encoding;
	
	/// <summary>
	/// Input text encoding for <see cref="Write"/>. If <c>null</c> (default), will use <see cref="Encoding"/>.
	/// </summary>
	public Encoding InputEncoding { get; set; }
	
	/// <summary>
	/// Waits and reads next full line.
	/// </summary>
	/// <param name="s">Receives the text. It does not contain newline characters (<c>'\r'</c>, <c>'\n'</c>).</param>
	/// <returns><c>false</c> if there is no more text to read because the console process ended or is ending.</returns>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// Waits for new text from console, reads it into an internal buffer, and then returns it one full line at a time.
	/// 
	/// To read a prompt (incomplete line that asks for user input), use <see cref="Read"/> instead. This function would just hang waiting for full line ending with newline characters.
	/// </remarks>
	public bool ReadLine(out string s) => _Read(out s, true);
	
	/// <summary>
	/// Waits and reads next full or partial line.
	/// </summary>
	/// <param name="s">Receives the text. It does not contain newline characters (<c>'\r'</c>, <c>'\n'</c>).</param>
	/// <returns><c>false</c> if there is no more text to read because the console process ended or is ending.</returns>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// Waits for new text from console, reads it into an internal buffer, and then returns it one line at a time.
	/// 
	/// Sets <see cref="IsLine"/> = <c>true</c> if the line text in the buffer is terminated with newline characters. Else sets <b>IsLine</b> = <c>false</c>.
	///
	/// When <b>IsLine</b> is <c>false</c>, <i>s</i> text can be either a prompt (incomplete line that asks for user input) or an incomplete line/prompt text (the remainder will be retrieved later). Your script should somehow distinguish it. If it's a known prompt, let it call <see cref="Write"/>. Else call <see cref="Wait"/>. See example.
	///
	/// It's impossible to automatically distinguish a prompt from a partial line or partial prompt. The console program can write line text in parts, possibly with delays in between. Or it can write a prompt text and wait for user input. Also this function may receive line text in parts because of limited buffer size etc.
	/// </remarks>
	/// <inheritdoc cref="consoleProcess" path="/example"/>
	public bool Read(out string s) => _Read(out s, false);
	
	/// <summary>
	/// Returns:
	/// <br/>• 1 - there is data available to read.
	/// <br/>• 0 - no data.
	/// <br/>• -1 - error, eg process ended. Supports <see cref="lastError"/>.
	/// </summary>
	internal int CanReadNow_ => _i < _n ? 1 : !Api.PeekNamedPipe(_hOutRead, null, 0, out _, out int n) ? -1 : n > 0 ? 1 : 0;
	
	internal IntPtr OutputHandle_ => _hOutRead;
	
	bool _ReadPipe() {
		//native console API allows any buffer size when writing, but wrappers usually use small buffer, eg .NET 4-5 KB, msvcrt 5 KB, C++ not tested
		_b ??= new byte[8000];
		_c ??= new char[_b.Length + 10];
		_sb ??= new();
		_n = _i = 0;
		
		for (; ; ) {
			IntPtr hRead = _hOutRead;
			//IsError = hRead == _hErrRead;
			
			if (!Api.ReadFileArr(hRead, _b, out int nr)) return false;
			if (nr > 0) {
				var b = _b.AsSpan(0, nr);
				
				//BOM? Noticed with: robocopy /unicode (UTF-16 BOM followed by ASCII).
				if (b is [0xEF, 0xBB, 0xBF, ..]) b = b[3..]; else if (b is [0xFF, 0xFE, ..]) b = b[2..];
				
				if (_skipN && _b[0] == 10) {
					_skipN = false;
					b = b[1..];
				}
				
				_decoder ??= Encoding.GetDecoder(); //ensures we'll not get partial multibyte chars (UTF8 etc) at buffer end/start
				_n = _decoder.GetChars(b, _c, false);
				if (_n > 0) return true;
			}
		}
	}
	
	bool _Read(out string text, bool needLine, Func<string, bool> prompt = null) {
		text = null;
		if (Ended) return _R(false);
		
		if (_wasReadMore) _wasReadMore = false; else _sb?.Clear();
		
		readPipe:
		if (_i >= _n) {
			if (!_ReadPipe()) return _R(_Ended(ref text));
		}
		
		var k = new Span<char>(_c, _i, _n - _i);
		
		int i = k.IndexOfAny('\n', '\r');
		if (i < 0) {
			_sb.Append(k);
			_n = 0;
			if (!needLine) {
				switch (_PeekWait(30, 1)) {
				case _PWResult.OK: goto readPipe;
				case _PWResult.End: return _R(_Ended(ref text));
				}
				var s1 = _sb.ToString();
				if (prompt != null) {
					if (prompt(s1)) {
						_sb.Clear();
						return _R(_ReturnText(ref text, s1, false));
					}
					if (_PeekWait(5000, 1) == _PWResult.OK) goto readPipe;
					text = s1; //for exception message
					return false;
				} else {
					return _R(_ReturnText(ref text, s1, false));
				}
			}
			goto readPipe;
		}
		
		string s = _sb.Length > 0 ? _sb.Append(k[0..i]).ToString() : k[0..i].ToString();
		if (k[i++] == '\r') {
			if (i == k.Length) _skipN = true;
			else if (k[i] == '\n') i++;
		}
		_i += i;
		
		return _R(_ReturnText(ref text, s, true));
		
		bool _ReturnText(ref string text, string s, bool isLine) {
			text = s;
			IsLine = isLine;
			if (isLine) _sb.Clear();
			return true;
		}
		
		bool _Ended(ref string text) {
			if (lastError.code != Api.ERROR_BROKEN_PIPE) throw new AuException(0);
			Ended = true;
			if (_sb.Length == 0) return false;
			return _ReturnText(ref text, _sb.ToString(), true);
		}
		
		bool _R(bool r, [CallerLineNumber] int l_ = 0) {
			//print.it("return", l_);
			return r;
		}
	}
	
	/// <summary>
	/// Waits for more text and tells next <see cref="Read"/> to get old + new text.
	/// </summary>
	/// <param name="timeout">Timeout, ms. The function returns <c>false</c> if did not receive more text during that time. If -1, returns <c>true</c> without waiting (next <see cref="Read"/> will wait).</param>
	/// <returns><c>true</c> if received more text or if <i>timeout</i> is -1.</returns>
	/// <exception cref="InvalidOperationException"><see cref="IsLine"/> <c>true</c>. Or multiple <b>Wait</b> without <b>Read</b>.</exception>
	/// <remarks>
	/// If returns <c>true</c>, next <see cref="Read"/> will get the old text + new text. If the console process ends while waiting, next <b>Read</b> will get the old text, and <see cref="IsLine"/> will be <c>true</c>.
	/// </remarks>
	public bool Wait(int timeout = -1) {
		if (IsLine) throw new InvalidOperationException("IsLine true");
		if (_wasReadMore) throw new InvalidOperationException("multiple Wait without Read");
		if (timeout < 0) {
			if (timeout != -1) throw new ArgumentException();
		} else {
			if (_PeekWait(timeout, 15) == _PWResult.Timeout) return false; //let next _Read clear _sb
		}
		return _wasReadMore = true; //let next _Read don't clear _sb but set _wasReadMore = false
	}
	bool _wasReadMore;
	
	/// <summary>
	/// Waits for next prompt (incomplete line that asks for user input). Reads the prompt and all lines before it. Then can write input text and <c>"\n"</c>.
	/// </summary>
	/// <param name="prompt">Prompt text. Format: [wildcard expression](xref:wildcard_expression).</param>
	/// <param name="input">Input text. If <c>""</c>, writes just <c>"\n"</c>. If <c>null</c>, does not write.</param>
	/// <returns>List of lines before the prompt. The last item is the prompt.</returns>
	/// <exception cref="AuException">Next prompt text does not match <i>prompt</i> (after waiting 5 s for full prompt). Or the console process ended. Or failed to write <i>input</i>.</exception>
	/// <example>
	/// <code><![CDATA[
	/// using var c = new consoleProcess("example.exe");
	/// c.Prompt("User: ", "A");
	/// c.Prompt("Password: ", "B");
	/// while (c.Read(out var s)) print.it(s);
	/// ]]></code>
	/// <code><![CDATA[
	/// var a = c.Prompt("User:");
	/// print.it(a);
	/// c.Write(a.Any(o => o.Contains("keyword")) ? "A" : "B");
	/// ]]></code>
	/// <code><![CDATA[
	/// using var c = new consoleProcess("cmd.exe");
	/// var prompt = @"C:\*>";
	/// c.Prompt(prompt, "example.exa");
	/// foreach (var s in c.Prompt(prompt).SkipLast(1)) print.it(s);
	/// c.Write("exit");
	/// ]]></code>
	/// </example>
	public List<string> Prompt(string prompt, string input = null) {
		wildex wild = prompt;
		bool ok = false;
		Func<string, bool> f = s => ok = wild.Match(s);
		List<string> a = new();
		while (!ok) {
			if (!_Read(out var s, false, f))
				throw new AuException(Ended ? $"The console process ended while waiting for prompt \"{prompt}\"." : $"The prompt text does not match \"{prompt}\". It is \"{s}\".");
			a.Add(s);
		}
		if (input != null) Write(input);
		return a;
	}
	
	/// <summary>
	/// Sends text to the console's input. Also sends character <c>'\n'</c> (like key <c>Enter</c>), unless <i>text</i> ends with <c>'\n'</c> or <i>noNL</i> is <c>true</c>.
	/// </summary>
	/// <param name="text"></param>
	/// <param name="noNL">Don't append character <c>'\n'</c> when <i>text</i> does not end with <c>'\n'</c>.</param>
	/// <exception cref="AuException">Failed.</exception>
	public void Write(string text, bool noNL = false) {
		if (!text.NE()) _Write(text);
		if (!noNL && text is not [.., '\n']) _Write("\n");
		
		void _Write(string s) {
			bool ok = Api.WriteFile2(_hInWrite, (InputEncoding ?? _encoding ?? Encoding.UTF8).GetBytes(s), out _);
			if (!ok) throw new AuException(0);
		}
	}
	
	/// <summary>
	/// <see cref="Read"/> sets this property = <c>true</c> if in console output the line text ended with newline characters; <c>false</c> if not.
	/// </summary>
	/// <remarks>
	/// If returns <c>false</c>, the text returned by the last <b>Read</b> is either a prompt (incomplete line that asks for user input) or an incomplete line. You can use <see cref="Wait"/> to wait for more text.
	/// </remarks>
	public bool IsLine { get; private set; }
	
	///// <summary>
	///// Returns <c>true</c> if the last <b>ReadX</b> function retrieved text from the standard error stream; <c>false</c> if from the standard output stream.
	///// </summary>
	//public bool IsError { get; private set; }
	
	/// <summary>
	/// Returns <c>true</c> if a <b>ReadX</b> function detected that the console output stream is closed. The process is ended or ending.
	/// </summary>
	public bool Ended { get; private set; }
	
	/// <summary>
	/// Gets the exit code of the console process.
	/// If the process is still running, waits until it exits.
	/// </summary>
	/// <value>If fails, returns <c>int.MinValue</c>.</value>
	public int ExitCode {
		get {
			bool retry = false; g1:
			if (!Api.GetExitCodeProcess(_hProcess, out int r)) return int.MinValue;
			if (r == 259 && !retry && (retry = 0 == Api.WaitForSingleObject(_hProcess, -1))) goto g1; //STILL_ACTIVE
			return r;
		}
	}
	
	/// <summary>
	/// Terminates the console process.
	/// </summary>
	/// <param name="exitCode"></param>
	public void TerminateNow(int exitCode = -1) {
		Api.TerminateProcess(_hProcess, exitCode);
		TerminateFinally = false;
	}
	
	/// <summary>
	/// If the console process is still running when this variable is dying, terminate it.
	/// Default <c>true</c>.
	/// </summary>
	public bool TerminateFinally { get; set; }
	
	_PWResult _PeekWait(int time, int minPeriod, int maxPeriod = 100) {
		for (int period = minPeriod; time > 0;) {
			if (period > 0) {
				period.ms();
				time -= period;
			}
			if (period < maxPeriod) period++;
			if (!Api.PeekNamedPipe(_hOutRead, null, 0, out _, out int nr)) return _PWResult.End;
			if (nr > 0) return _PWResult.OK;
		}
		return _PWResult.Timeout;
	}
	
	enum _PWResult { OK, Timeout, End }
	
	/// <summary>
	/// Reads all console output text until its process ends. Returns that text.
	/// </summary>
	/// <remarks>
	/// Does not parse lines. Does not normalize newline characters.
	/// </remarks>
	/// <exception cref="AuException">Failed.</exception>
	public string ReadAllText() {
		while (_ReadPipe()) _sb.Append(_c, 0, _n);
		if (lastError.code != Api.ERROR_BROKEN_PIPE) throw new AuException(0);
		var s = _sb.ToString();
		_sb.Clear();
		return s;
	}
	
	/// <summary>
	/// Reads all console output text until its process ends. Calls callback function.
	/// </summary>
	/// <param name="output">Called for each text chunk received from console.</param>
	/// <remarks>
	/// Does not parse lines. Does not normalize newline characters.
	/// </remarks>
	/// <exception cref="AuException">Failed.</exception>
	public void ReadAllText(Action<string> output) {
		if (_sb?.Length > 0) {
			var s = _sb.ToString();
			_sb.Clear();
			output(s);
		}
		while (_ReadPipe()) output(new(_c, 0, _n));
		if (lastError.code != Api.ERROR_BROKEN_PIPE) throw new AuException(0);
	}
}
