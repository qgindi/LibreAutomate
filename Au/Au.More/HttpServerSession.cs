using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Au.More {
	/// <summary>
	/// Simple HTTP 1.1 server. Can be used for communicating between two scripts or apps on local network, internet or same computer. Also can receive requests from web browsers etc.
	/// </summary>
	/// <remarks>
	/// To receive HTTP messages, create a class with base <b>HttpServerSession</b> and add function <see cref="MessageReceived"/>. Example in <see cref="Listen"/>.
	/// </remarks>
	public abstract class HttpServerSession {
		/// <summary>
		/// Simple HTTP 1.1 server.
		/// </summary>
		/// <param name="port">TCP port.</param>
		/// <param name="ip">A local IPv4 or IPv6 address. If <c>null</c> (default), uses <see cref="IPAddress.IPv6Any"/> and dual mode (supports IPv6 and IPv4 connections).</param>
		/// <exception cref="Exception">Exceptions of <see cref="TcpListener"/> functions. Unlikely.</exception>
		/// <remarks>
		/// Runs all the time and listens for new TCP client connections. For each connected client starts new thread, creates new object of your <b>HttpServerSession</b>-based type, and calls <see cref="Run"/>, which calls <see cref="MessageReceived"/>. Supports keep-alive. Multiple sessions can run simultaneously.
		///
		///	Uses <see cref="TcpListener"/>, not <see cref="HttpListener"/>, therefore don't need administrator privileges, <c>netsh</c> and opening ports in firewall. Just the standard firewall dialog first time.
		///
		/// The HTTP server is accessible from local network computers. Usually not accessible from the internet. To make accessible from the internet, you can use ngrok or similar software. This server does not support https (secure connections), but ngrok makes internet connections secure.
		/// 
		/// If <i>ip</i> is <c>null</c> or an IPv6 address, supports IPv6 and IPv4 connections.
		/// </remarks>
		/// <example>
		/// HTTP server.
		/// <code><![CDATA[
		/// HttpServerSession.Listen<MyHttpSession>();
		/// 
		/// class MyHttpSession : HttpServerSession {
		/// 	//protected override void Run() {
		/// 	//	print.it($"Session started. Client IP: {ClientIP}");
		/// 	//	base.Run();
		/// 	//	print.it("Session ended");
		/// 	//}
		/// 	
		/// 	protected override void MessageReceived(HSMessage m, HSResponse r) {
		/// 		//Auth((u, p) => p == "password206474");
		/// 		
		/// 		print.it(m.Method, m.TargetPath, m.UrlParameters, m.Headers, m.ContentText);
		/// 		
		/// 		string response = "RESPONSE";
		/// 		r.SetContent(response);
		/// 		//or
		/// 		//r.Content = response.ToUTF8();
		/// 		//r.Headers.Add("Content-Type", "text/plain; charset=utf-8");
		/// 	}
		/// }
		/// ]]></code>
		/// HTTP client.
		/// <code><![CDATA[
		/// var res = internet.http.Get("http://127.0.0.1:4455/file.txt?a=1&b=2"/*, auth: "user:pw"*/).Text(); //from same computer
		/// //var res = internet.http.Get("http://ComputerName:4455/file.txt?a=1&b=2"/*, auth: "user:pw"*/).Text(); //from local network
		/// //var res = internet.http.Get("https://1111-11-111-11-111.ngrok-free.app/file.txt?a=1&b=2"/*, auth: "user:pw"*/).Text(); //from internet through ngrok
		/// print.it(res);
		/// ]]></code>
		/// </example>
		public static void Listen<TSession>(int port = 4455, string ip = null) where TSession : HttpServerSession, new() {
			var server = new TcpListener(ip != null ? IPAddress.Parse(ip) : IPAddress.IPv6Any, port);
			
			//support IPv6 and IPv4 connections. It solves the "HttpClient 21 s delay" problem.
			var socket = server.Server;
			if (ip == null || socket.AddressFamily == AddressFamily.InterNetworkV6) socket.DualMode = true;
			
			server.Start();
			try {
				for (; ; ) {
					var client = server.AcceptTcpClient();
					run.thread(() => {
						new TSession()._Run(client);
						Interlocked.Decrement(ref s_nThreads);
					}).Name = "Au.HttpServerSession";
					Interlocked.Increment(ref s_nThreads);
					while (s_nThreads >= (osVersion.is32BitProcess ? 200 : 2000)) 200.ms();
				}
			}
			finally {
				server.Stop();
			}
		}
		static int s_nThreads;
		
		TcpClient _client;
		NetworkStream _ns;
		HSMessage _message; //current message
		
		/// <summary>
		/// Gets the <b>TcpClient</b> object of this session.
		/// </summary>
		protected TcpClient Client => _client;
		
		/// <summary>
		/// Gets the IP address of the client.
		/// </summary>
		protected IPAddress ClientIP => ((System.Net.IPEndPoint)_client.Client.RemoteEndPoint).Address;
		
		/// <summary>
		/// Print warning when something fails. This is for debug.
		/// </summary>
		protected bool Verbose { get; set; }
		
		/// <summary>
		/// Keep-alive timeout, in milliseconds. Default 10_000.
		/// </summary>
		protected int KeepAliveTimeout { get; set; } = 10_000;
		
		/// <summary>
		/// Called when the server receives a HTTP request message.
		/// </summary>
		/// <param name="m">Contains request info and content.</param>
		/// <param name="r">Allows to set response info and content.</param>
		/// <remarks>
		///	Not called if failed to read the message.
		/// 
		/// The server uses try/catch when calling this. Prints unhandled exceptions if <see cref="Verbose"/> <c>true</c>. On unhandled exception sends error 500 (<b>InternalServerError</b>) and closes the connection.
		/// </remarks>
		protected abstract void MessageReceived(HSMessage m, HSResponse r);
		
		/// <summary>
		/// Performs basic authentication. If fails (either the client did not use basic authentication or <i>auth</i> returned <c>false</c>), throws exception. The client will receive error 401 (Unauthorized) and can retry.
		/// </summary>
		/// <param name="auth">Callback function. Receives the user name and password. Returns <c>true</c> to continue or <c>false</c> to fail.</param>
		/// <remarks>
		/// After successful authentication does not repeat it again when the client sends more messages in this session.
		/// </remarks>
		/// <example><see cref="HttpServerSession"/></example>
		protected void Auth(Func<string, string, bool> auth) {
			if (_auth != true) {
				_auth = _message.Headers.TryGetValue("Authorization", out var s)
					&& s.Split2_(' ', out var s1, out var s2, 5, 1)
					&& s1.Eqi("Basic")
					&& Convert.FromBase64String(s2).ToStringUTF8().Split2_(':', out s1, out s2, 0, 0)
					&& auth(s1, s2);
				if (_auth == false) throw new UnauthorizedAccessException();
			}
		}
		bool? _auth;
		
		void _Run(TcpClient client) {
			_client = client;
			_client.ReceiveTimeout = 30_000; //tested: throws when the socked receives 0 bytes in this time. It's not the total time of a Read.
			_client.SendTimeout = 30_000; //it seems this is how long the socket waits until previous chunk is sent. We send in max 64 KB chunks.
			try { Run(); }
			catch (Exception e1) {
				if (Verbose) print.warning(e1, "HttpServerSession: ");
			}
			_client.Close();
		}
		
		/// <summary>
		/// Executes the session: reads requests, calls your <see cref="MessageReceived"/>, writes responses, implements keep-alive.
		/// </summary>
		/// <remarks>
		/// The server uses try/catch when calling this. Prints unhandled exceptions if <see cref="Verbose"/> <c>true</c>. On unhandled exception closes the connection.
		/// </remarks>
		[SkipLocalsInit]
		protected virtual void Run() {
			_ns = _client.GetStream();
			g1:
			HttpStatusCode status;
			try {
				status = _ReadRequest();
			}
			catch (HttpReadException_ eh) {
				status = eh.status;
			}
			if (status == HttpReader_.Disconnected) return;
			
			HSResponse response = new() { Status = status };
			
			if (status == HttpStatusCode.OK) {
				try { MessageReceived(_message, response); }
				catch (UnauthorizedAccessException) when (_auth == false) {
					_auth = null;
					response.Status = HttpStatusCode.Unauthorized;
					response.Headers["WWW-Authenticate"] = "Basic";
				}
				catch (Exception e1) {
					if (Verbose) print.warning(e1, "HttpServerSession: ");
					if (response.Status == HttpStatusCode.OK) response.Status = HttpStatusCode.InternalServerError;
				}
			}
			
			bool keepAlive = KeepAliveTimeout > 0 && status == HttpStatusCode.OK && !(_message.Headers.TryGetValue("Connection", out var v1) && v1.Eqi("close"));
			if (!keepAlive) response.Headers["Connection"] = "close";
			
			_WriteResponse(response);
			_message = null;
			
			if (keepAlive) {
				if (_ns.Socket.Poll(Math.Min(KeepAliveTimeout, int.MaxValue / 1000) * 1000, SelectMode.SelectRead) && _ns.DataAvailable) goto g1; //else timeout or closed or error
			} else if (status != HttpStatusCode.OK) {
				//Cannot close until the client writes all data and reads the response. Else instead of response it may receive error 'connection reset'.
				//	On error often not all request bytes are read. And the size of this message is unknown.
				//	Read all remaining request bytes until the client closes connection.
				//		Client should close when it reads the response, because: 1. It's an error. 2. We send 'Connection: close'.
				//	tested: _ns.Socket.LingerState does not work.
				//	not tested (because this code works well): _ns.Close(timeout);.
				_client.ReceiveTimeout = 10_000; //if client never closes, we'll get timeout exception
				Span<byte> b = stackalloc byte[0x4000];
				while (_ns.Read(b) != 0) {  }
			}
		}
		
		void _WriteResponse(HSResponse r) {
			r.Headers.TryAdd("Date", DateTime.UtcNow.ToString("R"));
			r.Headers.TryAdd("Server", "Au");
			
			byte[] content = r.Content;
			bool hasContent = !content.NE_();
			if (hasContent) {
				//r.Headers.TryAdd("Content-Type", "Content-Type: text/plain; charset=utf-8"); //no. Server cannot guess. The HTML default is application/octet-stream.
				
				//compress
				if (content.Length > 1000 && _message.Headers.TryGetValue("Accept-Encoding", out var ae) && !r.Headers.ContainsKey("Content-Encoding")) {
					var h = ae.Lower().Split(new char[] { ',', ';' }, StringSplitOptions.TrimEntries); //use ';' to split items like "br;q=1.0", and ignore q
					var (content2, s2) =
						h.Contains("br") ? (Convert2.BrotliCompress(content), "br")
						: h.Contains("gzip") ? (Convert2.GzipCompress(content), "gzip")
						: h.Contains("deflate") ? (Convert2.DeflateCompress(content), "deflate")
						: (content, null);
					if (content2.Length < content.Length) {
						content = content2;
						r.Headers["Content-Encoding"] = s2;
					}
				}
			}
			r.Headers["Content-Length"] = content.Lenn_().ToS(); //if no content, set 0, else client may wait even if there is empty line
			
			int bs = 50 + r.Reason.Lenn() + r.Headers.Sum(o => o.Key.Length + o.Value.Length + 4);
			using (var w = new StreamWriter(_ns, Encoding.Latin1, bs, leaveOpen: true)) {
				w.Write($"HTTP/1.1 {(int)r.Status} {r.Status}");
				if (!r.Reason.NE()) w.Write(", " + r.Reason);
				w.WriteLine();
				
				foreach (var (k, v) in r.Headers) w.WriteLine(k + ": " + v);
				w.WriteLine();
			}
			
			if (hasContent) {
				//perf.first();
				//_ns.Write(content); //does not wait until client reads all data. Usually returns after ~1 ms regardless of size etc.
				for (int i = 0; i < content.Length;) {
					int n = Math.Min(0x10000, content.Length - i);
					_ns.Write(content, i, n); //waits if previous buffer still not sent
					i += n;
				}
				//perf.nw();
			}
		}
		
		[SkipLocalsInit]
		unsafe HttpStatusCode _ReadRequest() {
			_message = new();
			HttpReader_ reader = new(_ns);
			
			//read the request line
			{
				string s; do s = reader.ReadLine(); while (s.Length == 0);
				if (!s.RxMatch(@"^(GET|HEAD|POST|PUT|DELETE|CONNECT|OPTIONS|TRACE|PATCH) (.+) HTTP/(\d\.\d)$", out RXMatch m)) return HttpStatusCode.BadRequest;
				if (m[3].Value is not ("1.1" or "1.0")) return HttpStatusCode.HttpVersionNotSupported;
				_message.Method = m[1].Value;
				_message.RawTarget = s = m[2].Value;
				int i = s.IndexOf('?');
				if (i >= 0) {
					_message.UrlParameters = HSMessage.ParseUrlParameters_(s.AsSpan(i + 1));
					s = s[..i];
				}
				_message.TargetPath = WebUtility.UrlDecode(s);
			}
			
			//read headers
			var headers = _message.Headers;
			reader.ReadHeaders(headers);
			
			//read content
			_message.Content = reader.ReadContent(headers);
			
			//print.it("Target", message.RawTarget, message.TargetPath, message.UrlParameters);
			//print.it("Headers", message.Headers);
			//print.it("Content", message.ContentText);
			
			return HttpStatusCode.OK;
		}
	}
	
	/// <summary>
	/// Reads an HTTP request or response message.
	/// All functions may throw <see cref="HttpReadException_"/> and exceptions of <b>NetworkStream.Read</b>.
	/// The class was designed to read request, therefore throws exceptions such as <b>BadRequest</b>, but can be used to read response too.
	/// Uses 16 KB of stack. Consider <c>[SkipLocalsInit]</c>.
	/// </summary>
	unsafe ref struct HttpReader_ {
		readonly NetworkStream _ns;
		Span<byte> _b, _line;
		int _n; //current buffered bytes
		int _pos; //current position in _b (0.._n)
		const int c_bsize = 0x4000; //16 K
		fixed byte __b[c_bsize];
		
		public const HttpStatusCode Disconnected = 0;
		
		public HttpReader_(NetworkStream ns) {
			_ns = ns;
			fixed (byte* p = __b) _b = new(p, c_bsize);
		}
		
		void _Buffer() {
			if (_n > 0) {
				_b = _b.Slice(_n);
				if (_b.IsEmpty) {
					if (_line.Length == c_bsize) throw new HttpReadException_(HttpStatusCode.RequestHeaderFieldsTooLarge);
					fixed (byte* p = __b) _b = new(p, c_bsize);
					_line.CopyTo(_b);
					int ll = _line.Length;
					_line = _b;
					_b = _b.Slice(ll);
				}
				_pos = 0;
			}
			_n = _ns.Read(_b);
			if (_n == 0) throw new HttpReadException_(Disconnected);
		}
		
		//note: would be simpler with _ns.ReadByte(), but it is slow. Therefore we _ns.Read() chunks into __b, and then read bytes from there.
		int _ReadByte() {
			if (_pos == _n) _Buffer();
			return _b[_pos++];
		}
		
		public string ReadLine() {
			_line = _b.Slice(_pos);
			for (int len = 0; ; len++) {
				int c = _ReadByte();
				if (c is 10 or 13) {
					_ReadRN(c);
					return Encoding.Latin1.GetString(_line.Slice(0, len));
				}
			}
		}
		
		void _ReadRN(int c) {
			if (c == 10) return;
			if (c == 13 && _ReadByte() == 10) return;
			throw new HttpReadException_(HttpStatusCode.BadRequest);
		}
		
		public void ReadRN() {
			_line = _b.Slice(_pos);
			_ReadRN(_ReadByte());
		}
		
		public byte[] Read(int length) {
			var a = GC.AllocateUninitializedArray<byte>(length);
			Span<byte> span = a;
			
			//at first read from _b
			int n = _n - _pos;
			if (n > 0) {
				n = Math.Min(n, length);
				_b.Slice(_pos, n).CopyTo(a);
				_pos += n;
				span = span.Slice(n);
			}
			
			//then read from _ns, if did not read all from _b
			while (!span.IsEmpty) {
				n = _ns.Read(span);
				if (n == 0) throw new HttpReadException_(Disconnected);
				span = span.Slice(n);
			}
			
			return a;
		}
		
		/// <summary>
		/// Reads headers until empty line.
		/// </summary>
		/// <param name="headers">The function adds headers here.</param>
		public void ReadHeaders(Dictionary<string, string> headers) {
			string header = null;
			for (; ; ) {
				var s = ReadLine();
				if (s.Length == 0) break;
				if (s[0] is ' ' or '\t') {
					if (header == null) throw new HttpReadException_(HttpStatusCode.BadRequest);
					header = string.Concat(header, " ", s.AsSpan(1));
				} else {
					_Header();
					header = s;
				}
			}
			_Header();
			
			void _Header() {
				if (header != null) {
					int i = header.IndexOf(':'); if (i <= 0 || header[i - 1] <= ' ') throw new HttpReadException_(HttpStatusCode.BadRequest);
					var k = header[..i];
					var v = header.AsSpan(i + 1).Trim().ToString();
					if (!headers.TryAdd(k, v)) headers[k] += ", " + v;
				}
			}
		}
		
		/// <summary>
		/// Reads content and trailing headers.
		/// </summary>
		/// <param name="headers">The function gets content length etc from here. Also reads trailing headers here.</param>
		/// <returns><c>null</c> if <i>headers</i> don't contain content-length or transfer-encoding.</returns>
		public byte[] ReadContent(Dictionary<string, string> headers) {
			byte[] content = null;
			
			//get content info etc from headers
			int contentLength = 0;
			bool chunked = false, trailer = false;
			foreach (var (k, v) in headers) {
				if (k.Eqi("Content-Length")) contentLength = v.ToInt();
				else if (k.Eqi("Transfer-Encoding")) {
					if (v.Eq("chunked")) chunked = true;
					else throw new HttpReadException_(HttpStatusCode.NotImplemented, k);
				} else if (k.Eqi("Trailer")) trailer = true;
			}
			
			//read content
			if (chunked || contentLength > 0) {
				if (chunked) {
					List<byte[]> chunks = new();
					for (; ; ) {
						var s = ReadLine();
						if (!s.ToInt(out int len, flags: STIFlags.IsHexWithout0x | STIFlags.DontSkipSpaces)) throw new HttpReadException_(HttpStatusCode.BadRequest);
						if (len == 0) break;
						chunks.Add(Read(len));
						ReadRN();
					}
					
					if (chunks.Count > 0) {
						byte[] ac = chunks[0];
						if (chunks.Count > 1) {
							ac = new byte[chunks.Sum(o => o.Length)];
							int i = 0;
							foreach (var a in chunks) { a.CopyTo(ac, i); i += a.Length; }
						}
						content = ac;
					}
					
					if (trailer) { //headers after content
						ReadHeaders(headers);
					} else {
						ReadRN();
					}
				} else {
					content = Read(contentLength);
				}
			}
			
			return content;
		}
	}
	
	class HttpReadException_ : Exception {
		public HttpReadException_(HttpStatusCode status, string message = null) : base(message) { this.status = status; }
		public readonly HttpStatusCode status;
	}
}

namespace Au.Types {
	/// <summary>
	/// See <see cref="HttpServerSession.MessageReceived"/>.
	/// </summary>
	public class HSMessage {
		/// <summary>
		/// Method, like <c>"GET"</c> or <c>"POST"</c>.
		/// </summary>
		public string Method { get; internal set; }
		
		/// <summary>
		/// Target, like <c>"/file.html"</c> or <c>"/file.html?a=1&amp;b=2"</c> or <c>"/"</c>. May be URL-encoded.
		/// </summary>
		public string RawTarget { get; internal set; }
		
		/// <summary>
		/// Target without URL parameters, like <c>"/file.html"</c> or <c>"/"</c>. Not URL-encoded.
		/// </summary>
		public string TargetPath { get; internal set; }
		
		/// <summary>
		/// URL parameters (query string). Not URL-encoded.
		/// </summary>
		/// <value><c>null</c> if there are no URL parameters.</value>
		public Dictionary<string, string> UrlParameters { get; internal set; }
		
		/// <summary>
		/// Headers. Case-insensitive.
		/// </summary>
		public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
		
		/// <summary>
		/// Raw content. For example POST data as UTF-8 text or binary.
		/// </summary>
		/// <value><c>null</c> if the message is without content.</value>
		public byte[] Content { get; internal set; }
		
		/// <summary>
		/// <c>Content-Type</c> header info.
		/// </summary>
		/// <value><c>null</c> if <c>Content-Type</c> header is missing or invalid.</value>
		public HSContentType ContentType => _contentType ??= HSContentType.Create(Headers);
		HSContentType _contentType;
		
		/// <summary>
		/// <see cref="Content"/> converted to text.
		/// </summary>
		/// <value><c>null</c> if there is no content.</value>
		/// <remarks>If text encoding unspecified, uses UTF-8; if specified invalid, uses ASCII.</remarks>
		public string Text => _contentText ??= Content == null ? null : (ContentType?.Encoding ?? Encoding.UTF8).GetString(Content);
		string _contentText;
		
		/// <summary>
		/// JSON-deserializes <see cref="Content"/> to object of type <b>T</b>.
		/// </summary>
		/// <returns><c>default(T)</c> if the request does not have body data.</returns>
		/// <exception cref="Exception">Exceptions of <see cref="JsonSerializer.Deserialize{TValue}(Stream, JsonSerializerOptions?)"/>.</exception>
		public T Json<T>() => Content == null ? default : JsonSerializer.Deserialize<T>(Content, InternetUtil_.JsonSerializerOptions);
		
		/// <summary>
		/// JSON-deserializes <see cref="Content"/> to object of specified type.
		/// </summary>
		/// <returns><c>null</c> if the request does not have body data.</returns>
		/// <exception cref="Exception">Exceptions of <see cref="JsonSerializer.Deserialize(Stream, Type, JsonSerializerOptions?)"/>.</exception>
		public object Json(Type type) => Content == null ? default : JsonSerializer.Deserialize(Content, type, InternetUtil_.JsonSerializerOptions);
		
		/// <summary>
		/// Keys/values from POST content with <c>Content-Type: application/x-www-form-urlencoded</c>.
		/// </summary>
		/// <value><c>null</c> if the message has no content of this type.</value>
		public Dictionary<string, string> Urlencoded {
			get {
				if (_contentUrlParameters == null && Content != null && Headers.TryGetValue("Content-Type", out var v) && v.Starts("application/x-www-form-urlencoded", true)) {
					_contentUrlParameters = ParseUrlParameters_(Encoding.Latin1.GetString(Content));
				}
				return _contentUrlParameters;
			}
		}
		Dictionary<string, string> _contentUrlParameters;
		
		internal static Dictionary<string, string> ParseUrlParameters_(RStr s) {
			Dictionary<string, string> d = null;
			for (int i = 0, j; i < s.Length; i = j + 1) {
				int q = -1;
				for (j = i; j < s.Length && s[j] != '&'; j++) if (s[j] == '=' && q < 0) q = j;
				if (q > 0) (d ??= new())[WebUtility.UrlDecode(s.Slice(i, q - i).ToString())] = WebUtility.UrlDecode(s.Slice(++q, j - q).ToString());
			}
			return d;
		}
		
		/// <summary>
		/// Parts of multipart content. For example of POST content with <c>Content-Type: multipart/form-data</c>.
		/// </summary>
		/// <value><c>null</c> if the message has no multipart content.</value>
		public Dictionary<string, HSContentPart> Multipart {
			get {
				if (_contentParts == null && Content != null && Headers.TryGetValue("Content-Type", out var v) && v.Starts("multipart/", true)) {
					_contentParts = _GetContentMultipart();
				}
				return _contentParts;
			}
		}
		Dictionary<string, HSContentPart> _contentParts;
		
		Dictionary<string, HSContentPart> _GetContentMultipart() {
			if (Content == null || ContentType?.Boundary is not string sb || Content.Length < sb.Length * 2 + 8) return null;
			//print.it($"'{Content.ToStringUTF8()}'");
			Dictionary<string, HSContentPart> a = null;
			//need to parse bytes, not string, because part bodies can be binary or use various encodings
			RByte k = Content, b = Encoding.Latin1.GetBytes("--" + sb), b0 = b.Slice(2);
			if (!_FindBound(k, b, 0, out int startBound, out int endBound, out bool last)) return null;
			for (int index = 0; !last;) {
				int startPart = endBound;
				if (!_FindBound(k, b, startPart, out startBound, out endBound, out last)) return null;
				var part = k.Slice(startPart, startBound - startPart);
				//print.it($"<<{part.ToStringUTF8()}>>");
				int i = 0;
				if (!part.StartsWith("\r\n"u8)) {
					i = part.IndexOf("\r\n\r\n"u8) + 2; if (i < 2) continue;
				}
				var dh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				if (i != 0) {
					var sh = Encoding.Latin1.GetString(part.Slice(0, i - 2)).RxReplace(@"\n\h", " ");
					foreach (var v in sh.Lines(true)) {
						if (v.Split2_(':', out var s1, out var s2, 1, 0)) dh[s1] = s2;
					}
				}
				HSContentPart p = new(index++, dh, part.Slice(i + 2).ToArray());
				(a ??= new())[p.Name] = p;
			}
			return a;
			
			static bool _FindBound(RByte k, RByte b, int i, out int start, out int end, out bool last) {
				start = end = 0; last = false;
				for (; i < k.Length; i = end) {
					int j = k.Slice(i).IndexOf(b);
					if (j < 0) break;
					start = i + j;
					end = start + b.Length + 2;
					if (end > k.Length) break;
					if (start == 0 || (start >= 2 && k[start - 1] == '\n' && k[start - 2] == '\r')) {
						if (start >= 2) start -= 2;
						if (k[end - 1] == '\n' && k[end - 2] == '\r') return true;
						if (k[end - 1] == '-' && k[end - 2] == '-') { last = true; return true; }
					}
				}
				return false;
			}
		}
	}
	
	/// <summary>
	/// Contains a single part of a multipart POST data.
	/// </summary>
	/// <param name="Index">0-based index of this part in the list of parts.</param>
	/// <param name="Headers">Headers of this part.</param>
	/// <param name="Content">Raw content of this part. For example UTF-8 text.</param>
	public record class HSContentPart(int Index, Dictionary<string, string> Headers, byte[] Content) {
		/// <inheritdoc cref="HSMessage.ContentType"/>
		public HSContentType ContentType => _contentType ??= HSContentType.Create(Headers);
		HSContentType _contentType;
		
		/// <summary>
		/// <see cref="Content"/> converted to text.
		/// </summary>
		/// <value><c>null</c> if there is no content.</value>
		/// <remarks>If text encoding unspecified, uses UTF-8; if specified invalid, uses ASCII.</remarks>
		public string Text => _contentText ??= Content == null ? null : (ContentType?.Encoding ?? Encoding.UTF8).GetString(Content);
		string _contentText;
		
		System.Net.Mime.ContentDisposition _ContentDisposition() {
			if (_contentDisposition == null && Headers.TryGetValue("Content-Disposition", out var s)) try { _contentDisposition = new(s); } catch {  }
			return _contentDisposition;
		}
		System.Net.Mime.ContentDisposition _contentDisposition;
		
		/// <summary>
		/// Gets name from <c>Content-Disposition</c> header.
		/// </summary>
		/// <value>If <c>Content-Disposition</c> header or name is missing, returns <c>Index.ToS()</c>.</value>
		/// <remarks>
		///	Decodes <c>"=?utf-8?B?base64?="</c>.
		/// </remarks>
		public string Name => _name ??= _DecodeMime(_ContentDisposition()?.Parameters["name"] ?? Index.ToS());
		string _name;
		
		/// <summary>
		/// Gets filename from <c>Content-Disposition</c> header.
		/// </summary>
		/// <value><c>null</c> if <c>Content-Disposition</c> header or filename is missing.</value>
		/// <remarks>
		///	Decodes <c>"utf-8''urlencoded"</c> or <c>"=?utf-8?B?base64?="</c>.
		/// </remarks>
		public string FileName {
			get {
				if (_fileName == null && _ContentDisposition() is { } cd) {
					if (cd.Parameters["filename*"] is string s && s.Starts("utf-8''")) { //never mind: can be "any-charset'language'"
						try { _fileName = WebUtility.UrlDecode(s[7..]); } catch {  }
					}
					_fileName ??= _DecodeMime(cd.Parameters["filename"]);
				}
				return _fileName;
			}
		}
		string _fileName;
		
		static string _DecodeMime(string s) {
			if (s != null && s.Starts("=?utf-8?B?", true) && s.Ends("?=")) {
				try { return Convert.FromBase64String(s[10..^2]).ToStringUTF8(); } catch {  }
			}
			return s;
		}
	}
	
	/// <summary>
	/// Contains properties of HTTP <c>Content-Type</c> header.
	/// See <see cref="HSMessage.ContentType"/>.
	/// </summary>
	public class HSContentType {
		/// <summary>
		/// Creates from <c>Content-Type</c> header.
		/// </summary>
		/// <value><c>null</c> if <c>Content-Type</c> header is missing or invalid.</value>
		public static HSContentType Create(Dictionary<string, string> headers) {
			if (headers.TryGetValue("Content-Type", out var s)) {
				try { return new(new(s)); }
				catch {  }
			}
			return null;
		}
		
		HSContentType(System.Net.Mime.ContentType t) {
			MediaType = t.MediaType;
			Boundary = t.Boundary;
			Charset = t.CharSet;
			Encoding = _GetEncoding(t);
		}
		
		///
		public string MediaType { get; }
		
		/// <summary>
		/// Returns the boundary parameter without double quotes, or <c>null</c> if not specified.
		/// </summary>
		public string Boundary { get; }
		
		/// <summary>
		/// Returns the <c>charset</c> parameter, or <c>null</c> if not specified.
		/// </summary>
		public string Charset { get; }
		
		/// <summary>
		/// Gets text encoding.
		/// </summary>
		/// <value>Returns:
		/// <br/>• <c>null</c> if multipart content (<b>Boundary</b> not <c>null</c>).
		/// <br/>• UTF-8 if <c>charset</c> is <c>utf-8</c> or not specified.
		/// <br/>• <b>Encoding</b> that matches <c>charset</c>.
		/// <br/>• ASCII if <c>charset</c> is invalid.
		/// </value>
		public Encoding Encoding { get; }
		
		Encoding _GetEncoding(System.Net.Mime.ContentType t) {
			var s = Charset;
			if (s == null) return t.Boundary != null ? null : Encoding.UTF8;
			if (s.Eqi("utf-8")) return Encoding.UTF8;
			if (s.Eqi("us-ascii")) return Encoding.ASCII;
			if (s.Eqi("iso-8859-1")) return Encoding.Latin1;
			return StringUtil.GetEncoding(s) ?? Encoding.ASCII;
		}
	}
	
	/// <summary>
	/// See <see cref="HttpServerSession.MessageReceived"/>.
	/// </summary>
	public class HSResponse {
		/// <summary>
		/// Response status code. Initially <b>OK</b>.
		/// </summary>
		public HttpStatusCode Status { get; set; }
		
		/// <summary>
		/// Response reason phrase. Initially <c>null</c>.
		/// </summary>
		public string Reason { get; set; }
		
		/// <summary>
		/// Response headers. Initially empty.
		/// The server later may add <c>Date</c>, <c>Server</c>, <c>Content-Encoding</c>, <c>Content-Length</c>.
		/// </summary>
		public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
		
		/// <summary>
		/// Raw response content.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// r.Content = text.ToUTF8();
		/// ]]></code>
		/// </example>
		/// <remarks>
		/// The server may send this data compressed (it depends on headers etc).
		/// </remarks>
		public byte[] Content { get; set; }
		
		/// <summary>
		/// Sets response content text.
		/// </summary>
		/// <param name="content">Sets <see cref="Content"/>: <c>Content = content.ToUTF8();</c>.</param>
		/// <param name="contentType">If not <c>null</c>, sets <c>Content-Type</c> header.</param>
		public void SetContentText(string content, string contentType = "text/plain; charset=utf-8") {
			Content = content?.ToUTF8();
			if (contentType != null) Headers["Content-Type"] = contentType;
		}
		
		/// <summary>
		/// JSON-serializes object of type <b>T</b>, and sets <see cref="Content"/>. Also sets <c>Content-Type</c> header.
		/// </summary>
		/// <param name="obj">Object of type <b>T</b>.</param>
		/// <param name="contentType">If not <c>null</c>, sets <c>Content-Type</c> header.</param>
		/// <exception cref="Exception">Exceptions of <see cref="JsonSerializer.SerializeToUtf8Bytes{TValue}(TValue, JsonSerializerOptions?)"/>.</exception>
		public void SetContentJson<T>(T obj, string contentType = "application/json; charset=utf-8") {
			Content = JsonSerializer.SerializeToUtf8Bytes(obj, InternetUtil_.JsonSerializerOptions);
			if (contentType != null) Headers["Content-Type"] = contentType;
		}
		
		/// <summary>
		/// JSON-serializes object of specified type, and sets <see cref="Content"/>. Also sets <c>Content-Type</c> header.
		/// </summary>
		/// <param name="obj">Object.</param>
		/// <param name="type"><i>obj</i> type.</param>
		/// <param name="contentType">If not <c>null</c>, sets <c>Content-Type</c> header.</param>
		/// <exception cref="Exception">Exceptions of <see cref="JsonSerializer.SerializeToUtf8Bytes{TValue}(TValue, JsonSerializerOptions?)"/>.</exception>
		public void SetContentJson<T>(object obj, Type type, string contentType = "application/json; charset=utf-8") {
			Content = JsonSerializer.SerializeToUtf8Bytes(obj, type, InternetUtil_.JsonSerializerOptions);
			if (contentType != null) Headers["Content-Type"] = contentType;
		}
	}
	
	static class InternetUtil_ {
		static readonly Lazy<JsonSerializerOptions> s_defaultSerializerOptions = new(() => new(JsonSerializerDefaults.Web));
		
		public static JsonSerializerOptions JsonSerializerOptions => s_defaultSerializerOptions.Value;
	}
}
