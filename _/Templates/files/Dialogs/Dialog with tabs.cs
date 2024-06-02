//.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
//..

_Dialog1();

bool _Dialog1() {
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
	
	b = bMain.End();
	
#if WPF_PREVIEW //menu Edit > View > WPF preview
	tc.SelectedIndex = 0;
	b.Window.Preview();
#endif
	if (!b.ShowDialog()) return false;
	//print.it(b1.ResultButton);
	
	return true;
}
