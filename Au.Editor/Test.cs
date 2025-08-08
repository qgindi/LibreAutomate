#if DEBUG || IDE_LA
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
using System.Windows.Controls;
//using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;

using Au.Compiler;
using static Au.Controls.Sci;

static class Test {
	//TODO
	/// <summary>
	/// Aaa moo <paramref name="moo"/> <c>moo</c> <i>moo</i> <b><i>moo</i></b> <b>Koo</b> <c>Koo</c> <b><c>Koo</c></b> kk <u>Koy</u> <see cref="Koo"/>.
	/// </summary>
	/// <param name="moo"></param>
	/// <returns></returns>
	/// <remarks>
	/// <![CDATA[**bold** m `m`]]>
	/// <see langword="true"/>
	/// </remarks>
	static int Koo(RECT moo) => 0;

	public static void FromMenubar() {
		print.clear();



		//var doc = Panels.Editor.ActiveDoc;
		//doc.ESetUndoMark_(-1);

		//var s = Panels.Editor.ActiveDoc.aaaText.Trim();
		//print.it(App.Model.FindByItemPath(s)?.ItemPath);

		//foreach (var v in App.Model.Root.Descendants(true)) {
		//	print.it(v.ItemPath, v.FilePath);
		//}

		//PipIPC.RunScriptInPip(App.Model.FindCodeFile("PipTestScript"));

		//timer2.every(500, _=> { GC.Collect(); });

		//Cpp.Cpp_Test();

#if !IDE_LA
#endif
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
