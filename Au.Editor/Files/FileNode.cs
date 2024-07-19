using Au.Controls;
using Au.Compiler;
using System.Xml;
using System.Xml.Linq;

partial class FileNode : TreeBase<FileNode>, ITreeViewItem {
	#region types
	
	//Not saved in file.
	[Flags]
	enum _State : byte {
		Deleted = 1,
	}
	
	//Saved in file.
	[Flags]
	enum _Flags : byte {
		_obsolete_Symlink = 1,
	}
	
	#endregion
	
	#region fields, ctors, load/save
	
	FilesModel _model;
	string _name;
	string _displayName;
	uint _id;
	FNType _type;
	_State _state;
	_Flags _flags;
	string _linkTarget;
	string _icon;
	uint _testScriptId;
	
	//this ctor is used when creating new item of known type
	public FileNode(FilesModel model, string name, FNType type) {
		_model = model;
		_type = type;
		_SetName(name);
		_id = _model.AddGetId(this);
	}
	
	//this ctor is used when importing items from files etc.
	//name is filename with extension.
	//filePath is used: 1. To detect type (when !isDir). 2. If isLink, to set link target.
	public FileNode(FilesModel model, string name, string filePath, bool isDir, bool isLink = false) {
		_model = model;
		_type = isDir ? FNType.Folder : _DetectFileType(filePath);
		_SetName(name);
		_id = _model.AddGetId(this);
		if (isLink) _linkTarget = filePath;
	}
	
	//this ctor is used when copying an item or importing a workspace.
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
	
	//this ctor is used when loading workspace (reading files.xml)
	FileNode(XmlReader x, FileNode parent, FilesModel model) {
		_model = model;
		if (parent == null) { //the root node
			if (x.Name != "files") throw new ArgumentException("XML root element name must be 'files'");
		} else {
			_type = XmlTagToFileType(x.Name, canThrow: true);
			while (x.MoveToNextAttribute()) {
				var v = x.Value; if (v.NE()) continue;
				switch (x.Name) {
				case "n": _SetName(v); break;
				case "i": v.ToInt(out _id); break;
				case "f": _flags = (_Flags)v.ToInt(); break;
				case "path": _linkTarget = v.Starts(@".\") ? _model.WorkspaceDirectory + v[1..] : v; break;
				case "icon": _icon = v; break;
				case "run": v.ToInt(out _testScriptId); break;
				}
			}
			if (_name == null) throw new ArgumentException("no 'n' attribute in XML");
		}
	}
	
	internal void _WorkspaceLoaded(uint id, bool importing) {
		_id = id; //if id in XML was invalid, the caller generated new id
		
		if (_flags.Has(_Flags._obsolete_Symlink)) { //before v0.17 for folder links used symlinks. Bad idea.
			_flags &= ~_Flags._obsolete_Symlink;
			var linkPath = FilePath;
			filesystem.more.getFinalPath(linkPath, out _linkTarget);
			if (!importing) {
				Api.RemoveDirectory(linkPath);
				_model.Save.WorkspaceLater(1);
			}
		};
	}
	
	public static FileNode LoadWorkspace(string file, FilesModel model) {
		var root = XmlLoad(file, (x, p) => new FileNode(x, p, model));
		root._isExpanded = true;
		return root;
	}
	
	public void SaveWorkspace(string file) => XmlSave(file, (x, n) => n._XmlWrite(x, false));
	
	void _XmlWrite(XmlWriter x, bool exporting) {
		if (Parent == null) {
			x.WriteStartElement("files");
		} else {
			x.WriteStartElement(_type switch { FNType.Folder => "d", FNType.Script => "s", FNType.Class => "c", _ => "n" });
			x.WriteAttributeString("n", _name);
			if (!exporting) x.WriteAttributeString("i", _id.ToString());
			if (_flags != 0) x.WriteAttributeString("f", ((int)_flags).ToString());
			if (IsLink) x.WriteAttributeString("path", _linkTarget.PathStarts(_model.WorkspaceDirectory) ? @"." + _linkTarget[_model.WorkspaceDirectory.Length..] : _linkTarget);
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
	/// Setter can change Script to Class or vice versa; call when closing DProperties.
	/// </summary>
	public FNType FileType {
		get => _type;
		set {
			Debug.Assert(IsCodeFile);
			if (value == _type) return;
			_type = value;
			_testScriptId = 0;
			
			_model.Save.WorkspaceLater();
			Compiler.Uncache(this);
			CodeInfo.FilesChanged();
			FilesModel.Redraw(this);
		}
	}
	
	/// <summary>
	/// true if folder or root.
	/// </summary>
	public bool IsFolder => _type == FNType.Folder;
	
	/// <summary>
	/// true if script file.
	/// </summary>
	public bool IsScript => _type == FNType.Script;
	
	/// <summary>
	/// true if class file.
	/// </summary>
	public bool IsClass => _type == FNType.Class;
	
	/// <summary>
	/// true if script or class file.
	/// false if folder or not a code file.
	/// </summary>
	public bool IsCodeFile => _type is FNType.Script or FNType.Class;
	
	/// <summary>
	/// true if not script/class/folder.
	/// </summary>
	public bool IsOtherFileType => _type == FNType.Other;
	
	/// <summary>
	/// File name with extension.
	/// </summary>
	public string Name => _name;
	
	/// <summary>
	/// File name. Without extension if ends with ".cs".
	/// </summary>
	public string DisplayName => _displayName ??= IsCodeFile ? _name.RemoveSuffix(".cs", true) : _name;
	
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
	/// Formats string like ":0x10000000A", with <see cref="Id"/> in low-order uint and <see cref="FilesModel.WorkspaceSN"/> in high-order int.
	/// Such string can be passed to <see cref="FilesModel.Find"/>.
	/// </summary>
	public string IdStringWithWorkspace => ":0x" + (_id | ((long)_model.WorkspaceSN << 32)).ToString("X");
	
	/// <summary>
	/// Formats SciTags &lt;open&gt; link tag to open this file.
	/// </summary>
	/// <param name="path">In link name use <see cref="ItemPath"/> instead of name.</param>
	/// <param name="line">If not null, appends |line. It is 1-based.</param>
	public string SciLink(bool path = false, int? line = null)
		=> line == null
		? $"<open {IdStringWithWorkspace}>{(path ? ItemPath : _name)}<>"
		: $"<open {IdStringWithWorkspace}|{line.Value}>{(path ? ItemPath : _name)}<>";
	
	/// <summary>
	/// true if is a link to an external file or folder.
	/// See also IsExternal.
	/// </summary>
	public bool IsLink => _linkTarget != null;
	
	/// <summary>
	/// If <see cref="IsLink"/>, returns target path, else null.
	/// </summary>
	public string LinkTarget => _linkTarget;
	
	/// <summary>
	/// true if this or an ancestor is <see cref="IsLink"/>.
	/// </summary>
	public bool IsExternal {
		get {
			for (var v = this; v != null; v = v.Parent) if (v.IsLink) return true;
			return false;
		}
	}
	
	/// <summary>
	/// Gets the filename extension of the target file, like ".cs". Returns "" if folder.
	/// </summary>
	public string FileExt => IsFolder ? "" : pathname.getExtension(IsLink ? LinkTarget : Name);
	
	/// <summary>
	/// Gets or sets custom icon name (like "*Pack.Icon color") or null.
	/// </summary>
	/// <remarks>
	/// The setter will save workspace.
	/// User can set custom icon: menu Tools > Icons.
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
	/// Gets custom or default icon name or file path.
	/// </summary>
	public string IconString => CustomIconName ?? (IsOtherFileType ? FilePath : GetFileTypeImageSource(FileType));
	
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
	
	//rejected: cache item path and file path.
	//	This func compares the cached item path with ancestor names. No garbage when cached, but just 30-40% faster.
	//	Another way: when an item renamed or moved, reset cached paths of that item and of its descendants.
	//	But creating paths is fast and not so much garbage. And not so frequently used. Keeping paths in memory isn't good too.
	//public string ItemPath2() {
	//	if (_itemPath is string s) {
	//		var root = Root;
	//		int end = s.Length;
	//		for (var f = this; f != root; f = f.Parent) {
	//			if (f == null) { Debug.Assert(IsDeleted); return null; }
	//			string name = f._name;
	//			int len = name.Length, start = end - len;
	//			//print.it(start, end);
	//			if (start <= 0 || s[start - 1] != '\\' || !s.Eq(start, name)) break; //the slowest part. Tested: with for loop slower.
	//			end = start - 1;
	//			//print.it(end);
	//		}
	//		if (end == 0) return s;
	//		print.it(end);
	//	}
	//	return _itemPath = _ItemPath();
	//}
	//string _itemPath;
	
	/// <summary>
	/// Returns item path relative to <i>ancestor</i>, like @"Folder\Name.cs" or @"Name.cs".
	/// Returns null if this item is deleted or not in <i>ancestor</i>.
	/// If <i>ancestor</i> is null or <b>Root</b>, the result is the same as <b>ItemPath</b>.
	/// </summary>
	public string ItemPathIn(FileNode ancestor) => _ItemPath(null, ancestor, noBS: true);
	
	[SkipLocalsInit]
	unsafe string _ItemPath(string prefix = null, FileNode ancestor = null, bool noBS = false) {
		int len = prefix.Lenn();
		var root = ancestor ?? Root;
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
		if (noBS && ancestor?.Parent != null) return new string(p, 1, len - 1);
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
			FileNode link = Parent; while (link != null && !link.IsLink) link = link.Parent;
			if (link != null) return _ItemPath(link.LinkTarget, link);
			
			return _ItemPath(_model.FilesDirectory);
		}
	}
	
	/// <summary>
	/// If is open (active or not), returns the <b>SciCode</b>, else null.
	/// </summary>
	public SciCode OpenDoc { get; internal set; }
	
	/// <summary>
	/// Returns Name.
	/// </summary>
	public override string ToString() => _name;
	
	///// <summary>
	///// Data used by code info classes.
	///// </summary>
	//public CiFileData ci;
	
	#endregion
	
	#region text
	
	/// <summary>
	/// Gets text from editor (if this is the active open document) or file (<see cref="GetFileText"/>).
	/// </summary>
	/// <param name="text">"" if failed (eg file not found) or is folder.</param>
	/// <param name="silent">Don't print warning when failed. If null, silent only if file not found.</param>
	/// <returns>(true, null) if got text or is folder. (false, error) if failed.</returns>
	public BoolError GetCurrentText(out string text, bool? silent = false) {
		if (this == _model.CurrentFile) { text = Panels.Editor.ActiveDoc.aaaText; return true; }
		return GetFileText(out text, silent);
	}
	
	/// <summary>
	/// Gets text from file.
	/// Does not get editor text like <see cref="GetCurrentText"/>.
	/// </summary>
	/// <param name="text">"" if failed (eg file not found) or is folder.</param>
	/// <param name="silent">Don't print warning when failed. If null, silent only if file not found.</param>
	/// <returns>(true, null) if got text or is folder. (false, error) if failed.</returns>
	public BoolError GetFileText(out string text, bool? silent = false) {
		text = "";
		if (IsFolder) return true;
		
		//rejected: cache text. OS caching isn't too slow.
		
		string path = FilePath, es = null;
		if (path == null) { //IsDeleted
			es = "Deleted.";
			path = "unknown";
		} else {
			try {
				using var sr = filesystem.waitIfLocked(() => new StreamReader(path));
				
				if (sr.BaseStream.Length > 100_000_000) es = "File too big, > 100_000_000.";
				else text = sr.ReadToEnd();
				
				//rejected: update _fileModTime. Would need to call OnAppActivatedAndThisIsOpen if changed.
				//var fs = (FileStream)sr.BaseStream;
				//if (fs.Length > 100_000_000) es = "File too big, > 100_000_000.";
				//else {
				//	text = sr.ReadToEnd();
				//	if (!silent) _fileModTime = Api.GetFileInformationByHandle(fs.SafeFileHandle.DangerousGetHandle(), out var fi) ? fi.ftLastWriteTime : 0;
				//}
			}
			catch (Exception ex) {
				bool notFound = ex is FileNotFoundException or DirectoryNotFoundException;
				Debug_.PrintIf(!notFound && silent == true, ex);
				if (notFound) silent ??= true;
				es = ex.ToStringWithoutStack();
			}
		}
		
		if (es == null) return true;
		es = $"Failed to get text of <open>{ItemPath}<>, file <explore>{path}<>\r\n\t{es}";
		if (silent != true) print.warning(es, -1);
		return new(false, es);
	}
	
	/// <summary>
	/// Loads text of any file like <see cref="GetFileText"/>.
	/// Thread-safe. Silent when file not found or too big.
	/// </summary>
	/// <param name="filePath">Full path, to pass directly to .NET <b>File</b> function.</param>
	/// <returns>null if failed or file size more than 100_000_000.</returns>
	public static string GetFileTextLL(string filePath) {
		//TODO3: option to load only text files. If unknown extension, try to load part and find \0.
		try {
			using var sr = filesystem.waitIfLocked(() => new StreamReader(filePath));
			if (sr.BaseStream.Length > 100_000_000) return null;
			return sr.ReadToEnd();
		}
		catch (Exception ex) {
			if (ex is not (FileNotFoundException or DirectoryNotFoundException)) print.warning(ex.ToStringWithoutStack(), -1);
		}
		return null;
	}
	
	long _fileModTime;
	
	//called when SciDoc loaded or saved the file
	internal void _UpdateFileModTime() {
		_fileModTime = Api.GetFileAttributesEx(FilePath, 0, out var d) ? d.ftLastWriteTime : 0;
	}
	
	internal void _CheckModifiedExternally(SciCode doc) {
		if (doc.EIsBinary) return;
		Debug_.PrintIf(_fileModTime == 0);
		if (!Api.GetFileAttributesEx(FilePath, 0, out var d) || d.ftLastWriteTime == _fileModTime) return;
		_fileModTime = d.ftLastWriteTime;
		doc.EFileModifiedExternally_(); //calls GetFileText
	}
	
	/// <summary>
	/// Replaces all specified text ranges with specified texts.
	/// If the file is open in editor, replaces it there; saves if it isn't the active document. Else replaces file text.
	/// </summary>
	/// <param name="text">Current text.</param>
	/// <param name="a">Text ranges and replacement texts. Must be sorted by range. Ranges must not overlap.</param>
	/// <returns>false if a is empty or failed to save.</returns>
	/// <param name="newText"></param>
	/// <exception cref="ArgumentException">Ranges are overlapped or not sorted. Only #if DEBUG.</exception>
	public bool ReplaceAllInText(string text, List<StartEndText> a, out string newText) {
		newText = null;
		if (a.Count > 0) {
			var doc = OpenDoc;
			if (doc != null) {
				Debug.Assert(!doc.aaaIsReadonly);
				StartEndText.ThrowIfNotSorted(a);
				
				using (doc.aaaNewUndoAction()) {
					for (int i = a.Count; --i >= 0;) {
						var (from, to, s) = a[i];
						doc.aaaNormalizeRange(true, ref from, ref to);
						if (CiStyling.IsProtected(doc, from, to)) continue; //hidden text (embedded image)
						doc.aaaReplaceRange(false, from, to, s);
					}
				}
				
				newText = doc.aaaText;
				if (doc != Panels.Editor.ActiveDoc) doc.ESaveText_(true);
				return true;
			} else {
				newText = StartEndText.ReplaceAll(text, a);
				try {
					SaveNewTextOfClosedFile(newText);
					return true;
				}
				catch (Exception e1) { print.warning($"Failed to save {SciLink()}. {e1.ToStringWithoutStack()}"); }
			}
		}
		return false;
	}
	
	/// <exception cref="Exception"></exception>
	public void SaveNewTextOfClosedFile(string text) {
		Debug.Assert(OpenDoc == null);
		if (DontSave) throw new AuException("This file should not be modified.");
		filesystem.saveText(FilePath, text);
		_UpdateFileModTime();
		CodeInfo.FilesChanged();
	}
	
	/// <summary>
	/// true if is link to a file in folders.ThisAppBS + "Default" or "Templates".
	/// #if DEBUG, always returns false.
	/// </summary>
#if DEBUG
	public bool DontSave => false;
#else
	public bool DontSave => IsLink && FilePath is var s && s.Starts(folders.ThisAppBS) && 0 != s.Eq(folders.ThisAppBS.Length, true, @"Default\", @"Templates\");
#endif
	
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
	
	public static string GetFileTypeImageSource(FNType ft, bool openFolder = false)
		=> ft switch {
			FNType.Script => App.Settings.icons.ft_script ?? EdResources.c_iconScript,
			FNType.Class => App.Settings.icons.ft_class ?? EdResources.c_iconClass,
			FNType.Folder => openFolder
				? App.Settings.icons.ft_folderOpen ?? EdResources.c_iconFolderOpen
				: App.Settings.icons.ft_folder ?? EdResources.c_iconFolder,
			_ => null
		};
	
	public object Image {
		get {
			var s = CustomIconName ?? (IsOtherFileType ? FilePath : GetFileTypeImageSource(FileType, _isExpanded));
			if (IsLink) return new object[] { s, "link:" };
			return s;
		}
	}
	
	int ITreeViewItem.Color(TVColorInfo ci) => ci.isTextBlack && !IsFolder && _model.OpenFiles.Contains(this) ? 0x1fafad2 : -1;
	
	int ITreeViewItem.TextColor(TVColorInfo ci) => _model.IsCut(this) ? 0xff0000 : -1;
	
	int ITreeViewItem.BorderColor(TVColorInfo ci) => this == _model.CurrentFile ? (ci.isTextBlack ? 0x1A0A0FF : 0xFF8000) : -1;
	
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
	/// Redraws only this control; to update all, call <see cref="FilesModel.Redraw"/> instead.
	/// </summary>
	public void UpdateControlRow() => TreeControl.Redraw(this);
	
	#endregion
	
	#region find, enum
	
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
	
	static FileNode _FindIn(IEnumerable<FileNode> e, RStr name, FNFind kind, bool preferFile) {
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
		bool retry = false; gRetry:
#if true //fast, but allocates
		int i = name.LastIndexOf('\\');
		var lastName = i < 0 ? name : name[(i + 1)..]; //never mind: allocation. To avoid allocation would need to enumerate without dictionary, and in big workspace it can be 100 times slower.
		if (_model._nameMap.MultiGet_(lastName, out FileNode v, out var a)) {
			if (a != null) {
				foreach (var f in a) if (_Cmp(f)) return f;
			} else {
				if (_Cmp(v)) return v;
				if (orAnywhere && i < 0) return v;
			}
		}
		if (!retry) {
			retry = kind is FNFind.CodeFile or FNFind.Class ? !lastName.Ends(".cs", true) : kind is FNFind.Folder ? false : !lastName.Contains('.');
			if (retry) { name += ".cs"; goto gRetry; }
		}
		return null;
		
		bool _Cmp(FileNode f) {
			if (null == FilesModel.KindFilter_(f, kind)) return false;
			f = f.Parent;
			for (int j = i; j > 0 && f != null; f = f.Parent) {
				int k = name.LastIndexOf('\\', j - 1);
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
			orAnywhere = false;
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
	/// Gets all descendant nodes, except descendants of folders named "Garbage".
	/// </summary>
	public IEnumerable<FileNode> DescendantsExceptGarbage() {
		int level = 0;
		foreach (var f in Descendants()) {
			if (level == 0) {
				if (f.IsFolder) if (f.Name.Eqi("Garbage")) level = f.Level;
			} else {
				if (f.Level > level) continue;
				level = 0;
			}
			yield return f;
		}
	}
	
	/// <summary>
	/// Finds ancestor (including self) project folder and its main file.
	/// </summary>
	/// <returns>true if this is in a project folder and found its main file.</returns>
	/// <param name="folder">If the function returns true, receives the project folder, else null.</param>
	/// <param name="main">If the function returns true, receives the project-main file, else null.</param>
	/// <param name="ofAnyScript">Get project even if this is a non-main script in project folder.</param>
	public bool FindProject(out FileNode folder, out FileNode main, bool ofAnyScript = false) {
		for (FileNode r = Root, f = IsFolder ? this : Parent; f != r && f != null; f = f.Parent) {
			if (!f.IsProjectFolder(out var fm)) continue;
			if (this.IsScript && this != fm && !ofAnyScript) break; //non-main scripts are not part of project
			main = fm;
			folder = f;
			return true;
		}
		folder = main = null;
		return false;
	}
	//CONSIDER: multiscript project. Each script in it is compiled together with cs files.
	
	/// <summary>
	/// If this is in a project, returns the project-main file. Else returns this.
	/// This must be a code file (asserts).
	/// </summary>
	/// <param name="ofAnyScript">Get project even if this is a non-main script in project folder.</param>
	public FileNode GetProjectMainOrThis(bool ofAnyScript = false) {
		Debug.Assert(IsCodeFile);
		return FindProject(out _, out var pm, ofAnyScript) ? pm : this;
	}
	
	//public FileNode GetProjectMainOrThis
	
	/// <summary>
	/// Returns true if this is a folder and Name starts with '@' and contains main code file.
	/// </summary>
	/// <param name="main">Receives the main code file. It is the first direct child code file.</param>
	public bool IsProjectFolder(out FileNode main) {
		main = null;
		if (IsFolder && _name[0] == '@') {
			for (var f = FirstChild; f != null; f = f.Next) {
				if (f.IsCodeFile) { main = f; return true; }
			}
		}
		return false;
	}
	
	public IEnumerable<FileNode> EnumProjectClassFiles(FileNode fSkip = null) {
		foreach (var f in Descendants()) {
			if (f._type == FNType.Class && f != fSkip) yield return f;
		}
	}
	
	/// <summary>
	/// Gets class file role from metacomments.
	/// Note: can be slow, because loads file text if this is a class file.
	/// </summary>
	public FNClassFileRole GetClassFileRole() {
		if (_type != FNType.Class) return FNClassFileRole.None;
		var r = FNClassFileRole.Class;
		if (GetCurrentText(out var code, silent: null)) {
			var meta = MetaComments.FindMetaComments(code);
			if (meta.end > 0) {
				int i = -1;
				bool findDefineTest = false;
				foreach (var v in MetaComments.EnumOptions(code, meta)) {
					i++;
					if (findDefineTest) {
						if (v.NameIs("define")) return FNClassFileRole.Class;
					} else {
						if (!v.NameIs("role")) continue;
						if (v.ValueIs("classLibrary")) return FNClassFileRole.Library;
						if (v.ValueIs("classFile")) break;
						r = FNClassFileRole.App;
						if (findDefineTest = i == 0) continue; //maybe using feature "test-run without a test script", eg `/*/ role miniProgram; define TEST; /*/ ... #if TEST ...`
						break;
					}
				}
			}
		}
		return r;
	}
	
	#endregion
	
	#region new item
	
	public static string CreateNameUniqueInFolder(FileNode folder, string fromName, bool forFolder, bool autoGenerated = false, bool moving = false, FileNode renaming = null) {
		bool isGlobal = !forFolder && !moving && fromName.Eqi("global.cs");
		if (isGlobal) {
			if (null == folder.Model.Find("global.cs", FNFind.Class, silent: true)) return fromName;
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
				if (!autoGenerated) print.it($"Info: name \"{oldName}\" has been changed to \"{s}\"{(isGlobal ? null : ", to make it unique in the folder")}.");
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
	
	public static class Templates {
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
			var ext = FileExt;
			if (ext.Length > 0) if (name.IndexOf('.') < 0 || (IsCodeFile && !name.Ends(ext, true))) name += ext;
		}
		if (name == _name) return true;
		name = CreateNameUniqueInFolder(Parent, name, IsFolder, renaming: this);
		
		if (!RenameL_(name)) return false;
		
		_model.Save.WorkspaceLater();
		FilesModel.Redraw(this, remeasure: true, renamed: true);
		Compiler.Uncache(this, andDescendants: true);
		CodeInfo.FilesChanged();
		return true;
	}
	
	internal bool RenameL_(string name) {
		if (!IsLink) {
			if (!_model.TryFileOperation(() => filesystem.rename(this.FilePath, name, FIfExists.Fail))) return false;
		}
		_SetName(name);
		return true;
		
		//CONSIDER: when renaming or moving, search all code files (except in "Garbage*" folders) and replace the old name or path in strings.
		//	Also doc the garbage folders feature.
	}
	
	/// <summary>
	/// Returns true if can move the tree node into the specified position.
	/// For example, cannot move parent into child etc.
	/// Does not check whether can move the file.
	/// </summary>
	public bool CanMove(FNInsertPos ipos) {
		//cannot move into self or descendants
		if (ipos.f == this || ipos.f.IsDescendantOf(this)) return false;
		
		//cannot move into a non-folder or before/after self
		switch (ipos.pos) {
		case FNInsert.Before:
			if (Next == ipos.f) return false;
			break;
		case FNInsert.After:
			if (Previous == ipos.f) return false;
			break;
		default: //inside
			if (!ipos.f.IsFolder) return false;
			break;
		}
		return true;
	}
	
	/// <summary>
	/// Moves this into, before or after target.
	/// Also moves file if need.
	/// </summary>
	/// <param name="ipos">If f null, uses Root, Last.</param>
	public bool FileMove(FNInsertPos ipos) {
		if (ipos.f == null) ipos = new(Root, FNInsert.Last);
		if (!CanMove(ipos)) return false;
		
		var newParent = ipos.ParentFolder;
		if (newParent != Parent) {
			var name = CreateNameUniqueInFolder(newParent, _name, IsFolder, moving: true);
			
			if (!IsLink) {
				if (!_model.TryFileOperation(() => filesystem.move(this.FilePath, newParent.FilePath + "\\" + name, FIfExists.Fail))) return false;
			}
			
			if (name != _name) _SetName(name);
		}
		
		//move tree node
		Remove();
		Common_MoveCopyNew(ipos);
		Compiler.Uncache(this, andDescendants: true);
		
		return true;
	}
	
	public void Common_MoveCopyNew(FNInsertPos ipos) {
		AddToTree(ipos, setSaveWorkspace: true);
		CodeInfo.FilesChanged();
	}
	
	/// <summary>
	/// Adds this to the tree, updates control, optionally sets to save workspace.
	/// </summary>
	public void AddToTree(FNInsertPos ipos, bool setSaveWorkspace) {
		if (ipos.Inside) ipos.f.AddChild(this, first: ipos.pos == FNInsert.First); else ipos.f.AddSibling(this, after: ipos.pos == FNInsert.After);
		_model.UpdateControlItems();
		if (setSaveWorkspace) _model.Save.WorkspaceLater();
	}
	
	/// <summary>
	/// Copies this into, before or after target.
	/// Also copies file if need.
	/// Returns the copy, or null if fails.
	/// </summary>
	/// <param name="ipos">If f null, uses Root, Last.</param>
	/// <param name="model">Copy into this FileModel. It is != _model when importing workspace.</param>
	/// <param name="copyLinkTarget">For links, copy the link target into the workspace.</param>
	internal FileNode _FileCopy(FNInsertPos ipos, FilesModel model, bool copyLinkTarget = false) {
		_model.Save?.TextNowIfNeed(onlyText: true);
		if (ipos.f == null) ipos = new(Root, FNInsert.Last);
		
		//create unique name
		var newParent = ipos.ParentFolder;
		string name = CreateNameUniqueInFolder(newParent, _name, IsFolder);
		
		//copy file or directory
		if (!IsLink || copyLinkTarget) {
			if (!_model.TryFileOperation(() => filesystem.copy(FilePath, newParent.FilePath + "\\" + name, FIfExists.Fail))) return null;
		}
		
		//create new FileNode with descendants
		var f = new FileNode(model, this, name);
		if (copyLinkTarget) f._linkTarget = null;
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
		f.Common_MoveCopyNew(ipos);
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
	/// If none, throws ArgumentException if canThrow, else returns FNType.Other.
	/// </summary>
	public static FNType XmlTagToFileType(string tag, bool canThrow) => tag switch {
		"d" => FNType.Folder,
		"s" => FNType.Script,
		"c" => FNType.Class,
		"n" => FNType.Other,
		_ => !canThrow ? FNType.Other : throw new ArgumentException("XML element name must be 'd', 's', 'c' or 'n'")
	};
	
	/// <summary>
	/// Detects file type from extension and code.
	/// If .cs, returns Class or Script, else Other.
	/// Must be not folder.
	/// </summary>
	static FNType _DetectFileType(string path) {
		var type = FNType.Other;
		if (path.Ends(".cs", true)) {
			type = FNType.Class;
			try {
				var code = filesystem.loadText(path);
				if (CiUtil.IsScript(code)) type = FNType.Script;
			}
			catch (Exception ex) { Debug_.Print(ex); }
		}
		return type;
	}
	
	#endregion
}
