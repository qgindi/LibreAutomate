/// Use events to execute code when something happened or changed in the dialog.

using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(400);
b.R.Add("Text", out TextBox t1).Focus();
b.R.Add("List", out ListBox list1).Items("Zero|One|Two");
b.R.Add(out CheckBox c1, "Check");
b.R.AddButton("Button", _ => { print.it("Button clicked"); c1.IsChecked = true; });
b.R.AddOkCancel();
b.End();

b.Loaded += () => { print.it("Loaded"); };
b.OkApply += e => { print.it("OkApply", e.Button); };

t1.TextChanged += (o, e) => {
	var v = o as TextBox; //o is t1, but assume you don't want to use the t1 variable here
	print.it("text changed", v.Text);
};

list1.SelectionChanged += (o, e) => {
	print.it("selected", list1.SelectedIndex, list1.SelectedItem);
};

c1.Click += (o, e) => {
	print.it("checkbox clicked", c1.IsChecked);
};

c1.Checked += _CheckedUnchecked;
c1.Unchecked += _CheckedUnchecked;

void _CheckedUnchecked(object o, RoutedEventArgs e) {
	print.it("checked/unchecked", c1.IsChecked);
}

if (!b.ShowDialog()) return;

/// Another way - use a <+recipe Dialog - *class>dialog class<> and override <b>OnX<> functions.
