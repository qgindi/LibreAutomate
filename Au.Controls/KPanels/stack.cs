using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Au.Controls
{
	public partial class KPanels
	{
		partial class _Node
		{
			/// <summary>
			/// Gets nodes docked and really visible in this stack (not in tab, not floating, not hidden).
			/// </summary>
			List<_Node> _Stack_DockedNodes {
				get {
					var a = new List<_Node>();
					foreach (var v in Children()) if (v._elem.Parent == _stack.grid) a.Add(v);
					return a;
				}
			}

			/// <summary>
			/// true if parent is stack and this is docked and not floating/hidden.
			/// </summary>
			bool _IsDockedInStack => _ParentIsStack && _elem.Parent == Parent._stack.grid;

			_Node _PreviousDockedInStack {
				get {
					for (var v = this; (v = v.Previous) != null;) if (v._IsDockedInStack) return v;
					return null;
				}
			}

			//not used
			//_Node _NextDockedInStack {
			//	get {
			//		for (var v = this; (v = v.Next) != null;) if (v._IsDockedInStack) return v;
			//		return null;
			//	}
			//}

			/// <summary>
			/// Adds elements of this to parent stack. Creates splitter if need, adds row/column, sets element properties (caption etc).
			/// </summary>
			/// <param name="moving">Called when moving this. Inserts, shifts sibling indices, etc. If false, adds as last item in stack.</param>
			/// <param name="splitterSize"></param>
			void _AddToStack(bool moving, int splitterSize) {
				int i = _index * 2;
				if (i > 0) _CreateSplitter(splitterSize);
				var pstack = Parent._stack;
				var g = pstack.grid;
				if (pstack.isVertical) {
					g.InsertRow(i, new RowDefinition { Height = _dockedSize, MinHeight = c_minSize });
					g.AddChild(_elem, i, 0);
				} else {
					g.InsertColumn(i, new ColumnDefinition { Width = _dockedSize, MinWidth = c_minSize });
					g.AddChild(_elem, 0, i);
				}
				if (moving) {
					_ShiftSiblingIndices(1);

					var next = Next;
					if (next != null) {
						if (next._splitter == null) next._CreateSplitter(c_defaultSplitterSize);

						//workaround for: when moving a fixed-size item, 1 pixel of splitter after it may stop working. If splitter size is 1 pixel...
						else if (next._SplitterSize < c_defaultSplitterSize) next._SplitterSize = c_defaultSplitterSize;
					}
				}
				_AddRemoveCaptionAndBorder();
			}

			/// <summary>
			/// Replaces target with this in parent stack.
			/// Used when moving, to create new parent (this) tab or stack for target and the moved node.
			/// Does not add/remove tree nodes.
			/// </summary>
			void _ReplaceInStack(_Node target) {
				if (target._splitter != null) {
					target._SetSplitterEvents(false);
					_splitter = target._splitter; target._splitter = null;
					_SetSplitterEvents(true);
				}

				_dockedSize = target._dockedSize;
				if (_dockedSize.IsAuto) _SizeDef = _dockedSize = new GridLength(100, GridUnitType.Star);

				int i = _index * 2;
				var pstack = Parent._stack;
				var g = pstack.grid;
				if (pstack.isVertical) Grid.SetRow(_elem, i); else Grid.SetColumn(_elem, i);
				g.Children.Remove(target._elem);
				g.Children.Add(_elem);
				_AddRemoveCaptionAndBorder();
				target._AddRemoveCaptionAndBorder();
			}

			/// <summary>
			/// Removes an element of this from parent stack grid. Removes its row/column and shifts sibling indices.
			/// </summary>
			/// <param name="e">_elem or _splitter</param>
			void _RemoveGridRowCol(FrameworkElement e) {
				var pstack = Parent._stack;
				if (pstack.isVertical) pstack.grid.RemoveRow(e, false); else pstack.grid.RemoveColumn(e, false);
			}

			void _ShowHideInStack(bool show) {
				var g = Parent._stack.grid;
				var a = Parent._Stack_DockedNodes;
				if (!show) {
					_dockedSize = _SizeDef;

					g.Children.Remove(_elem);
					if (_splitter != null) _splitter.Visibility = Visibility.Collapsed;
					_SizeMin = 0;
					_SizeDef = default; //Auto

					a.Remove(this);
					if (a.Count == 0) {
						if (Parent._state == _DockState.Float) Parent._Hide();
						else Parent._ShowHideInStack(show);
					} else if (_dockedSize.IsStar && !a.Any(o => o._SizeDef.IsStar)) { //if hiding last star-sized, make the last visible fixed node star-sized
						a.LastOrDefault(o => o._SizeDef.IsAbsolute)?._ChangeSizeUnit(GridUnitType.Star, false);
						//never mind: later should restore. It makes everything complicated. This probably is rare, and it's easy for user to set fixed size again.
					}
				} else {
					if (a.Count == 0) {
						if (Parent._state.Has(_DockState.Float)) Parent._SetDockState(_DockState.Float);
						else Parent._ShowHideInStack(show);
					}

					_SizeDef = _dockedSize;
					_SizeMin = c_minSize;
					g.Children.Add(_elem);
				}
				Parent._Stack_UpdateSplittersVisibility();
			}

			#region splitter

			void _CreateSplitter(int size) {
				bool verticalStack = Parent._stack.isVertical;
				var c = new GridSplitter2();
				if (verticalStack) { //horz splitter
					c.ResizeDirection = GridResizeDirection.Rows;
					c.VerticalAlignment = VerticalAlignment.Top;
					c.HorizontalAlignment = HorizontalAlignment.Stretch;
				} else { //vert splitter
					c.ResizeDirection = GridResizeDirection.Columns;
					c.HorizontalAlignment = HorizontalAlignment.Left;
					//default stretch
				}
				if (_pm.SplitterBrush != null) c.Background = _pm.SplitterBrush;
				_splitter = c;
				_SplitterSize = size;
				var g = Parent._stack.grid;
				int i = _index * 2 - 1;
				if (verticalStack) {
					g.InsertRow(i, new RowDefinition { Height = default }); //Auto
					g.AddChild(c, i, 0);
				} else {
					g.InsertColumn(i, new ColumnDefinition { Width = default });
					g.AddChild(c, 0, i);
				}
				_SetSplitterEvents(true);
			}

			void _SetSplitterEvents(bool add) {
				if (add) {
					_splitter.ContextMenuOpening += _SplitterContextMenu;
					_splitter.PreviewMouseDown += _OnMouseDown;
				} else {
					_splitter.ContextMenuOpening -= _SplitterContextMenu;
					_splitter.PreviewMouseDown -= _OnMouseDown;
				}
			}

			/// <summary>
			/// This must be stack. Hides splitter of first visible child and shows splitters of other visible children.
			/// </summary>
			void _Stack_UpdateSplittersVisibility() {
				var a = _Stack_DockedNodes;
				for (int i = 0; i < a.Count; i++) {
					var v = a[i];
					if (i > 0) v._splitter.Visibility = Visibility.Visible;
					else if (v._splitter != null) v._splitter.Visibility = Visibility.Collapsed;
				}
			}

			void _RemoveSplitter() {
				if (_splitter != null) {
					_RemoveGridRowCol(_splitter);
					_splitter = null;
				}
			}

			/// <summary>
			/// Gets or sets actual height of <see cref="_splitter"/> in vertical stack or width in horizontal stack.
			/// </summary>
			int _SplitterSize {
				get {
					if (_splitter == null) return 0;
					return (Parent._stack.isVertical ? _splitter.ActualHeight : _splitter.ActualWidth).ToInt();
				}
				set {
					if (_splitter == null) return;
					if (Parent._stack.isVertical) _splitter.Height = value; else _splitter.Width = value;
				}
			}

			void _SplitterContextMenu(object sender, ContextMenuEventArgs e) {
				e.Handled = true;
				var parentStack = Parent._stack;
				var m = new popupMenu();
				m.Submenu("Splitter size", m => {
					int z = _SplitterSize;
					for (int i = 1; i <= 10; i++) {
						m.AddRadio(i.ToString(), i == z, o => _SplitterSize = o.Text.ToInt());
					}
				});
				m.Separator();
				bool vert = parentStack.isVertical;
				_PreviousDockedInStack._SplitterContextMenu_Unit(m, vert ? "Top" : "Left");
				_SplitterContextMenu_Unit(m, vert ? "Bottom" : "Right");
				if ((parentStack.isVertical ? parentStack.grid.RowDefinitions.Where(o => !o.Height.IsAuto).Count() : parentStack.grid.ColumnDefinitions.Where(o => !o.Width.IsAuto).Count()) > 2) {
					//m.Separator();
					m.AddCheck("Resize Nearest\tCtrl", _splitter.ResizeNearest, _ => _splitter.ResizeNearest ^= true);
				}
				if (Parent.Parent != null) {
					m.Separator();
					m.Submenu("Stack", m => {
						bool isFloating = Parent._state.Has(_DockState.Float);
						m[isFloating ? "Dock" : "Float\tAlt+drag"] = o => Parent._SetDockState(isFloating ? 0 : _DockState.Float);
						Parent._ContextMenu_Move(m);
					});
				}
				timer.after(100, _ => Mouse.SetCursor(Cursors.Arrow)); //workaround. 30 too small, 50 ok
				m.Show();
			}

			void _SplitterContextMenu_Unit(popupMenu m, string s1) {
				var unitNow = _SizeDef.GridUnitType;
				//var unitNow = _dockedSize.GridUnitType;
				//print.it(this, unitNow);
				bool allToolbars = _IsToolbarsNode;
				bool disableFixed = !allToolbars && unitNow != GridUnitType.Pixel
					&& Parent.Children().Count(o => o._SizeDef.GridUnitType == GridUnitType.Star) < (unitNow == GridUnitType.Star ? 2 : 1);
				_UnitItem(s1 + " fixed", GridUnitType.Pixel);
				if (allToolbars) _UnitItem(s1 + " auto", GridUnitType.Auto);

				void _UnitItem(string text, GridUnitType unit) {
					m.AddCheck(text, unit == unitNow, o => _SetUnit(unit), disable: disableFixed && unit == GridUnitType.Pixel);
					//CONSIDER: don't disable. Maybe user wants to set row A fixed and then row B star, not vice versa. But if forgets and makes window smaller, some panels and splitters may become invisible.
				}

				void _SetUnit(GridUnitType unit) {
					if (unit == _SizeDef.GridUnitType) unit = GridUnitType.Star; //unchecking Fixed or Auto
					_ChangeSizeUnit(unit, true);
				}
			}

			#endregion

			#region row/col, size

			//void _NormalizeChildStars(bool percent) {//FUTURE: remove if unused. Not sure it is finished.
			//	var e = Children();
			//	double sizeOfStars = percent ? e.Sum(v => !v._dockedSize.IsStar ? 0 : v._SizeNowDockedOrNot) : 0;
			//	foreach (var v in e) {
			//		if (!v._dockedSize.IsStar) continue;
			//		var z = _SizeNowDockedOrNot;
			//		if (percent && sizeOfStars >= 0.1) z = z * 100 / sizeOfStars;
			//		_dockedSize = new GridLength(z, GridUnitType.Star);
			//		if (_IsDockedInStack) _SizeDef = _dockedSize;
			//	}
			//}

			///// <summary>
			///// If _IsDockedInStack, returns _SizeNow, else _dockedSize.Value.
			///// </summary>
			//double _SizeNowDockedOrNot => _IsDockedInStack ? _SizeNow : _dockedSize.Value;

			///// <summary>
			///// Gets actual row height in vertical stack or column width in horizontal stack.
			///// </summary>
			//double _SizeNow => new _RowCol(this).SizeNow;

			/// <summary>
			/// Gets or sets defined row height/unit in vertical stack or column width/unit in horizontal stack.
			/// </summary>
			GridLength _SizeDef {
				get => new _RowCol(this).SizeDef;
				set => new _RowCol(this) { SizeDef = value };
				//set {
				//	print.it(this, value.GridUnitType);
				//	new _RowCol(this) { SizeDef = value };
				//}
			}

			///// <summary>
			///// Gets GridLength with actual row height in vertical stack or column width in horizontal stack.
			///// </summary>
			//GridLength _SizeDefNow => new _RowCol(this).SizeDefNow;

			/// <summary>
			/// Gets or sets minimal row height in vertical stack or column width in horizontal stack.
			/// </summary>
			double _SizeMin {
				//get => new _RowCol(this).SizeMin;
				set => new _RowCol(this) { SizeMin = value };
			}

			///// <summary>
			///// Gets or sets maximal row height in vertical stack or column width in horizontal stack.
			///// </summary>
			//double _SizeMax {
			//	get => new _RowCol(this).SizeMax;
			//	set => new _RowCol(this) { SizeMax = value };
			//}

			/// <summary>
			/// Sets size = actual size and new unit.
			/// </summary>
			void _ChangeSizeUnit(GridUnitType unit, bool updateStars) {
				Debug.Assert(_IsDockedInStack);
				_dockedSize = new _RowCol(this).ChangeUnit(unit);
				if (updateStars && unit == GridUnitType.Star) //update other stars, else the splitter may jump
					foreach (var v in Parent._Stack_DockedNodes)
						if (v != this && v._dockedSize.IsStar)
							v._ChangeSizeUnit(unit, false); //unit is same, just update value in grid and _dockedSize
			}

			struct _RowCol
			{
				readonly DefinitionBase _d;

				public _RowCol(_Node node) {
					var stack = node.Parent._stack;
					int i = node._index * 2;
					if (stack.isVertical) _d = stack.grid.RowDefinitions[i];
					else _d = stack.grid.ColumnDefinitions[i];
				}

				public DefinitionBase RowCol => _d;

				public GridLength SizeDef {
					get => (_d is RowDefinition rd) ? rd.Height : (_d as ColumnDefinition).Width;
					set { if (_d is RowDefinition rd) rd.Height = value; else (_d as ColumnDefinition).Width = value; }
				}

				public double SizeNow => (_d is RowDefinition rd) ? rd.ActualHeight : (_d as ColumnDefinition).ActualWidth;

				public GridLength SizeDefNow => new GridLength(SizeNow, SizeDef.GridUnitType);

				public double SizeMin {
					get => (_d is RowDefinition rd) ? rd.MinHeight : (_d as ColumnDefinition).MinWidth;
					set { if (_d is RowDefinition rd) rd.MinHeight = value; else (_d as ColumnDefinition).MinWidth = value; }
				}

				public double SizeMax {
					get => (_d is RowDefinition rd) ? rd.MaxHeight : (_d as ColumnDefinition).MaxWidth;
					set { if (_d is RowDefinition rd) rd.MaxHeight = value; else (_d as ColumnDefinition).MaxWidth = value; }
				}

				public GridLength ChangeUnit(GridUnitType unit) { var r = new GridLength(SizeNow, unit); SizeDef = r; return r; }
			}

			static GridLength _GridLengthFromString(string s) {
				double w = 0; var u = GridUnitType.Star;
				if (s == null) u = GridUnitType.Auto;
				else if (s.Ends("*")) w = s.Length > 1 ? s.ToNumber(..^1) : 1.0;
				else { w = s.ToNumber(); u = GridUnitType.Pixel; }
				return new GridLength(w, u);
				//also tested GridLengthConverter
			}

			static string _GridLengthToString(GridLength k) {
				if (k.IsAuto) return null;
				var s = Math.Round(k.Value, 10).ToS();
				return k.IsStar ? s + "*" : s;
				//GridLength.ToString is almost same, but: for Auto returns "Auto"; can return long string like "425.79999999999995" instead of "425.8".
			}

			#endregion
		}
	}
}
