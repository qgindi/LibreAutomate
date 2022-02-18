using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using System.Xml.Linq;
using System.Linq;

//CONSIDER: Name "Cookbook" is a bit too long if tab. Need a shorter name, eg Howto.

//CONSIDER: Add a menu-button. Menu:
//	Item "Request a recipe for this search query (uses internet)".
//	Checkbox "Auto-update the cookbook (uses internet)".

class PanelCookbook : DockPanel {
	KTreeView _tv;
	TextBox _search;
	_Item _root;
	bool _loaded;
	string _cookbookPath;

	public PanelCookbook() {
		//this.UiaSetName("Cookbook panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

		_search = new TextBox { Margin = new(2), ToolTip = "Part of recipe name, or wildcard expression" };
		this.Children.Add(_search);
		SetDock(_search, Dock.Top);
		_search.TextChanged += _search_TextChanged;

		//TODO: _history menu-button

		_tv = new() { Name = "Cookbook_list", SingleClickActivate = true, HotTrack = true };
		this.Children.Add(_tv);

#if DEBUG
		_tv.ItemClick += (_, e) => {
			if (e.MouseButton == System.Windows.Input.MouseButton.Right) {
				int i = popupMenu.showSimple("1 Reload (debug)");
				if (i == 1) {
					Menus.File.Workspace.Save_now();
					_Load();
				}
			}
		};
#endif
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
		if (!_loaded && e.Property.Name == "IsVisible" && e.NewValue is bool y && y) {
			_loaded = true;
			_Load();
			_tv.ItemActivated += (_, e) => _OpenRecipe(e.Item as _Item);
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
					var tag = x.Name.LocalName;
					bool dir = tag == "d";
					if (!dir) if (level == 0 || tag != "s") continue;
					var name = x.Attr("n"); if (!dir) name = name[..^3];
					var i = new _Item(name, dir);
					ip.AddChild(i);
					if (dir) _AddItems(x, i, level + 1);
				}
			}

			_tv.SetItems(_root.Children());
		}
		catch { }
	}

	void _OpenRecipe(_Item recipe) {
		if (recipe.dir) return;

		//get file path
		var stack = new Stack<string>();
		stack.Push(recipe.text + ".cs");
		for (var p = recipe.Parent; p.HasParent; p = p.Parent) stack.Push(p.text);
		stack.Push(_cookbookPath);
		var path = string.Join("\\", stack);
		//print.it(path, filesystem.exists(path).File);

		try {
			var code = filesystem.loadText(path);
			//print.it(code);
			Panels.Recipe.SetText(code);
		}
		catch { }
	}

	private void _search_TextChanged(object sender, TextChangedEventArgs e) {
		var s = _search.Text;
		if (s.Length < 2) {
			_tv.SetItems(_root.Children());
			return;
		}

		var wild = wildex.hasWildcardChars(s) ? new wildex(s, noException: true) : null;
		bool allMatch = true;
		var root2 = _Search(_root);
		if (root2 != null && allMatch) root2 = _root; //eg ** matches all

		_Item _Search(_Item parent) {
			_Item R = null;
			for (var n = parent.FirstChild; n != null; n = n.Next) {
				_Item r;
				if (n.dir) {
					r = _Search(n);
				} else {
					if (!n.text.Contains(s, StringComparison.OrdinalIgnoreCase))
						if (wild == null || !wild.Match(n.text)) { allMatch = false; continue; }
					r = new _Item(n.text, false);
				}
				if (r == null) continue;
				R ??= new _Item(parent.text, true) { isExpanded = true };
				R.AddChild(r);
			}
			return R;
		}

		_tv.SetItems(root2?.Children());
	}

//	internal void RecipeLinkClicked(string s) {
//#if DEBUG
//		if (_root == null) Panels.PanelManager[this].Visible = true;
//#endif
//		var v = _root.Descendants().FirstOrDefault(o => !o.dir && o.text.Like(s));
//		_tv.EnsureVisible(v);
//		_tv.Select(v);
//		_OpenRecipe(v);
//	}

	class _Item : TreeBase<_Item>, ITreeViewItem {
		internal readonly string text;
		internal readonly bool dir;
		internal bool isExpanded;

		public _Item(string text, bool dir) {
			this.text = text;
			this.dir = dir;
		}

		#region ITreeViewItem

		string ITreeViewItem.DisplayText => text;

		string ITreeViewItem.ImageSource => isExpanded ? @"resources/images/expanddown_16x.xaml" : (_IsFolder ? @"resources/images/expandright_16x.xaml" : null);

		void ITreeViewItem.SetIsExpanded(bool yes) { isExpanded = yes; }

		bool ITreeViewItem.IsExpanded => isExpanded;

		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();

		bool ITreeViewItem.IsFolder => _IsFolder;
		bool _IsFolder => base.HasChildren;

		#endregion
	}
}
