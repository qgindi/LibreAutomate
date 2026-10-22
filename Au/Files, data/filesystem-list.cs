using System.IO.Enumeration;

namespace Au;

partial class filesystem {
	static object _List(FLReturn? returnKind, bool? files, string directory, bool recurse, Glob glob, FLFlags flags) {
		if (!Path.IsPathFullyQualified(directory))
			throw new ArgumentException(directory is [_, ':'] ? $@"Must be {directory}\" : "Not full path");
		
		var enOpt = new EnumerationOptions {
			AttributesToSkip = flags.Has(FLFlags.SkipHidden) ? FileAttributes.Hidden : 0, //default Hidden|System
			BufferSize = 10000, //faster than the default 4K
			IgnoreInaccessible = flags.Has(FLFlags.IgnoreInaccessible), //default true
			RecurseSubdirectories = recurse,
			//rejected: parameter for MaxRecursionDepth. Rarely used.
		};
		
		bool skipHiddenSystem = flags.Has(FLFlags.SkipHiddenSystem),
			skipSome = recurse && !skipHiddenSystem && directory is [.., ':', '\\' or '/'],
			useShouldInclude = files != null || skipHiddenSystem,
			useShouldRecurse = recurse && (skipHiddenSystem || skipSome || !flags.Has(FLFlags.RecurseLinks));
		
		bool _ShouldInclude(ref FileSystemEntry e) {
			if (e.IsDirectory == files) return false;
			if (skipHiddenSystem && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) return false;
			return true;
		}
		
		bool _ShouldRecurse(ref FileSystemEntry e) {
			if (skipHiddenSystem && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) return false;
			
			if (!flags.Has(FLFlags.RecurseLinks) && e.Attributes.Has(FileAttributes.ReparsePoint) && filesystem.IsNtfsLink_(e.ToFullPath())) return false;
			
			//skip Recycle Bin etc. Useless, access denied.
			if (skipSome && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System) && e.IsDirectory && e.Directory.SequenceEqual(e.RootDirectory))
				if (0 != e.FileName.Eq(true,
					"$Recycle.Bin",
					"System Volume Information", //access denied
					"Recovery", //access denied
					"Documents and Settings" //link, access denied
				)) return false;
			
			return true;
		}
		
		if (returnKind == null) {
			IEnumerable<FileSystemInfo> r = files switch {
				true => new FileSystemEnumerable<FileInfo>(directory, static (ref e) => (FileInfo)e.ToFileSystemInfo(), enOpt) {
					ShouldIncludePredicate = useShouldInclude ? _ShouldInclude : null,
					ShouldRecursePredicate = useShouldRecurse ? _ShouldRecurse : null
				},
				false => new FileSystemEnumerable<DirectoryInfo>(directory, static (ref e) => (DirectoryInfo)e.ToFileSystemInfo(), enOpt) {
					ShouldIncludePredicate = useShouldInclude ? _ShouldInclude : null,
					ShouldRecursePredicate = useShouldRecurse ? _ShouldRecurse : null
				},
				_ => new FileSystemEnumerable<FileSystemInfo>(directory, static (ref e) => e.ToFileSystemInfo(), enOpt) {
					ShouldIncludePredicate = useShouldInclude ? _ShouldInclude : null,
					ShouldRecursePredicate = useShouldRecurse ? _ShouldRecurse : null
				},
			};
			return glob?.Filter(r, directory) ?? r;
		} else if (returnKind == FLReturn.Paths) {
			IEnumerable<string> r = new FileSystemEnumerable<string>(directory, static (ref e) => e.ToSpecifiedFullPath(), enOpt) {
				ShouldIncludePredicate = useShouldInclude ? _ShouldInclude : null,
				ShouldRecursePredicate = useShouldRecurse ? _ShouldRecurse : null
			};
			return glob?.Filter(r, directory) ?? r;
		} else if (returnKind == FLReturn.Names && glob == null) {
			IEnumerable<string> r = new FileSystemEnumerable<string>(directory, static (ref e) => e.FileName.ToString(), enOpt) {
				ShouldIncludePredicate = useShouldInclude ? _ShouldInclude : null,
				ShouldRecursePredicate = useShouldRecurse ? _ShouldRecurse : null
			};
			return r;
		} else { //RelativePaths, or Names+glob
			static string _RelativePath(ref FileSystemEntry e) {
				int i = e.RootDirectory.Length;
				if (i >= e.Directory.Length) return e.FileName.ToString();
				if (e.Directory[i] is '\\' or '/') i++; //root is C:\dir, else C:/
				return Path.Join(e.Directory[i..], e.FileName);
			}
			
			IEnumerable<string> r = new FileSystemEnumerable<string>(directory, _RelativePath, enOpt) {
				ShouldIncludePredicate = useShouldInclude ? _ShouldInclude : null,
				ShouldRecursePredicate = useShouldRecurse ? _ShouldRecurse : null
			};
			if (returnKind == FLReturn.Names) {
				return glob.Filter(r, null).Select(static o => Path.GetFileName(o));
			} else {
				return glob?.Filter(r, null) ?? r;
			}
		}
	}
	
	/// <summary>
	/// Enumerates files and directories in the specified directory, and gets their properties.
	/// </summary>
	/// <param name="directory">Directory path. Must be full path without environment variables; see <see cref="Path2.Normalize"/>.</param>
	/// <param name="recurse">
	/// <br/>• <c>true</c> - enumerate all descendants.
	/// <br/>• <c>false</c> - enumerate only direct children.
	/// </param>
	/// <param name="glob">Can be used to specify paths to include or/and exclude. Glob patterns must be relative to <i>directory</i>. Example: <c>new(["*.txt", "**/*.cs"], ["notThisDir/", "**/*.tmp"])</c>.</param>
	/// <param name="flags"></param>
	/// <returns>An enumerable collection of file system info objects. Paths are normalized (may not start with <c>directory</c>); to get relative paths can be used <see cref="Path.GetRelativePath"/>.</returns>
	public static IEnumerable<FileSystemInfo> listAll(string directory, bool recurse, Glob glob = null, FLFlags flags = 0)
		=> _List(null, null, directory, recurse, glob, flags) as IEnumerable<FileSystemInfo>;
	
	/// <summary>
	/// Enumerates files (but not directories) in the specified directory, and gets their properties.
	/// </summary>
	/// <inheritdoc cref="listAll(string, bool, Glob, FLFlags)"/>
	public static IEnumerable<FileInfo> listFiles(string directory, bool recurse, Glob glob = null, FLFlags flags = 0)
		=> _List(null, true, directory, recurse, glob, flags) as IEnumerable<FileInfo>;
	
	/// <summary>
	/// Enumerates directories in the specified directory, and gets their properties.
	/// </summary>
	/// <inheritdoc cref="listAll(string, bool, Glob, FLFlags)"/>
	public static IEnumerable<DirectoryInfo> listDirectories(string directory, bool recurse, Glob glob = null, FLFlags flags = 0)
		=> _List(null, false, directory, recurse, glob, flags) as IEnumerable<DirectoryInfo>;
	
	/// <summary>
	/// Enumerates files and directories in the specified directory, and gets their paths or names.
	/// </summary>
	/// <param name="format">Return full paths, filenames or relative paths.</param>
	/// <returns>An enumerable collection of paths or names. Full paths are not normalized (always start with <c>directory</c>).</returns>
	/// <inheritdoc cref="listAll(string, bool, Glob, FLFlags)"/>
	public static IEnumerable<string> listAll(FLReturn format, string directory, bool recurse, Glob glob = null, FLFlags flags = 0)
		=> _List(format, null, directory, recurse, glob, flags) as IEnumerable<string>;
	
	/// <summary>
	/// Enumerates files (but not directories) in the specified directory, and gets their paths or names.
	/// </summary>
	/// <inheritdoc cref="listAll(FLReturn, string, bool, Glob, FLFlags)"/>
	public static IEnumerable<string> listFiles(FLReturn format, string directory, bool recurse, Glob glob = null, FLFlags flags = 0)
		=> _List(format, true, directory, recurse, glob, flags) as IEnumerable<string>;
	
	/// <summary>
	/// Enumerates directories in the specified directory, and gets their paths or names.
	/// </summary>
	/// <inheritdoc cref="listAll(FLReturn, string, bool, Glob, FLFlags)"/>
	public static IEnumerable<string> listDirectories(FLReturn format, string directory, bool recurse, Glob glob = null, FLFlags flags = 0)
		=> _List(format, false, directory, recurse, glob, flags) as IEnumerable<string>;
}
