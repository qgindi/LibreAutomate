using System.Xml.Linq;
using Au.Triggers;

partial class TriggersAndToolbars {
	public static bool Edit(string file) {
		var f = _GetFile(file, create: true);
		if (f == null) return false;
		return App.Model.OpenAndGoTo(f);
	}

	public static void GoToToolbars() {
		var folder = GetProject(create: true);
		folder = folder.Children().First(f => f.IsFolder && f.Name.Eqi("Toolbars"));

		folder.SelectSingle();
		var tv = Panels.Files.TreeControl;
		if (!folder.IsExpanded) tv.Expand(folder, true);
		tv.EnsureVisible(folder, scrollTop: true);

		//var m = new popupMenu();
		//foreach (var f in folder.Children()) {
		//	if (!f.IsClass) continue;
		//	m[f.DisplayName] = _ => App.Model.OpenAndGoTo(f);
		//}
		//m.Show();
	}

	/// <summary>
	/// Finds or creates project @"\@Triggers and toolbars".
	/// </summary>
	/// <param name="create">Create if does not exist.</param>
	/// <returns>The project folder. Returns null if does not exist and <i>create</i> false.</returns>
	public static FileNode GetProject(bool create) {
		var fProject = App.Model.Find(@"\@Triggers and toolbars", FNFind.Folder);
		if (create) {
			if (fProject == null) {
				fProject = App.Model.NewItemL(s_templPath);
				print.it("Info: folder \"@Triggers and toolbars\" has been created.");
			} else { //create missing files. Note: don't cache, because files can be deleted at any time. Fast enough.
				var xTempl = FileNode.Templates.LoadXml(s_templPath); //fast, does not load the xml file each time
				_Folder(xTempl, fProject);
				void _Folder(XElement xParent, FileNode fParent) {
					foreach (var x in xParent.Elements()) {
						bool isFolder = x.Name.LocalName == "d";
						string name = x.Attr("n");
						if (isFolder && (name == "Scripts" || name == "Functions")) continue;
						var ff = fParent.Children().FirstOrDefault(o => o.Name.Eqi(name));
						if (ff == null) {
							ff = App.Model.NewItemLX(x, new(fParent, FNInsert.Last));
						} else if (isFolder) {
							_Folder(x, ff);
						}
					}
				}
			}

			//set run at startup
			const string c_script = @"\@Triggers and toolbars\Triggers and toolbars.cs";
			bool startupFound = false;
			var ss = App.Model.UserSettings.startupScripts;
			if (ss.NE()) {
				ss = c_script;
			} else {
				try {
					var x = csvTable.parse(ss);
					var rx = @"(?i)^(?://)?(?:\\@Triggers and toolbars\\)?Triggers and toolbars(?:\.cs)?$"; //path or name; with or without .cs; can be //disabled
					startupFound = x.Rows.Exists(a => a[0].RxIsMatch(rx));
					if (!startupFound) {
						x.AddRow(c_script);
						ss = x.ToString();
					}
				}
				catch (FormatException) { }
			}
			if (!startupFound) {
				App.Model.UserSettings.startupScripts = ss;
				print.it("Info: script \"Triggers and toolbars\" will run at program startup. If you want to disable it, add prefix // in Options > Workspace > Run scripts...");
			}
		}
		return fProject;
	}
	const string s_templPath = @"Default\@Triggers and toolbars";

	static FileNode _GetFile(string file, bool create, FNFind kind = FNFind.File) {
		var f = GetProject(create: create);
		return f?.FindRelative(file, kind);
	}

	public static void Restart() {
		var f = _GetFile(@"Triggers and toolbars.cs", create: false);
		if (f != null) CompileRun.CompileAndRun(true, f, runFromEditor: true);
	}

	/// <summary>
	/// Disables, enables or toggles triggers in all processes. See <see cref="ActionTriggers.DisabledEverywhere"/>.
	/// Also updates UI: changes tray icon and checks/unchecks the menu item.
	/// </summary>
	/// <param name="disable">If null, toggles.</param>
	public static void DisableTriggers(bool? disable) {
		bool dis = disable switch { true => true, false => false, _ => !ActionTriggers.DisabledEverywhere };
		if (dis == ActionTriggers.DisabledEverywhere) return;
		ActionTriggers.DisabledEverywhere = dis; //notifies us to update tray icon etc
	}

	//from ActionTriggers.DisabledEverywhere through our message-only window
	internal static void OnDisableTriggers() {
		bool dis = ActionTriggers.DisabledEverywhere;
		App.TrayIcon.Disabled = dis;
		if (App.Commands is { } ac) ac[nameof(Menus.TT.Disable_triggers)].Checked = dis;
	}

	public static void ShowActiveTriggers() {
		for (wnd w = default; ;) {
			w = wnd.findFast(null, "Au.Triggers.Hooks", messageOnly: true, w);
			if (w.Is0) break;
			Api.AllowSetForegroundWindow(w.ProcessId);
			w.Post(Api.WM_USER + 30);
		}
	}

	//public static void AddTrigger()
	//{

	//}

	public static void NewToolbar() {
		var tt = new TriggersAndToolbars();
		tt._NewToolbar();
	}

	public static void SetToolbarTrigger() {
		var tt = new TriggersAndToolbars();
		tt._SetToolbarTrigger();
	}
}
