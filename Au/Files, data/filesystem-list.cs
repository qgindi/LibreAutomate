//#define FSI_EXTRA

using System.IO.Enumeration;

namespace Au.Types;

/// <summary>
/// The return type of <see cref="filesystem.list"/>.
/// Use like <c>var paths = filesystem.list(@"C:\path").All;</c>.
/// </summary>
public class FLQuery {
	string _directory;
	PathFilter _filter;
	FLFlags _flags;
	
	///
	public FLQuery(string directory, PathFilter filter = null, FLFlags flags = 0) {
		if (filter != null && !filter.CheckPatterns_(flags.Has(FLFlags.Recurse))) filter = null;
		_directory = directory;
		_filter = filter;
		_flags = flags;
	}
	
	/// <summary>
	/// Enumerates files and/or directories in the directory, using a callback function to create objects of desired type.
	/// </summary>
	/// <param name="transform">A function that converts <see cref="FileSystemEntry"/> to <c>TResult</c>. See <see cref="FileSystemEnumerable{TResult}"/>.</param>
	/// <param name="files">
	/// What kind of file system entries to include:
	/// <br/>• <c>true</c> - files only.
	/// <br/>• <c>false</c> - directories only.
	/// <br/>• <c>null</c> - all.
	/// </param>
	/// <returns>An enumerable collection of objects created by the <c>transform</c> function.</returns>
	/// <exception cref="ArgumentException"><i>directory</i> is not full path.</exception>
	/// <exception cref="DirectoryNotFoundException">The specified directory does not exist.</exception>
	/// <exception cref="IOException">Failed. For example <i>directory</i> is a file.</exception>
	/// <exception cref="UnauthorizedAccessException">A directory is inaccessible. See <see cref="FLFlags.IgnoreInaccessible"/>.</exception>
	/// <remarks>
	/// Uses <see cref="FileSystemEnumerable{TResult}"/>.
	/// 
	/// The order of results probably is breadth-first, but it's not documented.
	/// 
	/// The returned paths are normalized, ie may not start with exact <i>directory</i> string. If you need relative path, use property <see cref="FLPath.RelativePath"/>.
	/// 
	/// For NTFS links (symbolic links, mount points) gets link info, not target info.
	/// 
	/// These errors are ignored (the function works as if the [sub]directory is empty):
	/// 1. Missing target directory of a NTFS link.
	/// 2. If used flag <see cref="FLFlags.IgnoreInaccessible"/> - access denied.
	/// </remarks>
	public IEnumerable<TResult> Enumerate<TResult>(FileSystemEnumerable<TResult>.FindTransform transform, bool? files) {
		if (!Path.IsPathFullyQualified(_directory))
			throw new ArgumentException(_directory is [_, ':'] ? $@"Must be {_directory}\" : "Not full path");
		
		bool recurse = _flags.Has(FLFlags.Recurse);
		
		var enOpt = new EnumerationOptions {
			AttributesToSkip = (_flags.Has(FLFlags.SkipHidden) ? FileAttributes.Hidden : 0) | (_filter?.AttributesToSkip ?? 0), //default Hidden|System
			BufferSize = 10000, //faster than the default 4K
			IgnoreInaccessible = _flags.Has(FLFlags.IgnoreInaccessible), //default true
			RecurseSubdirectories = recurse,
		};
		if (_filter != null) enOpt.MaxRecursionDepth = _filter.MaxRecursionDepth;
		
		bool skipHiddenSystem = _flags.Has(FLFlags.SkipHiddenSystem),
			skipSome = recurse && !skipHiddenSystem && _directory is [.., ':', '\\' or '/'],
			useShouldInclude = files != null || skipHiddenSystem || _filter != null,
			useShouldRecurse = recurse && (skipHiddenSystem || skipSome || !_flags.Has(FLFlags.RecurseLinks) || _filter != null);
		
		bool _ShouldInclude(ref FileSystemEntry e) {
			if (e.IsDirectory == files) return false;
			if (skipHiddenSystem && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) return false;
			if (_filter?.ShouldInclude(ref e) == false) return false;
			return true;
		}
		
		bool _ShouldRecurse(ref FileSystemEntry e) {
			if (skipHiddenSystem && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) return false;
			if (!_flags.Has(FLFlags.RecurseLinks) && e.Attributes.Has(FileAttributes.ReparsePoint) && filesystem.IsNtfsLink_(e.ToFullPath())) return false;
			
			//skip ?:\$Recycle.Bin
			if (skipSome && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System) && e.IsDirectory && e.Directory.SequenceEqual(e.RootDirectory))
				if (e.FileName.Eqi("$Recycle.Bin")) return false;
			
			if (_filter?.ShouldRecurse(ref e) == false) return false;
			return true;
		}
		
		return new FileSystemEnumerable<TResult>(_directory, transform, enOpt) {
			ShouldIncludePredicate = useShouldInclude ? _ShouldInclude : null,
			ShouldRecursePredicate = useShouldRecurse ? _ShouldRecurse : null
		};
	}
	
	#region infos
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their properties.
	/// </summary>
	/// <returns>An enumerable collection of file or directory info objects.</returns>
	/// <inheritdoc cref="Enumerate"/>
	public IEnumerable<FileSystemInfo> EnumerateAllInfos() => Enumerate(_CreateFSI<FileSystemInfo>, null);
	
	/// <summary>
	/// Enumerates files in the directory. Gets their properties.
	/// </summary>
	/// <returns>An enumerable collection of file info objects.</returns>
	/// <inheritdoc cref="Enumerate"/>
	public IEnumerable<FileInfo> EnumerateFileInfos() => Enumerate(_CreateFSI<FileInfo>, true);
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their properties.
	/// </summary>
	/// <returns>An enumerable collection of directory info objects.</returns>
	/// <inheritdoc cref="Enumerate"/>
	public IEnumerable<DirectoryInfo> EnumerateDirectoryInfos() => Enumerate(_CreateFSI<DirectoryInfo>, false);
	
	/// <returns>An array of file or directory info objects.</returns>
	/// <inheritdoc cref="EnumerateAllInfos"/>
	public FileSystemInfo[] AllInfos => EnumerateAllInfos().ToArray();
	
	/// <returns>An array of file info objects.</returns>
	/// <inheritdoc cref="EnumerateFileInfos"/>
	public FileInfo[] FileInfos => EnumerateFileInfos().ToArray();
	
	/// <returns>An array of directory info objects.</returns>
	/// <inheritdoc cref="EnumerateDirectoryInfos"/>
	public DirectoryInfo[] DirectoryInfos => EnumerateDirectoryInfos().ToArray();
	
	static T _CreateFSI<T>(ref FileSystemEntry e) where T : FileSystemInfo
#if FSI_EXTRA
		=> (T)FSIExtra_.CreateFSI(ref e);
#else
		=> (T)e.ToFileSystemInfo();
#endif
	#endregion
	
	#region FLPath
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their paths and some other properties.
	/// </summary>
	/// <returns>An enumerable collection of structs containing path (assign to a string to get it) and several other properties (name etc).</returns>
	/// <inheritdoc cref="Enumerate"/>
	public IEnumerable<FLPath> EnumerateAll() => Enumerate(FLPath.Transform_, null);
	
	/// <summary>
	/// Enumerates files in the directory. Gets their paths and some other properties.
	/// </summary>
	/// <inheritdoc cref="EnumerateAll"/>
	public IEnumerable<FLPath> EnumerateFiles() => Enumerate(FLPath.Transform_, true);
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their paths and some other properties.
	/// </summary>
	/// <inheritdoc cref="EnumerateAll"/>
	public IEnumerable<FLPath> EnumerateDirectories() => Enumerate(FLPath.Transform_, false);
	
	/// <returns>An array of structs containing path (assign to a string to get it) and several other properties (name etc).</returns>
	/// <inheritdoc cref="EnumerateAll"/>
	public FLPath[] All => EnumerateAll().ToArray();
	
	/// <inheritdoc cref="All" path="/returns"/>
	/// <inheritdoc cref="EnumerateFiles"/>
	public FLPath[] Files => EnumerateFiles().ToArray();
	
	/// <inheritdoc cref="All" path="/returns"/>
	/// <inheritdoc cref="EnumerateDirectories"/>
	public FLPath[] Directories => EnumerateDirectories().ToArray();
	#endregion
	
	#region paths
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their paths as string.
	/// </summary>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] AllPaths => Enumerate(_GetPath, null).ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their paths as string.
	/// </summary>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] FilePaths => Enumerate(_GetPath, true).ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their paths as string.
	/// </summary>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] DirectoryPaths => Enumerate(_GetPath, false).ToArray();
	
	private static string _GetPath(ref FileSystemEntry entry) => entry.ToFullPath();
	#endregion
	
	#region names
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their filenames.
	/// </summary>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] AllNames => Enumerate(_GetName, null).ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their filenames.
	/// </summary>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] FileNames => Enumerate(_GetName, true).ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their filenames.
	/// </summary>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] DirectoryNames => Enumerate(_GetName, false).ToArray();
	
	private static string _GetName(ref FileSystemEntry entry) => entry.FileName.ToString();
	#endregion
}

/// <summary>
/// The return type of some file system enumeration functions.
/// This lightweight type contains a file path and also provides some other commonly used properties. Has implicit conversion to string (path).
/// Can be used like <c>foreach (var v in filesystem.list(directory).All) print.it(v, v.Name, v.IsDirectory);</c>.
/// </summary>
public struct FLPath {
	string _path;
	ushort _nameOffset, _relativeOffset;
	FileAttributes _attr;
	
	internal static FLPath Transform_(ref FileSystemEntry e) {
		var path = e.ToFullPath();
		return new FLPath {
			_path = path,
			_nameOffset = checked((ushort)(path.Length - e.FileName.Length)),
			_relativeOffset = checked((ushort)(e.RelativeOffset_)),
			_attr = e.Attributes
		};
	}
	
	/// <summary>
	/// Full path.
	/// </summary>
	public string Path => _path;
	
	/// <summary>
	/// Filename.
	/// </summary>
	public string Name => _path[_nameOffset..];
	
	/// <summary>
	/// Relative path.
	/// </summary>
	public string RelativePath => _path[_relativeOffset..];
	
	internal int RelativeOffset_ => _relativeOffset;
	
	/// <summary>
	/// Attributes.
	/// </summary>
	public FileAttributes Attributes => _attr;
	
	/// <summary>
	/// Returns <c>true</c> if it's a directory.
	/// </summary>
	public bool IsDirectory => _attr.Has(FileAttributes.Directory);
	
	/// <summary>
	/// Returns <c>true</c> if it's a NTFS link (symbolic link, mount point etc).
	/// </summary>
	public bool IsNtfsLink => _attr.Has(FileAttributes.ReparsePoint) && filesystem.IsNtfsLink_(_path);
	
	/// <summary>
	/// Name as span.
	/// </summary>
	public RStr NameSpan => _path.AsSpan(_nameOffset);
	
	/// <summary>
	/// Relative path as span.
	/// </summary>
	public RStr RelativePathSpan => _path.AsSpan(_relativeOffset);
	
	/// <summary>
	/// The number of intermediate directories from <c>filesystem.list</c> <i>directory</i>.
	/// </summary>
	public int RelativeDepth => RelativePathSpan.Count('\\');
	
	/// <summary>
	/// Implicit conversion to string. Returns full path.
	/// </summary>
	public static implicit operator string(FLPath p) => p._path;
	
	/// <summary>
	/// Returns full path.
	/// </summary>
	public override string ToString() => _path;
}

/// <summary>
/// Flags for file system enumeration methods.
/// </summary>
[Flags]
public enum FLFlags {
	/// <summary>
	/// Get all descendants, not just direct children.
	/// </summary>
	Recurse = 1,
	
	/// <summary>
	/// Also enumerate target directories of NTFS links (symbolic links, mount points etc). Use with <c>Recurse</c>.
	/// </summary>
	RecurseLinks = 2,
	
	/// <summary>
	/// Skip files and subdirectories that have <c>Hidden</c> attribute.
	/// </summary>
	SkipHidden = 4,
	
	/// <summary>
	/// Skip files and subdirectories that have <c>Hidden</c> and <c>System</c> attributes (both).
	/// These files/directories usually are created and used only by the operating system.
	/// Without this flag, skips only the hidden-system directory <c>$Recycle.Bin</c> when enumerating a root directory like <c>@"C:\"</c>.
	/// </summary>
	SkipHiddenSystem = 8,
	
	/// <summary>
	/// Don't throw exception when fails to enumerate the directory or a subdirectory because of its security settings.
	/// </summary>
	IgnoreInaccessible = 16,
}

//Probably not useful when we have FLPath.
#if FSI_EXTRA
/// <summary>
/// Extra properties attached to a <c>FileSystemInfo</c>.
/// </summary>
internal class FSIExtra_(int relOffset) {
	int _relOffset = relOffset;
	string _relPath;
	
	static ConditionalWeakTable<FileSystemInfo, FSIExtra_> s_listCWT = new();
	
	public static FileSystemInfo CreateFSI(ref FileSystemEntry e) {
		var r = e.ToFileSystemInfo();
		s_listCWT.Add(r, new FSIExtra_(e.RelativeOffset_));
		return r;
	}
	
	static FSIExtra_ _Get(FileSystemInfo f) {
		if (!s_listCWT.TryGetValue(f, out var x)) throw new InvalidOperationException("This property is available only for objects created by `FLQuery`.");
		return x;
	}
	
	public static int RelativeOffset(FileSystemInfo f) => _Get(f)._relOffset;
	
	public static string RelativePath(FileSystemInfo f) {
		var x = _Get(f);
		return x._relPath ??= f.FullName[x._relOffset..];
	}
	
	public static int RelativeDepth(FileSystemInfo f) {
		var x = _Get(f);
		return f.FullName.AsSpan(x._relOffset).Count('\\');
	}
}
#endif

public static partial class ExtMisc {
	extension(FileSystemInfo t) {
#if FSI_EXTRA
		internal int RelativeOffset_ => FSIExtra_.RelativeOffset(t);
		
		/// <summary>
		/// Gets the path relative to the directory passed to <c>filesystem.list</c>.
		/// </summary>
		/// <exception cref="InvalidOperationException">This object was not created by <c>FLQuery</c>.</exception>
		public string RelativePath => FSIExtra_.RelativePath(t);
		
		/// <summary>
		/// Gets the number of intermediate directories between this object's path and the path of the directory passed to <c>filesystem.list</c>.
		/// </summary>
		/// <exception cref="InvalidOperationException">This object was not created by <c>FLQuery</c>.</exception>
		public int RelativeDepth => FSIExtra_.RelativeDepth(t);
#endif
		
		/// <summary>
		/// Returns <c>true</c> if this is a <c>DirectoryInfo</c>.
		/// </summary>
		public bool IsDirectory => t is DirectoryInfo;
		
		/// <summary>
		/// Returns <c>true</c> if this is a NTFS link, eg symbolic link or mount point.
		/// </summary>
		public bool IsNtfsLink => t.Attributes.Has(FileAttributes.ReparsePoint) && filesystem.IsNtfsLink_(t.FullName);
		
		///// <summary>
		///// Gets path relative to <i>relativeTo</i>. See <see cref="Path.GetRelativePath"/>.
		///// </summary>
		///// <param name="relativeTo">A directory path.</param>
		//public string GetRelativePath(string relativeTo) {
		//	var path = t.ToString(); //documented: returns original path. However .NET file enum methods create FileSystemInfo with normalized path as original. FileName returns normalized path.
		//	var dir = relativeTo.AsSpan().TrimEnd("\\/");
		//	if (path.PathStarts_(dir)) { //try fast
		//		int i = dir.Length; while (i < path.Length && path[i] is '\\' or '/') i++;
		//		return path[i..];
		//	}
		//	Debug_.Print("path does not start with relativeTo");
		
		//	return Path.GetRelativePath(relativeTo, t.FullName);
		//}
	}
	
	extension(ref FileSystemEntry t) {
		/// <summary>
		/// Gets relative path.
		/// </summary>
		public string ToRelativePath() {
			int i = t.RootDirectory.Length;
			if (i >= t.Directory.Length) return t.FileName.ToString();
			if (t.Directory[i] is '\\' or '/') i++; //root is C:\dir, else C:\
			return Path.Join(t.Directory[i..], t.FileName);
		}
		
		internal int RelativeOffset_ => t.RootDirectory.Length + (t.RootDirectory[^1] is '\\' or '/' ? 0 : 1);
	}
}
