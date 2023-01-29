# Mouse get position, wait, cursor, etc
Get mouse cursor position.

```csharp
var p = mouse.xy;
print.it(p);
```

Is mouse left button pressed?

```csharp
if (mouse.isPressed(MButtons.Left)) print.it("yes");
```

Wait for mouse left button down. <a href='/api/Au.mouse.waitForClick.html'>More info</a>.

```csharp
mouse.waitForClick(0, MButtons.Left);
```

Wait for cursor (mouse pointer). <a href='/api/Au.mouse.waitForCursor.html'>More info</a>.

```csharp
mouse.waitForCursor(0, MCursor.Hand); //standard
mouse.waitForCursor(0, -3191259760238497114); //custom
```

Get cursor hash for <b>waitForCursor</b>.

```csharp
if(MouseCursor.GetCurrentVisibleCursor(out var c)) print.it(MouseCursor.Hash(c));
```

Also there are more <a href='/api/Au.mouse.html'>mouse</a> class functions.
