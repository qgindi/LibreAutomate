namespace Au.Controls;

using static Sci;

public class SciTextBuilder {
	readonly StringBuilder _b = new();
	readonly List<(int marker, int pos)> _markers = new();
	readonly List<(int indic, int start, int end, int value)> _indicators = new();
	readonly List<(int indic, int start, int end, object data)> _links = new();
	readonly List<(int style, int start, int end)> _styles = new();
	readonly List<SciFoldPoint> _folding = new();
	Stack<(int indic, int start, int value)> _stackIndicator = new();
	Stack<(int indic, int start, object data)> _stackLink = new();
	Stack<(int style, int start)> _stackStyle = new();

	public void Clear() {
		_b.Clear();
		_markers.Clear();
		_indicators.Clear();
		_links.Clear();
		_styles.Clear();
		_folding.Clear();
		_stackIndicator.Clear();
		_stackLink.Clear();
		_stackStyle.Clear();
	}

	public unsafe void Apply(KScintilla sci) {
		var s = _b.ToString();
		sci.aaaSetText(s, ignoreTags: true);
		if (_styles.Count > 0) {
			//code like in GetScintillaStylingBytes
			var styles8 = new byte[Encoding.UTF8.GetByteCount(s)];
			var map8 = styles8.Length == s.Length ? null : Convert2.Utf8EncodeAndGetOffsets_(s).offsets;
			foreach (var v in _styles) {
				int i = v.start, end = v.end;
				if (map8 != null) { i = map8[i]; end = map8[end]; }
				while (i < end) styles8[i++] = (byte)v.style;
			}
			sci.Call(SCI_STARTSTYLING);
			fixed (byte* p = styles8) sci.Call(SCI_SETSTYLINGEX, styles8.Length, p);
		}
		foreach (var v in _markers) {
			sci.aaaMarkerAdd(v.marker, true, v.pos);
		}
		foreach (var v in _indicators.AsSpan()) {
			sci.aaaIndicatorAdd(v.indic, true, v.start..v.end, v.value);
		}
		foreach (var v in _links.AsSpan()) {
			var (start, end) = sci.aaaNormalizeRange(true, v.start..v.end);
			if (v.indic < 0) {
				sci.aaaIndicatorAdd(-v.indic, false, start..end);
				sci.aaaIndicatorAdd(-v.indic + 1, false, start..end);
			} else {
				sci.aaaIndicatorAdd(v.indic, false, start..end);
			}
			sci.AaRangeDataAdd(false, start..end, v.data);
		}
		if (_folding.Count > 0) sci.aaaFoldingApply(_folding);
	}

	/// <summary>
	/// Gets current text length.
	/// </summary>
	public int Length => _b.Length;

	/// <summary>
	/// Adds text.
	/// </summary>
	public SciTextBuilder Text(RStr text) {
		_b.Append(text);
		return this;
	}

	/// <summary>
	/// Adds <c>"\r\n"</c>.
	/// </summary>
	public SciTextBuilder NL() {
		_b.AppendLine();
		return this;
	}

	/// <summary>
	/// Adds marker in current or previous line.
	/// </summary>
	/// <param name="prevLine">Add at the line of position <c>Length - 2</c>.</param>
	public SciTextBuilder Marker(int marker, bool prevLine = false) {
		_markers.Add((marker, Length - (prevLine ? 2 : 0)));
		return this;
	}

	/// <summary>
	/// Adds indicator in a text range.
	/// </summary>
	/// <param name="indic">Indicator index, 0-31.</param>
	/// <exception cref="ArgumentOutOfRangeException"><i>range</i> is not within current text.</exception>
	public SciTextBuilder Indic(int indic, int start, int end, int value = 1) {
		_indicators.Add((indic, start, end, value));
		return this;
	}

	/// <summary>
	/// Adds text with indicator.
	/// </summary>
	/// <param name="indic">Indicator index, 0-31.</param>
	public SciTextBuilder Indic(int indic, RStr text, int value = 1) {
		int start = Length;
		_b.Append(text);
		return Indic(indic, start, Length, value);
	}

	/// <summary>
	/// Starts indicator range from current text length. Later use <see cref="Indic_"/> to end it.
	/// </summary>
	/// <param name="indic">Indicator index, 0-31.</param>
	public SciTextBuilder Indic(int indic, int value = 1) {
		_stackIndicator.Push((indic, Length, value));
		return this;
	}

	/// <summary>
	/// Ends indicator range started by <see cref="Indic(int, int)"/>.
	/// </summary>
	public SciTextBuilder Indic_() {
		var v = _stackIndicator.Pop();
		return Indic(v.indic, v.start, Length, v.value);
	}

	/// <summary>
	/// Adds link in a text range.
	/// </summary>
	/// <param name="indic">Indicator index. If negative, will be set 2 indicators: -value and -value+1.</param>
	/// <exception cref="ArgumentOutOfRangeException"><i>range</i> is not within current text.</exception>
	public SciTextBuilder Link(object data, int start, int end, int indic) {
		_links.Add((indic, start, end, data));
		return this;
	}

	/// <summary>
	/// Adds link in a text range.
	/// </summary>
	public SciTextBuilder Link(object data, RStr text, int indic) {
		int start = Length;
		_b.Append(text);
		return Link(data, start, Length, indic);
	}

	/// <summary>
	/// Starts link range from current text length. Later use <see cref="Link_"/> to end it.
	/// </summary>
	public SciTextBuilder Link(object data, int indic) {
		_stackLink.Push((indic, Length, data));
		return this;
	}

	/// <summary>
	/// Ends link range started by <see cref="Link(object, int)"/>.
	/// </summary>
	public SciTextBuilder Link_() {
		var v = _stackLink.Pop();
		return Link(v.data, v.start, Length, v.indic);
	}

	/// <summary>
	/// Adds style in a text range.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"><i>range</i> is not within current text.</exception>
	public SciTextBuilder Style(int style, int start, int end) {
		_styles.Add((style, start, end));
		return this;
	}

	/// <summary>
	/// Adds text with style.
	/// </summary>
	public SciTextBuilder Style(int style, RStr text) {
		int start = Length;
		_b.Append(text);
		return Style(style, start, Length);
	}

	/// <summary>
	/// Starts style range from current text length. Later use <see cref="Style_"/> to end it.
	/// </summary>
	public SciTextBuilder Style(int style) {
		_stackStyle.Push((style, Length));
		return this;
	}

	/// <summary>
	/// Ends style range started by <see cref="Style(int)"/>.
	/// </summary>
	public SciTextBuilder Style_() {
		var v = _stackStyle.Pop();
		return Style(v.style, v.start, Length);
	}

	/// <summary>
	/// Adds a folding start or end point.
	/// </summary>
	public SciTextBuilder Fold(SciFoldPoint fp) {
		_folding.Add(fp);
		return this;
	}

	public int BoldStyle { get; set; }
	public SciTextBuilder B(int start, int end) => Style(BoldStyle, start, end);
	public SciTextBuilder B(RStr text) => Style(BoldStyle, text);
	public SciTextBuilder B() => Style(BoldStyle);
	public SciTextBuilder B_() => Style_();

	public int GrayStyle { get; set; }
	public SciTextBuilder Gray(int start, int end) => Style(GrayStyle, start, end);
	public SciTextBuilder Gray(RStr text) => Style(GrayStyle, text);
	public SciTextBuilder Gray() => Style(GrayStyle);
	public SciTextBuilder Gray_() => Style_();

	public int GreenStyle { get; set; }
	public SciTextBuilder Green(int start, int end) => Style(GreenStyle, start, end);
	public SciTextBuilder Green(RStr text) => Style(GreenStyle, text);
	public SciTextBuilder Green() => Style(GreenStyle);
	public SciTextBuilder Green_() => Style_();

	public int LinkIndic { get; set; }
	public SciTextBuilder Link(object data, int start, int end) => Link(data, start, end, LinkIndic);
	public SciTextBuilder Link(object data, RStr text) => Link(data, text, LinkIndic);
	public SciTextBuilder Link(object data) => Link(data, LinkIndic);

	public int Link2Indic { get; set; }
	public SciTextBuilder Link2(object data, int start, int end) => Link(data, start, end, Link2Indic);
	public SciTextBuilder Link2(object data, RStr text) => Link(data, text, Link2Indic);
	public SciTextBuilder Link2(object data) => Link(data, Link2Indic);

	/// <summary>
	/// Callers can use this as they want. Not used by this class.
	/// </summary>
	public int ControlWidth { get; set; }
}
