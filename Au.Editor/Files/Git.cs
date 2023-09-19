using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Au.Controls;
using System.Windows.Documents;
using System.Text.Json.Nodes;
using System.Text.Json;

static class Git {
	static string _dir, _gitExe;
	static WorkspaceSettings.User _sett;
	
	static bool _Start(bool setup = false) {
		_dir = folders.Workspace;
#if SCRIPT
		_sett = WorkspaceSettings.Load(_dir + @"\settings2.json").user;
#else
		_sett = App.Model.WSSett.CurrentUser;
#endif
		if (!setup) if (!filesystem.exists(_dir + @"\.git").Directory) return false;
		if (!_FindGit(out _gitExe, out _)) return false;
		return true;
	}
	
	static bool _FindGit(out string path, out bool isPrivate) {
		if (isPrivate = filesystem.exists(path = _PrivateGitExePath)) return true;
		return null != (path = filesystem.searchPath("git.exe") ?? filesystem.searchPath(folders.ProgramFiles + @"Git\cmd\git.exe"));
	}
	
	static string _PrivateGitExePath => folders.ThisAppBS + @"Git\mingw64\bin\git.exe";
	//static string _PrivateGitExePath => folders.ThisAppBS + @"Git\cmd\git.exe";
	
	static bool _ParseURL(string url, out string owner, out string repo) {
		if (!url.RxMatch(@"^https://github.com/([[:alnum:]\-]+)/([\w\-\.]+?)(?:\.git)?$", out var m)) { owner = repo = null; return false; }
		owner = m[1].Value;
		repo = m[2].Value;
		return true;
	}
	
	static string _GetURL() {
		if (_gitExe != null && _Git("config --get remote.origin.url").NullIfEmpty_() is string s) {
			if (s.Ends(".git")) s = s[..^4];
			return s;
		}
		return _sett.gitUrl;
	}
	
	static bool _Git(string cl, out string output) {
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
	
	static async Task<bool> _DGitApply(wnd wOwner) {
		_Start(setup: true); //may need to set _gitExe
		try {
			return await Task.Run(() => {
				bool localExists = filesystem.exists(_dir + @"\.git").Directory;
				var url = _sett.gitUrl;
				if (!url.Ends(".git")) url += ".git";
				
				if (localExists && _Git("remote get-url origin") == url) return true;
				
				//repo exists?
				if (!_Git($"ls-remote {url}", out var ls_remote_result)) {
					//rejected: try to create repo now.
					//	To get token: `git credential fill`.
					//if (ls_remote_result.Starts("remote: Repository not found")) { }
					_SetupError(ls_remote_result);
					return false;
				}
				
				if (localExists) {
					if (!_GitSetup("remote set-url origin " + url)) return false;
				} else {
					//repo empty?
					if (ls_remote_result.Trim().NE()) { //repo empty
						//repo private?
						_ParseURL(url, out var owner, out var repo); //info: the dialog validates the URL format
						var r = _GithubGet($"users/{owner}/repos");
						if (r.AsArray().Any(v => ((string)v["name"]).Eqi(repo))) {
							_SetupError("The repository must be private.\nYou can make it public later.");
							return false;
						}
						
						if (!_GitSetup("init -b main")) return false;
						if (!_GitSetup("remote add origin " + url)) return false;
						if (!_GitSetup("config push.autoSetupRemote true")) return false;
						_Config();
					} else { //repo not empty
						var s1 = "The GitHub repository (remote) is not empty.\nYou can cancel now, and create new private empty repository (without readme etc).\nOr you may want to clone it (download and use).";
						int dr = dialog.show(null, s1, "10 Cancel|1 Clone remote repository to new workspace\nCreates new workspace folder, downloads the remote repository into it and extracts files. Does not modify current workspace.|2 Clone remote repository to this workspace\nDownloads the remote repository to current workspace. It adds folder .git, but does not modify workspace files. Later you can sync in either direction.", DFlags.CommandLinks, icon: DIcon.Warning, owner: wOwner);
						if (dr is not (1 or 2)) return false;
						var tempDir = _dir + @"\.temp";
						filesystem.createDirectory(tempDir);
						using var tf = new TempFile(directory: tempDir);
						if (!_Git($"clone \"{url}\" \"{tf}\"", out var so)) { _SetupError(so); return false; }
						if (!FilesModel.IsWorkspaceDirectoryOrZip(tf, out _)) { _SetupError("The repository isn't a workspace"); return false; }
						if (dr == 1) { //full clone to new workspace
							var dir2 = pathname.makeUnique(_dir, isDirectory: true);
							filesystem.move(tf, dir2);
							Panels.Output.Scintilla.AaTags.AddLinkTag("+openWorkspace", s => FilesModel.LoadWorkspace(dir2));
							print.it($"<>The GitHub repository has been cloned to <link {dir2}>new workspace folder<>. Now you can <+openWorkspace {dir2}>open the new workspace<>.");
							var dir3 = _dir; _dir = dir2;
							try { _Config(); }
							finally { _dir = dir3; }
						} else { //clone only .git to this workspace
							filesystem.move(tf + @"\.git", _dir + @"\.git");
							_Config();
						}
					}
					
					void _Config() {
						_GitSetup("config core.autocrlf false");
						_GitSetup("config core.quotepath off");
					}
				}
				return true;
				
				bool _GitSetup(string cl) {
					if (_Git(cl, out var so)) return true;
					_SetupError($"git {cl}\n{so}");
					return false;
				}
			});
		}
		catch (Exception e1) { _SetupError(e1.ToString()); return false; }
		
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
		var b = new StringBuilder("[\n");
		foreach (var m in text.RxFindAll(@"(?m)^\h*case ("".+?""):.*//\{(.*)\}")) b.Append($$"""{"text": {{m[1].Value}}, {{m[2].Value}}},{{'\n'}}""");
		try {
			foreach (var v in JsonSerializer.Deserialize<_MenuItem[]>(b.Append(']').ToString(), new JsonSerializerOptions() { AllowTrailingCommas = true })) {
				if ((v.separator & 1) != 0) sub.InsertCustom(i++);
				var mi = sub.InsertCustom(i++, v.text, o => _RunScript(f, o), escapeUnderscore: false);
				if (!v.icon.NE()) try { mi.Icon = ImageUtil.LoadWpfImageElement(v.icon); } catch { }
				if (!v.tooltip.NE()) mi.ToolTip = v.tooltip;
				if ((v.separator & 2) != 0) sub.InsertCustom(i++);
			}
		}
		catch (Exception e1) { print.it(e1); }
	}
	
	record _MenuItem(string text, string icon, string tooltip, int separator);
	
	static bool _FindScript(out FileNode f) {
		bool useDefaultScript = _sett.gitScript.NE();
		f = App.Model.FindCodeFile(useDefaultScript ? "Git script.cs" : _sett.gitScript);
		if (f != null) return true;
		if (!useDefaultScript) print.warning("Git script not found: '" + _sett.gitScript + "'. In dialog 'Git setup' please edit or clear 'Script'.", -1);
		return false;
	}
	
	static void _RunScript(FileNode f, string command, bool defer = false) {
		App.Model.Save.AllNowIfNeed();
		_CheckIgnored();
		CompileRun.CompileAndRun(true, f, new string[] { command, _gitExe, _dir }, ifRunning: defer ? Au.Compiler.MCIfRunning.wait : null);
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
			string file = _dir + @"\.gitignore", s;
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
	
	public class DGit : KDialogWindow {
		public static void AaShow() {
			ShowSingle(() => new DGit());
		}
		
		public DGit() {
			_Start(setup: true);
#if SCRIPT
			Title = "Git setup";
#else
			InitWinProp("Git setup", App.Wmain);
#endif
			
			var b = new wpfBuilder(this).WinSize().Columns(0, -1, 0);
			var panel = b.Panel;
			
			var url = _GetURL();
			TextBox tUrl = new() { Text = url };
			
			WBLink lHelp = new("Help", _ => HelpUtil.AuHelp("editor/git")),
				lFolder = new("Workspace folder", _dir),
				lIgnore = new("File .gitignore", _ => _GitignoreFile()),
				lSizes = new("Print folder sizes", _ => Task.Run(_PrintFolderSizes)),
				lGithub = new("GitHub", _ => run.itSafe(tUrl.TextOrNull() ?? "https://github.com"));
			b.R.Add<TextBlock>().Text(lHelp, "    ", lFolder, "    ", lIgnore, "    ", lSizes, "    ", lGithub);
			
			b.R.Add<AdornerDecorator>("URL", out _).Add(tUrl, flags: WBAdd.ChildOfLast).Watermark("https://github.com/owner/name")
				.Validation(o => !_ParseURL(tUrl.Text, out _, out _) ? (tUrl.Text.NE() ? "URL empty" : "URL must be like https://github.com/owner/name") : null);
			
			CancellationTokenSource cts = null;
			Button bGitInstall = null;
			b.R.Add(out Label tGitStatus).Span(2).AddButton(out bGitInstall, "", _InstallGit)
				.Validation(o => !_FindGit(out _, out _) ? "Git not installed" : null);
			_SetGitControlText();
			b.Window.Closed += (_, _) => { cts?.Cancel(); };
			
			b.R.Add("Script", out TextBox tScript, _sett.gitScript);
#if !SCRIPT
			b.Validation(o => tScript.Text is var s1 && !s1.NE() && null == App.Model.FindCodeFile(s1) ? "Script not found" : null);
#endif
			
			b.R.AddOkCancel();
			b.End();
			
			b.OkApply += async e => {
				_sett.gitUrl = tUrl.Text;
				_sett.gitScript = tScript.TextOrNull();
				_GitignoreApply();
				_GitattributesApply();
#if !SCRIPT
				if (_sett.gitScript.NE()) App.Model.AddMissingDefaultFiles(git: true);
#endif
				e.Cancel = true;
				var w = b.Window.Hwnd();
				w.Enable(false);
				if (await _DGitApply(w)) b.Window.Close();
				else w.Enable(true);
			};
			
			async void _InstallGit(WBButtonClickArgs k) {
				k.Button.IsEnabled = false;
				tGitStatus.Content = "Downloading";
				cts = new();
				await _InstallGit2();
				cts = null;
				_SetGitControlText();
				k.Button.IsEnabled = true;
			}
			
			async Task<bool> _InstallGit2() {
				try {
					//can create files in LA dir?
					var zip = folders.ThisAppBS + "mingit.zip";
					using (File.Create(zip, 0, FileOptions.DeleteOnClose)) { }
					
					//get URL of the latest mingit zip
					var r = _GithubGet($"repos/git-for-windows/git/releases/latest");
					var e1 = r["assets"].AsArray().Where(o => ((string)o["name"]).Like("MinGit-*-64-bit.zip", true));
					r = e1.FirstOrDefault(o => ((string)o["name"]).Contains("-busybox-")) ?? e1.First(); //get the smallest, which is busybox, or any if busybox removed
					var url = (string)r["browser_download_url"];
					
					//download and unzip
					try {
						var rm = internet.http.Get(url, dontWait: true);
						if (!await rm.DownloadAsync(zip, p => { tGitStatus.Content = $"Downloading, {p.Percent}%"; }, cts.Token).ConfigureAwait(false)) return false;
						var gitDir = folders.ThisAppBS + "Git";
						filesystem.delete(gitDir);
						System.IO.Compression.ZipFile.ExtractToDirectory(zip, gitDir, overwriteFiles: true);
					}
					finally {
						try { filesystem.delete(zip); } catch { }
					}
				}
				catch (Exception e1) {
					print.it(e1.Message);
					if (e1 is UnauthorizedAccessException && !uacInfo.isAdmin) print.it("\tRestart this program as administrator.");
					return false;
				}
				return true;
			}
			
			void _SetGitControlText() {
				bool found = _FindGit(out _, out bool isPrivate);
				tGitStatus.Content = !found ? "Git not installed" : isPrivate ? "Private Git installed and will be used" : "Shared Git found and will be used";
				bGitInstall.Content = isPrivate ? "Update private Git" : "Install private Git";
			}
			
			void _GitignoreFile() {
				var file = _dir + "\\.gitignore";
				string s1;
				if (filesystem.exists(file)) {
					run.selectInExplorer(file);
					s1 = "exists and you can edit it. Else this dialog would";
				} else {
					s1 = "still does not exist. This dialog will";
				}
				print.it($"<><lc #C0E0A0>File <b>.gitignore<> {s1} create it and write this text. The 'Include...' checkbox deletes some lines.<>");
				print.it(c_defaultGitignore);
			}
			
			void _GitignoreApply() {
				var file = _dir + "\\.gitignore";
				if (!filesystem.exists(file)) filesystem.saveText(file, c_defaultGitignore);
			}
			
			void _GitattributesApply() {
				var file = _dir + "\\.gitattributes";
				if (!filesystem.exists(file)) filesystem.saveText(file, """
* -text
/.state/* binary

""");
			}
			
			static void _PrintFolderSizes() {
				FileTree.PrintSizes(_dir, true, .1, dirFilter: o => !(o.Level is 0 && o.Name.Lower() is ".git" or ".compiled" or ".temp"));
			}
		}
		
#if !SCRIPT
		protected override void OnSourceInitialized(EventArgs e) {
			App.Model.UnloadingThisWorkspace += Close;
			base.OnSourceInitialized(e);
		}
		
		protected override void OnClosed(EventArgs e) {
			App.Model.UnloadingThisWorkspace -= Close;
			base.OnClosed(e);
		}
#endif
	}
	
	const string c_defaultGitignore = """
/.nuget/*/*
/.interop/
/exe/
/dll/

# Don't change these defaults
!/.nuget/*/*.csproj
/.compiled/
/.temp/

""";
}
