using Microsoft.Extensions.FileSystemGlobbing;

class GitBinaryFiles(string repoDir) {
	string _laDirBS = repoDir / @"_\";
	const string c_versionFileRel = @".github\bin-version";
	string _versionFile = repoDir / c_versionFileRel;
	
#if REPO2
		const string c_ghRepoName = "test-git";
#else
		const string c_ghRepoName = "LA-downloads";
#endif
	
	public void SetHooks() {
		var relPath = Path.GetRelativePath(repoDir, process.thisExePath);
		
		var preCommit = repoDir / @".git\hooks\pre-commit";
		if (!filesystem.exists(preCommit, true)) filesystem.saveText(preCommit, $"""
#!/bin/sh

"{relPath}" "gitPreCommitHook"
exit $?
""");
		
		var prePush = repoDir / @".git\hooks\pre-push";
		if (!filesystem.exists(prePush, true)) filesystem.saveText(prePush, $"""
#!/bin/sh

"{relPath}" "gitPrePushHook"
exit $?
""");
	}
	
	public int PreCommitHook() {
		//print.it("pre-commit hook");
		
		if (_GetNewVersionFileTextIfFilesChanged() is not string versionData) return 0;
		
		var g = new GitRepo(repoDir);
		g.Print = true;
		
		//exit if nothing to commit
		if (g.gitOut("status --porcelain").Count == 0) return 0;
		
		filesystem.saveText(_versionFile, versionData);
		File.SetAttributes(_versionFile, FileAttributes.Archive); //remove Hidden attr
		
		try {
			g.git($"add {c_versionFileRel}");
		}
		catch {
			filesystem.delete(_versionFile);
			return 1;
		}
		
		return 0;
	}
	
	public int PrePushHook() {
		//print.it("pre-push hook");
		
		//exit if nothing to push
		string commits = Console.In.ReadToEnd();
		if (commits.NE()) return 0;
		
		//exit if file _versionFile not updated
		var fileAttr = File.GetAttributes(_versionFile);
		if (fileAttr.Has(FileAttributes.Hidden)) return 0;
		
		//upload
		_Upload();
		
		File.SetAttributes(_versionFile, fileAttr | FileAttributes.Hidden);
		
		return 0;
	}
	
	string _GetNewVersionFileTextIfFilesChanged() {
		string list = """
*.db
*MSTSCLib.dll
toc.json
toc-ai.yml
xrefmap.yml
Roslyn\*.dll
Debugger\**
32\apphost.exe
64\apphost.exe
64\ARM\apphost.exe
""";
		Matcher matcher = new();
		matcher.AddIncludePatterns(list.Lines(noEmpty: true));
		var files = matcher.GetResultsInFullPath(_laDirBS);
		var hash = System.Security.Cryptography.SHA256.HashFiles(files);
		
		if (filesystem.exists(_versionFile)) {
			var a = File.ReadAllLines(_versionFile);
			if (a[1] == hash) return null;
		}
		
		return $"""
{DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm")}
{hash}
{string.Join("\r\n", files.Select(o => o[_laDirBS.Length..]))}
""";
	}
	
	void _Upload() {
		var v = _VersionData.Load(_versionFile);
		
		using var listFile = new TempFile();
		filesystem.saveText(listFile, string.Join('\n', v.files));
		
		var zipFile = folders.ThisAppTemp + v.date + ".7z";
		filesystem.delete(zipFile);
		
		var d = dialog.showProgress(true, "Updating LA binary files", "Compressing...");
		try {
			if (0 != run.console(out string s1, _laDirBS + @"32\7za.exe", $@"a ""{zipFile}"" @""{listFile}""", _laDirBS))
				throw new AuException(s1);
			//run.it(zipFile); dialog.show("zip OK");
			
			d.Send.ChangeText2("Uploading...", false);
			var rm = new GithubReleaseManager(c_ghRepoName, "bin");
			rm.AddOrReplaceAsset(zipFile, "application/x-compressed");
		}
		finally {
			d.Send.Close();
			filesystem.delete(zipFile, FDFlags.CanFail);
		}
	}
	
	public int Restore(bool test = false) {
		string laDirBS = _laDirBS;
		if (test) {
			laDirBS += @"test\";
			filesystem.createDirectory(laDirBS);
		}
		
		//is any file missing or possibly modified?
		var v = _VersionData.Load(_versionFile); //it's ok: exception if does not exist
		var date = v.ToDateTime();
		if (!v.files.Any(f => File.GetLastWriteTimeUtc(laDirBS + f) != date)) return 0;
		
		//is really missing/modified, of just changed the last-write-time?
		var hash = System.Security.Cryptography.SHA256.HashFiles(v.files, laDirBS);
		
		if (v.hash != hash)
			_Restore(laDirBS, v);
		
		foreach (var f in v.files) {
			File.SetLastWriteTimeUtc(laDirBS + f, date);
		}
		
		return 0;
	}
	
	void _Restore(string laDirBS, _VersionData v) {
		print.it("Getting app files that are not generated when building this solution");
		
		string zipFilename = v.date + ".7z";
		print.it($"    Downloading {zipFilename} from https://github.com/qgindi/{c_ghRepoName}/releases/bin"); //the page URL for web browsers
		var url = $"https://github.com/qgindi/{c_ghRepoName}/releases/download/bin/{zipFilename}";
		using TempFile zipFile = new(".7z");
		internet.http.Get(url, true).Download(zipFile, p => {
			//Console.Write($"\r{p.Percent}%"); //no, VS does not support overwriting the same line in the output window
		});
		
		print.it($"    Extracting {zipFilename}");
		int r = run.console(out string so, _laDirBS / @"32\7za.exe", $@"x ""{zipFile}"" -aoa", laDirBS);
		if (r != 0) throw new AuException($"Failed to extract {zipFilename}. " + so);
		
		print.it("    Done");
	}
	
	record class _VersionData(string date, string hash, string[] files) {
		public static _VersionData Load(string file) {
			var a = File.ReadAllLines(file);
			return new(a[0], a[1], a.RemoveAt(0, 2));
		}
		
		public DateTime ToDateTime() => DateTime.ParseExact(date, "yyyy-MM-dd_HH-mm", null, 0);
	}
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

static class Ext_ {
	//TODO: remove after updating the used LA NuGet package
	extension(string) {
		public static string operator /(string s1, string s2) {
			if (s2 is ['\\' or '/', ..] && s2 is not [_, '\\' or '/', ..]) s2 = s2[1..];
			return Path.Combine(s1, s2);
		}
	}
	
	extension(System.Security.Cryptography.SHA256) {
		/// <summary>
		/// Computes a SHA-256 hash of one or more files (file content).
		/// </summary>
		/// <param name="files">One or more file paths. Can be relative paths if <i>parentDirectory</i> specified.</param>
		/// <param name="parentDirectory">Parent directory of files.</param>
		/// <returns>A lowercase hex string. Returns <c>null</c> if a file does not exist.</returns>
		/// <exception cref="Exception">Failed to load an existing file or create hash.</exception>
		public static string HashFiles(IEnumerable<string> files, string parentDirectory = null) {
			using var sha = System.Security.Cryptography.SHA256.Create();
			byte[] buffer = new byte[64 * 1024];
			
			foreach (string file_ in files) {
				string file = parentDirectory is null ? file_ : parentDirectory / file_;
				if (!File.Exists(file)) return null;
				using var fs = filesystem.loadStream(file);
				for (int n; (n = fs.Read(buffer, 0, buffer.Length)) > 0;) {
					sha.TransformBlock(buffer, 0, n, null, 0);
				}
			}
			
			sha.TransformFinalBlock([], 0, 0);
			
			return Convert.ToHexStringLower(sha.Hash);
		}
	}
}
