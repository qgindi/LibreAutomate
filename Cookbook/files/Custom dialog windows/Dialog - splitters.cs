/// How to add a splitter:
/// - Need 3 or more columns (for vertical splitter) or rows (for horizontal splitter). Don't use auto-sized columns.
/// - Add a <see cref="GridSplitter"/> control in a middle column or row.
/// - Call <see cref="wpfBuilder.Splitter"/> to set splitter properties; for vertical splitter specify how many rows it spans.
/// - If vertical splitter spans multiple rows, in other rows call <see cref="wpfBuilder.Skip"/> to make space for the splitter.

using System.Windows;
using System.Windows.Controls;

/// Horizontal splitter.

var bh = new wpfBuilder("Window").WinSize(400).Height(400);
bh.Columns(-1, -1);
bh.Row(-1).Add(out TextBox _).Add(out TextBox _); //top row
bh.R.Add<GridSplitter>().Splitter(vertical: false); //add splitter in the middle row
bh.Row(-1).Add(out TextBox _).Add(out TextBox _); //bottom row
bh.R.AddOkCancel();
bh.End();
if (!bh.ShowDialog()) return;

/// Vertical splitter.

var bv = new wpfBuilder("Window").WinSize(400);
bv.Columns(-1, 0, -1); //3 columns (control, splitter, control)
bv.R.Add(out TextBox _) //add control in the left column
	.Add<GridSplitter>().Splitter(vertical: true, 2) //add splitter in the middle column; let it span 2 rows
	.Add(out TextBox _); //add control in the left column
bv.R.Add(out TextBox _) //second row
	.Skip() //make space for the splitter
	.Add(out TextBox _);
bv.R.AddOkCancel();
bv.End();
if (!bv.ShowDialog()) return;

/// Two vertical splitters.

var b2 = new wpfBuilder("Window").WinSize(400);
b2.Columns(-1, 0, -1, 0, -1); //5 columns (control, splitter, control, splitter, control)
b2.R.StartGrid().R.Add("Text1", out TextBox _).R.Add("Text2", out TextBox _).End(); //left panel
b2.Add<GridSplitter>().Splitter(vertical: true);
b2.StartGrid().R.Add("Text1", out TextBox _).R.Add("Text2", out TextBox _).End(); //center panel
b2.Add<GridSplitter>().Splitter(vertical: true);
b2.StartGrid().R.Add("Text1", out TextBox _).R.Add("Text2", out TextBox _).End(); //right panel
b2.R.AddOkCancel();
b2.End();
if (!b2.ShowDialog()) return;

/// Vertical and horizontal.

var bvh = new wpfBuilder("Window").WinSize(400, 400);
bvh.Columns(-1, 0, -1);
bvh.Row(-1).Add(out TextBox _)
	.Add<GridSplitter>().Splitter(vertical: true, 3)
	.Add(out TextBox _);
bvh.R.Add<GridSplitter>().Splitter(vertical: false);
bvh.Row(-1).Add(out TextBox _)
	.Skip()
	.Add(out TextBox _);
bvh.R.AddOkCancel();
bvh.End();
if (!bvh.ShowDialog()) return;

/// Vertical and horizontal. Horizontal in the right side only.

var b3 = new wpfBuilder("Window").WinSize(400, 400);
b3.Options(margin: new(0));
b3.Columns(100, 0, -1);
b3.Row(-1).Add(out TextBox _);
b3.Add<GridSplitter>().Splitter(vertical: true);
b3.StartGrid()
	.Row(-2).Add(out TextBox _)
	.R.Add<GridSplitter>().Splitter(vertical: false)
	.Row(-1).Add(out TextBox _)
	.End();
b3.Options(margin: new(3));
b3.R.AddOkCancel();
b3.End();
if (!b3.ShowDialog()) return;
