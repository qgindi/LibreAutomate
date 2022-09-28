using Au.Controls;
using Au.Compiler;
using System.Xml;
using System.Xml.Linq;

partial class FileNode : TreeBase<FileNode>, ITreeViewItem
{
	#region types

	//Not saved in file.
	[Flags]
	enum _State : byte
	{
		Deleted = 1,
	}

	//Saved in file.
	[Flags]
	enum _Flags : byte
	{
		// = 1,
	}

	#endregion

	#region fields, ctors, load/save

	FilesModel _model;
	string _name;
	string _displayName;
	uint _id;
	EFileType _type;
	_State _state;
	_Flags _flags;
	string _linkTarget;
	string _icon;
	uint _testScriptId;

	//this ctor is used when creating new item of known type
	public FileNode(FilesModel model, string name, EFileType type) {
		_model = model;
		_type = type;
		_SetName(name);
		_id = _model.AddGetId(this);
	}

	//this ctor is used when importing items from files etc.
	//name is filename with extension.
	//sourcePath is used when !isFolder: 1. To detect type. 2. If isLink, to set link target.
	public FileNode(FilesModel model, string name, string sourcePath, bool isFolder, bool isLink = false) {
		_model = model;
		_type = isFolder ? EFileType.Folder : _DetectFileType(sourcePath);
		_SetName(name);
		_id = _model.AddGetId(this);
		//if (isLink && !isFolder) _linkTarget = folders.unexpandPath(sourcePath); //rejected. Too much trouble with it.
		if (isLink && !isFolder) _linkTarget = sourcePath;
	}

	//this ctor is used when copying or importing a workspace.
	//Deep-copies fields from f, except _model, _name, _id (generates new) and _testScriptId.
	FileNode(FilesModel model, FileNode f, string name) {
		_model = model;
		_SetName(name);
		_type = f._type;
		_state = f._state;
		_flags = f._flags;
		_linkTarget = f._linkTarget;
		_icon = f.CustomIconName;
		_id = _model.AddGetId(this);
	}

	//this ctor is used when reading files.xml
	FileNode(XmlReader x, FileNode parent, FilesModel model) {
		_model = model;
		if (parent == null) { //the root node
			if (x.Name != "files") throw new ArgumentException("XML root element name must be 'files'");
			x["max-i"].ToInt(out uint u);
			_model.MaxId = u;
		} else {
			_type = XmlTagToFileType(x.Name, canThrow: true);
			uint id = 0;
			while (x.MoveToNextAttribute()) {
				var v = x.Value; if (v.NE()) continue;
				switch (x.Name) {
				case "n": _SetName(v); break;
				case "i": v.ToInt(out id); break;
				case "f": _flags = (_Flags)v.ToInt(); break;
				case "path": if (!IsFolder) _linkTarget = v; break;
				case "icon": _icon = v; break;
				case "run": v.ToInt(out _testScriptId); break;
				}
			}
			if (_name == null) throw new ArgumentException("no 'n' attribute in XML");
			_id = _model.AddGetId(this, id);
		}
	}

	public static FileNode Load(string file, FilesModel model) => XmlLoad(file, (x, p) => new FileNode(x, p, model));

	public void Save(string file) => XmlSave(file, (x, n) => n._XmlWrite(x, false));

	void _XmlWrite(XmlWriter x, bool exporting) {
		if (Parent == null) {
			x.WriteStartElement("files");
			if (_model != null) x.WriteAttributeString("max-i", _model.MaxId.ToString()); //null when exporting
		} else {
			string t = "n";
			switch (_type) {
			case EFileType.Folder: t = "d"; break;
			case EFileType.Script: t = "s"; break;
			case EFileType.Class: t = "c"; break;
			}
			x.WriteStartElement(t);
			x.WriteAttributeString("n", _name);
			if (!exporting) x.WriteAttributeString("i", _id.ToString());
			if (_flags != 0) x.WriteAttributeString("f", ((int)_flags).ToString());
			if (IsLink) x.WriteAttributeString("path", LinkTarget);
			var ico = CustomIconName; if (ico != null) x.WriteAttributeString("icon", ico);
			if (!exporting && _testScriptId != 0) x.WriteAttributeString("run", _testScriptId.ToString());
		}
	}

	public static void Export(FileNode[] a, string file) => new FileNode().XmlSave(file, (x, n) => n._XmlWrite(x, true), children: a);

	FileNode() { } //used by Export

	#endregion

	#region properties

	/// <summary>
	/// Panels.Files.TreeControl.
	/// </summary>
	public static KTreeView TreeControl => Panels.Files.TreeControl;

	/// <summary>
	/// Gets workspace that contains this file.
	/// </summary>
	public FilesModel Model => _model;

	/// <summary>
	/// Gets the root node (Model.Root).
	/// </summary>
	public FileNode Root => _model.Root;

	/// <summary>
	/// File type.
	/// </summary>
	public EFileType FileType => _type;

	/// <summary>
	/// true if folder or root.
	/// </summary>
	public bool IsFolder => _type == EFileType.Folder;

	/// <summary>
	/// true if script file.
	/// </summary>
	public bool IsScript => _type == EFileType.Script;

	/// <summary>
	/// true if class file.
	/// </summary>
	public bool IsClass => _type == EFileType.Class;

	/// <summary>
	/// true if script or class file.
	/// false if folder or not a code file.
	/// </summary>
	public bool IsCodeFile => _type == EFileType.Script || _type == EFileType.Class;

	/// <summary>
	/// true if not script/class/folder.
	/// </summary>
	public bool IsOtherFileType => _type == EFileType.Other;

	/// <summary>
	/// File name with extension.
	/// </summary>
	public string Name => _name;

	/// <summary>
	/// File name. Without extension if ends with ".cs".
	/// </summary>
	public string DisplayName => _displayName ??= _name.RemoveSuffix(".cs", true);

	void _SetName(string name) {
		if (_name != null) _model._nameMap.MultiRemove_(_name, this); //renaming
		_model._nameMap.MultiAdd_(_name = name, this);
		_displayName = null;
	}

	/// <summary>
	/// Unique id in this workspace. To find faster, with database, etc.
	/// Root id is 0.
	/// Ids of deleted items are not reused.
	/// </summary>
	public uint Id => _id;

	/// <summary>
	/// <see cref="Id"/> as string.
	/// </summary>
	public string IdString => _id.ToString();

	/// <summary>
	/// Formats string like "&lt;0x10000000A&gt;", with <see cref="Id"/> in low-order int and <see cref="FilesModel.WorkspaceSN"/> in high-order int.
	/// Such string can be passed to <see cref="FilesModel.Find"/>.
	/// </summary>
	public string IdStringWithWorkspace => "<0x" + (_id | ((long)_model.WorkspaceSN << 32)).ToString("X") + ">";

	/// <summary>
	/// Formats SciTags &lt;open&gt; link tag to open this file.
	/// </summary>
	/// <param name="path">In link name use <see cref="ItemPath"/> instead of name.</param>
	public string SciLink(bool path = false) => $"<open \"{IdStringWithWorkspace}\">{(path ? ItemPath : _name)}<>";

	/// <summary>
	/// true if is external file, ie not in this workspace folder.
	/// </summary>
	public bool IsLink => _linkTarget != null;

	/// <summary>
	/// If <see cref="IsLink"/>, returns target path, else null.
	/// </summary>
	public string LinkTarget => _linkTarget;

	/// <summary>
	/// Gets or sets custom icon name (like "*Pack.Icon color") or null.
	/// </summary>
	/// <remarks>
	/// The setter will save workspace.
	/// User can set custom icon: menu -> Tools -> Icons.
	/// Currently editor does not support item icons as .ico files etc. Not difficult to add, but probably don't need when we have 25000 XAML icons. For .exe files can use any icons.
	/// </remarks>
	public string CustomIconName {
		get => _icon;
		set {
			_icon = value;
			_model.Save.WorkspaceLater();
			Compiler.Uncache(this);
			FilesModel.Redraw(this);
		}
	}

	/// <summary>
	/// Gets or sets other item to run instead of this. None if null.
	/// The setter will save workspace.
	/// </summary>
	public FileNode TestScript {
		get {
			if (_testScriptId != 0) {
				var f = _model.FindById(_testScriptId); if (f != null) return f;
				TestScript = null;
			}
			return null;
		}
		set {
			uint id = value?._id ?? 0;
			if (_testScriptId == id) return;
			_testScriptId = id;
			_model.Save.WorkspaceLater();
		}
	}

	/// <summary>
	/// Gets or sets 'Delete' flag. Does nothing more.
	/// </summary>
	public bool IsDeleted {
		get => 0 != (_state & _State.Deleted);
		set { Debug.Assert(value); _state |= _State.Deleted; }
	}

	/// <summary>
	/// true if is deleted or is not in current workspace.
	/// </summary>
	public bool IsAlien => IsDeleted || _model != App.Model;

	/// <summary>
	/// Returns item path in workspace, like @"\Folder\Name.cs" or @"\Name.cs".
	/// Returns null if this item is deleted.
	/// </summary>
	public string ItemPath => _ItemPath();

	[SkipLocalsInit]
	unsafe string _ItemPath(string prefix = null) {
		int len = prefix.Lenn();
		var root = Root;
		for (var f = this; f != root; f = f.Parent) {
			if (f == null) { Debug.Assert(IsDeleted); return null; }
			len += f._name.Length + 1;
		}
		var p = stackalloc char[len];
		char* e = p + len;
		for (var f = this; f != root; f = f.Parent) {
			f._name.CopyTo_(e -= f._name.Length);
			*(--e) = '\\';
		}
		if (e > p) prefix.CopyTo_(p);
		return new string(p, 0, len);
	}

	/// <summary>
	/// Gets full path of the file.
	/// If this is a link, it is the link target.
	/// </summary>
	public string FilePath {
		get {
			if (this == Root) return _model.FilesDirectory;
			if (IsDeleted) return null;
			if (IsLink) return LinkTarget;
			return _ItemPath(_model.FilesDirectory);
		}
	}

	/// <summary>
	/// Gets text from file or editor.
	/// Returns "" if file not found.
	/// </summary>
	/// <param name="saved">Always get text from file. If false (default), gets editor text if this is current file.</param>
	/// <param name="warningIfNotFound">Print warning if file not found. If false, prints only other exceptions.</param>
	/// <param name="cache">Cache text. Next time return that text. Not used if gets text from editor.</param>
	public string GetText(bool saved = false, bool warningIfNotFound = false, bool cache = false) {
		if (IsFolder) return "";
		if (!saved && this == _model.CurrentFile) {
			return Panels.Editor.ZActiveDoc.zText;
		}
		//if(cache) print.it("GetText", Name, _text != null);
		if (_text != null) return _text;
		string r = null, es = null, path = FilePath;
		if (path == null) { //IsDeleted
			if (warningIfNotFound) print.warning($"Cannot get text of {Name}. The file is deleted.", -1);
			r = "";
		} else {
			try {
				using var sr = filesystem.waitIfLocked(() => new StreamReader(path));
				if (sr.BaseStream.Length > 100_000_000) es = "File too big, > 100_000_000.";
				else r = sr.ReadToEnd();
			}
			catch (Exception ex) {
				if (warningIfNotFound || !(ex is FileNotFoundException or DirectoryNotFoundException)) es = ex.ToStringWithoutStack();
			}
			r ??= "";
			if (es != null) {
				print.warning($"{es}\r\n\tFailed to get text of <open>{ItemPath}<>, file <explore>{path}<>", -1);
			} else if (cache && Model.IsWatchingFileChanges && !this.IsLink && r.Length < 1_000_000) { //don't cache links because we don't watch their file folders
				_text = r; //FUTURE: set = null after some time if not used
			}
		}
		return r;
	}
	string _text;

	public void UnCacheText(bool fromWatcher = false) {
		//print.it("UnCacheText", Name, _text != null);
		_text = null;
		if (fromWatcher) {
			var doc = Panels.Editor.ZGetOpenDocOf(this);
			if (doc != null) doc.EFileModifiedExternally_();
			else this.Model.EditGoBack.OnTextReplaced(this);
		}
	}

	///// <summary>
	///// Gets or sets 'has triggers' flag.
	///// The setter will save workspace.
	///// </summary>
	//public bool HasTriggers {
	//	get => 0 != (_flags & _Flags.HasTriggers);
	//	set {
	//		if(value != HasTriggers) {
	//			_flags.SetFlag(_Flags.HasTriggers, value);
	//			_model.Save.WorkspaceLater();
	//		}
	//	}
	//}

	/// <summary>
	/// Returns Name.
	/// </summary>
	public override string ToString() => _name;

	#endregion

	#region ITreeViewItem

	IEnumerable<ITreeViewItem> ITreeViewItem.Items => Children();

	/// <summary>
	/// Gets or sets expanded state.
	/// The setter sets to save later but does not update the control (for it use <see cref="KTreeView.Expand"/> instead, it calls the setter).
	/// </summary>
	public bool IsExpanded => _isExpanded;
	bool _isExpanded;

	public void SetIsExpanded(bool yes) { if (yes != _isExpanded) { _isExpanded = yes; _model.Save.StateLater(); } }

	string ITreeViewItem.DisplayText => DisplayName;

	void ITreeViewItem.SetNewText(string text) { FileRename(text); }

	public const string
		c_imageScript = "*Material.ScriptOutline #73BF00",
		c_imageClass = "*Codicons.SymbolClass #008EEE",
		c_imageFolder = "*Material.FolderOutline #EABB00",
		c_imageFolderOpen = "*Material.FolderOpenOutline #EABB00";

	public static string GetFileTypeImageSource(EFileType ft, bool openFolder = false)
		=> ft switch {
			EFileType.Script => c_imageScript,
			EFileType.Class => c_imageClass,
			EFileType.Folder => openFolder ? c_imageFolderOpen : c_imageFolder,
			_ => null
		};

	public string ImageSource => CustomIconName ?? (IsOtherFileType ? FilePath : GetFileTypeImageSource(FileType, _isExpanded));

	//TVCheck ITreeViewItem.CheckState { get; }

	//bool ITreeViewItem.IsDisabled { get; }

	//bool ITreeViewItem.IsBold => this == _model.CurrentFile;
	//bool ITreeViewItem.IsBold => TreeControl.FocusedItem == this;

	//bool ITreeViewItem.IsSelectable { get; }

	int ITreeViewItem.Color => !IsFolder && _model.OpenFiles.Contains(this) && _IsTextBlack ? 0x1d2fafa : -1;

	int ITreeViewItem.TextColor => _model.IsCut(this) ? 0xff : -1;

	int ITreeViewItem.BorderColor => this == _model.CurrentFile ? 0x1FFA0A0 : -1;

	static bool _IsTextBlack => (uint)Api.GetSysColor(Api.COLOR_WINDOWTEXT) == 0; //if not high-contrast theme

	#endregion

	#region tree view

	/// <summary>
	/// Unselects all and selects this. Ensures visible. Does not open document.
	/// If this is root, just unselects all.
	/// </summary>
	public void SelectSingle() {
		if (this == Root) TreeControl.UnselectAll();
		else if (!IsAlien) {
			TreeControl.EnsureVisible(this);
			TreeControl.SelectSingle(this, andFocus: true);
		}
	}

	public bool IsSelected {
		get => TreeControl.IsSelected(this);
		set => TreeControl.Select(this, value);
	}

	/// <summary>
	/// Call this to update/redraw control row view when changed its data (text, image, checked, color, etc).
	/// Redraws only this control; to updtae all, call <see cref="FilesModel.Redraw"/> instead.
	/// </summary>
	public void UpdateControlRow() => TreeControl.Redraw(this);

	#endregion

	#region find

	/// <summary>
	/// Finds descendant file or folder by name or @"\relative path".
	/// Returns null if not found; also if name is null/"".
	/// </summary>
	/// <param name="name">Name like "name.cs" or relative path like @"\name.cs" or @"\subfolder\name.cs".</param>
	/// <param name="kind"></param>
	public FileNode FindDescendant(string name, FNFind kind = FNFind.Any) {
		if (name.NE()) return null;
		if (name[0] == '\\') return _FindRelative(name, kind);
		return _FindIn(Descendants(), name, kind, true);
	}

	static FileNode _FindIn(IEnumerable<FileNode> e, ReadOnlySpan<char> name, FNFind kind, bool preferFile) {
		FileNode folder = null;
		foreach (var f in e) {
			if (!name.Eqi(f._name)) continue;
			if (null == FilesModel.KindFilter_(f, kind)) continue;
			if (preferFile && f.IsFolder) { folder ??= f; continue; }
			return f;
		}
		return folder;
	}

	FileNode _FindRelative(string name, FNFind kind, bool orAnywhere = false) {
#if true //fast, but allocates
		int i = name.LastIndexOf('\\');
		var lastName = name[(i + 1)..]; //never mind: allocation. To avoid allocation would need to enumerate without dictionary, and in big workspace it can be 100 times slower.
		if (_model._nameMap.MultiGet_(lastName, out FileNode v, out var a)) {
			if (a != null) {
				foreach (var f in a) if (_Cmp(f)) return f;
			} else {
				if (_Cmp(v)) return v;
				if (orAnywhere && i < 0) return v;
			}
		}
		return null;

		bool _Cmp(FileNode f) {
			if (null == FilesModel.KindFilter_(f, kind)) return false;
			f = f.Parent;
			for (int j = i; j > 0; f = f.Parent) {
				int k = name.LastIndexOf('\\', j - 1);
				//int k = j; while (--k >= 0 && name[k] != '\\') { }
				if (!name.Eq((k + 1)..j, f.Name ?? "", true)) return false;
				j = k;
			}
			return f == this;
		}
#else //allocation-free, without dictionary
		if (name.Starts(@"\\")) return null;
		var f = this; int lastSegEnd = -1;
		foreach (var v in name.Segments(@"\", SegFlags.NoEmpty)) {
			var s = name.AsSpan(v.start, v.Length);
			bool last = (lastSegEnd = v.end) == name.Length;
			for (f = f.FirstChild; f != null; f = f.Next) {
				if (last) {
					if (null != FilesModel.KindFilter_(f, kind) && s.Eqi(f._name)) break;
					//if (s.Eqi(f._name) && null != FilesModel.KindFilter_(f, kind)) break;
				} else {
					if (f.IsFolder && s.Eqi(f._name)) break;
				}
			}
			if (f == null) return null;
		}
		if (lastSegEnd != name.Length) return null; //prevents finding when name is "" or @"\" or @"xxx\".
		return f;
#endif
	}

	/// <summary>
	/// Finds file or folder by name or path relative to: this folder, parent folder (if this is file) or root (if relativePath starts with @"\").
	/// Returns null if not found; also if name is null/"".
	/// </summary>
	/// <param name="relativePath">Examples: "name.cs", @"subfolder\name.cs", @".\subfolder\name.cs", @"..\parent\name.cs", @"\root path\name.cs".</param>
	/// <param name="kind"></param>
	/// <param name="orAnywhere">If <i>relativePath</i> is filename and does not exist in this folder, if single such file exists anywhere, return that file.</param>
	public FileNode FindRelative(string relativePath, FNFind kind = FNFind.Any, bool orAnywhere = false) {
		if (!IsFolder) return Parent.FindRelative(relativePath, kind, orAnywhere);
		var s = relativePath;
		if (s.NE()) return null;
		FileNode p = this;
		if (s[0] == '\\') p = Root;
		else if (s[0] == '.') {
			int i = 0;
			for (; s.Eq(i, @"..\"); i += 3) { p = p.Parent; if (p == null) return null; }
			if (i == 0 && s.Starts(@".\")) i = 2;
			if (i != 0) {
				if (i == s.Length) return (p == Root || !(kind is FNFind.Any or FNFind.Folder)) ? null : p;
				s = s[i..];
			}
		}
		return p._FindRelative(s, kind, orAnywhere);
	}

	/// <summary>
	/// Finds all descendant files (and not folders) that have the specified name.
	/// Returns empty array if not found.
	/// </summary>
	/// <param name="name">File name. If starts with backslash, works like <see cref="FindDescendant"/>.</param>
	public FileNode[] FindAllDescendantFiles(string name) {
		if (!name.NE()) {
			if (name[0] == '\\') {
				var f1 = _FindRelative(name, FNFind.File);
				if (f1 != null) return new FileNode[] { f1 };
			} else {
				return Descendants().Where(k => !k.IsFolder && k._name.Eqi(name)).ToArray();
			}
		}
		return Array.Empty<FileNode>();
	}

	/// <summary>
	/// Finds ancestor (including self) project folder and its main file.
	/// If both found, sets folder and main and returns true. If some not found, sets folder=null, main=null, and returns false.
	/// If ofAnyScript, gets project even if this is a non-main script in project folder. 
	/// </summary>
	public bool FindProject(out FileNode folder, out FileNode main, bool ofAnyScript = false) {
		folder = main = null;
		for (FileNode r = Root, f = IsFolder ? this : Parent; f != r && f != null; f = f.Parent) {
			if (!f.IsProjectFolder(out main)) continue;
			if (this.IsScript && this != main && !ofAnyScript) { //non-main scripts are not part of project
				main = null;
				break;
			}
			folder = f;
			return true;
		}
		return false;
	}

	//CONSIDER: multiscript project. Each script in it is compiled together with cs files.

	/// <summary>
	/// Returns true if this is a folder and Name starts with '@' and contains main code file.
	/// </summary>
	/// <param name="main">Receives the main code file. It is the first direct child code file.</param>
	public bool IsProjectFolder(out FileNode main) {
		main = null;
		if (IsFolder && _name[0] == '@') {
			foreach (var f in Children()) {
				if (f.IsCodeFile) { main = f; return true; }
			}
		}
		return false;
	}

	public IEnumerable<FileNode> EnumProjectClassFiles(FileNode fSkip = null) {
		foreach (var f in Descendants()) {
			if (f._type == EFileType.Class && f != fSkip) yield return f;
		}
	}

	/// <summary>
	/// Gets class file role from metacomments.
	/// Note: can be slow, because loads file text if this is a class file.
	/// </summary>
	public EClassFileRole GetClassFileRole() {
		if (_type != EFileType.Class) return EClassFileRole.None;
		var code = GetText();
		var meta = MetaComments.FindMetaComments(code);
		if (meta.end == 0) return EClassFileRole.Class;
		foreach (var v in MetaComments.EnumOptions(code, meta)) {
			if (!v.NameIs("role")) continue;
			if (v.ValueIs("classLibrary")) return EClassFileRole.Library;
			if (v.ValueIs("classFile")) break;
			return EClassFileRole.App;
		}
		return EClassFileRole.Class;
	}

	public enum EClassFileRole
	{
		/// <summary>Not a class file.</summary>
		None,
		/// <summary>Has meta role miniProgram/exeProgram/editorExtension.</summary>
		App,
		/// <summary>Has meta role classLibrary.</summary>
		Library,
		/// <summary>Has meta role classFile, or no meta role.</summary>
		Class,
	}

	#endregion

	#region new item

	public static string CreateNameUniqueInFolder(FileNode folder, string fromName, bool forFolder, bool autoGenerated = false, bool moving = false, FileNode renaming = null) {
		if (!forFolder && !moving && fromName.Eqi("global.cs")) {
			if (null == folder.Model.Find("global.cs", FNFind.Class)) return fromName;
		} else {
			if (!_Exists(fromName)) return fromName;
		}

		string oldName = fromName, ext = null;
		if (!forFolder) {
			int i = fromName.LastIndexOf('.');
			if (i >= 0) { ext = fromName[i..]; fromName = fromName[..i]; }
		}
		fromName = fromName.RxReplace(@"\d+$", "");
		for (int i = 2; ; i++) {
			var s = fromName + i + ext;
			if (!_Exists(s)) {
				if (!autoGenerated) print.it($"Info: name \"{oldName}\" has been changed to \"{s}\", to make it unique in the folder.");
				return s;
			}
		}

		bool _Exists(string s) {
			var f = _FindIn(folder.Children(), s, FNFind.Any, false);
			if (f != null) return f != renaming;
			if (filesystem.exists(folder.FilePath + "\\" + s)) return true; //orphaned file?
			return false;
		}
	}

	public static class Templates
	{
		public static readonly string DefaultDirBS = folders.ThisAppBS + @"Templates\files\";
		public static readonly string UserDirBS = AppSettings.DirBS + @"Templates\";

		public static string FileName(ETempl templ) => templ switch { ETempl.Class => "Class.cs", _ => "Script.cs" };

		public static string FilePathRaw(ETempl templ, bool user) => (user ? UserDirBS : DefaultDirBS) + FileName(templ);

		public static string FilePathReal(ETempl templ, bool? user = null) {
			bool u = user ?? ((ETempl)App.Settings.templ_use).Has(templ);
			var file = FilePathRaw(templ, u);
			if (u && !filesystem.exists(file, true)) file = FilePathRaw(templ, false);
			return file;
		}

		public static string Load(ETempl templ, bool? user = null) {
			return filesystem.loadText(FilePathReal(templ, user));
		}

		public static bool IsStandardTemplateName(string template, out ETempl result, bool ends = false) {
			int i = ends ? template.Ends(false, s_names) : template.Eq(false, s_names);
			if (i-- == 0) { result = 0; return false; }
			result = (ETempl)(1 << i);
			return true;
		}

		readonly static string[] s_names = { "Script.cs", "Class.cs" };

		/// <summary>
		/// Loads Templates\files.xml and optionally finds a template in it.
		/// Returns null if template not found. Exception if fails to load file.
		/// Uses caching to avoid loading file each time, but reloads if file modified; don't modify the XML DOM.
		/// </summary>
		/// <param name="template">null or relative path of template in Templates\files. Case-insensitive.</param>
		public static XElement LoadXml(string template = null) {
			//load files.xml first time, or reload if file modified
			filesystem.getProperties(s_xmlFilePath, out var fp, FAFlags.UseRawPath);
			if (s_xml == null || fp.LastWriteTimeUtc != s_xmlFileTime) {
				s_xml = XmlUtil.LoadElem(s_xmlFilePath);
				s_xmlFileTime = fp.LastWriteTimeUtc;
			}

			var x = s_xml;
			if (template != null) {
				var a = template.Split('\\');
				for (int i = 0; i < a.Length; i++) x = x?.Elem(i < a.Length - 1 ? "d" : null, "n", a[i], ignoreCase: true);
				Debug.Assert(x != null);
			}
			return x;
		}
		static XElement s_xml;
		static readonly string s_xmlFilePath = folders.ThisAppBS + @"Templates\files.xml";
		static DateTime s_xmlFileTime;

		public static bool IsInDefault(XElement x) => x.Ancestors().Any(o => o.Attr("n") == "Default");
	}

	[Flags]
	public enum ETempl { Script = 1, Class = 2 }

	#endregion

	#region rename, move, copy

	/// <summary>
	/// Changes Name of this object and renames its file (if not link).
	/// Returns false if name is empty or fails to rename its file.
	/// </summary>
	/// <param name="name">
	/// Name, like "New name.cs" or "New name".
	/// If not folder, adds previous extension if no extension or changed code file extension.
	/// If invalid filename, replaces invalid characters etc.
	/// </param>
	public bool FileRename(string name) {
		name = pathname.correctName(name);
		if (!IsFolder) {
			var ext = pathname.getExtension(_name);
			if (ext.Length > 0) if (name.IndexOf('.') < 0 || (IsCodeFile && !name.Ends(ext, true))) name += ext;
		}
		if (name == _name) return true;
		name = CreateNameUniqueInFolder(Parent, name, IsFolder, renaming: this);

		if (!IsLink) {
			if (!_model.TryFileOperation(() => filesystem.rename(this.FilePath, name, FIfExists.Fail))) return false;
		}

		_SetName(name);
		_model.Save.WorkspaceLater();
		FilesModel.Redraw(this, remeasure: true);
		CodeInfo.FilesChanged();
		return true;
	}

	/// <summary>
	/// Returns true if can move the tree node into the specified position.
	/// For example, cannot move parent into child etc.
	/// Does not check whether can move the file.
	/// </summary>
	public bool CanMove(FileNode target, FNPosition pos) {
		//cannot move into self or descendants
		if (target == this || target.IsDescendantOf(this)) return false;

		//cannot move into a non-folder or before/after self
		switch (pos) {
		case FNPosition.Inside:
			if (!target.IsFolder) return false;
			break;
		case FNPosition.Before:
			if (Next == target) return false;
			break;
		case FNPosition.After:
			if (Previous == target) return false;
			break;
		}
		return true;
	}

	/// <summary>
	/// Moves this into, before or after target.
	/// Also moves file if need.
	/// </summary>
	/// <param name="target"></param>
	/// <param name="pos"></param>
	public bool FileMove(FileNode target, FNPosition pos) {
		if (target == null) { target = Root; pos = FNPosition.Inside; }
		if (!CanMove(target, pos)) return false;

		var newParent = (pos == FNPosition.Inside) ? target : target.Parent;
		if (newParent != Parent) {
			var name = CreateNameUniqueInFolder(newParent, _name, IsFolder, moving: true);

			if (!IsLink) {
				if (!_model.TryFileOperation(() => filesystem.move(this.FilePath, newParent.FilePath + "\\" + name, FIfExists.Fail))) return false;
			}

			if (name != _name) _SetName(name);
		}

		//move tree node
		Remove();
		Common_MoveCopyNew(target, pos);

		return true;
	}

	public void Common_MoveCopyNew(FileNode target, FNPosition pos) {
		target.AddChildOrSibling(this, pos, true);
		CodeInfo.FilesChanged();
	}

	/// <summary>
	/// Adds f to the tree, updates control, optionally sets to save workspace.
	/// </summary>
	public void AddChildOrSibling(FileNode f, FNPosition inBeforeAfter, bool setSaveWorkspace) {
		if (inBeforeAfter == FNPosition.Inside) AddChild(f); else AddSibling(f, inBeforeAfter == FNPosition.After);
		_model.UpdateControlItems();
		if (setSaveWorkspace) _model.Save.WorkspaceLater();
	}

	/// <summary>
	/// Copies this into, before or after target.
	/// Also copies file if need.
	/// Returns the copy, or null if fails.
	/// </summary>
	/// <param name="target"></param>
	/// <param name="pos"></param>
	/// <param name="newModel">Used when importing workspace.</param>
	internal FileNode FileCopy(FileNode target, FNPosition pos, FilesModel newModel = null) {
		_model.Save?.TextNowIfNeed(true);
		if (target == null) { target = Root; pos = FNPosition.Inside; }

		//create unique name
		var newParent = (pos == FNPosition.Inside) ? target : target.Parent;
		string name = CreateNameUniqueInFolder(newParent, _name, IsFolder);

		//copy file or directory
		if (!IsLink) {
			if (!_model.TryFileOperation(() => filesystem.copy(FilePath, newParent.FilePath + "\\" + name, FIfExists.Fail))) return null;
		}

		//create new FileNode with descendants
		var model = newModel ?? _model;
		var f = new FileNode(model, this, name);
		_CopyChildren(this, f);

		void _CopyChildren(FileNode from, FileNode to) {
			if (!from.IsFolder) return;
			foreach (var v in from.Children()) {
				var t = new FileNode(model, v, v._name);
				to.AddChild(t);
				_CopyChildren(v, t);
			}
		}

		//insert at the specified place and set to save
		f.Common_MoveCopyNew(target, pos);
		return f;
	}

	#endregion

	#region util

	//public bool ContainsName(string name, bool printInfo = false) {
	//	if (null == _FindIn(Children(), name, null, false)) return false;
	//	if (printInfo) print.it(name + " exists in this folder.");
	//	return true;
	//}

	/// <summary>
	/// Gets file type from XML tag which should be "d", "s", "c" or "n".
	/// If none, throws ArgumentException if canThrow, else returns EFileType.Other.
	/// </summary>
	public static EFileType XmlTagToFileType(string tag, bool canThrow) => tag switch {
		"d" => EFileType.Folder,
		"s" => EFileType.Script,
		"c" => EFileType.Class,
		"n" => EFileType.Other,
		_ => !canThrow ? EFileType.Other : throw new ArgumentException("XML element name must be 'd', 's', 'c' or 'n'")
	};

	/// <summary>
	/// Detects file type from extension.
	/// If .cs, returns Class, else Other.
	/// Must be not folder.
	/// </summary>
	static EFileType _DetectFileType(string path) {
		var type = EFileType.Other;
		if (path.Ends(".cs", true)) {
			type = EFileType.Class;
			try {
				var code = filesystem.loadText(path);
				if(CiUtil.IsScript(code)) type = EFileType.Script;
			}
			catch (Exception ex) { Debug_.Print(ex); }

			//FUTURE: later allow to change file type script from/to class. Eg in Properties.
		}
		return type;
	}

	#endregion
}

/// <summary>
/// File type of a <see cref="FileNode"/>.
/// Saved in XML as tag name: d folder, s script, c class, n other.
/// </summary>
enum EFileType : byte
{
	Folder, //must be 0
	Script,
	Class,
	Other,
}
