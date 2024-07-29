extern alias CAW;

using System.Collections.Immutable;
using Au.Controls;
using Au.Compiler;
using EStyle = CiStyling.EStyle;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using CAW::Microsoft.CodeAnalysis.Classification;
using CAW::Microsoft.CodeAnalysis.Tags;
using CAW::Microsoft.CodeAnalysis.FindSymbols;

static class CiUtil {
	#region simple string util
	
	/// <summary>
	/// Returns <c>=> c is ' ' or '\t';</c>
	/// </summary>
	public static bool IsSpace(char c) => c is ' ' or '\t';
	
	/// <summary>
	/// Returns true if <i>pos</i> is in <i>code</i> and <c>code[pos] is ' ' or '\t'</c>.
	/// </summary>
	public static bool IsSpace(string code, int pos) => (uint)pos < code.Length && code[pos] is ' ' or '\t';
	
	/// <summary>
	/// Skips <c>' '</c> and <c>'\t'</c> characters after <i>pos</i>.
	/// </summary>
	public static int SkipSpace(string code, int pos) {
		while (IsSpace(code, pos)) pos++;
		return pos;
	}
	
	/// <summary>
	/// Skips <c>' '</c> and <c>'\t'</c> characters before <i>pos</i>.
	/// </summary>
	public static int SkipSpaceBack(string code, int pos) {
		while (IsSpace(code, pos - 1)) pos--;
		return pos;
	}
	
	/// <summary>
	/// Returns the start and end of the range consisting of <c>' '</c> and <c>'\t'</c> characters around <i>pos</i>.
	/// </summary>
	public static (int start, int end) SkipSpaceAround(string code, int pos) {
		int start = pos, end = pos;
		while (IsSpace(code, start - 1)) start--;
		while (IsSpace(code, end)) end++;
		return (start, end);
	}
	
	/// <summary>
	/// If <i>pos</i> is at the end of a line and not at the end of the string, returns the start of next line. Else returns <i>pos</i>.
	/// </summary>
	public static int SkipNewline(string code, int pos) {
		if (pos < code.Length && code[pos] == '\r') pos++;
		if (pos < code.Length && code[pos] == '\n') pos++;
		return pos;
	}
	
	/// <summary>
	/// If <i>pos</i> is at the start of a line, returns the end of previous line. Else returns <i>pos</i>.
	/// </summary>
	public static int SkipNewlineBack(string code, int pos) {
		if (pos > 0 && code[pos - 1] == '\n') pos--;
		if (pos > 0 && code[pos - 1] == '\r') pos--;
		return pos;
	}
	
	/// <summary>
	/// Skips <c>' '</c>, <c>'\t'</c>, <c>'\r'</c> and <c>'\n'</c> characters after <i>pos</i>.
	/// </summary>
	public static int SkipSpaceAndNewline(string code, int pos) {
		while (pos < code.Length && code[pos] is ' ' or '\t' or '\r' or '\n') pos++;
		return pos;
	}
	
	/// <summary>
	/// Skips <c>' '</c>, <c>'\t'</c>, <c>'\r'</c> and <c>'\n'</c> characters before <i>pos</i>.
	/// </summary>
	public static int SkipSpaceAndNewlineBack(string code, int pos) {
		while (pos > 0 && code[pos - 1] is ' ' or '\t' or '\r' or '\n') pos--;
		return pos;
	}
	
	/// <summary>
	/// Returns true if i is at a line start + any number of spaces and tabs.
	/// </summary>
	public static bool IsLineStart(RStr s, int i/*, out int startOfLine*/) {
		while (i > 0 && s[i - 1] is '\t' or ' ') i--;
		//startOfLine = j;
		return i == 0 || s[i - 1] == '\n';
	}
	
	/// <summary>
	/// Returns true if i is at a line start + any number of spaces and tabs.
	/// </summary>
	/// <param name="startOfLine">Receives i or the start of horizontal whitespace before i.</param>
	public static bool IsLineStart(RStr s, int i, out int startOfLine) {
		while (i > 0 && s[i - 1] is '\t' or ' ') i--;
		startOfLine = i;
		return i == 0 || s[i - 1] == '\n';
	}
	
	/// <summary>
	/// Creates string containing n tabs or n*4 spaces, depending on <b>App.Settings.ci_formatTabIndent</b>.
	/// See also <see cref="CiUtilExt.AppendIndent"/>.
	/// </summary>
	public static string CreateIndentString(int n) {
		if (n < 1) return "";
		if (App.Settings.ci_formatTabIndent) return new('\t', n);
		return new(' ', n * 4);
	}
	
	/// <summary>
	/// Returns string with same indent as of the document line from pos.
	/// The string must not contain multiline raw/verbatim strings; this func ignores it.
	/// </summary>
	public static string IndentStringForInsertSimple(string s, SciCode doc, int pos, bool indentFirstLine = false, int indentPlus = 0) {
		if (s.Contains('\n')) {
			int indent = doc.aaaLineIndentFromPos(true, pos) + indentPlus;
			//if (!App.Settings.ci_formatTabIndent) s = s.RxReplace(@"(?m)^\t+", m => CreateIndentString(m.Length)); //rejected. This could be useful for snippets. But then also need to apply App.Settings.ci_formatCompact=false, eg move braces to new lines, indent switch block. Then also need to do all it in code inserted by tools. Better let users format code afterwards.
			if (indent > 0) s = s.RxReplace(indentFirstLine ? @"(?m)^" : @"(?<=\n)", CreateIndentString(indent));
		}
		return s;
	}
	
	public static string GoogleURL(string query) => App.Settings.internetSearchUrl + System.Net.WebUtility.UrlEncode(query);
	
	#endregion
	
	#region syntax
	
	/// <summary>
	/// Returns true if <i>pos</i> is in trivia where normal tokens are not allowed. For example in comment, doccomment, directive, disabled text.
	/// </summary>
	public static bool IsPosInNonblankTrivia(SyntaxNode node, int pos, string code) {
		if (pos > 0) {
			bool minus1 = (pos == code.Length || SyntaxFacts.IsNewLine(code[pos])) && !SyntaxFacts.IsNewLine(code[pos - 1]); //end of text or non-empty line. At the left can be eg `//comment`.
			if (minus1) pos--;
			var trivia = node.FindTrivia(pos);
			var tk = trivia.Kind();
			if (!(tk is 0 or SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia)) {
				var ts = trivia.Span;
				//print.it($"{trivia.Kind()}, {pos}, {trivia.FullSpan}, {ts}, '{trivia}'");
				if (tk is SyntaxKind.DisabledTextTrivia or SyntaxKind.EndIfDirectiveTrivia or SyntaxKind.ElifDirectiveTrivia or SyntaxKind.ElseDirectiveTrivia or SyntaxKind.BadDirectiveTrivia) return true;
				if (minus1 && pos + 1 == ts.End && tk is SyntaxKind.MultiLineCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia) return false;
				if (pos > trivia.FullSpan.Start && pos < ts.End) return true;
			}
		}
		return false;
	}
	
	/// <summary>
	/// Returns true if <i>code</i> contains global statements or is empty or the first method of the first class is named "Main".
	/// </summary>
	public static bool IsScript(string code) {
		var cu = CreateSyntaxTree(code);
		var f = cu.Members.FirstOrDefault();
		if (f != null) {
			if (f is GlobalStatementSyntax) return true;
			if (f is BaseNamespaceDeclarationSyntax nd) f = nd.Members.FirstOrDefault();
			if (f is ClassDeclarationSyntax cd && cd.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.Text == "Main") return true;
		} else {
			var u = cu.Usings.FirstOrDefault();
			if (u != null && u.GlobalKeyword.RawKind != 0) return false; //global.cs?
			return !cu.AttributeLists.Any(); //AssemblyInfo.cs?
		}
		return false;
	}
	
	#endregion
	
	#region symbols
	
	public static (ISymbol symbol, CodeInfo.Context cd) GetSymbolFromPos(bool andZeroLength = false, bool preferVar = false) {
		if (!CodeInfo.GetContextAndDocument(out var cd)) return default;
		return (GetSymbolFromPos(cd, andZeroLength, preferVar), cd);
	}
	
	public static ISymbol GetSymbolFromPos(CodeInfo.Context cd, bool andZeroLength = false, bool preferVar = false) {
		if (andZeroLength && _TryGetAltSymbolFromPos(cd) is ISymbol s1) return s1;
		var sym = SymbolFinder.FindSymbolAtPositionAsync(cd.document, cd.pos).Result;
		if (sym is IMethodSymbol ims) sym = ims.PartialImplementationPart ?? sym;
		else if (preferVar && sym is INamedTypeSymbol) { //for 'this' and 'base' SymbolFinder gets INamedTypeSymbol
			int i1 = cd.pos, i2 = i1;
			while (i1 > 0 && SyntaxFacts.IsIdentifierPartCharacter(cd.code[i1 - 1])) i1--;
			while (i2 < cd.code.Length && SyntaxFacts.IsIdentifierPartCharacter(cd.code[i2])) i2++;
			if (cd.code.Eq(i1..i2, "this") || cd.code.Eq(i1..i2, "base")) {
				var node = cd.syntaxRoot.FindToken(cd.pos).Parent;
				var semo = cd.semanticModel;
				return semo.GetSymbolInfo(node).GetAnySymbol() ?? semo.GetDeclaredSymbolForNode(node);
			}
		}
		return sym;
	}
	
	static ISymbol _TryGetAltSymbolFromPos(CodeInfo.Context cd) {
		if (cd.code.Eq(cd.pos, '[')) { //indexer?
			var t = cd.syntaxRoot.FindToken(cd.pos, true);
			if (t.IsKind(SyntaxKind.OpenBracketToken) && t.Parent is BracketedArgumentListSyntax b && b.Parent is ElementAccessExpressionSyntax es) {
				return cd.semanticModel.GetSymbolInfo(es).GetAnySymbol();
			}
		}
		//rejected: in the same way get cast operator if pos is before '('. Not very useful.
		return null;
	}
	
	public static (ISymbol symbol, string keyword, HelpKind helpKind, SyntaxToken token) GetSymbolEtcFromPos(CodeInfo.Context cd, bool forHelp = false) {
		if (_TryGetAltSymbolFromPos(cd) is ISymbol s1) return (s1, null, default, default);
		
		int pos = cd.pos; if (pos > 0 && SyntaxFacts.IsIdentifierPartCharacter(cd.code[pos - 1])) pos--;
		if (!cd.syntaxRoot.FindTouchingToken(out var token, pos, findInsideTrivia: true)) return default;
		
		string word = cd.code[token.Span.ToRange()];
		
		var k = token.Kind();
		if (k == SyntaxKind.IdentifierToken) {
			switch (word) {
			case "var" when forHelp: //else get the inferred type
			case "dynamic":
			case "nameof":
			case "unmanaged": //tested cases
				return (null, word, HelpKind.ContextualKeyword, token);
			}
		} else if (token.Parent is ImplicitObjectCreationExpressionSyntax && (!forHelp || cd.pos == token.Span.End)) {
			//for 'new(' get the ctor or type
		} else if (k == SyntaxKind.BaseKeyword) {
			
		} else {
			//print.it(
			//	//token.IsKeyword(), //IsReservedKeyword||IsContextualKeyword, but not IsPreprocessorKeyword
			//	SyntaxFacts.IsReservedKeyword(k), //also true for eg #if
			//	SyntaxFacts.IsContextualKeyword(k)
			//	//SyntaxFacts.IsQueryContextualKeyword(k) //included in IsContextualKeyword
			//	//SyntaxFacts.IsAccessorDeclarationKeyword(k),
			//	//SyntaxFacts.IsPreprocessorKeyword(k), //true if #something or can be used in #something context. Also true for eg if without #.
			//	//SyntaxFacts.IsPreprocessorContextualKeyword(k) //badly named. True only if #something.
			//	);
			
			if (SyntaxFacts.IsReservedKeyword(k)) {
				bool pp = (word == "if" || word == "else") && token.GetPreviousToken().IsKind(SyntaxKind.HashToken);
				if (pp) word = "#" + word;
				return (null, word, pp ? HelpKind.PreprocKeyword : HelpKind.ReservedKeyword, token);
			}
			if (SyntaxFacts.IsContextualKeyword(k)) {
				return (null, word, SyntaxFacts.IsAttributeTargetSpecifier(k) ? HelpKind.AttributeTarget : HelpKind.ContextualKeyword, token);
			}
			if (SyntaxFacts.IsPreprocessorKeyword(k)) {
				//if(SyntaxFacts.IsPreprocessorContextualKeyword(k)) word = "#" + word; //better don't use this internal func
				if (token.GetPreviousToken().IsKind(SyntaxKind.HashToken)) word = "#" + word;
				return (null, word, HelpKind.PreprocKeyword, token);
			}
			if (token.Parent is BaseArgumentListSyntax bals) {
				if (!GetFunctionSymbolInfoFromArgumentList(bals, cd.semanticModel, out var si)) return default;
				return (si.GetAnySymbol(), null, default, token);
			}
			switch (token.IsInString(cd.pos, cd.code, out _)) {
			case true: return (null, null, HelpKind.String, token);
			case null: return default;
			}
		}
		//note: don't pass contextual keywords to FindSymbolAtPositionAsync or GetSymbolInfo.
		//	It may get info for something other, eg 'new' -> ctor or type, or 'int' -> type 'Int32'.
		
		return (GetSymbolFromPos(cd), null, default, token);
	}
	
	/// <summary>
	/// Gets SymbolInfo of invoked method, ctor or indexer from its argument list.
	/// </summary>
	public static bool GetFunctionSymbolInfoFromArgumentList(BaseArgumentListSyntax als, SemanticModel semo, out SymbolInfo si) {
		si = default;
		var pa = als.Parent;
		if (als is ArgumentListSyntax && pa is InvocationExpressionSyntax or ObjectCreationExpressionSyntax) {
			si = semo.GetSymbolInfo(pa);
		} else if (als is BracketedArgumentListSyntax && pa is ElementAccessExpressionSyntax eacc) {
			si = semo.GetSymbolInfo(eacc);
		} else return false;
		return !si.IsEmpty;
	}
	
	public enum HelpKind {
		None, ReservedKeyword, ContextualKeyword, AttributeTarget, PreprocKeyword, String
	}
	
	public static void OpenSymbolEtcFromPosHelp() {
		string url = null;
		if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		var (sym, keyword, helpKind, _) = GetSymbolEtcFromPos(cd, forHelp: true);
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
			url = GoogleURL(s);
		} else if (helpKind == HelpKind.String) {
			int i = popupMenu.showSimple("1 C# strings|2 String formatting|3 Wildcard expression|4 Output tags|11 Regex tool (Ctrl+Space)|12 Keys tool (Ctrl+Space)", PMFlags.ByCaret);
			switch (i) {
			case 1: url = "C# strings"; break;
			case 2: url = "C# string formatting"; break;
			case 3: HelpUtil.AuHelp("articles/Wildcard expression"); break;
			case 4: HelpUtil.AuHelp("articles/Output tags"); break;
			case 11: CiTools.CmdShowRegexWindow(); break;
			case 12: CiTools.CmdShowKeysWindow(); break;
			}
			if (url != null) url = GoogleURL(url);
		}
		if (url != null) run.itSafe(url);
	}
	
	/// <summary>
	/// From position in code gets ArgumentSyntax and its IParameterSymbol.
	/// </summary>
	/// <param name="arg">Not null if returns true and the argument list isn't empty.</param>
	/// <param name="ps">If returns true, the array contains 1 or more elements. Multiple if cannot resolve overload.</param>
	public static bool GetArgumentParameterFromPos(BaseArgumentListSyntax als, int pos, SemanticModel semo, out ArgumentSyntax arg, out IParameterSymbol[] ps) {
		arg = null; ps = null;
		var args = als.Arguments;
		var index = args.Count == 0 ? 0 : als.Arguments.IndexOf(o => pos <= o.FullSpan.End); //print.it(index);
		if (index < 0) return default;
		if (!CiUtil.GetFunctionSymbolInfoFromArgumentList(als, semo, out var si)) return default;
		string name = null;
		if (args.Count > 0) {
			arg = args[index];
			var nc = arg.NameColon;
			if (nc != null) name = nc.Name.Identifier.Text;
		}
		ps = GetParameterSymbol(si, index, name, als.Arguments.Count, o => o.Type.TypeKind == TypeKind.Delegate)
			.DistinctBy(o => o.Type.ToString()) //tested with Task.Run. 8 overloads, 4 distinct parameter types.
			.ToArray();
		return ps.Length > 0;
	}
	
	/// <summary>
	/// Gets IParameterSymbol of siFunction's parameter matching argument index or name.
	/// Can return multiple if cannot resolve overload.
	/// </summary>
	/// <param name="siFunction">SymbolInfo of the method, ctor or indexer.</param>
	/// <param name="index">Argument index. Not used if used name.</param>
	/// <param name="name">Parameter name, if specified in the argument, else null.</param>
	/// <param name="argCount">Count of arguments.</param>
	/// <param name="filter"></param>
	public static IEnumerable<IParameterSymbol> GetParameterSymbol(SymbolInfo siFunction, int index, string name, int argCount, Func<IParameterSymbol, bool> filter = null) {
		foreach (var v in siFunction.GetAllSymbols()) {
			if (_Get(v) is { } r) yield return r;
		}
		
		IParameterSymbol _Get(ISymbol fsym) {
			var parms = fsym switch { IMethodSymbol ms => ms.Parameters, IPropertySymbol ps => ps.Parameters, _ => default };
			if (!parms.IsDefaultOrEmpty && parms.Length >= argCount) {
				var ps = name != null ? parms.FirstOrDefault(o => o.Name == name) : index < parms.Length ? parms[index] : null;
				if (ps != null) if (filter == null || filter(ps)) return ps;
			}
			return null;
		}
	}
	
	/// <summary>
	/// Gets <b>ILocalSymbol</b> or <b>IParameterSymbol</b> of the nearest declared/accessible local variable or parameter of one of specified types.
	/// Uses current document and caret position.
	/// Returns null if not found.
	/// </summary>
	/// <param name="types">Fully qualified type name. The type must be in an assembly, not in source.</param>
	/// <exception cref="ArgumentException">Type not found.</exception>
	public static ISymbol GetNearestLocalVariableOfType(params string[] types) {
		if (!CodeInfo.GetContextAndDocument(out var cd)) return null;
		var semo = cd.semanticModel;
		var ats = types.Select(o => semo.Compilation.GetTypeByMetadataName(o) ?? throw new ArgumentException($"Type not found: {o}."));
		var a = GetLocalVariablesAt(semo, cd.pos, o => ats.Contains(o));
		return a.Count > 0 ? a[^1] : null;
	}
	
	/// <summary>
	/// Gets <b>ILocalSymbol</b> or <b>IParameterSymbol</b> of local variables and parameters that can be used at position <i>pos</i>. The order is the same as declared in code.
	/// Not perfect.
	/// </summary>
	public static List<ISymbol> GetLocalVariablesAt(SemanticModel semo, int pos, Func<ITypeSymbol, bool> ofType = null) {
		var a = new List<ISymbol>();
		var scopes = _GetLocalScopes(semo, pos);
		if (scopes.Count > 0) {
			var e = semo.GetAllDeclaredSymbols(scopes[^1], default);
			//var e=semo.GetAllDeclaredSymbols(scopes[^1], default, o => { CiUtil.PrintNode(o); return true; });
			foreach (var v in e) {
				//if (v is not (ILocalSymbol or IParameterSymbol)) print.it(v.Name, v.GetTypeDisplayName());
				if (v is not (ILocalSymbol or IParameterSymbol)) continue;
				if (ofType != null && !ofType(v.GetSymbolType())) continue;
				var n2 = v.DeclaringSyntaxReferences.First().GetSyntax();
				var s2 = _GetLocalScope(n2);
				if (!scopes.Contains(s2)) {
					//print.it($"<>    <c #ff8080>{v.Name}, {v.GetSymbolType()}<>");
					continue;
				}
				var span = n2 is ForEachStatementSyntax fe ? fe.Identifier.Span : n2.Span;
				if (pos <= span.End) {
					//print.it($"<>    <c #c0c0c0>{v.Name}, {v.GetSymbolType()}<>");
					continue;
				}
				//print.it(v.Name, v.GetSymbolType());
				a.Add(v);
			}
		}
		return a;
	}
	
	/// <summary>
	/// Gets ancestor scopes of local variables and parameters.
	/// For { block } gets its parent if there may be declared variables/parameters that are visible only in that block; eg function declaration or foreach statement.
	/// </summary>
	/// <param name="semo"></param>
	/// <param name="pos"></param>
	static List<SyntaxNode> _GetLocalScopes(SemanticModel semo, int pos) {
		var a = new List<SyntaxNode>();
		var node = semo.SyntaxTree.GetCompilationUnitRoot().FindToken(pos).Parent;
		bool inside = false;
		foreach (var v in node.AncestorsAndSelf()) {
			if (v is CompilationUnitSyntax) {
				a.Add(v);
				break;
			}
			if (!(inside || (inside = v.Span.ContainsInside(pos)))) continue;
			if (_IsLocalScope(v)) {
				a.Add(v);
				if (v is BaseMethodDeclarationSyntax) break;
				if (v is LocalFunctionStatementSyntax lf && lf.Modifiers.Any(SyntaxKind.StaticKeyword)) break;
				if (v is AnonymousFunctionExpressionSyntax af && af.Modifiers.Any(SyntaxKind.StaticKeyword)) break;
			} else if (v is BaseTypeDeclarationSyntax or NamespaceDeclarationSyntax) {
				a.Clear();
				break;
			}
		}
		return a;
	}
	
	/// <summary>
	/// Returns true if local/parameter variables declared inside n aren't visible outside.
	/// </summary>
	/// <param name="n"></param>
	static bool _IsLocalScope(SyntaxNode n) {
		if (n is BlockSyntax) return !_Is2(n.Parent);
		return _Is2(n) || n is SwitchSectionSyntax or CompilationUnitSyntax;
		
		static bool _Is2(SyntaxNode n) => n is BaseMethodDeclarationSyntax
			or LocalFunctionStatementSyntax
			or AnonymousFunctionExpressionSyntax
			or ForStatementSyntax
			or CommonForEachStatementSyntax
			or CatchClauseSyntax
			or FixedStatementSyntax
			or UsingStatementSyntax
			or SwitchExpressionArmSyntax
		;
	}
	
	static SyntaxNode _GetLocalScope(SyntaxNode node) => node.FirstAncestorOrSelf<SyntaxNode>(o => _IsLocalScope(o));
	
	public static string GetSymbolHelpUrl(ISymbol sym) {
		//print.it(sym);
		//print.it(sym.IsInSource(), sym.IsFromSource());
		if (sym is IParameterSymbol or ITypeParameterSymbol) return null;
		string query;
		IModuleSymbol metadata = null;
		foreach (var loc in sym.Locations) {
			if ((metadata = loc.MetadataModule) != null) break;
		}
		
		bool au = metadata?.Name == "Au.dll";
		if (au && !sym.HasPublicResultantVisibility()) metadata = null; //no online doc for other than public, protected, protected internal. But the google search is useful eg if it's an Api class member visible because of meta testInternal.
		
		if (metadata != null) {
			if (au && sym.IsEnumMember()) sym = sym.ContainingType;
			//print.it(sym, sym.GetType(), sym.GetType().GetInterfaces());
			if (sym is INamedTypeSymbol nt && nt.IsGenericType) {
				var qn = sym.QualifiedName(noDirectName: true);
				if (au) query = qn + "." + sym.MetadataName.Replace('`', '-');
				else query = $"{qn}.{sym.Name}<{string.Join(", ", nt.TypeParameters)}>";
			} else {
				query = sym.QualifiedName();
			}
			
			if (query.Ends("..ctor")) query = query.ReplaceAt(^6.., au ? ".-ctor" : " constructor");
			else if (query.Ends(".this[]")) query = query.ReplaceAt(^7.., ".Item");
			
			if (au) return HelpUtil.AuHelpUrl(query);
			if (metadata.Name.Starts("Au.")) return null;
			
			string kind = (sym is INamedTypeSymbol ints) ? ints.TypeKind.ToString() : sym.Kind.ToString();
			query = query + " " + kind.Lower();
		} else if (!sym.IsInSource() && !au) { //eg an operator of string etc
			if (!(sym is IMethodSymbol me && me.MethodKind == MethodKind.BuiltinOperator)) return null;
			//print.it(sym, sym.Kind, sym.QualifiedName());
			//query = "C# " + sym.ToString(); //eg "string.operator +(string, string)", and Google finds just Equality
			//query = "C# " + sym.QualifiedName(); //eg "System.String.op_Addition", and Google finds nothing
			query = "C# " + sym.ToString().RxReplace(@"\(.+\)$", "", 1).Replace('.', ' '); //eg C# string operator +, not bad
		} else if (sym.IsExtern) { //[DllImport]
			query = sym.Name + " function";
		} else if (sym is INamedTypeSymbol nt1 && nt1.IsComImport) { //[ComImport] interface or coclass
			query = sym.Name + " " + nt1.TypeKind.ToString().Lower();
		} else if (sym.ContainingType?.IsComImport == true) { //[ComImport] interface method
			query = sym.ContainingType.Name + "." + sym.Name;
		} else if (_IsNativeApiClass(sym.ContainingType)) {
			if (sym is INamedTypeSymbol nt2) query = sym.Name + " " + (nt2.TypeKind switch { TypeKind.Struct => "structure", TypeKind.Enum => "enumeration", TypeKind.Delegate => "callback function", _ => null });
			else query = sym.Name; //constant or Guid/etc
		} else if (sym is IFieldSymbol && _IsNativeApiClass(sym.ContainingType.ContainingType)) {
			query = sym.ContainingType.Name + "." + sym.Name; //struct field or enum member
		} else {
			return null;
		}
		
		static bool _IsNativeApiClass(INamedTypeSymbol t)
			=> t?.TypeKind is TypeKind.Class or TypeKind.Struct
			&& (t.BaseType?.Name == "NativeApi" || t.Name.Contains("Native") || t.Name.Ends("Api"));
		
		return GoogleURL(query);
	}
	
	public static PSFormat GetParameterStringFormat(SyntaxNode node, SemanticModel semo, bool isString, bool ignoreInterpolatedString = false) {
		var kind = node.Kind();
		//print.it(kind);
		SyntaxNode parent;
		if (isString || kind == SyntaxKind.StringLiteralExpression) parent = node.Parent;
		else if (kind == SyntaxKind.InterpolatedStringText && !ignoreInterpolatedString) parent = node.Parent.Parent;
		else return PSFormat.None;
		
		while (parent is BinaryExpressionSyntax && parent.IsKind(SyntaxKind.AddExpression)) parent = parent.Parent; //"string"+"string"+...
		
		PSFormat format = PSFormat.None;
		if (parent is ArgumentSyntax asy) {
			if (parent.Parent is ArgumentListSyntax alis) {
				if (alis.Parent is ExpressionSyntax es && es is BaseObjectCreationExpressionSyntax or InvocationExpressionSyntax) {
					var si = semo.GetSymbolInfo(es);
					if (si.Symbol is IMethodSymbol m) {
						format = _GetFormat(m, alis);
					} else if (!si.CandidateSymbols.IsDefaultOrEmpty) {
						foreach (var v in si.CandidateSymbols.OfType<IMethodSymbol>()) {
							if ((format = _GetFormat(v, alis)) != 0) break;
						}
					}
				}
			} else if (parent.Parent is BracketedArgumentListSyntax balis && balis.Parent is ElementAccessExpressionSyntax eacc) {
				if (semo.GetSymbolInfo(eacc).Symbol is IPropertySymbol ips && ips.IsIndexer) {
					var ims = ips.SetMethod;
					if (ims != null) format = _GetFormat(ims, balis);
				}
			}
			
			PSFormat _GetFormat(IMethodSymbol ims, BaseArgumentListSyntax alis) {
				IParameterSymbol p = null;
				var pa = ims.Parameters;
				if (pa.Length > 0) {
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
						foreach (var v in p.GetAttributes()) {
							switch (v.AttributeClass.Name) {
							case nameof(ParamStringAttribute): return v.GetConstructorArgument<PSFormat>(0, SpecialType.None);
							case nameof(System.Diagnostics.CodeAnalysis.StringSyntaxAttribute) when v.GetConstructorArgument<string>(0, SpecialType.System_String) == System.Diagnostics.CodeAnalysis.StringSyntaxAttribute.Regex: return PSFormat.NetRegex; //note: the attribute also can be set on properties and fields. But Regex doesn't have.
							}
						}
					}
				}
				return PSFormat.None;
			}
		}
		return format;
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
	/// Calls Classifier.GetClassifiedSpansAsync and corrects overlapped items etc.
	/// </summary>
	public static async Task<List<ClassifiedSpan>> GetClassifiedSpansAsync(Document document, int from, int to, CancellationToken cancellationToken = default) {
		var e = await Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(from, to)).ConfigureAwait(false);
		return _CorrectClassifiedSpans(e);
	}
	
	/// <summary>
	/// Calls Classifier.GetClassifiedSpans and corrects overlapped items etc.
	/// </summary>
	public static List<ClassifiedSpan> GetClassifiedSpans(SemanticModel semo, Document document, int from, int to, CancellationToken cancellationToken = default) {
		var proj = document.Project;
		var opt = new ClassificationOptions { ColorizeRegexPatterns = false, ColorizeJsonPatterns = true }; //default true true, but don't work; turn off regex anyway.
		var e = Classifier.GetClassifiedSpans(proj.Solution.Services, proj, semo, TextSpan.FromBounds(from, to), opt, true, cancellationToken);
		return _CorrectClassifiedSpans(e);
	}
	
	static List<ClassifiedSpan> _CorrectClassifiedSpans(IEnumerable<ClassifiedSpan> espans) {
		var a = espans as List<ClassifiedSpan>;
		//print.clear(); foreach (var v in a) print.it(v.ClassificationType);
		
		//Order StringEscapeCharacter correctly. Now in $"string" they are randomly after or before the string. Must be after.
		//	This code ignores regex and json tokens, because Classifier.GetClassifiedSpans does not produce them (why?). And callers don't support it too.
		for (int k = a.Count; --k > 0;) {
			if (a[k - 1].ClassificationType == ClassificationTypeNames.StringEscapeCharacter)
				//if (a[k].ClassificationType == ClassificationTypeNames.StringLiteral || a[k].ClassificationType == ClassificationTypeNames.VerbatimStringLiteral)
				if (a[k].TextSpan.Contains(a[k - 1].TextSpan))
					Math2.Swap(ref a.Ref(k), ref a.Ref(k - 1));
		}
		
		//remove StaticSymbol
		int i = 0, j = 0;
		for (; i < a.Count; i++) {
			if (a[i].ClassificationType == ClassificationTypeNames.StaticSymbol) continue;
			if (j != i) a[j] = a[i];
			j++;
		}
		if ((i -= j) > 0) a.RemoveRange(a.Count - i, i);
		
		return a;
	}
	
	/// <summary>
	/// For C# code gets style bytes that can be used with SCI_SETSTYLINGEX for UTF-8 text.
	/// Uses Classifier.GetClassifiedSpansAsync, like the code editor.
	/// Controls that use this should set styles like this example, probably when handle created:
	/// <c>var styles = new CiStyling.TTheme { FontSize = 9 };
	/// styles.ToScintilla(this);</c>
	/// </summary>
	public static byte[] GetScintillaStylingBytes(string code) {
		var styles8 = new byte[Encoding.UTF8.GetByteCount(code)];
		var map8 = styles8.Length == code.Length ? null : Convert2.Utf8EncodeAndGetOffsets_(code).offsets;
		using var ws = new AdhocWorkspace();
		var document = CreateDocumentFromCode(ws, code, needSemantic: true);
		var semo = document.GetSemanticModelAsync().Result;
		var a = GetClassifiedSpansAsync(document, 0, code.Length).Result;
		foreach (var v in a) {
			//print.it(v.TextSpan, v.ClassificationType, code[v.TextSpan.Start..v.TextSpan.End]);
			EStyle style = CiStyling.StyleFromClassifiedSpan(v, semo);
			if (style == EStyle.None) continue;
			var (i, end) = v.TextSpan;
			if (map8 != null) { i = map8[i]; end = map8[end]; }
			while (i < end) styles8[i++] = (byte)style;
		}
		return styles8;
	}
	
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
	
	/// <summary>
	/// Calls <b>string.Compare</b>. Moves items starting with an ASCII non-symbol char to the bottom.
	/// </summary>
	public class CompletionListSortComparer : IComparer<string> {
		public int Compare(string x, string y) {
			int r = _IsAsciiNonSymChar(x) - _IsAsciiNonSymChar(y);
			if (r == 0) r = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
			return r;
			
			static int _IsAsciiNonSymChar(string s) => s.Length > 0 && s[0] is char c && (char.IsAsciiLetterOrDigit(c) || c == '_' || c > 127) ? 0 : 1;
		}
	}
	public static readonly CompletionListSortComparer SortComparer = new();
	
	#endregion
	
	#region create a temp syntax tree, document, compilation, solution
	
	/// <summary>
	/// From C# code creates a Roslyn workspace+project+document for code analysis.
	/// If <i>needSemantic</i>, adds default references and a document with default global usings (same as in default global.cs).
	/// </summary>
	/// <param name="ws"><c>using var ws = new AdhocWorkspace(); //need to dispose</c></param>
	/// <param name="code">Any C# code fragment, valid or not.</param>
	/// <param name="needSemantic">Add default references (.NET and Au.dll) and global usings.</param>
	public static Document CreateDocumentFromCode(AdhocWorkspace ws, string code, bool needSemantic) {
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId documentId = DocumentId.CreateNewId(projectId);
		var pi = ProjectInfo.Create(projectId, VersionStamp.Default, "l", "l", LanguageNames.CSharp, null, null,
			new CSharpCompilationOptions(OutputKind.WindowsApplication, allowUnsafe: true),
			new CSharpParseOptions(LanguageVersion.Preview),
			metadataReferences: needSemantic ? new MetaReferences().Refs : null //tested: does not make slower etc
			);
		var sol = ws.CurrentSolution.AddProject(pi);
		if (needSemantic) {
			sol = sol.AddDocument(DocumentId.CreateNewId(projectId), "g.cs", c_globalUsingsText);
		}
		return sol.AddDocument(documentId, "l.cs", code).GetDocument(documentId);
		
		//It seems it's important to dispose workspaces.
		//	In the docs project at first didn't dispose. After maybe 300_000 times: much slower, process memory 3 GB, sometimes hangs.
	}
	
	public const string c_globalUsingsText = """
global using Au;
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
global using Au.Triggers;
global using System.Windows;
global using System.Windows.Controls;
global using System.Windows.Media;
""";
	
	/// <summary>
	/// Creates Compilation from a file or project folder.
	/// Supports meta etc, like the compiler. Does not support test script, meta testInternal, project references.
	/// </summary>
	/// <param name="f">A code file or a project folder. If in a project folder, creates from the project.</param>
	/// <returns>null if can't create, for example if f isn't a code file or if meta contains errors.</returns>
	public static Compilation CreateCompilationFromFileNode(FileNode f) { //not CSharpCompilation, it creates various small problems
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		if (!f.IsCodeFile) return null;
		
		var m = new MetaComments(MCFlags.ForCodeInfo);
		if (!m.Parse(f, projFolder)) return null; //with this flag never returns false, but anyway
		
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
	/// <param name="ws"><c>using var ws = new AdhocWorkspace(); //need to dispose</c></param>
	/// <param name="f">A code file or a project folder. If in a project folder, creates from the project.</param>
	/// <returns>null if can't create, for example if f isn't a code file or if meta contains errors.</returns>
	public static (Solution sln, MetaComments meta) CreateSolutionFromFileNode(AdhocWorkspace ws, FileNode f) {
		if (f.FindProject(out var projFolder, out var projMain)) f = projMain;
		if (!f.IsCodeFile) return default;
		
		var m = new MetaComments(MCFlags.ForCodeInfo);
		if (!m.Parse(f, projFolder)) return default; //with this flag never returns false, but anyway
		
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
		
		return (ws.CurrentSolution.AddProject(pi), m);
	}
	
	/// <summary>
	/// Calls <b>CSharpSyntaxTree.ParseText</b> and returns <b>CompilationUnitSyntax</b>.
	/// </summary>
	public static CompilationUnitSyntax CreateSyntaxTree(string code) {
		return CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Preview)).GetCompilationUnitRoot();
	}
	
	#endregion
	
	#region DEBUG
	
#if DEBUG
	public static void PrintNode(SyntaxNode x, int pos = 0, bool printNode = true, bool printErrors = false, bool indent = false) {
		if (x == null) { print.it("null"); return; }
		if (printNode) {
			string si = null;
			if (indent) {
				int i = x.Ancestors().Count();
				if (--i > 0) si = new('\t', i);
			}
			print.it($"<>{si}<c blue>{pos}, {x.Span}, {x.FullSpan}, k={x.Kind()}, t={x.GetType().Name},<> '<c green><\a>{(x is CompilationUnitSyntax ? null : x.ToString().Limit(10, middle: true, lines: true))}</\a><>'");
		}
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}
	
	public static void PrintNode(SyntaxToken x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, {x.Kind()},<> '<c green><\a>{x.ToString().Limit(10, middle: true, lines: true)}</\a><>'");
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}
	
	public static void PrintNode(SyntaxTrivia x, int pos = 0, bool printNode = true, bool printErrors = false) {
		if (printNode) print.it($"<><c blue>{pos}, {x.Span}, {x.Kind()},<> '<c green><\a>{x.ToString().Limit(10, middle: true, lines: true)}</\a><>'");
		if (printErrors) foreach (var d in x.GetDiagnostics()) print.it(d.Code, d.Location.SourceSpan, d);
	}
	
	public static void DebugHiliteRange(int start, int end, int indic = SciCode.c_indicTestBox) {
		var doc = Panels.Editor.ActiveDoc;
		doc.aaaIndicatorClear(indic);
		if (end > start) doc.aaaIndicatorAdd(indic, true, start..end);
	}
	
	public static void DebugHiliteRange(TextSpan span, int indic = SciCode.c_indicTestBox) => DebugHiliteRange(span.Start, span.End, indic);
	
	public static void DebugHiliteRanges(List<Range> a, int indic = SciCode.c_indicTestBox) {
		var doc = Panels.Editor.ActiveDoc;
		doc.aaaIndicatorClear(indic);
		int i = 0;
		foreach (var v in a) doc.aaaIndicatorAdd(indic, true, v, ++i);
	}
	
	static IEnumerable<string> DebugGetSymbolInterfaces(ISymbol sym) {
		return sym.GetType().FindInterfaces((t, _) => t.Name.Ends("Symbol") && t.Name != "ISymbol", null).Select(o => o.Name);
	}
	
	//unfinished. Just prints what we can get from CSharpSyntaxContext.
	public static /*CiContextType*/void DebugGetContextType(/*in CodeInfo.Context cd,*/ CSharpSyntaxContext c) {
		//print.it("--------");
		print.clear();
		//print.it(cd.pos);
		_Print("IsInNonUserCode", c.IsInNonUserCode);
		_Print("IsGlobalStatementContext", c.IsGlobalStatementContext);
		_Print("IsAnyExpressionContext", c.IsAnyExpressionContext);
		//_Print("IsAtStartOfPattern", c.IsAtStartOfPattern);
		//_Print("IsAtEndOfPattern", c.IsAtEndOfPattern);
		_Print("IsAttributeNameContext", c.IsAttributeNameContext);
		//_Print("IsCatchFilterContext", c.IsCatchFilterContext);
		_Print("IsConstantExpressionContext", c.IsConstantExpressionContext);
		_Print("IsCrefContext", c.IsCrefContext);
		//_Print("IsDeclarationExpressionContext", c.IsDeclarationExpressionContext); //removed from Roslyn
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
	
	//unfinished. Also does not support namespaces.
	//public static CiContextType DebugGetContextType(CompilationUnitSyntax t, int pos) {
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
	
#endif
	
	#endregion
}
