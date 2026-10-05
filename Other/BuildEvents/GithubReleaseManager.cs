using System.Text.Json.Nodes;
using System.Net.Http;

class GithubReleaseManager {
	readonly string _repo, _tag, _urlBase, _urlUploadBase;
	readonly string[] _headers;
	long _releaseId;
	JsonArray _assets;
	
	/// <summary>
	/// Sets fields.
	/// Gets GitHub API token from env var API_GITHUB.
	/// </summary>
	/// <param name="repo">Like "owner/repo". If only "repo", uses "qgindi/repo".</param>
	/// <param name="tag">Tag name of an existing or new release.</param>
	public GithubReleaseManager(string repo, string tag) {
		_repo = repo.Contains('/') ? repo : "qgindi/" + repo;
		_tag = tag;
		_urlBase = $"https://api.github.com/repos/{_repo}/releases/";
		_urlUploadBase = $"https://uploads.github.com/repos/{_repo}/releases/";
		var token = Environment.GetEnvironmentVariable("API_GITHUB") ?? throw new Exception("no env var API_GITHUB");
		_headers = [$"Authorization: Bearer {token}", "Accept: application/vnd.github+json", "X-GitHub-Api-Version: 2026-03-10"];
	}
	
	void _InitAssetsOfExistingRelease() {
		if (_releaseId != 0) return;
		var j = internet.http.Get($"{_urlBase}tags/{_tag}", headers: _headers).Json();
		//j.Print();
		_assets = j["assets"].AsArray();
		_releaseId = (long)j["id"];
		//foreach (var v in _assets) v.Print();
	}
	
	public JsonArray Assets {
		get {
			_InitAssetsOfExistingRelease();
			return _assets;
		}
	}
	
	public bool DeleteAssetIfExists(string fileName) {
		_InitAssetsOfExistingRelease();
		
		if (_assets.FirstOrDefault(o => ((string)o["name"]).Eqi(fileName)) is not { } v) return false;
		//print.it("deleting " + fileName);
		internet.http.Get($"{_urlBase}assets/{v["id"]}", headers: _headers, also: m => { m.Method = HttpMethod.Delete; }).EnsureSuccessStatusCode();
		return true;
	}
	
	public void AddOrReplaceAsset(string file, string contentType) {
		_InitAssetsOfExistingRelease();
		
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
