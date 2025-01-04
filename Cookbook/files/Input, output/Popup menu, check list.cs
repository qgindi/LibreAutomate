/// To quickly insert <see cref="popupMenu"/> code, use snippet <b>menuSnippet<>: type <.c>menu<> and select from the list.

var m = new popupMenu("4f14a87a-58e5-4bb6-96db-bbc5e6988e21");
m["A"] = o => { print.it(o); }; //executes code { print.it(o); } when clicked
m["B|Tooltip"] = o => {  };
m.Last.BackgroundColor = System.Drawing.Color.Lavender;
m.Submenu("Submenu", m => {
	m["C"] = o => {  };
	m["D"] = o => {  };
});
m.Separator();
m["Run program"] = o => run.it(folders.System + @"notepad.exe");
m["Run script"] = o => script.run("Script123456789.cs");
m["Copy-paste"] = o => {
	string s = clipboard.copy();
	s = s.Upper();
	clipboard.paste(s);
};
m.Show();

/// Quick ways to add a menu item:
/// - Clone an existing item.
/// - Snippet: start typing <.c>menu<> and select <b>menuItemSnippet<>.
/// - Drag and drop a script, file, folder or link. Then select <b>t[name] = ...<>.
/// - Use the quick capturing hotkey (default <mono>Ctrl+Shift+Q<>) to add a "run program" button.

/// To set menu item icon: click the item in the code editor, and double-click an icon in the <b>Icons<> dialog.

/// Simple menu. Function <see cref="popupMenu.showSimple"/> returns id of the selected item, or 0 if canceled.

int i = popupMenu.showSimple("1 One|2 Two|3 Three||0 Cancel");
switch (i) {
case 1: print.it(1); break;
case 2: print.it(2); break;
case 3: print.it(3); break;
default: return;
}

/// Menu with checkbox items and radiobutton items.

var mm = new popupMenu("4ea61728-f775-4c55-b95c-7376a9243cc5");
mm.CheckDontClose = true;
var c1 = mm.AddCheck("A", true);
var c2 = mm.AddCheck("B", !true, o => { print.it(o); });
mm.Separator();
var r1 = mm.AddRadio("C", true);
var r2 = mm.AddRadio("D", !true);
mm.Separator();
mm.Add(1, "OK");
if (1 != mm.Show()) return;
print.it(c1.IsChecked, c2.IsChecked, r1.IsChecked, r2.IsChecked);
