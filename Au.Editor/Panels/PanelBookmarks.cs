using System.Windows.Controls;
using Au.Controls;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

class PanelBookmarks {
	KTreeView _tv;
	_Item _root;
	bool _initOnce;
	
	public PanelBookmarks() {
		_tv = new KTreeView { Name = "Bookmarks_list", SingleClickActivate = true };
		P.Children.Add(_tv);
		
		FilesModel.AnyWorkspaceLoadedAndDocumentsOpened += _LoadIfNeed;
	}
	
	public DockPanel P { get; } = new();
	
	void _LoadIfNeed() {
		if (_root != null) return;
		_root = new(null, true);
		var s = App.Model.WorkspaceDirectory + @"\bookmarks.csv";
		if (filesystem.exists(s).File) {
			try {
				var csv = csvTable.load(s);
				_Item folder = null;
				foreach (var a in csv.Rows) {
					if (a[0][0] == '#') {
						if (a[0].ToInt(out uint u, 1) && App.Model.FindById(u) is { } f) {
							int flags = a[1].ToInt();
							_root.AddChild(folder = new(f, 0 != (flags & 1)));
						} else folder = null;
					} else if (folder != null) {
						folder.AddChild(new(a[0].ToInt(), 0, a[1]));
					}
				}
			}
			catch (Exception e1) { print.it(e1); }
		}
		_tv.SetItems(_root.Children());
		
		if (!_initOnce) {
			_initOnce = true;
			
			FilesModel.UnloadingAnyWorkspace += () => {
				_root = null;
				_tv.SetItems(null);
			};
			
			FilesModel.NeedRedraw += v => {
				if (!v.renamed) return;
				var f = _FindItemOfFile(v.f);
				if (f != null) _tv.Redraw(f, v.remeasure);
			};
			
			_tv.ItemActivated += e => {
				if (e.Mod != 0) return;
				if (e.Item is _Item b && !b.IsFolder) {
					App.Model.OpenAndGoTo(b.Parent.file, b.line);
				}
			};
			
			_tv.ItemClick += e => {
				switch (e.Button) {
				case System.Windows.Input.MouseButton.Left:
					var b = e.Item as _Item;
					switch (e.Mod) {
					case System.Windows.Input.ModifierKeys.Control: _DeleteItem(b); break;
					case System.Windows.Input.ModifierKeys.Shift when !b.IsFolder: _tv.EditLabel(b); break;
					}
					break;
				}
				if (e.Button == System.Windows.Input.MouseButton.Right && e.Mod == 0) _ContextMenu(e.Item as _Item);
			};
			
			//Panels.Editor.ActiveDocChanged += _ActiveDocChanged;
		}
	}
	
	internal void DocLoaded(SciCode doc) {
		_LoadIfNeed();
		var f = _FindItemOfFile(doc); if (f == null) return;
		bool removed = false;
		for (var b = f.FirstChild; b != null;) {
			int h = doc.aaaMarkerAdd(SciCode.c_markerBookmark, b.line);
			if (h == -1) {
				var bb = b; b = b.Next;
				bb.Remove();
				removed = true;
				continue;
			}
			b.markerHandle = h;
			b = b.Next;
		}
		if (removed) _tv.SetItems(_root.Children(), true);
	}
	
	//_Item _FindItemOfFile(FileNode fn) => _root.Children().FirstOrDefault(o => o.file == fn); //slow, garbage
	_Item _FindItemOfFile(FileNode fn) {
		for (var f = _root.FirstChild; f != null; f = f.Next) if (f.file == fn) return f;
		return null;
	}
	_Item _FindItemOfFile(SciCode doc) => _FindItemOfFile(doc.EFile);
	
	public void ToggleBookmark(bool editLabel) {
		var doc = Panels.Editor.ActiveDoc;
		if (doc == null) return;
		int line = doc.aaaLineFromPos();
		if (_IsBookmark(doc, line)) {
			if (!editLabel) _DeleteBookmark(doc, line);
			else if (_BookmarkFromLine(doc, line) is { } b) _tv.EditLabel(b);
		} else {
			int h = doc.aaaMarkerAdd(SciCode.c_markerBookmark, line); if (h < 0) return;
			var folder = _FindItemOfFile(doc);
			if (folder == null) {
				_root.AddChild(folder = new(doc.EFile, true));
			} else folder.SetIsExpanded(true);
			
			var name = _GetName();
			_Item b = new(line, h, name);
			if (folder.Children().FirstOrDefault(o => o.line > line) is { } b2) b2.AddSibling(b, false);
			else folder.AddChild(b);
			
			_tv.SetItems(_root.Children(), true);
			_tv.SelectSingle(b, true);
			if (editLabel) _tv.EditLabel(b);
		}
		
		string _GetName() {
			if (CodeInfo.GetDocumentAndFindNode(out var cd, out var node)) {
				var code = cd.code;
				if (_Node(node) is string s1) return s1 + "  " + _Line();
				return _Line();
				
				string _Line() => "● " + doc.aaaLineText(line).Trim().Limit(50);
				
				string _Node(SyntaxNode node) {
					for (; node != null; node = node.Parent) {
						if (node is LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or EventFieldDeclarationSyntax) {
							switch (node) {
							case LocalFunctionStatementSyntax k:
								string s1 = k.Identifier.Text + "()", s2 = _Node(k.Parent);
								return s2 == null ? s1 : $"{s1} in {s2}";
							case MethodDeclarationSyntax k:
								return k.Identifier.Text + "()";
							case ConstructorDeclarationSyntax k:
								return k.Identifier.Text + "()";
							case DestructorDeclarationSyntax k:
								return "~" + k.Identifier.Text + "()";
							case OperatorDeclarationSyntax k:
								return "operator " + k.OperatorToken.Text;
							case ConversionOperatorDeclarationSyntax k:
								return "operator " + k.Type;
							case PropertyDeclarationSyntax k:
								return k.Identifier.Text;
							case IndexerDeclarationSyntax k:
								return "this[]";
							case EventDeclarationSyntax k:
								return "event " + k.Identifier.Text;
							case EventFieldDeclarationSyntax k:
								return "event " + k.Declaration.Variables;
							}
							break;
						}
					}
					return null;
				}
			}
			return "Bookmark";
		}
	}
	
	public void NextBookmark() {
		var doc = Panels.Editor.ActiveDoc;
		if (doc == null) return;
		int line = doc.aaaLineFromPos();
		line = doc.Call(Sci.SCI_MARKERNEXT, line + 1, 1 << SciCode.c_markerBookmark);
		if (line < 0) {
			line = doc.Call(Sci.SCI_MARKERNEXT, 0, 1 << SciCode.c_markerBookmark);
			if (line < 0) return;
		}
		doc.aaaGoToLine(line);
	}
	
	void _ContextMenu(_Item b) {
		_tv.SelectSingle(b, true);
		var m = new popupMenu();
		
		if (b.IsFolder) {
			m["Delete bookmarks in this file\tCtrl+click"] = o => _DeleteItem(b);
		} else {
			m["Rename\tShift+click"] = o => _tv.EditLabel(b);
			m["Delete\tCtrl+click"] = o => _DeleteItem(b);
		}
		m.Separator();
		m["Collapse all"] = o => { foreach (var v in _root.Children()) _tv.Expand(v, false); };
		m.Submenu("More", m => {
			m["Delete all bookmarks..."] = o => {
				if (1 != dialog.show("Delete all bookmarks", "Are you sure?", "2 No|1 Yes", icon: DIcon.Warning)) return;
				foreach (var v in _root.Children().ToArray()) _DeleteItem(v);
			};
		});
		
		m.Show(owner: _tv);
	}
	
	bool _IsBookmark(SciCode doc, int line) => (doc.Call(Sci.SCI_MARKERGET, line) & 1 << SciCode.c_markerBookmark) != 0;
	
	_Item _BookmarkFromLine(SciCode doc, int line) {
		if (_FindItemOfFile(doc) is { } folder) {
			for (int i = 0; ; i++) {
				int h = doc.Call(Sci.SCI_MARKERHANDLEFROMLINE, line, i);
				if (h < 0) return null;
				for (var b = folder.FirstChild; b != null; b = b.Next) if (b.markerHandle == h) return b;
			}
		}
		return null;
	}
	
	void _DeleteBookmark(SciCode doc, int line, bool sciDelete = true) {
		if (_BookmarkFromLine(doc, line) is { } b) {
			if (sciDelete) doc.Call(Sci.SCI_MARKERDELETEHANDLE, b.markerHandle);
			_DeleteBookmarkL(b);
		}
	}
	
	void _DeleteBookmarkL(_Item b) {
		var folder = b.Parent;
		b.Remove();
		if (!folder.HasChildren) folder.Remove();
		_tv.SetItems(_root.Children(), true);
	}
	
	void _DeleteItem(_Item b) {
		if (b.IsFolder) {
			foreach (var v in b.Children().ToArray()) _DeleteItem(v);
		} else {
			var folder = b.Parent;
			if (b.markerHandle != 0 && folder.file.OpenDoc is { } doc) {
				doc.Call(Sci.SCI_MARKERDELETEHANDLE, b.markerHandle);
			}
			_DeleteBookmarkL(b);
		}
	}
	
	internal void DeletingLineWithMarker(SciCode doc, int line) {
		_DeleteBookmark(doc, line, false);
	}
	
	internal void SciModified(SciCode doc, ref Sci.SCNotification n) {
		if (n.linesAdded != 0) {
			//update the line field, and display
			var folder = _FindItemOfFile(doc);
			if (folder == null) return;
			bool redraw = false;
			for (var b = folder.FirstChild; b != null; b = b.Next) {
				int line = doc.Call(Sci.SCI_MARKERLINEFROMHANDLE, b.markerHandle);
				if (line != b.line) {
					b.line = line;
					redraw = true;
				}
			}
			if (redraw && folder.IsExpanded) _tv.Redraw();
			//never mind: don't need to redraw if the Bookmarks panel isn't visible. It's fast, just invalidates.
		}
	}
	
	internal void OnFileDeleted(IEnumerable<FileNode> e) {
		foreach (var v in e) {
			if (_FindItemOfFile(v) is { } folder) {
				folder.Remove();
				_tv.SetItems(_root.Children(), true);
			}
		}
	}
	
	class _Item : TreeBase<_Item>, ITreeViewItem {
		public readonly FileNode file; //if folder
		public int line, markerHandle; //if bookmark
		string _name;
		bool _isFolder, _isExpanded;
		
		public _Item(FileNode file, bool isExpanded) {
			this.file = file;
			_isFolder = true;
			_isExpanded = isExpanded;
		}
		
		public _Item(int line, int markerHandle, string name) {
			this.line = line;
			this.markerHandle = markerHandle;
			_name = name;
		}
		
		#region ITreeViewItem
		
		public bool IsFolder => _isFolder;
		
		public bool IsExpanded => _isExpanded;
		
		public void SetIsExpanded(bool yes) { _isExpanded = yes; }
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		
		string ITreeViewItem.DisplayText => _isFolder ? (file.Parent.HasParent ? $"{file.Name}  [{file.Parent.ItemPath}]" : file.Name) : $"{(line + 1).ToS()}  {_name}";
		//never mind: after moving the file should display the new path (just redraw).
		
		object ITreeViewItem.Image => _isFolder ? EdResources.FolderArrow(_isExpanded) : "*Ionicons.BookmarkiOS" + Menus.darkBlue;
		
		void ITreeViewItem.SetNewText(string text) { _name = text; }
		
		//has default implementation
		//int ITreeViewItem.Color(TVColorInfo ci) {
		
		//	return default;
		//}
		
		////has default implementation
		//int ITreeViewItem.SelectedColor(TVColorInfo ci) {
		
		//	return default;
		//}
		
		////has default implementation
		//int ITreeViewItem.TextColor(TVColorInfo ci) {
		
		//	return default;
		//}
		
		////has default implementation
		//int ITreeViewItem.BorderColor(TVColorInfo ci) {
		
		//	return default;
		//}
		
		////has default implementation
		//int ITreeViewItem.MesureTextWidth(GdiTextRenderer tr) {
		
		//	return default;
		//}
		
		#endregion
	}
}
