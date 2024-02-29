using System.Drawing;

namespace Au.Types;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
/// <summary>
/// Defines the search area for <see cref="uiimage.find"/> and similar functions.
/// </summary>
/// <remarks>
/// It can be a window/control, UI element, image or a rectangle in screen.
/// Has implicit conversions from <b>wnd</b>, <b>elm</b>, <b>Bitmap</b> and <b>RECT</b> (rectangle in screen).
/// Constructors can be used to specify a rectangle in window or UI element, which makes the area smaller and the function faster.
/// Example: <c>uiimage.find(new(w, (left, top, width, height)), image);</c>.
/// </remarks>
public class IFArea {
	internal enum AreaType : byte { Screen, Wnd, Elm, Bitmap }

	internal AreaType Type;
	readonly bool _hasRect, _hasCoord;
	internal wnd W;
	internal elm E;
	internal Bitmap B;
	RECT _r;
	readonly Coord _cLeft, _cTop, _cRight, _cBottom;

	IFArea(AreaType t) { Type = t; }

	/// <summary>Specifies a window or control and a rectangle in its client area.</summary>
	public IFArea(wnd w, RECT r) { Type = AreaType.Wnd; W = w; _r = r; _hasRect = true; }

	/// <summary>Specifies a UI element and a rectangle in it.</summary>
	public IFArea(elm e, RECT r) { Type = AreaType.Elm; E = e; _r = r; _hasRect = true; }

	//rejected. Rare etc.
	//public IFArea(Bitmap b, RECT r) { ... }

	/// <summary>
	/// Specifies a window or control and a rectangle in its client area.
	/// The parameters are of <see cref="Coord"/> type, therefore can be easily specified reverse and fractional coordinates, like <c>^10</c> and <c>.5f</c>. Use <c>^0</c> for right or bottom edge.
	/// </summary>
	public IFArea(wnd w, Coord left, Coord top, Coord right, Coord bottom) {
		Type = AreaType.Wnd;
		W = w;
		_cLeft = left;
		_cTop = top;
		_cRight = right;
		_cBottom = bottom;
		_hasRect = _hasCoord = true;
	}

	/// <summary>
	/// Specifies a UI element and a rectangle in it.
	/// The parameters are of <see cref="Coord"/> type, therefore can be easily specified reverse and fractional coordinates, like <c>^10</c> and <c>.5f</c>. Use <c>^0</c> for right or bottom edge.
	/// </summary>
	public IFArea(elm e, Coord left, Coord top, Coord right, Coord bottom) {
		Type = AreaType.Elm;
		E = e;
		_cLeft = left;
		_cTop = top;
		_cRight = right;
		_cBottom = bottom;
		_hasRect = _hasCoord = true;
	}

	public static implicit operator IFArea(wnd w) => new(AreaType.Wnd) { W = w };
	public static implicit operator IFArea(elm e) => new(AreaType.Elm) { E = e };
	public static implicit operator IFArea(Bitmap b) => new(AreaType.Bitmap) { B = b };
	public static implicit operator IFArea(RECT r) => new(AreaType.Screen) { _r = r };

	internal void Before_(bool windowPixels) {
		if (windowPixels && Type is AreaType.Screen or AreaType.Bitmap) throw new ArgumentException("Invalid flags for this area type");

		switch (Type) {
		case AreaType.Wnd:
			W.ThrowIfInvalid();
			break;
		case AreaType.Elm:
			if (E == null) throw new ArgumentNullException("area elm");
			W = E.WndContainer;
			goto case AreaType.Wnd;
		case AreaType.Bitmap:
			if (B == null) throw new ArgumentNullException("area Bitmap");
			break;
		}
	}

	internal bool GetRect_(out RECT r, out POINT resultOffset, IFFlags flags) {
		r = default;
		resultOffset = default;

		if (!W.Is0) {
			if (!W.IsVisible || W.IsMinimized) return false;
			if (!flags.HasAny(IFFlags.WindowDC | IFFlags.PrintWindow) && W.IsCloaked) return false;
		}

		//Get area rectangle.
		bool failed = false;
		switch (Type) {
		case AreaType.Wnd:
			failed = !W.GetClientRect(out r);
			break;
		case AreaType.Elm:
			failed = !E.GetRect(out r, W);
			break;
		case AreaType.Bitmap:
			r = new RECT(0, 0, B.Width, B.Height);
			break;
		default: //Screen
			r = _r;
			if (!screen.isInAnyScreen(r)) r = default;
			resultOffset.x = r.left; resultOffset.y = r.top;
			break;
		}
		if (failed) {
			W.ThrowIfInvalid();
			throw new AuException("*get rectangle");
		}

		//r is the area from where to get pixels. If Wnd or Elm, it is relative to W client area.
		//Intermediate results will be relative to r. Then will be added resultOffset if need.

		if (_hasRect) {
			RECT rr;
			if (_hasCoord) {
				RECT rc = new(0, 0, r.Width, r.Height);
				POINT p1 = Coord.NormalizeInRect(_cLeft, _cTop, rc), p2 = Coord.NormalizeInRect(_cRight, _cBottom, rc);
				rr = RECT.FromLTRB(p1.x, p1.y, p2.x, p2.y);
			} else {
				rr = _r;
			}
			resultOffset.x = rr.left; resultOffset.y = rr.top;
			rr.Offset(r.left, r.top);
			r.Intersect(rr);
		}

		if (Type == AreaType.Elm) {
			//adjust r and resultOffset,
			//	because object rectangle may be bigger than client area (eg WINDOW object)
			//	or its part is not in client area (eg scrolled web page).
			//	If not adjusted, then may capture part of parent or sibling controls or even other windows...
			//	Never mind: should also adjust control rectangle in ancestors in the same way.
			//		This is not so important because usually whole control is visible (resized, not clipped).
			int x = r.left, y = r.top;
			W.GetClientRect(out var rw);
			r.Intersect(rw);
			x -= r.left; y -= r.top;
			resultOffset.x -= x; resultOffset.y -= y;
		}

		return !r.NoArea; //never mind: if WaitChanged and this is the first time, immediately returns 'changed'
	}

	/// <summary>
	/// Calls GetRect_, and CaptureScreen.Image if need.
	/// </summary>
	/// <returns><c>false</c> if GetRect_ returns <c>false</c> (empty rect).</returns>
	internal bool GetOcrData_(OcrFlags flags, out Bitmap b, out POINT resultOffset) {
		if (Type == AreaType.Bitmap) { b = B; resultOffset = default; return true; }
		if (!GetRect_(out RECT r, out resultOffset, (IFFlags)flags)) { b = null; return false; }
		b = Type == AreaType.Screen ? CaptureScreen.Image(r) : CaptureScreen.Image(W, r, flags.ToCIFlags_());
		return true;
	}

	/// <summary>
	/// If <i>scale</i>!=0, returns <i>scale</i>. Else returns 1...2 depending on <i>engine</i> and <i>area</i> DPI.
	/// </summary>
	internal double GetOcrScale_(double scale, IOcrEngine engine) {
		if (scale != 0) return scale;
		if (engine.DpiScale) {
			int dpi = Type switch {
				AreaType.Wnd or AreaType.Elm => Dpi.OfWindow(W),
				AreaType.Screen => screen.of(_r).Dpi,
				_ => 200
			};
			if (dpi < 192) return 192d / dpi;
		}
		return 1d;
	}
}

/// <summary>
/// Image(s) or color(s) for <see cref="uiimage.find"/> and similar functions.
/// </summary>
/// <remarks>
/// Has implicit conversions from:
/// - string - path of .png or .bmp file. If not full path, uses <see cref="folders.ThisAppImages"/>.
/// - string that starts with <c>"resources/"</c> or has prefix <c>"resource:"</c> - resource name; see <see cref="ResourceUtil.GetGdipBitmap"/>.
/// - string with prefix <c>"image:"</c> - Base64 encoded .png image.\
///   Can be created with dialog "Find image or color in window" or with function <b>Au.Controls.KImageUtil.ImageToString</b> (in Au.Controls.dll).
/// - <see cref="ColorInt"/>, <b>int</b> or <b>uint</b> in 0xRRGGBB color format, <b>Color</b> - color. Alpha isn't used.
/// - <see cref="Bitmap"/> - image object.
/// - <b>IFImage[]</b> - multiple images or/and colors. Action - find any. To create a different action can be used callback function (parameter <i>also</i>).
/// 
/// Icons are not supported directly; you can use <see cref="icon"/> to get icon and convert to bitmap.
/// </remarks>
public struct IFImage {
	readonly object _o;
	IFImage(object o) { _o = o; }

	public static implicit operator IFImage(string pathEtc) => new(pathEtc);
	public static implicit operator IFImage(Bitmap image) => new(image);
	public static implicit operator IFImage(ColorInt color) => new(color);
	public static implicit operator IFImage(int color) => new((ColorInt)color);
	public static implicit operator IFImage(uint color) => new((ColorInt)color);
	public static implicit operator IFImage(Color color) => new((ColorInt)color);
	public static implicit operator IFImage(System.Windows.Media.Color color) => new((ColorInt)color);
	//public static implicit operator IFImage(IEnumerable<IFImage> list) => new(list); //error: cannot convert from interfaces
	public static implicit operator IFImage(IFImage[] list) => new(list);
	//public static implicit operator IFImage(List<IFImage> list) => new(list); //rare, can use ToArray()

	/// <summary>
	/// Gets the raw value stored in this variable. Can be <b>string</b>, <b>Bitmap</b>, <b>ColorInt</b>, <b>IFImage[]</b>, <c>null</c>.
	/// </summary>
	public object Value => _o;
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Flags for <see cref="uiimage.find"/> and similar functions.
/// </summary>
[Flags]
public enum IFFlags {
	/// <summary>
	/// Get pixels from the device context (DC) of the window client area, not from screen DC. Usually much faster.
	/// Can get pixels from window parts that are covered by other windows or offscreen. But not from hidden and minimized windows.
	/// Does not work on Windows 7 if Aero theme is turned off. Then this flag is ignored.
	/// Cannot find images in some windows (including Windows Store apps), and in some window parts (glass). All pixels captured from these windows/parts are black.
	/// If the window is partially or completely transparent, the image must be captured from its non-transparent version.
	/// If the window is DPI-scaled, the image must be captured from its non-scaled version. However if used a limiting rectangle, it must contain scaled coordinates.
	/// </summary>
	WindowDC = 1,

	/// <summary>
	/// Use API <msdn>PrintWindow</msdn> to get window pixels.
	/// Like <b>WindowDC</b>, works with background windows, etc. Differences:
	/// <br/>• On Windows 8.1 and later works with windows where <b>WindowDC</b> doesn't.
	/// <br/>• Works without Aero theme too.
	/// <br/>• Slower.
	/// <br/>• Some windows flicker.
	/// <br/>• From some controls randomly gets partial image (API bug).
	/// <br/>• Does not work with windows of higher [](xref:uac) integrity level (throws exception).
	/// <br/>• Unreliable with DPI-scaled windows; the window image slightly changes when resizing etc.
	/// </summary>
	PrintWindow = 2,
	//rejected, has no sense: /// <br/>• If used together with flag <b>WindowDC</b>, calls <b>PrintWindow</b> without flag <b>PW_RENDERFULLCONTENT</b>. Faster, less flickering, no bugs, but has the <b>WindowDC</b> problem: can't get pixels from some windows or window parts.

	///// <summary>
	///// Use DWM thumbnail API to get window pixels.
	///// Like <b>WindowDC</b>, works with background windows, etc. Differences:
	///// <br/>• Works with windows where <b>WindowDC</b> doesn't.
	///// <br/>• Requires Windows 10 or later. Exception if used on older OS.
	///// <br/>• Slower.
	///// <br/>• May not work with some windows (rare).
	///// </summary>
	//WindowDwm = 4,

	//note: the above values must be the same in CIFlags, CIUFlags, IFFlags, OcrFlags.

	/// <summary>
	/// This flag can make the function faster when <i>image</i> is a list of images. To search for each image, the function will use <see cref="Parallel.For"/> instead of <b>for</b>. For example, if the CPU has 4 cores (8 threads), can search for max 8 images simultaneously. However it does not mean it will be 8 times faster. Can be max 2 or 3 times faster, depending on the number of images, flag <b>WindowDC</b>, <i>diff</i>, <i>also</i>, CPU, RAM, area size, finds or not, image position, etc. Can be even slower. To measure speed, use <see cref="perf"/>.
	/// If used <i>also</i> callback function, it runs in any thread and any order, but one at a time (inside <c>lock() { }</c>).
	/// </summary>
	Parallel = 0x100

	//rejected: this was used in QM2. Now can use png alpha instead.
	///// <summary>
	///// Use the top-left pixel color of the image as transparent color (don't compare pixels that have this color).
	///// </summary>
	//MakeTransparent = ,
}

/// <summary>
/// Used with <see cref="uiimage.find"/> and <see cref="uiimage.wait"/>. Its callback function (parameter <i>also</i>) can return one of these values.
/// </summary>
public enum IFAlso {
	/// <summary>
	/// Stop searching.
	/// Let the main function return current result.
	/// </summary>
	OkReturn,

	/// <summary>
	/// Find more instances of current image. If used list of images, also search for other images.
	/// Then let the main function return current result.
	/// </summary>
	OkFindMore,

	/// <summary>
	/// Find more instances of current image. When used list of images, don't search for other images.
	/// Then let the main function return current result.
	/// </summary>
	OkFindMoreOfThis,

	/// <summary>
	/// If used list of images, search for other images. Don't search for more instances of current image.
	/// Then let the main function return current result.
	/// </summary>
	OkFindMoreOfList,

	/// <summary>
	/// Stop searching.
	/// Let the main function return <c>null</c> or throw exception or continue waiting. But if a <b>OkFindX</b> value used previously, return that result.
	/// </summary>
	NotFound,

	/// <summary>
	/// Find more instances of current image. If used list of images, also search for other images.
	/// If not found, let the main function return <c>null</c> or throw exception or continue waiting; but if a <b>OkFindX</b> value used previously, return that result.
	/// </summary>
	FindOther,

	/// <summary>
	/// Find more instances of current image. When used list of images, don't search for other images.
	/// If not found, let the main function return <c>null</c> or throw exception or continue waiting; but if a <b>OkFindX</b> value used previously, return that result.
	/// </summary>
	FindOtherOfThis,

	/// <summary>
	/// If used list of images, search for other images. Don't search for more instances of current image.
	/// If not found, let the main function return <c>null</c> or throw exception or continue waiting; but if a <b>OkFindX</b> value used previously, return that result.
	/// </summary>
	FindOtherOfList,
}
