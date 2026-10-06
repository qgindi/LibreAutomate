/// Creates SQLite databases containing reference assemblies (ref.db) and XML doc files (doc.db) of a .NET runtime.
/// Must be installed the .NET SDK for that .NET runtime version. This script gets data from folder 'dotnet\packs'.
/// Run *before* changing .NET major version of C# projects (<TargetFramework>...</TargetFramework>). Cannot run after, because can't compile scripts until the new ref.db is available.
/// Later can run at any time if want to test or update something.
/// Note: also run this when upgraded SDK RC -> final. Eg in .NET 8 SDK final some API changed.
/// 
/// Shows a list dialog. Creates databases for the selected runtime version, with names ref-new.db and doc-new.db, in folder _.
/// To replace old databases, exit LA and rename them to ref.db and doc.db.
/// 
/// The reference assemblies (in ref.db) contain only metadata of public API, not all code like dlls in folder 'dotnet\shared'.
/// 	Why need it when we can load PortableExecutableReference from folder 'dotnet\shared'? Because:
/// 		1. They are big and may add 100 MB of process memory. We need to load all, because cannot know which are actually used in various stages of compilation.
/// 		2. When loading from dll files, Windows Defender makes it as slow as 2.5 s or more, unless the files already are in OS file buffers.
/// 		3. Better compatibility. See https://github.com/dotnet/standard/blob/master/docs/history/evolution-of-design-time-assemblies.md
/// doc.db contains XML documentation files of .NET runtime assemblies.
/// 	Why need it:
/// 		1. Else users would have to download whole .NET SDK. Now need only runtimes.
/// 		2. Parsed XML files can use eg 200 MB of process memory. Now we get doc of a single type/method/etc from database only when need; all other data is not in memory.

/*/ r NuGet.Versioning.dll; /*/
using NuGet.Versioning;

print.clear();
RefAndDoc.Create();

static class RefAndDoc {
	public static void Create() {
		string dirPacks = @"C:\Program Files\dotnet\packs";
		string dirCore = dirPacks / "Microsoft.NETCore.App.Ref";
		List<string> a = [];
		List<SemanticVersion> asv = [];
		var verNow = SemanticVersion.Parse(Environment.Version.ToString());
		foreach (var f in filesystem.enumDirectories(dirCore)) { //for each version
			var s = f.Name;
			var v = SemanticVersion.Parse(s);
			if (v >= verNow) { a.Add(s); asv.Add(v); }
		}
		
		int i = dialog.showList(a, "Create databases for selected .NET runtime", "Showing .NET versions >= the current.\nIf going to upgrade .NET major version, select the new version.\nElse if just testing this script, select any current version.", footer: "Note: These are .NET SDK reference assembly directories. Versions may not match versions of runtime and even SDK.");
		if (--i < 0) return;
		_CreateRefAndDoc(dirPacks, dirCore, a[i], asv[i], false);
		print.it("RefAndDoc.Create done.");
	}
	
	static void _CreateRefAndDoc(string dirPacks, string dirCore, string version, SemanticVersion semv, bool all) {
		string subdir = $@"\ref\net{semv.Major}.{semv.Minor}\";
		
		var dir1 = dirCore / version + subdir;
		if (!Directory.Exists(dir1)) throw new DirectoryNotFoundException("Not found: " + dir1);
		
		//find WindowsDesktop folder of the same version
		string dirDesktop = dirPacks / "Microsoft.WindowsDesktop.App.Ref";
		var dir2 = dirDesktop / version + subdir;
		if (!Directory.Exists(dir2)) throw new DirectoryNotFoundException("Not found: " + dir2);
		//The preview or RC version sometimes is different (like `X.X.X-different`). Then exception. Workaround: rename the version folder of WindowsDesktop.
		
		_CreateRef(folders.Editor + "ref-new.db", dir1, dir2);
		_CreateDoc(folders.Editor + "doc-new.db", dir1, dir2);
	}
	
	static void _CreateRef(string dbFile, string dir1, string dir2) {
		filesystem.delete(dbFile);
		using var d = new sqlite(dbFile);
		using var trans = d.Transaction();
		d.Execute("CREATE TABLE ref (name TEXT PRIMARY KEY, data BLOB)");
		using var statInsert = d.Statement("INSERT OR REPLACE INTO ref VALUES (?, ?)");
		
		_AddDir(dir1, "WindowsBase", "System.Drawing");
		_AddDir(dir2);
		
		trans.Commit();
		d.Execute("VACUUM");
		
		print.it("Created " + dbFile);
		
		void _AddDir(string dir, params string[] skip) {
			foreach (var f in filesystem.enumFiles(dir)) {
				if (!f.Name.Ends(".dll", true)) continue;
				var asmName = f.Name[..^4];
				if (skip.Contains(asmName)) continue;
				_AddFile(asmName, f.FullPath);
				//break;
			}
		}
		
		void _AddFile(string asmName, string asmFile) {
			//print.it(asmName);
			statInsert.Bind(1, asmName);
			statInsert.Bind(2, File.ReadAllBytes(asmFile));
			statInsert.Step();
			statInsert.Reset();
		}
	}
	
	static void _CreateDoc(string dbFile, string dir1, string dir2) {
		filesystem.delete(dbFile);
		using var d = new sqlite(dbFile, sql: "PRAGMA page_size = 8192;"); //8192 makes file smaller by 2-3 MB.
		using var trans = d.Transaction();
		d.Execute("CREATE TABLE doc (name TEXT PRIMARY KEY, xml TEXT)");
		using var statInsert = d.Statement("INSERT INTO doc VALUES (?, ?)");
		using var statDupl = d.Statement("SELECT xml FROM doc WHERE name=?");
		var haveRefs = new List<string>();
		var uniq = new Dictionary<string, string>(); //name -> asmName
		
		//using var textFile = File.CreateText(Path.ChangeExtension(dbFile, "txt")); //test. Compresses almost 2 times better than db.
		
		_AddDir(dir1, ["WindowsBase"]);
		_AddDir(dir2, default, dir1);
		
		statInsert.BindAll(".", string.Join("\n", haveRefs)).Step();
		
		trans.Commit();
		d.Execute("VACUUM");
		
		print.it("Created " + dbFile);
		
		void _AddDir(string dir, ReadOnlySpan<string> skip = default, string coreDir = null) {
			foreach (var f in filesystem.enumFiles(dir)) {
				if (!f.Name.Ends(".xml", true)) continue;
				var asmName = f.Name[..^4];
				if (skip.Contains(asmName)) continue;
				if (!File.Exists(dir + asmName + ".dll")) {
					if (coreDir != null) { //in 10+ SDKs, the desktop dir also contains copies of core xml files. Skip them silently.
						if (new FileInfo(coreDir + f.Name) is { Exists: true } fi && fi.Length == new FileInfo(f.FullPath).Length) continue;
					}
					print.it("<><c 0x808080>" + f.Name + "</c>");
					continue;
				}
				_AddFile(asmName, f.FullPath);
				//break;
			}
		}
		
		void _AddFile(string asmName, string xmlFile) {
			//print.it(asmName);
			haveRefs.Add(asmName);
			var xr = XmlUtil.LoadElem(xmlFile);
			foreach (var e in xr.Descendants("member")) {
				var name = e.Attr("name");
				
				//remove <remarks> and <example>. Does not save much space, because .NET xmls don't have it.
				foreach (var v in e.Descendants("remarks").ToArray()) v.Remove();
				foreach (var v in e.Descendants("example").ToArray()) v.Remove();
				
				using var reader = e.CreateReader();
				reader.MoveToContent();
				var xml = reader.ReadInnerXml();
				//print.it(name, xml);
				
				//textFile.WriteLine(name); textFile.WriteLine(xml); textFile.WriteLine("\f");
				
				if (uniq.TryGetValue(name, out var prevRef)) {
					if (!statDupl.Bind(1, name).Step()) throw new AuException();
					var prev = statDupl.GetText(0);
					if (xml != prev && asmName != "System.Linq") print.it($"<>\t{name} already defined in {prevRef}\r\n<c 0xc000>{prev}</c>\r\n<c 0xff0000>{xml}</c>");
					statDupl.Reset();
				} else {
					statInsert.BindAll(name, xml).Step();
					uniq.Add(name, asmName);
				}
				statInsert.Reset();
			}
		}
	}
}
