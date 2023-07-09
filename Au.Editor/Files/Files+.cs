using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Au.Compiler;

/// <summary>
/// File type of a <see cref="FileNode"/>.
/// Saved in XML as tag name: d folder, s script, c class, n other.
/// </summary>
enum FNType : byte {
	Folder, //must be 0
	Script,
	Class,
	Other,
}

enum FNInsert {
	Inside, Before, After
}

enum FNFind {
	Any, File, Folder, CodeFile, Class/*, Script*/ //Script not useful because class files can be executable too
}

enum FNClassFileRole {
	/// <summary>Not a class file.</summary>
	None,
	/// <summary>Has meta role miniProgram/exeProgram/editorExtension.</summary>
	App,
	/// <summary>Has meta role classLibrary.</summary>
	Library,
	/// <summary>Has meta role classFile, or no meta role.</summary>
	Class,
}

class NewFileText {
	public bool replaceTemplate;
	public string text, meta;
	
	public NewFileText() { }
	
	public NewFileText(bool replaceTemplate, string text, string meta = null) {
		this.replaceTemplate = replaceTemplate;
		this.text = text;
		this.meta = meta;
	}
}

class DNewWorkspace : KDialogWindow {
	string _name, _location;
	public string ResultPath { get; private set; }
	
	public DNewWorkspace(string name, string location) {
		_name = name;
		_location = location;
		
		Title = "New workspace";
		
		var b = new wpfBuilder(this).WinSize(600);
		TextBox tName, tLocation = null;
		b.R.Add("Folder name", out tName, _name).Validation(_Validate).Focus();
		b.R.Add("Parent folder", out tLocation, _location).Validation(_Validate);
		b.R.AddButton("Browse...", _Browse).Width(70, "L");
		b.R.AddOkCancel();
		b.End();
		
		void _Browse(WBButtonClickArgs e) {
			var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") {
				InitFolderNow = filesystem.exists(tLocation.Text).Directory ? tLocation.Text : folders.ThisAppDocuments,
			};
			if (d.ShowOpen(out string s, this, selectFolder: true)) tLocation.Text = s;
		}
		
		string _Validate(FrameworkElement e) {
			var s = (e as TextBox).Text.Trim();
			if (e == tLocation) {
				if (!filesystem.exists(s).Directory) return "Folder does not exist";
			} else {
				if (pathname.isInvalidName(s) || s[0] is '.' or '_') return "Invalid filename";
				ResultPath = pathname.combine(tLocation.Text, s); //validation is when OK clicked
				if (filesystem.exists(ResultPath)) return s + " already exists";
			}
			return null;
		}
		
		//b.OkApply += e => {
		//	print.it(ResultPath); e.Cancel = true;
		//};
	}
}

class RepairWorkspace {
	const int c_markerInfo = 0;
	static HashSet<string> _clickedLinks = new();
	
	public static void Repair(bool inSelectedFolder) {
		var rootFolder = App.Model.Root;
		if (inSelectedFolder) {
			var a = FilesModel.TreeControl.SelectedItems;
			if (a.Length == 1 && a[0].IsFolder) rootFolder = a[0];
			else {
				dialog.showInfo(null, "Please right-click or select a folder.", owner: App.Hmain);
				return;
			}
		}
		
		using var workingState = Panels.Found.Prepare(PanelFound.Found.Repair, "Repair", out var b);
		if (workingState.NeedToInitControl) {
			var k = workingState.Scintilla;
			k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
		}
		
		//--------------
		
		b.Marker(c_markerInfo).Text("These items represent files that already don't exist. You may want to delete them from the Files panel.").NL();
		_Missing(rootFolder);
		void _Missing(FileNode folder) {
			foreach (var f in folder.Children()) {
				var path = f.FilePath;
				if (!filesystem.exists(path)) {
					int linkLen = 0;
					for (var s = path; !(s = pathname.getDirectory(s)).NE();) {
						if (filesystem.exists(s).Directory) { linkLen = s.Length; break; }
					}
					b.NL().Link(f, f.ItemPath); if (f.IsFolder) b.Text(" (folder)");
					var dir = path[..linkLen];
					b.NL().Link2(() => { run.itSafe(dir); }, $"\tMissing: {dir}").Text(path.AsSpan(linkLen..)).NL();
				} else if (f.IsFolder) {
					if (f.IsSymlink && !filesystem.more.getFileId(path, out _)) {
						b.NL().Link(f, f.ItemPath).Text(" (folder link)");
						b.Text("\r\n\tMissing target of symbolic link ").Link2(() => { run.selectInExplorer(path); }, path).NL();
					} else {
						_Missing(f);
					}
				}
			}
		}
		//note: don't use folder.Descendants(). It would also print files in missing folders.
		
		//--------------
		
		b.NL().Marker(c_markerInfo).Text("These files are in the workspace folder but not in the Files panel. You may want to import or delete them.").NL();
		_clickedLinks.Clear();
		var hs = rootFolder.Descendants().Where(o => !o.IsLink).Select(o => o.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
		_Orphaned(rootFolder.FilePath);
		void _Orphaned(string folder) {
			foreach (var f in filesystem.enumerate(folder, FEFlags.IgnoreInaccessible | FEFlags.UseRawPath)) {
				var fp = f.FullPath;
				if (!hs.Contains(fp)) {
					string rp = fp[App.Model.FilesDirectory.Length..];
					string addTo = pathname.getDirectory(rp);
					b.NL().Link(() => { run.selectInExplorer(fp); }, rp); if (f.IsDirectory) b.Text(" (folder)");
					b.Text("\r\n\t").Link(() => { _Import(addTo, fp); }, "Import");
					if (!addTo.NE()) b.Text(" to folder ").Link(() => { App.Model.OpenAndGoTo2(addTo, "expand"); }, addTo);
					b.NL();
				} else if (f.IsDirectory) _Orphaned(fp);
			}
		}
		
		void _Import(string addTo, string fp) {
			if (!_clickedLinks.Add(fp)) return;
			var f = App.Model.Find(addTo, FNFind.Folder);
			var pos = FNInsert.Inside;
			if (f != null) {
				f.SelectSingle();
				FilesModel.TreeControl.Expand(f, true);
			} else {
				f = App.Model.Root.FirstChild;
				if (f != null) pos = FNInsert.Before; else f = App.Model.Root;
			}
			App.Model.ImportFiles(new[] { fp }, f, pos, dontPrint: true);
		}
		
		//--------------
		
		List<(long size, FileNode f)> biggest = new();
		_Biggest(rootFolder);
		void _Biggest(FileNode folder) {
			foreach (var f in folder.Children()) {
				if (f.IsFolder) {
					if (!f.IsSymlink) _Biggest(f);
				} else if (!f.IsLink && filesystem.getProperties(f.FilePath, out var p, FAFlags.UseRawPath | FAFlags.DontThrow) && p.Size >= 50 * 1024) {
					biggest.Add((p.Size, f));
				}
			}
		}
		if (biggest.Any()) {
			b.NL().Marker(c_markerInfo).Text("Biggest files. You may want to delete some files if obsolete, garbage, etc. Size in KB.").NL();
			foreach (var (size, f) in biggest.OrderByDescending(o => o.size)) {
				b.NL().Text($"{size / 1024} ").Link(f, f.ItemPath);
			}
		}
		
		//--------------
		
		List<FileNode> folderLinks = null;
		_FolderLinks(rootFolder);
		void _FolderLinks(FileNode parent) {
			foreach (var f in parent.Children()) {
				if (f.IsFolder) {
					if (!f.IsSymlink) _FolderLinks(f);
					else (folderLinks ??= new()).Add(f);
				}
			}
		}
		if (folderLinks != null) {
			b.NL().NL().Marker(c_markerInfo).Text(@"Folder links. You may want to change the NTFS link type *junction* to *symbolic link* or vice versa.
Usually it does not matter, but for example git adds entire target folder if it's a junction (probably you don't want it).").NL();
			foreach (var f in folderLinks) {
				var linkPath = f.FilePath;
				int type = filesystem.IsNtfsLink_(linkPath);
				var st = type switch { 1 => "symbolic link", 2 => "junction", _ => "?" };
				string target = null;
				if (type != 0) {
					if (!filesystem.more.getFinalPath(f.FilePath, out target)) st = "invalid " + st;
				} else {
					if (!filesystem.exists(linkPath)) continue;
				}
				b.NL().Link(f, f.ItemPath).Text($" --> {target ?? "?"}\r\n\tType: {st}.  ");
				b.Link(() => _ChangeLinkType(f, target, type), type == 0 || target.NE() ? "Repair" : "Change").NL();
			}
			
			void _ChangeLinkType(FileNode f, string target, int type) {
				if (f.IsAlien) return;
				string linkPath = f.FilePath, tempPath = null;
				
				if (type == 0) { //not a reparse point. If eg git-clone'd, probably it's a text file containing the target path.
					string s = null;
					switch (filesystem.exists(linkPath, true)) {
					case 1:
						s = filesystem.loadText(linkPath);
						break;
					}
					if (s.NE() || s.Length > pathname.maxDirectoryPathLength || s.Any(o => pathname.isInvalidPathChar(o))) { print.it("Cannot convert. The file isn't a NTFS link."); return; }
					s = s.Replace('/', '\\');
					if (!dialog.showInput(out s, "Convert to symbolic link?", $"Link path: {linkPath}\n\nThe file isn't a NTFS link. It's a file containing this text. If it's the target path, click OK. Edit if need.", editText: s, owner: App.Wmain)) return;
					tempPath = pathname.makeUnique(linkPath, true);
					filesystem.move(linkPath, tempPath);
					target = s;
				} else if (target.NE()) { //eg downloaded from git in zip file
					if (!dialog.showInput(out target, "Link target path", $"Link path: {linkPath}\n\nThe link is invalid. Its target path is unknown. Enter target path and click OK to repair the link.", owner: App.Wmain)) return;
					type = 0;
				}
				
				try { filesystem.more.createSymbolicLink(linkPath, target, type != 1 ? CSLink.Directory : CSLink.Junction, elevate: true, deleteOld: true); }
				catch (Exception e1) {
					print.it(e1.ToStringWithoutStack());
					if (tempPath != null) { filesystem.move(tempPath, linkPath); tempPath = null; }
				}
				finally { if (tempPath != null) filesystem.delete(tempPath); }
			}
		}
		
		//--------------
		
		Panels.Found.SetResults(workingState, b);
	}
	
	//rejected. Anyway git symlinks usually are broken.
	//public static void FolderLinksNote() {
	//	if (_DirHasJunctions(App.Model.Root)) {
	//		Panels.Output.Scintilla.AaTags.AddLinkTag("+symlinkRepair", s => { if (s == "r") Repair(false); else App.Model.WSSett.ok_symlinks = true; });
	//		print.warning($@"You may want to repair folder links. <fold>
	//Some folder links in the workspace are NTFS links of type *junction*. You may want to change the type to *symbolic link*.
	//Folder links are created when importing a folder as a link. Older LibreAutomate used junctions, now symbolic links, because it works better with git.
	//<+symlinkRepair r>Repair...<>  <+symlinkRepair>Never mind<>
	//</fold>", -1, "<>Note: ");
	//	} else App.Model.WSSett.ok_symlinks = true;
	
	//	bool _DirHasJunctions(FileNode dir) {
	//		foreach (var f in dir.Children()) {
	//			if (f.IsFolder) {
	//				if (f.IsSymlink) {
	//					var path = f.FilePath;
	//					if (filesystem.IsNtfsLink_(path) == 2 && filesystem.more.getFinalPath(path, out var target)) return true;
	//				} else {
	//					if (_DirHasJunctions(f)) return true;
	//				}
	//			}
	//		}
	//		return false;
	//	}
	//}
}
