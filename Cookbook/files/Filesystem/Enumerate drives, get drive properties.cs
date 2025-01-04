/// Enumerate drives.

foreach (var x in DriveInfo.GetDrives()) print.it(x.Name, x.VolumeLabel, x.DriveType);

/// Get drive info. Enumerate child directories.

var c = new DriveInfo("C");
print.it(c.TotalSize, c.AvailableFreeSpace);
foreach (var d in c.RootDirectory.EnumerateDirectories()) print.it(d.FullName);
//or
foreach (var d in filesystem.enumDirectories(c.Name)) print.it(d.FullPath);

/// Get drive name of this program, and its type (fixed/removable/network).

print.it(folders.ThisAppDriveBS, folders.thisAppDriveType);

/// Get a removable drive by index or name.

print.it(folders.removableDrive(0));
