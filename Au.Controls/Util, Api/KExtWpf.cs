using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Au.Controls;

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
/// TextBox for a file or folder path.
/// Supports drag-drop and Browse dialog (right-click).
/// </summary>
public class KTextBoxFile : TextBox {
	bool _canDrop;
	
	///
	protected override void OnPreviewDragEnter(DragEventArgs e) {
		if (e.Handled = _canDrop = e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Link;
		base.OnPreviewDragEnter(e);
	}
	
	///
	protected override void OnPreviewDragOver(DragEventArgs e) {
		if (e.Handled = _canDrop) e.Effects = DragDropEffects.Link;
		base.OnPreviewDragEnter(e);
	}
	
	///
	protected override void OnDrop(DragEventArgs e) {
		if (e.Data.GetData(DataFormats.FileDrop) is string[] a && a.Length > 0) {
			var s = a[0];
			if (s.Ends(".lnk", true) && filesystem.exists(s).File)
				try { s = shortcutFile.getTarget(s); }
				catch (Exception) { }
			Text = s;
		}
		base.OnDrop(e);
	}
	
	///
	protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
		var m = new popupMenu { CheckDontClose = true };
		
		m["Browse..."] = o => {
			var d = new FileOpenSaveDialog(ClientGuid ?? (IsFolder ? "3d4a9167-929a-4346-adfb-e2f03427412c" : "6a7d02c0-7f98-4808-b764-84985ca6e767"));
			if (d.ShowOpen(out string s, owner: this.Hwnd(), selectFolder: IsFolder)) _SetText(s);
		};
		m["folders.EditMe + @\"EditMe\""] = o => _SetText(o.Text);
		m.Submenu("Known folders", m => {
			foreach (var v in typeof(folders).GetProperties()) {
				bool nac = folders.noAutoCreate;
				folders.noAutoCreate = true;
				string path = v.GetValue(null) switch { string k => k, FolderPath k => k.Path, _ => null };
				folders.noAutoCreate = nac;
				if (path == null) continue;
				var name = v.Name;
				m[$"{name}\t{path.Limit(80, middle: true)}"] = o => _SetText(Unexpand ? $"folders.{name} + @\"EditMe\"" : path + (path.Ends('\\') ? "" : "\\"));
			}
		});
		m.Submenu("Environment variables", m => {
			foreach (var (name, path) in Environment.GetEnvironmentVariables().OfType<System.Collections.DictionaryEntry>().Select(o => (o.Key as string, o.Value as string)).OrderBy(o => o.Item1)) {
				if (!pathname.isFullPath(path, orEnvVar: true)) continue;
				int i = path.IndexOf(';'); if (i > 0 && pathname.isFullPath(path.AsSpan(i + 1), orEnvVar: true)) continue; //list of paths
				if (IsFolder && filesystem.exists(path).File) continue;
				m[$"{name}\t{path.Limit(80, middle: true)}"] = o => _SetText(Unexpand ? $"%{name}%\\" : pathname.expand(path));
			}
		});
		m.Submenu("Folder windows", m => {
			foreach (var v in ExplorerFolder.All(onlyFilesystem: true).Select(o => o.GetFolderPath())) {
				m[v.Limit(100, middle: true)] = o => _SetText(v);
			}
		});
		m.Separator();
		m.AddCheck("Unexpand", Unexpand, o => { Unexpand ^= true; UnexpandChanged?.Invoke(); });
		
		void _SetText(string s) {
			Text = s;
			CaretIndex = s.Length;
		}
		
		m.Show(owner: this.Hwnd());
		e.Handled = true;
	}
	
	/// <summary>
	/// true if this control is for a folder path.
	/// </summary>
	public bool IsFolder { get; set; }
	
	/// <summary>
	/// For <see cref="FileOpenSaveDialog"/>.
	/// If not set, uses 2 different GUIDs: one for folders (see <see cref="IsFolder"/>), other for files.
	/// </summary>
	public string ClientGuid { get; set; }
	
	/// <summary>
	/// Let <b>GetCode</b> unexpand path.
	/// Also used/changed by the context menu.
	/// Default true.
	/// </summary>
	public bool Unexpand { get; set; } = true;
	
	public event Action UnexpandChanged;
	
	/// <summary>
	/// Formats code like '@"path"' or 'folders.X + @"relative"' or 'folders.X'.
	/// </summary>
	public string GetCode() {
		var s = Text;
		if (s.Contains('\"') || s.Starts("folders.")) return s;
		if (Unexpand && folders.unexpandPath(s, out var s1, out var s2)) return s2.NE() ? s1 : $"{s1} + @\"{s2}\"";
		return $"@\"{s}\"";
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
	/// Adds <b>Grid</b> with two horizontal separators and <b>TextBlock</b>.
	/// Looks almost like <see cref="KGroupBoxSeparator"/>, but is not inside a GroupBox+Panel.
	/// </summary>
	public static wpfBuilder xAddGroupSeparator(this wpfBuilder b, string text, bool center = false) {
		b.StartGrid().Columns(center ? -1 : 10, 0, -1);
		b.AddSeparator(vertical: false).Margin(right: 0);
		b.Add<TextBlock>(text).Margin(left: 3, right: 4).Font(bold: true).Brush(foreground: KGroupBox.TextColor);
		b.AddSeparator(vertical: false).Margin(left: 0);
		b.End();
		return b;
	}
	
	/// <summary>
	/// Adds a toolbar button with icon and tooltip.
	/// </summary>
	public static Button AddButton(this ToolBar t, string icon, string tooltip, Action<Button> click, bool enabled = true) {
		var c = new Button { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip };
		if (click != null) c.Click += (_, _) => click(c);
		if (!enabled) c.IsEnabled = false;
		t.Items.Add(c);
		return c;
	}
	
	/// <summary>
	/// Adds a toolbar checkbox with icon and tooltip.
	/// </summary>
	public static KCheckBox AddCheckbox(this ToolBar t, string icon, string tooltip, bool enabled = true) {
		var c = new KCheckBox { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip };
		c.Style = t.FindResource(ToolBar.CheckBoxStyleKey) as Style; //need because this is KCheckBox, not CheckBox
		if (!enabled) c.IsEnabled = false;
		t.Items.Add(c);
		return c;
	}
	
	public static ToolBar xAddToolBar(this wpfBuilder t, bool vertical = false, bool hideOverflow = false, bool controlBrush = false) {
		var tt = new ToolBarTray { IsLocked = true };
		if (vertical) tt.Orientation = Orientation.Vertical;
		var tb = new ToolBar();
		if (controlBrush) {
			tt.Background = SystemColors.ControlBrush;
			tb.Background = SystemColors.ControlBrush;
		}
		tt.ToolBars.Add(tb);
		if (hideOverflow) tb.HideGripAndOverflow(false);
		t.Add(tt);
		return tb;
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
	/// Adds <b>TextBlock</b> with green background, wrapping and some padding, and calls <see cref="wpfBuilder.FormatText"/>.
	/// </summary>
	public static wpfBuilder xAddInfoBlockF(this wpfBuilder t, wpfBuilder.InterpolatedString text) {
		var r = t.xAddInfoBlockT(null);
		t.FormatText(text);
		return r;
	}
	
	/// <summary>
	/// Adds <b>TextBlock</b> with green background, wrapping and some padding.
	/// </summary>
	public static wpfBuilder xAddInfoBlockT(this wpfBuilder t, string text) { //not overload. Somehow then it is used with $"string" too.
		return t.Add(out TextBlock r, text).Wrap().Brush(0xf0f8e0).Padding(1, 2, 1, 4);
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
	
	/// <summary>
	/// Sets binding to show/hide the last added element when the specified <b>CheckBox</b> checked/unchecked.
	/// </summary>
	public static wpfBuilder xBindCheckedVisible(this wpfBuilder t, CheckBox c) {
		t.Bind(FrameworkElement.VisibilityProperty, new Binding("IsChecked") { Source = c, Converter = s_bvc ??= new() });
		return t;
	}
	static BooleanToVisibilityConverter s_bvc;
}
