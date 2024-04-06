## Version 1..0 (2024-)

### Editor
New small features:
- Panel **Recipe**: you can set font in **Options**.
- Panel **Mouse**: context menu with some options.
- **Options > Other > Internet search URL**.
- Menu **Edit > Tidy code > Deduplicate wnd.find**.
- Hotkeys displayed in tooltips of toolbar buttons.
- To unload `AuCpp.dll`: `rundll32.exe "C:\path\to\AuCpp.dll",UnloadAuCppDll 0`.

New tools:
- .

New cookbook recipes:
- .

Improved:
- In tool **Find UI object**, the **Smaller** checkbox has been replaced with hotkey `Shift+F3`. Also now it works with more windows.

Fixed bugs:
- Fixed several bugs.

### Library
New classes:
- .

New members:
- **dialog.options.timeoutTextFormat**.
- **popupMenu.caretRectFunc**.
- **wait.retry**.
- **ScriptEditor.GetFileInfo**.
- **ScriptEditor.TestCurrentFileInProject**.

New parameters:
- **wnd.Move**, **wnd.Resize**, **wnd.MoveL**: *visibleRect* (exclude the transparent frame).

Improved:
- .

Fixed bugs:
- .

### Breaking changes
These static functions have been renamed to fix naming mistakes: **wpfBuider.FormatText** -> **formatTextOf**, **wpfBuider.FormattedText** -> **formattedText**, **popupMenu.DefaultFont** -> **defaultFont**, **popupMenu.DefaultMetrics** -> **defaultMetrics**, **toolbar.DefaultMetrics** -> **defaultMetrics**.