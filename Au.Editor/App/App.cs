using Au.Controls;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Threading;

[assembly: AssemblyTitle(App.AppNameLong)]
//more attributes in global2.cs

static partial class App {
	public const string
		AppNameLong = "LibreAutomate C#",
		AppNameShort = "LibreAutomate"; //must be without spaces etc
	
	internal static PrintServer PrintServer;
	public static AppSettings Settings;
	public static KMenuCommands Commands;
	public static FilesModel Model;
	public static RunningTasks Tasks;
	static EnvVarUpdater _envVarUpdater;
	
	//[STAThread] //no, makes command line etc slower. Will set STA later.
	static int Main(string[] args) {
#if DEBUG //note: not static ctor. Eg Settings used in scripts while creating some new parts of the app. The ctor would run there.
		//perf.first();
		//timer.after(1, _ => perf.nw());
		print.qm2.use = true;
		//print.clear(); 
		//print.redirectConsoleOutput = true; //cannot be before the CommandLine.ProgramStarted1 call.
#endif
		
		if (CommandLine.ProgramStarted1(args, out int exitCode)) return exitCode;
		
		//restart as admin if started as non-admin on admin user account
		if (args.Length > 0 && args[0] is "/n" or "-n") {
			args = args.RemoveAt(0);
			_raaResult = WinTaskScheduler.RResult.ArgN;
		} else if (uacInfo.ofThisProcess.Elevation == UacElevation.Limited) {
			if (_RestartAsAdmin(args)) return 0;
		}
		
		//Debug_.PrintLoadedAssemblies(true, !true);
		
		_Main(args);
		return 0;
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	static void _Main(string[] args) {
		//Debug_.PrintLoadedAssemblies(true, !true);
		//perf.next();
		
		AppDomain.CurrentDomain.UnhandledException += _UnhandledException;
		process.ThisThreadSetComApartment_(ApartmentState.STA);
		process.thisProcessCultureIsInvariant = true;
		DebugTraceListener.Setup(usePrint: true);
		Directory.SetCurrentDirectory(folders.ThisApp); //it is c:\windows\system32 when restarted as admin
		Api.SetSearchPathMode(Api.BASE_SEARCH_PATH_ENABLE_SAFE_SEARCHMODE); //let SearchPath search in current directory after system directories
		Api.SetErrorMode(Api.SEM_FAILCRITICALERRORS); //disable some error message boxes, eg when removable media not found; MSDN recommends too.
		_SetThisAppFoldersEtc();
		dialog.options.defaultTitle = AppNameShort + " message";
		
		if (CommandLine.ProgramStarted2(args)) return;
		
		PrintServer = new(
#if IDE_LA
			!
#endif
			true) { NoNewline = true };
		PrintServer.Start();
#if DEBUG
		print.qm2.use = !true;
		//timer.after(1, _ => perf.nw());
		_RemindToBuild32bit();
#endif
		_envVarUpdater = new();
		
		//perf.next('o');
		Settings = AppSettings.Load();
		//perf.next('s');
		//Debug_.PrintLoadedAssemblies(true, !true);
		
		AssemblyLoadContext.Default.Resolving += _Assembly_Resolving;
		AssemblyLoadContext.Default.ResolvingUnmanagedDll += _UnmanagedDll_Resolving;
		
		Tasks = new RunningTasks();
		//perf.next('t');
		
		ScriptEditor.IconNameToXaml_ = DIcons.GetIconString;
		FilesModel.LoadWorkspace(CommandLine.WorkspaceDirectory);
		//perf.next('W');
		CommandLine.ProgramLoaded();
		//perf.next('c');
		Loaded = AppState.LoadedWorkspace;
		
		TrayIcon.Update_();
		//perf.next('i');
		
		_app = new() { ShutdownMode = ShutdownMode.OnMainWindowClose };
		AppDomain.CurrentDomain.UnhandledException -= _UnhandledException;
		if (!Debugger.IsAttached)
			_app.DispatcherUnhandledException += (_, e) => {
				e.Handled = 1 == dialog.showError("Exception", e.Exception.ToStringWithoutStack(), "1 Continue|2 Exit", DFlags.Wider, Hmain, e.Exception.ToString());
			};
		//perf.next('a');
		
		_app.MainWindow = Wmain = new MainWindow();
		//perf.next('w');
		if (!Settings.runHidden || CommandLine.StartVisible) ShowWindow();
		//perf.next('W');
		
		s_timer = timer.every(1000, _TimerProc);
		//note: timer can make Process Hacker/Explorer show CPU usage, even if we do nothing. Eg 0.02 if 250, 0.01 if 500, <0.01 if 1000.
		//Timer1s += () => print.it("1 s");
		//Timer1sOr025s += () => print.it("0.25 s");
		
		_app.Dispatcher.InvokeAsync(() => {
			//perf.next('r');
			Model.RunStartupScripts(false);
			//perf.nw('s');
		});
		
		try {
			_app.Run();
			//Hidden app should start as fast as possible, because usually starts with Windows.
			//Tested with native message loop (before show window). Faster by 70 ms (240 vs 310 without the .NET startup time).
			//	But then problems. Eg cannot auto-create main window synchronously, because need to exit native loop and start WPF loop.
		}
		catch (Exception e1) when (!Debugger.IsAttached) {
			s_timer.Stop();
			Wmain.Close();
			dialog.showError("Exception", e1.ToString(), flags: DFlags.Wider);
		}
		finally { MainFinally_(); }
	}
	
	internal static void MainFinally_() {
		if (Loaded == AppState.Unloaded) return;
		Loaded = AppState.Unloading;
		
		s_timer.Stop(); //eg if will show a Debug.Assert dialog
		
		var fm = Model; Model = null;
		fm.Dispose(); //stops tasks etc
		
		Loaded = AppState.Unloaded;
		
		PrintServer.Stop();
	}
	
	class _Application : Application {
		protected override void OnSessionEnding(SessionEndingCancelEventArgs e) {
			base.OnSessionEnding(e);
			Wmain.Close();
			MainFinally_();
			process.thisProcessExitInvoke(); //OS terminates this process before or during process.thisProcessExit event
		}
	}
	
	//public static Application Instance => _app; //Application.Current
	static _Application _app;
	
	/// <summary>
	/// <b>Dispatcher</b> of main thread.
	/// </summary>
	public static Dispatcher Dispatcher => _app.Dispatcher;
	
	/// <summary>
	/// Main window.
	/// Not loaded if never was visible.
	/// Use only in main thread; if other threads need <b>Dispatcher</b> of main thread, use <see cref="Dispatcher"/>.
	/// </summary>
	public static MainWindow Wmain { get; private set; }
	
	/// <summary>
	/// Main window handle.
	/// defaul(wnd) if never was visible.
	/// </summary>
	public static wnd Hmain { get; internal set; }
	
	public static void ShowWindow() {
		//workaround for WPF bug: Window.Show pumps posted messages.
		//	And crashes if a message causes to call Show again.
		//	To reproduce, let the program start hidden, and double-click the tray icon.
		if (_sw1) return;
		
		_sw1 = true;
		Wmain.Show(); //auto-creates MainWindow if never was visible
		_sw1 = false;
		Hmain.ActivateL(true);
	}
	static bool _sw1;
	
	static void _UnhandledException(object sender, UnhandledExceptionEventArgs e) {
#if DEBUG
		print.qm2.write(e.ExceptionObject);
#else
		dialog.showError("Exception", e.ExceptionObject.ToString(), flags: DFlags.Wider);
#endif
	}
	
	private static Assembly _Assembly_Resolving(AssemblyLoadContext alc, AssemblyName an) {
		var dlls = s_arDlls ??= filesystem.enumFiles(folders.ThisAppBS + "Roslyn", "*.dll", FEFlags.UseRawPath)
			.ToDictionary(o => o.Name[..^4], o => o.FullPath);
		if (dlls.TryGetValue(an.Name, out var path)) return alc.LoadFromAssemblyPath(path);
		
		if (_FindEditorExtensionInStack(out var asm)) return MiniProgram_.ResolveAssemblyFromRefPathsAttribute_(alc, an, asm);
		
		//print.qm2.write(an); 
		return alc.LoadFromAssemblyPath(folders.ThisAppBS + an.Name + ".dll");
	}
	static Dictionary<string, string> s_arDlls;
	
	//resolve native dlls used by meta pr libraries that are used by editorExtension scripts.
	//	These libraries are loaded in default context.
	//	editorExtension assemblies are loaded in other contexts.
	//	Dlls directly used by editorExtension assemblies are resolved in RunAssembly.Run.
	private static IntPtr _UnmanagedDll_Resolving(Assembly _, string name) {
		if (_FindEditorExtensionInStack(out var asm)) return MiniProgram_.ResolveUnmanagedDllFromNativePathsAttribute_(name, asm);
		return default;
	}
	
	static bool _FindEditorExtensionInStack(out Assembly asm) {
		var st = new StackTrace(2); //not too slow
		for (int i = 0; ; i++) {
			var f = st.GetFrame(i); if (f == null) break;
			asm = f.GetMethod()?.DeclaringType?.Assembly;
			if (asm != null && asm.GetName().Name.Contains('|')) return true; //ScriptName|GUID
		}
		asm = null;
		return false;
	}
	
	static void _SetThisAppFoldersEtc() {
		if (filesystem.exists(folders.ThisAppBS + "data").Directory) {
			IsPortable = true;
			ScriptEditor.IsPortable = true;
		} else {
			//The app name was changed several times during the preview period.
			//	If was installed with an old name, rename the app doc directory instead of creating new.
			//	It contains app settings and probably the workspace folder.
			//	FUTURE: delete this code.
			var doc = folders.Documents + AppNameShort;
			if (!filesystem.exists(doc)) {
				try {
					foreach (var v in new[] { "Uiscripter", "Automaticode", "Autepad", "Derobotizer" }) {
						var s = folders.Documents + v;
						if (filesystem.exists(s)) {
							filesystem.move(s, doc);
							
							//rejected.
							//filesystem.more.createSymbolicLink(s, thisAppDoc, CSLink.Junction);
							
							var f = doc + @"\.settings\Settings.json";
							var text = File.ReadAllText(f);
							text = text.Replace($@"\\{v}\\", $@"\\{AppNameShort}\\");
							File.WriteAllText(f, text);
							
							break;
						}
					}
				}
				catch { }
			}
		}
		
		script.role = SRole.EditorExtension;
		folders.Editor = folders.ThisApp;
		
		try {
			//create now if does not exist
			_ = folders.ThisAppDocuments;
			_ = folders.ThisAppDataLocal;
			_ = folders.ThisAppTemp;
			//these are currently not used in editor and library, but may be used in role editorExtension scripts. Just prevent changing.
			folders.noAutoCreate = true;
			_ = folders.ThisAppDataRoaming;
			_ = folders.ThisAppDataCommon;
			_ = folders.ThisAppImages;
			folders.noAutoCreate = false;
		}
		catch (Exception e1) {
			dialog.showError("Failed to set app folders", e1.ToString());
			//throw;
			Environment.Exit(1);
		}
	}
	
#if DEBUG
	static void _RemindToBuild32bit() {
		if (IsAuAtHome)
			if (filesystem.getProperties(folders.ThisAppBS + @"..\Cpp", out var p64)
				&& filesystem.getProperties(folders.ThisAppBS + @"32\AuCpp.dll", out var p32)) {
				var v = p64.LastWriteTimeUtc - p32.LastWriteTimeUtc;
				if (v > default(TimeSpan)) print.it("Note: may need to build 32-bit AuCpp.dll.");
			}
	}
#endif
	
	internal static void OnMainWindowLoaded_() {
		if (IsPortable) {
			print.it($"<>Info: <help editor/Portable app>portable mode<>. Using <link {folders.ThisAppBS + "data"}>data<> folder.");
		} else {
			//in v0.12 changed some spec folders from "...\Au" to "...\LibreAutomate\_script"
			//	FUTURE: delete this code.
			_Folder(folders.Documents, "folders.ThisAppDocuments");
			_Folder(folders.LocalAppData, "folders.ThisAppDataLocal");
			static void _Folder(string dir, string name) {
				var dir1 = dir + @"\Au";
				if (filesystem.exists(dir1, useRawPath: true)) {
					var dir2 = dir + @"\LibreAutomate\_script";
					if (!filesystem.exists(dir2, useRawPath: true)) {
						try {
							filesystem.copy(dir1, dir2);
							print.it($"""
							<>Note: in this program version has been changed <help>{name}<> path.
								Old: <explore>{dir1}<>. The folder is no longer used. You can delete it.
								New: <explore>{dir2}<>. The old folder has been copied here.
							""");
						}
						catch { }
					}
				}
			}
			
			if (_raaResult is not (WinTaskScheduler.RResult.None or WinTaskScheduler.RResult.ArgN)) {
				var s1 = _raaResult == WinTaskScheduler.RResult.TaskNotFound ? null : $"\r\n\tFailed to run as administrator. Error: {_raaResult}.";
				var s = $"""
<>Info: running not as administrator. <fold>
	Without admin rights this program cannot automate admin windows etc. See <help articles/UAC>UAC<>.{s1}
	Restart as administrator: <+restartAdmin >now<>, <+restartAdmin /raa>now and always<> (later without UAC consent).
	</fold>
""";
				print.it(s);
				Panels.Output.Scintilla.AaTags.AddLinkTag("+restartAdmin", k => Restart(k, admin: true));
			} else if (CommandLine.Raa) { //restarted because clicked link "Restart as administrator: now and always"
				var name = IsAuAtHome ? "_Au.Editor" : "Au.Editor";
				bool ok = WinTaskScheduler.CreateTaskWithoutTriggers("Au", name, UacIL.System, process.thisExePath, "/s $(Arg0)", AppNameShort);
				if (!ok) print.warning(@"Failed to create Windows Task Scheduler task \Au\Au.Editor.", -1);
				
				//note: don't create the task in the setup program. It requires a C++ dll, and it triggers AV false positives.
			}
		}
	}
	
	internal static void OnMainWindowClosed_() {
		s_timer.Stop();
	}
	
	static WinTaskScheduler.RResult _raaResult;
	
	static bool _RestartAsAdmin(string[] args) {
		if (Debugger.IsAttached) return false; //very fast
		bool home = IsAuAtHome;
		string sesId = process.thisProcessSessionId.ToS();
		args = args.Length == 0 ? [sesId] : args.InsertAt(0, sesId);
		(int pid, _raaResult) = WinTaskScheduler.RunTask("Au",
			home ? "_Au.Editor" : "Au.Editor", //in C:\code\au\_ or <installed path>
			process.thisExePath, true, args);
		if (pid == 0) { //probably this program is not installed (no scheduled task)
			if (home) print.qm2.write("failed to run as admin", _raaResult);
			return false;
		}
		//Api.AllowSetForegroundWindow(pid); //fails and has no sense
		return true;
	}
	
	/// <summary>
	/// Restarts this program.
	/// </summary>
	/// <param name="commandLine">Command line arguments to append. Don't use /n and /v because this func manages it.</param>
	/// <param name="admin">UAC-elevate (verb runas).</param>
	public static void Restart(string commandLine = null, bool admin = false) {
		Debug.Assert(Loaded == AppState.LoadedUI);
		var cl = Hmain.IsVisible ? "/n /v /restart " : "/n /restart";
		if (!commandLine.NE()) cl = cl + " " + commandLine;
		process.thisProcessExit += _ => { run.it(process.thisExePath, cl, admin ? RFlags.Admin : RFlags.InheritAdmin); };
		_app.Shutdown(); //closes window async, with no possibility to cancel
	}
	
	/// <summary>
	/// Timer with 1 s period.
	/// </summary>
	public static event Action Timer1s;
	
	/// <summary>
	/// Timer with 1 s period when main window hidden and 0.25 s period when visible.
	/// </summary>
	public static event Action Timer1sOr025s;
	
	/// <summary>
	/// Timer with 0.25 s period, only when main window visible.
	/// </summary>
	public static event Action Timer025sWhenVisible;
	
	/// <summary>
	/// Timer with 1 s period, only when main window visible.
	/// </summary>
	public static event Action Timer1sWhenVisible;
	
	/// <summary>
	/// True if Timer1sOr025s period is 0.25 s (when main window visible), false if 1 s (when hidden).
	/// </summary>
	public static bool IsTimer025 => s_timerCounter > 0;
	static uint s_timerCounter;
	
	static void _TimerProc(timer t) {
		Timer1sOr025s?.Invoke();
		bool needFast = Wmain.IsVisible;
		if (needFast != (s_timerCounter > 0)) t.Every(needFast ? 250 : 1000);
		if (needFast) {
			Timer025sWhenVisible?.Invoke();
			s_timerCounter++;
		} else s_timerCounter = 0;
		if (0 == (s_timerCounter & 3)) {
			Timer1s?.Invoke();
			if (needFast) Timer1sWhenVisible?.Invoke();
		}
	}
	static timer s_timer;
	
	public static AppState Loaded;
	
	/// <summary>
	/// Gets Keyboard.FocusedElement. If null, and a HwndHost-ed control is focused, returns the HwndHost.
	/// Slow if HwndHost-ed control.
	/// </summary>
	public static FrameworkElement FocusedElement {
		get {
			var v = System.Windows.Input.Keyboard.FocusedElement;
			if (v != null) return v as FrameworkElement;
			return wnd.Internal_.ToWpfElement(Api.GetFocus());
		}
	}
	
	public static bool IsAuAtHome { get; } = Api.EnvironmentVariableExists("Au.Home<PC>") && folders.ThisAppBS.Eqi(@"C:\code\au\_\");
	
	public static bool IsPortable { get; private set; }
	
	public static async void CheckForUpdates(System.Windows.Controls.Button b = null) {
		bool forceNow = b != null;
		int day = (int)(DateTime.Now.Ticks / 864000000000);
		if (!forceNow && day == App.Settings.checkForUpdatesDay) return;
		App.Settings.checkForUpdatesDay = day;
		
		if (forceNow) b.IsEnabled = false;
		try {
			var r = await internet.http.GetAsync("https://www.libreautomate.com/version.txt");
			r.EnsureSuccessStatusCode();
			var s = await r.Content.ReadAsStringAsync();
			s = s.Lines()[0];
			if (s != Au_.Version && System.Version.TryParse(Au_.Version, out var v1) && System.Version.TryParse(s, out var v2) && v2 > v1) {
				//Panels.Output.Scintilla.AaTags.AddLinkTag("+appUpdate", _Update);
				//print.it($"<>{AppNameShort} {s} is available. The installed version is {Au_.Version}.  [<+appUpdate>update...<>]  [<link https://github.com/qgindi/LibreAutomate/tree/master/Other/DocFX/_doc/changes>changes<>]  [<link https://www.libreautomate.com>website<>]");
				print.it($"<>{AppNameShort} {s} is available. The installed version is {Au_.Version}.  [<link https://github.com/qgindi/LibreAutomate/tree/master/Other/DocFX/_doc/changes>changes<>]  [<link https://www.libreautomate.com>download<>]");
			} else if (forceNow) {
				dialog.showInfo(null, $"{AppNameShort} is up to date. Version {Au_.Version}.", owner: Hmain);
			}
		}
		catch (Exception e1) { if (forceNow) print.warning(e1); }
		finally { if (forceNow) b.IsEnabled = true; }
		
		//static async Task _Update(string s) {
		//	if (!dialog.showOkCancel(null, $"This will download and install the new {AppNameShort} version.")) return;
		//	try {
		
		//	}
		//	catch (Exception e1) { print.warning(e1); }
		//}
	}
}

enum AppState {
	/// <summary>
	/// Before the first workspace fully loaded.
	/// </summary>
	Loading,
	
	/// <summary>
	/// The first workspace is fully loaded etc, but the main window not.
	/// </summary>
	LoadedWorkspace,
	
	/// <summary>
	/// The main window is loaded and either visible now or was visible and now hidden.
	/// </summary>
	LoadedUI,
	
	/// <summary>
	/// Unloading workspace, stopping everything.
	/// </summary>
	Unloading,
	
	/// <summary>
	/// Main window closed, workspace unloaded, everything stopped.
	/// </summary>
	Unloaded,
}
