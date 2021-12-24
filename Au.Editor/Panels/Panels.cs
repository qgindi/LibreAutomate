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
	public static PanelOpen Open;
	public static PanelTasks Tasks;
	public static PanelOutput Output;
	public static PanelFind Find;
	public static PanelFound Found;
	public static PanelMouse Mouse;
	//menu and toolbars
	public static Menu Menu;
	//public static ToolBar[] Toolbars;
	public static ToolBar TFile, TEdit, TRun, TTools, THelp, TCustom1, TCustom2;

	public static void LoadAndCreateToolbars() {
		var pm = PanelManager = new KPanels();

		//FUTURE: later remove this code. Now need to delete old custom Layout.xml. It uses wrong document etc.
		var s1 = AppSettings.DirBS + "Layout.xml";
		if (filesystem.exists(s1).isFile) {
			var s2 = filesystem.loadText(s1);
			//print.it(s2);
			if (s2.RxIsMatch(@"<document name=""documents"" ?/>\s*</tab>")) filesystem.delete(s1);
		}

		pm.BorderBrush = SystemColors.ActiveBorderBrush;
		//pm.Load(folders.ThisAppBS + @"Default\Layout.xml", null);
		pm.Load(folders.ThisAppBS + @"Default\Layout.xml", AppSettings.DirBS + "Layout.xml");

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

		pm["Files"].Content = Files = new PanelFiles();
		pm["Open"].Content = Open = new PanelOpen();
		pm["Tasks"].Content = Tasks = new PanelTasks();
		pm["Find"].Content = Find = new PanelFind();
		pm["Output"].Content = Output = new PanelOutput();
		pm["Mouse"].Content = Mouse = new PanelMouse();
		pm["Found"].Content = Found = new PanelFound();

		pm["documents"].Content = Editor = new PanelEdit();
		//DocPlaceholder_ = pm["documents"];
	}
}
