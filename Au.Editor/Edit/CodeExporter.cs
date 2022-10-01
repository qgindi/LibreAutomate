using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Classification;
using EStyle = CiStyling.EStyle;
using TStyle = CiStyling.TStyle;

#if SCRIPT
namespace Script;
#endif

static class CodeExporter {
	public static EStyle[] GetStyles(string s) {
		using var ws = new AdhocWorkspace();
		var document = CiUtil.CreateDocumentFromCode(ws, s, needSemantic: true);
		var semo = document.GetSemanticModelAsync().Result;
		
		var a = new EStyle[s.Length];
		int prevEnd = 0; EStyle prevStyle = 0;
		foreach (var v in Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(0, s.Length)).Result) {
			var ct = v.ClassificationType;
			if (ct == ClassificationTypeNames.StaticSymbol) continue;
			EStyle style = CiStyling.StyleFromClassifiedSpan(v, semo);
			int start = v.TextSpan.Start, end = v.TextSpan.End;
			//print.it(style, s[start..end]);
			if (style == prevStyle && start > prevEnd && a[prevEnd] == 0) start = prevEnd; //join adjacent styles separated by whitespace
			prevEnd = end; prevStyle = style;
			a.AsSpan(start..end).Fill(style);
		}
		
		return a;
	}
	
	public static string ExportForum(string s) {
		var a = GetStyles(s);
		var b = new StringBuilder();
		EStyle prevStyle = 0;
		for (int i = 0; i < a.Length; i++) {
			var style = a[i];
			if (style != prevStyle) {
				if (prevStyle != 0) b.Append("(`)");
				if (style != 0) b.Append("(`").Append(_StyleToString(style)).Append(")");
				prevStyle = style;
			}
			b.Append(s[i]);
		}
		if (prevStyle != 0) b.Append("(`)");
		return b.ToString();
	}
	
	public static string ExportHtml(string s, bool spanClass, bool withCss = false) {
		var a = GetStyles(s);
		StringWriter t = new();
		if (withCss) t.Write("<style>\r\n{0}</style>\r\n", ExportCss());
		t.Write("<pre");
		if (spanClass) t.Write(" class='au'");
		if (!spanClass || withCss) t.Write(" style='{0}'", c_preStyle);
		t.Write(">\r\n");
		EStyle prevStyle = 0;
		int i = 0, ip = 0;
		for (; i < a.Length; i++) {
			var style = a[i];
			if (style != prevStyle) {
				_AppendText();
				if (style != 0) {
					if (spanClass) {
						t.Write("<span class='");
						t.Write(_StyleToString(style));
					} else {
						var k = _StyleToStruct(style);
						t.Write("<span style='color:#{0:X6}", k.color);
						if (k.bold) t.Write(";font-weight: bold");
					}
					t.Write("'>");
				}
				prevStyle = style;
			}
		}
		_AppendText();
		
		void _AppendText() {
			if (i > ip) { System.Net.WebUtility.HtmlEncode(s[ip..i], t); ip = i; }
			if (prevStyle != 0) t.Write("</span>");
		}
		
		t.Write("</pre>\r\n");
		return t.ToString();
	}
	
	const string c_preStyle = "background-color:#FFFFFF;border:#D1D7DC;border-style:solid;border-width:1px;padding-left:2px;line-height:normal;color:black;tab-size:4;font-family:\"Consolas\";";
	
	public static string ExportCss() {
		var b = new StringBuilder();
		b.Append("pre.au {").Append(c_preStyle).AppendLine("}");
		
		_Style(EStyle.Comment);
		_Style(EStyle.Constant);
		_Style(EStyle.Excluded);
		_Style(EStyle.Function);
		_Style(EStyle.Keyword);
		_Style(EStyle.Label);
		_Style(EStyle.Namespace);
		_Style(EStyle.Number);
		_Style(EStyle.Operator);
		_Style(EStyle.Preprocessor);
		_Style(EStyle.Punctuation);
		_Style(EStyle.String);
		_Style(EStyle.StringEscape);
		_Style(EStyle.Type);
		_Style(EStyle.Variable);
		_Style(EStyle.XmlDocTag);
		_Style(EStyle.XmlDocText);
		
		void _Style(EStyle style) {
			var k = _StyleToStruct(style);
			b.AppendFormat("pre.au .{0}{{color:#{1:X6};", _StyleToString(style), k.color);
			if (k.bold) b.Append("font-weight: bold;");
			b.Append("}");
		}
		
		return b.AppendLine().ToString();
	}
	
	static string _StyleToString(EStyle style)
		=> style switch {
			EStyle.Comment => "cm",
			EStyle.Constant => "cn",
			EStyle.Excluded => "ex",
			EStyle.Function => "fn",
			EStyle.Keyword => "kw",
			EStyle.Label => "lb",
			EStyle.Namespace => "ns",
			EStyle.Number => "nr",
			EStyle.Operator => "op",
			EStyle.Preprocessor => "pd",
			EStyle.Punctuation => "pn",
			EStyle.String => "st",
			EStyle.StringEscape => "se",
			EStyle.Type => "tp",
			EStyle.Variable => "vr",
			EStyle.XmlDocTag => "xt",
			_ => "xd" //XmlDocText
		};
	
	static CiStyling.TStyles _styles = new();
	
	static TStyle _StyleToStruct(EStyle style)
		=> style switch {
			EStyle.Comment => _styles.Comment,
			EStyle.Constant => _styles.Constant,
			EStyle.Excluded => _styles.Excluded,
			EStyle.Function => _styles.Function,
			EStyle.Keyword => _styles.Keyword,
			EStyle.Label => _styles.Label,
			EStyle.Namespace => _styles.Namespace,
			EStyle.Number => _styles.Number,
			EStyle.Operator => _styles.Operator,
			EStyle.Preprocessor => _styles.Preprocessor,
			EStyle.Punctuation => _styles.Punctuation,
			EStyle.String => _styles.String,
			EStyle.StringEscape => _styles.StringEscape,
			EStyle.Type => _styles.Type,
			EStyle.Variable => _styles.Variable,
			EStyle.XmlDocTag => _styles.XmlDocTag,
			_ => _styles.XmlDocText
		};
}
