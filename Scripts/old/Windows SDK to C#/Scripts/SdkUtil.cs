static class SdkUtil {
	
	public static string GetVisualStudioToolsFolderPath(bool preview) {
		//string vswherePath = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe";
		//if (0 != run.console(out string s, vswherePath, $"-latest {(preview ? "-prerelease" : "")} -property installationPath")) throw null;
		//s = s.Trim() + @"\VC\Tools\MSVC\";
		
		var s = $@"C:\Program Files\Microsoft Visual Studio\2022\{(preview ? "Preview" : "Community")}\VC\Tools\MSVC\";
		string ver = Directory.GetDirectories(s).OrderByDescending(d => d).First();
		return ver + @"\bin\Hostx64\x64";
	}
}
