/// Examples of some automation functions and C# language syntax.
/// To run this script, click the Run button (green triangle).
/// Look for more examples in the Help panel.

//print text in the Output panel
print.it("Example");

//show message box. Exit if Cancel.
if (!dialog.showOkCancel("Run File Explorer?")) return;

//open a folder in File Explorer
run.it(@"C:\Program Files");

//wait 3 s
3.s();

//The above is the simplest run-and-wait code. Try hotkey Ctrl+Shift+Q to create code that waits for window.

//send keys
keys.send("Ctrl+L"); //focus the address bar

//create two variables
string s = "text";
bool undo = true; //or false

//repeat 5 times
for (int i = 0; i < 5; i++) {
	//send text with variables
	keys.sendt($"Example {s} {i + 1}");
	
	//wait 500 ms
	500.ms();
	
	//if variable undo is true, execute statements in the { }
	if (undo) {
		keys.send("Ctrl+Z"); //Undo
	}
}
