# Dialog - window size, position, other properties
Use:
- <a href='/api/Au.wpfBuilder.WinSize.html'>wpfBuilder.WinSize</a> sets window size. Uses WPF units.
- <a href='/api/Au.wpfBuilder.WinXY.html'>wpfBuilder.WinXY</a> sets window position. Uses physical pixels.
- <a href='/api/Au.wpfBuilder.WinRect.html'>wpfBuilder.WinRect</a> sets window position and size. Uses physical pixels.
See also recipe <a href='Dialog - save window placement, control values.md'>save window placement</a>.

```csharp
using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(300, 300).WinXY(200, 200);
b.Row(-1).Add("Text", out TextBox _).Multiline();
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;
```

To set window state, style and other properties can be used <a href='/api/Au.wpfBuilder.WinProperties.html'>wpfBuilder.WinProperties</a>. Or get the <b>Window</b> object and set its properties.

```csharp
var b2 = new wpfBuilder("Window").WinSize(300, 300);
b2.WinProperties(topmost: true);
b2.Window.ShowInTaskbar = false;
b2.Row(-1).Add("Text", out TextBox _).Multiline();
b2.R.AddOkCancel();
b2.End();
if (!b2.ShowDialog()) return;
```

