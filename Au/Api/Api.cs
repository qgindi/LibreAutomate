//[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32|DllImportSearchPath.UserDirectories)]

#pragma warning disable 649, 169 //field never assigned/used

namespace Au.Types;

[DebuggerStepThrough]
static unsafe partial class Api {
	#region util
	
	/// <summary>
	/// Gets the native size of a struct variable.
	/// Returns Marshal.SizeOf(typeof(T)).
	/// Speed: the same (in Release config) as Marshal.SizeOf(typeof(T)), and 2 times faster than Marshal.SizeOf(v).
	/// </summary>
	internal static int SizeOf<T>(T v) => Marshal.SizeOf<T>();
	
	/// <summary>
	/// Gets the native size of a type.
	/// Returns Marshal.SizeOf(typeof(T)).
	/// </summary>
	internal static int SizeOf<T>() => Marshal.SizeOf<T>();
	
	/// <summary>
	/// Gets dll module handle (Api.GetModuleHandle) or loads dll (NativeLibrary.TryLoad), and returns unmanaged exported function address (Api.GetProcAddress).
	/// See also: GetDelegate.
	/// </summary>
	internal static IntPtr GetProcAddress(string dllName, string funcName) {
		IntPtr hmod = GetModuleHandle(dllName);
		if (hmod == default && !NativeLibrary.TryLoad(dllName, out hmod)) return default;
		
		return GetProcAddress(hmod, funcName);
	}
	
	/// <summary>
	/// Calls <see cref="GetProcAddress(string, string)"/> (loads dll or gets handle) and <see cref="Marshal.GetDelegateForFunctionPointer{TDelegate}(IntPtr)"/>.
	/// </summary>
	internal static bool GetDelegate<T>(out T deleg, string dllName, string funcName) where T : class {
		IntPtr fa = GetProcAddress(dllName, funcName); if (fa == default) { deleg = null; return false; }
		deleg = Marshal.GetDelegateForFunctionPointer<T>(fa);
		return deleg != null;
	}
	
	/// <summary>
	/// Calls API <see cref="GetProcAddress(IntPtr, string)"/> and <see cref="Marshal.GetDelegateForFunctionPointer{TDelegate}(IntPtr)"/>.
	/// </summary>
	internal static bool GetDelegate<T>(out T deleg, IntPtr hModule, string funcName) where T : class {
		deleg = null;
		IntPtr fa = GetProcAddress(hModule, funcName); if (fa == default) return false;
		deleg = Marshal.GetDelegateForFunctionPointer<T>(fa);
		return deleg != null;
	}
	
	/// <summary>
	/// If o is not <c>null</c>, calls <see cref="Marshal.ReleaseComObject"/>.
	/// </summary>
	internal static void ReleaseComObject<T>(T o) where T : class {
		if (o != null) Marshal.ReleaseComObject(o);
	}
	
	#endregion
	
	#region gdi32
	
	[DllImport("gdi32.dll")] //this and many other GDI functions don't use SetLastError
	internal static extern bool DeleteObject(IntPtr ho);
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);
	
	internal const int RGN_AND = 1;
	internal const int RGN_OR = 2;
	internal const int RGN_XOR = 3;
	internal const int RGN_DIFF = 4;
	internal const int RGN_COPY = 5;
	
	[DllImport("gdi32.dll")]
	internal static extern int CombineRgn(IntPtr hrgnDst, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int iMode);
	
	[DllImport("gdi32.dll")]
	internal static extern bool SetRectRgn(IntPtr hrgn, int left, int top, int right, int bottom);
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr CreateRectRgnIndirect(in RECT lprect);
	
	[DllImport("gdi32.dll")]
	internal static extern bool PtInRegion(IntPtr hrgn, int x, int y);
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);
	
	[DllImport("gdi32.dll")]
	internal static extern bool DeleteDC(IntPtr hdc);
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
	
	[DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
	internal static extern int GetObject(IntPtr h, int c, void* pv);
	
	/// <param name="mode">1 transparent, 2 opaque.</param>
	[DllImport("gdi32.dll")]
	internal static extern int SetBkMode(IntPtr hdc, int mode);
	
	[DllImport("gdi32.dll")]
	internal static extern int SetBkColor(IntPtr hdc, int color);
	
	[DllImport("gdi32.dll", EntryPoint = "TextOutW")]
	internal static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int c);
	
	[DllImport("gdi32.dll", EntryPoint = "TextOutW")]
	internal static extern bool TextOut(IntPtr hdc, int x, int y, char* lpString, int c);
	
	[DllImport("gdi32.dll", EntryPoint = "ExtTextOutW")]
	internal static extern bool ExtTextOut(IntPtr hdc, int x, int y, uint options, in RECT lprect, char* lpString, int c, int* lpDx = null);
	internal const uint ETO_CLIPPED = 0x4;
	
	[DllImport("gdi32.dll")]
	internal static extern bool MoveToEx(IntPtr hdc, int x, int y, out POINT lppt);
	
	[DllImport("gdi32.dll")]
	internal static extern bool GetCurrentPositionEx(IntPtr hdc, out POINT lppt);
	
	[DllImport("gdi32.dll")]
	internal static extern uint SetTextAlign(IntPtr hdc, uint align);
	
	[DllImport("gdi32.dll")]
	internal static extern int SetTextColor(IntPtr hdc, int color);
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr CreatePen(int iStyle, int cWidth, int color);
	
	[DllImport("gdi32.dll")]
	internal static extern bool LineTo(IntPtr hdc, int x, int y);
	
	[DllImport("gdi32.dll")] //tested: does not set last error
	internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);
	
	[DllImport("gdi32.dll")]
	internal static extern int GetDeviceCaps(IntPtr hdc, int index);
	
	[DllImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W")]
	internal static extern bool GetTextExtentPoint32(IntPtr hdc, string lpString, int c, out SIZE psizl);
	
	[DllImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W")]
	internal static extern bool GetTextExtentPoint32(IntPtr hdc, char* lpString, int c, out SIZE psizl);
	
	[DllImport("gdi32.dll", EntryPoint = "CreateFontW")]
	internal static extern IntPtr CreateFont(int cHeight, int cWidth = 0, int cEscapement = 0, int cOrientation = 0, int cWeight = 0, int bItalic = 0, int bUnderline = 0, int bStrikeOut = 0, int iCharSet = 0, int iOutPrecision = 0, int iClipPrecision = 0, int iQuality = 0, int iPitchAndFamily = 0, string pszFaceName = null);
	
	[DllImport("gdi32.dll", EntryPoint = "CreateFontIndirectW")]
	internal static extern IntPtr CreateFontIndirect(in LOGFONT lplf);
	
	internal const uint SRCCOPY = 0xCC0020;
	internal const uint CAPTUREBLT = 0x40000000;
	
	[DllImport("gdi32.dll")] //tested: in some cases does not set last error even if returns false
	internal static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, uint rop);
	
	[DllImport("gdi32.dll")]
	internal static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, uint rop);
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr CreateDIBSection(IntPtr hdc, in Api.BITMAPINFO pbmi, uint usage, out uint* ppvBits, IntPtr hSection = default, uint offset = 0);
	
	internal struct BITMAPINFOHEADER {
		public int biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}
	
	/// <summary>
	/// BITMAPINFOHEADER members and 3 uints for color table etc.
	/// </summary>
	internal struct BITMAPINFO {
		public readonly int biSize;
		public int biWidth;
		public int biHeight;
		public ushort biPlanes;
		public ushort biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
		public fixed uint bmiColors[3]; //info: GetDIBits(DIB_RGB_COLORS) sets 0xFF0000, 0xFF00, 0xFF. Note: with 8-bit colors bitmaps need 256, but this library does not use it.
		
		/// <summary>
		/// Sets <c>biSize=sizeof(BITMAPINFOHEADER)</c>. Note: it is less than <c>sizeof(BITMAPINFO)</c>.
		/// </summary>
		public BITMAPINFO() { biSize = sizeof(BITMAPINFOHEADER); }
		
		/// <summary>
		/// Sets width/height/bitcount/planes fields. Sets <c>biSize=sizeof(BITMAPINFOHEADER)</c>. Note: it is less than <c>sizeof(BITMAPINFO)</c>.
		/// </summary>
		public BITMAPINFO(int width, int height, int bitCount = 32) {
			biSize = sizeof(BITMAPINFOHEADER);
			biWidth = width;
			biHeight = height;
			biBitCount = (ushort)bitCount;
			biPlanes = 1;
		}
		
		//little tested
		///// <summary>
		///// Gets DIB bits of compatible bitmap. Uses API <msdn>GetDIBits</msdn>. Returns <c>null</c> if failed.
		///// </summary>
		///// <param name="hb">Bitmap handle.</param>
		///// <param name="topDown">Create top-down DIB.</param>
		///// <param name="dc">Eg <see cref="ScreenDC_"/>.</param>
		///// <param name="palColors">Use DIB_PAL_COLORS.</param>
		//public byte[] GetBitmapBits(IntPtr hb, bool topDown, IntPtr dc, bool palColors = false) {
		//	biBitCount = 0; //biBitCount=(ushort)bitCount; //somehow fails if bitCount is 32 and bitmap is 32-bit
		//	if (0 == GetDIBits(dc, hb, 0, 0, null, ref this, palColors ? 1 : 0)) return null;
		//	var r = new byte[biSizeImage];
		//	fixed (byte* p = r) {
		//		int hei = biHeight; if (topDown) biHeight = -hei;
		//		var k = GetDIBits(dc, hb, 0, hei, p, ref this, palColors ? 1 : 0);
		//		biHeight = hei;
		//		if (k == 0) return null;
		//	}
		//	return r;
		//}
	}
	
	[DllImport("gdi32.dll")]
	internal static extern int GetDIBits(IntPtr hdc, IntPtr hbm, int start, int cLines, void* lpvBits, ref BITMAPINFO lpbmi, int usage);
	
	/// <summary>
	/// lpbmi can be BITMAPINFOHEADER/BITMAPV5HEADER or BITMAPCOREHEADER.
	/// </summary>
	[DllImport("gdi32.dll")]
	internal static extern int SetDIBitsToDevice(IntPtr hdc, int xDest, int yDest, int w, int h, int xSrc, int ySrc, int StartScan, int cLines, void* lpvBits, void* lpbmi, uint ColorUse = 0); //DIB_RGB_COLORS
	
	//internal const int WHITE_BRUSH = 0;
	//internal const int LTGRAY_BRUSH = 1;
	//internal const int GRAY_BRUSH = 2;
	//internal const int DKGRAY_BRUSH = 3;
	//internal const int BLACK_BRUSH = 4;
	//internal const int NULL_BRUSH = 5;
	//internal const int HOLLOW_BRUSH = 5;
	//internal const int WHITE_PEN = 6;
	//internal const int BLACK_PEN = 7;
	//internal const int NULL_PEN = 8;
	//internal const int OEM_FIXED_FONT = 10;
	//internal const int ANSI_FIXED_FONT = 11;
	//internal const int ANSI_VAR_FONT = 12;
	//internal const int SYSTEM_FONT = 13;
	//internal const int DEVICE_DEFAULT_FONT = 14;
	//internal const int DEFAULT_PALETTE = 15;
	//internal const int SYSTEM_FIXED_FONT = 16;
	//internal const int DEFAULT_GUI_FONT = 17;
	//internal const int DC_BRUSH = 18;
	//internal const int DC_PEN = 19;
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr GetStockObject(int i);
	
	[DllImport("gdi32.dll")]
	internal static extern IntPtr CreateSolidBrush(int color);
	
	[DllImport("gdi32.dll")]
	internal static extern int IntersectClipRect(IntPtr hdc, int left, int top, int right, int bottom);
	
	internal struct LOGFONT {
		public int lfHeight;
		public int lfWidth;
		public int lfEscapement;
		public int lfOrientation;
		public int lfWeight;
		public byte lfItalic;
		public byte lfUnderline;
		public byte lfStrikeOut;
		public byte lfCharSet;
		public byte lfOutPrecision;
		public byte lfClipPrecision;
		public byte lfQuality;
		public byte lfPitchAndFamily;
		public fixed char lfFaceName[32];
	}
	
	internal struct NONCLIENTMETRICS {
		public int cbSize;
		public int iBorderWidth;
		public int iScrollWidth;
		public int iScrollHeight;
		public int iCaptionWidth;
		public int iCaptionHeight;
		public LOGFONT lfCaptionFont;
		public int iSmCaptionWidth;
		public int iSmCaptionHeight;
		public LOGFONT lfSmCaptionFont;
		public int iMenuWidth;
		public int iMenuHeight;
		public LOGFONT lfMenuFont;
		public LOGFONT lfStatusFont;
		public LOGFONT lfMessageFont;
		public int iPaddedBorderWidth;
	}
	
	[DllImport("gdi32.dll")] //does not set last error when fails
	internal static extern uint GetPixel(IntPtr hdc, int x, int y);
	
	#endregion
	
	#region advapi32
	
	[DllImport("advapi32.dll")]
	internal static extern int RegSetValueEx(IntPtr hKey, string lpValueName, int Reserved, Microsoft.Win32.RegistryValueKind dwType, void* lpData, int cbData);
	
	[DllImport("advapi32.dll")]
	internal static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, IntPtr Reserved, out Microsoft.Win32.RegistryValueKind dwType, void* lpData, ref int cbData);
	
	internal const uint TOKEN_WRITE = STANDARD_RIGHTS_WRITE | TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT;
	internal const uint TOKEN_SOURCE_LENGTH = 8;
	internal const uint TOKEN_READ = STANDARD_RIGHTS_READ | TOKEN_QUERY;
	internal const uint TOKEN_QUERY_SOURCE = 16;
	internal const uint TOKEN_QUERY = 8;
	internal const uint TOKEN_IMPERSONATE = 4;
	internal const uint TOKEN_EXECUTE = STANDARD_RIGHTS_EXECUTE;
	internal const uint TOKEN_DUPLICATE = 2;
	internal const uint TOKEN_AUDIT_SUCCESS_INCLUDE = 1;
	internal const uint TOKEN_AUDIT_SUCCESS_EXCLUDE = 2;
	internal const uint TOKEN_AUDIT_FAILURE_INCLUDE = 4;
	internal const uint TOKEN_AUDIT_FAILURE_EXCLUDE = 8;
	internal const uint TOKEN_ASSIGN_PRIMARY = 1;
	internal const uint TOKEN_ALL_ACCESS_P = STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE | TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT;
	internal const uint TOKEN_ALL_ACCESS = TOKEN_ALL_ACCESS_P | TOKEN_ADJUST_SESSIONID;
	internal const uint TOKEN_ADJUST_SESSIONID = 256;
	internal const uint TOKEN_ADJUST_PRIVILEGES = 32;
	internal const uint TOKEN_ADJUST_GROUPS = 64;
	internal const uint TOKEN_ADJUST_DEFAULT = 128;
	
	[DllImport("advapi32.dll", SetLastError = true)]
	internal static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out Handle_ TokenHandle);
	
	internal enum TOKEN_INFORMATION_CLASS {
		TokenUser = 1,
		TokenGroups,
		TokenPrivileges,
		TokenOwner,
		TokenPrimaryGroup,
		TokenDefaultDacl,
		TokenSource,
		TokenType,
		TokenImpersonationLevel,
		TokenStatistics,
		TokenRestrictedSids,
		TokenSessionId,
		TokenGroupsAndPrivileges,
		TokenSessionReference,
		TokenSandBoxInert,
		TokenAuditPolicy,
		TokenOrigin,
		TokenElevationType,
		TokenLinkedToken,
		TokenElevation,
		TokenHasRestrictions,
		TokenAccessInformation,
		TokenVirtualizationAllowed,
		TokenVirtualizationEnabled,
		TokenIntegrityLevel,
		TokenUIAccess,
		TokenMandatoryPolicy,
		TokenLogonSid,
		//Win8
		TokenIsAppContainer,
		TokenCapabilities,
		TokenAppContainerSid,
		TokenAppContainerNumber,
		TokenUserClaimAttributes,
		TokenDeviceClaimAttributes,
		TokenRestrictedUserClaimAttributes,
		TokenRestrictedDeviceClaimAttributes,
		TokenDeviceGroups,
		TokenRestrictedDeviceGroups,
		TokenSecurityAttributes,
		TokenIsRestricted,
		TokenProcessTrustLevel,
		TokenPrivateNameSpace,
		MaxTokenInfoClass  // MaxTokenInfoClass should always be the last enum
	}
	
	[DllImport("advapi32.dll", SetLastError = true)]
	internal static extern bool GetTokenInformation(HandleRef TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, void* TokenInformation, uint TokenInformationLength, out uint ReturnLength);
	
	[DllImport("advapi32.dll")]
	internal static extern byte* GetSidSubAuthorityCount(IntPtr pSid);
	
	[DllImport("advapi32.dll")]
	internal static extern uint* GetSidSubAuthority(IntPtr pSid, uint nSubAuthority);
	
	internal enum SECURITY_IMPERSONATION_LEVEL {
		SecurityAnonymous,
		SecurityIdentification,
		SecurityImpersonation,
		SecurityDelegation
	}
	
	internal enum TOKEN_TYPE {
		TokenPrimary = 1,
		TokenImpersonation
	}
	
	[DllImport("advapi32.dll", SetLastError = true)]
	internal static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, SECURITY_ATTRIBUTES lpTokenAttributes, SECURITY_IMPERSONATION_LEVEL ImpersonationLevel, TOKEN_TYPE TokenType, out IntPtr phNewToken);
	
	[DllImport("advapi32.dll", SetLastError = true)]
	internal static extern bool CreateProcessWithTokenW(IntPtr hToken, uint dwLogonFlags, string lpApplicationName, char[] lpCommandLine, uint dwCreationFlags, string lpEnvironment, string lpCurrentDirectory, in STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
	
	internal struct LUID {
		public uint LowPart;
		public int HighPart;
	}
	
	[DllImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", SetLastError = true)]
	internal static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);
	
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct LUID_AND_ATTRIBUTES {
		public LUID Luid;
		public uint Attributes;
	}
	
	internal struct TOKEN_PRIVILEGES {
		public int PrivilegeCount;
		public LUID_AND_ATTRIBUTES Privileges; //[1]
	}
	
	[DllImport("advapi32.dll", SetLastError = true)]
	internal static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, in TOKEN_PRIVILEGES NewState, uint BufferLength, [Out] TOKEN_PRIVILEGES[] PreviousState, IntPtr ReturnLength);
	
	[StructLayout(LayoutKind.Sequential)]
	internal sealed class SECURITY_ATTRIBUTES : IDisposable {
		public int nLength;
		public void* lpSecurityDescriptor;
		public int bInheritHandle;
		
		/// <summary>
		/// Creates SECURITY_ATTRIBUTES from string security descriptor.
		/// securityDescriptor can be <c>null</c>; then lpSecurityDescriptor will be <c>null</c>.
		/// </summary>
		public SECURITY_ATTRIBUTES(string securityDescriptor) {
			nLength = IntPtr.Size * 3;
			if (securityDescriptor != null && !ConvertStringSecurityDescriptorToSecurityDescriptor(securityDescriptor, 1, out lpSecurityDescriptor)) throw new AuException(0, "SECURITY_ATTRIBUTES");
		}
		
		public void Dispose() {
			if (lpSecurityDescriptor != null) {
				LocalFree(lpSecurityDescriptor);
				lpSecurityDescriptor = null;
			}
		}
		
		~SECURITY_ATTRIBUTES() => Dispose();
		
		/// <summary>
		/// Creates SECURITY_ATTRIBUTES that allows UAC low IL processes to open the kernel object.
		/// </summary>
		public static readonly SECURITY_ATTRIBUTES ForLowIL = new SECURITY_ATTRIBUTES("D:NO_ACCESS_CONTROLS:(ML;;NW;;;LW)");
		
		/// <summary>
		/// Creates SECURITY_ATTRIBUTES that allows UAC medium IL processes to open the pipe.
		/// Like of PipeSecurity that allows ReadWrite for AuthenticatedUserSid.
		/// </summary>
		public static readonly SECURITY_ATTRIBUTES ForPipes = new SECURITY_ATTRIBUTES("D:(A;;0x12019b;;;AU)");
	}
	
	[DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", SetLastError = true)]
	internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string StringSecurityDescriptor, uint StringSDRevision, out void* SecurityDescriptor, uint* SecurityDescriptorSize = null);
	
	//[DllImport("advapi32.dll", EntryPoint = "ConvertSecurityDescriptorToStringSecurityDescriptorW")]
	//internal static extern bool ConvertSecurityDescriptorToStringSecurityDescriptor(void* SecurityDescriptor, uint RequestedStringSDRevision, uint SecurityInformation, out char* StringSecurityDescriptor, out uint StringSecurityDescriptorLen);
	
	[DllImport("advapi32.dll", EntryPoint = "InitiateSystemShutdownW", SetLastError = true)]
	internal static extern bool InitiateSystemShutdown(string lpMachineName, string lpMessage, int dwTimeout, bool bForceAppsClosed, bool bRebootAfterShutdown);
	
	
	
	
	#endregion
	
	#region shell32
	
	//[DllImport("shell32.dll")]
	//internal static extern bool IsUserAnAdmin();
	
	internal const uint SHGFI_ICON = 0x000000100;     // get icon;
	internal const uint SHGFI_DISPLAYNAME = 0x000000200;     // get display name;
	internal const uint SHGFI_TYPENAME = 0x000000400;     // get type name;
	internal const uint SHGFI_ATTRIBUTES = 0x000000800;     // get attributes;
	internal const uint SHGFI_ICONLOCATION = 0x000001000;     // get icon location;
	internal const uint SHGFI_EXETYPE = 0x000002000;     // return exe type;
	internal const uint SHGFI_SYSICONINDEX = 0x000004000;     // get system icon index;
	internal const uint SHGFI_LINKOVERLAY = 0x000008000;     // put a link overlay on icon;
	internal const uint SHGFI_SELECTED = 0x000010000;     // show icon in selected state;
	internal const uint SHGFI_ATTR_SPECIFIED = 0x000020000;     // get only specified attributes;
	internal const uint SHGFI_LARGEICON = 0x000000000;     // get large icon;
	internal const uint SHGFI_SMALLICON = 0x000000001;     // get small icon;
	internal const uint SHGFI_OPENICON = 0x000000002;     // get open icon;
	internal const uint SHGFI_SHELLICONSIZE = 0x000000004;     // get shell size icon;
	internal const uint SHGFI_PIDL = 0x000000008;     // pszPath is a pidl;
	internal const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;     // use passed dwFileAttribute;
	internal const uint SHGFI_ADDOVERLAYS = 0x000000020;     // apply the appropriate overlays;
	internal const uint SHGFI_OVERLAYINDEX = 0x000000040;     // Get the index of the overlay;
	
	internal struct SHFILEINFO {
		public IntPtr hIcon;
		public int iIcon;
		public uint dwAttributes;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
		public string szDisplayName;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
		public string szTypeName;
	}
	
	//[DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
	//internal static extern nint SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, int cbFileInfo, uint uFlags);
	
	[DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
	static extern nint _SHGetFileInfo(nint pidl, uint dwFileAttributes, out SHFILEINFO psfi, int cbFileInfo, uint uFlags);
	
	internal static nint SHGetFileInfo(nint pidl, out SHFILEINFO psfi, uint uFlags, uint dwFileAttributes = 0) {
		return _SHGetFileInfo(pidl, dwFileAttributes, out psfi, Api.SizeOf<Api.SHFILEINFO>(), uFlags);
	}
	
	internal static nint SHGetFileInfo(string path, out SHFILEINFO psfi, uint uFlags, uint dwFileAttributes = 0) {
		//if (0 != (uFlags & SHGFI_PIDL)) { //not tested
		//	var pidl = Pidl.FromString_(path);
		//	if (pidl == 0) { psfi = default; return 0; }
		//	try { return _SHGetFileInfo(pidl, dwFileAttributes, out psfi, Api.SizeOf<Api.SHFILEINFO>(), uFlags); }
		//	finally { Marshal.FreeCoTaskMem(pidl); }
		//} else {
		fixed (char* p = path) return _SHGetFileInfo((nint)p, dwFileAttributes, out psfi, Api.SizeOf<Api.SHFILEINFO>(), uFlags);
		//}
	}
	
	//[DllImport("shell32.dll", PreserveSig = true)]
	//internal static extern int SHGetDesktopFolder(out IShellFolder ppshf);
	
	[DllImport("shell32.dll")]
	internal static extern int SHParseDisplayName(string pszName, IntPtr pbc, out IntPtr pidl, uint sfgaoIn, uint* psfgaoOut);
	
	[DllImport("shell32.dll", PreserveSig = true)]
	internal static extern int SHGetNameFromIDList(IntPtr pidl, SIGDN sigdnName, out string ppszName);
	
	[DllImport("shell32.dll", PreserveSig = true)]
	internal static extern int SHCreateShellItem(IntPtr pidlParent, IShellFolder psfParent, IntPtr pidl, out IShellItem ppsi);
	//This classic API supports absolute PIDL and parent+relative PIDL.
	//There are 2 newer API - SHCreateItemFromIDList (absoulte) and SHCreateItemWithParent (parent+relative). They can get IShellItem2 too, which is currently not useful here. Same speed.
	
	//[DllImport("shell32.dll", PreserveSig = true)]
	//internal static extern int SHCreateItemFromIDList(IntPtr pidl, in Guid riid, out IShellItem ppv); //or IShellItem2
	
	[DllImport("shell32.dll", PreserveSig = true)]
	internal static extern int SHBindToParent(IntPtr pidl, in Guid riid, out IShellFolder ppv, out IntPtr ppidlLast);
	
	[DllImport("shell32.dll", PreserveSig = true)]
	internal static extern int SHGetPropertyStoreForWindow(wnd hwnd, in Guid riid, out IPropertyStore ppv);
	
	internal static PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY() { fmtid = new Guid(0x9F4C2855, 0x9F79, 0x4B39, 0xA8, 0xD0, 0xE1, 0xD4, 0x2D, 0xE1, 0xD5, 0xF3), pid = 5 };
	
	[DllImport("shell32.dll")]
	internal static extern char** CommandLineToArgvW(string lpCmdLine, out int pNumArgs);
	
	[DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true)]
	internal static extern bool Shell_NotifyIcon(int dwMessage, in NOTIFYICONDATA lpData);
	
	internal const int NIM_ADD = 0x0;
	internal const int NIM_MODIFY = 0x1;
	internal const int NIM_DELETE = 0x2;
	internal const int NIM_SETFOCUS = 0x3;
	internal const int NIM_SETVERSION = 0x4;
	
	internal const int NOTIFYICON_VERSION_4 = 4;
	
	internal const uint NIF_MESSAGE = 0x1;
	internal const uint NIF_ICON = 0x2;
	internal const uint NIF_TIP = 0x4;
	internal const uint NIF_STATE = 0x8;
	internal const uint NIF_INFO = 0x10;
	internal const uint NIF_GUID = 0x20;
	internal const uint NIF_REALTIME = 0x40;
	internal const uint NIF_SHOWTIP = 0x80;
	
	internal const uint NIS_HIDDEN = 0x1;
	internal const uint NIS_SHAREDICON = 0x2;
	
	internal struct NOTIFYICONDATA {
		/// <summary>
		/// Sets cbSize, hWnd and uFlags.
		/// </summary>
		/// <param name="wNotify"></param>
		/// <param name="nifFlags"></param>
		public NOTIFYICONDATA(wnd wNotify, uint nifFlags = 0) {
			cbSize = Api.SizeOf<Api.NOTIFYICONDATA>();
			hWnd = wNotify;
			uFlags = nifFlags;
		}
		
		public int cbSize;
		public wnd hWnd;
		public int uID;
		public uint uFlags;
		public int uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
		public uint dwState;
		public uint dwStateMask;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
		public int uVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
		public uint dwInfoFlags;
		public Guid guidItem;
		public IntPtr hBalloonIcon;
	}
	
	internal const int NIN_SELECT = 0x400;
	internal const int NIN_KEYSELECT = 0x401;
	internal const int NIN_BALLOONSHOW = 0x402;
	internal const int NIN_BALLOONHIDE = 0x403;
	internal const int NIN_BALLOONTIMEOUT = 0x404;
	internal const int NIN_BALLOONUSERCLICK = 0x405;
	internal const int NIN_POPUPOPEN = 0x406;
	internal const int NIN_POPUPCLOSE = 0x407;
	
	[DllImport("shell32.dll", PreserveSig = true)]
	internal static extern int Shell_NotifyIconGetRect(in NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);
	internal struct NOTIFYICONIDENTIFIER {
		public int cbSize;
		public wnd hWnd;
		public int uID;
		public Guid guidItem;
	}
	
	internal struct SHSTOCKICONINFO {
		public int cbSize;
		public IntPtr hIcon;
		public int iSysImageIndex;
		public int iIcon;
		public fixed char szPath[260];
	}
	
	[DllImport("shell32.dll", PreserveSig = true)]
	internal static extern int SHGetStockIconInfo(StockIcon siid, uint uFlags, ref SHSTOCKICONINFO psii);
	
	[DllImport("shell32.dll", EntryPoint = "#6", PreserveSig = true)]
	internal static extern int SHDefExtractIcon(string pszIconFile, int iIndex, uint uFlags, IntPtr* phiconLarge, IntPtr* phiconSmall, int nIconSize);
	
	internal const int SHIL_LARGE = 0;
	internal const int SHIL_SMALL = 1;
	internal const int SHIL_EXTRALARGE = 2;
	//internal const int SHIL_SYSSMALL = 3;
	internal const int SHIL_JUMBO = 4;
	
	//[DllImport("shell32.dll", EntryPoint = "#727", PreserveSig = true)]
	//internal static extern int SHGetImageList(int iImageList, in Guid riid, out IImageList ppvObj);
	[DllImport("shell32.dll", EntryPoint = "#727", PreserveSig = true)]
	internal static extern int SHGetImageList(int iImageList, in Guid riid, out IntPtr ppvObj);
	
	internal const uint SHCNE_RENAMEITEM = 0x1;
	internal const uint SHCNE_CREATE = 0x2;
	internal const uint SHCNE_DELETE = 0x4;
	internal const uint SHCNE_MKDIR = 0x8;
	internal const uint SHCNE_RMDIR = 0x10;
	internal const uint SHCNE_MEDIAINSERTED = 0x20;
	internal const uint SHCNE_MEDIAREMOVED = 0x40;
	internal const uint SHCNE_DRIVEREMOVED = 0x80;
	internal const uint SHCNE_DRIVEADD = 0x100;
	internal const uint SHCNE_NETSHARE = 0x200;
	internal const uint SHCNE_NETUNSHARE = 0x400;
	internal const uint SHCNE_ATTRIBUTES = 0x800;
	internal const uint SHCNE_UPDATEDIR = 0x1000;
	internal const uint SHCNE_UPDATEITEM = 0x2000;
	internal const uint SHCNE_SERVERDISCONNECT = 0x4000;
	internal const uint SHCNE_UPDATEIMAGE = 0x8000;
	internal const uint SHCNE_DRIVEADDGUI = 0x10000;
	internal const uint SHCNE_RENAMEFOLDER = 0x20000;
	internal const uint SHCNE_FREESPACE = 0x40000;
	internal const uint SHCNE_EXTENDED_EVENT = 0x4000000;
	internal const uint SHCNE_ASSOCCHANGED = 0x8000000;
	internal const uint SHCNE_DISKEVENTS = 0x2381F;
	internal const uint SHCNE_GLOBALEVENTS = 0xC0581E0;
	internal const uint SHCNE_ALLEVENTS = 0x7FFFFFFF;
	internal const uint SHCNE_INTERRUPT = 0x80000000;
	
	internal const uint SHCNF_IDLIST = 0x0;
	internal const uint SHCNF_DWORD = 0x3;
	internal const uint SHCNF_PATH = 0x5;
	internal const uint SHCNF_PRINTER = 0x6;
	internal const uint SHCNF_FLUSH = 0x1000;
	internal const uint SHCNF_FLUSHNOWAIT = 0x3000;
	internal const uint SHCNF_NOTIFYRECURSIVE = 0x10000;
	
	[DllImport("shell32.dll")]
	internal static extern void SHChangeNotify(uint wEventId, uint uFlags, string dwItem1, string dwItem2);
	
	internal const uint SEE_MASK_CONNECTNETDRV = 0x80;
	internal const uint SEE_MASK_NOZONECHECKS = 0x800000;
	internal const uint SEE_MASK_UNICODE = 0x4000;
	internal const uint SEE_MASK_FLAG_NO_UI = 0x400;
	internal const uint SEE_MASK_INVOKEIDLIST = 0xC;
	internal const uint SEE_MASK_NOCLOSEPROCESS = 0x40;
	internal const uint SEE_MASK_NOASYNC = 0x100;
	internal const uint SEE_MASK_NO_CONSOLE = 0x8000;
	//internal const uint SEE_MASK_HMONITOR = 0x200000;
	//internal const uint SEE_MASK_WAITFORINPUTIDLE = 0x2000000;
	internal const uint SEE_MASK_FLAG_LOG_USAGE = 0x4000000;
	
	internal struct SHELLEXECUTEINFO {
		public int cbSize;
		public uint fMask;
		public wnd hwnd;
		public string lpVerb;
		public string lpFile;
		public string lpParameters;
		public string lpDirectory;
		public int nShow;
		public IntPtr hInstApp;
		public IntPtr lpIDList;
		public string lpClass;
		public IntPtr hkeyClass;
		public uint dwHotKey;
		public IntPtr hMonitor;
		public Handle_ hProcess;
	}
	
	[DllImport("shell32.dll", EntryPoint = "ShellExecuteExW", SetLastError = true)]
	internal static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO pExecInfo);
	
	[DllImport("shell32.dll", PreserveSig = true)]
	internal static extern int SHOpenFolderAndSelectItems(HandleRef pidlFolder, uint cidl, IntPtr[] apidl, uint dwFlags);
	
	[DllImport("shell32.dll", EntryPoint = "#152")]
	internal static extern int ILGetSize(IntPtr pidl);
	
	[DllImport("shell32.dll", EntryPoint = "#25")]
	internal static extern IntPtr ILCombine(IntPtr pidl1, IntPtr pidl2);
	
	[DllImport("shell32.dll", EntryPoint = "#21")]
	internal static extern bool ILIsEqual(IntPtr pidl1, IntPtr pidl2);
	
	internal const uint FO_MOVE = 0x1;
	internal const uint FO_COPY = 0x2;
	internal const uint FO_DELETE = 0x3;
	internal const uint FO_RENAME = 0x4;
	
	internal const uint FOF_MULTIDESTFILES = 0x1;
	internal const uint FOF_CONFIRMMOUSE = 0x2;
	internal const uint FOF_SILENT = 0x4;
	internal const uint FOF_RENAMEONCOLLISION = 0x8;
	internal const uint FOF_NOCONFIRMATION = 0x10;
	internal const uint FOF_WANTMAPPINGHANDLE = 0x20;
	internal const uint FOF_ALLOWUNDO = 0x40;
	internal const uint FOF_FILESONLY = 0x80;
	internal const uint FOF_SIMPLEPROGRESS = 0x100;
	internal const uint FOF_NOCONFIRMMKDIR = 0x200;
	internal const uint FOF_NOERRORUI = 0x400;
	internal const uint FOF_NOCOPYSECURITYATTRIBS = 0x800;
	internal const uint FOF_NORECURSION = 0x1000;
	internal const uint FOF_NO_CONNECTED_ELEMENTS = 0x2000;
	internal const uint FOF_WANTNUKEWARNING = 0x4000;
	internal const uint FOF_NORECURSEREPARSE = 0x8000;
	internal const uint FOF_NO_UI = 0x614;
	
	internal struct SHFILEOPSTRUCT {
		public wnd hwnd;
		public uint wFunc;
		public string pFrom;
		public string pTo;
		public ushort fFlags;
		
		//workaround for this problem: the 32-bit version of SHFILEOPSTRUCT uses Pack = 1, ie no 2-byte gap after fFlags.
		//	I don't want to use two versions. Then also would need two versions of code that use this struct.
		//	The last two members are not useful, but we need fAnyOperationsAborted. This workaround gets it through a property function.
		//	Update: we don't need fAnyOperationsAborted. We use FOF_SILENT therefore cannot be aborted. And it is unreliable.
		//		But the workaround is tested, on both platformas.
		
#if use_fAnyOperationsAborted
				//public bool fAnyOperationsAborted; //BOOL
				ushort _fAnyOperationsAborted_32, _fAnyOperationsAborted_common, _fAnyOperationsAborted_64;
				public bool fAnyOperationsAborted
				{
					get
					{
						if(_fAnyOperationsAborted_common != 0) return true;
						var v = osVersion.is32BitProcess ? _fAnyOperationsAborted_32 : _fAnyOperationsAborted_64;
						return v != 0;
					}
				}
#else
		private int fAnyOperationsAborted;
#endif
		private IntPtr hNameMappings;
		private string lpszProgressTitle;
		//these are private and not used, because would be at invalid offsets on 32-bit
	}
	
	//internal struct SHFILEOPSTRUCT
	//{
	//	public wnd hwnd;
	//	public uint wFunc;
	//	public string pFrom;
	//	public string pTo;
	//	public ushort fFlags;
	//	public bool fAnyOperationsAborted;
	//	public IntPtr hNameMappings;
	//	public string lpszProgressTitle;
	//}
	
	//[StructLayout(LayoutKind.Sequential, Pack = 1)]
	//internal struct SHFILEOPSTRUCT__32
	//{
	//	public wnd hwnd;
	//	public uint wFunc;
	//	public string pFrom;
	//	public string pTo;
	//	public ushort fFlags;
	//	public bool fAnyOperationsAborted;
	//	public IntPtr hNameMappings;
	//	public string lpszProgressTitle;
	//}
	
	[DllImport("shell32.dll", EntryPoint = "SHFileOperationW")]
	internal static extern int SHFileOperation(in SHFILEOPSTRUCT lpFileOp);
	
	[DllImport("shell32.dll", EntryPoint = "DragQueryFileW")]
	internal static extern int DragQueryFile(IntPtr hDrop, int iFile, char* lpszFile, int cch);
	
	[DllImport("shell32.dll")]
	internal static extern bool IsUserAnAdmin();
	
	[DllImport("shell32.dll", PreserveSig = true, EntryPoint = "SHEmptyRecycleBinW")]
	internal static extern int SHEmptyRecycleBin(wnd hwnd, string pszRootPath, int dwFlags);
	
	
	#endregion
	
	#region shlwapi
	
	[DllImport("shlwapi.dll", EntryPoint = "#176")]
	static extern int IUnknown_QueryService([MarshalAs(UnmanagedType.IUnknown)] object punk, in Guid guidService, in Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvOut);
	
	public static bool QueryService<T>(object from, in Guid guidService, out T result) where T : class {
		bool ok = 0 == IUnknown_QueryService(from, guidService, typeof(T).GUID, out var o);
		result = ok ? (T)o : null;
		return ok;
	}
	
	//[DllImport("shlwapi.dll")]
	//internal static extern uint ColorAdjustLuma(uint clrRGB, int n, bool fScale);
	
	[DllImport("shlwapi.dll")]
	internal static extern void ColorRGBToHLS(int clrRGB, out ushort pwHue, out ushort pwLuminance, out ushort pwSaturation);
	
	[DllImport("shlwapi.dll")]
	internal static extern int ColorHLSToRGB(ushort wHue, ushort wLuminance, ushort wSaturation);
	
	//internal enum ASSOCSTR
	//{
	//	ASSOCSTR_COMMAND = 1,
	//	ASSOCSTR_EXECUTABLE,
	//	ASSOCSTR_FRIENDLYDOCNAME,
	//	ASSOCSTR_FRIENDLYAPPNAME,
	//	ASSOCSTR_NOOPEN,
	//	ASSOCSTR_SHELLNEWVALUE,
	//	ASSOCSTR_DDECOMMAND,
	//	ASSOCSTR_DDEIFEXEC,
	//	ASSOCSTR_DDEAPPLICATION,
	//	ASSOCSTR_DDETOPIC,
	//	ASSOCSTR_INFOTIP,
	//	ASSOCSTR_QUICKTIP,
	//	ASSOCSTR_TILEINFO,
	//	ASSOCSTR_CONTENTTYPE,
	//	ASSOCSTR_DEFAULTICON,
	//	ASSOCSTR_SHELLEXTENSION,
	//	ASSOCSTR_DROPTARGET,
	//	ASSOCSTR_DELEGATEEXECUTE,
	//	ASSOCSTR_SUPPORTED_URI_PROTOCOLS,
	//	ASSOCSTR_PROGID,
	//	ASSOCSTR_APPID,
	//	ASSOCSTR_APPPUBLISHER,
	//	ASSOCSTR_APPICONREFERENCE,
	//	ASSOCSTR_MAX
	//}
	
	//[DllImport("shlwapi.dll", PreserveSig = true, EntryPoint = "AssocQueryStringW")]
	//internal static extern int AssocQueryString(uint flags, /*ASSOCSTR*/ int str, string pszAssoc, string pszExtra, char[] pszOut, ref int pcchOut);
	
	///// <summary>
	///// Returns executable path of file type.
	///// </summary>
	///// <param name="dotExt"></param>
	//internal static string AssocQueryString(string dotExt/*, ASSOCSTR what = ASSOCSTR.ASSOCSTR_EXECUTABLE*/)
	//{
	//	var b = ApiBuffer_.Char_(300, out var n);
	//	int hr = AssocQueryString(0x20, 2, dotExt, null, b, ref n); //ASSOCF_NOTRUNCATE
	//	if(hr == E_POINTER) hr = AssocQueryString(0x20, 2, dotExt, null, b = ApiBuffer_.Char_(n), ref n);
	//	return hr == 0 ? b.ToString(n) : null;
	//}
	
	
	#endregion
	
	#region comctl32
	
	[DllImport("comctl32.dll")]
	internal static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);
	
	[DllImport("comctl32.dll")]
	internal static extern bool ImageList_GetIconSize(IntPtr himl, out int cx, out int cy);
	
	internal const uint TME_LEAVE = 0x2;
	internal const uint TME_NONCLIENT = 0x10;
	internal const uint TME_CANCEL = 0x80000000;
	
	internal struct TRACKMOUSEEVENT {
		public int cbSize;
		public uint dwFlags;
		public wnd hwndTrack;
		public int dwHoverTime;
		
		public TRACKMOUSEEVENT(wnd w, uint flags, int hoverTime = 0) {
			cbSize = sizeof(TRACKMOUSEEVENT);
			hwndTrack = w;
			dwFlags = flags;
			dwHoverTime = hoverTime;
		}
	}
	
	[DllImport("comctl32.dll", EntryPoint = "_TrackMouseEvent")]
	internal static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);
	
	/// <summary>
	/// Calls <b>TrackMouseEvent</b> with <b>TME_LEAVE</b>.
	/// </summary>
	/// <param name="w"></param>
	/// <param name="track"><c>true</c> to start, <c>false</c> to cancel.</param>
	internal static bool TrackMouseLeave(wnd w, bool track) {
		var t = new TRACKMOUSEEVENT(w, track ? TME_LEAVE : TME_LEAVE | TME_CANCEL);
		return TrackMouseEvent(ref t);
	}
	
	[DllImport("comctl32.dll", EntryPoint = "#380", PreserveSig = true)]
	internal static extern int LoadIconMetric(IntPtr hinst, nint pszName, int lims, out IntPtr phico);
	
	[DllImport("comctl32.dll", EntryPoint = "#410")]
	internal static extern bool SetWindowSubclass(wnd hWnd, SUBCLASSPROC pfnSubclass, nint uIdSubclass, nint dwRefData = 0);
	
	internal delegate nint SUBCLASSPROC(wnd hWnd, int uMsg, nint wParam, nint lParam, nint uIdSubclass, nint dwRefData);
	
	[DllImport("comctl32.dll", EntryPoint = "#413")]
	internal static extern nint DefSubclassProc(wnd hWnd, int uMsg, nint wParam, nint lParam);
	
	[DllImport("comctl32.dll", EntryPoint = "#412")]
	internal static extern bool RemoveWindowSubclass(wnd hWnd, SUBCLASSPROC pfnSubclass, nint uIdSubclass);
	
	
	
	
	#endregion
	
	#region oleaut32
	
	[DllImport("oleaut32.dll", EntryPoint = "#6")]
	internal static extern void SysFreeString(char* bstrString);
	
	[DllImport("oleaut32.dll", EntryPoint = "#7")]
	internal static extern int SysStringLen(char* pbstr);
	
	[DllImport("oleaut32.dll", EntryPoint = "#4")]
	internal static extern BSTR SysAllocStringLen(string strIn, int len);
	
	[DllImport("oleaut32.dll", EntryPoint = "#2")]
	internal static extern BSTR SysAllocString(char* psz);
	
	[DllImport("oleaut32.dll", EntryPoint = "#147", PreserveSig = true)]
	internal static extern int VariantChangeTypeEx(ref VARIANT pvargDest, in VARIANT pvarSrc, uint lcid, ushort wFlags, VARENUM vt);
	
	[DllImport("oleaut32.dll", EntryPoint = "#9", PreserveSig = true)]
	internal static extern int VariantClear(ref VARIANT pvarg);
	
	[DllImport("oleaut32.dll", EntryPoint = "#35", PreserveSig = true)]
	internal static extern int GetActiveObject(in Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
	
	
	
	
	#endregion
	
	#region ole32
	
	[DllImport("ole32.dll", PreserveSig = true)]
	internal static extern int PropVariantClear(ref PROPVARIANT pvar);
	
	//internal enum APTTYPE
	//{
	//	APTTYPE_CURRENT = -1,
	//	APTTYPE_STA,
	//	APTTYPE_MTA,
	//	APTTYPE_NA,
	//	APTTYPE_MAINSTA
	//}
	
	//[DllImport("ole32.dll", PreserveSig = true)]
	//internal static extern int CoGetApartmentType(out APTTYPE pAptType, out int pAptQualifier);
	
	[DllImport("ole32.dll", PreserveSig = true)]
	internal static extern int OleInitialize(IntPtr pvReserved);
	
	[DllImport("ole32.dll")]
	internal static extern void OleUninitialize();
	
	[ComImport, Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IDropTarget {
		void DragEnter(System.Runtime.InteropServices.ComTypes.IDataObject d, int grfKeyState, POINT pt, ref int effect);
		void DragOver(int grfKeyState, POINT pt, ref int effect);
		void DragLeave();
		void Drop(System.Runtime.InteropServices.ComTypes.IDataObject d, int grfKeyState, POINT pt, ref int effect);
	}
	
	[DllImport("ole32.dll", PreserveSig = true)]
	internal static extern int RegisterDragDrop(wnd hwnd, IDropTarget pDropTarget);
	
	[DllImport("ole32.dll", PreserveSig = true)]
	internal static extern int RevokeDragDrop(wnd hwnd);
	
	[DllImport("ole32.dll")]
	internal static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium);
	
	[DllImport("ole32.dll", PreserveSig = true)]
	internal static extern int CLSIDFromProgID(string lpszProgID, out Guid lpclsid);
	
	#endregion
	
	#region oleacc
	
	[DllImport("oleacc.dll", PreserveSig = true)]
	internal static extern int AccessibleObjectFromWindow(wnd hwnd, EObjid dwId, in Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object o);
	
	[DllImport("oleacc.dll", PreserveSig = true)]
	internal static extern int AccessibleObjectFromEvent(wnd hwnd, EObjid dwObjectId, int dwChildId, out IntPtr ppacc, out VARIANT pvarChild);
	
	[DllImport("oleacc.dll")]
	internal static extern Handle_ GetProcessHandleFromHwnd(wnd hwnd);
	
	[DllImport("oleacc.dll", PreserveSig = true)]
	internal static extern int CreateStdAccessibleObject(wnd hwnd, EObjid idObject, in Guid riid, out IAccessible ppvObject);
	
	[ComImport, Guid("618736e0-3c3d-11cf-810c-00aa00389b71"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
	internal interface IAccessible {
		IAccessible get_accParent();
		int get_accChildCount();
		[PreserveSig] int get_accChild(VarInt varChild, [MarshalAs(UnmanagedType.IDispatch)] out object ppdispChild);
		string get_accName(VarInt varChild);
		string get_accValue(VarInt varChild);
		string get_accDescription(VarInt varChild);
		VarInt get_accRole(VarInt varChild);
		VarInt get_accState(VarInt varChild);
		string get_accHelp(VarInt varChild);
		int get_accHelpTopic(out string pszHelpFile, VarInt varChild);
		string get_accKeyboardShortcut(VarInt varChild);
		object get_accFocus();
		object get_accSelection();
		string get_accDefaultAction(VarInt varChild);
		void accSelect(ESelect flagsSelect, VarInt varChild);
		void accLocation(out int pxLeft, out int pyTop, out int pcxWidth, out int pcyHeight, VarInt varChild);
		object accNavigate(NAVDIR navDir, VarInt varStart);
		VarInt accHitTest(int xLeft, int yTop);
		void accDoDefaultAction(VarInt varChild);
		void put_accName(VarInt varChild, string szName);
		void put_accValue(VarInt varChild, string szValue);
		
		//NOTE: although some members are obsolete or useless, don't use default implementation, because then exception at run time.
	}
	
#pragma warning disable 169
	internal struct VarInt {
		ushort _vt, _1, _2, _3;
		nint _int, _4;
		public static implicit operator VarInt(int i) => new VarInt { _vt = 3, _int = i + 1 };
		public static implicit operator int(VarInt v) {
			if (v._vt == 3) return (int)v._int - 1;
			Debug_.Print($"VarInt vt={v._vt}, value={v._int}");
			throw new ArgumentException();
		}
	}
#pragma warning restore 169
	
	internal enum NAVDIR { UP = 1, DOWN, LEFT, RIGHT, NEXT, PREVIOUS, FIRSTCHILD, LASTCHILD }
	
	
	#endregion
	
	#region msvcrt
	
	//don't use, because for eg "0xffffffff" returns int.Max instead of -1.
	//[DllImport("msvcrt.dll", EntryPoint = "wcstol", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int strtoi(char* s, char** endPtr = null, int radix = 0);
	
	//don't use the u API because they return 1 if the value is too big and the string contains '-'.
	//[DllImport("msvcrt.dll", EntryPoint = "wcstoul", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern uint strtoui(char* s, char** endPtr = null, int radix = 0);
	
	[DllImport("msvcrt.dll", EntryPoint = "_wcstoi64", CallingConvention = CallingConvention.Cdecl)]
	internal static extern long strtoi64(char* s, char** endPtr = null, int radix = 0);
	//info: ntdll also has wcstol, wcstoul, _wcstoui64, but not _wcstoi64.
	
	//[DllImport("msvcrt.dll", EntryPoint = "_wcstoui64", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern ulong strtoui64(char* s, char** endPtr = null, int radix = 0);
	
	[DllImport("msvcrt.dll", EntryPoint = "_strtoi64", CallingConvention = CallingConvention.Cdecl)]
	internal static extern long strtoi64(byte* s, byte** endPtr = null, int radix = 0);
	
	//not used
	//[DllImport("msvcrt.dll", EntryPoint = "_strtoui64", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern long strtoui64(byte* s, byte** endPtr = null, int radix = 0);
	
	//This is used when working with char*. With C# strings use ExtString.ToInt32 etc.
	internal static int strtoi(char* s, char** endPtr = null, int radix = 0) {
		return (int)strtoi64(s, endPtr, radix);
	}
	
	//This is used with UTF-8 text.
	internal static int strtoi(byte* s, byte** endPtr = null, int radix = 0) {
		return (int)strtoi64(s, endPtr, radix);
	}
	
#if false //not used, because we have ExtString.ToInt32 etc, which has no overflow problems. But it supports only decimal and hex, not any radix.
		/// <summary>
		/// Converts part of string to int.
		/// Returns the int value.
		/// Returns 0 if the string is <c>null</c>, <c>""</c> or does not begin with a number; then <i>numberEndIndex</i> will be = <i>startIndex</i>.
		/// </summary>
		/// <param name="s">String.</param>
		/// <param name="startIndex">Offset in string where to start parsing.</param>
		/// <param name="numberEndIndex">Receives offset in string where the number part ends.</param>
		/// <param name="radix">If 0, parses the string as hexadecimal number if begins with "0x", as octal if begins with "0", else as decimal. Else it can be 2 to 36. Examples: 10 - parse as decimal (don't support "0x" etc); 16 - as hexadecimal (eg returns 26 if string is "1A" or "0x1A"); 2 - as binary (eg returns 5 if string is "101").</param>
		/// <exception cref="ArgumentOutOfRangeException">startIndex is invalid.</exception>
		internal static int strtoi(string s, int startIndex, out int numberEndIndex, int radix = 0)
		{
			int R = 0, len = s == null ? 0 : s.Length - startIndex;
			if(len < 0) throw new ArgumentOutOfRangeException("startIndex");
			if(len != 0)
				fixed (char* p = s) {
					char* t = p + startIndex, e = t;
					R = strtoi(t, &e, radix);
					len = (int)(e - t);
				}
			numberEndIndex = startIndex + len;
			return R;
		}

		/// <summary>
		/// Converts part of string to long.
		/// Returns the long value.
		/// Returns 0 if the string is <c>null</c>, <c>""</c> or does not begin with a number; then <i>numberEndIndex</i> will be = <i>startIndex</i>.
		/// </summary>
		/// <param name="s">String.</param>
		/// <param name="startIndex">Offset in string where to start parsing.</param>
		/// <param name="numberEndIndex">Receives offset in string where the number part ends.</param>
		/// <param name="radix">If 0, parses the string as hexadecimal number if begins with "0x", as octal if begins with "0", else as decimal. Else it can be 2 to 36. Examples: 10 - parse as decimal (don't support "0x" etc); 16 - as hexadecimal (eg returns 26 if string is "1A" or "0x1A"); 2 - as binary (eg returns 5 if string is "101").</param>
		/// <exception cref="ArgumentOutOfRangeException">startIndex is invalid.</exception>
		internal static long strtoi64(string s, int startIndex, out int numberEndIndex, int radix = 0)
		{
			long R = 0;
			int len = s == null ? 0 : s.Length - startIndex;
			if(len < 0) throw new ArgumentOutOfRangeException("startIndex");
			if(len != 0)
				fixed (char* p = s) {
					char* t = p + startIndex, e = t;
					R = strtoi64(t, &e, radix);
					len = (int)(e - t);
				}
			numberEndIndex = startIndex + len;
			return R;
		}

		internal static int strtoi(string s, int startIndex = 0, int radix = 0)
		{
			return strtoi(s, startIndex, out _, radix);
		}

		internal static long strtoi64(string s, int startIndex = 0, int radix = 0)
		{
			return strtoi64(s, startIndex, out _, radix);
		}
#endif
	
	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern char* _ltoa(int value, byte* s, int radix);
	
	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void* memmove(void* to, void* from, nint n);
	
	//[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void* memset(void* ptr, int ch, nint n);
	
	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int memcmp(void* p1, void* p2, nint count);
	
	
	
	#endregion
	
	#region winmm
	
	[DllImport("winmm.dll")]
	internal static extern uint timeBeginPeriod(uint uPeriod);
	
	[DllImport("winmm.dll")]
	internal static extern uint timeEndPeriod(uint uPeriod);
	
	[DllImport("winmm.dll")]
	internal static extern uint waveOutSetVolume(IntPtr hwo, uint dwVolume);
	
	[DllImport("winmm.dll")]
	internal static extern uint waveOutGetVolume(IntPtr hwo, out uint pdwVolume);
	
	[DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
	internal static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);
	
	internal const uint SND_FILENAME = 0x20000;
	internal const uint SND_NODEFAULT = 0x2;
	internal const uint SND_SYSTEM = 0x200000;
	internal const uint SND_ASYNC = 0x1;
	internal const uint SND_ALIAS = 0x10000;
	internal const uint SND_APPLICATION = 0x80;
	
	[DllImport("user32.dll")]
	internal static extern bool MessageBeep(uint uType);
	
	[DllImport("kernel32.dll")]
	internal static extern bool Beep(int dwFreq, int dwDuration);
	
	#endregion
	
	#region dwmapi
	
	internal enum DWMWA {
		NCRENDERING_ENABLED = 1,
		NCRENDERING_POLICY,
		TRANSITIONS_FORCEDISABLED,
		ALLOW_NCPAINT,
		CAPTION_BUTTON_BOUNDS,
		NONCLIENT_RTL_LAYOUT,
		FORCE_ICONIC_REPRESENTATION,
		FLIP3D_POLICY,
		EXTENDED_FRAME_BOUNDS,
		HAS_ICONIC_BITMAP,
		DISALLOW_PEEK,
		EXCLUDED_FROM_PEEK,
		CLOAK,
		CLOAKED,
		FREEZE_REPRESENTATION,
		PASSIVE_UPDATE_MODE,
	}
	
	[DllImport("dwmapi.dll")]
	internal static extern int DwmGetWindowAttribute(wnd hwnd, DWMWA dwAttribute, void* pvAttribute, int cbAttribute);
	
	//[DllImport("dwmapi.dll", PreserveSig = true)]
	//internal static extern int DwmSetWindowAttribute(wnd hwnd, DWMWA dwAttribute, void* pvAttribute, int cbAttribute);
	
	//[DllImport("dwmapi.dll", PreserveSig = true)]
	//internal static extern int DwmRegisterThumbnail(wnd hwndDestination, wnd hwndSource, out IntPtr phThumbnailId);
	
	//[DllImport("dwmapi.dll", PreserveSig = true)]
	//internal static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);
	
	//[DllImport("dwmapi.dll", PreserveSig = true)]
	//internal static extern int DwmQueryThumbnailSourceSize(IntPtr hThumbnail, out SIZE pSize);
	
	//[DllImport("dwmapi.dll", PreserveSig = true)]
	//internal static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, in DWM_THUMBNAIL_PROPERTIES ptnProperties);
	
	//[StructLayout(LayoutKind.Sequential, Pack = 1)]
	//internal struct DWM_THUMBNAIL_PROPERTIES {
	//	public uint dwFlags;
	//	public RECT rcDestination;
	//	public RECT rcSource;
	//	public byte opacity;
	//	public bool fVisible;
	//	public bool fSourceClientAreaOnly;
	//}
	
	//internal const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x10;
	//internal const uint DWM_TNP_RECTDESTINATION = 0x1;
	//internal const uint DWM_TNP_VISIBLE = 0x8;
	//internal const uint DWM_TNP_RECTSOURCE = 0x2;
	
	
	
	
	#endregion
	
	#region uxtheme
	
	[DllImport("uxtheme.dll")]
	static extern IntPtr OpenThemeData(wnd hwnd, string pszClassList);
	
	[DllImport("uxtheme.dll")]
	static extern IntPtr OpenThemeDataForDpi(wnd hwnd, string pszClassList, int dpi);
	
	internal static IntPtr OpenThemeData(wnd hwnd, string classList, int dpi) {
		if (osVersion.minWin10_1703) return OpenThemeDataForDpi(hwnd, classList, dpi);
		return OpenThemeData(hwnd, classList); //never mind: bad on Win8.1 non-primary screen with different DPI than primary if hwnd==0
	}
	
	[DllImport("uxtheme.dll", PreserveSig = true)]
	internal static extern int CloseThemeData(IntPtr hTheme);
	
	[DllImport("uxtheme.dll", PreserveSig = true)]
	internal static extern int GetThemePartSize(IntPtr hTheme, IntPtr hdc, int iPartId, int iStateId, RECT* prc, THEMESIZE eSize, out SIZE psz);
	internal enum THEMESIZE {
		TS_MIN,
		TS_TRUE,
		TS_DRAW
	}
	
	[DllImport("uxtheme.dll", PreserveSig = true)]
	internal static extern int DrawThemeBackground(IntPtr hTheme, IntPtr hdc, int iPartId, int iStateId, in RECT pRect, RECT* pClipRect = null);
	
	//[DllImport("uxtheme.dll", PreserveSig = true)]
	//internal static extern int SetWindowTheme(wnd hwnd, string pszSubAppName, string pszSubIdList);
	
	[DllImport("uxtheme.dll")]
	internal static extern int GetThemeSysColor(IntPtr hTheme, int iColorId);
	
	[DllImport("uxtheme.dll")]
	internal static extern IntPtr GetThemeSysColorBrush(IntPtr hTheme, int iColorId);
	
	[DllImport("uxtheme.dll", PreserveSig = true)]
	internal static extern int BufferedPaintInit();
	
	//[DllImport("uxtheme.dll", PreserveSig = true)]
	//internal static extern int BufferedPaintUnInit();
	
	[DllImport("uxtheme.dll")]
	internal static extern IntPtr BeginBufferedPaint(IntPtr hdcTarget, in RECT prcTarget, BP_BUFFERFORMAT dwFormat, ref BP_PAINTPARAMS pPaintParams, out IntPtr phdc);
	
	[DllImport("uxtheme.dll", PreserveSig = true)]
	internal static extern int EndBufferedPaint(IntPtr hBufferedPaint, bool fUpdateTarget);
	
	internal enum BP_BUFFERFORMAT {
		BPBF_COMPATIBLEBITMAP,
		BPBF_DIB,
		BPBF_TOPDOWNDIB,
		BPBF_TOPDOWNMONODIB
	}
	internal struct BP_PAINTPARAMS {
		public int cbSize;
		public uint dwFlags;
		public RECT* prcExclude;
		//public BLENDFUNCTION* pBlendFunction;
		uint pBlendFunction;
	}
	//internal struct BLENDFUNCTION {
	//	public byte BlendOp;
	//	public byte BlendFlags;
	//	public byte SourceConstantAlpha;
	//	public byte AlphaFormat;
	//}
	
	
	#endregion
	
	#region wtsapi
	
	[DllImport("wtsapi32.dll", SetLastError = true)]
	internal static extern bool WTSTerminateProcess(IntPtr hServer, int ProcessId, int ExitCode);
	
	
	
	#endregion
	
	#region ntdll
	
	internal struct RTL_OSVERSIONINFOW {
		public int dwOSVersionInfoSize;
		public uint dwMajorVersion;
		public uint dwMinorVersion;
		public uint dwBuildNumber;
		public uint dwPlatformId;
		public fixed char szCSDVersion[128];
	}
	
	[DllImport("ntdll.dll", ExactSpelling = true)]
	internal static extern int RtlGetVersion(ref RTL_OSVERSIONINFOW lpVersionInformation);
	
	[DllImport("ntdll.dll")]
	internal static extern uint NtQueryTimerResolution(out uint maxi, out uint mini, out uint current);
	//info: NtSetTimerResolution can set min 0.5 ms resolution. timeBeginPeriod min 1.
	
	[DllImport("ntdll.dll")]
	internal static extern void MD5Init(out Hash.MD5Context context);
	
	[DllImport("ntdll.dll")]
	internal static extern void MD5Update(ref Hash.MD5Context context, void* data, int dataLen);
	
	[DllImport("ntdll.dll")]
	internal static extern void MD5Final(ref Hash.MD5Context context);
	
#pragma warning disable 169
	[DllImport("ntdll.dll")]
	internal static extern int NtQueryInformationProcess(IntPtr ProcessHandle, int ProcessInformationClass, void* ProcessInformation, int ProcessInformationLength, out int ReturnLength);
	
	//all structs are same in 64-bit and 32-bit processes
	
	internal record struct PROCESS_BASIC_INFORMATION {
		long Reserved1;
		public long PebBaseAddress;
		long Reserved2_0, Reserved2_1;
		public long UniqueProcessId;
		public long ParentProcessId;
	}
	
	internal struct RTL_USER_PROCESS_PARAMETERS {
		fixed byte Reserved[96];
		public UNICODE_STRING ImagePathName;
		public UNICODE_STRING CommandLine;
	}
	
	internal struct UNICODE_STRING {
		public ushort Length;
		public ushort MaximumLength;
		public char* Buffer;
	}
	
	internal struct SYSTEM_PROCESS_INFORMATION {
		internal uint NextEntryOffset;
		internal uint NumberOfThreads;
		long SpareLi1;
		long SpareLi2;
		long SpareLi3;
		internal long CreateTime;
		internal long UserTime;
		internal long KernelTime;
		
		internal ushort NameLength;   // UNICODE_STRING   
		internal ushort MaximumNameLength;
		internal IntPtr NamePtr;     // This will point into the data block returned by NtQuerySystemInformation
		
		internal int BasePriority;
		internal IntPtr UniqueProcessId;
		internal IntPtr InheritedFromUniqueProcessId;
		internal uint HandleCount;
		internal uint SessionId;
		
		//unused members
	}
#pragma warning restore 169
	
	internal const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);
	
	[DllImport("ntdll.dll")]
	internal static extern int NtQuerySystemInformation(int five, SYSTEM_PROCESS_INFORMATION* SystemInformation, int SystemInformationLength, out int ReturnLength);
	
	[DllImport("ntdll.dll")]
	internal static extern int NtSuspendProcess(IntPtr handle);
	
	[DllImport("ntdll.dll")]
	internal static extern int NtResumeProcess(IntPtr handle);
	
	
	
	
	#endregion
	
	#region other
	
	[DllImport("msi.dll", EntryPoint = "#217")]
	internal static extern int MsiGetShortcutTarget(string szShortcutPath, char* szProductCode, char* szFeatureId, char* szComponentCode);
	
	[DllImport("msi.dll", EntryPoint = "#173")]
	internal static extern int MsiGetComponentPath(char* szProduct, char* szComponent, char* lpPathBuf, ref int pcchBuf);
	
	[DllImport("powrprof.dll")]
	internal static extern byte SetSuspendState(byte bHibernate, byte bForce, byte bWakeupEventsDisabled);
	
	[DllImport("powrprof.dll")]
	internal static extern byte IsPwrSuspendAllowed();
	
	//[DllImport("urlmon.dll", PreserveSig = true)]
	//internal static extern int FindMimeFromData(IntPtr pBC, string pwzUrl, byte[] pBuffer, int cbSize, string pwzMimeProposed, uint dwMimeFlags, out string ppwzMimeOut, uint dwReserved);
	
	#endregion
	
	#region struct
	
	internal struct NEWHEADER {
		public ushort wReserved;
		public ushort wResType;
		public ushort wResCount;
	};
	
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	internal struct ICONDIRENTRY {
		public byte bWidth;
		public byte bHeight;
		public byte bColorCount;
		public byte bReserved;
		public ushort wPlanes;
		public ushort wBitCount;
		public int dwBytesInRes;
		public int dwImageOffset;
	};
	
	#endregion
}
