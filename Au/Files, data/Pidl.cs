namespace Au.Types {
	/// <summary>
	/// Manages an <b>ITEMIDLIST</b> structure that is used to identify files and other shell objects instead of a file-system path.
	/// </summary>
	/// <remarks>
	/// Wraps an <b>ITEMIDLIST*</b>, also known as <i>PIDL</i> or <b>LPITEMIDLIST</b>.
	/// 
	/// When calling native shell API, virtual objects can be identified only by <b>ITEMIDLIST*</b>. Some API also support "parsing name", which may look like <c>"::{CLSID-1}\::{CLSID-2}"</c>. File-system objects can be identified by path as well as by <b>ITEMIDLIST*</b>. URLs can be identified by URL as well as by <b>ITEMIDLIST*</b>.
	/// 
	/// The <b>ITEMIDLIST</b> structure is in unmanaged memory. You can dispose <b>Pidl</b> variables, or GC will do it later. Always dispose if creating many.
	/// 
	/// This class has only <b>ITEMIDLIST</b> functions that are used in this library. Look for other functions in the MSDN library. Many of them are named with IL prefix, like <b>ILClone</b>, <b>ILGetSize</b>, <b>ILFindLastID</b>.
	/// </remarks>
	public unsafe class Pidl : IDisposable {
		IntPtr _pidl;

		/// <summary>
		/// Gets the <b>ITEMIDLIST*</b>.
		/// </summary>
		/// <remarks>
		/// The <b>ITEMIDLIST</b> memory is managed by this variable and will be freed when disposing or GC-collecting it. Use <see cref="GC.KeepAlive"/> where need.
		/// </remarks>
		public IntPtr UnsafePtr => _pidl;

		/// <summary>
		/// Gets the <b>ITEMIDLIST*</b>.
		/// </summary>
		/// <remarks>
		/// Use to pass to API where the parameter type is <b>HandleRef</b>. It is safer than <see cref="UnsafePtr"/> because ensures that this variable will not be GC-collected during API call even if not referenced after the call.
		/// </remarks>
		public HandleRef HandleRef => new HandleRef(this, _pidl);

		/// <summary>
		/// Returns <c>true</c> if the <b>ITEMIDLIST*</b> is <c>null</c>.
		/// </summary>
		public bool IsNull => _pidl == default;

		/// <summary>
		/// Assigns an <b>ITEMIDLIST</b> to this variable.
		/// </summary>
		/// <param name="pidl">
		/// <b>ITEMIDLIST*</b>.
		/// It can be created by any API that creates <b>ITEMIDLIST</b>. They allocate the memory with API <b>CoTaskMemAlloc</b>. This variable will finally free it with <b>Marshal.FreeCoTaskMem</b> which calls API <b>CoTaskMemFree</b>.
		/// </param>
		public Pidl(IntPtr pidl) => _pidl = pidl;

		/// <summary>
		/// Combines two <b>ITEMIDLIST</b> (parent and child) and assigns the result to this variable.
		/// </summary>
		/// <param name="pidlAbsolute">Absolute <b>ITEMIDLIST*</b> (parent folder).</param>
		/// <param name="pidlRelative">Relative <b>ITEMIDLIST*</b> (child object).</param>
		/// <remarks>
		/// Does not free <i>pidlAbsolute</i> and <i>pidlRelative</i>.
		/// </remarks>
		public Pidl(IntPtr pidlAbsolute, IntPtr pidlRelative) => _pidl = Api.ILCombine(pidlAbsolute, pidlRelative);

		/// <summary>
		/// Frees the <b>ITEMIDLIST</b> with <b>Marshal.FreeCoTaskMem</b> and clears this variable.
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		///
		protected virtual void Dispose(bool disposing) {
			if (_pidl != default) {
				Marshal.FreeCoTaskMem(_pidl);
				_pidl = default;
			}
		}

		///
		~Pidl() { Dispose(false); }

		/// <summary>
		/// Gets the <b>ITEMIDLIST</b> and clears this variable so that it cannot be used and will not free the <b>ITEMIDLIST</b> memory. To free it use <see cref="Marshal.FreeCoTaskMem"/>.
		/// </summary>
		public IntPtr Detach() {
			var R = _pidl;
			_pidl = default;
			return R;
		}

		/// <summary>
		/// Converts string to <b>ITEMIDLIST</b> and creates new <b>Pidl</b> variable that holds it.
		/// </summary>
		/// <returns><c>null</c> if failed.</returns>
		/// <param name="s">A file-system path or URL or shell object parsing name (see <see cref="ToShellString"/>) or <c>":: ITEMIDLIST"</c> (see <see cref="ToHexString"/>). Supports environment variables (see <see cref="pathname.expand"/>).</param>
		/// <param name="throwIfFailed">Throw exception if failed.</param>
		/// <exception cref="AuException">Failed, and <i>throwIfFailed</i> is <c>true</c>. Probably invalid s.</exception>
		/// <remarks>
		/// Calls <msdn>SHParseDisplayName</msdn>, except when string is <c>":: ITEMIDLIST"</c>.
		/// If <c>":: ITEMIDLIST"</c>, does not check whether the shell object exists.
		/// </remarks>
		public static Pidl FromString(string s, bool throwIfFailed = false) {
			IntPtr R = FromString_(s, throwIfFailed);
			return (R == default) ? null : new Pidl(R);
		}

		/// <summary>
		/// The same as <see cref="FromString"/>, but returns unmanaged <b>ITEMIDLIST*</b>.
		/// Later need to free it with Marshal.FreeCoTaskMem.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="throwIfFailed">If failed: <c>true</c> - throw AuException; <c>false</c> - return 0.</param>
		internal static IntPtr FromString_(string s, bool throwIfFailed = false) {
			IntPtr R;
			s = _Normalize(s);
			if (s.Starts(":: ")) {
				var span = s.AsSpan(3);
				int n = span.Length / 2;
				R = Marshal.AllocCoTaskMem(n + 2);
				byte* b = (byte*)R;
				n = Convert2.HexDecode(span, b, n);
				b[n] = b[n + 1] = 0;
			} else { //file-system path or URL or shell object parsing name
				var hr = Api.SHParseDisplayName(s, default, out R, 0, null);
				if (hr != 0) {
					if (throwIfFailed) throw new AuException(hr);
					return default;
				}
			}
			return R;
		}

		/// <summary>
		/// The same as <see cref="pathname.normalize"/>(CanBeUrlOrShell|DontPrefixLongPath), but ignores non-full path (returns s).
		/// </summary>
		/// <param name="s">File-system path or URL or "::...".</param>
		static string _Normalize(string s) {
			s = pathname.expand(s);
			if (!pathname.isFullPath(s)) return s; //note: not EEV. Need to expand to ":: " etc, and EEV would not do it.
			return pathname.Normalize_(s, PNFlags.DontPrefixLongPath, true);
		}

		/// <summary>
		/// Converts the <b>ITEMIDLIST</b> to file path or URL or shell object parsing name or display name, depending on <i>stringType</i>.
		/// </summary>
		/// <returns>Returns <c>null</c> if this variable does not have an <b>ITEMIDLIST</b> (eg disposed or detached). If failed, returns <c>null</c> or throws exception.</returns>
		/// <param name="stringType">
		/// String format. API <msdn>SIGDN</msdn>.
		/// Often used:
		/// <br/>• <b>SIGDN.NORMALDISPLAY</b> - returns object name without path. It is best to display in UI but cannot be parsed to create <b>ITEMIDLIST</b> again.
		/// <br/>• <b>SIGDN.FILESYSPATH</b> - returns path if the <b>ITEMIDLIST</b> identifies a file system object (file or directory). Else returns <c>null</c>.
		/// <br/>• <b>SIGDN.URL</b> - if URL, returns URL. If file system object, returns its path like <c>"file:///C:/a/b.txt"</c>. Else returns <c>null</c>.
		/// <br/>• <b>SIGDN.DESKTOPABSOLUTEPARSING</b> - returns path (if file system object) or URL (if URL) or shell object parsing name (if virtual object eg Control Panel). Note: not all returned parsing names can actually be parsed to create <b>ITEMIDLIST</b> again, therefore usually it's better to use <see cref="ToString"/> instead.
		/// </param>
		/// <param name="throwIfFailed">If failed, throw <b>AuException</b>.</param>
		/// <exception cref="AuException">Failed, and <i>throwIfFailed</i> is <c>true</c>.</exception>
		/// <remarks>
		/// Calls <msdn>SHGetNameFromIDList</msdn>.
		/// </remarks>
		public string ToShellString(SIGDN stringType, bool throwIfFailed = false) {
			var R = ToShellString(_pidl, stringType, throwIfFailed);
			GC.KeepAlive(this);
			return R;
		}

		/// <summary>
		/// Converts an <b>ITEMIDLIST</b> to file path or URL or shell object parsing name or display name, depending on <i>stringType</i>.
		/// </summary>
		/// <returns>Returns <c>null</c> if <i>pidl</i> is <c>default(IntPtr)</c>. If failed, returns <c>null</c> or throws exception.</returns>
		/// <inheritdoc cref="ToShellString(SIGDN, bool)"/>
		public static string ToShellString(IntPtr pidl, SIGDN stringType, bool throwIfFailed = false) {
			if (pidl == default) return null;
			var hr = Api.SHGetNameFromIDList(pidl, stringType, out string R);
			if (hr == 0) return R;
			if (throwIfFailed) throw new AuException(hr);
			return null;
		}

		/// <summary>
		/// Converts the <b>ITEMIDLIST</b> to string.
		/// If it identifies an existing file-system object (file or directory), returns path. If URL, returns URL. Else returns <c>":: ITEMIDLIST"</c> (see <see cref="ToHexString"/>).
		/// </summary>
		/// <returns><c>null</c> if this variable does not have an <b>ITEMIDLIST</b> (eg disposed or detached).</returns>
		public override string ToString() {
			var R = ToString(_pidl);
			GC.KeepAlive(this);
			return R;
		}

#if true
		/// <summary>
		/// This overload uses an <b>ITEMIDLIST*</b> that is not stored in a <b>Pidl</b> variable.
		/// </summary>
		public static string ToString(IntPtr pidl) {
			if (pidl == default) return null;
			Api.IShellItem si = null;
			try {
				if (0 == Api.SHCreateShellItem(default, null, pidl, out si)) {
					//if(0 == Api.SHCreateItemFromIDList(pidl, Api.IID_IShellItem, out si)) { //same speed
					//if(si.GetAttributes(0xffffffff, out uint attr)>=0) print.it(attr);
					if (si.GetAttributes(Api.SFGAO_BROWSABLE | Api.SFGAO_FILESYSTEM, out uint attr) >= 0 && attr != 0) {
						var f = (0 != (attr & Api.SFGAO_FILESYSTEM)) ? SIGDN.FILESYSPATH : SIGDN.URL;
						if (0 == si.GetDisplayName(f, out var R)) return R;
					}
				}
			}
			finally { Api.ReleaseComObject(si); }
			return ToHexString(pidl);
		}
		//this version is 40% slower with non-virtual objects (why?), but with virtual objects same speed as SIGDN_DESKTOPABSOLUTEPARSING.
		//The fastest (update: actually not) version would be to call ToShellString_(SIGDN_DESKTOPABSOLUTEPARSING), and then call ToHexString if it returns not a path or URL. But it is unreliable, because can return string in any format, eg "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App".
#elif false
			//this version works, but with virtual objects 2 times slower than SIGDN_DESKTOPABSOLUTEPARSING (which already is very slow with virtual).
			public static string ToString(IntPtr pidl)
			{
				if(pidl == default) return null;
				var R = ToShellString(pidl, SIGDN.FILESYSPATH);
				if(R == null) R = ToShellString(pidl, SIGDN.URL);
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
						if(0 == si.GetDisplayName(SIGDN.FILESYSPATH, out R)) return R;
						if(0 == si.GetDisplayName(SIGDN.URL, out R)) return R;
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
		/// Returns string <c>":: ITEMIDLIST"</c>.
		/// Returns <c>null</c> if this variable does not have an <b>ITEMIDLIST</b> (eg disposed or detached).
		/// </summary>
		/// <remarks>
		/// The string can be used with some functions of this library, mostly of classes <b>filesystem</b>, <b>Pidl</b> and <b>icon</b>. Cannot be used with native and .NET functions.
		/// </remarks>
		public string ToHexString() {
			var R = ToHexString(_pidl);
			GC.KeepAlive(this);
			return R;
		}

		/// <summary>
		/// Returns string <c>":: ITEMIDLIST"</c>.
		/// This overload uses an <b>ITEMIDLIST*</b> that is not stored in a <b>Pidl</b> variable.
		/// </summary>
		public static string ToHexString(IntPtr pidl) {
			if (pidl == default) return null;
			int n = Api.ILGetSize(pidl) - 2; //API gets size with the terminating '\0' (2 bytes)
			if (n < 0) return null;
			if (n == 0) return ":: "; //shell root - Desktop
			return ":: " + Convert2.HexEncode((void*)pidl, n);
		}
		//rejected: use base64 <b>ITEMIDLIST</b>. Shorter, but cannot easily split, for example in folders.UnexpandPath.

		/// <summary>
		/// If s starts with "::{", converts to <c>":: ITEMIDLIST"</c>. Else returns s.
		/// </summary>
		internal static string ClsidToItemidlist_(string s) {
			if (s != null && s.Starts("::{")) {
				using var pidl = FromString(s);
				if (pidl != null) return pidl.ToString();
			}
			return s;
		}

		/// <summary>
		/// Returns <c>true</c> if <b>ITEMIDLIST</b> values are equal.
		/// </summary>
		public bool ValueEquals(IntPtr pidl) => Api.ILIsEqual(_pidl, pidl);
	}
}
