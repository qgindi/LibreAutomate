
//#define TEST_FINDFIRSTFILEEX

using System.Linq;
using Microsoft.Win32;

namespace Au
{
	/// <summary>
	/// Contains static functions to work with files and directories, such as copy, move, delete, find, get properties, enumerate, create directory, safe load/save, wait if locked.
	/// </summary>
	/// <remarks>
	/// Also you can use .NET file system classes, such as <see cref="File"/> and <see cref="Directory"/> in <b>System.IO</b> namespace. In the past they were too limited and unsafe to use, for example no long paths, too many exceptions, difficult to recursively enumerate directories containing protected items. Later improved, but this class still has something they don't, for example environment variables in path, safe load/save. This class does not have low-level functions to open/read/write files.
	/// 
	/// Most functions support only full path. Most of them throw <b>ArgumentException</b> if passed a filename or relative path, ie in "current directory". Using current directory is unsafe.
	/// Most functions support extended-length paths (longer than 259). Such local paths should have <c>@"\\?\"</c> prefix, like <c>@"\\?\C:\..."</c>. Such network path should be like <c>@"\\?\UNC\server\share\..."</c>. See <see cref="pathname.prefixLongPath"/>, <see cref="pathname.prefixLongPathIfNeed"/>. Many functions support long paths even without prefix.
	/// 
	/// Disk drives like <c>@"C:\"</c> or <c>"C:"</c> are directories too.
	/// </remarks>
	public static partial class filesystem
	{
		#region attributes, exists, search, enum

		/// <summary>
		/// Adds SEM_FAILCRITICALERRORS to the error mode of this process, as MSDN recommends. Once in process.
		/// It is to avoid unnecessary message boxes when an API tries to access an ejected CD/DVD etc.
		/// </summary>
		static void _DisableDeviceNotReadyMessageBox() {
			if (_disabledDeviceNotReadyMessageBox) return;
			var em = Api.GetErrorMode();
			if (0 == (em & Api.SEM_FAILCRITICALERRORS)) Api.SetErrorMode(em | Api.SEM_FAILCRITICALERRORS);
			_disabledDeviceNotReadyMessageBox = true;

			//CONSIDER: SetThreadErrorMode
		}
		static bool _disabledDeviceNotReadyMessageBox;

		/// <summary>
		/// Gets file or directory attributes, size and times.
		/// Returns false if the file/directory does not exist.
		/// Calls API <msdn>GetFileAttributesEx</msdn>.
		/// </summary>
		/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If flag UseRawPath not used, supports environment variables (see <see cref="pathname.expand"/>).</param>
		/// <param name="properties">Receives properties.</param>
		/// <param name="flags"></param>
		/// <exception cref="ArgumentException">Not full path (when not used flag UseRawPath).</exception>
		/// <exception cref="AuException">The file/directory exist but failed to get its properties. Not thrown if used flag DontThrow.</exception>
		/// <remarks>
		/// For symbolic links etc, gets properties of the link, not of its target.
		/// You can also get most of these properties with <see cref="enumerate"/>.
		/// </remarks>
		public static unsafe bool getProperties(string path, out FileProperties properties, FAFlags flags = 0) {
			properties = new FileProperties();
			if (0 == (flags & FAFlags.UseRawPath)) path = pathname.NormalizeMinimally_(path, true); //don't need NormalizeExpandEV_, the API itself supports .. etc
			_DisableDeviceNotReadyMessageBox();
			if (!Api.GetFileAttributesEx(path, 0, out Api.WIN32_FILE_ATTRIBUTE_DATA d)) {
				if (!_GetAttributesOnError(path, flags, out _, &d)) return false;
			}
			properties.Attributes = d.dwFileAttributes;
			properties.Size = (long)d.nFileSizeHigh << 32 | d.nFileSizeLow;
			properties.LastWriteTimeUtc = DateTime.FromFileTimeUtc(d.ftLastWriteTime);
			properties.CreationTimeUtc = DateTime.FromFileTimeUtc(d.ftCreationTime);
			properties.LastAccessTimeUtc = DateTime.FromFileTimeUtc(d.ftLastAccessTime);
			return true;
		}

		/// <summary>
		/// Gets file or directory attributes.
		/// Returns false if the file/directory does not exist.
		/// Calls API <msdn>GetFileAttributes</msdn>.
		/// </summary>
		/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If flag UseRawPath not used, supports environment variables (see <see cref="pathname.expand"/>).</param>
		/// <param name="attributes">Receives attributes, or 0 if failed.</param>
		/// <param name="flags"></param>
		/// <exception cref="ArgumentException">Not full path (when not used flag UseRawPath).</exception>
		/// <exception cref="AuException">Failed. Not thrown if used flag DontThrow.</exception>
		/// <remarks>
		/// For symbolic links etc, gets properties of the link, not of its target.
		/// </remarks>
		public static unsafe bool getAttributes(string path, out FileAttributes attributes, FAFlags flags = 0) {
			attributes = 0;
			if (0 == (flags & FAFlags.UseRawPath)) path = pathname.NormalizeMinimally_(path, true); //don't need NormalizeExpandEV_, the API itself supports .. etc
			_DisableDeviceNotReadyMessageBox();
			var a = Api.GetFileAttributes(path);
			if (a == (FileAttributes)(-1)) return _GetAttributesOnError(path, flags, out attributes);
			attributes = a;
			return true;
		}

		static unsafe bool _GetAttributesOnError(string path, FAFlags flags, out FileAttributes attr, Api.WIN32_FILE_ATTRIBUTE_DATA* p = null) {
			attr = 0;
			var ec = lastError.code;
			switch (ec) {
			case Api.ERROR_FILE_NOT_FOUND:
			case Api.ERROR_PATH_NOT_FOUND:
			case Api.ERROR_NOT_READY:
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
		/// Returns false if INVALID_FILE_ATTRIBUTES or if relative path. No exceptions.
		/// </summary>
		static unsafe bool _GetAttributes(string path, out FileAttributes attr, bool useRawPath) {
			if (!useRawPath) path = pathname.NormalizeMinimally_(path, false);
			_DisableDeviceNotReadyMessageBox();
			attr = Api.GetFileAttributes(path);
			if (attr == (FileAttributes)(-1) && !_GetAttributesOnError(path, FAFlags.DontThrow, out attr)) return false;
			if (!useRawPath && !pathname.isFullPath(path)) { lastError.code = Api.ERROR_FILE_NOT_FOUND; return false; }
			return true;
		}

		/// <summary>
		/// Gets file or directory attributes as <see cref="FAttr"/> that tells whether it exists, is directory, symbolic link, readonly, hidden, system. See examples.
		/// </summary>
		/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If useRawPath is false (default), supports environment variables (see <see cref="pathname.expand"/>). Can be null.</param>
		/// <param name="useRawPath">Pass path to the API as it is, without any normalizing and full-path checking.</param>
		/// <remarks>
		/// Supports <see cref="lastError"/>. If you need exception when fails, instead call <see cref="getAttributes"/> and check attribute Directory.
		/// Always use full path. If path is not full: if useRawPath is false (default), can't find the file; if useRawPath is true, searches in "current directory".
		/// For symbolic links gets attributes of the link, not target; does not care whether its target exists.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// var path = @"C:\Test\test.txt";
		/// if (filesystem.exists(path)) print.it("exists");
		/// if (filesystem.exists(path).isFile) print.it("exists as file");
		/// if (filesystem.exists(path).isDir) print.it("exists as directory");
		/// if (filesystem.exists(path) is FAttr { isFile: true, isReadonly: false }) print.it("exists as file and isn't readonly");
		/// switch (filesystem.exists(path)) {
		/// case 0: print.it("doesn't exist"); break;
		/// case 1: print.it("file"); break;
		/// case 2: print.it("dir"); break;
		/// }
		/// ]]></code>
		/// </example>
		public static FAttr exists(string path, bool useRawPath = false) {
			if (_GetAttributes(path, out var a, useRawPath)) return new(a, true);
			return new(0, (a == (FileAttributes)(-1)) ? null : false);
		}

		/// <summary>
		/// Gets file system entry type - file, directory, symbolic link, whether it exists and is accessible.
		/// Returns NotFound (0) if does not exist. Returns AccessDenied (&lt; 0) if exists but this process cannot access it and get attributes.
		/// Calls API <msdn>GetFileAttributes</msdn>.
		/// </summary>
		/// <param name="path">Full path. Supports <c>@"\.."</c> etc. If useRawPath is false (default), supports environment variables (see <see cref="pathname.expand"/>). Can be null.</param>
		/// <param name="useRawPath">Pass path to the API as it is, without any normalizing and full-path checking.</param>
		/// <remarks>
		/// Supports <see cref="lastError"/>. If you need exception when fails, instead call <see cref="getAttributes"/> and check attributes Directory and ReparsePoint.
		/// Always use full path. If path is not full: if useRawPath is false (default) returns NotFound; if useRawPath is true, searches in "current directory".
		/// </remarks>
		internal static unsafe FileIs_ ExistsAs_(string path, bool useRawPath = false) {
			if (!_GetAttributes(path, out var a, useRawPath)) {
				return (a == (FileAttributes)(-1)) ? FileIs_.AccessDenied : FileIs_.NotFound;
			}
			var R = (0 != (a & FileAttributes.Directory)) ? FileIs_.Directory : FileIs_.File;
			if (0 != (a & FileAttributes.ReparsePoint)) R |= (FileIs_)4;
			return R;
		}

		/// <summary>
		/// Finds file or directory and returns full path.
		/// Returns null if cannot be found.
		/// </summary>
		/// <remarks>
		/// If the path argument is full path, calls <see cref="exists"/> and returns normalized path if exists, null if not.
		/// Else searches in these places:
		///	1. <i>dirs</i>, if used.
		/// 2. <see cref="folders.ThisApp"/>.
		/// 3. Calls API <msdn>SearchPath</msdn>, which searches in process directory, Windows system directories, current directory, PATH environment variable. The search order depends on API <msdn>SetSearchPathMode</msdn> or registry settings.
		/// 4. If path ends with ".exe", tries to get path from registry "App Paths" keys.
		/// </remarks>
		/// <param name="path">Full or relative path or just filename with extension. Supports network paths too.</param>
		/// <param name="dirs">0 or more directories where to search.</param>
		public static unsafe string searchPath(string path, params string[] dirs) {
			if (path.NE()) return null;

			string s = path;
			if (pathname.isFullPathExpand(ref s)) {
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

			if (Api.SearchPath(null, path) is string s1) return s1;

			if (path.Ends(".exe", true) && path.FindAny(@"\/") < 0) {
				try {
					path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\" + path, "", null) as string
						?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\" + path, "", null) as string;
					if (path != null) {
						path = pathname.normalize(path.Trim('\"'));
						if (exists(path, true)) return path;
					}
				}
				catch (Exception ex) { Debug_.Print(path + "    " + ex.Message); }
			}

			return null;
		}

		/// <summary>
		/// Gets names and other info of files and subdirectories in the specified directory.
		/// Returns an enumerable collection of <see cref="FEFile"/> objects containing the info.
		/// </summary>
		/// <param name="directoryPath">Full path of the directory.</param>
		/// <param name="flags"></param>
		/// <param name="filter">
		/// Callback function. Called for each file and subdirectory. If returns false, the file/subdirectory is not included in results.
		/// Can be useful when <b>Enumerate</b> is called indirectly, for example by the <see cref="copy"/> method. If you call it directly, you can instead skip the file in your foreach loop.
		/// Example: <c>filter: k => k.IsDirectory || k.Name.Ends(".png", true)</c>. See <see cref="FEFile.IsDirectory"/>.
		/// </param>
		/// <param name="errorHandler">
		/// Callback function. Called when fails to get children of a subdirectory, when using flag <see cref="FEFlags.AndSubdirectories"/>.
		/// Receives the subdirectory path. Can call <see cref="lastError"/><b>.Code</b> and throw an exception. If does not throw, the enumeration continues as if the directory is empty.
		/// If <i>errorHandler</i> not used, then <b>Enumerate</b> throws exception. See also: flag <see cref="FEFlags.IgnoreInaccessible"/>.
		/// </param>
		/// <exception cref="ArgumentException"><i>directoryPath</i> is invalid path or not full path.</exception>
		/// <exception cref="DirectoryNotFoundException"><i>directoryPath</i> directory does not exist.</exception>
		/// <exception cref="AuException">Failed to get children of <i>directoryPath</i> or of a subdirectory.</exception>
		/// <remarks>
		/// Uses API <msdn>FindFirstFile</msdn>.
		/// 
		/// By default gets only direct children. Use flag <see cref="FEFlags.AndSubdirectories"/> to get all descendants.
		/// 
		/// The paths that this function gets are normalized, ie may not start with exact <i>directoryPath</i> string. Expanded environment variables (see <see cref="pathname.expand"/>), "..", DOS path etc.
		/// Paths longer than <see cref="pathname.maxDirectoryPathLength"/> have <c>@"\\?\"</c> prefix (see <see cref="pathname.prefixLongPathIfNeed"/>).
		/// 
		/// For symbolic links and mounted folders, gets info of the link/folder and not of its target.
		/// 
		/// These errors are ignored:
		/// 1. Missing target directory of a symbolic link or mounted folder.
		/// 2. If used flag <see cref="FEFlags.IgnoreInaccessible"/> - access denied.
		/// 
		/// When an error is ignored, the function works as if that [sub]directory is empty; does not throw exception and does not call <i>errorHandler</i>.
		/// 
		/// Enumeration of a subdirectory starts immediately after the subdirectory itself is retrieved.
		/// </remarks>
		public static IEnumerable<FEFile> enumerate(string directoryPath, FEFlags flags = 0, Func<FEFile, bool> filter = null, Action<string> errorHandler = null) {
			//tested 2021-04-30: much faster than Directory.GetFiles in .NET 5. Faster JIT, and then > 2 times faster.

			string path = directoryPath;
			if (0 == (flags & FEFlags.UseRawPath)) path = pathname.normalize(path);
			path = path.RemoveSuffix('\\');

			_DisableDeviceNotReadyMessageBox();

			var d = new Api.WIN32_FIND_DATA();
			IntPtr hfind = default;
			var stack = new Stack<_EDStackEntry>();
			bool isFirst = true;
			FileAttributes attr = 0;
			int basePathLength = path.Length;
			var redir = new more.DisableRedirection();

			try {
				if (0 != (flags & FEFlags.DisableRedirection)) redir.Disable();

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
							case Api.ERROR_PATH_NOT_FOUND: //the directory not found, or symlink target directory is missing
							case Api.ERROR_DIRECTORY: //it is file, not directory. Error text is "The directory name is invalid".
							case Api.ERROR_BAD_NETPATH: //eg \\COMPUTER\MissingFolder
								if (stack.Count == 0 && !exists(path, true).isDir)
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

					var r = new FEFile(name, fp2, d, stack.Count);

					if (filter != null && !filter(r)) continue;

					yield return r;

					if (!isDir || (flags & FEFlags.AndSubdirectories) == 0 || r._skip) continue;
					if ((attr & FileAttributes.ReparsePoint) != 0 && (flags & FEFlags.AndSymbolicLinkSubdirectories) == 0) continue;
					stack.Push(new _EDStackEntry() { hfind = hfind, path = path });
					hfind = default; path = fullPath;
					isFirst = true;
				}
			}
			finally {
				if (hfind != default) Api.FindClose(hfind);
				while (stack.Count > 0) Api.FindClose(stack.Pop().hfind);

				redir.Revert();
			}
		}

		struct _EDStackEntry { internal IntPtr hfind; internal string path; }

		#endregion

		#region move, copy, rename, delete

		enum _FileOpType { Rename, Move, Copy, }

		static unsafe void _FileOp(_FileOpType opType, bool into, string path1, string path2, FIfExists ifExists, FCFlags copyFlags, Func<FEFile, bool> filter) {
			string opName = (opType == _FileOpType.Rename) ? "rename" : ((opType == _FileOpType.Move) ? "move" : "copy");
			path1 = _PreparePath(path1);
			var type1 = ExistsAs_(path1, true);
			if (type1 <= 0) throw new FileNotFoundException($"Failed to {opName}. File not found: '{path1}'");

			if (opType == _FileOpType.Rename) {
				opType = _FileOpType.Move;
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

			bool ok = false, copy = opType == _FileOpType.Copy, deleteSource = false, mergeDirectory = false;
			var del = new _SafeDeleteExistingDirectory();
			try {
				if (ifExists == FIfExists.MergeDirectory && type1 != FileIs_.Directory) ifExists = FIfExists.Fail;

				if (ifExists == FIfExists.Fail) {
					//API will fail if exists. We don't use use API flags 'replace existing'.
				} else {
					//Delete, RenameExisting, MergeDirectory
					//bool deleted = false;
					var existsAs = ExistsAs_(path2, true);
					switch (existsAs) {
					case FileIs_.NotFound:
						//deleted = true;
						break;
					case FileIs_.AccessDenied:
						break;
					default:
						if (more.IsSameFile_(path1, path2)) {
							//eg renaming "file.txt" to "FILE.txt"
							Debug_.Print("same file");
							//deleted = true;
							//copy will fail, move will succeed
						} else if (ifExists == FIfExists.MergeDirectory && (existsAs == FileIs_.Directory || existsAs == FileIs_.SymLinkDirectory)) {
							if (type1 == FileIs_.Directory || type1 == FileIs_.SymLinkDirectory) {
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
							_DeleteL(path2, existsAs == FileIs_.SymLinkDirectory);
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
							_CopyDirectory(path1, path2, mergeDirectory, copyFlags, filter);
							ok = true;
						}
						catch (Exception ex) when (opType != _FileOpType.Copy) {
							throw new AuException($"*{opName} '{path1}' to '{path2}'", ex);
						}
					} else {
						if (type1 == FileIs_.SymLinkDirectory)
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
		static unsafe void _CopyDirectory(string path1, string path2, bool merge, FCFlags copyFlags, Func<FEFile, bool> filter) {
			//FUTURE: add progressInterface parameter. Create a default interface implementation class that supports progress dialog and/or progress in taskbar button. Or instead create a ShellCopy function.
			//FUTURE: maybe add errorHandler parameter. Call it here when fails to copy, and also pass to Enumerate which calls it when fails to enum.

			//use intermediate array, and get it before creating path2 directory. It requires more memory, but is safer. Without it, eg bad things happen when copying a directory into itself.
			var edFlags = FEFlags.AndSubdirectories | FEFlags.NeedRelativePaths | FEFlags.UseRawPath | (FEFlags)copyFlags;
			var a = enumerate(path1, edFlags, filter).ToArray();

			bool ok = false;
			string s1 = null, s2 = null;
			if (!merge) {
				if (!path1.Ends(@":\")) ok = Api.CreateDirectoryEx(path1, path2, default);
				if (!ok) ok = Api.CreateDirectory(path2, default);
				if (!ok) goto ge;
			}

			//foreach(var f in Enumerate(path1, edFlags, filter)) { //no, see comments above
			foreach (var f in a) {
				s1 = f.FullPath; s2 = pathname.prefixLongPathIfNeed(path2 + f.Name);
				//print.it(s1, s2);
				//continue;
				if (f.IsDirectory) {
					if (merge) switch (exists(s2, true)) {
						case 2: continue; //never mind: check symbolic link mismatch
						case 1: _DeleteL(s2, false); break;
						}

					ok = Api.CreateDirectoryEx(s1, s2, default);
					if (!ok && 0 == (f.Attributes & FileAttributes.ReparsePoint)) ok = Api.CreateDirectory(s2, default);
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
					if (0 != (f.Attributes & FileAttributes.ReparsePoint)) {
						//To create or copy symbolic links, need SeCreateSymbolicLinkPrivilege privilege.
						//Admins have it, else this process cannot get it.
						//More info: MS technet -> "Create symbolic links".
						//Debug_.Print($"failed to copy symbolic link '{s1}'. It's OK, skipped it. Error: {lastError.messageFor()}");
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

		struct _SafeDeleteExistingDirectory
		{
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
		/// <param name="newName">New name without path. Example: "name.txt".</param>
		/// <param name="ifExists"></param>
		/// <exception cref="ArgumentException">
		/// - <i>path</i> is not full path (see <see cref="pathname.isFullPath"/>).
		/// - <i>newName</i> is invalid filename.
		/// </exception>
		/// <exception cref="FileNotFoundException">The file (path) does not exist or cannot be found.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Uses API <msdn>MoveFileEx</msdn>.
		/// </remarks>
		public static void rename(string path, string newName, FIfExists ifExists = FIfExists.Fail) {
			_FileOp(_FileOpType.Rename, false, path, newName, ifExists, 0, null);
		}

		/// <summary>
		/// Moves (changes path of) file or directory.
		/// </summary>
		/// <param name="path">Full path.</param>
		/// <param name="newPath">
		/// New full path.
		/// <note>It is not the new parent directory. Use <see cref="moveTo"/> for it.</note>
		/// </param>
		/// <param name="ifExists"></param>
		/// <exception cref="ArgumentException">path or newPath is not full path (see <see cref="pathname.isFullPath"/>).</exception>
		/// <exception cref="FileNotFoundException">The source file (path) does not exist or cannot be found.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// In most cases uses API <msdn>MoveFileEx</msdn>. It's fast, because don't need to copy files.
		/// In these cases copies/deletes: destination is on another drive; need to merge directories.
		/// When need to copy, does not copy security properties; sets default.
		/// 
		/// Creates the destination directory if does not exist (see <see cref="createDirectory"/>).
		/// If path and newPath share the same parent directory, just renames the file.
		/// </remarks>
		public static void move(string path, string newPath, FIfExists ifExists = FIfExists.Fail) {
			_FileOp(_FileOpType.Move, false, path, newPath, ifExists, 0, null);
		}

		/// <summary>
		/// Moves file or directory into another directory.
		/// </summary>
		/// <param name="path">Full path.</param>
		/// <param name="newDirectory">New parent directory.</param>
		/// <param name="ifExists"></param>
		/// <exception cref="ArgumentException">
		/// - <i>path</i> or <i>newDirectory</i> is not full path (see <see cref="pathname.isFullPath"/>).
		/// - <i>path</i> is drive. To move drive content, use <see cref="move"/>.
		/// </exception>
		/// <exception cref="FileNotFoundException">The source file (path) does not exist or cannot be found.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// In most cases uses API <msdn>MoveFileEx</msdn>. It's fast, because don't need to copy files.
		/// In these cases copies/deletes: destination is on another drive; need to merge directories.
		/// When need to copy, does not copy security properties; sets default.
		/// 
		/// Creates the destination directory if does not exist (see <see cref="createDirectory"/>).
		/// </remarks>
		public static void moveTo(string path, string newDirectory, FIfExists ifExists = FIfExists.Fail) {
			_FileOp(_FileOpType.Move, true, path, newDirectory, ifExists, 0, null);
		}

		/// <summary>
		/// Copies file or directory.
		/// </summary>
		/// <param name="path">Full path.</param>
		/// <param name="newPath">
		/// New full path.
		/// <note>It is not the new parent directory. Use <see cref="copyTo"/> for it.</note>
		/// </param>
		/// <param name="ifExists"></param>
		/// <param name="copyFlags">Options used when copying directory.</param>
		/// <param name="filter">
		/// Callback function. Can be used when copying directory. Called for each descendant file and subdirectory.
		/// If returns false, the file/subdirectory is not copied.
		/// Example: <c>filter: k => k.IsDirectory || k.Name.Ends(".png", true)</c>. See <see cref="FEFile.IsDirectory"/>.
		/// </param>
		/// <exception cref="ArgumentException">path or newPath is not full path (see <see cref="pathname.isFullPath"/>).</exception>
		/// <exception cref="FileNotFoundException">The source file (path) does not exist or cannot be found.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Uses API <msdn>CopyFileEx</msdn>.
		/// On Windows 7 does not copy security properties; sets default.
		/// Does not copy symbolic links (silently skips, no exception) if this process is not running as administrator.
		/// Creates the destination directory if does not exist (see <see cref="createDirectory"/>).
		/// </remarks>
		public static void copy(string path, string newPath, FIfExists ifExists = FIfExists.Fail, FCFlags copyFlags = 0, Func<FEFile, bool> filter = null) {
			_FileOp(_FileOpType.Copy, false, path, newPath, ifExists, copyFlags, filter);
		}

		/// <summary>
		/// Copies file or directory into another directory.
		/// </summary>
		/// <param name="path">Full path.</param>
		/// <param name="newDirectory">New parent directory.</param>
		/// <param name="ifExists"></param>
		/// <param name="copyFlags">Options used when copying directory.</param>
		/// <param name="filter">See <see cref="copy"/>.</param>
		/// <exception cref="ArgumentException">
		/// - <i>path</i> or <i>newDirectory</i> is not full path (see <see cref="pathname.isFullPath"/>).
		/// - <i>path</i> is drive. To copy drive content, use <see cref="copy"/>.
		/// </exception>
		/// <exception cref="FileNotFoundException">The source file (path) does not exist or cannot be found.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// Uses API <msdn>CopyFileEx</msdn>.
		/// On Windows 7 does not copy security properties; sets default.
		/// Does not copy symbolic links (silently skips, no exception) if this process is not running as administrator.
		/// Creates the destination directory if does not exist (see <see cref="createDirectory"/>).
		/// </remarks>
		public static void copyTo(string path, string newDirectory, FIfExists ifExists = FIfExists.Fail, FCFlags copyFlags = 0, Func<FEFile, bool> filter = null) {
			_FileOp(_FileOpType.Copy, true, path, newDirectory, ifExists, copyFlags, filter);
		}

		/// <summary>
		/// Deletes file or directory.
		/// Does nothing if it does not exist (no exception).
		/// </summary>
		/// <param name="path">Full path.</param>
		/// <param name="recycleBin">
		/// Send to the Recycle Bin. If not possible, delete anyway.
		/// Why could be not possible: 1. The file is in a removable drive (most removables don't have a recycle bin). 2. The file is too large. 3. The path is too long. 4. The Recycle Bin is not used on that drive (it can be set in the Recycle Bin Properties dialog). 5. This process is non-UI-interactive, eg a service. 6. Unknown reasons.
		/// Note: it is much slower. To delete multiple, use <see cref="delete(IEnumerable{string}, bool)"/>.
		/// </param>
		/// <exception cref="ArgumentException">path is not full path (see <see cref="pathname.isFullPath"/>).</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// If directory, also deletes all its files and subdirectories. If fails to delete some, tries to delete as many as possible.
		/// Deletes read-only files too.
		/// Does not show any message boxes etc (confirmation, error, UAC consent, progress).
		/// 
		/// Some reasons why this function can fail:
		/// 1. The file is open (in any process). Or a file in the directory is open.
		/// 2. This process does not have security permissions to access or delete the file or directory or some of its descendants.
		/// 3. The directory is (or contains) the "current directory" (in any process).
		/// </remarks>
		public static void delete(string path, bool recycleBin = false) {
			path = _PreparePath(path);
			_Delete(path, recycleBin);
		}

		/// <summary>
		/// Deletes multiple files or/and directories.
		/// The same as <see cref="delete(string, bool)"/>, but faster when using Recycle Bin.
		/// </summary>
		/// <param name="paths">string array, List or other collection. Full paths.</param>
		/// <param name="recycleBin"></param>
		/// <exception cref="ArgumentException">path is not full path (see <see cref="pathname.isFullPath"/>).</exception>
		/// <exception cref="AuException">Failed.</exception>
		public static void delete(IEnumerable<string> paths, bool recycleBin = false) {
			if (recycleBin) {
				var a = new List<string>();
				foreach (var v in paths) {
					var s = _PreparePath(v);
					if (exists(s, true)) a.Add(s);
				}
				if (a.Count == 0) return;
				if (_DeleteShell(null, true, a)) return;
				Debug_.Print("_DeleteShell failed");
			}
			foreach (var v in paths) delete(v);
		}

		/// <summary>
		/// note: path must be normalized.
		/// </summary>
		static FileIs_ _Delete(string path, bool recycleBin = false) {
			var type = ExistsAs_(path, true);
			if (type == FileIs_.NotFound) return type;
			if (type == FileIs_.AccessDenied) throw new AuException(0, $"*delete '{path}'");

			if (recycleBin) {
				if (_DeleteShell(path, true)) return type;
				Debug_.Print("_DeleteShell failed");
			}

			int ec = 0;
			if (type == FileIs_.Directory) {
				var dirs = new List<string>();
				foreach (var f in enumerate(path, FEFlags.AndSubdirectories | FEFlags.UseRawPath | FEFlags.IgnoreInaccessible)) {
					if (f.IsDirectory) dirs.Add(f.FullPath);
					else _DeleteL(f.FullPath, false); //delete as many as possible
				}
				for (int i = dirs.Count - 1; i >= 0; i--) {
					_DeleteL(dirs[i], true); //delete as many as possible
				}
				ec = _DeleteL(path, true);
				if (ec == 0) {
					//notify shell. Else, if it was open in Explorer, it shows an error message box.
					//Info: .NET does not notify; SHFileOperation does.
					ShellNotify_(Api.SHCNE_RMDIR, path);
					return type;
				}
				Debug_.Print("Using _DeleteShell.");
				//if(_DeleteShell(path, Recycle.No)) return type;
				if (_DeleteShell(path, false)) return type;
			} else {
				ec = _DeleteL(path, type == FileIs_.SymLinkDirectory);
				if (ec == 0) return type;
			}
			if (exists(path, true)) throw new AuException(ec, $"*delete '{path}'");

			//info:
			//RemoveDirectory fails if not empty.
			//Directory.Delete fails if a descendant is read-only, etc.
			//Also both fail if a [now deleted] subfolder containing files was open in Explorer. Workaround: sleep/retry.
			//_DeleteShell usually does not have these problems. But it is very slow.
			//But all fail if it is current directory in any process. If in current process, _DeleteShell succeeds; it makes current directory = its parent.
			return type;
		}

		static int _DeleteL(string path, bool dir) {
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
			for (int i = 1; (ec == Api.ERROR_SHARING_VIOLATION || ec == Api.ERROR_LOCK_VIOLATION || ec == Api.ERROR_DIR_NOT_EMPTY) && i <= 50; i++) {
				//ERROR_DIR_NOT_EMPTY: see comments above about Explorer. Also fails in other cases, eg when a file was opened in a web browser.
				string es = ec == Api.ERROR_DIR_NOT_EMPTY ? "ERROR_DIR_NOT_EMPTY when empty. Retry " : "ERROR_SHARING_VIOLATION. Retry ";
				Debug_.PrintIf(i > 1, es + i.ToString());
				Thread.Sleep(15);
				if (dir ? Api.RemoveDirectory(path) : Api.DeleteFile(path)) return 0;
				ec = lastError.code;
			}
			if (ec == Api.ERROR_FILE_NOT_FOUND || ec == Api.ERROR_PATH_NOT_FOUND) return 0;
			Debug_.Print("_DeleteL failed. " + lastError.messageFor(ec) + "  " + path
				+ (dir ? ("   Children: " + string.Join(" | ", enumerate(path).Select(f => f.Name))) : null));
			return ec;

			//never mind: .NET also calls DeleteVolumeMountPoint if it is a mounted folder.
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
		/// Returns true if created new directory, false if the directory already exists. Throws exception if fails.
		/// </summary>
		/// <param name="path">Path of new directory.</param>
		/// <param name="templateDirectory">Optional path of a template directory from which to copy some properties. See API <msdn>CreateDirectoryEx</msdn>.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>
		/// If the directory already exists, this function does nothing, and returns false.
		/// Else, at first it creates missing parent/ancestor directories, then creates the specified directory.
		/// To create the specified directory, calls API <msdn>CreateDirectory</msdn> or <msdn>CreateDirectoryEx</msdn> (if templateDirectory is not null).
		/// </remarks>
		public static bool createDirectory(string path, string templateDirectory = null) {
			return _CreateDirectory(path, templateDirectory: templateDirectory);
		}

		/// <summary>
		/// Creates parent directory for a new file, if does not exist.
		/// The same as <see cref="createDirectory"/>, just removes filename from <i>filePath</i>.
		/// Returns true if created new directory, false if the directory already exists. Throws exception if fails.
		/// </summary>
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

		static bool _CreateDirectory(string path, bool pathIsPrepared = false, string templateDirectory = null) {
			if (exists(path, pathIsPrepared).isDir) return false;
			if (!pathIsPrepared) path = _PreparePath(path);
			if (templateDirectory != null) templateDirectory = _PreparePath(templateDirectory);

			var stack = new Stack<string>();
			var s = path;
			do {
				stack.Push(s);
				s = _RemoveFilename(s, true);
				if (s == null) throw new AuException($@"*create directory '{path}'. Drive not found.");
			} while (!exists(s, true).isDir);

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
		}

		/// <summary>
		/// Expands environment variables (see <see cref="pathname.expand"/>). Throws ArgumentException if not full path. Normalizes. Removes or adds <c>'\\'</c> at the end.
		/// </summary>
		/// <exception cref="ArgumentException">Not full path.</exception>
		static string _PreparePath(string path) {
			if (!pathname.isFullPathExpand(ref path)) throw new ArgumentException($"Not full path: '{path}'.");
			return pathname.Normalize_(path, noExpandEV: true);
		}

		/// <summary>
		/// Finds filename, eg <c>@"b.txt"</c> in <c>@"c:\a\b.txt"</c>.
		/// </summary>
		/// <exception cref="ArgumentException"><c>'\\'</c> not found or is at the end. If noException, instead returns -1.</exception>
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
		/// <exception cref="ArgumentException"><c>'\\'</c> not found or is at the end. If noException, instead returns null.</exception>
		static string _RemoveFilename(string path, bool noException = false) {
			int i = _FindFilename(path, noException); if (i < 0) return null;
			return path[..--i];
		}

		/// <summary>
		/// Gets filename, eg <c>@"c:\a\b.txt"</c> -> <c>@"b.txt"</c>.
		/// </summary>
		/// <exception cref="ArgumentException"><c>'\\'</c> not found or is at the end. If noException, instead returns null.</exception>
		static string _GetFilename(string path, bool noException = false) {
			int i = _FindFilename(path, noException); if (i < 0) return null;
			return path[i..];
		}

		/// <summary>
		/// Returns true if character <c>c == '\\' || c == '/'</c>.
		/// </summary>
		static bool _IsSepChar(char c) { return c == '\\' || c == '/'; }
		#endregion

		#region open

		/// <summary>
		/// This function can be used to safely open a file that may be temporarily locked (used by another process or thread). Waits while the file is locked.
		/// </summary>
		/// <returns>Returns the return value of the lambda <i>f</i>.</returns>
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

		/// <exception cref="ArgumentOutOfRangeException"><i>millisecondsTimeout</i> less than -1.</exception>
		/// <exception cref="Exception">Exceptions thrown by the called function.</exception>
		/// <example>
		/// <code><![CDATA[
		/// File.WriteAllText(file, "TEXT"); //unsafe. Exception if the file is locked.
		/// 
		/// filesystem.waitIfLocked(() => File.WriteAllText(file, "TEXT")); //safe. Waits while the file is locked.
		/// ]]></code>
		/// </example>
		public static void waitIfLocked(Action f, int millisecondsTimeout = 2000) {
			var w = new _LockedWaiter(millisecondsTimeout);
			g1:
			try { f(); }
			catch (IOException e) when (w.ExceptionFilter(e)) { w.Sleep(); goto g1; }
		}

		struct _LockedWaiter
		{
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
		/// Uses <see cref="File.ReadAllText(string)"/> and <see cref="waitIfLocked{T}(Func{T}, int)"/>.
		/// </summary>
		/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
		/// <param name="encoding">Text encoding in file. Default <b>Encoding.UTF8</b>.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="File.ReadAllText(string)"/>.</exception>
		public static string loadText(string file, Encoding encoding = null) {
			file = pathname.NormalizeForNET_(file);
			return waitIfLocked(() => File.ReadAllText(file, encoding ?? Encoding.UTF8));
			//FUTURE: why ReadAllText so slow when file contains 17_000_000 empty lines?
			//	230 - 1600 ms. It seems makes so much garbage that triggers GC.
			//	QM2 reads+converts to UTF16 in 55 ms.
		}

		/// <summary>
		/// Loads file in a safer way.
		/// Uses <see cref="File.ReadAllBytes(string)"/> and <see cref="waitIfLocked{T}(Func{T}, int)"/>.
		/// </summary>
		/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="File.ReadAllBytes(string)"/>.</exception>
		public static byte[] loadBytes(string file) {
			file = pathname.NormalizeForNET_(file);
			return waitIfLocked(() => File.ReadAllBytes(file));
		}

		/// <summary>
		/// Loads file in a safer way.
		/// Uses <see cref="File.OpenRead(string)"/> and <see cref="waitIfLocked{T}(Func{T}, int)"/>.
		/// </summary>
		/// <param name="file">File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of <see cref="File.OpenRead(string)"/>.</exception>
		public static FileStream loadStream(string file) {
			file = pathname.NormalizeForNET_(file);
			return waitIfLocked(() => File.OpenRead(file));
		}

		/// <summary>
		/// Writes any data to a file in a safe way, using a callback function.
		/// </summary>
		/// <param name="file">
		/// File. Must be full path. Can contain environment variables etc, see <see cref="pathname.expand"/>.
		/// The file can exist or not; this function overwrites it. If the directory does not exist, this function creates it.
		/// </param>
		/// <param name="writer">
		/// Callback function (lambda etc) that creates/writes/closes a temporary file. Its parameter is the full path of the temporary file, which normally does not exist.
		/// May be called multiple times, because this function retries if the file is locked or if the directory does not exist (if writer throws <b>DirectoryNotFoundException</b> exception).
		/// </param>
		/// <param name="backup">Create backup file named <i>file</i> + "~backup".</param>
		/// <param name="tempDirectory">
		/// Directory for backup file and temporary file. If null or "" - <i>file</i>'s directory. Can contain environment variales etc.
		/// Must be in the same drive as <i>file</i>. If the directory does not exist, this function creates it.</param>
		/// <param name="lockedWaitMS">If cannot open file because it is open by another process etc, wait max this number of milliseconds. Can be <see cref="Timeout.Infinite"/> (-1).</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="Exception">Exceptions of the <i>writer</i> function.</exception>
		/// <exception cref="IOException">Failed to replace file. The <i>writer</i> function also can thow it.</exception>
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
			_Save(file, writer ?? throw new ArgumentNullException(), backup, tempDirectory, lockedWaitMS);
		}

#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
		/// <summary>
		/// Writes text to a file in a safe way (like <see cref="save"/>), using <see cref="File.WriteAllText"/>.
		/// </summary>
		/// <param name="text">Text to write.</param>
		/// <param name="encoding">Text encoding in file. Default is UTF-8 without BOM.</param>
		/// <exception cref="ArgumentException" />
		/// <exception cref="Exception" />
		/// <exception cref="IOException" />
		/// <inheritdoc cref="save"/>
		public static void saveText(string file, string text, bool backup = false, string tempDirectory = null, int lockedWaitMS = 2000, Encoding encoding = null) {
			_Save(file, text ?? "", backup, tempDirectory, lockedWaitMS, encoding);
		}

		/// <summary>
		/// Writes data to a file in a safe way (like <see cref="save"/>), using <see cref="File.WriteAllBytes"/>.
		/// </summary>
		/// <param name="bytes">Data to write.</param>
		/// <exception cref="ArgumentException" />
		/// <exception cref="Exception" />
		/// <exception cref="IOException" />
		/// <inheritdoc cref="save"/>
		public static void saveBytes(string file, byte[] bytes, bool backup = false, string tempDirectory = null, int lockedWaitMS = 2000) {
			_Save(file, bytes ?? throw new ArgumentNullException(), backup, tempDirectory, lockedWaitMS);
		}
#pragma warning restore CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)

		static void _Save(string file, object data, bool backup, string tempDirectory, int lockedWaitMS, Encoding encoding = null) {
			file = pathname.normalize(file, flags: PNFlags.DontPrefixLongPath);
			string s1 = tempDirectory.NE() ? file : pathname.combine(pathname.normalize(tempDirectory, flags: PNFlags.DontPrefixLongPath), pathname.getName(file), prefixLongPath: false);
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
			catch (DirectoryNotFoundException) when (_AutoCreateDir(temp)) { goto g1; }
			catch (IOException e) when (w.ExceptionFilter(e)) { w.Sleep(); goto g1; }

			w = new _LockedWaiter(lockedWaitMS);
			g2:
			string es = null;
			if (exists(file, true).isFile) {
				if (!Api.ReplaceFile(file, temp, back, 6)) es = "save"; //random ERROR_UNABLE_TO_REMOVE_REPLACED; _LockedWaiter knows it
				else if (backup) ShellNotify_(Api.SHCNE_RENAMEITEM, temp, file); //without it Explorer shows 2 files with filename of temp
				else if (!Api.DeleteFile(back)) Debug_.PrintNativeError_(); //maybe should wait/retry if failed, but never noticed
			} else {
				if (!Api.MoveFileEx(temp, file, Api.MOVEFILE_REPLACE_EXISTING)) es = "create";
			}
			if (es != null) {
				int ec = lastError.code;
				if (ec == Api.ERROR_PATH_NOT_FOUND && _AutoCreateDir(file)) goto g2;
				if (w.ExceptionFilter(ec)) { w.Sleep(); goto g2; }
				throw new IOException($"Failed to {es} file '{file}'. {lastError.messageFor(ec)}", ec);
			}

			static bool _AutoCreateDir(string filePath) {
				try { return createDirectoryFor(filePath); }
				catch { return false; }
			}
		}

		#endregion
	}
}

namespace Au.Types
{
	//CONSIDER: remove. Use FAttr instead.
	/// <summary>
	/// File system entry type - file, directory, symbolic link, whether it exists and is accessible.
	/// The enum value NotFound is 0; AccessDenied is negative ((int)0x80000000); other values are greater than 0.
	/// </summary>
	internal enum FileIs_
	{
		/// <summary>Does not exist.</summary>
		NotFound = 0,
		/// <summary>Is file. Attributes: Directory no, ReparsePoint no.</summary>
		File = 1,
		/// <summary>Is directory. Attributes: Directory yes, ReparsePoint no.</summary>
		Directory = 2,
		/// <summary>Is symbolic link to a file. Attributes: Directory no, ReparsePoint yes.</summary>
		SymLinkFile = 5,
		/// <summary>Is symbolic link to a directory, or is a mounted folder. Attributes: Directory yes, ReparsePoint yes.</summary>
		SymLinkDirectory = 6,
		/// <summary>Exists but this process cannot access it and get attributes.</summary>
		AccessDenied = int.MinValue,
	}

	/// <summary>
	/// Contains file or directory attributes. Tells whether it exists, is directory, symbolic link, readonly, hidden, system.
	/// See <see cref="filesystem.exists"/>.
	/// </summary>
	public struct FAttr
	{
		readonly FileAttributes _a;
		readonly bool _exists, _unknown;

		/// <param name="attributes">Attributes, or 0 if does not exist or can't get attributes.</param>
		/// <param name="exists">True if exists and can get attributes. False if does not exist. null if exists but can't get attributes.</param>
		public FAttr(FileAttributes attributes, bool? exists) { _a = attributes; _exists = exists == true; _unknown = exists == null; }

		/// <summary>
		/// Returns file or directory attributes. Returns 0 if <see cref="exists"/> false.
		/// </summary>
		public FileAttributes attr => _a;

		/// <summary>
		/// Returns <see cref="exists"/>.
		/// </summary>
		public static implicit operator bool(FAttr fa) => fa.exists;

		/// <summary>
		/// Returns 0 if !<see cref="exists"/>, 1 if <see cref="isFile"/>, 2 if <see cref="isDir"/>. Can be used with switch.
		/// </summary>
		public static implicit operator int(FAttr fa) => !fa.exists ? 0 : (fa.isDir ? 2 : 1);

		/// <summary>
		/// Exists and is accessible (<see cref="unknown"/> false).
		/// See also <see cref="isFile"/>, <see cref="isDir"/>.
		/// </summary>
		public bool exists => _exists;

		/// <summary>
		/// Exists but this process cannot access it and get attributes (error "access denied"). Then other bool properties return false.
		/// </summary>
		public bool unknown => _unknown;

		/// <summary>
		/// Is file (not directory), or symbolic link to a file (if <see cref="isSymlink"/> true).
		/// </summary>
		public bool isFile => 0 == (_a & FileAttributes.Directory) && _exists;

		/// <summary>
		/// Is directory, or symbolic link to a directory (if <see cref="isSymlink"/> true).
		/// </summary>
		public bool isDir => 0 != (_a & FileAttributes.Directory);

		/// <summary>
		/// Has <see cref="FileAttributes.ReparsePoint"/>.
		/// If <see cref="isFile"/> true, it is symbolic link to a file. If <see cref="isDir"/> true, it is symbolic link to a directory or is a mounted folder.
		/// </summary>
		public bool isSymlink => 0 != (_a & FileAttributes.ReparsePoint);

		/// <summary>
		/// Has <see cref="FileAttributes.ReadOnly"/>.
		/// </summary>
		public bool isReadonly => 0 != (_a & FileAttributes.ReadOnly);

		/// <summary>
		/// Has <see cref="FileAttributes.Hidden"/>.
		/// </summary>
		public bool isHidden => 0 != (_a & FileAttributes.Hidden);

		/// <summary>
		/// Has <see cref="FileAttributes.System"/>.
		/// </summary>
		public bool isSystem => 0 != (_a & FileAttributes.System);

		///
		public override string ToString() {
			return unknown ? "unknown" : (exists ? $"{{ isDir={isDir}, isSymlink={isSymlink}, attr={attr} }}" : "doesn't exist");
		}
	}

	/// <summary>
	/// Flags for <see cref="filesystem.getAttributes"/> and some other functions.
	/// </summary>
	[Flags]
	public enum FAFlags
	{
		///<summary>Pass path to the API as it is, without any normalizing and validating.</summary>
		UseRawPath = 1,

		///<summary>
		///If failed, return false and don't throw exception.
		///Then, if you need error info, you can use <see cref="lastError"/>. If the file/directory does not exist, it will return ERROR_FILE_NOT_FOUND or ERROR_PATH_NOT_FOUND or ERROR_NOT_READY.
		///If failed and the native error code is ERROR_ACCESS_DENIED or ERROR_SHARING_VIOLATION, the returned attributes will be (FileAttributes)(-1). The file probably exists but is protected so that this process cannot access and use it. Else attributes will be 0.
		///</summary>
		DontThrow = 2,
	}

	/// <summary>
	/// File or directory properties. Used with <see cref="filesystem.getProperties"/>.
	/// </summary>
	public struct FileProperties
	{
		///
		public FileAttributes Attributes;

		///<summary>File size. For directories it is usually 0.</summary>
		public long Size;

		///
		public DateTime LastWriteTimeUtc;

		///
		public DateTime CreationTimeUtc;

		///<summary>Note: this is unreliable. The operating system may not record this time automatically.</summary>
		public DateTime LastAccessTimeUtc;
	}

	/// <summary>
	/// flags for <see cref="filesystem.enumerate"/>.
	/// </summary>
	[Flags]
	public enum FEFlags
	{
		/// <summary>
		/// Enumerate subdirectories too.
		/// </summary>
		AndSubdirectories = 1,

		/// <summary>
		/// Also enumerate symbolic link and mounted folder target directories. Use with AndSubdirectories.
		/// </summary>
		AndSymbolicLinkSubdirectories = 2,

		/// <summary>
		/// Skip files and subdirectories that have Hidden attribute.
		/// </summary>
		SkipHidden = 4,

		/// <summary>
		/// Skip files and subdirectories that have Hidden and System attributes (both).
		/// These files/directories usually are created and used only by the operating system. Drives usually have several such directories. Another example - thumbnail cache files.
		/// Without this flag the function skips only these hidden-system root directories when enumerating a drive: <c>"$Recycle.Bin"</c>, <c>"System Volume Information"</c>, <c>"Recovery"</c>. If you want to include them too, use network path of the drive, for example <c>@"\\localhost\D$\"</c> for D drive.
		/// </summary>
		SkipHiddenSystem = 8,

		/// <summary>
		/// If fails to get contents of the directory or a subdirectory because of its security settings, assume that the [sub]directory is empty.
		/// Without this flag then throws exception or calls errorHandler.
		/// </summary>
		IgnoreInaccessible = 0x10,

		/// <summary>
		/// Temporarily disable file system redirection in this thread of this 32-bit process running on 64-bit Windows.
		/// Then you can enumerate the 64-bit System32 folder in your 32-bit process.
		/// Uses API <msdn>Wow64DisableWow64FsRedirection</msdn>.
		/// For vice versa (in 64-bit process enumerate the 32-bit System folder), instead use path folders.SystemX86.
		/// </summary>
		DisableRedirection = 0x20,

		/// <summary>
		/// Don't call <see cref="pathname.normalize"/>(directoryPath) and don't throw exception for non-full path.
		/// </summary>
		UseRawPath = 0x40,

		/// <summary>
		/// Let <see cref="FEFile.Name"/> be path relative to the specified directory path. Like <c>@"\name.txt"</c> or <c>@"\subdirectory\name.txt"</c> instead of "name.txt".
		/// </summary>
		NeedRelativePaths = 0x80,
	}

	/// <summary>
	/// flags for <see cref="filesystem.copy"/> and some other similar functions.
	/// Used only when copying directory.
	/// </summary>
	[Flags]
	public enum FCFlags
	{
		//note: these values must match the corresponding FEFlags values.

		/// <summary>
		/// Skip descendant files and directories that have Hidden and System attributes (both).
		/// They usually are created and used only by the operating system. Drives usually have several such directories. Another example - thumbnail cache files.
		/// They often are protected and would fail to copy, ruining whole copy operation.
		/// Without this flag the function skips only these hidden-system root directories when enumerating a drive: "$Recycle.Bin", "System Volume Information", "Recovery".
		/// </summary>
		SkipHiddenSystem = 8,

		/// <summary>
		/// If fails to get contents of the directory or a subdirectory because of its security settings, don't throw exception but assume that the [sub]directory is empty.
		/// </summary>
		IgnoreInaccessible = 0x10,
	}

	/// <summary>
	/// Contains name and other main properties of a file or subdirectory retrieved by <see cref="filesystem.enumerate"/>.
	/// The values are not changed after creating the variable.
	/// </summary>
	public class FEFile
	{
		///
		internal FEFile(string name, string fullPath, in Api.WIN32_FIND_DATA d, int level) {
			Name = name; FullPath = fullPath;
			Attributes = d.dwFileAttributes;
			Size = (long)d.nFileSizeHigh << 32 | d.nFileSizeLow;
			LastWriteTimeUtc = DateTime.FromFileTimeUtc(d.ftLastWriteTime); //fast, sizeof 8
			CreationTimeUtc = DateTime.FromFileTimeUtc(d.ftCreationTime);
			_level = (short)level;
		}

		///
		public string Name { get; }

		///
		public string FullPath { get; }

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
		/// Returns true if is directory or symbolic link to a directory or mounted folder.
		/// </summary>
		public bool IsDirectory { get { return (Attributes & FileAttributes.Directory) != 0; } }

		/// <summary>
		/// Descendant level.
		/// 0 if direct child of directoryPath, 1 if child of child, and so on.
		/// </summary>
		public int Level { get { return _level; } }
		short _level;

		/// <summary>
		/// Call this function if don't want to enumerate children of this subdirectory.
		/// </summary>
		public void SkipThisDirectory() { _skip = true; }
		internal bool _skip;

		/// <summary>
		/// Returns FullPath.
		/// </summary>
		public override string ToString() { return FullPath; }

		//This could be more dangerous than useful.
		///// <summary>
		///// Returns FullPath.
		///// </summary>
		//public static implicit operator string(FEFile f) { return f?.FullPath; }
	}

	/// <summary>
	/// What to do if the destination directory contains a file or directory with the same name as the source file or directory when copying, moving or renaming.
	/// Used with <see cref="filesystem.copy"/>, <see cref="filesystem.move"/> and similar functions.
	/// When renaming or moving, if the destination is the same as the source, these options are ignored and the destination is simply renamed. For example when renaming "file.txt" to "FILE.TXT".
	/// </summary>
	public enum FIfExists
	{
		/// <summary>Throw exception. Default.</summary>
		Fail,
		/// <summary>Delete destination.</summary>
		Delete,
		/// <summary>Rename (backup) destination.</summary>
		RenameExisting,
		/// <summary>
		/// If destination directory exists, merge the source directory into it, replacing existing files.
		/// If destination file exists, deletes it.
		/// If destination directory exists and source is file, fails.
		/// </summary>
		MergeDirectory,
#if not_implemented
			/// <summary>Copy/move with a different name.</summary>
			RenameNew,
			/// <summary>Display a dialog asking the user what to do.</summary>
			AskUser,
#endif
	}
}
