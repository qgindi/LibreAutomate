print.clear();
var b = new StringBuilder();
var hd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
_Dir(folders.NetRuntimeDesktopBS + "Microsoft.WindowsDesktop.App.deps.json", "*d|"); //must be first, because several assemblies are in both folders, and need to use from Desktop
_Dir(folders.NetRuntimeBS + "Microsoft.NETCore.App.deps.json", "|*c|");

//Au.Task
b.Append("|*a|Au");
File.WriteAllText(folders.ThisAppBS + "dotnet_ref_task.txt", b.ToString());

//Au.Editor
b.Append("|Au.Controls");
print.it(b);
File.WriteAllText(folders.ThisAppBS + "dotnet_ref_editor.txt", b.ToString());


void _Dir(string dir, string prefix) {
	//rejected: parse with JsonNode.
	var a = new List<string>();
	var s = File.ReadAllText(dir);
	//print.it(s);
	int from = s.Find("\"runtime\": {"), to = s.Find("\"native\": {", from);
	if (!s.RxFindAll(@"(?m)\h*""([^""]+)\.dll"": {", 1, out string[] k, range: from..to)) throw new AuException();
	foreach (var v in k) {
		if(!hd.Add(v)) {
			//print.it("duplicate", v);
			continue;
		}
		a.Add(v);
	}
	b.Append(prefix);
	b.AppendJoin('|', a.OrderBy(o => o));
}
//note: cannot use AssemblyDependencyResolver._assemblyPaths or corehost_resolve_component_dependencies.
//	Error "Hostpolicy must be initialized and corehost_main must have been called before". Our apphost does not use corehost_main etc.
