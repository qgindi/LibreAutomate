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
/// var d = new DialogClass();
/// if (d.ShowDialog() != true) return;
/// ]]></code>
/// </example>
public class DialogClass : Window {
	ComboBox _combo1;
	CheckBox _c1;
	
	///
	public DialogClass() {
		Title = "Dialog";
		var b = new wpfBuilder(this).WinSize(400);
		b.R.Add("Text", out TextBox text1).Focus()
			.Validation(_ => string.IsNullOrWhiteSpace(text1.Text) ? "Text cannot be empty" : null);
		b.R.Add("Combo", out _combo1).Items("Zero|One|Two");
		b.R.Add(out _c1, "Check");
		b.R.AddOkCancel();
		b.End();
		
		//if need, add initialization code (set control properties, events, etc) here or/and in Loaded event handler below
		
		//b.Loaded += () => {
			
		//};
		
		b.OkApply += e => {
			print.it($"Text: \"{text1.Text.Trim()}\"");
			print.it($"Combo index: {_combo1.SelectedIndex}");
			print.it($"Check: {_c1.IsChecked == true}");
		};
	}
}
