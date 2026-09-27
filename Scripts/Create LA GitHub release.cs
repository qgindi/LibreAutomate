/*/ c GithubReleaseManager.cs; /*/

print.clear();

const string repo = "qgindi/LibreAutomate";
//const string repo = "qgindi/laMain"; //for script testing

if (repo.Ends("LibreAutomate")) {
	if (!dialog.showOkCancel("Create new LA release?", $"GitHub repo: {repo}")) return;
}

string[] assets = [
	folders.Editor + "LibreAutomate.msi"
];

var grm = new GithubReleaseManager(repo, $"v{Au_.Version}");
grm.CreateNewRelease($"LibreAutomate {Au_.Version}", _ReleaseNotes(), assets);

string _ReleaseNotes() {
	var verXX = Version.Parse(Au_.Version).ToString(2);
	return $"""
[What's new in v{verXX}](https://github.com/qgindi/LibreAutomate/blob/master/Other/DocFX/_doc/changes/v{verXX}.md)

Download and run `LibreAutomate.msi`.

Or run [msiexec](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/msiexec). Examples:
- Run the installer in reduced UI mode: `msiexec /i "msi file path" /qr`
- Change the app folder: `msiexec /i "msi file path" INSTALLDIR="app folder path"`

LibreAutomate uses the .NET 10 Desktop Runtime. Will prompt to install it if missing.

Runs on Windows 10 (x64) and 11 (x64, Arm64). Also Windows 7 and 8.1 (see [installing .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows)).
""";
}
