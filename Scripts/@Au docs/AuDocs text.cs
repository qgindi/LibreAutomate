//In this file: functions that preprocess or postprocess file text without Roslyn.

//#define DISQUS

using System.Xml.Linq;
using System.Xml.XPath;

partial class AuDocs {
	regexp _rxRecord, _rxRecordParam, _rxRecordDocLine, _rxRecordDocParam, _rxSeealso1, _rxSeealso2, _rxSeealso3, _rxSeealso4, _rxSeealso5;
	
	string _PreprocessFileAsText(string path, string s) {
		//if (!path.Ends("param types.cs")) return s;
		
		//Convert 'record class X(...)' to the classic format, because DocFX:
		//	1. Ignores <param>.
		//	2. Copies summary etc from type to ctor.
		if (s.Contains("record ")) {
			//print.it($"<><lc greenyellow>{path}");
			_rxRecord ??= new(@"(?m)^\h*(public [\w ]*\brecord) (class|struct) (\w+)(\(([^()]++|(?-2))+\))[^\{;]*[\{;]");
			s = _rxRecord.Replace(s, m => {
				//print.it($"<><c blue>{m.Value}<>");
				
				_b.Clear();
				var mod = m[1].Value;
				var name = m[3].Value;
				var par = s[(m[4].Start + 1)..(m[4].End - 1)];
				var sub = m.Subject;
				bool noBody = sub[m.End - 1] == ';';
				
				_b.AppendFormat("{0} {1} {2} {{", mod, m[2].Value, name);
				//doc
				_rxRecordDocLine ??= new(@"\G\h*///[^/]");
				int docStart = 0;
				for (int i = m.Start; i > 3;) {
					i = sub.LastIndexOf('\n', i - 2); if (i < 0) break;
					if (!_rxRecordDocLine.IsMatch(sub, ++i..)) break;
					docStart = i;
				}
				RXMatch[] ap = null;
				if (docStart > 0) {
					//print.it($"<><c green><_>{sub[docStart..m.Start]}</_><>");
					_rxRecordDocParam ??= new(@"(?ms)^\h*///\h*<param name=""(\w+)"">(.+?)</param>\h*\R");
					if (!_rxRecordDocParam.FindAll(sub, out ap, docStart..m.Start)) ap = null;
					//if(ap!=null) foreach (var v in ap) print.it($"<><c brown><_>{v}</_><>");
				}
				
				//ctor
				_b.AppendLine();
				if (ap != null) foreach (var p in ap) _b.Append(p.Value);
				_b.AppendFormat("public {0}({1}){{}}\n", name, par);
				
				//properties
				_rxRecordParam ??= new(@"\G((?:[\w\.]+|\([^)]+\))(<([^<>]++|(?-2))+>)?(?:\[\])?) (\w+)(?:,\s*|$)");
				foreach (var k in _rxRecordParam.FindAll(par)) {
					string pType = k[1].Value, pName = k[4].Value;
					//print.it($"type='{pType}', name='{pName}'");
					_b.AppendLine();
					var mp = ap?.FirstOrDefault(o => o[1].Value == pName);
					if (mp != null) _b.Append("/// <summary>").Append(mp[2].Value).AppendLine("</summary>");
					_b.AppendFormat("public {0} {1} {{ get; init; }}\n", pType, pName);
				}
				
				if (noBody) _b.Append("}");
				//print.it(_b);
				
				//return m.Value;
				return _b.ToString();
			});
			//if(s.RxMatch(@"(?m)^[\h\w]*\brecord (?:class|struct)(\w+)\(", out var m2)) print.it("regex failed", path);
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
		//print.it($"<><lc green>{f.Name}<>");
		string name = f.Name, s = filesystem.loadText(f.FullPath);
		bool isApi = name.Starts(@"\api") && name != @"\api\index.html";
		//print.it(name, isApi);
		
		int nr;
		if (isApi) {
			//In class member pages, in title insert a link to the type.
			nr = s.RxReplace(@"<h1\b[^>]* data-uid=""(Au\.(?:Types\.|Triggers\.|More\.)?+([\w\.`]+))\.#?\w+\*?""[^>]*>(?:Method|Property|Field|Event|Operator|Constructor) (?=\w)",
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
			
			//Replace <seealso> link. DocFX sets incorrect text and incorrect URL.
			//	For <see> we specify correct text when preprocessing, but for <seealso> DocFX ignores that text.
			//	Workaround: when preprocessing, before <seealso cref> insert <seealso href> with correct text. Now regex replace.
			_rxSeealso1 ??= new("""(?ms)^\h*<div class="seealso">\R(.+?)\R\h*</div>""");
			s = _rxSeealso1.Replace(s, m1 => {
				var v = m1.Value;
				//bool debug = v.Contains("run.console");
				//if (debug) print.it("-----");
				
				//current docfx in method link text adds parameters with links to types
				_rxSeealso3 ??= new("""(?><div><a class="xref".+?</a>)\K.+(?=</div>)""");
				v = _rxSeealso3.Replace(v, "");
				//if (debug) print.it(v);
				
				//bug in current docfx: method href ends with `(raw parameters).html` instead of `.html#ns_type_name_para_meters_`. Also, generic method page name ends like `--2`.
				_rxSeealso4 ??= new("""<a class="xref" href="\K((.+?)(?:--\d+)?\(.+?\))\.html""");
				_rxSeealso5 ??= new(@"\W");
				v = _rxSeealso4.Replace(v, m2 => {
					//if (debug) print.it("-", m2.Value);
					return m2[2].Value + ".html#" + _rxSeealso5.Replace(m2[1].Value, "_");
				});
				//if (debug) print.it(v);
				
				//replace link text
				_rxSeealso2 ??= new("""(?m)^.+?<a href="https://text">(.+?)</a>.+\R(.+?<a class="xref".+?>)(.+?)(?=</a>)""");
				v = _rxSeealso2.Replace(v, m3 => m3[2].Value + System.Net.WebUtility.HtmlEncode(Encoding.UTF8.GetString(Convert2.HexDecode(m3[1].Value))));
				//if (debug) print.it(v);
				
				return v;
			});
			
			//ungroup Classes, Structs etc in namespace pages. Eg would be at first class screen.at and then separately struct screen.
			if (0 != name.Ends(true, @"\Au.html", @"\Au.More.html", @"\Au.Types.html", @"\Au.Triggers.html") && s.RxMatch(@"(?sm)(^\h*<h2 .+?)</article>", 1, out RXGroup g)) {
				var k = s.RxFindAll("""(?ms)^\h*<h5 class="ns"><a .+?>(.+?)</a>.+?</section>\R""", 0, g).OrderBy(o => o[1].Value);
				s = s.ReplaceAt(g, string.Join("", k));
			}
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
			if (nr > 0) print.warning("TODO3: if using <_><google> in conceptual topics, need to htmldecode-urlencode-htmlencode. Unless it's single word.</_>");
			
			//<msdn>...</msdn> -> <a href="google search in microsoft.com">
			nr = s.RxReplace(@"<msdn>(.+?)</msdn>", @"<a href=""https://www.google.com/search?q=site:microsoft.com+$1"">$1</a>", out s);
			if (nr > 0) print.warning("TODO3: if using <_><msdn> in conceptual topics, need to htmldecode-urlencode-htmlencode. Unless it's single word.</_>");
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
		
		_rxCss ??= new("""(?m)(\h*)(\Q<link rel="stylesheet" href="../styles/main.css">\E)""");
		//if(!_rxCss.IsMatch(s)) print.it(f.Name);
		s = _rxCss.Replace(s, "$1$2\n$1<link rel=\"stylesheet\" href=\"../styles/code.css\">", 1);
		
		_rxCode2 ??= new("""(?s)<code class="lang-[^"]*">(.+?)</code>""");
		s = _rxCode2.Replace(s, m => _Code(m[1].Value, isApi ? 1 : 2)); //syntax in api, and ```code``` in conceptual
		
		if (isApi) {
			_rxCode ??= new("""(?<=<pre>)%%(.+?)%%(?=</pre>)""");
			s = _rxCode.Replace(s, m => _Code(m[1].Value, 0)); //<code> in api
		}
		
#if DISQUS
		//TODO3: Now shows Disqus content when page loaded (if small) or scrolled to the bottom. Should show only when clicked <h2>User comments</h2>.
		
		//add this at the bottom of help pages
		var disqus = """

<hr/>
<h2>User comments</h2>
<div id="disqus_thread"></div>
<script>
    /**
    *  RECOMMENDED CONFIGURATION VARIABLES: EDIT AND UNCOMMENT THE SECTION BELOW TO INSERT DYNAMIC VALUES FROM YOUR PLATFORM OR CMS.
    *  LEARN WHY DEFINING THESE VARIABLES IS IMPORTANT: https://disqus.com/admin/universalcode/#configuration-variables    */
    /*
    var disqus_config = function () {
    this.page.url = PAGE_URL;  // Replace PAGE_URL with your page's canonical URL variable
    this.page.identifier = PAGE_IDENTIFIER; // Replace PAGE_IDENTIFIER with your page's unique identifier variable
    };
    */
    (function() { // DON'T EDIT BELOW THIS LINE
    var d = document, s = d.createElement('script');
    s.src = 'https://libreautomate.disqus.com/embed.js';
    s.setAttribute('data-timestamp', +new Date());
    (d.head || d.body).appendChild(s);
    })();
</script>

""";
		int i1=s.LastIndexOf("</article>");
		if(i1>0) s = s.Insert(i1, disqus);
		else print.warning("no </article> in " + name);
#endif
		
		//print.it(s);
		filesystem.saveText(file2, s);
	}
	
	regexp _rxCode, _rxCode2, _rxCss;
	
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
