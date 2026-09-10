/// 1. Clones Au.csproj to a temp file, and modifies the clone: adds more target frameworks, updates version, etc.
/// 2. Deletes obj folder, builds the clone project and creates nuget package.
/// 3. Adds the native dlls to the package.
/// 4. Prints links.

using System.IO.Compression;
using System.Xml.Linq;
using System.Xml.XPath;

if (script.testing) print.clear();

var outDir = pathname.normalize(folders.Editor + $@"..\Au\bin\Release");
var nupkg = $@"{outDir}\LibreAutomate.{Au_.Version}.nupkg";

//rejected. The API key expires in max 30 days. Also API keys may be not supported in the future. The other way is too difficult.
//if (args is ["/upload"]) {
//	var apiKey = Environment.GetEnvironmentVariable("API_NUGET") ?? throw new Exception("no env var API_NUGET");

//	var cl = $"""nuget push "{nupkg}" --api-key "{apiKey}" --source "https://api.nuget.org/v3/index.json" --force-english-output""";
//	var r = run.console(out string so, "dotnet.exe", cl);
//	if (r != 0) throw new Exception(so);
//	print.it(so);
//} else {

var auDir = @"C:\code\au\Au";
var auProj_nuget = auDir + @"\+Au.csproj";

var x = XElement.Load(auDir + @"\Au.csproj");
var tf = x.XPathSelectElement("/PropertyGroup/TargetFramework");
tf.ReplaceWith(new XElement("TargetFrameworks", tf.Value + ";net9.0-windows"));
x.XPathSelectElement("/PropertyGroup/Version").Value = Au_.Version;
x.XPathSelectElement("/PropertyGroup/Copyright").Value = $"Copyright (c) Gintaras Didžgalvis {DateTime.Now.Year}";
//x.XPathSelectElement("/Target[@Name='PreBuild']/Exec").Remove();
//print.it(x);
//return;
filesystem.saveText(auProj_nuget, x.ToString()); //not x.Save, it adds xml decl

filesystem.createDirectory(outDir);
filesystem.delete(Directory.GetFiles("LibreAutomate.*.nupkg")); //dotnet pack does nothing if the nupkg file exists
filesystem.delete(auDir + @"\obj");
int r = run.console(out var s, "dotnet.exe", $@"pack ""{auProj_nuget}"" -o {outDir} -c Release --nologo", auDir); //builds the clone project and creates nuget package
print.it(s);
//return;
if (r != 0) return;

filesystem.delete(auProj_nuget);
filesystem.delete(auDir + @"\obj");
filesystem.delete(auDir + @"\bin\Release\net10.0-windows");
filesystem.delete(auDir + @"\bin\Release\net9.0-windows");

if (!filesystem.exists(nupkg)) throw null;

using var za = ZipFile.Open(nupkg, ZipArchiveMode.Update);
za.CreateEntryFromFile(@"C:\code\au\_\64\AuCpp.dll", @"runtimes\win-x64\native\AuCpp.dll");
za.CreateEntryFromFile(@"C:\code\au\_\64\ARM\AuCpp.dll", @"runtimes\win-arm64\native\AuCpp.dll");
za.CreateEntryFromFile(@"C:\code\au\_\32\AuCpp.dll", @"runtimes\win-x86\native\AuCpp.dll");
za.CreateEntryFromFile(@"C:\code\au\_\64\Au.DllHost.exe", @"runtimes\win-x64\native\Au.DllHost.exe");
za.CreateEntryFromFile(@"C:\code\au\_\64\ARM\Au.DllHost.exe", @"runtimes\win-arm64\native\Au.DllHost.exe");
za.CreateEntryFromFile(@"C:\code\au\_\32\Au.DllHost.exe", @"runtimes\win-x86\native\Au.DllHost.exe");

print.it($"<><explore>{nupkg}<>");
print.it($"<><link>https://www.nuget.org/packages/manage/upload<>");
