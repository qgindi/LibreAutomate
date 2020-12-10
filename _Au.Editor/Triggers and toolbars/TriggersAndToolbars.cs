using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;

using Au;
using Au.Types;
using Au.Triggers;

static class TriggersAndToolbars
{
	public static void Edit(string file)
	{
		var f = _GetFile(file, create: true);
		if(f == null) return;
		Program.Model.OpenAndGoTo(f);
	}

	static FileNode _GetProject(bool create)
	{
		var fProject = Program.Model.Find(@"\@Triggers and toolbars", folder: true);
		if(create) {
			if(fProject == null) {
				fProject = Program.Model.NewItemLL(s_templPath);
				AOutput.Write("Info: project \"@Triggers and toolbars\" has been created.");
			} else { //create missing files. Note: don't cache, because files can be deleted at any time. Fast enough.
				var xTempl = FileNode.Templates.LoadXml(s_templPath).x; //fast, does not load the xml file each time
				_Folder(xTempl, fProject);
				void _Folder(XElement xParent, FileNode fParent)
				{
					foreach(var x in xParent.Elements()) {
						bool isFolder = x.Name.LocalName == "d";
						string name = x.Attr("n");
						if(isFolder && (name == "Scripts" || name == "Functions")) continue;
						var ff = fParent.Children().FirstOrDefault(o => o.Name.Eqi(name));
						if(ff == null) {
							ff = Program.Model.NewItemLLX(x, (fParent, FNPosition.Inside));
						} else if(isFolder) {
							_Folder(x, ff);
						}
					}
				}
			}

			//set run at startup
			const string script = @"\@Triggers and toolbars\Triggers and toolbars.cs";
			bool startupFound = false;
			var ss = Program.Model.StartupScriptsCsv;
			if(ss == null) {
				ss = script;
			} else {
				try {
					var x = ACsv.Parse(ss);
					var rx = @"(?i)^(?://)?(?:\\@Triggers and toolbars\\)?Triggers and toolbars(?:\.cs)?$"; //path or name; with or without .cs; can be //disabled
					startupFound = x.Data.Exists(a => a[0].RegexIsMatch(rx));
					if(!startupFound) {
						x.AddRow(script);
						ss = x.ToString();
					}
				}
				catch(FormatException) { }
			}
			if(!startupFound) {
				Program.Model.StartupScriptsCsv = ss;
				AOutput.Write("Info: script \"Triggers and toolbars\" will run at program startup. If you want to disable it, add prefix // in Options -> Run scripts...");
			}
		}
		return fProject;
	}
	const string s_templPath = @"Examples\@Triggers and toolbars";

	static FileNode _GetFile(string file, bool create)
	{
		var f = _GetProject(create: create);
		return f?.FindRelative(file, folder: false);
	}

	public static void Restart()
	{
		var f = _GetFile(@"Triggers and toolbars.cs", create: false);
		if(f != null) CompileRun.CompileAndRun(true, f, runFromEditor: true);
	}

	/// <summary>
	/// Disables, enables or toggles triggers in all processes. See <see cref="ActionTriggers.DisabledEverywhere"/>.
	/// Also updates UI: changes tray icon and checks/unchecks the menu item.
	/// </summary>
	/// <param name="disable">If null, toggles.</param>
	public static void DisableTriggers(bool? disable)
	{
		bool dis = disable switch { true => true, false => false, _ => !ActionTriggers.DisabledEverywhere };
		if(dis == ActionTriggers.DisabledEverywhere) return;
		ActionTriggers.DisabledEverywhere = dis; //notifies us to update tray icon etc
	}

	//from ActionTriggers.DisabledEverywhere through our message-only window
	internal static void OnDisableTriggers()
	{
		bool dis = ActionTriggers.DisabledEverywhere;
		EdTrayIcon.Disabled = dis;
		Strips.CheckCmd(nameof(CmdHandlers.TT_DisableTriggers), dis);
	}

	public static void ShowActiveTriggers()
	{
		for(AWnd w = default; ; ) {
			w = AWnd.FindFast(null, "Au.Triggers.Hooks", messageOnly: true, w);
			if(w.Is0) break;
			w.Post(Api.WM_USER + 30);
		}
	}

	public static void AddTrigger()
	{

	}

	public static void AddToolbar()
	{

	}
}