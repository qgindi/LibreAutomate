using System.Windows;
using System.Windows.Controls;

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
