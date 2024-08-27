//.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
//..

_Dialog1();

bool _Dialog1() {
	var b = new wpfBuilder("Window").WinSize(400);
	
	//examples of some controls
	b.R.Add("Text", out TextBox text1).Focus(); //Label and TextBox
	b.R.Add("Combo", out ComboBox combo1).Items("Zero|One|Two"); //Label and ComboBox with items
	b.R.Add(out CheckBox c1, "Check"); //CheckBox
	b.R.AddButton("Button", _ => { print.it("Button clicked"); }).Width(70).Align("L"); //Button that executes code
	b.R.StartStack() //horizontal stack
		.Add<Label>("Close and return").AddButton("1", 1).AddButton("2", 2) //Label and 2 Buttons that close the dialog and set ResultButton
		.End(); //end stack
	
	b.R.AddOkCancel();
	b.End();
	
#if WPF_PREVIEW //menu Edit > View > WPF preview
	b.Window.Preview();
#endif
	if (!b.ShowDialog()) return false;
	//print.it(b.ResultButton, text1.Text, combo1.SelectedIndex, c1.IsChecked == true);
	
	return true;
}

//See also: snippet wpfDialogSnippet.
