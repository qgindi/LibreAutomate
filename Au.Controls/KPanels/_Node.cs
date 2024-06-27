using System.Xml.Linq;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Au.Controls;

public partial class KPanels {
	partial class _Node : TreeBase<_Node>, ILeaf {
		readonly KPanels _pm;
		readonly _StackFields _stack;
		readonly _TabFields _tab;
		readonly _LeafFields _leaf;
		readonly FrameworkElement _elem; //_stack.grid or _tab.tc or _leaf.panel
		GridSplitter2 _splitter; //splitter before this node in stack. null if in tab or first in stack
		readonly LeafType _leafType;
		int _index; //index in parent stack or tab. Grid row/column index is _index*2, because at _index*2-1 is splitter if _index>0.
		GridLength _dockedSize;
		Dock _captionAt;
		_DockState _state, _savedDockState;
		_Floating _floatWindow;
		string _floatSavedRect;
		_WindowStyle _windowStyle;
		//_Flags _flags;
		bool _dontSave;
		
		static readonly Brush s_toolbarCaptionBrush = SystemColors.ControlBrush;
		const int c_minSize = 4;
		const int c_defaultSplitterSize = 4;
		
		class _StackFields {
			public Grid grid;
			public bool isVertical; //vertical stack with horizontal splitters
		}
		
		class _TabFields {
			public _TabControl tc;
			public bool isVerticalHeader; //vertical buttons at left/right
		}
		
		class _LeafFields {
			public _DockPanelWithBorder panel;
			public FrameworkElement content; //app sets it = any element
			public FrameworkElement caption; //TextBlock if panel/userdocument, Rectangle if toolbar/documentplaceholder. null if in tab.
			public string name; //used by the indexer to find it, also as caption/tabitem text
			public bool addedLater; //added with AddSibling
			public bool canClose; //AddSibling(canClose). Adds context menu item "Close".
			public bool isExtension; //AddSibling(isExtension). Saves.
		}
		
		[Flags]
		enum _Flags { Splitter_ResizeNearest = 1 }
		
		/// <summary>
		/// Used to create root node when loading from XML.
		/// </summary>
		public _Node(KPanels pm, XElement x) : this(pm, x, null, 0) { }
		
		/// <summary>
		/// Used to create nodes when loading from XML.
		/// </summary>
		_Node(KPanels pm, XElement x, _Node parent, int index) {
			_pm = pm;
			_index = index;
			
			string tag = x.Name.LocalName;
			
			if (parent == null) { //the root XML element
				if (tag != "stack") throw new ArgumentException("XML root element must be 'stack'");
				_pm._rootStack = this;
			} else {
				parent.AddChild(this);
			}
			
			switch (tag) {
			case "stack":
				_stack = new _StackFields { isVertical = x.Attr("o") == "v" };
				_elem = _stack.grid = new Grid();
				break;
			case "tab":
				_tab = new();
				_elem = _tab.tc = new _TabControl();
				break;
			case "panel":
			case "toolbar":
			case "document":
				_leaf = new();
				_elem = _leaf.panel = new();
				_leafType = tag[0] switch { 'p' => LeafType.Panel, 't' => LeafType.Toolbar, _ => LeafType.Document };
				_leaf.name = x.Attr("name") ?? throw new ArgumentException("XML element without 'name'");
				_leaf.isExtension = x.HasAttr("ext");
				_Dictionary.Add(_leaf.name, this);
				break;
			default: throw new ArgumentException("unknown XML tag: " + tag);
			}
			_elem.UseLayoutRounding = true;
			_elem.Tag = this;
			
			if (parent != null) {
				if (!_IsDocument) {
					_savedDockState = (_DockState)(x.Attr("state", 0) & 3);
					_floatSavedRect = x.Attr("floatRect");
					x.Attr(out _windowStyle, "window");
				}
				if (!_IsStack) x.Attr(out _captionAt, "captionAt");
				
				if (_ParentIsStack) {
					_dockedSize = _GridLengthFromString(x.Attr("z")); //height in vertical stack or width in horizontal stack
					_AddToStack(moving: false, _index == 0 ? 0 : x.Attr("s", c_defaultSplitterSize));
					
					var flags = (_Flags)x.Attr("flags", 0);
					if (flags.Has(_Flags.Splitter_ResizeNearest) && _splitter != null) _splitter.ResizeNearest = true;
					
					if (_IsTab) _InitTabControl();
				} else {
					_AddToTab(moving: false);
				}
			}
			
			if (!_IsLeaf) { //stack or tab
				if (_ParentIsTab) throw new ArgumentException(tag + " in 'tab'");
				int i = 0;
				foreach (var e in x.Elements()) {
					new _Node(_pm, e, this, i++);
				}
				Debug.Assert(i > 0);
				
				if (_IsTab && i > 0) {
					_tab.tc.SelectedIndex = Math.Clamp(x.Attr("active", 0), 0, i - 1);
				}
			}
			
			//print.it(new string('\t', parent?.Level ?? 0) + _ntype, _ptype, Name, _indexInParent);
			
			if (parent == null) { //the root XML element
				int nVisible = 0; _Node firstHidden = null;
				List<_Node> aFloat = null;
				foreach (var v in Descendants()) {
					if (!v._IsStack) if (v._IsVisibleReally(true)) nVisible++; else firstHidden ??= v;
					var ds = v._savedDockState;
					if (ds == _DockState.Float) (aFloat ??= new()).Add(v);
					else if (ds != 0) v._SetDockState(ds);
				}
				if (nVisible == 0 && firstHidden != null) { //if all non-stack hidden, unhide one, else user cannot unhide any because there are no captions to show the context menu
					firstHidden._SetDockState(0);
				}
				if (aFloat != null) {
					DependencyPropertyChangedEventHandler eh = null;
					eh = (_, e) => {
						if (e.NewValue is bool visible && visible) {
							_stack.grid.IsVisibleChanged -= eh;
							_stack.grid.Dispatcher.InvokeAsync(() => { foreach (var v in aFloat) v._SetDockState(_DockState.Float); });
						}
					};
					_stack.grid.IsVisibleChanged += eh;
				}
				
				//timer.after(1000, _ => _Test(5));
				////timer.after(5000, _ => _Test(0));
				//void _Test(int margin) {
				//	foreach (var v in Descendants(true)) {
				//		if (v._IsStack) v._stack.grid.Background = (v.Level & 3) switch { 0 => Brushes.CornflowerBlue, 1 => Brushes.Khaki, 2 => Brushes.YellowGreen, _ => Brushes.LightYellow };
				//		if(v!=this) v._elem.Margin = new Thickness(margin);
				//		if (v._splitter != null) v._splitter.Visibility = Visibility.Collapsed;
				//	}
				//}
				
				////_stack.grid.PreviewMouseMove += _RootGrid_PreviewMouseMove;
			}
		}
		
		/// <summary>
		/// Used when moving a node, to create new parent (this) stack or tab for it and target.
		/// Also when moving a node, to create new parent (this) tab for it (target).
		/// </summary>
		_Node(_Node target, bool isTab, bool verticalStack = false) {
			_pm = target._pm;
			
			bool targetIsRoot = !isTab && target.Parent == null;
			if (targetIsRoot) {
				_pm._rootStack = this;
			} else {
				_index = target._index;
				target.AddSibling(this, after: false);
				target.Remove();
				target._index = 0;
			}
			AddChild(target);
			
			if (isTab) {
				_tab = new();
				_elem = _tab.tc = new _TabControl();
			} else {
				_stack = new _StackFields { isVertical = verticalStack };
				_elem = _stack.grid = new Grid();
			}
			_elem.Tag = this;
			
			if (targetIsRoot) {
				//target._dockedSize = ...; //_AddToParentWhenMovingOrAddingLater will set it for target and this
				_pm._setContainer(_stack.grid);
			} else {
				_ReplaceInStack(target);
				if (isTab) {
					_captionAt = target._captionAt;
					_InitTabControl();
				}
			}
		}
		
		/// <summary>
		/// Used when creating new leaf node later (after loading).
		/// </summary>
		_Node(_Node target, bool after, LeafType type, string name, bool canClose, bool isExtension) {
			_pm = target._pm;
			_leaf = new() { addedLater = true, name = name, canClose = canClose, isExtension = isExtension };
			_elem = _leaf.panel = new() { Tag = this, UseLayoutRounding = true };
			_leafType = type;
			_dontSave = !isExtension;
			_Dictionary.Add(name, this);
			_AddToParentWhenMovingOrAddingLater(target, after);
		}
		
		public void Save(XmlWriter x) {
			if (Parent == null) { //mark to not save stack/tab nodes without savable leaf descendants
				_Children(this);
				static bool _Children(_Node p) {
					bool R = false;
					foreach (var v in p.Children()) {
						if (!v._IsLeaf) v._dontSave = !_Children(v);
						R |= !v._dontSave;
					}
					return R;
				}
			} else {
				if (_dontSave) return;
			}
			
			x.WriteStartElement(_IsStack ? "stack" : (_IsTab ? "tab" : (_IsToolbar ? "toolbar" : (_IsDocument ? "document" : "panel"))));
			
			if (_IsStack) x.WriteAttributeString("o", _stack.isVertical ? "v" : "h");
			
			if (Parent != null) {
				if (_IsLeaf) {
					x.WriteAttributeString("name", _leaf.name);
					if (_leaf.isExtension) x.WriteAttributeString("ext", "");
				}
				
				if (_ParentIsStack) {
					if (!_dockedSize.IsAuto) {
						if (_IsDockedInStack) _dockedSize = _SizeDef; //update _dockedSize
						x.WriteAttributeString("z", _GridLengthToString(_dockedSize));
					}
					
					var z = _SplitterSize;
					if (z > 0 && z != c_defaultSplitterSize) x.WriteAttributeString("s", z.ToString());
					
					if (_splitter != null && _splitter.ResizeNearest) x.WriteAttributeString("flags", ((int)_Flags.Splitter_ResizeNearest).ToString());
				}
				
				if (_IsTab) {
					int i = _tab.tc.SelectedIndex;
					if (i > 0) x.WriteAttributeString("active", i.ToString());
				}
				
				if (_captionAt != 0) x.WriteAttributeString("captionAt", _captionAt.ToString());
				if (!_IsDocument) {
					if (_state != 0) x.WriteAttributeString("state", ((int)_state).ToString());
					_floatWindow?.Save();
					if (_floatSavedRect != null) x.WriteAttributeString("floatRect", _floatSavedRect);
					if (_windowStyle != 0) x.WriteAttributeString("window", _windowStyle.ToString());
				}
			}
			
			if (!_IsLeaf) foreach (var v in Children()) v.Save(x);
			
			x.WriteEndElement();
		}
		
		bool _IsStack => _stack != null;
		bool _IsTab => _tab != null;
		bool _IsLeaf => _leaf != null;
		bool _IsPanel => _leafType == LeafType.Panel;
		bool _IsToolbar => _leafType == LeafType.Toolbar;
		bool _IsDocument => _leafType == LeafType.Document;
		bool _ParentIsStack => Parent?._IsStack ?? false;
		bool _ParentIsTab => Parent?._IsTab ?? false;
		bool _ParentIsTabAndNotFloating => _ParentIsTab && _state != _DockState.Float;
		
		/// <summary>
		/// true if this is toolbar or this is stack/tab containing only toolbars.
		/// </summary>
		bool _IsToolbarsNode => _IsToolbar || (!_IsLeaf && Descendants().All(o => o._IsToolbar));
		
		/// <summary>
		/// true if this is document or this is tab containing documents (or tab with 0 children, which normally is not possible).
		/// </summary>
		bool _IsDocumentsNode => _IsDocument || (_IsTab && (FirstChild?._IsDocument ?? true));
		
		Dictionary<string, _Node> _Dictionary => (_leaf.addedLater && _IsDocument) ? _pm._dictUserDoc : _pm._dictLeaf;
		
		/// <summary>
		/// Gets name of panel/toolbar/document. Exception if not leaf.
		/// </summary>
		public string Name => _leaf.name;
		
		/// <summary>
		/// Gets the UI element of this node. It is Grid if this is stack, or TabControl if tab, else DockPanel.
		/// </summary>
		public FrameworkElement Elem => _elem;
		
		public override string ToString() {
			string s;
			if (_IsLeaf) {
				s = Name;
				if (_IsToolbar) s = "tb " + Name;
				return s;
			}
			if (Parent == null) return "Root stack";
			var a = Descendants().Where(o => o._IsLeaf).ToArray();
			s = a.Length switch {
				0 => "",
				1 => a[0].ToString(),
				2 => a[0].ToString() + ", " + a[1].ToString(),
				_ => a[0].ToString() + " ... " + a[^1].ToString()
			};
			return (_IsTab ? "Tabs {" : "Stack {") + s + "}";
		}
		
		//string _ToStringWithoutTB() {
		//	var s = ToString();
		//	if (_IsToolbar) s = s[3..];
		//	return s;
		//}
		
		/// <summary>
		/// true if _IsPanel or (_IsDocument and _leaf.addedLater).
		/// </summary>
		bool _CanHaveCaptionWithText => _IsPanel || (_IsDocument && _leaf.addedLater);
		
		void _SetCaptionAt(Dock ca, bool firstTime = false) {
			//if (_ParentIsTab) {
			//	Parent._SetCaptionAt(ca, firstTime);
			//	return;
			//}
			Dock old = firstTime ? Dock.Top : _captionAt;
			_captionAt = ca;
			if (_IsTab) {
				var tc = _tab.tc;
				if (ca == tc.TabStripPlacement) return;
				if (_tab.isVerticalHeader) {
					_tab.isVerticalHeader = false;
					foreach (TabItem v in tc.Items) v.Style = null;
				}
				tc.TabStripPlacement = ca;
				foreach (TabItem v in tc.Items) v.MinWidth = tc.TabStripPlacement is Dock.Top or Dock.Bottom ? c_tabHeaderMinWidth : 0;
				_VerticalTabHeader();
			} else if (_leaf.caption != null) {
				DockPanel.SetDock(_leaf.caption, ca);
				if (_leaf.content != null) _SetToolbarOrientation();
				if (ca == old || !_CanHaveCaptionWithText) return;
				if (ca == Dock.Top || ca == Dock.Bottom) {
					if (old == Dock.Left || old == Dock.Right) _leaf.caption.LayoutTransform = null;
				} else {
					_leaf.caption.LayoutTransform = new RotateTransform(ca == Dock.Left ? 270d : 90d);
				}
			}
		}
		
		void _AddRemoveCaptionAndBorder() {
			if (!_IsLeaf) return;
			if (_ParentIsTab && !_state.Has(_DockState.Float)) {
				if (_leaf.caption != null) {
					_leaf.panel.Children.Remove(_leaf.caption);
					_leaf.caption = null;
					
					_leaf.panel.BorderThickness = default;
				}
			} else {
				if (_leaf.caption == null) {
					if (_CanHaveCaptionWithText) {
						_leaf.caption = new TextBlock {
							Text = Name,
							TextAlignment = TextAlignment.Center,
							Padding = new Thickness(2, 1, 2, 3),
							Background = _pm.CaptionBrush,
							Foreground = Brushes.Black,
							TextTrimming = TextTrimming.CharacterEllipsis
						};
					} else if (_IsToolbar) {
						var c = new Rectangle {
							MinHeight = 5,
							MinWidth = 5,
							Fill = s_toolbarCaptionBrush,
							//note: without Fill there are no events
						};
						c.MouseEnter += (_, _) => c.Fill = _pm.CaptionBrush;
						c.MouseLeave += (_, _) => c.Fill = s_toolbarCaptionBrush;
						_leaf.caption = c;
					} else { //document placeholder
						var c = new Rectangle {
							//MinHeight = 5, MinWidth = 5, //rejected. Let be 0 (no caption). User could accidentally undock when trying to scroll.
							//	Not tested captionless with tabbed documents. Then probably this code not used because _CanHaveCaptionWithText true.
							Fill = _pm.CaptionBrush,
						};
						_leaf.caption = c;
						
						_leaf.panel.LastChildFill = false;
						bool hasDoc = false;
						c.SizeChanged += (_, e) => {
							bool has = _leaf.panel.Children.Count > 1;
							if (has != hasDoc) _leaf.panel.LastChildFill = hasDoc = has;
						};
					}
					_leaf.panel.Children.Insert(0, _leaf.caption);
					_SetCaptionAt(_captionAt, true);
					_leaf.caption.ContextMenuOpening += _CaptionContextMenu;
					_leaf.caption.MouseDown += _OnMouseDown;
					
					if (_pm.BorderBrush != null && !_IsToolbar) {
						_leaf.panel.BorderBrush = _pm.BorderBrush;
						_leaf.panel.BorderThickness = new Thickness(1);
					}
				}
			}
		}
		
		void _SetToolbarOrientation() {
			if (_IsToolbar && _leaf.content is ToolBarTray t) {
				var ori = _captionAt == Dock.Top || _captionAt == Dock.Bottom ? Orientation.Vertical : Orientation.Horizontal;
				if (t.Orientation != ori) t.Orientation = ori;
			}
		}
		
		#region ILeaf
		
		FrameworkElement ILeaf.Content {
			get => _leaf.content;
			set {
				if (_leaf.content != null) _leaf.panel.Children.Remove(_leaf.content);
				_leaf.panel.Children.Add(_leaf.content = value);
				_SetToolbarOrientation();
			}
		}
		
		bool ILeaf.Visible {
			get => _IsVisibleReally();
			set {
				if (value && _savedDockState == _DockState.Float) return; //will make float async
				if (value) _Unhide(); else _Hide();
				if (value && _ParentIsTabAndNotFloating) {
					Parent._tab.tc.SelectedIndex = _index;
					if (value && !_elem.IsLoaded) _elem.UpdateLayout(); //workaround: WPF creates HwndHost control handle async. Let's create now.
				}
			}
		}
		
		/// <summary>
		/// Returns true if this node does not have hidden state and is not docked in hidden tab.
		/// </summary>
		bool _IsVisibleReally(bool useSavedState = false) {
			var state = useSavedState ? _savedDockState : _state;
			if (state.Has(_DockState.Hide)) return false;
			if (state == 0 && _ParentIsTab) return Parent._IsVisibleReally(useSavedState);
			return true;
		}
		
		bool ILeaf.Floating {
			get => _state == _DockState.Float;
			set {
				if (value && _savedDockState == _DockState.Float) return; //will make float async
				_SetDockState(value ? _DockState.Float : _state & ~_DockState.Float);
			}
		}
		
		public Action DontFocusTab { get; set; }
		
		ParentInfo ILeaf.Parent => new ParentInfo(_leaf.panel.Panel, Parent._elem, _index);
		
		ILeaf ILeaf.AddSibling(bool after, LeafType type, string name, bool canClose, bool isExtension) {
			if (name == null || (type is not (LeafType.Panel or LeafType.Toolbar or LeafType.Document))) throw new ArgumentException();
			return new _Node(this, after, type, name, canClose, isExtension);
		}
		
		public void Delete() {
			if (!_leaf.addedLater && !_leaf.isExtension) throw new InvalidOperationException();
			if (_state == _DockState.Float) _SetDockState(0);
			var oldParent = Parent;
			_RemoveFromParentWhenMovingOrDeleting();
			_RemoveParentIfNeedAfterMovingOrDeleting(oldParent);
			_Dictionary.Remove(_leaf.name);
		}
		
		void ILeaf.Rename(string name) {
			//if (!_IsLeaf) throw new InvalidOperationException(); //impossible, unless called from this class
			if (name == null) throw new ArgumentException();
			_Dictionary.Remove(_leaf.name);
			_Dictionary.Add(_leaf.name = name, this);
			if (_leaf.caption is TextBlock t) t.Text = name;
			if (_ParentIsTab) {
				(Parent._tab.tc.Items[_index] as TabItem).Header = name;
				Parent._VerticalTabHeader(onMove: true);
			}
			if (_floatWindow != null) _floatWindow.Title = name;
		}
		
		public event EventHandler<bool> VisibleChanged;
		
		public event EventHandler<bool> FloatingChanged;
		
		public event System.ComponentModel.CancelEventHandler Closing;
		
		//public event EventHandler<popupMenu> ContextMenuOpening;
		
		public event EventHandler TabSelected;
		
		public event EventHandler ParentChanged;
		
		#endregion
	}
	
	class _DockPanelWithBorder : Border {
		public readonly DockPanel Panel;
		
		public _DockPanelWithBorder() {
			Child = Panel = new();
			SnapsToDevicePixels = true;
		}
		
		public UIElementCollection Children => Panel.Children;
		
		public bool LastChildFill { get => Panel.LastChildFill; set => Panel.LastChildFill = value; }
		
		public new object Tag {
			get => base.Tag;
			set { base.Tag = value; Panel.Tag = value; }
		}
	}
}

