#if DEBUG

//extern alias CAW;

////using System.Xml.Linq;

////using System.Windows;
////using System.Windows.Controls;
////using System.Windows.Media;
////using System.Windows.Interop;
//using System.Windows.Input;

////using Au.Controls;
//using static Au.Controls.Sci;
//using Au.Compiler;
////using Au.Triggers;

//using Microsoft.CodeAnalysis;
//using CAW::Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using Microsoft.CodeAnalysis.Text;
//using Microsoft.CodeAnalysis.Shared.Extensions;
//using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
//using Microsoft.CodeAnalysis.CSharp.Extensions;
//using Microsoft.CodeAnalysis.Shared.Utilities;
//using CAW::Microsoft.CodeAnalysis.Shared.Utilities;
//using CAW::Microsoft.CodeAnalysis.FindSymbols;

//using System.Text.RegularExpressions;
////using System.Windows.Forms;
//using Au.Controls;
//using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Controls.Primitives;
//using System.Windows.Media;
//using System.Windows.Threading;
////using System.Net.Http;

static unsafe class Test {
	
	public static void FromMenubar() {
		//print.clear();
		
		CiSnippets.Reload();
		
		//DSnippets._ImportJson(keys.isNumLock ? @"C:\Users\G\.vscode\extensions\jorgeserrano.vscode-csharp-snippets-1.1.0\snippets\csharp.json" : @"C:\Users\G\.vscode\extensions\ms-dotnettools.csharp-2.23.15-win32-x64\snippets\csharp.json");
		//DSnippets._ImportVS(Directory.GetFiles(@"C:\Program Files\Microsoft Visual Studio\2022\Community\VC#\Snippets\1033\Visual C#"));
		
		//var f = App.Model.FindCodeFile("Oko.cs");
		//print.it(f.GetClassFileRole());
		
		//GenerateCode.CreateEventHandlers();
		//GenerateCode.CreateOverrides();
		
		var d = Panels.Editor.ActiveDoc; //
		//print.it(d.aaaCurrentPos16);
		
		//d.Call( Sci. SCI_EOLANNOTATIONSETSTYLEOFFSET, 512);
		//d.aaaStyleBackColor(512, 0xffffc0);
		//d.aaaStyleForeColor(512, 0xA06040);
		////d.Call(Sci.SCI_EOLANNOTATIONSETSTYLE, 0, 0);
		//print.it(d.Call(Sci.SCI_EOLANNOTATIONGETSTYLE, 0));
		//d.aaaSetString(Sci.SCI_EOLANNOTATIONSETTEXT, 0, "Test"u8);
		//d.Call(Sci.SCI_EOLANNOTATIONSETVISIBLE, Sci.EOLANNOTATION_STADIUM);
		
		//var s="""
		//for(int i=0;i<16;i++)
		//	{
		//	print.it( 1 );
		//	}
		
		//""";
		//		//s=";";
		
		//		int pos = d.aaaSelectionStart16, pos2 = d.aaaSelectionEnd16;
		//		if (!ModifyCode.FormatForInsert(ref s, ref pos, ref pos2)) return;
		//		print.it("---");
		//		print.it(s);
		
		//for (int i = 16; i < 32; i++) {
		//	d.aaaSetStringString(SCI_SETREPRESENTATION, $"{(char)i}\0_");
		//	//d.aaaSetString(SCI_SETREPRESENTATIONAPPEARANCE, $"{(char)i}", SC_REPRESENTATION_PLAIN);
		//	d.aaaSetString(SCI_SETREPRESENTATIONAPPEARANCE, $"{(char)i}", SC_REPRESENTATION_COLOUR);
		//	d.aaaSetString(SCI_SETREPRESENTATIONCOLOUR, $"{(char)i}", 0xC0C0C0);
		//}
		
		//Cpp.Cpp_Test();
		
		//print.it("");
		//int i=9;
		//print.it($"{i}");
		//print.it((object)"");
		//print.it("", "");
		//print.it([1,2]);
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
