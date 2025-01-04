/// Function <see cref="script.setup"/> can add standard tray icon.

script.setup(trayIcon: true);
3.s();

/// Function <see cref="script.trayIcon"/> adds standard tray icon with more options.

script.trayIcon(
	init: t => {
		t.Icon = icon.stock(StockIcon.HELP);
		t.Tooltip = "Tooltip";
	},
	menu: (t, m) => {
		m["Example"] = o => { dialog.show("Example"); };
		m["Run other script"] = o => { script.run("Example"); };
	}
);
15.s();

/// If need all options, use class <see cref="trayIcon"/>.

var ti = new trayIcon(1) { Icon = icon.trayIcon(), Tooltip = "example" };
ti.Visible = true;
ti.Click += o => { print.it("click"); };
ti.RightClick += o => { print.it("right click"); };
timer.after(2000, _ => { ti.ShowNotification("notification", "text", TINFlags.InfoIcon); });
dialog.show("tray icon"); //trayIcon works only in threads that process Windows messages; this function does it.
//wait.doEvents(30000); //another way to process messages

/// The above code could use an icon file or resource, but for simplicity it uses the script's icon, which can be changed in the <b>Icons<> dialog.
