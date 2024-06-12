using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using Au.Controls;

//CONSIDER: Add a menu-button. Menu:
//	Item "Request a recipe for this search query (uses internet)".

//CONSIDER: option to show Recipe panel when Cookbook panel is really visible and hide when isn't.

//TODO3: add some synonyms:
//	string/text, folder/directory, program/app/application, run/open, email/mail, regular expression/regex
//	See _DebugGetWords.

class PanelCookbook {
	KTreeView _tv;
	TextBox _search;
	_Item _root;
	bool _loaded;
	bool _openingRecipe;
	List<string> _history = new();
	
#if DEBUG
	static string s_cookbookPath;
#else
	static sqlite s_sqlite;
	static sqliteStatement s_sqliteGetText;
#endif
	
	public PanelCookbook() {
		P = new _Base(this);
		P.UiaSetName("Cookbook panel");
		
		var b = new wpfBuilder(P).Columns(-1, 0, 0).Brush(SystemColors.ControlBrush);
		b.R.Add(out _search).Tooltip("Part of recipe name.\nMiddle-click to clear.").UiaName("Find recipe");
		b.Options(modifyPadding: false, margin: new());
		_search.TextChanged += (_, _) => _Search(false);
		_search.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) _search.Text = ""; };
		b.xAddButtonIcon("*Material.TextSearch" + Menus.darkYellow, _ => _Search(true), "Find in recipe text");
		b.xAddButtonIcon("*EvaIcons.ArrowBack" + Menus.darkYellow, _ => _HistoryMenu(), "Go back...");
		b.Margin(right: 3);
		_tv = new() { Name = "Cookbook_list", SingleClickActivate = true, FullRowExpand = true, HotTrack = true, BackgroundColor = 0xf0f8e8 };
		b.Row(-1).Add(_tv);
		b.End();
		
		Panels.PanelManager["Cookbook"].DontActivateFloating = e => e == _tv;
		
#if DEBUG
		_tv.ItemClick += e => {
			if (e.Button == MouseButton.Right) {
				var m = new popupMenu();
				m.Add("DEBUG", disable: true);
				m["Reload"] = o => { Menus.File.Workspace.Save_now(); UnloadLoad(false); UnloadLoad(true); };
				//m["Check links"] = o => _DebugCheckLinks();
				m["Print name words"] = o => _DebugGetWords(false);
				m["Print body words"] = o => _DebugGetWords(true);
				m.Show();
			}
		};
#endif
	}
	
	public UserControl P { get; }
	
	void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
		if (!_loaded && e.Property.Name == "IsVisible" && e.NewValue is bool y && y) {
			_loaded = true;
			_Load();
			_tv.ItemActivated += e => _OpenRecipe(e.Item as _Item, false);
		}
	}
	
	class _Base : UserControl {
		PanelCookbook _p;
		
		public _Base(PanelCookbook p) { _p = p; }
		
		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
			_p.OnPropertyChanged(e);
			base.OnPropertyChanged(e);
		}
	}
	
	void _Load() {
		try {
#if DEBUG
			s_cookbookPath = folders.ThisAppBS + "Cookbook\\files";
			var xr = XmlUtil.LoadElem(s_cookbookPath + ".xml");
#else
			s_sqlite = new(folders.ThisAppBS + "cookbook.db", SLFlags.SQLITE_OPEN_READONLY);
			s_sqliteGetText = s_sqlite.Statement("SELECT data FROM files WHERE name=?");
			var xml = _GetText("files.xml"); if (xml == null) return;
			var xr = XElement.Parse(xml);
#endif
			
			_root = new _Item(null, FNType.Folder);
			_AddItems(xr, _root, 0);
			
			static void _AddItems(XElement xp, _Item ip, int level) {
				foreach (var x in xp.Elements()) {
					var name = x.Attr("n");
					if (name[0] == '-') continue;
					var ftype = FileNode.XmlTagToFileType(x.Name.LocalName, false);
					if (ftype == FNType.Other) continue;
					if (ftype != FNType.Folder) name = name[..^3];
					var i = new _Item(name, ftype);
					ip.AddChild(i);
					if (ftype == FNType.Folder) _AddItems(x, i, level + 1);
				}
			}
			
			_tv.SetItems(_root.Children());
		}
		catch (Exception e1) { print.it(e1); }
	}
	
	//Used by script "Create cookbook.db" to unlock database file and auto-reload.
	public bool UnloadLoad(bool load) {
		if (load == _loaded) return false;
		if (load) {
			_Load();
		} else {
#if !DEBUG
			s_sqliteGetText.Dispose(); s_sqliteGetText = null;
			s_sqlite.Dispose(); s_sqlite = null;
#endif
			_root = null;
			_loaded = false;
			_tv.SetItems(null);
		}
		return true;
	}
	
#if !DEBUG
	//In Release config loads files from database "cookbook.db" created by script "Create cookbook.db".
	//In Debug config loads files directly. It allows to edit them and see results without creating database.
	//Previously always loaded from files. But it triggered 7 false positives in virustotal.com. The "bad" recipe was PowerShell.
	//The same recipes don't trigger FP when in database. Additionally the script mangles text to avoid FP in the future.

	static string _Unmangle(string s) => s_unmangle.Replace(s, "$1");
	static readonly regexp s_unmangle = new(@"A([A-Z][a-z]+)");

	static string _GetText(string name) {
		if (!s_sqliteGetText.Reset().Bind(1, name).Step()) {
			print.warning($"{name} not found in cookbook.db. Reinstall this program.");
			return null;
		}
		var s = s_sqliteGetText.GetText(0);
		return _Unmangle(s);
	}
#endif
	
	void _OpenRecipe(_Item recipe, bool select) {
		if (recipe == null || recipe.dir) return;
		
		if (select) {
			_openingRecipe = true;
			_search.Text = "";
			_openingRecipe = false;
			_tv.EnsureVisible(recipe);
			_tv.Select(recipe);
		}
		
		if (recipe.GetBodyText() is string code) {
			Panels.Recipe.Display(recipe.name, code);
			AddToHistory_(recipe.name);
		}
	}
	
	void _Search(bool inBody) {
		var s = _search.Text.Trim();
		if (s.Length < 2) {
			_tv.SetItems(_root.Children());
			if (!_openingRecipe && _history.LastOrDefault() is string s1 && _FindRecipe(s1, exact: true) is _Item r) {
				_tv.EnsureVisible(r);
				_tv.Select(r);
			}
			return;
		}
		
		//print.clear();
		
		var root2 = _SearchContains(_root);
		_Item _SearchContains(_Item parent) {
			_Item R = null;
			for (var n = parent.FirstChild; n != null; n = n.Next) {
				_Item r;
				if (n.dir) {
					r = _SearchContains(n);
					if (r == null) continue;
				} else {
					var t = inBody ? n.GetBodyTextWithoutLinksEtc() : n.name;
					if (t == null || !t.Contains(s, StringComparison.OrdinalIgnoreCase)) continue;
					r = new _Item(n.name, n.ftype);
				}
				R ??= new _Item(parent.name, FNType.Folder) { isExpanded = true };
				R.AddChild(r);
			}
			return R;
			
			//rejected: use SQLite FTS5. Tried but didn't like.
			//	It would be useful with many big files. Now we have < 200 small files, total < 1 MB.
		}
		
		//try stemmed fuzzy. Max Levenshtein distance 1 for a word.
		//	rejected: use FuzzySharp. For max distance 1 don't need it.
		if (root2 == null && !inBody && s.Length >= 3) {
			var a1 = _Stem(s);
			root2 = _SearchFuzzy(_root);
			_Item _SearchFuzzy(_Item parent) {
				_Item R = null;
				for (var n = parent.FirstChild; n != null; n = n.Next) {
					_Item r;
					if (n.dir) {
						r = _SearchFuzzy(n);
						if (r == null) continue;
					} else {
						n.stemmedName ??= _Stem(n.name);
						bool allFound = true;
						foreach (var v1 in a1) {
							bool found = false;
							foreach (var v2 in n.stemmedName) {
								if (found = _Match(v1, v2)) break;
							}
							if (!(allFound &= found)) break;
						}
						if (!allFound) continue;
						r = new _Item(n.name, n.ftype);
					}
					R ??= new _Item(parent.name, FNType.Folder) { isExpanded = true };
					R.AddChild(r);
				}
				return R;
			}
		}
		//rejected: try joined words. Eg for "webpage" also find "web page" and "web-page".
		//	Will find all after typing "web". Never mind fuzzy.
		
		_tv.SetItems(root2?.Children());
		
		static bool _Match(string s1, string s2) {
			if (s1[0] != s2[0] || Math.Abs(s1.Length - s2.Length) > 1) return false; //the first char must match
			if (s1.Length > s2.Length) Math2.Swap(ref s1, ref s2); //let s1 be the shorter
			
			int ib = 0, ie1 = s1.Length, ie2 = s2.Length;
			while (ib < s1.Length && s1[ib] == s2[ib]) ib++; //skip common prefix
			while (ie1 > ib && s1[ie1 - 1] == s2[--ie2]) ie1--; //skip common suffix
			
			int n = ie1 - ib;
			if (n == 1) return s1.Length == s2.Length || ib == ie1;
			return n == 0;
		}
	}
	
	string[] _Stem(string s) {
		if (_stem.stemmer == null) _stem = (new(), new(), new regexp(@"(*UCP)[^\W_]+"));
		_stem.a.Clear();
		foreach (var v in _stem.rx.FindAll(s.Lower(), 0)) {
			_stem.a.Add(_stem.stemmer.Stem(v));
		}
		return _stem.a.ToArray();
	}
	(Porter2Stemmer.EnglishPorter2Stemmer stemmer, List<string> a, regexp rx) _stem;
	
	/// <summary>
	/// Finds and opens a recipe.
	/// Thread-safe.
	/// </summary>
	/// <param name="s">Wildcard or start or any substring of recipe name.</param>
	public void OpenRecipe(string s) {
		if (Environment.CurrentManagedThreadId == 1) _Open(); else App.Dispatcher.InvokeAsync(_Open);
		void _Open() {
			Panels.PanelManager[P].Visible = true;
			_OpenRecipe(_FindRecipe(s), true);
		}
	}
	
	/// <summary>
	/// Opens recipe in web browser. Does not change anything in the Cookbook panel. Does not add to history.
	/// </summary>
	/// <param name="name">Exact recipe name. If null, opens the cookbook index page.</param>
	public void OpenRecipeInWebBrowser(string name) {
		var s = name?.Replace("#", "Sharp").Replace(".", "dot") ?? "index"; //see project @Au docs -> AuDocs.Cookbook
		run.itSafe($"https://www.libreautomate.com/cookbook/{s}.html");
	}
	
	_Item _FindRecipe(string s, bool exact = false) {
		var d = _root.Descendants();
		if (exact) return d.FirstOrDefault(o => !o.dir && o.name == s);
		return d.FirstOrDefault(o => !o.dir && o.name.Like(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.name.Starts(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.name.Find(s, true) >= 0);
	}
	
	internal void AddToHistory_(string recipe) {
		_history.Remove(recipe);
		_history.Add(recipe);
		if (_history.Count > 20) _history.RemoveAt(0);
	}
	
	void _HistoryMenu() {
		var m = new popupMenu();
		for (int i = _history.Count - 1; --i >= 0;) m[_history[i]] = o => _Open(o.Text);
		m.Show(owner: P);
		
		void _Open(string name) {
			_OpenRecipe(_FindRecipe(name, exact: true), true);
		}
	}
	
#if DEBUG
	//rejected. Now checks when building online help.
	//void _DebugCheckLinks() {
	//	print.clear();
	//	foreach (var recipe in _root.Descendants().Where(o => !o.dir)) {
	//		var text = recipe.GetBodyText();
	//		if (text == null) { print.it("Failed to load the recipe. Probably renamed. Try to reload the tree."); return; }
	//		foreach (var m in text.RxFindAll(@"<\+recipe>(.+?)<>")) { //todo: can be <+recipe attr>text<>
	//			var s = m[1].Value;
	//			//print.it(s);
	//			if (null == _FindRecipe(s)) print.it($"Invalid link '{s}' in {recipe.name}");
	//		}
	//	}
	//}
	
	void _DebugGetWords(bool body) {
		print.clear();
		var hs = new HashSet<string>();
		foreach (var recipe in _root.Descendants().Where(o => !o.dir)) {
			string text;
			if (body) {
				text = recipe.GetBodyTextWithoutLinksEtc();
				if (text == null) { print.it("Failed to load the recipe. Probably renamed. Try to reload the tree."); return; }
			} else {
				text = recipe.name;
			}
			var a = _Stem(text);
			foreach (var s in a)
				if (s.Length > 2 && !s[0].IsAsciiDigit()) hs.Add(s);
		}
		print.it(hs.OrderBy(o => o, StringComparer.OrdinalIgnoreCase));
	}
#endif
	
	class _Item : TreeBase<_Item>, ITreeViewItem {
		internal readonly string name;
		internal readonly FNType ftype;
		internal bool isExpanded;
		internal string[] stemmedName;
		
		public _Item(string name, FNType ftype) {
			this.name = name;
			this.ftype = ftype;
		}
		
		internal bool dir => ftype == FNType.Folder;
		
		#region ITreeViewItem
		
		string ITreeViewItem.DisplayText => name;
		
		object ITreeViewItem.Image
			=> dir ? EdResources.FolderArrow(isExpanded)
			: ftype == FNType.Class ? EdResources.c_iconClass
			: "*BoxIcons.RegularCookie" + Menus.darkYellow;
		
		void ITreeViewItem.SetIsExpanded(bool yes) { isExpanded = yes; }
		
		bool ITreeViewItem.IsExpanded => isExpanded;
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		
		bool ITreeViewItem.IsFolder => dir;
		
		#endregion
		
#if DEBUG
		public string FullPath {
			get {
				if (_path == null && name != null) {
					var stack = s_stack1;
					stack.Clear();
					stack.Push(name + ".cs");
					for (var p = Parent; p != null && p.HasParent; p = p.Parent) stack.Push(p.name);
					stack.Push(s_cookbookPath);
					_path = string.Join("\\", stack);
					//print.it(_path, filesystem.exists(_path).File);
				}
				return _path;
			}
		}
		string _path;
		static Stack<string> s_stack1 = new();
		
		public string GetBodyText() {
			try { return filesystem.loadText(FullPath); } catch { return null; }
		}
#else
		public string GetBodyText() {
			return _GetText(name);
		}
#endif
		
		public string GetBodyTextWithoutLinksEtc() {
			var t = GetBodyText(); if (t == null) return null;
			t = t.RxReplace(@"<see cref=""(.+?)""/>", "$1");
			while (0 != t.RxReplace(@"<(\+?\w+)(?: [^>]+)?>(.+?)<(?:/\1|)>", "$2", out t)) { }
			t = t.RxReplace(@"\bimage:[\w/+=]+", "");
			return t;
		}
	}
}
