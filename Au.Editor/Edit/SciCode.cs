using Au.Controls;
using static Au.Controls.Sci;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

partial class SciCode : KScintilla {
	readonly aaaFileLoaderSaver _fls;
	readonly FileNode _fn;

	public FileNode EFile => _fn;

	public override string ToString() => _fn.ToString();

	//margins. Initially 0-4. We can add more with SCI_SETMARGINS.
	public const int
		c_marginFold = 0,
		c_marginImages = 1,
		c_marginMarkers = 2, //breakpoints etc
		c_marginLineNumbers = 3,
		c_marginChanges = 4; //currently not impl, just adds some space between line numbers and text

	//markers. We can use 0-20. History 21-24. Folding 25-31.
	public const int c_markerUnderline = 0, c_markerBookmark = 1, c_markerBreakpoint = 2;
	//public const int c_markerStepNext = 3;

	//indicators. We can use 8-31. KScintilla can use 0-7. Draws indicators from smaller to bigger, eg error on warning.
	public const int
		c_indicImages = 8,
		c_indicFound = 9,
		c_indicRefs = 10,
		c_indicBraces = 11,
		c_indicDiagHidden = 17,
		c_indicInfo = 18,
		c_indicWarning = 19,
		c_indicError = 20;

	internal SciCode(FileNode file, aaaFileLoaderSaver fls) {
		_fn = file;
		_fls = fls;

		if (fls.IsBinary) AaInitReadOnlyAlways = true;
		if (fls.IsImage) AaInitImages = true;

		Name = "document";
	}

	protected override void AaOnHandleCreated() {
		Call(SCI_SETMODEVENTMASK, (int)(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT /*| MOD.SC_MOD_INSERTCHECK | MOD.SC_MOD_BEFOREINSERT*/
			//| MOD.SC_MOD_CHANGEFOLD //only when text modified, but not when user clicks +-
			));

		aaaMarginSetType(c_marginFold, SC_MARGIN_SYMBOL);
		aaaMarginSetType(c_marginImages, SC_MARGIN_SYMBOL);
		Call(SCI_SETMARGINWIDTHN, c_marginImages, 0);
		aaaMarginSetType(c_marginMarkers, SC_MARGIN_SYMBOL);
		aaaMarginSetType(c_marginLineNumbers, SC_MARGIN_NUMBER);
		//aaaSetMarginType(c_marginChanges, SC_MARGIN_SYMBOL);
		Call(SCI_SETMARGINWIDTHN, c_marginChanges, 4);
		Call(SCI_SETMARGINLEFT, 0, 2);

		Call(SCI_SETWRAPMODE, App.Settings.edit_wrap ? SC_WRAP_WORD : 0);
		Call(SCI_SETINDENTATIONGUIDES, SC_IV_REAL);
		Call(SCI_ASSIGNCMDKEY, Math2.MakeLparam(SCK_RETURN, SCMOD_CTRL | SCMOD_SHIFT), SCI_NEWLINE);

		//Call(SCI_SETXCARETPOLICY, CARET_SLOP | CARET_EVEN, 20); //does not work
		//Call(SCI_SETVIEWWS, 1); Call(SCI_SETWHITESPACEFORE, 1, 0xcccccc);

		Call(SCI_SETEXTRADESCENT, 1); //eg to avoid drawing fold separator lines on text

		Call(SCI_SETCARETLINEFRAME, 1);
		aaaSetElementColor(SC_ELEMENT_CARET_LINE_BACK, 0xEEEEEE);
		Call(SCI_SETCARETLINEVISIBLEALWAYS, 1);

		Call(SCI_SETMOUSEDWELLTIME, 500);

		//C# interprets Unicode newline characters NEL, LS and PS as newlines.
		//	If editor does not support it, line numbers in errors/warnings/stacktraces may be incorrect.
		//	Visual Studio supports it. VSCode no; when pasted, suggests to replace to normal.
		//	Scintilla supports it (SCI_SETLINEENDTYPESALLOWED) only if using C++ lexer.
		//		Modified: now supports by default.
		//	Ascii VT and FF are not interpreted as newlines by C# and Scintilla.

		CiStyling.TStyles.Settings.ToScintilla(this);
		_InicatorsInit();
		if (_fn.IsCodeFile) CiFolding.InitFolding(this);
		_InitDragDrop();
	}

	protected override void DestroyWindowCore(HandleRef hwnd) {
		CodeInfo._styling.DocHandleDestroyed(this);
		base.DestroyWindowCore(hwnd);
	}

	//Called by PanelEdit.aaOpen.
	internal void EInit_(byte[] text, bool newFile, bool noTemplate) {
		//if(Hwnd.Is0) CreateHandle();
		Debug.Assert(!AaWnd.Is0);

		bool editable = _fls.SetText(this, text);
		if (!EIsBinary) _fn.UpdateFileModTime();
		ESetLineNumberMarginWidth_();

		if (newFile) _openState = noTemplate ? _EOpenState.NewFileNoTemplate : _EOpenState.NewFileFromTemplate;
		else if (App.Model.OpenFiles.Contains(_fn)) _openState = _EOpenState.Reopen;

		if (_fn.IsCodeFile) CiStyling.DocTextAdded();

		App.Model.EditGoBack.OnPosChanged(this);

		//detect \r without '\n', because it is not well supported. Also NEL, LS, PS.
		if (editable) {
			bool badNewlines = false;
			for (int i = 0; i < text.Length - 1;) { //ends with '\0'
				switch (text[i++]) {
				case 13 when text[i] != 10: badNewlines = true; break;
				case 0xc2 when text[i] == 0x85: badNewlines = true; break;
				case 0xe2 when text[i] == 0x80 && text[i + 1] is 0xa8 or 0xa9: badNewlines = true; break;
				}
			}
			if (badNewlines) {
				print.it($@"<>Note: text of {_fn.Name} contains unusual line end characters. <+badNL s>Show<>, <+badNL h>hide<>, <+badNL f>fix<>.");
				if (!s_badNewlines) {
					s_badNewlines = true;
					Panels.Output.Scintilla.AaTags.AddLinkTag("+badNL", s1 => {
						var doc = Panels.Editor.ActiveDoc;
						if (doc != null) {
							if (s1.Starts('f')) {
								if (!doc.aaaIsReadonly) doc.aaaText = doc.aaaText.ReplaceLineEndings();
								//info: ReplaceLineEndings also replaces single \n and FF.
								//	SCI_CONVERTEOLS ignores NEL, LS, PS.
							} else {
								doc.Call(SCI_SETVIEWEOL, s1.Starts('s') ? 1 : 0);
							}
						}
					});
				}
			}
		}
	}
	static bool s_badNewlines;

	//protected override void Dispose(bool disposing)
	//{
	//	print.qm2.write($"Dispose disposing={disposing} IsHandleCreated={IsHandleCreated} Visible={Visible}");
	//	base.Dispose(disposing);
	//}

	internal void EOpenDocActivated() {
		App.Model.EditGoBack.OnPosChanged(this);
	}

	protected override void AaOnSciNotify(ref SCNotification n) {
		//if (test_) {
		//	switch (n.nmhdr.code) {
		//	case NOTIF.SCN_UPDATEUI:
		//	case NOTIF.SCN_NEEDSHOWN:
		//	case NOTIF.SCN_PAINTED:
		//	case NOTIF.SCN_FOCUSIN:
		//	case NOTIF.SCN_FOCUSOUT:
		//	case NOTIF.SCN_DWELLSTART:
		//	case NOTIF.SCN_DWELLEND:
		//		break;
		//	case NOTIF.SCN_MODIFIED:
		//		print.it(n.nmhdr.code, n.modificationType);
		//		break;
		//	default:
		//		print.it(n.nmhdr.code);
		//		break;
		//	}
		//}


		switch (n.code) {
		case NOTIF.SCN_SAVEPOINTLEFT:
			App.Model.Save.TextLater();
			break;
		case NOTIF.SCN_SAVEPOINTREACHED:
			//never mind: we should cancel the 'save text later'
			break;
		case NOTIF.SCN_MODIFIED:
			//print.it("SCN_MODIFIED", n.modificationType, n.position, n.FinalPosition, aaaCurrentPos8, n.Text);
			//print.it(n.modificationType);
			//if(n.modificationType.Has(MOD.SC_PERFORMED_USER | MOD.SC_MOD_BEFOREINSERT)) {
			//	print.it($"'{n.Text}'");
			//	if(n.length == 2 && n.textUTF8!=null && n.textUTF8[0]=='\r' && n.textUTF8[1] == '\n') {
			//		Call(SCI_BEGINUNDOACTION); Call(SCI_ENDUNDOACTION);
			//	}
			//}
			if (n.modificationType.HasAny(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT)) {
				_modified = true;
				_TempRangeOnModifiedOrPosChanged(n.modificationType, n.position, n.length);
				App.Model.EditGoBack.OnTextModified(this, n.modificationType.Has(MOD.SC_MOD_DELETETEXT), n.position, n.length);
				if (CodeInfo.SciModified(this, n)) {
					_CodeModifiedAndCodeinfoOK();
				}
				Panels.Find.UpdateQuickResults();
				//} else if(n.modificationType.Has(MOD.SC_MOD_INSERTCHECK)) {
				//	//print.it(n.Text);
				//	//if(n.length==1 && n.textUTF8[0] == ')') {
				//	//	Call(Sci.SCI_SETOVERTYPE, _testOvertype = true);

				//	//}
				if (n.linesAdded != 0) ESetLineNumberMarginWidth_(onModified: true);
			}
			break;
		case NOTIF.SCN_CHARADDED:
			//print.it($"SCN_CHARADDED  {n.ch}  '{(char)n.ch}'");
			if (n.ch == '\n' /*|| n.ch == ';'*/) { //split scintilla Undo
				aaaAddUndoPoint();
			}
			if (n.ch != '\r' && n.ch <= 0xffff) { //on Enter we receive notifications for '\r' and '\n'
				CodeInfo.SciCharAdded(this, (char)n.ch);
			}
			break;
		case NOTIF.SCN_UPDATEUI:
			//print.it((uint)n.updated, _modified);
			if (0 != (n.updated & 1)) {
				if (_modified) _modified = false; else n.updated &= ~1; //ignore notifications when changed styling or markers
			}
			if (0 == (n.updated & 15)) break;
			if (0 != (n.updated & 3)) { //text (1), selection/click (2)
				_TempRangeOnModifiedOrPosChanged(0, 0, 0);
				if (0 != (n.updated & 2)) App.Model.EditGoBack.OnPosChanged(this);
				Panels.Editor.UpdateUI_EditEnabled_();
			}
			CodeInfo.SciUpdateUI(this, n.updated);
			break;
		case NOTIF.SCN_DWELLSTART:
			CodeInfo.SciMouseDwellStarted(this, n.position);
			break;
		case NOTIF.SCN_DWELLEND:
			CodeInfo.SciMouseDwellEnded(this);
			break;
		case NOTIF.SCN_MARGINCLICK:
			if (_fn.IsCodeFile) {
				CodeInfo.Cancel();
				if (n.margin == c_marginFold) {
					_FoldOnMarginClick(null, n.position);
				}
			}
			break;
		case NOTIF.SCN_STYLENEEDED:
			//print.it("SCN_STYLENEEDED");
			if (_fn.IsCodeFile) {
				EHideImages_(Call(SCI_GETENDSTYLED), n.position);
				Call(SCI_STARTSTYLING, n.position); //need this even if would not hide images
			} else {
				aaaSetStyled();
			}
			break;
			//case NOTIF.SCN_PAINTED:
			//	_Paint(true);
			//	break;
		}

		base.AaOnSciNotify(ref n);
	}
	bool _modified;

	protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		nint lResult = 0;
		handled = _WndProc((wnd)hwnd, msg, wParam, lParam, ref lResult);
		if (handled) return lResult;
		return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
	}

	bool _WndProc(wnd w, int msg, nint wparam, nint lparam, ref nint lresult) {
		switch (msg) {
		case Api.WM_CHAR: {
				int c = (int)wparam;
				if (c < 32) {
					if (c is not (9 or 10 or 13)) return true;
				} else {
					if (CodeInfo.SciBeforeCharAdded(this, (char)c)) return true;
				}
			}
			break;
		case Api.WM_MBUTTONDOWN:
			Api.SetFocus(w);
			return true;
		case Api.WM_RBUTTONDOWN: {
				//workaround for Scintilla bug: when right-clicked a margin, if caret or selection start is at that line, goes to the start of line
				POINT p = Math2.NintToPOINT(lparam);
				int margin = aaaMarginFromPoint(p, false);
				if (margin >= 0) {
					var selStart = aaaSelectionStart8;
					var (_, start, end) = aaaLineStartEndFromPos(false, aaaPosFromXY(false, p, false));
					if (selStart >= start && selStart <= end) return true;
					//do vice versa if the end of non-empty selection is at the start of the right-clicked line, to avoid comment/uncomment wrong lines
					if (margin == c_marginLineNumbers || margin == c_marginMarkers) {
						if (aaaSelectionEnd8 == start) aaaGoToPos(false, start); //clear selection above start
					}
				}
			}
			break;
		case Api.WM_CONTEXTMENU: {
				bool kbd = (int)lparam == -1;
				int margin = kbd ? -1 : aaaMarginFromPoint(Math2.NintToPOINT(lparam), true);
				switch (margin) {
				case -1:
					var m = new KWpfMenu();
					App.Commands[nameof(Menus.Edit)].CopyToMenu(m);
					m.Show(this, byCaret: kbd);
					break;
				case c_marginLineNumbers or c_marginMarkers or c_marginImages or c_marginChanges:
					ModifyCode.CommentLines(null, notSlashStar: true);
					break;
				case c_marginFold:
					int fold = popupMenu.showSimple("Folding: hide all|Folding: show all", owner: AaWnd) - 1; //note: no "toggle", it's not useful
					if (fold >= 0) Call(SCI_FOLDALL, fold);
					break;
				}
				return true;
			}
			//case Api.WM_PAINT:
			//	_Paint(false);
			//	break;
			//case Api.WM_PAINT: {
			//		using var p1 = perf.local();
			//		Call(msg, wparam, lparam);
			//		return true;
			//	}
		}

		//Call(msg, wparam, lparam);

		//in winforms version this was after base.WndProc. Now in hook cannot do it, therefore using async.
		//SHOULDDO: superclass and use normal wndproc instead of hook. Now possible various anomalies because of async.
		switch (msg) {
		//case Api.WM_MOUSEMOVE:
		//	CodeInfo.SciMouseMoved(this, Math2.LoShort(m.LParam), Math2.HiShort(m.LParam));
		//	break;
		case Api.WM_KILLFOCUS:
			//Dispatcher.InvokeAsync(() => CodeInfo.SciKillFocus(this));//no, dangerous
			CodeInfo.SciKillFocus(this);
			break;
			//case Api.WM_LBUTTONUP:
			//	//rejected. Sometimes I accidentally Ctrl+click and then wonder why it shows eg the github search dialog.
			//	if (Keyboard.Modifiers == ModifierKeys.Control && !aaaIsSelection) {
			//		Dispatcher.InvokeAsync(() => CiGoTo.GoToSymbolFromPos());
			//	}
			//	break;
		}

		return false;
	}

	protected override bool TranslateAcceleratorCore(ref System.Windows.Interop.MSG msg, ModifierKeys mod) {
		if (msg.message is Api.WM_KEYDOWN or Api.WM_SYSKEYDOWN) {
			var key = (KKey)msg.wParam;
			if (_ImageDeleteKey(key)) return true;
			if (CodeInfo.SciCmdKey(this, key, mod)) return true;
			switch ((key, mod)) {
			case (KKey.Enter, 0):
			case (KKey.Enter, ModifierKeys.Control | ModifierKeys.Shift):
				aaaAddUndoPoint();
				break;
			case (KKey.C, ModifierKeys.Control):
				ECopy();
				return true;
			case (KKey.V, ModifierKeys.Control):
				EPaste();
				return true;
			}
		}
		return base.TranslateAcceleratorCore(ref msg, mod);
	}

	protected override void OnGotFocus(RoutedEventArgs e) {
		if (!_noModelEnsureCurrentSelected) App.Model.EnsureCurrentSelected();
		base.OnGotFocus(e);
	}
	bool _noModelEnsureCurrentSelected;

	internal bool EIsUnsaved_ {
		get => _isUnsaved || 0 != Call(SCI_GETMODIFY);
		set {
			if (_isUnsaved = value) App.Model.Save.TextLater(1);
		}
	}
	bool _isUnsaved;

	public bool EIsBinary => _fls.IsBinary;

	//Called by PanelEdit.aaSaveText.
	internal bool ESaveText_() {
		Debug.Assert(!EIsBinary);
		if (EIsUnsaved_) {
			//print.qm2.write("saving");
			if (!App.Model.TryFileOperation(() => _fls.Save(this, _fn.FilePath, tempDirectory: _fn.IsExternal ? null : _fn.Model.TempDirectory))) return false;
			//info: with tempDirectory less noise for FileSystemWatcher (now removed, but anyway)
			_isUnsaved = false;
			Call(SCI_SETSAVEPOINT);
			_fn.UpdateFileModTime();
		}
		return true;
	}

	//Called by FileNode.OnAppActivatedAndThisIsOpen.
	internal void EFileModifiedExternally_() {
		Debug.Assert(!EIsBinary); //caller must check it
		if (!_fn.GetFileText(out var text) || text == this.aaaText) return;
		EReplaceTextGently(text);
		Call(SCI_SETSAVEPOINT);

		//rejected: print info. VS and VSCode reload silently.
		//if (this == Panels.Editor.ActiveDoc) print.it($"<>Info: file {_fn.SciLink()} has been reloaded because modified outside. You can Undo.");
	}

	//never mind: not called when zoom changes.
	internal void ESetLineNumberMarginWidth_(bool onModified = false) {
		int c = 4, lines = aaaLineCount;
		while (lines > 999) { c++; lines /= 10; }
		if (!onModified || c != _prevLineNumberMarginWidth) aaaMarginSetWidth(c_marginLineNumbers, _prevLineNumberMarginWidth = c, chars: true);
	}
	int _prevLineNumberMarginWidth;

	#region copy paste

	/// <summary>
	/// Called when copying (menu or Ctrl+C).
	/// Caller must not copy text to clipboard, and must not pass the event to Scintilla.
	/// </summary>
	public void ECopy(ECopyAs copyAs = ECopyAs.Text) {
		int i1 = aaaSelectionStart8, i2 = aaaSelectionEnd8, textLen = aaaLen8;
		if (textLen == 0) return;
		if (copyAs == ECopyAs.Text) {
			if (i2 != i1) Call(SCI_COPY);
		} else {
			bool isCS = _fn.IsCodeFile, isFragment = i2 != i1 && !(i1 == 0 && i2 == textLen);
			string s = isFragment ? aaaRangeText(false, i1, i2) : aaaText;
			if (isCS) s = _ImageRemoveScreenshots(s);
			switch (copyAs) {
			case ECopyAs.Forum:
				var b = new StringBuilder("[code]");
				if (isCS) {
					if (!isFragment) {
						var name = _fn.Name; if (name.RxIsMatch(@"(?i)^(Script|Class)\d*\.cs")) name = null;
						b.AppendFormat("// {0} \"{1}\"\r\n", _fn.IsScript ? "script" : "class", name);
					}
					s = CodeExporter.ExportForum(s);
				}
				s = b.Append(s).AppendLine("[/code]").ToString();
				break;
			case ECopyAs.HtmlSpanStyle or ECopyAs.HtmlSpanClass or ECopyAs.HtmlSpanClassCss:
				s = CodeExporter.ExportHtml(s, spanClass: copyAs != ECopyAs.HtmlSpanStyle, withCss: copyAs == ECopyAs.HtmlSpanClassCss);
				new clipboardData().AddText(s).AddHtml(s).SetClipboard();
				return;
			case ECopyAs.Markdown:
				s = $"```csharp\r\n{s}\r\n```\r\n";
				break;
				//case ECopyAs.TextWithoutScreenshots:
			}
			clipboard.text = s;
		}
	}

	public enum ECopyAs { Text, Forum, HtmlSpanStyle, HtmlSpanClassCss, HtmlSpanClass, Markdown, TextWithoutScreenshots }

	/// <summary>
	/// Called when pasting (menu or Ctrl+V). Inserts text, possibly with processed forum bbcode etc.
	/// Caller must not insert text, and must not pass the event to Scintilla.
	/// </summary>
	public void EPaste() {
		var s1 = clipboard.text; if (s1.NE()) return;

		var (isFC, text, name, isClass) = EIsForumCode_(s1, false);
		if (isFC) {
			string buttons = _fn.FileType != (isClass ? FNType.Class : FNType.Script)
				? "1 Create new file|0 Cancel"
				: "1 Create new file|2 Replace all text|3 Paste|0 Cancel";
			switch (dialog.show("Import C# file text from clipboard", "Source file: " + name, buttons, DFlags.CommandLinks, owner: this)) {
			case 1: //Create new file
				_NewFileFromForumCode(text, name, isClass);
				break;
			case 2: //Replace all text
				aaaSetText(text);
				break;
			case 3: //Paste
				CodeInfo.Pasting(this);
				aaaReplaceSel(text);
				break;
			} //rejected: option to rename this file
		} else {
			CodeInfo.Pasting(this);
			Call(SCI_PASTE); //not aaaReplaceSel, because can be SCI_SETMULTIPASTE etc
		}
	}

	internal static (bool yes, string text, string filename, bool isClass) EIsForumCode_(string s, bool newFile) {
		if (!s.RxMatch(@"^// (script|class) ""(.*?)""( |\R)", out var m)) return default;

		bool isClass = s[3] == 'c';
		s = s[m.End..];
		var name = m[2].Length > 0 ? m[2].Value : (isClass ? "Class1.cs" : "Script1.cs");

		if (newFile && dialog.showOkCancel("Import C# file from clipboard?", "Source file: " + name, owner: App.Hmain))
			_NewFileFromForumCode(s, name, isClass);

		return (true, s, name, isClass);
	}

	static void _NewFileFromForumCode(string text, string name, bool isClass) {
		App.Model.NewItem(isClass ? "Class.cs" : "Script.cs", null, name, text: new NewFileText(replaceTemplate: true, text));
	}

	#endregion

	#region indicators

	void _InicatorsInit() {
		if (!_fn.IsCodeFile) return;

		//workaround for: indicators too small if high DPI
		int style = _dpi < 144 ? INDIC_SQUIGGLEPIXMAP : INDIC_SQUIGGLE,
			strokeWidth = _dpi < 144 ? 100 : 200;
		//strokeWidth = _dpi < 144 ? 100 : _dpi < 192 ? 150 : 200;

		aaaIndicatorDefine(c_indicError, style, 0xff0000, strokeWidth: strokeWidth);
		aaaIndicatorDefine(c_indicWarning, style, 0x008000, strokeWidth: strokeWidth); //dark green
		aaaIndicatorDefine(c_indicInfo, INDIC_DIAGONAL, 0xc0c0c0, strokeWidth: strokeWidth);
		aaaIndicatorDefine(c_indicDiagHidden, INDIC_DOTS, 0xc0c0c0, strokeWidth: strokeWidth);
	}

	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
		base.OnDpiChanged(oldDpi, newDpi);
		if (!AaWnd.Is0) _InicatorsInit();
	}

	bool _indicHaveFound, _indicHaveDiag;

	internal void EInicatorsFound_(List<Range> a) {
		if (_indicHaveFound) {
			_indicHaveFound = false;
			aaaIndicatorClear(c_indicFound);
		}
		if (a == null || a.Count == 0) return;
		_indicHaveFound = true;

		foreach (var v in a) aaaIndicatorAdd(c_indicFound, true, v);
	}

	internal void EInicatorsDiag_(bool has) {
		if (_indicHaveDiag) {
			_indicHaveDiag = false;
			aaaIndicatorClear(c_indicDiagHidden);
			aaaIndicatorClear(c_indicInfo);
			aaaIndicatorClear(c_indicWarning);
			aaaIndicatorClear(c_indicError);
		}
		if (!has) return;
		_indicHaveDiag = true;
	}

	#endregion

	#region view

	[Flags]
	public enum EView { Wrap = 1, Images = 2 }

	internal static void EToggleView_call_from_menu_only_(EView what) {
		if (what.Has(EView.Wrap)) {
			App.Settings.edit_wrap ^= true;
			foreach (var v in Panels.Editor.OpenDocs) v.Call(SCI_SETWRAPMODE, App.Settings.edit_wrap ? SC_WRAP_WORD : 0);
		}
		if (what.Has(EView.Images)) {
			App.Settings.edit_noImages ^= true;
			foreach (var v in Panels.Editor.OpenDocs) v._ImagesOnOff();
		}

		//should not need this, because this func called from menu commands only.
		//	But somehow KMenuCommands does not auto change menu/toolbar checked state for Edit menu. Need to fix it.
		Panels.Editor.UpdateUI_EditView_();
	}

	void _CodeModifiedAndCodeinfoOK() {
		if (!_wpfPreview) return;
		s_timer1 ??= new(static t => {
			var doc = Panels.Editor.ActiveDoc;
			if (doc == t.Tag) doc._WpfPreviewRun(false);
		});
		s_timer1.Tag = this;
		s_timer1.After(500);
	}
	static timer s_timer1;
	static bool s_wpfPreviewInited;
	bool _wpfPreview;
	internal bool EIsWpfPreview => _wpfPreview;

	void _WpfPreviewRun(bool starting) {
		if (!_wpfPreview) return;
		CompileRun.RunWpfPreview(_fn, k => {
			bool hasWPF_PREVIEW = false;
			for (int i = k.m.GlobalCount; i < k.trees.Length; i++) {
				//print.it(m.CodeFiles[i]);
				if (!k.m.CodeFiles[i].code.Contains("WPF_PREVIEW")) continue;
				var cu = k.trees[i].GetCompilationUnitRoot();
				if (!cu.GetDirectives(d => d is IfDirectiveTriviaSyntax di && di.Condition.ToString() == "WPF_PREVIEW").Any()) continue;
				hasWPF_PREVIEW = true;
				break;
			}
			if (!hasWPF_PREVIEW) {
				if (starting) {
					print.it("""
<>To enable <help editor/Code editor>WPF preview<>, add #if WPF_PREVIEW with code that calls Preview(). Examples: <fold>
<code>
//code before b.ShowDialog
#if WPF_PREVIEW
b.Window.Preview();
#endif

//code near the start of the script file, when using dialog class DialogClass
#if WPF_PREVIEW
new DialogClass().Preview();
#endif

//code before dialog class DialogClass, when not using a script file
#if WPF_PREVIEW
class Program { static void Main() { new DialogClass().Preview(); }}
#endif
</code>
</fold>
""");
				}
				return false;
			}
			//print.it(k.compilation.GetDiagnostics());
			return !k.compilation.GetDiagnostics().Any(o => o.Severity == DiagnosticSeverity.Error);
		});
	}

	public static void WpfPreviewStartStop(MenuItem mi) {
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return;
		bool start = mi.IsChecked;
		if (start == doc._wpfPreview) return;
		doc._wpfPreview = start;

		if (start) doc._WpfPreviewRun(true);

		if (!s_wpfPreviewInited) {
			s_wpfPreviewInited = true;
			Panels.Editor.ActiveDocChanged += () => {
				mi.IsChecked = Panels.Editor.ActiveDoc?._wpfPreview ?? false;
			};
		}

		//update #if WPF_PREVIEW: styling, errors.
		CodeInfo.StopAndUpdateStyling();
	}

	#endregion

	#region acc

	protected override ERole AaAccessibleRole => ERole.DOCUMENT;

	protected override string AaAccessibleName => "document - " + _fn.DisplayName;

	protected override string AaAccessibleDescription => _fn.FilePath;

	#endregion
}
