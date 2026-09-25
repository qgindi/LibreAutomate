using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

[module: DefaultCharSet(CharSet.Unicode)]

class Net4 {
	[STAThread]
	static int Main(string[] args) {
		Console.InputEncoding = Encoding.UTF8;
		Console.OutputEncoding = Encoding.UTF8;

		switch (args) {
		case ["/typelib", _]:
			return TypelibConverter.Convert(args[1]);
		}
		return 1;
	}

	//public static void Msgbox(string text) {
	//	api.MessageBox(0, text, "LA setup", api.MB_TOPMOST | api.MB_TOPMOST);
	//}
}

//#pragma warning disable 649, 169 //field never assigned/used
//unsafe class api {
//	internal const uint MB_SETFOREGROUND = 0x10000;
//	internal const uint MB_TOPMOST = 0x40000;

//	[DllImport("user32.dll", EntryPoint = "MessageBoxW", SetLastError = true)]
//	internal static extern int MessageBox(nint hWnd, string lpText, string lpCaption, uint uType);
//}
//#pragma warning restore 649, 169 //field never assigned/used
