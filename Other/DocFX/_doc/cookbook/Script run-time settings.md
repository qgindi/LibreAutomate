# Script run-time settings
The script Properties dialog contains only settings for compiling and launching the script. Run-time settings can be set in script code.

Function <a href='/api/Au.script.setup.html'>script.setup</a> can add <a href='Tray icon and notifications.md'>tray icon</a>, two ways to terminate the script process, and more.

```csharp
script.setup(trayIcon: true, sleepExit: true);
```

The <span style='color:green'>ifRunning</span> option isn't applied if the .exe script is launched not from the editor. Then function <a href='/api/Au.script.single.html'>script.single</a> can be used to ensure single process instance.

```csharp
script.single("mutex7654329");
```

Use localized string and date parsing, formatting, comparing, display, etc.

```csharp
process.thisProcessCultureIsInvariant = false;
```

Class <a href='/api/Au.dialog.options.html'>dialog.options</a> can be used to set some options for standard dialogs.

```csharp
dialog.options.defaultScreen = screen.ofMouse;
2.s();
dialog.show("");
```

Use class <a href='/api/Au.opt.html'>opt</a> to set options for mouse, keyboard, clipboard and some other functions.

```csharp
opt.mouse.MoveSpeed = 30;
opt.key.TextSpeed = 50;

var w = wnd.find(1, "*- Notepad", "Notepad");
mouse.click(w, .5f, .5f);
keys.sendt("Slow text");
```

Class <a href='/api/Au.print.html'>print</a> has several options.

```csharp
print.redirectDebugOutput = true;
// ...
Debug.Print("debug");
```

