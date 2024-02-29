#if !SCRIPT
namespace Au.More;
#endif

/// <summary>
/// Contains file infos of this and descendant folders and files retrieved by <see cref="filesystem.enumerate"/>.
/// Can print a formatted list of descendant sizes.
/// </summary>
public class FileTree : TreeBase<FileTree> {
	FEFile _f;
	
	FileTree(FEFile f) {
		if (f != null) {
			_f = f;
			Size = f.Size;
		}
	}
	
	/// <summary>
	/// Gets the info retrieved by <see cref="filesystem.enumerate"/>.
	/// </summary>
	public FEFile Info => _f;
	
	/// <summary>
	/// Filename.
	/// </summary>
	public string Name => _f.Name;
	
	/// <summary>
	/// Full path.
	/// </summary>
	public string Path => _f.FullPath;
	
	/// <inheritdoc cref="FEFile.IsDirectory"/>
	public bool IsDirectory => _f.IsDirectory;
	
	/// <summary>
	/// Gets the file size. If directory, it's the sum of all descendant file sizes.
	/// </summary>
	public long Size { get; private set; }
	
	/// <summary>
	/// Calls <see cref="filesystem.enumerate"/> and creates a tree of descendants.
	/// </summary>
	/// <param name="path">Folder path.</param>
	/// <param name="onlyDirectories">Don't include files.</param>
	/// <param name="minSize">Don't include smaller files and directories. The unit is bytes.</param>
	/// <param name="ignoreInaccessible">If cannot access some descendant directories, ignore them and don't throw exception. Default <c>true</c>.</param>
	/// <param name="recurseNtfsLinks">Enumerate target directories of NTFS links, such as symbolic links and mount points.</param>
	/// <param name="dirFilter">Called for each descendant directory. If returns <c>false</c>, that directory with descendants is not included. But its size contributes to ancestor sizes anyway.</param>
	/// <param name="fileFilter">Called for each descendant file. If returns <c>false</c>, that file is not included. But its size contributes to ancestor sizes anyway.</param>
	/// <returns>The root of the tree. You can use its descendants and <see cref="Size"/>. Don't use <b>Info</b>, <b>Name</b>, <b>Path</b> and <b>IsDirectory</b>.</returns>
	/// <exception cref="Exception">Exceptions of <see cref="filesystem.enumerate"/>.</exception>
	public static FileTree Create(string path, bool onlyDirectories = false, long minSize = 0, bool ignoreInaccessible = true, bool recurseNtfsLinks = false, Func<FEFile, bool> dirFilter = null, Func<FEFile, bool> fileFilter = null) {
		var flags = ignoreInaccessible ? FEFlags.IgnoreInaccessible : 0;
		if (recurseNtfsLinks) flags |= FEFlags.RecurseNtfsLinks;
		
		FileTree root = new(null);
		_Dir(path, root, 0, false, flags);
		return root;
		
		void _Dir(string path, FileTree x, int level, bool skipDescendants, FEFlags flags) {
			foreach (var f in filesystem.enumerate(path, flags)) {
				f.Level = level;
				if (f.IsDirectory) {
					bool skip = skipDescendants || (dirFilter != null && !dirFilter(f));
					var y = new FileTree(f);
					_Dir(f.FullPath, y, level + 1, skip, flags | FEFlags.UseRawPath);
					x.Size += y.Size;
					if (!skip && y.Size >= minSize) x.AddChild(y);
				} else {
					x.Size += f.Size;
					bool skip = skipDescendants || onlyDirectories || f.Size < minSize || (fileFilter != null && !fileFilter(f));
					if (!skip) x.AddChild(new(f));
				}
			}
		}
	}
	
	/// <summary>
	/// Appends to a <b>StringBuilder</b> a list of sizes and names of descendants, formatted for <see cref="print.it(string)"/>, without a header.
	/// </summary>
	public void PrintSizes(StringBuilder b) {
		_Dir(this, 0);
		
		void _Dir(FileTree x, int level) {
			foreach (var y in x.Children().OrderByDescending(o => o.Size)) {
				b.Append('\t', level);
				var k = y.Size / MB;
				b.AppendFormat(k == 0 ? "0       " : k < .001 ? "<0.001  " : k < .1 ? "{0,-7:0.###} " : k < 1 ? "{0,-7:0.##} " : k < 10 ? "{0,-7:0.#} " : "{0,-7:0.} ", k);
				if (y.IsDirectory) {
					b.AppendFormat("<explore {0}>{1}<>", y.Path, y.Name);
					if (y.HasChildren) {
						b.AppendLine("    <fold>");
						_Dir(y, level + 1);
						b.Length -= 2;
						b.Append("</fold>");
					}
				} else {
					b.AppendFormat("<explore {0}>{1}<>  (file)", y.Path, y.Name);
				}
				b.AppendLine();
			}
		}
	}
	
	const double MB = 1048576;
	
	/// <summary>
	/// Formats and prints a list of sizes and names of folder's descendant folders and optionally files, with a header.
	/// </summary>
	/// <param name="minSizeMB">Don't include smaller files and directories. The unit is MB.</param>
	/// <inheritdoc cref="Create"/>
	public static void PrintSizes(string path, bool onlyDirectories = false, double minSizeMB = 0, bool ignoreInaccessible = true, bool recurseNtfsLinks = false, Func<FEFile, bool> dirFilter = null, Func<FEFile, bool> fileFilter = null) {
		var t = Create(path, onlyDirectories, (long)(minSizeMB * MB), ignoreInaccessible, recurseNtfsLinks, dirFilter, fileFilter);
		
		var b = new StringBuilder("<><lc #C0E0A0>Sizes (MB) of folders");
		if (!onlyDirectories) b.Append(" and files");
		b.Append($" in <link>{path}<>");
		if (minSizeMB > 0) b.Append($". Skipped sizes < {minSizeMB} MB.");
		b.AppendLine("<>");
		
		t.PrintSizes(b);
		print.it(b);
	}
}
