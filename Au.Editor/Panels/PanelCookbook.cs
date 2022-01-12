using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using System.Xml.Linq;

//CONSIDER: Cookbook is a bit too long if tab. Need a shorter name, eg Howto or Guide.
//	It seems "howto" is used for something long and detailed. Maybe "guide" too.
//	Cookbook maybe better, because recipes often are short. But it is often used for **specific** problems, not for learning of language basics.

class PanelCookbook : DockPanel
{
	KTreeView _tv;
	bool _loaded;
	string _cookbookPath;

	public PanelCookbook() {
		//this.UiaSetName("Cookbook panel"); //no UIA element for Panel. Use this in the future if this panel will be : UserControl.

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
			_tv.ItemActivated += _tv_ItemActivated;
		}
		base.OnPropertyChanged(e);
	}

	void _Load() {
		try {
			_cookbookPath = folders.ThisAppBS + "Cookbook\\files";
			var xr = XmlUtil.LoadElem(_cookbookPath + ".xml");

			var root = new _Item(null, true);
			_AddItems(xr, root);

			static void _AddItems(XElement xp, _Item ip) {
				foreach (var x in xp.Elements()) {
					var tag = x.Name.LocalName;
					bool dir = tag == "d";
					if (!dir && tag != "s") continue;
					var name = x.Attr("n"); if (!dir) name = name[..^3];
					var i = new _Item(name, dir);
					ip.AddChild(i);
					if (dir) _AddItems(x, i);
				}
			}

			_tv.SetItems(root.Children());
		}
		catch { }
	}

	private void _tv_ItemActivated(object sender, TVItemEventArgs e) {
		var recipe = e.Item as _Item;
		if (recipe.dir) return;

		//get file path
		var stack = new Stack<string>();
		stack.Push(recipe.text + ".cs");
		for (var p = recipe.Parent; p.HasParent; p = p.Parent) stack.Push(p.text);
		stack.Push(_cookbookPath);
		var path = string.Join("\\", stack);
		//print.it(path, filesystem.exists(path).isFile);

		try {
			var code = filesystem.loadText(path);
			//print.it(code);
			Panels.Recipe.SetText(code);
		}
		catch { }
	}

	class _Item : TreeBase<_Item>, ITreeViewItem
	{
		internal readonly string text;
		internal readonly bool dir;
		bool _isExpanded;

		public _Item(string text, bool dir) {
			this.text = text;
			this.dir = dir;
		}

		#region ITreeViewItem

		string ITreeViewItem.DisplayText => text;

		string ITreeViewItem.ImageSource => _isExpanded ? @"resources/images/expanddown_16x.xaml" : (_IsFolder ? @"resources/images/expandright_16x.xaml" : null);

		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }

		bool ITreeViewItem.IsExpanded => _isExpanded;

		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();

		bool ITreeViewItem.IsFolder => _IsFolder;
		bool _IsFolder => base.HasChildren;

		#endregion
	}
}
