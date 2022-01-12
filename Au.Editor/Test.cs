//using System.Linq;
//using System.Xml.Linq;

//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Media;
//using System.Windows.Interop;
//using System.Windows.Input;

//using Au.Controls;
using static Au.Controls.Sci;
using Au.Compiler;
//using System.Windows.Forms;

//using DiffMatchPatch;
//using System.Windows.Media.Imaging;
//using System.Resources;

//using System.Drawing;

using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Roslyn.Utilities;

/*



*/


#if TRACE

#pragma warning disable 169


static unsafe class Test
{
	public static void FromMenubar() {

		//perf.first();
		//var d=CiUtil.CreateRoslynDocument("using System; Console.Write(1);");
		//perf.next();
		//var tree = d.GetSyntaxTreeAsync().Result;
		//perf.nw();

		//EdDatabases.CreateRefAndDoc();
		//EdDatabases.CreateWinapi();

		//throw new AuException("test");

		var doc = Panels.Editor.ZActiveDoc;
		//doc.test_ = true;
		//print.it(doc.zCurrentPos16);

		//var v = CiUtil.GetSymbolEtcFromPos(out var k);
		//var semo = k.document.GetSemanticModelAsync().Result;
		//var comp = semo.Compilation;
		//var c = v.symbol.GetDocumentationComment(comp, expandIncludes: true, expandInheritdoc: true);
		//var s = c.FullXmlFragment;
		//print.it(s);


		//Au.Compiler.MetaReferences.DebugPrintCachedRefs();

		//doc.zSetString(SCI_EOLANNOTATIONSETTEXT, 9, "Annotation");
		//doc.Call(SCI_EOLANNOTATIONSETVISIBLE, EOLANNOTATION_STADIUM);

		//doc.zReplaceRange(true, 0, 0, "//");
		//int i = doc.zCurrentPos8 - 4;
		//doc.zSelect(false, i, i + 2);

		//print.it(CiUtil.IsScript(doc.zText));

//var s="aaa bbb"
//char c = 'a'

	}

	//static void TestScripting() {
	//	string code = @"if(!keys.isScrollLock) print.it(""test"");";

	//	if (Scripting.Compile(code, out var c, addUsings: true, addGlobalCs: true, wrapInClass: !true, dll: false, load: "")) {
	//		c.method.Invoke(null, new object[1]);
	//	} else {
	//		print.it(c.errors);
	//	}
	//}
/*
Aaa
bbb
ccc
*/

	class TestGC
	{
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
	static bool s_debug2;

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
}
#endif
