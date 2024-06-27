//#define PRINT

extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;

using Au.Controls;
using static Au.Controls.Sci;

class CiFolding {
	//Called from CiStyling._Work -> Task.Run when document opened or modified (250 ms timer).
	//We always set/update folding for entire code. Makes slower, but without it sometimes bad folding.
	public static List<SciFoldPoint> GetFoldPoints(SyntaxNode root, string code, CancellationToken cancelToken) {
#if PRINT
		using var p1 = perf.local();
#endif
		void _PN(char ch = default) {
#if PRINT
			p1.Next(ch);
#endif
		}
		
		List<SciFoldPoint> af = null;
		s_foldPoints.Clear();
		
		void _AddFoldPoint(FoldKind what, int pos, bool start, bool trimNewline = false, ushort separator = 0) {
			if (trimNewline) {
				if (code[pos - 1] == '\n') pos--;
				if (code[pos - 1] == '\r') pos--;
			}
			(af ??= new()).Add(new(pos, start, separator));
			if (start) s_foldPoints.Add((pos, what));
		}
		void _AddFoldPoints(FoldKind what, int start, int end, ushort separator = 0) {
			if (separator == 0) {
				int k = code.IndexOf('\n', start, end - start);
				if (k < 0 || k == end - 1) return;
			}
			_AddFoldPoint(what, start, true);
			_AddFoldPoint(what, end, false, true, separator);
		}
		
		IEnumerable<SyntaxNode> nodes;
#if !true //slow
		if (code.Length > 200_000) { //fast, but does not fold anything inside functions and statements
			nodes = root.DescendantNodes(static o => {
				if (o is MemberDeclarationSyntax) return o is BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax;
				return o is CompilationUnitSyntax;
			});
		} else { //slow
			nodes = root.DescendantNodes();
		}
#else //2-3 times faster
		if (code.Length > 500_000) { //fast, but does not fold anything inside functions and statements
			nodes = root.DescendantNodes(static o => {
				if (o is MemberDeclarationSyntax) return o is BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax;
				return o is CompilationUnitSyntax;
			});
		} else {
			nodes = root.DescendantNodes(n => {
				//note: all this code is just to make faster. It does not add any features.
				if (n is StatementSyntax) { //don't descend into statements that don't contain `{` etc
					var nspan = n.Span;
					if (nspan.Length < 20) return false;
					var s = code.AsSpan(nspan.ToRange());
					int i = s.IndexOf(1, '{');
					if (s[0] != '{') { //not BlockSyntax
						if (i > 0) {
							if (s[^1] == '}' || n is IfStatementSyntax { Statement: BlockSyntax } or DoStatementSyntax) i = s.IndexOf(i + 1, '{');
						} else if (s[^1] is ';' && s[^2] is ']' or '"') { //maybe like `int[] a = [ multiline ];` or `var s = """multiline""";`
							return s.Contains('\n');
						}
					}
					if (i < 0 && s.Contains('\n')) {
						i = s.IndexOf("\";");
						if (i < 0) i = s.IndexOf("];");
					}
					return i >= 0;
				}
				return true;
			});
		}
#endif
		SyntaxNode prevStatementOrMember = null;
		foreach (var n in nodes) {
			if (n is GlobalStatementSyntax g) continue;
			//CiUtil.PrintNode(n, indent: true);
			
			FoldKind foldKind = FoldKind.Member;
			int foldStart = -1;
			bool separatorBefore = false;
			SyntaxNode prevSibling = null;
			
			switch (n) {
			case BaseTypeDeclarationSyntax d: //class, struct, interface, enum
				foldStart = d.Identifier.SpanStart;
				foldKind = FoldKind.Type;
				separatorBefore = _PrevSibling(n) is StatementSyntax and not LocalFunctionStatementSyntax;
				break;
			case BaseMethodDeclarationSyntax d: //method, ctor, etc
				if (d.Body == null && d.ExpressionBody == null) continue; //extern, interface, partial
				foldStart = d.ParameterList.SpanStart; //not perfect, but the best common property
				separatorBefore = _PrevSibling(n) is BaseFieldDeclarationSyntax;
				break;
			case BasePropertyDeclarationSyntax d: //property, indexer, event
				foldStart = d.Type.FullSpan.End; //not perfect, but the best common property
				separatorBefore = _PrevSibling(n) is BaseFieldDeclarationSyntax;
				break;
			case LocalFunctionStatementSyntax d:
				if (d.Parent is GlobalStatementSyntax) separatorBefore = !(_PrevSibling(n) is null or LocalFunctionStatementSyntax);
				else if (_ManyLines(n, 3)) foldKind = FoldKind.Statement;
				else break;
				foldStart = d.Identifier.SpanStart;
				break;
			case AnonymousFunctionExpressionSyntax d when d.ExpressionBody == null: //lambda, delegate(){}
			case InitializerExpressionSyntax or CollectionExpressionSyntax:
			case LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } or InterpolatedStringExpressionSyntax:
				if (_ManyLines(n, n is AnonymousFunctionExpressionSyntax ? 3 : 5)) {
					foldStart = n.SpanStart;
					foldKind = FoldKind.Statement;
				}
				break;
			case NamespaceDeclarationSyntax d:
				foldStart = d.Name.SpanStart;
				foldKind = FoldKind.Namespace;
				break;
			}
			
			if (foldStart >= 0) {
				_AddFoldPoints(foldKind, foldStart, n.Span.End, separator: (ushort)(foldKind == FoldKind.Statement ? 0 : 1));
				
				//add separator before top-level local function preceded by statement of other type.
				//	Also before other functions preceded by field or simple event.
				if (separatorBefore) {
					int i = _PrevSibling(n).Span.End; //add separator at the bottom of line containing position i
					if (n is LocalFunctionStatementSyntax) { //if there are empty lines, set i at the last empty line
						var kPrev = SyntaxKind.EndOfLineTrivia;
						foreach (var t in n.GetLeadingTrivia()) {
							var k = t.Kind();
							if (k == SyntaxKind.EndOfLineTrivia && k == kPrev) i = t.SpanStart;
							kPrev = k;
						}
					}
					i = foldStart - i;
					if (i <= ushort.MaxValue) af[^2] = af[^2] with { separator = (ushort)i };
				}
			}
			if (n is StatementSyntax or MemberDeclarationSyntax) prevStatementOrMember = n;
			if (cancelToken.IsCancellationRequested) return null;
			
			SyntaxNode _PrevSibling(SyntaxNode n) {
				if (prevSibling != null) return prevSibling;
				var p = n.Parent;
				var r = prevStatementOrMember;
				while (r != null) {
					var p2 = r.Parent;
					if (p2 == p || p2 is GlobalStatementSyntax) break;
					r = p2;
				}
				return prevSibling = r;
			}
			
			bool _ManyLines(SyntaxNode n, int lines) {
				var s = code.AsSpan(n.Span.ToRange());
				for (int i = 0; ;) {
					i = s.IndexOf(i, '\n') + 1;
					if (i == 0) return false;
					if (--lines <= 1) return true;
				}
			}
		}
		_PN('n');
		
		List<TextSpan> disabledRanges = null;
		var dir = root.GetDirectives(o => o is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax or BranchingDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax);
		foreach (var v in dir) {
			if (v is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax) {
				_AddFoldPoint(FoldKind.Region, v.SpanStart, v is RegionDirectiveTriviaSyntax);
			} else if (v is BranchingDirectiveTriviaSyntax br && !br.BranchTaken) {
				var rd = br.GetRelatedDirectives();
				for (int i = 0; i < rd.Count - 1;) {
					if (rd[i++] == v) {
						int start = v.SpanStart, end = rd[i].SpanStart;
						while (end > start && code[end - 1] is not ('\n' or '\r')) end--;
						_AddFoldPoints(FoldKind.Disabled, start, end);
						(disabledRanges ??= new()).Add(TextSpan.FromBounds(start, end));
						break;
					}
				}
			}
		}
		_PN('d');
		
		//Find comments that need to fold:
		//	1. Blocks of //comments of >= 5 lines. Include empty lines, but split at the last empty line if last comments are followed by non-comments or other type of comments.
		//	2. Blocks of ///comments of >= 2 lines.
		//	3. /*...*/ comments of >= 2 lines. Same for /**...*/.
		//	4. //. is like #region, but can be not at the start of line too. Must not be followed by a non-space character.
		//	5. //.. is like #endregion, but can be not at the start of line too. Must not be followed by a non-space character.
		//		Rejected: //... unfolds 2 levels, //.... 3 levels and so on. Often //... comment used for "more code" or "etc". Also now not useful (was useful when C# did not have top-level statements).
		
		//Since root.DescendantTrivia is slow, we parse code
		//	and then use Roslyn just to verify that the found // etc is at start of trivia, ie isn't inside a string or other comments or #directive.
		//We skip disabled code (!BranchingDirectiveTriviaSyntax.BranchTaken).
		for (int ir = 0, nr = disabledRanges.Lenn_(), rangeStart = 0; ; rangeStart = disabledRanges[ir++].End) {
			int rangeEnd = ir == nr ? code.Length : disabledRanges[ir].Start;
			//print.it(rangeEnd-rangeStart);
			int rangeLength = rangeEnd - rangeStart;
			if (rangeLength > 2) { //possible at least //.
				var s = code.AsSpan(rangeStart, rangeLength); //current non-disabled range in code
				for (int i = 0; i <= s.Length - 4;) { //until last possible //..
					if (cancelToken.IsCancellationRequested) return null;
					
					int i0 = i = s.IndexOf(i, '/'); if ((uint)i > s.Length - 4) break;
					char c = s[++i]; if (c is not ('/' or '*')) continue;
					if (c == '/' && _IsDotComment(s, ++i, out bool closing)) { //.
						if (!_IsStartOfTrivia(false)) continue;
						_AddFoldPoint(FoldKind.DotFold, rangeStart + i0, !closing);
					} else if (c == '*' || CiUtil.IsLineStart(s, i0)) {
						i = i0 + 2;
						bool isLineComment = c == '/', isDocComment = false;
						if (!isLineComment) {
							//ignore /*single line*/
							int k = s.IndexOf(i, '\n');
							if (k < 0 || s[i..k].Contains("*/", StringComparison.Ordinal)) continue;
						} else if (s.Eq(i, '/') && !s.Eq(++i, '/')) { //doc comment ///
							isLineComment = false;
							i0 = i; //somehow Span of doc comment trivia starts after ///
							
							//ignore single-line doc comments
							int k = s.IndexOf(i, '\n'); if (k++ < 0) continue;
							while (k < s.Length && s[k] is '\t' or ' ') k++;
							if (!s.Eq(k, "///")) continue;
							isDocComment = true;
						} else {
							int nlines = 0, joinAt = 0, joinNlines = 0;
							for (; ; nlines++) {
								if (nlines > 0) {
									int lineStart = i;
									while (i < s.Length && s[i] is '\t' or ' ') i++; //skip indent
									g1:
									bool ok = s.Eq(i, '/') && s.Eq(i + 1, '/');
									if (ok) {
										i += 2;
										//is same type of comment?
										if (s.Eq(i, '/')) ok = s.Eq(++i, '/'); //is /// ?
										else ok = !_IsDotComment(s, i, out _); //is //. ?
									} else {
										//join adjacent blocks of comments that end with empty line
										if (i == s.Length) joinAt = 0; //join
										else if (s[i] is '\r' or '\n') { //empty line. Continue, and later either join or split here.
											joinAt = lineStart; joinNlines = nlines;
											while (++i < s.Length && s[i] <= ' ') { } //skip empty lines and indents
											goto g1;
										}
									}
									if (!ok) {
										if (joinAt > 0) { lineStart = joinAt; nlines = joinNlines; } //split
										i = lineStart;
										break;
									}
								}
								while (i < s.Length && s[i] != '\n') i++;
								if (i == s.Length) { nlines++; break; }
								i++;
							}
							if (nlines < 5) continue;
						}
						if (!_IsStartOfTrivia(isLineComment)) continue;
						_AddFoldPoints(isDocComment ? FoldKind.Doc : FoldKind.Comment, rangeStart + i0, rangeStart + i);
					}
					
					static bool _IsDotComment(RStr s, int j, out bool closing) {
						if (s.Eq(j, '.')) {
							if (closing = ++j < s.Length && s[j] == '.') j++;
							if (j == s.Length || s[j] <= ' ') return true; //must be at the end of line or followed by space (like //. comment). Else could be like //.Member
						} else closing = false;
						return false;
					}
					
					bool _IsStartOfTrivia(bool isLineComment) {
						//p2.First();
						int start = rangeStart + i0;
						var t = root.FindToken(start);
						var span = t.Span;
						//p2.Next();
						if (span.Contains(start)) { i = span.End - rangeStart; return false; }
#if true
						var v = t.FindTrivia(start);
						span = v.Span;
						if (span.End == 0) { Debug_.Print(start); return false; }
						bool ok = span.Start == start;
						if (!(ok && isLineComment)) i = span.End - rangeStart;
						//p2.Next();
						return ok;
#elif true //usually slightly slower, but in worst case could be much slower
						var v = t.Parent.FindTrivia(start);
						span = v.Span;
						if (span.End == 0) { Debug_.Print(start); return false; }
						bool ok = span.Start == start;
						if (!(ok && isLineComment)) i = span.End - rangeStart;
						return ok;
#else //slightly slower than above
						foreach (var v in t.GetAllTrivia()) {
							span = v.Span;
							if (span.Start == start) { if (!isLineComment) i = span.End - rangeStart; return true; }
							if (span.Start > start) break;
							if (span.End > start) { i = span.End - rangeStart; return false; }
						}
						return false;
#endif
					}
				}
			}
			if (ir == nr) break;
		}
		_PN('t');
		
		if (af != null) {
			af.Sort((x, y) => x.pos - y.pos);
			//remove redundant fold end points
			for (int i = 0, level = 0; i < af.Count; i++) {
				if (af[i].start) level++;
				else if (level > 0) level--;
				else af.RemoveAt(i--);
			}
			//print.it(af);
		}
		return af;
	}
	
	public static void Fold(SciCode doc, List<SciFoldPoint> af) {
		doc.aaaFoldingApply(af, SciCode.c_markerUnderline);
		doc.ERestoreEditorData_();
	}
	
	public static void InitFolding(SciCode doc) {
		doc.aaaFoldingInit(SciCode.c_marginFold, SciCode.c_markerUnderline);
	}
	
	[Flags]
	internal enum FoldKind { Member = 1, Type = 2, Region = 4, Comment = 8, Doc = 16, DotFold = 32, Disabled = 64, Statement = 128, Namespace = 256 }
	
	/// <summary>
	/// Fold points (start position and kind) of the last <see cref="GetFoldPoints"/>. Later used for the context menu.
	/// </summary>
	internal static readonly List<(int pos, FoldKind kind)> s_foldPoints = new();
}

partial class SciCode {
	internal void ERestoreEditorData_() {
		//print.it(_openState);
		if (_openState == _EOpenState.FoldingDone) return;
		var os = _openState; _openState = _EOpenState.FoldingDone;
		
		if (os is _EOpenState.NewFileFromTemplate) {
			if (_fn.IsScript) {
				var code = aaaText;
				if (!code.NE()) {
					//fold all //.
					for (int i = base.aaaLineCount - 1; --i >= 0;) {
						if (aaaFoldingLevel(i).isHeader) {
							int j = aaaLineEnd(false, i);
							if (aaaCharAt8(j - 1) == '.' && aaaCharAt8(j - 2) == '/') Call(SCI_FOLDLINE, i);
						}
					}
					
					if (code.RxMatch(@"//\.\.+\R\R?(?=\z|\R)", 0, out RXGroup g)) {
						aaaGoToPos(true, g.End);
					}
					//if (CodeInfo.GetContextAndDocument(out var cd, 0, true) && !cd.code.NE() && cd.document.GetSyntaxRootAsync().Result is CompilationUnitSyntax cu) {
					//	print.it(cu);
					//}
				}
			}
		} else if (os == _EOpenState.NewFileNoTemplate) {
		} else {
			//restore saved folding, some markers, scroll position and caret position
			if (App.Model.State.EditorGet(_fn, out _sed)) {
				int cp = aaaCurrentPos8;
				if (_sed.fold != null) {
					for (int i = _sed.fold.Length; --i >= 0;) EFoldLine(_sed.fold[i]);
					if (cp > 0) Call(SCI_ENSUREVISIBLEENFORCEPOLICY, aaaLineFromPos(false, cp));
				}
				//if (_sed.someMarker != null) {
				//	foreach (var v in _sed.someMarker) Call(SCI_MARKERADD, v, c_markerX);
				//}
				if (os != _EOpenState.Reopen) {
					if (_sed.top != 0 || _sed.pos != 0) {
						_sed.top = _sed.pos = 0;
						App.Model.State.EditorSave(_fn, _sed, true, false);
					}
				} else {
					int top = Math.Max(0, _sed.top), pos = Math.Clamp(_sed.pos, 0, aaaLen8);
					if (top + pos > 0 && cp == 0) {
						//workaround for:
						//	When reopening a non-first document, scrollbar position remains at the top, although scrolls the view.
						//	Scintilla calls SetScrollInfo(pos), but it does not work because still didn't call SetScrollInfo(max).
						//	Another possible workaround would be a timer, but even 50 ms is too small.
						Call(SCI_SETVSCROLLBAR, false);
						Call(SCI_SETVSCROLLBAR, true);
						
						if (top > 0) Call(SCI_SETFIRSTVISIBLELINE, Call(SCI_VISIBLEFROMDOCLINE, top));
						if (pos <= aaaLen8) {
							App.Model.EditGoBack.OnRestoringSavedPos();
							//aaaGoToPos(false, pos); //sometimes does not work well here (it also calls SCI_ENSUREVISIBLEENFORCEPOLICY)
							Call(SCI_GOTOPOS, pos);
						}
					}
				}
			}
		}
	}
	
	enum _EOpenState : byte { Open, Reopen, NewFileFromTemplate, NewFileNoTemplate, FoldingDone }
	_EOpenState _openState;
	
	/// <summary>
	/// Saves folding, markers, cursor position, etc.
	/// </summary>
	internal void ESaveEditorData_(bool closingDoc) {
		if (_openState < _EOpenState.FoldingDone) return; //if did not have time to open editor data, better keep old data than delete. Also if not a code file.
		
		var a = new List<int>();
		var x = new WorkspaceState.Editor {
			fold = _GetLines(31),
			//someMarker = _GetLines(c_markerBreakpoint),
		};
		if (!closingDoc) {
			x.pos = aaaCurrentPos8;
			x.top = Call(SCI_GETFIRSTVISIBLELINE);
			if (x.top > 0) x.top = Call(SCI_DOCLINEFROMVISIBLE, x.top); //save document line, because visible line changes after changing wrap mode or resizing in wrap mode etc. Never mind: the top visible line may be not at the start of the document line.
		}
		
		if (x.Equals(_sed, out bool changedState, out bool changedFolding)) return;
		_sed = x;
		App.Model.State.EditorSave(_fn, x, changedState, changedFolding);
		
		//Gets indices of lines containing markers or contracted folding points.
		//If marker 31, uses SCI_CONTRACTEDFOLDNEXT. Else uses SCI_MARKERNEXT; must be 0...24 (markers 25-31 are used for folding).
		int[] _GetLines(int marker/*, int skipLineFrom = 0, int skipLineTo = 0*/) {
			a.Clear();
			for (int i = 0; ; i++) {
				if (marker == 31) i = Call(SCI_CONTRACTEDFOLDNEXT, i);
				else i = Call(SCI_MARKERNEXT, i, 1 << marker);
				if (i < 0) break;
				a.Add(i);
			}
			return a.Count > 0 ? a.ToArray() : null;
		}
	}
	WorkspaceState.Editor _sed;
	
	public void EFoldLine(int line, bool unfold = false, bool andDescendants = false) {
		if (!aaaFoldingLevel(line).isHeader) return;
		
		if (unfold) {
			Call(andDescendants ? SCI_FOLDCHILDREN : SCI_FOLDLINE, line, 1);
			return;
		}
		
		if (0 != Call(SCI_GETFOLDEXPANDED, line)) {
			//get text for SCI_TOGGLEFOLDSHOWTEXT
			string s = aaaLineText(line), s2 = null;
			int i = 0; while (i < s.Length && s[i] is ' ' or '\t') i++;
			if (s.Eq(i, "///")) { //let s2 = summary text
				s_rxFold.summary1 ??= new regexp(@"\h*<summary>\h*$", RXFlags.ANCHORED);
				if (s_rxFold.summary1.IsMatch(s, (i + 3)..)) {
					i = aaaLineStart(false, line + 1);
					int len = aaaLen8;
					if (i < len) {
						const int maxLen = 100;
						s = aaaRangeText(false, i, Math.Min(len, i + maxLen + 10));
						int to = s.Find("</summary>");
						bool limited = to < 0;
						if (limited) to = Math.Min(s.Length, maxLen); //Min to avoid partial </summary> at the end
						s_rxFold.summary2 ??= new regexp(@"\h*///\h*\K.+");
						using (new StringBuilder_(out var b)) {
							foreach (var g in s_rxFold.summary2.FindAllG(s, 0, 0..to)) {
								if (b.Length > 0) b.Append(' ');
								b.Append(s, g.Start, g.Length);
							}
							if (limited) b.Append(" …");
							s2 = b.ToString();
						}
					}
				}
			} else { //if like 'void Method() {', let s2 = "... }"
				for (; i < s.Length; i++) {
					char c = s[i];
					if (c == '{') { s2 = "... }"; break; }
					if (c == '/' && i < s.Length - 1) {
						c = s[i + 1];
						if (c == '/') break;
						if (c == '*') {
							if (i >= s.Length - 4) break;
							i = s.Find("*/", i + 2); if (++i == 0) break;
						}
					}
				}
			}
			
			if (s2 != null) aaaSetString(SCI_TOGGLEFOLDSHOWTEXT, line, s2);
			else Call(SCI_FOLDLINE, line);
		}
		
		if (andDescendants) {
			for (int last = Call(SCI_GETLASTCHILD, line, -1); ++line < last;) {
				EFoldLine(line);
			}
		}
	}
	static (regexp summary1, regexp summary2) s_rxFold;
	
	/// <param name="modifiers">
	/// 0 - toggle this.
	/// Shift (1) - expand this and descendants.
	/// Ctrl (2) - toggle this and descendants.
	/// </param>
	void _FoldOnMarginClick(int pos8, int modifiers) {
		if (modifiers > 2) return;
		int line = Call(SCI_LINEFROMPOSITION, pos8);
		if (!aaaFoldingLevel(line).isHeader) return;
		if (modifiers == 1) {
			Call(SCI_FOLDCHILDREN, line, 1);
			return;
		}
		bool hide = 0 != Call(SCI_GETFOLDEXPANDED, line);
		EFoldLine(line, !hide, modifiers == 2);
		if (hide) {
			//move caret out of contracted region
			int pos = aaaCurrentPos8;
			if (pos > pos8) {
				int i = aaaLineEnd(false, Call(SCI_GETLASTCHILD, line, -1));
				if (pos <= i) aaaCurrentPos8 = pos8;
			}
		}
	}
	
	void _FoldContextMenu(int pos8 = -1) {
		if (!_fn.IsCodeFile) return;
		
		//get the fold kind for "Fold all of this kind"
		int headLine = aaaLineFromPos(false, pos8 < 0 ? aaaCurrentPos8 : pos8);
		if (!aaaFoldingLevel(headLine).isHeader) headLine = Call(SCI_GETFOLDPARENT, headLine);
		var kind = headLine < 0 ? 0 : _Folds().FirstOrDefault(o => o.line == headLine).kind;
		
		var m = new popupMenu();
		if (kind != default) {
			m["Fold all of this kind"] = o => _Fold(false, kind);
			m["Unfold all of this kind"] = o => _Fold(true, kind);
			m.Separator();
		}
		m["Fold functions, events"] = o => _Fold(false, CiFolding.FoldKind.Member);
		m["Fold regions"] = o => _Fold(false, CiFolding.FoldKind.Region);
		m["Fold //."] = o => _Fold(false, CiFolding.FoldKind.DotFold);
		m["Fold ///documentation"] = o => _Fold(false, CiFolding.FoldKind.Doc);
		m["Fold comments, #if"] = o => _Fold(false, CiFolding.FoldKind.Comment | CiFolding.FoldKind.Disabled);
		m["Fold all except types"] = o => _Fold(false, CiFolding.FoldKind.Comment | CiFolding.FoldKind.Disabled | CiFolding.FoldKind.Doc | CiFolding.FoldKind.DotFold | CiFolding.FoldKind.Member | CiFolding.FoldKind.Region);
		m.Separator();
		m["Unfold all"] = o => _Fold(true, 0); //not SCI_FOLDALL because need to skip meta
		
		m.Show(owner: AaWnd);
		
		void _Fold(bool expand, CiFolding.FoldKind kind) {
			var meta = Au.Compiler.MetaComments.FindMetaComments(aaaText);
			int metaLine = meta.end == 0 ? -1 : aaaLineFromPos(true, meta.start);
			var a = _Folds().ToArray();
			List<int> a2 = new();
			for (int i = 0; i < a.Length; i++) {
				var v = a[i];
				if (v.line == metaLine) continue;
				if (kind != 0) {
					if (!kind.Has(v.kind)) continue;
					//together with members and types also fold/unfold their ///documentation
					if (kind is CiFolding.FoldKind.Member or CiFolding.FoldKind.Type) {
						if (i > 0 && a[i - 1].kind == CiFolding.FoldKind.Doc) {
							int line2 = a[i - 1].line;
							if (v.line - 1 == Call(SCI_GETLASTCHILD, line2, -1)) a2.Add(line2);
						}
					}
				}
				a2.Add(v.line);
			}
			
			//In some cases something changes current pos. Also scrolls and unfolds. Sometimes then bad styling.
			//	Difficult to find the reason.
			//	Workaround: hide/show in this order.
			if (expand) {
				for (int i = 0; i < a2.Count; i++) {
					EFoldLine(a2[i], expand);
				}
			} else {
				for (int i = a2.Count; --i >= 0;) {
					EFoldLine(a2[i], expand);
				}
				
				//move caret out of contracted region
				int line = aaaLineFromPos(false, aaaCurrentPos8), line0 = line;
				while (0 == Call(SCI_GETLINEVISIBLE, line)) {
					int i = Call(SCI_GETFOLDPARENT, line);
					if ((uint)i >= line) break;
					line = i;
				}
				if (line < line0) aaaCurrentPos8 = aaaLineStart(false, line);
			}
		}
		
		//Gets an ordered copy of CiFolding.s_foldPoints with offsets converted to line indices.
		//	GetFoldPoints does not do it because it would make slower there.
		IEnumerable<(int line, CiFolding.FoldKind kind)> _Folds()
			=> CiFolding.s_foldPoints.OrderBy(o => o.pos).Select(o => (line: aaaLineFromPos(true, o.pos), kind: o.kind));
	}
}
