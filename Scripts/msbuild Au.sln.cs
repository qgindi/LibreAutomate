print.clear();
var solDir = @"C:\Test\au-clone";

_Clone();

Environment.CurrentDirectory = solDir;
Environment.SetEnvironmentVariable("NO_EXIT_EDITOR", "true");

//int r = run.console("dotnet", $"""build {solDir}\Au.sln -c Release"""); //cannot build C++ projects

string msbuild = folders.ProgramFiles + @"Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe";
int r = run.console(msbuild, $"""{solDir}\Au.sln -t:restore -t:build -p:Configuration=Release -verbosity:minimal""");
print.it(r);


void _Clone() {
	if (filesystem.exists(solDir)) filesystem.delete(Directory.GetFileSystemEntries(solDir));
	
	if (0 != run.console("git.exe", $"""clone --depth 1 "file:///C:/code/au" "{solDir}" """)) throw new Exception();
	
	//apply changes made after the last commit
	if (0 != run.console(out string s, "git.exe", $"""status --porcelain C:/code/au """)) throw new Exception();
	foreach (var v in s.Lines(noEmpty: true)) {
		var file = v[3..].Trim('"');
		//print.it(v, file);
		string from = @"C:\code\au\" + file, to = solDir + @"\" + file;
		if (v[1] is 'M' or 'A' or '?') filesystem.copy(from, to, FIfExists.Delete);
		else if (v[1] == 'D') filesystem.delete(to);
		else throw new Exception(v); //need to commit
	}
}
