using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

/*
This class file can be used in scripts etc like any class file. One of:
- Add this file to the same project folder.
- Or in script Properties dialog click button "Class file", select this file, OK.
- Or add this file to a library project and use the library anywhere. To use the library in a script, in its Properties click Project and select the library project.
*/

/// <summary>
/// 
/// </summary>
/// <example>
/// <code><![CDATA[
/// var d = new DialogWithTabs();
/// if (d.ShowDialog() != true) return;
/// ]]></code>
/// </example>
public class DialogWithTabs : Window {
	wpfBuilder _b;
	TabControl _tc;
	
	///
	public DialogWithTabs() {
		Title = "Dialog";
		_b = new wpfBuilder(this).WinSize(400);
		_b.Row(-1).Add(out _tc).Height(300..);
		_b.R.AddOkCancel(apply: "_Apply");
		_Page1();
		_Page2();
		// ...
		_b.End();
#if WPF_PREVIEW //menu Edit > View > WPF preview
		_tc.SelectedIndex = 0;
#endif
	}
	
	wpfBuilder _Page(string name, WBPanelType panelType = WBPanelType.Grid) {
		var tp = new TabItem { Header = name };
		_tc.Items.Add(tp);
		return new wpfBuilder(tp, panelType).Margin("3");
	}
	
	void _Page1() {
		var b = _Page("Page1");
		b.R.Add("Text", out TextBox text1)
			.Validation(_ => string.IsNullOrWhiteSpace(text1.Text) ? "Text cannot be empty" : null);
		b.End();
		
		//if need, add initialization code (set control properties, events, etc) here or/and in Loaded event handler below
		
		//b.Loaded += () => {
		
		//};
		
		_b.OkApply += e => {
			print.it($"Text: \"{text1.Text.Trim()}\"");
		};
	}
	
	void _Page2() {
		var b = _Page("Page2");
		b.R.Add("Combo", out ComboBox combo1).Items("Zero|One|Two");
		b.R.Add(out CheckBox c1, "Check");
		b.End();
		
		//bool loaded = false;
		//b.Loaded += () => { //note: this code runs when this page selected first time, which may never happen
		//	loaded = true;
		//};
		
		_b.OkApply += e => {
			//if (!loaded) return;
			
			print.it($"Combo index: {combo1.SelectedIndex}");
			print.it($"Check: {c1.IsChecked == true}");
		};
	}
	
	// ...
}
