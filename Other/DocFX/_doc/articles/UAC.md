---
uid: uac
title: UAC
---

# User Account Control (UAC)

UAC is a Windows security feature. It does not allow most programs to change important files and Windows settings. Only processes that run as administrator can do it. Also non-admin processes cannot interact with windows of admin processes.

The script editor process normally runs as administrator, unless incorrectly installed. Scripts run in separate processes. They inherit the editor's integrity level (admin/nonadmin), unless a different level is specified in script Properties or the .exe script is launched not by the editor.

Classes and functions that can be used to get UAC-related info: [uacInfo](), [wnd.Uac](), [wnd.UacAccessDenied]().

```csharp
print.it(uacInfo.isAdmin, uacInfo.ofThisProcess.IntegrityLevel);
```

When an admin process creates a new process, normally the new process is admin too. However [run.it]() by default creates a non-admin process.

Non-admin processes have these limitations (and more).
- Can't write to some folders, including Windows and Program Files.
- Can't write to some registry parts, including HKEY_LOCAL_SYSTEM and HKEY_CLASSES_ROOT.
- Can't manipulate services.
- Can't use some Windows API.
- Can't interact with admin windows: send keys and mouse clicks, use UI elements, manipulate windows, send Windows messages, etc.
- While an admin window is active, non-admin processes can't use most triggers, hooks and hotkeys, get key/mouse states.

Admin processes have some UAC-related problems too.
- Admin processes can't easily start non-admin processes. It is possible with workarounds only.
- You can't drag and drop files etc from a non-admin to addmin process.
- Admin processes can't get active COM objects from non-admin processes.
- Admin processes don't see drive letter mappings done by non-admin processes. In scripts use network path, like "\\server\share\file" instead of "Z:".
