//#define USE_CHANGED_EVENTS

using System.Xml;
using System.Xml.Linq;

partial class FilesModel {
	WildcardList _ignoredPaths, _ignoredPaths2;
	_FileWatchers _syncWatchers;
	
	bool _IsPathIgnored(RStr itemPath, bool orAncestorDir = false) {
		_ignoredPaths ??= new(WSSett.syncfs_skip);
		if (_ignoredPaths.IsMatch(itemPath, true)) return true;
		if (orAncestorDir) {
			_ignoredPaths2 ??= new(_ignoredPaths.ListOfStrings.Where(o => !o.Ends('*')).Select(o => o + @"\*").ToArray());
			if (_ignoredPaths2.IsMatch(itemPath, true)) return true;
		}
		return false;
	}
	
	//CONSIDER: call when workspace loaded, even if LA hidden. Now does not sync until shown (_syncWatchers too). But is it important?
	/// <summary>
	/// Called when: 1. WorkspaceLoadedWithUI; 2. WSSett.syncfs_skip changed in Options.
	/// </summary>
	internal async void SyncWithFilesystem_() {
		_ignoredPaths = new(WSSett.syncfs_skip);
		_ignoredPaths2 = null;
		
		//get the root and all folder links
		_RootDir[] rootDirs = Root.Descendants(andSelf: true)
			.Where(o => o.Id == 0 || (o.IsLink && o.IsFolder && filesystem.exists(o.FilePath).Directory))
			.Select(o => new _RootDir(o, o.ItemPath, o.FilePath))
			.ToArray();
		
		//gather all filesystem files and dirs. Can be slow. Async to avoid blocking the main thread.
		await Task.Run(() => {
			foreach (var rd in rootDirs) {
				RelativePath rel = new(rd.itemPath);
				rd.tree = _Dir(new(pathname.expand(rd.fullPath)));
				
				_DirTree _Dir(DirectoryInfo parent) {
					_DirTree t = new(parent.Name) { children = new() };
					try {
						foreach (var v in parent.EnumerateFileSystemInfos()) {
							if (v.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) continue;
							var relPath = rel.GetRelativePath(v.FullName);
							if (_IsPathIgnored(relPath)) continue;
							
							if (v is DirectoryInfo di) {
								t.children.Add(_Dir(di));
							} else {
								t.children.Add(v.Name);
							}
						}
					}
					catch (Exception ex) { //eg symlink target dir does not exist
						t.children = null; //don't remove descendant filenodes, because the symlink target may be restored later
						Debug_.Print(ex);
					}
					return t;
				}
			}
		});
		Debug.Assert(App.IsMainThread);
		if (App.Model != this) return;
		
		//get added and deleted files
		foreach (var rd in rootDirs) {
			_DirItemComparer comparer = new();
			List<Dictionary<object, _DirTree>> adict = new();
			_Dir(rd.fn, rd.tree, 0);
			void _Dir(FileNode parentNode, _DirTree parentTree, int level) {
				if (parentTree.children == null) return; //failed to enum this dir; don't remove filenodes
				
				if (level == adict.Count) adict.Add(new(comparer));
				Dictionary<object, _DirTree> dict = adict[level];
				dict.Clear();
				
				foreach (var v in parentTree.children) {
					dict.Add(v, v as _DirTree);
				}
				
				foreach (var f in parentNode.Children()) {
					if (f.IsLink) continue;
					if (!dict.Remove(f.Name, out var t)) {
						(rd.delete ??= new()).Add(f);
					} else if (f.IsFolder != (t != null)) {
						(rd.delete ??= new()).Add(f);
						dict.Add((object)t ?? f.Name, t);
					} else if (f.IsFolder) {
						_Dir(f, t, level + 1);
					}
				}
				
				if (dict.Count > 0) (rd.add ??= new()).Add((parentNode, dict.Keys.ToArray()));
			}
		}
		
		//update filenodes
		StringBuilder bAdd = null;
		bool deleted = false;
		foreach (var rd in rootDirs) {
			//print.it(rd.fn, rd.delete?.Select(o => o.ItemPath), rd.add?.Select(o => (o.parent.ItemPath, "[" + print.util.toString(o.files, compact: true) + "]")));
			if (rd.delete != null) {
				foreach (var f in rd.delete) {
					if (f.IsDeleted) continue; //was in a now deleted folder
					//bDel ??= new("Missing or <+options Workspace>excluded<> files were removed from the <b>Files<> panel:"); //probably not useful info. Just distracts and scares people.
					//bDel.Append("\r\n  ").Append(f.ItemPath);
					//if (f.IsFolder) bDel.Append("  (folder)");
					deleted = true;
					_Delete(f, syncing: 1);
				}
			}
			if (rd.add != null) {
				foreach (var v in rd.add) {
					_AddDirItems(v.parent, v.files, 0);
				}
			}
			//rejected: if renamed or moved inside workspace, preserve file id etc. If folder, preserve the order of items.
			//	Cannot make it reliable. It would be too dirty/heavy. Possible side effects. Rarely used/useful.
		}
		
		if (bAdd != null || deleted) {
			UpdateControlItems();
			Save.WorkspaceAsync();
			CodeInfo.FilesChanged();
			if (bAdd != null) {
				bAdd.Append("\r\nYou may want to review added files and: change the order; change some properties; delete unused files; hide unused files (Options > Workspace).<>");
				print.it(bAdd);
			}
		}
		
		_syncWatchers ??= new(this);
		
		void _AddDirItems(FileNode parent, IEnumerable<object> files, int indent) {
			var parentPath = parent.FilePath + "\\";
			
			foreach (var v in files) {
				var name = v.ToString();
				var f = new FileNode(this, name, parentPath + name, v is _DirTree);
				parent.AddChild(f, first: parent == parent.Root);
				bAdd ??= new("<><lc #FFFFB9>New files were added:");
				bAdd.AppendLine().Append(' ', indent * 2).Append("  ").Append(f.SciLink(true));
				if (f.IsFolder) bAdd.Append("  (folder)");
				
				if (v is _DirTree dt) _AddDirItems(f, dt.children, indent + 1);
			}
		}
	}
}

file record class _RootDir(FileNode fn, string itemPath, string fullPath) {
	public _DirTree tree;
	public List<FileNode> delete;
	public List<(FileNode parent, object[] files)> add;
}

file record class _DirTree(string name) {
	public List<object> children;
	public override string ToString() => name;
}

file class _DirItemComparer : IEqualityComparer<object> {
	bool IEqualityComparer<object>.Equals(object x, object y) {
		return x.ToString().Eqi(y.ToString());
	}
	
	int IEqualityComparer<object>.GetHashCode(object obj) {
		return obj.ToString().GetHashCode();
	}
}

partial class FilesModel {
	class _FileWatchers {
		class _Watcher : FileSystemWatcher {
			public _Watcher(string path, FileNode fn) : base(path) {
				FN = fn;
				SetFolderItemPathBS();
			}
			public FileNode FN { get; }
			public string FolderItemPathBS { get; private set; }
			
			public void SetFolderItemPathBS() {
				if (FN is { IsFolder: true } f) FolderItemPathBS = f == f.Root ? "\\" : f.ItemPath + "\\";
			}
		}
		
		readonly FilesModel _model;
		readonly Dictionary<string, _Watcher> _dDir;
		
		public _FileWatchers(FilesModel model) {
			_model = model;
			_dDir = new(StringComparer.OrdinalIgnoreCase);
			_guid = Guid.NewGuid().ToString("N");
			_syncFilePath = _model.FilesDirectory + "\\" + _guid + ".~!~";
			
			//_Add(null, _model.WorkspaceDirectory);
			_Add(_model.Root, _model.FilesDirectory);
			foreach (var f in _model.Root.Descendants()) {
				if (f.IsLink && f.IsFolder) _Add(f, f.LinkTarget);
			}
		}
		
		public void Dispose() {
			foreach (var v in _dDir.Values) v.Dispose();
			_dDir.Clear();
		}
		
		void _Add(FileNode fn, string path) {
			if (_dDir.ContainsKey(path)) {
				Debug_.Print(path);
				return;
			}
			if (!filesystem.exists(path).Directory) return;
			
			_Watcher fw = null;
			try {
				//print.it("add", path);
				fw = new _Watcher(path, fn);
				//if (fn == null) { //workspace dir
				//	fw.Filters.Add("files.xml");
				//	fw.Filters.Add("settings.json");
				//} else {
				fw.IncludeSubdirectories = true;
				if (fn == fn.Root) fw.InternalBufferSize = 64 * 1024;
				//}
				
				fw.Created += _EventInTpThread;
				fw.Deleted += _EventInTpThread;
				fw.Renamed += _EventInTpThread;
#if USE_CHANGED_EVENTS
				fw.Changed += _Event;
#else
				//if (fn == null) fw.Changed += _EventInTpThread;
#endif
#if DEBUG
				fw.Error += static (o, e) => { Debug_.Print(e.GetException()); }; //never noticed. Never mind, will update at the next startup.
#endif
				fw.EnableRaisingEvents = true;
				_dDir[path] = fw;
			}
			catch (Exception ex) {
				Debug_.Print(ex);
				fw?.Dispose();
			}
		}
		
		public void Add(FileNode f) {
			_Add(f, f.LinkTarget);
		}
		
		public void Remove(FileNode f) {
			string path = f.LinkTarget;
			//print.it("remove", path);
			if (_dDir.Remove(path, out var f1)) f1.Dispose();
			else Debug_.Print(path);
		}
		
		public void UpdatePaths() {
			foreach (var v in _dDir.Values) v.SetFolderItemPathBS();
		}
		
		public void FileOperationStarted(FOSync fos) {
			if (fos != FOSync.UserFileOp) return;
			//print.it("started", fos);
			_CreateSyncFile();
		}
		
		public void FileOperationEnded(FOSync fos) {
			if (fos != FOSync.FilesXml) return;
			//print.it("ended", fos);
			_CreateSyncFile();
			_hSyncFile.Dispose();
		}
		
		void _CreateSyncFile() {
			if (_hSyncFile.Is0) {
				_hSyncFile = Api.CreateFile(_syncFilePath, Api.GENERIC_WRITE, Api.FILE_SHARE_DELETE | Api.FILE_SHARE_READ, Api.CREATE_NEW, Api.FILE_ATTRIBUTE_TEMPORARY | Api.FILE_FLAG_DELETE_ON_CLOSE | Api.FILE_ATTRIBUTE_HIDDEN | Api.FILE_ATTRIBUTE_SYSTEM);
			}
		}
		
		readonly string _guid, _syncFilePath;
		Handle_ _hSyncFile;
		volatile int _paused;
		
		void _EventInTpThread(object sender, FileSystemEventArgs e) {
			var fw = sender as _Watcher;
			if (e.Name[^1] == '~') { //atomic save (LA, VS), Rider temp backup, or a sync file
				if (e.Name.Ends(".~!~") && e.Name.Length == _guid.Length + 4 && e.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Deleted) { //a sync file of this or another LA process
					if (e.ChangeType is WatcherChangeTypes.Created) {
						Interlocked.Increment(ref _paused);
					} else {
						int paused = Interlocked.Decrement(ref _paused);
						if (paused == 0) {
							if (e.Name.Starts(_guid)) {
								//print.it("own");
							} else {
								App.Dispatcher.InvokeAsync(_SyncFromAnotherLaProcess);
							}
						} else if (paused < 0) { //unlikely but possible. We did not receive the 'created' event because either: 1. Another LA process created its sync file before we started watching; 2. After a watcher error.
							_paused = 0;
						}
					}
				}
				return;
			}
			if (_paused > 0) return;
			if (e.Name.Ends(true, [".TMP", ".bak"]) > 0) return; //VS atomic save, etc
#if !USE_CHANGED_EVENTS
			if (e.ChangeType == WatcherChangeTypes.Renamed && ((RenamedEventArgs)e).OldName.Ends('~') && fw.FN != null) return; //atomic save
#endif
			//note: we ignore renaming from/to a filtered filename. Eg if renamed `file.txt` -> `file.bak` or `bind` -> `bin`.
			//	It could conflict with the atomic save detection. Rare. Will correct at the next startup.
			
			App.Dispatcher.InvokeAsync(() => _EventInMainThread(fw, e));
		}
		
		void _SyncFromAnotherLaProcess() {
			if (App.Model != _model) return;
			_timer?.Stop();
			_ae.Clear();
			_model._SyncFromAnotherLaProcess();
		}
		
		void _EventInMainThread(_Watcher fw, FileSystemEventArgs e) {
			if (App.Model != _model) return;
			
			string itemPath = null;
			var fnRootOrLinkFolder = fw.FN;
			if (fnRootOrLinkFolder != null) {
				if (fnRootOrLinkFolder.IsDeleted) return;
#if USE_CHANGED_EVENTS
				if (e.ChangeType == WatcherChangeTypes.Changed && filesystem.exists(e.FullPath, true).Directory) return;
#endif
				itemPath = fw.FolderItemPathBS + e.Name;
				if (_model._IsPathIgnored(itemPath, true)) return;
			}
			
			//print.it($"<><c green>{e.ChangeType}, {e.Name}, {(e as RenamedEventArgs)?.OldName}<>");
			
			//delay to filter out unusual replace-file sequences etc or join multiple events when copying a folder (and in some cases moving or deleting)
			_ae.Add(new(fw, e, itemPath)); //queue events. Without it, eg on copy folder, the printed items are not all and in random order, although imports without errors.
			_timer ??= new(_Timer);
			_timer.After(250);
		}
		
		timer _timer;
		List<_EventInfo> _ae = [];
		
		record class _EventInfo(_Watcher fw, FileSystemEventArgs e, string itemPath);
		
		void _Timer(timer t_) {
			if (App.Model != _model) return;
			foreach (var x in _ae) {
				if (x.fw.FN != null) {
					switch (x.e.ChangeType) {
					case WatcherChangeTypes.Renamed:
						if (x.e is RenamedEventArgs re) {
							var itemPathOld = x.fw.FolderItemPathBS + re.OldName;
							if (_model.FindByItemPath(itemPathOld) is { } fr && _model.FindByItemPath(x.itemPath) is null && !filesystem.exists(re.OldFullPath, true) && filesystem.exists(re.FullPath, true)) {
								fr.FileRename(pathname.getName(re.Name), syncing: true);
								print.it($"<><lc #FFFFB9>Renamed: {fr.SciLink(true)} (was {pathname.getName(re.OldName)})<>");
							}
						}
						break;
					case WatcherChangeTypes.Deleted:
						if (_model.FindByItemPath(x.itemPath) is { } fd && !filesystem.exists(x.e.FullPath, true)) {
							print.it($"<><lc #FFFFB9>Deleted: {fd.ItemPath}{(fd.IsFolder ? "  (folder)" : null)}<>");
							_model._Delete(fd, syncing: 2);
						}
						break;
					case WatcherChangeTypes.Created:
						if (_model._SyncImportFromWorkspaceDir(x.e.FullPath, x.itemPath) is { } fa) {
							foreach (var v in fa.Descendants(true)) print.it($"<><lc #FFFFB9>Added: {v.SciLink(true)}{(v.IsFolder ? "  (folder)" : null)}<>");
						}
						break;
					}
					//} else if (x.e.ChangeType != WatcherChangeTypes.Deleted) {
					//	print.it(x.e.Name);
				}
			}
			_ae.Clear();
		}
	}
	
	/// <summary>
	/// Called by a filesystem watcher when files.xml modified by another LA process.
	/// Normally it happens when using PiP (child session). One LA process runs in main session, and other in child session. Same user, same workspace.
	/// </summary>
	void _SyncFromAnotherLaProcess() {
		XElement newRoot;
		try {
			newRoot = filesystem.waitIfLocked(() => XElement.Load(WorkspaceFile)); //the slowest part
		}
		catch (Exception ex) { Debug_.Print(ex); return; }
		
		Save.PauseSaveWorkspace = true; //nothing here tries to save, but would be bad if somehow it would happen without this protection. Also it cancels async save.
		try {
			_SyncFromAnotherLaProcess(newRoot);
		}
		catch (Exception ex) {
			dialog.showError("Failed to sync workspace", ex.ToString(), owner: App.Hmain);
			Environment.Exit(1);
		}
		Save.PauseSaveWorkspace = false;
	}
	
	void _SyncFromAnotherLaProcess(XElement newRoot) {
		//Algorithm: remove all our filenodes from their parents, and then re-add like in the new XML.
		//Then easy to find added, deleted, moved and changed nodes.
		
		var aOld = Root.Descendants().ToArray();
		var dOld = aOld.ToDictionary(f => f.Id);
		uint[] aOldParent = new uint[aOld.Length]; for (int i = 0; i < aOld.Length; i++) aOldParent[i] = aOld[i].Parent.Id;
		List<FileNode> aAdded = [];
		
		foreach (var f in aOld) f.Remove();
		
		_Folder(Root, newRoot);
		Debug.Assert(Root.Descendants().Count() == newRoot.Descendants().Count());
		void _Folder(FileNode oldFolder, XElement newFolder) {
			foreach (var x in newFolder.Elements()) {
				if (!x.Attribute("i").Value.ToInt(out uint id)) throw new ArgumentException();
				if (!dOld.Remove(id, out var f)) {
					f = new(x, this);
					_idMap.Add(id, f); //exception if duplicate
					aAdded.Add(f);
				}
				oldFolder.AddChild(f);
				if (x.HasElements) _Folder(f, x);
			}
		}
		
		//DELETED
		foreach (var f in dOld.Values) {
			//print.it($"Deleted {f.Name}");
			if (!f.IsDeleted) _Delete(f, syncing: 1);
		}
		
		//ADDED
		//foreach (var f in aAdded) {
		//	print.it($"Added {f}");
		//}
		
		//CHANGED ATTRIBUTES OR TAG
		using (var oldEnum = Root.Descendants().GetEnumerator())
		using (var newEnum = newRoot.Descendants().GetEnumerator()) {
			while (oldEnum.MoveNext() && newEnum.MoveNext()) {
				var f = oldEnum.Current;
				Debug.Assert(newEnum.Current.Attribute("i").Value.ToInt(out uint u) && u == f.Id);
				if (aAdded.Contains(f)) continue;
				f.SyncAttributes(newEnum.Current);
			}
		}
		
		//CHANGED PARENT
		for (int i = 0; i < aOld.Length; i++) {
			var f = aOld[i];
			if (f.IsDeleted) continue;
			if (f.Parent.Id != aOldParent[i]) {
				//print.it($"Moved {f.Name}, {f.Descendants():print}");
				Au.Compiler.Compiler.Uncache(f, andDescendants: true);
			}
		}
		
		ChangedFolderItemPath_();
		UpdateControlItems();
		CodeInfo.FilesChanged();
	}
}

partial class FileNode {
	internal void SyncAttributes(XElement x) {
		var type = XmlTagToFileType(x.Name.LocalName, canThrow: true);
		if (type != _type) {
			if (!(IsCodeFile && type is FNType.Script or FNType.Class)) throw new ArgumentException();
			_type = type;
			_testScriptId = 0;
			Au.Compiler.Compiler.Uncache(this);
		}
		
		string icon = null;
		uint testScriptId = 0;
		//_Flags flags = 0;
		foreach (var attr in x.Attributes()) {
			string name = attr.Name.LocalName, value = attr.Value;
			if (value.NE()) continue;
			switch (name) {
			//case "i": break;
			//case "path": break; //never changes
			//case "f": flags = (_Flags)value.ToInt(); break; //currently there are no flags
			case "n" when value != _name:
				_SetName(value);
				Au.Compiler.Compiler.Uncache(this, andDescendants: true);
				break;
			case "icon": icon = value; break;
			case "run": value.ToInt(out testScriptId); break;
			}
		}
		
		//if (flags != _flags) {
		//	_flags = flags;
		//}
		
		if (icon != _icon) {
			_icon = icon;
			Au.Compiler.Compiler.Uncache(this);
		}
		
		if (testScriptId != _testScriptId) {
			_testScriptId = testScriptId;
		}
	}
}

enum FOSync { UserFileOp, UserFileWrite, FilesXml, PrivateFileWrite }
