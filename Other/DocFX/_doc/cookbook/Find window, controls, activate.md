# Find window, controls, activate
To create <a href='/api/Au.wnd.find.html'>wnd.find</a> code can be used:
- Tools in menu -> Code: Find window (Ctrl+Shift+W), Input recorder.
- Hotkey Ctrl+Shift+Q.
- Snippets winFindSnippet, actWinSnippet. You can see window properties in panel "Mouse".

Find window and create variable w1 that contains window handle. Window name must end with "- Notepad"; class name must be "Notepad". Can wait/retry max 1 s; exception if not found. Activate when found.

```csharp
var w1 = wnd.find(1, "*- Notepad", "Notepad");
w1.Activate();
```

Find window with no name, class name "Shell_TrayWnd", program "explorer.exe". Exit if not found.

```csharp
var w2 = wnd.find("", "Shell_TrayWnd", "explorer.exe");
if (w2.Is0) return;
```

Function <a href='/api/Au.wnd.findOrRun.html'>wnd.findOrRun</a> opens window if does not exist. Also activates.

```csharp
var w3 = wnd.findOrRun("* Notepad", run: () => run.it("notepad.exe")); //if not found, run "notepad.exe"
print.it(w3);
```

Get the active window.

```csharp
var w4 = wnd.active;
```

Get the mouse window.

```csharp
var w5 = wnd.fromMouse(WXYFlags.NeedWindow);
//w5 = wnd.fromXY(mouse.xy, WXYFlags.NeedWindow); //the same
```

Find all windows classnamed "Notepad" of program "notepad.exe".

```csharp
var a1 = wnd.findAll(cn: "Notepad", of: "notepad.exe");
foreach (var v in a1) {
	print.it(v);
}
```

Get all visible windows.

```csharp
var a2 = wnd.getwnd.allWindows(onlyVisible: true);
foreach (var v in a2) {
	print.it(v);
}
```

A window can contain child windows, also known as <i>controls</i>. This code finds child window named "User Promoted Notification Area", classnamed "ToolbarWindow32". Exception if not found. When found, moves the mouse cursor to its center.

```csharp
var w6 = wnd.find(0, "", "Shell_TrayWnd", "explorer.exe");
var c1 = w6.Child(0, "User Promoted Notification Area", "ToolbarWindow32");
mouse.move(c1);
```

To create <a href='/api/Au.wnd.Child.html'>wnd.Child</a> code can be used <b>wnd.find</b> code tools.
