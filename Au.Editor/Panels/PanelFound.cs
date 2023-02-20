using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Au.Controls;
using Au.Tools;
using System.Collections;

class PanelFound {
	List<_KScintilla> _a = new();
	int _iActive = -1;
	Grid _grid;
	KCheckBox _cKeep;
	ListBox _lb;

	public PanelFound() {
		P.UiaSetName("Found panel");

		var b = new wpfBuilder(P).Brush(SystemColors.ControlBrush);
		b.Options(modifyPadding: false, margin: new());

		var tb = b.xAddToolBar(hideOverflow: true, controlBrush: true);

		_cKeep = tb.AddCheckbox("*RemixIcon.Lock2Line" + Menus.black, "Keep results", enabled: false);
		_cKeep.CheckChanged += (_, _) => { if (_iActive >= 0) _Sci.isLocked = _cKeep.IsChecked; };

		b.Add<Border>().Border(thickness2: new(1, 0, 0, 0)).SpanRows(2);
		b.Add(out _grid, flags: WBAdd.ChildOfLast);

		b.Row(-1).Add(out _lb).Span(1).Width(70..).Border(thickness2: new(0, 1, 0, 0));
		_lb.SelectionChanged += (_, _) => {
			if (_iActive >= 0) _Sci.Visibility = Visibility.Hidden;
			_iActive = _lb.SelectedIndex;
			if (_iActive >= 0) {
				_Sci.Visibility = Visibility.Visible;
				_cKeep.IsChecked = _Sci.isLocked;
			} else {
				_cKeep.IsChecked = false;
			}
			_cKeep.IsEnabled = _iActive >= 0;
		};

		b.End();

		FilesModel.UnloadingAnyWorkspace += _CloseAll;
	}

	public UserControl P { get; } = new();

	_KScintilla _Sci => _a[_iActive];

	public void Prepare(FoundKind kind, string text) {
		Panels.PanelManager[P].Visible = true;

		if (_iActive < 0 || _Sci.kind != kind || _Sci.isLocked) {
			int i = _a.FindIndex(o => o.kind == kind && !o.isLocked);
			if (i < 0) {
				var c = new _KScintilla(kind) {
					Name = "Found_" + kind,
					AaInitReadOnlyAlways = true,
					AaInitTagsStyle = KScintilla.AaTagsStyle.AutoAlways,
					AaUsesEnter = true
				};
				_grid.Children.Add(c);
				i = _a.Count;
				_a.Add(c);
				var li = new KListBoxItemWithImage(kind switch {
					FoundKind.Files => "*FeatherIcons.File #008EEE",
					FoundKind.Text => "*Material.Text #464646",
					FoundKind.SymbolReferences => "*Codicons.SymbolMethod #8C40FF",
					_ => null
				}, null);
				li.ContextMenuOpening += (li, _) => {
					var m = new popupMenu();
					m["Close\tM-click"] = o => _Close(li);
					m.Show();
				};
				li.MouseDown += (li, e) => { if (e.ChangedButton == MouseButton.Middle) _Close(li); };
				_lb.Items.Add(li);
			}
			_lb.SelectedIndex = i;
		}

		_Sci.Clear();
		if (kind == FoundKind.Text) _Sci.aaaText = "<c #A0A0A0>... searching ...<>";
		(_lb.Items[_iActive] as KListBoxItemWithImage).SetText(text.Limit(15).RxReplace(@"\R", " "), text);
	}

	public void ClearResults(FoundKind kind) {
		if (_iActive < 0 || _Sci.kind != kind || _Sci.isLocked) return;
		_Sci.Clear();
		(_lb.Items[_iActive] as KListBoxItemWithImage).SetText(null, null);
	}

	public void SetFilesFindResults(string text) {
		Debug.Assert(_Sci.kind == FoundKind.Files);
		_Sci.aaaSetText(text);
	}

	public void SetFindInFilesResults(PanelFind._TextToFind ttf, string text, List<FileNode> files) {
		Debug.Assert(_Sci.kind == FoundKind.Text);
		_Sci.aaaSetText(text);
		_Sci.ttf = ttf;
		_Sci.files = files;
	}

	void _Close(object li) {
		int i = _lb.Items.IndexOf(li as KListBoxItemWithImage); if (i < 0) return;
		if (i == _iActive && _a.Count > 1) _lb.SelectedIndex = _iActive == _a.Count - 1 ? _iActive - 1 : _iActive + 1;
		var sci = _a[i];
		_a.RemoveAt(i);
		if (_a.Count == 0) _iActive = -1;
		_lb.Items.RemoveAt(i);
		_CloseSci(sci);
	}

	void _CloseSci(_KScintilla sci) {
		_grid.Children.Remove(sci);
		sci.Dispose();
		if (sci.IsFocused) Keyboard.Focus(_lb);
	}

	void _CloseAll() {
		if (_a.Count == 0) return;
		_iActive = -1;
		_lb.Items.Clear();
		foreach (var sci in _a) _CloseSci(sci);
		_a.Clear();
	}

	class _KScintilla : KScintilla {
		const int c_indic = 0;
		public readonly FoundKind kind;
		public bool isLocked;
		public PanelFind._TextToFind ttf;
		public List<FileNode> files;
		HashSet<FileNode> _openedFiles;

		public _KScintilla(FoundKind kind) {
			this.kind = kind;
		}

		public void Clear() {
			aaaClearText();
			ttf = null;
			files = null;
			_openedFiles = null;
		}

		protected override void AaOnHandleCreated() {
			aaaSetMarginWidth(1, 0);
			aaaStyleFont(Sci.STYLE_DEFAULT, App.Wmain);
			aaaStyleClearAll();
			AaTags.SetLinkStyle(new SciTags.UserDefinedStyle(), (false, default), false);
			aaaIndicatorDefine(c_indic, Sci.INDIC_BOX, 0xe08000);

			//open file
			AaTags.AddLinkTag("+open", s => {
				_OpenLinkClicked(s);
			});

			//Find -> replace all (in one file)
			AaTags.AddLinkTag("+ra", s => {
				if (!_OpenLinkClicked(s, replaceAll: true)) return;
				timer.after(10, _ => Panels.Find._ReplaceAllInFile(ttf));
				//info: without timer sometimes does not set cursor pos correctly
			});

			//Find -> open file and select a found text
			AaTags.AddLinkTag("+f", s => {
				var a = s.Split(' ');
				if (!_OpenLinkClicked(a[0])) return;
				var doc = Panels.Editor.ActiveDoc;
				//doc.Focus();
				int from = a[1].ToInt(), to = a[2].ToInt();
				timer.after(10, _ => {
					if (to >= doc.aaaLen16) return;
					App.Model.EditGoBack.RecordNext();
					doc.aaaSelect(true, from, to, true);
				});
				//info: scrolling works better with async when now opened the file
			});

			bool _OpenLinkClicked(string file, bool replaceAll = false) {
				var f = App.Model.Find(file); //<id>
				if (f == null) return false;
				if (f.IsFolder) f.SelectSingle();
				else {
					//avoid opening the file in editor when invalid regex replacement
					if (replaceAll && !Panels.Find._ValidateReplacement(ttf)) return false;

					if (!App.Model.OpenFiles.Contains(f)) (_openedFiles ??= new()).Add(f);
					if (!App.Model.SetCurrentFile(f)) return false;
				}
				//add indicator to help the user to find this line later
				aaaIndicatorClear(c_indic);
				var v = aaaLineStartEndFromPos(false, aaaCurrentPos8);
				aaaIndicatorAdd(false, c_indic, v.start..v.end);
				return true;
			}

			//Find -> replace all (in all files)
			AaTags.AddLinkTag("+raif", _ => Panels.Find._ReplaceAllInFiles(ttf, files, ref _openedFiles));

			//close all
			AaTags.AddLinkTag("+caf", s => {
				if (_openedFiles == null) return;
				App.Model.CloseFiles(_openedFiles);
				App.Model.CollapseAll(exceptWithOpenFiles: true);
				_openedFiles = null;
			});

			base.AaOnHandleCreated();
		}

		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			switch (msg) {
			case Api.WM_MBUTTONDOWN: //close file
				int pos = Call(Sci.SCI_POSITIONFROMPOINTCLOSE, Math2.LoShort(lParam), Math2.HiShort(lParam));
				if (AaTags.GetLinkFromPos(pos, out var tag, out var attr) && tag is "+f" or "+ra") {
					//print.it(tag, attr);
					var f = App.Model.Find(attr.Split(' ')[0]);
					if (f != null) App.Model.CloseFile(f, selectOther: true, focusEditor: true);
				}
				return default; //don't focus
			}
			return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
		}
	}
}

enum FoundKind {
	Files,
	Text,
	SymbolReferences
}
