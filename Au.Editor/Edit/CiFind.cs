extern alias CAW;

using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.FindSymbols;
using CAW::Microsoft.CodeAnalysis.Rename;
using CAW::Microsoft.CodeAnalysis.Rename.ConflictEngine;

using Au.Controls;
using static Au.Controls.Sci;

static class CiFind {
	const int c_markerSymbol = 0, c_markerInfo = 1, c_markerSeparator = 2, c_indicProject = 15;
	const int c_markerRenameComment = 3, c_markerRenameDisabled = 4, c_markerRenameString = 5, c_markerRenameError = 6;
	const int c_marginUsage = 1;
	const int c_marginStyleBlack = 50, c_marginStyleWrite = 51, c_marginStyleRead = 52;
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
			
			using var workingState = Panels.Found.Prepare(PanelFound.Found.SymbolReferences, sym.JustName(), out var b);
			var k = workingState.Scintilla;
			if (workingState.NeedToInitControl) {
				k.aaaMarkerDefine(c_markerSymbol, SC_MARK_BACKGROUND, backColor: 0xC0C0FF);
				k.aaaMarkerDefine(c_markerInfo, SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
				k.aaaMarkerDefine(c_markerSeparator, SC_MARK_UNDERLINE, backColor: 0xe0e0e0);
				k.aaaIndicatorDefine(c_indicProject, INDIC_GRADIENT, 0xC0C0C0, alpha: 255, underText: true);
				k.aaaMarginSetType(c_marginUsage, SC_MARGIN_TEXT);
				k.aaaStyleBackColor(c_marginStyleBlack, 0xE0E0E0);
				k.aaaStyleForeColor(c_marginStyleBlack, 0);
				k.aaaStyleBackColor(c_marginStyleWrite, 0xE0E0E0);
				k.aaaStyleForeColor(c_marginStyleWrite, 0x0000ff);
				k.aaaStyleBackColor(c_marginStyleRead, 0xE0E0E0);
				k.aaaStyleForeColor(c_marginStyleRead, 0x008000);
			}
			k.aaaMarginSetWidth(c_marginUsage, 0, implementations ? 0 : 7);
			
			//perf.first();
			Au.Compiler.TestInternal.RefsStart();
			var (solution, info) = await CiProjects.GetSolutionForFindReferences(sym, cd);
			
			//perf.next('s');
			bool multiProj = solution.ProjectIds.Count > 1;
			_LocationComparer locComp = new();
			var symComp = new _SymbolComparer(locComp);
			List<(int pos, byte kind, ushort usage)> aUsage = new(); //kind: 1 TypeOrNamespaceUsageInfo, 2 ValueUsageInfo
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
				HashSet<_Seen> seen = new();
				var options = FindReferencesSearchOptions.GetFeatureOptionsForStartingSymbol(sym);
				var rr = await SymbolFinder.FindReferencesAsync(sym, solution, options, default);
				//perf.next('f');
				
				//sort. Join duplicate definitions of logically same symbol added through meta c or in a case of partial method.
				var refSymbols = rr.Where(o => o.ShouldShow(options))
					.GroupBy(o => o.Definition, (defSym, e) => (defSym, defLocs: e.SelectMany(k => k.Definition.Locations), refs: e.SelectMany(k => k.Locations)), symComp)
					.OrderByDescending(o => o.defSym.Kind)
					.ThenBy(o => o.defSym.ContainingAssembly?.Name != cd.document.Project.AssemblyName) //let current project be the first
					.ThenBy(o => o.defSym.ContainingAssembly?.Name);
				
				bool allInThisDoc = refSymbols.All(o => o.refs.All(oo => oo.Document == cd.document));
				if (allInThisDoc) multiProj = false;
				
				foreach (var (defSym, defLocs, refs) in refSymbols) {
					if (refs.Any()) {
						//definition
						
						_Fold(true).Marker(c_markerSymbol);
						//usage
						string defInit = null;
						if (defSym is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol
							&& defSym.IsFromSource()
							&& defSym.Locations[0].FindNode(default) is SyntaxNode n1) {
							EqualsValueClauseSyntax evc = n1 switch { VariableDeclaratorSyntax g => g.Initializer, PropertyDeclarationSyntax g => g.Initializer, ParameterSyntax g => g.Default, _ => null };
							if (evc != null) {
								aUsage.Add((b.Length, 2, (ushort)ValueUsageInfo.Write));
								defInit = evc.ToString();
							}
						}
						
						_AppendSymbol(defSym, true, defLocs.Distinct(locComp), defInit);
						bool isCtor = defSym is IMethodSymbol ms && ms.IsConstructor();
						
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
							if (!seen.Add(new(f, span.Start))) continue; //remove logical duplicates added because of meta c
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
							
							//usage
							var sui = rloc.SymbolUsageInfo;
							if (sui.TypeOrNamespaceUsageInfoOpt != null) {
								aUsage.Add((b.Length, 1, (ushort)sui.TypeOrNamespaceUsageInfoOpt.Value));
							} else if (sui.ValueUsageInfoOpt != null) {
								if (isCtor && sui.ValueUsageInfoOpt == ValueUsageInfo.Read) //for 'new()' of a ctor Roslyn gives ValueUsageInfo.Read
									aUsage.Add((b.Length, 1, (ushort)TypeOrNamespaceUsageInfo.ObjectCreation));
								else if (!(defSym is IMethodSymbol && sui.ValueUsageInfoOpt is ValueUsageInfo.Read)) //'read' for methods has no sense
									aUsage.Add((b.Length, 2, (ushort)sui.ValueUsageInfoOpt.Value));
							}
							
							PanelFound.AppendFoundLine(b, f, text, span.Start, span.End, workingState, displayFile: !allInThisDoc, indicHilite: PanelFound.Indicators.HiliteG);
						}
						if (multiProj) _Fold(false);
						
						_Fold(false);
					}
				}
			}
			//perf.next();
			
			if (!info.NE()) b.Marker(c_markerInfo).Text(info).NL();
			
			void _AppendSymbol(ISymbol sym, bool refDef = false, IEnumerable<Location> locations = null, string defInit = null) {
				var sDef = sym is INamespaceSymbol
					? sym.ToString() /* with ancestor namespaces */
					: sym.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat); /* no namespaces, no param names, no return type */
				bool isInSource = false;
				var symName = sym.JustName();
				var e = (locations ?? sym.Locations)
					.Where(o => o.IsInSource)
					.Select(o => (f: CiProjects.FileOf(o.SourceTree, solution), ts: o.SourceSpan))
					.OrderBy(o => StringUtil.LevenshteinDistance(o.f.DisplayName, symName));
				foreach (var (f, ts) in e) {
					if (isInSource) b.Text("   ");
					b.Link2(new PanelFound.CodeLink(f, ts.Start));
					if (!isInSource) {
						if (refDef) b.B(sDef); else b.Text(sDef);
						if (defInit != null) b.Text(" ").Text(defInit.Limit(50).ReplaceLineEndings(" "));
						b.Text("      ");
					}
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
			
			//margin text
			foreach (var v in aUsage) {
				string s = null;
				int style = c_marginStyleBlack;
				if (v.kind == 1) {
					var u = (TypeOrNamespaceUsageInfo)v.usage;
					s = u switch {
						TypeOrNamespaceUsageInfo.Base => "base",
						TypeOrNamespaceUsageInfo.Import => "using",
						//TypeOrNamespaceUsageInfo.NamespaceDeclaration => "ns",
						TypeOrNamespaceUsageInfo.ObjectCreation => "new",
						//TypeOrNamespaceUsageInfo.Qualified => "A.B", //weird and distracting
						TypeOrNamespaceUsageInfo.Qualified => ".",
						TypeOrNamespaceUsageInfo.TypeArgument => "<T>",
						TypeOrNamespaceUsageInfo.TypeConstraint => "where",
						_ => null
					};
				} else {
					var u = (ValueUsageInfo)v.usage;
					s = u switch {
						ValueUsageInfo.Name => "name",
						ValueUsageInfo.Read or ValueUsageInfo.ReadableReference => "read",
						ValueUsageInfo.ReadableWritableReference => "ref",
						ValueUsageInfo.ReadWrite => "r w",
						ValueUsageInfo.WritableReference => "out",
						ValueUsageInfo.Write => "write",
						_ => null
					};
					if (u.Has(ValueUsageInfo.Write)) style = c_marginStyleWrite; else if (u.Has(ValueUsageInfo.Read)) style = c_marginStyleRead;
				}
				int line = k.aaaLineFromPos(true, v.pos);
				k.Call(SCI_MARGINSETSTYLE, line, style);
				if (s != null) k.aaaSetString(SCI_MARGINSETTEXT, line, s);
			}
			
			timer.after(1, _ => GC.Collect());
			//perf.nw();
		}
		finally {
			_working = false;
			Au.Compiler.TestInternal.RefsEnd();
		}
	}
	
	record struct _Seen(object file, int pos);
	
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
		if (modified || 0 == doc.aaaIndicatorGetValue(SciCode.c_indicRefs, doc.aaaCurrentPos8) || doc.aaaHasSelection)
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
			var cancelTS = _cancelTS = new CancellationTokenSource();
			var cancelToken = cancelTS.Token;
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
				
				foreach (var v in ar) {
					if (cd.sci.SnippetMode_ != null && 0 != (cd.sci.aaaIndicatorGetAll(v.Start.Value, true) & 1 << SciCode.c_indicSnippetFieldActive)) continue; //avoid mixed color
					cd.sci.aaaIndicatorAdd(SciCode.c_indicRefs, true, v);
				}
			}
			catch (OperationCanceledException) { return; }
			catch (Exception e1) { Debug_.Print(e1); } //Roslyn bug: when caret at '!=' in 'if (Sheet != App.ActiveSheet)' (COM). Also once "Unexpected value 'PointerElementAccess'".
			finally {
				cancelTS.Dispose();
				if (cancelTS == _cancelTS) _cancelTS = null;
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
		} else if (chL == '#' && CiUtil.IsLineStart(code, pos)) {
			_Directive();
		}
		
		chR = --pos >= 0 ? code[pos] : default;
		if (chR is ')' or ']' or '}' or '>') {
			chL = chR switch { ')' => '(', ']' => '[', '}' => '{', _ => '<' };
			_Brace(true);
		} else if (chR == '#' && CiUtil.IsLineStart(code, pos)) {
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
			IEnumerable<DirectiveTriviaSyntax> a = null;
			if (node is IfDirectiveTriviaSyntax or ElseDirectiveTriviaSyntax or ElifDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax) {
				var m = node.GetMatchingConditionalDirectives(default);
				if (m.Length > 1) a = m;
			} else {
				var node2 = node.GetMatchingDirective(default);
				if (node2 != null) a = [node, node2];
			}
			if (a == null) return;
			foreach (var v in a) cd.sci.aaaIndicatorAdd(SciCode.c_indicBraces, true, v.HashToken.Span.ToRange());
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
		bool _overloads, _comments, _disabled, _strings, _preview;
		List<_File> _a;
		FileNode _currentFile;
		KScintilla _sciPreview;
		static WeakReference<KScintilla> s_sciPreview;
		
		public async void Rename() {
			if (s_sciPreview?.TryGetTarget(out var sciPreview) ?? false) Panels.Found.Close(sciPreview);
			s_sciPreview = null;
			
			if (!CodeInfo.GetContextAndDocument(out var cd)) return;
			_currentFile = cd.sci.EFile;
			
			var sym = await RenameUtilities.TryGetRenamableSymbolAsync(cd.document, cd.pos, default);
			
			if (sym == null || !sym.IsInSource() || sym.IsImplicitlyDeclared || !sym.Locations[0].FindToken(default).IsKind(SyntaxKind.IdentifierToken)) {
				dialog.show("This element cannot be renamed", owner: App.Hmain);
				return;
			}
			
			if (!_Dialog(sym)) return;
			
			//using var p1 = perf.local();
			LightweightRenameLocations rlocs;
			Au.Compiler.TestInternal.RefsStart();
			try {
				(var solution, _info) = await CiProjects.GetSolutionForFindReferences(sym, cd);
				//p1.Next('s');
				
				SymbolRenameOptions sro = new(_overloads, _strings, _comments | _disabled);
				rlocs = await Renamer.FindRenameLocationsAsync(solution, sym, sro, default);
			}
			finally { Au.Compiler.TestInternal.RefsEnd(); }
			//p1.Next('f');
			
			Dictionary<FileNode, _File> df = new();
			HashSet<_Seen> seen = new();
			foreach (var v in rlocs.Locations) {
				var f = CiProjects.FileOf(v.DocumentId);
				var span = v.Location.SourceSpan;
				if (!seen.Add(new(f, span.Start))) continue; //remove logical duplicates added because of meta c
				
				int marker = 0;
				if (v.IsRenameInStringOrComment) {
					var trivia = v.Location.SourceTree.GetRoot(default).FindTrivia(v.Location.SourceSpan.Start);
					marker = trivia.Kind() switch {
						SyntaxKind.None => c_markerRenameString,
						SyntaxKind.DisabledTextTrivia => c_markerRenameDisabled,
						_ => c_markerRenameComment
					};
					if (marker == c_markerRenameComment && !_comments) continue;
					if (marker == c_markerRenameDisabled && !_disabled) continue;
				} else if (v.CandidateReason != CandidateReason.None) {
					marker = c_markerRenameError;
				}
				if (marker > 0) _preview = true;
				
				if (!df.TryGetValue(f, out var x)) {
					if (!rlocs.Solution.GetDocument(v.DocumentId).TryGetText(out var st)) { Debug_.Print(f); continue; }
					df.Add(f, x = new(f, st.ToString(), new()));
				}
				
				x.a.Add(new(span.Start, span.End, _newName, marker));
			}
			
			//Resolve conflicts.
			//	We use Renamer.FindRenameLocationsAsync (above) and ResolveConflictsAsync (below).
			//	Bad: ResolveConflictsAsync makes 4 times slower. Optional if don't need conflict resolution.
			//Could instead use Renamer.RenameSymbolAsync. Simpler, and same speed. Problems:
			//	- Fails to rename some, eg tuple fields. Now fails only to resolve conflicts (that is why the try/catch).
			//	- Skips #if-disabled code.
			//	- Joins some changes, eg in doc comments. Then a change can span many lines. How to display it?
			try {
				var res = await rlocs.ResolveConflictsAsync(sym, _newName, default, default);
				if (res.IsSuccessful && res.RelatedLocations.Any(o => o.Type.HasAny(RelatedLocationType.UnresolvedConflict))) {
					if (!dialog.showOkCancel("Unresolved conflict", "Rename anyway?", owner: App.Hmain)) return;
					//But does not find eg variable name conflicts in top-level statements.
					//In some places false positive unresolved conflict. Same in VS. Therefore don't show text like "This name already exists".
				}
				if (res.IsSuccessful && res.RelatedLocations.Any(o => o.Type is RelatedLocationType.ResolvedNonReferenceConflict or RelatedLocationType.ResolvedReferenceConflict)) {
					var drloc = rlocs.Locations.ToDictionary(o => o.Location.SourceSpan.Start);
					foreach (var did in res.DocumentIds) {
						var drels = res.GetRelatedLocationsForDocument(did);
						if (!drels.Any(o => o.Type is RelatedLocationType.ResolvedNonReferenceConflict or RelatedLocationType.ResolvedReferenceConflict)) continue;
						var f = CiProjects.FileOf(did);
						var doc1 = res.OldSolution.GetDocument(did);
						var doc2 = res.NewSolution.GetDocument(did);
						foreach (var tc in await doc2.GetTextChangesAsync(doc1)) {
							if (tc.Span.Length == _oldName.Length && tc.NewText == _newName) continue; //not a conflict
							if (!seen.Add(new(did, tc.Span.End))) continue; //remove logical duplicates added because of meta c
							
							if (!df.TryGetValue(f, out var x)) {
								if (!doc1.TryGetText(out var st)) { Debug_.Print(f); continue; }
								df.Add(f, x = new(f, st.ToString(), new()));
							}
							
							//Roslyn may add indent, and it is always spaces. Trim it.
							var s = tc.NewText.TrimStart();
							int start = tc.Span.Start, end = tc.Span.End;
							while (start < end && x.text[start] is '\t' or ' ') start++;
							
							//remove the overlapping change added before. Eg remove 'NewName' if this is 'PrependedNamespace.NewName'.
							if (end > start) x.a.RemoveAll(o => o.start < end && o.end > start);
							
							x.a.Add(new(start, end, s, 0));
						}
					}
				}
			}
			catch (Exception e1) { Debug_.PrintIf(sym.Kind != SymbolKind.Field, e1); }
			
			_a = df.Values.OrderBy(o => o.f.Name).ToList();
			foreach (var v in _a) v.a.Sort((x, y) => x.end - y.end);
			//p1.Next();
			
			if (_preview) _Preview();
			else _Finish();
		}
		
		bool _Dialog(ISymbol sym) {
			_oldName = sym.JustName();
			bool hasOverloads = CAW::Microsoft.CodeAnalysis.Rename.RenameUtilities.GetOverloadedSymbols(sym).Any();
			
			var b = new wpfBuilder("Rename symbol").Width(300..);
			b.WinProperties(WindowStartupLocation.CenterOwner, showInTaskbar: false);
			b.R.Add(out TextBox newName, _oldName).Font("Consolas").Focus()
				.Validation(_ => SyntaxFacts.IsValidIdentifier(newName.Text.TrimStart('@')) ? null : "Invalid name");
			newName.SelectAll();
			b.R.Add(out KCheckBox cOverloads, "Include overloads").Hidden(!hasOverloads).Checked(0 != (App.Settings.ci_rename & 1));
			b.R.Add(out KCheckBox cComments, "Include comments").Checked(0 != (App.Settings.ci_rename & 2));
			b.R.Add(out KCheckBox cDisabled, "Include #if-disabled code").Checked(0 != (App.Settings.ci_rename & 4));
			b.R.Add(out KCheckBox cStrings, "Include strings").Checked(0 != (App.Settings.ci_rename & 8));
			//b.R.Add(out KCheckBox cPreview, "Preview");
			b.R.AddOkCancel();
			b.End();
			if (!b.ShowDialog(App.Wmain)) return false;
			_overloads = cOverloads.IsChecked;
			_comments = cComments.IsChecked;
			_disabled = cDisabled.IsChecked;
			_strings = cStrings.IsChecked;
			//_preview = cPreview.IsChecked;
			App.Settings.ci_rename = (_overloads ? 1 : 0) | (_comments ? 2 : 0) | (_disabled ? 4 : 0) | (_strings ? 8 : 0);
			_newName = newName.Text;
			return _newName != _oldName;
		}
		
		void _Preview() {
			using var workingState = Panels.Found.Prepare(PanelFound.Found.SymbolRename, "Renaming", out var b);
			if (workingState.NeedToInitControl) {
				var k = workingState.Scintilla;
				k.aaaMarkerDefine(c_markerInfo, SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
				k.aaaMarkerDefine(c_markerSeparator, SC_MARK_UNDERLINE, backColor: 0xe0e0e0);
				k.aaaMarginSetWidth(1, 12);
				k.aaaMarkerDefine(c_markerRenameComment, SC_MARK_SMALLRECT, backColor: 0x80C000);
				k.aaaMarkerDefine(c_markerRenameDisabled, SC_MARK_SMALLRECT, backColor: 0);
				k.aaaMarkerDefine(c_markerRenameString, SC_MARK_SMALLRECT, backColor: 0xC09060);
				k.aaaMarkerDefine(c_markerRenameError, SC_MARK_SMALLRECT, backColor: 0xFF0000);
			}
			b.Marker(c_markerInfo).Text("You may want to exclude some of these. Right-click.\r\n");
			
			FileNode prevFile = null;
			foreach (var (f, text, a) in _a) {
				foreach (var v in a) {
					//if (v.marker > 0) b.Marker(v.marker);
					if (v.marker == 0) continue;
					b.Marker(v.marker);
					if (f != prevFile) { if (prevFile != null) b.Marker(c_markerSeparator, prevLine: true); prevFile = f; }
					PanelFound.AppendFoundLine(b, f, text, v.start, v.end, workingState, displayFile: true, indicHilite: PanelFound.Indicators.HiliteB);
					//rejected: in results display newName (hilited) as if already replaced. Or old red and new green, like in VSCode.
				}
			}
			
			b.Marker(c_markerInfo)
				.B().Link(() => _Link(false), "Rename").B_()
				.Text("  ").Link(() => _Link(true), "Cancel").NL();
			b.Marker(c_markerInfo).Text("Markers: green - comment, black - #if, brown - string, red - error or ambiguous.");
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
				int line = 1;
				for (int j = 0; j < _a.Count; j++) {
					var a = _a[j].a;
					for (int i = 0; i < a.Count; i++) {
						if (a[i].marker == 0) continue;
						if (0 != _sciPreview.aaaIndicatorGetValue(PanelFound.Indicators.Excluded, _sciPreview.aaaLineStart(false, line++)))
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
					var a = a1.Select(o => new StartEndText(o.start, o.end, o.text)).ToList();
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
		
		record struct _Change(int start, int end, string text, int marker);
		record struct _File(FileNode f, string text, List<_Change> a);
	}
}
