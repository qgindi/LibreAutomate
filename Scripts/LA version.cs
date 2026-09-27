#define DONE

print.clear();

var solutionDirBS = @"C:\code\au\";

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

//modify global2.cs
{
	var file = solutionDirBS + @"Au\resources\global2.cs";
	var s1 = filesystem.loadText(file);
	if (0 == s1.RxReplace(@"(?m)^\tpublic const string Version = ""\K[\d\.]+", sVer, out s1, 1)) throw null; //change Au_.Version
	filesystem.saveText(file, s1);
}

//modify resource scripts of C++ projects
{
	var file = solutionDirBS + @"Cpp\Cpp.rc";
	var s1 = filesystem.loadText(file);
	if (2 != s1.RxReplace(@"VERSION \K[\d,]+", $"{v.Major},{v.Minor},{v.Build},0", out s1, 2)) throw null;
	if (2 != s1.RxReplace("""Version", "\K[\d\.]+""", $"{sVer}.0", out s1, 2)) throw null;
	filesystem.saveText(file, s1);
}

print.it("DONE");
