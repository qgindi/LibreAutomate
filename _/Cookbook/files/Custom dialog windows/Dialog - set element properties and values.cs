/// After adding an element, you can call functions to set its properties/state and initial value/content. Some functions can be used with any element, other only with elements of some types.

using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(400);
b.R.Add(out CheckBox check1, "Check").Checked(true).Tooltip("tooltip"); //check the CheckBox, and set tooltip
b.AddButton("Button", null).Disabled(true).Width(70, "R"); //disable the Button, set width and right-align
b.Row(50).Add(out TextBox text1).Multiline().Focus(); //make the TextBox multiline and focused. Its height = row height.
b.Row(50).Add(out TextBox text2, "You can select and copy this read-only text.").Readonly(); //read-only text
b.R.Add(out ComboBox combo1).Editable().Items("Zero|One|Two"); //make the ComboBox editable, add items
b.R.Add(out ListBox list1).Items("Zero", "One", "Two").Select(1); //add ListBox items, select item
b.R.AddButton("Button 1", null).Hidden(true); //hide the button (but its space remains)
b.R.AddButton("Button 2", null).Hidden(null); //collapse the button and remove its space
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;

/// Fill ComboBox when expanding.

var b2 = new wpfBuilder("Window").WinSize(400);
b2.R.Add("Files", out ComboBox combo2).Items(once: false, cb => { foreach (var f in filesystem.enumFiles(@"C:\Test")) cb.Items.Add(f.Name); });
//b2.R.Add("Files", out ComboBox combo2).Items(once: false, cb => { cb.ItemsSource = filesystem.enumFiles(@"C:\Test").Select(o => o.Name); }); //another way
b2.R.AddOkCancel();
b2.End();
if (!b2.ShowDialog()) return;
var file = combo2.SelectedItem as string;
print.it(file);
