//#define TEST_COPYPASTE

using System.Windows;
using System.Windows.Controls;

using Au.Controls;
using static Au.Controls.Sci;

class PanelEdit {
	readonly List<SciCode> _docs = new();
	SciCode _activeDoc;
	
	public PanelEdit() {
		P.Background = SystemColors.AppWorkspaceBrush;
		App.Commands.BindKeysTarget(P, "Edit");
		_UpdateUI_IsOpen();
		UpdateUI_EditView_();
	}
	
	public Grid P { get; } = new();
	
	public SciCode ActiveDoc => _activeDoc;
	
	/// <summary>
	/// When opened new document, or switched to an open document, or closed current or all documents.
	/// When closing, <b>ActiveDoc</b> is null when calling event handlers.
	/// </summary>
	public event Action ActiveDocChanged;
	
	/// <summary>
	/// true if 1 or more documents are open.
	/// </summary>
	public bool IsOpen => _activeDoc != null;
	
	/// <summary>
	/// Documents that currently are open in editor.
	/// See also <see cref="FilesModel.OpenFiles"/> (it contains these and more).
	/// </summary>
	public IReadOnlyList<SciCode> OpenDocs => _docs;
	
	/// <summary>
	/// If f is already open, unhides its control.
	/// Else loads f text and creates control. If fails, does not change anything.
	/// Hides current file's control.
	/// Returns false if failed to read file.
	/// Does not save text of previously active document.
	/// </summary>
	/// <param name="f"></param>
	/// <param name="newFile">Should be true if opening the file first time after creating.</param>
	/// <param name="focusEditor">If null, focus later, when mouse enters editor. Ignored if editor was focused (sets focus). Also depends on <i>newFile</i>.</param>
	/// <param name="noTemplate">New file was created with custom text (option 'replaceTemplate').</param>
	public bool Open(FileNode f, bool newFile, bool? focusEditor, bool noTemplate) {
		Debug.Assert(!App.Model.IsAlien(f));
		
		if (f == _activeDoc?.FN) return true;
		
		//print.it(focusEditor, new StackTrace(true));
		bool focusNow = !newFile && (focusEditor == true || (_activeDoc?.AaWnd.IsFocused ?? false));
		
		void _ShowHideActiveDoc(bool show) {
			if (show) {
				_activeDoc.Visibility = Visibility.Visible;
				//Children.Add(_activeDoc);
			} else if (_activeDoc != null) {
				_activeDoc.Visibility = Visibility.Hidden;
				//Children.Remove(_activeDoc);
				_activeDoc.ETempRanges_HidingOrClosingActiveDoc_();
			}
		}
		
		var doc = f.OpenDoc;
		if (doc != null) {
			_ShowHideActiveDoc(false);
			_activeDoc = doc;
			_ShowHideActiveDoc(true);
			doc.EOpenDocActivated();
			_UpdateUI_IsOpen();
			UpdateUI_EditEnabled_();
			ActiveDocChanged?.Invoke();
		} else {
			var path = f.FilePath;
			byte[] text = null;
			KScintilla.aaaFileLoaderSaver fls = default;
			try { text = fls.Load(path); }
			catch (Exception ex) { print.it("Failed to open file. " + ex.Message); }
			if (text == null) return false;
			
			_ShowHideActiveDoc(false);
			doc = new SciCode(f, fls);
			_docs.Add(doc);
			f.OpenDoc = _activeDoc = doc;
			P.Children.Add(doc);
			doc.EInit_(text, newFile, noTemplate);
			_UpdateUI_IsOpen();
			UpdateUI_EditEnabled_();
			ActiveDocChanged?.Invoke();
			//CodeInfo.FileOpened(doc);
		}
		
		if (focusNow) _activeDoc.Focus();
		else if (focusEditor == null || (newFile && focusEditor == true)) {
			//if opens on single click, focus later, when mouse is in doc.
			//	Else eg user clicks and presses Del to delete file but instead deletes char in doc text. Or wants to rename but F2 does nothing.
			
			int count = 60 * 4; //60 s timeout
			App.Timer025sWhenVisible += _Timer;
			void _Timer() {
				//print.it("timer");
				if (--count > 0 && f == _activeDoc?.FN && Panels.Files.TreeControl.IsFocused) {
					if (wnd.fromMouse() != doc.AaWnd
						|| !Panels.Files.TreeControl.IsKeyboardFocused //editing item label
						) return;
					doc.Focus();
				}
				App.Timer025sWhenVisible -= _Timer;
			}
		}
		
		Panels.Find.UpdateQuickResults();
		return true;
	}
	
	/// <summary>
	/// If f is open, closes its document and destroys its control.
	/// f can be any, not necessary the active document.
	/// Saves text before closing the active document.
	/// Does not show another document when closed the active document.
	/// </summary>
	/// <param name="f"></param>
	public void Close(FileNode f) {
		Debug.Assert(f != null);
		SciCode doc;
		if (f == _activeDoc?.FN) {
			_activeDoc.ETempRanges_HidingOrClosingActiveDoc_();
			App.Model.Save.TextNowIfNeed(closingDoc: true);
			doc = _activeDoc;
			_activeDoc = null;
			ActiveDocChanged?.Invoke();
		} else {
			doc = f.OpenDoc;
			if (doc == null) return;
		}
		_Close(doc);
		_docs.Remove(doc);
		_UpdateUI_IsOpen();
	}
	
	void _Close(SciCode doc) {
		ClosingDoc?.Invoke(doc);
		if (doc.IsFocused) doc.IsEnabled = false; //prevent focusing the scintilla control after hiding/parking
		P.Children.Remove(doc);
		//CodeInfo.FileClosed(doc);
		doc.FN.OpenDoc = null;
		doc.Dispose();
	}
	
	/// <summary>
	/// Closes all documents and destroys controls.
	/// Called by FilesModel.UnloadingWorkspace_.
	/// </summary>
	public void CloseAll(bool saveTextIfNeed) {
		_activeDoc?.ETempRanges_HidingOrClosingActiveDoc_();
		if (saveTextIfNeed) App.Model.Save.TextNowIfNeed();
		_activeDoc = null;
		ActiveDocChanged?.Invoke();
		foreach (var doc in _docs) _Close(doc);
		_docs.Clear();
		_UpdateUI_IsOpen();
	}
	
	/// <summary>
	/// When closing a document (active or not).
	/// Called after setting <b>ActiveDoc</b> = null, invoking <b>ActiveDocChanged</b> and saving changes (event handlers must not make new changes).
	/// </summary>
	public event Action<SciCode> ClosingDoc;
	
	public bool SaveText() {
		return _activeDoc?.ESaveText_(false) ?? true;
	}
	
	public void SaveEditorData(bool closingDoc) {
		_activeDoc?.ESaveEditorData_(closingDoc);
	}
	
	//public bool IsModified => _activeDoc?.IsModified ?? false;
	
	internal void OnAppActivated_() {
		foreach (var doc in _docs) {
			doc.FN._CheckModifiedExternally(doc);
		}
	}
	
	/// <summary>
	/// Enables/disables Edit and Run toolbars/menus and some other UI parts depending on whether a document is open in editor.
	/// </summary>
	void _UpdateUI_IsOpen() {
		bool enable = _activeDoc != null;
		if (enable != _uiDisabled_IsOpen) return;
		_uiDisabled_IsOpen = !enable;
		_editDisabled = 0;
		
		string[] a1 = [
			nameof(Menus.Edit),
			nameof(Menus.Code),
			nameof(Menus.TT.Toolbar_trigger),
			nameof(Menus.TT.Script_triggers),
			nameof(Menus.Run.Run_script),
			nameof(Menus.Run.Compile),
			nameof(Menus.Run.Debug_run),
			nameof(Menus.Run.Publish),
			//nameof(Menus.Run.Debugger),
			//and not Properties, Rename, Delete, More, because users can right-click in the Files panel
			];
		foreach (string v in a1) App.Commands[v].Enable(enable);
	}
	bool _uiDisabled_IsOpen;
	
	/// <summary>
	/// Enables/disables commands (toolbar buttons, menu items) depending on document state such as "can undo".
	/// Called on SCN_UPDATEUI.
	/// </summary>
	internal void UpdateUI_EditEnabled_() {
		_EUpdateUI disable = 0;
		var d = _activeDoc;
		if (d == null) return; //we disable the toolbar and menu
		if (0 == d.Call(SCI_CANUNDO)) disable |= _EUpdateUI.Undo;
		if (0 == d.Call(SCI_CANREDO)) disable |= _EUpdateUI.Redo;
		if (!d.aaaHasSelection) disable |= _EUpdateUI.Copy;
		if (disable.Has(_EUpdateUI.Copy) || d.aaaIsReadonly) disable |= _EUpdateUI.Cut;
		//if(0 == d.Call(SCI_CANPASTE)) disable |= EUpdateUI.Paste; //rejected. Often slow. Also need to see on focused etc.
		
		var dif = disable ^ _editDisabled;
		//print.it(dif);
		if (dif == 0) return;
		
		_editDisabled = disable;
		if (dif.Has(_EUpdateUI.Undo)) App.Commands[nameof(Menus.Edit.UndoRedo.Undo)].Enable(!disable.Has(_EUpdateUI.Undo));
		if (dif.Has(_EUpdateUI.Redo)) App.Commands[nameof(Menus.Edit.UndoRedo.Redo)].Enable(!disable.Has(_EUpdateUI.Redo));
		if (dif.Has(_EUpdateUI.Cut)) App.Commands[nameof(Menus.Edit.Clipboard.Cut)].Enable(!disable.Has(_EUpdateUI.Cut));
		if (dif.Has(_EUpdateUI.Copy)) App.Commands[nameof(Menus.Edit.Clipboard.Copy)].Enable(!disable.Has(_EUpdateUI.Copy));
		//if(dif.Has(EUpdateUI.Paste)) App.Commands[nameof(Menus.Edit.Paste)].Enable(!disable.Has(EUpdateUI.Paste));
		
	}
	_EUpdateUI _editDisabled;
	
	internal void UpdateUI_EditView_() {
		App.Commands[nameof(Menus.Edit.View.Wrap_lines)].Checked = App.Settings.edit_wrap;
		App.Commands[nameof(Menus.Edit.View.Images_in_code)].Checked = !App.Settings.edit_noImages;
	}
	
	//void _UpdateUI_ActiveDocChanged() {
	
	//}
	
	[Flags]
	enum _EUpdateUI {
		Undo = 1,
		Redo = 2,
		Cut = 4,
		Copy = 8,
		//Paste = 16,
		
	}
}
