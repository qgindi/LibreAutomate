using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace Au.Controls;

public partial class KPanels {
	partial class _Node {
		[Flags]
		enum _DockState { Hide = 1, Float = 2, }
		
		[Flags]
		enum _WindowStyle { Topmost = 1, Unowned = 2, }
		
		void _CaptionContextMenu(object sender, ContextMenuEventArgs e) {
			if (!_IsGoodMouseEvent(sender, e, out var target)) return;
			e.Handled = true;
			target._CaptionContextMenu(this);
		}
		
		void _CaptionContextMenu(_Node thisOrParentTab) {
			if (_IsDocument && !_leaf.addedLater) return;
			var m = new popupMenu();
			
			bool canClose = _leaf?.canClose ?? false;
			if (canClose) m["Close\tM-click"] = _ => _UserClosing();
			_DockStateItem(_DockState.Hide, canClose ? "Hide" : "Hide\tM-click");
			
			if (_state.Has(_DockState.Float)) _DockStateItem(0, "Dock\tD-click");
			else _DockStateItem(_DockState.Float, "Float\tD-click, drag");
			
			m.Submenu("Caption at", m => {
				_CaptionAtItem(Dock.Left);
				_CaptionAtItem(Dock.Top);
				_CaptionAtItem(Dock.Right);
				_CaptionAtItem(Dock.Bottom);
				
				void _CaptionAtItem(Dock ca) {
					m.AddRadio(ca.ToString(), ca == thisOrParentTab._captionAt, o => thisOrParentTab._SetCaptionAt(ca));
				}
			});
			
			m.Submenu("Window", m => {
				_WindowItem(_WindowStyle.Topmost, "Topmost");
				_WindowItem(_WindowStyle.Unowned, "Unowned, can min/max");
				
				void _WindowItem(_WindowStyle ws, string s) {
					m.AddCheck(s, _windowStyle.Has(ws), o => { _windowStyle ^= ws; _floatWindow?.ChangeWindowStyle(ws); });
				}
			});
			
			_ContextMenu_Move(m);
			
			if (_leaf?.isExtension ?? false) {
				m["Remove..."] = o => {
					var s1 = _leafType == LeafType.Toolbar ? "toolbar" : "panel";
					if (!dialog.showYesNo($"Remove this extension {s1}?", this.Name, owner: _pm._ContainerWindow)) return;
					Delete();
					print.it($"Info: extension {s1} {this.Name} has been removed. To uninstall the extension, also remove its script from startup scripts in Options and delete the script.");
				};
			}
			
			_ShowSubmenus();
			
			//ContextMenuOpening?.Invoke(this, m);
			
			m.Show(owner: _pm._ContainerWindow);
			
			void _DockStateItem(_DockState state, string text) {
				m[text] = o => _SetDockState(state);
			}
			
			void _ShowSubmenus() {
				var a = new List<_Node>();
				foreach (var v in RootAncestor.Descendants()) {
					if (v._IsStack || !v._state.Has(_DockState.Hide)) continue;
					if (v._IsTab && v.Children().All(o => o._state.Has(_DockState.Hide))) continue;
					a.Add(v);
				}
				if (a.Count == 0) return;
				m.Separator();
				a.Sort((x, y) => {
					if (x._IsToolbar && !y._IsToolbar) return -1;
					if (y._IsToolbar && !x._IsToolbar) return 1;
					return string.Compare(x.ToString(), y.ToString(), true);
				});
				m.Submenu("Show", m => {
					int i = 0;
					foreach (var v in a) {
						if (i > 0 && a[i - 1]._IsToolbar != a[i]._IsToolbar) m.Separator();
						i++;
						m[v.ToString()] = _ => v._Unhide();
					}
#if DEBUG
					if (a.Count > 1) {
						m.Separator();
						m["Show all (debug)"] = _ => {
							foreach (var v in a) v._Unhide();
						};
					}
#endif
				});
			}
#if DEBUG
			m.Separator();
			m.Submenu("Debug", m => {
				m["Invalidate window"] = _ => _Invalidate(_pm._ContainerWindow);
				m["Invalidate floats"] = _ => {
					foreach (var v in RootAncestor.Descendants()) {
						if (v._floatWindow != null) _Invalidate(v._floatWindow);
					}
				};
				m.Add(0, "info: toggle ScrollLock if does not work", disable: true);
			});
			
			void _Invalidate(Window w) {
				//if (keys.isScrollLock) Api.InvalidateRect(w.Hwnd(), IntPtr.Zero, true); //works
				////else w.UpdateLayout(); //no
				//else w.InvalidateVisual(); //no
				
				Api.InvalidateRect(w.Hwnd(), keys.isScrollLock);
			}
#endif
		}
		
		private protected void _OnMouseDown(object sender, MouseButtonEventArgs e) {
			switch (e.ChangedButton) { case MouseButton.Left: case MouseButton.Middle: break; default: return; }
			if (_IsGoodMouseEvent(sender, e, out var target)) target._OnMouseDown(e);
		}
		
		void _OnMouseDown(MouseButtonEventArgs e) {
			if (_IsStack) {
				if (!(e.ChangedButton == MouseButton.Left && e.ClickCount == 1 && Keyboard.Modifiers == ModifierKeys.Alt)) return;
				if (Parent == null) { e.Handled = true; return; }
			}
			e.Handled = true;
			if (e.ChangedButton == MouseButton.Left) {
				if (e.ClickCount == 1) {
					e.Handled = false; //if tab item, let select it
					timer.after(1, _ => { //Dispatcher.InvokeAsync does not work
						POINT p = mouse.xy;
						if (Api.DragDetect(_elem.Hwnd(), p)) {
							_SetDockState(_DockState.Float, onDrag: true);
							_floatWindow?.Drag(p);
						}
					});
				} else if (e.ClickCount == 2) {
					_SetDockState(_state ^ _DockState.Float);
				}
			} else {
				if (_leaf?.canClose ?? false) _UserClosing();
				else _Hide();
			}
		}
		
		bool _IsGoodMouseEvent(object sender, RoutedEventArgs e, out _Node target) {
			target = null;
			if (e.Source == sender) {
				if (_IsTab && e.OriginalSource is not TabPanel) return false; //tab control border
				target = sender == _splitter ? Parent : this;
			} else if (e.Source is TabItem ti && ti.Parent == sender) target = _NodeFromTabItem(ti);
			else return false;
			return true;
		}
		
		void _UserClosing() {
			if (Closing != null) {
				var e = new CancelEventArgs();
				Closing(this, e);
				if (e.Cancel) return;
			}
			Delete();
		}
		
		void _Hide() => _SetDockState(_DockState.Hide);
		void _Unhide() => _SetDockState(_state & ~_DockState.Hide);
		
		void _SetDockState(_DockState state, bool onDrag = false) {
			//print.qm2.write(this, state, "                    ", _state);
			_savedDockState = 0;
			if (state == _DockState.Hide) state |= _state & _DockState.Float;
			if (state == _state) {
				if (state == 0 && Parent._state.Has(_DockState.Hide)) Parent._Unhide(); //in hidden tab
				return;
			}
			
			var oldState = _state;
			_state = state;
			
			if (oldState == _DockState.Float) {
				_floatWindow?.Close();
				//_floatWindow sets _floatWindow=null when closing
			} else if (state == _DockState.Float) {
				_floatWindow = new _Floating(this, onDrag);
				//ctor uses docked rect
			}
			
			if (state == 0) { //dock; was hidden or floating
				_AddRemoveCaptionAndBorder();
				if (_ParentIsTab) _ShowHideInTab(true);
				else _ShowHideInStack(true);
			} else {
				if (oldState == 0) { //was docked; now hide or float
					if (_ParentIsTab) _ShowHideInTab(false);
					else _ShowHideInStack(false);
				}
				
				_AddRemoveCaptionAndBorder();
				
				if (state == _DockState.Float) {
					_floatWindow.Content = _elem;
					_floatWindow.ShowIfOwnerVisible();
				}
			}
			
			if ((state ^ oldState).Has(_DockState.Hide)) VisibleChanged?.Invoke(this, oldState.Has(_DockState.Hide));
			if ((state ^ oldState).Has(_DockState.Float)) FloatingChanged?.Invoke(this, state.Has(_DockState.Float));
		}
		
		void _ContextMenu_Move(popupMenu m) {
			m.Submenu("Move to", m => {
				m.RawText = true;
				string sThis = ToString();
				foreach (var target in RootAncestor.Descendants(andSelf: true)) {
					bool targetInTab = target._ParentIsTab;
					if (targetInTab) {
						if (!_IsLeaf || target._IsDocument != _IsDocument) continue;
					} else if (_IsDocument && _ParentIsTab) {
						//allow only beside parent tab or in/besides another document or doc tab. Elsewhere probably not useful, just adds many menu items.
						if (target != Parent && !target._IsDocumentsNode) continue;
					}
					if (target.Ancestors(andSelf: true).Contains(this)) continue;
					
					string sTarget = target.ToString();
					var s1 = new string(' ', target.Level * 4) + sTarget + (target._state switch { 0 => null, _DockState.Float => " (floating)", _ => " (hidden)" });
					m.Submenu(s1, m => {
						bool sep = false;
						//this would be duplicate of before/after
						//if (target._IsStack || (target._IsTab && _IsLeaf && (target.FirstChild?._IsDocument ?? false) == _IsDocument)) {
						//	int i = 0;
						//	foreach (var u in target.Children()) {
						//		m[i++ == 0 ? "First" : ($"Before '{u}'")] = o => _MoveTo(u, _HowToMove.BeforeTarget);
						//	}
						//	m["Last"] = o => _MoveTo(target.LastChild, _HowToMove.AfterTarget);
						//	sep = true;
						//}
						if (target.Parent != null) {
							//if (sep) m.Separator();
							if (target.Previous != this) _AddMI($"Before '{sTarget}'", _HowToMove.BeforeTarget);
							if (target.Next != this) _AddMI($"After '{sTarget}'", _HowToMove.AfterTarget);
							sep = true;
						}
						if (!targetInTab) {
							if (sep) m.Separator();
							if (target._IsLeaf && _IsLeaf && target._IsDocument == _IsDocument) {
								m.Add(0, $"Create tabs and add '{sThis}' as:", disable: true);
								_AddMI($"- First tab (before '{sTarget}')", _HowToMove.FirstInNewTab);
								_AddMI($"- Last tab (after '{sTarget}')", _HowToMove.LastInNewTab);
								m.Separator();
							}
							m.Add(0, $"Create stack and add '{sThis}' at:", disable: true);
							_AddMI("- Left", _HowToMove.NewStack, Dock.Left);
							_AddMI("- Right", _HowToMove.NewStack, Dock.Right);
							_AddMI("- Top", _HowToMove.NewStack, Dock.Top);
							_AddMI("- Bottom", _HowToMove.NewStack, Dock.Bottom);
						}
						if (target._IsStack || (target._IsTab && _IsLeaf) && target.FirstChild == null) { //empty
							m.Separator();
							_AddMI($"- Into '{sTarget}'", _HowToMove.Child);
						}
						
						void _AddMI(string text, _HowToMove how, Dock dock = default) {
							_MoveRect k = new(target, how, dock);
							m.Add(text, o => _MoveTo(k.target, k.how, k.dock)).Tag = k;
						}
						
						_RectTimer(m, true);
					}).Tag = target;
				}
				
				_RectTimer(m, false);
				
				static void _RectTimer(popupMenu m, bool second) {
					var osd = new osdRect { Color = second ? 0x60c000 : 0x4040ff };
					if (second) osd.Opacity = .5;
					PMItem pmi = null;
					timer.every(100, t => {
						if (m.IsOpen) {
							var mi = m.FocusedItem;
							if (mi != pmi) {
								pmi = mi;
								bool visible = false;
								if (mi != null) {
									if (!second) {
										var n = mi.Tag as _Node;
										if (visible = _GetRectInScreen(n, out var r)) osd.Rect = r;
									} else if (mi.Tag is _MoveRect k) {
										var n = k.target;
										if (n._floatWindow == null)
											if (visible = _GetRectInScreen(n, out var r)) {
												r.Inflate(-osd.Thickness, -osd.Thickness);
												if (k.how is _HowToMove.BeforeTarget or _HowToMove.AfterTarget) {
													bool vert = n._ParentIsTab ? n.Parent._captionAt is Dock.Left or Dock.Right : n.Parent._stack.isVertical;
													if (k.how == _HowToMove.BeforeTarget) {
														if (vert) r.bottom = r.top + r.Height / 2; else r.right = r.left + r.Width / 2;
													} else {
														if (vert) r.top = r.bottom - r.Height / 2; else r.left = r.right - r.Width / 2;
													}
												} else if (k.how == _HowToMove.Child) {
													if (n._stack.isVertical) r.bottom = r.top + r.Height / 4; else r.right = r.left + r.Width / 4;
												} else if (k.how == _HowToMove.NewStack) {
													if (k.dock == Dock.Left) r.right = r.left + r.Width / 2;
													if (k.dock == Dock.Right) r.left = r.right - r.Width / 2;
													if (k.dock == Dock.Top) r.bottom = r.top + r.Height / 2;
													if (k.dock == Dock.Bottom) r.top = r.bottom - r.Height / 2;
												}
												osd.Rect = r;
											}
									}
								}
								osd.Visible = visible;
							}
						} else {
							t.Stop();
							osd.Dispose();
						}
					});
				}
				
				static bool _GetRectInScreen(_Node n, out RECT r) {
					if (n._IsVisibleReally()) {
						FrameworkElement e =
							n._IsStack ? n._stack.grid
							: n._IsTab ? n._tab.tc
							: n._elem.Parent is TabItem ti ? ti
							: n._leaf.panel.Panel;
						try {
							r = e.RectInScreen();
							return true;
						}
						catch (Exception) {  }
					}
					r = default;
					return false;
				}
			});
		}
		
		enum _HowToMove //don't reorder
		{
			BeforeTarget, //before target in parent stack or tab
			AfterTarget, //after target in parent stack or tab
			NewStack, //create new stack in place of target; add target and this to it; use dock to set orientation of new stack and index ot this and target
			FirstInNewTab, //create new tab in place of target; add this (first) and target to it
			LastInNewTab, //create new tab in place of target; add target and this (last) to it
			Child, //add as child of target (target is empty stack or tab)
		}
		
		record class _MoveRect(_Node target, _HowToMove how, Dock dock = default);
		
		void _MoveTo(_Node target, _HowToMove how, Dock dock = default) {
			if (target == this) return;
			bool beforeAfter = how <= _HowToMove.AfterTarget;
			if (_state != 0) _SetDockState(0);
			if (target._state != 0 && !beforeAfter) target._SetDockState(0);
			
			bool after = how == _HowToMove.AfterTarget;
			var oldParent = Parent;
			
			if (beforeAfter && target.Parent == oldParent && oldParent._IsTab) { //just reorder buttons
				_ReorderInTab(target, after);
				return;
			}
			
			_RemoveFromParentWhenMovingOrDeleting();
			
			switch (how) {
			case _HowToMove.NewStack:
				new _Node(target, isTab: false, verticalStack: dock == Dock.Top || dock == Dock.Bottom);
				target._AddToStack(moving: false, c_defaultSplitterSize);
				after = dock == Dock.Right || dock == Dock.Bottom;
				break;
			case _HowToMove.FirstInNewTab:
			case _HowToMove.LastInNewTab:
				new _Node(target, isTab: true);
				target._AddToTab(moving: false);
				after = how == _HowToMove.LastInNewTab;
				break;
			}
			
			if (how == _HowToMove.Child) _AddToParentWhenMoving(target);
			else _AddToParentWhenMovingOrAddingLater(target, after);
			
#if false //debug print
			int i = 0;
			foreach (var v in Parent.Children()) {
				print.it(i++, v._index, v);
				if (Parent._IsStack) {
					if (v._splitter != null) print.it("splitter", _RC(v._splitter));
					print.it("elem    ", _RC(v._elem), v._dockedSize, v._SizeDef);
					int _RC(FrameworkElement e) => Parent._stack.isVertical ? Grid.GetRow(e) : Grid.GetColumn(e);
				}
			}
#endif
			
			_RemoveParentIfNeedAfterMovingOrDeleting(oldParent);
			
			if (how <= _HowToMove.NewStack && _IsDocument && oldParent._IsTab && !_ParentIsTab) {
				_captionAt = oldParent._captionAt;
				new _Node(this, isTab: true);
				_AddToTab(moving: false);
			}
			
			if (Parent != oldParent) ParentChanged?.Invoke(this, EventArgs.Empty);
		}
		
		void _AddToParentWhenMovingOrAddingLater(_Node target, bool after) {
			target.AddSibling(this, after);
			_index = target._index + (after ? 1 : 0);
			if (_ParentIsTab) {
				_AddToTab(moving: true);
				_AddRemoveCaptionAndBorder();
				//if(select) Parent._tab.tc.SelectedIndex = _index;
			} else {
				if (!(_dockedSize.IsAuto && _IsToolbarsNode)) _dockedSize = new GridLength(100, GridUnitType.Star);
				if (Parent.Count == 2) target._SizeDef = target._dockedSize = new GridLength(100, GridUnitType.Star);
				
				_AddToStack(moving: true, c_defaultSplitterSize);
			}
		}
		
		void _AddToParentWhenMoving(_Node parent) {
			parent.AddChild(this, first: true);
			_index = 0;
			if (_ParentIsTab) {
				_AddToTab(moving: true);
				_AddRemoveCaptionAndBorder();
				Parent._tab.tc.SelectedIndex = _index;
			} else {
				if (!(_dockedSize.IsAuto && _IsToolbarsNode)) _dockedSize = new GridLength(100, GridUnitType.Star);
				
				_AddToStack(moving: true, c_defaultSplitterSize);
			}
		}
		
		void _RemoveFromParentWhenMovingOrDeleting() {
			if (Parent._IsStack) {
				_RemoveGridRowCol(_elem);
				_RemoveSplitter();
				if (_index == 0) Next?._RemoveSplitter();
			} else {
				if (_elem.Parent is TabItem ti) { //null if hidden or floating
					ti.Content = null;
					Parent._tab.tc.Items.Remove(ti);
				}
			}
			_ShiftSiblingIndices(-1);
			Remove();
		}
		
		void _RemoveParentIfNeedAfterMovingOrDeleting(_Node oldParent) {
			int n = oldParent.Count;
			if (n == 0) {
				var pp = oldParent.Parent;
				oldParent._RemoveFromParentWhenMovingOrDeleting();
				oldParent._RemoveParentIfNeedAfterMovingOrDeleting(pp);
			} else if (n == 1) {
				if (!_IsDocument) {
					var f = oldParent.FirstChild;
					if (oldParent.Parent != null) {
						f._MoveTo(oldParent, _HowToMove.BeforeTarget);
					} else {
						f._RemoveFromParentWhenMovingOrDeleting();
						_pm._rootStack = f;
						_pm._setContainer(f._stack.grid);
					}
				}
			} else if (oldParent._IsTab && Parent != oldParent) {
				oldParent._VerticalTabHeader(onMove: true);
			}
		}
		
		void _ShiftSiblingIndices(int n) {
			for (var v = this; (v = v.Next) != null;) v._index += n;
		}
	}
	
	[Conditional("DEBUG")]
	internal void PrintTree_(string header = null) {
		print.qm2.write("-----" + header);
		foreach (var v in _rootStack.Descendants(true)) {
			print.qm2.write(new string('\t', v.Level) + v);
		}
	}
}
