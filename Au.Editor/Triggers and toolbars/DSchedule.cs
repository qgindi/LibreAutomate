using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml;
using System.Xml.Linq;
using api = WinSchedulerApi;
using TT = WinSchedulerApi.TASK_TRIGGER_TYPE2;
using TTS = WinSchedulerApi.TASK_SESSION_STATE_CHANGE_TYPE;

class DSchedule : KDialogWindow {
	public static void ShowFor(FileNode f, int triggerIndex1Based = 0) {
		if (f == null) return;
#if TEST
		var d = new DSchedule(f);
		d.ShowDialog();
		//d.ShowAndWait();
		//d.Show();
		//dialog.show("DSchedule");
#else
		if (!f.IsExecutableDirectly()) {
			dialog.showInfo(null, "This file isn't runnable as a script.", owner: App.Wmain);
			return;
		}
		f.SingleDialog(() => new DSchedule(f, triggerIndex1Based) { Owner = App.Wmain, ShowInTaskbar = false });
#endif
	}
	
	FileNode _f;
	wpfBuilder _b;
	ListBox _lbPages;
	Grid _gPages;
	event Action<WBButtonClickArgs> _apply;
	
	api.ITaskDefinition _td;
	string _taskName;
	
	static readonly string s_taskFolder = @"\Au\" + Environment.UserName;
	
	DSchedule(FileNode f, int selectTrigger) {
		_f = f;
		
		Title = "Schedule - " + f.Name;
		
		var b = _b = new wpfBuilder(this).WinSize(700, 420).Columns(-1.2, 8, -2);
		b.Options(bindLabelVisibility: true);
		
		//left column
		
		Button bDeleteTrigger = null;
		
		b.Row(-1).StartGrid().Columns(-1);
		
		b.Row(-1).Add(out _lbPages);
		ScrollViewer.SetHorizontalScrollBarVisibility(_lbPages, ScrollBarVisibility.Disabled); //to wrap item text
		
		b.StartGrid().Columns(-1, -1);
		b.AddButton("New trigger", _ => _NewTrigger(true));
		b.AddButton(out bDeleteTrigger, "Delete trigger", _ => _DeleteTrigger()).Disabled();
		b.End();
		b.R.Add(out KCheckBox cTaskEnabled, "Task enabled").Margin("T10B10");
		
		b.End();
		
		//right column
		
		b.Skip(1).Add(out _gPages).Margin("0");
		
		b.R.AddSeparator().Span(-1);
		
		b.R.StartOkCancel();
		b.xAddInfoBlockT(out var tExistsInfo, "New scheduled task.");
		b.AddOkCancel(out var bOK, out _, out _, apply: "_Apply");
		b.AddButton("Task Scheduler ▾", _ => {
			var m = new popupMenu();
			m["OK, open task in Task Scheduler"] = o => {
				bOK.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				if (!IsVisible) _EditTask(); //may be not closed if validation error
			};
			bool disabled = !_TaskExists();
			m["Cancel, open task in Task Scheduler", disable: disabled] = o => { Close(); _EditTask(); };
			m.Submenu("Delete scheduled task", m => {
				m["Delete"] = o => {
					WinScheduler.DeleteTask(s_taskFolder, _taskName);
					Close();
				};
			}, disable: disabled);
			m["Open Task Scheduler"] = o => { _EditTask(true); };
			m.Show(owner: this);
			
			void _EditTask(bool justOpenFolder = false) {
				WinScheduler.EditTask(s_taskFolder, justOpenFolder ? null : _taskName);
			}
			
			bool _TaskExists() => WinScheduler.TaskExists(s_taskFolder, _taskName);
		});
		b.End();
		b.End();
		
		b.OkApply += o => {
			//print.clear();
			
			_td.Settings.Enabled = cTaskEnabled.IsChecked;
			_apply(o);
			
			//update trigger strings in listbox
			var tc = _td.Triggers;
			for (int i = 1; i < _lbPages.Items.Count - 2; i++) {
				if (0 == tc.get_Item(i, out var v)) {
					var li = _lbPages.Items[i] as ListBoxItem;
					li.Content = _CreatePageListItemContent(v.FormatTriggerString(), true);
				}
			}
			
			//print.it(_td.XmlText); print.scrollToTop();
			
			int hr = WinScheduler.CreateTaskFromDefinition(s_taskFolder, _taskName, _td, UacIL.High, api.TASK_CREATION.TASK_CREATE_OR_UPDATE);
			if (hr != 0) {
				string s = lastError.messageFor(hr);
				if (hr == Api.E_ACCESSDENIED && !uacInfo.isAdmin) s += "\n\nRestart this program as administrator.";
				dialog.showError("Failed to register scheduled task", s, owner: this);
				o.Cancel = true;
			} else {
				tExistsInfo.Hide_(true);
			}
		};
		
		bool exists = WinScheduler.FindScriptTask(_f.Name, _f.ItemPath, out var r);
		if (exists) {
			_taskName = r.rt.Name;
			_td = r.td;
		} else {
			_taskName = _f.Name[..^3];
			if (!WinScheduler.Connect(out var ts)) throw new AuException("*get task service");
			
			var sArgs = "*" + _f.ItemPathOrName();
			if (0 != ts.NewTask(0, out _td)) throw new AuException("*create task definition");
			_td.XmlText = WinScheduler.CreateXmlForNewTask(UacIL.High, WinScheduler.EditorPath, sArgs);
			
			_NewTrigger(false);
		}
		tExistsInfo.Hide_(exists);
		//print.it(_td.XmlText);
		
		cTaskEnabled.IsChecked = _td.Settings.Enabled;
		
		_AddPage("Triggers", null);
		_AddPage("Conditions", _ConditionsPage());
		_AddPage("Settings", _SettingsPage(r.scriptArgs));
		
		foreach (var t in _td.EnumTriggers()) {
			_CreateTriggerPage(t, false);
		}
		
		_lbPages.SelectionChanged += (_, e) => {
			int i = _lbPages.SelectedIndex, n = _lbPages.Items.Count;
			if (i == 0) {
				_lbPages.SelectedIndex = n > 3 ? 1 : -1;
			} else {
				if (e.RemovedItems is [ListBoxItem { Tag: UIElement p1 }]) p1.Visibility = Visibility.Hidden;
				if (e.AddedItems is [ListBoxItem { Tag: UIElement p2 }]) p2.Visibility = Visibility.Visible;
				bDeleteTrigger.IsEnabled = i < n - 2;
			}
		};
		
#if TEST
		//_lbPages.SelectedIndex = _lbPages.Items.Count - 2;
		_lbPages.SelectedIndex = 1;
#else
		int nTriggers = _lbPages.Items.Count - 3;
		if (selectTrigger > 0 && selectTrigger <= nTriggers) _lbPages.SelectedIndex = selectTrigger;
		else if (nTriggers > 0) _lbPages.SelectedIndex = 1;
#endif
		
#if WPF_PREVIEW
		b.Window.Preview();
#endif
	}
	
	void _AddPage(string name, _Page page) {
		var li = new ListBoxItem { Content = _CreatePageListItemContent(name, page is _TriggerPage), Tag = page };
		_lbPages.Items.Insert(_lbPages.Items.Count - (page is _TriggerPage ? 2 : 0), li);
		if (page != null) {
			page.li = li;
			page.Visibility = Visibility.Collapsed;
			_gPages.Children.Add(page);
		}
	}
	
	object _CreatePageListItemContent(string text, bool isTrigger) {
		if (!isTrigger) return text;
		var r = new DockPanel();
		var img = ImageUtil.LoadWpfImageElement("*Codicons.SymbolEvent #4066FF @14");
		img.VerticalAlignment = VerticalAlignment.Top;
		img.Margin = new(-3, 0, 0, 2);
		r.Children.Add(img);
		var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
		r.Children.Add(t);
		return r;
	}
	
	class _Page : UserControl {
		public ListBoxItem li;
		
		public int Index => ((ListBox)li.Parent).Items.IndexOf(li);
	}
	
	_Page _ConditionsPage() {
		var sett = _td.Settings;
		
		_Page uc = new();
		var b = new wpfBuilder(uc);
		b.Options(bindLabelVisibility: true);
		
		//print.it(sett.);
		
		b.R.xAddGroupSeparator("Idle");
		
		_TimeIntervalControls kIdle = new(this, b, "Start only if the computer is idle for:", "1 m|10 m|1 h", 1, ("PT1M", "P31D"));
		_TimeIntervalControls kIdleWait = new(this, b, "Wait for idle for", "0 s|1 m|10 m|1 h", 3, cOther: kIdle.c);
		b.Tooltip("Max time interval to wait for the above condition");
		b.Last2.HorizontalAlignment = HorizontalAlignment.Right;
		b.R.Add(out KCheckBox cIdleStop, "Stop if the computer ceases to be idle").Margin(20).xBindCheckedEnabled(kIdle.c)
			.Tooltip("Note: triggers of type \"Computer idle\" may stop the task even if this isn't checked.");
		b.R.Add(out KCheckBox cIdleRestart, "Restart when idle again").Margin(40).xBindCheckedEnabled(kIdle.c);
		if (sett.RunOnlyIfIdle) {
			var idle = sett.IdleSettings;
			kIdle.Value = idle.IdleDuration;
			kIdleWait.Value0 = idle.WaitTimeout;
			cIdleStop.IsChecked = idle.StopOnIdleEnd;
			cIdleRestart.IsChecked = idle.RestartOnIdle;
		}
		
		b.R.xAddGroupSeparator("Power");
		
		b.R.Add(out KCheckBox cDisallowStartIfOnBatteries, "Start only if the computer is on AC power").Checked(sett.DisallowStartIfOnBatteries);
		b.R.Add(out KCheckBox cStopIfGoingOnBatteries, "Stop if the computer switches to battery power").Margin(20).Checked(sett.StopIfGoingOnBatteries).xBindCheckedEnabled(cDisallowStartIfOnBatteries);
		b.R.Add(out KCheckBox cWakeToRun, "Wake the computer to run this task").Checked(sett.WakeToRun);
		
		b.R.xAddGroupSeparator("Network");
		
		b.R.Add(out KCheckBox cNetwork, "Required network connection:").Checked(sett.RunOnlyIfNetworkAvailable);
		b.Add(out TextBox tNetwork).xBindCheckedEnabled(cNetwork, true);
		if (sett.RunOnlyIfNetworkAvailable && sett.NetworkSettings is { } ns) tNetwork.Text = ns.Name;
		
		b.End();
		
		_apply += o => {
			try {
				if (sett.RunOnlyIfIdle = kIdle.c.IsChecked) {
					var idle = sett.IdleSettings;
					idle.IdleDuration = kIdle.Value;
					idle.WaitTimeout = kIdleWait.Value;
					idle.StopOnIdleEnd = cIdleStop.IsChecked;
					idle.RestartOnIdle = cIdleRestart.IsChecked;
				}
				sett.DisallowStartIfOnBatteries = cDisallowStartIfOnBatteries.IsChecked;
				sett.StopIfGoingOnBatteries = cStopIfGoingOnBatteries.IsChecked;
				sett.WakeToRun = cWakeToRun.IsChecked;
				if (sett.RunOnlyIfNetworkAvailable = cNetwork.IsChecked) sett.NetworkSettings.Name = tNetwork.TextOrNull();
			}
			catch (Exception ex) { dialog.showError("Failed to apply Conditions", ex.ToString()); } //unlikely (all validated)
		};
		
		return uc;
	}
	
	_Page _SettingsPage(string scriptArgs) {
		var sett = _td.Settings;
		
		_Page uc = new();
		var b = new wpfBuilder(uc);
		b.Options(bindLabelVisibility: true);
		
		b.R.Add(out KCheckBox cRunAfterMissed, "Run task later if a scheduled start is missed").Checked(sett.StartWhenAvailable);
		
		_TimeIntervalControls kRetry = new(this, b, "If can't start the task, retry every:", "1 m|10 m|1 h", 0, ("PT1M", "P31D"));
		kRetry.c.ToolTip = "When Task Scheduler can't start the task when it should. This does not include failures in LibreAutomate or script, such as \"LibreAutomate can't find or start the script\" or \"the script process crashed or returned not 0\".";
		var v1 = (n: sett.RestartCount, t: sett.RestartInterval);
		bool restart = v1.n > 0 && v1.t != null;
		if (restart) kRetry.Value = v1.t;
		
		b.R.Add("Max times", out TextBox tFailedRetryTimes, restart ? v1.n : 3).xBindCheckedEnabled(kRetry.c)
			.Validation(o => _ValidateNumber(o, 1, 999), _ValidationLinkClick);
		b.Last2.HorizontalAlignment = HorizontalAlignment.Right;
		
		_TimeIntervalControls kStop = new(this, b, "Stop the task if it runs longer than:", "30 s|5 m|1 h|3 d", 3) { Value = sett.ExecutionTimeLimit };
		_TimeIntervalControls kDelete = new(this, b, "Delete expired task after:", "0 s|30 d|365 d", 1, checkboxValidation: _ValidationForDeleteExpired) { Value0 = sett.DeleteExpiredTaskAfter };
		
		b.StartDock()
			.Add("If the task is already running", out ComboBox cbIfRunning)
			.Items("Start a new instance|Queue a new instance|Don't start a new instance|Stop the existing instance")
			.Select(Math.Clamp((int)sett.MultipleInstances, 0, 3))
			.End();
		
		b.R.AddSeparator().Margin("T8B8");
		b.R.StartDock().Add("Script arguments", out TextBox tArgs, scriptArgs)
			.Tooltip("Optional string to pass to the script process in command line. The script receives parsed arguments in the args variable.")
			.End();
		
		b.R.AddSeparator().Margin("T8");
		b.Row(-1).xAddInfoBlockT("""
This tool creates or updates a Windows Task Scheduler task that starts LibreAutomate with a command to run this script. You can edit the task here or in Task Scheduler. All info about Task Scheduler is on the internet.

To run a script without LibreAutomate, instead create .exe program from the script, and in Task Scheduler create a task to run it.
""", scrollViewer: true);
		
		b.End();
		
		_apply += o => {
			try {
				sett.StartWhenAvailable = cRunAfterMissed.IsChecked;
				sett.RestartInterval = kRetry.Value;
				sett.RestartCount = kRetry.c.IsChecked ? tFailedRetryTimes.Text.ToInt() : 0;
				sett.ExecutionTimeLimit = kStop.Value ?? "PT0S"; //if null, will be the default 3 days
				sett.DeleteExpiredTaskAfter = kDelete.Value;
				sett.MultipleInstances = cbIfRunning.SelectedIndex;
				
				var x = _td.Actions.GetExecAction(1);
				var s = $"\">{_f.ItemPathOrName()}\"";
				if (tArgs.TextOrNull() is { } sa) s = s + " " + sa;
				x.Arguments = s;
			}
			catch (Exception ex) { dialog.showError("Failed to apply Settings", ex.ToString()); } //unlikely (all validated)
		};
		
		return uc;
		
		string _ValidationForDeleteExpired(FrameworkElement e) {
			if (!((KCheckBox)e).IsChecked) return null;
			if (_lbPages.Items.OfType<ListBoxItem>().Any(o => o.Tag is _TriggerPage { cExpire: { IsChecked: true } })) return null;
			return "Please also check Expire for a trigger";
		}
	}
	
	const TT c_unsupportedTT = (TT)(-1);
	readonly TT[] _ttMap = [TT.TIME, TT.DAILY, TT.WEEKLY, TT.MONTHLY, TT.MONTHLYDOW, TT.LOGON, TT.SESSION, TT.EVENT, TT.IDLE, TT.REGISTRATION, c_unsupportedTT];
	
	void _CreateTriggerPage(api.ITrigger t, bool select) {
		_TriggerPage page = new() { trigger = t };
		var b = new wpfBuilder(page).Columns(60, -1);
		b.Options(bindLabelVisibility: true);
		
		b.R.Add("Schedule", out ComboBox cbTriggerType).Span(1)
			.Items("Once|Daily|Weekly|Month days|Month week days|Log on|User session event|Event log|Computer idle|This task created or modified").Select(-1);
		
		var tt = t.Type;
		if (!_ttMap.Contains(tt) || tt == c_unsupportedTT) {
			tt = c_unsupportedTT;
			cbTriggerType.Items.Add("<unsupported trigger type>");
		}
		
		var dtf = CultureInfo.InvariantCulture.DateTimeFormat;
		var aWeekDayNames = dtf.DayNames;
		var aMonthNames = dtf.MonthNames.Take(12).ToArray();
		string defaultUserId = $@"{Environment.MachineName}\{Environment.UserName}";
		_TimeIntervalControls kDelay = null;
		
		var ap = new (Panel panel, Action<WBButtonClickArgs> apply)[11];
		var ab = new Action<int>[11] { _Once, _Daily, _Weekly, _Month, _MonthDOW, _Logon, _Session, _Eventlog, _Idle, _Registration, _Unsupported };
		
		wpfBuilder _BuilderProlog(int iType, params WBGridLength[] widths) {
			var b = new wpfBuilder();
			b.Options(bindLabelVisibility: true);
			if (widths.Length > 0) b.Columns(widths); else b.Columns(60, -1);
			ap[iType].panel = b.Panel;
			return b;
		}
		
		T _ApplyProlog<T>(TT type) where T : class {
			if (tt != type) {
				//There is no API "replace trigger type" or "replace trigger at" or "insert trigger at".
				//	To change trigger type, need to remove the trigger and add new trigger.
				//	But it adds to the end. Workaround: remove/add the listbox item too. Nevermind, it's rare.
				
				var old = t;
				page.trigger = t = _td.Triggers.Create(tt = type);
				t.Id = old.Id;
				
				int i = page.Index;
				_td.Triggers.Remove(i);
				
				if (i < _td.Triggers.Count) {
					bool select = page.li == _lbPages.SelectedItem;
					_lbPages.Items.Remove(page.li);
					_lbPages.Items.Insert(_lbPages.Items.Count - 2, page.li);
					if (select) {
						_lbPages.SelectedItem = page.li;
						_lbPages.ScrollIntoView(page.li);
					}
				}
			}
			return t as T;
		}
		
		//- trigger types
		b.Row((0, 100..)).StartGrid().Columns(60, -1, 0);
		
		_DateTimeControls dtStart = new(this, b, 1, "Start") { Value = _IsDateTrigger() ? t.StartBoundary : null };
		
		b.R.Add(out ContentControl cc).Margin("0");
		
		void _Once(int iType) {
			if (tt == TT.TIME && t is api.ITimeTrigger m) {
				kDelay.Value = m.RandomDelay;
			}
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.ITimeTrigger>(TT.TIME);
				m.RandomDelay = kDelay.Value;
			};
		}
		
		void _Daily(int iType) {
			var b = _BuilderProlog(iType, 60, 50, -1);
			b.R.Add("Every", out TextBox tEvery, "1")
				.Validation(o => _ValidateNumber(tEvery, 1, 365), _ValidationLinkClick)
				.Add<Label>("days");
			b.End();
			if (tt == TT.DAILY && t is api.IDailyTrigger m) {
				tEvery.Text = m.DaysInterval.ToString();
				kDelay.Value = m.RandomDelay;
			}
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.IDailyTrigger>(TT.DAILY);
				m.DaysInterval = (short)tEvery.Text.ToInt();
				m.RandomDelay = kDelay.Value;
			};
		}
		
		void _Weekly(int iType) {
			var b = _BuilderProlog(iType, 60, 50, -1);
			b.R.Add("Every", out TextBox tEvery, "1")
				.Validation(o => _ValidateNumber(tEvery, 1, 99), _ValidationLinkClick)
				.Add<Label>("weeks");
			var cdWeeks = new KCheckDropdownBox { Checkboxes = aWeekDayNames, TextWrapping = TextWrapping.Wrap };
			b.R.Add("On", cdWeeks)
				.Validation(_ValidateBits, _ValidationLinkClick);
			if (tt == TT.WEEKLY) {
				var m = (api.IWeeklyTrigger)t;
				tEvery.Text = m.WeeksInterval.ToString();
				cdWeeks.Checked = m.DaysOfWeek;
				kDelay.Value = m.RandomDelay;
			}
			b.End();
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.IWeeklyTrigger>(TT.WEEKLY);
				m.WeeksInterval = (short)tEvery.Text.ToInt();
				m.DaysOfWeek = (ushort)cdWeeks.Checked;
				m.RandomDelay = kDelay.Value;
			};
		}
		
		void _Month(int iType) {
			var b = _BuilderProlog(iType);
			var cdMonths = new KCheckDropdownBox { Checkboxes = aMonthNames, AllItem = "All months", AllText = "All months", TextWrapping = TextWrapping.Wrap };
			b.R.Add("Months", cdMonths)
				.Validation(_ValidateBits, _ValidationLinkClick);
			var cdDays = new KCheckDropdownBox { DropdownSettings = () => new(new UniformGrid()), Checkboxes = Enumerable.Range(1, 31).Cast<object>().Append("Last").ToArray(), TextWrapping = TextWrapping.Wrap };
			b.R.Add("Days", cdDays)
				.Validation(_ValidateBits, _ValidationLinkClick);
			if (tt == TT.MONTHLY) {
				var m = (api.IMonthlyTrigger)t;
				cdMonths.Checked = m.MonthsOfYear;
				cdDays.Checked = m.DaysOfMonth | (m.RunOnLastDayOfMonth ? 1u << 31 : 0);
				kDelay.Value = m.RandomDelay;
			}
			b.End();
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.IMonthlyTrigger>(TT.MONTHLY);
				m.MonthsOfYear = (ushort)cdMonths.Checked;
				m.DaysOfMonth = (uint)(cdDays.Checked & 0x7fff_ffff);
				m.RunOnLastDayOfMonth = 0 != (cdDays.Checked & 0x8000_0000);
				m.RandomDelay = kDelay.Value;
			};
		}
		
		void _MonthDOW(int iType) {
			var b = _BuilderProlog(iType, 60, 100, -1);
			var cdMonths = new KCheckDropdownBox { Checkboxes = aMonthNames, AllItem = "All months", AllText = "All months", TextWrapping = TextWrapping.Wrap };
			b.R.Add("Months", cdMonths)
				.Validation(_ValidateBits, _ValidationLinkClick);
			var cdWeeks = new KCheckDropdownBox { Checkboxes = ["first", "second", "third", "fourth", "last"], AllText = "every", TextWrapping = TextWrapping.Wrap };
			b.R.Add("On", cdWeeks)
				.Validation(_ValidateBits, _ValidationLinkClick);
			var dDays = new KCheckDropdownBox { Checkboxes = aWeekDayNames, AllItem = "All week days", AllText = "{0} (all days)", TextWrapping = TextWrapping.Wrap };
			b.Add(dDays)
				.Validation(_ValidateBits, _ValidationLinkClick);
			if (tt == TT.MONTHLYDOW) {
				var m = (api.IMonthlyDOWTrigger)t;
				cdMonths.Checked = m.MonthsOfYear;
				cdWeeks.Checked = m.WeeksOfMonth | (m.RunOnLastWeekOfMonth ? 1u << 4 : 0);
				dDays.Checked = m.DaysOfWeek;
				kDelay.Value = m.RandomDelay;
			}
			b.End();
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.IMonthlyDOWTrigger>(TT.MONTHLYDOW);
				m.MonthsOfYear = (ushort)cdMonths.Checked;
				m.WeeksOfMonth = (ushort)(cdWeeks.Checked & 0xf);
				m.RunOnLastWeekOfMonth = 0 != (cdWeeks.Checked & 0x10);
				m.DaysOfWeek = (ushort)dDays.Checked;
				m.RandomDelay = kDelay.Value;
			};
		}
		
		void _Logon(int iType) {
			var b = _BuilderProlog(iType);
			b.R.Add("User", out ComboBox cbUser).Editable();
			string user = null;
			if (tt == TT.LOGON) {
				var m = (api.ILogonTrigger)t;
				user = m.UserId;
				kDelay.Value = m.Delay;
			}
			_InitUserCombo(cbUser, user);
			b.End();
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.ILogonTrigger>(TT.LOGON);
				var s = cbUser.Text; if (s is "" or "<any>") s = null;
				m.UserId = s;
				m.Delay = kDelay.Value;
			};
		}
		
		void _Session(int iType) {
			TTS[] ttsMap = [TTS.TASK_CONSOLE_CONNECT, TTS.TASK_CONSOLE_DISCONNECT, TTS.TASK_REMOTE_CONNECT, TTS.TASK_REMOTE_DISCONNECT, TTS.TASK_SESSION_LOCK, TTS.TASK_SESSION_UNLOCK];
			
			var b = _BuilderProlog(iType);
			b.R.Add("Event", out ComboBox cbEvent).Items("Local connection to user session|Local disconnect from user session|Remote connection to user session|Remote disconnect from user session|Workstation lock|Workstation unlock").Select(-1);
			b.R.Add("User", out ComboBox cbUser).Editable();
			string user = null;
			if (tt == TT.SESSION) {
				var m = (api.ISessionStateChangeTrigger)t;
				cbEvent.SelectedIndex = Array.IndexOf(ttsMap, m.StateChange);
				user = m.UserId;
				kDelay.Value = m.Delay;
			}
			_InitUserCombo(cbUser, user);
			b.End();
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.ISessionStateChangeTrigger>(TT.SESSION);
				var s = cbUser.Text; if (s is "" or "<any>") s = null;
				m.UserId = s;
				m.StateChange = ttsMap[cbEvent.SelectedIndex];
				m.Delay = kDelay.Value;
			};
		}
		
		void _Eventlog(int iType) {
			bool custom = false;
			var b = _BuilderProlog(iType, 60, 80, 10, 0, 100, -1, 0);
			b.R.Add("Log", out TextBox tLog)
				.Validation(o => tLog.Visibility == Visibility.Visible && tLog.Text.NE() ? "Log not specified" : null, _ValidationLinkClick);
			b.R.Add("Source", out TextBox tSource);
			b.R.Add("Event ID", out TextBox tID)
				.Validation(o => _ValidateNumber(o, 0, ushort.MaxValue, allowEmpty: true), _ValidationLinkClick);
			b.Skip().Add("Level", out ComboBox cbLevel).Items("<any>|Critical|Error|Warning|Information|Verbose");
			b.Skip().AddButton(out var bMore, "More ▾", null);
			b.R.Add("Query", out TextBox tQuery).Multiline(60..75).Hidden(null)
				.Validation(o => { if (custom) try { XElement.Parse(tQuery.Text); } catch { return "Invalid XML"; } return null; }, _ValidationLinkClick);
			if (tt == TT.EVENT) {
				var m = (api.IEventTrigger)t;
				var (q, log, source, id, level) = m.GetQuery();
				if (q != null) {
					if (log != null) {
						tLog.Text = log;
						tSource.Text = source;
						tID.Text = id;
						if (level is >= 1 and <= 5) cbLevel.SelectedIndex = level;
					} else {
						_ToggleCustom();
						tQuery.Text = q;
					}
				}
				kDelay.Value = m.Delay;
			}
			b.End();
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.IEventTrigger>(TT.EVENT);
				string q;
				if (custom) {
					q = tQuery.Text;
				} else {
					string s = "*", log = tLog.Text, source = tSource.TextOrNull(), id = tID.TextOrNull(), level = cbLevel.SelectedIndex is var si && si > 0 ? si.ToS() : null;
					if ((source ?? id ?? level) != null) {
						s = "*[System[";
						string sep = null;
						_Append(source, "Provider[@Name='", "']");
						_Append(id, "EventID=");
						_Append(level, "Level=");
						s += "]]";
						
						void _Append(string v, string pref, string suf = null) {
							if (v != null) {
								s = s + sep + pref + v + suf;
								sep = " and ";
							}
						}
					}
					var x = new XElement("QueryList",
						new XElement("Query",
							new XAttribute("Id", "0"),
							new XAttribute("Path", log),
							new XElement("Select",
								new XAttribute("Path", log),
								s
							)
						)
					);
					q = x.ToString();
				}
				m.Subscription = q;
				m.Delay = kDelay.Value;
			};
			
			bMore.Click += (_, _) => {
				var m = new popupMenu();
				m["Open Event Viewer"] = o => { run.it(@"eventvwr.msc"); };
				m["Paste event details", disable: !tLog.IsVisible] = o => {
					if (clipboard.text is { } s && s.Starts("Log Name:")) {
						foreach (var line in s.Lines()) {
							if (line.RxMatch(@"^(Log Name|Source|Event ID|Level):\h*(.+)", out var m)) {
								var v = m[2].Value;
								var c = line[m[1].Start + 2] switch { 'g' => tLog, 'u' => tSource, 'e' => tID, _ => null };
								if (c != null) c.Text = v;
								else if (cbLevel.SelectedIndex > 0) cbLevel.Text = v; //rejected: paste Level. But need to change if incorrect selected.
							}
						}
					} else {
						dialog.showInfo(null, "In Event Viewer select an event and copy its details as text.", owner: this);
					}
				};
				m.Separator();
				m.AddCheck("Custom", custom, _ => _ToggleCustom());
				m.Show(owner: this);
			};
			
			void _ToggleCustom() {
				custom ^= true;
				tLog.Collapse_(custom);
				tSource.Collapse_(custom);
				tID.Collapse_(custom);
				cbLevel.Collapse_(custom);
				tQuery.Collapse_(!custom);
			}
		}
		
		void _Idle(int iType) {
			var b = _BuilderProlog(iType);
			b.R.xAddInfoBlockT("To change idle conditions, use the Conditions page.");
			b.End();
			
			ap[iType].apply = o => {
				_ApplyProlog<api.ITrigger>(TT.IDLE);
			};
		}
		
		void _Registration(int iType) {
			if (tt == TT.REGISTRATION && t is api.IRegistrationTrigger m) {
				kDelay.Value = m.Delay;
			}
			
			ap[iType].apply = o => {
				var m = _ApplyProlog<api.IRegistrationTrigger>(TT.REGISTRATION);
				m.Delay = kDelay.Value;
			};
		}
		
		void _Unsupported(int iType) {
			var b = _BuilderProlog(iType);
			b.R.xAddInfoBlockT(t.Type == TT.BOOT ? "LibreAutomate cannot run at system startup. Delete this trigger.\nInstead create .exe program from this script, and in Task Scheduler create a task to run it." : "Cannot edit this trigger.");
			b.End();
		}
		
		void _InitUserCombo(ComboBox cb, string user) {
			if (!user.NE()) cb.Items.Add(user);
			cb.Items.Add("<any>");
			if (user != defaultUserId) cb.Items.Add(defaultUserId);
			cb.SelectedIndex = 0;
		}
		
		b.End();
		//-
		
		b.R.xAddGroupSeparator("Trigger settings");
		
		//- Trigger settings
		b.Row(-1).Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }).Margin("0")
			.StartGrid(childOfLast: true);
		
		kDelay = new _TimeIntervalControls(this, b, "", "30 s|1 m|1 h|1 d", 2, ("PT1S", "P31D"));
		
		b.R.StartGrid().Columns(0, -1, 0, -1);
		
		_TimeIntervalControls kRepeat = new(this, b, "Repeat every:", "5 m|1 h", 1, ("PT1M", "P31D"));
		_TimeIntervalControls kRepeatDuration = new(this, b, "for a duration of:", "|15 m|1 h|1 d", 3,
			validation: o => ((ComboBox)o).Text is "" ? null : _ValidateTimeInterval(o, /*"PT1M"*/kRepeat.Value, "P31D"),
			cOther: kRepeat.c, noRow: true);
		b.R.Add(out KCheckBox cRepeatStop, "Stop all running tasks at end of repetition duration").Margin(22).xBindCheckedEnabled(kRepeat.c);
		if (t.Repetition is { } rep && rep.Interval is string s1) {
			kRepeat.Value = s1;
			if (rep.Duration is string s2) kRepeatDuration.Value = rep.Duration; else kRepeatDuration.cb.SelectedIndex = 0;
			cRepeatStop.IsChecked = rep.StopAtDurationEnd;
		}
		b.End();
		
		_TimeIntervalControls kStop = new(this, b, "Stop task if it runs longer than:", "30 s|5 m|1 h|3 d", 2) { Value = t.ExecutionTimeLimit };
		
		b.R.StartGrid().Columns(0, -1, 0);
		_DateTimeControls dtActivate = new(this, b, 2, "Activate") { Value = _IsDateTrigger() ? null : t.StartBoundary };
		_DateTimeControls dtExpire = new(this, b, 3, "Expire") { Value = t.EndBoundary };
		dtExpire.dt.ValidationDateMustBeAfter = () => (_IsDateTrigger() ? dtStart : dtActivate.c.IsChecked ? dtActivate : null)?.dt.Value;
		page.cExpire = dtExpire.c;
		b.End();
		
		b.R.Add(out KCheckBox cEnabled, "Trigger enabled").Checked(t.Enabled);
		
		b.End();
		//-
		
		b.End();
		
		_apply += page.apply = o => {
			try {
				int i = cbTriggerType.SelectedIndex;
				ap[i].apply?.Invoke(o);
				
				if (kRepeat.c.IsChecked) {
					var rep = t.Repetition;
					rep.Interval = kRepeat.Value;
					rep.Duration = kRepeatDuration.Value;
					rep.StopAtDurationEnd = cRepeatStop.IsChecked;
				} else {
					t.Repetition = null;
				}
				
				t.ExecutionTimeLimit = kStop.Value;
				t.StartBoundary = _IsDateTrigger() ? dtStart.Value : dtActivate.Value;
				t.EndBoundary = dtExpire.Value;
				t.Enabled = cEnabled.IsChecked;
			}
			catch (Exception ex) { dialog.showError("Failed to apply trigger #" + page.Index, ex.ToString()); } //unlikely (all validated)
		};
		
		cbTriggerType.SelectionChanged += (_, e) => {
			int i = cbTriggerType.SelectedIndex;
			
			if (ap[i].panel == null) ab[i](i);
			cc.Content = ap[i].panel;
			
			bool isDateTrigger = i <= 4;
			dtStart.Collapse(!isDateTrigger);
			dtActivate.Collapse(isDateTrigger);
			kDelay.c.Content = isDateTrigger ? "Delay task for up to (random delay):" : "Delay task for:";
			kDelay.Collapse(_ttMap[i] == TT.IDLE);
		};
		
		cbTriggerType.SelectedIndex = Array.IndexOf(_ttMap, tt);
		
		_AddPage(page.ToString(), page);
		
		if (select) {
			_lbPages.SelectedItem = page.li;
			_lbPages.ScrollIntoView(page.li);
		}
		
		//note: gets the saved trigger type. It is different if changed and still not applied.
		bool _IsDateTrigger() => tt is >= TT.TIME and <= TT.MONTHLYDOW;
	}
	
	void _NewTrigger(bool createPage) {
		var trigger = _td.Triggers.Create(TT.TIME);
		var dt = DateTime.Now;
		trigger.StartBoundary = dt.ToString("s");
		if (createPage) _CreateTriggerPage(trigger, true);
	}
	
	void _DeleteTrigger() {
		if (_lbPages.SelectedItem is ListBoxItem { Tag: _TriggerPage page } li) {
			if (0 != _td.Triggers.Remove(_lbPages.SelectedIndex)) return;
			_lbPages.Items.Remove(li);
			_apply -= page.apply;
		}
	}
	
	class _TriggerPage : _Page {
		public api.ITrigger trigger;
		public Action<WBButtonClickArgs> apply;
		public KCheckBox cExpire;
		
		public override string ToString() {
			return trigger.FormatTriggerString();
		}
	}
	
	static string _DecodeTimeInterval(string s, string returnIf0 = null) {
		if (s.NE()) return null;
		if (s[0] != 'P') return s;
		try {
			var t = XmlConvert.ToTimeSpan(s);
			if (t.Ticks == 0) return returnIf0;
			StringBuilder b = new();
			_Append(t.Days, 'd');
			_Append(t.Hours, 'h');
			_Append(t.Minutes, 'm');
			_Append(t.Seconds, 's');
			return b.ToString();
			
			void _Append(int n, char c) {
				if (n > 0) {
					if (b.Length > 0) b.Append(" ");
					b.Append(n).Append(' ').Append(c);
				}
			}
		}
		catch { return s; }
	}
	
	static string _EncodeTimeInterval(string s) {
		if (s.NE() || !s.RxMatch(@"(?i)^ *P?(?:(\d+) *d(?:ays?)?,?)? *T?(?:(\d+) *h(?:ours?)?,?)? *(?:(\d+) *m(?:in(?:utes?)?)?,?)? *(?:(\d+) *s(?:ec(?:onds?)?)?)? *$", out var m)) return null;
		bool dExists = m[1].Exists, hmsExists = m[2].Exists || m[3].Exists || m[4].Exists;
		if (!(dExists || hmsExists)) return null;
		var b = new StringBuilder("P");
		if (dExists) b.Append(m[1].Value).Append('D');
		if (hmsExists) {
			b.Append('T');
			if (m[2].Exists) b.Append(m[2].Value).Append('H');
			if (m[3].Exists) b.Append(m[3].Value).Append('M');
			if (m[4].Exists) b.Append(m[4].Value).Append('S');
		}
		return b.ToString();
	}
	
	static string _ValidateTimeInterval(FrameworkElement e, string min = null, string max = null, Func<string> also = null) {
		var c = (ComboBox)e;
		if (c.IsEnabled && c.Visibility == Visibility.Visible) {
			var s = c.Text.Trim();
			if (s.NE()) return "Empty";
			if (_EncodeTimeInterval(s) is not string k) return "Invalid duration format";
			
			try {
				var t = XmlConvert.ToTimeSpan(k);
				//print.it(k, t);
				if (min != null && t < XmlConvert.ToTimeSpan(min)) return "Duration min " + _DecodeTimeInterval(min);
				max ??= "P24855D"; //int.MaxValue seconds
				if (t > XmlConvert.ToTimeSpan(max)) return "Duration max " + _DecodeTimeInterval(max);
				
				if (also is { } vp) return vp();
			}
			catch { return "Invalid duration format"; }
		}
		return null;
	}
	
	static string _ValidateNumber(FrameworkElement e, int min, int max, bool allowEmpty = false) {
		var c = (TextBox)e;
		if (c.IsEnabled && c.Visibility == Visibility.Visible) {
			var s = c.Text.Trim();
			if (s.NE()) return allowEmpty ? null : "Empty";
			if (!s.ToInt(out long r, 0, out int end, STIFlags.NoHex) || end < s.Length) return "Invalid number format";
			if (r < min) return "Min " + min;
			if (r > max) return "Max " + max;
		}
		return null;
	}
	
	static string _ValidateBits(FrameworkElement e) {
		var c = (KCheckDropdownBox)e;
		if (c.IsEnabled && c.Visibility == Visibility.Visible) {
			if (c.Checked == 0) return "Empty";
		}
		return null;
	}
	
	void _ValidationLinkClick(FrameworkElement e) {
		if (!e.IsVisible) {
			var page = e.FindVisualAncestor<_Page>(false, null, false) as _Page;
			_lbPages.SelectedIndex = page.Index;
		}
	}
	
	class _TimeIntervalControls {
		public ComboBox cb;
		public KCheckBox c;
		Label _label;
		KCheckBox _cOther;
		
		public _TimeIntervalControls(DSchedule dialog, wpfBuilder b, string label, string cbItems, int cbIndex,
			(string min, string max) valid = default, Func<FrameworkElement, string> validation = null, Func<FrameworkElement, string> checkboxValidation = null,
			KCheckBox cOther = null, bool noRow = false) {
			
			validation ??= o => _ValidateTimeInterval(o, valid.min, valid.max);
			
			if (!noRow) b.Row(0);
			if ((_cOther = cOther) == null) {
				b.Add(out c, label);
				if (checkboxValidation != null) b.Validation(checkboxValidation, dialog._ValidationLinkClick);
			} else b.Add(out _label, label);
			
			b.Add(out cb).Editable().LabeledBy()
				.Items(cbItems).Select(cbIndex)
				.Validation(validation, dialog._ValidationLinkClick);
			b.xBindCheckedEnabled(c ?? _cOther, true);
		}
		
		public string Value {
			get => (c ?? _cOther).IsChecked ? _EncodeTimeInterval(cb.Text) : null;
			set {
				if (_DecodeTimeInterval(value) is string s) {
					cb.Text = s;
					if (c != null) c.IsChecked = true;
				}
			}
		}
		
		public string Value0 {
			set {
				if (_DecodeTimeInterval(value, "0 s") is string s) {
					cb.Text = s;
					if (c != null) c.IsChecked = true;
				}
			}
		}
		
		public void Collapse(bool collapse) {
			_label?.Collapse_(collapse);
			c?.Collapse_(collapse);
			cb.Collapse_(collapse);
		}
	}
	
	class _DateTimeControls {
		public KDateTime dt;
		public KCheckBox c;
		Label _label;
		KCheckBox _cSync;
		bool _expire;
		
		/// <param name="id">1 Start, 2 Activate, 3 Expire.</param>
		public _DateTimeControls(DSchedule dialog, wpfBuilder b, int id, string label, Func<FrameworkElement, string> validation = null) {
			_expire = id == 3;
			bool checkbox = id is 2 or 3;
			
			b.Row(0);
			if (checkbox) b.Add(out c, label); else b.Add(out _label, label);
			
			b.Add(out dt).LabeledBy(bindVisibility: false)
				.Validation(validation ?? KDateTime.Validation, dialog._ValidationLinkClick);
			if (checkbox) b.xBindCheckedEnabled(c);
			
			b.Add(out _cSync, "Sync across time zones");
			if (checkbox) b.xBindCheckedEnabled(c);
		}
		
		/// <summary>
		/// Note: getter throws if the date control text is invalid. Must be validated.
		/// </summary>
		public string Value {
			get => c?.IsChecked == false ? null : XmlConvert.ToString(dt.Value.Value, _cSync.IsChecked ? XmlDateTimeSerializationMode.Local : XmlDateTimeSerializationMode.Unspecified);
			set {
				if (!value.NE()) {
					try {
						dt.Value = XmlConvert.ToDateTime(value, XmlDateTimeSerializationMode.Local);
						if (c != null) c.IsChecked = true;
						_cSync.IsChecked = value.RxIsMatch(@"(?:Z|[+\-][\d:]+)$");
						return;
					}
					catch { }
				}
				var d = DateTime.Now;
				if (_expire) d = d.AddYears(1);
				dt.Value = d;
			}
		}
		
		public void Collapse(bool collapse) {
			_label?.Collapse_(collapse);
			c?.Collapse_(collapse);
			dt.Collapse_(collapse);
			_cSync.Collapse_(collapse);
		}
	}
}
