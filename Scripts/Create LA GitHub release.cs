/*/ c LaCloudGithub.cs; /*/

print.clear();

bool debug = false;

if (!debug) {
	int button = dialog.show("Create new LA release on GitHub", null, "1 Build/release with GitHub Actions\nWill push new commit+tag, it triggers `publish.yml`|2 From local build\nUse only if can't use GitHub actions|0 Cancel", flags: DFlags.CommandLinks);
	switch (button) {
	case 1: CreateReleaseWithGithubActions(); break;
	case 2: CreateReleaseDirectly(); break;
	default: return;
	}
}


void CreateReleaseDirectly() {
	string repo = debug ? "qgindi/laMain" : "qgindi/LibreAutomate";
	
	string[] assets = [
		folders.Editor + "LibreAutomate.msi"
	];
	
	var grm = new LA.Shared.LaCloudGithub(repo, $"v{Au_.Version}");
	grm.CreateNewRelease($"LibreAutomate {Au_.Version}", _ReleaseNotes(), assets);
	
	string _ReleaseNotes() {
		var verXX = Version.Parse(Au_.Version).ToString(2);
		return $"""
[What's new in v{verXX}](https://github.com/qgindi/LibreAutomate/blob/master/Other/DocFX/_doc/changes/v{verXX}.md)

---

Download and run `LibreAutomate.msi`.

Or run [msiexec](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/msiexec). Examples:
- Run the installer in reduced UI mode: `msiexec /i "msi file path" /qr`
- Change the app folder: `msiexec /i "msi file path" INSTALLDIR="app folder path"`

LibreAutomate uses the .NET 10 Desktop Runtime. Will prompt to install it if missing.

Runs on Windows 10 (x64) and 11 (x64, Arm64). Also 7 and 8.1; see [installing .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows).
""";
	}
}

void CreateReleaseWithGithubActions() {
	//GitRepo g = new(@"C:\code\Test\GitHub-actions");
	GitRepo g = new(folders.Editor) { Print = true };
	
	string tag = $"v{Au_.Version}";
	//string tag = $"v1.16.7-test";
#if true
	g.git($"add -A");
	if (g.git($"commit -m \"{tag}\"", "nothing to commit, working tree clean"))
		g.git($"push origin HEAD");
	
	if (g.git($"tag {tag}", "fatal: tag '*' already exists"))
		g.git($"push origin {tag}"); //triggers the GitHub Actions workflow. It builds the solution, creates installer, and creates or restores a GitHub release with this tag.
	else
		dialog.show("The tag already exists", $"To create or restore the release, go to LA GitHub Actions and re-run the run. Or re-run the workflow; select tag {tag} in the combo box. Note: it will use the commit of the tag (it may be not the last commit).\n\nIf want just to edit the release (description, files), do it manually in the GitHub release page.", icon: DIcon.Warning);
#else
//delete tag
Git($"tag -d {tag}");
Git($"push origin --delete {tag}");
#endif
}

/// <summary>
/// Executes Git commands for the specified repository.
/// </summary>
/// <param name="repoDir">Folder containing a Git repository.</param>
/// <remarks>
/// Git must be installed. To find it, uses environment variable <c>PATH</c>. Ctor throws exception if not found.
/// <para/>
/// When connecting to GitHub first time, shows a sign-in dialog (Git Credential Manager).
/// If later shows a "select account" dialog, need to logout some users. In cmd run: <c>git credential-manager github --help</c>
/// </remarks>
class GitRepo(string repoDir) {
	static string s_git = filesystem.searchPath("git.exe") ?? throw new FileNotFoundException("git.exe");
	
	/// <summary>
	/// Print the Git command (blue text) and its output.
	/// </summary>
	public bool Print;
	
	/// <summary>
	/// Executes a Git command.
	/// </summary>
	/// <param name="args">Command line arguments for <c>git.exe</c>.</param>
	/// <param name="softError">If an output line matches this wildcard and <c>git.exe</c> returns not 0, this function returns <c>false</c> instead of throwing exception.</param>
	/// <returns><c>true</c>, unless failed with <i>softError</i>.</returns>
	/// <exception cref="Exception"><c>git.exe</c> returned not 0.</exception>
	public bool git(string args, string softError = null) => _Git(args, softError);
	
	/// Executes a Git command and returns the output lines.
	/// <param name="args">Command line arguments for <c>git.exe</c>.</param>
	/// <exception cref="Exception"><c>git.exe</c> returned not 0.</exception>
	public List<string> gitOut(string args) {
		List<string> a = [];
		_Git(args, null, a);
		return a;
	}
	
	bool _Git(string args, string softError = null, List<string> output = null) {
		if (Print) print.it($"<><c blue>git {args}<>");
		bool failed = false;
		int r = run.console(s => {
			if (Print) print.it(s);
			output?.Add(s);
			failed |= !softError.NE() && s.Like(softError);
		}, s_git, args, repoDir);
		if (r == 0) return true;
		if (failed) return false;
		throw new Exception($"git {args}");
	}
}
