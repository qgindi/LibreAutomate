using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Au.Controls;
using System.Windows.Interop;

partial class PanelDebug {
	_Debugger _d;
	(Button debug, Button restart, Button next, Button step, Button stepOut, Button @continue, Button pause, Button end) _buttons;
	ComboBox _cbThreads;
	KPanels.ILeaf _ipanel;
	bool _restart;
	_Session _s;
	static IntPtr s_event;

	record class _Session(int processId, bool attachMode) {
		public FileNode file;
		public int threadId;
		public _FRAME frame;
		public bool attached, stepping, inStoppedEvent, stoppedOnException;
		public (int breakpointId, bool nonstop) runToHere;
		public _RthData restartToHere;
		public Process process;
		public string[] exceptionsT, exceptionsU;
		public _STOPPED thrownException;
		public string tePath;
		public TextSpan teSpan;
		public List<int> startedThreads;
		public Dictionary<string, string> modules = new();
	}

	public PanelDebug() {
		P.UiaSetName("Debug panel");

		var b = new wpfBuilder(P).Columns(-1).Brush(SystemColors.ControlBrush);
		b.Options(margin: new());

		var tb = b.xAddToolBar(hideOverflow: true);
		tb.UiaSetName("Debug_toolbar");
		const string c_color = Menus.blue, c_color2 = Menus.green2, c_color3 = Menus.black;
		_buttons.debug = _TbButton("*Material.Bug" + c_color2, _ => _Start(), "Run with debugger.\nWill stop (pause) at a breakpoint or exception line.\nTo add a breakpoint, click the white margin in the code editor.");
		_buttons.restart = _TbButton("*Codicons.DebugRestart" + c_color2, _ => _Restart(), "Restart");
		_buttons.end = _TbButton("*Material.SquareOutline @14" + c_color3, button => {
			if (_s == null) return;
			if (_s.attachMode) {
				var m = new popupMenu();
				m.Add("Disconnect", o => _Disconnect(), "*Codicons.DebugDisconnect").Tooltip = "Continue without debugger";
				m.Add("End task", o => _End(), "*Material.SquareOutline @14").Tooltip = "Stop debugging and end task";
				var r = button.RectInScreen();
				m.Show(xy: new(r.left, r.bottom), excludeRect: r, owner: button);
			} else {
				_End();
			}
		}, "Stop debugging and end task.\nRight click to disconnect the debugger only.");
		_buttons.end.xContextMenu(m => {
			m.Add("Disconnect", o => _Disconnect(), "*Codicons.DebugDisconnect", disable: !IsDebugging).Tooltip = "Continue without debugger";
		});
		tb.Items.Add(new Separator());
		_buttons.@continue = _TbButton("*Codicons.DebugContinue @14" + c_color, _ => _Continue(), "Continue\n\nF5");
		_buttons.pause = _TbButton("*Codicons.DebugPause @14" + c_color, _ => _Pause(), "Pause\n\nF6");
		_buttons.next = _TbButton("*Codicons.DebugStepOver @14" + c_color, _ => _Next(), "Step over\n\nF10");
		_buttons.step = _TbButton("*Codicons.DebugStepInto" + c_color, _ => _Step(), "Step into\n\nF11");
		_buttons.stepOut = _TbButton("*Codicons.DebugStepOut" + c_color, _ => _StepOut(), "Step out\n\nShift+F11");
		tb.Items.Add(new Separator());
		//_TbButton("*BoxIcons.RegularMenu" + c_color3, null,  "More debugger commands").xDropdownMenu(_CommandsMenu);
		_TbButton("*EvaIcons.Options2" + Menus.green, null, "Debugger options").xDropdownMenu(_OptionsMenu);
#if DEBUG
		_TbButton("*WeatherIcons.SnowWind #FF3300", _ => { _Test(); }, "Test");
#endif

		Button _TbButton(string icon, Action<Button> click, string tooltip/*, bool overflow = false*/) {
			var v = tb.AddButton(icon, click, tooltip);
			//if (overflow) ToolBar.SetOverflowMode(v, OverflowMode.Always);
			return v;
		}

		b.Options(margin: new());

		using (_Header("Variables", false)) {

		}
		_VariablesViewInit();
		b.Row((-Math.Max(1, App.Settings.debug.hVar), 1..)).xAddInBorder(_tvVariables, thickness: new(0, 0, 0, 1));

		using (_Header("Call stack", true, 0, 16, -1)) {
			b.Skip().Add(out _cbThreads).Tooltip("Threads.\nThe text is thread id and name (Thread.Name).");
			_cbThreads.SelectionChanged += (_, _) => { if (_cbThreads.SelectedItem is ComboBoxItem k && k.Tag is _THREAD t) _SelectedThread(t); };
		}
		_StackViewInit();
		b.Row((-Math.Max(1, App.Settings.debug.hStack), 1..)).Add(_tvStack);

		b.End();
		_UpdateUI(_UU.Init);

		_ipanel = Panels.PanelManager["Debug"];
		_ipanel.DontActivateFloating = e => true;

		UsingEndAction _Header(string text, bool splitter, params WBGridLength[] cols) {
			b.Row(0);
			if (splitter) {
				b.Add(out GridSplitter k).Splitter(vertical: false, thickness: double.NaN).And(0);
				k.DragCompleted += (_, e) => { App.Settings.debug.hVar = _tvVariables.ActualHeight; App.Settings.debug.hStack = _tvStack.ActualHeight; };
			}
			b.StartGrid().Columns(cols);
			b.Add(out TextBlock t).FormatText($"<b>{text}</b>").Margin("2").Align(y: VerticalAlignment.Bottom);
			t.IsHitTestVisible = false;
			return new UsingEndAction(() => b.End());
		}
	}

	public UserControl P { get; } = new();

	public bool IsDebugging { get; private set; }

	public bool IsStopped { get; private set; }

	void _Start(FileNode restart = null, _RthData runToHere = null) {
		var file = restart ?? App.Model.CurrentFile;
		if (file == null || file.IsAlien) return;

		if (IsDebugging) {
			_restart = true;
			_s.file = file;
			_s.restartToHere = runToHere;
			_End();
			return;
		}

		if (s_event == 0) s_event = Api.CreateEvent2(0, false, false, "Au.event.Debugger");
		else Api.ResetEvent(s_event);

		int processId = CompileRun.CompileAndRun(true, file, noDefer: true, runFromEditor: true, debugAttach: processId => _Attach(processId, false, file, runToHere));
		if (processId <= 0) {
			if (restart != null) _UpdateUI(_UU.Ended);
		}
	}

	void _Restart(_RthData runToHere = null) {
		if (_s?.file is { } f) _Start(f, runToHere);
	}

	bool _Attach(int processId, bool attachMode, FileNode file, _RthData runToHere = null) {
		//#if DEBUG
		//		print.clear(); print.qm2.clear();
		//#endif

		_restart = false;
		_s = new(processId, attachMode) { file = file };
		_d = new _Debugger(_Events);
		if (!_d.Init()) return _Failed();
		IsDebugging = true;

		_SetOptions();
		_SetExceptions(0);
		_SetBreakpoints();
		if (runToHere != null) _RunToHere(runToHere.file, runToHere.line, runToHere.nonstop);

		if (!attachMode) _TempDisableAuDebugging(true);

		if (_d.SendSync(1, $"-target-attach {processId}") != "^done") {
			_Print("Failed to attach debugger."); //never mind: fails to attach to 32-bit process, error "parameter incorrect"
			IsDebugging = false;
			_d.Send($"-gdb-exit");
			return _Failed();
		}

		_s.attached = true;
		_UpdateUI(_UU.Started);
		_PrintThread(null, true);
		_AutoShowHidePanel(true);
		RegHotkeys.RegisterDebug();
		print.it("<><lc #C0C0FF>Debugging started<>");
		Api.SetEvent(s_event); //let the process run
		return true;

		bool _Failed() {
			_d.Dispose();
			_d = null;
			_UpdateUI(_UU.Ended);
			_s = null;
			return false;
		}
	}

	public void Start() {
		_Start();
	}

	public bool Attach(int processId) {
		if (IsDebugging) return false;
		var f = App.Tasks.FileFromProcessId(processId);
		if (f == null) return false;
		return _Attach(processId, true, f);
	}

	public bool EndIfDebugging(int processId) {
		if (!IsDebugging || processId != _s.processId) return false;
		_End();
		return true;
	}

	internal void WmHotkey_(RegHotkeys.Id id) {
		if (!IsDebugging) return;
		switch (id) {
		case RegHotkeys.Id.DebugNext: _Next(); break;
		case RegHotkeys.Id.DebugStep: _Step(); break;
		case RegHotkeys.Id.DebugStepOut: _StepOut(); break;
		case RegHotkeys.Id.DebugContinue: _Continue(); break;
		case RegHotkeys.Id.DebugPause: _Pause(); break;
			//case RegHotkeys.Id.DebugEnd: _End(); break;
			//case RegHotkeys.Id.DebugRestart: _Restart(); break;
		}
	}

	#region options, exceptions, breakpoints

	void _OptionsMenu(popupMenu m) {
		m[(App.Settings.debug.breakT & 9) switch { 1 => "Exceptions...  (break when thrown)", 9 => "Exceptions...  (break when caught)", _ => "Exceptions..." }] = _DExceptionTypes;
		m.AddCheck("Step into properties and operators", App.Settings.debug.stepIntoAll, o => {
			App.Settings.debug.stepIntoAll = o.IsChecked;
			if (IsDebugging) _d.Send("-gdb-set enable-step-filtering " + (o.IsChecked ? "0" : "1"));
		});
		m.AddCheck("Debug optimized code", App.Settings.debug.noJMC, o => { App.Settings.debug.noJMC = o.IsChecked; }, disable: IsDebugging);
		m.Separator();
		m.AddCheck("Print clicked variable in 1 line", App.Settings.debug.printVarCompact, o => { App.Settings.debug.printVarCompact = o.IsChecked; });
		m.AddCheck("Print 'module loaded/unloaded'", (App.Settings.debug.printEvents & 1) != 0, o => { App.Settings.debug.printEvents ^= 1; });
		m.AddCheck("Print 'thread started/ended'", (App.Settings.debug.printEvents & 2) != 0, o => { App.Settings.debug.printEvents ^= 2; });
		m.Separator();
		m.AddCheck("Activate LA when stepping", App.Settings.debug.activateLA, o => { App.Settings.debug.activateLA ^= true; });

		void _DExceptionTypes(PMItem mi) {
			var w = new KDialogWindow();
			w.InitWinProp("Exception settings", App.Wmain);
			var b = new wpfBuilder(w).WinSize(450, 400).Columns(-1);

			b.StartStack();
			b.Add(out KCheckBox cThrown, "Break when exception thrown").Checked((App.Settings.debug.breakT & 1) != 0)
				.Add(out KCheckBox cCaught, "when caught in user code").Checked((App.Settings.debug.breakT & 8) != 0).xBindCheckedEnabled(cThrown);
			b.End();
			b.StartStack().Margin("TBL16");
			b.Add(out KCheckBox cUseListT, "If exception is").Checked((App.Settings.debug.breakT & 2) != 0)
				.Add(out KCheckBox cNotT, "not").Checked((App.Settings.debug.breakT & 4) != 0).xBindCheckedEnabled(cUseListT);
			b.End();
			b.Row(-2).Add(out TextBox eListT, App.Settings.debug.breakListT).LabeledBy(cUseListT).Multiline(wrap: TextWrapping.NoWrap).Margin("B10")
				.Tooltip("""
Exception types.
If the list is empty or 'if exception is' unchecked, will break on all exceptions.
Example:
System.DivideByZeroException
System.IO.FileNotFoundException
//comment
""");

			b.Add(out KCheckBox cUU, "Break when exception unhandled in user code is caught elsewhere").Checked((App.Settings.debug.breakU & 1) != 0);
			b.Add<Label>("If exception is not").Margin("TBL16");
			b.Row(-1).Add(out TextBox eListU, App.Settings.debug.breakListU).LabeledBy().Multiline(wrap: TextWrapping.NoWrap)
				.Tooltip("""
Exception types.
Example:
System.OperationCanceledException
System.Threading.Tasks.TaskCanceledException
//comment
""");

			b.R.AddOkCancel();
			if (!w.ShowAndWait()) return;

			App.Settings.debug.breakListT = eListT.TextOrNull();
			App.Settings.debug.breakListU = eListU.TextOrNull();
			App.Settings.debug.breakT.SetFlag_(1, cThrown.IsChecked);
			App.Settings.debug.breakT.SetFlag_(2, cUseListT.IsChecked);
			App.Settings.debug.breakT.SetFlag_(4, cNotT.IsChecked);
			App.Settings.debug.breakT.SetFlag_(8, cCaught.IsChecked);
			App.Settings.debug.breakU.SetFlag_(1, cUU.IsChecked);
			_SetExceptions(3);

			//CONSIDER: allow to specify stack patterns where 'thrown' exceptions are ignored
		}
	}

	void _SetOptions() {
		if (App.Settings.debug.stepIntoAll) _d.Send("-gdb-set enable-step-filtering 0");
		if (App.Settings.debug.noJMC) _d.Send("-gdb-set just-my-code 0");
	}

	/// <param name="action">0 init, 1 change 'throw', 2 change 'user-unhandled', 3 change both.</param>
	void _SetExceptions(int action) {
		if (!IsDebugging) return;
		if (action is 0 or 1 or 3) _Apply("throw", App.Settings.debug.breakT, App.Settings.debug.breakListT, ref _s.exceptionsT);
		if (action is 0 or 2 or 3) _Apply("user-unhandled", App.Settings.debug.breakU | 6, App.Settings.debug.breakListU, ref _s.exceptionsU);

		void _Apply(string tuu, int flags, string types, ref string[] ids) {
			if (action != 0 && ids != null) _d.SendSync(3, $"-break-exception-delete {string.Join(' ', ids)}");
			ids = null;
			if ((flags & 1) != 0) {
				string etypes = "*";
				if ((flags & 2) != 0 && !types.NE()) {
					using (new StringBuilder_(out var sb)) {
						bool appendNot = (flags & 4) != 0;
						foreach (var v in types.Split_('\n')) {
							if (v.Starts("//")) continue;
							if (appendNot) { appendNot = false; sb.Append("--not"); }
							sb.Append(' ').Append(v);
						}
						if (sb.Length > 0) etypes = sb.ToString();
					}
				}
				var s = _d.SendSync(2, $"-break-exception-insert {tuu} {etypes}");
				if (s.Starts("^done,bkpt={")) { //^done,bkpt={number="1"}
					ids = [s.Split('"')[1]];
				} else if (s.Starts("^done,bkpt=[")) { //^done,bkpt=[{number="1"},{number="2"}]
					var a = new _MiRecord(s).data["bkpt"].AsArray();
					ids = new string[a.Count];
					for (int i = 0; i < ids.Length; i++) ids[i] = (string)a[i]["number"];
				}
			}
		}

		//With -break-exception-insert can be specified unhandled|user-unhandled|throw|throw+user-unhandled.
		//	It seems 'unhandled' is always on and cannot be changed.
		//	'user-unhandled' means "Break when handled exception was unhandled in user code". Eg in Task.Run action when not awaited.
	}

	void _SetBreakpoints() {
		foreach (var b in Panels.Breakpoints.GetBreakpoints()) {
			_SetBreakpoint(b);
		}
	}

	void _SetBreakpoint(IBreakpoint b) {
		b.Id = _SetBreakpoint(b.File, b.Line);
		if (_s.runToHere.nonstop && b.Id != _s.runToHere.breakpointId) _d.Send($"-break-activate false {b.Id}");
		if (b.HasProperties) _SetBreakpointCondition(b);
	}

	int _SetBreakpoint(FileNode f, int line) {
		var s = _d.SendSync(4, $"-break-insert \"{f.FilePath.Replace(@"\", @"\\")}:{line + 1}\"");
		var r = new _MiRecord(s);
		var d = r.Data<_DONE_BKPT>().bkpt;
		return d.number;
	}

	internal void BreakpointAddedDeleted_(IBreakpoint b, bool added) {
		if (!IsDebugging) return;
		if (added) {
			App.Dispatcher.InvokeAsync(() => { if (IsDebugging) _SetBreakpoint(b); });
		} else {
			int n = b.Id;
			b.Id = 0;
			Debug_.PrintIf(n == 0);
			if (n != 0 && n != _s.runToHere.breakpointId) _d.Send($"-break-delete {n}");
		}
	}

	internal void BreakpointPropertiesChanged_(IBreakpoint b) {
		if (IsDebugging) _SetBreakpointCondition(b);
	}

	void _SetBreakpointCondition(IBreakpoint b) {
		string s;
		if (b.HasProperties) {
			string cond = b.Condition, log = b.Log;
			cond = cond?.RxReplace(@"[\r\n]+", " ");
			if (log.NE()) s = cond;
			else {
				log = log.RxReplace(@"[\r\n]+", " ");
				if (!b.LogExpression) log = log.Escape(quote: true);
				else log = $"({log}).ToString()";
				if (cond.NE()) cond = "true";
				s = $"Au.More.LaDebugger_.Logpoint({cond}, {log}, \"{b.File.IdStringWithWorkspace}|{b.Line + 1}\")";
			}
		} else s = "false";
		_d.Send($"-break-condition {b.Id} {s}");
		//note: without ""
	}

	#endregion

	internal void AddMarginMenuItems_(SciCode doc, popupMenu m, int line) {
		m["Run to here", "*JamIcons.ArrowCircleDownRight @14" + Menus.blue] = o => _RunToHere(doc.EFile, line, false);
		m["Run to here non-stop", "*JamIcons.ArrowCircleDownRight @14" + Menus.blue] = o => _RunToHere(doc.EFile, line, true);
		if (IsDebugging) m["Restart and run to here non-stop", "*Codicons.DebugRestart @14" + Menus.green2] = o => _RunToHere(doc.EFile, line, true, true);
		if (IsStopped) m["Jump to here", "*Codicons.DebugStackframe @14" + Menus.green2] = o => _JumpToHere(doc.EFile, line);
	}

	void _Step(string s) {
		if (!IsStopped) return;
		bool step = s[0] != 'c';
		if (step && _s.stoppedOnException) s = "finish"; //workaround for: if was stopped on user-unhandled exception, 'step' and 'next' behave like 'continue'. Even VS and VSCode have this bug.
		_s.stoppedOnException = false;
		if (_ExecStepL(step && _s.threadId != 0 ? $"-exec-{s} --thread {_s.threadId}" : $"-exec-{s}")) {
			IsStopped = false;
			_s.stepping = step;
			_s.frame = null;
			_UpdateUI(_UU.Resumed);
		} else if (step) { //eg when paused in [Native Frames]. Never seen after modifying netcoredbg.
			_Print("Can't step here. Try 'Continue' or 'Run to here'.");
		}
	}

	void _Next() => _Step("next");

	void _Step() => _Step("step");

	void _StepOut() => _Step("finish");

	void _Continue() => _Step("continue");

	bool _ExecStepL(string s) {
		//return _d.SendSync(6, s) == "^running";
		return _d.SendSync(6, s, o => {
			int i = 0; while (i < o.Length && o[i].IsAsciiDigit()) i++;
			//print.it($"<><c blue>{o[i..]}<>");
			if (o.Eq(i, "^done,variables=") || o.Eq(i, "^done,stack=")) return true; //old
			return false;
		}) == "^running";
	}

	void _RunToHere(FileNode f, int line, bool nonstop, bool restart = false) {
		if (!IsDebugging) {
			_Start(runToHere: new(f, line, nonstop));
		} else if (restart) {
			_Restart(runToHere: new(f, line, nonstop));
		} else {
			if (_s.runToHere.breakpointId != 0) _RthEnd();
			if (_s.runToHere.nonstop = nonstop) _d.Send("-break-activate false");
			_s.runToHere.breakpointId = _SetBreakpoint(f, line);
			if (nonstop && _IsEnabledBreakpoint(_s.runToHere.breakpointId)) _d.Send($"-break-activate true {_s.runToHere.breakpointId}");
			if (IsStopped) _Continue();
		}
	}

	void _RthEnd() {
		if (!_IsEnabledBreakpoint(_s.runToHere.breakpointId)) _d.Send($"-break-delete {_s.runToHere.breakpointId}");
		_s.runToHere.breakpointId = 0;
		if (_s.runToHere.nonstop) { _s.runToHere.nonstop = false; _d.Send("-break-activate true"); }
	}

	void _JumpToHere(FileNode f, int line) {
		_s.stoppedOnException = false;
		var s = _d.SendSync(7, $"-jump \"{f.FilePath.Replace(@"\", @"\\")}:{line + 1}\"");
		if (s?.Starts("^done,sp=") == true) {
			int i = 9;
			line = s.ToInt(i, out i) - 1;
			int endLine = s.ToInt(++i, out i) - 1;
			int col = s.ToInt(++i, out i) - 1;
			int endCol = s.ToInt(++i, out i) - 1;
			App.Model.OpenAndGoTo(f, line, col, activateLA: App.Settings.debug.activateLA);
			_marker2.Delete();
			_marker.Add(line, col, endLine, endCol);
			_d.Send($"-stack-list-frames --thread {_s.threadId}");
		} else {
			if (s.Like("^error,msg=\"*\"")) s = s[12..^1];
			if (s == "SetIP cannot be done on any frame except the leaf frame.") _Print("Cannot jump to another function.");
			else _Print("Cannot jump to here. " + s);
		}
	}

	bool _IsEnabledBreakpoint(int id) => Panels.Breakpoints.GetBreakpoints().Any(o => o.Id == id);

	record class _RthData(FileNode file, int line, bool nonstop);

	void _Pause() {
		if (!IsDebugging || IsStopped) return;
		int tid = _s.threadId;
		if (tid == 0) { //pause main thread
			try {
				if (_s.process == null) _s.process = Process.GetProcessById(_s.processId); else _s.process.Refresh();
				tid = _s.process.Threads[0].Id;
			}
			catch (Exception e1) { Debug_.Print(e1); }
		}
		_d.Send($"-exec-interrupt --thread {tid}"); //note: netcoredbg code is modified, added --thread parameter. If --thread 0, works like without --thread.
	}

	void _End() {
		if (IsDebugging) _d.Send($"-exec-abort");
	}

	void _Disconnect() {
		if (IsDebugging) _d.Send($"-gdb-exit"); //detach and exit debugger
	}

	void _Events(string s) {
		if (_s == null) {
			Debug_.Print("_s==null");
			return;
		}

		if (s.ToInt(out int token, 0, out int endToken)) s = s[endToken..]; //info: currently not using tokens

#if DEBUG
		if (s.Starts("^error")) print.it($"<><c red>{s}<>");
		//else if (0 == s.Starts(true, "^done", "=message,", "=library-", "=thread-")) print.it("EVENT", s);
#endif

		if (s == "^exit") {
			_d.Dispose();
			_d = null;
			IsDebugging = IsStopped = false;
			_UpdateUI(_UU.Ended);
			RegHotkeys.UnregisterDebug();
			_AutoShowHidePanel(false);

			if (_restart) {
				_restart = false;
				var file = _s.file;
				var rth = _s.restartToHere;
				timer.after(100, _ => _Start(file, rth));
			}

			_s = null;
			print.it("<><lc #C0C0FF><>");
		} else if (s.Starts("*stopped")) {
			if (s.Eq(17, "exited")) {
				IsDebugging = IsStopped = false;
				_d.Send($"-gdb-exit");
				_UpdateUI(_UU.Ended);
				if (s.RxMatch(@"\bexit-code=""(.+?)""", 1, out string ec)) _Print($"The process has exited with code {ec} (0x{ec.ToInt():X}).");
			} else {
				IsStopped = true;

				_s.inStoppedEvent = true;
				try { if (!_Stopped(s)) return; }
				finally { _s.inStoppedEvent = false; }

				_UpdateUI(_UU.Paused);
			}
		} else if (s.Starts("^done,stack=")) {
			if (!IsStopped) return;
			_FRAME[] a = new _MiRecord(s).Data<_DONE_STACK>().stack;
			if (a.Length == 0) return;
			_StackViewSetItems(a);
			_s.frame = a[0];
			_ListVariables();

			if (!_marker.Exists && !_marker2.Exists) {
				for (int i = 0; i < a.Length; i++) { //if not in user code, try to go to user code and add _marker2
					if (_GoToLine(a[i])) break;
				}
			}
		} else if (s.Starts("^done,variables=")) {
			if (!IsStopped) return;
			_VARIABLE[] a = new _MiRecord(s).Data<_DONE_VARIABLES>().variables;
			_VariablesViewSetItems(a);
		} else if (s.Starts('=')) {
			if (s.Starts("=library-")) {
				var r = new _MiRecord(s);
				string id = (string)r.data["id"], lib = (string)r.data["target_name"];
				if (s.Starts("=library-loaded,")) {
					_s.modules[id] = lib;
					if ((App.Settings.debug.printEvents & 1) != 0) _Print($"Module loaded{((string)r.data["symbols_loaded"] == "1" ? " with symbols" : "")}: {lib}");
				} else if (s.Starts("=library-unloaded,")) {
					_s.modules.Remove(id);
					if ((App.Settings.debug.printEvents & 1) != 0) _Print($"Module unloaded: {lib}");
				}
			} else if (s.Starts("=thread-created,id=")) {
				_PrintThread(s, true);
			} else if (s.Starts("=thread-exited,id=")) {
				_PrintThread(s, false);
				if (s.ToInt(19) == _s.threadId) _s.threadId = 0;
			} else if (s.Starts("=message,")) { //Debug.Print etc, like =message,text="TEXT\r\n",send-to="output-window",source="Debugger.Log"
				var r = new _MiRecord(s);
				var text = ((string)r.data["text"]).TrimEnd();
				_Print(text.Starts("<>") ? text[2..] : $"<\a>{text}</\a>");
				//BAD: not in sync with print.it.
			}
		}
	}

	//returns false to continue
	bool _Stopped(string s) {
		var x = new _MiRecord(s).Data<_STOPPED>();
		_s.threadId = x.thread_id;
		_s.stoppedOnException = false;
		var thrownException = _s.thrownException; _s.thrownException = null;

		switch (x.reason) {
		case "breakpoint-hit":
			if (_s.runToHere.breakpointId != 0 && x.bkptno == _s.runToHere.breakpointId) {
				_RthEnd();
				//never mind: does not delete the _runToHere breakpoint if stops there on exception
			} else {
				if (!x.exception.NE()) _Print($"<c red>{x.exception}<>"); //bad expression of condition or logpoint
			}
			break;
		case "exception-received":
			if (_StoppedOnException(x)) return false;
			_s.stoppedOnException = true;
			break;
		case "end-stepping-range":
			if (thrownException != null) if (!_DetectCatch(x, thrownException)) return false;
			break;
			//case "signal-received": //Pause or Debugger.Break

			//	break;
		}

		if (_GetThreads() is _THREAD[] a) { //fast
			a = a.Where(t => t.id == _s.threadId || !_IsHiddenThreadName(t.name)).ToArray();
			bool same = a.Length == _cbThreads.Items.Count;
			if (same) for (int i = 0; i < a.Length; i++) if (!(same = _cbThreads.Items[i] is ComboBoxItem k && k.Tag is _THREAD t && t == a[i])) break;
			if (!same) {
				_cbThreads.Items.Clear();
				foreach (var t in a) _cbThreads.Items.Add(new ComboBoxItem { Content = $"{t.id}  {t.name}", Tag = t });
			}

			_GoToLine(x.frame);

			int iSel = Array.FindIndex(a, t => t.id == _s.threadId);
			if (iSel >= 0) {
				if (iSel != _cbThreads.SelectedIndex) _cbThreads.SelectedIndex = iSel; //_cbThreads.SelectionChanged -> _SelectedThread
				else _SelectedThread(a[iSel]);
			}
		}

		return true;

		_THREAD[] _GetThreads() {
			if (_d.SendSync(5, "-thread-info") is string s && s.Starts("^done,threads=")) {
				_THREAD[] a = new _MiRecord(s).Data<_DONE_THREADS>().threads;
				foreach (var t in a) {
					var s1 = t.name;
					_GetThreadNameAndTime(t.id, out t.name, out t.time);
					if (t.name == null && s1 != "<No name>") t.name = s1;
				}
				a = a.OrderBy(o => o.time).ToArray();
				a[0].name ??= "Main Thread";
				return a;
			}
			return null;
		}

		bool _StoppedOnException(_STOPPED x) {
			if (_s.runToHere.nonstop && x.exception_stage != "unhandled") {
				_Continue();
				return true;
			}

			var stage = x.exception_stage switch { "throw" => "thrown", "user-unhandled" => "unhandled in user code", _ => x.exception_stage };

			if ((App.Settings.debug.breakT & 9) == 9 && x.exception_stage == "throw") {
				_s.thrownException = x;
				_s.tePath = null;
				if (_ExecStepL("-exec-finish")) return true;
				Debug_.Print("-exec-x failed on thrown exception");
				_s.thrownException = null;
			}

			_PrintException(x, stage, false);
			return false;
		}

		//Returns: true - stop, false - continue.
		bool _DetectCatch(_STOPPED x, _STOPPED thrownException) {
			var f = x.frame;
			if (!f.fullname.NE()) {
				try {
					var s = filesystem.loadText(f.fullname);
					for (int i = 0, line = 0; i < s.Length; i++) {
						if (++line == f.line) {
							int pos = i + f.col - 1;
							if (s.Eq(pos, "catch") || s.Eq(pos, "when")) {
								var tok = CiUtil.GetSyntaxTree(s).FindToken(pos);
								var cc = tok.Parent as CatchClauseSyntax;
								if (cc == null && tok.Parent is CatchFilterClauseSyntax) cc = tok.Parent.Parent as CatchClauseSyntax;
								if (cc != null) {
									_s.thrownException = thrownException;
									_s.tePath = f.fullname;
									_s.teSpan = cc.Block.Span;
									if (_ExecStepL("-exec-next")) return false; //run until `{ }` or next `when`
									Debug_.Print("-exec-next failed on detected catch or when");
									_s.thrownException = null;
									_s.tePath = null;
									return true;
								}
							} else if (f.fullname == _s.tePath && _s.teSpan.Contains(pos)) {
								_PrintException(thrownException, "caught", true);
								break;
							}
							if (_s.stepping) break;
							_Continue();
							return false;
						}
						i = s.IndexOf('\n', i);
						if (i < 0) break;
					}
				}
				catch (Exception e1) { Debug_.Print(e1); }
			} else Debug_.Print("non-user code");
			return true;
		}

		void _PrintException(_STOPPED x, string stage, bool caught) {
			if (_VarCreateL("$exception.ToString()") is { } v) {
				string s = v.value.Trim('"').Unescape(), color = caught ? "#CC00FF" : "red", append = null;
				if (_VarCreateL("$exception.InnerException") is { } vi && vi.value.Starts('{')) {
					append = " See also: Debug panel > Variables > $exception > InnerException.";
				}
				_Print($"<c {color}>{stage.Upper(SUpper.FirstChar)}: {s}\r\n{append}<>");
			}
		}
	}

	void _SelectedThread(_THREAD t) {
		if (!IsStopped) return;
		//if (!_s.inStoppedEvent) _VariablesViewChangedFrameOrThread();
		if (t.id != _s.threadId) {
			_s.threadId = t.id;
			_ClearTreeviewsAndMarkers();
		}
		_d.Send($"-stack-list-frames --thread {_s.threadId}");
	}

	bool _GoToLine(_FRAME f, bool keepMarkers = false) {
		if (f != null) {
			int line = f.line - 1, col = f.col - 1, line2 = f.end_line - 1, col2 = f.end_col - 1;
			if (App.Model.OpenAndGoTo(f.fullname, line, col, activateLA: App.Settings.debug.activateLA)) {
				if (f.level > 0) {
					_marker2.Add(line, col, line2, col2);
				} else {
					_marker2.Delete();
					_marker.Add(line, col, line2, col2);
				}
				return true;
			}
		}
		if (!keepMarkers) {
			_marker.Delete();
			_marker2.Delete();
		}
		return false;
	}
	_Marker _marker = new(SciCode.c_markerDebugLine, SciCode.c_indicDebug), _marker2 = new(SciCode.c_markerDebugLine2, SciCode.c_indicDebug2);

	void _UpdateUI(_UU u) {
		if (u == _UU.Resumed) {
			_timerResumeUU ??= new(_ => _UpdateUI(_UU.Resumed2));
			_timerResumeUU.After(100);
		} else {
			_timerResumeUU?.Stop();
			bool deb = IsDebugging, stopped = IsStopped;
			_buttons.debug.IsEnabled = !deb;
			_buttons.restart.IsEnabled = deb && !_restart;
			_buttons.end.IsEnabled = deb;
			_buttons.next.IsEnabled = stopped;
			_buttons.step.IsEnabled = stopped;
			_buttons.stepOut.IsEnabled = stopped;
			_buttons.@continue.IsEnabled = stopped;
			_buttons.pause.IsEnabled = deb && !stopped;
			bool restart = deb || _restart;
			_buttons.debug.Visibility = !restart ? Visibility.Visible : Visibility.Collapsed;
			_buttons.restart.Visibility = restart ? Visibility.Visible : Visibility.Collapsed;
			_buttons.@continue.Visibility = !deb || stopped ? Visibility.Visible : Visibility.Collapsed;
			_buttons.pause.Visibility = deb && !stopped ? Visibility.Visible : Visibility.Collapsed;
			if (!stopped && u is _UU.Resumed2 or _UU.Ended) {
				_cbThreads.Items.Clear();
				_ClearTreeviewsAndMarkers();
			}

			if (u == _UU.Ended) _TempDisableAuDebugging(false);
		}
	}

	enum _UU { Init, Started, Ended, Paused, Resumed, Resumed2 }

	timer _timerResumeUU;

	void _ClearTreeviewsAndMarkers() {
		_StackViewSetItems(null);
		_VariablesViewSetItems(null);
		_marker.Delete();
		_marker2.Delete();
	}

	void _AutoShowHidePanel(bool starting) {
		if (starting) {
			if (_hidePanelWhenEnds) _timerHidePanel?.Stop();
			if (!_ipanel.Visible) _ipanel.Visible = _hidePanelWhenEnds = true;
		} else if (_hidePanelWhenEnds && !_restart) {
			_timerHidePanel ??= new timer(t => {
				if (_ipanel.Visible) {
					if (_ipanel.Parent.Panel.IsMouseOver) return;
					var w = wnd.fromMouse(WXYFlags.Raw);
					if (w == _tvVariables.Hwnd || w == _tvStack.Hwnd) return; //eg when ending/disconnecting through a context menu of a toolbar button
					_ipanel.Visible = false;
				}
				t.Stop();
				_hidePanelWhenEnds = false;
			});
			_timerHidePanel.Every(200);
		}
	}
	bool _hidePanelWhenEnds;
	timer _timerHidePanel;

	#region util

	static void _Print(string s) {
		print.it($"<><lc #f8f8d0>{s}<>");
	}

	static unsafe bool _GetThreadNameAndTime(int id, out string name, out long time) {
		name = null;
		using var th = Api.OpenThread(Api.THREAD_QUERY_LIMITED_INFORMATION, false, id);
		if (!Api.GetThreadTimes(th, out time, out _, out _, out _)) {
			time = long.MaxValue; //for sorting
			return false;
		}
		if (osVersion.minWin10_1607) {
			if (0x10000000 == Api.GetThreadDescription(th, out var s1) && s1 != null) {
				if (s1[0] != 0) name = new string(s1);
				Api.LocalFree(s1);
			}
		}
		return true;
	}

	static bool _IsHiddenThreadName(string s) => s is "Au.Aux" or ".NET TP Gate" or ".NET Tiered Compilation Worker" or ".NET Counter Poller" or ".NET Finalizer" or "Stylus Input" or "Au.JSettings";

	string _GetModuleName(_FRAME f) {
		if (f.clr_addr.module_id.NE()) return ""; //f.func "[Native Frames]"
		if (_s.modules.TryGetValue(f.clr_addr.module_id, out var r)) r = pathname.getName(r);
		return r;
	}

	string _FormatFrameString(_FRAME f/*, bool forPrint = false*/) {
		if (f.file.NE()) return $"{_GetModuleName(f)}!{f.func}";
		//if (forPrint) return $"<open {f.file}|{f.line}><\a>{f.func}  ::  {f.file} {f.line}</\a><>";
		return $"{f.func}  ::  {f.file} {f.line}";
	}

	void _PrintThread(string s, bool started) {
		if ((App.Settings.debug.printEvents & 2) == 0) return;
		if (s == null) { //called when attached. Detects the main thread and prints threads ordered by the start time.
			if (_s.startedThreads is { } a) {
				List<(int id, string name, long time)> k = new();
				foreach (var v in a) if (_GetThreadNameAndTime(v, out var name, out var time) && !_IsHiddenThreadName(name)) k.Add((v, name, time));
				bool once = false;
				foreach (var v in k.OrderBy(o => o.time)) {
					string name = v.name;
					if (!once) { once = true; name ??= "Main Thread"; }
					_Print($"Thread started: {v.id} {name}");
				}
			}
		} else {
			int id = s.ToInt(s.Find(",id=\"") + 5);
			if (_s.attached) {
				if (_GetThreadNameAndTime(id, out var name, out _) && !_IsHiddenThreadName(name))
					_Print($"Thread {(started ? "started" : "ended")}: {id} {name}");
			} else if (started) {
				(_s.startedThreads ??= new()).Add(id);
			}
		}
	}

	[Conditional("DEBUG")]
	static void _TempDisableAuDebugging(bool disable) {
		if (disable == _tempDisableAuDebugging) return;
		if (disable) if (!App.IsAuAtHome || Debugger.IsAttached) return;
		string from = folders.ThisAppBS + (disable ? "Au.pdb" : "Au-.pdb"), to = disable ? "Au-.pdb" : "Au.pdb";
		try { filesystem.rename(from, to, FIfExists.Delete); }
		catch (Exception e1) { Debug_.Print(e1); return; }
		_tempDisableAuDebugging = disable;
	}
	static bool _tempDisableAuDebugging;

	#endregion

	class _Marker {
		readonly int _marker, _indic;
		SciCode _doc;
		int _line, _handle;

		public _Marker(int marker, int indic) {
			_marker = marker;
			_indic = indic;
		}

		public void Add(int line, int column, int line2, int column2) {
			//Column = column;
			var doc = Panels.Editor.ActiveDoc;

			if (line != _line || doc != _doc) {
				Delete();
				_doc = doc;
				_line = line;
				_handle = _doc.aaaMarkerAdd(_marker, line);
			} else _doc.aaaIndicatorClear(_indic);

			int start = doc.aaaLineStart(false, line) + column;
			int end = doc.aaaLineStart(false, line2) + column2;
			doc.aaaIndicatorAdd(_indic, false, start..end);
		}

		public void Delete() {
			if (_doc == null) return;
			if (!_doc.AaWnd.Is0) {
				_doc.aaaMarkerDeleteHandle(_handle);
				_doc.aaaIndicatorClear(_indic);
			}
			_doc = null;
			_line = 0;
			_handle = 0;
		}

		public bool Exists => _doc != null;

		public SciCode Doc => _doc;
		//public int Line => _line;
		//public int Column { get; private set; }
	}

	#region MI output record types. Generated by _MiRecord._PrintType.

	record _BKPT(int number, string type, string disp, string enabled, string func, string file, string fullname, int line, string warning);
	record _DONE_BKPT(_BKPT bkpt);

	record _THREAD(int id, string state) { public string name; public long time; }
	record _DONE_THREADS(_THREAD[] threads);

	record struct _CLR_ADDR(string module_id, string method_token, int method_version, int il_offset, int native_offset);
	record _FRAME(int level, string file, string fullname, int line, int col, int end_line, int end_col, _CLR_ADDR clr_addr, string func, string addr, string active_statement_flags);
	record _STOPPED(string reason, int thread_id, string stopped_threads, int bkptno, int times, _FRAME frame, string exception_name, string exception, string exception_stage, string exception_category, string signal_name);

	record _DONE_STACK(_FRAME[] stack);

	record _VARIABLE(string name, string value);
	record _DONE_VARIABLES(_VARIABLE[] variables);

	record _VAR(string name, string value, string attributes, string exp, int numchild, string type, int thread_id);

	record _DONE_CHILDREN(int numchild, _VAR[] children, int has_more);

	#endregion
}

//CONSIDER: later report these small bugs and suggestions, maybe consolidated into one github issue.
//1. When stops because of exception, step commands don't work in some cases. Eg when fails to load a [DllImport] function.
//	Fix: in `AsyncStepper::SetupStep` replace `if (pFrame == nullptr) return E_FAIL;` with `if (pFrame == nullptr) return S_FALSE;`.
//2. -exec-run fails if I compiled.
//3. Debugger crashes when evaluating expression like `c[0]` with evalFlags EVAL_NOFUNCEVAL.
//	Command string: $"-var-create - \"c[0]\" --frame 0 --evalFlags 128"
//	Here c is a variable of a class with an indexer, eg List.
//4. The used Roslyn dlls are very old. They don't support newer C# features.
//5. The Roslyn scripting dlls are not used. Instead use <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />.
//6. Both protocols (MI and VSCode) should support everything supported by the debugger. Examples:
/*
Now MI protocol supports setting options at any time (-gdb-set), but VSCode protocol only in the launch request (bot not in the attach request). Also it seems MI supports an additional option 'hot reload' (I didn't test).
Now MI protocol does not support --thread in -exec-interrupt, although VSCode supports it.
Now MI protocol does not support ExceptionBreakpoint.negativeCondition (prefix '!' in VSCode protocol).
*/
