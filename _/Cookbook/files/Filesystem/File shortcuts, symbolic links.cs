/// Use class <see cref="shortcutFile"/>.

/// Create shortcut to Notepad.exe.

using (var x = shortcutFile.create(@"C:\Test\Notepad.lnk")) {
	x.TargetPath = folders.System + "Notepad.exe";
	//x.Hotkey = (KMod.Ctrl | KMod.Alt, KKey.D5); //optionally set more properties
	x.Save();
}

/// Get shortcut target path.

string path = shortcutFile.getTarget(@"C:\Test\Notepad.lnk");
print.it(path);

/// Get shortcut properties.

using (var x = shortcutFile.open(@"C:\Test\Notepad.lnk")) {
	print.it(x.TargetPath);
	print.it(x.GetIconLocation(out var ii), ii);
}

/// Delete shortcut (if exists) and unregister its hotkey.

shortcutFile.delete(@"C:\Test\Notepad.lnk");

/// Shortcuts also are known as <i>shell links</i>. Also there are <i>symbolic links</i> and other types of filesystem links.

var symlink = folders.Desktop + "Test";
if (filesystem.exists(symlink).IsNtfsLink) filesystem.delete(symlink); //deletes the symbolic link but not its target
Directory.CreateSymbolicLink(symlink, @"C:\Test");
//File.CreateSymbolicLink(symlink2, @"C:\Test\file.txt");

/// Symbolic links can be absolute (target is full path) or relative (target is relative to the link's parent directory, like <mono>@"Abc\Def"<> or <mono>@"..\Abc\Def"<>).
