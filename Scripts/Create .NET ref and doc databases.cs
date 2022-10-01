CreateRefAndDoc();

/// <summary>
/// Creates SQLite databases containing design-time assemblies (ref.db) and XML documentation files (doc.db) of a .NET runtime. The SDK must be installed.
/// </summary>
/// <remarks>
/// Shows a list dialog.
/// 	If selected All, creates for all runtime versions starting from 6.0, with names ref.version.db (eg ref.6.0.0.db) and doc.version.db, in folders.ThisAppBS.
/// 	Else creates only for the selected runtime version, with names ref.db.new and doc.db.new, in folders.ThisAppBS; next time editor will use them (replace the old files).
/// We ship and at run time load databases of single version, named ref.db and doc.db. In the future should allow to download and use multiple versions.
/// Also this function allows users to create databases from SDKs installed on their PC, but currently this feature is not exposed. Would need to add UI and exception handling.
/// ref.db contains dlls from 'dotnet\packs' folder. They contain only metadata of public API, not all code like dlls in the 'dotnet\shared' folder.
/// 	Why need it when we can load PortableExecutableReference from 'dotnet\shared' folder? Because:
/// 		1. They are big and may add 100 MB of process memory. We need to load all, because cannot know which are actually used in various stages of compilation.
/// 		2. When loading from dll files, Windows Defender makes it as slow as 2.5 s or more, unless the files already are in OS file buffers.
/// 		3. Better compatibility. See https://github.com/dotnet/standard/blob/master/docs/history/evolution-of-design-time-assemblies.md
/// doc.db contains XML documentation files of .NET runtime assemblies. From the same 'dotnet\packs' folder.
/// 	Why need it:
/// 		1. Else users would have to download whole .NET SDK. Now need only runtimes.
/// 		2. Parsed XML files can use eg 200 MB of process memory. Now we get doc of a single type/method/etc from database only when need; all other data is not in memory.
/// 		
/// Need to run this after changing .NET version of C# projects (<TargetFramework>...</TargetFramework>). Also update COREVER2 etc in AppHost.cpp.
/// </remarks>
void CreateRefAndDoc() {
	string dirPacks = pathname.normalize(folders.NetRuntimeBS + @"..\..\..\packs");
	string dirCore = dirPacks + @"\Microsoft.NETCore.App.Ref\";
	var a = new List<string>();
	foreach (var f in filesystem.enumerate(dirCore, FEFlags.UseRawPath)) { //for each version
		if (!f.IsDirectory) continue;
		var s = f.Name;
		int v1 = s.ToInt(0, out int ne), v2 = s.ToInt(ne + 1);
		if (v1 < 6 || (v1 == 6 && v2 < 0)) continue; //must be 6.0 or later
		a.Add(s);
	}
	a.Add("All");
	int i = dialog.showList(a, "Create database", "For runtime", footer: "Note: These are .NET SDK reference assembly directories. Versions may not match versions of runtime and even SDK.") - 1;
	if (i < 0) return;
	int n = a.Count - 1;
	if (i < n) {
		_CreateRefAndDoc(dirPacks, dirCore, a[i], false);
	} else {
		for (i = 0; i < n; i++) _CreateRefAndDoc(dirPacks, dirCore, a[i], true);
	}
	print.it("CreateRefAndDoc done.");
}

void _CreateRefAndDoc(string dirPacks, string dirCore, string version, bool all) {
	string subdirRN = @"\ref\net" + version.RxReplace(@"^\d+\.\d+\K.+", @"\", 1);
	
	var dir1 = dirCore + version + subdirRN;
	if (!filesystem.exists(dir1, true).Directory) throw new DirectoryNotFoundException("Not found: " + dir1);
	
	//find WindowsDesktop folder. Must have same X.X.X version. Preview version may be different.
	bool preview; int i = version.Find("-p", true);
	if (preview = i >= 0) version = version[..(i + 2)];
	string verDesktop = null;
	string dirDesktop = dirPacks + @"\Microsoft.WindowsDesktop.App.Ref\";
	foreach (var f in filesystem.enumerate(dirDesktop, FEFlags.UseRawPath)) { //for each version
		if (!f.IsDirectory) continue;
		var s = f.Name;
		if (preview ? s.Starts(version, true) : s == version) { verDesktop = s; break; }
	}
	if (verDesktop == null) throw new DirectoryNotFoundException("Not found: WindowsDesktop SDK");
	var dir2 = dirDesktop + verDesktop + subdirRN;
	if (!filesystem.exists(dir2, true).Directory) throw new DirectoryNotFoundException("Not found: " + dir2);
	
	string dbRef, dbDoc;
	if (all) {
		dbRef = folders.ThisAppBS + "ref." + version + ".db";
		dbDoc = folders.ThisAppBS + "doc." + version + ".db";
	} else {
		dbRef = folders.ThisAppBS + "ref.db.new";
		dbDoc = folders.ThisAppBS + "doc.db.new";
	}
	_CreateRef(dbRef, dir1, dir2);
	_CreateDoc(dbDoc, dir1, dir2);
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
		foreach (var f in filesystem.enumerate(dir)) {
			if (f.IsDirectory) continue;
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
	
	_AddDir(dir1, "WindowsBase");
	_AddDir(dir2);
	
	statInsert.BindAll(".", string.Join("\n", haveRefs)).Step();
	
	trans.Commit();
	d.Execute("VACUUM");
	
	print.it("Created " + dbFile);
	
	void _AddDir(string dir, params string[] skip) {
		foreach (var f in filesystem.enumerate(dir)) {
			if (f.IsDirectory) continue;
			if (!f.Name.Ends(".xml", true)) continue;
			var asmName = f.Name[..^4];
			if (skip.Contains(asmName)) continue;
			if (!filesystem.exists(dir + asmName + ".dll").File) {
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
