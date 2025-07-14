using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace Au.Controls;

/// <summary>
/// Extension methods for dialogs.
/// </summary>
public static class KExtWpf {
	/// <summary>
	/// Adds KCheckBox, CheckBox or RadioButton that can be used with TextBox in a propertygrid row. Or alone in a grid or stack row.
	/// </summary>
	/// <param name="b"></param>
	/// <param name="c"></param>
	/// <param name="name">Checkbox text.</param>
	/// <param name="noR">Don't add new row.</param>
	/// <param name="check">Checkbox state.</param>
	public static wpfBuilder xAddCheck<T>(this wpfBuilder b, out T c, string name, bool noR = false, bool check = false) where T : ToggleButton, new() {
		if (!noR && b.Panel is Grid) b.Row(0);
		b.Add<T>(out c, name).Height(18);
		if (check) b.Checked();
		return b;
	}
	
	/// <summary>
	/// Adds KTextBox that can be used with KCheckBox in a propertygrid row. Or alone in a grid or stack row.
	/// Sets multiline with limited height. If in grid, sets padding/margin for propertygrid.
	/// </summary>
	public static wpfBuilder xAddText(this wpfBuilder b, out KTextBox t, string text = null) {
		b.Add(out t, text).Multiline(..55, wrap: TextWrapping.NoWrap);
		if (b.Panel is Grid) b.Padding(new Thickness(0, 0, 0, 1)).Margin(left: 4);
		return b;
	}
	
	/// <summary>
	/// Adds KCheckBox (<see cref="xAddCheck"/>) and multiline KTextBox (<see cref="xAddText"/>) in a propertygrid row.
	/// </summary>
	/// <param name="b"></param>
	/// <param name="name">Checkbox text.</param>
	/// <param name="text">Textbox text.</param>
	/// <param name="noR">Don't add new row.</param>
	/// <param name="check">Checkbox state.</param>
	public static KCheckTextBox xAddCheckText(this wpfBuilder b, string name, string text = null, bool noR = false, bool check = false) {
		xAddCheck(b, out KCheckBox c, name, noR, check);
		var m = c.Margin; if (m.Top <= 1) c.Margin = m with { Top = m.Top + 1 };
		xAddText(b, out var t, text);
		return new(c, t);
	}
	
	/// <summary>
	/// Adds KCheckBox (<see cref="xAddCheck"/>) and multiline KTextBox (<see cref="xAddText"/>) in a propertygrid row.
	/// Also adds ▾ button that shows a drop-down list (see <see cref="KCheckTextBox.Set(bool, string, List{string})"/>).
	/// Unlike ComboBox, text can be multiline and isn't selected when receives focus.
	/// </summary>
	/// <param name="b"></param>
	/// <param name="name">Checkbox text.</param>
	/// <param name="text">Textbox text.</param>
	/// <param name="check">Checkbox state.</param>
	public static KCheckTextBox xAddCheckTextDropdown(this wpfBuilder b, string name, string text = null, bool check = false) {
		xAddCheck(b, out KCheckBox c, name, check: check);
		xAddText(b, out var t, text);
		b.And(14).Add(out Button k, "▾").Padding(new Thickness(0)).Border(); //tested: ok on Win7
		k.Width += 4;
		return new(c, t, k);
	}
	
	/// <summary>
	/// Adds KCheckBox (<see cref="xAddCheck"/>) and readonly ComboBox (<see cref="xAddOther"/>) in a propertygrid row.
	/// </summary>
	/// <param name="b"></param>
	/// <param name="name">Checkbox text.</param>
	/// <param name="items">Combobox items like "One|Two".</param>
	/// <param name="index">Combobox selected index.</param>
	public static KCheckComboBox xAddCheckCombo(this wpfBuilder b, string name, string items, int index = 0) {
		xAddCheck(b, out KCheckBox c, name);
		xAddOther(b, out ComboBox t);
		if (!items.NE()) {
			b.Items(items);
			if (index != 0) t.SelectedIndex = index;
		}
		return new(c, t);
	}
	
	/// <summary>
	/// Adds any control that can be used in a propertygrid row.
	/// </summary>
	public static wpfBuilder xAddOther<T>(this wpfBuilder b, out T other, string text = null, string label = null) where T : FrameworkElement, new() {
		if (label != null) b.xAddOther(out TextBlock _, label);
		b.Add(out other, text);
		_xSetOther(b, other);
		return b;
	}
	
	static void _xSetOther(wpfBuilder b, FrameworkElement e) {
		b.Height(19).Margin(left: 4);
		if (e is Control) b.Padding(e is ComboBox ? new Thickness(4, 1, 4, 0) : new Thickness(4, 0, 4, 0)); //tested with Button and ComboBox
	}
	
	/// <summary>
	/// Adds button that can be used in a propertygrid row.
	/// </summary>
	public static wpfBuilder xAddButton(this wpfBuilder b, out Button button, string text, Action<WBButtonClickArgs> click) {
		b.AddButton(out button, text, click);
		_xSetOther(b, button);
		return b;
	}
	
	/// <summary>
	/// Adds button that can be used in a propertygrid row.
	/// </summary>
	public static wpfBuilder xAddButton(this wpfBuilder b, string text, Action<WBButtonClickArgs> click) => xAddButton(b, out _, text, click);
	
	/// <summary>
	/// Adds KCheckBox (<see cref="xAddCheck"/>) and other control (<see cref="xAddOther"/>) in a propertygrid row.
	/// </summary>
	/// <param name="name">Checkbox text.</param>
	/// <param name="text">Text of other control.</param>
	public static wpfBuilder xAddCheckAnd<T>(this wpfBuilder b, out KCheckBox r, string name, out T other, string text = null) where T : FrameworkElement, new() {
		xAddCheck(b, out r, name);
		xAddOther(b, out other, text);
		return b;
	}
	
	/// <summary>
	/// Adds Border with standard thickness/color and an element in it.
	/// </summary>
	public static Border xAddInBorder<T>(this wpfBuilder b, out T var, string margin = null, Thickness? thickness = null) where T : FrameworkElement, new() {
		b.Add(out Border c).Border(thickness2: thickness);
		if (margin != null) b.Margin(margin);
		b.Add(out var, WBAdd.ChildOfLast);
		return c;
	}
	
	/// <summary>
	/// Adds Border with standard thickness/color and an element in it.
	/// </summary>
	public static Border xAddInBorder(this wpfBuilder b, FrameworkElement e, string margin = null, Thickness? thickness = null) {
		b.Add(out Border c).Border(thickness2: thickness);
		if (margin != null) b.Margin(margin);
		b.Add(e, WBAdd.ChildOfLast);
		return c;
	}
	
	/// <summary>
	/// Adds KCheckBox with icon like in a toolbar.
	/// </summary>
	public static wpfBuilder xAddCheckIcon(this wpfBuilder b, out KCheckBox r, string icon, string tooltip, Action checkChanged = null) {
		b.Add(out r, ImageUtil.LoadWpfImageElement(icon)).Tooltip(tooltip);
		r.Style = b.Panel.FindResource(ToolBar.CheckBoxStyleKey) as Style;
		r.Focusable = false;
		if (checkChanged != null) r.CheckChanged += (_, _) => checkChanged();
		//var p = (r.Content as Viewbox).Child as System.Windows.Shapes.Path;
		//r.Checked += (_, _) => p.Fill.Opacity = 1;
		//r.Unchecked += (_, _) => p.Fill.Opacity = 0.3;
		return b;
	}
	
	/// <summary>
	/// Adds Button with icon like in a toolbar.
	/// </summary>
	public static wpfBuilder xAddButtonIcon(this wpfBuilder b, out Button r, string icon, Action<WBButtonClickArgs> click, string tooltip) {
		b.AddButton(out r, ImageUtil.LoadWpfImageElement(icon), click).Tooltip(tooltip);
		r.Style = b.Panel.FindResource(ToolBar.ButtonStyleKey) as Style;
		r.Focusable = false;
		return b;
	}
	
	/// <summary>
	/// Adds Button with icon like in a toolbar.
	/// </summary>
	public static wpfBuilder xAddButtonIcon(this wpfBuilder b, string icon, Action<WBButtonClickArgs> click, string tooltip)
		=> xAddButtonIcon(b, out _, icon, click, tooltip);
	
	/// <summary>
	/// Adds <b>Grid</b> with two horizontal separators and <b>TextBlock</b>.
	/// Looks almost like <see cref="KGroupBoxSeparator"/>, but is not inside a GroupBox+Panel.
	/// </summary>
	/// <param name="text">String (to add new <b>TextBlock</b>) or <b>FrameworkElement</b> (probably <b>TextBlock</b>).</param>
	public static wpfBuilder xAddGroupSeparator(this wpfBuilder b, object text, bool center = false) {
		b.StartGrid().Columns(center ? -1 : 10, 0, -1);
		b.AddSeparator(vertical: false).Margin(right: 0);
		Debug.Assert(text is string or FrameworkElement);
		if (text is string s) {
			b.Add<TextBlock>(s).Margin(left: 3, right: 4).Font(bold: true);
		} else if (text is FrameworkElement e) {
			b.Add(e);
		}
		b.AddSeparator(vertical: false).Margin(left: 0);
		b.End();
		return b;
	}
	
	public static ToolBar xAddToolBar(this wpfBuilder t, bool vertical = false, bool hideOverflow = false, bool controlBrush = false) {
		var tt = new ToolBarTray { IsLocked = true };
		if (vertical) tt.Orientation = Orientation.Vertical;
		var tb = new ToolBar();
		if (controlBrush) {
			tt.Background = SystemColors.ControlBrush;
			tb.Background = SystemColors.ControlBrush;
		}
		KeyboardNavigation.SetTabNavigation(tb, KeyboardNavigationMode.Once);
		tt.ToolBars.Add(tb);
		if (hideOverflow) tb.HideGripAndOverflow(false);
		t.Add(tt);
		return tb;
	}
	
	/// <summary>
	/// Adds a toolbar button with icon and tooltip.
	/// </summary>
	/// <param name="click">Can be null.</param>
	/// <param name="padding">Set <c>Padding = new(4, 2, 4, 2)</c>.</param>
	public static Button AddButton(this ToolBar t, string icon, Action<Button> click, string tooltip, bool enabled = true, bool padding = true) {
		var c = new Button { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip };
		if (click != null) c.Click += (_, _) => click(c);
		if (padding) c.Padding = new(4, 2, 4, 2);
		if (!enabled) c.IsEnabled = false;
		t.Items.Add(c);
		return c;
	}
	
	/// <summary>
	/// Adds a toolbar checkbox with icon and tooltip.
	/// </summary>
	/// <param name="padding">Set <c>Padding = new(4, 2, 4, 2)</c>.</param>
	public static KCheckBox AddCheckbox(this ToolBar t, string icon, string tooltip, bool enabled = true, bool padding = true) {
		var c = new KCheckBox { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip };
		c.Style = t.FindResource(ToolBar.CheckBoxStyleKey) as Style; //need because this is KCheckBox, not CheckBox
		if (padding) c.Padding = new(4, 2, 4, 2);
		if (!enabled) c.IsEnabled = false;
		t.Items.Add(c);
		return c;
	}
	
	/// <summary>
	/// Adds <b>MouseRightButtonDown</b> event handler which shows a context menu.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="fill">Let it fill the menu.</param>
	public static void xContextMenu(this ButtonBase t, Action<popupMenu> fill) {
		t.MouseRightButtonDown += (_, _) => {
			var m = new popupMenu();
			fill(m);
			m.Show(owner: t);
			//var r = t.RectInScreen();
			//m.Show(xy: new(r.left, r.bottom), excludeRect: r, owner: t);
		};
	}
	
	/// <summary>
	/// Adds <b>Click</b> event handler which shows a drop-down menu.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="fill">Let it fill the menu.</param>
	public static void xDropdownMenu(this ButtonBase t, Action<popupMenu> fill) {
		t.Click += (_, _) => {
			var m = new popupMenu();
			fill(m);
			var r = t.RectInScreen();
			m.Show(xy: new(r.left, r.bottom), excludeRect: r, owner: t);
		};
	}
	
	/// <summary>
	/// Adds ScrollViewer, adds 2-column grid or vertical stack panel in it (StartGrid, StartStack), and calls <c>Options(modifyPadding: false, margin: new(1))</c>.
	/// </summary>
	public static ScrollViewer xStartPropertyGrid(this wpfBuilder b, string margin = null, bool stack = false) {
		b.Add(out ScrollViewer v);
		if (margin != null) b.Margin(margin);
		v.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		v.FocusVisualStyle = null;
		if (stack) b.StartStack(vertical: true, childOfLast: true); else b.StartGrid(childOfLast: true);
		b.Options(modifyPadding: false, margin: new(1));
		return v;
	}
	
	/// <summary>
	/// Ends grid/stack set by <see cref="xStartPropertyGrid"/> and restores options.
	/// </summary>
	/// <param name="b"></param>
	public static void xEndPropertyGrid(this wpfBuilder b) {
		b.Options(modifyPadding: true, margin: new Thickness(3));
		b.End();
	}
	
	/// <summary>
	/// Sets header control properties: center, bold.
	/// It can be Label, TextBlock or CheckBox. Not tested others.
	/// </summary>
	public static void xSetHeaderProp(this wpfBuilder b) {
		//b.Font(bold: true).Brush(foreground: SystemColors.ControlDarkDarkBrush).Align("C");
		b.Font(bold: true).Align("C");
	}
	//public static void xSetHeaderProp(this wpfBuilder b, bool vertical = false) {
	//	b.Font(bold: true).Brush(foreground: SystemColors.ControlDarkDarkBrush);
	//	if (vertical) {
	//		b.Align(y: "C");
	//		b.Last.LayoutTransform = new RotateTransform(270d);
	//	} else {
	//		b.Align("C");
	//	}
	//}
	
	/// <summary>
	/// Adds vertical splitter.
	/// </summary>
	public static void xAddSplitterV(this wpfBuilder b, int span = 1, double thickness = 4) {
		b.Add<GridSplitter2>().Splitter(true, span, thickness);
	}
	
	/// <summary>
	/// Adds horizontal splitter.
	/// </summary>
	public static void xAddSplitterH(this wpfBuilder b, int span = 1, double thickness = 4) {
		b.R.Add<GridSplitter2>().Splitter(false, span, thickness);
	}
	
	/// <summary>
	/// Adds <b>TextBlock</b> with green background, wrapping and some padding, and calls <see cref="wpfBuilder.FormatText"/>.
	/// </summary>
	/// <inheritdoc cref="wpfBuilder.FormatText(wpfBuilder.InterpolatedString)" path="/param"/>
	public static wpfBuilder xAddInfoBlockF(this wpfBuilder t, wpfBuilder.InterpolatedString text, bool scrollViewer = false) {
		var r = t.xAddInfoBlockT(null, scrollViewer);
		t.FormatText(text);
		return r;
	}
	
	/// <summary>
	/// Adds <b>TextBlock</b> with green background, wrapping and some padding.
	/// </summary>
	public static wpfBuilder xAddInfoBlockT(this wpfBuilder t, string text, bool scrollViewer = false) //not overload. Somehow then it is used with $"string" too.
		=> xAddInfoBlockT(t, out _, text, scrollViewer);
	
	/// <summary>
	/// Adds <b>TextBlock</b> with green background, wrapping and some padding.
	/// </summary>
	public static wpfBuilder xAddInfoBlockT(this wpfBuilder t, out TextBlock r, string text = null, bool scrollViewer = false) {
		WBAdd flags = 0;
		if (scrollViewer) {
			t.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
			flags = WBAdd.ChildOfLast;
		}
		return t.Add(out r, text, flags).Wrap().Brush(WpfUtil_.IsHighContrastDark ? 0x2E4D00 : 0xf8fff0).Padding(2, 1, 2, 2);
	}
	
	/// <summary>
	/// Adds icon-button "Help" for a control.
	/// </summary>
	public static wpfBuilder xAddControlHelpButton(this wpfBuilder t, Action<WBButtonClickArgs> click, string tooltip = null) {
		return xAddButtonIcon(t, "*Material.HelpCircleOutline #4080FF|#99CCFF", click, tooltip).Margin(0);
	}
	
	/// <summary>
	/// Adds icon-button "Help" for a control. On click calls <see cref="HelpUtil.AuHelp"/> with <i>helpTopic</i>.
	/// </summary>
	public static wpfBuilder xAddControlHelpButton(this wpfBuilder t, string helpTopic, string tooltip = null)
		=> xAddControlHelpButton(t, _ => HelpUtil.AuHelp(helpTopic), tooltip);
	
	/// <summary>
	/// Adds icon-button "Help" for a dialog window. On click or F1 calls <i>clickF1</i>.
	/// </summary>
	public static wpfBuilder xAddDialogHelpButtonAndF1(this wpfBuilder t, Action clickF1) {
		t.Window.PreviewKeyDown += (_, e) => {
			if (e.Key == Key.F1 && Keyboard.Modifiers == 0) {
				clickF1();
				e.Handled = true;
			}
		};
		return xAddButtonIcon(t, "*Material.HelpBox #4080FF|#99CCFF", _ => clickF1(), "Help (F1)").Width(24).Margin(20);
	}
	
	/// <summary>
	/// Adds icon-button "Help" for a dialog window. On click or F1 calls <see cref="HelpUtil.AuHelp"/> with <i>helpTopic</i>.
	/// </summary>
	public static wpfBuilder xAddDialogHelpButtonAndF1(this wpfBuilder t, string helpTopic)
		=> xAddDialogHelpButtonAndF1(t, () => HelpUtil.AuHelp(helpTopic));
	
	/// <summary>
	/// Can be used like <see cref="wpfBuilder.Validation"/> with hotkey <b>TextBox</b> controls.
	/// </summary>
	public static wpfBuilder xValidateHotkey(this wpfBuilder b, bool errorIfEmpty = false) {
		return b.Validation(e => {
			var s = (e as TextBox).Text;
			if (!errorIfEmpty && s.NE()) return null;
			if (keys.more.parseHotkeyString(s, out _, out _)) return null;
			return "Invalid hotkey";
		});
	}
	
	/// <summary>
	/// Sets binding to show/hide the last added element when the specified <b>CheckBox</b> checked/unchecked.
	/// </summary>
	public static wpfBuilder xBindCheckedVisible(this wpfBuilder t, CheckBox c, bool setLabeledBy = false) {
		t.Bind(FrameworkElement.VisibilityProperty, new Binding("IsChecked") { Source = c, Converter = s_bvc ??= new() });
		if (setLabeledBy) {
			Debug.Assert(!(t.Last is ButtonBase or Label or TextBlock));
			System.Windows.Automation.AutomationProperties.SetLabeledBy(t.Last, c);
		}
		return t;
	}
	static BooleanToVisibilityConverter s_bvc;
	
	/// <summary>
	/// Sets binding to enable/disable the last added element when the specified <b>CheckBox</b> checked/unchecked.
	/// </summary>
	public static wpfBuilder xBindCheckedEnabled(this wpfBuilder t, CheckBox c, bool setLabeledBy = false) {
		t.Bind(FrameworkElement.IsEnabledProperty, new Binding("IsChecked") { Source = c });
		if (setLabeledBy) {
			Debug.Assert(!(t.Last is ButtonBase or Label or TextBlock));
			System.Windows.Automation.AutomationProperties.SetLabeledBy(t.Last, c);
		}
		return t;
	}
	
	/// <summary>
	/// Calls <b>DockPanel.SetDock</b> and <c>t.Children.Add</c>.
	/// </summary>
	public static void xAddDocked(this DockPanel t, UIElement e, Dock dock) {
		DockPanel.SetDock(e, dock);
		t.Children.Add(e);
	}
}
