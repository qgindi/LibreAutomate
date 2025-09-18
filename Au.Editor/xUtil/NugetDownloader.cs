using System.IO.Compression;
using System.Xml.Linq;

namespace LA;

static class NugetDownloader {
#if USED
	/// <summary>
	/// Downloads a NuGet package to a temp directory.
	/// </summary>
	/// <param name="package">Package name.</param>
	/// <param name="version">Package version. If null, gets the newest version compatible with the .NET Runtime used by this process.</param>
	/// <returns>Path of the downloaded nupkg file.</returns>
	/// <exception cref="OperationCanceledException">User-canceled.</exception>
	/// <exception cref="Exception">Failed.</exception>
	/// <remarks>
	/// If the package already exists in the temp directory, does not download again (just returns its path).
	/// </remarks>
	public static string Download(string package, string version = null) {
		package = package.Lower();
		version ??= _GetLatestCompatibleVersion(package);
		version = version.Lower();
		string fileName = $"{package}.{version}.nupkg";
		string tempDir = folders.ThisAppTemp + "download";
		string filePath = tempDir + "\\" + fileName;
		
		bool exists = false;
		if (filesystem.exists(filePath)) {
			try {
				using (ZipFile.OpenRead(filePath)) { }
				exists = true;
			}
			catch (InvalidDataException) { filesystem.delete(filePath); } //probably partially downloaded
		}
		
		if (!exists) {
			//delete old
			try {
				foreach (var v in filesystem.enumFiles(tempDir, $"{package}.*.nupkg")) {
					filesystem.delete(v.FullPath, FDFlags.CanFail);
				}
			}
			catch { } //eg the dir still does not exist
			
			string nupkgUrl = $"https://api.nuget.org/v3-flatcontainer/{package}/{version}/{fileName}";
			print.it($"Downloading from NuGet: {fileName}");
			bool ok = false;
			try {
				if (!internet.http.Get(nupkgUrl, true).Download(filePath)) throw new OperationCanceledException();
				ok = true;
			}
			finally { if (!ok) filesystem.delete(filePath); } //if failed, delete partially downloaded file
		}
		
		return filePath;
	}
	
	/// <summary>
	/// Extracts a nupkg file.
	/// </summary>
	/// <param name="nupkgPath">Path of a nupkg file.</param>
	/// <param name="extractDir">Base directory to extract files to. Creates if does not exist; else overwrites existing same files and does not delete other files.</param>
	/// <param name="rxPath">If not null, extracts only files whose relative path matches this regex. Final path will be the matched part; eg can be used <c>\K</c> to remove part of path (flatten).</param>
	/// <exception cref="Exception">Failed.</exception>
	public static void Extract(string nupkgPath, string extractDir, regexp rxPath = null) {
		print.it($"Extracting NuGet package: {pathname.getName(nupkgPath)}");
		filesystem.createDirectory(extractDir);
		using var z = ZipFile.OpenRead(nupkgPath);
		foreach (var e in z.Entries) {
			var relPath = e.FullName;
			if (rxPath != null && !rxPath.Match(relPath, 0, out relPath)) continue;
			var s = extractDir + "\\" + relPath;
			if (relPath.Contains('/')) filesystem.createDirectoryFor(s);
			e.ExtractToFile(s, overwrite: true);
		}
	}
	
	//This was used before the normal download URLs were known. Now used DotnetUtil.DownloadNetRuntimesForOtherArch.
	///// <summary>
	///// Downloads and extracts both .NET runtimes (core and desktop) for CPU architecture x64/ARM64 other than of this process.
	///// </summary>
	///// <param name="dir">Extract both to this directory.</param>
	///// <param name="portable">Extract to <i>dir</i> subdirectory <c>"dotnet"</c> (if x64) or <c>"dotnetARM"</c> (if ARM64). Delete old subdirectory.</param>
	///// <exception cref="OperationCanceledException">User-canceled.</exception>
	///// <exception cref="Exception">Failed.</exception>
	//public static void DownloadNetRuntimesForOtherArch(string dir, bool portable) {
	//	bool forArm = !osVersion.isArm64Process;
	
	//	if (portable) {
	//		dir = dir + "\\dotnet" + (forArm ? "ARM" : null);
	//		filesystem.delete(dir);
	//		filesystem.createDirectory(dir);
	//	}
	
	//	string arch = forArm ? "arm64" : "x64";
	//	string version = Environment.Version.ToString();
	//	regexp rx = new(@"(?i)^runtimes/win-\w+/(?:lib/net[^/]+|native)/\K.+");
	//	var f1 = Download($"Microsoft.NETCore.App.Runtime.win-{arch}", version);
	//	var f2 = Download($"Microsoft.WindowsDesktop.App.Runtime.win-{arch}", version);
	//	Extract(f1, dir, rx);
	//	Extract(f2, dir, rx);
	//}
	
	static string _GetLatestCompatibleVersion(string package) {
		var runtimeVersion = Environment.Version.ToString(2);
		package = package.Lower();
		
		var j = internet.http.Get($"https://api.nuget.org/v3-flatcontainer/{package}/index.json").Json();
		var versions = j["versions"].AsArray().Select(v => v.ToString()).Reverse();
		
		foreach (var v in versions) {
			//print.it(v);
			string nuspecUrl = $"https://api.nuget.org/v3-flatcontainer/{package}/{v}/{package}.nuspec";
			var nuspec = XElement.Parse(internet.http.Get(nuspecUrl).Text());
			//print.it(nuspec);
			var ns = nuspec.GetDefaultNamespace();
			var deps = nuspec.Element(ns + "metadata").Element(ns + "dependencies");
			if (deps == null) return v;
			foreach (var g in deps.Elements(ns + "group")) {
				var s = g.Attr("targetFramework");
				//print.it(s);
				if (s.Starts("net", true)) {
					if (s.RxMatch(@"^(?i)net(\d+\.\d+)(?=-windows|$)", 1, out s))
						if (s.CompareTo(runtimeVersion) <= 0) return v;
				} else {
					if (s.Starts(".NETStandard", true) || s.Starts(".NETCoreApp", true)) return v;
				}
			}
		}
		return null;
	}
#endif
}
