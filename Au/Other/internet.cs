using System.Net.NetworkInformation;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;

namespace Au {
	/// <summary>
	/// This class, together with <see cref="ExtInternet"/> (extension methods), make easier to use <see cref="HttpClient"/> and other .NET Internet classes for tasks like download, post, ping.
	/// </summary>
	public static class internet {
		/// <summary>
		/// Sends an ICMP echo message to the specified website and returns true if successful. Can be used to check Internet connectivity.
		/// </summary>
		/// <param name="hostNameOrAddress">Domain name like "google.com" or IP like "123.45.67.89".</param>
		/// <param name="timeout">Timeout in milliseconds.</param>
		/// <remarks>>
		/// Not all websites support it.
		///
		/// Uses <see cref="Ping"/>.
		/// </remarks>
		public static bool ping(string hostNameOrAddress!! = "google.com", int timeout = 5000) {
			try {
				using var ping = new Ping();
				var reply = ping.Send(hostNameOrAddress, timeout);
				return reply.Status == IPStatus.Success;
			}
			catch { return false; }
		} //also tested http, but slow etc.

		/// <summary>
		/// Sends an ICMP echo message to the specified website and returns true if successful. Gets the roundtrip time.
		/// </summary>
		public static bool ping(out int roundtripTime, string hostNameOrAddress!! = "google.com", int timeout = 5000) {
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
		/// Gets a static singleton <see cref="HttpClient"/> instance that can be used in scripts to download web pages, post web form data, etc.
		/// </summary>
		/// <remarks>
		/// Creates the <b>HttpClient</b> instance with the parameterless constructor. Creates only the first time; later just returns it.
		/// 
		/// <b>internet.http</b> just makes easier to discover internet get/post/etc functions when using this library. You can instead create an <b>HttpClient</b> instance and use its functions in the same way. See the second example. Use the same <b>HttpClient</b> instance when making multiple get/post/etc requests; don't need to dispose if you use it in this way. Libraries should create own <b>HttpClient</b> instances and don't use <b>internet.http</b>, because the program/script may set its properties unsuitable for your library.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0"); //optional, this is just an example
		/// string s = internet.http.Get("https://httpbin.org/anything").Text();
		/// ]]></code>
		/// The same without <b>internet.http</b>.
		/// <code><![CDATA[
		/// var http = new HttpClient();
		/// http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");
		/// string s = http.Get("https://httpbin.org/anything").Text();
		/// ]]></code>
		/// The same again.
		/// <code><![CDATA[
		/// var http = new HttpClient() { BaseAddress = new("https://httpbin.org") };
		/// http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");
		/// string s = http.Get("anything").Text();
		/// ]]></code>
		/// </example>
		public static HttpClient http => _lazyHC.Value; //rejected: public setter
		static Lazy<HttpClient> _lazyHC = new(() => new());

		/// <summary>
		/// Creates an <b>HttpContent</b> for posting web form fields with functions like <see cref="HttpClient.PostAsync(string?, HttpContent?)"/> and <see cref="ExtInternet.Post"/>.
		/// </summary>
		/// <param name="fields">One or more web form field names and values. Can't specify file upload fields here; for it later call <b>AddFile</b>; see example.</param>
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
		/// string s = internet.http.Post("https://httpbin.org/anything", internet.jsonContent(new POINT(10, 20))).Text();
		/// ]]></code>
		/// </example>
		public static JsonContent jsonContent<T>(T x)
			=> JsonContent.Create(x);

		/// <summary>
		/// Joins a URL address and parameters. Urlencodes parameters.
		/// </summary>
		/// <param name="address">URL part without parameters or with some parameters. The function does not modify it.</param>
		/// <param name="parameters">URL parameters to append, like <c>"name1=value1", "name2=value2"</c>. The function urlencodes them.</param>
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
		/// <param name="contentType">Content-Type header, for example "image/png".</param>
		/// <param name="fileName">Filename. If null, gets from <i>file</i>.</param>
		/// <returns>This.</returns>
		/// <remarks>
		///	Opens the file and stores the stream in this <b>MultipartFormDataContent</b> object. Won't auto-close it after uploading. To close files, dispose this <b>MultipartFormDataContent</b> object, for example with the 'using' like in the example. Else the file will remain opened/locked until this process exits or until next garbage collection.
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
		/// Gets content text as string. Downloads it if need.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="ignoreError">Don't call <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.</param>
		/// <exception cref="HttpRequestException">Failed HTTP request. No exception if <i>ignoreError</i> true.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="HttpContent.ReadAsStringAsync()"/>.</exception>
		public static string Text(this HttpResponseMessage t, bool ignoreError = false) {
			if (!ignoreError) t.EnsureSuccessStatusCode();
			return t.Content.ReadAsStringAsync().Result;
		}

		/// <summary>
		/// Gets content data as byte[]. Downloads it if need. If content is text, the array contains that text, usually UTF-8.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="ignoreError">Don't call <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.</param>
		/// <exception cref="HttpRequestException">Failed HTTP request. No exception if <i>ignoreError</i> true.</exception>
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
		/// <exception cref="HttpRequestException">Failed HTTP request. No exception if <i>ignoreError</i> true.</exception>
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
		/// <param name="file">File path.</param>
		/// <returns>This.</returns>
		/// <exception cref="HttpRequestException">Failed HTTP request.</exception>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="HttpContent.ReadAsStream()"/>, <see cref="File.Create(string)"/> and other used functions.</exception>
		/// <remarks>
		/// By default <b>HttpClient</b> and similar functions download content to a memory buffer before returning. To avoid it, use <i>completionOption</i> <see cref="HttpCompletionOption.ResponseHeadersRead"/>, then call this function (it will download the file), and finally dispose the <b>HttpResponseMessage</b>.
		/// </remarks>
		/// <seealso cref="Get(HttpClient, string, string, string[])"/>
		public static HttpResponseMessage Save(this HttpResponseMessage t, string file) {
			t.EnsureSuccessStatusCode();
			using var s1 = t.Content.ReadAsStream();
			using var s2 = File.Create(pathname.normalize(file));
			s1.CopyTo(s2);
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
		public static void AddMany(this HttpRequestHeaders t, params string[] headers) {
			foreach (var s in headers) {
				int i = s?.IndexOf(':') ?? -1; if (i < 1) throw new ArgumentException("headers must be like \"name1: value1\", \"name2: value2\"");
				int j = i + 1; while (s.Eq(j, ' ')) j++;
				t.Add(s[..i], s[j..]);
			}
		}
		//rejected: overload with (string, string)[]. Longer code and no real advantages.
		//rejected: overload Strings. Shorter code if can safely uses string, but longer etc if need to use array because values may contain '|'.

		/// <summary>
		/// Sends a GET request to the specified URL, and gets the response.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="url">URL. To create URL with urlencoded parameters you can use <see cref="internet.urlAppend"/>.</param>
		/// <param name="dontWait">Use <see cref="HttpCompletionOption.ResponseHeadersRead"/>.</param>
		/// <param name="headers">null or an array of request headers in this format: <c>"name: value".</c> Also you can add headers to <see cref="HttpClient.DefaultRequestHeaders"/>, like <c>internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");</c>.</param>
		/// <returns>An <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. To get content use <see cref="Text"/> etc.</returns>
		/// <exception cref="Exception">Exceptions of <see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption)"/>.</exception>
		/// <exception cref="UriFormatException">Invalid URL format.</exception>
		/// <exception cref="Exception">If <i>headers</i> used, exceptions of <see cref="AddMany"/>.</exception>
		/// <example>
		/// <code><![CDATA[
		/// string s = internet.http.Get("https://httpbin.org/anything").Text();
		/// ]]></code>
		/// </example>
		public static HttpResponseMessage Get(this HttpClient t, string url!!, bool dontWait = false, string[] headers = null) {
			var m = new HttpRequestMessage(HttpMethod.Get, url);
			if (headers != null) m.Headers.AddMany(headers);
			return t.Send(m, dontWait ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead);
		}

		/// <summary>
		/// Sends a GET request to the specified URL, and gets the response. Handles HTTP errors and exceptions.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="r">Receives an <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. To get content use <see cref="Text"/> etc. Will be null if failed because of an exception.</param>
		/// <param name="url">URL. To create URL with urlencoded parameters you can use <see cref="internet.urlAppend"/>.</param>
		/// <param name="dontWait">Use <see cref="HttpCompletionOption.ResponseHeadersRead"/>.</param>
		/// <param name="headers">null or an array of request headers in this format: <c>"name: value".</c> Also you can add headers to <see cref="HttpClient.DefaultRequestHeaders"/>, like <c>internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");</c>.</param>
		/// <param name="printError">If failed, call <see cref="print.warning"/>.</param>
		/// <returns>false if failed.</returns>
		/// <exception cref="UriFormatException">Invalid URL format.</exception>
		/// <exception cref="Exception">If <i>headers</i> used, exceptions of <see cref="AddMany"/>.</exception>
		/// <example>
		/// <code><![CDATA[
		/// if (!internet.http.TryGet(out var r, "https://httpbin.org/anything", printError: true)) return;
		/// print.it(r.Text());
		/// ]]></code>
		/// </example>
		public static bool TryGet(this HttpClient t, out HttpResponseMessage r, string url!!, bool dontWait = false, string[] headers = null, bool printError = false) {
			var m = new HttpRequestMessage(HttpMethod.Get, url);
			if (headers != null) m.Headers.AddMany(headers);
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
		/// <param name="resultFile">File path.</param>
		/// <param name="headers">null or an array of request headers in this format: <c>"name: value".</c> Also you can add headers to <see cref="HttpClient.DefaultRequestHeaders"/>, like <c>internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");</c>.</param>
		/// <returns>An <b>HttpResponseMessage</b> object that contains response headers etc. Rarely used.</returns>
		/// <exception cref="Exception">Exceptions of <see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption)"/> and <see cref="Save"/>.</exception>
		/// <exception cref="Exception">If <i>headers</i> used, exceptions of <see cref="AddMany"/>.</exception>
		public static HttpResponseMessage Get(this HttpClient t, string url!!, string resultFile!!, string[] headers = null) {
			var r = Get(t, url, true, headers).Save(resultFile);
			r.Dispose();
			return r;
			//FUTURE: progress.
		}

		/// <summary>
		/// Sends a POST request to the specified URL, and gets the response.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="url">URL.</param>
		/// <param name="content">Data to post. Usually web form data (see <see cref="internet.formContent"/>) or JSON (see <see cref="internet.jsonContent"/>). Can be null.</param>
		/// <param name="headers">null or an array of request headers in this format: <c>"name: value".</c> Also you can add headers to <see cref="HttpClient.DefaultRequestHeaders"/>, like <c>internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");</c>.</param>
		/// <returns>An <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. To get content use <see cref="Text"/> etc.</returns>
		/// <exception cref="Exception">Exceptions of <see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption)"/>.</exception>
		/// <exception cref="Exception">If <i>headers</i> used, exceptions of <see cref="AddMany"/>.</exception>
		/// <example>
		/// <code><![CDATA[
		/// using var content = internet.formContent(("name1", "value1"), ("name2", "value2")).AddFile("name3", @"C:\Test\file.png");
		/// string s = internet.http.Post("https://httpbin.org/anything", content).Text();
		/// ]]></code>
		/// Note: the 'using' in the above example will close the file stream. Don't need it when content does not contain files.
		/// </example>
		public static HttpResponseMessage Post(this HttpClient t, string url!!, HttpContent content, string[] headers = null) {
			var rm = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
			if (headers != null) rm.Headers.AddMany(headers);
			return t.Send(rm);
		}
		//rejected: bool disposeContent = true (auto-close MultipartFormDataContent streams etc).
		//	Users may want to retry etc. Usually this is used in scripts that will exit soon. Usually not so important to close files immediately.
		//rejected. string resultFile = null. It seems POST is rarely used to download files. Also don't need parameter dontWait; HttpClient.PostAsync does not have it too.

		/// <summary>
		/// Sends a POST request to the specified URL, and gets the response. Handles HTTP errors and exceptions.
		/// </summary>
		/// <param name="t"></param>
		/// <param name="r">Receives an <b>HttpResponseMessage</b> object that can be used to get response content (web page HTML, JSON, file, etc), headers etc. To get content use <see cref="Text"/> etc. Will be null if failed because of an exception.</param>
		/// <param name="url">URL.</param>
		/// <param name="content">Data to post. Usually web form data (see <see cref="internet.formContent"/>) or JSON (see <see cref="internet.jsonContent"/>). Can be null.</param>
		/// <param name="headers">null or an array of request headers in this format: <c>"name: value".</c> Also you can add headers to <see cref="HttpClient.DefaultRequestHeaders"/>, like <c>internet.http.DefaultRequestHeaders.Add("User-Agent", "Script/1.0");</c>.</param>
		/// <param name="printError">If failed, call <see cref="print.warning"/>.</param>
		/// <returns>false if failed.</returns>
		/// <exception cref="UriFormatException">Invalid URL format.</exception>
		/// <exception cref="Exception">If <i>headers</i> used, exceptions of <see cref="AddMany"/>.</exception>
		/// <example>
		/// <code><![CDATA[
		/// var content = internet.formContent(("name1", "value1"), ("name2", "value2"));
		/// if (!internet.http.TryPost(out var r, "https://httpbin.org/anything", content, printError: true)) return;
		/// print.it(r.Text());
		/// ]]></code>
		/// </example>
		public static bool TryPost(this HttpClient t, out HttpResponseMessage r, string url!!, HttpContent content, string[] headers = null, bool printError = false) {
			var rm = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
			if (headers != null) rm.Headers.AddMany(headers);
			try {
				r = t.Send(rm);
				if (r.IsSuccessStatusCode) return true;
				if (printError) print.warning($"HTTP POST failed. {(int)r.StatusCode} ({r.StatusCode}), {r.ReasonPhrase}");
			}
			catch (Exception e) {
				r = null;
				if (printError) print.warning($"HTTP POST failed. {e.ToStringWithoutStack()}");
			}
			return false;
		}
	}
}
