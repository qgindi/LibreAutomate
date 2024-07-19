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

enum FNInsert { Last, First, Before, After }

record struct FNInsertPos(FileNode f, FNInsert pos) {
	public bool Inside => pos is FNInsert.First or FNInsert.Last;
	public FileNode ParentFolder => Inside ? f : f.Parent;
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
	
	public static void Repair() {
		var rootFolder = App.Model.Root;
		
		using var workingState = Panels.Found.Prepare(PanelFound.Found.Repair, "Repair", out var b);
		if (workingState.NeedToInitControl) {
			var k = workingState.Scintilla;
			k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
		}
		
		//broken links
		
		b.Marker(c_markerInfo).Text("Broken links (missing target). You may want to delete them from the Files panel.").NL();
		var links = rootFolder.Descendants().Where(o => o.IsLink && !filesystem.exists(o.FilePath)).ToArray();
		if (links.Length > 0) {
			foreach (var f in links) {
				b.NL().Link(f, f.ItemPath); if (f.IsFolder) b.Text(" (folder)");
				b.Text(" -> ").Text(f.FilePath);
			}
			b.NL();
		} else {
			b.Text("\r\nNone.\r\n");
		}
		
		//biggest files
		
		b.NL().Marker(c_markerInfo).Text("Big files. You may want to delete some files if not used. Size in KB.").NL();
		List<(long size, FileNode f)> biggest = new();
		_Biggest(rootFolder);
		void _Biggest(FileNode folder) {
			foreach (var f in folder.Children()) {
				if (f.IsLink) continue;
				if (f.IsFolder) {
					_Biggest(f);
				} else if (filesystem.getProperties(f.FilePath, out var p, FAFlags.UseRawPath | FAFlags.DontThrow) && p.Size >= 50 * 1024) {
					biggest.Add((p.Size, f));
				}
			}
		}
		if (biggest.Any()) {
			foreach (var (size, f) in biggest.OrderByDescending(o => o.size)) {
				b.NL().Text($"{size / 1024} ").Link(f, f.ItemPath);
			}
		} else {
			b.Text("\r\nNone.\r\n");
		}
		
		//--------------
		
		Panels.Found.SetResults(workingState, b);
	}
}
