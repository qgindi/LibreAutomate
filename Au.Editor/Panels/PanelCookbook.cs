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

class PanelCookbook : UserControl {
	KTreeView _tv;
	TextBox _search;
	_Item _root;
	bool _loaded;
	string _cookbookPath;
	List<string> _history = new();

	public PanelCookbook() {
		this.UiaSetName("Cookbook panel");

		var b = new wpfBuilder(this).Columns(-1, 0).Brush(SystemColors.ControlBrush);
		b.R.Add(out _search).Tooltip("Part of recipe name");
		b.Options(modifyPadding: false, margin: new());
		_search.TextChanged += _search_TextChanged;
		_search.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) _search.Text = ""; };
		b.xAddButtonIcon("*Material.History #EABB00", _ => _HistoryMenu(), "History"); b.Margin(right: 3);
		_tv = new() { Name = "Cookbook_list", SingleClickActivate = true, HotTrack = true };
		b.Row(-1).Add(_tv);
		b.End();

#if DEBUG
		_tv.ItemClick += (_, e) => {
			if (e.MouseButton == MouseButton.Right) {
				int i = popupMenu.showSimple("1 Reload (debug)|2 Check links");
				if (i == 1) {
					Menus.File.Workspace.Save_now();
					_Load();
				} else if (i == 2) {
					_CheckLinks();
				}
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
			_cookbookPath = folders.ThisAppBS + "Cookbook\\files";
			var xr = XmlUtil.LoadElem(_cookbookPath + ".xml");

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

		if (_GetRecipeText(recipe) is string code) {
			Panels.Recipe.Display(recipe.text, code);
			AddToHistory(recipe.text);
		}
	}

	string _GetRecipeText(_Item recipe) {
		//get file path
		var stack = new Stack<string>();
		stack.Push(recipe.text + ".cs");
		for (var p = recipe.Parent; p.HasParent; p = p.Parent) stack.Push(p.text);
		stack.Push(_cookbookPath);
		var path = string.Join("\\", stack);
		//print.it(path, filesystem.exists(path).File);

		try { return filesystem.loadText(path); } catch { return null; }
	}

	private void _search_TextChanged(object sender, TextChangedEventArgs e) {
		var s = _search.Text;
		if (s.Length < 2) {
			_tv.SetItems(_root.Children());
			return;
		}

		var stemmer = new Porter2Stemmer.EnglishPorter2Stemmer();
		var sb = new StringBuilder();
		foreach (var v in _root.Descendants()) if (!v.dir) v.stemmed ??= _Stem(v.text);
		var stemmed = s.Length < 4 ? null : _Stem(s);

		//print.clear();
		var root2 = _Search(_root);

		_Item _Search(_Item parent) {
			_Item R = null;
			for (var n = parent.FirstChild; n != null; n = n.Next) {
				_Item r;
				if (n.dir) {
					r = _Search(n);
				} else {
					if (!n.text.Contains(s, StringComparison.OrdinalIgnoreCase)) {
						if (stemmed == null) continue;
						var z = FuzzySharp.Fuzz.PartialRatio(stemmed, n.stemmed, FuzzySharp.PreProcess.PreprocessMode.Full);
						//if (z > 60) print.it(n.stemmed, z);
						if (z < 75) continue;
					}
					r = new _Item(n.text, false);
				}
				if (r == null) continue;
				R ??= new _Item(parent.text, true) { isExpanded = true };
				R.AddChild(r);
			}
			return R;

			//CONSIDER: full-text search, including recipe text. Can be used SQLite FTS easily.
		}

		_tv.SetItems(root2?.Children());

		string _Stem(string s) {
			sb.Clear();
			foreach (var t in s.Lower().Segments(SegSep.Word, SegFlags.NoEmpty)) {
				if (sb.Length > 0) sb.Append(' ');
				sb.Append(stemmer.Stem(s[t.Range]));
			}
			return sb.ToString();
		}
	}

	internal void OpenRecipe(string s) {
		Panels.PanelManager[this].Visible = true;
		_OpenRecipe(_FindRecipe(s), true);
	}

	_Item _FindRecipe(string s) {
		var d = _root.Descendants();
		return d.FirstOrDefault(o => !o.dir && o.text.Like(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.text.Starts(s, true))
			?? d.FirstOrDefault(o => !o.dir && o.text.Find(s, true) >= 0);
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
			var v = _root.Descendants().FirstOrDefault(o => !o.dir && o.text == name);
			_OpenRecipe(v, true);
		}
	}

#if DEBUG
	void _CheckLinks() {
		foreach (var recipe in _root.Descendants().Where(o => !o.dir)) {
			var text = _GetRecipeText(recipe);
			if (text == null) { print.it("Failed to load the recipe. Probably renamed. Try to reload the tree."); return; }
			foreach (var m in text.RxFindAll(@"<\+recipe>(.+?)<>")) {
				var s = m[1].Value;
				//print.it(s);
				if (null == _FindRecipe(s)) print.it($"Invalid link '{s}' in {recipe.text}");
			}
		}
	}
#endif

	class _Item : TreeBase<_Item>, ITreeViewItem {
		internal readonly string text;
		internal readonly bool dir;
		internal bool isExpanded;
		internal string stemmed;

		public _Item(string text, bool dir) {
			this.text = text;
			this.dir = dir;
		}

		#region ITreeViewItem

		string ITreeViewItem.DisplayText => text;

		string ITreeViewItem.ImageSource => isExpanded ? @"resources/images/expanddown_16x.xaml" : (_IsFolder ? @"resources/images/expandright_16x.xaml" : "*BoxIcons.RegularCookie #EABB00");

		void ITreeViewItem.SetIsExpanded(bool yes) { isExpanded = yes; }

		bool ITreeViewItem.IsExpanded => isExpanded;

		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();

		bool ITreeViewItem.IsFolder => _IsFolder;
		bool _IsFolder => base.HasChildren;

		#endregion
	}
}
