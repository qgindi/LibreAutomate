/*
Most tags are like in QM2.

NEW TAGS:
   <bi> - bold italic.
   <mono> - monospace font.
   <size n> - font size (1-127).
   <fold> - collapsed lines.
   <explore> - select file in File Explorer.
   <\a>text</\a> - alternative for <_>text</_>.
   <nonl> - no newline.

NEW PARAMETERS:
   <c ColorName> - .NET color name for text color. Also color can be #RRGGBB.
   <bc ColorName> - .NET color name for background color. Also color can be #RRGGBB.
   <lc ColorName> - .NET color name for background color, whole line. Also color can be #RRGGBB.

RENAMED TAGS:
	<script>, was <macro>.
	<bc>, was <z>.
	<lc>, was <Z>.

REMOVED TAGS:
	<tip>.
	<mes>, <out>. Now use <fold>.

DIFFERENT SYNTAX:
	Most tags can be closed with <> or </> or </anything>.
		Except these: <_>text</_>, <\a>text</\a>, <code>code</code>, <fold>text</fold>.
		No closing tag: <image "file">.
	Attributes can be enclosed with "" or '' or non-enclosed (except for <image>).
		Does not support escape sequences. An attribute ends with "> (if starts with ") or '> (if starts with ') or > (if non-enclosed).
		In QM2 need "" for most; some can be non-enclosed. QM2 supports escape sequences.
	Link tag attribute parts now are separated with "|". In QM2 was " /".

OTHER CHANGES:
	Supports user-defined link tags. Need to provide delegates of functions that implement them. Use SciTags.AddCommonLinkTag or SciTags.AddLinkTag.
	These link tags are not implemented by this class, but you can provide delegates of functions that implement them:
		<open>, <script>.
	<help> by default calls Au.More.HelpUtil.AuHelp, which opens a topic in web browser. You can override it with SciTags.AddCommonLinkTag or SciTags.AddLinkTag.
	<code> attributes are not used.

CHANGES IN <image>:
	Don't need the closing tag (</image>).
	Currently supports only 16x16 icons. Does not support icon resources.
	Supports images embedded directly in text.
	More info in help topic "Output tags". File "Output tags.md".
*/

namespace Au.Controls;

using static Sci;

/// <summary>
/// Adds links and text formatting to a <see cref="KScintilla"/> control.
/// </summary>
/// <remarks>
/// Links and formatting is specified in text, using tags like in HTML. Depending on control style, may need prefix <c><![CDATA[<>]]></c>.
/// Reference: [](xref:output_tags).
/// Tags are supported by <see cref="print.it"/> when it writes to the Au script editor.
/// 
/// This control does not implement some predefined tags: open, script.
/// If used, must be implemented by the program.
/// Also you can register custom link tags that call your callback functions.
/// See <see cref="AddLinkTag"/>, <see cref="AddCommonLinkTag"/>.
/// 
/// Tags are supported by some existing controls based on <see cref="KScintilla"/>. In editor it is the output (use <see cref="print.it"/>, like in the example below). In this library - the <see cref="KSciInfoBox"/> control. To enable tags in other <see cref="KScintilla"/> controls, use <see cref="KScintilla.AaInitTagsStyle"/> and optionally <see cref="KScintilla.AaInitImages"/>.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// print.it("<>Text with <i>tags<>.");
/// ]]></code>
/// </example>
public unsafe class SciTags {
	const int STYLE_FIRST_EX = STYLE_LASTPREDEFINED + 1;
	const int NUM_STYLES_EX = STYLE_MAX - STYLE_LASTPREDEFINED;
	
	struct _TagStyle {
		uint u1, u2;
		
		//u1
		public int Color { get => (int)(u1 & 0xffffff); set => u1 = (u1 & 0xff000000) | ((uint)value & 0xffffff) | 0x1000000; }
		public bool HasColor => 0 != (u1 & 0x1000000);
		public int Size { get => (int)(u1 >> 25); set => u1 = (u1 & 0x1ffffff) | ((uint)Math.Clamp(value, 0, 127) << 25); }
		
		//u2
		public int BackColor { get => (int)(u2 & 0xffffff); set => u2 = (u2 & 0xff000000) | ((uint)value & 0xffffff) | 0x1000000; }
		public bool HasBackColor => 0 != (u2 & 0x1000000);
		public bool Bold { get => 0 != (u2 & 0x2000000); set { if (value) u2 |= 0x2000000; else u2 &= unchecked((uint)~0x2000000); } }
		public bool Italic { get => 0 != (u2 & 0x4000000); set { if (value) u2 |= 0x4000000; else u2 &= unchecked((uint)~0x4000000); } }
		public bool Underline { get => 0 != (u2 & 0x8000000); set { if (value) u2 |= 0x8000000; else u2 &= unchecked((uint)~0x8000000); } }
		public bool Eol { get => 0 != (u2 & 0x10000000); set { if (value) u2 |= 0x10000000; else u2 &= unchecked((uint)~0x10000000); } }
		public bool Hotspot { get => 0 != (u2 & 0x40000000); set { if (value) u2 |= 0x40000000; else u2 &= unchecked((uint)~0x40000000); } }
		public bool Mono { get => 0 != (u2 & 0x80000000); set { if (value) u2 |= 0x80000000; else u2 &= unchecked((uint)~0x80000000); } }
		
		public bool Equals(_TagStyle x) { return x.u1 == u1 && x.u2 == u2; }
		public void Merge(_TagStyle x) {
			var t1 = x.u1;
			if (HasColor) t1 &= 0xff000000;
			if (Size > 0) t1 &= 0x1ffffff;
			u1 |= t1;
			var t2 = x.u2;
			if (HasBackColor) {
				t2 &= 0xff000000;
				t2 &= unchecked((uint)~0x10000000); //don't inherit Eol
			}
			u2 |= t2;
		}
		public bool IsEmpty => u1 == 0 & u2 == 0;
		
		public _TagStyle(UserDefinedStyle k) {
			u1 = u2 = 0;
			if (k.textColor != null) Color = k.textColor.Value.argb;
			if (k.backColor != null) BackColor = k.backColor.Value.argb;
			Size = k.size;
			Bold = k.bold;
			Italic = k.italic;
			Underline = k.underline;
			Eol = k.eolFilled;
			Mono = k.monospace;
		}
	}
	
	/// <summary>
	/// For <see cref="AddStyleTag"/>.
	/// </summary>
	public class UserDefinedStyle {
		public ColorInt? textColor, backColor;
		public int size;
		public bool bold, italic, underline, eolFilled, monospace;
	}
	
	readonly KScintilla _c;
	readonly List<_TagStyle> _styles = new();
	
	internal SciTags(KScintilla c) {
		_c = c;
	}
	
	void _SetUserStyles(int from) {
		int i, j;
		for (i = from; i < _styles.Count; i++) {
			_TagStyle st = _styles[i];
			j = i + STYLE_FIRST_EX;
			if (st.HasColor) _c.aaaStyleForeColor(j, st.Color);
			if (st.HasBackColor) { _c.aaaStyleBackColor(j, st.BackColor); if (st.Eol) _c.aaaStyleEolFilled(j, true); }
			if (st.Bold) _c.aaaStyleBold(j, true);
			if (st.Italic) _c.aaaStyleItalic(j, true);
			if (st.Underline) _c.aaaStyleUnderline(j, true);
			if (st.Mono) _c.aaaStyleFont(j, "Consolas");
			if (st.Hotspot) _c.aaaStyleHotspot(j, true);
			int size = st.Size;
			if (size > 0) {
				if (size < 6 && st.Hotspot) size = 6;
				_c.aaaStyleFontSize(j, size);
			}
		}
	}
	
	/// <summary>
	/// Clears user-defined (through tags) styles.
	/// Max number of user styles is NUM_STYLES_EX (216). Need to clear old styles before new styles can be defined.
	/// This func is usually called after clearing control text.
	/// </summary>
	void _ClearUserStyles() {
		if (_styles.Count > 0) {
			_c.aaaStyleClearRange(STYLE_FIRST_EX);
			_styles.Clear();
		}
		//QM2 also cleared the image cache, but now it is shared by all controls of this thread.
	}
	
	internal void OnTextChanged_(bool inserted, in SCNotification n) {
		//if deleted or replaced all text, clear user styles
		if (!inserted && n.position == 0 && _c.aaaLen8 == 0) {
			_ClearUserStyles();
			//_linkDelegates.Clear(); //no
		}
	}
	
	/// <summary>
	/// Displays <see cref="PrintServer"/> messages that are currently in its queue.
	/// </summary>
	/// <param name="ps">The <b>PrintServer</b> instance.</param>
	/// <param name="onMessage">
	/// A callback function that can be called when this function gets/removes a message from ps.
	/// When message type is Write, it can change message text; if null, this function ignores the message.
	/// It also processes messages of type TaskEvent; this function ignores them.
	/// </param>
	/// <remarks>
	/// Removes messages from the queue.
	/// Appends text messages + "\r\n" to the control's text, or clears etc (depends on message).
	/// Messages with tags must have prefix "&lt;&gt;".
	/// Limits text length to about 4 MB (removes oldest text when exceeded).
	/// </remarks>
	/// <seealso cref="PrintServer.SetNotifications"/>
	public void PrintServerProcessMessages(PrintServer ps, Action<PrintServerMessage> onMessage = null) {
		//info: Cannot call _c.Write for each message, it's too slow. Need to join all messages.
		//	If multiple messages, use StringBuilder.
		//	If some messages have tags, use string "<\x15\x0\x4" to separate messages. Never mind: don't escape etc.
		
		string s = null;
		StringBuilder b = null;
		bool hasTags = false, hasTagsPrev = false, scrollToTop = false;
		while (ps.GetMessage(out var m)) {
			onMessage?.Invoke(m);
			switch (m.Type) {
			case PrintServerMessageType.Clear:
				_c.aaaClearText();
				s = null;
				b?.Clear();
				break;
			case PrintServerMessageType.ScrollToTop:
				scrollToTop = true;
				break;
			case PrintServerMessageType.Write when m.Text != null:
				if (s == null) {
					s = m.Text;
					hasTags = hasTagsPrev = s.Starts("<>");
				} else {
					b ??= new StringBuilder();
					if (b.Length == 0) b.Append(s);
					
					s = m.Text;
					
					bool hasTagsThis = m.Text.Starts("<>");
					if (hasTagsThis && !hasTags) { hasTags = true; b.Insert(0, "<\x15\x0\x4"); }
					
					if (!hasTags) {
						b.Append("\r\n");
					} else if (hasTagsThis) {
						b.Append("\r\n<\x15\x0\x4");
						//info: add "\r\n" here, not later, because later it would make more difficult <lc> tag
					} else {
						b.Append(hasTagsPrev ? "\r\n<\x15\x0\x4" : "\r\n");
					}
					b.Append(s);
					hasTagsPrev = hasTagsThis;
				}
				break;
			}
		}
		
		if (s != null) { //else 0 messages or the last message is Clear
			if (b != null) s = b.ToString();
			
			//limit
			int len = _c.aaaLen8;
			if (len > 4 * 1024 * 1024) {
				len = _c.aaaLineStartFromPos(false, len / 2);
				if (len > 0) _c.aaaReplaceRange(false, 0, len, "...\r\n");
			}
			
			if (hasTags) AddText(s, true, true);
			else _c.aaaAppendText(s, true, true, true);
			
			//test slow client
			//Thread.Sleep(500);
			//print.qm2.write(s.Length / 1048576d);
		}
		
		if (scrollToTop) _c.Call(SCI_SETFIRSTVISIBLELINE);
		//never mind: more print.it() may be after print.scrollToTop().
	}
	
	/// <summary>
	/// Sets or appends styled text.
	/// </summary>
	/// <param name="text">Text with tags (optionally).</param>
	/// <param name="append">Append. Also appends "\r\n". If false, replaces control text.</param>
	/// <param name="skipLTGT">If text starts with "&lt;&gt;", skip it.</param>
	/// <param name="scroll">Set caret and scroll to the end. If null, does it if <i>append</i> true.</param>
	public void AddText(string text, bool append, bool skipLTGT, bool? scroll = null) {
		//perf.first();
		if (text.NE() || (skipLTGT && text == "<>")) {
			if (append) _c.aaaAppendText("", true, true, true); else _c.aaaClearText();
			return;
		}
		
		int len = Encoding.UTF8.GetByteCount(text);
		byte* buffer = MemoryUtil.Alloc(len * 2 + 8), s = buffer;
		try {
			Encoding.UTF8.GetBytes(text, new Span<byte>(buffer, len));
			if (append) { s[len++] = (byte)'\r'; s[len++] = (byte)'\n'; }
			if (skipLTGT && s[0] == '<' && s[1] == '>') { s += 2; len -= 2; }
			s[len] = s[len + 1] = 0;
			_AddText(s, len, append, scroll);
		}
		finally {
			MemoryUtil.Free(buffer);
		}
	}
	
	record struct _StackItem(byte kind, byte style, int i = 0, string s = null, ushort attrLen = 0); //kind: 0 style, 1 link, 2 fold
	
	class _Garbage {
		public readonly List<_StackItem> stack = new();
		public readonly List<StartEnd> codes = new();
		public readonly List<POINT> folds = new();
		public readonly List<(int start, int end, string s)> links = new();
		
		public void Clear() {
			stack.Clear();
			codes.Clear();
			folds.Clear();
			links.Clear();
		}
	}
	[ThreadStatic] static WeakReference<_Garbage> t_garbage;
	
	void _AddText(byte* s, int len, bool append, bool? scroll) {
		//perf.next();
		byte* s0 = s, sEnd = s + len; //source text
		byte* t = s0; //destination text, ie without some tags
		byte* r0 = s0 + (len + 2), r = r0; //destination style bytes
		
		int prevStylesCount = _styles.Count;
		bool hasTags = false;
		byte currentStyle = STYLE_DEFAULT;
		
		if ((t_garbage ??= new(null)).TryGetTarget(out var m)) m.Clear(); else t_garbage.SetTarget(m = new());
		
		while (s < sEnd) {
			//find '<'
			var ch = *s++;
			if (ch != '<') {
				_Write(ch, currentStyle);
				continue;
			}
			
			var tag = s;
			
			//end tag. Support <> and </tag>, but don't care what tag it is.
			if (s[0] == '/') {
				s++;
				ch = *s; if ((char)ch is '+' or '.') s++;
				while (((char)*s).IsAsciiAlpha()) s++;
				if (s[0] != '>') goto ge;
			}
			if (s[0] == '>') {
				int n = m.stack.Count - 1;
				if (n < 0) goto ge; //<> without tag
				s++;
				var v = m.stack[n];
				if (v.kind is 0 or 1) { //the tag is a style tag or some other styled tag (eg link)
					if (currentStyle >= STYLE_FIRST_EX && _styles[currentStyle - STYLE_FIRST_EX].Eol) {
						if (*s == '\r') _Write(*s++, currentStyle);
						if (*s == '\n') _Write(*s++, currentStyle);
					}
					currentStyle = v.style;
					if (v.kind == 1) m.links.Add((v.i, (int)(t - s0), v.s));
				} else { //currently can be only 2 for <fold>
					if (!(s - tag == 6 && BytePtr_.AsciiStarts(tag + 1, "fold"))) goto ge;
					m.folds.Add((v.i, (int)(t - s0)));
					m.links.Add((v.i, v.i + v.attrLen, ""));
				}
				m.stack.RemoveAt(n);
				continue;
			}
			
			//multi-message separator
			if (s[0] == 0x15 && s[1] == 0 && s[2] == 4 && (s - s0 == 1 || s[-2] == 10)) {
				s += 3;
				if (s[0] == '<' && s[1] == '>') s += 2; //message with tags
				else { //one or more messages without tags
					while (s < sEnd && !(s[0] == '<' && s[1] == 0x15 && s[2] == 0 && s[3] == 4 && s[-1] == 10)) _Write(*s++, STYLE_DEFAULT);
				}
				currentStyle = STYLE_DEFAULT;
				m.stack.Clear();
				continue;
			}
			
			//read tag name
			ch = *s; if ((char)ch is '_' or '\a' or '+' or '.') s++;
			while (((char)*s).IsAsciiAlpha()) s++;
			int tagLen = (int)(s - tag);
			if (tagLen == 0) goto ge;
			
			//read attribute
			byte* attr = null; int attrLen = 0;
			if (*s == 32) {
				var quot = *(++s);
				if ((char)quot is '\'' or '"') s++; else quot = (byte)'>'; //never mind: escape sequences \\, \', \"
				attr = s;
				while (*s != quot && *s != 0) s++;
				if (*s == 0) goto ge; //either the end of string or a multi-message separator
				attrLen = (int)(s - attr);
				if (quot != '>' && *(++s) != '>') goto ge;
				s++;
			} else {
				if (*s++ != '>') goto ge;
			}
			
			//tags
			_TagStyle style = default;
			bool linkTag = false;
			int i2;
			ch = *tag;
			var span = new RByte(tag, tagLen);
			switch (tagLen << 16 | ch) {
			case 1 << 16 | 'b':
				style.Bold = true;
				break;
			case 1 << 16 | 'i':
				style.Italic = true;
				break;
			case 2 << 16 | 'b' when tag[1] == 'i':
				style.Bold = style.Italic = true;
				break;
			case 1 << 16 | 'u':
				style.Underline = true;
				break;
			case 1 << 16 | 'c':
			case 2 << 16 | 'b' when tag[1] == 'c':
			case 2 << 16 | 'l' when tag[1] == 'c':
			case 2 << 16 | 'B' when tag[1] == 'C': //fbc
			case 1 << 16 | 'z' or 1 << 16 | 'Z': //fbc
				if (attr == null) goto ge;
				int color;
				if (((char)*attr).IsAsciiDigit()) color = Api.strtoi(attr);
				else if (*attr == '#') color = Api.strtoi(attr + 1, radix: 16);
				else {
					var c = System.Drawing.Color.FromName(new string((sbyte*)attr, 0, attrLen));
					if (c.A == 0) break; //invalid color name
					color = c.ToArgb() & 0xffffff;
				}
				if (ch == 'c') style.Color = color; else style.BackColor = color;
				if ((char)ch is 'l' or 'B' or 'Z') style.Eol = true;
				break;
			case 4 << 16 | 's' when span.SequenceEqual("size"u8) && attr != null:
				style.Size = Api.strtoi(attr);
				break;
			case 4 << 16 | 'm' when span.SequenceEqual("mono"u8):
				style.Mono = true;
				break;
			//case 6 << 16 | 'h' when span.SequenceEqual("hidden"u8): //rejected. Not useful; does not hide newlines.
			//	style.Hidden = true;
			//	break;
			case 5 << 16 | 'i' when span.SequenceEqual("image"u8) && attr != null:
				for (var h = tag - 1; h < s; h++) _Write(*h, STYLE_HIDDEN);
				hasTags = true;
				continue;
			case 4 << 16 | 'n' when span.SequenceEqual("nonl"u8):
				if (s[0] == 13) s++;
				if (s[0] == 10) s++;
				continue;
			case 1 << 16 | '_': //<_>text where tags are ignored</_>
			case 1 << 16 | '\a': //<\a>text where tags are ignored</\a>
				i2 = BytePtr_.AsciiFindString(s, (int)(sEnd - s), ch == '_' ? "</_>" : "</\a>"); if (i2 < 0) goto ge;
				while (i2-- > 0) _Write(*s++, currentStyle);
				s += 4;
				continue;
			case 4 << 16 | 'c' when span.SequenceEqual("code"u8): //<code>code</code>
				i2 = BytePtr_.AsciiFindString(s, (int)(sEnd - s), "</code>"); if (i2 < 0) goto ge;
				if (CodeStylesProvider != null) {
					int iStartCode = (int)(t - s0);
					m.codes.Add(new(iStartCode, iStartCode + i2));
					hasTags = true;
				}
				while (i2-- > 0) _Write(*s++, STYLE_DEFAULT);
				s += 7;
				continue;
			case 4 << 16 | 'f' when span.SequenceEqual("fold"u8): //<fold>text</fold>
				bool foldHasAttr = attrLen > 0; if (foldHasAttr) attrLen = Math.Min(attrLen, ushort.MaxValue);
				m.stack.Add(new(2, 0, (int)(t - s0), attrLen: (ushort)(foldHasAttr ? attrLen : 2)));
				//add 'expand/collapse' link in this line
				byte foldStyle = _GetStyleIndex(new _TagStyle { Hotspot = true, Underline = true, Color = 0x80FF }, currentStyle, m.stack);
				if (!foldHasAttr) _WriteString(">>", foldStyle); else for (int i = 0; i < attrLen; i++) _Write(attr[i], foldStyle);
				//let the folded text start from next line
				var s1 = s; if (s1[0] == '<' && (char)s1[1] is '_' or '\a' && s1[2] == '>') s1 += 3;
				if (s1[0] is not (10 or 13)) _WriteString("\r\n", currentStyle);
				hasTags = true;
				continue;
			case 4 << 16 | 'l' when span.SequenceEqual("link"u8):
			case 6 << 16 | 'g' when span.SequenceEqual("google"u8):
			case 4 << 16 | 'h' when span.SequenceEqual("help"u8):
			case 7 << 16 | 'e' when span.SequenceEqual("explore"u8):
			case 4 << 16 | 'o' when span.SequenceEqual("open"u8):
			case 6 << 16 | 's' when span.SequenceEqual("script"u8):
				linkTag = true;
				break;
			default:
				//user-defined tag or unknown.
				//user-defined tags must start with '+' (links) or '.' (styles).
				//don't hide unknown tags, unless start with '+'. Can be either misspelled (hiding would make harder to debug) or not intended for us (forgot <_>).
				if (ch == '+') {
					//if(!_userLinkTags.ContainsKey(new string((sbyte*)tag, 0, tagLen))) goto ge; //no, it makes slower and creates garbage. Also would need to look in the static dictionary too. It's not so important to check now because we use '+' prefix.
					linkTag = true;
					break;
				} else if (ch == '.' && (_userStyles?.TryGetValue(new string((sbyte*)tag, 0, tagLen), out style) ?? false)) {
					break;
				}
				goto ge;
			}
			
			if (linkTag) {
				if (_linkStyle != null) style = new _TagStyle(_linkStyle);
				else {
					style.Color = 0x0080FF;
					style.Underline = true;
				}
				style.Hotspot = true;
			}
			
			byte si = _GetStyleIndex(style, currentStyle, m.stack);
			if (linkTag) {
				m.stack.Add(new(1, currentStyle, (int)(t - s0), Encoding.UTF8.GetString(tag, (int)(s - tag - 1))));
			} else {
				m.stack.Add(new(0, currentStyle));
			}
			currentStyle = si;
			
			hasTags = true;
			continue;
			ge: //invalid format of the tag
			_Write((byte)'<', currentStyle);
			s = tag;
		}
		
		Debug.Assert(t <= s0 + len);
		Debug.Assert(r <= r0 + len);
		Debug.Assert(t - s0 == r - r0);
		*t = 0; len = (int)(t - s0);
		
		if (_styles.Count > prevStylesCount) _SetUserStyles(prevStylesCount);
		
		//perf.next();
		int prevLen = append ? _c.aaaLen8 : 0;
		_c.aaaAddText8_(append, scroll ?? append, s0, len);
		if (!hasTags) {
			_c.Call(SCI_STARTSTYLING, prevLen);
			_c.Call(SCI_SETSTYLING, len, STYLE_DEFAULT);
			return;
		}
		
		foreach (var v in m.links) {
			_c.AaRangeDataAdd(false, (prevLen + v.start)..(prevLen + v.end), v.s);
		}
		
		for (int i = m.folds.Count; --i >= 0;) { //need reverse for nested folds
			var v = m.folds[i];
			int lineStart = _c.Call(SCI_LINEFROMPOSITION, v.x + prevLen), lineEnd = _c.Call(SCI_LINEFROMPOSITION, v.y + prevLen);
			int level = _c.Call(SCI_GETFOLDLEVEL, lineStart) & SC_FOLDLEVELNUMBERMASK;
			_c.Call(SCI_SETFOLDLEVEL, lineStart, level | SC_FOLDLEVELHEADERFLAG);
			for (int j = lineStart + 1; j <= lineEnd; j++) _c.Call(SCI_SETFOLDLEVEL, j, level + 1);
			_c.Call(SCI_FOLDLINE, lineStart);
		}
		
		int endStyled = 0;
		foreach (var v in m.codes) {
			_StyleRangeTo(v.start);
			var code = Encoding.UTF8.GetString(s0 + v.start, v.Length);
			//print.qm2.write(v, code);
			var b = CodeStylesProvider(code);
			_c.Call(SCI_STARTSTYLING, v.start + prevLen);
			fixed (byte* p = b) _c.Call(SCI_SETSTYLINGEX, b.Length, p);
			endStyled = v.end;
		}
		
		_StyleRangeTo(len);
		//perf.next();
		//print.qm2.write(perf.ToString());
		
		void _StyleRangeTo(int to) {
			if (endStyled < to) {
				_c.Call(SCI_STARTSTYLING, endStyled + prevLen);
				_c.Call(SCI_SETSTYLINGEX, to - endStyled, r0 + endStyled);
			}
		}
		
		void _Write(byte ch, byte style) {
			//print.qm2.write($"{ch} {style}");
			*t++ = ch; *r++ = style;
		}
		
		void _WriteString(string ss, byte style) {
			for (int i_ = 0; i_ < ss.Length; i_++) _Write((byte)ss[i_], style);
		}
	}
	
	byte _GetStyleIndex(_TagStyle style, byte currentStyle, List<_StackItem> stack) {
		//merge nested style with ancestors
		if (currentStyle >= STYLE_FIRST_EX) style.Merge(_styles[currentStyle - STYLE_FIRST_EX]);
		for (int j = stack.Count; --j > 0;) {
			var v = stack[j];
			if (v.kind is not (0 or 1)) continue; //a non-styled tag
			if (v.style >= STYLE_FIRST_EX) style.Merge(_styles[v.style - STYLE_FIRST_EX]);
		}
		
		//find or add style
		int i, n = _styles.Count;
		for (i = 0; i < n; i++) if (_styles[i].Equals(style)) break;
		if (i == NUM_STYLES_EX) {
			i = currentStyle;
			//CONSIDER: overwrite old styles added in previous calls. Now we just clear styles when control text cleared.
		} else {
			if (i == n) _styles.Add(style);
			i += STYLE_FIRST_EX;
		}
		return (byte)i;
	}
	
	/// <summary>
	/// Called on SCN_HOTSPOTRELEASECLICK.
	/// </summary>
	internal void OnLinkClick_(int pos, bool ctrl) {
		if (keys.gui.isAlt) return;
		if (!GetLinkFromPos(pos, out var tag, out var attr)) return;
		//process it async, because bad things happen if now we remove focus or change control text etc
		_c.Dispatcher.InvokeAsync(() => OnLinkClick(tag, attr));
	}
	
	public bool GetLinkFromPos(int pos, out string tag, out string attr) {
		tag = attr = null;
		if (pos >= 0 && _c.AaRangeDataGet(false, pos, out string s, out int from, out int to)) {
			if (s.Length == 0) { //<fold>
				_c.Call(SCI_TOGGLEFOLD, _c.Call(SCI_LINEFROMPOSITION, pos));
			} else if (s.RxMatch(@"(?s)^(\+?\w+)(?| '([^']*)'| ""([^""]*)""| ([^>]*))?$", out var m)) {
				tag = m[1].Value;
				attr = m[2].Value ?? _c.aaaRangeText(false, from, to);
				return true;
			}
		}
		return false;
	}
	
	//note: attr can be ""
	public void OnLinkClick(string tag, string attr) {
		//print.it($"'{tag}'  '{attr}'");
		
		if (_userLinkTags.TryGetValue(tag, out var d) || s_userLinkTags.TryGetValue(tag, out d)) {
			d.Invoke(attr);
			return;
		}
		
		var a = attr.Split('|');
		bool one = a.Length == 1;
		string s1 = a[0], s2 = one ? null : a[1];
		
		switch (tag) {
		case "link":
			run.itSafe(s1, s2);
			break;
		case "google":
			run.itSafe("https://www.google.com/search?q=" + System.Net.WebUtility.UrlEncode(s1) + s2);
			break;
		case "help":
			HelpUtil.AuHelp(attr);
			break;
		case "explore":
			run.selectInExplorer(attr);
			break;
		default:
			//case "open": case "script": //the control recognizes but cannot implement these. The lib user can implement.
			//others are unregistered tags. Only if start with '+' (others are displayed as text).
			if (opt.warnings.Verbose) dialog.showWarning("Debug", "Tag '" + tag + "' is not implemented.\nUse SciTags.AddCommonLinkTag or SciTags.AddLinkTag.");
			break;
		}
	}
	
	public void SetLinkStyle(UserDefinedStyle style, (bool use, ColorInt color)? activeColor = null, bool? activeUnderline = null) {
		_linkStyle = style;
		if (activeColor != null) {
			var v = activeColor.Value;
			_c.Call(SCI_SETHOTSPOTACTIVEFORE, v.use, v.color.ToBGR());
		}
		if (activeUnderline != null) _c.Call(SCI_SETHOTSPOTACTIVEUNDERLINE, activeUnderline.Value);
	}
	UserDefinedStyle _linkStyle;
	
	readonly Dictionary<string, Action<string>> _userLinkTags = new();
	static readonly ConcurrentDictionary<string, Action<string>> s_userLinkTags = new();
	
	/// <summary>
	/// Adds (registers) a user-defined link tag for this control.
	/// </summary>
	/// <param name="name">
	/// Tag name, like "+myTag".
	/// Must start with '+'. Other characters must be 'a'-'z', 'A'-'Z'. Case-sensitive.
	/// Or can be one of predefined link tags, if you want to override or implement it (some are not implemented by the control).
	/// If already exists, replaces the delegate.
	/// </param>
	/// <param name="a">
	/// A delegate of a callback function (probably you'll use a lambda) that is called on link click.
	/// It's string parameter contains tag's attribute (if "&lt;name "attribute"&gt;TEXT&lt;&gt;) or link text (if "&lt;name&gt;TEXT&lt;&gt;).
	/// The function is called in control's thread. The mouse button is already released. It is safe to do anything with the control, eg replace text.
	/// </param>
	/// <remarks>
	/// Call this function when control handle is already created. Until that <see cref="KScintilla.AaTags"/> returns null.
	/// </remarks>
	/// <seealso cref="AddCommonLinkTag"/>
	public void AddLinkTag(string name, Action<string> a) {
		_userLinkTags[name] = a;
	}
	
	/// <summary>
	/// Adds (registers) a user-defined link tag for all controls.
	/// </summary>
	/// <param name="name">
	/// Tag name, like "+myTag".
	/// Must start with '+'. Other characters must be 'a'-'z', 'A'-'Z'. Case-sensitive.
	/// Or can be one of predefined link tags, if you want to override or implement it (some are not implemented by the control).
	/// If already exists, replaces the delegate.
	/// </param>
	/// <param name="a">
	/// A delegate of a callback function (probably you'll use a lambda) that is called on link click.
	/// It's string parameter contains tag's attribute (if "&lt;name "attribute"&gt;TEXT&lt;&gt;) or link text (if "&lt;name&gt;TEXT&lt;&gt;).
	/// The function is called in control's thread. The mouse button is already released. It is safe to do anything with the control, eg replace text.
	/// </param>
	/// <seealso cref="AddLinkTag"/>
	public static void AddCommonLinkTag(string name, Action<string> a) {
		s_userLinkTags[name] = a;
	}
	
	/// <summary>
	/// Adds (registers) a user-defined style tag for this control.
	/// </summary>
	/// <param name="name">
	/// Tag name, like ".my".
	/// Must start with '.'. Other characters must be 'a'-'z', 'A'-'Z'. Case-sensitive.
	/// </param>
	/// <param name="style"></param>
	/// <exception cref="ArgumentException">name does not start with '.'.</exception>
	/// <exception cref="InvalidOperationException">Trying to add more than 100 styles.</exception>
	/// <remarks>
	/// Call this function when control handle is already created. Until that <see cref="KScintilla.AaTags"/> returns null.
	/// </remarks>
	public void AddStyleTag(string name, UserDefinedStyle style) {
		_userStyles ??= new();
		if (_userStyles.Count >= 100) throw new InvalidOperationException();
		if (!name.Starts('.')) throw new ArgumentException();
		_userStyles.Add(name, new _TagStyle(style));
	}
	Dictionary<string, _TagStyle> _userStyles;
	
	public Func<string, byte[]> CodeStylesProvider;
	
	//FUTURE: add control-tags, like <clear> (clear output), <scroll> (ensure line visible), <mark x> (add some marker etc).
	//FUTURE: let our links be accessible objects.
}
