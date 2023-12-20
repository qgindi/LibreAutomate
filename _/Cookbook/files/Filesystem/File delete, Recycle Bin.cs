/// Use class <see cref="filesystem"/>.

/// Delete file or folder.

filesystem.delete(@"C:\Temp\file.txt"); //delete permanantly
filesystem.delete(@"C:\Temp\file.txt", FDFlags.RecycleBin); //move to the Recycle Bin if possible, else delete permanently

/// Delete everything from Temp folder, except locked files/folders.

foreach (var f in filesystem.enumerate(folders.Temp)) {
	filesystem.delete(f.FullPath, FDFlags.CanFail);
}

/// Delete all .txt files from folder. Not from subfolders.

foreach (var f in filesystem.enumFiles(@"C:\Temp", "*.txt"))
	filesystem.delete(f.FullPath);

/// Empty the Recycle Bin.

filesystem.more.emptyRecycleBin(); //all drives
filesystem.more.emptyRecycleBin("C:"); //single drive
filesystem.more.emptyRecycleBin(progressUI: true); //with progress UI

/// Open the Recycle Bin folder.

run.it(folders.shell.RecycleBin);
