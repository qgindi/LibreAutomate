// Build event script for Au.Editor and Cpp projects.

using Microsoft.Win32;

script.setup(exception: UExcept.Dialog | UExcept.Print);

//print.ignoreConsole = true;
//print.qm2.use = true;
//print.it(args);

//if (args.Length == 0) { //dev
//	Environment.CurrentDirectory = @"C:\code\au";
//	//return GitBinaryFiles.PrePushHook();
//	return GitBinaryFiles.Restore(Environment.CurrentDirectory + "\\", true);
//}

string solutionDirBS = folders.ThisAppBS[..^28];

return args[0] switch {
	"cppPostBuild" => CppPostBuild(), //$(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe cppPostBuild $(Configuration) $(Platform)
	"preBuild" => EditorPreBuild(), //$(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe preBuild $(Configuration)
	"postBuild" => EditorPostBuild(), //$(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe postBuild $(Configuration)
	"dllPostBuild" => DllPostBuild(), //$(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe dllPostBuild "$(TargetPath)" $(Platform)
	"roslynPostBuild" => RoslynPostBuild(),
	"gitPrePushHook" => GitBinaryFiles.PrePushHook(),
	_ => 1
};

/// Exits editor. Copies AuCpp.dll and unloads the old dll from processes.
int CppPostBuild() {
	_ExitEditor();
	if (!_CopyAuCppDllIfNeed(args[2], false)) return 1;
	return 0;
}

/// Exits editor. If need, copies AuCpp.dll and unloads the old dll from processes.
int EditorPreBuild() {
	_ExitEditor();
	_CopyAuCppDllIfNeed("Win32", true);
	_CopyAuCppDllIfNeed("x64", true);
	_CopyAuCppDllIfNeed("ARM64", true);
	return GitBinaryFiles.Restore(solutionDirBS);
}

/// Exits editor. Copies the dll (eg Scintilla).
int DllPostBuild() {
	_ExitEditor();
	var toDir = $@"{solutionDirBS}_\{args[2] switch { "x64" => "64", "ARM64" => @"64\ARM", _ => throw new ArgumentException("platform") }}";
	filesystem.copyTo(args[1], toDir, FIfExists.Delete);
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

bool _CopyAuCppDllIfNeed(string platform, bool editor) {
	string src = $@"{solutionDirBS}Cpp\bin\{args[1]}\{platform}\AuCpp.dll";
	string dest = $@"{solutionDirBS}_\{platform switch { "Win32" => "32", "x64" => "64", "ARM64" => @"64\ARM", _ => throw new ArgumentException("platform") }}\AuCpp.dll";
	if (!filesystem.getProperties(src, out var p1)) { if (!editor) print.it("Failed `filesystem.getProperties(src)`"); return false; }
	filesystem.getProperties(dest, out var p2);
	if (p1.LastWriteTimeUtc != p2.LastWriteTimeUtc || p1.Size != p2.Size) {
		print.it($"Updating {dest}");
		if (p2.Size != 0 && !_Api.DeleteFile(dest)) {
			_Api.Cpp_Unload(1);
			wait.until(-3, () => filesystem.delete(dest, FDFlags.CanFail) != false);
		}
		filesystem.copy(src, dest);
	}
	return true;
}

/// Creates Au.Editor.exe and Au.Task.exe for x64 and ARM64.
/// Uses our apphost.exe as template. Adds resources.
/// Deletes Au.Editor.*.json files.
int EditorPostBuild() {
	//perf.first();
	var dirOut = solutionDirBS + @"_\";

#if !true //copy output files from `$(ProjectDir)$(OutDir)` to dirOut. Bad: can't start LA from VS (no program path setting in UI; VS ignores executablePath in launchsettings.json).
	var dirBin = args[2][..^1];
	int rce = run.console(out string rco, "robocopy.exe", $"{dirBin} {dirOut} /s /xd runtimes /xf *.json *.exe");
	if (rce >= 8) { print.it(rco); return 10; }
	foreach (var rtd in new string[] { @"\runtimes\win-x64", @"\runtimes\win-arm64" }) {
		var sd1 = dirBin + rtd;
		if (filesystem.exists(sd1)) run.console("robocopy.exe", $"{sd1} {dirOut + rtd} /s /xf *.json *.exe");
	}
#else //use `<OutDir>`. Bad: VS adds unwanted files to dirOut. Eg from NuGet packages may add native dlls for many unused OS/platforms.
	//filesystem.delete(Directory.GetFiles(dirOut, "..."));
#endif

	//make sure `.git\hooks\pre-push` exists. See `PrePushHook` in `GitBinaryFiles.cs`.
	var prePush = solutionDirBS + @".git\hooks\pre-push";
	if (!filesystem.exists(prePush, true)) {
		filesystem.saveText(prePush, """
#!/bin/sh

"Other/BuildEvents/bin/Debug/BuildEvents.exe" "gitPrePushHook"
exit $?

""");
	}

	string exe1 = "Au.Editor.exe", exe2 = "Au.Task.exe";
	if (script.testing) {
		exe1 = "Au.Editor2.exe"; exe2 = "Au.Task2.exe";
		print.ignoreConsole = true;
		//todo: Environment.CurrentDirector = 
	}

	//This script creates both x64 and arm64 Au.Editor.exe and Au.Task.exe. Adds resources.
	//It would be difficult or impossible in VS. Eg can't add multiple icons, can't quickly build both x64 and arm64.

	using var rk = Registry.CurrentUser.CreateSubKey(@"Software\Au\BuildEvents");
	var verResFile1 = folders.ThisApp + $"{exe1}.res";
	var verResFile2 = folders.ThisApp + $"{exe2}.res";

	var v = FileVersionInfo.GetVersionInfo(dirOut + "Au.Editor.dll");
	bool verChanged = !(rk.GetValue("version") is string s1 && s1 == v.FileVersion);
	//verChanged = true;
	if (verChanged || !filesystem.exists(verResFile1)) if (!_VersionInfo(verResFile1, exe1, "LibreAutomate")) return 1;
	if (verChanged || !filesystem.exists(verResFile2)) if (!_VersionInfo(verResFile2, exe2, "LibreAutomate miniProgram")) return 2;

	//TODO3: test https://github.com/resourcelib/resourcelib

	_EnsureApphostOK(dirOut);

	for (int i = 0; i < 2; i++) {
		string appHost = $@"64\{(i == 1 ? @"ARM\" : "")}apphost.exe";
		string exe1Arch = exe1.Insert(^4, i == 0 ? "" : "-arm"), exe2Arch = exe2.Insert(^4, i == 0 ? "" : "-arm");
		var s = $"""
[FILENAMES]
Exe={appHost}
SaveAs={exe1Arch}
[COMMANDS]
-add ..\Au.Editor\Resources\ico\app.ico, ICONGROUP,32512,0
-add ..\Au.Editor\Resources\ico\app_disabled.ico, ICONGROUP,32513,0
-add ..\Au.Editor\Resources\ico\PictureInPicture.ico, ICONGROUP,32514,0
-addoverwrite ..\Au.Editor\Resources\Au.manifest, MANIFEST,1,0
-add "{verResFile1}", VERSIONINFO,1,0

""";
		if (!_RunRhScript(s, exe1Arch)) return 3;
		_PatchApphost(dirOut + exe1Arch, "Au.Editor.dll");

		filesystem.getProperties(dirOut + appHost, out var p1);
		if (verChanged || !filesystem.getProperties(dirOut + exe2Arch, out var p2) || p1.CreationTimeUtc > p2.LastWriteTimeUtc) {
			print.it($"Creating {exe2Arch}");
			s = $"""
[FILENAMES]
Exe={appHost}
SaveAs={exe2Arch}
[COMMANDS]
-add ..\Au.Editor\Resources\ico\Script.ico, ICONGROUP,32512,0
-addoverwrite ..\Au.Editor\Resources\Au.manifest, MANIFEST,1,0
-add "{verResFile2}", VERSIONINFO,1,0

""";
			if (!_RunRhScript(s, exe2Arch)) return 4;
			_PatchApphost(dirOut + exe2Arch, "Au.Task.dll");
		}

	}

	filesystem.copy(dirOut + "Au.Editor.deps.json", dirOut + "Au.Editor-arm.deps.json", FIfExists.Delete);
	filesystem.copy(dirOut + "Au.Task.deps.json", dirOut + "Au.Task-arm.deps.json", FIfExists.Delete);
	filesystem.copy(dirOut + "Au.Editor.runtimeconfig.json", dirOut + "Au.Editor-arm.runtimeconfig.json", FIfExists.Delete);
	filesystem.copy(dirOut + "Au.Task.runtimeconfig.json", dirOut + "Au.Task-arm.runtimeconfig.json", FIfExists.Delete);

	if (verChanged) {
		rk.SetValue("version", v.FileVersion);
	}

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
		var rh = solutionDirBS + @"Other\BuildEvents\.tools\ResourceHacker";
		int r = run.console(rh + ".exe", cl, dirOut);
		if (r == 0) return true;
		print.it(File.ReadAllText(rh + ".log", Encoding.Unicode)); //RH is not a console program and we cannot capture its console output.
		return false;
	}

	static unsafe void _PatchApphost(string path, string name) {
		//write dll name
		var b = File.ReadAllBytes(path);
		int i = b.AsSpan().IndexOf("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2"u8);
		i += Encoding.UTF8.GetBytes(name, 0, name.Length, b, i);
		b.AsSpan(i, 64).Clear();

		//set subsystem = GUI (default is console)
		fixed (byte* p = b) {
			uint subsystemOffset = *(uint*)(p + 0x3C) + 0x5C;
			*(ushort*)(p + subsystemOffset) = 2;
		}

		filesystem.saveBytes(path, b);
	}

	//Copies apphost.exe of all platforms from SDK if need.
	static bool _EnsureApphostOK(string dirOut) {
		var packs = @"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Host.win-";
		var version = new DirectoryInfo(packs + "x64")
			.GetDirectories(Environment.Version.ToString(2) + ".*")
			.MaxBy(o => o.Name[..(o.Name.LastIndexOf('.') + 1)].ToInt())
			.Name;

		string[] platforms = ["x64", "arm64", "x86"], platforms2 = ["64", @"64\ARM", "32"];
		foreach (var (i, plat) in platforms.Index()) {
			var path = $@"{packs}{plat}\{version}\runtimes\win-{plat}\native\apphost.exe";
			var path2 = dirOut + platforms2[i] + @"\apphost.exe";

			if (!filesystem.getProperties(path, out var p1)) { print.it("Not found: " + path); return false; }
			if (!filesystem.getProperties(path2, out var p2) || p1.LastWriteTimeUtc > p2.LastWriteTimeUtc) {
				print.it("Updating " + path2);
				filesystem.copy(path, path2, FIfExists.Delete);
			}
		}
		return true;
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

//Exits editor. Copies dlls etc.
int RoslynPostBuild() {
	_ExitEditor();

	var from = args[1].Trim();
	var to = $@"{solutionDirBS}_\Roslyn";

	foreach (var f in filesystem.enumFiles(to)) {
		filesystem.delete(f.FullPath, FDFlags.CanFail);
	}
	foreach (var f in filesystem.enumFiles(from)) {
		if (0 == f.Name.Ends(true, ".dll", ".xml")) continue;
		if (0 != f.Name.Starts(true, "System.Configuration.", "System.Security.")) continue;
		filesystem.copyTo(f.FullPath, to);
	}
	return 0;
}

unsafe class _Api : NativeApi {
	[DllImport("kernel32.dll", EntryPoint = "DeleteFileW", SetLastError = true)]
	internal static extern bool DeleteFile(string lpFileName);

	/// <param name="flags">1 - wait less.</param>
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_Unload(uint flags);
}
