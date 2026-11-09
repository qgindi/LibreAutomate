/*/ role miniProgram; define TEST; /*/

using System.Text.Json.Nodes;
using System.Net.Http;

#if TEST
print.clear();

var m = new LaCloudGithub("LA-downloads", "v1.0.0");
//m.DeleteAssetIfExists("test.7z");
m.AddOrReplaceAsset(@"C:\Test\test.7z", "application/x-compressed");
#else
namespace LA.Shared;
#endif

class LaCloudGithub {
	readonly string _repo, _tag, _urlBase, _urlUploadBase;
	readonly string[] _headers;
	long _releaseId;
	JsonArray _assets;
	
	/// <summary>
	/// Sets fields.
	/// Gets GitHub API token from env var API_GITHUB.
	/// </summary>
	/// <param name="repo">Repo name in qgindi account, eg <c>"LA-downloads"</c>.</param>
	/// <param name="tag">Tag name of an existing or new release.</param>
	public LaCloudGithub(string repo, string tag) {
		var token = Environment.GetEnvironmentVariable("API_GITHUB") ?? throw new Exception("no env var API_GITHUB");
		_repo = "qgindi/" + repo;
		_tag = tag;
		_urlBase = $"https://api.github.com/repos/{_repo}/releases/";
		_urlUploadBase = $"https://uploads.github.com/repos/{_repo}/releases/";
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
	
	/// <summary>
	/// Uploads a file to the release. Replaces if exists.
	/// </summary>
	/// <param name="file">Local file path.</param>
	/// <param name="contentType">Content type. Optional for <c>7z</c> and <c>zip</c> files.</param>
	public void AddOrReplaceAsset(string file, string contentType = null) {
		contentType ??= Path.GetExtension(file).Lower() switch { ".7z" => "application/x-compressed", ".zip" => "application/x-zip-compressed", _ => throw new ArgumentException("For this file type need `contentType`") };
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
	
	/// <summary>
	/// Creates new tag and release. Makes it the latest. Uploads assets. Uses the last commit.
	/// </summary>
	/// <param name="title"></param>
	/// <param name="releaseNotes"></param>
	/// <param name="assets">Files to attach.</param>
	public void CreateNewRelease(string title, string releaseNotes, IEnumerable<string> assets) {
		foreach (var v in assets) if (!filesystem.exists(v).File) throw new FileNotFoundException("asset not found.", v);
		
		const string api = "https://api.github.com";
		
		print.it($"<><lc yellowgreen>Creating GitHub release {_tag} in repo {_repo}. Wait until DONE.<>");
		
		print.it($"Creating tag {_tag}");
		
		// Get the repository's default branch.
		var repoInfo = internet.http.Get(
			$"{api}/repos/{_repo}",
			headers: _headers).Json();
		
		string defaultBranch = (string)repoInfo["default_branch"];
		//print.it($"Default branch: {defaultBranch}");
		
		// Get the current commit of the default branch.
		var branchInfo = internet.http.Get(
			$"{api}/repos/{_repo}/git/ref/heads/{defaultBranch}",
			headers: _headers).Json();
		
		string commitSha = (string)branchInfo["object"]["sha"];
		//print.it($"Current commit: {commitSha}");
		
		// Create the Git tag.
		var tagResponse = internet.http.Post(
			$"{api}/repos/{_repo}/git/refs",
			internet.jsonContent(new {
				@ref = $"refs/tags/{_tag}",
				sha = commitSha
			}),
			headers: _headers);
		
		if (!tagResponse.IsSuccessStatusCode) {
			var je = tagResponse.Json(true);
			if ((string)je["message"] != "Reference already exists") {
				je.Print();
				tagResponse.EnsureSuccessStatusCode();
			}
		}
		
		// Create the draft release.
		print.it($"Creating draft release");
		var release = internet.http.Post(
			$"{api}/repos/{_repo}/releases",
			internet.jsonContent(new {
				tag_name = _tag,
				target_commitish = commitSha,
				name = title,
				body = releaseNotes,
				draft = true,
				prerelease = false,
				make_latest = "true"
			}),
			headers: _headers).Json();
		
		int releaseId = (int)release["id"];
		string htmlUrl = (string)release["html_url"];
		
		foreach (string path in assets) {
			byte[] data = File.ReadAllBytes(path);
			string name = Path2.GetName(path);
			
			print.it($"Uploading {name} ({data.Length / 1024 / 1024.0:F1} MB)");
			
			using var content = new ByteArrayContent(data);
			content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
			
			var r = internet.http.Post(
				$"https://uploads.github.com/repos/{_repo}/releases/{releaseId}/assets?name={Uri.EscapeDataString(name)}",
				content,
				headers: _headers);
			r.EnsureSuccessStatusCode();
		}
		
		// Publish the release.
		print.it($"Publishing");
		var r2 = internet.http.Post(
			$"{api}/repos/{_repo}/releases/{releaseId}",
			internet.jsonContent(new {
				draft = false,
				make_latest = "true"
			}),
			headers: _headers);
		if (!r2.IsSuccessStatusCode) print.it(r2.Text(true));
		var published = r2.Json();
		
		//published.Print();
		
		print.it($"<>DONE.  <link>https://github.com/{_repo}/releases<>");
	}
}
