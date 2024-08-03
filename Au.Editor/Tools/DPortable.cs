using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Au.Controls;

class DPortable : KDialogWindow {
	public static void ShowSingle() {
		if (App.IsPortable) {
			dialog.showError("Portable mode", "Please restart this program in non-portable mode.", owner: App.Wmain);
			return;
		}
		ShowSingle(() => new DPortable());
	}
	
	string _dirNet = folders.NetRuntime, _dirNetDesktop = folders.NetRuntimeDesktop;
	bool _exists;
	
	record class _Dir(string local, string portableRelative) {
		public string portable => portableRelative.NE() ? App.Settings.portable_dir : App.Settings.portable_dir + "\\" + portableRelative;
		public bool copy;
		public string skip;
		public KCheckBox cCopy;
		public TextBox tSkip;
	}
	
	_Dir _dApp, _dWs, _dSett, _dScript, _dDoc, _dRoaming;
	
	DPortable() {
		if (App.Settings.portable_dir.NE() && folders.RemovableDrive0.Path is string drive) App.Settings.portable_dir = drive + @"PortableApps\LibreAutomate";
		if (filesystem.more.comparePaths(folders.ThisApp, _dirNet) is CPResult.Same or CPResult.AContainsB) _dirNet = _dirNetDesktop = null;
		if (App.Settings.portable_skip.Lenn_() != 6) App.Settings.portable_skip = ["\\Git", ".git\r\n\\exe", "", "", ".git", ""];
		
		InitWinProp("Portable LibreAutomate setup", App.Wmain);
		var b = new wpfBuilder(this);
		
		b.R.Add("Install in folder", out TextBox tDir).Focus().Validation(_ => _ValidateFolder());
		
		b.R.xAddInfoBlockT(null);
		var tbInfoExists = b.Last as TextBlock;
		
		int iDir = 0;
		b.R.StartGrid().Span(2).Columns(0, -1);
		b.R.Add<TextBlock>("Copy folder").Font(bold: true).Add<TextBlock>("Skip subfolders").Font(bold: true);
		_dApp = _AddDir(folders.ThisApp, "", "Program files (LA, .NET)");
		_dWs = _AddDir(App.Model.WorkspaceDirectory, @"data\doc\" + pathname.getName(App.Model.WorkspaceDirectory), "Workspace (scripts etc)");
		_dSett = _AddDir(folders.ThisAppDocuments + ".settings", @"data\doc\.settings", @"Settings");
		_dScript = _AddDir(folders.ThisAppDocuments + "_script", @"data\doc\_script", @"Script data");
		_dDoc = _AddDir(folders.ThisAppDocuments, @"data\doc", @"Documents (except the above 3 subfolders)");
		_dRoaming = _AddDir(folders.ThisAppDataRoaming, @"data\AppRoaming", @"AppData");
		b.R.AddSeparator(false);
		b.End();
		
		_Dir _AddDir(string local, string portableRelative, string label) {
			int i = iDir++;
			
			if (!filesystem.exists(local).Directory) filesystem.createDirectory(local);
			_Dir d = new(local, portableRelative);
			
			b.R.AddSeparator(false);
			
			b.R.StartStack(vertical: true);
			b.Add(out d.cCopy, label).Checked(d.copy = 0 != (App.Settings.portable_check & 1 << i));
			d.cCopy.CheckChanged += (_, _) => App.Settings.portable_check.SetFlag_(1 << i, d.copy = d.cCopy.IsChecked);
			b.xAddInfoBlockF($"<a href=\"{local}\">{local.Limit(70, middle: true)}</a>{(i == 0 ? "" : " -> \\")}{portableRelative}").Padding("T1 B2");
			b.End();
			
			b.Add(out d.tSkip, d.skip = App.Settings.portable_skip[i]).Multiline(wrap: TextWrapping.NoWrap).Width(100..500)
				.Tooltip("These folders will not be copied. Existing folders will not be deleted or updated.\r\nExamples:\r\nDescendantFolderName\r\n\\DirectChildFolderName\r\n\\Folder1\\Folder2\r\n//comment");
			d.tSkip.TextChanged += (o, _) => { App.Settings.portable_skip[i] = d.skip = (o as TextBox).Text; };
			
			return d;
		}
		
		b.R.StartOkCancel();
		b.AddButton("Print details", _ => _PrintDetails()).Width(100);
		b.AddButton("Install", null, WBBFlags.OK);
		b.AddButton("Cancel", null, WBBFlags.Cancel);
		b.AddButton("Help", _ => HelpUtil.AuHelp("editor/Portable app")).Width(70);
		b.End();
		
		b.End();
		
		tDir.TextChanged += (_, _) => {
			var s = App.Settings.portable_dir = tDir.Text.Trim().TrimEnd("\\/");
			_exists = filesystem.exists(s).Directory;
			tbInfoExists.Text = _exists ? "The portable folder already exists.\nChecked portable folders will be updated." : s == "" ? "" : "The folder still does not exist; it will be created.\nCheck all 'Copy folder'. Or only those you want to copy.\nIn the future you can use this tool to update portable files.";
			if (_exists && filesystem.exists(_dApp.portable).Directory) _dApp.cCopy.IsEnabled = true; else (_dApp.cCopy.IsEnabled, _dApp.cCopy.IsChecked) = (false, true);
		};
		tDir.Text = App.Settings.portable_dir;
		
		b.OkApply += e => {
			Task.Run(() => {
				try { _InstallThread(); }
				catch (Exception e1) { dialog.showError(@"Failed", e1.ToString()); }
			});
		};
	}
	
	void _InstallThread() {
		print.it($"<><lc YellowGreen>{(_exists ? "Updating" : "Installing")} portable LibreAutomate. Please wait until DONE.<>");
		
		Action portableWsPathSetting = _WsPathInPortableSettings();
		
		if (_dApp.copy || !_exists) {
			_Copy(_dApp, "/mir /xf unins* /xd dotnet data");
			
			if (_dirNet != null) {
				var dotnet = App.Settings.portable_dir + @"\dotnet";
				_Copy2(_dirNet, dotnet, "/e");
				_Copy2(_dirNetDesktop, dotnet, "/e");
			}
		}
		
		if (_dDoc.copy) _Copy(_dDoc, $"""/mir /xd "{_dWs.local}" "{_dWs.portable}" "{_dSett.local}" "{_dSett.portable}" "{_dScript.local}" "{_dScript.portable}" """);
		else filesystem.createDirectory(_dDoc.portable); //need subdir "data" to detect portable mode
		if (_dWs.copy) _Copy(_dWs, $"""/mir /xd "{_dWs.local}\.compiled" "{_dWs.local}\.temp" """);
		if (_dSett.copy) _Copy(_dSett);
		if (_dScript.copy) _Copy(_dScript);
		if (_dRoaming.copy) _Copy(_dRoaming);
		
		portableWsPathSetting?.Invoke();
		if (_dWs.copy) {
			var file = _dWs.portable + @"\settings.json";
			var s = filesystem.loadText(file);
			s = s.Replace("\"gitBackup\": true", "\"gitBackup\": false");
			filesystem.saveText(file, s);
		}
		
		print.it($"<>DONE. Installed in <link>{App.Settings.portable_dir}<>.");
		
		static void _Copy(_Dir d, string how = "/mir") => _Copy2(d.local, d.portable, how, d.skip);
		
		static void _Copy2(string dir, string dirTo, string how, string skipDirs = null) {
			print.it($"Copying {dir}");
			var s = _RobocopyArgs(false, dir, dirTo, how, skipDirs);
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
		}
		
		Action _WsPathInPortableSettings() {
			if (!_dSett.copy && !_dWs.copy) return null;
			
			string wsPath = null, file = _dSett.portable + @"\Settings.json";
			if (!_dWs.copy) { //preserve the portable workspace path setting when replacing the portable settings file
				try { wsPath = (string)JsonNode.Parse(filesystem.loadBytes(file))["workspace"]; }
				catch { }
			}
			
			return () => {
				var j = filesystem.exists(file) ? JsonNode.Parse(filesystem.loadBytes(file)) : new JsonObject();
				j["workspace"] = wsPath ?? $@"%folders.ThisAppDocuments%\{pathname.getName(_dWs.local)}";
				filesystem.saveText(file, j.ToJsonString());
			};
		}
	}
	
	static string _RobocopyArgs(bool log, string dir, string dirTo, string how, string skipDirs) {
		dir = pathname.unprefixLongPath(dir); //robocopy unaware about "\\?\"
		dirTo = pathname.unprefixLongPath(dirTo);
		
		StringBuilder b = new($"""
"{dir}" "{dirTo}" {how} /xj /r:0 /w:1 /np /mt
""");
		
		if (!skipDirs.NE() && skipDirs.Lines(noEmpty: true) is var a) {
			bool once = false;
			foreach (var s in a) {
				if (s.Starts("//")) continue;
				if (!once) { once = true; b.Append(" /xd"); }
				
				//workaround for: robocopy does not support relative paths. Can be either filename or full path.
				//	If filename, skips all source and dest descendants with that name.
				//	To achieve the same with a relative path, specify full paths in both source and dest dir.
				if (s[0] is '\\' or '/') b.Append($" \"{dir}{s}\" \"{dirTo}{s}\"");
				else b.Append($" \"{s}\"");
			}
		}
		
		if (log) b.Append(" /l /bytes /nfl /njh");
		
		return b.ToString();
	}
	
	string _RobocopyArgs(bool log, _Dir d, string how) => _RobocopyArgs(log, d.local, d.portable, how, d.skip);
	
	async void _PrintDetails() {
		print.clear();
		
		static async Task<long> _GetDirSize2(string dir, string how = "/mir", string skipDirs = null) {
			return await Task.Run(() => {
				var s = _RobocopyArgs(true, dir, folders.Temp + "06e5f7d0-13ec-438a-a415-53b624d3c308", how, skipDirs);
				run.console(out var so, "robocopy.exe", s);
				if (so.RxMatch(@"(?m)^\h*Bytes\h*:\h*\K\d+", 0, out so) && so.ToNumber(out long k)) return k;
				return -1;
			});
		}
		
		static async Task<long> _GetDirSize(_Dir d, string how = "/mir") => await _GetDirSize2(d.local, how, d.skip);
		
		long sizeApp = await _GetDirSize(_dApp);
		long sizeNet = _dirNet == null ? 0 : await _GetDirSize2(_dirNet) + await _GetDirSize2(_dirNetDesktop);
		long sizeWs = await _GetDirSize(_dWs);
		long sizeSett = await _GetDirSize(_dSett);
		long sizeScript = await _GetDirSize(_dScript);
		long sizeDoc = await _GetDirSize(_dDoc, $"""/mir /xd "{_dWs.local}" "{_dSett.local}" "{_dScript.local}" """);
		long sizeRoaming = await _GetDirSize(_dRoaming);
		
		var b = new StringBuilder();
		b.AppendLine($"""
<><lc YellowGreen>Portable LibreAutomate setup details. The 'Skip subfolders' setting is applied.<>
Total size: {_MB(sizeApp + sizeNet + sizeWs + sizeSett + sizeScript + sizeDoc + sizeRoaming)} MB.
Folder sizes (MB):
	{"Program",-15} {_MB(sizeApp)}
	{".NET Runtime",-15} {_MB(sizeNet)}
	{"Workspace",-15} {_MB(sizeWs)}
	{"Settings",-15} {_MB(sizeSett)}
	{"Script data",-15} {_MB(sizeScript)}
	{"Documents",-15} {_MB(sizeDoc)} (except the above 3 subfolders)
	{"AppData",-15} {_MB(sizeRoaming)}
""");
		
		_Links(b);
		
		print.it(b.ToString());
		print.scrollToTop();
		
		static string _MB(long size) {
			const int MB = 1024 * 1024;
			if (size < 0) return "<failed>";
			if (size == 0) return "0";
			if (size < MB) return "<1";
			return (size / MB).ToString();
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
		if (App.Settings.portable_dir.NE()) return "Where to install?";
		
		//error if workspace is in portable folder (shared). Or part of it (link target).
		if (filesystem.exists(App.Settings.portable_dir)) {
			bool bad = filesystem.more.comparePaths(App.Settings.portable_dir, _dWs.local) is CPResult.AContainsB or CPResult.BContainsA or CPResult.Same;
			if (!bad) {
				foreach (var f in App.Model.Root.Descendants()) {
					if (f.IsLink) {
						var s = f.FilePath;
						if (filesystem.more.comparePaths(App.Settings.portable_dir, s) is CPResult.AContainsB or CPResult.BContainsA or CPResult.Same) {
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
