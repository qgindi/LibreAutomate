using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(400);
b.R.Add(out Menu menu).Margin("T-2 B0");
b.R.Add(out TextBox text1).Multiline(200);
b.R.AddOkCancel();
b.End();
_CreateMenu(menu);
if (!b.ShowDialog()) return;

void _CreateMenu(Menu menu) {
	//File
	var mFile = _TopItem("_File");
	_Item(mFile, "_Open", o => { print.it(o.Header); });
	_Separator(mFile);
	_Item(mFile, "E_xit", o => { print.it(o.Header); b.Window.Close(); });
	//Edit
	var mEdit = _TopItem("_Edit");
	_Item(mEdit, "_Paste", o => { print.it(o.Header); text1.Paste(); });
	var mSubmenu = _Item(mEdit, "_Submenu");
	_Item(mSubmenu, "_In submenu", o => { print.it(o.Header); });
	
	MenuItem _Item(ItemsControl parent, string name, Action<MenuItem> click = null) {
		var mi = new MenuItem { Header = name };
		if(click != null) mi.Click += (sender, _) => click(sender as MenuItem);
		parent.Items.Add(mi);
		return mi;
	}
	
	MenuItem _TopItem(string name) => _Item(menu, name);
	
	void _Separator(ItemsControl parent) { parent.Items.Add(new Separator()); }
}
