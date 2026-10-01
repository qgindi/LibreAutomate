/// Prints Virustotal false positives for the LA setup file or any other file.
/// If still not scanned, uploads and waits until scanned, then prints results.
/// Can be run directly at any time (the Run button) or periodically (from the timer script).

/*/ role exeProgram; outputPath %folders.Workspace%\exe\Virustotal false positives; /*/

using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

_Mode mode = script.testing ? _Mode.Testing : args is ["/github"] ? _Mode.Github : args is ["/release"] ? _Mode.Release : throw null;

string file = folders.Editor + "LibreAutomate.msi";

if (mode is _Mode.Testing) {
	//print.clear();
	//file = folders.Editor + @"64\AuCpp.dll";
	//file = folders.Editor + @"64\Au.DllHost.exe";
	//file = folders.Editor + @"..\Setup\bin\Release\net48\vt-1.zip";
	//file = folders.Editor + @"..\Setup\bin\Release\net48\offline-1.zip.lzma";
	//file = folders.Editor + @"..\Setup\bin\Release\net48\LA-setup.exe";
	//file = @"C:\code\Test\Rust\hello-rust\target\release\hello-rust.exe";
	//file = folders.Downloads + "LibreAutomateSetup.exe";
	
}

var apikey = Environment.GetEnvironmentVariable("API_VIRUSTOTAL");
string[] headers = ["x-apikey: " + apikey];
string filename = Path.GetFileName(file);
bool uploadedNow = false;

//bool test = true;
//if (test) {
//	_Upload();
//	return;
//}

string id;
if (mode is _Mode.Github) { //from the timer script
	var r1 = internet.http.Get("https://api.github.com/repos/qgindi/LibreAutomate/releases/latest");
	var j1 = r1.Json(true)["assets"][0];
	//_PrintJson(j1);
	var digest = (string)j1["digest"];
	id = digest[7..]; //prefix "sha256:"
} else {
	id = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
}

//try get existing results for that file
g1:
var r = internet.http.Get($"https://www.virustotal.com/api/v3/files/{id}", headers: headers);
var j = r.Json(true);
if (r.IsSuccessStatusCode) {
	//_PrintJson(j);
	//print.it("-------");
	//_PrintJson(j["data"]["attributes"]["last_analysis_results"]);
	//print.it("-------");
	//_PrintJson(j["data"]["attributes"]["last_analysis_stats"]);
	//print.it("-------");
	
	int nFP = (int)j["data"]["attributes"]["last_analysis_stats"]["malicious"];
	int nIgnore = 0;
	if (nFP > nIgnore) {
		print.it($"<><lc #FFC977>Virustotal: {nFP} false positives for {filename}<>");
		foreach (var (av, n) in j["data"]["attributes"]["last_analysis_results"].AsObject().Where(kv => (string)kv.Value["category"] == "malicious")) {
			var result = (string)n["result"];
			print.it($"{av,-20}  {result}");
		}
	} else if (script.testing) {
		print.it($"Virustotal: {nFP} false positives for {filename}");
	}
} else if (r.StatusCode == System.Net.HttpStatusCode.NotFound && !uploadedNow) {
	id = _Upload();
	sound.speak("Analysing finished");
	goto g1;
} else {
	_HttpOK(r, false);
}

string _Upload() {
	print.it($"Virustotal: uploading {filename}");
	var url = "https://www.virustotal.com/api/v3/files";
	if (new FileInfo(file).Length > 32_000_000 /*|| test*/) url = _GetLargeFileUploadUrl();
	string analysisId;
	using (var form = internet.formContent()) {
		//workaround for: the large-file URL fails if Content-Disposition header values are not enclosed in "
		var content = new StreamContent(filesystem.loadStream(file));
		form.Add(content, "\"file\"");
		content.Headers.ContentDisposition.FileName = $"\"{filename}\"";
		
		var r = internet.http.Post(url, form, headers: headers);
		_HttpOK(r, true);
		var j = r.Json();
		analysisId = (string)j["data"]["id"];
	}
	
	//poll the analysis
	for (bool once = false; ; once = true) {
		var r = internet.http.Get($"https://www.virustotal.com/api/v3/analyses/{analysisId}", headers: headers);
		_HttpOK(r, true);
		var j = r.Json();
		string status = (string)j["data"]["attributes"]["status"];
		//print.it("status", status);
		if (status == "completed") {
			//_PrintJson(j);
			uploadedNow = true;
			return (string)j["meta"]["file_info"]["sha256"];
		} else {
			if (!once) {
				print.it("Virustotal: analysing...");
				10.s();
			}
			5.s();
			//if (!dialog.showYesNo("Continue polling?")) return;
		}
	}
	
}

string _GetLargeFileUploadUrl() {
	//get a single-use URL for uploading a large file
	var r = internet.http.Get("https://www.virustotal.com/api/v3/files/upload_url", headers: headers);
	_HttpOK(r, true);
	var j = r.Json();
	return (string)j["data"];
}

static bool _HttpOK(HttpResponseMessage r, bool exception) {
	if (r.IsSuccessStatusCode) return true;
	print.it(r.StatusCode, r.ReasonPhrase);
	try { _PrintJson(r.Json(true)); }
	catch (JsonException) { print.it(r.Text(true)); }
	if (exception) throw new AuException();
	return false;
}

static void _PrintJson(JsonNode j) {
	print.it(j.ToJsonString(new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
}

enum _Mode { Testing, Release, Github }
