namespace Au;

partial class filesystem {
	/// <summary>
	/// Miscellaneous rarely used file/directory functions.
	/// </summary>
	public static partial class more {
		/// <summary>
		/// Returns <c>true</c> if two paths are of the same existing file, regardless of path format etc.
		/// </summary>
		/// <param name="path1">Path of a file or directory. Supports environment variables (see <see cref="pathname.expand"/>) and paths relative to current directory.</param>
		/// <param name="path2">Path of a file or directory. Supports environment variables (see <see cref="pathname.expand"/>) and paths relative to current directory.</param>
		/// <param name="useSymlink">If a path is of a symbolic link, use the link. If <c>false</c>, uses its target; for example, returns <c>false</c> if the target doesn't exist.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <seealso cref="comparePaths(string, string, bool, bool)"/>
		public static bool isSameFile(string path1, string path2, bool useSymlink = false) {
			using var h1 = _OpenFileHandleForFileInfo(path1, useSymlink); if (h1.Is0) return false;
			using var h2 = _OpenFileHandleForFileInfo(path2, useSymlink); if (h2.Is0) return false;
			return Api.GetFileInformationByHandle(h1, out var k1)
				&& Api.GetFileInformationByHandle(h2, out var k2)
				&& k1.FileIndex == k2.FileIndex && k1.dwVolumeSerialNumber == k2.dwVolumeSerialNumber;
		}
		
		/// <summary>
		/// Gets <see cref="FileId"/> of a file or directory.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		/// <param name="path">Path of a file or directory. Supports environment variables (see <see cref="pathname.expand"/>) and paths relative to current directory.</param>
		/// <param name="fileId"></param>
		/// <param name="ofSymlink">If <i>path</i> is of a symbolic link, get <b>FileId</b> of the link, not of its target.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <remarks>
		/// Calls API <msdn>GetFileInformationByHandle</msdn>.
		/// 
		/// A file id can be used to uniquely identify a file or directory regardless of path format.
		/// 
		/// Note: later the function can get a different id for the same path. For example after deleting the file and then creating new file at the same path (some apps save files in this way). You may want to use <see cref="getFinalPath"/> instead.
		/// </remarks>
		public static unsafe bool getFileId(string path, out FileId fileId, bool ofSymlink = false) {
			using var h = _OpenFileHandleForFileInfo(path, ofSymlink);
			if (h.Is0 || !Api.GetFileInformationByHandle(h, out var k)) { fileId = default; return false; }
			fileId = new((int)k.dwVolumeSerialNumber, k.FileIndex);
			return true;
		}
		
		static Handle_ _OpenFileHandleForFileInfo(string path, bool ofSymlink = false) {
			path = pathname.NormalizeMinimally_(path, throwIfNotFullPath: false);
			return Api.CreateFile(path, 0, Api.FILE_SHARE_ALL, Api.OPEN_EXISTING, ofSymlink ? Api.FILE_FLAG_BACKUP_SEMANTICS | Api.FILE_FLAG_OPEN_REPARSE_POINT : Api.FILE_FLAG_BACKUP_SEMANTICS);
			//info: need FILE_FLAG_BACKUP_SEMANTICS for directories. Ignored for files.
		}
		
		/// <summary>
		/// Gets full normalized path of an existing file or directory or symbolic link target.
		/// </summary>
		/// <returns><c>false</c> if failed. For example if the path does not point to an existing file or directory. Supports <see cref="lastError"/>.</returns>
		/// <param name="path">Full or relative path. Supports environment variables (see <see cref="pathname.expand"/>).</param>
		/// <param name="result">Receives full path, or <c>null</c> if failed.</param>
		/// <param name="ofSymlink">If <i>path</i> is of a symbolic link, get final path of the link, not of its target.</param>
		/// <param name="format">Result format.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <remarks>
		/// Calls API <msdn>GetFinalPathNameByHandle</msdn>.
		/// 
		/// Unlike <see cref="pathname.normalize"/>, this function works with existing files and directories, not any strings.
		/// </remarks>
		/// <seealso cref="shortcutFile.getTarget(string)"/>
		public static bool getFinalPath(string path, out string result, bool ofSymlink = false, FPFormat format = FPFormat.PrefixIfLong) {
			result = null;
			using var h = _OpenFileHandleForFileInfo(path, ofSymlink);
			if (h.Is0 || !Api.GetFinalPathNameByHandle(h, out var s, format == FPFormat.VolumeGuid ? 1u : 0u)) return false;
			if (format == FPFormat.PrefixNever || (format == FPFormat.PrefixIfLong && s.Length <= pathname.maxDirectoryPathLength))
				s = pathname.unprefixLongPath(s);
			result = s;
			return true;
			
			//never mind: does not change the root if it is like @"\\ThisComputer\share" or @"\\ThisComputer\C$" or @"\\127.0.0.1\c$" or @"\\LOCALHOST\c$" and it is the same as "C:\".
			//	Tested: getFileId returns the same value for all these.
		}
		
		/// <summary>
		/// Compares final paths of two existing files or directories to determine equality or relationship.
		/// </summary>
		/// <param name="pathA">Full or relative path of an existing file or directory, in any format. Supports environment variables (see <see cref="pathname.expand"/>).</param>
		/// <param name="pathB">Full or relative path of an existing file or directory, in any format. Supports environment variables (see <see cref="pathname.expand"/>).</param>
		/// <param name="ofSymlinkA">If <i>pathA</i> is of a symbolic link, get final path of the link, not of its target.</param>
		/// <param name="ofSymlinkB">If <i>pathB</i> is of a symbolic link, get final path of the link, not of its target.</param>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <remarks>
		/// Before comparing, calls <see cref="getFinalPath"/>, therefore paths can have any format.
		/// Example: <c>@"C:\Test\"</c> and <c>@"C:\A\..\Test"</c> are equal.
		/// Example: <c>@"C:\Test\file.txt"</c> and <c>"file.txt"</c> are equal if the file is in <c>@"C:\Test</c> and <c>@"C:\Test</c> is current directory.
		/// Example: <c>@"C:\Temp\file.txt"</c> and <c>"%TEMP%\file.txt"</c> are equal if TEMP is an environment variable = <c>@"C:\Temp</c>.
		/// </remarks>
		/// <seealso cref="isSameFile(string, string, bool)"/>
		public static CPResult comparePaths(string pathA, string pathB, bool ofSymlinkA = false, bool ofSymlinkB = false)
			=> comparePaths(ref pathA, ref pathB, ofSymlinkA, ofSymlinkB);
		
		/// <summary>
		/// Compares final paths of two existing files or directories to determine equality or relationship.
		/// Also gets final paths (see <see cref="getFinalPath"/>).
		/// </summary>
		/// <inheritdoc cref="comparePaths(string, string, bool, bool)"/>
		public static CPResult comparePaths(ref string pathA, ref string pathB, bool ofSymlinkA = false, bool ofSymlinkB = false) {
			if (!getFinalPath(pathA, out pathA, ofSymlinkA, FPFormat.PrefixAlways)) return CPResult.Failed;
			if (!getFinalPath(pathB, out pathB, ofSymlinkB, FPFormat.PrefixAlways)) return CPResult.Failed;
			//print.it(pathA, pathB);
			if (pathA.Eqi(pathB)) return CPResult.Same;
			if (pathA.Length < pathB.Length && pathB.Starts(pathA, true) && (pathB[pathA.Length] == '\\' || pathA.Ends('\\'))) return CPResult.AContainsB;
			if (pathB.Length < pathA.Length && pathA.Starts(pathB, true) && (pathA[pathB.Length] == '\\' || pathB.Ends('\\'))) return CPResult.BContainsA;
			return CPResult.None;
		}
		
		/// <summary>
		/// Calls <see cref="enumerate"/> and returns the sum of all descendant file sizes.
		/// With default flags, it includes sizes of all descendant files, in this directory and all subdirectories except in inaccessible [sub]directories.
		/// </summary>
		/// <param name="path">Full path.</param>
		/// <param name="flags"><b>Enumerate</b> flags.</param>
		/// <exception cref="Exception">Exceptions of <see cref="enumerate"/>. By default no exceptions if used full path and the directory exists.</exception>
		/// <remarks>
		/// This function is slow if the directory is large.
		/// Don't use this function for files (throws exception) and drives (instead use <see cref="DriveInfo"/>, it's fast and includes sizes of Recycle Bin and other protected hidden system directories).
		/// </remarks>
		public static long calculateDirectorySize(string path, FEFlags flags = FEFlags.AllDescendants | FEFlags.IgnoreInaccessible) {
			return enumerate(path, flags).Sum(f => f.Size);
		}
		
		/// <summary>
		/// Empties the Recycle Bin.
		/// </summary>
		/// <param name="drive">If not <c>null</c>, empties the Recycle Bin on this drive only. Example: <c>"D:"</c>.</param>
		/// <param name="progressUI">Show progress dialog if slow. Default <c>true</c>.</param>
		public static void emptyRecycleBin(string drive = null, bool progressUI = false) {
			Api.SHEmptyRecycleBin(default, drive, progressUI ? 1 : 7);
		}
		
		/// <summary>
		/// Creates a NTFS symbolic link or junction.
		/// </summary>
		/// <param name="linkPath">Full path of the link. Supports environment variables etc.</param>
		/// <param name="targetPath">If <i>type</i> is <b>Junction</b>, must be full path. Else can be either full path or path relative to the parent directory of the link. If starts with an environment variable, the function expands it before creating the link.</param>
		/// <param name="type"></param>
		/// <param name="elevate">If fails to create symbolic link because this process does not have admin rights, run <c>cmd.exe mklink</c> as administrator. Will show a dialog and UAC consent. Not used if type is <b>Junction</b>, because don't need admin rights to create junctions.</param>
		/// <param name="deleteOld">If <i>linkPath</i> already exists as a symbolic link or junction, replace it.</param>
		/// <remarks>
		/// Some reasons why this function can fail:
		/// - The link already exists. Solution: use <c>deleteOld: true</c>.
		/// - This process is running not as administrator. Solution: use <i>type</i> <b>Junction</b> or <c>elevate: true</c>. To create symbolic links without admin rights, in Windows Settings enable developer mode.
		/// - The file system format is not NTFS. For example FAT32 in USB drive.
		/// 
		/// More info: <google>CreateSymbolicLink, mklink, NTFS symbolic links, junctions</google>.
		/// </remarks>
		/// <exception cref="ArgumentException">Not full path.</exception>
		/// <exception cref="AuException">Failed.</exception>
		public static void createSymbolicLink(string linkPath, string targetPath, CSLink type, bool elevate = false, bool deleteOld = false) {
			linkPath = pathname.normalize(linkPath);
			if (type is CSLink.Junction) {
				targetPath = pathname.normalize(targetPath); //junctions don't support relative path
			} else { //symlinks support relative path
				targetPath = targetPath.Replace('/', '\\'); //rumors: the link may not work if with /
			}
			
			string trueLinkPath = null;
			try {
				if (exists(linkPath, useRawPath: true) is var e && e) {
					if (!(deleteOld && e.IsNtfsLink)) throw new AuException(Api.ERROR_ALREADY_EXISTS, "*to create symbolic link.");
					trueLinkPath = linkPath;
					linkPath = pathname.makeUnique(linkPath, true);
				} else createDirectoryFor(linkPath);
				
				if (type is CSLink.Junction or CSLink.JunctionOrSymlink) {
					var r = run.console(out string s, "cmd.exe", $"""/u /c "mklink /d /j "{linkPath}" "{targetPath}" """, encoding: Encoding.Unicode); //tested: UTF-16 on Win11 and Win7
					if (r == 0) return;
					if (!(type == CSLink.JunctionOrSymlink && s.Starts("Local volumes are required"))) throw new AuException("*to create junction. " + s.Trim());
				}
				
				uint fl = type == CSLink.File ? 0u : 1u; //SYMBOLIC_LINK_FLAG_DIRECTORY
				if (osVersion.minWin10_1703) fl |= 2u; //SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE
				if (Api.CreateSymbolicLink(linkPath, targetPath, fl)) return;
				
				int ec = lastError.code;
				if (ec == Api.ERROR_PRIVILEGE_NOT_HELD && elevate && !uacInfo.isAdmin) {
					if (dialog.showOkCancel("Create symbolic link", "Administrator rights required.\n\nTo create without admin rights, in Windows Settings enable developer mode.", icon: DIcon.Shield)) {
						using var tf = new TempFile();
						var d = type == CSLink.File ? null : "/d ";
						var cl = $"""/u /c "mklink {d}"{linkPath}" "{targetPath}" 2>"{tf}" """; //redirects stderr to temp file
						var r = run.it(folders.System + "cmd.exe", cl, RFlags.Admin | RFlags.WaitForExit, new() { WindowState = ProcessWindowStyle.Hidden });
						if (r.ProcessExitCode == 0) return;
						string s = null; try { s = loadText(tf, encoding: Encoding.Unicode).Trim(); } catch { }
						throw new AuException("*to create symbolic link. " + s);
					}
				}
				throw new AuException(ec, "*to create symbolic link.");
			}
			finally { if (trueLinkPath != null && filesystem.exists(linkPath)) move(linkPath, trueLinkPath, FIfExists.Delete); }
		}
		
		/// <summary>
		/// Loads unmanaged dll of correct 64/32 bitness.
		/// </summary>
		/// <param name="fileName">Dll file name like "name.dll".</param>
		/// <exception cref="DllNotFoundException"></exception>
		/// <remarks>
		/// If your program uses an unmanaged dll and can run as either 64-bit or 32-bit process, you need 2 versions of the dll - 64-bit and 32-bit. If not using deps.json, let they live in subfolders "64" and "32" of your program folder. They must have same name. This function loads correct dll version. Then [DllImport("dll")] will use the loaded dll. Don't need two different DllImport for functions ([DllImport("dll64")] and [DllImport("dll32")]).
		/// 
		/// Looks in:
		/// - subfolder "64" or "32" of the Au.dll folder.
		/// - calls NativeLibrary.TryLoad, which works like [DllImport], eg may use info from deps.json.
		/// - subfolder "64" or "32" of folder specified in environment variable "Au.Path". For example the dll is unavailable if used in an assembly (managed dll) loaded in a nonstandard environment, eg VS forms designer or VS C# Interactive (then folders.ThisApp is "C:\Program Files (x86)\Microsoft Visual Studio\..."). Workaround: set %Au.Path% = the main Au directory and restart Windows.
		/// </remarks>
		internal unsafe static void LoadDll64or32Bit_(string fileName) {
			//Debug.Assert(default == Api.GetModuleHandle(fileName)); //no, asserts if cpp dll is injected by acc

			string rel = (sizeof(nint) == 4 ? @"32\" : @"64\") + fileName;
			//note: don't use osVersion.is32BitProcess here. Its static ctor makes this func slower at startup.
			//	And folders.ThisAppBS is slow first time, therefore call AppContext.BaseDirectory directly.

			//Au.dll dir + 64/32
			var asm = typeof(more).Assembly;
			if (asm.Location is [_, ..] s1) {
				s1 = s1[..(s1.LastIndexOf('\\') + 1)] + rel;
				if (NativeLibrary.TryLoad(s1, out _)) return;
			}

			//like [DllImport]. It uses NATIVE_DLL_SEARCH_DIRECTORIES, which probably was built at startup from deps.json.
			//	Also finds in temp dir when <PublishSingleFile>+<IncludeNativeLibrariesForSelfExtract>.
			if (NativeLibrary.TryLoad(fileName, asm, null, out _)) return;

			//environment variable + 64/32
			if (Environment.GetEnvironmentVariable("Au.Path") is string s2)
				if (NativeLibrary.TryLoad(pathname.combine(s2, rel), out _)) return;

			throw new DllNotFoundException(fileName + " not found");
		}
		
		#region garbage
		
#if false //currently not used
		/// <summary>
		/// Gets HKEY_CLASSES_ROOT registry key of file type or protocol.
		/// The key usually contains subkeys "shell", "DefaultIcon", sometimes "shellex" and more.
		/// For example, for ".txt" can return "txtfile", for ".cs" - "VisualStudio.cs.14.0".
		/// Looks in "HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts" and in HKEY_CLASSES_ROOT.
		/// Returns <c>null</c> if the type/protocol is not registered.
		/// Returns <c>null</c> if <i>fileType</i> does not end with ".extension" and does not start with "protocol:"; also if starts with "shell:".
		/// </summary>
		/// <param name="fileType">
		/// File type extension like ".txt" or protocol like "http:".
		/// Can be full path or URL; the function gets extension or protocol from the string.
		/// Can start with %environment variable%.
		/// </param>
		/// <param name="isFileType">Don't parse <i>fileType</i>, it does not contain full path or URL or environment variables. It is ".ext" or "protocol:".</param>
		/// <param name="isURL">fileType is URL or protocol like "http:". Used only if <c>isFileType == true</c>, ie it is protocol.</param>
		internal static string getFileTypeOrProtocolRegistryKey(string fileType, bool isFileType, bool isURL)
		{
			if(!isFileType) fileType = GetExtensionOrProtocol(fileType, out isURL);
			else if(isURL) fileType = fileType.RemoveSuffix(1); //"proto:" -> "proto"
			if(fileType.NE()) return null;

			string userChoiceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + fileType + @"\UserChoice";
			if(Registry.GetValue(userChoiceKey, "ProgId", null) is string s1) return s1;
			if(isURL) return fileType;
			if(Registry.ClassesRoot.GetValue(fileType, null) is string s2) return s2;
			return null;

			//note: IQueryAssociations.GetKey is very slow.
		}

		/// <summary>
		/// Gets file path extension like ".txt" or URL protocol like "http".
		/// Returns <c>null</c> if path does not end with ".extension" and does not start with "protocol:"; also if starts with "shell:".
		/// </summary>
		/// <param name="path">File path or URL. Can be just extension like ".txt" or protocol like "http:".</param>
		/// <param name="isProtocol">Receives <c>true</c> if URL or protocol.</param>
		internal static string GetExtensionOrProtocol(string path, out bool isProtocol)
		{
			isProtocol = false;
			if(path.NE()) return null;
			if(!PathIsExtension(path)) {
				int i = path.IndexOf(':');
				if(i > 1) {
					path = path[..i]; //protocol
					if(path == "shell") return null; //eg "shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"
					isProtocol = true;
				} else {
					path = pathname.getExtension(path);
					if(path.NE()) return null;
				}
			}
			return path;
		}
#endif
		
#if false
	//this is ~300 times slower than filesystem.move. SHFileOperation too. Use only for files or other shell items in virtual folders. Unfinished.
	public static void renameFileOrDirectory(string path, string newName)
	{
		perf.first();
		if(pathname.isInvalidName(newName)) throw new ArgumentException("Invalid filename.", nameof(newName));
		path = _PreparePath(path, nameof(path));

		perf.next();
		var si = _ShellItem(path, "*rename");
		perf.next();
		var fo = new api.FileOperation() as api.IFileOperation;
		perf.next();
		try {
			fo.SetOperationFlags(4); //FOF_SILENT. Without it shows a hidden dialog that becomes the active window.
			AuException.ThrowIfFailed(fo.RenameItem(si, newName, null), "*rename");
			perf.next();
			AuException.ThrowIfFailed(fo.PerformOperations(), "*rename");
			perf.next();
		}
		finally {
			Api.ReleaseComObject(fo);
			Api.ReleaseComObject(si);
		}
		perf.nw();
	}

	static api.IShellItem _ShellItem(string path, string errMsg)
	{
		var pidl = More.PidlFromString(path, true);
		try {
			var guid = typeof(api.IShellItem).GUID;
			AuException.ThrowIfFailed(api.SHCreateItemFromIDList(pidl, guid, out var R), errMsg);
			return R;
		}
		finally { Marshal.FreeCoTaskMem(pidl); }
	}

	static class api
	{
		[DllImport("shell32.dll", PreserveSig = true)]
		internal static extern int SHCreateItemFromIDList(IntPtr pidl, in Guid riid, out IShellItem ppv);

		[ComImport, Guid("3ad05575-8857-4850-9277-11b85bdb8e09"), ClassInterface(ClassInterfaceType.None)]
		internal class FileOperation { }

		[ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IFileOperation
		{
			[PreserveSig] int Advise(IFileOperationProgressSink pfops, out uint pdwCookie);
			[PreserveSig] int Unadvise(uint dwCookie);
			[PreserveSig] int SetOperationFlags(uint dwOperationFlags);
			[PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
			[PreserveSig] int SetProgressDialog(IOperationsProgressDialog popd);
			[PreserveSig] int SetProperties(IntPtr pproparray); //IPropertyChangeArray
			[PreserveSig] int SetOwnerWindow(wnd hwndOwner);
			[PreserveSig] int ApplyPropertiesToItem(IShellItem psiItem);
			[PreserveSig] int ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] Object punkItems);
			[PreserveSig] int RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IFileOperationProgressSink pfopsItem);
			[PreserveSig] int RenameItems([MarshalAs(UnmanagedType.IUnknown)] Object pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
			[PreserveSig] int MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IFileOperationProgressSink pfopsItem);
			[PreserveSig] int MoveItems([MarshalAs(UnmanagedType.IUnknown)] Object punkItems, IShellItem psiDestinationFolder);
			[PreserveSig] int CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, IFileOperationProgressSink pfopsItem);
			[PreserveSig] int CopyItems([MarshalAs(UnmanagedType.IUnknown)] Object punkItems, IShellItem psiDestinationFolder);
			[PreserveSig] int DeleteItem(IShellItem psiItem, IFileOperationProgressSink pfopsItem);
			[PreserveSig] int DeleteItems([MarshalAs(UnmanagedType.IUnknown)] Object punkItems);
			[PreserveSig] int NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, IFileOperationProgressSink pfopsItem);
			[PreserveSig] int PerformOperations();
			[PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
		}

		[ComImport, Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IFileOperationProgressSink
		{
			[PreserveSig] int StartOperations();
			[PreserveSig] int FinishOperations(int hrResult);
			[PreserveSig] int PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
			[PreserveSig] int PostRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, int hrRename, IShellItem psiNewlyCreated);
			[PreserveSig] int PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
			[PreserveSig] int PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, int hrMove, IShellItem psiNewlyCreated);
			[PreserveSig] int PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
			[PreserveSig] int PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, int hrCopy, IShellItem psiNewlyCreated);
			[PreserveSig] int PreDeleteItem(uint dwFlags, IShellItem psiItem);
			[PreserveSig] int PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem psiNewlyCreated);
			[PreserveSig] int PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
			[PreserveSig] int PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem psiNewItem);
			[PreserveSig] int UpdateProgress(uint iWorkTotal, uint iWorkSoFar);
			[PreserveSig] int ResetTimer();
			[PreserveSig] int PauseTimer();
			[PreserveSig] int ResumeTimer();
		}

		[ComImport, Guid("0C9FB851-E5C9-43EB-A370-F0677B13874C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IOperationsProgressDialog
		{
			[PreserveSig] int StartProgressDialog(wnd hwndOwner, uint flags);
			[PreserveSig] int StopProgressDialog();
			[PreserveSig] int SetOperation(SPACTION action);
			[PreserveSig] int SetMode(uint mode);
			[PreserveSig] int UpdateProgress(ulong ullPointsCurrent, ulong ullPointsTotal, ulong ullSizeCurrent, ulong ullSizeTotal, ulong ullItemsCurrent, ulong ullItemsTotal);
			[PreserveSig] int UpdateLocations(IShellItem psiSource, IShellItem psiTarget, IShellItem psiItem);
			[PreserveSig] int ResetTimer();
			[PreserveSig] int PauseTimer();
			[PreserveSig] int ResumeTimer();
			[PreserveSig] int GetMilliseconds(out ulong pullElapsed, out ulong pullRemaining);
			[PreserveSig] int GetOperationStatus(out PDOPSTATUS popstatus);
		}

		internal enum SPACTION
		{
			SPACTION_NONE,
			SPACTION_MOVING,
			SPACTION_COPYING,
			SPACTION_RECYCLING,
			SPACTION_APPLYINGATTRIBS,
			SPACTION_DOWNLOADING,
			SPACTION_SEARCHING_INTERNET,
			SPACTION_CALCULATING,
			SPACTION_UPLOADING,
			SPACTION_SEARCHING_FILES,
			SPACTION_DELETING,
			SPACTION_RENAMING,
			SPACTION_FORMATTING,
			SPACTION_COPY_MOVING
		}

		internal enum PDOPSTATUS
		{
			PDOPS_RUNNING = 1,
			PDOPS_PAUSED,
			PDOPS_CANCELLED,
			PDOPS_STOPPED,
			PDOPS_ERRORS
		}


	}
#endif
		
		//rejected: unreliable. Uses registry, where many mimes are incorrect and nonconstant.
		//	Use System.Web.MimeMapping.GetMimeMapping. It uses a hardcoded list, although too small.
		///// <summary>
		///// Gets file's MIME content type, like "text/html" or "image/png".
		///// Returns <c>false</c> if cannot detect it.
		///// </summary>
		///// <param name="file">File name or path or URL or just extension like ".txt". If <i>canAnalyseData</i> is <c>true</c>, must be full path of a file, and the file must exist and can be opened to read; else the function uses just <c>.extension</c>, and the file may exist or not.</param>
		///// <param name="contentType">Result.</param>
		///// <param name="canAnalyseData">If cannot detect from file extension, try to detect from file data.</param>
		///// <exception cref="ArgumentException">Not full path. Only if <i>canAnalyseData</i> is <c>true</c>.</exception>
		///// <exception cref="Exception">Exceptions of <see cref="File.ReadAllBytes"/>. Only if <i>canAnalyseData</i> is <c>true</c>.</exception>
		///// <remarks>
		///// Uses API <msdn>FindMimeFromData</msdn>.
		///// </remarks>
		//public static bool getMimeContentType(string file, out string contentType, bool canAnalyseData = false)
		//{
		//	if(file.Ends(".cur", true)) { contentType = "image/x-icon"; return true; } //registered without MIME or with text/plain
		
		//	int hr = Api.FindMimeFromData(default, file, null, 0, null, 0, out contentType, 0);
		//	if(hr != 0 && canAnalyseData) {
		//		file = pathname.normalize(file);
		//		using(var stream = File.OpenRead(file)) {
		//			var data = new byte[256];
		//			int n = stream.Read(data, 0, 256);
		//			hr = Api.FindMimeFromData(default, null, data, n, null, 0, out contentType, 0);
		//		}
		//	}
		//	return hr == 0;
		//	//note: the returned unmanaged string is freed with CoTaskMemFree, which uses HeapFree(GetProcessHeap).
		//	//	In MSDN it is documented incorrectly: "should be freed with the operator delete function".
		//	//	To discover it, call HeapSize(GetProcessHeap) before and after CoTaskMemFree. It returns -1 when called after.
		//}
		
		#endregion
	}
}
