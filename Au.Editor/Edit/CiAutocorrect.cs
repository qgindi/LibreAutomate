extern alias CAW;

using System.Windows.Input;
using Au.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Indentation;
using CAW::Microsoft.CodeAnalysis.Indentation;

class CiAutocorrect {
	/// <summary>
	/// Call when added text with { } etc and want it behave like when the user types { etc.
	/// </summary>
	public void BracketsAdded(SciCode doc, int innerFrom, int innerTo, EBrackets operation) {
		var r = doc.ETempRanges_Add(this, innerFrom, innerTo);
		if (operation == EBrackets.NewExpression) r.OwnerData = "new";
		else r.OwnerData = "ac";
	}
	
	public enum EBrackets {
		/// <summary>
		/// The same as when the user types '(' etc and is auto-added ')' etc. The user can overtype the ')' with the same character or delete '()' with Backspace.
		/// </summary>
		Regular,
		
		/// <summary>
		/// Like Regular, but also the user can overtype entire empty '()' with '[' or '{'. Like always, is auto-added ']' or '}' and the final result is '[]' or '{}'.
		/// </summary>
		NewExpression,
	}
	
	/// <summary>
	/// Called on Enter, Ctrl/Shift+Enter, Ctrl+;, Tab, Backspace and Delete, before passing it to Scintilla. Won't pass if returns true.
	/// Enter: adds new line. If need, completes statement (if before `)` etc) and/or adds indentation. Always returns true.
	/// Ctrl/Shift+Enter: The same as above, but anywhere. The hotkey depends on App.Settings.ci_enterWith; if not the hotkey, can just add new line + indentation.
	/// Ctrl+;: Like SciBeforeCharAdded(';'), but anywhere; and inserts semicolon now.
	/// Tab: calls/returns SciBeforeCharAdded, which skips auto-added ')' etc.
	/// Backspace: If inside an empty temp range, selects the '()' etc to erase and returns false.
	/// Backspace: If in blank line before `}` line, deletes the blank line and adds new line after the block.
	/// Delete, Backspace: If after deleting newline would be tabs after caret, deletes newline with tabs and returns true.
	/// </summary>
	public bool SciBeforeKey(SciCode doc, KKey key, ModifierKeys mod) {
		if (key is KKey.Enter) {
			switch (mod) {
			case 0:
				_OnEnter(-1);
				return true;
			case ModifierKeys.Control:
				_OnEnter(0);
				return true;
			case ModifierKeys.Shift:
				_OnEnter(1);
				return true;
			case ModifierKeys.Control | ModifierKeys.Shift:
				_OnEnter(2);
				return true;
			}
		} else {
			switch ((key, mod)) {
			case (KKey.OemSemicolon, ModifierKeys.Control):
				_OnSemicolon(anywhere: true);
				return true;
			case (KKey.Back, 0):
				return _OnBackspaceOrDelete(doc, true) || SciBeforeCharAdded(doc, '\b');
			case (KKey.Delete, 0):
				return _OnBackspaceOrDelete(doc, false);
			case (KKey.Tab, 0):
				return SciBeforeCharAdded(doc, '\t');
			}
		}
		return false;
	}
	
	/// <summary>
	/// Called on WM_CHAR, before passing it to Scintilla. Won't pass if returns true. Not called if ch less than ' '.
	/// If ch is ')' etc, and at current position is ')' etc previously added on '(' etc, clears the temp range and returns true.
	/// If ch is ';' and current position is at '(...|)' and the statement etc must end with ';': adds ';' if missing, sets current position after ';', and returns true.
	/// If ch is '"' after two '"', may close raw string (add """") and return true.
	/// Also called by SciBeforeKey on Backspace and Tab. Then ch is '\b' or '\t'.
	/// </summary>
	public bool SciBeforeCharAdded(SciCode doc, char ch) {
		int pos8 = doc.aaaCurrentPos8;
		if (pos8 == doc.aaaLen8 && ch != '\b' && !CodeInfo._compl.IsVisibleUI) { //if pos8 is at the end of text, add newline
			if (doc.aaaHasSelection) {
				int i1 = doc.aaaSelectionStart8, i2 = doc.aaaSelectionEnd8;
				doc.aaaInsertText(false, pos8, "\r\n");
				doc.aaaSelect(false, i1, i2);
			} else doc.aaaInsertText(false, pos8, "\r\n");
		}
		
		if (doc.aaaHasSelection) return false;
		
		if (ch == ';') return _OnSemicolon(anywhere: false);
		
		bool isBackspace = false, isOpenBrac = false;
		
		switch (ch) {
		case '"' or '\'' or ')' or ']' or '}' or '>' or '\t': break; //skip auto-added char
		case '\b': isBackspace = true; break; //on Backspace delete auto-added char too
		case '[' or '{' or '(' or '<': isOpenBrac = true; break; //replace auto-added '()' when completing 'new Type' with '[]' or '{}'
		default: return false;
		}
		
		var r = doc.ETempRanges_Enum(pos8, this, endPosition: (ch == '"' || ch == '\''), utf8: true).FirstOrDefault();
		if (r == null) {
			if (ch == '"') return _RawString();
			return false;
		}
		if (isOpenBrac && (object)r.OwnerData is not ("ac" or "new")) return false;
		r.GetCurrentFromTo(out int from, out int to, utf8: true);
		
		if (isBackspace || isOpenBrac) {
			if (pos8 != from) return false;
		} else {
			if (ch != '\t' && ch != doc.aaaCharAt8(to)) { //info: '\0' if pos8 invalid
				if (ch == '"') return _RawString();
				return false;
			}
			if (ch == '\t' && doc.aaaCharAt8(pos8 - 1) is '\t' or '\n') return false; //don't exit temp range if pos8 is at the start of line
		}
		for (int i = pos8; i < to; i++) switch (doc.aaaCharAt8(i)) { case ' ' or '\r' or '\n' or '\t': break; default: return false; } //eg space before '}'
		
		//rejected: ignore user-typed '(' or '<' after auto-added '()' or '<>' by autocompletion. Probably more annoying than useful, because then may want to type (cast) or ()=>lambda or (tup, le).
		//if(isOpenBrac && (ch == '(' || ch == '<') && ch == doc.aaaCharAt(pos8 - 1)) {
		//	r.OwnerData = null;
		//	return true;
		//}
		if (isOpenBrac && r.OwnerData != (object)"new") return false;
		
		r.Remove();
		
		if (isBackspace || isOpenBrac) {
			doc.Call(Sci.SCI_SETSEL, pos8 - 1, to + 1); //select and pass to Scintilla, let it delete or overtype
			return false;
		}
		
		doc.aaaCurrentPos8 = to + 1;
		return true;
		
		bool _RawString() { //close raw string now if need. In SciCharAdded too late, code is invalid and cannot detect correctly.
			if (pos8 > 3 && doc.aaaCharAt8(pos8 - 1) == '"' && doc.aaaCharAt8(pos8 - 2) == '"' && doc.aaaCharAt8(pos8 - 3) != '@') {
				if (!CodeInfo.GetContextAndDocument(out var cd)) return false;
				var pos16 = cd.pos;
				var token = cd.syntaxRoot.FindToken(pos16 - 1);
				var tkind = token.Kind();
				if (tkind is SyntaxKind.StringLiteralToken or SyntaxKind.InterpolatedStringEndToken) {
					//if pos is at ""|, make """|""" and append ; if missing
					var span = token.Span;
					if (pos16 == span.End && (tkind == SyntaxKind.StringLiteralToken ? span.Length == 2 : token.Parent.Span.Length == 3)) {
						//never mind: does not work if $$"". Then code is already invalid, and can't detect correctly. VS ignores it too.
						doc.aaaInsertText(false, pos8, "\"");
						doc.aaaAddUndoPoint();
						var nt = token.GetNextToken(includeZeroWidth: true);
						bool semicolon = nt.IsKind(SyntaxKind.SemicolonToken) && nt.IsMissing;
						doc.aaaInsertText(false, pos8 + 1, semicolon ? "\"\"\";" : "\"\"\"");
						doc.aaaCurrentPos8 = pos8 + 1;
						return true;
					}
				} else if (tkind is SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken) {
					//if pos it at """|""", make """"|""""
					int q1 = 0, q2 = 0;
					for (int i = pos8; doc.aaaCharAt8(--i) == '"';) q1++;
					for (int i = pos8; doc.aaaCharAt8(i++) == '"';) q2++;
					if (q1 == q2 && q1 >= 3) {
						if (tkind is SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken && token.SpanStart != pos8 - q1) return false;
						doc.aaaInsertText(false, pos8, "\"");
						doc.aaaAddUndoPoint();
						doc.aaaInsertText(false, pos8 + 1, "\"");
						doc.aaaCurrentPos8 = pos8 + 1;
						return true;
					}
				}
			}
			return false;
		}
	}
	
	/// <summary>
	/// Called on SCN_CHARADDED.
	/// If ch is '(' etc, adds ')' etc. Similar with /*.
	/// At the start of line corrects indentation of '}' (or formats the block) or '{'. Removes that of '#'.
	/// Replaces code like 5s with 5.s(); etc.
	/// If ch is '/' in XML comments, may add the end tag.
	/// </summary>
	public void SciCharAdded(CodeInfo.CharContext c) {
		char ch = c.ch;
		
		if (ch is ';' or ':') {
			_AutoFormat(ch);
			return;
			//never mind: formats even if ';' added in a wrong place, eg `Func(...|...)`. In VS too.
		}
		
		string replaceText = ch switch { '"' => "\"", '\'' => "'", '(' => ")", '[' => "]", '{' => "}", '<' => ">", '*' => "*/", 's' or 't' or '}' or '#' or '/' => "", _ => null };
		if (replaceText == null) return;
		
		if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		string code = cd.code;
		int pos = cd.pos - 1; if (pos < 0) return;
		
		Debug.Assert(code[pos] == ch);
		if (code[pos] != ch) return;
		
		bool isBeforeWord = ch != '}' && cd.pos < code.Length && SyntaxFacts.IsIdentifierStartCharacter(code[cd.pos]); //usually user wants to enclose the word manually, unless typed '{' in interpolated string
		if (isBeforeWord && ch is not ('{' or '#' or '/')) return;
		
		var root = cd.syntaxRoot;
		//if(!root.ContainsDiagnostics) return; //no. Don't use errors. It can do more bad than good. Tested.
		
		if (ch == 's') { //when typed like 5s or 500ms, replace with 5.s(); or 500.ms();
			if (pos > 0 && code[pos - 1] == 'm') { pos--; replaceText = ".ms();"; } else replaceText = ".s();";
			if (_IsNumericLiteralStatement(out _)) {
				//never mind: should ignore if not int s/ms or double s. Error if eg long or double ms.
				c.doc.aaaReplaceRange(true, pos, cd.pos, replaceText, moveCurrentPos: true);
				c.ignoreChar = true;
			}
			return;
		}
		if (ch == 't') { //when typed like 5t, replace with for (int i = 0; i < 5; i++) {  }
			if (_IsNumericLiteralStatement(out int spanStart)) {
				var br = code.Eq(cd.pos, '{') ? null : "{  }";
				replaceText = $"for (int i = 0; i < {code[spanStart..pos]}; i++) {br}";
				c.doc.aaaReplaceRange(true, spanStart, cd.pos, replaceText);
				c.doc.aaaCurrentPos16 = spanStart + replaceText.Length - (br == null ? 0 : 2);
				c.ignoreChar = true;
			}
			return;
		}
		bool _IsNumericLiteralStatement(out int spanStart) {
			if (pos > 0 && code[pos - 1].IsAsciiDigit()) {
				var node = root.FindToken(pos - 1).Parent;
				if (node.IsKind(SyntaxKind.NumericLiteralExpression) && node.Parent is ExpressionStatementSyntax) {
					var span = node.Span;
					spanStart = span.Start;
					return span.Contains(pos - 1);
				}
			}
			spanStart = 0;
			return false;
		}
		
		int replaceLength = 0, tempRangeFrom = 0, tempRangeTo = 0, newPos = 0;
		
		if (ch == '#') { //#directive
			if (InsertCodeUtil.IsLineStart(code, pos, out int i) && i < pos) {
				if (root.FindTrivia(pos).IsDirective) {
					c.doc.aaaDeleteRange(true, i, pos);
				}
			}
			return;
		} else if (ch == '*') { /**/
			var trivia = root.FindTrivia(pos);
			if (!trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)) return;
			if (trivia.SpanStart != --pos) return;
		} else if (ch == '/') { //</tag>
			if (!(root.FindToken(pos, findInsideTrivia: true).Parent is XmlElementEndTagSyntax et && et.Name?.IsMissing != false && et.Parent is XmlElementSyntax e1)) return;
			replaceText = e1.StartTag.Name.ToString() + '>';
		} else {
			var token = root.FindToken(pos);
			var node = token.Parent;
			
			if (ch == '}') { //decrease indentation
				if (!token.IsKind(SyntaxKind.CloseBraceToken) || pos != token.SpanStart) return;
				if (App.Settings.ci_formatAuto) {
					if (node.Parent is var p && p is (StatementSyntax and not BlockSyntax) or (MemberDeclarationSyntax and not GlobalStatementSyntax) or AccessorDeclarationSyntax) node = p;
					if (node is StatementSyntax or MemberDeclarationSyntax or AccessorDeclarationSyntax) _AutoFormat(node, cd);
				} else if (InsertCodeUtil.IsLineStart(code, pos, out int i) && i > 1) {
					if (node is not (BlockSyntax or SwitchStatementSyntax or MemberDeclarationSyntax or AccessorListSyntax or InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax or SwitchExpressionSyntax or PropertyPatternClauseSyntax)) return;
					var sInd = _GetIndentation();
					if (!code.Eq(i..pos, sInd)) {
						c.doc.aaaReplaceRange(true, i, pos, sInd);
						c.ignoreChar = true;
					}
				}
				return;
			}
			
			string _GetIndentation() => new CiIndentation(cd, token.SpanStart, false, useDefaultOptions: true).ToString();
			//info: without useDefaultOptions would be `if (true)\r\n\t{  }` if not compact formatting. We need `if (true)\r\n{  }`.
			
			var kind = node.Kind();
			if (kind == SyntaxKind.InterpolatedStringText) {
				node = node.Parent;
				kind = node.Kind();
			}
			
			if (isBeforeWord && kind != SyntaxKind.Interpolation) return;
			
			var span = node.Span;
			if (span.Start > pos) return; // > if pos is in node's leading trivia, eg comments or #if-disabled block
			
			tempRangeFrom = tempRangeTo = cd.pos;
			if (ch == '\'') {
				if (kind != SyntaxKind.CharacterLiteralExpression || span.Start != pos) return;
			} else if (ch == '"') {
				bool isVerbatim, isInterpolated = false;
				switch (kind) {
				case SyntaxKind.StringLiteralExpression:
					isVerbatim = code[span.Start] == '@';
					break;
				case SyntaxKind.InterpolatedStringExpression:
					isInterpolated = true;
					isVerbatim = code[span.Start] == '@' || code[span.Start + 1] == '@';
					break;
				default: return;
				}
				if (span.Start != pos - (isVerbatim ? 1 : 0) - (isInterpolated ? 1 : 0)) return;
			} else {
				if (ch == '<' && !_IsGenericLessThan()) return; //can be operators
				switch (kind) {
				case SyntaxKind.CompilationUnit:
				case SyntaxKind.CharacterLiteralExpression:
				case SyntaxKind.StringLiteralExpression:
				case SyntaxKind.Utf8StringLiteralExpression:
					return;
				case SyntaxKind.InterpolatedStringExpression:
					//after next typed { in interpolated string remove } added after first {
					if (ch == '{' && code.Eq(pos - 1, "{{}") && c.doc.ETempRanges_Enum(cd.pos, this, endPosition: true).Any()) {
						replaceLength = 1;
						replaceText = null;
						tempRangeFrom = 0;
						break;
					}
					return;
				default:
					if (_IsInNonblankTrivia(node, pos)) return;
					if (ch == '{') {
						if (kind == SyntaxKind.Interpolation) { //add } or }} etc if need
							int n = 0;
							for (int i = pos; code[i] == '{'; i--) n++;
							if (n > 1) tempRangeFrom = 0; //raw string like $$"""...{{
							for (int i = cd.pos; code.Eq(i, '}'); i++) n--;
							if (n <= 0) return;
							if (n > 1) replaceText = new('}', n);
						} else {
							replaceText = "  }";
							if (pos > 0 && !char.IsWhiteSpace(code[pos - 1])) {
								replaceText = " {  }";
								tempRangeFrom++;
								cd.pos--; replaceLength = 1; //replace the '{' too
							} else if (InsertCodeUtil.IsLineStart(code, pos, out int i) && i >= 4
								&& token.IsKind(SyntaxKind.OpenBraceToken) && pos == token.SpanStart) {
								//if { at the start of line (and maybe after an indentation), correct indentation
								var sInd = _GetIndentation();
								if (!code.Eq(i..pos, sInd)) {
									replaceText = sInd + "{  }";
									tempRangeFrom = i + sInd.Length + 1;
									cd.pos = i; replaceLength = pos - i + 1;
								}
							}
							
							newPos = tempRangeFrom + 1;
						}
					}
					break;
				}
				
				bool _IsGenericLessThan() {
					if (kind is SyntaxKind.TypeParameterList or SyntaxKind.TypeArgumentList) return true;
					if (kind != SyntaxKind.LessThanExpression) return false;
					var tok2 = token.GetPreviousToken(); if (!tok2.IsKind(SyntaxKind.IdentifierToken)) return false;
					var semo = cd.semanticModel;
					var sa = semo.GetSymbolInfo(tok2).GetAllSymbols();
					if (!sa.IsEmpty) {
						foreach (var v in sa) {
							if (v is INamedTypeSymbol { IsGenericType: true } or IMethodSymbol { IsGenericMethod: true }) return true;
							//bad: if eg IList and IList<T> are available, GetSymbolInfo gets only IList. Then no '>' completion. The same in VS.
							//	OK if only IList<T> available. Methods OK.
						}
					}
					return false;
				}
			}
		}
		
		
		if (newPos > 0) { // `{ | }`
			using var undo = new SciCode.aaaUndoAction(cd.sci);
			c.doc.aaaReplaceRange(true, cd.pos, cd.pos + replaceLength, replaceText);
			c.doc.aaaCurrentPos16 = newPos;
			_AutoFormat(ch);
			//add temprange AFTER autoformatting. Else it may disappear.
			newPos = c.doc.aaaCurrentPos16;
			tempRangeFrom = newPos - 1;
			tempRangeTo = newPos + 1;
		} else {
			c.doc.aaaReplaceRange(true, cd.pos, cd.pos + replaceLength, replaceText, moveCurrentPos: ch is ';' or '/');
		}
		
		if (tempRangeFrom > 0) c.doc.ETempRanges_Add(this, tempRangeFrom, tempRangeTo);
		else c.ignoreChar = true;
	}
	
	//anywhere true when Ctrl+;.
	static bool _OnSemicolon(bool anywhere) {
		if (!CodeInfo.GetDocumentAndFindToken(out var cd, out var token)) return false;
		var node0 = token.Parent;
		
		if (!anywhere) {
			var tk1 = token.Kind();
			if (!(tk1 is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken
				&& node0 is BaseArgumentListSyntax or BaseParameterListSyntax or ParenthesizedExpressionSyntax or TupleExpressionSyntax or CollectionExpressionSyntax or DoStatementSyntax
				&& cd.pos == token.SpanStart)) return false;
		}
		
		var nodeStat = node0.GetStatementEtc(cd.pos);
		if (nodeStat == null) return false;
		
		var lastToken = nodeStat.GetLastToken(includeZeroWidth: true);
		var tk2 = lastToken.Kind();
		if (tk2 is SyntaxKind.SemicolonToken || (tk2 is SyntaxKind.CloseBraceToken && nodeStat is TypeDeclarationSyntax tds && tds.ParameterList != null)) {
			//CiUtil.PrintNode(nodeStat, printErrors: true);
			Debug_.PrintIf(nodeStat.ContainsSkippedText); //SHOULDDO: test, maybe it helps to detect code incorrectly interpreted by Roslyn
			if (nodeStat.GetDiagnostics().Any(o => o.Id is "CS1513" or "CS1026")) return false; //"} expected" or ") expected". Eg in `timer.after(1, _=>{print.it(1));` after `(1)`.
			if (lastToken.IsMissing) {
				_InsertNodeCompletionTextWithAutoformat(cd, nodeStat, ";", lastToken.GetPreviousToken().Span.End);
			} else {
				cd.sci.aaaGoToPos(true, lastToken.SpanStart + 1);
			}
		} else return false;
		
		return true;
		
		//rejected: make similar feature for '{'. On '{': `if (...|)` -> `if (...) { | }`. `Func(...|)` -> `Func(...) { | }`.
		//	Rarely used. Much work to detect all possible cases where `(...{ })` is valid. Eg lambda/anonymous func, `is`, `switch`, `with`, `new`, `new X`, `new X()`, `new X[]`.
	}
	
	static void _OnEnter(int mod) {
		var doc = Panels.Editor.ActiveDoc;
		using var undo = new SciCode.aaaUndoAction(doc);
		if (mod < 0 && doc.aaaHasSelection) doc.Call(Sci.SCI_DELETEBACK);
		if (!_OnEnter2(mod)) doc.Call(Sci.SCI_NEWLINE);
	}
	
	//mod: Ctrl 0, Shift 1, Ctrl+Shift 2, none -1.
	static bool _OnEnter2(int mod) {
		if (!CodeInfo.GetContextAndDocument(out var cd)) return false;
		var doc = cd.sci;
		var code = cd.code;
		int pos = cd.pos;
		if (pos < 1) return false;
		
		bool anywhere = mod == App.Settings.ci_enterWith, canCorrect = anywhere, isEOF = false;
		
		var tok1 = cd.syntaxRoot.FindToken(pos);
		if (tok1.IsKind(SyntaxKind.EndOfFileToken)) {
			if (!anywhere) if (1 == _InNonblankTriviaOrStringOrChar(cd, tok1)) return true;
			tok1 = tok1.GetPreviousToken();
			if (tok1.RawKind == 0) return false;
			isEOF = true;
		}
		var tk1 = tok1.Kind();
		SyntaxNode nodeFromPos = tok1.Parent;
		
		if (!anywhere) {
			if (tk1 is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken && tok1.SpanStart == pos) {
				if (!(code[pos - 1] is ',' or ' ' || App.Settings.ci_classicEnter || mod >= 0)) {
					canCorrect = nodeFromPos is BaseArgumentListSyntax or BaseParameterListSyntax or ParenthesizedExpressionSyntax or TupleExpressionSyntax or CollectionExpressionSyntax or CatchDeclarationSyntax or CatchFilterClauseSyntax or (StatementSyntax and not BlockSyntax) || _IsSwitchCast(nodeFromPos);
				}
			} else if (!isEOF) {
				int r = _InNonblankTriviaOrStringOrChar(cd, tok1);
				if (r == 1) return true; //yes and corrected
				if (r == 2) return false; //yes and not corrected
				if (r == 3) canCorrect = true; //string or char. Let's complete statement.
			}
		}
		
		SyntaxNode node = null;
		foreach (var v in nodeFromPos.AncestorsAndSelf()) {
			if (v is BlockSyntax) {
				if (v.Parent is BlockSyntax or GlobalStatementSyntax || v.Span.ContainsInside(pos)) break;
				continue;
			} else if (v is StatementSyntax) {
			} else if (v is MemberDeclarationSyntax) {
				if (v is GlobalStatementSyntax) break;
			} else if (v is AttributeListSyntax) {
				if (v.Parent is ParameterSyntax) continue;
			} else if (v is ExpressionSyntax) {
				if (!(v.Parent is InitializerExpressionSyntax)) continue;
			} else if (v is AccessorDeclarationSyntax or ElseClauseSyntax or FinallyClauseSyntax or CatchClauseSyntax or UsingDirectiveSyntax or ExternAliasDirectiveSyntax or CollectionElementSyntax) {
			} else continue;
			
			if (v is EnumMemberDeclarationSyntax or ExpressionSyntax or CollectionElementSyntax && pos >= v.Span.End) continue; //if `{ ... member| }`, move caret after `}`
			
			node = v;
			break;
		}
		if (node == null || !node.Span.Contains(pos)) canCorrect = false;
		
		if (canCorrect) {
			bool needBraces = false, needSemicolon = false, needComma = false;
			SyntaxToken afterToken = default;
			
			switch (node) {
			//StatementSyntax
			case CommonForEachStatementSyntax k: _WithBlock(k.Statement, k.CloseParenToken); break;
			case FixedStatementSyntax k: _WithBlock(k.Statement, k.CloseParenToken); break;
			case ForStatementSyntax k: _WithBlock(k.Statement, k.CloseParenToken); break;
			case IfStatementSyntax k: _WithBlock(k.Statement, k.CloseParenToken); break;
			case LockStatementSyntax k: _WithBlock(k.Statement, k.CloseParenToken); break;
			case UsingStatementSyntax k: _WithBlock(k.Statement, k.CloseParenToken); break;
			case WhileStatementSyntax k: _WithBlock(k.Statement, k.CloseParenToken); break;
			case TryStatementSyntax k: _WithBlock(k.Block, k.TryKeyword); break;
			case LocalFunctionStatementSyntax k: _Function(k.Body, k.ExpressionBody, k.ParameterList.CloseParenToken, k.SemicolonToken); break;
			case SwitchStatementSyntax k:
				var obt = k.OpenBraceToken;
				if (needBraces = obt.IsMissing) {
					var cpt = k.CloseParenToken;
					if (cpt.IsMissing) { //if 'switch(word) no {}', sometimes Roslyn thinks '(word)...' is a cast expression. See _IsSwitchCast.
						if (k.SwitchKeyword.GetNextToken().Parent is not CastExpressionSyntax ce) return false;
						cpt = ce.CloseParenToken;
					}
					afterToken = cpt;
				} else {
					if (pos <= obt.SpanStart) afterToken = obt;
				}
				break;
			//MemberDeclarationSyntax
			case NamespaceDeclarationSyntax k: _WithBraces(k.OpenBraceToken); break;
			case BaseTypeDeclarationSyntax k: _WithBraces(k.OpenBraceToken); break;
			case BaseMethodDeclarationSyntax k: _Function(k.Body, k.ExpressionBody, k.ParameterList.CloseParenToken, k.SemicolonToken); break;
			case PropertyDeclarationSyntax k: _Property(k.AccessorList, k.ExpressionBody, k.Identifier, k.SemicolonToken); break;
			case IndexerDeclarationSyntax k: _Property(k.AccessorList, k.ExpressionBody, k.ParameterList.CloseBracketToken, k.SemicolonToken); break;
			case EventDeclarationSyntax k: _Property(k.AccessorList, null, k.Identifier, default); break;
			case EnumMemberDeclarationSyntax k: _WithComma(k.EqualsValue); break;
			//other
			case AccessorDeclarationSyntax k: _Function(k.Body, k.ExpressionBody, k.Keyword, k.SemicolonToken); break;
			case ElseClauseSyntax k: _WithBlock(k.Statement, k.ElseKeyword); break;
			case FinallyClauseSyntax k: _WithBlock(k.Block, k.FinallyKeyword); break;
			case CatchClauseSyntax k: _WithBlock(k.Block, k.Filter?.CloseParenToken ?? k.Declaration?.CloseParenToken ?? k.CatchKeyword); break;
			case ExpressionSyntax k: _WithComma(k); break;
			case CollectionElementSyntax k: _WithComma(k); break;
			case AttributeListSyntax: break;
			default: _WithSemicolon(node.GetLastToken(includeZeroWidth: true)); break;
			}
			//print.it($"{{}}={needBraces}  ;={needSemicolon},  ,={needComma}");
			
			//for nodes that can have a child block statement. Eg `if`, 'catch`.
			void _WithBlock(StatementSyntax statement, in SyntaxToken tokenBeforeBlock) {
				if (tokenBeforeBlock.IsMissing) return;
				if (statement is BlockSyntax bs && !bs.OpenBraceToken.IsMissing) afterToken = bs.OpenBraceToken;
				else { needBraces = true; afterToken = tokenBeforeBlock; }
			}
			
			//for nodes that have `{  }` but it isn't a block statement. Eg type declarations.
			void _WithBraces(in SyntaxToken openBraceToken) {
				if (needBraces = openBraceToken.IsMissing) afterToken = openBraceToken;
				else if (pos <= openBraceToken.SpanStart) afterToken = openBraceToken;
			}
			
			//for member and local functions
			void _Function(BlockSyntax block, ArrowExpressionClauseSyntax arrow, in SyntaxToken tokenBeforeBlock, in SyntaxToken semicolonToken) {
				if (tokenBeforeBlock.IsMissing) return;
				if (block != null) afterToken = block.OpenBraceToken;
				else if (needBraces = arrow is null) afterToken = tokenBeforeBlock;
				else needSemicolon = semicolonToken.IsMissing;
			}
			
			//for properties, indexers and events
			void _Property(AccessorListSyntax block, ArrowExpressionClauseSyntax arrow, in SyntaxToken tokenBeforeBlock, in SyntaxToken semicolonToken) {
				if (tokenBeforeBlock.IsMissing) return;
				if (block != null) afterToken = block.OpenBraceToken;
				else if (needBraces = arrow is null) afterToken = tokenBeforeBlock;
				else needSemicolon = semicolonToken.IsMissing;
			}
			
			//for statements that should end with semicolon
			void _WithSemicolon(in SyntaxToken semicolonToken) {
				if (!semicolonToken.IsKind(SyntaxKind.SemicolonToken)) { Debug_.Print(semicolonToken.Kind()); return; }
				if (needSemicolon = semicolonToken.IsMissing) afterToken = semicolonToken.GetPreviousToken();
				else afterToken = semicolonToken;
			}
			
			//for enum members and object/collection initializer expression elements
			void _WithComma(SyntaxNode lastNode) {
				var lastToken = lastNode.GetLastToken();
				var nextToken = lastToken.GetNextToken();
				var tk = nextToken.Kind();
				if (tk is SyntaxKind.CommaToken && nextToken.GetNextToken().IsKind(SyntaxKind.CloseBraceToken)) afterToken = nextToken;
				else { needComma = true; afterToken = lastToken; }
			}
			
			canCorrect = needBraces || needSemicolon || needComma;
			if (afterToken.RawKind == 0) afterToken = node.GetLastToken();
			pos = _SkipNewlineBack(code, canCorrect ? afterToken.Span.End : afterToken.FullSpan.End);
			
			if (canCorrect) {
				CiIndentation ind = new(cd, node.SpanStart, forNewLine: false);
				string s, sInd = ind.ToString();
				if (needBraces) {
					string sInd2 = node is SwitchStatementSyntax && App.Settings.ci_formatCompact ? sInd : (ind + 1).ToString();
					s = App.Settings.ci_formatCompact
						? " {" + "\r\n" + sInd2 + "\r\n" + sInd + "}"
						: "\r\n" + sInd + "{" + "\r\n" + sInd2 + "\r\n" + sInd + "}";
				} else {
					s = (needSemicolon ? ";" : ",") + "\r\n" + sInd;
				}
				
				_InsertNodeCompletionTextWithAutoformat(cd, node, s, pos, needBraces ? 3 + sInd.Length : 0);
				return true;
			} else {
				doc.aaaCurrentPos16 = pos;
			}
		}
		
		//auto-indent
		{
			var (from, to) = _SkipSpaceAround(code, pos); //remove spaces and tabs around the line break
			if (from == 0) return false;
			bool atStartOfLine = code[from - 1] == '\n';
			bool atStartOfNonblankLine = atStartOfLine && !(to == code.Length || code[to] is '\r' or '\n');
			
			CiIndentation ind = new(cd, from, forNewLine: !atStartOfNonblankLine);
			CiIndentation indBefore = atStartOfNonblankLine ? new(cd, from - (code.Eq(from - 2, '\r') ? 2 : 1), forNewLine: true) : default;
			bool noInd = ind == 0 && indBefore == 0;
			string sInd = ind.ToString(), sBefore = indBefore.ToString(), sAfter = "";
			
			// `{ | }` -> `{\r\n\t|\r\n}`
			if (!atStartOfNonblankLine && code.Eq(to, '}') && cd.syntaxRoot.FindToken(to) is { RawKind: (int)SyntaxKind.CloseBraceToken } cbt && cbt.SpanStart == to) {
				node = cbt.Parent;
				
				bool same = App.Settings.ci_formatCompact && node is SwitchStatementSyntax;
				
				if (noInd && !same) return false;
				
				string sInd2 = (same ? ind : ind - 1).ToString();
				sAfter = "\r\n" + sInd2;
				
				if (!App.Settings.ci_formatCompact // `owner {` -> `owner\r\n{`
					&& code[from - 1] == '{'
					&& !(node is BlockSyntax && node.Parent is BlockSyntax or GlobalStatementSyntax)
					&& node is not InterpolationSyntax
					&& !InsertCodeUtil.IsLineStart(code, from - 1, out int i1)
					) {
					from = i1;
					sBefore = "\r\n" + (ind - 1).ToString() + "{";
				}
			} else {
				if (noInd) return false;
				if (atStartOfLine && !atStartOfNonblankLine) sBefore = sInd;
			}
			var s = sBefore + "\r\n" + sInd + sAfter;
			
			doc.aaaReplaceRange(true, from, to, s);
			doc.aaaGoToPos(true, from + s.Length - sAfter.Length);
		}
		
		return true;
	}
	
	/// <returns>0 no, 1 yes and corrected, 2 yes and not corrected, 3 string or char.</returns>
	static int _InNonblankTriviaOrStringOrChar(CodeInfo.Context cd, SyntaxToken token) {
		string code = cd.code, suffix = null;
		bool insertEmptyLine = false;
		int pos = cd.pos, posStart = pos, posEnd = pos;
		var span = token.Span;
		if (pos < span.Start || pos > span.End) { //trivia
			var trivia = token.FindTrivia(pos);
			span = trivia.Span;
			var kind = trivia.Kind();
			if (pos == span.Start && kind != SyntaxKind.MultiLineDocumentationCommentTrivia) return 0; //info: /** span starts after /**
			switch (kind) {
			case SyntaxKind.MultiLineCommentTrivia when span.Length == 4 && pos == span.Start + 2:
				insertEmptyLine = true;
				break;
			case SyntaxKind.MultiLineCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia:
				return 2;
			case SyntaxKind.SingleLineCommentTrivia:
				suffix = "//";
				break;
			case SyntaxKind.SingleLineDocumentationCommentTrivia:
				suffix = "/// ";
				if (InsertCodeUtil.IsLineStart(code, pos, out int sol)) pos = posStart = posEnd = _SkipNewlineBack(code, sol);
				break;
			default: return 0;
			}
			if (suffix != null) { //trim spaces
				while (posStart > span.Start && _IsSpace(code[posStart - 1])) posStart--;
				while (posEnd < span.End && _IsSpace(code[posEnd])) posEnd++;
				if (kind is SyntaxKind.SingleLineDocumentationCommentTrivia && code.Eq(posStart - 3, "/// ") && InsertCodeUtil.IsLineStart(code, posStart - 3)) posStart++; //let empty lines be `/// `, not '///`
			}
		} else {
			if (span.ContainsInside(pos) && token.IsKind(SyntaxKind.CharacterLiteralToken)) return 3;
			bool? isString = token.IsInString(pos, code, out var si, orU8: true);
			if (isString == false) return 0;
			if (si.isRawPrefixCenter) {
				cd.sci.aaaInsertText(true, pos, "\r\n\r\n"); //let Enter in raw string prefix like """|""" add extra newline
				cd.sci.aaaCurrentPos16 = pos + 2;
				return 1;
			}
			if (isString == null && !si.isRawMultilineBetweenStartQuotesAndText) return 2;
			if (si.isRawMultiline || si.isRawMultilineBetweenStartQuotesAndText) {
				(posStart, posEnd) = _SkipSpaceAround(code, pos);
				var sInd = new CiIndentation(cd, posStart, forNewLine: true, rawString: true).ToString();
				if (sInd.Length > 0) {
					string sIndBefore = !si.isRawMultilineBetweenStartQuotesAndText && code[posStart - 1] == '\n' ? sInd : null; //is start of line?
					cd.sci.aaaReplaceRange(true, posStart, posEnd, sIndBefore + "\r\n" + sInd, moveCurrentPos: true);
					return 1;
				}
			}
			if (si.isRaw && !si.stringNode.ContainsDiagnostics) {
				var textSpan = si.stringNode is InterpolatedStringExpressionSyntax ises ? ises.Contents.Span : si.textSpan;
				if (pos != textSpan.Start) return 3;
				//"""|text""" -> """|\r\ntext\r\n"""
				using var undo = new SciCode.aaaUndoAction(cd.sci);
				cd.sci.aaaInsertText(true, pos + textSpan.Length, "\r\n");
				cd.sci.aaaInsertText(true, pos, "\r\n");
				return 1;
			}
			if (!si.isClassic) return 2;
			return 3;
			//rejected: split string into "abc" + "" or "abc\r\n" + "". Rarely used. Better complete statement.
		}
		
		var doc = cd.sci;
		int indent = doc.aaaLineIndentationFromPos(true, posStart);
		if (indent < 1 /*&& prefix == null*/ && suffix == null && !insertEmptyLine) return 2;
		
		var b = new StringBuilder();
		if (insertEmptyLine) {
			b.AppendLine().AppendLine().AppendIndent(indent);
			
			doc.aaaInsertText(true, pos, b.ToString());
			doc.aaaCurrentPos16 = pos + 2;
		} else {
			b.AppendLine();
			if (suffix != null) b.AppendIndent(indent).Append(suffix);
			
			doc.aaaReplaceRange(true, posStart, posEnd, b.ToString(), moveCurrentPos: true);
		}
		
		return 1;
	}
	
	static bool _OnBackspaceOrDelete(SciCode doc, bool back) {
		//when joining 2 non-empty lines with Delete or Backspace, remove indentation from the second line
		if (doc.aaaHasSelection) return false;
		int i = doc.aaaCurrentPos16;
		var code = doc.aaaText;
		RXGroup g;
		if (back) {
			if (_BlockCompletion()) return true;
			int i0 = i;
			if (code.Eq(i - 1, '\n')) i--;
			if (code.Eq(i - 1, '\r')) i--;
			if (i == i0) return false;
		} else {
			//if at the start of a line containing just tabs/spaces, let Delete delete entire line
			if (code.RxMatch(@"(?m)^\h+\R", 0, out g, RXFlags.ANCHORED, i..)) goto g1;
		}
		if (!code.RxMatch(@"(?m)(?<!^)\R\h+", 0, out g, RXFlags.ANCHORED, i..)) return false;
		g1: doc.aaaDeleteRange(true, g.Start, g.End);
		return true;
		
		//On Backspace in the last blank line before `}` in multiline `{ block }` deleted that line and adds new line after the block.
		//Supports most `{ }` kinds and `[collection]`.
		bool _BlockCompletion() {
			var (from, to) = _SkipSpaceAround(code, i);
			int nextLineAt = _SkipNewline(code, to);
			if ((nextLineAt > to && code.At_(from - 1) == '\n')) {
				int braceAt = nextLineAt; while (_IsSpace(code, braceAt)) braceAt++;
				if (code.At_(braceAt) is '}' or ']') {
					if (CodeInfo.GetDocumentAndFindToken(out var cd, out var tok, to) && tok.Kind() is var tk && tk is SyntaxKind.CloseBraceToken or SyntaxKind.CloseBracketToken) {
						var node = tok.Parent;
						string s = null, sInd = null;
						bool addSemicolon = false, format = false;
						int moveCaret = -2;
						
						if (node is ExpressionSyntax || (node is BlockSyntax && node.Parent is ExpressionSyntax)) {
							//note: API returns incorrect indentation before `]` if no comma before
							if (tok.GetNextToken(includeZeroWidth: true) is { RawKind: (int)SyntaxKind.SemicolonToken } tSemicolon) {
								sInd = new CiIndentation(cd, tSemicolon.Parent.GetStatementEtc(cd.pos).Span.End, true).ToString();
								if (addSemicolon = tSemicolon.IsMissing) to = tok.FullSpan.End; else to = tSemicolon.FullSpan.End;
								format = true;
							} else {
								to = braceAt;
								s = code[nextLineAt..braceAt];
								moveCaret = 1;
							}
						} else if (node is StatementSyntax or BaseTypeDeclarationSyntax or NamespaceDeclarationSyntax or AccessorListSyntax) {
							sInd = new CiIndentation(cd, braceAt, false).ToString();
							if (node is BlockSyntax && node.Parent is var p && !(p is BlockSyntax or GlobalStatementSyntax)) {
								if (p is DoStatementSyntax dss && dss.WhileKeyword.IsMissing) {
									to = braceAt + 1;
									s = sInd + "} while();";
								} else if (p is CatchClauseSyntax) to = p.Parent.FullSpan.End;
								else to = p.FullSpan.End;
							} else {
								to = tok.FullSpan.End;
							}
						} else return false;
						
						if (s == null) {
							string s2;
							if (code[to - 1] == '\n') s2 = sInd + "\r\n"; else { s2 = "\r\n" + sInd + "\r\n" + sInd; moveCaret -= sInd.Length; }
							if (addSemicolon) s = sInd + code[braceAt..++braceAt] + ";" + code[braceAt..to] + s2;
							else s = sInd + code[braceAt..to] + s2;
						}
						
						using var undo = format ? new SciCode.aaaUndoAction(cd.sci) : default;
						cd.sci.aaaReplaceRange(true, from, to, s);
						cd.sci.aaaCurrentPos16 = from + s.Length + moveCaret;
						if (format) _AutoFormat(default);
						
						return true;
					}
				}
			}
			return false;
		}
	}
	
	static void _AutoFormat(char ch) {
		//This func is called when text changed in these cases:
		//	1. On ';' or ':' (from SciCharAdded). Then *pos* is after the char. Then *ch* is ';' or ':'.
		//	2. On '{' when SciCharAdded added `{  }`. Then *pos* is after `{ `. Then *ch* is '{'.
		//	3. On Backspace, if completed a `{ block };`. Then *pos* is in new line after ';'. Then *ch* is '\0'.
		
		if (!App.Settings.ci_formatAuto && ch != ':') return;
		
		if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		int pos = cd.pos;
		
		var tok = cd.syntaxRoot.FindTokenOnLeftOfPosition(pos);
		if (tok.Parent is not { } n) return;
		
		if (ch is ';' or ':') if (pos != n.Span.End) return; //skip if in trivia, string, for(;;) etc
		
		int maxTo = -1;
		var tk = tok.Kind();
		if (ch == ':') {
			if (n is not SwitchLabelSyntax sls) return;
			if (!App.Settings.ci_formatAuto) maxTo = sls.Keyword.SpanStart; //just correct indentation
		} else if (tk is SyntaxKind.SemicolonToken) {
			if (n is StatementSyntax && n.Parent is var p && p is StatementSyntax and not BlockSyntax) n = p; //eg format `if(...) statement;`, not just `statement;`
		} else if (tk is SyntaxKind.OpenBraceToken) {
			//blocks: BlockSyntax, SwitchStatementSyntax, BaseTypeDeclarationSyntax, NamespaceDeclarationSyntax, AccessorListSyntax
			//expressions etc: InitializerExpressionSyntax, AnonymousObjectCreationExpressionSyntax, SwitchExpressionSyntax, PropertyPatternClauseSyntax
			
			bool skipBlock = ch == '{'; //don't format `{  }`, because would make `{ }`
			if (n is BlockSyntax bs) { //eg `if(...) {  }` or `if(...) {\r\n\t\r\n}` or `if(...) {\r\n\t\r\n statements }`. Format `if(...) ` or `if(...) {\r\n\t\r\n}`.
				if (n.Parent is BlockSyntax or GlobalStatementSyntax) return;
				if (skipBlock || bs.Statements.Any()) maxTo = n.SpanStart; //don't format `{  }` and `{\r\n\t\r\n statements }`
				n = n.Parent;
			} else { //`class C {  }`, `switch(...) {  }`, `new C {  }`, etc
				if (n is ExpressionSyntax or PropertyPatternClauseSyntax) return;
				if (skipBlock || !tok.GetNextToken().IsKind(SyntaxKind.CloseBraceToken)) maxTo = tok.SpanStart; //don't format `{  }` and `{\r\n\t\r\n tokens }`
				if (n is AccessorListSyntax) n = n.Parent;
			}
		}
		
		_AutoFormat(n, cd, maxTo);
	}
	
	static void _AutoFormat(SyntaxNode n, CodeInfo.Context cd, int maxTo = -1) {
		var (from, to) = n.GetRealFullSpan(spanEnd: true);
		if (maxTo > from) to = maxTo;
		_AutoFormat(from, to, cd);
	}
	
	static void _AutoFormat(int from, int to, CodeInfo.Context cd) {
		Debug.Assert(App.Settings.ci_formatAuto);
		Debug.Assert(cd.code == cd.sci.aaaText);
		//CiUtil.DebugHiliteRange(from, to); wait.doEvents(); 500.ms(); CiUtil.DebugHiliteRange(0, 0);
		
		int pos = cd.sci.aaaCurrentPos16;
		if (ModifyCode.Format(cd, ref from, ref to, ref pos, ref pos) is string s) {
			cd.sci.aaaReplaceRange(true, from, to, s);
			if (pos >= from || pos <= to) cd.sci.aaaCurrentPos16 = pos;
		}
	}
	
	#region util
	
	static bool _IsInNonblankTrivia(SyntaxNode node, int pos) {
		var trivia = node.FindTrivia(pos);
		if (trivia.RawKind != 0) {
			//print.it($"{trivia.Kind()}, {pos}, {trivia.FullSpan}, '{trivia}'");
			var ts = trivia.Span;
			if (!(pos > ts.Start && pos < ts.End)) { //pos is not inside trivia; possibly at start or end.
				bool lookBefore = pos == ts.Start && trivia.IsKind(SyntaxKind.EndOfLineTrivia) && node.FullSpan.Start < pos;
				if (!lookBefore) return false;
				trivia = node.FindTrivia(pos - 1); //can be eg single-line comment
				switch (trivia.Kind()) {
				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.MultiLineDocumentationCommentTrivia:
					return false;
				}
				//CiUtil.PrintNode(trivia);
			}
			switch (trivia.Kind()) {
			case SyntaxKind.None:
			case SyntaxKind.WhitespaceTrivia:
			case SyntaxKind.EndOfLineTrivia:
				break;
			default:
				return true; //mostly comments, directives and #if-disabled text
			}
		}
		return false;
	}
	
	/// <summary>
	/// Returns <c>=> c is ' ' or '\t';</c>
	/// </summary>
	static bool _IsSpace(char c) => c is ' ' or '\t';
	
	/// <summary>
	/// Returns true if <i>pos</i> is in <i>code</i> and <c>code[pos] is ' ' or '\t'</c>.
	/// </summary>
	static bool _IsSpace(string code, int pos) => (uint)pos < code.Length && code[pos] is ' ' or '\t';
	
	/// <summary>
	/// Returns the start and end of the range consisting of <c>' '</c> and <c>'\t'</c> characters around <i>pos</i>.
	/// </summary>
	static (int start, int end) _SkipSpaceAround(string code, int pos) {
		int start = pos, end = pos;
		while (_IsSpace(code, start - 1)) start--;
		while (_IsSpace(code, end)) end++;
		return (start, end);
	}
	
	/// <summary>
	/// If <i>pos</i> is at the end of a line and not at the end of the string, returns the start of next line. Else returns <i>pos</i>.
	/// </summary>
	static int _SkipNewline(string code, int pos) {
		if (pos < code.Length && code[pos] == '\r') pos++;
		if (pos < code.Length && code[pos] == '\n') pos++;
		return pos;
	}
	
	/// <summary>
	/// If <i>pos</i> is at the start of a line, returns the end of previous line. Else returns <i>pos</i>.
	/// </summary>
	static int _SkipNewlineBack(string code, int pos) {
		if (pos > 0 && code[pos - 1] == '\n') pos--;
		if (pos > 0 && code[pos - 1] == '\r') pos--;
		return pos;
	}
	
	/// <summary>
	/// If <c>switch(word)</c> without <c>{}</c> is followed by another statement, Roslyn thinks <c>(word)</c> is a cast expression.
	/// This function returns true if <i>node</i> is <b>CastExpressionSyntax</b> followed by <c>switch</c>.
	/// </summary>
	/// <param name="node">Parent node of <c>)</c> token.</param>
	/// <returns></returns>
	static bool _IsSwitchCast(SyntaxNode node) => node is CastExpressionSyntax ce && ce.OpenParenToken.GetPreviousToken() is { RawKind: (int)SyntaxKind.SwitchKeyword, Parent: SwitchStatementSyntax };
	
	/// <summary>
	/// Inserts text after node, autoformats node if need, and sets final cursor position.
	/// Used for statement completion on Enter or ';'.
	/// Uses single text modification operation (insert or replace).
	/// </summary>
	/// <param name="cd"></param>
	/// <param name="node">Node to format.</param>
	/// <param name="sInsert">Text to insert. Eg ";" or " {  }". Does not format it.</param>
	/// <param name="pos">The end of the span of the last normal token of the node. Formats until it.</param>
	/// <param name="finalPosMinus">Move the final caret position from the end of inserted text back by this count chars.</param>
	static void _InsertNodeCompletionTextWithAutoformat(CodeInfo.Context cd, SyntaxNode node, string sInsert, int pos, int finalPosMinus = 0) {
		if (App.Settings.ci_formatAuto) {
			int from = node.GetRealFullSpan().Start, to = pos;
			var sf = ModifyCode.Format(cd, ref from, ref to, ref pos, ref pos);
			if (sf != null) { cd.sci.aaaReplaceRange(true, from, to, sf + sInsert); goto g1; }
		}
		cd.sci.aaaInsertText(true, pos, sInsert);
		g1: cd.sci.aaaGoToPos(true, pos + sInsert.Length - finalPosMinus);
	}
	
	#endregion
}

/// <summary>
/// Gets indentation for a code line.
/// </summary>
struct CiIndentation {
	int _spaces;
	
	/// <summary>
	/// Determines indentation for line containing <i>pos</i>; if <i>forNewLine</i> - for new line that would be inserted at <i>pos</i>.
	/// Can be used to determine the indentation of new line on <c>Enter</c>. Not for formatting of existing code.
	/// Ignores current indentation of the line, but may use that of previous line.
	/// Does not have the formatter problems (bad comment alignment etc).
	/// </summary>
	public CiIndentation(CodeInfo.Context cd, int pos, bool forNewLine, bool rawString = false, bool useDefaultOptions = false) {
		var d = cd.document;
		var root = cd.syntaxRoot;
		string code = cd.code;
		SourceText stext;
		
		if (forNewLine) {
			code = code.Insert(pos, "\r\n\r\n");
			stext = SourceText.From(code);
			//try faster way. Faster ~2 times if big file. And the speed is stable; the simple way is randomly much slower.
			bool fast = false;
			if (!rawString) {
				if (root.FindTrivia(pos) is { RawKind: not 0 } tri) { //the most common case. Eg when pos is at the end of a line or in `{  }`.
					if (fast = tri.SpanStart == pos) {
						root = root.InsertTriviaBefore(tri, new SyntaxTriviaList(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed));
						//stext = root.GetText(); code = stext.ToString(); //much slower
					}
				} else if (root.FindToken(pos) is { RawKind: not 0 } tok) {
					if (fast = tok.SpanStart == pos && !tok.HasLeadingTrivia) {
						root = root.ReplaceToken(tok, tok.WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed));
					}
				}
			}
			if (!fast) root = root.SyntaxTree.WithChangedText(stext).GetCompilationUnitRoot();
			pos += 2;
		} else {
			stext = root.SyntaxTree.GetText();
		}
		
		var parsedDocument = new ParsedDocument(d.Id, stext, root, d.Project.GetExtendedLanguageServices());
		var indenter = parsedDocument.LanguageServices.GetRequiredService<IIndentationService>();
		int line = parsedDocument.Text.Lines.GetLineFromPosition(pos).LineNumber;
		var indOpt = useDefaultOptions ? IndentationOptions.GetDefault(parsedDocument.LanguageServices) : new IndentationOptions(ModifyCode.FormattingOptions);
		IndentationResult ind = indenter.GetIndentation(parsedDocument, line, indOpt, default);
		
		//IndentationResult -> _ind
		
		int to = ind.BasePosition, i = to;
		while (i > 0 && code[i - 1] is '\t' or ' ') i--;
		int k = 0;
		for (; i < to; i++) {
			if (code[i] == ' ') k++;
			else k = k / 4 * 4 + 4;
		}
		
		_spaces = k + ind.Offset;
		
		if (forNewLine && !rawString) {
			//bad indentation after `break;` in `switch{}`
			if (_spaces >= 4 && root.FindTokenOnLeftOfPosition(pos).Parent is BreakStatementSyntax bss && bss.Parent is SwitchSectionSyntax && !bss.SemicolonToken.IsMissing) {
				_spaces -= 4;
			}
		}
	}
	
	public static CiIndentation operator ++(CiIndentation i) { i._spaces += 4; return i; }
	
	public static CiIndentation operator --(CiIndentation i) { i._spaces = Math.Max(0, i._spaces - 4); return i; }
	
	public static CiIndentation operator +(CiIndentation i, int plus) => new() { _spaces = i._spaces + plus * 4 };
	
	public static CiIndentation operator -(CiIndentation i, int minus) => new() { _spaces = Math.Max(0, i._spaces - minus * 4) };
	
	public override string ToString() {
		if (_spaces == 0) return "";
		if (!App.Settings.ci_formatTabIndent) return new(' ', _spaces);
		var (tabs, spaces) = Math.DivRem(_spaces, 4);
		return spaces == 0 ? new('\t', tabs)
			: tabs == 0 ? new(' ', spaces)
			: new string('\t', tabs) + new string(' ', spaces);
	}
	
	public int Spaces => _spaces;
	
	public static implicit operator int(CiIndentation i) => (i._spaces + 3) / 4;
}
