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
	/// Get files, directories or both:
	/// <br/>• <c>true</c> - files only.
	/// <br/>• <c>false</c> - directories only.
	/// <br/>• <c>null</c> - all.
	/// </param>
	/// <returns>An enumerable collection of objects created by the <c>transform</c> function. The file system enumeration starts each time when the caller starts to enumerate the collection, and ends when the caller ends to enumerate it.</returns>
	/// <exception cref="ArgumentException"><i>directory</i> is not full path.</exception>
	/// <exception cref="DirectoryNotFoundException">The specified directory does not exist.</exception>
	/// <exception cref="IOException">Failed. For example <i>directory</i> is a file.</exception>
	/// <exception cref="UnauthorizedAccessException">A directory is inaccessible. See <see cref="FLFlags.IgnoreInaccessible"/>.</exception>
	/// <remarks>
	/// Uses <see cref="FileSystemEnumerable{TResult}"/>.
	/// 
	/// The order of results probably is breadth-first, but it's not documented.
	/// 
	/// For NTFS links (symbolic links, mount points etc) gets link info, not target info.
	/// </remarks>
	public IEnumerable<TResult> Enumerate<TResult>(FileSystemEnumerable<TResult>.FindTransform transform, bool? files = null) {
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
			followLinks = _flags.Has(FLFlags.RecurseFollowLinks),
			useShouldInclude = files != null || skipHiddenSystem || _filter != null,
			useShouldRecurse = recurse && (skipHiddenSystem || skipSome || !followLinks || _filter != null);
		
		bool _ShouldInclude(ref FileSystemEntry e) {
			if (e.IsDirectory == files) return false;
			if (skipHiddenSystem && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) return false;
			if (_filter?.ShouldInclude(ref e) == false) return false;
			return true;
		}
		
		bool _ShouldRecurse(ref FileSystemEntry e) {
			if (skipHiddenSystem && e.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) return false;
			if (!followLinks && e.IsNtfsLink) return false;
			
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
	
	#region FLPath
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their paths and some other properties.
	/// </summary>
	/// <returns>Enumerable collection. The file system enumeration starts each time when the caller starts to enumerate the collection, and ends when the caller ends to enumerate it.</returns>
	/// <inheritdoc cref="Enumerate{TResult}"/>
	public IEnumerable<FLPath> Enumerate(bool? files = null) => Enumerate(static (ref e) => new FLPath(e), files);
	
	/// <returns>Array. Unlike <see cref="Enumerate"/>, this function returns when the file system enumeration ends.</returns>
	/// <inheritdoc cref="Enumerate"/>
	public FLPath[] All => Enumerate().ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their paths and some other properties.
	/// </summary>
	/// <inheritdoc cref="All"/>
	public FLPath[] Files => Enumerate(true).ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their paths and some other properties.
	/// </summary>
	/// <inheritdoc cref="All"/>
	public FLPath[] Directories => Enumerate(false).ToArray();
	#endregion
	
	#region infos
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their properties.
	/// </summary>
	/// <returns>Enumerable collection of <see cref="FileInfo"/> and <see cref="DirectoryInfo"/> objects. The file system enumeration starts each time when the caller starts to enumerate the collection, and ends when the caller ends to enumerate it.</returns>
	/// <inheritdoc cref="Enumerate{TResult}"/>
	public IEnumerable<FileSystemInfo> EnumerateInfos(bool? files = null) => Enumerate(static (ref e) => e.ToFileSystemInfo(), files);
	
	/// <returns>Array of <see cref="FileInfo"/> and <see cref="DirectoryInfo"/> objects. Unlike <see cref="EnumerateInfos"/>, this function returns when the file system enumeration ends.</returns>
	/// <inheritdoc cref="EnumerateInfos"/>
	public FileSystemInfo[] AllInfos => EnumerateInfos().ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their properties.
	/// </summary>
	/// <returns>Array. Unlike <see cref="EnumerateInfos"/>, this function returns when the file system enumeration ends.</returns>
	/// <inheritdoc cref="AllInfos"/>
	public FileInfo[] FileInfos => EnumerateInfos(true).Cast<FileInfo>().ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their properties.
	/// </summary>
	/// <returns>Array. Unlike <see cref="EnumerateInfos"/>, this function returns when the file system enumeration ends.</returns>
	/// <inheritdoc cref="AllInfos"/>
	public DirectoryInfo[] DirectoryInfos => EnumerateInfos(false).Cast<DirectoryInfo>().ToArray();
	#endregion
	
	#region paths
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their paths as string.
	/// </summary>
	/// <returns>Array. All paths start with exact <i>directory</i> string (not normalized).</returns>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] AllPaths => Enumerate(_GetPath, null).ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their paths as string.
	/// </summary>
	/// <inheritdoc cref="AllPaths" path="/returns"/>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] FilePaths => Enumerate(_GetPath, true).ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their paths as string.
	/// </summary>
	/// <inheritdoc cref="AllPaths" path="/returns"/>
	/// <inheritdoc cref="Enumerate" path="/exception"/>
	public string[] DirectoryPaths => Enumerate(_GetPath, false).ToArray();
	
	private static string _GetPath(ref FileSystemEntry entry) => entry.ToSpecifiedFullPath();
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
/// Provides a file path and several other commonly used properties. Has implicit conversion to string (path).
/// Can be used like <c>foreach (var v in filesystem.list(directory).All) print.it(v, v.Name, v.IsDirectory);</c>.
/// </summary>
public class FLPath {
	string _path, _name, _relativePath;
	ushort _nameOffset, _relativeOffset;
	FileAttributes _attr;
	
	///
	public FLPath(in FileSystemEntry e) {
		_path = e.ToFullPath();
		_nameOffset = checked((ushort)(_path.Length - e.FileName.Length));
		_relativeOffset = checked((ushort)(e.RelativeOffset_));
		_attr = e.Attributes;
	}
	
	/// <summary>
	/// Full path.
	/// </summary>
	/// <remarks>
	/// The paths are normalized, ie may not start with exact <i>directory</i> string. If you need relative path, use property <see cref="RelativePath"/>.
	/// </remarks>
	public string Path => _path;
	
	/// <summary>
	/// Filename.
	/// </summary>
	public string Name => _name ??= _path[_nameOffset..];
	
	/// <summary>
	/// Relative path.
	/// </summary>
	public string RelativePath => _relativePath ??= _relativeOffset == _nameOffset ? Name : _path[_relativeOffset..];
	
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
	/// Use when you avoid creating a new <c>string</c> (<c>Name</c> creates once, when used the first time).
	/// </summary>
	public RStr NameSpan => _path.AsSpan(_nameOffset);
	
	/// <summary>
	/// Relative path as span.
	/// Use when you avoid creating a new <c>string</c> (<c>RelativePath</c> creates once, when used the first time).
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
	/// Get all descendants, not just direct children. Don't follow NTFS directory links.
	/// </summary>
	Recurse = 1,
	
	/// <summary>
	/// Get all descendants, not just direct children. Follow NTFS directory links (symbolic links, mount points etc).
	/// Includes flag <c>Recurse</c>.
	/// </summary>
	RecurseFollowLinks = 3,
	
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

public static partial class ExtMisc {
	extension(FileSystemInfo t) {
		/// <summary>
		/// Returns <c>true</c> if this is a <c>DirectoryInfo</c>.
		/// </summary>
		public bool IsDirectory => t is DirectoryInfo;
		
		/// <summary>
		/// Returns <c>true</c> if this is a NTFS link, eg symbolic link or mount point.
		/// </summary>
		public bool IsNtfsLink => t.Attributes.Has(FileAttributes.ReparsePoint) && filesystem.IsNtfsLink_(t.FullName);
	}
	
	extension(in FileSystemEntry t) {
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
		
		/// <summary>
		/// The number of intermediate directories from <c>filesystem.list</c> <i>directory</i>.
		/// </summary>
		public int RelativeDepth => t.Directory.Length <= t.RootDirectory.Length ? 0 : t.Directory[t.RelativeOffset_..].Count('\\') + 1;
		
		/// <summary>
		/// Returns <c>true</c> if this is a NTFS link, eg symbolic link or mount point.
		/// </summary>
		public bool IsNtfsLink => t.Attributes.Has(FileAttributes.ReparsePoint) && filesystem.IsNtfsLink_(t.ToFullPath());
	}
	
	extension(FLQuery t) {
		/// <summary>
		/// Calls <see cref="FLQuery.All"/> and reorders results. Return results in depth-first order (each directory is immediately followed by all its descendants).
		/// </summary>
		/// <remarks>
		/// Directories must not be excluded without also excluding all their descendants.
		/// </remarks>
		/// <inheritdoc cref="FLQuery.All"/>
		public FLPath[] AllDepthFirst()
			=> _ToDepthFirst(t.All, static o => o.Path);
		
		/// <summary>
		/// Calls <see cref="FLQuery.AllInfos"/> and reorders results. Return results in depth-first order (each directory is immediately followed by all its descendants).
		/// </summary>
		/// <remarks>
		/// Directories must not be excluded without also excluding all their descendants.
		/// </remarks>
		/// <inheritdoc cref="FLQuery.AllInfos"/>
		public FileSystemInfo[] AllInfosDepthFirst()
			=> _ToDepthFirst(t.AllInfos, static o => o.FullName);
		
		/// <summary>
		/// Calls <see cref="FLQuery.Enumerate{TResult}(FileSystemEnumerable{TResult}.FindTransform, bool?)"/> and reorders results. Return results in depth-first order (each directory is immediately followed by all its descendants).
		/// </summary>
		/// <param name="getPath">Callback function that returns path. Can be full or relative path. Must be normal path that came from a file system enumeration function: separator character <c>\</c>, no <c>\..</c> etc.</param>
		/// <returns>Array.</returns>
		/// <remarks>
		/// Directories must not be excluded without also excluding all their descendants.
		/// </remarks>
		/// <inheritdoc cref="FLQuery.Enumerate{TResult}(FileSystemEnumerable{TResult}.FindTransform, bool?)"/>
		public TResult[] AllDepthFirst<TResult>(FileSystemEnumerable<TResult>.FindTransform transform, Func<TResult, string> getPath)
			=> _ToDepthFirst(t.Enumerate(transform).ToArray(), getPath);
	}
	
	static T[] _ToDepthFirst<T>(T[] a, Func<T, string> getPath) {
		if (a.Length < 2) return a;
		
		// ToLookup preserves the original order within each group.
		var children = a.ToLookup(x => Path.GetDirectoryName(getPath(x))!, StringComparer.OrdinalIgnoreCase);
		
		// All items are descendants of one directory, so the shortest parent path is that directory.
		var root = children.MinBy(g => g.Key.Length)!.Key;
		
		var result = new T[a.Length];
		var stack = new Stack<IEnumerator<T>>();
		stack.Push(children[root].GetEnumerator());
		
		int i = 0;
		while (stack.Count > 0) {
			var e = stack.Peek();
			if (!e.MoveNext()) {
				stack.Pop();
				continue;
			}
			
			var f = e.Current;
			result[i++] = f;
			
			// Files have no group, so this yields an empty sequence for them.
			stack.Push(children[getPath(f)].GetEnumerator());
		}
		
		return result.ToArray();
	}
}
