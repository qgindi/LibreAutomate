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

static unsafe class Test {

	public static void FromMenubar() {
		print.clear( );

		//var d = Panels.Editor.ActiveDoc;
		//int pos = d.aaaCurrentPos16;
		if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		var token = cd.syntaxRoot.FindToken(cd.pos);
		var node = token.Parent.GetStatementEtc(cd.pos);
		CiUtil.PrintNode(node, printErrors: true);
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
