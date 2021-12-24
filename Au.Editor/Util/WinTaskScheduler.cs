using System.Security.Principal;

static class WinTaskScheduler
{
	static string _SidCurrentUser => WindowsIdentity.GetCurrent().User.ToString();
	static string _SddlCurrentUserReadExecute => "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;" + _SidCurrentUser + ")";
	static string c_sddlEveryoneReadExecute = "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;WD)";

	static Api.ITaskFolder _GetOrCreateFolder(Api.ITaskService ts, string taskFolder, out bool createdNew)
	{
		Api.ITaskFolder tf;
		try { tf = ts.GetFolder(taskFolder); createdNew = false; }
		catch(FileNotFoundException) { tf = ts.GetFolder(null).CreateFolder(taskFolder, c_sddlEveryoneReadExecute); createdNew = true; }
		return tf;
	}

	/// <summary>
	/// Creates or updates a trigerless task that executes a program as system, admin or user.
	/// This process must be admin.
	/// You can use <see cref="RunTask"/> to run the task.
	/// </summary>
	/// <param name="taskFolder">
	/// This function creates the folder (and ancestors) if does not exist.
	/// </param>
	/// <param name="taskName">See <see cref="RunTask"/>.</param>
	/// <param name="programFile">Full path of an exe file. This function does not normalize it.</param>
	/// <param name="IL">Can be System, High or Medium. If System, runs in SYSTEM account. Else in creator's account.</param>
	/// <param name="args">Command line arguments. Can contain literal substrings $(Arg0), $(Arg1), ..., $(Arg32) that will be replaced by <see cref="RunTask"/>.</param>
	/// <exception cref="UnauthorizedAccessException">Probably because this process is not admin.</exception>
	/// <exception cref="Exception"></exception>
	public static void CreateTaskToRunProgramOnDemand(string taskFolder, string taskName, UacIL IL, string programFile, string args = null)
	{
		var userId = IL == UacIL.System ? "<UserId>S-1-5-18</UserId>" : null;
		var runLevel = IL == UacIL.High ? "<RunLevel>HighestAvailable</RunLevel>" : null;
		var xml =
$@"<?xml version='1.0' encoding='UTF-16'?>
<Task version='1.3' xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'>

<RegistrationInfo>
<Author>Au</Author>
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
		if(!createdNew) try { tf.DeleteTask(taskName, 0); } catch(FileNotFoundException) { } //we use DeleteTask/TASK_CREATE, because TASK_CREATE_OR_UPDATE does not update task file's security
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
	/// <param name="taskFolder">Can be like <c>@"\Folder"</c> or <c>"Folder"</c> or <c>@"\"</c> or <c>""</c> or null.</param>
	/// <param name="taskName">Can be like <c>"Name"</c> or <c>@"\Folder\Name"</c> or <c>@"Folder\Name"</c>.</param>
	/// <param name="joinArgs">Join args into single arg for $(Arg0).</param>
	/// <param name="args">Replacement values for substrings $(Arg0), $(Arg1), ..., $(Arg32) in 'create task' args. See <msdn>IRegisteredTask.Run</msdn>.</param>
	/// <exception cref="Exception">Failed. Probably the task does not exist.</exception>
	public static int RunTask(string taskFolder, string taskName, bool joinArgs, params string[] args)
	{
		object a; if(args.NE_()) a = null; else if(joinArgs) a = StringUtil.CommandLineFromArray(args); else a = args;
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
	public static bool TaskExists(string taskFolder, string taskName)
	{
		var ts = new Api.TaskScheduler() as Api.ITaskService;
		ts.Connect();
		try { ts.GetFolder(taskFolder).GetTask(taskName); }
		catch(FileNotFoundException) { return false; }
		return true;
	}

	/// <summary>
	/// Deletes task if exists.
	/// This process must be admin.
	/// </summary>
	/// <param name="taskFolder">See <see cref="RunTask"/>.</param>
	/// <param name="taskName">See <see cref="RunTask"/>.</param>
	/// <exception cref="Exception">Failed.</exception>
	public static void DeleteTask(string taskFolder, string taskName)
	{
		var ts = new Api.TaskScheduler() as Api.ITaskService;
		ts.Connect();
		try { ts.GetFolder(taskFolder).DeleteTask(taskName, 0); } catch(FileNotFoundException) { }
	}
}
