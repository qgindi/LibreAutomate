using Au.Controls;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace LA;

static class Panels {
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
	public static PanelBookmarks Bookmarks;
	public static PanelBreakpoints Breakpoints;
	public static PanelDebug Debug;
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
				if (!s2.Contains("<panel name=\"Outline\"")) { //v0.4 added several new panels etc, and users would not know the best place for them, or even how to move
					filesystem.delete(customLayoutPath, FDFlags.RecycleBin);
					bool silent = s2.RxIsMatch(@"<document name=""documents"" ?/>\s*</tab>"); //very old and incompatible
					if (!silent) print.it("Info: The window layout has been reset, because several new panels have been added in this app version.\r\n\tIf you want to undo it: 1. Exit the program. 2. Restore file Layout.xml from the Recycle Bin (replace the existing file). 3. Run the program. 4. Move panels from the bottom of the window to a better place.");
					//rejected: show Yes/No dialog. Let users at first see the new default layout, then they can undo.
				}
			}
			catch (Exception e1) { Debug_.Print(e1); }
		}
		
		pm.BorderBrush = SystemColors.ActiveBorderBrush;
		//pm.Load(folders.ThisAppBS + @"Default\Layout.xml", null);
		pm.Load(folders.ThisAppBS + @"Default\Layout.xml", customLayoutPath);
		
		int saveCounter = 0;
		App.Timer1sWhenVisible += () => {
			if (++saveCounter >= 60) {
				saveCounter = 0;
				pm.Save();
			}
		};
		
		pm["Menu"].Content = Menu = new Menu();
		TFile = _CreateToolbar("File");
		TEdit = _CreateToolbar("Edit");
		TRun = _CreateToolbar("Run");
		TTools = _CreateToolbar("Tools");
		THelp = _CreateToolbar("Help", dp => {
			//dp.Children.Add(TBoxHelp = new TextBox { Height = 20, Margin = new Thickness(3, 1, 0, 2) }); //FUTURE
			return Dock.Right;
		});
		TCustom1 = _CreateToolbar("Custom1");
		TCustom2 = _CreateToolbar("Custom2");
	}
	
	static ToolBar _CreateToolbar(string name, Func<DockPanel, Dock> dockPanel = null) {
		var c = new ToolBar { Name = name/*, Background = SystemColors.ControlBrush*/ };
		c.UiaSetName(name);
		c.HideGripAndOverflow(false);
		var tt = new ToolBarTray { IsLocked = true/*, Background = SystemColors.ControlBrush*/ }; //because ToolBar looks bad if parent is not ToolBarTray
		tt.ToolBars.Add(c);
		FrameworkElement content = tt;
		if (dockPanel != null) {
			var p = new DockPanel { Background = tt.Background };
			p.Children.Add(tt);
			DockPanel.SetDock(tt, dockPanel(p));
			content = p;
		}
		PanelManager[name].Content = content;
		
		c.ContextMenuOpening += DCustomize.ToolbarContextMenuOpening;
		c.PreviewMouseRightButtonDown += (o, e) => { //prevent closing the overflow panel on right mouse button down
			if ((e.OriginalSource as UIElement).VisualAncestors(true).Any(o => o is System.Windows.Controls.Primitives.ToolBarOverflowPanel)) e.Handled = true;
		};
		
		return c;
	}
	
	public static void CreatePanels() {
		var pm = PanelManager;
		
		pm["documents"].Content = (Editor = new()).P;
		//DocPlaceholder_ = pm["documents"];
		
		pm["Files"].Content = (Files = new()).P;
		_AddDontFocus("Outline", (Outline = new()).P);
		pm["Cookbook"].Content = (Cookbook = new()).P;
		_AddDontFocus("Debug", (Debug = new()).P);
		_AddDontFocus("Open", (Open = new()).P);
		_AddDontFocus("Tasks", (Tasks = new()).P);
		pm["Find"].Content = (Find = new()).P;
		_AddDontFocus("Bookmarks", (Bookmarks = new()).P);
		_AddDontFocus("Breakpoints", (Breakpoints = new()).P);
		_AddDontFocus("Output", (Output = new()).P);
		_AddDontFocus("Mouse", (Mouse = new()).P);
		_AddDontFocus("Found", (Found = new()).P);
		_AddDontFocus("Recipe", (Recipe = new()).P);
		
		void _AddDontFocus(string panel, FrameworkElement content) {
			var p = pm[panel];
			p.Content = content;
			p.DontFocusTab = () => {
				var doc = Panels.Editor.ActiveDoc;
				if (doc != null) doc.Focus(); else Keyboard.ClearFocus();
			};
		}
	}
}
