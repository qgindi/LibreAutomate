extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;

using System.Xml.Linq;
using Au.Controls;
using System.Windows.Input;

static class CiSnippets {
	class _CiComplItemSnippet : CiComplItem {
		public readonly XElement x;
		public _Context context;
		public readonly bool custom;
		
		public _CiComplItemSnippet(string name, XElement x, bool custom) : base(CiComplProvider.Snippet, default, name, CiItemKind.Snippet) {
			this.x = x;
			this.custom = custom;
		}
	}
	
	static List<_CiComplItemSnippet> s_items;
	
	[Flags]
	enum _Context {
		None,
		Namespace = 1, //global, namespace{ }
		Type = 2, //class{ }, struct{ }, interface{ }
		Function = 4, //method{ }, lambda{ }
		Arrow = 8, //lambda=>, function=>
		Attributes = 16, //[Attributes]
		Unknown = 32,
		Any = 0xffff,
		Line = 0x10000, //at start of line
		/*
		
		A context specifies where in code to add the snippets to the completion list. Several contexts can be combined.
		- **Function** - inside function body. Also in the main script code (top-level statements).
		- **Type** - inside a class, struct or interface but not inside functions. Use for snippets that insert entire methods, properties, etc.
		- **Namespace** - outside of types. Use for snippets that insert entire types.
		- **Attributes** - use for snippets that insert an `[attribute]`.
		- **Line** - at the start of a line. For example check **Line** and **Any** for snippets that insert a `#directive`.
		- **Any** - anywhere.
		- **None** - nowhere.
		
		*/
	}
	
	static _Context s_context;
	
	public static void AddSnippets(List<CiComplItem> items, TextSpan span, CompilationUnitSyntax root, string code, bool surround, CSharpSyntaxContext syncon = null) {
		if (syncon != null) {
			//CSharpSyntaxContext was discovered later and therefore almost not used here.
			if (syncon.IsObjectCreationTypeContext) return;
			//CiUtil.GetContextType(syncon);
		}
		
		_Context context = _Context.Unknown;
		int pos = span.Start;
		bool inDirective = pos > 0 && code[pos - 1] == '#' && !surround; //when invoked the list after #, span does not include #
		
		if (inDirective) {
		} else if (pos < root.GetHeaderLength()) {
		} else {
			//get node from start
			var token = root.FindToken(pos);
			var node = token.Parent;
			
			//find ancestor/self that contains pos inside
			while (node != null && !node.Span.ContainsInside(pos)) node = node.Parent;
			
			switch (node) {
			case BlockSyntax:
			case SwitchSectionSyntax: //between case: and break;
			case ElseClauseSyntax:
			case LabeledStatementSyntax:
			case IfStatementSyntax s1 when pos > s1.CloseParenToken.SpanStart:
			case WhileStatementSyntax s2 when pos > s2.CloseParenToken.SpanStart:
			case DoStatementSyntax s3 when pos < s3.WhileKeyword.SpanStart:
			case ForStatementSyntax s4 when pos > s4.CloseParenToken.SpanStart:
			case CommonForEachStatementSyntax s5 when pos > s5.CloseParenToken.SpanStart:
			case LockStatementSyntax s6 when pos > s6.CloseParenToken.SpanStart:
			case FixedStatementSyntax s7 when pos > s7.CloseParenToken.SpanStart:
			case UsingStatementSyntax s8 when pos > s8.CloseParenToken.SpanStart:
				context = _Context.Function;
				break;
			case TypeDeclarationSyntax td when pos > td.OpenBraceToken.Span.Start: //{ } of class, struct, interface
				context = _Context.Type;
				break;
			case NamespaceDeclarationSyntax ns when pos > ns.OpenBraceToken.Span.Start:
			case FileScopedNamespaceDeclarationSyntax ns2 when pos >= ns2.SemicolonToken.Span.End:
				context = _Context.Namespace;
				break;
			case CompilationUnitSyntax:
			case null:
				context = _Context.Namespace | _Context.Function; //Function for top-level statements. TODO3: only if in correct place.
				break;
			case LambdaExpressionSyntax:
			case ArrowExpressionClauseSyntax: //like void F() =>here
				context = _Context.Arrow;
				break;
			case AttributeListSyntax:
				context = _Context.Attributes;
				break;
			default:
				if (span.IsEmpty) { //if '=> here;' or '=> here)' etc, use =>
					var t2 = token.GetPreviousToken();
					if (t2.IsKind(SyntaxKind.EqualsGreaterThanToken) && t2.Parent is LambdaExpressionSyntax) context = _Context.Arrow;
				}
				break;
			}
		}
		s_context = context;
		
		if (s_items == null) {
			var a = new List<_CiComplItemSnippet>();
			foreach (var f in filesystem.enumFiles(AppSettings.DirBS, "*Snippets.xml")) _LoadFile(f.FullPath, true);
			_LoadFile(DefaultFile, false);
			if (a.Count == 0) return;
			a.Sort((x, y) => { //for the surround list
				int r = CiUtil.SortComparer.Compare(x.Text, y.Text);
				if (r == 0) return (x.custom ? 0 : 1) - (y.custom ? 0 : 1); //custom first
				return r;
			});
			_DetectContextsOfSnippets(a);
			s_items = a;
			
			void _LoadFile(string file, bool custom) {
				try {
					var hidden = DSnippets.GetHiddenSnippets(custom ? pathname.getName(file) : "default");
					if (hidden?.Contains("") == true) return;
					
					var xroot = LoadSnippetsFile_(file);
					if (xroot == null) return;
					foreach (var xs in xroot.Elements("snippet")) {
						var name = xs.Attr("name");
						if (hidden?.Contains(name) == true) continue;
						a.Add(new _CiComplItemSnippet(name, xs, custom));
					}
				}
				catch (Exception ex) { print.it("Failed to load snippets from " + file + "\r\n\t" + ex.ToStringWithoutStack()); }
			}
		}
		
		bool isLineStart = CodeUtil.IsLineStart(code, pos - (inDirective ? 1 : 0));
		
		for (int i = 0; i < s_items.Count; i++) {
			var v = s_items[i];
			if (!v.context.HasAny(context)) continue;
			if (v.context.Has(_Context.Line) && !isLineStart) continue;
			if (!surround && v.Text.Ends("Surround")) continue;
			if (inDirective) {
				if (v.Text[0] != '#') continue;
				v = new _CiComplItemSnippet(v.Text[1..], v.x, v.custom); //like in VS. Else typing-filtering does not work.
			}
			v.group = 0; v.hidden = 0; v.hilite = 0; v.moveDown = 0;
			v.ci.Span = span;
			items.Add(v);
		}
	}
	
	internal static XElement LoadSnippetsFile_(string file) {
		var xroot = XmlUtil.LoadElem(file);
		if (xroot.Name != "snippets") {
			if (xroot.Name == "Au.Snippets") {
				xroot = _ConvertOldFormat(xroot);
				try { xroot.Save(file); } catch { }
			} else return null;
		}
		return xroot;
		
		static XElement _ConvertOldFormat(XElement xRootOld) {
			var xRootNew = new XElement("snippets");
			foreach (var xg in xRootOld.Elements("group")) {
				foreach (var xs in xg.Elements("snippet")) {
					_ConvertSnippet(xs);
					xRootNew.Add(xs);
				}
			}
			return xRootNew;
			
			static void _ConvertSnippet(XElement xs) {
				if (!xs.HasElements) _ConvertCode(xs);
				else foreach (var x in xs.Elements("list")) _ConvertCode(x);
				
				static void _ConvertCode(XElement x) {
					if (x.Value is string v) {
						if (v.Find("$end$") is int i1 && i1 >= 0) { //$end$xxx$end$ -> ${1:xxx}, or $end$ -> $0
							int i1end = i1 + 5;
							if (v.Find("$end$", i1end) is int i2 && i2 > 0) v = v.ReplaceAt(i1..(i2 + 5), "${1:" + v[i1end..i2] + "}");
							else v = v.ReplaceAt(i1..i1end, i1end < v.Length && v[i1end] is >= '0' and <= '9' ? "${0}" : "$0");
						}
						v = v.Replace("$random$", "${RANDOM}");
						v = v.Replace("$guid$", "${GUID}");
						v = v.Replace("$var$", "${VAR}");
						x.Value = v;
					}
				}
			}
		}
	}
	
	static void _DetectContextsOfSnippets(List<_CiComplItemSnippet> a) {
		regexp rx1 = new(@"\$(?|(VAR|RANDOM|RANDOM_HEX|TM_FILENAME_BASE)\b|\{((?1))\})"),
			rx2 = new(@"\$(?|([1-9]\d*)(?!\d)|\{((?1))\})"),
			rx3 = new(@"\$(?:\{[A-Z_]+\}|[A-Z_]+\b|0|\{0\})"),
			rx4 = new(@"\$\{\d+:(.*?)\}");
		
		Parallel.ForEach(a, v => {
			if (v.x.Attr("context") is string s) {
				v.context = _GetFromAttr(s);
			} else {
				var x = v.x.HasElements ? v.x.Elements().First() : v.x;
				v.context = _Detect(x.Value, v.Text);
			}
		});
		
		_Context _Detect(string code, string name) {
			if (code.NE()) return _Context.None;
			
			code = rx1.Replace(code, "$1");
			code = rx2.Replace(code, "i");
			code = rx3.Replace(code, "");
			code = rx4.Replace(code, "$1");
			
			//bool debug = name.Starts("ctor");
			//if (debug) {
			//	print.it($"<><lc #B3DF00>{name}<>\r\n<\a>{code}</\a>");
			//}
			
			try {
				var cu = CiUtil.GetSyntaxTree(code);
				if (cu.Usings.Any() || cu.Externs.Any() || cu.AttributeLists.Any()) return _Context.Namespace;
				if (!cu.Members.Any()) {
					if (cu.ContainsDirectives) return _Context.Any | _Context.Line;
					if (code.Starts("/// ")) return _Context.Namespace | _Context.Type | _Context.Line;
					return _Context.Any;
				}
				if (cu.Members.Any(SyntaxKind.GlobalStatement)) {
					if (!cu.Members.All(o => o is GlobalStatementSyntax)) {
						if (cu.Members[0] is GlobalStatementSyntax) return _Context.Namespace; //TLS + types
						//Debug_.Print($"{name}: GlobalStatement after non-global");
						if (_TryInClass()) return _Context.Type;
						return _Context.Function;
					}
					foreach (GlobalStatementSyntax gs in cu.Members) {
						var stat = gs.Statement;
						if (stat is LocalFunctionStatementSyntax) { //can be local or member function
							if (_TryInClass()) return _Context.Type;
						} else if (stat is LocalDeclarationStatementSyntax lds) { //can be local or member variable
							if (lds.Declaration.Type.IsVar) return _Context.Function;
						} else {
							if (_TryInClass()) return _Context.Type;
							//if (stat is ExpressionStatementSyntax ess && ess.SemicolonToken.IsMissing && ess.Expression is InvocationExpressionSyntax && cu.Members.Count == 1) return _Context.Attributes; //`Attribute(...)` //rejected
							return _Context.Function;
						}
					}
					return _Context.Type | _Context.Function;
				} else {
					if (cu.Members.Any(o => o is BaseNamespaceDeclarationSyntax)) return _Context.Namespace;
					if (cu.Members.All(o => o is BaseTypeDeclarationSyntax)) return _Context.Namespace | _Context.Type;
					return _Context.Type;
				}
				
				bool _TryInClass(bool debug = false) {
					try {
						int n1 = cu.GetDiagnostics().Count();
						if (n1 == 0) return false;
						var cu2 = CiUtil.GetSyntaxTree("class C{\r\n" + code + "\r\n}");
						var d2 = cu2.GetDiagnostics();
						int n2 = d2.Count();
						if (n2 >= n1) return false;
						if (n2 > 0 && d2.Any(o => o.Code == 1519)) return false; //"Invalid token 'token' in class, struct, or interface member declaration". Eg elseSnippet.
						return true;
					}
					catch { return false; }
				}
			}
			catch { return _Context.Any; }
		}
		
		static _Context _GetFromAttr(string s) {
			_Context r = 0;
			foreach (var se in s.Split(.., '|')) {
				r |= s.AsSpan(se.Range) switch {
					"Function" => _Context.Function | _Context.Arrow,
					"Type" => _Context.Type,
					"Namespace" => _Context.Namespace,
					"Attributes" => _Context.Attributes,
					"Any" => _Context.Any,
					"Line" => _Context.Line,
					_ => 0
				};
			}
			return r;
		}
	}
	
	public static void Reload() => s_items = null;
	
	public static int Compare(CiComplItem i1, CiComplItem i2) {
		if (i1 is _CiComplItemSnippet x && i2 is _CiComplItemSnippet y) {
			return (x.custom ? 0 : 1) - (y.custom ? 0 : 1); //sort custom first
		}
		return 0;
	}
	
	public static System.Windows.Documents.Section GetDescription(CiComplItem item) {
		var snippet = item as _CiComplItemSnippet;
		var m = new CiText();
		m.StartParagraph();
		m.Append("Snippet "); m.Bold(item.Text); m.Append(".");
		_AppendInfo(snippet.x);
		bool isList = snippet.x.HasElements;
		if (isList) {
			foreach (var v in snippet.x.Elements("list")) {
				m.Separator();
				m.StartParagraph();
				m.Append(StringUtil.RemoveUnderlineChar(v.Attr("item")));
				_AppendInfo(v);
				_AppendCode(v);
			}
		} else {
			_AppendCode(snippet.x);
		}
		if (snippet.x.Attr(out string more, "more")) {
			if (isList) m.Separator();
			m.StartParagraph(); m.Append(more); m.EndParagraph();
		}
		
		//CONSIDER: add link "Edit". User suggestion.
		
		return m.Result;
		
		void _AppendInfo(XElement x) {
			if (x.Attr(out string info, "info")) m.Append(" " + info);
			m.EndParagraph();
		}
		
		void _AppendCode(XElement x) {
			m.CodeBlock(x.Value.Replace("$end$", ""));
		}
	}
	
	/// <summary>
	/// Surrounds a text range with a snippet.
	/// </summary>
	/// <param name="snippetXml">If null, shows menu with all "surround" snippets that are valid at current place in code. Else can be either full snippet XML or just code; must contain <c>${SELECTED_TEXT}</c>.</param>
	/// <param name="range">If null, uses the selected range, or current statement etc if there is no selection.</param>
	/// <remarks>
	/// Can also insert using directives etc where need.
	/// Formats the text and snippet.
	/// Can start snippet mode (Tab-navigation etc).
	/// </remarks>
	public static void Surround(string snippetXml = null, Range? range = null) {
		if (!CodeInfo.GetContextAndDocument(out var k)) return;
		var (from, to) = range?.GetStartEnd(k.code.Length) ?? CodeUtil.GetSurroundRange(k);
		
		XElement x;
		if (snippetXml != null) {
			if (!snippetXml.Starts('<')) snippetXml = "<snippet><![CDATA[" + snippetXml + "]]></snippet>";
			x = XElement.Parse(snippetXml);
		} else {
			List<CiComplItem> a = new();
			AddSnippets(a, new(from, 0), k.syntaxRoot, k.code, true);
			if (a.Count == 0) return;
			
			var m = new popupMenu { RawText = true };
			List<XElement> list = new();
			foreach (_CiComplItemSnippet snippet in a) {
				string name = null;
				if (snippet.x.HasElements) {
					list.Clear();
					foreach (var v in snippet.x.Elements("list")) if (_CanAdd(v)) list.Add(v);
					if (list.Count > 3) {
						var sub = new popupMenu { RawText = true };
						foreach (var v in list) _Add(sub, v, true);
						m.Submenu(name, () => sub);
					} else if (list.Count > 0) {
						foreach (var v in list) _Add(m, v, true);
					}
				} else {
					if (_CanAdd(snippet.x)) _Add(m, snippet.x, false);
				}
				
				bool _CanAdd(XElement x) => x.Value.Contains("${SELECTED_TEXT}");
				
				void _Add(popupMenu m, XElement x, bool listItem) {
					string s = name;
					if (s == null) {
						s = snippet.Text;
						if (s.Like("*?Snippet")) s = s[..^7];
						else if (s.Like("*?Surround")) s = s[..^8];
						name = s;
					}
					if (listItem) s = s + "  |  " + StringUtil.RemoveUnderlineChar(x.Attr("item"));
					var v = m.Add(s);
					v.Tag = x;
					v.Tooltip = snippet.x.Attr("info") + "\n\n" + x.Value;
				}
				//CONSIDER: hotkeys for surround snippets.
			}
			if (0 == m.Show()) return;
			x = m.Result.Tag as XElement;
		}
		
		if (to - from > 1 && k.code[to - 1] == '\n') {
			if (k.code[--to - 1] == '\r') to--;
		}
		
		_Commit(k.sci, from, to, x, k.code[from..to]);
	}
	
	public static void Commit(SciCode doc, CiComplItem item, int codeLenDiff) {
		doc.SnippetMode_?.End();
		
		var snippet = item as _CiComplItemSnippet;
		var x = snippet.x;
		
		//list of snippets?
		if (x.HasElements) {
			var a = x.Elements("list").ToArray();
			var m = new popupMenu();
			foreach (var v in a) m.Add(v.Attr("item"));
			m.FocusedItem = m.Items.First();
			int g = m.Show(PMFlags.ByCaret | PMFlags.Underline);
			if (g == 0) return;
			x = a[g - 1];
		}
		
		_Commit(doc, item.ci.Span.Start, item.ci.Span.End + codeLenDiff, x);
	}
	
	static void _Commit(SciCode doc, int pos, int endPos, XElement x, string surroundText = null) {
		doc.SnippetMode_?.End();
		
		var xSnippet = x.Name == "list" ? x.Parent : x;
		
		string s = x.Value;
		
		//##directive -> #directive
		if (s.Starts('#') && doc.aaaText.Eq(pos - 1, '#')) s = s[1..];
		
		//get variable name from code
		string varName = null;
		if (_GetAttr("var", out string attrVar)) {
			if (attrVar.RxMatch(@"^(.+?), *(.+)$", out var m)) {
				try {
					var t = CodeUtil.GetNearestLocalVariableOfType(m[1].Value);
					varName = t?.Name ?? m[2].Value;
				}
				catch (ArgumentException ex1) { print.it($"Error in {xSnippet.Attr("name")}: {ex1.Message}"); }
			}
		}
		
		//enclose in { } if in =>
		if (s_context == _Context.Arrow && !s.Starts("throw ")) {
			if (s.Contains('\n')) {
				s = "{\r\n" + s.RxReplace(@"(?m)^", "\t") + "\r\n}";
			} else {
				s = "{ " + s + " }";
			}
			//never mind: should add ; if missing
		}
		
		//rejected: ensure unique names of declared variables.
		
		//maybe need meta comments
		if (_GetAttr("meta", out var attrMeta)) {
			int len1 = doc.aaaLen16;
			if (InsertCode.MetaComment(attrMeta)) {
				int lenDiff = doc.aaaLen16 - len1;
				pos += lenDiff;
				endPos += lenDiff;
			}
		}
		
		//maybe need using directives
		if (_GetAttr("using", out var attrUsing)) {
			int len1 = doc.aaaLen16;
			if (InsertCode.UsingDirective(attrUsing)) {
				int lenDiff = doc.aaaLen16 - len1;
				pos += lenDiff;
				endPos += lenDiff;
			}
		}
		
		var snippetMode = new CiSnippetMode(ref s, doc, varName, surroundText);
		snippetMode.Start(pos, endPos, s);
		
		if (_GetAttr("print", out var attrPrint)) {
			print.it(attrPrint.Insert(attrPrint.Starts("<>") ? 2 : 0, "Snippet " + xSnippet.Attr("name") + " says: "));
		}
		
		bool _GetAttr(string name, out string value) => x.Attr(out value, name) || (x != xSnippet && xSnippet.Attr(out value, name));
	}
	
	public static readonly string DefaultFile = folders.ThisApp + @"Default\Snippets.xml";
	public static readonly string CustomFile = AppSettings.DirBS + "Snippets.xml";
}

class CiSnippetMode {
	//$n/${n:text} info.
	record struct _Dollar(int n, string text, int offset, int len);
	
	//$n/${n:text} info used in snippet mode.
	class _Field {
		public int start, end, n;
#if DEBUG
		public override string ToString() => $"_Field {n} ({start}..{end})";
#endif
	}
	
	SciCode _doc;
	List<_Dollar> _dollars;
	_Field[] _fields;
	_Field _activeField, _modifiedField;
	StartEnd _range;
	int _finalCaretPos;
	bool _ignoreModified, _ignorePosChanged;
	bool _isSurround;
	
	const string c_markComment = "/*\f\v*/", c_markAlt = "__\f\v__";
	
	public CiSnippetMode(ref string s, SciCode doc, string varName, string selectedText) {
		_doc = doc;
		_isSurround = !selectedText.NE();
		
		//replace escape sequences in an easy but not perfect way
		bool escaped = 0 != s.RxReplace(@"\\[$}\\]", m => m.Subject[m.End - 1] switch { '$' => "\uf100", '}' => "\uf101", _ => "\uf102" }, out s); //Unicode private use area
		
		//in a `/* */` use c_markAlt instead of c_markComment
		var blockComments = s.RxIsMatch(@"/\*.+?\*/") && CiUtil.GetSyntaxTree(s) is { } cu ? cu.DescendantTrivia().Where(o => o.Kind() is SyntaxKind.MultiLineCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia).Select(o => o.Span).ToArray() : null;
		
		if (s.Contains('$')) {
			StringBuilder b = new();
			int i = 0;
			bool hasDollar0 = false;
			foreach (var m in s.RxFindAll(@"(?s)\$(?:(?|\{(\d+)(?::(.+?))?\}|(\d+))|\{([A-Z].+?)\})")) {
				b.Append(s, i, m.Start - i);
				i = m.End;
				if (m[3].Exists) { //variable, eg ${SELECTED_TEXT}
					var v = m[3].Value;
					b.Append(_Variable(v));
				} else { //field, eg ${1:i} or $1
					_dollars ??= new();
					
					int n = m[1].Value.ToInt();
					if (n == 0) {
						if (hasDollar0) continue; //error in snippet. Multiple $0 have no sense.
						hasDollar0 = true;
					}
					
					string v = _Trim(m[2].Value);
					if (v.NE()) {
						if (n != 0) foreach (var q in _dollars) if (q.n == n && q.text != null) { v = q.text; break; } //eg `${n:s}, $n`. But ignore `$n, ${n:s}`, it's insane.
					} else if (v.Starts('$')) {
						v = _Variable(v[1..]);
					}
					
					v = v.NullIfEmpty_();
					string v2 = v;
					v ??= blockComments?.Any(o => o.ContainsInside(m.Start)) == true ? c_markAlt : c_markComment;
					
					_dollars.Add(new(n, v2, b.Length, v.Length));
					b.Append(v);
				}
			}
			if (b.Length > 0) {
				b.Append(s, i, s.Length - i);
				s = b.ToString();
			}
		}
		
		if (escaped) s = s.Replace('\uf100', '$').Replace('\uf101', '}').Replace('\uf102', '\\');
		
		string _Variable(string v) {
			string def = null;
			int i = v.IndexOf(':');
			if (i > 0) { def = v[++i..]; v = v[..--i]; }
			v = v switch {
				"SELECTED_TEXT" => selectedText,
				"VAR" => varName ?? "VAR",
				"GUID" => Guid.NewGuid().ToString(),
				"RANDOM" => new Random().Next(0, 1000000).ToString("d6"),
				"RANDOM_HEX" => new Random().Next(0, 0x1000000).ToString("x6"),
				"TM_FILENAME_BASE" => v, //info: TM_FILENAME_BASE is used in VSCode snippets
				_ => null
			};
			return v.NE() ? def : v;
		}
		
		static bool _IsSpace(char c) => SyntaxFacts.IsWhitespace(c) || SyntaxFacts.IsNewLine(c);
		
		static string _Trim(string s) {
			if (s != null) {
				int i = 0; while (i < s.Length && _IsSpace(s[i])) i++;
				int j = s.Length; while (j > i && _IsSpace(s[j - 1])) j--;
				if (i > 0 || j < s.Length) s = s[i..j];
			}
			return s;
		}
		
		//not supported these rarely used features:
		//	Rarely used variables.
		//	$VARIABLE. Supports only ${VARIABLE} and ${1:$VARIABLE}.
		//	Supports variables in field default value only in the simplest form: ${1:$VARIABLE}.
		//	Nested fields, like ${1:a ${2:b}}.
		//	Choice, like ${1|one,two,three|}.
		//	Variable transforms (regex).
		//	isFileTemplate.
	}
	
	public void Start(int pos, int endPos, string s) {
		int pos0 = pos;
		ModifyCode.FormatForInsert(ref s, ref pos, endPos, _dollars == null ? null : _ChangesCallback);
		
		void _ChangesCallback(IList<TextChange> a) {
			var dollars = _dollars.AsSpan();
			foreach (ref var v in dollars) v.offset += pos0 - pos;
			for (int i = dollars.Length; --i >= 0;) {
				int dStart = dollars[i].offset + pos, dEnd = dStart + dollars[i].len;
				//print.it($"dStart={dStart}, dEnd={dEnd}");
				foreach (var v in a) {
					int cStart = v.Span.Start, cEnd = v.Span.End;
					if (cStart >= dEnd) break;
					Debug.Assert(!((cStart < dStart && cEnd > dStart) || (cStart < dEnd && cEnd > dEnd))); //a change must not span dStart or dEnd
					var dif = v.NewText.Length - (cEnd - cStart);
					if (cStart <= dStart) dollars[i].offset += dif;
					else dollars[i].len += dif;
				}
			}
		}
		
		//CodeInfo.Pasting(_doc, silent: true); //to auto-add missing using directives //rejected. Does not work well with EReplaceTextGently (because it makes multiple modifications). Namespaces can be specified in snippet.
		if (_dollars == null) {
			_doc.aaaReplaceRange(true, pos, endPos, s, true);
		} else {
			//remove markers of empty fields
			for (int i = _dollars.Count; --i >= 0;) {
				if (_dollars[i].text == null) {
					int len = _dollars[i].len;
					s = s.Remove(_dollars[i].offset, len);
					_dollars.Ref(i).len = 0;
					for (int j = i + 1; j < _dollars.Count; j++) _dollars.Ref(j).offset -= len;
				}
			}
			//never mind: the formatter splits line `/*mark*/code` -> `/*mark*/\r\ncode`
			
			if (_isSurround) _doc.EReplaceTextGently(pos, endPos, s);
			else _doc.aaaReplaceRange(true, pos, endPos, s);
			
			int nDollar0 = 0; //max 1
			foreach (ref var v in _dollars.AsSpan()) {
				v.offset += pos;
				if (v.n == 0) nDollar0++;
			}
			
			if (_dollars.Count == 1) {
				var d = _dollars[0];
				_doc.aaaSelect(true, d.offset, d.offset + d.len, makeVisible: true);
				
				//show signature if like `Method($0...``. Also may need to add temp range.
				if (d.len == 0) {
					int k = d.offset - pos; //$n offset in s
					bool showSignature = s.RxIsMatch(@"\w[([][^)\]]*""?$", range: ..k);
					(int from, int to) tempRange = default;
					if (s.Eq(k - 1, "()") || s.Eq(k - 1, "[]") || s.Eq(k - 1, "\"\"")) tempRange = (d.offset, d.offset);
					else if (s.Eq(k - 2, "{  }")) tempRange = (d.offset - 1, d.offset + 1);
					
					if (tempRange.to > 0) CodeInfo._correct.BracketsAdded(_doc, tempRange.from, tempRange.to, default);
					if (showSignature) CodeInfo.ShowSignature();
				}
			} else {
				_fields = new _Field[_dollars.Count - nDollar0];
				_finalCaretPos = -1;
				
				int fi = 0, iStart = 0, nStart = int.MaxValue;
				foreach (var v in _dollars) {
					if (v.n == 0) {
						_finalCaretPos = _doc.aaaPos8(v.offset);
					} else {
						if (v.n < nStart) { nStart = v.n; iStart = fi; }
						var f = new _Field { n = v.n, start = v.offset, end = v.offset + v.len };
						_doc.aaaNormalizeRange(true, ref f.start, ref f.end);
						_doc.aaaIndicatorAdd(SciCode.c_indicSnippetField, false, f.start..f.end, f.n);
						_fields[fi++] = f;
					}
				}
				
				_range = new(pos, pos + s.Length);
				_doc.aaaNormalizeRange(true, ref _range.start, ref _range.end);
				
				_SetActiveField(_fields[iStart], select: true);
				
				_doc.SnippetMode_ = this;
			}
		}
	}
	
	public void End(bool goToFinal = false) {
		if (_doc.SnippetMode_ != this) return;
		_doc.SnippetMode_ = null;
		
		_doc.aaaIndicatorClear(SciCode.c_indicSnippetField);
		
		_FieldLeaved();
		
		if (goToFinal) _doc.aaaGoToPos(false, _finalCaretPos >= 0 ? _finalCaretPos : _range.end);
	}
	
	void _SetActiveField(_Field field, bool select) {
		if (field == _activeField) return;
		_FieldLeaved();
		_activeField = field;
		foreach (var f in _fields) {
			if (f.n == field.n) _doc.aaaIndicatorAdd(SciCode.c_indicSnippetFieldActive, false, f.start..f.end, f.n);
		}
		if (select) {
			_ignorePosChanged = true;
			_doc.aaaSelect(false, field.start, field.end);
			_ignorePosChanged = false;
		}
	}
	
	void _FieldLeaved() {
		if (_activeField == null) return;
		_activeField = null;
		_doc.aaaIndicatorClear(SciCode.c_indicSnippetFieldActive);
		_ReplaceTextOfRelatedFields();
	}
	
	_Field _FieldFromPos(int pos, int pos2) {
		foreach (var f in _fields) if (pos >= f.start && pos2 <= f.end) return f;
		return null;
	}
	
	_Field _FieldFromSel() => _FieldFromPos(_doc.aaaSelectionStart8, _doc.aaaSelectionEnd8);
	
	public void SciModified(in Sci.SCNotification n) {
		if (_ignoreModified) return;
		
		bool deleted = n.modificationType.Has(Sci.MOD.SC_MOD_DELETETEXT);
		int pos = n.position, len = n.length, pos2 = deleted ? pos + len : pos;
		
		bool cancel = false, cantReplaceRelated = false;
		if (deleted) {
			//cancel etc if deleted entire snippet, or part of snippet together with other code, or any part of a field together with other code. Other cases detected by _FieldFromPos.
			if (pos <= _range.start && pos2 >= _range.end) { //probably Undo
				cancel = cantReplaceRelated = true;
			} else if (pos < _range.start) {
				cancel = true;
				cantReplaceRelated = pos2 > _range.start;
			} else if (pos2 > _range.end) {
				cancel = true;
				cantReplaceRelated = pos < _range.end;
			} else {
				foreach (var f in _fields) {
					if (pos < f.start) cancel = pos2 > f.start;
					else if (pos2 > f.end) cancel = pos < f.end;
					if (cancel) break;
				}
				if (cancel) cantReplaceRelated = true;
				//else cancel = pos < _finalCaretPos && pos2 > _finalCaretPos;
			}
		}
		
		var field = cancel ? null : _FieldFromPos(pos, pos2);
		if (field == null) {
			if (cantReplaceRelated) _modifiedField = null;
			End();
			return;
		}
		
		if (deleted) len = -len;
		foreach (var f in _fields) {
			if (f.start > pos) f.start += len;
			if (f.end >= pos2) f.end += len;
		}
		if (_finalCaretPos >= pos2) _finalCaretPos += len;
		_range.end += len;
		
		_doc.aaaIndicatorAdd(SciCode.c_indicSnippetField, false, field.start..field.end, field.n);
		if (_activeField != null) _doc.aaaIndicatorAdd(SciCode.c_indicSnippetFieldActive, false, field.start..field.end, field.n);
		
		_modifiedField = field;
		
		//_TestShowFields();
	}
	
	public void SciPosChanged() {
		if (_ignorePosChanged) return;
		if (_FieldFromSel() is { } field) {
			_SetActiveField(field, select: false);
		} else {
			_FieldLeaved();
		}
	}
	
	public bool SciKey(KKey key, ModifierKeys mod) {
		switch ((key, mod)) {
		case (KKey.Escape, 0):
			End();
			return true;
		case (KKey.Enter, 0):
			if (_FieldFromSel() == null) break;
			End(goToFinal: true);
			return true;
		case (KKey.Tab, 0):
			return _Tab(false);
		case (KKey.Tab, ModifierKeys.Shift):
			return _Tab(true);
		}
		return false;
		
		bool _Tab(bool shift) {
			if (_activeField == null) return false;
			int i = Array.IndexOf(_fields, _activeField), nNow = _activeField.n, nNext;
			_Field fNext = null;
			if (shift) {
				nNext = 0;
				foreach (var f in _fields) if (f.n < nNow && f.n > nNext) { nNext = f.n; fNext = f; } //find max n that is > nNow
				if (nNext == 0) return true;
			} else {
				nNext = int.MaxValue;
				foreach (var f in _fields) if (f.n > nNow && f.n < nNext) { nNext = f.n; fNext = f; } //find min n that is > nNow
				if (nNext == int.MaxValue) {
					End(goToFinal: true);
					return true;
				}
			}
			_SetActiveField(fNext, select: true);
			return true;
		}
	}
	
	unsafe void _ReplaceTextOfRelatedFields() {
		var field = _modifiedField; if (field == null) return;
		_modifiedField = null;
		if (_fields.Any(o => o.n == field.n && o != field)) {
			
			//workaround for: Scintilla selects some text when clicked somewhere in the same line after a replacement.
			//if (Api.GetCapture() == _doc.AaWnd) Api.ReleaseCapture(); //works, but then caret stars to flicker. Scintilla does not listen for released capture.
			if (Api.GetCapture() == _doc.AaWnd && mouse.isPressed(MButtons.Left)) {
				var xy = mouse.xy; _doc.AaWnd.MapScreenToClient(ref xy);
				_doc.AaWnd.Send(Api.WM_LBUTTONUP, 0, Math2.MakeLparam(xy));
			}
			
			int textLen = field.end - field.start;
			var text = MemoryUtil.Alloc(textLen);
			try {
				MemoryUtil.Copy(_doc.aaaRangePointer(field.start, field.end), text, textLen);
				_ignoreModified = true;
				using var undo = new KScintilla.aaaUndoAction(_doc);
				foreach (var f in _fields) {
					if (f.n == field.n && f != field) {
						_doc.Call(Sci.SCI_SETTARGETRANGE, f.start, f.end);
						_doc.Call(Sci.SCI_REPLACETARGET, textLen, text);
						int len = textLen - (f.end - f.start), oldStart = f.start, oldEnd = f.end;
						if (_finalCaretPos >= oldEnd) _finalCaretPos += len;
						foreach (var q in _fields) {
							if (q.start > oldStart) q.start += len;
							if (q.end >= oldEnd) q.end += len;
						}
						_range.end += len;
					}
				}
			}
			finally {
				MemoryUtil.Free(text);
				_ignoreModified = false;
			}
			if (_doc.SnippetMode_ == this) { //else called from End()
				foreach (var f in _fields) {
					if (f.n == field.n && f != field) {
						_doc.aaaIndicatorAdd(SciCode.c_indicSnippetField, false, f.start..f.end, f.n);
					}
				}
			}
			//_TestShowFields();
		}
	}
	
	//#if DEBUG
	//	void _TestShowFields() {
	//		_doc.aaaIndicatorClear(SciCode.c_indicTestStrike);
	//		_doc.aaaIndicatorClear(SciCode.c_indicTestPoint);
	//		foreach (var f in _fields) {
	//			_doc.aaaIndicatorAdd(SciCode.c_indicTestStrike, false, f.start..f.end);
	//		}
	//		if (_finalCaretPos >= 0) _doc.aaaIndicatorAdd(SciCode.c_indicTestPoint, false, _finalCaretPos..(_finalCaretPos + 1));
	//	}
	//#endif
}
