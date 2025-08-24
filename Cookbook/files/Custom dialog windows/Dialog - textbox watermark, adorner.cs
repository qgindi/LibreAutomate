/// Use <see cref="wpfBuilder.Watermark"/>.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

var b = new wpfBuilder("Window").WinSize(250);
b.R.Add<AdornerDecorator>().Add(out TextBox text1, flags: WBAdd.ChildOfLast).Watermark("Water");
b.R.Add<AdornerDecorator>().Add(out ComboBox combo1, flags: WBAdd.ChildOfLast).Editable().Watermark(out var adorner, "Snow").Items("Zero|One|Two");
b.R.Add(out TextBox text2).Watermark("Rain"); //without AdornerDecorator. In some kinds of windows may not work (rare).
b.R.AddButton("Change watermark text", _ => { adorner.Text = "Ice"; });
if (!b.ShowDialog()) return;
