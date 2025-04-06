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
	
	public static void FromMenubar() {
		print.clear();
		
		timer2.every(500, _=> { GC.Collect(); });
		
		//Cpp.Cpp_Test();
		
		//var f1 = App.Model.CurrentFile;
		////var f2 = f1.FindRelative(true, "coko.cs");
		////var f2 = f1.FindRelative(true, "LibTT.cs", FNFind.File);
		////var f2 = f1.FindRelative(true, "mmnn", FNFind.Folder);
		////var f2 = f1.FindRelative(true, "near.txt", FNFind.File);
		////var f2 = f1.FindRelative(true, FilesModel.TreeControl.SelectedItems[0].Name);
		////var f2 = App.Model.Find(FilesModel.TreeControl.SelectedItems[0].Name, FNFind.Class);
		//var f2 = App.Model.Find(FilesModel.TreeControl.SelectedItems[0].ItemPath, FNFind.Class);
		//print.it(f2?.ItemPath);
		
		//var d = Panels.Editor.ActiveDoc;
		//print.it(d.aaaCurrentPos16);
		
		//if(TriggersAndToolbars.FindTriggersOf(Panels.Editor.ActiveDoc?.EFile) is not {  } a) return;
		//if(TriggersAndToolbars.FindTriggersOf(App.Model.Find("Script example1.cs")) is not {  } a) return;
		//foreach (var v in a) {
		//	string color = v.type == null ? "gray" : v.type.Starts("Toolbar") ? "blue" : "red";
		//	string s2 = v.arguments.ReplaceLineEndings("  ").Limit(300, middle: true);
		//	print.it($"<><c {color}>{v}<>    <open {v.file.IdStringWithWorkspace}||{v.pos}>Trigger<>: {v.type ?? "<unknown>"} {s2}");
		//}
		
		//bool alt = keys.isNumLock;
		//string s1 = null;
		//s1 = "Script example1.cs";
		////s1 = "Delete shell icon cache.cs";
		//s1 = "Backup code.cs";
		
		//var f = alt ? App.Model.CurrentFile : App.Model.Find(s1);
		////perf.first();
		////var s = TriggersAndToolbars.GetTriggersStringOf(f);
		//var task = TriggersAndToolbars.GetTriggersStringAsync(f);
		////perf.next();
		////timer.after(1, _ => { perf.nw('T'); });
		////App.Dispatcher.InvokeAsync(() => { perf.nw('T'); });
		//var s = await task;
		////perf.nw();
		//print.it("<>" + s);
		
		//TriggersAndToolbars.QuickWindowTrigger(wnd.fromMouse(WXYFlags.NeedWindow), 3);
		//TriggersAndToolbars.Test();
		
		//if (!CodeInfo.GetContextAndDocument(out var cd)) return;
		
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
