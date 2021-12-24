#pragma once
#include "stdafx.h"

#define CMP2(s, c)	((s)[0]==c[0] && (s)[1]==c[1])
#define CMP3(s, c)	(CMP2(s, c) && (s)[2]==c[2])
#define CMP4(s, c)	(CMP3(s, c) && (s)[3]==c[3])
#define CMP5(s, c)	(CMP4(s, c) && (s)[4]==c[4])
#define CMP6(s, c)	(CMP5(s, c) && (s)[5]==c[5])

//Calls _wcstoi64 and casts to int.
//Use instead of wcstol to avoid its overflow problem. Eg for "0xFFFFFFFF" it returns INT_MAX; this func returns -1.
inline int strtoi(STR s, LPWSTR* end = nullptr, int radix = 0) {
	return (int)_wcstoi64(s, end, radix);
}

//Fast memory buffer.
//Depending on required size, uses memory in fixed-size memory in this variable (eg on stack) or allocates from heap.
//Does not call ctor/dtor.
template <class T, size_t nElemOnStack = 1000>
class Buffer
{
	static const size_t c_nStackBytes = nElemOnStack * sizeof(T);

	T* _p;
	BYTE _onStack[c_nStackBytes];
public:
	explicit Buffer() noexcept { _p = (T*)_onStack; } //later call Init, unless need <= elements than nElemOnStack

	__forceinline explicit Buffer(size_t nElem) noexcept { _Init(nElem); }

	~Buffer() { FreeHeapMemory(); }

	//Frees old memory and allocates new. Does not preserve.
	T* Alloc(size_t nElem) { FreeHeapMemory(); return _Init(nElem); }

	//Frees old memory, allocates new and calls memset(0). Does not preserve.
	T* AllocAndZero(size_t nElem) { FreeHeapMemory(); return _InitZ(nElem); }

	//Reallocates memory in the grow direction, preserving existing data.
	T* Realloc(size_t nElem) { if(nElem > nElemOnStack) _Realloc(nElem * sizeof(T)); return _p; }

	//Frees heap memory. Called by dtor.
	__declspec(noinline)
		void FreeHeapMemory()
	{
		if((LPBYTE)_p != _onStack) {
			free(_p);
			_p = (T*)_onStack;
		}
	}

	operator T*() { return _p; }

	size_t Capacity() { return (LPBYTE)_p == _onStack ? nElemOnStack : _msize(_p) * sizeof(T); }

private:
	__forceinline T* _Init(size_t nElem) { return (T*)_Init(nElem * sizeof(T), c_nStackBytes); }

	T* _InitZ(size_t nElem) { return (T*)memset(_Init(nElem), 0, nElem * sizeof(T)); }

	__declspec(noinline)
		void* _Init(size_t requiredSize, size_t stackSize)
	{
		_p = (T*)_onStack;
		if(requiredSize > stackSize) _p = (T*)malloc(requiredSize);
		return _p;
	}

	__declspec(noinline)
		void _Realloc(size_t requiredSize)
	{
		if((LPBYTE)_p != _onStack) {
			_p = (T*)realloc(_p, requiredSize);
		} else {
			_p = (T*)malloc(requiredSize);
			memcpy(_p, _onStack, c_nStackBytes);
		}
	}

	//tested: the __declspec(noinline) functions are added once for all T, making template instance code very small.
};

namespace str
{
//null-safe wcslen.
inline size_t Len(STR s) { return s ? wcslen(s) : 0; }

inline bool IsEmpty(STR s) { return s == null || *s == 0; }

//Formats string by appending strings and numbers to an internal buffer (Buffer<WCHAR, 1000>.
//Can be used instead of std::wstringstream which adds ~130 KB to the dll size. I don't trust CString etc too, although did not test.
class StringBuilder
{
	static const size_t c_bufferSize = 1000;
	size_t _len, _all;
	Buffer<WCHAR, c_bufferSize> _b;

	void _ReallocIfNeed(size_t lenAppend)
	{
		auto n = _len + lenAppend;
		if(n >= _all) {
			n += _all;
			_b.Realloc(n);
			_all = n;
		}
	}
public:
	StringBuilder()
	{
		_len = 0;
		_all = c_bufferSize;
		_b[0] = 0;
	}

	void Clear()
	{
		_b.FreeHeapMemory();
		_len = 0;
		_all = c_bufferSize;
		_b[0] = 0;
	}

	operator LPWSTR() {
		return _b;
	}

	int Length() {
		return (int)_len;
	}

	BSTR ToBSTR() {
		return SysAllocStringLen(_b, (UINT)_len);
	}

	void Append(STR s, size_t lenS)
	{
		if(lenS > 0) {
			_ReallocIfNeed(lenS);
			memcpy(_b + _len, s, lenS * 2);
			auto n = _len + lenS;
			_b[n] = 0;
			_len = n;
		}
	}

	void Append(STR s) { Append(s, Len(s)); }

	friend StringBuilder& operator<<(StringBuilder& b, STR s) {
		b.Append(s);
		return b;
	}

	void AppendBSTR(BSTR s) { Append(s, SysStringLen(s)); }
	//note: cannot add Append and << overloads for BSTR because then compiler chooses them for LPWSTR etc.

	void Append(__int64 i, int radix = 10)
	{
		_ReallocIfNeed(20);
		_i64tow(i, _b + _len, radix);
		auto n = _len; while(_b[n]) n++;
		_b[n] = 0;
		_len = n;
	}

	friend StringBuilder& operator<<(StringBuilder& b, __int64 i) {
		b.Append(i);
		return b;
	}

	friend StringBuilder& operator<<(StringBuilder& b, int i) {
		b.Append((__int64)i);
		return b;
	}

	void AppendChar(WCHAR c, int count = 1)
	{
		if(count > 0) {
			_ReallocIfNeed(count);
			LPWSTR t = _b + _len;
			while(--count >= 0) *t++ = c;
			*t = 0;
			_len = t - _b;
		}
	}

	friend StringBuilder& operator<<(StringBuilder& b, WCHAR c) {
		b.AppendChar(c);
		return b;
	}

	friend StringBuilder& operator<<(StringBuilder& b, char c) {
		b.AppendChar((WCHAR)c);
		return b;
	}

	//Gets buffer that can be passed to an API function that needs it.
	//The buffer is after the formatted string, so the API will append text, not replace.
	//After calling the API, call FixBuffer.
	//size - receives available size, which is >=minSize.
	//minSize - minimal buffer size you need. Default 500.
	LPWSTR GetBufferToAppend(out int& size, int minSize = 500)
	{
		_ReallocIfNeed(minSize + 1);
		size = (int)(_all - _len - 1);
		return _b + _len;
	}

	//Sets correct length after calling GetBufferToAppend and an API function that writes to the buffer.
	//If appendLen<0, calls wcslen.
	void FixBuffer(int appendLen = -1)
	{
		auto n = _len + (appendLen < 0 ? wcslen(_b + _len) : appendLen);
		assert(n < _all); if(n >= _all) n = _len;
		_b[n] = 0;
		_len = n;
	}
};

namespace pcre
{

BSTR GetErrorMessage(int code);
pcre2_code_16* Compile(STR rx, size_t len, __int64 flags = 0, out BSTR* errStr = null);
bool Match(pcre2_code_16* code, STR s, size_t len, size_t start = 0, UINT flags = 0);

//If code is not null, calls pcre2_code_free_16.
static void Free(pcre2_code_16* code) {
	if(code != null) pcre2_code_free_16(code);
}

};


//Wildcard expression.
//More info in the C# version and help file.
class Wildex
{
	Wildex(Wildex&& x) = delete; //disable copying
public:
	/// <summary>
	/// The type of text (wildcard expression) used when creating the Wildex variable.
	/// </summary>
	enum class WildType :byte
	{
		/// Simple text (option t, or no *? characters and no t r options).
		Text,

		/// Wildcard (has *? characters and no t r options).
		/// Match() calls str::Like.
		Wildcard,

		/// PCRE regular expression (option r).
		RegexPcre,

		/// Multiple parts (option m).
		/// Match() calls Match() for each part and returns true if all negative (option n) parts return true (or there are no such parts) and some positive (no option n) part returns true (or there are no such parts).
		Multi,
	};

private:
	union {
		LPWSTR _text;
		pcre2_code_16* _regex;
		Wildex* _multi_array;
	};
	union {
		int _text_length;
		int _multi_count;
	};
	WildType _type;
	bool _ignoreCase;
	bool _not;
	bool _freeText;

public:
	Wildex() { ZEROTHIS; }
	~Wildex();
	bool Parse(STR w, size_t lenW, bool dontCopyString = false, out BSTR* errStr = null);
	bool Match(STR s, size_t lenS) const;
	//Returns true if not null.
	bool Is() const { return _text != null; }

	static bool HasWildcards(STR s, size_t lenS);
};

EXPORT STR Cpp_LowercaseTable();
bool Like(STR s, size_t lenS, STR w, size_t lenW, bool ignoreCase = false);
bool Equals(STR w, size_t lenW, STR s, size_t lenS, bool ignoreCase = false);

int Switch(STR s, size_t lenS, std::initializer_list<STR> a);
int Switch(STR s, std::initializer_list<STR> a);


} //namespace str


class Bstr : public CComBSTR
{
public:
	using CComBSTR::CComBSTR; //inherit ctors

	//Calls Attach(SysAllocStringLen(s, len)) and returns m_str.
	BSTR Assign(STR s, int len)
	{
		Attach(SysAllocStringLen(s, len));
		return m_str;
	}

	bool Equals(STR s, int lenS, bool ignoreCase)
	{
		if(m_str == null) return s == null;
		return str::Equals(m_str, Length(), s, lenS);
	}

	bool Equals(STR s, bool ignoreCase)
	{
		if(m_str == null) return s == null;
		return str::Equals(m_str, Length(), s, str::Len(s));
	}

	bool Equals(BSTR s, bool ignoreCase)
	{
		if(m_str == null) return s == null;
		return str::Equals(m_str, Length(), s, SysStringLen(s));
	}
};

struct BstrNameValue
{
	Bstr name;
	Bstr value;
};
