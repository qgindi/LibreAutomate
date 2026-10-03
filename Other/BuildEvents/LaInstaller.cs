/*/ define SCRIPT; nuget -\Microsoft.Extensions.FileSystemGlobbing; /*/
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Win32;

#if SCRIPT
print.clear();
print.qm2.clear();

#if true
var x = new LaInstaller(folders.Editor, inCI: false, testUpgrade: false);
x.Create();

//run.selectInExplorer(x.MsiFile);
//run.it("msiexec.exe", $@"/i ""{x.MsiFile}"""); //note: not as admin, it's not good for testing
#else
int r = run.console(folders.Editor + @"..\Other\BuildEvents\bin\Debug\BuildEvents.exe", "createInstaller");
print.it(r);
#endif
#endif

class LaInstaller(string outDir, bool inCI, bool testUpgrade = false) {
	const string c_tempDir = @"C:\Temp\Au\msi";
	
	public string MsiFile { get;private set; }
	
	public int Create() {
		if (inCI) {
			if (testUpgrade) throw new ArgumentException();
		} else {
			if (!testUpgrade) _Uninstall();
		}
		
		_CopyFiles();
		
		string solDir = pathname.getDirectory(outDir);
		string jsonFile = solDir + @"\Other\MsiCA\msi.json";
		Environment.SetEnvironmentVariable("_MSI_SOLUTION_DIR", solDir);
		
		string msiName = "LibreAutomate";
		string version = Au_.Version;
		if (testUpgrade) {
			var g = Version.Parse(version);
			version = $"{g.Major}.{g.Minor}.{g.Build + 1}";
			msiName = "LibreAutomate-" + version;
			print.it("Test upgrading", version);
		}
		
		string mpdev = folders.ProgramFiles + @"Master Packager Ltd\Master Packager Dev\mpdev.exe";
		string cl = $"""build "{jsonFile}" --properties $.outputDirectory="{outDir}" $.version={version} $.outputFileName={msiName}""";
		int r = run.console(mpdev, cl, c_tempDir);
		if (r != 0) { print.it("<><c red>failed<>"); return r; }
		
		MsiFile = $@"{outDir}\{msiName}.msi";
		
		_PatchMsi();

		return 0;
	}
	
	void _CopyFiles() {
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
		
		string to = c_tempDir + @"\_";
		filesystem.delete(to);
		Matcher matcher = new();
		matcher.AddIncludePatterns(list.Lines(noEmpty: true));
		foreach (var s1 in matcher.GetResultsInFullPath(outDir)) {
			var s2 = to + s1[outDir.Length..];
			filesystem.copy(s1, s2);
		}
	}
	
	void _PatchMsi() {
		dynamic installer = Activator.CreateInstance(Type.GetTypeFromProgID("WindowsInstaller.Installer"));
		dynamic db = installer.OpenDatabase(MsiFile, 1);
		
		//change custom action flags
		_Update("UPDATE `CustomAction` SET `Type` = 1 WHERE `Action` = 'LA_BeforeCostFinalize'");
		_Update("UPDATE `CustomAction` SET `Type` = 1 WHERE `Action` = 'LA_BeforeValidate'");
		
		db.Commit();
		
		void _Update(string sql) {
			dynamic view = db.OpenView(sql);
			view.Execute();
			view.Close();
		}
	}
	
	void _Uninstall() {
		//this fails to uninstall other version
		//if (filesystem.exists(folders.ProgramFiles + @"LibreAutomate\Au.Editor.exe"))
		//	run.it("msiexec.exe", $@"/x ""{outDir + "LibreAutomate.msi"}"" /qn", flags: RFlags.InheritAdmin);
		
		if (_GetUninstallString() is string s) {
			run.it("msiexec.exe", $@"{s} /qn", flags: RFlags.InheritAdmin);
		}
	}
	
	static string _GetUninstallString() {
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
