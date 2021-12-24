// UnmanagedDll.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "cpp.h"

#if _DEBUG
//#if 1


__interface __declspec(uuid("3AB5235E-2768-47A2-909A-B5852A9D1868"))
	IInterface : IUnknown
{
	int __stdcall Add(int a, int b);
};

class Inter :public IInterface
{
public:
	int __stdcall Add(int a, int b) {
		return a + b;
	}

	// Inherited via IInterface
	virtual HRESULT __stdcall QueryInterface(REFIID riid, void** ppvObject) override
	{
		if(riid == __uuidof(IInterface) || riid == __uuidof(IUnknown)) {
			*ppvObject = this;
			return 0;
		}
		*ppvObject = nullptr;
		return E_NOINTERFACE;
	}
	virtual ULONG __stdcall AddRef(void) override
	{
		return 1;
	}
	virtual ULONG __stdcall Release(void) override
	{
		return 1;
	}
};

EXPORT IInterface* Cpp_GetInterface() { return new Inter(); }






LRESULT CALLBACK KeyHookProc(int code, WPARAM wParam, LPARAM lParam)
{
	if(code < 0) goto g1;
	if(wParam == 0x8F) {
		//Print(L"<hook>");
		HANDLE ev = OpenEventW(EVENT_MODIFY_STATE, false, L"ee57812345hh");
		if(ev == 0) {
			PRINTS(L"key sync hook: OpenEventW failed");
			goto g1;
		}
		SetEvent(ev);
		CloseHandle(ev);
	}
g1:
	return CallNextHookEx(0, code, wParam, lParam);
}

EXPORT HHOOK Cpp_InputSync(int action, int tid, HHOOK hh)
{
	if(action == 1) {
		auto hh = SetWindowsHookExW(WH_KEYBOARD, KeyHookProc, GetCurrentModuleHandle(), tid);
		if(hh == 0 && tid != 0) hh = SetWindowsHookExW(WH_KEYBOARD, KeyHookProc, GetCurrentModuleHandle(), 0); //console. GetWindowThreadProcessId lies. To get real id probably need to enumerate threads and call EnumThreadWindows for each. Too slow.
		return hh;
	} else if(action == 2) {
		UnhookWindowsHookEx(hh);
	}
	return 0;
}
//
//LRESULT CALLBACK ForegroundIdleProc(int code, WPARAM wParam, LPARAM lParam)
//{
//	//Print(code);
//	if(code < 0) goto g1;
//	HANDLE ev = OpenEventW(EVENT_MODIFY_STATE, false, L"ee57812345hh");
//	if(ev == 0) {
//		Print(L"failed");
//		goto g1;
//	}
//	SetEvent(ev);
//	CloseHandle(ev);
//
//	g1:
//	return CallNextHookEx(0, code, wParam, lParam);
//}
//
//EXPORT HHOOK Cpp_InputSync(int action, int tid, HHOOK hh)
//{
//	if(action == 1) {
//		return SetWindowsHookExW(WH_FOREGROUNDIDLE, ForegroundIdleProc, GetCurrentModuleHandle(), 0);
//	} else if(action == 2) {
//		UnhookWindowsHookEx(hh);
//	}
//	return 0;
//}

//#define IID_IThreadExitEvent __uuidof(IThreadExitEvent)
//__interface  __declspec(uuid("57017F56-E7CA-4A7B-A8F8-2AE36077F50D"))
//	IThreadExitEvent :IUnknown
//{
//	STDMETHODIMP Unsubscribe();
//};
//
//class ThreadExitEvent :public IThreadExitEvent
//{
//	long _cRef;
//	FARPROC _callback;
//	STD_IUNKNOWN_METHODS(IThreadExitEvent)
//public:
//	ThreadExitEvent(FARPROC callback) {
//		_cRef = 1;
//		_callback = callback;
//	}
//
//	~ThreadExitEvent() {
//		if(_callback == null) return;
//		Print(GetCurrentThreadId());
//		_callback();
//	}
//
//	STDMETHODIMP Unsubscribe() {
//		_callback = null;
//		return 0;
//	}
//};
//
//EXPORT
//IThreadExitEvent* Cpp_ThreadExitEvent(FARPROC callback)
//{
//	return new ThreadExitEvent(callback);
//}
//
//FARPROC s_callback;
//
//EXPORT
//void Cpp_ThreadExitEvent2(FARPROC callback)
//{
//	s_callback = callback;
//}
//
//void OnThreadExit()
//{
//	Print(GetCurrentThreadId());
//	if(s_callback) s_callback();
//}


//#include <atlstr.h>
//#include <atlfile.h>

extern HMODULE s_moduleHandle;

void TestStringBuilder()
{
	Perf.First();
	str::StringBuilder b;
	//b.Append(L"one ");
	//b.Append(50);
	//b.Append(L" four", 2);
	//b.Append(L" 0x");
	//b.Append(10, 16);
	//b.AppendChar(' ');
	//b.AppendChar('A');
	//b.AppendChar(' ', 4);
	//b.AppendChar('A');

	HWND ww = 0;

	static const STR rundll = L"\\SysWOW64\\rundll32.exe \"";
	static const STR bits = L"32";

	b << L"kkk" << L' ' << 5 << ',';
	b.AppendChar(' ');
	b.AppendChar(L' ');

	LPWSTR t; int n;
	t = b.GetBufferToAppend(out n); b.FixBuffer(GetWindowsDirectoryW(t, n));
	b << rundll;
	t = b.GetBufferToAppend(out n); b.FixBuffer(GetModuleFileNameW(s_moduleHandle, t, n));
	auto u = wcsrchr(t, '\\') - 5; u[0] = bits[0]; u[1] = bits[1]; //"32" to "64" or vice versa
	if(GetFileAttributes(t) == INVALID_FILE_ATTRIBUTES) return; //avoid messagebox when our antimatter dll does not exist
	b << L"\",Cpp_RunDll " << (__int64)ww; //note: without W

	b << 'A';
	b.AppendChar('.', 4);

	Perf.Next();
	Print(b);
	Perf.NW();
}

EXPORT void Cpp_TestWildex(STR s, STR w)
{
	auto lenS = wcslen(s);
	auto lenW = wcslen(w);

	Perf.First();
	str::Wildex x;
	Bstr es;
	if(!x.Parse(w, lenW, false, &es)) { Print(es); return; }
	Perf.Next();

	bool yes;
	for(int i = 0; i < 1000; i++) yes = x.Match(s, lenS);
	Perf.NW();

	Print(yes);
}

EXPORT void Cpp_TestPCRE(STR s, STR p, DWORD flags)
{
	int rc = 0;

	Perf.First();
	int errNum; size_t errOffs;
	auto re = pcre2_compile(p, -1, flags | PCRE2_UTF, &errNum, &errOffs, null);
	if(re == null) {
		PCRE2_UCHAR buffer[256];
		pcre2_get_error_message(errNum, buffer, _countof(buffer));
		Printf(L"error %s at %i", buffer, errOffs);
		return;
	}

#if false
	Perf.Next();
	LPBYTE ser = null; size_t serSize = 0;
	pcre2_code* a[2] = { re,re };
	rc = pcre2_serialize_encode((const pcre2_code_16 **)a, 2, &ser, &serSize, null);
	if(rc <= 0) {
		Print(L"pcre2_serialize_encode error");
		return;
	}
	pcre2_code_free(re); re = null;
	Perf.Next();

	Print(serSize);

	rc = pcre2_serialize_decode(&re, 1, ser, null);
	if(rc <= 0) {
		Print(L"pcre2_serialize_decode error");
		return;
	}

	pcre2_serialize_free(ser);
#endif

	Perf.Next();
	auto m = pcre2_match_data_create_from_pattern(re, null);
	auto len = wcslen(s);

	for(int i = 0; i < 1000; i++) {
		rc = pcre2_match(re, s, len, 0, 0, m, null, null);
		if(rc <= 0) {
			Print(rc);
			return;
		}
	}

	Perf.Next();

	//auto v = pcre2_get_ovector_pointer(m);
	//for(int i = 0; i < rc; i++) {
	//	PCRE2_SPTR substring_start = s + v[2 * i];
	//	size_t substring_length = v[2 * i + 1] - v[2 * i];
	//	Printf(L"%i: %.*s", i, (int)substring_length, (char *)substring_start);
	//}

	pcre2_match_data_free(m);
	pcre2_code_free(re);
	Perf.NW();
}


//class TestTL {
//public:
//	TestTL() {
//		Printf(L"+ %p %i", this, GetCurrentThreadId());
//	}
//
//	~TestTL() {
//		Printf(L"- %p %i", this, GetCurrentThreadId());
//	}
//};
//
//void Voo(LPWSTR s) {
//	//std::wstring k(s);
//	//Print(k.c_str() == null);
//	CString g(s);
//	//Print(g.GetString() == null);
//	//Print((STR)g == null);
//}
//
//enum
//	//class
//	eTest {
//	One = 1,
//	Two = 2,
//	Four = 4,
//};
//ENABLE_BITMASK_OPERATORS(eTest);
//
//#include <vector>
//#include <array>
//
//STR s_testSTR = L"te";
//
//struct eKKK {
//	enum {
//		One = 1,
//		Two = 2,
//		Four = 4,
//	};
//
//	//static const int One = 1;
//	//static const int Two = 2;
//	//static const int Four = 4;
//};
//
//enum class eKKK {
//	One = 1,
//	Two = 2,
//	Four = 4,
//	Eight = 8,
//};
//ENABLE_BITMASK_OPERATORS(eKKK);

EXPORT
int Cpp_TestInt(int a)
{
	return sizeof(VARIANT);
}

EXPORT
int Cpp_TestString(STR a)
{
	return 1;
}


#define IID_ICppTest __uuidof(ICppTest)
__interface  __declspec(uuid("3426CF3C-F7C2-4322-A292-463DB8729B54"))
	ICppTest :IUnknown
{
	STDMETHODIMP TestInt(int a, int b, int c);
	STDMETHODIMP TestString(STR a, int b, int c);
	STDMETHODIMP TestBSTR(BSTR a, int b, int c);
};

class CppTest :public ICppTest
{
	STD_IUNKNOWN_METHODS_SIMPLE(ICppTest)
public:
	STDMETHODIMP TestInt(int a, int b, int c)
	{
		return E_NOTIMPL;
	}
	STDMETHODIMP TestString(STR a, int b, int c)
	{
		return E_NOTIMPL;
	}
	STDMETHODIMP TestBSTR(BSTR a, int b, int c)
	{
		return E_NOTIMPL;
	}
};

EXPORT
ICppTest* Cpp_Interface()
{
	return new CppTest();
}

//#include <OCIdl.h>
//#include <DispEx.h>
//
//
//EXPORT
//void TestSimple()
//{
//	//throw 5;
//	int* p = 0;
//	Print(*p);
//}
//
//EXPORT
//void TestUnmanaged(int& k, int& g)
//{
////MessageBox(0, L"", L"bb", 0);
//}
//
//EXPORT
//void TestStructBlit(POINT& p)
//{
//	Print(p.x);
//	p.y = 8;
//}
//
//struct STSTR
//{
//	int k;
//	LPWSTR s;
//};
//
//EXPORT
//void TestStructString(STSTR& p)
//{
//	Print(p.s);
//	Print(p.k);
//	//p.s = L"retstr";
//	//p.s = SysAllocString(L"retstr");
//	//p.s = (LPWSTR)CoTaskMemAlloc(10); CopyMemory(p.s, L"retstr", sizeof(L"retstr"));
//	p.s[0] = 'A';
//	p.k = 8;
//	Print(L"returning");
//}
//
//EXPORT
//void TestArray(int* p)
//{
//	Print(p[0]);
//	p[0] = 5;
//}
//
//EXPORT
//void TestArrayStr(LPWSTR* p)
//{
//	Print(p[0]);
//	*p[0] = 'A';
//}
//
//__interface __declspec(uuid("3AB5235E-2768-47A2-909A-B5852A9D1868"))
//	ITest :IUnknown
//{
//	HRESULT STDMETHODCALLTYPE Test1(int i);
//	//HRESULT STDMETHODCALLTYPE Test2(int* p);
//	HRESULT STDMETHODCALLTYPE TestOL(int* p);
//	HRESULT STDMETHODCALLTYPE TestNext(char* p);
//};
//
//class CTest :public ITest
//{
//	int _refCount;
//
//public:
//	CTest() {
//		_refCount = 1;
//	}
//
//	~CTest() {
//		Printf(L"dtor thread: %i", GetCurrentThreadId());
//		//Print(L"dtor");
//	}
//
//	HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) {
//		//wchar_t* name = L"?"; //info: QM2 has DebugGetInterfaceName
//		//if (riid == __uuidof(ITest)) name = L"ITest"; //called
//		//else if (riid == __uuidof(IUnknown)) name = L"IUnknown"; //called
//		//else if (riid == __uuidof(IProvideClassInfo)) name = L"IProvideClassInfo"; //called
//		//else if (riid == __uuidof(IDispatch)) name = L"IDispatch";
//		//else if (riid == __uuidof(IDispatchEx)) name = L"IDispatchEx";
//		//else if (riid == __uuidof(IErrorInfo)) name = L"IErrorInfo";
//		//else if (riid == __uuidof(IEnumVARIANT)) name = L"IEnumVARIANT";
//		//else if (riid == __uuidof(IConnectionPoint)) name = L"IConnectionPoint";
//		//else if (riid == __uuidof(IConnectionPointContainer)) name = L"IConnectionPointContainer";
//
//		//Printf(L"QueryInterface: %s", name);
//
//		if (riid != __uuidof(ITest) && riid != __uuidof(IUnknown)) {
//			*ppvObject = 0;
//			return E_NOINTERFACE;
//		}
//		_refCount++;
//		*ppvObject = this;
//		return 0;
//	}
//	ULONG STDMETHODCALLTYPE AddRef() {
//		++_refCount;
//		//Printf(L"AddRef, %i", _refCount);
//		return _refCount;
//	}
//	ULONG STDMETHODCALLTYPE Release() {
//		_refCount--;
//		//Printf(L"Release, %i", _refCount);
//		if (_refCount == 0) delete this;
//		return _refCount;
//	}
//
//	HRESULT STDMETHODCALLTYPE Test1(int i)
//	{
//		Print(L"Test1");
//		return 0;
//	}
//
//	//HRESULT STDMETHODCALLTYPE Test2(int* p)
//	//{
//	//	Print(p[0]);
//	//	return 0;
//	//}
//
//	HRESULT STDMETHODCALLTYPE TestOL(int* p)
//	{
//		Print(1);
//		return 0;
//	}
//
//	HRESULT STDMETHODCALLTYPE TestNext(char* p)
//	{
//		Print(2);
//		return 0;
//	}
//};
//
//EXPORT
//ITest* CreateTestInterface()
//{
//	return new CTest();
//}
//
//EXPORT void CreateTestInterface2(ITest*& t)
//{
//	t = new CTest();
//}
//
//
//
//
//
//
//#include <OleAuto.h>
//#include <oleacc.h>
//#pragma comment(lib, "oleacc.lib")
//
//
//EXPORT
//void DllTestAcc()
//{
//	POINT p = { 257, 1138 };
//	IAccessible* a = 0; VARIANT vc = {};
//	HRESULT hr = AccessibleObjectFromPoint(p, &a, &vc);
//	if (hr != 0) {
//		Print((uint)hr); return;
//	}
//	try {
//		BSTR s = 0;
//		hr = a->get_accName(vc, &s);
//		if (hr != 0) {
//			Print((uint)hr); return;
//		}
//
//		Print(s);
//		//Print((__int64)s);
//		//SysFreeString(s);
//
//	}
//	catch (...) {
//		a->Release();
//		Print(L"exception");
//	}
//}

class One
{
public:
	int m;
	One() {
		m = 1;
	}
	virtual ~One() {
		Printf(L"~One, %i", m);
	}
};

class Two : public One
{
public:
	~Two() {
		Print(L"~Two");
		m = 0;
	}
};

class Move
{
public:
	int m;

	Move() {
		m = 1;
	}

	//~Move() {
	//	Printf(L"~Move, %i", m);
	//}

	Move(Move& x) {
		m = x.m;
	}

	Move(Move&& x) {
		m = x.m;
		x.m = 0;
	}

	Move& operator=(Move& x) {
		m = x.m;
		return *this;
	}

	Move& operator=(Move&& x) {
		m = x.m;
		x.m = 0;
		return *this;
	}
};

class Move2 :public Move
{
public:
	Move2() : Move() {}

	~Move2() {
		Printf(L"~Move2, %i", m);
	}

	Move2(Move2& x) {
		m = x.m;
	}

	Move2(Move2&& x) {
		m = x.m;
		x.m = 0;
	}

	Move2& operator=(Move2& x) {
		m = x.m;
		return *this;
	}

	Move2& operator=(Move2&& x) {
		Move::operator=(std::move(x));
		return *this;
	}
};

void _TestIAccessibleImpl();

//struct S1 { int i; int a[1]; };
//struct S2 { int i; int a[0]; };
//struct S3 { int i; int a[]; };
//struct S4 { double d; int i; char a[]; };
//struct S5 { char i; int a[]; };

//void TestFileDialog() {
//	IFileDialog* pfd;
//
//	// CoCreate the dialog object.
//	HRESULT hr = CoCreateInstance(CLSID_FileOpenDialog,
//		NULL,
//		CLSCTX_INPROC_SERVER,
//		__uuidof(IFileDialog), (LPVOID*)&pfd);
//
//	if(SUCCEEDED(hr))     {
//		// Show the dialog
//		hr = pfd->Show(0);
//
//		if(SUCCEEDED(hr))         {
//			// Obtain the result of the user's interaction with the dialog.
//			IShellItem* psiResult;
//			hr = pfd->GetResult(&psiResult);
//
//			if(SUCCEEDED(hr))             {
//				// Do something with the result.
//				psiResult->Release();
//			}
//		}
//		pfd->Release();
//	}
//
//}

EXPORT void Cpp_Test()
{
	//TestFileDialog();


	//_TestIAccessibleImpl();
		//CHandle hRead, hWrite;
		//if(!CreatePipe(&hRead.m_h, &hWrite.m_h, null, 0)) return;
		//Printf(L"%p %p", hRead.m_h, hWrite.m_h);


		//HWND w = FindWindowW(L"QM_Editor", null);
		//wn::PrintWnd(w);
		////Print(wn::ClassNameIs(w, L"QM_Editor"));
		////Print(wn::ClassNameIs(w, L"QM_Edito"));
		////Print(wn::ClassNameIs(w, L"QM_*"));
		////Print(wn::ClassNameIs(w, L"-QM_*"));

		//str::Wildex x;
		////STR s = L"**k kkk";
		////STR s = L"**r (kkk";
		//STR s = L"QM_Editor";
		//s=L"**t QM_Editor";
		////s=L"**r .+r$";
		//s=L"**m moo||QM_Editor";
		//s=L"**n QM_Editor-";
		//s=L"**m *_Editor";
		//s=L"**m moo||**r .+r$";
		//s=L"**m moo||**r .+r$||**n *i*";
		//Bstr es;
		//if(!x.Parse(s, wcslen(s), true, &es)) {
		//	Print(es); return;
		//}
		//Print(wn::ClassNameIs(w, x));
		//Print(x.Match(L"QM_Editor", 9));

		//str::StringBuilder t;
		////STR s = L"TEST";
		//LPWSTR s = L"TEST";
		//t.Append(s);
		//Print(t);

		//Move2 t1;
		//t1.m = 5;

		////Move2 t2 = t1;
		////Move2 t2; t2 = t1;

		////Move2 t2 = std::move(t1);
		//Move2 t2; t2 = std::move(t1);

		//Printf(L"%i %i", t1.m, t2.m);

		//str::StringBuilder b;
		////b.Append(L"---");
		////b.Append(L"123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 123456789 ");
		//for(int i = 0; i < 1000; i++) {
		//	b << i;
		//	b << L"    ";
		//}
		//
		//Print(b);

		//for(int i = 0; i < 5; i++) {
		//	Sleep(100);
		//	Perf.First();
		//	Smart<IStream> x;
		//	CreateStreamOnHGlobal(0, true, &x);
		//	for(int j = 0; j < 100000; j++) if(x->Write("abcdefgh", 8, null)) { Print("failed"); return; }

		//	Perf.NW();

		//	DWORD size = 0; istream::GetSize(x, size); Print(size);
		//}


		//eKKK e = eKKK::One | eKKK::Two;
		//eKKK e = eKKK::One;

		//Print(!!e);

		////if(e&eKKK::One) Print(1);
		//if((bool)(e & eKKK::One)) Print(1);
		//if(ALL(e, eKKK::One)) Print(2);
		//if((bool)(e & (eKKK::One | eKKK::Four))) Print(3);
		////if(ANY(e, eKKK::One, eKKK::Four)) Print(4);
		//if(ANY(e, eKKK::One | eKKK::Four)) Print(4);

		//eAF2 f = eAF::eAF2::InWebPage| eAF::eAF2::Java;

		//Printf(L"%*sa", 4, L"");

		//HWND w = FindWindowW(L"QM_Editor", null);
		////int x = 7;
		////wn::EnumChildWindows(w, [](HWND c) { wn::PrintWnd(c); return true; });

		//STR s_testSTR = L"thames";

		//Print(str::Switch(s_testSTR, 6, { L"moo", L"te", L"thames", L"notin" }));

		////Print(wn::ClassNameIs(w, { L"moo", L"qm_*itor", L"khy" }));

		//TestStringBuilder();

		//Bstr b1;
		//Print((void*)b1.m_str);
		//Bstr b2(L"test");
		//Print(b2);

		//Bstr s;
		//Print(wn::ClassName(w, s));
		//Print(s);
		//Print(wn::Name(w, s));
		//Print(s);

		//Print(sizeof(CSimpleArray<int>));
		//Print(sizeof(CAtlArray<int>));
		//Print(sizeof(std::vector<int>));
		//Print(sizeof(std::array<int, 100>));

		//Print(str::Switch(L"two", 3, L"one", L"two", L"three", null));

		//#if true
		//	eTest t = eTest::One | eTest::Two;
		//	//eTest t = eTest::Two;
		//	//t = (eTest)0;
		//	Print(t); //error if 'enum class'
		//	Print((int)t);
		//
		//	if(t) Print("t is not 0"); //error if 'enum class'
		//	if(t&eTest::Two) Print("has Two"); //error if 'enum class'
		//	if(!t) Print("t is 0");
		//	if(!!t) Print("t is not 0");
		//	if(!!(t&eTest::Two)) Print("has Two");
		//
		//	if((t&eTest::Two) == eTest::Two) Print("has Two");
		//	t |= eTest::Four;
		//	if((t&(eTest::Two | eTest::Four)) == (eTest::Two | eTest::Four)) Print("has (Two|Four)");
		//	Print((int)(t&~eTest::Two));
		//#else
		//	eTest t = One | Two;
		//	//eTest t = Two;
		//	Print(t);
		//	if(t&Two) Print("has Two");
		//	if(t&Two == Two) Print("has Two");
		//	t |= Four;
		//	if(t&(Two | Four) == (Two | Four)) Print("has (Two|Four)");
		//	Print(t&~Two);
		//#endif
		//
		//	POINT p = {}, pp=p;
		//	//int p = 5, pp = p;
		//	//p |= pp;
		//	//if(!p) Print(1);

		//Print(sizeof(std::wstring));
		//Print(sizeof(CString));

		//Perf.First();
		//for(size_t i = 0; i < 100000; i++) {
		////for(size_t i = 0; i < 1; i++) {
		//	Voo(L"1234567890");
		//	//Voo(null);
		//}
		//Perf.NW();

		//static TestTL s;
		//static thread_local TestTL t;

		//CSimpleMap<int, int> m;
		//m.Add(1, 10);
		//m.Add(2, 20);
		//Print(m.FindKey(2));

		//void* p = null;
		//for(int i = 1; i < 20; i++) {
		//	p = _recalloc(p, i, 8);
		//	Printf(L"%i %p", i, p);
		//}

		//CAtlMap<int, int> m;


		//CAtlList<int> _a;
		//_a.AddTail(1);
		//_a.AddTail(2);
		//for(auto pos = _a.GetHeadPosition(); pos != null;) {
		//	Print(_a.GetNext(ref pos));
		//}

		//PRINTHEX(10);
		//PRINTF(L"%s %i", L"MOO", 10);

		////Print(IsOS64Bit());

		////HWND w = FindWindow(L"Chrome_WidgetWin_1", null);
		//HWND w = FindWindow(L"QM_Editor", null);
		//if(!w) {
		//	Print("window not found");
		//	return;
		//}
		//DWORD pid, tid = GetWindowThreadProcessId(w, &pid);
		////bool is64bit, ok = IsProcess64Bit(pid, is64bit);
		////Printf(L"ok=%i is64=%i", ok, is64bit);

		//Bstr s;

}

//#include "IAccessible2.h"
//
//void InProcAccTest(IAccessible* a)
//{
//	//Print(L"TEST");
//	Smart<IAccessible2> a2;
//	if(!QueryService(a, &a2, &IID_IAccessible)) { Print("QS failed"); return; }
//
//	//AccessibleStates states;
//	//if(a2->get_states(&states)) { Print("get_states failed"); return; }
//	//Print((uint)states);
//
//	//long n;
//	//if(a2->get_nExtendedStates(&n)) { Print("get_nExtendedStates failed"); return; } //fails
//	//Print(n);
//}

class MyAcc :IAccessible
{
	HWND _w;
	int _ref;

public:
	MyAcc(HWND w) {
		_w = w;
		_ref = 1;
	}
#pragma region
	// Inherited via IAccessible
	virtual HRESULT __stdcall QueryInterface(REFIID riid, void ** ppvObject) override
	{
		if(riid == IID_IUnknown || riid == IID_IDispatch || riid == IID_IAccessible) {
			*ppvObject = this;
			_ref++;
			return 0;
		}
		*ppvObject = 0;
		return E_NOINTERFACE;
	}
	virtual ULONG __stdcall AddRef(void) override
	{
		_ref++;
		return 1;
	}
	virtual ULONG __stdcall Release(void) override
	{
		_ref--;
		Printf(L"Release: %i", _ref);
		return 1;
	}
	virtual HRESULT __stdcall GetTypeInfoCount(UINT * pctinfo) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall GetTypeInfo(UINT iTInfo, LCID lcid, ITypeInfo ** ppTInfo) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall GetIDsOfNames(REFIID riid, LPOLESTR * rgszNames, UINT cNames, LCID lcid, DISPID * rgDispId) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall Invoke(DISPID dispIdMember, REFIID riid, LCID lcid, WORD wFlags, DISPPARAMS * pDispParams, VARIANT * pVarResult, EXCEPINFO * pExcepInfo, UINT * puArgErr) override
	{
		return E_NOTIMPL;
	}
#pragma endregion
	virtual HRESULT __stdcall get_accParent(IDispatch ** ppdispParent) override
	{
		//Print(__FUNCTION__);
		return AccessibleObjectFromWindow(_w, 0, IID_IAccessible, (void**)ppdispParent);
	}
	virtual HRESULT __stdcall get_accChildCount(long * pcountChildren) override
	{
		//Print(__FUNCTION__);
		*pcountChildren = 0;
		return 0;
	}
	virtual HRESULT __stdcall get_accChild(VARIANT varChild, IDispatch ** ppdispChild) override
	{
		//Print(__FUNCTION__);
		*ppdispChild = 0;
		if(varChild.vt != VT_I4)return E_INVALIDARG;
		return S_FALSE;
	}
	virtual HRESULT __stdcall get_accName(VARIANT varChild, BSTR * pszName) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accValue(VARIANT varChild, BSTR * pszValue) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accDescription(VARIANT varChild, BSTR * pszDescription) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accRole(VARIANT varChild, VARIANT * pvarRole) override
	{
		//Print(__FUNCTION__);
		pvarRole->vt = VT_I4; pvarRole->lVal = varChild.lVal == 0 ? ROLE_SYSTEM_LIST : ROLE_SYSTEM_LISTITEM;
		return 0;
	}
	virtual HRESULT __stdcall get_accState(VARIANT varChild, VARIANT * pvarState) override
	{
		//Print(__FUNCTION__);
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accHelp(VARIANT varChild, BSTR * pszHelp) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accHelpTopic(BSTR * pszHelpFile, VARIANT varChild, long * pidTopic) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accKeyboardShortcut(VARIANT varChild, BSTR * pszKeyboardShortcut) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accFocus(VARIANT * pvarChild) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accSelection(VARIANT * pvarChildren) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accDefaultAction(VARIANT varChild, BSTR * pszDefaultAction) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall accSelect(long flagsSelect, VARIANT varChild) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall accLocation(long * pxLeft, long * pyTop, long * pcxWidth, long * pcyHeight, VARIANT varChild) override
	{
		//Print(__FUNCTION__);
		WINDOWINFO wi = { sizeof(WINDOWINFO) }; GetWindowInfo(_w, &wi);
		*pxLeft = wi.rcClient.left;
		*pyTop = wi.rcClient.top;
		*pcxWidth = wi.rcClient.right - wi.rcClient.left;
		*pcyHeight = wi.rcClient.bottom - wi.rcClient.top;
		return 0;
	}
	virtual HRESULT __stdcall accNavigate(long navDir, VARIANT varStart, VARIANT * pvarEndUpAt) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall accHitTest(long xLeft, long yTop, VARIANT * pvarChild) override
	{
		//Print(__FUNCTION__);
		pvarChild->vt = VT_I4;
		pvarChild->lVal = 0;
		return 0;
	}
	virtual HRESULT __stdcall accDoDefaultAction(VARIANT varChild) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall put_accName(VARIANT varChild, BSTR szName) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall put_accValue(VARIANT varChild, BSTR szValue) override
	{
		return E_NOTIMPL;
	}
};

MyAcc* m;

LRESULT __stdcall _WndProc(HWND w, UINT msg, WPARAM wParam, LPARAM lParam) {

	//Print(msg);
	switch(msg) {
	case WM_DESTROY:
		m = null;
		//PostQuitMessage(0);
		PostMessage(w, WM_APP + 127, 0, 0);
		break;
	case WM_GETOBJECT:
		//Print((int)lParam);
		if((int)lParam == OBJID_CLIENT) {
			Print("WM_GETOBJECT");
			if(m == null) m = new MyAcc(w);
			return LresultFromObject(IID_IAccessible, wParam, (LPUNKNOWN)m);
		}
		break;
	}

	return DefWindowProcW(w, msg, wParam, lParam);
}

void _TestIAccessibleImpl() {

	WNDCLASSEXW x = {};
	x.cbSize = sizeof(x);
	x.hbrBackground = (HBRUSH)(COLOR_BTNFACE + 1);
	x.hCursor = LoadCursor(0, (STR)IDC_ARROW);
	x.lpszClassName = L"Montejo";
	x.lpfnWndProc = _WndProc;
	auto atom = RegisterClassExW(&x);
	auto w = CreateWindowExW(WS_EX_TOPMOST, (STR)atom, L"Gooo", WS_VISIBLE | WS_POPUPWINDOW | WS_CAPTION, 500, 300, 300, 300, 0, 0, 0, 0);
	MSG m;
	while(GetMessageW(&m, 0, 0, 0) > 0) {
		if(m.message == WM_APP + 127) break;
		TranslateMessage(&m);
		DispatchMessageW(&m);
	}
}

#endif //#if _DEBUG

//not tested
//struct _EWData {
//	int** aAll;
//	int** aVisible;
//	int nAll, nVisible, capAll, capVisible;
//};
//
//void _EWAdd(int*& a, int& len, int& cap, int initCap, HWND w) {
//	if(a == null) a = (int*)HeapAlloc(GetProcessHeap(), 0, (cap = initCap) * 4);
//	else if(len == cap) a = (int*)HeapReAlloc(GetProcessHeap(), 0, a, (cap *= 2) * 4);
//	a[len++] = (int)(LPARAM)w;
//}
//
//BOOL _EWProc(HWND w, LPARAM param) {
//	_EWData& d = *(_EWData*)param;
//	if(d.aAll != null) _EWAdd(*d.aAll, d.nAll, d.capAll, 1000, w);
//	if(d.aVisible != null && IsWindowVisible(w)) _EWAdd(*d.aVisible, d.nVisible, d.capVisible, 200, w);
//	return 1;
//}
//
//EXPORT void EnumWindowsEx(int api, int** aAll, int& nAll, int** aVisible, int& nVisible) {
//	_EWData d;
//	d.aAll = aAll;
//	d.aVisible = aVisible;
//	d.capAll = nAll;
//	d.capVisible = nVisible;
//	d.nAll = d.nVisible = 0;
//
//	EnumWindows(_EWProc, (LPARAM)&d);
//
//	if(aAll != null) nAll = d.nAll;
//	if(aVisible != null) nVisible = d.nVisible;
//}

//struct _EWData {
//	int* a;
//	int cap, len;
//	bool onlyVisible;
//};
//
//BOOL _EWProc(HWND w, LPARAM param) {
//	_EWData& d = *(_EWData*)param;
//	if(d.onlyVisible && !IsWindowVisible(w)) return 1;
//	if(d.len == d.cap) {
//		HANDLE hh = GetProcessHeap();
//		if(d.cap == 0) d.a = (int*)HeapAlloc(hh, 0, (d.cap = (d.onlyVisible ? 200 : 1000)) * 4);
//		else d.a = (int*)HeapReAlloc(hh, 0, d.a, (d.cap *= 2) * 4);
//	}
//	d.a[d.len++] = (int)(LPARAM)w;
//	return 1;
//}
//
//EXPORT int* EnumWindowsEx(int& len, BOOL onlyVisible, int api) {
//	_EWData d = {};
//	d.onlyVisible = onlyVisible;
//
//	EnumWindows(_EWProc, (LPARAM)&d);
//	len = d.len;
//	return d.a;
//}
//Faster than C#. Eg 250 vs 310 mcs.
