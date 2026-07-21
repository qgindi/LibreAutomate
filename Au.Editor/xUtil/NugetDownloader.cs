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
#endif
}
