/// Use <see cref="filesystem.list"/>.
/// Tool: menu <b>Code > Get files in folder<>.

var dirPath = @"C:\Test";

foreach (var f in filesystem.list(dirPath).All) print.it(f.IsDirectory, f); //files and directories
foreach (var f in filesystem.list(dirPath).Files) print.it(f.Name, f.Attributes); //files
foreach (var f in filesystem.list(dirPath).Directories) print.it(f.Name); //directories
foreach (var f in filesystem.list(dirPath).FileInfos) print.it($"{f.Name,-30}  {f.Length,-10}  {f.LastWriteTime}"); //files, get more properties

//all descendant files and directories
foreach (var f in filesystem.list(dirPath, flags: FLFlags.Recurse).AllDepthFirst()) {
	print.it($"{new string(' ', f.RelativeDepth * 2)}{f.Name}");
}

var array = filesystem.list(dirPath, "*.txt").FileInfos; //.txt files as FileInfo[] (file properties)
var arrayS = filesystem.list(dirPath).FilePaths; //all files as string[] (paths)

array = filesystem.list(dirPath, "*.png|*.bmp").FileInfos; //.png and .bmp files
array = filesystem.list(dirPath, "**/*.png|**/*.bmp", FLFlags.Recurse).FileInfos; //all descendant .png and .bmp files
array = filesystem.list(dirPath, "||*.png|*.bmp").FileInfos; //all files except .png and .bmp
array = filesystem.list(dirPath, @"**r \.html?$").FileInfos; //.htm and .html files (use regex)

foreach (var f in filesystem.list(dirPath).FileInfos.OrderBy(o => o.Length)) print.it(f.Name, f.Length); //sort by size
array = filesystem.list(dirPath).FileInfos.OrderByDescending(o => o.LastWriteTimeUtc).ToArray(); //sort by time, newest first
var array2 = filesystem.list(dirPath).AllInfos.OrderBy(o => !o.IsDirectory).ThenBy(o => o.Extension, StringComparer.OrdinalIgnoreCase).ToArray(); //sort by file type, directories first

//all descendant files and directories; skip directories named "bin" or "obj" anywhere
foreach (var f in filesystem.list(dirPath, "||**/bin/|**/obj/", FLFlags.Recurse | FLFlags.IgnoreInaccessible).All) {
	print.it(f);
}

//all descendant files except ".lnk" and ".txt"; skip directory named "skip"
foreach (var f in filesystem.list(dirPath, "||skip/", FLFlags.Recurse).All) {
	if (f.Name.Ends(true, ".lnk", ".txt") > 0) continue; //an alternative to glob patterns
	print.it(f);
}

//get all properties and relative paths
foreach (var v in filesystem.list(dirPath, flags: FLFlags.Recurse).Enumerate((ref e) => (info: e.ToFileSystemInfo(), relativePath: e.ToRelativePath())))
	print.it(v);

//example with IncludeFunc: get only readonly files
var array3 = filesystem.list(dirPath, new() { IncludeFunc = (ref f) => f.Attributes.Has(FileAttributes.ReadOnly) }).FilePaths;

//example with IncludeFunc: get .txt files modified in last 30 days
var date = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30));
var filter = new PathFilter { IncludeFunc = (ref f) => f.FileName.Ends(".txt", true) && f.LastWriteTimeUtc.DateTime >= date };
foreach (var f in filesystem.list(dirPath, filter).FilePaths) print.it(f);

/// Also can be used .NET classes <see cref="Directory"/> (when need only paths), <see cref="DirectoryInfo"/> (when also need properties) or namespace <see cref="System.IO.Enumeration"/> (when need custom filtering). Their default options are different, for example skips hidden and system files, recurses NTFS links. Also they don't support glob patterns, but support * wildcard in filenames only.

var array10 = Directory.GetFiles(dirPath, "*.htm"); //files as string[]
var array11 = Directory.GetDirectories(dirPath); //directories as string[]
foreach (var f in Directory.EnumerateFiles(dirPath)) print.it(f);
foreach (var f in new DirectoryInfo(dirPath).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = true, IgnoreInaccessible = true })) {
	print.it(f.FullName);
	if (f is FileInfo fi) print.it($"file, size={fi.Length}");
	else /*if (f is DirectoryInfo di)*/ print.it("directory");
}
