using System.Drawing;
using System.Text.Json.Nodes;
using System.Net.Http;

namespace Au.More;

/// <summary>
/// This OCR engine uses Microsoft Azure Computer Vision OCR.
/// </summary>
/// <remarks>
/// Sends image to Microsoft Azure and gets results. The OCR engine is accurate but much slower than the default engine or Tesseract, and usually slower than Google Cloud.
/// 
/// To use this engine, need to have a Microsoft Azure account and get API key and endpoint URL. The service isn't free, but 500 or so requests/month are free.
/// </remarks>
public class OcrMicrosoftAzure : IOcrEngine {
	string _endpointUrl, _apiKey;
	
	/// <param name="endpointUrl">Endpoint URL, like <c>"https://xxxx.cognitiveservices.azure.com/"</c>.</param>
	/// <param name="apiKey">API key.</param>
	public OcrMicrosoftAzure(string endpointUrl, string apiKey) {
		if (!endpointUrl.Like("https://*.cognitiveservices.azure.com/")) print.warning("endpointUrl should be like https://xxxx.cognitiveservices.azure.com/");
		_endpointUrl = endpointUrl;
		_apiKey = apiKey;
	}
	
	/// <inheritdoc cref="IOcrEngine.DpiScale"/>
	public bool DpiScale { get; set; }
	
	/// <inheritdoc cref="IOcrEngine.Recognize"/>
	/// <exception cref="Exception">Failed.</exception>
	public OcrWord[] Recognize(Bitmap b, bool dispose, double scale) {
		var b0 = b;
		b = IOcrEngine.PrepareBitmap(b, dispose, scale, 50, 50);
		var png = IOcrEngine.GetBitmapPngFileData(b);
		if (dispose || b != b0) b.Dispose();
		
		var url = $"{_endpointUrl}formrecognizer/documentModels/prebuilt-read:analyze?api-version=2022-08-31"; //&stringIndexType=textElements
		var headers = new[] { "Ocp-Apim-Subscription-Key: " + _apiKey };
		var requestJson = $$"""
{
"base64Source": "{{Convert.ToBase64String(png)}}"
}
""";
		if (!internet.http_.TryPost(out var r, url, internet.jsonContent(requestJson), headers))
			throw new AuException(r.Text(true));
		url = r.Headers.GetValues("Operation-Location").First();
		//perf.next();
		500.ms();
		var j = wait.until(new(90) { Period = 300 }, () => {
			var v = internet.http_.Get(url, headers: headers).Json();
			return (string)v["status"] == "succeeded" ? v : null;
		});
		//perf.nw();
		return _ParseJson(j["analyzeResult"], scale);
	}
	
	static OcrWord[] _ParseJson(JsonNode j, double scale) {
		List<OcrWord> a = new();
		HashSet<int> hs = new();
		int i = 0;
		foreach (var page in j["pages"].AsArray()) {
			foreach (var line in page["lines"].AsArray()) {
				hs.Add((int)line["spans"][0]["offset"]);
			}
			foreach (var word in page["words"].AsArray()) {
				var sep = i++ == 0 ? null : hs.Contains((int)word["span"]["offset"]) ? "\r\n" : " ";
				a.Add(new(sep, (string)word["content"], _PolyToRect(word), scale));
			}
		}
		
		return a.ToArray();
		
		static RECT _PolyToRect(JsonNode n) {
			var a = n["polygon"].AsArray();
			return IOcrEngine.PolyToRect(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7]);
		}
	}
}
