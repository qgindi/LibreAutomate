using System.Linq;

partial class FilesModel
{
	public class AutoSave
	{
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
		/// Sets timer to save state.xml later, if not already set.
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
		/// If state.xml is set to save (StateLater), saves it now.
		/// </summary>
		public void StateNowIfNeed() {
			if (_stateAfterS > 0) _SaveStateNow();
		}

		/// <summary>
		/// If editor text is set to save (TextLater), saves it now.
		/// Also saves markers, folding, etc, unless onlyText is true.
		/// </summary>
		public void TextNowIfNeed(bool onlyText = false) {
			if (_textAfterS > 0) _SaveTextNow();
			if (onlyText) return;
			Panels.Editor.ZSaveEditorData();
		}

		void _SaveWorkspaceNow() {
			_workspaceAfterS = 0;
			Debug.Assert(_model != null); if (_model == null) return;
			if (!_model._SaveWorkspaceNow()) _workspaceAfterS = 60; //if fails, retry later
		}

		void _SaveStateNow() {
			_stateAfterS = 0;
			Debug.Assert(_model != null); if (_model == null) return;
			if (!_model._SaveStateNow()) _stateAfterS = 300; //if fails, retry later
		}

		void _SaveTextNow() {
			_textAfterS = 0;
			Debug.Assert(_model != null); if (_model == null) return;
			Debug.Assert(Panels.Editor.ZIsOpen);
			if (!Panels.Editor.ZSaveText()) _textAfterS = 300; //if fails, retry later
		}

		/// <summary>
		/// Calls WorkspaceNowIfNeed, StateNowIfNeed, TextNowIfNeed.
		/// </summary>
		public void AllNowIfNeed() {
			WorkspaceNowIfNeed();
			StateNowIfNeed();
			TextNowIfNeed();
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
			Root.Save(WorkspaceFile);
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
	bool _SaveStateNow() {
		if (DB == null) return true;
		try {
			using (var trans = DB.Transaction()) {
				DB.Execute("REPLACE INTO _misc VALUES ('expanded',?)",
					string.Join(" ", Root.Descendants().Where(n => n.IsExpanded).Select(n => n.IdString)));

				using (new StringBuilder_(out var b)) {
					var a = OpenFiles;
					b.Append(a.IndexOf(_currentFile));
					foreach (var v in a) b.Append(' ').Append(v.IdString); //FUTURE: also save current position and scroll position, eg "id.pos.scroll"
					DB.Execute("REPLACE INTO _misc VALUES ('open',?)", b.ToString());
				}

				trans.Commit();
			}
			return true;
		}
		catch (SLException ex) {
			Debug_.Print(ex);
			return false;
		}
	}

	/// <summary>
	/// Called at the end of opening this workspace.
	/// </summary>
	public void LoadState(bool expandFolders=false, bool openFiles=false) {
		if (DB == null) return;
		try {
			Save.LoadingState = true;

			if (expandFolders) {
				if (DB.Get(out string s, "SELECT data FROM _misc WHERE key='expanded'") && !s.NE()) {
					foreach (var v in s.Segments(" ")) {
						var f = FindById(s[v.Range]);
						//if (f != null) TreeControl.Expand(f, true);
						if (f != null) f.SetIsExpanded(true);
					}
				}
			}

			if (openFiles) {
				if (DB.Get(out string s, "SELECT data FROM _misc WHERE key='open'") && !s.NE()) {
					//format: indexOfActiveDocOrMinusOne id1 id2 ...
					int i = -2, iActive = s.ToInt();
					FileNode fnActive = null;
					//perf.first();
					foreach (var v in s.Segments(" ")) {
						i++; if (i < 0) continue;
						var fn = FindById(s[v.Range]); if (fn == null) continue;
						OpenFiles.Add(fn);
						if (i == iActive) fnActive = fn;
					}
					//perf.next();
					if (fnActive == null || !SetCurrentFile(fnActive)) _UpdateOpenFiles(null); //disable Previous command
					//perf.nw();
				}
			}
		}
		catch (Exception ex) { Debug_.Print(ex); }
		finally { Save.LoadingState = false; }
	}
}
