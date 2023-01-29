# File exists, size, time, attributes
Use <a href='/api/Au.filesystem.html'>filesystem</a> functions <a href='/api/Au.filesystem.exists.html'>exists</a>, <a href='/api/Au.filesystem.getAttributes.html'>getAttributes</a>, <a href='/api/Au.filesystem.getProperties.html'>getProperties</a> or <a href='/api/Au.filesystem.enumerate.html'>enumerate</a>.

```csharp
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
```

Also can be used .NET classes <a href='https://www.google.com/search?q=System.IO.File+class'>File</a>, <a href='https://www.google.com/search?q=System.IO.Directory+class'>Directory</a>, <a href='https://www.google.com/search?q=System.IO.FileInfo+class'>FileInfo</a> and <a href='https://www.google.com/search?q=System.IO.DirectoryInfo+class'>DirectoryInfo</a>. They provide the same info.

```csharp
if (File.Exists(filePath)) print.it("exists as file");
if (Directory.Exists(filePath)) print.it("exists as directory");

print.it(File.GetAttributes(filePath));

var fi = new FileInfo(filePath); if (fi.Exists) print.it(fi.Length, fi.LastWriteTimeUtc);

foreach (var f in new DirectoryInfo(dirPath).EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = true }))
	print.it(f.FullName, f.Attributes, f is FileInfo k ? k.Length : 0);
```

To set file properties can be used .NET functions.

```csharp
File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.ReadOnly); //add attribute
File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly); //remove attribute

new DirectoryInfo(dirPath).Attributes |= FileAttributes.Hidden; //add attribute
new DirectoryInfo(dirPath).Attributes &= ~FileAttributes.Hidden; //remove attribute

File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
```

