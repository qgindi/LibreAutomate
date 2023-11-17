using System.Collections;
//using MahApps.Metro.IconPacks;
using System.Windows.Markup;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.Loader;
using System.Xml.Linq;

static class Icons {
	public static void CreateDB() {
		new Application();

		string dbFile = Program.c_outputDirBS + "icons.db";
		filesystem.delete(dbFile);

		using var d = new sqlite(dbFile);
		//using var d = new sqlite(dbFile, sql: "PRAGMA journal_mode=WAL"); //no. Does not make select faster.
		using var trans = d.Transaction();
		d.Execute("CREATE TABLE _tables (name TEXT COLLATE NOCASE, template TEXT)");

		var alc = AssemblyLoadContext.Default;

		//var dupl = new Dictionary<string, string>(); int nDupl = 0;

		int nTables = 0, nIcons = 0, nSkipped = 0;
		foreach (var dll in Directory.EnumerateFiles(folders.ThisApp, "MahApps.Metro.IconPacks.?*.dll")) {
			//print.it(dll);
			var asm = alc.LoadFromAssemblyPath(dll);
			foreach (var ty in asm.ExportedTypes.Where(o => o.Name.Like("PackIcon*DataFactory"))) {
				var table = ty.Name[8..^11];
				var templ = _GetTemplate(asm);
				print.it(table, templ);
				//return;
				nTables++;

				d.Execute("INSERT INTO _tables VALUES (?, ?)", table, templ);
				d.Execute($"CREATE TABLE {table} (name TEXT PRIMARY KEY COLLATE NOCASE, data TEXT)");
				using var statInsert = d.Statement($"INSERT INTO {table} VALUES (?, ?)");

				bool bug1 = table == "RemixIcon", bug2 = table == "Unicons";

				foreach (DictionaryEntry k in ty.GetMethod("Create").Invoke(null, null) as IDictionary) {
					string name = k.Key.ToString(), data = k.Value.ToString();
					if (data.NE()) continue; //icon "None"
					if (data.Length > 10000) {
						//print.it(name, data.Length);
						nSkipped++;
						continue; //nothing very useful
					}
					//if (!dupl.TryAdd(data, table + "." + name)) { nDupl++; print.it("DUPL", dupl[data], table + "." + name); /*continue;*/ } //1.2%. All from same assemblies. Often similar name, but often different. Makes DB smaller by 100 KB (from 18 MB).

					//print.it(name, data);
					//print.it(name);
					if (bug1 && name is "BookMarkFill" or "BookMarkLine") continue; //uppercase duplicate of BookmarkFill etc
					if (bug2 && name is "SnowFlake") continue; //uppercase duplicate of Snowflake etc

					statInsert.Bind(1, name).Bind(2, data);
					statInsert.Step();
					statInsert.Reset();
					nIcons++;
				}
			}
		}

		trans.Commit();
		d.Execute("VACUUM");

		print.it($"Done. {nTables} tables, {nIcons} icons, skipped {nSkipped}"/*, $"{nDupl} duplicate"*/); //29 tables, 25877 icons, skipped 37
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
}
