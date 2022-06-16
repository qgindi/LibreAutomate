using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Indentation;
using Microsoft.CodeAnalysis.CSharp.Indentation;

static class ModifyCode {
	/// <summary>
	/// Comments out (adds // or /**/) or uncomments selected text or current line.
	/// </summary>
	/// <param name="comment">Comment out (true), uncomment (false) or toggle (null).</param>
	/// <param name="notSlashStar">Comment out lines, even if there is nut-full-line selection.</param>
	/// <param name="doc">If null, uses Panels.Editor.ZActiveDoc.</param>
	public static void CommentLines(bool? comment, bool notSlashStar = false) {
		//how to comment/uncomment: // or /**/
		if (!CodeInfo.GetContextAndDocument(out var cd, -2)) return;
		var doc = cd.sci;
		int selStart = cd.pos, selEnd = doc.zSelectionEnd16, replStart = selStart, replEnd = selEnd;
		bool com, slashStar = false, isSelection = selEnd > selStart;
		string code = cd.code, s = null;
		var root = cd.syntaxRoot;

		if (!notSlashStar) {
			var trivia = root.FindTrivia(selStart);
			if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) {
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
			//is the first line //comments ?
			var trivia = root.FindTrivia(replStart);
			if (trivia.IsKind(SyntaxKind.WhitespaceTrivia)) trivia = root.FindTrivia(trivia.Span.End);
			if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)) {
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

		bool caretAtEnd = isSelection && doc.zCurrentPos16 == selEnd;
		doc.zReplaceRange(true, replStart, replEnd, s);
		if (isSelection) {
			int i = replStart, j = replStart + s.Length;
			doc.zSelect(true, caretAtEnd ? i : j, caretAtEnd ? j : i);
		}
	}

	public static void Format(bool selection) {
		if (!CodeInfo.GetContextAndDocument(out var cd, metaToo: true)) return;

		var doc = cd.sci;
		int from, to;
		if (selection) {
			from = doc.zSelectionStart16;
			to = doc.zSelectionEnd16;
			if (from == to) {
				var node = CiUtil.GetStatementEtcFromPos(cd, from);
				if (node == null) return;
				(from, to) = node.FullSpan;
			}
		} else {
			from = 0;
			to = cd.code.Length;
			if (to == 0) return;
		}
		int from0 = from, tail = cd.code.Length - to;

		string s = Format(cd, ref from, ref to);
		//var c = "code"; print.it($"<><{c}>{s}</{c}>");

		if (s == null) return;
		if (selection) {
			doc.ZReplaceTextGently(s, from..to);
			doc.zSelect(true, from0, doc.zLen16 - tail);
		} else {
			doc.ZReplaceTextGently(s);
		}
	}

	public static string Format(CodeInfo.Context cd, ref int from, ref int to) {
		//perf.first();
		string code = cd.code;

		//exclude newline at the end. Else formats entire leading trivia of next statement.
		if (to < code.Length) {
			if (to - from > 0 && code[to - 1] == '\n') to--;
			if (to - from > 0 && code[to - 1] == '\r') to--;
			if (to - from == 0) return null;
		}

		//include empty lines at the start, because would format anyway
		if (from > 0) {
			bool nlStart = false; int from0 = from;
			while (from > 0 && code[from - 1] is '\r' or '\n' or '\t' or ' ') if (code[--from] is '\r' or '\n') nlStart = true;
			if (nlStart) {
				if (code[from] == '\r') from++;
				if (from < from0 && code[from] == '\n') from++;
			}
		}
		//CiUtil.HiliteRange(from, to);

		//workarounds for some nasty Roslyn features that can't be changed with options:
		//	Removes tabs from empty lines.
		//	Indents //comment after code//comment.
		//The best way would be to modify Roslyn code. Fastest/cleanest code. Difficult.
		//Another way - fix formatted code. Not 100% reliable.
		//Chosen way - before formatting, in empty lines add a marked block comment. Finally remove. The same in lines containing only //comment.

		var root = cd.syntaxRoot;
		const string c_mark1 = "/*\a\b*/";
		int fix1 = code.RxReplace(@"(?m)^\h*\K(?=\R|//)", c_mark1, out code, range: from..to);
		if (fix1 > 0) {
			to += code.Length - cd.code.Length;
			//print.it($"'{code[from..to]}'");
			root = root.SyntaxTree.WithChangedText(SourceText.From(code)).GetCompilationUnitRoot();
		}
		//perf.next();

		//how to modify rules? But probably not useful.
		//var rules=Formatter.GetDefaultFormattingRules(cd.document);
		//foreach (var v in rules) {
		//	print.it(v);
		//	//if (v is AnchorIndentationFormattingRule d) print.it(d);
		//}

		var od = CSharpSyntaxFormattingOptions.Default;
		var options = new CSharpSyntaxFormattingOptions(
			useTabs: true,
			tabSize: 4,
			indentationSize: 4,
			newLine: "\r\n",
			separateImportDirectiveGroups: false,
			spacing: od.Spacing,
			spacingAroundBinaryOperator: od.SpacingAroundBinaryOperator,
			newLines: NewLinePlacement.BeforeCatch | NewLinePlacement.BeforeFinally | NewLinePlacement.BetweenQueryExpressionClauses,
			labelPositioning: LabelPositionOptions.NoIndent,
			indentation: IndentationPlacement.BlockContents | IndentationPlacement.SwitchCaseContents | IndentationPlacement.SwitchCaseContentsWhenBlock,
			wrappingKeepStatementsOnSingleLine: true,
			wrappingPreserveSingleLine: true);

		var span = TextSpan.FromBounds(from, to);
		var services = cd.document.Project.Solution.Workspace.Services;
		var a1 = Formatter.GetFormattedTextChanges(root, span, services, options);
		//perf.next();

		string code2 = code;
		bool replaced = false;
		if (a1.Count > 0) {
			var b = new StringBuilder();
			int i1 = 0;
			foreach (var v in a1) {
				int ss = v.Span.Start, se = v.Span.End;
				//Debug_.PrintIf(ss < from || se > to, $"from: {from}, ss: {ss},  to: {to}, se: {se},  v: {v}");
				if (ss < from || se > to || code.Eq(v.Span.ToRange(), v.NewText)) continue;
				if (se - ss == 1 && code[ss] == ' ' && v.NewText == "" && code.Eq((ss - 2)..(ss + 2), "{  }")) continue; //don't replace "{  }" with "{ }"
				b.Append(code, i1, ss - i1);
				b.Append(v.NewText);
				i1 = v.Span.End;
				replaced = true;
			}

			if (replaced) {
				b.Append(code, i1, code.Length - i1);
				code = b.ToString();
			}
		}
		if (!replaced) return null;

		var ret = code[from..(to + (code.Length - code2.Length))];

		if (fix1 > 0) {
			var r1 = ret.Replace(c_mark1, "");
			to -= ret.Length - r1.Length;
			ret = r1;
		}

		//perf.nw();
		return ret;
	}
}

//gets 0 for empty lines. OK for //comments.
//var ind = Microsoft.CodeAnalysis.CSharp.Indentation.CSharpIndentationService.Instance;
//var ir=ind.GetIndentation(cd.document, 73, FormattingOptions.IndentStyle.Smart, default);
//print.it(ir.BasePosition, ir.Offset);


partial class SciCode {
	/// <summary>
	/// Replaces text without losing markers, expanding folded code, etc.
	/// </summary>
	public void ZReplaceTextGently(string s, Range? range = null) {
		var (rFrom, rTo) = range.GetStartEnd(zLen16);
		int len = s.Lenn(); if (len == 0) goto gRaw;
		string old = range.HasValue ? zRangeText(true, rFrom, rTo) : zText;
		if (len > 1_000_000 || old.Length > 1_000_000 || old.Length == 0) goto gRaw;
		var dmp = new DiffMatchPatch.diff_match_patch();
		var a = dmp.diff_main(old, s, true); //the slowest part. Timeout 1 s; then a valid but smaller.
		if (a.Count > 1000) goto gRaw;
		dmp.diff_cleanupEfficiency(a);
		using (new SciCode.UndoAction(this)) {
			for (int i = a.Count - 1, j = old.Length; i >= 0; i--) {
				var d = a[i];
				if (d.operation == DiffMatchPatch.Operation.INSERT) {
					zInsertText(true, j + rFrom, d.text);
				} else {
					j -= d.text.Length;
					if (d.operation == DiffMatchPatch.Operation.DELETE)
						zDeleteRange(true, j + rFrom, j + d.text.Length + rFrom);
				}
			}
		}
		return;
	gRaw:
		if (range.HasValue) zReplaceRange(true, rFrom, rTo, s);
		else zText = s;

		//never mind: then Undo sets position at the first replaced part (in the document it's the last, because replaces in reverse order).
		//	And then Redo sets position at the last replaced part.
		//	Could try SCI_ADDUNDOACTION.
	}
}
