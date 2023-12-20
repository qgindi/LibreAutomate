using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Windows.Data;

//create dialog
var b = new wpfBuilder("DataGrid example").WinSize(400, 400);
b.Row(-1).Add(out DataGrid g);
b.R.AddOkCancel();
b.End();

//columns, properties
g.Columns.Add(new DataGridCheckBoxColumn { Binding = new Binding("Check"), Width = 20 });
g.Columns.Add(new DataGridTextColumn { Header = "Editable", Binding = new Binding("Text"), Width = new(1, DataGridLengthUnitType.Star) });
g.Columns.Add(new DataGridTextColumn { Header = "Readonly", Binding = new Binding("Comment"), IsReadOnly = true, Width = 100 });
g.AutoGenerateColumns = false;
g.AlternatingRowBackground = Brushes.Wheat;
g.GridLinesVisibility = DataGridGridLinesVisibility.Vertical;
g.VerticalGridLinesBrush = Brushes.LightGray;
//g.IsReadOnly = true;

//data
var a = new ObservableCollection<Abc>() {
	new(true, "text 1"),
	new(!true, "text 2", "comment"),
	new(true, "text 3"),
	new(!true, "text 4"),
};
g.ItemsSource = a;

//single-click editing
g.PreviewMouseDown += (o, e) => {
	if (e.ChangedButton == MouseButton.Left) {
		g.Dispatcher.InvokeAsync(() => g.BeginEdit(e), System.Windows.Threading.DispatcherPriority.Input);
	}
};

if (!b.ShowDialog()) return;

print.it(a); //automatically updated when the user edits cells

//row data type
record Abc(bool Check, string Text, string Comment = null) { }
