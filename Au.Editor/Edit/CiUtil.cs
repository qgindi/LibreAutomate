using System.Linq;
using Au.Controls;
using Au.Compiler;
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
using Microsoft.CodeAnalysis.Classification;
using EToken = CiStyling.EToken;

static class CiUtil {
	//not used
	//public static bool GetSymbolFromPos(out ISymbol sym, out CodeInfo.Context cd) {
	//	(sym, _, _, _) = GetSymbolEtcFromPos(out cd);
	//	return sym != null;
	//}

	public static (ISymbol symbol, string keyword, HelpKind kind, SyntaxToken token) GetSymbolEtcFromPos(out CodeInfo.Context cd, bool metaToo = false) {
		var doc = Panels.Editor.ZActiveDoc; if (doc == null) { cd = default; return default; }
		if (!CodeInfo.GetContextAndDocument(out cd, metaToo: metaToo)) return default;
		return GetSymbolOrKeywordFromPos(cd.document, cd.pos16, cd.code);
	}

	public static (ISymbol symbol, string keyword, HelpKind helpKind, SyntaxToken token) GetSymbolOrKeywordFromPos(Document document, int position, string code) {
		//using var p1 = perf.local();

		//CONSIDER: try SymbolFinder. Same speed.
		//var r = SymbolFinder.FindSymbolAtPositionAsync(document, position).Result;
		//print.it(r);

		if (position > 0 && SyntaxFacts.IsIdentifierPartCharacter(code[position - 1]) && !code.Eq(position, '[')) position--;

		var root = document.GetSyntaxRootAsync().Result;
		if (!root.FindTouchingToken(out var token, position, findInsideTrivia: true)) return default;

		var span = token.Span; string word = code[span.Start..span.End];
		//PrintNode(token);

		SyntaxNode node = token.Parent;
		var tkind = token.Kind();
		if (tkind == SyntaxKind.IdentifierToken) {
			switch (word) {
			case "var":
			case "dynamic":
			case "nameof":
			case "unmanaged": //tested cases
				return (null, word, HelpKind.ContextualKeyword, token);
			}
		} else if (tkind == SyntaxKind.OpenBracketToken) { //indexer?
			if (token.Parent is BracketedArgumentListSyntax bal && bal.Parent is ElementAccessExpressionSyntax es) node = es;
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
			switch (token.IsInString(position, code, out _)) {
			case true: return (null, null, HelpKind.String, token);
			case null: return default;
			}
		}
		//note: don't pass contextual keywords to GetSymbolInfo. It may get info for something other, eg for 'new' gets the ctor method.

		ISymbol symbol = null;
		var model = document.GetSemanticModelAsync().Result;
		//p1.Next();
		bool preferGeneric = tkind == SyntaxKind.IdentifierToken && token.GetNextToken().IsKind(SyntaxKind.GreaterThanToken);

		var sa = model.GetSymbolInfo(node).GetAllSymbols();
		if (!sa.IsDefault) {
			foreach (var v in sa) {
				Debug_.PrintIf(v is IErrorTypeSymbol);
				bool gen = v is INamedTypeSymbol { IsGenericType: true } or IMethodSymbol { IsGenericMethod: true };
				//print.it(v, gen, v.Kind);
				if (gen == preferGeneric) { symbol = v; break; }
				symbol ??= v;
			}
		}

		return (symbol, null, default, token);
	}

	public enum HelpKind {
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

			if (query.Ends("..ctor")) query = query.ReplaceAt(^6.., au ? ".-ctor" : " constructor");
			else if (query.Ends(".this[]")) query = query.ReplaceAt(^7.., ".Item");

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
		if (parent is ArgumentSyntax asy) {
			if (parent.Parent is ArgumentListSyntax alis) {
				switch (alis.Parent) {
				case ObjectCreationExpressionSyntax oce:
					format = _GetFormat(oce, alis);
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
					format = _GetFormat(ies, alis);
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
			} else if (parent.Parent is BracketedArgumentListSyntax balis && balis.Parent is ElementAccessExpressionSyntax eacc) {
				if (semo.GetSymbolInfo(eacc).Symbol is IPropertySymbol ips && ips.IsIndexer) {
					var ims = ips.SetMethod;
					if (ims != null) format = _GetFormat2(ims, balis);
				}
			}

			PSFormat _GetFormat(ExpressionSyntax es, BaseArgumentListSyntax alis) {
				if (semo.GetSymbolInfo(es).Symbol is IMethodSymbol ims) return _GetFormat2(ims, alis);
				return PSFormat.None;
			}

			PSFormat _GetFormat2(IMethodSymbol ims, BaseArgumentListSyntax alis) {
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
				return PSFormat.None;
			}
		}
		return format;
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
	/// From C# code creates a Roslyn workspace+project+document for code analysis.
	/// If <i>needSemantic</i>, adds default references and a document with default global usings (same as in default global.cs).
	/// </summary>
	public static Document CreateDocumentFromCode(string code, bool needSemantic) {
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

	/// <summary>
	/// Creates Compilation from a file or project folder.
	/// Supports meta etc, like the compiler. Does not support test script, meta testInternal, project references.
	/// </summary>
	/// <param name="f">A code file or a project folder. If in a project folder, creates from the project.</param>
	/// <returns>null if can't create, for example if f isn't a code file or if meta contains errors.</returns>
	public static Compilation CreateCompilationFromFileNode(FileNode f) { //not CSharpCompilation, it creates various small problems
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		if (!f.IsCodeFile) return null;

		var m = new MetaComments();
		if (!m.Parse(f, projFolder, EMPFlags.ForCodeInfo)) return null; //with this flag never returns false, but anyway

		var pOpt = m.CreateParseOptions();
		var trees = new CSharpSyntaxTree[m.CodeFiles.Count];
		for (int i = 0; i < trees.Length; i++) {
			var f1 = m.CodeFiles[i];
			trees[i] = CSharpSyntaxTree.ParseText(f1.code, pOpt, f1.f.FilePath, Encoding.Default) as CSharpSyntaxTree;
		}

		var cOpt = m.CreateCompilationOptions();
		return CSharpCompilation.Create("Compilation", trees, m.References.Refs, cOpt);
	} //FUTURE: remove if unused

	/// <summary>
	/// Creates Solution from a file or project folder.
	/// Supports meta etc, like the compiler. Does not support test script, meta testInternal, project references.
	/// </summary>
	/// <param name="f">A code file or a project folder. If in a project folder, creates from the project.</param>
	/// <returns>null if can't create, for example if f isn't a code file or if meta contains errors.</returns>
	public static (Solution sln, MetaComments meta) CreateSolutionFromFileNode(FileNode f) {
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		if (!f.IsCodeFile) return default;

		var m = new MetaComments();
		if (!m.Parse(f, projFolder, EMPFlags.ForCodeInfo)) return default; //with this flag never returns false, but anyway

		var projectId = ProjectId.CreateNewId();
		var adi = new List<DocumentInfo>();
		foreach (var f1 in m.CodeFiles) {
			var docId = DocumentId.CreateNewId(projectId);
			var tav = TextAndVersion.Create(SourceText.From(f1.code, Encoding.UTF8), VersionStamp.Default, f1.f.FilePath);
			adi.Add(DocumentInfo.Create(docId, f1.f.Name, null, SourceCodeKind.Regular, TextLoader.From(tav), f1.f.ItemPath));
		}

		var pi = ProjectInfo.Create(projectId, VersionStamp.Default, f.Name, f.Name, LanguageNames.CSharp, null, null,
			m.CreateCompilationOptions(),
			m.CreateParseOptions(),
			adi,
			null,
			m.References.Refs);

		var ws = new AdhocWorkspace();
		return (ws.CurrentSolution.AddProject(pi), m);
	}

	/// <summary>
	/// For C# code gets style bytes that can be used with SCI_SETSTYLINGEX for UTF-8 text.
	/// Uses Classifier.GetClassifiedSpansAsync, like the code editor.
	/// Controls that use this should set styles like this example, probably when handle created:
	/// var styles = new CiStyling.TStyles { FontSize = 9 };
	/// styles.ToScintilla(this);
	/// </summary>
	public static byte[] GetScintillaStylingBytes(string code) {
		var styles8 = new byte[Encoding.UTF8.GetByteCount(code)];
		var map8 = Convert2.MapUtf8Offsets_(code);
		var document = CreateDocumentFromCode(code, needSemantic: true);
		var semo = document.GetSemanticModelAsync().Result;
		var a = Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(0, code.Length)).Result;
		foreach (var v in a) {
			var ct = v.ClassificationType; if (ct == ClassificationTypeNames.StaticSymbol) continue; /*duplicate*/
			//print.it(v.TextSpan, ct, code[v.TextSpan.Start..v.TextSpan.End]);
			EToken style = CiStyling.StyleFromClassifiedSpan(v, semo);
			if (style == EToken.None) continue;
			int i = v.TextSpan.Start, end = v.TextSpan.End; if (map8 != null) { i = map8[i]; end = map8[end]; }
			for (; i < end; i++) styles8[i] = (byte)style;
		}
		return styles8;
	}

	/// <summary>
	/// Returns true if <i>code</i> contains global statements or is empty or the first method of the first class is named "Main".
	/// </summary>
	public static bool IsScript(string code) {
		var cu = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview), "", Encoding.UTF8).GetCompilationUnitRoot();
		var f = cu.Members.FirstOrDefault();
		if (f is GlobalStatementSyntax or null) return true;
		if (f is BaseNamespaceDeclarationSyntax nd) f = nd.Members.FirstOrDefault();
		if (f is ClassDeclarationSyntax cd && cd.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text == "Main") return true;
		return false;
	}

#if DEBUG
	public static void PrintNode(SyntaxNode x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (x == null) { print.it("null"); return; }
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, {x.FullSpan}, k={x.Kind()}, t={x.GetType().Name},<> '<c green>{(x is CompilationUnitSyntax ? null : x.ToString().Limit(10, middle: true, lines: true))}<>'");
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}

	public static void PrintNode(SyntaxToken x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, {x.Kind()},<> '<c green>{x.ToString().Limit(10, middle: true, lines: true)}<>'");
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}

	public static void PrintNode(SyntaxTrivia x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, {x.Kind()},<> '<c green>{x.ToString().Limit(10, middle: true, lines: true)}<>'");
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
