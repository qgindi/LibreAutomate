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

#if TRACE

#pragma warning disable 169

static unsafe class Test {

	//static void /*REPLACED*/ getFileProp(string path) {
	//	using Handle_ h = new(Api.CreateFile(path, Api.FILE_READ_ATTRIBUTES, Api.FILE_SHARE_ALL, Api.OPEN_EXISTING));
	//	if (h.Is0) { /*print.it(path);*/return; }
	//	if (!GetFileTime(h, out _, out _, out var t)) print.it(path);
	//}
	//[DllImport("kernel32.dll")]
	//internal static extern bool GetFileTime(IntPtr hFile, out long lpCreationTime, out long lpLastAccessTime, out long lpLastWriteTime);

	public static void /*REPLACED*/ FromMenubar() {
		//print.clear();
		//CiFind.RenameSymbol();

		//var d = Panels.Editor.ActiveDoc;
		//{
		//	using var undo = new KScintilla.aaaUndoAction(d);
		//	d.Call(Sci.SCI_PASTE);
		//	if(keys.isScrollLock) d.Call(SCI_ADDUNDOACTION, 1);
		//}

		//print.it(d.Call(Sci.SCI_CANUNDO), Sci.Sci_CanUndoRedoContainer(d.AaSciPtr, false, 1));

		//if(keys.isScrollLock) Sci.Sci_SetUndoMark(d.AaSciPtr, 1);
		//else print.it(Sci.Sci_GetUndoMark(d.AaSciPtr, false), Sci.Sci_GetUndoMark(d.AaSciPtr, true));
		
		//SciUndo.OfWorkspace.UndoRedoMultiFileReplace(keys.isScrollLock);
		
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

	public static void /*REPLACED*/ MonitorGC() {
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
