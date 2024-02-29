using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Au.Controls;

public unsafe partial class KTreeView {
	_VisibleItem[] _avi;
	Dictionary<ITreeViewItem, int> _dvi; //for IndexOf
	int _width, _height, _itemHeight, _itemLineHeight, _imageSize, _imageMarginX, _marginLeft, _marginRight, _itemsWidth, _dpi;
	int _focusedIndex, _hotIndex;
	(int indexPlus1, bool scrollTop) _ensureVisible;
	NativeScrollbar_ _vscroll, _hscroll;
	
	struct _VisibleItem {
		public ITreeViewItem item;
		public int measured;
		public ushort level;
		public TVParts noParts;
		public bool isSelected;
		
		public bool Select(bool on) {
			if (on && !item.IsSelectable) return false;
			isSelected = on;
			return true;
		}
	}
	
	///
	public KTreeView() {
		//UseLayoutRounding = true;
		Focusable = true;
		FocusVisualStyle = null;
		_focusedIndex = _hotIndex = -1;
		_ScrollInit();
	}
	
	#region size, dpi, set visible items, root/index/count properties
	
	void _SetDpiAndItemSize(int dpi) {
		_dpi = dpi;
		_imageSize = _dpi / 6;
		_imageMarginX = _DpiScale(4);
		_marginLeft = _DpiScale(ItemMarginLeft);
		_marginRight = _DpiScale(ItemMarginRight);
		
		using var tr = new GdiTextRenderer(dpi);
		_itemHeight = _itemLineHeight = Math.Max(_imageSize, tr.MeasureText("A").height) + _DpiScale(2);
		if (CustomItemHeightAddPercent > 0) _itemHeight += Math2.PercentToValue(_itemHeight, CustomItemHeightAddPercent, true);
	}
	
	///
	protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) {
		if (_hasHwnd) {
			_SetDpiAndItemSize(newDpi.PixelsPerInchY.ToInt()); //don't use Dpi.OfWindow(this), it's invalid when reparenting
			_MeasureClear(false);
		}
		base.OnDpiChanged(oldDpi, newDpi);
	}
	
	///
	public int Dpi => _dpi;
	
	void _SetVisibleItems(bool init) {
		bool wasEmpty = _avi.NE_();
		_hotIndex = -1;
		_ensureVisible = default;
		
		int n = _itemsSource == null ? 0 : _CountVisible(_itemsSource);
		static int _CountVisible(IEnumerable<ITreeViewItem> a) {
			int r = 0;
			foreach (var v in a) {
				r++;
				if (v.IsExpanded) r += _CountVisible(v.Items);
			}
			return r;
		} //SHOULDDO: now calls v.Items 2 times (_CountVisible and _AddVisible). It can be expensive.
		
		if (n == 0) {
			_avi = Array.Empty<_VisibleItem>();
			_dvi = null;
			_focusedIndex = -1;
			_itemsWidth = 0;
			if (wasEmpty) return;
		} else {
			List<ITreeViewItem> selected = null; ITreeViewItem focused = null;
			if (!(init || wasEmpty)) {
				selected = SelectedItems;
				focused = FocusedItem;
			}
			_focusedIndex = -1;
			_itemsWidth = 0;
			
			_avi = new _VisibleItem[n];
			_dvi = new Dictionary<ITreeViewItem, int>(n);
			_AddVisible(_itemsSource, 0, 0);
			int _AddVisible(IEnumerable<ITreeViewItem> a, int i, int level) {
				foreach (var v in a) {
					_avi[i] = new _VisibleItem { item = v, level = (ushort)level };
					_dvi.Add(v, i);
					i++;
					if (v.IsExpanded) i = _AddVisible(v.Items, i, level + 1);
				}
				return i;
			}
			
			if (selected != null) {
				foreach (var v in selected) {
					int i = IndexOf(v); if (i < 0) continue;
					_avi[i].Select(true);
					if (v == focused) _focusedIndex = i;
				}
			}
		}
		
		if (_hasHwnd) {
			if (init && _vscroll.Pos > 0) _vscroll.Pos = 0;
			
			_Measure();
			_Invalidate();
		}
	}
	
	/// <summary>
	/// Sets (adds, replaces or removes) all items.
	/// </summary>
	/// <param name="items">Items at tree root. Can be null.</param>
	/// <param name="modified">true when adding/removing one or more items in same tree/list. Preserves selection, scroll position, etc.</param>
	public void SetItems(IEnumerable<ITreeViewItem> items, bool modified = false) {
		_itemsSource = items;
		_SetVisibleItems(!modified);
	}
	IEnumerable<ITreeViewItem> _itemsSource;
	
	/// <summary>
	/// Gets the number of visible items.
	/// </summary>
	public int CountVisible => _avi.Lenn_();
	
	/// <summary>
	/// Gets item index in visible items.
	/// Returns -1 if not found.
	/// </summary>
	/// <param name="item">Can be null.</param>
	public int IndexOf(ITreeViewItem item) {
		if (_dvi != null && _dvi.TryGetValue(item, out int i)) return i;
		return -1;
	}
	
	/// <summary>
	/// Gets item index in visible items.
	/// Returns -1 if not found.
	/// </summary>
	public int IndexOf(Func<ITreeViewItem, bool> func) {
		if (!_avi.NE_()) {
			for (int i = 0; i < _avi.Length; i++) {
				if (func(_avi[i].item)) return i;
			}
		}
		return -1;
	}
	
	/// <summary>
	/// Gets visible item at index.
	/// </summary>
	/// <exception cref="IndexOutOfRangeException"></exception>
	/// <exception cref="NullReferenceException">Items not added.</exception>
	public ITreeViewItem this[int index] => _avi[index].item;
	
	#endregion
	
	#region scroll, top index, expand folder, ensure visible
	
	void _ScrollInit() {
		_vscroll = new(true, i => i * _itemHeight, i => (i + 1) * _itemHeight);
		_hscroll = new(false, i => i, i => i + 1);
		
		_vscroll.PosChanged += (sb, part) => {
			_hotIndex = -1;
			_Measure(true);
			_Invalidate();
			if (part <= -2) _OnMouseMoveOrScroll(true); //wheel, not scrollbar or keyboard
		};
		
		_hscroll.PosChanged += (sb, part) => {
			_Invalidate();
		};
	}
	
	///
	protected override void OnMouseWheel(MouseWheelEventArgs e) {
		e.Handled = true;
		if (!_avi.NE_() && _vscroll.Visible) _vscroll.WndProc(_w, Api.WM_MOUSEWHEEL, Math2.MakeLparam(0, e.Delta), 0);
		base.OnMouseWheel(e);
	}
	
	/// <summary>
	/// Gets or sets index of the first item in the scroll view; it is the value of the vertical scrollbar.
	/// The <c>set</c> function scrolls if need. Clamps if invalid index.
	/// </summary>
	public int TopIndex {
		get => _vscroll.Pos;
		set { _vscroll.Pos = value; }
	}
	
	/// <summary>
	/// Expands or collapses folder.
	/// </summary>
	/// <param name="index"></param>
	/// <param name="expand">If null, toggles.</param>
	/// <exception cref="InvalidOperationException">Not folder. Or control not created.</exception>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public void Expand(int index, bool? expand) {
		if (!_hasHwnd) throw new InvalidOperationException();
		if (!_IndexToItem(index, out var item)) throw new IndexOutOfRangeException();
		if (!item.IsFolder) throw new InvalidOperationException();
		bool wasExp = item.IsExpanded;
		bool exp = expand == null ? !wasExp : expand.Value;
		if (exp == wasExp) return;
		if (!exp && _focusedIndex > index && _IsInside(index, _focusedIndex)) _avi[_focusedIndex = index].Select(true); //select/focus item if a descendant is focused
		item.SetIsExpanded(exp);
		_SetVisibleItems(init: false);
	}
	
	/// <summary>
	/// Expands or collapses folder.
	/// </summary>
	/// <param name="item"></param>
	/// <param name="expand">If null, toggles.</param>
	/// <exception cref="InvalidOperationException">Not folder. Or control not created.</exception>
	/// <exception cref="ArgumentException"><i>item</i> is not a visible item in this control. No exception if <i>expand</i> == false.</exception>
	public void Expand(ITreeViewItem item, bool? expand) {
		if (!_IndexOfOrThrowIfImportant(expand != false, item, out int i)) return;
		Expand(i, expand);
	}
	
	//internal bool test;
	
	/// <summary>
	/// Scrolls if need to make item actually visible.
	/// </summary>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public void EnsureVisible(int index, bool scrollTop = false) {
		if (!_IsValid(index)) throw new IndexOutOfRangeException();
		if (!_hasHwnd || !IsVisible) { _ensureVisible = (index + 1, scrollTop); return; }
		bool retry = false;
		g1:
		_ensureVisible = default;
		var r = GetRectPhysical(index);
		if (scrollTop) {
			if (r.top != 0) _vscroll.Pos = index;
		} else if (r.top < 0 || r.bottom > _height) {
			int max = _vscroll.Max;
			_vscroll.Pos = (r.top < 0 || _height < _itemHeight) ? index : index - _height / _itemHeight + 1;
			if (!retry) if (retry = _vscroll.Max > max) goto g1; //added horz scrollbar and maybe it covers the item
		}
	}
	
	/// <summary>
	/// Expands descendant folders and scrolls if need to make item actually visible.
	/// </summary>
	public void EnsureVisible(ITreeViewItem item, bool scrollTop = false) {
		int i = IndexOf(item);
		if (i < 0) { //expand ancestor folders
			if (!_Find(_itemsSource, item)) throw new ArgumentException();
			_SetVisibleItems(init: false);
			i = IndexOf(item);
			static bool _Find(IEnumerable<ITreeViewItem> a, ITreeViewItem item) {
				if (a != null)
					foreach (var v in a) {
						if ((object)v == item) return true;
						if (v.IsFolder && _Find(v.Items, item)) {
							v.SetIsExpanded(true);
							return true;
						}
					}
				return false;
			}
		}
		EnsureVisible(i, scrollTop);
	}
	
	#endregion
	
	#region mouse/keyboard input and related events
	
	//Returns false if not on item.
	void _OnMouseDown(MouseButton button, nint wParam, nint lParam, bool @double = false) {
		if (button != MouseButton.Middle && Focusable) Focus();
		var xy = Math2.NintToPOINT(lParam);
		if (!HitTest(xy, out var h) || !Api.GetCapture().Is0) return;
		
		if (button == MouseButton.Left && h.part == TVParts.Image && h.item.IsFolder) {
			Expand(h.index, null);
		} else {
			var mk = (0 != (wParam & Api.MK_CONTROL) ? ModifierKeys.Control : 0) | (0 != (wParam & Api.MK_SHIFT) ? ModifierKeys.Shift : 0); //never mind Alt, it's never used and may activate menubar
			bool multiSelect = MultiSelect && button == MouseButton.Left && !@double && (mk is ModifierKeys.Control or ModifierKeys.Shift);
			bool checkbox = h.part == TVParts.Checkbox && button == MouseButton.Left && !multiSelect;
			bool unselectOnUp = false;
			if (button == MouseButton.Left && !multiSelect && !checkbox) {
				unselectOnUp = MultiSelect && IsSelected(h.index); //unselect other on up, else could not drag multiple
				Select(h.index, true, unselectOther: !unselectOnUp, focus: true);
			}
			bool clickEvent = false, activateEvent = false;
			if (checkbox) {
				clickEvent = true;
			} else if (!@double || button == MouseButton.Middle) {
				Api.SetCapture(_w);
				_mouse = (true, button, h, xy, mk, multiSelect, unselectOnUp);
			} else if (button == MouseButton.Left) {
				//print.it("double", h.item);
				if (!(FullRowExpand && !MultiSelect) && h.part != TVParts.Image && h.item.IsFolder) Expand(h.index, null);
				clickEvent = true;
				activateEvent = !SingleClickActivate;
			}
			if (clickEvent || activateEvent) _MouseEvents(clickEvent, activateEvent, false, button, h, xy, mk, @double);
		}
	}
	(bool active, MouseButton button, TVHitTest h, POINT xy, ModifierKeys mk, bool multiSelect, bool unselect) _mouse;
	
	void _MouseEvents(bool click, bool activate, bool drag, MouseButton button, in TVHitTest h, POINT xy, ModifierKeys mk, bool @double = false) {
		var v = new TVItemEventArgs(h.item, h.index, h.part, button, @double ? 2 : 1, xy, mk);
		if (click) ItemClick?.Invoke(v);
		if (activate && !h.item.IsDisabled) ItemActivated?.Invoke(v);
		if (drag) ItemDragStart?.Invoke(v);
	}
	
	void _MouseEnd() {
		if (_mouse.active) {
			_mouse.active = false;
			Api.ReleaseCapture();
		}
	}
	
	bool _OnMouseUp(MouseButton button) {
		if (_mouse.active && button == _mouse.button) {
			_MouseEnd();
			if (_mouse.multiSelect) { //extend selection on up. If on down, interferes with drag.
				int i = _mouse.h.index;
				if (_mouse.mk == ModifierKeys.Shift) _ShiftSelect(i); else Select(i, !IsSelected(i), unselectOther: false);
				_focusedIndex = i;
			} else {
				if (_mouse.unselect) Select(_mouse.h.index, true, unselectOther: true);
				if (FullRowExpand && !MultiSelect && button == MouseButton.Left && _mouse.mk == 0 && _mouse.h.item.IsFolder) {
					Expand(_mouse.h.index, null);
				}
				bool activateEvent = SingleClickActivate && _mouse.button == MouseButton.Left;
				_MouseEvents(true, activateEvent, false, _mouse.button, _mouse.h, _mouse.xy, _mouse.mk);
			}
			return true;
		}
		return false;
	}
	
	void _OnMouseMove(nint wParam) {
		if (_mouse.active) {
			if (0 != (wParam & (_mouse.button switch { MouseButton.Left => Api.MK_LBUTTON, MouseButton.Right => Api.MK_RBUTTON, _ => Api.MK_MBUTTON }))) {
				//print.it("move");
				if (Math2.Distance(_w.MouseClientXY, _mouse.xy) > _itemLineHeight / 4) {
					//print.it("drag");
					int i = _mouse.h.index;
					if (!IsSelected(i)) SelectSingle(i, andFocus: true);
					_MouseEnd();
					_MouseEvents(false, false, true, _mouse.button, _mouse.h, _mouse.xy, _mouse.mk);
				}
			} else {
				_MouseEnd();
			}
		}
		_OnMouseMoveOrScroll(false);
	}
	
	void _OnMouseMoveOrScroll(bool wheel) {
		int i = _ItemFromY(_w.MouseClientXY.y);
		if (i != _hotIndex) {
			if (HotTrack) {
				if (_hotIndex >= 0) _Invalidate(_hotIndex);
				if (i >= 0) _Invalidate(i);
			}
			
			if (_hotIndex < 0 != i < 0) Api.TrackMouseLeave(_w, i >= 0);
			_hotIndex = i;
			
			if (ShowLabelTip) (_labeltip ??= new(this)).HotChanged(i);
			
			(_tooltip ??= new(this)).HotChanged(i);
		}
	}
	
	void _OnMouseLeave() {
		if (_hotIndex >= 0) {
			if (HotTrack) _Invalidate(_hotIndex);
			_hotIndex = -1;
		}
		_labeltip?.Hide();
		_tooltip?.Hide();
	}
	
	/// <summary>
	/// Whether to higlight an item when mouse is over.
	/// </summary>
	public bool HotTrack { get; set; }
	
	/// <summary>
	/// Whether to show a tooltip with full item text when mouse is over an item with partially visible text. Default true.
	/// </summary>
	public bool ShowLabelTip { get; set; } = true;
	
	/// <summary>
	/// Whether to activate an item on single click instead of double click.
	/// </summary>
	/// <seealso cref="ItemActivated"/>
	public bool SingleClickActivate { get; set; }
	
	/// <summary>
	/// To expand/collapse folders can click not only icon.
	/// Ignored if <see cref="MultiSelect"/>.
	/// </summary>
	public bool FullRowExpand { get; set; }
	
	///
	protected override void OnKeyDown(KeyEventArgs e) {
		//print.it(e.Key, _focusedIndex);
		if (EditingLabel) {
			var k = e.Key;
			switch (k) {
			case Key.Enter: EndEditLabel(); e.Handled = true; break;
			case Key.Escape: EndEditLabel(true); e.Handled = true; break;
			}
		} else {
			base.OnKeyDown(e);
			if (!e.Handled) e.Handled = ProcessKey(e.Key);
		}
	}
	
	/// <summary>
	/// Processes keys such as arrow, page, Enter, Ctrl+A.
	/// Returns true if handled.
	/// </summary>
	/// <seealso cref="keys.more.KKeyToWpf"/>
	public bool ProcessKey(Key k) {
		bool handled = false;
		if (!_avi.NE_()) {
			var mod = Keyboard.Modifiers;
			bool isFocus = _IsValid(_focusedIndex);
			int selIndex = -1, selAdd = 0;
			switch (k) {
			case Key.Enter:
				handled = true;
				if (isFocus) {
					var v = _IndexToItem(_focusedIndex);
					if (!v.IsDisabled) ItemActivated?.Invoke(new(v, _focusedIndex, Mod: mod));
				}
				break;
			case Key.Home or Key.End or Key.Down or Key.Up or Key.PageDown or Key.PageUp:
				selIndex = _vscroll.KeyNavigate(_focusedIndex, keys.more.KKeyFromWpf(k));
				break;
			case Key.Left:
			case Key.Right:
				if (mod != 0) break;
				handled = true;
				if (isFocus) {
					var v = _avi[_focusedIndex];
					if (v.item.IsFolder) {
						if ((k == Key.Right && !v.item.IsExpanded) || (k == Key.Left && v.item.IsExpanded)) {
							Expand(_focusedIndex, k == Key.Right);
							break;
						}
						if (k == Key.Right) { selAdd = 1; break; } //folder -> child
					}
					if (k == Key.Left && v.level > 0) { //child -> folder
						for (int i = _focusedIndex; --i >= 0; selAdd--) if (_avi[i].level < v.level) break;
						selAdd--;
					}
				}
				break;
			case Key.A when mod == ModifierKeys.Control && MultiSelect:
				handled = true;
				Select(.., true);
				break;
			}
			
			if (selAdd != 0) selIndex = Math.Clamp((isFocus ? _focusedIndex : (selAdd > 0 ? -1 : _avi.Length)) + selAdd, 0, _avi.Length - 1);
			if (selIndex >= 0 && (mod == 0 || (mod == ModifierKeys.Shift && MultiSelect))) {
				handled = true;
				if (selIndex != _focusedIndex) {
					if (mod == 0) SelectSingle(selIndex, andFocus: false); else _ShiftSelect(selIndex);
					SetFocusedItem(selIndex);
				}
			}
		}
		return handled;
	}
	
	/// <summary>
	/// When an item double-clicked. Or clicked, if <see cref="SingleClickActivate"/>. Also on Enter key if focused.
	/// Not sent if disabled.
	/// </summary>
	public event Action<TVItemEventArgs> ItemActivated;
	
	/// <summary>
	/// When an item clicked or double-clicked with the left, right or middle mouse button.
	/// </summary>
	public event Action<TVItemEventArgs> ItemClick;
	
	/// <summary>
	/// When drag start detected.
	/// </summary>
	public event Action<TVItemEventArgs> ItemDragStart;
	
	/// <summary>
	/// Right-click non on an item.
	/// </summary>
	public event Action RightClickInEmptySpace;
	
	#endregion
	
	#region selection, focus, checkboxes
	
	/// <summary>
	/// Whether can select multiple items, for example with Ctrl or Shift key.
	/// </summary>
	public bool MultiSelect { get; set; } //never mind: if set false, unselect all except focused
	
	/// <summary>
	/// Selects or unselects item.
	/// </summary>
	/// <param name="index"></param>
	/// <param name="select">true to select, false to unselect.</param>
	/// <param name="unselectOther">Unselect other items. Used only if <see cref="MultiSelect"/> true, else always unselects other items.</param>
	/// <param name="focus">Make the item focused and ensure visible.</param>
	/// <param name="scrollTop">Scroll if need so that the item would be at the top of really visible range.</param>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public void Select(int index, bool select = true, bool unselectOther = false, bool focus = false, bool scrollTop = false) {
		if (!_IsValid(index)) throw new IndexOutOfRangeException();
		
		if (focus) SetFocusedItem(index, scrollTop ? TVFocus.EnsureVisible | TVFocus.ScrollTop : TVFocus.EnsureVisible);
		else if (scrollTop) _vscroll.Pos = index;
		
		Select(index..(index + 1), select, unselectOther);
		if (select && unselectOther) SelectedSingle?.Invoke(this, index);
	}
	
	/// <summary>
	/// Selects or unselects item.
	/// </summary>
	/// <param name="item"></param>
	/// <param name="select">true to select, false to unselect.</param>
	/// <param name="unselectOther">Unselect other items. Used only if <see cref="MultiSelect"/> true, else always unselects other items.</param>
	/// <param name="focus">Make the item focused and ensure visible.</param>
	/// <param name="scrollTop">Scroll if need so that the item would be at the top of really visible range.</param>
	/// <exception cref="ArgumentException"><i>item</i> is not a visible item in this control. No exception if <i>select</i> false.</exception>
	public void Select(ITreeViewItem item, bool select = true, bool unselectOther = false, bool focus = false, bool scrollTop = false) {
		if (!_IndexOfOrThrowIfImportant(select, item, out int i)) return; //ok if tries to unselect an already unselected item in a collapsed folder
		Select(i, select, unselectOther, focus, scrollTop);
	}
	
	/// <summary>
	/// Selects or unselects range of items.
	/// </summary>
	/// <param name="range">Range of item indices. For example <c>..</c> means all.</param>
	/// <param name="select">true to select, false to unselect.</param>
	/// <param name="unselectOther">Unselect other items. Used only if <see cref="MultiSelect"/> true, else always unselects other items.</param>
	public void Select(Range range, bool select = true, bool unselectOther = false) {
		var (from, to) = range.GetStartEnd(_avi.Lenn_());
		if (select && !MultiSelect) {
			if (to - from > 1) throw new InvalidOperationException();
			unselectOther = true;
		}
		bool invalidate = false;
		if (select && unselectOther) {
			for (int k = 0; k < 2; k++) {
				int i, j; if (k == 0) { i = 0; j = from; } else { i = to; j = _avi.Length; }
				for (; i < j; i++) if (_avi[i].isSelected) { _avi[i].isSelected = false; invalidate = true; }
			}
		}
		for (; from < to; from++) if (select != IsSelected(from)) invalidate |= _avi[from].Select(select);
		if (invalidate) _Invalidate();
		
		SelectionChanged?.Invoke(this, EventArgs.Empty);
	}
	
	/// <summary>
	/// Unselects all.
	/// </summary>
	public void UnselectAll() => Select(.., select: false);
	
	public event EventHandler SelectionChanged;
	public event EventHandler<int> SelectedSingle;
	
	/// <summary>
	/// Selects item, unselects others, optionally makes the focused.
	/// </summary>
	/// <param name="andFocus">Make the item focused and ensure visible.</param>
	/// <param name="scrollTop">Scroll if need so that the item would be at the top of really visible range.</param>
	public void SelectSingle(int index, bool andFocus, bool scrollTop = false) => Select(index, true, true, andFocus, scrollTop);
	
	/// <summary>
	/// Selects item, unselects others, optionally makes the focused.
	/// </summary>
	/// <param name="andFocus">Make the item focused and ensure visible.</param>
	/// <param name="scrollTop">Scroll if need so that the item would be at the top of really visible range.</param>
	public void SelectSingle(ITreeViewItem item, bool andFocus, bool scrollTop = false) => Select(item, true, true, andFocus, scrollTop);
	
	void _ShiftSelect(int last) {
		int from; if (last >= _focusedIndex) from = Math.Max(_focusedIndex, 0); else { from = last; last = _focusedIndex; }
		Select(from..++last, true, unselectOther: false);
	}
	
	/// <summary>
	/// Returns true if the item is selected.
	/// </summary>
	/// <seealso cref="MultiSelect"/>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public bool IsSelected(int index) => _avi[index].isSelected;
	
	/// <summary>
	/// Returns true if the item is visible and selected.
	/// </summary>
	/// <seealso cref="MultiSelect"/>
	public bool IsSelected(ITreeViewItem item) {
		int i = IndexOf(item); //if not found, probably it is an unselected item in a collapsed folder
		return i >= 0 && _avi[i].isSelected;
	}
	
	/// <summary>
	/// Gets selected items. Returns empty list if none selected.
	/// </summary>
	/// <seealso cref="MultiSelect"/>
	public List<int> SelectedIndices {
		get {
			var a = new List<int>();
			for (int i = 0; i < _avi.Length; i++) if (_avi[i].isSelected) a.Add(i);
			return a;
		}
	}
	
	/// <summary>
	/// Gets selected items. Returns empty list if none selected.
	/// </summary>
	/// <seealso cref="MultiSelect"/>
	public List<ITreeViewItem> SelectedItems {
		get {
			var a = new List<ITreeViewItem>();
			for (int i = 0; i < _avi.Length; i++) if (_avi[i].isSelected) a.Add(_avi[i].item);
			return a;
		}
	}
	
	/// <summary>
	/// Returns index of first selected item, or -1 if no selection.
	/// </summary>
	public int SelectedIndex {
		get {
			for (int i = 0; i < _avi.Length; i++) if (_avi[i].isSelected) return i;
			return -1;
		}
	}
	
	/// <summary>
	/// Returns first selected item, or null if no selection.
	/// </summary>
	public ITreeViewItem SelectedItem {
		get {
			int i = SelectedIndex; return i >= 0 ? _avi[i].item : null;
		}
	}
	
	/// <summary>
	/// Gets index of item that has logical focus within the control. Can be -1.
	/// </summary>
	public int FocusedIndex => _focusedIndex;
	
	/// <summary>
	/// Gets item that has logical focus within the control. Can be null.
	/// </summary>
	public ITreeViewItem FocusedItem => _IndexToItem(_focusedIndex);
	
	/// <summary>
	/// Sets item that has logical focus within the control.
	/// </summary>
	/// <param name="index">Can be -1.</param>
	/// <param name="flags"></param>
	/// <exception cref="IndexOutOfRangeException"></exception>
	/// <remarks>
	/// Used with keyboard actions (Enter-activate, arrows, page down/up), range selection (Shift+click when <see cref="MultiSelect"/> true), optionally <see cref="Select"/>.
	/// </remarks>
	public void SetFocusedItem(int index, TVFocus flags = TVFocus.EnsureVisible) {
		if (!_IsValid(index) && index != -1) throw new IndexOutOfRangeException();
		_focusedIndex = index;
		if (flags.HasAny(TVFocus.EnsureVisible | TVFocus.ScrollTop)) if (index >= 0) EnsureVisible(index, flags.Has(TVFocus.ScrollTop));
	}
	
	/// <summary>
	/// Sets item that has logical focus within the control.
	/// </summary>
	/// <param name="item">Can be null.</param>
	/// <param name="flags"></param>
	/// <exception cref="ArgumentException">The item is not a visible item in this control.</exception>
	public void SetFocusedItem(ITreeViewItem item, TVFocus flags = TVFocus.EnsureVisible)
		=> SetFocusedItem(_IndexOfOrThrow(item, canBeNull: true), flags);
	
	///
	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		_Invalidate();
		base.OnGotKeyboardFocus(e);
	}
	
	///
	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) {
		_MouseEnd();
		_Invalidate();
		base.OnLostKeyboardFocus(e);
	}
	
	/// <summary>
	/// Has checkboxes.
	/// </summary>
	/// <remarks>
	/// To get checkbox states the control calls <see cref="ITreeViewItem.CheckState"/>.
	/// The control does not automatically set checkbox states. You can do it in <see cref="ItemClick"/> event handler like in the example.
	/// </remarks>
	/// <example>
	/// Event handler.
	/// <code><![CDATA[
	/// _tv.ItemClick+=(_,e)=>{
	/// 	var v=e.Item as TvItem; //TvItem is your class that implements ITreeViewItem interface
	/// 	if(e.ClickedPart==KTreeView.TVParts.Checkbox && !v.IsDisabled) v.CheckState=v.CheckState==default ? KTreeView.TVCheck.Checked : default;
	/// };
	/// ]]></code>
	/// Property of your TvItem class that implements ITreeViewItem interface.
	/// <code><![CDATA[
	/// KTreeView.TVCheck CheckState {
	/// 	get => _checkState;
	/// 	set { if(value!=_checkState) { _checkState=value; _tv.Redraw(this); } }
	/// }
	/// KTreeView.TVCheck _checkState;
	/// ]]></code>
	/// </example>
	public bool HasCheckboxes {
		get => _hasCheckboxes;
		set {
			if (value != _hasCheckboxes) {
				_hasCheckboxes = value;
				_MeasureClear(true);
			}
		}
	}
	bool _hasCheckboxes;
	
	/// <summary>
	/// Gets checked items (<see cref="TVCheck.Checked"/>). Returns empty list if none checked.
	/// </summary>
	public List<int> CheckedIndices {
		get {
			var a = new List<int>();
			for (int i = 0; i < _avi.Length; i++) if (_avi[i].item.CheckState == TVCheck.Checked) a.Add(i);
			return a;
		}
	}
	
	/// <summary>
	/// Gets checked items (<see cref="TVCheck.Checked"/>). Returns empty list if none checked.
	/// </summary>
	public List<ITreeViewItem> CheckedItems {
		get {
			var a = new List<ITreeViewItem>();
			for (int i = 0; i < _avi.Length; i++) if (_avi[i].item.CheckState == TVCheck.Checked) a.Add(_avi[i].item);
			return a;
		}
	}
	
	//rejected. Then also need setter, but CheckState is read-only and I don't want to make it read-write.
	//	/// <summary>
	//	/// Gets checked items (<see cref="TVCheck.Checked"/>) as flags.
	//	/// Skips items that don't have state <b>Checked</b> or <b>Unchecked</b>.
	//	/// </summary>
	//	public ulong CheckedBits {
	//		get {
	//			ulong flags=0; ulong j=1;
	//			for (int i = 0; i < _avi.Length; i++) {
	//				switch (_avi[i].item.CheckState) {
	//				case TVCheck.Checked: flags|=j; break;
	//				case TVCheck.Unchecked: break;
	//				default: continue;
	//				}
	//				if(j<0x8000000000000000) j<<=1; else break;
	//			}
	//			return flags;
	//		}
	//	}
	
	#endregion
	
	#region edit label
	
	/// <summary>
	/// Starts focused item text editing.
	/// The type must implement <see cref="ITreeViewItem.SetNewText"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">Control not created.</exception>
	public void EditLabel(Action<bool> ended = null) { int i = _focusedIndex; if (i >= 0) EditLabel(i, ended); }
	
	/// <summary>
	/// Starts item text editing.
	/// The type must implement <see cref="ITreeViewItem.SetNewText"/>.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="InvalidOperationException">Control not created.</exception>
	public void EditLabel(ITreeViewItem item, Action<bool> ended = null) => _EditLabel(item, _IndexOfOrThrow(item), ended);
	
	/// <summary>
	/// Starts item text editing.
	/// The type must implement <see cref="ITreeViewItem.SetNewText"/>.
	/// </summary>
	/// <exception cref="IndexOutOfRangeException"></exception>
	/// <exception cref="InvalidOperationException">Control not created.</exception>
	public void EditLabel(int index, Action<bool> ended = null) {
		if (!_IndexToItem(index, out var item)) throw new IndexOutOfRangeException();
		_EditLabel(item, index, ended);
	}
	
	void _EditLabel(ITreeViewItem item, int index, Action<bool> ended) {
		EndEditLabel();
		EnsureVisible(index);
		if (!IsFocused) Focus();
		
		var r = GetRectPhysical(index, TVParts.Text | TVParts.MarginRight | TVParts.Right);
		r.left -= _imageMarginX;
		r.Intersect(_w.ClientRect);
		if (r.Width < 8 || r.Height < 8) return;
		
		_leItem = item;
		_leEnded = ended;
		
		//add border. Cannot add WS_BORDER to _leEdit because it also adds padding and therefore must be taller than we need, else there is no top border.
		_leBorder = WndUtil.CreateWindow((w, msg, wp, lp) => {
			if (msg == Api.WM_COMMAND && Math2.HiWord(wp) == /*EN_KILLFOCUS*/ 0x0200) EndEditLabel();
			return Api.DefWindowProc(w, msg, wp, lp);
		}, true, "Static", null, WS.CHILD | WS.VISIBLE | WS.BORDER | (WS)6 /*SS_WHITERECT*/, 0, r.left, r.top, r.Width, r.Height, _w);
		
		_leEdit = WndUtil.CreateWindow("Edit", item.DisplayText, WS.CHILD | WS.VISIBLE | Api.ES_AUTOHSCROLL, 0, 0, 0, r.Width - 2, r.Height - 2, _leBorder);
		WndUtil.SetFont(_leEdit);
		EditLabelStartedEventArgs e = new(this, item);
		e.SelectText();
		
		Api.SetFocus(_leEdit);
		
		EditLabelStarted?.Invoke(e);
	}
	
	wnd _leEdit, _leBorder;
	ITreeViewItem _leItem;
	Action<bool> _leEnded;
	
	/// <summary>
	/// When item text editing started.
	/// </summary>
	public event Action<EditLabelStartedEventArgs> EditLabelStarted;
	
	public record class EditLabelStartedEventArgs(KTreeView tv, ITreeViewItem item) {
		public string Text {
			get => item.DisplayText;
			set { tv._leEdit.SetText(value); }
		}
		
		/// <summary>
		/// Selects text. To select all, use 0 -1.
		/// </summary>
		public void SelectText(int start = 0, int end = -1) {
			tv._leEdit.Send(/*EM_SETSEL*/ 0x00B1, start, end);
		}
	}
	
	public bool EditingLabel => !_leEdit.Is0;
	
	/// <summary>
	/// Ends item text editing.
	/// </summary>
	public void EndEditLabel(bool cancel = false) {
		if (!EditingLabel) return;
		int index = cancel ? -1 : IndexOf(_leItem);
		if (!cancel) cancel = index < 0 || !IsVisible;
		//print.it("EndEditLabel, cancel=", cancel);
		bool focus = Api.GetFocus() == _leEdit;
		var text = cancel ? null : _leEdit.ControlText;
		var item = _leItem; _leItem = null;
		var ended = _leEnded; _leEnded = null;
		_leEdit = default;
		Api.DestroyWindow(_leBorder); _leBorder = default;
		if (focus) Focus();
		if (!cancel && text != item.DisplayText) {
			int meas = _avi[index].measured;
			item.SetNewText(text);
			if (_avi[index].measured == meas) Redraw(index, remeasure: true); //SetNewText (app) should do it, but may forget
		}
		ended?.Invoke(!cancel);
	}
	
	#endregion
	
	#region drag & drop
	
	///
	protected override void OnDragLeave(DragEventArgs e) {
		base.OnDragLeave(e);
		_dd?.ClearInsertMark();
		_dd = null;
	}
	
	///
	protected override void OnDrop(DragEventArgs e) {
		base.OnDrop(e);
		_dd?.ClearInsertMark();
		_dd = null;
	}
	
	/// <summary>
	/// Can be called from "drag over" override or event handler to show/hide insertion mark, expand/collapse folder and scroll if need.
	/// </summary>
	/// <param name="canDrop">Can drop here. If false, hides insertion mark and returns.</param>
	/// <remarks>
	/// Draws black line between nearest items. If mouse is on a folder vertical center, draws rectangle.
	/// When mouse is on a folder, expands it if pressed key Right and collapses if Left.
	/// Scrolls when mouse is near top or bottom or pressed key Down, Up, PageDown, PageUp, Home or End.
	/// </remarks>
	/// <seealso cref="GetDropInfo"/>
	public void OnDragOver2(bool canDrop) {
		if (!canDrop || _avi.NE_()) { _dd?.ClearInsertMark(); return; }
		_dd ??= new _DragDrop(this);
		var p = _w.MouseClientXY;
		GetDropInfo(p, out var d);
		bool noMark = false;
		Key key;
		//expand/collapse folder
		if (Keyboard.IsKeyDown(key = Key.Right) || Keyboard.IsKeyDown(key = Key.Left)) {
			if (d.targetItem?.IsFolder ?? false) Expand(d.targetIndex, key == Key.Right);
			return;
		}
		//scroll. When not moving mouse, this is called every 64 ms.
		if (_vscroll.Visible) {
			int scroll = 0, dist = _imageSize * 3 / 2;
			if (Keyboard.IsKeyDown(key = Key.Down)) scroll = 3;
			else if (Keyboard.IsKeyDown(key = Key.Up)) scroll = -3;
			else if (Keyboard.IsKeyDown(key = Key.PageDown)) scroll = _height / _itemHeight;
			else if (Keyboard.IsKeyDown(key = Key.PageUp)) scroll = -_height / _itemHeight;
			else if (Keyboard.IsKeyDown(Key.Home)) noMark = _Scroll(0, true);
			else if (Keyboard.IsKeyDown(Key.End)) noMark = _Scroll(_vscroll.Max, true);
			else if (_height > dist * 3) {
				key = 0;
				if (p.y < dist) scroll--; else if (p.y > _height - dist) scroll++;
			}
			if (scroll == 0) {
				_dd.scrolling = false;
				_dd.scrollDelayTime = 0;
				_dd.scrollTime = 0;
			} else if (_dd.scrolling || key != 0) {
				long time = Environment.TickCount64;
				if (time - _dd.scrollTime > 110) {
					_dd.scrollTime = time;
					noMark = _Scroll(scroll, false);
				} else noMark = true;
			} else if (_dd.scrollDelayTime == 0 || p != _dd.scrollDelayPoint) {
				_dd.scrollDelayTime = Environment.TickCount64;
				_dd.scrollDelayPoint = p;
			} else if (Environment.TickCount64 - _dd.scrollDelayTime > 400) {
				_dd.scrolling = true;
			}
		}
		//insertion mark
		if (!noMark) _dd.SetInsertMark(d.targetIndex, d.insertAfter, d.intoFolder);
		
		bool _Scroll(int scroll, bool absolute) {
			if (!absolute) scroll += _vscroll.Pos;
			int i = Math.Clamp(scroll, 0, _vscroll.Max);
			if (i == _vscroll.Pos) return false;
			_dd.ClearInsertMark();
			_vscroll.Pos = i;
			return true;
		}
	}
	
	//fields for drag scrolling and insertion mark
	class _DragDrop {
		KTreeView _tv;
		public bool scrolling;
		public long scrollDelayTime, scrollTime;
		public POINT scrollDelayPoint;
		public int insertIndex;
		public bool insertAfter, insertFolder;
		
		public _DragDrop(KTreeView tv) {
			_tv = tv;
			insertIndex = -1;
		}
		
		public void SetInsertMark(int i, bool after, bool folder) {
			if (i == insertIndex && after == insertAfter && folder == insertFolder) return;
			int pi = insertIndex;
			insertIndex = i; insertAfter = after; insertFolder = folder;
			if (pi >= 0) _tv._Invalidate(pi);
			if (i >= 0) _tv._Invalidate(i);
		}
		
		public void ClearInsertMark() => SetInsertMark(-1, false, false);
	}
	_DragDrop _dd;
	
	/// <summary>
	/// Can be called from "drag over" and "drop" overrides or event handlers to get drop info.
	/// This overload uses mouse position (<see cref="wnd.MouseClientXY"/>).
	/// </summary>
	/// <param name="d"></param>
	/// <seealso cref="OnDragOver2"/>
	public void GetDropInfo(out TVDropInfo d) => GetDropInfo(_w.MouseClientXY, out d);
	
	/// <summary>
	/// Can be called from "drag over" and "drop" overrides or event handlers to get drop info.
	/// </summary>
	/// <param name="xy">A point relative to the top-left of the control without border. Physical pixels.</param>
	/// <param name="d"></param>
	/// <seealso cref="OnDragOver2"/>
	public void GetDropInfo(POINT xy, out TVDropInfo d) {
		d = default;
		d.xy = xy;
		if (_avi.NE_()) {
			d.targetIndex = -1;
			return;
		}
		if (HitTest(d.xy, out var h)) {
			d.targetIndex = h.index;
			double y = d.xy.y, top = _ItemTop(h.index), quarter = _itemHeight / 4d;
			bool after = y >= top + quarter * 2;
			if (h.item.IsFolder && y >= top + quarter) {
				if (y < top + quarter * 3) d.intoFolder = true;
				else if (h.index < _avi.Length - 1 && _avi[h.index + 1].level > _avi[h.index].level) d.targetIndex++; //between folder and its first child
				else d.insertAfter = after;
			} else d.insertAfter = after;
			
			d.targetItem = _avi[d.targetIndex].item;
		} else {
			d.targetIndex = -1;
		}
	}
	
	#endregion
}
