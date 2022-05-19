class EditGoBack {
	record struct _Location(FileNode fn, int pos);

	List<_Location> _a = new();
	int _i = -1;
	bool _canGoBack, _canGoForward;
	long _time;

	internal void OnPosChanged(SciCode doc) {
		//print.it("pos", zCurrentPos8);

		bool add = false;
		int pos = doc.zCurrentPos8;
		var prev = _a.Count > 0 ? _a[_i] : default;
		if (prev.fn != doc.ZFile) {
			add = true;
		} else {
			if (pos == prev.pos) return; //eg on Back/Forward
			add = Environment.TickCount64 - _time > 500
				&& Math.Abs(doc.zLineFromPos(false, pos) - doc.zLineFromPos(false, prev.pos)) >= 10;
		}

		var now = new _Location(doc.ZFile, pos);
		if (add) {
			if (++_i < _a.Count) _a.RemoveRange(_i, _a.Count - _i); //after GoBack
			else if (_a.Count == 256) _a.RemoveAt(0);
			_i = _a.Count;
			_a.Add(now);
		} else _a[_i] = now;

		_time = Environment.TickCount64;
		_UpdateUI();

		//timer.after(1000, _ => {
		//	print.it("----");
		//	print.it(_a);
		//});
	}

	internal void OnTextModified(SciCode doc, bool deleted, int pos, int len) {
		//print.it("mod", deleted, pos, len);
		_time = 0;
		if (_i < 0) return; //probably impossible, but anyway
		if (deleted) len = -len;
		var fn = doc.ZFile;
		for (int i = _a.Count; --i >= 0;) {
			if (_a[i].fn == fn && _a[i].pos > pos) {
				_a[i] = new(fn, Math.Max(pos, _a[i].pos + len));
			}
		}
		if (deleted) { //remove adjacent duplicates
			for (int i = _a.Count - 1; --i >= 0;) {
				if (_a[i].fn == fn && _a[i].pos == pos && _a[i + 1].fn == fn && _a[i + 1].pos == pos) {
					_a.RemoveAt(i);
					if (i <= _i) _i--;
				}
			}
			//for (int i = 0; i < _a.Count; i++) print.it(_a[i].pos);
			_UpdateUI();
		}
	}

	//eg when modified externally
	internal void OnTextReplaced(FileNode fn) {
		//print.it("replaced", fn);
		_time = 0;
		if (_i < 0) return;
		int iFirst = _a.FindIndex(o => o.fn == fn); if (iFirst < 0) return;
		_a[iFirst] = new(fn, 0);
		for (int i = _a.Count; --i > iFirst;) {
			if (_a[i].fn == fn) {
				_a.RemoveAt(i);
				if (i <= _i) _i--;
			}
		}
		_UpdateUI();
	}

	internal void OnRestoringSavedPos() {
		_time = Environment.TickCount64;
	}

	internal void OnFileDeleted(FileNode fn) {
		//print.it($"before: count={_a.Count}, _i={_i}");
		for (int i = _a.Count; --i >= 0;) {
			if (_a[i].fn == fn) {
				_a.RemoveAt(i);
				if (i <= _i) _i--;
				//print.it(i, _i);
			}
		}
		//print.it($"after:  count={_a.Count}, _i={_i}");
		_time = 0;
		_UpdateUI();
	}

	public void GoBack() {
		if (_i < 1) return;
		_i--;
		_GoTo();
	}

	public void GoForward() {
		if (_i > _a.Count - 2) return;
		_i++;
		_GoTo();
	}

	void _GoTo() {
		var v = _a[_i];
		_UpdateUI();
		if (!App.Model.SetCurrentFile(v.fn)) return;
		var doc = Panels.Editor.ZActiveDoc;
		doc.zGoToPos(false, v.pos);
		doc.Focus();
	}

	void _UpdateUI() {
		bool canGoBack = _i > 0, canGoForward = _i < _a.Count - 1;
		if (canGoBack != _canGoBack) App.Commands[nameof(Menus.File.OpenCloseGo.Go_back)].Enabled = _canGoBack = canGoBack;
		if (canGoForward != _canGoForward) App.Commands[nameof(Menus.File.OpenCloseGo.Go_forward)].Enabled = _canGoForward = canGoForward;
	}

	internal static void DisableUI() {
		App.Commands[nameof(Menus.File.OpenCloseGo.Go_back)].Enabled = false;
		App.Commands[nameof(Menus.File.OpenCloseGo.Go_forward)].Enabled = false;
	}
}
