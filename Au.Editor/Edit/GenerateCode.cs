extern alias CAW;

using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Rename;
using acc = Microsoft.CodeAnalysis.Accessibility;

/// <summary>
/// Inserts various code in code editor. With correct indentation etc.
/// Some functions can insert in other controls too.
/// </summary>
static class GenerateCode {
	/// <summary>
	/// Called from CiCompletion._ShowList on char '/'. If need, inserts XML doc comment with empty summary, param and returns tags.
	/// </summary>
	public static void DocComment(CodeInfo.Context cd) {
		int pos = cd.pos;
		string code = cd.code;
		SciCode doc = cd.sci;
		
		if (0 == code.Eq(pos - 3, false, "///\r", "///\n") || !InsertCodeUtil.IsLineStart(code, pos - 3)) return;
		
		var root = cd.syntaxRoot;
		var node = root.FindToken(pos).Parent;
		var start = node.SpanStart;
		if (start < pos) return;
		
		while (node is not MemberDeclarationSyntax) { //can be eg func return type (if no public etc) or attribute
			node = node.Parent;
			if (node == null) return;
		}
		if (node is GlobalStatementSyntax || node.SpanStart != start) return;
		
		//already has doc comment?
		foreach (var v in node.GetLeadingTrivia()) {
			if (v.IsDocumentationCommentTrivia) { //singleline (preceded by ///) or multiline (preceded by /**)
				var span = v.Span;
				if (span.Start != pos || span.Length > 2) return; //when single ///, span includes only newline after ///
			}
		}
		//print.it(pos);
		//CiUtil.PrintNode(node);
		
		string s = @" <summary>
/// 
/// </summary>";
		BaseParameterListSyntax pl = node switch {
			BaseMethodDeclarationSyntax met => met.ParameterList,
			RecordDeclarationSyntax rec => rec.ParameterList,
			IndexerDeclarationSyntax ids => ids.ParameterList,
			_ => null
		};
		if (pl != null) {
			var b = new StringBuilder(s);
			foreach (var p in pl.Parameters) {
				b.Append("\r\n/// <param name=\"").Append(p.Identifier.Text).Append("\"></param>");
			}
			if ((node is MethodDeclarationSyntax mm && !code.Eq(mm.ReturnType.Span, "void")) || node is IndexerDeclarationSyntax)
				b.Append("\r\n/// <returns></returns>");
			
			s = b.ToString();
			//rejected: <typeparam name="TT"></typeparam>. Rarely used.
		}
		
		s = InsertCodeUtil.IndentStringForInsertSimple(s, doc, pos);
		
		doc.aaaInsertText(true, pos, s, true, true);
		doc.aaaGoToPos(true, pos + s.Find("/ ") + 2);
	}
	
	/// <summary>
	/// Prints delegate code. Does not insert.
	/// </summary>
	public static void CreateDelegate() {
		if (!_CreateDelegate()) print.it("To create delegate code, the text cursor must be where a delegate can be used, for example after 'Event+=' or in a function argument list.");
	}
	
	static bool _CreateDelegate() {
		if (!CodeInfo.GetDocumentAndFindToken(out var cd, out var token)) return false;
		int pos = cd.pos;
		var semo = cd.semanticModel;
		
		if (token.IsKind(SyntaxKind.SemicolonToken)) {
			if (pos > token.SpanStart) return false;
			token = token.GetPreviousToken();
		}
		
		for (var node = token.Parent; node != null; node = node.Parent) {
			if (node is AssignmentExpressionSyntax aes) {
				if (node.Kind() is SyntaxKind.SimpleAssignmentExpression or SyntaxKind.AddAssignmentExpression or SyntaxKind.SubtractAssignmentExpression)
					//if (pos >= aes.OperatorToken.Span.End)
					return _GetTypeAndFormat(aes.Left, aes);
			} else if (node is BaseArgumentListSyntax als) {
				if (!node.Span.ContainsInside(pos)) continue;
				var (arg, ps) = InsertCodeUtil.GetArgumentParameterFromPos(als, pos, semo);
				if (ps != null) return _Format(ps.Type);
			} else if (node is ReturnStatementSyntax rss) {
				//if (pos >= rss.ReturnKeyword.Span.End)
				return _GetTypeAndFormat(rss.GetAncestor<MethodDeclarationSyntax>()?.ReturnType);
			} else if (node is ArrowExpressionClauseSyntax ae) {
				//if (pos >= ae.ArrowToken.Span.End)
				switch (node.Parent) {
				case MethodDeclarationSyntax m: return _GetTypeAndFormat(m.ReturnType);
				case PropertyDeclarationSyntax p: return _GetTypeAndFormat(p.Type);
				}
			} else continue;
			break;
		}
		
		return false;
		
		bool _GetTypeAndFormat(SyntaxNode sn, AssignmentExpressionSyntax aes = null) {
			if (sn == null) return false;
			return _Format(semo.GetTypeInfo(sn).Type, aes);
		}
		
		bool _Format(ITypeSymbol type, AssignmentExpressionSyntax aes = null) {
			if (type is not INamedTypeSymbol t || t is IErrorTypeSymbol || t.TypeKind != TypeKind.Delegate) return false;
			var b = new StringBuilder("<><lc #A0C0A0>Delegate method and lambda<>\r\n<code>");
			var m = t.DelegateInvokeMethod;
			
			//method
			
			var format = new SymbolDisplayFormat(
				genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
				memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef,
				miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers,
				parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeDefaultValue
				);
			
			b.Append(m.ToMinimalDisplayString(semo, pos, format));
			
			string methodName = "_RenameMe";
			if (aes != null) {
				if (aes.Left is IdentifierNameSyntax ins) {
					methodName = $"_{ins.Identifier.Text}";
				} else if (aes.Left is MemberAccessExpressionSyntax maes) {
					if (maes.Expression is IdentifierNameSyntax ins2) {
						methodName = $"_{ins2.Identifier.Text}_{maes.Name.Identifier.Text}";
					} else {
						methodName = $"_{maes.Name.Identifier.Text}";
					}
				}
			}
			b.Replace(" Invoke(", $" {methodName}(");
			
			b.Append(" {\r\n\t");
			if (!m.ReturnsVoid) b.Append("return default;"); //never mind ref return
			b.Append("\r\n}\r\n");
			
			//lambda
			
			var s = "()";
			if (m.Parameters.Any()) {
				format = format.WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters);
				bool withTypes = m.Parameters.Any(o => o.RefKind != 0 || o.IsParams);
				if (withTypes) format = format.RemoveParameterOptions(SymbolDisplayParameterOptions.IncludeDefaultValue);
				else format = format.WithParameterOptions(SymbolDisplayParameterOptions.IncludeName);
				s = m.ToMinimalDisplayString(semo, pos, format);
				if (m.Parameters.Length == 1 && !withTypes) s = s[7..^1]; else s = s[6..]; //remove 'Invoke' and maybe '()'
			}
			b.Append(s).AppendLine(" => </code>");
			
			print.it(b);
			return true;
		}
	}
	
	public static void ImplementInterfaceOrAbstractClass(int position = -1) {
		if (!CodeInfo.GetContextAndDocument(out var cd, position)) return;
		var semo = cd.semanticModel;
		
		var thisType = semo.GetEnclosingNamedType2(cd.pos, out var node, out var declNode);
		if (thisType == null || thisType.TypeKind is not (TypeKind.Class or TypeKind.Struct)) return;
		
		var baseClass = thisType.BaseType.IsAbstract ? thisType.BaseType : null;
		if (baseClass == null && thisType.Interfaces.IsDefaultOrEmpty) return;
		
		var baseFromPos = position < 0 && node.GetAncestorOrThis<BaseTypeSyntax>() is BaseTypeSyntax bts ? semo.GetTypeInfo(bts.Type).Type as INamedTypeSymbol : null;
		
		List<(INamedTypeSymbol type, List<ISymbol> members)> types = new();
		bool hasInterfaces = false;
		if (baseFromPos != null) _GetOfType(baseFromPos);
		else {
			if (baseClass != null) _GetOfType(baseClass);
			foreach (var t in thisType.Interfaces) _GetOfType(t);
		}
		
		//note: GetAllUnimplementedMembersInThis gets not all members. Eg for ITreeViewItem skips properties that have default impl (but includes such methods). Or gets some garbage.
		void _GetOfType(INamedTypeSymbol t) {
			List<ISymbol> members = null;
			bool isInterface = t.TypeKind == TypeKind.Interface;
			hasInterfaces |= isInterface;
			foreach (var m in t.GetMembers()) {
				if (!m.IsAbstract) if (!isInterface || m.IsStatic || m.DeclaredAccessibility == acc.Private) continue;
				if (m is not (IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.UserDefinedOperator or MethodKind.Conversion } or IPropertySymbol or IEventSymbol)) continue;
				ISymbol k = isInterface ? thisType.FindImplementationForInterfaceMember(m) : thisType.FindImplementationForAbstractMember(m);
				if (k != null && k.ContainingType != thisType) k = null;
				if (k == null) {
					if (members == null) types.Add((t, members = new()));
					members.Add(m);
				}
			}
			members?.Sort((x, y) => (y.IsAbstract ? 1 : 0) - (x.IsAbstract ? 1 : 0));
		}
		
		if (types.Count == 0) {
			dialog.show("Found 0 unimplemented members", owner: App.Hmain);
			return;
		}
		
		bool explicitly;
		string buttons = hasInterfaces
			? "1 Implement\nLike 'public Type Member'|2 Implement explicitly\nLike 'Type Interface.Member'|0 Cancel"
			: "1 Implement|0 Cancel";
		switch (dialog.show("Implement", "This will add members that were not implemented.", buttons, flags: DFlags.CommandLinks, owner: App.Hmain)) {
		case 1: explicitly = false; break;
		case 2: explicitly = true; break;
		default: return;
		}
		
		position = declNode.CloseBraceToken.Span.Start;
		while (cd.code[position - 1] is ' ' or '\t') position--;
		
		var b = new StringBuilder();
		var format = CiText.s_symbolFullFormat.WithParameterOptions(CiText.s_symbolFullFormat.ParameterOptions & ~SymbolDisplayParameterOptions.IncludeOptionalBrackets);
		var formatExp = format.WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef);
		
		var hsMembers = thisType.MemberNames.ToHashSet(); //nevermind method overloads, it's rare
		
		foreach (var (type, members) in types) {
			b.AppendLine().AppendLine("//" + type.ToMinimalDisplayString(semo, position, CiText.s_symbolFullFormat));
			
			bool isInterface = type.TypeKind == TypeKind.Interface;
			foreach (var v in members) {
				bool expl = false;
				if (isInterface) expl = explicitly || v.DeclaredAccessibility != acc.Public || !hsMembers.Add(v.Name);
				
				string append = null;
				switch (v) {
				case IMethodSymbol ims:
					if (ims.MethodKind == MethodKind.Ordinary) {
						append = ims.ReturnsVoid ? @" {

}" : @" {

	return default;
}";
					} else append = " => default;";
					break;
				case IPropertySymbol ips:
					if (!expl && isInterface) {
						if (ips.GetMethod != null && ips.GetMethod.DeclaredAccessibility != acc.Public) expl = true;
						if (ips.SetMethod != null && ips.SetMethod.DeclaredAccessibility != acc.Public) expl = true;
					}
					break;
				case IEventSymbol:
					append = !expl ? ";" : @" {
	add {  }
	remove {  }
}";
					break;
				default:
					continue;
				}
				
				b.AppendLine();
				if (isInterface) {
					if (!v.IsAbstract) b.AppendLine("//has default implementation");
					if (!expl) b.Append("public ");
					if (v.IsStatic) b.Append("static ");
				} else {
					b.Append(v.DeclaredAccessibility switch { acc.Public => "public", acc.Internal => "internal", acc.Protected => "protected", acc.ProtectedOrInternal => "protected internal", acc.ProtectedAndInternal => "private protected", _ => "" });
					b.Append(" override ");
				}
				b.Append(v.ToMinimalDisplayString(semo, position, expl ? formatExp : format))
					.AppendLine(append);
			}
		}
		
		var text = b.ToString();
		text = text.Replace("] { get; set; }", @"] {
	get { return default; }
	set {  }
}"); //indexers
		text = text.RxReplace(@"[^\]] \{\K set; \}", @"
	set {  }
}"); //write-only properties
		
		text = InsertCodeUtil.IndentStringForInsertSimple(text, cd.sci, position, true, 1);
		
		cd.sci.aaaInsertText(true, position, text, addUndoPointAfter: true);
		cd.sci.aaaGoToPos(true, position);
		cd.sci.aaaSelect(true, position + text.Length, position);
		
		//tested: Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementInterfaceService works but the result is badly formatted (without spaces, etc). Internal, undocumented.
	}
}
