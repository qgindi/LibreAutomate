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
	static string s_git;

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
		s_git ??= filesystem.searchPath("git.exe") ?? throw new FileNotFoundException("git.exe");
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
