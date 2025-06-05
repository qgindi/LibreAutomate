## Version 1.12.0 (2025-05-31)

### Editor
Menu **Run > PiP session** and **Run in PiP**. Creates a separate session in a Picture-in-Picture window, where UI automation scripts can run in the background without interfering with your mouse and keyboard. Requires Windows 8.1 or later.

Updated the icons database. Many new icons.

Icon strings: margin and layers. See menu **Tools > Icons > Custom icon string**.

New tools:
- .

New cookbook recipes:
- .

Improved:
- .

Fixed bugs:
- Menu **Tools > Update icons** may not clear caches of image size != 16.
- Autocompletion list sometimes disappears when pasting.

### Library
New classes:
- .

New members:
- **script.runInPip**, **script.isInPip**.
- **miscInfo.isChildSession**.

New parameters:
- .

Improved:
- .

Fixed bugs:
- **filesystem.enumDirectories**: flag **AllDescendants** does not work.

### Breaking changes
