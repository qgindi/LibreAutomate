namespace Au.Types;

/// <summary>
/// <i>button</i> parameter type for <see cref="mouse.clickEx(MButton, bool)"/> and similar functions.
/// </summary>
/// <remarks>
/// There are two groups of values:
/// 1. Button (<c>Left</c>, <c>Right</c>, <c>Middle</c>, <c>X1</c>, <c>X2</c>). Default or 0: <c>Left</c>.
/// 2. Action (<c>Down</c>, <c>Up</c>, <c>DoubleClick</c>). Default: click.
/// 
/// Multiple values from the same group cannot be combined. For example <c>Left|Right</c> is invalid.
/// Values from different groups can be combined. For example <c>Right|Down</c>.
/// </remarks>
[Flags]
public enum MButton {
	/// <summary>The left button.</summary>
	Left = 1,
	
	/// <summary>The right button.</summary>
	Right = 2,
	
	/// <summary>The middle button.</summary>
	Middle = 4,
	
	/// <summary>The 4-th button.</summary>
	X1 = 8,
	
	/// <summary>The 5-th button.</summary>
	X2 = 16,
	
	//rejected: not necessary. Can be confusing.
	///// <summary>
	///// Click (press and release).
	///// This is default. Value 0.
	///// </summary>
	//Click = 0,
	
	/// <summary>(flag) Press and don't release.</summary>
	Down = 32,
	
	/// <summary>(flag) Don't press, only release.</summary>
	Up = 64,
	
	/// <summary>(flag) Double-click.</summary>
	DoubleClick = 128,
}

/// <summary>
/// Flags for mouse buttons.
/// Used with functions that check mouse button states (pressed or not).
/// </summary>
/// <remarks>
/// The values are the same as <see cref="System.Windows.Forms.MouseButtons"/>, therefore can be cast to/from.
/// </remarks>
[Flags]
public enum MButtons {
	/// <summary>The left button.</summary>
	Left = 0x00100000,
	
	/// <summary>The right button.</summary>
	Right = 0x00200000,
	
	/// <summary>The middle button.</summary>
	Middle = 0x00400000,
	
	/// <summary>The 4-th button.</summary>
	X1 = 0x00800000,
	
	/// <summary>The 5-th button.</summary>
	X2 = 0x01000000,
}

/// <summary>
/// At the end of <c>using(...) { ... }</c> block releases mouse buttons pressed by the function that returned this variable. See example.
/// </summary>
/// <example>
/// Drag and drop: start at x=8 y=8, move 20 pixels down, drop.
/// <code><![CDATA[
/// using(mouse.leftDown(w, 8, 8)) mouse.moveBy(0, 20); //the button is auto-released when the 'using' code block ends
/// ]]></code>
/// </example>
public struct MRelease : IDisposable {
	MButton _buttons;
	///
	public static implicit operator MRelease(MButton b) => new MRelease() { _buttons = b };
	
	/// <summary>
	/// Releases mouse buttons pressed by the function that returned this variable.
	/// </summary>
	public void Dispose() {
		if (0 == (_buttons & MButton.Down)) return;
		if (0 != (_buttons & MButton.Left)) mouse.clickEx(MButton.Left | MButton.Up, true);
		if (0 != (_buttons & MButton.Right)) mouse.clickEx(MButton.Right | MButton.Up, true);
		if (0 != (_buttons & MButton.Middle)) mouse.clickEx(MButton.Middle | MButton.Up, true);
		if (0 != (_buttons & MButton.X1)) mouse.clickEx(MButton.X1 | MButton.Up, true);
		if (0 != (_buttons & MButton.X2)) mouse.clickEx(MButton.X2 | MButton.Up, true);
	}
}

/// <summary>
/// Standard cursor ids.
/// Used with <see cref="mouse.waitForCursor(Seconds, MCursor, bool)"/>.
/// </summary>
public enum MCursor {
	/// <summary>Standard arrow.</summary>
	Arrow = 32512,
	
	/// <summary>I-beam (text editing).</summary>
	IBeam = 32513,
	
	/// <summary>Hourglass.</summary>
	Wait = 32514,
	
	/// <summary>Crosshair.</summary>
	Cross = 32515,
	
	/// <summary>Vertical arrow.</summary>
	UpArrow = 32516,
	
	/// <summary>Double-pointed arrow pointing northwest and southeast.</summary>
	SizeNWSE = 32642,
	
	/// <summary>Double-pointed arrow pointing northeast and southwest.</summary>
	SizeNESW = 32643,
	
	/// <summary>Double-pointed arrow pointing west and east.</summary>
	SizeWE = 32644,
	
	/// <summary>Double-pointed arrow pointing north and south.</summary>
	SizeNS = 32645,
	
	/// <summary>Four-pointed arrow pointing north, south, east, and west.</summary>
	SizeAll = 32646,
	
	/// <summary>Slashed circle.</summary>
	No = 32648,
	
	/// <summary>Hand.</summary>
	Hand = 32649,
	
	/// <summary>Standard arrow and small hourglass.</summary>
	AppStarting = 32650,
	
	/// <summary>Arrow and question mark.</summary>
	Help = 32651,
}

/// <summary>
/// This type is used for parameters of <see cref="mouse"/> functions that accept multiple types of UI objects (window, UI element, screen, etc).
/// </summary>
/// <remarks>
/// Has implicit conversions from <see cref="wnd"/>, <see cref="elm"/>, <see cref="uiimage"/>, <see cref="screen"/>, <see cref="RECT"/> and <c>bool</c> (relative coordinates).
/// Also has static functions to specify more parameters.
/// </remarks>
public struct MObject {
	object _o;
	MObject(object o) => _o = o;
	
	///
	public object Value => _o;
	
	/// <summary>
	/// Allows to specify coordinates in the client area of a window or control.
	/// </summary>
	/// <exception cref="AuWndException">The window handle is 0.</exception>
	/// <seealso cref="Window(wnd, bool)"/>
	public static implicit operator MObject(wnd w) { w.ThrowIf0(); return new(w); }
	
	/// <summary>
	/// Allows to specify coordinates in the rectangle of a UI element.
	/// </summary>
	/// <exception cref="ArgumentNullException"/>
	public static implicit operator MObject(elm e) => new(Not_.NullRet(e));
	
	/// <summary>
	/// Allows to specify coordinates in the rectangle of an image found in a window etc.
	/// </summary>
	/// <exception cref="ArgumentNullException"/>
	public static implicit operator MObject(uiimage i) => new(Not_.NullRet(i));
	
	/// <summary>
	/// Allows to specify coordinates in a rectangle anywhere on screen.
	/// </summary>
	/// <seealso cref="RectInWindow(wnd, RECT)"/>
	public static implicit operator MObject(RECT r) => new(r);
	
	/// <summary>
	/// Allows to specify coordinates in a screen.
	/// </summary>
	/// <seealso cref="Screen(screen, bool)"/>
	public static implicit operator MObject(screen s) => new((s, false));
	
	/// <summary>
	/// Allows to specify coordinates relative to <see cref="mouse.xy"/> or <see cref="mouse.lastXY"/>.
	/// </summary>
	public static implicit operator MObject(bool useLastXY) => new(useLastXY);
	
	/// <summary>
	/// Allows to specify coordinates in a screen, either in the work area or in entire rectangle.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// mouse.move(MObject.Screen(screen.primary, true), ^10, ^10); //near the bottom-right corner of the work area of the primary screen
	/// ]]></code>
	/// </example>
	public static MObject Screen(screen s, bool workArea) => new((s, workArea));
	
	/// <summary>
	/// Allows to specify coordinates in a window or control, either in the client area or in entire rectangle.
	/// </summary>
	/// <exception cref="AuWndException">The window handle is 0.</exception>
	public static MObject Window(wnd w, bool nonClient) { w.ThrowIf0(); return new((w, true)); }
	
	/// <summary>
	/// Allows to specify coordinates in a rectangle in the client area of a window or control.
	/// </summary>
	/// <exception cref="AuWndException">The window handle is 0.</exception>
	public static MObject RectInWindow(wnd w, RECT r) { w.ThrowIf0(); return new((w, r)); }
}
