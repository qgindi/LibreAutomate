using System.Linq;
using System.Xml.Linq;
using System.IO.Compression;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

partial class FilesModel {
	public readonly FileNode Root;
	public readonly int WorkspaceSN; //sequence number of workspace open in this process: 1, 2...
	static int s_workspaceSN;
	public readonly string WorkspaceName;
	public readonly string WorkspaceDirectory;
	public readonly string WorkspaceFile; //.xml file containing the tree of .cs etc files
	public readonly string FilesDirectory; //.cs etc files
										   //public readonly string CacheDirectory; //any cache files
	public readonly string TempDirectory; //any temporary files
	public readonly string NugetDirectory; //NuGet libraries
	public readonly string NugetDirectoryBS; //NugetDirectory\
	public readonly string DllDirectory; //user-created or downloaded libraries
	public readonly string DllDirectoryBS; //LibrariesDirectory\
										   //public readonly string ExeDirectory; //user-created exe files
	public readonly AutoSave Save;
	readonly Dictionary<uint, FileNode> _idMap;
	internal readonly Dictionary<string, object> _nameMap;
	public List<FileNode> OpenFiles;
	readonly string _dbFile;
	public readonly sqlite DB;
	public readonly string SettingsFile;
	public readonly WorkspaceSettings WSSett;
	readonly bool _importing;
	readonly bool _initedFully;
	public object CompilerContext;

	/// <summary>
	/// 
	/// </summary>
	/// <param name="c">Tree control. Can be null, for example when importing workspace.</param>
	/// <param name="file">Workspace file (XML).</param>
	/// <exception cref="ArgumentException">Invalid or not full path.</exception>
	/// <exception cref="Exception">XElement.Load exceptions. And possibly more.</exception>
	public FilesModel(string file, bool importing) {
		_importing = importing;
		WorkspaceFile = pathname.normalize(file);
		WorkspaceDirectory = pathname.getDirectory(WorkspaceFile);
		WorkspaceName = pathname.getName(WorkspaceDirectory);
		FilesDirectory = WorkspaceDirectory + @"\files";
		//CacheDirectory = WorkspaceDirectory + @"\.cache";
		TempDirectory = WorkspaceDirectory + @"\.temp";
		NugetDirectory = WorkspaceDirectory + @"\.nuget";
		NugetDirectoryBS = NugetDirectory + @"\";
		DllDirectory = WorkspaceDirectory + @"\dll";
		DllDirectoryBS = DllDirectory + @"\";
		//ExeDirectory = WorkspaceDirectory + @"\exe";
		if (!_importing) {
			WorkspaceSN = ++s_workspaceSN;
			filesystem.createDirectory(FilesDirectory);
			Save = new AutoSave(this);
		}
		_idMap = new();
		_nameMap = new(StringComparer.OrdinalIgnoreCase);

		Root = FileNode.Load(WorkspaceFile, this); //recursively creates whole model tree; caller handles exceptions

		if (!_importing) {
			WSSett = WorkspaceSettings.Load(SettingsFile = WorkspaceDirectory + @"\settings.json");

			_dbFile = WorkspaceDirectory + @"\state.db";
			try {
				DB = new sqlite(_dbFile, sql:
					//"PRAGMA journal_mode=WAL;" + //no, it does more bad than good
					"CREATE TABLE IF NOT EXISTS _misc (key TEXT PRIMARY KEY, data TEXT);" +
					"CREATE TABLE IF NOT EXISTS _editor (id INTEGER PRIMARY KEY, top INTEGER, pos INTEGER, lines BLOB);"
					);
			}
			catch (Exception ex) {
				print.it($"Failed to open file '{_dbFile}'. Will not load/save workspace state: lists of open files, expanded folders, markers, folding, etc.\r\n\t{ex.ToStringWithoutStack()}");
			}

			folders.Workspace = new FolderPath(WorkspaceDirectory);
			Environment.SetEnvironmentVariable("dll", DllDirectory);
		}
		_initedFully = true;
	}

	public void Dispose() {
		if (_importing) return;
		if (_initedFully) {
			App.Tasks.OnWorkspaceClosed();
			RecentTT.Clear();
			//Save.AllNowIfNeed(); //owner FilesPanel calls this before calling this func. Because may need more code in between.
		}
		Save?.Dispose();
		_InitWatcher(false);
		DB?.Dispose();
		WSSett?.Dispose();
		EditGoBack.DisableUI();
	}

	#region tree control

	public static FilesView TreeControl => Panels.Files.TreeControl;

	/// <summary>
	/// Updates control when changed number or order of visible items (added, removed, moved, etc).
	/// </summary>
	public void UpdateControlItems() { TreeControl.SetItems(Root.Children(), true); }

	/// <summary>
	/// When need to redraw an item in controls that display it.
	/// If the parameter is null, redraw all items.
	/// </summary>
	public static event Action<(FileNode f, bool remeasure)> NeedRedraw;

	/// <summary>
	/// Raises <see cref="NeedRedraw"/> event.
	/// </summary>
	/// <param name="f"></param>
	public static void Redraw(FileNode f = null, bool remeasure = false) { NeedRedraw?.Invoke((f, remeasure)); }

	#endregion

	#region load workspace

	public static void LoadWorkspace(string wsDir = null) {
		wsDir ??= App.Settings.workspace;
		if (wsDir.NE()) wsDir = folders.ThisAppDocuments + "Main";
		var xmlFile = wsDir + @"\files.xml";
		var oldModel = App.Model;
		FilesModel m = null;
	g1:
		try {
			//SHOULDDO: if editor runs as admin, the workspace directory should be write-protected from non-admin processes.

			if (s_isNewWorkspace = !filesystem.exists(xmlFile).File) {
				filesystem.copy(folders.ThisAppBS + @"Default\Workspace", wsDir);
			}

			oldModel?.UnloadingWorkspace_(); //saves all, closes documents, sets current file = null

			m = new FilesModel(xmlFile, importing: false);
		}
		catch (Exception ex) {
			m?.Dispose();
			m = null;
			//print.it($"Failed to load '{wsDir}'. {ex.Message}");
			switch (dialog.showError("Failed to load workspace", wsDir,
				"1 Retry|2 Load another|3 Create new|0 Cancel",
				owner: App.Hmain, expandedText: ex.ToString())) {
			case 1: goto g1;
			case 2: OpenWorkspaceUI(); break;
			case 3: NewWorkspaceUI(); break;
			}
			if (App.Model == null) Environment.Exit(1);
			return;
		}

		oldModel?.Dispose();
		App.Model = m;

		//CONSIDER: unexpand path
		if (wsDir != App.Settings.workspace) {
			if (App.Settings.workspace != null) {
				var ar = App.Settings.recentWS ?? Array.Empty<string>();
				int i = Array.IndexOf(ar, wsDir); if (i >= 0) ar = ar.RemoveAt(i);
				App.Settings.recentWS = ar.InsertAt(0, App.Settings.workspace);
			}
			App.Settings.workspace = wsDir;
		}

		if (App.Loaded == EProgramState.LoadedUI) m.WorkspaceLoadedWithUI(onUiLoaded: false);
	}

	[MethodImpl(MethodImplOptions.NoInlining)] //avoid loading WPF at startup before loading UI
	public void WorkspaceLoadedWithUI(bool onUiLoaded) {
		if (!s_isNewWorkspace) LoadState(expandFolders: true);
		TreeControl.SetItems();
		OpenFiles = new List<FileNode>();
		if (s_isNewWorkspace) {
			s_isNewWorkspace = false;
			AddMissingDefaultFiles(true, true);
			SetCurrentFile(Root.FirstChild, newFile: true);
		} else {
			LoadState(openFiles: true);
		}
		_InitWatcher(true);
		WorkspaceLoadedAndDocumentsOpened?.Invoke();
		if (!onUiLoaded) RunStartupScripts();
	}

	static bool s_isNewWorkspace;
	internal bool NoGlobalCs_; //used by MetaComments.Parse

	public event Action WorkspaceLoadedAndDocumentsOpened;

	/// <summary>
	/// Shows "Open workspace" dialog. On OK loads the selected workspace.
	/// </summary>
	public static void OpenWorkspaceUI() {
		var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") { Title = "Open workspace" };
		if (!d.ShowOpen(out string s, App.Hmain, selectFolder: true)) return;
		if (!filesystem.exists(s + @"\files.xml").File) dialog.showError("The folder must contain file files.xml");
		else LoadWorkspace(s);
	}

	/// <summary>
	/// Shows dialog to create new workspace. On OK creates and loads new workspace.
	/// </summary>
	public static void NewWorkspaceUI() {
		var path = GetDirectoryPathForNewWorkspace();
		if (path != null) LoadWorkspace(path);
	}

	#endregion

	#region find, id

	/// <summary>
	/// Finds file or folder by name or @"\relative path" or id or full path.
	/// </summary>
	/// <param name="name">
	/// Can be:
	/// Name, like "name.cs" or just "name".
	/// Relative path like @"\name.cs" or @"\subfolder\name.cs".
	/// Full path in this workspace or of a linked external file.
	/// &lt;id&gt; - enclosed <see cref="FileNode.IdString"/>, or <see cref="FileNode.IdStringWithWorkspace"/>.
	/// 
	/// Case-insensitive. If enclosed in &lt;&gt;, can be followed by any text.
	/// If just "name" and not found and name does not end with ".cs", tries to find name + ".cs".
	/// </param>
	/// <param name="kind">Ignored if <i>name</i> is id.</param>
	/// <param name="silent">Don't print warning "Found multiple...".</param>
	public FileNode Find(string name, FNFind kind = FNFind.Any, bool silent = false) {
		FoundMultiple = null;
		if (name.NE()) return null;
		if (name[0] == '<') {
			name.ToInt(out long id, 1);
			return FindById(id);
		}
		if (pathname.isFullPath(name)) return FindByFilePath(name, kind);
		if (name[0] == '\\') return Root.FindDescendant(name, kind);
		return _FindByName(name, kind);

		FileNode _FindByName(string name, FNFind kind) {
			if (_nameMap.MultiGet_(name, out FileNode v, out var a)) {
				if (v != null) return KindFilter_(v, kind);
				FileNode first = null;
				foreach (var f in a) {
					if (KindFilter_(f, kind) == null) continue;
					if (first == null) first = f; else (FoundMultiple ??= new() { first }).Add(f);
				}
				if (FoundMultiple == null) return first; //note: don't return the first if found multiple. Unsafe.
				if (!silent) {
					var paths = string.Join(", ", FoundMultiple.Select(o => o.SciLink(path: true)));
					print.warning($"Found multiple '{name}'. Use path if possible, or rename.\r\n\tPaths: {paths}."/*, -1*/);
				}
				return null;
			}
			if (kind != FNFind.Folder && !name.Ends(".cs", true)) return _FindByName(name + ".cs", kind);
			return null;
		}
	}

	/// <summary>
	/// When <see cref="Find"/> returns null because exists multiple items with the specified name/kind, this property contains them. Else null.
	/// </summary>
	public List<FileNode> FoundMultiple;

	internal static FileNode KindFilter_(FileNode f, FNFind kind) => kind switch {
		FNFind.File => !f.IsFolder ? f : null,
		FNFind.Folder => f.IsFolder ? f : null,
		FNFind.CodeFile => f.IsCodeFile ? f : null,
		FNFind.Class => f.IsClass ? f : null,
		//FNFind.Script => f.IsScript ? f : null,
		_ => f,
	};

	/// <summary>
	/// Calls <see cref="Find(string, FNFind, bool)"/>(name, FNFind.CodeFile).
	/// </summary>
	public FileNode FindCodeFile(string name, bool silent = false) => Find(name, FNFind.CodeFile, silent);

	/// <summary>
	/// Finds file or folder by its file path (<see cref="FileNode.FilePath"/>).
	/// </summary>
	/// <param name="path">Full path of a file in this workspace or of a linked external file.</param>
	/// <param name="kind"></param>
	public FileNode FindByFilePath(string path, FNFind kind = FNFind.Any) {
		var d = FilesDirectory;
		if (path.Starts(d, true) && path.Eq(d.Length, '\\')) return Root.FindDescendant(path[d.Length..], kind); //is in workspace folder
		if (kind != FNFind.Folder) foreach (var f in Root.Descendants()) if (f.IsLink && path.Eqi(f.LinkTarget)) return KindFilter_(f, kind); //find link
		return null;
	}

	///// <summary>
	///// If path starts with <see cref="FilesDirectory"/> and '\\', removes the FilesDirectory part and returns true.
	///// </summary>
	///// <param name="path">Full or relative path or name.</param>
	//bool _FullPathToRelative(ref string path) {
	//	var d = FilesDirectory;
	//	bool full = path.Starts(d, true) && path.Eq(d.Length, '\\');
	//	if (full) path = path[d.Length..];
	//	return full;
	//}

	/// <summary>
	/// Adds id/f to the dictionary that is used by <see cref="FindById"/> etc.
	/// If id is 0 or duplicate, generates new.
	/// Returns <i>id</i> or the generated id.
	/// </summary>
	public uint AddGetId(FileNode f, uint id = 0) {
	g1:
		if (id == 0) {
			//Normally we don't reuse ids of deleted items.
			//	Would be problems with something that we cannot/fail/forget to delete when deleting items.
			//	We save MaxId in XML: <files max-i="MaxId">.
			id = ++MaxId;
			if (id == 0) { //if new item created every 8 s, we have 1000 years, but anyway
				for (uint u = 1; u < uint.MaxValue; u++) if (!_idMap.ContainsKey(u)) { MaxId = u - 1; break; } //fast
				goto g1;
			} else if (_idMap.ContainsKey(id)) { //damaged XML file, or maybe a bug?
				Debug_.Print("id already exists:" + id);
				MaxId = _idMap.Keys.Max();
				id = 0;
				goto g1;
			}
			Save?.WorkspaceLater(); //null when importing this workspace
		}
		try { _idMap.Add(id, f); }
		catch (ArgumentException) {
			print.warning($"Duplicate id of '{f.Name}'. Creating new.");
			id = 0;
			goto g1;
		}
		return id;
	}

	/// <summary>
	/// Current largest id, used to generate new id.
	/// The root FileNode's ctor reads it from XML attribute 'max-i' and sets this property.
	/// </summary>
	public uint MaxId { get; set; }

	/// <summary>
	/// Finds file or folder by its <see cref="FileNode.Id"/>.
	/// Returns null if id is 0 or not found.
	/// id can contain <see cref="WorkspaceSN"/> in high-order int.
	/// </summary>
	public FileNode FindById(long id) {
		int idc = (int)(id >> 32); if (idc != 0 && idc != WorkspaceSN) return null;
		uint idf = (uint)id;
		if (idf == 0) return null;
		if (_idMap.TryGetValue(idf, out var f)) {
			Debug_.PrintIf(f == null, "deleted: " + idf);
			return f;
		}
		Debug_.Print("id not found: " + idf);
		return null;
	}

	/// <summary>
	/// Finds file or folder by its <see cref="FileNode.IdString"/>.
	/// Note: it must not be as returned by <see cref="FileNode.IdStringWithWorkspace"/>.
	/// </summary>
	public FileNode FindById(string id) {
		id.ToInt(out long n);
		return FindById(n);
	}

	/// <summary>
	/// Finds all files (and not folders) with the specified name.
	/// Returns empty array if not found.
	/// </summary>
	/// <param name="name">File name, like "name.cs". If starts with backslash, works like <see cref="Find"/>. Does not support <see cref="FileNode.IdStringWithWorkspace"/> string and filename without extension.</param>
	public FileNode[] FindAllFiles(string name) {
		return Root.FindAllDescendantFiles(name);
	}

	#endregion

	#region open/close, select, current, selected, context menu

	/// <summary>
	/// Returns true if f is null or isn't in this workspace or is deleted.
	/// </summary>
	public bool IsAlien(FileNode f) => f?.Model != this || f.IsDeleted;

	/// <summary>
	/// Closes f if open.
	/// Saves text if need, removes from OpenItems, deselects in treeview.
	/// </summary>
	/// <param name="f">Can be any item or null. Does nothing if it is null, folder or not open.</param>
	/// <param name="activateOther">When closing current file, if there are more open files, activate another open file.</param>
	/// <param name="selectOther">Select the activated file.</param>
	/// <param name="focusEditor">If <i>activateOther</i> true, focus code editor.</param>
	public bool CloseFile(FileNode f, bool activateOther = true, bool selectOther = false, bool focusEditor = false) {
		if (IsAlien(f)) return false;
		var of = OpenFiles;
		if (!of.Remove(f)) return false;

		Panels.Editor.ZClose(f);
		f.IsSelected = false;

		if (f == _currentFile) {
			if (activateOther && of.Count > 0) {
				var ff = of[0];
				if (selectOther) ff.SelectSingle();
				if (_SetCurrentFile(ff, focusEditor: focusEditor)) return true;
			}
			_currentFile = null;
		}
		f.UpdateControlRow();

		_UpdateOpenFiles(_currentFile);
		Save.StateLater();

		return true;
	}

	/// <summary>
	/// Closes specified files that are open.
	/// </summary>
	/// <param name="files">Any <b>IEnumerable</b> except <b>OpenFiles</b>.</param>
	/// <param name="dontClose">null or <b>FileNode</b> or <b>BitArray</b> to not close.</param>
	public void CloseFiles(IEnumerable<FileNode> files, object dontClose = null) {
		if (files == OpenFiles) files = OpenFiles.ToArray();
		bool closeCurrent = false;
		int i = 0;
		foreach (var f in files) {
			if (dontClose is System.Collections.BitArray ba) {
				if (i < ba.Length && ba[i++]) continue;
			} else if (f == dontClose) continue;
			if (f == _currentFile) closeCurrent = true; else CloseFile(f, activateOther: false);
		}
		if (closeCurrent) CloseFile(_currentFile);
	}

	/// <summary>
	/// Updates PanelOpen, enables/disables Previous command.
	/// </summary>
	void _UpdateOpenFiles(FileNode current) {
		Panels.Open.ZUpdateList();
		App.Commands[nameof(Menus.File.OpenCloseGo.Previous_document)].Enabled = OpenFiles.Count > 1;
	}

	/// <summary>
	/// Called by <see cref="PanelFiles.ZLoadWorkspace"/> before opening another workspace and disposing this.
	/// Saves all, closes documents, sets _currentFile = null.
	/// </summary>
	internal void UnloadingWorkspace_() {
		Save.AllNowIfNeed();
		UnloadingWorkspaceEvent?.Invoke();
		_currentFile = null;
		Panels.Editor.ZCloseAll(saveTextIfNeed: false);
		OpenFiles.Clear();
		_UpdateOpenFiles(null);
	}

	/// <summary>
	/// Note: unsubscribe to avoid memory leaks.
	/// </summary>
	public event Action UnloadingWorkspaceEvent;

	//rejected. Let unsubscribe in OnClosed: App.Model.UnloadingWorkspaceEvent -= Close;
	///// <summary>
	///// Closes window <i>w</i> when unloading workspace.
	///// </summary>
	//public void UnloadingWorkspaceCloseWindow(Window w) {
	//	Action aClose = w.Close;
	//	UnloadingWorkspaceEvent += aClose;
	//	EventHandler closed = null;
	//	closed = (_, _) => { UnloadingWorkspaceEvent -= aClose; w.Closed -= closed; };
	//	w.Closed += closed;
	//}

	/// <summary>
	/// Gets the current file. It is open/active in the code editor.
	/// </summary>
	public FileNode CurrentFile => _currentFile;
	FileNode _currentFile;

	/// <summary>
	/// Selects the node. If not folder, opens its file in the code editor.
	/// Returns false if failed to open, for example if <i>f</i> is a folder.
	/// </summary>
	public bool SetCurrentFile(FileNode f, bool dontChangeTreeSelection = false, bool newFile = false, bool? focusEditor = true, bool noTemplate = false) {
		if (IsAlien(f)) return false;
		if (!dontChangeTreeSelection) f.SelectSingle();
		if (_currentFile != f) _SetCurrentFile(f, newFile, focusEditor, noTemplate);
		return _currentFile == f;
	}

	/// <summary>
	/// If f!=_currentFile and not folder:
	///		Opens it in editor, adds to OpenFiles, sets _currentFile, saves state later, updates UI.
	///		Saves and hides current document.
	///	Returns false if fails to read file or if f is folder.
	/// </summary>
	/// <param name="f"></param>
	/// <param name="newFile">Should be true if opening the file first time after creating.</param>
	/// <param name="focusEditor">If null, focus later, when mouse enters editor. Ignored if editor was focused (sets focus). Also depends on <i>newFile</i>.</param>
	bool _SetCurrentFile(FileNode f, bool newFile = false, bool? focusEditor = true, bool noTemplate = false) {
		Debug.Assert(!IsAlien(f));
		if (f == _currentFile) return true;
		//print.it(f);
		if (f.IsFolder) return false;

		if (_currentFile != null) Save.TextNowIfNeed();

		var fPrev = _currentFile;
		_currentFile = f;

		if (!Panels.Editor.ZOpen(f, newFile, focusEditor, noTemplate)) {
			_currentFile = fPrev;
			if (OpenFiles.Contains(f)) _UpdateOpenFiles(_currentFile); //?
			return false;
		}

		fPrev?.UpdateControlRow();
		_currentFile?.UpdateControlRow();

		var of = OpenFiles;
		of.Remove(f);
		of.Insert(0, f);
		_UpdateOpenFiles(f);
		Save.StateLater();

		return true;
	}

	void _ItemRightClicked(FileNode f) { //Dispatcher.InvokeAsync
		if (IsAlien(f)) return;
		if (!f.IsSelected) f.SelectSingle();
		_ContextMenu();
	}
	static ContextMenu s_contextMenu;
	bool s_inContextMenu;

	void _ContextMenu() {
		if (s_inContextMenu) return; //workaround for: sometimes, when dying mouse generates >1 rclick, somehow the menu is at screen 0 0
		if (s_contextMenu == null) {
			var m = new ContextMenu { PlacementTarget = TreeControl };
			//m.ItemsSource = App.Commands[nameof(Menus.File)].MenuItem.Items; //shows menu but then closes on mouse over
			App.Commands[nameof(Menus.File)].CopyToMenu(m);
			m.Closed += (_, _) => s_inContextMenu = false;
			s_contextMenu = m;
		}
		s_contextMenu.IsOpen = true;
		s_inContextMenu = true;
	}

	//Called when editor control focused, etc.
	public void EnsureCurrentSelected() {
		//if(_currentFile != null && !_currentFile.IsSelected && _control.SelectedIndices.Count < 2) _currentFile.SelectSingle();
		if (_currentFile != null && !_currentFile.IsSelected) _currentFile.SelectSingle();
	}

	/// <summary>
	/// Selects the node, opens its file in the code editor, optionally goes to the specified position or line or line/column.
	/// Returns false if failed to open, for example if it is a folder (then just selects folder).
	/// </summary>
	/// <param name="f"></param>
	/// <param name="line">If not negative, goes to this 0-based line.</param>
	/// <param name="columnOrPos">If not negative, goes to this 0-based position in text (if line negative) or to this 0-based column in line.</param>
	/// <param name="findText">If not null, finds this text (<b>FindWord</b>), and goes there if found. Then <i>line</i> and <i>columnPos</i> not used.</param>
	public bool OpenAndGoTo(FileNode f, int line = -1, int columnOrPos = -1, string findText = null) {
		App.Wmain.ZShowAndActivate();
		bool wasOpen = _currentFile == f;
		if (!SetCurrentFile(f)) return false;
		var doc = Panels.Editor.ZActiveDoc;
		if (findText != null) {
			line = -1;
			columnOrPos = doc.zText.FindWord(findText);
		}
		if (line >= 0 || columnOrPos >= 0) {
			if (line >= 0) {
				int i = doc.zLineStart(false, line);
				if (columnOrPos > 0) i = doc.zPos8(Math.Min(doc.zPos16(i) + columnOrPos, doc.zLen16)); //not SCI_FINDCOLUMN, it calculates tabs
				columnOrPos = i;
			}
			if (!wasOpen) wait.doEvents(); //else scrolling does not work well if now opened the file. Can't async, because caller may use the new pos immediately.
			doc.zGoToPos(false, columnOrPos);
		} else {
			if (!wasOpen) wait.doEvents(); //caller then may call zGoToPos or zSelect etc
		}
		doc.Focus();
		return true;
	}

	/// <summary>
	/// Finds code file and calls <see cref="OpenAndGoTo(FileNode, int, int, string)"/>. Does nothing if not found.
	/// </summary>
	public bool OpenAndGoTo(string file, int line = -1, int columnOrPos = -1, string findText = null) {
		var f = FindCodeFile(file); if (f == null) return false;
		return OpenAndGoTo(f, line, columnOrPos, findText);
	}

	/// <summary>
	/// Finds file or folder and selects the node. If not folder, opens its file in the code editor, optionally goes to the specified position or line or line/column.
	/// Returns false if failed to find or select.
	/// </summary>
	/// <param name="fileOrFolder">See <see cref="Find"/>.</param>
	/// <param name="line1Based">If not empty, goes to this 1-based line.</param>
	/// <param name="column1BasedOrPos">If not empty, goes to this 0-based position in text (if line empty) or to this 1-based column in line.</param>
	/// <remarks>
	/// If column1BasedOrPos or line1Based not empty, searches only files, not folders.
	/// </remarks>
	public bool OpenAndGoTo2(string fileOrFolder, string line1Based = null, string column1BasedOrPos = null) {
		var f = Find(fileOrFolder, line1Based.NE() && column1BasedOrPos.NE() ? FNFind.Any : FNFind.CodeFile);
		if (f == null) return false;
		if (f.IsFolder) {
			f.SelectSingle();
			return true;
		}
		int line = line1Based.NE() ? -1 : line1Based.ToInt() - 1;
		int columnOrPos = -1; if (!column1BasedOrPos.NE()) columnOrPos = column1BasedOrPos.ToInt() - (line < 0 ? 0 : 1);
		return OpenAndGoTo(f, line, columnOrPos);
	}

	/// <summary>
	/// Finds code file, selects the node, opens in the code editor, searches for the specified text. If finds, goes there.
	/// Returns false if failed to find file or select.
	/// </summary>
	/// <param name="fileOrFolder">See <see cref="Find"/>.</param>
	/// <param name="findText"></param>
	public bool OpenAndGoTo3(string fileOrFolder, string findText) {
		var f = FindCodeFile(fileOrFolder);
		if (f == null) return false;
		return OpenAndGoTo(f, findText: findText);
	}

	#endregion

	#region rename, delete, open/close (menu commands), properties

	public void RenameSelected(bool newFile = false) {
		Panels.PanelManager[Panels.Files].Visible = true; //exception if not visible
		TreeControl.EditLabel(ended: newFile ? ok => { if (ok && Keyboard.IsKeyDown(Key.Enter)) Panels.Editor.ZActiveDoc?.Focus(); } : null);
	}

	public void DeleteSelected() {
		var a = TreeControl.SelectedItems; if (a.Length < 1) return;

		//confirmation
		var text = string.Join("\n", a.Select(f => f.Name));
		var expandedText = "The file will be deleted, unless it is external.\r\nWill use Recycle Bin, if possible.";
		var con = new DControls { Checkbox = "Don't delete file" };
		var r = dialog.show("Deleting", text, "1 OK|0 Cancel", owner: TreeControl, controls: con, expandedText: expandedText);
		if (r == 0) return;

		foreach (var f in a) {
			_Delete(f, dontDeleteFile: con.IsChecked); //info: and saves everything, now and/or later
		}
	}

	bool _Delete(FileNode f, bool dontDeleteFile = false, bool recycleBin = true, bool canDeleteLinkTarget = false) {
		var e = f.Descendants(true);

		CloseFiles(e);
		Uncut();
		App.Model.EditGoBack.OnFileDeleted(f);

		if (!dontDeleteFile && (canDeleteLinkTarget || !f.IsLink)) {
			if (!TryFileOperation(() => filesystem.delete(f.FilePath, recycleBin ? FDFlags.RecycleBin : 0), deletion: true)) return false;
			//FUTURE: move to folder 'deleted'. Moving to RB is very slow. No RB if in removable drive etc.
		} else {
			string s1 = dontDeleteFile ? "File not deleted:" : "The deleted item was a link to";
			print.it($"<>Info: {s1} <explore>{f.FilePath}<>");
		}

		foreach (var k in e) {
			try { DB?.Execute("DELETE FROM _editor WHERE id=?", k.Id); } catch (SLException ex) { Debug_.Print(ex); }
			Au.Compiler.Compiler.Uncache(k);
			_idMap[k.Id] = null;
			_nameMap.MultiRemove_(k.Name, k);
			k.IsDeleted = true;
		}

		f.Remove();
		UpdateControlItems();
		//FUTURE: call event to update other controls.

		Save.WorkspaceLater();
		CodeInfo.FilesChanged();
		return true;
	}

	//SHOULDDO: once (2 times) crashed when deleting folder "@Triggers and toolbars".
	//	Both times in editor was opened a class file from the folder.
	//	First time: messagebox "exception processing message, unexpected parameters".
	//		After restarting were deleted files and tree items. Just several warnings about not found file id.
	//	Second time with debugger: access violation exception somewhere in DispatchMessage -> COM message processing.
	//		After restarting were deleted files but not tree items. Tried to reopen the file, but failed, and no editor control was created.
	//	COM wasn't used explicitly. Maybe because of Recycle Bin.
	//	Then could not reproduce (after recompiling same code).

	//bool _Delete(FileNode f, bool dontDeleteFile = false, bool recycleBin = true, bool canDeleteLinkTarget = false) {
	//	print.qm2.clear();
	//	var e = f.Descendants(true);

	//	CloseFiles(e);
	//	Uncut();

	//	if (!dontDeleteFile && (canDeleteLinkTarget || !f.IsLink)) {
	//		print.qm2.write("deleting files");
	//		if (!TryFileOperation(() => filesystem.delete(f.FilePath, recycleBin), deletion: true)) return false;
	//		print.qm2.write("ok");
	//		//FUTURE: move to folder 'deleted'. Moving to RB is very slow. No RB if in removable drive etc.
	//	} else {
	//		string s1 = dontDeleteFile ? "File not deleted:" : "The deleted item was a link to";
	//		print.it($"<>Info: {s1} <explore>{f.FilePath}<>");
	//	}

	//	foreach (var k in e) {
	//		print.qm2.write("deleting from DB", k);
	//		print.qm2.write(f);
	//		try { DB?.Execute("DELETE FROM _editor WHERE id=?", k.Id); } catch (SLException ex) { Debug_.Print(ex); }
	//		print.qm2.write("ok 1");
	//		Au.Compiler.Compiler.Uncache(k);
	//		_idMap[k.Id] = null;
	//		_nameMap.MultiRemove_(k.Name, k);
	//		k.IsDeleted = true;
	//		print.qm2.write("ok 2");
	//	}
	//	print.qm2.write("db ok");

	//	f.Remove();
	//	print.qm2.write(1);
	//	UpdateControlItems();
	//	print.qm2.write(2);
	//	//FUTURE: call event to update other controls.

	//	Save.WorkspaceLater();
	//	print.qm2.write(3);
	//	CodeInfo.FilesChanged();
	//	print.qm2.write(4);
	//	return true;
	//}

	/// <summary>
	/// Opens the selected item(s) in our editor or in default app or selects in Explorer.
	/// </summary>
	/// <param name="how">1 open, 2 open in new window (not impl), 3 open in default app, 4 select in Explorer.</param>
	public void OpenSelected(int how) {
		var a = TreeControl.SelectedItems; if (a.Length == 0) return;
		foreach (var f in a) {
			switch (how) {
			case 1:
				if (f.IsFolder) TreeControl.Expand(f, true);
				else SetCurrentFile(f);
				break;
			//case 2:
			//	if(f.IsFolder) continue;
			//	//FUTURE
			//	break;
			case 3:
				run.it(f.FilePath);
				break;
			case 4:
				run.selectInExplorer(f.FilePath);
				break;
			}
		}
	}

	/// <summary>
	/// Closes selected or all items, or collapses folders.
	/// Used to implement menu File -> Open/Close.
	/// </summary>
	public void CloseEtc(ECloseCmd how, FileNode dontClose = null) {
		switch (how) {
		case ECloseCmd.CloseSelectedOrCurrent:
			var a = TreeControl.SelectedItems;
			if (a.Length > 0) CloseFiles(a);
			else CloseFile(_currentFile);
			break;
		case ECloseCmd.CloseAll:
			CloseFiles(OpenFiles, dontClose);
			CollapseAll();
			if (dontClose != null) TreeControl.EnsureVisible(dontClose);
			break;
		case ECloseCmd.CollapseAllFolders:
			CollapseAll();
			break;
		case ECloseCmd.CollapseInactiveFolders:
			CollapseAll(exceptWithOpenFiles: true);
			break;
		}
	}

	public void CollapseAll(bool exceptWithOpenFiles = false) {
		bool update = false;
		foreach (var v in Root.Descendants()) {
			if (v.IsExpanded) {
				if (exceptWithOpenFiles && v.Descendants().Any(o => OpenFiles.Contains(o))) continue;
				update = true;
				v.SetIsExpanded(false);
			}
		}
		if (update) UpdateControlItems();
	}

	public enum ECloseCmd {
		/// <summary>
		/// Closes selected files. If there are no selected files, closes current file. Does not collapse selected folders.
		/// </summary>
		CloseSelectedOrCurrent,
		CloseAll,
		CollapseAllFolders,
		CollapseInactiveFolders,
	}

	public void Properties() {
		FileNode f = null;
		if (s_inContextMenu) {
			var a = TreeControl.SelectedItems;
			if (a.Length == 1) f = a[0];
		} else {
			EnsureCurrentSelected();
			f = _currentFile;
		}
		if (f == null) return;
		if (f.IsCodeFile) new DProperties(f).Show();
		//else if(f.IsFolder) new DFolderProperties(f).Show();
		//else new DOtherFileProperties(f).Show();
	}

	#endregion

	#region new item

	/// <summary>
	/// Gets the place where item should be added in operations such as new, paste, import.
	/// If in context menu or atSelection (true when pasting), uses selection and may show dialog "Into the folder?". Else first item in workspace.
	/// </summary>
	(FileNode target, FNPosition pos) _GetInsertPos(bool atSelection = false) {
		FileNode target; FNPosition pos = FNPosition.Before;

		if (atSelection || s_inContextMenu) {
			target = TreeControl.FocusedItem as FileNode;
			if (target == null) return (Root, FNPosition.Inside);

			int i;
			bool isFolder = target.IsFolder && target.IsSelected && TreeControl.SelectedIndices.Count == 1;
			if (isFolder && !target.HasChildren) pos = FNPosition.Inside;
			else if (isFolder && (i = popupMenu.showSimple("1 First in the folder|2 Last in the folder|3 Above|4 Below")) > 0) {
				switch (i) {
				case 1: target = target.FirstChild; break;
				case 2: pos = FNPosition.Inside; break;
				case 4: pos = FNPosition.After; break;
				}
			} else if (target.Next == null) pos = FNPosition.After; //usually users want to add after the last, not before
		} else { //top
			target = Root.FirstChild;
			if (target == null) { target = Root; pos = FNPosition.Inside; }
		}

		return (target, pos);
	}

	/// <summary>
	/// Creates new item.
	/// Opens file, or selects folder, or opens main file of project folder. Optionally begins renaming.
	/// Loads files.xml, finds template's element and calls <see cref="NewItemX"/>; it calls <see cref="NewItemLX"/>.
	/// </summary>
	/// <param name="template">
	/// Relative path of a file or folder in the Templates\files folder. Case-sensitive, as in workspace.
	/// Examples: "File.cs", "File.txt", "Subfolder", "Subfolder\File.cs".
	/// Special names: null (creates folder), "Script.cs", "Class.cs".
	/// If folder and not null, adds descendants too; removes '!' from the start of template folder name.
	/// </param>
	/// <param name="where">If null, adds at the context menu position or top.</param>
	/// <param name="name">If not null, creates with this name (eg "name.cs"). Else gets name from template. In any case makes unique name.</param>
	public FileNode NewItem(string template, (FileNode target, FNPosition pos)? where = null, string name = null, bool beginRenaming = false, EdNewFileText text = null) {
		XElement x = null;
		if (template != null) {
			x = FileNode.Templates.LoadXml(template); if (x == null) return null;
		}
		return NewItemX(x, where, name, beginRenaming, text);
	}

	/// <summary>
	/// Creates new item.
	/// Returns the new item, or null if fails.
	/// Does not open/select/startRenaming.
	/// </summary>
	/// <param name="template">See <see cref="NewItem"/>.</param>
	/// <param name="where">If null, adds at the context menu position or top.</param>
	/// <param name="name">If not null, creates with this name (eg "name.cs"). Else gets name from template. In any case makes unique name.</param>
	public FileNode NewItemL(string template, (FileNode target, FNPosition pos)? where = null, string name = null) {
		XElement x = null;
		if (template != null) {
			x = FileNode.Templates.LoadXml(template); if (x == null) return null;
		}
		return NewItemLX(x, where, name);
	}

	/// <summary>
	/// Creates new item.
	/// Opens file, or selects folder, or opens main file of project folder. Optionally begins renaming.
	/// Calls <see cref="NewItemLX"/>.
	/// <param name="template">An XElement of files.xml of the Templates workspace. If null, creates folder.</param>
	/// <param name="where">If null, adds at the context menu position or top.</param>
	/// <param name="name">If not null, creates with this name (eg "name.cs"). Else gets name from template. In any case makes unique name.</param>
	/// </summary>
	public FileNode NewItemX(XElement template, (FileNode target, FNPosition pos)? where = null, string name = null, bool beginRenaming = false, EdNewFileText text = null) {
		string s = null;
		if (text != null && text.replaceTemplate) {
			s = text.meta.NE() ? text.text : _MetaPlusText(text.text);
			text = null;
		}

		var f = NewItemLX(template, where, name, s);
		if (f == null) return null;

		if (beginRenaming && template != null && FileNode.Templates.IsInDefault(template)) beginRenaming = false;

		if (f.IsFolder) {
			if (f.IsProjectFolder(out var main) && main != null) SetCurrentFile(f = main, newFile: true); //open the main file of the new project folder
			else f.SelectSingle(); //select the new folder
		} else {
			SetCurrentFile(f, newFile: true, noTemplate: text?.replaceTemplate ?? false); //open the new file
		}

		if (text != null && f == CurrentFile) {
			Debug.Assert(f.IsScript);
			s = f.GetText();
			var me = Au.Compiler.MetaComments.FindMetaComments(s).end;
			if (!text.meta.NE()) {
				if (me == 0) s = _MetaPlusText(s); //never mind: should skip script doc comments at start. Rare and not important.
				else s = s.Insert(me - 3, (s[me - 4] == ' ' ? "" : " ") + text.meta + " ");
			}
			if (!text.text.NE()) {
				if (s.NE()) s = text.text;
				else if (s.RxMatch(@"\R\R", 0, out RXGroup g, range: me..)) s = s.Insert(g.End, text.text);
				else if (s.RxMatch(@"\R\z", 0, out g, range: me..)) s = s + "\r\n" + text.text;
			}
			Panels.Editor.ZActiveDoc.zSetText(s);
		}

		if (beginRenaming && f.IsSelected) RenameSelected(newFile: !f.IsFolder);
		return f;

		string _MetaPlusText(string t) => $"/*/ {text.meta} /*/{(t.Starts("//.") ? " " : "\r\n")}{t}";
	}

	/// <summary>
	/// Creates new item.
	/// Returns the new item, or null if fails.
	/// Does not open/select/startRenaming.
	/// </summary>
	/// <param name="template">An XElement of files.xml of the Templates workspace. If null, creates folder.</param>
	/// <param name="where">If null, adds at the context menu position or top.</param>
	/// <param name="name">If not null, creates with this name (eg "name.cs"). Else gets name from template. In any case makes unique name.</param>
	/// <param name="text">If not null, sets this text. If null, sets default text (template etc). Not used for folders.</param>
	public FileNode NewItemLX(XElement template, (FileNode target, FNPosition pos)? where = null, string name = null, string text = null) {
		var (target, pos) = where ?? _GetInsertPos();
		FileNode newParent = (pos == FNPosition.Inside) ? target : target.Parent;

		//create unique name
		bool isFolder = template == null || template.Name.LocalName == "d";
		if (name == null) {
			bool append1 = true;
			if (template == null) {
				name = "Folder";
			} else {
				name = template.Attr("n");
				if (isFolder && name.Starts('!')) name = name[1..];
				append1 = !FileNode.Templates.IsInDefault(template);
			}
			//let unique names start from 1
			if (append1) {
				int i;
				if (!isFolder && (i = name.LastIndexOf('.')) > 0) name = name.Insert(i, "1"); else name += "1";
			}
		}
		name = FileNode.CreateNameUniqueInFolder(newParent, name, isFolder, autoGenerated: true);

		return _NewItem(target, pos, template, name, text);
	}

	FileNode _NewItem(FileNode target, FNPosition pos, XElement template, string name, string text) {
		var fileType = template == null ? EFileType.Folder : FileNode.XmlTagToFileType(template.Name.LocalName, canThrow: false);
		Debug.Assert(fileType is not (EFileType.Script or EFileType.Class) || name.Ends(".cs"));

		if (text == null && fileType != EFileType.Folder) {
			string relPath = template.Attr("n");
			for (var p = template; (p = p.Parent).Name.LocalName != "files";) relPath = p.Attr("n") + "\\" + relPath;
			if (fileType == EFileType.Other) {
				text = filesystem.loadText(FileNode.Templates.DefaultDirBS + relPath);
			} else if (FileNode.Templates.IsStandardTemplateName(relPath, out var tt)) {
				text = FileNode.Templates.Load(tt);
				//if (tt == FileNode.ETempl.Script) text = text.RxReplace(@"\bScript\s*\{", "Script {", 1); //no. The user will see warning when compiling, and let update custom template.
			} else {
				text = filesystem.loadText(FileNode.Templates.DefaultDirBS + relPath);
				if (text.Length < 20 && text.Starts("//#")) { //load default or custom template?
					tt = text switch { "//#script" => FileNode.ETempl.Script, "//#class" => FileNode.ETempl.Class, _ => 0 };
					if (tt != 0) text = FileNode.Templates.Load(tt);
				}
			}
		}

		FileNode parent = (pos == FNPosition.Inside) ? target : target.Parent;
		var path = parent.FilePath + "\\" + name;
		if (!TryFileOperation(() => {
			if (fileType == EFileType.Folder) filesystem.createDirectory(path);
			else filesystem.saveText(path, text, tempDirectory: TempDirectory);
		})) return null;

		var f = new FileNode(this, name, fileType);
		f.Common_MoveCopyNew(target, pos);

		if (template != null) {
			if (template.Attr(out string icon, "icon")) f.CustomIconName = icon;

			if (fileType == EFileType.Folder) {
				foreach (var x in template.Elements()) {
					_NewItem(f, FNPosition.Inside, x, x.Attr("n"), null);
				}
			}
		}

		return f;
	}

	#endregion

	#region clipboard

	struct _Clipboard {
		public FileNode[] items;
		public bool cut;
		public uint clipSN;

		public bool IsCut(FileNode f) => cut && items.Contains(f);
	}
	_Clipboard _clipboard;

	public void CutCopySelected(bool cut) {
		Uncut();
		var a = TreeControl.SelectedItems; if (a.NE_()) return;
		_clipboard = new _Clipboard { items = a, cut = cut };
		if (cut) {
			//we don't support cut to outside this workspace. Much work, rarely used, can copy/delete.
			//	The same with copy/paste between workspaces.
			clipboard.clear();
			TreeControl.Redraw();
		} else {
			var d = new clipboardData();
			d.AddFiles(a.Select(o => o.FilePath).ToArray());
			d.AddText(string.Join("\r\n", a.Select(o => o.Name)));
			d.SetClipboard();
		}
		_clipboard.clipSN = Api.GetClipboardSequenceNumber();
	}

	public void Paste() {
		if (_clipboard.items != null && _clipboard.clipSN != Api.GetClipboardSequenceNumber()) Uncut();
		var (target, pos) = _GetInsertPos(true);
		if (_clipboard.items != null) {
			_MultiCopyMove(!_clipboard.cut, _clipboard.items, target, pos);
			Uncut();
		} else {
			using (new clipboard.OpenClipboard_(false)) {
				var h = Api.GetClipboardData(Api.CF_HDROP);
				if (h != default) {
					var a = clipboardData.HdropToFiles_(h);
					_DroppedOrPasted(null, a, false, target, pos);
				} else if (clipboardData.GetText_(0) is string s && s.Length > 0) {
					SciCode.IsForumCode_(s, newFile: true);
				}
			}
		}
	}

	public void Uncut() {
		bool cut = _clipboard.cut;
		if (!cut && _clipboard.items != null && _clipboard.clipSN == Api.GetClipboardSequenceNumber()) clipboard.clear();
		_clipboard = default;
		if (cut) TreeControl.Redraw();
	}

	public bool IsCut(FileNode f) => _clipboard.IsCut(f);

	public void SelectedCopyPath(bool full) {
		var a = TreeControl.SelectedItems; if (a.Length == 0) return;
		clipboard.text = string.Join("\r\n", a.Select(f => full ? f.FilePath : f.ItemPath));
	}

	#endregion

	#region import, move, copy

	void _DroppedOrPasted(FileNode[] nodes, string[] files, bool copy, FileNode target, FNPosition pos) {
		if (nodes != null) {
			_MultiCopyMove(copy, nodes, target, pos);
		} else {
			if (target == null) { target = Root; pos = FNPosition.Inside; }
			if (files.Length == 1 && IsWorkspaceDirectoryOrZip_ShowDialogOpenImport(files[0], out int dialogResult)) {
				switch (dialogResult) {
				case 1: timer.after(1, _ => LoadWorkspace(files[0])); break;
				case 2: ImportWorkspace(files[0], (target, pos)); break;
				}
				return;
			}
			ImportFiles(files, target, pos, copySilently: copy);
		}
	}

	/// <summary>
	/// Imports one or more files into the workspace.
	/// </summary>
	/// <param name="a">Files. If null, shows dialog to select files.</param>
	public void ImportFiles(string[] a = null) {
		if (a == null) if (!new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") { Title = "Import files" }.ShowOpen(out a)) return;

		var (target, pos) = _GetInsertPos();
		ImportFiles(a, target, pos);
	}

	/// <summary>
	/// Imports another workspace folder or zip file (workspace or not) into this workspace.
	/// </summary>
	/// <param name="wsDirOrZip">Workspace directory or any .zip file.</param>
	/// <param name="where">If null, calls _GetInsertPos.</param>
	public void ImportWorkspace(string wsDirOrZip = null, (FileNode target, FNPosition pos)? where = null) {
		try {
			string wsDir, folderName;
			bool isZip = wsDirOrZip.Ends(".zip") && filesystem.exists(wsDirOrZip).File, notWorkspace = false;

			if (isZip) {
				folderName = pathname.getNameNoExt(wsDirOrZip);
				wsDir = folders.ThisAppTemp + folderName;
				filesystem.delete(wsDir);
				ZipFile.ExtractToDirectory(wsDirOrZip, wsDir);
				notWorkspace = !IsWorkspaceDirectoryOrZip(wsDir, out _);
			} else {
				wsDir = wsDirOrZip;
				folderName = pathname.getName(wsDir);
			}

			//create new folder for workspace's items
			var folder = NewItemLX(null, where, folderName);
			if (folder == null) return;

			if (notWorkspace) {
				ImportFiles(Directory.GetFileSystemEntries(wsDir), folder, FNPosition.Inside, copySilently: true);
			} else {
				var m = new FilesModel(wsDir + @"\files.xml", importing: true);
				var a = m.Root.Children().ToArray();
				_MultiCopyMove(true, a, folder, FNPosition.Inside, true);
				m.Dispose(); //currently does nothing
				print.it($"Info: Imported '{wsDirOrZip}' to folder '{folder.Name}'.\r\n\t{GetSecurityInfo()}");
			}

			folder.SelectSingle();
			if (isZip) filesystem.delete(wsDir);
		}
		catch (Exception ex) { print.it(ex.Message); }
	}

	void _MultiCopyMove(bool copy, FileNode[] a, FileNode target, FNPosition pos, bool importingWorkspace = false) {
		if (copy) TreeControl.UnselectAll();
		try {
			bool movedCurrentFile = false;
			var a2 = new List<FileNode>(a.Length);
			foreach (var f in (pos == FNPosition.After) ? a.Reverse() : a) {
				if (!importingWorkspace && !this.IsMyFileNode(f)) continue; //deleted?
				if (copy) {
					var fCopied = f.FileCopy(target, pos, this);
					if (fCopied != null) a2.Add(fCopied);
				} else {
					if (!f.FileMove(target, pos)) continue;
					if (!movedCurrentFile && _currentFile != null) {
						if (f == _currentFile || (f.IsFolder && _currentFile.IsDescendantOf(f))) movedCurrentFile = true;
					}
				}
			}
			if (movedCurrentFile) TreeControl.EnsureVisible(_currentFile);
			if (copy && (pos != FNPosition.Inside || target.IsExpanded)) {
				bool focus = true;
				foreach (var f in a2) {
					f.IsSelected = true;
					if (focus) { focus = false; TreeControl.SetFocusedItem(f); }
				}
			}
		}
		catch (Exception ex) { print.it(ex.Message); }

		//info: don't need to schedule saving here. FileCopy and FileMove did it.
	}

	public void ImportFiles(string[] a, FileNode target, FNPosition pos, bool copySilently = false, bool dontSelect = false, bool dontPrint = false) {
		bool fromWorkspaceDir = false, dirsDropped = false;
		for (int i = 0; i < a.Length; i++) {
			var s = a[i] = pathname.normalize(a[i]);
			if (s.Find(@"\$RECYCLE.BIN\", true) > 0) {
				dialog.show("Files from Recycle Bin", $"At first restore the file to the <a href=\"{FilesDirectory}\">workspace folder</a> or other normal folder.",
					icon: DIcon.Info, owner: TreeControl, onLinkClick: e => run.itSafe(e.LinkHref));
				return;
			}
			var fd = FilesDirectory;
			if (!fromWorkspaceDir) {
				if (s.Starts(fd, true) && (s.Length == fd.Length || s[fd.Length] == '\\')) fromWorkspaceDir = true;
				else if (!dirsDropped) dirsDropped = filesystem.exists(s).Directory;
			}
		}
		int r;
		if (copySilently) {
			if (fromWorkspaceDir) {
				dialog.showInfo("Files from workspace folder", "Ctrl not supported."); //not implemented
				return;
			}
			r = 2; //copy
		} else if (fromWorkspaceDir) {
			r = 3; //move
		} else {
			string buttons = (dirsDropped ? null : "1 Add as a link to the external file|") + "2 Copy to the workspace folder|3 Move to the workspace folder|0 Cancel";
			r = dialog.show("Import files", string.Join("\n", a), buttons, DFlags.CommandLinks, owner: TreeControl, footer: GetSecurityInfo("v|"));
			if (r == 0) return;
		}

		var newParent = (pos == FNPosition.Inside) ? target : target.Parent;
		bool select = !dontSelect && (pos != FNPosition.Inside || target.IsExpanded), focus = select;
		if (select) TreeControl.UnselectAll();
		try {
			var newParentPath = newParent.FilePath + "\\";
			var (nf1, nd1, nc1) = dontPrint ? default : _CountFilesFolders();

			foreach (var path in a) {
				var g = filesystem.exists(path, true);
				if (!g.Exists || g.IsNtfsLink) continue;
				bool isDir = g.Directory && r != 1;

				if (fromWorkspaceDir) {
					var relPath = path[FilesDirectory.Length..];
					var fExists = this.Find(relPath);
					if (fExists != null) {
						fExists.FileMove(target, pos);
						continue;
					}
				}

				FileNode k;
				var name = pathname.getName(path);
				if (r == 1) {
					if (name.Ends(".cs", true)) name = name.Insert(^3, " (link)"); else name += " (link)";
				}
				if (!fromWorkspaceDir) name = FileNode.CreateNameUniqueInFolder(newParent, name, isDir);

				if (r == 1) { //add as link
					k = new FileNode(this, name, path, false, path); //CONSIDER: unexpand
				} else {
					k = new FileNode(this, name, path, isDir);
					if (isDir) _AddDir(path, k);
					if (!TryFileOperation(() => {
						var newPath = newParentPath + name;
						if (r == 2) filesystem.copy(path, newPath, FIfExists.Fail);
						else filesystem.move(path, newPath, FIfExists.Fail);
					})) continue;
				}
				target.AddChildOrSibling(k, pos, false);
				if (select) {
					k.IsSelected = true;
					if (focus) { focus = false; TreeControl.SetFocusedItem(k); }
				}
			}

			if (!dontPrint) {
				var (nf2, nd2, nc2) = _CountFilesFolders();
				int nf = nf2 - nf1, nd = nd2 - nd1, nc = nc2 - nc1;
				if (nf + nd > 0) print.it($"Info: Imported {nf} files and {nd} folders.{(nc > 0 ? GetSecurityInfo("\r\n\t") : null)}");
			}
		}
		catch (Exception ex) { print.it(ex.Message); }
		Save.WorkspaceLater();
		CodeInfo.FilesChanged();

		void _AddDir(string path, FileNode parent) {
			foreach (var u in filesystem.enumerate(path, FEFlags.UseRawPath | FEFlags.SkipHiddenSystem)) {
				bool isDir = u.IsDirectory;
				var k = new FileNode(this, u.Name, u.FullPath, isDir);
				parent.AddChild(k);
				if (isDir) _AddDir(u.FullPath, k);
			}
		}

		(int nf, int nd, int nc) _CountFilesFolders() {
			int nf = 0, nd = 0, nc = 0;
			foreach (var v in Root.Descendants()) if (v.IsFolder) nd++; else { nf++; if (v.IsCodeFile) nc++; }
			return (nf, nd, nc);
		}
	}

	/// <summary>
	/// Adds to workspace 1 file (not folder) that exists in workspace folder in filesystem.
	/// </summary>
	public FileNode ImportFromWorkspaceFolder(string path, FileNode target, FNPosition pos) {
		FileNode R = null;
		try {
			if (!filesystem.exists(path, true).File) return null;

			var relPath = path[FilesDirectory.Length..];
			var fExists = this.Find(relPath);
			if (fExists != null) {
				if (fExists.IsFolder) return null;
				R = fExists;
			} else {
				var name = pathname.getName(path);
				R = new FileNode(this, name, path, false);
				target.AddChildOrSibling(R, pos, false);
			}
		}
		catch (Exception ex) { print.it(ex.Message); }
		Save.WorkspaceLater();
		CodeInfo.FilesChanged();
		return R;
	}

	#endregion

	#region export

	/// <summary>
	/// Shows dialog to get path for new or exporting workspace.
	/// Returns workspace's directory path.
	/// Does not create any files/directories.
	/// </summary>
	/// <param name="name">Default name of the workspace.</param>
	/// <param name="location">Default parent directory of the main directory of the workspace.</param>
	public static string GetDirectoryPathForNewWorkspace(string name = null, string location = null) {
		var d = new DNewWorkspace(name, location ?? folders.ThisAppDocuments) { Owner = App.Wmain, ShowInTaskbar = false };
		if (d.ShowDialog() != true) return null;
		return d.ResultPath;
	}

	public bool ExportSelected(string location = null, bool zip = false) {
		var a = TreeControl.SelectedItems; if (a.Length < 1) return false;

		string name = a[0].Name; if (!a[0].IsFolder) name = pathname.getNameNoExt(name);

		if (a.Length == 1 && a[0].IsFolder && a[0].HasChildren) a = a[0].Children().ToArray();

		string wsDir;
		if (zip) {
			var d = new FileOpenSaveDialog("{4D1F3AFB-DA1A-45AC-8C12-41DDA5C51CDA}") {
				FileTypes = "Zip files|*.zip",
				DefaultExt = "zip",
				InitFolderFirstTime = location ?? folders.ThisAppDocuments,
				FileNameText = name + ".zip",
			};
			if (!d.ShowSave(out location, App.Hmain, overwritePrompt: false)) return false;
			wsDir = folders.ThisAppTemp + "Workspace zip";
			filesystem.delete(wsDir);
		} else {
			wsDir = GetDirectoryPathForNewWorkspace(name, location);
			if (wsDir == null) return false;
		}

		string filesDir = wsDir + @"\files";
		try {
			filesystem.createDirectory(filesDir);
			foreach (var f in a) {
				if (!f.IsLink) filesystem.copyTo(f.FilePath, filesDir);
			}
			FileNode.Export(a, wsDir + @"\files.xml");
		}
		catch (Exception ex) {
			print.it(ex);
			return false;
		}

		if (zip) {
			filesystem.delete(location);
			ZipFile.CreateFromDirectory(wsDir, location);
			filesystem.delete(wsDir);
			wsDir = location;
		}

		print.it($"<>Exported to <explore>{wsDir}<>");
		return true;
	}

	#endregion

	#region watch folder

	FileSystemWatcher _watcher;

	public bool IsWatchingFileChanges => _watcher != null;

	void _InitWatcher(bool init) {
		if (init) {
			try {
				_watcher = new FileSystemWatcher(FilesDirectory) {
					IncludeSubdirectories = true,
					NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
				};
				//we need to know only when files are modified. Not when deleted/created/renamed.
				//	But may save using temp file etc, therefore also need 'created' and 'renamed' events.
				_watcher.Changed += _watcher_Event;
				_watcher.Created += _watcher_Event;
				//_watcher.Deleted += _watcher_Event;
				_watcher.Renamed += _watcher_Event;
				_watcher.EnableRaisingEvents = true;
			}
			catch (Exception ex) { init = false; Debug_.Print(ex); }
		}
		if (!init) {
			_watcher?.Dispose(); //disables raising events and sets all events = null
			_watcher = null;
		}
		//SHOULDDO: watch links too. Maybe without a FileSystemWatcher.
		//	Algorithm:
		//	When getting link target text (when opening in editor or getting FileNode text), remember file mod time.
		//	If it is current file in editor, use timer to watch for changed mod time.
		//	Else, when getting FileNode text, don't use cached text if changed mod time.
	}

	private void _watcher_Event(object sender, FileSystemEventArgs e) { /*in thread pool*/
		//if(e.Name.Ends("~temp") || e.Name.Ends("~backup")) return; //no such events, because we use other directory for temp files
		if (e.ChangeType == WatcherChangeTypes.Changed && !filesystem.exists(e.FullPath, true).File) return; //we receive 'directory changed' after every 'file changed' etc

		try { TreeControl?.Dispatcher.InvokeAsync(() => _watcher_Event2(e)); }
		catch (Exception ex) { Debug_.Print(ex); }
	}

	private void _watcher_Event2(FileSystemEventArgs e) { //in main thread
		var name = e.Name;
		//var name = e is RenamedEventArgs ren ? ren.OldName : e.Name;
		var f = Find("\\" + name);
		//if(e is RenamedEventArgs r) print.it(e.ChangeType, r.OldName, e.Name, r.OldFullPath, e.FullPath, f); else print.it(e.ChangeType, e.Name, e.FullPath, f);
		if (f == null || f.IsLink) return;
		//Debug_.Print($"<><c blue>File {e.ChangeType.ToString().Lower()} externally: {f}  ({e.FullPath})<>");
		if (f.IsFolder) {
			//if(e.ChangeType == WatcherChangeTypes.Changed) return;
			foreach (var v in f.Descendants()) v.UnCacheText(fromWatcher: true);
		} else {
			f.UnCacheText(fromWatcher: true);
		}
	}

	/// <summary>
	/// Calls Action a in try/catch. On exception prints message and returns false.
	/// Temporarily disables the file system watcher if need.
	/// </summary>
	public bool TryFileOperation(Action a, bool deletion = false) {
		bool pause = _watcher != null && !deletion;
		try {
			if (pause) _watcher.EnableRaisingEvents = false;
			a();
		}
		catch (Exception ex) { print.it(ex); return false; }
		finally { if (pause) _watcher.EnableRaisingEvents = true; } //fast
		return true;
	}

	#endregion

	#region fill menu

	/// <summary>
	/// Adds recent workspaces to submenu File -> Workspace.
	/// </summary>
	public static void FillMenuRecentWorkspaces(MenuItem sub) {
		void _Add(string path, int i) {
			var mi = new MenuItem { Header = path.Replace("_", "__") };
			if (i == 0) mi.FontWeight = FontWeights.Bold;
			mi.Click += (_, _) => LoadWorkspace(path);
			sub.Items.Insert(i, mi);
		}

		while (sub.Items[0] is not Separator) sub.Items.RemoveAt(0);
		_Add(App.Settings.workspace, 0);
		var ar = App.Settings.recentWS;
		int j = 0, i = 0, n = ar?.Length ?? 0;
		for (; i < n; i++) {
			if (sub.Items.Count >= 15 || !filesystem.exists(ar[i]).Directory) ar[i] = null;
			else _Add(ar[i], ++j);
		}
		if (j < i) App.Settings.recentWS = ar.Where(o => o != null).ToArray();
	}

	/// <summary>
	/// Adds templates to File -> New.
	/// </summary>
	public static void FillMenuNew(MenuItem sub) {
		var xroot = FileNode.Templates.LoadXml();
		if (sub.Items.Count > c_menuNewDefItems) { //not first time
			if (xroot == sub.Tag) return; //else rebuild menu because Templates\files.xml modified
			for (int i = sub.Items.Count; --i >= c_menuNewDefItems;) sub.Items.RemoveAt(i);
		}
		sub.Tag = xroot;

		var templDir = FileNode.Templates.DefaultDirBS;
		_CreateMenu(sub, xroot, null, 0);

		void _CreateMenu(MenuItem mParent, XElement xParent, string dir, int level) {
			foreach (var x in xParent.Elements()) {
				string tag = x.Name.LocalName, name = x.Attr("n");
				int isFolder = tag == "d" ? 1 : 0;
				if (isFolder == 1) {
					isFolder = name[0] switch { '@' => 2, '!' => 3, _ => 1 }; //@ project, ! simple folder
				} else {
					if (level == 0 && FileNode.Templates.IsStandardTemplateName(name, out _)) continue;
				}
				string relPath = dir + name;
				if (isFolder == 3) name = name[1..];
				var item = new MenuItem { Header = name.Replace("_", "__") };
				if (isFolder == 1) {
					_CreateMenu(item, x, relPath + "\\", level + 1);
				} else {
					item.Click += (_, e) => App.Model.NewItemX(x, beginRenaming: true);
					var ft = FileNode.XmlTagToFileType(tag, canThrow: false);
					item.Icon = ft == EFileType.Other
						? new Image { Source = icon.of(templDir + relPath)?.ToWpfImage() }
						: ImageUtil.LoadWpfImageElement(FileNode.GetFileTypeImageSource(ft));
				}
				mParent.Items.Add(item);
			}
		}
	}
	const int c_menuNewDefItems = 3;

	#endregion

	#region other

	/// <summary>
	/// Adds some default files if missing.
	/// </summary>
	/// <param name="scriptForNewWorkspace">If empty workspace, creates new empty script from current template.</param>
	/// <param name="globalCs">If class file "global.cs" not found, creates it in existing or new folder "Classes".</param>
	public void AddMissingDefaultFiles(bool scriptForNewWorkspace = false, bool globalCs = false) {
		if (scriptForNewWorkspace && Root.FirstChild == null) {
			NewItem(@"Script.cs");
		}
		if (globalCs && null == Find("global.cs", FNFind.Class)) {
			var folder = Find(@"\Classes", FNFind.Folder);
			if (folder == null) folder = NewItemL(null, (Root, FNPosition.Inside), "Classes");
			NewItemL(@"Default\global.cs", (folder, FNPosition.Inside));
		}
	}

	public class UserData {
		public string guid { get; set; }
		public string startupScripts { get; set; }
	}

	public UserData CurrentUser => WSSett?.users?.FirstOrDefault(o => o.guid == App.UserGuid);

	public string StartupScriptsCsv {
		get => CurrentUser?.startupScripts;
		set {
			if (WSSett == null) return;
			var u = CurrentUser;
			if (u == null) {
				u = new UserData { guid = App.UserGuid };
				var a = WSSett.users ?? Array.Empty<UserData>();
				a = a.InsertAt(0, u);
				WSSett.users = a;
			}
			u.startupScripts = value;
		}
	}

	public void RunStartupScripts() {
		var csv = StartupScriptsCsv; if (csv == null) return;
		try {
			var x = csvTable.parse(csv);
			foreach (var row in x.Rows) {
				string file = row[0];
				if (file.Starts("//")) continue;
				var f = FindCodeFile(file);
				if (f == null) { print.it("Startup script not found: " + file + ". Please edit Options -> Run scripts..."); continue; }
				int delay = 10;
				if (x.ColumnCount > 1) {
					var sd = row[1];
					delay = sd.ToInt(0, out int end);
					if (end > 0 && !sd.Ends("ms", true)) delay = (int)Math.Min(delay * 1000L, int.MaxValue);
					if (delay < 10) delay = 10;
				}
				timer.after(delay, t => {
					CompileRun.CompileAndRun(true, f);
				});
			}
		}
		catch (FormatException) { }
	}

	//Used mostly by SciCode, but owned by workspace because can go to any file.
	internal readonly EditGoBack EditGoBack = new();

	#endregion

	#region util

	/// <summary>
	/// Returns true if FileNode f is not null and belongs to this FilesModel and is not deleted.
	/// </summary>
	public bool IsMyFileNode(FileNode f) { return Root.IsAncestorOf(f); }

	/// <summary>
	/// Returns true if s is path of a workspace directory or .zip file.
	/// </summary>
	public static bool IsWorkspaceDirectoryOrZip(string path, out bool zip) {
		zip = false;
		switch (filesystem.exists(path)) {
		case 2:
			string xmlFile = path + @"\files.xml";
			if (filesystem.exists(xmlFile).File && filesystem.exists(path + @"\files").Directory) {
				try { return XmlUtil.LoadElem(xmlFile).Name == "files"; } catch { }
			}
			break;
		case 1 when path.Ends(".zip", true):
			return zip = true;
		}
		return false;
	}

	/// <summary>
	/// If s is path of a workspace directory or .zip file, shows "Open/import" dialog and returns true.
	/// dialogResult receives: 1 Open, 2 Import, 0 Cancel.
	/// </summary>
	public static bool IsWorkspaceDirectoryOrZip_ShowDialogOpenImport(string path, out int dialogResult) {
		dialogResult = 0;
		if (!IsWorkspaceDirectoryOrZip(path, out bool zip)) return false;
		var text1 = zip ? "Import files from zip" : "Workspace";
		var buttons = zip ? "2 Import|0 Cancel" : "1 Open|2 Import|0 Cancel";
		dialogResult = dialog.show(text1, path, buttons, footer: GetSecurityInfo("v|"));
		return true;
	}

	/// <summary>
	/// Security info string.
	/// </summary>
	public static string GetSecurityInfo(string prefix = null) {
		return prefix + "Security info: Unknown C# script files can contain malicious code - virus, spyware, etc. It is safe to import, open and edit C# files if you don't run them. Triggers don't work until run.";
	}

	#endregion
}

enum FNPosition {
	//note: must match Aga.Controls.Tree.NodePosition
	Inside, Before, After
}

enum FNFind {
	Any, File, Folder, CodeFile, Class/*, Script*/ //Script not useful because class files can be executable too
}

class EdNewFileText {
	public bool replaceTemplate;
	public string text, meta;

	public EdNewFileText() { }

	public EdNewFileText(bool replaceTemplate, string text, string meta = null) {
		this.replaceTemplate = replaceTemplate;
		this.text = text;
		this.meta = meta;
	}
}
