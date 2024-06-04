using Au.Controls;
using Au.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Media;
using System.Windows.Documents;

//CONSIDER: right-click "Find" - search backward. The same for "Replace" (reject "find next"). Rarely used.
//CONSIDER: option to replace and don't find next until next click. Eg Eclipse has buttons "Replace" and "Replace/Find". Or maybe delay to preview.

class PanelFind {
	TextBox _tFind, _tReplace;
	KCheckBox _cCase, _cWord, _cRegex;
	Button _bFilter;
	KPopup _ttRegex, _ttNext;
	WatermarkAdorner _adorner1;

	public PanelFind() {
		P.UiaSetName("Find panel");

		var b = new wpfBuilder(P).Columns(-1).Brush(SystemColors.ControlBrush);
		b.Options(modifyPadding: false, margin: new Thickness(2));
		b.AlsoAll((b, _) => { if (b.Last is Button k) k.Padding = new(1, 0, 1, 1); });
		
		wpfBuilder _AddTextbox(out TextBox tb) =>
			b.Row((-1, 19..)).Add<AdornerDecorator>()
				.Add(out tb, flags: WBAdd.ChildOfLast)
				.Margin(-1, -1, -1, -1)
				.Multiline(wrap: TextWrapping.Wrap);
		
		_AddTextbox(out _tFind).Name("Find_text", true).Watermark(out _adorner1, "Find");
		b.xAddSplitterH();
		_AddTextbox(out _tReplace).Name("Replace_text", true).Watermark("Replace");
		SetFont_(false);
		
		b.R.StartGrid().Columns((-1, ..80), (-1, ..80), (-1, ..80), 0).Margin(top: 3);
		b.R.AddButton("Find", _bFind_Click).Tooltip("Find next in editor");
		b.AddButton(out var bReplace, "Replace", _bReplace_Click).Tooltip("Replace current found text in editor and find next.\nRight click - find next.");
		bReplace.MouseRightButtonUp += (_, _) => _bFind_Click(null);
		b.AddButton("Repl. all", _bReplaceAll_Click).Tooltip("Replace all in editor");

		b.R.AddButton("In files", _FindAllInFiles).Tooltip("Find text in files");
		b.StartStack();
		_bFilter = b.xAddButtonIcon("*Material.FolderSearchOutline" + Menus.green, _FilterMenu, "Let 'In files' search only in current project or root folder");
		b.Padding(1, 0, 1, 1);
		b.xAddButtonIcon("*EvaIcons.Options2" + Menus.green, _ => _Options(), "More options");

		var cmd1 = App.Commands[nameof(Menus.Edit.Navigate.Go_back)];
		var bBack = b.xAddButtonIcon(Menus.iconBack, _ => Menus.Edit.Navigate.Go_back(), "Go back");
		b.Disabled(!cmd1.Enabled);
		cmd1.CanExecuteChanged += (o, e) => bBack.IsEnabled = cmd1.Enabled;

		b.End();

		b.R.Add(out _cCase, "Case").Tooltip("Match case");
		b.Add(out _cWord, "Word").Tooltip("Whole word");
		b.Add(out _cRegex, "Regex").Tooltip("Regular expression.\nF1 - Regex tool and help.");
		b.End().End();

		P.IsVisibleChanged += (_, _) => {
			Panels.Editor.ActiveDoc?.EInicatorsFound_(P.IsVisible ? _aEditor : null);
		};

		_tFind.TextChanged += (_, _) => UpdateQuickResults();

		foreach (var v in new[] { _tFind, _tReplace }) {
			v.AcceptsTab = true;
			v.IsInactiveSelectionHighlightEnabled = true;
			v.GotKeyboardFocus += _tFindReplace_KeyboardFocus;
			v.LostKeyboardFocus += _tFindReplace_KeyboardFocus;
			v.ContextMenu = new KWpfMenu();
			v.ContextMenuOpening += _tFindReplace_ContextMenuOpening;
			v.PreviewMouseUp += (o, e) => { //use up, else up will close popup. Somehow on up ClickCount always 1.
				if (e.ChangedButton == MouseButton.Middle) {
					var tb = o as TextBox;
					if (tb.Text.NE()) _RecentPopupList(tb); else tb.Clear();
				}
			};
		}

		foreach (var v in new[] { _cCase, _cWord, _cRegex }) v.CheckChanged += _CheckedChanged;

		P.KeyDown += (_, e) => {
			switch (e.Key, Keyboard.Modifiers) {
			case (Key.F1, 0):
				if (_cRegex.IsChecked) _ShowRegexInfo((e.OriginalSource as TextBox) ?? _tFind, F1: true);
				break;
			default: return;
			}
			e.Handled = true;
		};
	}

	public UserControl P { get; } = new();
	
	internal void SetFont_(bool changed) {
		System.Windows.Media.FontFamily ff = new (App.Settings.font_find.name);
		double fs = App.Settings.font_find.size * 4 / 3;
		for (int i = 0; i < 2; i++) {
			var c = i == 0 ? _tFind : _tReplace;
			c.FontFamily = ff;
			c.FontSize = fs;
			if (changed && c.Parent is AdornerDecorator p) p.AdornerLayer.Update();
		}
	}

	#region control events

	private void _tFindReplace_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
		var c = sender as TextBox;
		var m = c.ContextMenu as KWpfMenu;
		m.Items.Clear();
		m["_Undo\0" + "Ctrl+Z", c.CanUndo] = o => c.Undo();
		m["_Redo\0" + "Ctrl+Y", c.CanRedo] = o => c.Redo();
		m["Cu_t\0" + "Ctrl+X", c.SelectionLength > 0] = o => c.Cut();
		m["_Copy\0" + "Ctrl+C", c.SelectionLength > 0] = o => c.Copy();
		m["_Paste\0" + "Ctrl+V", Clipboard.ContainsText()] = o => c.Paste();
		m["_Select All\0" + "Ctrl+A"] = o => c.SelectAll();
		m["Cl_ear\0" + "M-click"] = o => c.Clear();
		m["Rece_nt\0" + "M-click"] = o => _RecentPopupList(c);
	}

	private void _tFindReplace_KeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
		if (!_cRegex.IsChecked) return;
		var tb = sender as TextBox;
		if (e.NewFocus == tb) {
			//use timer to avoid temporary focus problems, for example when tabbing quickly or closing active Regex window
			timer.after(70, _ => { if (tb.IsFocused) _ShowRegexInfo(tb, F1: false); });
		} else if ((_regexWindow?.IsVisible ?? false)) {
			timer.after(70, _ => {
				if ((_regexWindow?.IsVisible ?? false) && !_regexWindow.Hwnd.IsActive) {
					var c = Keyboard.FocusedElement;
					if (c != _tFind && c != _tReplace) _regexWindow.Hwnd.ShowL(false);
				}
			});
		}
	}

	private void _CheckedChanged(object sender, RoutedEventArgs e) {
		if (sender == _cWord) {
			if (_cWord.IsChecked) _cRegex.IsChecked = false;
		} else if (sender == _cRegex) {
			if (_cRegex.IsChecked) {
				_cWord.IsChecked = false;
				_adorner1.Text = "Find  (F1 - regex tool)";
			} else {
				_regexWindow?.Close();
				_regexWindow = null;
				_adorner1.Text = "Find";
			}
		}
		UpdateQuickResults();
	}

	RegexWindow _regexWindow;
	string _regexTopic;

	void _ShowRegexInfo(TextBox tb, bool F1) {
		if (F1) {
			_regexWindow ??= new RegexWindow();
			_regexWindow.UserClosed = false;
		} else {
			if (_regexWindow == null || _regexWindow.UserClosed) return;
		}

		if (_regexWindow.Hwnd.Is0) {
			var r = P.RectInScreen();
			r.Offset(0, -20);
			_regexWindow.ShowByRect(App.Wmain, Dock.Right, r, true);
		} else _regexWindow.Hwnd.ShowL(true);

		_regexWindow.InsertInControl = tb;

		bool replace = tb == _tReplace;
		var s = _regexWindow.CurrentTopic;
		if (s == "replace") {
			if (!replace) _regexWindow.CurrentTopic = _regexTopic;
		} else if (replace) {
			_regexTopic = s;
			_regexWindow.CurrentTopic = "replace";
		}
	}

	private void _bFind_Click(WBButtonClickArgs e) {
		if (!_GetTextToFind(out var ttf)) return;
		_FindNextInEditor(ttf, false);
	}

	private void _bReplace_Click(WBButtonClickArgs e) {
		if (!_GetTextToFind(out var ttf, forReplace: true)) return;
		_FindNextInEditor(ttf, true);
	}

	void _Options() {
		var b = new wpfBuilder("Find text options").WinSize(350);
		b.R.StartGrid<KGroupBox>("Find in files");
		b.R.Add("Search in", out ComboBox cbFileType, true).Items("All files|C# files (*.cs)|Other files").Select(_SearchIn);
		b.R.Add(
			new TextBlock() { TextWrapping = TextWrapping.Wrap, Text = "Skip files where path in workspace matches a wildcard from this list" },
			out TextBox tSkip,
			string.Join("\r\n", _SkipWildex),
			row2: 0)
			.Multiline(100, TextWrapping.NoWrap)
			.Tooltip(@"Example:
*.exe
\FolderA\*
*\FolderB\*");
		b.R.Add(out KCheckBox cParallel, "Load files in parallel threads").Checked(App.Settings.find_parallel)
			.Tooltip("""
Load files in multiple threads simultaneously.
Makes much faster if disk is SSD, but much slower if HDD. Always faster if files are in the OS file cache. This feature can be useful if the workspace contains >1000 files.
To see maximal speed difference, before searching clear the OS file cache or restart the computer. The search time is displayed at the bottom of results if there are slow files; the 'Slow files' list does not reflect the actual speed gain.
This setting also is used by 'Find references' etc.
""");
		int iSlow = App.Settings.find_printSlow; string sSlow = iSlow > 0 ? iSlow.ToS() : null;
		b.R.StartStack().Add("Print file load+search times >=", out TextBox tSlow, sSlow).Width(50).Add<Label>("ms").End();
		b.End();
		b.R.AddOkCancel();
		b.End();
		b.Window.ShowInTaskbar = false;
		if (!b.ShowDialog(App.Wmain)) return;
		App.Settings.find_searchIn = cbFileType.SelectedIndex;
		App.Settings.find_skip = tSkip.Text; _aSkipWildcards = null;
		App.Settings.find_parallel = cParallel.IsChecked;
		App.Settings.find_printSlow = tSlow.Text.ToInt();

		//FUTURE: option to use cache to make faster.
		//	Now, if many files, first time can be very slow because of AV eg Windows Defender.
		//	To make faster, I added Windows Defender exclusion for cs file type. Remove when testing cache etc.
		//	When testing WD impact, turn off/on its real-time protection and restart this app.
		//	For cache use SQLite database in App.Model.CacheDirectory. Add text of files of size eg < 100 KB.

		//rejected: support wildex in 'skip'. Not useful.
		//	code here:
		//		b.R.Add(
		//			new TextBlock() {
		//				TextWrapping = TextWrapping.Wrap,
		//				Text = "Skip files where path in workspace matches a wildcard expression from this list"
		//			},
		//			out TextBox tSkip,
		//			string.Join("\r\n", _SkipWildex),
		//			row2: 0)
		//			.Multiline(100, TextWrapping.NoWrap)
		//			.Tooltip(@"Example:
		//*.exe
		//\FolderA\*
		//*\FolderB\*
		//**r regex")
		//			.Validation(e => {
		//				string s1 = null;
		//				try { foreach (var v in (e as TextBox).Text.Lines(true)) new wildex(s1 = v); }
		//				catch (ArgumentException e1) { return $"{e1.Message}\n{s1}"; }
		//				return null;
		//			});
		//	code in the 'find' function:
		//wildex[] aWildex = _SkipWildex is var sw && sw.Length != 0 ? sw.Select(o => new wildex(o, noException: true)).ToArray() : null;
		//foreach (var v in folder.Descendants()) {
		//	...
		//	if (aWildex != null) {
		//		var path = v.ItemPath;
		//		if (aWildex.Any(o => o.Match(path))) continue;
		//	}
		//	aSearchInFiles.Add(v);
		//}

	}

	void _FilterMenu(WBButtonClickArgs e) {
		int f = _filter;
		var m = new popupMenu();
		m.AddRadio("Search in entire workspace", f == 0, _ => f = 0, image: _FilterImage(0));
		m.AddRadio("Search in current root folder", f == 1, _ => f = 1, image: _FilterImage(1));
		m.AddRadio("Search in current @Project", f == 2, _ => f = 2, image: _FilterImage(2));
		m.Show();
		_SetFilter(f);
	}
	int _filter; //0 workspace, 1 root folder, 2 project or root folder

	static string _FilterImage(int f) => "*Material.FolderSearchOutline" + (f == 0 ? Menus.green : f == 1 ? Menus.orange : Menus.red);

	void _SetFilter(int f) {
		if (f != _filter) {
			_filter = f;
			_bFilter.Content = ImageUtil.LoadWpfImageElement(_FilterImage(f));
			_bFilter.ToolTip = f switch { 0 => "Search in entire workspace", 1 => "Search in current root folder", _ => "Search in current @Project" };
		}
	}

	#endregion

	#region common

	/// <summary>
	/// Makes visible and sets find text = s (should be selected text of a control; can be null/"").
	/// </summary>
	public void CtrlF(string s/*, bool findInFiles = false*/) {
		Panels.PanelManager[P].Visible = true;
		_tFind.Focus();
		if (s.NE()) {
			_tFind.SelectAll(); //often user wants to type new text
			return;
		}
		_tFind.Text = s;
		//_tFind.SelectAll(); //no, somehow WPF makes selected text gray like disabled when non-focused
		//if (findInFiles) _FindAllInFiles(false); //rejected. Not so useful.
	}

	/// <summary>
	/// Makes visible and sets find text = selected text of e.
	/// Supports KScintilla and TextBox. If other type or null or no selected text, just makes visible etc.
	/// </summary>
	public void CtrlF(FrameworkElement e/*, bool findInFiles = false*/) {
		string s = null;
		switch (e) {
		case KScintilla c:
			s = c.aaaSelectedText();
			break;
		case TextBox c:
			s = c.SelectedText;
			break;
		}
		CtrlF(s/*, findInFiles*/);
	}

	//rejected. Could be used for global keyboard shortcuts, but currently they work only if the main window is active.
	///// <summary>
	///// Makes visible and sets find text = selected text of focused control.
	///// </summary>
	//public void aaCtrlF() => aaCtrlF(FocusManager.GetFocusedElement(App.Wmain));

	/// <summary>
	/// Called when changed find text or options. Also when activated another document.
	/// Async-updates find-hiliting in editor.
	/// </summary>
	public void UpdateQuickResults() {
		if (!P.IsVisible) return;

		_timerUQR ??= new timer(_ => {
			_FindAllInEditor();
			Panels.Editor.ActiveDoc?.EInicatorsFound_(_aEditor);
		});

		_timerUQR.After(150);
	}
	timer _timerUQR;

	internal class _TextToFind {
		public string findText;
		public string replaceText;
		public regexp rx;
		public bool wholeWord;
		public bool matchCase;
		public int filter;

		public bool IsSameFindTextAndOptions(_TextToFind ttf)
			=> ttf.findText == findText
			&& ttf.matchCase == matchCase
			&& ttf.wholeWord == wholeWord
			&& (ttf.rx != null) == (rx != null);
		//ignore filter
	}

	bool _GetTextToFind(out _TextToFind ttf, bool forReplace = false, bool noRecent = false, bool noErrorTooltip = false) {
		_ttRegex?.Close();
		string text = _tFind.Text; if (text.NE()) { ttf = null; return false; }
		ttf = new() { findText = text, matchCase = _cCase.IsChecked, filter = _filter };
		try {
			if (_cRegex.IsChecked) {
				var fl = RXFlags.MULTILINE;
				if (!ttf.matchCase) fl |= RXFlags.CASELESS;
				ttf.rx = new regexp(ttf.findText, flags: fl);
			} else {
				ttf.wholeWord = _cWord.IsChecked;
			}
		}
		catch (ArgumentException e) { //regexp ctor throws if invalid
			if (!noErrorTooltip) TUtil.InfoTooltip(ref _ttRegex, _tFind, e.Message);
			return false;
		}
		if (forReplace) ttf.replaceText = _tReplace.Text;

		if (!noRecent) _AddToRecent(ttf);

		if (forReplace && (Panels.Editor.ActiveDoc?.aaaIsReadonly ?? true)) return false;
		return true;
	}

	static void _FindAllInString(string text, _TextToFind ttf, Action<int, int> found) {
		_SkipImages si = new(text);

		if (ttf.rx != null) {
			foreach (var g in ttf.rx.FindAllG(text, 0)) {
				if (si.Skip(g.Start, g.End)) continue;
				found(g.Start, g.End);
			}
		} else {
			for (int i = 0; i < text.Length; i += ttf.findText.Length) {
				i = ttf.wholeWord ? text.FindWord(ttf.findText, i.., !ttf.matchCase, "_") : text.Find(ttf.findText, i, !ttf.matchCase);
				if (i < 0) break;
				int to = i + ttf.findText.Length;
				if (si.Skip(i, to)) continue;
				found(i, to);
			}
		}
	}

	/// <summary>
	/// Finds hidden images and determines whether a found text range is in an image.
	/// </summary>
	struct _SkipImages {
		string _text;
		int _imageStart, _imageEnd;

		public _SkipImages(string text) { _text = text; }

		/// <summary>
		/// Returns true if <i>start</i> or <i>end</i> is inside a hidden @"image:Base64" or /*image:Base64*/.
		/// </summary>
		public bool Skip(int start, int end) {
			while (start >= _imageEnd) _FindImage();
			if (end > _imageStart) {
				if (end < _imageEnd || start > _imageStart) return true;
			}
			return false;
		}

		void _FindImage() {
			for (int i = _imageEnd + 2; i < _text.Length; i += 6) {
				i = _text.Find("image:", i);
				if (i < 0) break;
				if (s_rx.Match(_text, 0, out RXGroup g, (i - 2)..)) {
					bool isString = _text[i - 1] == '"';
					_imageStart = i + (isString ? 6 : -2);
					_imageEnd = g.End - (isString ? 1 : 0);
					return;
				}
			}
			_imageStart = _imageEnd = int.MaxValue;
		}

		static regexp s_rx = new(@"@""image:[A-Za-z0-9/+]{40,}=*""|/\*image:[A-Za-z0-9/+]{40,}=*\*/", RXFlags.ANCHORED);
	}

	#endregion

	#region in editor

	void _FindNextInEditor(_TextToFind ttf, bool replace) {
		_ttNext?.Close();
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return;
		var text = doc.aaaText; if (text.Length == 0) return;
		int i, to, len = 0, from8 = replace ? doc.aaaSelectionStart8 : doc.aaaSelectionEnd8, from = doc.aaaPos16(from8), to8 = doc.aaaSelectionEnd8;
		RXMatch rm = null;
		bool retryFromStart = false, retryRx = false;
		g1:
		if (ttf.rx != null) {
			//this code solves this problem: now will not match if the regex contains \K etc, because 'from' is different
			if (replace && _lastFind.doc == doc && _lastFind.from8 == from8 && _lastFind.to8 == to8 && _lastFind.text == text && ttf.IsSameFindTextAndOptions(_lastFind.ttf)) {
				i = from8; to = to8; rm = _lastFind.rm;
				goto g2;
			}

			if (ttf.rx.Match(text, out rm, from..)) {
				i = rm.Start;
				len = rm.Length;
				if (i == from && len == 0 && !(replace | retryRx | retryFromStart)) {
					if (++i > text.Length) i = -1;
					else {
						if (i < text.Length) if (text.Eq(i - 1, "\r\n") || char.IsSurrogatePair(text, i - 1)) i++;
						from = i; retryRx = true; goto g1;
					}
				}
				if (len == 0) doc.Focus();
			} else i = -1;
		} else {
			i = ttf.wholeWord ? text.FindWord(ttf.findText, from.., !ttf.matchCase, "_") : text.Find(ttf.findText, from, !ttf.matchCase);
			len = ttf.findText.Length;
		}
		if (i < 0) {
			SystemSounds.Asterisk.Play();
			_lastFind.ttf = null;
			if (retryFromStart || from8 == 0) return;
			from = 0; retryFromStart = true; replace = false;
			goto g1;
		}
		if (retryFromStart) TUtil.InfoTooltip(ref _ttNext, _tFind, "Info: searching from start.");
		to = doc.aaaPos8(i + len);
		i = doc.aaaPos8(i);
		g2:
		if (replace && i == from8 && to == to8) {
			var repl = ttf.replaceText;
			if (rm != null) if (!_TryExpandRegexReplacement(rm, repl, out repl)) return;
			//doc.aaaReplaceRange(i, to, repl); //also would need to set caret pos = to
			doc.aaaReplaceSel(repl);
			_FindNextInEditor(ttf, false);
		} else {
			if (CiStyling.IsProtected(doc, i, to)) {
				//print.it("hidden");
				//if (1 != dialog.show("Select hidden text?", "The found text is in a hidden text range. Do you want to select it?", "Yes|No", owner: Base, defaultButton: 2)) {
				doc.aaaGoToPos(false, CiStyling.SkipProtected(doc, to));
				return;
				//}
			}

			App.Model.EditGoBack.RecordNext();
			doc.aaaSelect(false, i, to, true);

			_lastFind.ttf = ttf;
			_lastFind.doc = doc;
			_lastFind.text = text;
			_lastFind.from8 = i;
			_lastFind.to8 = to;
			_lastFind.rm = rm;
		}
	}

	(_TextToFind ttf, SciCode doc, string text, int from8, int to8, RXMatch rm) _lastFind;

	private void _bReplaceAll_Click(WBButtonClickArgs e) {
		if (!_GetTextToFind(out var ttf, forReplace: true)) return;
		_ReplaceAllInEditor(ttf);
	}

	//Can replace in inactive SciCode too.
	void _ReplaceAllInEditor(_TextToFind ttf, SciCode doc = null, SciUndo undoInFiles = null) {
		doc ??= Panels.Editor.ActiveDoc;
		if (doc.aaaIsReadonly) return;
		var text = doc.aaaText;
		var a = _FindReplacements(ttf, text);
		if (doc.EFile.ReplaceAllInText(text, a, out var text2))
			undoInFiles?.RifAddFile(doc, text, text2, a);
	}

	void _ReplaceAllInClosedFile(_TextToFind ttf, FileNode f, SciUndo undoInFiles) {
		if (!f.GetCurrentText(out string text, silent: null)) return;
		var a = _FindReplacements(ttf, text);
		if (f.ReplaceAllInText(text, a, out var text2))
			undoInFiles?.RifAddFile(f, text, text2, a);
	}

	List<StartEndText> _FindReplacements(_TextToFind ttf, string text) {
		List<StartEndText> a = new();
		var repl = ttf.replaceText;
		if (ttf.rx != null) {
			if (ttf.rx.FindAll(text, out var ma)) {
				_SkipImages si = new(text);
				foreach (var m in ma) {
					if (si.Skip(m.Start, m.End)) continue;
					if (!_TryExpandRegexReplacement(m, repl, out var r)) break;
					a.Add(new(m.Start, m.End, r));
				}
			}
		} else {
			_FindAllInString(text, ttf, (start, end) => a.Add(new(start, end, repl)));
		}
		return a;
	}

	bool _TryExpandRegexReplacement(RXMatch m, string repl, out string result) {
		try {
			result = m.ExpandReplacement(repl);
			return true;
		}
		catch (Exception e) {
			TUtil.InfoTooltip(ref _ttRegex, _tReplace, e.Message);
			result = null;
			return false;
		}
	}

	internal bool ValidateReplacement_(_TextToFind ttf/*, FileNode file*/) {
		//FUTURE: add regexp.IsValidReplacement and use it here.
		//if (ttf.rx != null
		//	&& file.GetCurrentText(out var s, silent: true)
		//	&& ttf.rx.Match(s, out RXMatch m)
		//	&& !_TryExpandRegexReplacement(m, _tReplace.Text, out _)
		//	) return false;
		return true;
	}

	List<Range> _aEditor = new(); //all found in editor text

	void _FindAllInEditor() {
		_aEditor.Clear();
		if (!_GetTextToFind(out var ttf, noRecent: true, noErrorTooltip: true)) return;
		var text = Panels.Editor.ActiveDoc?.aaaText; if (text.NE()) return;
		_FindAllInString(text, ttf, (start, end) => _aEditor.Add(start..end));
	}

	#endregion

	#region in files

	int _SearchIn => Math.Clamp(App.Settings.find_searchIn, 0, 2);

	string[] _SkipWildex => _aSkipWildcards ??= (App.Settings.find_skip ?? "").Lines(true);
	string[] _aSkipWildcards;
	readonly string[] _aSkipImagesEtc = [".png", ".bmp", ".jpg", ".jpeg", ".gif", ".tif", ".tiff", ".ico", ".cur", ".ani", ".snk", ".dll"];

	async void _FindAllInFiles(WBButtonClickArgs e) {
		if (_cancelTS != null) {
			_cancelTS.Cancel();
			return;
		}

		//using var p1 = perf.local();
		if (!_GetTextToFind(out var ttf)) return;

		App.Model.Save.AllNowIfNeed(); //save text of current document

		e.Button.IsEnabled = false;
		var cancelTimer = timer.after(1000, _ => { if (_cancelTS != null) { e.Button.Content = "Stop"; e.Button.IsEnabled = true; } });
		try {
			using var workingState = Panels.Found.Prepare(PanelFound.Found.Text, ttf.findText, out var b);

			const int c_markerFile = 0, c_markerInfo = 1;
			if (workingState.NeedToInitControl) {
				var k = workingState.Scintilla;
				k.aaaMarkerDefine(c_markerFile, Sci.SC_MARK_BACKGROUND, backColor: 0xC0E0A0);
				k.aaaMarkerDefine(c_markerInfo, Sci.SC_MARK_BACKGROUND, backColor: 0xEEE8AA);
			}

			FileNode folder = App.Model.Root;
			if (_filter > 0 && Panels.Editor.ActiveDoc?.EFile is FileNode fn) {
				if (_filter == 2 && fn.FindProject(out var proj, out _, ofAnyScript: true)) folder = proj;
				else folder = fn.AncestorsFromRoot(noRoot: true).FirstOrDefault() ?? folder;
			}

			List<(FileNode f, string s, int time, int len)> aSearchInFiles = new();
			int searchIn = _SearchIn;
			foreach (var v in folder.Descendants()) {
				if (v.IsFolder) continue;
				if (v.IsCodeFile) {
					if (searchIn == 2) continue; //0 all, 1 C#, 2 other
				} else {
					if (searchIn == 1) continue;
					if (v.Name.Ends(true, _aSkipImagesEtc) > 0) continue;
				}
				if (_SkipWildex.Length > 0 && v.ItemPath.Like(true, _SkipWildex) > 0) continue;
				aSearchInFiles.Add((v, v.FilePath, 0, 0));
			}

			List<(FileNode f, Range[] ar, string text, int i)> aResults = new();
			long timeStarted = perf.ms;

			//p1.Next();
			_cancelTS = new CancellationTokenSource();
			await Task.Run(() => {
				var ctoken = _cancelTS.Token;
				var po = new ParallelOptions { CancellationToken = ctoken };
				if (App.Settings.find_parallel) {
					Parallel.For(0, aSearchInFiles.Count, po, static () => new List<Range>(),
						(j, ps, ar) => {
							ref var v = ref aSearchInFiles.Ref(j);
							long t1 = perf.mcs;
							var text = FileNode.GetFileTextLL(v.s);
							_File(v.f, text, ar, j);
							v.time += (int)((perf.mcs - t1) / 100);
							v.len = text.Lenn();
							return ar;
						}, static _ => { });
				} else {
					for (int from = 0, i = 0; i < aSearchInFiles.Count; from = i) {
						for (int sumLength = 0; i < aSearchInFiles.Count && sumLength < 4_000_000; i++) { //load max 8 MB of text
							ref var v = ref aSearchInFiles.Ref(i);
							long t1 = perf.mcs;
							v.s = FileNode.GetFileTextLL(v.s);
							v.time = (int)((perf.mcs - t1) / 100);
							sumLength += v.len = v.s.Lenn();
							ctoken.ThrowIfCancellationRequested();
						}
						Parallel.For(from, i, po, static () => new List<Range>(),
							(j, ps, ar) => {
								ref var v = ref aSearchInFiles.Ref(j);
								long t1 = perf.mcs;
								_File(v.f, v.s, ar, j);
								v.time += (int)((perf.mcs - t1) / 100);
								v.s = null; //GC
								return ar;
							}, static _ => { });
						//need the Parallel in case of a very slow regex. Makes faster even if not regex.
					}
				}

				void _File(FileNode f, string text, List<Range> ar, int i) {
					if (text.NE() || text.Contains('\0')) return;

					ar.Clear();
					_FindAllInString(text, ttf, (start, end) => ar.Add(start..end));

					if (ar.Count > 0) {
						lock (aResults) {
							aResults.Add((f, ar.ToArray(), text, i));
						}
					}
				}
			});
			//p1.Next();

			int nFound = aResults.Sum(o => o.ar.Length);
			bool limited = nFound > 100_000;
			foreach (var (f, ar, text, _) in aResults.OrderBy(o => o.i)) {
				//file
				var path = f.ItemPath;
				b.Marker(c_markerFile)
					.Link2(new PanelFound.CodeLink(f, ar[0].Start.Value))
					.Gray(path.AsSpan(0..^f.Name.Length))
					.B(f.Name);
				int ns = 120 - path.Length * 7 / 4; if (ns > 0) b.Text(new string(' ', ns));
				b.Link_();
				if (!limited) b.Text("    ").Link(new PanelFound.ReplaceInFileLink(f), "Replace");
				if (f.IsExternal) b.Green("    //external");
				b.NL();
				//found text instances in that file
				if (limited) continue;
				for (int i = 0; i < ar.Length; i++) {
					var range = ar[i];
					PanelFound.AppendFoundLine(b, f, text, range.Start.Value, range.End.Value, workingState, displayFile: false);
				}
			}

			b.Marker(c_markerInfo);
			if (aResults.Count > 0) {
				b.Text($"Found {nFound} in {aResults.Count} files.    ");
				if (!limited) b.Link("RAIF", "Replace all...");
			} else b.Text("Not found.");
			b.NL();

			if (folder != App.Model.Root) b.Marker(c_markerInfo).Text($"Searched only in folder '{folder.Name}'.").NL();
			if (searchIn > 0) b.Marker(c_markerInfo).Link2(new Action(_Options), $"Searched only in {(searchIn == 1 ? "C#" : "non-C#")} files.").NL();

			if (App.Settings.find_printSlow > 0) {
				bool once = false;
				foreach (var v in aSearchInFiles) {
					int t = v.time / 10;
					if (t < App.Settings.find_printSlow) continue;
					if (!once) {
						once = true;
						b.Marker(c_markerInfo)
							.Link2(new Action(_Options), $"Searched in {aSearchInFiles.Count} files. Time: {perf.ms - timeStarted} ms. Slow files:").NL();
					}
					b.Text($"{t} ms ").Link(v.f, v.f.ItemPath).Text($" , length {v.len}\r\n");
				}
			}

			//p1.Next();
			Panels.Found.SetFindInFilesResults(workingState, b, ttf, aResults.Select(o => o.f).ToList());
		}
		catch (OperationCanceledException) { }
		finally {
			_cancelTS?.Dispose();
			_cancelTS = null;
			cancelTimer.Stop();
			e.Button.Content = "In files";
			e.Button.IsEnabled = true;
		}
	}
	CancellationTokenSource _cancelTS;

	internal void ReplaceAllInEditorFromFoundPanel_(_TextToFind ttf) {
		if (!_CanReplaceFromFoundPanel(ttf)) return;
		_ReplaceAllInEditor(ttf);
	}

	bool _CanReplaceFromFoundPanel(_TextToFind ttf) {
		bool ok = ttf.findText == _tFind.Text
			&& ttf.matchCase == _cCase.IsChecked
			&& ttf.wholeWord == _cWord.IsChecked
			&& (ttf.rx != null) == _cRegex.IsChecked
			&& ttf.filter == _filter;
		if (!ok) {
			dialog.show(null, "Please click 'In files' to update the Found panel.", owner: P);
			return false;
		}
		ttf.replaceText = _tReplace.Text;
		_AddToRecent(ttf, onlyRepl: true);
		return true;
	}

	internal void ReplaceAllInFilesFromFoundPanel_(_TextToFind ttf, List<FileNode> files) {
		if (!ValidateReplacement_(ttf)) return; //avoid opening files in editor when invalid regex replacement
		if (!_CanReplaceFromFoundPanel(ttf)) return;

		bool haveExternal = files.Any(o => o.IsExternal);

		switch (dialog.show("Replace text in files", "Replaces text in all files displayed in the Found panel.",
			haveExternal ? "1 Replace all|2 Replace all except external|0 Cancel" : "1 Replace all|0 Cancel",
			flags: /*DFlags.CommandLinks |*/ DFlags.CenterMouse,
			owner: App.Hmain)) {
		case 1:
			if (haveExternal) { //remove duplicate files (two links pointing to the same file). Else possible confusion eg disabled Undo.
				HashSet<FileId> hs = new();
				files = files.Where(o => !o.IsExternal || !filesystem.more.getFileId(o.FilePath, out var u) || hs.Add(u)).ToList();
			}
			break;
		case 2:
			files = files.Where(o => !o.IsExternal).ToList();
			break;
		default: return;
		}

		var progress = App.Hmain.TaskbarButton;
		var undoInFiles = SciUndo.OfWorkspace;
		try {
			undoInFiles.StartReplaceInFiles();
			progress.SetProgressState(WTBProgressState.Normal);

			for (int i = 0; i < files.Count; i++) {
				progress.SetProgressValue(i, files.Count);
				var f = files[i];
				if (f.OpenDoc != null) {
					_ReplaceAllInEditor(ttf, f.OpenDoc, undoInFiles);
				} else if (!f.DontSave) {
					_ReplaceAllInClosedFile(ttf, f, undoInFiles);
				}
			}

		}
		finally {
			undoInFiles.FinishReplaceInFiles($"replace text '{ttf.findText.Limit(50)}' with '{ttf.replaceText.Limit(50)}'");
			progress.SetProgressState(WTBProgressState.NoProgress);
			CodeInfo.FilesChanged();
		}
	}

	#endregion

	#region recent

	string _recentPrevFind, _recentPrevReplace;
	int _recentPrevOptions;

	//temp is false when clicked a button, true when changed the find text or a checkbox.
	void _AddToRecent(_TextToFind ttf, bool onlyRepl = false) {
		if (!onlyRepl) {
			int k = ttf.matchCase ? 1 : 0; if (ttf.wholeWord) k |= 2; else if (ttf.rx != null) k |= 4;
			k |= _filter << 8;
			if (ttf.findText != _recentPrevFind || k != _recentPrevOptions) _Add(false, _recentPrevFind = ttf.findText, _recentPrevOptions = k);
		}
		if (!ttf.replaceText.NE() && ttf.replaceText != _recentPrevReplace) _Add(true, _recentPrevReplace = ttf.replaceText, 0);

		static void _Add(bool replace, string text, int options) {
			if (text.Length > 3000) return;
			var ri = new _RecentItem(text, options);
			var a = _RecentLoad(replace);
			if (a.NE_()) a = new _RecentItem[] { ri };
			else if (a[0].t == text) a[0] = ri;
			else {
				for (int i = a.Length; --i > 0;) if (a[i].t == text) a = a.RemoveAt(i); //no duplicates
				if (a.Length > 29) a = a[0..29]; //limit count
				a = a.InsertAt(0, ri);
			}
			_RecentSave(replace, a);
		}
	}

	void _RecentPopupList(TextBox tb) {
		bool replace = tb == _tReplace;
		var a = _RecentLoad(replace);
		if (a.NE_()) return;
		var p = new KPopupListBox { PlacementTarget = tb };
		var k = p.Control;
		foreach (var v in a) k.Items.Add(v);
		p.OK += o => {
			var r = o as _RecentItem;
			tb.Text = r.t;
			if (!replace) {
				int k = r.o;
				_cCase.IsChecked = 0 != (k & 1);
				_cWord.IsChecked = 0 != (k & 2);
				_cRegex.IsChecked = 0 != (k & 4);
				_SetFilter(k >> 8 & 3);
			}
		};
		P.Dispatcher.InvokeAsync(() => p.IsOpen = true);
	}

	static _RecentItem[] _RecentLoad(bool replace) {
		var file = _RecentFile(replace);
		var x = filesystem.exists(file, true).File ? csvTable.load(file) : null;
		if (x == null) return null;
		var a = new _RecentItem[x.RowCount];
		for (int i = 0; i < a.Length; i++) {
			a[i] = new(x[i][1], x[i][0].ToInt());
		}
		return a;
	}

	static void _RecentSave(bool replace, _RecentItem[] a) {
		var x = new csvTable { ColumnCount = 2, RowCount = a.Length };
		for (int i = 0; i < a.Length; i++) {
			x[i][0] = a[i].o.ToS();
			x[i][1] = a[i].t;
		}
		x.Save(_RecentFile(replace));
	}

	static string _RecentFile(bool replace) => AppSettings.DirBS + (replace ? "Recent replace.csv" : "Recent find.csv");

	record _RecentItem(string t, int o) {
		public override string ToString() => t.Limit(200); //ListBox item display text
	}

	#endregion
}
