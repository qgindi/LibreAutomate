using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.CSharp.QuickInfo;
using System.Windows.Documents;
using Microsoft.CodeAnalysis;

class CiQuickInfo
{
	public async Task<Section> GetTextAt(int pos16) {
		//using var p1 = perf.local();
		if (!CodeInfo.GetContextAndDocument(out var cd, pos16)) return null;

		var service = QuickInfoService.GetService(cd.document);

		//var o1=cd.document.Project.Solution.Options.Workspace.GetOption(QuickInfoOptions.IncludeNavigationHintsInQuickInfo); //default true. Not tested, but probably it adds "go to" info to the tagged text items; we currently don't use it.
		//var o2=cd.document.Project.Solution.Workspace.Options.GetOption(QuickInfoOptions.ShowRemarksInQuickInfo, "C#"); //default true, but CodeInfo._CreateWorkspace sets false
		//We don't include <remarks>. Sometimes it takes too much space. Badly formatted if eg contains markdown.

		var r = await Task.Run(async () => await service.GetQuickInfoAsync(cd.document, pos16));
		//p1.Next();
		if (r == null) return null;

		//print.it(r.Span, r.RelatedSpans);
		//print.it(r.Tags);

		var x = new CiText();

		//bool hasDocComm = false;
		//QuickInfoSection descr = null;
		var a = r.Sections;
		if (a.Length == 0) return null; //when cursor is on }. //SHOULDDO: display block start code, like in VS.
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

		return x.Result;
	}
}
