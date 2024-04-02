extern alias CAW;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Completion;
//using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
//using Microsoft.CodeAnalysis.Tags;
//using Microsoft.CodeAnalysis.DocumentationComments;
//using Microsoft.CodeAnalysis.FindSymbols;
//using Roslyn.Utilities;

/// <summary>
/// Code info util extension methods.
/// </summary>
static class CiUtilExt {
	/// <summary>
	/// Detects whether <i>position</i> is inside a string (literal or interpolated, except when in an interpolation code).
	/// </summary>
	/// <returns>
	/// - false - not in a string; or is in a hole of an interpolated string.
	/// - true - in a string; or in a text part of an interpolated string.
	/// - null - inside of a prefix or suffix of a string or interpolation, eg @", $", @$", """, $$""", {{, }}.
	/// </returns>
	/// <param name="t">This token. It's ref; if <b>EndOfFileToken</b>, the function sets it = previous token.</param>
	/// <param name="position">Where this token has been found using code like <c>var token = root.FindToken(position);</c>.</param>
	/// <param name="code">All code. The function uses it to compare substrings easier and faster.</param>
	/// <param name="x">If the function returns true, receives span etc.</param>
	/// <param name="orU8">Support "text"u8 etc.</param>
	public static bool? IsInString(this ref SyntaxToken t, int position, string code, out CiStringInfo x, bool orU8 = false) {
		x = default;
		var k = t.Kind();
		if (k == SyntaxKind.EndOfFileToken) {
			t = t.GetPreviousToken(includeSkipped: true, includeDirectives: true);
			k = t.Kind();
		}
		//CiUtil.PrintNode(t);
		
		var span = t.Span;
		int start = span.Start, end = span.End;
		if (position < start || position > end) return false;
		bool isInterpolated = false, isVerbatim = false, isRaw = false, isRawPrefixCenter = false, isU8 = false;
		var node = t.Parent;
		
		if (node.IsKind(SyntaxKind.StringLiteralExpression) || (orU8 && (isU8 = node.IsKind(SyntaxKind.Utf8StringLiteralExpression)))) {
			if (position == start) return false;
			if (k is not (SyntaxKind.StringLiteralToken or SyntaxKind.Utf8StringLiteralToken)) isRaw = true;
			if (!isRaw) {
				if (isVerbatim = code[start++] == '@') if (position == start++) return null; //inside @"
				if (!isU8) {
					if (position < end) { end--; goto gTrue; }
					if (code[end - 1] != '"' || end == start || node.NoClosingQuote()) goto gTrue; //no closing "
				} else if (position < end) {
					end -= 3;
					if (position > end) return null;
					goto gTrue;
				}
				return false;
			} else {
				while (start < end && code[start] == '"') start++; //skip """
				int nq = start - span.Start;
				bool ml = k is SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.Utf8MultiLineRawStringLiteralToken;
				if (position < start) { //inside """
					if (!isU8 && _IsRawPrefixCenter(span.Start, nq)) goto gTrue;
					return null;
				}
				if (ml) { //skip newline
					while (start < end && code[start] is ' ' or '\t') start++;
					if (code[start] == '\r') start++;
					if (code[start++] != '\n') return null;
				}
				if (position < start) return null; //before newline
				
				if (position == end) {
					if (!isU8 && node.NoClosingQuote()) goto gTrue;
					return false;
				}
				
				if (isU8) end -= 2;
				while (nq > 0 && end > start && code[--end] == '"') nq--;
				if (nq > 0) goto gTrue; //unterminated
				if (position > end) return null; //inside """, or """ not in its own line (error)
				if (ml) {
					while (end > start && code[end - 1] is ' ' or '\t') end--;
					if (code[--end] != '\n') return null;
					if (code[end - 1] == '\r') end--;
					if (position > end) return null;
					//never mind indentation
				}
				goto gTrue;
			}
		}
		isInterpolated = true;
		
		bool _IsRawPrefixCenter(int iq, int nq) {
			//most likely the user will write a raw string at """|""" and not at """"""|
			if (nq < 6 || 0 != (nq & 1)) return false;
			iq += nq / 2; if (iq != position) return false;
			//if (isRawPrefixCenter = node.NoClosingQuote()) start = end = iq; //not always works
			//return isRawPrefixCenter;
			start = end = iq;
			return isRawPrefixCenter = true;
		}
		
		if (k is SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken) {
			int iq = start, nq = 0; while (code[iq] == '$') iq++;
			while (code.Eq(iq + nq, '"')) nq++;
			if (_IsRawPrefixCenter(iq, nq)) goto gTrue;
		}
		
		switch (k) {
		case SyntaxKind.InterpolatedStringTextToken:
			goto gTrue;
		case SyntaxKind.InterpolatedStringEndToken:
		case SyntaxKind.InterpolatedRawStringEndToken when position == start:
			_BackToTextToken(ref t);
			goto gTrue;
		case SyntaxKind.InterpolatedRawStringEndToken:
			return position == end ? false : null;
		case SyntaxKind.InterpolatedStringStartToken or SyntaxKind.InterpolatedVerbatimStringStartToken:
		case SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken:
			return position > start ? null : false;
		case SyntaxKind.OpenBraceToken when t.Parent is InterpolationSyntax:
			if (position == start) {
				if (!_BackToTextToken(ref t)) return null;
				goto gTrue;
			}
			if (code.Eq(position - 1, "{{")) return null;
			break;
		case SyntaxKind.CloseBraceToken when t.Parent is InterpolationSyntax:
			if (code.Eq(position - 1, "}}")) return null;
			break;
		}
		
		bool _BackToTextToken(ref SyntaxToken t) {
			var tt = t.GetPreviousToken();
			var kk = tt.Kind();
			if (kk == SyntaxKind.OpenBraceToken) return false;
			if (kk == SyntaxKind.InterpolatedStringTextToken) (start, end) = tt.Span;
			else end = start;
			return true;
		}
		
		return false;
		gTrue:
		x.textSpan = TextSpan.FromBounds(start, end);
		x.stringNode = t.Parent;
		if (isInterpolated && x.stringNode is not InterpolatedStringExpressionSyntax) x.stringNode = x.stringNode.Parent;
		Debug.Assert(x.stringNode is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax);
		if (x.isInterpolated = isInterpolated) {
			var ns = x.stringNode.SpanStart;
			if (code[ns] == '@' || code[ns + 1] == '@') isVerbatim = true;
			else if (code[ns + 1] != '"' || code.Eq(ns + 1, "\"\"\"")) isRaw = true;
		}
		x.isVerbatim = isVerbatim;
		x.isRaw = isRaw;
		x.isClassic = !(isVerbatim | isRaw);
		x.isRawPrefixCenter = isRawPrefixCenter;
		x.isU8 = isU8;
		return true;
	}
	
	/// <summary>
	/// Returns true if this token is a string, and the closing quote(s) is missing (contains diagnostics error).
	/// </summary>
	public static bool NoClosingQuote(this SyntaxNode t) //fast
		=> t.ContainsDiagnostics && t.GetDiagnostics().Any(o => o.Id is "CS1010" or "CS1039" or "CS8997");
	//Newline in constant, Unterminated string literal, Unterminated raw string literal.
	//note: SyntaxToken may contain the same diagnostics too, but not always, eg no if $$"""""". Same speed.
	
	public static void Deconstruct(this TextSpan t, out int Start, out int End) { Start = t.Start; End = t.End; }
	
	public static Range ToRange(this TextSpan t) => t.Start..t.End;
	
	/// <summary>
	/// <c>position &gt; t.Start &amp;&amp; position &lt; t.End;</c>
	/// </summary>
	public static bool ContainsInside(this TextSpan t, int position) => position > t.Start && position < t.End;
	
	/// <summary>
	/// <c>position &gt;= t.Start &amp;&amp; position &lt;= t.End;</c>
	/// </summary>
	public static bool ContainsOrTouches(this TextSpan t, int position) => position >= t.Start && position <= t.End;
	
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
	/// Finds token at <i>position</i>. Returns true if its span contains or touches <i>position</i>.
	/// May return previous token in some cases, eg if the found token is EndOfFileToken.
	/// </summary>
	public static bool FindTouchingToken(this SyntaxNode t, out SyntaxToken token, int position, bool findInsideTrivia = false) {
		token = t.FindToken(position, findInsideTrivia);
		if (!token.IsKind(SyntaxKind.EndOfFileToken) && token.Span.ContainsOrTouches(position)) return true;
		token = token.GetPreviousToken();
		if (token.Span.End == position) return true;
		token = default;
		return false;
	}
	
	/// <summary>
	/// Gets full span, not including leading trivia that is not comments/doccomments touching the declaration.
	/// </summary>
	/// <param name="minimalLeadingTrivia">Get leading trivia just until the first newline when searching backwards. Usually it is indentation whitespace or nothing.</param>
	public static TextSpan GetRealFullSpan(this SyntaxNode t, bool minimalLeadingTrivia = false) {
		int from = t.SpanStart;
		var a = t.GetLeadingTrivia();
		for (int i = a.Count; --i >= 0;) {
			var v = a[i];
			var k = v.Kind();
			//print.it(i, k);
			if (k == SyntaxKind.EndOfLineTrivia) {
				if (i == 0 || minimalLeadingTrivia) break;
				k = a[i - 1].Kind();
				if (k == SyntaxKind.EndOfLineTrivia) break;
				if (k == SyntaxKind.WhitespaceTrivia) if (i == 1 || a[i - 2].IsKind(SyntaxKind.EndOfLineTrivia)) break;
			} else {
				if (k is not (SyntaxKind.WhitespaceTrivia
					or SyntaxKind.SingleLineCommentTrivia
					or SyntaxKind.SingleLineDocumentationCommentTrivia
					or SyntaxKind.MultiLineCommentTrivia
					or SyntaxKind.MultiLineDocumentationCommentTrivia)
					) break;
			}
			from = v.SpanStart;
		}
		return TextSpan.FromBounds(from, t.FullSpan.End);
	}
	
	public static bool Eq(this string t, TextSpan span, string s, bool ignoreCase = false)
		=> t.Eq(span.Start..span.End, s, ignoreCase);
	
	/// <summary>
	/// SyntaxFacts.IsNewLine(t[i]);
	/// </summary>
	public static bool IsCsNewlineChar(this string t, int i)
		=> SyntaxFacts.IsNewLine(t[i]);
	
	/// <summary>
	/// i == 0 || SyntaxFacts.IsNewLine(t[i - 1]);
	/// </summary>
	public static bool IsCsStartOfLine(this string t, int i)
		=> i == 0 || SyntaxFacts.IsNewLine(t[i - 1]);
	
	/// <summary>
	/// i == t.Length || SyntaxFacts.IsNewLine(t[i]);
	/// </summary>
	public static bool IsCsEndOfLine(this string t, int i)
		=> i == t.Length || SyntaxFacts.IsNewLine(t[i]);
	
	[Conditional("DEBUG")]
	public static void DebugPrint(this CompletionItem t, string color = "blue") {
		print.it($"<><c {color}>{t.DisplayText},    {string.Join("|", t.Tags)},    prefix={t.DisplayTextPrefix},    suffix={t.DisplayTextSuffix},    filter={t.FilterText},    sort={t.SortText},    inline={t.InlineDescription},    automation={t.AutomationText},    provider={t.ProviderName}<>");
		print.it(string.Join("\n", t.Properties));
	}
	
	[Conditional("DEBUG")]
	public static void DebugPrintIf(this CompletionItem t, bool condition, string color = "blue") {
		if (condition) DebugPrint(t, color);
	}
	
	/// <summary>
	/// Gets symbol name as single word good for displaying etc.
	/// Known cases when the return value != <b>Name</b>:
	/// ".ctor" -> "TypeName".
	/// "Finalize" -> "~TypeName".
	/// "QualifiedInterface.Explicit" -> "Explicit".
	/// </summary>
	public static string JustName(this ISymbol t) {
		var s = t.Name;
		if (SyntaxFacts.IsValidIdentifier(s) && !t.IsDestructor()) return s;
		return t.ToDisplayString(SymbolDisplayFormat.ShortFormat);
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
	/// Like <b>GetEnclosingNamedType</b>, but <i>pos</i> can be anywhere in the class/struct/interface/enum definition span, not just in { }.
	/// </summary>
	public static INamedTypeSymbol GetEnclosingNamedType2(this SemanticModel t, int pos, out SyntaxNode posNode, out BaseTypeDeclarationSyntax declNode) {
		posNode = t.Root.FindToken(pos).Parent;
		declNode = posNode?.GetAncestorOrThis<BaseTypeDeclarationSyntax>();
		if (declNode == null || !declNode.Span.Contains(pos) || declNode.CloseBraceToken.IsMissing) return null;
		return t.GetEnclosingNamedType(declNode.OpenBraceToken.SpanStart + 1, default);
	}
	
	public static (string kind, string access) ImageResource(this ISymbol t) {
		var glyph = t.GetGlyph();
		return glyph switch {
			Glyph.ClassPublic => ("resources/ci/class.xaml", null),
			Glyph.ClassInternal => ("resources/ci/class.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.ClassPrivate => ("resources/ci/class.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.ClassProtected => ("resources/ci/class.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.ConstantPublic => ("resources/ci/constant.xaml", null),
			Glyph.ConstantInternal => ("resources/ci/constant.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.ConstantPrivate => ("resources/ci/constant.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.ConstantProtected => ("resources/ci/constant.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.DelegatePublic => ("resources/ci/delegate.xaml", null),
			Glyph.DelegateInternal => ("resources/ci/delegate.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.DelegatePrivate => ("resources/ci/delegate.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.DelegateProtected => ("resources/ci/delegate.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.EnumPublic => ("resources/ci/enum.xaml", null),
			Glyph.EnumInternal => ("resources/ci/enum.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.EnumPrivate => ("resources/ci/enum.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.EnumProtected => ("resources/ci/enum.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.EnumMemberPublic => ("resources/ci/enummember.xaml", null),
			Glyph.EnumMemberInternal => ("resources/ci/enummember.xaml", "resources/ci/overlayinternal.xaml"), //?
			Glyph.EnumMemberPrivate => ("resources/ci/enummember.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.EnumMemberProtected => ("resources/ci/enummember.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.EventPublic => ("resources/ci/event.xaml", null),
			Glyph.EventInternal => ("resources/ci/event.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.EventPrivate => ("resources/ci/event.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.EventProtected => ("resources/ci/event.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.ExtensionMethodPublic => ("resources/ci/extensionmethod.xaml", null),
			Glyph.ExtensionMethodInternal => ("resources/ci/extensionmethod.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.ExtensionMethodPrivate => ("resources/ci/extensionmethod.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.ExtensionMethodProtected => ("resources/ci/extensionmethod.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.FieldPublic => ("resources/ci/field.xaml", null),
			Glyph.FieldInternal => ("resources/ci/field.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.FieldPrivate => ("resources/ci/field.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.FieldProtected => ("resources/ci/field.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.InterfacePublic => ("resources/ci/interface.xaml", null),
			Glyph.InterfaceInternal => ("resources/ci/interface.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.InterfacePrivate => ("resources/ci/interface.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.InterfaceProtected => ("resources/ci/interface.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.MethodPublic => ("resources/ci/method.xaml", null),
			Glyph.MethodInternal => ("resources/ci/method.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.MethodPrivate => ("resources/ci/method.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.MethodProtected => ("resources/ci/method.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.PropertyPublic => ("resources/ci/property.xaml", null),
			Glyph.PropertyInternal => ("resources/ci/property.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.PropertyPrivate => ("resources/ci/property.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.PropertyProtected => ("resources/ci/property.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.StructurePublic => ("resources/ci/structure.xaml", null),
			Glyph.StructureInternal => ("resources/ci/structure.xaml", "resources/ci/overlayinternal.xaml"),
			Glyph.StructurePrivate => ("resources/ci/structure.xaml", "resources/ci/overlayprivate.xaml"),
			Glyph.StructureProtected => ("resources/ci/structure.xaml", "resources/ci/overlayprotected.xaml"),
			
			Glyph.Keyword => ("resources/ci/keyword.xaml", null), //implicit 'value' parameter in a property etc
			Glyph.Label => ("resources/ci/label.xaml", null),
			Glyph.Local or Glyph.Parameter or Glyph.RangeVariable => ("resources/ci/localvariable.xaml", null),
			Glyph.Namespace => ("resources/ci/namespace.xaml", null),
			Glyph.Operator => ("resources/ci/operator.xaml", null),
			Glyph.TypeParameter => ("resources/ci/typeparameter.xaml", null),
			_ => default
		};
	}
	
	/// <summary>
	/// Appends n tabs or n*4 spaces, depending on <b>App.Settings.ci_formatIndentationSpaces</b>.
	/// </summary>
	public static StringBuilder AppendIndent(this StringBuilder t, int n) {
		if (App.Settings.ci_formatTabIndent) t.Append('\t', n);
		else t.Append(' ', n * 4);
		return t;
	}
}

record struct CiStringInfo {
	/// <summary>
	/// The text part of string literal or a text part of interpolated string. Without enclosing and prefix.
	/// </summary>
	public TextSpan textSpan;
	
	/// <summary>
	/// Entire string node. Can be either <b>LiteralExpressionSyntax</b> or <b>InterpolatedStringExpressionSyntax</b>.
	/// </summary>
	public SyntaxNode stringNode;
	
	public bool isVerbatim, isRaw, isInterpolated, isU8;
	
	/// <summary>
	/// Not verbatim/raw. Can be "...", $"..." or "..."u8.
	/// </summary>
	public bool isClassic;
	
	/// <summary>
	/// At """|""".
	/// </summary>
	public bool isRawPrefixCenter;
}