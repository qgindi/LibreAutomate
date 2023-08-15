/// For keyboard, autotext and mouse triggers you can specify window(s) where they will work or not work. It is known as "trigger scope". A trigger scope is applied to triggers added afterwards, until another scope.

///This is a full working script. To test it, create new script and paste all code. Or in file "Keyboard triggers" paste code from #region. Then click the Run button and press Alt+1 etc in various windows.

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
Triggers.Of.NotWindows(
	new("*WordPad", "WordPadClass"),
	new("*Paint", "MSPaintApp")
	);
hk["Alt+9"] = o => { print.it(o); };

#endregion

Triggers.Run();

/// Tips:
/// - To quickly insert <mono>Triggers.Of.Window(...)<> code, use the quick capturing hotkey (default Ctrl+Shift+Q).
/// - Also you can copy-paste window name etc from the 'Find window' tool. To get code like <mono>new("name", "class")<>, in the combo box select 'finder', and let 'Control' be unchecked.
