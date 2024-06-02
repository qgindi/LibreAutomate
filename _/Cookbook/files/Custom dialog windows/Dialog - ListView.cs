using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

var d = new DialogListViewExample();
if (d.ShowDialog() != true) return;

class DialogListViewExample : Window {
	ListView _lv; //this control will display data
	List<Abc> _a; //data that will be displayed
	
	//listview item data type example
	record Abc(string Name, string Value) { }
	
	public DialogListViewExample() {
		//create dialog
		Title = "ListView example";
		var b = new wpfBuilder(this).WinSize(600, 500).Columns(70, -1);
		b.R.AddButton("Update", _ => _Update()).Span(1);
		b.Row(-1).Add(out _lv).Span(-1);
		b.End();
		
		//add listview columns and bind to data
		var gv = new GridView { AllowsColumnReorder = false };
		gv.Columns.Add(new() { Header = "A", Width = 150, DisplayMemberBinding = new Binding("Name") { Mode = BindingMode.OneTime } });
		gv.Columns.Add(new() { Header = "B", Width = 400, DisplayMemberBinding = new Binding("Value") { Mode = BindingMode.OneTime } });
		_lv.View = gv;
		
		//data
		_a = new() {
			new("One", "jhsjhjs"),
			new("Two", "mcnvmncm"),
		};
		_lv.ItemsSource = _a;
		
		//item selected event
		_lv.SelectionChanged += (_, e) => {
			if (_lv.SelectedItem is Abc x) {
				print.it("selected", x);
			}
		};
		
		//item double-clicked event
		_lv.PreviewMouseDoubleClick += (o, e) => {
			e.Handled = true;
			if (e.ChangedButton == MouseButton.Left && _EventItemData(e) is Abc x) {
				print.it("double-clicked", x);
			}
		};
		
		//context menu event
		_lv.ContextMenuOpening += (o, e) => {
			e.Handled = true;
			if (_EventItemData(e) is Abc x) {
				print.it("context menu", x);
			} else {
				print.it("context menu");
			}
		};
		
		//b.Loaded += () => {
		
		//};
		
		//b.OkApply += e => {
		
		//};
	}
	
	//example of updating data
	void _Update() {
		_a.RemoveAt(0);
		_a.Add(new("Three", "qwwqewe"));
		_lv.ItemsSource = null;
		_lv.ItemsSource = _a;
	}
	
	static DependencyObject _EventItem(RoutedEventArgs e) => ((ItemsControl)e.Source).ContainerFromElement(e.OriginalSource as DependencyObject);
	
	static object _EventItemData(RoutedEventArgs e) => _EventItem(e) is ContentControl c ? c.Content : null;
}
