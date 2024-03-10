namespace Au {
	/// <summary>
	/// Creates shell shortcuts (<c>.lnk</c> files) and gets shortcut properties.
	/// </summary>
	public unsafe sealed class shortcutFile : IDisposable {
		Api.IShellLink _isl;
		Api.IPersistFile _ipf;
		string _lnkPath;
		bool _isOpen;
		bool _changedHotkey;

		/// <summary>
		/// Releases internally used COM objects (<b>IShellLink</b>, <b>IPersistFile</b>).
		/// </summary>
		public void Dispose() {
			if (_isl != null) {
				Api.ReleaseComObject(_ipf); _ipf = null;
				Api.ReleaseComObject(_isl); _isl = null;
			}
		}

		/// <summary>
		/// Returns the internally used <b>IShellLink</b> COM interface.
		/// </summary>
		internal Api.IShellLink IShellLink => _isl;
		//This could be public, but then need to make IShellLink public. It is defined in a non-standard way. Never mind, it is not important.

		shortcutFile(string lnkPath, uint mode) {
			_isl = new Api.ShellLink() as Api.IShellLink;
			_ipf = _isl as Api.IPersistFile;
			_lnkPath = pathname.normalize(lnkPath);
			if (mode != Api.STGM_WRITE && (mode == Api.STGM_READ || filesystem.exists(_lnkPath).File)) {
				AuException.ThrowIfHresultNot0(_ipf.Load(_lnkPath, mode), "*open");
				_isOpen = true;
			}
		}

		/// <summary>
		/// Opens a shortcut file (<c>.lnk</c>) for getting shortcut properties.
		/// </summary>
		/// <param name="lnkPath">Shortcut file (<c>.lnk</c>) path.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="AuException">Failed to open <c>.lnk</c> file.</exception>
		public static shortcutFile open(string lnkPath) {
			return new shortcutFile(lnkPath, Api.STGM_READ);
		}

		/// <summary>
		/// Creates a new <see cref="shortcutFile"/> instance that can be used to create or replace a shortcut file.
		/// </summary>
		/// <param name="lnkPath">Shortcut file (<c>.lnk</c>) path.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <remarks>
		/// You can set properties and finally call <see cref="Save"/>.
		/// If the shortcut file already exists, <b>Save</b> replaces it.
		/// </remarks>
		public static shortcutFile create(string lnkPath) {
			return new shortcutFile(lnkPath, Api.STGM_WRITE);
		}

		/// <summary>
		/// Creates a new <see cref="shortcutFile"/> instance that can be used to create or modify a shortcut file.
		/// </summary>
		/// <param name="lnkPath">Shortcut file (<c>.lnk</c>) path.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="AuException">Failed to open existing <c>.lnk</c> file.</exception>
		/// <remarks>
		/// Exception if file exists but cannot open it for read-write access.
		/// You can get and set properties and finally call <see cref="Save"/>.
		/// If the shortcut file already exists, <b>Save</b> updates it.
		/// </remarks>
		public static shortcutFile openOrCreate(string lnkPath) {
			return new shortcutFile(lnkPath, Api.STGM_READWRITE);
		}

		/// <summary>
		/// Saves to the shortcut file (<c>.lnk</c>).
		/// </summary>
		/// <exception cref="AuException">Failed to save.</exception>
		/// <remarks>
		/// Creates parent folder if need.
		/// </remarks>
		public void Save() {
			if (_changedHotkey && !_isOpen && filesystem.exists(_lnkPath).File) _UnregisterHotkey(_lnkPath);

			filesystem.createDirectoryFor(_lnkPath);
			AuException.ThrowIfHresultNot0(_ipf.Save(_lnkPath, true), "*save");
		}

		/// <summary>
		/// Gets or sets shortcut target path.
		/// </summary>
		/// <value><c>null</c> if target isn't a file system object, eg Control Panel or URL.</value>
		/// <remarks>The <c>get</c> function gets path with expanded environment variables. If possible, it corrects the target of MSI shortcuts and 64-bit Program Files shortcuts where <b>IShellLink.GetPath</b> lies.</remarks>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		/// <exception cref="ArgumentException">The <c>set</c> function allows max length 259.</exception>
		public string TargetPath {
			get => _CorrectPath(_GetString(_WhatString.Path), true);
			set { AuException.ThrowIfHresultNot0(_isl.SetPath(_Max259(value))); }
		}

		/// <summary>
		/// Gets shortcut target path and does not correct wrong MSI shortcut target.
		/// </summary>
		public string TargetPathRawMSI {
			get => _CorrectPath(_GetString(_WhatString.Path));
		}

		/// <summary>
		/// Gets or sets a non-file-system target (eg Control Panel) through its <b>ITEMIDLIST</b>.
		/// </summary>
		/// <remarks>
		/// Also can be used for any target type, but gets raw value, for example MSI shortcut target is incorrect.
		/// Most but not all shortcuts have this property; the <c>get</c> function returns <c>null</c> if the shortcut does not have it.
		/// </remarks>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		public Pidl TargetPidl {
			get => (0 == _isl.GetIDList(out var pidl)) ? new Pidl(pidl) : null;
			set { AuException.ThrowIfHresultNot0(_isl.SetIDList(value?.UnsafePtr ?? default)); GC.KeepAlive(value); }
		}

		/// <summary>
		/// Gets or sets a URL target.
		/// Note: it is a <c>.lnk</c> shortcut, not a <c>.url</c> shortcut.
		/// </summary>
		/// <value>The <c>get</c> function returns string <c>"file:///..."</c> if target is a file.</value>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		public string TargetURL {
			get {
				if (0 != _isl.GetIDList(out var pidl)) return null;
				try { return Pidl.ToShellString(pidl, SIGDN.URL); }
				finally { Marshal.FreeCoTaskMem(pidl); }
			}
			set {
				TargetAnyType = value;
			}
		}

		/// <summary>
		/// Gets or sets target of any type - file/folder, URL, virtual shell object (see <see cref="Pidl"/>).
		/// The string can be used with <see cref="run.it"/>.
		/// </summary>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		public string TargetAnyType {
			get {
				var R = TargetPath; if (R != null) return R; //support MSI etc
				if (0 != _isl.GetIDList(out var pidl)) return null;
				try { return Pidl.ToString(pidl); } finally { Marshal.FreeCoTaskMem(pidl); }
			}
			set {
				var pidl = Pidl.FromString_(value, true);
				try { AuException.ThrowIfHresultNot0(_isl.SetIDList(pidl)); } finally { Marshal.FreeCoTaskMem(pidl); }
			}
		}

		/// <summary>
		/// Gets custom icon file path and icon index.
		/// </summary>
		/// <returns><c>null</c> if the shortcut does not have a custom icon (then you see its target icon).</returns>
		/// <param name="iconIndex">Receives 0 or icon index or negative icon resource id.</param>
		[SkipLocalsInit]
		public string GetIconLocation(out int iconIndex) {
			var b = stackalloc char[1024];
			if (0 != _isl.GetIconLocation(b, 1024, out iconIndex)) return null;
			return _CorrectPath(new(b));
		}

		/// <summary>
		/// Sets icon file path and icon index.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="iconIndex">0 or icon index or negative icon resource id.</param>
		/// <exception cref="AuException"/>
		/// <exception cref="ArgumentException">Max length 259.</exception>
		public void SetIconLocation(string path, int iconIndex = 0) {
			AuException.ThrowIfHresultNot0(_isl.SetIconLocation(_Max259(path), iconIndex));
		}

		/// <summary>
		/// Gets or sets the working directory path.
		/// </summary>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		/// <exception cref="ArgumentException">The <c>set</c> function allows max length 259.</exception>
		public string WorkingDirectory {
			get => _CorrectPath(_GetString(_WhatString.WorkingDirectory));
			set { AuException.ThrowIfHresultNot0(_isl.SetWorkingDirectory(_Max259(value))); } //see Description comments
		}

		/// <summary>
		/// Throws if <i>s</i> longer than 259.
		/// <b>SetPath</b> then would throw without error description. <b>SetIconLocation</b> would limit to 260. <b>SetWorkingDirectory</b> and <b>SetDescription</b> succeed but corrupt the <c>.lnk</c> file. <b>SetArguments</b> OK. Others not tested, rare, never mind.
		/// </summary>
		static string _Max259(string s) => s.Lenn() <= 259 ? s : throw new ArgumentException("max length 259");

		/// <summary>
		/// Gets or sets the command-line arguments.
		/// </summary>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		public string Arguments {
			get => _GetString(_WhatString.Arguments);
			set { AuException.ThrowIfHresultNot0(_isl.SetArguments(value)); }
		}

		/// <summary>
		/// Gets or sets the description text (comment).
		/// </summary>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		/// <exception cref="ArgumentException">The <c>set</c> function allows max length 259.</exception>
		public string Description {
			get => _GetString(_WhatString.Description);
			set { AuException.ThrowIfHresultNot0(_isl.SetDescription(_Max259(value))); }
		}

		/// <summary>
		/// Gets or sets hotkey.
		/// </summary>
		/// <exception cref="ArgumentException">The value for the <c>set</c> function includes <c>Win</c>.</exception>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		public (KMod, KKey) Hotkey {
			get {
				if (0 != _isl.GetHotkey(out ushort k2)) return default;
				uint k = k2;
				return ((KMod)(k >> 8 & 7), (KKey)(k & 0xFF));
			}
			set {
				var (m, k) = value;
				if (0 != (m & ~(KMod.Ctrl | KMod.Alt | KMod.Shift))) throw new ArgumentException("Win not supported");
				AuException.ThrowIfHresultNot0(_isl.SetHotkey(Math2.MakeWord((uint)k, (uint)m)));
				_changedHotkey = true;
			}
		}

		/// <summary>
		/// Gets or sets the window show state.
		/// Most programs ignore it.
		/// </summary>
		/// <value>1 (normal, default), 2 (minimized) or 3 (maximized).</value>
		/// <exception cref="AuException">The <c>set</c> function failed.</exception>
		public int ShowState {
			get => (0 == _isl.GetShowCmd(out var R)) ? R : Api.SW_SHOWNORMAL;
			set { AuException.ThrowIfHresultNot0(_isl.SetShowCmd(value)); }
		}

		//Not implemented wrappers for these IShellLink methods:
		//SetRelativePath, Resolve - not useful.
		//All are easy to call through the IShellLink property.

		#region public static

		/// <summary>
		/// Gets shortcut target path or URL or virtual shell object <b>ITEMIDLIST</b>.
		/// Uses <see cref="open"/> and <see cref="TargetAnyType"/>.
		/// </summary>
		/// <param name="lnkPath">Shortcut file (<c>.lnk</c>) path.</param>
		/// <exception cref="AuException">Failed to open.</exception>
		public static string getTarget(string lnkPath) {
			return open(lnkPath).TargetAnyType;
		}

		/// <summary>
		/// If shortcut file exists, unregisters its hotkey and deletes it.
		/// </summary>
		/// <param name="lnkPath"><c>.lnk</c> file path.</param>
		/// <exception cref="AuException">Failed to unregister hotkey.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="filesystem.delete(string, FDFlags)"/>.</exception>
		public static void delete(string lnkPath) {
			if (!filesystem.exists(lnkPath).File) return;
			_UnregisterHotkey(lnkPath);
			filesystem.delete(lnkPath);
		}

		#endregion

		#region private

		/// <exception cref="AuException">Failed to open or save.</exception>
		static void _UnregisterHotkey(string lnkPath) {
			Debug.Assert(filesystem.exists(lnkPath).File);
			using var x = openOrCreate(lnkPath);
			var k = x.Hotkey;
			if (k != default) {
				x.Hotkey = default;
				x.Save();
			}
		}

		enum _WhatString { Path, Arguments, WorkingDirectory, Description }

		[SkipLocalsInit]
		string _GetString(_WhatString what) {
			using FastBuffer<char> b = new();
			for (; ; ) {
				int hr = 1;
				switch (what) {
				case _WhatString.Path: hr = _isl.GetPath(b.p, b.n); break;
				case _WhatString.Arguments: hr = _isl.GetArguments(b.p, b.n); break;
				case _WhatString.WorkingDirectory: hr = _isl.GetWorkingDirectory(b.p, b.n); break;
				case _WhatString.Description: hr = _isl.GetDescription(b.p, b.n); break;
				}
				if (hr != 0) return null;
				if (b.GetString(b.FindStringLength(), out var s, BSFlags.Truncates)) return s;
			}
		}

		string _CorrectPath(string R, bool fixMSI = false) {
			if (R.NE()) return null;

			if (!fixMSI) {
				R = pathname.expand(R);
			} else if (R.Find(@"\Installer\{") > 0) {
				//For MSI shortcuts GetPath gets like "C:\WINDOWS\Installer\{90110409-6000-11D3-8CFE-0150048383C9}\accicons.exe".
				var product = stackalloc char[40];
				var component = stackalloc char[40];
				if (0 != Api.MsiGetShortcutTarget(_lnkPath, product, null, component)) return null;
				//note: for some shortcuts MsiGetShortcutTarget gets empty component. Then MsiGetComponentPath fails.
				//	On my PC was 1 such shortcut - Microsoft Office Excel Viewer.lnk in start menu.
				//	Could not find a workaround.

				int na = 1024; var b = stackalloc char[na];
				int hr = Api.MsiGetComponentPath(product, component, b, ref na);
				if (hr < 0) return null; //eg not installed, just advertised
				if (na == 0) return null;
				R = new(b, 0, na);
				//note: can be a registry key instead of file path. No such shortcuts on my PC.
			}

			//GetPath problem: replaces "c:\program files" with "c:\program files (x86)".
			//These don't help: SLGP_RAWPATH, GetIDList, disabled redirection.
			//GetWorkingDirectory and GetIconLocation get raw path, and envronment variables such as %ProgramFiles% are expanded to (x86) in 32-bit process.
			if (osVersion.is32BitProcessAnd64BitOS) {
				if (_pf == null) { string s = folders.ProgramFilesX86; _pf = s + "\\"; }
				if (R.Starts(_pf, true) && !filesystem.exists(R)) {
					var s2 = R.Remove(_pf.Length - 7, 6);
					if (filesystem.exists(s2)) R = s2;
					//info: "C:\\Program Files (x86)\\" in English, "C:\\Programme (x86)\\" in German etc.
					//never mind: System32 folder also has similar problem, because of redirection.
					//note: ShellExecuteEx also has this problem.
				}
			}

			return R;
		}
		static string _pf;

		#endregion
	}
}
