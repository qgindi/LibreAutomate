/// Use class <see cref="filesystem"/>.

/// Copy file or folder. Replace existing file(s).

string fc1 = @"C:\Test\file.txt"; //old path
string fc2 = @"C:\Test2\file.txt"; //new path
filesystem.copy(fc1, fc2, FIfExists.Delete);

filesystem.copyTo(fc1, @"C:\Test2"); //copy to the specified folder

/// Merge folders, replacing existing files.

filesystem.copy(@"C:\Test", @"C:\Test2", FIfExists.MergeDirectory);

/// Copy all .png files to the specified folder. Don't include subfolders.

foreach (var f in filesystem.enumFiles(@"C:\Test", "*.png"))
	filesystem.copyTo(f.FullPath, @"C:\Test2");

//or can do the same with filter functions
filesystem.copy(@"C:\Test", @"C:\Test2", fileFilter: f => f.Name.Ends(".png", true), dirFilter: _ => 0);

/// Copy all .png files to the specified folder. From subfolders too.

//create subfolders like in the source folder
filesystem.copy(@"C:\Test", @"C:\Test2", 0, FCFlags.NoEmptyDirectories, f => f.Name.Ends(".png", true));

//copy all to single folder. Give unique names if need.
foreach (var f in filesystem.enumFiles(@"C:\Test", "*.png", FEFlags.AllDescendants))
	filesystem.copyTo(f.FullPath, @"C:\Test2", FIfExists.RenameNew);
 
/// Copy files modified today.

var date = DateTime.Today;

//copy from subfolders too. Create subfolders like in the source folder.
filesystem.copy(@"C:\Test", @"C:\Test2", FIfExists.Delete, FCFlags.NoEmptyDirectories, fileFilter: o => o.LastWriteTimeUtc >= date);

//don't include subfolders. For filtering use LINQ function <b>Where</b>.
foreach (var f in filesystem.enumFiles(@"C:\Test").Where(o => o.LastWriteTimeUtc >= date))
	filesystem.copyTo(f.FullPath, @"C:\Test2", FIfExists.Delete);

/// The functions automatically create missing destination folders.

/// Or use <google>robocopy</google>.

print.clear();
int rc = run.console(out string rs, "robocopy.exe", """
"C:\Test" "C:\Test2" /e /sl
""");
if (rc > 8) throw new Exception($"robocopy failed:\r\n{rs}");
print.it("done", rc);
