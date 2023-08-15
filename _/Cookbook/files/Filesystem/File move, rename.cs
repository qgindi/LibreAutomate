/// Use class <see cref="filesystem"/>.

/// Move file or folder.

string fm1 = @"C:\Test\file.txt"; //old path
string fm2 = @"C:\Test2\file.txt"; //new path
filesystem.move(fm1, fm2);

filesystem.moveTo(fm1, @"C:\Test2"); //move to the specified folder

/// Rename file or folder (directory).

filesystem.rename(@"C:\Test\a.txt", "b.txt");

/// Append date and time.

string s1 = @"C:\Test\test.txt";
var dt = DateTime.Now; //time now
//var dt = File.GetLastWriteTime(s1); //file time
var s2 = s1.Insert(pathname.findExtension(s1), dt.ToString(" yyyy-MM-dd HHmmss"));
//print.it(s2); //C:\Test\test 2022-02-03 190439.txt
filesystem.move(s1, s2); //use move instead of rename because s2 is full path
//filesystem.rename(s1, pathname.getName(s2)); //the same

/// Change extension of all files in folder and subfolders.

foreach (var f in filesystem.enumFiles(@"C:\Test", "*.html", FEFlags.AllDescendants))
	filesystem.rename(f.FullPath, Path.ChangeExtension(f.Name, "htm"));
