/// When creating, testing and debugging scripts and other code, often you want to see what code parts are executed, what are values of variables there, etc. In most cases for it can be used <see cref="print.it"/>. You can insert it temporarily, and later delete or disable the code line.

print.it("Script started");
Test(5);
print.it("Script ended");

void Test(int i) {
	print.it("Test", i);
	//print.it("disabled");
}

/// To get current stack of functions, use <see cref="StackTrace"/>.

F1();
void F1() { F2(); }
void F2() { F3(); }
void F3() { print.it(new StackTrace(0, true)); }

/// Sometimes it's difficult to debug with just the above functions. You may want to execute some script parts in step mode (one statement at a time), and in each step see variables, stack, etc. Then need a debugger. This program does not have a debugger, but you can use debuggers of Visual Studio or Visual Studio Code. At first need to attach a debugger to the script's process. Let the script call <see cref="script.debug"/> (<u>click the link for more info<>) or <see cref="Debugger.Launch"/> (only with Visual Studio). Then to start step mode call <see cref="Debugger.Break"/>. To insert code can be used menu Run -> Debugger. When the debugger is attached, it displays script code, and you can click its debug toolbar buttons to execute steps etc. You can click its left margin to set breakpoints. Also it will break on exceptions.

script.debug(); //prints process name and id, and waits until you attach a debugger
Debugger.Break(); //starts step mode
print.it(1);
print.it(2);
print.it(3);

/// See also classes in namespace <see cref="System.Diagnostics"/> and its child namespaces.

script.setup(debug: true);
// ...
Debug.Assert(false);

/// Function <see cref="script.debug"/> can launch a script to automate attaching a debugger. Set it in Options -> General -> Debugger script. Example script:

// Attaches Visual Studio or VSCode debugger to the process id = args[0].
// If args is empty, attaches to this process for testing this script.

bool test = args.Length == 0; //test this script
if (test) print.clear();
int id = test ? process.thisProcessId : args[0].ToInt();

int debugger = 0; //1 VS, 2 VSCode
var w = wnd.find("*Visual Studio*", "HwndWrapper[DefaultDomain;*");
if (!w.Is0) debugger = 1;
else {
	w = wnd.find("*Visual Studio Code*", "Chrome_WidgetWin_1");
	if (!w.Is0) debugger = 2;
}
if (debugger == 0) {
	dialog.showInfo("Debugger window not found", "Supported debuggers: Visual Studio, VSCode.");
} else if (uacInfo.ofProcess(id).Elevation == UacElevation.Full && uacInfo.ofProcess(w.ProcessId).Elevation != UacElevation.Full) {
	dialog.showInfo("Debugger isn't admin", "The debugger process must be running as administrator.");
	debugger = 0;
}
if (debugger == 0) {
	if (!test) process.terminate(id); //because it's waiting in script.debug
	return;
}

w.Activate();
100.ms();
if (debugger == 1) {
	keys.send("Ctrl+Alt+P");
	var w2 = wnd.find(5, "Attach to Process", "#32770");
	500.ms();
	var e = w2.Elm["LISTITEM", null, new("id=4102", $"desc=ID: {id}? *")].Find(3); //note: use '?' because can be ',' or ';' etc depending on regional settings
	200.ms();
	e.Focus(true);
	keys.send("Alt+a");
} else {
	keys.send("Ctrl+Shift+D");
	g1:
	var e1 = w.Elm["web:COMBOBOX", "Debug Launch Configurations"].Find(3);
	if (e1.Value != ".NET Core Attach") {
		//e1.ComboSelect(".NET Core Attach", "100k"); //crashes with how = default or "i". "s" and "m" don't work
		//keys.send(100, "Esc"); //somehow the first Enter does not close. Need Esc or second Enter. But State not EXPANDED.
		if (!dialog.showOkCancel("Select debug configuration", "In the combo box please select \".NET Core Attach\". Then click OK here.")) return;
		goto g1;
	}
	keys.send("F5");
	var e = w.Elm["web:COMBOBOX", "Select the process to attach to"].Find(15);
	clipboard.paste($"{id}");
	keys.send("Enter");
}

if (test) {
	wait.forCondition(0, () => Debugger.IsAttached);
	Debugger.Break();
	print.it("Debugger script.");
}
