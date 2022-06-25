//#define TEST_COPYPASTE

using System.Windows;
using System.Windows.Controls;

using Au.Controls;
using static Au.Controls.Sci;

class PanelEdit : Grid
{
	readonly List<SciCode> _docs = new();
	SciCode _activeDoc;

	public PanelEdit() {
		this.Background = SystemColors.AppWorkspaceBrush;
		App.Commands.BindKeysTarget(this, "Edit");
		_UpdateUI_IsOpen();
		_UpdateUI_EditView();
	}

	public SciCode ZActiveDoc => _activeDoc;

	public event Action ZActiveDocChanged;

	public bool ZIsOpen => _activeDoc != null;

	/// <summary>
	/// Documents that are actually open currently.
	/// Note: <see cref="FilesModel.OpenFiles"/> contains not only these.
	/// </summary>
	public IReadOnlyList<SciCode> ZOpenDocs => _docs;

	/// <summary>
	/// If f is open (active or not), returns its SciCode, else null.
	/// See <see cref="ZOpenDocs"/>.
	/// </summary>
	public SciCode ZGetOpenDocOf(FileNode f) {
		foreach (var v in _docs) if (v.EFile == f) return v;
		return null;
	}

	/// <summary>
	///	If f is already open, unhides its control.
	///	Else loads f text and creates control. If fails, does not change anything.
	/// Hides current file's control.
	/// Returns false if failed to read file.
	/// Does not save text of previously active document.
	/// </summary>
	/// <param name="f"></param>
	/// <param name="newFile">Should be true if opening the file first time after creating.</param>
	/// <param name="focusEditor">If null, focus later, when mouse enters editor. Ignored if editor was focused (sets focus). Also depends on <i>newFile</i>.</param>
	/// <param name="noTemplate">New file was created with custom text (option 'replaceTemplate').</param>
	public bool ZOpen(FileNode f, bool newFile, bool? focusEditor, bool noTemplate) {
		Debug.Assert(!App.Model.IsAlien(f));

		if (f == _activeDoc?.EFile) return true;

		//print.it(focusEditor, new StackTrace(true));
		bool focusNow = !newFile && (focusEditor == true || (_activeDoc?.Hwnd.IsFocused ?? false));

		void _ShowHideActiveDoc(bool show) {
			if (show) {
				_activeDoc.Visibility = Visibility.Visible;
				//Children.Add(_activeDoc);
			} else if (_activeDoc != null) {
				_activeDoc.Visibility = Visibility.Hidden;
				//Children.Remove(_activeDoc);
			}
		}

		var doc = ZGetOpenDocOf(f);
		if (doc != null) {
			_ShowHideActiveDoc(false);
			_activeDoc = doc;
			_ShowHideActiveDoc(true);
			doc.EOpenDocActivated();
			_UpdateUI_IsOpen();
			_UpdateUI_EditEnabled();
			ZActiveDocChanged?.Invoke();
		} else {
			var path = f.FilePath;
			byte[] text = null;
			KScintilla.FileLoaderSaver fls = default;
			try { text = fls.Load(path); }
			catch (Exception ex) { print.it("Failed to open file. " + ex.Message); }
			if (text == null) return false;

			_ShowHideActiveDoc(false);
			doc = new SciCode(f, fls);
			_docs.Add(doc);
			_activeDoc = doc;
			Children.Add(doc);
			doc.EInit_(text, newFile, noTemplate);
			_UpdateUI_IsOpen();
			_UpdateUI_EditEnabled();
			ZActiveDocChanged?.Invoke();
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
				if (--count > 0 && f == _activeDoc?.EFile && Panels.Files.TreeControl.IsFocused) {
					if (wnd.fromMouse() != doc.Hwnd
						|| !Panels.Files.TreeControl.IsKeyboardFocused //editing item label
						) return;
					doc.Focus();
				}
				App.Timer025sWhenVisible -= _Timer;
			}
		}

		Panels.Find.ZUpdateQuickResults(true);
		return true;
	}

	/// <summary>
	/// If f is open, closes its document and destroys its control.
	/// f can be any, not necessary the active document.
	/// Saves text before closing the active document.
	/// Does not show another document when closed the active document.
	/// </summary>
	/// <param name="f"></param>
	public void ZClose(FileNode f) {
		Debug.Assert(f != null);
		SciCode doc;
		if (f == _activeDoc?.EFile) {
			App.Model.Save.TextNowIfNeed();
			doc = _activeDoc;
			_activeDoc = null;
			ZActiveDocChanged?.Invoke();
		} else {
			doc = ZGetOpenDocOf(f);
			if (doc == null) return;
		}
		Children.Remove(doc);
		if (doc.IsFocused) App.Wmain.Focus();
		//CodeInfo.FileClosed(doc);
		doc.Dispose();
		_docs.Remove(doc);
		_UpdateUI_IsOpen();
	}

	/// <summary>
	/// Closes all documents and destroys controls.
	/// </summary>
	public void ZCloseAll(bool saveTextIfNeed) {
		if (saveTextIfNeed) App.Model.Save.TextNowIfNeed();
		_activeDoc = null;
		ZActiveDocChanged?.Invoke();
		foreach (var doc in _docs) {
			Children.Remove(doc);
			if (doc.IsFocused) App.Wmain.Focus();
			doc.Dispose();
		}
		_docs.Clear();
		_UpdateUI_IsOpen();
	}

	public bool ZSaveText() {
		return _activeDoc?.ESaveText_() ?? true;
	}

	public void ZSaveEditorData() {
		_activeDoc?.ESaveEditorData_();
	}

	//public bool ZIsModified => _activeDoc?.IsModified ?? false;

	/// <summary>
	/// Enables/disables Edit and Run toolbars/menus and some other UI parts depending on whether a document is open in editor.
	/// </summary>
	void _UpdateUI_IsOpen() {
		bool enable = _activeDoc != null;
		if (enable != _uiDisabled_IsOpen) return;
		_uiDisabled_IsOpen = !enable;
		_editDisabled = 0;

		App.Commands[nameof(Menus.Edit)].Enabled = enable;
		App.Commands[nameof(Menus.Code)].Enabled = enable;
		//App.Commands[nameof(Menus.Run)].Enabled = enable; //no, should not disable eg End_task and Recent
		App.Commands[nameof(Menus.Run.Run_script)].Enabled = enable;
		App.Commands[nameof(Menus.Run.Compile)].Enabled = enable;
		App.Commands[nameof(Menus.Run.Debugger)].Enabled = enable;
		//App.Commands[nameof(Menus.File.Properties)].Enabled = enable; //also Rename, Delete, More //don't disable because can right-click
	}
	bool _uiDisabled_IsOpen;

	/// <summary>
	/// Enables/disables commands (toolbar buttons, menu items) depending on document state such as "can undo".
	/// Called on SCN_UPDATEUI.
	/// </summary>
	internal void _UpdateUI_EditEnabled() {
		_EUpdateUI disable = 0;
		var d = _activeDoc;
		if (d == null) return; //we disable the toolbar and menu
		if (0 == d.Call(SCI_CANUNDO)) disable |= _EUpdateUI.Undo;
		if (0 == d.Call(SCI_CANREDO)) disable |= _EUpdateUI.Redo;
		if (0 != d.Call(SCI_GETSELECTIONEMPTY)) disable |= _EUpdateUI.Copy;
		if (disable.Has(_EUpdateUI.Copy) || d.zIsReadonly) disable |= _EUpdateUI.Cut;
		//if(0 == d.Call(SCI_CANPASTE)) disable |= EUpdateUI.Paste; //rejected. Often slow. Also need to see on focused etc.

		var dif = disable ^ _editDisabled;
		//print.it(dif);
		if (dif == 0) return;

		_editDisabled = disable;
		if (dif.Has(_EUpdateUI.Undo)) App.Commands[nameof(Menus.Edit.Undo)].Enabled = !disable.Has(_EUpdateUI.Undo);
		if (dif.Has(_EUpdateUI.Redo)) App.Commands[nameof(Menus.Edit.Redo)].Enabled = !disable.Has(_EUpdateUI.Redo);
		if (dif.Has(_EUpdateUI.Cut)) App.Commands[nameof(Menus.Edit.Cut)].Enabled = !disable.Has(_EUpdateUI.Cut);
		if (dif.Has(_EUpdateUI.Copy)) App.Commands[nameof(Menus.Edit.Copy)].Enabled = !disable.Has(_EUpdateUI.Copy);
		//if(dif.Has(EUpdateUI.Paste)) App.Commands[nameof(Menus.Edit.Paste)].Enabled = !disable.Has(EUpdateUI.Paste);

	}
	_EUpdateUI _editDisabled;

	internal void _UpdateUI_EditView() {
		App.Commands[nameof(Menus.Edit.View.Wrap_lines)].Checked = App.Settings.edit_wrap;
		App.Commands[nameof(Menus.Edit.View.Images_in_code)].Checked = !App.Settings.edit_noImages;
	}

	//void _UpdateUI_ActiveDocChanged() {

	//}

	[Flags]
	enum _EUpdateUI
	{
		Undo = 1,
		Redo = 2,
		Cut = 4,
		Copy = 8,
		//Paste = 16,

	}
}
