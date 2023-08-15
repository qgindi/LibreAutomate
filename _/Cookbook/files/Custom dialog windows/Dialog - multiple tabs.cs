/// To start creating a dialog with multiple tab pages, use menu File -> New -> Dialogs. Or copy-paste or drag-drop this code.

using System.Windows;
using System.Windows.Controls;

var bMain = new wpfBuilder("Window").WinSize(400);
var b = bMain;
b.Row(-1).Add(out TabControl tc).Height(300..);
b.R.AddOkCancel(apply: "_Apply");

wpfBuilder _Page(string name, WBPanelType panelType = WBPanelType.Grid) {
	var tp = new TabItem { Header = name };
	tc.Items.Add(tp);
	return new wpfBuilder(tp, panelType);
}

//--------------------------------

var b1 = b = _Page("Page1");
b.R.Add("Text", out TextBox _);
b.AddButton("Close 5", 5);
b.End();

//--------------------------------

var b2 = b = _Page("Page2");
b.R.Add("Combo", out ComboBox _).Editable().Items("Zero|One|Two");
b.R.Add(out CheckBox _, "Check");
b.End();

//--------------------------------

//tc.SelectedIndex = 1;

b = bMain.End();
if (!b.ShowDialog()) return;
//print.it(b1.ResultButton);
