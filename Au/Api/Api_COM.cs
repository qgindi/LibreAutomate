﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Au.Types
{
	static unsafe partial class Api
	{
		internal struct STRRET
		{
			public uint uType;

			[StructLayout(LayoutKind.Explicit)]
			public struct TYPE_1
			{
				[FieldOffset(0)]
				public IntPtr pOleStr;
				[FieldOffset(0)]
				public uint uOffset;
				[FieldOffset(0)]
				public fixed sbyte cStr[260];
			}
			public TYPE_1 _2;
		}

		internal static Guid IID_IShellFolder = new Guid(0x000214E6, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

		[ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IShellFolder
		{
			[PreserveSig] int ParseDisplayName(Wnd hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, uint* pchEaten, out IntPtr ppidl, uint* pdwAttributes);
			[PreserveSig] int EnumObjects(Wnd hwnd, uint grfFlags, out IEnumIDList ppenumIDList);
			[PreserveSig] int BindToObject(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);
			[PreserveSig] int BindToStorage(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);
			[PreserveSig] int CompareIDs(LPARAM lParam, IntPtr pidl1, IntPtr pidl2);
			[PreserveSig] int CreateViewObject(Wnd hwndOwner, in Guid riid, out IntPtr ppv);
			[PreserveSig] int GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
			[PreserveSig] //int GetUIObjectOf(Wnd hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, in Guid riid, IntPtr rgfReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
			int GetUIObjectOf(Wnd hwndOwner, uint cidl, IntPtr* apidl, in Guid riid, IntPtr rgfReserved, [MarshalAs(UnmanagedType.Interface)] out object ppv);
			[PreserveSig] int GetDisplayNameOf(IntPtr pidl, uint uFlags, out STRRET pName);
			[PreserveSig] int SetNameOf(Wnd hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
		}

		internal static Guid IID_IShellItem = new Guid(0x43826D1E, 0xE718, 0x42EE, 0xBC, 0x55, 0xA1, 0xE2, 0x61, 0xC3, 0x7B, 0xFE);

		[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IShellItem
		{
			[PreserveSig] int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv); //IBindCtx
			[PreserveSig] int GetParent(out IShellItem ppsi);
			[PreserveSig] int GetDisplayName(Native.SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
			[PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
			[PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
		}

		[ComImport, Guid("000214F2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IEnumIDList
		{
			[PreserveSig] int Next(int celt, [MarshalAs(UnmanagedType.LPArray)] [Out] IntPtr[] rgelt, out int pceltFetched);
			[PreserveSig] int Skip(int celt);
			[PreserveSig] int Reset();
			[PreserveSig] int Clone(out IEnumIDList ppenum);
		}

		//internal const uint GIL_OPENICON = 0x1;
		//internal const uint GIL_FORSHELL = 0x2;
		//internal const uint GIL_ASYNC = 0x20;
		//internal const uint GIL_DEFAULTICON = 0x40;
		//internal const uint GIL_FORSHORTCUT = 0x80;
		//internal const uint GIL_CHECKSHIELD = 0x200;
		//internal const uint GIL_SIMULATEDOC = 0x1;
		//internal const uint GIL_PERINSTANCE = 0x2;
		//internal const uint GIL_PERCLASS = 0x4;
		//internal const uint GIL_NOTFILENAME = 0x8;
		//internal const uint GIL_DONTCACHE = 0x10;
		//internal const uint GIL_SHIELD = 0x200;
		//internal const uint GIL_FORCENOSHIELD = 0x400;

		//internal static Guid IID_IExtractIcon = new Guid(0x000214FA, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);

		//[ComImport, Guid("000214fa-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		//internal interface IExtractIcon
		//{
		//	[PreserveSig] int GetIconLocation(uint uFlags, [MarshalAs(UnmanagedType.LPArray)] [Out] char[] pszIconFile, int cchMax, out int piIndex, out uint pwFlags);
		//	[PreserveSig] int Extract([MarshalAs(UnmanagedType.LPWStr)] string pszFile, int nIconIndex, IntPtr* phiconLarge, IntPtr* phiconSmall, int nIconSize);
		//}

		[ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IShellLink
		{
			[PreserveSig] int GetPath([MarshalAs(UnmanagedType.LPArray)] [Out] char[] pszFile, int cch, IntPtr pfd = default, uint fFlags = 0);
			[PreserveSig] int GetIDList(out IntPtr ppidl);
			[PreserveSig] int SetIDList(IntPtr pidl);
			[PreserveSig] int GetDescription([MarshalAs(UnmanagedType.LPArray)] [Out] char[] pszName, int cch);
			[PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
			[PreserveSig] int GetWorkingDirectory([MarshalAs(UnmanagedType.LPArray)] [Out] char[] pszDir, int cch);
			[PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
			[PreserveSig] int GetArguments([MarshalAs(UnmanagedType.LPArray)] [Out] char[] pszArgs, int cch);
			[PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
			[PreserveSig] int GetHotkey(out ushort pwHotkey);
			[PreserveSig] int SetHotkey(ushort wHotkey);
			[PreserveSig] int GetShowCmd(out int piShowCmd);
			[PreserveSig] int SetShowCmd(int iShowCmd);
			[PreserveSig] int GetIconLocation([MarshalAs(UnmanagedType.LPArray)] [Out] char[] pszIconPath, int cch, out int piIcon);
			[PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
			[PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved = 0);
			[PreserveSig] int Resolve(Wnd hwnd, uint fFlags);
			[PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);

			//info: default string marshaling in COM interfaces is BSTR, but in this interface strings are LPWSTR. Cannot use plain string and char[].
			//	Instead of [MarshalAs(UnmanagedType.LPArray)] [Out] char[] can be just char*. Then also need fixed when calling.
		}

		[ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
		internal class ShellLink { }

		[ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IPersistFile
		{
			// IPersist
			[PreserveSig] int GetClassID(out Guid pClassID);
			// IPersistFile
			[PreserveSig] int IsDirty();
			[PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
			[PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
			[PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
			[PreserveSig] int GetCurFile(out IntPtr ppszFileName);
		}

		//see also VARIANT in Struct.cs
		internal struct PROPVARIANT :IDisposable
		{
			public VARENUM vt; //ushort
			public ushort _u1;
			public uint _u2;
			public LPARAM value;
			public LPARAM value2;

			/// <summary>
			/// Calls PropVariantClear.
			/// </summary>
			public void Dispose()
			{
				PropVariantClear(ref this);
			}
		}

		internal struct PROPERTYKEY
		{
			public Guid fmtid;
			public uint pid;
		}

		internal static Guid IID_IPropertyStore = new Guid(0x886D8EEB, 0x8CF2, 0x4446, 0x8D, 0x02, 0xCD, 0xBA, 0x1D, 0xBD, 0xCF, 0x99);

		[ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IPropertyStore
		{
			[PreserveSig] int GetCount(out int cProps);
			[PreserveSig] int GetAt(int iProp, out PROPERTYKEY pkey);
			[PreserveSig] int GetValue(in PROPERTYKEY key, out PROPVARIANT pv);
			[PreserveSig] int SetValue(in PROPERTYKEY key, ref PROPVARIANT propvar);
			[PreserveSig] int Commit();
		}

		//internal struct IMAGEINFO
		//{
		//	public IntPtr hbmImage;
		//	public IntPtr hbmMask;
		//	public int Unused1;
		//	public int Unused2;
		//	public RECT rcImage;
		//}

		//note: this is used in the lib, even if IImageList isn't.
		internal static Guid IID_IImageList = new Guid(0x46EB5926, 0x582E, 0x4017, 0x9F, 0xDF, 0xE8, 0x99, 0x8D, 0xAA, 0x09, 0x50);

		//[ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		//internal interface IImageList
		//{
		//	[PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
		//	[PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
		//	[PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
		//	[PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
		//	[PreserveSig] int AddMasked(IntPtr hbmImage, uint crMask, out int pi);
		//	[PreserveSig] int Draw(IntPtr pimldp); //ref IMAGELISTDRAWPARAMS
		//	[PreserveSig] int Remove(int i);
		//	[PreserveSig] int GetIcon(int i, uint flags, out IntPtr picon);
		//	[PreserveSig] int GetImageInfo(int i, out IMAGEINFO pImageInfo);
		//	[PreserveSig] int Copy(int iDst, [MarshalAs(UnmanagedType.IUnknown)] Object punkSrc, int iSrc, uint uFlags);
		//	[PreserveSig] int Merge(int i1, [MarshalAs(UnmanagedType.IUnknown)] Object punk2, int i2, int dx, int dy, in Guid riid, out IntPtr ppv);
		//	[PreserveSig] int Clone(in Guid riid, out IntPtr ppv);
		//	[PreserveSig] int GetImageRect(int i, out RECT prc);
		//	[PreserveSig] int GetIconSize(out int cx, out int cy);
		//	[PreserveSig] int SetIconSize(int cx, int cy);
		//	[PreserveSig] int GetImageCount(out int pi);
		//	[PreserveSig] int SetImageCount(int uNewCount);
		//	[PreserveSig] int SetBkColor(uint clrBk, out uint pclr);
		//	[PreserveSig] int GetBkColor(out uint pclr);
		//	[PreserveSig] int BeginDrag(int iTrack, int dxHotspot, int dyHotspot);
		//	[PreserveSig] int EndDrag();
		//	[PreserveSig] int DragEnter(Wnd hwndLock, int x, int y);
		//	[PreserveSig] int DragLeave(Wnd hwndLock);
		//	[PreserveSig] int DragMove(int x, int y);
		//	[PreserveSig] int SetDragCursorImage([MarshalAs(UnmanagedType.IUnknown)] Object punk, int iDrag, int dxHotspot, int dyHotspot);
		//	[PreserveSig] int DragShowNolock([MarshalAs(UnmanagedType.Bool)] bool fShow);
		//	[PreserveSig] int GetDragImage(out POINT ppt, out Point pptHotspot, in Guid riid, out IntPtr ppv);
		//	[PreserveSig] int GetItemFlags(int i, out uint dwFlags);
		//	[PreserveSig] int GetOverlayImage(int iOverlay, out int piIndex);
		//}

		//[ComImport, Guid("00020400-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
		//internal interface IDispatch
		//{
		//}
	}
}