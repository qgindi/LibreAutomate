---
uid: wait_timeout
---

# Wait timeout

Most "wait for" functions have a *timeout* parameter. It is the maximal time to wait, in seconds. If 0, waits indefinitely. If > 0, after that time interval throws **TimeoutException**. If < 0, after that time interval returns the default value of the return type (`false`, `null`, `0`, `default`).

Some "find" functions have a *wait* parameter. It is like *timeout*, but 0 means "don't wait". To wait indefinitely, use some large value, for example `8e88`. Also, "find" functions throw **NotFoundException**, not **TimeoutException**.

The type of these parameters is [Seconds](). It allows to specify wait options.

Examples:
```csharp
//wait for Notepad window
var w = wnd.wait(0, true, "* Notepad");
print.it(w);

//wait for Notepad window max 5 seconds. Then throw exception.
var w = wnd.wait(5, true, "* Notepad");
print.it(w);

//wait for Notepad window max 5 seconds. Then exit.
var w = wnd.wait(-5, true, "* Notepad");
if(w.Is0) { print.it("timeout"); return; }
print.it(w);

//wait for hotkey max 5 seconds. Then exit.
if(!keys.waitForHotkey(-5, "Ctrl+Shift+K")) return;
print.it("hotkey");

//specify wait options
wait.until(new Seconds(0) { Period = 100, MaxPeriod = 100 }, () => keys.isCtrl);
```
