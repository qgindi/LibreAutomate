/// There are several useful <+ms>Windows command line<> programs and commands.
/// 
/// To run programs, use <see cref="run.console"/>.

run.console("ipconfig.exe", "/flushdns");

/// To run other commands, use a <_>.bat</_> file or <+ms>cmd.exe<>.

var commands = """
cd /d C:\Test\Folder
dir
""";
commands = commands.Replace("\r\n", " && ");
run.console("cmd.exe", $"""/u /c "{commands}" """, encoding: Encoding.Unicode);

/// Also you can find command line programs on the internet, or even already have them installed.

string file1 = @"C:\Test\icons.db";
var file2 = @"C:\Test\icons.7z";
run.console(folders.ProgramFiles + @"7-Zip\7z.exe", $"""a "{file2}" "{file1}" """);

/// Some links:
/// - <link https://learn.microsoft.com/en-us/sysinternals/downloads/>Sysinternals<>
/// - <link https://www.nirsoft.net/utils/>NirSoft<>
