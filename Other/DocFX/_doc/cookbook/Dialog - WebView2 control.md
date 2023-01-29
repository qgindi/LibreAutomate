# Dialog - WebView2 control
<a href='https://www.google.com/search?q=WebView2'>WebView2</a> is a web browser control based on Microsoft Edge (Chromium). NuGet: <u title='Paste the underlined text in menu -> Tools -> NuGet'>Microsoft.Web.WebView2</u>.

```csharp
/*/ nuget -\Microsoft.Web.WebView2; /*/

using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(700, 600);

b.Row(-1).Add(out WebView2 k);
k.Source = new("https://www.google.com");

b.R.AddSeparator();
b.R.AddOkCancel();
b.End();
if (!b.ShowDialog()) return;
```

