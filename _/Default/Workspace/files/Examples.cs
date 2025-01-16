/// Examples of some automation functions and C# language syntax.
/// To run this script, click the Run button (green triangle).
/// Look for more examples in the Cookbook panel.

//print text in the Output panel
print.it("Example");

//show message box. Exit if Cancel.
if (!dialog.showOkCancel("Run Notepad?")) return;

//run Notepad
run.it(@"notepad.exe");

//wait 1 s
1.s();

//create string variable s
string s = "text";

//repeat 5 times
for (int i = 0; i < 5; i++) {
	//send text with variables
	keys.sendt($"Example {s} {i + 1}");
	
	//wait 500 ms
	500.ms();
	
	//send keys
	keys.send("Ctrl+Z"); //Undo
}
