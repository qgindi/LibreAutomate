//FUTURE: GetCpuUsage.

using Microsoft.Win32;

namespace Au {
	/// <summary>
	/// Computer shutdown etc.
	/// </summary>
	public static unsafe class computer {
		/// <summary>
		/// Gets the number of milliseconds elapsed since Windows startup, not including the time when the computer sleeps or hibernates.
		/// To get time with sleep, use <see cref="Environment.TickCount64"/>.
		/// </summary>
		/// <remarks>
		/// Uses API <msdn>QueryUnbiasedInterruptTime</msdn>.
		/// Uses the low-resolution system timer. Its period usually is 15.25 ms.
		/// Independent of computer clock time changes.
		/// </remarks>
		public static long tickCountWithoutSleep {
			get {
				if (!Api.QueryUnbiasedInterruptTime(out long t)) return Api.GetTickCount64();
				return t / 10000;
			}
		}

		//public static void setTime(DateTime time) {

		//}

		/// <summary>
		/// Initiates computer shutdown or restart operation.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="restart">Reboot.</param>
		/// <param name="force">Don't allow to cancel. Applications with unsaved changes will be forcibly closed.</param>
		/// <param name="timeoutS">The length of time to display the shutdown dialog box, in seconds.</param>
		/// <param name="message">Display this text in the shutdown dialog box and write to the event log.</param>
		/// <param name="computer">The network name of the computer to be shut down. If <c>null</c> (default), shuts down this computer. If used, this process must be admin.</param>
		/// <remarks>
		/// Calls API <msdn>InitiateSystemShutdown</msdn>.
		/// </remarks>
		public static bool shutdown(bool restart = false, bool force = false, int timeoutS = 0, string message = null, string computer = null) {
			SecurityUtil.SetPrivilege("SeShutdownPrivilege", true);
			if (!computer.NE()) SecurityUtil.SetPrivilege("SeRemoteShutdownPrivilege", true, computer);
			return Api.InitiateSystemShutdown(computer, message, timeoutS, force, restart);
		}

		/// <summary>
		/// Initiates computer shutdown or restart operation.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="flags"><b>ExitWindowsEx</b> parameter <i>uFlags</i>.</param>
		/// <param name="reason"><b>ExitWindowsEx</b> parameter <i>dwReason</i>.</param>
		/// <remarks>
		/// Calls API <msdn>ExitWindowsEx</msdn>.
		/// </remarks>
		public static bool shutdown(int flags, uint reason = 0) {
			SecurityUtil.SetPrivilege("SeShutdownPrivilege", true);
			return Api.ExitWindowsEx(flags, reason);
		}

		/// <summary>
		/// Initiates computer logoff (sign out) operation.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="force">Don't allow to cancel. Applications with unsaved changes will be forcibly closed.</param>
		public static bool logoff(bool force = false) {
			SecurityUtil.SetPrivilege("SeShutdownPrivilege", true);
			return Api.ExitWindowsEx(force ? 4 : 16, 0);
		}

		/// <summary>
		/// Computer sleep, hibernate or monitor off.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="how"></param>
		/// <remarks>
		/// To sleep or hibernate uses API <msdn>SetSuspendState</msdn>. To turn off display uses <msdn>WM_SYSCOMMAND</msdn>.
		/// 
		/// The <b>SetSuspendState</b> behavior is undefined if the system does not support S1-S3 sleep or S4 hibernate power states. It may fail or use hibernation instead of sleep. About power states: <msdn>System Power States</msdn>. Available sleep states: <c>run.console("powercfg.exe", "/A");</c>
		/// </remarks>
		public static bool suspend(CSuspend how) {
			if (how == CSuspend.SleepOrDisplay) how = 0 != Api.IsPwrSuspendAllowed() ? CSuspend.Sleep : CSuspend.Display;
			if (how == CSuspend.Display) {
				var w = WndUtil.CreateWindowDWP_(messageOnly: true);
				Api.DefWindowProc(w, Api.WM_SYSCOMMAND, Api.SC_MONITORPOWER, 2);
				Api.DestroyWindow(w);
				return true;
			}
			SecurityUtil.SetPrivilege("SeShutdownPrivilege", true);
			return 0 != Api.SetSuspendState((byte)(how == CSuspend.Hibernate ? 1 : 0), 0, 0);
			//documented: parameter bForce has no effect.
		}

		/// <summary>
		/// Initiates computer lock operation.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Uses API <msdn>LockWorkStation</msdn>.
		/// </remarks>
		public static bool lockOrSwitchUser() {
			//if (switchUser) return Api.WTSDisconnectSession(default, -1, false); //on Win10 the same as lock
			return Api.LockWorkStation();
		}

		/// <summary>
		/// Returns <c>true</c> if the computer is using battery power.
		/// </summary>
		/// <seealso cref="SystemInformation.PowerStatus"/>
		public static bool isOnBattery => SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline; //first time 4 ms

		//public static bool isOnBattery => System.Windows.SystemParameters.PowerLineStatus == System.Windows.PowerLineStatus.Offline; //first time 21 ms

		//FUTURE: events desktopSwitchEvent, sleepEvent. Like SystemEvents.
		//public static event Action desktopSwitchEvent {
		//	add {

		//	}
		//	remove {

		//	}
		//}

		//public static void waitForDesktop(Seconds timeout, bool normalDesktop) { //normal, UAC, lock, screensaver, etc
		//																			   //using var hook = new WinEventHook(EEvent.SYSTEM_DESKTOPSWITCH);
		//}

		#region suspendResumeEvent

		static Action<PowerModes> _srAction;
		static object _srLock = new();
		static IntPtr _srHandle;

		/// <summary>
		/// When the computer is about to enter a suspended state (sleep or hibernate) or has resumed operation after being suspended.
		/// </summary>
		/// <remarks>
		/// Many system events are available in <see cref="SystemEvents"/> class. For suspend/resume notifications could be used <see cref="SystemEvents.PowerModeChanged"/>, but it does not work on most computers. Use this event instead.
		///
		/// The event handler is executed in other thread. The parameter can be only <b>Resume</b> or <b>Suspend</b>. See API <msdn>PBT_APMSUSPEND</msdn> and <msdn>PBT_APMRESUMESUSPEND</msdn>.
		/// </remarks>
		public static event Action<PowerModes> suspendResumeEvent {
			add {
				lock (_srLock) {
					if (osVersion.minWin8) {
						if (_srHandle == default) {
							Api.DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS p = new() { Callback = &_SRCallback };
							_srHandle = Api.RegisterSuspendResumeNotification((IntPtr)(&p), 2);
							if (_srHandle == default) throw new AuException(0);
						}
					} else {
						SystemEvents.PowerModeChanged += _SystemEvents_PowerModeChanged;
					}
					_srAction += value;
				}
			}
			remove {
				lock (_srLock) {
					_srAction -= value;
					if (osVersion.minWin8) {
						if (_srAction == null && _srHandle != default) {
							Api.UnregisterSuspendResumeNotification(_srHandle);
							_srHandle = default;
						}
					} else {
						SystemEvents.PowerModeChanged -= _SystemEvents_PowerModeChanged;
					}
				}

			}
		}

		[UnmanagedCallersOnly]
		static int _SRCallback(void* context, int type, void* setting) {
			if (type is 4 or 7) _srAction?.Invoke(type == 4 ? PowerModes.Suspend : PowerModes.Resume);
			return 0;
		}

		static void _SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e) {
			if (e.Mode is PowerModes.Suspend or PowerModes.Resume) _srAction?.Invoke(e.Mode);
		}

		#endregion
	}
}

namespace Au.Types {
	/// <summary>
	/// Used with <see cref="computer.suspend"/>.
	/// </summary>
	public enum CSuspend {
		/// <summary>Sleep (power state S1-S3). If these power states unavailable, the function may hibernate instead.</summary>
		Sleep,

		/// <summary>Hibernate.</summary>
		Hibernate,

		/// <summary>Turn off display. It should activate Modern Suspend S0 if available.</summary>
		Display,

		/// <summary>Sleep if power states S1-S3 available, else turn off display (it should activate Modern Suspend S0 if available).</summary>
		SleepOrDisplay,
	}
}