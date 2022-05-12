using System.Windows;
using System.Windows.Controls;
using System.Linq;
using Au.Controls;
using Au.Tools;

class DIcons : KDialogWindow {
	/// <param name="fileIcon">Called from file Properties dialog. <i>find</i> is null or full icon name.</param>
	public static void ZShow(bool fileIcon = false, string find = null) {
		if (s_dialog == null) {
			s_dialog = new(expandFileIcon: fileIcon, randomizeColors: find == null);
			s_dialog.Show();
		} else {
			s_dialog.Hwnd().ActivateL(true);
		}
		if (find != null) s_dialog._tName.Text = find;
	}
	static DIcons s_dialog;

	protected override void OnClosed(EventArgs e) {
		s_dialog = null;
		base.OnClosed(e);
	}

	enum _Action {
		FileIcon = 1,
		MenuIcon,
		InsertXamlVar,
		InsertXamlField,
		CopyName,
		CopyXaml,
		ExportXaml,
		ExportIcon,
	}

	List<_Item> _a;
	string _color = "#000000";
	Random _random;
	int _dpi;
	//bool _withCollection;
	TextBox _tName;

	DIcons(bool expandFileIcon, bool randomizeColors) {
		Title = "Icons";
		Owner = App.Wmain;
		ShowInTaskbar = false;

		var b = new wpfBuilder(this).WinSize(600, 600);
		b.Columns(-1, 0);

		//left - edit control and tree view
		b.Row(-1).StartDock();
		b.Add(out _tName).Tooltip(@"Search.
Part of icon name, or wildcard expression.
Examples: part, Part (match case), start*, *end, **rc regex case-sensitive.
Can be Pack.Icon, like Modern.List.").Dock(Dock.Top);
		//b.Focus(); //currently cannot use this because of WPF tooltip bugs
		b.xAddInBorder(out KTreeView tv); //tv.SingleClickActivate = true;
		b.End();

		//right - color picker, buttons, etc
		b.StartGrid().Columns(-1);
		b.Add(out KColorPicker colors);
		colors.ColorChanged += color => {
			_random = null;
			_color = _ColorToString(color);
			tv.Redraw();
		};
		b.StartStack();

		TextBox randFromTo = null, iconSizes = null;

		b.AddButton("Randomize colors", _ => _RandomizeColors());
		b.Add("L %", out randFromTo, "30-70").Width(50);
		b.End();
		b.AddSeparator().Margin("B20");

		//rejected: double-clicking an icon clicks the last clicked button. Unclear and not so useful.
		//_Action lastAction = 0;
		tv.ItemActivated += (o, e) => {
			//	switch (lastAction) {
			//	case 0: break;
			//	case _Action.FileIcon: _SetIcon(tv); break;
			//	default: _InsertCodeOrExport(tv, lastAction); break;
			//	}
			_InsertCodeOrExport(tv, _Action.MenuIcon);
		};

		b.StartStack<Expander>(out var exp1, "Set icon of selected files");
		b.AddButton(out var bThis, "This", _ => { _SetIcon(tv); /*lastAction = _Action.FileIcon;*/ }).Width(70).Disabled();
		b.AddButton("Default", _ => _SetIcon(null)).Width(70);
		//b.AddButton("Random", null).Width(70); //idea: set random icons for multiple selected files. Probably too crazy.
		b.AddButton("Show current", _ => _tName.Text = FilesModel.TreeControl.SelectedItems.FirstOrDefault()?.CustomIconName).Margin("L20");
		b.End();
		if (expandFileIcon) exp1.IsExpanded = true;

		b.StartGrid<Expander>("Insert code for menu/toolbar/etc icon");
		b.R.Add<Label>("Set icon of: ");
		b.StartStack();
		b.AddButton(out var bMenuItem, "Menu or toolbar item", _ => _InsertCodeOrExport(tv, _Action.MenuIcon)).Disabled()
			.Tooltip("To assign the selected icon to a toolbar button or menu item,\nin the code editor click its line (anywhere except action code)\nand then click this button. Or double-click an icon.");
		b.End();
		b.R.Add<Label>("Insert line: ");
		b.StartStack();
		b.AddButton(out var bCodeVar, "Variable = XAML", _ => _InsertCodeOrExport(tv, _Action.InsertXamlVar)).Disabled();
		b.AddButton(out var bCodeField, "Field = XAML", _ => _InsertCodeOrExport(tv, _Action.InsertXamlField)).Disabled();
		b.End();
		b.R.Add<Label>("Copy text: ");
		b.StartStack();
		b.AddButton(out var bCodeName, "Name", _ => _InsertCodeOrExport(tv, _Action.CopyName)).Width(70).Disabled()
			.Tooltip("Shorter string than XAML.\nCan be used with custom menus and toolbars,\neditor menus and toolbars (edit Commands.xml),\nscript.editor.GetIcon, IconImageCache, ImageUtil,\noutput tag <image>.");
		b.AddButton(out var bCodeXaml, "XAML", _ => _InsertCodeOrExport(tv, _Action.CopyXaml)).Width(70).Disabled();
		b.End();
		//b.Add<Label>("Tip: double-clicking an icon clicks the same button.");
		b.End();

		b.StartStack<Expander>("Export to current workspace folder");
		b.AddButton(out var bExportXaml, ".xaml", _ => _InsertCodeOrExport(tv, _Action.ExportXaml)).Width(70).Disabled();
		b.AddButton(out var bExportIco, ".ico", _ => _InsertCodeOrExport(tv, _Action.ExportIcon)).Width(70).Disabled();
		b.Add("sizes", out iconSizes, "16,24,32,48,64").Width(100);
		b.End();

		b.StartStack<Expander>("Other actions");
		b.AddButton("Clear program's icon cache", _ => IconImageCache.Common.Clear(redrawWindows: true));
		//SHOULDDO: clear when app version changes.
		b.End();

		//b.StartGrid<Expander>("List display options");
		////b.Add("Background", out ComboBox cBackground).Items("Default|Control|White|Black)");
		////cBackground.SelectionChanged += (o, e) => _ChangeBackground();
		//b.Add(out KCheckBox cCollection, "Collection");
		//cCollection.CheckChanged += (_, _) => {
		//	_withCollection = cCollection.IsChecked == true;
		//	tv.Redraw();
		//};
		//b.End();

		b.Row(-1);
		b.R.Add<TextBlock>().Align("R").Text("Thanks to ", "<a>MahApps.Metro.IconPacks", new Action(() => run.it("https://github.com/MahApps/MahApps.Metro.IconPacks")));
		b.End();

		b.End();

		b.Loaded += () => {
			_dpi = Dpi.OfWindow(this);
			_OpenDB();

			_a = new(30000);
			foreach (var (table, _) in s_tables) {
				using var stat = s_db.Statement("SELECT name FROM " + table);
				while (stat.Step()) {
					var k = new _Item(table, stat.GetText(0));
					//var s = _ColorName(k); if (s.Length < 20 || s.Length > 60) print.it(s.Length, s);
					_a.Add(k);
				}
			}
			_a.Sort((a, b) => string.Compare(a._name, b._name, StringComparison.OrdinalIgnoreCase));
			if (randomizeColors) _RandomizeColors();
			tv.SetItems(_a);
		};

		_tName.TextChanged += (_, _) => {
			string name = _tName.Text, table = null;
			Func<_Item, bool> f = null;
			bool select = false;
			if (!name.NE()) {
				if (select = name.RxMatch(@"^\*(\w+)\.(\w+) #(\w+)$", out var m)) { //full name with * and #color
					table = m[1].Value;
					name = m[2].Value;
					f = o => o._name == name && o._table == table;
					colors.Color = m[3].Value.ToInt(0, STIFlags.IsHexWithout0x);
				} else {
					if (name.RxMatch(@"^(\w+)\.(.+)", out m)) { table = m[1].Value; name = m[2].Value; }
					wildex wild = null;
					StringComparison comp = StringComparison.OrdinalIgnoreCase;
					bool matchCase = name.RxIsMatch("[A-Z]");
					if (wildex.hasWildcardChars(name)) {
						try { wild = new wildex(name, matchCase && !name.Starts("**")); }
						catch { name = null; }
					} else if (matchCase) comp = StringComparison.Ordinal;

					if (name != null) f = o => (table == null || o._table.Eqi(table)) && (wild?.Match(o._name) ?? o._name.Contains(name, comp));
				}
			}
			var e = f == null ? _a : _a.Where(f);
			tv.SetItems(e);
			if (select && (select = e.Count() == 1)) tv.Select(0);
			_EnableControls(select);
		};

		tv.SelectedSingle += (o, i) => {
			_EnableControls(true);
			//var k = _a[i];
			//if(GetIconFromBigDB(k._table, k._name, _ItemColor(k), out var xaml)) {
			//	print.it(xaml);
			//}
		};

		b.WinSaved(App.Settings.wndpos.icons, o => App.Settings.wndpos.icons = o);

		void _EnableControls(bool enable) {
			bThis.IsEnabled = enable;
			bMenuItem.IsEnabled = enable;
			bCodeVar.IsEnabled = enable;
			bCodeField.IsEnabled = enable;
			bCodeXaml.IsEnabled = enable;
			bCodeName.IsEnabled = enable;
			bExportXaml.IsEnabled = enable;
			bExportIco.IsEnabled = enable;
		}

		void _SetIcon(KTreeView tv) {
			string icon = tv?.SelectedItem is _Item k ? _ColorName(k) : null;
			foreach (var v in FilesModel.TreeControl.SelectedItems) {
				v.CustomIconName = icon;
			}
		}

		void _InsertCodeOrExport(KTreeView tv, _Action what) {
			//lastAction = what;
			if (tv.SelectedItem is not _Item k) return;
			string code = null;
			if (what == _Action.MenuIcon) {
				InsertCode.SetMenuToolbarItemIcon(_ColorName(k));
			} else if (what == _Action.CopyName) {
				code = _ColorName(k);
			} else if (GetIconFromBigDB(k._table, k._name, _ItemColor(k), out var xaml)) {
				xaml = xaml.Replace('\"', '\'').RxReplace(@"\R\s*", "");
				switch (what) {
				case _Action.InsertXamlVar: code = $"string icon{k._name} = \"{xaml}\";"; break;
				case _Action.InsertXamlField: code = $"public const string {k._name} = \"{xaml}\";"; break;
				case _Action.CopyXaml: code = xaml; break;
				case _Action.ExportXaml: _Export(false); break;
				case _Action.ExportIcon: _Export(true); break;
				}

				void _Export(bool ico) {
					//App.Model.New
					var cf = App.Model.CurrentFile.Parent; if (cf == null) return;
					var path = $"{cf.FilePath}\\{k._name}{(ico ? ".ico" : ".xaml")}";
					//CONSIDER: if path exists, show dialog
					if (ico) {
						var sizes = iconSizes.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(o => o.ToInt()).ToArray();
						KImageUtil.XamlImageToIconFile(path, xaml, sizes);
					} else {
						filesystem.saveText(path, xaml);
					}
					var fn = App.Model.ImportFromWorkspaceFolder(path, cf, FNPosition.Inside);
					if (fn == null) print.it("failed");
					else print.it($"<>Icon exported to <open>{fn.ItemPath}<>");
				}
			}

			if (code != null) {
				if (what is _Action.CopyName or _Action.CopyXaml) clipboard.text = code;
				else if (what is _Action.InsertXamlVar or _Action.InsertXamlField) InsertCode.Statements(code);
				else InsertCode.TextSimply(code);
			}
		}

		void _RandomizeColors() {
			_random = new();
			//perf.first();
			//if (keys.isScrollLock) { //generate random HLS. Colors look not as good, maybe because the green-yellow range is too narrow and red-purple range too wide.
			//	foreach (var v in _a) {
			//		int L = _random.Next(40, 200);
			//		double k = _random.NextDouble() * 15d; int S = 240 - (int)(k * k); //print.it(S); //generate less low-saturation colors
			//		if (S < 60 && L > 60) S += 60; //avoid light-gray, because looks like disabled
			//		v._color = ColorInt.FromHLS(_random.Next(0, 240), L, S, false);
			//	}
			//} else {
			int iFrom = 0, iTo = 100; if (randFromTo.Text.RxMatch(@"^(\d+) *- *(\d+)", out var m)) { iFrom = m[1].Value.ToInt(); iTo = m[2].Value.ToInt(); }
			float briFrom = Math.Clamp(iFrom / 100f, 0f, 0.9f), briTo = Math.Clamp(iTo / 100f, briFrom + 0.05f, 1f);
			int middleL = ((briTo + briFrom) * 120f).ToInt();
			foreach (var v in _a) {
				int c = _random.Next(0, 0xffffff);
				var (H, L, S) = ColorInt.ToHLS(c, false);
				if (S < 60 && L > 60) c = ColorInt.FromHLS(H, L, S += 60, false); //avoid light-gray, because looks like disabled
				var bri = ColorInt.GetPerceivedBrightness(c, false);
				if (bri < briFrom || bri > briTo) c = ColorInt.FromHLS(H, middleL, S, false);
				v._color = c;
			}
			//}
			//perf.nw(); //4 ms
			tv.Redraw();
		}

		//void _ChangeBackground() {
		//	int i = cBackground.SelectedIndex;
		//	tv.currently does not support custom background color. We could custom-draw, but never mind.
		//}
	}

	static string _ColorToString(int c) => "#" + c.ToS("X6");

	string _ItemColor(_Item k) => _random == null ? _color : _ColorToString(k._color);

	string _ColorName(_Item k) => "*" + k._table + "." + k._name + " " + _ItemColor(k);

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
		_dpi = newDpi.PixelsPerInchX.ToInt();
		base.OnDpiChanged(oldDpi, newDpi);
	}

	class _Item : ITreeViewItem {
		public string _table, _name;
		public int _color;

		public _Item(string table, string name) {
			_table = table; _name = name;
		}

		//string ITreeViewItem.DisplayText => s_dialog._withCollection ? (_name + new string(' ', Math.Max(8, 40 - _name.Length * 2)) + "(" + _table + ")") : _name;
		string ITreeViewItem.DisplayText => _name + new string(' ', Math.Max(8, 40 - _name.Length * 2)) + "(" + _table + ")";

		System.Drawing.Bitmap ITreeViewItem.Image {
			//note: don't store UIElement or Bitmap. They can use hundreds MB of memory and it does not make faster/better. Let GC dispose unused objects asap.
			get {
				try {
					//using var p1 = perf.local();
					if (GetIconFromBigDB(_table, _name, s_dialog._ItemColor(this), out string xaml)) {
						//p1.Next('d');
						return ImageUtil.LoadGdipBitmapFromXaml(xaml, s_dialog._dpi, (16, 16));
					}
				}
				catch (Exception ex) { Debug_.Print(ex.ToStringWithoutStack()); }
				return null;
			}
		}
	}

	static sqlite s_db;
	static List<(string table, string templ)> s_tables; //~30

	static void _OpenDB() {
		if (s_db == null) {
			var db = s_db = new sqlite(folders.ThisAppBS + "icons.db", SLFlags.SQLITE_OPEN_READONLY);
			process.thisProcessExit += _ => db.Dispose();
			s_tables = new();
			using var st = s_db.Statement("SELECT * FROM _tables");
			while (st.Step()) {
				s_tables.Add((st.GetText(0), st.GetText(1)));
			}
		}
	}

	/// <exception cref="Exception"></exception>
	public static bool GetIconFromBigDB(string table, string name, string color, out string xaml) {
		xaml = null;
		string templ = null; foreach (var v in s_tables) if (v.table == table) { templ = v.templ; break; }
		if (templ == null) return false;
		if (!s_db.Get(out string data, $"SELECT data FROM {table} WHERE name='{name}'")) return false;

		int i = templ.Find(" Data=\"{x:Null}\""); if (i < 0) return false;
		templ = templ.ReplaceAt(i + 7, 8, data);

		if (0 == templ.RxReplace(@"(?:Fill|Stroke)=""\K[^""]+", color, out templ)) return false;

		if (templ.Contains("\"{")) return false;
		//print.it(templ);
		//xaml = templ;
		xaml = $@"<Viewbox Width='16' Height='16' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>{templ}</Viewbox>";
		return true;
	}

	/// <param name="icon">Icon name, like "*Pack.Icon color", where color is like #RRGGBB or color name. Can be null.</param>
	/// <exception cref="Exception"></exception>
	public static bool GetIconFromBigDB(string icon, out string xaml) {
		xaml = null;
		if (icon == null || !icon.Starts('*')) return false;
		int i = icon.IndexOf('.'); if (i < 0) return false;
		int j = icon.IndexOf(' ', i + 1); if (j < 0) return false;
		_OpenDB();
		return GetIconFromBigDB(icon[1..i], icon[++i..j], icon[++j..], out xaml);
	}

#if true
	/// <param name="icon">Icon name, like "*Pack.Icon color", where color is like #RRGGBB or color name. Can be null.</param>
	public static bool TryGetIconFromBigDB(string icon, out string xaml) {
		//using var p1 = perf.local();
		try { return GetIconFromBigDB(icon, out xaml); }
		catch (Exception e1) { Debug_.Print(e1.ToStringWithoutStack()); }
		xaml = null;
		return false;
	}
	//This version gets data directly from the big DB. Slightly slower but simpler.
	//Maybe not good to use an 18 MB DB directly, but I didn't notice something bad.
	//note: the big DB must have PRIMARY KEY. Don't need it with other version.
#else
	//This (outdated) version stores XAML of used icons in a small DB file. When missing, gets from the big DB and copies to the small.
	//Has advantages and disadvantages.
	//Faster 2 times (when the small DB is WAL). But both versions much faster than converting XAML to GDI+ bitmap. Same memory usage.
	//shoulddo: when deleting a file, delete its icon from the small DB if not used by other files.
	public static bool TryGetIconFromBigDB(string icon, out string xaml) {
		xaml = null;
		//using var p1 = perf.local();
		try {
			if(s_iconsDB == null) {
				var dbFile = folders.ThisAppDataLocal + "icons.db"; //shoulddo: if portable, create in workspace folder
				var db = s_iconsDB = new(dbFile, sql: "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS icons (icon TEXT PRIMARY KEY, xaml TEXT)");
				process.thisProcessExit += _ => db.Dispose();
			}
			if (!s_iconsDB.Get(out xaml, "SELECT xaml FROM icons WHERE icon=?", icon)) {
			//if (!s_iconsDB.Get(out xaml, $"SELECT xaml FROM icons WHERE icon='{icon}'")) { //same speed. With same prepared statement same.
				if (!Au.Tools.DIcons.GetIconFromBigDB(icon, out xaml)) return false;
				s_iconsDB.Execute("INSERT INTO icons VALUES (?, ?)", icon, xaml);
			}
			xaml = $@"<Viewbox Width='16' Height='16' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>{xaml}</Viewbox>";
			return true;
		}
		catch (Exception e1) { Debug_.Print(e1.ToStringWithoutStack()); }
		return false;
	}
	static sqlite s_iconsDB;
#endif

	public static string GetIconString(string s, EGetIcon what) {
		if (what != EGetIcon.IconNameToXaml) s = App.Model.Find(s, silent: true)?.ImageSource;
		if (what != EGetIcon.PathToIconName && s != null) TryGetIconFromBigDB(s, out s);
		return s;
	}
}
