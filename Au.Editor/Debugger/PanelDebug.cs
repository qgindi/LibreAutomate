using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Au.Controls;
using System.Windows.Interop;

//CONSIDER: instead of the menu, show popup on mouse dwell over the margin.
//	Normally the first button (under the cursor) is "Toggle breakpoint". In debug mode it is "Run to here".
//	Or show "Run to here" on click in code, like in Rider.

partial class PanelDebug {
	_Debugger _d;
	(Button debug, Button restart, Button next, Button step, Button stepOut, Button @continue, Button pause, Button end) _buttons;
	ComboBox _cbThreads;
	bool _hidePanelWhenEnds;
	bool _restart;
	_Session _s;
	
	record class _Session(int processId, bool attachMode) {
		public FileNode file;
		public int threadId;
		public _FRAME frame;
		public bool attached;
		public string[] exceptionsT, exceptionsU;
		public bool resuming;
		public bool pausing;
		public int runToHere;
		public bool workaround1;
		public bool inStopped;
		public List<int> startedThreads;
		public _STOPPED thrownException;
		public Dictionary<string, string> modules = new();
	}
	
	public PanelDebug() {
		//try {
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
		_buttons.next = _TbButton("*Codicons.DebugStepOver @14" + c_color, _ => _Next(), "Step over  (F10)");
		_buttons.step = _TbButton("*Codicons.DebugStepInto" + c_color, _ => _Step(), "Step into  (F11)");
		_buttons.stepOut = _TbButton("*Codicons.DebugStepOut" + c_color, _ => _StepOut(), "Step out  (Shift+F11)");
		_buttons.@continue = _TbButton("*Codicons.DebugContinue @14" + c_color, _ => _Continue(), "Continue  (F5)");
		_buttons.pause = _TbButton("*Codicons.DebugPause @14" + c_color, _ => _Pause(), "Pause  (F6)");
		tb.Items.Add(new Separator());
		//_TbButton("*BoxIcons.RegularMenu" + c_color3, null,  "Options and more commands").xDropdownMenu(_OptionsMenu);
		_TbButton("*EvaIcons.Options2" + Menus.green, null, "Options").xDropdownMenu(_OptionsMenu);
#if DEBUG
		//_TbButton("*WeatherIcons.SnowWind #FF3300", _ => { _Test(); }, "Test");
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
		b.Row(-1.5).xAddInBorder(_tvVariables, thickness: new(0, 0, 0, 1));
		
		using (_Header("Call stack", true, 0, 16, -1)) {
			b.Skip().Add(out _cbThreads).Tooltip("Threads.\nThe text is thread id and name (Thread.Name).");
			_cbThreads.SelectionChanged += (_, _) => { if (_cbThreads.SelectedItem is ComboBoxItem k && k.Tag is _THREAD t) _SelectedThread(t); };
		}
		_StackViewInit();
		b.Row(-1).Add(_tvStack);
		
		b.End();
		_UpdateUI(_UU.Init);
		
		Panels.PanelManager["Debug"].DontActivateFloating = e => true;
		
		UsingEndAction _Header(string text, bool splitter, params WBGridLength[] cols) {
			b.Row(0);
			if (splitter) b.Add<GridSplitter>().Splitter(vertical: false, thickness: double.NaN).And(0);
			b.StartGrid().Columns(cols);
			b.Add(out TextBlock t).FormatText($"<b>{text}</b>").Margin("2").Align(y: VerticalAlignment.Bottom);
			t.IsHitTestVisible = false;
			return new UsingEndAction(() => b.End());
		}
		//}
		//catch (Exception e) { print.it(e); }
	}
	
	public UserControl P { get; } = new();
	
	public bool IsDebugging { get; private set; }
	
	public bool IsStopped { get; private set; }
	
	void _Start(FileNode restart = null) {
		var file = restart ?? App.Model.CurrentFile;
		if (file == null || file.IsAlien) return;
		
		if (IsDebugging) {
			_restart = true;
			_s.file = file;
			_End();
			return;
		}
		
		int processId = CompileRun.CompileAndRun(true, file, noDefer: true, runFromEditor: true, debugger: true);
		_Attach(processId, false, file);
	}
	
	void _Restart() {
		if (_s?.file is { } f) _Start(f);
	}
	
	void _Attach(int processId, bool attachMode, FileNode file) {
		if (processId <= 0) return;
		
		_restart = false;
		_s = new(processId, attachMode) { file = file };
		_d = new _Debugger(_Events);
		IsDebugging = true;
		print.it("<><lc #C0C0FF>Debugging started<>");
		
		_SetOptions();
		_SetExceptions(0);
		_SetBreakpoints();
		
		_d.Send($"1-target-attach {processId}");
		
		//CONSIDER:
		//if (attachMode) {
		//	_Pause();
		//}
		
		//all buttons are disabled until event "1^done" or error
		_buttons.debug.IsEnabled = false;
		_buttons.restart.IsEnabled = false;
	}
	
	public void Start() {
		var p = Panels.PanelManager["Debug"];
		if (!p.Visible) p.Visible = _hidePanelWhenEnds = true;
		_Start();
	}
	
	public bool Attach(int processId/*, bool ensureAttached*/) {
		if (IsDebugging) return false;
		_Attach(processId, true, App.Tasks.FileFromProcessId(processId));
		//if (ensureAttached) return wait.until(new Seconds(-10) { DoEvents = true }, () => _s?.attached != false) && _s != null;
		return true;
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
		m.AddCheck("Step into properties and operators", App.Settings.debug.stepIntoAll, o => {
			App.Settings.debug.stepIntoAll = o.IsChecked;
			if (IsDebugging) _d.Send("-gdb-set enable-step-filtering " + (o.IsChecked ? "0" : "1"));
		});
		m.AddCheck("Don't call functions to get values", App.Settings.debug.noFuncEval, o => { App.Settings.debug.noFuncEval = o.IsChecked; });
		m.Separator();
		m.Add("Break on exception:", disable: true);
		m.AddCheck("    When thrown (even if handled)", (App.Settings.debug.breakT & 1) != 0, o => { App.Settings.debug.breakT ^= 1; _SetExceptions(1); });
		m.AddCheck("    If unhandled in user code but handled elsewhere", (App.Settings.debug.breakU & 1) != 0, o => { App.Settings.debug.breakU ^= 1; _SetExceptions(2); });
		m["    Exception types..."] = _DExceptionTypes;
		m.Separator();
		m.Add("Print events:", disable: true);
		m.AddCheck("    Module loaded/unloaded", (App.Settings.debug.printEvents & 1) != 0, o => { App.Settings.debug.printEvents ^= 1; });
		m.AddCheck("    Thread started/ended", (App.Settings.debug.printEvents & 2) != 0, o => { App.Settings.debug.printEvents ^= 2; });
		//CONSIDER: "Topmost when floating"
		//CONSIDER: "Activate LA when paused". Now activates.
		
		void _DExceptionTypes(PMItem mi) {
			var w = new KDialogWindow();
			w.InitWinProp("Exception types", App.Wmain);
			var b = new wpfBuilder(w).WinSize(700, 500).Columns(-1, 8, -1);
			
			b.R.Add(out KSciInfoBox info).Height(90);
			info.aaaText = """
Exception types for 'Break on exception' options.
If the list is empty or inactive, will break on all exceptions.
Example:
System.DivideByZeroException
System.IO.FileNotFoundException
//comment
""";
			
			b.Row(-1).StartGrid().Columns(-1);
			b.Add<Label>("When thrown").Margin("B0").Row(-1).Add(out TextBox eListT, App.Settings.debug.breakListT, labeledBy: b.Last).Multiline(wrap: TextWrapping.NoWrap);
			b.Add(out KCheckBox cOnlyT, "The list is active").Checked((App.Settings.debug.breakT & 2) != 0);
			b.End();
			
			b.Skip().StartGrid().Columns(-1);
			b.Add<Label>("If unhandled in user code").Margin("B0").Row(-1).Add(out TextBox eListU, App.Settings.debug.breakListU, labeledBy: b.Last).Multiline(wrap: TextWrapping.NoWrap);
			b.Add(out KCheckBox cOnlyU, "The list is active").Checked((App.Settings.debug.breakU & 2) != 0);
			b.End();
			
			b.R.Skip(2).AddOkCancel();
			if (!w.ShowAndWait()) return;
			
			App.Settings.debug.breakListT = eListT.TextOrNull();
			App.Settings.debug.breakListU = eListU.TextOrNull();
			App.Settings.debug.breakT.SetFlag_(2, cOnlyT.IsChecked);
			App.Settings.debug.breakU.SetFlag_(2, cOnlyU.IsChecked);
			_SetExceptions(3);
		}
	}
	
	void _SetOptions() {
		if (App.Settings.debug.stepIntoAll) _d.Send("-gdb-set enable-step-filtering 0");
		//if (App.Settings.debug.stepIntoExternal) _d.Send("-gdb-set just-my-code 0");
	}
	
	/// <param name="action">0 init, 1 change 'throw', 2 change 'user-unhandled', 3 change both.</param>
	void _SetExceptions(int action) {
		if (!IsDebugging) return;
		if (action is 0 or 1 or 3) _Apply("throw", App.Settings.debug.breakT, App.Settings.debug.breakListT, ref _s.exceptionsT);
		if (action is 0 or 2 or 3) _Apply("user-unhandled", App.Settings.debug.breakU, App.Settings.debug.breakListU, ref _s.exceptionsU);
		
		void _Apply(string tuu, int flags, string types, ref string[] ids) {
			if (action != 0 && ids != null) _d.SendSync(3, $"-break-exception-delete {string.Join(' ', ids)}");
			ids = null;
			if ((flags & 1) != 0) {
				string etypes = "*";
				if ((flags & 2) != 0 && !types.NE()) {
					using (new StringBuilder_(out var sb)) {
						foreach (var v in types.Split_('\n')) {
							if (!v.Starts("//")) sb.Append(' ').Append(v);
						}
						if (sb.Length == 0) return;
						etypes = sb.ToString();
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
			if (n != 0 && n != _s.runToHere) _d.Send($"-break-delete {n}");
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
				if (log[0] != '"') log = log.Escape(quote: true);
				if (cond.NE()) cond = "true";
				s = $"Au.More.Debug_._Logpoint({cond}, {log}, \"{b.File.IdStringWithWorkspace}|{b.Line + 1}\")";
			}
		} else s = "false";
		_d.Send($"-break-condition {b.Id} {s}");
		//note: without ""
	}
	
	#endregion
	
	bool _CanResume() {
		if (!IsStopped || _s.resuming) return false;
		return _s.resuming = true;
	}
	
	void _Step(string s) {
		if (!_CanResume()) return;
		_d.Send(_s.threadId != 0 ? $"-exec-{s} --thread {_s.threadId}" : $"-exec-{s}");
	}
	void _Next() {
		_Step("next");
	}
	
	void _Step() {
		_Step("step");
	}
	
	void _StepOut() {
		_Step("finish");
	}
	
	void _Continue() {
		if (_CanResume()) _d.Send($"-exec-continue");
	}
	
	internal void AddMarginMenuItems_(SciCode doc, popupMenu m, int line) {
		if (IsStopped) m["Run to here", "*JamIcons.ArrowCircleDownRight #EE3000 @14"] = o => _RunToHere(doc.EFile, line);
	}
	
	void _RunToHere(FileNode f, int line) {
		if (!_CanResume()) return;
		_s.runToHere = _SetBreakpoint(f, line);
		_d.Send($"-exec-continue");
	}
	
	void _Pause() {
		if (!IsDebugging || IsStopped) return;
		_s.pausing = true;
		_d.Send($"-exec-interrupt");
	}
	
	void _End() {
		if (IsDebugging) _d.Send($"-exec-abort");
	}
	
	void _Disconnect() {
		if (IsDebugging) _d.Send($"-gdb-exit"); //detach and exit debugger
	}
	
	void _Events(string s) {
#if DEBUG
		if (0 == s.Starts(true, "^done", "=library-", "=thread-", "=message,")) print.it("EVENT", s);
#endif
		
		if (_s == null) {
			Debug_.Print("_s==null");
			return;
		}
		
		if (s == "^exit") {
			_d.Dispose();
			_d = null;
			IsDebugging = IsStopped = false;
			_UpdateUI(_UU.Ended);
			RegHotkeys.UnregisterDebug();
			
			if (_restart) {
				_restart = false;
				var file = _s.file;
				timer.after(100, _ => _Start(file));
			} else if (_hidePanelWhenEnds) {
				_hidePanelWhenEnds = false;
				Panels.PanelManager[Panels.Debug.P].Visible = false;
			}
			
			_s = null;
			print.it("<><lc #C0C0FF><>");
		} else if (s == "1^done") { //attached
			_s.attached = true;
			_UpdateUI(_UU.Started);
			_PrintThread(null, true);
			RegHotkeys.RegisterDebug();
		} else if (s.Starts("1^error,msg=")) { //failed to attach. Eg when the process is admin but editor isn't.
			_Print("Failed to attach debugger. " + s[12..]);
			_d.Send($"-gdb-exit");
		} else if (s == "^running") {
			IsStopped = _s.resuming = false;
			_s.frame = null;
			_UpdateUI(_UU.Resumed);
		} else if (s.Starts("*stopped,")) {
			if (s.Eq(17, "exited")) {
				IsDebugging = IsStopped = _s.resuming = false;
				_d.Send($"-gdb-exit");
				_UpdateUI(_UU.Ended);
				if (s.RxMatch(@"\bexit-code=""(.+?)""", 1, out string ec)) _Print($"Process ended. Exit code: {ec}.");
			} else {
				//part 2 of the netcoredbg crash workaround
				if (_s.workaround1) {
					_s.workaround1 = false;
					if (s.Starts("""*stopped,reason="signal""")) return;
				}
				
				IsStopped = true;
				//_s.stopCount++;
				
				_s.inStopped = true;
				try { _Stopped(s); }
				finally { _s.inStopped = false; }
				
				_UpdateUI(_UU.Paused);
			}
		} else if (s.Starts("^done,stack=")) {
			if (!IsStopped) return;
			_FRAME[] a = new _MiRecord(s).Data<_DONE_STACK>().stack;
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
				int tid = s.ToInt(19);
				if (tid == _s.threadId) {
					_s.threadId = 0;
					
					//workaround for debugger bug: netcoredbg crashes later when pausing, ~1/5 times
					//	FUTURE: remove the workaround when the debugger will fix it.
					if (!IsStopped) {
						_s.workaround1 = true;
						_d.Send($"-exec-interrupt");
						_d.Send("1000-exec-continue");
					}
				}
			} else if (s.Starts("=message,")) { //Debug.Print etc, like =message,text="TEXT\r\n",send-to="output-window",source="Debugger.Log"
				var r = new _MiRecord(s);
				var text = ((string)r.data["text"]).TrimEnd();
				_Print(text.Starts("<>") ? text[2..] : $"<\a>{text}</\a>");
				//BAD: not in sync with print.it.
			}
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
	}
	
	void _Stopped(string s) {
		var x = new _MiRecord(s).Data<_STOPPED>();
		int pauseWorkaround = -1;
		
		switch (x.reason) {
		case "end-stepping-range":
			
			break;
		case "breakpoint-hit":
			if (_s.runToHere != 0 && x.bkptno == _s.runToHere) {
				if (!Panels.Breakpoints.GetBreakpoints().Any(o => o.Id == x.bkptno)) _d.Send($"-break-delete {_s.runToHere}");
				_s.runToHere = 0;
			} //never mind: does not delete the _runToHere breakpoint if stops there on exception
			break;
		case "exception-received":
			if (_StoppedOnException(x)) return;
			break;
		case "signal-received":
			//part 1 of workaround for: random x.thread_id if never stopped or the last stopped thread ended. Switch to the main thread.
			//	FUTURE: change the code if netcoredbg will fulfill this feature request: https://github.com/Samsung/netcoredbg/issues/150
			if (_s.pausing && x.thread_id != _s.threadId) pauseWorkaround = _s.threadId;
			
			//if (x.frame.func == "Au.More.DebugTraceListener.Fail()") {
			//	print.it("step out");
			//	_StepOut();
			//	return;
			//}
			break;
		}
		
		_s.pausing = false;
		_s.threadId = x.thread_id;
		
		if (_GetThreads() is _THREAD[] a) { //fast
			a = a.Where(t => t.id == _s.threadId || !_IsHiddenThreadName(t.name)).ToArray();
			bool same = a.Length == _cbThreads.Items.Count;
			if (same) for (int i = 0; i < a.Length; i++) if (!(same = _cbThreads.Items[i] is ComboBoxItem k && k.Tag is _THREAD t && t == a[i])) break;
			if (!same) {
				_cbThreads.Items.Clear();
				foreach (var t in a) _cbThreads.Items.Add(new ComboBoxItem { Content = $"{t.id}  {t.name}", Tag = t });
			}
			
			int iSel = Array.FindIndex(a, t => t.id == _s.threadId);
			
			if (pauseWorkaround != -1 && !(iSel == 0 && pauseWorkaround == 0)) { //part 2 of the workaround
				iSel = pauseWorkaround == 0 ? 0 : Array.FindIndex(a, t => t.id == pauseWorkaround);
				if (iSel < 0) iSel = 0;
				_s.threadId = a[iSel].id;
			} else {
				_GoToLine(x.frame);
			}
			
			if (iSel >= 0) {
				if (iSel != _cbThreads.SelectedIndex) _cbThreads.SelectedIndex = iSel; //_cbThreads.SelectionChanged -> _SelectedThread
				else _SelectedThread(a[iSel]);
			}
		}
		
		_THREAD[] _GetThreads() {
			if (_d.SendSync(5, "-thread-info") is string s && s.Starts("^done,threads=")) {
				_THREAD[] a = new _MiRecord(s).Data<_DONE_THREADS>().threads;
				foreach (var t in a) _GetThreadNameAndTime(t.id, out t.name, out t.time);
				a = a.OrderBy(o => o.time).ToArray();
				a[0].name ??= "Main Thread";
				return a;
			}
			return null;
		}
		
		bool _StoppedOnException(_STOPPED x) {
			var stage = x.exception_stage switch { "throw" => "thrown", "user-unhandled" => "unhandled in user code", _ => x.exception_stage };
			if ((App.Settings.debug.breakT & 1) != 0) { //continue if this is a duplicate stop when using option 'break when thrown'
				if (x.exception_stage == "throw") _s.thrownException = x;
				else if (_s.thrownException is { } xx) {
					_s.thrownException = null;
					if (x == xx with { exception_stage = x.exception_stage }) {
						_Print($"<c red>Exception {stage}.<>");
						_Continue();
						return true;
					}
				}
			}
			var b = new StringBuilder($"<c red>{x.exception_name} {stage}.\r\n\tMessage: <\a>{x.exception}</\a>\r\n\tCall stack: <><fold>\r\n");
			if (_d.SendSync(6, $"-stack-list-frames --thread {x.thread_id}") is string s && s.Starts("^done,stack=")) {
				_FRAME[] a = new _MiRecord(s).Data<_DONE_STACK>().stack;
				foreach (var f in a) {
					b.Append("\t\t").AppendLine(_FormatFrameString(f, true));
				}
			}
			b.Append("</fold>");
			_Print(b.ToString());
			return false;
			//FUTURE: show exception UI.
		}
	}
	
	void _SelectedThread(_THREAD t) {
		//print.it(t.id);
		if (!IsStopped) return;
		if (!_s.inStopped) _VariablesViewChangedFrameOrThread();
		if (t.id != _s.threadId) {
			_s.threadId = t.id;
			_ClearTreeviewsAndMarkers();
		}
		_d.Send($"-stack-list-frames --thread {_s.threadId}");
	}
	
	bool _GoToLine(_FRAME f, bool keepMarkers = false) {
		//_frameMarker = null;
		if (f != null) {
			int line = f.line - 1, col = f.col - 1;
			if (App.Model.OpenAndGoTo(f.fullname, line, col)) {
				if (f.level > 0) {
					_marker2.Add(SciCode.c_markerDebugLine2, line, col);
					//_frameMarker = _marker2;
				} else {
					_marker2.Delete();
					_marker.Add(SciCode.c_markerDebugLine, line, col);
					//_frameMarker = _marker;
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
	_Marker _marker = new(), _marker2 = new();
	//_Marker _frameMarker;
	
	void _UpdateUI(_UU u) {
		if (u == _UU.Resumed) {
			_timerResumeUU ??= new(_ => _UpdateUI(_UU.Resumed2));
			_timerResumeUU.After(100);
			return;
		}
		
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
		
	}
	
	enum _UU { Init, Started, Ended, Paused, Resumed, Resumed2 }
	
	timer _timerResumeUU;
	
	void _ClearTreeviewsAndMarkers() {
		_StackViewSetItems(null);
		_VariablesViewSetItems(null);
		_marker.Delete();
		_marker2.Delete();
	}
	
	#region util
	
	static void _Print(string s) {
		print.it($"<><lc #f8f8d0>{s}<>");
	}
	
	static unsafe bool _GetThreadNameAndTime(int id, out string name, out long time) {
		name = null;
		using var th = Api.OpenThread(Api.THREAD_QUERY_LIMITED_INFORMATION, false, id);
		Api.GetThreadTimes(th, out time, out _, out _, out _);
		if (time == 0) {
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
	
	static bool _IsHiddenThreadName(string s) => s is "Au.Aux" or ".NET TP Gate" or ".NET Tiered Compilation Worker" or ".NET Counter Poller";
	
	string _GetModuleName(_FRAME f) {
		if (_s.modules.TryGetValue(f.clr_addr.module_id, out var r)) r = pathname.getName(r);
		return r;
	}
	
	string _FormatFrameString(_FRAME f, bool forPrint = false) {
		if (f.file.NE()) return $"{_GetModuleName(f)}!{f.func}";
		if (forPrint) return $"<open {f.file}|{f.line}><\a>{f.func}  ●  {f.file}:{f.line}</\a><>";
		return $"{f.func}  ●  {f.file}:{f.line}";
	}
	
	#endregion
	
	class _Marker {
		SciCode _doc;
		int _line, _column, _handle;
		
		public void Add(int marker, int line, int column) {
			_column = column;
			var doc = Panels.Editor.ActiveDoc;
			if (line == _line && doc == _doc) return;
			Delete();
			_doc = doc;
			_line = line;
			_handle = _doc.aaaMarkerAdd(marker, line);
		}
		
		public void Delete() {
			if (_doc == null) return;
			if (!_doc.AaWnd.Is0) _doc.aaaMarkerDeleteHandle(_handle);
			_doc = null;
			_line = 0;
			_handle = 0;
		}
		
		public bool Exists => _doc != null;
		
		public SciCode Doc => _doc;
		public int Line => _line;
		public int Column => _column;
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
//1. Debugger steps into cast operators. Code to reproduce:
/*
var c = new C();
_ = c.Prop;
_ = c[0];
c[0] = 0;
_ = c - c;
string s = c; //steps into
int k = (int)c; //steps into
;

class C {
	public int Prop {
		get {
			return 4;
		}
	}
	
	public int this[int i] {
		get {
			return 9;
		}
		set {
			;
		}
	}
	
	public static int operator -(C c, C d) {
		return 7;
	}
	
	public static implicit operator string(C c) {
		return "aa";
	}
	
	public static explicit operator int(C c) {
		return 4;
	}
}
*/
//2. Debugger crashes when evaluating expression like `c[0]` with evalFlags EVAL_NOFUNCEVAL.
//Command string: $"-var-create - \"c[0]\" --frame 0 --evalFlags 128"
//Here c is a variable of a class with an indexer.
//Can use the same code to reproduce as in 1. Or can use:
/*
var c = new List<int>() { 1, 2 };
Debugger.Break();
*/
//3. The used Roslyn dlls are very old. They don't support newer C# features.
//4. The Roslyn scripting dlls are not used. Instead use <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />.
//5. Both protocols (MI and VSCode) should support everything supported by the debugger. Examples:
/*
Now MI protocol supports setting options at any time (-gdb-set), but VSCode protocol only in the launch request (bot not in the attach request). Also it seems MI supports an additional option 'hot reload' (I didn't test so far).
Now MI protocol does not support --thread in -exec-continue and -exec-interrupt, although VSCode supports it.
Now MI protocol does not support ExceptionBreakpoint.negativeCondition (prefix '!' in VSCode protocol).
*/
