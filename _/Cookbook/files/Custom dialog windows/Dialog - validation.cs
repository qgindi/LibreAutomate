/// Use <see cref="wpfBuilder.Validation"/> to prevent closing the dialog with the OK button if some control values are invalid.

using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(400);
b.R.Add("Text", out TextBox text1).Focus()
	.Validation(_ => string.IsNullOrWhiteSpace(text1.Text) ? "Text cannot be empty" : null);
b.R.Add("Number", out TextBox text2).Focus()
	.Validation(e => { return int.TryParse((e as TextBox).Text, out int i) && i >= 0 && i <= 100 ? null : "Number must be an integer number 0-100"; });
b.R.Add("Combo", out ComboBox combo1).Items("Zero|One|Two").Select(-1)
	.Validation(_ => combo1.SelectedIndex < 0 ? "Please select something in Combo" : null);
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;
int count = int.Parse(text2.Text);
print.it(count);
