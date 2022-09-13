namespace Au {
	/// <summary>
	/// Contains static functions to execute or open programs, files, folders, web pages, etc, start new threads.
	/// </summary>
	public static class run {
		/// <summary>
		/// Runs/opens a program, document, directory (folder), URL, new email, Control Panel item etc.
		/// The returned <see cref="RResult"/> variable contains some process info - process id etc.
		/// </summary>
		/// <param name="file">
		/// Examples:
		/// <br/>• <c>@"C:\file.txt"</c>
		/// <br/>• <c>folders.Documents</c>
		/// <br/>• <c>folders.System + "notepad.exe"</c>
		/// <br/>• <c>@"%folders.System%\notepad.exe"</c>
		/// <br/>• <c>@"%TMP%\file.txt"</c>
		/// <br/>• <c>"notepad.exe"</c>
		/// <br/>• <c>@"..\folder\x.exe"</c>
		/// <br/>• <c>"http://a.b.c/d"</c>
		/// <br/>• <c>"file:///path"</c>
		/// <br/>• <c>"mailto:a@b.c"</c>
		/// <br/>• <c>":: ITEMIDLIST"</c>
		/// <br/>• <c>@"shell:::{CLSID}"</c>
		/// <br/>• <c>@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"</c>.
		/// 
		/// <para>
		/// More info in Remarks.
		/// </para>
		/// </param>
		/// <param name="args">
		/// Command line arguments.
		/// This function expands environment variables if starts with <c>"%"</c> or <c>"\"%"</c>.
		/// </param>
		/// <param name="flags"></param>
		/// <param name="dirEtc">
		/// Allows to specify more parameters: current directory, verb, etc.
		/// If string, it sets initial current directory for the new process. If "", gets it from <i>file</i>. More info: <see cref="ROptions.CurrentDirectory"/>.
		/// </param>
		/// <exception cref="ArgumentException">Used both <b>ROptions.Verb</b> and <b>RFlags.Admin</b> and this process isn't admin.</exception>
		/// <exception cref="AuException">Failed. For example, the file does not exist.</exception>
		/// <remarks>
		/// It works like when you double-click a file icon. It may start new process or not. For example it may just activate window if the program is already running.
		/// Uses API <msdn>ShellExecuteEx</msdn>.
		/// Similar to <see cref="Process.Start(string, string)"/>.
		/// 
		/// The <i>file</i> parameter can be:
		/// - Full path of a file or directory. Examples: <c>@"C:\file.txt"</c>, <c>folders.Documents</c>, <c>folders.System + "notepad.exe"</c>, <c>@"%folders.System%\notepad.exe"</c>.
		/// - Filename of a file or directory, like <c>"notepad.exe"</c>. The function calls <see cref="filesystem.searchPath"/>.
		/// - Path relative to <see cref="folders.ThisApp"/>. Examples: <c>"x.exe"</c>, <c>@"subfolder\x.exe"</c>, <c>@".\subfolder\x.exe"</c>, <c>@"..\another folder\x.exe"</c>.
		/// - URL. Examples: <c>"https://www.example.com"</c>, <c>"file:///path"</c>.
		/// - Email, like <c>"mailto:a@b.c"</c>. Subject, body etc also can be specified, and Google knows how.
		/// - Shell object's ITEMIDLIST like <c>":: ITEMIDLIST"</c>. See <see cref="Pidl.ToHexString"/>, <see cref="folders.shell"/>. Can be used to open virtual folders and items like Control Panel.
		/// - Shell object's parsing name, like <c>@"shell:::{CLSID}"</c> or <c>@"::{CLSID}"</c>. See <see cref="Pidl.ToShellString"/>. Can be used to open virtual folders and items like Control Panel.
		/// - To run a Windows Store App, use <c>@"shell:AppsFolder\WinStoreAppId"</c> format. Example: <c>@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"</c>. To discover the string use hotkey Ctrl+Shift+Q or function <see cref="WndUtil.GetWindowsStoreAppId"/> or Google.
		/// - To open a Windows Settings page can be used <google>ms-settings</google>, like <c>"ms-settings:display"</c>. To open Settings use <c>"ms-settings:"</c>.
		/// 
		/// Supports environment variables, like <c>@"%TMP%\file.txt"</c>. See <see cref="pathname.expand"/>.
		/// </remarks>
		/// <seealso cref="wnd.find"/>
		/// <seealso cref="wnd.findOrRun"/>
		/// <seealso cref="wnd.runAndFind"/>
		/// <example>
		/// Run Notepad and wait for an active Notepad window.
		/// <code><![CDATA[
		/// run.it("notepad.exe");
		/// 1.s();
		/// wnd w = wnd.wait(10, true, "*- Notepad", "Notepad");
		/// ]]></code>
		/// Run Notepad or activate a Notepad window.
		/// <code><![CDATA[
		/// wnd w = wnd.findOrRun("*- Notepad", run: () => run.it("notepad.exe"));
		/// ]]></code>
		/// Run File Explorer and wait for new folder window. Ignores matching windows that already existed.
		/// <code><![CDATA[
		/// var w = wnd.runAndFind(
		/// 	() => run.it(@"explorer.exe"),
		/// 	10, cn: "CabinetWClass");
		/// ]]></code>
		/// </example>
		public static RResult it(string file, string args = null, RFlags flags = 0, ROptions dirEtc = null) {
			Api.SHELLEXECUTEINFO x = default;
			x.cbSize = Api.SizeOf(x);
			x.fMask = Api.SEE_MASK_NOZONECHECKS | Api.SEE_MASK_NOASYNC | Api.SEE_MASK_CONNECTNETDRV | Api.SEE_MASK_UNICODE;
			x.nShow = Api.SW_SHOWNORMAL;

			bool curDirFromFile = false;
			var more = dirEtc;
			if (more != null) {
				x.lpVerb = more.Verb;
				if (x.lpVerb != null) x.fMask |= Api.SEE_MASK_INVOKEIDLIST; //makes slower. But verbs are rarely used.

				if (more.CurrentDirectory is string cd) {
					if (cd.Length == 0) curDirFromFile = true; else cd = pathname.expand(cd);
					x.lpDirectory = cd;
				}

				if (!more.OwnerWindow.IsEmpty) x.hwnd = more.OwnerWindow.Hwnd.Window;

				switch (more.WindowState) {
				case ProcessWindowStyle.Hidden: x.nShow = Api.SW_HIDE; break;
				case ProcessWindowStyle.Minimized: x.nShow = Api.SW_SHOWMINIMIZED; break;
				case ProcessWindowStyle.Maximized: x.nShow = Api.SW_SHOWMAXIMIZED; break;
				}

				x.fMask &= ~more.FlagsRemove;
				x.fMask |= more.FlagsAdd;
			}

			if (flags.Has(RFlags.Admin)) {
				if (x.lpVerb == null || x.lpVerb.Eqi("runas")) x.lpVerb = "runas";
				else if (!uacInfo.isAdmin) throw new ArgumentException("Cannot use Verb with flag Admin, unless this process is admin");
			}

			file = _NormalizeFile(false, file, out bool isFullPath, out bool isShellPath);
			Pidl pidl = null;
			if (isShellPath) { //":: ITEMIDLIST" or "::{CLSID}..." (we convert it too because the API does not support many)
				pidl = Pidl.FromString(file); //does not throw
				if (pidl != null) {
					x.lpIDList = pidl.UnsafePtr;
					x.fMask |= Api.SEE_MASK_INVOKEIDLIST;
				} else x.lpFile = file;
			} else {
				x.lpFile = file;

				if (curDirFromFile && isFullPath) x.lpDirectory = pathname.getDirectory(file);
			}
			x.lpDirectory ??= Directory.GetCurrentDirectory();
			if (!args.NE()) x.lpParameters = pathname.expand(args);

			if (0 == (flags & RFlags.ShowErrorUI)) x.fMask |= Api.SEE_MASK_FLAG_NO_UI;
			if (0 == (flags & RFlags.WaitForExit)) x.fMask |= Api.SEE_MASK_NO_CONSOLE;
			if (0 != (flags & RFlags.MostUsed)) x.fMask |= Api.SEE_MASK_FLAG_LOG_USAGE;
			x.fMask |= Api.SEE_MASK_NOCLOSEPROCESS;

			WndUtil.EnableActivate(-1);

			bool waitForExit = 0 != (flags & RFlags.WaitForExit);
			bool needHandle = flags.Has(RFlags.NeedProcessHandle);

			bool ok = false; int pid = 0, errorCode = 0;
			bool asUser = !flags.HasAny(RFlags.Admin | RFlags.InheritAdmin) && uacInfo.isAdmin; //info: new process does not inherit uiAccess
			if (asUser) {
				ok = Cpp.Cpp_ShellExec(x, out pid, out int injectError, out int execError);
				if (!ok) {
					if (injectError != 0) {
						print.warning("Failed to run as non-admin.");
						//once in TT process started to always fail. More info in UnmarshalAgentIAccessible().
						asUser = false;
					} else errorCode = execError;
				}
			}
			if (!asUser) {
				ok = Api.ShellExecuteEx(ref x);
				if (!ok) errorCode = lastError.code;
			}
			pidl?.Dispose();
			if (!ok) throw new AuException(errorCode, $"*run '{file}'");

			var R = new RResult();
			WaitHandle_ ph = null;

			if (needHandle || waitForExit) {
				if (pid != 0) x.hProcess = Handle_.OpenProcess(pid, Api.PROCESS_ALL_ACCESS);
				if (!x.hProcess.Is0) ph = new WaitHandle_(x.hProcess, true);
			}

			if (!waitForExit) {
				if (pid != 0) R.ProcessId = pid;
				else if (!x.hProcess.Is0) R.ProcessId = process.processIdFromHandle(x.hProcess);
			}

			try {
				Api.AllowSetForegroundWindow();

				if (x.lpVerb != null && Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
					Thread.CurrentThread.Join(50); //need min 5-10 for file Properties. And not Sleep.

				if (ph != null) {
					if (waitForExit) {
						ph.WaitOne();
						if (Api.GetExitCodeProcess(x.hProcess, out var exitCode)) R.ProcessExitCode = exitCode;
					}
					if (needHandle) R.ProcessHandle = ph;
				}
			}
			finally {
				if (R.ProcessHandle == null) {
					if (ph != null) ph.Dispose();
					else x.hProcess.Dispose();
				}
			}

			return R;

			//tested: works well in MTA thread.
			//rejected: in QM2, run also has a 'window' parameter. However it just makes limited, unclear etc, and therefore rarely used. Instead use wnd.findOrRun or Find/Run/Wait like in the examples.
			//rejected: in QM2, run also has 'autodelay'. Better don't add such hidden things. Let the script decide what to do.
		}

		/// <summary>
		/// Calls <see cref="it"/> and handles exceptions. All parameters are the same.
		/// If <b>Run</b> throws exception, writes it to the output as warning and returns null.
		/// </summary>
		/// <remarks>
		/// This is useful when you don't care whether <b>Run</b> succeeded and don't want to use try/catch.
		/// Handles only exception of type <see cref="AuException"/>. It is thrown when fails, usually when the file does not exist.
		/// </remarks>
		/// <seealso cref="print.warning"/>
		/// <seealso cref="OWarnings.Disable"/>
		/// <seealso cref="wnd.findOrRun"/>
		[MethodImpl(MethodImplOptions.NoInlining)] //uses stack
		public static RResult itSafe(string s, string args = null, RFlags flags = 0, ROptions dirEtc = null) {
			try {
				return it(s, args, flags, dirEtc);
			}
			catch (AuException e) {
				print.warning(e.Message, 1);
				return null;
			}
		}

		static string _NormalizeFile(bool runConsole, string file, out bool isFullPath, out bool isShellPath) {
			isShellPath = isFullPath = false;
			file = pathname.expand(file);
			if (file.NE()) throw new ArgumentException();
			if (runConsole || !(isShellPath = pathname.IsShellPath_(file))) {
				if (isFullPath = pathname.isFullPath(file)) {
					var fl = runConsole ? PNFlags.DontExpandDosPath : PNFlags.DontExpandDosPath | PNFlags.DontPrefixLongPath;
					file = pathname.Normalize_(file, fl, true);

					//ShellExecuteEx supports long path prefix for exe but not for documents.
					//Process.Start supports long path prefix, except when the exe is .NET.
					if (!runConsole) file = pathname.unprefixLongPath(file);

					if (FileSystemRedirection.IsSystem64PathIn32BitProcess(file) && !filesystem.exists(file)) {
						file = FileSystemRedirection.GetNonRedirectedSystemPath(file);
					}
				} else if (!pathname.isUrl(file)) {
					//ShellExecuteEx searches everywhere except in app folder.
					//Process.Start prefers current directory.
					var s2 = filesystem.searchPath(file);
					if (s2 != null) {
						file = s2;
						isFullPath = true;
					}
				}
			}
			return file;
		}

		/// <summary>
		/// Runs a console program, waits until its process ends, and gets its output text.
		/// This overload writes text lines to the output in real time.
		/// </summary>
		/// <param name="exe">
		/// Path or name of an .exe or .bat file. Can be:
		/// <br/>• Full path. Examples: <c>@"C:\folder\x.exe"</c>, <c>folders.System + "x.exe"</c>, <c>@"%folders.System%\x.exe"</c>.
		/// <br/>• Filename, like <c>"x.exe"</c>. This function calls <see cref="filesystem.searchPath"/>.
		/// <br/>• Path relative to <see cref="folders.ThisApp"/>. Examples: <c>"x.exe"</c>, <c>@"subfolder\x.exe"</c>, <c>@".\subfolder\x.exe"</c>, <c>@"..\folder\x.exe"</c>.
		/// 
		/// <para>
		/// Supports environment variables, like <c>@"%TMP%\x.bat"</c>. See <see cref="pathname.expand"/>.
		/// </para>
		/// </param>
		/// <param name="args">null or command line arguments.</param>
		/// <param name="curDir">
		/// Initial current directory of the new process.
		/// <br/>• If null, uses <c>Directory.GetCurrentDirectory()</c>.
		/// <br/>• Else if "", calls <c>pathname.getDirectory(exe)</c>.
		/// <br/>• Else calls <see cref="pathname.expand"/>.
		/// </param>
		/// <param name="encoding">
		/// Console's text encoding.
		/// If null (default), uses <see cref="Console.OutputEncoding"/>, which by default isn't Unicode. Programs that display Unicode text use <see cref="Encoding.UTF8"/> or <see cref="Encoding.Unicode"/>.
		/// </param>
		/// <returns>The process exit code. Usually a non-0 value means error.</returns>
		/// <exception cref="AuException">Failed, for example file not found.</exception>
		/// <remarks>
		/// The console window is hidden. The text that would be displayed in it is redirected to this function.
		/// 
		/// Console programs have two output text streams - standard output and standard error. This function gets both. Alternatively use <see cref="Process.Start"/>; it gets the output and error streams separately, and some lines may be received in incorrect order in time.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// string v = "example";
		/// int r1 = run.console(@"C:\Test\console1.exe", $@"/an ""{v}"" /etc");
		/// 
		/// int r2 = run.console(s => print.it(s), @"C:\Test\console2.exe");
		/// 
		/// int r3 = run.console(out var text, @"C:\Test\console3.exe", encoding: Encoding.UTF8);
		/// print.it(text);
		/// ]]></code>
		/// </example>
		public static int console(string exe, string args = null, string curDir = null, Encoding encoding = null) {
			return _RunConsole(s => print.it(s), null, exe, args, curDir, encoding, true);
		}

		/// <summary>
		/// Runs a console program, waits until its process ends, and gets its output text when it ends.
		/// </summary>
		/// <param name="output">A variable that receives the output text.</param>
		/// <param name="exe"></param>
		/// <param name="args"></param>
		/// <param name="curDir"></param>
		/// <param name="encoding"></param>
		/// <exception cref="AuException">Failed, for example file not found.</exception>
		public static int console(out string output, string exe, string args = null, string curDir = null, Encoding encoding = null) {
			var b = new StringBuilder();
			var r = _RunConsole(null, b, exe, args, curDir, encoding, false);
			output = b.ToString();
			return r;
		}

		/// <summary>
		/// Runs a console program, waits until its process ends, and gets its output text.
		/// This overload uses a callback function that receives text lines in real time.
		/// </summary>
		/// <param name="output">
		/// A callback function that receives the output text.
		/// Unless <i>rawText</i> true:
		/// <br/>• it isn't called until is retrieved full line with line break characters;
		/// <br/>• it receives single full line at a time, without line break characters.
		/// </param>
		/// <param name="exe"></param>
		/// <param name="args"></param>
		/// <param name="curDir"></param>
		/// <param name="encoding"></param>
		/// <param name="rawText">Call the callback function whenever text is retrieved (don't wait for full line). Pass raw text, in chunks of any size.</param>
		/// <exception cref="AuException">Failed, for example file not found.</exception>
		public static int console(Action<string> output, string exe, string args = null, string curDir = null, Encoding encoding = null, bool rawText = false) {
			return _RunConsole(output, null, exe, args, curDir, encoding, !rawText);
		}

		static unsafe int _RunConsole(Action<string> outAction, StringBuilder outStr, string exe, string args, string curDir, Encoding encoding, bool needLines) {
			exe = _NormalizeFile(true, exe, out _, out _);
			//args = pathname.expand(args); //rejected

			encoding ??= Console.OutputEncoding; //fast. Default is an internal type System.Text.OSEncoding that wraps API GetConsoleOutputCP.
			var decoder = encoding.GetDecoder(); //ensures we'll not get partial multibyte chars (UTF8 etc) at buffer end/start

			var ps = new ProcessStarter_(exe, args, curDir, rawExe: true);

			Handle_ hProcess = default;
			var sa = new Api.SECURITY_ATTRIBUTES(null) { bInheritHandle = 1 };
			if (!Api.CreatePipe(out Handle_ hOutRead, out Handle_ hOutWrite, sa, 0)) throw new AuException(0);

			byte* b = null; //buffer before decoding
			char* c = null; //buffer after decoding
			StringBuilder sb = null; //holds part of line when buffer does not end with newline
			try {
				Api.SetHandleInformation(hOutRead, 1, 0); //remove HANDLE_FLAG_INHERIT

				ps.si.dwFlags |= Api.STARTF_USESTDHANDLES | Api.STARTF_USESHOWWINDOW;
				ps.si.hStdOutput = hOutWrite;
				ps.si.hStdError = hOutWrite;
				ps.flags |= Api.CREATE_NEW_CONSOLE;

				if (!ps.StartL(out var pi, inheritHandles: true)) throw new AuException(0);
				hOutWrite.Dispose(); //important: must be here
				pi.hThread.Dispose();
				hProcess = pi.hProcess;

				//native console API allows any buffer size when writing, but wrappers usually use small buffer, eg .NET 4-5 KB, msvcrt 5 KB, C++ not tested
				const int bSize = 8000, cSize = bSize + 10;
				b = MemoryUtil.Alloc(bSize);

				for (bool skipN = false; ;) {
					if (Api.ReadFile(hOutRead, b, bSize, out int nr)) {
						if (nr == 0) continue;
					} else {
						if (lastError.code != Api.ERROR_BROKEN_PIPE) throw new AuException(0);
						//process ended
						if (sb != null && sb.Length > 0) {
							outAction(sb.ToString());
						}
						break;
					}

					if (c == null) c = MemoryUtil.Alloc<char>(cSize);
					int nc = decoder.GetChars(b, nr, c, cSize, false);

					if (needLines) {
						var k = new Span<char>(c, nc);
						if (skipN) {
							skipN = false;
							if (c[0] == '\n' && nc > 0) k = k[1..]; //\r\n split in 2 buffers
						}
						while (k.Length > 0) {
							int i = k.IndexOfAny('\n', '\r');
							if (i < 0) {
								(sb ??= new()).Append(k);
								break;
							}
							string s;
							if (sb != null && sb.Length > 0) {
								sb.Append(k[0..i]);
								s = sb.ToString();
								sb.Clear();
							} else {
								s = k[0..i].ToString();
							}
							outAction(s);
							if (k[i++] == '\r') {
								if (i == k.Length) skipN = true;
								else if (k[i] == '\n') i++;
							}
							k = k[i..];
						}
					} else if (nc > 0) {
						if (outAction != null) {
							outAction(new(c, 0, nc));
						} else {
							outStr.Append(c, nc);
						}
					}
				}

				if (!Api.GetExitCodeProcess(hProcess, out int exitCode)) exitCode = int.MinValue;
				return exitCode;
			}
			finally {
				hProcess.Dispose();
				hOutRead.Dispose();
				hOutWrite.Dispose();
				MemoryUtil.Free(b);
				MemoryUtil.Free(c);
			}
		}

		/// <summary>
		/// Opens parent folder in File Explorer (folder window) and selects the file.
		/// </summary>
		/// <returns>false if fails, for example if the file does not exist.</returns>
		/// <param name="path">
		/// Full path of a file or directory or other shell object.
		/// Supports <c>@"%environmentVariable%\..."</c> (see <see cref="pathname.expand"/>) and <c>"::..."</c> (see <see cref="Pidl.ToHexString"/>).
		/// </param>
		public static bool selectInExplorer(string path) {
			using var pidl = Pidl.FromString(path);
			if (pidl == null) return false;
			return 0 == Api.SHOpenFolderAndSelectItems(pidl.HandleRef, 0, null, 0);
		}

		/// <summary>
		/// Starts new thread: creates new <see cref="Thread"/> object, sets some properties and calls <see cref="Thread.Start"/>.
		/// Returns the <b>Thread</b> variable.
		/// </summary>
		/// <param name="threadProc">Thread procedure. Parameter <i>start</i> of <b>Thread</b> constructor.</param>
		/// <param name="background">
		/// If true (default), sets <see cref="Thread.IsBackground"/> = true.
		/// The process ends when the main thread and all foreground threads end; background threads then are terminated.
		/// </param>
		/// <param name="sta">If true (default), sets <see cref="ApartmentState.STA"/>.</param>
		/// <exception cref="OutOfMemoryException"></exception>
		public static Thread thread(Action threadProc, bool background = true, bool sta = true) {
			var t = new Thread(threadProc.Invoke);
			if (background) t.IsBackground = true;
			if (sta) t.SetApartmentState(ApartmentState.STA);
			t.Start();
			return t;
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="run.it"/>.
	/// </summary>
	[Flags]
	public enum RFlags {
		/// <summary>
		/// Show error message box if fails, for example if file not found.
		/// Note: this does not disable exceptions. To avoid exceptions use try/catch or <see cref="run.itSafe"/>.
		/// </summary>
		ShowErrorUI = 1,

		/// <summary>
		/// If started new process, wait until it exits.
		/// </summary>
		WaitForExit = 2,

		/// <summary>
		/// If started new process, get process handle (<see cref="RResult.ProcessHandle"/>).
		/// </summary>
		NeedProcessHandle = 4,

		/// <summary>
		/// Run new process as administrator.
		/// If this process isn't admin:
		/// <br/>• Shows UAC consent dialog.
		/// <br/>• Uses verb "runas", therefore other verb cannot be specified.
		/// <br/>• Cannot set current directory for the new process.
		/// <br/>• The new process does not inherit environment variables of this process.
		/// </summary>
		Admin = 8,

		/// <summary>
		/// If this process runs as administrator, run new process as administrator too.
		/// Without this flag, if this process runs as administrator:
		/// <br/>• Starts new process as non-administrator from the shell process (explorer.exe).
		/// <br/>• If it fails (for example if shell process isn't running), calls <see cref="print.warning"/> and starts new process as administrator.
		/// <br/>• The new process does not inherit environment variables of this process.
		/// </summary>
		InheritAdmin = 16,

		/// <summary>
		/// Add the app to the "Most used" list in the Start menu if launched often.
		/// </summary>
		MostUsed = 32,
	}

	/// <summary>
	/// More parameters for <see cref="run.it"/>.
	/// </summary>
	/// <remarks>
	/// Implicit conversion from <b>string</b> sets <see cref="CurrentDirectory"/>.
	/// </remarks>
	public class ROptions {
		/// <summary>
		/// Sets <see cref="CurrentDirectory"/>.
		/// </summary>
		public static implicit operator ROptions(string curDir) => new ROptions { CurrentDirectory = curDir };

		/// <summary>
		/// Initial current directory for the new process.
		/// If null (default), the new process will inherit the curent directory of this process.
		/// If "", the function gets parent directory path from the <i>file</i> parameter, if possible (if full path is specified or found). If not possible, same as null.
		/// <note>Some programs look for their files in current directory and fail to start if it is not the program's directory.</note>
		/// </summary>
		public string CurrentDirectory;

		/// <summary>
		/// File's right-click menu command, also known as verb. For example "edit", "print", "properties". The default verb is bold in the menu.
		/// Not all menu items will work. Some may have different name than in the menu.
		/// </summary>
		public string Verb;

		/// <summary>
		/// Owner window for error message boxes.
		/// Also, new window should be opened on the same screen. However many programs ignore it.
		/// </summary>
		public AnyWnd OwnerWindow;

		/// <summary>
		/// Preferred window state.
		/// Many programs ignore it.
		/// </summary>
		public ProcessWindowStyle WindowState;

		/// <summary>
		/// Flags to add to <msdn>SHELLEXECUTEINFO</msdn> field <b>fMask</b>.
		/// Default flags: SEE_MASK_NOZONECHECKS, SEE_MASK_NOASYNC, SEE_MASK_NOCLOSEPROCESS, SEE_MASK_CONNECTNETDRV, SEE_MASK_UNICODE, SEE_MASK_FLAG_NO_UI (if no flag <b>ShowErrorUI</b>), SEE_MASK_NO_CONSOLE (if no flag <b>WaitForExit</b>), SEE_MASK_FLAG_LOG_USAGE (if flag <b>MostUsed</b>); also SEE_MASK_INVOKEIDLIST if need.
		/// </summary>
		public uint FlagsAdd;

		/// <summary>
		/// Flags to remove from <msdn>SHELLEXECUTEINFO</msdn> field <b>fMask</b>.
		/// Default flags: see <see cref="FlagsAdd"/>.
		/// </summary>
		public uint FlagsRemove;

		//no. If need, caller can get window and call EnsureInScreen etc.
		//public screen Screen;
		//this either does not work or I could not find a program that uses default window position (does not save/restore)
		//if(!more.Screen.IsNull) { x._14.hMonitor = more.Screen.ToDevice().Handle; x.fMask |= Api.SEE_MASK_HMONITOR; }
	}

	/// <summary>
	/// Results of <see cref="run.it"/>.
	/// </summary>
	public class RResult {
		/// <summary>
		/// The exit code of the process.
		/// 0 if no flag <b>WaitForExit</b> or if cannot wait.
		/// </summary>
		/// <remarks>
		/// Usually the exit code is 0 or a process-defined error code.
		/// </remarks>
		public int ProcessExitCode { get; internal set; }

		/// <summary>
		/// The process id.
		/// 0 if used flag <b>WaitForExit</b> or if did not start new process (eg opened the document in an existing process) or if cannot get it.
		/// </summary>
		public int ProcessId { get; internal set; }

		/// <summary>
		/// If used flag <b>NeedProcessHandle</b>, contains process handle. Later the <see cref="WaitHandle"/> variable must be disposed.
		/// null if no flag or if did not start new process (eg opened the document in an existing process) or if cannot get it.
		/// </summary>
		/// <example>
		/// This code does the same as <c>run.it(@"notepad.exe", flags: SRFlags.WaitForExit);</c>
		/// <code><![CDATA[
		/// var r = run.it(@"notepad.exe", flags: SRFlags.NeedProcessHandle);
		/// using(var h = r.ProcessHandle) h?.WaitOne();
		/// ]]></code>
		/// </example>
		public WaitHandle ProcessHandle { get; internal set; }

		/// <summary>
		/// Returns <see cref="ProcessId"/> as string.
		/// </summary>
		public override string ToString() {
			return ProcessId.ToString();
		}
	}
}
