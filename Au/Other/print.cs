namespace Au;

/// <summary>
/// Writes text to the output window, console, log file or custom writer.
/// </summary>
[DebuggerStepThrough]
public static partial class print {
	/// <summary>
	/// Returns <c>true</c> if this process is attached to a console.
	/// </summary>
	public static bool isConsoleProcess => Api.GetConsoleOutputCP() != 0; //fast
	
	//public static bool isConsoleProcess => Api.GetStdHandle(Api.STD_INPUT_HANDLE) is not (0 or -1); //no, may be true even if not attached to a console, eg if this non-console program started from cmd/bat on Win7
	
	/// <summary>
	/// Returns <c>true</c> if is writing to console, <c>false</c> if to the output window etc.
	/// </summary>
	/// <remarks>
	/// Does not write to console in these cases:
	/// - <see cref="isConsoleProcess"/> is <c>false</c>.
	/// - <see cref="ignoreConsole"/> is <c>true</c>.
	/// - <see cref="logFile"/> is not <c>null</c>.
	/// - The startup info of this process tells to not show console window and to not redirect the standard output.
	/// </remarks>
	public static bool isWritingToConsole {
		get {
			if (!isConsoleProcess || ignoreConsole || logFile != null) return false;
			if (!_isVisibleConsole.HasValue) {
				Api.GetStartupInfo(out var x);
				_isVisibleConsole = x.hStdOutput != default || 0 == (x.dwFlags & 1) || 0 != x.wShowWindow; //redirected stdout, or visible console window
			}
			return _isVisibleConsole.Value;
		}
	}
	static bool? _isVisibleConsole;
	
	/// <summary>
	/// If <c>true</c>, in console process will not use the console window. Then everything is like in non-console process.
	/// </summary>
	/// <seealso cref="redirectConsoleOutput"/>
	/// <seealso cref="redirectDebugOutput"/>
	public static bool ignoreConsole { get; set; }
	
	/// <summary>
	/// Clears the output window or console text (if <see cref="isWritingToConsole"/>) or log file (if <see cref="logFile"/> not <c>null</c>).
	/// </summary>
	public static void clear() {
		if (logFile != null) {
			_ClearToLogFile();
		} else if (isWritingToConsole) {
			_ConsoleAction(PrintServerMessageType.Clear);
		} else if (qm2.use) {
			qm2.clear();
		} else {
			_ServerAction(PrintServerMessageType.Clear);
		}
	}
	
	/// <summary>
	/// Scrolls the output window or console to the top.
	/// </summary>
	public static void scrollToTop() {
		if (logFile != null) {
		} else if (isWritingToConsole) {
			_ConsoleAction(PrintServerMessageType.ScrollToTop);
		} else if (qm2.use) {
		} else {
			_ServerAction(PrintServerMessageType.ScrollToTop);
		}
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)] //avoid loading System.Console.dll
	static void _ConsoleAction(PrintServerMessageType action) {
		try {
			switch (action) {
			case PrintServerMessageType.Clear:
				//exception if redirected, it is documented.
				//if (!Console.IsOutputRedirected) Console.Clear(); //no, Clear does something more than if(IsOutputRedirected)
				Console.Clear();
				break;
			case PrintServerMessageType.ScrollToTop:
				Console.SetCursorPosition(0, 0);
				break;
			}
		}
		catch { }
	}
	
	/// <summary>
	/// Writes string to the output.
	/// </summary>
	/// <remarks>
	/// Appends newline (<c>"\r\n"</c>), unless text is like <c>"&lt;&gt;text&lt;nonl&gt;"</c>.
	/// 
	/// Can display links, colors, images, etc. More info: [](xref:output_tags).
	/// 
	/// Where the text goes:
	/// - If redirected, to wherever it is redirected. See <see cref="writer"/>.
	/// - Else if using log file (<see cref="logFile"/> not <c>null</c>), writes to the file.
	/// - Else if using console (<see cref="isWritingToConsole"/> returns <c>true</c>), writes to console.
	/// - Else if using local <see cref="PrintServer"/> (in this process), writes to it.
	/// - Else if exists global <see cref="PrintServer"/> (in any process), writes to it.
	/// - Else nowhere.
	/// </remarks>
	public static void it(string value) {
		writer.WriteLine(value);
	}
	
	/// <summary>
	/// Writes value of any type to the output.
	/// </summary>
	/// <param name="value">Value of any type. If <c>null</c>, writes <c>"null"</c>.</param>
	/// <remarks>
	/// If the type is unsigned integer (<b>uint</b>, <b>ulong</b>, <b>ushort</b>, <b>byte</b>, <b>nuint</b>), writes in hexadecimal format with prefix <c>"0x"</c>, unless <see cref="noHex"/> <c>true</c>.
	/// 
	/// This overload is used for all types except: strings, arrays, generic collections. They have own overloads; to use this function need to cast to object.
	/// For <b>Span</b> and other ref struct types use <c>print.it(x.ToString());</c>.
	/// </remarks>
	public static void it(object value) {
		it(util.toString(value));
	}
	
	/// <summary>
	/// Writes interpolated string to the output.
	/// </summary>
	/// <param name="value">Interpolated string. Can contain <c>:print</c> format like in the example, to display the value like <see cref="it(object)"/>.</param>
	/// <example>
	/// <code><![CDATA[
	/// int[] a = { 1, 2, 3 };
	/// print.it($"a: {a}"); //a: System.Int32[]
	/// print.it($"a: {a:print}"); //a: { 1, 2, 3 }
	/// ]]></code>
	/// </example>
	public static void it(InterpolatedString value) {
		writer.WriteLine(value.GetFormattedText());
	}
	
	/// <summary>
	/// Writes an array or generic collection to the output.
	/// </summary>
	/// <param name="value">
	/// Array or generic collection of any type.
	/// If <c>null</c>, writes <c>"null"</c>.
	/// The format depends on type:
	/// <br/>• <b>char[]</b> - like string.
	/// <br/>• <b>byte[]</b> - like <c>xx-xx-xx</c>; in hexadecimal, unless <see cref="noHex"/> <c>true</c>.
	/// <br/>• Other - multiple lines.
	/// </param>
	public static void it<T>(IEnumerable<T> value) {
		if (value is char[] or byte[]) print.it(util.toString(value));
		else list("\r\n", value);
	}
	
	/// <summary>
	/// Writes an array or generic collection to the output, as a list of items separated by <i>separator</i>.
	/// </summary>
	/// <param name="value">Array or generic collection of any type. If <c>null</c>, writes <c>"null"</c>.</param>
	public static void list<T>(string separator, IEnumerable<T> value) {
		string s = "null";
		if (value != null)
			using (new StringBuilder_(out var b)) {
				bool once = false;
				foreach (var v in value) {
					if (!once) once = true; else b.Append(separator);
					util.toString(b, v, compact: true);
				}
				s = b.ToString();
			}
		print.it(s);
	}
	
	/// <summary>
	/// Writes multiple arguments of any type to the output, using separator <c>", "</c>.
	/// </summary>
	/// <remarks>
	/// If a value is <c>null</c>, writes <c>"null"</c>.
	/// If a value is unsigned integer (<b>uint</b>, <b>ulong</b>, <b>ushort</b>, <b>byte</b>, <b>nuint</b>), writes in hexadecimal format with prefix <c>"0x"</c>.
	/// </remarks>
	public static void it(object value1, object value2, params object[] more) {
		it(util.toList(", ", value1, value2, more));
	}
	
	/// <summary>
	/// Writes multiple arguments of any type to the output, using <i>separator</i>.
	/// </summary>
	/// <inheritdoc cref="it(object, object, object[])"/>
	public static void list(string separator, object value1, object value2, params object[] more) {
		it(util.toList(separator, value1, value2, more));
	}
	
	/// <summary>
	/// Gets or sets object that actually writes text when is called <see cref="it"/>.
	/// </summary>
	/// <remarks>
	/// If you want to redirect or modify or just monitor output text, use code like in the example. It is known as "output redirection".
	/// Redirection is applied to whole process, not just this thread.
	/// Redirection affects <see cref="it"/>, <see cref="redirectConsoleOutput"/> and <see cref="redirectDebugOutput"/>. It does not affect <see cref="directly"/> and <see cref="clear"/>.
	/// Don't call <see cref="it"/> in method <b>WriteLine</b> of your writer class. It would call itself and create stack overflow. Call <see cref="directly"/>, like in the example.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// print.writer = new OutputWriterWithTime();
	/// 
	/// print.it("test");
	/// 
	/// class OutputWriterWithTime :TextWriter {
	/// 	public override void WriteLine(string value) { print.directly(DateTime.Now.ToString("T") + ".  " + value); }
	/// 	public override Encoding Encoding => Encoding.Unicode;
	/// }
	/// ]]></code>
	/// </example>
	public static TextWriter writer { get; set; } = new _OutputWriter();
	
	/// <summary>
	/// Our default writer class for the Writer property.
	/// </summary>
	class _OutputWriter : LineWriter_ {
		public override Encoding Encoding => Encoding.Unicode;
		
		protected override void WriteLineNow(string s) => directly(s);
	}
	
	/// <summary>
	/// Same as <see cref="it"/>, but does not pass the string to <see cref="writer"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)] //for stack trace, used in _WriteToServer
	public static void directly(string value) {
		value ??= "";
		//qm2.write($"'{value}'");
		if (logFile != null) _WriteToLogFile(value);
		else if (isWritingToConsole) _ConsoleWriteLine(value);
		else if (qm2.use) qm2.write(value);
		else _ServerWrite(value);
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)] //avoid loading System.Console.dll
	static void _ConsoleWriteLine(string value) => Console.WriteLine(value);
	
	/// <summary>
	/// Writes a warning text to the output.
	/// By default appends the stack trace.
	/// </summary>
	/// <param name="text">Warning text.</param>
	/// <param name="showStackFromThisFrame">If &gt;= 0, appends the stack trace, skipping this number of frames. Default 0. Does not append if <i>text</i> looks like a stack trace.</param>
	/// <param name="prefix">Text before <i>text</i>. Default <c>"&lt;&gt;Warning: "</c>.</param>
	/// <remarks>
	/// Calls <see cref="print.it"/>.
	/// Does not show more than 1 warning/second, unless <b>opt.warnings.Verbose</b> == <c>true</c> (see <see cref="OWarnings.Verbose"/>).
	/// To disable some warnings, use code <c>opt.warnings.Disable("warning text wildcard");</c> (see <see cref="OWarnings.Disable"/>).
	/// </remarks>
	/// <seealso cref="OWarnings"/>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void warning(string text, int showStackFromThisFrame = 0, string prefix = "<>Warning: ") {
		if (opt.warnings.IsDisabled(text)) return;
		
		if (!opt.warnings.Verbose) {
			var t = Api.GetTickCount64();
			if (t - s_warningTime < 1000) return;
			s_warningTime = t;
		}
		
		string s = text ?? "";
		if (showStackFromThisFrame >= 0 && !(s.Contains("\n   at ") && s.RxIsMatch(@"Exception: .*\R   at "))) { //include stack unless text contains stack
			var x = new StackTrace(showStackFromThisFrame + 1, true);
			var st = x.ToString(); var rn = st.Ends('\n') ? "" : "\r\n";
			s = $"{prefix}{s} <fold><\a>\r\n{st}{rn}</\a></fold>";
		} else s = prefix + s;
		
		it(s);
	}
	static long s_warningTime;
	
	/// <summary>
	/// Writes an exception warning to the output.
	/// </summary>
	/// <inheritdoc cref="warning(string, int, string)"/>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void warning(Exception e, string prefix = "<>Warning: ") {
		warning(e.ToString(), -1, prefix);
	}
	
	/// <summary>
	/// Let <b>Console.WriteX</b> methods in non-console process write to the same destination as <see cref="it"/>.
	/// </summary>
	/// <remarks>
	/// The default value is <c>true</c> in non-console scripts that use class <see cref="Console"/> and have role <b>miniProgram</b> (default); also <b>exeProgram</b> if started from the script editor. Also in these scripts <b>Console.ReadLine</b> uses <see cref="dialog.showInput"/>.
	/// 
	/// If <b>Console.Write</b> text does not end with <c>'\n'</c> character, it is buffered and not displayed until called again with text ending with <c>'\n'</c> character or until called <b>Console.WriteLine</b>.
	/// 
	/// <b>Console.Clear</b> will not clear output; it will throw exception.
	/// </remarks>
	public static bool redirectConsoleOutput {
		set {
			if (value) {
				if (_prevConsoleOut != null || isConsoleProcess) return;
				_prevConsoleOut = Console.Out;
				Console.SetOut(writer);
			} else if (_prevConsoleOut != null) {
				Console.SetOut(_prevConsoleOut);
				_prevConsoleOut = null;
			}
		}
		get => _prevConsoleOut != null;
	}
	static TextWriter _prevConsoleOut;
	//note: don't call this before AllocConsole. Then can't restore, and IsOutputRedirected always returns true.
	
	/// <summary>
	/// Let <b>Debug.Write</b>, <b>Trace.Write</b> and similar methods also write to the same destination as <see cref="it"/>.
	/// </summary>
	/// <remarks>
	/// Does not replace existing <b>Debug.Write</b> etc destinations, just add new destination.
	/// 
	/// If <b>Debug/Trace.Write</b> text does not end with <c>'\n'</c> character, it is buffered and not displayed until called again with text ending with <c>'\n'</c> character or until called <b>Debug/Trace.WriteLine</b>.
	/// 
	/// Tip: To write to the output window even in console process, set <c>print.ignoreConsole=true;</c> before calling this method first time.
	/// </remarks>
	public static bool redirectDebugOutput {
		set {
			if (value) {
				if (_traceListener != null) return;
				//Trace.Listeners.Add(IsWritingToConsole ? (new ConsoleTraceListener()) : (new TextWriterTraceListener(Writer)));
				Trace.Listeners.Add(_traceListener = new TextWriterTraceListener(writer));
				//speed: 5000
			} else if (_traceListener != null) {
				Trace.Listeners.Remove(_traceListener);
				_traceListener = null;
			}
		}
		get => _traceListener != null;
	}
	static TextWriterTraceListener _traceListener;
	
	/// <summary>
	/// Sets log file path.
	/// When set (not <c>null</c>), text passed to <see cref="it"/> will be written to the file.
	/// If value is <c>null</c> - restores default behavior.
	/// </summary>
	/// <remarks>
	/// The first <see cref="it"/> etc call (in this process) creates or opens the file and deletes old content if the file already exists.
	/// 
	/// Also supports mailslots. For <b>LogFile</b> use mailslot name, as documented in <msdn>CreateMailslot</msdn>. Multiple processes can use the same mailslot.
	/// </remarks>
	/// <exception cref="ArgumentException">The <c>set</c> function throws this exception if the value is not full path and not <c>null</c>.</exception>
	public static string logFile {
		get => _logFile;
		set {
			lock (_lockObj1) {
				if (_hFile != null) {
					_hFile.Close();
					_hFile = null;
				}
				if (value != null) {
					_logFile = pathname.normalize(value);
				} else _logFile = null;
			}
		}
		
	}
	static string _logFile;
	static _LogFile _hFile;
	static readonly object _lockObj1 = new();
	
	/// <summary>
	/// If <c>true</c>, will add current local time when using log file (see <see cref="logFile"/>).
	/// </summary>
	public static bool logFileTimestamp { get; set; }
	
	static void _WriteToLogFile(string s) {
		lock (_lockObj1) {
			if (_hFile == null) {
				g1:
				_hFile = _LogFile.Open();
				if (_hFile == null) {
					var e = lastError.code;
					if (e == Api.ERROR_SHARING_VIOLATION) {
						var u = pathname.makeUnique(_logFile, false);
						if (u != _logFile) { _logFile = u; goto g1; }
					}
					var logf = _logFile;
					_logFile = null;
					print.warning($"Failed to create or open log file '{logf}'. {lastError.messageFor(e)}");
					directly(s);
					return;
				}
			}
			_hFile.WriteLine(s);
		}
	}
	
	static void _ClearToLogFile() {
		lock (_lockObj1) {
			if (_hFile == null) {
				try { filesystem.delete(_logFile); } catch { }
			} else {
				_hFile.Clear();
			}
		}
	}
	
	unsafe class _LogFile {
		//info: We don't use StreamWriter. It creates more problems than would make easier.
		//	Eg its finalizer does not write to file. If we try to Close it in our finalizer, it throws 'already disposed'.
		//	Also we don't need such buffering. Better to write to the OS file buffer immediately, it's quite fast.
		
		Handle_ _h;
		string _name;
		
		/// <summary>
		/// Opens <b>LogFile</b> file handle for writing.
		/// Uses <b>CREATE_ALWAYS</b>, <b>GENERIC_WRITE</b>, <b>FILE_SHARE_READ</b>.
		/// </summary>
		public static _LogFile Open() {
			var path = logFile;
			var h = CreateFile_(path, false);
			if (h.Is0) return null;
			return new _LogFile() { _h = h, _name = path };
		}
		
		/// <summary>
		/// Writes <c>s + "\r\n"</c> and optionally timestamp.
		/// </summary>
		/// <remarks>
		/// If fails to write to file: Sets <b>LogFile</b>=<c>null</c>, which closes file handle. Writes a warning and <i>s</i> to the output window or console.
		/// </remarks>
		[SkipLocalsInit]
		public bool WriteLine(string s) {
			bool ok;
			int n = Encoding.UTF8.GetByteCount(s ??= "") + 1;
			using FastBuffer<byte> b = new(n + 35);
			byte* p = b.p;
			if (logFileTimestamp) {
				Api.GetLocalTime(out var t);
				Api.wsprintfA(p, "%i-%02i-%02i %02i:%02i:%02i.%03i   ", __arglist(t.wYear, t.wMonth, t.wDay, t.wHour, t.wMinute, t.wSecond, t.wMilliseconds));
				int nn = Ptr_.Length(p);
				Encoding.UTF8.GetBytes(s, new Span<byte>(p + nn, n));
				n += nn;
				if (s.Starts("<>")) {
					Api.memmove(p + 2, p, nn);
					p[0] = (byte)'<'; p[1] = (byte)'>';
				}
			} else {
				Encoding.UTF8.GetBytes(s, new Span<byte>(p, n));
			}
			p[n - 1] = 13; p[n++] = 10;
			
			ok = Api.WriteFile(_h, p, n, out _);
			if (!ok) {
				string emsg = lastError.message;
				logFile = null;
				print.warning($"Failed to write to log file '{_name}'. {emsg}");
				directly(s);
				//Debug.Assert(false);
			}
			return ok;
		}
		
		/// <summary>
		/// Sets file size = 0.
		/// </summary>
		public bool Clear() {
			bool ok = Api.SetFilePointerEx(_h, 0, null, Api.FILE_BEGIN) && Api.SetEndOfFile(_h);
			Debug.Assert(ok);
			return ok;
		}
		
		/// <summary>
		/// Closes file handle.
		/// </summary>
		public void Close() => _h.Dispose();
	}
	
	/// <summary>
	/// Calls <b>Api.CreateFile</b> to open file or mailslot.
	/// </summary>
	/// <param name="name">File path or mailslot name.</param>
	/// <param name="openExisting">Use <b>OPEN_EXISTING</b>. If <c>false</c>, uses <b>CREATE_ALWAYS</b>.</param>
	internal static Handle_ CreateFile_(string name, bool openExisting) {
		return Api.CreateFile(name, Api.GENERIC_WRITE, Api.FILE_SHARE_READ, openExisting ? Api.OPEN_EXISTING : Api.CREATE_ALWAYS);
		
		//tested: CREATE_ALWAYS works with mailslot too. Does not erase messages. Undocumented what to use.
	}
	
	///
#if !DEBUG
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static class qm2 {
		/// <summary>
		/// Sets to use QM2 as the output server.
		/// </summary>
		public static bool use { get; set; }
		
		/// <summary>
		/// Clears QM2 output panel.
		/// </summary>
		public static void clear() => _WriteToQM2(null);
		
		/// <summary>
		/// Writes line to QM2.
		/// </summary>
		public static void write(object o) => _WriteToQM2(o?.ToString() ?? "");
		
		/// <summary>
		/// Writes multiple arguments of any type to the output, using separator <c>", "</c>.
		/// </summary>
		/// <remarks>
		/// If a value is <c>null</c>, writes <c>"null"</c>.
		/// If a value is unsigned integer (<b>uint</b>, <b>ulong</b>, <b>ushort</b>, <b>byte</b>, <b>nuint</b>), writes in hexadecimal format with prefix <c>"0x"</c>.
		/// </remarks>
		public static void write(object value1, object value2, params object[] more) {
			write(util.toList(", ", value1, value2, more));
		}
		
		/// <summary>
		/// The same as <see cref="write"/>, but with <c>[Conditional("DEBUG")]</c>.
		/// </summary>
		[Conditional("DEBUG")]
		public static void writeD(object value1, object value2, params object[] more) {
			write(util.toList(", ", value1, value2, more));
		}
		
		/// <param name="s">If <c>null</c>, clears output.</param>
		static void _WriteToQM2(string s) {
			if (!_hwndQM2.IsAlive) {
				_hwndQM2 = Api.FindWindowEx(cn: "QM_Editor");
				if (_hwndQM2.Is0) return;
			}
			_hwndQM2.Send(Api.WM_SETTEXT, -1, s);
		}
		static wnd _hwndQM2;
	}
	
	/// <summary>
	/// Write unsigned numeric types in decimal format, not hexadecimal.
	/// </summary>
	public static bool noHex { get; set; }
	
	/// <summary>
	/// Some functions used by the <b>print</b> class.
	/// </summary>
	public static class util {
		/// <summary>
		/// Converts value of any type to <b>string</b>. Formats it like <see cref="it(object)"/>.
		/// </summary>
		/// <param name="value">Value of any type. If <c>null</c>, returns <c>"null"</c>.</param>
		/// <param name="compact">If <i>value</i> is <b>IEnumerable</b>, format it like <c>"{ item1, item2 }"</c>.</param>
		public static string toString(object value, bool compact = false) {
			switch (value) {
			case null: return "null";
			case string t: return t;
			case ulong or uint or ushort or byte or nuint when !noHex:
			case System.Collections.IEnumerable when !value.GetType().IsCOMObject: //info: eg Excel.Range and many other Excel interfaces are IEnumerable, and this process crashes, sometimes Excel too
			case System.Collections.DictionaryEntry:
				using (new StringBuilder_(out var b)) {
					toString(b, value, compact);
					return b.ToString();
				}
			default: return value.ToString();
			}
		}
		
		/// <summary>
		/// Appends value of any type to <b>StringBuilder</b>. Formats it like <see cref="it(object)"/>.
		/// </summary>
		/// <inheritdoc cref="toString(object, bool)"/>
		public static void toString(StringBuilder b, object value, bool compact) {
			switch (value) {
			case null: b.Append("null"); break;
			case string s: b.Append(s); break;
			case ulong u: _Unsigned(b, u); break;
			case uint u: _Unsigned(b, u); break;
			case ushort u: _Unsigned(b, u); break;
			case byte u: _Unsigned(b, u); break;
			case nuint u: _Unsigned(b, u); break;
			case char[] a: b.Append(a); break;
			case byte[] a:
				if (noHex) b.AppendJoin('-', a);
				else b.Append(BitConverter.ToString(a));
				break;
			case System.Collections.IEnumerable e when !value.GetType().IsCOMObject:
				if (compact) b.Append("{ ");
				string sep = null;
				foreach (var v in e) {
					if (sep == null) sep = compact ? ", " : "\r\n"; else b.Append(sep);
					toString(b, v, compact);
				}
				if (compact) b.Append(" }");
				break;
			case System.Collections.DictionaryEntry de:
				b.AppendFormat("[{0}, {1}]", de.Key, de.Value);
				break;
			default: b.Append(value); break;
			}
			
			static void _Unsigned(StringBuilder b, ulong u) {
				if (noHex) b.Append(u);
				else b.Append("0x").Append(u.ToString("X"));
			}
		}
		
		/// <summary>
		/// Converts multiple values of any type to <b>string</b> like <see cref="list(string, object, object, object[])"/>.
		/// </summary>
		public static string toList(string sep, object value1, object value2, params object[] more) {
			if (more == null) more = s_oaNull; //workaround for: if the third argument is null, we receive null and not object[] { null }
			else if (more.GetType() != typeof(object[])) more = new object[] { more }; //workaround for: if the third argument is an array, prints its elements without { }. If empty array, prints nothing (even no comma). With this workaround - only if object[], which is rare; and it's good, because may be used in a wrapper function that passes its 'params object[]' parameter here.
			
			using (new StringBuilder_(out var b)) {
				for (int i = 0, n = 2 + more.Length; i < n; i++) {
					if (i > 0) b.Append(sep);
					util.toString(b, i == 0 ? value1 : (i == 1 ? value2 : more[i - 2]), compact: true);
				}
				return b.ToString();
				
				//rejected: escape strings (eg if contains characters "\r\n,\0"):
				//	it can damage formatting tags etc;
				//	the string may be already escaped, eg wnd.ToString or elm.ToString;
				//	we don't know whether the caller wants it;
				//	let the caller escape it if wants, it's easy.
			}
		}
		static readonly object[] s_oaNull = { null };
	}
	
#pragma warning disable 1591 //no XML doc
	/// <summary>
	/// Interpolated string handler that adds <c>:print</c> format like <see cref="it(InterpolatedString)"/>.
	/// </summary>
	[InterpolatedStringHandler, EditorBrowsable(EditorBrowsableState.Never)]
	public ref struct InterpolatedString {
		DefaultInterpolatedStringHandler _f;
		
		public InterpolatedString(int literalLength, int formattedCount) {
			_f = new(literalLength, formattedCount);
		}
		
		public InterpolatedString(int literalLength, int formattedCount, IFormatProvider provider) {
			_f = new(literalLength, formattedCount, provider);
		}
		
		public InterpolatedString(int literalLength, int formattedCount, IFormatProvider provider, Span<char> initialBuffer) {
			_f = new(literalLength, formattedCount, provider, initialBuffer);
		}
		
		public void AppendLiteral(string value)
			 => _f.AppendLiteral(value);
		
		public void AppendFormatted<T>(T value)
			 => _f.AppendFormatted(value);
		
		public void AppendFormatted<T>(T value, int alignment)
			 => _f.AppendFormatted(value, alignment);
		
		public void AppendFormatted<T>(T value, string format) {
			if (format == "print") _f.AppendLiteral(util.toString(value, compact: true));
			else _f.AppendFormatted(value, format);
		}
		
		public void AppendFormatted<T>(T value, int alignment, string format) {
			if (format == "print") _f.AppendFormatted(util.toString(value, compact: true), alignment);
			else _f.AppendFormatted(value, alignment, format);
		}
		
		public void AppendFormatted(RStr value)
			=> _f.AppendFormatted(value);
		
		public void AppendFormatted(RStr value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public void AppendFormatted(string value)
			=> _f.AppendFormatted(value);
		
		public void AppendFormatted(string value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public void AppendFormatted(object value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public string GetFormattedText() => _f.ToStringAndClear();
	}
}
