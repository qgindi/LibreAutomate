using System.Windows;
using System.Windows.Controls;

wpfBuilder.gridLines = true; //draw grid lines to see columns and rows

var b = new wpfBuilder("Window").WinSize(400, 200);
b.Columns(0, 100, -1); //3 columns. Widths: first - auto, second - 100, third - what remains.
b.R.Add("Text", out TextBox _).Add(out CheckBox _, "Check 1"); //row with 3 controls: Label, TextBox, CheckBox
b.R.Add("Longer text", out TextBox _).Add(out CheckBox _, "Check 2"); //row with 3 controls too
b.Row(-1).Add(out TextBox _).Span(2).Multiline(); //row with 1 control that spans 2 columns. This row will be resized vertically when resizing the window. Window height must be specified.
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;

/// The default window layout is a 2-column grid (table). To set columns use <see cref="wpfBuilder.Columns"/>.
/// 
/// Column width can be of 3 types:
/// - If > 0 - width in WPF units (1/96 inch).
/// - If 0 - auto (of the widest control). Often used for labels.
/// - If < 0 - fraction of the remaining space. Aka <i>star-sized<>.

/// Default columns are 0, -1. The first column (auto-sized) is intended for labels; the second fills the remaining space.
///
/// Function <see cref="wpfBuilder.Row"/> starts a new row with specified height. Function <b>R<> starts an auto-sized row, same as <q>Row(0)<>.

/// Row height can be of 3 types:
/// - If > 0 - height in WPF units (1/96 inch).
/// - If 0 - auto (of the tallest control). It is default.
/// - If < 0 - fraction of the remaining space.

/// If there are star-sized columns or rows, need to set container width or height (<b>WinSize<>, <b>Size<>, etc).

/// Example of multiple star-sized columns and rows.

var b2 = new wpfBuilder("Window").WinSize(400, 400);
b2.Columns(-25, -75); //25% and 75%
b2.Row(-25).Add(out TextBox _).Add(out TextBox _); //25%
b2.Row(-75).Add(out TextBox _).Add(out TextBox _); //75%
b2.R.AddOkCancel();
b2.End();
if (!b2.ShowDialog()) return;

/// Parameters of functions <b>Columns<> and <b>Row<> are of type <see cref="WBGridLength"/>. It allows to specify min/max width/height too.

var b3 = new wpfBuilder("Window").WinSize(400, 400);
b3.Columns((-25, 100..), -75); //>= 100
b3.Row(-25).Add(out TextBox _).Add(out TextBox _);
b3.Row((-75, ..200)).Add(out TextBox _).Add(out TextBox _); //<= 200
b3.R.AddOkCancel();
b3.End();
if (!b3.ShowDialog()) return;

/// An element spans 1 column. If it's the last in the row, it also spans the remaining columns. To change it use <see cref="wpfBuilder.Span"/>; it sets how many columns spans the last added element. See the first example.

/// An element spans 1 row. Use <see cref="wpfBuilder.SpanRows"/> to set how many rows spans the last added element.
///
/// An element is added in the next available cell. To add one or more empty cells, use <see cref="wpfBuilder.Skip"/>; also use it to avoid overlapping with elements that span multiple rows (<b>SpanRows<>).
