## Version 1.9.0 (2025-)

### Editor
Fixed bugs:
- Empty list in window **Active toolbars**.

New options:
- **Options > Other > Documentation**. You can use local documentation of the installed program version instead of the online documentation of the latest program version.

### Library
New members:
- **keys.waitForHotkeys**.
- **HelpUtil.AuHelpBaseUrl**.

New parameters:
- **HttpServerSession.Listen** parameter *started* (callback).

Other changes:
- Removed file `sqlite3.dll`. Now class `sqlite` uses `winsqlite3.dll` of Windows 10+; on older OS auto-downloads.
