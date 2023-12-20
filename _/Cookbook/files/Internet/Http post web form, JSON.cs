/// Use class <see cref="HttpClient"/>. Also there are several extension methods for it, and utility functions in class <see cref="internet"/>.

using System.Net.Http;
//using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http.Json;

/// Post web form data.

var content1 = internet.formContent(("name1", "value1"), ("name2", "value2"));
string s1 = internet.http.Post("https://httpbin.org/anything", content1).Text();
print.it(s1);

/// Post web form data that includes a file (upload).

using var content2 = internet.formContent(("name1", "value1"), ("name2", "value2")).AddFile("name3", @"C:\Test\file.png");
string s2 = internet.http.Post("https://httpbin.org/anything", content2).Text();
print.it(s2);

/// Post an object as JSON.

//var v = new MyData(10, "test"); //create an object of some type
var v = new { //or create an object of anonymous type
	one = 10,
	two = "test",
	three = new string[] { "a", "b" },
};
string s3 = internet.http.Post("https://httpbin.org/anything", internet.jsonContent(v)).Text();
print.it(s3);

//record MyData(int i, string s);

/// Get JSON response elements.

var j = internet.http.Post("https://httpbin.org/anything", null).Json();
print.it((string)j["origin"]);
print.it((string)j["headers"]["Host"]);

/// Get JSON response and convert to object of specified type.

var q = internet.http.Post("https://httpbin.org/anything", null).Json<R1>();
print.it(q);
record R1(string origin, string url);

/// Handle HTTP errors and exceptions.

if (!internet.http.TryPost(out var r, "https://httpbin.org/anything", null)) return;
print.it(r.Json());

/// You can use <b>HttpClient</b> directly too. Functions used in other examples just simplify it. Look for more info/examples on the Internet.

using var http = new HttpClient();
var post = new KeyValuePair<string, string>[] {
	new("name1", "value1"),
	new("name2", "value2"),
};
var r1 = http.PostAsync("https://httpbin.org/post", new FormUrlEncodedContent(post)).Result;
r1.EnsureSuccessStatusCode();
string s4 = r1.Content.ReadAsStringAsync().Result;
print.it(s4);

/// Also you can use libraries, for example <google>RestSharp</google>.
