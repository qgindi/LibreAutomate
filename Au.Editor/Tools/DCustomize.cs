using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Au.Controls;
using Au.Tools;
using System.Xml.Linq;

#if SCRIPT
namespace Script;
#endif

class DCustomize : KDialogWindow {
	public static void ShowSingle(string commandName = null) {
		var d = ShowSingle(() => new DCustomize());
		if (commandName != null) d._SelectAndOpen(commandName);
	}
	
	/// <summary>
	/// If the dialog is open, sets its Image field text and returns true. Called from the Icons dialog.
	/// </summary>
	public static bool AaSetImage(string s) {
		if (!GetSingle(out DCustomize d)) return false;
		if (d._panelProp.IsVisible) d._tImage.Text = s;
		return true;
	}
	
	List<_Custom> _tree;
	KSciInfoBox _info;
	
	ContextMenu _menu;
	Dictionary<string, _Default> _dict = new();
	
	KTreeView _tv;
	Panel _panelProp;
	TextBox _tColor, _tText, _tImage, _tKeys, _tBtext;
	TextBox _tDefText, _tDefImage, _tDefKeys;
	KCheckBox _cSeparator;
	ComboBox _cbHide, _cbImageAt;
	Label _lCommandPath;
	
	_Custom _ti; //current item
	_Custom _clip; //cut/copy item
	bool _ignoreEvents;
	
	DCustomize() {
		InitWinProp("Customize", App.Wmain);
		
		var b = new wpfBuilder(this).WinSize(700, 700).Columns(220, 0, -1);
		b.Row(-1);
		
		b.xAddInBorder(_tv = new());
		b.Add<GridSplitter>().Splitter(vertical: true);
		
		b.StartGrid().Columns(-1); //right side
		
		b.Row(84).Add(out _info);
		b.AddSeparator(vertical: false);
		
		b.Row(-1).StartStack(vertical: true).Hidden();
		_panelProp = b.Panel;
		
		b.Add(out _lCommandPath).Padding("4").Brush(Brushes.LightBlue, Brushes.Black);
		b.StartGrid<KGroupBox>("Properties common to menu item and toolbar button").Columns(0, -1, 30, 30);
		b.R.Add("Text", out _tText).Tooltip("Text.\nInsert _ before Alt-underlined character.");
		b.R.Add("Color", out _tColor).Tooltip("Text color.\nCan be a .NET color name or #RRGGBB or #RGB.")
			.xAddButtonIcon("*MaterialDesign.ColorLens" + Menus.green, _ => KColorPicker.ColorTool(s => { _tColor.Text = s; }, b.Window, modal: true, add0xRgbButton: false, addBgrButton: false), "Colors"); b.Span(1);
		b.R.Add("Image", out _tImage).Tooltip("Icon name etc.\nSee ImageUtil.LoadWpfImageElement.")
			.xAddButtonIcon(Menus.iconIcons, _ => { _tImage.SelectAll(); DIcons.ShowSingle(); }, "Icons tool.\nSelect an icon and click button 'Menu or toolbar item'."); b.Span(1);
		b.R.Add("Keys", out _tKeys).Tooltip("Keyboard or/and mouse shortcut(s), like Ctrl+E, Shift+M-click.\nSee keys.more.parseHotkeyString.")
			.xAddButtonIcon("*Material.KeyboardOutline" + Menus.green, _ => _KeysTool(), "Keys tool");
		b.xAddButtonIcon("*FeatherIcons.Eye" + Menus.blue, _ => _KeysList(), "Existing hotkeys");
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
		
		b.StartGrid<KGroupBox>("Toolbar button properties").Columns(0, 100, -1);
		b.R.Add("Text", out _tBtext).Multiline(wrap: TextWrapping.NoWrap).Tooltip("Button text, if different than menu item text.");
		b.R.Add("Image at", out _cbImageAt).Items("", "left", "top", "right", "bottom").Span(1).Tooltip("Display image + text and put image at this side.\nFor submenu-items always left.");
		b.R.Add(out _cSeparator, "Separator before");
		b.R.Add("Hide", out _cbHide).Items("", "always", "never").Span(1).Tooltip("When to move the button to the overflow dropdown.\nIf empty - when the toolbar is too small.");
		b.End();
		
		b.StartGrid<KGroupBox>("Move");
		b.R.AddButton("Up", _ => _Move(_ti, true)).Tooltip("Shift+Up");
		b.R.AddButton("Down", _ => _Move(_ti, false)).Tooltip("Shift+Down");
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
		_tv.KeyDown += _tv_KeyDown;
		_FillTree();
		
		foreach (var v in new Control[] { _tText, _tColor, _tImage, _tKeys, _tBtext, _cbImageAt, _cbHide, _cSeparator }) {
			void _Update() { if (!_ignoreEvents) { _GetControlValues(); _tv.Redraw(); } }
			if (v is TextBox tb) tb.TextChanged += (_, _) => _Update();
			else if (v is ComboBox cb) cb.SelectionChanged += (_, _) => _Update();
			else if (v is KCheckBox ch) ch.CheckChanged += (_, _) => _Update();
		}
		
		b.Loaded += () => {
			_info.aaaText = $"""
Here you can edit menus, toolbars and hotkeys of the main window.

<b>menu</b> - customized menu items. Right-click...
<b>File, etc</b> - toolbars. Right-click to add a button.

Menu items cannot be removed or reordered. Default toolbar buttons cannot be removed, but you can edit, reorder and hide.

Text color in the list: blue - customized; gray - hidden button; red - cut.

You also can edit the <explore {App.Commands.UserFile}>file<> in an XML editor. To reset everything, delete the file. To reset an item or toolbar, remove it from XML.
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
			foreach (var v in a.OrderBy(o => o.Value, StringComparer.OrdinalIgnoreCase)) {
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
		_lCommandPath.Content = string.Join(" > ", path);
		
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
		var c = _tree.SelectMany(o => o.Children()).LastOrDefault(o => o.def.name == commandName); //Last to prefer toolbar button and not menu item
		if (c != null) _SelectAndOpen(c);
	}
	
	void _tv_ItemClick(TVItemEventArgs e) {
		var t = e.Item as _Custom;
		if (e.Button == MouseButton.Right) {
			var m = new popupMenu();
			
			var c0 = t.Level == 0 ? t : t.Parent;
			m[c0.isMenu ? "Customize menu item..." : "Add button..."] = o => { _menu.PlacementTarget = this; _menu.IsOpen = true; };
			if (t.Level == 1) {
				m.Separator();
				m["Cut"] = o => _Cut(t);
				_MiPaste(false);
				m.AddCheck("Hide", t.hide == "always", o => _Hide(t, o.IsChecked));
				if (!_IsDefaultButton(t)) {
					m.Submenu("Delete", m => {
						m["Delete"] = o => _Delete(t, ask: false);
					});
				}
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
		if (_clip == null || _clip == where) return false;
		if (_IsDefaultButton(_clip) && where != _clip.Parent && where.Parent != _clip.Parent) return false; //don't move to another toolbar
		return true;
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
			if (_IsDefaultButton(t)) return; //don't move to another toolbar
			int i = _tree.IndexOf(t.Parent) + (up ? -1 : 1);
			if ((uint)i >= _tree.Count) return;
			where = _tree[i];
		}
		t.Remove();
		if (where.Level == 0) where.AddChild(t, first: !up); else where.AddSibling(t, after: !up);
		_tv.SetItems(_tree, modified: true);
		_tv.EnsureVisible(t);
	}
	
	void _Delete(_Custom t, bool ask) {
		if (!_IsDefaultButton(t)) {
			if (ask && !dialog.showOkCancel("Delete?", t.displayText, owner: this)) return;
			if (t == _ti) _panelProp.Visibility = Visibility.Hidden;
			_clip = null;
			t.Remove();
			_tv.SetItems(_tree, true);
		} else if (t == _ti) {
			_cbHide.SelectedIndex = 1;
		} else {
			t.hide = "always";
			_tv.Redraw(t);
		}
	}
	
	void _Hide(_Custom t, bool hide) {
		if (t == _ti) {
			_cbHide.SelectedIndex = hide ? 1 : 0;
		} else {
			t.hide = hide ? "always" : null;
			_tv.Redraw(t);
		}
	}
	
	bool _IsDefaultButton(_Custom t) {
		if (t.Level > 0 && !t.isMenu) {
			_xdefault ??= XmlUtil.LoadElem(App.Commands.DefaultFile);
			return _xdefault.Element(t.Parent.displayText)?.Element(t.def.name) != null;
		}
		return false;
	}
	XElement _xdefault;
	
	void _FillMenu() {
		_Menu(Panels.Menu, _menu = new(), null, 0);
		
		void _Menu(ItemsControl sourceMenu, ItemsControl destMenu, _Default parentCommand, int level) {
			foreach (var o in sourceMenu.Items) {
				if (o is not MenuItem ms || ms.Command is not KMenuCommands.Command c) continue;
				string name = c.Name;
				
				var def = new _Default(name, c.Attribute, c.Text, parentCommand);
				_dict.Add(name, def);
				
				var m = new MenuItem { Name = name, Header = c.Text };
				destMenu.Items.Add(m);
				
				if (ms.Role is MenuItemRole.SubmenuHeader or MenuItemRole.TopLevelHeader) {
					if (level > 0) m.PreviewMouseLeftButtonUp += (o, e) => { if (e.Source == o) { this.Focus(); _AddToCustomized(def, _contextMenuOwner); }; };
					
					_Menu(ms, m, def, level + 1);
				} else {
					m.Click += (o, _) => _AddToCustomized(def, _contextMenuOwner);
				}
			}
			//}
		}
	}
	
	void _FillTree() {
		_tree = new();
		var a = App.Commands.LoadFiles(); if (a == null) return;
		
		foreach (var xx in a) {
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
		xr.SaveElem(App.Commands.UserFile);
	}
	
	//default properties (Menus member name and attributes)
	record _Default(string name, CommandAttribute attr, string text, _Default parent);
	
	//customized properties (in XML file)
	class _Custom : TreeBase<_Custom>, ITreeViewItem {
		readonly DCustomize _d;
		public readonly _Default def;
		public string displayText;
		public readonly bool isMenu;
		bool _isExpanded;
		//customized properties
		public bool separator;
		public string ctext, color, image, keys, btext, imageAt, hide;
		
		public _Custom(DCustomize d, _Default def, bool isMenu, string displayText = null) {
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
		
		object ITreeViewItem.Image
			=> Level == 0 ? EdResources.FolderArrow(_isExpanded) : (image ?? def.attr.image);
		
		int ITreeViewItem.Color(TVColorInfo ci)
			=> Level > 0 ? -1 : isMenu ? 0xF0C080 : 0xC0E0A0;
		
		int ITreeViewItem.TextColor(TVColorInfo ci)
			=> this == _d._clip ? 0xFF0000
			: hide == "always" ? 0x808080
			: IsCustomized() ? (ci.isHighContrastDark ? 0xFFFF00 : 0x0000FF)
			: Level == 0 ? 0
			: -1;
		
		#endregion
		
		public bool IsCustomized() {
			if (def == null) return false;
			if (separator && isMenu) return true; //never mind: or may be specified in the default XML file, but that info now is lost.
			return ctext != null || color != null || image != null || keys != null || btext != null || imageAt != null || hide != null;
		}
	}
	
	void _tv_KeyDown(object sender, KeyEventArgs e) {
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
				if (t.Level > 0) _Delete(t, ask: true);
				break;
			case (ModifierKeys.Control, Key.X):
				if (t.Level > 0) _Cut(t);
				break;
			case (ModifierKeys.Control, Key.V):
				if (_CanPaste(t)) _Paste(t);
				break;
			case (ModifierKeys.Shift, Key.Up):
				if (t.Level > 0) _Move(t, up: true);
				break;
			case (ModifierKeys.Shift, Key.Down):
				if (t.Level > 0) _Move(t, up: false);
				break;
			default: return;
			}
		}
		gh:
		e.Handled = true;
	}
	
	public static void ToolbarContextMenuOpening(object sender, ContextMenuEventArgs _) {
		var toolbar = sender as ToolBar;
		FrameworkElement e = null;
		ICommand ic = null;
		
		//WPF does not have a better way to get the right-clicked element when it is disabled. Mouse.DirectlyOver etc return the container.
		var xy = mouse.xy;
		foreach (FrameworkElement v in toolbar.Items) {
			if (v.IsVisible && v is System.Windows.Controls.Primitives.ButtonBase or Border && v.RectInScreen().Contains(xy)) {
				e = v;
				if (v is ButtonBase b1) ic = b1.Command;
				else if (v is Border b2 && b2.Child is Menu m2) ic = m2.Items.OfType<MenuItem>().SingleOrDefault()?.Command;
				break;
			}
		}
		if (ic is not KMenuCommands.Command c) return;
		
#if true
		var m = new popupMenu();
		m["Customize..."] = o => DCustomize.ShowSingle(c.Name);
		m.Show();
#else //rejected: menu item "Hide". The #else code hides/shows the button, but also need to manage its separator, and add menu item "Separator before".
		var a = App.Commands.LoadFiles(); if (a == null) return;
		if (a.FirstOrDefault(o => o.Name == toolbar.Name)?.Element(c.Name) is not { } x) return;
		
		OverflowMode hide1 = x.Attr("hide") switch { "always" => OverflowMode.Always, "never" => OverflowMode.Never, _ => OverflowMode.AsNeeded };
		
		var m = new popupMenu();
		m["Customize"] = o => DCustomize.ShowSingle(c.Name);
		m.Submenu("Hide", m => {
			m.AddRadio("Always", hide1 == OverflowMode.Always, _Hide);
			m.AddRadio("Never", hide1 == OverflowMode.Never, _Hide);
			m.AddRadio("Auto", hide1 == OverflowMode.AsNeeded, _Hide);
		});
		m.Show();
		
		void _Hide(PMItem k) {
			OverflowMode hide2 = k.Text switch { "Always" => OverflowMode.Always, "Never" => OverflowMode.Never, _ => OverflowMode.AsNeeded };
			if (hide2 != hide1) {
				x.SetAttributeValue("hide", hide2 switch { OverflowMode.Always => "always", OverflowMode.Never => "never", _ => null });
				var xr = new XElement("commands", a);
				xr.SaveElem(App.Commands.UserFile);
				
				bool isOfi1 = ToolBar.GetIsOverflowItem(e);
				ToolBar.SetOverflowMode(e, hide2);
				bool isOfi2 = ToolBar.GetIsOverflowItem(e);
				print.it(isOfi1, isOfi2); //updated async
				//if (isOfi2!=isOfi1) {
				//	print.it(isOfi2);
				//}
				//todo: separator
			}
		}
#endif
	}
}
