/// Creates doc-html.db from files created by script "Au docs". It will be used in LA Read panel.
/// Executed by `Au docs.cs`.

/*/ testInternal Au; nuget html\HtmlAgilityPack; /*/

//#define IEWB

using HtmlAgilityPack; //tested: AngleSharp slower

if (script.testing) print.clear();
bool dev = !true;

//Debug_.MemorySetAnchor();
if (!GC.TryStartNoGCRegion(2_000_000_000)) throw null; //makes faster > 2 times

const string c_rootDir = @"C:\Temp\Au\DocFX\site\";
string[] subDirs = ["api", "articles", "editor", "cookbook"]; //note: don't add the root index.html (let users open it in their web browser)

var dbFile = folders.ThisAppBS + "doc-html.db";
sqliteStatement dbInsert;

filesystem.delete(dbFile);
using (var db = new sqlite(dbFile)) {
	db.Execute("CREATE TABLE doc (name TEXT PRIMARY KEY, text TEXT)");
	using var dbInsert_ = dbInsert = db.Statement("INSERT INTO doc VALUES (?, ?)");
	using var dbTrans = db.Transaction();
	//perf.first();
	_Convert();
	_AddOtherFiles();
	//perf.nw();
	dbTrans.Commit();
	db.Execute("VACUUM");
}
filesystem.copyTo(dbFile, @"C:\code\Au.Editor", FIfExists.Delete);

print.scrollToTop();
//Debug_.MemoryPrint();

void _Convert() {
	List<_FileHtml> a = [];
	foreach (var subDir in subDirs) {
		foreach (var f in filesystem.enumFiles(c_rootDir + subDir, "*.html", FEFlags.AllDescendants | FEFlags.NeedRelativePaths)) {
			var name = f.Name[1..];
			if (name is "toc.html") continue;
			a.Add(new() { name = $"{subDir}/{name.Replace('\\', '/')}", html = f.FullPath });
		}
	}
	//perf.next();
	if (dev) {
		foreach (var f in a) {
			f.html = _ProcessHtml(f.html, f.name);
		}
	} else {
		Parallel.ForEach(a, f => { //with no-GC region 3 times faster, else 20% faster
			f.html = _ProcessHtml(f.html, f.name);
		});
	}
	//perf.next();
	foreach (var f in a) {
		_AddRow(f.name, f.html);
	}
}

void _AddRow(string name, object data) {
	dbInsert.BindAll(name, data).Step();
	dbInsert.Reset();
}

void _AddOtherFiles() {
	foreach (var f in filesystem.enumFiles(c_rootDir + "styles")) {
		var path = f.FullPath;
		object data = pathname.getExtension(f.Name) switch { ".css" => filesystem.loadText(path), ".eot" => filesystem.loadBytes(path), _ => null };
		
#if IEWB
		if (f.Name == "docfx.vendor.min.css") { //fix the hidden scrollbar issue
			var s1 = (string)data;
			if (0 == s1.RxReplace("""@-ms-viewport\s*\{\s*width:\s*device-width\s*}""", "", out s1, 1)) throw null;
			data = s1;
		}
#endif
		
		if (data != null) _AddRow($"styles/{f.Name}", data);
		//if (data == null) print.it(f.Name);
	}
	
	_AddRow("styles/la.css", """
h1 { background-color: #9ACD32 !important; margin: 0 -2px 8px -2px !important; padding: 1px 2px 3px 2px !important; }
h1, h2, h3 { font-size: 110% !important; }
h4, h5 { font-size: 100% !important; }
div.col-md-12,div.container-fluid { padding-left: 2px !important; padding-right: 2px !important; }
td, th { white-space: normal !important; word-wrap: break-word; }
pre { word-wrap: normal !important; }
""");
	
#if IEWB
	_AddRow("styles/la.js", """
function ancestorOrThis(el, tag) {
    while (el && el.nodeName !== tag) el = el.parentElement;
    return el;
}

document.addEventListener('contextmenu', function(e) {
	e.preventDefault(); 
	var contextFlag = 0; // 0: no selection, 1: selection, 2: inside <pre> (and no selection)

	var selectedText = window.getSelection().toString();
	
	if (selectedText.length > 0) {
		contextFlag = 1;
	} else {
		var preElement = ancestorOrThis(e.target, 'PRE');
		if (preElement && preElement.parentElement.className !== 'codewrapper') {
			contextFlag = 2;
			selectedText = preElement.textContent;
		}
	}

	window.external.ShowContextMenu(e.clientX, e.clientY, contextFlag, selectedText);
	return false;
});

document.addEventListener('click', function(e) {
	var targetElement = e.target;

	var aElement = ancestorOrThis(e.target, 'A');
	if (aElement && aElement.classList.contains('nuget')) {
		e.preventDefault();
		window.external.NugetLinkClicked(targetElement.textContent);
	}
});
""");
#endif
	
	foreach (var f in filesystem.enumFiles(c_rootDir + "images", "*.png")) {
		_AddRow($"images/{f.Name}", filesystem.loadBytes(f.FullPath));
	}
}

string _ProcessHtml(string path, string uri) {
	if (dev) print.clear();
	
	var doc = new HtmlDocument();
	doc.Load(path, Encoding.Default);
	var h = doc.DocumentNode.Element("html");
	var head = h.Element("head");
	var body = h.Element("body");
	var relPath = head.Element("link").GetAttributeValue("href", ""); relPath = relPath[..relPath.FindNot("./")];
	
	List<HtmlNode> are = [];
	List<HtmlAttribute> ara = [];
	void _Remove(HtmlNode n) { if (n != null) are.Add(n); }
	void _RemoveAttr(HtmlAttribute n) { if (n != null) ara.Add(n); }
	
	foreach (var v in head.Elements("meta")) {
		if (v.ChildAttributes("property").Any()) _Remove(v);
	}
	
	_Remove(head.SelectSingleNode("link[@rel='shortcut icon']"));
	_Remove(head.SelectSingleNode("meta[@name='title']"));
	
	head.AppendChild(HtmlNode.CreateNode($"""<link rel="stylesheet" href="{relPath}styles/la.css">"""));
	head.AppendChild(doc.CreateTextNode("\n"));
	
	_Remove(body.SelectSingleNode(".//header"));
	_Remove(body.SelectSingleNode(".//div[@class='sidenav hide-when-search']"));
	if (body.SelectSingleNode(".//div[@class='article row grid-right']") is { } div2) div2.RemoveClass();
	
	if (h.SelectNodes("//script") is { } scripts)
		foreach (var v in scripts) _Remove(v);
	
#if IEWB
	body.AppendChild(HtmlNode.CreateNode($"""<script type="text/javascript" src="{relPath}styles/la.js"></script>"""));
#endif
	body.AppendChild(doc.CreateTextNode("\n"));
	
	foreach (var vn in body.Descendants("div")) {
		//if (!vn.HasChildNodes) print.it(vn.OuterHtml);
		if (!vn.HasChildNodes) _Remove(vn);
	}
	
	foreach (var v in are) v.Remove();
	
	if (uri.Starts(@"api")) {
		foreach (var vn in body.Descendants())
			if (vn.NodeType is HtmlNodeType.Element) {
				foreach (var va in vn.GetAttributes()) {
					switch (va.Name) {
					case "data-uid":
					case "id" when vn.Name is "h1" or "h3" or "h4" or "h5" or "h6" or "a" or "td": _RemoveAttr(va); continue;
					case "id" when vn.Name is "h2": continue; //overload
					}
					
					//if (va.Name is not ("class" or "href" or "style") && va.Value.Length > 20) print.it(vn.Name, va.Name, va.Value);
				}
			}
	}
	
	foreach (var va in body.GetDataAttributes()) _RemoveAttr(va);
	
	foreach (var v in ara) v.Remove();
	
#if IEWB
	//IE displays too wide tabs in pre code, and does not support CSS tab-size. Workaround: replace tabs with spaces.
	if (body.SelectNodes(".//pre[not(parent::div[@class='codewrapper'])]//code") is { } preCodes) {
		foreach (var v in preCodes) {
			var s = v.InnerHtml;
			if (s.Contains('\t')) {
				s = s.RxReplace(@"(?m)^\t+", m => new string(' ', m.Length * 4));
				v.InnerHtml = s;
			}
		}
	}
#endif
	
	//links that LA handles
	if (uri.Starts("cookbook") && body.SelectNodes(".//span[@title='Paste the underlined text in menu > Tools > NuGet']") is { } nugetLinks) {
		foreach (var v in nugetLinks) {
			v.Attributes.RemoveAll();
			v.Name = "a";
#if IEWB
			v.AddClass("nuget");
#else
			v.SetAttributeValue("href", "la-link:nuget/" + v.InnerText);
#endif
		}
	}
	
	//remove the LA window image
	if (uri == "editor/Application.html") {
		body.SelectSingleNode(".//p/img").Remove();
	}
	
	if (dev) {
		print.it(head.OuterHtml);
		print.it(body.OuterHtml.RxReplace(@"\R(?:\R\h*)+\R", "\n\n"));
		print.scrollToTop();
		if (!dialog.showYesNo("Continue", path[c_rootDir.Length..])) Environment.Exit(0);
	}
	
	return doc.DocumentNode.OuterHtml;
}

class _FileHtml {
	public string name, html;
}
