---
uid: debugger
---

# Debugging C# scripts
## Useful functions
See Cookbook recipe [Script testing and debugging](https://www.libreautomate.com/cookbook/Script%20testing%20and%20debugging.html).

## Debugger
Debugger in LibreAutomate is similar to that of [Visual Studio](https://www.google.com/search?q=Visual+Studio+debugger), [VSCode](https://www.google.com/search?q=VSCode+debugger), [Rider](https://www.google.com/search?q=Rider+debugger) etc.

Two panels are dedicated to debugging: **Debug** and **Breakpoints**. If the **Debug** panel was hidden when debugging starts, it becomes visible temporarily while debugging. Both panels initially are hidden; to show always, right-click any panel caption.

To run current script with the debugger, click the **Debug run** button or menu item, or use the toolbar in the **Debug** panel. Usually at first you add one or more breakpoints in code (click the white margin). The script stops at a breakpoint line, or on exception, or in **Debugger.Break**, **Debug.Assert**, or when clicked **Pause**. Then you can use the **Debug** toolbar or hotkeys to execute the script in steps. Also you can click the white margin and select **Run to here**.

To attach the debugger to an already running script, use the **Tasks** panel or [script.debug](). Or **Debug.Assert**, if the script starts with code like `script.setup(debug: true);`.

### Features
- Standard stepping features.
- Breakpoints, logpoints.
- Break on exception.
- Call stack.
- List of variables. Click a variable to print it.
- Hover the mouse on a variable in code to display its value.
- Supports **Debugger.Break**, **Debug.Assert**, **Debug.Print** etc.
- Run to here.
- Jump to here.

### Some missing features
- "Hot reload" or "Edit and continue".
- Evaluate any C# expression.
- Set variable's value.
- Open and debug source files that don't exist in the workspace.
- Attach the debugger to unknown processes.

### Notes
Thanks to [Samsung/netcoredbg](https://github.com/Samsung/netcoredbg).

Antivirus software may quarantine `netcoredbg.exe` (debugger). Add it to the exclusions list of the antivirus.

To debug optimized code (`/*/ optimize true; /*/`), temporarily check **Debug optimized code** in the options menu. Normally you don't debug optimized code, it may not work well.

Cannot debug 32-bit processes.

## Other debuggers
You may have Visual Studio or other IDE with a better .NET debugger. You can attach that debugger to a running script. Let the script call one of these functions:
- **Debugger.Launch**. Example: `Debugger.Launch(); Debugger.Break();`. Note: it shows several dialogs before you can start debugging.
- [script.debug](). Example: `script.debug(true); Debugger.Break();`. It shows a dialog with the script process id and waits for a debugger attached. Then you can attach a debugger. To automate it, create a window-triggered script.

```csharp
Triggers.Window[TWEvent.ActiveNew, "Attach debugger", "#32770"] = o => script.run("Attach debugger.cs", o.Window.ProcessId.ToS());
```

Script `Attach debugger.cs`:
```csharp
// Attaches the Visual Studio debugger to the process id = args[0].
// If run from editor (args empty), attaches to this process for testing this script.

bool test = script.testing; //test this script
if (test) print.clear();
int id = test ? process.thisProcessId : args[0].ToInt();

bool canAttach = false;
var w = wnd.find("*Visual Studio*", "HwndWrapper[DefaultDomain;*");
if (w.Is0) {
	dialog.showInfo("Debugger window not found", "Supported debuggers: Visual Studio, VSCode, dnSpy.");
} else if (uacInfo.ofProcess(id).Elevation == UacElevation.Full && uacInfo.ofProcess(w.ProcessId).Elevation != UacElevation.Full) {
	dialog.showInfo("Debugger isn't admin", "The debugger process must be running as administrator.");
} else canAttach = true;
if (!canAttach) {
	if (!test) process.terminate(id); //because it's waiting in script.debug
	return;
}

w.Activate();
600.ms();
keys.send("Ctrl+Alt+P");
var w2 = wnd.find(5, "Attach to Process", "**m HwndWrapper[DefaultDomain;*||#32770");
300.ms();
if (w2.ClassNameIs("H*")) { //new VS
	w2.Elm["STATICTEXT", id.ToS()].Find(5).Parent.Focus(true);
} else { //old VS
	w2.Elm["LISTITEM", null, ["id=4102", $"desc=ID: {id}? *"]].Find(5).Focus(true); //note: use '?' because can be ',' or ';' etc depending on regional settings
}
w2.Elm["BUTTON", "Attach", "state=!DISABLED"].Find(3).Invoke();

if (test) {
	wait.until(0, () => Debugger.IsAttached);
	Debugger.Break();
	print.it("Debugger script.");
}
```
