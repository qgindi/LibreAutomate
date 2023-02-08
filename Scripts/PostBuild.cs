/// Au.Editor project post-build script.
/// Post-build event in project Properties: "$(SolutionDir)Other\Programs\PostBuild.exe"
/// Creates Au.Editor.exe and Au.Task.exe.
/// Uses 64\Au.AppHost.exe as template. Adds resources.
/// Deletes Au.Editor.*.json files.

/*/ role exeProgram; outputPath %folders.ThisApp%\..\Other\Programs; console true; /*/

//perf.first();
string exe1 = "Au.Editor.exe", exe2 = "Au.Task.exe";
if(script.testing) {
	exe1 = "Au.Editor2.exe"; exe2 = "Au.Task2.exe";
	print.ignoreConsole=true;
}
var dirOut = pathname.normalize(@"..\..\_", folders.ThisApp) + "\\";

using var rk = Registry.CurrentUser.CreateSubKey(@"Software\Au\PostBuild");
var verResFile1 = folders.ThisApp + $"{exe1}.res";
var verResFile2 = folders.ThisApp + $"{exe2}.res";

var v = FileVersionInfo.GetVersionInfo(dirOut + "Au.Editor.dll");
bool verChanged = !(rk.GetValue("version") is string s1 && s1 == v.FileVersion);
verChanged = true;//TODO
if (verChanged || !filesystem.exists(verResFile1)) if (!_VersionInfo(verResFile1, exe1, "LibreAutomate C#")) return 1;
if (verChanged || !filesystem.exists(verResFile2)) if (!_VersionInfo(verResFile2, exe2, "LibreAutomate miniProgram")) return 2;

var s = $"""
[FILENAMES]
Exe=64\Au.AppHost.exe
SaveAs={exe1}
[COMMANDS]
-add ..\Au.Editor\Resources\ico\app.ico, ICONGROUP,32512,0
-add ..\Au.Editor\Resources\ico\app_disabled.ico, ICONGROUP,32513,0
-addoverwrite ..\Au.Editor\Resources\Au.manifest, MANIFEST,1,0
-add dotnet_ref_editor.txt, 220,1,0
-add "{verResFile1}", VERSIONINFO,1,0

""";
if (!_RunScript(s, exe1)) return 3;

filesystem.getProperties(dirOut + @"64\Au.AppHost.exe", out var p1);
if (verChanged || !filesystem.getProperties(dirOut + exe2, out var p2) || p1.LastWriteTimeUtc > p2.LastWriteTimeUtc) {
	print.it($"Creating {exe2}");
	s = $"""
[FILENAMES]
Exe=64\Au.AppHost.exe
SaveAs={exe2}
[COMMANDS]
-add ..\Au.Editor\Resources\ico\Script.ico, ICONGROUP,32512,0
-addoverwrite ..\Au.Editor\Resources\Au.manifest, MANIFEST,1,0
-add dotnet_ref_task.txt, 220,1,0
-add "{verResFile2}", VERSIONINFO,1,0

""";
	if (!_RunScript(s, exe2)) return 4;
}

if (verChanged) {
	rk.SetValue("version", v.FileVersion);
}

//filesystem.delete(Directory.GetFiles(dirOut, "Au.Editor.*.json"));//TODO

//perf.nw();
return 0;

bool _RunScript(string s, string fileName) {
	var exePath = dirOut + fileName;
	filesystem.delete(exePath);
	using var tf = new TempFile();
	filesystem.saveText(tf, s);
	if (!_RunCL($"-script \"{tf.File}\"")) return false;
	var dt = DateTime.UtcNow;
	File.SetCreationTimeUtc(exePath, dt);
	File.SetLastWriteTimeUtc(exePath, dt);
	return true;
}

bool _RunCL(string cl) {
	int r = run.console(folders.ThisApp + "ResourceHacker.exe", cl, dirOut);
	if (r == 0) return true;
	print.it(File.ReadAllText(folders.ThisApp + "ResourceHacker.log", Encoding.Unicode)); //RH is not a console program and we cannot capture its console output.
	return false;
}

bool _VersionInfo(string resFile, string fileName, string fileDesc) {
	print.it($"Creating version resource for {fileName}");
	var rc = $$"""

1 VERSIONINFO
FILEVERSION {{v.FileMajorPart}},{{v.FileMinorPart}},{{v.FileBuildPart}},{{v.FilePrivatePart}}
PRODUCTVERSION {{v.ProductMajorPart}},{{v.ProductMinorPart}},{{v.ProductBuildPart}},{{v.ProductPrivatePart}}
FILEOS 0x4
FILETYPE 0x1
{
BLOCK "StringFileInfo"
{
	BLOCK "000004b0"
	{
		VALUE "CompanyName", "{{v.CompanyName}}"
		VALUE "FileDescription", "{{fileDesc}}"
		VALUE "FileVersion", "{{v.FileVersion}}"
		VALUE "InternalName", "{{fileName[..^4]}}"
		VALUE "LegalCopyright", "Copyright 2020-{{DateTime.UtcNow.Year}} Gintaras Didžgalvis"
		VALUE "OriginalFilename", "{{fileName}}"
		VALUE "ProductName", "{{v.ProductName}}"
		VALUE "ProductVersion", "{{v.ProductVersion}}"
	}
}

BLOCK "VarFileInfo"
{
	VALUE "Translation", 0x0000 0x04B0  
}
}

""";
	using var rcFile = new TempFile(".rc");
	filesystem.saveText(rcFile, rc, encoding: Encoding.UTF8);
	return _RunCL($"""-open "{rcFile}" -save "{resFile}" -action compile""");
}
