using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Classification;
using EToken = CiStyling.EToken;

partial class AuDocs {
	static void _CreateCodeCss(string siteDir) {
		var s = new CiStyling.TStyles();
		var b = new StringBuilder();
		
		_Style("c", s.Comment);
		_Style("const", s.Constant);
		_Style("ex", s.Excluded);
		_Style("f", s.Function);
		_Style("k", s.Keyword);
		_Style("label", s.Label);
		_Style("ns", s.Namespace);
		_Style("n", s.Number);
		_Style("o", s.Operator);
		_Style("pre", s.Preprocessor);
		_Style("p", s.Punctuation);
		_Style("s", s.String);
		_Style("se", s.StringEscape);
		_Style("t", s.Type);
		_Style("v", s.Variable);
		_Style("x1", s.XmlDocTag);
		_Style("x2", s.XmlDocText);
		
		void _Style(string name, CiStyling.TStyle k) {
			b.AppendFormat("pre span.{0}{{color:#{1:X6};", name, k.color);
			if (k.bold) b.Append("font-weight: bold;");
			b.AppendLine("}");
		}
		
		filesystem.saveText(siteDir + @"\styles\code.css", b.ToString());
	}
	
	//note: runs in parallel threads.
	static string _Code(string s, bool syntax) {
		if (syntax) {
			s = System.Net.WebUtility.HtmlDecode(s); //eg &lt; in generic parameters
			
			//remove/fix something in parameters
			s = s.RxReplace(@"\(\w+\)0", "0"); //(Enum)0 => 0
			s = s.RxReplace(@"\bdefault\([^)?]+\? *\)", "null"); //default(Nullable?) => null
			s = s.RxReplace(@"\bdefault\(.+?\)", "default"); //default(Struct) => default
			s = s.RxReplace(@"\[ParamString\(PSFormat\.\w+\)\] ", "");
			s = s.RxReplace(@" ?\*(?=\w)", "* ");
		} else {
			s = Encoding.UTF8.GetString(Convert.FromBase64String(s));
		}
		
		using var ws = new AdhocWorkspace();
		var document = CiUtil.CreateDocumentFromCode(ws, s, needSemantic: true);
		var semo = document.GetSemanticModelAsync().Result;
		
		//at first set byte[] styles.
		//	Can't format text directly because GetClassifiedSpansAsync results may be overlapped, eg at first entire string and then its escape sequences.
		//	And it simplifies formatting.
		var a = new byte[s.Length];
		int prevEnd = 0; EToken prevStyle = 0;
		foreach (var v in Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(0, s.Length)).Result) {
			var ct = v.ClassificationType;
			if (ct == ClassificationTypeNames.StaticSymbol) continue;
			EToken style = CiStyling.StyleFromClassifiedSpan(v, semo);
			int start = v.TextSpan.Start, end = v.TextSpan.End;
			//print.it(style, s[start..end]);
			if (style == prevStyle && start > prevEnd && a[prevEnd] == 0) start = prevEnd; //join adjacent styles separated by whitespace
			prevEnd = end; prevStyle = style;
			a.AsSpan(start..end).Fill((byte)style);
		}
		
		var b = new StringBuilder();
		for (int i = 0; i < a.Length;) {
			int start = i; byte u = a[i]; while (i < a.Length && a[i] == u) i++;
			string text = System.Net.WebUtility.HtmlEncode(s[start..i]);
			if (u == 0) {
				b.Append(text);
			} else {
				var k = (EToken)u switch {
					EToken.Comment => "c",
					EToken.Constant => "const",
					EToken.Excluded => "ex",
					EToken.Function => "f",
					EToken.Keyword => "k",
					EToken.Label => "label",
					EToken.Namespace => "ns",
					EToken.Number => "n",
					EToken.Operator => "o",
					EToken.Preprocessor => "pre",
					EToken.Punctuation => "p",
					EToken.String => "s",
					EToken.StringEscape => "se",
					EToken.Type => "t",
					EToken.Variable => "v",
					EToken.XmlDocTag => "x1",
					EToken.XmlDocText => "x2",
					_ => null,
				};
				b.AppendFormat("<span class=\"{0}\">", k);
				b.Append(text);
				b.Append("</span>");
			}
		}
		s = b.ToString();
		
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
}
