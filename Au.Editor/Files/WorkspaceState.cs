//We save workspace state data in folder "%folders.Workspace%\.state", in files:
//	state - top/pos of open files, open files, expanded folders, maybe some markers.
//		It's a text file containing 3 compact lines like:
//			editor|id1=t1p2|id2=p2h1,2,3
//			open|id1|id2
//			expanded|id1|id2
//	folding - contracted fold points of all files.
//		It's a binary file, as compact as possible. Managed by struct WorkspaceState._Folding.

/// <summary>
/// Workspace state, such as open files, expanded folders, editor caret position, folded lines.
/// WorkspaceDirectory + @"\.state\state.json"
/// </summary>
class WorkspaceState : IDisposable {
	readonly FilesModel _model;
	readonly _State _s;
	
	public WorkspaceState(FilesModel model) {
		_model = model;
		_s = new _State(model);
	}
	
	public void Dispose() {
		_s.SaveIfNeed();
	}
	
	void _Save() {
		if (_suspendSave == 0) _s.SaveIfNeed();
		else _suspendSave |= 2;
	}
	byte _suspendSave; //flags: 1 suspended, 2 save when resumed
	
	public void SuspendSave(bool suspend) {
		if (suspend) _suspendSave |= 1;
		else {
			if ((_suspendSave & 2) != 0) _s.SaveIfNeed();
			_suspendSave = 0;
		}
	}
	
	#region editor
	
	//Contains parsed editor state.
	public record struct Editor {
		public int top, pos;
		public int[] fold/*, someMarker*/;
		
		public bool Equals(in Editor e, out bool changedState, out bool changedFolding) {
			changedState = !(top == e.top && pos == e.pos /*&& someMarker.AsSpan().SequenceEqual(e.someMarker)*/);
			changedFolding = !fold.AsSpan().SequenceEqual(e.fold);
			return !(changedState || changedFolding);
		}
	}
	
	public bool EditorGet(FileNode f, out Editor x) {
		x = default;
		if (_s.Find(f, out var t)) {
			var s = _s.editor;
			for (int start = t.valueStart, end; start < t.end; start = end) {
				for (end = ++start; end < t.end && s[end] is not (>= 'g' and <= 'z');) end++;
				int what = s[start - 1];
				switch (what) {
				case 'p' or 't': //int
					var k = s.ToInt(start, STIFlags.IsHexWithout0x);
					if (what == 't') x.top = k; else x.pos = k;
					break;
				//case 'h': //int[]
				//	int n = 1; for (int j = start; j < end; j++) if (s[j] == ',') n++;
				//	var a = new int[n];
				//	a[0] = start; for (int i = 0, j = start; j < end; j++) if (s[j] == ',') a[++i] = j + 1;
				//	for (int i = 0, j = 0; i < n; i++) a[i] = j += s.ToInt(a[i], STIFlags.IsHexWithout0x); //deltas
				//	if (what == 'h') x.someMarker = a;
				//	break;
				}
			}
		}
		
		if (_Folding.Get(f) is { } af) x.fold = af.ToArray();
		
		return x != default;
	}
	
	public void EditorSave(FileNode f, in Editor x, bool changedState, bool changedFolding) {
		if (changedState) _EditorSave(x);
		if (changedFolding) _Folding.Save(f, x.fold);
		
		void _EditorSave(in Editor x) {
			string s = _EditorFormat(f, x);
			if (_s.Find(f, out var t)) {
				if (s != null && _s.editor.Eq(t.start..t.end, s)) return;
				_s.editor = string.Concat(s, _s.editor.AsSpan(..t.start), _s.editor.AsSpan(t.end..));
			} else {
				if (s == null) return;
				_s.editor = s + _s.editor;
			}
			_Save();
		}
		
		static string _EditorFormat(FileNode f, in Editor x) {
			if (x.top == 0 && x.pos == 0 /*&& x.someMarker == null*/) return null;
			using (new StringBuilder_(out var b)) {
				b.Append('|').AppendHex(f.Id).Append('=');
				if (x.top > 0) b.Append('t').AppendHex(x.top);
				if (x.pos > 0) b.Append('p').AppendHex(x.pos);
				//_AppendLines(x.someMarker, 'h');
				return b.ToString();
				
				//void _AppendLines(int[] a, char c) {
				//	if (a == null) return;
				//	b.Append(c);
				//	for (int i = 0, j = 0; i < a.Length; j = a[i++]) {
				//		if (i > 0) b.Append(',');
				//		b.AppendHex(a[i] - j); //delta
				//	}
				//}
			}
		}
	}
	
	/// <summary>
	/// When deleting a file, called to delete its saved states.
	/// </summary>
	public void EditorDelete(FileNode f) {
		if (_s.Find(f, out var v)) {
			_s.editor = _s.editor.Remove(v.start..v.end);
			_Save();
		}
		
		_Folding.Delete(f);
	}
	
	//called by FilesModel.CloseFile
	public void Cleanup() {
		if (_cleanup++ != 0) return; //only the first and every 256-th time
		
		var hs = _model.OpenFiles.Select(o => o.Id).ToHashSet();
		var b = new StringBuilder(_s.editor.Length);
		int n1 = 0, n2 = 0;
		foreach (var t in _s.editor.Split(.., '|', StringSplitOptions.RemoveEmptyEntries)) {
			n1++;
			if (!_s.editor.ToInt(out uint id, t.start, out int i, STIFlags.IsHexWithout0x)) continue; //remove if _js.editor corrupt
			if (!hs.Contains(id)) { //the file isn't in OpenFiles
				if (_model.FindById(id) == null) continue; //remove if file deleted
				if (_s.editor.AsSpan(++i..t.end).IndexOfAny("gh") < 0) continue; //remove if no markers etc
			}
			b.Append(_s.editor, t.start - 1, t.Length + 1);
			n2++;
		}
		if (n2 < n1) {
			_s.editor = b.ToString();
			_Save();
		}
		
		//CONSIDER: also cleanup folding? Normally don't need; unless some files edited manually, or an exception prevented deleting data, etc.
	}
	byte _cleanup;
	
	#endregion
	
	#region files
	
	//These functions set/get the 'open' and 'expanded' lines in the state file.
	
	/// <param name="what">0 open, 1 expanded.</param>
	public IEnumerable<uint> FilesGet(int what) {
		var s = what switch { 0 => _s.open, 1 => _s.expanded, _ => throw null };
		foreach (var v in s.Split(.., '|')) {
			yield return (uint)s.ToInt(v.start, STIFlags.IsHexWithout0x);
		}
	}
	
	public void FilesSave(IEnumerable<FileNode> open, IEnumerable<FileNode> expanded) {
		using (new StringBuilder_(out var b)) {
			_s.open = _FormatIds(b, open);
			_s.expanded = _FormatIds(b, expanded);
		}
		_Save();
		
		static string _FormatIds(StringBuilder b, IEnumerable<FileNode> e) {
			b.Clear();
			foreach (var f in e) {
				if (b.Length > 0) b.Append('|');
				b.AppendHex(f.Id);
			}
			return b.ToString();
		}
	}
	
	#endregion
	
	//Loads/saves the state file. Contains its lines as public string fields.
	class _State {
		string _file;
		public string editor, open, expanded;
		string _editor, _open, _expanded;
		
		public _State(FilesModel model) {
			_file = model.WorkspaceDirectory + @"\.state\state";
			try {
				var s = filesystem.loadText(_file);
				foreach (var line in s.Lines(..)) {
					int what = s.Eq(line.start, false, "editor|", "open|", "expanded|");
					if (what == 0) continue;
					switch (what) {
					case 1:
						editor = s[(line.start + 6)..line.end];
						break;
					case 2:
						open = s[(line.start + 5)..line.end];
						break;
					case 3:
						expanded = s[(line.start + 9)..line.end];
						break;
					}
				}
			}
			catch (Exception e1) {
				if (!filesystem.exists(_file)) _ConvertOldDb();
				else print.it(e1);
			}
			
			_editor = editor ??= "";
			_open = open ??= "";
			_expanded = expanded ??= "";
			
			//convert from old SQLite database
			void _ConvertOldDb() {
				var dbFile = model.WorkspaceDirectory + @"\state.db";
				if (filesystem.exists(dbFile, true)) {
					try {
						using (var db = new sqlite(dbFile, SLFlags.SQLITE_OPEN_READONLY)) {
							//using (var p = db.Statement("SELECT * FROM _misc")) { } //never mind open/expanded/top/pos. Convert just folding.
							_Folding.ConvertOldDb(db, model);
						}
						filesystem.delete(dbFile);
					}
					catch (Exception e1) { Debug_.Print(e1); }
				}
			}
		}
		
		public void SaveIfNeed() {
			if (editor == _editor && open == _open && expanded == _expanded) return;
			try {
				var s = $"editor{editor}\nopen|{open}\nexpanded|{expanded}\n";
				
				//print.it("saved");
				//if (editor != _editor) print.it($"\teditor: old={_editor}, new={editor}");
				//if (open != _open) print.it($"\topen: old={_open}, new={open}");
				//if (expanded != _expanded) print.it($"\texpanded: old={_expanded}, new={expanded}");
				
				filesystem.saveText(_file, s);
				_editor = editor;
				_open = open;
				_expanded = expanded;
			}
			catch (Exception e1) { Debug_.Print(e1); }
		}
		
		public bool Find(FileNode f, out (int start, int valueStart, int end) r) {
			var s = $"|{f.Id:X}=";
			int i = editor.Find(s);
			if (i < 0) { r = default; return false; }
			int j = editor.IndexOf('|', i + s.Length); if (j < 0) j = editor.Length;
			r = new(i, i + s.Length, j);
			return true;
		}
	}
	
	//Contains folding data of a source file.
	//The static functions load/save the folding file and get/set/delete folding data of a source file.
	record struct _Folding(string path, byte[] bytes, int start, int valueStart, int end) {
		//Loads the folding file if exists, and finds f data in it.
		//Returns false if: file does not exist; failed to load; did not find f data.
		//If returns false: sets path; sets bytes if loaded.
		//If returns true: sets all fields.
		static bool _Load(FileNode f, out _Folding r) {
			uint id = f.Id;
			byte[] b = null;
			var file = f.Model.WorkspaceDirectory + $@"\.state\folding";
			if (filesystem.exists(file, useRawPath: true)) {
				try {
					b = filesystem.loadBytes(file);
					//find byte 0 followed by 7-bit-encoded id. There are no other byte 0, because: an id cannot be 0; a line delta cannot be 0.
					Span<byte> sid = stackalloc byte[8];
					sid = sid[.._Encode7Bit(id, sid, 1)];
					int start = b.AsSpan().IndexOf(sid);
					if (start >= 0) {
						int vstart = start + sid.Length, end = vstart;
						while (end < b.Length && b[end] != 0) end++;
						if (end > start) { //else corrupt
							r = new(file, b, start, vstart, end);
							return true;
						}
					}
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}
			r = new(file, b, 0, 0, 0);
			return false;
		}
		
		//7-bit encodes id.
		//span - memory for writing. Must be at least 5 bytes + offset.
		//offset - offset in <i>span</i> where to start writing.
		//Returns the end offset.
		static int _Encode7Bit(uint id, Span<byte> span, int offset = 0) {
			while (id > 0x7Fu) { span[offset++] = (byte)(id | 0x80u); id >>= 7; }
			span[offset++] = (byte)id;
			return offset;
		}
		
		/// <summary>
		/// Gets <i>f</i> data from the folding file.
		/// </summary>
		/// <returns>[+] lines. Returns null if: no file; no data; failed.</returns>
		public static List<int> Get(FileNode f) {
			if (_Load(f, out var x)) {
				try {
					var a = new List<int>();
					//read array of 7-bit encoded line deltas
					var ms = new MemoryStream(x.bytes, x.valueStart, x.end - x.valueStart, false);
					using var r = new BinaryReader(ms);
					int line = -1;
					while (ms.Position < ms.Length) {
						int delta = r.Read7BitEncodedInt();
						a.Add(line += delta);
					}
					return a;
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}
			return null;
		}
		
		/// <summary>
		/// If <i>a</i> null, deletes <i>f</i> data from file if exists. Else saves <i>f</i> data in file, unless same data is already saved.
		/// </summary>
		/// <param name="a">[+] lines or null</param>
		public static void Save(FileNode f, int[] a) {
			if (a == null) {
				Delete(f);
			} else {
				var ms = new MemoryStream();
				using var w = new BinaryWriter(ms);
				Write(w, f.Id, a);
				var b = ms.ToArray();
				
				bool exists = _Load(f, out var x);
				if (exists && x.bytes.AsSpan(x.start..x.end).SequenceEqual(b)) return;
				
				try {
					filesystem.save(x.path, temp => {
						using var fs = File.Create(temp);
						fs.Write(b);
						if (exists) {
							fs.Write(x.bytes, 0, x.start);
							fs.Write(x.bytes, x.end, x.bytes.Length - x.end);
						}
					});
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}
		}
		
		public static void Write(BinaryWriter w, uint id, int[] a) {
			w.Write((byte)0); //record separator, to find the record later. Other data does not contain 0. The minimal possible delta is 1 (see 'line = -1').
			w.Write7BitEncodedInt((int)id);
			for (int i = 0, line = -1; i < a.Length; line = a[i++]) {
				int delta = a[i] - line;
				Debug.Assert(delta > 0);
				w.Write7BitEncodedInt(delta);
			}
		}
		
		public static void Delete(FileNode f) {
			if (_Load(f, out var x)) {
				try {
					var b = x.bytes.RemoveAt(x.start, x.end - x.start);
					//if (b.Length > 0) filesystem.saveBytes(x.path, b); else filesystem.delete(x.path);
					filesystem.saveBytes(x.path, b);
				}
				catch (Exception e1) { Debug_.Print(e1); }
			}
		}
		
		public static void ConvertOldDb(sqlite db, FilesModel model) {
			var ms = new MemoryStream();
			using var w = new BinaryWriter(ms);
			using var p = db.Statement("SELECT id,lines FROM _editor ORDER BY id");
			while (p.Step()) {
				uint id = (uint)p.GetInt(0);
				if (model.FindById(id) != null && p.GetArray<int>(1) is { } a) {
					for (int i = 0; i < a.Length; i++) a[i] &= 0x7FFFFFF;
					_Folding.Write(w, id, a);
				}
			}
			filesystem.saveBytes(model.WorkspaceDirectory + @"\.state\folding", ms.ToArray());
		}
	}
}
