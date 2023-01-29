# Dialog - links, formatted text
To display short static text with formatting, links and images, can be used WPF elements of type <a href='https://www.google.com/search?q=System.Windows.Controls.TextBlock+class'>TextBlock</a>. Function <a href='/api/Au.wpfBuilder.Text.html'>wpfBuilder.Text</a> makes it easier.

```csharp
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
```

