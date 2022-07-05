using System.Windows.Input;

namespace Au.Controls;

/// <summary>
/// Interface for <see cref="KTreeView"/> items. Provides text and other properties.
/// </summary>
public interface ITreeViewItem {
	//	bool HasItems { get; }
	//	int ItemsCount { get; }
	//	ITreeViewItem Parent { get; }

	/// <summary>
	/// Folder's child items. Return null if not folder.
	/// </summary>
	IEnumerable<ITreeViewItem> Items => null;

	/// <summary>
	/// Is folder.
	/// </summary>
	bool IsFolder => false;

	/// <summary>
	/// Is expanded folder.
	/// </summary>
	bool IsExpanded => false;

	/// <summary>
	/// Called after expanding or collapsing this folder.
	/// </summary>
	void SetIsExpanded(bool yes) { }

	/// <summary>
	/// Text to display.
	/// </summary>
	string DisplayText { get; }

	/// <summary>
	/// Called after label editing.
	/// </summary>
	void SetNewText(string text) { }

	/// <summary>
	/// Image to display.
	/// Alternatively use <see cref="ImageSource"/>.
	/// </summary>
	/// <remarks>
	/// If image's DPI does not match control's DPI (<see cref="KTreeView.Dpi"/>), draws it auto-scaled.
	/// </remarks>
	System.Drawing.Bitmap Image => null;

	/// <summary>
	/// Where to find image or icon to display.
	/// Can have prefix:
	/// "imagefile:" - path of png/bmp/jpg/gif/tif or xaml file.
	/// "resource:" - path of png/bmp/jpg/gif/tif or xaml resource. Don't need prefix if starts with "resources/", like "resources/file.png".
	/// "image:" - Base64 encoded image file.
	/// If without prefix, gets file icon; see <see cref="icon.of"/>.
	/// </summary>
	/// <remarks>
	/// To load image the control calls <see cref="ImageUtil.LoadGdipBitmap"/> or <see cref="ImageUtil.LoadWpfImageElement"/>.
	/// The control uses <see cref="IconImageCache"/>; see <see cref="KTreeView.ImageCache"/>.
	/// </remarks>
	string ImageSource => null;

	/// <summary>
	/// How to draw checkbox when <see cref="KTreeView.HasCheckboxes"/> true. Default <b>Unchecked</b>.
	/// </summary>
	TVCheck CheckState => default;

	/// <summary>
	/// Draw text and checkbox like disabled (usually gray).
	/// </summary>
	bool IsDisabled => false;

	/// <summary>
	/// Draw bold text.
	/// </summary>
	bool IsBold => false;

	/// <summary>
	/// Can be selected. Default true.
	/// </summary>
	bool IsSelectable => true;

	/// <summary>
	/// Background color in 0xBBGGRR format. If -1 (default), uses default colors, depending on state (normal, selected, hot).
	/// If alpha is 1 - 3: if contains flag 1, draws selection color when selected; if 2, draws hot color when hot.
	/// </summary>
	int Color => -1;

	/// <summary>
	/// Text color in 0xBBGGRR format. If -1 (default), uses default colors, depending on state (normal, disabled).
	/// </summary>
	int TextColor => -1;

	/// <summary>
	/// Border color in 0xBBGGRR format. No border if -1 (default).
	/// If alpha is 1, draws left edge between text and icon.
	/// </summary>
	int BorderColor => -1;
}

/// <summary>
/// <see cref="KTreeView"/> checkbox state.
/// </summary>
public enum TVCheck : byte { Unchecked, Checked, Mixed, Excluded, RadioUnchecked, RadioChecked, None }

/// <summary>
/// <see cref="KTreeView"/> item parts.
/// </summary>
[Flags]
public enum TVParts {
	/// <summary>Empty area at the left (indentation).</summary>
	Left = 1,

	/// <summary>Get checkbox rectangle.</summary>
	Checkbox = 2,

	/// <summary>Get left margin rectangle.</summary>
	MarginLeft = 4,

	/// <summary>Get image rectangle.</summary>
	Image = 8,

	/// <summary>Get text rectangle.</summary>
	Text = 16,

	/// <summary>Get right margin rectangle.</summary>
	MarginRight = 32,

	/// <summary>Empty area at the right.</summary>
	Right = 64,

	//note: values must be increasing from left to right
}

/// <summary>
/// <see cref="KTreeView.HitTest"/> results.
/// </summary>
public struct TVHitTest {
	/// <summary>Item, or null if not on an item.</summary>
	public ITreeViewItem item;

	/// <summary>Item index, or -1 if not on an item.</summary>
	public int index;

	/// <summary>Item part.</summary>
	public TVParts part;
}

/// <param name="Item"></param>
/// <param name="Index"></param>
/// <param name="Part">Valid if clicked with mouse.</param>
/// <param name="Button">Valid if clicked with mouse.</param>
/// <param name="ClickCount">1 click, 2 double-click, 0 key or acc.</param>
/// <param name="XY">Valid if clicked with mouse.</param>
/// <param name="Mod"></param>
public record TVItemEventArgs(ITreeViewItem Item, int Index, TVParts Part = 0, MouseButton Button = 0, int ClickCount = 0, POINT XY = default, ModifierKeys Mod = 0);

/// <summary>
/// Custom-draw interface used with <see cref="KTreeView.CustomDraw"/>.
/// </summary>
/// <remarks>
/// When drawing the control, the first and last called functions are <b>Begin</b> and <b>End</b>. For each actually visible item are called functions in this order: <b>DrawBackground</b>, <b>DrawCheckbox</b>, <b>DrawImage</b>, <b>DrawText</b>, <b>DrawMarginLeft</b>, <b>DrawMarginRight</b>. If a bool function returns false, the control draws that part of the item.
/// </remarks>
public interface ITVCustomDraw {
	void Begin(TVDrawInfo cd, GdiTextRenderer tr);
	bool DrawBackground() => false;
	bool DrawCheckbox() => false;
	bool DrawImage(System.Drawing.Bitmap image) => false;
	bool DrawText() => false;
	void DrawMarginLeft() { }
	void DrawMarginRight() { }
	void End() { }
}

/// <summary>
/// Custom-draw info passed to <see cref="ITVCustomDraw"/>.
/// </summary>
public class TVDrawInfo {
	public readonly KTreeView control;

	/// <summary>GDI device context handle.</summary>
	public readonly IntPtr dc;

	public readonly System.Drawing.Graphics graphics;
	public readonly int dpi;

	/// <summary>Index of current item.</summary>
	public int index;

	/// <summary>Current item.</summary>
	public ITreeViewItem item;

	/// <summary>Background drawing rectangle of current item.</summary>
	public RECT rect;

	/// <summary>Image drawing rectangle of current item.</summary>
	public RECT imageRect;

	/// <summary>Text drawing X offset of current item.</summary>
	public int xText;

	/// <summary>Text drawing Y offset of current item.</summary>
	public int yText;

	/// <summary>Left margin drawing X offset of current item.</summary>
	public int xLeft;

	/// <summary>Right margin drawing X offset of current item.</summary>
	public int xRight;

	/// <summary>Left margin width of current item.</summary>
	public int marginLeft;

	/// <summary>Right margin width of current item.</summary>
	public int marginRight;

	/// <summary>Checkbox size.</summary>
	public SIZE checkSize;

	public bool isSelected, isHot, isFocusedItem, isFocusedControl;

	public TVDrawInfo(KTreeView tv, IntPtr dc, System.Drawing.Graphics graphics, int dpi) {
		control = tv;
		this.dc = dc;
		this.graphics = graphics;
		this.dpi = dpi;
	}
}

/// <summary>See <see cref="KTreeView.GetDropInfo"/>.</summary>
public struct TVDropInfo {
	/// <summary>Point relative to the top-left of the control without border. Physical pixels.</summary>
	public POINT xy;

	/// <summary>Item at <b>targetIndex</b>, or null if <b>xy</b> is not on an item.</summary>
	public ITreeViewItem targetItem;

	/// <summary>Index of item from <b>xy</b>, or -1 if <b>xy</b> is not on an item.</summary>
	public int targetIndex;

	/// <summary>If true, should insert after the drop target item. Else before. Not used if <b>intoFolder</b> is true.</summary>
	public bool insertAfter;

	/// <summary><b>xy</b> is in a folder center, therefore should move to the folder.</summary>
	public bool intoFolder;
}

