using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

//SHOULDDO: when a checkbox button command invoked with a hotkey, now does not change check state in menu and toolbar.
//	Only in Edit menu. Even if target="" and scintilla not focused. Works well in other menus. Don't know why.
//	Currently affected code explicitly changes check state.

namespace Au.Controls;

/// <summary>
/// Builds a WPF window menu with submenus and items that execute static methods defined in a class and nested classes.
/// Supports xaml/png/etc images, key/mouse shortcuts, auto-Alt-underline, easy creating of toolbar buttons and context menus with same/synchronized properties (command, text, image, enabled, checked, etc).
/// </summary>
/// <remarks>
/// Creates submenus from public static nested types with <see cref="CommandAttribute"/>.
/// Creates executable menu items from public static methods with <see cref="CommandAttribute"/>.
/// From each such type and method creates a <see cref="Command"/> object that you can access through indexer.
/// Supports methods <c>public static void Method()</c> and <c>public static void Method(MenuItem)</c>.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// var cmd=new KMenuCommands(typeof(Commands), menu);
/// cmd[nameof(Commands.Edit.Paste)].Enabled = false;
/// cmd[nameof(Commands.File.Rename)].SetKeys("F12", _window);
/// ]]></code>
/// 
/// <code><![CDATA[
/// static class Commands {
/// 	[Command('F')]
/// 	public static class File {
/// 		[Command('R')]
/// 		public static void Rename() {  }
/// 		
/// 		[Command('D')]
/// 		public static void Delete() {  }
/// 		
/// 		[Command("_Properties...", image = "properties.xaml")]
/// 		public static void Properties() {  }
/// 		
/// 		[Command('N')]
/// 		public static class New {
/// 			[Command('D')]
/// 			public static void Document() {  }
/// 			
/// 			[Command('F')]
/// 			public static void Folder() {  }
/// 		}
/// 		
/// 		[Command('x', separator = true)]
/// 		public static void Exit(object param) {  }
/// 	}
/// 	
/// 	[Command('E')]
/// 	public static class Edit {
/// 		[Command('t')]
/// 		public static void Cut() {  }
/// 		
/// 		[Command('C')]
/// 		public static void Copy() {  }
/// 		
/// 		[Command('P')]
/// 		public static void Paste() {  }
/// 		
/// 		[Command('D', name = "Edit-Delete")]
/// 		public static void Delete() {  }
/// 		
/// 		[Command('a')]
/// 		public static void Select_all() {  }
/// 	}	
/// }
/// ]]></code>
/// </example>
public partial class KMenuCommands {
	readonly Dictionary<string, Command> _d = new(200);
	Menu _menubar;
	
	/// <summary>
	/// Builds a WPF window menu with submenus and items that execute static methods defined in a class and nested classes.
	/// See example in class help.
	/// </summary>
	/// <param name="commands">A type that contains nested types with methods. Must be in single source file (not partial class).</param>
	/// <param name="menu">An empty <b>Menu</b> object. This function adds items to it.</param>
	/// <param name="autoUnderline">Automatically insert _ in item text for Alt-underlining where not specified explicitly.</param>
	/// <param name="itemFactory">Optional callback function that is called for each menu item. Can create menu items, set properties, create toolbar buttons, etc.</param>
	/// <exception cref="ArgumentException">Duplicate name. Use <see cref="CommandAttribute.name"/>.</exception>
	public KMenuCommands(Type commands, Menu menu, bool autoUnderline = true, Action<FactoryParams> itemFactory = null) {
		_menubar = menu;
		_CreateMenu(commands, menu, autoUnderline, itemFactory);
	}
	
	void _CreateMenu(Type type, ItemsControl parentMenu, bool autoUnderline, Action<FactoryParams> itemFactory, List<string> added = null, string namePrefix_ = null, string inheritTarget_ = null) {
		var am = type.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
		
		if (am.Length == 0) { //dynamic submenu
			parentMenu.Items.Add(new Separator());
			return;
		}
		
		var list = new List<(MemberInfo mi, CommandAttribute a)>(am.Length);
		foreach (var mi in am) {
			var ca = mi.GetCustomAttribute<CommandAttribute>(false);
			//var ca = mi.GetCustomAttributes().OfType<CommandAttribute>().FirstOrDefault(); //CommandAttribute and inherited. Similar speed. Don't need because factory action receives MemberInfo an can get other attributes from it.
			if (ca != null) list.Add((mi, ca));
		}
		
		var au = new List<char>();
		
		foreach (var (mi, ca) in list.OrderBy(o => o.a.order_)) {
			if (ca.separator && !ca.hide) parentMenu.Items.Add(new Separator());
			
			ca.target ??= inheritTarget_;
			
			string text = ca.text, buttonText, dots = null; //menu item text, possibly with _ for Alt-underline
			if (text == "...") { dots = text; text = null; }
			if (text != null) {
				buttonText = StringUtil.RemoveUnderlineChar(text, '_');
			} else {
				buttonText = text = mi.Name.Replace('_', ' ') + dots;
				char u = ca.underlined;
				if (u != default) {
					int i = text.IndexOf(u);
					if (i >= 0) text = text.Insert(i, "_"); else print.it($"Alt-underline character '{u}' not found in \"{text}\"");
				}
			}
			
			var namePrefix = ca.namePrefix ?? namePrefix_;
			string name = namePrefix + (ca.name ?? mi.Name);
			var c = new Command(this, name, text, mi, ca);
			_d.Add(name, c);
			added?.Add(name);
			
			c.ButtonText = buttonText;
			if (ca.tooltip is string tt) c.ButtonTooltip = parentMenu is Menu ? tt : $"{buttonText}{(ca.checkable ? "  (option)" : null)}.\n{tt}";
			else if (ca.checkable) c.ButtonTooltip = $"{buttonText}  (option)";
			
			FactoryParams f = null;
			if (itemFactory != null) {
				f = new FactoryParams(c, mi) { text = text, image = ca.image, param = ca.param };
				itemFactory(f);
				if (c.MenuItem == null) c.SetMenuItem_(f.text, f.image); //did not call SetMenuItem
			} else {
				if (c.MenuItem == null) c.SetMenuItem_(text, ca.image);
			}
			if (!ca.keysText.NE()) c.MenuItem.InputGestureText = ca.keysText;
			if (autoUnderline && c.MenuItem.Header is string s && _FindUnderlined(s, out char uc)) au.Add(char.ToLower(uc));
			if (ca.checkable) c.MenuItem.IsCheckable = true;
			if (c.ButtonTooltip != null) c.MenuItem.ToolTip = c.ButtonTooltip;
			if (ca.noIndirectDisable) c.NoIndirectDisable = true;
			if (parentMenu is Menu m1) c.MenuItem.MinHeight = 22;
			
			if (!ca.hide) parentMenu.Items.Add(c.MenuItem);
			if (mi is TypeInfo ti) _CreateMenu(ti, c.MenuItem, autoUnderline, itemFactory, added, namePrefix, ca.target);
		}
		
		if (autoUnderline) {
			foreach (var v in parentMenu.Items) {
				if (v is MenuItem m && m.Header is string s && s.Length > 0 && !_FindUnderlined(s, out _)) {
					int i = 0;
					for (; i < s.Length; i++) {
						char ch = s[i]; if (!char.IsLetterOrDigit(ch)) continue;
						ch = char.ToLower(ch);
						if (!au.Contains(ch)) { au.Add(ch); break; }
					}
					if (i == s.Length) i = 0;
					m.Header = s.Insert(i, "_");
				}
			}
		}
		
		static bool _FindUnderlined(string s, out char u) {
			u = default;
			int i = 0;
			g1: i = s.IndexOf('_', i) + 1;
			if (i == 0 || i == s.Length) return false;
			u = s[i++];
			if (u == '_') goto g1;
			return true;
		}
	}
	
	/// <summary>
	/// Gets a <b>Command</b> by name.
	/// </summary>
	/// <param name="command">Method name, for example "Select_all". Or nested type name if it's a submenu-item.</param>
	/// <exception cref="KeyNotFoundException"></exception>
	public Command this[string command] => _d[command];
	
	/// <summary>
	/// Tries to find a <b>Command</b> by name. Returns false if not found.
	/// Same as the indexer, but does not throw exception when not found.
	/// </summary>
	/// <param name="command">Method name, for example "Select_all". Or nested type name if it's a submenu-item.</param>
	/// <param name="c"></param>
	public bool TryFind(string command, out Command c) => _d.TryGetValue(command, out c);
	
	/// <summary>
	/// Adds to <i>target</i>'s <b>InputBindings</b> all keys etc where <b>CommandAttribute.target</b> == <i>name</i>.
	/// </summary>
	/// <param name="target"></param>
	/// <param name="name"></param>
	public void BindKeysTarget(UIElement target, string name) {
		//print.it($"---- {name} = {target}");
		foreach (var c in _d.Values) {
			var ca = c.Attribute;
			var keys = c.Keys;
			if (!keys.NE() && ca.target == name) {
				//print.it(c, keys);
				int i = keys.IndexOf(", ");
				if (i < 0) _Add(keys); else foreach (var v in keys.Split(", ")) _Add(v);
				void _Add(string s) {
					if (!Au.keys.more.parseHotkeyString(s, out var mod, out var key, out var mouse)) {
						c.CustomizingError("invalid keys: " + s);
						return;
					}
					if (key != default) target.InputBindings.Add(new KeyBinding(c, key, mod));
					else if (target is System.Windows.Interop.HwndHost) c.CustomizingError(s + ": mouse shortcuts don't work in the target control");
					else target.InputBindings.Add(new MouseBinding(c, new MouseGesture(mouse, mod)));
					
					//FUTURE: support mouse shortcuts in HwndHost
					//if (target is System.Windows.Interop.HwndHost hh) {
					//	hh.MessageHook += _Hh_MessageHook;
					//	//or use native mouse hook
					//} else {
					//	target.InputBindings.Add(new MouseBinding(this, new MouseGesture(mouse, mod)));
					//}
				}
				var mi = c.MenuItem;
				var s = mi.InputGestureText;
				if (s.NE()) s = keys; else s = s + ", " + keys;
				mi.InputGestureText = s;
			}
		}
		
		//let global key bindings work in any window of this thread, not only when target (main window) is active. Never mind mouse bindings.
		if (name == "") {
			var a = target.InputBindings.OfType<KeyBinding>().ToArray();
			if (a.Length > 0) {
				EventManager.RegisterClassHandler(typeof(Window), UIElement.KeyDownEvent, new KeyEventHandler(_KeyDown));
				//InputManager.Current.PreProcessInput += _App_PreProcessInput; //works too, but more events
				
				void _KeyDown(object source, KeyEventArgs e) {
					if (Environment.CurrentManagedThreadId != 1) return;
					//perf.first();
					if (e.Handled) return;
					var k = e.Key; if (k == Key.System) k = e.SystemKey;
					if (k is Key.LeftCtrl or Key.LeftShift or Key.LeftAlt or Key.RightCtrl or Key.RightShift or Key.RightAlt or Key.LWin or Key.RWin or Key.DeadCharProcessed or Key.ImeProcessed) return;
					//print.it(k);
					ModifierKeys mod = 0; bool haveMod = false;
					foreach (var kb in a) {
						//print.it(kb.Command);
						if (kb.Key != k) continue;
						if (!haveMod) { haveMod = true; mod = Keyboard.Modifiers; }
						if (kb.Modifiers != mod) continue;
						var c = kb.Command; var cp = kb.CommandParameter;
						if (c.CanExecute(cp)) c.Execute(cp);
						e.Handled = true;
						break;
						//note: execute even if main window disabled. Maybe the command works in current window. Or maybe user wants to save (Ctrl+S).
					}
					//perf.nw(); //fast
				}
				
				//void _PreProcessInput(object sender, PreProcessInputEventArgs e) {
				//	if (e.Canceled) return;
				//	var re = e.StagingItem.Input.RoutedEvent;
				//	if (re == Keyboard.KeyDownEvent && e.StagingItem.Input is KeyEventArgs ke) {
				//		var k = ke.Key; if (k == Key.System) k = ke.SystemKey;
				//		if (k is Key.LeftCtrl or Key.LeftShift or Key.LeftAlt or Key.RightCtrl or Key.RightShift or Key.RightAlt or Key.LWin or Key.RWin or Key.DeadCharProcessed or Key.ImeProcessed) return;
				//		print.it(k);
				//	//} else { //no mouse events in hwndhosted control. It's ok, don't need global mouse shortcuts. Normal WPF bindings don't work too.
				//	//	print.it(re);
				//	}
				//}
			}
		}
	}
	
	public string DefaultFile { get; private set; }
	public string UserFile { get; private set; }
	
	/// <summary>
	/// Adds toolbar buttons specified in <i>xmlFileCustomized</i> and <i>xmlFileDefault</i>.
	/// Applies customizations specified there.
	/// </summary>
	/// <param name="xmlFileDefault">XML file containing default toolbar buttons. See Default\Commands.xml in editor project.</param>
	/// <param name="xmlFileCustomized">XML file containing user-modified commands and toolbar buttons. Can be null. The file can exist or not.</param>
	/// <param name="toolbars">Empty toolbars where to add buttons. XML tag = <b>Name</b> property.</param>
	public void InitToolbarsAndCustomize(string xmlFileDefault, string xmlFileCustomized, ToolBar[] toolbars) {
		DefaultFile = xmlFileDefault;
		UserFile = xmlFileCustomized;
		
		var a = LoadFiles(); if (a == null) return;
		
		foreach (var x in a) {
			ToolBar tb = null;
			var tbname = x.Name.LocalName;
			if (tbname != "menu") {
				tb = toolbars.FirstOrDefault(o => o.Name == tbname);
				if (tb == null) { Debug_.Print("Unknown toolbar " + tbname); continue; }
			}
			
			foreach (var v in x.Elements()) {
				if (_d.TryGetValue(v.Name.LocalName, out var c)) c.Customize_(v, tb);
			}
		}
		
	}
	
	/// <summary>
	/// Loads and merges default and customized commands files.
	/// </summary>
	public XElement[] LoadFiles() {
		static XElement[] _LoadFile(string file) {
			try { return XmlUtil.LoadElem(file).Elements().ToArray(); }
			catch (Exception ex) { print.it($"<>Failed to load file <explore>{file}<>. <_>{ex.ToStringWithoutStack()}</_>"); return null; }
		}
		
		var a = _LoadFile(DefaultFile); if (a == null) return null;
		var ac = UserFile != null && filesystem.exists(UserFile, true).File ? _LoadFile(UserFile) : null;
		
		if (ac != null) { //replace a elements with elements that exist in ac. If some toolbar does not exist there, use default.
			for (int i = 0; i < a.Length; i++) {
				var name = a[i].Name;
				foreach (var x in ac) if (x.Name == name && x.HasElements) { _AddMissingButtons(a[i], x); a[i] = x; break; }
			}
			
			void _AddMissingButtons(XElement xDef, XElement xUser) {
				if (xUser.Name.LocalName == "menu") return;
				foreach (var v in xDef.Elements().Except(xUser.Elements(), s_xmlNameComparer)) {
					//rejected: hide new buttons to avoid pushing some old buttons to the overflow if the toolbar size cannot grow.
					//	Then new and probably important features would be used rarely and/or inconveniently.
					//	Instead hide just duplicates, eg when the new button actually is an old button moved to this toolbar.
					//v.SetAttributeValue("hide", "always");
					foreach (var tb in ac) if (tb != xUser && tb.Elements(v.Name).Any()) { v.SetAttributeValue("hide", "always"); break; }
					
					if (v.PreviousNode is XElement xdPrev && xUser.Element(xdPrev.Name) is { } xuPrev) xuPrev.AddAfterSelf(v); //insert in default place
					else xUser.Add(v); //add to the end
				}
			}
		}
		
		return a;
	}
	
	//currently not used.
	//public void AddExtensionMenus(Type commands, MenuItem parentMenu = null, bool autoUnderline = true) {
	//	ItemsControl pa = parentMenu ?? _menubar as ItemsControl;
	//	_extensions ??= new();
	//	if (_extensions.Remove(commands.Name, out var t)) {
	//		foreach (var v in t.menus) pa.Items.Remove(v);
	//		foreach (var v in t.commands) _d.Remove(v);
	//	}
	//	int n = pa.Items.Count;
	//	List<string> added = new();
	//	_CreateMenu(commands, pa, autoUnderline, null, added);
	//	_extensions.Add(commands.Name, (pa.Items.OfType<object>().Skip(n).ToArray(), added));
	//}
	//Dictionary<string, (object[] menus, List<string> commands)> _extensions;
	////_extensions used to support updating the same extension. Remove the code if don't need.
}
