using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;

namespace Au.Controls;

public partial class KPanels {
	partial class _Node {
		static readonly Style
			s_styleTabControl = XamlResources.Dictionary["AuPanelsTabControlStyle"] as Style,
			s_styleTabItem = XamlResources.Dictionary["AuPanelsTabItemStyle"] as Style,
			s_styleTabLeft = XamlResources.Dictionary["TabItemVerticalLeft"] as Style,
			s_tyleTabRight = XamlResources.Dictionary["TabItemVerticalRight"] as Style;
		
		void _InitTabControl() {
			var tc = _tab.tc;
			tc.Style = s_styleTabControl;
			if (_pm.CaptionBrush != Brushes.LightSteelBlue) {
				tc.ApplyTemplate();
				if (VisualTreeHelper.GetChild(tc, 0) is Grid tg) tg.Background = _pm.CaptionBrush; //note: tc must have a parent.
			}
			tc.Padding = default;
			tc.TabStripPlacement = _captionAt;
			tc.SizeChanged += (_, e) => {
				switch (tc.TabStripPlacement) { case Dock.Top: case Dock.Bottom: return; }
				bool bigger = e.NewSize.Height > e.PreviousSize.Height;
				if (bigger != _tab.isVerticalHeader) _VerticalTabHeader(e.NewSize.Height);
			};
			tc.ContextMenuOpening += _CaptionContextMenu;
			tc.PreviewMouseDown += _OnMouseDown;
			tc.SelectionChanged += (o, e) => {
				if (e.Source != o) return; //eg a descendant ComboBox
				e.Handled = true;
				if (e.AddedItems.Count == 0) return;
				var v = (e.AddedItems[0] as TabItem).Tag as _Node;
				v.TabSelected?.Invoke(v, EventArgs.Empty);
			};
			
			//implement DontFocusTab (prevent changing focus when clicking a tabitem)
			tc.PreviewMouseLeftButtonDown += (_, e) => {
				if (e.Source is TabItem ti && ti.Tag is _Node n && n.DontFocusTab != null) {
					bool focusWithin;
					if (Keyboard.FocusedElement != null) {
						focusWithin = tc.IsKeyboardFocusWithin;
					} else {
						wnd w = Api.GetFocus();
						if (!w.IsChildOf(ti.Hwnd())) return;
						focusWithin = null != tc.FindVisualDescendant(o => o is HwndHost hh && hh.Handle == w.Handle);
					}
					if (focusWithin && ti.IsSelected) return;
					
					ti.PreviewGotKeyboardFocus += _PreviewGotKeyboardFocus;
					ti.Dispatcher.InvokeAsync(() => { ti.PreviewGotKeyboardFocus -= _PreviewGotKeyboardFocus; });
					void _PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) {
						e.Handled = true;
						if (focusWithin) ti.Dispatcher.InvokeAsync(n.DontFocusTab);
					}
					//note: cannot implement it in _AddToTab, because then we don't know whether the tab header clicked or some descendant.
				}
			};
		}
		
		void _VerticalTabHeader(double height = -1, bool onMove = false) {
			var tc = _tab.tc;
			if (tc.TabStripPlacement is Dock.Top or Dock.Bottom) return;
			
			if (height < 0) height = tc.ActualHeight;
			
			var d = _CalcHeight(); //not too slow
			bool vertHeader = d < height - 10;
			if (vertHeader == _tab.isVerticalHeader && !onMove) return;
			_tab.isVerticalHeader = vertHeader;
			var dock = tc.TabStripPlacement;
			foreach (TabItem v in tc.Items) {
				v.Style = vertHeader ? (dock == Dock.Left ? s_styleTabLeft : s_tyleTabRight) : s_styleTabItem;
			}
			
			double _CalcHeight() {
				var cult = CultureInfo.InvariantCulture;
				var fdir = tc.FlowDirection;
				var font = new Typeface(tc.FontFamily, tc.FontStyle, tc.FontWeight, tc.FontStretch);
				var fsize = tc.FontSize;
				var brush = SystemColors.ControlTextBrush;
				//var ppd = VisualTreeHelper.GetDpi(tc).PixelsPerDip; print.it(ppd); //ignored, and we don't need it
				double r = 4;
				foreach (TabItem v in tc.Items) {
					var f = new FormattedText(v.Header.ToString(), cult, fdir, font, fsize, brush, 1);
					r += f.Width + 11;
				}
				return r;
			}
		}
		
		/// <summary>
		/// Adds this to parent tab at startup or when moving.
		/// Caller before must call AddChild (or AddSibling) and set _index.
		/// </summary>
		void _AddToTab(bool moving) {
			var ti = new TabItem { Style = s_styleTabItem, Header = _leaf.name, Content = _elem, Tag = this };
			var tc = Parent._tab.tc;
			tc.Items.Insert(_index, ti);
			if (moving) {
				_ShiftSiblingIndices(1);
				Parent._VerticalTabHeader(onMove: true);
			}
		}
		
		void _ShowHideInTab(bool show) {
			var tc = Parent._tab.tc;
			var ti = tc.Items[_index] as TabItem;
			if (!show) {
				var a = tc.Items.OfType<TabItem>().Where(o => o.Visibility == Visibility.Visible).ToArray();
				if (a.Length > 1) {
					if (ti == tc.SelectedItem) {
						int i = Array.IndexOf(a, ti);
						if (++i == a.Length) i -= 2;
						tc.SelectedItem = a[i];
					}
				} else if (!_IsDocument) {
					if (Parent._state == _DockState.Float) Parent._Hide();
					else if (!Parent._state.Has(_DockState.Hide)) Parent._ShowHideInStack(show);
				}
				ti.Visibility = Visibility.Collapsed;
				ti.Content = null;
			} else {
				if (tc.Parent == null) {
					if (Parent._state.Has(_DockState.Float)) Parent._SetDockState(_DockState.Float);
					else Parent._ShowHideInStack(show);
				}
				
				ti.Content = _elem;
				ti.Visibility = Visibility.Visible;
				tc.SelectedItem = ti;
			}
		}
		
		void _ReorderInTab(_Node target, bool after) {
			if (target == this || (after && target.Next == this) || (!after && target.Previous == this)) return;
			Remove(); target.AddSibling(this, after);
			int index = 0; foreach (var v in Parent.Children()) v._index = index++;
			//to avoid auto-selecting next item when removed active item, we remove all inactive items and then add in new order.
			var tc = Parent._tab.tc;
			var sel = tc.SelectedItem;
			var a = tc.Items.OfType<TabItem>().ToArray();
			for (int i = a.Length; --i >= 0;) if (a[i] != sel) tc.Items.RemoveAt(i);
			Array.Sort(a, (x, y) => (x.Tag as _Node)._index - (y.Tag as _Node)._index);
			for (int i = 0; i < a.Length; i++) if (a[i] != sel) tc.Items.Insert(i, a[i]);
		}
		
		static _Node _NodeFromTabItem(TabItem ti) => ti.Tag as _Node;
	}
	
	class _TabControl : TabControl {
		protected override void OnKeyDown(KeyEventArgs e) {
			//Apps often use Ctrl+Tab and Ctrl+Shift+Tab eg to switch documents, but TabControl would steal them for switching tabs.
			//	To swith TabControl tabs also can be used Shift+Tab (makes tab item focused) then arrows.
			if (e.Key == Key.Tab && Keyboard.Modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift)) return;
			base.OnKeyDown(e);
		}
	}
}
