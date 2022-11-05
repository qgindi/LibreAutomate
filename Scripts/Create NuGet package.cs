print.clear();
var od = @"C:\Test\nuget";
//var od = @"C:\code\au\_";

foreach (var f in filesystem.enumFiles(od, "LibreAutomate.*.nupkg")) filesystem.delete(f.FullPath); //dotnet pack does nothing if the nupkg file exists

int r = run.console(out var s, "dotnet.exe", $@"pack Au.csproj -o {od} --no-build -c Release --nologo", @"C:\code\au\Au");
print.it(s);
if (r != 0) return;

s.RxMatch(@"'(.+?)'", 1, out string path);

using var za = ZipFile.Open(path, ZipArchiveMode.Update);
za.CreateEntryFromFile(@"C:\code\au\_\64\AuCpp.dll", @"runtimes\win-x64\native\AuCpp.dll");
za.CreateEntryFromFile(@"C:\code\au\_\64\sqlite3.dll", @"runtimes\win-x64\native\sqlite3.dll");
za.CreateEntryFromFile(@"C:\code\au\_\32\AuCpp.dll", @"runtimes\win-x86\native\AuCpp.dll");
za.CreateEntryFromFile(@"C:\code\au\_\32\sqlite3.dll", @"runtimes\win-x86\native\sqlite3.dll");

print.it($"<><explore>{path}<>");
print.it($"<><link>https://www.nuget.org/packages/manage/upload<>");
