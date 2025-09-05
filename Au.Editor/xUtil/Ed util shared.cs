//This file also can be used in scripts (meta c).

using System.Security.Cryptography;

/// <summary>
/// Calls <see cref="ProtectedData"/> <c>Protect</c> or <c>Unprotect</c> with LA's entropy.
/// </summary>
static class EdProtectedData {
	static byte[] _entropy = [212, 71, 168, 115, 1, 83, 144, 90];
	
	/// <returns>Protected data as Base64 string. On exception prints warning and returns <c>null</c>.</returns>
	public static string Protect(string s) {
		try { return Convert.ToBase64String(ProtectedData.Protect(s.ToUTF8(), _entropy, DataProtectionScope.CurrentUser)); }
		catch (Exception ex) { print.warning(ex); return null; }
	}
	
	/// <returns>Unprotected data as string. On exception prints warning and returns <c>null</c>.</returns>
	public static string Unprotect(string s) {
		try { return ProtectedData.Unprotect(Convert.FromBase64String(s), _entropy, DataProtectionScope.CurrentUser).ToStringUTF8(); }
		catch (Exception ex) { print.warning(ex); return null; }
	}
}

/// <summary>
/// Compresses and extracts files using the LA's installed 7za.exe.
/// </summary>
static class SevenZip {
	static string _Sevenzip => folders.ThisAppBS + @"32\7za.exe";
	
	/// <summary>
	/// Compresses a file or directory.
	/// </summary>
	/// <param name="output">7za.exe output text.</param>
	/// <param name="zipFile">The compressed file to create. If exists, deletes at first; else creates directory for it if need.</param>
	/// <param name="fileOrDir">File, directory or wildcard. Full path or path in the current directory. If directory like <c>@"dir\*"</c>, adds its contents to the root.</param>
	/// <param name="curDir">Current directory path to pass to 7za.exe.</param>
	/// <param name="type7z">Create 7z archive regardless of <i>zipFile</i> filename extension.</param>
	/// <param name="switches">Optional 7za.exe command line switches/arguments.</param>
	/// <returns>false if returned an error code.</returns>
	/// <exception cref="Exception">Exceptions of used functions. Unlikely.</exception>
	public static bool Compress(out string output, string zipFile, string fileOrDir, string curDir = null, bool type7z = false, string switches = null) {
		if (filesystem.exists(zipFile)) filesystem.delete(zipFile); else filesystem.createDirectoryFor(zipFile);
		return 0 == run.console(out output, _Sevenzip, $@"a ""{zipFile}"" ""{fileOrDir}"" {(type7z ? "-t7z " : "")}{switches}", curDir);
	}
	
	/// <summary>
	/// Compresses a multiple files/directories.
	/// </summary>
	/// <param name="output">7za.exe output text.</param>
	/// <param name="zipFile">The compressed file to create. If exists, deletes at first; else creates directory for it if need.</param>
	/// <param name="files">List of files/directories/wildcards. Full paths or paths in <i>inDir</i>. If a directory is like <c>@"dir\*"</c>, adds its contents to the root.</param>
	/// <param name="curDir">Current directory path to pass to 7za.exe. It will be the base directory of non-full-path files specified in <i>files</i>.</param>
	/// <param name="type7z">Create 7z archive regardless of <i>zipFile</i> filename extension.</param>
	/// <param name="switches">Optional 7za.exe command line switches/arguments.</param>
	/// <returns>false if returned an error code.</returns>
	/// <exception cref="Exception">Exceptions of used functions. Unlikely.</exception>
	public static bool Compress(out string output, string zipFile, IEnumerable<string> files, string curDir = null, bool type7z = false, string switches = null) {
		if (filesystem.exists(zipFile)) filesystem.delete(zipFile); else filesystem.createDirectoryFor(zipFile);
		using var tf = new TempFile();
		filesystem.saveText(tf, string.Join("\n", files));
		return 0 == run.console(out output, _Sevenzip, $@"a ""{zipFile}"" @""{tf}"" {(type7z ? "-t7z " : "")}{switches}", curDir);
	}
	
	/// <summary>
	/// Extracts a compressed file to a directory.
	/// </summary>
	/// <param name="output">7za.exe output text.</param>
	/// <param name="zipFile">The compressed file.</param>
	/// <param name="outDir">The directory where to extract. If does not exist, creates; else does not delete files before extracting.</param>
	/// <param name="switches">Optional 7za.exe command line switches/arguments. The default "-aoa" means overwrite all existing files without prompt.</param>
	/// <returns>false if returned an error code.</returns>
	public static bool Extract(out string output, string zipFile, string outDir, string switches = "-aoa") {
		return 0 == run.console(out output, _Sevenzip, $@"x ""{zipFile}"" -o""{outDir}"" {switches}");
	}
}
