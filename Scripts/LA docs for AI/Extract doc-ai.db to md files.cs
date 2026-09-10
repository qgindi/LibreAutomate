print.it("<><lc yellowgreen>Extracting doc-ai.db to md files. Wait until DONE.<>");

if (script.testing) print.clear();
var dir = @"C:\Temp\Au\markdown"; //note: should be excluded from AV, else very slow
filesystem.delete(Directory.EnumerateFileSystemEntries(dir));

using var db = new sqlite(folders.Editor + "doc-ai.db", SLFlags.SQLITE_OPEN_READONLY);
using var sta = db.Statement("SELECT name,text FROM doc");
int n1 = 0, n2 = 0;
while (sta.Step()) {
	string name = sta.GetText(0), text = sta.GetText(1);
	
	//n1++;
	//var s = text.RxReplace(@"\R###+ (?:Exceptions|See [Aa]lso)(\R(?!##).*)+", "");
	//if (s.Length < 400) {
	////if (!name.Starts("[") && s.Length is >= 300 and < 400) {
	//	print.it("<><lc greenyellow>SKIPPED<>", s.Length);
	//	print.it(s);
	//	continue;
	//}
	//n2++;
	
	if (pathname.isInvalidName(name)) {
		name = name.Replace('<', '{').Replace('>', '}');
		name = name.Replace("*", "").Replace("/", ", ");
		if (pathname.isInvalidName(name)) {
			//print.it(name);
			name = pathname.correctName(name);
		}
	}
	string path;
	if (name[0] == '[') {
		int i = name.IndexOf(']');
		path = $@"{dir}\{name[1..i]}\{name}.md";
	} else {
		if (!name.RxMatch(@"^(Au(?:\.Types|\.More|\.Triggers)?+)\.([^\.]+)", out var m)) throw null;
		path = $@"{dir}\api\{m[1]}\{m[2]}\{name}.md";
	}
	filesystem.saveText(path, text);
}
//print.it(n1, n2);

var readme = """
This repository contains the LibreAutomate documentation converted to Markdown format and optimized for use with AI tools.

[LibreAutomate](https://github.com/qgindi/LibreAutomate) is a C# script editor and automation library for Windows.

The original documentation is available on the [LibreAutomate website](https://www.libreautomate.com/api/index.html).
""";
filesystem.saveText($@"{dir}\README.md", readme);

print.it($"<>DONE. Extracted to <link>{dir}<>");
