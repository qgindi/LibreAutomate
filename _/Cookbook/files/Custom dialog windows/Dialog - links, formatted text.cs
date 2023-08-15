/// To display short static text with formatting, links and images, can be used WPF elements of type <see cref="TextBlock"/>. Function <see cref="wpfBuilder.Text"/> makes it easier.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

var b = new wpfBuilder("Window").WinSize(400);
b.R.Add(out TextBlock _).Text(
	"Text ",
	"<b>bold", " ",
	"<i>italic", " ",
	"<u>underline", " ",
	"<a>link", () => print.it("click"), " ",
	new Run("color") { Foreground = Brushes.Blue, Background = Brushes.Cornsilk }, " ",
	new Run("font") { FontFamily = new("Consolas"), FontSize = 16 }, ". ",
	ImageUtil.LoadWpfImageElement("*EvaIcons.ImageOutline #73BF00"), "\n",
	"controls", new TextBox() { MinWidth = 100, Height = 20, Margin = new(3) }, new CheckBox() { Content = "Check" }
	);
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;
