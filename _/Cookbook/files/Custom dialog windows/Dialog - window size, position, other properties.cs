/// Use:
/// - <see cref="wpfBuilder.WinSize"/> sets window size. Uses WPF units.
/// - <see cref="wpfBuilder.WinXY"/> sets window position. Uses physical pixels.
/// - <see cref="wpfBuilder.WinRect"/> sets window position and size. Uses physical pixels.
/// See also recipe <+recipe>save window placement<>.

using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(300, 300).WinXY(200, 200);
b.Row(-1).Add("Text", out TextBox _).Multiline();
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;

/// To set window state, style and other properties can be used <see cref="wpfBuilder.WinProperties"/>. Or get the <b>Window</b> object and set its properties.

var b2 = new wpfBuilder("Window").WinSize(300, 300);
b2.WinProperties(topmost: true);
b2.Window.ShowInTaskbar = false;
b2.Row(-1).Add("Text", out TextBox _).Multiline();
b2.R.AddOkCancel();
b2.End();
if (!b2.ShowDialog()) return;
