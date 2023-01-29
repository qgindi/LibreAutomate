# Tray icon and notifications
Function <a href='/api/Au.script.setup.html'>script.setup</a> can add standard tray icon.

```csharp
script.setup(trayIcon: true);
2.s();
```

Function <a href='/api/Au.script.trayIcon.html'>script.trayIcon</a> adds standard tray icon with more options.

```csharp
script.trayIcon(
	init: t => {
		t.Icon = icon.stock(StockIcon.HELP);
		t.Tooltip = "Middle-click to end the script";
	},
	menu: (t, m) => {
		m["Example"] = o => { dialog.show("Example"); };
		m["Run other script"] = o => { script.run("Example"); };
	}
);
15.s();
```

If need all options, use class <a href='/api/Au.trayIcon.html'>trayIcon</a>.

```csharp
var ti = new trayIcon(1) { Icon = icon.trayIcon(), Tooltip = "example" };
ti.Visible = true;
ti.Click += o => { print.it("click"); };
ti.RightClick += o => { print.it("right click"); };
timer.after(2000, _ => { ti.ShowNotification("notification", "text", TINFlags.InfoIcon); });
dialog.show("tray icon"); //trayIcon works only in threads that process Windows messages; this function does it.
//wait.doEvents(30000); //another way to process messages
```

The above code could use an icon file or resource, but for simplicity it uses the script's icon, which can be changed in the Icons dialog.
