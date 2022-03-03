using System.Security.Principal;

static class WinTaskScheduler {
	static string _SidCurrentUser => WindowsIdentity.GetCurrent().User.ToString();
	//static string _SddlCurrentUserReadExecute => "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;" + _SidCurrentUser + ")";
	static string _SddlCurrentUserReadExecute => "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;" + _SidCurrentUser + ")";
	static string c_sddlEveryoneReadExecute = "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;WD)";

	static Api.ITaskFolder _GetOrCreateFolder(Api.ITaskService ts, string taskFolder, out bool createdNew) {
		Api.ITaskFolder tf;
		try { tf = ts.GetFolder(taskFolder); createdNew = false; }
		catch (FileNotFoundException) { tf = ts.GetFolder(null).CreateFolder(taskFolder, c_sddlEveryoneReadExecute); createdNew = true; }
		return tf;
	}

	/// <summary>
	/// Creates or updates a trigerless task that executes a program as system, admin or user.
	/// This process must be admin.
	/// You can use <see cref="RunTask"/> to run the task.
	/// </summary>
	/// <param name="taskFolder">This function creates the folder (and ancestors) if does not exist.</param>
	/// <param name="taskName">See <see cref="RunTask"/>.</param>
	/// <param name="programFile">Full path of an exe file. This function does not normalize it.</param>
	/// <param name="IL">Can be System, High or Medium. If System, runs in SYSTEM account. Else in creator's account.</param>
	/// <param name="args">Command line arguments. Can contain literal substrings $(Arg0), $(Arg1), ..., $(Arg32) that will be replaced by <see cref="RunTask"/>.</param>
	/// <param name="author"></param>
	/// <exception cref="UnauthorizedAccessException">Probably because this process is not admin.</exception>
	/// <exception cref="Exception"></exception>
	public static void CreateTaskWithoutTriggers(string taskFolder, string taskName, UacIL IL, string programFile, string args = null, string author = "Au") {
		var userId = IL == UacIL.System ? "<UserId>S-1-5-18</UserId>" : null;
		var runLevel = IL switch { UacIL.System => null, UacIL.High => "<RunLevel>HighestAvailable</RunLevel>", _ => "<RunLevel>LeastPrivilege</RunLevel>" };
		var version = osVersion.minWin10 ? "4" : "3";
		var xml =
$@"<?xml version='1.0' encoding='UTF-16'?>
<Task version='1.{version}' xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'>

<RegistrationInfo>
<Author>{author}</Author>
</RegistrationInfo>

<Principals>
<Principal id='Author'>
{userId}
{runLevel}
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

</Task>";
		var ts = new Api.TaskScheduler() as Api.ITaskService;
		ts.Connect();
		var tf = _GetOrCreateFolder(ts, taskFolder, out bool createdNew);
		if (!createdNew) try { tf.DeleteTask(taskName, 0); } catch (FileNotFoundException) { } //we use DeleteTask/TASK_CREATE, because TASK_CREATE_OR_UPDATE does not update task file's security
		var logonType = IL == UacIL.System ? Api.TASK_LOGON_TYPE.TASK_LOGON_SERVICE_ACCOUNT : Api.TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN;
		var sddl = IL == UacIL.System ? c_sddlEveryoneReadExecute : _SddlCurrentUserReadExecute;
		tf.RegisterTask(taskName, xml, Api.TASK_CREATION.TASK_CREATE, null, null, logonType, sddl);

		//note: cannot create a task that runs only in current interactive session, regardless of user.
		//	Tried INTERACTIVE: userId "S-1-5-4", logonType TASK_LOGON_GROUP. But then runs in all logged in sessions.
	}

	/// <summary>
	/// Runs task. Does not wait.
	/// Returns process id.
	/// </summary>
	/// <param name="taskFolder">Can be like <c>@"\Folder"</c> or <c>@"\A\B"</c> or <c>"Folder"</c> or <c>@"\"</c> or <c>""</c> or null.</param>
	/// <param name="taskName">Can be like <c>"Name"</c> or <c>@"\Folder\Name"</c> or <c>@"Folder\Name"</c>.</param>
	/// <param name="joinArgs">Join args into single arg for $(Arg0).</param>
	/// <param name="args">Replacement values for substrings $(Arg0), $(Arg1), ..., $(Arg32) in 'create task' args. See <msdn>IRegisteredTask.Run</msdn>.</param>
	/// <exception cref="Exception">Failed. Probably the task does not exist.</exception>
	public static int RunTask(string taskFolder, string taskName, bool joinArgs, params string[] args) {
		object a; if (args.NE_()) a = null; else if (joinArgs) a = StringUtil.CommandLineFromArray(args); else a = args;
		var ts = new Api.TaskScheduler() as Api.ITaskService;
		ts.Connect();
		var rt = ts.GetFolder(taskFolder).GetTask(taskName).Run(a);
		rt.get_EnginePID(out int pid);
		return pid;
	}

	/// <summary>
	/// Returns true if the task exists.
	/// </summary>
	/// <param name="taskFolder">See <see cref="RunTask"/>.</param>
	/// <param name="taskName">See <see cref="RunTask"/>.</param>
	/// <exception cref="Exception">Failed.</exception>
	public static bool TaskExists(string taskFolder, string taskName) {
		var ts = new Api.TaskScheduler() as Api.ITaskService;
		ts.Connect();
		try { ts.GetFolder(taskFolder).GetTask(taskName); }
		catch (FileNotFoundException) { return false; }
		return true;
	}

	/// <summary>
	/// Deletes task if exists.
	/// This process must be admin.
	/// </summary>
	/// <param name="taskFolder">See <see cref="RunTask"/>.</param>
	/// <param name="taskName">See <see cref="RunTask"/>.</param>
	/// <exception cref="Exception">Failed.</exception>
	public static void DeleteTask(string taskFolder, string taskName) {
		var ts = new Api.TaskScheduler() as Api.ITaskService;
		ts.Connect();
		try { ts.GetFolder(taskFolder).DeleteTask(taskName, 0); } catch (FileNotFoundException) { }
	}

	/// <summary>
	/// Opens Task Scheduler UI for task editing.
	/// This process must be admin.
	/// Starts task and returns.
	/// </summary>
	/// <param name="taskFolder">See <see cref="RunTask"/>.</param>
	/// <param name="taskName">Task name (without path).</param>
	public static void EditTask(string taskFolder, string taskName) {
		Task.Run(() => {
			//run Task Scheduler UI
			var w = wnd.runAndFind(
				() => run.it(folders.System + "mmc.exe", folders.System + "taskschd.msc", flags: RFlags.InheritAdmin),
				60, cn: "MMCMainFrame");

			//expand folder "Task Scheduler Library"
			var tv = w.Child(id: 12785);
			tv.Focus();
			var htvi = wait.forCondition(5, () => tv.Send(TVM_GETNEXTITEM, TVGN_CHILD, tv.Send(TVM_GETNEXTITEM)));
			wait.forCondition(10, () => 0 != tv.Send(TVM_EXPAND, TVE_EXPAND, htvi)); //note: don't wait for TVM_GETITEMSTATE TVIS_EXPANDED

			//open the specified folder
			var e = elm.fromWindow(tv, EObjid.CLIENT);
			e.Item = 2;
			taskFolder = taskFolder.Trim('\\').Replace('\\', '|');
			e.Expand(taskFolder, waitS: 10, notLast: true).Select();

			//open Properties dialog of the specified task
			var lv = w.Child(30, "***wfName listViewMain", "*.SysListView32.*"); //the slowest part, ~1 s
			lv.Elm["LISTITEM", taskName, flags: EFFlags.ClientArea | EFFlags.HiddenToo].Find(10).Select();
			lv.Post(Api.WM_KEYDOWN, (int)KKey.Enter);

			//wait for task Properties dialog and select tab "Trigger"
			var wp = wnd.find(10, taskName + "*", "*.Window.*", WOwner.Process(w.ProcessId));
			wp.Activate();
			var tc = wp.Child(5, cn: "*.SysTabControl32.*");
			tc.Send(TCM_SETCURFOCUS, 1);

			//never mind: the script may fail at any step, although on my computers never failed.
			//	Let it do as much as it can. It's better than nothing.
			//	Task.Run silently handles exceptions.
		});
	}

	const int TVM_GETNEXTITEM = 0x110A;
	const int TVM_EXPAND = 0x1102;

	const int TVGN_CHILD = 0x4;

	const int TVE_EXPAND = 0x2;

	const int TCM_SETCURFOCUS = 0x1330;
}
