using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Au.Controls;

class DIcons : KDialogWindow {
	public static void ShowSingle(string find = null) {
		var d = ShowSingle(() => new DIcons(randomizeColors: find == null));
		if (find != null) d._tName.Text = find;
	}
	
	enum _Action {
		FileIcon = 1,
		MenuIcon,
		//InsertXamlVar,
		//InsertXamlField,
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
	
	DIcons(bool randomizeColors) {
		InitWinProp("Icons", App.Wmain);
		
		var b = new wpfBuilder(this).WinSize(600, 600);
		b.Columns(-1, 0);
		
		//left - edit control and tree view
		b.Row(-1).StartDock();
		b.Add(out _tName).Tooltip(@"Search.
Part of icon name, or wildcard expression.
Examples: part, Part (match case), start*, *end, **rc regex case-sensitive.
Can be Pack.Icon, like Modern.List.")
			.Dock(Dock.Top).Focus();
		_tName.PreviewMouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) _tName.Clear(); };
		//b.Focus(); //currently cannot use this because of WPF tooltip bugs
		b.xAddInBorder(out KTreeView tv); //tv.SingleClickActivate = true;
		tv.ImageBrush = System.Drawing.Brushes.White;
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
		
		b.StartGrid<Expander>(out var exp1, "Set file icon").Columns(0, 76, 76, -1);
		b.R.Add("File", out ComboBox cbIconOf).Items("Selected file(s)", "Script files", "Class files", "Folders", "Open folders").Span(2);
		b.R.Add<Label>("Icon");
		b.AddButton(out var bThis, "This", _ => { _SetIcon(tv); /*lastAction = _Action.FileIcon;*/ }).Disabled();
		b.AddButton("Default", _ => _SetIcon(null));
		//b.AddButton("Random", null); //idea: set random icons for multiple selected files. Probably too crazy.
		b.AddButton("Show current", _ => _ShowCurrent()).Margin("L20");
		b.End();
		//if (expandFileIcon) exp1.IsExpanded = true;
		exp1.IsExpanded = true;
		
		b.StartGrid<Expander>(out var exp2, "Menu/toolbar/etc icon");
		b.R.Add<Label>("Set icon of: ");
		b.StartStack();
		b.AddButton(out var bMenuItem, "Menu or toolbar item", _ => _InsertCodeOrExport(tv, _Action.MenuIcon)).Disabled()
			.Tooltip("To assign the selected icon to a toolbar button or menu item,\nin the code editor click its line (anywhere except action code)\nand then click this button.\n\nIf the 'Customize' window is open, this button sets its Image text.");
		b.End();
		//rejected. Rarely used. Can copy-paste XAML.
		//b.R.Add<Label>("Insert line: ");
		//b.StartStack();
		//b.AddButton(out var bCodeVar, "Variable = XAML", _ => _InsertCodeOrExport(tv, _Action.InsertXamlVar)).Disabled();
		//b.AddButton(out var bCodeField, "Field = XAML", _ => _InsertCodeOrExport(tv, _Action.InsertXamlField)).Disabled();
		//b.End();
		b.R.Add<Label>("Copy text: ");
		b.StartStack();
		b.AddButton(out var bCodeName, "Name", _ => _InsertCodeOrExport(tv, _Action.CopyName)).Width(70).Disabled()
			.Tooltip("Shorter string than XAML.\nCan be used with custom menus and toolbars,\neditor menus and toolbars (edit Commands.xml),\nScriptEditor.GetIcon, IconImageCache, ImageUtil,\noutput tag <image>.");
		b.AddButton(out var bCodeXaml, "XAML", _ => _InsertCodeOrExport(tv, _Action.CopyXaml)).Width(70).Disabled();
		b.End();
		b.End();
		//if (expandMenuIcon) exp2.IsExpanded = true;
		exp2.IsExpanded = true;
		
		b.StartStack<Expander>("Export to current workspace folder");
		b.AddButton(out var bExportXaml, ".xaml", _ => _InsertCodeOrExport(tv, _Action.ExportXaml)).Width(70).Disabled();
		b.AddButton(out var bExportIco, ".ico", _ => _InsertCodeOrExport(tv, _Action.ExportIcon)).Width(70).Disabled();
		b.Add("sizes", out iconSizes, "16,24,32,48,64").Width(100);
		b.End();
		
		//b.StartStack<Expander>("Other actions");
		//b.AddButton("Update cached icons", _ => IconImageCache.ClearAll());
		//b.End();
		
		b.StartGrid<Expander>("Options");
		b.Add("List background", out ComboBox cBackground)
			.Items("White|Black|Dark|Control")
			.Select(Math.Clamp(App.Settings.dicons_listColor, 0, 3));
		cBackground.SelectionChanged += (o, e) => {
			App.Settings.dicons_listColor = cBackground.SelectedIndex;
			_SetListIconBrush();
			tv.Redraw();
		};
		_SetListIconBrush();
		void _SetListIconBrush() {
			tv.ImageBrush = App.Settings.dicons_listColor switch { 0 => System.Drawing.Brushes.White, 1 => System.Drawing.Brushes.Black, 2 => System.Drawing.Brushes.DimGray, _ => System.Drawing.SystemBrushes.Control }; ;
		}
		var darkContrast = b.xAddCheckText("High contrast color", App.Settings.dicons_contrastColor, check: App.Settings.dicons_contrastUse);
		b.Tooltip("Append this color, like \"*Pack.Name selectedColor|thisColor\". This color is for high contrast dark theme. Can be #RRGGBB or color name.");
		darkContrast.c.CheckChanged += (_, _) => App.Settings.dicons_contrastUse = darkContrast.c.IsChecked;
		darkContrast.t.TextChanged += (_, _) => {
			var s = darkContrast.t.TextOrNull();
			if (s != null) for (int i = 0; i < 2; i++) { try { System.Windows.Media.ColorConverter.ConvertFromString(s); } catch { s = i == 0 ? "#" + s : null; } }
			App.Settings.dicons_contrastColor = s;
		};
		
		//b.Add(out KCheckBox cCollection, "Collection");
		//cCollection.CheckChanged += (_, _) => {
		//	_withCollection = cCollection.IsChecked == true;
		//	tv.Redraw();
		//};
		b.End();
		
		b.Row(-1);
		b.R.Add<TextBlock>().Align("R").FormatText($"Thanks to <a href='https://github.com/MahApps/MahApps.Metro.IconPacks'>MahApps.Metro.IconPacks</a>");
		b.End();
		
		b.End();
		
		b.Loaded += () => {
			_dpi = Dpi.OfWindow(this);
			_OpenDB();
			
			_a = new(30000);
			foreach (var (table, _) in s_tables) {
				using var stat = s_db.Statement("SELECT name FROM " + table);
				while (stat.Step()) {
					var k = new _Item(this, table, stat.GetText(0));
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
				if (select = name.RxMatch(@"^\*(\w+)\.(\w+) #(\w+)(?:\|.+)?$", out var m)) { //full name with * and #color or #color|highContrastColor
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
			//bCodeVar.IsEnabled = enable;
			//bCodeField.IsEnabled = enable;
			bCodeXaml.IsEnabled = enable;
			bCodeName.IsEnabled = enable;
			bExportXaml.IsEnabled = enable;
			bExportIco.IsEnabled = enable;
		}
		
		void _SetIcon(KTreeView tv) {
			string icon = tv?.SelectedItem is _Item k ? _ColorName(k) : null;
			int si = cbIconOf.SelectedIndex;
			if (si == 0) {
				foreach (var v in FilesModel.TreeControl.SelectedItems) v.CustomIconName = icon;
			} else {
				switch (si) {
				case 1: App.Settings.icons.ft_script = icon; break;
				case 2: App.Settings.icons.ft_class = icon; break;
				case 3: App.Settings.icons.ft_folder = icon; break;
				case 4: App.Settings.icons.ft_folderOpen = icon; break;
				}
				FilesModel.Redraw();
			}
		}
		
		void _ShowCurrent() {
			_tName.Text = cbIconOf.SelectedIndex switch {
				1 => App.Settings.icons.ft_script,
				2 => App.Settings.icons.ft_class,
				3 => App.Settings.icons.ft_folder,
				4 => App.Settings.icons.ft_folderOpen,
				_ => FilesModel.TreeControl.SelectedItems.FirstOrDefault()?.CustomIconName
			};
		}
		
		void _InsertCodeOrExport(KTreeView tv, _Action what) {
			//lastAction = what;
			if (tv.SelectedItem is not _Item k) return;
			//string code = null;
			if (what == _Action.MenuIcon) {
				var s = _ColorName(k);
				if (DCustomize.AaSetImage(s)) return;
				InsertCode.SetMenuToolbarItemIcon(s);
			} else if (what == _Action.CopyName) {
				clipboard.text = _ColorName(k);
			} else if (GetIconFromBigDB(k._table, k._name, _ItemColor(k), out var xaml)) {
				xaml = xaml.Replace('"', '\'').RxReplace(@"\R\s*", "");
				switch (what) {
				//case _Action.InsertXamlVar: code = $"string icon{k._name} = \"{xaml}\";"; break;
				//case _Action.InsertXamlField: code = $"public const string {k._name} = \"{xaml}\";"; break;
				case _Action.CopyXaml: clipboard.text = xaml; break;
				case _Action.ExportXaml: _Export(false); break;
				case _Action.ExportIcon: _Export(true); break;
				}
				
				void _Export(bool ico) {
					var folder = App.Model.CurrentFile?.Parent ?? App.Model.Root;
					var path = $"{folder.FilePath}\\{k._name}{(ico ? ".ico" : ".xaml")}";
					if (ico) {
						var sizes = iconSizes.Text.Split_(',').Select(o => o.ToInt()).ToArray();
						KImageUtil.XamlImageToIconFile(path, xaml, sizes);
					} else {
						filesystem.saveText(path, xaml);
					}
					var fn = App.Model.ImportFileFromWorkspaceFolder(path, new(folder, folder.Parent == null ? FNInsert.First : FNInsert.Last));
					if (fn == null) print.it("failed");
					else print.it($"<>Exported to <open>{fn.ItemPath}<>");
				}
			}
			
			//if (code != null) {
			//	if (what is _Action.CopyName or _Action.CopyXaml) clipboard.text = code;
			//	else if (what is _Action.InsertXamlVar or _Action.InsertXamlField) InsertCode.Statements(code);
			//	else InsertCode.TextSimply(code);
			//}
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
	}
	
	static string _ColorToString(int c) => "#" + c.ToS("X6");
	
	string _ItemColor(_Item k) => _random == null ? _color : _ColorToString(k._color);
	
	//string _ColorName(_Item k) => "*" + k._table + "." + k._name + " " + _ItemColor(k);
	string _ColorName(_Item k) {
		var s = "*" + k._table + "." + k._name + " " + _ItemColor(k);
		if (App.Settings.dicons_contrastUse && !App.Settings.dicons_contrastColor.NE()) s = s + "|" + App.Settings.dicons_contrastColor;
		return s;
	}
	
	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
		_dpi = newDpi.PixelsPerInchX.ToInt();
		base.OnDpiChanged(oldDpi, newDpi);
	}
	
	class _Item : ITreeViewItem {
		DIcons _dialog;
		public string _table, _name;
		public int _color;
		
		public _Item(DIcons dialog, string table, string name) {
			_dialog = dialog;
			_table = table; _name = name;
		}
		
		//string ITreeViewItem.DisplayText => s_dialog._withCollection ? (_name + new string(' ', Math.Max(8, 40 - _name.Length * 2)) + "(" + _table + ")") : _name;
		string ITreeViewItem.DisplayText => _name + new string(' ', Math.Max(8, 40 - _name.Length * 2)) + "(" + _table + ")";
		
		object ITreeViewItem.Image {
			//note: don't store UIElement or Bitmap. They can use hundreds MB of memory and it does not make faster/better. Let GC dispose unused objects asap.
			get {
				try {
					//using var p1 = perf.local();
					if (GetIconFromBigDB(_table, _name, _dialog._ItemColor(this), out string xaml)) {
						//p1.Next('d');
						return ImageUtil.LoadGdipBitmapFromXaml(xaml, _dialog._dpi, (16, 16));
					}
				}
				catch (Exception ex) { Debug_.Print(ex); }
				return null;
			}
		}
	}
	
	static sqlite s_db;
	static Dictionary<string, string> s_tables; //table->template, ~30 tables
	
	/// <summary>
	/// Returns true if <i>s</i> starts with "*pack.name" (possibly with library) and the pack exists.
	/// Fast, does not use the database (may use only the first time).
	/// </summary>
	public static bool PossiblyIconName(RStr s) {
		if (!WpfUtil_.DetectIconString(s, out var d)) return false;
		_OpenDB();
		return s_tables.ContainsKey(s[d.pack..d.endPack].ToString());
	}
	
	static void _OpenDB() {
		if (s_db == null) {
			var db = s_db = new sqlite(folders.ThisAppBS + "icons.db", SLFlags.SQLITE_OPEN_READONLY);
			process.thisProcessExit += _ => db.Dispose();
			s_tables = new(StringComparer.OrdinalIgnoreCase);
			using var st = s_db.Statement("SELECT * FROM _tables");
			while (st.Step()) {
				s_tables.Add(st.GetText(0), st.GetText(1));
			}
		}
	}
	
	/// <exception cref="Exception"></exception>
	public static bool GetIconFromBigDB(string table, string name, string color, out string xaml, int size = 0) {
		xaml = null;
		_OpenDB();
		if (!s_tables.TryGetValue(table, out var templ)) return false;
		if (!s_db.Get(out string data, $"SELECT data FROM {table} WHERE name='{name}'")) return false;
		//the SELECT is the slowest part. With prepared statement just slightly faster.
		
		if (!WpfUtil_.SetColorInXaml(ref templ, color)) return false;
		
		int i = templ.Find(" Data=\"{x:Null}\""); if (i < 0) return false;
		
		if (size is > 0 and <= 16) {
			if (templ.Contains(" Width=\"")) { //size specified in XAML is required to properly render the icon
				print.warning($"@size not supported for this icon: *{table}.{name}", -1);
			} else {
				int margin = 16 - size;
				string dot5 = 0 != (margin & 1) ? ".5" : null;
				templ = templ.Insert(i + 16, $" Width=\"{size}\" Height=\"{size}\" Margin=\"{margin / 2}{dot5}\"");
				templ = templ.Replace(" SnapsToDevicePixels=\"False\"", " SnapsToDevicePixels=\"True\""); //less blurry
				
				//FUTURE:
				// @12[D1][U1][L1][R1] (shift down etc)
			}
		}
		
		templ = templ.ReplaceAt(i + 7, 8, data);
		if (templ.Contains("\"{")) return false;
		
		xaml = $@"<Viewbox Width='16' Height='16' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>{templ}</Viewbox>";
		return true;
	}
	
	/// <param name="icon">Icon name, like "*Pack.Icon color", where color is like #RRGGBB or color name. If just "*Pack.Icon", sets "black". Can be null.</param>
	/// <exception cref="Exception"></exception>
	public static bool GetIconFromBigDB(string icon, out string xaml) {
		xaml = null;
		if (!WpfUtil_.ParseIconString(icon, out var r)) return false;
		return GetIconFromBigDB(r.pack, r.name, r.color, out xaml, r.size);
	}
	
#if true
	/// <param name="icon">Icon name, like "*Pack.Icon color", where color is like #RRGGBB or color name. If just "*Pack.Icon", sets "black". Can be null.</param>
	public static bool TryGetIconFromBigDB(string icon, out string xaml) {
		//using var p1 = perf.local();
		try { return GetIconFromBigDB(icon, out xaml); }
		catch (Exception e1) { Debug_.Print(e1); }
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
				var dbFile = folders.ThisAppDataLocal + "icons.db";
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
		catch (Exception e1) { Debug_.Print(e1); }
		return false;
	}
	static sqlite s_iconsDB;
#endif
	
	public static string GetIconString(string s, EGetIcon what) {
		if (what != EGetIcon.IconNameToXaml) {
			s = App.Model.Find(s, silent: true)?.Image switch {
				string v => v,
				IEnumerable<object> v => v.First() as string,
				_ => null
			};
		}
		if (what != EGetIcon.PathToIconName && s != null) TryGetIconFromBigDB(s, out s);
		return s;
	}
}
