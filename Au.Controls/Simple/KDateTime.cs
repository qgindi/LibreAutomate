using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CalendarControl = System.Windows.Controls.Calendar;

namespace Au.Controls;

/// <summary>
/// Text box for editing date and time.
/// </summary>
public class KDateTime : TextBox {
	const string c_format = "yyyy-MM-dd HH:mm:ss";
	
	///
	public KDateTime() {
		AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(_MouseLeftButtonUp), handledEventsToo: true);
	}
	
	/// <summary>
	/// <b>Text</b> as <b>DateTime</b>.
	/// </summary>
	/// <value>
	/// Setter sets control text, or clears if null.
	/// Getter returns control text converted to <b>DateTime</b>, or null if fails to convert.
	/// Use <see cref="wpfBuilder.Validation"/> with <see cref="Validation"/> to ensure that <b>Text</b> is valid date-time.
	/// </value>
	public DateTime? Value {
		get => _ParseDate(Text, out var d) ? d : null;
		set { Text = value?.ToString(c_format); }
	}
	
	/// <summary>
	/// Validation function for <see cref="wpfBuilder.Validation"/>.
	/// </summary>
	public static string Validation(FrameworkElement e) => ((KDateTime)e)._Validation();
	
	string _Validation() {
		if (IsEnabled && Visibility == Visibility.Visible) {
			string s = Text;
			if (s.Length == 0) return "Date/time empty";
			if (!_ParseDate(s, out var d)) return "Date/time invalid";
			if (d.Year < ValidationFirstYear) return "Min year " + ValidationFirstYear;
			if (ValidationDateMustBeAfter?.Invoke() is DateTime d2 && d <= d2) return "Must be > " + d2.ToString(c_format);
		}
		return null;
	}
	
	/// <summary>
	/// Validation fails if the control's year is less than this value.
	/// Default 1602. Note: if less than 1601-01-02, user code may not work because can't convert to FILETIME.
	/// </summary>
	public int ValidationFirstYear { get; set; } = 1602;
	
	/// <summary>
	/// Validation fails if the control's date isn't after that of returned by the callback function (if returns not null).
	/// </summary>
	public Func<DateTime?> ValidationDateMustBeAfter { get; set; }
	
	/// <summary>
	/// Selects and spins field from mouse.
	/// </summary>
	protected override void OnMouseWheel(MouseWheelEventArgs e) {
		int pos = _MouseCharPos(e, false);
		if (pos >= 0) {
			int add = e.Delta > 0 ? 1 : -1;
			var mod = Keyboard.Modifiers;
			if (mod is 0 or ModifierKeys.Control) {
				if (mod == ModifierKeys.Control) add *= 10;
				_FieldUpDown(pos, add);
				Focus();
				base.Cursor = Cursors.None;
				e.Handled = true;
			}
		}
		base.OnMouseWheel(e);
	}

	///
	protected override void OnMouseMove(MouseEventArgs e) {
		if (base.Cursor == Cursors.None) base.Cursor = null;
		base.OnMouseMove(e);
	}
	
	///
	protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
		_clickedThis = IsMouseCaptured;
		base.OnPreviewMouseLeftButtonUp(e);
	}
	
	bool _clickedThis;
	
	void _MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
		if (!_clickedThis) return;
		_clickedThis = false;
		if (SelectionLength == 0 && _PartFromPos(Text, _MouseCharPos(e, true), out int part, out var m)) {
			base.Select(m[part].Start, m[part].Length);
			if (part <= 3) _ShowCalendar(part, false);
		}
	}
	
	void _ShowCalendar(int part, bool focusCalendar) {
		if (!_ParseDate(Text, out var dt)) return;
		
		var cis = new Style(typeof(CalendarItem));
		cis.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));
		
		var c = new CalendarControl {
			CalendarItemStyle = cis,
			Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.InstalledUICulture.IetfLanguageTag),
			DisplayMode = part switch { 1 => CalendarMode.Decade, 2 => CalendarMode.Year, _ => CalendarMode.Month },
			DisplayDate = dt,
		};
		//var v = new Viewbox { Child = c, Width = 220,  }; //make font bigger. But then blurred.
		
		_calendarPopup = new Popup() { Child = c, PlacementTarget = this, StaysOpen = false };
		
		_calendarPopup.Closed += (_, _) => {
			if (_calendarPopup.IsKeyboardFocusWithin) Focus();
			_calendarPopup = null;
		};
		
		_calendarPopup.IsOpen = true;
		
		if (_calendarPopup != null) {
			if (focusCalendar) c.Focus();
			
			c.PreviewMouseLeftButtonUp += (sender, e) => {
				if (c.DisplayMode == CalendarMode.Month && e.OriginalSource is UIElement k && k.FindVisualAncestor<CalendarDayButton>(true, sender, false) is { } cdb) {
					_CloseCalendar(c);
				}
			};
			
			c.PreviewKeyDown += (_, e) => {
				if (e.Key is Key.Enter && c.DisplayMode == CalendarMode.Month) e.Handled = _CloseCalendar(c);
				if (e.Key is Key.Escape) e.Handled = _CloseCalendar();
			};
			
			_calendarPopup.PreviewMouseWheel += (_, e) => {
				if (!c.RectInScreen().Contains(mouse.xy)) _calendarPopup.IsOpen = false;
			};
		}
	}
	
	Popup _calendarPopup;
	
	bool _CloseCalendar(CalendarControl c = null) {
		bool r = _calendarPopup?.IsOpen == true;
		if (r) {
			if (c != null) {
				var d = c.SelectedDate ?? c.DisplayDate;
				var s = Text;
				if (_ParseRX(s, out var m)) {
					Text = d.ToString("yyyy-MM-dd") + s[m[3].End..];
					Select(m[4].Start, m[4].Length);
				}
			}
			_calendarPopup.IsOpen = false;
		}
		return r;
	}
	
	/// <summary>
	/// Selects adjacent field on Left/Right/space. Spins current field on Up/Down. Opens calendar on Alt+Down. Closes calendar.
	/// </summary>
	protected override void OnPreviewKeyDown(KeyEventArgs e) {
		e.Handled = _CloseCalendar() && e.Key is Key.Escape or Key.Enter;
		
		if (e.Key is Key.Left or Key.Right or Key.Down or Key.Up) {
			var mod = Keyboard.Modifiers;
			if (mod is 0 or ModifierKeys.Control) {
				var s = Text;
				int pos = CaretIndex;
				if (_PartFromPos(s, pos, out int part, out var m)) {
					if (e.Key is Key.Left or Key.Right) {
						part += e.Key == Key.Right ? 1 : -1;
						if (part == 0) part = 6; else if (part == 7) part = 1;
						base.Select(m[part].Start, m[part].Length);
					} else {
						int add = e.Key is Key.Up ? 1 : -1;
						if (mod == ModifierKeys.Control) add *= 10;
						_FieldUpDown(pos, add);
					}
					e.Handled = true;
				}
			}
		} else if (e.Key is Key.Space) { //no OnPreviewTextInput
			e.Handled = _OnPreviewTextInput(' ');
		} else if (e.SystemKey == Key.Down) {
			e.Handled = true;
			if (_PartFromPos(Text, CaretIndex, out int part, out var m)) {
				if (part <= 3) {
					base.Select(m[part].Start, m[part].Length);
					_ShowCalendar(part, true);
				}
			}
		}
		
		if (!e.Handled) base.OnPreviewKeyDown(e);
	}
	
	/// <summary>
	/// Limits the number of digits in current field. Selects adjacent field on -/:. Blocks other characters.
	/// </summary>
	protected override void OnPreviewTextInput(TextCompositionEventArgs e) {
		if (e.Text is [(>= '0' and <= '9') or '-' or ':' or ' '] s) e.Handled = _OnPreviewTextInput(s[0]);
		else e.Handled = true;
		base.OnPreviewTextInput(e);
	}
	
	bool _OnPreviewTextInput(char c) {
		int pos = CaretIndex;
		if (_PartFromPos(Text, pos, out int part, out var m)) {
			if (c is >= '0' and <= '9') {
				int k = part == 1 ? 4 : 2;
				if (m[part].Length == k && SelectionLength == 0) {
					Select(m[part].Start, m[part].Length);
				}
			} else {
				if (++part < 7) Select(m[part].Start, m[part].Length);
				return true;
			}
		}
		return false;
	}
	
	void _FieldUpDown(int pos, int add) {
		var s = Text;
		if (!_PartFromPos(s, pos, out int part, out _)) return;
		if (!_ParseDate(s, out var d)) return;
		try {
			switch (part) {
			case 1: d = d.AddYears(add); break;
			case 2: d = d.AddMonths(add); break;
			case 3: d = d.AddDays(add); break;
			case 4: d = d.AddHours(add); break;
			case 5: d = d.AddMinutes(add); break;
			case 6: d = d.Add(TimeSpan.FromSeconds(add)); break;
			}
			Text = s = d.ToString(c_format);
			if (_ParseRX(s, out var m)) {
				base.Select(m[part].Start, m[part].Length);
			}
		}
		catch { }
	}
	
	bool _ParseRX(string s, out RXMatch m) {
		_rx ??= new(@"^(\d{0,4})-(\d{0,2})-(\d{0,2}) +(\d{0,2}):(\d{0,2}):(\d{0,2})$");
		return _rx.Match(s, out m);
	}
	regexp _rx;
	
	bool _PartFromPos(string s, int pos, out int part, out RXMatch m) {
		if (!_ParseRX(s, out m)) { part = 0; return false; }
		part = 6; while (part > 0 && !(pos >= m[part].Start && pos <= m[part].End)) part--;
		return part > 0;
	}
	
	static bool _ParseDate(string s, out DateTime d) {
		return DateTime.TryParseExact(s, "yyyy-M-d H:m:s", null, DateTimeStyles.None, out d);
	}
	
	int _MouseCharPos(MouseEventArgs e, bool snapToText) => base.GetCharacterIndexFromPoint(e.GetPosition(this), snapToText);
}
