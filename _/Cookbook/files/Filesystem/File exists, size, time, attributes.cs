/// Use <see cref="filesystem"/> functions <help filesystem.exists>exists<>, <help filesystem.getAttributes>getAttributes<>, <help filesystem.getProperties>getProperties<> or <help filesystem.enumerate>enumerate<>.

var filePath = @"C:\Test\test.txt";
var dirPath = @"C:\Test";

if (filesystem.exists(filePath)) print.it("exists");
if (filesystem.exists(filePath).File) print.it("exists as file");
if (filesystem.exists(filePath).Directory) print.it("exists as directory");
if (filesystem.exists(filePath) is FAttr { File: true, IsReadonly: false }) print.it("exists as file and isn't readonly");
switch (filesystem.exists(filePath)) {
case 0: print.it("doesn't exist"); break;
case 1: print.it("file"); break;
case 2: print.it("directory"); break;
}

if (filesystem.getAttributes(filePath, out var attr)) print.it(attr, attr.Has(FileAttributes.Directory), attr.Has(FileAttributes.ReadOnly));

if (filesystem.getProperties(filePath, out var p)) print.it(p.LastWriteTimeUtc, p.Size, p.Attributes);

foreach (var f in filesystem.enumerate(dirPath)) print.it(f.FullPath, f.IsDirectory, f.Size);

/// Also can be used .NET classes <see cref="File"/>, <see cref="Directory"/>, <see cref="FileInfo"/> and <see cref="DirectoryInfo"/>. They provide the same info.

if (File.Exists(filePath)) print.it("exists as file");
if (Directory.Exists(filePath)) print.it("exists as directory");

print.it(File.GetAttributes(filePath));

var fi = new FileInfo(filePath); if (fi.Exists) print.it(fi.Length, fi.LastWriteTimeUtc);

foreach (var f in new DirectoryInfo(dirPath).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = true }))
	print.it(f.FullName, f.Attributes, f is FileInfo k ? k.Length : 0);

/// To set file properties can be used .NET functions.

File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly); //add attribute
File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly); //remove attribute

new DirectoryInfo(dirPath).Attributes |= FileAttributes.Hidden; //add attribute
new DirectoryInfo(dirPath).Attributes &= ~FileAttributes.Hidden; //remove attribute

File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
