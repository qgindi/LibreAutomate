using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using System.Linq;
using Au.Controls;
using Au.Tools;

//CONSIDER: Add a menu-button. Menu:
//	Item "Request a recipe for this search query (uses internet)".
//	Checkbox "Auto-update the cookbook (uses internet)".

//CONSIDER: option to show Recipe panel when Cookbook panel is really visible and hide when isn't.

//SHOULDDO: add some synonyms:
//	string/text, folder/directory, program/app/application, run/open, email/mail, regular expression/regex
//	See _DebugGetWords.

class PanelCookbook : UserControl {
	KTreeView _tv;
	TextBox _search;
	_Item _root;
	bool _loaded;
	List<string> _history = new();

	static string s_cookbookPath;

	public PanelCookbook() {
		this.UiaSetName("Cookbook panel");

		var b = new wpfBuilder(this).Columns(-1, 0, 0).Brush(SystemColors.ControlBrush);
		b.R.Add(out _search).Tooltip("Part of recipe name.\nMiddle-click to clear.");
		b.Options(modifyPadding: false, margin: new());
		_search.TextChanged += (_, _) => _Search(false);
		_search.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) _search.Text = ""; };
		b.xAddButtonIcon("*Material.TextSearch #EABB00", _ => _Search(true), "Find in recipe text");
		b.xAddButtonIcon("*Material.History #EABB00", _ => _HistoryMenu(), "History");
		b.Margin(right: 3);
		_tv = new() { Name = "Cookbook_list", SingleClickActivate = true, HotTrack = true };
		b.Row(-1).Add(_tv);
		b.End();

#if DEBUG
		_tv.ItemClick += (_, e) => {
			if (e.MouseButton == MouseButton.Right) {
				var m = new popupMenu();
				m.Add("DEBUG", disable: true);
				m["Reload"] = o => {
					Menus.File.Workspace.Save_now();
					_Load();
				};
				m["Check links"] = o => _DebugCheckLinks();
				m["Print name words"] = o => _DebugGetWords(false);
				m["Print body words"] = o => _DebugGetWords(true);
				m.Show();
			}
		};
#endif
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
		if (!_loaded && e.Property.Name == "IsVisible" && e.NewValue is bool y && y) {
			_loaded = true;
			_Load();
			_tv.ItemActivated += (_, e) => _OpenRecipe(e.Item as _Item, false);
		}
		base.OnPropertyChanged(e);
	}

	void _Load() {
		try {
			s_cookbookPath = folders.ThisAppBS + "Cookbook\\files";
			var xr = XmlUtil.LoadElem(s_cookbookPath + ".xml");

			_root = new _Item(null, true);
			_AddItems(xr, _root, 0);

			static void _AddItems(XElement xp, _Item ip, int level) {
				foreach (var x in xp.Elements()) {
					var name = x.Attr("n");
					if (name[0] == '-') continue;
					var tag = x.Name.LocalName;
					bool dir = tag == "d";
					if (!dir) {
						if (tag != "s") continue;
						name = name[..^3];
					}
					var i = new _Item(name, dir);
					ip.AddChild(i);
					if (dir) _AddItems(x, i, level + 1);
				}
			}

			_tv.SetItems(_root.Children());
		}
		catch { }
	}

	void _OpenRecipe(_Item recipe, bool select) {
		if (recipe == null || recipe.dir) return;

		if (select) {
			_search.Text = "";
			_tv.EnsureVisible(recipe);
			_tv.Select(recipe);
		}

		if (recipe.GetBodyText() is string code) {
			Panels.Recipe.Display(recipe.name, code);
			AddToHistory(recipe.name);
		}
	}

	void _Search(bool inBody) {
		var s = _search.Text.Trim();
		if (s.Length < 2) {
			_tv.SetItems(_root.Children());
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
					if (!t.Contains(s, StringComparison.OrdinalIgnoreCase)) continue;
					r = new _Item(n.name, false);
				}
				R ??= new _Item(parent.name, true) { isExpanded = true };
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
						r = new _Item(n.name, false);
					}
					R ??= new _Item(parent.name, true) { isExpanded = true };
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
		if (_stem.stemmer == null) _stem = (new(), new());
		_stem.a.Clear();
		foreach (var t in s.Lower().Segments(SegSep.Word, SegFlags.NoEmpty)) {
			_stem.a.Add(_stem.stemmer.Stem(s[t.Range]));
		}
		return _stem.a.ToArray();
	}
	(Porter2Stemmer.EnglishPorter2Stemmer stemmer, List<string> a) _stem;

	internal void OpenRecipe(string s) {
		Panels.PanelManager[this].Visible = true;
		_OpenRecipe(_FindRecipe(s), true);
	}

	_Item _FindRecipe(string s) {
		var d = _root.Descendants();
		return d.FirstOrDefault(o => !o.dir && o.name.Like(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.name.Starts(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.name.Find(s, true) >= 0);
	}

	internal void AddToHistory(string recipe) {
		_history.Remove(recipe);
		_history.Add(recipe);
		if (_history.Count > 20) _history.RemoveAt(0);
	}

	void _HistoryMenu() {
		var m = new popupMenu();
		for (int i = _history.Count - 1; --i >= 0;) m[_history[i]] = o => _Open(o.Text);
		m.Show(owner: this);

		void _Open(string name) {
			var v = _root.Descendants().FirstOrDefault(o => !o.dir && o.name == name);
			_OpenRecipe(v, true);
		}
	}

#if DEBUG
	void _DebugCheckLinks() {
		print.clear();
		foreach (var recipe in _root.Descendants().Where(o => !o.dir)) {
			var text = recipe.GetBodyText();
			if (text == null) { print.it("Failed to load the recipe. Probably renamed. Try to reload the tree."); return; }
			foreach (var m in text.RxFindAll(@"<\+recipe>(.+?)<>")) {
				var s = m[1].Value;
				//print.it(s);
				if (null == _FindRecipe(s)) print.it($"Invalid link '{s}' in {recipe.name}");
			}
		}
	}

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
		print.it(hs.OrderBy(o => o));
	}
#endif

	class _Item : TreeBase<_Item>, ITreeViewItem {
		internal readonly string name;
		internal readonly bool dir;
		internal bool isExpanded;
		internal string[] stemmedName;

		public _Item(string name, bool dir) {
			this.name = name;
			this.dir = dir;
		}

		#region ITreeViewItem

		string ITreeViewItem.DisplayText => name;

		string ITreeViewItem.ImageSource => isExpanded ? @"resources/images/expanddown_16x.xaml" : (_IsFolder ? @"resources/images/expandright_16x.xaml" : "*BoxIcons.RegularCookie #EABB00");

		void ITreeViewItem.SetIsExpanded(bool yes) { isExpanded = yes; }

		bool ITreeViewItem.IsExpanded => isExpanded;

		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();

		bool ITreeViewItem.IsFolder => _IsFolder;
		bool _IsFolder => base.HasChildren;

		#endregion

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

		public string GetBodyTextWithoutLinksEtc() {
			var t = GetBodyText(); if (t == null) return null;
			t = t.RxReplace(@"<see cref=""(.+?)""/>", "$1");
			while (0 != t.RxReplace(@"<(\+?\w+)(?: [^>]+)?>(.+?)<(?:/\1|)>", "$2", out t)) { }
			t = t.RxReplace(@"\bimage:[\w/+=]+", "");
			return t;
		}
	}
}
