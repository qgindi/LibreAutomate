using System.Windows.Controls;
using System.Text.Json.Nodes;

static partial class GitSync {
	static WorkspaceSettings.git_t _sett;
	static string _gitExe, _dir;
	
	//setup: 1 when opening the dialog, 2 on OK.
	static bool _Start(int setup = 0) {
		_dir = folders.Workspace;
		_dir = @"C:\Users\G\Documents\LibreAutomate\Main";//TODO
#if SCRIPT
		_sett = WorkspaceSettings.Load(_dir + @"\settings2.json").git ??= new();
#else
		if (setup == 2) {
			App.Model.WSSett.git ??= _sett;
		} else {
			_sett = App.Model.WSSett.git;
			if (_sett == null) { if (setup == 1) _sett = new(); else return false; }
		}
#endif
		if (!_sett.use) return false;
		if (!_FindGit(out _gitExe, out _)) { print.warning("git.exe not found"); return false; }
		if (!_ParseURL()) return false;
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
		if (_ParseURL(_sett.repoUrl, out _, out _)) return true;
		print.warning("Invalid GitHub URL: " + _sett.repoUrl);
		return false;
	}
	
	static string _PrivateGitExePath => folders.ThisAppBS + @"Git\mingw64\bin\git.exe";
	//static string _PrivateGitExePath => folders.ThisAppBS + @"Git\cmd\git.exe";
	
	static bool _Git(string cl, out string output) {
		if (_gitExe == null) { output = null; return false; }
		int ec = run.console(out output, _gitExe, cl, _dir);
		output = output.Trim();
		//print.it(ec, output);
		return ec == 0;
	}
	
	static string _Git(string cl) => _Git(cl, out var s) ? s : null;
	
	static JsonNode _GithubGet(string endpoint) {
		var ah = new[] { "Accept: application/vnd.github+json", "X-GitHub-Api-Version: 2022-11-28" };
		var r = internet.http.Get($"https://api.github.com/{endpoint}", headers: ah);
		return r.Json();
	}
	
	static async Task _DGitApply() {
		if (!_Start(setup: 2)) return; //may need to set _gitExe
		try {
			await Task.Run(() => {
				bool localExists = filesystem.exists(_dir + @"\.git").Directory;
				var url = _sett.repoUrl; if (!url.Ends(".git")) url += ".git";
				
				if (localExists && _Git("remote get-url origin") == url) return;
				
				//repo exists?
				if (!_Git($"ls-remote {url}", out var ls_remote_result)) {
					//rejected: try to create repo now.
					//	To get token: `git credential fill`.
					//if (ls_remote_result.Starts("remote: Repository not found")) { }
					_SetupError(ls_remote_result);
					return;
				}
				
				if (!localExists) {
					//repo empty?
					if (!ls_remote_result.Trim().NE()) {
						_SetupError("The repository is not empty.\nCreate new private repository without readme etc.");
						return;
					}//CONSIDER: allow non-empty. Maybe allow public. Or write doc how to clone eg with GitHub Desktop.
					
					//repo private?
					_ParseURL(_sett.repoUrl, out var owner, out var repo); //info: the dialog validates the URL format
					var r = _GithubGet($"users/{owner}/repos");
					if (r.AsArray().Any(v => ((string)v["name"]).Eqi(repo))) {
						_SetupError("The repository must be private.\nYou can make it public later.");
						return;
					}
					
					if (!_GitSetup("init -b main")) return;
					if (!_GitSetup("remote add origin " + url)) return;
					if (!_GitSetup("config push.autoSetupRemote true")) return;
				} else {
					if (!_GitSetup("remote set-url origin " + url)) return;
				}
				
				bool _GitSetup(string cl) {
					if (_Git(cl, out var so)) return true;
					_SetupError($"git {cl}\n{so}");
					return false;
				}
			});
		}
		catch (Exception e1) { _SetupError(e1.ToString()); }
		
		static void _SetupError(string text) {
			print.it($"Failed: {text.Trim().Replace("\n", "\n\t")}");
		}
	}
	
#if !SCRIPT
	public static void FillMenuGit(MenuItem sub) {
		sub.RemoveAllCustom();
		
		if (!_Start()) return;
		if (!_FindScript(out FileNode f)) return;
		
		if (!f.GetCurrentText(out string text)) return;
		int i = 0;
		foreach (var m in text.RxFindAll(@"(?m)^\h*const string menu_\w+\s*=\s*""(.+)""; *(?:// *(-)? *(\*\w+\.\w+ \S+)?)?")) {
			var s1 = m[1].Value;
			if (m[2].Exists) sub.InsertCustom(i++);
			var mi = sub.InsertCustom(i++, s1.Unescape(), o => _RunScript(f, o), escapeUnderscore: false);
			if (m[3].Value is string s2) try { mi.Icon = ImageUtil.LoadWpfImageElement(s2); } catch { }
		}
		sub.InsertCustom(i);
	}
	
	static bool _FindScript(out FileNode f) {
		bool useDefaultScript = _sett.script.NE();
		f = App.Model.FindCodeFile(useDefaultScript ? "Git script.cs" : _sett.script);
		if (f != null) return true;
		if (!useDefaultScript) print.warning("Git script not found: " + _sett.script + ". Please edit menu File -> Git -> Git setup -> Script.", -1);
		return false;
	}
	
	static void _RunScript(FileNode f, string command, bool defer = false) {
		App.Model.Save.AllNowIfNeed();
		_CheckIgnored();
		CompileRun.CompileAndRun(true, f, new string[] { command, _gitExe, _dir, _sett.repoUrl, _sett.message }, ifRunning: defer ? Au.Compiler.MCIfRunning.wait : null);
	}
	
	public static void Test() {
		if (!_Start()) return;
		if (!_FindScript(out FileNode f)) return;
		_RunScript(f, "Test", defer: true);
	}
	
	static void _CheckIgnored() {
		bool i1 = false, i2 = false, i3 = false;
		if (_Git(@"check-ignore .compiled/* .temp/* .nuget/*/*.csproj", out var go)) {
			i1 = go.Contains(".comp");
			i2 = go.Contains(".temp");
			i3 = go.Contains(".nuge");
		}
		if (!i1 || !i2 || i3) {
			string file = _dir + "\\.gitignore", s;
			if (filesystem.exists(file)) {
				s = filesystem.loadText(file) + "\r\n";
				if (!i1) s += "/./compiled\r\n";
				if (!i2) s += "/.temp/\r\n";
				if (i3) s += "!/.nuget/*/*.csproj\r\n";
			} else {
				s = c_defaultGitignore;
			}
			filesystem.saveText(file, s);
		}
	}
#endif
}
