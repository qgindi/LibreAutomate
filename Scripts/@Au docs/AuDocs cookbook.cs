using System.Xml.Linq;

partial class AuDocs {
	
	public static void Cookbook(string docDir) {
		App.Settings ??= new(); //need internetSearchUrl
		
		var sbToc = new StringBuilder();
		List<(string name, string nameMd, string path)> aFiles = new();
		regexp rxTag = new(@"<([\+\.]?\w+)(?: ([^>\r\n]+))?>((?:[^<]++|(?R)|<\w+(?:, \w+)*>)*)<(?:/\1)?>");
		regexp rxEscape = new(@"[\!\#\$\%\&\'\(\)\*\+\-\/\:\<\=\>\?\@\[\\\]\^\_\`\{\|\}\~]");
		const string website = "https://www.libreautomate.com";
		
		string dirTo = @"C:\Temp\Au\DocFX\cookbook", dirToLink = docDir + @"\cookbook";
		if (filesystem.exists(dirTo)) filesystem.delete(Directory.GetFiles(dirTo));
		else filesystem.createDirectory(dirTo);
		if (!filesystem.exists(dirToLink).IsNtfsLink) filesystem.more.createSymbolicLink(dirToLink, dirTo, CSLink.Directory);
		
		var dirFrom = folders.ThisAppBS + "..\\Cookbook\\files";
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
					var nameMd = name.Replace("#", "Sharp").Replace(".", "dot-") + ".md"; //also in PanelCookbook.cs
					sbToc.Append('#', level).AppendFormat(" [{0}]({1})\r\n", name, nameMd);
					aFiles.Add((name, nameMd, cspath));
				}
			}
		}
		
		//print.it(sbToc.ToString());
		filesystem.saveText(dirTo + @"\toc.md", sbToc.ToString());
		filesystem.saveText(dirTo + @"\index.md", """
# Cookbook
This is an online copy of the LibreAutomate cookbook.
""");
		
		foreach (var (name, nameMd, path) in aFiles) {
			var code = filesystem.loadText(path);
			bool test = false;
			//test = name == "test";
			//if (test) {
			//	print.it($"<><lc #B3DF00>{name}<>");
			//	print.it(code);
			//	print.it("-------------");
			//}
			
			var b = new StringBuilder();
			b.Append("# ").AppendLine(name);
			string usings;
			foreach (var (isText, s) in PanelRecipe.ParseRecipe(code, out usings)) {
				if (isText) {
					//CONSIDER: markdown-escape (replace * with \* etc).
					//	Now escapes only in several tags, where noticed bad Type<T> etc.
					//	Or will need to review each new recipe as webpage. All initial recipes are reviewed.
					//	When reviewing, if something is bad, usually that text must be in <mono> (inline code) etc.
					
					if (test) print.it(s);
					b.AppendLine(rxTag.Replace(s, _Repl));
				} else {
					b.AppendLine();
					b.AppendLine("```csharp");
					b.Append(s);
					if (!s.Ends('\n')) b.AppendLine();
					b.AppendLine("```");
					b.AppendLine();
				}
			}
			
			if (test) print.it(b.ToString());
			filesystem.saveText($@"{dirTo}\{nameMd}", b.ToString());
			
			string _Repl(RXMatch m) {
				if (test) print.it(m);
				string tag = m[1].Value, s = m[3].Value;
				
				if (tag == "_") return _MarkdownEscape(s); //raw text
				
				if (tag is ".k" or ".x" or ".c" or "mono") {
					Debug_.PrintIf(s.Contains('<') && m.Value.Ends("<>")); //if contains <, must end with </.c> etc
					Debug_.PrintIf(s.Contains('\n'), s);
				}
				
				bool onlyRawText = s.Like("<_>*</_>");
				if (onlyRawText) s = _MarkdownEscape(s[3..^4]);
				else if (s.Contains('<')) s = rxTag.Replace(s, _Repl);
				
				//non-link tags
				switch (tag) {
				case "b" or "i" or "u":
					return $"<{tag}>{s}</{tag}>";
				case "bi":
					return $"<b><i>{s}</i></b>";
				case ".k": //C# keyword
					return $"<code style='color:#00f'>{s}</code>";
				case ".x": //API name
					return $"<code style='color:#e06060'>{s}</code>";
				case ".c" or "mono": //inline code or key/hotkey
					return $"`{s}`";
				case "c":
					throw new ArgumentException("Don't use the c tag (color). It can't be converted to markdown for AI. Use .c (code). " + m);
				case "+nuget":
					return $"<span style='color:#080;text-decoration:underline' title='Paste the underlined text in menu > Tools > NuGet'>{s}</span>";
				case "open":
					return s;
				}
				
				var attr = m[2].Value;
				if (attr == null) attr = s;
				else {
					if (attr is ['\'', .., '\''] or ['"', .., '"']) attr = attr[1..^1];
					Debug_.PrintIf(attr.Contains('|'));
				}
				
				//links
				switch (tag) {
				case "help":
					if (HelpUtil.AuHelpUrl(attr) is string url1 && url1.Starts(website)) return $"<a href=\"{url1[website.Length..]}\">{s}</a>";
					break;
				case "link":
					if (!attr.Starts("http")) return s; //eg %folders.Workspace%
					return $"<a href=\"{attr}\">{s}</a>";
				case "google":
					return $"<a href=\"https://www.google.com/search?q={System.Net.WebUtility.UrlEncode(attr)}\">{s}</a>";
				case "+lang":
					attr += ", C# reference";
					goto case "google";
				case "+ms":
					attr += " site:microsoft.com";
					goto case "google";
				case "+recipe":
					//if(_FindRecipe(attr) is string rec) return $"[{s}]({rec})"; //why DocFX ignores it?
					if (_FindRecipe(attr) is string rec) return $"<a href=\"{rec}\">{s}</a>"; //DocFX replaces .md with .html
					break;
				case "+see": //was <see cref="attr"/>, now <+see 'attr'>attr<>
					if (PanelRecipe.GetSeeUrl(attr, usings) is string url2) {
						if (url2.Starts(website)) url2 = url2[website.Length..];
						if (!onlyRawText) s = _MarkdownEscape(s);
						return $"<a href=\"{url2}\">{s}</a>";
					}
					break;
				}
				
				print.it(tag, attr);
				return s;
			}
			
			//see PanelCookbook._FindRecipe.
			string _FindRecipe(string s) {
				foreach (var (name, nameMd, _) in aFiles) if (name.Like(s, true)) return nameMd;
				foreach (var (name, nameMd, _) in aFiles) if (name.Starts(s, true)) return nameMd;
				foreach (var (name, nameMd, _) in aFiles) if (name.Find(s, true) >= 0) return nameMd;
				return null;
			}
			
			string _MarkdownEscape(string s) => rxEscape.Replace(s, @"\$0");
		}
	}
}
