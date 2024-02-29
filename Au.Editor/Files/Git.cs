using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Text.Json.Nodes;
using Au.Controls;

static partial class Git {
	static string _gitExe, //git.exe path
		_dir, //workspace dir
		_go; //git output. If failed, it is "[exitcode] output".
	static bool _isPrivateGit;
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
	
	static bool _Start(bool setup = false) {
		if (_gitExe == null || setup || _dir != App.Model.WorkspaceDirectory) {
			_dir = App.Model.WorkspaceDirectory;
			if (!setup) if (!filesystem.exists(_dir + @"\.git").Directory) return false;
			if (!_FindGit(out _gitExe, out _isPrivateGit)) return false;
		}
		return true;
	}
	
	static bool _FindGit(out string path, out bool isPrivate) {
		if (isPrivate = filesystem.exists(path = folders.ThisAppBS + @"Git\cmd\git.exe")) return true; //or folders.ThisAppBS + @"Git\mingw64\bin\git.exe"
		return null != (path = filesystem.searchPath("git.exe") ?? filesystem.searchPath(folders.ProgramFiles + @"Git\cmd\git.exe"));
	}
	
	#region commands
	
	static bool _InitCommand() {
		bool ok = _Start();
		if (ok) {
			if (!_EnsureMain()) return false;
			App.Model.Save.AllNowIfNeed();
			_CheckIgnored();
		} else if (t_autoBackup) {
			print.it("Auto-backup error. To fix it use menu File -> Git -> Git setup.");
		} else {
			Setup();
		}
		return ok;
		
		static bool _EnsureMain() {
			if (!gits("branch --show-current")) return false;
			if (_go != "main") {
				if (t_autoBackup) { print.it("Auto-backup error: not main branch."); return false; }
				if (!dialog.showOkCancel("Switch to main branch?", $"The Git command can work only with the main branch.\nCurrent branch is '{_go}'.", owner: App.Hmain)) return false;
				if (!git("switch main")) return false;
			}
			return true;
		}
		
		static void _CheckIgnored() {
			bool i1 = false, i2 = false, i3 = false;
			if (gits(@"check-ignore .compiled/* .temp/* .nuget/*/*.csproj")) {
				i1 = _go.Contains(".comp");
				i2 = _go.Contains(".temp");
				i3 = _go.Contains(".nuge");
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
	}
	
	static void _Thread(Action func) {
		if (_InitCommand()) run.thread(func);
	}
	
	static bool _NotThread => Environment.CurrentManagedThreadId == 1;
	
#if DEBUG
	public static void Test() {
		if (_NotThread) { _Thread(Test); return; }
		
		//gita("");
	}
#endif
	
	public static void Status() {
		if (_NotThread) { _Thread(Status); return; }
		
		print.it(_GetStatus(true));
	}
	
	static bool _Commit(string m = null) {
		if (m == null) {
			m = "backup";
			do {
				if (!dialog.showInput(out m, "Git commit", "Message", editText: m, owner: App.Hmain,
					footer: "Print:    <a href=\"status\">status</a>", onLinkClick: _Link
					)) return false;
			} while (m.NE());
		}
		m = m.Replace("\"", "''");
		
		if (!git("add .")) return false;
		if (!git($"commit -m \"{m}\"")) return false;
		return true;
	}
	
	public static void Commit(bool autoBackup = false, int autoBackupWorkspaceSN = 0) {
		if (_NotThread) { _Thread(() => Commit(autoBackup, autoBackupWorkspaceSN)); return; }
		
		if (autoBackup && App.Model.WorkspaceSN != autoBackupWorkspaceSN) return; //this code runs async with a delay and in new thread. During that time can be loaded another workspace.
		t_autoBackup = autoBackup;
		try {
			var gs = _GetStatus();
			if (!gs.CanCommit) _Print("Nothing to commit.");
			else if (_Commit(autoBackup ? "auto-backup" : null)) _Print("==== DONE ====");
			else if (autoBackup) print.it("Auto-backup failed. Try menu File -> Git -> Commit."); //else git() prints errors
		}
		catch (Exception e1) { print.it((autoBackup ? "Auto-backup: " : null) + "Git commit failed", e1); }
		finally { t_autoBackup = false; }
		
		static void _Print(string s) {
			if (!t_autoBackup) print.it(s);
		}
	}
	[ThreadStatic] static bool t_autoBackup;
	
	public static void Push() {
		if (_NotThread) { _Thread(Push); return; }
		
		var gs = _GetStatus(); //note: don't use _GetStatus(true) to detect whether need to push. In some cases it can't detect local and remote differences, eg after modifying the local commit history. Also makes slower.
		if (gs.CanCommit) if (!_Commit()) return;
		if (!git("push")) {
			gs = _GetStatus(true);
			string s1, s2;
			if (gs.behind > 0) {
				s1 = $"Exist {gs.ahead} local and {gs.behind} remote new different commits.";
				s2 = "0 Cancel|1 Overwrite remote\ngit push --force|2 Pull instead...\nYou can keep local or remote commits";
			} else { //eg after modifying the local commit history
				s1 = "The safe 'git push' command failed.";
				s2 = "0 Cancel|1 Try 'git push --force'\nIt overwrites remote commits.";
			}
			switch (dialog.show("Git push", s1, s2, DFlags.CommandLinks, DIcon.Warning, owner: App.Hmain)) {
			case 1:
				if (!git("push --force")) return;
				break;
			case 2:
				Pull();
				return;
			default: return;
			}
		} else if (_go == "Everything up-to-date") {
			print.it("==== Nothing to push ====");
			return;
		}
		print.it("==== DONE ====");
	}
	
	public static void Pull() {
		if (_NotThread) { _Thread(Pull); return; }
		
		if (!git("fetch")) return;
		var gs = _GetStatus();
		if (gs.behind == 0) { print.it("==== No new remote commits ===="); return; }
		
		//refuse to pull if there are uncommitted changes
		bool stashed = false;
		if (gs.CanCommit) {
			//changes in the .state dir are likely. They are not important and can be dropped. The .toolbars dir is similar.
			//	If uncommitted changes are only in these dirs: Now stash. Finally drop if successful, else restore.
			if (!gs.changes.Any(o => !(o.Eq(3, ".state/", true) || o.Eq(3, ".toolbars/", true)))) stashed = gits("stash");
			if (!stashed) {
				if (1 != dialog.show("Before git pull", "Need to commit, else new local changes would be lost. Commit now?",
					"1 OK|Cancel", owner: App.Hmain,
					footer: "Print:    <a href=\"status\">status</a>", onLinkClick: _Link
					)) return;
				if (!_Commit()) return;
				gs.ahead++;
			}
		}
		
		bool stashDrop = false, push = false;
		try {
			if (gs.ahead > 0) {
				//note: don't merge. It can damage files, eg files.xml.
				int button = dialog.show("Git pull", $"Exist {gs.ahead} local and {gs.behind} remote new different commits.                                              ",
					"0 Cancel|1 Keep remote\nDiscards new local changes and applies remote changes.\nAt first backups the new local commits in a new branch.\nFinally reloads the workspace.|2 Keep local\nDoes not modify the workspace.\nInserts the new remote commits before the new local commits.",
					flags: DFlags.CommandLinks | DFlags.ExpandDown, icon: DIcon.Warning, owner: App.Hmain,
					expandedText: "You can click Cancel and use eg GitHubDesktop to pull. It allows to merge remote changes and resolve possible conflicts. Be careful, it can damage some files.\n\nTo avoid this, always pull before making changes in the workspace.",
					footer: "Print:    <a href=\"diff\">diff for 'keep remote'</a>    <a href=\"diffR\">diff for 'keep local'</a>", onLinkClick: _Link
					);
				
				if (button == 1) {
					if (!git($"branch {DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")}")) return;
					if (!git("reset --hard origin/main")) return;
				} else if (button == 2) {
					if (!git("merge -s ours")) return;
					push = true;
				} else return;
			} else {
				DControls dc = new() { Checkbox = "Backup workspace files", IsChecked = !ScriptEditor.IsPortable };
				if (1 != dialog.show("Git pull", "Will update your local repo and workspace files to match the remote repo.\nFinally will reload the workspace.",
					"1 OK|Cancel", owner: App.Hmain, controls: dc)) return;
				if (dc.IsChecked) _BackupWorkspace();
				if (!git("merge --ff-only")) return;
				if (_go.Starts("Already up to date")) { print.it("==== No new remote commits ===="); return; }
			}
			stashDrop = !push;
		}
		finally {
			if (stashed) {
				if (stashDrop) gits("stash drop"); else gits("stash pop");
			}
		}
		print.it("==== DONE ====");
		
		if (push) {
			//print.it("Now you can push to GitHub. It will add the new local commits to the GitHub repo.");
			if (dialog.showYesNo("Now push to GitHub?", "It will add the new local commits to the GitHub repo.\nOr you can click No and push later.", owner: App.Hmain)) {
				if (!git("push")) return;
				print.it("==== DONE ====");
			}
		} else {
			App.Dispatcher.InvokeAsync(ReloadWorkspace);
		}
	}
	
	public static void RunGui() {
		if (!_InitCommand()) return;
		
		var s = Microsoft.Win32.Registry.GetValue(@"HKEY_CLASSES_ROOT\x-github-client\shell\open\command", "", null) as string;
		if (s != null) {
			s = s.Split(' ')[0].Trim('"');
			if (!filesystem.exists(s)) s = null;
		}
		s ??= folders.LocalAppData + @"GitHubDesktop\GitHubDesktop.exe";
		if (filesystem.exists(s)) run.itSafe(s, null, RFlags.InheritAdmin); else print.it("<>GitHubDesktop not found. <link https://desktop.github.com/>Download<>.");
	}
	
	public static void RunCmd() {
		if (!_InitCommand()) return;
		
		string oldPath = null;
		if (_isPrivateGit) Environment.SetEnvironmentVariable("PATH", pathname.getDirectory(_gitExe) + ";" + (oldPath = Environment.GetEnvironmentVariable("PATH")));
		run.itSafe(folders.System + "cmd.exe", null, RFlags.InheritAdmin, _dir);
		if (_isPrivateGit) Environment.SetEnvironmentVariable("PATH", oldPath);
	}
	
	public static void WorkspaceFolder() {
		run.itSafe(App.Model.WorkspaceDirectory);
	}
	
	public static void ReloadWorkspace() {
		FilesModel.LoadWorkspace(App.Model.WorkspaceDirectory);
	}
	
	public static void Signout() {
		if (_NotThread) { _Thread(Signout); return; }
		
		if (!gits("config --get remote.origin.url")) return;
		_go.RxMatch(@"^https://github.com/(.+?)/", 1, out string user);
		if (!git("credential reject", stdin: $"protocol=https\nhost=github.com\nusername={user}\n\n")) return;
		print.it("==== DONE ====");
	}
	
	public static void Maintenance() {
		if (!_InitCommand()) return;
		
		var w = new KDialogWindow() { Title = "Git repository maintenance", ShowInTaskbar = false, Owner = App.Wmain };
		var b = new wpfBuilder(w).WinSize(400);
		
		var info = new System.Windows.Documents.Run();
		_Info();
		b.R.xAddInfoBlockF($"""
The local repository is folder <a {() => { var s1 = _dir + @"\.git"; filesystem.setAttributes(s1, FileAttributes.Hidden, false); run.selectInExplorer(s1); }}>.git</a> in the workspace folder.
{info}
Use this tool to make it smaller or delete old commits or branches.
This tool modifies only the .git folder, not workspace files.
""");
		
		b.R.xAddGroupSeparator("Safe tasks");
		b.R.AddButton("Run 'git gc' to compact the folder", async _ => {
			w.IsEnabled = false;
			await Task.Run(() => _Gc());
			_Info();
			w.IsEnabled = true;
		}).Tooltip("Collects garbage. Packs and compresses files.\nDoes not change the history, branches, etc.");
		
		b.R.xAddGroupSeparator("Destructive tasks");
		b.R.AddButton("Delete branch...", _ => _Branches());
		b.R.AddButton("Delete old commits...", _ => _DeleteOldCommits());
		
		b.R.xAddGroupSeparator("Info");
		b.R.AddButton("Print workspace folder sizes", _ => _PrintFolderSizes()).Tooltip("Print sizes of workspace folders except .git, .compiled and .temp.\nThis info can be useful when editing file .gitignore.");
		
		b.End();
		w.Show(); //not ShowDialog
		
		void _Info() {
			int nFiles = 0, nDirs = 0;
			long size = filesystem.enumerate(_dir + "\\.git", FEFlags.AllDescendants | FEFlags.IgnoreInaccessible).Sum(o => { if (o.IsDirectory) { nDirs++; return 0; } nFiles++; return o.Size; });
			gits("rev-list --count HEAD");
			info.Text = $"Size {size / 1024 / 1024} MB. Contains {nFiles} files and {nDirs} folders.\nThe main branch contains {_go} commits.";
		}
		
		static void _Gc() {
			//something adds readonly attribute to many dirs in .git/objects. Then git gc cannot delete them.
			foreach (var v in filesystem.enumDirectories(_dir + @"\.git\objects")) {
				//print.it(v.Attributes, v.Name);
				if (v.Attributes.Has(FileAttributes.ReadOnly)) {
					try { File.SetAttributes(v.FullPath, FileAttributes.Directory); } catch { }
				}
			}
			git("reflog expire --all --expire=now"); //need after deleting old commits, else does not make smaller
			git("gc --aggressive --prune=now");
			//run.it("cmd.exe", $"/c \"{gitExe}\" gc --aggressive --prune=now", RFlags.InheritAdmin | RFlags.WaitForExit, _dir);
			print.it("==== DONE ====");
		}
		
		void _Branches() {
			if (!gits("branch")) return;
			var m = new popupMenu();
			foreach (var v in _go.Lines()) {
				if (v.Starts('*')) m.Add(v).IsDisabled = true;
				else m[v.TrimStart()] = _DeleteBranch;
			}
			m.Show();
			
			void _DeleteBranch(PMItem mi) {
				var s = mi.Text;
				if (1 != dialog.show("Delete branch?", s + "\n\nThis action cannot be undone.", "0 Cancel|1 Delete", icon: DIcon.Warning, owner: w)) return;
				if (!git($"branch -D \"{s}\"")) return;
				_Info();
				print.it("==== DONE ====");
			}
		}
		
		async void _DeleteOldCommits() {
			if (1 != dialog.show("Deleting old commits", "This will delete all commits except the last.\nThen will push to GitHub.\n\nThis action cannot be undone.", "0 Cancel|1 Delete", icon: DIcon.Warning, owner: w)) return;
			w.IsEnabled = false;
			await Task.Run(() => {
				if (!git("checkout --orphan m7j9u447352k")) return;
				if (!git("add .")) return;
				if (!git($"commit -m \"Deleted old commits\"")) return;
				if (!git("branch -D main")) return;
				if (!git("branch -m main")) return;
				git("push --force");
				_Gc();
			});
			_Info();
			w.IsEnabled = true;
			
			//Also tried to delete old commits older than a selected commit. But could not find how to do it correctly.
			//	Git rebase fails (errors etc).
			//	Git clone shallow works, but does not delete remote commits.
		}
		
		static void _PrintFolderSizes() {
			FileTree.PrintSizes(_dir, true, .1, dirFilter: o => !(o.Level is 0 && o.Name.Lower() is ".git" or ".compiled" or ".temp"));
		}
	}
	
	public static void AutoBackup(bool now) {
		if (!App.Model.UserSettings.gitBackup) return;
		if (!CodeInfo.IsReadyForEditing) return;
		if (!now && _autoBackupTime != 0 && Environment.TickCount64 - _autoBackupTime < 1000 * 60 * 60 * 4) return; //4 hours
		_autoBackupTime = Environment.TickCount64;
		var wsSN = App.Model.WorkspaceSN;
		timer.after(500, _ => {
			//print.it("auto-backup", DateTime.Now);
			Commit(autoBackup: true, autoBackupWorkspaceSN: wsSN);
		});
	}
	static long _autoBackupTime;
	
	#endregion
	
	#region setup
	
	public static bool IsReady => _Start();
	
	public static void Setup() {
		new _DSetup().ShowDialog();
	}
	
	class _DSetup : KDialogWindow {
		public _DSetup() {
			_Start(setup: true);
			
			InitWinProp("Git setup", App.Wmain);
			var b = new wpfBuilder(this).WinSize(500).Columns(0, -1, 0);
			var panel = b.Panel;
			TextBox tUrl = null;
			
			b.R.StartGrid();
			Action lHelp = () => HelpUtil.AuHelp("editor/Git, backup, sync"),
				lGithub = () => run.itSafe(tUrl.TextOrNull() ?? "https://github.com");
			b.Add<TextBlock>().FormatText($"<a {lHelp}>Help</a>    <a href='{_dir}'>Workspace folder</a>    <a {lGithub}>GitHub</a>").Align(y: VerticalAlignment.Center);
			b.End();
			
			b.R.xAddGroupSeparator("GitHub repository");
			var url = _GetURL();
			b.R.Add<AdornerDecorator>("URL", out _).Add(out tUrl, url, flags: WBAdd.ChildOfLast).Watermark("https://github.com/owner/repo")
				.Validation(o => !_ParseURL(tUrl.Text, out _, out _) ? (tUrl.Text.NE() ? "URL empty" : "URL must be like https://github.com/owner/repo") : null);
			
			b.R.xAddGroupSeparator("Git");
			CancellationTokenSource cts = null;
			Button bGitInstall = null;
			b.R.Add(out Label tGitStatus).Span(2).AddButton(out bGitInstall, "", _InstallGit)
				.Validation(o => !_FindGit(out _, out _) ? "Git not installed" : null);
			_SetGitControlText();
			b.Window.Closed += (_, _) => { cts?.Cancel(); };
			
			b.R.AddSeparator();
			b.R.Add("OK", out ComboBox cbOK).Items(filesystem.exists(_dir + @"\.git").Directory ? "Update local settings" : "Create local repository linked with the still empty GitHub repository", "Clone (download) the GitHub repository to new workspace");
			b.R.AddOkCancel();
			b.End();
			
			b.OkApply += async e => {
				e.Cancel = true;
				App.Model.WSSett.CurrentUser.gitUrl = tUrl.Text;
				_Start(setup: true); //may need to set _gitExe
				var w = b.Window.Hwnd();
				b.Window.IsEnabled = false; //disables controls but allows to close the window, unlike b.Window.Hwnd().Enable(false);. Git may hang, eg when auth fails on my vmware Win7.
				bool ok = false, clone = cbOK.SelectedIndex == 1;
				try {
					ok = await Task.Run(() => _OkTask(clone));
				}
				catch (Exception e1) { print.it(e1); }
				if (ok) b.Window.Close(); else b.Window.IsEnabled = true;
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
					print.warning(e1);
					if (e1 is UnauthorizedAccessException && !uacInfo.isAdmin) print.it("\tRestart this program as administrator.");
					return false;
				}
				return true;
			}
			
			void _SetGitControlText() {
				bool found = _FindGit(out _, out bool isPrivate);
				tGitStatus.Content = !found ? "Git not installed" : isPrivate ? "Private Git is installed and will be used" : "Shared Git found and will be used";
				bGitInstall.Content = isPrivate ? "Update private Git" : "Install private Git";
			}
		}
		
		protected override void OnSourceInitialized(EventArgs e) {
			App.Model.UnloadingThisWorkspace += Close;
			base.OnSourceInitialized(e);
		}
		
		protected override void OnClosed(EventArgs e) {
			App.Model.UnloadingThisWorkspace -= Close;
			base.OnClosed(e);
		}
		
		static bool _OkTask(bool clone) {
			var url = App.Model.WSSett.CurrentUser.gitUrl;
			if (!url.Ends(".git")) url += ".git";
			
			if (clone) {
				var dir2 = pathname.makeUnique(_dir, isDirectory: true);
				bool cloned = git($"clone \"{url}\" \"{dir2}\"");
				if (cloned && !(cloned = FilesModel.IsWorkspaceDirectoryOrZip(dir2, out _))) print.it("Error. The GitHub repository does not contain a workspace.");
				if (!cloned) { filesystem.delete(dir2); return false; }
				
				var dir3 = _dir; _dir = dir2;
				try { _Config(); }
				finally { _dir = dir3; }
				
				Panels.Output.Scintilla.AaTags.AddLinkTag("+openWorkspace", s => FilesModel.LoadWorkspace(dir2));
				print.it($"<>The GitHub repository has been cloned to <link {dir2}>new workspace folder<>. Now you can <+openWorkspace {dir2}>open the new workspace<>. Also you may want to copy some files from <link {_dir}>old workspace<>.");
			} else {
				var file1 = _dir + "\\.gitignore";
				if (!filesystem.exists(file1)) filesystem.saveText(file1, c_defaultGitignore);
				
				var file2 = _dir + "\\.gitattributes";
				if (!filesystem.exists(file2)) filesystem.saveText(file2, """
* -text
/.state/* binary

""");
				
				bool localExists = filesystem.exists(_dir + @"\.git").Directory;
				if (localExists && gits("remote get-url origin") && _go == url) return true;
				
				//repo exists?
				if (!git($"ls-remote {url}")) return false;
				//rejected: try to create repo now.
				//	To get token, see the commented out _GithubGet overload.
				
				if (localExists) {
					if (!git("remote set-url origin " + url)) return false;
				} else {
					//repo empty?
					if (!_go.NE()) {
						print.it("Error. The GitHub repository must be empty (0 commits, no readme etc). Else it can only be cloned.");
						return false;
					}
					
					//repo private?
					_ParseURL(url, out var owner, out var repo); //info: the dialog validates the URL format
					var r = _GithubGet($"users/{owner}/repos");
					if (r.AsArray().Any(v => ((string)v["name"]).Eqi(repo))) {
						print.it("Error. The GitHub repository must be private. You can make it public later if really need.");
						return false;
					}
					
					if (!git("init -b main")) return false;
					if (!git("remote add origin " + url)) return false;
					if (!git("config push.autoSetupRemote true")) return false;
					_Config();
					print.it("<>Local Git repository has been created. Now you can use the Git menu to make backups etc.  [<help editor/git, backup, sync>Help<>]");
				}
			}
			return true;
			
			void _Config() {
				git("config core.autocrlf false");
				git("config core.quotepath off");
				
				if (!gits("config --get user.name") || _go.NE()) git("config user.name \"unknown\"");
				if (!gits("config --get user.email") || _go.NE()) git("config user.email \"unknown@unknown.com\"");
			}
		}
		
		static bool _ParseURL(string url, out string owner, out string repo) {
			if (!url.RxMatch(@"^https://github.com/([[:alnum:]\-]+)/([\w\-\.]+?)(?:\.git)?$", out var m)) { owner = repo = null; return false; }
			owner = m[1].Value;
			repo = m[2].Value;
			return true;
		}
		
		static string _GetURL() {
			if (_gitExe != null && gits("config --get remote.origin.url") && !_go.NE()) return _go.Ends(".git", true) ? _go[..^4] : _go;
			return App.Model.WSSett.CurrentUser.gitUrl;
		}
		
		static JsonNode _GithubGet(string endpoint) {
			var ah = new[] { "Accept: application/vnd.github+json", "X-GitHub-Api-Version: 2022-11-28" };
			var r = internet.http.Get($"https://api.github.com/{endpoint}", headers: ah);
			return r.Json();
		}
		
		//not used, not finished, just shows how to get oauth token to use GitHub features where need it
		//static JsonNode _GithubGet(string endpoint, string user) {
		//	if (!gits("credential fill", stdin: $"protocol=https\nhost=github.com\nusername={user}\n\n")) throw new AuException();
		//	if (!_go.RxMatch(@"\Rpassword=(.+)", 1, out string token)) throw new AuException();
		
		//	var ah = new[] { "Accept: application/vnd.github+json", "X-GitHub-Api-Version: 2022-11-28", $"Authorization: Bearer {token}" };
		//	var r = internet.http.Get($"https://api.github.com/{endpoint}", headers: ah);
		//	return r.Json();
		//}
	}
	
	#endregion
	
	#region util
	
	/// <summary>
	/// What should <b>git</b> print.
	/// </summary>
	enum _GP {
		/// <summary>Print blue and error. Default.</summary>
		Error,
		/// <summary>Print everything.</summary>
		All,
		/// <summary>Print blue only.</summary>
		Blue,
		/// <summary>Print nothing.</summary>
		Silent
	}
	
	static bool git(string s, _GP gp = _GP.Error, string stdin = null) {
		if (t_autoBackup) gp = _GP.Silent;
		bool silent = gp == _GP.Silent;
		if (!silent) print.it($"<><c #4040FF>git {s}<>");
		using var c = new consoleProcess(_gitExe, s, _dir);
		if (stdin != null) c.Write(stdin);
		
		bool slow = !silent && 0 != s.Starts(false, "clone", "add", "commit", "push", "fetch", "gc", "ls-remote");
		var t = slow ? timer2.after(3000, static o => { if (o.Tag == null) print.it("wait..."); }) : null;
		
		_go = c.ReadAllText().Trim("\r\n");
		
		if (t != null) { t.Tag = ""; t.Stop(); }
		
		int ec = c.ExitCode;
		bool ok = ec == 0;
		if (!ok) _go = $"[{ec}] {_go}";
		if (!_go.NE()) if (gp == _GP.All || (!ok && gp == _GP.Error)) _Print(_go, !ok);
		return ok;
		
		static void _Print(string s, bool error) {
			var color = error ? "FF3300" : "206000";
			print.it($"<><c #{color}><_>{s}</_><>");
		}
	}
	
	static bool gits(string s, string stdin = null) => git(s, _GP.Silent, stdin);
	
	static bool gita(string s, string stdin = null) => git(s, _GP.All, stdin);
	
	static _Status _GetStatus(bool remoteUpdate = false) {
		if (remoteUpdate) git("remote update"); //can be "fetch", but somehow I see "remote update" everywhere. Same speed (> 1.5 s). Both download everything including blobs, even "fetch --dry-run".
		git("status -z --branch --no-renames");
		
		int j = _go.IndexOf('\0');
		string s1 = _go[..j], s2 = j < _go.Length - 1 ? _go[++j..] : null;
		if (!s1.RxMatch(@"^##[^\[]+(?:\[(?:ahead (\d+))?(?:, )?(?:behind (\d+))?\])?", out var m)) throw new AuException("Unexpected 'git status' output:\r\n" + _go);
		
		return new(m[1].Exists ? m[1].Value.ToInt() : 0, m[2].Exists ? m[2].Value.ToInt() : 0, s2?.Split('\0', StringSplitOptions.RemoveEmptyEntries));
	}
	
	record struct _Status(int ahead, int behind, string[] changes) {
		public bool CanCommit => changes != null;
		
		public override string ToString() {
			var b = new StringBuilder($"New commits: {ahead} local, {behind} remote.\r\n");
			if (changes == null) {
				b.Append("No changes to be committed.");
			} else {
				var a = changes;
				for (int i = 0; i < a.Length; i++) {
					var s2 = a[i][..2];
					string s = s2 switch {
					[_, 'D'] or ['D', ' '] => "  deleted:  ",
					[_, '?'] or ['A', _] => "  added:    ",
					[_, 'M'] or ['M', ' '] => "  modified: ",
						_ => "  " + s2 + ":       "
					};
					a[i] = s + a[i][2..];
				}
				b.AppendLine("Changes to be committed:").AppendJoin("\r\n", a.OrderBy(o => o));
			}
			return b.ToString();
		}
	}
	
	static void _Link(DEventArgs e) {
		switch (e.LinkHref) {
		case "status": Status(); break;
		case "diff": _Diff(true); break;
		case "diffR": _Diff(false); break;
		}
		
		void _Diff(bool R) {
			var s1 = R ? "-R " : "";
			git($"diff {s1}--compact-summary origin/main");
			print.it(_go);
			git($"diff {s1}origin/main");
			_go = _go.RxReplace(@"\Rindex .+", "", 1).RxReplace(@"\R--- .(.+)\R\+\+\+ .\1", "", 1);
			print.it(_go);
		}
	}
	
	static bool _BackupWorkspace() {
		if (!gits("ls-tree -r HEAD --name-only")) goto ge;
		
		var zipFile = _dir + @"\.temp\git backup.7z";
		if (filesystem.exists(zipFile)) filesystem.delete(zipFile); else filesystem.createDirectoryFor(zipFile);
		using (var tf = new TempFile()) {
			filesystem.saveText(tf, _go);
			if (0 != run.console(out _, folders.Editor + @"32\7za.exe", $@"a ""{zipFile}"" -iw-@""{tf}""", _dir)) goto ge;
		}
		
		print.it($"<>Created <explore {zipFile}>temporary backup<> of workspace files.");
		return true;
		ge: print.it("Failed to backup workspace files.");
		return false;
	}
	
	#endregion
}
