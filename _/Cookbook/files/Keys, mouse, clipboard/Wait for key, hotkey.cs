/// Wait for key. <help keys.waitForKey>More info<>.

var key = keys.waitForKey(0, up: false, block: false); //wait for any key
print.it(key);

if (!keys.waitForKey(-5, KKey.Tab, block: true)) return; //wait for <mono>Tab<> and block it. Exit if not pressed in 5 s.
print.it("Tab");

/// Wait for hotkey. <help keys.waitForHotkey>More info<>.

keys.waitForHotkey(5, "Ctrl+Win+K"); //exception after 5 s

/// Wait until <mono>Ctrl<>, <mono>Alt<>, <mono>Shift<> and <mono>Win<> aren't pressed.

keys.waitForNoModifierKeys();
