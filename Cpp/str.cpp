#include "stdafx.h"
#include "cpp.h"

namespace str
{

#pragma region Like, Equals, lowercase table

static WCHAR _caseTable[0x10000];
static bool _caseTableCreated;

//Returns static WCHAR table[0x10000] containing all Unicode characters in that range. Uppercase characters converted to lowercase.
EXPORT STR Cpp_LowercaseTable()
{
	if(!_caseTableCreated) {
		LPWSTR t = _caseTable;
		for(size_t i = 0; i < 0x10000; i++) t[i] = (WCHAR)i;
		CharLowerBuff(t, 0x10000);
		_caseTableCreated = true;
	} //speed: 350
	return _caseTable;
}

inline STR _LowercaseTable() {
	return _caseTableCreated ? _caseTable : Cpp_LowercaseTable();
}

//Compares string s of length lenS with wildcard pattern w of length lenW.
//Returns true if match. Returns false if s==null. Exception if w==null.
//EXPORT //C# has own function. Calling this from C# is significantly slower when string short.
bool Like(STR s, size_t lenS, STR w, size_t lenW, bool ignoreCase /*= false*/)
{
	if(s == null) return false;
	if(lenW == 0) return lenS == 0;
	if(lenW == 1 && w[0] == '*') return true;
	if(lenS == 0) return false;

	STR table = ignoreCase ? _LowercaseTable() : null;
	STR se = s + lenS, we = w + lenW;

	//find '*' from start. Makes faster in some cases.
	for(; (w < we && s < se); w++, s++) {
		size_t cS = s[0], cW = w[0];
		if(cW == '*') goto g1;
		if(cW == cS || cW == '?') continue;
		if((table == null) || (table[cW] != table[cS])) return false;
	}
	if(w == we) return s == se; //w ended?
	goto gr; //s ended
g1:

	//find '*' from end. Makes "*text" much faster.
	for(; (we > w && se > s); we--, se--) {
		size_t cS = se[-1], cW = we[-1];
		if(cW == '*') break;
		if(cW == cS || cW == '?') continue;
		if((table == null) || (table[cW] != table[cS])) return false;
	}

	//Algorithm by Alessandro Felice Cantatore, http://xoomer.virgilio.it/acantato/dev/wildcard/wildmatch.html
	//Changes: supports '\0' in string; case-sensitive or not; restructured, in many cases faster.
	{
		size_t i = 0;
		gStar: //info: goto used because C# compiler makes the loop faster when it contains less code
		w += i + 1;
		if(w == we) return true;
		s += i;

		for(i = 0; s + i < se; i++) {
			size_t sW = w[i];
			if(sW == '*') goto gStar;
			if(sW == s[i] || sW == '?') continue;
			if((table != null) && (table[sW] == table[s[i]])) continue;
			s++; i = -1;
		}

		w += i;
	}
gr:
	while(w < we && *w == '*') w++;
	return w == we;

	//info: Could implement escape sequence ** for * and maybe *? for ?.
	//	But it makes code slower etc.
	//	Not so important.
	//	Most users would not know about it.
	//	Usually can use ? for literal * and ?.
	//	Usually can use regular expression if need such precision.
	//	Then cannot use "**options " for wildcard expressions.
	//	Could use other escape sequences, eg [*], [?] and [[], but it makes slower and is more harmful than useful.

	//The first two loops are fast, but Equals much faster when !ignoreCase. We cannot use its optimizations.
	//The slowest case is "*substring*", because then the first two loops don't help.
	//	Then similar speed as string.IndexOf(ordinal) and API <msdn>FindStringOrdinal</msdn>.
	//	Possible optimization, but need to add much code, and makes not much faster, and makes other cases slower, difficult to avoid it.
}

//Compares string s of length lenS with string w of length lenW.
//Returns true if match. Returns false if s==null. Exception if w==null.
//EXPORT //C# has own function. Calling this from C# is significantly slower when string short.
bool Equals(STR w, size_t lenW, STR s, size_t lenS, bool ignoreCase /*= false*/)
{
	if(s == null || lenS != lenW) return false;
	if(lenW == 0) return true;

	if(!ignoreCase) return 0 == memcmp(s, w, lenW * 2); //fastest

	//optimization: at first compare case-sensitive, as much as possible.
	//	never mind: in 32-bit process this is not the fastest code (too few registers). But makes much faster anyway.
	//	tested: strings don't have to be aligned at 4 or 8.
	while(lenW >= 12) {
		if(*(__int64*)w != *(__int64*)s) break;
		if(*(__int64*)(w + 4) != *(__int64*)(s + 4)) break;
		if(*(__int64*)(w + 8) != *(__int64*)(s + 8)) break;
		w += 12; s += 12; lenW -= 12;
	}

	auto table = _LowercaseTable();
	for(size_t i = 0; i < lenW; i++) {
		size_t c1 = w[i], c2 = s[i];
		if(c1 != c2 && table[c1] != table[c2]) goto gFalse;
	}
	return true; gFalse: return false;
}

//this could be used by C#, but calling unmanaged functions from C# is slow when with 'fixed', which makes much slower with short strings.
//EXPORT bool Cpp_StringEqualsI(STR a, STR b, int len)
//{
//	if(len != 0) {
//		//optimization: at first compare case-sensitive, as much as possible.
//		//	never mind: in 32-bit process this is not the fastest code (too few registers). But makes much faster anyway.
//		//	tested: strings don't have to be aligned at 4 or 8.
//		while(len >= 12) {
//			if(*(__int64*)a != *(__int64*)b) break;
//			if(*(__int64*)(a + 4) != *(__int64*)(b + 4)) break;
//			if(*(__int64*)(a + 8) != *(__int64*)(b + 8)) break;
//			a += 12; b += 12; len -= 12;
//		}
//
//		auto table = _LowercaseTable();
//
//		for(size_t i = 0; i < len; i++) {
//			size_t c1 = a[i], c2 = b[i];
//			if(c1 != c2 && table[c1] != table[c2]) goto gFalse;
//		}
//	}
//	return true; gFalse: return false;
//}

#pragma endregion

//EXPORT void Cpp_Free(void* p) {
//	if(p) free(p);
//}


namespace pcre
{

BSTR _GetErrorMessage(int code, int compileErrorOffset)
{
	WCHAR b[300]; b[0] = 0;
	auto t = b;
	if(compileErrorOffset >= 0) {
		wcscat(t, L"Regular expression error at offset "); t += wcslen(t);
		_itow((int)compileErrorOffset, t, 10); t += wcslen(t);
		wcscat(t, L": "); t += 2;
	}
	pcre2_get_error_message_16(code, t, _countof(b) - (t - b));
	return SysAllocString(b);
}

//Calls pcre2_get_error_message_16 and returns SysAllocString.
BSTR GetErrorMessage(int code) {
	return _GetErrorMessage(code, -1);
}

//Calls pcre2_compile_16.
//This version is used in this dll, eg by Wildex.
//Adds PCRE2_UTF if rx contains non-ASCII characters and flags does not contain PCRE2_UTF or PCRE2_NEVER_UTF.
//More info in Cpp_RegexCompile.
pcre2_code_16* Compile(STR rx, size_t len, __int64 flags /*= 0*/, out BSTR* errStr /*= null*/)
{
	int errCode; size_t errOffset;
	UINT f = (UINT)flags, fe = flags >> 32;

	//If rx contains non-ASCII characters, add PCRE2_UTF flag. Else case-insensitive does not work.
	if(!(f & (PCRE2_UTF | PCRE2_NEVER_UTF))) {
		for(size_t i = 0; i < len; i++) if(rx[i] >= 128) { f |= PCRE2_UTF; break; }
	}
	fe &= 0xFF; //reserve other bits for the future, eg non-PCRE flags or JIT flags. Currently the EXTRA flag values are 1-8.

	pcre2_compile_context_16* cc = null;
	if(fe) {
		cc = pcre2_compile_context_create_16(null);
		pcre2_set_compile_extra_options_16(cc, fe);
	}
	auto re = pcre2_compile_16(rx, len, f, &errCode, &errOffset, cc);
	if(cc) pcre2_compile_context_free_16(cc);
	if(re == null) {
		if(errStr)* errStr = _GetErrorMessage(errCode, (int)errOffset);
		return null;
	}
	return re;
}

//Calls/returns pcre2_compile_16. Returns null if fails (errors in regular expression etc).
//This version is called from C#. In this dll use Compile instead.
//flags is __int64 consisting of pcre2_compile_16 flags in lo 32 bits and pcre2_set_compile_extra_options_16 flags in lo 8 bits of hi 32 bits.
//	Adds PCRE2_UTF if rx contains non-ASCII characters and flags does not contain PCRE2_UTF or PCRE2_NEVER_UTF.
//codeSize receives code size (PCRE2_INFO_SIZE).
//errStr, if not null, receives error text when fails. Caller then must SysFreeString it.
EXPORT pcre2_code_16* Cpp_RegexCompile(STR rx, size_t len, __int64 flags, out int& codeSize/*, out int& nGroups*/, out BSTR* errStr = null)
{
	auto code = pcre::Compile(rx, len, flags, errStr);
	if(code != null) {
		size_t z = 0;
		pcre2_pattern_info_16(code, PCRE2_INFO_SIZE, &z);
		codeSize = (int)z;
		//pcre2_pattern_info_16(code, PCRE2_INFO_CAPTURECOUNT, &nGroups); nGroups++;
	} else {
		codeSize = 0;
		//nGroups = 0;
	}
	return code;
}

//If code is not null, calls pcre2_code_free_16 and returns code memory size (PCRE2_INFO_SIZE). Else returs 0.
//This version is called from C#. In this dll use Free instead.
EXPORT int Cpp_RegexDtor(pcre2_code_16* code)
{
	if(code == null) return 0;
	size_t codeSize = 0;
	pcre2_pattern_info_16(code, PCRE2_INFO_SIZE, &codeSize);
	pcre2_code_free_16(code);
	return (int)codeSize;
}

struct _RxMdVec { CHeapPtr<POINT> a; int n; };

extern "C" int pcre2_match_data_create_au(const pcre2_code* code, pcre2_match_data* md);
//add this in pcre2_match_data.c, and make sure it is still correct after upgrading PCRE library
/*
//au: allows the caller to allocate pcre2_match_data in a faster way than the default malloc/free.
//	At first call pcre2_match_data_create_au with md=null. It returns the memory size needed.
//	Then allocate memory, call pcre2_match_data, pass the memory as md. It initializes the pcre2_match_data.
//	Note: do it in C++. In C# slower, tested.
static void* default_malloc(size_t size, void* data) { (void)data; return malloc(size); }
static void default_free(void* block, void* data) { (void)data; free(block); }
int pcre2_match_data_create_au(const pcre2_code *code, pcre2_match_data* md)
{
	//code from pcre2_match_data_create_from_pattern (below)
	int oveccount = ((pcre2_real_code *)code)->top_bracket + 1;
	int memSize = offsetof(pcre2_match_data, ovector) + 2 * oveccount * sizeof(PCRE2_SIZE);

	if(md != NULL) {
		//code from PRIV(memctl_malloc) in pcre2_context.h
		md->memctl.malloc = default_malloc;
		md->memctl.free = default_free;
		md->memctl.memory_data = NULL;

		//code from pcre2_match_data_create (below)
		md->oveccount = oveccount;
		md->flags = 0;
	}

	return memSize;
}
*/

//After upgrading PCRE library, this reminds to check/reapply its modifications. Then edit this line.
//More info in config.h in PCRE project.
static_assert(PCRE2_MAJOR == 10 && PCRE2_MINOR == 33);

//Cpp_RegexMatch results.
struct RegexMatch
{
	//[out] Array that receives x=from and y=to of match and submatches.
	//Func allocates thread-local memory for it. Then caller copies it ASAP and does not free.
	//tested: this is the fastest way when caller is C#.
	//Func sets it on full and partial match. Else sets null.
	POINT* vec;

	//[out] vec element count: pcre2_get_ovector_count_16 if matched, 0 if not, 1 if partial.
	int vecCount;

	//[out] pcre2_get_startchar_16.
	int indexNoK;

	//[out] pcre2_get_mark_16.
	STR mark;
};

#define STACK_MD 1

//Calls pcre2_match_16 and returns its return value. If PCRE2_ERROR_PARTIAL, returns 0.
//Allocates/frees match data in a fast way. Copies results to m, if not null.
//errStr, if not null, receives error text when fails, except when no match or partial match. Caller then must SysFreeString it.
//This version is called from C#. In this dll you can use Free; use this func when need match data (ovector etc).
EXPORT int Cpp_RegexMatch(pcre2_code_16* code, STR s, size_t len, size_t start = 0, UINT flags = 0,
	int(*callout)(pcre2_callout_block*, void*) = null, ref RegexMatch * m = null, bool needM = true, out BSTR * errStr = null)
{
	pcre2_match_data_16* md;
#ifdef STACK_MD
	char stack[1000]; //ovector[~55]
	int memSize = pcre2_match_data_create_au(code, null);
	if(memSize <= 1000) pcre2_match_data_create_au(code, md = (pcre2_match_data_16*)stack);
	else
#endif
		md = pcre2_match_data_create_from_pattern_16(code, null);

	int R = pcre2_match_16(code, s, len, start, flags, md, null, callout);

	assert(R != 0); //this could be if md contains too small ovector
	if(R == PCRE2_ERROR_PARTIAL) R = 0;

	if(needM) {
		//info: read PCRE API doc, section "HOW PCRE2_MATCH() RETURNS A STRING AND CAPTURED SUBSTRINGS"
		int n = R > 0 ? pcre2_get_ovector_count_16(md) : (R == 0 ? 1 : 0);
		//Printf(L"R=%i, n=%i", R, n);

		if(n == 0) {
			m->vec = null;
		} else {
			thread_local _RxMdVec t_vec; _RxMdVec& t = t_vec;
			if(t.n < n) {
				if(t.a) t.a.Free();
				if(t.a.Allocate(n)) t.n = (int)n;
				else { t.n = n = 0; R = PCRE2_ERROR_NOMEMORY; }
			}
			POINT* g = t.a;

			auto v = pcre2_get_ovector_pointer_16(md);
			for(int i = 0; i < n; i++) {
				POINT& p = g[i];
				p.x = (int)v[i * 2]; p.y = (int)v[i * 2 + 1];
			}
			m->vec = g;
		}
		m->vecCount = n;
		m->indexNoK = (int)pcre2_get_startchar_16(md);
		m->mark = pcre2_get_mark_16(md);
	}

	static_assert(PCRE2_ERROR_NOMATCH == -1 && PCRE2_ERROR_PARTIAL == -2);
	if(R < PCRE2_ERROR_PARTIAL && errStr != null) {
		*errStr = GetErrorMessage(R);
		//FUTURE: if UTF error, in error text include the offset. It seems pcre2_get_startchar_16 returns it.
	}

#ifdef STACK_MD
	if((void*)md != stack)
#endif
		pcre2_match_data_free_16(md);
	return R;
}

//Calls pcre2_match_16 and returns true if it returns >0.
//Allocates/frees match data in a fast way.
//This version is used in this dll, eg by Wildex.
bool Match(pcre2_code_16* code, STR s, size_t len, size_t start, UINT flags)
{
	pcre2_match_data_16* md;
#ifdef STACK_MD
	char stack[1000]; //ovector[~55]
	int memSize = pcre2_match_data_create_au(code, null);
	if(memSize <= 1000) pcre2_match_data_create_au(code, md = (pcre2_match_data_16*)stack);
	else
#endif
		md = pcre2_match_data_create_from_pattern_16(code, null);

	int R = pcre2_match_16(code, s, len, start, flags, md, null, null);

#ifdef STACK_MD
	if((void*)md != stack)
#endif
		pcre2_match_data_free_16(md);
	return R > 0 || R == PCRE2_ERROR_PARTIAL;
}

//rejected
////Calls pcre2_substitute_16 and returns its return value.
////errStr, if not null, receives error text when fails. Caller then must SysFreeString it.
//EXPORT int Cpp_RegexSubstitute(pcre2_code_16* code, STR s, size_t len, size_t start, UINT flags,
//	STR repl, size_t rlen, LPWSTR outputbuffer, size_t* outlen, out BSTR* errStr = null)
//{
//	size_t outlen0 = *outlen;
//	int r = pcre2_substitute_16(code, s, len, start, flags, null, null, repl, rlen, outputbuffer, outlen);
//	if(r < 0 && errStr != null && !(r == PCRE2_ERROR_NOMEMORY && *outlen > outlen0)) {
//		*errStr = GetErrorMessage(r);
//	}
//	return r;
//}
}

#pragma region Wildex

Wildex::~Wildex()
{
	if(_text != null) {
		switch(_type) {
		case WildType::RegexPcre: pcre::Free(_regex); break;
		case WildType::Multi: delete[] _multi_array; break;
		default: if(_freeText) free(_text);
		}
		_text = null;
	}
}

//Parses wildcard expression and initializes this variable.
//On error sets errStr (if not null) and returns false.
//dontCopyString - don't allocate/free own copy of w. The caller keeps w valid while using this variable.
//Call once (asserts).
bool Wildex::Parse(STR w, size_t lenW, bool dontCopyString/* = false*/, out BSTR* errStr/* = null*/)
{
	//if(w == null) return false;
	assert(_text == null); //_text = null; _not = _freeText = false;
	_type = WildType::Wildcard;
	_ignoreCase = true;
	STR es = L"Invalid \"**options \" in wildcard expression.";
	STR split = L"||"; size_t splitLen = 2;

	if(lenW >= 3 && w[0] == '*' && w[1] == '*') {
		for(size_t i = 2, j; i < lenW; i++) {
			switch(w[i]) {
			case 't': _type = WildType::Text; break;
			case 'r': _type = WildType::RegexPcre; break;
			case 'm': _type = WildType::Multi; break;
			case 'c': _ignoreCase = false; break;
			case 'n': _not = true; break;
			case ' ': w += ++i; lenW -= i; goto g1;
			case 'R': es = L"Option R in wildcard expression. Use r instead."; //.NET Regex
			case '(':
				if(w[i - 1] != 'm') goto ge;
				for(j = ++i; j < lenW; j++) if(w[j] == ')') break;
				if(j >= lenW || j == i) goto ge;
				split = w + i; splitLen = j - i;
				i = j;
				break;
			default: goto ge;
			}
		}
	ge:
		if(errStr)* errStr = SysAllocString(es);
		return false;
	g1:
		switch(_type) {
		case WildType::RegexPcre: {
			_regex = pcre::Compile(w, lenW, _ignoreCase ? PCRE2_CASELESS : 0, out errStr);
			if(_regex == null) return false;
		} goto gr;
		case WildType::Multi: {
			int count = 1; auto eos = w + lenW; auto splitChar = split[0];
			for(auto t = w; t <= eos - splitLen; ) { //calc part count
				if(t[0] == splitChar && 0 == wcsncmp(t, split, splitLen)) { count++; t += splitLen; } else t++;
			}
			_multi_array = new Wildex[_multi_count = count];

			for(int i = 0; i < count; i++, w += splitLen) {
				STR wi = w;
				if(i == count - 1) w = eos;
				else { //find next splitter
					for(; w <= eos - splitLen; w++) if(w[0] == splitChar && 0 == wcsncmp(w, split, splitLen)) break;
				}
				if(!_multi_array[i].Parse(wi, w - wi, true, out errStr)) return false;
			}
		} goto gr;
		}
	}

	if(_type == WildType::Wildcard && !HasWildcards(w, lenW)) _type = WildType::Text;
	if(dontCopyString) _text = (LPWSTR)w;
	else {
		_text = (LPWSTR)malloc((lenW + 1) * 2); memcpy(_text, w, lenW * 2); _text[lenW] = 0;
		_freeText = true;
	}
	_text_length = (int)lenW;
gr:
	return true;
}

bool Wildex::Match(STR s, size_t lenS) const
{
	if(s == null) return false;

	bool R = false;
	switch(_type) {
	case WildType::Wildcard:
		R = Like(s, lenS, _text, _text_length, _ignoreCase);
		break;
	case WildType::Text:
		R = Equals(s, lenS, _text, _text_length, _ignoreCase);
		break;
	case WildType::RegexPcre:
		R = pcre::Match(_regex, s, lenS);
		break;
	case WildType::Multi:
		//[n] parts: all must match (with their option n applied)
		int nNot = 0;
		for(int i = 0; i < _multi_count; i++) {
			Wildex& w = _multi_array[i];
			if(w._not) {
				if(!w.Match(s, lenS)) return _not; //!v->Match(s) means 'matches if without option n applied'
				nNot++;
			}
		}
		if(nNot == _multi_count) return !_not; //there are no parts without option n

		//non-[n] parts: at least one must match
		for(int i = 0; i < _multi_count; i++) {
			Wildex& w = _multi_array[i];
			if(!w._not && w.Match(s, lenS)) return !_not;
		}
		break;
	}
	return R ^ _not;
}

/// <summary>
/// Returns true if string contains wildcard characters: '*', '?'.
/// </summary>
/// <param name="s">Can be null.</param>
/*static*/ bool Wildex::HasWildcards(STR s, size_t lenS)
{
	if(s == null) return false;
	for(size_t i = 0; i < lenS; i++) {
		auto c = s[i];
		if(c == '*' || c == '?') goto yes;
	}
	return false; yes: return true;
}

//EXPORT bool Cpp_WildexParse(Wildex& x, STR w, size_t lenW, out BSTR* errStr) {
//	return x.Parse(w, lenW, false, errStr);
//}
//
//EXPORT bool Cpp_WildexMatch(Wildex& x, STR s, size_t lenS) {
//	return x.Match(s, lenS);
//}
//
//EXPORT void Cpp_WildexDtor(Wildex& x) {
//	x.~Wildex();
//}

#pragma endregion

#pragma region Switch

//Compares string s of length lenS with a list of '\0'-terminated non-null strings, and returns 1-based index of the matched string, or 0 if none matches.
//Example: int i=str::Switch(L"two", -1, { L"one", L"two", L"three" }); //2
//If s==null, returns 0.
int Switch(STR s, size_t lenS, std::initializer_list<STR> a)
{
	if(s == null) return 0;
	int i = 1;
	for(const STR* p = a.begin(); p < a.end(); i++) {
		STR t = *p++;
		for(size_t j = 0; j < lenS; j++) {
			WCHAR c = t[j];
			if(c != s[j] || c == 0) goto g1;
		}
		if(t[lenS] == 0) return i;
	g1:;
	}
	return 0;
}

//Compares '\0'-terminated string s with a list of '\0'-terminated non-null strings, and returns 1-based index of the matched string, or 0 if none matches.
//Example: int i=str::Switch(L"two", { L"one", L"two", L"three" }); //2
//If s==null, returns 0.
int Switch(STR s, std::initializer_list<STR> a)
{
	if(s == null) return 0;
	int i = 1;
	for(const STR* p = a.begin(); p < a.end(); i++) {
		STR t = *p++;
		for(STR ss = s; ; ) {
			WCHAR c = *t++;
			if(c != *ss++) goto g1;
			if(c == 0) break;
		}
		return i;
	g1:;
	}
	return 0;
}

#pragma endregion

} //namespace str
