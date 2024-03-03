namespace Au.More;

/// <summary>
/// Manages native vertical or horizontal scrollbar of a window.
/// </summary>
internal /*abstract*/ class NativeScrollbar_ {
	readonly bool _vertical;
	readonly Func<int, int> _itemStart, _itemEnd;
	wnd _w;
	int _pos, _max, _nItems, _offset;
	
	public NativeScrollbar_(bool vertical, Func<int, int> itemStart, Func<int, int> itemEnd) {
		_vertical = vertical;
		_itemStart = itemStart;
		_itemEnd = itemEnd;
	}
	//public NativeScrollbar_(bool vertical) {
	//	_vertical = vertical;
	//}
	
	public bool Visible {
		get => _visible;
		set => _Show(value, true);
	}
	bool _visible;
	
	void _Show(bool show, bool api) {
		if (_visible != show) {
			_visible = show;
			if (!show) {
				if (_pos != 0 && !_w.Is0) Api.SCROLLINFO.SetPos(_w, _vertical, 0, false);
				_pos = _max = _offset = 0;
			}
			if (api && !_w.Is0) Api.ShowScrollBar(_w, _vertical ? Api.SB_VERT : Api.SB_HORZ, show);
		}
	}
	
	/// <summary>
	/// If need, shows or hides vertical and/or horizontal scrollbar of same window.
	/// When need to show or hide both, this function is 2 times faster than calling <see cref="Visible"/> separately.
	/// </summary>
	public static void ShowVH(NativeScrollbar_ vert, bool showV, NativeScrollbar_ horz, bool showH) {
		Debug.Assert(vert._w == horz._w && vert._vertical && !horz._vertical);
		if (showV == vert.Visible && showH == horz.Visible) return;
		bool dif = showV != showH;
		vert._Show(showV, dif);
		horz._Show(showH, dif);
		if (!dif) Api.ShowScrollBar(vert._w, Api.SB_BOTH, showV); //2 times faster and 2 times less messages, except wm_paint
	}
	
	//public void SetRange(int max, int page) {
	//	_max = max;
	//	_pos = Math.Min(_pos, max);
	//	Api.SCROLLINFO.SetRange(_w, _vertical, max + page - 1, page, true);
	//}
	
	//protected abstract int ItemStart(int i);
	//protected abstract int ItemEnd(int i);
	
	public void SetRange(int nItems) {
		_max = 0;
		_nItems = nItems;
		if (nItems > 1) {
			var rc = _w.ClientRect;
			int to = _itemEnd(_nItems - 1) - (_vertical ? rc.Height : rc.Width);
			for (int i = _nItems; --i >= 0;) { //how many items in the last page?
				if (_itemStart(i) < to) { _max = i + 1; break; }
			}
		}
		_SetPos(_pos);
		Api.SCROLLINFO.SetRange(_w, _vertical, _nItems - 1, _nItems - _max, true);
	}
	
	bool _SetPos(int pos) {
		pos = Math.Clamp(pos, 0, _max);
		if (pos == _pos) return false;
		_pos = pos;
		_offset = _pos == 0 ? 0 : _itemStart(_pos);
		return true;
	}
	
	void _Scroll(int pos, int part) {
		if (!_SetPos(pos)) return;
		Api.SCROLLINFO.SetPos(_w, _vertical, _pos, true);
		PosChanged?.Invoke(this, part);
	}
	
	/// <summary>
	/// Gets current scroll position (index of top visible item).
	/// Setter sets scroll position, clamped 0-Max. Does not invalidate the control.
	/// </summary>
	public int Pos {
		get => _pos;
		set => _Scroll(value, -1);
	}
	
	public int Offset => _offset;
	
	/// <summary>
	/// Gets max scroll position. Returns 0 if no scrollbar.
	/// </summary>
	public int Max => _max;
	
	/// <summary>
	/// Gets or sets item count.
	/// Use setter to set item count when there is no scrollbar; asserts !_visible.
	/// </summary>
	public int NItems {
		get => _nItems;
		set {
			Debug.Assert(!_visible);
			_nItems = value;
		}
	}
	
	/// <summary>
	/// When scrollbar position changed.
	/// The <b>int</b> parameter is event source: if scrollbar, it is one of Api.SB_ constants; if <see cref="Pos"/>, it is -1; if wheel, it is -2 if down, -3 if up.
	/// </summary>
	public event Action<NativeScrollbar_, int> PosChanged;
	
	public bool WndProc(wnd w, int msg, nint wParam, nint lParam) {
		_w = w;
		switch (msg) {
		case Api.WM_NCDESTROY:
			_w = default;
			break;
		case Api.WM_VSCROLL when _vertical:
		case Api.WM_HSCROLL when !_vertical:
			_WmScroll(Math2.LoWord(wParam));
			return true;
		case Api.WM_MOUSEWHEEL:
			_WmScroll(Math2.HiShort(wParam) < 0 ? -2 : -3);
			return true;
		}
		return false;
	}
	
	void _WmScroll(int part) {
		if (!_visible) return;
		int pos = _pos;
		switch (part) {
		case Api.SB_THUMBTRACK:
			pos = Api.SCROLLINFO.GetTrackPos(_w, _vertical);
			break;
		case Api.SB_LINEDOWN: pos++; break;
		case Api.SB_LINEUP: pos--; break;
		case Api.SB_PAGEDOWN or Api.SB_PAGEUP:
			var rc = _w.ClientRect;
			int clientSize = _vertical ? rc.Height : rc.Width;
			if (part == Api.SB_PAGEDOWN) {
				for (int to = _offset + clientSize; pos < _nItems && _itemEnd(pos) <= to;) pos++;
			} else {
				for (int to = _offset - clientSize; pos >= 0 && _itemStart(pos) >= to;) pos--;
				pos++;
			}
			break;
		case Api.SB_TOP: pos = 0; break;
		case Api.SB_BOTTOM: pos = _max; break;
		case -2 or -3:
			int k = Api.SystemParametersInfo(Api.SPI_GETWHEELSCROLLLINES, 3);
			pos += part == -2 ? k : -k;
			break;
		default: return;
		}
		_Scroll(pos, part);
	}
	
	/// <summary>
	/// Calculates new focused item index when pressed key Down, Up, PageDown, PageUp, End or Home.
	/// </summary>
	/// <param name="i">Current focused item index. Can be -1.</param>
	/// <param name="k"></param>
	/// <remarks>
	/// This scrollbar must be vertical. Asserts.
	/// Returns unchanged <i>i</i> (even if -1) if <i>k</i> isn't a navigation key or if cannot change focused item.
	/// Works like standard list controls.
	/// </remarks>
	public int KeyNavigate(int i, KKey k) {
		Debug.Assert(_vertical);
		if (!_vertical || _nItems == 0) return i;
		switch (k) {
		case KKey.Home: i = 0; break;
		case KKey.End: i = int.MaxValue; break;
		case KKey.Down: i++; break;
		case KKey.Up: if (i < 0) i = int.MaxValue; else i--; break;
		case KKey.PageDown when i < _nItems - 1:
		case KKey.PageUp when i > 0:
			int clientHeight = _w.ClientRect.Height;
			if (k == KKey.PageDown) {
				int to = _offset + clientHeight;
				if (_itemEnd(i + 1) > to) to = _itemStart(i + 1) + clientHeight;
				while (i + 1 < _nItems && _itemEnd(i + 1) <= to) i++;
			} else {
				int to = _offset;
				if (i == _pos) to -= clientHeight;
				while (i - 1 >= 0 && _itemStart(i - 1) >= to) i--;
			}
			break;
		default: return i;
		}
		
		return Math.Clamp(i, 0, _nItems - 1);
	}
}
