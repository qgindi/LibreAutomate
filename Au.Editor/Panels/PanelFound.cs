using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Au.Controls;
using Au.Tools;
using static Au.Controls.Sci;

class PanelFound {
	List<_KScintilla> _a = new();
	int _iActive = -1;
	Grid _grid;
	ListBox _lb;
	
	public PanelFound() {
		P.UiaSetName("Found panel");
		
		var b = new wpfBuilder(P).Brush(SystemColors.ControlBrush);
		b.Options(modifyPadding: false, margin: new());
		
		var tb = b.xAddToolBar(hideOverflow: true, controlBrush: true);
		tb.UiaSetName("Found_toolbar");
		
		var cKeep = tb.AddCheckbox("*RemixIcon.Lock2Line" + Menus.black, "Keep results", enabled: false);
		cKeep.CheckChanged += (_, _) => { if (_iActive >= 0) _Sci.isLocked = cKeep.IsChecked; };
		
		var bCloseOF = tb.AddButton("*Codicons.CloseAll" + Menus.black, "Close opened files", _ => _Sci?.CloseOpenedFiles(), enabled: false);
		
		b.Add<Border>().Border(thickness2: new(1, 0, 0, 0)).SpanRows(2);
		b.Add(out _grid, flags: WBAdd.ChildOfLast);
		
		b.Row(-1).Add(out _lb).Span(1).Width(70..).Border(thickness2: new(0, 1, 0, 0)).UiaName("Found_pages");
		_lb.SelectionChanged += (_, _) => {
			if (_iActive >= 0) _Sci.Visibility = Visibility.Hidden;
			_iActive = _lb.SelectedIndex;
			if (_iActive >= 0) {
				_Sci.Visibility = Visibility.Visible;
				cKeep.IsChecked = _Sci.isLocked;
			} else {
				cKeep.IsChecked = false;
			}
			cKeep.IsEnabled = _iActive >= 0;
			bCloseOF.IsEnabled = _iActive >= 0;
		};
		
		b.End();
		
		FilesModel.UnloadingAnyWorkspace += _CloseAll;
	}
	
	public UserControl P { get; } = new();
	
	_KScintilla _Sci => _a[_iActive];
	
	public WorkingState Prepare(Found kind, string text, out SciTextBuilder builder) {
		Panels.PanelManager[P].Visible = true;
		
		if (_iActive < 0 || _Sci.kind != kind || _Sci.isLocked) {
			int i = _a.FindIndex(o => o.kind == kind && !o.isLocked);
			if (i < 0) {
				var c = new _KScintilla(kind);
				_grid.Children.Add(c);
				i = _a.Count;
				_a.Add(c);
				var li = new KListBoxItemWithImage(kind switch {
					Found.Files => "*FeatherIcons.File" + Menus.black,
					//Found.Text => "*Material.Text" + Menus.black,
					Found.Text => "*Material.FindReplace" + Menus.black,
					Found.SymbolReferences => "*Codicons.References" + Menus.black,
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
		(_lb.Items[_iActive] as KListBoxItemWithImage).SetText(text.Limit(15).RxReplace(@"\R", " "), text);
		
		builder = new SciTextBuilder() {
			BoldStyle = Styles.Bold,
			GrayStyle = Styles.Gray,
			GreenStyle = Styles.Green,
			HiliteIndic = Indicators.Hilite,
			Hilite2Indic = Indicators.Hilite2,
			LinkIndic = Indicators.Link,
			Link2Indic = Indicators.Link2,
			ControlWidth = (int)_grid.ActualWidth,
		};
		
		if (kind is Found.Files) return new(_Sci, disable: false);
		return new(_Sci, disable: true);
	}
	
	public void ClearResults(Found kind) {
		if (_iActive < 0 || _Sci.kind != kind || _Sci.isLocked) return;
		_Sci.Clear();
		(_lb.Items[_iActive] as KListBoxItemWithImage).SetText(null, null);
	}
	
	bool _IsSciOk(in WorkingState ws) {
		return _iActive >= 0 && _Sci == ws.Scintilla;
	}
	
	public void SetFilesFindResults(in WorkingState ws, SciTextBuilder b) {
		if (!_IsSciOk(ws)) return;
		b.Apply(_Sci);
	}
	
	public void SetFindInFilesResults(in WorkingState ws, SciTextBuilder b, PanelFind._TextToFind ttf, List<FileNode> files) {
		if (!_IsSciOk(ws)) return;
		b.Apply(_Sci);
		_Sci.ttf = ttf;
		_Sci.files = files;
	}
	
	public void SetSymbolReferencesResults(in WorkingState ws, SciTextBuilder b) {
		if (!_IsSciOk(ws)) return;
		b.Apply(_Sci);
	}
	
	/// <summary>
	/// Appends limited text of line of text range <i>start..end</i>, as a link that opens file <i>f</i> and select text <i>start..end</i>, with highlighted range <i>start..end</i>.
	/// </summary>
	/// <param name="text">Text of file <i>f</i>.</param>
	public static void AppendFoundLine(SciTextBuilder b, FileNode f, string text, int start, int end, bool displayFile) {
		int wid = Math.Clamp((b.ControlWidth - 20) / 10, 30, 100);
		int lineStart = start, lineEnd = end;
		int lsMax = Math.Max(start - wid, 0), leMax = Math.Min(end + 200, text.Length); //start/end limits like in VS
		while (lineStart > lsMax && !text.IsCsNewlineChar(lineStart - 1)) lineStart--;
		bool limitStart = lineStart > 0 && !text.IsCsNewlineChar(lineStart - 1);
		while (lineStart < start && text[lineStart] is ' ' or '\t') lineStart++;
		while (lineEnd < leMax && !text.IsCsNewlineChar(lineEnd)) lineEnd++;
		bool limitEnd = lineEnd < text.Length && !text.IsCsNewlineChar(lineEnd);
		b.Link2(new CodeLink(f, start, end));
		if (limitStart) b.Text("…");
		b.Text(text.AsSpan(lineStart..lineEnd));
		b.Hilite(b.Length - (lineEnd - start), b.Length - (lineEnd - end));
		if (limitEnd) b.Text("…");
		if (displayFile) b.Text("      ").Gray(f.Name);
		b.Link_().NL();
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
		public readonly Found kind;
		public bool isLocked, once;
		public PanelFind._TextToFind ttf;
		public List<FileNode> files;
		HashSet<FileNode> _openedFiles;
		
		public _KScintilla(Found kind) {
			this.kind = kind;
			Name = "Found_" + kind;
			AaInitReadOnlyAlways = true;
			AaInitTagsStyle = AaTagsStyle.AutoAlways;
		}
		
		protected override void AaOnHandleCreated() {
			aaaMarginSetWidth(1, 0);
			aaaStyleFont(STYLE_DEFAULT, App.Wmain);
			aaaStyleClearAll();
			Call(SCI_SETCARETSTYLE, CARETSTYLE_INVISIBLE);
			Call(SCI_SETEXTRAASCENT, 1);
			Call(SCI_SETEXTRADESCENT, 1);
			
			//indicators
			aaaIndicatorDefine(Indicators.Hilite, INDIC_STRAIGHTBOX, 0xffff00, alpha: 255, borderAlpha: 255, underText: true);
			aaaIndicatorDefine(Indicators.Hilite2, INDIC_STRAIGHTBOX, 0xFFC000, alpha: 255, borderAlpha: 255, underText: true); //currently not used. Edit when used.
			aaaIndicatorDefine(Indicators.FocusRect, INDIC_FULLBOX, 0x4169E1, alpha: 25, borderAlpha: 255, strokeWidth: _dpi / 96); //better than SC_ELEMENT_CARET_LINE_BACK/SCI_SETCARETLINEFRAME etc
			
			//link indicators
			aaaIndicatorDefine(-Indicators.Link, INDIC_COMPOSITIONTHIN, 0x0080ff, hoverColor: 0x8000ff);
			aaaIndicatorDefine(-Indicators.Link + 1, INDIC_TEXTFORE, 0x0080ff, hoverColor: 0x8000ff);
			aaaIndicatorDefine(Indicators.Link2, INDIC_HIDDEN, hoverColor: 1);
			
			//styles
			aaaStyleBold(Styles.Bold, true);
			aaaStyleForeColor(Styles.Gray, 0x808080);
			aaaStyleForeColor(Styles.Green, 0x008000);
			
			if (kind == Found.SymbolReferences) aaaFoldingInit(0, autoFold: true);
			
			base.AaOnHandleCreated();
		}
		
		protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
			switch (msg) {
			case Api.WM_MBUTTONDOWN: //close file
				int pos = Call(SCI_POSITIONFROMPOINTCLOSE, Math2.LoShort(lParam), Math2.HiShort(lParam));
				if (AaRangeDataGet(false, pos, out object o)) {
					var f = o switch { FileNode f1 => f1, CodeLink cl => cl.file, _ => null };
					if (f != null) App.Model.CloseFile(f, selectOther: true, focusEditor: true);
				}
				return default; //don't focus
			}
			return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
		}
		
		protected override void AaOnSciNotify(ref SCNotification n) {
			//if (n.code == NOTIF.SCN_MODIFIED) print.it(n.code, n.modificationType, n.position, n.length); else if (n.code is not (NOTIF.SCN_PAINTED or NOTIF.SCN_STYLENEEDED)) print.it(n.code);
			switch (n.code) {
			case NOTIF.SCN_INDICATORRELEASE:
				_OnClickIndicLink(n.position);
				break;
			}
			base.AaOnSciNotify(ref n);
		}
		
		public void Clear() {
			aaaClearText();
			ttf = null;
			files = null;
			_openedFiles = null;
		}
		
		public void CloseOpenedFiles() {
			if (_openedFiles != null) {
				App.Model.CloseFiles(_openedFiles);
				App.Model.CollapseAll(exceptWithOpenFiles: true);
				_openedFiles = null;
			}
		}
		
		void _OnClickIndicLink(int pos8) {
			if (AaRangeDataGet(false, pos8, out object o)) {
				switch (o) {
				case FileNode f:
					_OpenLinkClicked(f);
					break;
				case CodeLink k:
					if (_OpenLinkClicked(k.file)) {
						timer.after(10, _ => {
							var doc = Panels.Editor.ActiveDoc;
							if (doc?.EFile != k.file || k.end >= doc.aaaLen16) return;
							App.Model.EditGoBack.RecordNext();
							doc.aaaGoToPos(true, k.start);
							doc.Focus();

							//rejected: briefly show a marker, or hilite the line, or change caret color/width.
							//	More distracting than useful.
							//	The default blinking caret + default highlighting are easy to notice.
						});
						//info: scrolling works better with async when now opened the file
					}
					break;
				case ReplaceAllLink k:
					if (_OpenLinkClicked(k.file, replaceAll: true)) {
						timer.after(10, _ => {
							if (Panels.Editor.ActiveDoc?.EFile != k.file) return;
							Panels.Find._ReplaceAllInFile(ttf);
						});
						//info: without timer sometimes does not set cursor pos correctly
					}
					break;
				case string s when s == "RAIF": //Replace all in all files
					Panels.Find._ReplaceAllInFiles(ttf, files, ref _openedFiles);
					break;
				case Action k:
					k.Invoke();
					break;
				}
			} else Debug_.Print(pos8);
		}
		
		bool _OpenLinkClicked(FileNode f, bool replaceAll = false) {
			if (App.Model.IsAlien(f)) return false;
			if (f.IsFolder) f.SelectSingle();
			else {
				//avoid opening the file in editor when invalid regex replacement
				if (replaceAll && !Panels.Find._ValidateReplacement(ttf)) return false;
				
				if (!App.Model.OpenFiles.Contains(f)) (_openedFiles ??= new()).Add(f);
				if (!App.Model.SetCurrentFile(f)) return false;
			}
			//add indicator to help the user to find this line later
			aaaIndicatorClear(Indicators.FocusRect);
			var v = aaaLineStartEndFromPos(false, aaaCurrentPos8);
			aaaIndicatorAdd(Indicators.FocusRect, false, v.start..v.end);
			return true;
		}
	}
	
	public enum Found {
		Files,
		Text,
		SymbolReferences
	}
	
	//CONSIDER: instead of taskbar button progress:
	//	While searching hide scintilla (or don't create until finished) and in its place show a WPF progressbar.
	//	But then need to dispatch messages, eg async-await.
	public struct WorkingState : IDisposable {
		public KScintilla Scintilla { get; private set; }
		
		public WorkingState(KScintilla sci, bool disable) {
			Scintilla = sci;
			if (disable) Panels.Found.P.IsEnabled = false;
		}
		
		public void Dispose() {
			Panels.Found.P.IsEnabled = true;
		}
		
		/// <summary>
		/// Returns true when called first time for current Scintilla control; let the caller set Scintilla styles, markers, etc. Later returns false.
		/// </summary>
		public bool NeedToInitControl {
			get {
				var k = Scintilla as _KScintilla;
				if (k.once) return false;
				k.once = true;
				return true;
			}
		}
	}
	
	public record class CodeLink(FileNode file, int start, int end);
	
	public record class ReplaceAllLink(FileNode file);
	
	/// <summary>
	/// Indices of indicators defined by this control.
	/// </summary>
	public static class Indicators {
		public const int Hilite = 0, Hilite2 = 1, FocusRect = 2, Link = -16, Link2 = 18;
	}
	
	/// <summary>
	/// Indices of styles defined by this control.
	/// </summary>
	public static class Styles {
		public const int Bold = 30, Gray = 29, Green = 28; //31 STYLE_HIDDEN
	}
}
