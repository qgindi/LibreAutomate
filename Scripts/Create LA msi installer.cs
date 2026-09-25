/*/ nuget test\Microsoft.Extensions.FileSystemGlobbing; /*/
//#define UPGRADE
using Microsoft.Extensions.FileSystemGlobbing;

print.clear();
print.qm2.clear();
var tempDir = @"C:\Temp\Au\msi";

_CopyFiles();

#if !UPGRADE
C.Uninstall();
#endif

string edDir = folders.Editor;
string solDir = pathname.getDirectory(edDir);

string mpdev = folders.ProgramFiles + @"Master Packager Ltd\Master Packager Dev\mpdev.exe";
string jsonFile = solDir + @"\Other\MsiCA\msi.json";
string icon = solDir + @"\Au.Editor\resources\ico\app.ico";
Environment.SetEnvironmentVariable("_MSI_SOLUTION_DIR", solDir);

string msiName = "LibreAutomate";
string version = Au_.Version;
#if UPGRADE
var g = Version.Parse(version);
version = $"{g.Major}.{g.Minor}.{g.Build + 1}";
msiName = "LibreAutomate-" + version;
print.it("Test upgrading", version);
#endif

string cl = $"""build "{jsonFile}" --properties $.outputDirectory="{edDir}" $.icon="{icon}" $.version={version} $.outputFileName={msiName}""";
int r = run.console(mpdev, cl, tempDir);
if (r != 0) { print.it("<><c red>failed<>"); return; }

string msi = $@"{edDir}\{msiName}.msi";
_PatchMsi(msi);

//run.selectInExplorer(msi);
//run.it("msiexec.exe", $@"/i ""{msi}"""); //note: not as admin, it's not good for testing


void _CopyFiles() {
	string from = folders.Editor, to = tempDir + @"\_";
	string list = """
Au*.exe

Au*.dll
*MSTSCLib.dll
Microsoft.Web.WebView2.Core.dll
Microsoft.Web.WebView2.Wpf.dll
NuGet.*.dll

64\**
32\*

Au*.json
Au*.xml

default.exe.manifest
toc.json
toc-ai.yml
xrefmap.yml

runtimes\win-*64\native\WebView2Loader.dll

Default\**.xml
Default\Themes\*.csv
Default\Workspace\files\**

Templates\files.xml
Templates\files\**

*.db

Debugger\**
Roslyn\*.dll
""";
	/*

	*/
	
	//copy from `from` to `to` all files that don't exist in `to` or are different
	var hs1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	Matcher matcher = new();
	matcher.AddIncludePatterns(list.Lines());
	foreach (var s1 in matcher.GetResultsInFullPath(from)) {
		var rel = s1[from.Length..];
		hs1.Add(rel);
		var s2 = to + rel;
		if (!filesystem.getProperties(s1, out var p1)) throw new FileNotFoundException();
		if (filesystem.getProperties(s2, out var p2) && p2.LastWriteTimeUtc == p1.LastWriteTimeUtc && p2.Size == p1.Size) continue;
		print.it("Copying: " + s1);
		filesystem.copy(s1, s2, FIfExists.Delete);
	}
	
	//delete from `to` files and directories that are not in `from`
	_Dir(to);
	int _Dir(string dir) {
		int n = 0;
		foreach (var v in filesystem.enumerate(dir, FEFlags.UseRawPath).ToArray()) {
			n++;
			string path = v.FullPath;
			if (v.IsDirectory) {
				if (_Dir(path) > 0) continue;
			} else {
				if (hs1.Contains(path[to.Length..])) continue;
			}
			n--;
			print.it("Deleting: " + path);
			filesystem.delete(path);
		}
		return n;
	}
}

static void _PatchMsi(string msi) {
	dynamic installer = Activator.CreateInstance(Type.GetTypeFromProgID("WindowsInstaller.Installer")!);
	dynamic db = installer.OpenDatabase(msi, 1);
	
	//change the custom action flags
	_Update("UPDATE `CustomAction` SET `Type` = 1 WHERE `Action` = 'LA_BeforeCostFinalize'");
	_Update("UPDATE `CustomAction` SET `Type` = 1 WHERE `Action` = 'LA_BeforeValidate'");
	
	//disable restore point creation
	_Update("INSERT INTO `Property` (`Property`, `Value`) VALUES ('MSIFASTINSTALL', '1')");
	
	db.Commit();
	
	void _Update(string sql) {
		dynamic view = db.OpenView(sql);
		view.Execute();
		view.Close();
	}
}


class C {
	public static void Uninstall() {
		//this fails to uninstall other version
		//if (filesystem.exists(folders.ProgramFiles + @"LibreAutomate-msi\Au.Editor.exe"))
		//	run.it("msiexec.exe", $@"/x ""{folders.Editor + "LibreAutomate.msi"}"" /qn", flags: RFlags.InheritAdmin);
		
		if (GetUninstallString() is string s) {
			run.it("msiexec.exe", $@"{s} /qn", flags: RFlags.InheritAdmin);
		}
	}
	
	public static string GetUninstallString() {
		using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
		foreach (var name in key.GetSubKeyNames()) {
			if (name[0] != '{') continue;
			using var subkey = key.OpenSubKey(name);
			if (subkey.GetValue("DisplayName") as string == "LibreAutomate") {
				var s = subkey.GetValue("UninstallString") as string;
				if (s is null || !s.Starts("MsiExec.exe ", true)) continue;
				return s[12..];
			}
		}
		return null;
	}
}
