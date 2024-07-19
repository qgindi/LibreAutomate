partial class FilesModel {
	WildcardList _syncFsSkip;
	string _syncFsSkipText;
	
	public async void SyncWithFilesystem() {
		if (_syncFsSkip is null || _syncFsSkipText != App.Model.WSSett.syncfs_skip) _syncFsSkip = new(_syncFsSkipText = App.Model.WSSett.syncfs_skip);
		
		//get the root and all folder links
		var model = App.Model;
		_RootDir[] rootDirs = model.Root.Descendants(andSelf: true)
			.Where(o => o.Id == 0 || (o.IsLink && o.IsFolder && filesystem.exists(o.FilePath).Directory))
			.Select(o => new _RootDir(o, o.ItemPath, o.FilePath))
			.ToArray();
		
		//gather all filesystem files and dirs. Can be slow. Async to avoid blocking the main thread.
		await Task.Run(() => {
			foreach (var rd in rootDirs) {
				//print.it(rd.itemPath, rd.fullPath);
				
				RelativePath rel = new(rd.itemPath);
				rd.tree = _Dir(new(rd.fullPath));
				
				_DirTree _Dir(DirectoryInfo parent) {
					_DirTree t = new(parent.Name) { children = new() };
					try {
						foreach (var v in parent.EnumerateFileSystemInfos()) {
							if (v.Attributes.Has(FileAttributes.Hidden | FileAttributes.System)) continue;
							var relPath = rel.GetRelativePath(v.FullName); //print.it(relPath.ToString());
							if (_syncFsSkip.IsMatch(relPath, true)) continue;
							
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
		if (App.Model != model) return;
		
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
		StringBuilder bDel = null, bAdd = null;
		foreach (var rd in rootDirs) {
			//print.it(rd.fn, rd.delete?.Select(o => o.ItemPath), rd.add?.Select(o => (o.parent.ItemPath, "[" + print.util.toString(o.files, compact: true) + "]")));
			if (rd.delete != null) {
				foreach (var f in rd.delete) {
					(bDel ??= new("\r\nDeleted:")).Append("\r\n\t").Append(f.IsFolder ? "Folder " : "").Append(f.ItemPath);
					_Delete(f, syncing: true);
				}
			}
			if (rd.add != null) {
				foreach (var v in rd.add) {
					_AddDirItems(v.parent, v.files, 1);
				}
			}
			//rejected: if renamed or moved inside workspace, preserve file id etc. If folder, preserve the order of items.
			//	Cannot make it reliable. It would be too dirty/heavy. Possible side effects. Rarely used/useful.
		}
		
		if (bDel != null || bAdd != null) {
			UpdateControlItems();
			Save.WorkspaceLater();
			CodeInfo.FilesChanged();
			print.it("<>Detected new, deleted or excluded files in the workspace. The Files panel now reflects the changes." + (bAdd is null ? null : "\r\nYou may want to review added files and: change the order; change some properties; delete unused files; hide unused files (Options > Workspace).") + bDel + bAdd);
		}
		
		void _AddDirItems(FileNode parent, IEnumerable<object> files, int indent) {
			var parentPath = parent.FilePath + "\\";
			
			foreach (var v in files) {
				var name = v.ToString();
				var f = new FileNode(this, name, parentPath + name, v is _DirTree);
				parent.AddChild(f);
				(bAdd ??= new("\r\nAdded:")).AppendLine().Append('\t', indent).Append(f.IsFolder ? "Folder " : "").Append(f.SciLink(true));
				
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
