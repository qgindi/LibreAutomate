/// Use class <see cref="ZipFile"/>.

string zipFile = @"C:\Test\Z.zip";
//filesystem.delete(zipFile);

/// Create .zip file from folder.

ZipFile.CreateFromDirectory(@"C:\Test\Folder", zipFile);
ZipFile.CreateFromDirectory(@"C:\Test\Folder", zipFile, CompressionLevel.Fastest, includeBaseDirectory: true);

/// Extract .zip file.

ZipFile.ExtractToDirectory(zipFile, @"C:\Test\Folder2");

/// Create .zip file from single file.

string file = @"C:\Test\sqlite.db";
using (var z = ZipFile.Open(zipFile, ZipArchiveMode.Create))
	z.CreateEntryFromFile(file, pathname.getName(file));

/// Create .zip file and add .db files from folder.

using (var z = ZipFile.Open(zipFile, ZipArchiveMode.Create)) {
	foreach (var f in filesystem.enumFiles(@"C:\Test", "*.db"))
		z.CreateEntryFromFile(f.FullPath, f.Name);
}

/// Create .zip file from folder. Don't include subfolders named "protected" or "ico".

using (var z = ZipFile.Open(zipFile, ZipArchiveMode.Create)) {
	foreach (var f in filesystem.enumerate(@"C:\Test\Folder", FEFlags.NeedRelativePaths, dirFilter: d => d.Name.Ends(true, @"\protected", @"\ico") > 0 ? 0 : 2)) {
		//print.it(f.Name);
		z.CreateEntryFromFile(f.FullPath, f.Name[1..].Replace('\\', '/'));
	}
}

/// Create .zip file and add files/folders specified in a list or array.

string list = @"
C:\Test\sqlite.db
C:\Test\Folder";
string[] a = list.Lines(noEmpty: true);
using (var z = ZipFile.Open(zipFile, ZipArchiveMode.Create)) {
	foreach (var f in a) {
		switch (filesystem.exists(f)) {
		case 1: //file
			z.CreateEntryFromFile(f, pathname.getName(f));
			break;
		case 2: //folder
			foreach (var k in filesystem.enumFiles(f, flags: FEFlags.AllDescendants | FEFlags.NeedRelativePaths))
				z.CreateEntryFromFile(k.FullPath, k.Name[1..].Replace('\\', '/'));
			break;
		default:
			print.warning("file not found: " + f);
			break;
		}
	}
}

/// List/find/extract zip file entries.

using (var z = ZipFile.OpenRead(zipFile)) {
	foreach (var e in z.Entries) {
		print.it(e.Name, e.FullName, e.Length, e.CompressedLength);
		if(e.Name.Eqi("m.txt")) {
			//var s = @"C:\Test\Extracted\" + e.Name; //extract all to the same folder
			var s = @"C:\Test\Extracted\" + e.FullName; //create subfolders if need
			filesystem.createDirectoryFor(s);
			e.ExtractToFile(s, overwrite: true);
		}
	}
}

/// If need other compression formats or more features, look for a zip library on the internet, for example in <google>NuGet</google>. More info in recipe <+recipe>.NET, NuGet, other libraries<>.
/// 
/// Also you can find command line programs on the internet, or even already have them installed.

string file1 = @"C:\Test\icons.db";
var file2 = @"C:\Test\icons.7z";
run.console(folders.ProgramFiles + @"7-Zip\7z.exe", $@"a ""{file2}"" ""{file1}""");
