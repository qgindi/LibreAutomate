//TODO: once crashed when replacing in files.
//	It's randon, may be hard to reproduce.
//	Replaced </msdn> with </ms> in Au project. Crashed in the middle.
//	Probably crashed while saving, because left one ~backup file. The normal file was saved OK.
//	Process ended quickly. No exception, no log event, no dump file.

using Au.Controls;
using Au.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Media;
using System.Windows.Data;

//CONSIDER: right-click "Find" - search backward. The same for "Replace" (reject "find next"). Rarely used.
//CONSIDER: option to replace and don't find next until next click. Eg Eclipse has buttons "Replace" and "Replace/Find". Or maybe delay to preview.

class PanelFind {
	KScintilla _tFind, _tReplace;
	KCheckNoBox _cCase, _cWord, _cRegex;
	Button _bFilter;
	KPopup _ttRegex, _ttNext;
	
	public PanelFind() {
		P.UiaSetName("Find panel");
		
		var b = new wpfBuilder(P).Columns(-1).Brush(SystemColors.ControlBrush);
		b.Options(modifyPadding: false, margin: new Thickness(2));
		b.AlsoAll((b, _) => { if (b.Last is Button k) k.Padding = new(1, 0, 1, 1); });
		
		KScintilla _AddTextbox(string name) {
			var k = new KScintilla { AaWrapLines = true };
			var border = b.Row(-1).xAddInBorder(k);
			border.Margin = new(2, 0, 2, 0);
			b.Name(name, true);
			k.AaHandleCreated += k => _tFindReplace_HandleCreated(k);
			return k;
		}
		
		b.Row(3);
		_tFind = _AddTextbox("Find_text");
		b.xAddSplitterH();
		_tReplace = _AddTextbox("Replace_text");
		
		b.R.StartGrid().Columns((-1, ..80), (-1, ..80), (-1, ..80), 0).Margin(top: 3);
		b.R.AddButton(out var bFind, "Find", _ => _FindNextInEditor()).Tooltip("Find next in editor.\nRight click - find previous.");
		bFind.MouseRightButtonUp += (_, _) => FindNextInEditor(true);
		b.AddButton(out var bReplace, "Replace", _ => _FindNextInEditor(replace: true)).Tooltip("Replace current found text in editor and find next.\nRight click - find next.");
		bReplace.MouseRightButtonUp += (_, _) => _FindNextInEditor();
		b.AddButton("Repl. all", _bReplaceAll_Click).Tooltip("Replace all in editor");
		
		b.R.AddButton("In files", _FindAllInFiles).Tooltip("Find text in files");
		b.StartStack();
		b.xAddButtonIcon(out _bFilter, "*Material.FolderSearchOutline" + Menus.green, _FilterMenu, "Let 'In files' search only in current project or root folder").Padding(1, 0, 1, 1);
		b.xAddButtonIcon("*EvaIcons.Options2" + Menus.green, _ => _Options(), "More options");
		
		var cmd1 = App.Commands[nameof(Menus.Edit.Navigate.Go_back)];
		b.xAddButtonIcon(Menus.iconBack, _ => Menus.Edit.Navigate.Go_back(), "Go back").Disabled(!cmd1.Enabled);
		var bBack = b.Last;
		cmd1.CanExecuteChanged += (o, e) => bBack.IsEnabled = cmd1.Enabled;
		
		b.xAddButtonIcon(Menus.iconRegex, _ => { _cRegex.IsChecked = true; _ShowRegexInfo(_tReplace.IsFocused ? _tReplace : _tFind); }, "Regex tool");
		b.xAddButtonIcon("*MaterialDesign.SavedSearch" + Menus.blue, _SavedSearches, "Saved searches");
		
		b.End();
		
		b.R.StartStack();
		b.Add(out _cCase, "Case").Tooltip("Match case").Checked(App.Settings.find_case);
		b.Add(out _cWord, "Word").Tooltip("Whole word").Checked(App.Settings.find_word);
		b.Add(out _cRegex, "Regex").Tooltip("Regular expression");
		b.End().End().End();
		
		foreach (var v in new[] { _cCase, _cWord, _cRegex }) v.CheckChanged += _CheckedChanged;
		
		P.IsVisibleChanged += (_, _) => {
			if (P.IsVisible) UpdateQuickResults();
			else {
				foreach (var d in Panels.Editor.OpenDocs) d.EInicatorsFound_(null);
				
				App.Settings.find_case = _cCase.IsChecked;
				App.Settings.find_word = _cWord.IsChecked;
			}
		};
	}
	
	public UserControl P { get; } = new();
	
	internal void CodeStylesChanged_() {
		if (!_tFind.IsLoaded) return;
		_SetCodeStyles(_tFind);
		_SetCodeStyles(_tReplace);
	}
	
	void _SetCodeStyles(KScintilla k) {
		CiStyling.TTheme.Current.ToScintilla(k, fontName: App.Settings.font_find.name, fontSize: App.Settings.font_find.size);
		k.aaaStyleForeColor(255, 0xa0a0a0); //watermark
		
		if (k.Parent is Border { Parent: Grid g } b) {
			double h = Dpi.Unscale(k.aaaLineHeight(), k._dpi);
			g.RowDefinitions[Grid.GetRow(b)].MinHeight = h + 3;
		}
	}
	
	#region control events
	
	void _tFindReplace_HandleCreated(KScintilla k) {
		_SetCodeStyles(k);
		
		k.aaaMarginSetWidth(1, 0);
		k.aaaMarginSetWidth(-1, 2);
		
		if (k == _tFind) k.AaTextChanged += _ => { _RegexStyling(); UpdateQuickResults(); };
		
		_SetWatermark(true);
		
		void _SetWatermark(bool set) {
			if (set) {
				k.aaaMarginSetWidth(-1, -4);
				k.Call(Sci.SCI_EOLANNOTATIONSETSTYLE, 0, 255);
				k.aaaSetString(Sci.SCI_EOLANNOTATIONSETTEXT, 0, k == _tFind ? "Find"u8 : "Replace"u8);
				k.Call(Sci.SCI_EOLANNOTATIONSETVISIBLE, 1);
			} else {
				k.Call(Sci.SCI_EOLANNOTATIONSETVISIBLE);
				k.aaaMarginSetWidth(-1, 2);
			}
		}
		
		k.MessageHook += (nint hwnd, int msg, nint wp, nint lp, ref bool handled) => {
			var w = (wnd)hwnd;
			if (msg == Api.WM_CONTEXTMENU) {
				var m = new popupMenu();
				m["Undo\tCtrl+Z", disable: 0 == k.Call(Sci.SCI_CANUNDO)] = o => k.Call(Sci.SCI_UNDO);
				m["Redo\tCtrl+Y", disable: 0 == k.Call(Sci.SCI_CANREDO)] = o => k.Call(Sci.SCI_REDO);
				m["Cut\tCtrl+X", disable: !k.aaaHasSelection] = o => k.Call(Sci.SCI_CUT);
				m["Copy\tCtrl+C", disable: !k.aaaHasSelection] = o => k.Call(Sci.SCI_COPY);
				m["Paste\tCtrl+V", disable: 0 == k.Call(Sci.SCI_CANPASTE)] = o => k.Call(Sci.SCI_PASTE);
				m["Select all\tCtrl+A"] = o => k.Call(Sci.SCI_SELECTALL);
				m["Clear\tM-click"] = o => k.aaaClearText();
				m["Recent\tM-click"] = o => _RecentPopupList(k);
				m.Show(owner: w);
			} else if (msg == Api.WM_SETFOCUS || msg == Api.WM_KILLFOCUS) {
				bool focus = msg == Api.WM_SETFOCUS;
				if (focus) _SetWatermark(false); else if (k.aaaLen8 == 0) _SetWatermark(true);
				if (_cRegex.IsChecked) {
					if (focus) {
						//use timer to avoid temporary focus problems, for example when tabbing quickly or closing active Regex window
						timer.after(70, _ => { if (k.AaWnd.IsFocused) _ShowRegexInfo(k, onFocus: true); });
					} else if (_regexWindow?.IsVisible is true) {
						timer.after(70, _ => {
							if (_regexWindow?.IsVisible is true && !_regexWindow.Hwnd.IsActive) {
								var c = Api.GetFocus();
								if (c != _tFind.AaWnd && c != _tReplace.AaWnd) _regexWindow.Hwnd.ShowL(false);
							}
						});
					}
				}
			} else if (msg == Api.WM_MBUTTONUP) {
				if (k.aaaLen8 > 0) k.aaaClearText(); else _RecentPopupList(k);
			}
			return 0;
		};
	}
	
	RegexWindow _regexWindow;
	string _regexTopic;
	
	void _ShowRegexInfo(KScintilla k, bool onFocus = false) {
		if (onFocus) {
			if (_regexWindow == null || _regexWindow.UserClosed) return;
		} else {
			_regexWindow ??= new RegexWindow();
			_regexWindow.UserClosed = false;
		}
		
		if (_regexWindow.Hwnd.Is0) {
			var r = P.RectInScreen();
			r.Offset(0, -20);
			_regexWindow.ShowByRect(App.Wmain, Dock.Right, r, true);
		} else _regexWindow.Hwnd.ShowL(true);
		
		_regexWindow.InsertInControl = k;
		
		bool replace = k == _tReplace;
		var s = _regexWindow.CurrentTopic;
		if (s == "replace") {
			if (!replace) _regexWindow.CurrentTopic = _regexTopic;
		} else if (replace) {
			_regexTopic = s;
			_regexWindow.CurrentTopic = "replace";
		}
	}
	
	unsafe void _RegexStyling() {
		if (!_cRegex.IsChecked) return;
		var s = _tFind.aaaText;
		if (s.NE()) return;
		var b = new byte[s.Length];
		RegexParser.GetScintillaStylingBytes16(s, PSFormat.Regexp, b);
		_tFind.aaaSetStyling(0, KScintilla.aaaConvertStylingBytesToUtf8(b, s));
	}
	
	void _CheckedChanged(object sender, RoutedEventArgs e) {
		if (sender == _cWord) {
			if (_cWord.IsChecked) _cRegex.IsChecked = false;
		} else if (sender == _cRegex) {
			if (_cRegex.IsChecked) {
				_cWord.IsChecked = false;
				
				_RegexStyling();
			} else {
				_regexWindow?.Close();
				_regexWindow = null;
				
				_tFind.Call(Sci.SCI_STARTSTYLING);
				_tFind.Call(Sci.SCI_SETSTYLING, _tFind.aaaLen8);
			}
		}
		UpdateQuickResults();
	}
	
	void _Options() {
		var b = new wpfBuilder("Find text options").WinSize(420);
		b.R.StartGrid<KGroupBox>("Find in files");
		b.R.Add("Search in", out ComboBox cbFileType, true).Items("All files|C# files (*.cs)|Other files").Select(_SearchIn);
		b.R.Add(
			new TextBlock() { TextWrapping = TextWrapping.Wrap, Text = "Skip files whose workspace paths match a wildcard from this list" },
			out TextBox tSkip,
			App.Settings.find_skip,
			row2: 0)
			.Multiline(100, TextWrapping.NoWrap)
			.Tooltip(@"Example:
*.exe
\FolderA\*
*\FolderB\*
//Comment");
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
		App.Settings.find_skip = tSkip.Text; _skip = null;
		App.Settings.find_parallel = cParallel.IsChecked;
		App.Settings.find_printSlow = tSlow.Text.ToInt();
		
		//rejected: support wildex in 'skip'. Not useful.
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
			_tFind.Call(Sci.SCI_SELECTALL); //often user wants to type new text
			return;
		}
		_tFind.aaaText = s;
	}
	
	/// <summary>
	/// Makes visible and sets find text = selected text of e.
	/// Supports KScintilla and TextBox. If other type or null or no selected text, just makes visible etc.
	/// </summary>
	public void CtrlF(FrameworkElement e) {
		string s = null;
		switch (e) {
		case KScintilla c:
			s = c.aaaSelectedText();
			break;
		case TextBox c:
			s = c.SelectedText;
			break;
		}
		CtrlF(s);
	}
	
	/// <summary>
	/// Called when changed find text or options. Also when activated another document.
	/// Async-updates find-hiliting in editor.
	/// </summary>
	public void UpdateQuickResults() {
		if (!P.IsVisible) return;
		
		_timerUQR ??= new timer(_ => {
			Panels.Editor.ActiveDoc?.EInicatorsFound_(_FindAllInEditor());
		});
		
		_timerUQR.After(150);
	}
	timer _timerUQR;
	
	internal class TextToFind_ {
		public string findText;
		public string replaceText;
		public regexp rx;
		public bool wholeWord;
		public bool matchCase;
		public int filter;
		
		public bool IsSameFindTextAndOptions(TextToFind_ ttf)
			=> ttf.findText == findText
			&& ttf.matchCase == matchCase
			&& ttf.wholeWord == wholeWord
			&& (ttf.rx != null) == (rx != null);
		//ignore filter
		
		public int OptionsInt {
			get {
				int r = matchCase ? 1 : 0;
				if (wholeWord) r |= 2; else if (rx != null) r |= 4;
				return r | (filter << 8);
			}
		}
	}
	
	bool _GetTextToFind(out TextToFind_ ttf, bool forReplace = false, bool noRecent = false, bool noErrorTooltip = false) {
		_ttRegex?.Close();
		string text = _tFind.aaaText; if (text.NE()) { ttf = null; return false; }
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
		if (forReplace) ttf.replaceText = _tReplace.aaaText;
		
		if (!noRecent) _AddToRecent(ttf);
		
		if (forReplace && (Panels.Editor.ActiveDoc?.aaaIsReadonly ?? true)) return false;
		return true;
	}
	
	static void _FindAllInString(string text, TextToFind_ ttf, Action<int, int> found) {
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
	
	public void FindNextInEditor(bool back) {
		if (back) {
			//this was added later. Since PCRE can't search backward, let's use _FindAllInEditor.
			_ttNext?.Close();
			var doc = Panels.Editor.ActiveDoc; if (doc == null) return;
			var a = _FindAllInEditor();
			int pos = doc.aaaSelectionStart8;
			for (int i = a.Count; --i >= 0;) {
				if (a[i].End.Value <= pos) {
					_SelectFound(true, doc, a[i].Start.Value, a[i].End.Value);
					_rxReplPos = default;
					return;
				}
			}
			SystemSounds.Asterisk.Play();
		} else {
			_FindNextInEditor();
		}
	}
	
	void _FindNextInEditor(bool replace = false) {
		if (!_GetTextToFind(out var ttf, forReplace: replace)) return;
		_FindNextInEditor2(ttf, replace);
	}
	
	void _FindNextInEditor2(TextToFind_ ttf, bool replace) {
		_ttNext?.Close();
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return;
		var text = doc.aaaText; if (text.Length == 0) return;
		int i, to, len = 0, from8 = replace ? doc.aaaSelectionStart8 : doc.aaaSelectionEnd8, from = doc.aaaPos16(from8), to8 = doc.aaaSelectionEnd8;
		RXMatch rm = null;
		bool retryFromStart = false, retryRx = false;
		g1:
		if (ttf.rx != null) {
			//this code solves this problem: now would not match if the regex contains \K etc, because 'from' is different
			if (replace && _rxReplPos.doc == doc && _rxReplPos.from8 == from8 && _rxReplPos.to8 == to8 && _rxReplPos.text == text && ttf.IsSameFindTextAndOptions(_rxReplPos.ttf)) {
				i = from8; to = to8; rm = _rxReplPos.rm;
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
			_rxReplPos = default;
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
			_FindNextInEditor2(ttf, false);
		} else if (_SelectFound(false, doc, i, to)) {
			_rxReplPos = (ttf, doc, text, i, to, rm);
		}
	}
	
	bool _SelectFound(bool back, SciCode doc, int start, int end) {
		if (CiStyling.IsProtected(doc, start, end)) {
			int skip = back ? CiStyling.SkipProtected(doc, start, true) : CiStyling.SkipProtected(doc, end, false);
			doc.aaaGoToPos(false, skip);
			return false;
		}
		
		App.Model.EditGoBack.RecordNext();
		doc.aaaSelect(false, start, end, true);
		return true;
	}
	
	(TextToFind_ ttf, SciCode doc, string text, int from8, int to8, RXMatch rm) _rxReplPos;
	
	void _bReplaceAll_Click(WBButtonClickArgs e) {
		if (!_GetTextToFind(out var ttf, forReplace: true)) return;
		_ReplaceAllInEditor(ttf);
	}
	
	//Can replace in inactive SciCode too.
	void _ReplaceAllInEditor(TextToFind_ ttf, SciCode doc = null, SciUndo undoInFiles = null) {
		doc ??= Panels.Editor.ActiveDoc;
		if (doc.aaaIsReadonly) return;
		var text = doc.aaaText;
		var a = _FindReplacements(ttf, text);
		if (doc.EFile.ReplaceAllInText(text, a, out var text2))
			undoInFiles?.RifAddFile(doc, text, text2, a);
	}
	
	void _ReplaceAllInClosedFile(TextToFind_ ttf, FileNode f, SciUndo undoInFiles) {
		if (!f.GetCurrentText(out string text, silent: null)) return;
		var a = _FindReplacements(ttf, text);
		if (f.ReplaceAllInText(text, a, out var text2))
			undoInFiles?.RifAddFile(f, text, text2, a);
	}
	
	List<StartEndText> _FindReplacements(TextToFind_ ttf, string text) {
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
	
	internal bool ValidateReplacement_(TextToFind_ ttf/*, FileNode file*/) {
		//FUTURE: add regexp.IsValidReplacement and use it here.
		//if (ttf.rx != null
		//	&& file.GetCurrentText(out var s, silent: true)
		//	&& ttf.rx.Match(s, out RXMatch m)
		//	&& !_TryExpandRegexReplacement(m, _tReplace.Text, out _)
		//	) return false;
		return true;
	}
	
	List<Range> _FindAllInEditor() {
		if (!_GetTextToFind(out var ttf, noRecent: true, noErrorTooltip: true)) return null;
		var text = Panels.Editor.ActiveDoc?.aaaText; if (text.NE()) return null;
		List<Range> a = new(); //all found in editor text
		_FindAllInString(text, ttf, (start, end) => a.Add(start..end));
		return a;
	}
	
	#endregion
	
	#region in files
	
	int _SearchIn => Math.Clamp(App.Settings.find_searchIn, 0, 2);
	
	WildcardList _skip;
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
				if ((_skip ??= new(App.Settings.find_skip)).IsMatch(v.ItemPath, true)) continue;
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
					.Link2(new PanelFound.CodeLink(f, ar[0].Start.Value, isHeading: true))
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
	
	internal void ReplaceAllInEditorFromFoundPanel_(TextToFind_ ttf) {
		if (!_CanReplaceFromFoundPanel(ttf)) return;
		_ReplaceAllInEditor(ttf);
	}
	
	bool _CanReplaceFromFoundPanel(TextToFind_ ttf) {
		bool ok = ttf.findText == _tFind.aaaText
			&& ttf.matchCase == _cCase.IsChecked
			&& ttf.wholeWord == _cWord.IsChecked
			&& (ttf.rx != null) == _cRegex.IsChecked
			&& ttf.filter == _filter;
		if (!ok) {
			dialog.show(null, "Please click 'In files' to update the Found panel.", owner: P);
			return false;
		}
		ttf.replaceText = _tReplace.aaaText;
		_AddToRecent(ttf, onlyRepl: true);
		return true;
	}
	
	internal void ReplaceAllInFilesFromFoundPanel_(TextToFind_ ttf, List<FileNode> files) {
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
	
	#region recent, saved
	
	string _recentPrevFind, _recentPrevReplace;
	int _recentPrevOptions;
	
	//temp is false when clicked a button, true when changed the find text or a checkbox.
	void _AddToRecent(TextToFind_ ttf, bool onlyRepl = false) {
		if (!onlyRepl) {
			int options = ttf.OptionsInt;
			if (ttf.findText != _recentPrevFind || options != _recentPrevOptions) _Add(false, _recentPrevFind = ttf.findText, _recentPrevOptions = options);
		}
		if (!ttf.replaceText.NE() && ttf.replaceText != _recentPrevReplace) _Add(true, _recentPrevReplace = ttf.replaceText, 0);
		
		static void _Add(bool replace, string text, int options) {
			if (text.Length > 3000) return;
			var ri = new _RecentItem(text, options);
			var a = _RecentLoad(replace);
			if (a.NE_()) a = new _RecentItem[] { ri };
			else if (a[0].text == text) a[0] = ri;
			else {
				for (int i = a.Length; --i > 0;) if (a[i].text == text) a = a.RemoveAt(i); //no duplicates
				if (a.Length > 29) a = a[0..29]; //limit count
				a = a.InsertAt(0, ri);
			}
			_RecentSave(replace, a);
		}
	}
	
	void _RecentPopupList(KScintilla k) {
		bool replace = k == _tReplace;
		var a = _RecentLoad(replace);
		if (a.NE_()) return;
		var p = new KPopupListBox { PlacementTarget = k };
		var c = p.Control;
		foreach (var v in a) c.Items.Add(v);
		p.OK += o => _RecentUse(o as _RecentItem, k);
		P.Dispatcher.InvokeAsync(() => p.IsOpen = true);
	}
	
	void _RecentUse(_RecentItem r, KScintilla k) {
		k.aaaText = r.text;
		if (k == _tFind) {
			int g = r.options;
			_cCase.IsChecked = 0 != (g & 1);
			_cWord.IsChecked = 0 != (g & 2);
			_cRegex.IsChecked = 0 != (g & 4);
			_SetFilter(g >> 8 & 3);
		}
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
			x[i][0] = a[i].options.ToS();
			x[i][1] = a[i].text;
		}
		x.Save(_RecentFile(replace));
	}
	
	static string _RecentFile(bool replace) => AppSettings.DirBS + (replace ? "Recent replace.csv" : "Recent find.csv");
	
	record _RecentItem(string text, int options) {
		public override string ToString() => text.Limit(200); //ListBox item display text
	}
	
	record _SavedItem(string name, string text, int options, string repl) : _RecentItem(text, options) {
		public override string ToString() => name; //ListBox item display text
	}
	
	void _SavedSearches(WBButtonClickArgs ba) {
		string file = AppSettings.DirBS + "Saved searches.csv";
		
		Panels.Editor.SyncEditorTextIfFileIs(file, true);
		
		var p = new KPopupListBox { PlacementTarget = ba.Button };
		var c = p.Control;
		
		c.Items.Add("Save this search...");
		c.Items.Add("Edit saved searches");
		c.Items.Add(new Separator());
		
		if (filesystem.exists(file, true).File) {
			var x = csvTable.load(file); //note: no try/catch. If bad format, shows an exception mesage box with error description.
			if (x.ColumnCount < 4) x.ColumnCount = 4;
			foreach (var r in x.Rows) if (r[0].NE()) r[0] = r[2]?.Limit(200);
			foreach (var r in x.Rows.OrderBy(o => o[0], StringComparer.OrdinalIgnoreCase)) {
				if (!r[1].NE()) c.Items.Add(new _SavedItem(r[0], r[2], r[1].ToInt(), r[3]));
			}
		}
		
		(c.ItemContainerStyle = new(typeof(ListBoxItem))).Setters.Add(new Setter(ListBoxItem.ToolTipProperty, new Binding("text")));
		
		p.OK += o => {
			if (o is _SavedItem g) {
				if (g.repl is string repl) { _tReplace.Focus(); _tReplace.aaaText = repl; }
				_tFind.Focus();
				_RecentUse(g, _tFind);
			} else if (o == c.Items[0]) { //Save this
				if (!_GetTextToFind(out var ttf, noRecent: true)) return; //info: returns false if text empty or regex invalid (shows tooltip)
				string replace = _tReplace.aaaText.NullIfEmpty_();
				
				var b = new wpfBuilder("Save this search").WinSize(400);
				b.R.Add("Name (optional)", out TextBox tName).Focus();
				b.R.Add(out KCheckBox cAndReplace, "Include the Replace field").Disabled(replace.NE());
				b.R.AddOkCancel();
				b.Window.ShowInTaskbar = false;
				if (!b.ShowDialog(App.Wmain)) return;
				
				var x = filesystem.exists(file, true).File ? csvTable.load(file) : new();
				if (x.ColumnCount < 4) x.ColumnCount = 4;
				if (x.RowCount == 0) { x.AddRow(); x.AddRow("CSV: name, flags, findText, replaceText"); }
				string name = tName.Text.NullIfEmpty_(), text = ttf.findText, options = ttf.OptionsInt.ToS();
				if (!cAndReplace.IsChecked) replace = null;
				string[] dup = null;
				var dupName = name is null ? null : x.Rows.FirstOrDefault(o => o[0]?.Eqi(name) == true);
				var dupText = x.Rows.FirstOrDefault(o => o[2]?.Eqi(text) == true);
				if (dupName != null || dupText != null) {
					switch (dialog.show("Replace existing?", $"A search with this {(dupName is null ? "text" : "name")} is already saved.", "1 Replace|2 Add new|0 Cancel", owner: App.Hmain)) {
					case 1: dup = dupName ?? dupText; break;
					case 2: break;
					default: return;
					}
				}
				if (dup != null) (dup[0], dup[1], dup[2], dup[3]) = (name, options, text, replace);
				else x.InsertRow(0, name, options, text, replace);
				x.Save(file);
				Panels.Editor.SyncEditorTextIfFileIs(file, false);
			} else if (o == c.Items[1]) { //Edit saved searches
				App.Model.ImportLinkOrOpen(file);
			}
		};
		
		p.IsOpen = true;
	}
	
	#endregion
}
