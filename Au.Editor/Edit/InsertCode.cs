using System.Linq;
using Au.Controls;
using System.Windows;
using System.Windows.Controls;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using acc = Microsoft.CodeAnalysis.Accessibility;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Rename;

[Flags]
/// <summary>Flags for <see cref="InsertCode.Statements"/>.</summary>
enum ICSFlags {
	/// <summary>If text contains '%', remove it and finally move caret there.</summary>
	GoToPercent = 1,

	/// <summary>Collapse folding points in inserted code.</summary>
	Fold = 2,

	/// <summary>Activate editor window.</summary>
	ActivateEditor = 4,

	/// <summary>Don't focus the editor control. Without this flag focuses it if window is active or activated.</summary>
	NoFocus = 8,

	GoToStart = 16,

	SelectNewCode = 32,
}

/// <summary>
/// Inserts various code in code editor. With correct indentation etc.
/// Some functions can insert in other controls too.
/// </summary>
static class InsertCode {
	/// <summary>
	/// Inserts one or more statements at current line. With correct position, indentation, etc.
	/// If editor is null or readonly, prints in output.
	/// Async if called from non-main thread.
	/// </summary>
	/// <param name="s">Text. The function ignores "\r\n" at the end. Does nothing if null.</param>
	/// <param name="separate">Prepend/append empty line to separate from surrounding code if need. If null, does it if <i>s</i> contains '\n'.</param>
	public static void Statements(string s, ICSFlags flags = 0, bool? separate = null) {
		if (s == null) return;
		bool sep = separate ?? s.Contains('\n');

		if (Environment.CurrentManagedThreadId == 1) _Statements(s, flags, sep);
		else App.Dispatcher.InvokeAsync(() => _Statements(s, flags, sep));
	}

	static void _Statements(string s, ICSFlags flags, bool separate) {
		if (!App.Hmain.IsVisible) App.ShowWindow();
		if (!CodeInfo.GetContextAndDocument(out var k, metaToo: true)) {
			print.it(s);
			return;
		}
		var root = k.syntaxRoot;
		var code = k.code;
		var pos = k.pos;
		var token = root.FindToken(pos);
		var node = token.Parent;

		//get the best valid insertion place

		bool havePos = false;
		var last = root.AttributeLists.LastOrDefault() as SyntaxNode
			?? root.Usings.LastOrDefault() as SyntaxNode
			?? root.Externs.LastOrDefault() as SyntaxNode
			?? root.GetDirectives(o => o is DefineDirectiveTriviaSyntax).LastOrDefault();
		if (last != null) {
			int e1 = last.FullSpan.End;
			if (havePos = pos <= e1) pos = e1;
		}

		if (!havePos) {
			var members = root.Members;
			if (members.Any()) {
				var g = members.LastOrDefault(o => o is GlobalStatementSyntax);
				int posAfterTLS = g?.FullSpan.End ?? members.First().FullSpan.Start;

				bool done1 = false;
				if (node is BlockSyntax) {
					done1 = node.Span.ContainsInside(pos);
				} else if (node is MemberDeclarationSyntax) {
					done1 = true;
					//don't use posAfterTLS if before the first type
					bool here = node == members.FirstOrDefault(o => o is not GlobalStatementSyntax) && pos <= node.SpanStart;
					pos = Math.Min(pos, here ? node.SpanStart : posAfterTLS);
				} else if (node is CompilationUnitSyntax && g != members[^1]) { //after types
					done1 = true;
					pos = Math.Min(pos, posAfterTLS);
				}
				if (!done1) {
					for (; node is not CompilationUnitSyntax; node = node.Parent) {
						//CiUtil.PrintNode(node);
						if (node is StatementSyntax) {
							var pa = node.Parent;
							if (node is BlockSyntax && pa is not (BlockSyntax or GlobalStatementSyntax)) continue;
							var span = node.Span;
							if (havePos = pos >= span.End && token.IsKind(SyntaxKind.CloseBraceToken)) pos = node.FullSpan.End;
							else if (havePos = pos >= span.Start) pos = span.Start;
							break;
						}
						if (node is MemberDeclarationSyntax) {
							pos = posAfterTLS;
							break;
						}
					}
				}

				havePos |= pos == posAfterTLS;
			}
		}

		if (k.meta.end > 0 && pos <= k.meta.end) {
			havePos = true;
			pos = k.meta.end;
			if (code.Eq(pos, "\r\n")) pos += 2; else if (code.Eq(pos, '\n')) pos++;
		}

		if (!havePos) { //if in comments or directive or disabled code, move to the start of trivia or line
			var trivia = root.FindTrivia(pos);
			var tk = trivia.Kind();
			if (tk is not (SyntaxKind.EndOfLineTrivia or SyntaxKind.None)) {
				if (tk == SyntaxKind.DisabledTextTrivia) {
					while (pos > 0 && code[pos - 1] != '\n') pos--;
				} else {
					pos = trivia.FullSpan.Start;
				}
				//rejected: move to the start of entire #if ... block.
				//	Rare, not easy, may be far, and maybe user wants to insert into disabled code.
			}
		}

		//rename symbols in s if need

		try { _RenameNewSymbols(ref s, k.document, node, pos); }
		catch (Exception e1) { Debug_.Print(e1); }

		//indent, newlines

		string breakLine = null;
		for (; pos > 0 && code[pos - 1] != '\n'; pos--) if (code[pos - 1] is not (' ' or '\t')) { breakLine = "\r\n"; break; }
		int replTo = pos; while (replTo < code.Length && code[replTo] is ' ' or '\t') replTo++;

		var d = k.sci;

		var t2 = root.FindToken(pos); if (t2.SpanStart >= pos) t2 = t2.GetPreviousToken();
		bool afterOpenBrace = t2.IsKind(SyntaxKind.OpenBraceToken);
		bool beforeCloseBrace = replTo < code.Length && code[replTo] == '}';

		int indent = d.zLineIndentationFromPos(true, pos);
		if (afterOpenBrace && breakLine != null && !(t2.Parent is BlockSyntax bs1 && bs1.Parent is GlobalStatementSyntax)) indent++;
		else if (beforeCloseBrace) indent++;

		var b = new StringBuilder(breakLine);
		if (separate && !afterOpenBrace && !s.Starts("{\r\n") && pos > 0) {
			int nn = 0; for (int i = pos; --i >= 0 && code[i] <= ' ';) if (code[i] == '\n' && ++nn == 2) break;
			if (nn < 2) b.Append('\t', indent).AppendLine();
		}

		InsertCodeUtil.AppendCodeWithIndent(b, s, indent, andNewline: true);

		if (separate && !s.Ends("\n}") && replTo < code.Length && code[replTo] is not ('\r' or '}')) {
			b.Append('\t', indent).AppendLine();
		}
		if (indent > 0) b.Append('\t', beforeCloseBrace ? indent - 1 : indent);
		s = b.ToString();

		//insert

		int go = -1;
		if (flags.Has(ICSFlags.GoToPercent)) {
			go = s.IndexOf('%');
			if (go >= 0) s = s.Remove(go, 1);
		}

		CodeInfo.Pasting(d, silent: true);
		d.zSetAndReplaceSel(true, pos, replTo, s);

		if (go >= 0) d.zGoToPos(true, pos + go);
		else if (flags.Has(ICSFlags.SelectNewCode)) d.zSelect(true, pos + s.TrimEnd('\t').Length, pos, true);
		else if (flags.Has(ICSFlags.GoToStart)) d.zGoToPos(true, pos);

		if (flags.Has(ICSFlags.Fold)) _FoldInsertedCode(d, pos, s.LineCount());

		static void _FoldInsertedCode(SciCode doc, int start, int nLines) {
			string text = doc.zText;
			timer.after(400, _ => { //because fold points are added async, 250 ms timer + async/await
				var d = Panels.Editor.ZActiveDoc; if (d != doc || d.zText != text) return;
				for (int line = d.zLineFromPos(true, start), i = line + nLines - 1; --i >= line;) {
					if (0 != (d.Call(Sci.SCI_GETFOLDLEVEL, i) & Sci.SC_FOLDLEVELHEADERFLAG)) d.Call(Sci.SCI_FOLDLINE, i);
				}
			});
		}

		var w = d.Hwnd.Window;
		if (flags.Has(ICSFlags.ActivateEditor)) w.ActivateL();
		if (!flags.Has(ICSFlags.NoFocus) && w.IsActive) d.Focus();
	}

	static void _RenameNewSymbols(ref string s, Document doc1, SyntaxNode node, int pos) {
		//find the function or TLS where to look for declared symbols in editor code
		var scope = node;
		for (; scope is not CompilationUnitSyntax; scope = scope.Parent) {
			if (scope is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax) {
				if (scope.Span.ContainsInside(pos)) break;
			}
			//CiUtil.PrintNode(scope);
		}
		//CONSIDER: make names unique in non-static local/lambda/anonymous functions and container.

		//modified Roslyn's GetAllDeclaredSymbols. Would be difficult to use it.
		static void _GetDeclaredSymbols(SemanticModel semo, SyntaxNode node, List<ISymbol> symbols, HashSet<string> names, /*int pos = -1,*/ bool skipDesc = false) {
			if (node is not CompilationUnitSyntax && semo.GetDeclaredSymbol(node) is ISymbol sym) {
				if (symbols != null) symbols.Add(sym);
				if (names != null) names.Add(sym.Name);
			}
			if (skipDesc) return;
			foreach (var n in node.ChildNodes()) {
				if (n is AnonymousFunctionExpressionSyntax or AnonymousObjectCreationExpressionSyntax or TupleExpressionSyntax) continue; //lambda etc

				//rejected: don't rename if variables in both codes are in unrelated { blocks }.
				//	This code detects blocks in editor code. Also would need to detect in new code.
				//if (pos > 0 && n is BlockSyntax && !n.Span.ContainsInside(pos)) continue;

				_GetDeclaredSymbols(semo, n, symbols, names, /*pos,*/ n is LocalFunctionStatementSyntax or BaseTypeDeclarationSyntax);
			}
		}

		//get symbols declared in s
		List<ISymbol> a2 = new();
		HashSet<string> h2 = new();
		var doc2 = CiUtil.CreateDocumentFromCode(s, false);
		var semo2 = doc2.GetSemanticModelAsync().Result;
		_GetDeclaredSymbols(semo2, semo2.Root, a2, h2);
		//print.it("---- h2 ----"); foreach (var v in h2) print.it(v);
		if (h2.Count == 0) return;

		//get names of symbols declared in scope (editor code)
		HashSet<string> h1 = new();
		var semo1 = doc1.GetSemanticModelAsync().Result;
		_GetDeclaredSymbols(semo1, scope, null, h1/*, pos*/);
		if (scope is CompilationUnitSyntax) h1.Add("args");
		//print.it("---- h1 ----"); foreach (var v in h1) print.it(v);
		if (h1.Count == 0) return;

		//in s replace symbol names that exist in scope (editor code)
		bool renamed = false;
		var sol = doc2.Project.Solution;
		foreach (var sym in a2) {
			string name = sym.Name, name2;
			if (!h1.Contains(name)) continue;

			//create unique name and add to hs1
			int i = 2;
			if (name.RxMatch(@"\d+$", 0, out RXGroup g)) {
				i = Math.Max(i, name.ToInt(g.Start) + 1);
				name = name[..g.Start];
			}
			while (h2.Contains(name2 = name + i) || !h1.Add(name2)) i++;
			//print.it(sym.Name, name2);

			sol = Renamer.RenameSymbolAsync(sol, sym, name2, null).Result;
			h2.Remove(sym.Name);
			renamed = true;
		}
		if (renamed) s = sol.GetDocument(doc2.Id).GetTextAsync().Result.ToString();

		//rejected: also replace names that match reserved keywords. This program will not create such names.
	}

	/// <summary>
	/// Inserts text in code editor at current position, not as new line, replaces selection.
	/// If editor is null or readonly, does nothing.
	/// </summary>
	/// <param name="s">If contains '%', removes it and moves caret there.</param>
	public static void TextSimply(string s) {
		Debug.Assert(Environment.CurrentManagedThreadId == 1);
		var d = Panels.Editor.ZActiveDoc;
		if (d == null || d.zIsReadonly) return;
		TextSimplyInControl(d, s);
	}

	/// <summary>
	/// Inserts text in specified or focused control.
	/// At current position, not as new line, replaces selection.
	/// </summary>
	/// <param name="c">If null, uses the focused control, else sets focus.</param>
	/// <param name="s">
	/// If contains '%', removes it and moves caret there.
	/// Alternatively use '\b', then does not touch '%'.
	/// If contains '%' or \b, must be single line.
	/// </param>
	public static void TextSimplyInControl(FrameworkElement c, string s) { //SHOULDDO: flags for processing % etc
		if (c == null) {
			c = App.FocusedElement;
			if (c == null) return;
		} else {
			Debug.Assert(Environment.CurrentManagedThreadId == c.Dispatcher.Thread.ManagedThreadId);
			if (c != App.FocusedElement) //be careful with HwndHost
				c.Focus();
		}

		int i = s.IndexOf('\b');
		if (i < 0) i = s.IndexOf('%');
		if (i >= 0) {
			Debug.Assert(!s.Contains('\r'));
			s = s.Remove(i, 1);
			i = s.Length - i;
		}

		if (c is KScintilla sci) {
			if (sci.zIsReadonly) return;
			sci.zReplaceSel(s);
			while (i-- > 0) sci.Call(Sci.SCI_CHARLEFT);
		} else if (c is TextBox tb) {
			if (tb.IsReadOnly) return;
			tb.SelectedText = s;
			tb.CaretIndex = tb.SelectionStart + tb.SelectionLength - Math.Max(i, 0);
		} else {
			Debug_.Print(c);
			if (!c.Hwnd().Window.ActivateL()) return;
			Task.Run(() => {
				var k = new keys(null);
				k.AddText(s);
				if (i > 0) k.AddKey(KKey.Left).AddRepeat(i);
				k.SendIt();
			});
		}
	}

	/// <summary>
	/// In current document gently replaces text in range from..to with before + text + after. Indents if need.
	/// Ignores newline at the end of the range text.
	/// </summary>
	public static void Surround(int from, int to, string before, string after, int indentPlus, bool concise = false) {
		var doc = Panels.Editor.ZActiveDoc;

		int indent = doc.zLineIndentationFromPos(true, from);
		if (indent > 0) {
			var si = new string('\t', indent);
			before = before.RxReplace("(?m)^", si);
			after = after.RxReplace("(?m)^", si);
		}

		var s = doc.zRangeText(true, from, to);
		var b = new StringBuilder();

		if(concise && s.LineCount() <= 1) {
			b.Append(before.TrimEnd("\r\n")).Append(' ');
			b.Append(s.TrimEnd("\r\n")).Append(' ');
			b.Append(after.TrimStart("\r\n"));
		} else {
			b.Append(before);
			InsertCodeUtil.AppendCodeWithIndent(b, s, indentPlus, andNewline: false);
			b.Append(after);
		}

		doc.ZReplaceTextGently(b.ToString(), from..to);
	}

	/// <summary>
	/// In current document gently replaces the selected text or statement with before + text + after. Indents if need.
	/// Ignores newline at the end of the selected text.
	/// </summary>
	/// <param name="concise">If text is single line, surround as single line.</param>
	public static void Surround(string before, string after, int indentPlus, bool concise = false) {
		var doc = Panels.Editor.ZActiveDoc;
		int from = doc.zSelectionStart16, to = doc.zSelectionEnd16;
		if (from == to) {
			if (!CodeInfo.GetContextAndDocument(out var cd, from)) return;
			var stat = CiUtil.GetStatementEtcFromPos(cd, from);
			if (stat == null) return;
			var span = stat.GetRealFullSpan(minimalLeadingTrivia: true);
			(from, to) = span;
		}
		Surround(from, to, before, after, indentPlus, concise);
	}

	/// <summary>
	/// Inserts code 'using ns;\r\n' in correct place in editor text, unless it is already exists.
	/// Returns true if inserted.
	/// </summary>
	/// <param name="ns">Namespace, eg "System.Diagnostics". Can be multiple, separated with semicolon (can be whitespce around).</param>
	/// <param name="missing">Don't check whether the usings exist. Caller knows they don't exist.</param>
	public static bool UsingDirective(string ns, bool missing = false) {
		Debug.Assert(Environment.CurrentManagedThreadId == 1);
		if (!CodeInfo.GetContextAndDocument(out var k, 0, metaToo: true)) return false;
		var namespaces = ns.Split(';', StringSplitOptions.TrimEntries);
		int i = _FindUsingsInsertPos(k, missing ? null : namespaces);
		if (i < 0) return false;

		var b = new StringBuilder();
		if (i > 0 && k.code[i - 1] != '\n') b.AppendLine();
		foreach (var v in namespaces) {
			if (v != null) b.Append("using ").Append(v).AppendLine(";");
		}

		k.sci.zInsertText(true, i, b.ToString(), addUndoPointAfter: true, restoreFolding: true);

		return true;

		//tested: CompilationUnitSyntax.AddUsings isn't better than this code. Does not skip existing. Does not add newlines. Does not skip comments etc. Did't test #directives.
	}

	/// <summary>
	/// Finds where new using directives can be inserted: end of existing using directives or 0 or end of extern aliases or #directives or meta or doc comments or 1 comments line.
	/// If namespaces!=null, clears existing namespaces in it (sets =null); if all cleared, returns -1.
	/// </summary>
	static int _FindUsingsInsertPos(CodeInfo.Context k, string[] namespaces = null) {
		//In namespaces clears elements that exist in e. If all cleared, sets namespaces=null and returns true.
		bool _ClearExistingUsings(IEnumerable<UsingDirectiveSyntax> e) {
			int n = namespaces.Count(o => o != null); //if (n == 0) return;
			foreach (var u in e) {
				int i = Array.IndexOf(namespaces, u.Name.ToString());
				if (i >= 0) {
					namespaces[i] = null;
					if (--n == 0) { namespaces = null; return true; }
				}
			}
			return false;
		}

		//at first look for "global using"
		var semo = k.semanticModel;
		if (namespaces != null && _ClearExistingUsings(CiUtil.GetAllGlobalUsings(semo))) return -1;

		int end = -1;
		var cu = k.syntaxRoot;

		//then look in current namespace, ancestor namespaces, compilation unit
		int pos = k.sci.zCurrentPos16;
		for (var node = cu.FindToken(pos).Parent; node != null; node = node.Parent) {
			SyntaxList<UsingDirectiveSyntax> usings; SyntaxList<ExternAliasDirectiveSyntax> externs;
			if (node is NamespaceDeclarationSyntax ns) {
				if (pos <= ns.OpenBraceToken.SpanStart || pos > ns.CloseBraceToken.SpanStart) continue;
				usings = ns.Usings; externs = ns.Externs;
			} else if (node is CompilationUnitSyntax) {
				usings = cu.Usings; externs = cu.Externs;
			} else continue;

			if (usings.Any()) {
				if (namespaces != null && _ClearExistingUsings(usings)) return -1;
				if (end < 0) end = usings[^1].FullSpan.End;
			} else if (externs.Any()) {
				if (end < 0) end = externs[^1].FullSpan.End;
			}
			if (end >= 0 && namespaces == null) break;
		}

		if (end < 0) { //insert at start but after #directives and certain comments
			int end2 = -1;
			foreach (var v in cu.GetLeadingTrivia()) if (v.IsDirective) end2 = v.FullSpan.End; //skip directives
			if (end2 < 0) {
				end2 = k.meta.end; //skip meta
				if (end2 == 0) if (k.code.RxMatch(@"(\s*///.*\R)+", 0, out RXGroup g1, RXFlags.ANCHORED, end2..)) end2 = g1.End; //skip ///comments
				if (k.code.RxMatch(@"\s*//.+\R", 0, out RXGroup g2, RXFlags.ANCHORED, end2..)) end2 = g2.End; //skip 1 line of //comments, because usually it is //. for folding
			}
			end = end2;
		}
		return end;
	}

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

		if (node is not MemberDeclarationSyntax) { //can be eg func return type (if no public etc) or attribute
			node = node.Parent;
			if (node is not MemberDeclarationSyntax || node.SpanStart != start) return;
		}
		if (node is GlobalStatementSyntax) return;

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
		var pl = node switch { BaseMethodDeclarationSyntax met => met.ParameterList, RecordDeclarationSyntax rec => rec.ParameterList, _ => null };
		if (pl != null) {
			var b = new StringBuilder(s);
			foreach (var p in pl.Parameters) {
				b.Append("\r\n/// <param name=\"").Append(p.Identifier.Text).Append("\"></param>");
			}
			if (node is MethodDeclarationSyntax mm) {
				var rt = mm.ReturnType;
				if (!code.Eq(rt.Span, "void")) b.Append("\r\n/// <returns></returns>");
			}
			s = b.ToString();
			//rejected: <typeparam name="TT"></typeparam>. Rarely used.
		}

		s = InsertCodeUtil.IndentStringForInsertSimple(s, doc, pos);

		doc.zInsertText(true, pos, s, true, true);
		doc.zGoToPos(true, pos + s.Find("/ ") + 2);
	}

	public static void AddFileDescription() {
		var doc = Panels.Editor.ZActiveDoc; if (doc == null) return;
		doc.zInsertText(false, 0, "/// Description\r\n\r\n");
		doc.zSelect(false, 4, 15, makeVisible: true);
	}

	public static void AddClassProgram() {
		if (!CodeInfo.GetContextAndDocument(out var cd) /*|| !cd.sci.ZFile.IsScript*/) return;
		int start, end = cd.code.Length;
		var members = cd.syntaxRoot.Members;
		if (members.Any()) {
			start = _FindRealStart(false, members[0]);
			if (members[0] is not GlobalStatementSyntax) end = start;
			else if (members.FirstOrDefault(v => v is not GlobalStatementSyntax) is SyntaxNode sn) end = _FindRealStart(true, sn);

			int _FindRealStart(bool needEnd, SyntaxNode sn) {
				int start = sn.SpanStart;
				//find first empty line in comments before
				var t = sn.GetLeadingTrivia();
				for (int i = t.Count; --i >= 0;) {
					var v = t[i];
					int ss = v.SpanStart;
					if (ss < cd.meta.end) break;
					//if (needEnd) { print.it($"{v.Kind()}, '{v}'"); continue; }
					var k = v.Kind();
					if (k == SyntaxKind.EndOfLineTrivia) {
						while (i > 0 && t[i - 1].IsKind(SyntaxKind.WhitespaceTrivia)) i--;
						if (i == 0 || t[i - 1].IsKind(k)) return needEnd ? ss : v.Span.End;
					} else if (k == SyntaxKind.SingleLineCommentTrivia) {
						if (cd.code.Eq(ss, "//.") && char.IsWhiteSpace(cd.code[ss + 3])) break;
					} else if (k is not (SyntaxKind.WhitespaceTrivia or SyntaxKind.MultiLineCommentTrivia or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia)) {
						break; //eg #directive
					}
				}
				return start;
			}
		} else start = end;
		//CiUtil.HiliteRange(start, end); return;

		var before = """
class Program {
	static void Main(string[] a) => new Program(a);
	Program(string[] args) {

""";
		var after = @"
	}
}";
		Surround(start, end, before, after, indentPlus: 2);
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
			var b = new StringBuilder("<><Z #A0C0A0>Delegate method and lambda<>\r\n<code>");
			var m = t.DelegateInvokeMethod;

			//method

			var format = new SymbolDisplayFormat(
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

	public static void ImplementInterfaceOrAbstractClass(bool explicitly, int position = -1) {
		if (!CodeInfo.GetContextAndDocument(out var cd, position)) return;
		var semo = cd.semanticModel;
		var node = semo.Root.FindToken(cd.pos).Parent;
		//CiUtil.PrintNode(node);

		bool haveBaseType = false;
		for (var n = node; n != null; n = n.Parent) {
			//print.it(n.Kind());
			if (n is BaseTypeSyntax bts) {
				node = bts.Type;
				haveBaseType = true;
			} else if (n is TypeDeclarationSyntax tds) {
				if (!haveBaseType) try { node = tds.BaseList.Types[0].Type; } catch { return; }
				position = tds.CloseBraceToken.Span.Start;
				goto g1;
			}
		}
		return;
	g1:

		var baseType = semo.GetTypeInfo(node).Type as INamedTypeSymbol;
		if (baseType == null) return;
		bool isInterface = false;
		switch (baseType.TypeKind) {
		case TypeKind.Interface: isInterface = true; break;
		case TypeKind.Class when baseType.IsAbstract: break;
		default: return;
		}

		var b = new StringBuilder();
		var format = CiText.s_symbolFullFormat.WithParameterOptions(CiText.s_symbolFullFormat.ParameterOptions & ~SymbolDisplayParameterOptions.IncludeOptionalBrackets);
		var formatExp = format.WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef);

		b.Append("\r\n#region ").Append(baseType.ToMinimalDisplayString(semo, position, CiText.s_symbolFullFormat));

		if (isInterface) {
			_Base(baseType, explicitly);
			foreach (var bi in baseType.AllInterfaces) {
				_Base(bi,
					explicitly || (baseType.IsGenericType && !bi.IsGenericType) //eg public IEnumerable<T> and explicit IEnumerable
					);
			}
		} else {
			for (; baseType != null && baseType.IsAbstract; baseType = baseType.BaseType) {
				_Base(baseType, false);
			}
		}

		b.AppendLine("\r\n\r\n#endregion");

		void _Base(INamedTypeSymbol type, bool explicitly) {
			foreach (var v in type.GetMembers()) {
				//print.it(v, v.IsStatic, v.GetType().GetInterfaces());
				bool isAbstract = v.IsAbstract;
				if (!isAbstract && !isInterface) continue;
				if (v.IsStatic) continue;
				bool expl = explicitly || (isInterface && v.DeclaredAccessibility != acc.Public);

				string append = null;
				switch (v) {
				case IMethodSymbol ims when ims.MethodKind == MethodKind.Ordinary:
					append = ims.ReturnsVoid ? @" {
	
}" : @" {
	
	return default;
}";
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

				//if(null != classType.FindImplementationForInterfaceMember(v)) continue; //never mind

				b.AppendLine("\r\n");
				if (isInterface) {
					if (!isAbstract) b.AppendLine("//has default implementation");
					if (!expl) b.Append("public ");
				} else {
					b.Append(v.DeclaredAccessibility switch { acc.Public => "public", acc.Internal => "internal", acc.Protected => "protected", acc.ProtectedOrInternal => "protected internal", acc.ProtectedAndInternal => "private protected", _ => "" });
					b.Append(" override ");
				}
				b.Append(v.ToMinimalDisplayString(semo, position, expl ? formatExp : format)).Append(append);
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

		//print.it(text);
		//clipboard.text = text;

		cd.sci.zInsertText(true, position, text, addUndoPointAfter: true);
		cd.sci.zGoToPos(true, position);

		//tested: Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementInterfaceService works but the result is badly formatted (without spaces, etc). Internal, undocumented.
	}

	/// <summary>
	/// Called from Dicons.
	/// </summary>
	/// <param name="icon">Like "*Pack.Name #color".</param>
	public static void SetMenuToolbarItemIcon(string icon) {
		if (!CodeInfo.GetDocumentAndFindNode(out var cd, out var node, -2)) return;
		var semo = cd.semanticModel;

		//find nearest argumentlist and its method symbol
		BaseArgumentListSyntax arglist = null; IMethodSymbol method = null;
		var es = node.FirstAncestorOrSelf<ExpressionStatementSyntax>()?.Expression;
		if (es is InvocationExpressionSyntax ies) { //m.Add("name", "image"); or m.Submenu("name", , "image"); etc
			arglist = ies.ArgumentList;
			method = semo.GetSymbolInfo(ies.Expression).Symbol as IMethodSymbol;
		} else if (es is AssignmentExpressionSyntax aes && aes.Left is ElementAccessExpressionSyntax eaes) { //m["name", "image"] = ;
			arglist = eaes.ArgumentList;
			if (semo.GetSymbolInfo(eaes).Symbol is IPropertySymbol ips && ips.IsIndexer) method = ips.SetMethod;
		} else return;
		if (method == null) return;

		//get index of parameter of type MTImage
		var timage = semo.Compilation.GetTypeByMetadataName("Au.Types." + nameof(MTImage)); if (timage == null) return;
		int paramIndex = 0; string paramName = null;
		foreach (var p in method.Parameters) {
			if (p.Type == timage) { paramName = p.Name; break; }
			paramIndex++;
		}
		if (paramIndex == method.Parameters.Length) return;

		//find image argument
		ArgumentSyntax arg = null; int i = 0;
		foreach (var a in arglist.Arguments) {
			var nc = a.NameColon;
			if (nc != null) {
				if (nc.Name.Identifier.Text == paramName) arg = a;
			} else {
				if (i == paramIndex) arg = a;
			}
			if (arg != null) break;
			i++;
		}

		//replace or insert
		int replFrom, replTo; string prefix = null, suffix = null;
		if (arg != null) {
			var span = arg.Span;
			replFrom = span.Start;
			replTo = span.End;
		} else if (paramIndex < arglist.Arguments.Count) {
			replFrom = replTo = arglist.Arguments[paramIndex].SpanStart;
			suffix = ", ";
		} else {
			replFrom = replTo = arglist.Span.End - 1;
			if (arglist.Arguments.Count > 0) prefix = ", ";
		}
		icon = $"{prefix}{paramName}: \"{icon}\"{suffix}";
		//print.it(cd.pos, replFrom, replTo, icon);

		cd.sci.zReplaceRange(true, replFrom, replTo, icon);
	}

	//FUTURE: dialogs in SurroundFor and SurroundTryCatch. Eg to set variable name, count, whether to add finally.
	public static void SurroundFor() {
		var before = """
for (int i = 0; i < count; i++) {

""";
		var after = """

}

""";
		Surround(before, after, 1);
	}

	public static void SurroundTryCatch() {
		//SHOULDDO: If a statement is like 'var v = expression;', make 'Type v = null; try { v = expression; } catch {  }'.

		var before = """
try {

""";
		var after = """

}
catch (Exception) {  }

""";
		Surround(before, after, 1, concise: true);
	}
}
