using System.Windows;
using System.Windows.Controls;

namespace Au.Controls;

public partial class KPanels {
	/// <summary>
	/// Interface for a leaf item (panel, toolbar or document).
	/// </summary>
	public interface ILeaf {
		/// <summary>
		/// Gets or sets content of panel/toolbar/document.
		/// </summary>
		FrameworkElement Content { get; set; }
		
		/// <summary>
		/// true if visible, either floating or docked.
		/// The <c>get</c> function returns true even if inactive tab item. The <c>set</c> function makes tab item active.
		/// </summary>
		bool Visible { get; set; }
		
		/// <summary>
		/// true if floating and visible.
		/// false if docked or hidden.
		/// </summary>
		bool Floating { get; set; }
		
		/// <summary>
		/// If not null, clicking on the parent tab header does not change the current focus (if possible).
		/// The action is called if the currently focused element is in the tab control; it must set focus to some visible element or call <b>Keyboard.ClearFocus</b>.
		/// </summary>
		Action DontFocusTab { get; set; }
		
		
		Func<UIElement, bool> DontActivateFloating { get; set; }
		
		/// <summary>
		/// Adds new leaf item (panel, toolbar or document) before or after this.
		/// </summary>
		/// <param name="after"></param>
		/// <param name="type"></param>
		/// <param name="name"></param>
		/// <param name="canClose">Add "Close    M-click" item in context menu. It will fire <see cref="Closing"/> event and call <see cref="Delete"/> if not canceled.</param>
		/// <param name="isExtension">Save layout etc in file.</param>
		/// <returns>Interface of the new item.</returns>
		/// <exception cref="ArgumentException"><i>type</i> is not Panel/Toolbar/Document, or <i>name</i> is null, or <i>name</i> panel already exists.</exception>
		/// <remarks>
		/// Added items can be deleted with <see cref="Delete"/>.
		/// Add documents only by the document placeholder or by added documents. Don't add other nodes by documents.
		/// </remarks>
		ILeaf AddSibling(bool after, LeafType type, string name, bool canClose, bool isExtension);
		
		/// <summary>
		/// Deletes this leaf item added with <see cref="AddSibling"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">Added not with <b>AddSibling</b>.</exception>
		void Delete();
		
		/// <summary>
		/// Renames this document.
		/// </summary>
		void Rename(string name);
		
		/// <summary>
		/// Gets parent elements and index.
		/// </summary>
		ParentInfo Parent { get; }
		
		/// <summary>
		/// After hiding or showing this leaf item.
		/// </summary>
		event EventHandler<bool> VisibleChanged;
		
		/// <summary>
		/// After made floating or non-floating this leaf item.
		/// </summary>
		event EventHandler<bool> FloatingChanged;
		
		/// <summary>
		/// When user tries to close this leaf item.
		/// Only if added with <see cref="AddSibling"/> with <i>canClose</i> true.
		/// </summary>
		event System.ComponentModel.CancelEventHandler Closing;
		
		///// <summary>
		///// When opening context menu of this leaf item.
		///// You can add menu items. All default items are already added.
		///// </summary>
		//event EventHandler<popupMenu> ContextMenuOpening;
		//FUTURE: reenable this if useful when ContextMenu_ will be public
		
		/// <summary>
		/// When this tab item selected (becomes the active item).
		/// </summary>
		event EventHandler TabSelected;
		
		/// <summary>
		/// When moved to other tab or stack.
		/// </summary>
		event EventHandler ParentChanged;
	}
	
	/// <summary>Leaf item type.</summary>
	public enum LeafType { None, Panel, Toolbar, Document }
	
	public struct ParentInfo {
		readonly DockPanel _panel;
		readonly FrameworkElement _elem;
		readonly int _index;
		
		internal ParentInfo(DockPanel panel, FrameworkElement elem, int index) {
			_panel = panel; _elem = elem; _index = index;
		}
		
		/// <summary>
		/// Gets <b>DockPanel</b> that contains or will contain <see cref="ILeaf.Content"/>.
		/// The first child is caption, and is <b>TextBlock</b> or <b>Rectangle</b>. The second child is <b>Content</b> (if set) or none.
		/// </summary>
		public DockPanel Panel => _panel;
		
		/// <summary>
		/// Gets parent <b>Grid</b> if in stack, else null.
		/// </summary>
		public Grid Grid => _elem as Grid;
		
		/// <summary>
		/// Gets parent <b>TabControl</b> if in tab, else null.
		/// </summary>
		public TabControl TabControl => _elem as TabControl;
		
		/// <summary>
		/// Gets parent <b>TabItem</b> if in tab, else null.
		/// Its <b>Tag</b> is this <b>ILeaf</b>.
		/// </summary>
		public TabItem TabItem => TabControl?.Items[_index] as TabItem;
		
		/// <summary>
		/// Gets node index in parent node. If in tab, it is also tab item index.
		/// </summary>
		public int Index => _index;
	}
}
