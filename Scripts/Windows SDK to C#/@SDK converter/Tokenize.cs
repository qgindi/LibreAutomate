namespace SdkConverter;

unsafe partial class Converter {
	void _Tokenize(char* s) {
		_tok.Capacity = _src.Length / 8;
		_tok.Add(new _Token());
		
		bool isNewLine = true;
		int len;
		char c;
		
		for (; (c = *s) != 0; s++) {
			switch (c) {
			case '\r':
			case '\n':
				isNewLine = true;
				break;
			case ' ':
			case '\t':
			case '\v':
			case '\f':
				break;
			case '`': //was #define, #undef or #pragma pack
				if (isNewLine) { //else it is preprocessor operator
					if (_nTokUntilDefUndef == 0 && s[1] != '(') {
						_nTokUntilDefUndef = _tok.Count;
						s[-1] = '\0'; //was '\n
						_tok.Add(new _Token(s - 1, 1));
					}
				}
				_tok.Add(new _Token(s, 1));
				break;
			default:
				//TODO3: replace '$' with '_'. Although SDK does not have such identifiers when removed CRT headers.
				if (_IsCharIdentStart(c)) {
					len = _LenIdent(s);
					
					////is prefix"string" or prefix'char'?
					//bool isPrefix = false;
					//switch(s[len]) {
					//case '"':
					//	if(len == 1) isPrefix = (c == 'L' || c == 'u' || c == 'U');
					//	else if(len == 2) isPrefix = (c == 'u' && s[1] == '8');
					//	//info: raw strings replaced to escaped strings when preprocessing
					//	break;
					//case '\'':
					//	if(len == 1) isPrefix = (c == 'L' || c == 'u' || c == 'U');
					//	break;
					//}
					//if(isPrefix) { //remove prefix
					//	for(; len > 0; len--) *s++ = ' ';
					//	s--;
					//	continue;
					//}
					
				} else if (_IsCharDigit(c)) len = _LenNumber(s);
				else if (c == '"') len = _SkipString(s);
				else if (c == '\'') len = _SkipApos(s);
				else len = 1;
				_tok.Add(new _Token(s, len));
				s += len - 1;
				break;
			}
		}
		
		_nTok = _tok.Count;
		if (_nTokUntilDefUndef == 0) _nTokUntilDefUndef = _nTok;
		
		for (int i = 0; i < 10; i++) _tok.Add(new _Token(s, 1)); //to safely query next token
		_tok[0] = new _Token(s, 1); //to safely query previous token. Also used as an empty token. The empty list item added before tokenizing.
	}
	
	/// <summary>
	/// Parses and folds string constant, starting from s, which must be at the starting ".
	/// Returns folded string length, including both ".
	/// For example, if s is '"a" "b"', makes it '"ab"   ' and returns 4.
	/// Error if the ending " is missing.
	/// </summary>
	//#if NEWSTRING
	int _SkipString(char* s) {
		Debug.Assert(*s == '"');
		
		//is prefix?
		bool isPrefix = false;
		int iPrevTok = _tok.Count - 1;
		_Token pt = _tok[iPrevTok];
		char c = *pt.s;
		if (_IsCharIdentStart(c) && pt.len == (int)(s - pt.s)) {
			if (pt.len == 1) {
				isPrefix = (c == 'L' || c == 'u' || c == 'U');
			} else if (pt.len == 2) {
				isPrefix = (c == 'u' && pt.s[1] == '8');
			}
			if (isPrefix) _tok.RemoveAt(_tok.Count - 1);
			//info: raw strings replaced to escaped strings in script.
		}
		
		char* s0 = s++, d = s; //d used for folding
		g0:
		for (; ; s++) {
			*d++ = *s; //folding
			if (*s == '"') {
				if (s[-1] == '\\') {
					//is \" or \\"?
					int k = -1; while (s[k - 1] == '\\') k--;
					if ((k & 1) != 0) continue;
				}
				break;
			}
			if (*s <= '\r') {
				if (*s == 0 || *s == '\r' || *s == '\n') _Err(s0, "missing \"");
			}
		}
		
		//fold strings
		char* f = ++s; //skip "
		while (_IsCharSpaceOrRN(*f)) f++;
		if (isPrefix) {
			if (*f == *pt.s && (pt.len == 1 || f[1] == pt.s[1]) && f[pt.len] == '"') { s = f + pt.len + 1; d--; goto g0; }
		} else {
			if (*f == '"') { s = f + 1; d--; goto g0; }
		}
		
		for (f = d; f < s; f++) *f = ' '; //erase what is moved to the left
		
		//ANSI string constant?
		if (!isPrefix && _nTokUntilDefUndef != 0 && _TokIsChar(iPrevTok - 1, '`')) {
			//print.it(new string(s0, 0, (int)(d - s0)));
			//_Err(iPrevTok, "ANSI string");
			*s0 = '\x2'; //later will restore '"' and add to "FAILED TO CONVERT"
		}
		
		return (int)(d - s0);
		
		//info: could also give parsed length, but this is simpler. Caller will have to skip spaces in any case.
	}
	
	/// <summary>
	/// Parses character constant, starting from s, which must be at the starting '.
	/// Returns its length, including both '.
	/// Error if the ending ' is missing.
	/// </summary>
	int _SkipApos(char* s) {
		Debug.Assert(*s == '\'');
		
		//is prefix?
		_Token pt = _tok[_tok.Count - 1];
		char c = *pt.s;
		if (_IsCharIdentStart(c) && pt.len == (int)(s - pt.s) && pt.len == 1) {
			if (c == 'L' || c == 'u' || c == 'U') _tok.RemoveAt(_tok.Count - 1);
		}
		
		for (char* s0 = s++; ; s++) {
			if (*s == '\'') {
				if (s[-1] == '\\') {
					//is \' or \\'?
					int k = -1; while (s[k - 1] == '\\') k--;
					if ((k & 1) != 0) continue;
				}
				return (int)(s + 1 - s0);
			}
			if (*s <= '\r') {
				if (*s == 0 || *s == '\r' || *s == '\n') _Err(s0, "missing '");
			}
		}
	}
	
	/// <summary>
	/// Parses a number literal and returns its length, including suffix.
	/// Error if invalid.
	/// </summary>
	int _LenNumber(char* s) {
		if (!_IsCharDigit(*s)) return 0;
		char* s0 = s++;
		
		//hex, bin, oct
		int hexEtc = 0;
		if (*s0 == '0') {
			if (*s == 'x' || *s == 'X') {
				hexEtc = 2;
				for (++s; _IsCharHexDigit(*s); s++) { }
			} else if (*s == 'b' || *s == 'B') {
				hexEtc = 3;
				for (++s; *s == '0' || *s == '1'; s++) { }
			} else if (_IsCharDigit(*s)) hexEtc = 1; //or can be double like 01.2
			
			if (hexEtc >= 2 && (s - s0) == 2) _Err(s, "unexpected");
		}
		
		bool dbl = false;
		if (hexEtc < 2) {
			while (_IsCharDigit(*s)) s++;
			//double fraction
			if (*s == '.') {
				dbl = true;
				for (++s; _IsCharDigit(*s); s++) { }
			}
		}
		
		//double exponent
		if (*s == 'E' || *s == 'e') {
			dbl = true;
			s++;
			if (*s == '-' || *s == '+') s++;
			if (!_IsCharDigit(*s)) _Err(s, "unexpected");
			while (_IsCharDigit(*s)) s++;
		}
		
		if (hexEtc == 1 && !dbl) { //validate oct digits now
			for (char* t = s0 + 2; t < s; t++) if (*t > '7') _Err(t, "unexpected");
		}
		
		//suffix
		uint suffixType;
		s += _NumberSuffix(s, dbl, out suffixType);
		
		return (int)(s - s0);
	}
	
	/// <summary>
	/// Scans integer or floating-point constant suffix and returns its length.
	/// Error if invalid suffix.
	/// Converts C++ suffix to C#.
	/// </summary>
	/// <param name="s"></param>
	/// <param name="type">Receives flags: 1 unsigned, 2 __int64, 4 float.</param>
	int _NumberSuffix(char* s, bool floatingPoint, out uint type) {
		type = 0;
		char* s0 = s;
		int lenTrim = 0;
		
		if (floatingPoint) {
			if (*s == 'f' || *s == 'F') {
				s++; type |= 4;
			} else if (*s == 'l' || *s == 'L') *s++ = ' ';
		} else {
			if (*s == 'u' || *s == 'U') { s++; type |= 1; }
			
			if (*s == 'l' || *s == 'L') {
				s++; lenTrim = 1;
				if (*s == 'l' || *s == 'L') {
					*s++ = ' ';
					type |= 2;
				} else s[-1] = ' ';
			} else if (*s == 'i' || *s == 'I') {
				if (s[1] == '8') {
					s[0] = s[1] = ' ';
					s += 2; lenTrim = 2;
				} else {
					if (s[1] == '6' && s[2] == '4') {
						type |= 2;
						s[0] = 'L'; lenTrim = 2;
					} else if ((s[1] == '3' && s[2] == '2') || (s[1] == '1' && s[2] == '6')) {
						s[0] = ' '; lenTrim = 3;
					} else _Err(s, "unexpected");
					
					s[1] = s[2] = ' ';
					s += 3;
				}
			}
		}
		
		if (_IsCharIdent(*s)) _Err(s, "unexpected");
		return (int)(s - lenTrim - s0);
	}
}
