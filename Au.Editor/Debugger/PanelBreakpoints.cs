using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Au.Controls;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Xml.Linq;

class PanelBreakpoints {
	KTreeView _tv;
	_Item _root;
	string _file;
	bool _initOnce;
	int _save;
	
	public PanelBreakpoints() {
		_tv = new() { Name = "Breakpoints_list", SingleClickActivate = true, HasCheckboxes = true, SmallIndent = true };
		P.Children.Add(_tv);
		
		FilesModel.AnyWorkspaceLoadedAndDocumentsOpened += _LoadIfNeed;
		
		Panels.PanelManager["Breakpoints"].DontActivateFloating = e => e == _tv;
	}
	
	public DockPanel P { get; } = new();
	
	void _LoadIfNeed() {
		if (_root != null) return;
		_root = new(this, null, true);
		
		_file = App.Model.WorkspaceDirectory + @"\.state\breakpoints.xml";
		if (filesystem.exists(_file).File) {
			try {
				var xr = XElement.Load(_file);
				foreach (var xf in xr.Elements("file")) {
					if (xf.Attr(out int id, "id") && App.Model.FindById((uint)id) is { } f) {
						_Item folder = new(this, f, xf.HasAttr("exp"));
						_root.AddChild(folder);
						foreach (var xb in xf.Elements("b")) folder.AddChild(new(this, xb));
					} else {
						_SaveLater(1);
					}
				}
			}
			catch (Exception e1) { print.it(e1); }
		}
		_TvSetItems();
		
		if (!_initOnce) {
			_initOnce = true;
			
			FilesModel.UnloadingAnyWorkspace += () => {
				if (_root == null) return;
				SaveNowIfNeed();
				_root = null;
				_TvSetItems();
			};
			
			FilesModel.NeedRedraw += v => {
				if (!v.renamed) return;
				var f = _FindItemOfFile(v.f);
				if (f != null) _tv.Redraw(f, v.remeasure);
			};
			
			_tv.ItemActivated += e => {
				if (e.Mod != 0) return;
				if (e.Item is _Item b && !b.IsFolder) {
					_SetEnabled(b, true);
					App.Model.OpenAndGoTo(b.Parent.file, b.line);
				}
			};
			
			_tv.ItemClick += e => {
				var b = e.Item as _Item;
				switch (e.Button) {
				case MouseButton.Left when e.Part is TVParts.Checkbox:
					_SetEnabled(b, !b.IsEnabledOrHasEnabledChildren);
					break;
				case MouseButton.Left:
					switch (e.Mod) {
					case ModifierKeys.Control: _DeleteItem(b); break;
					}
					break;
				case MouseButton.Right:
					_ContextMenu(b);
					break;
				case MouseButton.Middle:
					_SetEnabled(b, !b.IsEnabledOrHasEnabledChildren);
					break;
				}
			};
			
			_tv.PreviewKeyDown += (_, e) => {
				if (e.Key is Key.Up or Key.Down && e.KeyboardDevice.Modifiers == ModifierKeys.Shift && _tv.FocusedItem is _Item { IsFolder: true } f) {
					e.Handled = true;
					_MoveFolder(f, e.Key is Key.Up);
				}
			};
			
			_tv.RightClickInEmptySpace += () => _ContextMenu(null);
			
			App.Timer1sWhenVisible += () => { if (_save != 0 && --_save <= 0) _SaveNow(); };
		}
	}
	
	public void SaveNowIfNeed() {
		if (_save != 0) _SaveNow();
	}
	
	void _SaveNow() {
		try {
			var xr = new XElement("breakpoints");
			for (var f = _root.FirstChild; f != null; f = f.Next) {
				var xf = new XElement("file");
				xf.SetAttributeValue("id", f.file.IdString);
				if (f.IsExpanded) xf.SetAttributeValue("exp", "");
				xr.Add(xf);
				for (var b = f.FirstChild; b != null; b = b.Next) {
					var xb = new XElement("b");
					xb.SetAttributeValue("line", b.line.ToS());
					xb.SetAttributeValue("name", b.name);
					if (b.IsEnabled) xb.SetAttributeValue("en", "");
					if (!b.condition.NE()) xb.SetAttributeValue("condition", b.condition);
					if (!b.log.NE()) xb.SetAttributeValue("log", b.log);
					if (b.LogExpression) xb.SetAttributeValue("flags", "1");
					xf.Add(xb);
				}
			}
			xr.Save(_file);
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
			if (b.SetMarkerInDoc()) b = b.Next;
			else {
				var bb = b; b = b.Next;
				bb.Remove();
				removed = true;
				_SaveLater(1);
			}
		}
		if (removed) _TvSetItems(true);
	}
	
	_Item _FindItemOfFile(FileNode fn) {
		if (_root != null && fn != null)
			for (var f = _root.FirstChild; f != null; f = f.Next)
				if (f.file == fn) return f;
		return null;
	}
	_Item _FindItemOfFile(SciCode doc) => _FindItemOfFile(doc?.EFile);
	
	public void ToggleBreakpoint(int pos8 = -1, bool logpoint = false) {
		var doc = Panels.Editor.ActiveDoc;
		if (doc == null || !doc.EFile.IsCodeFile) return;
		bool useSelStart = pos8 < 0;
		int line = useSelStart ? doc.aaaLineFromPos() : doc.aaaLineFromPos(false, pos8);
		if (_IsBreakpoint(doc, line)) {
			_DeleteBreakpoint(doc, line);
		} else {
			if (!useSelStart) if (doc.aaaLineFromPos() == line) useSelStart = true;
			
			int h = doc.aaaMarkerAdd(logpoint ? SciCode.c_markerBreakpointL : SciCode.c_markerBreakpoint, line); if (h < 0) return;
			var folder = _FindItemOfFile(doc);
			if (folder == null) {
				_root.AddChild(folder = new(this, doc.EFile, true), true);
			} else folder.SetIsExpanded(true);
			
			var name = PanelBookmarks.GetMarkerName_(doc, line, useSelStart) ?? "Breakpoint";
			_Item b = new(this, line, h, name) { IsEnabled = true, log = logpoint ? "LOGPOINT" : null };
			if (folder.Children().FirstOrDefault(o => o.line > line) is { } b2) b2.AddSibling(b, false);
			else folder.AddChild(b);
			
			_TvSetItems(true);
			_TvSelect(b);
			_SaveLater();
			
			if (logpoint) _BreakpointProperties(b, doc);
		}
	}
	
	void _SetEnabled(_Item b, bool enabled) {
		if (b.IsFolder) {
			foreach (var v in b.Children()) _SetEnabled(v, enabled);
		} else if (b.IsEnabled != enabled) {
			b.IsEnabled = enabled;
			_tv.Redraw(b);
			b.SetMarkerInDoc();
		}
		_SaveLater();
	}
	
	internal bool SciMiddleClick_(SciCode doc, int line) {
		if (_BreakpointFromLine(doc, line) is not { } b) return false;
		_SetEnabled(b, !b.IsEnabled);
		return true;
	}
	
	internal void AddMarginMenuItems_(SciCode doc, popupMenu m, int line, int pos8) {
		if (_BreakpointFromLine(doc, line) is { } b) {
			m["Delete breakpoint", "*Material.MinusCircle @12 #EE3000"] = o => ToggleBreakpoint(pos8);
			m["Breakpoint properties..."] = o => _BreakpointProperties(b, doc);
			m.AddCheck("Enabled breakpoint\tM-click", b.IsEnabled, o => _SetEnabled(b, o.IsChecked));
		} else {
			m["Add breakpoint", "*Material.Circle @12 #EE3000"] = o => ToggleBreakpoint(pos8);
			m["Add logpoint", "*BootstrapIcons.DiamondFill @14" + Menus.green2] = o => ToggleBreakpoint(pos8, true);
		}
	}
	
	void _BreakpointProperties(_Item x, DependencyObject owner) {
		var w = new KDialogWindow();
		w.InitWinProp("Breakpoint properties", owner);
		var b = new wpfBuilder(w).Width(300..800);
		b.R.Add("Condition", out TextBox eCondition, x.condition).Font("Consolas")
			.Tooltip("""
Break when this expression evaluates to true.
Example: i==5
Not all kinds of expressions are supported.
Conditional breakpoints have different marker and Breakpoints panel text color.
""");
		//b.R.Add("Hit count", out TextBox eHit).Width(50, "L");
		b.R.Add("Message", out TextBox eLog, x.log)
			.Tooltip("""
Print this text or expression instead of pausing.
Such breakpoints are known as logpoints, tracepoints.
Green marker and Breakpoints panel text color.
Simple text examples:
  Simple text
  Text with <b>output tags<>
Expression examples:
  "Multiline\nstring"
  variable
  "variable=" + variable
  "<c red>x.y=" + x.y + "<>"
  Au.clipboard.text
""");
		b.R.Skip().Add(out KCheckBox cLE, "Message is expression").Checked(x.LogExpression)
			.Tooltip("""
Message is a C# expression. See examples in Message tooltip.
Not all kinds of expressions are supported.
""");
		b.R.AddOkCancel();
		b.End();
		var e1 = !x.log.NE() ? eLog : eCondition; e1.SelectAll(); e1.Focus();
		if (!w.ShowAndWait()) return;
		
		string cond = eCondition.TextOrNull(), log = eLog.TextOrNull();
		bool isLE = cLE.IsChecked;
		if (cond != x.condition || log != x.log || isLE != x.LogExpression) {
			x.condition = cond;
			x.log = log;
			x.LogExpression = isLE;
			_SaveLater();
			_tv.Redraw(x);
			x.SetMarkerInDoc();
			Panels.Debug.BreakpointPropertiesChanged_(x);
		}
	}
	
	internal void SciMouseDwell_(bool started, SciCode doc, in Sci.SCNotification n) {
		if (_marginTooltip != null) _marginTooltip.IsOpen = false;
		if (n.position >= 0 || !doc.EFile.IsCodeFile) return;
		if (started) {
			int margin = doc.aaaMarginFromPoint((n.x, n.y));
			if (margin != SciCode.c_marginMarkers) return;
			int pos = doc.aaaPosFromXY(false, (n.x, n.y), false);
			int line = doc.aaaLineFromPos(false, pos);
			if (_BreakpointFromLine(doc, line) is { } b && b.HasProperties) {
				_marginTooltip ??= new ToolTip { Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint, HorizontalOffset = 20 };
				_marginTooltip.PlacementTarget = doc;
				b.TooltipSetContent(_marginTooltip);
				_marginTooltip.IsOpen = true;
			}
		}
	}
	ToolTip _marginTooltip;
	
	void _TvSetItems(bool modified = false) {
		if (_root?.Count > 0) {
			_tv.SetItems(_root.Children(), modified);
		} else {
			_tv.SetItems(null);
		}
	}
	
	void _TvSelect(_Item b) {
		if (!b.IsFolder && !b.Parent.IsExpanded) _tv.Expand(b.Parent, true);
		_tv.SelectSingle(b, true);
	}
	
	void _ContextMenu(_Item b) {
		var m = new popupMenu();
		
		if (b != null) {
			_TvSelect(b);
			
			if (b.IsFolder) {
				m["Move up\tShift+Up", disable: b.Previous == null] = o => _MoveFolder(b, true);
				m["Move down\tShift+Down", disable: b.Next == null] = o => _MoveFolder(b, false);
				m.Separator();
				bool haveEnabled1 = b.IsEnabledOrHasEnabledChildren;
				m[haveEnabled1 ? "Disable these breakpoints\tM-click" : "Enable these breakpoints\tM-click"] = o => _SetEnabled(b, !haveEnabled1);
				m["Delete these breakpoints\tCtrl+click"] = o => _DeleteItem(b);
			} else {
				m["Delete\tCtrl+click"] = o => _DeleteItem(b);
				m["Properties..."] = o => _BreakpointProperties(b, P);
				m.AddCheck("Enabled\tM-click", b.IsEnabled, o => _SetEnabled(b, o.IsChecked));
			}
			m.Separator();
		} else if (!(_root?.Count > 0)) {
			print.it("To add a breakpoint, click the white margin in the code editor.");
			return;
		}
		
		bool haveEnabled = _root.IsEnabledOrHasEnabledChildren;
		m[haveEnabled ? "Disable all breakpoints" : "Enable all breakpoints"] = o => { foreach (var v in _root.Children()) _SetEnabled(v, !haveEnabled); };
		m.Submenu("More", m => {
			m["Delete all disabled breakpoints"] = o => {
				foreach (var v in _root.Descendants().ToArray()) if (!v.IsFolder && !v.IsEnabled) _DeleteItem(v);
			};
			m["Delete all breakpoints"] = o => {
				foreach (var v in _root.Children().ToArray()) _DeleteItem(v);
			};
		});
		
		m.Show(owner: _tv);
	}
	
	bool _IsBreakpoint(SciCode doc, int line) => (doc.Call(Sci.SCI_MARKERGET, line) & 63 << SciCode.c_markerBreakpoint) != 0;
	
	_Item _BreakpointFromLine(SciCode doc, int line) {
		if (_FindItemOfFile(doc) is { } folder) {
			for (int i = 0; ; i++) {
				int h = doc.Call(Sci.SCI_MARKERHANDLEFROMLINE, line, i);
				if (h < 0) return null;
				for (var b = folder.FirstChild; b != null; b = b.Next) if (b.markerHandle == h) return b;
			}
		}
		return null;
	}
	
	void _DeleteBreakpoint(SciCode doc, int line, bool sciDelete = true) {
		if (_BreakpointFromLine(doc, line) is { } b) {
			if (sciDelete) doc.aaaMarkerDeleteHandle(b.markerHandle);
			_DeleteBreakpointL(b);
		}
	}
	
	void _DeleteBreakpointL(_Item b) {
		if (b.IsEnabled) Panels.Debug.BreakpointAddedDeleted_(b, false);
		var folder = b.Parent;
		b.Remove();
		if (!folder.HasChildren) folder.Remove();
		_TvSetItems(true);
		_SaveLater();
	}
	
	void _DeleteItem(_Item b) {
		if (b.IsFolder) {
			foreach (var v in b.Children().ToArray()) _DeleteItem(v);
		} else {
			var folder = b.Parent;
			if (b.markerHandle != 0 && folder.file.OpenDoc is { } doc) {
				doc.aaaMarkerDeleteHandle(b.markerHandle);
			}
			_DeleteBreakpointL(b);
		}
	}
	
	internal void FileDeleted(IEnumerable<FileNode> files) {
		foreach (var file in files) {
			if (_FindItemOfFile(file) is { } folder) {
				foreach (var b in folder.Children()) if (b.IsEnabled) Panels.Debug.BreakpointAddedDeleted_(b, false);
				folder.Remove();
				_TvSetItems(true);
				_SaveLater();
			}
		}
	}
	
	void _MoveFolder(_Item f, bool up) {
		if (up && f == _root.FirstChild) return;
		var ff = up ? f.Previous : f.Next;
		if (ff == null) return;
		f.Remove();
		ff.AddSibling(f, after: !up);
		_TvSetItems(true);
		_SaveLater();
	}
	
	internal void SciDeletingLineWithMarker(SciCode doc, int line) {
		_DeleteBreakpoint(doc, line, false);
	}
	
	internal void SciModified(SciCode doc, in Sci.SCNotification n) {
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
		}
	}
	
	internal IEnumerable<IBreakpoint> GetBreakpoints(bool disabledToo = false) {
		for (var f = _root.FirstChild; f != null; f = f.Next) {
			for (var b = f.FirstChild; b != null; b = b.Next) {
				if (disabledToo || b.IsEnabled) yield return b;
			}
		}
	}
	
	class _Item : TreeBase<_Item>, ITreeViewItem, IBreakpoint {
		readonly PanelBreakpoints _view;
		public readonly FileNode file; //if folder
		public int line, markerHandle; //if breakpoint
		public string condition, log; //if breakpoint
		public readonly string name;
		readonly bool _isFolder;
		bool _isExpanded;
		bool _isEnabled;
		
		//folder
		public _Item(PanelBreakpoints view, FileNode file, bool isExpanded) {
			_view = view;
			this.file = file;
			_isFolder = true;
			_isExpanded = isExpanded;
		}
		
		//new breakpoint
		public _Item(PanelBreakpoints bv, int line, int markerHandle, string name) {
			_view = bv;
			this.line = line;
			this.markerHandle = markerHandle;
			this.name = name;
		}
		
		//loading breakpoint
		public _Item(PanelBreakpoints bv, XElement x) {
			_view = bv;
			line = x.Attr("line", 0);
			name = x.Attr("name");
			_isEnabled = x.HasAttr("en");
			condition = x.Attr("condition");
			log = x.Attr("log");
			if (x.Attr(out int flags, "flags")) {
				if ((flags & 1) != 0) LogExpression = true;
			}
		}
		
#if DEBUG
		public override string ToString() => ((ITreeViewItem)this).DisplayText;
#endif
		
		public bool IsEnabledOrHasEnabledChildren => IsFolder ? Children().Any(static o => o.IsEnabledOrHasEnabledChildren) : _isEnabled;
		
		public bool SetMarkerInDoc() {
			if (Parent.file.OpenDoc is { } doc) {
				if (markerHandle != 0) { doc.aaaMarkerDeleteHandle(markerHandle); markerHandle = 0; }
				int marker = !log.NE() ? SciCode.c_markerBreakpointL : !condition.NE() ? SciCode.c_markerBreakpointC : SciCode.c_markerBreakpoint;
				if (!_isEnabled) marker++;
				var h = doc.aaaMarkerAdd(marker, line);
				if (h != -1) { markerHandle = h; return true; }
			}
			return false;
		}
		
		//ITreeViewItem
		
		public bool IsFolder => _isFolder;
		
		public bool IsExpanded => _isExpanded;
		
		public void SetIsExpanded(bool yes) { _isExpanded = yes; _view._SaveLater(5 * 60); }
		
		IEnumerable<ITreeViewItem> ITreeViewItem.Items => base.Children();
		
		string ITreeViewItem.DisplayText => _isFolder ? (file.Parent.HasParent ? $"{file.Name}  [{file.Parent.ItemPath}]" : file.Name) : $"{(line + 1).ToS()}  {name}";
		//never mind: after moving the file should display the new path (just redraw).
		
		object ITreeViewItem.Image => _isFolder ? EdResources.FolderArrow(_isExpanded) : null;
		
		TVParts ITreeViewItem.NoParts => _isFolder ? TVParts.Checkbox : TVParts.Image;
		
		TVCheck ITreeViewItem.CheckState => _isEnabled ? TVCheck.Checked : TVCheck.Unchecked;
		
		int ITreeViewItem.TextColor(TVColorInfo ci) => !log.NE() ? 0x00A000 : !condition.NE() ? (ci.isHighContrastDark ? 0x8080FF : 0x0000FF) : -1;
		
		int ITreeViewItem.TooltipDelay => HasProperties ? 500 : 0;
		
		public void TooltipSetContent(ToolTip tt) {
			if (tt.Content is not TextBlock k) tt.Content = k = new TextBlock { FontFamily = new("Consolas") };
			string s1 = condition.NE() ? null : "Condition: ", s2 = log.NE() ? null : condition.NE() ? "Message: " : "\nMessage: ";
			k.Text = $"{s1}{condition}{s2}{log}";
		}
		
		//IBreakpoint
		
		FileNode IBreakpoint.File => file ?? Parent.file;
		
		int IBreakpoint.Line => line;
		
		string IBreakpoint.Condition => condition;
		
		string IBreakpoint.Log => log;
		
		public bool LogExpression { get; set; }
		
		public bool HasProperties => !(condition.NE() && log.NE());
		
		public bool IsEnabled {
			get => _isEnabled;
			set {
				if (value == _isEnabled) return;
				_isEnabled = value;
				Panels.Debug.BreakpointAddedDeleted_(this, _isEnabled);
			}
		}
		
		int IBreakpoint.Id { get; set; }
	}
}

interface IBreakpoint {
	FileNode File { get; }
	int Line { get; }
	string Condition { get; }
	string Log { get; }
	bool LogExpression { get; set; }
	bool HasProperties { get; }
	bool IsEnabled { get; }
	int Id { get; set; }
}
