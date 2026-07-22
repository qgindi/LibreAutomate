// Build event script for Au.Editor and Cpp projects.

using Microsoft.Win32;
using Vestris.ResourceLib;

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
	var dirOut = solutionDirBS + @"_\";

	//make sure `.git\hooks\pre-push` exists. See `PrePushHook` in `GitBinaryFiles.cs`.
	var prePush = solutionDirBS + @".git\hooks\pre-push";
	if (!filesystem.exists(prePush, true)) {
		filesystem.saveText(prePush, """
#!/bin/sh

"Other/BuildEvents/bin/Debug/BuildEvents.exe" "gitPrePushHook"
exit $?

""");
	}

	//How native resources (version info, icons, manifest) are added to LA program files:
	//1. To change LA/Au version, run script "LA version and resources.cs".
	//		It changes Au_.Version in global2.cs, and using rc.exe creates .res files for Au.Editor and Au.Task projects. These files are specified in project properties.
	//2. Build Au.Editor project. Because of dependencies and build order, it at first builds Au, then Au.Task, then Au.Editor.
	//		Now we have Au.dll with new Au_.Version, Au.Task.exe with new resources, and Au.Editor.exe with new resources.
	//3. This code runs in Au.Editor post-build.
	//		If version changed or an arm64 file does not exist:
	//			Copies the arm64 apphost.exe to Au.Editor-arm.exe and Au.Task.exe, patches them, and copies resources into them from the x64 exe files.
	//			Also copies json files for the arm64 programs.

	using var rk = Registry.CurrentUser.CreateSubKey(@"Software\Au\BuildEvents");
	bool verChanged = rk.GetValue("version") as string != Au_.Version;
	verChanged = true;//TODO
	if (!verChanged && filesystem.exists(dirOut + "Au.Editor-arm.exe") && filesystem.exists(dirOut + "Au.Task-arm.exe")) return 0;
	print.it("Creating arm64 exe files.");

	_EnsureApphostOK(dirOut);
	_CreateArmExe(true);
	_CreateArmExe(false);

	void _CreateArmExe(bool editor) {
		string fn = editor ? "Au.Editor" : "Au.Task";
		string armExe = dirOut + fn + "-arm.exe";

		filesystem.copy(dirOut + @"64\arm\apphost.exe", armExe, FIfExists.Delete);
		_PatchApphost(armExe, fn + ".dll");

		_CopyResources(dirOut + fn + ".exe", armExe);

		filesystem.copy(dirOut + fn + ".deps.json", dirOut + fn + "-arm.deps.json", FIfExists.Delete);
		filesystem.copy(dirOut + fn + ".runtimeconfig.json", dirOut + fn + "-arm.runtimeconfig.json", FIfExists.Delete);
	}

	rk.SetValue("version", Au_.Version);
	return 0;

	static unsafe void _PatchApphost(string path, string dllFilename) {
		//write dll name
		var b = File.ReadAllBytes(path);
		int i = b.AsSpan().IndexOf("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2"u8);
		i += Encoding.UTF8.GetBytes(dllFilename, 0, dllFilename.Length, b, i);
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

	static void _CopyResources(string from, string to) {
		var vi = new ResourceInfo();
		vi.Load(from);
		foreach (ResourceId rt in vi.ResourceTypes) {
			if (rt.Id == 3) continue; //ICON
			foreach (Resource resource in vi.Resources[rt]) {
				resource.SaveTo(to);
			}
		}
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
