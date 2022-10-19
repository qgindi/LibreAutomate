//FUTURE: allow to add scripts to toolbars.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Au.Controls;
using Au.Tools;
using System.Xml.Linq;
using System.Windows.Documents;
using System.ComponentModel;
using System.Reflection;

#if SCRIPT
namespace Script;
#endif

class DCommands : KDialogWindow {
	public static void ZShow(string commandName = null) {
		if (s_dialog == null) {
			s_dialog = new();
			s_dialog.Show();
		} else {
			s_dialog.Hwnd().ActivateL(true);
		}
		if (commandName != null) s_dialog._SelectAndOpen(commandName);
	}
	static DCommands s_dialog;
	
	protected override void OnClosed(EventArgs e) {
		s_dialog = null;
		base.OnClosed(e);
	}
	
	/// <summary>
	/// If the dialog is open, sets its Image field text and returns true;
	/// </summary>
	public static bool ZSetImage(string s) {
		if (s_dialog == null) return false;
		if (s_dialog._panelProp.IsVisible) s_dialog._tImage.Text = s;
		return true;
	}
	
	List<_Custom> _tree;
	KSciInfoBox _info;
	
	ContextMenu _menu;
	Dictionary<string, _Default> _dict = new();
	
	_KTreeView _tv;
	Panel _panelProp;
	TextBox _tColor, _tText, _tImage, _tKeys, _tBtext;
	TextBox _tDefText, _tDefImage, _tDefKeys;
	KCheckBox _cSeparator;
	ComboBox _cbHide, _cbImageAt;
	Label _lCommandPath;
	
	_Custom _ti; //current item
	_Custom _clip; //cut/copy item
	bool _ignoreEvents;
	
	///
	public DCommands() {
		Title = "Customize";
		Owner = App.Wmain;
		ShowInTaskbar = false;
		
		var b = new wpfBuilder(this).WinSize(700, 700).Columns(220, 0, -1);
		b.Row(-1);
		
		b.xAddInBorder(_tv = new(this));
		b.Add<GridSplitter>().Splitter(vertical: true);
		
		b.StartGrid().Columns(-1); //right side
		
		b.Row(84).Add(out _info);
		b.AddSeparator(vertical: false);
		
		b.Row(-1).StartStack(vertical: true).Hidden();
		_panelProp = b.Panel;
		
		b.Add(out _lCommandPath).Padding("4").Brush(Brushes.LightBlue);
		b.StartGrid<GroupBox>("Properties common to menu item and toolbar button").Columns(0, -1, 30, 30);
		b.R.Add("Text", out _tText).Tooltip("Text.\nInsert _ before Alt-underlined character.");
		b.R.Add("Color", out _tColor).Tooltip("Text color.\nCan be a .NET color name or #RRGGBB or #RGB.")
			.xAddButtonIcon("*MaterialDesign.ColorLens #99BF00", _ => _ColorTool(), "Colors"); b.Span(1);
		b.R.Add("Image", out _tImage).Tooltip("Icon name etc.\nSee ImageUtil.LoadWpfImageElement.")
			.xAddButtonIcon("*FontAwesome.IconsSolid #99BF00", _ => { _tImage.SelectAll(); DIcons.ZShow(); }, "Icons tool.\nSelect an icon, expand 'Code...' and click 'Menu...'."); b.Span(1);
		b.R.Add("Keys", out _tKeys).Tooltip("Keyboard or/and mouse shortcut(s), like Ctrl+E, Shift+M-click.\nSee keys.more.parseHotkeyString.")
			.xAddButtonIcon("*Material.KeyboardOutline #99BF00", _ => _KeysTool(), "Keys tool");
		b.xAddButtonIcon("*FeatherIcons.Eye #008EEE", _ => _KeysList(), "Existing hotkeys");
		b.StartGrid<Expander>("Default");
		b.R.Add("Text", out _tDefText); _Readonly(_tDefText);
		b.R.Add("Image", out _tDefImage); _Readonly(_tDefImage);
		b.R.Add("Keys", out _tDefKeys); _Readonly(_tDefKeys);
		static void _Readonly(TextBox k) {
			k.IsReadOnly = true;
			k.IsReadOnlyCaretVisible = true;
			k.Background = SystemColors.ControlBrush;
		}
		b.End();
		b.End();
		
		b.StartGrid<GroupBox>("Toolbar button properties");
		b.R.Add("Text", out _tBtext).Tooltip("Button text, if different than menu item text.");
		b.R.Add("Image at", out _cbImageAt).Items("", "left", "top", "right", "bottom").Tooltip("Display image + text and put image at this side.\nFor submenu-items always left.");
		b.R.Add(out _cSeparator, "Separator before");
		b.R.Add("Hide", out _cbHide).Items("", "always", "never").Tooltip("When to move the button to the overflow dropdown.\nIf empty - when the toolbar is too small.");
		b.End();
		
		b.StartGrid<GroupBox>("Move");
		b.R.AddButton("Up", _ => _Move(_ti, true)).Tooltip("Ctrl+Up");
		b.R.AddButton("Down", _ => _Move(_ti, false)).Tooltip("Ctrl+Down");
		b.End().Width(90).Align("L");
		
		b.End();
		
		b.R.AddSeparator(vertical: false);
		b.R.StartOkCancel()
			.AddButton(out var bOK, "Save", _ => { _Save(); }).Width(70).Tooltip("Saves changes. Will be applied when the program starts next time.")
			.AddButton(out var bOK2, "Save and restart", _ => { _Save(); Close(); App.Restart(); }).Width(120).Tooltip("Saves changes and restarts the program.")
			.AddButton(out var bCancel, "Cancel", _ => Close()).Width(70)
			.End();
		
		b.End(); //right side
		b.End();
		
		_FillMenu();
		
		_tv.SingleClickActivate = true;
		_tv.ItemActivated += _tv_ItemActivated;
		_tv.ItemClick += _tv_ItemClick;
		_FillTree();
		
		foreach (var v in new Control[] { _tText, _tColor, _tImage, _tKeys, _tBtext, _cbImageAt, _cbHide, _cSeparator }) {
			void _Update() { if (!_ignoreEvents) { _GetControlValues(); _tv.Redraw(); } }
			if (v is TextBox tb) tb.TextChanged += (_, _) => _Update();
			else if (v is ComboBox cb) cb.SelectionChanged += (_, _) => _Update();
			else if (v is KCheckBox ch) ch.CheckChanged += (_, _) => _Update();
		}
		
		b.Loaded += () => {
			_info.zText = $"""
Here you can edit menus, toolbars and hotkeys of the main window.

<b>menu</b> - customized menu items. To edit a menu item, add or select it here (right-click...) or in a toolbar.
<b>File, etc</b> - toolbars. Right-click to add a button.

Menu items cannot be removed or reordered. Default toolbar buttons cannot be removed, but you can edit, reorder and hide.

Text color in the list: blue - customized; gray - hidden button; red - cut.

The data is in these files: <explore {defFile}>default<>, <explore {userFile}>user<>. You also can edit the user file in an XML editor. To reset everything, delete the user file. To reset an item or toolbar, remove it from the XML.
""";
			
		};
		
		void _KeysTool() {
			_tKeys.SelectAll();
			_tKeys.Focus();
			var k = new KeysWindow { InsertInControl = _tKeys, ClickClose = KPopup.CC.Outside, CloseHides = true };
			k.SetFormat(PSFormat.Hotkey);
			k.ShowByRect(_tKeys, Dock.Bottom);
		}
		
		void _KeysList() {
			var m = new popupMenu();
			var a = new Dictionary<_Default, string>();
			//add default hotkeys to a
			foreach (var v in _dict) {
				if (v.Value.attr.keys is string k) a.Add(v.Value, k);
			}
			//add custom hotkeys to a
			foreach (var v in _tree.SelectMany(o => o.Children())) {
				if (v.keys is string k) {
					if (k == "") a.Remove(v.def); else a[v.def] = k;
				}
			}
			//add all to menu
			foreach (var v in a.OrderBy(o => o.Value)) {
				var u = m.Add(v.Key.text + "\t" + v.Value, o => {
					var d = o.Tag as _Default;
					var c = _tree.SelectMany(o => o.Children()).FirstOrDefault(o => o.def == d);
					if (c == null) {
						if (!dialog.showOkCancel("Customize this menu item?", d.text, owner: this)) return;
						_AddToCustomized(d, _tree.Find(o => o.isMenu));
					} else {
						_SelectAndOpen(c);
					}
				});
				u.Tag = v.Key;
				if (v.Value != v.Key.attr.keys) u.TextColor = Colors.Blue; //customized
			}
			m.Show(owner: this);
		}
		
		void _ColorTool() {
			var m = new popupMenu();
			var a = typeof(Colors).GetProperties();
			for (int i = 0; i < a.Length; i++) {
				if (a[i].GetValue(0) is Color c) {
					var k = m.Add(i + 1, a[i].Name);
					k.TextColor = c;
					if (ColorInt.GetPerceivedBrightness(k.TextColor.argb, false) > .85) k.BackgroundColor = 0;
				}
			}
			int j = m.Show() - 1; if (j < 0) return;
			_tColor.Text = a[j].Name;
		}
	}
	
	void _tv_ItemActivated(TVItemEventArgs e) => _Open(e.Item as _Custom);
	
	void _Open(_Custom c) {
		//print.it(c.name);
		if (c.Level == 0) {
			_panelProp.Visibility = Visibility.Hidden;
			return;
		}
		_ti = c;
		_ignoreEvents = true;
		_panelProp.Visibility = Visibility.Visible;
		
		var path = new Stack<string>();
		for (var v = c.def; v != null; v = v.parent) path.Push(v.text);
		_lCommandPath.Content = string.Join(" -> ", path);
		
		//common
		_tText.Text = c.ctext ?? c.def.text;
		_tColor.Text = c.color;
		_tImage.Text = c.image ?? c.def.attr.image;
		_tKeys.Text = c.keys ?? c.def.attr.keys;
		_tDefText.Text = c.def.text;
		_tDefImage.Text = c.def.attr.image;
		_tDefKeys.Text = c.def.attr.keys;
		//button
		_tBtext.Text = c.btext;
		_cbImageAt.SelectedIndex = c.imageAt switch { "left" => 1, "top" => 2, "right" => 3, "bottom" => 4, _ => 0 };
		_cSeparator.IsChecked = c.separator;
		_cbHide.SelectedIndex = c.hide switch { "always" => 1, "never" => 2, _ => 0 };
		
		_ignoreEvents = false;
	}
	
	void _GetControlValues() {
		var c = _ti;
		c.ctext = _Text(_tText, c.def.text);
		c.color = _Text(_tColor);
		c.image = _Text(_tImage, c.def.attr.image);
		c.keys = _Text(_tKeys, c.def.attr.keys);
		c.btext = _Text(_tBtext);
		c.imageAt = _Text(_cbImageAt);
		c.separator = _cSeparator.IsChecked;
		c.hide = _Text(_cbHide);
		
		string _Text(Control tbcb, string def = null) {
			var s = tbcb switch { TextBox tb => tb.Text, ComboBox cb => cb.SelectedValue as string, _ => null };
			s = s.NullIfEmpty_();
			if (def == null) return s;
			if (s == def) return null;
			return s ?? "";
		}
	}
	
	void _SelectAndOpen(_Custom t) {
		_tv.SelectSingle(t, andFocus: true);
		_tv.EnsureVisible(t);
		if (t != _ti) _Open(t);
	}
	
	void _SelectAndOpen(string commandName) {
		var c = _tree.SelectMany(o => o.Children()).FirstOrDefault(o => o.def.name == commandName);
		if (c != null) _SelectAndOpen(c);
	}
	
	void _tv_ItemClick(TVItemEventArgs e) {
		var t = e.Item as _Custom;
		if (e.Button == MouseButton.Right) {
			var m = new popupMenu();
			
			var c0 = t.Level == 0 ? t : t.Parent;
			m[c0.isMenu ? "Add to customized..." : "Add button..."] = o => { _menu.PlacementTarget = this; _menu.IsOpen = true; };
			if (t.Level == 1) {
				m.Separator();
				m["Cut"] = o => _Cut(t);
				_MiPaste(false);
				m.Submenu("Delete", m => {
					m["Delete " + t.displayText] = o => _Delete(t);
				});
			} else {
				_MiPaste(true);
			}
			
			void _MiPaste(bool into) {
				if (_CanPaste(t)) m["Paste"] = o => _Paste(t);
			}
			
			_contextMenuOwner = t;
			m.Show(owner: this);
		}
	}
	
	_Custom _contextMenuOwner;
	
	void _Cut(_Custom t) {
		if (_clip != null) _tv.Redraw(_clip);
		_clip = t;
		_tv.Redraw(t); //red etc
	}
	
	bool _CanPaste(_Custom where) {
		return _clip != null && _clip != where;
	}
	
	void _Paste(_Custom where) {
		_clip.Remove();
		if (where.Level == 0) where.AddChild(_clip); else where.AddSibling(_clip, after: false);
		_clip = null;
		_tv.SetItems(_tree, modified: true);
	}
	
	void _AddToCustomized(_Default def, _Custom where) {
		var c0 = where.Level == 0 ? where : where.Parent;
		var c = c0.Children().FirstOrDefault(o => o.def == def);
		if (c == null) {
			c = new _Custom(this, def, c0.isMenu);
			if (c0 == where) where.AddChild(c); else where.AddSibling(c, after: false);
			
			_tv.SetItems(_tree, modified: true);
		}
		
		_SelectAndOpen(c);
	}
	
	void _Move(_Custom t, bool up) {
		var where = up ? t.Previous : t.Next;
		if (where == null) {
			int i = _tree.IndexOf(t.Parent) + (up ? -1 : 1);
			if ((uint)i >= _tree.Count) return;
			where = _tree[i];
		}
		t.Remove();
		if (where.Level == 0) where.AddChild(t, first: !up); else where.AddSibling(t, after: !up);
		_tv.SetItems(_tree, modified: true);
		_tv.EnsureVisible(t);
	}
	
	void _Delete(_Custom t) {
		if (t == _ti) _panelProp.Visibility = Visibility.Hidden;
		_clip = null;
		t.Remove();
		_tv.SetItems(_tree, true);
	}
	
	void _FillMenu() {
		_Menu(typeof(Menus), _menu = new(), null, 0);
		
		void _Menu(Type type, ItemsControl parentMenu, _Default parentCommand, int level) {
			var am = type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
			
			if (am.Length == 0) { //dynamic submenu
				parentMenu.Items.Add(new Separator());
				return;
			}
			
			var list = new List<(MemberInfo mi, CommandAttribute a)>(am.Length);
			foreach (var mi in am) {
				var ca = mi.GetCustomAttribute<CommandAttribute>(false);
				if (ca != null) list.Add((mi, ca));
			}
			
			foreach (var (mi, ca) in list.OrderBy(o => o.a.order_)) {
				string name = ca.name ?? mi.Name;
				
				if (ca.separator && !ca.hide) parentMenu.Items.Add(new Separator());
				
				var (text, buttonText) = KMenuCommands.GetTextFromAttributes(ca, mi);
				
				var def = new _Default(name, ca, text, buttonText, parentCommand);
				_dict.Add(name, def);
				
				var m = new MenuItem { Name = name, Header = text };
				parentMenu.Items.Add(m);
				
				if (mi is TypeInfo ti) {
					if (level > 0) m.PreviewMouseLeftButtonUp += (o, e) => { if (e.Source == o) { this.Focus(); _AddToCustomized(def, _contextMenuOwner); }; };
					
					_Menu(ti, m, def, level + 1);
				} else {
					m.Click += (o, _) => _AddToCustomized(def, _contextMenuOwner);
				}
			}
		}
	}
	
#if SCRIPT
	static string defFile = @"C:\code\au\_\Default\Commands.xml";
	static string userFile = @"C:\Users\G\Documents\LibreAutomate\.settings\Commands.xml";
#else
	static string defFile = folders.ThisAppBS + @"Default\Commands.xml";
	static string userFile = AppSettings.DirBS + "Commands.xml";
#endif
	
	void _FillTree() {
		_tree = new();
		var ax = KMenuCommands.LoadFiles(defFile, userFile);
		if (ax == null) return;
		foreach (var xx in ax) {
			var s1 = xx.Name.LocalName;
			bool isMenu = s1 == "menu";
			var vv = new _Custom(this, null, isMenu, s1);
			_tree.Add(vv);
			foreach (var x in xx.Elements()) {
				if (!_dict.TryGetValue(x.Name.LocalName, out var def)) continue;
				var c = new _Custom(this, def, isMenu) {
					color = x.Attr("color"),
					ctext = x.Attr("text"),
					image = x.Attr("image"),
					keys = x.Attr("keys"),
					hide = x.Attr("hide"),
					imageAt = x.Attr("imageAt"),
					separator = x.HasAttr("separator"),
					btext = x.Attr("btext"),
				};
				vv.AddChild(c);
			}
		}
		
		_tv.SetItems(_tree);
	}
	
	void _Save() {
		var xr = new XElement("commands");
		foreach (var t in _tree) {
			var xt = new XElement(t.displayText);
			xr.Add(xt);
			foreach (var c in t.Children()) {
				if (c.isMenu && !c.IsCustomized()) continue;
				var x = new XElement(c.def.name);
				xt.Add(x);
				if (c.separator) x.SetAttributeValue("separator", "");
				_Set("text", c.ctext);
				_Set("color", c.color);
				_Set("image", c.image);
				_Set("keys", c.keys);
				_Set("btext", c.btext);
				_Set("imageAt", c.imageAt);
				_Set("hide", c.hide);
				
				void _Set(string attr, string s) {
					if (s != null) x.SetAttributeValue(attr, s);
				}
			}
		}
		//print.clear();
		//print.it(xr);
		xr.SaveElem(userFile);
	}
	
	//default properties (Menus member name and attributes)
	record _Default(string name, CommandAttribute attr, string text, string buttonText, _Default parent);
	
	//customized properties (in XML file)
	class _Custom : TreeBase<_Custom>, ITreeViewItem {
		readonly DCommands _d;
		public readonly _Default def;
		public string displayText;
		public readonly bool isMenu;
		bool _isExpanded;
		//customized properties
		public bool separator;
		public string ctext, color, image, keys, btext, imageAt, hide;
		
		public _Custom(DCommands d, _Default def, bool isMenu, string displayText = null) {
			_d = d;
			this.def = def;
			this.isMenu = isMenu;
			this.displayText = displayText ?? StringUtil.RemoveUnderlineChar(def.text, '_');
			_isExpanded = def == null;
		}
		
		#region ITreeViewItem
		
		void ITreeViewItem.SetIsExpanded(bool yes) { _isExpanded = yes; }
		
		public bool IsExpanded => _isExpanded;
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		
		public bool IsFolder => base.HasChildren;
		
		string ITreeViewItem.DisplayText => displayText;
		
		object ITreeViewItem.Image => Level == 0 ? (_isExpanded ? @"resources/images/expanddown_16x.xaml" : @"resources/images/expandright_16x.xaml") : (image ?? def.attr.image);
		
		int ITreeViewItem.Color => Level > 0 ? -1 : isMenu ? 0x80C0F0 : 0xA0E0C0;
		
		int ITreeViewItem.TextColor => this == _d._clip ? 0xFF : (hide == "always" ? 0x808080 : IsCustomized() ? 0xFF0000 : -1);
		
		#endregion
		
		public bool IsCustomized() {
			if (def == null) return false;
			if (separator && isMenu) return true; //never mind: or may be specified in the default XML file, but that info now is lost.
			return ctext != null || color != null || image != null || keys != null || btext != null || imageAt != null || hide != null;
		}
	}
	
	class _KTreeView : KTreeView {
		DCommands _d;
		
		public _KTreeView(DCommands d) {
			_d = d;
		}
		
		protected override void OnKeyDown(KeyEventArgs e) {
			if (!e.Handled) _d._OnKeyDown(e);
			base.OnKeyDown(e);
		}
	}
	
	void _OnKeyDown(KeyEventArgs e) {
		var k = (e.KeyboardDevice.Modifiers, e.Key);
		switch (k) {
		case (0, Key.Escape):
			if (_clip != null) {
				_clip = null;
				_tv.Redraw();
			}
			goto gh;
		}
		if (_tv.FocusedItem is _Custom t) {
			switch (k) {
			case (0, Key.Delete):
				if (t.Level > 0 && dialog.showOkCancel("Delete?", t.displayText, owner: this)) _Delete(t);
				break;
			case (ModifierKeys.Control, Key.X):
				if (t.Level > 0) _Cut(t);
				break;
			case (ModifierKeys.Control, Key.V):
				if (_CanPaste(t)) _Paste(t);
				break;
			case (ModifierKeys.Control, Key.Up):
				if (t.Level > 0) _Move(t, up: true);
				break;
			case (ModifierKeys.Control, Key.Down):
				if (t.Level > 0) _Move(t, up: false);
				break;
			default: return;
			}
		}
		gh:
		e.Handled = true;
	}
}
