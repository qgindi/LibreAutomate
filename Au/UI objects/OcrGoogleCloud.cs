using System.Drawing;
using System.Text.Json.Nodes;
using System.Net.Http;

namespace Au.More;

/// <summary>
/// This OCR engine uses <see href="https://cloud.google.com/vision/docs/reference/rest/v1/images/annotate">Google Cloud Vision API</see>.
/// </summary>
/// <remarks>
/// Sends image to Google and gets results. The OCR engine is accurate but much slower than the default engine or Tesseract. Depends on internet connection speed, because Google may return several MB of results.
/// 
/// To use this engine, need to have a Google Cloud account, enable Vision API and get API key. The service isn't free, but 1000 or so requests/month are free.
/// </remarks>
public class OcrGoogleCloud : IOcrEngine {
	string _apiKey;
	
	/// <param name="apiKey">API key.</param>
	public OcrGoogleCloud(string apiKey) {
		_apiKey = apiKey;
	}
	
	/// <summary>
	/// Feature type, like <c>"TEXT_DETECTION"</c>. Or JSON of <b>features</b> array content, like <c>"""{ "type": "TEXT_DETECTION", "model": "builtin/latest" }"""</c>.
	/// If <c>null</c>, uses <c>"DOCUMENT_TEXT_DETECTION"</c>.
	/// </summary>
	public string Features { get; set; }
	
	/// <summary>
	/// JSON of <b>imageContext</b>, like <c>"""{ "languageHints": [ "ja" ] }"""</c>. Optional.
	/// </summary>
	public string ImageContext { get; set; }

	/// <inheritdoc cref="IOcrEngine.DpiScale"/>
	public bool DpiScale { get; set; }
	
	/// <inheritdoc cref="IOcrEngine.Recognize"/>
	/// <exception cref="Exception">Failed.</exception>
	public OcrWord[] Recognize(Bitmap b, bool dispose, double scale) {
		var b0 = b;
		b = IOcrEngine.PrepareBitmap(b, dispose, scale);
		var png = IOcrEngine.GetBitmapPngFileData(b);
		if (dispose || b != b0) b.Dispose();
		
		var url = "https://vision.googleapis.com/v1/images:annotate?key=" + _apiKey;
		
		var feat = Features ?? "DOCUMENT_TEXT_DETECTION";
		if (!feat.Starts('{')) feat = $$"""{ "type": "{{feat}}" }""";
		
		string ic = ImageContext, ic2 = ic.NE() ? null : ",\r\n      \"imageContext\": ";
		
		var requestJson = $$"""
{
  "requests": [
    {
      "features": [
        {{feat}}
      ],
      "image": {
        "content": "{{Convert.ToBase64String(png)}}"
      }{{ic2}}{{ic}}
    }
  ]
}
""";
		
		//perf.first();
		if (!internet.http_.TryPost(out var r, url, internet.jsonContent(requestJson), ["Accept-Encoding: br, gzip, deflate"], dontWait: true))
			throw new AuException(r.Text(true));
		//perf.next();
#if true //can be faster > 10 times. Also we use compression. Together it makes this part 100 times faster than the TryPost.
		var j = _ReadResponse(r);
		//perf.nw();
		return _ParseJson(j["responses"][0], scale);
#else
		var j=r.Json();
		//perf.nw();
		return _ParseJson(j["responses"][0], scale);
#endif
	}
	
	//Reads response until "fullTextAnnotation".
	//Can make the download size smaller > 10 times.
	static unsafe JsonNode _ReadResponse(HttpResponseMessage rm) {
		var find = "\"fullTextAnnotation\":"u8;
		using var ab = new ArrayBuilder_<byte>() { Capacity = 250_000 };
		int have = 0;
		using (var stream = rm.Content.ReadAsStream()) {
			for (; ; ) {
				if (have + 17000 > ab.Capacity) ab.ReAlloc(ab.Capacity * 2);
				int n = stream.Read(new Span<byte>(ab.Ptr + have, ab.Capacity - have - 10));
				if (n == 0) break;
				int old = Math.Min(find.Length, have);
				int i = new RByte(ab.Ptr + have - old, n + old).IndexOf(find);
				if (i > 0) {
					i += have - old;
					while (ab.Ptr[i - 1] is 32 or 9 or 10 or 13 or (byte)',') i--;
					ab.Ptr[i++] = (byte)'}';
					ab.Ptr[i++] = (byte)']';
					ab.Ptr[i++] = (byte)'}';
					have = i;
					break;
				}
				have += n;
			}
		}
		rm.Dispose();
		return JsonNode.Parse(new RByte(ab.Ptr, have));
	}
	
	static OcrWord[] _ParseJson(JsonNode j, double scale) {
		List<OcrWord> a = new();
		string text = null;
		int i = 0;
		foreach (var word in j["textAnnotations"].AsArray()) {
			var s = (string)word["description"];
			if (text == null) {
				text = s;
			} else {
				int i2 = text.Find(s, i);
				var sep = text[i..i2]; if (sep == "\n") sep = "\r\n";
				a.Add(new(sep, s, _PolyToRect(word), scale));
				i = i2 + s.Length;
			}
		}
		
		return a.ToArray();
		
		static RECT _PolyToRect(JsonNode n) {
			var a = n["boundingPoly"]["vertices"].AsArray();
			JsonNode tl = a[0], tr = a[1], br = a[2], bl = a[3];
			return IOcrEngine.PolyToRect(tl["x"], tl["y"], tr["x"], tr["y"], br["x"], br["y"], bl["x"], bl["y"]);
		}
	}
}
