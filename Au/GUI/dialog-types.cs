#pragma warning disable 649 //unused fields in API structs

namespace Au.Types {
	
#pragma warning disable 1591 //missing XML documentation
	/// <summary>
	/// Standard icons for <see cref="dialog.show"/> and similar functions.
	/// </summary>
	public enum DIcon {
		Warning = 0xffff,
		Error = 0xfffe,
		Info = 0xfffd,
		Shield = 0xfffc,
		
		//these are undocumented but used in .NET TaskDialogStandardIcon. But why need?
		//ShieldBlueBar = ushort.MaxValue - 4,
		//ShieldGrayBar = ushort.MaxValue - 8,
		//ShieldWarningYellowBar = ushort.MaxValue - 5,
		//ShieldErrorRedBar = ushort.MaxValue - 6,
		//ShieldSuccessGreenBar = ushort.MaxValue - 7,
		
		/// <summary>
		/// Use <ms>IDI_APPLICATION</ms> icon from unmanaged resources of this program file or main assembly.
		/// If there are no icons - default program icon.
		/// C# compilers add app icon with this id. The <see cref="DIcon.App"/> value is = <ms>IDI_APPLICATION</ms> (32512).
		/// If this program file contains multiple native icons in range <c>DIcon.App</c> to 0xf000, you can specify them like <c>DIcon.App+1</c>.
		/// </summary>
		App = Api.IDI_APPLICATION
	}
	
	//rejected: struct DIcon with public static fields for common icons, like TaskDialogIcon. Rarely used.
	//	Then also could support string like "x". But is it good?
	//	Bad: intellisense does not auto-show completions like it does for enum. In our editor could make it show.
	//public struct DIcon2
	//{
	//	object _o; //icon, Icon, Bitmap, IntPtr, string, int (standard icon)
	
	//	internal DIcon2(object o) {
	//		_o = o;
	//	}
	
	//	public static implicit operator DIcon2(icon i) => new(i);
	
	//	//public static implicit operator DIcon2(string i) => new(icon.of(i, ?)); //now we don't know icon size. Need different sizes for main and footer icons.
	//}
	
	/// <summary>
	/// Text edit field type for <see cref="dialog.showInput"/>, <see cref="dialog.SetEditControl"/>, etc.
	/// </summary>
	public enum DEdit {
		None, Text, Multiline, Password, Number, Combo
	}
#pragma warning restore 1591 //missing XML documentation
	
	/// <summary>
	/// Flags for <see cref="dialog.show"/> and similar functions.
	/// </summary>
	[Flags]
	public enum DFlags {
		/// <summary>
		/// Display custom buttons as a column of command-links, not as a row of classic buttons.
		/// Command links can have multi-line text. The first line has bigger font.
		/// More info about custom buttons: <see cref="dialog.show"/>.
		/// </summary>
		CommandLinks = 1,
		
		/// <summary>
		/// Show expanded text in footer.
		/// </summary>
		ExpandDown = 1 << 1,
		
		/// <summary>
		/// Set <see cref="dialog.Width"/> = 700.
		/// </summary>
		Wider = 1 << 2,
		
		/// <summary>
		/// Allow to cancel even if there is no <b>Cancel</b> button.
		/// It adds <b>X</b> (Close) button to the title bar, and also allows to close the dialog with the <c>Esc</c> key.
		/// When the dialog is closed with the <b>X</b> button or <c>Esc</c>, the returned result button id is 0 if there is no <c>Cancel</c> button; else the same as when clicked the <c>Cancel</c> button.
		/// </summary>
		XCancel = 1 << 3,
		
		/// <summary>
		/// Show the dialog in the center of the owner window.
		/// </summary>
		CenterOwner = 1 << 4,
		
		/// <summary>
		/// Show the dialog at the mouse position. 
		/// </summary>
		CenterMouse = 1 << 5,
		
		/// <summary>
		/// x y are relative to the primary screen (ignore <see cref="dialog.Screen"/> etc).
		/// More info: <see cref="dialog.SetXY"/>. 
		/// </summary>
		RawXY = 1 << 6,
		
		//rejected. Can use dialog.Topmost, dialog.options.topmostIfNoOwnerWindow.
		///// <summary>
		///// Make the dialog a topmost window (always on top of other windows), regardless of <see cref="dialog.options.topmostIfNoOwnerWindow"/> etc.
		///// More info: <see cref=""/>. 
		///// </summary>
		//Topmost = ,
		
		//NoTaskbarButton = , //not so useful
		//NeverActivate = , //don't know how to implement. TDF_NO_SET_FOREGROUND does not work. LockSetForegroundWindow does not work if we can activate windows. HCBT_ACTIVATE can prevent activating but does not prevent deactivating.
		//AlwaysActivate = , //Don't use. Always allow. Because after AllowActivate (which is also used by Activate etc) always activates dialogs regardless of anything. As well as in uiAccess process.
	}
	
	/// <summary>
	/// Used with <see cref="dialog.show"/> and similar functions to add more controls and get their final values.
	/// </summary>
	public class DControls {
		/// <summary>
		/// If not <c>null</c>, adds checkbox with this text.
		/// </summary>
		public string Checkbox { get; set; }
		
		/// <summary>
		/// Sets initial and gets final checkbox value (<c>true</c> if checked).
		/// </summary>
		public bool IsChecked { get; set; }
		
		/// <summary>
		/// Adds radio buttons.
		/// A list of strings <c>"id text"</c> separated by <c>|</c>, like <c>"1 One|2 Two|3 Three"</c>.
		/// </summary>
		public Strings RadioButtons { get; set; }
		
		/// <summary>
		/// Sets initial and gets final checked radio button. It is button id (as specified in <see cref="RadioButtons"/>), not index.
		/// See <see cref="dialog.SetRadioButtons"/>.
		/// </summary>
		public int RadioId { get; set; }
		
		/// <summary>
		/// Adds a text edit control.
		/// Note: then the dialog cannot have a progress bar.
		/// </summary>
		public DEdit EditType { get; set; }
		
		/// <summary>
		/// Sets initial and gets final text edit control value.
		/// </summary>
		public string EditText { get; set; }
		
		/// <summary>
		/// Sets combo box list items used when <see cref="EditType"/> is <see cref="DEdit.Combo"/>.
		/// </summary>
		public Strings ComboItems { get; set; }
	}
	
	/// <summary>
	/// Arguments for <see cref="dialog"/> event handlers.
	/// </summary>
	/// <remarks>
	/// To return a non-zero value from the callback function, assign the value to the <c>returnValue</c> field.
	/// More info: <ms>TaskDialogCallbackProc</ms>.
	/// </remarks>
	public class DEventArgs : EventArgs {
		internal DEventArgs(dialog obj_, wnd hwnd_, DNative.TDN message_, nint wParam_, nint lParam_) {
			d = obj_; hwnd = hwnd_; message = message_; wParam = wParam_;
			LinkHref = (message_ == DNative.TDN.HYPERLINK_CLICKED) ? Marshal.PtrToStringUni(lParam_) : null;
		}
		
#pragma warning disable 1591 //missing XML documentation
		public dialog d;
		public wnd hwnd;
		/// <summary>Reference: <ms>task dialog notifications</ms>.</summary>
		public DNative.TDN message;
		public nint wParam;
		public int returnValue;
#pragma warning restore 1591 //missing XML documentation
		
		/// <summary>
		/// Clicked hyperlink <c>href</c> attribute value. Use in <see cref="dialog.HyperlinkClicked"/> event handler.
		/// </summary>
		public string LinkHref { get; private set; }
		
		/// <summary>
		/// Clicked button id. Use in <see cref="dialog.ButtonClicked"/> event handler.
		/// </summary>
		public int Button => (int)wParam;
		
		/// <summary>
		/// Dialog timer time in milliseconds. Use in <see cref="dialog.Timer"/> event handler.
		/// The event handler can set <c>returnValue</c>=1 to reset this.
		/// </summary>
		public int TimerTimeMS => (int)wParam;
		
		/// <summary>
		/// Your <see cref="dialog.ButtonClicked"/> event handler function can use this to prevent closing the dialog.
		/// </summary>
		public bool DontCloseDialog { set { returnValue = value ? 1 : 0; } }
		
		/// <summary>
		/// Gets or sets edit field text.
		/// </summary>
		public string EditText {
			get => d.EditControl.ControlText;
			set { d.EditControl.SetText(value); }
		}
	}
	
	/// <summary>
	/// Can be used through <see cref="dialog.Send"/>, to interact with dialog while it is open.
	/// </summary>
	/// <remarks>
	/// Example (in an event handler): <c>e.d.Close();</c>
	/// </remarks>
	public class DSend {
		volatile dialog _tdo;
		
		internal DSend(dialog tdo) { _tdo = tdo; }
		internal void Clear_() { _tdo = null; }
		
		/// <summary>
		/// Sends a message to the dialog.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Example (in an event handler): <c>e.d.Send.Message(DNative.TDM.CLICK_VERIFICATION, 1);</c>
		/// Also there are several other functions to send some messages: change text, close dialog, enable/disable buttons, update progress.
		/// Reference: <ms>task dialog messages</ms>.
		/// <c>NAVIGATE_PAGE</c> not supported.
		/// </remarks>
		public int Message(DNative.TDM message, nint wParam = 0, nint lParam = 0) {
			return _tdo?.SendMessage_(message, wParam, lParam) ?? 0;
		}
		
		void _SetText(bool resizeDialog, DNative.TDE partId, string text) {
			_tdo?.SetText_(resizeDialog, partId, text);
		}
		
		/// <summary>
		/// Changes the main big-font text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeText1(string text, bool resizeDialog) {
			_SetText(resizeDialog, DNative.TDE.MAIN_INSTRUCTION, text);
		}
		
		/// <summary>
		/// Changes the main small-font text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeText2(string text, bool resizeDialog) {
			_SetText(resizeDialog, DNative.TDE.CONTENT, text);
		}
		
		/// <summary>
		/// Changes the footer text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeFooterText(string text, bool resizeDialog) {
			_SetText(resizeDialog, DNative.TDE.FOOTER, text);
		}
		
		/// <summary>
		/// Changes the expanded area text.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// </remarks>
		public void ChangeExpandedText(string text, bool resizeDialog) {
			_SetText(resizeDialog, DNative.TDE.EXPANDED_INFORMATION, text);
		}
		
#if false //currently not implemented
		/// <summary>
		/// Applies new properties to the dialog while it is already open.
		/// Call this method while the dialog is open, eg in an event handler, after setting new properties.
		/// Sends message <c>DNative.TDM.NAVIGATE_PAGE</c>.
		/// </summary>
		public void Reconstruct()
		{
			var td = _tdo; if(td == null) return;
			_ApiSendMessageTASKDIALOGCONFIG(_dlg, (uint)DNative.TDM.NAVIGATE_PAGE, 0, ref td._c);
		}

		[DllImport("user32.dll", EntryPoint = "SendMessageW")]
		static extern nint _ApiSendMessageTASKDIALOGCONFIG(wnd hWnd, uint msg, nint wParam, in TASKDIALOGCONFIG c);
#endif
		/// <summary>
		/// Clicks a button. Normally it closes the dialog.
		/// </summary>
		/// <param name="buttonId">A button id or some other number that will be returned by <see cref="dialog.ShowDialog"/>.</param>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Sends message <see cref="DNative.TDM.CLICK_BUTTON"/>.
		/// </remarks>
		public bool Close(int buttonId = 0) {
			return 0 != Message(DNative.TDM.CLICK_BUTTON, buttonId);
		}
		
		/// <summary>
		/// Enables or disables a button.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Example: <c>d.Created += e => { e.d.Send.EnableButton(4, false); };</c>
		/// Sends message <see cref="DNative.TDM.ENABLE_BUTTON"/>.
		/// </remarks>
		public void EnableButton(int buttonId, bool enable) {
			Message(DNative.TDM.ENABLE_BUTTON, buttonId, enable ? 1 : 0);
		}
		
		/// <summary>
		/// Sets progress bar value, 0 to 100.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Sends message <see cref="DNative.TDM.SET_PROGRESS_BAR_POS"/>.
		/// </remarks>
		public int Progress(int percent) {
			if (percent < 100) Message(DNative.TDM.SET_PROGRESS_BAR_POS, percent + 1); //workaround for the progress bar control lag. https://stackoverflow.com/questions/5332616/disabling-net-progressbar-animation-when-changing-value
			return Message(DNative.TDM.SET_PROGRESS_BAR_POS, percent);
		}
	}
	
	#region public WinAPI
	
#pragma warning disable 1591 //missing XML documentation
	/// <summary>
	/// Rarely used constants for Windows API used by <see cref="dialog"/>.
	/// </summary>
	/// <remarks>
	/// Constants are in enums. Enum name is constant prefix. Enum members are without prefix. For example for <c>TDM_CLICK_BUTTON</c> use <c>DNative.TDM.CLICK_BUTTON</c>.
	/// </remarks>
	public static class DNative {
		/// <summary>
		/// Messages that your <see cref="dialog"/> event handler can send to the dialog.
		/// </summary>
		public enum TDM {
			NAVIGATE_PAGE = Api.WM_USER + 101,
			CLICK_BUTTON = Api.WM_USER + 102, // wParam = button id
			SET_MARQUEE_PROGRESS_BAR = Api.WM_USER + 103, // wParam = 0 (nonMarque) wParam != 0 (Marquee)
			SET_PROGRESS_BAR_STATE = Api.WM_USER + 104, // wParam = new progress state (0, 1 or 2)
			SET_PROGRESS_BAR_RANGE = Api.WM_USER + 105, // lParam = Math2.MakeLparam(min, max)
			SET_PROGRESS_BAR_POS = Api.WM_USER + 106, // wParam = new position
			SET_PROGRESS_BAR_MARQUEE = Api.WM_USER + 107, // wParam = 0 (stop marquee), wParam != 0 (start marquee), lParam = speed (milliseconds between repaints)
			SET_ELEMENT_TEXT = Api.WM_USER + 108, // wParam = element (enum DNative.TDE), lParam = new element text (string)
			CLICK_RADIO_BUTTON = Api.WM_USER + 110, // wParam = radio button id
			ENABLE_BUTTON = Api.WM_USER + 111, // wParam = button id, lParam = 0 (disable), lParam != 0 (enable)
			ENABLE_RADIO_BUTTON = Api.WM_USER + 112, // wParam = radio button id, lParam = 0 (disable), lParam != 0 (enable)
			CLICK_VERIFICATION = Api.WM_USER + 113, // wParam = 0 (unchecked), 1 (checked), lParam = 1 (set key focus)
			UPDATE_ELEMENT_TEXT = Api.WM_USER + 114, // wParam = element (enum DNative.TDE), lParam = new element text (string)
			SET_BUTTON_ELEVATION_REQUIRED_STATE = Api.WM_USER + 115, // wParam = button id, lParam = 0 (elevation not required), lParam != 0 (elevation required)
			UPDATE_ICON = Api.WM_USER + 116  // wParam = icon element (enum DNative.TDIE), lParam = new icon (icon handle or DIcon)
		}
		
		/// <summary>
		/// Notification messages that your <see cref="dialog"/> event handler receives.
		/// </summary>
		public enum TDN : uint {
			CREATED = 0,
			NAVIGATED = 1,
			BUTTON_CLICKED = 2,
			HYPERLINK_CLICKED = 3,
			TIMER = 4,
			DESTROYED = 5,
			RADIO_BUTTON_CLICKED = 6,
			DIALOG_CONSTRUCTED = 7,
			VERIFICATION_CLICKED = 8,
			HELP = 9,
			EXPANDO_BUTTON_CLICKED = 10
		}
		
		/// <summary>
		/// Constants for <see cref="DNative.TDM.SET_ELEMENT_TEXT"/> and <see cref="DNative.TDM.UPDATE_ELEMENT_TEXT"/> messages used with <see cref="dialog"/>.
		/// </summary>
		public enum TDE {
			CONTENT,
			EXPANDED_INFORMATION,
			FOOTER,
			MAIN_INSTRUCTION
		}
		
		/// <summary>
		/// Constants for <see cref="DNative.TDM.UPDATE_ICON"/> message used with <see cref="dialog"/>.
		/// </summary>
		public enum TDIE {
			ICON_MAIN,
			ICON_FOOTER
		}
	}
#pragma warning restore 1591 //missing XML documentation
	
	#endregion public WinAPI
	
}

namespace Au {
	public partial class dialog {
		#region private WinAPI
		
		delegate int _TaskDialogIndirectDelegate(in TASKDIALOGCONFIG c, out int pnButton, out int pnRadioButton, out int pChecked);
		static readonly _TaskDialogIndirectDelegate TaskDialogIndirect = _GetTaskDialogIndirect();
		
		static _TaskDialogIndirectDelegate _GetTaskDialogIndirect() {
			//Activate manifest that tells to use comctl32.dll version 6. The API is unavailable in version 5.
			//Need this if the host app does not have such manifest, eg if uses the default manifest added by Visual Studio.
			using (ActCtx_.Activate()) {
				//don't use DllImport, because it uses v5 comctl32.dll if it is already loaded.
				Api.GetDelegate(out _TaskDialogIndirectDelegate R, "comctl32.dll", "TaskDialogIndirect");
				return R;
			}
		}
		
		//TASKDIALOGCONFIG flags.
		[Flags]
		enum _TDF {
			ENABLE_HYPERLINKS = 0x0001,
			USE_HICON_MAIN = 0x0002,
			USE_HICON_FOOTER = 0x0004,
			ALLOW_DIALOG_CANCELLATION = 0x0008,
			USE_COMMAND_LINKS = 0x0010,
			USE_COMMAND_LINKS_NO_ICON = 0x0020,
			EXPAND_FOOTER_AREA = 0x0040,
			EXPANDED_BY_DEFAULT = 0x0080,
			VERIFICATION_FLAG_CHECKED = 0x0100,
			SHOW_PROGRESS_BAR = 0x0200,
			SHOW_MARQUEE_PROGRESS_BAR = 0x0400,
			CALLBACK_TIMER = 0x0800,
			POSITION_RELATIVE_TO_WINDOW = 0x1000,
			RTL_LAYOUT = 0x2000,
			NO_DEFAULT_RADIO_BUTTON = 0x4000,
			CAN_BE_MINIMIZED = 0x8000,
			//NO_SET_FOREGROUND = 0x00010000, //Win8, does not work
			SIZE_TO_CONTENT = 0x1000000,
		}
		
		//TASKDIALOGCONFIG buttons.
		[Flags]
		enum _TDCBF {
			OK = 1, Yes = 2, No = 4, Cancel = 8, Retry = 0x10, Close = 0x20,
		}
		
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		unsafe struct TASKDIALOG_BUTTON {
			public int id;
			public char* text;
		}
		
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		unsafe struct TASKDIALOGCONFIG {
			public int cbSize;
			public wnd hwndParent;
			public IntPtr hInstance;
			public _TDF dwFlags;
			public _TDCBF dwCommonButtons;
			public string pszWindowTitle;
			public IntPtr hMainIcon;
			public string pszMainInstruction;
			public string pszContent;
			public int cButtons;
			public TASKDIALOG_BUTTON* pButtons;
			public int nDefaultButton;
			public int cRadioButtons;
			public TASKDIALOG_BUTTON* pRadioButtons;
			public int nDefaultRadioButton;
			public string pszVerificationText;
			public string pszExpandedInformation;
			public string pszExpandedControlText;
			public string pszCollapsedControlText;
			public IntPtr hFooterIcon;
			public string pszFooter;
			public TaskDialogCallbackProc pfCallback;
			public IntPtr lpCallbackData;
			public int cxWidth;
		}
		
		delegate int TaskDialogCallbackProc(wnd hwnd, DNative.TDN notification, nint wParam, nint lParam, IntPtr data);
		
		#endregion private WinAPI
		
	}
}
