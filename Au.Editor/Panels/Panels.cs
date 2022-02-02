using Au.Controls;
using System.Windows.Controls;
using System.Windows;

static class Panels
{
	public static KPanels PanelManager;
	//internal static KPanels.ILeaf DocPlaceholder_;
	//panels
	public static PanelEdit Editor;
	public static PanelFiles Files;
	public static PanelOutline Outline;
	public static PanelCookbook Cookbook;
	public static PanelOpen Open;
	public static PanelTasks Tasks;
	public static PanelOutput Output;
	public static PanelFind Find;
	public static PanelFound Found;
	public static PanelMouse Mouse;
	public static PanelRecipe Recipe;
	//menu and toolbars
	public static Menu Menu;
	//public static ToolBar[] Toolbars;
	public static ToolBar TFile, TEdit, TRun, TTools, THelp, TCustom1, TCustom2;

	public static void LoadAndCreateToolbars() {
		var pm = PanelManager = new KPanels();

		//FUTURE: later remove this code. Now may need to delete old custom Layout.xml.
		var customLayoutPath = AppSettings.DirBS + "Layout.xml";
		if (filesystem.exists(customLayoutPath).File) {
			try {
				var s2 = filesystem.loadText(customLayoutPath);
				//print.it(s2);
				if (!s2.Contains("<panel name=\"Outline\"")) { //one app version added several new panels etc, and users would not know the best place for them, or even how to move
					filesystem.delete(customLayoutPath, recycleBin: true);
					bool silent = s2.RxIsMatch(@"<document name=""documents"" ?/>\s*</tab>"); //very old and incompatible
					if (!silent) print.it("Info: The window layout has been reset, because several new panels have been added in this app version.\r\n\tIf you want to undo it: 1. Exit the program. 2. Restore file Layout.xml from the Recycle Bin (replace the existing file). 3. Run the program. 4. Move panels from the bottom of the window to a better place.");
					//rejected: show Yes/No dialog. Let users at first see the new default layout, then they can undo.
				}
			}
			catch (Exception e1) { Debug_.Print(e1.ToStringWithoutStack()); }
		}

		pm.BorderBrush = SystemColors.ActiveBorderBrush;
		//pm.Load(folders.ThisAppBS + @"Default\Layout.xml", null);
		pm.Load(folders.ThisAppBS + @"Default\Layout.xml", customLayoutPath);

		pm["Menu"].Content = Menu = new Menu();
		TFile = _TB("File");
		TEdit = _TB("Edit");
		TRun = _TB("Run");
		TTools = _TB("Tools");
		THelp = _TB("Help", true);
		TCustom1 = _TB("Custom1");
		TCustom2 = _TB("Custom2");

		ToolBar _TB(string name, bool isHelp = false) {
			var c = new ToolBar { Name = name };
			var tt = new ToolBarTray { IsLocked = true }; //because ToolBar looks bad if parent is not ToolBarTray
			c.UiaSetName(name);
			tt.ToolBars.Add(c);
#if true
			if (isHelp) {
				var p = new DockPanel { Background = tt.Background };
				DockPanel.SetDock(tt, Dock.Right);
				p.Children.Add(tt);
				//FUTURE
				//var box = new TextBox { Height = 20, Margin = new Thickness(3, 1, 3, 2), Padding = new Thickness(1, 1, 1, 0) };
				//p.Children.Add(box);
				pm[name].Content = p;
			} else {
				pm[name].Content = tt;
			}
#else
			if (name == "Help") c.Items.Add(new TextBox { Width = 150, Padding = new Thickness(1, 0, 1, 0) });
			pm[name].Content = tt;
#endif
			return c;
		}
	}

	public static void CreatePanels() {
		var pm = PanelManager;

		pm["Files"].Content = Files = new();
		pm["Outline"].Content = Outline = new();
		pm["Cookbook"].Content = Cookbook = new();
		pm["Open"].Content = Open = new();
		pm["Tasks"].Content = Tasks = new();
		pm["Find"].Content = Find = new();
		pm["Output"].Content = Output = new();
		pm["Mouse"].Content = Mouse = new();
		pm["Found"].Content = Found = new();
		pm["Recipe"].Content = Recipe = new();

		pm["documents"].Content = Editor = new();
		//DocPlaceholder_ = pm["documents"];

		var ti1 = Panels.PanelManager[Panels.Files].Parent.TabItem; if (ti1 != null) ti1.MinWidth = 56; //make Files tabitem wider
	}
}
