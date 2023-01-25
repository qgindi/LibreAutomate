/// Examples of some automation functions and C# language syntax.
/// To run this script, click the Run button (green triangle).
/// Look for more examples in the Cookbook panel.

//print text in the Output panel
print.it("Example");

//show message box. Exit if Cancel.
if (!dialog.showOkCancel("Run WordPad?")) return;

//run WordPad
run.it(folders.ProgramFiles + @"Windows NT\Accessories\wordpad.exe");

//wait 1 s
1.s();

//create string variable s
string s = "text";

//repeat 10 times
for (int i = 0; i < 10; i++) {
	//send text with variables
	keys.sendt($"Example {s} {i + 1}");
	
	//wait 200 ms
	200.ms();
	
	//send keys
	keys.send("Ctrl+Z"); //Undo
}

//find and click UI element "View" in WordPad
var w = wnd.find(1, "*- WordPad", "WordPadClass");
var e = w.Elm["PAGETAB", "View"].Find(1);
e.Invoke();
