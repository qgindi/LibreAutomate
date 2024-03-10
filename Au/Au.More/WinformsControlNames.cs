namespace Au.More
{
	/// <summary>
	/// Gets programming names of .NET Windows Forms controls.
	/// </summary>
	/// <remarks>
	/// Usually each control has a unique name. It's the <b>Control.Name</b> property. Useful to identify controls without a classic name/text.
	/// The control id of these controls is not useful, it is not constant.
	/// </remarks>
	public sealed class WinformsControlNames : IDisposable
	{
		ProcessMemory _pm;
		wnd _w;

		///
		public void Dispose() {
			if (_pm != null) { _pm.Dispose(); _pm = null; }
			GC.SuppressFinalize(this);
		}

		static readonly int WM_GETCONTROLNAME = Api.RegisterWindowMessage("WM_GETCONTROLNAME");

		/// <summary>
		/// Prepares to get control names.
		/// </summary>
		/// <param name="w">Any top-level or child window of that process.</param>
		/// <exception cref="AuWndException"><i>w</i> invalid.</exception>
		/// <exception cref="AuException">Failed to allocate process memory (see <see cref="ProcessMemory"/>) needed to get control names, usually because of [](xref:uac).</exception>
		public WinformsControlNames(wnd w) {
			_pm = new ProcessMemory(w, 4096); //throws
			_w = w;
		}

		/// <summary>
		/// Gets control name.
		/// </summary>
		/// <returns><c>null</c> if failed or the name is empty.</returns>
		/// <param name="c">The control. Can be a top-level window too. Must be of the same process as the window specified in the constructor.</param>
		public string GetControlName(wnd c) {
			if (_pm == null) return null;
			if (!IsWinformsControl(c)) return null;
			if (!c.SendTimeout(5000, out var R, WM_GETCONTROLNAME, 4096, _pm.Mem) || (int)R < 1) return null;
			int len = (int)R - 1;
			if (len == 0) return "";
			return _pm.ReadCharString(len);
		}

		/// <summary>
		/// Returns <c>true</c> if window class name starts with <c>"WindowsForms"</c>.
		/// Usually it means that we can get Windows Forms control name of <i>w</i> and its child controls.
		/// </summary>
		/// <param name="w">The window. Can be top-level or control.</param>
		public static bool IsWinformsControl(wnd w) {
			return w.ClassNameIs("WindowsForms*");
		}

		/// <summary>
		/// Gets the programming name of a Windows Forms control.
		/// </summary>
		/// <returns><c>null</c> if it is not a Windows Forms control or if failed.</returns>
		/// <param name="c">The control. Can be top-level window too.</param>
		/// <remarks>
		/// This function is easy to use and does not throw exceptions. However, when you need names of multiple controls of a single window, better create a <b>WinformsControlNames</b> instance (once) and for each control call its <b>GetControlName</b> method, it will be faster.</remarks>
		public static string GetSingleControlName(wnd c) {
			if (!IsWinformsControl(c)) return null;
			try {
				using (var x = new WinformsControlNames(c)) return x.GetControlName(c);
			}
			catch { }
			return null;
		}

		//Don't use this cached version, it does not make significantly faster. Also, keeping process handle in such a way is not good, would need to use other thread to close it after some time.
		///// <summary>
		///// Gets programming name of a Windows Forms control.
		///// Returns <c>null</c> if it is not a Windows Forms control or if failed.
		///// </summary>
		///// <param name="c">The control. Can be top-level window too.</param>
		///// <remarks>When need to get control names repeatedly or quite often, this function can be faster than creating <b>WinformsControlNames</b> instance each time and calling its <b>GetControlName</b> method, because this function remembers the last used process etc. Also it is easier to use and does not throw exceptions.</remarks>
		//public static string GetSingleControlName(wnd c)
		//{
		//	if(!IsWinformsControl(c)) return null;
		//	uint pid = c.ProcessId; if(pid == 0) return null;
		//	lock (_prevLock) {
		//		if(pid != _prevPID || perf.ms - _prevTime > 1000) {
		//			if(_prev != null) { _prev.Dispose(); _prev = null; }
		//			try { _prev = new WinformsControlNames(c); } catch { }
		//			//print.it("new");
		//		} //else print.it("cached");
		//		_prevPID = pid; _prevTime = perf.ms;
		//		if(_prev == null) return null;
		//		return _prev.GetControlName(c);
		//	}
		//}
		//static WinformsControlNames _prev; static uint _prevPID; static long _prevTime; static object _prevLock = new object(); //cache
	}
}
