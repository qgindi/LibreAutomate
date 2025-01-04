/// <see cref="wpfBuilder.Brush"/> sets the background and/or text color (or gradient, pattern, etc) of the last added element.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

var b = new wpfBuilder("Window").WinSize(250).Columns(-1);
b.Brush(Brushes.Moccasin); //dialog background color
//b.Font(size: 16); //default font for all controls

//use stock brushes for element background and text colors
b.R.Add(out TextBox _, "Text").Brush(Brushes.LightYellow, Brushes.Blue);

//create custom brushes
var solidBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0xFF, 0));
var gradientBrush = new LinearGradientBrush(Colors.MediumBlue, Colors.MintCream, 0); //for vertical gradient use 90 instead of 0
b.R.AddButton("Button", null).Brush(gradientBrush, solidBrush);

//font
b.R.Add<Label>("Font").Font(size: 16, bold: true);

//remove border
b.R.Add(out TextBox _, "no border").Border(null, 0);

//set border color and thickness
b.R.Add(out TextBox _, "custom border").Border(Brushes.LawnGreen, 2);

//border with rounded corners
b.R.Add<Border>().Border(Brushes.Orange, 1, padding: new Thickness(3, 0, 3, 1), cornerRadius: 3).Align(HorizontalAlignment.Center)
	.Add<Label>("Label", WBAdd.ChildOfLast);

if (!b.ShowDialog()) return;
