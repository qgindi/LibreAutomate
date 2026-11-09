using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Xml.Linq;

namespace LA.Shared;

/// <summary>
/// Uploads or lists blobs in Azure Blob Storage using the REST API.
/// </summary>
class LaCloudAzure {
	const string c_version = "2021-08-06";
	
	readonly string _account, _endpoint;
	readonly byte[] _key;
	
	/// <summary>
	/// Gets connection string from env var AZURE_BLOB_CS_LA, and parses.
	/// </summary>
	public LaCloudAzure() {
		string connectionString = Environment.GetEnvironmentVariable("AZURE_BLOB_CS_LA") ?? throw new Exception("no env var AZURE_BLOB_CS_LA");
		
		var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
			int i = part.IndexOf('=');
			if (i > 0) d[part[..i].Trim()] = part[(i + 1)..].Trim(); // value may contain '=' (base64 key)
		}
		_account = d["AccountName"];
		_key = Convert.FromBase64String(d["AccountKey"]);
		_endpoint = d.TryGetValue("BlobEndpoint", out var be)
			? be.TrimEnd('/')
			: $"https://{_account}.blob.{d.GetValueOrDefault("EndpointSuffix", "core.windows.net")}";
	}
	
	/// <summary>
	/// URL-escapes path parts of blob name.
	/// </summary>
	/// <param name="blobName">Like "directory/file.7z".</param>
	public static string UrlEscapeBlobName(string blobName) => string.Join("/", blobName.Split('/').Select(Uri.EscapeDataString));
	
	/// <summary>
	/// Uploads a file as a block blob. Replaces if exists.
	/// </summary>
	/// <param name="container">Container name.</param>
	/// <param name="directory">Directory, like <c>"abc"</c> or <c>"abc/def"</c> or <c>null</c>.</param>
	/// <param name="file">Local file path.</param>
	/// <param name="contentType">Content type. Optional for <c>7z</c> and <c>zip</c> files.</param>
	public void Upload(string container, string directory, string file, string contentType = null) {
		var blobName = Path.GetFileName(file); if (!directory.NE()) blobName = directory + "/" + blobName;
		contentType ??= Path.GetExtension(file).Lower() switch { ".7z" => "application/x-compressed", ".zip" => "application/x-zip-compressed", _ => throw new ArgumentException("For this file type need `contentType`") };
		using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
		_Upload(container, blobName, fs, contentType);
	}
	
	void _Upload(string container, string blobName, Stream stream, string contentType) {
		long length = stream.Length - stream.Position;
		
		var uri = new Uri(_endpoint + "/" + container + "/" + UrlEscapeBlobName(blobName));
		
		string date = DateTime.UtcNow.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
		
		// Canonicalized headers: x-ms-* headers, lowercase, sorted by name.
		string canonHeaders = $"x-ms-blob-type:BlockBlob\nx-ms-date:{date}\nx-ms-version:{c_version}\n";
		string canonResource = $"/{_account}{uri.AbsolutePath}";
		
		string stringToSign = string.Join("\n",
			"PUT",
			"",                                    // Content-Encoding
			"",                                    // Content-Language
			length == 0 ? "" : length.ToString(),  // Content-Length (empty if 0)
			"",                                    // Content-MD5
			contentType,                           // Content-Type
			"",                                    // Date (empty because x-ms-date is used)
			"", "", "", "",                        // If-Modified-Since, If-Match, If-None-Match, If-Unmodified-Since
			"",                                    // Range
			canonHeaders + canonResource);
		
		string signature;
		using (var hmac = new HMACSHA256(_key))
			signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
		
		using var req = new HttpRequestMessage(HttpMethod.Put, uri);
		req.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");
		req.Headers.TryAddWithoutValidation("x-ms-date", date);
		req.Headers.TryAddWithoutValidation("x-ms-version", c_version);
		req.Headers.Authorization = new AuthenticationHeaderValue("SharedKey", $"{_account}:{signature}");
		req.Content = new StreamContent(stream); // Content-Length is set automatically for seekable streams
		req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
		
		using var resp = internet.http.Send(req);
		if (!resp.IsSuccessStatusCode) {
			string body = resp.Text(true);
			throw new HttpRequestException($"Azure upload failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");
		}
	}
	
	/// <summary>
	/// Gets names of blobs and sub-directories directly inside one directory of a container. Anonymous access.
	/// </summary>
	/// <param name="containerUrl">Like <c>"https://qgindi.blob.core.windows.net/libreautomate"</c>.</param>
	/// <param name="directory">Directory, like <c>"abc"</c> or <c>"abc/def"</c> or <c>null</c>.</param>
	/// <returns>Filenames without directory path. Names of directories end with <c>"/"</c>.</returns>
	public static IEnumerable<string> List(string containerUrl, string directory) {
		return ListXml(containerUrl, directory)
			.Select(e => {
				var s = (string)e.Element("Name");
				int i = s.LastIndexOf('/', s.Length - (s[^1] == '/' ? 2 : 1)) + 1;
				return s[i..];
			});
	}
	
	/// <summary>
	/// Gets XML elements of blobs and sub-directories directly inside one directory of a container. Anonymous access.
	/// </summary>
	/// <param name="containerUrl">Like <c>"https://qgindi.blob.core.windows.net/libreautomate"</c>.</param>
	/// <param name="directory">Directory, like <c>"abc"</c> or <c>"abc/def"</c> or <c>null</c>.</param>
	public static IEnumerable<XElement> ListXml(string containerUrl, string directory) {
		string prefix = directory;
		if (!prefix.NE()) {
			if (!prefix.EndsWith('/')) prefix += "/";
			prefix = Uri.EscapeDataString(prefix);
		}
		
		for (string marker = null; ;) {
			string url = $"{containerUrl.TrimEnd('/')}?restype=container&comp=list&delimiter=%2F&prefix={prefix}";
			if (marker != null) url += "&marker=" + Uri.EscapeDataString(marker);
			
			var r = internet.http.Get(url, headers: ["x-ms-version: 2021-08-06"]);
			string xml = r.Text(true);
			if (!r.IsSuccessStatusCode) throw new HttpRequestException($"Azure list failed: {(int)r.StatusCode} {r.ReasonPhrase}\n{xml}");
			
			var root = XElement.Parse(xml);
			//print.it(root);
			foreach (var e in root.Element("Blobs").Elements()) {
				yield return e;
			}
			
			marker = (string)root.Element("NextMarker");
			if (marker?.Length == 0) break;
		}
	}
}
