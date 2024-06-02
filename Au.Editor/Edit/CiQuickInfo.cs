using Microsoft.CodeAnalysis.QuickInfo;
//using Microsoft.CodeAnalysis.CSharp.QuickInfo;
using System.Windows.Documents;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class CiQuickInfo {
	public async Task<Section> GetTextAt(int pos16) {
		//using var p1 = perf.local();
		if (!CodeInfo.GetContextAndDocument(out var cd, pos16)) return null;
		//p1.Next();
		
		var tok = cd.syntaxRoot.FindToken(pos16);
		var tspan = tok.Span;
		bool isInToken = !tspan.IsEmpty && tspan.ContainsOrTouches(pos16);
		if (isInToken) {
			//print.it(tok.Kind());
			
			//CONSIDER: if over text like `0x123456` or `#123456` or `blue`, and it is a token or in a string, display quick info popup with the color (text and background). And add link "Edit color".
			//	But problem: `0x123456` can be RGB or BGR. Maybe then display both.
			
			//ignore literals
			if (cd.code[tspan.Start] is (>= '0' and <= '9') or '\'' or '"' or '@' or '$' or '{') return null;
			var tk = tok.Kind(); if (tk is SyntaxKind.InterpolatedStringTextToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword) return null;
			
			Panels.Debug.SciMouseHover_(cd);
			
			//} else { //trivia. May show quickinfo for some trivia kinds, eg a cref in doccomment, #endx block start.
			//var tri = cd.syntaxRoot.FindTrivia(pos16);
			//print.it(tri, tri.IsDirective, tri.Kind());
		}
		
		var opt1 = QuickInfoOptions.Default with { IncludeNavigationHintsInQuickInfo = false };
		var opt2 = new Microsoft.CodeAnalysis.LanguageService.SymbolDescriptionOptions { QuickInfoOptions = opt1 };
		
		var service = QuickInfoService.GetService(cd.document);
		var r = await Task.Run(async () => await service.GetQuickInfoAsync(cd.document, pos16, opt2, default));
		//p1.Next();
		if (r == null) return null;
		//this oveload is internal, but:
		//	- The public overload does not have an options parameter. In old Roslyn could set options for workspace, but it stopped working.
		//	- Roslyn in Debug config asserts "don't use this function".
		
		//ignore literals again. The above not always works, eg the service gets quickinfo when the mouse is over `}` in `"string"}` or `number}`.
		if (isInToken && r.Span.End <= tspan.Start) return null;
		
		var x = new CiText();
		
		if (!r.Sections.IsDefaultOrEmpty) {
			//bool hasDocComm = false;
			//QuickInfoSection descr = null;
			var a = r.Sections;
			for (int i = 0; i < a.Length; i++) {
				var se = a[i];
				//print.it(se.Kind, se.Text);
				
				x.StartParagraph();
				
				if (i == 0) { //image
					CiUtil.TagsToKindAndAccess(r.Tags, out var kind, out var access);
					if (kind != CiItemKind.None) {
						if (access != default) x.Image(access);
						x.Image(kind);
						x.Append(" ");
					}
				}
				
				var tp = se.TaggedParts;
				if (tp[0].Tag == TextTags.LineBreak) { //remove/replace some line breaks in returns and exceptions
					int lessNewlines = se.Kind switch { QuickInfoSectionKinds.ReturnsDocumentationComments => 1, QuickInfoSectionKinds.Exception => 2, _ => 0 };
					var k = new List<TaggedText>(tp.Length - 1);
					for (int j = 1; j < tp.Length; j++) {
						var v = tp[j];
						if (lessNewlines > 0 && j > 1) {
							if (v.Tag == TextTags.LineBreak) {
								if (j == 2) continue; //remove line break after "Returns:" etc
								if (lessNewlines == 2) { //in list of exceptions replace "\n  " with ", "
									if (++j == tp.Length || tp[j].Tag != TextTags.Space) { j--; continue; }
									v = new(TextTags.Text, ", ");
								}
							}
						}
						k.Add(v);
					}
					x.AppendTaggedParts(k, false);
				} else {
					x.AppendTaggedParts(tp);
				}
				
				x.EndParagraph();
			}
		}
		
		if (!r.RelatedSpans.IsDefaultOrEmpty) { //when mouse is on `}` or `#endx`, r.RelatedSpans contains spans of the `class etc` or `#if etc`
			foreach (var v in r.RelatedSpans) {
				x.StartParagraph();
				x.Append(cd.code[v.ToRange()]);
				x.EndParagraph();
			}
		}
		
		return x.Result;
	}
}
