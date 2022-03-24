using Microsoft.CodeAnalysis.QuickInfo;
//using Microsoft.CodeAnalysis.CSharp.QuickInfo;
using System.Windows.Documents;
using Microsoft.CodeAnalysis;

class CiQuickInfo {
	public async Task<Section> GetTextAt(int pos16) {
		//using var p1 = perf.local();
		if (!CodeInfo.GetContextAndDocument(out var cd, pos16)) return null;

		//don't include <remarks>. Sometimes it takes too much space. Badly formatted if eg contains markdown.
		var opt1 = QuickInfoOptions.Default with { ShowRemarksInQuickInfo = false, IncludeNavigationHintsInQuickInfo = false };
		var opt2 = new Microsoft.CodeAnalysis.LanguageServices.SymbolDescriptionOptions(opt1, Microsoft.CodeAnalysis.Classification.ClassificationOptions.Default);

		var service = QuickInfoService.GetService(cd.document);
		var r = await Task.Run(async () => await service.GetQuickInfoAsync(cd.document, pos16, opt2, default));
		//p1.Next();
		if (r == null) return null;
		//this oveload is internal, but:
		//	- The public overload does not have an options parameter. Used to set options for workspace, but it stopped working.
		//	- Roslyn in Debug config asserts "don't use this function".

		//print.it(r.Span, r.RelatedSpans);
		//print.it(r.Tags);

		var a = r.Sections;
		if (a.Length == 0) return null; //when cursor is on }. //SHOULDDO: display block start code, like in VS.

		//don't show some useless quickinfos, eg for literals
		if (r.Tags.Length == 2 && a.Length == 2 && a[1].Kind == QuickInfoSectionKinds.DocumentationComments) {
			//print.it(r.Tags[0], a[1].Kind, a[1].Text);
			var s = a[1].Text;
			if (s.Starts("Represents ")) {
				switch (r.Tags[0]) {
				case "Class":
					if (s == "Represents text as a sequence of UTF-16 code units.") return null;
					break;
				case "Structure":
					if(s.RxIsMatch(@"^Represents a (\d+-bit u?n?signed integer|[\w+-]+ floating-point number)\.$")) return null;
					break;
				}
			}
		}

		var x = new CiText();

		//bool hasDocComm = false;
		//QuickInfoSection descr = null;
		for (int i = 0; i < a.Length; i++) {
			var se = a[i];
			//print.it(se.Kind, se.Text);

			//if (se.Kind == QuickInfoSectionKinds.RemarksDocumentationComments) continue;

			x.StartParagraph();

			//if (se.Kind == QuickInfoSectionKinds.RemarksDocumentationComments) {
			//	x.Append("More info in Remarks (click and press F1)."); //no, because the DB does not contain Au and .NET remarks; would show this info only for others (local, XML files).
			//} else {
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
			//}

			x.EndParagraph();
		}

		return x.Result;
	}
}
