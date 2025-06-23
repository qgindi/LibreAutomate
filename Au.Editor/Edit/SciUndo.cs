using Au.Controls;
using static Au.Controls.Sci;

class SciUndo : IDisposable {
	public static SciUndo OfWorkspace => (App.Model.UndoContext_ ??= new SciUndo()) as SciUndo;
	
	sqlite _db;
	SLTransaction _transaction; //of current record
	int _id; //of current record
	int _nFiles; //in current record
	SciCode _lastDoc; //in current record
	int _idToUndo;
	HashSet<int> _invalidIds = [];
	
	void IDisposable.Dispose() {
		if (_db != null) {
			_db.Dispose();
			_db = null;
		}
	}
	
	/// <summary>
	/// Starts a multi-file replace undo record.
	/// </summary>
	public void StartReplaceInFiles() {
		_nFiles = 0;
		_lastDoc = null;
		try {
			//temp database. Uses a temp file when exceeds 2 MB.
			_db ??= new sqlite(@"", sql: """
CREATE TABLE records (id INTEGER, descr TEXT);
CREATE TABLE files (id INTEGER, fileId INTEGER, oldHash BLOB, newHash BLOB, undo BLOB, redo BLOB, find TEXT, repl TEXT);
""");
		}
		catch (Exception e1) { Debug_.Print(e1); return; }
		_transaction = _db.Transaction();
		_id++;
	}
	
	/// <summary>
	/// Commits the multi-file replace undo record started with <see cref="StartReplaceInFiles"/>.
	/// </summary>
	public void FinishReplaceInFiles(string operationDescription) {
		if (_db == null) return;
		if (_nFiles == 1 && Panels.Editor.ActiveDoc is SciCode ad && _lastDoc == ad) { //if replaced only in Panels.Editor.ActiveDoc
			_nFiles--;
			ad.ESetUndoMark_(-1);
		}
		if (_nFiles > 0) {
			try {
				_db.Execute("INSERT INTO records VALUES (?, ?)", _id, operationDescription);
				_transaction.Commit();
				
				//if was undone, that range of ids becomes invalid. Let UndoRedoMultiFileReplace skip them.
				for (int i = _idToUndo + 1; i < _id; i++) _invalidIds.Add(i); //never mind: also should remove from the database
				
				_idToUndo = _id;
				return;
			}
			catch (Exception e1) { print.it(e1); }
		}
		_transaction.Rollback();
		_id--;
	}
	
	/// <summary>
	/// Adds an open file to the current record.
	/// </summary>
	public void RifAddFile(SciCode doc, string oldText, string newText, List<StartEndText> changes) {
		if (_db == null) return;
		int n = _nFiles;
		RifAddFile(doc.EFile, oldText, newText, changes);
		if (_nFiles > n) (_lastDoc = doc).ESetUndoMark_(_id);
	}
	
	/// <summary>
	/// Adds a closed file to the current record.
	/// </summary>
	public void RifAddFile(FileNode f, string oldText, string newText, List<StartEndText> changes) {
		if (_db == null) return;
		
		//optimization: don't store full texts of files. Store only hashes and changes. Can make 100 times smaller.
		
		//optimization: don't store full info if all "find" or/and "replace" texts are same. They can be different only if used regex. Can make 4 times smaller.
		string sFind = oldText[changes[0].Range]; for (int i = 1; i < changes.Count; i++) if (!oldText.Eq(changes[i].Range, sFind)) { sFind = null; break; }
		string sRepl = changes[0].text; for (int i = 1; i < changes.Count; i++) if (changes[i].text != sRepl) { sRepl = null; break; }
		
		var ms = new MemoryStream();
		var bw = new BinaryWriter(ms);
		try {
			using var p = _db.Statement("INSERT INTO files VALUES (?, ?, ?, ?, ?, ?, ?, ?)");
			p.Bind(1, _id)
				.Bind(2, f.Id)
				.BindStruct(3, Hash.MD5(oldText))
				.BindStruct(4, Hash.MD5(newText))
				.Bind(5, _TextToBlob(false))
				.Bind(6, _TextToBlob(true))
				.Bind(7, sFind)
				.Bind(8, sRepl)
				.Step();
		}
		catch (Exception e1) { print.it(e1); return; }
		_nFiles++;
		
		Span<byte> _TextToBlob(bool redo) {
			ms.SetLength(0);
			int offset = 0;
			foreach (var v in changes) {
				if (redo) {
					bw.Write7BitEncodedInt(v.start);
					if (sFind == null) bw.Write7BitEncodedInt(v.Length);
					if (sRepl == null) bw.Write(v.text);
				} else {
					bw.Write7BitEncodedInt(v.start + offset);
					offset += v.text.Length - v.Length;
					if (sRepl == null) bw.Write7BitEncodedInt(v.text.Length);
					if (sFind == null) bw.Write(oldText[v.start..v.end]); //not bw.Write(oldText.AsSpan(v.start..v.end));
				}
			}
			return ms.GetBuffer().AsSpan(0, (int)ms.Position);
		}
	}
	
	bool _RifUndoRedo(bool redo, int id) {
		if (id < 1 || id > _id) return false;
		_db.Get(out string descr, "SELECT descr FROM records WHERE id=?", id);
		_db.Get(out int count, "SELECT COUNT(*) FROM files WHERE id=?", id);
		if (!dialog.showOkCancel($"{(redo ? "Redo" : "Undo")} in {count} files", "It was:\n" + descr, DFlags.CenterMouse, owner: App.Hmain))
			return false; //rejected: option to undo/redo only in this file. Confusing and probably not useful.
		
		StringBuilder skipped = null;
		//these will be reused to make less allocations
		StringBuilder sb = null;
		List<StartEndText> aset = new();
		MemoryStream ms = new();
		BinaryReader br = new(ms);
		
		using var x = _db.Statement("SELECT fileId, oldHash, newHash, undo, redo, find, repl FROM files WHERE id=?", id);
		while (x.Step()) {
			if (App.Model.FindById(x.GetInt(0)) is not FileNode f) continue;
			//print.it(f);
			if (f.OpenDoc is SciCode doc) {
				int mark = doc.EGetUndoMark_(redo);
				if (mark == id) {
					Debug_.PrintIf(Hash.MD5(doc.aaaText) != _HashBefore());
					_UndoRedo(doc, redo, mark);
					Debug_.PrintIf(Hash.MD5(doc.aaaText) != _HashAfter());
				} else if (0 == doc.Call(SCI_CANUNDO) && 0 == doc.Call(SCI_CANREDO) && Hash.MD5(doc.aaaText) == _HashBefore()) { //opened later
					doc.EReplaceTextGently(_TextFromBlob(doc.aaaText));
					doc.Call(SCI_EMPTYUNDOBUFFER);
				} else {
					_Skipped(f);
					continue;
				}
				doc.ESaveText_(true);
			} else {
				try {
					var textNow = filesystem.loadText(f.FilePath);
					if (Hash.MD5(textNow) == _HashBefore()) {
						f.SaveNewTextOfClosedFile(_TextFromBlob(textNow));
					} else {
						_Skipped(f);
					}
				}
				catch (Exception e1) { _Skipped(f, e1.ToString()); }
			}
			
			Hash.MD5Result _HashBefore() => x.GetStruct<Hash.MD5Result>(redo ? 1 : 2);
			Hash.MD5Result _HashAfter() => x.GetStruct<Hash.MD5Result>(redo ? 2 : 1);
			
			unsafe string _TextFromBlob(string textNow) {
				string sFind = x.GetText(5), sRepl = x.GetText(6);
				
				aset.Clear();
				var blob = x.GetBlob(redo ? 4 : 3, out int len1);
				ms.SetLength(0); ms.Write(new(blob, len1)); ms.Position = 0;
				
				while (ms.Position < len1) {
					int i = br.Read7BitEncodedInt();
					int len = (redo && sFind != null) ? sFind.Length : (!redo && sRepl != null) ? sRepl.Length : br.Read7BitEncodedInt();
					string s = (redo && sRepl != null) ? sRepl : (!redo && sFind != null) ? sFind : br.ReadString();
					aset.Add(new(i, i + len, s));
				}
				
				StartEndText.ReplaceAll(textNow, aset, ref sb);
				return sb.ToString();
			}
		}
		
		if (skipped != null) print.it(skipped);
		
		void _Skipped(FileNode f, string s = null) {
			skipped ??= new($"<>The multi-file {(redo ? "redo" : "undo")} operation skipped these files:\r\n");
			skipped.AppendLine($"\t{f.SciLink(true)}. {s ?? "Text modified later."}");
		}
		
		_idToUndo = redo ? id : id - 1;
		return true;
	}
	
	public void UndoRedo(bool redo) {
		var doc = Panels.Editor.ActiveDoc; if (doc == null) return;
		int mark = doc.EGetUndoMark_(redo);
		if (mark > 0)
			_RifUndoRedo(redo, mark);
		else
			_UndoRedo(doc, redo, mark);
	}
	
	/// <summary>
	/// Calls SCI_UNDO or SCI_REDO.
	/// If <i>mark</i> != 0, does not change current position at the end of the operation.
	/// To set and get mark use <see cref="SciCode.ESetUndoMark_"/> and <see cref="SciCode.EGetUndoMark_"/>.
	/// </summary>
	static void _UndoRedo(SciCode doc, bool redo, int mark) {
		doc.Call(redo ? SCI_REDO : SCI_UNDO, mark != 0);
	}
	
	public void UndoRedoMultiFileReplace(bool redo) {
		int id = _idToUndo; if (redo) id++;
		while (_invalidIds.Contains(id)) id += redo ? 1 : -1;
		_RifUndoRedo(redo, id);
	}
}

partial class SciCode {
	List<(int u, int mark)> _undoMarks;
	
	internal void ESetUndoMark_(int mark) {
		_undoMarks ??= [];
		int u = Call(SCI_GETUNDOCURRENT) - 1;
		if (u < 0) return;
		if (0 != (256 & Call(SCI_GETUNDOACTIONTYPE, u))) throw new InvalidOperationException("Cannot set Undo mark for a possibly not full Undo action"); //has flag "can coalesce"
		Debug_.PrintIf(_undoMarks.Any(o => o.u >= u));//TODO: remove
		_undoMarks.Add((u, mark));
	}
	
	internal int EGetUndoMark_(bool redo) {
		if (!_undoMarks.NE_()) {
			int u = Call(SCI_GETUNDOCURRENT);
			if (redo) {
				int n = Call(SCI_GETUNDOACTIONS);
				while (u < n && 0 != (256 & Call(SCI_GETUNDOACTIONTYPE, u))) u++; //if multiple actions coalesced, get the last, because only it can be marked
				if (u >= n) return 0;
			} else {
				if (--u < 0) return 0;
			}
			//print.it(u, _GetUndoStack(), _undoMarks);
			var a = _undoMarks;
			for (int i = a.Count; --i >= 0;) {
				if (a[i].u == u) return a[i].mark;
				if (a[i].u < u) break;
			}
		}
		return 0;
	}
	
	void _ManageUndoOnModified(MOD mod) {
		if (mod.Has(MOD.SC_PERFORMED_USER)) {
			//from _undoMarks remove items for Scintilla Undo actions that have been removed by SCI_EMPTYUNDOBUFFER or when user-modified after an Undo or non-last Redo
			if (_undoMarks is { } a) {
				int n = Call(SCI_GETUNDOACTIONS) - 1; //the last action in the Scintilla's Undo stack. If the Undo stack previously had >= actions than now, they were removed and added this action.
				while (a.Count > 0 && a[^1].u >= n) a.RemoveAt(a.Count - 1);
			}
		}
	}
	
	//#if DEBUG
	//	int[] _GetUndoStack() {
	//		int n = Call(SCI_GETUNDOACTIONS);
	//		var a = new int[n];
	//		for (int i = 0; i < n; i++) {
	//			a[i] = Call(SCI_GETUNDOACTIONTYPE, i);
	//		}
	//		return a;
	//	}
	//#endif
	
	/// <summary>
	/// <c>=> new aaaUndoAction(this);</c>
	/// </summary>
	public EUndoAction ENewUndoAction(bool onUndoDontChangeCaretPos = false) => new EUndoAction(this, onUndoDontChangeCaretPos);
	
	/// <summary>
	/// Ctor calls <see cref="KScintilla.aaaBeginUndoAction"/>. Dispose() calls <see cref="KScintilla.aaaEndUndoAction"/>.
	/// Does nothing if it's a nested undo action.
	/// </summary>
	public struct EUndoAction : IDisposable {
		SciCode _sci;
		bool _onUndoDontChangeCaretPos;
		
		/// <summary>
		/// Calls SCI_BEGINUNDOACTION.
		/// </summary>
		/// <param name="sci">Can be null, then does nothing.</param>
		/// <param name="onUndoDontChangeCaretPos"></param>
		public EUndoAction(SciCode sci, bool onUndoDontChangeCaretPos = false) {
			_onUndoDontChangeCaretPos = onUndoDontChangeCaretPos;
			_sci = sci;
			_sci?.aaaBeginUndoAction();
		}
		
		/// <summary>
		/// Calls SCI_ENDUNDOACTION and clears this variable.
		/// </summary>
		public void Dispose() {
			if (_sci != null) {
				if (0 == _sci.aaaEndUndoAction()) {
					if (_onUndoDontChangeCaretPos) _sci.ESetUndoMark_(-1);
				}
				_sci = null;
			}
		}
	}
}
