namespace Au.Types;

/// <summary>
/// Represents a button or separator in <see cref="toolbar"/>.
/// </summary>
/// <remarks>
/// Most properties cannot be changed while the toolbar is open. Can be changed <b>Tag</b>, <b>Tooltip</b>.
/// </remarks>
public class TBItem : MTItem {
	internal SIZE textSize;
	internal readonly TBItemType type;
	internal bool textAlways;
	internal popupMenu menu;

	internal TBItem(TBItemType type) {
		this.type = type;
		textAlways = type == TBItemType.Group;
	}

	internal bool IsSeparator_ => type == TBItemType.Separator;
	internal bool IsGroup_ => type == TBItemType.Group;
	internal bool IsMenu_ => type == TBItemType.Menu;
	internal bool IsSeparatorOrGroup_ => type is TBItemType.Separator or TBItemType.Group;

	///
	public TBItemType ItemType => type;

	/// <summary>Gets button action.</summary>
	public Action<TBItem> Clicked => base.clicked as Action<TBItem>;
}

/// <summary>
/// Used with <see cref="TBItem.ItemType"/>.
/// </summary>
public enum TBItemType : byte {
#pragma warning disable 1591 //doc
	Button,
	Menu,
	Separator,
	Group,
#pragma warning restore
}

/// <summary>
/// Used with <see cref="toolbar.MiscFlags"/>.
/// </summary>
[Flags]
public enum TBFlags {
	/// <summary>
	/// Activate the owner window when the toolbar clicked. Default.
	/// </summary>
	ActivateOwnerWindow = 1,

	/// <summary>
	/// Hide the toolbar when a full-screen window is active. Default.
	/// </summary>
	HideWhenFullScreen = 2,

	//rejected: use SHQueryUserNotificationState to detect also presentation mode etc. Too slow, eg 1300 mcs, which is 100-500 times slower than wnd.isFullScreen.
	//	HideWhenFullScreenActive
	//	HideWhenFullScreenRunning (QUNS_BUSY) (in primary screen only)
	//	HideWhenPresentation (QUNS_PRESENTATION_MODE)
	//	HideWhenD3DFullScreen (QUNS_RUNNING_D3D_FULL_SCREEN). I guess it is for games. Not tested.
	//	But problem: no QUNS_BUSY if fullscreen in non-primary screen.
	//	Also found this comment (not tested): "It also only works when the Explorer shell is running."
}

/// <summary>
/// Used with <see cref="toolbar.Border"/>.
/// </summary>
public enum TBBorder {
	//note: don't reorder.

	/// <summary>No border.</summary>
	None,

	/// <summary>1 pixel border.</summary>
	Width1,

	/// <summary>1 pixel border + 1 pixel padding.</summary>
	Width2,

	/// <summary>1 pixel border + 2 pixels padding.</summary>
	Width3,

	/// <summary>1 pixel border + 3 pixels padding.</summary>
	Width4,

	/// <summary>3D border.</summary>
	ThreeD,

	/// <summary>Standard window border.</summary>
	Thick,

	/// <summary>Title bar and standard window border.</summary>
	Caption,

	/// <summary>Title bar, <b>X</b> button and standard window border.</summary>
	CaptionX,
}

/// <summary>
/// Used with <see cref="toolbar.Anchor"/>.
/// </summary>
public enum TBAnchor {
	//top 1, bottom 2, left 4, right 8

	/// <summary>
	/// Anchors are top and left edges. Default.
	/// </summary>
	TopLeft = 1 | 4,

	/// <summary>
	/// Anchors are top and right edges.
	/// </summary>
	TopRight = 1 | 8,

	/// <summary>
	/// Anchors are bottom and left edges.
	/// </summary>
	BottomLeft = 2 | 4,

	/// <summary>
	/// Anchors are bottom and right edges.
	/// </summary>
	BottomRight = 2 | 8,

	/// <summary>
	/// Anchors are top, left and right edges. The toolbar is resized horizontally when resizing its owner.
	/// </summary>
	TopLR = 1 | 4 | 8,

	/// <summary>
	/// Anchors are bottom, left and right edges. The toolbar is resized horizontally when resizing its owner.
	/// </summary>
	BottomLR = 2 | 4 | 8,

	/// <summary>
	/// Anchors are left, top and bottom edges. The toolbar is resized vertically when resizing its owner.
	/// </summary>
	LeftTB = 4 | 1 | 2,

	/// <summary>
	/// Anchors are right, top and bottom edges. The toolbar is resized vertically when resizing its owner.
	/// </summary>
	RightTB = 8 | 1 | 2,

	/// <summary>
	/// Anchors are all edges. The toolbar is resized when resizing its owner.
	/// </summary>
	All = 15,

	/// <summary>
	/// Use owner's opposite left/right edge than specified. In other words, attach toolbar's left edge to owner's right edge or vice versa.
	/// This flag is for toolbars that normally are outside of the owner rectangle (at the left or right).
	/// This flag cannot be used with <b>TopLR</b>, <b>BottomLR</b>, <b>All</b>.
	/// </summary>
	OppositeEdgeX = 32,

	/// <summary>
	/// Use owner's opposite top/bottom edge than specified. In other words, attach toolbar's top edge to owner's bottom edge or vice versa.
	/// This flag is for toolbars that normally are outside of the owner rectangle (above or below).
	/// This flag cannot be used with <b>LeftTB</b>, <b>RightTB</b>, <b>All</b>.
	/// </summary>
	OppositeEdgeY = 64,

	/// <summary>
	/// Anchor is screen, not owner window. Don't move the toolbar together with its owner window.
	/// </summary>
	Screen = 128,
}

static partial class TBExt_ {
	internal static bool HasTop(this TBAnchor a) => 0 != ((int)a & 1);
	internal static bool HasBottom(this TBAnchor a) => 0 != ((int)a & 2);
	internal static bool HasLeft(this TBAnchor a) => 0 != ((int)a & 4);
	internal static bool HasRight(this TBAnchor a) => 0 != ((int)a & 8);
	internal static bool OppositeX(this TBAnchor a) => 0 != ((int)a & 32);
	internal static bool OppositeY(this TBAnchor a) => 0 != ((int)a & 64);
	internal static bool OfScreen(this TBAnchor a) => 0 != ((int)a & 128);
	internal static TBAnchor WithoutFlags(this TBAnchor a) => a & TBAnchor.All;
}

//rejected. Instead use System.Windows.Size. It loads 1 assembly in 1.5 ms and does not add much process memory.
//public record struct SizeD
//{
//	[System.Text.Json.Serialization.JsonInclude]
//	public double width;
//	[System.Text.Json.Serialization.JsonInclude]
//	public double height;

//	public SizeD(double width, double height) { this.width = width; this.height = height; }
//}

/// <summary>
/// Used with <see cref="toolbar.Offsets"/>.
/// </summary>
public record struct TBOffsets {
	/// <summary>
	/// Horizontal distance from the owner's left edge (right if <see cref="TBAnchor.OppositeEdgeX"/>) to the toolbar's left edge.
	/// </summary>
	public double Left { get; set; }

	/// <summary>
	/// Vertical distance from the owner's top edge (bottom if <see cref="TBAnchor.OppositeEdgeY"/>) to the toolbar's top edge.
	/// </summary>
	public double Top { get; set; }

	/// <summary>
	/// Horizontal distance from the toolbar's right edge to the owner's right edge (left if <see cref="TBAnchor.OppositeEdgeX"/>).
	/// </summary>
	public double Right { get; set; }

	/// <summary>
	/// Vertical distance from the toolbar's bottom edge to the owner's bottom edge (top if <see cref="TBAnchor.OppositeEdgeY"/>).
	/// </summary>
	public double Bottom { get; set; }

	/// <summary>
	/// Sets all properties.
	/// </summary>
	public TBOffsets(double left, double top, double right, double bottom) {
		Left = left; Top = top; Right = right; Bottom = bottom;
	}
}

/// <summary>
/// Reasons to hide a toolbar. Used with <see cref="toolbar.Hide"/>.
/// </summary>
[Flags]
public enum TBHide {
	/// <summary>Owner window is hidden, minimized, etc.</summary>
	Owner = 1,

	/// <summary>A full-screen window is active. See flag <see cref="TBFlags.HideWhenFullScreen"/>.</summary>
	FullScreen = 2,

	//Satellite = 128, //no, _SetVisible and this enum aren't used with satellites

	/// <summary>This and bigger flag values can be used by callers for any purpose. Value 0x10000.</summary>
	User = 0x10000,
}

/// <summary>
/// Used with <see cref="toolbar.Layout"/>.
/// </summary>
public enum TBLayout {
	/// <summary>Default layout. Buttons are in single row. Wrapped when exceeds maximal row width. More rows can be added with <see cref="toolbar.Group"/>.</summary>
	HorizontalWrap,

	/// <summary>Buttons are in single column, like in a popup menu. Separators are horizontal.</summary>
	Vertical, //SHOULDDO: if some buttons don't fit, add overflow drop-down menu. Or scrollbar; or add VerticalScroll.

	//	/// <summary>Buttons are in single row. When it exceeds maximal row width, buttons are moved to a drop-down menu. More rows can be added with <see cref="toolbar.Group"/>.</summary>
	//	Horizontal,//SHOULDDO
}

/// <summary>
/// Used with <see cref="toolbar.NoContextMenu"/>.
/// </summary>
[Flags]
public enum TBNoMenu {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	Menu = 1,
	Edit = 1 << 1,
	Anchor = 1 << 2,
	Layout = 1 << 3,
	Border = 1 << 4,
	Sizable = 1 << 5,
	AutoSize = 1 << 6,
	MiscFlags = 1 << 7,
	Toolbars = 1 << 8,
	Help = 1 << 9,
	Close = 1 << 10,
	File = 1 << 11,
	Text = 1 << 12,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// Flags for <see cref="toolbar"/> constructor.
/// </summary>
[Flags]
public enum TBCtor {
	/// <summary>
	/// Don't load saved settings. Delete the settings file of the toolbar, if exists.
	/// </summary>
	ResetSettings = 1,

	/// <summary>
	/// Don't load and save settings. No file will be created or opened.
	/// </summary>
	DontSaveSettings = 2,
}

/// <summary>
/// Used with <see cref="toolbar.DpiScaling"/>.
/// </summary>
public struct TBScaling {
	///
	public TBScaling(bool? size, bool? offsets) { this.size = size; this.offsets = offsets; }

	/// <summary>
	/// Scale toolbar size and related properties.
	/// If default (<c>null</c>), scales size, except of empty toolbars created by <see cref="toolbar.AutoHideScreenEdge"/>.
	/// </summary>
	public bool? size;

	/// <summary>
	/// Scale toolbar offsets. See <see cref="toolbar.Offsets"/>.
	/// If default (<c>null</c>), scales offsets, except when anchor is screen (not window etc).
	/// </summary>
	public bool? offsets;
}

/// <summary>
/// Used with <see cref="toolbar.Metrics"/> and <see cref="toolbar.defaultMetrics"/>.
/// </summary>
/// <param name="ButtonPaddingX">Button padding left and right.</param>
/// <param name="ButtonPaddingY">Button padding top and bottom.</param>
/// <remarks>
/// All values are in logical pixels (1 pixel when DPI is 100%).
/// </remarks>
public record class TBMetrics(int ButtonPaddingX = 0, int ButtonPaddingY = 0);

/// <summary>
/// Used with <see cref="toolbar.Show(wnd, ITBOwnerObject)"/>.
/// </summary>
/// <remarks>
/// Allows a toolbar to follow an object in the owner window, for example a UI element or image. Or to hide in certain conditions.
/// Define a class that implements this interface. Create a variable of that class and pass it to <see cref="toolbar.Show(wnd, ITBOwnerObject)"/>.
/// The interface functions are called every 250 ms, sometimes more frequently. Not called when the owner window is invisible or cloaked or minimized.
/// </remarks>
public interface ITBOwnerObject {
	/// <summary>
	/// Returns <c>false</c> to close the toolbar.
	/// </summary>
	/// <remarks>
	/// Not called if the owner window is invisible or cloaked or minimized.
	/// The default implementation returns <c>true</c>.
	/// </remarks>
	bool IsAlive => true;

	/// <summary>
	/// Returns <c>false</c> to hide the toolbar temporarily.
	/// </summary>
	/// <remarks>
	/// Not called if the owner window is invisible or cloaked or minimized.
	/// The default implementation returns <c>true</c>.
	/// </remarks>
	bool IsVisible => true;

	/// <summary>
	/// Gets object rectangle.
	/// </summary>
	/// <returns><c>false</c> if failed.</returns>
	/// <param name="r">Rectangle in screen coordinates.</param>
	/// <remarks>
	/// Not called if the owner window is invisible or cloaked or minimized or if <see cref="IsVisible"/> returned <c>false</c>.
	/// </remarks>
	bool GetRect(out RECT r);
}