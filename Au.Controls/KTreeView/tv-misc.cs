using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Au.Controls
{
	public unsafe partial class KTreeView
	{
		bool _IsValid(int index) => (uint)index < _avi.Lenn_();

		//unused
		//bool _IndexToItemStruct(int i, out _VisibleItem item) {
		//	bool r = _IsValid(i);
		//	item = r ? _avi[i] : default;
		//	return r;
		//}

		bool _IndexToItem(int i, out ITreeViewItem item) {
			bool r = _IsValid(i);
			item = r ? _avi[i].item : null;
			return r;
		}

		ITreeViewItem _IndexToItem(int i) => _IsValid(i) ? _avi[i].item : null;

		int _IndexOfOrThrow(ITreeViewItem item, bool canBeNull = false) {
			int i = -1;
			if (item != null) { i = IndexOf(item); if (i < 0) throw new ArgumentException(); } else if (!canBeNull) throw new ArgumentNullException();
			return i;
		}

		bool _IndexOfOrThrowIfImportant(bool important, ITreeViewItem item, out int index) {
			index = IndexOf(item);
			if (index < 0) return !important ? false : throw new ArgumentException();
			return true;
		}

		bool _IsInside(int iParent, int iChild) {
			int i = iParent;
			if (iChild > i) {
				var level = _avi[i].level;
				while (++i < _avi.Length && _avi[i].level > level) if (i == iChild) return true;
			}
			//never mind: faster would be to use Parent. Would need to add Parent to the interface. Currently don't need speed.
			return false;
		}

		//unused
		//bool _IsInside(ITreeViewItem parent, int iChild) {
		//	int i = IndexOf(parent);
		//	return i >= 0 && _IsInside(i, iChild);
		//}

		int _DpiScale(int value) => More.Dpi.Scale(value, _dpi);

		int _ItemTop(int index) => (index - _vscroll.Pos) * _itemHeight;

		/// <summary>
		/// Returns item index, or -1 if not on item.
		/// </summary>
		/// <param name="y">In control coord, physical.</param>
		int _ItemFromY(int y) {
			int i = y / _itemHeight + _vscroll.Pos;
			if (!_IsValid(i)) i = -1;
			return i;
		}

		struct _PartOffsets
		{
			public int left, checkbox, marginLeft, image, text, marginRight, right;
		}

		void _GetPartOffsets(int i, out _PartOffsets p) {
			p.left = -_hscroll.Offset;
			p.checkbox = p.left + _avi[i].level * _imageSize;
			p.marginLeft = p.checkbox; if (HasCheckboxes) p.marginLeft += _itemHeight;
			p.image = p.marginLeft + _marginLeft;
			p.text = p.image + _imageSize + _imageMarginX * 2;
			p.marginRight = p.text + _avi[i].measured;
			p.right = p.marginRight + _marginRight;
			//print.it(p.checkbox, p.marginLeft, p.image, p.text, p.marginRight, p.right);
		}

		/// <summary>
		/// Gets item rectangle in physical pixel units.
		/// Horizontally the rectangle is limited to the visible area.
		/// </summary>
		/// <exception cref="IndexOutOfRangeException"></exception>
		/// <exception cref="InvalidOperationException">Control not created.</exception>
		public RECT GetRectPhysical(int index, TVParts parts = 0, bool inScreen = false, bool clampX = false) {
			if (!_IsValid(index)) throw new IndexOutOfRangeException();
			if (!_hasHwnd) throw new InvalidOperationException();
			int y = _ItemTop(index);
			var r = new RECT(0, y, _width, _itemHeight);
			if (parts != 0) {
				_GetPartOffsets(index, out var k);
				//left
				if (parts.Has(TVParts.Left)) r.left = k.left;
				else if (parts.Has(TVParts.Checkbox)) r.left = k.checkbox;
				else if (parts.Has(TVParts.MarginLeft)) r.left = k.marginLeft;
				else if (parts.Has(TVParts.Image)) r.left = k.image;
				else if (parts.Has(TVParts.Text)) r.left = k.text;
				else if (parts.Has(TVParts.MarginRight)) r.left = k.marginRight;
				else r.left = k.right;
				//right
				if (parts.Has(TVParts.Right)) r.right = Math.Max(k.right, _width);
				else if (parts.Has(TVParts.MarginRight)) r.right = k.right;
				else if (parts.Has(TVParts.Text)) r.right = k.marginRight;
				else if (parts.Has(TVParts.Image)) r.right = k.text;
				else if (parts.Has(TVParts.MarginLeft)) r.right = k.image;
				else if (parts.Has(TVParts.Checkbox)) r.right = k.marginLeft;
				else r.right = k.checkbox;
				//clamp
				if (clampX) {
					r.left = Math.Clamp(r.left, 0, _width);
					r.right = Math.Clamp(r.right, 0, _width);
				}
			} else if (!clampX) {
				r.left = -_hscroll.Offset;
				r.right = Math.Max(_width, _itemsWidth - r.left);
			}
			if (inScreen) _w.MapClientToScreen(ref r);
			return r;
		}

		/// <summary>
		/// Gets item rectangle in WPF logical pixel units.
		/// </summary>
		/// <exception cref="IndexOutOfRangeException"></exception>
		public Rect GetRectLogical(int index, TVParts parts = 0, bool inScreen = false, bool clampX = false) {
			var r = GetRectPhysical(index, parts, inScreen, clampX);
			double f = 96d / _dpi;
			return new Rect(r.left * f, r.top * f, r.Width * f, r.Height * f);
		}

		/// <summary>
		/// Gets item from point, and its part.
		/// Returns false if not on an item.
		/// </summary>
		/// <param name="p">Point in control coordinates. Physical pixels.</param>
		/// <param name="h">Results.</param>
		public bool HitTest(POINT p, out TVHitTest h) {
			h = default;
			if ((uint)p.x < _width && (uint)p.y < _height) {
				int i = _ItemFromY(p.y);
				if (_IndexToItem(i, out var v)) {
					h.index = i;
					h.item = v;
					_GetPartOffsets(i, out var k);
					int x = p.x;
					if (x < k.checkbox) h.part = TVParts.Left;
					else if (x < k.marginLeft) h.part = TVParts.Checkbox;
					else if (x < k.image) h.part = TVParts.MarginLeft;
					else if (x < k.text) h.part = TVParts.Image;
					else if (x < k.marginRight) h.part = TVParts.Text;
					else if (x < k.right) h.part = TVParts.MarginRight;
					else h.part = TVParts.Right;
					return true;
				}
			}
			h.index = -1;
			return false;
		}

		/// <summary>
		/// Gets item from mouse, and its part.
		/// Returns false if not on an item.
		/// </summary>
		/// <param name="h">Results.</param>
		public bool HitTest(out TVHitTest h) => HitTest(_w.MouseClientXY, out h);

		_LabelTip _labeltip;

		class _LabelTip
		{
			readonly KTreeView _tv;
			ToolTip _tt;
			timer _timer;
			int _i;
			_VisibleItem[] _avi;

			public _LabelTip(KTreeView tv) { _tv = tv; }

			public void HotChanged(int i) {
				if (i >= 0) {
					var r = _tv.GetRectPhysical(i, TVParts.Text, clampX: true);
					if (_tv._avi[i].measured > r.Width) {
						_timer ??= new timer(_Show);
						_avi = _tv._avi; _i = i;
						_timer.After(300);
						return;
					}
				}
				Hide();
			}

			void _Show(timer t) {
				if (_avi != _tv._avi || _i != _tv._hotIndex) return;
				if (_tt == null) {
					_tt = new ToolTip() {
						PlacementTarget = _tv, //need for correct DPI in non-primary screen
						Placement = PlacementMode.Right,
						HorizontalOffset = 4,
						HasDropShadow = false //why no shadow on Win10?
					};
					if (osVersion.minWin8) _tt.Padding = new Thickness(2, -1, 2, 1); //on Win7 makes too small
				}
				var r = _tv.GetRectLogical(_i, clampX: true);
				r.X -= 4; r.Width += 4;
				_tt.PlacementRectangle = r;
				_tt.Content = _avi[_i].item.DisplayText;
				_tt.IsOpen = true;
			}

			public void Hide() {
				_avi = null;
				_timer?.Stop();
				if (_tt != null && _tt.IsOpen) _tt.IsOpen = false;
			}
		}


	}
}
