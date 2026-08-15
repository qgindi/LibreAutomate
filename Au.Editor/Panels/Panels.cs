using Au.Controls;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Windows.Media;

namespace LA;

static class Panels {
	public static KPanels PanelManager;
	//panels
	public static PanelEdit Editor;
	public static PanelFiles Files;
	public static PanelOutline Outline;
	public static PanelHelp Help;
	public static PanelOpen Open;
	public static PanelTasks Tasks;
	public static PanelOutput Output;
	public static PanelFind Find;
	public static PanelFound Found;
	public static PanelMouse Mouse;
	public static PanelRead Read;
	public static PanelBookmarks Bookmarks;
	public static PanelBreakpoints Breakpoints;
	public static PanelDebug Debug;
	//menu and toolbars
	public static Menu Menu;
	public static ToolBar TFile, TEdit, TRun, TTools, TCustom1, TCustom2;
	
	public static void LoadAndCreateToolbars() {
		var pm = PanelManager = new KPanels();
		
		var customLayoutPath = AppSettings.DirBS + "Layout.xml";
		if (filesystem.exists(customLayoutPath).File) {
			try {
				var x = XmlUtil.LoadElem(customLayoutPath);
				if (x.XPathSelectElement("//panel[@name='Outline']") == null) { //v0.4 added several new panels etc, and users would not know the best place for them, or even how to move
					filesystem.delete(customLayoutPath, FDFlags.RecycleBin);
					print.it("Info: The window layout has been reset, because several new panels have been added in this app version.\r\n\tIf you want to undo it: 1. Exit the program. 2. Restore file Layout.xml from the Recycle Bin (replace the existing file). 3. Run the program. 4. Move panels from the bottom of the window to a better place.");
				} else if (x.XPathSelectElement("//panel[@name='Read']") == null) { //in v1.15 renamed some panels and removed the Help toolbar
					x.XPathSelectElement("//*[@name='Help']")?.Remove();
					x.XPathSelectElement("//panel[@name='Cookbook']")?.SetAttributeValue("name", "Help");
					x.XPathSelectElement("//panel[@name='Recipe']")?.SetAttributeValue("name", "Read");
					
					//also renamed some attributes
					foreach (var v in x.Descendants()) {
						if (v.Attribute("captionAt") is { } k) { v.SetAttributeValue("headerAt", k.Value); k.Remove(); }
					}
					
					x.SaveElem(customLayoutPath);
					
					//also remove the Help toolbar from toolbar customizations
					var customCommandsPath = AppSettings.DirBS + "Commands.xml";
					if (filesystem.exists(customCommandsPath).File) {
						var xc = XmlUtil.LoadElem(customCommandsPath);
						if (xc.XPathSelectElement("//Help") is { } x1) {
							x1.Remove();
							xc.Save(customCommandsPath);
						}
					}
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
		
		KPanels.ILeaf _AddDontFocus(string panel, FrameworkElement content) {
			var p = pm[panel];
			p.Content = content;
			p.DontFocusTab = () => {
				var doc = Panels.Editor.ActiveDoc;
				if (doc != null) doc.Focus(); else Keyboard.ClearFocus();
			};
			return p;
		}
		
		pm["documents"].Content = (Editor = new()).P;
		
		pm["Files"].Content = (Files = new()).P;
		_AddDontFocus("Outline", (Outline = new()).P);
		pm["Help"].Content = (Help = new()).P;
		_AddDontFocus("Debug", (Debug = new()).P);
		_AddDontFocus("Open", (Open = new()).P);
		_AddDontFocus("Tasks", (Tasks = new()).P);
		pm["Find"].Content = (Find = new()).P;
		_AddDontFocus("Bookmarks", (Bookmarks = new()).P);
		_AddDontFocus("Breakpoints", (Breakpoints = new()).P);
		_AddDontFocus("Output", (Output = new()).P);
		_AddDontFocus("Mouse", (Mouse = new()).P);
		_AddDontFocus("Found", (Found = new()).P);
		var pRead = _AddDontFocus("Read", (Read = new()).P);
		pRead.Visible = false;
	}
}
