using System.Drawing;

#pragma warning disable 649 //unused fields in API structs

//rejected: by default show dialog in screen of mouse, like with <c>dialog.options.defaultScreen = screen.ofMouse;</c>.
//	Some Windows etc dialogs do it, and for me it's probably better. Eg Explorer's Properties even is at mouse position (top-left corner).
//rejected: dialog.showCheckboxes. See Cookbook > Dialog - enum check-list, select.

namespace Au {
	/// <summary>
	/// Standard dialogs to show information or get user input.
	/// </summary>
	/// <remarks>
	/// You can use static functions (less code) or create class instances (more options).
	/// More info: <see cref="show"/>.
	/// 
	/// Uses task dialog API <msdn>TaskDialogIndirect</msdn>.
	/// 
	/// Cannot be used in services. Instead use <b>MessageBox.Show</b> with option <b>ServiceNotification</b> or <b>DefaultDesktopOnly</b>, or API <msdn>MessageBox</msdn> with corresponding flags.
	/// </remarks>
	/// <example>
	/// Simple examples.
	/// <code><![CDATA[
	/// dialog.show("Info");
	/// 
	/// string s = "More info.";
	/// dialog.showInfo("Info", s);
	/// 
	/// if(!dialog.showYesNo("Continue?", "More info.")) return;
	/// 
	/// switch(dialog.show("Save?", "More info.", "1 Save|2 Don't save|0 Cancel")) {
	/// case 1: print.it("save"); break;
	/// case 2: print.it("don't"); break;
	/// default: print.it("cancel"); break;
	/// }
	/// 
	/// if(!dialog.showInput(out string s, "Example")) return;
	/// print.it(s);
	/// ]]></code>
	/// 
	/// This example creates a class instance, sets properties, shows dialog, uses events, uses result.
	/// <code><![CDATA[
	/// var d = new dialog();
	/// d.SetText("Main text.", "More text.\nSupports <a href=\"link data\">links</a> if you subscribe to HyperlinkClicked event.");
	/// d.SetButtons("1 OK|2 Cancel|3 Custom|4 Custom2");
	/// d.SetIcon(DIcon.Warning);
	/// d.SetExpandedText("Expanded info\nand more info.", true);
	/// d.CanBeMinimized = true;
	/// d.SetCheckbox("Check");
	/// d.SetRadioButtons("1 r1|2 r2");
	/// d.SetTimeout(30, "OK");
	/// d.HyperlinkClicked += e => { dialog.show("link clicked", e.LinkHref, owner: e.hwnd); };
	/// d.ButtonClicked += e => { print.it(e.Button); if(e.Button == 4) e.DontCloseDialog = true; };
	/// d.ProgressBar = true; d.Timer += e => { e.d.Send.Progress(e.TimerTimeMS / 100); };
	/// var r = d.ShowDialog();
	/// print.it(r, d.Controls.IsChecked, d.Controls.RadioId);
	/// switch(r) { case 1: print.it("OK"); break; case dialog.Timeout: print.it("timeout"); break; }
	/// ]]></code>
	/// </example>
	public class dialog {
		#region private API
		
		//[DllImport("comctl32.dll")]
		//static extern int TaskDialogIndirect(in TASKDIALOGCONFIG c, out int pnButton, out int pnRadioButton, out int pChecked);
		delegate int _TaskDialogIndirectDelegate(in _Api.TASKDIALOGCONFIG c, out int pnButton, out int pnRadioButton, out int pChecked);
		static readonly _TaskDialogIndirectDelegate TaskDialogIndirect = _GetTaskDialogIndirect();
		
		static _TaskDialogIndirectDelegate _GetTaskDialogIndirect() {
			//Activate manifest that tells to use comctl32.dll version 6. The API is unavailable in version 5.
			//Need this if the host app does not have such manifest, eg if uses the default manifest added by Visual Studio.
			using (ActCtx_.Activate()) {
				//Also, don't use DllImport, because it uses v5 comctl32.dll if it is already loaded.
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
		
		static unsafe class _Api {
			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			internal struct TASKDIALOG_BUTTON {
				public int id;
				public char* text;
			}
			
			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			internal struct TASKDIALOGCONFIG {
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
			
			internal delegate int TaskDialogCallbackProc(wnd hwnd, DNative.TDN notification, nint wParam, nint lParam, IntPtr data);
		}
		
		#endregion private API
		
		#region static options
		
		/// <summary>
		/// Default options used by <see cref="dialog"/> class functions.
		/// </summary>
		public static class options {
			/// <summary>
			/// Default title bar text.
			/// Default value - <see cref="script.name"/>. In exe it is exe file name like <c>"Example.exe"</c>.
			/// </summary>
			public static string defaultTitle {
				get => _defaultTitle ?? script.name;
				set { _defaultTitle = value; }
			}
			static string _defaultTitle;
			
			/// <summary>
			/// Right-to-left layout.
			/// </summary>
			/// <seealso cref="dialog.RtlLayout"/>
			public static bool rtlLayout { get; set; }
			
			/// <summary>
			/// If there is no owner window, let the dialog be always on top of most other windows.
			/// Default <c>true</c>.
			/// </summary>
			/// <seealso cref="Topmost"/>
			public static bool topmostIfNoOwnerWindow { get; set; } = true;
			
			/// <summary>
			/// Show dialogs on this screen when screen is not explicitly specified (property <see cref="Screen"/> or parameter <i>screen</i>) and there is no owner window.
			/// The <b>screen</b> must be lazy or empty.
			/// </summary>
			/// <exception cref="ArgumentException"><b>screen</b> with <b>Handle</b>. Must be lazy or empty.</exception>
			/// <example>
			/// <code><![CDATA[
			/// dialog.options.defaultScreen = screen.ofActiveWindow;
			/// dialog.options.defaultScreen = screen.ofMouse;
			/// dialog.options.defaultScreen = screen.index(1, lazy: true);
			/// ]]></code>
			/// </example>
			public static screen defaultScreen {
				get => _defaultScreen;
				set => _defaultScreen = value.ThrowIfWithHandle_;
			}
			static screen _defaultScreen;
			
			/// <summary>
			/// If icon not specified, use <see cref="DIcon.App"/>.
			/// </summary>
			public static bool useAppIcon { get; set; }
			
			/// <summary>
			/// If owner window not specified, use the active or top window of current thread as owner window (disable it, etc).
			/// </summary>
			/// <seealso cref="SetOwnerWindow"/>
			public static bool autoOwnerWindow { get; set; }
			
			/// <summary>
			/// Timeout text format string.
			/// </summary>
			/// <remarks>
			/// Default: <c>"This dialog will disappear if not clicked in {0} s.\nTimeout action: {1}."</c>.
			/// Use placeholder <c>{0}</c> for seconds (in the first line) and <c>{1}</c> for default action (in the second line). 
			/// </remarks>
			/// <seealso cref="SetTimeout(int, string, bool)"/>
			public static string timeoutTextFormat { get; set; } = c_defaultTimeoutTextFormat;
			
			internal const string c_defaultTimeoutTextFormat = "This dialog will disappear if not clicked in {0} s.\nTimeout action: {1}.";
		}
		
		#endregion static options
		
		_Api.TASKDIALOGCONFIG _c;
		DFlags _flags;
		
		dialog(DFlags flags) {
			_c.cbSize = Api.SizeOf(_c);
			_flags = flags;
			RtlLayout = options.rtlLayout;
		}
		
		/// <summary>
		/// Initializes a new <see cref="dialog"/> instance and sets main properties.
		/// </summary>
		/// <remarks>
		/// More info: <see cref="show"/>.
		/// </remarks>
		/// <inheritdoc cref="show" path="/param"/>
		public dialog(
			string text1 = null, string text2 = null, Strings buttons = default, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
			string expandedText = null, string footer = null, string title = null, DControls controls = null,
			int defaultButton = 0, Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
			) : this(flags) {
			if (0 != (flags & DFlags.Wider)) Width = 700;
			
			SetText(text1, text2);
			SetIcon(icon);
			SetButtons(buttons, 0 != (flags & DFlags.CommandLinks));
			if (defaultButton != 0) DefaultButton = defaultButton;
			if (controls != null) {
				_controls = controls;
				if (controls.Checkbox != null) SetCheckbox(controls.Checkbox, controls.IsChecked);
				if (controls.RadioButtons.Value != null) SetRadioButtons(controls.RadioButtons, controls.RadioId);
			}
			SetOwnerWindow(owner, 0 != (flags & DFlags.CenterOwner));
			SetXY(x, y, 0 != (flags & DFlags.RawXY));
			Screen = screen;
			SetTimeout(secondsTimeout);
			SetExpandedText(expandedText, 0 != (flags & DFlags.ExpandDown));
			SetFooter(footer);
			SetTitleBarText(title);
			if (onLinkClick != null) HyperlinkClicked += onLinkClick;
		}
		
		#region set properties
		
		void _SetFlag(_TDF flag, bool on) {
			if (on) _c.dwFlags |= flag; else _c.dwFlags &= ~flag;
		}
		
		bool _HasFlag(_TDF flag) {
			return (_c.dwFlags & flag) != 0;
		}
		
		/// <summary>
		/// Changes title bar text.
		/// If <i>title</i> is <c>null</c> or <c>""</c> or this function not called, will use <see cref="options.defaultTitle"/>.
		/// </summary>
		public void SetTitleBarText(string title) {
			_c.pszWindowTitle = title.NE() ? options.defaultTitle : title;
			//info: if "", API uses "ProcessName.exe".
		}
		
		/// <summary>
		/// Sets text.
		/// </summary>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		public void SetText(string text1 = null, string text2 = null) {
			_c.pszMainInstruction = text1;
			_c.pszContent = text2;
		}
		
		/// <summary>
		/// Sets common icon. Or custom icon from app resources.
		/// </summary>
		public void SetIcon(DIcon icon) {
			_c.hMainIcon = (IntPtr)(int)icon;
			_SetFlag(_TDF.USE_HICON_MAIN, false);
		}
		
		/// <summary>
		/// Sets custom icon.
		/// </summary>
		/// <param name="icon">Icon of size 32 or 16 (or more if high DPI). Can be <see cref="icon"/>, <b>Icon</b>, <b>IntPtr</b> (native icon handle), <b>Bitmap</b>.</param>
		public void SetIcon(object icon) {
			_iconGC = icon; //GC
			_c.hMainIcon = _IconHandle(icon);
			_SetFlag(_TDF.USE_HICON_MAIN, _c.hMainIcon != default);
			//tested: displays original-size 32 and 16 icons, but shrinks bigger icons to 32.
			//note: for App icon ShowDialog will execute more code. The same for footer icon.
		}
		object _iconGC; //GC
		
		static IntPtr _IconHandle(object o)
			=> o switch {
				icon a => a.Handle,
				Icon a => a.Handle,
				IntPtr a => a,
				Bitmap a => new icon(a.GetHicon()),
				null => default,
				_ => throw new ArgumentException("unsupported object type")
			};
		
		#region buttons
		
		const int c_idOK = 1;
		const int c_idCancel = 2;
		const int c_idRetry = 4;
		const int c_idYes = 6;
		const int c_idNo = 7;
		const int c_idClose = 8;
		const int c_idTimeout = int.MinValue;
		
		/// <summary>
		/// The return value of <b>ShowX</b> functions on timeout.
		/// </summary>
		public const int Timeout = int.MinValue;
		
		_Buttons _buttons;
		
		struct _Buttons {
			List<(int id, string s)> _customButtons, _radioButtons;
			
			int _defaultButtonUserId;
			bool _isDefaultButtonSet;
			public int DefaultButtonUserId { get => _defaultButtonUserId; set { _defaultButtonUserId = value; _isDefaultButtonSet = true; } }
			
			bool _hasXButton;
			
			public _TDCBF SetButtons(Strings buttons, Strings customButtons) {
				_customButtons = null;
				_mapIdUserNative = null;
				_defaultButtonUserId = 0;
				_isDefaultButtonSet = false;
				
				switch (customButtons.Value) {
				case string s:
					_ParseButtons(s, true);
					break;
				case IEnumerable<string> e:
					int id = 0;
					foreach (var v in e) {
						string s = _ParseSingleString(v, ref id, true);
						(_customButtons ??= new()).Add((id, s));
					}
					DefaultButtonUserId = 1;
					break;
				}
				
				return _ParseButtons(buttons, false);
			}
			
			_TDCBF _ParseButtons(Strings buttons, bool onlyCustom) {
				var ba = buttons.ToArray(); if (ba.NE_()) return 0;
				
				_TDCBF commonButtons = 0;
				int id = 0, nextNativeId = 100;
				
				foreach (var v in ba) {
					string s = _ParseSingleString(v, ref id, onlyCustom);
					
					int nativeId = 0;
					if (!onlyCustom) {
						switch (s) {
						case "OK": commonButtons |= _TDCBF.OK; nativeId = c_idOK; break;
						case "Yes": commonButtons |= _TDCBF.Yes; nativeId = c_idYes; break;
						case "No": commonButtons |= _TDCBF.No; nativeId = c_idNo; break;
						case "Cancel": commonButtons |= _TDCBF.Cancel; nativeId = c_idCancel; break;
						case "Retry": commonButtons |= _TDCBF.Retry; nativeId = c_idRetry; break;
						case "Close": commonButtons |= _TDCBF.Close; nativeId = c_idClose; break;
						}
					}
					
					if (nativeId == 0) { //custom button
						(_customButtons ??= new()).Add((id, s));
						if (id < 0) nativeId = nextNativeId++; //need to map, because native ids of positive user ids are minus user ids
					}
					if (nativeId != 0) (_mapIdUserNative ??= new()).Add((id, nativeId));
					
					if (!_isDefaultButtonSet) DefaultButtonUserId = id;
				}
				
				return commonButtons;
			}
			
			static string _ParseSingleString(string s, ref int id, bool dontSplit) {
				if (!dontSplit && StringUtil.ParseIntAndString(s, out var i, out string r)) id = i; else { r = s; id++; }
				r = r.Trim("\r\n"); //API does not like newline at start, etc
				if (r.Length == 0) r = " "; //else API exception
				else r = r.Replace("\r\n", "\n"); //API adds 2 newlines for \r\n. Only for custom buttons, not for other controls/parts.
				return r;
			}
			
			public void SetRadioButtons(Strings buttons) {
				_radioButtons = null;
				var ba = buttons.ToArray(); if (ba.NE_()) return;
				
				_radioButtons = new();
				int id = 0;
				foreach (var v in ba) {
					string s = _ParseSingleString(v, ref id, false);
					_radioButtons.Add((id, s));
				}
			}
			
			List<(int userId, int nativeId)> _mapIdUserNative;
			
			public int MapIdUserToNative(int userId) {
				if (userId == c_idTimeout) return userId; //0x80000000
				if (_mapIdUserNative != null) { //common buttons, and custom buttons with negative user id
					foreach (var v in _mapIdUserNative) if (v.userId == userId) return v.nativeId;
				}
				return -userId; //custom button with positive user id
			}
			
			public int MapIdNativeToUser(int nativeId) {
				if (nativeId == c_idTimeout) return nativeId; //0x80000000
				if (nativeId <= 0) return -nativeId; //custom button with positive user id
				if (_mapIdUserNative != null) { //common buttons, and custom buttons with negative user id
					foreach (var v in _mapIdUserNative) if (v.nativeId == nativeId) return v.userId;
				}
				if (nativeId == c_idOK) return nativeId; //single OK button auto-added when no buttons specified
				Debug.Assert(nativeId == c_idCancel && _hasXButton);
				return 0;
			}
			
			/// <summary>
			/// Sets <c>c.pButtons</c>, <c>c.cButtons</c>, <c>c.pRadioButtons</c> and <c>c.cRadioButtons</c>.
			/// Later call <b>MarshalFreeButtons</b>.
			/// </summary>
			public unsafe void MarshalButtons(ref _Api.TASKDIALOGCONFIG c) {
				c.pButtons = _MarshalButtons(false, out c.cButtons);
				c.pRadioButtons = _MarshalButtons(true, out c.cRadioButtons);
				
				_hasXButton = ((c.dwFlags & _TDF.ALLOW_DIALOG_CANCELLATION) != 0);
			}
			
			/// <summary>
			/// Frees memory allocated by <b>MarshalButtons</b> and sets the <i>c</i> members to <c>null</c>/0.
			/// </summary>
			public unsafe void MarshalFreeButtons(ref _Api.TASKDIALOGCONFIG c) {
				MemoryUtil.Free(c.pButtons);
				MemoryUtil.Free(c.pRadioButtons);
				c.pButtons = null; c.pRadioButtons = null;
				c.cButtons = 0; c.cRadioButtons = 0;
			}
			
			unsafe _Api.TASKDIALOG_BUTTON* _MarshalButtons(bool radio, out int nButtons) {
				var a = radio ? _radioButtons : _customButtons;
				int n = a == null ? 0 : a.Count;
				nButtons = n;
				if (n == 0) return null;
				int nba = n * sizeof(_Api.TASKDIALOG_BUTTON), nb = nba;
				foreach (var v in a) nb += (v.s.Length + 1) * 2;
				var r = (_Api.TASKDIALOG_BUTTON*)MemoryUtil.Alloc(nb);
				char* s = (char*)((byte*)r + nba);
				for (int i = 0; i < n; i++) {
					var v = a[i];
					r[i].id = radio ? v.id : MapIdUserToNative(v.id);
					int len = v.s.Length + 1;
					r[i].text = Api.lstrcpyn(s, v.s, len);
					s += len;
				}
				return r;
			}
		}
		
		/// <summary>
		/// Sets common and/or custom buttons and custom buttons style.
		/// </summary>
		/// <param name="buttons">
		/// Common and/or custom buttons, like with <see cref="show"/>.
		/// These ids should be negative if you use <i>customButtons</i> too, because ids of <i>customButtons</i> are 1, 2, ... .
		/// </param>
		/// <param name="asCommandLinks">Custom buttons style. If <c>false</c> - row of classic buttons. If <c>true</c> - column of command-link buttons that can have multiline text.</param>
		/// <param name="customButtons">
		/// Additional custom buttons. All will be custom, even if named <c>"OK"</c> etc.
		/// List of labels without ids. Can be string like <c>"One|Two|..."</c> or <b>string[]</b> or <b>List&lt;string&gt;</b>.
		/// Button ids will be 1, 2, ... .
		/// <see cref="DefaultButton"/> will be 1. You can change it later.
		/// </param>
		public void SetButtons(Strings buttons, bool asCommandLinks = false, Strings customButtons = default) {
			_c.dwCommonButtons = _buttons.SetButtons(buttons, customButtons);
			DefaultButton = _buttons.DefaultButtonUserId;
			_SetFlag(_TDF.USE_COMMAND_LINKS, asCommandLinks);
		}
		
		/// <summary>
		/// Specifies which button responds to the <c>Enter</c> key.
		/// If 0 or not set, auto-selects.
		/// </summary>
		/// <value>Button id.</value>
		public int DefaultButton { set { _c.nDefaultButton = _buttons.MapIdUserToNative(value); } }
		
		/// <summary>
		/// Adds radio buttons.
		/// </summary>
		/// <param name="buttons">A list of strings <c>"id text"</c> separated by <c>|</c>, like <c>"1 One|2 Two|3 Three"</c>.</param>
		/// <param name="defaultId">Check the radio button that has this id. If omitted or 0, checks the first. If negative, does not check.</param>
		/// <remarks>
		/// To get selected radio button id after closing the dialog, use <see cref="Controls"/>.
		/// </remarks>
		public void SetRadioButtons(Strings buttons, int defaultId = 0) {
			_controls ??= new DControls();
			_buttons.SetRadioButtons(_controls.RadioButtons = buttons);
			_c.nDefaultRadioButton = _controls.RadioId = defaultId;
			_SetFlag(_TDF.NO_DEFAULT_RADIO_BUTTON, defaultId < 0);
		}
		
		#endregion buttons
		
		/// <summary>
		/// Adds check box (if <i>text</i> is not <c>null</c>/empty).
		/// </summary>
		/// <remarks>
		/// To get check box state after closing the dialog, use <see cref="Controls"/>.
		/// </remarks>
		public void SetCheckbox(string text, bool check = false) {
			_controls ??= new DControls();
			_c.pszVerificationText = _controls.Checkbox = text;
			_SetFlag(_TDF.VERIFICATION_FLAG_CHECKED, _controls.IsChecked = check);
		}
		
		/// <summary>
		/// Adds text that the user can show and hide.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="showInFooter">Show the text at the bottom of the dialog.</param>
		public void SetExpandedText(string text, bool showInFooter = false) {
			if (text.NE()) { text = null; showInFooter = false; }
			_SetFlag(_TDF.EXPAND_FOOTER_AREA, showInFooter);
			_c.pszExpandedInformation = text;
		}
		
		/// <summary>
		/// Set properties of the control that shows and hides text added by <see cref="SetExpandedText"/>.
		/// </summary>
		/// <param name="defaultExpanded"></param>
		/// <param name="collapsedText"></param>
		/// <param name="expandedText"></param>
		public void SetExpandControl(bool defaultExpanded, string collapsedText = null, string expandedText = null) {
			_SetFlag(_TDF.EXPANDED_BY_DEFAULT, defaultExpanded);
			_c.pszCollapsedControlText = collapsedText;
			_c.pszExpandedControlText = expandedText;
		}
		
		//rejected: tuple (DIcon icon, string text) footer. Bad intellisense. Nobody would be happy.
		
		/// <summary>
		/// Adds text and common icon at the bottom of the dialog.
		/// </summary>
		/// <param name="text">Text, optionally preceded by an icon character and <c>|</c>, like <c>"i|Text"</c>. Icons: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
		public void SetFooter(string text) {
			DIcon i = 0;
			if (text?.Eq(1, '|') ?? false) {
				i = text[0] switch { 'x' => DIcon.Error, '!' => DIcon.Warning, 'i' => DIcon.Info, 'v' => DIcon.Shield, 'a' => DIcon.App, _ => 0 };
				text = text[2..];
			}
			SetFooter(text, i);
		}
		
		/// <summary>
		/// Adds text and common icon at the bottom of the dialog.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="icon"></param>
		public void SetFooter(string text, DIcon icon) {
			_c.pszFooter = text;
			_c.hFooterIcon = (IntPtr)(int)icon;
			_SetFlag(_TDF.USE_HICON_FOOTER, false);
		}
		
		/// <summary>
		/// Adds text and custom icon at the bottom of the dialog.
		/// </summary>
		/// <param name="text">Text.</param>
		/// <param name="icon">Icon of size 16 (or more if high DPI). Can be <see cref="icon"/>, <b>Icon</b>, <b>IntPtr</b> (native icon handle), <b>Bitmap</b>.</param>
		public void SetFooter(string text, object icon) {
			_c.pszFooter = text;
			_iconFooterGC = icon; //GC
			_c.hFooterIcon = _IconHandle(icon);
			_SetFlag(_TDF.USE_HICON_FOOTER, _c.hFooterIcon != default);
		}
		object _iconFooterGC; //GC
		
		/// <summary>
		/// Adds <b>Edit</b> or <b>Combo</b> control.
		/// </summary>
		/// <param name="editType">Control type/style.</param>
		/// <param name="editText">Initial edit field text.</param>
		/// <param name="comboItems">Combo box items used when <i>editType</i> is <b>Combo</b>.</param>
		/// <remarks>
		/// To get control text after closing the dialog, use <see cref="Controls"/>.
		/// 
		/// Dialogs with an input field cannot have a progress bar.
		/// </remarks>
		public void SetEditControl(DEdit editType, string editText = null, Strings comboItems = default) {
			_controls ??= new DControls();
			_controls.EditType = editType;
			_controls.EditText = editText;
			_controls.ComboItems = comboItems;
			//will set other props later, because need to override user-set props
		}
		
		/// <summary>
		/// Sets the width of the dialog's client area.
		/// </summary>
		/// <remarks>
		/// The actual width will depend on DPI (the Windows setting "scale" or "text size").
		/// If less than default width, will be used default width.
		/// </remarks>
		/// <seealso cref="DFlags.Wider"/>
		public int Width { set { _c.cxWidth = value / 2; } }
		
		/// <summary>
		/// Sets owner window.
		/// </summary>
		/// <param name="owner">Owner window, or one of its child/descendant controls. Can be <b>wnd</b>, WPF window or element, winforms window or control. Can be <c>null</c>.</param>
		/// <param name="ownerCenter">Show the dialog in the center of the owner window. <see cref="SetXY"/> and <see cref="Screen"/> are ignored.</param>
		/// <param name="dontDisable">Don't disable the owner window. If <c>false</c>, disables if it belongs to this thread.</param>
		/// <remarks>
		/// The owner window will be disabled, and this dialog will be on top of it.
		/// This window will be in owner's screen, if screen was not explicitly specified with the <see cref="Screen"/> property. <see cref="dialog.options.defaultScreen"/> is ignored.
		/// </remarks>
		/// <seealso cref="options.autoOwnerWindow"/>
		public void SetOwnerWindow(AnyWnd owner, bool ownerCenter = false, bool dontDisable = false) {
			_c.hwndParent = owner.IsEmpty ? default : owner.Hwnd.Window;
			_SetFlag(_TDF.POSITION_RELATIVE_TO_WINDOW, ownerCenter);
			_enableOwner = dontDisable;
		}
		bool _enableOwner;
		
		/// <summary>
		/// Sets dialog position in screen.
		/// </summary>
		/// <param name="x">X position in <see cref="Screen"/>. If <c>default</c> - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If <c>default</c> - screen center.</param>
		/// <param name="rawXY"><i>x y</i> are relative to the primary screen (ignore <see cref="Screen"/> etc).</param>
		public void SetXY(Coord x, Coord y, bool rawXY = false) {
			_x = x; _y = y;
			_rawXY = rawXY;
		}
		
		Coord _x, _y; bool _rawXY;
		
		/// <summary>
		/// Sets the screen (display monitor) where to show the dialog in multi-screen environment.
		/// </summary>
		/// <remarks>
		/// If not set, will be used owner window's screen or <see cref="options.defaultScreen"/>.
		/// More info: <see cref="screen"/>, <see cref="wnd.MoveInScreen"/>.
		/// </remarks>
		public screen Screen { set; get; }
		
		/// <summary>
		/// Let the dialog close itself after <i>closeAfterS</i> seconds. Then <see cref="ShowDialog"/> returns <see cref="Timeout"/>.
		/// </summary>
		/// <seealso cref="options.timeoutTextFormat"/>
		public void SetTimeout(int closeAfterS, string timeoutActionText = null, bool noInfo = false) {
			_timeoutS = closeAfterS;
			_timeoutActionText = timeoutActionText;
			_timeoutNoInfo = noInfo;
		}
		int _timeoutS; bool _timeoutActive, _timeoutNoInfo; string _timeoutActionText, _timeoutFooterText;
		
		/// <summary>
		/// Right-to left layout.
		/// Default = <see cref="dialog.options.rtlLayout"/>.
		/// </summary>
		public bool RtlLayout { set; get; }
		
		/// <summary>
		/// Add <b>Minimize</b> button to the title bar.
		/// </summary>
		public bool CanBeMinimized { set; get; }
		
		/// <summary>
		/// Show progress bar.
		/// </summary>
		public bool ProgressBar { set; get; }
		
		/// <summary>
		/// Show progress bar that does not indicate which part of the work is already done.
		/// </summary>
		public bool ProgressBarMarquee { set; get; }
		
		/// <summary>
		/// Makes the dialog window topmost or non-topmost.
		/// If <c>true</c>, will set topmost style when creating the dialog. If <c>false</c>, will not set.
		/// If <c>null</c> (default), the dialog will be topmost if both these are true: no owner window, <see cref="dialog.options.topmostIfNoOwnerWindow"/> is <c>true</c> (default).
		/// </summary>
		public bool? Topmost { set; get; }
		
		#endregion set properties
		
		wnd _dlg;
		int _threadIdInShow;
		bool _locked;
		
		/// <summary>
		/// Shows the dialog.
		/// Call this method after setting text and other properties.
		/// </summary>
		/// <returns>Selected button id.</returns>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public unsafe int ShowDialog() {
			//info: named ShowDialog, not Show, to not confuse with the static Show() which is used almost everywhere in documentation.
			
			_result = 0;
			_isClosed = false;
			
			SetTitleBarText(_c.pszWindowTitle); //if not set, sets default
			_EditControlInitBeforeShowDialog(); //don't reorder, must be before flags
			
			if (_c.hwndParent.Is0 && options.autoOwnerWindow) {
				var wa = wnd.thisThread.active;
				if (wa.Is0) wa = wnd.getwnd.TopThreadWindow_(onlyVisible: true, nonPopup: true);
				_c.hwndParent = wa; //info: MessageBox.Show also does it, but it also disables all thread windows
			}
			if (_c.hwndParent.IsAlive) {
				if (!_enableOwner && !_c.hwndParent.IsOfThisThread) _enableOwner = true;
				if (_enableOwner && !_c.hwndParent.IsEnabled(false)) _enableOwner = false;
			}
			
			_SetPos(true); //get screen
			
			_SetFlag(_TDF.SIZE_TO_CONTENT, true); //can make max 50% wider
			_SetFlag(_TDF.ALLOW_DIALOG_CANCELLATION, _flags.Has(DFlags.XCancel));
			_SetFlag(_TDF.RTL_LAYOUT, RtlLayout);
			_SetFlag(_TDF.CAN_BE_MINIMIZED, CanBeMinimized);
			_SetFlag(_TDF.SHOW_PROGRESS_BAR, ProgressBar);
			_SetFlag(_TDF.SHOW_MARQUEE_PROGRESS_BAR, ProgressBarMarquee);
			_SetFlag(_TDF.ENABLE_HYPERLINKS, HyperlinkClicked != null);
			_SetFlag(_TDF.CALLBACK_TIMER, (_timeoutS > 0 || Timer != null));
			
			_timeoutActive = false;
			if (_timeoutS > 0) {
				_timeoutActive = true;
				if (!_timeoutNoInfo) {
					_timeoutFooterText = _c.pszFooter;
					_c.pszFooter = _TimeoutFooterText(_timeoutS);
					if (_c.hFooterIcon == default) _c.hFooterIcon = (IntPtr)DIcon.Info;
				}
			}
			
			if (_c.hMainIcon == default && options.useAppIcon) SetIcon(DIcon.App);
			if ((long)_c.hMainIcon is >= 1 and < 0xf000) _c.hInstance = icon.GetAppIconModuleHandle_((int)_c.hMainIcon);
			else if ((long)_c.hFooterIcon is >= 1 and < 0xf000) _c.hInstance = icon.GetAppIconModuleHandle_((int)_c.hFooterIcon);
			//info: DIcon.App is IDI_APPLICATION (32512).
			//Although MSDN does not mention that IDI_APPLICATION can be used when hInstance is NULL, it works. Even works for many other undocumented system resource ids, eg 100.
			//Non-NULL hInstance is ignored for icons specified as TD_x. It is documented and logical.
			//For App icon we could instead use icon handle, but then the small icon for the title bar and taskbar button can be distorted because shrinked from the big icon. Now extracts small icon from resources.
			
			_c.pfCallback = _CallbackProc;
			
			int rNativeButton = 0, rRadioButton = 0, rIsChecked = 0, hr = 0;
			WindowsHook hook = null;
			
			try {
				_threadIdInShow = Environment.CurrentManagedThreadId;
				
				_buttons.MarshalButtons(ref _c);
				if (_c.pButtons == null) _SetFlag(_TDF.USE_COMMAND_LINKS | _TDF.USE_COMMAND_LINKS_NO_ICON, false); //to avoid exception
				
				if (_timeoutActive) { //Need mouse/key messages to stop countdown on click or key.
					hook = WindowsHook.ThreadGetMessage(_HookProc);
				}
				
				wnd.Internal_.EnableActivate(true);
				
				for (int i = 0; i < 10; i++) { //see the API bug workaround comment below
					_LockUnlock(true); //see the API bug workaround comment below
					
					hr = _CallTDI(out rNativeButton, out rRadioButton, out rIsChecked);
					
					//TaskDialog[Indirect] API bug:
					//	If called simultaneously by 2 threads, often fails and returns an unknown error code 0x800403E9.
					//Known workarounds:
					//	1. Lock. Unlock on first callback message. Now used.
					//	2. Retry. Now used only for other unexpected errors, eg out-of-memory.
					
					//if(hr != 0) print.it("0x" + hr.ToString("X"), !_dlg.Is0);
					if (hr == 0 //succeeded
						|| hr == Api.E_INVALIDARG //will never succeed
						|| hr == unchecked((int)0x8007057A) //invalid cursor handle (custom icon disposed)
						|| !_dlg.Is0 //_dlg is set if our callback function was called; then don't retry, because the dialog was possibly shown, and only then error.
						) break;
					Thread.Sleep(30);
				}
				
				if (hr == 0) {
					_result = _buttons.MapIdNativeToUser(rNativeButton);
					if (_controls != null) {
						_controls.IsChecked = rIsChecked != 0;
						_controls.RadioId = rRadioButton;
					}
					
					WndUtil.WaitForAnActiveWindow(doEvents: true);
				}
			}
			finally {
				_LockUnlock(false);
				
				//Normally the dialog now is destroyed and _dlg now is 0, because _SetClosed called on the destroy message.
				//But on exception it is not called and the dialog is still alive and visible.
				//Therefore Windows shows its annoying "stopped working" UI (cannot reproduce it now with Core).
				//To avoid it, destroy the dialog now. Also to avoid possible memory leaks etc.
				if (!_dlg.Is0) Api.DestroyWindow(_dlg);
				
				_SetClosed();
				_threadIdInShow = 0;
				hook?.Dispose();
				_buttons.MarshalFreeButtons(ref _c);
			}
			
			if (hr != 0) throw new Win32Exception(hr);
			
			return _result;
		}
		
		int _CallTDI(out int pnButton, out int pnRadioButton, out int pChecked) {
			//#if DEBUG
			//			//Debug_.PrintIf("1" != Environment.GetEnvironmentVariable("COMPlus_legacyCorruptedStateExceptionsPolicy"), "no env var COMPlus_legacyCorruptedStateExceptionsPolicy=1");
			//			pnButton = pnRadioButton = pChecked = 0;
			//			try {
			//#endif
			return TaskDialogIndirect(in _c, out pnButton, out pnRadioButton, out pChecked);
			//#if DEBUG
			//			}
			//			catch (Exception e) {
			//				throw new Win32Exception("_CallTDI: " + e.ToStringWithoutStack()); //note: not just throw;, and don't add inner exception
			//			}
			
			//			//The API throws 'access violation' exception if some value is invalid (eg unknown flags in dwCommonButtons) or it does not like something.
			//			//By default .NET does not allow to handle eg access violation exceptions.
			//			//	Previously we would add [HandleProcessCorruptedStateExceptions], but Core ignores it.
			//			//	Now our AppHost sets environment variable COMPlus_legacyCorruptedStateExceptionsPolicy=1 before loading runtime.
			//			//	Or could move the API call to the C++ dll.
			//#endif
			
			//CONSIDER: don't use the API. Because:
			//	1. The API is so unreliable. Unexpected errors and even exceptions. Etc, etc.
			//	2. Has not all we need, and modifying it is so dirty. Eg adding edit control.
			//	3. Not everything is possible, eg cannot show window inactive.
			//	4. Does not auto-set enough width from text.
		}
		
		void _LockUnlock(bool on) {
			var obj = "/0p4oSiwoE+7Saqf30udQQ";
			if (on) {
				Debug.Assert(!_locked);
				_locked = false;
				Monitor.Enter(obj, ref _locked);
			} else if (_locked) {
				Monitor.Exit(obj);
				_locked = false;
			}
		}
		
		//Need to call this twice:
		//	1. Before showing dialog, to get screen while the dialog still isn't the active window.
		//	2. On TDN.CREATED, to move dialog if need.
		void _SetPos(bool before) {
			if (before) _scrn = default;
			if (_HasFlag(_TDF.POSITION_RELATIVE_TO_WINDOW)) return;
			bool isXY = !_x.IsEmpty || !_y.IsEmpty;
			if (_flags.Has(DFlags.CenterMouse)) {
				if (!before) {
					var p = mouse.xy;
					var scrn = screen.of(p);
					if (screen.of(_dlg).Handle != scrn.Handle) _dlg.MoveL_(scrn.Rect.XY); //resize if different DPI
					var r = _dlg.Rect;
					r.Move(p.x - r.Width / 2, p.y - 20);
					r.EnsureInScreen(scrn);
					_dlg.MoveL(r.left, r.top);
				}
			} else if (!_rawXY) {
				if (before) {
					_scrn = Screen;
					if (_scrn.IsEmpty && _c.hwndParent.Is0) _scrn = options.defaultScreen;
					if (_scrn.LazyFunc != null) _scrn = _scrn.Now;
				} else if (isXY || !_scrn.IsEmpty) {
					_dlg.MoveInScreen(_x, _y, _scrn);
				}
			} else if (!before && isXY) {
				_dlg.Move(_x, _y);
				_dlg.EnsureInScreen();
			}
		}
		screen _scrn;
		
		int _CallbackProc(wnd w, DNative.TDN message, nint wParam, nint lParam, IntPtr data) {
			Action<DEventArgs> e = null;
			int R = 0;
			
			//print.it(message);
			switch (message) {
			case DNative.TDN.DIALOG_CONSTRUCTED:
				_LockUnlock(false);
				Send = new DSend(this); //note: must be before setting _dlg, because another thread may call if(d.IsOpen) d.Send.Message(..).
				_dlg = w;
				break;
			case DNative.TDN.DESTROYED:
				//print.it(w.IsAlive); //valid
				e = Destroyed;
				break;
			case DNative.TDN.CREATED:
				if (_enableOwner) _c.hwndParent.Enable(true);
				_SetPos(false);
				
				if (Topmost ?? (_c.hwndParent.Is0 && options.topmostIfNoOwnerWindow)) w.ZorderTopmost();
				
				//w.SetStyleAdd(WS.THICKFRAME); //does not work
				
				if (_IsEdit) _EditControlCreate();
				
				//if(FlagKeyboardShortcutsVisible) w.Post(Api.WM_UPDATEUISTATE, 0x30002); //rejected. Don't need too many rarely used features.
				
				//fix API bug: dialog window is hidden if process STARTUPINFO specifies hidden window
				timer.after(1, _ => _dlg.ShowL(true)); //use timer because at this time still invisible always
				
				e = Created;
				break;
			case DNative.TDN.TIMER:
				if (_timeoutActive) {
					int timeElapsed = (int)wParam / 1000;
					if (timeElapsed < _timeoutS) {
						if (!_timeoutNoInfo) Send.ChangeFooterText(_TimeoutFooterText(_timeoutS - timeElapsed - 1), false);
					} else {
						_timeoutActive = false;
						Send.Close(c_idTimeout);
					}
				}
				
				e = Timer;
				break;
			case DNative.TDN.BUTTON_CLICKED:
				e = ButtonClicked;
				wParam = _buttons.MapIdNativeToUser((int)wParam);
				break;
			case DNative.TDN.HYPERLINK_CLICKED:
				e = HyperlinkClicked;
				break;
			case DNative.TDN.HELP:
				e = HelpF1;
				break;
			default:
				e = OtherEvents;
				break;
			}
			
			if (_IsEdit) _EditControlOnMessage(message);
			
			if (e != null) {
				var ed = new DEventArgs(this, _dlg, message, wParam, lParam);
				e(ed);
				R = ed.returnValue;
			}
			
			if (message == DNative.TDN.DESTROYED) _SetClosed();
			
			return R;
		}
		
		/// <summary>
		/// After the dialog has been created and before it is displayed.
		/// </summary>
		public event Action<DEventArgs> Created;
		
		/// <summary>
		/// When the dialog is closed and its window handle is no longer valid.
		/// </summary>
		public event Action<DEventArgs> Destroyed;
		
		/// <summary>
		/// Every 200 ms.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// var d = new dialog("test");
		/// d.Timer += e => { print.it(e.TimerTimeMS); };
		/// d.ShowDialog();
		/// ]]></code>
		/// </example>
		public event Action<DEventArgs> Timer;
		
		/// <summary>
		/// When the user selects a button.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// var d = new dialog("test", buttons: "1 Can close|2 Can't close");
		/// d.ButtonClicked += e => { print.it(e.Button); e.DontCloseDialog = e.Button == 2; };
		/// d.ShowDialog();
		/// ]]></code>
		/// </example>
		public event Action<DEventArgs> ButtonClicked;
		
		/// <summary>
		/// When the user clicks a hyperlink in the dialog text.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// var d = new dialog("test", "Text with <a href=\"link data\">links</a>.");
		/// d.HyperlinkClicked += e => { print.it(e.LinkHref); };
		/// d.ShowDialog();
		/// ]]></code>
		/// </example>
		public event Action<DEventArgs> HyperlinkClicked;
		
		/// <summary>
		/// When the user presses <c>F1</c>.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// var d = new dialog("test", "Some info.", footer: "Press F1 for more info.");
		/// d.HelpF1 += e => { run.it("https://www.google.com/search?q=more+info"); };
		/// d.ShowDialog();
		/// ]]></code>
		/// </example>
		public event Action<DEventArgs> HelpF1;
		
		/// <summary>
		/// Events other than <see cref="Created"/>, <see cref="Destroyed"/>, <see cref="Timer"/>, <see cref="ButtonClicked"/>, <see cref="HyperlinkClicked"/>, <see cref="HelpF1"/>. See API <msdn>TaskDialogCallbackProc</msdn>.
		/// </summary>
		public event Action<DEventArgs> OtherEvents;
		
		#region async etc
		
		/// <summary>
		/// Shows the dialog in new thread and returns without waiting until it is closed.
		/// </summary>
		/// <remarks>
		/// Calls <see cref="ThreadWaitForOpen"/>, therefore the dialog is already open when this function returns.
		/// More info: <see cref="showNoWait"/>
		/// </remarks>
		/// <exception cref="AggregateException">Failed to show dialog.</exception>
		public void ShowDialogNoWait() {
			var t = Task.Run(() => ShowDialog());
			if (!ThreadWaitForOpen()) throw t.Exception ?? new AggregateException();
		}
		
		/// <summary>
		/// Selected button id. The same as the <see cref="ShowDialog"/> return value.
		/// </summary>
		/// <remarks>
		/// If the result is still unavailable (the dialog still not closed):
		/// - If called from the same thread that called <see cref="ShowDialog"/>, returns 0.
		/// - If called from another thread, waits until the dialog is closed.
		/// 
		/// Note: <see cref="ShowDialogNoWait"/> calls <see cref="ShowDialog"/> in another thread.
		/// </remarks>
		public int Result {
			get {
				if (!_WaitWhileInShow()) return 0;
				return _result;
			}
		}
		int _result;
		
		/// <summary>
		/// After closing the dialog contains values of checkbox, radio buttons and/or text edit control.
		/// <c>null</c> if no controls.
		/// </summary>
		public DControls Controls => _controls;
		DControls _controls;
		
		bool _WaitWhileInShow() {
			if (_threadIdInShow != 0) {
				if (_threadIdInShow == Environment.CurrentManagedThreadId) return false;
				while (_threadIdInShow != 0) Thread.Sleep(15);
			}
			return true;
		}
		
		/// <summary>
		/// Can be used by other threads to wait until the dialog is open.
		/// </summary>
		/// <returns>
		/// <br/>• <c>true</c> - the dialog is open and you can send messages to it.
		/// <br/>• <c>false</c> - the dialog is already closed or failed to show.
		/// </returns>
		public bool ThreadWaitForOpen() {
			_AssertIsOtherThread();
			while (!IsOpen) {
				if (_isClosed) return false;
				wait.doEvents(15); //need ~3 loops if 15. Without doEvents hangs if a form is the dialog owner.
			}
			return true;
		}
		
		/// <summary>
		/// Can be used by other threads to wait until the dialog is closed.
		/// </summary>
		public void ThreadWaitForClosed() {
			_AssertIsOtherThread();
			while (!_isClosed) {
				Thread.Sleep(30);
			}
			_WaitWhileInShow();
		}
		
		void _AssertIsOtherThread() {
			if (_threadIdInShow != 0 && _threadIdInShow == Environment.CurrentManagedThreadId)
				throw new AuException("wrong thread");
		}
		
		/// <summary>
		/// Returns <c>true</c> if the dialog is open and your code can send messages to it.
		/// </summary>
		public bool IsOpen => !_dlg.Is0;
		
		void _SetClosed() {
			_isClosed = true;
			if (_dlg.Is0) return;
			_dlg = default;
			Send.Clear_();
		}
		bool _isClosed;
		
		#endregion async etc
		
		#region send messages
		
		/// <summary>
		/// Gets dialog window handle as <b>wnd</b>.
		/// </summary>
		/// <returns><c>default(wnd)</c> if the dialog is not open.</returns>
		public wnd DialogWindow => _dlg;
		
		/// <summary>
		/// Allows to modify dialog controls while it is open, and close the dialog.
		/// </summary>
		/// <remarks>
		/// Example: <c>d.Send.Close();</c> .
		/// Example: <c>d.Send.ChangeText2("new text", false);</c> .
		/// Example: <c>d.Send.Message(DNative.TDM.CLICK_VERIFICATION, 1);</c> .
		/// 
		/// Can be used only while the dialog is open. Before showing the dialog returns <c>null</c>. After closing the dialog the returned variable is deactivated; its method calls are ignored.
		/// Can be used in dialog event handlers. Also can be used in another thread, for example with <see cref="showNoWait"/> and <see cref="showProgress"/>.
		/// </remarks>
		public DSend Send { get; private set; }
		
		//called by DSend
		internal int SendMessage_(DNative.TDM message, nint wParam = 0, nint lParam = 0) {
			switch (message) {
			case DNative.TDM.CLICK_BUTTON:
			case DNative.TDM.ENABLE_BUTTON:
			case DNative.TDM.SET_BUTTON_ELEVATION_REQUIRED_STATE:
				wParam = _buttons.MapIdUserToNative((int)wParam);
				break;
			}
			
			return (int)_dlg.Send((int)message, wParam, lParam);
		}
		
		//called by DSend
		internal void SetText_(bool resizeDialog, DNative.TDE partId, string text) {
			if (partId == DNative.TDE.CONTENT && (_controls?.EditType ?? default) == DEdit.Multiline) {
				text = _c.pszContent = text + c_multilineString;
			}
			
			_dlg.Send((int)(resizeDialog ? DNative.TDM.SET_ELEMENT_TEXT : DNative.TDM.UPDATE_ELEMENT_TEXT), (int)partId, text ?? "");
			//info: null does not change text.
			
			if (_IsEdit) _EditControlUpdateAsync(!resizeDialog);
			//info: sometimes even UPDATE_ELEMENT_TEXT sends our control to the bottom of the Z order.
		}
		
		#endregion send messages
		
		#region hookProc, timeoutText
		
		//Disables timeout on click or key.
		unsafe void _HookProc(HookData.ThreadGetMessage d) {
			switch (d.msg->message) {
			case Api.WM_LBUTTONDOWN:
			case Api.WM_NCLBUTTONDOWN:
			case Api.WM_RBUTTONDOWN:
			case Api.WM_NCRBUTTONDOWN:
			case Api.WM_KEYDOWN:
			case Api.WM_SYSKEYDOWN:
				if (_timeoutActive && d.msg->hwnd.Window == _dlg) {
					_timeoutActive = false;
					//_TimeoutFooterTextHide();
					Send.ChangeFooterText(_timeoutFooterText, false);
				}
				break;
			}
		}
		
		string _TimeoutFooterText(int timeLeft) {
			using (new StringBuilder_(out var b)) {
				var format = options.timeoutTextFormat;
				if (format.NE()) return format;
				if (_timeoutActionText.NE()) format = format?.Lines()[0];
				b.AppendFormat(format, timeLeft, _timeoutActionText);
				if (!_timeoutFooterText.NE()) b.Append('\n').Append(_timeoutFooterText);
				return b.ToString();
			}
		}
		
		#endregion hookProc, timeoutText
		
		#region Edit control
		
		//never mind: our edit control disappears when moving the dialog to a screen with different DPI
		
		bool _IsEdit => _controls != null && _controls.EditType != DEdit.None;
		
		void _EditControlInitBeforeShowDialog() {
			if (!_IsEdit) return;
			ProgressBarMarquee = true;
			ProgressBar = false;
			_c.pszContent ??= "";
			if (_c.pszExpandedInformation != null && _controls.EditType == DEdit.Multiline) _SetFlag(_TDF.EXPAND_FOOTER_AREA, true);
		}
		
		void _EditControlUpdate(bool onlyZorder = false) {
			if (_editWnd.Is0) return;
			if (!onlyZorder) {
				_EditControlGetPlace(out RECT r);
				_editParent.MoveL(r);
				_editWnd.MoveL(0, 0, r.Width, r.Height);
			}
			_editParent.ZorderTopRaw_();
		}
		
		void _EditControlUpdateAsync(bool onlyZorder = false) {
			_editParent.Post(Api.WM_APP + 111, onlyZorder ? 1 : 0);
		}
		
		//to reserve space for multiline Edit control we append this to text2
		const string c_multilineString = "\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n ";
		
		wnd _EditControlGetPlace(out RECT r) {
			wnd parent = _dlg; //don't use the DirectUIHWND control for it, it can create problems
			
			//create or get cached font and calculate control height
			_editFont = NativeFont_.RegularCached(Dpi.OfWindow(parent));
			//tested: on Win8.1 API isn't PM-DPI-aware.
			
			//We'll hide the progress bar control and create our Edit control in its place.
			wnd prog = parent.Child(cn: "msctls_progress32", flags: WCFlags.HiddenToo);
			prog.GetRectIn(parent, out r);
			
			if (_controls.EditType == DEdit.Multiline) {
				int top = r.top;
				if (!_c.pszContent.Ends(c_multilineString)) {
					_c.pszContent += c_multilineString;
					_dlg.Send((int)DNative.TDM.SET_ELEMENT_TEXT, (int)DNative.TDE.CONTENT, _c.pszContent);
					prog.GetRectIn(parent, out r); //used to calculate Edit control height: after changing text, prog is moved down, and we know its previous location...
				}
				if (_editMultilineHeight == 0) { _editMultilineHeight = r.bottom - top; } else top = r.bottom - _editMultilineHeight;
				r.top = top;
			} else {
				r.top = r.bottom - (_editFont.HeightOnScreen + 8);
			}
			
			prog.ShowL(false);
			return parent;
		}
		int _editMultilineHeight;
		
		void _EditControlCreate() {
			wnd parent = _EditControlGetPlace(out RECT r);
			
			//Create an intermediate "#32770" to be direct parent of the Edit control.
			//It is safer (the dialog will not receive Edit notifications) and helps to solve Tab/Esc problems.
			var pStyle = WS.CHILD | WS.VISIBLE | WS.CLIPCHILDREN | WS.CLIPSIBLINGS; //don't need WS_TABSTOP
			var pExStyle = WSE.NOPARENTNOTIFY; //not WSE.CONTROLPARENT
			_editParent = WndUtil.CreateWindow("#32770", null, pStyle, pExStyle, r.left, r.top, r.Width, r.Height, parent);
			Api.SetWindowLongPtr(_editParent, GWL.DWL.DLGPROC, Marshal.GetFunctionPointerForDelegate(_editControlParentProcHolder = _EditControlParentProc));
			
			//Create Edit or ComboBox control.
			string cn = "Edit";
			var style = WS.CHILD | WS.VISIBLE; //don't need WS_TABSTOP
			switch (_controls.EditType) {
			case DEdit.Text: style |= Api.ES_AUTOHSCROLL; break;
			case DEdit.Password: style |= Api.ES_PASSWORD | Api.ES_AUTOHSCROLL; break;
			case DEdit.Number: style |= Api.ES_NUMBER | Api.ES_AUTOHSCROLL; break;
			case DEdit.Multiline: style |= Api.ES_MULTILINE | Api.ES_AUTOVSCROLL | Api.ES_WANTRETURN | WS.VSCROLL; break;
			case DEdit.Combo: style |= Api.CBS_DROPDOWN | Api.CBS_AUTOHSCROLL | WS.VSCROLL; cn = "ComboBox"; break;
			}
			_editWnd = WndUtil.CreateWindow(cn, null, style, WSE.CLIENTEDGE, 0, 0, r.Width, r.Height, _editParent);
			WndUtil.SetFont(_editWnd, _editFont);
			
			//Init the control.
			_editWnd.SetText(_controls.EditText);
			if (_controls.EditType == DEdit.Combo) {
				if (_controls.ComboItems.Value != null) {
					foreach (var s in _controls.ComboItems.ToArray()) _editWnd.Send(Api.CB_INSERTSTRING, -1, s);
				}
				RECT cbr = _editWnd.Rect;
				_editParent.ResizeL(cbr.Width, cbr.Height); //because ComboBox resizes itself
			} else {
				_editWnd.Send(Api.EM_SETSEL, 0, -1);
			}
			_editParent.ZorderTopRaw_();
			Api.SetFocus(_editWnd);
		}
		
		void _EditControlOnMessage(DNative.TDN message) {
			switch (message) {
			case DNative.TDN.BUTTON_CLICKED:
				_controls.EditText = _editWnd.ControlText;
				break;
			case DNative.TDN.EXPANDO_BUTTON_CLICKED:
			case DNative.TDN.NAVIGATED:
				_EditControlUpdateAsync(); //when expando clicked, sync does not work even with doevents
				break;
			}
		}
		
		/// <summary>
		/// Gets edit control handle as <b>wnd</b>.
		/// </summary>
		public wnd EditControl => _editWnd;
		wnd _editWnd, _editParent;
		NativeFont_ _editFont;
		
		//Dlgproc of our intermediate #32770 control, the parent of out Edit control.
		nint _EditControlParentProc(wnd w, int msg, nint wParam, nint lParam) {
			switch (msg) {
			case Api.WM_SETFOCUS: //enables Tab when in single-line Edit control
				Api.SetFocus(_dlg.ChildFast(null, "DirectUIHWND"));
				return 1;
			case Api.WM_NEXTDLGCTL: //enables Tab when in multi-line Edit control
				Api.SetFocus(_dlg.ChildFast(null, "DirectUIHWND"));
				return 1;
			case Api.WM_CLOSE: //enables Esc when in edit control
				_dlg.Send(msg);
				return 1;
			case Api.WM_APP + 111: //async update edit control pos
				_EditControlUpdate(wParam != 0);
				return 1;
			}
			return 0;
			//BAD: Alt+key doesn't work when the edit control is focused.
			//	https://github.com/qgindi/LibreAutomate/issues/28
			//	Also we receive 500 pairs of WM_GETDLGCODE + WM_GETTEXT.
			//	To fix this, probably would need a getmsg hook. It could detect the event and focus a button. Too expensive.
		}
		WNDPROC _editControlParentProcHolder;
		
		#endregion Edit control
		
		#region Show
		
		/// <summary>
		/// Shows dialog.
		/// </summary>
		/// <returns>Selected button id.</returns>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		/// <param name="buttons">
		/// Button ids and labels. Examples: <c>"OK|Cancel"</c>, <c>"1 &amp;Save|2 Do&amp;n't Save|0 Cancel"</c>.
		/// If omitted, <c>null</c> or <c>""</c>, the dialog will have <b>OK</b> button, id 1.
		/// Common buttons: <b>OK</b>, <b>Yes</b>, <b>No</b>, <b>Retry</b>, <b>Cancel</b>, <b>Close</b>.
		/// More info in Remarks.
		/// </param>
		/// <param name="flags"></param>
		/// <param name="icon"></param>
		/// <param name="owner">Owner window. See <see cref="SetOwnerWindow"/>.</param>
		/// <param name="expandedText">Text that the user can show and hide.</param>
		/// <param name="footer">Text at the bottom of the dialog. Icon can be specified like <c>"i|Text"</c>, where <c>i</c> is: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
		/// <param name="title">Title bar text. If omitted, <c>null</c> or <c>""</c>, uses <see cref="options.defaultTitle"/>.</param>
		/// <param name="controls">Can be used to add more controls and later get their values: checkbox, radio buttons, text input.</param>
		/// <param name="defaultButton">id of button that responds to the <c>Enter</c> key.</param>
		/// <param name="x">X position in <see cref="Screen"/>. If default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If default - center.</param>
		/// <param name="screen"><see cref="Screen"/>. Examples: <c>screen.ofMouse</c>, <c>screen.index(1)</c>.</param>
		/// <param name="secondsTimeout">If not 0, after this time (seconds) auto-close the dialog and return <see cref="Timeout"/>.</param>
		/// <param name="onLinkClick">
		/// A link-clicked event handler function, eg lambda. Enables hyperlinks in small-font text.
		/// Example:
		/// <code><![CDATA[
		/// dialog.show("", "Text <a href=\"example\">link</a>.", onLinkClick: e => { print.it(e.LinkHref); });
		/// ]]></code>
		/// </param>
		/// <remarks>
		/// Tip: Use named arguments. Example: <c>dialog.show("Text", icon: DIcon.Info, title: "Title")</c> .
		/// 
		/// This function allows you to use many dialog features, but not all. Alternatively you can create a <b>dialog</b> class instance, set properties and call <b>ShowDialog</b>. Example in <see cref="dialog"/> class help.
		/// 
		/// <h5>More info about the <i>buttons</i> parameter</h5>
		/// 
		/// Missing ids are auto-generated, for example <c>"OK|Cancel|100 Custom1|Custom2"</c> is the same as <c>"1 OK|2 Cancel|100 Custom1|101 Custom2"</c>.
		/// 
		/// The first in the list button is default, ie responds to the <c>Enter</c> key. For example, <c>"2 No|1 Yes"</c> adds <b>Yes</b> and <b>No</b> buttons and makes <b>No</b> default.
		/// 
		/// To create keyboard shortcuts, use <c>&amp;</c> character in custom button labels. Use <c>&amp;&amp;</c> for literal <c>&amp;</c>. Example: <c>"1 &amp;Tuesday[]2 T&amp;hursday[]3 Saturday &amp;&amp; Sunday"</c>.
		/// 
		/// Trims newlines around ids and labels. For example, <c>"\r\n1 One\r\n|\r\n2\r\nTwo\r\n\r\n"</c> is the same as <c>"1 One|2 Two"</c>.
		/// 
		/// There are 6 <i>common buttons</i>: <b>OK</b>, <b>Yes</b>, <b>No</b>, <b>Retry</b>, <b>Cancel</b>, <b>Close</b>. Buttons that have other labels are <i>custom buttons</i>.
		/// How common buttons are different:
		/// 1. <see cref="DFlags.CommandLinks"/> does not change their style.
		/// 2. They have keyboard shortcuts that cannot be changed. Inserting <c>&amp;</c> in a label makes it a custom button.
		/// 3. Button <b>Cancel</b> can be selected with the <c>Esc</c> key. It also adds <b>X</b> (Close) button in title bar, which selects <b>Cancel</b>.
		/// 4. Always displayed in standard order (eg <b>Yes</b> <b>No</b>, never <b>No</b> <b>Yes</b>). But you can for example use <c>"2 No|1 Yes"</c> to set default button = <b>No</b>.
		/// 5. The displayed button label is localized, ie different when the Windows UI language is not English.
		/// 
		/// You can use flag <see cref="DFlags.CommandLinks"/> to change the style of custom buttons.
		/// 
		/// See also: <see cref="SetButtons"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// if(1 != dialog.show("Continue?", null, "1 OK|2 Cancel", icon: DIcon.Info)) return;
		/// print.it("OK");
		/// 
		/// switch (dialog.show("Save changes?", "More info.", "1 Save|2 Don't Save|0 Cancel")) {
		/// case 1: print.it("save"); break;
		/// case 2: print.it("don't"); break;
		/// default: print.it("cancel"); break;
		/// }
		/// ]]></code>
		/// 
		/// <code><![CDATA[
		/// var con = new DControls { Checkbox = "Check", RadioButtons = "1 One|2 Two|3 Three", EditType = DEdit.Combo, EditText = "zero", ComboItems = ["one", "two"] };
		/// var r = dialog.show("Main text", "More text.", "1 OK|2 Cancel", expandedText: "Expanded text", controls: con, secondsTimeout: 30);
		/// print.it(r, con.IsChecked, con.RadioId, con.EditText);
		/// switch(r) {
		/// case 1: print.it("OK"); break;
		/// case dialog.Timeout: print.it("timeout"); break;
		/// default: print.it("Cancel"); break;
		/// }
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static int show(
			string text1 = null, string text2 = null, Strings buttons = default, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
			string expandedText = null, string footer = null, string title = null, DControls controls = null,
			int defaultButton = 0, Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
			) {
			var d = new dialog(text1, text2, buttons, flags, icon, owner,
				expandedText, footer, title, controls,
				defaultButton, x, y, screen, secondsTimeout, onLinkClick);
			return d.ShowDialog();
		}
		
		/// <summary>
		/// Shows dialog with <see cref="DIcon.Info"/> icon.
		/// </summary>
		/// <remarks>Calls <see cref="show"/>.</remarks>
		/// <example></example>
		/// <inheritdoc cref="show"/>
		public static int showInfo(string text1 = null, string text2 = null, Strings buttons = default, DFlags flags = 0, AnyWnd owner = default, string expandedText = null, string title = null, int secondsTimeout = 0) {
			return show(text1, text2, buttons, flags, DIcon.Info, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
		}
		
		/// <summary>
		/// Shows dialog with <see cref="DIcon.Warning"/> icon.
		/// </summary>
		/// <remarks>Calls <see cref="show"/>.</remarks>
		/// <example></example>
		/// <inheritdoc cref="show"/>
		public static int showWarning(string text1 = null, string text2 = null, Strings buttons = default, DFlags flags = 0, AnyWnd owner = default, string expandedText = null, string title = null, int secondsTimeout = 0) {
			return show(text1, text2, buttons, flags, DIcon.Warning, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
		}
		
		/// <summary>
		/// Shows dialog with <see cref="DIcon.Error"/> icon.
		/// </summary>
		/// <remarks>Calls <see cref="show"/>.</remarks>
		/// <example></example>
		/// <inheritdoc cref="show"/>
		public static int showError(string text1 = null, string text2 = null, Strings buttons = default, DFlags flags = 0, AnyWnd owner = default, string expandedText = null, string title = null, int secondsTimeout = 0) {
			return show(text1, text2, buttons, flags, DIcon.Error, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
		}
		
		/// <summary>
		/// Shows dialog with <b>OK</b> and <b>Cancel</b> buttons.
		/// </summary>
		/// <returns><c>true</c> if selected <b>OK</b>.</returns>
		/// <remarks>Calls <see cref="show"/>.</remarks>
		/// <example></example>
		/// <inheritdoc cref="show"/>
		public static bool showOkCancel(string text1 = null, string text2 = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default, string expandedText = null, string title = null, int secondsTimeout = 0) {
			return 1 == show(text1, text2, "OK|Cancel", flags, icon, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
		}
		
		/// <summary>
		/// Shows dialog with <b>Yes</b> and <b>No</b> buttons.
		/// </summary>
		/// <returns><c>true</c> if selected <b>Yes</b>.</returns>
		/// <remarks>Calls <see cref="show"/>.</remarks>
		/// <example></example>
		/// <inheritdoc cref="show"/>
		public static bool showYesNo(string text1 = null, string text2 = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default, string expandedText = null, string title = null, int secondsTimeout = 0) {
			return 1 == show(text1, text2, "Yes|No", flags, icon, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
		}
		//CONSIDER: add more parameters to all funcs like this.
		
		#endregion Show
		
		#region ShowInput
		
		/// <summary>
		/// Shows dialog with a text edit field and gets that text.
		/// </summary>
		/// <returns><c>true</c> if selected <b>OK</b> (or a custom button with id 1).</returns>
		/// <param name="s">Variable that receives the text.</param>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Read-only text below main instruction, above the edit field.</param>
		/// <param name="editType">Edit field type. It can be simple text (default), multiline, number, password or combo box.</param>
		/// <param name="editText">Initial edit field text.</param>
		/// <param name="comboItems">Combo box items used when <i>editType</i> is <b>DEdit.Combo</b>.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window. See <see cref="SetOwnerWindow"/>.</param>
		/// <param name="expandedText">Text that the user can show and hide.</param>
		/// <param name="footer">Text at the bottom of the dialog. Icon can be specified like <c>"i|Text"</c>, where <c>i</c> is: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
		/// <param name="title">Title bar text. If omitted, <c>null</c> or <c>""</c>, uses <see cref="options.defaultTitle"/>.</param>
		/// <param name="controls">Can be used to add more controls and later get their values: checkbox, radio buttons.</param>
		/// <param name="x">X position in <see cref="Screen"/>. If default - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If default - screen center.</param>
		/// <param name="screen"><see cref="Screen"/>. Examples: <c>screen.ofMouse</c>, <c>screen.index(1)</c>.</param>
		/// <param name="secondsTimeout">If not 0, after this time (seconds) auto-close the dialog and return <see cref="Timeout"/>.</param>
		/// <param name="onLinkClick">Enables hyperlinks in small-font text. A link-clicked event handler function, like with <see cref="show"/>.</param>
		/// <param name="buttons">
		/// Buttons. A list of strings <c>"id text"</c> separated by <c>|</c>, like <c>"1 OK|2 Cancel|10 Browse..."</c>. See <see cref="show"/>.
		/// Note: this function returns <c>true</c> only when clicked button with id 1.
		/// Usually custom buttons are used with <i>onButtonClick</i> function, which for example can get button id or disable closing the dialog.
		/// </param>
		/// <param name="onButtonClick">A button-clicked event handler function. See examples.</param>
		/// <remarks>
		/// This function allows you to use many dialog features, but not all. Alternatively you can create a <b>dialog</b> class instance, call <see cref="SetEditControl"/> or use the <i>controls</i> parameter, set other properties and call <b>ShowDialog</b>.
		/// </remarks>
		/// <example>
		/// Simple.
		/// <code><![CDATA[
		/// string s;
		/// if(!dialog.showInput(out s, "Example")) return;
		/// print.it(s);
		/// 
		/// if(!dialog.showInput(out var s2, "Example")) return;
		/// print.it(s2);
		/// ]]></code>
		/// 
		/// With checkbox.
		/// <code><![CDATA[
		/// var con = new DControls { Checkbox = "Check" };
		/// if(!dialog.showInput(out var s, "Example", "Comments.", controls: con)) return;
		/// print.it(s, con.IsChecked);
		/// ]]></code>
		/// 
		/// With <i>onButtonClick</i> function.
		/// <code><![CDATA[
		/// int r = 0;
		/// dialog.showInput(out string s, "Example", buttons: "OK|Cancel|Later", onButtonClick: e => r = e.Button);
		/// print.it(r);
		/// 
		/// if(!dialog.showInput(out string s, "Example", flags: DFlags.CommandLinks, buttons: "OK|Cancel|10 Set text", onButtonClick: e => {
		/// 	if(e.Button == 10) { e.EditText = "text"; e.DontCloseDialog = true; }
		/// })) return;
		/// 
		/// if(!dialog.showInput(out string s2, "Example", "Try to click OK while text is empty.", onButtonClick: e => {
		/// 	if(e.Button == 1 && e.EditText.NE()) {
		/// 		dialog.show("Text cannot be empty.", owner: e.hwnd);
		/// 		e.d.EditControl.Focus();
		/// 		e.DontCloseDialog = true;
		/// 	}
		/// })) return;
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static bool showInput(out string s,
			string text1 = null, string text2 = null,
			DEdit editType = DEdit.Text, string editText = null, Strings comboItems = default,
			DFlags flags = 0, AnyWnd owner = default,
			string expandedText = null, string footer = null, string title = null, DControls controls = null,
			Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null,
			string buttons = "1 OK|2 Cancel", Action<DEventArgs> onButtonClick = null
			) {
			if (buttons.NE()) buttons = "1 OK|2 Cancel";
			
			var d = new dialog(text1, text2, buttons, flags, 0, owner,
				expandedText, footer, title, controls,
				0, x, y, screen, secondsTimeout, onLinkClick);
			
			d.SetEditControl(editType == DEdit.None ? DEdit.Text : editType, editText, comboItems);
			if (onButtonClick != null) d.ButtonClicked += onButtonClick;
			
			bool r = 1 == d.ShowDialog();
			s = r ? d._controls.EditText : null;
			return r;
		}
		
		/// <summary>
		/// Shows dialog with a number edit field and gets that number.
		/// </summary>
		/// <returns><c>true</c> if selected <b>OK</b>.</returns>
		/// <param name="i">Variable that receives the number.</param>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Read-only text below main instruction, above the edit field.</param>
		/// <param name="editText">Initial edit field text.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window. See <see cref="SetOwnerWindow"/>.</param>
		/// <remarks>
		/// Calls <see cref="showInput"/> and converts string to <b>int</b>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// int i;
		/// if(!dialog.showInputNumber(out i, "Example")) return;
		/// print.it(i);
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		public static bool showInputNumber(out int i,
			string text1 = null, string text2 = null, int? editText = null,
			DFlags flags = 0, AnyWnd owner = default
			) {
			i = 0;
			if (!showInput(out string s, text1, text2, DEdit.Number, editText?.ToString(), default, flags, owner)) return false;
			i = s.ToInt();
			return true;
		}
		
		#endregion ShowInput
		
		#region ShowList
		
		/// <summary>
		/// Shows dialog with a list of command-link buttons, and returns 1-based button index or 0.
		/// </summary>
		/// <returns>1-based index of the selected button. Returns 0 if clicked the <b>X</b> (close window) button or pressed <c>Esc</c>.</returns>
		/// <param name="list">List items (buttons). Can be like <c>"One|Two|Three"</c> or <c>new("One", "Two", "Three")</c> or string array or <b>List</b>. See <see cref="SetButtons"/>.</param>
		/// <param name="text1">Main instruction. Bigger font.</param>
		/// <param name="text2">Text below main instruction.</param>
		/// <param name="flags"></param>
		/// <param name="owner">Owner window. See <see cref="SetOwnerWindow"/>.</param>
		/// <param name="expandedText">Text that the user can show and hide.</param>
		/// <param name="footer">Text at the bottom of the dialog. Icon can be specified like <c>"i|Text"</c>, where <c>i</c> is: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
		/// <param name="title">Title bar text. If omitted, <c>null</c> or <c>""</c>, uses <see cref="options.defaultTitle"/>.</param>
		/// <param name="controls">Can be used to add more controls and later get their values: checkbox, radio buttons, text input.</param>
		/// <param name="defaultButton">id (1-based index) of button that responds to the <c>Enter</c> key.</param>
		/// <param name="x">X position in <see cref="Screen"/>. If default - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
		/// <param name="y">Y position in <see cref="Screen"/>. If default - screen center.</param>
		/// <param name="screen"><see cref="Screen"/>. Examples: <c>screen.ofMouse</c>, <c>screen.index(1)</c>.</param>
		/// <param name="secondsTimeout">If not 0, after this time (seconds) auto-close the dialog and return <see cref="Timeout"/>.</param>
		/// <param name="onLinkClick">Enables hyperlinks in small-font text. A link-clicked event handler function, like with <see cref="show"/>.</param>
		/// <remarks>
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a <b>dialog</b> class instance, set properties and call <b>ShowDialog</b>. Example in <see cref="dialog"/> class help.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// int r = dialog.showList("One|Two|Three", "Example", y: -1, secondsTimeout: 15);
		/// if(r <= 0) return; //X/Esc or timeout
		/// print.it(r);
		/// ]]></code>
		/// </example>
		/// <exception cref="Win32Exception">Failed to show dialog.</exception>
		/// <seealso cref="popupMenu.showSimple"/>
		public static int showList(
			Strings list, string text1 = null, string text2 = null, DFlags flags = 0, AnyWnd owner = default,
			string expandedText = null, string footer = null, string title = null, DControls controls = null,
			int defaultButton = 0, Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0,
			Action<DEventArgs> onLinkClick = null
			) {
			var d = new dialog(text1, text2, default, flags | DFlags.XCancel | DFlags.ExpandDown, 0, owner,
				expandedText, footer, title, controls,
				0, x, y, screen, secondsTimeout, onLinkClick);
			
			d.SetButtons(default, true, list);
			if (defaultButton != 0) d.DefaultButton = defaultButton;
			
			return d.ShowDialog();
		}
		
		#endregion ShowList
		
		#region ShowProgress
		
		/// <summary>
		/// Shows dialog with progress bar.
		/// Creates dialog in new thread and returns without waiting until it is closed.
		/// </summary>
		/// <returns>Variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="Controls"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>; through the <b>Send</b> property you can set progress, modify controls and close the dialog (see example).</returns>
		/// <param name="marquee">Let the progress bar animate without indicating a percent of work done.</param>
		/// <remarks>
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a <b>dialog</b> class instance, set properties and call <see cref="ShowDialogNoWait"/>.
		/// 
		/// More info: <see cref="show"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var pd = dialog.showProgress(false, "Working", buttons: "1 Stop", y: -1);
		/// for(int i = 1; i <= 100; i++) {
		/// 	if(!pd.IsOpen) { print.it(pd.Result); break; } //if the user closed the dialog
		/// 	pd.Send.Progress(i); //don't need this if marquee
		/// 	50.ms(); //do something in the loop
		/// }
		/// pd.Send.Close();
		/// ]]></code>
		/// </example>
		/// <inheritdoc cref="show"/>
		public static dialog showProgress(bool marquee,
			string text1 = null, string text2 = null, string buttons = "0 Cancel", DFlags flags = 0, AnyWnd owner = default,
			string expandedText = null, string footer = null, string title = null, DControls controls = null,
			Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
		) {
			if (buttons.NE()) buttons = "0 Cancel";
			
			var d = new dialog(text1, text2, buttons, flags, 0, owner,
				expandedText, footer, title, controls,
				0, x, y, screen, secondsTimeout, onLinkClick);
			
			if (marquee) d.ProgressBarMarquee = true; else d.ProgressBar = true;
			
			d.ShowDialogNoWait();
			
			if (marquee) d.Send.Message(DNative.TDM.SET_PROGRESS_BAR_MARQUEE, 1);
			
			return d;
		}
		
		#endregion ShowProgress
		
		#region ShowNoWait
		
		/// <summary>
		/// Shows dialog like <see cref="show"/> but does not wait.
		/// Creates dialog in other thread and returns without waiting until it is closed.
		/// </summary>
		/// <returns>Variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="Controls"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>; through the <b>Send</b> property you can modify controls and close the dialog (see example).</returns>
		/// <remarks>
		/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a <b>dialog</b> class instance, set properties and call <see cref="ShowDialogNoWait"/>.
		/// 
		/// More info: <see cref="show"/>.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// dialog.showNoWait("Simple example");
		/// 
		/// var d = dialog.showNoWait("Another example", "text", "1 OK|2 Cancel", y: -1, secondsTimeout: 30);
		/// 2.s(); //do something while the dialog is open
		/// d.Send.ChangeText2("new text", false);
		/// 2.s(); //do something while the dialog is open
		/// d.ThreadWaitForClosed(); print.it(d.Result); //wait until the dialog is closed and get result. Optional, just an example.
		/// ]]></code>
		/// </example>
		/// <inheritdoc cref="show"/>
		public static dialog showNoWait(
			string text1 = null, string text2 = null, Strings buttons = default, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
			string expandedText = null, string footer = null, string title = null, DControls controls = null,
			int defaultButton = 0, Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
			) {
			var d = new dialog(text1, text2, buttons, flags, icon, owner,
				expandedText, footer, title, controls,
				defaultButton, x, y, screen, secondsTimeout, onLinkClick);
			d.ShowDialogNoWait();
			return d;
		}
		
		#endregion ShowNoWait
	}
}

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
		/// Use <msdn>IDI_APPLICATION</msdn> icon from unmanaged resources of this program file or main assembly.
		/// If there are no icons - default program icon.
		/// C# compilers add app icon with this id. The <b>DIcon.App</b> value is = <b>IDI_APPLICATION</b> (32512).
		/// If this program file contains multiple native icons in range <b>DIcon.App</b> to 0xf000, you can specify them like <c>DIcon.App+1</c>.
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
		/// Sets combo box list items used when <see cref="EditType"/> is <b>Combo</b>.
		/// </summary>
		public Strings ComboItems { get; set; }
	}
	
	/// <summary>
	/// Arguments for <see cref="dialog"/> event handlers.
	/// </summary>
	/// <remarks>
	/// To return a non-zero value from the callback function, assign the value to the <b>returnValue</b> field.
	/// More info: <msdn>TaskDialogCallbackProc</msdn>.
	/// </remarks>
	public class DEventArgs : EventArgs {
		internal DEventArgs(dialog obj_, wnd hwnd_, DNative.TDN message_, nint wParam_, nint lParam_) {
			d = obj_; hwnd = hwnd_; message = message_; wParam = wParam_;
			LinkHref = (message_ == DNative.TDN.HYPERLINK_CLICKED) ? Marshal.PtrToStringUni(lParam_) : null;
		}
		
#pragma warning disable 1591 //missing XML documentation
		public dialog d;
		public wnd hwnd;
		/// <summary>Reference: <msdn>task dialog notifications</msdn>.</summary>
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
		/// The event handler can set <b>returnValue</b>=1 to reset this.
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
		/// Reference: <msdn>task dialog messages</msdn>.
		/// <b>NAVIGATE_PAGE</b> currently not supported.
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
		/// Sends message <b>DNative.TDM.NAVIGATE_PAGE</b>.
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
		/// <param name="buttonId">A button id or some other number that will be returned by <b>ShowDialog</b>.</param>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Sends message <b>DNative.TDM.CLICK_BUTTON</b>.
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
		/// Sends message <b>DNative.TDM.ENABLE_BUTTON</b>.
		/// </remarks>
		public void EnableButton(int buttonId, bool enable) {
			Message(DNative.TDM.ENABLE_BUTTON, buttonId, enable ? 1 : 0);
		}
		
		/// <summary>
		/// Sets progress bar value, 0 to 100.
		/// </summary>
		/// <remarks>
		/// Call this method while the dialog is open, eg in an event handler.
		/// Sends message <b>DNative.TDM.SET_PROGRESS_BAR_POS</b>.
		/// </remarks>
		public int Progress(int percent) {
			if (percent < 100) Message(DNative.TDM.SET_PROGRESS_BAR_POS, percent + 1); //workaround for the progress bar control lag. https://stackoverflow.com/questions/5332616/disabling-net-progressbar-animation-when-changing-value
			return Message(DNative.TDM.SET_PROGRESS_BAR_POS, percent);
		}
	}
	
	#region public API
#pragma warning disable 1591 //missing XML documentation
	/// <summary>
	/// Rarely used constants for Windows API used by <see cref="dialog"/>.
	/// </summary>
	/// <remarks>
	/// Constants are in enums. Enum name is constant prefix. Enum members are without prefix. For example for <b>TDM_CLICK_BUTTON</b> use <c>DNative.TDM.CLICK_BUTTON</c>.
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
		/// Constants for <b>DNative.TDM.SET_ELEMENT_TEXT</b> and <b>DNative.TDM.UPDATE_ELEMENT_TEXT</b> messages used with <see cref="dialog"/>.
		/// </summary>
		public enum TDE {
			CONTENT,
			EXPANDED_INFORMATION,
			FOOTER,
			MAIN_INSTRUCTION
		}
		
		/// <summary>
		/// Constants for <b>DNative.TDM.UPDATE_ICON</b> message used with <see cref="dialog"/>.
		/// </summary>
		public enum TDIE {
			ICON_MAIN,
			ICON_FOOTER
		}
	}
	
#pragma warning restore 1591 //missing XML documentation
	#endregion public API
}
