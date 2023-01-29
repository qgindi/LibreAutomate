# Select-click menu, control (keyboard shortcuts)
Usually the best way to select/click a menu item is hotkey or Alt+keys. It can be recorded with the Input recorder.

```csharp
wnd.find(0, "*- Notepad", "Notepad").Activate();
keys.send("Ctrl+V"); //hotkey
keys.send("Alt+E P"); //Alt+keys
```

Actually with Alt should be used characters, not keys, because keys may not work with other keyboard layouts.

```csharp
keys.send("Alt+^ep"); //Alt+characters
```

To select menu items also can be used arrow keys, Home, End, finally Enter. To activate menu bar, use Alt or F10. To show context menu, use the Apps/Menu or Shift+F10.

Ribbon items (controls) can be selected/clicked in the same way.

```csharp
wnd.find(0, "*- WordPad").Activate();
keys.send("Alt+^hb");
```

Often dialog controls can be selected/clicked in the same way too. Or use Tab or Shift+Tab to select, Space to click, Alt+Down to expand combobox, arrows/Home/End to select list items, Ctrl+Tab to select tab pages.

```csharp
wnd.find(1, "* Properties", "#32770").Activate();
keys.send("Alt+^r");
keys.send("Tab*3 Space");
```

When keys can't be used, try UI element functions. See the <a href='UI elements (find, click, check, focus, select, expand, check menu item, wait for state, etc).md'>UI elements</a> recipe. Or mouse functions, but scripts with mouse functions may stop working after changing window size, layout or DPI.

See also <a href='https://www.google.com/search?q=keyboard+shortcuts+and+hotkeys+in+Windows+and+applications'>keyboard shortcuts and hotkeys in Windows and applications</a>.
