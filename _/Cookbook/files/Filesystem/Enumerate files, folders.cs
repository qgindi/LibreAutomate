/// Use <see cref="filesystem"/> functions <see cref="filesystem.enumerate"/>, <see cref="filesystem.enumFiles"/>, <see cref="filesystem.enumDirectories"/>.
/// Tool: menu <b>Code > Get files in folder<>.

var dirPath = @"C:\Test";

foreach (var f in filesystem.enumerate(dirPath)) print.it(f.IsDirectory, f.FullPath); //files and directories
foreach (var f in filesystem.enumFiles(dirPath)) print.it(f.Name, f.Size); //files
foreach (var f in filesystem.enumDirectories(dirPath)) print.it(f.Name); //directories

//all descendant files and directories
foreach (var f in filesystem.enumerate(dirPath, FEFlags.AllDescendants | FEFlags.IgnoreInaccessible)) {
	print.it($"{new string(' ', f.Level)}{f.Name}");
}

var array = filesystem.enumFiles(dirPath, "*.txt").ToArray(); //.txt files as FEFile[] (file properties)
var arrayS = filesystem.enumFiles(dirPath).Select(o => o.FullPath).ToArray(); //all files as string[] (paths)

array = filesystem.enumFiles(dirPath, "**m *.png||*.bmp").ToArray(); //.png and .bmp files
array = filesystem.enumFiles(dirPath, "**nm *.png||*.bmp").ToArray(); //all files except .png and .bmp
array = filesystem.enumFiles(dirPath, @"**r \.html?$").ToArray(); //.htm and .html files (use regex)

foreach (var f in filesystem.enumFiles(dirPath).OrderBy(o => o.Size)) print.it(f.Name, f.Size); //sort by size
array = filesystem.enumFiles(dirPath).OrderByDescending(o => o.LastWriteTimeUtc).ToArray(); //sort by time, newest first
array = filesystem.enumerate(dirPath).OrderBy(o => !o.IsDirectory).ThenBy(o => o.Extension.Lower()).ToArray(); //sort by file type, directories first

//readonly files
array = filesystem.enumerate(dirPath, FEFlags.OnlyFiles, f => f.Attributes.Has(FileAttributes.ReadOnly)).ToArray();

//.txt files modified in last 30 days
var date = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30));
foreach (var f in filesystem.enumerate(dirPath, FEFlags.OnlyFiles, f => f.Name.Ends(".txt", true) && f.LastWriteTimeUtc >= date)) print.it(f.Name);

//all descendant files and directories; skip directories named "Debug" or "Release"
foreach (var f in filesystem.enumerate(dirPath, dirFilter: d => d.Name.Eq(true, "Debug", "Release") > 0 ? 0 : 3)) {
	print.it(f.FullPath);
}

//all descendant files except ".lnk" and ".txt"; skip directories named "protected"
foreach (var f in filesystem.enumerate(dirPath, dirFilter: d => d.Name.Eqi("protected") ? 0 : 2)) {
	if (f.Name.Ends(true, ".lnk", ".txt") > 0) continue;
	print.it(f.FullPath);
}

//get the same as array, using file filter function
array = filesystem.enumerate(dirPath, 0, f => f.Name.Ends(true, ".lnk", ".txt") == 0, d => d.Name.Eqi("protected") ? 0 : 2).ToArray();

/// Also can be used .NET classes <see cref="Directory"/> (when need only paths), <see cref="DirectoryInfo"/> (when also need properties) or namespace <see cref="System.IO.Enumeration"/> (when need custom filtering).

var array1 = Directory.GetFiles(dirPath, "*.htm"); //files as string[]
var array2 = Directory.GetDirectories(dirPath); //directories as string[]
foreach (var f in Directory.EnumerateFiles(dirPath)) print.it(f);
foreach (var f in new DirectoryInfo(dirPath).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = true, IgnoreInaccessible = true })) {
	print.it(f.FullName);
	if (f is FileInfo fi) print.it($"file, size={fi.Length}"); //file
	else /*if (f is DirectoryInfo di)*/ print.it("directory");
}
