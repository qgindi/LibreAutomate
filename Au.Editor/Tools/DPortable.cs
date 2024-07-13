/* role editorExtension; define SCRIPT; testInternal Au,Au.Editor,Au.Controls; r Au.Editor.dll; r Au.Controls.dll; /*/

using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Au.Controls;

#if SCRIPT
DPortable.ShowSingle();
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
	bool _exists, _copyPF, _copyWs, _copyDoc;
	string _skipDirsWs, _skipDirsDoc, _skipDirsApp;
	
	DPortable() {
#if SCRIPT
		print.clear();
		
		//test with smaller folders
		_dirThisApp = folders.ProgramFiles + "LibreAutomate";
		_dirWorkspace = folders.ThisAppDocuments + "Main";
		
		(string[] portable_skip, int _) sett = default;
#else
		var sett = App.Settings;
#endif
		if (sett.portable_skip.Lenn_() != 3) sett.portable_skip = [".git\r\n\\exe", ".git", "\\Git"];
		
		if (_dirPortableApp.NE() && folders.RemovableDrive0.Path is string drive) App.Settings.portable_dir = _dirPortableApp = drive + @"PortableApps\LibreAutomate";
		if (filesystem.more.comparePaths(_dirThisApp, _dirNet) is CPResult.Same or CPResult.AContainsB) _dirNet = _dirNetDesktop = null;
		if (!filesystem.exists(_dirScriptDataLocal).Directory) _dirScriptDataLocal = null;
		if (!filesystem.exists(_dirScriptDataRoaming).Directory) _dirScriptDataRoaming = null;
		
		InitWinProp("Portable LibreAutomate setup", App.Wmain);
		var b = new wpfBuilder(this).WinSize(500);
		
		b.R.Add("Install in folder", out TextBox tDir).Focus().Validation(_ => _ValidateFolder());
		
		b.R.StartStack(out KGroupBox gExists, "The folder already exists", vertical: true);
		gExists.Visibility = Visibility.Collapsed;
		b.Add(out KCheckBox cUpdatePF, "Update program files (LA, .NET)");
		b.Add(out KCheckBox cUpdateWs, "Update workspace (scripts etc)");
		b.Add(out KCheckBox cUpdateDoc, "Update settings and other data")
			.Validation(_ => _exists && !cUpdatePF.IsChecked && !cUpdateWs.IsChecked && !cUpdateDoc.IsChecked ? "All 'Update' unchecked" : null);
		b.End();
		
		b.R.StartGrid<KGroupBox>("Skip folders").Columns(-1, 20, -1, 20, -1);
		var tSkipWs = _AddSkip(0, _dirWorkspace, "workspace");
		var tSkipDoc = _AddSkip(1, _dirThisAppDocuments, "documents");
		var tSkipApp = _AddSkip(2, _dirThisApp, "program");
		b.End();
		
		TextBox _AddSkip(int dir, string linkPath, string linkName) {
			if (dir > 0) b.Skip();
			b.StartStack(vertical: true);
			b.Add<TextBlock>().FormatText($"In <a href=\"{linkPath}\">{linkName}</a>");
			b.Add(out TextBox t, sett.portable_skip[dir])
				.Multiline(60, TextWrapping.NoWrap)
				.Tooltip("These folders will not be copied. Existing folders will not be deleted or updated.\r\nExamples:\r\nDescendantFolderName\r\n\\DirectChildFolderName\r\n\\Folder1\\Folder2");
			t.TextChanged += (_, _) => { sett.portable_skip[dir] = t.Text; };
			b.End();
			return t;
		}
		
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
		};
		tDir.Text = _dirPortableApp;
		
		b.OkApply += e => {
			_copyPF = !_exists || cUpdatePF.IsChecked;
			_copyWs = !_exists || cUpdateWs.IsChecked;
			_copyDoc = !_exists || cUpdateDoc.IsChecked;
			_skipDirsWs = tSkipWs.Text;
			_skipDirsDoc = tSkipDoc.Text;
			_skipDirsApp = tSkipApp.Text;
			
			Task.Run(() => {
				try { _InstallThread(); }
				catch (Exception e1) { dialog.showError(@"Failed", e1.ToString()); }
			});
		};
	}
	
	void _InstallThread() {
		//using var p1 = perf.local();
		print.it($"<><lc YellowGreen>{(_exists ? "Updating" : "Installing")} portable LibreAutomate. Please wait until DONE.<>");
		if (_copyPF) _CopyPF();
		if (_copyDoc || _copyWs) _CopyData();
		print.it($"<>DONE. Installed in <link>{_dirPortableApp}<>.");
		
		void _CopyPF() {
			_Copy(_dirThisApp, _dirPortableApp, "/mir /xf unins* /xd dotnet data", _skipDirsApp);
			
			if (_dirNet != null) {
				var dotnet = _dirPortableApp + @"\dotnet";
				_Copy(_dirNet, dotnet, "/e");
				_Copy(_dirNetDesktop, dotnet, "/e");
			}
		}
		
		void _CopyData() {
			var data = _dirPortableApp + @"\data\";
			
			if (_copyDoc && _dirScriptDataLocal != null) _Copy(_dirScriptDataLocal, data + @"AppLocal\_script", $"""/mir /xd "{_dirScriptDataLocal}\iconCache" """);
			if (_copyDoc && _dirScriptDataRoaming != null) _Copy(_dirScriptDataRoaming, data + @"AppRoaming\_script", "/mir");
			
			string portableWsName = pathname.getName(_dirWorkspace), portableWsPath = $@"{data}doc\{portableWsName}";
			if (_copyDoc) _Copy(_dirThisAppDocuments, data + "doc", $"""/mir /xd "{_dirWorkspace}" "{portableWsPath}" """, _skipDirsDoc);
			if (!_copyWs) _copyWs = !filesystem.exists(portableWsPath);
			if (_copyWs) _Copy(_dirWorkspace, portableWsPath, $"""/mir /xd "{_dirWorkspace}\.compiled" "{_dirWorkspace}\.temp" """, _skipDirsWs);
			
			print.it("Changing settings");
			var file = data + @"doc\.settings\Settings.json";
			var j = JsonNode.Parse(filesystem.loadBytes(file));
			j["workspace"] = $@"%folders.ThisAppDocuments%\{portableWsName}";
			filesystem.saveText(file, j.ToJsonString());
			if (_copyWs) {
				file = portableWsPath + @"\settings.json";
				var s = filesystem.loadText(file);
				s = s.Replace("\"gitBackup\": true", "\"gitBackup\": false");
				filesystem.saveText(file, s);
			}
		}
		
		static void _Copy(string dir, string dirTo, string how, string skipDirs = null) {
			dir = pathname.unprefixLongPath(dir); //robocopy unaware about "\\?\"
			dirTo = pathname.unprefixLongPath(dirTo);
			
			print.it($"Copying {dir}");
			var s = $"""
"{dir}" "{dirTo}" {how} /xj /r:0 /w:1 /np /mt{_GetSkipDirsCL()}
""";
			//info: the `/r:0` turns off the robocopy's slow and unreliable retries. Use this loop instead.
			//tested: with `/mt` (multithreaded) faster eg 20 -> 17 s or 49 -> 31 s. Faster even if HDD.
			
			for (int i = 0; ;) {
				int r = run.console(out var so, "robocopy.exe", s);
				//print.it($"<><c red>{r}<>"); print.it(so);
				if ((uint)r < 8) break;
				if (++i == 5) {
					print.it($"<><c red>Failed to copy '{dir}' to '{dirTo}'.<>\r\n<\a>{so}</\a>");
					if (r == 16) throw new AuException();
					break;
				}
				100.ms();
			}
			
			//CONSIDER: options:
			//	1. Keep new and newer portable files. (instead of /mir use /e /xo; or /mir /xo /xx)
			//		Bad: if both files modified...
			//	2. Copy symbolic link target. And workspace link target.
			
			string _GetSkipDirsCL() {
				if (skipDirs == null) return null;
				var a = skipDirs.Lines(noEmpty: true);
				if (a.Length == 0) return null;
				StringBuilder b = new(" /xd");
				foreach (var s in a) {
					//workaround for: robocopy does not support relative paths. Can be either filename or full path.
					//	If filename, skips all source and dest descendants with that name.
					//	To achieve the same with a relative path, specify full paths in both source and dest dir.
					if (s.Starts('\\')) b.Append($" \"{dir}{s}\" \"{dirTo}{s}\"");
					else b.Append($" \"{s}\"");
				}
				return b.ToString();
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
		_BigFiles(_dirThisAppDocuments, "documents");
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
