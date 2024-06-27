using Au.Controls;
using EStyle = CiStyling.EStyle;

static class RegexParser {
	public record struct RXSpan(int start, int end, EStyle token);
	
	static regexp s_rx1 = new(@"\G[A-Z_=\d]+\)");
	
	public static List<RXSpan> GetClassifiedSpans(RStr s, bool net = false) {
		var a = new List<RXSpan>();
		int i = 0;
		
		while (s.Eq(i, "(*") && s_rx1.Match(s, 0, out var r1, (i + 2)..)) i = r1.end;
		if (i > 0) _Add(0, i, EStyle.RxOption);
		
		try { _Sub(s, false); }
		catch (Exception ex) { Debug_.Print(ex); }
		
		void _Sub(RStr s, bool extended) {
			int inCharClass = 0, charClassStart = 0;
			while (i < s.Length) {
				int ii = i;
				var c = s[i++];
				if (c is '\\') {
					if (!_Read(s)) return;
					if (c.IsAsciiAlpha()) {
						if (c is 'Q' && inCharClass == 0) { //literal text
							_Add(ii, i, EStyle.RxMeta);
							if (s.Find(@"\E", i, out int j)) _Add(j, i = j + 2, EStyle.RxMeta); else i = s.Length;
						} else if (c is 'b' or 'B' or 'A' or 'Z' or 'z' or 'G' or 'R' or 'X' or 'K' && inCharClass == 0) { //anchors etc
							_Add(ii, i, EStyle.RxMeta);
						} else if (c is 'w' or 'W' or 'd' or 'D' or 's' or 'S' or 'h' or 'H' or 'v' or 'V' or 'C') { //char class
							_Add(ii, i, EStyle.RxChars);
						} else if (c is 'N') { //`\N` (any char except newline) or `\N{U+xxxx}` (Unicode character code)
							if (s.Eq(i, "{U+")) _FindAdd(s, '}', EStyle.RxEscape); else _Add(ii, i, EStyle.RxChars);
						} else if (c is 'p' or 'P') { //Unicode properties
							if (s.Eq(i, '{')) _FindAdd(s, '}', EStyle.RxChars);
						} else if (c is 'g' or 'k' && inCharClass == 0) { //backreference
							if (!_Read(s)) return;
							if (c is '{') _FindAdd(s, '}');
							else if (c is '<') _FindAdd(s, '>');
							else if (c is '\'') _FindAdd(s, '\'');
							else if (c is '-' or '+' or (>= '0' and <= '9')) _AddNumber(s);
						} else if (c is 'x') { //ASCII char code
							if (s.Eq(i, '{')) i = s.FindEnd('}', i); else while (i < ii + 4 && char.IsAsciiHexDigit(s.At_(i))) i++;
							_Add(ii, i, EStyle.RxEscape);
						} else if (c is 'u' && net) { //Unicode char code
							while (i < ii + 6 && char.IsAsciiHexDigit(s.At_(i))) i++;
							_Add(ii, i, EStyle.RxEscape);
						} else { //`\n` etc
							_Add(ii, i, EStyle.RxEscape);
						}
					} else if (c is >= '1' and <= '9') {
						_AddNumber(s);
					} else {
						_Add(ii, i, EStyle.RxEscape);
					}
				} else if (c is '[') {
					if (inCharClass > 0) {
						if (net) {
							if (!(i > charClassStart + 2 && s[i - 2] == '-')) continue; //`[ab-[cd]]`
						} else {
							if (s.Eq(i, ':') && s.Find(']', i, out int j) && s[j - 1] == ':') _Add(ii, i = ++j, EStyle.RxChars);
							continue;
						}
					}
					if (s.Eq(i, '^')) i++;
					_Add(ii, i, EStyle.RxChars);
					charClassStart = i;
					inCharClass++;
				} else if (inCharClass > 0) {
					if (c is ']') {
						_Add(ii, i, EStyle.RxChars);
						inCharClass--;
					} else if (c is '-' && i > charClassStart + 1 && !s.Eq(i, ']')) {
						_Add(ii, i, EStyle.RxChars);
					}
				} else if (c is '.') {
					_Add(ii, i, EStyle.RxChars);
				} else if (c is '^' or '$' or '|' or '+' or '*' or '?') {
					_Add(ii, i, EStyle.RxMeta);
				} else if (c is '{') {
					_Add(ii, i, EStyle.RxMeta);
					if (s.Find('}', i, out int j)) _Add(j, i = j + 1, EStyle.RxMeta);
				} else if (c is '(') {
					if (!_Read(s, EStyle.RxMeta)) return;
					if (c is '*') {
						if (!_Read(s, EStyle.RxMeta)) return;
						if (char.IsLower(c)) { //`(*atomic:` etc
							_FindAdd(s, ':');
							_Sub(s, extended);
						} else { //`(*FAIL)`, `(*MARK:name)` etc
							_FindAdd(s, ')');
						}
						continue;
					}
					if (c is '?') {
						if (!_Read(s, EStyle.RxOption)) return;
						if (c is ':' or '|' or '>' or '=' or '!' or '*') goto g1;
						if (c is '#') { //`(?comment)
							_FindAdd(s, ')', EStyle.RxComment);
						} else if (c is '<' && s.At_(i) is '=' or '!' or '*') { //`(<=` etc
							i++;
							goto g1;
						} else if (c is '<' or '\'') { //`(?<name>group)`, `(?'name'group)`
							_FindAdd(s, c is '\'' ? c : '>');
							_Sub(s, extended);
						} else if (c is 'P') { //`(?P<name>group)`, `(?P=name)`, `(?P>name)`
							if (!_Read(s, EStyle.RxMeta)) return;
							if (c is '<') {
								_FindAdd(s, '>');
								_Sub(s, extended);
							} else {
								_FindAdd(s, ')');
							}
						} else if (c is '(') { //`(?(condition)group)`
							if (s.Eq(i, '?') && (s.At_(i + 1) is '=' or '!' || (s.At_(i + 1) is '<' && s.At_(i + 2) is '=' or '!'))) {
								i += s.At_(i + 1) is '<' ? 3 : 2;
								_Add(ii, i, EStyle.RxMeta);
								_Sub(s, extended);
							} else {
								_FindAdd(s, ')');
							}
							_Sub(s, extended);
						} else if (c is 'R' or '&' || c.IsAsciiDigit() || (c is '-' or '+' && s.At_(i).IsAsciiDigit())) { //`(?R)`, `(?1)`, `(?-1)`, `(?&name)`
							_FindAdd(s, ')');
						} else if (c is 'C') { //callout
							if (!_Read(s, EStyle.RxMeta)) return;
							//if (c is '\'' or '`' or '"' or '^' or '%' or '#' or '$' or '{') {} //never mind
							_FindAdd(s, ')', EStyle.RxCallout);
						} else { //`(?options)
							for (bool minus = false; ;) {
								if (c is ')') break;
								if (c is '-') minus = true;
								else if (c is 'x') extended = !minus;
								else if (c is '^') extended = false;
								else if (c is ':') {
									_Add(ii, ii + 1, EStyle.RxMeta);
									_Add(ii + 1, i - 1, EStyle.RxOption);
									ii = i - 1;
									goto g1; //like `(?i:group)`
								}
								if (!_Read(s, EStyle.RxOption)) return;
							}
							_Add(ii, i, EStyle.RxOption);
						}
						continue;
					}
					i--;
					g1:
					_Add(ii, i, EStyle.RxMeta);
					_Sub(s, extended);
				} else if (c is ')') {
					_Add(ii, i, EStyle.RxMeta);
					return;
				} else if (c is '#' && extended) {
					_FindAdd(s, '\n', EStyle.RxComment);
				}
				
				bool _Read(RStr s, EStyle tFalse = 0) {
					if (i < s.Length) { c = s[i++]; return true; }
					if (tFalse != 0) _Add(ii, s.Length, tFalse);
					return false;
				}
				
				void _FindAdd(RStr s, char c, EStyle t = EStyle.RxMeta) {
					_Add(ii, i = s.FindEnd(c, i), t);
				}
				
				void _AddNumber(RStr s, EStyle t = EStyle.RxMeta) {
					while (s.At_(i).IsAsciiDigit()) i++;
					_Add(ii, i, t);
				}
			}
		}
		
		void _Add(int from, int to, EStyle t) { a.Add(new(from, to, t)); }
		
		return a;
	}
	
	static bool Find(this RStr t, char c, int from, out int foundAt) {
		foundAt = t.IndexOf(from, c);
		return foundAt >= 0;
	}
	
	static bool Find(this RStr t, string s, int from, out int foundAt) {
		foundAt = t.IndexOf(from, s);
		return foundAt >= 0;
	}
	
	static int FindEnd(this RStr t, char c, int from) {
		int i = t.IndexOf(from, c);
		return i < 0 ? t.Length : i + 1;
	}
	
	static byte[] _ToScintillaStylingBytes(RStr s, List<RXSpan> a) {
		var styles8 = new byte[Encoding.UTF8.GetByteCount(s)];
		_ToScintillaStylingBytes(s, a, styles8);
		return styles8;
	}
	
	static void _ToScintillaStylingBytes(RStr s, List<RXSpan> a, Span<byte> styles8) {
		var map8 = s.IsAscii() ? null : Convert2.Utf8EncodeAndGetOffsets_(s).offsets;
		styles8[..(map8?[^1] ?? s.Length)].Fill((byte)EStyle.RxText);
		foreach (var v in a) {
			//print.it(v, s[v.start..v.end].ToString());
			var (i, end) = (v.start, v.end);
			if (map8 != null) { i = map8[i]; end = map8[end]; }
			while (i < end) styles8[i++] = (byte)v.token;
		}
	}
	
	/// <summary>
	/// Parses a regular expression or wildcard expression and writes regular expression syntax highlighting styles to <i>styles8</i>.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="format"><b>Regexp</b>, <b>NetRegex</b> or <b>Wildex</b>.</param>
	/// <param name="styles8">Writes styles here. Must be of length greater or equal to <c>Encoding.UTF8.GetByteCount(s)</c>.</param>
	public static void GetScintillaStylingBytes(RStr s, PSFormat format, Span<byte> styles8) {
		Debug.Assert(styles8.Length >= Encoding.UTF8.GetByteCount(s));
		if (format is PSFormat.Wildex) {
			if (s is not ['*', '*', _, _, _, ..]) return;
			WXType type = 0;
			string split = null;
			for (int i = 2, j; i < s.Length; i++) {
				switch (s[i]) {
				case ' ':
					styles8 = styles8[(Encoding.UTF8.GetByteCount(s[..i]) + 1)..];
					s = s[(i + 1)..];
					goto g1;
				case 'r': type = WXType.RegexPcre; break;
				case 'R': type = WXType.RegexNet; break;
				case 'm': type = WXType.Multi; break;
				case '(':
					if (s[i - 1] != 'm') return;
					for (j = ++i; j < s.Length; j++) if (s[j] == ')') break;
					if (j == s.Length || j == i) return;
					split = s[i..j].ToString();
					i = j;
					break;
				case 'c' or 'n': break;
				default: return;
				}
			}
			g1:
			if (type is WXType.RegexPcre or WXType.RegexNet) {
				var a = GetClassifiedSpans(s, type is WXType.RegexNet);
				_ToScintillaStylingBytes(s, a, styles8);
			} else if (type is WXType.Multi) {
				foreach (var v in s.Split(split ?? "||")) {
					int i8 = Encoding.UTF8.GetByteCount(s[..v.start]);
					GetScintillaStylingBytes(s[v.Range], PSFormat.Wildex, styles8[i8..]);
				}
			}
		} else {
			var a = GetClassifiedSpans(s, format is PSFormat.NetRegex);
			_ToScintillaStylingBytes(s, a, styles8);
		}
	}
}
