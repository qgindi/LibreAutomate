using System.Windows;
using System.Windows.Controls;
using Au.Controls;

namespace Au.Tools;

class DCommandline : KDialogWindow {
	wpfBuilder _b;
	CheckBox _cEditorNoPath, _cWait;
	TextBox _tArgs;
	FileNode _f;
	
	public static void ShowForCurrentFile() {
		var f = App.Model.CurrentFile;
		if (f == null) return;
		if (!f.IsExecutableDirectly()) {
			dialog.showInfo(null, "This file isn't runnable as a script.", owner: App.Wmain);
			return;
		}
		f.SingleDialog(() => new DCommandline(f));
	}
	
	DCommandline(FileNode f) {
		_f = f;
		InitWinProp("Command line - " + _f.Name, App.Wmain);
		var b = _b = new wpfBuilder(this).WinSize(500);
		b.R.xAddInfoBlockT("This tool creates a command line string to run this script from other programs (cmd, PowerShell, shortcut, etc). More info in Cookbook folder \"Script\".");
		b.R.AddSeparator();
		b.R.Add(out _cEditorNoPath, "Editor program name without path");
		b.R.Add("Script arguments", out _tArgs);
		b.R.Add(out _cWait, "Can wait and capture script.writeResult text");
		b.R.AddSeparator();
		b.R.StartStack();
		b.AddButton("Copy to clipboard", _ => { clipboard.text = _FormatCL(1); });
		b.AddButton("Create shortcut...", _ => _Shortcut());
		b.AddOkCancel(null, "Close");
		b.End();
		b.End();
	}
	
	//action: 1 clipboard, 2 shortcut
	string _FormatCL(int action) {
		var sb = new StringBuilder();
		bool wait = false;
		if (action == 1) {
			_AppendQ(_cEditorNoPath.IsChecked == true ? process.thisExeName : process.thisExePath);
			sb.Append(' ');
			wait = _cWait.IsChecked == true;
		}
		_AppendQ(_f.ItemPathOrName(), wait);
		if (_tArgs.Text.NullIfEmpty_() is { } sa) sb.Append(' ').Append(sa);
		
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
}
