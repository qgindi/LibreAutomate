using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Au.Controls;

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
	
	void _CloseOK() {
		IsOpen = false;
		if (_lb.SelectedItem is { } v) OK?.Invoke(v);
	}
	
	///
	protected override void OnMouseUp(MouseButtonEventArgs e) {
		switch (e.ChangedButton) {
		case MouseButton.Left: _CloseOK(); break;
		case MouseButton.Middle: IsOpen = false; break;
		}
		base.OnMouseUp(e);
	}
	
	#region steal keyboard from the focused HwndHost-ed control
	
	protected override void OnKeyUp(KeyEventArgs e) { //if OnKeyDown, next time Esc defocuses the HwndHost-ed control
		switch (e.Key) {
		case Key.Enter: _CloseOK(); break;
		case Key.Escape: IsOpen = false; break;
		}
		base.OnKeyUp(e);
	}
	
	protected override void OnOpened(EventArgs e) {
		_lb.Focus();
		base.OnOpened(e);
		_hook = WindowsHook.ThreadKeyboard(_Hook);
	}
	WindowsHook _hook; //also tested ComponentDispatcher.ThreadPreprocessMessage, but it does not steal messages from HwndHost-ed controls
	
	protected override void OnClosed(EventArgs e) {
		_hook?.Dispose();
		_hook = null;
		base.OnClosed(e);
	}
	
	bool _Hook(HookData.ThreadKeyboard k) {
		if (!_lb.IsKeyboardFocusWithin) return false;
		if (PresentationSource.FromVisual(_lb) is not { } inputSource) return false;
		var key = KeyInterop.KeyFromVirtualKey((int)k.key);
		var e = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, key) { RoutedEvent = k.IsUp ? Keyboard.KeyUpEvent : Keyboard.KeyDownEvent };
		_lb.RaiseEvent(e);
		return true;
	}
	
	#endregion
}
