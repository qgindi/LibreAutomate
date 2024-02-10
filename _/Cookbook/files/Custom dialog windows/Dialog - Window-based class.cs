/// To start creating a <b>Window<>-based class that uses a <see cref="wpfBuilder"/> to add elements etc, you can use menu <b>File > New > Dialogs<>.
/// The main reason to use a Window-based class is to be able to override functions of the base class in order to receive various notifications (it's a more powerful alternative to events) or change the behavior or initial properties of the class.

var d = new Dialogs.DialogClass();
if (d.ShowDialog() != true) return;

namespace Dialogs {
	using System;
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
		
		//In your Window-based class you can override (replace) virtual functions of the Window class. 
		//Add a function with the same name, arguments, etc as a base's function, and with the override keyword. Then your function will be called instead of the base's function.
		//The code editor shows a list of overridable functions when you type the override keyword and space. Then you can start typing a function name to filter the list. Select a function. It inserts an empty function that just calls the base's function. Add more code before or/and after the call.
		//Usually a corresponding event exists. For example event Closed for function OnClosed. This function is called before event handlers.
		
		//Example of an override function. 
		protected override void OnClosed(EventArgs e) {
			print.it("closed");
			
			//Your function should call the base function. Unless its documentation says it isn't necessary or you want to steal the notification from the base class.
			//If a corresponding event exists, the base function usually raises the event (calls event handlers). For example base.OnClosed raises the Closed event.
			base.OnClosed(e);
		}
	}
}
