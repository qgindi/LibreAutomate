#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Au
{
	/*public FUTURE*/
	static class computer
	{
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

		public static void setTime(DateTime time) {

		}

		public static void shutdown() {

		}

		public static void restart() {

		}

		public static void lockOrSwitchUser() {

		}

		public static void logoff() {

		}

		public static void sleep() {

		}

		public static void hibernate() {

		}

		//FUTURE: events desktopSwitchEvent, sleepEvent. Like SystemEvents.
		//public static event Action desktopSwitchEvent {
		//	add {

		//	}
		//	remove {

		//	}
		//}

		public static void waitForDesktop(double secondsTimeout, bool normalDesktop) { //normal, UAC, lock, screensaver, etc
			//using var hook = new WinEventHook(EEvent.SYSTEM_DESKTOPSWITCH);
		}
	}
}
