/*/ c GithubReleaseManager.cs; /*/

print.clear();

const string repo = "qgindi/LibreAutomate";
//const string repo = "qgindi/laMain"; //for script testing

if (repo.Ends("LibreAutomate")) {
	if (!dialog.showOkCancel("Create new LA release?", $"GitHub repo: {repo}")) return;
}

var dir = pathname.normalize(folders.Editor + @"..\Setup\bin\Release\net48");
string[] assets = [
	$@"{dir}\LA-setup.exe",
	$@"{dir}\offline-1.zip.lzma",
	$@"{dir}\offline-2.zip.lzma"
];

var grm = new GithubReleaseManager(repo, $"v{Au_.Version}");
grm.CreateNewRelease($"LibreAutomate {Au_.Version}", _ReleaseNotes(), assets);

string _ReleaseNotes() {
	var verXX = Version.Parse(Au_.Version).ToString(2);
	return $"""
[What's new in v{verXX}](https://github.com/qgindi/LibreAutomate/blob/master/Other/DocFX/_doc/changes/v{verXX}.md)

Download `LA-setup.exe`. If you want to run setup while offline, also download the `.lzma` files (setup will use them).
""";
}
