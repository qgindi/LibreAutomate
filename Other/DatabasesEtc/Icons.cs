//In VS set this as startup project, and run. It creates file "icons-new.db" in "_" dir.
//If everything OK: exit LA, delete "icons.db", rename "icons-new.db" -> "icons.db", run LA.

#define DB //undefine when debugging, to skip database code

using System.Collections;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
//using MahApps.Metro.IconPacks;

static class Icons {
	public static void CreateDB() {
		new Application();

#if DB
		string dbFile = Program.c_outputDirBS + "icons-new.db";
		filesystem.delete(dbFile);

		using var d = new sqlite(dbFile);
		//using var d = new sqlite(dbFile, sql: "PRAGMA journal_mode=WAL"); //no. Does not make select faster.
		using var trans = d.Transaction();
		d.Execute("CREATE TABLE _tables (name TEXT COLLATE NOCASE, template TEXT)");
#endif

		Dictionary<string, HashSet<string>> dict = new(StringComparer.OrdinalIgnoreCase);
		//var duplData = new Dictionary<string, string>(); int nDupl = 0;

		var alc = AssemblyLoadContext.Default;
		var asmCore = alc.LoadFromAssemblyPath(folders.ThisApp + "MahApps.Metro.IconPacks.Core.dll");
		var factoryGen = asmCore.GetType("MahApps.Metro.IconPacks.PackIconDataFactory`1");

		int nTables = 0, nIcons = 0, nSkipped = 0;
		foreach (var dll in Directory.EnumerateFiles(folders.ThisApp, "MahApps.Metro.IconPacks.?*.dll")) {
			if (dll.Ends(".Core.dll", true)) continue;
			//print.it(dll);
			var asm = alc.LoadFromAssemblyPath(dll);
			foreach (var ty in asm.ExportedTypes.Where(o => o.Name.Like("PackIcon*Kind"))) {
				var table = ty.Name[8..^4];
				var templ = _GetTemplate(asm);
				//print.it(table, templ);
				//return;
				nTables++;

#if DB
				d.Execute("INSERT INTO _tables VALUES (?, ?)", table, templ);
				d.Execute($"CREATE TABLE {table} (name TEXT PRIMARY KEY COLLATE NOCASE, data TEXT)");
				using var statInsert = d.Statement($"INSERT INTO {table} VALUES (?, ?)");
#endif

				var hNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				dict.Add(table, hNames);

				var factory = factoryGen.MakeGenericType(ty);
				foreach (DictionaryEntry k in factory.GetMethod("Create").Invoke(null, null) as IDictionary) {
					string name = k.Key.ToString(), data = k.Value.ToString();
					if (data.NE()) continue; //icon "None"
					if (data.Length > 10000) {
						//print.it(name, data.Length);
						nSkipped++;
						continue; //nothing very useful
					}

					//if (!duplData.TryAdd(data, table + "." + name)) {
					//	//1.2%. All from same assemblies. Often similar name, but often different.
					//	nDupl++;
					//	print.it("DUPL DATA", duplData[data], table + "." + name);
					//	continue;
					//}

					if (!hNames.Add(name)) { //duplicate name like Snowflake and SnowFlake
											 //print.it("DUPL NAME", table + "." + name);
						continue;
					}

					//print.it(table, name);

#if DB
					statInsert.Bind(1, name).Bind(2, data);
					statInsert.Step();
					statInsert.Reset();
#endif
					nIcons++;
				}
			}
		}

#if DB
		int nMissing = _AddMissingFromOldDB(d, dict);

		trans.Commit();
		d.Execute("VACUUM");
#else
		int nMissing = 0;
#endif

		print.it($"Done. {nTables} tables, {nIcons} icons, skipped {nSkipped}. Also added {nMissing} missing old icons."/*, $"{nDupl} duplicate"*/); //29 tables, 25877 icons, skipped 37
	}

	static string _GetTemplate(Assembly asm) {
		var asmname = asm.GetName().Name;
		int i = asmname.LastIndexOf('.') + 1;
		string rn = "PackIcon" + asmname[i..];
		var rd = new ResourceDictionary() { Source = new Uri($@"pack://application:,,,/{asmname};component/themes/{rn.Lower()}.xaml") };
		//print.it(rd);
		var template = rd["MahApps.Templates." + rn] as ControlTemplate;
		string xaml = XamlWriter.Save(template);
		xaml = xaml.RxReplace(@" xmlns(?::\w+)?="".+?""", "");
		var x = XElement.Parse(xaml);
		x = x.Descendants("Path").First();
		return x.ToString();
	}

#if DB
	//In new IconPacks versions some icons are renamed. For backward compatibility we need icons with old names too.
	static int _AddMissingFromOldDB(sqlite d, Dictionary<string, HashSet<string>> dict) {
		int nMissing = 0;
		using var dOld = new sqlite(Program.c_outputDirBS + "icons.db", SLFlags.SQLITE_OPEN_READONLY);
		using var stTables = dOld.Statement("SELECT name FROM _tables");
		while (stTables.Step()) {
			var table = stTables.GetText(0);
			var hsNamesNew = dict[table];
			using var statInsert = d.Statement($"INSERT INTO {table} VALUES (?, ?)");
			using var stNames = dOld.Statement($"SELECT * FROM {table}");
			while (stNames.Step()) {
				var name = stNames.GetText(0);
				if (!hsNamesNew.Contains(name)) {
					//print.it(table, name);
					nMissing++;
					statInsert.Bind(1, name).Bind(2, stNames.GetText(1));
					statInsert.Step();
					statInsert.Reset();
				}
			}
		}
		return nMissing;
	}
#endif
}
