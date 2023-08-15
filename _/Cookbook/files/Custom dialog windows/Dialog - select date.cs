using System.Windows;
using System.Windows.Controls;

process.thisProcessCultureIsInvariant = false;

var b = new wpfBuilder("Window").WinSize(400);
b.R.Add("Text", out DatePicker d1);
d1.SelectedDate = DateTime.Now;
d1.SelectedDateFormat = DatePickerFormat.Long;
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;
var date = d1.SelectedDate ?? DateTime.Now.Date;
print.it(date);
