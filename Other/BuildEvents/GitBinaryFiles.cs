using Microsoft.Extensions.FileSystemGlobbing;

class GitBinaryFiles(string repoDir) {
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
	
	_Group[] _Groups => [
		/*
Format-versioned, large, rarely changes. The newest version can be used by older LA versions if the same package format (same filenames, same DB tables, etc).
File like `unversioned-1.7z`. The 1 is package format version.
		*/
		new("unversioned", "1", """
icons.db
winapi.db
*MSTSCLib.dll
"""),
		/*
Format-versioned, small, frequently changes. The newest version can be used by older LA versions if the same package format (same filenames, same DB tables, etc).
File like `doc-1.7z`. The 1 is package format version.
		*/
		new("doc", "1", """
doc-ai.db
doc-html.db
toc-ai.yml
toc.json
xrefmap.yml
"""),
		/*
One 7z file for each .NET major version.
File like `net-10.7z`.
		*/
		new("net", Environment.Version.ToString(2), """
doc.db
ref.db
32\apphost.exe
64\apphost.exe
64\ARM\apphost.exe
"""),
		/*
Upload new 7z file whenever the files change. Normally 1-3 times/year.
File like `fork-2026-10-15.7z`
		*/
		new("fork", null, """
Debugger\**
Roslyn\*.dll
"""),
		];
	
	public int PreCommitHook() {
		//print.it("pre-commit hook");
		
		var g = new GitRepo(repoDir) { Print = true };
		
		//exit if nothing to commit
		if (g.gitOut("status --porcelain").Count == 0) return 0;
		
		foreach (var group in _Groups) {
			group.Init(repoDir);
			
			if (group.GetGroupInfoIfFilesChanged() is not string versionData) continue;
			
			filesystem.saveText(group.GroupInfoFile, versionData);
			File.SetAttributes(group.GroupInfoFile, FileAttributes.Archive); //remove Hidden attr
			
			try {
				g.git($"add {group.GroupInfoFileRel}");
			}
			catch {
				filesystem.delete(group.GroupInfoFile);
				return 1;
			}
		}
		
		return 0;
	}
	
	public int PrePushHook() {
		//print.it("pre-push hook");
		
		//exit if nothing to push
		string commits = Console.In.ReadToEnd();
		if (commits.NE()) return 0;
		
		foreach (var group in _Groups) {
			group.Init(repoDir);
			
			//is the group info file updated?
			var fileAttr = File.GetAttributes(group.GroupInfoFile);
			if (fileAttr.Has(FileAttributes.Hidden)) continue;
			
			group.Upload();
			
			File.SetAttributes(group.GroupInfoFile, fileAttr | FileAttributes.Hidden);
		}
		
		return 0;
	}
	
	public int Restore(bool test = false) {
		string laDirBS = repoDir / @"_\";
		if (test) {
			laDirBS += @"test\";
			filesystem.createDirectory(laDirBS);
		}
		
		bool once = false;
		foreach (var group in _Groups) {
			group.Init(repoDir);
			
			//is any file missing or possibly modified?
			var v = _GroupInfo.Load(group.GroupInfoFile);
			var date = v.GetDateTime();
			if (!v.files.Any(f => File.GetLastWriteTimeUtc(laDirBS + f) != date)) continue;
			
			//is really missing/modified, of just changed the last-write-time?
			var hash = System.Security.Cryptography.SHA256.HashFiles(v.files, laDirBS);
			
			if (v.hash != hash) {
				if (!once) {
					once = true;
					print.it("Getting app files that are not generated when building this solution");
				}
				group.Restore(laDirBS, v);
			}
			
			foreach (var f in v.files) {
				File.SetLastWriteTimeUtc(laDirBS + f, date);
			}
		}
		
		if (once) print.it("    Done");
		
		return 0;
	}
	
	record class _GroupInfo(string date, string hash, string[] files) {
		public static _GroupInfo Load(string file) {
			var a = File.ReadAllLines(file);
			return new(a[0], a[1], a.RemoveAt(0, 2));
		}
		
		public DateTime GetDateTime() => DateTime.ParseExact(date, "yyyy-MM-dd", null, 0);
	}
	
	record class _Group(string groupName, string packageVersion, string filePatterns) {
		string _laDirBS;
		
		public void Init(string repoDir) {
			_laDirBS = repoDir / @"_\";
			GroupInfoFileRel = $@".github\restore\{groupName}";
			GroupInfoFile = repoDir / GroupInfoFileRel;
		}
		
		public string GroupInfoFileRel { get; private set; }
		
		public string GroupInfoFile { get; private set; }
		
		public string GetGroupInfoIfFilesChanged() {
			Matcher matcher = new();
			matcher.AddIncludePatterns(filePatterns.Lines(noEmpty: true));
			var files = matcher.GetResultsInFullPath(_laDirBS);
			var hash = System.Security.Cryptography.SHA256.HashFiles(files);
			
			if (filesystem.exists(GroupInfoFile)) {
				var a = File.ReadAllLines(GroupInfoFile);
				if (a[1] == hash) return null;
			}
			
			return $"""
{DateTime.UtcNow.ToString("yyyy-MM-dd")}
{hash}
{string.Join("\r\n", files.Select(o => o[_laDirBS.Length..]))}
""";
		}
		
		public void Upload() {
			var v = _GroupInfo.Load(GroupInfoFile);
			
			using var listFile = new TempFile();
			filesystem.saveText(listFile, string.Join('\n', v.files));
			
			string zipFilename = _ZipFilename(v), zipFile = folders.ThisAppTemp + zipFilename;
			filesystem.delete(zipFile);
			
			var d = dialog.showProgress(true, "Updating LA binary files", zipFilename, footer: "Compressing...");
			try {
				if (0 != run.console(out string s1, _laDirBS + @"32\7za.exe", $@"a ""{zipFile}"" @""{listFile}""", _laDirBS))
					throw new AuException(s1);
				//run.it(zipFile); dialog.show("zip OK");
				
				d.Send.ChangeFooterText("Uploading...", false);
				var rm = new GithubReleaseManager(c_ghRepoName, "bin");
				rm.AddOrReplaceAsset(zipFile, "application/x-compressed");
			}
			finally {
				d.Send.Close();
				filesystem.delete(zipFile, FDFlags.CanFail);
			}
		}
		
		string _ZipFilename(_GroupInfo v) => $"{groupName}-{packageVersion ?? v.date}.7z";
		
		public void Restore(string laDirBS, _GroupInfo v) {
			string zipFilename = _ZipFilename(v);
			print.it($"    Downloading {zipFilename} from https://github.com/qgindi/{c_ghRepoName}/releases/bin"); //the page URL for web browsers
			var url = $"https://github.com/qgindi/{c_ghRepoName}/releases/download/bin/{zipFilename}";
			using TempFile zipFile = new(".7z");
			internet.http.Get(url, true).Download(zipFile, p => {
				//Console.Write($"\r{p.Percent}%"); //no, VS does not support overwriting the same line in the output window
			});
			
			print.it($"    Extracting {zipFilename}");
			int r = run.console(out string so, _laDirBS / @"32\7za.exe", $@"x ""{zipFile}"" -aoa", laDirBS);
			if (r != 0) throw new AuException($"Failed to extract {zipFilename}. " + so);
		}
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
