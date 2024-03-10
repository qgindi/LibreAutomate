using System.Drawing;

namespace Au;

/// <summary>
/// OCR functions. Recognizes text on screen or in image, gets word positions, finds text, clicks found words.
/// </summary>
public class ocr {
	readonly IFArea _area;

	static regexp s_rx1 = new(@"\s+");

	internal ocr(OcrWord[] words, IFArea area) {
		_area = area;
		Words = words;
		TextForFind = _BuildText(true);
	}

	/// <summary>
	/// Recognized words (text, rectangle, etc).
	/// </summary>
	public OcrWord[] Words { get; private set; }

	/// <summary>
	/// Recognized text as single line.
	/// Any whitespace between words is replaced with single space.
	/// <see cref="find"/> and similar functions search in this text.
	/// </summary>
	public string TextForFind { get; private set; }

	/// <summary>
	/// Recognized text.
	/// </summary>
	public string Text => _textML ??= _BuildText(false);
	string _textML;

	string _BuildText(bool forFind) {
		var b = new StringBuilder();
		foreach (var word in Words) {
			var s = word.Separator;
			if (forFind) if (!s.NE() && s != " ") s = s is "\r\n" or "\n" ? " " : s_rx1.Replace(s, " "); //replace any whitespace with " "
			b.Append(s);
			if (forFind) word.Offset = b.Length;
			b.Append(word.Text);
		}
		return b.ToString();
	}

	/// <summary>
	/// Index (in <see cref="Words"/>) of the first found word.
	/// If did not search for text (for example called <see cref="recognize"/>), this property is 0.
	/// </summary>
	public int FoundWordIndex { get; internal set; }

	/// <summary>
	/// Range of found text in <see cref="TextForFind"/>.
	/// </summary>
	public StartEnd FoundTextRange { get; internal set; }

	//internal void SortResults_() {
	//	Array.Sort(Words, (x, y) => {
	//		if (y.Rect.top >= x.Rect.bottom) return -1;
	//		if (x.Rect.top >= y.Rect.bottom) return 1;
	//		return x.Rect.left - y.Rect.left;
	//	});
	//}

	internal void AdjustResults_(POINT resultOffset, OcrFlags flags) {
		if (flags.HasAny(OcrFlags.WindowDC | OcrFlags.PrintWindow) && Dpi.GetScalingInfo_(_area.W, out bool scaled, out _, out _) && scaled) {
			int d1 = screen.of(_area.W.Window).Dpi, d2 = Dpi.OfWindow(_area.W);
			foreach (ref var word in Words.AsSpan()) {
				var r = word.Rect;
				r.left = Math2.MulDiv(r.left, d1, d2);
				r.top = Math2.MulDiv(r.top, d1, d2);
				r.right = Math2.MulDiv(r.right, d1, d2);
				r.bottom = Math2.MulDiv(r.bottom, d1, d2);
				word.Rect = r;
			}
		}

		if (resultOffset.x != 0 || resultOffset.y != 0) {
			foreach (ref var word in Words.AsSpan()) {
				var r = word.Rect;
				r.Offset(resultOffset.x, resultOffset.y);
				word.Rect = r;
			}
		}
	}

	/// <summary>
	/// Gets or sets the default OCR engine.
	/// </summary>
	/// <remarks>
	/// If not set, the <c>get</c> function returns a static <see cref="OcrWin10"/> object. To use another OCR engine, create and assign an object of type <see cref="OcrWin10"/>, <see cref="OcrTesseract"/>, <see cref="OcrGoogleCloud"/>, <see cref="OcrMicrosoftAzure"/> or other class that implements <see cref="IOcrEngine"/>.
	/// </remarks>
	public static IOcrEngine engine {
		get => s_engine ??= new OcrWin10();
		set { s_engine = value; }
	}
	static IOcrEngine s_engine;

	/// <summary>
	/// Performs OCR (text recognition).
	/// </summary>
	/// <returns>
	/// Returns an <see cref="ocr"/> object that contains recognized words etc.
	/// Returns <c>null</c> if the area is empty.
	/// </returns>
	/// <inheritdoc cref="find(IFArea, string, OcrFlags, double, IOcrEngine, int)"/>
	/// <remarks>
	/// Captures image from screen or window (unless <i>area</i> is <b>Bitmap</b>) and passes it to the OCR engine (calls <see cref="IOcrEngine.Recognize"/>). Then creates and returns an <b>ocr</b> object that contains results.
	///
	/// The speed depends on engine, area size and amount of text.
	/// </remarks>
	public static ocr recognize(IFArea area, OcrFlags flags = 0, double scale = 0, IOcrEngine engine = null) {
		engine ??= ocr.engine;
		area.Before_(flags.HasAny(OcrFlags.WindowDC | OcrFlags.PrintWindow));
		if (!area.GetOcrData_(flags, out var b, out var resultOffset)) return null;
		scale = area.GetOcrScale_(scale, engine);
		var a = engine.Recognize(b, dispose: area.Type != IFArea.AreaType.Bitmap, scale);
		var r = new ocr(a, area);
		r.AdjustResults_(resultOffset, flags);
		return r;
	}

	/// <summary>
	/// Performs OCR (text recognition) and finds text in results.
	/// </summary>
	/// <returns>
	/// Returns an <see cref="ocr"/> object that contains the word index and can click it etc.
	/// Returns <c>null</c> if not found.
	/// </returns>
	/// <param name="area">
	/// On-screen area or image:
	/// <br/>• <see cref="wnd"/> - window or control (its client area).
	/// <br/>• <see cref="elm"/> - UI element.
	/// <br/>• <see cref="Bitmap"/> - image.
	/// <br/>• <see cref="RECT"/> - a rectangle area in screen.
	/// <br/>• <see cref="IFArea"/> - can contain <b>wnd</b>, <b>elm</b> or <b>Bitmap</b>. Also allows to specify a rectangle in it, which makes the area smaller and the function faster. Example: <c>new(w, (left, top, width, height))</c>.
	/// </param>
	/// <param name="text">
	/// Text to find in <see cref="TextForFind"/>. Can have prefix:
	/// <br/>• <c>"**r "</c> - PCRE regular expression. Example: <c>@"**r \bwhole words\b"</c>.
	/// <br/>• <c>"**R "</c> - .NET regular expression.
	/// <br/>• <c>"**i "</c> - case-insensitive.
	/// <br/>• <c>"**t "</c> - case-sensitive (default).
	/// </param>
	/// <param name="flags"></param>
	/// <param name="scale">
	/// Scale factor (how much to resize the image before performing OCR).
	/// Value 2 or 3 may improve results of OCR engine <see cref="OcrWin10"/> or <see cref="OcrTesseract"/>.
	/// If 0 (default), depends on engine's <see cref="IOcrEngine.DpiScale"/> and area's DPI.
	/// </param>
	/// <param name="engine">OCR engine. Default: <see cref="engine"/> (<see cref="OcrWin10"/> if not specified).</param>
	/// <param name="skip">Skip this count of found text instances.</param>
	/// <exception cref="AuWndException">Invalid window handle (the <i>area</i> argument).</exception>
	/// <exception cref="ArgumentException">An argument is/contains a <c>null</c>/invalid value.</exception>
	/// <exception cref="AuException">Something failed.</exception>
	/// <remarks>
	/// The function captures image from screen or window (unless area is <b>Bitmap</b>) and passes it to the OCR engine (calls <see cref="IOcrEngine.Recognize"/>). Then finds the specified text in results. If found, creates and returns an <b>ocr</b> object that contains results.
	///
	/// The speed depends on engine, area size and amount of text.
	/// </remarks>
	public static ocr find(IFArea area, string text, OcrFlags flags = 0, double scale = 0, IOcrEngine engine = null, int skip = 0)
		=> new ocrFinder(text, flags, scale, engine, skip).Find(area);

	/// <summary>
	/// Performs OCR (text recognition) and finds text in results. Can wait and throw <b>NotFoundException</b>.
	/// </summary>
	/// <returns>
	/// Returns an <see cref="ocr"/> object that contains the word index and can click it etc.
	/// If not found, throws exception or returns <c>null</c> (if <i>wait</i> negative).
	/// </returns>
	/// <param name="wait">The wait timeout, seconds. If 0, does not wait. If negative, does not throw exception when not found.</param>
	/// <exception cref="NotFoundException" />
	/// <exception cref="AuWndException">Invalid window handle (the area argument), or the window closed while waiting.</exception>
	/// <inheritdoc cref="find(IFArea, string, OcrFlags, double, IOcrEngine, int)"/>
	public static ocr find(Seconds wait, IFArea area, string text, OcrFlags flags = 0, double scale = 0, IOcrEngine engine = null, int skip = 0)
		=> new ocrFinder(text, flags, scale, engine, skip).Find(area, wait);

	/// <summary>
	/// Performs OCR (text recognition) and finds text in results. Waits until found.
	/// </summary>
	/// <returns>Returns an <see cref="ocr"/> object that contains the word index and can click it etc. On timeout returns <c>null</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <exception cref="TimeoutException"><i>timeout</i> time has expired (if &gt; 0).</exception>
	/// <exception cref="AuWndException">Invalid window handle (the area argument), or the window closed while waiting.</exception>
	/// <inheritdoc cref="find(IFArea, string, OcrFlags, double, IOcrEngine, int)"/>
	public static ocr wait(Seconds timeout, IFArea area, string text, OcrFlags flags = 0, double scale = 0, IOcrEngine engine = null, int skip = 0)
		=> new ocrFinder(text, flags, scale, engine, skip).Wait(timeout, area);

	/// <summary>
	/// Performs OCR (text recognition) and waits until the specified text does not exist in results.
	/// </summary>
	/// <returns>Returns <c>true</c>. On timeout returns <c>false</c> if <i>timeout</i> is negative; else exception.</returns>
	/// <param name="timeout">Timeout, seconds. Can be 0 (infinite), &gt;0 (exception) or &lt;0 (no exception). More info: [](xref:wait_timeout).</param>
	/// <inheritdoc cref="wait(Seconds, IFArea, string, OcrFlags, double, IOcrEngine, int)"/>
	public static bool waitNot(Seconds timeout, IFArea area, string text, OcrFlags flags = 0, double scale = 0, IOcrEngine engine = null, int skip = 0)
		=> new ocrFinder(text, flags, scale, engine, skip).WaitNot(timeout, area);

	/// <summary>
	/// Gets the rectangle of the found word.
	/// </summary>
	/// <param name="inScreen">Convert to screen coordinates. If <c>false</c>, it's in <i>area</i> coordinates (window client area, etc) without rectangle offset.</param>
	/// <param name="word">Word index offset from <see cref="FoundWordIndex"/>.</param>
	public RECT GetRect(bool inScreen, int word = 0) {
		int i = FoundWordIndex + word; if ((uint)i >= Words.Length) throw new ArgumentOutOfRangeException("word");
		var r = Words[i].Rect;
		if (inScreen) {
			if (_area.Type == IFArea.AreaType.Wnd) {
				_area.W.MapClientToScreen(ref r);
			} else if (_area.Type == IFArea.AreaType.Elm) {
				if (!_area.E.GetRect(out var rr)) return default;
				r.Offset(rr.left, rr.top);
			}
		}
		return r;
	}

	/// <summary>
	/// Moves the mouse to the found text (the first word).
	/// </summary>
	/// <param name="x">X coordinate in the word. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y coordinate in the word. Default - center.</param>
	/// <param name="word">Word index offset from <see cref="FoundWordIndex"/>.</param>
	/// <exception cref="InvalidOperationException"><i>area</i> is <b>Bitmap</b>.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="mouse.move(wnd, Coord, Coord, bool)"/>.</exception>
	/// <remarks>
	/// Calls <see cref="mouse.move(wnd, Coord, Coord, bool)"/>.
	/// </remarks>
	public void MouseMove(Coord x = default, Coord y = default, int word = 0) => _MouseAction(x, y, 0, word);

	/// <summary>
	/// Clicks the found text (the first word).
	/// </summary>
	/// <param name="x">X coordinate in the word. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y coordinate in the word. Default - center.</param>
	/// <param name="button">Which button and how to use it.</param>
	/// <param name="word">Word index offset from <see cref="FoundWordIndex"/>.</param>
	/// <exception cref="InvalidOperationException"><i>area</i> is <b>Bitmap</b>.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="mouse.clickEx(MButton, wnd, Coord, Coord, bool)"/>.</exception>
	/// <remarks>
	/// Calls <see cref="mouse.clickEx(MButton, wnd, Coord, Coord, bool)"/>.
	/// </remarks>
	public MRelease MouseClick(Coord x = default, Coord y = default, MButton button = MButton.Left, int word = 0) {
		_MouseAction(x, y, button == 0 ? MButton.Left : button, word);
		return button;
	}

	/// <summary>
	/// Double-clicks the found text (the first word).
	/// </summary>
	/// <inheritdoc cref="MouseClick(Coord, Coord, MButton, int)"/>
	public void MouseClickD(Coord x = default, Coord y = default, int word = 0) => MouseClick(x, y, MButton.DoubleClick, word);

	/// <summary>
	/// Right-clicks the found text (the first word).
	/// </summary>
	/// <inheritdoc cref="MouseClick(Coord, Coord, MButton, int)"/>
	public void MouseClickR(Coord x = default, Coord y = default, int word = 0) => MouseClick(x, y, MButton.Right, word);

	void _MouseAction(Coord x, Coord y, MButton button, int word) {
		if (_area.Type == IFArea.AreaType.Bitmap) throw new InvalidOperationException();

		var r = GetRect(false, word);
		if (r.NoArea) return;

		var p = Coord.NormalizeInRect(x, y, r, centerIfEmpty: true);

		if (_area.Type == IFArea.AreaType.Screen) {
			if (button == 0) mouse.move(p);
			else mouse.clickEx(button, p);
		} else {
			var w = _area.W;
			if (_area.Type == IFArea.AreaType.Elm) {
				if (!_area.E.GetRect(out var rr, w) || rr.NoArea) throw new AuException(0, "*get rectangle");
				p.x += rr.left; p.y += rr.top;
			}
			if (button == 0) mouse.move(w, p.x, p.y);
			else mouse.clickEx(button, w, p.x, p.y);
		}
	}

	/// <summary>
	/// Posts mouse-click messages to the window, using coordinates in the found text (the first word).
	/// </summary>
	/// <param name="x">X coordinate in the word. Default - center. Examples: <c>10</c>, <c>^10</c> (reverse), <c>.5f</c> (fraction).</param>
	/// <param name="y">Y coordinate in the word. Default - center.</param>
	/// <param name="button">Can specify the left (default), right or middle button. Also flag for double-click, press or release.</param>
	/// <param name="word">Word index offset from <see cref="FoundWordIndex"/>.</param>
	/// <exception cref="InvalidOperationException"><i>area</i> is <b>Bitmap</b> or <b>Screen</b>.</exception>
	/// <exception cref="AuException">Failed to get UI element rectangle (when searched in a UI element).</exception>
	/// <remarks>
	/// Does not move the mouse.
	/// Does not wait until the target application finishes processing the message.
	/// Works not with all elements.
	/// </remarks>
	public void PostClick(Coord x = default, Coord y = default, MButton button = MButton.Left, int word = 0) {
		var w = _area.W;
		if (w.Is0) throw new InvalidOperationException();

		var r = GetRect(false, word);
		if (r.NoArea) return;

		if (_area.Type == IFArea.AreaType.Elm) {
			if (!_area.E.GetRect(out var rr, w) || r.NoArea) throw new AuException(0, "*get rectangle");
			r.Offset(rr.left, rr.top);
		}

		mouse.PostClick_(w, r, x, y, button);
	}

	//rejected: PostClickD, PostClickR. Rarely used.
}
