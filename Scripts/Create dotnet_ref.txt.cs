print.clear();
var a = new List<string>();
var hd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
_Dir(folders.NetRuntimeDesktopBS + "Microsoft.WindowsDesktop.App.deps.json", "d"); //must be first, because several assemblies are in both folders, and need to use from Desktop
_Dir(folders.NetRuntimeBS + "Microsoft.NETCore.App.deps.json", "c");
//print.it(a);
var s = string.Join('|', a.OrderBy(o => o));
print.it(s);
//print.it(s.Length); return;
File.WriteAllText(folders.ThisAppBS + "dotnet_ref.txt", s);

void _Dir(string dir, string prefix) {
	//rejected: parse with JsonNode.
	var s = File.ReadAllText(dir);
	//print.it(s);
	int from = s.Find("\"runtime\": {"), to = s.Find("\"native\": {", from);
	if (!s.RxFindAll(@"(?m)\h*""([^""]+)\.dll"": {", 1, out string[] k, range: from..to)) throw new AuException();
	foreach (var v in k) {
		if(!hd.Add(v)) {
			//print.it("duplicate", v);
			continue;
		}
		a.Add(prefix + v);
	}
}
