#if DEBUG
extern alias CAW;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using CAW::Microsoft.CodeAnalysis.Shared.Utilities;
using CAW::Microsoft.CodeAnalysis.FindSymbols;

using Au.Triggers;
using Au.Controls;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

static unsafe class Test {

	//public static void RxTest([StringSyntax("Regex")] this string t) { }

	public static void FromMenubar() {
		print.clear();

		//new Regex(@"\A\[^a-b]\a text\d\b (?<name>moo)");

		//RxTest(@"\d");
		//@"\d".RxTest();

		//var d = Panels.Editor.ActiveDoc;
		////int pos = d.aaaCurrentPos16;
		////d.aaaInsertText(true, 0, "//");
		//using (new SciCode.aaaUndoAction(d)) {
		//	d.aaaInsertText(true, 0, "//");
		//	d.aaaGoToPos(true, 2);
		//	//d.aaaReplaceRange(true, 0, 1, "//");
		//}
		if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		//print.it(CiUtil.IsPosInNonblankTrivia(cd.syntaxRoot, cd.pos, cd.code));
		//var trivia = cd.syntaxRoot.FindTrivia(cd.pos, keys.isNumLock);
		//print.it(cd.pos);
		//CiUtil.PrintNode(trivia);
		//print.it(trivia.Span, trivia.FullSpan);

		//var token = cd.syntaxRoot.FindToken(cd.pos);
		//var node = token.Parent.GetStatementEtc(cd.pos);
		//CiUtil.PrintNode(node, printErrors: true);
	}

	public static void MonitorGC() {
		//if(!s_debug2) {
		//	s_debug2 = true;
		//	new TestGC();

		//	//timer.every(50, _ => {
		//	//	if(!s_debug) {
		//	//		s_debug = true;
		//	//		timer.after(100, _ => new TestGC());
		//	//	}
		//	//});
		//}
	}
	//static bool s_debug2;

	class TestGC {
		~TestGC() {
			if (Environment.HasShutdownStarted) return;
			if (AppDomain.CurrentDomain.IsFinalizingForUnload()) return;
			print.it("GC", GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
			//timer.after(1, _ => new TestGC());
			//var f = App.Wmain; if(!f.IsHandleCreated) return;
			//f.BeginInvoke(new Action(() => new TestGC()));
			new TestGC();
		}
	}
}
#endif
