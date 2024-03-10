/// 1. Clones Au.csproj to a temp file, and modifies the clone: adds more target frameworks, updates version, etc.
/// 2. Deletes obj folder, builds the clone project and creates nuget package.
/// 3. Adds the native dlls to the package.
/// 4. Prints links.

using System.Xml.Linq;
using System.Xml.XPath;

print.clear();
var auDir = @"C:\code\au\Au";
var auProj_nuget = auDir + @"\+Au.csproj";

var x = XElement.Load(auDir + @"\Au.csproj");
x.XPathSelectElement("/PropertyGroup/TargetFrameworks").Value += ";net6.0-windows";
x.XPathSelectElement("/PropertyGroup/Version").Value = typeof(osVersion).Assembly.GetName().Version.ToString(3);
x.XPathSelectElement("/PropertyGroup/Copyright").Value = $"Copyright (c) Gintaras Didžgalvis {DateTime.Now.Year}";
x.XPathSelectElement("/Target[@Name='PreBuild']/Exec").Remove();
//print.it(x);
//return;
filesystem.saveText(auProj_nuget, x.ToString()); //not x.Save, it adds xml decl

var od = @"C:\code\au\Au\bin\Release";
filesystem.createDirectory(od);
foreach (var f in filesystem.enumFiles(od, "LibreAutomate.*.nupkg")) filesystem.delete(f.FullPath); //dotnet pack does nothing if the nupkg file exists
filesystem.delete(auDir + @"\obj");
int r = run.console(out var s, "dotnet.exe", $@"pack ""{auProj_nuget}"" -o {od} -c Release --nologo", auDir); //builds the clone project and creates nuget package
print.it(s);
//return;
if (r != 0) return;

filesystem.delete(auProj_nuget);
filesystem.delete(auDir + @"\obj");
filesystem.delete(auDir + @"\bin\Release\net6.0-windows");

s.RxMatch(@"Successfully created package '(.+?)'", 1, out string path);
if (!filesystem.exists(path)) throw null;

using var za = ZipFile.Open(path, ZipArchiveMode.Update);
za.CreateEntryFromFile(@"C:\code\au\_\64\AuCpp.dll", @"runtimes\win-x64\native\AuCpp.dll");
za.CreateEntryFromFile(@"C:\code\au\_\64\sqlite3.dll", @"runtimes\win-x64\native\sqlite3.dll");
za.CreateEntryFromFile(@"C:\code\au\_\32\AuCpp.dll", @"runtimes\win-x86\native\AuCpp.dll");
za.CreateEntryFromFile(@"C:\code\au\_\32\sqlite3.dll", @"runtimes\win-x86\native\sqlite3.dll");

print.it($"<><explore>{path}<>");
print.it($"<><link>https://www.nuget.org/packages/manage/upload<>");
