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

//TODO: doc about 'Find references/implementations', 'hilite references', 'go to base'.

static class CiFind {
	const int c_markerSymbol = 0, c_markerInfo = 1, c_indicProject = 15;
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
				k.aaaMarkerDefine(c_markerSymbol, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
				k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xADC8FF);
				k.aaaIndicatorDefine(c_indicProject, Sci.INDIC_GRADIENT, 0xCDE87C, alpha: 255, underText: true);
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
							
							if (multiProj && rloc.Document.Project.Id != prevProjId) {
								prevProjId = rloc.Document.Project.Id;
								_Fold(false);
								_Fold(true);
								b.Indic(c_indicProject).Text("Project ").B(rloc.Document.Project.Name).Indic_().NL();
							}
							
							PanelFound.AppendFoundLine(b, f, text, span.Start, span.End, displayFile: true);
						}
						if (multiProj) _Fold(false);
						
						_Fold(false);
					}
				}
			}
			//perf.next();
			
			if (!info.NE()) {
				b.Marker(c_markerInfo).Text(info).NL();
			}
			
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
			
			Panels.Found.SetSymbolReferencesResults(workingState, b);
			
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
		if (modified || 0 == doc.Call(Sci.SCI_INDICATORVALUEAT, SciCode.c_indicRefs, doc.aaaCurrentPos8) || doc.aaaHasSelection)
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
	
	#if true
	public static async void RenameSymbol() {//TODO
		print.clear();
		if (_working) return;
		_working = true;
		try {
			var (sym, cd) = CiUtil.GetSymbolFromPos();
			if (sym == null || !sym.IsInSource()) return;
			
			perf.first();
			Au.Compiler.TestInternal.RefsStart();
			var (solution, info) = await CiProjects.GetSolutionForFindReferences(sym, cd);
			perf.next('s');
			SymbolRenameOptions sro = new();
			
			var h = await Microsoft.CodeAnalysis.Rename.Renamer.FindRenameLocationsAsync(solution, sym, sro, default, default);
			print.it(h.Locations.Length);
			//return;
			perf.next('L');
			
			var sol2 = await Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(solution, sym, sro, "newName");
			//print.it(sol2.GetChangedDocuments(solution));
			perf.next('r');
			
			int n=0;
			foreach (var projChange in sol2.GetChanges(solution).GetProjectChanges()) {
				print.it("<><c blue>PROJECT", projChange.NewProject.Name, "<>");
				foreach (var docId in projChange.GetChangedDocuments(true)) {
					var doc = projChange.NewProject.GetDocument(docId);
					print.it("FILE", doc.Name);
					var oldDoc = projChange.OldProject.GetDocument(docId);
					foreach (var tc in doc.GetTextChangesAsync(oldDoc).Result) {
						print.it(tc);
						n++;
					}
				}
			}
			perf.nw();
			print.it(n);
		}
		finally {
			_working = false;
			Au.Compiler.TestInternal.RefsEnd();
		}
	}
	#else
	public static async void RenameSymbol() {//TODO
		print.clear();
		if (_working) return;
		_working = true;
		try {
			var (sym, cd) = CiUtil.GetSymbolFromPos();
			if (sym == null || !sym.IsInSource()) return;
			
			perf.first();
			Au.Compiler.TestInternal.RefsStart();
			var (solution, info) = await CiProjects.GetSolutionForFindReferences(sym, cd);
			perf.next('s');
			SymbolRenameOptions sro = new();
			var sol2 = await Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(solution, sym, sro, "newName");
			//print.it(sol2.GetChangedDocuments(solution));
			perf.next('r');
			
			foreach (var projChange in sol2.GetChanges(solution).GetProjectChanges()) {
				print.it("<><c blue>PROJECT", projChange.NewProject.Name, "<>");
				foreach (var docId in projChange.GetChangedDocuments(true)) {
					var doc = projChange.NewProject.GetDocument(docId);
					print.it("FILE", doc.Name);
					var oldDoc = projChange.OldProject.GetDocument(docId);
					foreach (var tc in doc.GetTextChangesAsync(oldDoc).Result) {
						print.it(tc);
					}
				}
			}
			perf.nw();
		}
		finally {
			_working = false;
			Au.Compiler.TestInternal.RefsEnd();
		}
	}
	#endif
}
