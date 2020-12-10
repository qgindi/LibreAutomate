using Au.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
//using System.Linq;

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Au
{
	/// <summary>
	/// Adds extension methods for some WPF classes.
	/// </summary>
	public static class AExtWpf
	{
		/// <summary>
		/// Gets native window handle of this <b>Window</b> or <b>Popup</b>, or container window handle of this child object.
		/// Returns <c>default(AWnd)</c> if: called before creating or after closing real window; failed; <i>t</i> is null.
		/// </summary>
		/// <param name="t"></param>
		public static AWnd Hwnd(this DependencyObject t) {
			switch (t) {
			case null: return default;
			case Window w: return (AWnd)new WindowInteropHelper(w).Handle; //FromDependencyObject works too, but this is usually slightly faster
			case Popup p: t = p.Child; if (t == null) return default; break; //FromVisual(Popup) returns null, FromDependencyObject too
			}
			if (PresentationSource.FromDependencyObject(t) is HwndSource hs) return (AWnd)hs.Handle;
			return default;
		}
		//rejected: notPopup. Not useful.
		///// <summary>
		///// Gets window handle of this <b>Window</b>, <b>Popup</b> or container window handle of this child object.
		///// Returns <c>default(AWnd)</c> if: called before creating real window; failed; <i>t</i> is null.
		///// </summary>
		///// <param name="t"></param>
		///// <param name="notPopup">If this is <b>Popup</b> or in a <b>Popup</b>, get handle of popup's owner <b>Window</b>.</param>
		//public static AWnd Hwnd(this DependencyObject t, bool notPopup = false)
		//{
		//	switch(t) {
		//	case null: return default;
		//	case Window w: return (AWnd)new WindowInteropHelper(w).Handle; //FromDependencyObject works too, but this is usually slightly faster
		//	case Popup p when !notPopup: t = p.Child; if(t == null) return default; break; //FromVisual(Popup) returns null; or maybe owner window, not tested.
		//	}
		//	if(notPopup) {
		//		var w = Window.GetWindow(t); if(w == null) return default; //if Popup or in Popup, gets owner WIndow
		//		return (AWnd)new WindowInteropHelper(w).Handle;
		//	}
		//	if(PresentationSource.FromDependencyObject(t) is HwndSource hs) return (AWnd)hs.Handle;
		//	return default;
		//}

		/// <summary>
		/// Enumerates visual descendant objects, including parts of composite controls, and calls callback function <i>f</i> for each.
		/// When <i>f</i> returns true, stops and returns that object. Returns null if <i>f</i> does not return true.
		/// </summary>
		public static DependencyObject FindVisualDescendant(this DependencyObject t, Func<DependencyObject, bool> f) {
			for (int i = 0, n = VisualTreeHelper.GetChildrenCount(t); i < n; i++) {
				var v = VisualTreeHelper.GetChild(t, i);
				if (f(v)) return v;
				v = FindVisualDescendant(v, f);
				if (v != null) return v;
			}
			return null;
		}

		/// <summary>
		/// Enumerates visual descendant objects, including parts of composite controls, and calls callback function <i>f</i> for each.
		/// When <i>f</i> returns true, stops and returns that object. Returns null if <i>f</i> does not return true.
		/// </summary>
		public static IEnumerable<DependencyObject> VisualDescendants(this DependencyObject t) {
			for (int i = 0, n = VisualTreeHelper.GetChildrenCount(t); i < n; i++) {
				var v = VisualTreeHelper.GetChild(t, i);
				yield return v;
				foreach (var k in VisualDescendants(v)) yield return k;
				//SHOULDDO: now creates much garbage if tree is big.
				//	See https://stackoverflow.com/a/30441479/2547338.
				//	See AExt.Descendants. But it cannot be used here because VisualTreeHelper does not give an IEnumerable.
			}
		}

		/// <summary>
		/// Enumerates visual descendant objects, including parts of composite controls, and calls callback function <i>f</i> for each.
		/// When <i>f</i> returns true, stops and returns that object. Returns null if <i>f</i> does not return true.
		/// </summary>
		public static System.Collections.IEnumerable LogicalDescendants(this DependencyObject t) {
			//foreach (var v in LogicalTreeHelper.GetChildren(t)) {
			//	yield return v;
			//	if (v is DependencyObject d)
			//		foreach (var k in LogicalDescendants(d)) yield return k;
			//}
			return LogicalTreeHelper.GetChildren(t).Descendants_(o => o is DependencyObject d ? LogicalTreeHelper.GetChildren(d) : null);
		}

		/// <summary>
		/// Finds object in chain of visual tree objects that includes <see cref="RoutedEventArgs.Source"/> and its ancestors until <i>sender</i> (including).
		/// </summary>
		/// <param name="t">The event arguments parameter of an event handler.</param>
		/// <param name="sender">The <i>sender</i> parameter of the event handler.</param>
		/// <param name="f">Function that returns true for the wanted object.</param>
		public static DependencyObject SourceOrAncestor(this RoutedEventArgs t, object sender, Func<DependencyObject, bool> f) {
			for (var v = t.Source as DependencyObject; v != null; v = VisualTreeHelper.GetParent(v)) {
				if (f(v)) return v;
				if (v == sender) break;
			}
			return null;
		}

		/// <summary>
		/// Finds object of type T (or inherited) in chain of visual tree objects that includes <see cref="RoutedEventArgs.Source"/> and its ancestors until <i>sender</i> (including).
		/// </summary>
		/// <param name="t">The event arguments parameter of an event handler.</param>
		/// <param name="sender">The <i>sender</i> parameter of the event handler.</param>
		/// <param name="orSender">If object of type T not found, return <i>sender</i>. If false (default), returns null.</param>
		public static DependencyObject SourceOfType<T>(this RoutedEventArgs t, object sender, bool orSender = false) where T : DependencyObject {
			for (var v = t.Source as DependencyObject; v != null; v = VisualTreeHelper.GetParent(v)) {
				if (v is T r1) return r1;
				if (v == sender) return orSender ? v : null;
			}
			return null;
		}

		/// <summary>
		/// Finds object of type T1 or T2 (or inherited) in chain of visual tree objects that includes <see cref="RoutedEventArgs.Source"/> and its ancestors until <i>sender</i> (including).
		/// </summary>
		/// <param name="t">The event arguments parameter of an event handler.</param>
		/// <param name="sender">The <i>sender</i> parameter of the event handler.</param>
		/// <param name="orSender">If object of type T1 or T2 not found, return <i>sender</i>. If false (default), returns null.</param>
		public static DependencyObject SourceOfType<T1, T2>(this RoutedEventArgs t, object sender, bool orSender = false) where T1 : DependencyObject where T2 : DependencyObject {
			for (var v = t.Source as DependencyObject; v != null; v = VisualTreeHelper.GetParent(v)) {
				if (v is T1 r1) return r1;
				if (v is T2 r2) return r2;
				if (v == sender) return orSender ? v : null;
			}
			return null;
		}

		/// <summary>
		/// Gets rectangle of this element in screen coordinates.
		/// </summary>
		public static RECT RectInScreen(this FrameworkElement t) {
			if (t is Window w) return w.Hwnd().Rect; //else would be incorrect: x/y of client area, width/height of window
			Point p1 = t.PointToScreen(default), p2 = t.PointToScreen(new Point(t.ActualWidth, t.ActualHeight));
			return new RECT(p1.X.ToInt(), p1.Y.ToInt(), p2.X.ToInt(), p2.Y.ToInt(), false);
		}

		/// <summary>
		/// Returns true if <see cref="ToggleButton.IsChecked"/> == true.
		/// </summary>
		/// <param name="t"></param>
		public static bool IsCheck(this CheckBox t) => t.IsChecked.GetValueOrDefault();

#if true
		static unsafe void _Move(Window t, int x, int y, in RECT r, bool andSize) {
			var wstate = t.WindowState;
			if (t.IsLoaded) {
				var w = t.Hwnd();
				if (w.Is0) throw new ObjectDisposedException("Window");
				if (wstate != WindowState.Normal) t.WindowState = WindowState.Normal;
				if (andSize) w.MoveLL(r); else w.MoveLL(x, y);
			} else {
				//tested: don't need this for Popup. Its PlacementRectangle uses physical pixels.

				t.WindowStartupLocation = WindowStartupLocation.Manual;
				if (wstate == WindowState.Minimized) t.ShowActivated = false;

				t_cbt ??= AHookWin.ThreadCbt(k => {
					if (k.code == HookData.CbtEvent.CREATEWND) {
						var c = k.CreationInfo->lpcs;
						if (!c->style.Has(WS.CHILD)) {
							var name = c->Name;
							if (name.Length > 25 && name.StartsWith("m8KFOuCJOUmjziONcXEi3A ")) {
								var s = name[23..].ToString();
								if (name[^1] == ';') {
									c->x = s.ToInt(0, out int e); c->y = s.ToInt(e);
								} else if (RECT.TryParse(s, out var r)) {
									c->x = r.left; c->y = r.top; c->cx = r.Width; c->cy = r.Height;
								}
							}
						}
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
		[ThreadStatic] static AHookWin t_cbt;
#elif true //does not change Title, but I don't like creating window handle before showing window
		static void _Move(Window t, int x, int y, in RECT r, bool andSize) {
			var wstate=t.WindowState;
			if(wstate!=WindowState.Normal) t.WindowState=WindowState.Normal;
			if(t.IsLoaded) {
				var w=t.Hwnd();
				if(w.Is0) throw new ObjectDisposedException("Window");
				if(andSize) w.MoveLL(r); else w.MoveLL(x, y);
			} else {
				var screen=AScreen.Of(new POINT(x, y));
				var si=screen.GetInfo();
				var rs=si.workArea;
			
				if(andSize) {
					x=r.left; y=r.top;
					var stc=t.SizeToContent;
					if(stc!=SizeToContent.WidthAndHeight) {
						double f=96d/screen.Dpi;
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
					w.MoveLL(x, y);
				
					if(wstate!=WindowState.Normal) {
						if(wstate==WindowState.Maximized) t.SizeToContent=SizeToContent.Manual;
						t.WindowState=wstate;
					} else if(!t.ShowActivated) {
						w.ZorderTop();
					}
				};
			
				if(!si.isPrimary) {
					using var h=AHookWin.ThreadCbt(d=> {
						if(d.code== HookData.CbtEvent.CREATEWND) unsafe {
							var w=d.CreationInfo(out var c, out _);
							if(c->style!=0 && !c->style.Has(WS.CHILD)) { //note: this does not work if ShowInTaskbar = false. Then WPF creates a "Hidden Window" before, even if owner specified.
								AOutput.Write(c->x, c->y, c->cx, c->cy, c->hwndParent, c->style, c->lpszClass, c->lpszName);
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
				if (andSize) w.MoveLL(r); else w.MoveLL(x, y);
			} else {
				//tested: don't need this for Popup. Its PlacementRectangle uses physical pixels.

				if (andSize) {
					x = r.left; y = r.top;
					var stc = t.SizeToContent;
					if (stc != SizeToContent.WidthAndHeight) {
						double f = 96d / AScreen.Of(x, y).Dpi;
						if (!stc.Has(SizeToContent.Width)) t.Width = r.Width * f;
						if (!stc.Has(SizeToContent.Height)) t.Height = r.Height * f;
					}
				}

				t.WindowStartupLocation = WindowStartupLocation.Manual;
				t.Left = double.NaN; t.Top = double.NaN; //default location, somewhere near top-left of primary screen or owner's screen
				if (wstate == WindowState.Minimized) t.ShowActivated = false;

				t.SourceInitialized += (_, _) => {
					var w = t.Hwnd();
					var v = AScreen.Of(x, y).Info;
					var rs = v.workArea;
					if (!v.isPrimary) {
						using var h = AHookWin.ThreadCbt(k => k.code == HookData.CbtEvent.ACTIVATE); //workaround for WPF bug: activates window when DPI changes
						w.MoveLL(rs.left, rs.top); //let DPI-scale
					}
					var rw = w.Rect;
					x = Math.Clamp(x, rs.left, Math.Max(rs.right - rw.Width, rs.left));
					y = Math.Clamp(y, rs.top, Math.Max(rs.bottom - rw.Height, rs.top));
					w.MoveLL(x, y);
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
		/// The unit is physical pixels. WPF provides <b>Left</b> and <b>Top</b> properties, but the unit is logical pixels, therefore cannot set exact location on high DPI screens, especially if there are mutiple screens with different DPI.
		/// 
		/// If the window is already loaded, just ensures it is not maximized/minimized and calls <see cref="AWnd.MoveLL"/>.
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
		/// The unit is physical pixels. WPF provides <b>Left</b>, <b>Top</b>, <b>Width</b> and <b>Height</b> properties, but the unit is logical pixels, therefore cannot set exact rectangle on high DPI screens, especially if there are mutiple screens with different DPI.
		/// 
		/// If the window is already loaded, just ensures it is not maximized/minimized and calls <see cref="AWnd.MoveLL"/>.
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
		public static void AddChild(this Grid g, UIElement e, int row, int column) {
			Grid.SetRow(e, row);
			Grid.SetColumn(e, column);
			g.Children.Add(e);
		}

		/// <summary>
		/// Workaround for WPF bug: on DPI change tries to activate window.
		/// Call on WM_DPICHANED message or in OnDpiChanged override.
		/// </summary>
		public static void DpiChangedWorkaround(this Window t) => _DCW(t.Dispatcher, t.Hwnd());

		/// <summary>
		/// Workaround for WPF bug: on DPI change tries to activate window.
		/// Call on WM_DPICHANED message or in OnDpiChanged override. Only if top-level window.
		/// </summary>
		public static void DpiChangedWorkaround(this HwndSource t) => _DCW(t.Dispatcher, (AWnd)t.Handle);

		static void _DCW(Dispatcher d, AWnd w) {
			if (AWnd.Active != w) {
				bool wasVisible = w.IsVisible; //allow to activate when opening window in non-primary screen
				var h = AHookWin.ThreadCbt(k => k.code == HookData.CbtEvent.ACTIVATE && (AWnd)k.wParam == w && (wasVisible || !w.IsVisible));
				d.InvokeAsync(() => h.Dispose());
			}
		}
	}
}

//namespace Au.Types
//{
//	///
//	public enum EnsureIn
//	{
//		///
//		None,
//		///
//		Screen,
//		///
//		WorkArea
//	}
//}