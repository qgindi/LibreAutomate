/// Runs script "Extract doc-ai.db to md files.cs" (unless ran recently).
/// Pushes to https://github.com/qgindi/LA-doc4ai.

if (script.testing) print.clear();

var dir = @"C:\Temp\Au\markdown\";
var readme = dir + "README.md";
bool skipExtract = filesystem.getProperties(readme, out var fp) && fp.LastWriteTimeUtc > DateTime.UtcNow.Add(TimeSpan.FromMinutes(-10));
if (!skipExtract) {
	int ec = script.runWait(@"Extract doc-ai.db to md files.cs");
	if (ec != 0) return;
}

print.it("<><lc yellowgreen>Pushing LA docs md files to GitHub. Wait until DONE.<>");

var git = folders.Editor + @"Git\cmd\git.exe";
if (!filesystem.exists(git)) git = filesystem.searchPath("git.exe") ?? throw new FileNotFoundException("git.exe");

var gitDir = dir + ".git";
filesystem.delete(gitDir);

print.it("Creating temp local Git repo");
Git("init");
Git("remote add origin https://github.com/qgindi/LA-doc4ai.git");

print.it("Git commit");
Git("add .");
Git("commit -m \"updated\"");

print.it("Git push --force");
Git("push --force origin main");

filesystem.delete(gitDir);

print.it("<>DONE: <link>https://github.com/qgindi/LA-doc4ai<>");

void Git(string args) {
	int r = run.console(out string output, git, args, dir);
	if (r != 0) throw new Exception($"git {args}\r\n{output}");
}
