using EStyle = CiStyling.EStyle;

static class RegexParser {
	public record struct RXSpan(int start, int end, EStyle token);
	
	static regexp s_rx1 = new(@"\G[A-Z_]+(?:=\d+)?\)");
	
	public static List<RXSpan> GetClassifiedSpans(RStr s, bool net = false) {
		var a = new List<RXSpan>();
		int i = 0, ii = 0;
		bool altExtClasses = false;
		
		while (s.Eq(i, "(*") && s_rx1.Match(s, 0, out var r1, (i + 2)..)) {
			if (r1.Length == 3 && s.Eq(i + 2, "EC")) altExtClasses = true; //au mod: (*EC) sets flag PCRE2_ALT_EXTENDED_CLASS. Without it cannot correctly format or validate the regex; also users could not use this feature in the Find tool etc.
			i = r1.end;
		}
		if (i > 0) _Add(0, i, EStyle.RxOption);
		
		try { _Sub(s, false); }
		catch (Exception ex) { Debug_.Print(ex); }
		
		return a;
		
		void _Sub(RStr s, bool extended) {
			while (i < s.Length) {
				ii = i;
				char c = s[i++];
				if (c is '\\') {
					_Backslash(s, false);
				} else if (c is '[') {
					_CharClass(s, altExtClasses);
				} else if (c is '.') {
					_Add(ii, i, EStyle.RxChars);
				} else if (c is '^' or '$' or '|' or '+' or '*' or '?') {
					_Add(ii, i, EStyle.RxMeta);
				} else if (c is '{') {
					while (i < s.Length && s[i] is (>= '0' and <= '9') or ',' or ' ') i++;
					if (s.Eq(i, '}')) i++;
					_Add(ii, i, EStyle.RxMeta);
				} else if (c is '(') {
					if (!_Read(s, out c, EStyle.RxMeta)) return;
					if (c is '*') {
						if (!_Read(s, out c, EStyle.RxMeta)) return;
						if (char.IsLower(c)) { //`(*atomic:` etc
							_FindAdd(s, ':');
							if (i < s.Length) {
								if (s[(ii + 2)..(i - 1)] is "scan_substring" or "scs" && s[i] is '(') _FindAdd(s, ')');
								_Sub(s, extended);
							}
						} else { //`(*FAIL)`, `(*MARK:name)`, `(*:markName)` etc
							_FindAdd(s, ')');
						}
						continue;
					}
					if (c is '?') {
						if (!_Read(s, out c, EStyle.RxOption)) return;
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
							if (!_Read(s, out c, EStyle.RxMeta)) return;
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
						} else if (c is 'C' && !net) { //callout
							//if (!s.Eq(i, ')')) { if (!_Read(s, EStyle.RxMeta)) return; if (c is '\'' or '`' or '"' or '^' or '%' or '#' or '$' or '{') {} } //never mind
							_FindAdd(s, ')', EStyle.RxCallout);
						} else if (c is '[' && !net) { //extended character class
							_ExtendedCharClass(s);
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
								if (!_Read(s, out c, EStyle.RxOption)) return;
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
			}
		}
		
		void _Backslash(RStr s, bool inCharClass) {
			if (!_Read(s, out char c)) return;
			switch (c) {
			case >= '1' and <= '9' when !inCharClass:
				_AddNumber(s);
				break;
			case 'Q' when !inCharClass: //literal text
				_Add(ii, i, EStyle.RxMeta);
				if (_Find(s, @"\E", i, out int j)) _Add(j, i = j + 2, EStyle.RxMeta); else i = s.Length;
				break;
			case 'b' or 'B' or 'A' or 'Z' or 'z' or 'G' or 'R' or 'X' or 'K' when !inCharClass:
				_Add(ii, i, EStyle.RxMeta);
				break;
			case 'g' or 'k' when !inCharClass:
				if (!_Read(s, out c)) return;
				if (c is '{') _FindAdd(s, '}');
				else if (c is '<') _FindAdd(s, '>');
				else if (c is '\'') _FindAdd(s, '\'');
				else if (c is '-' or '+' or (>= '0' and <= '9')) _AddNumber(s);
				break;
			case 'N' when s.Eq(i, "{U+"): //`\N{U+xxxx}` (Unicode char code)
				_FindAdd(s, '}', EStyle.RxEscape);
				break;
			case 'w' or 'W' or 'd' or 'D' or 's' or 'S' or 'h' or 'H' or 'v' or 'V' or 'N' or 'C':
				_Add(ii, i, EStyle.RxChars);
				break;
			case 'p' or 'P': //Unicode properties
				if (s.Eq(i, '{')) _FindAdd(s, '}', EStyle.RxChars); else if (char.IsAsciiLetter(s.At_(i))) _Add(ii, ++i, EStyle.RxChars);
				break;
			case 'x': //ASCII char code
				if (s.Eq(i, '{')) i = _FindEnd(s, '}', i); else while (i < ii + 4 && char.IsAsciiHexDigit(s.At_(i))) i++;
				_Add(ii, i, EStyle.RxEscape);
				break;
			case 'u' when net:
				while (i < ii + 6 && char.IsAsciiHexDigit(s.At_(i))) i++;
				_Add(ii, i, EStyle.RxEscape);
				break;
			default:  //`\n` etc
				_Add(ii, i, EStyle.RxEscape);
				break;
			}
		}
		
		void _CharClass(RStr s, bool altExtended) {
			if (s.Eq(i, '^')) i++;
			_Add(ii, i, EStyle.RxChars);
			int ccStart = i;
			while (i < s.Length) {
				ii = i;
				char c = s[i++];
				if (c == '\\') {
					_Backslash(s, true);
				} else if (c == '[') {
					if (net) {
						if (i > ccStart + 2 && s[i - 2] == '-') _CharClass(s, false); //`[ab-[cd]]`
					} else if (s.Eq(i, ':')) { //`[...[:posix:]...]`
						_PosixCharClass(s);
					} else if (altExtended) { //nested alt-extended class
						_CharClass(s, true);
					}
				} else if (c is ']') {
					_Add(ii, i, EStyle.RxChars);
					return;
				} else if (altExtended) {
					if (c is '-' or '|' or '&' or '~' && s.Eq(i, c)) _Add(ii, ++i, EStyle.RxChars);
				} else {
					if (c is '-' && i > ccStart + 1 && !s.Eq(i, ']')) _Add(ii, i, EStyle.RxChars);
				}
			}
		}
		
		void _ExtendedCharClass(RStr s) {
			_Add(ii, i, EStyle.RxChars);
			while (i < s.Length) {
				ii = i;
				char c = s[i++];
				if (c == '\\') {
					_Backslash(s, true);
				} else if (c == '[') {
					if (s.Eq(i, ':')) { //`[...[:posix:]...]`
						_PosixCharClass(s);
					} else {
						_CharClass(s, false);
					}
				} else if (c is ']') {
					if (s.Eq(i, ')')) i++;
					_Add(ii, i, EStyle.RxChars);
					return;
				} else if (c is '-' or '+' or '&' or '|' or '^' or '!' or '(' or ')') {
					_Add(ii, i, EStyle.RxChars);
				}
			}
		}
		
		void _PosixCharClass(RStr s) {
			while (++i < s.Length && s[i] is >= 'a' and <= 'z') ;
			if (s.Eq(i, ':')) i++;
			if (s.Eq(i, ']')) i++;
			_Add(ii, i, EStyle.RxChars);
		}
		
		bool _Read(RStr s, out char c, EStyle tFalse = 0) {
			if (i < s.Length) { c = s[i++]; return true; }
			if (tFalse != 0) _Add(ii, s.Length, tFalse);
			c = default;
			return false;
		}
		
		void _FindAdd(RStr s, char c, EStyle t = EStyle.RxMeta) {
			_Add(ii, i = _FindEnd(s, c, i), t);
		}
		
		void _AddNumber(RStr s, EStyle t = EStyle.RxMeta) {
			while (s.At_(i).IsAsciiDigit()) i++;
			_Add(ii, i, t);
		}
		
		void _Add(int from, int to, EStyle t) {
			if (a.Count > 0 && a[^1].token == t && a[^1].end == from) a[^1] = new(a[^1].start, to, t);
			else a.Add(new(from, to, t));
		}
	}
	
	static bool _Find(RStr t, char c, int from, out int foundAt) {
		foundAt = t.IndexOf(from, c);
		return foundAt >= 0;
	}
	
	static bool _Find(RStr t, string s, int from, out int foundAt) {
		foundAt = t.IndexOf(from, s);
		return foundAt >= 0;
	}
	
	static int _FindEnd(RStr t, char c, int from) {
		int i = t.IndexOf(from, c);
		return i < 0 ? t.Length : i + 1;
	}
	
	/// <summary>
	/// Parses a regular expression or wildcard expression and writes regular expression syntax highlighting styles to <i>styles</i> (not UTF-8).
	/// </summary>
	/// <param name="s"></param>
	/// <param name="format"><b>Regexp</b>, <b>NetRegex</b> or <b>Wildex</b>.</param>
	/// <param name="styles">Writes styles here. Must be of length greater or equal to <c>s.Length</c>.</param>
	public static void GetScintillaStylingBytes16(RStr s, PSFormat format, Span<byte> styles) {
		Debug.Assert(styles.Length >= s.Length);
		if (format is PSFormat.Wildex) {
			if (s is not ['*', '*', _, _, _, ..]) return;
			WXType type = 0;
			string split = null;
			for (int i = 2, j; i < s.Length; i++) {
				switch (s[i]) {
				case ' ':
					styles = styles[(i + 1)..];
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
				_ToScintillaStylingBytes16(s, a, styles);
			} else if (type is WXType.Multi) {
				foreach (var v in s.SplitSE(split ?? "||")) {
					GetScintillaStylingBytes16(s[v.Range], PSFormat.Wildex, styles[v.start..]);
				}
			}
		} else {
			var a = GetClassifiedSpans(s, format is PSFormat.NetRegex);
			_ToScintillaStylingBytes16(s, a, styles);
		}
	}
	
	static void _ToScintillaStylingBytes16(RStr s, List<RXSpan> a, Span<byte> styles) {
		styles[..s.Length].Fill((byte)EStyle.RxText);
		foreach (var v in a) {
			//print.it(v, s[v.start..v.end].ToString());
			var (i, end) = (v.start, v.end);
			while (i < end) styles[i++] = (byte)v.token;
		}
	}
}
