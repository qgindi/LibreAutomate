/// Use class <see cref="filesystem"/>.

/// Delete file or folder.

filesystem.delete(@"C:\Temp\file.txt"); //delete permanantly
filesystem.delete(@"C:\Temp\file.txt", FDFlags.RecycleBin); //move to the Recycle Bin if possible, else delete permanently

/// Delete everything from the Temp folder, except locked files/folders.

foreach (var f in filesystem.list(folders.Temp).All) {
	filesystem.delete(f, FDFlags.CanFail);
}

/// Delete all `.txt` files from folder. Not from subfolders.

foreach (var f in filesystem.list(@"C:\Temp", "*.txt").Files)
	filesystem.delete(f);

/// Empty the Recycle Bin.

filesystem.more.emptyRecycleBin(); //all drives
filesystem.more.emptyRecycleBin("C:"); //single drive
filesystem.more.emptyRecycleBin(progressUI: true); //with progress UI

/// Open the Recycle Bin folder.

run.it(folders.shell.RecycleBin);
