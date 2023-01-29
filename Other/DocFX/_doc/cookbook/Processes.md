# Processes
A process is a running program. It can have visible windows or not. Also known as <i>task</i>. Use class <a href='/api/Au.process.html'>process</a>.

Print names of all processes of this user session.

```csharp
print.clear();
var a = process.allProcesses(ofThisSession: true);
foreach (var v in a) {
	print.it(v.Name);
}
```

Terminate, suspend and resume all "notepad.exe" processes (end tasks).

```csharp
process.terminate("notepad.exe");
process.suspend(true, "notepad.exe");
process.suspend(false, "notepad.exe");
```

If process does not exist.

```csharp
if (!process.exists("notepad.exe")) {
	print.it("does not exist");
}
```

Wait for a "notepad.exe" process. See also <a href='Process triggers (start, end).md'>Process triggers</a>.

```csharp
wait.forCondition(0, () => process.exists("notepad.exe"));
```

Wait until there are no "notepad.exe" processes.

```csharp
wait.forCondition(0, () => !process.exists("notepad.exe"));
```

Get window process id and terminate its process.

```csharp
var w1 = wnd.find(1, "*- Notepad", "Notepad");
int pid = w1.ProcessId;
process.terminate(pid);
```

Get window process name.

```csharp
var w2 = wnd.find(1, "*- Notepad", "Notepad");
var program = w2.ProgramName;
```

Get name and path of current process (the script process).

```csharp
print.it(process.thisExeName, process.thisExePath);
```

Get script name.

```csharp
print.it(script.name);
```

When need to get more process properties, use class <a href='https://www.google.com/search?q=C%23+class+Process'>Process</a> or <a href='WMI.md'>WMI</a>.
