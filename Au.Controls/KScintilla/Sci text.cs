//Functions to work with Scintilla control text, etc.

namespace Au.Controls;

using static Sci;

public unsafe partial class KScintilla {
	#region low level
	
	/// <summary>
	/// Calls a Scintilla message that sets a string which is passed using <i>lParam</i>.
	/// The string can be null if the Scintilla message allows it.
	/// If the message changes control text, this function does not work if the control is read-only. At first make non-readonly temporarily.
	/// Don't call this function from another thread.
	/// </summary>
	public int aaaSetString(int sciMessage, nint wParam, RStr lParam) {
		fixed (byte* s = _ToUtf8(lParam, out var len)) {
			return Call(sciMessage, wParam, s);
		}
	}
	
	/// <summary>
	/// Calls a Scintilla message that sets a string which is passed using <i>lParam</i> (UTF-8 string) and <i>wParam</i> (UTF-8 string length).
	/// The string can be null if the Scintilla message allows it.
	/// If the message changes control text, this function does not work if the control is read-only. At first make non-readonly temporarily.
	/// Don't call this function from another thread.
	/// </summary>
	public int aaaSetString(int sciMessage, RStr lParam) {
		fixed (byte* s = _ToUtf8(lParam, out var len)) {
			return Call(sciMessage, len, s);
		}
	}
	
	/// <summary>
	/// Calls a Scintilla message that sets a string which is passed using <i>wParam</i>.
	/// The string can be null if the Scintilla message allows it.
	/// If the message changes control text, this function does not work if the control is read-only. At first make non-readonly temporarily.
	/// Don't call this function from another thread.
	/// </summary>
	public int aaaSetString(int sciMessage, RStr wParam, nint lParam) {
		fixed (byte* s = _ToUtf8(wParam)) {
			return Call(sciMessage, (nint)s, lParam);
		}
	}
	
	/// <summary>
	/// Calls a Scintilla message that sets a string which is passed using <i>lParam</i>.
	/// With many messages the <i>lParam</i> string must be '\0'-terminated, eg UTF-8 string literal like <c>"example"u8</c>.
	/// The string can be null if the Scintilla message allows it.
	/// If the message changes control text, this function does not work if the control is read-only. At first make non-readonly temporarily.
	/// </summary>
	public int aaaSetString(int sciMessage, nint wParam, RByte lParam) {
		fixed (byte* p = lParam) {
			return Call(sciMessage, wParam, p);
		}
	}
	
	/// <summary>
	/// Calls a Scintilla message and passes two strings using <i>wParam</i> and <i>lParam</i>.
	/// <i>wParamlParam</i> must be like "WPARAM\0LPARAM". Asserts if no '\0'.
	/// If the message changes control text, this function does not work if the control is read-only. At first make non-readonly temporarily.
	/// Don't call this function from another thread.
	/// </summary>
	public int aaaSetStringString(int sciMessage, RStr wParamlParam) {
		fixed (byte* s = _ToUtf8(wParamlParam, out var len)) {
			int i = Ptr_.Length(s);
			Debug.Assert(i < len);
			return Call(sciMessage, (nint)s, s + i + 1);
		}
	}
	
	/// <summary>
	/// Calls a Scintilla message that gets a string when length is known.
	/// Always uses <i>utf8Length</i> bytes of the result (does not find length).
	/// Can get binary string (with '\0' characters).
	/// </summary>
	/// <param name="sciMessage"></param>
	/// <param name="wParam"></param>
	/// <param name="utf8Length">
	/// Known length (bytes) of the result UTF-8 string, without the terminating '\0' character.
	/// If 0, returns "" and does not call the message.
	/// </param>
	public string aaaGetStringOfLength(int sciMessage, nint wParam, int utf8Length)
		=> _GetString(sciMessage, wParam, utf8Length, false);
	
	/// <summary>
	/// Calls a Scintilla message that gets a string. See <see cref="aaaGetStringOfLength"/>.
	/// To get buffer size, at first calls <i>sciMessage</i> with <i>lParam</i>=0 (null buffer).
	/// Can get binary string (with '\0' characters).
	/// Don't call this function from another thread.
	/// </summary>
	/// <param name="sciMessage"></param>
	/// <param name="wParam"></param>
	public string aaaGetStringGetLength(int sciMessage, nint wParam)
		=> _GetString(sciMessage, wParam, Call(sciMessage, wParam), false);
	
	/// <summary>
	/// Calls a Scintilla message that gets a '\0'-terminated string.
	/// Cannot get binary string (with '\0' characters).
	/// Don't call this function from another thread.
	/// </summary>
	/// <param name="sciMessage"></param>
	/// <param name="wParam"></param>
	/// <param name="bufferSize">
	/// How much UTF-8 bytes to allocate for Scintilla to store the text.
	/// Can be either known or max expected text length, without the terminating '\0' character. The function will find length of the retrieved string (finds '\0').
	/// If 0, returns "" and does not call the message.
	/// </param>
	public string aaaGetString0Terminated(int sciMessage, nint wParam, int bufferSize)
		=> _GetString(sciMessage, wParam, bufferSize, true);
	
	[SkipLocalsInit]
	string _GetString(int sciMessage, nint wParam, int len, bool findLength) {
		if (len == 0) return "";
		using FastBuffer<byte> b = new(len + 1);
		b[len] = 0;
		Call(sciMessage, wParam, b.p);
		Debug.Assert(b[len] == 0);
		if (findLength) len = b.FindByteStringLength();
		return Encoding.UTF8.GetString(b, len);
	}
	
	static string _FromUtf8(byte* b) => Convert2.Utf8Decode(b);
	
	static byte[] _ToUtf8(RStr s) => Convert2.Utf8Encode(s);
	
	static byte[] _ToUtf8(RStr s, out int utf8Length) {
		var r = Convert2.Utf8Encode(s);
		utf8Length = r.Length - 1;
		return r;
	}
	
	/// <summary>
	/// Optimized 'get text' function.
	/// </summary>
	/// <param name="start8">Start index, UTF-8.</param>
	/// <param name="end8">End index, UTF-8.</param>
	/// <remarks>
	/// Does not create an intermediate byte[].
	/// Gets big text 5 times faster than aaaGetStringOfLength. Tested with text 31K length, 1K lines.
	/// </remarks>
	string _RangeText(int start8, int end8) {
		Debug.Assert(end8 >= start8);
		Debug.Assert((uint)end8 <= aaaLen8);
		if (end8 == start8) return "";
		int gap = Sci_Range(AaSciPtr, start8, end8, out var p1, out var p2);
		if (p2 != null) {
			int n1 = gap - start8, n2 = end8 - gap;
			int len1 = Encoding.UTF8.GetCharCount(p1, n1);
			int len2 = Encoding.UTF8.GetCharCount(p2, n2);
			nint k1 = (nint)p1, k2 = (nint)p2;
			return string.Create(len1 + len2, (k1, k2, n1, n2), static (span, a) => {
				int len1 = Encoding.UTF8.GetChars(new RByte((byte*)a.k1, a.n1), span);
				Encoding.UTF8.GetChars(new RByte((byte*)a.k2, a.n2), span.Slice(len1));
			});
		} else {
			int n1 = end8 - start8;
			int len1 = Encoding.UTF8.GetCharCount(p1, n1);
			nint k1 = (nint)p1;
			return string.Create(len1, (k1, n1), static (span, a) => {
				Encoding.UTF8.GetChars(new RByte((byte*)a.k1, a.n1), span);
			});
		}
	}
	
	/// <summary>
	/// If <i>utf16</i>, converts <i>from</i> and <i>to</i> from characters to UTF-8 bytes.
	/// </summary>
	/// <param name="utf16">Input values are UTF-16.</param>
	/// <param name="from"></param>
	/// <param name="to">If -1, uses <see cref="aaaLen8"/>.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid argument, eg greater than text length or <i>to</i> less than <i>from</i>.</exception>
	public void aaaNormalizeRange(bool utf16, ref int from, ref int to) {
		if (from < 0 || (to < from && to != -1)) throw new ArgumentOutOfRangeException();
		if (utf16) from = aaaPos8(from);
		if (to < 0) to = aaaLen8; else if (utf16) to = aaaPos8(to);
	}
	
	/// <summary>
	/// If <i>utf16</i>, converts <i>from</i> and <i>to</i> from characters to UTF-8 bytes.
	/// </summary>
	/// <param name="utf16">Input values are UTF-16.</param>
	/// <param name="r">Range. Can be spacified from start or/and from end.</param>
	/// <exception cref="ArgumentOutOfRangeException">Invalid argument, eg <i>to</i> less than <i>from</i>.</exception>
	public (int from, int to) aaaNormalizeRange(bool utf16, Range r) {
		int from, to;
		if (r.Start.IsFromEnd || r.End.IsFromEnd) {
			(from, to) = r.GetStartEnd(utf16 ? aaaLen16 : aaaLen8);
			if (utf16) {
				from = aaaPos8(from);
				to = aaaPos8(to);
			}
		} else {
			from = r.Start.Value;
			to = r.End.Value;
			aaaNormalizeRange(utf16, ref from, ref to);
		}
		return (from, to);
	}
	
	/// <summary>
	/// Same as <see cref="aaaNormalizeRange(bool, ref int, ref int)"/>, but can be <i>to</i> less than <i>from</i>. If so, returns true.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Invalid argument, eg greater than text length.</exception>
	public bool aaaNormalizeRangeCanBeReverse(bool utf16, ref int from, ref int to, bool swapFromTo) {
		bool reverse = to >= 0 && to < from;
		if (reverse) Math2.Swap(ref from, ref to);
		aaaNormalizeRange(utf16, ref from, ref to);
		if (reverse && !swapFromTo) Math2.Swap(ref from, ref to);
		return reverse;
	}
	
	/// <summary>
	/// => utf16 ? aaaPos8(pos) : pos;
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Negative.</exception>
	int _ParamPos(bool utf16, int pos) => pos >= 0 ? (utf16 ? aaaPos8(pos) : pos) : throw new ArgumentOutOfRangeException();
	
	/// <summary>
	/// => utf16 ? aaaPos16(pos) : pos;
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Negative.</exception>
	int _ReturnPos(bool utf16, int pos) => pos >= 0 ? (utf16 ? aaaPos16(pos) : pos) : throw new ArgumentOutOfRangeException();
	
	/// <summary>
	/// pos >= 0 ? (utf16 ? aaaPos16(pos) : pos) : pos;
	/// </summary>
	int _ReturnPosCanBeNegative(bool utf16, int pos) => pos >= 0 ? (utf16 ? aaaPos16(pos) : pos) : pos;
	
	/// <summary>
	/// => line;
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException">Negative.</exception>
	int _ParamLine(int line) => line >= 0 ? line : throw new ArgumentOutOfRangeException();
	
	struct _NoReadonly : IDisposable {
		KScintilla _t;
		bool _ro;
		
		public _NoReadonly(KScintilla t) {
			_t = t;
			_ro = _t.AaInitReadOnlyAlways || _t.aaaIsReadonly;
			if (_ro) _t.Call(SCI_SETREADONLY, 0);
		}
		
		public void Dispose() {
			if (_ro) _t.Call(SCI_SETREADONLY, 1);
		}
	}
	
	struct _NoUndoNotif : IDisposable {
		KScintilla _t;
		bool _noUndo, _noNotif;
		
		public _NoUndoNotif(KScintilla t, SciSetTextFlags flags) {
			_t = t;
			_noUndo = flags.Has(SciSetTextFlags.NoUndo) && 0 != _t.Call(SCI_GETUNDOCOLLECTION);
			_noNotif = flags.Has(SciSetTextFlags.NoNotify) && !_t.AaDisableModifiedNotifications;
			if (_noNotif) _t.AaDisableModifiedNotifications = true;
			if (_noUndo) _t.Call(SCI_SETUNDOCOLLECTION);
		}
		
		public void Dispose() {
			if (_noUndo) {
				_t.Call(SCI_EMPTYUNDOBUFFER);
				_t.Call(SCI_SETUNDOCOLLECTION, 1);
			}
			if (_noNotif) _t.AaDisableModifiedNotifications = false;
		}
	}
	
	#endregion
	
	#region set/get/clear all text, append text
	
	/// <summary>
	/// Removes all text (SCI_CLEARALL).
	/// </summary>
	/// <param name="flags"></param>
	public void aaaClearText(SciSetTextFlags flags = 0) {
		if (_w.Is0) return;
		using (new _NoUndoNotif(this, flags))
		using (new _NoReadonly(this))
			Call(SCI_CLEARALL);
	}
	
	/// <summary>
	/// Replaces all text.
	/// Parses tags if need.
	/// </summary>
	/// <param name="s">Text.</param>
	/// <param name="flags"></param>
	/// <param name="ignoreTags">Don't parse tags, regardless of <b>AaInitTagsStyle</b>.</param>
	public void aaaSetText(string s, SciSetTextFlags flags = 0, bool ignoreTags = false) {
		using (new _NoUndoNotif(this, flags)) {
			if (!ignoreTags && _CanParseTags(s)) {
				aaaClearText();
				AaTags.AddText(s, false, AaInitTagsStyle == AaTagsStyle.AutoWithPrefix);
			} else {
				using (new _NoReadonly(this))
					aaaSetString(SCI_SETTEXT, 0, s ?? "");
			}
		}
	}
	
	bool _CanParseTags(string s) {
		if (s.NE()) return false;
		return AaInitTagsStyle switch {
			AaTagsStyle.AutoAlways => s.Contains('<'),
			AaTagsStyle.AutoWithPrefix => s.Starts("<>"),
			_ => false,
		};
	}
	
	/// <summary>
	/// Appends text and optionally "\r\n".
	/// Parses tags if need. Optionally scrolls and moves current position to the end (SCI_GOTOPOS).
	/// </summary>
	/// <param name="s"></param>
	/// <param name="andRN">Also append "\r\n". Ignores (uses true) if parses tags.</param>
	/// <param name="scroll">Move current position and scroll to the end.</param>
	/// <param name="ignoreTags">Don't parse tags, regardless of <b>AaInitTagsStyle</b>.</param>
	public void aaaAppendText(string s, bool andRN, bool scroll, bool ignoreTags = false) {
		s ??= "";
		if (!ignoreTags && _CanParseTags(s)) {
			AaTags.AddText(s, true, AaInitTagsStyle == AaTagsStyle.AutoWithPrefix, scroll);
		} else {
			var a = Convert2.Utf8Encode(s, andRN ? "\r\n" : "");
			using (new _NoReadonly(this))
				fixed (byte* b = a) Call(SCI_APPENDTEXT, a.Length, b);
			
			if (scroll) Call(SCI_GOTOPOS, aaaLen8);
		}
	}
	
	/// <summary>
	/// SCI_APPENDTEXT.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="scroll">Move current position and scroll to the end.</param>
	public void aaaAppendText8(RByte s, bool scroll) {
		using (new _NoReadonly(this)) {
			fixed (byte* p = s) Call(SCI_APPENDTEXT, s.Length, p);
		}
		
		if (scroll) Call(SCI_GOTOPOS, aaaLen8);
	}
	
	/// <summary>
	/// Sets or appends UTF-8 text of specified length. Does not parse tags.
	/// If <i>scroll</i>, moves current position and scrolls to the end (SCI_GOTOPOS).
	/// </summary>
	internal void aaaAddText8_(bool append, bool scroll, byte* s, int lenToAppend) {
		using (new _NoReadonly(this))
			if (append) Call(SCI_APPENDTEXT, lenToAppend, s);
			else Call(SCI_SETTEXT, 0, s);
		
		if (scroll) Call(SCI_GOTOPOS, aaaLen8);
	}
	
	//not used now
	///// <summary>
	///// Sets or appends styled UTF-8 text of specified length.
	///// Does not append newline (s should contain it). Does not parse tags. Moves current position and scrolls to the end.
	///// Uses SCI_ADDSTYLEDTEXT. Caller does not have to move cursor to the end.
	///// lenToAppend is length in bytes, not in cells.
	///// </summary>
	//internal void aaaAddStyledText_(bool append, byte* s, int lenBytes)
	//{
	//	if(append) Call(SCI_SETEMPTYSELECTION, TextLengthBytes);
	
	//	using(new _NoReadonly(this))
	//	if(!append) Call(SCI_SETTEXT);
	//	Call(SCI_ADDSTYLEDTEXT, lenBytes, s);
	
	//	if(append) Call(SCI_GOTOPOS, TextLengthBytes);
	//}
	
	#endregion
	
	/// <summary>
	/// Gets (SCI_GETCURRENTPOS) or sets (SCI_SETEMPTYSELECTION) current caret position in UTF-8 bytes.
	/// The <c>set</c> function makes empty selection; does not scroll and does not make visible like aaaGoToPos.
	/// </summary>
	public int aaaCurrentPos8 { get => Call(SCI_GETCURRENTPOS); set => Call(SCI_SETEMPTYSELECTION, value); }
	
	/// <summary>
	/// Gets (SCI_GETCURRENTPOS) or sets (SCI_SETEMPTYSELECTION) current caret position in UTF-16 chars.
	/// The <c>set</c> function makes empty selection; does not scroll and does not make visible like aaaGoToPos.
	/// </summary>
	public int aaaCurrentPos16 { get => aaaPos16(aaaCurrentPos8); set => Call(SCI_SETEMPTYSELECTION, aaaPos8(value)); }
	
	/// <summary>
	/// SCI_GETSELECTIONSTART UTF-8.
	/// </summary>
	public int aaaSelectionStart8 => Call(SCI_GETSELECTIONSTART);
	
	/// <summary>
	/// SCI_GETSELECTIONSTART UTF-16.
	/// </summary>
	public int aaaSelectionStart16 => aaaPos16(aaaSelectionStart8);
	
	/// <summary>
	/// SCI_GETSELECTIONEND UTF-8.
	/// Always greater or equal than SelectionStart8.
	/// </summary>
	public int aaaSelectionEnd8 => Call(SCI_GETSELECTIONEND);
	
	/// <summary>
	/// SCI_GETSELECTIONEND UTF-16.
	/// Always greater or equal than SelectionStart16.
	/// </summary>
	public int aaaSelectionEnd16 => aaaPos16(aaaSelectionEnd8);
	
	/// <summary>
	/// <c>utf16 ? (aaaSelectionStart16, aaaSelectionEnd16) : (aaaSelectionStart8, aaaSelectionEnd8)</c>
	/// </summary>
	public (int start, int end) aaaSelection(bool utf16)
		=> utf16 ? (aaaSelectionStart16, aaaSelectionEnd16) : (aaaSelectionStart8, aaaSelectionEnd8);
	
	/// <summary>
	/// true if !SCI_GETSELECTIONEMPTY.
	/// </summary>
	public bool aaaHasSelection => 0 == Call(SCI_GETSELECTIONEMPTY);
	
	/// <summary>
	/// Gets line index from character position.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="pos">A position in document text. Returns the last line if too big.</param>
	public int aaaLineFromPos(bool utf16, int pos)
		=> Call(SCI_LINEFROMPOSITION, _ParamPos(utf16, pos));
	
	/// <summary>
	/// Gets line index at <see cref="aaaSelectionStart8"/>.
	/// </summary>
	public int aaaLineFromPos()
		=> Call(SCI_LINEFROMPOSITION, aaaSelectionStart8);
	
	/// <summary>
	/// Gets line start position from line index.
	/// </summary>
	/// <param name="utf16">Return UTF-16.</param>
	/// <param name="line">0-based line index. Returns text length if too big.</param>
	public int aaaLineStart(bool utf16, int line) => _ReturnPos(utf16, _LineStart(line));
	
	int _LineStart(int line) {
		if (line < 0) throw new ArgumentOutOfRangeException();
		int R = Call(SCI_POSITIONFROMLINE, _ParamLine(line));
		return R >= 0 ? R : aaaLen8;
		//If line < 0, Scintilla returns line start from selection start.
		//If line > number of lines, Scintilla returns -1.
	}
	
	/// <summary>
	/// Gets line end position from line index.
	/// </summary>
	/// <param name="utf16">Return UTF-16.</param>
	/// <param name="line">0-based line index. Returns text length if too big.</param>
	/// <param name="withRN">Include \r\n.</param>
	public int aaaLineEnd(bool utf16, int line, bool withRN = false) {
		line = _ParamLine(line);
		return _ReturnPos(utf16, withRN ? _LineStart(line + 1) : Call(SCI_GETLINEENDPOSITION, line));
	}
	
	/// <summary>
	/// Gets line start position from any position.
	/// </summary>
	/// <param name="utf16">pos is UTF-16. Return UTF-16.</param>
	/// <param name="pos">A position in document text. Returns text length if too big.</param>
	public int aaaLineStartFromPos(bool utf16, int pos)
		=> aaaLineStart(utf16, aaaLineFromPos(utf16, pos));
	
	/// <summary>
	/// Gets line start position from any position and gets line index.
	/// Returns start position.
	/// </summary>
	/// <param name="utf16">pos is UTF-16. Return UTF-16.</param>
	/// <param name="pos">A position in document text. Returns text length if too big.</param>
	/// <param name="line">Receives line index.</param>
	public int aaaLineStartFromPos(bool utf16, int pos, out int line)
		=> aaaLineStart(utf16, line = aaaLineFromPos(utf16, pos));
	
	/// <summary>
	/// Gets line end position from any position.
	/// </summary>
	/// <param name="utf16">pos is UTF-16. Return UTF-16.</param>
	/// <param name="pos">A position in document text. Returns text length if too big.</param>
	/// <param name="withRN">Include \r\n.</param>
	/// <param name="lineStartIsLineEnd">If pos is at a line start (0 or after '\n' character), return pos.</param>
	public int aaaLineEndFromPos(bool utf16, int pos, bool withRN = false, bool lineStartIsLineEnd = false) {
		int pos0 = pos;
		pos = _ParamPos(utf16, pos);
		if (lineStartIsLineEnd) {
			if (pos == 0 || aaaCharAt8(pos - 1) == '\n') return pos0;
		}
		return aaaLineEnd(utf16, aaaLineFromPos(false, pos), withRN);
	}
	
	/// <summary>
	/// Gets line index, start and end positions from position.
	/// </summary>
	/// <param name="utf16">pos is UTF-16. Return UTF-16.</param>
	/// <param name="pos">A position in document text. Uses the last line if too big.</param>
	/// <param name="withRN">Include \r\n.</param>
	/// <param name="utf16Return">If not null, overrides <i>utf16</i> for return values.</param>
	public (int line, int start, int end) aaaLineStartEndFromPos(bool utf16, int pos, bool withRN = false, bool? utf16Return = null) {
		int startPos = aaaLineStartFromPos(false, _ParamPos(utf16, pos), out int line);
		int endPos = aaaLineEnd(false, line, withRN);
		utf16 = utf16Return ?? utf16;
		return (line, _ReturnPos(utf16, startPos), _ReturnPos(utf16, endPos));
	}
	
	/// <summary>
	/// Gets line text.
	/// </summary>
	/// <param name="line">0-based line index. If invalid, returns "".</param>
	/// <param name="withRN">Include \r\n.</param>
	public string aaaLineText(int line, bool withRN = false) => _RangeText(aaaLineStart(false, line), aaaLineEnd(false, line, withRN));
	
	/// <summary>
	/// Gets line height.
	/// Currently all lines are of the same height.
	/// </summary>
	public int aaaLineHeight() => Call(SCI_TEXTHEIGHT, 0);
	
	/// <summary>
	/// Gets the number of lines.
	/// </summary>
	public int aaaLineCount => Call(SCI_GETLINECOUNT);
	
	/// <summary>
	/// Gets the number of tabs + spaces/4 at the start of the line that contains the specified position.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="pos">A position in document text.</param>
	/// <param name="extraSpaces">Receives the number of extra spaces, 0 to 3.</param>
	public int aaaLineIndentFromPos(bool utf16, int pos, out int extraSpaces) {
		int line = aaaLineFromPos(utf16, pos);
		int i = Call(SCI_GETLINEINDENTATION, line), r = i / 4;
		extraSpaces = i - r * 4;
		return r;
	}
	
	/// <summary>
	/// Gets the number of tabs + spaces/4 at the start of the line that contains the specified position.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="pos">A position in document text.</param>
	public int aaaLineIndentFromPos(bool utf16, int pos) => aaaLineIndentFromPos(utf16, pos, out _);
	
	/// <summary>
	/// Gets position from point.
	/// </summary>
	/// <param name="utf16">Return UTF-16.</param>
	/// <param name="p">Point in client area.</param>
	/// <param name="minusOneIfFar">Return -1 if p is not in text characters.</param>
	public int aaaPosFromXY(bool utf16, POINT p, bool minusOneIfFar)
		=> _ReturnPosCanBeNegative(utf16, Call(minusOneIfFar ? SCI_POSITIONFROMPOINTCLOSE : SCI_POSITIONFROMPOINT, p.x, p.y));
	
	/// <summary>
	/// Gets annotation text of line.
	/// Returns "" if the line does not contain annotation or is invalid line index.
	/// </summary>
	public string aaaAnnotationText(int line) => AaImages?.AnnotationText_(line) ?? aaaAnnotationText_(line);
	
	/// <summary>
	/// Gets raw annotation text which can contain image info.
	/// aaaAnnotationText gets text without image info.
	/// Returns "" if the line does not contain annotation or is invalid line index.
	/// </summary>
	public string aaaAnnotationText_(int line) => aaaGetStringGetLength(SCI_ANNOTATIONGETTEXT, line);
	
	/// <summary>
	/// Sets annotation text of line.
	/// Does nothing if invalid line index.
	/// If s is null or "", removes annotation.
	/// Preserves existing image info.
	/// </summary>
	public void aaaAnnotationText(int line, string s, bool eol = false) {
		if (eol) aaaAnnotationText_(line, s, eol);
		else if (AaImages != null) AaImages.AnnotationText_(line, s);
		else aaaAnnotationText_(line, s);
	}
	
	/// <summary>
	/// Sets raw annotation text which can contain image info.
	/// If s is null or "", removes annotation.
	/// </summary>
	internal void aaaAnnotationText_(int line, string s, bool eol = false) {
		if (s.NE()) s = null;
		aaaSetString(eol ? SCI_EOLANNOTATIONSETTEXT : SCI_ANNOTATIONSETTEXT, line, s);
	}
	
	/// <summary>
	/// Moves <i>from</i> to the start of its line, and <i>to</i> to the end of its line.
	/// Does not change <i>to</i> if it is at a line start.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="from">Start index.</param>
	/// <param name="to">End index.</param>
	/// <param name="withRN">Include "\r\n".</param>
	public void aaaRangeToFullLines(bool utf16, ref int from, ref int to, bool withRN = false) {
		Debug.Assert(from <= to);
		from = _ReturnPos(utf16, aaaLineStartFromPos(utf16, from));
		to = _ReturnPos(utf16, aaaLineEndFromPos(utf16, to, withRN, true));
	}
	
	/// <summary>
	/// SCI_INSERTTEXT.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="pos">Start index. Cannot be negative.</param>
	/// <param name="s">Text to insert. Can be null.</param>
	/// <param name="addUndoPointBefore">Call <see cref="aaaAddUndoPoint"/> before.</param>
	/// <param name="addUndoPointAfter">Call <see cref="aaaAddUndoPoint"/> after.</param>
	/// <param name="restoreFolding">If <i>pos</i> is hidden because of folding, finally collapse its folding again. See <see cref="aaaFoldingRestorer"/>.</param>
	/// <remarks>
	/// Does not parse tags.
	/// Does not change current selection, unless <i>pos</i> is in it; for it use <see cref="aaaReplaceSel"/> or <see cref="aaaReplaceRange"/>.
	/// </remarks>
	public void aaaInsertText(bool utf16, int pos, string s, bool addUndoPointBefore = false, bool addUndoPointAfter = false, bool restoreFolding = false) {
		if (addUndoPointBefore) aaaAddUndoPoint();
		using (new _NoReadonly(this))
		using (new aaaFoldingRestorer(restoreFolding ? this : null, pos))
			aaaSetString(SCI_INSERTTEXT, _ParamPos(utf16, pos), s ?? "");
		if (addUndoPointAfter) aaaAddUndoPoint();
	}
	
	/// <summary>
	/// If ctor detects that the line from <i>pos</i> is hidden because of folding, <b>Dispose</b> collapses its folding again.
	/// Use when modifying text to prevent unfolding.
	/// </summary>
	public struct aaaFoldingRestorer : IDisposable {
		KScintilla _sci;
		int _foldLine;
		//tested: temp setting SCI_SETAUTOMATICFOLD does not work. If restoring async, does not expand, but draws incorrectly.
		
		/// <param name="sci">Can be null, then does nothing.</param>
		/// <param name="pos"></param>
		public aaaFoldingRestorer(KScintilla sci, int pos) {
			_sci = sci;
			_foldLine = -1;
			if (sci != null) {
				int line = sci.aaaLineFromPos(true, pos);
				if (0 == sci.Call(SCI_GETLINEVISIBLE, line)) _foldLine = sci.Call(SCI_GETFOLDPARENT, line);
			}
		}
		
		public void Dispose() {
			if (_foldLine < 0) return;
			_sci.Call(SCI_FOLDLINE, _foldLine);
			
			//If at the modified line index was a nested folding point, Scintilla will expand again, very async.
			//	Could restore again with the following code, but it can be dangerous, eg document closed. Never mind.
			//var sci = _sci; var i = _foldLine;
			//timer.after(300, _ => sci.Call(SCI_FOLDLINE, i));
		}
	}
	
	///// <summary>
	///// Inserts text at current position.
	///// Does not parse tags.
	///// Does not change current selection; for it use <see cref="aaaReplaceSel"/>.
	///// </summary>
	///// <param name="s">Text to insert. Can be null.</param>
	//public void aaaInsertText(string s)
	//{
	//	using(new _NoReadonly(this))
	//		aaaSetString(SCI_INSERTTEXT, -1, s ?? "");
	//}
	
	/// <summary>
	/// SCI_DELETERANGE.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="from">Start index.</param>
	/// <param name="to">End index. If -1, uses control text length.</param>
	/// <remarks>
	/// Does not parse tags.
	/// Does not change current selection, unless it is in the range (including <i>to</i>); for it use <see cref="aaaReplaceSel"/> or <see cref="aaaReplaceRange"/>.
	/// </remarks>
	public void aaaDeleteRange(bool utf16, int from, int to) {
		aaaNormalizeRange(utf16, ref from, ref to);
		using (new _NoReadonly(this))
			Call(SCI_DELETERANGE, from, to - from);
	}
	
	/// <summary>
	/// Replaces text range.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="from">Start index.</param>
	/// <param name="to">End index. If -1, uses control text length. Can be less than <i>from</i>.</param>
	/// <param name="s">Replacement text. Can be null.</param>
	/// <param name="moveCurrentPos">
	/// After replacing set curent position at the end of the replacement. If <i>from</i> less than to - at <i>from</i>.
	/// Else if current position was in the range (including <i>to</i>), Scintilla sets at <i>from</i>.
	/// Else does not change current position and selection.
	/// </param>
	/// <remarks>
	/// Does not parse tags.
	/// By default does not change current selection, unless it is in the range (including <i>to</i>).
	/// </remarks>
	public void aaaReplaceRange(bool utf16, int from, int to, string s, bool moveCurrentPos = false) {
		bool reverse = aaaNormalizeRangeCanBeReverse(utf16, ref from, ref to, swapFromTo: true);
		using (new _NoReadonly(this)) {
			int fromEnd = !moveCurrentPos || reverse ? 0 : aaaLen8 - to;
			Call(SCI_SETTARGETRANGE, from, to);
			aaaSetString(SCI_REPLACETARGET, s ?? "");
			if (moveCurrentPos) aaaCurrentPos8 = reverse ? from : aaaLen8 - fromEnd;
		}
	}
	
	/// <summary>
	/// Gets range text.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="from">Start index.</param>
	/// <param name="to">End index. If -1, uses control text length.</param>
	public string aaaRangeText(bool utf16, int from, int to) {
		aaaNormalizeRange(utf16, ref from, ref to);
		return _RangeText(from, to);
	}
	
	/// <summary>
	/// Gets direct pointer to a text range in Scintilla buffer (SCI_GETRANGEPOINTER).
	/// Does not validate arguments, just asserts to >= from.
	/// </summary>
	/// <param name="from">UTF-8 start position.</param>
	/// <param name="to">UTF-8 end position.</param>
	public byte* aaaRangePointer(int from, int to) {
		Debug.Assert(to >= from);
		return (byte*)CallRetPtr(SCI_GETRANGEPOINTER, from, to - from);
	}
	
	/// <summary>
	/// Gets direct pointer to a text range in Scintilla buffer (SCI_GETRANGEPOINTER).
	/// Does not validate arguments, just asserts to >= from.
	/// </summary>
	/// <param name="from">UTF-8 start position.</param>
	/// <param name="to">UTF-8 end position.</param>
	public RByte aaaRangeSpan(int from, int to) => new(aaaRangePointer(from, to), to - from);
	
	/// <summary>
	/// SCI_REPLACESEL.
	/// </summary>
	/// <param name="s">Replacement text. Can be null.</param>
	/// <remarks>
	/// Does not parse tags.
	/// If read-only, asserts and fails (unlike most other functions that change text).
	/// </remarks>
	public void aaaReplaceSel(string s) {
		Debug.Assert(!aaaIsReadonly);
		aaaSetString(SCI_REPLACESEL, 0, s ?? "");
	}
	
	/// <summary>
	/// Sets selection (SCI_SETSEL) and replaces with new text (SCI_REPLACESEL).
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="from">Start index.</param>
	/// <param name="to">End index. If -1, uses control text length. Can be less than from.</param>
	/// <param name="s">Replacement text. Can be null.</param>
	/// <remarks>
	/// Does not parse tags.
	/// If read-only, asserts and fails (unlike most other functions that change text).
	/// </remarks>
	public void aaaSetAndReplaceSel(bool utf16, int from, int to, string s) {
		Debug.Assert(!aaaIsReadonly);
		aaaSelect(utf16, from, to);
		aaaSetString(SCI_REPLACESEL, 0, s ?? "");
	}
	
	/// <summary>
	/// SCI_GOTOPOS and ensures visible.
	/// </summary>
	public void aaaGoToPos(bool utf16, int pos) {
		pos = _ParamPos(utf16, pos);
		int line = Call(SCI_LINEFROMPOSITION, pos);
		Call(SCI_ENSUREVISIBLEENFORCEPOLICY, line);
		Call(SCI_GOTOPOS, pos);
	}
	
	/// <summary>
	/// SCI_GOTOLINE and ensures visible.
	/// </summary>
	public void aaaGoToLine(int line) {
		Call(SCI_ENSUREVISIBLEENFORCEPOLICY, line);
		Call(SCI_GOTOLINE, line);
	}
	
	/// <summary>
	/// SCI_SETSEL and optionally ensures visible.
	/// </summary>
	/// <param name="utf16"></param>
	/// <param name="from"></param>
	/// <param name="to">If -1, uses text length. Else <i>to</i> can be less than <i>from</i>. Caret will be at <i>to</i>.</param>
	/// <param name="makeVisible">Ensure line visible and selection visible. Without it in some cases selection to the left of the caret may be invisible.</param>
	public void aaaSelect(bool utf16, int from, int to, bool makeVisible = false) {
		aaaNormalizeRangeCanBeReverse(utf16, ref from, ref to, swapFromTo: false);
		if (makeVisible) aaaGoToPos(false, from);
		Call(SCI_SETSEL, from, to);
	}
	
	/// <summary>
	/// SCI_GETREADONLY, SCI_SETREADONLY.
	/// </summary>
	public bool aaaIsReadonly {
		get => 0 != Call(SCI_GETREADONLY);
		set => Call(SCI_SETREADONLY, value ? 1 : 0);
	}
	
	//public bool aaaIsReadonly {
	//	get => _isReadOnly;
	//	set {
	//		if (value != _isReadOnly) {
	//			_isReadOnly = value;
	//			if (!_w.Is0) Call(SCI_SETREADONLY, _isReadOnly);
	//		}
	//	}
	//}
	//bool _isReadOnly;
	
	/// <summary>
	/// Gets text and offsets of lines containing selection.
	/// Returns true. If <i>ifFullLines</i> is true, may return false.
	/// </summary>
	/// <param name="utf16">Return UTF-16.</param>
	/// <param name="x">Results.</param>
	/// <param name="ifFullLines">Fail (return false) if selection length is 0 or selection start is not at a line start.</param>
	/// <param name="oneMore">Get +1 line if selection ends at a line start, except if selection length is 0.</param>
	public bool aaaGetSelectionLines(bool utf16, out (int selStart, int selEnd, int linesStart, int linesEnd, string text) x, bool ifFullLines = false, bool oneMore = false) {
		x = default;
		x.selStart = aaaSelectionStart8; x.selEnd = aaaSelectionEnd8;
		if (ifFullLines && x.selEnd == x.selStart) return false;
		var (_, start, end) = aaaLineStartEndFromPos(false, x.selStart);
		if (ifFullLines && start != x.selStart) return false;
		x.linesStart = start;
		
		if (x.selEnd > x.selStart) {
			(_, start, end) = aaaLineStartEndFromPos(false, x.selEnd);
			if (!oneMore && start == x.selEnd) end = start; //selection end is at line start. We need the line only if oneMore.
			if (ifFullLines && x.selEnd < end) return false;
		}
		
		x.linesEnd = end;
		x.text = _RangeText(x.linesStart, end);
		if (utf16) {
			x.linesStart = aaaPos16(x.linesStart);
			x.linesEnd = aaaPos16(x.linesEnd);
			x.selStart = aaaPos16(x.selStart);
			x.selEnd = aaaPos16(x.selEnd);
		}
		return true;
	}
	
	public string aaaSelectedText() => _RangeText(aaaSelectionStart8, aaaSelectionEnd8);
	
	/// <summary>
	/// SCI_FINDTEXT.
	/// </summary>
	/// <param name="utf16">pos is UTF-16. Return UTF-16.</param>
	/// <param name="s"></param>
	/// <param name="start"></param>
	/// <param name="end">If -1, text length.</param>
	public unsafe int aaaFindText(bool utf16, RStr s, int start = 0, int end = -1) {
		aaaNormalizeRange(utf16, ref start, ref end);
		fixed (byte* b = _ToUtf8(s)) {
			var k = new Sci_TextToFind { cpMin = start, cpMax = end, lpstrText = b, chrgText = default };
			return _ReturnPosCanBeNegative(utf16, Call(SCI_FINDTEXT, SCFIND_MATCHCASE, &k));
		}
		//tested: with SCI_SEARCHINTARGET slightly slower
	}
	
	/// <summary>
	/// SCI_GETCHARAT.
	/// </summary>
	public char aaaCharAt8(int pos8) => (char)Call(SCI_GETCHARAT, pos8);
	
	/// <summary>
	/// SCI_BEGINUNDOACTION, SCI_ENDUNDOACTION.
	/// </summary>
	public void aaaAddUndoPoint() {
		Call(SCI_BEGINUNDOACTION);
		Call(SCI_ENDUNDOACTION);
	}
	
	/// <summary>
	/// SCI_BEGINUNDOACTION.
	/// </summary>
	/// <returns>Final counter of nested undo actions. Called SCI_BEGINUNDOACTION if it's 1.</returns>
	public int aaaBeginUndoAction() {
		if (_inUndoAction == 0) Call(SCI_BEGINUNDOACTION);
		return ++_inUndoAction;
	}
	int _inUndoAction;
	
	/// <summary>
	/// SCI_ENDUNDOACTION.
	/// </summary>
	/// <returns>Final counter of nested undo actions. Called SCI_ENDUNDOACTION if it's 0.</returns>
	public int aaaEndUndoAction() {
		Debug.Assert(_inUndoAction > 0);
		if (--_inUndoAction == 0) Call(SCI_ENDUNDOACTION);
		return _inUndoAction;
	}
	
	/// <summary>
	/// <c>=> new aaaUndoAction(this);</c>
	/// </summary>
	public aaaUndoAction aaaNewUndoAction(bool onUndoDontChangeCaretPos = false) => new aaaUndoAction(this, onUndoDontChangeCaretPos);
	
	/// <summary>
	/// Ctor calls <see cref="aaaBeginUndoAction"/>. Dispose() calls <see cref="aaaEndUndoAction"/>.
	/// Does nothing if it's a nested undo action.
	/// </summary>
	public struct aaaUndoAction : IDisposable {
		KScintilla _sci;
		bool _onUndoDontChangeCaretPos;
		
		/// <summary>
		/// Calls SCI_BEGINUNDOACTION.
		/// </summary>
		/// <param name="sci">Can be null, then does nothing.</param>
		/// <param name="onUndoDontChangeCaretPos"></param>
		public aaaUndoAction(KScintilla sci, bool onUndoDontChangeCaretPos = false) {
			_onUndoDontChangeCaretPos = onUndoDontChangeCaretPos;
			_sci = sci;
			_sci?.aaaBeginUndoAction();
		}
		
		/// <summary>
		/// Calls SCI_ENDUNDOACTION and clears this variable.
		/// </summary>
		public void Dispose() {
			if (_sci != null) {
				if (0 == _sci.aaaEndUndoAction()) {
					if (_onUndoDontChangeCaretPos) Sci_SetUndoMark(_sci.AaSciPtr, -1);
				}
				_sci = null;
			}
		}
	}
}

/// <summary>
/// Flags for 'set text', 'clear text' and similar functions. Eg you can disable Undo collection or 'changed' notifications.
/// Note: Ignores NoUndo and NoNotify if <b>AaInitReadOnlyAlways</b>, because then Undo and notifications are disabled when creating control.
/// </summary>
[Flags]
public enum SciSetTextFlags {
	/// <summary>
	/// Cannot be undone. Clear Undo buffer.
	/// </summary>
	NoUndo = 1,
	
	/// <summary>
	/// Don't send 'modified' and 'text changed' notifications (don't call overrides and events).
	/// </summary>
	NoNotify = 2,
	
	/// <summary>
	/// NoUndo | NoNotify.
	/// </summary>
	NoUndoNoNotify = 3,
}

/// <summary>
/// Provides fast direct access to a range of UTF-8 characters in Scintilla internal text.
/// Uses SCI_GETRANGEPOINTER. See <see cref="KScintilla.aaaRangePointer"/>.
/// Ensures that the gap is not moved (it could be slow if frequently).
/// </summary>
unsafe struct SciDirectRange {
	int _from, _to, _gap;
	byte* _p1, _p2; //before and after gap
	
	public SciDirectRange(KScintilla sci, int from8, int to8) {
		_from = from8;
		_to = to8;
		_gap = sci.Call(SCI_GETGAPPOSITION);
		//print.it(_from, _to, _gap);
		if (_gap > _from && _gap < _to) {
			_p1 = sci.aaaRangePointer(_from, _gap);
			_p2 = sci.aaaRangePointer(_gap, _to);
		} else {
			_p1 = sci.aaaRangePointer(_from, _to);
			_p2 = null;
		}
	}
	
	/// <summary>
	/// Returns character at position <i>i</i> in entire text (not from the start of the range).
	/// </summary>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public char this[int i] {
		get {
			if (i < _from || i >= _to) throw new IndexOutOfRangeException();
			if (_p2 == null || i < _gap) return (char)_p1[i - _from];
			return (char)_p2[i - _gap];
		}
	}
}
