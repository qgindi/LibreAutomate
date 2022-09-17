
namespace Au.Types;

/// <summary>
/// File system entry type - file, directory, NTFS link, whether it exists and is accessible.
/// The enum value NotFound is 0; AccessDenied is negative ((int)0x80000000); other values are greater than 0.
/// </summary>
internal enum FileIs_ {
	/// <summary>Does not exist.</summary>
	NotFound = 0,

	/// <summary>Is file, and not NTFS link.</summary>
	File = 1,

	/// <summary>Is directory, and not NTFS link.</summary>
	Directory = 2,

	/// <summary>Is NTFS link to file.</summary>
	NtfsLinkFile = 5,

	/// <summary>Is NTFS link to directory.</summary>
	NtfsLinkDirectory = 6,

	/// <summary>Exists but this process cannot access it and get attributes.</summary>
	AccessDenied = int.MinValue,
}

/// <summary>
/// Contains file or directory attributes. Tells whether it exists, is directory, readonly, hidden, system, NTFS link.
/// See <see cref="filesystem.exists"/>.
/// </summary>
public struct FAttr {
	readonly FileAttributes _a;
	readonly bool _exists, _unknown, _ntfsLink;

	/// <param name="attributes">Attributes, or 0 if does not exist or can't get attributes.</param>
	/// <param name="exists">True if exists and can get attributes. False if does not exist. null if exists but can't get attributes.</param>
	/// <param name="ntfsLink">Is a NTFS link, such as symbolic link or mounted folder.</param>
	internal FAttr(FileAttributes attributes, bool? exists, bool ntfsLink) {
		_a = attributes;
		_exists = exists == true;
		_unknown = exists == null;
		_ntfsLink = ntfsLink;
	}

	/// <summary>
	/// Returns file or directory attributes. Returns 0 if <see cref="Exists"/> false.
	/// </summary>
	public FileAttributes Attributes => _a;

	/// <summary>
	/// Returns <see cref="Exists"/>.
	/// </summary>
	public static implicit operator bool(FAttr fa) => fa.Exists;

	/// <summary>
	/// Returns 0 if !<see cref="Exists"/>, 1 if <see cref="File"/>, 2 if <see cref="Directory"/>. Can be used with switch.
	/// </summary>
	public static implicit operator int(FAttr fa) => !fa.Exists ? 0 : (fa.Directory ? 2 : 1);

	/// <summary>
	/// Exists and is accessible (<see cref="Unknown"/> false).
	/// See also <see cref="File"/>, <see cref="Directory"/>.
	/// </summary>
	public bool Exists => _exists;

	/// <summary>
	/// Exists but this process cannot access it and get attributes (error "access denied"). Then other bool properties return false.
	/// </summary>
	public bool Unknown => _unknown;

	/// <summary>
	/// Is file (not directory), or NTFS link to a file (if <see cref="IsNtfsLink"/> true).
	/// </summary>
	public bool File => 0 == (_a & FileAttributes.Directory) && _exists;

	/// <summary>
	/// Is directory, or NTFS link to a directory (if <see cref="IsNtfsLink"/> true).
	/// </summary>
	public bool Directory => 0 != (_a & FileAttributes.Directory);

	/// <summary>
	/// It is a NTFS link, such as symbolic link or mounted folder. Don't confuse with shell links (shortcuts).
	/// If <see cref="File"/> true, the target is a file. If <see cref="Directory"/> true, the target is a directory.
	/// </summary>
	public bool IsNtfsLink => _ntfsLink;

	/// <summary>
	/// Has <see cref="FileAttributes.ReadOnly"/>.
	/// </summary>
	public bool IsReadonly => 0 != (_a & FileAttributes.ReadOnly);

	/// <summary>
	/// Has <see cref="FileAttributes.Hidden"/>.
	/// </summary>
	public bool IsHidden => 0 != (_a & FileAttributes.Hidden);

	/// <summary>
	/// Has <see cref="FileAttributes.System"/>.
	/// </summary>
	public bool IsSystem => 0 != (_a & FileAttributes.System);

	///
	public override string ToString() {
		return Unknown ? "unknown" : (Exists ? $"{{ Directory={Directory}, IsNtfsLink={IsNtfsLink}, Attributes={Attributes} }}" : "doesn't exist");
	}
}

/// <summary>
/// Flags for <see cref="filesystem.getAttributes"/> and some other functions.
/// </summary>
[Flags]
public enum FAFlags {
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
public record struct FileProperties {
	///
	public FileAttributes Attributes { get; set; }

	///<summary>File size. For directories it is usually 0.</summary>
	public long Size { get; set; }

	///
	public DateTime LastWriteTimeUtc { get; set; }

	///
	public DateTime CreationTimeUtc { get; set; }

	///<summary>Note: this is unreliable. The operating system may not record this time automatically.</summary>
	public DateTime LastAccessTimeUtc { get; set; }

	/// <summary>
	/// It is a NTFS link, such as symbolic link or mounted folder. Don't confuse with shell links (shortcuts).
	/// </summary>
	public bool IsNtfsLink { get; set; }
}

/// <summary>
/// flags for <see cref="filesystem.enumerate"/>.
/// </summary>
[Flags]
public enum FEFlags {
	/// <summary>
	/// Enumerate all descendants, not only direct children. Also known as "recurse subdirectories".
	/// </summary>
	AllDescendants = 1,

	/// <summary>
	/// Also enumerate target directories of NTFS links, such as symbolic links and mounted folders. Use with <b>AllDescendants</b>.
	/// </summary>
	RecurseNtfsLinks = 2,

	/// <summary>
	/// Skip files and subdirectories that have <b>Hidden</b> attribute.
	/// </summary>
	SkipHidden = 4,

	/// <summary>
	/// Skip files and subdirectories that have <b>Hidden</b> and <b>System</b> attributes (both).
	/// These files/directories usually are created and used only by the operating system. Drives usually have several such directories. Another example - thumbnail cache files.
	/// Without this flag the function skips only these hidden-system root directories when enumerating a drive: <c>"$Recycle.Bin"</c>, <c>"System Volume Information"</c>, <c>"Recovery"</c>. If you want to include them too, use network path of the drive, for example <c>@"\\localhost\D$\"</c> for D drive.
	/// </summary>
	SkipHiddenSystem = 8, //note: must match FCFlags

	/// <summary>
	/// If fails to get contents of the directory or a subdirectory because of its security settings, assume that the [sub]directory is empty.
	/// Without this flag then throws exception or calls <i>errorHandler</i>.
	/// </summary>
	IgnoreInaccessible = 0x10, //note: must match FCFlags

	/// <summary>
	/// Get only files and not subdirectories.
	/// Note: the <i>dirFilter</i> callback function is called just to ask whether to include children.
	/// </summary>
	OnlyFiles = 0x20,

	/// <summary>
	/// Don't call <see cref="pathname.normalize"/>(directoryPath) and don't throw exception for non-full path.
	/// </summary>
	UseRawPath = 0x40,

	/// <summary>
	/// Let <see cref="FEFile.Name"/> be path relative to the specified directory path. Like <c>@"\name.txt"</c> or <c>@"\subdirectory\name.txt"</c> instead of "name.txt".
	/// </summary>
	NeedRelativePaths = 0x80,

	//rejected. Rarely used. Can use FileSystemRedirection, it's public.
	///// <summary>
	///// Temporarily disable file system redirection in this thread of this 32-bit process running on 64-bit Windows.
	///// Then you can enumerate the 64-bit System32 folder in your 32-bit process.
	///// Uses API <msdn>Wow64DisableWow64FsRedirection</msdn>.
	///// For vice versa (in 64-bit process enumerate the 32-bit System folder), instead use path folders.SystemX86.
	///// </summary>
	//DisableRedirection = 0x100,
}

/// <summary>
/// flags for <see cref="filesystem.copy"/> and some other similar functions.
/// Used only when copying directory.
/// </summary>
[Flags]
public enum FCFlags {
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

	/// <summary>
	/// Don't create subdirectories that after applying all filters would be empty.
	/// </summary>
	NoEmptyDirectories = 0x10000,
}

/// <summary>
/// flags for <see cref="filesystem.delete"/>.
/// </summary>
[Flags]
public enum FDFlags {
	/// <summary>
	/// Send to the Recycle Bin. If not possible, delete anyway, unless used <i>CanFail</i>.
	/// Why could be not possible: 1. The file is in a removable drive (most removables don't have a recycle bin). 2. The file is too large. 3. The path is too long. 4. The Recycle Bin is not used on that drive (it can be set in the Recycle Bin Properties dialog). 5. This process is non-UI-interactive, eg a service. 6. Unknown reasons.
	/// Note: it is much slower. To delete multiple, use <see cref="filesystem.delete(IEnumerable{string}, FDFlags)"/>.
	/// </summary>
	RecycleBin = 1,

	/// <summary>
	/// If fails to delete, don't wait/retry and don't throw exception.
	/// </summary>
	CanFail = 2,

	//rejected. Rarely useful. Maybe in the future.
	///// <summary>
	///// Fail if has read-only attribute.
	///// </summary>
	//ReadonlyFail = 4,
}

/// <summary>
/// Contains name and other main properties of a file or subdirectory retrieved by <see cref="filesystem.enumerate"/>.
/// The values are not changed after creating the variable.
/// </summary>
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

	///
	public string Name { get; }

	///
	public string FullPath { get; }

	///
	public string Extension => IsDirectory ? "" : pathname.getExtension(Name); //note: if null for directory, then OrderBy throws exception

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
	public int Level => _level;
	readonly short _level;

	/// <summary>
	/// <msdn>WIN32_FIND_DATA</msdn>.dwReserved0.
	/// </summary>
	[EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	public uint ReparseTag { get; }

	/// <summary>
	/// It is a NTFS link, such as symbolic link or mounted folder. Don't confuse with shell links (shortcuts).
	/// </summary>
	public bool IsNtfsLink => Attributes.Has(FileAttributes.ReparsePoint) && 0 != (ReparseTag & 0x20000000);

	/// <summary>
	/// Returns FullPath.
	/// </summary>
	public override string ToString() => FullPath;

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
public enum FIfExists {
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

	/// <summary>Copy/move with a different name.</summary>
	RenameNew,

#if not_implemented
	/// <summary>Display a dialog asking the user what to do.</summary>
	Ask,
#endif
}
