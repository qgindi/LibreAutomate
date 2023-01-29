# Command line programs
There are sevaral useful <a href='https://www.google.com/search?q=Windows+command+line+site%3Amicrosoft.com'>Windows command line</a> programs and commands.

To run programs, use <a href='/api/Au.run.console.html'>run.console</a>.

```csharp
run.console("ipconfig.exe", "/flushdns");
```

To run other commands, use a .bat file or <a href='https://www.google.com/search?q=cmd.exe+site%3Amicrosoft.com'>cmd.exe</a>.

```csharp
var commands = """
cd /d C:\Test\Folder
dir
""";
commands = commands.Replace("\r\n", " && ");
run.console("cmd.exe", $"""/u /c "{commands}" """, encoding: Encoding.Unicode);
```

Also you can find command line programs on the internet, or even already have them installed.

```csharp
string file1 = @"C:\Test\icons.db";
var file2 = @"C:\Test\icons.7z";
run.console(folders.ProgramFiles + @"7-Zip\7z.exe", $"""a "{file2}" "{file1}" """);
```

Some links:
- <a href='https://learn.microsoft.com/en-us/sysinternals/downloads/'>Sysinternals</a>
- <a href='https://www.nirsoft.net/utils/'>NirSoft</a>
