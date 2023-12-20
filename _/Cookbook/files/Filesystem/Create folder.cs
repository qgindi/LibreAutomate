/// Create folder (directory) if does not exist.

filesystem.createDirectory(@"C:\Test\Folder"); //also creates C:\Test if need

/// Create folder (if does not exist) for the specified file.

filesystem.createDirectoryFor(@"C:\Test\Folder\file.txt"); //creates C:\Test\Folder if need

/// If need custom security permissions, there are several ways:
/// - Use a folder that has these security settings as a template.
/// - Run <google>icacls or cacls</google> with <see cref="run.console"/>.
/// - Use <b>DirectoryInfo.SetAccessControl</b> and <b>System.Security.AccessControl.DirectorySecurity</b>.

filesystem.createDirectory(@"C:\Test\Folder", @"C:\Template folder");
