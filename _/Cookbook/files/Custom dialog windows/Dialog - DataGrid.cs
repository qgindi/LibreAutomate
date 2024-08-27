using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Windows.Data;

//create dialog
var b = new wpfBuilder("DataGrid example").WinSize(400, 400);

var g = new DataGrid {
	AutoGenerateColumns = false,
	AlternatingRowBackground = Brushes.Wheat,
	GridLinesVisibility = DataGridGridLinesVisibility.Vertical,
	VerticalGridLinesBrush = Brushes.LightGray,
	//IsReadOnly = true,
};
b.Row(-1).Add(g);

b.R.AddOkCancel();
b.End();

//columns, properties
g.Columns.Add(new DataGridCheckBoxColumn { Binding = new Binding("Check"), Width = 20 });
g.Columns.Add(new DataGridTextColumn { Header = "Editable", Binding = new Binding("Text"), Width = new(1, DataGridLengthUnitType.Star) });
g.Columns.Add(new DataGridTextColumn { Header = "Readonly", Binding = new Binding("Comment"), IsReadOnly = true, Width = 100 });

//data
var a = new ObservableCollection<Abc>() { //or List<Abc>
	new(true, "text 1"),
	new(!true, "text 2", "comment"),
	new(true, "text 3"),
	new(!true, "text 4"),
};
g.ItemsSource = a;

if (!b.ShowDialog()) return;

print.it(a); //automatically updated when the user edits cells

//row data type
record Abc(bool Check, string Text, string Password = null) {
	public Abc() : this(false, null) {  }
}
