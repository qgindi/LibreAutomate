/// <google>WebView2<> is a web browser control based on Microsoft Edge (Chromium). NuGet: <+nuget>webview\Microsoft.Web.WebView2<>.

/*/ nuget webview\Microsoft.Web.WebView2; /*/

using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;

var b = new wpfBuilder("Window").WinSize(800, 600);
b.Row(-1).Add(out WebView2 k);
b.R.AddSeparator();
b.R.AddOkCancel();
b.End();

b.Window.Loaded += async (_, _) => {
	//Specify WebView2 user data directory. The default is the program directory of this process; usually it's bad.
	var env = await CoreWebView2Environment.CreateAsync(userDataFolder: folders.ThisAppDataRoaming + "WebView2");
	await k.EnsureCoreWebView2Async(env);
	
	k.Source = new("https://www.libreautomate.com");
};

if (!b.ShowDialog()) return;
