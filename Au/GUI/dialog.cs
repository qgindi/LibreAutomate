//rejected: by default show dialog in screen of mouse, like with <c>dialog.options.defaultScreen = screen.ofMouse;</c>.
//	Some Windows etc dialogs do it, and for me it's probably better. Eg Explorer's Properties even is at mouse position (top-left corner).
//rejected: dialog.showCheckboxes. See Cookbook > Dialog - enum check-list, select.

namespace Au;

/// <summary>
/// Standard dialogs to show information or get user input.
/// </summary>
/// <remarks>
/// You can use static functions like <see cref="show"/> (less code) or create class instances.
/// 
/// Uses task dialog API <ms>TaskDialogIndirect</ms>.
/// 
/// Cannot be used in services. Instead use <see cref="System.Windows.Forms.MessageBox.Show"/> with option <c>ServiceNotification</c> or <c>DefaultDesktopOnly</c>, or API <ms>MessageBox</ms> with corresponding flags, or API <ms>WTSSendMessage</ms>.
/// </remarks>
/// <example>
/// Simple examples.
/// <code><![CDATA[
/// dialog.show("Example", "Message.);
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
/// var d = new dialog("Example", "Message.");
/// d.Buttons("1 OK|2 Cancel|3 Custom|4 Custom2", asCommandLinks: true)
/// 	.Icon(DIcon.Warning)
/// 	.ExpandedText("Expanded info.", true)
/// 	.Checkbox("Check")
/// 	.RadioButtons("1 r1|2 r2")
/// 	.CloseAfter(30, "OK");
/// d.ButtonClicked += e => { print.it(e.Button); if(e.Button == 4) e.DontCloseDialog = true; };
/// d.Progress();
/// d.Timer += e => { e.d.Send.Progress(e.TimerTimeMS / 100); };
/// var r = d.ShowDialog();
/// print.it(r, d.Controls.IsChecked, d.Controls.RadioId);
/// switch(r) { case 1: print.it("OK"); break; case dialog.Timeout: print.it("timeout"); break; }
/// ]]></code>
/// </example>
public partial class dialog {
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
		public static bool rtlLayout { get; set; }
		
		/// <summary>
		/// If there is no owner window, let the dialog be always on top of most other windows.
		/// Default <c>true</c>.
		/// </summary>
		/// <seealso cref="DFlags"/>
		public static bool topmostIfNoOwnerWindow { get; set; } = true;
		
		/// <summary>
		/// Show dialogs on this screen when screen is not explicitly specified (<see cref="InScreen"/> or parameter <i>screen</i>) and there is no owner window.
		/// The <see cref="screen"/> must be lazy or empty.
		/// </summary>
		/// <exception cref="ArgumentException"><see cref="screen"/> with <c>Handle</c>. Must be lazy or empty.</exception>
		/// <example>
		/// <code><![CDATA[
		/// dialog.options.defaultScreen = screen.ofActiveWindow;
		/// dialog.options.defaultScreen = screen.ofMouse;
		/// dialog.options.defaultScreen = screen.at.left(lazy: true);
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
		/// If owner window not specified (see <see cref="OwnerWindow"/>), use the active or top window of current thread as owner window (disable it, etc).
		/// </summary>
		public static bool autoOwnerWindow { get; set; }
		
		/// <summary>
		/// Timeout text format string. See <see cref="CloseAfter(int, string, bool)"/>.
		/// </summary>
		/// <remarks>
		/// Default: <c>"{0} s until this dialog closes, unless clicked.\nTimeout action: {1}."</c>.
		/// Use placeholder <c>{0}</c> for seconds (in the first line) and <c>{1}</c> for timeout action (in the second line). 
		/// </remarks>
		public static string timeoutTextFormat { get; set; } = c_defaultTimeoutTextFormat;
		
		internal const string c_defaultTimeoutTextFormat = "{0} s until this dialog closes, unless clicked.\nTimeout action: {1}.";
	}
	
	#endregion static options
	
	TASKDIALOGCONFIG _c;
	DFlags _flags;
	
	dialog(DFlags flags) {
		_c.cbSize = Api.SizeOf(_c);
		_flags = flags;
		RtlLayout = options.rtlLayout;
	}
	
	/// <summary>
	/// Initializes a new <see cref="dialog"/> instance and sets main properties.
	/// </summary>
	/// <inheritdoc cref="show" path="/param"/>
	public dialog(
		string text1 = null, DText text2 = null, Strings buttons = default, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
		DText expandedText = null, DText footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0
		) : this(flags) {
		Text1(text1);
		Text2(text2);
		Icon(icon);
		Buttons(buttons, 0 != (flags & DFlags.CommandLinks));
		if (controls != null) {
			_controls = controls;
			if (controls.Checkbox != null) Checkbox(controls.Checkbox, controls.IsChecked);
			if (controls.RadioButtons.Value != null) RadioButtons(controls.RadioButtons, controls.RadioId);
		}
		OwnerWindow(owner, 0 != (flags & DFlags.CenterOwner));
		XY(x, y, 0 != (flags & DFlags.RawXY));
		InScreen(screen);
		CloseAfter(secondsTimeout);
		ExpandedText(expandedText, 0 != (flags & DFlags.ExpandDown));
		_SetFooter(footer);
		Title(title);
		if (flags.Has(DFlags.Wider)) Wider(700);
		CanBeMinimized = flags.Has(DFlags.MinimizeButton);
		if (flags.Has(DFlags.Topmost)) Topmost = true; else if (flags.Has(DFlags.NoTopmost)) Topmost = false;
	}
	
	#region set properties
	
	void _SetFlag(_TDF flag, bool on) {
		if (on) _c.dwFlags |= flag; else _c.dwFlags &= ~flag;
	}
	
	bool _HasFlag(_TDF flag) {
		return (_c.dwFlags & flag) != 0;
	}
	
	/// <summary>
	/// Sets title bar text.
	/// If not set, will use <see cref="options.defaultTitle"/>.
	/// </summary>
	public dialog Title(string title) {
		_c.pszWindowTitle = title.NE() ? options.defaultTitle : title; //info: if "", API uses "ProcessName.exe".
		return this;
	}
	
	/// <summary>
	/// Sets heading text.
	/// </summary>
	/// <param name="text">Text. Can be <c>null</c>.</param>
	public dialog Text1(string text) {
		_c.pszMainInstruction = text;
		return this;
	}
	
	/// <summary>
	/// Sets message text.
	/// </summary>
	/// <param name="text">Text. Can be string, or string with links like <c><![CDATA[new("Text <a>link</a> text.", e => { print.it("link"); })]]></c>, or <c>null</c>.</param>
	public dialog Text2(DText text) {
		_c.pszContent = _DTextGetText(text, 0);
		return this;
	}
	
	string _DTextGetText(DText t, int caller) {
		if (t is null || t.text.NE()) return null;
		if (t.links.NE_()) return t.text;
		
		(_links ??= [[], [], []])[caller] = t.links; //replace old items in _links
		
		int i = 0;
		return t.text.RxReplace(@"<a\K(?=>.+?</a>)", m => {
			if (i == t.links.Length) return "";
			return $" href=\"{c_guidLink};{caller};{i++}\"";
		});
	}
	const string c_guidLink = "37de6377cf1c43a2a6eb9a7945fca3c0";
	Action<DEventArgs>[][] _links;
	
	bool _DTextLinkClicked(string href) {
		if (_links != null && href is { Length: > 35 } s && s.Starts(c_guidLink) && s.ToInt(out int caller, 33) && (uint)caller < 3 && s.ToInt(out int i, 35) && (uint)i < _links[caller].Length) {
			_links[caller][i]?.Invoke(new(this, _dlg, DNative.TDN.HYPERLINK_CLICKED, 0, 0, 0, null));
			return true;
		}
		return false;
	}
	
	/// <summary>
	/// Sets common icon.
	/// </summary>
	/// <remarks>
	/// The value also can be a native icon group resource id (cast to <see cref="DIcon"/>), in range 1 to 0xf000.
	/// </remarks>
	public dialog Icon(DIcon icon) {
		_c.hMainIcon = (nint)icon;
		_iconGC = null;
		return this;
	}
	
	/// <summary>
	/// Sets custom icon.
	/// </summary>
	/// <param name="icon">Can be:
	/// <br/>• <see cref="icon"/>.
	/// <br/>• <see cref="System.Drawing.Icon"/>.
	/// <br/>• <c>IntPtr</c> - native icon handle.
	/// <br/>• <see cref="System.Drawing.Bitmap"/>.
	/// <br/>• string - XAML image, eg copied from the <b>Icons</b> tool. See <see cref="ImageUtil.LoadGdipBitmapFromXaml"/>.
	/// </param>
	/// <remarks>
	/// The icon should be of logical size 32 or 16.
	/// </remarks>
	public dialog Icon(object icon) {
		_iconGC = icon; //will set when showing dialog, because may need screen DPI
		_c.hMainIcon = 0;
		return this;
		//tested: displays original-size 32 and 16 icons, but shrinks bigger icons to 32.
	}
	object _iconGC; //GC
	
	#region buttons
	
	const int c_idOK = 1;
	const int c_idCancel = 2;
	const int c_idRetry = 4;
	const int c_idYes = 6;
	const int c_idNo = 7;
	const int c_idClose = 8;
	const int c_idTimeout = int.MinValue;
	
	/// <summary>
	/// The return value of <c>ShowX</c> functions on timeout.
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
		/// Later call <c>MarshalFreeButtons</c>.
		/// </summary>
		public unsafe void MarshalButtons(ref TASKDIALOGCONFIG c) {
			c.pButtons = _MarshalButtons(false, out c.cButtons);
			c.pRadioButtons = _MarshalButtons(true, out c.cRadioButtons);
			
			_hasXButton = ((c.dwFlags & _TDF.ALLOW_DIALOG_CANCELLATION) != 0);
		}
		
		/// <summary>
		/// Frees memory allocated by <c>MarshalButtons</c> and sets the <i>c</i> members to <c>null</c>/0.
		/// </summary>
		public unsafe void MarshalFreeButtons(ref TASKDIALOGCONFIG c) {
			MemoryUtil.Free(c.pButtons);
			MemoryUtil.Free(c.pRadioButtons);
			c.pButtons = null; c.pRadioButtons = null;
			c.cButtons = 0; c.cRadioButtons = 0;
		}
		
		unsafe TASKDIALOG_BUTTON* _MarshalButtons(bool radio, out int nButtons) {
			var a = radio ? _radioButtons : _customButtons;
			int n = a == null ? 0 : a.Count;
			nButtons = n;
			if (n == 0) return null;
			int nba = n * sizeof(TASKDIALOG_BUTTON), nb = nba;
			foreach (var v in a) nb += (v.s.Length + 1) * 2;
			var r = (TASKDIALOG_BUTTON*)MemoryUtil.Alloc(nb);
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
	/// Sets buttons.
	/// </summary>
	/// <param name="buttons">
	/// List of button names or <c>"id name"</c>. Examples: <c>"OK|Cancel"</c>, <c>"1 Yes|2 No"</c>, <c><![CDATA["1 &Save|2 Do&n't Save|0 Cancel"]]></c>, <c>["1 One", "2 Two"]</c>.
	/// Can contain common buttons (named <b>OK</b>, <b>Yes</b>, <b>No</b>, <b>Retry</b>, <b>Cancel</b>, <b>Close</b>) and/or custom buttons (any other names).
	/// This first in the list button will be focused (aka *default button*).
	/// More info in Remarks.
	/// </param>
	/// <param name="asCommandLinks">The style of custom buttons. If <c>false</c> - row of classic buttons. If <c>true</c> - column of command-link buttons that can have multiline text.</param>
	/// <remarks>
	/// If buttons not set, the dialog will have <b>OK</b> button, id 1.
	/// 
	/// Missing ids are auto-generated, for example <c>"OK|Cancel|100 Custom1|Custom2"</c> is the same as <c>"1 OK|2 Cancel|100 Custom1|101 Custom2"</c>.
	/// 
	/// The first in the list button is the default button, ie is focused and therefore responds to the <c>Enter</c> key. For example, <c>"2 No|1 Yes"</c> adds <b>Yes</b> and <b>No</b> buttons and makes <b>No</b> default.
	/// 
	/// To create keyboard shortcuts, use <c>&amp;</c> character in custom button labels. Use <c>&amp;&amp;</c> for literal <c>&amp;</c>. Example: <c><![CDATA["1 &Tuesday[]2 T&hursday[]3 Saturday && Sunday"]]></c>.
	/// 
	/// Trims newlines around ids and labels. For example, <c>"\r\n1 One\r\n|\r\n2\r\nTwo\r\n\r\n"</c> is the same as <c>"1 One|2 Two"</c>.
	/// 
	/// There are 6 <i>common buttons</i>: <b>OK</b>, <b>Yes</b>, <b>No</b>, <b>Retry</b>, <b>Cancel</b>, <b>Close</b>. Buttons that have other names are <i>custom buttons</i>.
	/// How common buttons are different:
	/// 1. The button style is not affected by <i>asCommandLinks</i> or <see cref="DFlags.CommandLinks"/>.
	/// 2. They have keyboard shortcuts that cannot be changed. Inserting <c>&amp;</c> in a label makes it a custom button.
	/// 3. Button <b>Cancel</b> can be selected with the <c>Esc</c> key. It also adds <b>X</b> (Close) button in title bar, which selects <b>Cancel</b>.
	/// 4. Always displayed in standard order (eg <b>Yes</b> <b>No</b>, never <b>No</b> <b>Yes</b>). But you can for example use <c>"2 No|1 Yes"</c> to set default button = <b>No</b>.
	/// 5. The displayed button label is localized, ie different when the Windows UI language is not English.
	/// </remarks>
	public dialog Buttons(Strings buttons = default, bool asCommandLinks = false) {
		_btn.buttons = buttons;
		_btn.commandLinks = asCommandLinks;
		return this;
	}
	
	/// <summary>
	/// Sets custom buttons to be displayed as a list.
	/// </summary>
	/// <param name="buttons">
	/// List of button names. Can be string like <c>"One|Two|..."</c> or <c>string[]</c> or <c>List&lt;string&gt;</c>.
	/// Button ids will be 1, 2, ... . Default button will be 1, unless changed with <see cref="Default"/>.
	/// Unlike <see cref="Buttons"/>, this function does not allow to specify button ids; also all specified buttons will be custom buttons, even if named like <c>"OK"</c>.
	/// </param>
	/// <param name="asCommandLinks">The style of custom buttons. If <c>false</c> - row of classic buttons. If <c>true</c> - column of command-link buttons that can have multiline text.</param>
	/// <remarks>
	/// You can call <see cref="Buttons"/> too, to add common buttons (like <b>OK</b>, <b>Cancel</b>); use negative button ids. Both functions set the style (classic or command link) of custom buttons; wins the one called last.
	/// </remarks>
	public dialog ButtonsList(Strings buttons = default, bool asCommandLinks = true) {
		_btn.list = buttons;
		_btn.commandLinks = asCommandLinks;
		return this;
	}
	
	(Strings buttons, Strings list, bool commandLinks, int idDefault) _btn;
	
	/// <summary>
	/// Sets default button. It responds to the <c>Enter</c> key.
	/// </summary>
	/// <param name="id">Button id. If 0 - the first button in the list.</param>
	public dialog Default(int id) {
		_btn.idDefault = id;
		return this;
	}
	
	/// <summary>
	/// Adds radio buttons.
	/// </summary>
	/// <param name="buttons">A list of strings <c>"id text"</c> separated by <c>|</c>, like <c>"1 One|2 Two|3 Three"</c>.</param>
	/// <param name="idDefault">Check the radio button that has this id. If omitted or 0, checks the first. If negative, does not check.</param>
	/// <remarks>
	/// To get selected radio button id after closing the dialog, use <see cref="Controls"/>.
	/// </remarks>
	public dialog RadioButtons(Strings buttons, int idDefault = 0) {
		_controls ??= new();
		_controls.RadioButtons = buttons;
		_controls.RadioId = idDefault;
		return this;
	}
	
	#endregion buttons
	
	/// <summary>
	/// Adds check box (if <i>text</i> is not <c>null</c>/empty).
	/// </summary>
	/// <remarks>
	/// To get check box state after closing the dialog, use <see cref="Controls"/>.
	/// </remarks>
	public dialog Checkbox(string text, bool check = false) {
		_controls ??= new();
		_controls.Checkbox = text;
		_controls.IsChecked = check;
		return this;
	}
	
	/// <summary>
	/// Adds text in expander control.
	/// </summary>
	/// <param name="showInFooter">Show the text at the bottom of the dialog.</param>
	/// <inheritdoc cref="Text2" path="/param"/>
	public dialog ExpandedText(DText text, bool showInFooter = false) {
		string s = _DTextGetText(text, 1);
		_SetFlag(_TDF.EXPAND_FOOTER_AREA, showInFooter);
		_c.pszExpandedInformation = s;
		return this;
	}
	
	/// <summary>
	/// Set properties of the expander control that shows and hides text added by <see cref="ExpandedText"/>.
	/// </summary>
	/// <param name="expand"></param>
	/// <param name="collapsedText"></param>
	/// <param name="expandedText"></param>
	public dialog Expander(bool expand, string collapsedText = null, string expandedText = null) {
		_SetFlag(_TDF.EXPANDED_BY_DEFAULT, expand);
		_c.pszCollapsedControlText = collapsedText;
		_c.pszExpandedControlText = expandedText;
		return this;
	}
	
	/// <summary>
	/// Adds footer text.
	/// </summary>
	/// <inheritdoc cref="Text2" path="/param"/>
	public dialog FooterText(DText text) {
		_footerText = _DTextGetText(text, 2);
		return this;
	}
	string _footerText;
	
	/// <summary>
	/// Adds footer icon.
	/// </summary>
	/// <remarks>
	/// The value also can be a native icon group resource id (cast to <see cref="DIcon"/>), in range 1 to 0xf000.
	/// </remarks>
	public dialog FooterIcon(DIcon icon) {
		_c.hFooterIcon = (nint)icon;
		_iconFooterGC = null;
		return this;
	}
	
	/// <summary>
	/// Sets footer icon.
	/// </summary>
	/// <inheritdoc cref="Icon(object)" path="/param"/>
	/// <remarks>
	/// The icon should be of logical size 16.
	/// </remarks>
	public dialog FooterIcon(object icon) {
		_iconFooterGC = icon; //will set when showing dialog, because may need screen DPI
		_c.hFooterIcon = 0;
		return this;
	}
	object _iconFooterGC; //GC
	
	//Used by ctor. Supports string with icon, like "i|Info".
	void _SetFooter(DText text) {
		var s = _DTextGetText(text, 2);
		if (s?.Eq(1, '|') ?? false) {
			FooterIcon(s[0] switch { 'x' => DIcon.Error, '!' => DIcon.Warning, 'i' => DIcon.Info, 'v' => DIcon.Shield, 'a' => DIcon.App, _ => 0 });
			s = s[2..];
		}
		_footerText = s;
	}
	
	/// <summary>
	/// Adds Edit or ComboBox control.
	/// </summary>
	/// <param name="editType">Control type/style.</param>
	/// <param name="editText">Initial edit field text.</param>
	/// <param name="comboItems">Combo box items used when <i>editType</i> is <see cref="DEdit.Combo"/>.</param>
	/// <remarks>
	/// To get control text after closing the dialog, use <see cref="Controls"/>.
	/// 
	/// Dialogs with an input field cannot have a progress bar.
	/// </remarks>
	public dialog Edit(DEdit editType, string editText = null, Strings comboItems = default) {
		_controls ??= new();
		_controls.EditType = editType;
		_controls.EditText = editText;
		_controls.ComboItems = comboItems;
		return this;
	}
	
	/// <summary>
	/// Makes the dialog wider.
	/// </summary>
	/// <param name="width">
	/// Width of the dialog's client area, in logical pixels. The actual width depends on the screen DPI.
	/// If the value is less than default width, will be used default width.
	/// </param>
	/// <seealso cref="DFlags.Wider"/>
	public dialog Wider(int width) {
		_c.cxWidth = width / 2;
		return this;
	}
	
	/// <summary>
	/// Sets owner window.
	/// </summary>
	/// <param name="owner">Owner window, or one of its child/descendant controls. Can be <see cref="wnd"/>, WPF window or element, winforms window or control. Can be <c>null</c>.</param>
	/// <param name="ownerCenter">Show the dialog in the center of the owner window.</param>
	/// <param name="dontDisable">Don't disable the owner window. If <c>false</c>, disables if it belongs to this thread.</param>
	/// <remarks>
	/// The owner window will be disabled, and this dialog will be on top of it.
	/// This window will be in owner's screen, if screen was not explicitly specified (see <see cref="InScreen"/>). <see cref="dialog.options.defaultScreen"/> is ignored.
	/// </remarks>
	/// <seealso cref="options.autoOwnerWindow"/>
	public dialog OwnerWindow(AnyWnd owner, bool ownerCenter = false, bool dontDisable = false) {
		_c.hwndParent = owner.IsEmpty ? default : owner.Hwnd.Window;
		_SetFlag(_TDF.POSITION_RELATIVE_TO_WINDOW, ownerCenter);
		_enableOwner = dontDisable;
		return this;
	}
	bool _enableOwner;
	
	/// <summary>
	/// Sets dialog position in screen.
	/// </summary>
	/// <param name="x">X position in screen. If <c>default</c> - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y position in screen. If <c>default</c> - screen center.</param>
	/// <param name="rawXY"><i>x y</i> are relative to the primary screen (ignore <see cref="InScreen"/> etc).</param>
	/// <seealso cref="InScreen"/>
	public dialog XY(Coord x, Coord y, bool rawXY = false) {
		_x = x; _y = y;
		_rawXY = rawXY && (!_x.IsEmpty || !_y.IsEmpty);
		return this;
	}
	Coord _x, _y; bool _rawXY;
	
	/// <summary>
	/// Sets the screen (display monitor) where to show the dialog in multi-screen environment.
	/// </summary>
	/// <remarks>
	/// If not set, will be used owner window's screen or <see cref="options.defaultScreen"/>.
	/// More info: <see cref="screen"/>, <see cref="wnd.MoveInScreen"/>.
	/// </remarks>
	public dialog InScreen(screen screen) {
		Screen = screen;
		return this;
	}
	
	/// <summary>
	/// Sets to automatically close the dialog after <i>timeoutS</i> seconds. Then <see cref="ShowDialog"/> returns <see cref="Timeout"/>.
	/// </summary>
	/// <param name="timeoutS">Timeout in seconds.</param>
	/// <param name="timeoutAction">Short text to display what will happen on timeout. For example button name. See <see cref="options.timeoutTextFormat"/>.</param>
	/// <param name="noInfo">Don't display the timeout information in the footer.</param>
	public dialog CloseAfter(int timeoutS, string timeoutAction = null, bool noInfo = false) {
		_timeoutS = timeoutS;
		_timeoutActionText = timeoutAction;
		_timeoutNoInfo = noInfo;
		return this;
	}
	int _timeoutS; bool _timeoutActive, _timeoutNoInfo; string _timeoutActionText;
	
	/// <summary>
	/// Sets progress bar.
	/// </summary>
	/// <param name="show">Whether to show progress bar.</param>
	/// <param name="marquee">Show just an animation that does not indicate a progress.</param>
	/// <remarks>
	/// To start or stop the marquee animation, use code like this when the dialog is visible: <c>d.Send.Message(DNative.TDM.SET_PROGRESS_BAR_MARQUEE, 1);</c>.
	/// To set progress, use <see cref="DSend.Progress(int)"/> when the dialog is visible. Example in <see cref="showProgress"/>.
	/// </remarks>
	public dialog Progress(bool show, bool marquee = false) {
		ProgressBar = show && !marquee;
		ProgressBarMarquee = show && marquee;
		return this;
	}
	
	#endregion set properties
	
	#region old hidden non-fluent set properties
	
	///<inheritdoc cref="InScreen"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public screen Screen { set; get; }
	
	/// <summary>
	/// Show progress bar.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public bool ProgressBar { set; get; }
	
	/// <summary>
	/// Show progress bar that does not indicate which part of the work is already done.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public bool ProgressBarMarquee { set; get; }
	
	/// <summary>
	/// Right-to left layout.
	/// Default = <see cref="dialog.options.rtlLayout"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)] //use options.rtlLayout instead
	public bool RtlLayout { set; get; }
	
	/// <summary>
	/// Add <b>Minimize</b> button to the title bar.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)] //use DFlags.MinimizeButton instead
	public bool CanBeMinimized { set; get; }
	
	/// <summary>
	/// Makes the dialog window topmost or non-topmost.
	/// </summary>
	/// <value>
	/// <c>true</c> - set topmost style when creating the dialog.
	/// <c>false</c> - don't set.
	/// <c>null</c> (default) - topmost if both these are true: no owner window, <see cref="dialog.options.topmostIfNoOwnerWindow"/> is <c>true</c> (default).
	/// </value>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public bool? Topmost { set; get; }
	
	#endregion
	
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
		
		Title(_c.pszWindowTitle); //if not set, sets default
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
		_SetFlag(_TDF.ENABLE_HYPERLINKS, _links != null || HyperlinkClicked != null);
		_SetFlag(_TDF.CALLBACK_TIMER, (_timeoutS > 0 || Timer != null));
		
		_c.dwCommonButtons = _buttons.SetButtons(_btn.buttons, _btn.list);
		_SetFlag(_TDF.USE_COMMAND_LINKS, _btn.commandLinks);
		_c.nDefaultButton = _buttons.MapIdUserToNative(_btn.idDefault != 0 ? _btn.idDefault : _buttons.DefaultButtonUserId);
		
		if (_controls != null) {
			_c.pszVerificationText = _controls.Checkbox;
			_SetFlag(_TDF.VERIFICATION_FLAG_CHECKED, _controls.IsChecked);
			
			_buttons.SetRadioButtons(_controls.RadioButtons);
			_c.nDefaultRadioButton = _controls.RadioId;
			_SetFlag(_TDF.NO_DEFAULT_RADIO_BUTTON, _controls.RadioId < 0);
		}
		
		_timeoutActive = _timeoutS > 0;
		_c.pszFooter = _timeoutActive && !_timeoutNoInfo ? _TimeoutFooterText(_timeoutS) : _footerText;
		
		screen screenForIcons = default;
		if (_iconGC != null) _c.hMainIcon = _IconHandle(_iconGC, false); else if (_c.hMainIcon == 0 && options.useAppIcon) _c.hMainIcon = (nint)DIcon.App;
		if (_iconFooterGC != null) _c.hFooterIcon = _IconHandle(_iconFooterGC, true);
		_SetFlag(_TDF.USE_HICON_MAIN, _c.hMainIcon != 0 && _iconGC != null);
		_SetFlag(_TDF.USE_HICON_FOOTER, _c.hFooterIcon != 0 && _iconFooterGC != null);
		
		IntPtr _IconHandle(object o, bool small) {
			if (o is string s) {
				int k = small ? 16 : 32;
				if (screenForIcons.IsEmpty) screenForIcons = _GetScreenBeforeShow();
				k = Dpi.Scale(k, screenForIcons.Dpi);
				o = ImageUtil.XamlIconToGdipIcon_(s, k);
			}
			return o switch {
				icon a => a.Handle,
				System.Drawing.Icon a => a.Handle,
				System.Drawing.Bitmap a => new icon(a.GetHicon()),
				IntPtr a => a,
				_ => 0
			};
		}
		
		if (_iconGC == null && (long)_c.hMainIcon is >= 1 and < 0xf000) _c.hInstance = icon.GetAppIconModuleHandle_((int)_c.hMainIcon);
		else if (_iconFooterGC == null && (long)_c.hFooterIcon is >= 1 and < 0xf000) _c.hInstance = icon.GetAppIconModuleHandle_((int)_c.hFooterIcon);
		//info: DIcon.App is IDI_APPLICATION (32512).
		//Although MSDN does not mention that IDI_APPLICATION can be used when hInstance is NULL, it works. Even works for many other undocumented system resource ids, eg 100.
		//For App icon we could instead use icon handle, but then the small icon for the title bar and taskbar button can be distorted because shrinked from the big icon. Now extracts small icon from resources.
		
		_c.pfCallback = _CallbackProc;
		
		int rNativeButton = 0, rRadioButton = 0, rIsChecked = 0, hr = 0;
		WindowsHook hook = null;
		
		try {
			_threadIdInShow = Environment.CurrentManagedThreadId;
			
			_buttons.MarshalButtons(ref _c);
			if (_c.pButtons == null) _SetFlag(_TDF.USE_COMMAND_LINKS | _TDF.USE_COMMAND_LINKS_NO_ICON, false); //avoid exception
			
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
	
	//Called twice:
	//	1. Before showing dialog, to get screen while the dialog still isn't the active window if need. On TDN.CREATED screen.ofActiveWindow would be bad.
	//	2. On TDN.CREATED, to move dialog if need.
	void _SetPos(bool before) {
		if (before) _screenForMove = default;
		if (_HasFlag(_TDF.POSITION_RELATIVE_TO_WINDOW)) return;
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
				_screenForMove = Screen;
				if (_screenForMove.IsEmpty && _c.hwndParent.Is0) _screenForMove = options.defaultScreen;
				if (_screenForMove.LazyFunc != null) _screenForMove = _screenForMove.Now;
			} else if (!_x.IsEmpty || !_y.IsEmpty || !_screenForMove.IsEmpty) {
				_dlg.MoveInScreen(_x, _y, _screenForMove);
			}
		} else if (!before) {
			_dlg.Move(_x, _y);
			_dlg.EnsureInScreen();
		}
	}
	screen _screenForMove;
	
	//To get DPI for icons.
	screen _GetScreenBeforeShow() {
		if (!_screenForMove.IsEmpty) return _screenForMove;
		if (Api.GetSystemMetrics(Api.SM_CMONITORS) > 1) {
			if (_HasFlag(_TDF.POSITION_RELATIVE_TO_WINDOW)) return screen.of(_c.hwndParent); //ownerCenter
			if (_flags.Has(DFlags.CenterMouse)) return screen.of(mouse.xy);
			if (_rawXY) return screen.of(Coord.Normalize(_x, _y, centerIfEmpty: true));
			if (!_c.hwndParent.Is0) return screen.of(_c.hwndParent);
		}
		return screen.primary;
	}
	
	int _CallbackProc(wnd w, DNative.TDN message, nint wParam, nint lParam, IntPtr data) {
		Action<DEventArgs> e = null;
		int R = 0, button = 0, timerTime = 0;
		string linkHref = null;
		
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
			else if (ProgressBarMarquee) Send.Message(DNative.TDM.SET_PROGRESS_BAR_MARQUEE, 1);
			
			//if(FlagKeyboardShortcutsVisible) w.Post(Api.WM_UPDATEUISTATE, 0x30002); //rejected. Don't need too many rarely used features.
			
			//fix API bug: dialog window is hidden if process STARTUPINFO specifies hidden window
			timer.after(1, _ => _dlg.ShowL(true)); //use timer because at this time still invisible always
			
			e = Created;
			break;
		case DNative.TDN.TIMER:
			timerTime = (int)wParam;
			if (_timeoutActive) {
				int timeElapsed = timerTime / 1000;
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
			button = _buttons.MapIdNativeToUser((int)wParam);
			break;
		case DNative.TDN.HYPERLINK_CLICKED:
			linkHref = Marshal.PtrToStringUni(lParam);
			if (_DTextLinkClicked(linkHref)) return 0;
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
			var ed = new DEventArgs(this, _dlg, message, wParam, button, timerTime, linkHref);//TODO: test
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
	/// Events other than <see cref="Created"/>, <see cref="Destroyed"/>, <see cref="Timer"/>, <see cref="ButtonClicked"/>, <see cref="HyperlinkClicked"/>, <see cref="HelpF1"/>. See API <ms>TaskDialogCallbackProc</ms>.
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
	/// Gets dialog window handle as <see cref="wnd"/>.
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
				Send.ChangeFooterText(_footerText, false);
			}
			break;
		}
	}
	
	string _TimeoutFooterText(int timeLeft) {
		var format = options.timeoutTextFormat;
		if (format.NE()) return _footerText;
		if (_timeoutActionText.NE()) format = format.Lines()[0];
		using (new StringBuilder_(out var b)) {
			b.AppendFormat(format, timeLeft, _timeoutActionText);
			if (!_footerText.NE()) b.Append('\n').Append(_footerText);
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
	/// Gets edit control handle as <see cref="wnd"/>.
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
}
