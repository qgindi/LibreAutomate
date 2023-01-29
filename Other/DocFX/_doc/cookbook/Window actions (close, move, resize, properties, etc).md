# Window actions (close, move, resize, properties, etc)
At first need to <a href='/api/Au.wnd.find.html'>find</a> the window and create a <a href='/api/Au.wnd.html'>wnd</a> variable.

```csharp
var w = wnd.find(1, "*- Notepad", "Notepad");
```

Then type the variable name and dot (.), and select a function from the list. If need help, click the function name in the code editor and press F1.

Close the found window.

```csharp
w.Close();
```

Close all similar windows.

```csharp
var a1 = wnd.findAll(cn: "Notepad", of: "notepad.exe");
foreach (var v in a1) {
	//print.it(v);
	v.Close();
}
```

Move, resize.

```csharp
w.Move(100, 100, workArea: true);
w.Resize(500, 300);
w.Move(100, 100, 500, 300, workArea: true); //move and resize
```

Get rectangle.

```csharp
var r = w.Rect;
print.it(r);
```

Is active?

```csharp
if (w.IsActive) print.it("active");
```

Is closed?

```csharp
if (!w.IsAlive) print.it("closed");
```

Maximize, minimize, restore, hide, show.

```csharp
w.ShowMaximized();
1.s();
w.ShowMinimized();
1.s();
w.ShowNotMinimized();
1.s();
w.ShowNotMinMax();
1.s();
w.Show(false);
1.s();
w.Show(true);
```

Also <b>wnd</b> variables can be used with functions of various classes as arguments.

```csharp
mouse.move(w, 10, 10);

wnd w2 = wnd.getwnd.nextMain(w);
```

