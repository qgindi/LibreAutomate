using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Input;

namespace Au.Controls;

public partial class KPanels {
	partial class _Node {
		class _Floating : Window {
			readonly _Node _node;
			readonly Window _owner;
			readonly bool _noActivate;
			
			public _Floating(_Node node, bool onDrag) {
				_node = node;
				_owner = _node._pm._ContainerWindow;
				_noActivate = _node._IsToolbarsNode || _node.DontActivateFloating != null;
				
				//workaround for: if a HwndHost-ed native control in this panel is focused, WPF may activate the floating panel and disable next mouse click
				if (Keyboard.FocusedElement == null) {
					var wFocus = Api.GetFocus();
					if (!wFocus.Is0 && wFocus.IsChildOf(_owner.Hwnd())) {
						if (null != _node.Elem.FindVisualDescendant(o => o is HwndHost h && h.Handle == wFocus.Handle)) _owner.Focus();
					}
				}
				
				var style = WS.THICKFRAME | WS.POPUP | WS.CLIPCHILDREN;
				if (_node._IsStack) style |= WS.CAPTION;
				if (_node._windowStyle.Has(_WindowStyle.Unowned)) style |= WS.CAPTION | WS.MAXIMIZEBOX | WS.MINIMIZEBOX | WS.SYSMENU;
				var estyle = _node._windowStyle.Has(_WindowStyle.Unowned) ? WSE.APPWINDOW | WSE.WINDOWEDGE : WSE.TOOLWINDOW | WSE.WINDOWEDGE;
				
				RECT rect = default;
				bool defaultRect = onDrag | (_node._floatSavedRect == null);
				if (defaultRect) {
					var c2 = _node._ParentIsTab ? _node.Parent._elem : _node._elem;
					if (c2.IsVisible) {
						rect = c2.RectInScreen();
						Dpi.AdjustWindowRectEx(c2, ref rect, style, estyle);
					} else {
						var p = mouse.xy;
						rect = (p.x - 10, p.y - 10, 200, 200);
					}
				}
				
				base.SourceInitialized += (_, _) => {
					var w = this.Hwnd();
					w.SetStyle(style);
					if (_noActivate) w.SetExStyle(WSE.NOACTIVATE, WSFlags.Add);
				};
				
				base.Title = script.name + " - " + _node.ToString();
				base.WindowStartupLocation = WindowStartupLocation.Manual;
				base.ShowActivated = false;
				if (!_node._windowStyle.Has(_WindowStyle.Unowned)) {
					if (!_node._windowStyle.Has(_WindowStyle.Topmost)) base.Owner = _owner;
					base.WindowStyle = WindowStyle.ToolWindow;
					base.ShowInTaskbar = false; //never mind: if false, WPF creates a "Hidden Window", probably as owner, even if owner specified
				}
				if (_node._windowStyle.Has(_WindowStyle.Topmost)) base.Topmost = true;
				
				if (defaultRect) this.SetRect(rect);
				else WndSavedRect.Restore(this, _node._floatSavedRect);
				
				_owner.Closing += _Owner_Closing;
				_owner.IsVisibleChanged += _Owner_IsVisibleChanged;
			}
			
			public void ShowIfOwnerVisible() {
				if (_owner.IsVisible) Show();
			}
			
			private void _Owner_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
				//never mind: no event if closed from outside.
				bool visible = (bool)e.NewValue;
				//print.it("owner visible", visible);
				if (visible) Show(); else Hide();
			}
			
			private void _Owner_Closing(object sender, CancelEventArgs e) {
				//print.it("owner closing", e.Cancel);
				if (!e.Cancel) this.Close();
			}
			
			protected override void OnClosing(CancelEventArgs e) {
				//print.it("closing", e.Cancel, _node._state);
				if (!e.Cancel) {
					_owner.IsVisibleChanged -= _Owner_IsVisibleChanged;
					Save();
					Content = null;
					_node._floatWindow = null;
				}
				base.OnClosing(e);
			}
			
			public void Save() {
				//print.it("save");
				_node._floatSavedRect = new WndSavedRect(this).ToString();
			}
			
			protected override void OnSourceInitialized(EventArgs e) {
				base.OnSourceInitialized(e);
				var hs = PresentationSource.FromVisual(this) as HwndSource;
				hs.AddHook(WndProc);
			}
			
			private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
				var w = (wnd)hwnd;
				//WndUtil.PrintMsg(w, msg, wParam, lParam);
				switch (msg) {
				case Api.WM_MOUSEACTIVATE:
					bool no = Math2.LoWord(lParam) != Api.HTCLIENT;
					if (!no) {
						if (_noActivate) { //activate if clicked a focusable element
							var ie = Mouse.DirectlyOver;
							if (ie == null) { //native child control?
								if (_node.DontActivateFloating != null) {
									var c = wnd.fromMouse(WXYFlags.Raw);
									if (c != w && !c.Is0) {
										if (wnd.Internal_.ToWpfElement(c) is { } e) no = _node.DontActivateFloating(e);
									}
								}
							} else {
								no = true;
								for (var e = ie as UIElement; e != null && e != _node._elem; e = VisualTreeHelper.GetParent(e) as UIElement) {
									//print.it(e, e.Focusable);
									if (e.Focusable) {
										if (e is ButtonBase) {
											//e.Focusable = false;
											//Dispatcher.InvokeAsync(() => e.Focusable = true);
										} else {
											no = _node.DontActivateFloating?.Invoke(e) == true;
										}
										break;
									}
								}
							}
						} else if (!_node._IsStack) { //activate if clicked not panel caption and not tab header
							FrameworkElement e;
							if (_node._IsTab) e = (_node._tab.tc.SelectedItem as TabItem)?.Content as FrameworkElement;
							else e = _node._leaf.content;
							no = e != null && !e.RectInScreen().Contains(mouse.xy);
						}
					}
					if (no) {
						//print.it("MA_NOACTIVATE");
						w.ZorderTop();
						handled = true;
						return (IntPtr)Api.MA_NOACTIVATE;
						//never mind: if clicked or dragged resizable border, on mouse up OS activates the window.
					}
					break;
				case Api.WM_CLOSE:
					if (w.IsMinimized) w.ShowNotMinimized(); //without this would be exception (why?)
					if (_node._state == _DockState.Float) { //closing not by _SetDockState
						handled = true;
						_node._SetDockState(0);
					}
					break;
				}
				
				return default;
			}
			
			//Currently supports only moving but not docking. Docking is implemented in _ContextMenu_Move+_MoveTo.
			public void Drag(POINT p) {
				
				//bool canDock = false;
				//_DockTarget target = null;
				var w = this.Hwnd();
				RECT r = w.Rect;
				POINT offs = (p.x - r.left, p.y - r.top);
				bool ok = WndUtil.DragLoop(w, MButtons.Left, d => {
					if (d.msg.message != Api.WM_MOUSEMOVE) return;
					
					p = mouse.xy;
					w.MoveL(p.x - offs.x, p.y - offs.y);
					
					//if (!canDock && keys.gui.isAlt) {
					//	canDock = true;
					//	//w.SetTransparency(true, 128);
					//	//_dockIndic = new _DockIndicator(_manager, this);
					//	//_dockIndic.Show(this);
					//}
					////if (canDock) _dockIndic.OnFloatMoved(_manager.PointToClient(p));
				});
				
				//if (canDock) {
				//	w.SetTransparency(false);
				//	_dockIndic.Close();
				//	if (ok) {
				//		target = _dockIndic.OnFloatDropped();
				//	}
				//	_dockIndic = null;
				//}
				
				//return target;
			}
			
			public void ChangeWindowStyle(_WindowStyle style) {
				bool on = _node._windowStyle.Has(style);
				var w = this.Hwnd();
				if (style == _WindowStyle.Topmost) {
					bool unowned = _node._windowStyle.Has(_WindowStyle.Unowned);
					if (on) {
						if (!unowned) base.Owner = null;
						w.ZorderTopmost();
					} else {
						w.ZorderNoTopmost();
						if (!unowned) base.Owner = _owner;
					}
				} else if (style == _WindowStyle.Unowned) {
					if (!on) w.ShowNotMinMax();
					bool topmost = _node._windowStyle.Has(_WindowStyle.Topmost);
					if (!topmost) base.Owner = on ? null : _owner; //or `WndUtil.SetOwnerWindow(w, on ? default : _owner.Hwnd());`
					w.SetExStyle(w.ExStyle & ~(WSE.TOOLWINDOW | WSE.APPWINDOW) | (on ? WSE.APPWINDOW : WSE.TOOLWINDOW));
					var ws = WS.CAPTION | WS.MAXIMIZEBOX | WS.MINIMIZEBOX | WS.SYSMENU;
					if (!on && _node._IsStack) ws &= ~WS.CAPTION;
					w.SetStyle(ws, on ? WSFlags.Add | WSFlags.UpdateNonclient : WSFlags.Remove | WSFlags.UpdateNonclient);
					base.ShowInTaskbar = on;
				}
			}
		}
		
		public Func<UIElement, bool> DontActivateFloating { get; set; }
	}
}
