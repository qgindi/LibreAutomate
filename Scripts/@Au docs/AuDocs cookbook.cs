using System.Xml.Linq;
using ADL;

partial class AuDocs {
	
	public static void Cookbook(string docDir) {
		var sbToc = new StringBuilder();
		List<(string name, string path)> aFiles = new();
		
		string dirTo = @"C:\Temp\Au\DocFX\cookbook", dirToLink = docDir + @"\cookbook";
		if (filesystem.exists(dirTo)) filesystem.delete(Directory.GetFiles(dirTo));
		else filesystem.createDirectory(dirTo);
		if (!filesystem.exists(dirToLink).IsNtfsLink) filesystem.more.createSymbolicLink(dirToLink, dirTo, CSLink.Directory);
		
		var dirFrom = folders.Editor + @"..\Cookbook\files";
		var xr = XmlUtil.LoadElem(dirFrom + ".xml");
		
		_AddItems(xr, 1, dirFrom);
		
		void _AddItems(XElement xp, int level, string path) {
			//see PanelCookbook._Load().
			foreach (var x in xp.Elements()) {
				var name = x.Attr("n");
				if (name[0] == '-') continue;
				var tag = x.Name.LocalName;
				bool dir = tag == "d";
				if (dir) {
					sbToc.Append('#', level).AppendFormat(" {0}\r\n", name);
					_AddItems(x, level + 1, path + "\\" + name);
				} else {
					if (tag != "s") continue;
					var cspath = path + "\\" + name;
					name = name[..^3];
					sbToc.Append('#', level).AppendFormat(" [{0}](/cookbook/{1})\r\n", name, Uri.EscapeDataString(name) + ".html");
					aFiles.Add((name, cspath));
				}
			}
		}
		
		//print.it(sbToc.ToString());
		filesystem.saveText(dirTo + @"\toc.md", sbToc.ToString());
		filesystem.saveText(dirTo + @"\index.md", "");
		
		foreach (var (name, path) in aFiles) {
			var code = filesystem.loadText(path);
			bool test = false;
			//test = name == "test";
			//if (test) {
			//	print.it($"<><lc #B3DF00>{name}<>");
			//	print.it(code);
			//	print.it("-------------");
			//}
			
			var md = AuDocsShared.RecipeCodeToMd(name, code, test);
			if (test) print.it(md);
			filesystem.saveText($@"{dirTo}\{name}.md", md);
		}
	}
}
