/// The WPF <b>WebBrowser</b> control is based on Internet Explorer (IE).
/// Note: Many websites don't support IE. Use the control to display web files that are tested and work well with IE.
/// See also recipe <+recipe>WebView2<>.

using System.Windows;
using System.Windows.Controls;

_WebBrowserDisableIE7Emulation();
var b = new wpfBuilder("Window").WinSize(800, 600);
b.Row(-1).Add(out WebBrowser wb).LoadFile("https://www.google.com");
if (!b.ShowDialog()) return;


static void _WebBrowserDisableIE7Emulation() {
	var v = _GetIEVersion()?.Major ?? 0; if(v<8) return;
	v*=1000;
	var e=process.thisExeName;
	const string rk=@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";
	if(Registry.GetValue(rk, e, null) is int t && t==v) return;
	Registry.SetValue(rk, e, v);
}

static Version _GetIEVersion() {
	var s = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Internet Explorer", "svcVersion", null) as string; //IE10+
	s ??= Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Internet Explorer", "Version", null) as string;
	return s == null ? null : new(s);
}
