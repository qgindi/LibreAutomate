using System;
using Au;
using Au.Types;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
//using MahApps.Metro.IconPacks;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.Loader;
using System.Xml.Linq;

class Program
{
	[STAThread]
	static void Main(string[] args) {
		print.qm2.use = true;
		print.clear();
		new Application();
		CreateDB();

		//var v = new MahApps.Metro.IconPacks.PackIconFileIcons() { Kind = MahApps.Metro.IconPacks.PackIconFileIconsKind.Adobe };
		//var v = new MahApps.Metro.IconPacks.PackIconBootstrapIcons() { Kind = MahApps.Metro.IconPacks.PackIconBootstrapIconsKind.Alarm };
		//print.it(_GetTemplate(v.GetType().Assembly));
		//print.it(v.Data);
		//print.it(v.Flip, v.RotationAngle, v.Spin, v.Background, v.Brush);
		//print.it(v.LayoutTransform.GetValue(ScaleTransform.ScaleYProperty));
		//print.it(XamlWriter.Save(v));
		//print.it(v.Style);

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

	static void CreateDB() {
		string dbFile = @"Q:\app\Au\_\icons.db";
		filesystem.delete(dbFile);

		using var d = new sqlite(dbFile);
		//using var d = new sqlite(dbFile, sql: "PRAGMA journal_mode=WAL"); //no. Does not make select faster.
		using var trans = d.Transaction();
		d.Execute("CREATE TABLE _tables (name TEXT, template TEXT)");

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
				d.Execute($"CREATE TABLE {table} (name TEXT PRIMARY KEY, data TEXT)");
				using var statInsert = d.Statement($"INSERT INTO {table} VALUES (?, ?)");

				foreach (DictionaryEntry k in ty.GetMethod("Create").Invoke(null, null) as IDictionary) {
					string name = k.Key.ToString(), data = k.Value.ToString();
					if (data.NE()) continue; //icon "None"
					if (data.Length > 10000) {
						//print.it(name, data.Length);
						nSkipped++;
						continue; //nothing very useful
					}
					//if (!dupl.TryAdd(data, table + "." + name)) { nDupl++; print.it("DUPL", dupl[data], table + "." + name); /*continue;*/ } //1.2%. All from same assemblies. Often similar name, but often different. Makes DB smaller by 100 KB (from 18 MB).
					nIcons++;
					//print.it(name, data);
					statInsert.Bind(1, name).Bind(2, data);
					statInsert.Step();
					statInsert.Reset();
				}
			}
		}

		trans.Commit();
		d.Execute("VACUUM");

		print.it($"Done. {nTables} tables, {nIcons} icons, skipped {nSkipped}"/*, $"{nDupl} duplicate"*/); //29 tables, 25877 icons, skipped 37
	}
}
