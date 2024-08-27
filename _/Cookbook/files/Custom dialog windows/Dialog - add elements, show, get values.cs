/// Class <see cref="wpfBuilder"/> allows to create <google>WPF<> dialog windows quite easily without a visual designer. To start creating a dialog, use snippet <b>wpfDialogSnippet<>: type <.c>wpf<> and select from the list. Or menu <b>File > New > Dialogs<>.

using System.Windows;
using System.Windows.Controls;

/// Add controls, show dialog, get control values.

var b = new wpfBuilder("Window").WinSize(400);
b.R.Add("Text", out TextBox text1).Focus(); //row with 2 controls: Label and TextBox; also makes the TextBox focused
b.R.Add("Combo", out ComboBox combo1).Items("Zero|One|Two"); //row with 2 controls too; also adds 3 items to the ComboBox
b.R.Add(out CheckBox check1, "Check"); //row with 1 CheckBox control
b.R.AddButton("Button", _ => { print.it($"Button clicked. Text: {text1.Text}"); }); //row with 1 Button control. When clicked, print text1 value.
b.R.AddOkCancel(); //row with OK and Cancel buttons that close the dialog
b.End();
if (!b.ShowDialog()) return; //show dialog. Exit if closed not with the OK button.
print.it(text1.Text, combo1.SelectedIndex, check1.IsChecked == true); //get control values

/// WPF elements are of 2 main types: controls (visible elements like button, text box, label) and panels (containers for multiple elements). To add controls use <see cref="wpfBuilder.Add"/> and other <b>AddX<> functions. To add <+recipe>panels<> use <b>StartX<> functions. Use other functions to set properties/content of the last added element.

/// Adding elements is like filling a table - in rows left-to-right and top-to-bottom. Usually don't need to set control position and size; for it you can define <+recipe Dialog - columns>columns and rows<>.

/// Control variables are used to set control type, get value, set properties, etc. They can be declared as <b>Add<> <.k>out<> parameters (like in the first example) or earlier (like in the next example). Also you can create elements and then add.

TextBox t1, t2;
CheckBox c1;
var b2 = new wpfBuilder("Window").WinSize(400);
b2.R.Add("Text", out t1);
b2.R.Add(out c1, "Check").Add(out t2);

//create and add
var tb = new TextBox { Text = "text" };
b2.R.Add(tb);

//two ways to specify control type when don't need a variable
b2.R.Add(out Label _, "A").Add<Label>("B");

//also there are more AddX functions
b2.AddSeparator();

b2.R.AddOkCancel();
b2.End();
if (!b2.ShowDialog()) return;

/// A simple dialog with buttons that close it.

var b3 = new wpfBuilder("Window").Columns(80, 80, 80);
b3.R.AddButton("One", 1).AddButton("Two", 2).AddButton("Three", 3);
b3.End();
if (!b3.ShowDialog()) return;
print.it(b3.ResultButton);
