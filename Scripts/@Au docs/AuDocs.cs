using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using System.Collections.Immutable;
using System.Xml.Linq;
using Markdig;
using System.Xml;

partial class AuDocs {
	CSharpCompilation _compilation;
	CSharpSemanticModel _semo;
	StringBuilder _b = new(), _bFile = new();
	int _filePos;
	string _code;
	int _currentFileIndex;
	ISymbol _currentSym;
	MarkdownPipeline _pipeline;
	_Analyze _analyze;
	
	string[] _auNamespaces = { "Au.Types.", "Au.More.", "Au.Triggers.", "Au." };
	HashSet<string> _hsException = new(), _hsSeealso = new();
	regexp _rxLineStart = new(@"(?m)^");
	regexp _rxEmptyLine = new(@"\R\h*\R");
	regexp _rxC = new(@"(?s)<c><!\[CDATA\[(<c.+?</c>)\]\]></c>");
	regexp _rxLi = new(@"(?m)^(\h*)<br /> *•");
	regexp _rxLi2 = new(@"(?m)^\h*-");
	regexp _rxIndentation, _rxIndentation2;
	regexp _rxParams;
	regexp _rxFixInherited = new(@"(</(summary|param|typeparam|remarks|example|returns|value|exception|seealso|code|note|para|table|div|h\d|ul|ol)>)(<(?2)\b)");
	regexp _rxFixInheritedBr = new(@"(</[a-z]+>)(?=<br\b)");
	regexp _rxParaNote = new(@"<(para|note)\b.*>((?s).+?)</\1>");
	regexp _rxLineBreak = new(@"(?m)\\(?!$)");
	
	public AuDocs() {
		var p = new MarkdownPipelineBuilder();
		p.BlockParsers.RemoveAll(o => o is Markdig.Parsers.IndentedCodeBlockParser or Markdig.Parsers.HeadingBlockParser);
		
		//rejected: disable EmphasisInlineParser (*, _), CodeInlineParser (`), EscapeInlineParser (\). It also disables the pipe tables extension and maybe some other. Instead we'll temporarily escape these characters.
		//p.InlineParsers.RemoveAll(o => o is not (Markdig.Parsers.Inlines.HtmlEntityParser or Markdig.Parsers.Inlines.LinkInlineParser or Markdig.Parsers.Inlines.AutolinkInlineParser or Markdig.Parsers.Inlines.LineBreakInlineParser or Markdig.Parsers.Inlines.LiteralInlineParser));
		
		p.DisableHeadings();
		p.UsePipeTables();
		_pipeline = p.Build();
		
		_analyze = new();
		
		opt.warnings.Verbose = true;
	}
	
	public void Preprocess(string sourceDir, string destDir, bool testSmall) {
		filesystem.delete(destDir);
		filesystem.createDirectory(destDir);
		
		//info: currently there are no excluded .cs files. Would need to parse Au.csproj.
		
		var parseOpt = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse);
		var trees = new List<CSharpSyntaxTree>();
		var files = new List<(string path, string code)>();
		
		foreach (var f in filesystem.enumerate(sourceDir, FEFlags.NeedRelativePaths, dirFilter: o => o.Level == 0 && 0 != o.Name.Eq(true, @"\obj", @"\bin", @"\docfx_project") ? 0 : 2)) {
			var path = f.FullPath;
			if (f.Name.Ends(".cs", true)) {
				if (testSmall && f.Name == @"\_Aaa.cs") continue;
				var code = filesystem.loadText(path);
				code = _PreprocessFileAsText(path, code);
				var tree = CSharpSyntaxTree.ParseText(code, parseOpt, f.Name) as CSharpSyntaxTree;
				trees.Add(tree);
				files.Add((f.Name, code));
			} else if (f.Name.Eqi(@"\Au.csproj")) {
				var s = filesystem.loadText(path);
				s = s.RxReplace(@"(?ms)^\h*<COMReference\b.+?</COMReference>", "", 1); //mute docfx warning
				s = s.Replace("<SignAssembly>true</SignAssembly>", "");
				s = s.RxReplace("<AssemblyOriginatorKeyFile>.+?</AssemblyOriginatorKeyFile>", "");
				filesystem.saveText(destDir + f.Name, s);
			} else {
				filesystem.copy(path, destDir + f.Name);
			}
		}
		
		var refs = Au.Compiler.MetaReferences.DefaultReferences.Values;
		var compOpt = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
		_compilation = CSharpCompilation.Create("doc", trees, refs, compOpt);
		
		//note: cannot get all declared symbols directly from Compilation. Somehow _compilation.GetSymbolsWithName skips ctors etc. Also tried other classes, unsuccessfully.
		//print.it(_compilation.GetSymbolsWithName(o=>true).Where(o=>!o.IsImplicitlyDeclared && o.HasPublicResultantVisibility()).Select(o=>o.ToString()).OrderBy(o=>o));
		//Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindSourceDeclarationsAsync //not tested
		
		for (int i = 0; i < trees.Count; i++) {
			_currentFileIndex = i;
			var tree = trees[i];
			_semo = _compilation.GetSemanticModel(tree) as CSharpSemanticModel;
			
			//if(!tree.FilePath.Ends("\\filesystem.cs")) continue;
			_bFile.Clear();
			_filePos = 0;
			var f = files[i];
			_code = f.code;
			_PreprocessFile(tree);
			_bFile.Append(f.code, _filePos, f.code.Length - _filePos);
			var s = _bFile.ToString();
			filesystem.saveText(destDir + f.path, s);
			
			//print.it($"<><lc green>{f.path}<>");
			//print.it(s);
			//if(f.path.Ends("param types.cs")) print.it(s);
		}
		
		_analyze.Finally();
	}
	
	void _PreprocessFile(CSharpSyntaxTree tree) {
		_analyze.StartingFile(_semo, _currentFileIndex);
		
		var root = tree.GetCompilationUnitRoot();
		foreach (var m1 in root.Members) {
			if (m1 is not BaseNamespaceDeclarationSyntax ns) throw new Exception("not namespace: " + m1);
			_Namespace(ns);
		}
	}
	
	void _Namespace(BaseNamespaceDeclarationSyntax ns) {
		_analyze.AnalyzeType(ns);
		
		foreach (var m2 in ns.Members) {
			switch (m2) {
			case NamespaceDeclarationSyntax ns2:
				_Namespace(ns2);
				break;
			case BaseTypeDeclarationSyntax type:
				_Type(type);
				break;
			case DelegateDeclarationSyntax del:
				_DocComments(del);
				break;
			default:
				throw new Exception("not type or namespace: " + m2);
			}
		}
		
		_analyze.AnalyzeType(null);
	}
	
	void _Type(BaseTypeDeclarationSyntax type) {
		if (!_DocComments(type)) return;
		if (type is TypeDeclarationSyntax td) {
			
			//remove some attributes
			foreach (var al in td.AttributeLists) {
				if (al.ToString() is "[ComVisible(true)]") _SkipSource(al.Span);
			}
			
			foreach (var m in td.Members) {
				if (m is BaseTypeDeclarationSyntax t2) _Type(t2);
				else _DocComments(m);
			}
		} else if (type is EnumDeclarationSyntax ed) {
			foreach (var m in ed.Members) {
				_DocComments(m, _semo.GetDeclaredSymbol(m));
			}
		} else throw new Exception("type: " + type);
		
		_analyze.AnalyzeType(null);
	}
	
	//returns true if public
	bool _DocComments(MemberDeclarationSyntax m, ISymbol sym = null) {
		//is public?
		if (sym == null) { //else enum member
			sym = m is BaseFieldDeclarationSyntax f ? _semo.GetDeclaredSymbol(f.Declaration.Variables[0]) : _semo.GetDeclaredSymbol(m);
			if (sym == null) { _Warning("Declared symbol not found", m); return false; }
			if (!sym.HasPublicResultantVisibility()) return false;
			
			if (m is BaseTypeDeclarationSyntax btd) _analyze.AnalyzeType(btd);
		}
		
		var lt = m.GetLeadingTrivia();
		var trivia = lt.LastOrDefault(o => o.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
		var span = trivia.Span;
		bool autoInheritdoc = false;
		if (trivia.RawKind == 0) {
			autoInheritdoc = _IsEligibleForAutomaticInheritdoc(sym);
			if (!autoInheritdoc) return true;
			//tested: DocFX does not get method documentation from overriden base method or implemented interface method.
		} else if (span.Length < 10) return true; //length 2 if just ///
		
		var sdoc = autoInheritdoc ? "" : _code[(span.Start + (_code[span.Start] == ' ' ? 1 : 0))..span.End];
		_currentSym = sym;
		_analyze.AnalyzeDoc(sdoc, sym);
		
		if (span.Length > 0) {
			_SkipSource(span.Start - 3, span.End);
		} else {
			int i = m.SpanStart;
			_SkipSource(i, i);
			_bFile.AppendLine();
		}
		
		//print.it($"<><c brown>{AuDocs.VsGoto(sym)}<>");
		
		string sxml;
		if (autoInheritdoc || sdoc.Contains("<inheritdoc")) {
			sxml = sym.GetDocumentationComment(_compilation, expandIncludes: true, expandInheritdoc: true).FullXmlFragment;
			
			//inherited XML now is without newlines between block tags. Restore.
			if (0 != _rxFixInherited.Replace(sxml, "$1\n$3", out sxml)) {
				//print.it($"<><c brown>{sym}<>");
				//print.it(sxml);
			}
			if (0 != _rxFixInheritedBr.Replace(sxml, "$1\n", out sxml)) {
				//print.it($"<><c brown>{sym}<>");
				//print.it(sxml);
			}
		} else { //faster without GetDocumentationComment, which calls GetDocumentationCommentXml and then creates DocumentationComment, which parses XML again
			sxml = sym.GetDocumentationCommentXml(expandIncludes: true);
		}
		
		if (sxml.Starts("<!-- Badly formed XML comment ignored")) { //eg EFileInfo.kind (record, <param> text empty)
			//print.it(sxml);
			return true;
		}
		
		//remove indentation added by GetDocumentationCommentXml etc
		_rxIndentation ??= new(@"(?>\n) +");
		if (!sxml.Starts("<doc>") && _rxIndentation.Match(sxml, 0, out string si)) {
			if (si.Length is not (5 or 6)) throw new InvalidOperationException(); //\n and 4-5 spaces
			_rxIndentation2 ??= new(@"(?m)^     ?(?=[^ ])");
			sxml = _rxIndentation2.Replace(sxml, "");
			//print.it("-----");
			//print.it(sxml);
		}
		//print.it(sxml);
		
		_b.Clear();
		
		var xr = XElement.Parse(sxml, LoadOptions.PreserveWhitespace);
		
		void _Typeparam(ImmutableArray<ITypeParameterSymbol> a) {
			foreach (var p in a) {
				if (xr.Elem("typeparam", "name", p.Name) is XElement x) _Append(x);
			}
		}
		void _Param(ImmutableArray<IParameterSymbol> a) {
			foreach (var p in a) {
				var x = xr.Elem("param", "name", p.Name);
				if (x == null || !_Append(x)) _analyze.MissingParam(p, x);
			}
		}
		
		//add only parameters of this symbol (ignore inherited)
		if (sym is INamedTypeSymbol nt) {
			if (nt.IsGenericType) _Typeparam(nt.TypeParameters);
		} else if (sym is IMethodSymbol ims) {
			if (ims.IsGenericMethod) _Typeparam(ims.TypeParameters);
			_Param(ims.Parameters);
		} else if (sym is IPropertySymbol ips && ips.IsIndexer) {
			_Param(ips.Parameters);
		}
		
		int nSummary = 0, nRemarks = 0, nExample = 0, nReturns = 0, nValue = 0; _hsException.Clear(); _hsSeealso.Clear(); //ignore inherited duplicates
		foreach (var x in xr.Elements()) {
			switch (x.Name.LocalName) {
			case "param" or "typeparam":
				break;
			case "summary":
				if (nSummary++ == 0) _Append(x);
				break;
			case "remarks":
				if (nRemarks++ == 0) _Append(x);
				break;
			case "example":
				if (nExample++ == 0) _Append(x);
				break;
			case "returns":
				if (nReturns++ == 0) _Append(x);
				//if (sym is not IMethodSymbol) _Warning("<returns> for a non-method.", x); //it's OK, for properties DocFX interprets it as <value>
				break;
			case "value":
				if (nValue++ == 0) _Append(x);
				break;
			case "exception":
				if (_hsException.Add(x.Attr("cref"))) _Append(x, true);
				break;
			case "seealso":
				if (_hsSeealso.Add(x.Attr("cref"))) _Append(x, true);
				break;
			case "inheritdoc":
				if (!autoInheritdoc) _Warning("Non-expanded <inheritdoc>. Try to fully qualify a parameter type.", sxml);
				continue;
			default:
				_Warning("unsupported tag", x);
				continue;
			}
		}
		
		sxml = _b.ToString();
		sdoc = _rxLineStart.Replace(sxml, "/// ");
		//print.it(sdoc);
		_bFile.Append(sdoc);
		
		_analyze.AnalyzeDoc2(xr, sxml);
		
		return true;
		
		bool _Append(XElement xp, bool hasCref = false) {
			if (hasCref) _Cref(xp);
			
			if (xp.IsEmpty || xp.FirstNode == null || (!xp.HasElements && string.IsNullOrWhiteSpace(xp.Value))) {
				_b.AppendLine(xp.ToString());
				return false;
			}
			
			//catch some mistakes of using HTML blocks in text that will be parsed by Markdig
			bool hasParaNote = false;
			foreach (var x in xp.Elements()) {
				var tag = x.Name.LocalName;
				switch (tag) {
				case "b" or "i" or "br" or "a" or "see" or "c" or "google" or "msdn" or "sqlite":
					break;
				case "code":
					break;
				case "note" or "para":
					//if <note> in param, in intellisense it is displayed as simple text without NOTE etc in the same line. Instead use <para>NOTE: text</para>, and here replace with <note type="note">Text</note>.
					if (tag[0] == 'n') {
						if (xp.Name.LocalName is "param" or "typeparam" or "summary") _Warning("<note> in param or summary. Bad for intellisense. Use <para>NOTE: Text</para>. The NOTE is text for note type attribute, and can be any uppercase text.", xp);
					} else if (x.FirstNode is XText t1 && t1 is not XCData) {
						var s2 = t1.Value;
						if (s2.Contains(':') && s2.RxMatch(@"\s*([A-Z]+):\s+((?s).+)", out var m2)) {
							t1.Value = m2[2].Value;
							x.Name = "note";
							x.SetAttributeValue("type", m2[1].Value.Lower());
						}
					}
					_EnsureSeparatedWithEmptyLine(x, false); _EnsureSeparatedWithEmptyLine(x, true); //prevent joining with the adjacent <p> etc
					x.AddFirst("\n\n"); x.Add("\n\n"); //enable parsing markdown inside (don't interpret it as a HTML block)
					hasParaNote = true;
					break;
				case "list":
					break;
				case "table" or "div" or "h3" or "h4" or "h5" or "h6" or "ul" or "ol":
					_AssertLine(x, false);
					_EnsureSeparatedWithEmptyLine(x, true); //after a HTML block need an empty line to turn on markdown parsing
					var s1 = x.ToString();
					if (0 != (1 & _rxEmptyLine.FindAllG(s1, 0).Count())) _Warning("A HTML block element contains odd count of empty lines. Possibly will be incorrectly parsed markdown.", s1);
					break;
				default:
					//print.it(x.Name.LocalName, sym);
					_Warning("Please add this tag to the switch block: " + tag, xp);
					break;
				}
			}
			
			bool hasC = false;
			foreach (var x in xp.Descendants()) {
				var tag = x.Name.LocalName;
				switch (tag) {
				case "see":
					_Cref(x);
					break;
				case "google" or "msdn" or "sqlite":
					x.Name = "a";
					var site = tag[0] switch { 'm' => "+site:microsoft.com", 's' => "+site:sqlite.org", _ => "" };
					x.SetAttributeValue("href", $"https://www.google.com/search?q={System.Net.WebUtility.UrlEncode(x.Value)}{site}");
					break;
				case "c":
					if (x.FirstNode is XCData) break;
					x.ReplaceAll(new XCData(x.ToString()));
					hasC = true;
					break;
				case "code":
					_AssertLine(x, false);
					_AssertLine(x, true);
					//replace <code> -> <pre>, and Base64-encode.
					//	Markdig skips <pre>...</pre>, even if contains empty lines.
					//	DocFX will not replace with <pre><code> which then would be parsed by the DocFX code highlighter at run time.
					//	When postprocessing, we'll set colors using Roslyn.
					string code = null;
					switch (x.FirstNode) {
					case XCData xx: code = xx.Value; break;
					case XText xx: code = xx.Value; break; //must be without html entities
					default: print.it(x.FirstNode.NodeType); continue;
					}
					x.Name = "pre";
					x.Value = "%%" + Convert.ToBase64String(Encoding.UTF8.GetBytes(code.TrimStart("\r\n"))) + "%%";
					break;
				case "seealso":
					_Warning("<seealso> in text.", x);
					break;
				case "list":
					_Warning("Don't use <list>. Instead use <ul> or <ol> or markdown '- item' or '<br/>• item'.", x);
					break;
				}
			}
			
			//escape some markdown inline characters
			uint escaped = 0;
			foreach (var n in xp.DescendantNodes()) {
				if (n is XText t && n is not XCData) {
					var v = t.Value;
					if (v.Contains('*')) { escaped |= 1; v = v.Replace("*", ",.1.,"); }
					if (v.Contains('_')) { escaped |= 2; v = v.Replace("_", ",.2.,"); }
					if (v.Contains('`')) { escaped |= 4; v = v.Replace("`", ",.3.,"); }
					if (v.Contains('\\') && _rxLineBreak.Replace(v, ",.4.,", out v) > 0) escaped |= 8; //escape \char but not \endofline
					if (escaped > 0) t.Value = v;
				}
			}
			
			//print.it("---");
			var sx = xp.ToString();
			//print.it(sx);
			int from = sx.IndexOf('>') + 1, to = sx.LastIndexOf('<');
			//print.it(from, to);
			var s = sx[from..to];
			//print.it(s);
			_b.Append(sx, 0, from);
			
			//list items should start with <br/>• to display in intellisense as list. Else would need <list ...><item><description>...</description></item>...
			//	note: by default VS saves not as UTF-8. To make it save as UTF-8, create or edit file ".editorconfig" in the solution dir, and add this:
			//		[*]
			//		charset = utf-8
			if (0 == _rxLi.Replace(s, "$1-", out s))
				if (xp.Name.LocalName is "param" or "returns" or "summary" && _rxLi2.IsMatch(s)) _Warning("Markdown list items in <summary>, <param> and <returns> should start with <br/>• (Alt+L) to display correctly in intellisense.", s);
			
			s = Markdig.Markdown.ToHtml(s, _pipeline);
			
			//if (s.Starts("<p>") && s.Ends("</p>\n") && s.Find("<p>", 3) < 0) s = s[3..^5]; //no, need <p> for CSS white-space: pre-line
			if (hasParaNote) s = _rxParaNote.Replace(s, m => m.Value.Replace("<p>", "").Replace("</p>", "")); //remove <p> from <para> and <note>
			if (hasC) s = _rxC.Replace(s, "$1");
			if (0 != (escaped & 1)) s = s.Replace(",.1.,", "*");
			if (0 != (escaped & 2)) s = s.Replace(",.2.,", "_");
			if (0 != (escaped & 4)) s = s.Replace(",.3.,", "`");
			if (0 != (escaped & 8)) s = s.Replace(",.4.,", "\\");
			
			_b.Append(s);
			_b.Append(sx, to, sx.Length - to);
			_b.Append('\n');
			
			return true;
		}
		
		//from Roslyn method GetDocumentationComment
		static bool _IsEligibleForAutomaticInheritdoc(ISymbol symbol) {
			if (symbol.IsOverride) return true;
			if (symbol.ContainingType is null) return false;
			if (symbol.Kind is SymbolKind.Method or SymbolKind.Property or SymbolKind.Event) return symbol.ExplicitOrImplicitInterfaceImplementations().Any();
			return false;
		}
	}
	
	void _Cref(XElement x) {
		string s = x.Attr("cref");
		if (s == null) return; //href
		//print.it(s);
		var kind = s[0];
		bool removeParameters = false, overload = false;
		
		//restore cref modified by GetDocumentationComment
		if (s.FindAny("`([") < 0) { //simple. Also GetFirstSymbolForDeclarationId can't find indexers where s ends with ".Item"
			s = s[2..];
		} else {
			var syms = DocumentationCommentId.GetSymbolsForDeclarationId(s, _compilation);
			if (syms.Length == 0) {
				_Warning("Can't find cref symbol " + s, x); //none
				s = s[2..].Replace("@", ""); //never mind: should replace ``1 etc for generic. Difficult etc.
				removeParameters = kind is 'M' or 'P'; //method, indexer
			} else {
				var sym = syms[0];
				s = sym.ToDisplayString(_formatCref); //same as ToString except: tuple (...) -> ValueTuple<...>. DocFX does not support tuple (...).
				
				removeParameters = sym is IMethodSymbol || (sym is IPropertySymbol ips && ips.IsIndexer);
				overload = removeParameters && sym.Name == _currentSym.Name && sym.ContainingType == _currentSym.ContainingType;
				//don't remove parameters if sym and _currentSym are overloads of the same method or indexer
			}
			s = s.Replace('<', '{').Replace('>', '}');
			
			if (removeParameters) {
				_rxParams ??= new(@"\bparams ");
				s = _rxParams.Replace(s);
			}
		}
		x.SetAttributeValue("cref", s);
		
		//set link text if DocFX would do it incorrectly
		if (!overload && x.IsEmpty && x.Name.LocalName.Starts("see")) {
			//print.it(s);
			int from = 0, to = s.Length, i; string append = null;
			foreach (var v in _auNamespaces) if (s.Starts(v)) { from = v.Length; break; }
			if (removeParameters) {
				switch (kind) {
				case 'M':
					i = s.IndexOf('(');
					if (i > 0) to = i;
					break;
				case 'P':
					i = s.IndexOf('[');
					if (i > 0) { to = i; append = "[]"; }
					break;
				}
			}
			s = s[from..to];
			s = s.Replace('{', '<');
			s = s.Replace('}', '>');
			s += append;
			//print.it(s);
			x.Value = s;
			if (x.Name.LocalName == "seealso") { //DocFX ignores value. Let's insert it as <seealso href> for postprocessing.
				_b.AppendLine($"""<seealso href="https://text">{Convert2.HexEncode(Encoding.UTF8.GetBytes(s))}</seealso>""");
			}
		}
	}
	
	SymbolDisplayFormat _formatCref = new SymbolDisplayFormat(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeExplicitInterface,
		parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
		delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
		extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
		propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.ExpandValueTuple | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
		);
	
	void _SkipSource(int from, int to) {
		_bFile.Append(_code, _filePos, from - _filePos);
		_filePos = to;
	}
	
	void _SkipSource(TextSpan span) => _SkipSource(span.Start, span.End);
	
	/// <summary>prints warning if x is not at the start or end of a line</summary>
	void _AssertLine(XElement x, bool end) {
		var n = end ? x.NextNode : x.PreviousNode;
		if (n == null) return;
		if (n is XText t && n is not XCData) {
			var v = t.Value;
			if (end ? v.Starts("\n") : v.Ends("\n")) return;
		}
		//if(end) x.AddAfterSelf("\n"); else x.AddBeforeSelf("\n");
		var tag = x.Name.LocalName;
		_Warning(end ? $"</{tag}> must end a line." : $"<{tag}> must start a line.", x.Parent);
	}
	
	void _EnsureSeparatedWithEmptyLine(XElement x, bool end) {
		var n = end ? x.NextNode : x.PreviousNode;
		if (n == null) return;
		if (n is XText t && n is not XCData) {
			var v = t.Value;
			if (end ? v.Starts("\n\n") : v.Ends("\n\n")) return;
			if (v == "\n") if ((end ? n.NextNode : n.PreviousNode) == null) return;
			if (end ? v.Starts("\n") : v.Ends("\n")) {
				t.Value = end ? "\n" + v : v + "\n";
				return;
			}
		}
		_AssertLine(x, end);
	}
	
	void _Warning(string warning, object text) {
		print.warning($"<_>{warning}</_>\r\n\tMember: {VsGoto(_currentSym)} in {_semo.SyntaxTree.FilePath}\r\n\tText: <_>{text}</_>", 1);
	}
	
	public static string VsGoto(ISymbol sym) {
		var s = sym.QualifiedName();
		var text = sym.ToString();
		//var text = s;
		return $"<script VS goto.cs|{s}>{text}<>";
	}
	
	//static readonly PortableExecutableReference[] s_refs = Au.Compiler.MetaReferences.DefaultReferences.Values.ToArray();
	//static readonly CSharpParseOptions s_parseOpt = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse);
	//static readonly CSharpCompilationOptions s_compOpt = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
}
