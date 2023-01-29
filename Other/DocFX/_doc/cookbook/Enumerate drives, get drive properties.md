# Enumerate drives, get drive properties
Enumerate drives.

```csharp
foreach (var x in DriveInfo.GetDrives()) print.it(x.Name, x.VolumeLabel, x.DriveType);
```

Get drive info. Enumerate child directories.

```csharp
var c = new DriveInfo("C");
print.it(c.TotalSize, c.AvailableFreeSpace);
foreach (var d in c.RootDirectory.EnumerateDirectories()) print.it(d.FullName);
//or
foreach (var d in filesystem.enumDirectories(c.Name)) print.it(d.FullPath);
```

Get drive name of this program, and its type (fixed/removable/network).

```csharp
print.it(folders.ThisAppDriveBS, folders.thisAppDriveType);
```

Get a removable drive by index or name.

```csharp
print.it(folders.removableDrive(0));
```

