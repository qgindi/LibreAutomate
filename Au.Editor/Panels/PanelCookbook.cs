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
//	string text, folder directory, program app application, run open, email mail
//	Use the debug context menu to print all words.

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
		b.R.Add(out _search).Tooltip("Part of recipe name");
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
		var s = _search.Text;
		if (s.Length < 2) {
			_tv.SetItems(_root.Children());
			return;
		}
		string stemmed = null;

		//print.clear();

		var root2 = _Search2(_root);

		_Item _Search2(_Item parent) {
			_Item R = null;
			for (var n = parent.FirstChild; n != null; n = n.Next) {
				_Item r;
				if (n.dir) {
					r = _Search2(n);
					if (r == null) continue;
				} else {
					if (inBody) {
						var t = n.GetBodyTextWithoutLinksEtc();
						if (!t.Contains(s, StringComparison.OrdinalIgnoreCase)) continue;

						//rejected: use SQLite FTS5. Tried but didn't like.
						//	It would be useful with many big files. Now we have < 200 small files, total < 1 MB.
					} else {
						if (!n.name.Contains(s, StringComparison.OrdinalIgnoreCase)) {
							if (s.Length < 4) continue;
							stemmed ??= _Stem(s);
							n.stemmedName ??= _Stem(n.name);
							var z = FuzzySharp.Fuzz.PartialRatio(stemmed, n.stemmedName, FuzzySharp.PreProcess.PreprocessMode.Full);
							//if (z > 60) print.it(n.stemmed, z);
							if (z < 75) continue;
						}

						//never mind:
						//	Eg if s is "file folder", does not find "File and folder dialogs".
						//	Eg if s is "folder file", does not find "... file, folder ...".
						//	Maybe in some cases it's good, but in most cases bad.
						//	Try Sorted? But then probably bad eg "webpage" and "web page". Try Weighted?
					}
					r = new _Item(n.name, false);
				}
				R ??= new _Item(parent.name, true) { isExpanded = true };
				R.AddChild(r);
			}
			return R;
		}

		_tv.SetItems(root2?.Children());
	}

	string _Stem(string s) {
		if (_stem.stemmer == null) _stem = (new(), new());
		var sb = _stem.sb;
		sb.Clear();
		foreach (var t in s.Lower().Segments(SegSep.Word, SegFlags.NoEmpty)) {
			if (sb.Length > 0) sb.Append(' ');
			sb.Append(_stem.stemmer.Stem(s[t.Range]));
		}
		return sb.ToString();
	}
	(Porter2Stemmer.EnglishPorter2Stemmer stemmer, StringBuilder sb) _stem;

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
			text = _Stem(text);
			foreach (var seg in text.Segments(SegSep.Word, SegFlags.NoEmpty))
				if (seg.Length > 2 && !text[seg.start].IsAsciiDigit()) hs.Add(text[seg.Range]);
		}
		print.it(hs.OrderBy(o => o));
	}
#endif

	class _Item : TreeBase<_Item>, ITreeViewItem {
		internal readonly string name;
		internal readonly bool dir;
		internal bool isExpanded;
		internal string stemmedName;

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
