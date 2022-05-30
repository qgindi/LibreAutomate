//using System.Linq;
//using System.Xml.Linq;

using System.Windows;
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
//using Microsoft.CodeAnalysis.Options;
//using Microsoft.CodeAnalysis.Formatting;


/*



*/


#if TRACE

#pragma warning disable 169

static unsafe class Test {


	//	static void TestMarkdig() {
	//		string markdown = @"List:
	//- one.
	//- two.

	//	";

	//		string html = Markdig.Markdown.ToHtml(markdown);
	//		print.it(html);
	//	}


	public static void FromMenubar() {
		print.clear();

		//InsertCode.CreateDelegate();
		//var f = App.Model.FindCodeFile("InsertCode");
		//CompileRun.CompileAndRun(true, f, runFromEditor: true);

		//InsertCode.Statements("var s = \"\"\"\r\ntext\r\ntext\r\n\"\"\";");

		//var f = App.Model.FindCodeFile(@"\@DNewToolbar\test DNewToolbar.cs");
		//CompileRun.CompileAndRun(true, f, runFromEditor: true);



		//App.Model.NewItem("Class.cs", text: new(true, "moo"));

		//		var s = @"var w = wnd.find(1, ""name"", ""class"");
		//var w2 = w.Child(""name"");
		//int args = 20;";
		//		//s = @"var s = ""a""; var s = ""b"";";
		//		InsertCode.Statements(s);

		//InsertCode.SetMenuToolbarItemIcon("*Unicons.Thunderstorm #0D84F2");

		//TestMarkdig();

		//if (!CodeInfo.GetDocumentAndFindNode(out var cd, out var node)) return;
		//CiUtil.PrintNode(node);
		//var span = node.GetRealFullSpan();
		//cd.sciDoc.zSelect(true, span.Start, span.End);

		//var node = token.Parent;
		//print.it("---------");
		//foreach (var v in node.AncestorsAndSelf()) CiUtil.PrintNode(v);
		//print.it("---------");
		//for(var v=node;v!=null; v=v.Parent) CiUtil.PrintNode(v);

		//var pos16 = cd.pos16;
		//bool? inString = token.IsInString(pos16, cd.code, out var x);
		//if (inString == true) print.it($"{pos16}, {x.textSpan}, {(x.isInterpolated ? "i" : "")}, {(x.isVerbatim ? "V" : x.isRaw ? "R" : "")}");
		//else print.it(inString);

		//perf.first();
		//var d=CiUtil.CreateRoslynDocument("using System; Console.Write(1);");
		//perf.next();
		//var tree = d.GetSyntaxTreeAsync().Result;
		//perf.nw();

		//EdDatabases.CreateRefAndDoc();
		//EdDatabases.CreateWinapi();

		//throw new AuException("test");

		//var doc = Panels.Editor.ZActiveDoc;
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

		//Cpp.Cpp_Test();
	}

	//static void TestFormatting() {
	//	//works, but:
	//	//	Moves { to new line. Don't know how to change.
	//	//	Removes tabs from empty lines.

	//	if (!CodeInfo.GetContextAndDocument(out var k)) return;
	//	var cu = k.document.GetSyntaxTreeAsync().Result.GetCompilationUnitRoot();

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
