#if !EMPTY

#pragma warning disable 649, 169 //field never assigned/used
unsafe class api
#if NET
	: Au.Types.NativeApi
#endif
	{
	[DllImport("kernel32.dll", EntryPoint = "GetFileAttributesW", SetLastError = true)]
	internal static extern FileAttributes GetFileAttributes(string lpFileName);
	
	[DllImport("kernel32.dll", EntryPoint = "MoveFileExW", SetLastError = true)]
	internal static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);
	
	internal const uint MOVEFILE_REPLACE_EXISTING = 0x1;
	internal const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;
	
	[DllImport("user32.dll")]
	internal static extern bool IsWindow(nint hWnd);
	
	[DllImport("user32.dll", EntryPoint = "SendMessageW", SetLastError = true)]
	internal static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);
	
	[DllImport("user32.dll", EntryPoint = "FindWindowExW", SetLastError = true)]
	internal static extern nint FindWindowEx(nint hWndParent, nint hWndChildAfter, string lpszClass, string lpszWindow);
	
	[DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
	internal static extern nint SendMessageTimeout(nint hWnd, uint Msg, nint wParam, nint lParam, uint fuFlags, int uTimeout, out nuint lpdwResult);
	
	internal const int HWND_MESSAGE = -3;
	internal const int HWND_BROADCAST = 65535;
	
	internal const uint SMTO_ABORTIFHUNG = 0x2;
	
	internal const int WM_SETTEXT = 0x000C;
	internal const int WM_CLOSE = 0x10;
	
	[DllImport("user32.dll", EntryPoint = "SendNotifyMessageW", SetLastError = true)]
	internal static extern bool SendNotifyMessage(nint hWnd, uint Msg, nint wParam, nint lParam);
	
	[ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
	internal class ShellLink { }
	
	[ComImport, Guid("000214f9-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellLinkW {
		void _0();
		nint _1();
		void _2();
		void _3();
		void _4();
		void _5();
		void _6();
		void _7();
		void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
		ushort _8();
		void _9();
		int _10();
		void _11();
		int _12();
		void _13();
		void _14();
		void _15();
		void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
	}
	
	[ComImport, Guid("0000010b-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IPersistFile {
		Guid _0();
		void _1();
		void _2();
		void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int fRemember);
	}
	
	[DllImport("user32.dll")]
	internal static extern nint GetForegroundWindow();
}
#pragma warning restore 649, 169 //field never assigned/used
#endif
