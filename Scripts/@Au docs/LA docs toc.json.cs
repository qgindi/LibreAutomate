/// Creates toc.json from files created by script "Au docs". It will be used in LA Help panel.
/// Executed by `Au docs.cs`.

using System.Text.Json.Nodes;
using System.Xml.Linq;

if (script.testing) print.clear();

//HashSet<string> hsDbNames = [];
//using (var db = new sqlite(folders.ThisAppBS + "doc-ai.db", SLFlags.SQLITE_OPEN_READONLY)) {
//	using var sta = db.Statement("SELECT name FROM doc");
//	while (sta.Step()) {
//		hsDbNames.Add(sta.GetText(0));
//	}
//}

var root = new JsonArray();
_Cookbook();
_Api(@"C:\Temp\Au\DocFX\site\api\toc.json");
_Other(@"C:\Temp\Au\DocFX\site\articles", "Articles");
_Other(@"C:\Temp\Au\DocFX\site\editor", "Editor");
var json = root.ToJsonString(new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
filesystem.saveText(folders.ThisAppBS + "toc.json", json);
filesystem.saveText(@"C:\code\Au.Editor\toc.json", json);

void _Cookbook() {
	var xr = XmlUtil.LoadElem(folders.ThisAppBS + @"..\Cookbook\files.xml");
	
	var jdk = new JsonObject();
	jdk.Add("name", "Cookbook");
	root.Add(jdk);
	_AddItems(xr, jdk);
	
	static void _AddItems(XElement xp, JsonObject jp) {
		var ja = new JsonArray();
		jp.Add("items", ja);
		foreach (var x in xp.Elements()) {
			string type = x.Name.LocalName, name = x.Attr("n");
			if (name[0] == '-' || type == "o") continue;
			var j = new JsonObject();
			ja.Add(j);
			if (type == "d") {
				j["name"] = name;
				_AddItems(x, j);
			} else {
				j["name"] = name[..^3];
			}
		}
	}
}

void _Api(string sourcePath) {
	var json = filesystem.loadText(sourcePath);
	var j1 = JsonNode.Parse(json) as JsonObject;
#if !true
	foreach (JsonObject ns in j1["items"].AsArray()) {
		var j2 = new JsonObject();
		root.Add(j2);
		_Tidy(ns, j2, 1);
	}
#else
	var j2 = new JsonObject();
	j2["name"] = "Library API";
	root.Add(j2);
	_Tidy(j1, j2, 0);
#endif
	
	static void _Tidy(JsonObject j1, JsonObject j2, int level) {
		string name = null, href = null, kind = null;
		foreach (var (k, v) in j1) {
			switch (k) {
			case "name":
				name = (string)v;
				break;
			case "href":
				if (level == 1) {
					kind = "Namespace";
				} else if (level is 2 or 3) {
					href = (string)v;
					var file = @"C:\Temp\Au\DocFX\site\api\" + href;
					if (!filesystem.exists(file).File) throw new AuException(href);
					var html = filesystem.loadText(file);
					
					//kind
					if (!html.RxMatch(@"<h1 .+?>(\w+)", 1, out kind)) throw null;
					
					//rename operators to match DB
					if (kind == "Operator") {
						if (name.Starts("operator ")) name = name[9..]; else if (name.Ends(" operator")) name = name[..^9];
					}
					
					//check whether the full name == the DB name
					//if (kind != "Field") {
					//	if (level == 2) {
					//		s = $"{(string)j.Parent.Parent["name"]}.{s}";
					//		//print.it(s);
					//	} else {
					//		var jType = j.Parent.Parent.Parent.Parent;
					//		s = $"{(string)jType.Parent.Parent["name"]}.{(string)jType["name"]}.{s}";
					//	}
					//	if (!hsDbNames.Remove(s)) print.it(s);
					//}
				}
				break;
			case "items" when v is JsonArray { Count: > 0 } ja1:
				var ja2 = new JsonArray();
				if (level == 2) { //remove the member kind level
					foreach (var jk in ja1) {
						_Items(jk["items"].AsArray(), ja2);
					}
				} else {
					_Items(ja1, ja2);
				}
				j2["items"] = ja2;
				
				void _Items(JsonArray ja1, JsonArray ja2) {
					foreach (JsonObject jj1 in ja1) {
						var jj2 = new JsonObject();
						ja2.Add(jj2);
						_Tidy(jj1, jj2, level + 1);
					}
				}
				break;
			}
		}
		
		if (kind != null) j2.Insert(0, "kind", kind);
		if (href != null) j2.Insert(0, "href", href);
		if (name != null) j2.Insert(0, "name", name);
	}
}

void _Other(string dir, string docKind) {
	var jdk = new JsonObject();
	jdk.Add("name", docKind);
	root.Add(jdk);
	_AddItems(dir, jdk);
	
	static void _AddItems(string dir, JsonObject jp, string hrefPath = null) {
		var ja = new JsonArray();
		jp.Add("items", ja);
		foreach (var f in filesystem.list(dir).All) {
			string name = f.Name;
			if (!f.IsDirectory) {
				if (!name.Ends(".html")) continue;
				name = f.Name[..^5];
				if (name is "toc" or "index") continue;
			}
			var j = new JsonObject();
			j.Add("name", name);
			ja.Add(j);
			var href = Uri.EscapeDataString(f.Name);
			if (f.IsDirectory) {
				_AddItems(f, j, hrefPath + href + "/");
			} else {
				j.Add("href", hrefPath + href);
			}
		}
	}
	
}
