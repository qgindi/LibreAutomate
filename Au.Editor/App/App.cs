using Au.Controls;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Threading;
using System.Linq;

static class App {
	public const string
		AppNameLong = "C# Uiscripter",
		AppNameShort = "Uiscripter"; //must be without spaces etc

	public static string UserGuid;
	internal static print.Server PrintServer;
	public static AppSettings Settings;
	public static KMenuCommands Commands;
	public static FilesModel Model;
	public static RunningTasks Tasks;

	//[STAThread] //no, makes command line etc slower. Will set STA later.
	static int Main(string[] args) {
#if TRACE //note: not static ctor. Eg Settings used in scripts while creating some new parts of the app, eg recorder. The ctor would run there.
		perf.first();
		//timer.after(1, _ => perf.nw());
		print.qm2.use = true;
		//print.clear();
		//print.redirectConsoleOutput = true; //cannot be before the CommandLine.ProgramStarted1 call.
#endif

		if (CommandLine.ProgramStarted1(args, out int exitCode)) return exitCode;

		//restart as admin if started as non-admin on admin user account
		if (args.Length > 0 && args[0] == "/n") {
			args = args.RemoveAt(0);
		} else if (uacInfo.ofThisProcess.Elevation == UacElevation.Limited) {
			if (_RestartAsAdmin(args)) return 0;
		}

		//Debug_.PrintLoadedAssemblies(true, !true);
		_Main(args);
		return 0;
	}

	static void _Main(string[] args) {
		//Debug_.PrintLoadedAssemblies(true, !true);

		AppDomain.CurrentDomain.UnhandledException += _UnhandledException;
		process.ThisThreadSetComApartment_(ApartmentState.STA);
		process.thisProcessCultureIsInvariant = true;
		DebugTraceListener.Setup(usePrint: true);
		Directory.SetCurrentDirectory(folders.ThisApp); //it is c:\windows\system32 when restarted as admin
		Api.SetSearchPathMode(Api.BASE_SEARCH_PATH_ENABLE_SAFE_SEARCHMODE); //let SearchPath search in current directory after system directories
		Api.SetErrorMode(Api.GetErrorMode() | Api.SEM_FAILCRITICALERRORS); //disable some error message boxes, eg when removable media not found; MSDN recommends too.
		_SetThisAppDocuments();

		if (CommandLine.ProgramStarted2(args)) return;

		PrintServer = new print.Server(true) { NoNewline = true };
		PrintServer.Start();
#if TRACE
		print.qm2.use = !true;
		//timer.after(1, _ => perf.nw());
#endif

		perf.next('o');
		Settings = AppSettings.Load(); //the slowest part, >50 ms. Loads many dlls.
									   //Debug_.PrintLoadedAssemblies(true, !true);
		perf.next('s');
		UserGuid = Settings.user; if (UserGuid == null) Settings.user = UserGuid = Guid.NewGuid().ToString();

		AssemblyLoadContext.Default.Resolving += _Assembly_Resolving;
		AssemblyLoadContext.Default.ResolvingUnmanagedDll += _UnmanagedDll_Resolving;

		Tasks = new RunningTasks();
		perf.next('t');

		script.editor.IconNameToXaml_ = (s, what) => DIcons.GetIconString(s, what);
		FilesModel.LoadWorkspace(CommandLine.WorkspaceDirectory);
		perf.next('W');
		CommandLine.ProgramLoaded();
		perf.next('c');
		Loaded = EProgramState.LoadedWorkspace;

		timer.every(1000, t => _TimerProc(t));
		//note: timer can make Process Hacker/Explorer show CPU usage, even if we do nothing. Eg 0.02 if 250, 0.01 if 500, <0.01 if 1000.
		//Timer1s += () => print.it("1 s");
		//Timer1sOr025s += () => print.it("0.25 s");

		TrayIcon.Update_();
		perf.next('i');

		_app = new() {
			ShutdownMode = ShutdownMode.OnExplicitShutdown //will set OnMainWindowClose when creating main window. If now, would exit if a startup script shows/closes a WPF window.
		};
		_app.Dispatcher.InvokeAsync(() => Model.RunStartupScripts());
		if (!Settings.runHidden || CommandLine.StartVisible) _app.Dispatcher.Invoke(() => ShowWindow());
		try {
			_app.Run();
			//Hidden app should start as fast as possible, because usually starts with Windows.
			//Tested with native message loop. Faster by 70 ms (240 vs 310 without the .NET startup time).
			//	But then problems. Eg cannot auto-create main window synchronously, because need to exit native loop and start WPF loop.
		}
		finally {
			Loaded = EProgramState.Unloading;
			var fm = Model; Model = null;
			fm.Dispose(); //stops tasks etc
			Loaded = EProgramState.Unloaded;

			PrintServer.Stop();
		}
	}

	/// <summary>
	/// WPF <b>Application</b> of main thread.
	/// </summary>
	public static Au.Editor.WpfApp WpfApp => _app;
	static Au.Editor.WpfApp _app;

	/// <summary>
	/// WpfApp.Dispatcher.
	/// </summary>
	public static Dispatcher Dispatcher => _app.Dispatcher;

	/// <summary>
	/// Main window.
	/// Auto-creates if this property never was accessed or if the main window never was visible; but does not show and does not create hwnd.
	/// Use only in main thread; if other threads need <b>Dispatcher</b> of main thread, use that of <see cref="WpfApp"/>.
	/// </summary>
	public static MainWindow Wmain {
		get {
			if (_wmain == null) {
				AppDomain.CurrentDomain.UnhandledException -= _UnhandledException;
				_app.DispatcherUnhandledException += (_, e) => {
					e.Handled = 1 == dialog.showError("Exception", e.Exception.ToStringWithoutStack(), "1 Continue|2 Exit", DFlags.Wider, Hmain, e.Exception.ToString());
				};
				_app.InitializeComponent();
				_app.MainWindow = _wmain = new MainWindow();
				_app.ShutdownMode = ShutdownMode.OnMainWindowClose;
				_wmain.Init();
			}
			return _wmain;
		}
	}
	static MainWindow _wmain;

	/// <summary>
	/// Main window handle.
	/// defaul(wnd) if never was visible.
	/// </summary>
	public static wnd Hmain;

	public static void ShowWindow() {
		Wmain.Show(); //auto-creates MainWindow if never was visible
		Hmain.ActivateL(true);
	}

	private static void _UnhandledException(object sender, UnhandledExceptionEventArgs e) {
#if TRACE
		print.qm2.write(e.ExceptionObject);
#else
		dialog.showError("Exception", e.ExceptionObject.ToString(), flags: DFlags.Wider);
#endif
	}

	private static Assembly _Assembly_Resolving(AssemblyLoadContext alc, AssemblyName an) {
		var dlls = s_arDlls ??= filesystem.enumFiles(folders.ThisAppBS + "Roslyn", "*.dll", FEFlags.UseRawPath)
			.ToDictionary(o => o.Name[..^4], o => o.FullPath);
		if (dlls.TryGetValue(an.Name, out var path)) return alc.LoadFromAssemblyPath(path);

		//is it used by an editorExtension script?
		var st = new StackTrace(2); //not too slow
		for (int i = 0; ; i++) {
			var f = st.GetFrame(i); if (f == null) break;
			var asm = f.GetMethod()?.DeclaringType?.Assembly;
			if (asm.GetName().Name.Contains('|')) //ScriptName|GUID
				return MiniProgram_.ResolveAssemblyFromRefPathsAttribute_(alc, an, asm);
		}

		//print.qm2.write(an);
		return alc.LoadFromAssemblyPath(folders.ThisAppBS + an.Name + ".dll");
	}
	static Dictionary<string, string> s_arDlls;

	private static IntPtr _UnmanagedDll_Resolving(Assembly _, string name) {
		//is it used by an editorExtension script?
		var st = new StackTrace(2); //not too slow
		for (int i = 0; ; i++) {
			var f = st.GetFrame(i); if (f == null) break;
			var asm = f.GetMethod()?.DeclaringType?.Assembly;
			if (asm.GetName().Name.Contains('|')) //ScriptName|GUID
				return MiniProgram_.ResolveUnmanagedDllFromNativePathsAttribute_(name, asm);
		}

		return default;
	}

	static void _SetThisAppDocuments() {
		string thisAppDoc = folders.Documents + AppNameShort;

		//The app name was changed several times during the preview period.
		//	If was installed with an old name, rename the app doc directory instead of creating new.
		//	It contains app settings and probably the workspace folder.
		//	FUTURE: delete this code.
		if (!filesystem.exists(thisAppDoc)) {
			try {
				string s;
				if (filesystem.exists(s = folders.Documents + "Automaticode")) filesystem.move(s, thisAppDoc);
				else if (filesystem.exists(s = folders.Documents + "Autepad")) filesystem.move(s, thisAppDoc);
				else if (filesystem.exists(s = folders.Documents + "Derobotizer")) filesystem.move(s, thisAppDoc);
			}
			catch { }
		}

		folders.ThisAppDocuments = (FolderPath)thisAppDoc; //creates if does not exist
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
		bool needFast = _wmain?.IsVisible ?? false;
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

	public static EProgramState Loaded;

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

	static bool _RestartAsAdmin(string[] args) {
		if (Debugger.IsAttached) return false; //very fast
		try {
			string sesId = process.thisProcessSessionId.ToS();
			args = args.Length == 0 ? new[] { sesId } : args.InsertAt(0, sesId);

			bool isAuHomePC = Api.EnvironmentVariableExists("Au.Home<PC>") && !folders.ThisAppBS.Starts(@"C:\Program Files", true);

			//int pid = 
			WinTaskScheduler.RunTask("Au",
				isAuHomePC ? "_Au.Editor" : "Au.Editor", //in Q:\app\Au\_ or <installed path>
				true, args);
			//Api.AllowSetForegroundWindow(pid); //fails and has no sense
		}
		catch (Exception ex) { //probably this program is not installed (no scheduled task)
			print.qm2.write(ex);
			return false;
		}
		return true;
	}

	internal static class TrayIcon {
		static IntPtr[] _icons;
		static bool _disabled;
		static wnd _wNotify;

		const int c_msgNotify = Api.WM_APP + 1;
		static int s_msgTaskbarCreated;

		internal static void Update_() {
			if (_icons == null) {
				_icons = new IntPtr[2];

				s_msgTaskbarCreated = WndUtil.RegisterMessage("TaskbarCreated", uacEnable: true);

				WndUtil.RegisterWindowClass("Au.Editor.TrayNotify", _WndProc);
				_wNotify = WndUtil.CreateWindow("Au.Editor.TrayNotify", null, WS.POPUP, WSE.NOACTIVATE);
				//not message-only, because must receive s_msgTaskbarCreated and also used for context menu

				process.thisProcessExit += _ => {
					var d = new Api.NOTIFYICONDATA(_wNotify);
					Api.Shell_NotifyIcon(Api.NIM_DELETE, d);
				};

				_Add(false);
			} else {
				var d = new Api.NOTIFYICONDATA(_wNotify, Api.NIF_ICON) { hIcon = _GetIcon() };
				bool ok = Api.Shell_NotifyIcon(Api.NIM_MODIFY, d);
				Debug_.PrintIf(!ok, lastError.message);
			}
		}

		static void _Add(bool restore) {
			var d = new Api.NOTIFYICONDATA(_wNotify, Api.NIF_MESSAGE | Api.NIF_ICON | Api.NIF_TIP /*| Api.NIF_SHOWTIP*/) { //need NIF_SHOWTIP if called NIM_SETVERSION(NOTIFYICON_VERSION_4)
				uCallbackMessage = c_msgNotify,
				hIcon = _GetIcon(),
				szTip = App.AppNameLong
			};
			if (Api.Shell_NotifyIcon(Api.NIM_ADD, d)) {
				//d.uFlags = 0;
				//d.uVersion = Api.NOTIFYICON_VERSION_4;
				//Api.Shell_NotifyIcon(Api.NIM_SETVERSION, d);

				//timer.after(2000, _ => Update(TrayIconState.Disabled));
				//timer.after(3000, _ => Update(TrayIconState.Running));
				//timer.after(4000, _ => Update(TrayIconState.Normal));
			} else if (!restore) { //restore when "TaskbarCreated" message received. It is also received when taskbar DPI changed.
				Debug_.Print(lastError.message);
			}
		}

		static IntPtr _GetIcon() {
			int i = _disabled ? 1 : 0;
			ref IntPtr icon = ref _icons[i];
			if (icon == default) Api.LoadIconMetric(Api.GetModuleHandle(null), Api.IDI_APPLICATION + i, 0, out icon);
			return icon;
			//Windows 10 on DPI change automatically displays correct non-scaled icon if it is from native icon group resource.
			//	I guess then it calls GetIconInfoEx to get module/resource and extracts new icon from same resource.
		}

		static void _Notified(nint wParam, nint lParam) {
			int msg = Math2.LoWord(lParam);
			//if (msg != Api.WM_MOUSEMOVE) WndUtil.PrintMsg(default, msg, 0, 0);
			switch (msg) {
			case Api.WM_LBUTTONUP:
				ShowWindow();
				break;
			case Api.WM_RBUTTONUP:
				_ContextMenu();
				break;
			case Api.WM_MBUTTONDOWN:
				TriggersAndToolbars.DisableTriggers(null);
				break;
			}
		}

		static nint _WndProc(wnd w, int m, nint wParam, nint lParam) {
			//WndUtil.PrintMsg(w, m, wParam, lParam);
			if (m == c_msgNotify) _Notified(wParam, lParam);
			else if (m == s_msgTaskbarCreated) _Add(true); //when explorer restarted or taskbar DPI changed
			else if (m == Api.WM_DESTROY) _Exit();
			else if (m == Api.WM_DISPLAYCHANGE) {
				Tasks.OnWM_DISPLAYCHANGE();
			}

			return Api.DefWindowProc(w, m, wParam, lParam);
		}

		static void _ContextMenu() {
			var m = new popupMenu();
			m.AddCheck("Disable triggers\tM-click", check: _disabled, _ => TriggersAndToolbars.DisableTriggers(null));
			m.Separator();
			m.Add("Exit", _ => _Exit());
			m.Show(MSFlags.AlignBottom | MSFlags.AlignCenterH);
		}

		static void _Exit() {
			_app.Shutdown();
		}

		public static bool Disabled {
			get => _disabled;
			set { if (value == _disabled) return; _disabled = value; Update_(); }
		}
	}
}

enum EProgramState {
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

enum ERegisteredHotkeyId {
	QuickCaptureMenu = 1,
	QuickCaptureDwnd = 2,
	QuickCaptureDelm = 3,
}

namespace Au.Editor {
	partial class WpfApp : Application {
	}
}