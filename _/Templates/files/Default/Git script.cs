/*/ noWarnings CS8321; /*/
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

if (script.testing) {
	ScriptEditor.InvokeCommand("Git_test", dontWait: true); //hidden command that runs this script like from the Git menu, with args[0] = "Test"
	return;
}

//print.it(args);
string command = args[0]; //menu command. One of menu_X from the list above.
string gitExe = args[1]; //git.exe path
string dir = args[2]; //workspace directory
string url = args[3]; //GitHub repository URL
string message = args[4]; //default commit message
string go = null; //git() output

const string menu_Status = "Git status"; //*MaterialDesign.InfoOutline #EABB00|#E0E000
const string menu_Commit = "_Commit (local backup)"; //*RemixIcon.GitCommitLine #464646|#E0E000
const string menu_Push = "Commit and _push (upload)"; //*Unicons.CloudUpload #3586FF|#E0E000
const string menu_Pull = "P_ull (download and update)"; //*Unicons.CloudDownload #3586FF|#E0E000
const string menu_GUI = "GitHubDesktop"; //- *Codicons.Github #771FB1|#E0E000
const string menu_Cmd = "Cmd"; //*Material.Console #464646|#E0E000
const string menu_Folder = "Workspace folder"; //*Material.Folder #EABB00|#E0E000
const string menu_ReloadWS = "Reload workspace"; //*Material.Reload #464646|#E0E000
const string menu_Maintenance = "Maintenance...";

bool test = false;
switch (command) {
case "Test": Test(); break;
case menu_Status: Status(); break;
case menu_Commit: Commit(); break;
case menu_Push: Push(); break;
case menu_Pull: Pull(); break;
case menu_GUI: RunGui(); break;
case menu_Cmd: RunCmd(); break;
case menu_Folder: run.itSafe(dir); break;
case menu_ReloadWS: ReloadWorkspace(); break;
case menu_Maintenance: Maintenance(); break;
}

void Test() {
	test = true;
	print.clear();
	if (!_EnsureMain()) return;
	//Status();
	//Commit();
	//Push();
	//Pull();
	perf.first();
	Maintenance();
	
	//gita("add .");
	//gita("commit -m backup");
	//if (!git("commit -m backup", notError: o => o.Like("[1] *\nnothing to commit*"))) return;
	//print.it("ok");
	//gita("push origin");
	//var d = "protocol=https\nhost=github.com\nusername=qgindi\n\n";
	//git("credential reject", d);
	//gita("credential fill", d);
	//gita("fetch");
	//gita("fetch --dry-run");
	//gita("remote update");
	//gita("status");
	//gita("status -v");
	//gita("ls-files --modified");
	//gita("ls-files --deleted");
	//gita("ls-files --others");
	//gita("diff --name-status"); //shows modified and deleted, with prefix MM/D
	//gita("status --short"); //shows modified, deleted and added, with prefix MM/D/A
	//gita("show");
	//gita("log");
	//gita("log -n3");
	//gita("diff");
	//gita("blame files/Script1.cs");
	//gita("""grep --cached --word-regexp "System.IO.Compression" """);
	//gita("""grep --cached "\bSystem\.IO\.Compression\b" """);
	//gita("""grep --cached --perl-regexp "\bSystem\.IO\.Compression\b" """);
	//gita("merge --abort");
	
	//gita("merge --ff-only");
	
	//git("remote update");
	//if (!_GetStatus(out var can)) return;
	//print.it(go);
	//print.it(can.Add, can.Commit, can.Push);
	
	//print.it(gita("branch -r"));
	
	//gita("remote -v");
	
	//gita(@"check-ignore --verbose .compiled/*");
	//gita(@"check-ignore .compiled/*");
	//gita(@"check-ignore .compiled/* .temp/* .nuget/*/*.csproj");
	//gita(@"status --ignored");
	//gita(@"ls-files -i");
	
	//gita("status --short");
	//gita("status --porcelain=2");
	
	//if (git("merge")) return;
	//dialog.show("debug");
	
	
	////gita("mergetool --tool-help");
	////run.it("git.exe", "mergetool --tool=tortoisemerge", RFlags.WaitForExit, dir);
	//run.it("git.exe", "mergetool --tool=winmerge", RFlags.WaitForExit, dir);
	////gita("mergetool --tool=winmerge");
	
	
	//if (!git("merge --abort")) return;
	
	//if (!git($"branch {DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")}")) return;
	
	//print.it(DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss"));
	
	//if (!gita("branch --show-current")) return;
	
	//gita("diff --name-status origin/main");
	//gita("diff --compact-summary origin/main");
	//gita("diff --stat origin/main"); //same as --compact-summary
	//gita("diff -R origin/main");
	//git("diff -R main origin/main");
	//go = go.RxReplace(@"\Rindex .+", "", 1).RxReplace(@"\R--- .(.+)\R\+\+\+ .\1", "", 1);
	//_Print(go, false);
	
	//gita("add \"README r.md\"");
	//gita("add files/local.cs");
	
	//_PrintStatus();
	
	//gita("log -n3 --date=human");
	//gita("log --date=human");
	//var m = new popupMenu();
	//foreach (var v in go.RxFindAll(@"(?m)^commit (\w+)(?:\R.+)+?\RDate:\h+(.+)\R\R\h+(.+)")) {
	//	//print.it($"{v[2].Value}\t{v[3].Value}");
	//	m[$"{v[2].Value}\t{v[3].Value}"] = null;
	//}
	
	//m.Show();
	
	
	
	
	//filesystem.copy(@"C:\Users\G\Desktop\.git - Copy", @"C:\Users\G\Documents\LibreAutomate\Main\.git", FIfExists.Delete);
	
	//if (!git("checkout --orphan j9u447352")) return;
	////if (!git("checkout --orphan j9u447352 " + hash)) return;
	//if (!git("add -A")) return;
	//if (!git("commit -m Test")) return;
	//if (!git("branch -D main")) return;
	//if (!git("branch -m main")) return;
	////if (!git("push -f origin main")) return;
	
	
	////something adds readonly attribute to many dirs in .git/objects. Then git gc fails to delete them.
	//foreach (var v in filesystem.enumDirectories(dir + @"\.git\objects")) {
	//	//print.it(v.Attributes, v.Name);
	//	if (v.Attributes.Has(FileAttributes.ReadOnly)) {
	//		try { File.SetAttributes(v.FullPath, FileAttributes.Directory); } catch { }
	//	}
	//}
	////if (!git("reflog expire --all --expire=now")) return;
	//if (!git("gc --aggressive --prune=now")) return;
	
	
	
	
	//_Branches();
	//void _Branches() {
	//	if (!gits("branch") || !go.Contains('\n')) return;
	//	var m = new popupMenu();
	//	foreach (var v in go.Lines()) {
	//		if (v.Starts('*')) continue;
	//		//var s=v.TrimStart(" *");
	//		var s=v.TrimStart();
	//		m[s]=_DeleteBranch;
	//	}
	//	m.Show();
	
	//	void _DeleteBranch(PMItem mi) {
	//		print.it(mi.Text);
	//	}
	//}
	
	//gita("diff");
	//gita("ls-files --eol");
	//gita("config core.autocrlf false");
	
	//gita("rev-list --count HEAD");
	//gita("rev-list --no-merges --count HEAD");
	//gita("rev-list HEAD");
}

void Status() {
	if (!_EnsureMain()) return;
	git("remote update");
	_PrintStatus();
}

bool _Commit(out GS can) {
	if (!_GetStatus(out can)) return false;
	if (can.Commit) {
		var m = message;
		while (m.NE()) if (!dialog.showInput(out m, "Git commit", "Message")) return false;
		m = m.Replace("\"", "''");
		
		if (/*can.Add &&*/ !git("add .")) return false;
		if (!git($"commit -m \"{m}\"")) return false;
	}
	return true;
}

void Commit() {
	if (!_EnsureMain()) return;
	if (!_Commit(out var can)) return;
	print.it(can.Commit ? "==== DONE ====" : "Nothing to commit.");
}

void Push() {
	if (!_EnsureMain()) return;
	if (!_Commit(out var can)) return;
	if (!can.Push) { print.it(can.Commit ? "Nothing to push." : "Nothing to commit or push."); return; }
	if (!git("push")) {
		if (!go.Like("[1] *[rejected]*")) return;
		int button = dialog.show("Git push", "Rejected.", "0 Cancel|1 Push --force\nOverwrite remote.|2 Try to pull instead...", DFlags.CommandLinks, DIcon.Warning);
		switch (button) {
		case 1:
			if (!git("push --force")) return;
			break;
		case 2:
			Pull();
			return;
		default:
			//print.it("Git push canceled.");
			return;
		}
	}
	print.it("==== DONE ====");
}

void Pull() {
	if (!_EnsureMain()) return;
	if (!git("fetch")) return;
	
	//refuse to pull if there are uncommitted changes
	if (!_GetStatus(out var can)) return;
	var status = go;
	bool stashed = false;
	if (can.Commit) {
		//changes in the .state dir are very likely. They are not important and can/should be dropped. The .toolbars dir is similar.
		//	Now stash. Finally drop if successful, else restore.
		if (gits("status --short")) {
			if (go.RxIsMatch(@"^( *\S+ ""?\.(?:state|toolbars)/.+)(\R(?1))*$")) stashed = gits("stash"); //all uncommitted changes are in these dirs
		}
		if (!stashed) {
			if (1 != dialog.show("Before git pull", "Need to commit, else new local changes would be lost. Commit now?",
				"1 OK|Cancel",
				footer: "Print:    <a href=\"1\">status</a>",
				onLinkClick: _Link
				)) return;
		}
	}
	
	bool stashDrop = false, push = false;
	try {
		if (!git("merge --ff-only", GP.Blue)) {
			if (!go.Contains("fatal: Not possible to fast-forward")) { _Print(go, true); return; }
			
			//note: don't merge. It can damage files, eg files.xml.
			int button = dialog.show("Git pull", "The remote and local repositories both contain new commits. How to resolve it?",
				"0 Cancel|1 Keep remote changes\nWill replace workspace files with files from the remote repository.\nWill move local changes to a backup branch.|2 Keep local changes\nNext 'push' will upload them to the remote repository.\nRemote changes will be in the commit history.",
				flags: DFlags.CommandLinks | DFlags.ExpandDown, icon: DIcon.Warning,
				expandedText: "You can click Cancel and use eg GitHubDesktop to pull. It allows to merge remote changes and resolve possible conflicts. Be careful, it can damage some files.\n\nTo avoid this, always pull before making any changes in the workspace.",
				footer: "Print:    <a href=\"1\">status</a>    <a href=\"2\">diff for 'keep remote'</a>    <a href=\"3\">diff for 'keep local'</a>",
				onLinkClick: _Link
				);
			
			if (button == 1) {
				if (!git($"branch {DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")}")) return;
				if (!git("reset --hard origin/main")) return;
			} else if (button == 2) {
				if (!git("merge -s ours")) return;
				push = true;
			} else return;
		} else {
			if (go.Starts("Already up to date.")) { print.it("No new remote commits."); return; }
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
		if (dialog.showYesNo("Now push?")) {
			if (!git("push")) return;
			print.it("==== DONE ====");
		}
	} else {
		if (!test) ReloadWorkspace();
	}
	
	void _Link(DEventArgs e) {
		switch (e.LinkHref) {
		case "1": _PrintStatus(); break;
		case "2": _Diff(true); break;
		case "3": _Diff(false); break;
		}
		
		void _Diff(bool R) {
			var s1 = R ? "-R " : "";
			git($"diff {s1}--compact-summary origin/main");
			print.it(go);
			git($"diff {s1}origin/main");
			go = go.RxReplace(@"\Rindex .+", "", 1).RxReplace(@"\R--- .(.+)\R\+\+\+ .\1", "", 1);
			print.it(go);
		}
	}
}

void RunGui() {
	var s = Microsoft.Win32.Registry.GetValue(@"HKEY_CLASSES_ROOT\x-github-client\shell\open\command", "", null) as string;
	if (s != null) {
		s = s.Split(' ')[0].Trim('\"');
		if (!filesystem.exists(s)) s = null;
	}
	s ??= folders.LocalAppData + @"GitHubDesktop\GitHubDesktop.exe";
	if (filesystem.exists(s)) run.itSafe(s, null, RFlags.InheritAdmin); else print.it("<>GitHubDesktop not found. <link https://desktop.github.com/>Download<>.");
}

void RunCmd() {
	if (filesystem.searchPath("git.exe") == null) Environment.SetEnvironmentVariable("PATH", pathname.getDirectory(gitExe) + ";" + Environment.GetEnvironmentVariable("PATH"));
	run.it(folders.System + "cmd.exe", null, RFlags.InheritAdmin, dir);
	1.s(); //workaround for: occasionally inactive window if miniProgram
}

void ReloadWorkspace() {
	ScriptEditor.InvokeCommand("Reload_this_workspace", dontWait: true);
}

void Maintenance() {
	perf.next();
	var b = new wpfBuilder("Git repository maintenance").WinSize(400);
	
	WBLink link1 = new(".git", _ => { var s1 = dir + @"\.git"; filesystem.setAttributes(s1, FileAttributes.Hidden, false); run.selectInExplorer(s1); });
	var info = new System.Windows.Documents.Run();
	perf.next();
	_Info();
	perf.next('i');
	b.Loaded += () => { perf.next(); timer.after(1, _ => { perf.nw(); }); };//TODO
	b.R.Add(out TextBlock tInfo)
		.Text("The local repository is folder ", link1, " in the workspace folder. It contains all commits (workspace backups).\n", info,
		"\nUse this tool to make it smaller or delete old commits or branches. This tool modifies only the .git folder, not workspace files.");
	tInfo.TextWrapping = TextWrapping.Wrap;
	tInfo.Background = new SolidColorBrush((Color)(ColorInt)0xF8FFF0);
	
	_Sep("Safe tasks");
	b.R.AddButton("Run git gc to compact the folder", async _ => {
		b.Window.IsEnabled = false;
		await Task.Run(() => _Gc());
		_Info();
		b.Window.IsEnabled = true;
	}).Tooltip("Collects garbage. Packs and compresses files.\nDoes not change the history, branches, etc.");
	
	_Sep("Destructive tasks");
	b.R.AddButton("Delete branch...", _ => _Branches());
	b.R.AddButton("Delete commits older than...", _ => _DeleteOldCommits());
	
	b.End();
	if (!b.ShowDialog()) return;
	
	void _Info() {
		int nFiles = 0, nDirs = 0;
		long size = filesystem.enumerate(dir + "\\.git", FEFlags.AllDescendants | FEFlags.IgnoreInaccessible).Sum(o => { if (o.IsDirectory) { nDirs++; return 0; } nFiles++; return o.Size; });
		info.Text = $"Size {size / 1024 / 1024} MB. Contains {nFiles} files and {nDirs} folders.";
	}
	
	void _Sep(string text) {
		//b.R.StartGrid().Columns(-1, 0, -1).AddSeparator().Add<Label>(text).AddSeparator().End();
		b.R.StartDock().Add<Label>(text).AddSeparator().End();
	}
	
	void _Gc() {
		//something adds readonly attribute to many dirs in .git/objects. Then git gc cannot delete them.
		foreach (var v in filesystem.enumDirectories(dir + @"\.git\objects")) {
			//print.it(v.Attributes, v.Name);
			if (v.Attributes.Has(FileAttributes.ReadOnly)) {
				try { File.SetAttributes(v.FullPath, FileAttributes.Directory); } catch { }
			}
		}
		//if (!git("reflog expire --all --expire=now")) return;
		git("gc --aggressive --prune=now");
		//run.it("cmd.exe", $"/c \"{gitExe}\" gc --aggressive --prune=now", RFlags.InheritAdmin | RFlags.WaitForExit, dir);
	}
	
	void _Branches() {
		if (!gits("branch")) return;
		var m = new popupMenu();
		foreach (var v in go.Lines()) {
			if (v.Starts('*')) m.Add(v).IsDisabled = true;
			else m[v.TrimStart()] = _DeleteBranch;
		}
		m.Show();
		
		void _DeleteBranch(PMItem mi) {
			var s = mi.Text;
			if (1 != dialog.show("Delete branch?", s + "\n\nThis action cannot be undone.", "0 Cancel|1 Delete", icon: DIcon.Warning, owner: b.Window)) return;
			if (!git($"branch -D \"{s}\"")) return;
			_Info();
		}
	}
	
	async void _DeleteOldCommits() {
		gits("log --date=iso-local");
		
		var w = b.Window;
		var m = new popupMenu();
		List<RXMatch> a = new();
		foreach (var v in go.RxFindAll(@"(?m)^commit (.+)(?:\R.+)*\RDate:\h+(.+) .....\R\R\h+(.+)")) {
			a.Add(v);
			m.Add(a.Count, $"{a.Count}.  {v[2].Value/*[..^3]*/}\t{v[3].Value}");
		}
		m.Last.IsDisabled = true;
		int depth = m.Show(owner: w);
		if (depth < 1) return;
		
		if (!test) if (!dialog.showOkCancel("Deleting old commits", "This will delete commits older than the selected.\n\nAlso will delete all branches except main.\n\nWill move the .git folder to the Recycle Bin and create new smaller .git folder.", icon: DIcon.Warning, owner: w)) return;
		
		w.IsEnabled = false;
		await Task.Run(() => {
#if true //This code uses git clone. Works better than the #else code. However branches are lost, is it bad or good.
			g1:
			var v = a[depth - 1];
			var date = v[2].Value;
			
			using (var tf = new TempFile()) {
				//if (!git($"clone --no-local --depth {i + 1} \"{dir}\" \"{tf}\"")) return; //does not work as documented. Eg if specified 20, can get 30 instead. Depends on repo.
				if (!git($"clone --no-local --shallow-since=\"{date}\" \"{dir}\" \"{tf}\"")) return;
				//if (!git($"clone --shallow-since=\"{v[2].Value[..^6]}\" \"{url}\" \"{tf}\"")) return; //the same
				
				//Sometimes git clone skips the selected commit and instead picks a later commit.
				//	It happens when the selected commit was done on GitHub and next local commit is a merge commit.
				//	Partial workaround: prevent deleting the selected commit and instead keep more commits than selected.
				var dir1 = dir; dir = tf;
				try {
					if (gits("rev-list --count HEAD") && go.ToInt(out int n) && n < depth) {
						if (++depth < a.Count) {
							print.it("<><c orange>Cannot use this commit. Retrying with previous (older) commit.<>");
							goto g1;
						}
						print.it("<><c red>Cannot use this commit. Please select another commit.<>");
						return;
					}
				}
				finally { dir = dir1; }
				
				//never mind: sometimes, when selected the last possible commit, error "fatal: remote did not send all necessary objects".
				
				filesystem.copy(dir + @"\.git\config", tf.File + @"\.git\config", FIfExists.Delete);
				if (!test) filesystem.delete(dir + @"\.git", FDFlags.RecycleBin | FDFlags.CanFail);
				filesystem.move(tf.File + @"\.git", dir + @"\.git", FIfExists.Delete);
			}
#else //This code can only remove all branches except the last. And not always makes .git smaller. If used, remove the menu and change text in the confirmation dialog.
			//todo: backup.
			var v = a[depth - 1];
			var hash = v[1].Value;
			if (!git("checkout --orphan j9u447352")) return;
			//if (!git("checkout --orphan j9u447352 " + hash)) return; //does not work. In examples it is used when the below code would contain git rebase, which just throws many errors.
			if (!git("add -A")) return;
			if (!git($"commit -m \"{v[3].Value}\"")) return;
			if (!git("branch -D main")) return;
			if (!git("branch -m main")) return;
			_Gc();
#endif
			print.it("Deleted old commits. Now you can push to the remote repository.");
		});
		_Info();
		w.IsEnabled = true;
	}
}

//Low level.
//Prints nothing. Returns exitcode and gets output. Does not set the go variable.
int gitL(string s, out string so, string stdin = null) {
	using var c = new consoleProcess(gitExe, s, dir);
	if (stdin != null) c.Write(stdin);
	so = c.ReadAllText().Trim("\r\n");
	return c.ExitCode;
}

//High level.
//Sets the go variable = output. If failed, it is "[exitcode] output".
//If failed, calls notError(go), and assumes succeeded if it return true.
bool git(string s, GP gp = GP.Error, Func<string, bool> notError = null, string stdin = null) {
	if (gp != GP.Silent) print.it($"<><c #4040FF>git {s}<>");
	int ec = gitL(s, out go, stdin);
	bool ok = ec == 0;
	if (!ok) go = $"[{ec}] {go}";
	if (!ok && notError != null) ok = notError(go);
	if (!go.NE()) if (gp == GP.All || (!ok && gp == GP.Error)) _Print(go, !ok);
	return ok;
}

//`git(s, GP.All, null, stdin)`
bool gita(string s, string stdin = null) => git(s, GP.All, null, stdin);

//`git(s, GP.Silent, null, stdin)`
bool gits(string s, string stdin = null) => git(s, GP.Silent, null, stdin);

static void _Print(string s, bool error) {
	var color = error ? "FF3300" : "206000";
	print.it($"<><c #{color}><_>{s}</_><>");
}

bool _EnsureMain() {
	if (!gits("branch --show-current")) return false;
	if (go != "main") {
		if (!dialog.showOkCancel("Switch to main?", $"Can work only with the main branch.\nCurrent branch is '{go}'.")) return false;
		if (!git("switch main")) return false;
	}
	return true;
}

bool _GetStatus(out GS can) {
	can = default;
	if (!git("status")) return false;
	can.Add = go.Contains("(use \"git add");
	can.Commit = can.Add || go.Contains("\nChanges to be committed:");
	can.Push = can.Commit || !go.Contains("\nYour branch is up to date with");
	return true;
}

//Calls 'git status' and prints go translated to human language.
void _PrintStatus() {
	if (git("status")) _Print(_TranslateStatusText(go), false);
}

static string _TranslateStatusText(string s) {
	s = s.RxReplace(@"\R  \(use ""git .+", "");
	
	List<string> a = new();
	_Files("Changes not staged for commit:");
	_Files("Changes to be committed:");
	_Files("Untracked files:", true);
	if (a.Count > 0) s = s.RxReplace(@"(\n`)+", "\n\nChanges to be committed:\n    " + string.Join("\n    ", a));
	
	void _Files(string line1, bool untracked = false) {
		s = s.RxReplace($@"\R\R{line1}((?:\R\h+.+)+)", m => {
			var e = m[1].Value.RxFindAll(@"\R\h+\K.+", 0);
			if (untracked) e = e.Select(o => "new file:   " + o);
			a.AddRange(e.OrderBy(o => o));
			return "\n`";
		}, 1);
	}
	
	s = s.Replace("On branch main\nYour branch", "Branch 'main' (local)");
	s = s.Replace("'origin/main'", "'origin/main' (remote)");
	
	s = s.Replace("""no changes added to commit (use "git add" and/or "git commit -a")""", "");
	s = s.Replace("nothing to commit, working tree clean", "Nothing to commit.");
	
	return s.Trim("\r\n");
}

enum GP {
	/// <summary>Print blue and error. Default.</summary>
	Error,
	/// <summary>Print blue, error and not error.</summary>
	All,
	/// <summary>Print blue only.</summary>
	Blue,
	/// <summary>Print nothing, even blue.</summary>
	Silent
}

record struct GS(bool Add, bool Commit, bool Push);
