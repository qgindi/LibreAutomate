## Version 0.15.0 (2023-)

### Editor
New tools:
- Get files in folder.

New cookbook recipes:
- End script task.

Improved:
- Improvemets in "drop files", "surround code", completion list sorting and more.

### Library
New classes:
- **print.util**.
- **InputDesktopException**. All functions that send keyboard/mouse input or activate/focus a window now throw this exception if failed because of changed input desktop. It allows to use Ctrl+Alt+Delete or Win+L to end scripts that use these functions even without `script.setup(lockExit: true)`, unless the script handles these exceptions.

New members:
- **script.pause**.
- **script.paused**.
- **print.list**.
- **print.noHex**.
- **print.it** overload for interpolated strings.
- **ExplorerFolder.All**.

New parameters:
- **script.setup**: *exitKey*, *pauseKey*.
- **RegisteredHotkey.Register**: *noRepeat*.

Improved:
- **script.setup** flag **UExcept.Dialog**: the dialog now has links to functions in the call stack.

### Bug fixes

Editor:
- Fixed several exceptions and other bugs.

Library:
- Fixed: **script.trayIcon** and some **script.setup** features don't work in exeProgram script started not from editor.
- Fixed: **popupMenu** check/radio menu item click delegate not invoked if **CheckDontClose** true.


### Breaking changes
