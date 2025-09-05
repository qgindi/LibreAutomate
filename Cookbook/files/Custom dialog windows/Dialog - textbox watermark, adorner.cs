/// Use <see cref="wpfBuilder.Watermark"/>.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

var b = new wpfBuilder("Window").WinSize(250);
b.R.Add<AdornerDecorator>().Child().Add(out TextBox text1).Watermark("Water");
b.R.Add<AdornerDecorator>().Child().Add(out ComboBox combo1).Editable().Watermark(out var adorner, "Snow").Items("Zero|One|Two");
b.R.Add(out TextBox text2).Watermark("Rain"); //without AdornerDecorator. In some kinds of windows may not work (rare).
b.R.AddButton("Change watermark text", _ => { adorner.Text = "Ice"; });
if (!b.ShowDialog()) return;
