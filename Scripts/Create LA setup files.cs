/// Creates LZMA-compressed files containing files for LA setup.
/// Updates hashes in ..\Setup\Hardcoded.cs.
/// Builds the setup project.
/// Opens the setup output folder.

/*/ c ..\@LA setup\Util.cs; c ..\@LA setup\api.cs; /*/

#define CREATE_FILES
#define BUILD_SETUP

//Files to add. Wildcard list to match relative paths.
string[] lists = [
"""
Au.*exe
Au.*dll
Au.*json
Au.*xml
AxMSTSCLib.dll
MSTSCLib.dll
Microsoft.Web.WebView2.Core.dll
Microsoft.Web.WebView2.Wpf.dll
NuGet.*.dll

default.exe.manifest
toc.json
toc-ai.yml
xrefmap.yml

64\*.exe
64\*.dll
32\*.exe
32\*.dll

Debugger\*.dll
Debugger\*.exe

Roslyn\*.dll

runtimes\win-*64\native\WebView2Loader.dll

Default\*.xml
Default\Themes\*.csv
Default\Workspace\files\*

Templates\files.xml
Templates\files\*

doc-ai.db
doc-html.db

"""
,
"""
doc.db
icons.db
ref.db
winapi.db

"""
];

bool createZipForVirustotal = true;

print.clear();
print.it($@"<><bc yellowgreen>Creating LZMA files, updating hashes, and building the setup project. Wait until DONE.");

string dir = folders.ThisAppBS;
string dirSetupOutput = folders.ThisAppBS + @"..\Setup\bin\Release\net48\";
string hardcodedFile = folders.ThisApp + @"..\Setup\Hardcoded.cs";
string s = filesystem.loadText(hardcodedFile), s0 = s;

#if CREATE_FILES
foreach (var (i, list) in lists.Index()) {
	var paths = _ListToPaths(list, dir);
	int index = i + 1;
	string listName = index switch { 1 => "main", 2 => "database", _ => null };
	string varLzma = "HashOfLzmaFile" + index, varFiles = "HashOfFiles" + index;
	string lzmaFile = dirSetupOutput + $"offline-{index}.zip.lzma";
	
	if (i > 0) {
		var hashFiles = Util.HashFiles(paths);
		if (_IsHashHardcoded(varFiles, hashFiles) && Util.HashFiles([lzmaFile]) is string s1 && _IsHashHardcoded(varLzma, s1)) {
			print.it($"Skipping {listName} files (up to date)");
			continue;
		}
		_HardcodeHash(varFiles, hashFiles);
	}
	
	print.it($"Compressing {listName} files");
	var zipFile = folders.Temp + $@"LibreAutomate\offline-{index}.zip";
	_CreateZipFile(paths, zipFile, compressed: false);
	_CreateLzmaFile(zipFile, lzmaFile);
	filesystem.delete(zipFile);
	
	string hashLzma = Util.HashFiles([lzmaFile]);
	_HardcodeHash(varLzma, hashLzma);
	
	if (createZipForVirustotal && i is 0) {
		var vtZip = dirSetupOutput + $"vt-{index}.zip";
		_CreateZipFile(paths, vtZip, compressed: true);
		//run.selectInExplorer(vtZip);
	}
}

if (s != s0) filesystem.saveText(hardcodedFile, s);
#endif

#if BUILD_SETUP
print.it("Building the setup project");
var proj = pathname.normalize(folders.ThisApp + @"..\Setup\LA setup.csproj");
if (0 != run.console("dotnet.exe", $@"build ""{proj}"" --configuration Release --nologo --verbosity quiet")) return;
500.ms();
run.selectInExplorer(dirSetupOutput + "LA-setup.exe");
#endif

print.it("DONE");


bool _IsHashHardcoded(string variable, string hash) => s.Contains($@" {variable} = ""{hash}""");

void _HardcodeHash(string variable, string hash) {
	if (s.RxReplace($@" {variable} = ""\K[^""]*", hash, out s, 1) != 1) throw new AuException();
}

static void _CreateZipFile(List<string> paths, string zipFile, bool compressed) {
	using var tf = new TempFile();
	File.WriteAllLines(tf, paths);
	filesystem.delete(zipFile);
	int r = run.console(out var o, folders.ThisAppBS + @"32\7za.exe", $"a {(compressed ? "" : "-mx=0")} {zipFile} @{tf}");
	if (r != 0) throw new Exception(o);
}

static void _CreateLzmaFile(string zipFile, string lzmaFile) {
	string lzmaExe = folders.ThisAppBS + @"..\Setup\LZMA\lzma.exe"; //from the 7-zip LZMA SDK (https://www.7-zip.org/sdk.html), lzma2301\bin. LZMA encoding is not supported by 7za.exe and other exes.
	int r = run.console(out var o, lzmaExe, $"e \"{zipFile}\" \"{lzmaFile}\" -mt3"); //tested: -mt with any CPU number makes faster like 72 -> 43 s
	if (r != 0) throw new Exception(o);
}

static List<string> _ListToPaths(string list, string dir) {
	List<string> r = [];
	var aLines = list.Lines(noEmpty: true);
	var aFound = new bool[aLines.Length];
	
	var a1 = filesystem.enumFiles(dir, flags: FEFlags.AllDescendants);
	foreach (var f in a1) {
		var rel = f.FullPath[dir.Length..];
		if (rel.Starts(true, @"Git\", @"SDK\") > 0) continue;
		//print.it(rel);
		bool ignore = true; int i = -1;
		foreach (var v in aLines) {
			i++;
			if (v.Starts("//")) continue;
			if (rel.Like(v, true)) { ignore = false; aFound[i] = true; break; }
		}
		if (ignore) {
			//print.it(rel);
			continue;
		}
		//print.it(rel);
		r.Add(rel);
	}
	
	//all specified files exist?
	foreach (var (i, found) in aFound.Index()) {
		if (!found && !aLines[i].Starts("//")) throw new Exception("Missing file: " + aLines[i]);
	}
	
	return r;
}
