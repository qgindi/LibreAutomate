// Build event script for Au.Editor and Cpp projects.

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

/// Creates Au.Editor.exe and Au.Task.exe for ARM64. Also Au.Task.exe for x64.
/// Uses our apphost.exe as template. Adds resources.
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
	//		It changes Au_.Version in global2.cs, and using rc.exe creates .res files for Au.Editor.exe and Au.Task.exe.
	//2. Build Au.Editor project. It adds the .res to Au.Editor.exe.
	//3. This code runs in Au.Editor post-build.
	//		If an exe file does not exist or its version != that of Au.Editor.exe:
	//			Creates Au.Task.exe ands adds the .res.
	//			Creates Au.Editor-arm.exe and Au.Task-arm.exe. Copies resources from the x64 exe files.
	//			Also copies json files.

	bool _VersionChanged() {
		try {
			var v = FileVersionInfo.GetVersionInfo(dirOut + "Au.Editor.exe");
			var v2 = FileVersionInfo.GetVersionInfo(dirOut + "Au.Editor-arm.exe");
			var v3 = FileVersionInfo.GetVersionInfo(dirOut + "Au.Task-arm.exe");
			var v4 = FileVersionInfo.GetVersionInfo(dirOut + "Au.Task.exe");
			return !(v2.FileVersion == v.FileVersion && v3.FileVersion == v.FileVersion && v4.FileVersion == v.FileVersion);
		}
		catch (FileNotFoundException) { return true; }

		//This is fast enough. Don't use Au_.Version, because we use Au.dll from NuGet, not the newest one (it would cause circular reference).
	}

	if (!_VersionChanged()) return 0;
	print.it("Creating arm64 exe files and Au.Task.exe.");

	if (!_EnsureApphostOK(dirOut)) return 1;
	_CreateAuTaskExe();
	_CreateArmExe(true);
	_CreateArmExe(false);

	return 0;

	void _CreateArmExe(bool editor) {
		string fn = editor ? "Au.Editor" : "Au.Task";
		string armExe = dirOut + fn + "-arm.exe";

		filesystem.copy(dirOut + @"64\arm\apphost.exe", armExe, FIfExists.Delete);
		_PatchApphost(armExe, "Au.Editor.dll");

		_CopyResources(dirOut + fn + ".exe", armExe);

		if (editor) {
			filesystem.copy(dirOut + fn + ".deps.json", dirOut + fn + "-arm.deps.json", FIfExists.Delete);
			filesystem.copy(dirOut + fn + ".runtimeconfig.json", dirOut + fn + "-arm.runtimeconfig.json", FIfExists.Delete);
		}
	}

	static unsafe void _PatchApphost(string path, string dllFilename) {
		//write dll name
		var bytes = filesystem.loadBytes(path);
		Span<byte> b = bytes;
		int i = b.IndexOf("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2"u8);
		i += Encoding.UTF8.GetBytes(dllFilename, b[i..]);
		b.Slice(i, 64).Clear();

		//set subsystem = GUI (default is console)
		fixed (byte* p = b) {
			uint subsystemOffset = *(uint*)(p + 0x3C) + 0x5C;
			*(ushort*)(p + subsystemOffset) = 2;
		}

		filesystem.saveBytes(path, bytes);
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

				Span<byte> b = filesystem.loadBytes(path2);
				if (b.IndexOf("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2"u8) < 0) { print.it("String 1 not found in " + path2); return false; }
				if (b.IndexOf("\0\019ff3e9c3602ae8e841925bb461a0adb064a1f1903667a5e0d87e8f608f425ac"u8) < 0) { print.it("String 2 not found in " + path2); return false; }
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

	static void _AddResToExe(string exePath, string resPath) {
		string tempDir = folders.Temp + $@"\res_{Guid.NewGuid()}\";
		Directory.CreateDirectory(tempDir);
		try {
			string csFile = tempDir + "empty.cs";
			string tempDll = tempDir + "resources.dll";

			File.WriteAllText(csFile, "");

			string arm = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "Arm" : "";
			string csc = folders.Windows + $@"Microsoft.NET\Framework{arm}64\v4.0.30319\csc.exe";
			string cl = $"/target:library /out:\"{tempDll}\" /win32res:\"{resPath}\" \"{csFile}\"";
			int ec = run.console(out var s, csc, cl);
			if (ec != 0) throw new Exception($"csc failed.\r\n{s}");

			_CopyResources(tempDll, exePath);

			//Or we can add resources directly from ico etc. But may be difficult to add version resource.
		}
		finally {
			try { Directory.Delete(tempDir, true); }
			catch { }
		}
	}

	void _CreateAuTaskExe() {
		string fn = "Au.Task";
		string exe = dirOut + fn + ".exe";

		filesystem.copy(dirOut + @"64\apphost.exe", exe, FIfExists.Delete);
		_PatchApphost(exe, "Au.Editor.dll");

		_AddResToExe(exe, solutionDirBS + $@"Au.Editor\resources\Au.Task.exe.res");
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
