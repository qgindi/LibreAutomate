extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using CAW::Microsoft.CodeAnalysis.Shared.Utilities;
using CAW::Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using CAW::Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp.Formatting;

static class ModifyCode {
	//SHOULDDO: if tries to uncomment /// doc comment, remove /// . Now does nothing. VS removes // and leaves /.
	//	Only if <code>.
	
	/// <summary>
	/// Comments out (adds // or /**/) or uncomments selected text or current line.
	/// </summary>
	/// <param name="comment">Comment out (true), uncomment (false) or toggle (null).</param>
	/// <param name="notSlashStar">Comment out lines, even if there is not-full-line selection.</param>
	public static void Comment(bool? comment, bool notSlashStar = false) {
		//how to comment/uncomment: // or /**/
		if (!CodeInfo.GetContextAndDocument(out var cd, -2)) return;
		var doc = cd.sci;
		int selStart = cd.pos, selEnd = doc.aaaSelectionEnd16, replStart = selStart, replEnd = selEnd;
		bool com, slashStar = false, isSelection = selEnd > selStart;
		string code = cd.code, s = null;
		var root = cd.syntaxRoot;
		
		if (!notSlashStar) {
			var trivia = root.FindTrivia(selStart);
			if (trivia.IsMultiLineComment()) {
				var span = trivia.Span;
				if (slashStar = comment != true && selEnd <= trivia.Span.End) {
					com = false;
					replStart = span.Start; replEnd = span.End;
					s = code[(replStart + 2)..(replEnd - 2)];
				} else notSlashStar = true;
			}
		}
		
		if (!slashStar) {
			//get the start and end of lines containing selection
			while (replStart > 0 && code[replStart - 1] != '\n') replStart--;
			if (!(replEnd > replStart && code[replEnd - 1] == '\n')) {
				while (replEnd < code.Length && code[replEnd] is not ('\r' or '\n')) replEnd++;
				if (replEnd > replStart && code.Eq(replEnd, "\r\n")) replEnd += 2; //prevent on Undo moving the caret to the end of line and possibly hscrolling
			}
			if (replEnd == replStart) return;
			
			//are all lines //comments ?
			bool allLinesComments = true;
			for (int i = replStart; i < replEnd; i++) {
				var trivia = root.FindTrivia(i);
				if (trivia.IsWhitespace()) trivia = root.FindTrivia(i = trivia.Span.End);
				if (trivia.Kind() is not (SyntaxKind.SingleLineCommentTrivia or SyntaxKind.EndOfLineTrivia)) { allLinesComments = false; break; }
				i = code.IndexOf('\n', i, replEnd - i); if (i < 0) break;
			}
			if (allLinesComments) {
				com = comment ?? false;
			} else {
				if (comment == false) return;
				com = true;
				slashStar = isSelection && !notSlashStar && (selStart > replStart || selEnd < replEnd);
				if (slashStar) { replStart = selStart; replEnd = selEnd; }
			}
			
			s = code[replStart..replEnd];
			if (slashStar) {
				s = "/*" + s + "*/";
			} else {
				if (com) {
					s.RxFindAll(@"(?m)^[\t ]*(.*)\R?", out RXMatch[] a);
					//find smallest common indent
					int indent = 0; //tabs*4 or spaces*1
					foreach (var m in a) {
						if (m[1].Length == 0) continue;
						int n = 0; for (int i = m.Start; i < m[1].Start; i++) if (s[i] == ' ') n++; else n = (n & ~3) + 4;
						indent = indent == 0 ? n : Math.Min(indent, n);
						if (indent == 0) break;
					}
					//insert // in lines containing code
					var b = new StringBuilder();
					foreach (var m in a) {
						if (m[1].Length == 0) {
							b.Append(s, m.Start, m.Length);
						} else {
							int i = m.Start; for (int n = 0; n < indent; i++) if (s[i] == ' ') n++; else n = (n & ~3) + 4;
							b.Append(s, m.Start, i - m.Start).Append("//").Append(s, i, m.End - i);
						}
					}
					s = b.ToString();
				} else { //remove single // from all lines
					s = s.RxReplace(@"(?m)^([ \t]*)//", "$1");
				}
			}
		}
		
		bool caretAtEnd = isSelection && doc.aaaCurrentPos16 == selEnd;
		doc.aaaReplaceRange(true, replStart, replEnd, s);
		if (isSelection) {
			int i = replStart, j = replStart + s.Length;
			doc.aaaSelect(true, caretAtEnd ? i : j, caretAtEnd ? j : i);
		}
	}
	
	//public static string Format(ref int from, ref int to) {
	//	if (!CodeInfo.GetContextAndDocument(out var cd, from, metaToo: true)) return null;
	//	return Format(cd, ref from, ref to);
	//}
	
	public static void Format(bool selection) {
		if (!CodeInfo.GetContextAndDocument(out var cd, -2, metaToo: true)) return;
		
		var doc = cd.sci;
		int from, to, selStart, selEnd;
		if (selection) {
			selStart = from = cd.pos;
			selEnd = to = doc.aaaSelectionEnd16;
			if (from == to) {
				var node = CiUtil.GetStatementEtcFromPos(cd, from);
				if (node == null) return;
				(from, to) = node.FullSpan;
			}
		} else {
			from = 0;
			to = cd.code.Length;
			if (to == 0) return;
			selStart = selEnd = -1;
		}
		int from0 = from;
		
		string s = Format(cd, ref from, ref to, ref selStart, ref selEnd);
		if (s == null) return;
		if (selection) {
			doc.EReplaceTextGently(s, from..to);
			doc.aaaSelect(true, selStart, selEnd);
		} else {
			doc.EReplaceTextGently(s);
		}
	}
	
	/// <summary>
	/// Formats text of the specified range.
	/// </summary>
	/// <param name="cd"></param>
	/// <param name="from">Start of range. The function may adjust it to span horizontal whitespace before.</param>
	/// <param name="to">End of range. The function may adjust it to exclude newline after.</param>
	/// <param name="selStart">Selection start. The function adjusts it to match the formatted text. Can be -1 if don't need.</param>
	/// <param name="selEnd">Selection end. The function adjusts it to match the formatted text. Can be -1 if don't need. Can be the same variable as <i>selStart</i>.</param>
	/// <returns>Formatted text of the final range.</returns>
	public static string Format(CodeInfo.Context cd, ref int from, ref int to, ref int selStart, ref int selEnd) {
		string code = cd.code;
		
		//exclude newline at the end. Else formats entire leading trivia of next token.
		if (to < code.Length) {
			if (to - from > 0 && code[to - 1] == '\n') to--;
			if (to - from > 0 && code[to - 1] == '\r') to--;
			if (to == from) return null;
		}
		
		//include whitespace before. Else _Format can't detect \r\n before when indented.
		while (from > 0 && code[from - 1] is '\t' or ' ') from--;
		
		if (!_Format(cd, from, to, out var a)) return null;
		
		StringBuilder b = null;
		int i1 = from, caret1 = selStart, caret2 = selEnd, moveCaret1 = 0, moveCaret2 = 0;
		foreach (var v in a) {
			string newText = v.NewText;
			int ss = v.Span.Start, se = v.Span.End;
			if (ss < from) {
				if (se < from || se > to || newText.NE()) continue;
				//probably previous line is blank with indentation, and formatter tries to remove that indentation, eg replace "\t\r\n" with "\r\n\t"
				int n = newText.LastIndexOf('\n') + 1; if (n == 0) continue;
				newText = newText[n..];
				ss = from;
				if (code.Eq(ss..se, newText)) continue;
			} else {
				if (se > to || code.Eq(v.Span, newText)) continue;
			}
			
			b ??= new();
			b.Append(code, i1, ss - i1);
			b.Append(newText);
			i1 = se;
			
			_Caret(caret1, ref moveCaret1);
			_Caret(caret2, ref moveCaret2);
			
			void _Caret(int caret, ref int moveCaret) {
				if (caret >= se) moveCaret += ss - se + newText.Length;
				else if (caret > ss) moveCaret += ss - caret + newText.Length;
			}
		}
		
		if (b == null) return null;
		b.Append(code, i1, to - i1);
		
		caret1 += moveCaret1;
		if (caret2 >= 0) caret2 = Math.Max(caret2 + moveCaret2, caret1);
		selStart = caret1;
		selEnd = caret2;
		
		return b.ToString();
	}
	
	static bool _Format(CodeInfo.Context cd, int from, int to, out IList<TextChange> ac, string code = null) {
		bool changedCode = code != null;
		code ??= cd.code;
		var root = cd.syntaxRoot;
		
		//workaround for some nasty Roslyn features that can't be changed with options:
		//	Removes tabs from empty lines.
		//	If next line after code//comment1 is //comment2, aligns //comment2 with //comment1.
		//Before formatting, in blank lines add a marker (doc comment). The same in lines containing only //comment.
		//Other ways: 1. Modify Roslyn code; too difficult etc. 2. Fix formatted code; not 100% reliable.
		const string c_mark = "///\a\b"; const int c_markLen = 5;
		int nw = code.RxReplace(@"(?m)^\h*\K(?=\R|//(?!/(?!/)))", c_mark, out code, range: from..to);
		if (nw > 0 || changedCode) {
			root = root.SyntaxTree.WithChangedText(SourceText.From(code)).GetCompilationUnitRoot();
			if (root.GetText().Length != code.Length) { Debug_.Print("bad new code"); ac = null; return false; }
		}
		
		//include \r\n before. Else may not correct indentation.
		//	never mind: then formats all trivia before. We'll skip it.
		if (from > 0 && code[from - 1] == '\n') from--;
		if (from > 0 && code[from - 1] == '\r') from--;
		//never mind: does not add space before statement when *from* is after eg `{` or `}` or `;`.
		//	VS adds space or newline when using "Format selection", but not when auto-format.
		
		try { ac = Formatter.GetFormattedTextChanges(root, TextSpan.FromBounds(from, to + nw * c_markLen), cd.document.Project.Solution.Services, FormattingOptions); }
		catch (Exception e1) { Debug_.Print(e1); ac = null; return false; } //https://www.libreautomate.com/forum/showthread.php?tid=7622
		if (ac.Count == 0) return false;
		
		//part 2 of the workaround. Remove marker traces from ac. Then ac will match the original code (the caller's version).
		if (nw > 0) {
			//_PrintFormattingTextChanges("BEFORE", code, ac);
			
			var aInserted = new int[nw];
			for (int i = 0, j = 0; i < nw; j += c_markLen) aInserted[i++] = j = code.Find("///\a\b", j);
			
			for (int i = 0; i < ac.Count; i++) {
				var v = ac[i];
				
				//some changes contain marks
				var s = v.NewText; int lenRemoved = 0;
				if (s.Length >= c_markLen) { s = s.Replace(c_mark, null); lenRemoved = v.NewText.Length - s.Length; }
				
				int startOfChange = v.Span.Start, nInsertedBefore = 0;
				while (nInsertedBefore < nw && aInserted[nInsertedBefore] < startOfChange) nInsertedBefore++;
				
				if (nInsertedBefore > 0 || lenRemoved > 0) ac[i] = new(new(startOfChange - nInsertedBefore * c_markLen, v.Span.Length - lenRemoved), s);
			}
			
			//_PrintFormattingTextChanges("AFTER", code, ac);
		}
		
		return true;
		
		//BAD: Roslyn does not format multiline collection initializers.
		//	https://github.com/dotnet/roslyn/issues/8269
	}
	
#if DEBUG
	static void _PrintFormattingTextChanges(string header, string code, IList<TextChange> a) {
		print.it("----", header);
		foreach (var v in a) {
			var color = code.AsSpan(v.Span.ToRange()).Eq(v.NewText) ? "gray" : "green";
			print.it($"<><c {color}><\a>{v}</\a><>");
		}
	}
#endif
	
	/// <summary>
	/// Formats code for inserting in current document at <i>start</i>.
	/// If end &gt; <i>start</i> - for replacing text at <c>start..end</c>.
	/// May decrease <i>start</i>; does it before calling <i>changes</i>.
	/// </summary>
	/// <returns><c>true</c> if formatted.</returns>
	public static bool FormatForInsert(ref string s, ref int start, int end, Action<IList<TextChange>> changes = null) {
		if (s.NE()) return false;
		if (!CodeInfo.GetContextAndDocument(out var cd, start, metaToo: true)) return false;
		
		var code = cd.code.ReplaceAt(start..end, s);
		int end2 = start + s.Length;
		int start2 = start; while (start2 > 0 && cd.code[start2 - 1] is '\t' or ' ') start2--; //include whitespace before. Else _Format can't detect \r\n before when indented.
		
		if (!_Format(cd, start2, end2, out var a, code)) return false;
		
		for (int i = a.Count; --i >= 0;) {
			var v = a[i];
			int ss = v.Span.Start, se = v.Span.End;
			if (ss < start2 && se >= start2 && se < end2
				&& !(se == start2 && v.NewText == " " && cd.code.Eq(se - 1, '\n')) //formatter bug: in TLS replaces `A\n{B` with `A {B`
				) {
				int n = v.NewText.AsSpan().TrimEnd("\t ").Length;
				a[i] = new(TextSpan.FromBounds(start2, se), v.NewText[n..]);
			} else {
				if (ss < start2 || se > end2 || code.Eq(ss..se, v.NewText)) a.RemoveAt(i);
				else if (v.NewText.NE() && se - ss == 1 && code.Eq((ss - 2)..(ss + 2), "{  }")) a.RemoveAt(i); //don't replace `{  }` with `{ }` eg in snippet
			}
		}
		if (a.Count == 0) return false;
		
		//_PrintFormattingTextChanges("CHANGES", code, a);
		
		start = start2;
		if (changes != null) {
			//if (start2 < start) s = string.Concat(cd.code.AsSpan(start2..start), s); //currently not used
			changes(a);
		}
		
		var b = new StringBuilder();
		int k = start;
		foreach (var v in a) {
			b.Append(code, k, v.Span.Start - k).Append(v.NewText);
			k = v.Span.End;
		}
		b.Append(code, k, end2 - k);
		s = b.ToString();
		
		return true;
	}
	
	/// <summary>
	/// Can be replaced by editorExtension scripts.
	/// </summary>
	public static CSharpSyntaxFormattingOptions FormattingOptions {
		get {
			if (_formattingOptions == null) {
				switch (App.Settings.ci_formatCompact, App.Settings.ci_formatTabIndent) {
				case (true, true): //default in this program
					_formattingOptions = new() {
						LineFormatting = new() { UseTabs = true },
						Indentation = IndentationPlacement.BlockContents | IndentationPlacement.SwitchCaseContents | IndentationPlacement.SwitchCaseContentsWhenBlock,
						NewLines = NewLinePlacement.BeforeCatch | NewLinePlacement.BeforeFinally | NewLinePlacement.BetweenQueryExpressionClauses,
						LabelPositioning = LabelPositionOptions.NoIndent,
					};
					break;
				case (false, true):
					_formattingOptions = new() {
						LineFormatting = new() { UseTabs = true },
					};
					break;
				case (true, false):
					_formattingOptions = new() {
						Indentation = IndentationPlacement.BlockContents | IndentationPlacement.SwitchCaseContents | IndentationPlacement.SwitchCaseContentsWhenBlock,
						NewLines = NewLinePlacement.BeforeCatch | NewLinePlacement.BeforeFinally | NewLinePlacement.BetweenQueryExpressionClauses,
						LabelPositioning = LabelPositionOptions.NoIndent,
					};
					break;
				default:
					_formattingOptions = CSharpSyntaxFormattingOptions.Default;
					break;
				}
			}
			return _formattingOptions;
		}
		set { _formattingOptions = value; }
	}
	static CSharpSyntaxFormattingOptions _formattingOptions;
	
	public static void CleanupWndFind() {
		if (!CodeInfo.GetContextAndDocument(out var cd, metaToo: true)) return;
		var doc = cd.sci;
		
		int from, to;
		if (doc.aaaHasSelection) {
			from = doc.aaaSelectionStart16; to = doc.aaaSelectionEnd16;
		} else {
			from = 0; to = cd.code.Length;
		}
		
		Dictionary<string, List<LocalDeclarationStatementSyntax>> d = new();
		
		foreach (var n in cd.syntaxRoot.DescendantNodes(TextSpan.FromBounds(from, to))) {
			if (n is not LocalDeclarationStatementSyntax lds) continue;
			var ds = lds.Declaration;
			if (!(ds.Type.ToString() is "var" or "wnd" or "Au.wnd")) continue;
			if (ds.Variables.Count != 1) continue;
			var ds2 = ds.Variables[0];
			if (ds2.Initializer?.Value is not InvocationExpressionSyntax ies) continue;
			if (ies.Expression.ToString() != "wnd.find") {
				//maybe wnd.find(...).Activate()
				if (!ies.ToString().Like("wnd.find(*).Activate()")) continue;
				if (ies.Expression is not MemberAccessExpressionSyntax mas) continue;
				ies = mas.Expression as InvocationExpressionSyntax;
				if (ies?.Expression.ToString() != "wnd.find") continue;
			}
			var a = ies.ArgumentList.Arguments;
			if (a.Count > 0) {
				var k0 = a[0].Expression.Kind();
				if (k0 is SyntaxKind.NumericLiteralExpression || (a[0].Expression is PrefixUnaryExpressionSyntax pues && pues.Operand.Kind() is SyntaxKind.NumericLiteralExpression)) a = a.RemoveAt(0);
				else if (!(k0 is SyntaxKind.StringLiteralExpression or SyntaxKind.NullLiteralExpression)) continue; //probably Seconds
			}
			if (a.Count == 0) continue;
			var s = a.ToString();
			if (d.TryGetValue(s, out var list)) list.Add(lds); else d[s] = new() { lds };
			
			//CONSIDER: if found with similar name (eg "Doc1 - App" -> "Doc2 - App"), ask whether to remove these too. Eg UI with checkboxes.
			//	Also the UI should offer to replace these with `w.WaitForName`.
			//	But maybe in many cases the user would do it manually faster than with all this confusing UI. Or just leave it as is.
		}
		
		List<(int start, int end, string repl)> aRepl = new();
		foreach (var (k, a) in d) {
			if (a.Count < 2) continue;
			static SyntaxNode _Scope(SyntaxNode n) {
				n = n.Parent;
				if (n is GlobalStatementSyntax) n = n.Parent;
				return n;
			}
			g1:
			var ds0 = a[0];
			var repl = ds0.Declaration.Variables[0].Identifier.Text;
			var scope0 = _Scope(ds0);
			a.RemoveAt(0);
			for (int i = 0; i < a.Count; i++) {
				var lds = a[i];
				var scope = _Scope(lds);
				if (scope != scope0) {
					bool ok = false;
					foreach (var v in scope.Ancestors()) {
						if (ok = v == scope0) break;
						if (v is MemberDeclarationSyntax && v is not GlobalStatementSyntax) break;
						if (v is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) break;
					}
					if (!ok) continue;
				}
				
				//remove the duplicate wnd.find statement
				if (lds.Declaration.Variables[0].Initializer.Value is InvocationExpressionSyntax ies && ies.Expression is MemberAccessExpressionSyntax mas && mas.Name.Identifier.Text == "Activate") { //.Activate()
					var span = lds.Span;
					aRepl.Add((span.Start, mas.Expression.Span.End, repl));
				} else {
					var span = lds.GetRealFullSpan(true);
					aRepl.Add((span.Start, span.End, ""));
				}
				a.RemoveAt(i--);
				
				//find references of the variable declared in the duplicate wnd.find statement
				if (cd.semanticModel.GetDeclaredSymbol(lds.Declaration.Variables[0]) is ISymbol sym) {
					if (SymbolFinder.FindReferencesAsync(sym, cd.document.Project.Solution, [cd.document]).Result.SingleOrDefault() is { } rs) {
						foreach (var loc in rs.Locations) {
							var span = loc.Location.SourceSpan;
							aRepl.Add((span.Start, span.End, repl));
						}
					}
				}
			}
			if (a.Count > 1) goto g1;
		}
		
		if (aRepl.Count == 0) {
			dialog.showInfo("Deduplicate wnd.find", $"No duplicate wnd.find statements found{(doc.aaaHasSelection ? " in the selected text" : "")}.", owner: doc.AaWnd, secondsTimeout: 5);
			return;
		}
		
		doc.EInicatorsFound_(aRepl.Select(o => o.start..o.end).ToList());
		wait.doEvents(1000);
		if (doc != Panels.Editor.ActiveDoc || doc.aaaText != cd.code) return;
		using (new SciCode.aaaUndoAction(doc)) {
			foreach (var v in aRepl.OrderByDescending(o => o.end)) {
				if (v.start > to) continue; to = v.start; //a variable in a replaced statement
				doc.aaaReplaceRange(true, v.start, v.end, v.repl);
			}
		}
	}
}


partial class SciCode {
	/// <summary>
	/// Replaces text without losing markers, expanding folded code, etc.
	/// </summary>
	public void EReplaceTextGently(string s, Range? range = null) {
		var (rFrom, rTo) = range.GetStartEnd(aaaLen16);
		int len = s.Lenn(); if (len == 0) goto gRaw;
		string old = range.HasValue ? aaaRangeText(true, rFrom, rTo) : aaaText;
		if (len > 1_000_000 || old.Length > 1_000_000 || old.Length == 0) goto gRaw;
		var dmp = new DiffMatchPatch.diff_match_patch();
		var a = dmp.diff_main(old, s, true); //the slowest part. Timeout 1 s; then a valid but smaller.
		if (a.Count > 1000) goto gRaw;
		dmp.diff_cleanupEfficiency(a);
		using (new SciCode.aaaUndoAction(this)) {
			for (int i = a.Count - 1, j = old.Length; i >= 0; i--) {
				var d = a[i];
				if (d.operation == DiffMatchPatch.Operation.INSERT) {
					aaaInsertText(true, j + rFrom, d.text);
				} else {
					j -= d.text.Length;
					if (d.operation == DiffMatchPatch.Operation.DELETE)
						aaaDeleteRange(true, j + rFrom, j + d.text.Length + rFrom);
				}
			}
		}
		return;
		gRaw:
		if (range.HasValue) aaaReplaceRange(true, rFrom, rTo, s);
		else aaaText = s;
		
		//never mind: then Undo sets position at the first replaced part (in the document it's the last, because replaces in reverse order).
		//	And then Redo sets position at the last replaced part.
		//	Could try SCI_ADDUNDOACTION.
	}
}
