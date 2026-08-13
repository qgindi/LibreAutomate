using System.Text.Json.Nodes;
using System.Net.Http;

class GithubReleaseManager {
	readonly string _urlBase, _urlUploadBase;
	readonly string[] _headers;
	long _releaseId;
	JsonArray _assets;
	
	public GithubReleaseManager(string repo) {
		_urlBase = $"https://api.github.com/repos/qgindi/{repo}/releases/";
		_urlUploadBase = $"https://uploads.github.com/repos/qgindi/{repo}/releases/";
		var token = Environment.GetEnvironmentVariable("API_GITHUB") ?? throw new InvalidOperationException("API_GITHUB is not set.");
		_headers = [$"Authorization: Bearer {token}", "Accept: application/vnd.github+json", "X-GitHub-Api-Version: 2026-03-10"];
	}
	
	public void Init(string tag) {
		var j = internet.http.Get($"{_urlBase}tags/{tag}", headers: _headers).Json();
		//j.Print();
		_assets = j["assets"].AsArray();
		_releaseId = (long)j["id"];
		//foreach (var v in _assets) v.Print();
	}
	
	public bool DeleteAssetIfExists(string fileName) {
		if (_assets.FirstOrDefault(o => ((string)o["name"]).Eqi(fileName)) is not { } v) return false;
		//print.it("deleting " + fileName);
		internet.http.Get($"{_urlBase}assets/{v["id"]}", headers: _headers, also: m => { m.Method = HttpMethod.Delete; }).EnsureSuccessStatusCode();
		return true;
	}
	
	public void AddOrReplaceAsset(string file, string contentType) {
		var name = Path.GetFileName(file);
		if (!name.RxIsMatch(@"^[A-Za-z0-9_\-\.]+$")) throw new ArgumentException("Filename can contain only ASCII alphanumeric, -, _ and dot. Else would be renamed.");
		
		DeleteAssetIfExists(name);
		
		var url = internet.urlAppend($"{_urlUploadBase}{_releaseId}/assets", "name=" + name);
		var bytes = filesystem.loadBytes(file);
		var content = new ByteArrayContent(bytes);
		content.Headers.ContentType = new(contentType);
		var j = internet.http.Post(url, content, headers: _headers).Json();
		//j.Print();
	}
}
