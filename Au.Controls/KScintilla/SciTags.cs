/*
Most tags are like in QM2.

NEW TAGS:
   <bi> - bold italic.
   <mono> - monospace font.
   <size n> - font size (1-127).
   <fold> - collapsed lines.
   <explore> - select file in File Explorer.
   <\a>text</\a> - alternative for <_>text</_>.

NEW PARAMETERS:
   <c ColorName> - .NET color name for text color. Also color can be #RRGGBB.
   <z ColorName> - .NET color name for background color. Also color can be #RRGGBB.
   <Z ColorName> - .NET color name for background color, whole line. Also color can be #RRGGBB.

RENAMED TAGS:
	<script>, was <macro>.

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
	<code> attributes are not used. Currently supports only C# code; for it uses the C++ lexer.

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
/// Tags are supported by some existing controls based on <see cref="KScintilla"/>. In editor it is the output (use <see cref="print.it"/>, like in the example below). In this library - the <see cref="KSciInfoBox"/> control. To enable tags in other <see cref="KScintilla"/> controls, use <see cref="KScintilla.ZInitTagsStyle"/> and optionally <see cref="KScintilla.ZInitImages"/>.
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
			if (k.textColor != null) Color = k.textColor.GetValueOrDefault().argb;
			if (k.backColor != null) BackColor = k.backColor.GetValueOrDefault().argb;
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

	KScintilla _c;
	List<_TagStyle> _styles = new List<_TagStyle>();
	List<int> _stack = new List<int>();

	internal SciTags(KScintilla c) {
		_c = c;
	}

	void _SetUserStyles(int from) {
		int i, j;
		for (i = from; i < _styles.Count; i++) {
			_TagStyle st = _styles[i];
			j = i + STYLE_FIRST_EX;
			if (st.HasColor) _c.zStyleForeColor(j, st.Color);
			if (st.HasBackColor) { _c.zStyleBackColor(j, st.BackColor); if (st.Eol) _c.zStyleEolFilled(j, true); }
			if (st.Bold) _c.zStyleBold(j, true);
			if (st.Italic) _c.zStyleItalic(j, true);
			if (st.Underline) _c.zStyleUnderline(j, true);
			if (st.Mono) _c.zStyleFont(j, "Consolas");
			if (st.Hotspot) _c.zStyleHotspot(j, true);
			int size = st.Size;
			if (size > 0) {
				if (size < 6 && st.Hotspot) size = 6;
				_c.zStyleFontSize(j, size);
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
			_c.zStyleClearRange(STYLE_FIRST_EX);
			_styles.Clear();
		}
		//QM2 also cleared the image cache, but now it is shared by all controls of this thread.
	}

	internal void OnTextChanged_(bool inserted, in SCNotification n) {
		//if deleted or replaced all text, clear user styles
		if (!inserted && n.position == 0 && _c.zLen8 == 0) {
			_ClearUserStyles();
			//_linkDelegates.Clear(); //no
		}
	}

	/// <summary>
	/// Displays <see cref="print.Server"/> messages that are currently in its queue.
	/// </summary>
	/// <param name="ps">The print.Server instance.</param>
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
	/// <seealso cref="print.Server.SetNotifications"/>
	public void PrintServerProcessMessages(print.Server ps, Action<PrintServerMessage> onMessage = null) {
		//info: Cannot call _c.Write for each message, it's too slow. Need to join all messages.
		//	If multiple messages, use StringBuilder.
		//	If some messages have tags, use string "<\x15\x0\x4" to separate messages. Never mind: don't escape etc.

		string s = null;
		StringBuilder b = null;
		bool hasTags = false, hasTagsPrev = false;
		while (ps.GetMessage(out var m)) {
			onMessage?.Invoke(m);
			switch (m.Type) {
			case PrintServerMessageType.Clear:
				_c.zClearText();
				s = null;
				b?.Clear();
				break;
			case PrintServerMessageType.Write when m.Text != null:
				if (s == null) {
					s = m.Text;
					hasTags = hasTagsPrev = s.Starts("<>");
				} else {
					if (b == null) b = new StringBuilder();
					if (b.Length == 0) b.Append(s);

					s = m.Text;

					bool hasTagsThis = m.Text.Starts("<>");
					if (hasTagsThis && !hasTags) { hasTags = true; b.Insert(0, "<\x15\x0\x4"); }

					if (!hasTags) {
						b.Append("\r\n");
					} else if (hasTagsThis) {
						b.Append("\r\n<\x15\x0\x4");
						//info: add "\r\n" here, not later, because later it would make more difficult <Z> tag
					} else {
						b.Append(hasTagsPrev ? "\r\n<\x15\x0\x4" : "\r\n");
					}
					b.Append(s);
					hasTagsPrev = hasTagsThis;
				}
				break;
			}
		}
		if (s == null) return; //0 messages, or the last message is Clear
		if (b != null && b.Length > 0) s = b.ToString();

		//if(sb!=null) s += " >>>> " + sb.Capacity.ToString();

		//_c.zAppendText(s, true, true, true); return;

		//limit
		int len = _c.zLen8;
		if (len > 4 * 1024 * 1024) {
			len = _c.zLineStartFromPos(false, len / 2);
			if (len > 0) _c.zReplaceRange(false, 0, len, "...\r\n");
		}

		if (hasTags) AddText(s, true, true);
		else _c.zAppendText(s, true, true, true);

		//test slow client
		//Thread.Sleep(500);
		//print.qm2.write(s.Length / 1048576d);
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
			if (append) _c.zAppendText("", true, true, true); else _c.zClearText();
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

	void _AddText(byte* s, int len, bool append, bool? scroll) {
		//perf.next();
		byte* s0 = s, sEnd = s + len; //source text
		byte* t = s0; //destination text, ie without some tags
		byte* r0 = s0 + (len + 2), r = r0; //destination style bytes

		int prevStylesCount = _styles.Count;
		bool hasTags = false;
		byte currentStyle = STYLE_DEFAULT;
		_stack.Clear();
		List<StartEnd> codes = null;
		List<POINT> folds = null;

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
				ch = *s; if (ch == '+' || ch == '.') s++;
				while (((char)*s).IsAsciiAlpha()) s++;
				if (s[0] != '>') goto ge;
			}
			if (s[0] == '>') {
				int n = _stack.Count - 1;
				if (n < 0) goto ge; //<> without tag
				s++;
				int i = _stack[n];
				if (i >= 0) { //the tag is a style tag or some other styled tag (eg link)
					if (currentStyle >= STYLE_FIRST_EX && _styles[currentStyle - STYLE_FIRST_EX].Eol) {
						if (*s == '\r') _Write(*s++, currentStyle);
						if (*s == '\n') _Write(*s++, currentStyle);
					}
					currentStyle = (byte)i;
				} else { //currently can be only <fold>
					i &= 0x7fffffff;
					if (!(s - tag == 6 && BytePtr_.AsciiStarts(tag + 1, "fold"))) goto ge;
					(folds ??= new List<POINT>()).Add((i, (int)(t - s0)));
					//if(s < sEnd && *s != '\r' && *s != '\n') _WriteString("\r\n", STYLE_DEFAULT); //no, can be an end of tag there
				}
				_stack.RemoveAt(n);
				continue;
			}
			//SHOULDDO: ignore unclosed tags, like in string "ab <b>cd".

			//multi-message separator
			if (s[0] == 0x15 && s[1] == 0 && s[2] == 4 && (s - s0 == 1 || s[-2] == 10)) {
				s += 3;
				if (s[0] == '<' && s[1] == '>') s += 2; //message with tags
				else { //one or more messages without tags
					while (s < sEnd && !(s[0] == '<' && s[1] == 0x15 && s[2] == 0 && s[3] == 4 && s[-1] == 10)) _Write(*s++, STYLE_DEFAULT);
				}
				currentStyle = STYLE_DEFAULT;
				_stack.Clear();
				continue;
			}

			//read tag name
			ch = *s; if (ch == '_' || ch == '\a' || ch == '+' || ch == '.') s++;
			while (((char)*s).IsAsciiAlpha()) s++;
			int tagLen = (int)(s - tag);
			if (tagLen == 0) goto ge;

			//read attribute
			byte* attr = null; int attrLen = 0;
			if (*s == 32) {
				s++;
				var quot = *s;
				if (quot == '\'' || quot == '\"') s++; else quot = (byte)'>'; //never mind: escape sequences \\, \', \"
				int n = (int)(sEnd - s);
				int i = (quot == '>') ? BytePtr_.AsciiFindChar(s, n, quot) : BytePtr_.AsciiFindString(s, n, (quot == '\'') ? "'>" : "\">");
				if (i < 0) goto ge;
				attr = s; s += i + 1; attrLen = i;
				if (quot != '>') s++;
				else if (s[-2] == '<') goto ge; //<tag attr TEXT<>
			} else {
				if (*s != '>') goto ge;
				s++;
			}

			//tags
			_TagStyle style = default;
			bool hideTag = false, noEndTag = false, userLinkTag = false;
			string linkTag = null;
			int stackInt = 0;
			int i2;
			ch = *tag;
			switch (tagLen << 16 | ch) {
			case 1 << 16 | 'b':
				style.Bold = true;
				break;
			case 1 << 16 | 'i':
				style.Italic = true;
				break;
			case 2 << 16 | 'b':
				if (tag[1] == 'i') style.Bold = style.Italic = true;
				else goto ge;
				break;
			case 1 << 16 | 'u':
				style.Underline = true;
				break;
			case 1 << 16 | 'c':
			case 1 << 16 | 'z':
			case 1 << 16 | 'Z':
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
				if (ch == 'Z') style.Eol = true;
				break;
			case 4 << 16 | 's':
				if (attr == null) goto ge;
				if (BytePtr_.AsciiStarts(tag + 1, "ize")) style.Size = Api.strtoi(attr);
				else goto ge;
				break;
			case 4 << 16 | 'm':
				if (BytePtr_.AsciiStarts(tag + 1, "ono")) style.Mono = true;
				else goto ge;
				break;
			//case 6 << 16 | 'h': //rejected. Not useful; does not hide newlines.
			//	if(CharPtr_.AsciiStartsWith(tag + 1, "idden")) style.Hidden = true;
			//	else goto ge;
			//	break;
			case 5 << 16 | 'i':
				if (attr == null) goto ge;
				if (!BytePtr_.AsciiStarts(tag + 1, "mage")) goto ge;
				hideTag = noEndTag = true;
				break;
			case 1 << 16 | '_': //<_>text where tags are ignored</_>
			case 1 << 16 | '\a': //<\a>text where tags are ignored</\a>
				i2 = BytePtr_.AsciiFindString(s, (int)(sEnd - s), ch == '_' ? "</_>" : "</\a>"); if (i2 < 0) goto ge;
				while (i2-- > 0) _Write(*s++, currentStyle);
				s += 4;
				//hasTags = true;
				continue;
			case 4 << 16 | 'c': //<code>code</code>
				if (!BytePtr_.AsciiStarts(tag + 1, "ode")) goto ge;
				i2 = BytePtr_.AsciiFindString(s, (int)(sEnd - s), "</code>"); if (i2 < 0) goto ge;
				if (CodeStylesProvider != null) {
					int iStartCode = (int)(t - s0);
					(codes ??= new()).Add(new(iStartCode, iStartCode + i2));
					hasTags = true;
				}
				while (i2-- > 0) _Write(*s++, STYLE_DEFAULT);
				s += 7;
				continue;
			case 4 << 16 | 'f': //<fold>text</fold>
				if (!BytePtr_.AsciiStarts(tag + 1, "old")) goto ge;
				stackInt = (int)(t - s0);
				//add 'expand/collapse' link in this line. Max 6 characters, because overwriting "<fold>".
				_WriteString(" ", STYLE_HIDDEN); //it is how we later detect links
				_WriteString(">>", _GetStyleIndex(new _TagStyle { Hotspot = true, Underline = true, Color = 0x80FF }, currentStyle));
				//let the folded text start from next line
				var s1 = s; if (s1[0] == '<' && (s1[1] == '_' || s1[1] == '\a') && s1[2] == '>') s1 += 3;
				switch (*s1) { case 10: case 13: break; default: _WriteString("\r\n", currentStyle); break; }
				break;
			case 4 << 16 | 'l':
				linkTag = "link";
				break;
			case 6 << 16 | 'g':
				linkTag = "google";
				break;
			case 4 << 16 | 'h':
				linkTag = "help";
				break;
			case 7 << 16 | 'e':
				linkTag = "explore";
				break;
			case 4 << 16 | 'o':
				linkTag = "open";
				break;
			case 6 << 16 | 's':
				linkTag = "script";
				break;
			default:
				//user-defined tag or unknown.
				//user-defined tags must start with '+' (links) or '.' (styles).
				//don't hide unknown tags, unless start with '+' etc. Can be either misspelled (hiding would make harder to debug) or not intended for us (forgot <_>).
				if (ch == '+') {
					//if(!_userLinkTags.ContainsKey(linkTag = new string((sbyte*)tag, 0, tagLen))) goto ge; //no, it makes slower and creates garbage. Also would need to look in the static dictionary too. It's not so important to check now because we use '+' prefix.
					//info: initially was used '_', not '+'. But it creates more problems. Eg C# stack trace can contain "... at A.<>c.<_Main>b__1_0() ...".
					linkTag = "";
					userLinkTag = true;
				} else if (ch == '.' && (_userStyles?.TryGetValue(new string((sbyte*)tag, 0, tagLen), out style) ?? false)) {
					//userStyleTag = true;
				} else goto ge;
				break;
			}

			if (linkTag != null) {
				if (!userLinkTag && !BytePtr_.AsciiStarts(tag, linkTag)) goto ge;
				//if(attr == null) goto ge; //no, use text as attribute
				if (_linkStyle != null) style = new _TagStyle(_linkStyle);
				else {
					style.Color = 0x0080FF;
					style.Underline = true;
				}
				style.Hotspot = true;
				hideTag = true;
			}

			if (hideTag) {
				for (var h = tag - 1; h < s; h++) _Write(*h, STYLE_HIDDEN);
			}

			hasTags = true;
			if (noEndTag) continue;

			if (!style.IsEmpty) {
				byte si = _GetStyleIndex(style, currentStyle);
				_stack.Add(currentStyle);
				currentStyle = si;
			} else {
				int k = unchecked((int)0x80000000); //no-style flag
				k |= stackInt;
				_stack.Add(k);
			}

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
		int prevLen = append ? _c.zLen8 : 0;
		_c.zAddText8_(append, scroll ?? append, s0, len);
		if (!hasTags) {
			_c.Call(SCI_STARTSTYLING, prevLen);
			_c.Call(SCI_SETSTYLING, len, STYLE_DEFAULT);
			return;
		}

		if (folds != null) {
			for (int i = folds.Count - 1; i >= 0; i--) { //need reverse for nested folds
				var v = folds[i];
				int lineStart = _c.Call(SCI_LINEFROMPOSITION, v.x + prevLen), lineEnd = _c.Call(SCI_LINEFROMPOSITION, v.y + prevLen);
				int level = _c.Call(SCI_GETFOLDLEVEL, lineStart) & SC_FOLDLEVELNUMBERMASK;
				_c.Call(SCI_SETFOLDLEVEL, lineStart, level | SC_FOLDLEVELHEADERFLAG);
				for (int j = lineStart + 1; j <= lineEnd; j++) _c.Call(SCI_SETFOLDLEVEL, j, level + 1);
				_c.Call(SCI_FOLDLINE, lineStart);
			}
		}

		int endStyled = 0;
		if (codes != null) {
			for (int i = 0; i < codes.Count; i++) {
				var v = codes[i];
				_StyleRangeTo(v.start);
				var code = Encoding.UTF8.GetString(s0 + v.start, v.Length);
				//print.qm2.write(v, code);
				var b = CodeStylesProvider(code);
				_c.Call(SCI_STARTSTYLING, v.start + prevLen);
				fixed (byte* p = b) _c.Call(SCI_SETSTYLINGEX, b.Length, p);
				endStyled = v.end;
			}
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

	byte _GetStyleIndex(_TagStyle style, byte currentStyle) {
		//merge nested style with ancestors
		int k = currentStyle;
		if (k >= STYLE_FIRST_EX) style.Merge(_styles[k - STYLE_FIRST_EX]);
		for (int j = _stack.Count - 1; j > 0; j--) {
			k = _stack[j];
			if (k < 0) continue; //a non-styled tag
			k &= 0xff; //remove other possible flags
			if (k >= STYLE_FIRST_EX) style.Merge(_styles[k - STYLE_FIRST_EX]);
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
		_c.Dispatcher.InvokeAsync(() => _OnLinkClick(tag, attr));
	}

	public bool GetLinkFromPos(int pos, out string tag, out string attr) {
		tag = attr = null;
		if (pos <= 0) return false;

		int iTag, iText, k;
		//to find the start of link text (after <tag>), search for STYLE_HIDDEN before
		for (iText = pos; iText > 0; iText--) if (_c.zGetStyleAt(iText - 1) == STYLE_HIDDEN) break;
		if (iText == 0) return false;
		//to find the start of <tag>, search for some other style before
		for (iTag = iText - 1; iTag > 0; iTag--) if (_c.zGetStyleAt(iTag - 1) != STYLE_HIDDEN) break;
		//to find the end of link text, search for a non-hotspot style after
		for (pos++; /*SCI_GETSTYLEAT returns 0 if index invalid, it is documented*/; pos++) {
			k = _c.zGetStyleAt(pos);
			if (k < STYLE_FIRST_EX || !_c.zStyleHotspot(k)) break;
		}
		//get text <tag>LinkText
		var s = _c.zRangeText(false, iTag, pos);
		//print.it(iTag, iText, pos, s);

		//is it <fold>?
		if (s == " >>") {
			int line = _c.Call(SCI_LINEFROMPOSITION, iTag);
			_c.Call(SCI_TOGGLEFOLD, line);
			return false;
		}
		//get tag, attribute and text
		if (!s.RxMatch(@"(?s)^<(\+?\w+)(?| '([^']*)'| ""([^""]*)""| ([^>]*))?>(.+)", out var m)) return false;
		tag = m[1].Value; attr = m[2].Value ?? m[3].Value;
		//print.it($"'{tag}'  '{attr}'");

		return true;
	}

	//note: attr can be ""
	void _OnLinkClick(string tag, string attr) {
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
			run.itSafe("https://www.google.com/search?q=" + Uri.EscapeDataString(s1) + s2);
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
			var v = activeColor.GetValueOrDefault();
			_c.Call(SCI_SETHOTSPOTACTIVEFORE, v.use, v.color.ToBGR());
		}
		if (activeUnderline != null) _c.Call(SCI_SETHOTSPOTACTIVEUNDERLINE, activeUnderline.GetValueOrDefault());
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
	/// Call this function when control handle is already created. Until that <see cref="KScintilla.ZTags"/> returns null.
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
	/// Call this function when control handle is already created. Until that <see cref="KScintilla.ZTags"/> returns null.
	/// </remarks>
	public void AddStyleTag(string name, UserDefinedStyle style) {
		if (_userStyles == null) _userStyles = new Dictionary<string, _TagStyle>();
		if (_userStyles.Count >= 100) throw new InvalidOperationException();
		if (!name.Starts('.')) throw new ArgumentException();
		_userStyles.Add(name, new _TagStyle(style));
	}
	Dictionary<string, _TagStyle> _userStyles;

	public Func<string, byte[]> CodeStylesProvider;

	internal void OnLButtonDownWhenNotFocused_(nint wParam, nint lParam, ref bool setFocus) {
		if (setFocus && _c.ZInitReadOnlyAlways && !keys.gui.isAlt) {
			int pos = _c.Call(SCI_CHARPOSITIONFROMPOINTCLOSE, Math2.LoShort(lParam), Math2.HiShort(lParam));
			//print.it(pos);
			if (pos >= 0 && _c.zStyleHotspot(_c.zGetStyleAt(pos))) setFocus = false;
		}
	}

	//FUTURE: add control-tags, like <clear> (clear output), <scroll> (ensure line visible), <mark x> (add some marker etc).
	//FUTURE: let our links be accessible objects.
}
