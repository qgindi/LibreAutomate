using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Au.Controls;

namespace Au.Tools;

/// <summary>
/// KCheckBox and KTextBox. Optionally + Button and KPopupListBox.
/// </summary>
public class KCheckTextBox {
	public readonly KCheckBox c;
	public readonly KTextBox t;
	readonly Button _button;
	KPopupListBox _popup;
	object _items; //List<string> or Func<List<string>>

	///
	public KCheckTextBox(KCheckBox c, KTextBox t, Button button = null) {
		this.c = c;
		this.t = t;
		c.Tag = this;
		t.Tag = this;
		t.Small = true;
		_button = button;
		if (_button != null) {
			//_button.ClickMode = ClickMode.Press; //open on button down. But then Popup.StaysOpen=false does not work. Tried async, but same. //SHOULDDO: replace Popup in KPopupListBox with KPopup.
			_button.Click += (_, _) => {
				if (_popup?.IsOpen ?? false) {
					_popup.IsOpen = false;
					return;
				}
				List<string> a = null;
				switch (_items) {
				case List<string> u: a = u; break;
				case Func<List<string>> u: a = u(); break;
				}
				if (a.NE_()) return;
				if (_popup == null) {
					_popup = new KPopupListBox { PlacementTarget = t };
					_popup.OK += o => {
						c.IsChecked = true;
						var s = o as string;
						t.Text = s;
						t.Focus();
					};
				}
				_popup.Control.ItemsSource = null;
				_popup.Control.ItemsSource = a;
				_popup.Control.MinWidth = t.ActualWidth + _button.ActualWidth - 1;
				_popup.IsOpen = true;
			};
		}
	}

	///
	public void Deconstruct(out KCheckBox c, out TextBox t) { c = this.c; t = this.t; }

	/// <summary>
	/// Gets or sets <b>Visibility</b> of controls. If false, <b>Visibility</b> is <b>Collapsed</b>.
	/// </summary>
	public bool Visible {
		get => t.Visibility == Visibility.Visible;
		set {
			var vis = value ? Visibility.Visible : Visibility.Collapsed;
			c.Visibility = vis;
			t.Visibility = vis;
			if (_button != null) _button.Visibility = vis;
		}
	}

	public void Set(bool check, string text) {
		c.IsChecked = check;
		t.Text = text;
	}

	public void Set(bool check, string text, List<string> items) {
		c.IsChecked = check;
		t.Text = text;
		_items = items;
	}

	public void Set(bool check, string text, Func<List<string>> items) {
		c.IsChecked = check;
		t.Text = text;
		_items = items;
	}

	/// <summary>
	/// If checked and visible and text not empty, gets text and returns true. Else sets s=null and returns false.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="emptyToo">If text empty, get "" and return true.</param>
	public bool GetText(out string s, bool emptyToo = false) {
		s = null;
		if (!c.IsChecked || !Visible) return false;
		var v = t.Text;
		if (!emptyToo && v.Length == 0) return false;
		s = v;
		return true;
	}

	public bool CheckIfTextNotEmpty() {
		if (!c.IsChecked && t.Text.Length > 0) { c.IsChecked = true; return true; }
		return false;
	}
}

/// <summary>
/// KCheckBox and ComboBox.
/// </summary>
public class KCheckComboBox {
	public readonly KCheckBox c;
	public readonly ComboBox t;

	///
	public KCheckComboBox(KCheckBox c, ComboBox t) {
		this.c = c;
		this.t = t;
		c.Tag = this;
		t.Tag = this;
	}

	///
	public void Deconstruct(out KCheckBox c, out ComboBox t) { c = this.c; t = this.t; }

	/// <summary>
	/// Gets or sets <b>Visibility</b> of controls. If false, <b>Visibility</b> is <b>Collapsed</b>.
	/// </summary>
	public bool Visible {
		get => t.Visibility == Visibility.Visible;
		set {
			var vis = value ? Visibility.Visible : Visibility.Collapsed;
			c.Visibility = vis;
			t.Visibility = vis;
		}
	}

	/// <summary>
	/// If checked and visible, gets selected item index and returns true. Else sets index=-1 and returns false.
	/// </summary>
	public bool GetIndex(out int index) {
		index = -1;
		if (!c.IsChecked || !Visible) return false;
		index = t.SelectedIndex;
		return index >= 0;
	}

	/// <summary>
	/// If checked and visible, gets selected item text and returns true. Else sets text=null and returns false.
	/// </summary>
	public bool GetText(out string text) {
		if (!GetIndex(out int i)) { text = null; return false; }
		text = t.Items[i] as string;
		return true;
	}
}

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
		if (b.Panel is Grid) b.Padding(new Thickness(0, -1, 0, 1)).Margin(left: 4);
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
		b.Items(items);
		if (index != 0) t.SelectedIndex = index;
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
		b.Height(18).Margin(left: 4);
		if (e is Control) b.Padding(new Thickness(4, 0, 4, 0)); //tested with Button and ComboBox
	}

	/// <summary>
	/// Adds button that can be used in a propertygrid row.
	/// </summary>
	public static void xAddButton(this wpfBuilder b, out Button button, string text, Action<WBButtonClickArgs> click) {
		b.AddButton(out button, text, click);
		_xSetOther(b, button);
	}

	/// <summary>
	/// Adds button that can be used in a propertygrid row.
	/// </summary>
	public static void xAddButton(this wpfBuilder b, string text, Action<WBButtonClickArgs> click) => xAddButton(b, out _, text, click);

	/// <summary>
	/// Adds KCheckBox (<see cref="xAddCheck"/>) and other control (<see cref="xAddOther"/>) in a propertygrid row.
	/// </summary>
	/// <param name="b"></param>
	/// <param name="name">Checkbox text.</param>
	/// <param name="other"></param>
	/// <param name="text">Other control text.</param>
	public static KCheckBox xAddCheckAnd<T>(this wpfBuilder b, string name, out T other, string text = null) where T : FrameworkElement, new() {
		xAddCheck(b, out KCheckBox c, name);
		xAddOther(b, out other, text);
		return c;
	}

	/// <summary>
	/// Adds Border with standard thickness/color and an element in it.
	/// </summary>
	public static Border xAddInBorder<T>(this wpfBuilder b, out T var, string margin = null) where T : FrameworkElement, new() {
		b.Add(out Border c).Border();
		if (margin != null) b.Margin(margin);
		b.Add(out var, flags: WBAdd.ChildOfLast);
		return c;
	}

	/// <summary>
	/// Adds Border with standard thickness/color and an element in it.
	/// </summary>
	public static Border xAddInBorder(this wpfBuilder b, FrameworkElement e, string margin = null) {
		b.Add(out Border c).Border();
		if (margin != null) b.Margin(margin);
		b.Add(e, flags: WBAdd.ChildOfLast);
		return c;
	}

	/// <summary>
	/// Adds KCheckBox with icon like in a toolbar.
	/// </summary>
	public static KCheckBox xAddCheckIcon(this wpfBuilder b, string icon, string tooltip) {
		b.Add(out KCheckBox c, ImageUtil.LoadWpfImageElement(icon)).Tooltip(tooltip);
		c.Style = b.Panel.FindResource(ToolBar.CheckBoxStyleKey) as Style;
		c.Focusable = false;
		//var p = (c.Content as Viewbox).Child as System.Windows.Shapes.Path;
		//c.Checked += (_, _) => p.Fill.Opacity = 1;
		//c.Unchecked += (_, _) => p.Fill.Opacity = 0.3;
		return c;
	}

	/// <summary>
	/// Adds Button with icon like in a toolbar.
	/// </summary>
	public static Button xAddButtonIcon(this wpfBuilder b, string icon, Action<WBButtonClickArgs> click, string tooltip) {
		b.AddButton(out Button c, ImageUtil.LoadWpfImageElement(icon), click).Tooltip(tooltip);
		c.Style = b.Panel.FindResource(ToolBar.ButtonStyleKey) as Style;
		c.Focusable = false;
		return c;
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
	/// Sets header control properties: center, bold, dark gray text.
	/// It can be Label, TextBlock or CheckBox. Not tested others.
	/// </summary>
	public static void xSetHeaderProp(this wpfBuilder b) {
		b.Font(bold: true).Brush(foreground: SystemColors.ControlDarkDarkBrush).Align("C");
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
}
