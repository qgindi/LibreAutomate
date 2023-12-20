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

/// Sometimes it's difficult to debug with just the above functions. You may want to execute some script parts in step mode (one statement at a time), and in each step see variables, stack, etc. Then need a debugger. This program has a basic debugger. Also you can use other debuggers: Visual Studio, Visual Studio Code, JetBrains Rider etc. To attach a debugger to the script's process, let the script call <see cref="script.debug"/> (<u>click the link for more info<>) or <see cref="Debugger.Launch"/> (only with Visual Studio). Then to start step mode call <see cref="Debugger.Break"/>. When the debugger is attached, it shows script code, and you can click its debug toolbar buttons to execute steps etc. You can click its left margin to set breakpoints. Also it will break on exceptions.

script.debug(); //attaches a debugger, or prints process name and id and waits until you attach a debugger
Debugger.Break(); //starts step mode
print.it(1);
print.it(2);
print.it(3);

/// See also classes in namespace <see cref="System.Diagnostics"/> and its child namespaces.

script.setup(debug: true);
// ...
Debug.Assert(false);

/// Function <see cref="script.debug"/> can launch a script to automate attaching a debugger other than LA. Set it in Options -> Workspace -> Debugger script. Example script:

/// Attaches a debugger to the process id = args[0].
/// If run from editor (args empty), attaches to this process for testing this script.
/// Supported debuggers: Visual Studio, VSCode, dnSpy.

bool test = script.testing; //test this script
if (test) print.clear();
int id = test ? process.thisProcessId : args[0].ToInt();

int debugger = 0; //1 VS, 2 VSCode, 3 dnSpy

var w = wnd.find("*Visual Studio*", "HwndWrapper[DefaultDomain;*");
if (!w.Is0) debugger = 1;
else {
	w = wnd.find("*Visual Studio Code*", "Chrome_WidgetWin_1");
	if (!w.Is0) debugger = 2;
	else {
		w = wnd.find("dnSpy*", "HwndWrapper[dnSpy;*");
		if (!w.Is0) debugger = 3;
	}
}

if (debugger == 0) {
	dialog.showInfo("Debugger window not found", "Supported debuggers: Visual Studio, VSCode, dnSpy.");
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
	500.ms(); //VS needs some random time to update changed code in its editor. If didn't update before showing the Attach dialog, shows messagebox "The source file is different...".
	keys.send("Ctrl+Alt+P");
	var w2 = wnd.find(5, "Attach to Process", "**m HwndWrapper[DefaultDomain;*||#32770");
	300.ms();
	if (w2.ClassNameIs("H*")) { //new VS
		w2.Elm["STATICTEXT", id.ToS()].Find(5).Parent.Focus(true);
	} else { //old VS
		w2.Elm["LISTITEM", null, ["id=4102", $"desc=ID: {id}? *"]].Find(5).Focus(true); //note: use '?' because can be ',' or ';' etc depending on regional settings
	}
	w2.Elm["BUTTON", "Attach", "state=!DISABLED"].Find(3).Invoke();
} else if (debugger == 3) {
	keys.send("Ctrl+Alt+P");
	var w2 = wnd.find(5, "Attach to Process", "HwndWrapper[dnSpy;*");
	500.ms();
	var e = w2.Elm["STATICTEXT", id.ToS()].Find(10).Parent.Parent;
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
	wait.until(0, () => Debugger.IsAttached);
	Debugger.Break();
	print.it("Debugger script.");
}
