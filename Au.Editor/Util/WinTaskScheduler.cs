using System.Security.Principal;

static class WinTaskScheduler {
	static string _SidCurrentUser => WindowsIdentity.GetCurrent().User.ToString();
	//static string _SddlCurrentUserReadExecute => "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;" + _SidCurrentUser + ")";
	static string _SddlCurrentUserReadExecute => "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;" + _SidCurrentUser + ")";
	static string c_sddlEveryoneReadExecute = "D:AI(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGX;;;WD)";
	
	static bool _Connect(out api.ITaskService ts) {
		try {
			ts = new api.TaskScheduler() as api.ITaskService;
			return 0 == ts.Connect();
		}
		catch { ts = null; return false; }
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
	public static bool CreateTaskWithoutTriggers(string taskFolder, string taskName, UacIL IL, string programFile, string args = null, string author = "Au") {
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
		if (!_Connect(out var ts)) return false;
		if (0 != ts.GetFolder(taskFolder, out var tf)) {
			if (0 != ts.GetFolder(null, out tf) || 0 != tf.CreateFolder(taskFolder, c_sddlEveryoneReadExecute, out tf)) return false;
		} else {
			tf.DeleteTask(taskName, 0); //delete if exists. We use DeleteTask/TASK_CREATE, because TASK_CREATE_OR_UPDATE does not update task file's security.
		}
		var logonType = IL == UacIL.System ? api.TASK_LOGON_TYPE.TASK_LOGON_SERVICE_ACCOUNT : api.TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN;
		var sddl = IL == UacIL.System ? c_sddlEveryoneReadExecute : _SddlCurrentUserReadExecute;
		return 0 == tf.RegisterTask(taskName, xml, api.TASK_CREATION.TASK_CREATE, null, null, logonType, sddl, out _);
		
		//note: cannot create a task that runs only in current interactive session, regardless of user.
		//	Tried INTERACTIVE: userId "S-1-5-4", logonType TASK_LOGON_GROUP. But then runs in all logged in sessions.
	}
	
	/// <summary>
	/// Runs task. Does not wait.
	/// </summary>
	/// <returns>Process id. Returns 0 if failed, eg if the task does not exist or is disabled.</returns>
	/// <param name="taskFolder">Can be like <c>@"\Folder"</c> or <c>@"\A\B"</c> or <c>"Folder"</c> or <c>@"\"</c> or <c>""</c> or null.</param>
	/// <param name="taskName">Can be like <c>"Name"</c> or <c>@"\Folder\Name"</c> or <c>@"Folder\Name"</c>.</param>
	/// <param name="pathMustBe">If not null, don't run if the task action's path does not match this.</param>
	/// <param name="joinArgs">Join args into single arg for $(Arg0).</param>
	/// <param name="args">Replacement values for substrings $(Arg0), $(Arg1), ..., $(Arg32) in 'create task' args. See <msdn>IRegisteredTask.Run</msdn>.</param>
	public static (int processId, RResult result) RunTask(string taskFolder, string taskName, string pathMustBe, bool joinArgs, params string[] args) {
		if (!_Connect(out var ts)) return (0, RResult.CantConnect);
		if (0 != ts.GetFolder(taskFolder, out var tf) || 0 != tf.GetTask(taskName, out var t)) return (0, RResult.TaskNotFound);

		if (0 == t.get_Enabled(out var enabled) && enabled == 0) return (0, RResult.TaskDisabled);
		
		if (pathMustBe != null) {
			if (0 != t.get_Definition(out var td) || 0 != td.get_Actions(out var ac)) return (0, RResult.BadTask);
			if (0 != ac.get_Item(1, out var a1) || a1 is not api.IExecAction a2) return (0, RResult.BadTask); //1-based, it's documented
			a2.get_Path(out var s1);
			if (!filesystem.more.isSameFile(pathMustBe, s1)) return (0, RResult.BadPath);
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
	/// <param name="taskFolder">See <see cref="RunTask"/>.</param>
	/// <param name="taskName">See <see cref="RunTask"/>.</param>
	public static bool TaskExists(string taskFolder, string taskName) {
		if (!_Connect(out var ts)) return false;
		return 0 == ts.GetFolder(taskFolder, out var tf) && 0 == tf.GetTask(taskName, out _);
	}
	
	/// <summary>
	/// Deletes task if exists.
	/// This process must be admin.
	/// </summary>
	/// <param name="taskFolder">See <see cref="RunTask"/>.</param>
	/// <param name="taskName">See <see cref="RunTask"/>.</param>
	public static void DeleteTask(string taskFolder, string taskName) {
		if (!_Connect(out var ts)) return;
		if (0 == ts.GetFolder(taskFolder, out var tf)) tf.DeleteTask(taskName, 0);
	}
	
	
#pragma warning disable 649, 169 //field never assigned/used
	unsafe class api : NativeApi {
		[ComImport, Guid("0f87369f-a4e5-4cfc-bd3e-73e6154572dd"), ClassInterface(ClassInterfaceType.None)]
		internal class TaskScheduler { }
		
		
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
		
		[ComImport, Guid("2faba4c7-4da9-4013-9697-20cc3fd40f85")] //dual
		internal interface ITaskService {
			[PreserveSig] int GetFolder(string path, out ITaskFolder ppFolder);
			[PreserveSig] int GetRunningTasks(int flags, out IRunningTaskCollection ppRunningTasks);
			[PreserveSig] int NewTask(uint flags, out ITaskDefinition ppDefinition);
			[PreserveSig] int Connect(object serverName = null, object user = null, object domain = null, object password = null);
			[PreserveSig] int get_Connected(out short pConnected);
			[PreserveSig] int get_TargetServer(out string pServer);
			[PreserveSig] int get_ConnectedUser(out string pUser);
			[PreserveSig] int get_ConnectedDomain(out string pDomain);
			[PreserveSig] int get_HighestVersion(out uint pVersion);
		}
		
		[ComImport, Guid("8cfac062-a080-4c15-9a88-aa7c2af80dfc")] //dual
		internal interface ITaskFolder {
			[PreserveSig] int get_Name(out string pName);
			[PreserveSig] int get_Path(out string pPath);
			[PreserveSig] int GetFolder(string path, out ITaskFolder ppFolder);
			[PreserveSig] int GetFolders(int flags, out ITaskFolderCollection ppFolders);
			[PreserveSig] int CreateFolder(string subFolderName, object sddl, out ITaskFolder ppFolder);
			[PreserveSig] int DeleteFolder(string subFolderName, int flags);
			[PreserveSig] int GetTask(string path, out IRegisteredTask ppTask);
			[PreserveSig] int GetTasks(int flags, out IRegisteredTaskCollection ppTasks);
			[PreserveSig] int DeleteTask(string name, int flags);
			[PreserveSig] int RegisterTask(string path, string xmlText, TASK_CREATION flags, object userId, object password, TASK_LOGON_TYPE logonType, object sddl, out IRegisteredTask ppTask);
			[PreserveSig] int RegisterTaskDefinition(string path, ITaskDefinition pDefinition, int flags, object userId, object password, TASK_LOGON_TYPE logonType, object sddl, out IRegisteredTask ppTask);
			[PreserveSig] int GetSecurityDescriptor(int securityInformation, out string pSddl);
			[PreserveSig] int SetSecurityDescriptor(string sddl, int flags);
		}
		
		[ComImport, Guid("86627eb4-42a7-41e4-a4d9-ac33a72f2d52")] //dual
		internal interface IRegisteredTaskCollection {
			[PreserveSig] int get_Count(out int pCount);
			[PreserveSig] int get_Item(object index, out IRegisteredTask ppRegisteredTask);
			[PreserveSig] int get__NewEnum([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
		}
		
		[ComImport, Guid("9c86f320-dee3-4dd1-b972-a303f26b061e")] //dual
		internal interface IRegisteredTask {
			[PreserveSig] int get_Name(out string pName);
			[PreserveSig] int get_Path(out string pPath);
			[PreserveSig] int get_State(out TASK_STATE pState);
			[PreserveSig] int get_Enabled(out short pEnabled);
			[PreserveSig] int put_Enabled(short enabled);
			[PreserveSig] int Run(object @params, out IRunningTask ppRunningTask);
			[PreserveSig] int RunEx(object @params, int flags, int sessionID, string user, out IRunningTask ppRunningTask);
			[PreserveSig] int GetInstances(int flags, out IRunningTaskCollection ppRunningTasks);
			[PreserveSig] int get_LastRunTime(out DateTime pLastRunTime);
			[PreserveSig] int get_LastTaskResult(out int pLastTaskResult);
			[PreserveSig] int get_NumberOfMissedRuns(out int pNumberOfMissedRuns);
			[PreserveSig] int get_NextRunTime(out DateTime pNextRunTime);
			[PreserveSig] int get_Definition(out ITaskDefinition ppDefinition);
			[PreserveSig] int get_Xml(out string pXml);
			[PreserveSig] int GetSecurityDescriptor(int securityInformation, out string pSddl);
			[PreserveSig] int SetSecurityDescriptor(string sddl, int flags);
			[PreserveSig] int Stop(int flags);
			[PreserveSig] int GetRunTimes(in SYSTEMTIME pstStart, in SYSTEMTIME pstEnd, ref uint pCount, out SYSTEMTIME* pRunTimes);
		}
		
		[ComImport, Guid("79184a66-8664-423f-97f1-637356a5d812")] //dual
		internal interface ITaskFolderCollection {
			[PreserveSig] int get_Count(out int pCount);
			[PreserveSig] int get_Item(object index, out ITaskFolder ppFolder);
			[PreserveSig] int get__NewEnum([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
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
			[PreserveSig] int get_RegistrationInfo(out IRegistrationInfo ppRegistrationInfo);
			[PreserveSig] int put_RegistrationInfo(IRegistrationInfo pRegistrationInfo);
			[PreserveSig] int get_Triggers(out ITriggerCollection ppTriggers);
			[PreserveSig] int put_Triggers(ITriggerCollection pTriggers);
			[PreserveSig] int get_Settings(out ITaskSettings ppSettings);
			[PreserveSig] int put_Settings(ITaskSettings pSettings);
			[PreserveSig] int get_Data(out string pData);
			[PreserveSig] int put_Data(string data);
			[PreserveSig] int get_Principal(out IPrincipal ppPrincipal);
			[PreserveSig] int put_Principal(IPrincipal pPrincipal);
			[PreserveSig] int get_Actions(out IActionCollection ppActions);
			[PreserveSig] int put_Actions(IActionCollection pActions);
			[PreserveSig] int get_XmlText(out string pXml);
			[PreserveSig] int put_XmlText(string xml);
		}
		
		[ComImport, Guid("6a67614b-6828-4fec-aa54-6d52e8f1f2db")] //dual
		internal interface IRunningTaskCollection {
			[PreserveSig] int get_Count(out int pCount);
			[PreserveSig] int get_Item(object index, out IRunningTask ppRunningTask);
			[PreserveSig] int get__NewEnum([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
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
			[PreserveSig] int get_Count(out int pCount);
			[PreserveSig] int get_Item(int index, out IAction ppAction);
			[PreserveSig] int get__NewEnum([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
			[PreserveSig] int get_XmlText(out string pText);
			[PreserveSig] int put_XmlText(string text);
			[PreserveSig] int Create(TASK_ACTION_TYPE type, out IAction ppAction);
			[PreserveSig] int Remove(object index);
			[PreserveSig] int Clear();
			[PreserveSig] int get_Context(out string pContext);
			[PreserveSig] int put_Context(string context);
		}
		
		[ComImport, Guid("D98D51E5-C9B4-496a-A9C1-18980261CF0F")] //dual
		internal interface IPrincipal {
			[PreserveSig] int get_Id(out string pId);
			[PreserveSig] int put_Id(string Id);
			[PreserveSig] int get_DisplayName(out string pName);
			[PreserveSig] int put_DisplayName(string name);
			[PreserveSig] int get_UserId(out string pUser);
			[PreserveSig] int put_UserId(string user);
			[PreserveSig] int get_LogonType(out TASK_LOGON_TYPE pLogon);
			[PreserveSig] int put_LogonType(TASK_LOGON_TYPE logon);
			[PreserveSig] int get_GroupId(out string pGroup);
			[PreserveSig] int put_GroupId(string group);
			[PreserveSig] int get_RunLevel(out TASK_RUNLEVEL_TYPE pRunLevel);
			[PreserveSig] int put_RunLevel(TASK_RUNLEVEL_TYPE runLevel);
		}
		
		[ComImport, Guid("8FD4711D-2D02-4c8c-87E3-EFF699DE127E")] //dual
		internal interface ITaskSettings {
			[PreserveSig] int get_AllowDemandStart(out short pAllowDemandStart);
			[PreserveSig] int put_AllowDemandStart(short allowDemandStart);
			[PreserveSig] int get_RestartInterval(out string pRestartInterval);
			[PreserveSig] int put_RestartInterval(string restartInterval);
			[PreserveSig] int get_RestartCount(out int pRestartCount);
			[PreserveSig] int put_RestartCount(int restartCount);
			[PreserveSig] int get_MultipleInstances(out TASK_INSTANCES_POLICY pPolicy);
			[PreserveSig] int put_MultipleInstances(TASK_INSTANCES_POLICY policy);
			[PreserveSig] int get_StopIfGoingOnBatteries(out short pStopIfOnBatteries);
			[PreserveSig] int put_StopIfGoingOnBatteries(short stopIfOnBatteries);
			[PreserveSig] int get_DisallowStartIfOnBatteries(out short pDisallowStart);
			[PreserveSig] int put_DisallowStartIfOnBatteries(short disallowStart);
			[PreserveSig] int get_AllowHardTerminate(out short pAllowHardTerminate);
			[PreserveSig] int put_AllowHardTerminate(short allowHardTerminate);
			[PreserveSig] int get_StartWhenAvailable(out short pStartWhenAvailable);
			[PreserveSig] int put_StartWhenAvailable(short startWhenAvailable);
			[PreserveSig] int get_XmlText(out string pText);
			[PreserveSig] int put_XmlText(string text);
			[PreserveSig] int get_RunOnlyIfNetworkAvailable(out short pRunOnlyIfNetworkAvailable);
			[PreserveSig] int put_RunOnlyIfNetworkAvailable(short runOnlyIfNetworkAvailable);
			[PreserveSig] int get_ExecutionTimeLimit(out string pExecutionTimeLimit);
			[PreserveSig] int put_ExecutionTimeLimit(string executionTimeLimit);
			[PreserveSig] int get_Enabled(out short pEnabled);
			[PreserveSig] int put_Enabled(short enabled);
			[PreserveSig] int get_DeleteExpiredTaskAfter(out string pExpirationDelay);
			[PreserveSig] int put_DeleteExpiredTaskAfter(string expirationDelay);
			[PreserveSig] int get_Priority(out int pPriority);
			[PreserveSig] int put_Priority(int priority);
			[PreserveSig] int get_Compatibility(out TASK_COMPATIBILITY pCompatLevel);
			[PreserveSig] int put_Compatibility(TASK_COMPATIBILITY compatLevel);
			[PreserveSig] int get_Hidden(out short pHidden);
			[PreserveSig] int put_Hidden(short hidden);
			[PreserveSig] int get_IdleSettings(out IIdleSettings ppIdleSettings);
			[PreserveSig] int put_IdleSettings(IIdleSettings pIdleSettings);
			[PreserveSig] int get_RunOnlyIfIdle(out short pRunOnlyIfIdle);
			[PreserveSig] int put_RunOnlyIfIdle(short runOnlyIfIdle);
			[PreserveSig] int get_WakeToRun(out short pWake);
			[PreserveSig] int put_WakeToRun(short wake);
			[PreserveSig] int get_NetworkSettings(out INetworkSettings ppNetworkSettings);
			[PreserveSig] int put_NetworkSettings(INetworkSettings pNetworkSettings);
		}
		
		[ComImport, Guid("85df5081-1b24-4f32-878a-d9d14df4cb77")] //dual
		internal interface ITriggerCollection {
			[PreserveSig] int get_Count(out int pCount);
			[PreserveSig] int get_Item(int index, out ITrigger ppTrigger);
			[PreserveSig] int get__NewEnum([MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
			[PreserveSig] int Create(TASK_TRIGGER_TYPE2 type, out ITrigger ppTrigger);
			[PreserveSig] int Remove(object index);
			[PreserveSig] int Clear();
		}
		
		[ComImport, Guid("416D8B73-CB41-4ea1-805C-9BE9A5AC4A74")] //dual
		internal interface IRegistrationInfo {
			[PreserveSig] int get_Description(out string pDescription);
			[PreserveSig] int put_Description(string description);
			[PreserveSig] int get_Author(out string pAuthor);
			[PreserveSig] int put_Author(string author);
			[PreserveSig] int get_Version(out string pVersion);
			[PreserveSig] int put_Version(string version);
			[PreserveSig] int get_Date(out string pDate);
			[PreserveSig] int put_Date(string date);
			[PreserveSig] int get_Documentation(out string pDocumentation);
			[PreserveSig] int put_Documentation(string documentation);
			[PreserveSig] int get_XmlText(out string pText);
			[PreserveSig] int put_XmlText(string text);
			[PreserveSig] int get_URI(out string pUri);
			[PreserveSig] int put_URI(string uri);
			[PreserveSig] int get_SecurityDescriptor(out object pSddl);
			[PreserveSig] int put_SecurityDescriptor(object sddl);
			[PreserveSig] int get_Source(out string pSource);
			[PreserveSig] int put_Source(string source);
		}
		
		[ComImport, Guid("09941815-ea89-4b5b-89e0-2a773801fac3")] //dual
		internal interface ITrigger {
			[PreserveSig] int get_Type(out TASK_TRIGGER_TYPE2 pType);
			[PreserveSig] int get_Id(out string pId);
			[PreserveSig] int put_Id(string id);
			[PreserveSig] int get_Repetition(out IRepetitionPattern ppRepeat);
			[PreserveSig] int put_Repetition(IRepetitionPattern pRepeat);
			[PreserveSig] int get_ExecutionTimeLimit(out string pTimeLimit);
			[PreserveSig] int put_ExecutionTimeLimit(string timelimit);
			[PreserveSig] int get_StartBoundary(out string pStart);
			[PreserveSig] int put_StartBoundary(string start);
			[PreserveSig] int get_EndBoundary(out string pEnd);
			[PreserveSig] int put_EndBoundary(string end);
			[PreserveSig] int get_Enabled(out short pEnabled);
			[PreserveSig] int put_Enabled(short enabled);
		}
		
		[ComImport, Guid("7FB9ACF1-26BE-400e-85B5-294B9C75DFD6")] //dual
		internal interface IRepetitionPattern {
			[PreserveSig] int get_Interval(out string pInterval);
			[PreserveSig] int put_Interval(string interval);
			[PreserveSig] int get_Duration(out string pDuration);
			[PreserveSig] int put_Duration(string duration);
			[PreserveSig] int get_StopAtDurationEnd(out short pStop);
			[PreserveSig] int put_StopAtDurationEnd(short stop);
		}
		
		internal enum TASK_TRIGGER_TYPE2 {
			TASK_TRIGGER_EVENT,
			TASK_TRIGGER_TIME,
			TASK_TRIGGER_DAILY,
			TASK_TRIGGER_WEEKLY,
			TASK_TRIGGER_MONTHLY,
			TASK_TRIGGER_MONTHLYDOW,
			TASK_TRIGGER_IDLE,
			TASK_TRIGGER_REGISTRATION,
			TASK_TRIGGER_BOOT,
			TASK_TRIGGER_LOGON,
			TASK_TRIGGER_SESSION_STATE_CHANGE = 11,
			TASK_TRIGGER_CUSTOM_TRIGGER_01
		}
		
		[ComImport, Guid("9F7DEA84-C30B-4245-80B6-00E9F646F1B4")] //dual
		internal interface INetworkSettings {
			[PreserveSig] int get_Name(out string pName);
			[PreserveSig] int put_Name(string name);
			[PreserveSig] int get_Id(out string pId);
			[PreserveSig] int put_Id(string id);
		}
		
		[ComImport, Guid("84594461-0053-4342-A8FD-088FABF11F32")] //dual
		internal interface IIdleSettings {
			[PreserveSig] int get_IdleDuration(out string pDelay);
			[PreserveSig] int put_IdleDuration(string delay);
			[PreserveSig] int get_WaitTimeout(out string pTimeout);
			[PreserveSig] int put_WaitTimeout(string timeout);
			[PreserveSig] int get_StopOnIdleEnd(out short pStop);
			[PreserveSig] int put_StopOnIdleEnd(short stop);
			[PreserveSig] int get_RestartOnIdle(out short pRestart);
			[PreserveSig] int put_RestartOnIdle(short restart);
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
			[PreserveSig] int get_Id(out string pId);
			[PreserveSig] int put_Id(string Id);
			[PreserveSig] int get_Type(out TASK_ACTION_TYPE pType);
		}
		
		internal enum TASK_ACTION_TYPE {
			TASK_ACTION_EXEC,
			TASK_ACTION_COM_HANDLER = 5,
			TASK_ACTION_SEND_EMAIL,
			TASK_ACTION_SHOW_MESSAGE
		}
		
		[ComImport, Guid("4c3d624d-fd6b-49a3-b9b7-09cb3cd3f047")] //dual
		internal interface IExecAction : IAction {
			// IAction
			[PreserveSig] new int get_Id(out string pId);
			[PreserveSig] new int put_Id(string Id);
			[PreserveSig] new int get_Type(out TASK_ACTION_TYPE pType);
			// IExecAction
			[PreserveSig] int get_Path(out string pPath);
			[PreserveSig] int put_Path(string path);
			[PreserveSig] int get_Arguments(out string pArgument);
			[PreserveSig] int put_Arguments(string argument);
			[PreserveSig] int get_WorkingDirectory(out string pWorkingDirectory);
			[PreserveSig] int put_WorkingDirectory(string workingDirectory);
		}
	}
#pragma warning restore 649, 169 //field never assigned/used
	
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
