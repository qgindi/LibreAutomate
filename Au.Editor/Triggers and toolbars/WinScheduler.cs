using System.Security.Principal;
using System.Xml.Linq;
using api = WinSchedulerApi;
using TT = WinSchedulerApi.TASK_TRIGGER_TYPE2;

static class WinScheduler {
	static string _SidCurrentUser => WindowsIdentity.GetCurrent().User.ToString();
	//static string _SddlCurrentUserReadExecute => "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;" + _SidCurrentUser + ")";
	static string _SddlCurrentUserReadExecute => "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;" + _SidCurrentUser + ")";
	static string c_sddlEveryoneReadExecute = "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;WD)";
	
	internal static bool Connect(out api.ITaskService ts) {
		try {
			ts = new api.TaskScheduler() as api.ITaskService;
			return 0 == ts.Connect();
		}
		catch { ts = null; return false; }
	}
	
	/// <summary>
	/// Creates or updates a trigerless task that executes a program as system, admin or user.
	/// Creates the folder (and ancestors) if does not exist.
	/// This process must be admin.
	/// You can use <see cref="RunTask"/> to run the task.
	/// </summary>
	/// <param name="IL">Can be System, High or Medium. If System, runs in SYSTEM account. Else in creator's account.</param>
	/// <param name="programFile">Full path of an exe file. This function does not normalize it.</param>
	/// <param name="args">Command line arguments. Can contain literal substrings $(Arg0), $(Arg1), ..., $(Arg32) that will be replaced by <see cref="RunTask"/>.</param>
	/// <returns>HRESULT.</returns>
	/// <inheritdoc cref="RunTask" path="/param"/>
	public static int CreateTaskWithoutTriggers(string taskFolder, string taskName, UacIL IL, string programFile, string args = null, string author = "Au", api.TASK_CREATION creation = api.TASK_CREATION.TASK_CREATE) {
		var xml = CreateXmlForNewTask(IL, programFile, args, author);
		return CreateTaskFromXml(taskFolder, taskName, xml, IL, creation);
	}
	
	/// <summary>
	/// Creates or updates a task using XML.
	/// Creates the folder (and ancestors) if does not exist.
	/// This process must be admin.
	/// You can use <see cref="RunTask"/> to run the task.
	/// </summary>
	/// <param name="IL">Can be System, High or Medium. If System, runs in SYSTEM account. Else in creator's account.</param>
	/// <returns>HRESULT.</returns>
	/// <inheritdoc cref="RunTask" path="/param"/>
	public static int CreateTaskFromXml(string taskFolder, string taskName, string xml, UacIL IL, api.TASK_CREATION creation = api.TASK_CREATION.TASK_CREATE) {
		return _CreateTask(taskFolder, taskName, xml, IL, creation);
	}
	
	/// <summary>
	/// Creates or updates a task using XML.
	/// Creates the folder (and ancestors) if does not exist.
	/// This process must be admin.
	/// You can use <see cref="RunTask"/> to run the task.
	/// </summary>
	/// <param name="IL">Can be System, High or Medium. If System, runs in SYSTEM account. Else in creator's account.</param>
	/// <returns>HRESULT.</returns>
	/// <inheritdoc cref="RunTask" path="/param"/>
	internal static int CreateTaskFromDefinition(string taskFolder, string taskName, api.ITaskDefinition td, UacIL IL, api.TASK_CREATION creation = api.TASK_CREATION.TASK_CREATE) {
		return _CreateTask(taskFolder, taskName, td, IL, creation);
	}
	
	static int _CreateTask(string taskFolder, string taskName, object tdOrXml, UacIL IL, api.TASK_CREATION creation = api.TASK_CREATION.TASK_CREATE) {
		if (!(IL is UacIL.High or UacIL.Medium or UacIL.System)) throw new ArgumentException();
		if (!Connect(out var ts)) return Api.E_FAIL;
		if (0 != ts.GetFolder(taskFolder, out var tf)) {
			int hr = ts.GetFolder(null, out tf);
			if (hr == 0) hr = tf.CreateFolder(taskFolder, c_sddlEveryoneReadExecute, out tf);
			if (hr != 0) return hr;
		} else if ((creation & api.TASK_CREATION.TASK_CREATE_OR_UPDATE) == api.TASK_CREATION.TASK_CREATE) {
			tf.DeleteTask(taskName, 0); //delete if exists. Note: TASK_CREATE_OR_UPDATE does not update task file's security.
		}
		var logonType = IL == UacIL.System ? api.TASK_LOGON_TYPE.TASK_LOGON_SERVICE_ACCOUNT : api.TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN;
		var sddl = IL == UacIL.System ? c_sddlEveryoneReadExecute : _SddlCurrentUserReadExecute;
		if (tdOrXml is api.ITaskDefinition td) {
			return tf.RegisterTaskDefinition(taskName, td, creation, null, null, logonType, sddl, out _);
		} else {
			return tf.RegisterTask(taskName, (string)tdOrXml, creation, null, null, logonType, sddl, out _);
		}
		
		//note: cannot create a task that runs only in current interactive session, regardless of user.
		//	Tried INTERACTIVE: userId "S-1-5-4", logonType TASK_LOGON_GROUP. But then runs in all logged in sessions.
	}
	
	/// <summary>
	/// Creates XML of a new trigerless task.
	/// </summary>
	/// <param name="IL">Can be System, High or Medium. If System, runs in SYSTEM account. Else in creator's account.</param>
	/// <param name="programFile">Full path of an exe file. This function does not normalize it.</param>
	/// <param name="args">Command line arguments. Can contain literal substrings $(Arg0), $(Arg1), ..., $(Arg32) that will be replaced by <see cref="RunTask"/>.</param>
	/// <param name="author"></param>
	public static string CreateXmlForNewTask(UacIL IL, string programFile, string args = null, string author = "Au") {
		if (!(IL is UacIL.High or UacIL.Medium or UacIL.System)) throw new ArgumentException();
		var userId = IL == UacIL.System ? "<UserId>S-1-5-18</UserId>\r\n" : null;
		var runLevel = IL switch { UacIL.System => null, UacIL.High => "<RunLevel>HighestAvailable</RunLevel>", _ => "<RunLevel>LeastPrivilege</RunLevel>" };
		return $"""
<?xml version='1.0' encoding='UTF-16'?>
<Task version='1.{(osVersion.minWin10 ? "4" : "3")}' xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'>

<RegistrationInfo>
<Author>{author}</Author>
</RegistrationInfo>

<Principals>
<Principal id='Author'>
{userId}{runLevel}
</Principal>
</Principals>

<Settings>
<MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
<Priority>5</Priority>
</Settings>

<Actions Context='Author'>
<Exec>
<Command>{programFile}</Command>
<Arguments>{args}</Arguments>
</Exec>
</Actions>

</Task>
""";
	}
	
	/// <summary>
	/// Runs task. Does not wait.
	/// </summary>
	/// <returns>Process id. Returns 0 if failed, eg if the task does not exist or is disabled.</returns>
	/// <param name="taskFolder">Can be like <c>@"\Folder"</c> or <c>@"\A\B"</c> or <c>"Folder"</c> or <c>@"\"</c> or <c>""</c> or null.</param>
	/// <param name="taskName">Can be like <c>"Name"</c> or <c>@"\Folder\Name"</c> or <c>@"Folder\Name"</c>.</param>
	/// <param name="pathMustBe">If not null, don't run if the task action's path does not match this.</param>
	/// <param name="joinArgs">Join args into single arg for $(Arg0).</param>
	/// <param name="args">Replacement values for substrings $(Arg0), $(Arg1), ..., $(Arg32) in 'create task' args. See <ms>IRegisteredTask.Run</ms>.</param>
	public static (int processId, RResult result) RunTask(string taskFolder, string taskName, string pathMustBe, bool joinArgs, params string[] args) {
		if (!Connect(out var ts)) return (0, RResult.CantConnect);
		if (0 != ts.GetFolder(taskFolder, out var tf) || 0 != tf.GetTask(taskName, out var t)) return (0, RResult.TaskNotFound);
		
		if (!t.Enabled) return (0, RResult.TaskDisabled);
		
		if (pathMustBe != null) {
			if (t.Definition.Actions.GetExecAction(1) is not { } action) return (0, RResult.BadTask);
			if (!filesystem.more.isSameFile(pathMustBe, action.Path)) return (0, RResult.BadPath);
		}
		
		object a; if (args.NE_()) a = null; else if (joinArgs) a = StringUtil.CommandLineFromArray(args); else a = args;
		if (0 != t.Run(a, out var rt)) return (0, RResult.RunFailed);
		rt.get_EnginePID(out int pid);
		return (pid, RResult.Success);
	}
	
	public enum RResult { None, Success, CantConnect, TaskNotFound, TaskDisabled, BadTask, BadPath, RunFailed, ArgN }
	
	/// <summary>
	/// Returns true if the task exists.
	/// </summary>
	/// <inheritdoc cref="RunTask" path="/param"/>
	public static bool TaskExists(string taskFolder, string taskName) {
		if (!Connect(out var ts)) return false;
		return 0 == ts.GetFolder(taskFolder, out var tf) && 0 == tf.GetTask(taskName, out _);
	}
	
	/// <summary>
	/// Deletes task if exists.
	/// This process must be admin.
	/// </summary>
	/// <inheritdoc cref="RunTask" path="/param"/>
	public static void DeleteTask(string taskFolder, string taskName) {
		if (!Connect(out var ts)) return;
		if (0 == ts.GetFolder(taskFolder, out var tf)) tf.DeleteTask(taskName, 0);
	}
	
	public static string EditorPath { get; set; } = process.thisExePath;
	
	public record struct FSTResult(api.IRegisteredTask rt, api.ITaskDefinition td, api.IExecAction action, string scriptArgs);
	
	public static bool FindScriptTask(string fileName, string itemPath, out FSTResult r) {
		r = default;
		if (Connect(out var ts)) {
			string user = Environment.UserName, thisExePath = EditorPath;
			if (0 == ts.GetFolder(@"Au\" + user, out var tf) && 0 == tf.GetTasks(api.TASK_ENUM_HIDDEN, out var tasks) && tasks.Count is int nTasks) {
				for (int i = 1; i <= nTasks; i++) {
					if (0 == tasks.get_Item(i, out var rtask) && rtask.Definition is { } td) {
						if (td.Actions is { Count: int nActions } actions) {
							for (int j = 1; j <= nActions; j++) {
								if (actions.GetExecAction(j) is { Arguments: var s } action && !s.NE()) {
									int start = 0, end;
									if (s[0] == '"') {
										end = s.IndexOf('"', start = 1);
									} else {
										end = s.IndexOf(' '); if (end < 0) end = s.Length;
									}
									if (s.At_(start) is '>' or '*') start++;
									if (end > start) {
										Range r1 = start..end;
										if (s.Eq(r1, fileName, true) || s.Eq(r1, itemPath, true)) {
											if (action.Path is string sp && filesystem.more.isSameFile(thisExePath, sp)) {
												string scriptArgs = null;
												if (end < s.Length) {
													if (s[end] == '"') end++;
													while (end < s.Length && s[end] == ' ') end++;
													if (end < s.Length) scriptArgs = s[end..];
												}
												r = new(rtask, td, action, scriptArgs);
												return true;
											}
										}
									}
								}
							}
						}
					}
				}
			}
		}
		return false;
	}
	
	/// <summary>
	/// Returns trigger strings of all found scheduler tasks that are set to run script <i>fn</i>.
	/// For a multi-trigger task returns multiple items.
	/// </summary>
	public static async Task<List<(string task, string trigger)>> GetScriptTriggersAsync(FileNode fn) {
		string name = fn.Name, itemPath = fn.ItemPath;
		List<(string task, string trigger)> ar = null;
		await Task.Run(_Work);
		return ar;
		
		void _Work() {
			StringBuilder b = null;
			if (FindScriptTask(name, itemPath, out var r)) {
				string taskName = r.rt.Name;
				foreach (var t in r.td.EnumTriggers()) {
					(b ??= new()).Clear();
					t.FormatTriggerString(b);
					(ar ??= new()).Add((taskName, b.ToString()));
				}
			}
		}
	}
	
	/// <summary>
	/// Opens Task Scheduler UI for task editing.
	/// This process must be admin.
	/// Starts task and returns.
	/// </summary>
	/// <param name="taskFolder">Task folder, like <c>@"A\B"</c>.</param>
	/// <param name="taskName">Task name (without path). If null, just opens the Task Scheduler folder.</param>
	public static void EditTask(string taskFolder, string taskName) {
		Task.Run(() => {
			//run Task Scheduler UI
			var w = wnd.runAndFind(
				() => run.it(folders.System + "mmc.exe", folders.System + "taskschd.msc", flags: RFlags.InheritAdmin),
				60, cn: "MMCMainFrame");
			
			//expand folder "Task Scheduler Library"
			var tv = w.Child(id: 12785);
			tv.Focus();
			var htvi = wait.until(5, () => tv.Send(api.TVM_GETNEXTITEM, api.TVGN_CHILD, tv.Send(api.TVM_GETNEXTITEM)));
			wait.until(10, () => 0 != tv.Send(api.TVM_EXPAND, api.TVE_EXPAND, htvi)); //note: don't wait for TVM_GETITEMSTATE TVIS_EXPANDED
			
			//open the specified folder
			var e = elm.fromWindow(tv, EObjid.CLIENT);
			e.Item = 2;
			taskFolder = taskFolder.Trim('\\').Replace('\\', '|');
			e.Expand(taskFolder, waitS: 10, notLast: true).Select();
			
			if (taskName == null) return;
			
			//open Properties dialog of the specified task
			var lv = w.Child(30, "***wfName listViewMain", "*.SysListView32.*"); //the slowest part, ~1 s
			lv.Elm["LISTITEM", taskName, flags: EFFlags.ClientArea | EFFlags.HiddenToo].Find(10).Select();
			lv.Post(Api.WM_KEYDOWN, (int)KKey.Enter);
			
			//wait for task Properties dialog and select tab "Trigger"
			var wp = wnd.find(10, taskName + "*", "*.Window.*", WOwner.Process(w.ProcessId));
			wp.Activate();
			var tc = wp.Child(5, cn: "*.SysTabControl32.*");
			tc.Send(api.TCM_SETCURFOCUS, 1);
			
			//never mind: the script may fail at any step, although on my computers never failed.
			//	Let it do as much as it can. It's better than nothing.
			//	Task.Run silently handles exceptions.
		});
	}
}

static class ExtScheduler {
	
	public static IEnumerable<api.ITrigger> EnumTriggers(this api.ITaskDefinition t) {
		if (t.Triggers is { Count: int nTriggers } triggers && nTriggers > 0) {
			for (int k = 1; k <= nTriggers; k++) {
				if (0 == triggers.get_Item(k, out var r)) yield return r;
			}
		}
	}
	
	public static string FormatTriggerString(this api.ITrigger t) {
		StringBuilder sb = new();
		FormatTriggerString(t, sb);
		return sb.ToString();
	}
	
	public static void FormatTriggerString(this api.ITrigger t, StringBuilder b) {
		if (!t.Enabled) b.Append("(disabled) ");
		var ttype = t.Type;
		if ((int)ttype is >= 1 and <= 5) {
			string sStart = t.StartBoundary;
			var (sStartDate, sStartTime) = SplitDateTime(sStart);
			b.Append($"At {sStartTime} ");
			
			switch (ttype) {
			case TT.TIME:
				b.Append($"on {sStartDate}");
				break;
			case TT.DAILY when t is api.IDailyTrigger g:
				short daysInterval = g.DaysInterval;
				b.Append("every ").Append(daysInterval == 1 ? "day" : $"{daysInterval} day");
				break;
			case TT.WEEKLY when t is api.IWeeklyTrigger g:
				ushort daysOfWeek1 = g.DaysOfWeek;
				short weeksInterval = g.WeeksInterval;
				if ((daysOfWeek1 & 0x7f) == 0x7f) b.Append("every day of the week,"); else { b.Append("on "); _DaysOfWeek(daysOfWeek1); }
				if (weeksInterval == 1) b.Append(" every week"); else b.AppendFormat(" every {0} weeks", weeksInterval);
				break;
			case TT.MONTHLY when t is api.IMonthlyTrigger g:
				b.Append("on day ");
				_DaysOfMonth(g.DaysOfMonth, g.RunOnLastDayOfMonth);
				b.Append(" of ");
				ushort monthsOfYear1 = g.MonthsOfYear;
				if ((monthsOfYear1 & 0xfff) == 0xfff) b.Append("every month"); else _MonthsOfYear(monthsOfYear1);
				break;
			case TT.MONTHLYDOW when t is api.IMonthlyDOWTrigger g:
				ushort weeksOfMonth = g.WeeksOfMonth;
				if (g.RunOnLastWeekOfMonth) weeksOfMonth |= 0x10;
				if ((weeksOfMonth & 0x1f) == 0x1f) b.Append("every"); else { b.Append("on "); _WeeksOfMonth(weeksOfMonth); }
				b.Append(" ");
				_DaysOfWeek(g.DaysOfWeek);
				ushort monthsOfYear2 = g.MonthsOfYear;
				if ((monthsOfYear2 & 0xfff) == 0xfff) b.Append(" every month"); else { b.Append(" each "); _MonthsOfYear(monthsOfYear2); }
				break;
			}
			if ((int)ttype >= 3) b.Append(", starting ").Append(sStartDate);
		} else {
			switch (ttype) {
			case TT.BOOT:
				b.Append("At system startup");
				break;
			case TT.EVENT:
				if (t is api.IEventTrigger et) {
					var v = et.GetQuery();
					b.Append("On event: ").Append(v.log == null ? v.query : $"log={v.log}, source={v.source}, id={v.id}");
					if (v.level != 0) b.Append(", level=").Append(v.level);
				}
				break;
			case TT.IDLE:
				b.Append("When computer is idle");
				break;
			case TT.LOGON when t is api.ILogonTrigger g:
				b.Append("At log on of ").Append(g.UserId ?? "any user");
				break;
			case TT.REGISTRATION:
				b.Append("When the task is created or modified");
				break;
			case TT.SESSION when t is api.ISessionStateChangeTrigger g:
				b.Append("On ");
				b.AppendFormat(g.StateChange switch {
					api.TASK_SESSION_STATE_CHANGE_TYPE.TASK_CONSOLE_CONNECT => "local connection to {0} session",
					api.TASK_SESSION_STATE_CHANGE_TYPE.TASK_CONSOLE_DISCONNECT => "local disconnect from {0} session",
					api.TASK_SESSION_STATE_CHANGE_TYPE.TASK_REMOTE_CONNECT => "remote connection to {0} session",
					api.TASK_SESSION_STATE_CHANGE_TYPE.TASK_REMOTE_DISCONNECT => "remote disconnect from {0} session",
					api.TASK_SESSION_STATE_CHANGE_TYPE.TASK_SESSION_LOCK => "workstation lock of {0}",
					api.TASK_SESSION_STATE_CHANGE_TYPE.TASK_SESSION_UNLOCK => "workstation unlock of {0}",
					_ => ""
				}, g.UserId ?? "any user");
				break;
			default:
				b.Append("Custom trigger");
				break;
			}
		}
		b.Append('.');
		
		if (t.Repetition is { } rep && rep.Interval is string repInterval) {
			b.Append(" Then repeat every "); _Time(repInterval);
			if (rep.Duration is string repDuration) { b.Append(" for a duration of "); _Time(repDuration); }
			b.Append('.');
		}
		
		if (t.EndBoundary is string sEnd) {
			var (sd, st) = SplitDateTime(sEnd);
			b.Append($" Expires {sd} {st}.");
		}
		
		static (string date, string time) SplitDateTime(string s) {
			if (s?.IndexOf('T') is int i && i > 0) {
				var sd = s[..i++];
				int j = s.FindAny("+-", i..); if (j < 0) j = s.Length;
				return (sd, s[i..j]);
			} else return default;
		}
		
		void _Time(string s) {
			if (s.RxMatch(@"^P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?)?$", out var m)) {
				for (int i = 1, n = 0; i <= 3; i++) {
					if (m[i].Value is string k) {
						if (n++ > 0) b.Append(' ');
						b.Append(k).Append(' ');
						b.Append(s[m[i].End] switch { 'D' => "day", 'H' => "hour", _ => "minute" });
						if (k != "1") b.Append('s');
					}
				}
			} else b.Append(s);
		}
		
		void _DaysOfWeek(uint days) {
			string sep = null;
			for (int i = 0; i < 7; i++) {
				if ((days >> i & 1) != 0) {
					b.Append(sep); sep ??= "|";
					b.Append(i switch { 0 => "Sunday", 1 => "Monday", 2 => "Tuesday", 3 => "Wednesday", 4 => "Thursday", 5 => "Friday", 6 => "Saturday", _ => null });
				}
			}
		}
		
		void _DaysOfMonth(uint days, bool lastDay) {
			string sep = null;
			for (int i = 0; i < 31; i++) {
				if ((days >> i & 1) != 0) {
					b.Append(sep); sep ??= "|";
					b.Append(i + 1);
				}
			}
			if (lastDay) b.Append(sep).Append("last");
		}
		
		void _MonthsOfYear(uint months) {
			string sep = null;
			for (int i = 0; i < 12; i++) {
				if ((months >> i & 1) != 0) {
					b.Append(sep); sep ??= "|";
					b.Append((i + 1) switch { 1 => "January", 2 => "February", 3 => "March", 4 => "April", 5 => "May", 6 => "June", 7 => "July", 8 => "August", 9 => "September", 10 => "October", 11 => "November", 12 => "December", _ => null });
				}
			}
		}
		
		void _WeeksOfMonth(uint weeks) {
			string sep = null;
			for (int i = 0; i < 5; i++) {
				if ((weeks >> i & 1) != 0) {
					b.Append(sep); sep ??= "|";
					b.Append((i + 1) switch { 1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "last", _ => null });
				}
			}
		}
	}
	
	/// <summary>
	/// Calls <b>get_Item</b> and casts to <b>IExecAction</b>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="index">1-based.</param>
	/// <returns>null if fails.</returns>
	public static api.IExecAction GetExecAction(this api.IActionCollection t, int index) {
		if (0 == t.get_Item(1, out var v)) return v as api.IExecAction;
		return null;
	}
	
	/// <summary>
	/// Calls <b>Subscription</b> and parses the query.
	/// If it's a standard simple query, sets <b>log</b>, <b>source</b> and <b>id</b>. Else they will be null. Always sets <b>query</b>.
	/// </summary>
	public static (string query, string log, string source, string id, int level) GetQuery(this api.IEventTrigger t) {
		var s = t.Subscription;
		try {
			var x = XElement.Parse(s);
			if (x is { Name.LocalName: "QueryList" }
				&& x.Element("Query") is { NextNode: null } xq && xq.Attr("Path") is { } log
				&& xq.Element("Select") is { NextNode: null, Value: ['*', ..] v } xs && xs.Attr("Path") == log) {
				
				string source = null, id = null, level = null;
				bool ok = false;
				if (v == "*") ok = true;
				else if (v.Starts("*[System[") && v.Ends("]]")) {
					foreach (var k in v[9..^2].Split(" and ")) {
						if (k.Like("Provider[@Name='*']")) source = k[16..^2];
						else if (k.Starts("EventID=")) id = k[8..];
						else if (k.Like("(EventID=*)") && !k.Contains(' ')) id = k[9..^1];
						else if (k.Starts("Level=")) level = k[6..];
						else if (k.Like("(Level=*)") && !k.Contains(' ')) level = k[7..^1];
						else { ok = false; break; }
						ok = true;
					}
					if (ok) return (s, log, source, id, level?.ToInt() ?? 0);
				}
			}
		}
		catch { }
		return (s, null, null, null, 0);
	}
	
	//not used
	//public static void SetPrincipal(this api.ITaskDefinition t, UacIL IL, string author = "Au") {
	//	if (!(IL is UacIL.High or UacIL.Medium or UacIL.System)) throw new ArgumentException();
	//	t.get_Principal(out var p);
	//	p.put_Id("Author");
	//	p.put_LogonType(api.TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN);
	//	if (IL == UacIL.System) p.put_UserId("<UserId>S-1-5-18</UserId>\r\n");
	//	else p.put_RunLevel(IL == UacIL.Medium ? api.TASK_RUNLEVEL_TYPE.TASK_RUNLEVEL_LUA : api.TASK_RUNLEVEL_TYPE.TASK_RUNLEVEL_HIGHEST);
	//}
}

#pragma warning disable 649, 169 //field never assigned/used
unsafe class WinSchedulerApi : NativeApi {
	[ComImport, Guid("0f87369f-a4e5-4cfc-bd3e-73e6154572dd"), ClassInterface(ClassInterfaceType.None)]
	internal class TaskScheduler { }
	
	[ComImport, Guid("2faba4c7-4da9-4013-9697-20cc3fd40f85")] //dual
	internal interface ITaskService {
		[PreserveSig] int GetFolder(string path, out ITaskFolder ppFolder);
		[PreserveSig] int GetRunningTasks(int flags, out IRunningTaskCollection ppRunningTasks);
		[PreserveSig] int NewTask(uint flags, out ITaskDefinition ppDefinition);
		[PreserveSig] int Connect(object serverName = null, object user = null, object domain = null, object password = null);
		bool Connected { get; }
		string TargetServer { get; }
		string ConnectedUser { get; }
		string ConnectedDomain { get; }
		uint HighestVersion { get; }
	}
	
	[ComImport, Guid("8cfac062-a080-4c15-9a88-aa7c2af80dfc")] //dual
	internal interface ITaskFolder {
		string Name { get; }
		string Path { get; }
		[PreserveSig] int GetFolder(string path, out ITaskFolder ppFolder);
		[PreserveSig] int GetFolders(int flags, out ITaskFolderCollection ppFolders);
		[PreserveSig] int CreateFolder(string subFolderName, object sddl, out ITaskFolder ppFolder);
		[PreserveSig] int DeleteFolder(string subFolderName, int flags);
		[PreserveSig] int GetTask(string path, out IRegisteredTask ppTask);
		[PreserveSig] int GetTasks(int flags, out IRegisteredTaskCollection ppTasks);
		[PreserveSig] int DeleteTask(string name, int flags);
		[PreserveSig] int RegisterTask(string path, string xmlText, TASK_CREATION flags, object userId, object password, TASK_LOGON_TYPE logonType, object sddl, out IRegisteredTask ppTask);
		[PreserveSig] int RegisterTaskDefinition(string path, ITaskDefinition pDefinition, TASK_CREATION flags, object userId, object password, TASK_LOGON_TYPE logonType, object sddl, out IRegisteredTask ppTask);
		[PreserveSig] int GetSecurityDescriptor(int securityInformation, out string pSddl);
		[PreserveSig] int SetSecurityDescriptor(string sddl, int flags);
	}
	
	[Flags]
	internal enum TASK_CREATION : uint {
		TASK_VALIDATE_ONLY = 0x1,
		TASK_CREATE,
		TASK_UPDATE = 0x4,
		TASK_CREATE_OR_UPDATE = 0x6,
		TASK_DISABLE = 0x8,
		TASK_DONT_ADD_PRINCIPAL_ACE = 0x10,
		TASK_IGNORE_REGISTRATION_TRIGGERS = 0x20
	}
	
	[ComImport, Guid("86627eb4-42a7-41e4-a4d9-ac33a72f2d52")] //dual
	internal interface IRegisteredTaskCollection {
		int Count { get; }
		[PreserveSig] int get_Item(object index, out IRegisteredTask ppRegisteredTask);
	}
	
	[ComImport, Guid("9c86f320-dee3-4dd1-b972-a303f26b061e")] //dual
	internal interface IRegisteredTask {
		string Name { get; }
		string Path { get; }
		TASK_STATE State { get; }
		bool Enabled { get; set; }
		[PreserveSig] int Run(object @params, out IRunningTask ppRunningTask);
		[PreserveSig] int RunEx(object @params, int flags, int sessionID, string user, out IRunningTask ppRunningTask);
		[PreserveSig] int GetInstances(int flags, out IRunningTaskCollection ppRunningTasks);
		DateTime LastRunTime { get; }
		int LastTaskResult { get; }
		int NumberOfMissedRuns { get; }
		DateTime NextRunTime { get; }
		ITaskDefinition Definition { get; }
		string Xml { get; }
		[PreserveSig] int GetSecurityDescriptor(int securityInformation, out string pSddl);
		[PreserveSig] int SetSecurityDescriptor(string sddl, int flags);
		[PreserveSig] int Stop(int flags);
		[PreserveSig] int GetRunTimes(in SYSTEMTIME pstStart, in SYSTEMTIME pstEnd, ref uint pCount, out SYSTEMTIME* pRunTimes);
	}
	
	[ComImport, Guid("79184a66-8664-423f-97f1-637356a5d812")] //dual
	internal interface ITaskFolderCollection {
		int Count { get; }
		[PreserveSig] int get_Item(object index, out ITaskFolder ppFolder);
	}
	
	internal struct SYSTEMTIME {
		public ushort wYear;
		public ushort wMonth;
		public ushort wDayOfWeek;
		public ushort wDay;
		public ushort wHour;
		public ushort wMinute;
		public ushort wSecond;
		public ushort wMilliseconds;
	}
	
	[ComImport, Guid("f5bc8fc5-536d-4f77-b852-fbc1356fdeb6")] //dual
	internal interface ITaskDefinition {
		IRegistrationInfo RegistrationInfo { get; set; }
		ITriggerCollection Triggers { get; set; }
		ITaskSettings Settings { get; set; }
		string Data { get; set; }
		IPrincipal Principal { get; set; }
		IActionCollection Actions { get; set; }
		string XmlText { get; set; }
	}
	
	[ComImport, Guid("6a67614b-6828-4fec-aa54-6d52e8f1f2db")] //dual
	internal interface IRunningTaskCollection {
		int Count { get; }
		[PreserveSig] int get_Item(object index, out IRunningTask ppRunningTask);
	}
	
	[ComImport, Guid("653758fb-7b9a-4f1e-a471-beeb8e9b834e")] //dual
	internal interface IRunningTask {
		[PreserveSig] int get_Name(out string pName);
		[PreserveSig] int get_InstanceGuid(out string pGuid);
		[PreserveSig] int get_Path(out string pPath);
		[PreserveSig] int get_State(out TASK_STATE pState);
		[PreserveSig] int get_CurrentAction(out string pName);
		[PreserveSig] int Stop();
		[PreserveSig] int Refresh();
		[PreserveSig] int get_EnginePID(out int pPID);
	}
	
	internal enum TASK_STATE {
		TASK_STATE_UNKNOWN,
		TASK_STATE_DISABLED,
		TASK_STATE_QUEUED,
		TASK_STATE_READY,
		TASK_STATE_RUNNING
	}
	
	[ComImport, Guid("02820E19-7B98-4ed2-B2E8-FDCCCEFF619B")] //dual
	internal interface IActionCollection {
		int Count { get; }
		[PreserveSig] int get_Item(int index, out IAction ppAction);
		void __NewEnum();
		string XmlText { get; set; }
		[PreserveSig] int Create(TASK_ACTION_TYPE type, out IAction ppAction);
		[PreserveSig] int Remove(object index);
		[PreserveSig] int Clear();
		string Context { get; set; }
	}
	
	[ComImport, Guid("D98D51E5-C9B4-496a-A9C1-18980261CF0F")] //dual
	internal interface IPrincipal {
		string Id { get; set; }
		string DisplayName { get; set; }
		string UserId { get; set; }
		TASK_LOGON_TYPE LogonType { get; set; }
		string GroupId { get; set; }
		TASK_RUNLEVEL_TYPE RunLevel { get; set; }
	}
	
	[ComImport, Guid("8FD4711D-2D02-4c8c-87E3-EFF699DE127E")] //dual
	internal interface ITaskSettings {
		bool AllowDemandStart { get; set; }
		string RestartInterval { get; set; }
		int RestartCount { get; set; }
		int MultipleInstances { get; set; }
		bool StopIfGoingOnBatteries { get; set; }
		bool DisallowStartIfOnBatteries { get; set; }
		bool AllowHardTerminate { get; set; }
		bool StartWhenAvailable { get; set; }
		string XmlText { get; set; }
		bool RunOnlyIfNetworkAvailable { get; set; }
		string ExecutionTimeLimit { get; set; }
		bool Enabled { get; set; }
		string DeleteExpiredTaskAfter { get; set; }
		int Priority { get; set; }
		int Compatibility { get; set; }
		bool Hidden { get; set; }
		IIdleSettings IdleSettings { get; set; }
		bool RunOnlyIfIdle { get; set; }
		bool WakeToRun { get; set; }
		INetworkSettings NetworkSettings { get; set; }
	} //info: ITaskSettings2/3 not useful
	
	[ComImport, Guid("416D8B73-CB41-4ea1-805C-9BE9A5AC4A74")] //dual
	internal interface IRegistrationInfo {
		string Description { get; set; }
		string Author { get; set; }
		string Version { get; set; }
		string Date { get; set; }
		string Documentation { get; set; }
		string XmlText { get; set; }
		string URI { get; set; }
		object SecurityDescriptor { get; set; }
		string Source { get; set; }
	}
	
	[ComImport, Guid("9F7DEA84-C30B-4245-80B6-00E9F646F1B4")] //dual
	internal interface INetworkSettings {
		string Name { get; set; }
		string Id { get; set; }
	}
	
	[ComImport, Guid("84594461-0053-4342-A8FD-088FABF11F32")] //dual
	internal interface IIdleSettings {
		string IdleDuration { get; set; }
		string WaitTimeout { get; set; }
		bool StopOnIdleEnd { get; set; }
		bool RestartOnIdle { get; set; }
	}
	
	internal enum TASK_COMPATIBILITY {
		TASK_COMPATIBILITY_AT,
		TASK_COMPATIBILITY_V1,
		TASK_COMPATIBILITY_V2,
		TASK_COMPATIBILITY_V2_1,
		TASK_COMPATIBILITY_V2_2,
		TASK_COMPATIBILITY_V2_3,
		TASK_COMPATIBILITY_V2_4
	}
	
	internal enum TASK_INSTANCES_POLICY {
		TASK_INSTANCES_PARALLEL,
		TASK_INSTANCES_QUEUE,
		TASK_INSTANCES_IGNORE_NEW,
		TASK_INSTANCES_STOP_EXISTING
	}
	
	internal enum TASK_RUNLEVEL_TYPE {
		TASK_RUNLEVEL_LUA,
		TASK_RUNLEVEL_HIGHEST
	}
	
	internal enum TASK_LOGON_TYPE {
		TASK_LOGON_NONE,
		TASK_LOGON_PASSWORD,
		TASK_LOGON_S4U,
		TASK_LOGON_INTERACTIVE_TOKEN,
		TASK_LOGON_GROUP,
		TASK_LOGON_SERVICE_ACCOUNT,
		TASK_LOGON_INTERACTIVE_TOKEN_OR_PASSWORD
	}
	
	[ComImport, Guid("BAE54997-48B1-4cbe-9965-D6BE263EBEA4")] //dual
	internal interface IAction {
		string Id { get; set; }
		TASK_ACTION_TYPE Type { get; }
	}
	
	internal enum TASK_ACTION_TYPE {
		TASK_ACTION_EXEC,
		TASK_ACTION_COM_HANDLER = 5,
		TASK_ACTION_SEND_EMAIL,
		TASK_ACTION_SHOW_MESSAGE
	}
	
	[ComImport, Guid("4c3d624d-fd6b-49a3-b9b7-09cb3cd3f047")] //dual
	internal interface IExecAction {
		void _0(); void _1(); void _2(); //IAction
		string Path { get; set; }
		string Arguments { get; set; }
		string WorkingDirectory { get; set; }
	}
	
	internal const int TASK_ENUM_HIDDEN = 0x1;
	
	internal enum TASK_TRIGGER_TYPE2 {
		EVENT,
		TIME,
		DAILY,
		WEEKLY,
		MONTHLY,
		MONTHLYDOW,
		IDLE,
		REGISTRATION,
		BOOT,
		LOGON,
		SESSION = 11,
		CUSTOM_TRIGGER_01
	}
	
	[ComImport, Guid("85df5081-1b24-4f32-878a-d9d14df4cb77")] //dual
	internal interface ITriggerCollection {
		int Count { get; }
		[PreserveSig] int get_Item(int index, out ITrigger ppTrigger);
		void __NewEnum();
		ITrigger Create(TASK_TRIGGER_TYPE2 type);
		[PreserveSig] int Remove(object index);
		void Clear();
	}
	
	[ComImport, Guid("09941815-ea89-4b5b-89e0-2a773801fac3")] //dual
	internal interface ITrigger {
		TASK_TRIGGER_TYPE2 Type { get; }
		string Id { get; set; }
		IRepetitionPattern Repetition { get; set; }
		string ExecutionTimeLimit { get; set; }
		string StartBoundary { get; set; }
		string EndBoundary { get; set; }
		bool Enabled { get; set; }
	}
	
	[ComImport, Guid("7FB9ACF1-26BE-400e-85B5-294B9C75DFD6")] //dual
	internal interface IRepetitionPattern {
		string Interval { get; set; }
		string Duration { get; set; }
		bool StopAtDurationEnd { get; set; }
	}
	
	[ComImport, Guid("b45747e0-eba7-4276-9f29-85c5bb300006")]
	internal interface ITimeTrigger {
		void _0();void _1();void _2();void _3();void _4();void _5();void _6();void _7();void _8();void _9();void _10();void _11();void _12();
		string RandomDelay { get; set; }
	}
	
	[ComImport, Guid("126c5cd8-b288-41d5-8dbf-e491446adc5c")]
	internal interface IDailyTrigger {
		void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12();
		short DaysInterval { get; set; }
		string RandomDelay { get; set; }
	}
	
	[ComImport, Guid("5038fc98-82ff-436d-8728-a512a57c9dc1")]
	internal interface IWeeklyTrigger {
		void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12();
		ushort DaysOfWeek { get; set; }
		short WeeksInterval { get; set; }
		string RandomDelay { get; set; }
	}
	
	[ComImport, Guid("97c45ef1-6b02-4a1a-9c0e-1ebfba1500ac")]
	internal interface IMonthlyTrigger {
		void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12();
		uint DaysOfMonth { get; set; }
		ushort MonthsOfYear { get; set; }
		bool RunOnLastDayOfMonth { get; set; }
		string RandomDelay { get; set; }
	}
	
	[ComImport, Guid("77d025a3-90fa-43aa-b52e-cda5499b946a")]
	internal interface IMonthlyDOWTrigger {
		void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12();
		ushort DaysOfWeek { get; set; }
		ushort WeeksOfMonth { get; set; }
		ushort MonthsOfYear { get; set; }
		bool RunOnLastWeekOfMonth { get; set; }
		string RandomDelay { get; set; }
	}
	
	//[ComImport, Guid("2a9c35da-d357-41f4-bbc1-207ac1b1f3cb")]
	//internal interface IBootTrigger {
	//	void _0();void _1();void _2();void _3();void _4();void _5();void _6();void _7();void _8();void _9();void _10();void _11();void _12();
	//	string Delay { get; set; }
	//}
	
	[ComImport, Guid("d45b0167-9653-4eef-b94f-0732ca7af251")]
	internal interface IEventTrigger {
		void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12();
		string Subscription { get; set; }
		string Delay { get; set; }
		ITaskNamedValueCollection ValueQueries { get; set; }
	}
	
	[ComImport, Guid("b4ef826b-63c3-46e4-a504-ef69e4f7ea4d")]
	internal interface ITaskNamedValueCollection {
		int Count { get; }
		[PreserveSig] int get_Item(int index, out ITaskNamedValuePair ppPair);
		void __NewEnum();
		[PreserveSig] int Create(string name, string value, out ITaskNamedValuePair ppPair);
		[PreserveSig] int Remove(int index);
		[PreserveSig] int Clear();
	}
	
	[ComImport, Guid("39038068-2b46-4afd-8662-7bb6f868d221")]
	internal interface ITaskNamedValuePair {
		string Name { get; set; }
		string Value { get; set; }
	}
	
	[ComImport, Guid("72dade38-fae4-4b3e-baf4-5d009af02b1c")]
	internal interface ILogonTrigger {
		void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12();
		string Delay { get; set; }
		string UserId { get; set; }
	}
	
	[ComImport, Guid("4c8fec3a-c218-4e0c-b23d-629024db91a2")]
	internal interface IRegistrationTrigger {
		void _0();void _1();void _2();void _3();void _4();void _5();void _6();void _7();void _8();void _9();void _10();void _11();void _12();
		string Delay { get; set; }
	}
	
	[ComImport, Guid("754da71b-4385-4475-9dd9-598294fa3641")]
	internal interface ISessionStateChangeTrigger {
		void _0(); void _1(); void _2(); void _3(); void _4(); void _5(); void _6(); void _7(); void _8(); void _9(); void _10(); void _11(); void _12();
		string Delay { get; set; }
		string UserId { get; set; }
		TASK_SESSION_STATE_CHANGE_TYPE StateChange { get; set; }
	}
	
	internal struct MONTHLYDOW {
		public ushort wWhichWeek;
		public ushort rgfDaysOfTheWeek;
		public ushort rgfMonths;
	}
	
	internal struct MONTHLYDATE {
		public uint rgfDays;
		public ushort rgfMonths;
	}
	
	internal struct WEEKLY {
		public ushort WeeksInterval;
		public ushort rgfDaysOfTheWeek;
	}
	
	internal struct DAILY {
		public ushort DaysInterval;
	}
	
	internal enum TASK_SESSION_STATE_CHANGE_TYPE {
		TASK_CONSOLE_CONNECT = 1,
		TASK_CONSOLE_DISCONNECT,
		TASK_REMOTE_CONNECT,
		TASK_REMOTE_DISCONNECT,
		TASK_SESSION_LOCK = 7,
		TASK_SESSION_UNLOCK
	}
	
	internal const int TVM_GETNEXTITEM = 0x110A;
	internal const int TVM_EXPAND = 0x1102;
	
	internal const int TVGN_CHILD = 0x4;
	
	internal const int TVE_EXPAND = 0x2;
	
	internal const int TCM_SETCURFOCUS = 0x1330;
}
#pragma warning restore 649, 169 //field never assigned/used
