using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Au.Controls;
using static Au.Controls.Sci;

class PanelFound {
	_LbItem _li;
	_KScintilla _sci;
	Grid _grid;
	ListBox _lb;
	
	public PanelFound() {
		P.UiaSetName("Found panel");
		
		var b = new wpfBuilder(P).Brush(SystemColors.ControlBrush);
		b.Options(modifyPadding: false, margin: new());
		
		var tb = b.xAddToolBar(hideOverflow: true, controlBrush: true);
		tb.UiaSetName("Found_toolbar");
		
		var cKeep = tb.AddCheckbox("*RemixIcon.Lock2Line" + Menus.black, "Keep results", enabled: false);
		cKeep.CheckChanged += (_, _) => { if (_sci != null) _sci.isLocked = cKeep.IsChecked; };
		
		var bCloseOF = tb.AddButton("*Codicons.CloseAll" + Menus.black, _ => _sci?.CloseOpenedFiles(), "Close opened files", enabled: false);
		
		b.Add<Border>().Border(thickness2: new(1, 0, 0, 0)).SpanRows(2);
		b.Add(out _grid, flags: WBAdd.ChildOfLast);
		
		b.Row(-1).Add(out _lb).Span(1).Border(thickness2: new(0, 1, 0, 0)).UiaName("Found_pages");
		_lb.SelectionChanged += (_, _) => {
			if (_sci != null) _sci.Visibility = Visibility.Hidden;
			_li = _lb.SelectedItem as _LbItem;
			_sci = _li?.sci;
			if (_sci != null) {
				_sci.Visibility = Visibility.Visible;
				cKeep.IsChecked = _sci.isLocked;
			} else {
				cKeep.IsChecked = false;
			}
			cKeep.IsEnabled = _sci != null && _sci.kind != Found.SymbolRename;
			bCloseOF.IsEnabled = _sci != null;
		};
		
		b.End();
		
		FilesModel.UnloadingAnyWorkspace += _CloseAll;
		
		Panels.PanelManager["Found"].DontActivateFloating = e => true;
	}
	
	public UserControl P { get; } = new();
	
	public WorkingState Prepare(Found kind, string text, out SciTextBuilder builder) {
		Panels.PanelManager[P].Visible = true;
		
		if (_sci == null || _sci.kind != kind || _sci.isLocked) {
			var li = _lb.Items.Cast<_LbItem>().FirstOrDefault(o => o.sci.kind == kind && !o.sci.isLocked);
			if (li == null) {
				var c = new _KScintilla(kind);
				_grid.Children.Add(c);
				li = new _LbItem(c, kind switch {
					Found.Files => "*FeatherIcons.File" + Menus.black,
					//Found.Text => "*Material.Text" + Menus.black,
					Found.Text => "*Material.FindReplace" + Menus.black,
					Found.SymbolReferences => Menus.iconReferences,
					Found.SymbolRename => "*PicolIcons.Edit" + Menus.red,
					Found.Repair => "*RPGAwesome.Repair" + Menus.black,
					_ => null
				}, null);
				li.ContextMenuOpening += (_, _) => {
					var m = new popupMenu();
					m["Close\tM-click"] = o => _Close(li);
					m.Show();
				};
				li.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Middle) _Close(li); };
				_lb.Items.Add(li);
			}
			_lb.SelectedItem = li;
		}
		
		_sci.Clear();
		_li.SetText(text.Limit(15).RxReplace(@"\R", " "), text);
		
		builder = new SciTextBuilder() {
			BoldStyle = Styles.Bold,
			GrayStyle = Styles.Gray,
			GreenStyle = Styles.Green,
			LinkIndic = Indicators.Link,
			Link2Indic = Indicators.Link2,
			user = (0, _sci)
		};
		
		if (kind is Found.Files) return new(_sci, disable: false);
		return new(_sci, disable: true);
	}
	
	public void ClearResults(Found kind) {
		if (_sci == null || _sci.kind != kind || _sci.isLocked) return;
		_sci.Clear();
		_li.SetText(null, null);
	}
	
	bool _IsSciOk(in WorkingState ws) {
		return _sci == ws.Scintilla;
	}
	
	public void SetResults(in WorkingState ws, SciTextBuilder b) {
		if (!_IsSciOk(ws)) return;
		b.Apply(_sci);
	}
	
	public void SetFindInFilesResults(in WorkingState ws, SciTextBuilder b, PanelFind._TextToFind ttf, List<FileNode> files) {
		if (!_IsSciOk(ws)) return;
		b.Apply(_sci);
		_sci.ttf = ttf;
		_sci.files = files;
	}
	
	/// <summary>
	/// Appends limited text of line of text range <i>start..end</i>, as a link that opens file <i>f</i> and select text <i>start..end</i>, with highlighted range <i>start..end</i>.
	/// </summary>
	/// <param name="text">Text of file <i>f</i>.</param>
	public static void AppendFoundLine(SciTextBuilder b, FileNode f, string text, int start, int end, bool displayFile, int indicHilite = Indicators.HiliteY) {
		if (b.user.i == 0) {
			var k = b.user.o as KScintilla;
			b.user.i = Math.Max((int)k.ActualWidth - k.aaaMarginGetX(4, dpiUnscale: true).right - 20, 1); //logical pixels
		}
		var fileName = displayFile ? f.Name.Limit(30) : null;
		int wid = Math.Clamp(b.user.i / 10 - (displayFile ? fileName.Length + 4 : 0), 20, 100); //chars
		int lineStart = start, lineEnd = end;
		int lsMax = Math.Max(start - wid, 0), leMax = Math.Min(end + 200, text.Length); //start/end limits like in VS
		while (lineStart > lsMax && !text.IsCsNewlineChar(lineStart - 1)) lineStart--;
		bool limitStart = lineStart > 0 && !text.IsCsNewlineChar(lineStart - 1);
		while (lineStart < start && text[lineStart] is ' ' or '\t') lineStart++;
		while (lineEnd < leMax && !text.IsCsNewlineChar(lineEnd)) lineEnd++;
		bool limitEnd = lineEnd < text.Length && !text.IsCsNewlineChar(lineEnd);
		
		b.Link2(new CodeLink(f, start, end));
		if (displayFile) b.Gray(fileName).Text("        ");
		if (limitStart) b.Text("…");
		b.Text(text.AsSpan(lineStart..start)).Indic(indicHilite, text.AsSpan(start..end)).Text(text.AsSpan(end..lineEnd));
		if (limitEnd) b.Text("…");
		b.Link_().NL();
		
		//CONSIDER: comments green.
		//	Now users don't see '//' if replaced with '...'.
		//	But for me it never was a problem.
	}
	
	public void Close(KScintilla sci) {
		var li = _lb.Items.OfType<_LbItem>().FirstOrDefault(o => o.sci == sci);
		if (li != null) _Close(li);
	}
	
	void _Close(_LbItem li) {
		if (li == _li && _lb.Items.Count is int n && n > 1) {
			int i = _lb.Items.IndexOf(li);
			_lb.SelectedIndex = i == n - 1 ? i - 1 : i + 1;
		}
		_lb.Items.Remove(li);
		_CloseSci(li.sci);
	}
	
	void _CloseSci(_KScintilla sci) {
		_grid.Children.Remove(sci);
		sci.Dispose();
		if (sci.IsFocused) Keyboard.Focus(_lb);
	}
	
	void _CloseAll() {
		var a = _lb.Items.OfType<_LbItem>().ToArray();
		_lb.Items.Clear();
		foreach (var v in a) _CloseSci(v.sci);
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
			AaNoMouseSetFocus = MButtons.Right | MButtons.Middle;
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
			aaaIndicatorDefine(Indicators.HiliteY, INDIC_STRAIGHTBOX, 0xffff00, alpha: 255, borderAlpha: 255, underText: true);
			aaaIndicatorDefine(Indicators.HiliteG, INDIC_STRAIGHTBOX, 0xC0FF60, alpha: 255, borderAlpha: 255, underText: true);
			aaaIndicatorDefine(Indicators.HiliteB, INDIC_GRADIENT, 0xA0A0FF, alpha: 255, underText: true);
			aaaIndicatorDefine(Indicators.FocusRect, INDIC_FULLBOX, 0x4169E1, alpha: 25, borderAlpha: 255, strokeWidth: _dpi / 96); //better than SC_ELEMENT_CARET_LINE_BACK/SCI_SETCARETLINEFRAME etc
			aaaIndicatorDefine(Indicators.Excluded, INDIC_STRIKE, 0xFF0000);
			
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
		
		protected override nint WndProc(wnd w, int msg, nint wp, nint lp) {
			switch (msg) {
			case Api.WM_MBUTTONDOWN: //close file
				int pos = Call(SCI_POSITIONFROMPOINTCLOSE, Math2.LoShort(lp), Math2.HiShort(lp));
				if (pos >= 0 && AaRangeDataGet(false, pos, out object o)) {
					var f = o switch { FileNode f1 => f1, CodeLink cl => cl.file, _ => null };
					if (f != null && (_openedFiles?.Contains(f) ?? false)) //close only if opened from this panel. Else may accidentally close (eg confuse middle/right button) and lose the undo history etc.
						App.Model.CloseFile(f, selectOther: true);
				}
				return 0;
			case Api.WM_CONTEXTMENU:
				if (kind == Found.SymbolRename) {
					int i = aaaCurrentPos8;
					if (0 != aaaIndicGetValue(Indicators.Link2, i)) {
						var v = aaaLineStartEndFromPos(false, i);
						if (0 == aaaIndicGetValue(Indicators.Excluded, i)) aaaIndicatorAdd(Indicators.Excluded, false, v.start..v.end);
						else aaaIndicatorClear(Indicators.Excluded, false, v.start..v.end);
					}
				}
				return 0;
			}
			
			return base.WndProc(w, msg, wp, lp);
		}
		
		protected override void AaOnSciNotify(ref SCNotification n) {
			//if (n.code == NOTIF.SCN_MODIFIED) print.it(n.code, n.modificationType, n.position, n.length); else if (n.code is not (NOTIF.SCN_PAINTED or NOTIF.SCN_STYLENEEDED)) print.it(n.code);
			switch (n.code) {
			case NOTIF.SCN_INDICATORRELEASE:
				if (n.modifiers == 0) _OnClickIndicLink(n.position);
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
				case ReplaceInFileLink k:
					if (_OpenLinkClicked(k.file, replaceAll: true)) {
						timer.after(10, _ => {
							if (Panels.Editor.ActiveDoc?.EFile != k.file) return;
							Panels.Find.ReplaceAllInEditorFromFoundPanel_(ttf);
						});
						//info: without timer sometimes does not set cursor pos correctly
					}
					break;
				case string s when s == "RAIF": //Replace all in all files
					Panels.Find.ReplaceAllInFilesFromFoundPanel_(ttf, files);
					break;
				case Action k:
					k.Invoke();
					break;
				default:
					Debug_.Print(o);
					break;
				}
			} else Debug_.Print(pos8);
		}
		
		bool _OpenLinkClicked(FileNode f, bool replaceAll = false) {
			if (App.Model.IsAlien(f)) return false;
			if (f.IsFolder) f.SelectSingle();
			else {
				//avoid opening the file in editor when invalid regex replacement
				if (replaceAll && !Panels.Find.ValidateReplacement_(ttf)) return false;
				
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
	
	class _LbItem : KListBoxItemWithImage {
		public readonly _KScintilla sci;
		public _LbItem(_KScintilla sci, object image, string text) : base(image, text) { this.sci = sci; }
	}
	
	public enum Found {
		Files,
		Text,
		SymbolReferences,
		SymbolRename,
		Repair
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
	
	public record class ReplaceInFileLink(FileNode file);
	
	/// <summary>
	/// Indices of indicators defined by this control.
	/// </summary>
	public static class Indicators {
		public const int HiliteY = 0, HiliteG = 1, HiliteB = 2, FocusRect = 3, Excluded = 4, Link = -16, Link2 = 18;
	}
	
	/// <summary>
	/// Indices of styles defined by this control.
	/// </summary>
	public static class Styles {
		public const int Bold = 30, Gray = 29, Green = 28; //31 STYLE_HIDDEN
	}
}
