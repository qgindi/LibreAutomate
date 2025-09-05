using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace Au.Controls;

/// <summary>
/// Replaces bool? IsChecked with bool IsChecked.
/// Adds events/overrides for "checked state changed".
/// </summary>
public class KCheckBox : CheckBox {
	public new bool IsChecked {
		get => base.IsChecked == true;
		set => base.IsChecked = value;
	}

	protected override void OnChecked(RoutedEventArgs e) {
		base.OnChecked(e);
		OnCheckChanged(e);
	}

	protected override void OnUnchecked(RoutedEventArgs e) {
		base.OnUnchecked(e);
		OnCheckChanged(e);
	}

	protected override void OnIndeterminate(RoutedEventArgs e) {
		base.OnIndeterminate(e);
		OnCheckChanged(e);
	}

	/// <summary>
	/// Raises <see cref="CheckChanged"/> event.
	/// </summary>
	protected virtual void OnCheckChanged(RoutedEventArgs e) {
		CheckChanged?.Invoke(this, e);
	}

	/// <summary>
	/// When check state changed (checked/unchecked/indeterminate).
	/// Can be used to avoid 2-3 event handlers (Checked/Unchecked/Indeterminate).
	/// </summary>
	public event RoutedEventHandler CheckChanged;
}

/// <summary>
/// <see cref="KCheckBox"/> without the box part.
/// </summary>
class KCheckNoBox : KCheckBox {
	static KCheckNoBox() {
		bool contrast = SystemParameters.HighContrast;
		
		var borderFactory = new FrameworkElementFactory(typeof(Border));
		borderFactory.Name = "border";
		borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
		borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(contrast ? 2 : 1));
		borderFactory.SetValue(Border.BorderBrushProperty, SystemColors.ControlLightBrush);
		borderFactory.SetValue(Border.BackgroundProperty, SystemColors.ControlLightLightBrush);
		borderFactory.SetValue(Border.PaddingProperty, new Thickness(4, 0, 4, 1));
		
		var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
		contentPresenter.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, new TemplateBindingExtension(ForegroundProperty));
		contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
		
		borderFactory.AppendChild(contentPresenter);
		
		var template = new ControlTemplate(typeof(KCheckNoBox));
		template.VisualTree = borderFactory;
		
		if (contrast) {
			var t1 = new Trigger { Property = IsCheckedProperty, Value = true };
			t1.Setters.Add(new Setter(Border.BackgroundProperty, SystemColors.HighlightBrush, "border"));
			t1.Setters.Add(new Setter(ForegroundProperty, SystemColors.HighlightTextBrush));
			template.Triggers.Add(t1);
		} else {
			var brushes = GetBrushes();
			
			var t1 = new Trigger { Property = IsCheckedProperty, Value = true };
			t1.Setters.Add(new Setter(Border.BackgroundProperty, brushes.checkedCold, "border"));
			t1.Setters.Add(new Setter(Border.BorderBrushProperty, SystemColors.ControlLightLightBrush, "border"));
			template.Triggers.Add(t1);
			
			var t2 = new Trigger { Property = IsMouseOverProperty, Value = true };
			t2.Setters.Add(new Setter(Border.BackgroundProperty, brushes.uncheckedHot, "border"));
			template.Triggers.Add(t2);
			
			var mt = new MultiTrigger(); //checked hot
			mt.Conditions.Add(new Condition { Property = IsCheckedProperty, Value = true });
			mt.Conditions.Add(new Condition { Property = IsMouseOverProperty, Value = true });
			mt.Setters.Add(new Setter(Border.BackgroundProperty, brushes.checkedHot, "border"));
			template.Triggers.Add(mt);
		}
		
		var style = new Style(typeof(KCheckNoBox));
		style.Setters.Add(new Setter(TemplateProperty, template));
		
		StyleProperty.OverrideMetadata(typeof(KCheckNoBox), new FrameworkPropertyMetadata(style));
	}
	
	static (Brush checkedCold, Brush checkedHot, Brush uncheckedHot) GetBrushes() {
		if (s_checkedCold == null) {
			var c = (Color)new ColorInt(0x92C1E6);
			SolidColorBrush brush1 = new(c), brush2 = new(Color.FromArgb(180, c.R, c.G, c.B)), brush3 = new(Color.FromArgb(80, c.R, c.G, c.B));
			brush1.Freeze();
			brush2.Freeze();
			brush3.Freeze();
			s_checkedHot = brush2;
			s_uncheckedHot = brush3;
			s_checkedCold = brush1;
		}
		return (s_checkedCold, s_checkedHot, s_uncheckedHot);
	}
	static Brush s_checkedCold, s_checkedHot, s_uncheckedHot;
}

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
			//_button.ClickMode = ClickMode.Press; //open on button down. But then Popup.StaysOpen=false does not work. Tried async, but same. //TODO3: replace Popup in KPopupListBox with KPopup.
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
	
	public void Clear(string text = null) {
		c.IsChecked = false;
		t.Text = text;
		_items = null;
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
