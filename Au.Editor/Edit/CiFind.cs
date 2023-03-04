using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.FindSymbols;
using Au.Controls;

//TODO: now if in Au.sln searching from eg Au project, does not search in other projects.
//TODO: doc about 'Find references' etc.

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
			
			var solution = cd.document.Project.Solution;
			//print.it(solution.Projects.Select(o=>o.Name));
			bool multiProj = solution.ProjectIds.Count > 1;
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
				
				void _AppendSymbols(string header, IEnumerable<ISymbol> e) {
					if (e.Any()) {
						_Fold(true).Marker(c_markerSymbol).Text(header).NL();
						foreach (var v in e.OrderBy(o => o.Name)) _AppendSymbol(v);
						_Fold(false);
					}
				}
			} else {
				var options = FindReferencesSearchOptions.GetFeatureOptionsForStartingSymbol(sym);
				var rr = await SymbolFinder.FindReferencesAsync(sym, solution, options, default);
				foreach (var v in rr) {
					if (!v.ShouldShow(options)) continue;
					
					//definition
					
					var def = v.Definition;
					_Fold(true).Marker(c_markerSymbol);
					_AppendSymbol(def, true);
					
					//references
					
					if (multiProj) _Fold(true);
					Project prevProj = null;
					foreach (var rloc in v.Locations.OrderBy(o => o.Document.Project.Name, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Document.Name, StringComparer.OrdinalIgnoreCase)) {
						var f = CodeInfo.FileOf(rloc.Document);
						var span = rloc.Location.SourceSpan;
						if (!f.GetCurrentText(out var text)) { Debug_.Print(f); continue; }
						
						if (multiProj && (object)rloc.Document.Project != prevProj) {
							prevProj = rloc.Document.Project;
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
			//BAD: no results for meta testInternal internals.
			
			void _AppendSymbol(ISymbol sym, bool refDef = false) {
				var sDef = sym.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat); /*no namespaces, no param names, no return type*/
				bool isInSource = false;
				foreach (var loc in sym.Locations) {
					if (!loc.IsInSource) continue;
					FileNode f = CodeInfo.FileOf(loc.SourceTree);
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
}
