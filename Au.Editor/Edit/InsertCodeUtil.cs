using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Util used by <see cref="InsertCode"/>. Also can be used everywhere.
/// </summary>
static class InsertCodeUtil {
	/// <summary>
	/// Returns true if pos in string is at a line start + any number of spaces and tabs.
	/// </summary>
	public static bool IsLineStart(string s, int pos) {
		int i = pos; while (--i >= 0 && (s[i] == ' ' || s[i] == '\t')) { }
		return i < 0 || s[i] == '\n';
	}

	/// <summary>
	/// Returns string with same indentation as of the document line from pos.
	/// Does not add indentation to the first line.
	/// The string must not contain multiline raw/verbatim strings; this func ignores it.
	/// </summary>
	public static string IndentStringForInsertSimple(string s, SciCode doc, int pos) {
		if (s.Contains('\n')) {
			int indent = doc.zLineIndentationFromPos(true, pos);
			if (indent > 0) s = s.RxReplace(@"(?<=\n)", new string('\t', indent));
		}
		return s;
	}

	/// <summary>
	/// Appends C# code <i>s</i> to <i>b</i>.
	/// For each line adds <i>indent</i> tabs, except in multiline @"string" or """string""".
	/// Ignores the last empty line of <i>s</i>. Appends newline at the end if <b>andNewline</b>.
	/// </summary>
	public static void AppendCodeWithIndent(StringBuilder b, string s, int indent, bool andNewline) {
		if (indent > 0) {
			var cu = CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Preview)).GetCompilationUnitRoot();
			var a = s.Lines(..); int i = 0;
			foreach (var v in a) {
				bool canIndent = true;
				if (s[v.start] == '#' && cu.FindTrivia(v.start).IsDirective) canIndent = false;
				else {
					var tok = cu.FindToken(v.start);
					canIndent = tok.IsInString(v.start, s, out _) == false;
				}
				if (canIndent) b.Append('\t', indent);
				b.Append(s, v.start, v.Length);
				if (++i < a.Length || andNewline) b.AppendLine();
			}
		} else {
			b.Append(s);
			if (!s.Ends('\n')) b.AppendLine();
		}
	}

	/// <summary>
	/// From position in code gets ArgumentSyntax and its IParameterSymbol.
	/// </summary>
	/// <returns>If successful, ps is not null; then arg is not null if the argument list isn't empty.</returns>
	public static (ArgumentSyntax arg, IParameterSymbol ps) GetArgumentParameterFromPos(BaseArgumentListSyntax als, int pos, SemanticModel semo) {
		var args = als.Arguments;
		var index = args.Count == 0 ? 0 : als.Arguments.IndexOf(o => pos <= o.FullSpan.End); //print.it(index);
		if (index < 0) return default;
		if (!GetFunctionSymbolInfoFromArgumentList(als, semo, out var si)) return default;
		ArgumentSyntax arg = null;
		string name = null;
		if (args.Count > 0) {
			arg = args[index];
			var nc = arg.NameColon;
			if (nc != null) name = nc.Name.Identifier.Text;
		}
		return (arg, GetParameterSymbol(si, index, name, als.Arguments.Count, o => o.Type.TypeKind == TypeKind.Delegate));
	}

	/// <summary>
	/// Gets IParameterSymbol of siFunction's parameter matching argument index or name.
	/// </summary>
	/// <param name="siFunction">SymbolInfo of the method, ctor or indexer.</param>
	/// <param name="index">Argument index. Not used if used name.</param>
	/// <param name="name">Parameter name, if specified in the argument, else null.</param>
	/// <param name="argCount">Count of arguments.</param>
	/// <param name="filter"></param>
	public static IParameterSymbol GetParameterSymbol(in SymbolInfo siFunction, int index, string name, int argCount, Func<IParameterSymbol, bool> filter = null) {
		var sym = siFunction.Symbol;
		if (sym == null && siFunction.CandidateSymbols.Length == 1) sym = siFunction.CandidateSymbols[0];
		if (sym != null) return _Get(sym);
		foreach (var sym2 in siFunction.CandidateSymbols) {
			var v = _Get(sym2);
			if (v != null) return v;
		}
		return null;

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
		var ats = types.Select(o => semo.Compilation.GetTypeByMetadataName(o) ?? throw new ArgumentException("Type not found."));
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
					//print.it($"<>    <c 0xff8080>{v.Name}, {v.GetSymbolType()}<>");
					continue;
				}
				var span = n2 is ForEachStatementSyntax fe ? fe.Identifier.Span : n2.Span;
				if (pos <= span.End) {
					//print.it($"<>    <c 0xc0c0c0>{v.Name}, {v.GetSymbolType()}<>");
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
	/// Returns true if local/parameter variabled declared inside n aren't visible outside.
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
}
