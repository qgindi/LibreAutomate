namespace Au.Types;

static unsafe partial class Api {
	internal struct STRRET {
		public uint uType;

		[StructLayout(LayoutKind.Explicit)]
		internal struct TYPE_1 {
			[FieldOffset(0)]
			public char* pOleStr;
			[FieldOffset(0)]
			public uint uOffset;
			[FieldOffset(0)]
			public fixed sbyte cStr[260];
		}
		public TYPE_1 _2;
	}

	//internal static Guid IID_IShellFolder = new Guid(0x000214E6, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

	[ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellFolder {
		//[PreserveSig] int ParseDisplayName(wnd hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, uint* pchEaten, out IntPtr ppidl, uint* pdwAttributes);
		//[PreserveSig] int EnumObjects(wnd hwnd, uint grfFlags, out IEnumIDList ppenumIDList);
		//[PreserveSig] int BindToObject(IntPtr pidl, IntPtr pbc, in Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
		//[PreserveSig] int BindToStorage(IntPtr pidl, IntPtr pbc, in Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
		//[PreserveSig] int CompareIDs(nint lParam, IntPtr pidl1, IntPtr pidl2);
		//[PreserveSig] int CreateViewObject(wnd hwndOwner, in Guid riid, out IntPtr ppv);
		//[PreserveSig] int GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
		void _0();
		void _1();
		void _2();
		void _3();
		void _4();
		void _5();
		void _6();
		[PreserveSig] int GetUIObjectOf(wnd hwndOwner, uint cidl, in IntPtr pidl, in Guid riid, nint rgfReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
		//[PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, out STRRET pName);
		//[PreserveSig] int SetNameOf(wnd hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
	}

	internal static bool GetUIObjectOf<T>(this IShellFolder t, IntPtr pidl, out T result) where T : class {
		result = null;
		if (0 != t.GetUIObjectOf(default, 1, pidl, typeof(T).GUID, 0, out var o) || o is not T r) return false;
		result = r;
		return true;
	}

	//internal static Guid IID_IShellItem = new Guid(0x43826D1E, 0xE718, 0x42EE, 0xBC, 0x55, 0xA1, 0xE2, 0x61, 0xC3, 0x7B, 0xFE);

	[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellItem {
		//[PreserveSig] int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv); //IBindCtx
		//[PreserveSig] int GetParent(out IShellItem ppsi);
		void _0();
		void _1();
		[PreserveSig] int GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
		[PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
		//[PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
	}

	//[ComImport, Guid("000214F2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	//internal interface IEnumIDList {
	//	[PreserveSig] int Next(int celt, [MarshalAs(UnmanagedType.LPArray)][Out] IntPtr[] rgelt, out int pceltFetched);
	//	[PreserveSig] int Skip(int celt);
	//	[PreserveSig] int Reset();
	//	[PreserveSig] int Clone(out IEnumIDList ppenum);
	//}

	[ComImport, Guid("000214fa-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IExtractIcon {
		[PreserveSig] int GetIconLocation(uint uFlags, StringBuilder pszIconFile, int cchMax, out int piIndex, out uint pwFlags);
		//[PreserveSig] int Extract([MarshalAs(UnmanagedType.LPWStr)] string pszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);
	}

	[ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IShellLink {
		[PreserveSig] int GetPath(char* pszFile, int cch, IntPtr pfd = default, uint fFlags = 0);
		[PreserveSig] int GetIDList(out IntPtr ppidl);
		[PreserveSig] int SetIDList(IntPtr pidl);
		[PreserveSig] int GetDescription(char* pszName, int cch);
		[PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
		[PreserveSig] int GetWorkingDirectory(char* pszDir, int cch);
		[PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
		[PreserveSig] int GetArguments(char* pszArgs, int cch);
		[PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
		[PreserveSig] int GetHotkey(out ushort pwHotkey);
		[PreserveSig] int SetHotkey(ushort wHotkey);
		[PreserveSig] int GetShowCmd(out int piShowCmd);
		[PreserveSig] int SetShowCmd(int iShowCmd);
		[PreserveSig] int GetIconLocation(char* pszIconPath, int cch, out int piIcon);
		[PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
		[PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved = 0);
		[PreserveSig] int Resolve(wnd hwnd, uint fFlags);
		[PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);

		//info: default string marshaling in COM interfaces is BSTR, but in this interface strings are LPWSTR. Cannot use plain string and char[].
		//	Instead of [MarshalAs(UnmanagedType.LPArray)] [Out] char[] can be just char*. Then also need fixed when calling.
	}

	[ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
	internal class ShellLink { }

	[ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IPersistFile {
		// IPersist
		[PreserveSig] int GetClassID(out Guid pClassID);
		// IPersistFile
		[PreserveSig] int IsDirty();
		[PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
		[PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
		//[PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
		//[PreserveSig] int GetCurFile(out IntPtr ppszFileName);
	}

	//see also VARIANT in Struct.cs
	internal struct PROPVARIANT : IDisposable {
		public VARENUM vt; //ushort
		ushort _u1;
		uint _u2;
		public nint value;
		public nint value2;

		/// <summary>
		/// Calls <b>PropVariantClear</b>.
		/// </summary>
		public void Dispose() {
			PropVariantClear(ref this);
		}
	}

	internal struct PROPERTYKEY {
		public Guid fmtid;
		public uint pid;
	}

	internal static Guid IID_IPropertyStore = new Guid(0x886D8EEB, 0x8CF2, 0x4446, 0x8D, 0x02, 0xCD, 0xBA, 0x1D, 0xBD, 0xCF, 0x99);

	[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IPropertyStore {
		[PreserveSig] int GetCount(out int cProps);
		[PreserveSig] int GetAt(int iProp, out PROPERTYKEY pkey);
		[PreserveSig] int GetValue(in PROPERTYKEY key, out PROPVARIANT pv);
		[PreserveSig] int SetValue(in PROPERTYKEY key, ref PROPVARIANT propvar);
		[PreserveSig] int Commit();
	}

	//note: this is used in the lib, even if IImageList isn't.
	internal static Guid IID_IImageList = new Guid(0x46EB5926, 0x582E, 0x4017, 0x9F, 0xDF, 0xE8, 0x99, 0x8D, 0xAA, 0x09, 0x50);

	[ComImport, Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IVirtualDesktopManager {
		[PreserveSig] int IsWindowOnCurrentVirtualDesktop(wnd topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);
		[PreserveSig] int GetWindowDesktopId(wnd topLevelWindow, out Guid desktopId);
		[PreserveSig] int MoveWindowToDesktop(wnd topLevelWindow, in Guid desktopId);
	}

	[ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"), ClassInterface(ClassInterfaceType.None)]
	internal class VirtualDesktopManager { }
}
