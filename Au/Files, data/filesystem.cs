
//#define TEST_FINDFIRSTFILEEX

using Microsoft.Win32;

namespace Au;

/// <summary>
/// File and directory functions. Copy, move, delete, find, get properties, enumerate, create directory, load/save, etc.
/// </summary>
/// <remarks>
/// Also you can use .NET file system classes, such as <see cref="File"/> and <see cref="Directory"/> in <b>System.IO</b> namespace. In the past they were too limited and unsafe to use, for example no long paths, too many exceptions, difficult to recursively enumerate directories containing protected items. Later improved, but this class still has something they don't, for example environment variables in path, safe load/save. This class does not have low-level functions to open/read/write files.
/// 
/// Most functions support only full path. Most of them throw <b>ArgumentException</b> if passed a filename or relative path, ie in "current directory". Using current directory is unsafe.
/// Most functions support extended-length paths (longer than 259). Such local paths should have <c>@"\\?\"</c> prefix, like <c>@"\\?\C:\..."</c>. Such network path should be like <c>@"\\?\UNC\server\share\..."</c>. See <see cref="pathname.prefixLongPath"/>, <see cref="pathname.prefixLongPathIfNeed"/>. Many functions support long paths even without prefix.
/// 
/// Disk drives like <c>@"C:\"</c> or <c>"C:"</c> are directories too.
/// </remarks>
public static partial class filesystem {
	#region exists, attributes, properties
	
	/// <summary>
	/// Gets file or directory attributes, size and times.
	/// Calls API <msdn>GetFileAttributesEx</msdn>.
	/// </summary>
	/// <returns><c>false</c> if the file/directory does not exist.</returns>
	/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If flag <b>UseRawPath</b> not used, supports environment variables (see <see cref="pathname.expand"/>).</param>
	/// <param name="properties">Receives properties.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException">Not full path (when not used flag <b>UseRawPath</b>).</exception>
	/// <exception cref="AuException">The file/directory exist but failed to get its properties. Not thrown if used flag <b>DontThrow</b>.</exception>
	/// <remarks>
	/// For NTFS links, gets properties of the link, not of its target.
	/// You can also get most of these properties with <see cref="enumerate"/>.
	/// </remarks>
	public static unsafe bool getProperties(string path, out FileProperties properties, FAFlags flags = 0) {
		properties = new();
		if (0 == (flags & FAFlags.UseRawPath)) path = pathname.NormalizeMinimally_(path); //the API supports .. etc
		if (!Api.GetFileAttributesEx(path, 0, out var d)) {
			if (!_GetAttributesOnError(path, flags, out _, out _, &d)) return false;
		}
		properties.Attributes = d.dwFileAttributes;
		properties.Size = (long)d.nFileSizeHigh << 32 | d.nFileSizeLow;
		properties.LastWriteTimeUtc = DateTime.FromFileTimeUtc(d.ftLastWriteTime);
		properties.CreationTimeUtc = DateTime.FromFileTimeUtc(d.ftCreationTime);
		properties.LastAccessTimeUtc = DateTime.FromFileTimeUtc(d.ftLastAccessTime);
		if (d.dwFileAttributes.Has(FileAttributes.ReparsePoint)) properties.IsNtfsLink = 0 != IsNtfsLink_(path);
		return true;
	}
	
	/// <summary>
	/// Gets file or directory attributes.
	/// </summary>
	/// <returns><c>false</c> if the file/directory does not exist.</returns>
	/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If flag <b>UseRawPath</b> not used, supports environment variables (see <see cref="pathname.expand"/>).</param>
	/// <param name="attributes">Receives attributes, or 0 if failed.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException">Not full path (when not used flag <b>UseRawPath</b>).</exception>
	/// <exception cref="AuException">Failed. Not thrown if used flag <b>DontThrow</b>.</exception>
	/// <remarks>
	/// Calls API <msdn>GetFileAttributes</msdn>.
	/// For NTFS links, gets attributes of the link, not of its target.
	/// </remarks>
	public static unsafe bool getAttributes(string path, out FileAttributes attributes, FAFlags flags = 0) {
		if (0 == (flags & FAFlags.UseRawPath)) path = pathname.NormalizeMinimally_(path); //the API supports .. etc
		var a = Api.GetFileAttributes(path);
		if (a == (FileAttributes)(-1)) return _GetAttributesOnError(path, flags, out attributes, out _);
		attributes = a;
		return true;
	}
	
	static unsafe bool _GetAttributesOnError(string path, FAFlags flags, out FileAttributes attr, out bool ntfsLink, Api.WIN32_FILE_ATTRIBUTE_DATA* p = null) {
		attr = 0; ntfsLink = false;
		var ec = lastError.code;
		switch (ec) {
		case Api.ERROR_FILE_NOT_FOUND or Api.ERROR_PATH_NOT_FOUND or Api.ERROR_NOT_READY:
			return false;
		case Api.ERROR_SHARING_VIOLATION: //eg c:\pagefile.sys. GetFileAttributes fails, but FindFirstFile succeeds.
		case Api.ERROR_ACCESS_DENIED: //probably in a protected directory. Then FindFirstFile fails, but try anyway.
			var d = new Api.WIN32_FIND_DATA();
			var hfind = Api.FindFirstFile(path, out d);
			if (hfind != (IntPtr)(-1)) {
				Api.FindClose(hfind);
				attr = d.dwFileAttributes;
				if (p != null) {
					p->dwFileAttributes = d.dwFileAttributes;
					p->nFileSizeHigh = d.nFileSizeHigh;
					p->nFileSizeLow = d.nFileSizeLow;
					p->ftCreationTime = d.ftCreationTime;
					p->ftLastAccessTime = d.ftLastAccessTime;
					p->ftLastWriteTime = d.ftLastWriteTime;
				}
				ntfsLink = 0 != d.IsNtfsLink;
				return true;
			}
			lastError.code = ec;
			attr = (FileAttributes)(-1);
			break;
		}
		if (0 != (flags & FAFlags.DontThrow)) return false;
		throw new AuException(ec, $"*get file attributes: '{path}'");
		
		//tested:
		//	If the file is in a protected directory, ERROR_ACCESS_DENIED.
		//	If the path is to a non-existing file in a protected directory, ERROR_FILE_NOT_FOUND.
		//	ERROR_SHARING_VIOLATION for C:\pagefile.sys etc.
	}
	
	/// <summary>
	/// Gets attributes.
	/// Returns <c>false</c> if INVALID_FILE_ATTRIBUTES or if relative path. No exceptions.
	/// </summary>
	static unsafe bool _GetAttributes(string path, out FileAttributes attr, out bool ntfsLink, bool useRawPath) {
		if (!useRawPath) path = pathname.NormalizeMinimally_(path, throwIfNotFullPath: false);
		//note: NormalizeMinimally_ does not remove \ at the end. The API succeeds if "C:\x\dir\" but fails if "C:\x\file\" (it's good).
		
		attr = Api.GetFileAttributes(path);
		if (attr != (FileAttributes)(-1)) ntfsLink = attr.Has(FileAttributes.ReparsePoint) && 0 != IsNtfsLink_(path);
		else if (!_GetAttributesOnError(path, FAFlags.DontThrow, out attr, out ntfsLink)) return false;
		
		if (!useRawPath && !pathname.isFullPath(path)) { lastError.code = Api.ERROR_FILE_NOT_FOUND; return false; }
		return true;
	}
	
	/// <summary>
	/// Sets file or directory attributes.
	/// </summary>
	/// <returns><c>false</c> if the file/directory does not exist.</returns>
	/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If flag <b>UseRawPath</b> not used, supports environment variables (see <see cref="pathname.expand"/>).</param>
	/// <param name="attributes">Attributes to set, add or remove.</param>
	/// <param name="add"><c>null</c> (default) - set; <c>true</c> - add; <c>false</c> - remove.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException">Not full path (when not used flag <b>UseRawPath</b>).</exception>
	/// <exception cref="AuException">Failed. Not thrown if used flag <b>DontThrow</b>.</exception>
	/// <remarks>
	/// Calls API <msdn>SetFileAttributes</msdn>.
	/// For NTFS links, sets attributes of the link, not of its target.
	/// </remarks>
	public static unsafe bool setAttributes(string path, FileAttributes attributes, bool? add = null, FAFlags flags = 0) {
		if (0 == (flags & FAFlags.UseRawPath)) path = pathname.NormalizeMinimally_(path); //the API supports .. etc
		var a = attributes;
		if (add != null) {
			var v = Api.GetFileAttributes(path);
			if (add.Value) a = v | attributes; else a = v & ~attributes;
			if (a == attributes) return true;
		}
		if (Api.SetFileAttributes(path, a)) return true;
		if (0 != (flags & FAFlags.DontThrow)) return false;
		throw new AuException(0, $"*set file attributes: '{path}'");
	}
	
	/// <summary>
	/// Calls API <b>FindFirstFile</b> to determine whether <i>path</i> is a NTFS link, such as symbolic link or mount point.
	/// </summary>
	/// <param name="path">Raw path (does not normalize).</param>
	/// <returns>-1 failed, 0 no, 1 symlink, 2 mount, 3 other.</returns>
	internal static int IsNtfsLink_(string path) {
		var hfind = Api.FindFirstFile(path, out var fd);
		if (hfind == (IntPtr)(-1)) return -1;
		int R = fd.IsNtfsLink;
		Api.FindClose(hfind);
		return R;
	}
	
	/// <summary>
	/// Gets file or directory attributes as <see cref="FAttr"/> that tells whether it exists, is directory, readonly, hidden, system, NTFS link. See examples.
	/// </summary>
	/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If <i>useRawPath</i> is <c>false</c> (default), supports environment variables (see <see cref="pathname.expand"/>). Can be <c>null</c>.</param>
	/// <param name="useRawPath">Pass <i>path</i> to the API as it is, without any normalizing and full-path checking.</param>
	/// <remarks>
	/// Supports <see cref="lastError"/>. If you need exception when fails, instead call <see cref="getAttributes"/> and check attribute <b>Directory</b>.
	/// Always use full path. If not full path: if <i>useRawPath</i> is <c>false</c> (default), can't find the file; if <i>useRawPath</i> is <c>true</c>, searches in "current directory".
	/// For NTFS links gets attributes of the link, not of the target; does not care whether its target exists.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var path = @"C:\Test\test.txt";
	/// if (filesystem.exists(path)) print.it("exists");
	/// if (filesystem.exists(path).File) print.it("exists as file");
	/// if (filesystem.exists(path).Directory) print.it("exists as directory");
	/// if (filesystem.exists(path) is FAttr { File: true, IsReadonly: false }) print.it("exists as file and isn't readonly");
	/// switch (filesystem.exists(path)) {
	/// case 0: print.it("doesn't exist"); break;
	/// case 1: print.it("file"); break;
	/// case 2: print.it("directory"); break;
	/// }
	/// ]]></code>
	/// </example>
	public static FAttr exists(string path, bool useRawPath = false) {
		if (_GetAttributes(path, out var a, out bool ntfsLink, useRawPath)) return new(a, true, ntfsLink);
		return new(0, (a == (FileAttributes)(-1)) ? null : false, false);
	}
	
	/// <summary>
	/// Gets file system entry type - file, directory, NTFS link, whether it exists and is accessible.
	/// Returns <b>NotFound</b> (0) if does not exist. Returns <b>AccessDenied</b> (&lt; 0) if exists but this process cannot access it and get attributes.
	/// Calls API <msdn>GetFileAttributes</msdn>.
	/// </summary>
	/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If <i>useRawPath</i> is <c>false</c> (default), supports environment variables (see <see cref="pathname.expand"/>). Can be <c>null</c>.</param>
	/// <param name="useRawPath">Pass path to the API as it is, without any normalizing and full-path checking.</param>
	/// <remarks>
	/// Supports <see cref="lastError"/>. If you need exception when fails, instead call <see cref="getAttributes"/>.
	/// Always use full path. If path is not full: if <i>useRawPath</i> is <c>false</c> (default) returns <b>NotFound</b>; if <i>useRawPath</i> is <c>true</c>, searches in "current directory".
	/// </remarks>
	internal static unsafe FileIs_ ExistsAs_(string path, bool useRawPath = false) {
		if (!_GetAttributes(path, out var a, out bool ntfsLink, useRawPath)) {
			return (a == (FileAttributes)(-1)) ? FileIs_.AccessDenied : FileIs_.NotFound;
		}
		var R = a.Has(FileAttributes.Directory) ? FileIs_.Directory : FileIs_.File;
		if (ntfsLink) R |= (FileIs_)4;
		return R;
	}
	
	#endregion
	
	#region enumerate, search
	
	/// <summary>
	/// Finds file or directory and returns full path.
	/// </summary>
	/// <returns><c>null</c> if not found.</returns>
	/// <remarks>
	/// If the <i>path</i> argument is full path, calls <see cref="exists"/> and returns normalized path if exists, <c>null</c> if not.
	/// Else searches in these places:
	/// 1. <i>dirs</i>, if used.
	/// 2. <see cref="folders.ThisApp"/>.
	/// 3. Calls API <msdn>SearchPath</msdn>, which searches in process directory, Windows system directories, current directory, <c>PATH</c> environment variable. The search order depends on API <msdn>SetSearchPathMode</msdn> or registry settings.
	/// 4. If <i>path</i> ends with <c>".exe"</c>, tries to get path from registry "App Paths" keys.
	/// </remarks>
	/// <param name="path">Full or relative path or just filename with extension. Supports network paths too.</param>
	/// <param name="dirs">0 or more directories where to search.</param>
	public static unsafe string searchPath(string path, params string[] dirs) {
		if (path.NE()) return null;
		
		string s = path;
		if (pathname.isFullPathExpand(ref s, strict: false)) {
			if (exists(s)) return pathname.Normalize_(s, noExpandEV: true);
			return null;
		}
		
		if (dirs != null) {
			foreach (var d in dirs) {
				s = d;
				if (!pathname.isFullPathExpand(ref s)) continue;
				s = pathname.combine(s, path);
				if (exists(s)) return pathname.Normalize_(s, noExpandEV: true);
			}
		}
		
		s = folders.ThisApp + path;
		if (exists(s)) return pathname.Normalize_(s, noExpandEV: true);
		
		if ((s = Api.SearchPath(null, path)) != null) return s;
		
		if (path.Ends(".exe", true) && path.FindAny(@"\/") < 0) {
			try {
				s = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\" + path, "", null) as string
					?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\" + path, "", null) as string;
				if (s != null) {
					s = _PreparePath(s.Trim('"'));
					if (exists(s, true)) return s;
				}
			}
			catch (Exception ex) { Debug_.Print(ex); }
		}
		
		//maybe PATH env var changed since this or parent process started
		//rejected. LA updates at startup an on WM_SETTINGCHANGE, and that's enough.
		//if (more.UpdatePathEnvVar_())
		//	if ((s = Api.SearchPath(null, path)) != null) return s;
		
		return null;
	}
	
	/// <summary>
	/// Gets names and other info of files and subdirectories in the specified directory.
	/// </summary>
	/// <returns>An enumerable collection of <see cref="FEFile"/> objects.</returns>
	/// <param name="directoryPath">Full path of the directory.</param>
	/// <param name="flags"></param>
	/// <param name="fileFilter">
	/// Callback function that is called for each file (but not subdirectory). Let it return <c>true</c> to include the file in results.
	/// Example: <c>f => f.Name.Ends(".png", true)</c>.
	/// </param>
	/// <param name="dirFilter">
	/// Callback function that is called for each subdirectory. Let it return flags: 1 - include the directory in results; 2 - include its children in results.
	/// The return value overrides flags <see cref="FEFlags.OnlyFiles"/> and <see cref="FEFlags.AllDescendants"/>.
	/// Example: <c>d => d.Name.Eqi("Debug") ? 0 : 3</c>.
	/// </param>
	/// <param name="errorHandler">
	/// Callback function that is called when fails to get children of a subdirectory, when using flag <see cref="FEFlags.AllDescendants"/>.
	/// Receives the subdirectory path. Can call <see cref="lastError"/><b>.Code</b> and throw an exception. If does not throw, the enumeration continues as if the directory is empty.
	/// If <i>errorHandler</i> not used, then <b>enumerate</b> throws exception. See also: flag <see cref="FEFlags.IgnoreInaccessible"/>.
	/// </param>
	/// <exception cref="ArgumentException"><i>directoryPath</i> is invalid path or not full path.</exception>
	/// <exception cref="DirectoryNotFoundException"><i>directoryPath</i> directory does not exist.</exception>
	/// <exception cref="AuException">Failed to get children of <i>directoryPath</i> or of a subdirectory.</exception>
	/// <remarks>
	/// Uses API <msdn>FindFirstFile</msdn>.
	/// 
	/// By default gets only direct children. Use flag <see cref="FEFlags.AllDescendants"/> to get all descendants.
	/// 
	/// The paths that this function gets are normalized, ie may not start with exact <i>directoryPath</i> string. Expanded environment variables (see <see cref="pathname.expand"/>), <c>".."</c>, DOS path etc. Paths longer than <see cref="pathname.maxDirectoryPathLength"/> have <c>@"\\?\"</c> prefix (see <see cref="pathname.prefixLongPathIfNeed"/>).
	/// 
	/// For NTFS links (symbolic links, mount points) gets link info, not target info.
	/// 
	/// These errors are ignored:
	/// 1. Missing target directory of a NTFS link.
	/// 2. If used flag <see cref="FEFlags.IgnoreInaccessible"/> - access denied.
	/// 
	/// When an error is ignored, the function works as if that [sub]directory is empty; does not throw exception and does not call <i>errorHandler</i>.
	/// 
	/// Enumeration of a subdirectory starts immediately after the subdirectory itself is retrieved.
	/// </remarks>
	public static IEnumerable<FEFile> enumerate(string directoryPath, FEFlags flags = 0,
		Func<FEFile, bool> fileFilter = null,
		Func<FEFile, int> dirFilter = null,
		Action<string> errorHandler = null
		) {
		//tested 2021-04-30: much faster than Directory.EnumerateX in .NET 5. Faster JIT, and then > 2 times faster.
		//tested 2022-01-31: ~20% slower than Directory.EnumerateX in .NET 6. Not tested JIT. Never mind.
		//	It seems .NET uses undocumented API NtQueryDirectoryFile.
		//rejected: in this func use .NET FileSystemEnumerable.
		//	Good: faster; familiar types.
		//	Bad: something we need is missing or difficult to return or need a workaround. Eg easily detect NTFS links, get relative path, prevent recursion to NTFS link target.
		
		string path = directoryPath;
		if (0 == (flags & FEFlags.UseRawPath)) path = _PreparePath(path);
		path = path.RemoveSuffix('\\');
		
		var d = new Api.WIN32_FIND_DATA();
		IntPtr hfind = default;
		var stack = new Stack<_EDStackEntry>();
		bool isFirst = true;
		FileAttributes attr = 0;
		int basePathLength = path.Length;
		//var redir = new FileSystemRedirection();
		
		try {
			//if (0 != (flags & FEFlags.DisableRedirection)) redir.Disable();
			
			for (; ; ) {
				if (isFirst) {
					isFirst = false;
					var path2 = ((path.Length <= pathname.maxDirectoryPathLength - 2) ? path : pathname.prefixLongPath(path)) + @"\*";
#if TEST_FINDFIRSTFILEEX
					hfind = Api.FindFirstFileEx(path2, Api.FINDEX_INFO_LEVELS.FindExInfoBasic, out d, 0, default, 0);
					//speed: FindFirstFileEx 0-2 % slower. FindExInfoBasic makes 0-2% faster. FIND_FIRST_EX_LARGE_FETCH makes 1-50% slower.
#else
					hfind = Api.FindFirstFile(path2, out d);
#endif
					if (hfind == (IntPtr)(-1)) {
						hfind = default;
						var ec = lastError.code;
						//print.it(ec, lastError.messageFor(ec), path);
						bool itsOK = false;
						switch (ec) {
						case Api.ERROR_FILE_NOT_FOUND:
						case Api.ERROR_NO_MORE_FILES:
						case Api.ERROR_NOT_READY:
							//rare, because most directories have "." and "..".
							//FindFirstFileEx sets ERROR_NO_MORE_FILES for some.
							//FindFirstFile could set ERROR_FILE_NOT_FOUND (documented), but was never in my tests.
							//ERROR_NOT_READY when trying to access an ejected CD/DVD drive etc.
							itsOK = true;
							break;
						case Api.ERROR_ACCESS_DENIED:
							itsOK = 0 != (flags & FEFlags.IgnoreInaccessible);
							break;
						case Api.ERROR_PATH_NOT_FOUND: //the directory not found, or NTFS link target directory is missing
						case Api.ERROR_DIRECTORY: //it is file, not directory. Error text is "The directory name is invalid".
						case Api.ERROR_BAD_NETPATH: //eg \\COMPUTER\MissingFolder
							if (stack.Count == 0 && !exists(path, true).Directory)
								throw new DirectoryNotFoundException($"Directory not found: '{path}'. {lastError.messageFor(ec)}");
							//itsOK = (attr & Api.FILE_ATTRIBUTE_REPARSE_POINT) != 0;
							itsOK = true; //or maybe the subdirectory was deleted after we retrieved it
							break;
						case Api.ERROR_INVALID_NAME: //eg contains invalid characters
						case Api.ERROR_BAD_NET_NAME: //eg \\COMPUTER
							if (stack.Count == 0)
								throw new ArgumentException(lastError.messageFor(ec));
							itsOK = true;
							break;
						}
						if (!itsOK) {
							if (errorHandler == null || stack.Count == 0) throw new AuException(ec, $"*enumerate directory '{path}'");
							Api.SetLastError(ec); //the above code possibly changed it, although currently it doesn't
							errorHandler(path);
						}
					}
				} else {
					if (!Api.FindNextFile(hfind, out d)) {
						Debug.Assert(lastError.code == Api.ERROR_NO_MORE_FILES);
						Api.FindClose(hfind);
						hfind = default;
					}
				}
				
				if (hfind == default) {
					if (stack.Count == 0) break;
					var t = stack.Pop();
					hfind = t.hfind; path = t.path;
					continue;
				}
				
				var name = d.Name;
				if (name == null) continue; //".", ".."
				attr = d.dwFileAttributes;
				bool isDir = (attr & FileAttributes.Directory) != 0;
				
				if ((flags & FEFlags.SkipHidden) != 0 && (attr & FileAttributes.Hidden) != 0) continue;
				const FileAttributes hidSys = FileAttributes.Hidden | FileAttributes.System;
				if ((attr & hidSys) == hidSys) {
					if ((flags & FEFlags.SkipHiddenSystem) != 0) continue;
					//skip Recycle Bin etc. It is useless, prevents copying drives, etc.
					if (isDir && path.Ends(':')) {
						if (name.Eqi("$Recycle.Bin")) continue;
						if (name.Eqi("System Volume Information")) continue;
						if (name.Eqi("Recovery")) continue;
					}
				}
				
				var fullPath = path + @"\" + name;
				if (0 != (flags & FEFlags.NeedRelativePaths)) name = fullPath[basePathLength..];
				
				//prepend @"\\?\" etc if need. Don't change fullPath length, because then would be difficult to get relative path.
				var fp2 = pathname.prefixLongPathIfNeed(fullPath);
				
				var r = new FEFile(name, fp2, d, stack.Count); //never mind, don't need for dirs if no dirFilter and is flag OnlyFiles
				
				if (isDir) {
					int inc = dirFilter != null ? dirFilter(r) : (flags.Has(FEFlags.OnlyFiles) ? 0 : 1) | (flags.Has(FEFlags.AllDescendants) ? 2 : 0);
					if (0 != (1 & inc)) yield return r;
					if (0 == (2 & inc)) continue;
					if (!flags.Has(FEFlags.RecurseNtfsLinks) && 0 != d.IsNtfsLink) continue;
					
					stack.Push(new _EDStackEntry() { hfind = hfind, path = path });
					hfind = default; path = fullPath;
					isFirst = true;
				} else {
					if (fileFilter == null || fileFilter(r)) yield return r;
				}
			}
		}
		finally {
			if (hfind != default) Api.FindClose(hfind);
			while (stack.Count > 0) Api.FindClose(stack.Pop().hfind);
			
			//redir.Revert();
		}
	}
	
	struct _EDStackEntry { internal IntPtr hfind; internal string path; }
	
	/// <summary>
	/// Gets names and other info of matching files in the specified directory.
	/// </summary>
	/// <param name="directoryPath">Full path of the directory.</param>
	/// <param name="pattern">
	/// File name pattern. Format: [wildcard expression](xref:wildcard_expression). Used only for files, not for subdirectories. Can be <c>null</c>.
	/// Examples:
	/// <br/>• <c>"*.png"</c> (only png files),
	/// <br/>• <c>"**m *.png||*.bmp"</c> (only png and bmp files),
	/// <br/>• <c>"**nm *.png||*.bmp"</c> (all files except png and bmp),
	/// <br/>• <c>@"**r \.html?$"</c> (regular expression that matches <c>.htm</c> and <c>.html</c> files).
	/// </param>
	/// <param name="flags">Flags. The function also adds flag <b>OnlyFiles</b>.</param>
	/// <exception cref="ArgumentException">
	/// <i>directoryPath</i> is invalid path or not full path.
	/// Invalid <i>pattern</i> (<c>"**options "</c> or regular expression).
	/// </exception>
	/// <exception cref="DirectoryNotFoundException"><i>directoryPath</i> directory does not exist.</exception>
	/// <exception cref="AuException">Failed to get children of <i>directoryPath</i> or of a subdirectory.</exception>
	/// <inheritdoc cref="enumerate(string, FEFlags, Func{FEFile, bool}, Func{FEFile, int}, Action{string})"/>
	public static IEnumerable<FEFile> enumFiles(string directoryPath, string pattern = null, FEFlags flags = 0) {
		flags |= FEFlags.OnlyFiles;
		if (pattern == null) return enumerate(directoryPath, flags);
		wildex w = pattern;
		return enumerate(directoryPath, flags, f => w.Match(f.Name));
	}
	
	/// <summary>
	/// Gets names and other info of matching subdirectories in the specified directory.
	/// </summary>
	/// <param name="directoryPath">Full path of the directory.</param>
	/// <param name="pattern">
	/// Directory name pattern. Format: [wildcard expression](xref:wildcard_expression). Can be <c>null</c>.
	/// </param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException">
	/// <i>directoryPath</i> is invalid path or not full path.
	/// Invalid <i>pattern</i> (<c>"**options "</c> or regular expression).
	/// </exception>
	/// <exception cref="DirectoryNotFoundException"><i>directoryPath</i> directory does not exist.</exception>
	/// <exception cref="AuException">Failed to get children of <i>directoryPath</i> or of a subdirectory.</exception>
	/// <inheritdoc cref="enumerate(string, FEFlags, Func{FEFile, bool}, Func{FEFile, int}, Action{string})"/>
	public static IEnumerable<FEFile> enumDirectories(string directoryPath, string pattern = null, FEFlags flags = 0) {
		if (pattern == null) return enumerate(directoryPath, flags, _ => false);
		wildex w = pattern;
		return enumerate(directoryPath, flags, _ => false, d => !w.Match(d.Name) ? 0 : flags.Has(FEFlags.AllDescendants) ? 3 : 1);
	}
	
	#endregion
	
	#region move, copy, rename, delete
	
	enum _FileOp { Rename, Move, Copy, }
	
	static unsafe void _FileOperation(_FileOp op, bool into, string path1, string path2, FIfExists ifExists,
		FCFlags copyFlags = 0, Func<FEFile, bool> copyFileFilter = null, Func<FEFile, int> copyDirFilter = null
		) {
		string opName = (op == _FileOp.Rename) ? "rename" : ((op == _FileOp.Move) ? "move" : "copy");
		path1 = _PreparePath(path1);
		var type1 = ExistsAs_(path1, true);
		if (type1 <= 0) throw new FileNotFoundException($"Failed to {opName}. File not found: '{path1}'");
		
		if (op == _FileOp.Rename) {
			op = _FileOp.Move;
			if (pathname.isInvalidName(path2)) throw new ArgumentException($"Invalid filename: '{path2}'");
			path2 = pathname.Combine_(_RemoveFilename(path1), path2);
		} else {
			string path2Parent;
			if (into) {
				path2Parent = _PreparePath(path2);
				path2 = pathname.Combine_(path2Parent, _GetFilename(path1));
			} else {
				path2 = _PreparePath(path2);
				path2Parent = _RemoveFilename(path2, true);
			}
			if (path2Parent != null) {
				try { _CreateDirectory(path2Parent, pathIsPrepared: true); }
				catch (Exception ex) { throw new AuException($"*create directory: '{path2Parent}'", ex); }
			}
		}
		
		bool ok = false, copy = op == _FileOp.Copy, deleteSource = false, mergeDirectory = false;
		var del = new _SafeDeleteExistingDirectory();
		try {
			if (ifExists == FIfExists.MergeDirectory && type1 != FileIs_.Directory) ifExists = FIfExists.Fail;
			
			if (ifExists == FIfExists.Fail) {
				//API will fail if exists. We don't use use API flags 'replace existing'.
			} else {
				//Delete, RenameExisting, MergeDirectory
				//bool deleted = false;
				var existsAs = ExistsAs_(path2, true);
				bool existsAsDir = existsAs is FileIs_.Directory or FileIs_.NtfsLinkDirectory;
				switch (existsAs) {
				case FileIs_.NotFound:
					//deleted = true;
					break;
				case FileIs_.AccessDenied:
					break;
				default:
					if (more.isSameFile(path1, path2, useSymlink: true)) {
						//eg renaming "file.txt" to "FILE.txt"
						Debug_.Print("same file");
						//deleted = true;
						//copy will fail, move will succeed
					} else if (ifExists == FIfExists.RenameNew) {
						path2 = pathname.makeUnique(path2, existsAsDir);
					} else if (ifExists == FIfExists.MergeDirectory && existsAsDir) {
						if (type1 is FileIs_.Directory or FileIs_.NtfsLinkDirectory) {
							//deleted = true;
							mergeDirectory = true;
							if (!copy) { copy = true; deleteSource = true; }
						} // else API will fail. We refuse to replace a directory with a file.
					} else if (ifExists == FIfExists.RenameExisting || existsAs == FileIs_.Directory) {
						//deleted = 
						del.Rename(path2, ifExists == FIfExists.RenameExisting);
						//Rename to a temp name. Finally delete if ok (if !RenameExisting), undo if failed.
						//It also solves this problem: if we delete the directory now, need to ensure that it does not delete the source directory, which is quite difficult.
					} else {
						//deleted = 0 ==
						_DeleteL(path2, existsAs == FileIs_.NtfsLinkDirectory);
					}
					break;
				}
				//if(!deleted) throw new AuException(Api.ERROR_FILE_EXISTS, $"*{opName}"); //don't need, later API will fail
			}
			
			if (!copy) {
				//note: don't use MOVEFILE_COPY_ALLOWED, because then moving directory to another drive fails with ERROR_ACCESS_DENIED and we don't know that the reason is different drive
				if (ok = Api.MoveFileEx(path1, path2, 0)) return;
				if (lastError.code == Api.ERROR_NOT_SAME_DEVICE) {
					copy = true;
					deleteSource = true;
				}
			}
			
			if (copy) {
				if (type1 == FileIs_.Directory) {
					try {
						_CopyDirectory(path1, path2, mergeDirectory, copyFlags, copyFileFilter, copyDirFilter);
						ok = true;
					}
					catch (Exception ex) when (op != _FileOp.Copy) {
						throw new AuException($"*{opName} '{path1}' to '{path2}'", ex);
					}
				} else {
					if (type1 == FileIs_.NtfsLinkDirectory)
						ok = Api.CreateDirectoryEx(path1, path2, default);
					else
						ok = Api.CopyFileEx(path1, path2, null, default, null, Api.COPY_FILE_FAIL_IF_EXISTS | Api.COPY_FILE_COPY_SYMLINK);
				}
			}
			
			if (!ok) throw new AuException(0, $"*{opName} '{path1}' to '{path2}'");
			
			if (deleteSource) {
				try {
					_Delete(path1);
				}
				catch (Exception ex) {
					if (!path1.Ends(':')) //moving drive contents. Deleted contents but cannot delete drive.
						print.warning($"Failed to delete '{path1}' after copying it to another drive. {ex.Message}");
					//throw new AuException("*move", ex); //don't. MoveFileEx also succeeds even if fails to delete source.
				}
			}
		}
		finally {
			//dialog.show();
			del.Finally(ok);
		}
	}
	
	//note: if merge, the destination directory must exist
	static unsafe void _CopyDirectory(string path1, string path2, bool merge, FCFlags copyFlags,
		Func<FEFile, bool> fileFilter, Func<FEFile, int> dirFilter
		) {
		//FUTURE: add progressInterface parameter. Create a default interface implementation class that supports progress dialog and/or progress in taskbar button. Or instead create a ShellCopy function.
		//FUTURE: maybe add errorHandler parameter. Call it here when fails to copy, and also pass to Enumerate which calls it when fails to enum.
		
		//use intermediate array, and get it before creating path2 directory. It requires more memory, but is safer. Without it, eg bad things happen when copying a directory into itself.
		var edFlags = FEFlags.AllDescendants | FEFlags.NeedRelativePaths | FEFlags.UseRawPath | (FEFlags)copyFlags;
		var a = enumerate(path1, edFlags, fileFilter, dirFilter).ToArray();
		
		if (copyFlags.Has(FCFlags.NoEmptyDirectories)) {
			for (int i = a.Length; --i >= 0;) {
				var f = a[i];
				if (!f.IsDirectory) continue;
				if (i < a.Length - 1 && a[i + 1] is FEFile ff) {
					if (ff.Name.Starts(f.Name) && ff.Name.Eq(f.Name.Length, '\\')) continue;
				}
				a[i] = null;
			}
		}
		
		bool ok = false;
		string s1 = null, s2 = null;
		if (!merge) {
			if (!path1.Ends(@":\")) ok = Api.CreateDirectoryEx(path1, path2, default);
			if (!ok) ok = Api.CreateDirectory(path2, default);
			if (!ok) goto ge;
		}
		
		string prevParentDir = null;
		foreach (var f in a) {
			if (f == null) continue;
			s1 = f.FullPath; s2 = pathname.prefixLongPathIfNeed(path2 + f.Name);
			//print.it(s2);
			
			//create intermediate dirs if need, eg if dirFilter returned 2 (don't include the dir but include its children)
			//TODO3: CreateDirectoryEx
			if (f.Level > 0 && (fileFilter != null || dirFilter != null)) {
				int ifn = _FindFilename(s2) - 1;
				if (prevParentDir == null || !s2.AsSpan(0, ifn).Eq(prevParentDir)) { //optimize to avoid this code for each file
					prevParentDir = s2[..ifn];
					_CreateDirectory(prevParentDir, pathIsPrepared: true);
				}
			}
			
			if (f.IsDirectory) {
				if (merge) switch (exists(s2, true)) {
					case 2: continue; //never mind: check NTFS link mismatch
					case 1: _DeleteL(s2, false); break;
					}
				
				ok = Api.CreateDirectoryEx(s1, s2, default);
				if (!ok && !f.IsNtfsLink) ok = Api.CreateDirectory(s2, default);
			} else {
				if (merge && getAttributes(s2, out var attr, FAFlags.DontThrow | FAFlags.UseRawPath)) {
					const FileAttributes badAttr = FileAttributes.ReadOnly | FileAttributes.Hidden;
					if (0 != (attr & FileAttributes.Directory)) _Delete(s2);
					else if (0 != (attr & badAttr)) Api.SetFileAttributes(s2, attr & ~badAttr);
				}
				
				uint fl = Api.COPY_FILE_COPY_SYMLINK; if (!merge) fl |= Api.COPY_FILE_FAIL_IF_EXISTS;
				ok = Api.CopyFileEx(s1, s2, null, default, null, fl);
			}
			if (!ok) {
				if (f.IsNtfsLink) {
					//To create or copy NTFS links, need SeCreateSymbolicLinkPrivilege privilege.
					//Admins have it, else this process cannot get it.
					//Debug_.Print($"failed to copy NTFS link '{s1}'. It's OK, skipped it. Error: {lastError.messageFor()}");
					continue;
				}
				if (0 != (copyFlags & FCFlags.IgnoreInaccessible)) {
					if (lastError.code == Api.ERROR_ACCESS_DENIED) continue;
				}
				goto ge;
			}
		}
		return;
		ge:
		string se = $"*copy directory '{path1}' to '{path2}'";
		if (s1 != null) se += $" ('{s1}' to '{s2}')";
		throw new AuException(0, se);
		//never mind: wrong API error code if path1 and path2 is the same directory.
	}
	
	struct _SafeDeleteExistingDirectory {
		string _oldPath, _tempPath;
		bool _dontDelete;
		
		/// <summary>
		/// note: path must be normalized.
		/// </summary>
		internal bool Rename(string path, bool dontDelete = false) {
			if (path.Length > pathname.maxDirectoryPathLength - 10) path = pathname.prefixLongPath(path);
			string tempPath = null;
			int iFN = _FindFilename(path);
			string s1 = path[..iFN] + "old", s2 = " " + path[iFN..];
			for (int i = 1; ; i++) {
				tempPath = s1 + i + s2;
				if (!exists(tempPath, true)) break;
			}
			if (!Api.MoveFileEx(path, tempPath, 0)) return false;
			_oldPath = path; _tempPath = tempPath; _dontDelete = dontDelete;
			return true;
		}
		
		internal bool Finally(bool succeeded) {
			if (_tempPath == null) return true;
			if (!succeeded) {
				if (!Api.MoveFileEx(_tempPath, _oldPath, 0)) return false;
			} else if (!_dontDelete) {
				try { _Delete(_tempPath); } catch { return false; }
			}
			_oldPath = _tempPath = null;
			return true;
		}
	}
	
	/// <summary>
	/// Renames file or directory.
	/// </summary>
	/// <param name="path">Full path.</param>
	/// <param name="newName">New name without path. Example: <c>"name.txt"</c>.</param>
	/// <param name="ifExists"></param>
	/// <exception cref="ArgumentException">
	/// - <i>path</i> is not full path (see <see cref="pathname.isFullPath"/>).
	/// - <i>newName</i> is invalid filename.
	/// </exception>
	/// <exception cref="FileNotFoundException"><i>path</i> not found.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// Uses API <msdn>MoveFileEx</msdn>.
	/// </remarks>
	public static void rename(string path, string newName, FIfExists ifExists = FIfExists.Fail) {
		_FileOperation(_FileOp.Rename, false, path, newName, ifExists);
	}
	
	/// <summary>
	/// Moves (changes path of) file or directory.
	/// </summary>
	/// <param name="path">Full path.</param>
	/// <param name="newPath">
	/// New full path.
	/// <para>NOTE: It is not the new parent directory. See <see cref="moveTo"/>.</para>
	/// </param>
	/// <param name="ifExists"></param>
	/// <exception cref="ArgumentException"><i>path</i> or <i>newPath</i> is not full path (see <see cref="pathname.isFullPath"/>).</exception>
	/// <exception cref="FileNotFoundException"><i>path</i> not found.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// In most cases uses API <msdn>MoveFileEx</msdn>. It's fast, because don't need to copy files.
	/// In these cases copies/deletes: destination is on another drive; need to merge directories.
	/// When need to copy, does not copy security properties; sets default.
	/// Creates the destination directory if does not exist (see <see cref="createDirectory"/>).
	/// If <i>path</i> and <i>newPath</i> share the same parent directory, just renames the file.
	/// </remarks>
	public static void move(string path, string newPath, FIfExists ifExists = FIfExists.Fail) {
		_FileOperation(_FileOp.Move, false, path, newPath, ifExists);
	}
	
	/// <summary>
	/// Moves file or directory into another directory.
	/// </summary>
	/// <param name="path">Full path.</param>
	/// <param name="newDirectory">Full path of the new parent directory.</param>
	/// <param name="ifExists"></param>
	/// <exception cref="ArgumentException">
	/// - <i>path</i> or <i>newDirectory</i> is not full path (see <see cref="pathname.isFullPath"/>).
	/// - <i>path</i> is drive. To move drive content, use <see cref="move"/>.
	/// </exception>
	/// <exception cref="FileNotFoundException"><i>path</i> not found.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// In most cases uses API <msdn>MoveFileEx</msdn>. It's fast, because don't need to copy files.
	/// In these cases copies/deletes: destination is on another drive; need to merge directories.
	/// When need to copy, does not copy security properties; sets default.
	/// Creates the destination directory if does not exist (see <see cref="createDirectory"/>).
	/// </remarks>
	public static void moveTo(string path, string newDirectory, FIfExists ifExists = FIfExists.Fail) {
		_FileOperation(_FileOp.Move, true, path, newDirectory, ifExists);
	}
	
	/// <summary>
	/// Copies file or directory.
	/// </summary>
	/// <param name="path">Full path.</param>
	/// <param name="newPath">
	/// Full path of the destination.
	/// <para>NOTE: It is not the new parent directory. See <see cref="copyTo"/>.</para>
	/// </param>
	/// <param name="ifExists"></param>
	/// <param name="copyFlags">Options used when copying directory.</param>
	/// <param name="fileFilter">Callback function that decides which files to copy when copying directory. See <see cref="enumerate(string, FEFlags, Func{FEFile, bool}, Func{FEFile, int}, Action{string})"/>. Note: this function uses <see cref="FEFlags.NeedRelativePaths"/>.</param>
	/// <param name="dirFilter">Callback function that decides which subdirectories to copy when copying directory. See <see cref="enumerate(string, FEFlags, Func{FEFile, bool}, Func{FEFile, int}, Action{string})"/>. Note: this function uses <see cref="FEFlags.NeedRelativePaths"/>.</param>
	/// <exception cref="ArgumentException"><i>path</i> or <i>newPath</i> is not full path (see <see cref="pathname.isFullPath"/>).</exception>
	/// <exception cref="FileNotFoundException"><i>path</i> not found.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// Uses API <msdn>CopyFileEx</msdn> and <msdn>CreateDirectoryEx</msdn>.
	/// On Windows 7 does not copy security properties; sets default.
	/// Does not copy symbolic links (silently skips, no exception) if this process is not running as administrator.
	/// Creates the destination directory if does not exist (see <see cref="createDirectory"/>).
	/// </remarks>
	public static void copy(string path, string newPath, FIfExists ifExists = FIfExists.Fail,
		FCFlags copyFlags = 0, Func<FEFile, bool> fileFilter = null, Func<FEFile, int> dirFilter = null
		) {
		_FileOperation(_FileOp.Copy, false, path, newPath, ifExists, copyFlags, fileFilter, dirFilter);
	}
	
	/// <summary>
	/// Copies file or directory into another directory.
	/// </summary>
	/// <param name="newDirectory">Full path of the new parent directory.</param>
	/// <exception cref="ArgumentException">
	/// - <i>path</i> or <i>newDirectory</i> is not full path (see <see cref="pathname.isFullPath"/>).
	/// - <i>path</i> is drive. To copy drive content, use <see cref="copy"/>.
	/// </exception>
	/// <exception cref="FileNotFoundException"><i>path</i> not found.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <inheritdoc cref="copy"/>
	public static void copyTo(string path, string newDirectory, FIfExists ifExists = FIfExists.Fail,
		FCFlags copyFlags = 0, Func<FEFile, bool> fileFilter = null, Func<FEFile, int> dirFilter = null
		) {
		_FileOperation(_FileOp.Copy, true, path, newDirectory, ifExists, copyFlags, fileFilter, dirFilter);
	}
	
	/// <summary>
	/// Deletes file or directory if exists.
	/// </summary>
	/// <returns><c>true</c> if deleted, <c>false</c> if failed (with flag <b>CanFail</b>), <c>null</c> if did not exist.</returns>
	/// <param name="path">Full path.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException"><i>path</i> is not full path (see <see cref="pathname.isFullPath"/>).</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// Does nothing if it does not exist (no exception).
	/// If directory, also deletes all its files and subdirectories. If fails to delete some, tries to delete as many as possible.
	/// Deletes read-only files too.
	/// Does not show any message boxes etc (confirmation, error, UAC consent, progress).
	/// 
	/// Some reasons why this function can fail:
	/// 1. The file is open (in any process). Or a file in the directory is open.
	/// 2. This process does not have security permissions to access or delete the file or directory or some of its descendants.
	/// 3. The directory is (or contains) the "current directory" (in any process).
	/// </remarks>
	public static bool? delete(string path, FDFlags flags = 0)
		=> _Delete(_PreparePath(path), flags);
	
	/// <summary>
	/// Deletes multiple files or/and directories.
	/// </summary>
	/// <returns><c>true</c> if deleted all, <c>false</c> if failed to delete all or some (with flag <b>CanFail</b>), <c>null</c> if none existed.</returns>
	/// <param name="paths">string array, <b>List</b> or other collection. Full paths.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException"><i>path</i> is not full path (see <see cref="pathname.isFullPath"/>).</exception>
	/// <exception cref="AggregateException">Failed to delete all or some items. The <b>AggregateException</b> object contains <b>AuException</b> for each failed-to-delete item.</exception>
	/// <remarks>
	/// This overload is faster when using Recycle Bin.
	/// If fails to delete some items specified in the list, deletes as many as possible.
	/// </remarks>
	public static bool? delete(IEnumerable<string> paths, FDFlags flags = 0) {
		if (flags.Has(FDFlags.RecycleBin)) {
			var a = new List<string>();
			foreach (var v in paths) {
				var s = _PreparePath(v);
				if (exists(s, true)) a.Add(s);
			}
			if (a.Count == 0) return null;
			if (_DeleteShell(null, true, a)) return true;
			Debug_.Print("_DeleteShell failed");
			//if (flags.Has(FDFlags.CanFail)) return false; //no, the shell API does not try to delete other items if fails to delete some
			//flags &= ~FDFlags.RecycleBin; //no
		}
		
		bool? R = null;
		if (flags.Has(FDFlags.CanFail)) {
			foreach (var v in paths) {
				switch (delete(v, flags)) {
				case true: R ??= true; break;
				case false: R = false; break;
				}
			}
			return R;
		} else {
			List<Exception> ae = null;
			foreach (var v in paths) {
				try { if (delete(v, flags) == true) R = true; }
				catch (AuException e1) { (ae ??= new()).Add(e1); }
			}
			return ae == null ? R : throw new AggregateException("Failed to delete.", ae);
		}
	}
	
	/// <summary>
	/// note: path must be normalized.
	/// </summary>
	static bool? _Delete(string path, FDFlags flags = 0) {
		bool canFail = flags.Has(FDFlags.CanFail);
		
		var type = ExistsAs_(path, true);
		if (type == FileIs_.NotFound) return null;
		if (type == FileIs_.AccessDenied) return canFail ? false : throw new AuException(0, $"*delete '{path}'");
		
		if (flags.Has(FDFlags.RecycleBin)) {
			if (_DeleteShell(path, true)) return true;
			if (canFail) return false;
			Debug_.Print("_DeleteShell failed");
		}
		
		int ec = 0;
		if (type == FileIs_.Directory) {
			var a = enumerate(path, FEFlags.AllDescendants | FEFlags.UseRawPath | FEFlags.IgnoreInaccessible).ToArray();
			//print.it(a);
			for (int i = a.Length; --i >= 0;) { //directories always are before their files, and will be empty when deleting in reverse
				var f = a[i];
				var at = f.Attributes;
				if (at.Has(FileAttributes.ReadOnly)) Api.SetFileAttributes(path, at & ~FileAttributes.ReadOnly);
				_DeleteL(f.FullPath, f.IsDirectory, canFail); //delete as many as possible
			}
			ec = _DeleteL(path, true, canFail);
			if (ec == 0) {
				//notify shell. Else, if it was open in Explorer, it shows an error message box.
				//Info: .NET does not notify; SHFileOperation does.
				ShellNotify_(Api.SHCNE_RMDIR, path);
				return true;
			}
			if (!canFail) {
				Debug_.Print("Using _DeleteShell.");
				if (_DeleteShell(path, false)) return true;
			}
		} else {
			ec = _DeleteL(path, type == FileIs_.NtfsLinkDirectory, canFail);
			if (ec == 0) return true;
		}
		
		if (exists(path, true)) return canFail ? false : throw new AuException(ec, $"*delete '{path}'");
		
		//info:
		//RemoveDirectory fails if not empty.
		//Directory.Delete fails if a descendant is read-only, etc.
		//Also both fail if a [now deleted] subfolder containing files was open in Explorer. Workaround: sleep/retry.
		//_DeleteShell usually does not have these problems. But it is very slow.
		//But all fail if it is current directory in any process. If in current process, _DeleteShell succeeds; it makes current directory = its parent.
		return true;
	}
	
	static int _DeleteL(string path, bool dir, bool canFail = false) {
		//print.it(dir, path);
		if (dir ? Api.RemoveDirectory(path) : Api.DeleteFile(path)) return 0;
		var ec = lastError.code;
		if (ec == Api.ERROR_ACCESS_DENIED) {
			var a = Api.GetFileAttributes(path);
			if (a != (FileAttributes)(-1) && 0 != (a & FileAttributes.ReadOnly)) {
				Api.SetFileAttributes(path, a & ~FileAttributes.ReadOnly);
				if (dir ? Api.RemoveDirectory(path) : Api.DeleteFile(path)) return 0;
				ec = lastError.code;
			}
		}
		if (!canFail) {
			for (int i = 1; (ec is Api.ERROR_SHARING_VIOLATION or Api.ERROR_LOCK_VIOLATION or Api.ERROR_DIR_NOT_EMPTY) && i <= 50; i++) {
				//ERROR_DIR_NOT_EMPTY: see comments above about Explorer. Also fails in other cases, eg when a file was opened in a web browser.
				Debug_.PrintIf(i > 1, (ec == Api.ERROR_DIR_NOT_EMPTY ? "ERROR_DIR_NOT_EMPTY when empty. Retry " : "ERROR_SHARING_VIOLATION. Retry ") + i.ToString());
				Thread.Sleep(15);
				if (dir ? Api.RemoveDirectory(path) : Api.DeleteFile(path)) return 0;
				ec = lastError.code;
			}
		}
		if (ec is Api.ERROR_FILE_NOT_FOUND or Api.ERROR_PATH_NOT_FOUND) return 0;
		Debug_.Print("_DeleteL failed. " + lastError.messageFor(ec) + "  " + path
			+ (dir ? ("   Children: " + string.Join(" | ", enumerate(path).Select(f => f.Name))) : null));
		return ec;
		
		//never mind: .NET also calls DeleteVolumeMountPoint if it is a mount point. Somehow only for recursed dirs.
		//	But I did not find in MSDN doc that need to do it before calling removedirectory. I think OS should unmount automatically.
		//	Tested on Win10, works without unmounting explicitly. Even Explorer updates its current folder without notification.
	}
	
	static bool _DeleteShell(string path, bool recycle, List<string> a = null) {
		if (a != null) path = string.Join("\0", a);
		if (wildex.hasWildcardChars(path)) throw new ArgumentException("*? not supported.");
		var x = new Api.SHFILEOPSTRUCT() { wFunc = Api.FO_DELETE };
		uint f = Api.FOF_NO_UI; //info: FOF_NO_UI includes 4 flags - noerrorui, silent, noconfirm, noconfirmmkdir
		if (recycle) f |= Api.FOF_ALLOWUNDO; else f |= Api.FOF_NO_CONNECTED_ELEMENTS;
		x.fFlags = (ushort)f;
		x.pFrom = path + "\0";
		x.hwnd = wnd.getwnd.root;
		var r = Api.SHFileOperation(x);
		//if(r != 0 || x.fAnyOperationsAborted) return false; //do not use fAnyOperationsAborted, it can be true even if finished to delete. Also, I guess it cannot be aborted because there is no UI, because we use FOF_SILENT to avoid deactivating the active window even when the UI is not displayed.
		//if(r != 0) return false; //after all, I don't trust this too
		//in some cases API returns 0 but does not delete. For example when path too long.
		if (a == null) {
			if (exists(path, true)) return false;
		} else {
			foreach (var v in a) if (exists(v, true)) return false;
		}
		return true;
		
		//Also tested IFileOperation, but it is even slower. Also it requires STA, which is not a big problem.
		
		//Unsuccessfully tried to add flag 'if cannot use Recycle Bin, show a Yes|No message box and delete or fail'.
		//	FOF_WANTNUKEWARNING does not work as it should, eg is ignored when the file is in a flash drive.
		//	It works only without FOF_NOCONFIRMATION, but then always shows confirmation if not disabled in RB Properties.
	}
	
	/// <summary>
	/// Creates new directory if does not exists.
	/// If need, creates missing parent/ancestor directories.
	/// </summary>
	/// <returns><c>true</c> if created new directory, <c>false</c> if the directory already exists. Throws exception if failed.</returns>
	/// <param name="path">Path of new directory.</param>
	/// <param name="templateDirectory">Optional path of a template directory from which to copy some properties. See API <msdn>CreateDirectoryEx</msdn>.</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <remarks>
	/// If the directory already exists, this function does nothing, and returns <c>false</c>.
	/// Else, at first it creates missing parent/ancestor directories, then creates the specified directory.
	/// To create the specified directory, calls API <msdn>CreateDirectory</msdn> or <msdn>CreateDirectoryEx</msdn> (if <i>templateDirectory</i> is not <c>null</c>).
	/// </remarks>
	public static bool createDirectory(string path, string templateDirectory = null) {
		return _CreateDirectory(path, templateDirectory: templateDirectory);
	}
	
	/// <summary>
	/// Creates parent directory for a new file, if does not exist.
	/// The same as <see cref="createDirectory"/>, just removes filename from <i>filePath</i>.
	/// </summary>
	/// <returns><c>true</c> if created new directory, <c>false</c> if the directory already exists. Throws exception if failed.</returns>
	/// <param name="filePath">Path of new file.</param>
	/// <exception cref="ArgumentException">Not full path. No filename.</exception>
	/// <exception cref="AuException">Failed.</exception>
	/// <example>
	/// <code><![CDATA[
	/// string path = @"D:\Test\new\test.txt";
	/// filesystem.createDirectoryFor(path);
	/// File.WriteAllText(path, "text"); //would fail if directory @"D:\Test\new" does not exist
	/// ]]></code>
	/// </example>
	public static bool createDirectoryFor(string filePath) {
		filePath = _PreparePath(filePath);
		var path = _RemoveFilename(filePath);
		return _CreateDirectory(path, pathIsPrepared: true);
	}
	
	/// <summary>
	/// Same as <b>createDirectoryFor</b>, but <i>filePath</i> must be prepared (<b>_PreparePath</b> or normalize).
	/// </summary>
	static bool _createDirectoryForPrepared(string filePath) {
		var path = _RemoveFilename(filePath);
		return _CreateDirectory(path, pathIsPrepared: true);
	}
	
	static bool _CreateDirectory(string path, bool pathIsPrepared = false, string templateDirectory = null) {
		if (exists(path, pathIsPrepared).Directory) return false;
		if (!pathIsPrepared) path = _PreparePath(path);
		if (templateDirectory != null) templateDirectory = _PreparePath(templateDirectory);
		
		var stack = new Stack<string>();
		var s = path;
		do {
			stack.Push(s);
			s = _RemoveFilename(s, true);
			if (s == null) throw new AuException($@"*create directory '{path}'. Drive not found.");
		} while (!exists(s, true).Directory);
		
		while (stack.Count > 0) {
			s = stack.Pop();
			int retry = 0;
			g1:
			bool ok = (templateDirectory == null || stack.Count > 0)
				? Api.CreateDirectory(s, default)
				: Api.CreateDirectoryEx(templateDirectory, s, default);
			if (!ok) {
				int ec = lastError.code;
				if (ec == Api.ERROR_ALREADY_EXISTS) continue;
				if (ec == Api.ERROR_ACCESS_DENIED && ++retry < 5) { Thread.Sleep(15); goto g1; } //sometimes access denied briefly, eg immediately after deleting the folder while its parent is open in Explorer. Now could not reproduce on Win10.
				throw new AuException(0, $@"*create directory '{path}'");
			}
		}
		return true;
	}
	
	internal static void ShellNotify_(uint @event, string path, string path2 = null) {
		//ThreadPool.QueueUserWorkItem(_ => Api.SHChangeNotify(@event, Api.SHCNF_PATH, path, path2)); //no, this process may end soon
		Api.SHChangeNotify(@event, Api.SHCNF_PATH, path, path2);
		//TODO3: test speed. If slow, use threadpool and the process exit event.
	}
	
	/// <summary>
	/// The same as <c>pathname.normalize(path)</c>.
	/// Expands environment variables, throws <b>ArgumentException</b> if not full path, normalizes, etc.
	/// </summary>
	/// <exception cref="ArgumentException">Not full path.</exception>
	static string _PreparePath(string path) {
		if (!pathname.isFullPathExpand(ref path)) throw new ArgumentException($"Not full path: '{path}'.");
		return pathname.Normalize_(path, noExpandEV: true);
	}
	
	/// <summary>
	/// Finds filename, eg <c>@"b.txt"</c> in <c>@"c:\a\b.txt"</c>.
	/// </summary>
	/// <exception cref="ArgumentException"><c>'\\'</c> not found or is at the end. If <i>noException</i>, instead returns -1.</exception>
	static int _FindFilename(string path, bool noException = false) {
		int R = path.FindLastAny(@"\/");
		if (R < 0 || R == path.Length - 1) {
			if (noException) return -1;
			throw new ArgumentException($"No filename in path: '{path}'.");
		}
		return R + 1;
	}
	
	/// <summary>
	/// Removes filename, eg <c>@"c:\a\b.txt"</c> -> <c>@"c:\a"</c>.
	/// </summary>
	/// <exception cref="ArgumentException"><c>'\\'</c> not found or is at the end. If <i>noException</i>, instead returns <c>null</c>.</exception>
	static string _RemoveFilename(string path, bool noException = false) {
		int i = _FindFilename(path, noException); if (i < 0) return null;
		return path[..--i];
	}
	
	/// <summary>
	/// Gets filename, eg <c>@"c:\a\b.txt"</c> -> <c>@"b.txt"</c>.
	/// </summary>
	/// <exception cref="ArgumentException"><c>'\\'</c> not found or is at the end. If <i>noException</i>, instead returns <c>null</c>.</exception>
	static string _GetFilename(string path, bool noException = false) {
		int i = _FindFilename(path, noException); if (i < 0) return null;
		return path[i..];
	}
	#endregion
	
	#region load, save
	
	/// <summary>
	/// This function can be used to safely open a file that may be temporarily locked (used by another process or thread). Waits while the file is locked.
	/// </summary>
	/// <returns>The return value of the lambda <i>f</i>.</returns>
	/// <param name="f">Lambda that calls a function that creates, opens or opens/reads/closes a file.</param>
	/// <param name="millisecondsTimeout">Wait max this number of milliseconds. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <exception cref="ArgumentOutOfRangeException"><i>millisecondsTimeout</i> less than -1.</exception>
	/// <exception cref="Exception">Exceptions thrown by the called function.</exception>
	/// <remarks>
	/// Calls the lambda and handles <b>IOException</b>. If the exception indicates that the file is locked, waits and retries in loop.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var s1 = File.ReadAllText(file); //unsafe. Exception if the file is locked.
	/// 
	/// var s2 = filesystem.waitIfLocked(() => File.ReadAllText(file)); //safe. Waits while the file is locked.
	/// 
	/// using(var f = filesystem.waitIfLocked(() => File.OpenText(file))) { //safe. Waits while the file is locked.
	/// 	var s3 = f.ReadToEnd();
	/// }
	/// 
	/// using(var f = filesystem.waitIfLocked(() => File.Create(file))) { //safe. Waits while the file is locked.
	/// 	f.WriteByte(1);
	/// }
	/// ]]></code>
	/// </example>
	public static T waitIfLocked<T>(Func<T> f, int millisecondsTimeout = 2000) {
		var w = new _LockedWaiter(millisecondsTimeout);
		g1:
		try { return f(); }
		catch (IOException e) when (w.ExceptionFilter(e)) { w.Sleep(); goto g1; }
	}
	
	/// <returns></returns>
	/// <example>
	/// <code><![CDATA[
	/// File.WriteAllText(file, "TEXT"); //unsafe. Exception if the file is locked.
	/// 
	/// filesystem.waitIfLocked(() => File.WriteAllText(file, "TEXT")); //safe. Waits while the file is locked.
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="waitIfLocked{T}(Func{T}, int)"/>
	public static void waitIfLocked(Action f, int millisecondsTimeout = 2000) {
		var w = new _LockedWaiter(millisecondsTimeout);
		g1:
		try { f(); }
		catch (IOException e) when (w.ExceptionFilter(e)) { w.Sleep(); goto g1; }
	}
	
	struct _LockedWaiter {
		int _timeout;
		long _t0;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public _LockedWaiter(int millisecondsTimeout) {
			if (millisecondsTimeout < -1) throw new ArgumentOutOfRangeException();
			_timeout = millisecondsTimeout;
			_t0 = perf.ms;
		}
		
		public bool ExceptionFilter(IOException e) => ExceptionFilter(e.HResult & 0xffff);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ExceptionFilter(int ec) {
			//print.it((uint)ec);
			switch (ec) {
			case Api.ERROR_SHARING_VIOLATION:
			case Api.ERROR_LOCK_VIOLATION:
			case Api.ERROR_USER_MAPPED_FILE:
			case Api.ERROR_UNABLE_TO_REMOVE_REPLACED: //ReplaceFile or File.Replace
				return _timeout < 0 || perf.ms - _t0 < _timeout;
			default: return false;
			}
		}
		
		public void Sleep() => Thread.Sleep(15);
	}
	
	/// <summary>
	/// Loads text file in a safer way.
	/// Uses <see cref="File.ReadAllText"/> and <see cref="waitIfLocked{T}(Func{T}, int)"/>.
	/// </summary>
	/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
	/// <param name="encoding">Text encoding in file (if there is no BOM). Default UTF-8.</param>
	/// <param name="lockedWaitMS">If cannot open the file because it is opened by another process etc, wait max this number of milliseconds. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <param name="missingWaitMS">If the file initially does not exist, wait max this number of milliseconds until exists. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A timeout is less than -1.</exception>
	/// <exception cref="TimeoutException"><i>missingWaitMS</i> is &gt; 0 and the file still does not exist after waiting.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="File.ReadAllText"/>.</exception>
	public static string loadText(string file, Encoding encoding = null, int lockedWaitMS = 2000, int missingWaitMS = 0) {
		file = _LoadIntro(file, missingWaitMS);
		return waitIfLocked(() => encoding == null ? File.ReadAllText(file) : File.ReadAllText(file, encoding), lockedWaitMS);
	}
	
	static string _LoadIntro(string file, int ms) {
		file = pathname.NormalizeMinimally_(file);
		if (ms != 0) {
			double t = ms > 0 ? ms / 1000d : ms == -1 ? 0d : throw new ArgumentOutOfRangeException();
			wait.until(t, () => exists(file, useRawPath: true));
		}
		return file;
	}
	
	/// <summary>
	/// Loads file in a safer way.
	/// Uses <see cref="File.ReadAllBytes(string)"/> and <see cref="waitIfLocked{T}(Func{T}, int)"/>.
	/// </summary>
	/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
	/// <param name="lockedWaitMS">If cannot open the file because it is opened by another process etc, wait max this number of milliseconds. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <param name="missingWaitMS">If the file initially does not exist, wait max this number of milliseconds until exists. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A timeout is less than -1.</exception>
	/// <exception cref="TimeoutException"><i>missingWaitMS</i> is &gt; 0 and the file still does not exist after waiting.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="File.ReadAllBytes(string)"/>.</exception>
	public static byte[] loadBytes(string file, int lockedWaitMS = 2000, int missingWaitMS = 0) {
		file = _LoadIntro(file, missingWaitMS);
		return waitIfLocked(() => File.ReadAllBytes(file), lockedWaitMS);
	}
	
	/// <summary>
	/// Loads file in a safer way.
	/// Uses <see cref="File.OpenRead(string)"/> and <see cref="waitIfLocked{T}(Func{T}, int)"/>.
	/// </summary>
	/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
	/// <param name="lockedWaitMS">If cannot open the file because it is opened by another process etc, wait max this number of milliseconds. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <param name="missingWaitMS">If the file initially does not exist, wait max this number of milliseconds until exists. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <exception cref="ArgumentException">Not full path.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A timeout is less than -1.</exception>
	/// <exception cref="TimeoutException"><i>missingWaitMS</i> is &gt; 0 and the file still does not exist after waiting.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="File.OpenRead(string)"/>.</exception>
	public static FileStream loadStream(string file, int lockedWaitMS = 2000, int missingWaitMS = 0) {
		file = _LoadIntro(file, missingWaitMS);
		return waitIfLocked(() => File.OpenRead(file), lockedWaitMS);
	}
	
	/// <summary>
	/// Writes any data to a file in a safe way, using a callback function.
	/// </summary>
	/// <param name="file">
	/// File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.
	/// If the file exists, this function overwrites it. If the directory does not exist, this function creates it.
	/// </param>
	/// <param name="writer">
	/// Callback function (lambda etc) that creates/writes/closes a temporary file. Its parameter is the full path of the temporary file, which normally does not exist.
	/// May be called multiple times, because this function retries if the file is locked or if the directory does not exist (if <i>writer</i> throws <b>DirectoryNotFoundException</b> exception).
	/// </param>
	/// <param name="backup">Create backup file named <i>file</i> + <c>"~backup"</c>.</param>
	/// <param name="tempDirectory">
	/// Directory for backup file and temporary file. If <c>null</c> or <c>""</c> - <i>file</i>'s directory. Can contain environment variables etc.
	/// Must be in the same drive as <i>file</i>. If the directory does not exist, this function creates it.</param>
	/// <param name="lockedWaitMS">If cannot open the file because it is opened by another process etc, wait max this number of milliseconds. Can be <see cref="Timeout.Infinite"/> (-1).</param>
	/// <exception cref="ArgumentException">Invalid <i>file</i> (eg not full path) or <i>lockedWaitMS</i> (less than -1).</exception>
	/// <exception cref="IOException">Failed to replace file.</exception>
	/// <exception cref="Exception">Exceptions of the function that actually writes data.</exception>
	/// <remarks>
	/// The file-write functions provided by .NET and Windows API are less reliable, because:
	/// 1. Fails if the file is temporarily open by another process or thread without sharing.
	/// 2. Can corrupt file data. If this thread, process, PC or disk dies while writing, may write only part of data or just make empty file. Usually it happens when PC is turned off incorrectly.
	/// 
	/// To protect from 1, this functions waits/retries if the file is temporarily open/locked, like <see cref="waitIfLocked"/>.
	/// To protect from 2, this function writes to a temporary file and renames/replaces the specified file using API <msdn>ReplaceFile</msdn>. Although not completely atomic, it ensures that file data is not corrupt; if cannot write all data, does not change existing file data.
	/// Also this function auto-creates directory if does not exist.
	/// 
	/// This function is slower. Speed can be important when saving many files.
	/// </remarks>
	public static void save(string file, Action<string> writer, bool backup = false, string tempDirectory = null, int lockedWaitMS = 2000) {
		Not_.Null(writer);
		_Save(file, writer, backup, tempDirectory, lockedWaitMS);
	}
	
	/// <summary>
	/// Writes text to a file in a safe way (like <see cref="save"/>), using <see cref="File.WriteAllText"/>.
	/// </summary>
	/// <param name="text">Text to write.</param>
	/// <param name="encoding">Text encoding in file. Default is UTF-8 without BOM.</param>
	/// <inheritdoc cref="save"/>
	public static void saveText(string file, string text, bool backup = false, string tempDirectory = null, int lockedWaitMS = 2000, Encoding encoding = null) {
		_Save(file, text ?? "", backup, tempDirectory, lockedWaitMS, encoding);
	}
	
	/// <summary>
	/// Writes data to a file in a safe way (like <see cref="save"/>), using <see cref="File.WriteAllBytes"/>.
	/// </summary>
	/// <param name="bytes">Data to write.</param>
	/// <inheritdoc cref="save"/>
	public static void saveBytes(string file, byte[] bytes, bool backup = false, string tempDirectory = null, int lockedWaitMS = 2000) {
		Not_.Null(bytes);
		_Save(file, bytes, backup, tempDirectory, lockedWaitMS);
	}
	
	static void _Save(string file, object data, bool backup, string tempDirectory, int lockedWaitMS, Encoding encoding = null) {
		file = _PreparePath(file);
		
		string s1 = file;
		if (!tempDirectory.NE()) {
			s1 = _PreparePath(tempDirectory);
			_CreateDirectory(s1, pathIsPrepared: true);
			s1 = pathname.combine(s1, pathname.getName(file));
		}
		
		_createDirectoryForPrepared(file);
		
		string temp = s1 + "~temp";
		string back = s1 + "~backup"; //always use the backup parameter, then ERROR_UNABLE_TO_REMOVE_REPLACED is far not so frequent, etc
		
		var w = new _LockedWaiter(lockedWaitMS);
		g1:
		try {
			switch (data) {
			case string text:
				//File.WriteAllText(temp, text, encoding ?? Encoding.UTF8); //no, it saves with BOM
				if (encoding != null) File.WriteAllText(temp, text, encoding);
				else File.WriteAllText(temp, text);
				break;
			case byte[] bytes:
				File.WriteAllBytes(temp, bytes);
				break;
			case Action<string> func:
				func(temp);
				break;
			}
		}
		catch (IOException e) when (w.ExceptionFilter(e)) { w.Sleep(); goto g1; }
		
		w = new _LockedWaiter(lockedWaitMS);
		g2:
		string es = null;
		if (exists(file, true).File) {
			if (!Api.ReplaceFile(file, temp, back, 6)) es = "save"; //random ERROR_UNABLE_TO_REMOVE_REPLACED; _LockedWaiter knows it
			else if (backup) ShellNotify_(Api.SHCNE_RENAMEITEM, temp, file); //without it Explorer shows 2 files with filename of temp
			else if (!Api.DeleteFile(back)) Debug_.PrintNativeError(); //maybe should wait/retry if failed, but never noticed
		} else {
			if (!Api.MoveFileEx(temp, file, Api.MOVEFILE_REPLACE_EXISTING)) es = "create";
		}
		if (es != null) {
			int ec = lastError.code;
			if (w.ExceptionFilter(ec)) { w.Sleep(); goto g2; }
			throw new IOException($"Failed to {es} file '{file}'. {lastError.messageFor(ec)}", ec);
		}
	}
	
	#endregion
}
