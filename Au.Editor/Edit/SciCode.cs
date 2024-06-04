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
		c_marginMarkers = 1,
		c_marginImages = 2,
		c_marginLineNumbers = 3,
		c_marginChanges = 4; //currently not impl, just adds some space between line numbers and text
	
	//markers. We can use 0-20. Changes 21-24. Folding 25-31.
	public const int c_markerUnderline = 0,
		c_markerBookmark = 1, c_markerBookmarkInactive = 2,
		c_markerBreakpoint = 3, c_markerBreakpointD = 4, c_markerBreakpointC = 5, c_markerBreakpointCD = 6, c_markerBreakpointL = 7, c_markerBreakpointLD = 8,
		c_markerDebugLine = 19, c_markerDebugLine2 = 20;
	//public const int c_markerStepNext = 3;
	
	//indicators. We can use 8-31. KScintilla can use 0-7. Draws indicators from smaller to bigger, eg error on warning.
	public const int
		c_indicImages = 8,
		c_indicRefs = 9,
		c_indicBraces = 10,
		c_indicDebug = 11,
		c_indicDebug2 = 12,
		c_indicFound = 13,
		c_indicSnippetField = 14,
		c_indicSnippetFieldActive = 15,
		c_indicDiagHidden = 20,
		c_indicInfo = 21,
		c_indicWarning = 22,
		c_indicError = 23,
		c_indicTestBox = 29,
		c_indicTestStrike = 30,
		c_indicTestPoint = 31
		;
	
	internal SciCode(FileNode file, aaaFileLoaderSaver fls) {
		_fn = file;
		_fls = fls;
		
		if (fls.IsBinary) AaInitReadOnlyAlways = true;
		if (fls.IsImage) AaInitImages = true;
		
		Name = "document";
	}
	
	protected override void AaOnHandleCreated() {
		Call(SCI_SETMODEVENTMASK, (int)(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT | MOD.SC_MOD_BEFOREDELETE /*| MOD.SC_MOD_INSERTCHECK | MOD.SC_MOD_BEFOREINSERT*/
			//| MOD.SC_MOD_CHANGEFOLD //only when text modified, but not when user clicks +-
			));
		
		aaaMarginSetType(c_marginFold, SC_MARGIN_SYMBOL);
		
		//Call(SCI_SETMARGINMASKN, 1, 0);
		//Call(SCI_SETMARGINMASKN, c_marginMarkers, ~SC_MASK_FOLDERS);
		aaaMarginSetType(c_marginMarkers, SC_MARGIN_COLOUR, sensitive: true, cursorArrow: true);
		Call(SCI_SETMARGINBACKN, c_marginMarkers, 0xFFFFFF);
		aaaMarginSetWidth(c_marginMarkers, 16);
		
		//aaaMarginSetType(c_marginImages, SC_MARGIN_SYMBOL);
		//Call(SCI_SETMARGINWIDTHN, c_marginImages, 0);
		
		aaaMarginSetType(c_marginLineNumbers, SC_MARGIN_NUMBER);
		
		aaaMarginSetWidth(c_marginChanges, 4);
		
		Call(SCI_SETMARGINLEFT, 0, 2);
		Call(SCI_SETWRAPMODE, App.Settings.edit_wrap ? SC_WRAP_WORD : 0);
		Call(SCI_SETINDENTATIONGUIDES, SC_IV_REAL);
		Call(SCI_ASSIGNCMDKEY, Math2.MakeLparam(SCK_RETURN, SCMOD_CTRL | SCMOD_SHIFT), SCI_NEWLINE);
		Call(SCI_SETEXTRADESCENT, 1); //eg to avoid drawing fold separator lines on text
		Call(SCI_SETMOUSEDWELLTIME, 500);
		
		Call(SCI_SETCARETLINEFRAME, 1);
		aaaSetElementColor(SC_ELEMENT_CARET_LINE_BACK, 0xEEEEEE);
		Call(SCI_SETCARETLINEVISIBLEALWAYS, 1);
		
		//Call(SCI_SETVIEWWS, 1); Call(SCI_SETWHITESPACEFORE, 1, 0xcccccc);
		
		CiStyling.TStyles.Settings.ToScintilla(this);
		_OnHandleCreatedOrDpiChanged();
		if (_fn.IsCodeFile) CiFolding.InitFolding(this);
		_InitDragDrop();
		
		//C# interprets Unicode newline characters NEL, LS and PS as newlines.
		//	If editor does not support it, line numbers in errors/warnings/stacktraces may be incorrect.
		//	Visual Studio supports it. VSCode no; when pasted, suggests to replace to normal.
		//	Scintilla supports it (SCI_SETLINEENDTYPESALLOWED) only if using C++ lexer.
		//		Modified: now supports by default.
		//	Ascii VT and FF are not interpreted as newlines by C# and Scintilla.
	}
	
	void _OnHandleCreatedOrDpiChanged() {
		_IndicatorsInit();
		_DefineIconMarkers();
		Call(SCI_SETXCARETPOLICY, CARET_STRICT | CARET_EVEN | CARET_SLOP, _dpi / 2);
	}
	
	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
		base.OnDpiChanged(oldDpi, newDpi);
		if (!AaWnd.Is0) _OnHandleCreatedOrDpiChanged();
	}
	
	protected override void DestroyWindowCore(HandleRef hwnd) {
		CodeInfo._styling.DocHandleDestroyed(this);
		base.DestroyWindowCore(hwnd);
	}
	
	//Called by PanelEdit.Open.
	internal void EInit_(byte[] text, bool newFile, bool noTemplate) {
		Debug.Assert(!AaWnd.Is0);
		
		bool editable = _fls.SetText(this, text);
		if (!EIsBinary) {
			_fn._UpdateFileModTime();
			//if (_fn.DontSave) aaaIsReadonly = true; //rejected. Never make editor readonly when opened a text file. Would need too many `if (doc.aaaIsReadonly) return;` etc everywhere.
			if (_fn.DontSave) print.it($"<>Warning: Don't edit {_fn.SciLink()}. It will not be saved. It will be replaced when updating this app.");
		}
		ESetLineNumberMarginWidth_();
		
		if (newFile) _openState = noTemplate ? _EOpenState.NewFileNoTemplate : _EOpenState.NewFileFromTemplate;
		else if (App.Model.OpenFiles.Contains(_fn)) _openState = _EOpenState.Reopen;
		
		if (_fn.IsCodeFile) CiStyling.DocTextAdded();
		Panels.Bookmarks.SciLoaded(this);
		Panels.Breakpoints.SciLoaded(this);
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
	
	//protected override void Dispose(bool disposing) {
	//	print.qm2.write($"Dispose disposing={disposing} IsHandleCreated={IsHandleCreated} Visible={Visible}");
	//	base.Dispose(disposing);
	//}
	
	internal void EOpenDocActivated() {
		_fn._CheckModifiedExternally(this);
		App.Model.EditGoBack.OnPosChanged(this);
	}
	
	protected override void AaOnSciNotify(ref SCNotification n) {
		bool isActive = this == Panels.Editor.ActiveDoc;
		//if (isActive) {
		//	//if (test_) {
		//		switch (n.nmhdr.code) {
		//		case NOTIF.SCN_UPDATEUI:
		//		case NOTIF.SCN_NEEDSHOWN:
		//		case NOTIF.SCN_PAINTED:
		//		case NOTIF.SCN_FOCUSIN:
		//		case NOTIF.SCN_FOCUSOUT:
		//		case NOTIF.SCN_DWELLSTART:
		//		case NOTIF.SCN_DWELLEND:
		//			break;
		//		case NOTIF.SCN_MODIFIED:
		//			print.it(n.nmhdr.code, n.modificationType);
		//			break;
		//		default:
		//			print.it(n.nmhdr.code);
		//			break;
		//		}
		//	//}
		//} else {
		//	//#if DEBUG
		//	//			if (n.code is not (NOTIF.SCN_FOCUSOUT or NOTIF.SCN_DWELLEND
		//	//				or NOTIF.SCN_SAVEPOINTLEFT or NOTIF.SCN_SAVEPOINTREACHED or NOTIF.SCN_MODIFIED or NOTIF.SCN_UPDATEUI or NOTIF.SCN_STYLENEEDED
		//	//				)) Debug_.Print($"AaOnSciNotify in background, {_fn}, {n.nmhdr.code}");
		//	//#endif
		//}
		
		switch (n.code) {
		case NOTIF.SCN_MODIFIED:
			//print.it("SCN_MODIFIED", n.modificationType, n.position, n.FinalPosition, aaaCurrentPos8, n.Text);
			if (n.modificationType.HasAny(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT)) {
				App.Model.Save.TextLater(); //just compares/sets a field. Note: don't use SCN_SAVEPOINTLEFT. No SCN_SAVEPOINTLEFT if text modified externally. Then would not save subsequent changes in editor.
				_modified = true;
				_TempRangeOnModifiedOrPosChanged(n.modificationType, n.position, n.length);
				SnippetMode_?.SciModified(n);
				if (isActive) {
					if (CodeInfo.SciModified(this, n)) _CodeModifiedAndCodeinfoOK(); //WPF preview
					Panels.Find.UpdateQuickResults();
				}
				Panels.Bookmarks.SciModified(this, n);
				Panels.Breakpoints.SciModified(this, n);
				Panels.Found.SciModified(this, n);
				App.Model.EditGoBack.SciModified(this, n.modificationType.Has(MOD.SC_MOD_DELETETEXT), n.position, n.length);
				if (n.linesAdded != 0) ESetLineNumberMarginWidth_(onModified: true);
			}
			break;
		case NOTIF.SCN_UPDATEUI:
			//note: SCN_UPDATEUI is async. Can be isActive true in SCN_MODIFIED but false in SCN_UPDATEUI. Or vice versa.
			//print.it(_modified, n.updated);
			if (n.updated.Has(UPDATE.SC_UPDATE_CONTENT)) {
				if (_modified) _modified = false;
				else if ((n.updated &= ~UPDATE.SC_UPDATE_CONTENT) == 0) break; //ignore when changed styling or markers
			}
			if (n.updated.HasAny(UPDATE.SC_UPDATE_CONTENT | UPDATE.SC_UPDATE_SELECTION)) {
				_TempRangeOnModifiedOrPosChanged(0, 0, 0);
				if (isActive) {
					if (n.updated.Has(UPDATE.SC_UPDATE_SELECTION)) App.Model.EditGoBack.OnPosChanged(this);
					Panels.Editor.UpdateUI_EditEnabled_();
				}
			}
			if ((n.updated & (UPDATE.SC_UPDATE_CONTENT | UPDATE.SC_UPDATE_SELECTION)) == UPDATE.SC_UPDATE_SELECTION) SnippetMode_?.SciPosChanged();
			if (isActive) CodeInfo.SciUpdateUI(this, n.updated);
			break;
		case NOTIF.SCN_CHARADDED when isActive:
			//print.it($"SCN_CHARADDED  {n.ch}  '{(char)n.ch}'");
			if (n.ch == '\n' /*|| n.ch == ';'*/) { //split scintilla Undo
				aaaAddUndoPoint();
			}
			if (n.ch != '\r' && n.ch <= 0xffff) { //on Enter we receive notifications for '\r' and '\n'
				CodeInfo.SciCharAdded(this, (char)n.ch);
			}
			break;
		case NOTIF.SCN_DWELLSTART when isActive:
			CodeInfo.SciMouseDwellStarted(this, n.position);
			Panels.Breakpoints.SciMouseDwell_(true, this, n);
			break;
		case NOTIF.SCN_DWELLEND when isActive:
			Panels.Breakpoints.SciMouseDwell_(false, this, n);
			break;
		case NOTIF.SCN_MARGINCLICK when isActive:
			if (_fn.IsCodeFile) {
				CodeInfo.Cancel();
				if (n.margin == c_marginFold) {
					_FoldOnMarginClick(n.position, n.modifiers);
				}
			}
			if (n.margin == c_marginMarkers) {
				if (n.modifiers is 0) _MarkersMarginClicked(false, n.position);
			}
			break;
		//case NOTIF.SCN_MARGINRIGHTCLICK: break; //can't use it because: 1. Need to handle WM_RBUTTONDOWN. 2. Need notification on button up.
		case NOTIF.SCN_STYLENEEDED:
			//print.it("SCN_STYLENEEDED");
			if (isActive && _fn.IsCodeFile) {
				EHideImages_(Call(SCI_GETENDSTYLED), n.position);
				Call(SCI_STARTSTYLING, n.position); //need this even if would not hide images
			} else {
				aaaSetStyled();
			}
			break;
		}
		
		base.AaOnSciNotify(ref n);
	}
	bool _modified;
	
	protected override nint WndProc(wnd w, int msg, nint wp, nint lp) {
		//WndUtil.PrintMsg(w, msg, wp, lp, new(Api.WM_TIMER, Api.WM_PAINT, Api.WM_MOUSEMOVE, Api.WM_NCHITTEST, Api.WM_SETCURSOR));
		
		switch (msg) {
		case Api.WM_CHAR: {
				int c = (int)wp;
				if (c < 32) {
					if (c is not (9 or 10 or 13)) return 0;
				} else {
					if (CodeInfo.SciBeforeCharAdded(this, (char)c)) return 0;
				}
			}
			break;
		//rejected. Possibly can be bad in some cases. I can't learn and test everything.
		//case Api.WM_IME_COMPOSITION: //to add missing newline, because no WM_CHAR when using IME
		//	if (0 != (lp & 0x800)) //GCS_RESULTSTR; Scintilla uses it.
		//		CodeInfo.SciBeforeCharAdded(this, default);
		//	break;
		case Api.WM_RBUTTONDOWN or Api.WM_MBUTTONDOWN: {
				POINT p = Math2.NintToPOINT(lp);
				int margin = aaaMarginFromPoint(p);
				if (margin >= 0) {
					if (msg == Api.WM_RBUTTONDOWN) {
						//prevent changing the caret/selection when rclicked some margins
						if (margin is c_marginFold or c_marginMarkers) return 0;
						
						//prevent changing the caret/selection if it is in the rclicked line
						var selStart = aaaSelectionStart8;
						var (_, start, end) = aaaLineStartEndFromPos(false, aaaPosFromXY(false, p, false));
						if (selStart >= start && selStart <= end) return 0;
						//do vice versa if the end of non-empty selection is at the start of the rclicked line, to avoid comment/uncomment wrong lines
						if (margin is c_marginLineNumbers or c_marginImages or c_marginChanges) {
							if (aaaSelectionEnd8 == start) aaaGoToPos(false, start); //clear selection above start
						}
					} else if (margin == c_marginMarkers) {
						int pos = aaaPosFromXY(false, p, false);
						if (pos >= 0) {
							int line = aaaLineFromPos(false, pos);
							if (!Panels.Breakpoints.SciMiddleClick_(this, line)) Panels.Bookmarks.SciMiddleClick_(this, line);
						}
					}
				}
			}
			break;
		case Api.WM_RBUTTONUP: {
				POINT p = Math2.NintToPOINT(lp);
				int margin = aaaMarginFromPoint(p);
				if (margin >= 0) {
					switch (margin) {
					case c_marginLineNumbers or c_marginImages or c_marginChanges:
						ModifyCode.Comment(null, notSlashStar: true);
						break;
					case c_marginMarkers:
						_MarkersMarginClicked(true, base.aaaPosFromXY(false, p, false));
						break;
					case c_marginFold:
						_FoldContextMenu(aaaPosFromXY(false, p, false));
						break;
					}
					return 0;
				}
			}
			break;
		case Api.WM_CONTEXTMENU: {
				bool kbd = (int)lp == -1;
				var m = new KWpfMenu();
				DCustomizeContextMenu.AddToMenu(m, "Edit");
				App.Commands[nameof(Menus.Edit)].CopyToMenu(m);
				m.Show(this, byCaret: kbd);
				return 0;
			}
		}
		
		var R = base.WndProc(w, msg, wp, lp);
		
		switch (msg) {
		case Api.WM_KILLFOCUS:
			CodeInfo.SciKillFocus(this);
			break;
		}
		
		return R;
	}
	
	void _MarkersMarginClicked(bool rclick, int pos8) {
		int line = aaaLineFromPos(false, pos8);
		var m = new popupMenu();
#if !true //breakpoints if left click, bookmarks if right click
		if (rclick) {
			Panels.Bookmarks.AddMarginMenuItems_(this, m, pos8);
		} else if (EFile.IsCodeFile) {
			Panels.Breakpoints.AddMarginMenuItems_(this, m, line, pos8);
			Panels.Debug.AddMarginMenuItems_(this, m, line);
		}
#else //breakpoints and bookmarks in single menu
		if (EFile.IsCodeFile) {
			Panels.Breakpoints.AddMarginMenuItems_(this, m, line, pos8);
			Panels.Debug.AddMarginMenuItems_(this, m, line);
			m.Separator();
		}
		Panels.Bookmarks.AddMarginMenuItems_(this, m, pos8);
#endif
		var xy = mouse.xy; xy.Offset(-_dpi / 2, -_dpi / 8);
		m.Show(xy: xy, owner: AaWnd);
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
	
	//Called by PanelEdit.SaveText.
	internal bool ESaveText_(bool force) {
		Debug.Assert(!EIsBinary); if (EIsBinary) return false;
		if (_fn.DontSave) return true;
		if (force || EIsUnsaved_) {
			//print.qm2.write("saving");
			if (!App.Model.TryFileOperation(() => _fls.Save(this, _fn.FilePath, tempDirectory: _fn.IsExternal ? null : _fn.Model.TempDirectory))) return false;
			//info: with tempDirectory less noise for FileSystemWatcher (now removed, but anyway)
			_isUnsaved = false;
			Call(SCI_SETSAVEPOINT);
			_fn._UpdateFileModTime();
			if (this != Panels.Editor.ActiveDoc) CodeInfo.FilesChanged();
		}
		return true;
	}
	
	//Called by FileNode.CheckModifiedExternally_.
	internal void EFileModifiedExternally_() {
		Debug.Assert(!EIsBinary); if (EIsBinary) return; //caller must check it
		if (!_fn.GetFileText(out var text) || text == this.aaaText) return;
		EReplaceTextGently(text);
		Call(SCI_SETSAVEPOINT);
		
		//rejected: print info. VS and VSCode reload silently.
		//if (this == Panels.Editor.ActiveDoc) print.it($"<>Info: file {_fn.SciLink()} has been reloaded because modified outside. You can Undo.");
	}
	
	//never mind: not called when zoom changes.
	internal void ESetLineNumberMarginWidth_(bool onModified = false) {
		int c = 4;
		int lines = aaaLineCount;
		while (lines > 999) { c++; lines /= 10; }
		if (!onModified || c != _prevLineNumberMarginWidth) aaaMarginSetWidth(c_marginLineNumbers, -(_prevLineNumberMarginWidth = c), 4);
	}
	int _prevLineNumberMarginWidth;
	
	protected override void AaOnDeletingLineWithMarkers(int line, uint markers) {
		if ((markers & 3 << c_markerBookmark) != 0) Panels.Bookmarks.SciDeletingLineWithMarker(this, line);
		if ((markers & 63 << c_markerBreakpoint) != 0) Panels.Breakpoints.SciDeletingLineWithMarker(this, line);
		base.AaOnDeletingLineWithMarkers(line, markers);
	}
	
	internal CiSnippetMode SnippetMode_;
	
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
			if (isCS) s = _ImageRemoveScreenshots(s, true);
			switch (copyAs) {
			case ECopyAs.Forum:
				var b = new StringBuilder("[code]");
				if (isCS) {
					if (!isFragment) {
						var name = _fn.Name; if (name.RxIsMatch(_fn.IsScript ? @"(?i)^Script\d*\.cs$" : @"(?i)^Class\d*\.cs$")) name = null;
						if (!(name == null && _fn.IsScript)) b.AppendFormat("// {0} \"{1}\"\r\n", _fn.IsScript ? "script" : "class", name);
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
			string buttons = _fn.FileType != (isClass ? FNType.Class : FNType.Script) || aaaIsReadonly
				? "1 Create new file|0 Cancel"
				: "1 Create new file|2 Replace all text|3 Paste|0 Cancel";
			switch (dialog.show("Import C# file text from clipboard", "Source file: " + name, buttons, DFlags.CommandLinks, owner: AaWnd)) {
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
	
	void _IndicatorsInit() {
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
	
	#region util
	
	void _DefineIconMarkers() {
		if (s_markerBitmaps.dpi != _dpi) {
			s_markerBitmaps.dpi = _dpi;
			_Bitmap(ref s_markerBitmaps.bookmark, "*Material.Bookmark #EABB00 @16", 14);
			_Bitmap(ref s_markerBitmaps.bookmark2, "*Material.BookmarkOutline #EABB00 @16", 14);
			_Bitmap(ref s_markerBitmaps.breakpoint, "*Material.Circle #EE3000", 8);
			_Bitmap(ref s_markerBitmaps.breakpointD, "*Material.Circle #A0A0A0", 8);
			_Bitmap(ref s_markerBitmaps.breakpointC, "*Codicons.DebugBreakpointConditional #EE3000", 8);
			_Bitmap(ref s_markerBitmaps.breakpointCD, "*Codicons.DebugBreakpointConditional #A0A0A0", 8);
			_Bitmap(ref s_markerBitmaps.breakpointL, "*BootstrapIcons.DiamondFill #40B000", 10);
			_Bitmap(ref s_markerBitmaps.breakpointLD, "*BootstrapIcons.DiamondFill #A0A0A0", 10);
			_Bitmap(ref s_markerBitmaps.debugLine, "*Codicons.DebugStackframe #40B000 @16", 14);
			_Bitmap(ref s_markerBitmaps.debugLine2, "*Codicons.DebugStackframe #808080 @16", 14);
		}
		_Marker(c_markerBookmark, s_markerBitmaps.bookmark);
		_Marker(c_markerBookmarkInactive, s_markerBitmaps.bookmark2);
		_Marker(c_markerBreakpoint, s_markerBitmaps.breakpoint);
		_Marker(c_markerBreakpointD, s_markerBitmaps.breakpointD);
		_Marker(c_markerBreakpointC, s_markerBitmaps.breakpointC);
		_Marker(c_markerBreakpointCD, s_markerBitmaps.breakpointCD);
		_Marker(c_markerBreakpointL, s_markerBitmaps.breakpointL);
		_Marker(c_markerBreakpointLD, s_markerBitmaps.breakpointLD);
		_Marker(c_markerDebugLine, s_markerBitmaps.debugLine);
		_Marker(c_markerDebugLine2, s_markerBitmaps.debugLine2);
		
		unsafe void _Bitmap(ref (int size, nint data) mb, string icon, int size) {
			if (mb.data != 0) { MemoryUtil.Free((uint*)mb.data); mb.data = 0; }
			using var b = ImageUtil.LoadGdipBitmapFromXaml(icon, _dpi, new(size, size));
			using var v = b.Data(System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			size = v.Width;
			var m = MemoryUtil.Alloc<uint>(size * size);
			MemoryUtil.Copy((uint*)v.Scan0, m, size * size * 4);
			for (uint* p = m, pe = p + size * size; p < pe; p++) *p = ColorInt.SwapRB(*p);
			mb = new(size, (nint)m);
		}
		
		unsafe void _Marker(int marker, (int size, nint data) b) {
			aaaMarkerDefine(marker, Sci.SC_MARK_RGBAIMAGE);
			Call(SCI_RGBAIMAGESETWIDTH, b.size);
			Call(SCI_RGBAIMAGESETHEIGHT, b.size);
			Call(SCI_MARKERDEFINERGBAIMAGE, marker, b.data);
		}
	}
	
	struct _MarkerBitmaps {
		public int dpi;
		public (int size, nint data) bookmark, bookmark2, breakpoint, breakpointD, breakpointC, breakpointCD, breakpointL, breakpointLD, debugLine, debugLine2;
	}
	static _MarkerBitmaps s_markerBitmaps;
	
	#endregion
}
