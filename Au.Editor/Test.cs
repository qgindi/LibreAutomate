extern alias CAW;

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

using System.Text.RegularExpressions;
//using System.Windows.Forms;
using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

#if TRACE

#pragma warning disable 169

static unsafe class Test {
	
	public static void FromMenubar() {
		
		//print.clear();
		
		var d = Panels.Editor.ActiveDoc;
		
		//for (int i = 16; i < 32; i++) {
		//	d.aaaSetStringString(SCI_SETREPRESENTATION, $"{(char)i}\0_");
		//	//d.aaaSetString(SCI_SETREPRESENTATIONAPPEARANCE, $"{(char)i}", SC_REPRESENTATION_PLAIN);
		//	d.aaaSetString(SCI_SETREPRESENTATIONAPPEARANCE, $"{(char)i}", SC_REPRESENTATION_COLOUR);
		//	d.aaaSetString(SCI_SETREPRESENTATIONCOLOUR, $"{(char)i}", 0xC0C0C0);
		//}

		//Cpp.Cpp_Test();
	}

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
