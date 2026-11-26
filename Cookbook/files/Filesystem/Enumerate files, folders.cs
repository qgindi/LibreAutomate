/// Use <see cref="filesystem.list"/>.
/// Tool: menu <b>Code > Get files in folder<>.

var dirPath = @"C:\Test";

foreach (var f in filesystem.list(dirPath).All) print.it(f.IsDirectory, f); //files and directories
foreach (var f in filesystem.list(dirPath).Files) print.it(f.Name, f.Attributes); //files
foreach (var f in filesystem.list(dirPath).Directories) print.it(f.Name); //directories

//print file properties formatted as table
foreach (var f in filesystem.list(dirPath).Files) {
	print.it($"{f.Name,-30}  {f.Size,-10}  {f.LastWriteTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
}

//all descendant files and directories
foreach (var f in filesystem.list(dirPath, flags: FLFlags.Recurse).All) {
	print.it($"{new string(' ', f.Depth * 2)}{f.Name}");
}

var array2 = filesystem.list(dirPath).FilePaths; //files as string[] (paths)

//include/exclude files using glob patterns
var array = filesystem.list(dirPath, "*.txt").Files; //.txt files
array = filesystem.list(dirPath, "*.png|*.bmp").Files; //.png and .bmp files
array = filesystem.list(dirPath, "**/*.png|**/*.bmp", FLFlags.Recurse).Files; //all descendant .png and .bmp files
array = filesystem.list(dirPath, "||*.png|*.bmp").Files; //all files except .png and .bmp
array = filesystem.list(dirPath, @"**r \.html?$").Files; //.htm and .html files (use regex)

//all descendant files and directories; skip directories named "bin" or "obj" anywhere
foreach (var f in filesystem.list(dirPath, "||**/bin/|**/obj/", FLFlags.Recurse | FLFlags.IgnoreInaccessible).All) {
	print.it(f);
}

//all descendant files except ".lnk" and ".txt"; skip directory named "skip"
foreach (var f in filesystem.list(dirPath, "||skip/", FLFlags.Recurse).All) {
	if (f.Name.Ends(true, ".lnk", ".txt") > 0) continue; //an alternative to glob patterns
	print.it(f);
}

//sorting
foreach (var f in filesystem.list(dirPath).Files.OrderBy(o => o.Size)) print.it(f.Name, f.Size); //sort by size
array = filesystem.list(dirPath).Files
	.OrderByDescending(o => o.LastWriteTimeUtc) //sort by time, newest first
	.ToArray();
array = filesystem.list(dirPath).All
	.OrderBy(o => !o.IsDirectory).ThenBy(o => o.Extension, StringComparer.OrdinalIgnoreCase) //sort by file type, directories first
	.ToArray();

//get only some properties
foreach (var v in filesystem.list(dirPath, flags: FLFlags.Recurse)
	.Enumerate(e => (path: e.Path.ToString(), size: e.Size), files: true))
	print.it(v);

//example with a filter function: get only readonly files
PathFilter filter = new() { FileFilter = e => e.Attributes.Has(FileAttributes.ReadOnly) };
var array3 = filesystem.list(dirPath, filter, FLFlags.Recurse).FilePaths;

//example with a filter function: get .txt files modified in last 30 days
var date = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30)).ToFileTimeUtc();
PathFilter filter2 = new() { FileFilter = e => e.Name.Ends(".txt", true) && e.LastWriteTimeUtc >= date };
foreach (var f in filesystem.list(dirPath, filter2).FilePaths) print.it(f);

/// Also can be used .NET classes <see cref="Directory"/> (when need only paths), <see cref="DirectoryInfo"/> (when also need properties) or namespace <see cref="System.IO.Enumeration"/> (when need custom filtering). Their default options are different, for example skips hidden and system files, recurses NTFS links. They don't support glob patterns, but support `*` wildcard in filenames only. They order results differently.

var array10 = Directory.GetFiles(dirPath, "*.htm"); //files as string[]
var array11 = Directory.GetDirectories(dirPath); //directories as string[]
foreach (var f in Directory.EnumerateFiles(dirPath)) print.it(f);
foreach (var f in new DirectoryInfo(dirPath).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = true, IgnoreInaccessible = true })) {
	print.it(f.FullName);
	if (f is FileInfo fi) print.it($"file, size={fi.Length}");
	else /*if (f is DirectoryInfo di)*/ print.it("directory");
}
