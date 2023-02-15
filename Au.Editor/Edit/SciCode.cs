//#define TRACE_TEMP_RANGES

using Au.Controls;
using static Au.Controls.Sci;
using System.Windows.Input;
using System.Windows.Controls;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Text.RegularExpressions;
using System.Windows;

partial class SciCode : KScintilla {
	readonly aaaFileLoaderSaver _fls;
	readonly FileNode _fn;

	public FileNode EFile => _fn;

	public override string ToString() => _fn.ToString();

	//margins. Initially 0-4. We can add more with SCI_SETMARGINS.
	public const int c_marginFold = 0;
	public const int c_marginImages = 1;
	public const int c_marginMarkers = 2; //breakpoints etc
	public const int c_marginLineNumbers = 3;
	public const int c_marginChanges = 4; //currently not impl, just adds some space between line numbers and text

	//markers. We can use 0-24. Folding 25-31.
	public const int c_markerUnderline = 0, c_markerBookmark = 1, c_markerBreakpoint = 2;
	//public const int c_markerStepNext = 3;

	//indicators. We can use 8-31. Lexers use 0-7. Draws indicators from smaller to bigger, eg error on warning.
	public const int c_indicFind = 8, c_indicImages = 9, c_indicDiagHidden = 17, c_indicInfo = 18, c_indicWarning = 19, c_indicError = 20;

	//#if DEBUG
	//	public const int c_indicTest = 21;
	//	internal void TestHidden_() {
	//		string code = aaaText;
	//		int start8 = code.Find("/*image:")+7;
	//		int end8 = code.Find("*/");

	//		byte style = 27;
	//		aaaStyleHidden(style, true);

	//		var b = new byte[end8-start8];
	//		Array.Fill(b, style);

	//		Call(SCI_STARTSTYLING, start8);
	//		unsafe { fixed (byte* bp = b) Call(SCI_SETSTYLINGEX, b.Length, bp); }
	//	}

	//	internal void TestIndicators_() {
	//		Call(SCI_INDICSETFORE, c_indicTest, 0x008000);
	//		Call(SCI_INDICSETSTYLE, c_indicTest, INDIC_BOX);
	//		aaaIndicatorClear(c_indicTest);
	//		int start = aaaSelectionStart8, end = aaaSelectionEnd8;
	//		aaaIndicatorAdd(false, c_indicTest, start..end, 1);
	//	}
	//#endif

	//static int _test;

	internal SciCode(FileNode file, aaaFileLoaderSaver fls) {
		//if(_test++==1) Tag = "test";

		//_edit = edit;
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

		aaaSetMarginType(c_marginFold, SC_MARGIN_SYMBOL);
		aaaSetMarginType(c_marginImages, SC_MARGIN_SYMBOL);
		Call(SCI_SETMARGINWIDTHN, c_marginImages, 0);
		aaaSetMarginType(c_marginMarkers, SC_MARGIN_SYMBOL);
		aaaSetMarginType(c_marginLineNumbers, SC_MARGIN_NUMBER);
		//aaaSetMarginType(c_marginChanges, SC_MARGIN_SYMBOL);
		Call(SCI_SETMARGINWIDTHN, c_marginChanges, 4);
		Call(SCI_SETMARGINLEFT, 0, 2);

		_InicatorsInit();

		Call(SCI_SETWRAPMODE, App.Settings.edit_wrap ? SC_WRAP_WORD : 0);
		Call(SCI_ASSIGNCMDKEY, Math2.MakeLparam(SCK_RETURN, SCMOD_CTRL | SCMOD_SHIFT), SCI_NEWLINE);

		if (_fn.IsCodeFile) {
			Call(SCI_SETEXTRADESCENT, 1); //eg to avoid drawing fold separator lines on text

			Call(SCI_SETCARETLINEFRAME, 1);
			Call(SCI_SETELEMENTCOLOUR, SC_ELEMENT_CARET_LINE_BACK, 0xEEEEEE);
			Call(SCI_SETCARETLINEVISIBLEALWAYS, 1);

			//C# interprets Unicode newline characters NEL, LS and PS as newlines. Visual Studio too.
			//	Scintilla and C++ lexer support it, but by default it is disabled.
			//	If disabled, line numbers in errors/warnings/stacktraces may be incorrect.
			//	Ascii VT and FF are not interpreted as newlines by C# and Scintilla.
			//	Not tested, maybe this must be set for each document in the control.
			//	Scintilla controls without C++ lexer don't support it.
			//		But if we temporarily set C++ lexer for <code>, newlines are displayed in whole text.
			//	Somehow this disables <fold> tag, therefore now not used for output etc.
			Call(SCI_SETLINEENDTYPESALLOWED, 1);

			Call(SCI_SETMOUSEDWELLTIME, 500);

			CiStyling.DocHandleCreated(this);

			//Call(SCI_ASSIGNCMDKEY, 3 << 16 | 'C', SCI_COPY); //Ctrl+Shift+C = raw copy

			//aaaStyleFont(STYLE_CALLTIP, "Calibri");
			//aaaStyleBackColor(STYLE_CALLTIP, 0xf8fff0);
			//aaaStyleForeColor(STYLE_CALLTIP, 0);
			//Call(SCI_CALLTIPUSESTYLE);
		} else {
			aaaStyleFont(STYLE_DEFAULT, "Consolas", 9);
			aaaStyleClearAll();
		}

		aaaStyleForeColor(STYLE_INDENTGUIDE, 0xcccccc);
		Call(SCI_SETINDENTATIONGUIDES, SC_IV_REAL);

		//Call(SCI_SETXCARETPOLICY, CARET_SLOP | CARET_EVEN, 20); //does not work

		//Call(SCI_SETVIEWWS, 1); Call(SCI_SETWHITESPACEFORE, 1, 0xcccccc);

		_InitDragDrop();

		//base.aaOnHandleCreated();
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

		if (_fn.IsCodeFile) CiStyling.DocTextAdded(this, newFile);

		App.Model.EditGoBack.OnPosChanged(this);

		//detect \r without '\n', because it is not well supported
		if (editable) {
			bool badCR = false;
			for (int i = 0, n = text.Length - 1; i <= n; i++) {
				if (text[i] == '\r' && (i == n || text[i + 1] != '\n')) badCR = true;
			}
			if (badCR) {
				print.it($@"<>Note: text of {_fn.Name} contains single \r (CR) as line end characters. It can create problems. <+badCR s>Show<>, <+badCR h>hide<>, <+badCR f>fix<>.");
				if (!s_badCR) {
					s_badCR = true;
					Panels.Output.Scintilla.AaTags.AddLinkTag("+badCR", s1 => {
						bool fix = s1.Starts('f');
						Panels.Editor.ActiveDoc?.Call(fix ? SCI_CONVERTEOLS : SCI_SETVIEWEOL, fix || s1.Starts('h') ? 0 : 1); //tested: SCI_CONVERTEOLS ignored if readonly
					});
				}
			}
		}
	}
	static bool s_badCR;

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


		switch (n.nmhdr.code) {
		case NOTIF.SCN_SAVEPOINTLEFT:
			App.Model.Save.TextLater();
			break;
		case NOTIF.SCN_SAVEPOINTREACHED:
			//never mind: we should cancel the 'save text later'
			break;
		case NOTIF.SCN_MODIFIED:
			//print.it("SCN_MODIFIED", n.modificationType, n.position, n.FinalPosition, aaaCurrentPos8, n.TextForFind);
			//print.it(n.modificationType);
			//if(n.modificationType.Has(MOD.SC_PERFORMED_USER | MOD.SC_MOD_BEFOREINSERT)) {
			//	print.it($"'{n.TextForFind}'");
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
				//	//print.it(n.TextForFind);
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
			switch ((key, mod)) {
			case (KKey.C, ModifierKeys.Control):
				ECopy();
				return true;
			case (KKey.V, ModifierKeys.Control):
				EPaste();
				return true;
			case (KKey.F12, 0):
				Menus.Edit.Go_to_definition();
				return true;
			default:
				if (_ImageDeleteKey(key)) return true;
				if (CodeInfo.SciCmdKey(this, key, mod)) return true;
				switch ((key, mod)) {
				case (KKey.Enter, 0):
				case (KKey.Enter, ModifierKeys.Control | ModifierKeys.Shift):
					aaaAddUndoPoint();
					break;
				}
				break;
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
			if (!App.Model.TryFileOperation(() => _fls.Save(this, _fn.FilePath, tempDirectory: _fn.IsLink ? null : _fn.Model.TempDirectory))) return false;
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
		if (!onModified || c != _prevLineNumberMarginWidth) aaaSetMarginWidth(c_marginLineNumbers, _prevLineNumberMarginWidth = c, chars: true);
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
		Call(SCI_INDICSETSTYLE, c_indicFind, INDIC_FULLBOX);
		//Call(SCI_INDICSETFORE, c_indicFind, 0x00a0f0); Call(SCI_INDICSETALPHA, c_indicFind, 160); //orange-brown, almost like in VS
		Call(SCI_INDICSETFORE, c_indicFind, 0x00ffff); Call(SCI_INDICSETALPHA, c_indicFind, 160); //yellow
		Call(SCI_INDICSETUNDER, c_indicFind, 1); //draw before text

		Call(SCI_INDICSETSTYLE, c_indicError, INDIC_SQUIGGLE); //INDIC_SQUIGGLEPIXMAP thicker
		Call(SCI_INDICSETFORE, c_indicError, 0xff); //red
		Call(SCI_INDICSETSTYLE, c_indicWarning, INDIC_SQUIGGLE);
		Call(SCI_INDICSETFORE, c_indicWarning, 0x008000); //dark green
		Call(SCI_INDICSETSTYLE, c_indicInfo, INDIC_DIAGONAL);
		Call(SCI_INDICSETFORE, c_indicInfo, 0xc0c0c0);
		Call(SCI_INDICSETSTYLE, c_indicDiagHidden, INDIC_DOTS);
		Call(SCI_INDICSETFORE, c_indicDiagHidden, 0xc0c0c0);
	}

	bool _indicHaveFind, _indicHaveDiag;

	internal void EInicatorsFind_(List<Range> a) {
		if (_indicHaveFind) {
			_indicHaveFind = false;
			aaaIndicatorClear(c_indicFind);
		}
		if (a == null || a.Count == 0) return;
		_indicHaveFind = true;

		foreach (var v in a) aaaIndicatorAdd(true, c_indicFind, v);
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

	#region temp ranges

	[Flags]
	public enum TempRangeFlags {
		/// <summary>
		/// Call onLeave etc when current position != current end of range.
		/// </summary>
		LeaveIfPosNotAtEndOfRange = 1,

		/// <summary>
		/// Call onLeave etc when range text modified.
		/// </summary>
		LeaveIfRangeTextModified = 2,

		/// <summary>
		/// Don't add new range if already exists a range with same current from, to, owner and flags. Then returns that range.
		/// </summary>
		NoDuplicate = 4,
	}

	public interface ITempRange {
		/// <summary>
		/// Removes this range from the collection of ranges of the document.
		/// Optional. Temp ranges are automatically removed sooner or later.
		/// Does nothing if already removed.
		/// </summary>
		void Remove();

		/// <summary>
		/// Gets current start and end positions of this range added with <see cref="ETempRanges_Add"/>.
		/// Returns false if the range is removed; then sets from = to = -1.
		/// </summary>
		bool GetCurrentFromTo(out int from, out int to, bool utf8 = false);

		/// <summary>
		/// Gets current start position of this range added with <see cref="ETempRanges_Add"/>. UTF-16.
		/// Returns -1 if the range is removed.
		/// </summary>
		int CurrentFrom { get; }

		/// <summary>
		/// Gets current end position of this range added with <see cref="ETempRanges_Add"/>. UTF-16.
		/// Returns -1 if the range is removed.
		/// </summary>
		int CurrentTo { get; }

		object Owner { get; }

		/// <summary>
		/// Any data. Not used by temp range functions.
		/// </summary>
		object OwnerData { get; set; }
	}

	class _TempRange : ITempRange {
		SciCode _doc;
		readonly object _owner;
		readonly int _fromUtf16;
		internal readonly int from;
		internal int to;
		internal readonly Action onLeave;
		readonly TempRangeFlags _flags;

		internal _TempRange(SciCode doc, object owner, int fromUtf16, int fromUtf8, int toUtf8, Action onLeave, TempRangeFlags flags) {
			_doc = doc;
			_owner = owner;
			_fromUtf16 = fromUtf16;
			from = fromUtf8;
			to = toUtf8;
			this.onLeave = onLeave;
			_flags = flags;
		}

		public void Remove() {
			_TraceTempRange("remove", _owner);
			if (_doc != null) {
				_doc._tempRanges.Remove(this);
				_doc = null;
			}
		}

		internal void Leaved() => _doc = null;

		public bool GetCurrentFromTo(out int from, out int to, bool utf8 = false) {
			if (_doc == null) { from = to = -1; return false; }
			if (utf8) {
				from = this.from;
				to = this.to;
			} else {
				from = _fromUtf16;
				to = CurrentTo;
			}
			return true;
		}

		public int CurrentFrom => _doc != null ? _fromUtf16 : -1;

		public int CurrentTo => _doc?.aaaPos16(to) ?? -1;

		public object Owner => _owner;

		public object OwnerData { get; set; }

		internal bool MustLeave(int pos, int pos2, int modLen) {
			return pos < from || pos2 > to
				|| (0 != (_flags & TempRangeFlags.LeaveIfPosNotAtEndOfRange) && pos2 != to)
				|| (0 != (_flags & TempRangeFlags.LeaveIfRangeTextModified) && modLen != 0);
		}

		internal bool Contains(int pos, object owner, bool endPosition)
			=> (endPosition ? (pos == to) : (pos >= from || pos <= to)) && (owner == null || ReferenceEquals(owner, _owner));

		internal bool Equals(int from2, int to2, object owner2, TempRangeFlags flags2) {
			if (from2 != from || to2 != to || flags2 != _flags
				//|| onLeave2 != onLeave //delegate always different if captured variables
				//|| !ReferenceEquals(onLeave2?.Method, onLeave2?.Method) //can be used but slow. Also tested Target, always different.
				) return false;
			return ReferenceEquals(owner2, _owner);
		}

		public override string ToString() => $"({CurrentFrom}, {CurrentTo}), owner={_owner}";
	}

	List<_TempRange> _tempRanges = new();

	/// <summary>
	/// Marks a temporary working range of text and later notifies when it is leaved.
	/// Will automatically update range bounds when editing text inside it.
	/// Supports many ranges, possibly overlapping.
	/// The returned object can be used to get range info or remove it.
	/// Used mostly for code info, eg to cancel the completion list or signature help.
	/// </summary>
	/// <param name="owner">Owner of the range. See also <see cref="ITempRange.OwnerData"/>.</param>
	/// <param name="from">Start of range, UTF-16.</param>
	/// <param name="to">End of range, UTF-16. Can be = from.</param>
	/// <param name="onLeave">
	/// Called when current position changed and is outside this range (before from or after to) or text modified outside it. Then also forgets the range.
	/// Called after removing the range.
	/// If leaved several ranges, called in LIFO order.
	/// Can be null.
	/// </param>
	/// <param name="flags"></param>
	public ITempRange ETempRanges_Add(object owner, int from, int to, Action onLeave = null, TempRangeFlags flags = 0) {
		int fromUtf16 = from;
		aaaNormalizeRange(true, ref from, ref to);
		//print.it(fromUtf16, from, to, aaaCurrentPos8);
#if DEBUG
		if (!(aaaCurrentPos8 >= from && (flags.Has(TempRangeFlags.LeaveIfPosNotAtEndOfRange) ? aaaCurrentPos8 == to : aaaCurrentPos8 <= to))) {
			Debug_.Print("bad");
			//CiUtil.HiliteRange(from, to);
		}
#endif

		if (flags.Has(TempRangeFlags.NoDuplicate)) {
			for (int i = _tempRanges.Count - 1; i >= 0; i--) {
				var t = _tempRanges[i];
				if (t.Equals(from, to, owner, flags)) return t;
			}
		}

		_TraceTempRange("ADD", owner);
		var r = new _TempRange(this, owner, fromUtf16, from, to, onLeave, flags);
		_tempRanges.Add(r);
		return r;
	}

	/// <summary>
	/// Gets ranges containing the specified position and optionally of the specified owner, in LIFO order.
	/// It's safe to remove the retrieved ranges while enumerating.
	/// </summary>
	/// <param name="position"></param>
	/// <param name="owner">If not null, returns only ranges where ReferenceEquals(owner, range.owner).</param>
	/// <param name="endPosition">position must be at the end of the range.</param>
	/// <param name="utf8"></param>
	public IEnumerable<ITempRange> ETempRanges_Enum(int position, object owner = null, bool endPosition = false, bool utf8 = false) {
		if (!utf8) position = aaaPos8(position);
		for (int i = _tempRanges.Count - 1; i >= 0; i--) {
			var r = _tempRanges[i];
			if (r.Contains(position, owner, endPosition)) yield return r;
		}
	}

	/// <summary>
	/// Gets ranges of the specified owner, in LIFO order.
	/// It's safe to remove the retrieved ranges while enumerating.
	/// </summary>
	/// <param name="owner">Returns only ranges where ReferenceEquals(owner, range.owner).</param>
	public IEnumerable<ITempRange> ETempRanges_Enum(object owner) {
		for (int i = _tempRanges.Count - 1; i >= 0; i--) {
			var r = _tempRanges[i];
			if (ReferenceEquals(owner, r.Owner)) yield return r;
		}
	}

	void _TempRangeOnModifiedOrPosChanged(MOD mod, int pos, int len) {
		if (_tempRanges.Count == 0) return;
		if (mod == 0) pos = aaaCurrentPos8;
		int pos2 = pos;
		if (mod.Has(MOD.SC_MOD_DELETETEXT)) { pos2 += len; len = -len; }
		for (int i = _tempRanges.Count - 1; i >= 0; i--) {
			var r = _tempRanges[i];
			if (r.MustLeave(pos, pos2, len)) {
				_TraceTempRange("leave", r.Owner);
				_tempRanges.RemoveAt(i);
				r.Leaved();
				r.onLeave?.Invoke();
			} else {
				r.to += len;
				Debug.Assert(r.to >= r.from);
			}
		}
	}

	[Conditional("TRACE_TEMP_RANGES")]
	static void _TraceTempRange(string action, object owner) => print.it(action, owner);

	#endregion

	#region acc

	protected override ERole AaAccessibleRole => ERole.DOCUMENT;

	protected override string AaAccessibleName => "document - " + _fn.DisplayName;

	protected override string AaAccessibleDescription => _fn.FilePath;

	#endregion
}
