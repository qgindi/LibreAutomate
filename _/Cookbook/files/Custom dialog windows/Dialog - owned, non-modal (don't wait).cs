/// Dialog windows shown by <see cref="wpfBuilder.ShowDialog"/> are modal. It means:
/// - The function waits until the dialog is closed.
/// - The function disables the owner window and other WPF windows of that thread.
///
/// This code shows a modal dialog with 1 button. The button shows another dialog which is non-modal and owned by the first dialog.

using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Dialog 1").WinSize(400, 300);
b.R.AddButton("Dialog 2", _ => { _Dialog2(b.Window); });
b.End();
if (!b.ShowDialog()) return;

void _Dialog2(Window owner) {
	var b = new wpfBuilder("Dialog 2").WinSize(300);
	b.R.Add(out TextBox t);
	b.End();
	var w = b.Window;
	w.Owner = owner;
	w.Show();
}
