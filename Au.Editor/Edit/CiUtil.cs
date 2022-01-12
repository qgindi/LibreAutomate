using System.Linq;
using Au.Controls;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
//using Microsoft.CodeAnalysis.FindSymbols;

static class CiUtil
{
	//not used
	//public static bool GetSymbolFromPos(out ISymbol sym, out CodeInfo.Context cd) {
	//	(sym, _, _, _) = GetSymbolEtcFromPos(out cd);
	//	return sym != null;
	//}

	public static bool Eq(this string t, TextSpan span, string s, bool ignoreCase = false)
		=> t.Eq(span.Start..span.End, s, ignoreCase);

	public static (ISymbol symbol, string keyword, HelpKind kind, SyntaxToken token) GetSymbolEtcFromPos(out CodeInfo.Context cd) {
		var doc = Panels.Editor.ZActiveDoc; if (doc == null) { cd = default; return default; }
		if (!CodeInfo.GetContextAndDocument(out cd)) return default;
		return GetSymbolOrKeywordFromPos(cd.document, cd.pos16, cd.code);
	}

	public static (ISymbol symbol, string keyword, HelpKind helpKind, SyntaxToken token) GetSymbolOrKeywordFromPos(Document document, int position, string code) {
		//using var p1 = perf.local();

		//CONSIDER: try SymbolFinder. Same speed.
		//var r = SymbolFinder.FindSymbolAtPositionAsync(document, position).Result;
		//print.it(r);

		if (position > 0 && SyntaxFacts.IsIdentifierPartCharacter(code[position - 1])) position--;

		var tree = document.GetSyntaxTreeAsync().Result;
		var token = tree.GetTouchingTokenAsync(position, default, findInsideTrivia: true).Result;
		if (token == default) return default;

		var span = token.Span; string word = code[span.Start..span.End];
		//PrintNode(token);

		if (token.IsKind(SyntaxKind.IdentifierToken)) {
			switch (word) {
			case "var":
			case "dynamic":
			case "nameof":
			case "unmanaged": //tested cases
				return (null, word, HelpKind.ContextualKeyword, token);
			}
		} else {
			var k = token.Kind();

			//PrintNode(token.GetPreviousToken());
			//print.it(
			//	//token.IsKeyword(), //IsReservedKeyword||IsContextualKeyword, but not IsPreprocessorKeyword
			//	SyntaxFacts.IsReservedKeyword(k), //also true for eg #if
			//	SyntaxFacts.IsContextualKeyword(k)
			//	//SyntaxFacts.IsQueryContextualKeyword(k) //included in IsContextualKeyword
			//	//SyntaxFacts.IsAccessorDeclarationKeyword(k),
			//	//SyntaxFacts.IsPreprocessorKeyword(k), //true if #something or can be used in #something context. Also true for eg if without #.
			//	//SyntaxFacts.IsPreprocessorContextualKeyword(k) //badly named. True only if #something.
			//	);
			//return default;

			if (SyntaxFacts.IsReservedKeyword(k)) {
				bool preproc = (word == "if" || word == "else") && token.GetPreviousToken().IsKind(SyntaxKind.HashToken);
				if (preproc) word = "#" + word;
				return (null, word, preproc ? HelpKind.PreprocKeyword : HelpKind.ReservedKeyword, token);
			}
			if (SyntaxFacts.IsContextualKeyword(k)) {
				return (null, word, SyntaxFacts.IsAttributeTargetSpecifier(k) ? HelpKind.AttributeTarget : HelpKind.ContextualKeyword, token);
			}
			if (SyntaxFacts.IsPreprocessorKeyword(k)) {
				//if(SyntaxFacts.IsPreprocessorContextualKeyword(k)) word = "#" + word; //better don't use this internal func
				if (token.GetPreviousToken().IsKind(SyntaxKind.HashToken)) word = "#" + word;
				return (null, word, HelpKind.PreprocKeyword, token);
			}
			switch (k) {
			case SyntaxKind.StringLiteralToken:
			case SyntaxKind.InterpolatedStringTextToken:
			case SyntaxKind.InterpolatedStringEndToken:
			case SyntaxKind.InterpolatedStringStartToken:
				return (null, null, HelpKind.String, token);
			}
		}
		//note: don't pass contextual keywords to GetSemanticInfo. It may get info for something other, eg for 'new' gets the ctor method.

		ISymbol symbol = null;
		var model = document.GetSemanticModelAsync().Result;
		//p1.Next();
		bool preferGeneric = token.GetNextToken().IsKind(SyntaxKind.GreaterThanToken);
		var si = model.GetSemanticInfo(token, document.Project.Solution.Workspace, default); //works better than GetSymbolInfo, and slightly faster. Or could call GetSymbolInfo, and if its Symbol is null, use its CandidateSymbols; not tested here.
		foreach (var v in si.GetSymbols(includeType: true)) {
			bool gen = false;
			switch (v) {
			case IErrorTypeSymbol: continue;
			case INamedTypeSymbol ints when ints.IsGenericType:
			case IMethodSymbol ims when ims.IsGenericMethod:
				gen = true;
				break;
			}
			//print.it(v, gen, v.Kind);
			if (gen == preferGeneric) { symbol = v; break; }
			symbol ??= v;
		}

		return (symbol, null, default, token);
	}

	public enum HelpKind
	{
		None, ReservedKeyword, ContextualKeyword, AttributeTarget, PreprocKeyword, String
	}

	public static void OpenSymbolOrKeywordFromPosHelp() {
		string url = null;
		var (sym, keyword, helpKind, _) = GetSymbolEtcFromPos(out _);
		if (sym != null) {
			url = GetSymbolHelpUrl(sym);
		} else if (keyword != null) {
			var s = helpKind switch {
				HelpKind.PreprocKeyword => "preprocessor directive",
				HelpKind.AttributeTarget => "attributes, ",
				_ => "keyword"
			};
			s = $"C# {s} \"{keyword}\"";
			//print.it(s); return;
			url = _GoogleURL(s);
		} else if (helpKind == HelpKind.String) {
			int i = popupMenu.showSimple("1 C# strings|2 String formatting|3 Wildcard expression|11 Regex tool (Ctrl+Space)|12 Keys tool (Ctrl+Space)", MSFlags.ByCaret);
			switch (i) {
			case 1: url = "C# strings"; break;
			case 2: url = "C# string formatting"; break;
			case 3: HelpUtil.AuHelp("articles/Wildcard expression"); break;
			case 11: CiTools.CmdShowRegexWindow(); break;
			case 12: CiTools.CmdShowKeysWindow(); break;
			}
			if (url != null) url = _GoogleURL(url);
		}
		if (url != null) run.itSafe(url);
	}

	static string _GoogleURL(string query) => "https://www.google.com/search?q=" + Uri.EscapeDataString(query);

	public static string GetSymbolHelpUrl(ISymbol sym) {
		//print.it(sym);
		//print.it(sym.IsInSource(), sym.IsFromSource());
		string query;
		IModuleSymbol metadata = null;
		foreach (var loc in sym.Locations) {
			if ((metadata = loc.MetadataModule) != null) break;
		}
		if (metadata != null) {
			bool au = metadata.Name == "Au.dll";
			if (au && sym.IsEnumMember()) sym = sym.ContainingType;
			query = sym.QualifiedName();
			query = query.Replace("..ctor", au ? ".-ctor" : null);
			if (au) return HelpUtil.AuHelpUrl(query);
			if (metadata.Name.Starts("Au.")) return null;
			string kind = (sym is INamedTypeSymbol ints) ? ints.TypeKind.ToString() : sym.Kind.ToString();
			query = query + " " + kind.Lower();
		} else if (!sym.IsInSource()) { //eg an operator of string etc
			if (!(sym is IMethodSymbol me && me.MethodKind == MethodKind.BuiltinOperator)) return null;
			//print.it(sym, sym.Kind, sym.QualifiedName());
			//query = "C# " + sym.ToString(); //eg "string.operator +(string, string)", and Google finds just Equality
			//query = "C# " + sym.QualifiedName(); //eg "System.String.op_Addition", and Google finds nothing
			query = "C# " + sym.ToString().RxReplace(@"\(.+\)$", "", 1).Replace('.', ' '); //eg C# string operator +, not bad
		} else if (sym.IsExtern) { //[DllImport]
			query = sym.Name + " function";
		} else if (sym is INamedTypeSymbol ints && ints.IsComImport) { //[ComImport]
			query = sym.Name + " " + ints.TypeKind.ToString().Lower();
		} else {
			return null;
		}

		return _GoogleURL(query);
	}

	/// <summary>
	/// Gets rectangle of caret if it was at the specified UTF-16 position.
	/// If <i>pos16</i> less than 0, uses current caret position.
	/// </summary>
	public static RECT GetCaretRectFromPos(SciCode doc, int pos16 = -1, bool inScreen = false) {
		int pos8 = pos16 < 0 ? doc.zCurrentPos8 : doc.zPos8(pos16);
		int x = doc.Call(Sci.SCI_POINTXFROMPOSITION, 0, pos8), y = doc.Call(Sci.SCI_POINTYFROMPOSITION, 0, pos8);
		var r = new RECT(x, y, 1, doc.Call(Sci.SCI_TEXTHEIGHT, doc.zLineFromPos(false, pos8)) + 2);
		if (inScreen) doc.Hwnd.MapClientToScreen(ref r);
		return r;
	}

	public static PSFormat GetParameterStringFormat(SyntaxNode node, SemanticModel semo, bool isString) {
		var kind = node.Kind();
		//print.it(kind);
		SyntaxNode parent;
		if (isString || kind == SyntaxKind.StringLiteralExpression) parent = node.Parent;
		else if (kind == SyntaxKind.InterpolatedStringText) parent = node.Parent.Parent;
		else return PSFormat.None;

		while (parent is BinaryExpressionSyntax && parent.IsKind(SyntaxKind.AddExpression)) parent = parent.Parent; //"string"+"string"+...

		PSFormat format = PSFormat.None;
		if (parent is ArgumentSyntax asy && parent.Parent is ArgumentListSyntax alis) {
			switch (alis.Parent) {
			case ObjectCreationExpressionSyntax oce:
				format = _GetFormat(oce);
				if (format == PSFormat.None) {
					switch (oce.Type.ToString()) { //fast if single word
					case "Regex":
					case "System.Text.RegularExpressions.Regex":
					case "RegexCompilationInfo":
					case "System.Text.RegularExpressions.RegexCompilationInfo":
						if ((object)asy == alis.Arguments[0]) format = PSFormat.NetRegex;
						break;
					}
				}
				break;
			case InvocationExpressionSyntax ies:
				format = _GetFormat(ies);
				if (format == PSFormat.None) {
					switch (ies.Expression.ToString()) {
					case "Regex.IsMatch":
					case "Regex.Match":
					case "Regex.Matches":
					case "Regex.Replace":
					case "Regex.Split":
						var aa = alis.Arguments;
						if (aa.Count >= 2 && (object)asy == aa[1]) format = PSFormat.NetRegex;
						break;
					}
				}
				break;
				//default:
				//	CiUtil.PrintNode(alis.Parent);
				//	break;
			}

			PSFormat _GetFormat(ExpressionSyntax es) {
				if (semo.GetSymbolInfo(es).Symbol is IMethodSymbol ims) {
					IParameterSymbol p = null;
					var pa = ims.Parameters;
					var nc = asy.NameColon;
					if (nc != null) {
						var name = nc.Name.Identifier.Text;
						foreach (var v in pa) if (v.Name == name) { p = v; break; }
					} else {
						int i; var aa = alis.Arguments;
						for (i = 0; i < aa.Count; i++) if ((object)aa[i] == asy) break;
						if (i >= pa.Length && pa[^1].IsParams) i = pa.Length - 1;
						if (i < pa.Length) p = pa[i];
					}
					if (p != null) {
						var fa = p.GetAttributes().FirstOrDefault(o => o.AttributeClass.Name == "ParamStringAttribute");
						if (fa != null) return fa.GetConstructorArgument<PSFormat>(0, SpecialType.None);
					}
				}
				return PSFormat.None;
			}
		}
		return format;
	}

	/// <summary>
	/// Returns true if node is in a "string literal" between "" or in a text part of an $"interpolated string".
	/// </summary>
	/// <param name="node">Any node. If returns true, finally its kind is StringLiteralExpression or InterpolatedStringExpression.</param>
	/// <param name="position"></param>
	public static bool IsInString(ref SyntaxNode node, int position) {
		if (node == null) return false;
		var nk = node.Kind();
		//print.it(nk, position, node.Span, node.GetType(), node);
		switch (nk) {
		case SyntaxKind.StringLiteralExpression:
			//return true only if position is in the string value.
			//false if <= the first " or >= the last ".
			//true if position is at the end of span and the last " is missing (error CS1010).
			var span = node.Span;
			int i = position - span.Start;
			if (i <= 0 || (i == 1 && node.ToString().Starts('@'))) return false;
			i = position - span.End;
			if (i > 0 || (i == 0 && !_NoClosingQuote(node))) return false;
			return true;
		case SyntaxKind.InterpolatedStringExpression:
			int j = node.Span.End - position;
			if (j != 1 && !(j == 0 && _NoClosingQuote(node))) return false;
			return true;
		case SyntaxKind.InterpolatedStringText:
		case SyntaxKind.Interpolation when position == node.SpanStart:
			node = node.Parent;
			nk = node.Kind();
			return nk == SyntaxKind.InterpolatedStringExpression;
		}
		return false;

		static bool _NoClosingQuote(SyntaxNode n) => n.ContainsDiagnostics && n.GetDiagnostics().Any(o => o.Id == "CS1010"); //Newline in constant
	}

	/// <summary>
	/// If range start..end is inside a string literal or char literal or text part of an interpolated string, returns literal type.
	/// Else if inside the prefix (@" or $@" etc), returns Prefix.
	/// Else returns None.
	/// The start and end of the literal are considered not inside.
	/// </summary>
	public static SType GetStringType(in SyntaxToken t, int start, int end, string code) {
		//PrintNode(t);
		var span = t.Span;
		if (start < span.Start || end > span.End) return 0;
		var kind = t.Kind();
		if (kind is SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken or SyntaxKind.InterpolatedStringStartToken or SyntaxKind.InterpolatedVerbatimStringStartToken) {
			if (start <= span.Start || end >= span.End) return 0; //assume the closing " isn't missing
			switch (kind) {
			case SyntaxKind.StringLiteralToken:
				if (code[span.Start] == '@') return start > span.Start + 1 ? SType.Verbatim : SType.Prefix;
				return SType.Simple;
			case SyntaxKind.CharacterLiteralToken:
				return SType.Char;
			default:
				return SType.Prefix;
			}
		} else {
			switch (kind) {
			case SyntaxKind.InterpolatedStringTextToken:
			case SyntaxKind.InterpolatedStringEndToken when end < span.End:
			case SyntaxKind.OpenBraceToken when end < span.End && t.Parent is InterpolationSyntax:
				return code.Eq(t.Parent.Parent.SpanStart, "$\"") ? SType.Interpolated : SType.InterVerbatim;
			}
		}
		return 0;
	}

	public enum SType { None, Simple, Verbatim, Interpolated, InterVerbatim, Char, Prefix }

	//FUTURE: remove if unused
	/// <summary>
	/// Gets syntax node at position.
	/// If document==null, calls CodeInfo.GetDocument().
	/// </summary>
	public static SyntaxNode NodeAt(int position, Document document = null) {
		if (document == null) {
			if (!CodeInfo.GetContextAndDocument(out var cd, position)) return null; //returns false if position is in meta comments
			document = cd.document;
			position = cd.pos16;
		}
		var root = document.GetSyntaxRootAsync().Result;
		return root.FindToken(position).Parent;
	}

	public static string GetTextWithoutUnusedUsingDirectives() {
		if (!CodeInfo.GetContextAndDocument(out var cd, 0, metaToo: true)) return cd.code;
		var code = cd.code;
		var semo = cd.document.GetSemanticModelAsync().Result;
		var a = semo.GetDiagnostics(null)
			.Where(d => d.Severity == DiagnosticSeverity.Hidden && d.Code == 8019)
			.Select(d => d.Location.SourceSpan)
			.OrderBy(span => span.Start);
		if (!a.Any()) return code;
		var b = new StringBuilder();
		int i = 0;
		foreach (var span in a) {
			int start = span.Start;
			if (start > i && code[start - 1] == ' ') start--;
			if (start > i) b.Append(code, i, start - i);
			i = span.End;
			if (b.Length == 0 || b[^1] == '\n') {
				if (code.Eq(i, "\r\n")) i += 2;
				else if (code.Eq(i, ' ')) i++;
			}
		}
		b.Append(code, i, code.Length - i);
		return b.ToString();
	}

	/// <summary>
	/// Gets "global using Namespace;" directives from all files of compilation. Skips aliases and statics.
	/// </summary>
	public static IEnumerable<UsingDirectiveSyntax> GetAllGlobalUsings(SemanticModel model) {
		foreach (var st in model.Compilation.SyntaxTrees) {
			foreach (var u in st.GetCompilationUnitRoot().Usings) {
				if (u.GlobalKeyword.RawKind == 0) break;
				if (u.Alias != null || u.StaticKeyword.RawKind != 0) continue;
				yield return u;
			}
		}
	}

	/// <summary>
	/// Finds child trivia of this token. Returns default if <i>position</i> is not in child trivia of this token. Does not descend into structured trivia.
	/// The code is from Roslyn source function FindTriviaByOffset. Roslyn has function to find trivia in SyntaxNode (recursive), but not in SyntaxToken.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="position">Position in whole code.</param>
	public static SyntaxTrivia FindTrivia(in this SyntaxToken t, int position) {
		int textOffset = position - t.Position;
		if (textOffset >= 0) {
			var leading = t.LeadingWidth;
			if (textOffset < leading) {
				foreach (var trivia in t.LeadingTrivia) {
					if (textOffset < trivia.FullWidth) return trivia;
					textOffset -= trivia.FullWidth;
				}
			} else if (textOffset >= leading + t.Width) {
				textOffset -= leading + t.Width;
				foreach (var trivia in t.TrailingTrivia) {
					if (textOffset < trivia.FullWidth) return trivia;
					textOffset -= trivia.FullWidth;
				}
			}
		}
		return default;
	}

	/// <summary>
	/// From C# code creates a Roslyn workspace+project+document for code analysis.
	/// If <i>needSemantic</i>, adds default references and a document with default global usings (same as in default global.cs).
	/// </summary>
	public static Document CreateRoslynDocument(string code, bool needSemantic) {
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId documentId = DocumentId.CreateNewId(projectId);
		var ws = new AdhocWorkspace();
		var pi = ProjectInfo.Create(projectId, VersionStamp.Default, "l", "l", LanguageNames.CSharp, null, null,
			new CSharpCompilationOptions(OutputKind.WindowsApplication, allowUnsafe: true),
			new CSharpParseOptions(LanguageVersion.Preview),
			metadataReferences: needSemantic ? new Au.Compiler.MetaReferences().Refs : null //tested: does not make slower etc
			);
		var sol = ws.CurrentSolution.AddProject(pi);
		if (needSemantic) {
			string code2 = @"global using Au;
global using Au.Types;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.IO.Compression;
global using System.Media;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Text.RegularExpressions;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Win32;
global using Au.More;
";
			sol = sol.AddDocument(DocumentId.CreateNewId(projectId), "g.cs", code2);
		}
		return sol.AddDocument(documentId, "l.cs", code).GetDocument(documentId);
	}

//	/// <summary>
//	/// From C# code creates a Roslyn workspace+project+document for syntax and semantic analysis.
//	/// If needSemantic adds default usings (same as in default global.cs; prepends to code) and references.
//	/// </summary>
//	public static Document CreateRoslynDocument(ref string code, bool needSemantic) {
//		if (needSemantic) code = @"using Au;
//using Au.Types;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Collections.Concurrent;
//using System.Diagnostics;
//using System.Globalization;
//using System.IO;
//using System.IO.Compression;
//using System.Media;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.Win32;
//using Au.More;
//" + code;
//		ProjectId projectId = ProjectId.CreateNewId();
//		DocumentId documentId = DocumentId.CreateNewId(projectId);
//		var ws = new AdhocWorkspace();
//		var pi = ProjectInfo.Create(projectId, VersionStamp.Default, "l", "l", LanguageNames.CSharp, null, null,
//			new CSharpCompilationOptions(OutputKind.WindowsApplication, allowUnsafe: true),
//			new CSharpParseOptions(LanguageVersion.Preview),
//			metadataReferences: needSemantic ? new Au.Compiler.MetaReferences().Refs : null //tested: does not make slower etc
//			);
//		return ws.CurrentSolution.AddProject(pi).AddDocument(documentId, "l.cs", code).GetDocument(documentId);
//	}

	/// <summary>
	/// Returns true if <i>code</i> contains global statements or is empty or the first method of the first class is named "Main".
	/// </summary>
	public static bool IsScript(string code) {
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId documentId = DocumentId.CreateNewId(projectId);
		using var ws = new AdhocWorkspace();
		var sol = ws.CurrentSolution.AddProject(projectId, "p", "p", LanguageNames.CSharp).AddDocument(documentId, "f.cs", code);
		var document = sol.GetDocument(documentId);
		var cu = document.GetSyntaxRootAsync().Result as CompilationUnitSyntax;
		var f = cu.Members.FirstOrDefault();
		if (f is GlobalStatementSyntax or null) return true;
		if (f is BaseNamespaceDeclarationSyntax nd) f = nd.Members.FirstOrDefault();
		if (f is ClassDeclarationSyntax cd && cd.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault().Identifier.Text == "Main") return true;
		return false;
	}

#if DEBUG
	public static void PrintNode(SyntaxNode x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (x == null) { print.it("null"); return; }
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, k={x.Kind()}, t={x.GetType().Name},<> '<c green>{x}<>'");
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}

	public static void PrintNode(SyntaxToken x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, {x.Kind()},<> '<c green>{x}<>'");
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}

	public static void PrintNode(SyntaxTrivia x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, {x.Kind()},<> '<c green>{x}<>'");
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}

	public static void HiliteRange(int start, int end) {
		var doc = Panels.Editor.ZActiveDoc;
		doc.InicatorsFind_(null);
		doc.InicatorsFind_(new List<Range> { start..end });
	}

	public static void HiliteRange(TextSpan span) => HiliteRange(span.Start, span.End);
#endif

	public static CiItemKind MemberDeclarationToKind(MemberDeclarationSyntax m) {
		return m switch {
			ClassDeclarationSyntax => CiItemKind.Class,
			StructDeclarationSyntax => CiItemKind.Structure,
			RecordDeclarationSyntax rd => rd.IsKind(SyntaxKind.RecordStructDeclaration) ? CiItemKind.Structure : CiItemKind.Class,
			EnumDeclarationSyntax => CiItemKind.Enum,
			DelegateDeclarationSyntax => CiItemKind.Delegate,
			InterfaceDeclarationSyntax => CiItemKind.Interface,
			OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax or IndexerDeclarationSyntax => CiItemKind.Operator,
			BaseMethodDeclarationSyntax => CiItemKind.Method,
			// => CiItemKind.ExtensionMethod,
			PropertyDeclarationSyntax => CiItemKind.Property,
			EventDeclarationSyntax or EventFieldDeclarationSyntax => CiItemKind.Event,
			FieldDeclarationSyntax f => f.Modifiers.Any(o => o.Text == "const") ? CiItemKind.Constant : CiItemKind.Field,
			EnumMemberDeclarationSyntax => CiItemKind.EnumMember,
			BaseNamespaceDeclarationSyntax => CiItemKind.Namespace,
			_ => CiItemKind.None
		};
	}

	public static void TagsToKindAndAccess(ImmutableArray<string> tags, out CiItemKind kind, out CiItemAccess access) {
		kind = CiItemKind.None;
		access = default;
		if (tags.IsDefaultOrEmpty) return;
		kind = tags[0] switch {
			WellKnownTags.Class => CiItemKind.Class,
			WellKnownTags.Structure => CiItemKind.Structure,
			WellKnownTags.Enum => CiItemKind.Enum,
			WellKnownTags.Delegate => CiItemKind.Delegate,
			WellKnownTags.Interface => CiItemKind.Interface,
			WellKnownTags.Method => CiItemKind.Method,
			WellKnownTags.ExtensionMethod => CiItemKind.ExtensionMethod,
			WellKnownTags.Property => CiItemKind.Property,
			WellKnownTags.Operator => CiItemKind.Operator,
			WellKnownTags.Event => CiItemKind.Event,
			WellKnownTags.Field => CiItemKind.Field,
			WellKnownTags.Local => CiItemKind.LocalVariable,
			WellKnownTags.Parameter => CiItemKind.LocalVariable,
			WellKnownTags.RangeVariable => CiItemKind.LocalVariable,
			WellKnownTags.Constant => CiItemKind.Constant,
			WellKnownTags.EnumMember => CiItemKind.EnumMember,
			WellKnownTags.Keyword => CiItemKind.Keyword,
			WellKnownTags.Namespace => CiItemKind.Namespace,
			WellKnownTags.Label => CiItemKind.Label,
			WellKnownTags.TypeParameter => CiItemKind.TypeParameter,
			//WellKnownTags.Snippet => CiItemKind.Snippet,
			_ => CiItemKind.None
		};
		if (tags.Length > 1) {
			access = tags[1] switch {
				WellKnownTags.Private => CiItemAccess.Private,
				WellKnownTags.Protected => CiItemAccess.Protected,
				WellKnownTags.Internal => CiItemAccess.Internal,
				_ => default
			};
		}
	}

	//The order must match CiItemKind.
	public static string[] ItemKindNames { get; } = new[] {
		"Class",
		"Structure",
		"Interface",
		"Enum",
		"Delegate",
		"Method",
		"ExtensionMethod",
		"Property",
		"Operator",
		"Event",
		"Field",
		"LocalVariable",
		"Constant",
		"EnumMember",
		"Namespace",
		"Keyword",
		"Label",
		"Snippet",
		"TypeParameter"
	};

#if DEBUG
	//unfinished. Just prints what we can get from CSharpSyntaxContext.
	public static /*CiContextType*/void GetContextType(/*in CodeInfo.Context cd,*/ CSharpSyntaxContext c) {
		//print.it("--------");
		print.clear();
		//print.it(cd.pos16);
		_Print("IsInNonUserCode", c.IsInNonUserCode);
		_Print("IsGlobalStatementContext", c.IsGlobalStatementContext);
		_Print("IsAnyExpressionContext", c.IsAnyExpressionContext);
		//_Print("IsAtStartOfPattern", c.IsAtStartOfPattern);
		//_Print("IsAtEndOfPattern", c.IsAtEndOfPattern);
		_Print("IsAttributeNameContext", c.IsAttributeNameContext);
		//_Print("IsCatchFilterContext", c.IsCatchFilterContext);
		_Print("IsConstantExpressionContext", c.IsConstantExpressionContext);
		_Print("IsCrefContext", c.IsCrefContext);
		_Print("IsDeclarationExpressionContext", c.IsDeclarationExpressionContext);
		//_Print("IsDefiniteCastTypeContext", c.IsDefiniteCastTypeContext);
		//_Print("IsEnumBaseListContext", c.IsEnumBaseListContext);
		//_Print("IsFixedVariableDeclarationContext", c.IsFixedVariableDeclarationContext);
		//_Print("IsFunctionPointerTypeArgumentContext", c.IsFunctionPointerTypeArgumentContext);
		//_Print("IsGenericTypeArgumentContext", c.IsGenericTypeArgumentContext);
		//_Print("IsImplicitOrExplicitOperatorTypeContext", c.IsImplicitOrExplicitOperatorTypeContext);
		_Print("IsInImportsDirective", c.IsInImportsDirective);
		//_Print("IsInQuery", c.IsInQuery);
		_Print("IsInstanceContext", c.IsInstanceContext);
		//_Print("IsIsOrAsOrSwitchOrWithExpressionContext", c.IsIsOrAsOrSwitchOrWithExpressionContext);
		//_Print("IsIsOrAsTypeContext", c.IsIsOrAsTypeContext);
		_Print("IsLabelContext", c.IsLabelContext);
		_Print("IsLocalVariableDeclarationContext", c.IsLocalVariableDeclarationContext);
		//_Print("IsMemberAttributeContext", c.IsMemberAttributeContext(new HashSet<SyntaxKind>(), default));
		_Print("IsMemberDeclarationContext", c.IsMemberDeclarationContext());
		_Print("IsNameOfContext", c.IsNameOfContext);
		_Print("IsNamespaceContext", c.IsNamespaceContext);
		_Print("IsNamespaceDeclarationNameContext", c.IsNamespaceDeclarationNameContext);
		_Print("IsNonAttributeExpressionContext", c.IsNonAttributeExpressionContext);
		_Print("IsObjectCreationTypeContext", c.IsObjectCreationTypeContext);
		_Print("IsOnArgumentListBracketOrComma", c.IsOnArgumentListBracketOrComma);
		_Print("IsParameterTypeContext", c.IsParameterTypeContext);
		_Print("IsPossibleLambdaOrAnonymousMethodParameterTypeContext", c.IsPossibleLambdaOrAnonymousMethodParameterTypeContext);
		_Print("IsPossibleTupleContext", c.IsPossibleTupleContext);
		_Print("IsPreProcessorDirectiveContext", c.IsPreProcessorDirectiveContext);
		_Print("IsPreProcessorExpressionContext", c.IsPreProcessorExpressionContext);
		_Print("IsPreProcessorKeywordContext", c.IsPreProcessorKeywordContext);
		_Print("IsPrimaryFunctionExpressionContext", c.IsPrimaryFunctionExpressionContext);
		_Print("IsRightOfNameSeparator", c.IsRightOfNameSeparator);
		_Print("IsRightSideOfNumericType", c.IsRightSideOfNumericType);
		_Print("IsStatementAttributeContext", c.IsStatementAttributeContext());
		_Print("IsStatementContext", c.IsStatementContext);
		_Print("IsTypeArgumentOfConstraintContext", c.IsTypeArgumentOfConstraintContext);
		_Print("IsTypeAttributeContext", c.IsTypeAttributeContext(default));
		_Print("IsTypeContext", c.IsTypeContext);
		_Print("IsTypeDeclarationContext", c.IsTypeDeclarationContext());
		_Print("IsTypeOfExpressionContext", c.IsTypeOfExpressionContext);
		_Print("IsWithinAsyncMethod", c.IsWithinAsyncMethod);
		//_Print("", c.);
		//_Print("", c.);
		//_Print("", c.);
		//_Print("", c.);

		static void _Print(string s, bool value) {
			if (value) print.it($"<><c red>{s}<>");
			else print.it(s);
		}

		//return CiContextType.Namespace;
	}
#endif

	//unfinished. Also does not support namespaces.
	//public static CiContextType GetContextType(CompilationUnitSyntax t, int pos) {
	//	var members = t.Members;
	//	var ms = members.FullSpan;
	//	//foreach(var v in members) print.it(v.GetType().Name, v); return 0;
	//	//print.it(pos, ms);
	//	//CiUtil.HiliteRange(ms);
	//	if (ms == default) { //assume empty top-level statements
	//		var v = t.AttributeLists.FullSpan;
	//		if (v == default) {
	//			v = t.Usings.FullSpan;
	//			if (v == default) v = t.Externs.FullSpan;
	//		}
	//		if (pos >= v.End) return CiContextType.Method;
	//	} else if (pos < ms.Start) {
	//	} else if (pos >= members.Span.End) {
	//		if (members.Last() is GlobalStatementSyntax) return CiContextType.Method;
	//	} else {
	//		int i = members.IndexOf(o => o is not GlobalStatementSyntax);
	//		if (i < 0 || pos <= members[i].SpanStart) return CiContextType.Method;

	//		//now the difficult part
	//		ms = members[i].Span;
	//		print.it(pos, ms);
	//		CiUtil.HiliteRange(ms);
	//		//unfinished. Here should use CSharpSyntaxContext.
	//	}
	//	return CiContextType.Namespace;
	//}
}

//enum CiContextType
//{
//	/// <summary>
//	/// Outside class/method/topLevelStatements. Eg before using directives or at end of file.
//	/// Completion list must not include types.
//	/// </summary>
//	Namespace,

//	/// <summary>
//	/// Inside class but outside method.
//	/// Completion list can include types but not functions and values.
//	/// </summary>
//	Class,

//	/// <summary>
//	/// Inside method/topLevelStatements.
//	/// Completion list can include all symbols.
//	/// </summary>
//	Method
//}

static class CiExt
{
	[Conditional("DEBUG")]
	public static void DebugPrint(this CompletionItem t, string color = "blue") {
		print.it($"<><c {color}>{t.DisplayText},    {string.Join("|", t.Tags)},    prefix={t.DisplayTextPrefix},    suffix={t.DisplayTextSuffix},    filter={t.FilterText},    sort={t.SortText},    inline={t.InlineDescription},    automation={t.AutomationText},    provider={t.ProviderName}<>");
		print.it(string.Join("\n", t.Properties));
	}

	[Conditional("DEBUG")]
	public static void DebugPrintIf(this CompletionItem t, bool condition, string color = "blue") {
		if (condition) DebugPrint(t, color);
	}

	public static string QualifiedName(this ISymbol t, bool onlyNamespace = false, bool noDirectName = false) {
		var g = t_qnStack ??= new Stack<string>();
		g.Clear();
		if (noDirectName) t = t.ContainingType ?? t.ContainingNamespace as ISymbol;
		if (!onlyNamespace) for (var k = t; k != null; k = k.ContainingType) g.Push(k.Name);
		for (var n = t.ContainingNamespace; n != null && !n.IsGlobalNamespace; n = n.ContainingNamespace) g.Push(n.Name);
		return string.Join(".", g);
	}
	[ThreadStatic] static Stack<string> t_qnStack;

	/// <summary>
	/// Cached, thread-safe.
	/// </summary>
	public static string QualifiedNameCached(this INamespaceSymbol t) { //only 2 times faster than QualifiedName, but no garbage. Same speed with Dictionary.
																		//if (t.IsGlobalNamespace || t.ContainingNamespace.IsGlobalNamespace) return t.Name; //same speed. Few such namespaces.
		if (!_namespaceNames.TryGetValue(t, out var s)) {
			s = t.QualifiedName();
			_namespaceNames.AddOrUpdate(t, s); //OrUpdate for thread safety
											   //print.it(s);
		}
		return s;
	}
	static ConditionalWeakTable<INamespaceSymbol, string> _namespaceNames = new();

	/// <summary>
	/// <c>position &gt; t.Start &amp;&amp; position &lt; t.End;</c>
	/// </summary>
	public static bool ContainsInside(this TextSpan t, int position) => position > t.Start && position < t.End;
}
