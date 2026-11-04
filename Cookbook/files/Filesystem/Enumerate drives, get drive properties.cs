/// Enumerate drives.

foreach (var x in DriveInfo.GetDrives()) print.it(x.Name, x.VolumeLabel, x.DriveType);

/// Get drive info. Enumerate child directories.

var c = new DriveInfo("C");
print.it(c.TotalSize, c.AvailableFreeSpace);
foreach (var d in c.RootDirectory.EnumerateDirectories()) print.it(d.FullName);
//or
foreach (var d in filesystem.list(c.Name).Directories) print.it(d);

/// Get home drive name and its type (fixed/removable/network).

print.it(folders.ThisAppDriveBS, new DriveInfo(folders.ThisAppDriveBS).DriveType);

/// Get a removable drive by index or name.

print.it(folders.removableDrive(0));
