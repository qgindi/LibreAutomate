using System.Net.NetworkInformation;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using System.IO.Compression;

namespace Au {
	/// <summary>
	/// This class, together with <see cref="ExtInternet"/> (extension methods), make easier to use <see cref="HttpClient"/> and other .NET Internet classes for tasks like download, post, ping.
	/// </summary>
	public static class internet {
		/// <summary>
		/// Sends an ICMP echo message to the specified website and returns <c>true</c> if successful. Can be used to check Internet connectivity.
		/// </summary>
		/// <param name="hostNameOrAddress">Domain name like <c>"google.com"</c> or IP like <c>"123.45.67.89"</c>.</param>
		/// <param name="timeout">Timeout in milliseconds.</param>
		/// <remarks>
		/// Not all websites support it.
		/// 
		/// Uses <see cref="Ping"/>.
		/// </remarks>
		public static bool ping(string hostNameOrAddress = "google.com", int timeout = 5000) {
			try {
				using var ping = new Ping();
				var reply = ping.Send(hostNameOrAddress, timeout);
				return reply.Status == IPStatus.Success;
			}
			catch { return false; }
		} //also tested http, but slow etc.
		
		/// <summary>
		/// Sends an ICMP echo message to the specified website and returns <c>true</c> if successful. Gets the roundtrip time.
		/// </summary>
		/// <param name="roundtripTime"><see cref="PingReply.RoundtripTime"/>.</param>
		/// <inheritdoc cref="ping(string, int)"/>
		public static bool ping(out int roundtripTime, string hostNameOrAddress = "google.com", int timeout = 5000) {
			roundtripTime = 0;
			try {
				using var ping = new Ping();
				var reply = ping.Send(hostNameOrAddress, timeout);
				roundtripTime = (int)Math.Min(reply.RoundtripTime, int.MaxValue);
				return reply.Status == IPStatus.Success;
			}
			catch { return false; }
		}
		
		/// <summary>
		/// Gets a static <see cref="HttpClient"/> instance that can be used in scripts to download web pages, post web form data, etc.
		/// </summary>
		/// <remarks>
		/// Creates <b>HttpClient</b> only the first time; later just returns it.
		/// 
		/// Sets these properties and default headers:
		/// - <see cref="SocketsHttpHandler.AutomaticDecompression"/> = <b>All</b>.
		/// - <c>User-Agent: Au</c>.
		/// 
		/// <b>internet.http</b> makes easier to discover and use internet get/post/etc functions when using this library. You can instead create an <b>HttpClient</b> instance and use its functions in the same way. See the second example. Use the same <b>HttpClient</b> instance when making multiple get/post/etc requests.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// string s = internet.http.Get("https://httpbin.org/anything").Text();
		/// ]]></code>
		/// Without <b>internet.http</b>.
		/// <code><![CDATA[
		/// using var http = new HttpClient();
		/// http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");
		/// string s = http.Get("https://httpbin.org/anything").Text();
		/// ]]></code>
		/// Or.
		/// <code><![CDATA[
		/// using var http = new HttpClient() { BaseAddress = new("https://httpbin.org") };
		/// http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");
		/// string s = http.Get("anything").Text();
		/// ]]></code>
		/// </example>
		public static HttpClient http => _lazyHC.Value; //rejected: public setter
		static Lazy<HttpClient> _lazyHC = new(_CreateHttpClient);
		
		static HttpClient _CreateHttpClient() {
			var h = new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All };
			var r = new HttpClient(h);
			r.DefaultRequestHeaders.Add("User-Agent", "Au"); //without it some servers reject requests
			process.thisProcessExit += _ => r.Dispose();
			return r;
			
			//HttpClient does not close the connection after sending a request. It's good. Most servers have keep-alive timeout 5 or 10 s.
			//	Closes when disposing. And maybe when server closes; and maybe after h.PooledConnectionIdleTimeout etc; not tested.
			//	Does not close when sending request to another server. Supports multiple connections at the same time.
			
			//It seems HttpClient.Send etc are thread-safe.
			//	Tested: if several threads send a request to the same URL, are created several connections.
			//	However threads cannot safely change base address, default headers, etc.
			
			//Another possible problem - DNS changes.
			//	By default, idle connections are closed after 1 minute.
			//	For active connections can set h.PooledConnectionLifetime. Never mind.
		}
		
		/// <summary>
		/// Gets a static <see cref="HttpClient"/> instance that can be used in this library.
		/// </summary>
		internal static HttpClient http_ => _lazyHC_.Value;
		static Lazy<HttpClient> _lazyHC_ = new(_CreateHttpClient);
		
		/// <summary>
		/// Creates an <b>HttpContent</b> for posting web form fields with functions like <see cref="HttpClient.PostAsync(string?, HttpContent?)"/> and <see cref="ExtInternet.Post"/>.
		/// </summary>
		/// <param name="fields">One or more web form field names and values. See example.</param>
		/// <exception cref="ArgumentException">An empty name.</exception>
		/// <example>
		/// <code><![CDATA[
		/// var content = internet.formContent(("name1", "value1"), ("name2", "value2")).AddFile("name3", @"C:\Test\file.png");
		/// string s = internet.http.Post("https://httpbin.org/anything", content).Text();
		/// ]]></code>
		/// </example>
		public static MultipartFormDataContent formContent(params (string name, object value)[] fields) {
			var m = new MultipartFormDataContent();
			foreach (var (n, v) in fields) m.Add(n, v?.ToString());
			return m;
		}
		//rejected: overload with params string[] fields. Not intuitive, need to learn how to separate names/values. Easy with tuples.
		
		//rejected. Does not support files and does not have significant advantages, just slightly smaller data to send.
		//public static FormUrlEncodedContent formContent2(params (string name, string value)[] fields)
		//	=> new(fields.Select(o=>new KeyValuePair<string, string>(o.Item1, o.Item2)));
		
		/// <summary>
		/// Creates an <b>HttpContent</b> for posting an object serialized as JSON. It can be used with functions like <see cref="HttpClient.PostAsync(string?, HttpContent?)"/> and <see cref="ExtInternet.Post"/>.
		/// </summary>
		/// <param name="x">An object of any type that can be serialized to JSON with a <b>JsonSerializer</b>.</param>
		/// <exception cref="Exception">Exceptions of <see cref="JsonContent.Create{T}(T, MediaTypeHeaderValue?, JsonSerializerOptions?)"/>.</exception>
		/// <remarks>
		/// Just calls <see cref="JsonContent.Create{T}(T, MediaTypeHeaderValue?, JsonSerializerOptions?)"/>. You can instead call it directly if want to specify media type or JSON serializer parameters.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var v = new POINT(10, 20);
		/// string s = internet.http.Post("https://httpbin.org/anything", internet.jsonContent(v)).Text();
		/// ]]></code>
		/// </example>
		public static JsonContent jsonContent<T>(T x)
			=> JsonContent.Create(x);
		
		/// <summary>
		/// Creates an <b>HttpContent</b> for posting a JSON string. It can be used with functions like <see cref="HttpClient.PostAsync(string?, HttpContent?)"/> and <see cref="ExtInternet.Post"/>.
		/// </summary>
		/// <param name="json">JSON string.</param>
		/// <example>
		/// <code><![CDATA[
		/// string json = "{ ... }";
		/// string s = internet.http.Post("https://httpbin.org/anything", jsonContent(json)).Text();
		/// ]]></code>
		/// </example>
		public static StringContent jsonContent(string json) => new(json, null, "application/json");
		
		/// <summary>
		/// Creates an <b>HttpContent</b> for posting JSON. It can be used with functions like <see cref="HttpClient.PostAsync(string?, HttpContent?)"/> and <see cref="ExtInternet.Post"/>.
		/// </summary>
		public static StringContent jsonContent(JsonNode json) => jsonContent(json.ToJsonString());
		
		/// <summary>
		/// Joins a URL address and parameters. Urlencodes parameters.
		/// </summary>
		/// <param name="address">URL part without parameters or with some parameters. The function does not modify it.</param>
		/// <param name="parameters">URL parameters to append, like <c>"name1=value1", "name2=value2"</c>. The function urlencodes them (<see cref="WebUtility.UrlEncode"/>).</param>
		/// <returns>String like <c>"address?name1=value1&amp;name2=value2"</c>.</returns>
		/// <exception cref="ArgumentException">Incorrect format of a parameters string.</exception>
		public static string urlAppend(string address, params string[] parameters) {
			var b = new StringBuilder(address);
			char sep = '?'; if (address != null && address.Contains('?')) sep = '&';
			foreach (var s in parameters) {
				int i = s.IndexOf('='); if (i < 1) throw new ArgumentException("parameters must be like \"name1=value1\", \"name2=value2\"");
				b.Append(sep).Append(WebUtility.UrlEncode(s[..i])).Append('=').Append(WebUtility.UrlEncode(s[++i..]));
				sep = '&';
			}
			return b.ToString();
		}
		//rejected: overload with (string, string)[]. Longer code and no real advantages.
		//rejected: overload with Strings. Unsafe, limited, und unclear how to use correctly.
		//rejected: overload with FormattableString. The user code is less readable, may be even longer, unclear, easy to make bugs. Difficult to prevent encoding the address part. Dubious advantage - can use FormattableString directly for 'url' parameters of functions like 'Get'.
	}
}

namespace Au.Types {
	
	/// <summary>
	/// Extension methods for .NET Internet functions.
	/// </summary>
	public static class ExtInternet {
		/// <summary>
		/// Adds a non-file field.
		/// Uses <see cref="MultipartFormDataContent.Add(HttpContent, string)"/>.
		/// </summary>
		/// <returns>This.</returns>
		/// <exception cref="ArgumentException">See <see cref="MultipartFormDataContent.Add(HttpContent, string)"/>.</exception>
		public static MultipartFormDataContent Add(this MultipartFormDataContent t, string name, string value) {
			t.Add(new StringContent(value), name);
			return t;
		}
		
		/// <summary>
		/// Adds a file field.
		/// Uses <see cref="MultipartFormDataContent.Add(HttpContent, string, string)"/>.
		/// Please read remarks about disposing.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="name">Field name.</param>
		/// <param name="file">File path.</param>
		/// <param name="contentType"><c>Content-Type</c> header, for example <c>"image/png"</c>.</param>
		/// <param name="fileName">Filename. If <c>null</c>, gets from <i>file</i>.</param>
		/// <returns>This.</returns>
		/// <remarks>
		/// Opens the file and stores the stream in this <b>MultipartFormDataContent</b> object. Won't auto-close it after uploading. To close files, dispose this <b>MultipartFormDataContent</b> object, for example with <c>using</c> like in the example. Else the file will remain opened/locked until this process exits or until next garbage collection.
		/// </remarks>
		/// <exception cref="ArgumentException">See <see cref="MultipartFormDataContent.Add(HttpContent, string, string)"/>.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="filesystem.loadStream"/>.</exception>
		/// <example>
		/// <code><![CDATA[
		/// using var content = internet.formContent(("name1", "value1"), ("name2", "value2")).AddFile("name3", @"C:\Test\file.png");
		/// string s = internet.http.Post("https://httpbin.org/anything", content).Text();
		/// ]]></code>
		/// </example>
		public static MultipartFormDataContent AddFile(this MultipartFormDataContent t, string name, string file, string contentType = null, string fileName = null) {
			var k = new StreamContent(filesystem.loadStream(file));
			if (contentType != null) k.Headers.ContentType = new MediaTypeHeaderValue(contentType);
			t.Add(k, name, fileName ?? pathname.getName(file));
			return t;
		}
		
		/// <summary>
		/// Adds multiple HTTP request headers.
		/// Uses <see cref="HttpHeaders.Add(string, string?)"/>.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="headers">Headers like <c>"name1: value1", "name2: value2"</c>.</param>
		/// <exception cref="ArgumentException">Incorrect format of a <i>headers</i> string.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="HttpHeaders.Add(string, string?)"/>.</exception>
		public static void AddMany(this HttpRequestHeaders t, params string[] headers) => AddMany(t, (IEnumerable<string>)headers);
		
		/// <inheritdoc cref="AddMany(HttpRequestHeaders, string[])"/>
		public static void AddMany(this HttpRequestHeaders t, IEnumerable<string> headers) {
			foreach (var s in headers) {
				int i = s?.IndexOf(':') ?? -1; if (i < 1) throw new ArgumentException("headers must be like \"name1: value1\", \"name2: value2\"");
				int j = i + 1; while (s.Eq(j, ' ')) j++;
				t.Add(s[..i], s[j..]);
			}
		}
		//rejected: overload with (string, string)[]. Longer code and no real advantages.
		//rejected: overload Strings. Shorter code if can safely uses string, but longer etc if need to use array because values may contain '|'.
		
		#region HttpClient
		
		/// <summary>
		/// Sends a GET request to the specified URL, and gets the response.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="url">URL. To create URL with urlencoded parameters you can use <see cref="internet.urlAppend"/>.</param>
		/// <param name="dontWait">Use <see cref="HttpCompletionOption.ResponseHeadersRead"/>.</param>
		/// <param name="headers">
		/// <c>null</c> or request headers like <c>["name1: value1", "name2: value2"]</c>.
		/// Also you can add headers to <see cref="HttpClient.DefaultRequestHeaders"/>, like <c>internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");</c>.
		/// </param>
		/// <param name="auth">String like <c>"username:password"</c> for basic authentication. Adds <c>Authorization</c> header.</param>
		/// <param name="also">Can set more properties for the request.</param>
		/// <returns>An <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. To get content use <see cref="Text"/> etc.</returns>
		/// <exception cref="Exception">
		/// Exceptions of <see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption)"/>.
		/// If <i>headers</i> used, exceptions of <see cref="AddMany"/>.
		/// </exception>
		/// <exception cref="UriFormatException">Invalid URL format.</exception>
		/// <example>
		/// <code><![CDATA[
		/// string s = internet.http.Get("https://httpbin.org/anything").Text();
		/// ]]></code>
		/// </example>
		public static HttpResponseMessage Get(this HttpClient t, string url, bool dontWait = false, IEnumerable<string> headers = null, string auth = null, Action<HttpRequestMessage> also = null) {
			using var m = _HttpMessage(HttpMethod.Get, url, headers, auth, also);
			return t.Send(m, dontWait ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
		}
		
		static HttpRequestMessage _HttpMessage(HttpMethod method, string url, IEnumerable<string> headers, string auth, Action<HttpRequestMessage> also, HttpContent content = null) {
			Not_.Null(url);
			var m = new HttpRequestMessage(method, url);
			if (headers != null) m.Headers.AddMany(headers);
			if (auth != null) m.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(auth.ToUTF8()));
			if (content != null) m.Content = content;
			also?.Invoke(m);
			return m;
		}
		
		//rejected. This could be used with 'also' parameter, and then could remove 'auth' parameter. But this library is mostly for non-programmers, and should make user code as simple as possible.
		///// <summary>
		///// Adds <c>Authorization</c> header for basic authentication.
		///// </summary>
		//public static void Auth(this HttpRequestMessage t, string user, string password) {
		//	t.Headers.Add("Authorization", "Basic " + Convert.ToBase64String($"{user}:{password}".ToUTF8()));
		//}
		
		/// <summary>
		/// Sends a GET request to the specified URL, and gets the response. Handles HTTP errors and exceptions.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="r">Receives <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. See example. Will be <c>null</c> if failed because of an exception.</param>
		/// <param name="url">URL. To create URL with urlencoded parameters you can use <see cref="internet.urlAppend"/>.</param>
		/// <param name="dontWait">Use <see cref="HttpCompletionOption.ResponseHeadersRead"/>.</param>
		/// <param name="printError">If failed, call <see cref="print.warning"/>.</param>
		/// <inheritdoc cref="Get(HttpClient, string, bool, IEnumerable{string}, string, Action{HttpRequestMessage})" path="/param"/>
		/// <returns><c>false</c> if failed.</returns>
		/// <exception cref="UriFormatException">Invalid URL format.</exception>
		/// <exception cref="Exception">If <i>headers</i> used, exceptions of <see cref="AddMany"/>.</exception>
		/// <example>
		/// <code><![CDATA[
		/// if (!internet.http.TryGet(out var r, "https://httpbin.org/anything", printError: true)) return;
		/// print.it(r.Text());
		/// ]]></code>
		/// </example>
		public static bool TryGet(this HttpClient t, out HttpResponseMessage r, string url, bool dontWait = false, IEnumerable<string> headers = null, bool printError = false, string auth = null, Action<HttpRequestMessage> also = null) {
			using var m = _HttpMessage(HttpMethod.Get, url, headers, auth, also);
			try {
				r = t.Send(m, dontWait ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
				if (r.IsSuccessStatusCode) return true;
				if (printError) print.warning($"HTTP GET failed. {(int)r.StatusCode} ({r.StatusCode}), {r.ReasonPhrase}");
			}
			catch (Exception e) {
				r = null;
				if (printError) print.warning($"HTTP GET failed. {e.ToStringWithoutStack()}");
			}
			return false;
		}
		
		/// <summary>
		/// Sends a GET request to the specified URL, and gets the response. Saves the response content (file, web page, etc) in a file.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="url">URL. To create URL with urlencoded parameters you can use <see cref="internet.urlAppend"/>.</param>
		/// <param name="resultFile">File path. The function uses <see cref="pathname.normalize"/>. Creates parent directory if need.</param>
		/// <inheritdoc cref="Get(HttpClient, string, bool, IEnumerable{string}, string, Action{HttpRequestMessage})" path="/param"/>
		/// <returns>An <b>HttpResponseMessage</b> object that contains response headers etc. Rarely used.</returns>
		/// <exception cref="Exception">
		/// Exceptions of <see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption)"/> and <see cref="Save"/>.
		/// If <i>headers</i> used, exceptions of <see cref="AddMany"/>.
		/// </exception>
		public static HttpResponseMessage Get(this HttpClient t, string url, string resultFile, IEnumerable<string> headers = null, string auth = null, Action<HttpRequestMessage> also = null) {
			Not_.Null(resultFile);
			var r = Get(t, url, true, headers, auth, also).Save(resultFile);
			r.Dispose();
			return r;
		}
		
		/// <summary>
		/// Sends a POST request to the specified URL, and gets the response.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="url">URL.</param>
		/// <param name="content">Data to post. Usually web form data (see <see cref="internet.formContent"/>) or JSON (see <see cref="internet.jsonContent"/>). Can be <c>null</c>.</param>
		/// <param name="dontWait">Use <see cref="HttpCompletionOption.ResponseHeadersRead"/>.</param>
		/// <inheritdoc cref="Get(HttpClient, string, bool, IEnumerable{string}, string, Action{HttpRequestMessage})" path="/param"/>
		/// <returns>An <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. To get content use <see cref="Text"/> etc.</returns>
		/// <exception cref="Exception">
		/// Exceptions of <see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption)"/>.
		/// If <i>headers</i> used, exceptions of <see cref="AddMany"/>.
		/// </exception>
		/// <example>
		/// Post form data.
		/// Note: the <c>using</c> will close the file stream. Don't need it when content does not contain files.
		/// <code><![CDATA[
		/// using var content = internet.formContent(("name1", "value1"), ("name2", "value2")).AddFile("name3", @"C:\Test\file.png");
		/// string s = internet.http.Post("https://httpbin.org/anything", content).Text();
		/// ]]></code>
		/// Post object as JSON.
		/// <code><![CDATA[
		/// var v = new POINT(1, 2);
		/// string s = internet.http.Post("https://httpbin.org/anything", internet.jsonContent(v)).Text();
		/// print.it(s);
		/// ]]></code>
		/// </example>
		public static HttpResponseMessage Post(this HttpClient t, string url, HttpContent content, IEnumerable<string> headers = null, bool dontWait = false, string auth = null, Action<HttpRequestMessage> also = null) {
			using var m = _HttpMessage(HttpMethod.Post, url, headers, auth, also, content);
			return t.Send(m, dontWait ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
		}
		//rejected: bool disposeContent = true (auto-close MultipartFormDataContent streams etc).
		//	Users may want to retry etc. Usually this is used in scripts that will exit soon. Usually not so important to close files immediately.
		//rejected. string resultFile = null. It seems POST is rarely used to download files. Also don't need parameter dontWait; HttpClient.PostAsync does not have it too.
		
		/// <summary>
		/// Sends a POST request to the specified URL, and gets the response. Handles HTTP errors and exceptions.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="r">Receives <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. See example. Will be <c>null</c> if failed because of an exception.</param>
		/// <param name="url">URL.</param>
		/// <param name="content">Data to post. Usually web form data (see <see cref="internet.formContent"/>) or JSON (see <see cref="internet.jsonContent"/>). Can be <c>null</c>.</param>
		/// <param name="printError">If failed, call <see cref="print.warning"/>.</param>
		/// <param name="dontWait">Use <see cref="HttpCompletionOption.ResponseHeadersRead"/>.</param>
		/// <inheritdoc cref="Get(HttpClient, string, bool, IEnumerable{string}, string, Action{HttpRequestMessage})" path="/param"/>
		/// <returns><c>false</c> if failed.</returns>
		/// <exception cref="UriFormatException">Invalid URL format.</exception>
		/// <exception cref="Exception">If <i>headers</i> used, exceptions of <see cref="AddMany"/>.</exception>
		/// <example>
		/// Post form data.
		/// <code><![CDATA[
		/// var content = internet.formContent(("name1", "value1"), ("name2", "value2"));
		/// if (!internet.http.TryPost(out var r, "https://httpbin.org/anything", content, printError: true)) return;
		/// print.it(r.Text());
		/// ]]></code>
		/// Post object as JSON.
		/// <code><![CDATA[
		/// var v = new POINT(1, 2);
		/// if (!internet.http.TryPost(out var r, "https://httpbin.org/anything", internet.jsonContent(v), printError: true)) return;
		/// print.it(r.Text());
		/// ]]></code>
		/// </example>
		public static bool TryPost(this HttpClient t, out HttpResponseMessage r, string url, HttpContent content, IEnumerable<string> headers = null, bool printError = false, bool dontWait = false, string auth = null, Action<HttpRequestMessage> also = null) {
			using var m = _HttpMessage(HttpMethod.Post, url, headers, auth, also, content);
			try {
				r = t.Send(m, dontWait ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
				if (r.IsSuccessStatusCode) return true;
				if (printError) print.warning($"HTTP POST failed. {(int)r.StatusCode} ({r.StatusCode}), {r.ReasonPhrase}");
			}
			catch (Exception e) {
				r = null;
				if (printError) print.warning($"HTTP POST failed. {e.ToStringWithoutStack()}");
			}
			return false;
		}
		
		#endregion
		
		#region HttpResponseMessage
		
		/// <summary>
		/// Gets content text as string. Downloads it if need.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="ignoreError">Don't call <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.</param>
		/// <exception cref="HttpRequestException">Failed HTTP request. No exception if <i>ignoreError</i> <c>true</c>.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="HttpContent.ReadAsStringAsync()"/>.</exception>
		public static string Text(this HttpResponseMessage t, bool ignoreError = false) {
			if (!ignoreError) t.EnsureSuccessStatusCode();
			return t.Content.ReadAsStringAsync().Result;
		}
		
		/// <summary>
		/// Gets content data as <b>byte[]</b>. Downloads it if need. If content is text, the array contains that text, usually UTF-8.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="ignoreError">Don't call <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.</param>
		/// <exception cref="HttpRequestException">Failed HTTP request. No exception if <i>ignoreError</i> <c>true</c>.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="HttpContent.ReadAsByteArrayAsync()"/>.</exception>
		public static byte[] Bytes(this HttpResponseMessage t, bool ignoreError = false) {
			if (!ignoreError) t.EnsureSuccessStatusCode();
			return t.Content.ReadAsByteArrayAsync().Result;
		}
		
		/// <summary>
		/// Parses content, which must be JSON, and returns the root node. Then you can access JSON elements like <c>var y = (string)r["x"]["y"];</c>. Downloads content if need.
		/// Uses <see cref="JsonNode.Parse(ReadOnlySpan{byte}, JsonNodeOptions?, JsonDocumentOptions)"/>.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="ignoreError">Don't call <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.</param>
		/// <exception cref="HttpRequestException">Failed HTTP request. No exception if <i>ignoreError</i> <c>true</c>.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="HttpContent.ReadAsByteArrayAsync()"/>.</exception>
		/// <exception cref="JsonException">Failed to parse JSON.</exception>
		public static JsonNode Json(this HttpResponseMessage t, bool ignoreError = false)
			=> JsonNode.Parse(Bytes(t, ignoreError));
		
		/// <summary>
		/// Parses content, which must be JSON. From it creates/returns an object of type <i>T</i>. Downloads content if need.
		/// Uses <see cref="HttpContentJsonExtensions.ReadFromJsonAsync{T}(HttpContent, JsonSerializerOptions?, CancellationToken)"/>.
		/// </summary>
		/// <param name="t"></param>
		/// <exception cref="HttpRequestException">Failed HTTP request.</exception>
		/// <exception cref="Exception">Exceptions of <b>ReadFromJsonAsync</b>.</exception>
		public static T Json<T>(this HttpResponseMessage t)
			=> t.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<T>().Result;
		
		/// <summary>
		/// Saves content in a file. Downloads if need.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="file">File path. The function uses <see cref="pathname.normalize"/>. Creates parent directory if need.</param>
		/// <returns>This.</returns>
		/// <exception cref="HttpRequestException">Failed HTTP request.</exception>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="HttpContent.ReadAsStream()"/>, <see cref="File.Create(string)"/> and other used functions.</exception>
		/// <remarks>
		/// By default <b>HttpClient</b> and similar functions download content to a memory buffer before returning. To avoid it, use <i>completionOption</i> <see cref="HttpCompletionOption.ResponseHeadersRead"/>, or <see cref="Get(HttpClient, string, bool, IEnumerable{string}, string, Action{HttpRequestMessage})"/> with <i>dontWait</i> <c>true</c>. Then call this function (it will download the file), and finally dispose the <b>HttpResponseMessage</b>.
		/// </remarks>
		/// <seealso cref="Get(HttpClient, string, string, IEnumerable{string}, string, Action{HttpRequestMessage})"/>
		public static HttpResponseMessage Save(this HttpResponseMessage t, string file) {
			t.EnsureSuccessStatusCode();
			filesystem.createDirectoryFor(file = pathname.normalize(file));
			using var s1 = t.Content.ReadAsStream();
			using var s2 = File.Create(file);
			_GetDecompressStream(t, s1).CopyTo(s2);
			return t;
			//rejected: decompress in all funcs. Then cannot use ReadAsByteArrayAsync, ReadAsStringAsync, ReadFromJsonAsync<T>.
		}
		
		static Stream _GetDecompressStream(HttpResponseMessage t, Stream s) {
			var ce = t.Content.Headers.ContentEncoding;
			if (ce.Count == 1) {
				switch (ce.First().Lower()) {
				case "br": return new BrotliStream(s, CompressionMode.Decompress);
				case "gzip": return new GZipStream(s, CompressionMode.Decompress);
				case "deflate": return new DeflateStream(s, CompressionMode.Decompress);
				}
			}
			return s;
		}
		
		/// <summary>
		/// Downloads content to stream and provides the progress.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="stream">Writes to this stream.</param>
		/// <param name="progress">Calls this callback function to report the progress. If <c>null</c>, shows standard progress dialog.</param>
		/// <param name="cancel">Can be used to cancel.</param>
		/// <param name="disposeStream">Call <c>stream.Dispose();</c>.</param>
		/// <returns><c>false</c> if canceled.</returns>
		/// <exception cref="HttpRequestException">Failed HTTP request.</exception>
		/// <exception cref="Exception">Other exceptions.</exception>
		/// <remarks>
		/// By default <b>HttpClient</b> and similar functions download content to a memory buffer before returning. To avoid it, use <i>completionOption</i> <see cref="HttpCompletionOption.ResponseHeadersRead"/>, or <see cref="Get(HttpClient, string, bool, IEnumerable{string}, string, Action{HttpRequestMessage})"/> with <i>dontWait</i> <c>true</c>. Then call this function (it will download the file), and finally dispose the <b>HttpResponseMessage</b>.
		/// 
		/// Cannot provide the progress percentage if the content length is unknown. Top reasons:
		/// - The HTTP server uses chunked transfer encoding.
		/// - The HTTP server uses content compression and the <b>HttpClient</b> is configured to automatically decompress (for example <see cref="internet.http"/>). Instead of <b>internet.http</b> create a <b>HttpClient</b> and optionally set header <c>"Accept-Encoding: br, gzip, deflate"</c>. This function will decompress.
		/// </remarks>
		public static bool Download(this HttpResponseMessage t, Stream stream, Action<ProgressArgs> progress = null, CancellationToken cancel = default, bool disposeStream = false) {
			dialog pd = null;
			Stream decomp = null;
			try {
				ArgumentNullException.ThrowIfNull(stream, nameof(stream));
				t.EnsureSuccessStatusCode();
				if (cancel.IsCancellationRequested) return false;
				long size = t.Content.Headers.ContentLength ?? 0;
				//print.it(size);
				//print.it(t);
				//BAD: content length is unknown for many files if the HttpClient uses AutomaticDecompression (eg internet.http).
				//	The header is removed, because impossible to know the decompressed length now.
				//BAD: content length is unknown if "Transfer-Encoding: chunked". Eg most webservers use chunked for html files.
				//GOOD: usually web servers don't use "Content-Encoding" and "Transfer-Encoding" for big files that are usually compressed, eg exe, zip, png.
				
				using var s1 = t.Content.ReadAsStream();
				
				string filename = null;
				string _DialogText(long bytes) => $"{filename}\n{bytes / 1048576d:0.#} MB";
				if (progress == null) {
					filename = pathname.getName(t.RequestMessage.RequestUri.AbsolutePath);
					pd = dialog.showProgress(size == 0, "Downloading", _DialogText(size));
				}
				
				Stream stream1 = s1, stream2 = stream;
				string ce = t.Content.Headers.ContentEncoding.FirstOrDefault()?.Lower();
				if (ce != null) {
					if (ce is not ("br" or "gzip" or "deflate") || t.Content.Headers.ContentEncoding.Count != 1) throw new NotSupportedException("Content-Encoding");
					if (size > 0 && size < 10_000_000) stream2 = new MemoryStream((int)size); //to display progress we need a temp stream; finally will decompress it to *stream*
					else stream1 = decomp = _DecompStream(ce, s1); //can't display progress. Will decompress directly when reading.
				}
				
				static Stream _DecompStream(string ce, Stream s)
					=> ce switch { "br" => new BrotliStream(s, CompressionMode.Decompress), "gzip" => new GZipStream(s, CompressionMode.Decompress), _ => new DeflateStream(s, CompressionMode.Decompress) };
				
				var b = new byte[16384];
				long have = 0; int n, ppercent = 0, ptime = Environment.TickCount;
				while ((n = stream1.Read(b)) > 0) {
					stream2.Write(b, 0, n);
					if (cancel.IsCancellationRequested) return false;
					have += n;
					int percent = 0, time = 0; bool updateProgress;
					if (size > 0) {
						percent = (int)(have * 100 / size);
						if (updateProgress = percent > ppercent) ppercent = percent;
					} else {
						time = Environment.TickCount;
						if (updateProgress = time - ptime > 200) ptime = time;
					}
					
					if (pd != null) {
						if (!pd.IsOpen) { pd = null; return false; }
						if (updateProgress) {
							if (size > 0) pd.Send.Progress(percent);
							else pd.Send.ChangeText2(_DialogText(have), resizeDialog: false);
						}
					} else {
						if (updateProgress) {
							ProgressArgs pa = new(size, have, percent);
							progress(pa);
							if (pa.Cancel) return false;
						}
						if (cancel.IsCancellationRequested) return false;
					}
				}
				
				if (size == 0) progress?.Invoke(new(have, have, 100));
				
				if (stream2 != stream) {
					stream2.Position = 0;
					decomp = _DecompStream(ce, stream2);
					decomp.CopyTo(stream);
				}
			}
			finally {
				decomp?.Dispose();
				if (disposeStream) stream.Dispose();
				pd?.Send.Close();
			}
			return true;
		}
		
		/// <summary>
		/// Downloads content to file and provides the progress.
		/// </summary>
		/// <param name="file">File path. The function uses <see cref="pathname.normalize"/>. Creates parent directory if need.</param>
		/// <inheritdoc cref="Download(HttpResponseMessage, Stream, Action{ProgressArgs}, CancellationToken, bool)"/>
		/// <example>
		/// <code><![CDATA[
		/// if (!internet.http.Get(url, true).Download(zip)) return;
		/// ]]></code>
		/// </example>
		public static bool Download(this HttpResponseMessage t, string file, Action<ProgressArgs> progress = null, CancellationToken cancel = default) {
			t.EnsureSuccessStatusCode();
			filesystem.createDirectoryFor(file = pathname.normalize(file));
			return Download(t, File.Create(file), progress, cancel, disposeStream: true);
		}
		
		/// <summary>
		/// The async version of <see cref="Download(HttpResponseMessage, Stream, Action{ProgressArgs}, CancellationToken, bool)"/>.
		/// </summary>
		/// <inheritdoc cref="Download(HttpResponseMessage, Stream, Action{ProgressArgs}, CancellationToken, bool)"/>
		public static Task<bool> DownloadAsync(this HttpResponseMessage t, Stream stream, Action<ProgressArgs> progress = null, CancellationToken cancel = default, bool disposeStream = false) {
			if (stream == null || !t.IsSuccessStatusCode) {
				if (disposeStream) stream.Dispose();
				ArgumentNullException.ThrowIfNull(stream, nameof(stream));
				t.EnsureSuccessStatusCode();
			}
			if (progress != null && SynchronizationContext.Current is { } ct && ct.GetType() != typeof(SynchronizationContext)) { //call in this thread
				bool canceled = false;
				var progress0 = progress;
				progress = pa => {
					//runs sync in task thread
					if (canceled) { pa.Cancel = true; return; }
					ct.Post(_ => {
						//runs async in caller's thread
						if (canceled) return;
						progress0(pa);
						if (pa.Cancel) canceled = true;
					}, null);
				};
				
				//note: don't use Progress<T>. With default synccontext it calls in random thread pool thread.
				//	This code works like Progress<T> with other synccontexts.
			}
			return Task.Run(() => Download(t, stream, progress, cancel, disposeStream));
		}
		
		/// <summary>
		/// The async version of <see cref="Download(HttpResponseMessage, string, Action{ProgressArgs}, CancellationToken)"/>.
		/// </summary>
		/// <inheritdoc cref="Download(HttpResponseMessage, string, Action{ProgressArgs}, CancellationToken)"/>
		public static Task<bool> DownloadAsync(this HttpResponseMessage t, string file, Action<ProgressArgs> progress = null, CancellationToken cancel = default) {
			t.EnsureSuccessStatusCode();
			filesystem.createDirectoryFor(file = pathname.normalize(file));
			var stream = File.Create(file);
			return DownloadAsync(t, stream, progress, cancel, disposeStream: true);
		}
		
		#endregion
	}
	
	/// <summary>
	/// Arguments for a progress callback function.
	/// </summary>
	/// <param name="Total">The max expected value. Or 0 if unknown.</param>
	/// <param name="Current">The current value.</param>
	/// <param name="Percent"><c>Current * 100 / Total</c>. Or 0 if unknown.</param>
	public record class ProgressArgs(long Total, long Current, int Percent) {
		/// <summary>
		/// The callback function can use this to cancel the operation.
		/// </summary>
		public bool Cancel { get; set; }
	}
}
