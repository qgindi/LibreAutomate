using System.Windows.Controls;
using Au.Controls;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Windows.Input;

class PanelBookmarks {
	KTreeView _tv;
	_Item _root;
	string _file;
	
	bool _initOnce;
	int _save;
	
	public PanelBookmarks() {
		_tv = new() { Name = "Bookmarks_list", SingleClickActivate = true };
		P.Children.Add(_tv);
		
		FilesModel.AnyWorkspaceLoadedAndDocumentsOpened += _LoadIfNeed;
	}
	
	public DockPanel P { get; } = new();
	
	void _LoadIfNeed() {
		if (_root != null) return;
		_root = new(null, true);
		
		_file = App.Model.WorkspaceDirectory + @"\bookmarks.csv";
		if (filesystem.exists(_file).File) {
			try {
				var csv = csvTable.load(_file);
				_Item folder = null;
				foreach (var a in csv.Rows) {
					if (a[0][0] == '#') {
						if (a[0].ToInt(out uint u, 1) && App.Model.FindById(u) is { } f) {
							int flags = a[1].ToInt();
							_root.AddChild(folder = new(f, 0 != (flags & 1)));
						} else {
							folder = null;
							_SaveLater(1);
						}
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
				if (_root == null) return;
				SaveNowIfNeed();
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
					_SetActive(b, true);
					App.Model.OpenAndGoTo(b.Parent.file, b.line);
				}
			};
			
			_tv.ItemClick += e => {
				var b = e.Item as _Item;
				switch (e.Button) {
				case System.Windows.Input.MouseButton.Left:
					switch (e.Mod) {
					case System.Windows.Input.ModifierKeys.Control: _DeleteItem(b); break;
					case System.Windows.Input.ModifierKeys.Shift when !b.IsFolder: _tv.EditLabel(b); break;
					}
					break;
				case System.Windows.Input.MouseButton.Right when e.Mod == 0:
					_ContextMenu(b);
					break;
				case System.Windows.Input.MouseButton.Middle when e.Mod == 0:
					_SetActive(b, !b.IsActiveOrHasActiveChildren);
					break;
				}
			};
			
			_tv.PreviewKeyDown += (_, e) => {
				if (e.Key is Key.Up or Key.Down && e.KeyboardDevice.Modifiers == ModifierKeys.Shift && _tv.FocusedItem is _Item { IsFolder: true } f) {
					e.Handled = true;
					_MoveFolder(f, e.Key is Key.Up);
				}
			};
			
			_tv.EditLabelStarted += (item, tb) => {
				tb.Text = (item as _Item).name; //edit without line number
				tb.SelectAll();
			};
			
			Panels.Editor.ClosingDoc += doc => {
				if (_FindItemOfFile(doc) is { } f) {
					foreach (var b in f.Children()) _SetActive(b, false);
				}
			};
			
			App.Timer1sWhenVisible += () => { if (_save != 0 && --_save <= 0) _SaveNow(); };
		}
	}
	
	public void SaveNowIfNeed() {
		if (_save != 0) _SaveNow();
	}
	
	void _SaveNow() {
		try {
			if (_root.HasChildren) {
				var csv = new csvTable { ColumnCount = 2 };
				for (var f = _root.FirstChild; f != null; f = f.Next) {
					csv[^0, 0] = "#" + f.file.IdString;
					if (f.IsExpanded) csv[^1, 1] = "1";
					for (var b = f.FirstChild; b != null; b = b.Next) {
						csv.Set(^0, 0, b.line);
						csv[^1, 1] = b.name;
					}
				}
				csv.Save(_file);
			} else {
				filesystem.saveText(_file, "");
			}
			_save = 0;
			//print.it("saved");
		}
		catch (Exception e1) { print.it(e1); }
	}
	
	void _SaveLater(int afterS = 30) {
		//print.it(new StackTrace(true));
		_save = _save == 0 ? afterS : Math.Min(_save, afterS);
	}
	
	internal void SciLoaded(SciCode doc) {
		_LoadIfNeed();
		var f = _FindItemOfFile(doc); if (f == null) return;
		bool removed = false;
		for (var b = f.FirstChild; b != null;) {
			int h = doc.aaaMarkerAdd(SciCode.c_markerBookmark, b.line);
			if (h == -1) {
				var bb = b; b = b.Next;
				bb.Remove();
				removed = true;
				_SaveLater(1);
				continue;
			}
			b.markerHandle = h;
			if (!b.isActive) doc.aaaMarkerAdd(SciCode.c_markerBookmarkInactive, b.line);
			b = b.Next;
		}
		if (removed) _tv.SetItems(_root.Children(), true);
	}
	
	//_Item _FindItemOfFile(FileNode fn) => _root.Children().FirstOrDefault(o => o.file == fn); //slow, garbage
	_Item _FindItemOfFile(FileNode fn) {
		if (fn != null)
			for (var f = _root.FirstChild; f != null; f = f.Next)
				if (f.file == fn) return f;
		return null;
	}
	_Item _FindItemOfFile(SciCode doc) => _FindItemOfFile(doc?.EFile);
	
	public void ToggleBookmark(bool editLabel, int line = -1) {
		var doc = Panels.Editor.ActiveDoc;
		if (doc == null) return;
		bool useSelStart = line < 0;
		if (line < 0) line = doc.aaaLineFromPos();
		if (_IsBookmark(doc, line)) {
			if (!editLabel) _DeleteBookmark(doc, line);
			else if (_BookmarkFromLine(doc, line) is { } b) _tv.EditLabel(b);
		} else {
			if (!useSelStart) if (doc.aaaLineFromPos() == line) useSelStart = true; else doc.aaaGoToLine(line);
			
			int h = doc.aaaMarkerAdd(SciCode.c_markerBookmark, line); if (h < 0) return;
			var folder = _FindItemOfFile(doc);
			if (folder == null) {
				_root.AddChild(folder = new(doc.EFile, true), true);
			} else folder.SetIsExpanded(true);
			
			var name = _GetName();
			_Item b = new(line, h, name) { isActive = true };
			if (folder.Children().FirstOrDefault(o => o.line > line) is { } b2) b2.AddSibling(b, false);
			else folder.AddChild(b);
			
			_tv.SetItems(_root.Children(), true);
			_TvSelect(b);
			if (editLabel) _tv.EditLabel(b);
			_SaveLater();
		}
		
		string _GetName() {
			if (CodeInfo.GetDocumentAndFindNode(out var cd, out var node, useSelStart ? -2 : doc.aaaLineStart(true, line))) {
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
	
	public void NextBookmark(bool up) {
		//If there are no active bookmarks, go to next/prev bookmark in this file.
		//Else if there is an active bookmark below or above (depending on *up*) current line in this file, go to it.
		//Else go to next/prev active bookmark in any file. Start searching from (not including):
		//	If this file contains bookmarks - from next/prev file in _root.Children.
		//	Else from the focused bookmark in _tv.
		
		if (!_root.HasChildren) return;
		
		_Item go = null;
		var doc = Panels.Editor.ActiveDoc;
		var file = _FindItemOfFile(doc);
		
		if (_root.Descendants().Any(static o => o.isActive)) {
			if (file != null) {
				var (prev, next) = _GetPrevNextInDoc(true);
				go = up ? prev : next;
			}
			if (go == null) {
				var a = _root.Descendants().ToArray();
				var from = file != null ? (up ? file.FirstChild : file.LastChild) : _tv.FocusedItem as _Item;
				from ??= up ? a[^1] : a[0];
				int i = Array.IndexOf(a, from);
				for (; ; ) {
					if (up) i = (i > 0 ? i : a.Length) - 1; else i = ++i < a.Length ? i : 0;
					if (a[i].isActive) { go = a[i]; break; }
				}
			}
		} else if (file != null) {
			var (prev, next) = _GetPrevNextInDoc(false);
			if (prev == null && next == null) return;
			go = up ? prev ?? file.LastChild : next ?? file.FirstChild;
		}
		
		if (go != null) {
			App.Model.OpenAndGoTo(go.Parent.file, go.line);
			_TvSelect(go);
		}
		
		(_Item prev, _Item next) _GetPrevNextInDoc(bool active) {
			int line = doc.aaaLineFromPos();
			_Item prev = null;
			for (var v = file.FirstChild; v != null; v = v.Next) {
				if (active && !v.isActive) continue;
				if (v.line < line) prev = v; else if (v.line > line) return (prev, v);
			}
			return (prev, null);
		}
	}
	
	void _SetActive(_Item b, bool active) {
		if (b.IsFolder) {
			foreach (var v in b.Children()) _SetActive(v, active);
		} else if (b.isActive != active) {
			b.isActive = active;
			_tv.Redraw(b);
			
			if (b.Parent.file.OpenDoc is {  } doc) {
				doc.Call(active ? Sci.SCI_MARKERDELETE : Sci.SCI_MARKERADD, b.line, SciCode.c_markerBookmarkInactive);
			}
		}
	}
	
	void _TvSelect(_Item b) {
		if (!b.IsFolder && !b.Parent.IsExpanded) _tv.Expand(b.Parent, true);
		_tv.SelectSingle(b, true);
	}
	
	void _ContextMenu(_Item b) {
		_TvSelect(b);
		var m = new popupMenu();
		
		if (b.IsFolder) {
			m["Move up\tShift+Up", disable: b.Previous == null] = o => _MoveFolder(b, true);
			m["Move down\tShift+Down", disable: b.Next == null] = o => _MoveFolder(b, false);
			m.Separator();
			m["Delete these bookmarks\tCtrl+click"] = o => _DeleteItem(b);
			m.AddCheck("Active bookmarks\tM-click", b.IsActiveOrHasActiveChildren, o => _SetActive(b, o.IsChecked));
		} else {
			m["Rename\tShift+click"] = o => _tv.EditLabel(b);
			m["Delete\tCtrl+click"] = o => _DeleteItem(b);
			m.AddCheck("Active\tM-click", b.isActive, o => _SetActive(b, o.IsChecked));
		}
		m.Separator();
		m["Deactivate all"] = o => { foreach (var v in _root.Children()) _SetActive(v, false); };
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
		_SaveLater();
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
	
	void _MoveFolder(_Item f, bool up) {
		if (up && f == _root.FirstChild) return;
		var ff = up ? f.Previous : f.Next;
		if (ff == null) return;
		f.Remove();
		ff.AddSibling(f, after: !up);
		_tv.SetItems(_root.Children(), true);
		_SaveLater();
	}
	
	internal void SciDeletingLineWithMarker(SciCode doc, int line) {
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
					_SaveLater(2 * 60);
				}
			}
			if (redraw && folder.IsExpanded) _tv.Redraw();
			//never mind: don't need to redraw if the Bookmarks panel isn't visible. It's fast, just invalidates.
		}
	}
	
	internal void SciContextMenu(SciCode doc) {
		var m = new popupMenu();
		m["Previous bookmark"] = o => NextBookmark(true);
		m["Next bookmark"] = o => NextBookmark(false);
		m.Separator();
		m["Add and/or rename bookmark\tShift+click"] = o => ToggleBookmark(true);
		if (this._BookmarkFromLine(doc, doc.aaaLineFromPos()) is { } b) m.AddCheck("Active bookmark\tM-click", b.isActive, o => _SetActive(b, o.IsChecked));
		m.Show(owner: doc);
	}
	
	internal void SciMiddleClick(SciCode doc, nint wParam, nint lParam) {
		if (wParam != Api.MK_MBUTTON) return;
		int pos = doc.aaaPosFromXY(false, Math2.NintToPOINT(lParam), false); if (pos < 0) return;
		int line = doc.aaaLineFromPos(false, pos);
		if (_BookmarkFromLine(doc, line) is {  } b) {
			_SetActive(b, !b.isActive);
		}
	}
	
	internal void FileDeleted(IEnumerable<FileNode> e) {
		foreach (var v in e) {
			if (_FindItemOfFile(v) is { } folder) {
				folder.Remove();
				_tv.SetItems(_root.Children(), true);
				_SaveLater();
			}
		}
	}
	
	class _Item : TreeBase<_Item>, ITreeViewItem {
		public readonly FileNode file; //if folder
		public int line, markerHandle; //if bookmark
		public string name;
		readonly bool _isFolder;
		bool _isExpanded;
		public bool isActive;
		
		public _Item(FileNode file, bool isExpanded) {
			this.file = file;
			_isFolder = true;
			_isExpanded = isExpanded;
		}
		
		public _Item(int line, int markerHandle, string name) {
			this.line = line;
			this.markerHandle = markerHandle;
			this.name = name;
		}
		
#if DEBUG
		public override string ToString() => ((ITreeViewItem)this).DisplayText;
#endif
		
		#region ITreeViewItem
		
		public bool IsFolder => _isFolder;
		
		public bool IsExpanded => _isExpanded;
		
		public void SetIsExpanded(bool yes) { _isExpanded = yes; Panels.Bookmarks._SaveLater(5 * 60); }
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		
		string ITreeViewItem.DisplayText => _isFolder ? (file.Parent.HasParent ? $"{file.Name}  [{file.Parent.ItemPath}]" : file.Name) : $"{(line + 1).ToS()}  {name}";
		//never mind: after moving the file should display the new path (just redraw).
		
		void ITreeViewItem.SetNewText(string text) { name = text; Panels.Bookmarks._SaveLater(); }
		
		object ITreeViewItem.Image => _isFolder ? EdResources.FolderArrow(_isExpanded) : isActive ? "*BoxIcons.SolidBookmark" + Menus.blue : "*BoxIcons.SolidBookmark" + Menus.black;
		
		#endregion
		
		public bool IsActiveOrHasActiveChildren => IsFolder ? Children().Any(static o => o.isActive) : isActive;
	}
}