using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using nint = System.IntPtr;
using wnd = System.IntPtr;

[module: DefaultCharSet(CharSet.Unicode)]

static class Program {
	static void Main(string[] args) {
		if(args.Length == 2) {
			Cpp_Arch(args[0], args[1]);
		} else if(args.Length == 0) {
			Cpp_Unload(1);
		}
	}

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_Arch(string a0, string a1);

	/// <param name="flags">1 - wait less.</param>
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_Unload(uint flags);
}

#if !true
static class qm {
	public static void clear() => _Write(null);

	public static void write(string s) => _Write(s ?? "");

	static void _Write(string s) {
		if (!api.IsWindow(_wQM)) {
			_wQM = api.FindWindow("QM_Editor", null);
			if (_wQM == default) return;
		}
		api.SendMessageS(_wQM, api.WM_SETTEXT, (nint)(-1), s);
	}
	static wnd _wQM;
}

unsafe class api {
	[DllImport("user32.dll")]
	internal static extern bool IsWindow(wnd hWnd);

	[DllImport("user32.dll", EntryPoint = "FindWindowW", SetLastError = true)]
	internal static extern wnd FindWindow(string lpClassName, string lpWindowName);

	[DllImport("user32.dll", EntryPoint = "SendMessageW", SetLastError = true)]
	internal static extern nint SendMessageS(wnd hWnd, uint Msg, nint wParam, string lParam);

	internal const ushort WM_SETTEXT = 0xC;

	[DllImport("user32.dll", EntryPoint = "MessageBoxW", SetLastError = true)]
	internal static extern int MessageBox(wnd hWnd, string lpText, string lpCaption, uint uType);

	internal const uint MB_TOPMOST = 0x40000;
}
#endif
