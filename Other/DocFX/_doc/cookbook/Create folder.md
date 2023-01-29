# Create folder
Create folder (directory) if does not exist.

```csharp
filesystem.createDirectory(@"C:\Test\Folder"); //also creates C:\Test if need
```

Create folder (if does not exist) for the specified file.

```csharp
filesystem.createDirectoryFor(@"C:\Test\Folder\file.txt"); //creates C:\Test\Folder if need
```

If need custom security permissions, there are several ways:
- Use a folder that has these security settings as a template.
- Run <a href='https://www.google.com/search?q=icacls+or+cacls'>icacls or cacls</a> with <a href='/api/Au.run.console.html'>run.console</a>.
- Use <b>DirectoryInfo.SetAccessControl</b> and <b>System.Security.AccessControl.DirectorySecurity</b>.

```csharp
filesystem.createDirectory(@"C:\Test\Folder", @"C:\Template folder");
```

