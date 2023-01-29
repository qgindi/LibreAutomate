# Dialog - Window-based class
To start creating a <b>Window</b>-based class that uses a <a href='/api/Au.wpfBuilder.html'>wpfBuilder</a> to add elements etc, use menu File -> New -> Dialogs.

```csharp
var d = new Dialogs.DialogClass();
if (d.ShowDialog() != true) return;		

namespace Dialogs {
using System.Windows;
using System.Windows.Controls;

class DialogClass : Window {
	public DialogClass() {
		Title = "Dialog";
		var b = new wpfBuilder(this).WinSize(400);
		b.R.Add("Text", out TextBox text1).Focus().Validation(_ => string.IsNullOrWhiteSpace(text1.Text) ? "Text cannot be empty" : null);
		b.R.Add("Combo", out ComboBox combo1).Items("Zero|One|Two");
		b.R.Add(out CheckBox c1, "Check");
		b.R.AddOkCancel();
		b.End();
		
		//if need, add initialization code (set control properties, events, etc) here or/and in Loaded event handler below
		
		//b.Loaded += () => {
			
		//};

		b.OkApply += e => {
			print.it($"Text: \"{text1.Text.Trim()}\"");
			print.it($"Combo index: {combo1.SelectedIndex}");
			print.it($"Check: {c1.IsChecked == true}");
		};
	}
}
}
```

