//In this file: functions that preprocess or postprocess file text without Roslyn.
using System.Xml.Linq;
using System.Xml.XPath;

partial class AuDocs {
	regexp _rxRecord = new(@"(?m)^\h*([\w ]*)\brecord ((class|struct) (\w+)(?:\(([^\)]+)\))?[^\{;]*[\{;])");
	regexp _rxRecordParam = new(@"^([\w\.<>\[\]]+)\s(\w+)");
	regexp _rxRecordDocLine = new(@"\G\h*///[^/]");
	regexp _rxRecordDocParam = new(@"(?ms)^\h*///\h*<param name=""(\w+)"">(.+?)</param>\h*\R");
	regexp _rxSeealso1, _rxSeealso2;
	
	string _PreprocessFileAsText(string path, string s) {
		//if (!path.Ends("param types.cs")) return s;
		
		//DocFX does not support records.
		//	If there is 'record' keyword, DocFX silently drops entire type.
		//	If there is 'record class X(...)', also silently drops everything below it, even if not public.
		//	Then warning if the type is used somewhere in parameters or cref.
		//	Also need to convert 'record class X(...)' to the classic format before we can process it correctly.
		if (s.Contains("record ")) {
			//print.it($"<><Z greenyellow>{path}");
			s = _rxRecord.Replace(s, m => {
				if (!m[5].Exists) return m.ExpandReplacement("$1 $2");
				//print.it($"<><c blue>{m.Value}<>");
				
				_b.Clear();
				var mod = m[1].Value;
				var name = m[4].Value;
				var par = m[5].Value;
				var sub = m.Subject;
				bool noBody = sub[m.End - 1] == ';';
				
				_b.AppendFormat("{0} {1} {2} {{", mod, m[3].Value, name);
				if (mod.Contains("public")) {
					//doc
					int docStart = 0;
					for (int i = m.Start; i > 3;) {
						i = sub.LastIndexOf('\n', i - 2); if (i < 0) break;
						if (!_rxRecordDocLine.IsMatch(sub, ++i..)) break;
						docStart = i;
					}
					RXMatch[] ap = null;
					if (docStart > 0) {
						//print.it($"<><c green><_>{sub[docStart..m.Start]}</_><>");
						if (!_rxRecordDocParam.FindAll(sub, out ap, docStart..m.Start)) ap = null;
						//if(ap!=null) foreach (var v in ap) print.it($"<><c brown><_>{v}</_><>");
					}
					
					//ctor
					_b.AppendLine();
					if (ap != null) foreach (var p in ap) _b.Append(p.Value);
					_b.AppendFormat("public {0}({1}){{}}\n", name, par);
					
					//properties
					foreach (var p in par.Split(',', StringSplitOptions.TrimEntries)) {
						//print.it(p);
						if (!_rxRecordParam.Match(p, out var k)) { print.warning(p); continue; }
						string pType = k[1].Value, pName = k[2].Value;
						_b.AppendLine();
						var mp = ap?.FirstOrDefault(o => o[1].Value == pName);
						if (mp != null) _b.Append("/// <summary>").Append(mp[2].Value).AppendLine("</summary>");
						_b.AppendFormat("public {0} {1} {{ get; init; }}\n", pType, pName);
					}
				}
				if (noBody) _b.Append("}");
				//print.it(_b);
				
				//return m.Value;
				return _b.ToString();
			});
			//if(s.RxMatch(@"(?m)^[\h\w]*\brecord (?:class|struct)", out var m2)) print.it("regex failed", path);
		}
		
		//if (path.Ends("param types.cs")) print.it(s);
		return s;
	}
	
	public void Postprocess(string siteDirTemp, string siteDir) {
		filesystem.delete(siteDir);
		filesystem.createDirectory(siteDir);
		var files = filesystem.enumFiles(siteDirTemp, flags: FEFlags.AllDescendants | FEFlags.NeedRelativePaths | FEFlags.UseRawPath).ToArray();
		Parallel.ForEach(files, f => { //faster: 8 s -> 3 s
			var name = f.Name;
			var file2 = siteDir + name;
			if (name.Ends(".html") && !name.Ends(@"\toc.html")) {
				_PostprocessFile(f, file2, siteDirTemp);
			} else if (name.Eqi(@"\styles\docfx.js")) {
				_ProcessJs(f.FullPath, file2);
			} else {
				filesystem.copy(f.FullPath, file2);
				if (name.Eqi(@"\xrefmap.yml")) _XrefMap(f.FullPath);
			}
		});
		_CreateCodeCss(siteDir);
	}
	
	void _PostprocessFile(FEFile f, string file2, string siteDirTemp) {
		//print.it($"<><Z green>{f.Name}<>");
		string name = f.Name, s = filesystem.loadText(f.FullPath);
		bool isApi = name.Starts(@"\api");
		
		int nr;
		if (isApi) {
			//In class member pages, in title insert a link to the type.
			nr = s.RxReplace(@"<h1\b[^>]* data-uid=""(Au\.(?:Types\.|Triggers\.|More\.)?+([\w\.`]+))\.\w+\*?""[^>]*>(?:Method|Property|Field|Event|Operator|Constructor) (?=\w)",
				m => m.ExpandReplacement(@"$0<a href=""$1.html"">$2</a>.").Replace("`", "-"),
				out s, 1);
			
			//Add "(+ n overloads)" link in h1 and "(next/top)" links in h2 if need.
			if (s.RxFindAll(@"<h2 class=""overload"" id=""(.+?)"".*?>Overload", out var a) && a.Length > 1) {
				var b = new StringBuilder();
				int jPrev = 0;
				for (int i = 0; i < a.Length; i++) {
					bool first = i == 0, last = i == a.Length - 1;
					int j = first ? s.Find("</h1>") : a[i].End;
					b.Append(s, jPrev, j - jPrev);
					jPrev = j;
					b.Append("<span style='font-size:14px; font-weight: 400; margin-left:20px;'>(");
					if (first) b.Append("+ ").Append(a.Length - 1).Append(" ");
					var href = last ? "top" : a[i + 1][1].Value;
					b.Append("<a href='#").Append(href).Append("'>");
					if (first) b.Append("overload").Append(a.Length == 2 ? "" : "s");
					else b.Append(last ? "top" : "next");
					b.Append("</a>)</span>");
				}
				b.Append(s, jPrev, s.Length - jPrev);
				s = b.ToString();
			}
			
			//Remove anchor from the first hidden overload <h2>, and add at the very top (before <header>).
			//	Without it would not work links to the top overload from others overloads in the same page.
			//	In the past needed this to prevent incorrect scrolling. It seems current web browsers don't scroll.
			if (s.RxMatch(@"(<h2 class=""overload"")( id="".+?"" data-uid="".+?"")", out var m)) {
				s = s.ReplaceAt(m[2], "");
				s = s.RxReplace(@"<a name=""top""\K", m[2].Value, 1);
			}
			
			//Replace <seealso> link text. DocFX sets incorrect text.
			//	For <see> we specify correct text when preprocessing, but for <seealso> DocFX ignores the text.
			//	Workaround: when preprocessing, before <seealso cref> insert <seealso href> with correct text. Now simply regex replace.
			_rxSeealso1 ??= new("""(?ms)^\h*<div class="seealso">\R(.+?)\R\h*</div>""");
			_rxSeealso2 ??= new("""(?m)^.+?<a href="https://text">(.+?)</a>.+\R(.+?<a class="xref".+?>)(.+?)</a>""");
			s = _rxSeealso1.Replace(s, m1 => _rxSeealso2.Replace(m1.Value, "$2$1</a>")); //in each block of <seealso> links replace each link
			
			//ungroup Classes, Structs etc in namespace pages. Eg would be at first class screen.at and then separately struct screen.
			if (0 != name.Ends(true, @"\Au.html", @"\Au.More.html", @"\Au.Types.html", @"\Au.Triggers.html") && s.RxMatch(@"(?sm)(^\h*<h2 .+?)</article>", 1, out RXGroup g)) {
				var k = s.RxFindAll("""(?ms)^\h*<h5 class="ns"><a .+?>(.+?)</a>.+?</section>\R""", 0, g).OrderBy(o => o[1].Value);
				s = s.ReplaceAt(g, string.Join("", k));
			}
			
			//SHOULDDO: in DocFX-generated links replace Boolean with bool etc. Also Nullable<Type> with Type?.
			//	They are in: parameter/etc types in method topics; method links in class topics; maybe more.
		} else {
			//in .md we use this for links to api: [Class]() or [Class.Func]().
			//	DocFX converts it to <a href="">Class</a> etc without warning.
			//	Now convert it to a working link.
			nr = s.RxReplace(@"<a href="""">(.+?)</a>", m => {
				var k = m[1].Value;
				string href = null;
				foreach (var ns in _auNamespaces) {
					if (filesystem.exists(siteDirTemp + "/api/" + ns + k + ".html").File) {
						href = "../api/" + ns + k + ".html";
						break;
					}
				}
				if (href == null) { print.it($"cannot resolve link: [{k}]()"); return m.Value; }
				return m.ExpandReplacement($@"<a href=""{href}"">$1</a>");
			}, out s);
			
			
			//<google>...</google> -> <a href="google search">
			nr = s.RxReplace(@"<google>(.+?)</google>", @"<a href=""https://www.google.com/search?q=$1"">$1</a>", out s);
			
			//<msdn>...</msdn> -> <a href="google search in microsoft.com">
			nr += s.RxReplace(@"<msdn>(.+?)</msdn>", @"<a href=""https://www.google.com/search?q=site:microsoft.com+$1"">$1</a>", out s);
			if (nr > 0) print.it("SHOULDDO: if using <google> or <msdn> in conceptual topics, need to htmldecode-urlencode-htmlencode. Unless it's single word.");
		}
		
		//javascript renderTables() replacement, to avoid it at run time. Also remove class table-striped.
		nr = s.RxReplace(@"(?s)<table(>.+?</table>)", @"<div class=""table-responsive""><table class=""table table-bordered table-condensed""$1</div>", out s);
		
		//the same for renderAlerts
		nr = s.RxReplace(@"<div class=""(NOTE|TIP|WARNING|IMPORTANT|CAUTION)\b",
			o => {
				string k = "info"; switch (o[1].Value[0]) { case 'W': k = "warning"; break; case 'I': case 'C': k = "danger"; break; }
				return o.Value + " alert alert-" + k;
			},
			out s);
		
		nr = s.RxReplace(@"<p>\s+", "<p>", out s); //<p>\n makes new line before. This is in notes only.
		
		s = _rxCss.Replace(s, "$1$2\n$1<link rel=\"stylesheet\" href=\"../styles/code.css\">", 1);
		s = _rxCode2.Replace(s, m => _Code(m[1].Value, true)); //syntax in api, and ```code``` in conceptual
		if (isApi) s = _rxCode.Replace(s, m => _Code(m[1].Value, false)); //<code> in api
		
		//print.it(s);
		filesystem.saveText(file2, s);
	}
	
	regexp _rxCode = new("""(?<=<pre>)%%(.+?)%%(?=</pre>)""");
	regexp _rxCode2 = new("""(?s)<code class="lang-csharp[^"]*">(.+?)</code>""");
	regexp _rxCss = new("""(?m)(\h*)(\Q<link rel="stylesheet" href="../styles/main.css">\E)""");
	
	static void _ProcessJs(string file, string file2) {
		var s = filesystem.loadText(file);
		
		//don't need to highlight code. We do it at build time.
		s = s.Replace("highlight();", "");
		
		//prevent adding <wbr> in link text
		s = s.Replace("breakText();", "").Replace("$(e).breakWord();", "");
		
		//we process tables and alerts in HTML at build time. At run time the repainting is visible, and it slows down page loading, + possible scrolling problems.
		s = s.Replace("renderTables();", "").Replace("renderAlerts();", "");
		
		//prevent adding footer when scrolled to the bottom
		s = s.Replace("renderFooter();", "");
		//Somehow adds anyway. Need to add empty footer.tmpl.partial. But this code line does not harm.
		
		filesystem.saveText(file2, s);
	}
	
	//From xrefmap.yml extracts conceptual topics and writes to _\xrefmap.yml.
	//Could simply copy the file, but it is ~2 MB, and we don't need api topics.
	//Editor uses it to resolve links in code info.
	static void _XrefMap(string file) {
		var b = new StringBuilder();
		var s = filesystem.loadText(file);
		foreach (var m in s.RxFindAll(@"(?m)^- uid:.+\R.+\R  href: (?!api/).+\R", (RXFlags)0)) {
			//print.it(m);
			b.Append(m);
		}
		
		filesystem.saveText(folders.ThisAppBS + "xrefmap.yml", b.ToString());
	}
}
