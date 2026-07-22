#define DONE

print.clear();

var solutionDirBS = @"C:\code\au\";
var resourcesDirBS = solutionDirBS + @"Au.Editor\resources\";

#if DONE
if (!dialog.showInput(out string sVer, null, $"""
This will:
- change Au_.Version in global2.cs
- create res files for Au.Editor and Au.Task projects

Version will be changed from {Au_.Version} to:
""", editText: Au_.Version)) return;
#else
var sVer = Au_.Version;
#endif

var v = Version.Parse(sVer);
var year = DateTime.Now.AddMonths(1).Year.ToS();

//modify global2.cs

var global2Cs = solutionDirBS + @"Au\resources\global2.cs";
var s1 = filesystem.loadText(global2Cs);
if (0 == s1.RxReplace(@"(?m)^\tpublic const string Version = ""\K[\d\.]+", sVer, out s1, 1)) throw null; //change Au_.Version
if (0 == s1.RxReplace(@"Copyright 2020-\K[\d]+", year, out s1, 1)) throw null; //change year if need
filesystem.saveText(global2Cs, s1);

//modify LibreAutomate.iss

var iss = solutionDirBS + @"Au.Editor\LibreAutomate.iss";
s1 = filesystem.loadText(iss);
if (0 == s1.RxReplace(@"(?m)^#define MyAppVersion ""\K[\d\.]+", sVer, out s1, 1)) throw null; //change version
filesystem.saveText(iss, s1);

//create res files

_CompileRc(true);
_CompileRc(false);

print.it("DONE");

void _CompileRc(bool editor) {
	var exeFilename = $"Au.{(editor ? "Editor" : "Task")}.exe";
	
	var rc = _IconsAndManifest(editor) + _VersionInfo(exeFilename, editor ? "LibreAutomate" : "LibreAutomate miniProgram");
	//print.it(rc);return;
	
	string resFile = resourcesDirBS + exeFilename + ".res";
	using var rcFile = new TempFile(".rc");
	filesystem.saveText(rcFile, rc, encoding: Encoding.UTF8);
	_RunCL($"""/nologo /fo "{resFile}" "{rcFile}" """);
}

string _IconsAndManifest(bool editor) {
	var s = $"""
LANGUAGE 0, 0

1 24 {resourcesDirBS}Au.manifest


""";
	if (editor) {
		s += $"""
32512 ICON {resourcesDirBS}ico\app.ico
32513 ICON {resourcesDirBS}ico\app_disabled.ico
32514 ICON {resourcesDirBS}ico\PictureInPicture.ico

""";
	} else {
		s += $"""
32512 ICON {resourcesDirBS}ico\Script.ico

""";
	}
	return s;
}

string _VersionInfo(string fileName, string fileDesc) {
	return $$"""

1 VERSIONINFO
FILEVERSION {{v.Major}},{{v.Minor}},{{v.Build}},0
PRODUCTVERSION {{v.Major}},{{v.Minor}},{{v.Build}},0
FILEOS 0x4
FILETYPE 0x1
{
BLOCK "StringFileInfo"
{
	BLOCK "000004b0"
	{
		VALUE "CompanyName", "Gintaras Didžgalvis"
		VALUE "FileDescription", "{{fileDesc}}"
		VALUE "FileVersion", "{{v.ToString()}}"
		VALUE "InternalName", "{{fileName[..^4]}}"
		VALUE "LegalCopyright", "Copyright 2020-{{year}} Gintaras Didžgalvis"
		VALUE "OriginalFilename", "{{fileName}}"
		VALUE "ProductName", "LibreAutomate"
		VALUE "ProductVersion", "{{v.ToString()}}"
	}
}

BLOCK "VarFileInfo"
{
	VALUE "Translation", 0x0000 0x04B0  
}
}

""";
}

void _RunCL(string cl) {
	var rc = Directory.EnumerateDirectories(@"C:\Program Files (x86)\Windows Kits\10\bin", "10.*")
		.MaxBy(o => Version.Parse(Path.GetFileName(o)))
		+ @"\x64\rc.exe";
	int r = run.console(rc, cl);
	if (r == 0) return;
	throw new AuException();
}
