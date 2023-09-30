/* role editorExtension; define SCRIPT; testInternal Au,Au.Editor,Au.Controls; r Au.Editor.dll; r Au.Controls.dll; /*/

using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Au.Controls;

#if SCRIPT
DPortable.aaShow();
file
#endif

class DPortable : KDialogWindow {
	public static void ShowSingle() {
		if (App.IsPortable) {
			dialog.showError("Portable mode", "Please restart this program in non-portable mode.", owner: App.Wmain);
			return;
		}
		ShowSingle(() => new DPortable());
	}

	string _dirPortableApp = App.Settings.portable_dir;
	string _dirThisApp = folders.ThisApp;
	string _dirNet = folders.NetRuntime;
	string _dirNetDesktop = folders.NetRuntimeDesktop;
	string _dirScriptDataLocal = folders.ThisAppDataLocal + "_script";
	string _dirScriptDataRoaming = folders.ThisAppDataRoaming + "_script";
	string _dirThisAppDocuments = folders.ThisAppDocuments;
	string _dirWorkspace = App.Model.WorkspaceDirectory;
	string _dirData;
	bool _exists, _replacePF, _replaceData;

	DPortable() {
		//test with smaller folders
		//_dirThisApp = folders.ProgramFiles + "LibreAutomate";
		//_dirWorkspace = folders.ThisAppDocuments + "Main";

		if (filesystem.more.comparePaths(_dirThisApp, _dirNet) is CPResult.Same or CPResult.AContainsB) _dirNet = _dirNetDesktop = null;
		if (!filesystem.exists(_dirScriptDataLocal).Directory) _dirScriptDataLocal = null;
		if (!filesystem.exists(_dirScriptDataRoaming).Directory) _dirScriptDataRoaming = null;

		InitWinProp("Portable LibreAutomate setup", App.Wmain);
		var b = new wpfBuilder(this).WinSize(450);

		if (_dirPortableApp.NE() && folders.RemovableDrive0.Path is string drive) App.Settings.portable_dir = _dirPortableApp = drive + @"PortableApps\LibreAutomate";
		b.R.Add("Install in folder", out TextBox tDir).Focus().Validation(_ => _ValidateFolder());

		b.R.StartStack(out KGroupBox gExists, "The folder already exists", vertical: true);
		b.Add(out KCheckBox cReplacePF, "Replace program files (LA, .NET)").Tooltip("Replace everything in the portable program folder except the data folder");
		b.Add(out KCheckBox cReplaceData, "Replace data (scripts, settings, etc)").Tooltip("Replace the data folder")
			.Validation(_ => _exists && !cReplacePF.IsChecked && !cReplaceData.IsChecked ? "Please check one or both 'Replace'" : null);
		b.End();

		b.R.Add("Data folder", out ComboBox cbData).Items(@"data|..\..\Documents\LibreAutomate").Editable()
			.Tooltip("Folder for scripts, settings and other data. Relative to the program folder.\nShould be either \"data\" or outside of the program folder.");

		b.R.AddSeparator();
		b.R.StartOkCancel();
		b.AddButton("Print details", _ => _PrintDetails()).Width(100);
		b.AddButton("Install", null, WBBFlags.OK);
		b.AddButton("Cancel", null, WBBFlags.Cancel);
		b.AddButton("Help", _ => HelpUtil.AuHelp("editor/Portable app")).Width(70);
		b.End();

		b.End();

		tDir.TextChanged += (_, _) => {
			App.Settings.portable_dir = _dirPortableApp = tDir.Text.Trim();
			_exists = filesystem.exists(_dirPortableApp).Directory;
			gExists.Visibility = _exists ? Visibility.Visible : Visibility.Collapsed;
			bool ntfs = true; try { ntfs = new DriveInfo(_dirPortableApp).DriveFormat == "NTFS"; } catch {  }
			cbData.IsEnabled = ntfs;
			if (!ntfs) {
				cbData.Text = "data";
			} else if (_exists) {
				var data = _dirPortableApp + @"\data";
				if (filesystem.more.getFinalPath(_dirPortableApp, out var s1, format: FPFormat.PrefixAlways) && filesystem.more.getFinalPath(data, out var s2, format: FPFormat.PrefixAlways)) {
					cbData.Text = Path.GetRelativePath(s1, s2);
				}
			}
		};
		tDir.Text = _dirPortableApp;

		b.OkApply += e => {
			_replacePF = cReplacePF.IsChecked;
			_replaceData = cReplaceData.IsChecked;
			_dirData = cbData.Text; if (_dirData.NE()) _dirData = "data";

			Task.Run(() => {
				try { _InstallThread(); }
				catch (Exception e1) { dialog.showError(@"Failed", e1.ToString()); }
			});
		};
	}

	void _InstallThread() {
		if (!_exists || (_replacePF && _replaceData)) {
			print.it("<><lc YellowGreen>Installing portable LibreAutomate<>");
			if (_exists) _DeleteOldData();
			_CopyPF();
			_CopyData();
		} else {
			var dirData = _dirPortableApp + @"\data";
			if (_replacePF) {
				print.it("<><lc YellowGreen>Replacing portable LibreAutomate program files<>");
				var temp = _dirPortableApp + "-data";
				print.it($"Moving old data folder to {temp}");
				filesystem.move(dirData, temp);
				try {
					_CopyPF();
				}
				finally {
					print.it($"Moving old data folder to {dirData}");
					filesystem.move(temp, dirData);
				}
			} else if (_replaceData) {
				print.it("<><lc YellowGreen>Replacing portable LibreAutomate data<>");
				_DeleteOldData();
				_CopyData();
			}
		}

		print.it($"<>DONE. Installed in <link>{_dirPortableApp}<>.");

		void _CopyPF() {
			if (_exists) {
				print.it("Deleting old folder");
				filesystem.delete(_dirPortableApp);
			}

			_Copy(_dirThisApp, _dirPortableApp);
			filesystem.delete(Directory.GetFiles(_dirPortableApp, "unins*"), FDFlags.CanFail); //4 MB

			if (_dirNet != null) {
				var dotnet = _dirPortableApp + @"\dotnet";
				_Copy(_dirNet, dotnet, FIfExists.Delete);
				_Copy(_dirNetDesktop, dotnet, FIfExists.MergeDirectory);
			}
		}

		void _CopyData() {
			var data = _dirPortableApp + @"\data";
			if (_dirData.Eqi("data")) {
				_dirData = data;
			} else {
				filesystem.more.createSymbolicLink(data, _dirData, CSLink.Directory, elevate: true); //probably relative link
				_dirData = pathname.normalize(_dirData, _dirPortableApp);
			}

			if (_dirScriptDataLocal != null) _Copy(_dirScriptDataLocal, _dirData + @"\appLocal\_script");
			if (_dirScriptDataRoaming != null) _Copy(_dirScriptDataRoaming, _dirData + @"\appRoaming\_script");

			_Copy(_dirThisAppDocuments, _dirData + @"\doc");

			_WorkspaceAndSettings();
		}

		static void _Copy(string dir, string dirTo, FIfExists _ifExists = FIfExists.Fail) {
			print.it($"Copying {pathname.unprefixLongPath(dir)}");
			filesystem.copy(dir, dirTo, _ifExists);
		}

		void _WorkspaceAndSettings() {
			string ws1 = _dirWorkspace, ws2, doc1 = _dirThisAppDocuments;
			switch (filesystem.more.comparePaths(ref doc1, ref ws1)) {
			case CPResult.AContainsB:
				ws2 = ws1[doc1.Length..];
				break;
			case CPResult.None:
				var copyTo = pathname.makeUnique(_dirData + @"\doc\" + pathname.getName(ws1), isDirectory: true);
				_Copy(ws1, copyTo, FIfExists.Fail);
				ws2 = "\\" + pathname.getName(copyTo);
				break;
			default: throw new AuException();
			}
			ws2 = "%folders.ThisAppDocuments%" + ws2;

			print.it("Changing settings");
			var settFile = _dirData + @"\doc\.settings\Settings.json";
			var j = JsonNode.Parse(filesystem.loadBytes(settFile));
			j["workspace"] = ws2;
			//print.it(j);
			//print.it(ws1, ws2);
			filesystem.saveText(settFile, j.ToJsonString());
		}

		void _DeleteOldData() {
			var data = _dirPortableApp + @"\data";
			if (filesystem.exists(data)) {
				print.it("Deleting old data");
				if (filesystem.more.getFinalPath(data, out var s)) filesystem.delete(s); //if symlink, delete target
				filesystem.delete(data);
			}
		}
	}

	void _PrintDetails() {
		long sizeProg = filesystem.more.calculateDirectorySize(_dirThisApp);
		long sizeNet = _dirNet == null ? 0 : filesystem.more.calculateDirectorySize(_dirNet) + filesystem.more.calculateDirectorySize(_dirNetDesktop);
		long sizeScriptData = 0;
		if (_dirScriptDataLocal != null) sizeScriptData += filesystem.more.calculateDirectorySize(_dirScriptDataLocal);
		if (_dirScriptDataRoaming != null) sizeScriptData += filesystem.more.calculateDirectorySize(_dirScriptDataRoaming);
		long sizeDoc = filesystem.more.calculateDirectorySize(_dirThisAppDocuments);
		bool wsInDoc = filesystem.more.comparePaths(_dirThisAppDocuments, _dirWorkspace) == CPResult.AContainsB;
		long sizeWs = filesystem.more.calculateDirectorySize(_dirWorkspace); if (wsInDoc) sizeDoc -= sizeWs;

		var b = new StringBuilder();
		b.Append($"""
<><lc YellowGreen>Portable LibreAutomate setup details<>
Total size: {_MB(sizeProg + sizeNet + sizeScriptData + sizeDoc + sizeWs)} MB.
Folder sizes (MB):
	{"Program",-15} {_MB(sizeProg)}
	{".NET Runtime",-15} {_MB(sizeNet)}
	{"AppData",-15} {_MB(sizeScriptData)}
	{"Documents",-15} {_MB(sizeDoc)}{(wsInDoc ? "  (except workspace)" : null)}
	{"Workspace",-15} {_MB(sizeWs)}

""");
		_BigFiles(_dirWorkspace, "workspace");
		void _BigFiles(string dir, string name) {
			var a = filesystem.enumDirectories(dir)
				.Select(o => (o.Name, size: _MB(filesystem.more.calculateDirectorySize(o.FullPath))))
				.Where(o => o.size > 0)
				.Select(o => $"\t{o.Name,-15} {o.size}").ToArray();
			if (a.Length > 0) b.AppendLine($"Big folders in <link {dir}>{name}<> (MB):").AppendJoin("\r\n", a).AppendLine();
		}
		var dirLib = _dirThisApp + @"\Libraries";
		if (filesystem.exists(dirLib).Directory && _MB(filesystem.more.calculateDirectorySize(dirLib)) is int n1 && n1 > 0) {
			b.AppendLine($"Some big folders in <link {_dirThisApp}>program<> folder (MB):\r\n\t{"Libraries",-15} {n1}");
		}
		_Links(b);

		print.clear();
		print.it(b.ToString());
		print.scrollToTop();

		static int _MB(long size) {
			const long MB = 1024 * 1024;
			//return (int)((size + MB / 2) / MB); //round
			return (int)(size / MB);
		}

		void _Links(StringBuilder b) {
			int n = 0;
			foreach (var f in App.Model.Root.Descendants()) {
				if (f.IsLink) {
					if (n++ == 0) b.AppendLine("Links to external files and folders will be invalid on other computers:");
					b.Append($"\t{(f.IsFolder ? "Folder " : null)}{f.SciLink(path: true)}  ->  <explore>{f.FilePath}<>\r\n");
				}
			}
		}
	}

	string _ValidateFolder() {
		if (_dirPortableApp.NE()) return "Where to install?";

		//error if workspace is in portable folder (shared). Or part of it (link target).
		if (filesystem.exists(_dirPortableApp)) {
			bool bad = filesystem.more.comparePaths(_dirPortableApp, _dirWorkspace) is CPResult.AContainsB or CPResult.BContainsA or CPResult.Same;
			if (!bad) {
				foreach (var f in App.Model.Root.Descendants()) {
					if (f.IsLink) {
						var s = f.FilePath;
						if (filesystem.more.comparePaths(_dirPortableApp, s) is CPResult.AContainsB or CPResult.BContainsA or CPResult.Same) {
							if (!bad) print.it("Links to portable:");
							bad = true;
							print.it($"<>{f.SciLink(path: true)}  ->  <explore>{s}<>");
						}
					}
				}
			}
			if (bad) return "Current workspace or part of it is in the portable folder, or vice versa.";
		}

		return null;
	}
}
