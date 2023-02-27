using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.FindSymbols;
//using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
//using Microsoft.CodeAnalysis.Tags;
//using Roslyn.Utilities;
using Au.Controls;

//TODO: now if in Au.sln searching from eg Au project, does not search in other projects.
//TODO: folding.
//TODO: hilite all in current doc, like Find does.
//TODO: doc about 'Find references' etc.

static class CiSymbols {
	const int c_markerSymbol = 0, c_indicProject = 15;
	
	public static /*async*/ void FindReferences() {
		//if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		var (sym, cd) = CiUtil.GetSymbolFromPos();
		//print.it(sym);
		if (sym == null) {
			Panels.Found.ClearResults(PanelFound.Found.SymbolReferences);
			return;
		}
		
		//print.it(cd.document.Project.Solution.Projects.Select(o=>o.Name));
		
		using var workingState = Panels.Found.Prepare(PanelFound.Found.SymbolReferences, sym.Name, out var b);
		if (workingState.NeedToInitControl) {
			var k = workingState.Scintilla;
			k.aaaMarkerDefine(c_markerSymbol, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
			k.aaaIndicatorDefine(c_indicProject, Sci.INDIC_GRADIENT, 0xCDE87C, alpha: 255, underText: true);
		}
		
		//List<Range> ar = new();
		var rr = SymbolFinder.FindReferencesAsync(sym, cd.document.Project.Solution).Result;
		//var rr = await SymbolFinder.FindReferencesAsync(sym, cd.document.Project.Solution);
		foreach (var v in rr) {
			//definition
			
			var def = v.Definition;
			var sDef = def.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat); /*no namespaces, no param names, no return type*/
			//if (!v.ShouldShow(FindReferencesSearchOptions.Default)) continue;
			if (!v.ShouldShow(FindReferencesSearchOptions.GetFeatureOptionsForStartingSymbol(sym))) continue; //for 'Find references' only, not for 'Rename'
			b.Fold(new(0, true));
			b.Marker(c_markerSymbol);
			if (def.IsInSource()) {
				bool once = false;
				foreach (var loc in def.Locations) {
					if (!loc.IsInSource) continue;
					FileNode f = CodeInfo.FileOf(loc.SourceTree);
					if (once) b.Text("   ");
					b.Link2(new PanelFound.CodeLink(f, loc.SourceSpan.Start, loc.SourceSpan.End));
					if (!once) b.B(sDef).Text("      ");
					b.Gray(f.Name);
					b.Link_();
					once = true;
					
					//if (loc.SourceTree == cd.syntaxRoot.SyntaxTree) {
					//	ar.Add(loc.SourceSpan.ToRange());
					//}
				}
			} else {
				b.B(sDef);
			}
			b.NL();
			
			//references
			
			bool multiProj = cd.document.Project.Solution.ProjectIds.Count > 1;
			if (multiProj) b.Fold(new(b.Length, true));
			Project prevProj = null;
			foreach (var rloc in v.Locations.OrderBy(o => o.Document.Project.Name, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Document.Name, StringComparer.OrdinalIgnoreCase)) {
				var f = CodeInfo.FileOf(rloc.Document);
				var span = rloc.Location.SourceSpan;
				if (!f.GetCurrentText(out var text)) { Debug_.Print(f); continue; }
				
				if (multiProj && (object)rloc.Document.Project != prevProj) {
					prevProj = rloc.Document.Project;
					b.Fold(new(b.Length - 2, false));
					b.Fold(new(b.Length, true));
					b.Indic(c_indicProject).Text("Project ").B(rloc.Document.Project.Name).Indic_().NL();
				}
				
				Panels.Found.AppendFoundLine(b, f, text, span.Start, span.End, displayFile: true);
				
				//print.it($"\t{rloc.Location}, {f}");
				//if (rloc.Document == cd.document) {
				//	ar.Add(span.ToRange());
				//}
			}
			
			if (multiProj) b.Fold(new(b.Length - 2, false));
			b.Fold(new(b.Length - 2, false));
		}
		//BAD: no results for meta testInternal internals.
		
		//CiUtil.HiliteRanges(ar);
		
		Panels.Found.SetSymbolReferencesResults(workingState, b);
	}
}
