## Version 1.9.0 (2025-)

### Editor
Fixed bugs:
- Empty list in window **Active toolbars**.

### Library
Removed file `sqlite3.dll`. Now class `sqlite` uses `winsqlite3.dll` of Windows 10+; on older OS auto-downloads.
