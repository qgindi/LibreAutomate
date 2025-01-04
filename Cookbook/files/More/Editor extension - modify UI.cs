/// Scripts with role <b>editorExtension<> can modify the UI of the script editor. This script contains examples of:
/// - Show/hide panels and toolbars.
/// - Add buttons to toolbars.
/// - Add menus to the menubar.
/// - Add controls to panels.
/// - Add panels and toolbars.
/// To run at startup, add the script name in <b>Options > Workspace > Run scripts when workspace loaded<>.
/// These menus and toolbars cannot be customized in menu <b>Tools > Customize<>.

/*/
role editorExtension;
testInternal Au.Editor;
r Au.Editor.dll;
r Au.Controls.dll;
/*/
using Au.Controls;
using System.Windows;
using System.Windows.Controls;

EditorExtension.WindowLoaded += () => _Load();

static void _Load() {
	
	// Show or hide a toolbar or panel.
	// Toolbar names: menu Tools > Customize. Panel names: displayed in UI.
	
	Panels.PanelManager["Custom2"].Visible = true;
	
	// Add buttons to a toolbar.
	
	const string tag = "wbLij6N5Y0KqcLPd0gTcBA";
	var tb = Panels.TCustom2;
	_RemoveOld(tb);
	tb.Items.Add(_TBButton("*MaterialDesign.History #0D69E1", "Button tooltip", _ => { print.it("click"); }));
	tb.Items.Add(_TBCheckbox("*Modern.Stream #0D69E1", "Checkbox tooltip", c => { print.it("checked", c.IsChecked); }));
	
	//Removes elements added by this script previously.
	void _RemoveOld(ItemsControl t) {
		foreach (var e in t.Items.OfType<FrameworkElement>().ToArray()) if (e.Tag is string s1 && s1 == tag) t.Items.Remove(e);
	}
	
	//Creates a toolbar button with icon and tooltip.
	static Button _TBButton(string icon, string tooltip, Action<Button> click) {
		var c = new Button { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip, Tag = tag };
		if (click != null) c.Click += (_, _) => click(c);
		return c;
	}
	
	//Creates a toolbar checkbox with icon and tooltip.
	static CheckBox _TBCheckbox(string icon, string tooltip, Action<CheckBox> click = null) {
		var c = new CheckBox { Content = ImageUtil.LoadWpfImageElement(icon), ToolTip = tooltip, Tag = tag };
		if (click != null) c.Click += (_, _) => click(c);
		return c;
	}
	
	// Add menus to the menubar.
	
	var menu = Panels.Menu;
	_RemoveOld(menu);
	_CreateMenu(menu);
	
	void _CreateMenu(Menu menu) {
		//Menu1
		var m1 = _TopItem("_Menu1");
		_Item(m1, "_Item1", o => { print.it(o.Header); });
		_Separator(m1);
		_Item(m1, "I_tem2", o => { print.it(o.Header); });
		//Menu2
		var m2 = _TopItem("_Menu2");
		_Item(m2, "_Item3", o => { print.it(o.Header); });
		var mSubmenu = _Item(m2, "_Submenu");
		_Item(mSubmenu, "_In submenu", o => { print.it(o.Header); });
		
		MenuItem _Item(ItemsControl parent, string name, Action<MenuItem> click = null) {
			var mi = new MenuItem { Header = name };
			if (parent is Menu) mi.Tag = tag;
			if (click != null) mi.Click += (sender, _) => click(sender as MenuItem);
			parent.Items.Add(mi);
			return mi;
		}
		
		MenuItem _TopItem(string name) => _Item(menu, name);
		
		void _Separator(ItemsControl parent) { parent.Items.Add(new Separator()); }
	}
	
	// Create new toolbar and add to a panel.
	// The panel root element is <b>DockPanel</b> or <b>Grid</b>, or <b>UserControl</b> containing one of these. This can change in the future.
	
	DockPanel dp = Panels.Output.P;
	dp.Children.Remove(dp.Children.OfType<ToolBarTray>().FirstOrDefault(o => o.Tag is string s1 && s1 == tag));
	
	var ptt = new ToolBarTray { Orientation = Orientation.Vertical, IsLocked = true, Tag = tag };
	DockPanel.SetDock(ptt, Dock.Left);
	dp.Children.Insert(0, ptt);
	
	var ptb = new ToolBar();
	ptt.ToolBars.Add(ptb);
	ptb.Items.Add(_TBButton("*MaterialDesign.History #0D69E1", "Button tooltip", _ => { print.it("click"); }));
	ptb.Items.Add(_TBCheckbox("*Modern.Stream #0D69E1", "Checkbox tooltip", c => { print.it("checked", c.IsChecked); }));
	
	// Add controls to a <b>Grid</b> panel.
	
	var b1 = new wpfBuilder().Tag(tag);
	b1.Add<Border>().Border(thickness2: new(0, 1, 0, 0)).Margin("0");
	b1.R.Add("Text", out TextBox _);
	b1.R.Add(out CheckBox _, "Check");
	b1.R.AddButton("Button", _ => { print.it("Button clicked"); });
	b1.End();
	
	Grid grid = Panels.Cookbook.P.Content as Grid;
	int row = grid.RowDefinitions.Count;
	if (grid.Children.OfType<Panel>().FirstOrDefault(o => o.Tag is string s1 && s1 == tag) is Panel oldPanel) { row = Grid.GetRow(oldPanel); grid.Children.Remove(oldPanel); } else grid.AddRows(0);
	grid.AddChild(b1.Panel, row, 0, columnSpan: 9);
	
	// Add panel.
	
	const string epName = "Example1";
	var ep = Panels.PanelManager[epName];
	if (ep == null) {
		ep = Panels.PanelManager.AddNewExtension(false, epName);
	}
	
	var b2 = new wpfBuilder();
	b2.R.Add("Text", out TextBox _);
	b2.R.Add(out CheckBox _, "Check");
	b2.R.AddButton("Button", _ => { print.it("Button clicked"); });
	b2.End();
	
	ep.Content = b2.Panel;
	
	// Add toolbar.
	
	const string etName = "Example2";
	var et = Panels.PanelManager[etName];
	if (et == null) {
		et = Panels.PanelManager.AddNewExtension(true, etName, where: Panels.PanelManager["Custom1"], after: false);
	}
	
	var ett = new ToolBarTray { IsLocked = true };
	var etb = new ToolBar { Name = "Example1" };
	ett.ToolBars.Add(etb);
	etb.Items.Add(_TBButton("*MaterialDesign.History #0D69E1", "Button tooltip", _ => { print.it("click"); }));
	etb.Items.Add(_TBCheckbox("*Modern.Stream #0D69E1", "Checkbox tooltip", c => { print.it("checked", c.IsChecked); }));
	et.Content = ett;
}
