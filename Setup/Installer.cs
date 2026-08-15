#if !EMPTY
using System.IO.Compression;

class Installer {
	readonly string _dir, _dirBS;
	readonly Action<string> _log;
	readonly Util.DownloadProgress _progress;
	string _progressPrefix;
	Version _prevVersion;
	long _sizeOfInstalledFiles;
	HashSet<string> _hsPaths = new(StringComparer.OrdinalIgnoreCase);
	
	public static bool Silent { get; set; }
	
	public Installer(string dir, Action<string> log) {
		_dir = Path.GetFullPath(dir.TrimEnd('\\', '/')); //callers ensure it's full path, but it may be with .. or ~ etc
		_dirBS = _dir + "\\";
		_log = log;
		_progress = Silent ? null : (total, downloaded) => { if (total > 0) _log($"{_progressPrefix}, {downloaded / (1024 * 1024)} / {total / (1024 * 1024)} MB"); };
	}
	
	public bool InstallApp() {
		try {
			if (!_EnsureAppDirOK()) return false;
			_InstallFiles();
			
#if NET
			_Delete("uninstall.exe"); //to avoid confusion when testing. It would not start because the companion dll and json files don't exist. We could copy them for testing, but it may cause confusion, eg can't start if different version. To test uninstalling, either run uninstall.exe created by VS or run this from outside.
#else
			string thisExe = Util.ThisExePath;
			File.Copy(thisExe, _dirBS + "uninstall.exe", overwrite: true);
#endif
			
			_log("Creating other directories");
			Util.CreateWritableDirectory(_dirBS + "Git");
			Util.CreateWritableDirectory(_dirBS + "SDK");
			Util.CreateWritableDirectory(folders.ProgramData + App.AppName);
			
			_log("Creating shortcuts");
			Util.CreateShortcut(folders.CommonPrograms + App.AppName + ".lnk", _dirBS + "Au.Editor.exe");
			
			_log("Writing registry entries");
			Reg.Install(_dir, _sizeOfInstalledFiles);
		}
		catch (Exception ex) {
			if (!Silent) App.Msgbox("Failed.", ex);
			return false;
		}
		return true;
	}
	
	void _InstallFiles() {
		int nPacks = 2;
		for (int i = 0; i < nPacks; i++) {
			_GetFilesFromLzma(i + 1);
		}
		
		_log($"Copying files");
		DontInterrupt = true;
		try {
			for (int i = 1; i <= nPacks; i++) {
				var tempZipFile = _dirBS + $"offline-{i}.zip";
				if (i > 1 && !File.Exists(tempZipFile)) continue; //existing files are up to date
				_ExtractZip(tempZipFile);
				_Delete(tempZipFile);
			}
			if (Util.IsArm64) {
				string s = _dirBS + "Au.Editor.exe";
				api.MoveFileEx(s, _dirBS + "Au.Editor-x64.exe", api.MOVEFILE_REPLACE_EXISTING);
				if (!api.MoveFileEx(_dirBS + "Au.Editor-arm.exe", s, 0)) throw new Exception("Failed to rename Au.Editor-arm.exe");
				_hsPaths.Remove("Au.Editor-arm.exe");
				_hsPaths.Add("Au.Editor-x64.exe");
			}
			_DeleteOldFilesAndWriteInstalledTxt();
		}
		finally { DontInterrupt = false; }
	}
	
	public static bool DontInterrupt { get; private set; }
	
	public static bool IsOffline => s_isOffline ??=
		Util.HashFiles([folders.ThisAppBS + "offline-1.zip.lzma"]) == Hardcoded.HashOfLzmaFile1
		&& Util.HashFiles([folders.ThisAppBS + "offline-2.zip.lzma"]) == Hardcoded.HashOfLzmaFile2;
	static bool? s_isOffline;
	
	void _GetFilesFromLzma(int index) {
		string packName = index switch { 1 => "program", 2 => "data", _ => throw null };
		_log(_progressPrefix = $"Downloading {packName} files");
		
		string lzmaFilename = $"offline-{index}.zip.lzma", lzmaFile = folders.ThisAppBS + lzmaFilename;
		bool downloadLzma = !IsOffline;
		if (downloadLzma && index > 1) {
			string[] a = null;
			if (index == 2) downloadLzma = Util.HashFiles(a = ["doc.db", "icons.db", "ref.db", "winapi.db"], _dirBS) != Hardcoded.HashOfFiles2;
			if (!downloadLzma) {
				foreach (var v in a) {
					_sizeOfInstalledFiles += new FileInfo(_dirBS + v).Length;
					_hsPaths.Add(v); //note: if in the future will add files in a subdir, will need to add the subdir at first
				}
				return;
			}
		}
		
		if (downloadLzma) {
			lzmaFile = _dirBS + lzmaFilename;
			using var http = Util.CreateHttpClient(true);
#if DEV
			var url = "https://github.com/qgindi/LA-downloads/releases/download/v1.0.0/" + lzmaFilename;
#else
			var url = $"https://github.com/qgindi/LibreAutomate/releases/download/v{App.AppVersion}/" + lzmaFilename;
#endif
			http.Download(url, lzmaFile, nRetry: Silent ? 5 : 1, _progress);
		}
		
		_log($"Decompressing {packName} files");
		var tempZipFile = _dirBS + $"offline-{index}.zip";
		SevenZip.LzmaAlone.Decompress(lzmaFile, tempZipFile);
		if (downloadLzma) _Delete(lzmaFile);
	}
	
	void _ExtractZip(string zipFile) {
		using ZipArchive zip = ZipFile.OpenRead(zipFile);
		List<(string file, ZipArchiveEntry z)> aFiles = [], aLocked = [];
		
		foreach (var z in zip.Entries) {
			var relPath = z.FullName.Replace('/', '\\');
			var dest = _dirBS + relPath;
			
			//create parent directory if need. The zip file may not contain entries for directories.
			for (int i = 0; (i = relPath.IndexOf('\\', i)) > 0;) { //add to hs in correct order (oldest ancestors first)
				var s = relPath[..i++];
				if (_hsPaths.Add(s)) Directory.CreateDirectory(_dirBS + s);
			}
			
			if (string.IsNullOrEmpty(z.Name)) { //directory
				if (_hsPaths.Add(relPath)) Directory.CreateDirectory(dest);
				continue;
			}
			
			_sizeOfInstalledFiles += z.Length;
			aFiles.Add((dest, z));
			_hsPaths.Add(relPath);
		}
		
		foreach (var (dest, z) in aFiles) {
			var attr = api.GetFileAttributes(dest);
			if ((int)attr == -1) {
				z.ExtractToFile(dest, true);
			} else {
				if (attr.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(dest, attr & ~FileAttributes.ReadOnly);
				if (!_ExtractFile(z, dest)) aLocked.Add((dest, z));
			}
		}
		
		gRetry:
		//wait-retry
		for (int j = 20; --j >= 0;) {
			for (int i = aLocked.Count; --i >= 0;) {
				var v = aLocked[i];
				if (_ExtractFile(v.z, v.file)) aLocked.RemoveAt(i);
			}
			if (aLocked.Count == 0) break;
			if (j > 0) Thread.Sleep(100);
		}
		
		//try to rename loaded dlls and files opened with share-delete
		for (int i = aLocked.Count; --i >= 0;) {
			var v = aLocked[i];
			var s = $"{v.file}.~old-locked";
			if (api.MoveFileEx(v.file, s, api.MOVEFILE_REPLACE_EXISTING)) {
				if (_ExtractFile(v.z, v.file)) aLocked.RemoveAt(i);
				api.MoveFileEx(s, null, api.MOVEFILE_DELAY_UNTIL_REBOOT);
			}
		}
		
		if (aLocked.Count == 0) return;
		
		//show "Retry?" dialog
		if (!Silent) {
			string sProcesses = null;
			if (Util.GetProcessesUsingFiles(aLocked.Select(o => o.file).ToArray()) is { } a1) sProcesses =
$"""

Locked by process:
{string.Join("\n", a1)}

""";
			var se = $"""
Failed to replace files:
{string.Join("\n", aLocked.Select(o => o.z.FullName))}
{sProcesses}
Retry?
Yes - retry.
No - replace after reboot. Please don't run the app until reboot.
""";
			var mbr = App.Msgbox(se, MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (mbr == MessageBoxResult.Yes) goto gRetry;
		}
		
		//extract to a temporary file, and let OS replace the locked file after reboot
		foreach (var (dest, z) in aLocked) {
			var s = $"{dest}.~new";
			z.ExtractToFile(s, overwrite: true);
			api.MoveFileEx(s, dest, api.MOVEFILE_REPLACE_EXISTING | api.MOVEFILE_DELAY_UNTIL_REBOOT);
			DontRunApp = true;
		}
		
		static bool _ExtractFile(ZipArchiveEntry t, string file) {
			try {
				t.ExtractToFile(file, true);
				return true;
			}
			catch (IOException) {
				return false;
			}
		}
	}
	
	public bool DontRunApp { get; private set; }
	
	bool _EnsureAppDirOK() {
		if (!Directory.Exists(_dir)) {
			_log("Creating folder");
			Directory.CreateDirectory(_dir);
		} else {
			if (!_dir.Equals(Reg.GetPreviousDir()?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) {
				_log("Folder exists");
				if (!Path.GetFileName(_dir).Equals(App.AppName) && Directory.EnumerateFileSystemEntries(_dir).Any() && !File.Exists(_dirBS + "Au.Editor.dll")) {
					if (!Silent) App.Msgbox("The folder already exists and is not empty.", icon: MessageBoxImage.Error);
					return false;
				} else {
					if (!Silent) if (App.Msgbox("The folder already exists. Install in it anyway?", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return false;
				}
			}
			
			var ed = _dirBS + "Au.Editor.dll";
			if (File.Exists(ed) && FileVersionInfo.GetVersionInfo(ed) is { } v) _prevVersion = new Version(v.FileMajorPart, v.FileMinorPart, v.FileBuildPart);
		}
		return true;
	}
	
	void _DeleteOldFilesAndWriteInstalledTxt() {
		var installedTxt = _dirBS + c_uninstallListFilename;
		
		//delete files installed by prev version but not this version
		try {
			string[] aOld = [];
			
			if (File.Exists(installedTxt)) {
				aOld = File.ReadAllLines(installedTxt);
			} else if (_prevVersion < new Version(1, 16, 4)) { //delete files used only in old LA versions that used other installer
				aOld = [
					"Au.Task-x64.exe",
					"cookbook.db",
					@"Roslyn\.exeProgram",
					"Au.Task.dll",
					"Au.Task.deps.json",
					"Au.Task-arm.deps.json",
					"Au.Task.runtimeconfig.json",
					"Au.Task-arm.runtimeconfig.json",
					"unins000.exe",
					"unins000.dat",
					];
				Util.DeleteFileOrDir(folders.CommonPrograms + "LibreAutomate C#.lnk");
			}
			foreach (var relPath in aOld.Reverse()) {
				if (_hsPaths.Contains(relPath)) continue;
				if (Path.IsPathRooted(relPath) || relPath.Contains("..")) continue;
				Util.DeleteFileOrDir(_dirBS + relPath, dirIfEmpty: true);
			}
		}
		catch { }
		
		File.WriteAllLines(installedTxt, _hsPaths.ToArray());
	}
	
	void _Delete(string path) {
		if (!Util.IsFullPath(path)) path = _dirBS + path;
		Util.DeleteFileOrDir(path);
	}
	
	public bool InstallDotnet() {
		_log(_progressPrefix = "Getting the .NET download URL");
		var url = DotnetInfo.GetDownloadUrl();
		
		var filename = "~" + Path.GetFileName(url);
		var file = folders.Downloads + filename; //not temp, because sometimes FP
		
		try {
			_log(_progressPrefix = "Downloading .NET Desktop Runtime");
			try {
				using var http = Util.CreateHttpClient(true);
				http.Download(url, file, nRetry: Silent ? 5 : 1, _progress);
			}
			catch (Exception ex) {
				if (!Silent) App.Msgbox("Failed to download .NET.\nYou'll have to install it manually.", ex, MessageBoxImage.Warning);
				return false;
			}
			
			_log("Installing .NET Desktop Runtime");
			try {
				//var cl = $"/install {(Silent ? "/quiet" : "")} /norestart"; //no
				var cl = "/install /quiet /norestart";
				var process = Process.Start(new ProcessStartInfo(file, cl) { UseShellExecute = true });
				process.WaitForExit();
				if (process.ExitCode == 0) return true;
				throw new Exception("Exit code not 0");
			}
			catch (Exception ex) {
				if (!Silent) App.Msgbox("Failed to install .NET.\nYou'll have to install it manually.", ex, MessageBoxImage.Warning);
			}
			return false;
		}
		finally {
			_Delete(file);
		}
	}
	
	public static void UnloadDll() {
		//close acc agent windows
		List<nint> a = [];
		for (nint w = 0; 0 != (w = api.FindWindowEx(api.HWND_MESSAGE, w, "AuCpp_IPA_1", null));) a.Add(w);
		int n = a.Count;
		if (n > 0) {
			Parallel.ForEach(a, static v => { api.SendMessageTimeout(v, api.WM_CLOSE, 0, 0, api.SMTO_ABORTIFHUNG, 2000, out _); });
			if (Silent) Thread.Sleep(n * 50);
			a.Clear();
		}
		
		//unload from processes where loaded by the clipboard hook
		_SendMessage0(api.HWND_BROADCAST); //top-level windows
		for (nint w = 0; 0 != (w = api.FindWindowEx(api.HWND_MESSAGE, w, null, null));) a.Add(w); //message-only windows
		Parallel.ForEach(a, _SendMessage0);
		if (Silent) Thread.Sleep(500);
		
		void _SendMessage0(nint hwnd) {
			if (Silent) api.SendMessageTimeout(hwnd, 0, 0, 0, api.SMTO_ABORTIFHUNG, 1000, out _);
			else api.SendNotifyMessage(hwnd, 0, 0, 0);
		}
	}
	
	public static bool EnsureAppNotRunning() {
		if (!Mutex.TryOpenExisting("Au.Editor.Mutex.m3gVxcTJN02pDrHiQ00aSQ", out var mutex)) return true;
		try {
			var w = api.FindWindowEx(0, 0, "Au.Editor.TrayNotify", null);
			api.SendMessage(w, api.WM_CLOSE, 0, 0);
			if (mutex.WaitOne(5000)) return true;
		}
		catch (AbandonedMutexException) { return true; }
		finally { mutex.Dispose(); }
		
		if (!Silent) App.Msgbox("LibreAutomate is running.", icon: MessageBoxImage.Error);
		return false;
	}
	
	const string c_uninstallListFilename = "installed.txt"; //once it was "uninstall.list", but it triggered Microsoft FP
	
	public bool UninstallApp() {
		try {
			var installedTxt = _dirBS + c_uninstallListFilename;
			if (File.Exists(installedTxt)) {
				foreach (var relPath in File.ReadAllLines(installedTxt).Reverse()) {
					if (Path.IsPathRooted(relPath) || relPath.Contains("..")) throw new InvalidOperationException();
					Util.DeleteFileOrDir(_dirBS + relPath, dirIfEmpty: true);
				}
				
				_Delete(c_uninstallListFilename);
				_Delete("GIT");
				_Delete("SDK");
				_Delete(folders.ProgramData + App.AppName);
				_Delete(folders.CommonPrograms + App.AppName + ".lnk");
				
				Reg.Uninstall();
			} else { //just remove from the installed apps list
				Reg.Uninstall();
				return true;
			}
		}
		catch (Exception ex) {
			if (!Silent) App.Msgbox("Failed.", ex);
			return false;
		}
		
		//delete self if in _dir, and _dir if empty
		bool deleteSelf = _dirBS.Equals(folders.ThisAppBS);
		if (deleteSelf) {
			var self = Process.GetCurrentProcess().MainModule.FileName;
			var self2 = self + ".~deleting";
			if (api.MoveFileEx(self, self2, api.MOVEFILE_REPLACE_EXISTING)) {
				api.MoveFileEx(self2, null, api.MOVEFILE_DELAY_UNTIL_REBOOT);
				if (Directory.GetFileSystemEntries(_dir).Length == 1) api.MoveFileEx(_dir, null, api.MOVEFILE_DELAY_UNTIL_REBOOT);
			}
			//never mind: there are ways to delete self without reboot. But then FP are more likely.
		} else {
			_Delete("uninstall.exe");
			Util.DeleteFileOrDir(_dir, dirIfEmpty: true);
		}
		
		//this triggers Microsoft FP. Never mind, don't delete.
		//try { Process.Start(new ProcessStartInfo("schtasks.exe", $@"/delete /tn \Au\{c_appFilename} /f") { UseShellExecute = false, CreateNoWindow = true }); }
		//catch { }
		
		return true;
	}
	
	public static class Reg {
		const string c_uninsKeyBase = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
		const string c_uninsId = App.AppName;
		const string c_uninsId_old = "LibreAutomate C#_is1"; //Inno Setup
		
		static RegistryKey _OpenOrCreateUninstallKey() {
			return Registry.LocalMachine.CreateSubKey(c_uninsKeyBase + c_uninsId);
		}
		
		static RegistryKey _OpenUninstallKey(bool writable) {
			if (Registry.LocalMachine.OpenSubKey(c_uninsKeyBase + c_uninsId, writable) is { } r1) return r1;
			if (Registry.LocalMachine.OpenSubKey(c_uninsKeyBase + c_uninsId_old, writable) is { } r2) return r2;
			return null;
		}
		
		static void _DeleteKey(string parentKey, string key) {
			try {
				using var rk2 = Registry.LocalMachine.OpenSubKey(parentKey, true);
				rk2?.DeleteSubKeyTree(key, false);
			}
			catch { }
		}
		
		public static string GetPreviousDir() {
			using var rk = _OpenUninstallKey(false);
			var r = rk?.GetValue("InstallLocation", null) as string;
			if (r is null || !Util.IsFullPath(r)) return null;
			return r.TrimEnd('\\');
		}
		
		public static void Install(string dir, long extractedSize) {
			using var rk = _OpenOrCreateUninstallKey();
			rk.SetValue("InstallLocation", dir);
			rk.SetValue("DisplayIcon", dir + @"\Au.Editor.exe");
			rk.SetValue("DisplayName", App.AppName);
			rk.SetValue("DisplayVersion", App.AppVersion);
			rk.SetValue("EstimatedSize", (int)(extractedSize / 1024));
			rk.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
			rk.SetValue("NoModify", 1);
			rk.SetValue("Publisher", "Gintaras Didžgalvis");
			string uninsExe = dir + @"\uninstall.exe";
			rk.SetValue("QuietUninstallString", $@"""{uninsExe}"" /SILENT");
			rk.SetValue("UninstallString", $@"""{uninsExe}""");
			
			_DeleteKey(c_uninsKeyBase, c_uninsId_old); //old Inno Setup key
			
			Registry.SetValue(AppPathKey, null, $@"{dir}\Au.Editor.exe");
			
#if !NET
			var ver = Environment.OSVersion.Version;
			if ((ver.Major, ver.Minor) == (6, 1)) {
				//workaround: on Win7 dotnet nuget/publish fails to connect to nuget
				try {
					const string c_rk = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client";
					if (Registry.GetValue(c_rk, "DisabledByDefault", null) is null) Registry.SetValue(c_rk, "DisabledByDefault", 0);
				}
				catch { }
			}
#endif
		}
		
		public static void Uninstall() {
			_DeleteKey(c_uninsKeyBase, c_uninsId);
			_DeleteKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths", c_appFilename + ".exe");
		}
		
		public const string AppPathKey = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{c_appFilename}.exe";
	}
	
#if DEV
	const string c_appFilename = "LA.Main"; //for testing registry etc
#else
	const string c_appFilename = "Au.Editor";
#endif
}
#else
#endif
