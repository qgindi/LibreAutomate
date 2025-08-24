namespace Au.Types;

/// <summary>
/// Taskbar button flash, progress, add/delete.
/// </summary>
public unsafe class WTaskbarButton {
	readonly wnd _w;
	
	internal WTaskbarButton(wnd w) => _w = w;
	
	/// <summary>
	/// Starts or stops flashing the taskbar button of this window.
	/// </summary>
	/// <param name="count">The number of times to flash. If 0, stops flashing.</param>
	public void Flash(int count) {
		//const uint FLASHW_STOP = 0;
		//const uint FLASHW_CAPTION = 0x00000001;
		const uint FLASHW_TRAY = 0x00000002;
		//const uint FLASHW_ALL = FLASHW_CAPTION | FLASHW_TRAY;
		//const uint FLASHW_TIMER = 0x00000004;
		//const uint FLASHW_TIMERNOFG = 0x0000000C;
		
		var fi = new Api.FLASHWINFO { cbSize = sizeof(Api.FLASHWINFO), hwnd = _w };
		if (count > 0) {
			fi.uCount = count;
			//fi.dwTimeout = (uint)periodMS; //not useful
			fi.dwFlags = FLASHW_TRAY;
		}
		Api.FlashWindowEx(ref fi);
		
		//tested. FlashWindow is easier but does not work for taskbar button, only for caption when no taskbar button.
	}
	
	/// <summary>
	/// Sets the state of the progress indicator displayed on the taskbar button of this window.
	/// Calls <ms>ITaskbarList3.SetProgressState</ms>.
	/// </summary>
	/// <param name="state">Progress indicator state and color.</param>
	public void SetProgressState(WTBProgressState state) {
		_TL?.SetProgressState(_w, state);
	}
	
	/// <summary>
	/// Sets the value of the progress indicator displayed on the taskbar button of this window.
	/// Calls <ms>ITaskbarList3.SetProgressValue</ms>.
	/// </summary>
	/// <param name="progressValue">Progress indicator value, 0 to <i>progressTotal</i>.</param>
	/// <param name="progressTotal">Max progress indicator value.</param>
	public void SetProgressValue(int progressValue, int progressTotal = 100) {
		_TL?.SetProgressValue(_w, progressValue, progressTotal);
	}
	
	/// <summary>
	/// Adds taskbar button of this window.
	/// Calls <ms>ITaskbarList.AddTab</ms>.
	/// </summary>
	public void Add() {
		_TL?.AddTab(_w);
		//tested: always returns 0, even if w is 0. Did not test other ITaskbarList3 methods.
	}
	
	/// <summary>
	/// Deletes taskbar button of this window.
	/// Calls <ms>ITaskbarList.DeleteTab</ms>.
	/// </summary>
	public void Delete() {
		_TL?.DeleteTab(_w);
	}
	
	static ITaskbarList3 _TL {
		get {
			var r = (ITaskbarList3)new TaskbarList();
			if (0 != r.HrInit()) return null;
			return r;
		}
	}
	
	[ComImport, Guid("56fdf344-fd6d-11d0-958a-006097c9a090"), ClassInterface(ClassInterfaceType.None)]
	class TaskbarList { }
	
	[ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	interface ITaskbarList3 {
		// ITaskbarList
		[PreserveSig] int HrInit();
		[PreserveSig] int AddTab(wnd hwnd);
		[PreserveSig] int DeleteTab(wnd hwnd);
		[PreserveSig] int ActivateTab(wnd hwnd);
		[PreserveSig] int SetActiveAlt(wnd hwnd);
		
		// ITaskbarList2
		[PreserveSig] int MarkFullscreenWindow(wnd hwnd, bool fFullscreen);
		
		// ITaskbarList3
		[PreserveSig] int SetProgressValue(wnd hwnd, long ullCompleted, long ullTotal);
		[PreserveSig] int SetProgressState(wnd hwnd, WTBProgressState state);
		[PreserveSig] int RegisterTab(wnd hwndTab, wnd hwndMDI);
		[PreserveSig] int UnregisterTab(wnd hwndTab);
		[PreserveSig] int SetTabOrder(wnd hwndTab, wnd hwndInsertBefore);
		[PreserveSig] int SetTabActive(wnd hwndTab, wnd hwndMDI, uint dwReserved);
		[PreserveSig] int ThumbBarAddButtons(wnd hwnd, uint cButtons, IntPtr pButton); //LPTHUMBBUTTON
		[PreserveSig] int ThumbBarUpdateButtons(wnd hwnd, uint cButtons, IntPtr pButton); //LPTHUMBBUTTON
		[PreserveSig] int ThumbBarSetImageList(wnd hwnd, IntPtr himl);
		[PreserveSig] int SetOverlayIcon(wnd hwnd, IntPtr hIcon, string pszDescription);
		[PreserveSig] int SetThumbnailTooltip(wnd hwnd, string pszTip);
		[PreserveSig] int SetThumbnailClip(wnd hwnd, ref RECT prcClip);
	}
}

/// <summary>
/// Used by <see cref="WTaskbarButton.SetProgressState"/>.
/// </summary>
[NoDoc]
public enum WTBProgressState {
#pragma warning disable 1591 //XML doc
	NoProgress = 0,
	Indeterminate = 0x1,
	Normal = 0x2,
	Error = 0x4,
	Paused = 0x8
#pragma warning restore 1591 //XML doc
}
