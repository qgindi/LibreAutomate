//rejected.
//	Existing functions are rarely used and can be invoked with hotkeys, eg Win+D = Show Desktop, Win+Tab = Task View, Win+M = Minimize All, Win+Shift+M = Undo.
//	Maybe will be added in the future, when added more functions, eg for virtual desktops.
#if !true
namespace Au
{
	/// <summary>
	/// Arranges windows, shows/hides desktop. The same as the taskbar right-click menu commands.
	/// </summary>
	public static class desktop
	{
		/// <summary>
		/// Shows or hides desktop.
		/// If there are non-minimized main windows, minimizes them. Else restores windows recently minimized by this function.
		/// </summary>
		public static void toggleShowDesktop() => _Do(0);

		/// <summary>
		/// Minimizes main windows.
		/// </summary>
		public static void minimizeWindows() => _Do(1);

		/// <summary>
		/// Cascades non-minimized main windows.
		/// </summary>
		public static void cascadeWindows() => _Do(3);

		/// <summary>
		/// Arranges non-minimized main windows horizontally or vertically.
		/// </summary>
		public static void tileWindows(bool vertically) => _Do(vertically ? 5 : 4);

		/// <summary>
		/// Restores windows recently minimized, cascaded or tiled with other functions of this class.
		/// </summary>
		public static void undoMinimizeEtc() => _Do(2);

		/// <summary>
		/// Shows the Task View, aka Window Switcher.
		/// May not work on some Windows versions.
		/// </summary>
		public static void taskView() => _Do(6);

		static void _Do(int what) {
			try {
				dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); //speed: faster than calling a method
				switch (what) {
				case 0: shell.ToggleDesktop(); break;
				case 1: shell.MinimizeAll(); break;
				case 2: shell.UndoMinimizeALL(); break;
				case 3: shell.CascadeWindows(); break;
				case 4: shell.TileHorizontally(); break;
				case 5: shell.TileVertically(); break;
				case 6: shell.WindowSwitcher(); break;
				}
				Marshal.ReleaseComObject(shell);

				//The COM object does not do exactly the same as the true Explorer commands.
				//Eg MinimizeAll does not activete desktop. Then a minimized window is active.
				if (what == 1 && wnd.active.IsMinimized && wnd.getwnd.desktop(out var wd, out _)) wd.ActivateL();
			}
			catch { }

			wnd.getwnd.shellWindow.MinimalSleepIfOtherThread_();
		}
	}

	//FUTURE: use IVirtualDesktopManager to manage virtual desktops.
	//Its MoveWindowToDesktop does not work with windows of other processes, but we can inject a dll into another process.
	//The inteface also has IsWindowOnCurrentVirtualDesktop and GetWindowDesktopId.
	//Also there are internal/undocumented interfaces to add/remove/switch desktops etc. There is a GitHub library. And another library that injects.
}
#endif
