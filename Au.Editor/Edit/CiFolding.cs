//#if TRACE
//#define PRINT
//#endif

using static Au.Controls.Sci;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;

class CiFolding {
	public record struct FoldPoint(int pos, bool start, ushort separator);

	//Called from CiStyling._Work -> Task.Run when document opened or modified (250 ms timer).
	//We always set/update folding for entire code. Makes slower, but without it sometimes bad folding.
	//Optimized, but still not very fast if big code.
	public static List<FoldPoint> GetFoldPoints(SyntaxNode root, string code, CancellationToken cancelToken) {
#if PRINT
		using var p1 = perf.local();
#endif
		void _PN(char ch = default) {
#if PRINT
			p1.Next(ch);
#endif
		}
		//using var p2 = new perf.Instance { Incremental = true };

		List<FoldPoint> af = null;

		void _AddFoldPoint(int pos, bool start, bool trimNewline = false, ushort separator = 0) {
			if (trimNewline) {
				if (code[pos - 1] == '\n') pos--;
				if (code[pos - 1] == '\r') pos--;
			}
			(af ??= new()).Add(new(pos, start, separator));
		}
		void _AddFoldPoints(int start, int end, ushort separator = 0) {
			if (separator == 0) {
				int k = code.IndexOf('\n', start, end - start);
				if (k < 0 || k == end - 1) return;
			}
			_AddFoldPoint(start, true);
			_AddFoldPoint(end, false, true, separator);
		}

		var nodes = root.DescendantNodes(o => {
			//don't descend into functions etc. Much faster.
			if (o is MemberDeclarationSyntax) return o is BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax;
			return o is CompilationUnitSyntax;
		});
		SyntaxNode prevNode = null;
		foreach (var v_ in nodes) {
			var v = v_; if (v is GlobalStatementSyntax g) v = g.Statement;
			//CiUtil.PrintNode(v);
			//print.it(v.GetType().Name);
			//bool noSeparator = false;
			bool separatorBefore = false;
			int foldStart = -1;
			switch (v) {
			case BaseTypeDeclarationSyntax d: //class, struct, interface, enum
				foldStart = d.Identifier.SpanStart;
				break;
			case BaseMethodDeclarationSyntax d: //method, ctor, etc
				if (d.Body == null && d.ExpressionBody == null) continue; //extern, interface, partial
				foldStart = d.ParameterList.SpanStart; //not perfect, but the best common property
				separatorBefore = prevNode is BaseFieldDeclarationSyntax;
				break;
			case BasePropertyDeclarationSyntax d: //property, indexer, event
				foldStart = d.Type.FullSpan.End; //not perfect, but the best common property
				separatorBefore = prevNode is BaseFieldDeclarationSyntax;
				break;

			case LocalFunctionStatementSyntax d:
				foldStart = d.Identifier.SpanStart;
				separatorBefore = prevNode is not (LocalFunctionStatementSyntax or null);
				break;
				//rejected. 1. Then would need to more code for the "hide all" command, to exclude namespaces. 2. Namespaces can be without { } (C# 10).
				//case NamespaceDeclarationSyntax d:
				//	foldStart = d.Name.SpanStart;
				//	break;				//rejected. Would make DescendantNodes slow.
				//case AnonymousFunctionExpressionSyntax d when d.ExpressionBody == null && v.Parent is ArgumentSyntax: //lambda, delegate(){}
				//	foldStart = v.SpanStart;
				//	noSeparator = true;
				//	break;
			}

			if (foldStart >= 0) {
				_AddFoldPoints(foldStart, v.Span.End, separator: 1);

				//add separator before local function preceded by statement of other type. Also before other function preceded by field or simple event.
				if (separatorBefore) {
					int i = prevNode.Span.End; //add separator at the bottom of line containing position i
					if (v is LocalFunctionStatementSyntax) { //if there are empty lines, set i at the last empty line
						var kPrev = SyntaxKind.EndOfLineTrivia;
						foreach (var t in v.GetLeadingTrivia()) {
							var k = t.Kind();
							if (k == SyntaxKind.EndOfLineTrivia && k == kPrev) i = t.SpanStart;
							kPrev = k;
						}
					}
					i = foldStart - i;
					if (i <= ushort.MaxValue) af[^2] = af[^2] with { separator = (ushort)i };
				}
			}
			prevNode = v;
			if (cancelToken.IsCancellationRequested) return null;
		}
		_PN('n');

		List<TextSpan> disabledRanges = null;
		var dir = root.GetDirectives(o => o is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax or BranchingDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax);
		_PN('d');
		//print.it(dir.Count);
		foreach (var v in dir) {
			//print.it(v.IsActive, v.GetType());
			//if(v is BranchingDirectiveTriviaSyntax br) print.it(br, br.BranchTaken, br.GetRelatedDirectives());
			if (v is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax) {
				_AddFoldPoint(v.SpanStart, v is RegionDirectiveTriviaSyntax);
			} else if (v is BranchingDirectiveTriviaSyntax br && !br.BranchTaken) {
				var rd = br.GetRelatedDirectives();
				for (int i = 0; i < rd.Count - 1;) {
					if (rd[i++] == v) {
						int start = v.SpanStart, end = rd[i].SpanStart;
						while (end > start && code[end - 1] is not ('\n' or '\r')) end--;
						_AddFoldPoints(start, end);
						(disabledRanges ??= new()).Add(TextSpan.FromBounds(start, end));
						break;
					}
				}
			}
		}
		_PN();

		//Find comments that need to fold:
		//	1. Blocks of //comments of >= 4 lines. Include empty lines, but split at last empty line if last comments are followed by non-comments or other type of comments.
		//	2. Blocks of ///comments of >= 2 lines.
		//	3. /*...*/ comments of >= 2 lines. Same for /**..*/.
		//	4. //. is like #region, but can be not at start of line too. Must not be followed by non-space character.
		//	5. //.. is like #endregion, but can be not at start of line too. More . can be added (like //...) to decrement folding level more. Must not be followed by non-space character.
		
		//BAD: //.. isn't good. In code examples often used //... . Better would be eg //' or //\, but now can't change.
		//	Workaround: autocorrect //... -> // ...

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
					if (c == '/' && _IsDotComment(s, ++i, out int nDots)) { //.
						if (!_IsStartOfTrivia(false)) continue;
						int k = rangeStart + i0;
						if (nDots == 1) _AddFoldPoint(k, true); else while (--nDots > 0) _AddFoldPoint(k, false);
					} else if (c == '*' || _IsStartOfLine(s, i0)) {
						i = i0 + 2;
						bool isLineComment = c == '/';
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
						} else {
							int nlines = 0, joinAt = 0, joinNlines = 0;
							for (; ; nlines++) {
								if (nlines > 0) {
									int lineStart = i;
									while (i < s.Length && s[i] is '\t' or ' ') i++; //skip indentation
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
											while (++i < s.Length && s[i] <= ' ') { } //skip empty lines and indentations
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
							if (nlines < 4) continue;
						}
						if (!_IsStartOfTrivia(isLineComment)) continue;
						_AddFoldPoints(rangeStart + i0, rangeStart + i);
					}

					static bool _IsStartOfLine(ReadOnlySpan<char> s, int j/*, out int startOfLine*/) {
						while (j > 0 && s[j - 1] is '\t' or ' ') j--;
						//startOfLine = j;
						return j == 0 || s[j - 1] == '\n';
					}

					static bool _IsDotComment(ReadOnlySpan<char> s, int j, out int nDots) {
						nDots = 0;
						if (s.Eq(j, '.')) {
							nDots = 1; while (++j < s.Length && s[j] == '.') nDots++;
							if (j == s.Length || s[j] <= ' ') return true; //must be at end of line or followed by space (like //. comment). Else could be like //.Member
						}
						return false;
					}

					bool _IsStartOfTrivia(bool isLineComment) {
						//p2.First();
						int start = rangeStart + i0;
						var t = root.FindToken(start);
						//SHOULDDO: possible optimization. Instead of root.FindToken use node.FindToken, where node is of function etc whose full span contains start.
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

	public static void Fold(SciCode doc, List<FoldPoint> af) {
		int underlinedLine = 0;

		int[] a = null;
		if (af != null) {
			a = new int[af.Count];
			for (int i = 0; i < a.Length; i++) {
				var v = af[i];
				int pos8 = doc.zPos8(v.pos);
				a[i] = pos8 | (v.start ? 0 : unchecked((int)0x80000000));

				if (v.separator != 0) {
					//add separator below, or above if start
					if (v.start) { //above
						int k = v.pos - v.separator; if (k <= 0) continue;
						pos8 = doc.zPos8(k);
					}
					int li = doc.zLineFromPos(false, pos8);
					_DeleteUnderlinedLineMarkers(li);
					//if(underlinedLine != li) print.it("add", li + 1);
					if (underlinedLine != li) doc.Call(SCI_MARKERADD, li, SciCode.c_markerUnderline);
					else underlinedLine++;
				}
			}
		}

		_DeleteUnderlinedLineMarkers(int.MaxValue);

		void _DeleteUnderlinedLineMarkers(int beforeLine) {
			if ((uint)underlinedLine > beforeLine) return;
			const int marker = 1 << SciCode.c_markerUnderline;
			for (; ; underlinedLine++) {
				underlinedLine = doc.Call(SCI_MARKERNEXT, underlinedLine, marker);
				if ((uint)underlinedLine >= beforeLine) break;
				//print.it("delete", underlinedLine + 1);
				do doc.Call(SCI_MARKERDELETE, underlinedLine, SciCode.c_markerUnderline);
				while (0 != (marker & doc.Call(SCI_MARKERGET, underlinedLine)));
			}
		}

		unsafe { //we implement folding in Scintilla. Calling many SCI_SETFOLDLEVEL here would be slow.
			fixed (int* ip = a) Sci_SetFoldLevels(doc.ZSciPtr, 0, -1, a.Lenn_(), ip);
		}
		//p1.Next('f');
		doc.ERestoreEditorData_();
		//p1.NW('F');
	}

	public static void InitFolding(SciCode doc) {
		const int foldMargin = SciCode.c_marginFold;
		doc.Call(SCI_SETMARGINTYPEN, foldMargin, SC_MARGIN_SYMBOL);
		doc.Call(SCI_SETMARGINMASKN, foldMargin, SC_MASK_FOLDERS);
		doc.Call(SCI_SETMARGINSENSITIVEN, foldMargin, 1);

		doc.Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEROPEN, SC_MARK_BOXMINUS);
		doc.Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDER, SC_MARK_BOXPLUS);
		doc.Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERSUB, SC_MARK_VLINE);
		doc.Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERTAIL, SC_MARK_LCORNER);
		doc.Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEREND, SC_MARK_BOXPLUSCONNECTED);
		doc.Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDEROPENMID, SC_MARK_BOXMINUSCONNECTED);
		doc.Call(SCI_MARKERDEFINE, SC_MARKNUM_FOLDERMIDTAIL, SC_MARK_TCORNER);
		for (int i = 25; i < 32; i++) {
			doc.Call(SCI_MARKERSETFORE, i, 0xffffff);
			doc.Call(SCI_MARKERSETBACK, i, 0x808080);
			doc.Call(SCI_MARKERSETBACKSELECTED, i, i == SC_MARKNUM_FOLDER ? 0xFF : 0x808080);
		}
		//doc.Call(SCI_MARKERENABLEHIGHLIGHT, 1); //red [+]

		doc.Call(SCI_SETAUTOMATICFOLD, SC_AUTOMATICFOLD_SHOW //show hidden lines when header line deleted. Also when hidden text modified, and it is not always good.
									| SC_AUTOMATICFOLD_CHANGE); //show hidden lines when header line modified like '#region' -> '//#region'
		doc.Call(SCI_SETFOLDFLAGS, SC_FOLDFLAG_LINEAFTER_CONTRACTED);
		doc.Call(SCI_FOLDDISPLAYTEXTSETSTYLE, SC_FOLDDISPLAYTEXT_STANDARD);
		doc.zStyleForeColor(STYLE_FOLDDISPLAYTEXT, 0x808080);

		doc.Call(SCI_SETMARGINCURSORN, foldMargin, SC_CURSORARROW);

		doc.zSetMarginWidth(foldMargin, 12);

		//separator lines below functions, types and namespaces
		doc.Call(SCI_MARKERDEFINE, SciCode.c_markerUnderline, SC_MARK_UNDERLINE);
		doc.Call(SCI_MARKERSETBACK, SciCode.c_markerUnderline, 0xe0e0e0);
	}
}

partial class SciCode {
	bool _FoldOnMarginClick(bool? fold, int startPos) {
		int line = Call(SCI_LINEFROMPOSITION, startPos);
		if (0 == (Call(SCI_GETFOLDLEVEL, line) & SC_FOLDLEVELHEADERFLAG)) return false;
		bool isExpanded = 0 != Call(SCI_GETFOLDEXPANDED, line);
		if (fold.HasValue && fold.GetValueOrDefault() != isExpanded) return false;
		if (isExpanded) {
			_FoldLine(line);
			//move caret out of contracted region
			int pos = zCurrentPos8;
			if (pos > startPos) {
				int i = zLineEnd(false, Call(SCI_GETLASTCHILD, line, -1));
				if (pos <= i) zCurrentPos8 = startPos;
			}
		} else {
			Call(SCI_FOLDLINE, line, 1);
		}
		return true;
	}

	void _FoldLine(int line) {
#if false
		Call(SCI_FOLDLINE, line);
#else
		string s = zLineText(line), s2 = "";
		for (int i = 0; i < s.Length; i++) {
			char c = s[i];
			if (c == '{') { s2 = "... }"; break; }
			if (c == '/' && i < s.Length - 1) {
				c = s[i + 1];
				if (c == '*') break;
				if (i < s.Length - 3 && c == '/' && s[i + 2] == '-' && s[i + 3] == '{') break;
			}
		}
		//quite slow. At startup ~250 mcs. The above code is fast.
		if (s2.Length == 0) Call(SCI_FOLDLINE, line); //slightly faster
		else zSetString(SCI_TOGGLEFOLDSHOWTEXT, line, s2);
#endif
	}

	internal void ERestoreEditorData_() {
		//print.it(_openState);
		if (_openState == _EOpenState.FoldingDone) return;
		var os = _openState; _openState = _EOpenState.FoldingDone;

		if (os is _EOpenState.NewFileFromTemplate) {
			if (_fn.IsScript) {
				var code = zText;
				if (!code.NE()) {
					//fold all //.
					for (int i = base.zLineCount - 1; --i >= 0;) {
						int k = Call(SCI_GETFOLDLEVEL, i);
						if (0 != (k & SC_FOLDLEVELHEADERFLAG)) {
							int j = zLineEnd(false, i);
							if (zCharAt(j - 1) == '.' && zCharAt(j - 2) == '/') Call(SCI_FOLDLINE, i);
						}
					}

					if (code.RxMatch(@"//\.\.+\R\R?(?=\z|\R)", 0, out RXGroup g)) {
						zGoToPos(true, g.End);
					}
					//if (CodeInfo.GetContextAndDocument(out var cd, 0, true) && !cd.code.NE() && cd.document.GetSyntaxRootAsync().Result is CompilationUnitSyntax cu) {
					//	print.it(cu);
					//}
				}
			}
		} else if (os == _EOpenState.NewFileNoTemplate) {
		} else {
			//restore saved folding, markers, scroll position and caret position
			var db = App.Model.DB; if (db == null) return;
			try {
				using var p = db.Statement("SELECT top,pos,lines FROM _editor WHERE id=?").Bind(1, _fn.Id);
				if (p.Step()) {
					int cp = zCurrentPos8;
					int top = Math.Max(0, p.GetInt(0));
					int pos = Math.Clamp(p.GetInt(1), 0, zLen8);
					var a = p.GetList<int>(2);
					if (a != null) {
						_savedLinesMD5 = _Hash(a);
						for (int i = a.Count - 1; i >= 0; i--) {
							int v = a[i];
							int line = v & 0x7FFFFFF, marker = v >> 27 & 31;
							if (marker == 31) _FoldLine(line);
							else Call(SCI_MARKERADDSET, line, 1 << marker);
						}
						if (cp > 0) Call(SCI_ENSUREVISIBLEENFORCEPOLICY, zLineFromPos(false, cp));
					}
					if (top + pos > 0) {
						if (os != _EOpenState.Reopen) {
							db.Execute("REPLACE INTO _editor (id,top,pos,lines) VALUES (?,0,0,?)", p => p.Bind(1, _fn.Id).Bind(2, a));
						} else if (cp == 0) {
							_savedTop = top;
							_savedPos = pos;

							//workaround for:
							//	When reopening a non-first document, scrollbar position remains at the top, although scrolls the view.
							//	Scintilla calls SetScrollInfo(pos), but it does not work because still didn't call SetScrollInfo(max).
							//	Another possible workaround would be a timer, but even 50 ms is too small.
							Call(SCI_SETVSCROLLBAR, false);
							Call(SCI_SETVSCROLLBAR, true);

							if (top > 0) Call(SCI_SETFIRSTVISIBLELINE, Call(SCI_VISIBLEFROMDOCLINE, top));
							if (pos <= zLen8) {
								App.Model.EditGoBack.OnRestoringSavedPos();
								zGoToPos(false, pos);
							}

							//workaround for: in wrap mode SCI_SETFIRSTVISIBLELINE sets wrong line because still not wrapped.
							//	Don't need when saving document line, not visible line.
							//if (top > 0 && 0 != Call(SCI_GETWRAPMODE)) {
							//	timer.after(10, _ => {
							//		if (this == Panels.Editor.ZActiveDoc && zCurrentPos8 == pos)
							//			Call(SCI_SETFIRSTVISIBLELINE, top);
							//	});
							//}
						}
					}
				}
			}
			catch (SLException ex) { Debug_.Print(ex); }
		}
	}

	enum _EOpenState : byte { Open, Reopen, NewFileFromTemplate, NewFileNoTemplate, FoldingDone }
	_EOpenState _openState;

	/// <summary>
	/// Saves folding, markers etc in database.
	/// </summary>
	internal void ESaveEditorData_() {
		//CONSIDER: save styling and fold levels of the visible part of current doc. Then at startup can restore everything fast, without waiting for warmup etc.
		//_TestSaveFolding();
		//return;

		//never mind: should update folding if edited and did not fold until end. Too slow. Not important.

		if (_openState < _EOpenState.FoldingDone) return; //if did not have time to open editor data, better keep old data than delete. Also if not a code file.
		var db = App.Model.DB; if (db == null) return;
		//var p1 = perf.local();
		var a = new List<int>();
		_GetLines(c_markerBookmark, a);
		_GetLines(c_markerBreakpoint, a);
		//p1.Next();
		_GetLines(31, a);
		//p1.Next();
		var hash = _Hash(a);
		//p1.Next();
		int top = Call(SCI_GETFIRSTVISIBLELINE), pos = zCurrentPos8;
		if (top > 0) top = Call(SCI_DOCLINEFROMVISIBLE, top); //save document line, because visible line changes after changing wrap mode or resizing in wrap mode etc. Never mind: the top visible line may be not at the start of the document line.
		if (top != _savedTop || pos != _savedPos || hash != _savedLinesMD5) {
			try {
				//using var p = db.Statement("REPLACE INTO _editor (id,top,pos,lines) VALUES (?,?,?,?)");
				//p.Bind(1, _fn.Id).Bind(2, top).Bind(3, pos).Bind(4, a).Step();
				db.Execute("REPLACE INTO _editor (id,top,pos,lines) VALUES (?,?,?,?)", p => p.Bind(1, _fn.Id).Bind(2, top).Bind(3, pos).Bind(4, a));
				_savedTop = top;
				_savedPos = pos;
				_savedLinesMD5 = hash;
			}
			catch (SLException ex) { Debug_.Print(ex); }
		}
		//p1.NW('D');

		/// <summary>
		/// Gets indices of lines containing markers or contracted folding points.
		/// </summary>
		/// <param name="marker">If 31, uses SCI_CONTRACTEDFOLDNEXT. Else uses SCI_MARKERNEXT; must be 0...24 (markers 25-31 are used for folding).</param>
		/// <param name="saved">Receives line indices | marker in high-order 5 bits.</param>
		void _GetLines(int marker, List<int> a/*, int skipLineFrom = 0, int skipLineTo = 0*/) {
			Debug.Assert((uint)marker < 32); //we have 5 bits for marker
			for (int i = 0; ; i++) {
				if (marker == 31) i = Call(SCI_CONTRACTEDFOLDNEXT, i);
				else i = Call(SCI_MARKERNEXT, i, 1 << marker);
				if ((uint)i > 0x7FFFFFF) break; //-1 if no more; ensure we have 5 high-order bits for marker; max 134 M lines.
												//if(i < skipLineTo && i >= skipLineFrom) continue;
				a.Add(i | (marker << 27));
			}
		}
	}

	//unsafe void _TestSaveFolding()
	//{
	//	//int n = zLineCount;
	//	//for(int i = 0; i < n; i++) print.it(i+1, (uint)Call(SCI_GETFOLDLEVEL, i));

	//	var a = new List<POINT>();
	//	for(int i = 0; ; i++) {
	//		i = Call(SCI_CONTRACTEDFOLDNEXT, i);
	//		if(i < 0) break;
	//		int j = Call(SCI_GETLASTCHILD, i, -1);
	//		//print.it(i, j);
	//		a.Add((i, j));
	//	}

	//	Call(SCI_FOLDALL, SC_FOLDACTION_EXPAND);
	//	Sci_SetFoldLevels(SciPtr, 0, zLineCount - 1, 0, null);
	//	timer.after(1000, _ => _TestRestoreFolding(a));
	//}

	//unsafe void _TestRestoreFolding(List<POINT> lines)
	//{
	//	var a = new int[lines.Count * 2];
	//	for(int i = 0; i < lines.Count; i++) {
	//		var p = lines[i];
	//		a[i * 2] = zLineStart(false, p.x);
	//		a[i * 2 + 1] = zLineStart(false, p.y) | unchecked((int)0x80000000);
	//	}
	//	Array.Sort(a, (e1, e2) => (e1 & 0x7fffffff) - (e2 & 0x7fffffff));
	//	fixed(int* ip = a) Sci_SetFoldLevels(SciPtr, 0, zLineCount - 1, a.Length, ip);
	//}

	int _savedTop, _savedPos;
	Hash.MD5Result _savedLinesMD5;

	static Hash.MD5Result _Hash(List<int> a) {
		if (a.Count == 0) return default;
		Hash.MD5Context md5 = default;
		foreach (var v in a) md5.Add(v);
		return md5.Hash;
	}
}
