namespace Au {
	partial class filesystem {
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
		/// Receives the subdirectory path. Can call <see cref="lastError.code"/> and throw an exception. If does not throw, the enumeration continues as if the directory is empty.
		/// If <i>errorHandler</i> not used, then <see cref="enumerate"/> throws exception. See also: flag <see cref="FEFlags.IgnoreInaccessible"/>.
		/// </param>
		/// <exception cref="ArgumentException"><i>directoryPath</i> is invalid path or not full path.</exception>
		/// <exception cref="DirectoryNotFoundException"><i>directoryPath</i> directory does not exist.</exception>
		/// <exception cref="AuException">Failed to get children of <i>directoryPath</i> or of a subdirectory.</exception>
		/// <remarks>
		/// Uses API <ms>FindFirstFile</ms>.
		/// 
		/// By default gets only direct children. Use flag <see cref="FEFlags.AllDescendants"/> to get all descendants.
		/// 
		/// The paths that this function gets are normalized, ie may not start with exact <i>directoryPath</i> string. Expanded environment variables (see <see cref="Path2.Expand"/>), <c>".."</c>, DOS path etc. Paths longer than <see cref="Path2.MaxSafeDirectoryPathLength"/> have <c>@"\\?\"</c> prefix (see <see cref="Path2.PrefixLongPathIfNeed"/>).
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
		[Obsolete]
		public static IEnumerable<FEFile> enumerate(string directoryPath, FEFlags flags = 0,
					Func<FEFile, bool> fileFilter = null,
					Func<FEFile, int> dirFilter = null,
					Action<string> errorHandler = null
					) {
			//tested 2021-04-30 with .NET 5: much faster than Directory.EnumerateX. Faster JIT, and then > 2 times faster.
			//tested later with .NET 6-10: ~20% slower than Directory.EnumerateX. Now .NET uses NtQueryDirectoryFile.
			
			//note: stuck at ~340000 files when recursively enumerating C:\ProgramData\Microsoft\Windows\Containers\Layers.
			//	Even can't end the task process normally (access denied error).
			//	Actually it just becomes very slow, and maybe would complete if I waited long enough. 
			//	Directory.EnumerateFileSystemEntries stuck at ~61000 files.
			//	The TreeSize app at first it seems stuck too, but after long time completes and reports 752000 files.
			
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
						var path2 = ((path.Length <= Path2.MaxSafeDirectoryPathLength - 2) ? path : Path2.PrefixLongPath(path)) + @"\*";
#if TEST_FINDFIRSTFILEEX
					hfind = Api.FindFirstFileEx(path2, Api.FINDEX_INFO_LEVELS.FindExInfoBasic, out d, 0, default, 0);
					//speed: FindFirstFileEx 0-2 % slower. FindExInfoBasic makes 0-2% faster. FIND_FIRST_EX_LARGE_FETCH makes 1-50% slower.
#else
						hfind = Api.FindFirstFile(path2, out d);
#endif
						if (hfind == -1) {
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
					var fp2 = Path2.PrefixLongPathIfNeed(fullPath);
					
					var r = new FEFile(name, fp2, d, stack.Count); //never mind, don't need for dirs if no dirFilter and is flag OnlyFiles
					
					if (isDir) {
						int inc = dirFilter != null ? dirFilter(r) : (flags.Has(FEFlags.OnlyFiles) ? 0 : 1) | (flags.Has(FEFlags.AllDescendants) ? 2 : 0);
						if (0 != (1 & inc)) yield return r;
						if (0 == (2 & inc)) continue;
						if (!flags.Has(FEFlags.RecurseNtfsLinks) && d.IsNtfsLink) continue;
						
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
		/// <param name="flags">Flags. The function also adds flag <c>OnlyFiles</c>.</param>
		/// <exception cref="ArgumentException">
		/// <i>directoryPath</i> is invalid path or not full path.
		/// Invalid <i>pattern</i> (<c>"**options "</c> or regular expression).
		/// </exception>
		/// <exception cref="DirectoryNotFoundException"><i>directoryPath</i> directory does not exist.</exception>
		/// <exception cref="AuException">Failed to get children of <i>directoryPath</i> or of a subdirectory.</exception>
		/// <inheritdoc cref="enumerate(string, FEFlags, Func{FEFile, bool}, Func{FEFile, int}, Action{string})"/>
		[Obsolete]
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
			if (pattern == null) return filesystem.enumerate(directoryPath, flags, _ => false);
			wildex w = pattern;
			int enumChildren = flags.Has(FEFlags.AllDescendants) ? 2 : 0;
			return filesystem.enumerate(directoryPath, flags, _ => false, d => (w.Match(d.Name) ? 1 : 0) | enumChildren);
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// flags for <see cref="filesystem.enumerate"/>.
	/// </summary>
	[Obsolete]
	[Flags]
	public enum FEFlags {
		/// <summary>
		/// Enumerate all descendants, not only direct children. Also known as "recurse subdirectories".
		/// </summary>
		AllDescendants = 1,
		
		/// <summary>
		/// Also enumerate target directories of NTFS links, such as symbolic links and mount points. Use with <c>AllDescendants</c>.
		/// </summary>
		RecurseNtfsLinks = 2,
		
		/// <summary>
		/// Skip files and subdirectories that have <c>Hidden</c> attribute.
		/// </summary>
		SkipHidden = 4,
		
		/// <summary>
		/// Skip files and subdirectories that have <c>Hidden</c> and <c>System</c> attributes (both).
		/// These files/directories usually are created and used only by the operating system. Drives usually have several such directories. Another example - thumbnail cache files.
		/// Without this flag the function skips only these hidden-system root directories when enumerating a drive: <c>$Recycle.Bin</c>, <c>System Volume Information</c>, <c>Recovery</c>. If you want to include them too, use network path of the drive, for example <c>@"\\localhost\D$\"</c> for <c>D</c> drive.
		/// </summary>
		SkipHiddenSystem = 8, //note: must match FCFlags
		
		/// <summary>
		/// If fails to get the contents of the directory or a subdirectory because of its security settings, assume that the [sub]directory is empty.
		/// Without this flag then throws exception or calls <i>errorHandler</i>.
		/// </summary>
		IgnoreInaccessible = 0x10, //note: must match FCFlags
		
		/// <summary>
		/// Get only files and not subdirectories.
		/// Note: the <i>dirFilter</i> callback function is called just to ask whether to include children.
		/// </summary>
		OnlyFiles = 0x20,
		
		/// <summary>
		/// Don't call <see cref="Path2.Normalize"/> and don't throw exception for non-full path.
		/// </summary>
		UseRawPath = 0x40,
		
		/// <summary>
		/// Let <see cref="FEFile.Name"/> be path relative to the specified directory path. Like <c>@"\name.txt"</c> or <c>@"\subdirectory\name.txt"</c> instead of <c>"name.txt"</c>.
		/// </summary>
		NeedRelativePaths = 0x80,
		
		//rejected. Rarely used. Can use FileSystemRedirection, it's public.
		///// <summary>
		///// Temporarily disable file system redirection in this thread of this 32-bit process running on 64-bit Windows.
		///// Then you can enumerate the 64-bit System32 folder in your 32-bit process.
		///// Uses API <ms>Wow64DisableWow64FsRedirection</ms>.
		///// For vice versa (in 64-bit process enumerate the 32-bit System folder), instead use path <c>folders.SystemX86</c>.
		///// </summary>
		//DisableRedirection = 0x100,
	}
	
	/// <summary>
	/// Contains name and other main properties of a file or subdirectory retrieved by <see cref="filesystem.enumerate"/>.
	/// The values are not changed after creating the variable.
	/// </summary>
	[Obsolete]
	public class FEFile {
		internal FEFile(string name, string fullPath, in Api.WIN32_FIND_DATA d, int level) {
			Name = name; FullPath = fullPath;
			Attributes = d.dwFileAttributes;
			Size = (long)d.nFileSizeHigh << 32 | d.nFileSizeLow;
			LastWriteTimeUtc = DateTime.FromFileTimeUtc(d.ftLastWriteTime); //fast, sizeof 8
			CreationTimeUtc = DateTime.FromFileTimeUtc(d.ftCreationTime);
			_level = (short)level;
			ReparseTag = d.dwReserved0;
		}
		
		/// <summary>
		/// Gets file name. Or relative path if used <see cref="FEFlags.NeedRelativePaths"/>.
		/// </summary>
		public string Name { get; }
		
		/// <summary>
		/// Gets full path.
		/// </summary>
		public string FullPath { get; }
		
		/// <summary>
		/// Gets filename extension. Returns <c>""</c> if directory.
		/// </summary>
		public string Extension => IsDirectory ? "" : Path.GetExtension(Name); //note: if null for directory, then OrderBy throws exception
		
		/// <summary>
		/// Returns file size. For directories it is usually 0.
		/// </summary>
		public long Size { get; }
		
		///
		public DateTime LastWriteTimeUtc { get; }
		
		///
		public DateTime CreationTimeUtc { get; }
		
		///
		public FileAttributes Attributes { get; }
		
		/// <summary>
		/// It is a directory. Or a NTFS link to a directory (see <see cref="IsNtfsLink"/>).
		/// </summary>
		public bool IsDirectory { get { return (Attributes & FileAttributes.Directory) != 0; } }
		
		/// <summary>
		/// Descendant level.
		/// 0 if direct child of the directory (<i>directoryPath</i>), 1 if child of child, and so on.
		/// </summary>
		public int Level {
			get => _level;
			internal set { _level = (short)value; }
		}
		short _level;
		
		/// <summary>
		/// It is a NTFS link, such as symbolic link or mount point. Don't confuse with shell links (shortcuts).
		/// </summary>
		public bool IsNtfsLink => Attributes.Has(FileAttributes.ReparsePoint) && 0 != (ReparseTag & 0x20000000);
		
		/// <summary>
		/// <ms>WIN32_FIND_DATA</ms><c>.dwReserved0</c>.
		/// </summary>
		public uint ReparseTag { get; }
		
		/// <summary>
		/// Returns <see cref="FullPath"/>.
		/// </summary>
		public override string ToString() => FullPath;
	}
}
