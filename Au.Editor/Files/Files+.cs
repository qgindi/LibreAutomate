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
	
	static FileNode _GetFolder(bool inSelectedFolder) {
		if (!inSelectedFolder) return App.Model.Root;
		var a = FilesModel.TreeControl.SelectedItems;
		if (a.Length == 1 && a[0].IsFolder) return a[0];
		dialog.showInfo(null, "Please right-click or select a folder.");
		return null;
	}
	
	static PanelFound.WorkingState _Prepare(out SciTextBuilder b, string title, string info) {
		var ws = Panels.Found.Prepare(PanelFound.Found.Repair, title, out b);
		if (ws.NeedToInitControl) {
			var k = ws.Scintilla;
			k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
		}
		b.Marker(c_markerInfo).Text(info).NL();
		return ws;
	}
	
	public static void MissingFiles(bool inSelectedFolder) {
		if (_GetFolder(inSelectedFolder) is not FileNode rootFolder) return;
		using var workingState = _Prepare(out var b, "Missing files", "These items represent files that already don't exist. You may want to delete them from the Files panel.");
		
		_Folder(rootFolder);
		void _Folder(FileNode folder) {
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
						_Folder(f);
					}
				}
			}
		}
		//note: don't use folder.Descendants(). It would also print files in missing folders.
		
		Panels.Found.SetResults(workingState, b);
	}
	
	static HashSet<string> _clickedLinks = new();
	
	public static void OrphanedFiles(bool inSelectedFolder) {
		if (_GetFolder(inSelectedFolder) is not FileNode rootFolder) return;
		using var workingState = _Prepare(out var b, "Orphaned files", "These files are in the workspace folder but not in the Files panel. You may want to import or delete them.");
		
		_clickedLinks.Clear();
		var hs = rootFolder.Descendants().Where(o => !o.IsLink).Select(o => o.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
		
		_Folder(rootFolder.FilePath);
		void _Folder(string folder) {
			foreach (var f in filesystem.enumerate(folder, FEFlags.IgnoreInaccessible | FEFlags.UseRawPath)) {
				var fp = f.FullPath;
				if (!hs.Contains(fp)) {
					string rp = fp[App.Model.FilesDirectory.Length..];
					string addTo = pathname.getDirectory(rp);
					b.NL().Link(() => { run.selectInExplorer(fp); }, rp); if(f.IsDirectory) b.Text(" (folder)");
					b.Text("\r\n\t").Link(() => { _Import(addTo, fp); }, "Import");
					if (!addTo.NE()) b.Text(" to folder ").Link(() => { App.Model.OpenAndGoTo2(addTo, "expand"); }, addTo);
					b.NL();
				} else if (f.IsDirectory) _Folder(fp);
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
		
		Panels.Found.SetResults(workingState, b);
	}
}
