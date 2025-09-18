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

using static Au.Controls.Sci;

namespace LA;

static class Test {
	/// <summary>
	/// 
	/// </summary>
	public static void FromMenubar() {
		//print.clear();
		
		
		
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
