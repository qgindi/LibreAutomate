//info: the "x" in filename is for DocFX to correctly resolve links (changes file processing order).

#if !DEBUG
namespace Au;

public partial class dialog {
	/// <remarks>This overload is obsolete. For text with links now use <see cref="DText"/> instead of string + <i>onLinkClick</i>.</remarks>
	/// <inheritdoc cref="show(string, DText, Strings, DFlags, DIcon, AnyWnd, DText, DText, string, DControls, Coord, Coord, screen, int)" path="/param"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[OverloadResolutionPriority(-1)]
	public dialog(
		string text1 = null, string text2 = null, Strings buttons = default, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
		string expandedText = null, string footer = null, string title = null, DControls controls = null,
		int defaultButton = 0, Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
		) : this(text1, text2, buttons, flags, icon, owner, expandedText, footer, title, controls, x, y, screen, secondsTimeout) {
		if (defaultButton != 0) Default(defaultButton);
		if (onLinkClick != null) HyperlinkClicked += onLinkClick;
	}
	
	/// <remarks>This overload is obsolete. For text with links now use <see cref="DText"/> instead of string + <i>onLinkClick</i>.</remarks>
	/// <inheritdoc cref="show(string, DText, Strings, DFlags, DIcon, AnyWnd, DText, DText, string, DControls, Coord, Coord, screen, int)"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[OverloadResolutionPriority(-1)]
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
	
	/// <remarks>This overload is obsolete. For text with links now use <see cref="DText"/> instead of string + <i>onLinkClick</i>.</remarks>
	/// <inheritdoc cref="showInput(out string, string, DText, DEdit, string, Strings, DFlags, AnyWnd, DText, DText, string, DControls, Coord, Coord, screen, int, string, Action{DEventArgs})"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[OverloadResolutionPriority(-1)]
	public static bool showInput(out string s,
		string text1 = null, string text2 = null,
		DEdit editType = DEdit.Text, string editText = null, Strings comboItems = default,
		DFlags flags = 0, AnyWnd owner = default,
		string expandedText = null, string footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null,
		string buttons = "1 OK|2 Cancel", Action<DEventArgs> onButtonClick = null
		) {
		if (buttons.NE()) buttons = "1 OK|2 Cancel";
		var d = new dialog(text1, text2, buttons, flags, 0, owner, expandedText, footer, title, controls, 0, x, y, screen, secondsTimeout, onLinkClick);
		
		d.Edit(editType != 0 ? editType : DEdit.Text, editText, comboItems);
		if (onButtonClick != null) d.ButtonClicked += onButtonClick;
		
		bool r = 1 == d.ShowDialog();
		s = r ? d._controls.EditText : null;
		return r;
	}
	
	/// <remarks>This overload is obsolete. For text with links now use <see cref="DText"/> instead of string + <i>onLinkClick</i>.</remarks>
	/// <inheritdoc cref="showList(Strings, string, DText, DFlags, AnyWnd, DText, DText, string, DControls, Coord, Coord, screen, int)"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[OverloadResolutionPriority(-1)]
	public static int showList(
		Strings list, string text1 = null, string text2 = null, DFlags flags = 0, AnyWnd owner = default,
		string expandedText = null, string footer = null, string title = null, DControls controls = null,
		int defaultButton = 0, Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0,
		Action<DEventArgs> onLinkClick = null
		) {
		var d = new dialog(text1, text2, default, flags | DFlags.XCancel | DFlags.ExpandDown, 0, owner,
			expandedText, footer, title, controls,
			defaultButton, x, y, screen, secondsTimeout, onLinkClick);
		d.ButtonsList(list);
		return d.ShowDialog();
	}
	
	/// <remarks>This overload is obsolete. For text with links now use <see cref="DText"/> instead of string + <i>onLinkClick</i>.</remarks>
	/// <inheritdoc cref="showProgress(bool, string, DText, string, DFlags, AnyWnd, DText, DText, string, DControls, Coord, Coord, screen, int)"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[OverloadResolutionPriority(-1)]
	public static dialog showProgress(bool marquee,
		string text1 = null, string text2 = null, string buttons = "0 Cancel", DFlags flags = 0, AnyWnd owner = default,
		string expandedText = null, string footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0, Action<DEventArgs> onLinkClick = null
	) {
		if (buttons.NE()) buttons = "0 Cancel";
		
		var d = new dialog(text1, text2, buttons, flags, 0, owner,
			expandedText, footer, title, controls,
			0, x, y, screen, secondsTimeout, onLinkClick);
		
		d.Progress(true, marquee);
		
		d.ShowDialogNoWait();
		
		return d;
	}
	
	/// <remarks>This overload is obsolete. For text with links now use <see cref="DText"/> instead of string + <i>onLinkClick</i>.</remarks>
	/// <inheritdoc cref="showNoWait(string, DText, Strings, DFlags, DIcon, AnyWnd, DText, DText, string, DControls, Coord, Coord, screen, int)"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[OverloadResolutionPriority(-1)]
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
	
	/// <inheritdoc cref="Title"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetTitleBarText(string title) { Title(title); }
	
	/// <summary>
	/// Sets text.
	/// </summary>
	/// <param name="text1">Heading text.</param>
	/// <param name="text2">Message text.</param>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetText(string text1 = null, string text2 = null) {
		_c.pszMainInstruction = text1;
		_c.pszContent = text2;
	}
	
	///<inheritdoc cref="Icon(DIcon)"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetIcon(DIcon icon) => Icon(icon);

	///<inheritdoc cref="Icon(object)"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetIcon(object icon) => Icon(icon);
	
	///<inheritdoc cref="Buttons"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetButtons(Strings buttons, bool asCommandLinks = false, Strings customButtons = default) {
		Buttons(buttons, asCommandLinks).ButtonsList(customButtons, asCommandLinks);
	}
	
	/// <summary>
	/// Specifies which button responds to the <c>Enter</c> key.
	/// If 0 or not set, auto-selects.
	/// </summary>
	/// <value>Button id.</value>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public int DefaultButton { set { Default(value); } }
	
	///<inheritdoc cref="RadioButtons"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetRadioButtons(Strings buttons, int defaultId = 0) => RadioButtons(buttons, defaultId);
	
	///<inheritdoc cref="Checkbox"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetCheckbox(string text, bool check = false) => Checkbox(text, check);
	
	///<inheritdoc cref="ExpandedText"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetExpandedText(string text, bool showInFooter = false) => ExpandedText(text, showInFooter);
	
	///<inheritdoc cref="Expander"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetExpandControl(bool defaultExpanded, string collapsedText = null, string expandedText = null)
		=> Expander(defaultExpanded, collapsedText, expandedText);
	
	/// <summary>
	/// Adds text and common icon at the bottom of the dialog.
	/// </summary>
	/// <param name="text">Text, optionally preceded by an icon character and <c>|</c>, like <c>"i|Text"</c>. Icons: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetFooter(string text) => _SetFooter(text);
	
	/// <summary>
	/// Adds text and common icon at the bottom of the dialog.
	/// </summary>
	/// <param name="text">Text.</param>
	/// <param name="icon"></param>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetFooter(string text, DIcon icon) => FooterText(text).FooterIcon(icon);
	
	/// <summary>
	/// Adds text and custom icon at the bottom of the dialog.
	/// </summary>
	/// <inheritdoc cref="Icon(object)" path="/param"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetFooter(string text, object icon) => FooterText(text).FooterIcon(icon);
	
	///<inheritdoc cref="EditControl"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetEditControl(DEdit editType, string editText = null, Strings comboItems = default)
		=> Edit(editType, editText, comboItems);
	
	/// <summary>
	/// Makes the dialog wider.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public int Width { set { _c.cxWidth = value / 2; } }
	
	///<inheritdoc cref="OwnerWindow"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetOwnerWindow(AnyWnd owner, bool ownerCenter = false, bool dontDisable = false)
		=> OwnerWindow(owner, ownerCenter, dontDisable);
	
	///<inheritdoc cref="XY"/>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetXY(Coord x, Coord y, bool rawXY = false) => XY(x, y, rawXY);
	
	/// <summary>
	/// Sets a timeout to close the dialog after <i>closeAfterS</i> seconds. Then <see cref="ShowDialog"/> returns <see cref="Timeout"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public void SetTimeout(int closeAfterS, string timeoutActionText = null, bool noInfo = false)
		=> CloseAfter(closeAfterS, timeoutActionText, noInfo);
}
#endif
