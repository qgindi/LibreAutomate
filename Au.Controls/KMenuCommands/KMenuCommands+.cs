using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace Au.Controls;

public partial class KMenuCommands {
	/// <summary>
	/// Contains a method delegate and a menu item that executes it. Implements <see cref="ICommand"/> and can have one or more attached buttons etc and key/mouse shortcuts that execute it. All can be disabled/enabled with single function call.
	/// Also used for submenu-items (created from nested types); it allows for example to enable/disable all descendants with single function call.
	/// </summary>
	public class Command : ICommand {
		readonly KMenuCommands _mc;
		readonly Delegate _del; //null if submenu
		readonly CommandAttribute _ca;
		MenuItem _mi;
		bool _enabled;
		
		internal Command(KMenuCommands mc, string name, string text, MemberInfo mi, CommandAttribute ca) {
			_mc = mc;
			_enabled = true;
			Name = name;
			Text = text;
			_ca = ca;
			Keys = ca.keys;
			if (mi is MethodInfo k) _del = k.CreateDelegate(k.GetParameters().Length == 0 ? typeof(Action) : typeof(Action<MenuItem>));
			//if (mi is MethodInfo k) _del = k.CreateDelegate(k.GetParameters().Length == 0 ? typeof(Action) : typeof(Action<object>));
		}
		
		internal void SetMenuItem_(object text, string image, MenuItem miFactory = null) {
			_mi = miFactory ?? new MenuItem { Header = text }; //factory action may have set it
			_mi.Command = this;
			if (image != null && miFactory?.Icon == null) _SetImage(image);
		}
		
		MenuItem _Mi => _mi ?? throw new InvalidOperationException("Call FactoryParams.SetMenuItem before.");
		
		///
		public MenuItem MenuItem => _mi;
		
		/// <summary>
		/// true if this is a submenu-item.
		/// </summary>
		public bool IsSubmenu => _del == null;
		
		/// <summary>
		/// Method name. If submenu-item - type name.
		/// Or <see cref="CommandAttribute.name"/>.
		/// May have prefix <see cref="CommandAttribute.namePrefix"/>.
		/// </summary>
		public string Name { get; }
		
		///
		public override string ToString() => Name;
		
		public CommandAttribute Attribute => _ca;
		
		/// <summary>
		/// Menu item text. Also button text/tooltip if not set.
		/// </summary>
		public string Text { get; }
		
		/// <summary>
		/// Button text or tooltip. Same as menu item text but without _ for Alt-underline.
		/// </summary>
		public string ButtonText { get; set; }
		
		/// <summary>
		/// <see cref="CommandAttribute.tooltip"/>.
		/// </summary>
		public string ButtonTooltip { get; set; }
		
		/// <summary>
		/// Default or custom hotkey etc.
		/// </summary>
		public string Keys { get; private set; }
		
		/// <summary>
		/// Setter subscribes to <see cref="MenuItem.SubmenuOpened"/> event.
		/// Will propagate to copied submenus.
		/// Call once.
		/// </summary>
		public RoutedEventHandler SubmenuOpened {
			get => _submenuOpened;
			set {
				Debug.Assert(_submenuOpened == null);
				_Mi.SubmenuOpened += _submenuOpened = value;
			}
		}
		RoutedEventHandler _submenuOpened;
		
		/// <summary>
		/// Something to attach to this object. Not used by this class.
		/// </summary>
		public object Tag { get; set; }
		
		/// <summary>
		/// Sets properties of a button to match properties of this menu item.
		/// </summary>
		/// <param name="b">Button or checkbox etc.</param>
		/// <param name="imageAt">If menu item has image, set <b>Content</b> = <b>DockPanel</b> with image and text and dock image at this side. If null (default), sets image without text. Not used if there is no image.</param>
		/// <param name="image">Button image element, if different than menu item image. Must not be a child of something.</param>
		/// <param name="text">Button text, if different than menu item text.</param>
		/// <param name="skipImage">Don't change image.</param>
		/// <exception cref="InvalidOperationException">This is a submenu. Or called from factory action before <see cref="FactoryParams.SetMenuItem"/>.</exception>
		/// <remarks>
		/// Sets these properties:
		/// - <b>Content</b> (image or/and text),
		/// - <b>ToolTip</b>,
		/// - <b>Foreground</b>,
		/// - <b>Command</b> (to execute same method and automatically enable/disable together),
		/// - Automation Name (if with image),
		/// - if checkable, synchronizes checked state (the button should be a ToggleButton (CheckBox or RadioButton)).
		/// </remarks>
		public void CopyToButton(ButtonBase b, Dock? imageAt = null, UIElement image = null, string text = null, bool skipImage = false) {
			if (IsSubmenu) throw new InvalidOperationException("Submenu. Use CopyToMenu.");
			_ = _Mi;
			text ??= ButtonText;
			
			if (skipImage) {
				switch (b.Content) {
				case DockPanel dp: image = dp.Children[0]; dp.Children.Clear(); break;
				case UIElement ue: image = ue; break;
				default: image = null; break;
				}
				b.Content = null;
			} else {
				image ??= CopyImage();
			}
			
			if (image == null) {
				b.Content = text;
				b.Padding = new Thickness(4, 1, 4, 2);
				_SetTooltip(ButtonTooltip);
			} else if (imageAt != null) {
				var v = new DockPanel();
				var dock = imageAt.Value;
				DockPanel.SetDock(image, dock);
				v.Children.Add(image);
				var t = new TextBlock { Text = text };
				if (dock == Dock.Left || dock == Dock.Right) t.Margin = new Thickness(2, -1, 2, 1);
				v.Children.Add(t);
				b.Content = v;
				_SetTooltip(ButtonTooltip);
			} else { //only image
				b.Content = image;
				_SetTooltip(ButtonTooltip ?? text);
			}
			b.Foreground = _mi.Foreground;
			if (image != null && !text.NE()) System.Windows.Automation.AutomationProperties.SetName(b, text);
			b.Command = this;
			
			if (_mi.IsCheckable) {
				if (b is ToggleButton tb) tb.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = _mi });
				else print.warning($"Menu item {Name} is checkable, but button isn't a ToggleButton (CheckBox or RadioButton).");
			}
			
			void _SetTooltip(string s) {
				string k = this.Keys, g = _mi.InputGestureText;
				if (!k.NE() || !g.NE()) {
					if (!g.NE()) k = k.NE() ? g : g + ", " + k;
					s = s.NE() ? k : $"{s}\n\n{k}";
				}
				b.ToolTip = s;
			}
		}
		
		//public void CopyToButton<T>(out T b, Dock? imageAt = null) where T : ButtonBase, new() => CopyToButton(b = new T(), imageAt);
		
		/// <summary>
		/// Sets properties of another menu item (not in this menu) to match properties of this menu item.
		/// If this is a submenu-item, copies with descendants.
		/// </summary>
		/// <param name="m"></param>
		/// <param name="image">Image element (<see cref="MenuItem.Icon"/>), if different. Must not be a child of something.</param>
		/// <param name="text">Text (<see cref="HeaderedItemsControl.Header"/>), if different.</param>
		/// <exception cref="InvalidOperationException">Called from factory action before <see cref="FactoryParams.SetMenuItem"/>.</exception>
		/// <remarks>
		/// Sets these properties:
		/// - <b>Header</b> (if string),
		/// - <b>Icon</b> (if possible),
		/// - <b>InputGestureText</b>,
		/// - <b>ToolTip</b>,
		/// - <b>Foreground</b>,
		/// - <b>Command</b> (to execute same method and automatically enable/disable together),
		/// - <b>IsCheckable</b> (and synchronizes checked state).
		/// </remarks>
		public void CopyToMenu(MenuItem m, UIElement image = null, object text = null) => _CopyToMenu(_Mi, m, image, text);
		
		static MenuItem _CopyToMenu(MenuItem from, MenuItem to, UIElement image = null, object text = null) {
			if (from.Command is not Command c) return null;
			to ??= new();
			
			to.Icon = image ?? _CopyImage(from);
			if (text != null) to.Header = text;
			else if (from.Header is string s) {
				if (to.Role is MenuItemRole.TopLevelItem) s = StringUtil.RemoveUnderlineChar(s, '_'); //eg _Find would disable normal operation of _File
				to.Header = s;
			}
			to.InputGestureText = from.InputGestureText;
			to.ToolTip = from.ToolTip;
			to.Foreground = from.Foreground;
			to.Command = c;
			
			bool checkable = from.IsCheckable;
			to.IsCheckable = checkable;
			if (checkable) to.SetBinding(MenuItem.IsCheckedProperty, new Binding("IsChecked") { Source = from });
			
			if (from.HasItems) {
				//lazy. Toolbar item submenus created now don't display hotkeys, because keyboard bindings are set afterwards.
				to.Items.Add("");
				RoutedEventHandler smo = null;
				smo = (_, _) => {
					to.SubmenuOpened -= smo;
					to.Items.Clear();
					_CopyDescendants(from, to);
				};
				to.SubmenuOpened += smo;
				
				if (c._submenuOpened != null) to.SubmenuOpened += c._submenuOpened;
			}
			
			return to;
		}
		
		static void _CopyDescendants(ItemsControl from, ItemsControl to) {
			int n = 0;
			foreach (var v in from.Items) {
				object k;
				switch (v) {
				case Separator:
					k = new Separator();
					break;
				case MenuItem g:
					k = _CopyToMenu(g, null);
					if (k == null) continue; //not Command. Added dynamically. Will add again.
					break;
				default: continue;
				}
				to.Items.Add(k);
				n++;
			}
		}
		
		/// <summary>
		/// Copies descendants of this submenu to a context menu.
		/// </summary>
		/// <exception cref="InvalidOperationException">This is not a submenu. Or called from factory action before <see cref="FactoryParams.SetMenuItem"/>.</exception>
		/// <remarks>
		/// For each new item sets the same properties as other overload.
		/// </remarks>
		public void CopyToMenu(ContextMenu cm) {
			if (!IsSubmenu) throw new InvalidOperationException("Not submenu");
			_CopyDescendants(_Mi, cm);
		}
		
		/// <summary>
		/// Copies menu item image element. Returns null if no image or cannot copy.
		/// </summary>
		/// <exception cref="InvalidOperationException">Called from factory action before <see cref="FactoryParams.SetMenuItem"/>.</exception>
		public UIElement CopyImage() => _CopyImage(_Mi);
		
		static UIElement _CopyImage(MenuItem from) {
			switch (from.Icon) {
			case Image im: return new Image { Source = im.Source };
			case UIElement e when e.Uid is string res: //see _SetImage
				if (ResourceUtil.HasResourcePrefix(res)) return ResourceUtil.GetXamlObject(res) as UIElement;
				if (res.Starts("source:")) return ImageUtil.LoadWpfImageElement(res[7..]);
				break;
			}
			return null;
		}
		
		bool _SetImage(string image, bool custom = false) {
			try {
				if (image.NE()) {
					_mi.Icon = null;
				} else {
#if DEBUG
					bool res = !(custom || image.Starts('*') || pathname.isFullPath(image));
#else
					bool res = !(custom || image.Starts('*'));
#endif
					var ie = res
						? ResourceUtil.GetWpfImageElement(image)
						: ImageUtil.LoadWpfImageElement(image);
					if (ie is not Image) ie.Uid = (res ? "resource:" : "source:") + image; //xaml source for _CopyImage
					_mi.Icon = ie;
				}
				return true;
			}
			catch (Exception ex) {
				if (custom) CustomizingError("failed to load image", ex);
				else print.it($"Failed to load image {image}. {ex.ToStringWithoutStack()}");
			}
			return false;
		}
		
		/// <summary>
		/// Gets enabled/disabled state of this command, menu item and all controls with <b>Command</b> property = this (see <see cref="CopyToButton"/>, <see cref="CopyToMenu"/>).
		/// </summary>
		public bool Enabled => _enabled;
		
		/// <summary>
		/// Sets enabled/disabled state of this command, menu item and all controls with <b>Command</b> property = this (see <see cref="CopyToButton"/>, <see cref="CopyToMenu"/>).
		/// If submenu-item, also enables/disables all descendants. Does not actually disable/enable the submenu-item.
		/// </summary>
		/// <exception cref="InvalidOperationException">Called from factory action before <see cref="FactoryParams.SetMenuItem"/>.</exception>
		public void Enable(bool enable) {
			_ = _Mi;
			if (enable == _enabled) return;
			_enabled = enable;
			if (IsSubmenu) {
				foreach (var v in _mi.Items) if (v is MenuItem m && m.Command is Command c && !c.NoIndirectDisable) c.Enable(enable);
			} else {
				CanExecuteChanged?.Invoke(this, EventArgs.Empty); //enables/disables this menu item and all buttons etc with Command=this
			}
		}
		
		/// <summary>
		/// Don't change the enabled state indirectly when changing that of the parent menu.
		/// </summary>
		public bool NoIndirectDisable { get; set; }
		
		/// <summary>
		/// Gets or sets checked state of this checkable menu item and all checkable controls with <b>Command</b> property = this (see <see cref="CopyToButton"/>, <see cref="CopyToMenu"/>).
		/// </summary>
		/// <exception cref="InvalidOperationException">Called from factory action before <see cref="FactoryParams.SetMenuItem"/>.</exception>
		public bool Checked {
			get => _Mi.IsChecked;
			set { if (value != _Mi.IsChecked) _mi.IsChecked = value; }
		}
		
		#region ICommand
		
		public bool CanExecute(object parameter) => _enabled;
		
		public void Execute(object parameter) {
			_mc.ExecutingStartedEnded?.Invoke(true);
			try {
				switch (_del) {
				case Action a0: a0(); break;
				case Action<MenuItem> a1: a1(_mi); break;
					//case Action<object> a1: a1(parameter); break;
					//default: throw new InvalidOperationException("Submenu");
				}
			}
			finally { _mc.ExecutingStartedEnded?.Invoke(false); }
		}
		
		/// <summary>
		/// When disabled or enabled with <see cref="Enabled"/>.
		/// </summary>
		public event EventHandler CanExecuteChanged;
		
		#endregion
		
		/// <summary>
		/// Finds and returns toolbar button that has this command. Returns null if not found.
		/// </summary>
		public ButtonBase FindButtonInToolbar(ToolBar tb) => tb.Items.OfType<ButtonBase>().FirstOrDefault(o => o.Command == this);
		
		/// <summary>
		/// Finds and returns toolbar menu-button that has this command. Returns null if not found.
		/// </summary>
		public MenuItem FindMenuButtonInToolbar(ToolBar tb) {
			foreach (var e in tb.Items) {
				if (e is Decorator d && d.Child is Menu m && m.Items[0] is MenuItem mi && mi.Command == this) return mi;
			}
			return null;
		}
		
		//public void Test() {
		//	foreach (var v in CanExecuteChanged.GetInvocationList()) {
		//		print.it(v.Target);
		//	}
		//}
		
		internal void Customize_(XElement x, ToolBar toolbar) {
			OverflowMode hide = default;
			bool separator = false;
			string text = null, btext = null;
			Dock? imageAt = null;
			
			foreach (var a in x.Attributes()) {
				string an = a.Name.LocalName, av = a.Value;
				try {
					switch (an) {
					case "keys":
						Keys = av;
						break;
					case "color":
						_mi.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(av));
						break;
					case "image":
						_SetImage(av, custom: true);
						break;
					case "text":
						_mi.Header = text = av;
						break;
					case "btext" when toolbar != null:
						btext = av;
						break;
					case "separator" when toolbar != null:
						separator = true;
						break;
					case "hide" when toolbar != null:
						hide = Enum.Parse<OverflowMode>(av, true);
						break;
					case "imageAt" when toolbar != null:
						imageAt = Enum.Parse<Dock>(av, true);
						break;
					default:
						CustomizingError($"attribute '{an}' can't be used here");
						break;
					}
				}
				catch (Exception ex) { CustomizingError($"invalid '{an}' value", ex); }
			}
			if ((btext ?? text) != null) ButtonText = btext ?? StringUtil.RemoveUnderlineChar(text, '_');
			
			if (toolbar != null) {
				try {
					if (separator) {
						var sep = new Separator();
						if (hide != default) ToolBar.SetOverflowMode(sep, hide);
						toolbar.Items.Add(sep);
					}
					FrameworkElement e;
					if (IsSubmenu) {
						var k = new MenuItem();
						var image = _mi.Icon;
						bool onlyImage = image != null && imageAt == null;
						if (image == null || onlyImage) k.Padding = new Thickness(3, 1, 3, 2); //make taller. If image+text, button too tall, text too high, icon too low, never mind. SHOULDDO: not good on Win7
						CopyToMenu(k, text: btext);
						if (onlyImage) { k.Header = k.Icon; k.Icon = null; } //make narrower
						if (ButtonTooltip != null) k.ToolTip = ButtonTooltip; else if (onlyImage) k.ToolTip = ButtonText;
						var m = new Menu { Background = toolbar.Background, IsMainMenu = false, UseLayoutRounding = true };
						m.Items.Add(k); //parent must be Menu, else wrong Role (must be TopLevelHeader, we can't change) and does not work
						
						//workaround for: 1. Descendant icon part black when checked. 2. Different drop-down menu style.
						//	Never mind: different hot style.
						e = new Border { Child = m };
						k.Padding = osVersion.minWin8 ? new(4, 2, 4, 2) : new(5, 3, 5, 3); //on Win7 smaller
					} else {
						var b = _mi.IsCheckable ? (ButtonBase)new CheckBox() : new Button(); //rejected: support RadioButton
						b.Focusable = false;
						b.UseLayoutRounding = true;
						CopyToButton(b, imageAt);
						b.Padding = new(4, 2, 4, 2);
						e = b;
					}
					if (hide != default) ToolBar.SetOverflowMode(e, hide);
					toolbar.Items.Add(e);
				}
				catch (Exception ex) { CustomizingError("failed to create button", ex); }
			}
		}
		
		public void CustomizingError(string s, Exception ex = null) {
			_mc.OnCustomizingError?.Invoke(this, s, ex);
		}
	}
	
	public event Action<bool> ExecutingStartedEnded;
	
	class _XElementNameEqualityComparer : IEqualityComparer<XElement> {
		bool IEqualityComparer<XElement>.Equals(XElement x, XElement y) => x.Name == y.Name;
		int IEqualityComparer<XElement>.GetHashCode(XElement x) => x.Name.GetHashCode();
	}
	static _XElementNameEqualityComparer s_xmlNameComparer = new();
	
	/// <summary>
	/// Called on error in a custom attribute.
	/// </summary>
	public Action<Command, string, Exception> OnCustomizingError;
	
	/// <summary>
	/// Parameters for factory action of <see cref="KMenuCommands"/>.
	/// </summary>
	public class FactoryParams {
		internal FactoryParams(Command command, MemberInfo member) { this.command = command; this.member = member; }
		
		/// <summary>
		/// The new command.
		/// <see cref="Command.MenuItem"/> is still null and you can call <see cref="SetMenuItem"/>.
		/// </summary>
		public readonly Command command;
		
		/// <summary>
		/// <see cref="MethodInfo"/> of method or <see cref="TypeInfo"/> of nested class.
		/// For example allows to get attributes of any type.
		/// </summary>
		public readonly MemberInfo member;
		
		/// <summary>
		/// Text or a WPF element to add to the text part of the menu item. In/out parameter.
		/// Text may contain _ for Alt-underline, whereas <c>command.Text</c> is without it.
		/// </summary>
		public object text;
		
		/// <summary><see cref="CommandAttribute.image"/>. In/out parameter.</summary>
		public string image;
		
		/// <summary><see cref="CommandAttribute.param"/>. In/out parameter. This class does not use it.</summary>
		public object param;
		
		/// <summary>
		/// Sets <see cref="Command.MenuItem"/> property.
		/// If your factory action does not call this function, the menu item will be created after it returns.
		/// </summary>
		/// <param name="mi">Your created menu item. If null, this function creates standard menu item.</param>
		/// <remarks>
		/// Uses the <i>text</i> and <i>image</i> fields; you can change them before. Sets menu item's <b>Icon</b> property if image!=null and mi?.Image==null. Sets <b>Header</b> property only if creates new item.
		/// The menu item will be added to the parent menu after your factory action returns.
		/// </remarks>
		public void SetMenuItem(MenuItem mi = null) => command.SetMenuItem_(text, image, mi);
	}
}

/// <summary>
/// Used with <see cref="KMenuCommands"/>.
/// Allows to add menu items in the same order as methods and nested types, and optionally specify menu item text etc.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class CommandAttribute : Attribute {
	internal readonly int order_;
	
	/// <summary>
	/// Command name to use instead of method/type name. Use to resolve duplicate name conflict.
	/// </summary>
	public string name;
	
	/// <summary>
	/// Name prefix of this command, and default prefix of descendants. Use to resolve duplicate name conflict.
	/// </summary>
	public string namePrefix;
	
	/// <summary>
	/// Menu item text. Use _ to Alt-underline a character. If "...", appends it to default text.
	/// </summary>
	public string text;
	
	/// <summary>
	/// Alt-underlined character in menu item text.
	/// </summary>
	public char underlined;
	
	/// <summary>
	/// Add separator before the menu item.
	/// </summary>
	public bool separator;
	
	/// <summary>
	/// Checkable menu item.
	/// </summary>
	public bool checkable;
	
	/// <summary>
	/// Default hotkey etc. See <see cref="KMenuCommands.BindKeysTarget"/>.
	/// </summary>
	public string keys;
	
	/// <summary>
	/// Element where the hotkey etc (default or customized) will work. See <see cref="KMenuCommands.BindKeysTarget"/>.
	/// If this property applied to a class (submenu), all descendant commands without this property inherit it from the ancestor class.
	/// </summary>
	public string target;
	
	/// <summary>
	/// Text for <see cref="MenuItem.InputGestureText"/>. If not set, will use <b>keys</b>.
	/// </summary>
	public string keysText;
	
	/// <summary>
	/// Image string.
	/// The factory action receives this string in parameters. It can load image and set menu item's <b>Icon</b> property.
	/// If factory action not used or does not set <b>Image</b> property and does not set image=null, this class loads image from database or exe or script resources and sets <b>Icon</b> property. The resource file can be xaml (for example converted from svg) or png etc. If using Visual Studio, to add an image to resources set its build action = Resource. More info: <see cref="Au.More.ResourceUtil"/>.
	/// </summary>
	public string image;
	
	/// <summary>
	/// Let <see cref="KMenuCommands.Command.CopyToButton"/> use this text for tooltip.
	/// </summary>
	public string tooltip;
	
	/// <summary>
	/// A string or other value to pass to the factory action.
	/// </summary>
	public object param;
	
	/// <summary>
	/// Don't add the <b>MenuItem</b> to menu.
	/// </summary>
	public bool hide;
	
	/// <summary>
	/// Don't change the enabled state indirectly when changing that of the parent menu.
	/// </summary>
	public bool noIndirectDisable;
	
	/// <summary>
	/// Sets menu item text = method/type name with spaces instead of _ , like Select_all -> "Select all".
	/// </summary>
	/// <param name="l_">[](xref:caller_info)</param>
	public CommandAttribute([CallerLineNumber] int l_ = 0) { order_ = l_; }
	
	/// <summary>
	/// Specifies menu item text.
	/// </summary>
	/// <param name="text">Menu item text. Use _ to Alt-underline a character, like "_Copy".</param>
	/// <param name="l_">[](xref:caller_info)</param>
	public CommandAttribute(string text, [CallerLineNumber] int l_ = 0) { this.text = text; order_ = l_; }
	
	/// <summary>
	/// Specifies Alt-underlined character. Sets menu item text = method/type name with spaces instead of _ , like Select_all -> "Select all".
	/// </summary>
	/// <param name="underlined">Character to underline.</param>
	/// <param name="l_">[](xref:caller_info)</param>
	public CommandAttribute(char underlined, [CallerLineNumber] int l_ = 0) { this.underlined = underlined; order_ = l_; }
}
