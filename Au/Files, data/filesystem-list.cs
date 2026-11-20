namespace Au.Types;

/// <summary>
/// The return type of <see cref="filesystem.list"/>.
/// Use like <c>var a = filesystem.list(@"C:\path").All;</c>.
/// </summary>
public class FLQuery {
	readonly string _directory;
	readonly PathFilter _filter;
	readonly FLFlags _flags;
	
	/// <exception cref="ArgumentException">
	/// <br/>• <i>directory</i> - not full path.
	/// <br/>• <i>filter</i> - a glob pattern is empty or contains an empty part or invalid wildcard expression.
	/// </exception>
	public FLQuery(string directory, PathFilter filter = null, FLFlags flags = 0) {
		if (filter?.IsEmpty() == true) filter = null;
		
		_directory = Path2.NormalizeMinimally_(directory, dontPrefix: true);
		_filter = filter;
		_flags = flags;
	}
	
	/// <summary>
	/// Enumerates files and/or directories in the directory, using a callback function to create objects of desired type.
	/// </summary>
	/// <param name="transform">A function that creates objects of desired type.</param>
	/// <param name="files">
	/// Get files, directories or both:
	/// <br/>• <c>true</c> - files only.
	/// <br/>• <c>false</c> - directories only.
	/// <br/>• <c>null</c> - all.
	/// </param>
	/// <returns>An enumerable collection of objects created by the <c>transform</c> function. The file system enumeration starts when the caller starts to enumerate the collection, and ends when the caller ends to enumerate it.</returns>
	/// <exception cref="ArgumentException"><i>directory</i> is not full path.</exception>
	/// <exception cref="DirectoryNotFoundException">The specified directory does not exist.</exception>
	/// <exception cref="UnauthorizedAccessException">A directory is inaccessible. See <see cref="FLFlags.IgnoreInaccessible"/>.</exception>
	/// <exception cref="IOException">Failed.</exception>
	/// <remarks>
	/// For NTFS links (symbolic links, mount points etc) gets link info, not target info.
	/// 
	/// When enumerating recursively, the order of results is depth-first (each directory is immediately followed by all its descendants).
	/// </remarks>
	public IEnumerable<TResult> Enumerate<TResult>(Func<FLEntry, TResult> transform, bool? files = null) {
		ArgumentNullException.ThrowIfNull(transform);
		
		PathStack_ pathStack = new(_directory);
		FLEntry.Data_ entryData = new(pathStack);
		bool hasFilterFunctions = _filter is { FileFilter: not null } or { DirectoryFilter: not null };
		
		if (_flags.Has(FLFlags.Recurse)) {
			var stack = new _DirectoryEnumerator[10];
			int depth = 0, stackMax = 0;
			bool skipSome = !_flags.Has(FLFlags.SkipHiddenSystem) && _directory is [.., ':', '\\' or '/'];
			
			try {
				if (!stack[0].Init(_directory, _flags, pathStack)) yield break;
				for (; ; ) {
					if (!stack[depth].MoveNext()) {
						if (--depth < 0) break;
						pathStack.Pop();
						continue;
					}
					
					ref var f = ref stack[depth].Current;
					
					bool isDir = f.IsDirectory;
					bool include = isDir != files;
					bool recurse = isDir;
					if (recurse) {
						if (!_flags.Has(FLFlags.RecurseFollowLinks) && f.IsLink) recurse = false;
						else if (skipSome && depth == 0 && f.Attributes.Has(FileAttributes.Hidden | FileAttributes.System) && f.Name.Eqi("$Recycle.Bin")) recurse = false;
					}
					
					if (!(include | recurse)) continue;
					
					pathStack.Push(f.Name);
					
					if (_filter != null) {
						if (include) include = _filter.ShouldInclude(pathStack.RelativePath, isDir);
						if (recurse) recurse = _filter.ShouldRecurse(pathStack.RelativePath);
						if (!(include | recurse)) { pathStack.Pop(); continue; }
					}
					
					FLEntry e = (include || hasFilterFunctions)
						? new(entryData, ref f, depth, recurse, include)
						: default;
					
					if (hasFilterFunctions) _CallFilterFunc(ref include, ref recurse, e);
					
					if (include)
						yield return transform(e);
					
					if (recurse) {
						if (depth + 1 == stack.Length) Array.Resize(ref stack, stack.Length * 2);
						ref var v = ref stack[depth];
#if true
						recurse = stack[++depth].Init(v.Handle, _flags, pathStack);
#else //compare speed
						recurse = stack[++depth].Init(Path.Join(pathStack.Path.ToString()), _flags, pathStack);
#endif
						if (recurse) stackMax = Math.Max(stackMax, depth); else depth--;
					}
					
					if (!recurse) pathStack.Pop();
				}
			}
			finally {
				while (stackMax >= 0) stack[stackMax--].Dispose();
			}
		} else {
			using var de = new _DirectoryEnumerator();
			if (!de.Init(_directory, _flags, pathStack)) yield break;
			while (de.MoveNext()) {
				ref var f = ref de.Current;
				
				if (f.IsDirectory == files) continue;
				if (_filter?.ShouldInclude(f.Name, f.IsDirectory) == false) continue;
				
				pathStack.Push(f.Name);
				
				FLEntry e = new(entryData, ref f);
				
				bool include = true, recurse = false;
				if (hasFilterFunctions) _CallFilterFunc(ref include, ref recurse, e);
				
				if (include)
					yield return transform(e);
				
				pathStack.Pop();
			}
		}
	}
	
	void _CallFilterFunc(ref bool include, ref bool recurse, FLEntry e) {
		if (!e.IsDirectory) {
			if (include && _filter.FileFilter is { } fif) include = fif(e);
		} else if (_filter.DirectoryFilter is { } dif) {
			if (dif(e)) (include, recurse) = (e.DirInclude, e.DirRecurse); //info: the property setters allow only to set = false
			else include = recurse = false;
		}
	}
	
	#region FLFile
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their paths and other properties.
	/// </summary>
	/// <returns>Enumerable collection. The file system enumeration starts when the caller starts to enumerate the collection, and ends when the caller ends to enumerate it.</returns>
	/// <inheritdoc cref="Enumerate{TResult}"/>
	public IEnumerable<FLFile> Enumerate(bool? files = null) => Enumerate(static e => new FLFile(e), files);
	
	/// <returns>Array. Unlike <see cref="Enumerate(bool?)"/>, this function returns when the file system enumeration ends.</returns>
	/// <inheritdoc cref="Enumerate{TResult}(Func{FLEntry, TResult}, bool?)"/>
	public FLFile[] All => Enumerate().ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their paths and other properties.
	/// </summary>
	/// <inheritdoc cref="All"/>
	public FLFile[] Files => Enumerate(true).ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their paths and other properties.
	/// </summary>
	/// <inheritdoc cref="All"/>
	public FLFile[] Directories => Enumerate(false).ToArray();
	#endregion
	
	#region paths
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their paths and returns <c>string[]</c>.
	/// </summary>
	/// <inheritdoc cref="Enumerate(bool?)" path="/exception"/>
	public string[] AllPaths => Enumerate(static e => e.Path.ToString(), null).ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their paths and returns <c>string[]</c>.
	/// </summary>
	/// <inheritdoc cref="Enumerate(bool?)" path="/exception"/>
	public string[] FilePaths => Enumerate(static e => e.Path.ToString(), true).ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their paths and returns <c>string[]</c>.
	/// </summary>
	/// <inheritdoc cref="Enumerate(bool?)" path="/exception"/>
	public string[] DirectoryPaths => Enumerate(static e => e.Path.ToString(), false).ToArray();
	#endregion
	
	#region names
	/// <summary>
	/// Enumerates files and directories in the directory. Gets their filenames and returns <c>string[]</c>.
	/// </summary>
	/// <inheritdoc cref="Enumerate(bool?)" path="/exception"/>
	public string[] AllNames => Enumerate(static e => e.Name.ToString(), null).ToArray();
	
	/// <summary>
	/// Enumerates files in the directory. Gets their filenames and returns <c>string[]</c>.
	/// </summary>
	/// <inheritdoc cref="Enumerate(bool?)" path="/exception"/>
	public string[] FileNames => Enumerate(static e => e.Name.ToString(), true).ToArray();
	
	/// <summary>
	/// Enumerates directories in the directory. Gets their filenames and returns <c>string[]</c>.
	/// </summary>
	/// <inheritdoc cref="Enumerate(bool?)" path="/exception"/>
	public string[] DirectoryNames => Enumerate(static e => e.Name.ToString(), false).ToArray();
	#endregion
	
	unsafe struct _DirectoryEnumerator : IDisposable {
		const int c_bufSize = 10000; //tested: bigger buffer does not make faster. It seems the API even does not use most of a large buffer eg 64K.
		byte* _b;
		Handle_ _hDir;
		Api.FILE_FULL_DIR_INFORMATION* _f;
		FLFlags _flags;
		PathStack_ _pathStack;
		
		public nint Handle => _hDir;
		
		public bool Init(string directory, FLFlags flags, PathStack_ pathStack) {
			_pathStack = pathStack;
			var s = Path2.PrefixLongPathIfNeed(directory);
			var h = Api.CreateFile(s, Api.FILE_LIST_DIRECTORY, Api.CFShare.READ | Api.CFShare.WRITE | Api.CFShare.DELETE, Api.CF.OPEN_EXISTING, Api.FILE_FLAG_BACKUP_SEMANTICS);
			return _Init(true, h, flags);
		}
		
		public bool Init(nint hParent, FLFlags flags, PathStack_ pathStack) {
			_pathStack = pathStack;
			var h = _OpenSubdirectory(hParent, pathStack.Name);
			return _Init(false, h, flags);
		}
		
		bool _Init(bool isRoot, Handle_ h, FLFlags flags) {
			Debug.Assert(_hDir.Is0);
			_f = null;
			_flags = flags;
			if (h.Is0) {
				var error = lastError.code;
				if (lastError.IsFileNotFoundError_(error)) {
					if (!isRoot) return false;
					throw new DirectoryNotFoundException($"Directory not found: {_pathStack.Path}") { HResult = unchecked((int)0x80070000 | (error & 0xFFFF)) };
				} else if (!isRoot && error is Api.ERROR_DIRECTORY or Api.ERROR_NOT_READY) return false;
				_ThrowIfCannotContinue(error);
				return false;
			}
			_hDir = h;
			if (_b == null) _b = (byte*)NativeMemory.Alloc((nuint)c_bufSize);
			return true;
		}
		
		void _ThrowIfCannotContinue(int error) {
			if (error != Api.ERROR_ACCESS_DENIED) throw new IOException($"{lastError.messageFor(error)}  Directory: {_pathStack.Path}", error);
			if (!_flags.Has(FLFlags.IgnoreInaccessible)) throw new UnauthorizedAccessException($"Access denied to directory: {_pathStack.Path}");
		}
		
		public bool MoveNext() {
			next:
			if (_f == null || _f->NextEntryOffset == 0) {
				//if (_f != null) { //makes slightly faster, but unsafe. Tested: the API does not fill full buffer. The unused size is unpredictable and depends on buffer size.
				//	long nextOffset = ((byte*)_f - _b) + sizeof(Api.FILE_FULL_DIR_INFORMATION) + _f->nameLength;
				//	if (nextOffset < 5000) return ref Unsafe.NullRef<Api.FILE_FULL_DIR_INFORMATION>();
				//	//print.it($"<><c blue>new<>");
				//}
				int r = Api.NtQueryDirectoryFile(_hDir, 0, 0, 0, out var status, _b, c_bufSize, 2, false, 0, false);
				//print.it((uint)r, (int)status.information);
				if (r != 0) {
					_hDir.Dispose();
					_f = null;
					if (r != Api.STATUS_NO_MORE_FILES) _ThrowIfCannotContinue(Api.RtlNtStatusToDosError(r));
					return false;
				}
				_f = (Api.FILE_FULL_DIR_INFORMATION*)_b;
			} else {
				_f = (Api.FILE_FULL_DIR_INFORMATION*)((byte*)_f + _f->NextEntryOffset);
			}
			
			if (_f->IsDirectory && _f->Name is "." or "..") goto next;
			if (_flags.Has(FLFlags.SkipHidden) && _f->Attributes.Has(FileAttributes.Hidden)) goto next;
			if (_flags.Has(FLFlags.SkipHiddenSystem) && _f->Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) goto next;
			
			return true;
		}
		
		public ref Api.FILE_FULL_DIR_INFORMATION Current => ref *_f;
		
		public void Dispose() {
			_hDir.Dispose();
			NativeMemory.Free(_b);
			_b = null;
		}
		
		static Handle_ _OpenSubdirectory(nint parentDirHandle, ReadOnlySpan<char> name) {
			//if (parentDirHandle == 0 || name.IsEmpty || name.Length > ushort.MaxValue / sizeof(char) - 1) throw new ArgumentException();
			
			fixed (char* p = name) {
				var us = new Api.UNICODE_STRING {
					Length = (ushort)(name.Length * sizeof(char)),
					MaximumLength = (ushort)(name.Length * sizeof(char)),
					Buffer = p
				};
				
				var oa = new Api.OBJECT_ATTRIBUTES {
					Length = (uint)sizeof(Api.OBJECT_ATTRIBUTES),
					RootDirectory = parentDirHandle,
					ObjectName = &us,
					Attributes = 0,
				};
				
				const uint FILE_DIRECTORY_FILE = 0x00000001;
				const uint FILE_SYNCHRONOUS_IO_NONALERT = 0x00000020;
				const uint FILE_OPEN_FOR_BACKUP_INTENT = 0x00004000;
				//const uint FILE_OPEN_REPARSE_POINT = 0x00200000; //don't follow symlinks/junctions
				
				int status = Api.NtCreateFile(
					out nint h,
					Api.FILE_LIST_DIRECTORY | Api.SYNCHRONIZE,
					in oa,
					out _,
					null,
					0,
					Api.CFShare.ALL,
					1, //FILE_OPEN
					FILE_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT | FILE_OPEN_FOR_BACKUP_INTENT);
				
				if (status < 0) {
					lastError.code = Api.RtlNtStatusToDosError(status);
					return default;
				}
				
				return new(h);
			}
		}
	}
	
	/// <summary>
	/// Contains current path in char[]. Appends and removes filenames.
	/// </summary>
	internal class PathStack_ {
		char[] _a;
		int _len;
		internal int nameOffset;
		internal readonly int relativeOffset;
		
		public PathStack_(string directory) {
			var d = directory.AsSpan().TrimEnd("\\/");
			_a = new char[d.Length + 255];
			d.CopyTo(_a);
			_len = d.Length;
			relativeOffset = _len + 1;
		}
		
		/// <summary>
		/// Appends filename.
		/// Sets current <see cref="Path"/> etc.
		/// </summary>
		public void Push(RStr name) {
			nameOffset = _len + 1;
			int n = _len + 1 + name.Length;
			if (_a.Length < n + 1) Array.Resize(ref _a, _a.Length + n + 1);
			_a[_len++] = '\\';
			name.CopyTo(_a.AsSpan(_len));
			_a[_len = n] = '\0';
		}
		
		/// <summary>
		/// Removes filename from current path.
		/// </summary>
		public void Pop() { //path -> parentPath\
			_len = nameOffset - 1;
			_a[_len] = '\0';
			nameOffset = nameOffset > relativeOffset ? Path.LastIndexOf('\\') + 1 : 0;
		}
		
		public RStr Path => _a.AsSpan(0, _len);
		
		public RStr RelativePath => _a.AsSpan(relativeOffset, _len - relativeOffset);
		
		public RStr Name => _a.AsSpan(nameOffset, _len - nameOffset);
		
		public RStr DirectoryBS => _a.AsSpan(0, nameOffset);
		
		public RStr RootDirectoryBS => _a.AsSpan(0, relativeOffset);
	}
}

/// <summary>
/// Provides information about a file or directory. Used in callback functions of file system enumeration methods.
/// </summary>
public unsafe ref struct FLEntry {
	/// <summary>
	/// A FLEntry keeps its data in single Data_ instance (local var of Enumerate).
	/// Because FLEntry must be a small ref struct that can change its data even when copied.
	/// Why ref struct: compiler won't allow its copies to escape the callback.
	/// </summary>
	internal class Data_ {
		internal readonly FLQuery.PathStack_ ps;
		internal int depth;
		internal bool recurse, include;
		internal Api.FILE_FULL_DIR_INFORMATION* f;
		
		internal Data_(FLQuery.PathStack_ pathStack) {
			ps = pathStack;
		}
	}
	
	internal readonly Data_ d;
	
	internal FLEntry(Data_ data, ref Api.FILE_FULL_DIR_INFORMATION f, int depth = 0, bool recurse = false, bool include = true) {
		d = data;
		d.depth = depth;
		d.recurse = recurse;
		d.include = include;
		d.f = (Api.FILE_FULL_DIR_INFORMATION*)Unsafe.AsPointer(ref f);
	}
	
	/// <summary>
	/// Full path.
	/// </summary>
	public RStr Path => d.ps.Path;
	
	/// <summary>
	/// Filename.
	/// </summary>
	public RStr Name => d.ps.Name;
	
	/// <summary>
	/// Relative path.
	/// </summary>
	public RStr RelativePath => d.ps.RelativePath;
	
	/// <summary>
	/// Parent directory path. Ends with backslash (<c>\</c>).
	/// </summary>
	public RStr Directory => d.ps.DirectoryBS;
	
	/// <summary>
	/// Root directory path. It's the value passed to <see cref="filesystem.list"/>. Ends with backslash (<c>\</c>).
	/// </summary>
	public RStr RootDirectory => d.ps.RootDirectoryBS;
	
	/// <summary>
	/// Recursion depth, starting from 0. In other words - the number of <c>\</c> in the relative path.
	/// </summary>
	public int Depth => d.depth;
	
	/// <summary>
	/// <c>true</c> if it's a directory.
	/// </summary>
	public bool IsDirectory => d.f->IsDirectory;
	
	/// <summary>
	/// <c>true</c> if it's a file system link (symbolic link, junction or mount point) to a file or directory.
	/// </summary>
	/// <remarks>
	/// To get the link target: <see cref="filesystem.more.getFinalPath"/>.
	/// </remarks>
	public bool IsLink => d.f->IsLink;
	
	/// <summary>
	/// Attributes.
	/// </summary>
	public FileAttributes Attributes => d.f->Attributes;
	
	/// <summary>
	/// File size.
	/// </summary>
	public long Size => d.f->EndOfFile;
	
	/// <summary>
	/// The number of bytes allocated for the file on the storage device. Aka "Size on disk".
	/// </summary>
	public long SizeAllocated => d.f->AllocationSize;
	
	/// <summary>
	/// Size of extended attributes, or reparse point tag (if <see cref="FileAttributes.ReparsePoint"/>).
	/// </summary>
	public uint EaInfo => d.f->EaSize;
	
	/// <summary>
	/// Time when the file was created. See <see cref="DateTime.FromFileTimeUtc"/>.
	/// </summary>
	public long CreationTimeUtc => d.f->CreationTime;
	
	/// <summary>
	/// Last time data was written to the file. See <see cref="DateTime.FromFileTimeUtc"/>.
	/// </summary>
	public long LastWriteTimeUtc => d.f->LastWriteTime;
	
	/// <summary>
	/// Last time the file was accessed. See <see cref="DateTime.FromFileTimeUtc"/>.
	/// </summary>
	public long LastAccessTimeUtc => d.f->LastAccessTime;
	
	/// <summary>
	/// Last time the file was changed (data or properties). See <see cref="DateTime.FromFileTimeUtc"/>.
	/// </summary>
	public long LastAnyChangeTimeUtc => d.f->ChangeTime;
	
	/// <summary>
	/// <c>true</c> if descendants of this directory will be enumerated (when enumerating recursively).
	/// The directory filter function can exclude descendants only: set this property = <c>false</c> and return <c>true</c>.
	/// </summary>
	public bool DirRecurse {
		get => d.recurse;
		set { d.recurse &= value; }
	}
	
	/// <summary>
	/// <c>true</c> if this directory entry is included in results.
	/// The directory filter function can exclude this entry only: set this property = <c>false</c> and return <c>true</c>.
	/// </summary>
	public bool DirInclude {
		get => d.include;
		set { d.include &= value; }
	}
}

/// <summary>
/// Contains file or directory properties retrieved by a file system enumeration function.
/// </summary>
/// <remarks>
/// Has implicit conversion to string (full path).
/// 
/// Can be used like <c>foreach (var v in filesystem.list(directory).All) print.it(v, v.Name, v.IsDirectory);</c>.
/// </remarks>
public class FLFile {
	string _path, _name, _relativePath;
	ushort _nameOffset, _relativeOffset, _depth;
	bool _isLink;
	FileAttributes _attr;
	
	///
	public FLFile(in FLEntry e) {
		_path = e.Path.ToString();
		_nameOffset = checked((ushort)e.d.ps.nameOffset);
		_relativeOffset = checked((ushort)e.d.ps.relativeOffset);
		_depth = checked((ushort)e.d.depth);
		_isLink = e.IsLink;
		_attr = e.Attributes;
		Size = e.Size;
		CreationTimeUtc = DateTime.FromFileTimeUtc(e.CreationTimeUtc);
		LastWriteTimeUtc = DateTime.FromFileTimeUtc(e.LastWriteTimeUtc);
	}
	
	/// <summary>
	/// Full path.
	/// </summary>
	public string Path => _path;
	
	/// <summary>
	/// Filename.
	/// </summary>
	public string Name => _name ??= _path[_nameOffset..];
	
	/// <summary>
	/// Relative path.
	/// </summary>
	public string RelativePath => _relativePath ??= _relativeOffset == _nameOffset ? Name : _path[_relativeOffset..];

	/// <summary>
	/// File name extension, like ".txt". Returns "" if no extension.
	/// </summary>
	public string Extension => IsDirectory ? "" : System.IO.Path.GetExtension(_path);
	
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
	public bool IsLink => _isLink;
	
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
	/// Recursion depth, starting from 0. In other words - the number of <c>\</c> in the relative path.
	/// </summary>
	public int Depth => _depth;
	
	/// <summary>
	/// File size.
	/// </summary>
	public long Size { get; }
	
	/// <summary>
	/// Time when the file was created.
	/// </summary>
	public DateTime CreationTimeUtc { get; }
	
	/// <summary>
	/// Last time data was written to the file.
	/// </summary>
	public DateTime LastWriteTimeUtc { get; }
	
	/// <summary>
	/// Implicit conversion to string. Returns full path.
	/// </summary>
	public static implicit operator string(FLFile p) => p._path;
	
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
	/// Get all descendants, not just direct children. Don't follow file system links.
	/// </summary>
	Recurse = 1,
	
	/// <summary>
	/// Get all descendants, not just direct children. Follow file system links (symbolic links, junctions, mount points) to directories.
	/// Includes flag <c>Recurse</c>.
	/// </summary>
	RecurseFollowLinks = 3,
	
	/// <summary>
	/// Skip files and subdirectories (and their descendants) that have <c>Hidden</c> attribute.
	/// </summary>
	SkipHidden = 4,
	
	/// <summary>
	/// Skip files and subdirectories (and their descendants) that have <c>Hidden</c> and <c>System</c> attributes (both).
	/// These files/directories usually are created and used only by the operating system.
	/// Without this flag, skips only the hidden-system directory <c>$Recycle.Bin</c> when enumerating a root directory like <c>@"C:\"</c>.
	/// </summary>
	SkipHiddenSystem = 8,
	
	/// <summary>
	/// Don't throw exception when fails to enumerate the directory or a subdirectory because of its security settings.
	/// </summary>
	IgnoreInaccessible = 16,
}
