
partial class FilesModel {
	public class AutoSave {
		FilesModel _model;
		int _workspaceAfterS, _stateAfterS, _textAfterS;
		internal bool LoadingState;
		
		public AutoSave(FilesModel model) {
			_model = model;
			App.Timer1s += _Program_Timer1s;
		}
		
		public void Dispose() {
			_model = null;
			App.Timer1s -= _Program_Timer1s;
			
			//must be all saved or unchanged
			Debug.Assert(_workspaceAfterS == 0);
			Debug.Assert(_stateAfterS == 0);
			Debug.Assert(_textAfterS == 0);
		}
		
		/// <summary>
		/// Sets timer to save files.xml later, if not already set.
		/// </summary>
		/// <param name="afterS">Timer time, seconds.</param>
		public void WorkspaceLater(int afterS = 5) {
			if (_workspaceAfterS < 1 || _workspaceAfterS > afterS) _workspaceAfterS = afterS;
		}
		
		/// <summary>
		/// Sets timer to save state later, if not already set.
		/// </summary>
		/// <param name="afterS">Timer time, seconds.</param>
		public void StateLater(int afterS = 30) {
			if (LoadingState) return;
			if (_stateAfterS < 1 || _stateAfterS > afterS) _stateAfterS = afterS;
		}
		
		/// <summary>
		/// Sets timer to save editor text later, if not already set.
		/// </summary>
		/// <param name="afterS">Timer time, seconds.</param>
		public void TextLater(int afterS = 60) {
			if (_textAfterS < 1 || _textAfterS > afterS) _textAfterS = afterS;
		}
		
		/// <summary>
		/// If files.xml is set to save (WorkspaceLater), saves it now.
		/// </summary>
		public void WorkspaceNowIfNeed() {
			if (_workspaceAfterS > 0) _SaveWorkspaceNow();
		}
		
		/// <summary>
		/// If state is set to save (StateLater), saves it now.
		/// </summary>
		public void StateNowIfNeed() {
			if (_stateAfterS > 0) _SaveStateNow();
		}
		
		/// <summary>
		/// If editor text is set to save (TextLater), saves it now.
		/// Also saves markers, folding, etc, unless onlyText is true.
		/// </summary>
		public void TextNowIfNeed(bool onlyText = false, bool closingDoc = false) {
			if (_textAfterS > 0) _SaveTextNow();
			if (onlyText) return;
			Panels.Editor?.SaveEditorData(closingDoc);
		}
		
		void _SaveWorkspaceNow() {
			_workspaceAfterS = 0;
			Debug.Assert(_model != null); if (_model == null) return;
			if (!_model._SaveWorkspaceNow()) _workspaceAfterS = 60; //if fails, retry later
		}
		
		void _SaveStateNow() {
			_stateAfterS = 0;
			Debug.Assert(_model != null);
			_model?._SaveStateNow();
		}
		
		void _SaveTextNow() {
			_textAfterS = 0;
			Debug.Assert(_model != null); if (_model == null) return;
			Debug.Assert(Panels.Editor.IsOpen);
			if (!Panels.Editor.SaveText()) _textAfterS = 300; //if fails, retry later
		}
		
		/// <summary>
		/// Calls WorkspaceNowIfNeed, TextNowIfNeed, StateNowIfNeed, Panels.Bookmarks.SaveNowIfNeed, Panels.Debugg.SaveNowIfNeed.
		/// </summary>
		public void AllNowIfNeed() {
			WorkspaceNowIfNeed();
			_model.State.SuspendSave(true);
			TextNowIfNeed();
			StateNowIfNeed();
			_model.State.SuspendSave(false);
			Panels.Bookmarks.SaveNowIfNeed();
			Panels.Breakpoints.SaveNowIfNeed();
		}
		
		void _Program_Timer1s() {
			if (_workspaceAfterS > 0 && --_workspaceAfterS == 0) _SaveWorkspaceNow();
			if (_stateAfterS > 0 && --_stateAfterS == 0) _SaveStateNow();
			if (_textAfterS > 0 && --_textAfterS == 0) _SaveTextNow();
		}
	}
	
	/// <summary>
	/// Used only by the Save class.
	/// </summary>
	bool _SaveWorkspaceNow() {
		try {
			//print.it("saving");
			Root.SaveWorkspace(WorkspaceFile);
			return true;
		}
		catch (Exception ex) { //XElement.Save exceptions are undocumented
			dialog.showError("Failed to save", WorkspaceFile, expandedText: ex.Message);
			return false;
		}
	}
	
	/// <summary>
	/// Used only by the Save class.
	/// </summary>
	void _SaveStateNow() {
		State.FilesSave(_openFiles, Root.Descendants().Where(n => n.IsExpanded));
	}
	
	/// <summary>
	/// Called at the end of opening this workspace.
	/// </summary>
	public void LoadState(bool expandFolders = false, bool openFiles = false) {
		try {
			Save.LoadingState = true;
			
			if (expandFolders) {
				foreach (var id in State.FilesGet(1))
					if (FindById(id) is { } f)
						f.SetIsExpanded(true);
			}
			
			if (openFiles) {
				foreach (var id in State.FilesGet(0)) {
					if (FindById(id) is { } f) _openFiles.Add(f);
				}
				if (_openFiles.Count == 0 || !SetCurrentFile(_openFiles[0])) _UpdateOpenFiles(null); //disable Previous command
			}
		}
		catch (Exception ex) { Debug_.Print(ex); }
		finally { Save.LoadingState = false; }
	}
}
