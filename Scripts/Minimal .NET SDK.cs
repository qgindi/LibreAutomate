/// Creates minimal .NET SDK for LA NuGet and Publish features.
/// Downloads full SDK to a temp folder, and gets only folders used by these features (tested).
/// Each platform folder is less than 100 MB; compressed less than 30 MB (full SDK is 200).
/// Finally prints an Upload link (https://www.libreautomate.com/download/sdk).
/// LA on user computers will auto-download when need.
/// Also installs the minimal SDK in `folders.Editor + "SDK"`. Must be x64 computer.

/*/ nuget -\SSH.NET; c Sftp.cs; c Passwords.cs; /*/
using Renci.SshNet;

if (args is ["upload"]) { //clicked link 'Upload'
	MinimalSDK.Upload();
	return;
}

print.clear();

var m = new MinimalSDK();
m.CreateX64();
m.CreateArm64();
m.Finally();

class MinimalSDK {
	const string c_version = "9.0.101"; //edit this if need. The SDK must use the first Runtime build, eg 9.0.0 and not 9.0.1 etc. Because the SDK can't be used on computers with older Runtime.
	
	bool _arm64;
	string _dirDotnet1, _dirDotnet2;
	
	static readonly string _TempDirBS = folders.ThisAppTemp + @"net-sdk-script-data\";
	
	public void CreateX64() {
		if (osVersion.isArm64OS) throw new PlatformNotSupportedException("Must be x64");
		print.it("-- Creating x64 --");
		_arm64 = false;
		_dirDotnet1 = $"{_TempDirBS}full {c_version} x64";
		_dirDotnet2 = folders.Editor + "SDK";
		_Create();
	}
	
	public void CreateArm64() {
		print.it("-- Creating arm64 --");
		_arm64 = true;
		_dirDotnet1 = $"{_TempDirBS}full {c_version} arm64";
		_dirDotnet2 = $"{_TempDirBS}minimal {c_version} arm64";
		_Create();
	}
	
	public void Finally() {
		//var site = "https://dash.cloudflare.com/";
		var site = "https://www.libreautomate.com/download/sdk";
		print.it($"<>-- DONE --\r\n<script {script.name}.cs|upload>Upload<> the zip files to {site} and delete temp folder <link {_TempDirBS}>net-sdk-script-data<>.");
	}
	
	public static void Upload() {
		print.it("Uploading. Wait until DONE.");
		var zip1 = $"{_TempDirBS}sdk-{Environment.Version.ToString(2)}-x64.zip";
		var zip2 = $"{_TempDirBS}sdk-{Environment.Version.ToString(2)}-arm64.zip";
		Sftp.UploadToLA("domains/libreautomate.com/public_html/download/sdk", zip1, zip2);
		print.it("DONE");
		if (dialog.showYesNo("Delete temp folder?", _TempDirBS)) filesystem.delete(_TempDirBS);
	}
	
	void _Create() {
		_DownloadSdk();
		_CreateFolder();
		_CopyRootFiles();
		_Create7z();
		if (!_arm64) {
			filesystem.more.createSymbolicLink(_dirDotnet2 + @"\host", @"C:\Program Files\dotnet\host", CSLink.Junction);
			filesystem.more.createSymbolicLink(_dirDotnet2 + @"\shared", @"C:\Program Files\dotnet\shared", CSLink.Junction);
		}
	}
	
	void _DownloadSdk() {
		if (filesystem.exists(_dirDotnet1)) return;
		string zipName = $"dotnet-sdk-{c_version}-win-{(_arm64 ? "arm64" : "x64")}.zip";
		var sevenzip = folders.Editor + @"32\7za.exe";
		string zipFile = _TempDirBS + zipName;
		if (!filesystem.exists(zipFile)) {
			print.it("Downloading SDK...");
			string url = $"https://builds.dotnet.microsoft.com/dotnet/Sdk/{c_version}/{zipName}";
			internet.http.Get(url, zipFile + "~");
			filesystem.rename(zipFile + "~", zipName);
		}
		
		print.it("Extracting SDK...");
		if (0 != run.console(out var s, sevenzip, $@"x -aoa -o""{_dirDotnet1}"" ""{zipFile}""")) {
			filesystem.delete(_dirDotnet1);
			throw new AuException($"*extract. {s}");
		}
	}
	
	void _CreateFolder() {
		print.it("Creating folder");
		
		string dirSdk1 = $@"{_dirDotnet1}\sdk\{c_version}";
		string dirSdk2 = $@"{_dirDotnet2}\sdk\{c_version}";
		
		if (!filesystem.exists(dirSdk1)) throw new ArgumentException("Please edit SDK version in this script");
		
		filesystem.delete(_dirDotnet2);
		
		long size = 0;
		var a1 = filesystem.enumFiles(dirSdk1, flags: FEFlags.AllDescendants | FEFlags.NeedRelativePaths).ToArray();
		foreach (var f in a1) {
			var path = f.FullPath;
			var rel = f.Name;
			
			if (rel.Ends(".resources.dll", true)) continue;
			if (rel.Find("\\net4", true) > 0) continue;
			//if (rel.Find("FSharp", true) > 0) continue;
			
			var a = rel.Lower().Split('\\', StringSplitOptions.RemoveEmptyEntries);
			if (a.Length > 1) {
				switch (a[0]) {
				case "dotnettools" or "extensions" or "fsharp" or "testhostnetframework": continue; //not used, big
				case "apphosttemplate" or "ref" or "runtimes" or "trustedroots": break; //maybe not used, but small
				case "current" or "microsoft" or "sdkresolvers": break; //used, small
				case "containers":
					if (!(a[1] is "build")) continue;
					break;
				case "roslyn":
					if (a[^1].Ends(".visualbasic.dll")) continue;
					//tested: does not work with the 2 *CodeAnalysis* dlls replaced with those from the LA Roslyn folder. Would be 7z 26 -> 18 MB.
					break;
				case "sdks" when a.Length > 2:
					if (a[1] is "microsoft.net.sdk" or "microsoft.net.sdk.windowsdesktop" or "microsoft.net.sdk.publish") {
						if (a[^2] is "vb") continue;
					} else if (a[1] is "microsoft.net.sdk.razor" or "microsoft.net.sdk.staticwebassets" or "microsoft.net.sdk.blazorwebassembly" or "fsharp.net.sdk") {
						continue;
					} else {
						if (!(a[2] is "build" or "tools" or "targets")) continue;
					}
					break;
				default:
					print.it(rel);
					break;
				}
			}
			
			filesystem.copy(path, dirSdk2 + rel);
			
			size += f.Size;
		}
		print.it($"Folder size: {double.Round(size / (1024 * 1024d), 1)}");
	}
	
	void _CopyRootFiles() {
		//copy dotnet.exe and license txt files.
		foreach (var f in filesystem.enumFiles(_dirDotnet1)) {
			var path = f.FullPath;
			//print.it(path);
			filesystem.copyTo(path, _dirDotnet2);
		}
	}
	
	void _Create7z() {
		print.it("Compressing...");
		
		var filename = $"sdk-{Environment.Version.ToString(2)}-{(_arm64 ? "arm64" : "x64")}.zip"; //note: it's 7z, but Hostinger for 7z does not set `content-length` header (no progress); also `content-type: text/plain`
		var zipFile = _TempDirBS + filename;
		filesystem.delete(zipFile);
		
		var sevenzip = folders.Editor + @"32\7za.exe";
		if (0 != run.console(out var s, sevenzip, $@"a ""{zipFile}"" ""{_dirDotnet2}\*"" -t7z -mx=7")) throw new AuException(s);
	}
}
