using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Numerics;

namespace Au.Controls;

/// <summary>
/// Read-only <b>TextBox</b> that shows a dropdown list of checkboxes and displays the list of checked items as its text.
/// </summary>
public class KCheckDropdownBox : TextBox {
	///
	public KCheckDropdownBox() {
		IsReadOnly = true;
		//TextWrapping = TextWrapping.Wrap;
		Cursor = Cursors.Arrow;
		AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(_MouseLeftButtonUp), handledEventsToo: true);
	}
	
	/// <summary>
	/// Checkbox names. Max 64.
	/// </summary>
	public object[] Checkboxes {
		get => _checkboxes;
		set {
			if (value?.Length > 64) throw new ArgumentException("Max 64.");
			_checkboxes = value;
			Checked = 0;
		}
	}
	object[] _checkboxes;
	
	/// <summary>
	/// Sets or gets bits that correspond to checked checkboxes.
	/// Set this property after setting <see cref="Checkboxes"/>.
	/// </summary>
	/// <exception cref="ArgumentException">The setter called before setting <see cref="Checkboxes"/>.</exception>
	public ulong Checked {
		get => _checked;
		set {
			int n = _checkboxes?.Length ?? 0;
			if (n == 0 && value != 0) throw new ArgumentException("Set Checkboxes before Checked.");
			
			_checked = value;
			
			string s = "";
			if (_checked != 0) {
				StringBuilder b = new();
				string sep = null;
				for (int i = 0; i < n; i++) {
					if (0 != (_checked & 1UL << i)) {
						b.Append(sep).Append(_checkboxes[i]);
						sep = ", ";
					}
				}
				s = b.ToString();
				if (AllText is { } s1) if (BitOperations.TrailingZeroCount(~_checked) >= n) s = string.Format(s1, s);
			}
			Text = s;
		}
	}
	ulong _checked;
	
	/// <summary>
	/// Text of "check all" checkbox, or null (default) to not add the checkbox.
	/// </summary>
	public object AllItem { get; set; }
	
	/// <summary>
	/// Control text when all checked, or null (default) to display the list.
	/// Can contain placeholder {0} for the list.
	/// </summary>
	public string AllText { get; set; }
	
	/// <summary>
	/// Used with <see cref="DropdownSettings"/>.
	/// </summary>
	/// <param name="panel">Panel that will contain checkboxes. Can be <b>WrapPanel</b>, <b>UniformGrid</b> or other simple panel type. Unsupported types: <b>Grid</b>, <b>Canvas</b>. Default: vertical <b>StackPanel</b>.</param>
	/// <param name="fixedWidth">Set the dropdown width = the control width. If false, can be wider.</param>
	/// <param name="noHScroll">Disable horizontal scrolling.</param>
	/// <param name="noVScroll">Disable vertical scrolling.</param>
	public record DDSettings(Panel panel = null, bool fixedWidth = false, bool noHScroll = false, bool noVScroll = false);
	
	/// <summary>
	/// Callback function that can provide the dropdown panel and settings. Called whenever starting to create a dropdown.
	/// </summary>
	public Func<DDSettings> DropdownSettings { get; set; }
	
	/// <summary>
	/// When dropdown initialized, before showing.
	/// </summary>
	public event Action<DropdownEventData> DropdownOpening;
	
	/// <summary>
	/// When dropdown closed.
	/// </summary>
	public event Action<DropdownEventData> DropdownClosed;
	
	/// <summary>
	/// Used with events.
	/// </summary>
	/// <param name="cb">This control.</param>
	/// <param name="popup">The dropdown window.</param>
	/// <param name="checkboxes">Array of checkboxes.</param>
	/// <param name="cancel">True when dropdown closed with <c>Esc</c> key and therefore checkbox states are ignored.</param>
	public record DropdownEventData(KCheckDropdownBox cb, Popup popup, CheckBox[] checkboxes, bool cancel);
	
	///
	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
		_clickedThis = IsMouseCaptured;
		base.OnPreviewMouseLeftButtonUp(e);
	}
	
	bool _clickedThis;
	
	void _MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
		if (!_clickedThis) return;
		_clickedThis = false;
		if (SelectionLength == 0) {
			_ShowDropdown();
		}
	}
	
	void _ShowDropdown() {
		int n = _checkboxes?.Length ?? 0;
		if (n == 0) return;
		
		var ps = DropdownSettings?.Invoke() ?? new();
		
		Panel panel = ps.panel ?? new StackPanel();
		panel.Margin = new(2);
		
		var ac = new CheckBox[n];
		for (int i = 0; i < n; i++) {
			var c = new CheckBox { Content = _checkboxes[i], Margin = new(1) };
			if (0 != (_checked & 1UL << i)) c.IsChecked = true;
			panel.Children.Add(c);
			ac[i] = c;
		}
		var cFirst = ac[0];
		
		if (AllItem is { } cai) {
			cFirst = new CheckBox { Content = cai, Margin = new(1), IsChecked = BitOperations.TrailingZeroCount(~_checked) >= n };
			panel.Children.Insert(0, cFirst);
			cFirst.Checked += (_, _) => _SetAll(true);
			cFirst.Unchecked += (_, _) => _SetAll(false);
			void _SetAll(bool check) {
				foreach (var c in ac) c.IsChecked = check;
			}
		}
		
		var sv = new ScrollViewer {
			Content = panel,
			HorizontalScrollBarVisibility = ps.noHScroll ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ps.noVScroll ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
		};
		
		var border = new Border {
			Child = sv,
			Background = SystemColors.WindowBrush,
			BorderThickness = new(1),
			BorderBrush = SystemColors.ActiveBorderBrush,
		};
		
		_popup = new Popup { Child = border, PlacementTarget = this, StaysOpen = false };
		if (ps.fixedWidth) _popup.Width = ActualWidth; else _popup.MinWidth = ActualWidth;
		
		bool esc = false;
		_popup.Closed += (_, _) => {
			var popup = _popup;
			_popup = null;
			
			if (!esc) {
				ulong r = 0, k = 1;
				foreach (var c in ac) {
					if (c.IsChecked == true) r |= k;
					k <<= 1;
				}
				Checked = r;
			}
			
			if (popup.IsKeyboardFocusWithin) Focus();
			
			DropdownClosed?.Invoke(new(this, popup, ac, esc));
		};
		
		_popup.PreviewKeyDown += (_, e) => {
			if (e.Key == Key.Space) { //workaround for: if StaysOpen false, on Space key disappears if mouse is not in it or its PlacementTarget
				e.Handled = true;
				if (Keyboard.FocusedElement is CheckBox c) c.IsChecked = !c.IsChecked;
			} else if (e.Key is Key.Escape or Key.Enter) {
				e.Handled = true;
				esc = e.Key == Key.Escape;
				_popup.IsOpen = false;
			}
		};
		
		border.MouseLeave += (_, _) => { IsDropdownOpen = false; };
		
		DropdownOpening?.Invoke(new(this, _popup, ac, false));
		
		_popup.IsOpen = true;
		if (cFirst.IsVisible) cFirst.Focus();
	}
	Popup _popup;
	
	/// <summary>
	/// Gets whether the dropdown is open, or opens/closes it.
	/// </summary>
	public bool IsDropdownOpen {
		get => _popup != null;
		set {
			if (value) {
				if (_popup == null) _ShowDropdown();
			} else {
				if (_popup != null) _popup.IsOpen = false;
			}
		}
	}
	
	///
	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		if (e.SystemKey == Key.Down) {
			IsDropdownOpen = true;
		}
		base.OnPreviewKeyDown(e);
	}
	
	///
	protected override void OnPreviewTextInput(TextCompositionEventArgs e) {
		if (IsReadOnly) IsDropdownOpen = true;
		base.OnPreviewTextInput(e);
	}
}
