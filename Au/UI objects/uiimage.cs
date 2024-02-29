using System.Drawing;

namespace Au;

/// <summary>
/// Captures, finds and clicks images and colors in windows.
/// </summary>
/// <remarks>
/// An image is any visible rectangular part of a window. A color is any visible pixel (the same as image of size 1x1).
/// A <b>uiimage</b> variable holds results of <see cref="find"/> and similar functions (rectangle etc).
/// </remarks>
public class uiimage {
	#region results

	readonly IFArea _area;

	///// <summary>
	///// <i>area</i> parameter of the function.
	///// </summary>
	//public IFArea Area => _area;

	internal uiimage(IFArea area) {
		_area = area;
	}

	/// <summary>
	/// Gets location of the found image, relative to the search area.
	/// </summary>
	/// <remarks>
	/// Relative to the window/control client area (if area type is <b>wnd</b>), UI element (if <b>elm</b>), image (if <b>Bitmap</b>) or screen (if <b>RECT</b>).
	/// More info: <see cref="find"/>.
	/// </remarks>
	public RECT Rect { get; init; }

	/// <summary>
	/// Gets location of the found image in screen coordinates.
	/// </summary>
	/// <remarks>
	/// Slower than <see cref="Rect"/>.
	/// </remarks>
	public RECT RectInScreen {
		get {
			RECT r;
			switch (_area.Type) {
			case IFArea.AreaType.Wnd:
				r = Rect;
				_area.W.MapClientToScreen(ref r);
				return r;
			case IFArea.AreaType.Elm:
				if (!_area.E.GetRect(out var rr)) return default;
				r = Rect;
				r.Offset(rr.left, rr.top);
				return r;
			}
			return Rect; //screen or bitmap
		}
	}

	/// <summary>
	/// Gets 0-based index of current matching image instance.
	/// </summary>
	/// <remarks>
	/// Can be useful in <i>also</i> callback functions.
	/// When the <i>image</i> argument is a list of images, <b>MatchIndex</b> starts from 0 for each list image.
	/// </remarks>
	public int MatchIndex { get; init; }

	/// <summary>
	/// When the <i>image</i> argument is a list of images, gets 0-based index of the list image.
	/// </summary>
	public int ListIndex { get; init; }

	/// <summary>
	/// Can be used in <i>also</i> callback function to skip <i>n</i> matching images. Example: <c>also: o => o.Skip(n)</c>.
	/// </summary>
	/// <param name="n">How many matching images to skip.</param>
	public IFAlso Skip(int n) => MatchIndex == n ? IFAlso.OkReturn : (MatchIndex < n ? IFAlso.FindOther : IFAlso.FindOtherOfList);

	/// <summary>
	/// Moves the mouse to the found image.
	/// </summary>
	/// <param name="x">X coordinate in the found image. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y coordinate in the found image. Default - center.</param>
	/// <exception cref="InvalidOperationException"><i>area</i> is <b>Bitmap</b>.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="mouse.move(wnd, Coord, Coord, bool)"/>.</exception>
	/// <remarks>
	/// Calls <see cref="mouse.move(wnd, Coord, Coord, bool)"/>.
	/// </remarks>
	public void MouseMove(Coord x = default, Coord y = default) => _MouseAction(x, y, 0);

	/// <summary>
	/// Clicks the found image.
	/// </summary>
	/// <param name="x">X coordinate in the found image. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y coordinate in the found image. Default - center.</param>
	/// <param name="button">Which button and how to use it.</param>
	/// <exception cref="InvalidOperationException"><i>area</i> is <b>Bitmap</b>.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="mouse.clickEx(MButton, wnd, Coord, Coord, bool)"/>.</exception>
	/// <remarks>
	/// Calls <see cref="mouse.clickEx(MButton, wnd, Coord, Coord, bool)"/>.
	/// </remarks>
	public MRelease MouseClick(Coord x = default, Coord y = default, MButton button = MButton.Left) {
		_MouseAction(x, y, button == 0 ? MButton.Left : button);
		return button;
	}

	/// <summary>
	/// Double-clicks the found image.
	/// </summary>
	/// <inheritdoc cref="MouseClick(Coord, Coord, MButton)"/>
	public void MouseClickD(Coord x = default, Coord y = default) => MouseClick(x, y, MButton.DoubleClick);

	/// <summary>
	/// Right-clicks the found image.
	/// </summary>
	/// <inheritdoc cref="MouseClick(Coord, Coord, MButton)"/>
	public void MouseClickR(Coord x = default, Coord y = default) => MouseClick(x, y, MButton.Right);

	void _MouseAction(Coord x, Coord y, MButton button) {
		if (_area.Type == IFArea.AreaType.Bitmap) throw new InvalidOperationException();

		Debug.Assert(!Rect.NoArea);
		if (Rect.NoArea) return;

		//rejected: Click will activate it. Don't activate if just Move.
		//if(0 != (_f._flags & IFFlags.WindowDC)) {
		//	if(_area.W.IsCloaked) _area.W.ActivateL();
		//}

		var p = Coord.NormalizeInRect(x, y, Rect, centerIfEmpty: true);

		if (_area.Type == IFArea.AreaType.Screen) {
			if (button == 0) mouse.move(p);
			else mouse.clickEx(button, p);
		} else {
			var w = _area.W;
			if (_area.Type == IFArea.AreaType.Elm) {
				if (!_area.E.GetRect(out var r, w) || r.NoArea) throw new AuException(0, "*get rectangle");
				p.x += r.left; p.y += r.top;
			}
			if (button == 0) mouse.move(w, p.x, p.y);
			else mouse.clickEx(button, w, p.x, p.y);
		}
	}

	/// <summary>
	/// Posts mouse-click messages to the window, using coordinates in the found image.
	/// </summary>
	/// <param name="button">Can specify the left (default), right or middle button. Also flag for double-click, press or release.</param>
	/// <exception cref="ArgumentException">Unsupported button specified.</exception>
	/// <inheritdoc cref="PostClickD(Coord, Coord)"/>
	public void PostClick(Coord x = default, Coord y = default, MButton button = MButton.Left) {
		var w = _area.W;
		if (w.Is0) throw new InvalidOperationException();

		Debug.Assert(!Rect.NoArea);
		if (Rect.NoArea) return;

		var r = Rect;
		if (_area.Type == IFArea.AreaType.Elm) {
			if (!_area.E.GetRect(out var rr, w) || r.NoArea) throw new AuException(0, "*get rectangle");
			r.Offset(rr.left, rr.top);
		}

		mouse.PostClick_(w, r, x, y, button);
	}

	/// <summary>
	/// Posts mouse-double-click messages to the window, using coordinates in the found image.
	/// </summary>
	/// <param name="x">X coordinate in the found image. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y coordinate in the found image. Default - center.</param>
	/// <exception cref="InvalidOperationException"><i>area</i> is <b>Bitmap</b> or <b>Screen</b>.</exception>
	/// <exception cref="AuException">Failed to get UI element rectangle (when searched in a UI element).</exception>
	/// <remarks>
	/// Does not move the mouse.
	/// Does not wait until the target application finishes processing the message.
	/// Works not with all elements.
	/// </remarks>
	public void PostClickD(Coord x = default, Coord y = default) => PostClick(x, y, MButton.DoubleClick);

	/// <summary>
	/// Posts mouse-right-click messages to the window, using coordinates in the found image.
	/// </summary>
	/// <inheritdoc cref="PostClickD(Coord, Coord)"/>
	public void PostClickR(Coord x = default, Coord y = default) => PostClick(x, y, MButton.Right);

	///
	public override string ToString() => $"{ListIndex}, {MatchIndex}, {Rect}";

	#endregion

	/// <summary>
	/// Finds image(s) or color(s) displayed in a window or other area.
	/// </summary>
	/// <returns>
	/// Returns a <see cref="uiimage"/> object that contains the rectangle of the found image and can click it etc.
	/// Returns <c>null</c> if not found.
	/// </returns>
	/// <param name="area">
	/// Where to search:
	/// <br/>• <see cref="wnd"/> - window or control (its client area).
	/// <br/>• <see cref="elm"/> - UI element.
	/// <br/>• <see cref="Bitmap"/> - image.
	/// <br/>• <see cref="RECT"/> - a rectangle area in screen.
	/// <br/>• <see cref="IFArea"/> - can contain <b>wnd</b>, <b>elm</b> or <b>Bitmap</b>. Also allows to specify a rectangle in it, which makes the area smaller and the function faster. Example: <c>new(w, (left, top, width, height))</c>.
	/// </param>
	/// <param name="image">Image or color to find. Or array of them. More info: <see cref="IFImage"/>.</param>
	/// <param name="flags"></param>
	/// <param name="diff">Maximal allowed color difference. Can be 0 - 100, but should be as small as possible. Use to find images with slightly different colors than the specified image.</param>
	/// <param name="also">
	/// Callback function. Called for each found image instance and receives its rectangle, match index and list index. Can return one of <see cref="IFAlso"/> values.
	/// <br/>Examples:
	/// <br/>• Skip 2 matching images: <c>also: o => o.Skip(2)</c>
	/// <br/>• Skip some matching images if some condition is <c>false</c>: <c>also: o => condition ? IFAlso.OkReturn : IFAlso.FindOther</c>
	/// <br/>• Get rectangles etc of all matching images: <c>also: o => { list.Add(o); return IFAlso.OkFindMore; }</c>
	/// <br/>• Do different actions depending on which list images found: <c>var found = new BitArray(images.Length); uiimage.find(w, images, also: o => { found[o.ListIndex] = true; return IFAlso.OkFindMoreOfList; }); if(found[0]) print.it(0); if(found[1]) print.it(1);</c>
	/// </param>
	/// <exception cref="AuWndException">Invalid window handle (the <i>area</i> argument).</exception>
	/// <exception cref="ArgumentException">An argument is/contains a <c>null</c>/invalid value.</exception>
	/// <exception cref="FileNotFoundException">Image file does not exist.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="ImageUtil.LoadGdipBitmap"/>.</exception>
	/// <exception cref="AuException">Something failed.</exception>
	/// <remarks>
	/// To create code for this function, use tool "Find image or color in window".
	/// 
	/// The speed mostly depends on:
	/// 1. The size of the search area. Use the smallest possible area (control or UI element or rectangle in window).
	/// 2. Flags <see cref="IFFlags.WindowDC"/> (makes faster), <see cref="IFFlags.PrintWindow"/>. The speed depends on window.
	/// 3. Video driver. Can be much slower if incorrect, generic or virtual PC driver is used. The above flags should help.
	/// 4. <i>diff</i>. Should be as small as possible.
	/// 
	/// If flag <see cref="IFFlags.WindowDC"/> or <see cref="IFFlags.PrintWindow"/> not used, the search area must be visible on the screen, because this function then gets pixels from the screen.
	/// 
	/// Can find only images that exactly match the specified image. With <i>diff</i> can find images with slightly different colors and brightness.
	/// 
	/// Transparent and partially transparent pixels of <i>image</i> are ignored. You can draw transparent areas with an image editor that supports it, for example Paint.NET.
	/// 
	/// This function is not the best way to find objects when the script is intended for long use or for use on multiple computers or must be very reliable. Because it may fail to find the image after changing some settings - system theme, application theme, text size (DPI), font smoothing (if the image contains text), etc. Also are possible various unexpected temporary conditions that may distort or hide the image, for example adjacent window shadow, a tooltip or some temporary window. If possible, in such scripts instead use other functions, eg find control or UI element.
	/// 
	/// Flags <see cref="IFFlags.WindowDC"/> and <see cref="IFFlags.PrintWindow"/> cannot be used if <i>area</i> is <b>Bitmap</b> or <b>RECT</b>.
	/// </remarks>
	/// <example>
	/// Code created with tool "Find image or color in window".
	/// <code><![CDATA[
	/// var w = wnd.find(1, "Window Name");
	/// string image = "image:iVBORw0KGgoAAAANSUhEUgAAABYAAAANCAYAAACtpZ5jAAAAAXNSR0IArs4c...";
	/// var im = uiimage.find(1, w, image);
	/// im.MouseClick();
	/// ]]></code>
	/// </example>
	public static uiimage find(IFArea area, IFImage image, IFFlags flags = 0, int diff = 0, Func<uiimage, IFAlso> also = null)
		=> new uiimageFinder(image, flags, diff, also).Find(area);

	/// <summary>
	/// Finds image(s) or color(s) displayed in a window or other area. Can wait and throw <b>NotFoundException</b>.
	/// </summary>
	/// <returns>
	/// Returns a <see cref="uiimage"/> object that contains the rectangle of the found image and can click it etc.
	/// If not found, throws exception or returns <c>null</c> (if <i>wait</i> negative).
	/// </returns>
	/// <param name="wait">The wait timeout, seconds. If 0, does not wait. If negative, does not throw exception when not found.</param>
	/// <exception cref="NotFoundException" />
	/// <exception cref="AuWndException">Invalid window handle (the area argument), or the window closed while waiting.</exception>
	/// <inheritdoc cref="find(IFArea, IFImage, IFFlags, int, Func{uiimage, IFAlso})"/>
	public static uiimage find(Seconds wait, IFArea area, IFImage image, IFFlags flags = 0, int diff = 0, Func<uiimage, IFAlso> also = null)
		=> new uiimageFinder(image, flags, diff, also).Find(area, wait);

	/// <summary>
	/// Finds image(s) or color(s) displayed in a window or other area. Waits until found.
	/// More info: <see cref="find"/>.
	/// </summary>
	/// <returns>Returns <see cref="uiimage"/> object containing the rectangle of the found image. On timeout returns <c>null</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <exception cref="AuWndException">Invalid window handle (the <i>area</i> argument), or the window closed while waiting.</exception>
	/// <inheritdoc cref="find(IFArea, IFImage, IFFlags, int, Func{uiimage, IFAlso})"/>
	public static uiimage wait(Seconds timeout, IFArea area, IFImage image, IFFlags flags = 0, int diff = 0, Func<uiimage, IFAlso> also = null)
		=> new uiimageFinder(image, flags, diff, also).Wait(timeout, area);

	/// <summary>
	/// Waits until image(s) or color(s) is not displayed in a window or other area.
	/// More info: <see cref="find"/>.
	/// </summary>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <inheritdoc cref="wait(Seconds, IFArea, IFImage, IFFlags, int, Func{uiimage, IFAlso})"/>
	public static bool waitNot(Seconds timeout, IFArea area, IFImage image, IFFlags flags = 0, int diff = 0, Func<uiimage, IFAlso> also = null)
		=> new uiimageFinder(image, flags, diff, also).WaitNot(timeout, area);

	/// <summary>
	/// Waits until something visually changes in a window or other area.
	/// More info: <see cref="find"/>.
	/// </summary>
	/// <remarks>
	/// Like <see cref="waitNot"/>, but instead of <i>image</i> parameter this function captures the area image at the beginning.
	/// </remarks>
	/// <inheritdoc cref="waitNot(Seconds, IFArea, IFImage, IFFlags, int, Func{uiimage, IFAlso})"/>
	public static bool waitChanged(Seconds timeout, IFArea area, IFFlags flags = 0, int diff = 0) {
		var f = new uiimageFinder(default, flags, diff, null);
		return f.Wait_(uiimageFinder.Action_.WaitChanged, timeout, area);
	}

	#region obsolete

	/// <inheritdoc cref="CaptureScreen.Image(RECT)"/>
	[Obsolete("Use CaptureScreen.Image"), EditorBrowsable(EditorBrowsableState.Never)]
	public static Bitmap capture(RECT r) => CaptureScreen.Image(r);

	/// <inheritdoc cref="CaptureScreen.Image(wnd, RECT?, CIFlags)"/>
	[Obsolete("Use CaptureScreen.Image"), EditorBrowsable(EditorBrowsableState.Never)]
	public static Bitmap capture(wnd w, RECT r, bool printWindow = false)
		=> CaptureScreen.Image(w, r, printWindow ? CIFlags.PrintWindow : CIFlags.WindowDC);

	/// <inheritdoc cref="CaptureScreen.Pixels(RECT)"/>
	[Obsolete("Use CaptureScreen.Pixels"), EditorBrowsable(EditorBrowsableState.Never)]
	public static uint[,] getPixels(RECT r) => CaptureScreen.Pixels(r);

	/// <inheritdoc cref="CaptureScreen.Pixels(wnd, RECT?, CIFlags)"/>
	[Obsolete("Use CaptureScreen.Pixels"), EditorBrowsable(EditorBrowsableState.Never)]
	public static uint[,] getPixels(wnd w, RECT r, bool printWindow = false)
		=> CaptureScreen.Pixels(w, r, printWindow ? CIFlags.PrintWindow : CIFlags.WindowDC);

	/// <inheritdoc cref="CaptureScreen.Pixel"/>
	[Obsolete("Use CaptureScreen.Pixel"), EditorBrowsable(EditorBrowsableState.Never)]
	public static unsafe uint getPixel(POINT p) => CaptureScreen.Pixel(p);

	/// <inheritdoc cref="CaptureScreen.ImageColorRectUI"/>
	[Obsolete("Use CaptureScreen.ImageColorRectUI"), EditorBrowsable(EditorBrowsableState.Never)]
	public static bool captureUI(out ICResult result, ICFlags flags = 0, AnyWnd owner = default) {
		bool R = CaptureScreen.ImageColorRectUI(out var r1, (CIUFlags)flags, owner);
		result = Unsafe.As<ICResult>(r1);
		return R;
	}

	#endregion
}
