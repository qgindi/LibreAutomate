/// Usually the best way to select/click a menu item is hotkey or <mono>Alt+keys<>. It can be recorded with the Input recorder.

wnd.find(0, "*- Notepad", "Notepad").Activate();
keys.send("Ctrl+V"); //hotkey
keys.send("Alt+E P"); //Alt+keys

/// Actually with <mono>Alt<> should be used characters, not keys, because keys may not work with other keyboard layouts.

keys.send("Alt+^ep"); //Alt+characters

/// To select menu items also can be used arrow keys, <mono>Home<>, <mono>End<>, finally <mono>Enter<>. To activate menu bar, use <mono>Alt<> or <mono>F10<>. To show context menu, use <mono>Apps/Menu<> or <mono>Shift+F10<>.

/// Ribbon items (controls) can be selected/clicked in the same way.

/// Often dialog controls can be selected/clicked in the same way too. Or use <mono>Tab<> or <mono>Shift+Tab<> to select, <mono>Space<> to click, <mono>Alt+Down<> to expand combobox, arrows/<mono>Home<>/<mono>End<> to select list items, <mono>Ctrl+Tab<> to select tab pages.

wnd.find(1, "* Properties", "#32770").Activate();
keys.send("Alt+^r");
keys.send("Tab*3 Space");

/// When keys can't be used, try UI element functions. See the <+recipe>UI elements<> recipe. Or mouse functions, but scripts with mouse functions may stop working after changing window size, layout or DPI.

/// See also <google>keyboard shortcuts and hotkeys in Windows and applications<>.
