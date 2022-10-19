using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Au.Controls;

/// <summary>
/// Adds some features.
/// </summary>
public class KTextBox : TextBox {
	/// <summary>
	/// Disables horizontal scrollbar when not focused.
	/// </summary>
	public bool Small {
		get => _small;
		set {
			if (_small) throw new InvalidOperationException();
			_small = true;
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		}
	}
	bool _small;

	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		if (_small) HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
		base.OnGotKeyboardFocus(e);
	}

	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		if (_small) HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
		base.OnLostKeyboardFocus(e);
	}
}
