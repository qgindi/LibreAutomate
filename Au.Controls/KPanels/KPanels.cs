using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Au.Controls
{
	/// <summary>
	/// Creates and manages window layout like in Visual Studio.
	/// Multiple docked movable/sizable/tabable/floatable/hidable/savable panels/toolbars/documents with splitters.
	/// </summary>
	/// <remarks>
	/// Layout is defined in default XML file, then saved in other XML file. See Layout.xml in Au.Editor project.
	/// If new app version adds/removes/renames panels etc, this class automatically updates the saved layout.
	/// 
	/// Let your window's ctor:
	/// - call <see cref="Load"/>;
	/// - set content of all leaf items (panels, toolbars, document placeholder) like <c>_panels["Files"].Content = new TreeView();</c>;
	/// - set <see cref="Container"/> like <c>_panels.Container = g => this.Content = g;</c>. The action is called immediately and also may be called later if need to create new root element when moving a panel etc.
	/// If want to save user-customized layout, call <see cref="Save"/> when closing window or at any time before it.
	/// </remarks>
	public partial class KPanels
	{
		readonly Dictionary<string, _Node> _dictLeaf = new();
		readonly Dictionary<string, _Node> _dictUserDoc = new();
		_Node _rootStack;
		string _xmlFile;
		bool _loaded;

		/// <summary>
		/// Loads layout from XML file.
		/// </summary>
		/// <param name="xmlFileDefault">
		/// XML file containing default layout. See Default\Layout.xml in editor project.
		/// If starts with '&lt;', loads from XML string instead.
		/// </param>
		/// <param name="xmlFileCustomized">XML file containing user-modified layout. It will be created or updated by <see cref="Save"/>. If null, will not save.</param>
		public void Load(string xmlFileDefault, string xmlFileCustomized) {
			if (_loaded) throw new InvalidOperationException();
			_loaded = true;
			_xmlFile = xmlFileCustomized;

			//At first try to load xmlFileCustomized. If it does not exist or is invalid, load xmlFileDefault.
			string xmlFile = xmlFileCustomized; bool useDefaultXML = xmlFileCustomized == null;
			gRetry:
			if (useDefaultXML) xmlFile = xmlFileDefault;
			else if (useDefaultXML = !filesystem.exists(xmlFile).File) goto gRetry;

			try {
				var x = XmlUtil.LoadElem(xmlFile);
				if (!useDefaultXML) _AutoUpdateXml(x, xmlFileDefault);
				new _Node(this, x); //creates all and sets _rootStack
			}
			catch (Exception e) when (!Debugger.IsAttached) {
				var sErr = $"Failed to load file '{xmlFile}'.\r\n\t{e.ToStringWithoutStack()}";
				if (useDefaultXML) {
					_xmlFile = null;
					dialog.showError("Cannot load window layout from XML file.",
						$"{sErr}\r\n\r\nReinstall the application.",
						expandedText: e.StackTrace);
					Environment.Exit(1);
				} else {
					print.warning(sErr, -1);
				}
				_dictLeaf.Clear(); _rootStack = null;
				useDefaultXML = true; goto gRetry;
			}
		}

		/// <summary>
		/// Saves layout to XML file <i>xmlFileCustomized</i> specified when calling <see cref="Load"/>.
		/// Can be called at any time. When closing window, should be called in OnClosing override after calling base.OnClosing.
		/// Does nothing if <i>xmlFileCustomized</i> was null.
		/// </summary>
		public void Save() {
			if (_xmlFile == null) return;
			try {
				filesystem.createDirectoryFor(_xmlFile);
				var sett = new XmlWriterSettings() {
					OmitXmlDeclaration = true,
					Indent = true,
					IndentChars = "\t"
				};
				filesystem.save(_xmlFile, temp => {
					using var x = XmlWriter.Create(temp, sett);
					x.WriteStartDocument();
					_rootStack.Save(x);
				});
				//run.it("notepad.exe", _xmlFile); timer.after(1000, _ => DeleteSavedFile());
			}
			catch (Exception ex) { print.qm2.write(ex); }
		}

		void _AutoUpdateXml(XElement rootStack, string xmlFileDefault) {
			var defRootStack = XmlUtil.LoadElem(xmlFileDefault);
			var eOld = rootStack.Descendants("panel").Concat(rootStack.Descendants("toolbar"))/*.Concat(rootStack.Descendants("document"))*/; //same speed as with .Where(cached delegate)
			var eNew = defRootStack.Descendants("panel").Concat(defRootStack.Descendants("toolbar"))/*.Concat(defRootStack.Descendants("document"))*/;
			var cmp = new _XmlNameAttrComparer();
			var added = eNew.Except(eOld, cmp);
			var removed = eOld.Except(eNew, cmp);
			if (removed.Any()) {
				foreach (var x in removed.ToArray()) {
					_Remove(x);
					print.it($"Info: {x.Name} {x.Attr("name")} has been removed in this app version.");
				}
				//removes x and ancestor stacks/tabs that would then have <= 1 child
				static void _Remove(XElement x) {
					var pa = x.Parent;
					x.Remove();
					var a = pa.Elements();
					int n = a.Count();
					if (n > 1) return;
					if (n == 1) {
						var f = a.First();
						f.SetAttributeValue("z", pa.Attr("z"));
						f.SetAttributeValue("s", pa.Attr("s"));
						f.Remove();
						pa.AddAfterSelf(f);
					}
					_Remove(pa);
				}
			}
			if (added.Any()) {
				foreach (var x in added) {
					x.SetAttributeValue("z", 50); //note: set even if was no "z", because maybe was in a tab
					rootStack.Add(x);
					print.it($"Info: {x.Name} {x.Attr("name")} has been added in this app version. You can right-click its caption and move it to a better place.");
				}
			}
			//print.it(rootStack);
		}

		///// <summary>
		///// Deletes the user's saved layout file. Then next time will use the default file.
		///// </summary>
		//public void DeleteSavedFile() {
		//	filesystem.delete(_xmlFile);
		//}

		/// <summary>
		/// Action that adds the root node (Grid) to a container (for example Window), like <c>_panels.Container = g => this.Content = g;</c>.
		/// The action is called immediately and also may be called later if need to create new root element when moving a panel etc.
		/// </summary>
		public Action<Grid> Container {
			get => _setContainer;
			set {
				_setContainer = value;
				var e = _rootStack.Elem as Grid;
				if (e.Parent == null) _setContainer(e);
			}
		}
		Action<Grid> _setContainer;

		//public Grid RootElem => _rootStack.Elem as Grid;

		/// <summary>
		/// Gets interface of a leaf item (panel, toolbar or document).
		/// </summary>
		/// <param name="name"></param>
		/// <param name="userDocument">It is a document (not panel/toolbar) added with <see cref="ILeaf.AddSibling"/>. User documents are in a separate dictionary, to avoid name conflicts.</param>
		/// <exception cref="KeyNotFoundException"></exception>
		public ILeaf this[string name, bool userDocument = false] => (userDocument ? _dictUserDoc : _dictLeaf)[name];

		/// <summary>
		/// Gets interface of container leaf item (panel, toolbar or document).
		/// </summary>
		/// <param name="e">Leaf's <b>Content</b> or any descendant.</param>
		/// <exception cref="NotFoundException"></exception>
		public ILeaf this[DependencyObject e] {
			get {
				while (e != null) {
					if (e is _DockPanelWithBorder d && d.Tag is ILeaf f) return f;
					e = VisualTreeHelper.GetParent(e); //same with LogicalTreeHelper
				}
				throw new NotFoundException();
			}
		}

		//rejected. Rarely used. Can set in Container action.
		///// <summary>
		///// Background brush of the root grid.
		///// Set before <see cref="Load"/>.
		///// </summary>
		//public Brush Background {
		//	get => _gridBackground;
		//	set => _gridBackground = value;
		//}

		/// <summary>
		/// Background brush of panel/document caption and tab strip. Default Brushes.LightSteelBlue.
		/// Set before <see cref="Load"/>.
		/// </summary>
		public Brush CaptionBrush { get; set; } = Brushes.LightSteelBlue;

		//rejected. Looks ugly when different color, unless white.
		///// <summary>
		///// Background brush of tab strip. Default Brushes.LightSteelBlue.
		///// Set before <see cref="Load"/>.
		///// </summary>
		//public Brush TabBrush { get; set; } = Brushes.LightSteelBlue;

		/// <summary>
		/// Background brush of splitters.
		/// Set before <see cref="Load"/>.
		/// </summary>
		public Brush SplitterBrush { get; set; }

		/// <summary>
		/// Border color of panels and documents.
		/// Set before <see cref="Load"/>.
		/// If not set, no borders will be added.
		/// </summary>
		public Brush BorderBrush { get; set; }

		/// <summary>
		/// Gets top-level window, for example to use as owner of menus/dialogs.
		/// Note: it may not be direct container of the root element.
		/// </summary>
		Window _ContainerWindow => _window ??= Window.GetWindow(_rootStack.Elem);
		Window _window;

		class _XmlNameAttrComparer : IEqualityComparer<XElement>
		{
			public bool Equals(XElement x, XElement y) => x.Attr("name") == y.Attr("name");
			public int GetHashCode(XElement obj) => obj.Attr("name").GetHashCode();
			//fast, same as with XName _name.
		}
	}
}
