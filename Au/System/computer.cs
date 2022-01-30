#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Au {
	public static class computer {
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
		/// <returns>false if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="restart">Reboot.</param>
		/// <param name="force">Don't allow to cancel. Applications with unsaved changes will be forcibly closed.</param>
		/// <param name="timeoutS">The length of time to display the shutdown dialog box, in seconds.</param>
		/// <param name="message">Display this text in the shutdown dialog box and write to the event log.</param>
		/// <param name="computer">The network name of the computer to be shut down. If null (default), shuts down this computer. If used, this process must be admin.</param>
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
		/// <returns>false if failed. Supports <see cref="lastError"/>.</returns>
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
		/// <returns>false if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="force">Don't allow to cancel. Applications with unsaved changes will be forcibly closed.</param>
		public static bool logoff(bool force = false) {
			SecurityUtil.SetPrivilege("SeShutdownPrivilege", true);
			return Api.ExitWindowsEx(force ? 4 : 16, 0);
		}

		/// <summary>
		/// Performs computer sleep (suspend) or hibernate operation.
		/// </summary>
		/// <returns>false if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="hibernate"></param>
		/// <param name="force">Don't allow programs to cancel.</param>
		/// <remarks>
		/// Uses API <msdn>SetSystemPowerState</msdn>.
		/// </remarks>
		public static bool sleep(bool hibernate = false, bool force = false) {
			SecurityUtil.SetPrivilege("SeShutdownPrivilege", true);
			return Api.SetSystemPowerState(!hibernate, force);
		}

		/// <summary>
		/// Initiates computer lock operation.
		/// </summary>
		/// <returns>false if failed. Supports <see cref="lastError"/>.</returns>
		/// <remarks>
		/// Uses API <msdn>LockWorkStation</msdn>.
		/// </remarks>
		public static bool lockOrSwitchUser() {
			//if (switchUser) return Api.WTSDisconnectSession(default, -1, false); //on Win10 the same as lock
			return Api.LockWorkStation();
		}

		/// <summary>
		/// Empties the Recycle Bin.
		/// </summary>
		/// <param name="drive">If not null, empties the Recycle Bin on this drive only. Example: "D:".</param>
		/// <param name="progressUI">Show progress dialog if slow. Default true.</param>
		public static void emptyRecycleBin(string drive = null, bool progressUI = false) {
			Api.SHEmptyRecycleBin(default, drive, progressUI ? 1 : 7);
		}

		//FUTURE: events desktopSwitchEvent, sleepEvent. Like SystemEvents.
		//public static event Action desktopSwitchEvent {
		//	add {

		//	}
		//	remove {

		//	}
		//}

		//public static void waitForDesktop(double secondsTimeout, bool normalDesktop) { //normal, UAC, lock, screensaver, etc
		//																			   //using var hook = new WinEventHook(EEvent.SYSTEM_DESKTOPSWITCH);
		//}
	}
}

