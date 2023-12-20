/// Use class <see cref="HttpClient"/>. Also there are several extension methods for it, and utility functions in class <see cref="internet"/>.

using System.Net.Http;

/// Download web page.

string html = internet.http.Get("https://www.example.com").Text();
print.it(html);

/// To urlencode URL parameters can be used <see cref="internet.urlAppend"/> or <see cref="System.Net.WebUtility.UrlEncode"/>.

string p1 = "one, two", p2 = "three, four";
var url1 = internet.urlAppend("https://httpbin.org/get", "p1=" + p1, "p2=" + p2);
print.it(url1);
print.it(internet.http.Get(url1).Text());

/// Download and print all response info.

var r = internet.http.Get("https://httpbin.org/anything");
print.it(r);
if (r.IsSuccessStatusCode) {
	print.it("---- TEXT ---");
	print.it(r.Text());
} else {
	print.it($"---- {(int)r.StatusCode} {r.ReasonPhrase} ---");
	print.it(r.Text(ignoreError: true));
}

/// Handle HTTP errors and exceptions.

if (!internet.http.TryGet(out var r2, "https://httpbin.org/anything", printError: true)) return;
print.it(r2.Text());

/// Download file.

string url = "http://speedtest.ftp.otenet.gr/files/test10Mb.db";
string file = folders.Temp + pathname.getName(url);
try { internet.http.Get(url, file); }
catch (Exception e1) { print.warning($"Failed to download. {e1.ToStringWithoutStack()}"); return; }
print.it("downloaded");

/// Add HTTP request headers.

//default headers for all Get/Post/etc
internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");
internet.http.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");

var s1 = internet.http.Get("https://httpbin.org/anything", headers: new[] { "Accept: application/json, text/json", "Cookie: mmm=nnn; xxx=yyy;" }).Text();
print.it(s1);

/// You can use <b>HttpClient</b> directly too. Functions used in other examples just simplify it. Look for more info/examples on the Internet.

using var http = new HttpClient();
string html2 = http.GetStringAsync("https://www.example.com").Result;
print.it(html2);

/// In UI code use async functions to avoid blocking the UI while the Internet function is working.

var b = new wpfBuilder("Window").Columns(120, 120);
CancellationTokenSource cts = null;
b.R.AddButton("Download", async o => {
	o.Button.IsEnabled = false;
	cts = new CancellationTokenSource();
	cts.CancelAfter(10_000); //10 s timeout
	var ctoken = cts.Token;
	try {
		var r4 = await internet.http.GetAsync("http://speedtest.ftp.otenet.gr/files/test10Mb.db", ctoken);
		r4.EnsureSuccessStatusCode();
		var data = await r4.Content.ReadAsByteArrayAsync(ctoken);
		print.it(data.Length);
	}
	catch(TaskCanceledException) { print.it("canceled or timeout"); }
	catch(Exception e) { print.it(e); }
	o.Button.IsEnabled = true;
	cts.Dispose();
	cts = null;
});
b.AddButton("Stop", _ => { cts?.Cancel(); });
b.Window.Closed += (_, _) => { cts?.Cancel(); };
if (!b.ShowDialog()) return;

/// Get JSON response elements.

var j = internet.http.Get("https://httpbin.org/anything").Json();
print.it((string)j["origin"]);
print.it((string)j["headers"]["Host"]);

/// Get JSON response and convert to object of specified type.

var q = internet.http.Get("https://httpbin.org/anything").Json<R1>();
print.it(q);
record R1(string origin, string url);

/// Also you can use libraries, for example <google>RestSharp</google>. See also recipe <+recipe>Parse HTML<>.
