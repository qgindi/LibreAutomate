using System.Linq;
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
	/// </summary>
	public static string IndentStringForInsert(string s, SciCode doc, int pos) {
		int indent = doc.zLineIndentationFromPos(true, pos);
		if (indent > 0) s = s.RxReplace(@"(?<=\n)", new string('\t', indent));
		return s;
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
}
