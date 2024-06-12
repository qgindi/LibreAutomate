using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Au.Types;

/// <summary>
/// Adds extension methods for some WPF classes.
/// </summary>
public static class ExtWpf {
	/// <summary>
	/// Gets native window handle of this <b>Window</b> or <b>Popup</b>, or container window handle of this child object.
	/// </summary>
	/// <returns><c>default(wnd)</c> if:
	/// <br/>• called before creating or after closing real window;
	/// <br/>• failed;
	/// <br/>• <i>t</i> is <c>null</c>.</returns>
	/// <param name="t"></param>
	public static wnd Hwnd(this DependencyObject t) {
		bool isPopup = false;
		switch (t) {
		case null: return default;
		case Window w: return (wnd)new WindowInteropHelper(w).Handle; //FromDependencyObject works too, but this is usually slightly faster
		case Popup p: t = p.Child; if (t == null) return default; isPopup = true; break; //FromVisual(Popup) returns null, FromDependencyObject too
		}
		if (PresentationSource.FromDependencyObject(t) is HwndSource hs) return (wnd)hs.Handle;
		if (isPopup) return default;
		return Hwnd(LogicalTreeHelper.GetParent(t));
	}
	//rejected: notPopup. Not useful.
	///// <summary>
	///// Gets window handle of this <b>Window</b>, <b>Popup</b> or container window handle of this child object.
	///// Returns <c>default(wnd)</c> if: called before creating real window; failed; <i>t</i> is <c>null</c>.
	///// </summary>
	///// <param name="t"></param>
	///// <param name="notPopup">If this is <b>Popup</b> or in a <b>Popup</b>, get handle of popup's owner <b>Window</b>.</param>
	//public static wnd Hwnd(this DependencyObject t, bool notPopup = false)
	//{
	//	switch(t) {
	//	case null: return default;
	//	case Window w: return (wnd)new WindowInteropHelper(w).Handle; //FromDependencyObject works too, but this is usually slightly faster
	//	case Popup p when !notPopup: t = p.Child; if(t == null) return default; break; //FromVisual(Popup) returns null; or maybe owner window, not tested.
	//	}
	//	if(notPopup) {
	//		var w = Window.GetWindow(t); if(w == null) return default; //if Popup or in Popup, gets owner WIndow
	//		return (wnd)new WindowInteropHelper(w).Handle;
	//	}
	//	if(PresentationSource.FromDependencyObject(t) is HwndSource hs) return (wnd)hs.Handle;
	//	return default;
	//}
	
	/// <summary>
	/// Gets <b>IWin32Window</b> of this window for <b>System.Windows.Forms</b> functions like <b>Form.ShowDialog</b> and <b>ColorDialog.ShowDialog</b>.
	/// </summary>
	public static System.Windows.Forms.IWin32Window FormOwner(this Window t) {
		var nw = new System.Windows.Forms.NativeWindow();
		nw.AssignHandle(t.Hwnd().Handle);
		return nw;
	}
	
	/// <summary>
	/// Enumerates visual descendant objects, including parts of composite controls, and calls callback function <i>f</i> for each.
	/// When <i>f</i> returns <c>true</c>, stops and returns that object. Returns <c>null</c> if <i>f</i> does not return <c>true</c>.
	/// </summary>
	public static DependencyObject FindVisualDescendant(this DependencyObject t, Func<DependencyObject, bool> f, bool orSelf = false) {
		if (orSelf && f(t)) return t;
		for (int i = 0, n = VisualTreeHelper.GetChildrenCount(t); i < n; i++) {
			var v = VisualTreeHelper.GetChild(t, i);
			if (f(v)) return v;
			v = FindVisualDescendant(v, f);
			if (v != null) return v;
		}
		return null;
	}
	
	/// <summary>
	/// Enumerates visual descendant objects, including parts of composite controls.
	/// </summary>
	public static IEnumerable<DependencyObject> VisualDescendants(this DependencyObject t) {
		for (int i = 0, n = VisualTreeHelper.GetChildrenCount(t); i < n; i++) {
			var v = VisualTreeHelper.GetChild(t, i);
			yield return v;
			foreach (var k in VisualDescendants(v)) yield return k;
			//TODO3: now creates much garbage if tree is big.
			//	See https://stackoverflow.com/a/30441479/2547338.
			//	See ExtMisc.Descendants. But it cannot be used here because VisualTreeHelper does not give an IEnumerable.
		}
	}
	
	/// <summary>
	/// Enumerates logical descendant objects, including parts of composite controls.
	/// </summary>
	public static IEnumerable LogicalDescendants(this DependencyObject t) {
		//foreach (var v in LogicalTreeHelper.GetChildren(t)) {
		//	yield return v;
		//	if (v is DependencyObject d)
		//		foreach (var k in LogicalDescendants(d)) yield return k;
		//}
		return LogicalTreeHelper.GetChildren(t).Descendants_(o => o is DependencyObject d ? LogicalTreeHelper.GetChildren(d) : null);
	}
	
	/// <summary>
	/// Gets visual ancestors (<see cref="VisualTreeHelper.GetParent"/>).
	/// </summary>
	/// <param name="t"></param>
	/// <param name="andThis">Include this object.</param>
	/// <param name="last">Last ancestor to get.</param>
	public static IEnumerable<DependencyObject> VisualAncestors(this DependencyObject t, bool andThis = false, object last = null) {
		for (var v = t; v != null; v = VisualTreeHelper.GetParent(v)) {
			if (!andThis) { andThis = true; continue; }
			yield return v;
			if (v == last) yield break;
		}
	}
	
	/// <summary>
	/// Calls callback function <i>f</i> for each visual ancestor (<see cref="VisualTreeHelper.GetParent"/>), and returns the ancestor for which <i>f</i> returns <c>true</c>.
	/// Also can return <i>last</i> or <c>null</c>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="andThis">Include this object.</param>
	/// <param name="f"></param>
	/// <param name="last">When found this ancestor, stop and return <i>last</i> if <i>andLast</i> <c>true</c> or <c>null</c> if <c>false</c>.</param>
	/// <param name="andLast">If <i>last</i> found, return <i>last</i> instead of <c>null</c>.</param>
	public static DependencyObject FindVisualAncestor(this DependencyObject t, bool andThis, Func<DependencyObject, bool> f, object last, bool andLast) {
		for (var v = t; v != null; v = VisualTreeHelper.GetParent(v)) {
			if (!andThis) { andThis = true; continue; }
			if (f(v)) return v;
			if (v == last) return andLast ? v : null;
		}
		return null;
	}
	
	/// <summary>
	/// Returns the nearest visual ancestor (<see cref="VisualTreeHelper.GetParent"/>) of type <i>T</i>.
	/// Also can return <i>last</i> or <c>null</c>.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="andThis">Include this object.</param>
	/// <param name="last">When found this ancestor, stop and return <i>last</i> if <i>andLast</i> <c>true</c> or <c>null</c> if <c>false</c>.</param>
	/// <param name="andLast">If <i>last</i> found, return <i>last</i> instead of <c>null</c>.</param>
	public static DependencyObject FindVisualAncestor<T>(this DependencyObject t, bool andThis, object last, bool andLast) where T : DependencyObject {
		for (var v = t; v != null; v = VisualTreeHelper.GetParent(v)) {
			if (!andThis) { andThis = true; continue; }
			if (v is T r1) return r1;
			if (v == last) return andLast ? v : null;
		}
		return null;
	}
	
	/// <summary>
	/// Gets rectangle of this element in screen coordinates.
	/// </summary>
	/// <returns>default if this is an invisible element (but not <b>Window</b>) or if fails.</returns>
	public static RECT RectInScreen(this FrameworkElement t) {
		if (t is Window w) return w.Hwnd().Rect; //else would be incorrect: x/y of client area, width/height of window
		if (t.IsVisible) {
			try {
				Point p1 = t.PointToScreen(default), p2 = t.PointToScreen(new Point(t.ActualWidth, t.ActualHeight));
				return RECT.FromLTRB(p1.X.ToInt(), p1.Y.ToInt(), p2.X.ToInt(), p2.Y.ToInt());
			}
			catch (Exception e1) { Debug_.Print(e1); }
		}
		return default;
	}
	
	/// <summary>
	/// Sets UI Automation name.
	/// </summary>
	public static void UiaSetName(this DependencyObject t, string name) {
		System.Windows.Automation.AutomationProperties.SetName(t, name);
	}
	
	//rejected, FBC. Looks not good. Better .IsChecked == true.
	/// <summary>
	/// Returns <c>true</c> if <c>IsChecked == true</c>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	//[Obsolete("use code IsChecked == true")]
	public static bool True(this CheckBox t) => t.IsChecked == true;
	
#if true
	//TODO3: does not work if this is called in ctor and caller sets Title afterwards.
	static unsafe void _Move(Window t, int x, int y, in RECT r, bool andSize) {
		var wstate = t.WindowState;
		if (t.IsLoaded) {
			var w = t.Hwnd();
			if (w.Is0) throw new ObjectDisposedException("Window");
			if (wstate != WindowState.Normal) t.WindowState = WindowState.Normal;
			if (andSize) w.MoveL(r); else w.MoveL(x, y);
		} else {
			//tested: don't need this for Popup. Its PlacementRectangle can use physical pixels.
			
			t.WindowStartupLocation = WindowStartupLocation.Manual;
			if (wstate == WindowState.Minimized) t.ShowActivated = false;
			
			WindowsHook.ThreadCbt(k => {
				if (k.code == HookData.CbtEvent.CREATEWND) {
					var c = k.CreationInfo->lpcs;
					if (!c->style.Has(WS.CHILD)) {
						var name = c->Name;
						if (name.Length > 25 && name.StartsWith("m8KFOuCJOUmjziONcXEi3A ")) {
							k.hook.Dispose();
							var s = name[23..].ToString();
							if (name[^1] == ';') {
								c->x = s.ToInt(0, out int e); c->y = s.ToInt(e);
							} else if (RECT.TryParse(s, out var r)) {
								c->x = r.left; c->y = r.top; c->cx = r.Width; c->cy = r.Height;
							}
						}
					}
				} else { //didn't detect the window? Because unhooks when detects.
					Debug_.Print($"{k.code} {k.Hwnd}");
					//Debug_.PrintIf(k.code != HookData.CbtEvent.SETFOCUS, $"{k.code} {k.Hwnd}"); //sometimes SETFOCUS before CREATEWND, and it is bad
				}
				return false;
			});
			
			t.Left = double.NaN;
			t.Top = double.NaN;
			if (andSize) {
				t.Width = double.NaN;
				t.Height = double.NaN;
			}
			
			//temporarily change Title. I didn't find other ways to recognize the window in the hook proc. Also in title we can pass r or x y.
			string title = t.Title, s;
			if (andSize) s = "m8KFOuCJOUmjziONcXEi3A " + r.ToStringSimple(); else s = $"m8KFOuCJOUmjziONcXEi3A {x} {y};";
			t.Title = s;
			//Need to restore Title ASAP.
			//	In CBT hook cannot change window name in any way.
			//	The first opportunity is WM_CREATE, it's before WPF events, but cannot set Title property there.
			//	The sequence of .NET events depends on Window properties etc:
			//		Default: IsVisibleChanged, HwndSource.AddSourceChangedHandler, SourceInitialized, Loaded. And several unreliable events inbetween.
			//		SizeToContent: IsVisibleChanged, HwndSource.AddSourceChangedHandler, Loaded, SourceInitialized.
			//		WindowInteropHelper(w).EnsureHandle(): SourceInitialized (before ShowX), IsVisibleChanged, HwndSource.AddSourceChangedHandler, Loaded.
			//		Window without controls: Initialized, ....
			SourceChangedEventHandler eh = null;
			eh = (_, _) => {
				HwndSource.RemoveSourceChangedHandler(t, eh);
				t.Title = title;
				//if (wstate == WindowState.Normal && !t.ShowActivated) t.Hwnd().ZorderTop(); //it seems don't need it
			};
			HwndSource.AddSourceChangedHandler(t, eh);
		}
	}
#elif true //does not change Title, but I don't like creating window handle before showing window
	static void _Move(Window t, int x, int y, in RECT r, bool andSize) {
		var wstate=t.WindowState;
		if(wstate!=WindowState.Normal) t.WindowState=WindowState.Normal;
		if(t.IsLoaded) {
			var w=t.Hwnd();
			if(w.Is0) throw new ObjectDisposedException("Window");
			if(andSize) w.MoveL(r); else w.MoveL(x, y);
		} else {
			var scrn=screen.of(new POINT(x, y));
			var si=scrn.GetInfo();
			var rs=si.workArea;
		
			if(andSize) {
				x=r.left; y=r.top;
				var stc=t.SizeToContent;
				if(stc!=SizeToContent.WidthAndHeight) {
					double f=96d/scrn.Dpi;
					if(!stc.Has(SizeToContent.Width)) t.Width=r.Width*f;
					if(!stc.Has(SizeToContent.Height)) t.Height=r.Height*f;
				}
			}
		
			t.WindowStartupLocation=WindowStartupLocation.Manual;
			t.Left=double.NaN; t.Top=double.NaN;
			t.Loaded+=(_,_)=> {
				var w=t.Hwnd();
				var rw=w.Rect;
				x=Math.Clamp(x, rs.left, Math.Max(rs.right-rw.Width, rs.left));
				y=Math.Clamp(y, rs.top, Math.Max(rs.bottom-rw.Height, rs.top));
				w.MoveL(x, y);
			
				if(wstate!=WindowState.Normal) {
					if(wstate==WindowState.Maximized) t.SizeToContent=SizeToContent.Manual;
					t.WindowState=wstate;
				} else if(!t.ShowActivated) {
					w.ZorderTop();
				}
			};
		
			if(!si.isPrimary) {
				using var h=WindowsHook.ThreadCbt(d=> {
					if(d.code== HookData.CbtEvent.CREATEWND) unsafe {
						var w=d.CreationInfo(out var c, out _);
						if(c->style!=0 && !c->style.Has(WS.CHILD)) { //note: this does not work if ShowInTaskbar = false. Then WPF creates a "Hidden Window" before, even if owner specified.
							print.it(c->x, c->y, c->cx, c->cy, c->hwndParent, c->style, c->lpszClass, c->lpszName);
			//				d.hook.Unhook();
							c->x=rs.left; c->y=rs.top;
							//the hook receives 2 windows. At first the true window and then some other HwndWrapper* with 0 x y cx cy style parent. The second is never visibe.
							//We use the 'c->style!=0' to ignore it. The real window always has some styles. There is no 100% reliable and clean way to recognize the real window.
							//Don't unhook, because future .NET versions may create more windows, maybe some before the real window. Or in some conditions.
						}
					}
					return false;
				});

				new WindowInteropHelper(t).EnsureHandle();
			}
		}
	}
#else //does not work well when maximized, per-monitor DPI, etc
	static void _Move(Window t, int x, int y, in RECT r, bool andSize) {
		var wstate = t.WindowState;
		if (wstate != WindowState.Normal) t.WindowState = WindowState.Normal;
		if (t.IsLoaded) {
			var w = t.Hwnd();
			if (w.Is0) throw new ObjectDisposedException("Window");
			if (andSize) w.MoveL(r); else w.MoveL(x, y);
		} else {
			//tested: don't need this for Popup. Its PlacementRectangle uses physical pixels.

			if (andSize) {
				x = r.left; y = r.top;
				var stc = t.SizeToContent;
				if (stc != SizeToContent.WidthAndHeight) {
					double f = 96d / screen.of(x, y).Dpi;
					if (!stc.Has(SizeToContent.Width)) t.Width = r.Width * f;
					if (!stc.Has(SizeToContent.Height)) t.Height = r.Height * f;
				}
			}

			t.WindowStartupLocation = WindowStartupLocation.Manual;
			t.Left = double.NaN; t.Top = double.NaN; //default location, somewhere near top-left of primary screen or owner's screen
			if (wstate == WindowState.Minimized) t.ShowActivated = false;

			t.SourceInitialized += (_, _) => {
				var w = t.Hwnd();
				var v = screen.of(x, y).Info;
				var rs = v.workArea;
				if (!v.isPrimary) {
					using var h = WindowsHook.ThreadCbt(k => k.code == HookData.CbtEvent.ACTIVATE); //workaround for WPF bug: activates window when DPI changes
					w.MoveL(rs.left, rs.top); //let DPI-scale
				}
				var rw = w.Rect;
				x = Math.Clamp(x, rs.left, Math.Max(rs.right - rw.Width, rs.left));
				y = Math.Clamp(y, rs.top, Math.Max(rs.bottom - rw.Height, rs.top));
				w.MoveL(x, y);
				//speed: when moving to a screen with different DPI, total time is same.

				if (wstate != WindowState.Normal) {
					if (wstate == WindowState.Maximized) t.SizeToContent = SizeToContent.Manual;
					t.WindowState = wstate;
				} else if (!t.ShowActivated) {
					w.ZorderTop();
				}
			};
		}
	}
#endif
	
	/// <summary>
	/// Sets window startup location before showing it first time. Also can move already loaded window.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="x">X coordinate in screen. Physical pixels.</param>
	/// <param name="y">Y coordinate in screen. Physical pixels.</param>
	/// <remarks>
	/// The unit is physical pixels. WPF provides <b>Left</b> and <b>Top</b> properties, but the unit is logical pixels, therefore cannot set exact location on high DPI screens, especially if there are multiple screens with different DPI.
	/// 
	/// If the window is already loaded, just ensures it is not maximized/minimized and calls <see cref="wnd.MoveL"/>.
	/// 
	/// Else sets window location for normal state (not minimized/maximized). Temporarily changes <b>Title</b>. Clears <b>WindowStartupLocation</b>, <b>Left</b>, <b>Top</b>. Clears <b>ShowActivated</b> if minimized. Does not change <b>SizeToContent</b>.
	/// </remarks>
	public static void SetXY(this Window t, int x, int y) => _Move(t, x, y, default, false);
	
	/// <summary>
	/// Sets window startup rectangle (location and size) before showing it first time. Also can move/resize already loaded window.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="r">Rectangle in screen. Physical pixels.</param>
	/// <remarks>
	/// The unit is physical pixels. WPF provides <b>Left</b>, <b>Top</b>, <b>Width</b> and <b>Height</b> properties, but the unit is logical pixels, therefore cannot set exact rectangle on high DPI screens, especially if there are multiple screens with different DPI.
	/// 
	/// If the window is already loaded, just ensures it is not maximized/minimized and calls <see cref="wnd.MoveL"/>.
	/// 
	/// Else sets window rectangle for normal state (not minimized/maximized). Temporarily changes <b>Title</b>. Clears <b>WindowStartupLocation</b>, <b>Left</b>, <b>Top</b>, <b>Width</b>, <b>Height</b>. Clears <b>ShowActivated</b> if minimized. Does not change <b>SizeToContent</b>.
	/// </remarks>
	public static void SetRect(this Window t, RECT r) => _Move(t, 0, 0, r, true);
	
	/// <summary>
	/// Inserts row and adjusts row indices of children that are in other rows.
	/// </summary>
	public static void InsertRow(this Grid t, int index, RowDefinition d) {
		_GridShift(t, true, index, 1);
		t.RowDefinitions.Insert(index, d);
	}
	
	/// <summary>
	/// Inserts column and adjusts column indices of children that are in other columns.
	/// </summary>
	public static void InsertColumn(this Grid t, int index, ColumnDefinition d) {
		_GridShift(t, false, index, 1);
		t.ColumnDefinitions.Insert(index, d);
	}
	
	/// <summary>
	/// Removes row and adjusts row indices of children that are in other rows.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="index"></param>
	/// <param name="removeChildren">Remove children that are in that row.</param>
	public static void RemoveRow(this Grid t, int index, bool removeChildren) {
		if (removeChildren) _GridRemoveRowColChildren(t, true, index);
		t.RowDefinitions.RemoveAt(index);
		_GridShift(t, true, index, -1);
	}
	
	/// <summary>
	/// Removes column and adjusts column indices of children that are in other columns.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="index"></param>
	/// <param name="removeChildren">Remove children that are in that column.</param>
	public static void RemoveColumn(this Grid t, int index, bool removeChildren) {
		if (removeChildren) _GridRemoveRowColChildren(t, false, index);
		t.ColumnDefinitions.RemoveAt(index);
		_GridShift(t, false, index, -1);
	}
	
	/// <summary>
	/// Removes a child element and its row from this grid. Adjusts row indices of children that are in other rows.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="e"></param>
	/// <param name="removeOtherElements">Also remove other elements that are in that row.</param>
	public static void RemoveRow(this Grid t, UIElement e, bool removeOtherElements) {
		int i = Grid.GetRow(e);
		_GridRemoveChild(t, e);
		RemoveRow(t, i, removeOtherElements);
	}
	
	/// <summary>
	/// Removes a child element and its column from this grid. Adjusts column indices of children that are in other columns.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="e"></param>
	/// <param name="removeOtherElements">Also remove other elements that are in that column.</param>
	public static void RemoveColumn(this Grid t, UIElement e, bool removeOtherElements) {
		int i = Grid.GetColumn(e);
		_GridRemoveChild(t, e);
		RemoveColumn(t, i, removeOtherElements);
	}
	
	static void _GridShift(Grid g, bool rows, int startIndex, int shift) {
		if (startIndex >= (rows ? g.RowDefinitions.Count : g.ColumnDefinitions.Count)) return;
		foreach (UIElement e in g.Children) {
			int k = rows ? Grid.GetRow(e) : Grid.GetColumn(e);
			if (k < startIndex) continue;
			k += shift;
			if (rows) Grid.SetRow(e, k); else Grid.SetColumn(e, k);
		}
	}
	
	static void _GridRemoveRowColChildren(Grid g, bool row, int index) {
		var cc = g.Children;
		for (int i = cc.Count; --i >= 0;) {
			var e = cc[i];
			int rc = row ? Grid.GetRow(e) : Grid.GetColumn(e);
			if (rc == index) _GridRemoveChild(g, e);
		}
	}
	
	static void _GridRemoveChild(Grid g, UIElement e) {
		g.Children.Remove(e);
		Grid.SetRow(e, 0);
		Grid.SetColumn(e, 0);
	}
	
	/// <summary>
	/// Adds a child element in specified row/column.
	/// </summary>
	public static void AddChild(this Grid g, UIElement e, int row, int column, int rowSpan = 1, int columnSpan = 1) {
		Grid.SetRow(e, row);
		Grid.SetColumn(e, column);
		if (rowSpan > 1) Grid.SetRowSpan(e, rowSpan);
		if (columnSpan > 1) Grid.SetColumnSpan(e, columnSpan);
		g.Children.Add(e);
	}
	
	/// <summary>
	/// Adds one or more columns. Like <see cref="wpfBuilder.Columns"/>, but does not clear existing columns.
	/// </summary>
	/// <inheritdoc cref="wpfBuilder.Columns" path="/param"/>
	public static void AddColumns(this Grid g, params WBGridLength[] widths) {
		foreach (var v in widths) g.ColumnDefinitions.Add(v.Column);
	}
	
	/// <summary>
	/// Adds one or more rows. Like <see cref="wpfBuilder.Row"/>.
	/// </summary>
	/// <param name="heights"><inheritdoc cref="wpfBuilder.Row" path="/param[@name='height']/node()"/></param>
	public static void AddRows(this Grid g, params WBGridLength[] heights) {
		foreach (var v in heights) g.RowDefinitions.Add(v.Row);
	}
	
	/// <summary>
	/// Gets the <b>Text</b> property. Returns <c>null</c> if it is <c>""</c>.
	/// </summary>
	public static string TextOrNull(this TextBox t) => t.Text.NullIfEmpty_();
	
	/// <summary>
	/// Workaround for WPF bug: on DPI change tries to activate window.
	/// Call on <b>WM_DPICHANED</b> message or in <b>OnDpiChanged</b> override.
	/// </summary>
	public static void DpiChangedWorkaround(this Window t) => _DCW(t.Dispatcher, t.Hwnd());
	
	/// <summary>
	/// Workaround for WPF bug: on DPI change tries to activate window.
	/// Call on <b>WM_DPICHANED</b> message or in <b>OnDpiChanged</b> override. Only if top-level window.
	/// </summary>
	public static void DpiChangedWorkaround(this HwndSource t) => _DCW(t.Dispatcher, (wnd)t.Handle);
	
	static void _DCW(Dispatcher d, wnd w) {
		if (wnd.active != w) {
			bool wasVisible = w.IsVisible; //allow to activate when opening window in non-primary screen
			var h = WindowsHook.ThreadCbt(k => k.code == HookData.CbtEvent.ACTIVATE && (wnd)k.wParam == w && (wasVisible || !w.IsVisible));
			d.InvokeAsync(() => h.Dispose());
		}
	}
	
	/// <returns><c>true</c> if in <b>ShowDialog</b>, <c>false</c> if not, <c>null</c> if failed (uses reflection).</returns>
	internal static bool? IsModal_(this Window t) {
		try {
			var f = typeof(Window).GetField("_showingAsDialog", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic);
			return (bool)f.GetValue(t);
		}
		catch {
			Debug_.Print("_showingAsDialog");
			return null;
		}
	}
	
	/// <summary>
	/// Hides the grip and/or overflow controls in this toolbar.
	/// Call before the toolbar is loaded.
	/// </summary>
	/// <param name="t"></param>
	/// <param name="hideGrip">Hide grip. Sets <b>SetIsLocked</b> <c>true</c>.</param>
	/// <param name="hideOverflow">Hides the overflow button while it is disabled.</param>
	/// <exception cref="InvalidOperationException">Loaded.</exception>
	public static void HideGripAndOverflow(this ToolBar t, bool hideGrip = true, bool hideOverflow = true) {
		if (hideGrip) ToolBarTray.SetIsLocked(t, true);
		if (hideOverflow) {
			if (t.IsLoaded) throw new InvalidOperationException("loaded");
			SizeChangedEventHandler h = null;
			h = (_, _) => {
				t.SizeChanged -= h;
				if (t.Template.FindName("OverflowButton", t) is ButtonBase ob) {
					ob.SetBinding(
						UIElement.VisibilityProperty,
						new Binding("IsEnabled") {
							RelativeSource = RelativeSource.Self,
							Converter = new BooleanToVisibilityConverter()
						});
				}
			};
			t.SizeChanged += h;
			//note: in Loaded event handler randomly does not work. Somehow t still does not have the overflow button.
		}
	}
	
	/// <summary>
	/// Clicks the button.
	/// Uses <see cref="IInvokeProvider.Invoke"/>.
	/// </summary>
	/// <exception cref="ElementNotEnabledException"></exception>
	public static void UiaClick(this Button t) { //tested: does not work with CheckBox
		if (UIElementAutomationPeer.CreatePeerForElement(t)?.GetPattern(PatternInterface.Invoke) is IInvokeProvider ip) ip.Invoke();
	}
	
	/// <summary>
	/// Shows the window in [preview mode](xref:code_editor).
	/// </summary>
	/// <param name="t"></param>
	/// <exception cref="InvalidOperationException">Called not in preview mode.</exception>
	/// <remarks>
	/// Changes some window properties (owner window, location, activation, etc), terminates previous preview process, calls <b>ShowDialog</b>. If closed, calls <b>Environment.Exit</b>.
	/// 
	/// If called not in preview mode, calls <b>Environment.Exit</b>.
	/// </remarks>
	public static void Preview(this Window t) {
		wnd wMain = ScriptEditor.MainWindow(); if (wMain.Is0) Environment.Exit(0);
		if (!Environment.CommandLine.RxMatch(@" WPF_PREVIEW (-?\d+) (-?\d+)$", out var m)) Environment.Exit(0);
		int pid = m[1].Value.ToInt();
		m[2].Value.ToInt(out long time);
		
		t.Title = "WPF preview";
		t.ShowActivated = false;
		t.WindowStartupLocation = WindowStartupLocation.Manual;
		t.WindowState = WindowState.Normal;
		t.ShowInTaskbar = true;
		t.Topmost = true;
		
		t.Loaded += (_, _) => {
			var w = t.Hwnd();
			//unsafe { int BOOL = 1; Api.DwmSetWindowAttribute(w, Api.DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, &BOOL, 0); } //does not disable the inflate/deflate animation; and don't need, with it even better
			
			//move to App.Settings.wpfpreview_xy or to the right side of the primary screen
			if ((int)ScriptEditor.WndMsg_.Send(Api.WM_USER, 3) is int xy && xy != 0) {
				var p = Math2.NintToPOINT(xy);
				w.MoveL(p.x, p.y);
				w.EnsureInScreen();
			} else {
				w.MoveInScreen(^1, .5f);
			}
			
			//_TerminatePrevious(); pid = 0; //async less flickering, especially when no animations, eg toolwindow
			t.Dispatcher.InvokeAsync(() => { _TerminatePrevious(); pid = 0; }, DispatcherPriority.ApplicationIdle);
			
			//rejected. See the commented out workaround below. Instead set Topmost = true and ShowInTaskbar = true.
			//	The workaround may not always work, eg for other windows.
			//	Also noticed other anomalies, eg in some cases OS activates wrong window when this window closed.
			//if (!WndUtil.SetOwnerWindow(w, wMain)) w.ZorderTopmost();
			
			var hs = PresentationSource.FromVisual(t) as HwndSource;
			hs.AddHook(_WndProc);
			nint _WndProc(nint hwnd, int msg, nint wp, nint lp, ref bool handled) {
				var w = (wnd)hwnd;
				switch (msg) {
				//case Api.WM_NCLBUTTONDOWN or Api.WM_NCRBUTTONDOWN: //the window is already active
				//case Api.WM_LBUTTONDOWN or Api.WM_RBUTTONDOWN or Api.WM_MBUTTONDOWN: //for checkboxes etc
				//	Task.Run(() => { var p = mouse.xy; Api.SetCursorPos(p.x, p.y); });
				//	//workaround for: if using SetOwnerWindow:
				//	//	When the window is inactive, on click nonclient (eg to close), the window hangs until mouse moved.
				//	//	Similar happens after clicking a checkbox ~30 times. Maybe other controls too.
				//	break;
				case Api.WM_EXITSIZEMOVE:
					//save wpfpreview_xy
					if (!(w.IsMinimized || w.IsMaximized)) {
						var r = w.Rect; if (r.left == 0 && r.top == 0) r.top++;
						ScriptEditor.WndMsg_.Send(Api.WM_USER, 4, Math2.MakeLparam(r.left, r.top));
					}
					break;
				}
				return 0;
			}
		};
		
		try {
			t.ShowDialog();
		}
		finally {
			_TerminatePrevious();
			Environment.Exit(0);
		}
		
		void _TerminatePrevious() {
			if (pid == 0) return;
			if (process.getTimes(pid, out long tc, out _) && tc <= time) {
				//print.it("terminate", pid, time, tc);
				process.terminate(pid);
			} else {
				//print.it("bad");
				//if previous task failed before calling this func, this wasn't called and therefore an even older task may be running. Close its window.
				foreach (var w in wnd.findAll("WPF preview", "HwndWrapper[*", "Au.Task.exe")) {
					if (!w.IsOfThisProcess) w.Close(noWait: true);
				}
			}
		}
	}
}
