// Build event script for Au.Editor and other projects.

using Vestris.ResourceLib;

string solutionDirBS = folders.ThisAppBS[..^28];

bool atHome = solutionDirBS.Eqi(@"C:\code\au\") && Environment.GetEnvironmentVariable("Au.Home<PC>") == "1";
bool inCI = !atHome && Environment.GetEnvironmentVariable("CI") == "true";
//bool inGithubActions = !atHome && Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true"; //inCI true too

script.setup(exception: inCI ? UExcept.Print : UExcept.Dialog | UExcept.Print);

//print.ignoreConsole = true;
//print.qm2.use = true;
//print.it(args);

//if (args.Length == 0) { //dev
//	Environment.CurrentDirectory = @"C:\code\au";
//	//return GitBinaryFiles.PrePushHook();
//	return GitBinaryFiles.Restore(Environment.CurrentDirectory + "\\", true);
//}

return args[0] switch {
	"cppPostBuild" => CppPostBuild(), //project Cpp: $(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe cppPostBuild $(Configuration) $(Platform)
	"dllPostBuild" => DllPostBuild(), //other C++ dll projects (Scintilla): $(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe dllPostBuild "$(TargetPath)" $(Platform)
	"preBuild" => EditorPreBuild(), //project Au.Editor: $(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe preBuild $(Configuration)
	"postBuild" => EditorPostBuild(), //project Au.Editor: $(SolutionDir)Other\BuildEvents\bin\Debug\BuildEvents.exe postBuild $(Configuration)
	"roslynPostBuild" => RoslynPostBuild(),
	"createInstaller" => CreateInstaller(),
	"gitPrePushHook" => atHome ? GitBinaryFiles.PrePushHook() : 0,
	_ => 1
};

/// Exits editor. Copies AuCpp.dll and unloads the old dll from processes.
int CppPostBuild() {
	_ExitEditor();
	if (!_CopyAuCppDllIfNeed(args[2], false)) return 1;
	return 0;
}

/// Exits editor. Copies the dll (eg Scintilla).
int DllPostBuild() {
	_ExitEditor();
	var toDir = $@"{solutionDirBS}_\{args[2] switch { "x64" => "64", "ARM64" => @"64\ARM", _ => throw new ArgumentException("platform") }}";
	filesystem.copyTo(args[1], toDir, FIfExists.Delete);
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

void _ExitEditor() {
	if (inCI || Environment.GetEnvironmentVariable("NO_EXIT_EDITOR") != null) return;

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
	if (atHome) {
		var prePush = solutionDirBS + @".git\hooks\pre-push";
		if (!filesystem.exists(prePush, true)) {
			filesystem.saveText(prePush, """
#!/bin/sh

"Other/BuildEvents/bin/Debug/BuildEvents.exe" "gitPrePushHook"
exit $?

""");
		}
	}

	bool _VersionChanged() {
		try {
			var v = Au_.Version;
			var v2 = FileVersionInfo.GetVersionInfo(dirOut + "Au.Editor-arm.exe").FileVersion;
			var v3 = FileVersionInfo.GetVersionInfo(dirOut + "Au.Task-arm.exe").FileVersion;
			var v4 = FileVersionInfo.GetVersionInfo(dirOut + "Au.Task.exe").FileVersion;
			return !(v2 == v && v3 == v && v4 == v);
		}
		catch (FileNotFoundException) { return true; }
	}

	if (_VersionChanged()) {
		print.it("Creating arm64 exe files and Au.Task.exe.");

		if (atHome) //else GitBinaryFiles.Restore downloads apphosts
			if (!_EnsureApphostOK(dirOut)) return 1;
		_CreateAuTaskExe();
		_CreateArmExe(true);
		_CreateArmExe(false);
	}

	_AddResourcesToExe(dirOut + "Au.Editor.exe", true);

	return 0;

	void _CreateAuTaskExe() {
		string exe = dirOut + "Au.Task.exe";

		filesystem.copy(dirOut + @"64\apphost.exe", exe, FIfExists.Delete);
		_PatchApphost(exe, "Au.Editor.dll");
		_AddResourcesToExe(exe, false);
	}

	void _CreateArmExe(bool editor) {
		string fn = editor ? "Au.Editor" : "Au.Task";
		string armExe = dirOut + fn + "-arm.exe";

		filesystem.copy(dirOut + @"64\arm\apphost.exe", armExe, FIfExists.Delete);
		_PatchApphost(armExe, "Au.Editor.dll");
		_AddResourcesToExe(armExe, editor);

		if (editor) {
			filesystem.copy(dirOut + fn + ".deps.json", dirOut + fn + "-arm.deps.json", FIfExists.Delete);
			filesystem.copy(dirOut + fn + ".runtimeconfig.json", dirOut + fn + "-arm.runtimeconfig.json", FIfExists.Delete);
		}
	}

	void _AddResourcesToExe(string exePath, bool editor) {
		var r = new ExeResources(exePath, solutionDirBS);
		r.AddManifest("Au.manifest");
		if (editor) r.AddIcons("app.ico", "app_disabled.ico", "PictureInPicture.ico"); else r.AddIcons("Script.ico");
		r.AddVersion(editor ? "LibreAutomate" : "LibreAutomate miniProgram");
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

int CreateInstaller() {
	try {
		var x = new LaInstaller(solutionDirBS + @"_\", inCI);
		x.Create();
	}
	catch (Exception ex) {
		print.it("Failed to create installer. " + ex);
		return 1;
	}
	return 0;
}

class ExeResources(string exe, string solutionDirBS) {
	string _resDirBS = solutionDirBS + @"Au.Editor\resources\";

	public void AddManifest(string file) {
		var m = new GenericResource(new(Kernel32.ResourceTypes.RT_MANIFEST), new(1), 0); //note: ManifestResource writes XML incorrectly
		m.Data = filesystem.loadBytes(_resDirBS + file);
		m.SaveTo(exe);
	}

	public void AddIcons(params string[] files) {
		int idDir = 32512, idIcon = 1;
		foreach (var file in files) {
			var ico = new IconFile($@"{_resDirBS}ico\{file}");
#if false //bug: overwrites previously added icons
			var idr = new IconDirectoryResource(ico) { Name = new(idDir++) };
			idr.SaveTo(exe);
#else
			var idr = new IconDirectoryResource { Name = new(idDir++) };
			List<IconResource> a = [];
			foreach (var v in ico.Icons) {
				a.Add(new(v, new(idIcon++), 0));
			}
			idr.Icons = a;
			idr.SaveTo(exe);
#endif
		}
	}

	public void AddVersion(string description) {
		var v = new VersionResource { Language = 0 };
		v.LoadFrom(process.thisExePath);
		var k = (StringFileInfo)v["StringFileInfo"];
		k["FileDescription"] = description + "\0";
		string fn = pathname.getName(exe);
		k["InternalName"] = k["OriginalFilename"] = fn + "\0";
		v.SaveTo(exe);
	}
}

unsafe class _Api : NativeApi {
	[DllImport("kernel32.dll", EntryPoint = "DeleteFileW", SetLastError = true)]
	internal static extern bool DeleteFile(string lpFileName);

	/// <param name="flags">1 - wait less.</param>
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_Unload(uint flags);
}
