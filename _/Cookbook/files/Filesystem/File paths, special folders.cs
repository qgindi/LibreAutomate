/// Examples of file and directory (folder) <google File path formats on Windows>paths</google>.

string file = @"C:\Windows\System32\Notepad.exe";
string folder = @"C:\Windows\System32";
string drive = @"C:\"; //some functions also support C: without \
string networkFile = @"\\computer\folder\folder2\file.txt";
string networkDrive = @"\\computer\folder\";

/// Without @ would need to escape \ characters.

file = "C:\\Windows\\System32\\Notepad.exe";

/// Use class <see cref="folders"/> to get paths of special folders.

string file2 = folders.System + "Notepad.exe";
string folder2 = folders.Documents;

/// Special folders and environment variables can be in path string. Some functions support such strings; for others need <see cref="pathname.expand"/>.

string pathSF = @"%folders.Documents%\file.txt";
pathSF = pathname.expand(pathSF); //like C:\Users\Me\Documents\file.txt

Environment.SetEnvironmentVariable("TestPath", @"C:\Test");
string pathEV = pathname.expand(@"%TestPath%\file.txt"); //C:\Test\file.txt

/// Use class <see cref="pathname"/> or <see cref="Path"/> to manipulate path strings.

string s = @"C:\Windows\System32\Notepad.exe";
string dir = pathname.getDirectory(s); //C:\Windows\System32
string name = pathname.getName(s); //Notepad.exe
string nameNoExt = pathname.getNameNoExt(s); //Notepad
string ext = pathname.getExtension(s); //.exe
string s2 = pathname.combine(dir, name); //C:\Windows\System32\Notepad.exe
print.it(pathname.isFullPath(s), pathname.isFullPath(name)); //True, False
var randomName = folders.ThisAppTemp + Guid.NewGuid().ToString() + ".txt"; //like C:\Users\Me\AppData\Local\Temp\Au\22bb3821-dae5-4ac2-94b5-363a22dbf40d.txt
var normal = pathname.normalize(@"C:\A\B\..\C\file.txt"); //C:\A\C\file.txt
var full = pathname.normalize(@"relative\file.txt", folders.Documents); //C:\Users\G\Documents\relative\file.txt
var full2 = pathname.normalize(@"C:\full\file.txt", folders.Documents); //C:\full\file.txt

var xml = Path.ChangeExtension(s, "xml"); //C:\Windows\System32\Notepad.xml
var root = Path.GetPathRoot(s); //C:\
var tempName = Path.GetTempFileName(); //like C:\Users\Me\AppData\Local\Temp\tmpDC9E.tmp
