/*/ noWarnings CS8321; /*/
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

//const string menu_Test = "Test";
const string menu_Status = "Git status";
const string menu_Commit = "_Commit (local backup)"; //- *RemixIcon.GitCommitLine #464646|#E0E000
const string menu_Push = "Commit and _push (upload)"; //*Unicons.CloudUpload #3586FF|#E0E000
const string menu_Pull = "P_ull (download and merge)"; //*Unicons.CloudDownload #3586FF|#E0E000
//const string menu_PullAbort = "Abort the last merge";
const string menu_Cmd = "Run cmd"; //- *Material.Console #464646|#E0E000
const string menu_GUI = "Run GitHubDesktop"; //*Codicons.Github #771FB1|#E0E000
const string menu_Folder = "Workspace folder"; //*Material.Folder #EABB00|#E0E000
const string menu_ReloadWS = "Reload workspace"; //*Material.Reload #464646|#E0E000
//const string menu_Compact = "Co_mpact local backups..."; //-
const string menu_Maintenance = "Maintenance..."; //-
//const string menu_GitHub = "GitHub repository";
//const string menu_ = "";

switch (command) {
case "Test": Test(); break;
case menu_Status: Status(); break;
case menu_Commit: Commit(); break;
case menu_Push: Push(); break;
case menu_Pull: Pull(); break;
//case menu_PullAbort: PullAbort(); break;
case menu_Cmd: RunCmd(); break;
case menu_GUI: RunGui(); break;
case menu_Folder: run.itSafe(dir); break;
//case menu_GitHub: run.itSafe(url); break;
case menu_ReloadWS: ReloadWorkspace(); break;
case menu_Maintenance: Maintenance(); break;
}

void Test() {
	print.clear();
	//git("add .");
	//git("commit -m test");
	//message = null;
	//Commit();
	//Push();
	//Pull();
	
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
	//gita("diff");
	//gita("blame files/Script1.cs");
	//gita("""grep --cached --word-regexp "System.IO.Compression" """);
	//gita("""grep --cached "\bSystem\.IO\.Compression\b" """);
	//gita("""grep --cached --perl-regexp "\bSystem\.IO\.Compression\b" """);
	//gita("merge --abort");
	
	//gita("merge --ff-only");
	
	//git("remote update");
	//if (!_GetStatus(out var can)) return;
	//print.it(go);//TODO
	//print.it(can.Add, can.Commit, can.Push);
	
	//print.it(gita("branch -r"));
	
	//gita("remote -v");
	
	//gita(@"check-ignore --verbose .compiled/*");
	//gita(@"check-ignore .compiled/*");
	//gita(@"check-ignore .compiled/* .temp/* .nuget/*/*.csproj");
	//gita(@"status --ignored");
	//gita(@"ls-files -i");
	
	//gita("status --short");
}

bool _GetStatus(out GS can) {
	can = default;
	if (!git("status")) return false;
	can.Add = go.Contains("(use \"git add");
	can.Commit = can.Add || go.Contains("\nChanges to be committed:");
	can.Push = can.Commit || !go.Contains("\nYour branch is up to date with");
	return true;
}

void Status() {
	git("remote update");
	gita("status");
}

bool _Commit(out GS can) {
	if (!_GetStatus(out can)) return false;
	if (can.Commit) {
		var m = message;
		while (m.NE()) if (!dialog.showInput(out m, "Git commit", "Message")) return false;
		m = m.Replace("\"", "''");
		
		if (/*can.Add &&*/ !git("add .")) return false;
		
		//if (!git($"commit -m \"{m}\"", notError: o => o.Like("[1] *\nnothing to commit*"))) return false;
		if (!git($"commit -m \"{m}\"")) return false;
	}
	return true;
}

void Commit() {
	if (!_Commit(out var can)) return;
	print.it(can.Commit ? "==== DONE ====" : "Nothing to commit.");
}

void Push() {
	if (!_Commit(out var can)) return;
	if (!can.Push) { print.it(can.Commit ? "Nothing to push." : "Nothing to commit or push."); return; }
	if (!git("push")) {
		if (!go.Like("[1] *[rejected]*")) return;
		int button = dialog.show("Git push failed", go, "100 Cancel|1 Overwrite cloud\ngit push --force", flags: DFlags.CommandLinks, defaultButton: 100);
		switch (button) {
		case 1: if (!git("push --force")) return; break;
		case 2: break;
		case 3: break;
		case 4: break;
		case 5: break;
		case 10: break;
		case 11: break;
		default: print.it("Git push cancelled."); return;
		}
	}
	print.it("==== DONE ====");
}

void Pull() {
	/*
Algorithm

Run `git fetch`.
Call _GetStatus.
If there is nothing to pull: return.
If there is fork:
	Don't try to merge. It can damage files, eg files.xml (invalid XML, invalid structure, duplicate ids).
	If there are >1 local commit from the common commit, refuse to do anything.
	Else show dialog "1 Pull and overwrite local|2 Force-push and overwrite cloud|10 Cancel".
		1.
			Run `git log` to get hash.
			If there are uncommitted changes, call Commit.
			Run `git revert hash`.
		2. Call Commit() and `git push --force`. Return.
		10. Return.
Else if there are uncommitted changes:
	Show dialog "1 Discard|10 Cancel".
		1. Call Commit() and run `git revert`. Or run `git restore`.
		10. Return.
Run `git merge --ff-only`.
	*/
	
	
	//refuse to pull if there are uncommitted changes
	if (!_GetStatus(out var can)) return;
	var status = go;
	bool stashed = false;
	if (can.Commit) {
		//changes in the .state dir are very likely. They are not important and can/should be dropped. The .toolbars dir is similar.
		//	Now stash. Finally drop if successful, else restore.
		if (gits("status --short")) {
			if (go.RxIsMatch(@"^( *\S+ ""?\.(?:state|toolbars)/.+)(\R(?1))*$")) stashed = gits("stash");
		}
		if (!stashed) {
			print.it("Cannt pull. There are uncommitted local changes that would be lost. Current status:");
			print.it(status);
			return;
		}
	}
	
	bool ok = false;
	try {
		if (!git("fetch")) return;
		
		//note: don't support merging when cannot fast-forward. It can create a mess difficult to resolve.
		if (!git("merge --ff-only")) {
			print.it("This script does not support 'git merge' when cannot fast-forward. Instead use a Git GUI client app (GitHubDesktop, TortoiseGit, etc).");
			return;
		}
		if (go.Starts("Already up to date.")) { print.it("Already up to date."); return; }
		ok = true;
	}
	finally {
		if (stashed) {
			if (ok) gits("stash drop"); else gits("stash pop");
		}
	}
	print.it("==== DONE ====");
	
	//ReloadWorkspace();//TODO
}

//void PullAbort() {
//	git("merge --abort");
//}

//void _Reset() {
//	if (!gits("log --format=\"%H %ch %s\"")) return;
//	print.it(go);
//}

void Maintenance() {
	if (!gita("count-objects")) return;
	
}

void RunCmd() {
	if (filesystem.searchPath("git.exe") == null) Environment.SetEnvironmentVariable("PATH", pathname.getDirectory(gitExe) + ";" + Environment.GetEnvironmentVariable("PATH"));
	run.it(folders.System + "cmd.exe", null, RFlags.InheritAdmin, dir);
	1.s(); //workaround for: occasionally inactive window if miniProgram
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

void ReloadWorkspace() {
	//print.it("==== Reloading workspace ====");
	ScriptEditor.InvokeCommand("Reload_this_workspace", dontWait: true);
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

//Low level.
//Prints nothing. Returns exitcode and gets output. Does not set the go variable.
int gitL(string s, out string so, string stdin = null) {
	using var c = new consoleProcess(gitExe, s, dir);
	if (stdin != null) c.Write(stdin);
	so = c.ReadAllText().Trim("\r\n");
	return c.ExitCode;
}

static void _Print(string s, bool error) {
	var color = error ? "FF3300" : "206000";
	print.it($"<><c #{color}><_>{s}</_><>");
	//var color = error ? "FFE8E8" : "E2FFDB";
	//print.it($"<><lc #{color}><_>{s}</_><>");
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
