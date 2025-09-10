namespace Au;

public partial class dialog {
	#region Show
	
	/// <summary>
	/// Shows dialog.
	/// </summary>
	/// <returns>Selected button id.</returns>
	/// <param name="text1">Heading text.</param>
	/// <param name="text2">Message text. Can be string, or string with links like <c><![CDATA[new("Text <a>link</a> text.", e => { print.it("link"); })]]></c>.</param>
	/// <param name="buttons">
	/// List of button names or <c>"id name"</c>. Examples: <c>"OK|Cancel"</c>, <c>"1 Yes|2 No"</c>, <c><![CDATA["1 &Save|2 Do&n't Save|0 Cancel"]]></c>, <c>["1 One", "2 Two"]</c>.
	/// Can contain common buttons (named <b>OK</b>, <b>Yes</b>, <b>No</b>, <b>Retry</b>, <b>Cancel</b>, <b>Close</b>) and/or custom buttons (any other names).
	/// This first in the list button will be focused (aka *default button*).
	/// More info: <see cref="Buttons"/>.
	/// </param>
	/// <param name="flags"></param>
	/// <param name="icon"></param>
	/// <param name="owner">Owner window. See <see cref="OwnerWindow"/>.</param>
	/// <param name="expandedText">Text in expander control. Can be string, or string with links like <c><![CDATA[new("Text <a>link</a> text.", e => { print.it("link"); })]]></c>.</param>
	/// <param name="footer">Text at the bottom of the dialog. Can be string, or string with links like <c><![CDATA[new("Text <a>link</a> text.", e => { print.it("link"); })]]></c>. Icon can be specified like <c>"i|Text"</c>, where <c>i</c> is: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
	/// <param name="title">Title bar text. If omitted, <c>null</c> or <c>""</c>, uses <see cref="options.defaultTitle"/>.</param>
	/// <param name="controls">Can be used to add more controls and later get their values: checkbox, radio buttons, text input.</param>
	/// <param name="x">X position in screen. Default - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y position in screen. Default - screen center.</param>
	/// <param name="screen">See <see cref="InScreen"/>. Examples: <c>screen.ofMouse</c>, <c>screen.at.left()</c>.</param>
	/// <param name="secondsTimeout">If not 0, after this time (seconds) auto-close the dialog and return <see cref="Timeout"/>.</param>
	/// <remarks>
	/// Tip: Use named arguments. Example: <c>dialog.show("Text", icon: DIcon.Info, title: "Title")</c> .
	/// 
	/// This function allows you to use many dialog features, but not all. Alternatively you can create a <see cref="dialog"/> class instance, set properties and call <see cref="ShowDialog"/>. Example in <see cref="dialog"/> class help.
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
	/// <exception cref="Win32Exception">Failed to show dialog. Unlikely.</exception>
	public static int show(
		string text1 = null, DText text2 = null, Strings buttons = default, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
		DText expandedText = null, DText footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0
		) {
		var d = new dialog(text1, text2, buttons, flags, icon, owner,
			expandedText, footer, title, controls,
			x, y, screen, secondsTimeout);
		return d.ShowDialog();
	}
	
	/// <summary>
	/// Shows dialog with <see cref="DIcon.Info"/> icon.
	/// </summary>
	/// <remarks>Calls <see cref="show"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="show"/>
	public static int showInfo(string text1 = null, DText text2 = null, Strings buttons = default, DFlags flags = 0, AnyWnd owner = default,
		DText expandedText = null, string title = null, int secondsTimeout = 0)
		=> show(text1, text2, buttons, flags, DIcon.Info, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
	
	/// <summary>
	/// Shows dialog with <see cref="DIcon.Warning"/> icon.
	/// </summary>
	/// <remarks>Calls <see cref="show"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="show"/>
	public static int showWarning(string text1 = null, DText text2 = null, Strings buttons = default, DFlags flags = 0, AnyWnd owner = default,
		DText expandedText = null, string title = null, int secondsTimeout = 0)
		=> show(text1, text2, buttons, flags, DIcon.Warning, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
	
	/// <summary>
	/// Shows dialog with <see cref="DIcon.Error"/> icon.
	/// </summary>
	/// <remarks>Calls <see cref="show"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="show"/>
	public static int showError(string text1 = null, DText text2 = null, Strings buttons = default, DFlags flags = 0, AnyWnd owner = default,
		DText expandedText = null, string title = null, int secondsTimeout = 0)
		=> show(text1, text2, buttons, flags, DIcon.Error, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
	
	/// <summary>
	/// Shows dialog with <b>OK</b> and <b>Cancel</b> buttons.
	/// </summary>
	/// <returns><c>true</c> if selected <b>OK</b>.</returns>
	/// <remarks>Calls <see cref="show"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="show"/>
	public static bool showOkCancel(string text1 = null, DText text2 = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
		DText expandedText = null, string title = null, int secondsTimeout = 0)
		=> 1 == show(text1, text2, "OK|Cancel", flags, icon, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
	
	/// <summary>
	/// Shows dialog with <b>Yes</b> and <b>No</b> buttons.
	/// </summary>
	/// <returns><c>true</c> if selected <b>Yes</b>.</returns>
	/// <remarks>Calls <see cref="show"/>.</remarks>
	/// <example></example>
	/// <inheritdoc cref="show"/>
	public static bool showYesNo(string text1 = null, DText text2 = null, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
		DText expandedText = null, string title = null, int secondsTimeout = 0)
		=> 1 == show(text1, text2, "Yes|No", flags, icon, owner, expandedText, title: title, secondsTimeout: secondsTimeout);
	
	#endregion Show
	
	#region ShowInput
	
	/// <summary>
	/// Shows dialog with a text edit field and gets that text.
	/// </summary>
	/// <returns><c>true</c> if selected <b>OK</b> (or a custom button with id 1).</returns>
	/// <param name="s">Variable that receives the text.</param>
	/// <param name="text1">Heading text.</param>
	/// <param name="text2">Message test (above the edit field).</param>
	/// <param name="editType">Edit field type. It can be simple text (default), multiline, number, password or combo box.</param>
	/// <param name="editText">Initial edit field text.</param>
	/// <param name="comboItems">Combo box items used when <i>editType</i> is <see cref="DEdit.Combo"/>.</param>
	/// <param name="flags"></param>
	/// <param name="owner">Owner window. See <see cref="OwnerWindow"/>.</param>
	/// <param name="expandedText">Text in expander control.</param>
	/// <param name="footer">Text at the bottom of the dialog. Icon can be specified like <c>"i|Text"</c>, where <c>i</c> is: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
	/// <param name="title">Title bar text. If omitted, <c>null</c> or <c>""</c>, uses <see cref="options.defaultTitle"/>.</param>
	/// <param name="controls">Can be used to add more controls and later get their values: checkbox, radio buttons.</param>
	/// <param name="x">X position in screen. Default - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y position in screen. Default - screen center.</param>
	/// <param name="screen">See <see cref="InScreen"/>. Examples: <c>screen.ofMouse</c>, <c>screen.index(1)</c>.</param>
	/// <param name="secondsTimeout">If not 0, after this time (seconds) auto-close the dialog and return <see cref="Timeout"/>.</param>
	/// <param name="buttons">
	/// Buttons. List of names or <c>"id name"</c>. Example: <c>"1 OK|2 Cancel|10 Browse..."</c>. See <see cref="Buttons"/>.
	/// Note: this function returns <c>true</c> only when clicked button with id 1.
	/// Usually custom buttons are used with <i>onButtonClick</i> function, which for example can get button id or disable closing the dialog.
	/// </param>
	/// <param name="onButtonClick">A button-clicked event handler function. See examples.</param>
	/// <remarks>
	/// This function allows you to use many dialog features, but not all. Alternatively you can create a <see cref="dialog"/> class instance, call <see cref="Edit"/> or use the <i>controls</i> parameter, set other properties and call <see cref="ShowDialog"/>.
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
		string text1 = null, DText text2 = null,
		DEdit editType = DEdit.Text, string editText = null, Strings comboItems = default,
		DFlags flags = 0, AnyWnd owner = default,
		DText expandedText = null, DText footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0,
		string buttons = "1 OK|2 Cancel", Action<DEventArgs> onButtonClick = null
		) {
		if (buttons.NE()) buttons = "1 OK|2 Cancel";
		var d = new dialog(text1, text2, buttons, flags, 0, owner, expandedText, footer, title, controls, x, y, screen, secondsTimeout);
		
		d.Edit(editType != 0 ? editType : DEdit.Text, editText, comboItems);
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
	/// <param name="text1">Heading text.</param>
	/// <param name="text2">Message text (above the edit field).</param>
	/// <param name="editText">Initial edit field text.</param>
	/// <param name="flags"></param>
	/// <param name="owner">Owner window. See <see cref="OwnerWindow"/>.</param>
	/// <remarks>
	/// Calls <see cref="showInput"/> and converts string to <c>int</c>.
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
		string text1 = null, DText text2 = null, int? editText = null,
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
	/// <param name="list">List items (buttons). Can be like <c>"One|Two|Three"</c> or <c>new("One", "Two", "Three")</c> or string array or <c>List</c>. See <see cref="Buttons"/>.</param>
	/// <param name="text1">Heading text.</param>
	/// <param name="text2">Message text.</param>
	/// <param name="flags"></param>
	/// <param name="owner">Owner window. See <see cref="OwnerWindow"/>.</param>
	/// <param name="expandedText">Text in expander control.</param>
	/// <param name="footer">Text at the bottom of the dialog. Icon can be specified like <c>"i|Text"</c>, where <c>i</c> is: <c>x</c> error, <c>!</c> warning, <c>i</c> info, <c>v</c> shield, <c>a</c> app.</param>
	/// <param name="title">Title bar text. If omitted, <c>null</c> or <c>""</c>, uses <see cref="options.defaultTitle"/>.</param>
	/// <param name="controls">Can be used to add more controls and later get their values: checkbox, radio buttons, text input.</param>
	/// <param name="x">X position in screen. Default - screen center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y position in screen. Default - screen center.</param>
	/// <param name="screen">See <see cref="InScreen"/>. Examples: <c>screen.ofMouse</c>, <c>screen.index(1)</c>.</param>
	/// <param name="secondsTimeout">If not 0, after this time (seconds) auto-close the dialog and return <see cref="Timeout"/>.</param>
	/// <remarks>
	/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a <see cref="dialog"/> class instance, set properties and call <see cref="ShowDialog"/>. Example in <see cref="dialog"/> class help.
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
		Strings list, string text1 = null, DText text2 = null, DFlags flags = 0, AnyWnd owner = default,
		DText expandedText = null, DText footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0
		) {
		return new dialog(text1, text2, default, flags | DFlags.XCancel | DFlags.ExpandDown, 0, owner,
			expandedText, footer, title, controls,
			x, y, screen, secondsTimeout)
			.ButtonsList(list)
			.ShowDialog();
	}
	
	#endregion ShowList
	
	#region ShowProgress
	
	/// <summary>
	/// Shows dialog with progress bar.
	/// Creates dialog in new thread and returns without waiting until it is closed.
	/// </summary>
	/// <returns>Variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="Controls"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>; through the <c>Send</c> property you can set progress, modify controls and close the dialog (see example).</returns>
	/// <param name="marquee">Let the progress bar animate without indicating a percent of work done.</param>
	/// <remarks>
	/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a <see cref="dialog"/> class instance, set properties and call <see cref="ShowDialogNoWait"/>.
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
		string text1 = null, DText text2 = null, string buttons = "0 Cancel", DFlags flags = 0, AnyWnd owner = default,
		DText expandedText = null, DText footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0
	) {
		if (buttons.NE()) buttons = "0 Cancel";
		
		var d = new dialog(text1, text2, buttons, flags, 0, owner,
			expandedText, footer, title, controls,
			x, y, screen, secondsTimeout);
		
		d.Progress(true, marquee);
		
		d.ShowDialogNoWait();
		
		return d;
	}
	
	#endregion ShowProgress
	
	#region ShowNoWait
	
	/// <summary>
	/// Shows dialog like <see cref="show"/> but does not wait.
	/// Creates dialog in other thread and returns without waiting until it is closed.
	/// </summary>
	/// <returns>Variable that can be used to communicate with the dialog using these methods and properties: <see cref="IsOpen"/>, <see cref="ThreadWaitForClosed"/>, <see cref="Result"/> (when closed), <see cref="Controls"/> (when closed), <see cref="DialogWindow"/>, <see cref="Send"/>; through the <c>Send</c> property you can modify controls and close the dialog (see example).</returns>
	/// <remarks>
	/// This function allows you to use most of the dialog features, but not all. Alternatively you can create a <see cref="dialog"/> class instance, set properties and call <see cref="ShowDialogNoWait"/>.
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
		string text1 = null, DText text2 = null, Strings buttons = default, DFlags flags = 0, DIcon icon = 0, AnyWnd owner = default,
		DText expandedText = null, DText footer = null, string title = null, DControls controls = null,
		Coord x = default, Coord y = default, screen screen = default, int secondsTimeout = 0
		) {
		var d = new dialog(text1, text2, buttons, flags, icon, owner,
			expandedText, footer, title, controls,
			x, y, screen, secondsTimeout);
		d.ShowDialogNoWait();
		return d;
	}
	
	#endregion ShowNoWait
}
