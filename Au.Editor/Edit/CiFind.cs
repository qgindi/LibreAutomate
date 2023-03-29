using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

//TODO: doc about 'Find references/implementations', 'hilite references', 'go to base'.

static class CiFind {
	const int c_markerSymbol = 0, c_markerInfo = 1, c_markerSeparator = 2, c_indicProject = 15;
	const int c_markerRenameComment = 3, c_markerRenameDisabled = 4, c_markerRenameString = 5, c_markerRenameError = 6;
	static bool _working;
	
	public static async void FindReferencesOrImplementations(bool implementations) {
		if (_working) return;
		_working = true;
		try {
			var (sym, cd) = CiUtil.GetSymbolFromPos();
			if (sym == null) {
				Panels.Found.ClearResults(PanelFound.Found.SymbolReferences);
				return;
			}
			
			using var workingState = Panels.Found.Prepare(PanelFound.Found.SymbolReferences, sym.Name, out var b);
			if (workingState.NeedToInitControl) {
				var k = workingState.Scintilla;
				k.aaaMarkerDefine(c_markerSymbol, Sci.SC_MARK_BACKGROUND, backColor: 0xC0C0FF);
				k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
				k.aaaMarkerDefine(c_markerSeparator, Sci.SC_MARK_UNDERLINE, backColor: 0xe0e0e0);
				k.aaaIndicatorDefine(c_indicProject, Sci.INDIC_GRADIENT, 0xC0C0C0, alpha: 255, underText: true);
			}
			
			//perf.first();
			Au.Compiler.TestInternal.RefsStart();
			var (solution, info) = await CiProjects.GetSolutionForFindReferences(sym, cd);
			
			//perf.next('s');
			bool multiProj = solution.ProjectIds.Count > 1;
			_LocationComparer locComp = new();
			var symComp = new _SymbolComparer(locComp);
			if (implementations) {
				if (sym is INamedTypeSymbol nts) {
					if (nts.TypeKind == TypeKind.Interface) {
						_AppendSymbols("Implementations", await SymbolFinder.FindImplementationsAsync(nts, solution));
						_AppendSymbols("Derived interfaces", await SymbolFinder.FindDerivedInterfacesAsync(nts, solution));
					} else {
						_AppendSymbols("Derived classes", await SymbolFinder.FindDerivedClassesAsync(nts, solution));
					}
				} else if (sym.ContainingType is INamedTypeSymbol nts2) {
					if (nts2.TypeKind == TypeKind.Interface) {
						_AppendSymbols("Implementations", await SymbolFinder.FindImplementationsAsync(sym, solution));
					} else {
						_AppendSymbols("Overrides", await SymbolFinder.FindOverridesAsync(sym, solution));
					}
				}
				if (b.Length == 0) return;
				//never mind: Roslyn gives 1 result when implemented in 2 projects and same type name and assembly name.
				//	Same in VS.
				//	We could generate unique assembly names, but it breaks [InternalsVisibleTo] and testInternal.
				//	Too difficult to modify Roslyn code.
				
				void _AppendSymbols(string header, IEnumerable<ISymbol> e) {
					e = e.Where(o => o.IsInSource());
					if (e.Any()) {
						//join duplicates added through meta c
						var implSymbols = e.GroupBy(o => o, (k, e) => (sym: k, locs: e.SelectMany(o => o.Locations).Distinct(locComp)), symComp)
							.OrderBy(o => o.sym.Name);
						
						_Fold(true).Marker(c_markerSymbol).Text(header).NL();
						foreach (var v in implSymbols) _AppendSymbol(v.sym, false, v.locs);
						_Fold(false);
					}
				}
			} else {
				HashSet<_Ref> seen = new();
				var options = FindReferencesSearchOptions.GetFeatureOptionsForStartingSymbol(sym);
				var rr = await SymbolFinder.FindReferencesAsync(sym, solution, options, default);
				//perf.next('f');
				
				//sort. Join duplicate definitions of logically same symbol added through meta c or in a case of partial method.
				var refSymbols = rr.Where(o => o.ShouldShow(options))
					.GroupBy(o => o.Definition, (defSym, e) => (defSym, e.SelectMany(k => k.Definition.Locations), e.SelectMany(k => k.Locations)), symComp)
					.OrderByDescending(o => o.defSym.Kind)
					.ThenBy(o => o.defSym.ContainingAssembly?.Name != cd.document.Project.AssemblyName) //let current project be the first
					.ThenBy(o => o.defSym.ContainingAssembly?.Name);
				
				foreach (var (defSym, defLocs, refs) in refSymbols) {
					if (refs.Any()) {
						//definition
						_Fold(true).Marker(c_markerSymbol);
						_AppendSymbol(defSym, true, defLocs.Distinct(locComp));
						
						//references
						if (multiProj) _Fold(true);
						ProjectId prevProjId = null;
						FileNode prevFile = null;
						var refs2 = refs
							.OrderBy(o => o.Document.Project.Id != cd.document.Project.Id) //let current project be the first
							.ThenBy(o => o.Document.Project.Name)
							.ThenBy(o => o.Document.Name);
						foreach (var rloc in refs2) {
							var f = CiProjects.FileOf(rloc.Document);
							var span = rloc.Location.SourceSpan;
							if (!seen.Add(new(f, span))) continue; //remove logical duplicates added because of meta c
							if (!rloc.Document.TryGetText(out var st)) { Debug_.Print(f); continue; }
							var text = st.ToString();
							
							if (f != prevFile) {
								if (prevFile != null) b.Marker(c_markerSeparator, prevLine: true);
								prevFile = f;
							}
							
							if (multiProj && rloc.Document.Project.Id != prevProjId) {
								prevProjId = rloc.Document.Project.Id;
								_Fold(false);
								_Fold(true);
								b.Indic(c_indicProject).Text("Project ").B(rloc.Document.Project.Name).Indic_().NL();
							}
							
							PanelFound.AppendFoundLine(b, f, text, span.Start, span.End, displayFile: true, indicHilite: PanelFound.Indicators.HiliteG);
						}
						if (multiProj) _Fold(false);
						
						_Fold(false);
					}
				}
			}
			//perf.next();
			
			if (!info.NE()) b.Marker(c_markerInfo).Text(info).NL();
			
			void _AppendSymbol(ISymbol sym, bool refDef = false, IEnumerable<Location> locations = null) {
				var sDef = sym is INamespaceSymbol
					? sym.ToString() /* with ancestor namespaces */
					: sym.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat); /* no namespaces, no param names, no return type */
				bool isInSource = false;
				foreach (var loc in locations ?? sym.Locations) {
					if (!loc.IsInSource) continue;
					FileNode f = CiProjects.FileOf(loc.SourceTree, solution);
					if (isInSource) b.Text("   ");
					b.Link2(new PanelFound.CodeLink(f, loc.SourceSpan.Start, loc.SourceSpan.End));
					if (!isInSource) { if (refDef) b.B(sDef); else b.Text(sDef); b.Text("      "); }
					b.Gray(f.Name);
					b.Link_();
					isInSource = true;
					//if (!refDef && multiProj) b.Text($" ({solution.GetDocument(loc.SourceTree)?.Project.Name})"); //bad if many partials
				}
				if (!isInSource) { if (refDef) b.B(sDef); else b.Text(sDef); }
				b.NL();
			}
			
			SciTextBuilder _Fold(bool start) => b.Fold(new(b.Length - (start ? 0 : 2), start));
			
			Panels.Found.SetResults(workingState, b);
			
			timer.after(1, _ => GC.Collect());
			//perf.nw();
		}
		finally {
			_working = false;
			Au.Compiler.TestInternal.RefsEnd();
		}
	}
	
	record struct _Ref(FileNode f, TextSpan span);
	
	class _LocationComparer : IEqualityComparer<Location> {
		public bool Equals(Location x, Location y)
			=> x.IsInSource && y.IsInSource && x.SourceSpan == y.SourceSpan && x.SourceTree.FilePath == y.SourceTree.FilePath;
		
		public int GetHashCode(Location x)
			=> x.Kind == LocationKind.SourceFile ? x.SourceSpan.Start : x.GetHashCode();
	}
	
	class _SymbolComparer : IEqualityComparer<ISymbol> {
		_LocationComparer _locComp;
		
		public _SymbolComparer(_LocationComparer locComp) { _locComp = locComp; }
		
		//public bool Equals(ISymbol x, ISymbol y) => x.Locations.Intersect(y.Locations, _locComp).Any();
		public bool Equals(ISymbol x, ISymbol y) { //faster, no garbage
			foreach (var xl in x.Locations)
				foreach (var yl in y.Locations)
					if (_locComp.Equals(xl, yl)) return true;
			return false;
		}
		
		public int GetHashCode(ISymbol x) => x.Name.GetHashCode();
	}
	
	public static void SciUpdateUI(SciCode doc, bool modified) {
		if (modified || 0 == doc.aaaIndicGetValue(SciCode.c_indicRefs, doc.aaaCurrentPos8) || doc.aaaHasSelection)
			doc.aaaIndicatorClear(SciCode.c_indicRefs);
		doc.aaaIndicatorClear(SciCode.c_indicBraces);
		_cancelTS?.Cancel();
		_cancelTS = null;
		_doc = doc;
		//_timer1.After(modified ? 1000 : 200);
		if (!modified) _timer1.After(200);
	}
	
	static readonly timer _timer1 = new(_SciUpdateUI);
	static CancellationTokenSource _cancelTS;
	static SciCode _doc;
	
	static async void _SciUpdateUI(timer _1) {
		if (!CodeInfo.GetContextAndDocument(out var cd) || cd.sci != _doc) return;
		if (cd.sci.aaaHasSelection) return;
		
		//hilite symbol references
		if (CiUtil.GetSymbolFromPos(cd) is ISymbol sym) {
			if (sym is IAliasSymbol a1 && a1.Target is INamespaceSymbol ns1 && ns1.ContainingNamespace == null) return; //'global' keyword. Would hilite entire code.
			List<Range> ar = new();
			_cancelTS = new CancellationTokenSource();
			var cancelToken = _cancelTS.Token;
			try {
				var options = FindReferencesSearchOptions.GetFeatureOptionsForStartingSymbol(sym);
				
				var solution = cd.document.Project.Solution;
				var ihs = ImmutableHashSet.Create(cd.document);
				var rr = await SymbolFinder.FindReferencesAsync(sym, solution, (IFindReferencesProgress)null, ihs, options, cancelToken);
				
				if (Panels.Editor.ActiveDoc != cd.sci) return;
				
				foreach (var v in rr) {
					if (!v.ShouldShow(options)) continue;
					
					//definition
					foreach (var loc in v.Definition.Locations) {
						if (loc.SourceTree == cd.syntaxRoot.SyntaxTree) {
							ar.Add(loc.SourceSpan.ToRange());
						}
					}
					
					//references
					foreach (var rloc in v.Locations) {
						if (rloc.Document == cd.document) {
							var span = rloc.Location.SourceSpan;
							if (span.Length == 0 && span.Start < cd.code.Length) span = new(span.Start, 1); //indexer
							ar.Add(span.ToRange());
						}
					}
				}
				
				foreach (var v in ar) cd.sci.aaaIndicatorAdd(SciCode.c_indicRefs, true, v);
			}
			catch (OperationCanceledException) { }
			finally {
				_cancelTS?.Dispose();
				_cancelTS = null;
			}
		}
		//See also: Roslyn -> AbstractDocumentHighlightsService.cs.
		
		//CONSIDER: now brace hiliting is distracting and I as a user rarely need it.
		//	Maybe hilite only when a single brace selected.
		//	But other IDEs hilite like this.
		_HighlightMatchingBracesOrDirectives(cd);
	}
	
	static void _HighlightMatchingBracesOrDirectives(CodeInfo.Context cd) {
		string code = cd.code;
		int pos = cd.pos;
		char chL = pos <= code.Length - 2 ? code[pos] : default, chR;
		if (chL is '(' or '[' or '{' or '<') {
			chR = chL switch { '(' => ')', '[' => ']', '{' => '}', _ => '>' };
			_Brace(false);
		} else if (chL == '#' && InsertCodeUtil.IsLineStart(code, pos)) {
			_Directive();
		}
		
		chR = --pos >= 0 ? code[pos] : default;
		if (chR is ')' or ']' or '}' or '>') {
			chL = chR switch { ')' => '(', ']' => '[', '}' => '{', _ => '<' };
			_Brace(true);
		} else if (chR == '#' && InsertCodeUtil.IsLineStart(code, pos)) {
			_Directive();
		}
		
		void _Brace(bool isPrevChar) {
			var (kL, kR) = chL switch {
				'(' => (SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken),
				'[' => (SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken),
				'{' => (SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken),
				_ => (SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken)
			};
			var token = cd.syntaxRoot.FindToken(pos);
			var span = token.Span;
			if (span.Start != pos || span.Length != 1) return;
			if (!token.IsKind(isPrevChar ? kR : kL)) return;
			var node = token.Parent;
			int posL = -1, posR = -1;
			foreach (var v in node.ChildTokens()) {
				if (posL < 0) {
					if (v.IsKind(kL) && v.Span.Length == 1) posL = v.SpanStart;
				} else if (posR < 0) {
					if (v.IsKind(kR) && v.Span.Length == 1) posR = v.SpanStart;
				}
			}
			if (posR < 0 || !(posL == pos || posR == pos)) return;
			if (posR - posL < 4) return; //don't hilite () etc
			cd.sci.aaaIndicatorAdd(SciCode.c_indicBraces, true, posL..(posL + 1));
			cd.sci.aaaIndicatorAdd(SciCode.c_indicBraces, true, posR..(posR + 1));
		}
		
		void _Directive() {
			if (cd.syntaxRoot.FindToken(pos, findInsideTrivia: true).Parent is not DirectiveTriviaSyntax node) return;
			DirectiveTriviaSyntax[] a = null;
			if (node is IfDirectiveTriviaSyntax or ElseDirectiveTriviaSyntax or ElifDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax) {
				var m = node.GetMatchingConditionalDirectives(default);
				if (m?.Count > 1) a = m.ToArray();
			} else {
				var node2 = node.GetMatchingDirective(default);
				if (node2 != null) a = new[] { node, node2 };
			}
			if (a == null) return;
			foreach (var v in a) cd.sci.aaaIndicatorAdd(SciCode.c_indicBraces, true, v.Span.ToRange());
		}
	}
	
	public static void RenameSymbol() {
		if (_working) return;
		_working = true;
		try { new _Renamer().Rename(); }
		finally { _working = false; }
	}
	
	class _Renamer {
		string _oldName, _newName, _info;
		bool _overloads, _comments, _strings, _preview;
		List<_File> _a;
		KScintilla _sciPreview;
		static WeakReference<KScintilla> s_sciPreview;
		
		public async void Rename() {
			if (s_sciPreview?.TryGetTarget(out var sciPreview) ?? false) Panels.Found.Close(sciPreview);
			s_sciPreview = null;
			
			//print.clear();
			if (!CodeInfo.GetContextAndDocument(out var cd)) return;
			
			//int indic = 6; cd.sci.aaaIndicatorDefine(indic, Sci.INDIC_GRADIENT, 0x0000ff, 120); cd.sci.aaaIndicatorClear(indic);
			
			var sym = await Microsoft.CodeAnalysis.Rename.RenameUtilities.TryGetRenamableSymbolAsync(cd.document, cd.pos, default);
			
			if (sym == null || !sym.IsInSource() || sym.IsImplicitlyDeclared || !sym.Locations[0].FindToken(default).IsKind(SyntaxKind.IdentifierToken)) {
				dialog.show("This element cannot be renamed", owner: App.Hmain);
				return;
			}
			
			if (!_Dialog(sym)) return;
			
			//using var p1 = perf.local();
			Solution solution;
			ImmutableArray<RenameLocation> locs;
			Au.Compiler.TestInternal.RefsStart();
			try {
				(solution, _info) = await CiProjects.GetSolutionForFindReferences(sym, cd);
				//p1.Next('s');
				
				SymbolRenameOptions sro = new(_overloads, _strings, _comments);
				locs = (await Microsoft.CodeAnalysis.Rename.Renamer.FindRenameLocationsAsync(solution, sym, sro, default, default)).Locations;
			}
			finally { Au.Compiler.TestInternal.RefsEnd(); }
			//p1.Next('f');
			
			Dictionary<FileNode, _File> df = new();
			HashSet<_Ref> seen = new();
			foreach (var v in locs) {
				var f = CiProjects.FileOf(v.DocumentId);
				var span = v.Location.SourceSpan;
				if (!seen.Add(new(f, span))) continue; //remove logical duplicates added because of meta c
				
				//if (f == cd.sci.EFile) cd.sci.aaaIndicatorAdd(indic, true, span.ToRange());
				//print.it(v.Location.SourceSpan, v.IsWrittenTo, v.CandidateReason, v.IsRenameInStringOrComment);
				
				if (!df.TryGetValue(f, out var x)) {
					if (!solution.GetDocument(v.DocumentId).TryGetText(out var st)) { Debug_.Print(f); continue; }
					df.Add(f, x = new(f, st.ToString(), new()));
				}
				
				int marker = 0;
				if (v.IsRenameInStringOrComment) {
					var trivia = v.Location.SourceTree.GetRoot(default).FindTrivia(v.Location.SourceSpan.Start);
					marker = trivia.Kind() switch { SyntaxKind.None => c_markerRenameString, SyntaxKind.DisabledTextTrivia => c_markerRenameDisabled, _ => c_markerRenameComment };
				} else if (v.CandidateReason != CandidateReason.None) marker = c_markerRenameError;
				if (marker > 0) _preview = true;
				
				x.a.Add(new(span.Start, marker));
			}
			_a = df.Values.OrderBy(o => o.f.Name).ToList();
			foreach (var v in _a) v.a.Sort((x, y) => x.pos - y.pos);
			
			//p1.Next();
			
			if (_preview) _Preview();
			else _Finish();
		}
		
		bool _Dialog(ISymbol sym) {
			_oldName = sym.Name;
			bool hasOverloads = Microsoft.CodeAnalysis.Rename.RenameUtilities.GetOverloadedSymbols(sym).Any();
			
			var b = new wpfBuilder("Rename symbol").WinSize(300);
			b.WinProperties(WindowStartupLocation.CenterOwner, showInTaskbar: false);
			b.R.Add(out TextBox newName, _oldName).Focus(); newName.SelectAll();
			b.R.Add(out KCheckBox cOverloads, "Include overloads").Hidden(!hasOverloads).Checked(0 == (App.Settings.ci_rename & 1));
			b.R.Add(out KCheckBox cComments, "Include comments and #if-disabled code").Checked(0 == (App.Settings.ci_rename & 2)).Tooltip("If found, before renaming will show in the Found panel.");
			b.R.Add(out KCheckBox cStrings, "Include strings").Checked(0 == (App.Settings.ci_rename & 4)).Tooltip("If found, before renaming will show in the Found panel.");
			//b.R.Add(out KCheckBox cPreview, "Preview");
			b.R.AddOkCancel();
			b.End();
			if (!b.ShowDialog(App.Wmain)) return false;
			_overloads = cOverloads.IsChecked;
			_comments = cComments.IsChecked;
			_strings = cStrings.IsChecked;
			//_preview = cPreview.IsChecked;
			App.Settings.ci_rename = (_overloads ? 0 : 1) | (_comments ? 0 : 2) | (_strings ? 0 : 4);
			_newName = newName.Text;
			return _newName != _oldName;
		}
		
		void _Preview() {
			using var workingState = Panels.Found.Prepare(PanelFound.Found.SymbolRename, "Renaming", out var b);
			if (workingState.NeedToInitControl) {
				var k = workingState.Scintilla;
				k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
				k.aaaMarkerDefine(c_markerSeparator, Sci.SC_MARK_UNDERLINE, backColor: 0xe0e0e0);
				k.aaaMarginSetWidth(1, 12);
				k.aaaMarkerDefine(c_markerRenameComment, Sci.SC_MARK_SMALLRECT, backColor: 0x80C000);
				k.aaaMarkerDefine(c_markerRenameDisabled, Sci.SC_MARK_SMALLRECT, backColor: 0);
				k.aaaMarkerDefine(c_markerRenameString, Sci.SC_MARK_SMALLRECT, backColor: 0xC09060);
				k.aaaMarkerDefine(c_markerRenameError, Sci.SC_MARK_SMALLRECT, backColor: 0xFF0000);
			}
			
			FileNode prevFile = null;
			foreach (var (f, text, a) in _a) {
				foreach (var v in a) {
					//if (v.marker > 0) b.Marker(v.marker);
					if (v.marker == 0) continue;
					b.Marker(v.marker);
					if (f != prevFile) { if (prevFile != null) b.Marker(c_markerSeparator, prevLine: true); prevFile = f; }
					PanelFound.AppendFoundLine(b, f, text, v.pos, v.pos + _oldName.Length, displayFile: true, indicHilite: PanelFound.Indicators.HiliteB);
					//rejected: in results display newName (hilited) as if already replaced. Or old red and new green, like in VSCode.
				}
			}
			
			b.Marker(c_markerInfo).Text("You may want to exclude some of these. Right-click.\r\n");
			b.Marker(c_markerInfo)
				.B().Link(() => _Link(false), "Rename").B_()
				.Text("  ").Link(() => _Link(true), "Cancel").NL();
			b.Marker(c_markerInfo).Text("Margin markers: green - comment, black - #if, brown - string, red - error or ambiguous.");
			if (!_info.NE()) b.NL().Marker(c_markerInfo).Text(_info);
			
			Panels.Found.SetResults(workingState, b);
			s_sciPreview = new(_sciPreview = workingState.Scintilla);
		}
		
		void _Link(bool cancel) {
			//async because cannot close Scintilla from link click notification
			App.Dispatcher.InvokeAsync(() => {
				if (!cancel) _Finish();
				Panels.Found.Close(_sciPreview);
			});
			s_sciPreview = null;
		}
		
		void _Finish() {
			foreach (var (f, text, _) in _a) {
				if (f.GetCurrentText(out var t1, null) && t1 != text) {
					dialog.show(null, $"Cannot rename symbol, because '{f.Name}' text changed in the meantime.", owner: App.Hmain);
					return;
				}
			}
			
			if (_sciPreview != null) { //remove excluded items
				int line = 0;
				for (int j = 0; j < _a.Count; j++) {
					var a = _a[j].a;
					for (int i = 0; i < a.Count; i++) {
						if (a[i].marker == 0) continue;
						if (0 != _sciPreview.aaaIndicGetValue(PanelFound.Indicators.Excluded, _sciPreview.aaaLineStart(false, line++)))
							a.RemoveAt(i--);
					}
					if (a.Count == 0) _a.RemoveAt(j--);
				}
				if (_a.Count == 0) return;
			}
			
			//var progress = App.Hmain.TaskbarButton;
			var undoInFiles = SciUndo.OfWorkspace;
			try {
				undoInFiles.StartReplaceInFiles();
				foreach (var (f, text, a1) in _a) {
					var a = a1.Select(o => new StartEndText(o.pos, o.pos + _oldName.Length, _newName)).ToList();
					if (f.ReplaceAllInText(text, a, out var text2)) {
						if (f.OpenDoc != null) undoInFiles.RifAddFile(f.OpenDoc, text, text2, a);
						else undoInFiles.RifAddFile(f, text, text2, a);
					}
				}
			}
			finally {
				undoInFiles.FinishReplaceInFiles($"rename symbol {_oldName} to {_newName}");
				//progress.SetProgressState(WTBProgressState.NoProgress);
				CodeInfo.FilesChanged();
			}
		}
		
		record struct _Pos(int pos, int marker);
		record struct _File(FileNode f, string text, List<_Pos> a);
	}
}
