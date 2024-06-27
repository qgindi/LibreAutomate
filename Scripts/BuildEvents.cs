/// Build event script for Au.Editor and Cpp projects.

/*/ role exeProgram; testInternal Au; outputPath C:\code\au\Other\Programs; console true; /*/
/*/ role exeProgram; testInternal Au; outputPath %folders.ThisApp%\..\Other\Programs; console true; /*/

script.setup(exception: UExcept.Dialog | UExcept.Print);

//print.ignoreConsole = true;
//print.qm2.use = true;
//print.it(args);

return args[0] switch {
	"cppPostBuild" => CppPostBuild(), //$(SolutionDir)Other\Programs\BuildEvents.exe cppPostBuild $(Configuration) $(Platform)
	"preBuild" => EditorPreBuild(), //$(SolutionDir)Other\Programs\BuildEvents.exe preBuild $(Configuration)
	"postBuild" => EditorPostBuild(), //$(SolutionDir)Other\Programs\BuildEvents.exe postBuild $(Configuration)
	_ => 1
};

/// Exits editor. Copies AuCpp.dll and unloads the old dll from processes.
int CppPostBuild() {
	_ExitEditor();
	if (!_CopyAuCppDllIfNeed(args[2] != "x64", false)) return 1;
	return 0;
}

/// Exits editor. If need, copies AuCpp.dll and unloads the old dll from processes.
int EditorPreBuild() {
	_ExitEditor();
	_CopyAuCppDllIfNeed(false, true);
	_CopyAuCppDllIfNeed(true, true);
	return 0;
}

void _ExitEditor() {
	for (int i = 2; --i >= 0;) {
		var w = wnd.findFast(cn: "Au.Editor.TrayNotify");
		if (!w.Is0) {
			w.Close(noWait: true);
			w.WaitForClosed(-2, waitUntilProcessEnds: true);
		}
	}
}

bool _CopyAuCppDllIfNeed(bool bit32, bool editor) {
	var cd = Environment.CurrentDirectory;
	string src = pathname.normalize($@"{cd}\..\Cpp\bin\{args[1]}\{(bit32 ? "Win32" : "x64")}\AuCpp.dll");
	string dest = pathname.normalize($@"{cd}\..\_\{(bit32 ? "32" : "64")}\AuCpp.dll");
	if (!filesystem.getProperties(src, out var p1)) { if (!editor) print.it("Failed `filesystem.getProperties(src)`"); return false; }
	filesystem.getProperties(dest, out var p2);
	if (p1.LastWriteTimeUtc != p2.LastWriteTimeUtc || p1.Size != p2.Size) {
		print.it($"Updating {dest}");
		if (p2.Size != 0 && !Api.DeleteFile(dest)) {
			Cpp.Cpp_Unload(1);
			wait.until(-3, () => filesystem.delete(dest, FDFlags.CanFail) != false);
		}
		filesystem.copy(src, dest);
	}
	return true;
}

/// Creates Au.Editor.exe and Au.Task.exe.
/// Uses 64\Au.AppHost.exe as template. Adds resources.
/// Deletes Au.Editor.*.json files.
int EditorPostBuild() {
	//perf.first();
	string exe1 = "Au.Editor.exe", exe2 = "Au.Task.exe";
	if (script.testing) {
		exe1 = "Au.Editor2.exe"; exe2 = "Au.Task2.exe";
		print.ignoreConsole = true;
	}
	var dirOut = pathname.normalize(@"..\..\_", folders.ThisApp) + "\\";
	
	using var rk = Registry.CurrentUser.CreateSubKey(@"Software\Au\BuildEvents");
	var verResFile1 = folders.ThisApp + $"{exe1}.res";
	var verResFile2 = folders.ThisApp + $"{exe2}.res";
	
	var v = FileVersionInfo.GetVersionInfo(dirOut + "Au.Editor.dll");
	bool verChanged = !(rk.GetValue("version") is string s1 && s1 == v.FileVersion);
	//verChanged = true;
	if (verChanged || !filesystem.exists(verResFile1)) if (!_VersionInfo(verResFile1, exe1, "LibreAutomate C#")) return 1;
	if (verChanged || !filesystem.exists(verResFile2)) if (!_VersionInfo(verResFile2, exe2, "LibreAutomate miniProgram")) return 2;
	
	//TODO3: test https://github.com/resourcelib/resourcelib
	
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
	if (!_RunRhScript(s, exe1)) return 3;
	
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
		if (!_RunRhScript(s, exe2)) return 4;
	}
	
	if (verChanged) {
		rk.SetValue("version", v.FileVersion);
	}
	
	filesystem.delete(Directory.GetFiles(dirOut, "Au.Editor.*.json"));
	
	//perf.nw();
	return 0;
	
	bool _RunRhScript(string s, string fileName) {
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
}
