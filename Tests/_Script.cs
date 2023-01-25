/*
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="cd &quot;$(TargetDir)&quot;&#xD;&#xA;&quot;$(SolutionDir)Other\Programs\ResourceHacker.exe&quot; -script &quot;$(ProjectDir)Resources\ResourceHacker.txt&quot;&#xD;&#xA;del &quot;$(TargetDir)$(TargetName).*.json&quot;&#xD;&#xA;" />
  </Target>
*/

//#define TOPLEVEL

using System;
using System.Collections.Generic;
#if true
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime;
using Microsoft.Win32;
using System.Runtime.InteropServices.ComTypes;
using System.Numerics;
using System.Globalization;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Windows;
using System.Collections;

using Au.Triggers;
using Au.Controls;
#endif

using Au;
using Au.Types;
using System.Resources;
using Au.More;
using System.Windows.Forms;

//using Microsoft.CodeAnalysis.CSharp.Scripting;

//using System.Management.Automation;

[module: System.Runtime.InteropServices.DefaultCharSet(System.Runtime.InteropServices.CharSet.Unicode)]

#if TOPLEVEL

//// TEST C# 9 ////

// TOP-LEVEL PROGRAMS

//int i = 7;
//print.it("no class", LocFunc(), args.Length, Thread.CurrentThread.GetApartmentState());

//var v = new Abc();
//v.Test();

////[STAThread]
//int LocFunc() {
//	return i;
//}

//class Abc
//{
//	public void Test() {
//		print.it("test", typeof(Abc).FullName);
//	}
//}


// RECORDS

//var p = new Product { Name = "asd", CategoryId = 4 };
//p = p with { CategoryId = 10 };
//print.it(p.Name, p.CategoryId);

//public record Product
//{
//	public string Name { get; init; }
//	public int CategoryId { get; init; }
//}

//public record Product2
//{
//	string Name;
//	int CategoryId;
//}


// PATTERN-MATCHING

//object o = "asdf";
//switch (o) {
//	case int: print.it("int"); break;
//	case string: print.it("string"); break;
//}

//int i = 15;
//switch (i) {
//	case < 10: print.it("<10"); break;
//	case >= 10 and <=20: print.it("10..20"); break;
//}

//if (o is not int) print.it("not");
////if (o is not int and i is not double) print.it("not"); //no
////if(i>=10 and i<=20) //no


// TARGET-TYPING

//System.Drawing.Point p = new(3, 5);
//System.Drawing.Point pp = new();

//Control c1 = null; Form f1 = null;
//Control c = c1 ?? f1; // Shared base type
////int? result = false ? 0 : null; // nullable value type //error


// SKIP LOCALS-INIT

//Skiiip();

//[System.Runtime.CompilerServices.SkipLocalsInitAttribute]
//unsafe void Skiiip() {
//	var v = stackalloc long[1000];

//	print.it(v[0]);
//}


// NATIVE INT

//nint i = 5;
////print.it(sizeof(nint));


//////////////////////////////



#else

partial class TestScript {

	class TestGC {
		~TestGC() {
			if (Environment.HasShutdownStarted) return;
			if (AppDomain.CurrentDomain.IsFinalizingForUnload()) return;
			print.it("GC", GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
			//timer.after(1, _ => new TestGC());
			//var f = Program.MainForm; if(!f.IsHandleCreated) return;
			//f.BeginInvoke(new Action(() => new TestGC()));
			new TestGC();
		}
	}
	static bool s_debug2;

	void _MonitorGC() {
		//return;
		if (!s_debug2) {
			s_debug2 = true;
			new TestGC();

			//timer.every(50, _ => {
			//	if(!s_debug) {
			//		s_debug = true;
			//		timer.after(100, _ => new TestGC());
			//	}
			//});
		}
	}


	//class _Settings
	//{
	//	public string OneTwo { get; set; }
	//	public int ThreeFour { get; set; }
	//	public int Five { get; set; }
	//	public bool Six { get; set; }
	//	public string Seven { get; set; }
	//	public string Eight { get; set; } = "def";
	//}

	//void TestJson()
	//{
	//	var file = @"C:\test\sett.json";
	//	var file2 = @"C:\test\sett.xml";

	//	var v = new _Settings { OneTwo = "text ąčę", ThreeFour = 100 };

	//	for(int i = 0; i < 5; i++) {
	//		//100.ms();
	//		//perf.first();
	//		//var k1 = new JsonSerializerOptions { IgnoreNullValues = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };
	//		//var b1 = JsonSerializer.SerializeToUtf8Bytes(v, k1);
	//		//perf.next();
	//		//File.WriteAllBytes(file, b1);
	//		//perf.nw();

	//		100.ms();
	//		perf.first();
	//		var b2 = File.ReadAllBytes(file);
	//		perf.next();
	//		var k2 = new JsonSerializerOptions { IgnoreNullValues = true };
	//		perf.next();
	//		v = JsonSerializer.Deserialize<_Settings>(b2, k2);
	//		perf.nw('J');
	//	}

	//	for(int i = 0; i < 5; i++) {
	//		//100.ms();
	//		//perf.first();
	//		//var r1 = new XElement("r");
	//		//r1.Add(new XElement("OneTwo", v.OneTwo));
	//		//r1.Add(new XElement("ThreeFour", v.ThreeFour.ToString()));
	//		//perf.next();
	//		//r1.Save(file2);
	//		//perf.nw();

	//		100.ms();
	//		perf.first();
	//		var r2 = XElement.Load(file2);
	//		perf.next();
	//		v = new _Settings();
	//		v.OneTwo = r2.Element("OneTwo").Value;
	//		var s2 = r2.Element("ThreeFour").Value;
	//		perf.nw('X');
	//		v.ThreeFour = s2.ToInt();
	//	}

	//	print.it(v.OneTwo, v.ThreeFour, v.Five, v.Six, v.Seven, v.Eight);

	//	//JsonDocument d; d.RootElement.
	//}

	//[DllImport("CppE")]
	//static extern int Cpp_Install(int step, string dir);

	//[DllImport("CppE")]
	//static extern int Cpp_Uninstall();


#if false
	void TestToolbar()
	{
		for(int i = 0; i < 1; i++) {
			var t = new toolbar("123");
			//t.NoText = true;
			//t.Border= TBBorder.Sizable3;t.Control.Text = "Toolbar";
			//t.Border = TBBorder.SizableWithCaptionX;

			//t["Find", @"C:\code\qm\find.ico"] = o => print.it(o);
			//t["Copy", @"C:\code\qm\copy.ico"] = o => print.it(o);
			//t.Separator("Tpi group");
			//t["Delete", @"C:\code\qm\delete.ico"] = o => print.it(o);
			//t["No image"] = o => print.it(o);
			//t["TT", tooltip: "WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW WWWWWWWWWWWW "] = o => print.it(o);
			////t.LastButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
			////t.LastButton.AutoToolTip = false;
			////t.LastButton.ToolTipText = "ggg";
			//t.Separator();
			//t["Run", @"C:\code\qm\run.ico"] = o => print.it(o);
			//t.Separator("");
			//t["Paste text", @"C:\code\qm\paste.ico"] = o => print.it(o);
			//t.LastButton.ToolTipText = "Toooooltip";

			//t.ExtractIconPathFromCode = true;
			//t["Auto icon"] = o => print.it("notepad.exe");
			//t["Failed icon", @"C:\code\qm\-.ico"] = o => print.it(o);
			////t.LastButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
			////t.Separator("");
			////t.Add(new ToolStripTextBox { ToolTipText= "ToolStripTextBox", AutoSize=false, Width=50 });
			////t.Add(new ToolStripComboBox { ToolTipText= "ToolStripComboBox", AutoSize=false, Width=50 });
			////t.Add(new ToolStripTextBox());
			////t.Add(new ToolStripTextBox());
			////t.Add(new ToolStripTextBox());
			////t.Add(new ToolStripButton("aaa"));
			////t.Add(new ToolStripButton("bbb"));
			////t["Multi\r\nline"] = o => print.it(o);

			//t["None"] = o => _B(TBBorder.None);
			//t["SWC"] = o => _B(TBBorder.SizableWithCaption);
			//t["Sizable1"] = o => _B(TBBorder.Sizable1);
			//t["Sizable2"] = o => _B(TBBorder.Sizable2);
			//t["Sizable3"] = o => _B(TBBorder.Sizable3);
			//t["Sizable3D"] = o => _B(TBBorder.Sizable3D);
			//t["Sizable"] = o => _B(TBBorder.Sizable);
			//t["FixedWithCaption"] = o => _B(TBBorder.FixedWithCaption);
			//t["SizableWithCaption"] = o => _B(TBBorder.SizableWithCaption);
			//t["Close"] = o => t.Close();

#if false
			var dd = new ToolStripDropDownButton("DD");
			t.Add(dd, @"C:\code\qm\find.ico");
			dd.DropDownOpening += (_, _) => {
				var m = new popupMenu(dd);
				m["one"] = o => print.it(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => print.it(o);
				}
			};
			var sb = new ToolStripSplitButton("SB");
			t.Add(sb, @"C:\code\qm\copy.ico", o => print.it(o));
#elif true
			//t.Control.Font = new Font("Courier New", 16);
			//t.Control.RightToLeft = RightToLeft.Yes;
			t.MenuButton("DD", m => {
				print.it("dd");
				//m.MultiShow = false;
				m["one"] = o => print.it(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => print.it(o);
				}
			}, @"C:\code\qm\find.ico", "MenuButton");
			t.SplitButton("SB", m => {
				m["one"] = o => print.it(o);
				//var sb = m.Control.OwnerItem as ToolStripSplitButton;
				//print.it(sb);
				//sb.DefaultItem = m.LastItem;
				using(m.Submenu("Sub")) {
					m["si"] = o => print.it(o);
				}
			}, @"C:\code\qm\copy.ico", "SplitButton", o => print.it(o));
			t.Separator("");
			t[true, "DD2", @"C:\code\qm\delete.ico"] = m => {
				print.it("create menu");
				//m.MultiShow = false;
				m["one"] = o => print.it(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => print.it(o);
				}
			};
			//t.SplitButton("SB", o => {
			//	print.it(o);
			//}, m => {
			//	m["one"] = o => print.it(o);
			//	using(m.Submenu("Sub")) {
			//		m["si"] = o => print.it(o);
			//	}
			//}, @"C:\code\qm\copy.ico", "SplitButton");
			//Action<popupMenu> menu1 = m => {
			//	m["one"] = o => print.it(o);
			//	using(m.Submenu("Sub")) {
			//		m["si"] = o => print.it(o);
			//	}
			//};
			//t.MenuButton("DD", menu1, @"C:\code\qm\find.ico", "MenuButton");
#elif false
			t.MenuButton("DD", @"C:\code\qm\find.ico");
			t.Menu = m => {
				m["one"] = o => print.it(o);
				using(m.Submenu("Sub")) {
					m["si"] = o => print.it(o);
				}
			};
#else
			t.MenuButton("DD", @"C:\code\qm\find.ico").Menu = m => {
				print.it("dd");
				//m.MultiShow = false;
				m["one"] = o => print.it(o);
				using(m.Submenu("Sub")) {
					m["two"] = o => print.it(o);
				}
			};
			t.SplitButton("SB", o => print.it(o), @"C:\code\qm\copy.ico").Menu = m => {
				print.it("dd");
				m["one"] = o => print.it(o);
				using(m.Submenu("Sub")) {
					m["two"] = o => print.it(o);
				}
			};
#endif
			//t.Separator("");
			////t["GC"] = o => GC.Collect();

			//var dd = new ToolStripSplitButton("SB2", null, (_, _)=>print.it("click"));
			//t.Add(dd, @"C:\code\qm\delete.ico");
			//dd.DropDownOpening += (_, _) => {
			//	var m = new popupMenu();
			//	dd.DropDown = m.Control;
			//	m["one"] = o => print.it(o);
			//};
			//dd.ButtonClick += (_, _) => print.it("button click");
			//dd.DoubleClickEnabled = true;
			//dd.ButtonDoubleClick += (_, _) => print.it("button double click");

			//timer.after(3000, _ => {
			//	var c = t.Control.Items[0];
			//	c.Select();
			//});

			//void _B(TBBorder b){
			//	t.Border = b;
			//	//print.it(WndUtil.BorderWidth((wnd)t.Control));
			//}

			//t.Bounds = new Rectangle(i * 300 + 700, 200, 200, 200);
			t.Show();
			//t.Window.ActivateL();
			wait.doEvents(200);

			//for(int j = 1; j <= (int)TBBorder.SizableWithCaptionX; j++) {
			//	wait.doEvents(1000);
			//	t.Border = (TBBorder)j;
			//}

			//wait.doEvents(1000);
			//t.Border = TBBorder.FixedWithCaption;
			//wait.doEvents(3000);
			//t.Border = TBBorder.SizableWithCaption;

			//var m = new popupMenu();
			//using(m.Submenu("Sub")) {

			//}
			//m.Show()
		}

		//var c = new System.Windows.Forms.VisualStyles.VisualStyleRenderer(VisualStyleElement.Window.FrameLeft.Inactive).GetColor(ColorProperty.BorderColor);
		//print.it((uint)c.ToArgb());

		//timer.after(500, _ => {
		//	var w = (wnd)t.Control;
		//	//w.SetStyle(WS.DLGFRAME, SetAddRemove.Add);
		//});

		dialog.options.topmostIfNoOwnerWindow = true;
		dialog.show();

		//timer.after(10000, _ => Application.Exit());
		//Application.Run();
	}
#endif

	//[MethodImpl(MethodImplOptions.NoInlining)]
	//void TestCallerArgumentExpression(string so, [CallerArgumentExpression("so")] string ca = null) //does not work
	//{
	//	print.it(so, ca);
	//}

	//void TestBitmapLockBitsDispose() {
	//	//Bitmap.FromFile(@"C:\Test\qm small icon.png").Dispose();
	//	//print.it("start");
	//	//3.s();
	//	//Debug_.MemorySetAnchor_();
	//	//for(int i = 0; i < 1000; i++) {
	//	//	var b = Bitmap.FromFile(@"C:\Test\qm small icon.png") as Bitmap;
	//	//	//var d=b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
	//	//	var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format64bppArgb);
	//	//	b.Dispose();
	//	//	b.UnlockBits(d);
	//	//}
	//	//Debug_.MemoryPrint_();
	//	//while(keys.isScrollLock) 100.ms();


	//	var w = +wnd.find("Au - Microsoft Visual Studio", "HwndWrapper[DefaultDomain;*");
	//	string image = @"image:iVBORw0KGgoAAAANSUhEUgAAAA0AAAAQCAYAAADNo/U5AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAC2SURBVDhPYzhz9cN/DHzlw/+Hp1b8v7osHisGazoNVISOr6/N+39rZ+f/k6efY2C4Jmwm4sJDRdPNjSVgGobvH56BVQMIwzX1rz36nzd40n8Grx4w3bfh3P8Hxxfg1wRSuHzvZXCQgmgQ/+Hj8/g1gWxAjgvpmGlgcYKa0G0CiWPDDCDCo2krWBMMgzTM3XkZnJxAhqJjBpgGEI1sGjbFMAzWBNOATQE2DHYeKRpAGBwQpOEP/wF46o8knB4kYgAAAABJRU5ErkJggg==";
	//	//string image = @"C:\Test\find.bmp";

	//	//for(int i = 0; i < 10; i++) {
	//	//	var im = +uiimage.find(w, image, IFFlags.WindowDC);
	//	//	print.it(im);
	//	//}

	//	var im = uiimage.find(0, w, image, IFFlags.WindowDC);

	//	//var h = Api.LoadImage(default, image, Api.IMAGE_BITMAP, 0, 0, Api.LR_LOADFROMFILE);
	//	//var b = Bitmap.FromHbitmap(h);
	//	//var d = b.LockBits(new Rectangle(default, b.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
	//	//print.it(d.Stride);
	//	//b.UnlockBits(d);

	//	//var im = +uiimage.find(w, b, IFFlags.WindowDC);
	//	print.it(im);

	//}

	static HashSet<object> hs = new HashSet<object>();
	static HashSet<object> hs2 = new HashSet<object>();
	static System.Collections.Concurrent.ConcurrentDictionary<object, byte> cd = new System.Collections.Concurrent.ConcurrentDictionary<object, byte>();

	void TestConcurrentSpeed() {
		var a = new object[100];
		for (int i = 0; i < a.Length; i++) a[i] = new object();

		perf.cpu();
		for (int i1 = 0; i1 < 7; i1++) {
			int n2 = a.Length;
			perf.first();
			for (int i2 = 0; i2 < n2; i2++) { hs.Add(a[i2]); }
			for (int i2 = 0; i2 < n2; i2++) { hs.Remove(a[i2]); }
			perf.next();
			for (int i2 = 0; i2 < n2; i2++) { lock (hs2) hs2.Add(a[i2]); } //20% slower
			for (int i2 = 0; i2 < n2; i2++) { lock (hs2) hs2.Remove(a[i2]); }
			perf.next();
			for (int i2 = 0; i2 < n2; i2++) { cd.TryAdd(a[i2], default); } //50% slower
			for (int i2 = 0; i2 < n2; i2++) { cd.TryRemove(a[i2], out _); }
			perf.next();
			for (int i2 = 0; i2 < n2; i2++) { var gc = GCHandle.Alloc(a[i2]); gc.Free(); } //50% faster
			perf.nw();
			Thread.Sleep(100);
		}
	}

	unsafe static class _Api {
		[DllImport("kernel32.dll", SetLastError = true)]
		internal static extern bool VirtualLock(void* lpAddress, nint dwSize);
		[DllImport("kernel32.dll", SetLastError = true)]
		internal static extern bool VirtualUnlock(void* lpAddress, nint dwSize);

	}

	//	unsafe void TestMemoryAllocSpeed()
	//	{
	//		byte[] a;
	//		byte* p1 = (byte*)Au.More.MemoryUtil.Alloc(1000); Au.More.MemoryUtil.Free(p1);
	//		byte* p2 = (byte*)Api.VirtualAlloc(default, 1000); Api.VirtualFree(p2);
	//		int n = 10_000_000;
	//		//n = 100_000_000;
	//		n = 0x10000;

	//		perf.cpu();
	//		for(int i1 = 0; i1 < 10; i1++) {
	//			perf.first();

	//			//	a = new byte[n];
	//			//	perf.next();
	//			//	fixed(byte* bp = a) {
	//			//		var ip = (long*)bp; for(int j = 0; j < n / 8; j++) ip[j] = j;
	//			//	}

	//			//perf.next();

	//			//	p1 = (byte*)Au.More.MemoryUtil.Alloc(n);
	//			//	perf.next();
	//			//	var ip = (long*)p1; for(int j = 0; j < n / 8; j++) ip[j] = j;
	//			//	perf.next();
	//			//	Au.More.MemoryUtil.Free(p1);

	//			//perf.next();

	//			//	//p2 = (byte*)Api.VirtualAlloc(default, n);
	//			//	p2 = (byte*)Api.VirtualAlloc(default, n, Api.MEM_COMMIT | Api.MEM_RESERVE, Api.PAGE_READWRITE);
	//			//	perf.next();
	//			//	var ip = (long*)p2; for(int j = 0; j < n / 8; j++) ip[j] = j;
	//			//	perf.next();
	//			//	Api.VirtualFree(p2);

	////			//test realloc speed
	////			p1 = (byte*)Au.More.MemoryUtil.Alloc(n);
	////			perf.next();
	////			var ip = (long*)p1; for(int j = 0; j < n / 8; j++) ip[j] = j;
	////			perf.next();
	////#if true
	////			Au.More.MemoryUtil.Free(p1);
	////			var p1c = (byte*)Au.More.MemoryUtil.Alloc(n + 20000);
	////#else
	////			var p1c = (byte*)Au.More.MemoryUtil.ReAlloc(p1, n + 20000);
	////#endif
	////			perf.next();
	////			Au.More.MemoryUtil.Free(p1c);

	//			perf.nw();
	//			//print.it(p1c == p1);
	//			Thread.Sleep(100);
	//		}
	//	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
	bool TestArrayExt(string[] a) {
		return a.NE_();
	}
	//int TestArrayExt(string[] a)
	//{
	//	return a.Len_();
	//}

	//	void TestMarkdig() {
	//		string markdown = @"List:
	//- one.
	//- two.

	//	";

	//		string html = Markdig.Markdown.ToHtml(markdown);
	//		print.it(html);
	//	}

	//	/// <summary>
	//	/// <para>List:</para>
	//	/// <list type="bullet">
	//	/// <item>ONE.</item>
	//	/// <item>TWO.</item>
	//	/// </list>
	//	/// 
	//	/// <p>List:</p>
	//	/// <ul>
	//	/// <li>one.</li>
	//	/// <li>two.</li>
	//	/// </ul>
	//	/// </summary>
	//	void TestMarkdownXmlDocComments() {
	//		string markdown = @"List:
	//- one.
	//- two.

	//<para>List:</para>
	//<list type=""bullet"">
	//<item>ONE.</item>
	//<item>TWO.</item>
	//</list>

	//<p>List:</p>
	//<ul>
	//<li>one.</li>
	//<li>two.</li>
	//</ul>

	//";

	//		string html = Markdig.Markdown.ToHtml(markdown);
	//		html = html.RxReplace(@"(?m)^", "/// ");
	//		print.it(html);
	//	}

	//void TestMenuDropdownBug()
	//{
	//	bool test = keys.isScrollLock;
	//	{
	//		var m = new ToolStripDropDownMenu();
	//		var k = m.Items;
	//		k.Add("0");
	//	}
	//	for(int j = 0; j < 1; j++) {
	//		Debug_.MemorySetAnchor_();

	//		perf.first();
	//		var m = new ToolStripDropDownMenu();
	//		var k = m.Items;
	//		if(test) m.ShowItemToolTips = false;

	//		for(int i = 0; i < 100; i++) {
	//			k.Add("0");
	//		}
	//		perf.next();
	//		//foreach(ToolStripMenuItem v in m.Items) {
	//		//	if(v.HasDropDown) print.it("has");
	//		//}
	//		//perf.next();
	//		//if(test) {
	//		//	m.ShowItemToolTips = true;
	//		//	perf.next();
	//		//}

	//		Debug_.MemoryPrint_();
	//		perf.write();

	//		//m.Show();

	//		//Debug_.MemoryPrint_();

	//		100.ms();
	//	}
	//}

	//class Implement : IDisposable
	//{
	//	int i;
	//}

	//class Implement2 : IEnumerable<int>
	//{

	//}

	//public interface ITest : ITestBase
	//{
	//	bool PropDef => true;
	//	bool Prop { get; set; }
	//	bool PropGet { get; }
	//	bool PropSet { set; }
	//	bool Meth(int i, out RECT r);
	//	void MethDef(int j) { }
	//	event EventHandler Event;
	//	int this[int index] { get; set; }
	//	bool Prop2 { get; protected set; }
	//	ref int Ref();
	//	internal bool PropInternal { get; set; }
	//	//	protected bool PropInternal { get; set; }

	//	const int Const = 5;
	//	static ITest() { }
	//	public static ITest operator +(ITest a, ITest b) => default;
	//	public struct Struct { int i; }
	//	static int s_i;
	//	public static void StatMeth() { }
	//	//	void ITestBase.OfBase() {  }
	//}

	//public interface ITestBase
	//{
	//	void OfBase();
	//}

	//class Implement3 : ITest
	//{
	//}

	//void One(bool two) { }
	//bool Two() => false;

	//	void TestFirefoxElm() {
	//		try {
	//			//var w = +wnd.find("*- Mozilla Firefox", "MozillaWindowClass");
	//			//var a = elm.fromWindow(w);


	//			print.it(a.MiscFlags, a);

	//			if (a.GetProperties("@", out var p)) {
	//				print.it(p.HtmlAttributes);
	//			}

	//		}
	//		finally {
	//#if DEBUG
	//			Cpp.DebugUnload();
	//#endif
	//		}
	//	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	void TestUnsafe() {
		//var a = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
		//ulong k = Unsafe.As<byte, ulong>(ref a[0]);
		//print.it(k);

		//a[2] = Unsafe.As<ulong, byte>(ref k);
		//print.it(a);

		var f = keys.isScrollLock ? FAFlags.DontThrow : FAFlags.UseRawPath;
		//f.SetFlag2(EFFlags.Reverse, true);
		//print.it(f);
		//f.SetFlag2(EFFlags.Reverse, false);
		//print.it(f);

		int k1 = 0, k2 = 0, k3 = 0, k4 = 0, k5 = 0;
		perf.cpu();
		//if(f.HasAny(EFFlags.HiddenToo)) k3++;
		for (int i1 = 0; i1 < 7; i1++) {
			int n2 = 10000;
			perf.first();
			for (int i2 = 0; i2 < n2; i2++) { f.SetFlag(FAFlags.DontThrow, true); f.SetFlag(FAFlags.DontThrow, false); }
			perf.next();
			for (int i2 = 0; i2 < n2; i2++) { if (f.HasFlag(FAFlags.UseRawPath)) k1++; }
			perf.next();
			for (int i2 = 0; i2 < n2; i2++) { if (f.Has(FAFlags.UseRawPath)) k2++; }
			perf.next();
			for (int i2 = 0; i2 < n2; i2++) { if (f.HasAny(FAFlags.UseRawPath)) k3++; }
			//perf.next();
			//for(int i2 = 0; i2 < n2; i2++) { if(f.HasAny5(EFFlags.HiddenToo)) k5++; }
			perf.nw();
			Thread.Sleep(200);
		}
		print.it(f, k1, k2, k3, k4, k5);
	}

	void TestNetCoreVersion() {
		print.it(folders.NetRuntime);
	}

	//[StructLayout(LayoutKind.Explicit, Size = 500000)]
	//struct BIGBIG { public override string ToString() => "TEST"; }
	//void TestStringInterpolationBoxing() {
	//	BIGBIG r = default;
	//	Debug_.MemorySetAnchor_();
	//	var s = $"a {r}?"; //result: boxes, although .ToString() causes an IDE suggestion to remove it
	//	Debug_.MemoryPrint_();
	//	print.it(s);
	//}

	void TestMinusSign() {
		print.clear();
		var v = -5;
		var s = v.ToString();
		print.it((uint)s[0]);
		print.it(Convert.ToInt32(s), int.Parse(s), s.ToInt());
		s = "-6";
		print.it((uint)s[0]);
		print.it(Convert.ToInt32(s), int.Parse(s), s.ToInt());

	}

	unsafe void TestNint() {
		//int i = -100;
		//nint n = i;
		//var s1 = n.ToString();
		//var s2 = n.ToS();
		//print.it(n, s1, s2, (uint)s1[0], (uint)s2[0]);

		////int i =unchecked((int) uint.MaxValue);
		long i = long.MaxValue;
		//nuint n = (nuint)i;

		//IntPtr n = (IntPtr)i;
		nint n = (nint)i;
		int k = (int)n;
		print.it(n, k);

		//nint n = (uint)8;
		//nint n = (IntPtr)5;
		//IntPtr p = n;
		//n = (nint)(void*)null;

		//Coord c = 6;
	}

	void TestWpfWindow() {
		//var w = new Au.Tests.MainWindow();
		var w = new Au.Tests.Window1();
		//var w = new Window();
		w.ShowDialog();
	}

	//System.PlatformNotSupportedException: System.Management currently is only supported for Windows desktop applications.
	//	void TestWMI() {
	//print.clear();

	//var interval = new TimeSpan( 0, 0, 1 );
	//const string isWin32Process = "TargetInstance isa \"Win32_Process\"";

	//// Listen for started processes.
	//WqlEventQuery startQuery
	//    = new WqlEventQuery( "__InstanceCreationEvent", interval, isWin32Process );
	//var _startWatcher = new ManagementEventWatcher( startQuery );
	//_startWatcher.Start();
	//_startWatcher.EventArrived += (_,e)=> { print.it("start", e.Context, e.NewEvent); };

	//// Listen for closed processes.
	//WqlEventQuery stopQuery
	//    = new WqlEventQuery( "__InstanceDeletionEvent", interval, isWin32Process );
	//var _stopWatcher = new ManagementEventWatcher( stopQuery );
	//_stopWatcher.Start();
	//_stopWatcher.EventArrived += (_,e)=> { print.it("end", e.Context, e.NewEvent); };


	//dialog.show("");
	//	}

	//void TestWER() {
	//	var f = new Window();
	//	f.MouseLeftButtonUp += (_, _) => {
	//		print.it("click");
	//		throw new Exception();
	//	};
	//	f.ShowDialog();

	//	//WndUtil.RegisterWindowClass("fffttt", _WP);
	//	//var w = WndUtil.CreateWindow("fffttt", "fffttt", WS.VISIBLE | WS.SYSMENU | WS.CAPTION, 0, 500, 400, 300, 300);
	//	//print.it(w);
	//	//while (Api.GetMessage(out var mm) > 0) Api.DispatchMessage(mm);

	//	//static nint _WP(wnd w, int m, nint wp, nint lp) {
	//	//	if (m == Api.WM_LBUTTONUP) {
	//	//		print.it("click");
	//	//		throw new Exception();
	//	//	}
	//	//	if (m == Api.WM_DESTROY) Api.PostQuitMessage(0);
	//	//	return Api.DefWindowProc(w, m, wp, lp);
	//	//}
	//}

	//void TestScripting() {
	//	object result = CSharpScript.EvaluateAsync("1 + 2").Result;
	//	print.it(result);
	//}

	HashSet<string> _GetDotnetAssemblies() {
		var s = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
		var a = s.Split(';');
		var h = new HashSet<string>(a.Length);
		foreach (var v in a) {
			int i = v.LastIndexOfAny(new[] { '\\', '/' }) + 1;
			var s1 = pathname.getNameNoExt(v);
			//if (s1.Starts("Au.")) break;
			h.Add(s1);
		}
		return h;
	}

	//static unsafe class _Api {
	//	[DllImport("FreeImage")]
	//	//[DllImport(@"sub\FreeImage")]
	//	//[DllImport(@"m\sub\FreeImage")]
	//	public static extern int FreeImage_ZLibCRC32(int crc, byte[] data, int size);

	//}

	void _Main() {
		//Windows.Win32.UI.WindowsAndMessaging.TITLEBARINFO ti = new();
		//Windows.Win32.PInvoke.GetTitleBarInfo(default, ref ti);

		//Windows.Win32.Graphics.Direct2D.ID2D1Factory f;

		//var a = new byte[] { 1, 2, 3, 4 };
		//print.it(_Api.FreeImage_ZLibCRC32(0, a, a.Length));

		//var rx = new regexp("a");
		//dialog.show("");

		//TestScript ts = null;

		//print.clear();
		//var h = _GetDotnetAssemblies();
		////print.it(h.OrderBy(o => o));
		//print.it(h);

		//var s = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
		//var a = s.Split(';');
		//print.it(a);

		//_=Microsoft.PowerShell.Commands.Utility.JoinStringCommand.CommonParameters;

		//var k=new System.Runtime.Loader.AssemblyDependencyResolver(@"C:\code\au\Tests\bin\Debug\net6.0-windows10.0.18362.0\Au.Tests.dll");
		//string s;
		//s = @"C:\code\au\Tests\bin\Debug\net6.0-windows10.0.18362.0\Markdig.Signed.dll";
		//s = @"C:\code\au\Tests\bin\Debug\net6.0-windows10.0.18362.0\runtimes\win10-x64\lib\netstandard1.6\Microsoft.Management.Infrastructure.dll";
		//print.it("path", k.ResolveAssemblyToPath(AssemblyName.GetAssemblyName(s)));

		//dialog.show("tests");
		//run.it("notepad.exe");

		//using var db = new sqlite(":memory:");
		//var rx = new regexp("d");
		//dialog.show("tests", $"{db}, {rx}");


		//		var psCode = """
		//& 'C:\Program Files\Windows NT\Accessories\wordpad.exe'
		//""";

		//		using var ps = PowerShell.Create();

		//		ps.AddScript(psCode);

		//		//ps.AddArgument(a1);
		//		//ps.AddArgument(a2);

		//		ps.Invoke();
		//		print.it("ok");


		//print.it(Environment.CurrentManagedThreadId); //6
		//var isOnline = await Task.Run(() => true);
		////var isOnline = await Task.Run(() => true).ConfigureAwait(true);
		////var isOnline = Task.Run(() => true).Result;
		//print.it(Environment.CurrentManagedThreadId, isOnline); //8

		//dialog.show("");
		//print.it(AppContext.BaseDirectory, AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES"));

		//var t = FreeImageAPI.FreeImage.CreateTag();
		//print.it(t);

		//TestMarkdig();
		//TestScripting();

		//Application.SetCompatibleTextRenderingDefault(false);
		//print.it("before");
		//Debug_.WriteLoadedAssemblies(true, true, true);

		//TestWMI();
		//new Script().Test();
		//TestSvg();
		//TestWpfWindow();
		//TestNint();
		//TestMinusSign();
		//TestStringInterpolationBoxing();
		//TestNetCoreVersion();

		//var v = new DirectoryInfo(@"C:\Test");
		//var o = new EnumerationOptions { RecurseSubdirectories = true };
		//var e = v.EnumerateFileSystemInfos("*", o);
		//foreach(var f in e) {
		//	print.it(e);
		//}

		//dialog.show();
		//var b = new wpfBuilder("Window");
		//b.ShowDialog();
	}

	[DllImport("kernel32.dll")]
	internal static extern uint GetErrorMode();

	[STAThread] static void Main(string[] args) { new TestScript(args); }
	TestScript(string[] args) {
		process.thisProcessCultureIsInvariant = true;
		//print.qm2.use = true;
		//print.clear();

		//TestWER(); return;
		//Console.WriteLine();
		//Api.SetErrorMode(Api.SEM_NOGPFAULTERRORBOX | Api.SEM_FAILCRITICALERRORS);
		//for (int i = 0; i < 20; i++) {
		//	var em = GetErrorMode();
		//	if (em != 3) print.it(i, em);
		//	1.ms();
		//}
		////throw new Exception();
		//return;

		//perf.first();
		try {
			_Main();
		}
		catch (Exception ex) { print.it(ex); }
	}
}
#endif
