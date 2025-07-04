/// Creates minimal .NET SDK for LA NuGet and Publish features.
/// Need .NET SDK x64 installed, and .NET SDK arm64 in `folders.Downloads` named like "dotnet-sdk-9.0.*-win-arm64.zip".
/// From them gets only folders used by these features (tested). Each platform folder is less than 100 MB; compressed less than 30 MB (full SDK is 200).
/// Finally prints an Upload link (https://www.libreautomate.com/download/sdk).
/// LA on user computers will auto-download when need.

/*/ nuget -\SSH.NET; c Sftp.cs; c Passwords.cs; /*/
using Renci.SshNet;

const string version = "9.0.301"; //edit this

if (args is ["upload"]) { //clicked link 'Upload'
	_Upload();
	return;
}

print.clear();

bool arm64 = false;
string dirDotnet1 = $@"C:\Program Files\dotnet";
string dirDotnet2 = folders.Editor + "SDK";
_All();
arm64 = true;
_ExtractSdkArm64();
_All();
//print.it("<>DONE. Upload the 7z files to <link>https://dash.cloudflare.com/<>, to the R2 Object Storage bucket `get` (get.libreautomate.com).\r\n\tFinally delete the `net-sdk-script-data` folder.");
print.it("<>DONE. Upload the 7z files to https://www.libreautomate.com/download/sdk.\r\n\tFinally delete the `net-sdk-script-data` folder.");
print.it($"<><link {_TempDir()}>Open temp folder<>  <script {script.name}.cs|upload>Upload<>");

void _All() {
	_CreateFolder();
	_CopyRootFiles();
	_Create7z();
	_CreateJunctions();
}

void _CreateFolder() {
	print.it($"Creating folder for {(arm64 ? "arm64" : "x64")}");
	
	string dirSdk1 = dirDotnet1 + $@"\sdk\" + version;
	string dirSdk2 = dirDotnet2 + $@"\sdk\" + version;
	
	if (!filesystem.exists(dirSdk1)) throw new ArgumentException("Please edit SDK version in this script");
	
	filesystem.delete(dirDotnet2);
	
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
				if (a[^1].Like("microsoft.codeanalysis*.dll")) {
					if (a[^1].Ends(".visualbasic.dll")) continue;
					
					//rejected. 1. Possibly different version. 2. Architecture x64 or arm64 (ours AnyCPU). These are >2 times bigger than ours (contains native code?).
					//var have = folders.Editor + @"Roslyn\" + a[^1];
					//if (filesystem.exists(have)) {
					//	if (!arm64) filesystem.copy(have, dirSdk2 + rel); //_TODO: Exclude when creating 7z.
					//	continue; //7z 26 -> 18 MB
					//}
				}
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
	foreach (var f in filesystem.enumFiles(dirDotnet1)) {
		var path = f.FullPath;
		//print.it(path);
		filesystem.copyTo(path, dirDotnet2);
	}
}

void _CreateJunctions() {
	//create symlinks to .NET Runtime
	if (!arm64) {
		filesystem.more.createSymbolicLink(dirDotnet2 + @"\host", dirDotnet1 + @"\host", CSLink.Directory);
		filesystem.more.createSymbolicLink(dirDotnet2 + @"\shared", dirDotnet1 + @"\shared", CSLink.Directory);
	}
}

void _Create7z() {
	print.it("Compressing...");
	
	var filename = $"sdk-{Environment.Version.ToString(2)}-{(arm64 ? "arm64" : "x64")}.zip"; //note: it's 7z, but Hostinger for 7z does not set `content-length` header (no progress); also `content-type: text/plain`
	var zipFile = _TempDir() + filename;
	filesystem.delete(zipFile);
	
	var sevenzip = folders.Editor + @"32\7za.exe";
	if (0 != run.console(out var s, sevenzip, $@"a ""{zipFile}"" ""{dirDotnet2}\*"" -t7z -mx=7")) throw new AuException(s);
}

string _TempDir() => folders.ThisAppTemp + @"net-sdk-script-data\";

void _ExtractSdkArm64() {
	print.it("Extracting .NET SDK arm64...");
	
	var pattern = $"dotnet-sdk-{Environment.Version.ToString(2)}.*-win-arm64.zip";
	var a = Directory.GetFiles(folders.Downloads, pattern);
	if (a.Length == 0) throw new AuException($"File like `{pattern}` not found in the Downloads folder. Download .NET SDK arm64 from https://dotnet.microsoft.com/en-us/download/dotnet/{Environment.Version.ToString(2)}, or edit the pattern in this script.");
	if (a.Length > 1) throw new AuException($"Multiple files like `{pattern}` found in the Downloads folder. Delete old files.");
	
	dirDotnet1 = _TempDir() + "arm64 full";
	dirDotnet2 = _TempDir() + "arm64 minimal";
	
	var sevenzip = folders.Editor + @"32\7za.exe";
	if (0 != run.console(out var s, sevenzip, $@"x -aoa -o""{dirDotnet1}"" ""{a[0]}""")) throw new AuException($"*extract files. {s}");
}

void _Upload() {
	print.it("Uploading. Wait until DONE.");
	var zip1 = _TempDir() + $"sdk-{Environment.Version.ToString(2)}-x64.zip";
	var zip2 = _TempDir() + $"sdk-{Environment.Version.ToString(2)}-arm64.zip";
	Sftp.UploadToLA("domains/libreautomate.com/public_html/download/sdk", zip1, zip2);
	print.it("DONE");
}
