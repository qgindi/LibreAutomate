/// <google>WebView2</google> is a web browser control based on Microsoft Edge (Chromium). NuGet: <+nuget>Microsoft.Web.WebView2<>.

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
