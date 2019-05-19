using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
//using System.Linq;
//using System.Xml.Linq;

using Au.Types;
using static Au.AStatic;
using Au.Util;

namespace Au
{
	/// <summary>
	/// Manages an ITEMIDLIST structure that is used to identify files and other shell objects instead of a file-system path.
	/// </summary>
	/// <remarks>
	/// Wraps an ITEMIDLIST*, also known as PIDL or LPITEMIDLIST. In C# it is IntPtr.
	/// When calling native shell API, virtual objects can be identified only by ITEMIDLIST*. Some API also support "parsing name", which may look like <c>"::{CLSID-1}\::{CLSID-2}"</c>. File-system objects can be identified by path as well as by ITEMIDLIST*. URLs can be identified by URL as well as by ITEMIDLIST*.
	/// 
	/// The ITEMIDLIST structure is in unmanaged memory. You should always dispose APidl variables.
	/// 
	/// This class has only ITEMIDLIST functions that are used in this library. Look for other functions in the MSDN library. Many of them are named with IL prefix, like ILClone, ILGetSize, ILFindLastID.
	/// </remarks>
	public unsafe sealed class APidl : IDisposable
	{
		IntPtr _pidl;

		/// <summary>
		/// Gets the ITEMIDLIST* (PIDL).
		/// </summary>
		/// <remarks>
		/// The ITEMIDLIST memory is managed by this variable and will be freed when disposing or GC-collecting it. Use <see cref="GC.KeepAlive"/> where need.
		/// </remarks>
		public IntPtr UnsafePtr => _pidl;

		/// <summary>
		/// Gets the ITEMIDLIST* (PIDL).
		/// </summary>
		/// <remarks>
		/// Use to pass to API where the parameter type is HandlePtr. It is safer than <see cref="UnsafePtr"/> because ensures that this variable will not be GC-collected during API call even if not referenced after the call.
		/// </remarks>
		public HandleRef HandleRef => new HandleRef(this, _pidl);

		/// <summary>
		/// Returns true if the PIDL is default(IntPtr).
		/// </summary>
		public bool IsNull => _pidl == default;

		/// <summary>
		/// Assigns an ITEMIDLIST to this variable.
		/// </summary>
		/// <param name="pidl">
		/// ITEMIDLIST* (PIDL).
		/// It can be created by any API that creates ITEMIDLIST. They allocate the memory with API CoTaskMemAlloc.
		/// This variable will finally free it with Marshal.FreeCoTaskMem.
		/// </param>
		public APidl(IntPtr pidl) => _pidl = pidl;

		/// <summary>
		/// Combines two ITEMIDLIST (parent and child) and assigns the result to this variable.
		/// </summary>
		/// <param name="pidlAbsolute">Absolute PIDL (parent folder).</param>
		/// <param name="pidlRelative">Relative PIDL (child object).</param>
		/// <remarks>
		/// Does not free <i>pidlAbsolute</i> and <i>pidlRelative</i>.
		/// </remarks>
		public APidl(IntPtr pidlAbsolute, IntPtr pidlRelative) => _pidl = Api.ILCombine(pidlAbsolute, pidlRelative);

		/// <summary>
		/// Frees the ITEMIDLIST with Marshal.FreeCoTaskMem and clears this variable.
		/// </summary>
		public void Dispose()
		{
			_Dispose();
			GC.SuppressFinalize(this);
		}

		void _Dispose()
		{
			if(_pidl != default) {
				Marshal.FreeCoTaskMem(_pidl);
				_pidl = default;
			}
		}

		///
		~APidl() { _Dispose(); }

		/// <summary>
		/// Gets the ITEMIDLIST and clears this variable so that it cannot be used and will not free the ITEMIDLIST memory. To free it use Marshal.FreeCoTaskMem.
		/// </summary>
		public IntPtr Detach()
		{
			var R = _pidl;
			_pidl = default;
			return R;
		}

		/// <summary>
		/// Converts string to ITEMIDLIST and creates new APidl variable that holds it.
		/// Returns null if failed.
		/// Note: APidl is disposable.
		/// </summary>
		/// <param name="s">A file-system path or URL or shell object parsing name (see <see cref="ToShellString"/>) or ":: HexEncodedITEMIDLIST" (see <see cref="ToHexString"/>). Supports environment variables (see <see cref="APath.ExpandEnvVar"/>).</param>
		/// <param name="throwIfFailed">If failed, throw AException.</param>
		/// <exception cref="AException">Failed, and throwIfFailed is true. Probably invalid s.</exception>
		/// <remarks>
		/// Calls <msdn>SHParseDisplayName</msdn>, except when string is ":: HexEncodedITEMIDLIST".
		/// Never fails if s is ":: HexEncodedITEMIDLIST", even if it creates an invalid ITEMIDLIST.
		/// </remarks>
		public static APidl FromString(string s, bool throwIfFailed = false)
		{
			IntPtr R = LibFromString(s, throwIfFailed);
			return (R == default) ? null : new APidl(R);
		}

		/// <summary>
		/// The same as <see cref="FromString"/>, but returns unmanaged ITEMIDLIST* (PIDL).
		/// Later need to free it with Marshal.FreeCoTaskMem.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="throwIfFailed"></param>
		internal static IntPtr LibFromString(string s, bool throwIfFailed = false)
		{
			IntPtr R;
			s = _Normalize(s);
			if(s.Starts(":: ")) { //hex-encoded ITEMIDLIST
				int n = (s.Length - 3) / 2;
				R = Marshal.AllocCoTaskMem(n + 2);
				byte* b = (byte*)R;
				n = AConvert.HexDecode(s, b, n, 3);
				b[n] = b[n + 1] = 0;
			} else { //file-system path or URL or shell object parsing name
				var hr = Api.SHParseDisplayName(s, default, out R, 0, null);
				if(hr != 0) {
					if(throwIfFailed) throw new AException(hr);
					return default;
				}
			}
			return R;
		}

		/// <summary>
		/// The same as <see cref="APath.Normalize"/>(CanBeUrlOrShell|DoNotPrefixLongPath), but ignores non-full path (returns s).
		/// </summary>
		/// <param name="s">File-system path or URL or "::...".</param>
		static string _Normalize(string s)
		{
			s = APath.ExpandEnvVar(s);
			if(!APath.IsFullPath(s)) return s; //note: not EEV. Need to expand to ":: " etc, and EEV would not do it.
			return APath.LibNormalize(s, PNFlags.DontPrefixLongPath, true);
		}

		/// <summary>
		/// Converts the ITEMIDLIST to file path or URL or shell object parsing name or display name, depending on stringType argument.
		/// Returns null if this variable does not have an ITEMIDLIST (eg disposed or detached).
		/// If failed, returns null or throws exception.
		/// </summary>
		/// <param name="stringType">
		/// String format. API <msdn>SIGDN</msdn>.
		/// Often used:
		/// - Native.SIGDN.NORMALDISPLAY - returns object name without path. It is best to display in UI but cannot be parsed to create ITEMIDLIST again.
		/// - Native.SIGDN.FILESYSPATH - returns path if the ITEMIDLIST identifies a file system object (file or directory). Else returns null.
		/// - Native.SIGDN.URL - if URL, returns URL. If file system object, returns its path like "file:///C:/a/b.txt". Else returns null.
		/// - Native.SIGDN.DESKTOPABSOLUTEPARSING - returns path (if file system object) or URL (if URL) or shell object parsing name (if virtual object eg Control Panel). Note: not all returned parsing names can actually be parsed to create ITEMIDLIST again, therefore usually it's better to use <see cref="ToString"/> instead.
		/// </param>
		/// <param name="throwIfFailed">If failed, throw AException.</param>
		/// <exception cref="AException">Failed, and throwIfFailed is true.</exception>
		/// <remarks>
		/// Calls <msdn>SHGetNameFromIDList</msdn>.
		/// </remarks>
		public string ToShellString(Native.SIGDN stringType, bool throwIfFailed = false)
		{
			var R = ToShellString(_pidl, stringType, throwIfFailed);
			GC.KeepAlive(this);
			return R;
		}

		/// <summary>
		/// This overload uses an ITEMIDLIST* that is not stored in a APidl variable.
		/// </summary>
		public static string ToShellString(IntPtr pidl, Native.SIGDN stringType, bool throwIfFailed = false)
		{
			if(pidl == default) return null;
			var hr = Api.SHGetNameFromIDList(pidl, stringType, out string R);
			if(hr == 0) return R;
			if(throwIfFailed) throw new AException(hr);
			return null;
		}

		/// <summary>
		/// Converts the ITEMIDLIST to string.
		/// If it identifies an existing file-system object (file or directory), returns path. If URL, returns URL. Else returns ":: HexEncodedITEMIDLIST" (see <see cref="ToHexString"/>).
		/// Returns null if this variable does not have an ITEMIDLIST (eg disposed or detached).
		/// </summary>
		public override string ToString()
		{
			var R = ToString(_pidl);
			GC.KeepAlive(this);
			return R;
		}

#if true
		/// <summary>
		/// This overload uses an ITEMIDLIST* that is not stored in a APidl variable.
		/// </summary>
		public static string ToString(IntPtr pidl)
		{
			if(pidl == default) return null;
			Api.IShellItem si = null;
			try {
				if(0 == Api.SHCreateShellItem(default, null, pidl, out si)) {
					//if(0 == Api.SHCreateItemFromIDList(pidl, Api.IID_IShellItem, out si)) { //same speed
					//if(si.GetAttributes(0xffffffff, out uint attr)>=0) Print(attr);
					if(si.GetAttributes(Api.SFGAO_BROWSABLE | Api.SFGAO_FILESYSTEM, out uint attr) >= 0 && attr != 0) {
						var f = (0 != (attr & Api.SFGAO_FILESYSTEM)) ? Native.SIGDN.FILESYSPATH : Native.SIGDN.URL;
						if(0 == si.GetDisplayName(f, out var R)) return R;
					}
				}
			}
			finally { Api.ReleaseComObject(si); }
			return ToHexString(pidl);
		}
		//this version is 40% slower with non-virtual objects (why?), but with virtual objects same speed as SIGDN_DESKTOPABSOLUTEPARSING.
		//The fastest (update: actually not) version would be to call LibToShellString(SIGDN_DESKTOPABSOLUTEPARSING), and then call LibToHexString if it returns not a path or URL. But it is unreliable, because can return string in any format, eg "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App".
#elif false
			//this version works, but with virtual objects 2 times slower than SIGDN_DESKTOPABSOLUTEPARSING (which already is very slow with virtual).
			public static string ToString(IntPtr pidl)
			{
				if(pidl == default) return null;
				var R = ToShellString(pidl, Native.SIGDN.FILESYSPATH);
				if(R == null) R = ToShellString(pidl, Native.SIGDN.URL);
				if(R == null) R = ToHexString(pidl);
				return R;
			}
#elif true
			//this version works, but with virtual objects 30% slower. Also 30% slower for non-virtual objects (why?).
			public static string ToString(IntPtr pidl)
			{
				if(pidl == default) return null;

				Api.IShellItem si = null;
				try {
					if(0 == Api.SHCreateShellItem(default, null, pidl, out si)) {
						string R = null;
						if(0 == si.GetDisplayName(Native.SIGDN.FILESYSPATH, out R)) return R;
						if(0 == si.GetDisplayName(Native.SIGDN.URL, out R)) return R;
					}
				}
				finally { Api.ReleaseComObject(si); }
				return ToHexString(pidl);
			}
#else
			//SHGetPathFromIDList also slow.
			//SHBindToObject cannot get ishellitem.
#endif

		/// <summary>
		/// Returns string ":: HexEncodedITEMIDLIST".
		/// Returns null if this variable does not have an ITEMIDLIST (eg disposed or detached).
		/// </summary>
		/// <remarks>
		/// The string can be used with some functions of this library, mostly of classes <b>Exec</b>, <b>APidl</b> and <b>AIcon</b>. Cannot be used with native and .NET functions.
		/// </remarks>
		public string ToHexString()
		{
			var R = ToHexString(_pidl);
			GC.KeepAlive(this);
			return R;
		}

		/// <summary>
		/// This overload uses an ITEMIDLIST* that is not stored in a APidl variable.
		/// </summary>
		public static string ToHexString(IntPtr pidl)
		{
			if(pidl == default) return null;
			int n = Api.ILGetSize(pidl) - 2; //API gets size with the terminating '\0' (2 bytes)
			if(n < 0) return null;
			if(n == 0) return ":: "; //shell root - Desktop
			return ":: " + AConvert.HexEncode((void*)pidl, n, true);
		}
	}
}