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
		c_marginLineNumbers = 2,
		c_marginChanges = 3; //currently not impl, just adds some space between line numbers and text
	
	//markers. We can use 0-20. Changes 21-24. Folding 25-31.
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
		Call(SCI_SETMODEVENTMASK, (int)(MOD.SC_MOD_INSERTTEXT | MOD.SC_MOD_DELETETEXT | MOD.SC_MOD_BEFOREDELETE /*| MOD.SC_MOD_INSERTCHECK | MOD.SC_MOD_BEFOREINSERT*/
			//| MOD.SC_MOD_CHANGEFOLD //only when text modified, but not when user clicks +-
			));
		
		aaaMarginSetType(c_marginFold, SC_MARGIN_SYMBOL);
		
		aaaMarginSetType(c_marginImages, SC_MARGIN_SYMBOL);
		Call(SCI_SETMARGINWIDTHN, c_marginImages, 0);
		
		aaaMarginSetType(c_marginLineNumbers, SC_MARGIN_NUMBER);
		Call(SCI_SETMARGINMASKN, 1, 0);
		Call(SCI_SETMARGINMASKN, c_marginLineNumbers, ~SC_MASK_FOLDERS);
		bool dark = WpfUtil_.IsHighContrastDark;
		aaaMarkerDefine(SciCode.c_markerBookmark, Sci.SC_MARK_VERTICALBOOKMARK, dark ? 0xFFFFFF : 0x404040, 0x8080ff);
		
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
		
		//Call(SCI_SETXCARETPOLICY, CARET_SLOP | CARET_EVEN, 20); //does not work
		//Call(SCI_SETVIEWWS, 1); Call(SCI_SETWHITESPACEFORE, 1, 0xcccccc);
		
		CiStyling.TStyles.Settings.ToScintilla(this);
		_InicatorsInit();
		if (_fn.IsCodeFile) CiFolding.InitFolding(this);
		_InitDragDrop();
		
		//C# interprets Unicode newline characters NEL, LS and PS as newlines.
		//	If editor does not support it, line numbers in errors/warnings/stacktraces may be incorrect.
		//	Visual Studio supports it. VSCode no; when pasted, suggests to replace to normal.
		//	Scintilla supports it (SCI_SETLINEENDTYPESALLOWED) only if using C++ lexer.
		//		Modified: now supports by default.
		//	Ascii VT and FF are not interpreted as newlines by C# and Scintilla.
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
		Panels.Bookmarks.DocLoaded(this);
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
				if (isActive) {
					if (CodeInfo.SciModified(this, n)) _CodeModifiedAndCodeinfoOK(); //WPF preview
					Panels.Find.UpdateQuickResults();
				}
				Panels.Bookmarks.SciModified(this, ref n);
				App.Model.EditGoBack.OnTextModified(this, n.modificationType.Has(MOD.SC_MOD_DELETETEXT), n.position, n.length);
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
			break;
		case NOTIF.SCN_MARGINCLICK when isActive:
			if (_fn.IsCodeFile) {
				CodeInfo.Cancel();
				if (n.margin == c_marginFold) {
					_FoldOnMarginClick(n.position, n.modifiers);
				}
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

	protected override void AaOnDeletingLineWithMarkers(int line, uint markers) {
		if ((markers & 1 << c_markerBookmark) != 0) Panels.Bookmarks.DeletingLineWithMarker(this, line);
		base.AaOnDeletingLineWithMarkers(line, markers);
	}

	protected override nint WndProc(wnd w, int msg, nint wp, nint lp) {
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
		case Api.WM_RBUTTONDOWN: {
				POINT p = Math2.NintToPOINT(lp);
				int margin = aaaMarginFromPoint(p);
				if (margin >= 0) {
					//prevent changing the caret/selection when rclicked some margins
					if (margin == c_marginFold) return 0;
					
					//prevent changing the caret/selection if it is in the rclicked line
					var selStart = aaaSelectionStart8;
					var (_, start, end) = aaaLineStartEndFromPos(false, aaaPosFromXY(false, p, false));
					if (selStart >= start && selStart <= end) return 0;
					//do vice versa if the end of non-empty selection is at the start of the rclicked line, to avoid comment/uncomment wrong lines
					if (margin is c_marginLineNumbers or c_marginImages or c_marginChanges) {
						if (aaaSelectionEnd8 == start) aaaGoToPos(false, start); //clear selection above start
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
						ModifyCode.CommentLines(null, notSlashStar: true);
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
				int margin = -1;
				POINT p = default;
				if (!kbd) {
					p = Math2.NintToPOINT(lp);
					margin = aaaMarginFromPoint(p, screenCoord: true);
				}
				if (margin < 0) {
					var m = new KWpfMenu();
					DCustomizeContextMenu.AddToMenu(m, "Edit");
					App.Commands[nameof(Menus.Edit)].CopyToMenu(m);
					m.Show(this, byCaret: kbd);
				}
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
		int c = 5; //3 would be good, but need some space for markers
		int lines = aaaLineCount;
		while (lines > 999) { c++; lines /= 10; }
		if (!onModified || c != _prevLineNumberMarginWidth) aaaMarginSetWidth(c_marginLineNumbers, -(_prevLineNumberMarginWidth = c), 4);
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
			string buttons = _fn.FileType != (isClass ? FNType.Class : FNType.Script) || aaaIsReadonly
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
