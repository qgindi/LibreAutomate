---
uid: ui_element_issues
---

# UI element issues

UI elements are implemented and live in their applications. Classes [elm]() and [elmFinder]() just communicate with them.

Many applications have various problems with their UI elements: bugs, incorrect/nonstandard/partial implementation, or initially disabled. This library implements workarounds for known problems, where possible.

### Known issues in various applications

**Application:** Chrome web browser. Also Edge, Brave, Opera and other apps that use Chromium. Window class name `"Chrome_WidgetWin_1"`.  
1. Web page UI elements initially are disabled (missing).
   Workarounds:
   - Functions [elmFinder.Find](), [elmFinder.Exists](), [elmFinder.Wait]() and [elmFinder.FindAll]() enable it if used role prefix `"web:"` or `"chrome:"`. Functions [elm.fromXY](), [elm.fromMouse]() and [elm.focused]() enable it if window class name starts with `"Chrome"`. However Chrome does it lazily, therefore shortly after enabling something still may not work. Note: this auto-enabling may fail with future Chrome versions.
   - Start Chrome with command line `--force-renderer-accessibility`.
2. Sometimes [elmFinder.Find]() etc may not find the element if parameter *wait* not used, especially after auto-enabling UI elements. Use *wait*, like `var e1 = w1.Elm["web:LINK", "Example"].Find(5);`.
3. Some new web browser versions add new features or bugs that break something.

**Application:** Firefox web browser.  
1. When Firefox starts, its web page UI elements are unavailable. Creates them only when something tries to find or get an element, but does it lazily, and the find/get function at first fails. Workaround: with [elmFinder.Find]() use parameter *wait*, like `var e1 = w1.Elm["web:LINK", "Example"].Find(5);`.
2. Occasionally Firefox briefly turns off its web page UI elements. Workaround: use parameter *wait*. With other web browsers also it's better to use *wait*.
3. Some new web browser versions add new features or bugs that break something.

**Application:** Applications (or just some windows) that don't have accessible objects but have UI Automation elements.
1. To find UI elements in these applications, need flag [EFFlags.UIA]().

**Application:** Java applications that use AWT/Swing. Window class name starts with `"SunAwt"`.
1. Must be enabled Java Access Bridge (JAB).  
If JAB is missing/disabled/broken, the **Find UI element** tool shows an "enable" link when you try to capture something in a Java window. Or you can enable JAB in **Options > OS**. Or use `jabswitch.exe`. Then restart Java apps. Also may need to restart apps that tried to use Java UI elements.
2. JAB is part of Java. Install Java 64-bit x64 or ARM64 (as your OS).
3. If you'll use JAB in 32-bit script processes (unlikely), also install Java 32-bit. If you'll use JAB in x64 script processes on Windows ARM64 (unlikely), also install Java x64.
4. Not supported on 32-bit OS.
5. JAB bug: briefly shows a console window at PC startup, after every sleep, etc. Or opens an invalid Windows Terminal window. Workaround: in Terminal settings set Default Terminal Application = Windows Console Host.

**Application:** Some controls.
1. UI elements of some controls are not connected to the UI element of the parent control. Then cannot find them if searching in whole window.  
Workaround: search only in that control. For example, use *prop* `"class"` or `"id"`. Or find the control ([wnd.Child]() etc) and search in it.

**Application:** Some controls with flag [EFFlags.NotInProc]().

UI elements of many standard Windows controls have bugs when they are retrieved without loading dll into the target process (see [EFFlags.NotInProc]()). Known bugs:
1. Toolbar buttons don't have `Name` in some cases.
2. [elm.Focus]() and [elm.Select]() often don't work properly.

Workarounds: Don't use [EFFlags.NotInProc](). Or use [EFFlags.UIA]().

**Application:** Where cannot load dll into the target process. For example Windows Store apps.
1. Function [elmFinder.Find]() is much slower, and uses much more CPU when waiting. More info: [EFFlags.NotInProc]().

**Application:** Processes of a different CPU architecture (32/64/ARM64) than this process.
1. To load the dll is used `Au.DllHost.exe`, which makes slower first time.

**Application:** DPI-scaled windows (see [Dpi.IsWindowVirtualized]()).
1. In some cases "element from point" and "get rectangle" functions may not work correctly with such windows. This process must be per-monitor-DPI-aware (LA script processes are).
