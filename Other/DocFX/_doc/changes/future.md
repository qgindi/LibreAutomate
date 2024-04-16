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
- Tool **Find UI object**: improved feature "capture smaller element". Use hotkey `Shift+F3`.
- Tool **Find UI object**: option "Use role in elm variable name".

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
- **elm** functions: in the *prop* parameter can be specified **url**. Use to search in side panels, developer tools, settings etc. The **Find UI element** tool now adds it when need.

Improved:
- **elm** with Chrome and other Chromium-based web browsers.

Fixed bugs:
- .

### Breaking changes
These static functions have been renamed:
- **wpfBuider.FormatText** -> **formatTextOf**,
- **wpfBuider.FormattedText** -> **formattedText**,
- **popupMenu.DefaultFont** -> **defaultFont**, 
- **popupMenu.DefaultMetrics** -> **defaultMetrics**, 
- **toolbar.DefaultMetrics** -> **defaultMetrics**.
