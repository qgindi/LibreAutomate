/// WPF elements (controls, panels) usually are inside panels of type <b>Grid</b>, <b>StackPanel</b> or <b>DockPanel</b>. These panels automatically set positions of child elements. Element positions and sizes in <b>Grid</b> depend on <+recipe Dialog - columns>columns and rows<>. In auto-sized columns/rows elements have default size or content size.
///
/// To change the size or position of the last added element:
/// - <see cref="wpfBuilder.Size"/> sets its width, height and optionally alignment. As well as <b>Width</b> and <b>Height</b>.
/// - <see cref="wpfBuilder.Margin"/> changes the amount of empty space around it, which can be used to set its position.
/// - <see cref="wpfBuilder.Align"/> changes its alignment inside the grid cell.

using System.Windows;
using System.Windows.Controls;

wpfBuilder.gridLines = true; //draw grid lines to see element positions in cells

var b = new wpfBuilder("Window").WinSize(400);
b.Columns(-1, -1);
b.Margin("0"); //remove margin from the root panel (default is 3)

//first row
b.R.Add(out TextBox _).Size(100, 50, "L"); //width 100, height 50, left-align
b.Add(out TextBox _).Margin(30, 3, 30, 10); //margins left, top, right, bottom

//second row
b.Options(margin: new(1)); //set default margin = 1 at all sides
b.Add(out TextBox _);
b.Add(out TextBox _).Margin("L20 R40"); //margins left, right

//third row
b.Options(margin: new(5, 10, 5, 5));
b.R.AddOkCancel();

b.End();
if (!b.ShowDialog()) return;

/// Default margin is 3 in all sides. To change it use <see cref="wpfBuilder.Options"/>. Default margin of nested panels is 0.

/// To set content position within the element, use <see cref="wpfBuilder.Padding"/> and <see cref="wpfBuilder.AlignContent"/>.

var b2 = new wpfBuilder("Window").WinSize(400);
b2.R.Add("Text", out TextBox _, "Text 1").Padding(10, 4, 10, 6);
b2.R.Add("Text", out TextBox _, "Text 2").AlignContent(HorizontalAlignment.Right);
b2.R.AddOkCancel();
b2.End();
if (!b2.ShowDialog()) return;
