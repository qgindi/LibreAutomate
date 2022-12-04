/// <summary>
/// Called from initEditor.cs on home PC.
/// </summary>

/*/ role editorExtension; define TEST; testInternal Au.Editor; r Au.Editor.dll; /*/

#if TEST
CookbookDb.Create(true);
#endif

class CookbookDb {
	public static void Create(bool createAlways = false) {
		if (createAlways) print.clear();
		var dir = folders.Editor + "Cookbook";
		var file = folders.Editor + "cookbook.db";
		if (!createAlways && filesystem.getProperties(dir, out var pd) && filesystem.getProperties(file, out var pf) && pf.LastWriteTimeUtc >= pd.LastWriteTimeUtc) return;
		
		DebugTraceListener.Setup(false);
		
		bool reload = Panels.Cookbook.UnloadLoad(false);
		filesystem.delete(file);
		
		using var db = new sqlite(file);
		db.Execute("CREATE TABLE files(name TEXT, data TEXT)");
		using (var trans = db.Transaction()) {
			using (var p = db.Statement("INSERT INTO files VALUES(?, ?)")) {
				foreach (var f in filesystem.enumerate(dir, FEFlags.AllDescendants | FEFlags.OnlyFiles, dirFilter: o => o.Name[0] == '-' ? 0 : 2)) {
					var name = f.Name;
					if (name[0] == '-') continue;
					int ftype = name.Ends(true, ".cs", ".xml"); if (ftype == 0) continue;
					//print.it(name);
					var s = filesystem.loadText(f.FullPath);
					
					//this is to avoid AV false positives.
					//	Previously the PowerShell recipe (.cs file) triggered 7 FP in virustotal.com. Did not trigger when in database, but anyway.
					var s1 = s;
					s = s.RxReplace(@"[A-Z][a-z]+", "A$0");
					//print.it($"<><lc green>{name}<>");
					//print.it(s);
					Debug.Assert(s.RxReplace(@"A([A-Z][a-z]+)", "$1") == s1);
					
					if (ftype == 1) name = name[..^3];
					p.Reset().Bind(1, name).Bind(2, s).Step();
				}
			}
			trans.Commit();
		}
		
		print.it("Info: updated cookbook.db");
		if (reload) Panels.Cookbook.UnloadLoad(true);
	}
}
