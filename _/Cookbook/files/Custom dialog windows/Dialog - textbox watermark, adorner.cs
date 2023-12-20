using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

var b = new wpfBuilder("Window").WinSize(250);
b.R.Add<AdornerDecorator>().Add(out TextBox text1, flags: WBAdd.ChildOfLast).Watermark("Water");
b.R.Add<AdornerDecorator>().Add(out ComboBox combo1, flags: WBAdd.ChildOfLast).Editable().Watermark(out var adorner, "Snow").Items("Zero|One|Two");
b.R.AddButton("Change watermark text", _ => { adorner.Text = "Ice"; });
if (!b.ShowDialog()) return;
