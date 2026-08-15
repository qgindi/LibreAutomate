#if !EMPTY
using System.ComponentModel;
using System.Net.Http;

static class DotnetInfo {
	const string c_versionXX = Hardcoded.DotnetVersionXX;
	const string c_versionXXX = Hardcoded.DotnetVersionXXX;
	
	public static string VersionXX => c_versionXX;
	
	public static bool IsInstalled() {
		try {
			if (Util.RunConsole(out string s, "dotnet.exe", "--list-runtimes")) {
				if (s.Split('\n').Any(o => o.StartsWith("Microsoft.WindowsDesktop.App " + c_versionXX))) return true;
			}
		}
		catch (Win32Exception) { }
		
		//make sure there is no runtime in standard location. Better let the user manually install it than we reinstall it.
		var di = new DirectoryInfo(folders.ProgramFiles + @"dotnet\shared\Microsoft.WindowsDesktop.App");
		if (di.Exists) {
			return di.EnumerateDirectories(c_versionXX + ".*").Any();
		}
		
		return false;
	}
	
	public static string GetDownloadUrl() {
		//using var p1 = perf.local();
		string rid = Util.IsArm64 ? "arm64" : "x64";
		string urlKnown = $"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/{c_versionXXX}/windowsdesktop-runtime-{c_versionXXX}-win-{rid}.exe";
		
		using var http = Util.CreateHttpClient(false);
		
		try { //fast but undocumented way
			var s = http.GetStringAsync($"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/{c_versionXX}/latest.version").GetAwaiter().GetResult().Trim();
			if (s.StartsWith(c_versionXX)) return $"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/{s}/windowsdesktop-runtime-{s}-win-{rid}.exe";
		}
		catch (OperationCanceledException) { return urlKnown; } //timeout
		catch { }
		//p1.Next();
		
		try { //try URLs of build versions from the hardcoded to the latest+1
			string urlLast = null;
			int known = int.Parse(c_versionXXX[(c_versionXXX.LastIndexOf('.') + 1)..]);
			for (int i = known; i < 100; i++) {
				var url = $"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/{c_versionXX}.{i}/windowsdesktop-runtime-{c_versionXX}.{i}-win-{rid}.exe";
				var rm = new HttpRequestMessage(HttpMethod.Head, url);
				var r = http.SendAsync(rm, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
				if (!r.IsSuccessStatusCode) break;
				urlLast = url;
				//if (i == 0) p1.Next();
			}
			return urlLast;
		}
		catch { return urlKnown; }
	}
}
#endif
