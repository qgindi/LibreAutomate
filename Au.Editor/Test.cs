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

interface Inter {
	void Koo(int i);
}
interface Inter2 : Inter {
	void Koo2(int i);
}
interface Inter3 : Inter2 {
	void Koo3(int i);
	new void Koo2(int i);
}

class C1 : Inter {
	public void Koo(int i) {
		
	}
}

class C2 : Inter {
	void Inter.Koo(int i) {
		
	}
}

class C22 : Inter2 {
	void Inter.Koo(int i) { }
	void Inter2.Koo2(int i) { }
}

class C3 : C2 {
	public C3() { }
}

class C4 : C2 {
	public virtual void Vik() { }
}

class C5 : C4 {
	public override void Vik() { }
}

abstract class AbsA {
	public abstract void Koo(int i);
}

class CA1 : AbsA {
	public override void Koo(int i) {
		
	}
}

class CA2 : AbsA {
	public override void Koo(int i) {
		
	}
}


#region
#region
#endregion
#endregion

#if true
#else
#endif

#if true
#elif true
#else
#endif

class Moo { public Moo() { } }

class C {
	public static int Prop {
		get => 0;
		set { }
	}
	public C() { }
	static C() { }
}

///// <summary>
///// <see cref="Kia"/>
///// </summary>
//class Moka {
//	public void Kia(int x) {  }
//	public void Kia(int x, int y) {  }
//	void User() {
//		Kia(1);
//		Kia(1, 2);
//		Kia();
//	}
//}

static unsafe class Test {
	
	static void getFileProp(string path) {
		using Handle_ h = new(Api.CreateFile(path, Api.FILE_READ_ATTRIBUTES, Api.FILE_SHARE_ALL, Api.OPEN_EXISTING));
		if (h.Is0) { /*print.it(path);*/return; }
		if (!GetFileTime(h, out _, out _, out var t)) print.it(path);
	}
	[DllImport("kernel32.dll")]
	internal static extern bool GetFileTime(IntPtr hFile, out long lpCreationTime, out long lpLastAccessTime, out long lpLastWriteTime);
	
	/// <summary>
	/// one two three four five six seven eight none ten 
	/// </summary>
	public static void FromMenubar() {
		//print.clear();
		//C c = new C();
		//CiGoTo.GoToBase();
		CiFind.RenameSymbol();
		
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
