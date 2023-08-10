using Octokit;

static partial class GitSync {
	static WorkspaceSettings.git_t _sett;
	static string _gitExe, _dir, _pat, _owner, _repo;
	
	static bool _Start(bool setup = false) {
		_dir = folders.Workspace;
		_dir = @"C:\Users\G\Documents\LibreAutomate\Main";//TODO
#if SCRIPT
		_sett = WorkspaceSettings.Load(_dir + @"\settings2.json").git;
#else
		_sett = App.Model.WSSett.git;
#endif
		if (!_sett.use) return false;
		if (!_FindGit(out _gitExe, out _)) { print.warning("git.exe not found"); return false; }
		if (!_ParseURL()) return false;
		//_GitSetCredHelper(out _);
		return true;
	}
	
	static bool _FindGit(out string path, out bool isShared) {
		isShared = false;
		if (!filesystem.exists(path = _PrivateGitExePath).File) {
			path = filesystem.searchPath("git.exe") ?? filesystem.searchPath(folders.ProgramFiles + @"Git\cmd\git.exe");
			isShared = path != null;
		}
		return path != null;
	}
	
	static bool _ParseURL(string url, out string owner, out string repo) {
		if (!url.RxMatch(@"^https://github.com/([[:alnum:]\-]+)/([\w\-\.]+?)(?:\.git)?$", out var m)) { owner = repo = null; return false; }
		owner = m[1].Value;
		repo = m[2].Value;
		return true;
	}
	
	static bool _ParseURL() {
		if (_ParseURL(_sett.repoUrl, out _owner, out _repo)) return true;
		print.warning("Invalid GitHub URL: " + _sett.repoUrl);
		return false;
	}
	
	static string _PrivateGitExePath => folders.ThisAppBS + @"Git\mingw64\bin\git.exe";
	//static string _PrivateGitExePath => folders.ThisAppBS + @"Git\cmd\git.exe";
	
	static bool _GitLocal(string cl) {
		if (_gitExe == null) return false;
		int ec = run.console(out var s, _gitExe, cl, _dir);
		return ec == 0;
	}
	
	static bool _GitLocal(string cl, out string output) {
		if (_gitExe == null) { output = null; return false; }
		int ec = run.console(out output, _gitExe, cl, _dir);
		//print.it(ec, output);
		return ec == 0;
	}
	
	static string _GitLocalS(string cl) => _GitLocal(cl, out var s) ? s : null;
	
	/// <summary>
	/// Sets user/pat env vars and runs git. Use when need auth.
	/// </summary>
	/// <param name="cl">Command line without 'git'.</param>
	/// <param name="output">If not null, receives output lines.</param>
	/// <returns>false if git returned not 0.</returns>
	static bool _GitRemote(string cl, List<string> output = null) {
		output?.Clear();
		using var tev1 = new TempEnvVar(("_GIT_USER", _owner), ("_GIT_TOKEN", _Pat));
		Action<string> action = s => {
			//warnings 'password authentication was removed' are incorrect, because we use PAT. Ignore them.
			if (s.Starts("remote: Support for password authentication was removed on")) return;
			if (s.Ends(" for information on currently recommended modes of authentication.")) return;
			
			if (output != null) output.Add(s);
			const string c_genToken = " In your GitHub account's Settings -> Developer settings generate a fine-grained personal access token for this repository, with repository permission 'Content read-write'.";
			if (s.Starts("fatal: Authentication failed")) {
				print.it("The GitHub personal access token is incorrect or expired." + c_genToken);
			} else if (s == "remote: Write access to repository not granted.") {
				print.it("The GitHub personal access token is incorrect." + c_genToken);
			}
		};
		int ec = run.console(action, _gitExe, cl, _dir);
		return ec == 0;
	}
	
	/// <summary>
	/// Calls the 'List output' overload and converts output to string.
	/// </summary>
	static bool _GitRemote(string cl, out string output) {
		List<string> a = new();
		bool ok = _GitRemote(cl, a);
		output = string.Join("\r\n", a);
		return ok;
	}
	
	static string _Pat {
		get {
			if (_pat == null && _sett.pat is string s) {
				try { _pat = Convert2.AesDecryptS(s, "Ctrl+Shift+Q"); }
				catch (Exception) { }
			}
			return _pat;
		}
	}
	
	static async Task _DGitApply() {
		_ParseURL(); //will always succeed, because the dialog validates the URL format
		try {
			bool localExists = filesystem.exists(_dir + @"\.git\config").File;
			
			var gh = new GitHubClient(new ProductHeaderValue("LA")) { Credentials = new(_Pat) };
			Repository r;
			try { r = await gh.Repository.Get(_owner, _repo); }
			catch (Octokit.NotFoundException) {
				_SetupError("Repository not found.\r\n\tCommon reasons: 1. Repository does not exist. 2. Incorrect URL. 3: The token does not allow to access this repository.");
				return;
			}
			catch (Octokit.AuthorizationException) {
				_SetupError("Cannot access the repository.\r\n\tProbably the token is incorrect or expired. Generate new token and paste in the backup/sync setup dialog.");
				return;
			}
			
			if (!r.Private) {
				if (!localExists) { //prevent using a public repo accidentally. But later allow.
					_SetupError("The repository must be private.\r\n\tYou can make it public later.");
					return;
				}
				print.it("<><c orange>Note: the GitHub repository is public. Anybody in the world can see your scripts etc. You can make it private to prevent it.<>");
			}
			
			var url = _sett.repoUrl; if (!url.Ends(".git")) url += ".git";
			
			if (!localExists) {
				try {
					if ((await gh.Repository.Commit.GetAll(r.Id)).Count > 0) {
						_SetupError("The repository is not empty.\r\n\tCreate new private repository without readme etc.");
						return;
						//rejected: allow the repo to have readme etc
					}
				}
				catch (Octokit.ApiException) { } //GetAll throws ApiException if empty repo. In the future may not throw, therefore also use 'if count > 0'.
			}
			
			await Task.Run(() => {
				if (!localExists) {
					if (!_GitSetup($"init -b {r.DefaultBranch}")) return;
					if (!_GitSetup("remote add origin " + url)) return;
					if (!_GitSetup("config push.autoSetupRemote true")) return;
				} else {
					if (!_GitSetup("remote set-url origin " + url)) return;
				}
				
				const string sh1 = "!f() { echo username=$_GIT_USER; echo password=$_GIT_TOKEN; }; f";
				bool hasHelpers = _GitLocal("config --local --get-all credential.helper", out var sh2);
				if (sh2.Trim() != sh1) {
					if (hasHelpers) _GitLocal("config --unset-all credential.helper");
					if (!_GitSetup($"config credential.helper \"{sh1}\"")) return;
				}
				
				bool _GitSetup(string cl) {
					if (!_GitLocal(cl, out var so)) return true;
					print.it($"Failed: git {cl}\r\n{so}");
					return false;
				}
			});
		}
		catch (Exception e1) { _SetupError(e1.ToString()); }
		
		static void _SetupError(string text) {
			print.it($"Failed: {text}");
		}
	}
	
	//TODO
	public static void Commit() {
		if (!_Start()) return;
		if (_Script("Git_commit")) return;
		string so;
		print.it(_GitLocal("add .", out so), so);
		print.it(_GitLocal("commit -a --message=~", out so), so);
	}
	
	//TODO
	public static void Push() {
		if (!_Start()) return;
		if (_Script("Git_push")) return;
		string so;
		print.it(_GitLocal("add .", out so), so);
		print.it(_GitLocal("commit -a --message=~", out so), so);
		print.it(_GitRemote($"push origin", out so), so);
		
		//note: don't use --amend with commit+push. It's only for commit without push.
	}
	
	//TODO
	public static void Pull() {
		if (!_Start()) return;
		if (_Script("Git_pull")) return;
		string so;
		print.it(_GitRemote($"pull origin", out so), so);
	}
	
	//TODO
	public static void CustomCommand(string command) {
		if (!_Start()) return;
		_Script("command");
	}
	
	static bool _Script(string command) {
		#if SCRIPT
		return false;
		#else
		if (_sett.script.NE()) return false;
		var f = App.Model.FindCodeFile(_sett.script);
		if (f == null) { print.it("Cannot execute the Git command. Git script not found: " + _sett.script + ". Please edit menu File -> Git -> Setup -> Script."); return true; }
		using var tev1 = new TempEnvVar(("_GIT_USER", _owner), ("_GIT_TOKEN", _Pat));
		CompileRun.CompileAndRun(true, f, new string[] { command }, noDefer: true);
		#endif
		return true;
	}
}
