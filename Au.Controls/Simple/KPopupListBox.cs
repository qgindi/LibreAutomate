using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Au.Controls;

//TODO3: Esc, Arrows, etc don't work in HwndHost-ed windows, eg Output History.

/// <summary>
/// Simple <see cref="Popup"/> with child <see cref="ListBox"/>.
/// </summary>
/// <remarks>
/// The <see cref="Control"/> property gets the <b>ListBox</b>. Add items to it.
/// Show the popup as usually (set <b>PlacementTarget</b> etc and <b>IsOpen</b>=true).
/// 
/// When an item clicked, closes the popup and fires <see cref="OK"/> event. Also when pressed Enter key when an item is selected.
/// Closes the popup without the event when clicked outside or pressed Esc key.
/// </remarks>
public class KPopupListBox : Popup {
	readonly ListBox _lb;
	
	///
	public KPopupListBox() {
		Child = _lb = new ListBox();
		StaysOpen = false;
	}
	
	/// <summary>
	/// Gets the <b>ListBox</b>.
	/// </summary>
	public ListBox Control => _lb;
	
	/// <summary>
	/// When an item clicked, or pressed Enter key and there is a selected item.
	/// The popup is already closed.
	/// </summary>
	public event Action<object> OK;
	
	///
	protected override void OnOpened(EventArgs e) {
		_lb.Focus();
		base.OnOpened(e);
	}
	
	///
	protected override void OnKeyDown(KeyEventArgs e) {
		switch (e.Key) {
		case Key.Enter: _Close(true, e); break;
		case Key.Escape: _Close(false, e); break;
		}
		base.OnKeyDown(e);
	}
	
	void _Close(bool ok, RoutedEventArgs e) {
		e.Handled = true;
		IsOpen = false;
		PlacementTarget?.Focus();
		
		if (ok) {
			var v = _lb.SelectedItem;
			if (v != null) OK?.Invoke(v);
		}
	}
	
	///
	protected override void OnMouseUp(MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
		case MouseButton.Left: _Close(true, e); break;
		case MouseButton.Middle: _Close(false, e); break;
		}
		base.OnMouseUp(e);
	}
}
