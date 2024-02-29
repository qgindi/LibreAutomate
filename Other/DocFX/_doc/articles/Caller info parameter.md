---
uid: caller_info
---

# Caller info parameter

Some functions have optional parameters *f_* and/or *l_* with attribute `[CallerFilePath]` or `[CallerLineNumber]`. They automatically pass source file/line info to the function. Usually the caller should not use them.

For example, menus (class **popupMenu**) and toolbars (class **toolbar**) use it for the **Edit** command (context menu). Triggers use it for menu **Run > Recent > trigger**.

If to add menu/toolbar items or triggers you create an intermediate function, the function should have *f_* and/or *l_* parameters and pass them to the menu/toolbar/trigger function. Example script:

```csharp
var m = new popupMenu("431beeab-e190-495a-be8f-05732ef6a74f");
m["Right click me to edit"] = o => print.it(1);
_AddMenuItem("You can edit me too", o => print.it(2));
_AddMenuItemWrongWay("But can be difficult to edit me", o => print.it(3));
m.Show();

void _AddMenuItem(string label, Action<PMItem> action, [CallerLineNumber] int l_ = 0) {
	m[label, l_: l_] = action;
	m.Last.TextColor = 0x0000ff;
}

void _AddMenuItemWrongWay(string label, Action<PMItem> action) {
	m[label] = action;
	m.Last.TextColor = 0xff0000;
}
```