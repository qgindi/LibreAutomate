/// <google>WebView2</google> is a web browser control based on Microsoft Edge (Chromium). NuGet: <+nuget>Microsoft.Web.WebView2<>.

/*/ nuget -\Microsoft.Web.WebView2; /*/

using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(700, 600);
b.Row(-1).Add(out WebView2 k);
b.R.AddSeparator();
b.R.AddOkCancel();
b.End();

b.Loaded += () => {
	var env = Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, @"C:\Temp", null).Result;
	k.EnsureCoreWebView2Async(env);
	k.Source = new("https://www.google.com");
};

if (!b.ShowDialog()) return;
