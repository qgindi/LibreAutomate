using System.Windows;
using System.Windows.Controls;
using Au.Controls;

namespace Au.Tools;

class DCommandline : KDialogWindow {
	wpfBuilder _b;
	CheckBox _cEditorNoPath, _cScriptNoPath, _cWait;
	TextBox _tArgs;
	
	DCommandline() {
		InitWinProp("Script command line triggers", App.Wmain);
		var b = _b = new wpfBuilder(this).WinSize(440);
		b.R.xAddInfoBlockT("This tool creates a command line string to run current script from other programs and scripts (cmd, PowerShell, Task Scheduler, shortcut, etc).\nMore info in Cookbook folder \"Script\".");
		b.R.AddSeparator();
		b.R.Add(out _cEditorNoPath, "Editor program name without path");
		b.R.Add(out _cScriptNoPath, "Script name without path");
		b.R.Add("Script arguments", out _tArgs);
		b.R.Add(out _cWait, "Can wait and capture script.writeResult text");
		b.R.AddSeparator();
		b.R.StartStack();
		b.AddButton("Copy to clipboard", _ => { clipboard.text = _FormatCL(1); });
		b.AddButton("Create shortcut...", _ => _Shortcut());
		b.AddButton(_IsScheduled() ? "Edit scheduled task" : "Create scheduled task", _ => _Schedule());
		b.End();
		b.End();
	}
	
	public static void ShowSingle() {
		ShowSingle(() => new DCommandline());
	}
	
	//action: 1 clipboard, 2 shortcut, 3 schedule
	string _FormatCL(int action) {
		var f = App.Model.CurrentFile; if (f == null) return null;
		var sb = new StringBuilder();
		bool wait;
		if (action == 1) {
			_AppendQ(_cEditorNoPath.IsChecked == true ? process.thisExeName : process.thisExePath);
			sb.Append(' ');
			wait = _cWait.IsChecked == true;
		} else {
			wait = action == 3;
		}
		_AppendQ(_cScriptNoPath.IsChecked == true ? f.Name : f.ItemPath, wait);
		var args = _tArgs.Text; if (args.Length > 0) sb.Append(' ').Append(args);
		
		return sb.ToString();
		
		void _AppendQ(string s, bool wait = false) {
			bool q = s.Contains(' ');
			if (q) sb.Append('"');
			if (wait) sb.Append('*');
			sb.Append(s);
			if (q) sb.Append('"');
		}
	}
	
	void _Shortcut() {
		if (App.IsPortable && 1 != dialog.showWarning("Portable mode warning", "This will create a shortcut file. Portable apps should not create files on host computer.\r\n\r\nDo you want to continue?", "1 Yes|2 No", owner: this)) return;
		var s = _FormatCL(2); if (s == null) return;
		var d = new FileOpenSaveDialog("38939d61-1971-45f5-8ce5-0c405aab792c") {
			FileNameText = App.Model.CurrentFile.DisplayName + ".lnk",
			FileTypes = "Shortcut|*.lnk",
			InitFolderFirstTime = folders.Desktop,
			Title = "Create shortcut"
		};
		if (!d.ShowSave(out var lnk, this)) return;
		try {
			using var sh = shortcutFile.create(lnk);
			sh.TargetPath = process.thisExePath;
			sh.Arguments = s;
			sh.Save();
		}
		catch (Exception e1) { dialog.showError("Failed", e1.ToStringWithoutStack(), owner: this); }
	}
	
	void _Schedule() {
		var s = _FormatCL(3); if (s == null) return;
		var user = Environment.UserName;
		string folder = @"Au\" + user /*App.Model.WorkspaceName*/, name = App.Model.CurrentFile.DisplayName;
		if (!WinTaskScheduler.TaskExists(folder, name)) {
			if (App.IsPortable && 1 != dialog.showWarning("Portable mode warning", "Scheduled task will be created on this computer. Portable apps should not do it.\r\n\r\nDo you want to continue?", "1 Yes|2 No", owner: this)) return;
			bool admin = uacInfo.isAdmin;
			bool ok = WinTaskScheduler.CreateTaskWithoutTriggers(folder, name,
				admin ? UacIL.High : UacIL.Medium,
				process.thisExePath, s, author: user);
			if (!ok) {
				dialog.showError("Failed", admin ? null : "Restart this program as administrator.", owner: this);
				return;
			}
		}
		WinTaskScheduler.EditTask(folder, name);
		
		//never mind: non-admin process can't create folders and tasks.
		//	But somehow can do it in the QM2 tasks folder.
		//	Now I don't know how to set folder security permissions.
	}
	
	static bool _IsScheduled() {
		return WinTaskScheduler.TaskExists(@"Au\" + Environment.UserName, App.Model.CurrentFile.DisplayName);
	}
}
