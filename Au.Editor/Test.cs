//using System.Xml.Linq;

//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Media;
//using System.Windows.Interop;
using System.Windows.Input;

//using Au.Controls;
using static Au.Controls.Sci;
using Au.Compiler;
using Au.Triggers;
//using System.Windows.Forms;

//using DiffMatchPatch;
//using System.Windows.Media.Imaging;
//using System.Resources;

//using System.Drawing;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.FindSymbols;

using System.Text.RegularExpressions;
using System.Windows.Forms;
using Au.Controls;
//using Microsoft.VisualBasic.Devices;
//using Microsoft.CodeAnalysis.Options;
//using Microsoft.CodeAnalysis.Formatting;

/*



*/


#if TRACE

#pragma warning disable 169

namespace System {
	
}

static unsafe class Test {

	/// <summary>
	/// one two three four five six seven eight none ten 
	/// </summary>
	public static void FromMenubar() {
		print.clear();

		


		//var d = Panels.Editor.ActiveDoc;


		//if (!CodeInfo.GetDocumentAndFindNode(out var cd, out var node)) return;
		//CiUtil.PrintNode(node);
		//var span = node.GetRealFullSpan();
		//cd.sci.aaaSelect(true, span.Start, span.End);



		//var v = CiUtil.GetSymbolEtcFromPos(out var k);
		//var semo = k.semanticModel;
		//var comp = semo.Compilation;
		//var c = v.symbol.GetDocumentationComment(comp, expandIncludes: true, expandInheritdoc: true);
		//var s = c.FullXmlFragment;
		//print.it(s);


		//Cpp.Cpp_Test();
	}

	//static void TestFormatting() {
	//	//works, but:
	//	//	Moves { to new line. Don't know how to change.
	//	//	Removes tabs from empty lines.

	//	if (!CodeInfo.GetContextAndDocument(out var k)) return;
	//	var cu = k.syntaxRoot;

	//	var workspace = k.document.Project.Solution.Workspace;
	//	var o = workspace.Options;
	//	o = o.WithChangedOption(FormattingOptions.UseTabs, "C#", true);
	//	//o = o.WithChangedOption(FormattingOptions.SmartIndent, "C#", FormattingOptions.IndentStyle.Block);
	//	//Microsoft.CodeAnalysis.Formatting.

	//	var f = Microsoft.CodeAnalysis.Formatting.Formatter.Format(cu, workspace, o);
	//	print.it(f);
	//}

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
