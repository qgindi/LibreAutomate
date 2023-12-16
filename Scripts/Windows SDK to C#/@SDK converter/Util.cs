namespace SdkConverter;

unsafe partial class Converter {
	#region SYMBOL
	
	/// <summary>
	/// If x is _Typedef, replaces it with its deepest used type.
	/// Error if x is not a type (is _Keyword).
	/// Returns pointer level.
	/// </summary>
	/// <param name="iTok">Used only for error place.</param>
	int _Unalias(int iTok, ref _Symbol x) {
		int ptr = 0;
		var t = x as _Typedef;
		if (t != null) {
			x = t.aliasOf;
			ptr = t.ptr;
		} else if (x is _Keyword) _Err(iTok, "unexpected");
		return ptr;
	}
	
	/// <summary>
	/// This overload has isConst parameter. If x is const _Typedef sets the isConst variable = true, else isConst unchanged.
	/// </summary>
	int _Unalias(int iTok, ref _Symbol x, ref bool isConst) {
		int ptr = 0;
		var t = x as _Typedef;
		if (t != null) {
			x = t.aliasOf;
			ptr = t.ptr;
			if (t.isConst) isConst = true;
		} else if (x is _Keyword) _Err(iTok, "unexpected");
		return ptr;
	}
	
	/// <summary>
	/// Adds x to _ns[_nsCurrent].sym.
	/// Error if already exists, unless it is a forward decl of same type or a typedef of same type.
	/// </summary>
	/// <param name="iTokName">Token index of symbol name.</param>
	/// <param name="addToGlobal">Add to _ns[0].sym.</param>
	void _AddSymbol(int iTokName, _Symbol x, bool addToGlobal = false) {
		__AddSymbol(_tok[iTokName], x, iTokName, addToGlobal);
	}
	
	/// <summary>
	/// Adds x to _ns[_nsCurrent].sym.
	/// Error if already exists, unless it is a forward decl of same type or a typedef of same type.
	/// </summary>
	/// <param name="iTokError">Where to show error if need.</param>
	/// <param name="addToGlobal">Add to _ns[0].sym.</param>
	void _AddSymbol(string name, _Symbol x, int iTokError, bool addToGlobal = false) {
		//print.it(name);
		__AddSymbol(_TokenFromString(name), x, iTokError, addToGlobal);
	}
	
	/// <summary>
	/// Adds x to _ns[_nsCurrent].sym.
	/// Error if already exists, unless it is a forward decl of same type or a typedef of same type.
	/// </summary>
	/// <param name="iTokError">Where to show error if need.</param>
	/// <param name="addToGlobal">Add to _ns[0].sym.</param>
	void __AddSymbol(_Token name, _Symbol x, int iTokError, bool addToGlobal) {
		Debug.Assert(_IsCharIdentStart(*name.s));
		if (_keywords.ContainsKey(name)) _Err(iTokError, "name already exists (keyword)");
		int ns = addToGlobal ? 0 : _nsCurrent;
		//print.it(name);
		try {
			_ns[ns].sym.Add(name, x);
		}
		catch (ArgumentException) {
			_Symbol p = _ns[ns].sym[name];
			g1:
			if (x.GetType() != p.GetType()) {
				if (((p is _Typedef) && 0 == _Unalias(iTokError, ref p)) || ((x is _Typedef) && 0 == _Unalias(iTokError, ref x))) goto g1;
				_Err(iTokError, "name already exists");
			}
			if (!x.forwardDecl) {
				if (!(x is _Typedef) && !(x is _Callback)) _Err(iTokError, "already defined");
				//info: C++ allows multiple identical typedef. We don't check identity, it is quite difficult, assume the header file is without such errors.
				//info: before adding struct or enum, the caller checks whether it is forward-declaraed. If so, overwrites the forward-declared object and does not call this function, because cannot change object associated with that name because forward-declared object pointer can be used in a typedef.
			}
		}
	}
	
	/// <summary>
	/// Finds symbol of token iTok in _keywords, _ns[_nsCurrent].sym and optionally in _ns[nsCurrent-1 ... 0].sym.
	/// Error if not found or the token is not an identifier.
	/// </summary>
	_Symbol _FindSymbol(int iTok, bool includingAncestorNamespaces) {
		_Symbol x;
		if (!_TryFindSymbol(iTok, out x, includingAncestorNamespaces)) _Err(iTok, "unknown identifier");
		return x;
	}
	
	/// <summary>
	/// Finds symbol of token iTok in _keywords, _ns[_nsCurrent].sym and optionally in _ns[nsCurrent-1 ... 0].sym.
	/// Error if the token is not an identifier.
	/// </summary>
	bool _TryFindSymbol(int iTok, out _Symbol x, bool includingAncestorNamespaces) {
		_Token token = _tok[iTok];
		if (_keywords.TryGetValue(token, out x)) return true;
		for (int i = _nsCurrent; i >= 0; i--) {
			if (_ns[i].sym.TryGetValue(token, out x)) return true;
			if (!includingAncestorNamespaces) break;
		}
		if (!_TokIsIdent(iTok)) _Err(iTok, "unexpected");
		return false;
	}
	
#if DEBUG
	/// <summary>
	/// Finds symbol by string name.
	/// </summary>
	bool _TryFindSymbol(string name, out _Symbol x, bool includingAncestorNamespaces) {
		fixed (char* n = name) {
			_Token token = new _Token(n, name.Length);
			if (_keywords.TryGetValue(token, out x)) return true;
			for (int i = _nsCurrent; i >= 0; i--) {
				if (_ns[i].sym.TryGetValue(token, out x)) return true;
				if (!includingAncestorNamespaces) break;
			}
		}
		return false;
	}
#endif
	
	/// <summary>
	/// Finds symbol of T type of token iTok in _keywords, _ns[_nsCurrent].sym and optionally in _ns[nsCurrent-1 ... 0].sym.
	/// Error if the token is not an identifier.
	/// </summary>
	bool _TryFindSymbolAs<T>(int iTok, out T x, bool includingAncestorNamespaces) where T : _Symbol {
		x = null;
		_Symbol t;
		if (!_TryFindSymbol(iTok, out t, includingAncestorNamespaces)) return false;
		x = t as T;
		return x != null;
	}
	
	/// <summary>
	/// Finds symbol of token iTok everywhere.
	/// Error if not found or not a keyword or the token is not an identifier.
	/// </summary>
	/// <param name="kwType">If not _KeywordT.Any, error if the keyword is not of this type.</param>
	_Keyword _FindKeyword(int iTok, _KeywordT kwType = _KeywordT.Any) {
		_Symbol x = _FindSymbol(iTok, true);
		var k = x as _Keyword;
		if (k == null) _Err(iTok, "unexpected");
		if (kwType != _KeywordT.Any && k.kwType != kwType) _Err(iTok, "unexpected");
		return k;
	}
	
	/// <summary>
	/// Finds symbol of token iTok in _keywords, _ns[_nsCurrent].sym and optionally in _ns[nsCurrent-1 ... 0].sym.
	/// Error if not found or not a type (struct, enum etc) or the token is not an identifier.
	/// </summary>
	_Symbol _FindType(int iTok, bool includingAncestorNamespaces) {
		_Symbol x = _FindSymbol(iTok, includingAncestorNamespaces);
		if (x is _Keyword) _Err(iTok, "unexpected");
		return x;
	}
	
	/// <summary>
	/// Returns true if symbol of token iTok exists in _keywords, _ns[_nsCurrent].sym and optionally in _ns[nsCurrent-1 ... 0].sym.
	/// Error if the token is not an identifier.
	/// </summary>
	bool _SymbolExists(int iTok, bool includingAncestorNamespaces) {
		_Symbol t;
		return _TryFindSymbol(iTok, out t, includingAncestorNamespaces);
	}
	
	#endregion
	
	#region TOKEN
	
	/// <summary>
	/// Returns _tok[i].s.
	/// </summary>
	char* T(int i) { return _tok[i].s; }
	
	/// <summary>
	/// Returns true if token iTok string is equal to s.
	/// </summary>
	bool _TokIs(int iTok, string s) {
		return _tok[iTok].Equals(s);
	}
	
	/// <summary>
	/// Returns 1-based index of matching string at token iTok.
	/// </summary>
	int _TokIs(int iTok, params string[] a) {
		for (int i = 0; i < a.Length; i++) if (_tok[iTok].Equals(a[i])) return i + 1;
		return 0;
	}
	
	/// <summary>
	/// Returns true if token iTok string starts with s.
	/// </summary>
	bool _TokStarts(int iTok, string s) {
		return _tok[iTok].StartsWith(s);
	}
	
	/// <summary>
	/// Returns true if token iTok is an identifier.
	/// </summary>
	bool _TokIsIdent(int iTok) {
		return _IsCharIdentStart(*T(iTok));
	}
	
	/// <summary>
	/// Returns true if token iTok is a number.
	/// </summary>
	bool _TokIsNumber(int iTok) {
		return _IsCharDigit(*T(iTok));
	}
	
	/// <summary>
	/// Returns true if token iTok is an identifier or number.
	/// </summary>
	bool _TokIsIdentOrNumber(int iTok) {
		return _IsCharIdent(*T(iTok));
	}
	
	/// <summary>
	/// Returns true if token iTok is character c.
	/// </summary>
	bool _TokIsChar(int iTok, char c) {
		return *T(iTok) == c;
	}
	
	/// <summary>
	/// Returns true if token iTok is character c1 or c2.
	/// </summary>
	bool _TokIsChar(int iTok, char c1, char c2) {
		char c = *T(iTok);
		return c == c1 || c == c2;
	}
	
	/// <summary>
	/// Returns true if token iTok is a character in chars.
	/// </summary>
	bool _TokIsChar(int iTok, string chars) {
		char c = *T(iTok);
		for (int i = 0; i < chars.Length; i++) if (c == chars[i]) return true;
		return false;
	}
	
	/// <summary>
	/// Converts token iTok from char* to string.
	/// </summary>
	string _TokToString(int iTok) {
		return _tok[iTok].ToString();
	}
	
	/// <summary>
	/// Converts raw tokens iTokFrom to (not including) iTokTo from char* to string.
	/// </summary>
	string _TokToString(int iTokFrom, int iTokTo) {
		iTokTo--;
		char* s = T(iTokFrom), se = T(iTokTo) + _tok[iTokTo].len;
		return new string(s, 0, (int)(se - s));
	}
	
	/// <summary>
	/// Returns true, if at token iTokAfterTypeNameAndPtr is '([callConv]*[funcTypeOrVariable])('.
	/// </summary>
	bool _DetectIsFuncType(int iTokAfterTypeNameAndPtr) {
		int i = iTokAfterTypeNameAndPtr;
		if (!_TokIsChar(i, '(')) return false;
		if (_TokIsIdent(++i)) i++; //eg __stdcall
		if (!_TokIsChar(i, '*')) return false;
		if (_TokIsIdent(++i)) i++; //func type name or optional parameter/member name
		if (!_TokIsChar(i, ')')) return false;
		return _TokIsChar(i + 1, '(');
	}
	
	/// <summary>
	/// Skips code block enclosed in {}, (), [] or ˂˃.
	/// i must be token index at the starting { etc. Returns token index at } etc.
	/// Inside the block:
	///		Skips nested enclosed blocks of the same type.
	///		Ignores nested enclosed blocks of other types, eg (), [] etc blocks when this block is {}.
	/// Error if i character is not {([˂.
	/// Error if the ending })]˃ is missing.
	/// </summary>
	int _SkipEnclosed(int i) {
		char cStart = *T(i);
		if (!(cStart == '{' || cStart == '(' || cStart == '[' || cStart == '<')) _Err(i, "unexpected");
		
		char cEnd = cStart; cEnd++; if (cEnd != ')') cEnd++; //')' is '(' + 1, others + 2
		int level = 1;
		
		for (++i; ; i++) {
			char c = *T(i);
			if (c == cStart) {
				level++;
			} else if (c == cEnd) {
				level--;
				if (level == 0) return i;
			} else if (c == 0) {
				_Err(i, $"missing {cEnd}");
			}
		}
	}
	
	/// <summary>
	/// Skips code block enclosed in {}, (), [] or ˂˃.
	/// _i must be at the starting { etc. Finally it will be at } etc.
	/// Inside the block:
	///		Skips nested enclosed blocks of the same type.
	///		Ignores nested enclosed blocks of other types, eg (), [] etc blocks when this block is {}.
	/// Error if _i character is not {([˂.
	/// Error if the ending })]˃ is missing.
	/// </summary>
	void _SkipEnclosed() {
		_i = _SkipEnclosed(_i);
	}
	
	_Token _TokenFromString(string s) {
		char* p = (char*)Marshal.StringToHGlobalUni(s); //never mind: finally release
		return new _Token(p, s.Length);
	}
	
	#endregion
	
	#region STRING
	
	//Some of these currently are not used.
	
	/// <summary>
	/// Gets line length.
	/// Scans string until first '\r', '\n' or '\0'.
	/// </summary>
	static int _LenLine(char* s) {
		char* s0 = s;
		while (!_IsCharRN0(*s)) s++;
		return (int)(s - s0);
	}
	
	/// <summary>
	/// Gets identifier length.
	/// Returns 0 if s does not start with an identifier.
	/// </summary>
	static int _LenIdent(char* s) {
		if (!_IsCharIdentStart(*s)) return 0;
		char* s0 = s++;
		while (_IsCharIdent(*s)) s++;
		return (int)(s - s0);
	}
	
	/// <summary>
	/// Returns true if s starts with word and is not followed by a C++ identifier character.
	/// </summary>
	static bool _IsWord(char* s, string word) {
		int i = 0;
		for (; i < word.Length; i++) if (s[i] != word[i]) return false;
		return !_IsCharIdent(s[i]);
	}
	
	/// <summary>
	/// Returns true if s starts with prefix.
	/// </summary>
	static bool _IsPrefix(char* s, string prefix) {
		for (int i = 0; i < prefix.Length; i++) if (s[i] != prefix[i]) return false;
		return true;
	}
	
	/// <summary>
	/// Finds ident as whole word.
	/// Returns index or -1.
	/// </summary>
	int _FindIdentifierInString(string s, string ident) {
		int len = ident.Length;
		for (int i = 0; (i = s.Find(ident, i)) >= 0; i += len) {
			if (i > 0 && _IsCharIdent(s[i - 1])) continue;
			if (i < s.Length - len && _IsCharIdent(s[i + len])) continue;
			return i;
		}
		return -1;
	}
	
	#endregion
	
	#region CHARACTER
	/// <summary>
	/// Character type table for _IsCharIdent etc.
	/// </summary>
	static readonly byte[] _ctt = new byte[0x1000];
	
	static void _InitTables() {
		_ctt['_'] = 1; _ctt['$'] = 1;
		for (int i = 'a'; i <= 'z'; i++) _ctt[i] = 1;
		for (int i = 'A'; i <= 'Z'; i++) _ctt[i] = 1;
		for (int i = '0'; i <= '9'; i++) _ctt[i] = 2;
		_ctt[' '] = _ctt['\t'] = _ctt['\v'] = _ctt['\f'] = 4;
		_ctt['\r'] = _ctt['\n'] = 8;
		_ctt['!'] = _ctt['%'] = _ctt['&'] /*= _ctt['('] = _ctt[')']*/ = _ctt['*'] = _ctt['+'] /*= _ctt[',']*/ = _ctt['-'] = _ctt['.'] = _ctt['/']
			= _ctt[':'] = _ctt['<'] = _ctt['='] = _ctt['>'] = _ctt['?'] /*= _ctt['['] = _ctt[']']*/ = _ctt['^'] = _ctt['|'] = _ctt['~']
			/*= _ctt['{'] = _ctt['}']*/ = 16;
		//_ctt['('] = _ctt[')'] = _ctt['{'] = _ctt['}'] = _ctt['['] = _ctt[']'] = _ctt[';'] = _ctt['\''] = _ctt[':'] = _ctt['"'] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = _ctt[''] = 32;
		for (int i = 'a'; i <= 'f'; i++) _ctt[i] |= 64;
		for (int i = 'A'; i <= 'F'; i++) _ctt[i] |= 64;
		_ctt[0] = 128;
		//others 0
	}
	
	/// <summary>
	/// Returns true if c is a C++ identifier character: alphanumeric, '_', '$'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharIdent(char c) {
		return (_ctt[c] & 3) != 0;
	}
	
	/// <summary>
	/// Returns true if c is a C++ identifier start character: alpha, '_', '$'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharIdentStart(char c) {
		return (_ctt[c] & 1) != 0;
	}
	
	/// <summary>
	/// Returns true if c is a number digit '0' to '9'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharDigit(char c) {
		return (_ctt[c] & 2) != 0;
	}
	
	/// <summary>
	/// Returns true if c is a number digit '0' to '9'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharHexDigit(char c) {
		return (_ctt[c] & (2 | 64)) != 0;
	}
	
	/// <summary>
	/// Returns true if c is a C++ space character, including new line characters: ' ', '\t', '\r', '\n', '\v', '\f'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharSpaceOrRN(char c) {
		return (_ctt[c] & 12) != 0;
	}
	
	/// <summary>
	/// Returns true if c is a C++ space character, except new line characters: ' ', '\t', '\v', '\f'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharSpaceNoRN(char c) {
		return _ctt[c] == 4;
	}
	
	/// <summary>
	/// Returns true if c is a new line character: '\r', '\n'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharRN(char c) {
		return _ctt[c] == 8;
	}
	
	/// <summary>
	/// Returns true if c is a new line character or end of string: '\r', '\n', '\0'.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharRN0(char c) {
		return (_ctt[c] & (8 | 128)) != 0;
	}
	
	/// <summary>
	/// Returns true if c is a C++ operator character, not including ,[](){}.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static bool _IsCharOperator(char c) {
		return (_ctt[c] & 16) != 0;
	}
	
	#endregion
}

#region ERROR

unsafe partial class Converter {
	/// <summary>
	/// Gets offset of token iTok in _src.
	/// </summary>
	int _Pos(int iTok) { return iTok < _tok.Count ? (int)(T(iTok) - _s0) : 0; }
	
	/// <summary>
	/// Throws exception indicating error at token iTok position.
	/// </summary>
	void _Err(int iTok, string errorText, [CallerMemberName] string m_ = null, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0) {
		__ThrowConverterException(_Pos(iTok), errorText, m_, f_, l_);
	}
	
	/// <summary>
	/// Throws exception indicating error at s position (s must be in _src).
	/// </summary>
	void _Err(char* s, string errorText, [CallerMemberName] string m_ = null, [CallerFilePath] string f_ = null, [CallerLineNumber] int l_ = 0) {
		__ThrowConverterException((int)(s - _s0), errorText, m_, f_, l_);
	}
	
	void __ThrowConverterException(int pos, string errorText, string callerName, string callerFile, int callerLine) {
		throw new ConverterException(
			$"{errorText}\r\n\tin <open {callerFile}|{callerLine}>{callerName}<>"
			, pos);
	}
	
	void _OnCatchException(Exception e) {
		//print.it(e);
		int i = e is ConverterException ce ? ce.Offset : _Pos(_i);
		ScriptEditor.Open(_cppFile, offset: i);
	}
}

class ConverterException : Exception {
	public int Offset { get; private set; }
	
	public ConverterException(string s, int offset) : base(s) {
		Offset = offset;
#if TEST_SMALL
			//Offset += 3; //VS saves some files with BOM, and QM2 loads with that BOM, but we get file data without BOM
#endif
	}
}

#endregion

#region debug

unsafe partial class Converter {
	string _DebugGetLine(int iTok) {
		if (iTok == 0) return null;
		char* s = T(iTok), se = s;
		while (s[-1] != '\n') s--;
		while (*se != '\r' && *se != '\n') se++;
		return new string(s, 0, (int)(se - s));
	}
}

#endregion
