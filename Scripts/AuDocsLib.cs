/*/
role classLibrary
outputPath %folders.Workspace%\dll
testInternal Au.Editor,Au,Microsoft.CodeAnalysis,Microsoft.CodeAnalysis.CSharp,Microsoft.CodeAnalysis.Features,Microsoft.CodeAnalysis.CSharp.Features,Microsoft.CodeAnalysis.Workspaces,Microsoft.CodeAnalysis.CSharp.Workspaces;
r Au.Editor.dll;
r Roslyn\Microsoft.CodeAnalysis.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.dll;
r Roslyn\Microsoft.CodeAnalysis.Features.dll;
r Roslyn\Microsoft.CodeAnalysis.CSharp.Features.dll;
r Roslyn\Microsoft.CodeAnalysis.Workspaces.dll /alias=CAW
r Roslyn\Microsoft.CodeAnalysis.CSharp.Workspaces.dll;
nuget -\Markdig;
/*/

extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using CAW::Microsoft.CodeAnalysis.Classification;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;

using System.Net;
using EStyle = LA.SciTheme.EStyle;
using Markdig;

namespace ADL;

#pragma warning disable CS1591 //Missing XML comment for publicly visible type or member

/// <summary>
/// Used by the "Au docs" script.
/// Also RecipeCodeToHtml used by the Read panel to create preview HTML of the current cookbook source script. In DEBUG mode only.
/// </summary>
public static class AuDocsShared {
	public static Func<string, string> ResolveAuApiLink;
	
	public static void Test() {
		print.clear();
		//var mdFile = folders.Editor + @"..\Cookbook\files\Filesystem\Create folder.cs";
		var mdFile = folders.Editor + @"..\Cookbook\files\Filesystem\Zip files (compress, extract).cs";
		var code = filesystem.loadText(mdFile);
		var name = pathname.getNameNoExt(mdFile);
		var html = RecipeCodeToHtml(name, code);
		//print.it(html);
	}
	
	static MarkdownPipeline _pipeline;
	
	public static string RecipeCodeToHtml(string name, string code) {
		if (_pipeline == null) {
			var p = new MarkdownPipelineBuilder();
			p.BlockParsers.RemoveAll(o => o is Markdig.Parsers.IndentedCodeBlockParser);
			p.UsePipeTables();
			_pipeline = p.Build();
		}
		
		var md = RecipeCodeToMd(name, code);
		var s = Markdig.Markdown.ToHtml(md, _pipeline);
		//print.it(s);
		s = PostprocessHtmlNonApi(name, s);
		return PostprocessHtmlCommon(name, s, isApi: false);
	}
	
	public static string RecipeCodeToMd(string name, string code, bool test = false) {
		var b = new StringBuilder();
		b.Append("# ").AppendLine(name);
		string usings;
		foreach (var (isText, s) in _ParseRecipe(name, code, out usings)) {
			if (isText) {
				//CONSIDER: markdown-escape (replace * with \* etc).
				//	Now escapes only in several tags, where noticed bad Type<T> etc.
				//	Or will need to review each new recipe as webpage. All initial recipes are reviewed.
				//	When reviewing, if something is bad, usually that text must be in <mono> (inline code) etc.
				
				if (test) print.it(s);
				b.AppendLine(_rxTag.Replace(s, _Repl));
			} else {
				b.AppendLine();
				b.AppendLine("```csharp");
				b.Append(s);
				if (!s.Ends('\n')) b.AppendLine();
				b.AppendLine("```");
				b.AppendLine();
			}
		}
		
		return b.ToString();
		
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
			else if (s.Contains('<')) s = _rxTag.Replace(s, _Repl);
			
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
			case "+nuget":
				return $"<span style='color:#080;text-decoration:underline' title='Paste the underlined text in menu > Tools > NuGet'>{s}</span>";
			case "open":
				return $"<code>{s}</code>";
			case "c" or ".c" or "mono":
				throw new ArgumentException("Don't use tags c, .c, mono. Use `code`. " + m);
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
				//print.it(name, attr);
				if (attr.Contains('/')) { //info: <help> is used only for "articles\x" and "editor\x"
					if (!filesystem.exists(folders.Editor + @"..\Other\DocFX\_doc\" + attr + ".md")) break;
					return $"<a href=\"/{UrlEscapePath(attr)}.html\">{s}</a>";
				}
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
				if (_FindRecipe(attr) is string rec) return $"<a href=\"/cookbook/{Uri.EscapeDataString(rec)}\">{s}</a>";
				break;
			case "+see": //was <see cref="attr"/>, now <+see 'attr'>attr<>. Then could not replace with <a> because usings still were not collected from codes.
				if (_GetSeeUrl(attr, usings, out bool isAu) is string url2) {
					if (isAu) url2 = $"/api/{url2}.html";
					if (!onlyRawText) s = _MarkdownEscape(s);
					return $"<a href=\"{url2}\">{s}</a>";
				}
				break;
			}
			
			print.it($"<>{name}: <c red>{tag}, {attr}<>");
			return s;
		}
		
		string _MarkdownEscape(string s) => _rxEscape.Replace(s, @"\$0");
	}
	
	static regexp _rxEscape = new(@"[\!\#\$\%\&\'\(\)\*\+\-\/\:\<\=\>\?\@\[\\\]\^\_\`\{\|\}\~]");
	static regexp _rxTag = new(@"<([\+\.]?\w+)(?: ([^>\r\n]+))?>((?:[^<]++|(?R)|<\w+(?:, \w+)*>)*)<(?:/\1)?>");
	const string c_website = "https://www.libreautomate.com";
	
	static string _FindRecipe(string s) {
		if (_aFR == null) {
			var xr = XmlUtil.LoadElem(folders.Editor + @"..\Cookbook\files.xml");
			_aFR = xr.Descendants("s").Select(x => {
				var name = x.Attr("n")[..^3];
				return (name, name + ".html");
			}).ToArray();
		}
		foreach (var v in _aFR) if (v.name.Like(s, true)) return v.filenameHtml;
		foreach (var v in _aFR) if (v.name.Starts(s, true)) return v.filenameHtml;
		foreach (var v in _aFR) if (v.name.Find(s, true) >= 0) return v.filenameHtml;
		print.warning("recipe not found: " + s);
		return null;
		//see PanelHelp._FindRecipe.
	}
	static (string name, string filenameHtml)[] _aFR;
	
	public static string _GetSeeUrl(string s, string usings, out bool isAu) {
		isAu = false;
		//add same namespaces as in default global.cs. Don't include global.cs because it may be modified.
		string code = usings + $"///<see cref='{s}'/>";
		using var ws = new AdhocWorkspace();
		var document = LA.CiUtil.CreateDocumentFromCode(ws, code, needSemantic: true);
		var syn = document.GetSyntaxRootSynchronously(default);
		var node = syn.FindToken(code.Length - 3 - s.Length, true).Parent.FirstAncestorOrSelf<CrefSyntax>();
		if (node != null) {
			var semo = document.GetSemanticModelAsync().Result_();
			if (semo.GetSymbolInfo(node).GetAnySymbol() is ISymbol sym)
				return LA.CiUtil.GetSymbolHelpUrl(sym, out isAu);
		}
		return null;
	}
	
	/// <summary>
	/// Splits a recipe source code into text and code parts.
	/// From text parts removes /// and replaces 'see' with '+see'.
	/// </summary>
	/// <param name="code">C# code.</param>
	/// <param name="usings">null or all using directives found in all codes.</param>
	static List<(bool isText, string s)> _ParseRecipe(string name, string code, out string usings) {
		//rejected:
		//	1. Ignore code before the first ///. Not really useful, just forces to always start with ///.
		//	2. Use {  } for scopes of variables. Its' better to use unique names.
		//	3. Use if(...) {  } to enclose code examples to select which code to test.
		//		Can accidentally damage real 'if' code. I didn't use it; it's better to test codes in other script.
		
		List<(bool isText, string s)> r = new();
		StringBuilder sbUsings = null;
		var ac = new List<(string code, int offset8, int len8)>();
		int iCode = 0;
		foreach (var m in code.RxFindAll(@"(?ms)^(?:///(?!=/)\N*\R*)+|^/\*\*.+?\*/\R*")) {
			//print.it("--------");
			//print.it(m);
			if (code.Eq(m.Start, "/// <summary>")) continue;
			int textTo = m.End, i = code.Find("\n/// <summary>", m.Start..m.End); if (i >= 0) textTo = i + 1;
			
			_Code(iCode, m.Start);
			_Text(m.Start, iCode = textTo);
		}
		_Code(iCode, code.Length);
		usings = sbUsings?.ToString();
		return r;
		
		void _Text(int start, int end) {
			while (code[end - 1] <= ' ') end--;
			bool ml = code[start + 1] == '*';
			if (ml) {
				start += 3; while (code[start] <= ' ') start++;
				end -= 2; while (end > start && code[end - 1] <= ' ') end--;
			}
			var s = code[start..end];
			if (!ml) s = s.RxReplace(@"(?m)^/// ?", "");
			s = s.RxReplace(@"<see cref=""(.+?)""(?:/|>(.+?)<[^>]*)>", m => {
				string v = m[1].Value, t = v;
				if (m[2].Exists) t = m[2].Value;
				else if (t.Contains('{')) t = "<_>" + t.Replace('{', '<').Replace('}', '>') + "</_>";
				//if (m[2].Exists) print.it(name, v, t);
				return $"<+see '{v}'>{t}<>";
			});
			//print.it("TEXT"); print.it(s);
			r.Add((true, s));
		}
		
		void _Code(int start, int end) {
			while (end > start && code[end - 1] <= ' ') end--;
			if (end == start) return;
			var s = code[start..end];
			//print.it("CODE"); print.it(s);
			r.Add((false, s));
			
			foreach (var m in s.RxFindAll(@"(?m)^using [\w\.]+;")) {
				(sbUsings ??= new()).AppendLine(m.Value);
			}
		}
	}
	
	public static string PostprocessHtmlNonApi(string name, string s) {
		int nr;
		//in .md we use this for links to our API: [Class]() or [Class.Member]().
		//	DocFX converts it to <a href="">Class</a> etc without warning.
		//	Now convert it to a working link.
		nr = s.RxReplace(@"<a href="""">(.+?)</a>", m => {
			var k = m[1].Value;
			string href = ResolveAuApiLink(k);
			if (href == null && k.LastIndexOf('.') is var i && i > 0) href = ResolveAuApiLink(k[..i]); //enum member
			if (href == null) { print.it($"cannot resolve link: [{k}]()"); return m.Value; }
			return $@"<a href=""{href}"">{k}</a>";
		}, out s);
		
		//in .md we use this for Google search links: [text](google:) or [text](google:urlencoded+google+query).
		//	To search only in microsoft.com (.NET/Windows API etc): [text](ms:) or [text](ms:urlencoded+google+query).
		//	The colon is to avoid DocFX warnings.
		//	Now convert it to a google search link.
		nr = s.RxReplace(@"<a href=""(ms|google):([^""]+)?"">(.+?)</a>", m => {
			string linkText = m[3].Value;
			string q = m[2].Value ?? WebUtility.UrlEncode(WebUtility.HtmlDecode(linkText));
			string site = m.Subject[m[1].Start] is 'm' ? "site:microsoft.com+" : null;
			return $@"<a href=""https://www.google.com/search?q={site}{q}"">{linkText}</a>";
		}, out s);
		if (s.Contains("<google>")) print.warning("<google> in .md files not supported. Use [text](google:) or [text](google:urlencoded+google+query)");
		if (s.Contains("<ms>")) print.warning("<ms> in .md files not supported. Use [text](ms:) or [text](ms:urlencoded+google+query)");
		
		return s;
	}
	
	public static string PostprocessHtmlCommon(string name, string s, bool isApi) {
		int nr;
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
		
		_rxCode2 ??= new("""(?s)<code class="lang-(?: hljs|csharp)">(.+?)</code>""");
		s = _rxCode2.Replace(s, m => _Code(m[1].Value, isApi ? 1 : 2)); //syntax in api, and ```code``` in conceptual
		
		if (isApi) {
			_rxCode ??= new("""(?<=<pre>)%%(.+?)%%(?=</pre>)""");
			s = _rxCode.Replace(s, m => _Code(m[1].Value, 0)); //<code> in api
		}
		
		return s;
	}
	
	static regexp _rxCode, _rxCode2, _rxCss;
	
	public static string CreateCodeCss() {
		var s = LA.SciTheme.Default;
		var b = new StringBuilder();
		
		_Style("c", s.Comment);
		_Style("const", s.Constant);
		_Style("ex", s.Excluded);
		_Style("f", s.Function);
		_Style("k", s.Keyword);
		_Style("goto", s.Label);
		_Style("v", s.LocalVariable);
		_Style("ns", s.Namespace);
		_Style("n", s.Number);
		_Style("o", s.Operator);
		_Style("pre", s.Preprocessor);
		_Style("p", s.Punctuation);
		_Style("s", s.String);
		_Style("se", s.StringEscape);
		_Style("t", s.Type);
		_Style("x1", s.XmlDocTag);
		_Style("x2", s.XmlDocText);
		
		void _Style(string name, LA.SciTheme.TStyle k) {
			b.AppendFormat("pre span.{0}{{color:#{1:X6};", name, k.color);
			if (k.bold) b.Append("font-weight: bold;");
			b.AppendLine("}");
		}
		
		b.AppendLine("""

summary { display: list-item; }
details { margin-bottom: 10px; }
""");
		
		return b.ToString();
	}
	
	//note: runs in parallel threads.
	//where: 0 api <code>, 1 api syntax, 2 non-api
	static string _Code(string s, int where) {
		if (where == 0) {
			s = Encoding.UTF8.GetString(Convert.FromBase64String(s));
		} else {
			s = System.Net.WebUtility.HtmlDecode(s); //eg &lt; in generic parameters
			
			//remove/fix something in parameters
			if (where == 1) {
				s = s.RxReplace(@"\(\w+\)0", "0"); //(Enum)0 => 0
				s = s.RxReplace(@"\bdefault\([^)?]+\? *\)", "null"); //default(Nullable?) => null
				s = s.RxReplace(@"\bdefault\(.+?\)", "default"); //default(Struct) => default
				s = s.RxReplace(@"\[ParamString\(PSFormat\.\w+\)\] ", "");
				s = s.RxReplace(@" ?\*(?=\w)", "* ");
			}
		}
		
		using var ws = new AdhocWorkspace();
		var document = LA.CiUtil.CreateDocumentFromCode(ws, s, needSemantic: true);
		//var semo = document.GetSemanticModelAsync().Result;
		
		//at first set byte[] styles.
		//	Can't format text directly because GetClassifiedSpansAsync results may be overlapped, eg at first entire string and then its escape sequences.
		//	And it simplifies formatting.
		var a = new byte[s.Length];
		int prevEnd = 0; EStyle prevStyle = 0;
		foreach (var v in Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(0, s.Length)).Result) {
			var ct = v.ClassificationType;
			if (ct == ClassificationTypeNames.StaticSymbol) continue;
			EStyle style = LA.CiStyling.StyleFromClassifiedSpan(v);
			int start = v.TextSpan.Start, end = v.TextSpan.End;
			//print.it(style, s[start..end]);
			if (style == prevStyle && start > prevEnd && a[prevEnd] == 0) start = prevEnd; //join adjacent styles separated by whitespace
			prevEnd = end; prevStyle = style;
			a.AsSpan(start..end).Fill((byte)style);
		}
		
		var b = new StringBuilder("<code>"); //<code> isn't necessary; it disables Google translate (or would need attribute class="notranslate" or translate="no") and maybe better in some other cases I don't know
		for (int i = 0; i < a.Length;) {
			int start = i; byte u = a[i]; while (i < a.Length && a[i] == u) i++;
			string text = System.Net.WebUtility.HtmlEncode(s[start..i]);
			if (u == 0) {
				b.Append(text);
			} else {
				var k = (EStyle)u switch {
					EStyle.Comment => "c",
					EStyle.Constant => "const",
					EStyle.Excluded => "ex",
					EStyle.Function or EStyle.Event => "f",
					EStyle.Keyword => "k",
					EStyle.Label => "goto", //not "label", it is used in DocFX CSS
					EStyle.LocalVariable or EStyle.Field => "v",
					EStyle.Namespace => "ns",
					EStyle.Number => "n",
					EStyle.Operator => "o",
					EStyle.Preprocessor => "pre",
					EStyle.Punctuation => "p",
					EStyle.String => "s",
					EStyle.StringEscape => "se",
					EStyle.Type => "t",
					EStyle.XmlDocTag => "x1",
					EStyle.XmlDocText => "x2",
					_ => null,
				};
				b.AppendFormat("<span class=\"{0}\">", k);
				b.Append(text);
				b.Append("</span>");
			}
		}
		s = b.Append("</code>").ToString();
		
		//print.it("--------------");
		//print.it(s);
		try { System.Xml.Linq.XElement.Parse("<x>" + s + "</x>"); }
		catch (Exception e1) { print.warning(e1.ToStringWithoutStack()); print.it(s); }
		
		return s;
	}
	
	//static CSharpSemanticModel _CreateSemanticModelForCode(string code) {
	//	var trees = new CSharpSyntaxTree[] {
	//		CSharpSyntaxTree.ParseText(code, s_parseOpt) as CSharpSyntaxTree,
	//		CSharpSyntaxTree.ParseText(CiUtil.c_globalUsingsText, s_parseOpt) as CSharpSyntaxTree
	//	};
	//	var compilation = CSharpCompilation.Create("doc", trees, s_refs, s_compOpt);
	//	return compilation.GetSemanticModel(trees[0]) as CSharpSemanticModel;
	//}
	
	public static string UrlEscapePath(string s) {
		s = s.Replace('\\', '/');
		if (!s.Contains('/')) return Uri.EscapeDataString(s);
		return string.Join('/', s.Split('/').Select(o => Uri.EscapeDataString(o)));
	}
}
