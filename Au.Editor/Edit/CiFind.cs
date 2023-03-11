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
	const int c_markerSymbol = 0, c_indicProject = 15;
	static bool _working;
	
	public static async void FindReferencesOrImplementations(bool implementations) {
		if (_working) return; //don't use cancellation. Should never be that slow.
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
				k.aaaIndicatorDefine(c_indicProject, Sci.INDIC_GRADIENT, 0xCDE87C, alpha: 255, underText: true);
			}
			
			var solution = CiProjects.GetSolutionForFindReferences(sym, cd);
			bool multiProj = solution.ProjectIds.Count > 1;
			if (implementations) {
				//TODO: sorting etc (meta c, pr)
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
				
				void _AppendSymbols(string header, IEnumerable<ISymbol> e) {
					if (e.Any()) {
						_Fold(true).Marker(c_markerSymbol).Text(header).NL();
						foreach (var v in e.OrderBy(o => o.Name)) _AppendSymbol(v);
						_Fold(false);
					}
				}
			} else {
				HashSet<_Ref> seen = new();
				_LocationComparer locComp = new();
				var options = FindReferencesSearchOptions.GetFeatureOptionsForStartingSymbol(sym);
				var rr = await SymbolFinder.FindReferencesAsync(sym, solution, options, default);
				var refSymbols = rr.Where(o => o.ShouldShow(options))
					.GroupBy(o => o.Definition.ToString(), (_, e) => (defSym: e.First().Definition, e.SelectMany(k => k.Definition.Locations), e.SelectMany(k => k.Locations))) //join duplicate definitions of logically same symbol added because of meta c or in a case of partial method
					.OrderByDescending(o => o.defSym.Kind)
					.ThenBy(o => o.defSym.ContainingAssembly?.Name != cd.document.Project.AssemblyName) //let current project be the first
					.ThenBy(o => o.defSym.ContainingAssembly?.Name);
				foreach (var (defSym, defLocs, refs) in refSymbols) {
					//definition
					
					_Fold(true).Marker(c_markerSymbol);
					_AppendSymbol(defSym, true, defLocs.Distinct(locComp));
					
					//references
					
					if (refs.Any()) {
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
					}
					
					_Fold(false);
				}
			}
			//BAD: no results for meta testInternal internals.
			
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
		}
		finally { _working = false; }
	}
	
	record struct _Ref(FileNode f, TextSpan span);
	
	class _LocationComparer : IEqualityComparer<Location> {
		public bool Equals(Location x, Location y) => x.IsInSource && y.IsInSource && x.SourceSpan == y.SourceSpan && x.SourceTree.FilePath == y.SourceTree.FilePath;
		
		public int GetHashCode(Location x) => (int)x.Kind;
	}
	
	public static void SciUpdateUI(SciCode doc, bool modified) {
		doc.aaaIndicatorClear(SciCode.c_indicRefs);
		doc.aaaIndicatorClear(SciCode.c_indicBraces);
		_cancelTS?.Cancel();
		_cancelTS = null;
		_doc = doc;
		_timer1.After(modified ? 1000 : 250);
	}
	
	static readonly timer _timer1 = new(_SciUpdateUI);
	static CancellationTokenSource _cancelTS;
	static SciCode _doc;
	
	static async void _SciUpdateUI(timer _1) {
		if (!CodeInfo.GetContextAndDocument(out var cd) || cd.sci != _doc) return;
		
		//hilite symbol references
		if (CiUtil.GetSymbolFromPos(cd) is ISymbol sym) {
			List<Range> ar = new();
			_cancelTS = new CancellationTokenSource();
			var cancelToken = _cancelTS.Token;
			try {
				var options = FindReferencesSearchOptions.GetFeatureOptionsForStartingSymbol(sym);
				var rr = await SymbolFinder.FindReferencesAsync(sym, cd.document.Project.Solution, (IFindReferencesProgress)null, ImmutableHashSet.Create(cd.document), options, cancelToken);
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
						var span = rloc.Location.SourceSpan;
						if (rloc.Document == cd.document) {
							ar.Add(span.ToRange());
						}
					}
				}
				
				foreach (var v in ar) cd.sci.aaaIndicatorAdd(true, SciCode.c_indicRefs, v);
			}
			catch (OperationCanceledException) { }
			finally {
				_cancelTS?.Dispose();
				_cancelTS = null;
			}
		}
		//See also: Roslyn -> AbstractDocumentHighlightsService.cs.
		
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
			cd.sci.aaaIndicatorAdd(true, SciCode.c_indicBraces, posL..(posL + 1));
			cd.sci.aaaIndicatorAdd(true, SciCode.c_indicBraces, posR..(posR + 1));
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
			foreach (var v in a) cd.sci.aaaIndicatorAdd(true, SciCode.c_indicBraces, v.Span.ToRange());
		}
	}
	
	public static void RenameSymbol() {//TODO
		print.clear();
		var (sym, cd) = CiUtil.GetSymbolFromPos();
		if (sym == null) return;
		var solution = cd.document.Project.Solution;
		//print.it(solution.Projects.Select(o=>o.Name));
		var sol2 = Microsoft.CodeAnalysis.Rename.Renamer.RenameSymbolAsync(solution, sym, new SymbolRenameOptions(), "newName").Result;
		//print.it(sol2.GetChangedDocuments(solution));
		
		foreach (var projChange in sol2.GetChanges(solution).GetProjectChanges()) {
			print.it("PROJECT", projChange.NewProject.Name);
			foreach (var docId in projChange.GetChangedDocuments(true)) {
				var doc = projChange.NewProject.GetDocument(docId);
				print.it("FILE", doc.Name);
				var oldDoc = projChange.OldProject.GetDocument(docId);
				foreach (var tc in doc.GetTextChangesAsync(oldDoc).Result) {
					print.it(tc);
				}
			}
		}
		//foreach (var projChange in sol2.GetChanges(solution).GetProjectChanges()) {
		//	print.it("PROJECT", projChange.NewProject.Name);
		//	foreach (var docId in projChange.GetChangedDocuments(true)) {
		//		var doc = projChange.NewProject.GetDocument(docId);
		//		print.it("FILE", doc.Name);
		//		var oldDoc = projChange.OldProject.GetDocument(docId);
		//		foreach (var tc in doc.GetTextChangesAsync(oldDoc).Result) {
		//			print.it(tc);
		//		}
		//	}
		//}
	}
}
