using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Au.Controls;

/// <summary>
/// Based on WPF <see cref="ContextMenu"/>, makes simpler to use it.
/// </summary>
/// <example>
/// <code><![CDATA[
/// var m = new KWpfMenu();
/// m["One"] = o => print.it(o);
/// using(m.Submenu("Sub")) {
/// 	m["Three"] = o => print.it(o);
/// 	m["Four"] = o => print.it(o);
/// }
/// m.Separator();
/// m["Two"] = o => { print.it(o); };
/// m.Show(this); //or m.IsOpen=true;
/// ]]></code>
/// </example>
public class KWpfMenu : ContextMenu
{
	///
	public KWpfMenu() {
	}

	/// <summary>
	/// Creates new <see cref="MenuItem"/> and adds to the menu. Returns it.
	/// </summary>
	/// <param name="text">
	/// Label. See <see cref="HeaderedItemsControl.Header"/>.
	/// If contains '\0' character, uses text before it for label and text after it for <see cref="MenuItem.InputGestureText"/>; example: "Text\0" + "Ctrl+E".
	/// </param>
	/// <param name="click">Action called on click.</param>
	/// <param name="icon">See <see cref="this[string, bool, object]"/>.</param>
	/// <remarks>
	/// Usually it's easier to use the indexer instead. It just calls this function. See example.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// m["Example"] = o => print.it(o);
	/// m.Last.IsChecked = true;
	/// ]]></code>
	/// </example>
	public MenuItem Add(object text, Action<WpfMenuActionArgs> click = null, object icon = null) {
		string gest = null;
		if (text is string s && s.Contains('\0')) {
			int j = s.IndexOf('\0');
			text = s[0..j];
			gest = s[++j..];
		}
		var item = new _MenuItem(this) {
			action = click,
			actionException = ActionException,
			Header = text
		};
		if (gest != null) item.InputGestureText = gest;
		item.Icon = MenuItemIcon_(icon);
		CurrentAddMenu.Items.Add(Last = item);
		ItemAdded?.Invoke(item);
		return item;
	}

	/// <summary>
	/// Creates new <see cref="MenuItem"/> and adds to the menu.
	/// </summary>
	/// <param name="text">
	/// Label. See <see cref="HeaderedItemsControl.Header"/>.
	/// If contains '\0' character, uses text before it for label and text after it for <see cref="MenuItem.InputGestureText"/>; example: "Text\0" + "Ctrl+E".
	/// </param>
	/// <param name="enabled">Disabled if false. Default true.</param>
	/// <param name="icon">
	/// Can be:
	/// - <see cref="Image"/> or other WPF control to assign directly to <see cref="MenuItem.Icon"/>.
	/// - <see cref="ImageSource"/> - a WPF image. To create image from icon, use <see cref="icon.ToWpfImage"/>.
	/// - string - image file path, or resource path that starts with "resources/" or has prefix "resource:", or Base64 encoded image with prefix "image:". Can be XAML file or resource. See <see cref="ImageUtil.LoadWpfImageElement"/>. Supports environment variables. If not full path, looks in <see cref="folders.ThisAppImages"/>.
	/// - <see cref="Uri"/> - image file path, or resource pack URI, or URL. Does not support environment variables and <see cref="folders.ThisAppImages"/>.
	/// 
	/// If failed to find or load image file, prints warning (<see cref="print.warning"/>).
	/// To create Base64 string, use menu Code > Find image.
	/// To add an image resource in Visual Studio, use build action "Resource" for the image file.
	/// </param>
	/// <value>Action called on click.</value>
	/// <remarks>
	/// Calls <see cref="Add(object, Action{WpfMenuActionArgs}, object)"/>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// m["Example"] = o => print.it(o);
	/// m.Last.IsChecked = true;
	/// ]]></code>
	/// </example>
	public Action<WpfMenuActionArgs> this[string text, bool enabled = true, object icon = null] {
		set {
			var v = Add(text, value, icon);
			if (!enabled) v.IsEnabled = false;
		}
	}

	/// <summary>
	/// Adds separator.
	/// </summary>
	public void Separator() { CurrentAddMenu.Items.Add(new Separator()); }

	/// <summary>
	/// Creates new <see cref="MenuItem"/> for a submenu and adds to the menu.
	/// </summary>
	/// <param name="text">Label. See <see cref="HeaderedItemsControl.Header"/>.</param>
	/// <param name="icon"><see cref="MenuItem.Icon"/>.</param>
	/// <param name="click">Action called on click. Rarely used.</param>
	/// <remarks>
	/// Then the add-item functions will add items to the submenu, until the returned variable is disposed.
	/// </remarks>
	/// <example><see cref="KWpfMenu"/></example>
	public UsingEndAction Submenu(object text, object icon = null, Action<WpfMenuActionArgs> click = null) {
		var mi = Add(text, click, icon);
		_submenuStack.Push(mi);
		return new UsingEndAction(() => _submenuStack.Pop());
		//CONSIDER: copy some properties of current menu. Or maybe WPF copies automatically, need to test.
	}

	Stack<MenuItem> _submenuStack = new();
	//bool _AddingSubmenuItems => _submenuStack.Count > 0;

	/// <summary>
	/// Gets <see cref="ItemsControl"/> of the menu or submenu where new items currently would be added.
	/// </summary>
	public ItemsControl CurrentAddMenu => _submenuStack.Count > 0 ? _submenuStack.Peek() : this;

	/// <summary>
	/// Gets the last added <see cref="MenuItem"/>.
	/// </summary>
	public MenuItem Last { get; private set; }

	/// <summary>
	/// Called when added a non-separator item.
	/// </summary>
	public Action<MenuItem> ItemAdded { get; set; }

	/// <summary>
	/// Whether to handle exceptions in item action code. If false (default), handles exceptions and on exception calls <see cref="print.warning"/>.
	/// Applied to menu items added afterwards.
	/// </summary>
	public bool ActionException { get; set; }

	/// <summary>
	/// Sets <see cref="ContextMenu.PlacementTarget"/> = <i>owner</i> and <see cref="ContextMenu.IsOpen"/> = true.
	/// </summary>
	/// <param name="owner"><see cref="ContextMenu.PlacementTarget"/>. The menu uses its DPI. If null, uses DPI of primary screen (WPF bug).</param>
	/// <param name="byCaret">Show by caret (text cursor) position if possible.</param>
	/// <param name="modal">Wait until closed.</param>
	public void Show(UIElement owner, bool byCaret = false, bool modal = false) {
		if (byCaret && miscInfo.getTextCursorRect(out RECT cr, out _)) {
			var r = owner == null ? cr : new Rect(owner.PointFromScreen(new Point(cr.left, cr.top)), owner.PointFromScreen(new Point(cr.right, cr.bottom)));
			r.Inflate(30, 2);
			PlacementRectangle = r;
			Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		}
		PlacementTarget = owner;

		//tested: VisualTreeHelper.SetRootDpi does not work.

		//workaround for: if focused is a native control in a HwndHost, it remains focused, and menu keyboard does not work normally.
		//	Temporarily remove native focus from the control.
		//	Also tried to redirect key messages with a hook, but it does not work for arrow keys.
		//	Also tried to remove focus in OnOpened, but it closes the menu.
		//	Never mind: hides caret. In notepad etc menus don't hide caret. But eg in VS hide too.
		if (owner is HwndHost hh && hh.IsFocused && Api.GetFocus() == (wnd)hh.Handle && FocusManager.GetFocusScope(hh) is UIElement fs) {
			fs.Focus();
		}

		IsOpen = true;

		if (modal) {
			_dispFrame = new DispatcherFrame();
			Dispatcher.PushFrame(_dispFrame);
		}
	}
	DispatcherFrame _dispFrame;

	void _EndModal() {
		if (_dispFrame != null) {
			_dispFrame.Continue = false;
			_dispFrame = null;
		}
	}

	///
	protected override void OnClosed(RoutedEventArgs e) {
		_EndModal();
		base.OnClosed(e);
	}

	internal static object MenuItemIcon_(object icon) {
		if (icon != null) {
			try {
				ImageSource iso = null;
				switch (icon) {
				case string s:
					return ImageUtil.LoadWpfImageElement(s);
				case Uri s:
					iso = BitmapFrame.Create(s);
					break;
				case ImageSource s:
					iso = s;
					break;
				default:
					return icon;
				}
				if (iso != null) return new Image { Source = iso };
			}
			catch (Exception ex) { print.warning("Failed to load menu item image. " + ex.ToStringWithoutStack()); }
		}
		return null;
		//rejected: cache, like popupMenu.
	}

	/// <summary>
	/// Creates and shows popup menu where items use ids instead of actions.
	/// Returns selected item id, or 0 if canceled.
	/// </summary>
	/// <param name="items">
	/// Menu items. Can be string[], List&lt;string&gt; or string like "One|Two|Three".
	/// Item id can be optionally specified like "1 One|2 Two|3 Three". If missing, uses id of previous non-separator item + 1. Example: "One|Two|100 Three Four" //1|2|100|101.
	/// For separators use null or empty strings: "One|Two||Three|Four".
	/// </param>
	/// <param name="owner"><see cref="ContextMenu.PlacementTarget"/>. The menu uses its DPI. If null, uses DPI of primary screen (WPF bug).</param>
	/// <param name="byCaret">Show by caret (text cursor) position if possible.</param>
	/// <param name="beforeShow">Called after adding menu items, before showing the menu. For example can set placement properties.</param>
	/// <remarks>
	/// The menu is modal; the function returns when closed.
	/// </remarks>
	/// <seealso cref="dialog.showList"/>
	public static int ShowSimple(Strings items, UIElement owner, bool byCaret = false, Action<KWpfMenu> beforeShow = null) {
		var a = items.ToArray();
		var m = new KWpfMenu();
		//	var dispFrame = new DispatcherFrame();
		//	m.Closed+=(_,_)=>dispFrame.Continue=false;
		int result = 0, autoId = 0;
		foreach (var v in a) {
			var s = v;
			if (s.NE()) {
				m.Separator();
			} else {
				if (s.ToInt(out int id, 0, out int end)) {
					if (s.Eq(end, ' ')) end++;
					s = s[end..];
					autoId = id;
				} else {
					id = ++autoId;
				}
				m.Add(s, o => result = (int)o.Item.Tag).Tag = id;
				//			m.Add(s, o => {
				//				result = (int)o.Item.Tag;
				//				dispFrame.Continue=false;
				//			}).Tag = id;
			}
		}
		beforeShow?.Invoke(m);
		m.Show(owner, byCaret: byCaret, modal: true);
		//	m.IsOpen=true;
		//	Dispatcher.PushFrame(dispFrame);
		return result;
	}

	class _MenuItem : MenuItem
	{
		KWpfMenu _m;
		public Action<WpfMenuActionArgs> action;
		public bool actionException;

		public _MenuItem(KWpfMenu m) { _m = m; }

		protected override void OnClick() {
			base.OnClick(); //must be first, because changes IsChecked (if IsCheckable)

			_m._EndModal(); //workaround for: OnClosed called with 160 ms delay. Same with native message loop.
			if (action != null) {
				try { action(new WpfMenuActionArgs(this)); }
				catch (Exception ex) when (!actionException) { print.warning(ex); }
			}
		}

		protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
			switch (e.ChangedButton) {
			//case MouseButton.Right:
			//	if (this.HasItems && e.Source != this) break;
			//	e.Handled = true;
			//	break;
			case MouseButton.Middle:
				_m.IsOpen = false;
				break;
			}
			base.OnPreviewMouseUp(e);
		}
	}
}
//}

//namespace Au.Types
//{
/// <summary>
/// Arguments for <see cref="KWpfMenu"/> item actions.
/// </summary>
public class WpfMenuActionArgs
{
	///
	public WpfMenuActionArgs(MenuItem item) { Item = item; }

	/// <summary>
	/// The menu item object.
	/// </summary>
	public MenuItem Item { get; }

	///
	public override string ToString() {
		var d = Item.Dispatcher;
		if (d.Thread == Thread.CurrentThread) return Item.Header.ToString();
		return d.Invoke(() => Item.Header.ToString());
	}
}