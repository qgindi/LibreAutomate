/// For keyboard, autotext and mouse triggers you can specify window(s) where they will work or not work. It is known as <i>trigger scope<>. A trigger scope is applied to triggers added afterwards, until another scope (if any). To create scope code can be used the quick capturing hotkey (default <mono>Ctrl+Shift+Q<>).

///This is a full working script. To test it, create new script and paste all code. Or in file <.c>Keyboard triggers<> paste code from the <.c>#region<>. Then click the <b>Run<> button and press <mono>Alt+1<> etc in various windows.

using Au.Triggers;
var Triggers = new ActionTriggers();
var hk = Triggers.Hotkey;

#region you can paste this code in file "Keyboard triggers"

//these triggers work everywhere
hk["Alt+1"] = o => { print.it(o); };
hk["Alt+2"] = o => { print.it(o); };

//these triggers work only in WordPad windows (when a WordPad window is active)
Triggers.Of.Window("*WordPad", "WordPadClass");
hk["Alt+3"] = o => { print.it(o); };
hk["Alt+4"] = o => { print.it(o); };

//this trigger works only in windows of wordpad.exe
Triggers.Of.Window(of: "wordpad.exe");
hk["Alt+5"] = o => { print.it(o); };

//this trigger works everywhere except in windows of wordpad.exe
Triggers.Of.NotWindow(of: "wordpad.exe");
hk["Alt+6"] = o => { print.it(o); };

//this trigger works everywhere
Triggers.Of.AllWindows();
hk["Alt+7"] = o => { print.it(o); };

//this trigger works only in WordPad and Paint windows
Triggers.Of.Windows(
	new("*WordPad", "WordPadClass"),
	new("*Paint", "MSPaintApp")
	);
hk["Alt+8"] = o => { print.it(o); };

//this trigger works everywhere except in WordPad and Paint windows
Triggers.Of.NotWindows([
	new("*WordPad", "WordPadClass"),
	new("*Paint", "MSPaintApp")
	]);
hk["Alt+9"] = o => { print.it(o); };

#endregion

Triggers.Run();
