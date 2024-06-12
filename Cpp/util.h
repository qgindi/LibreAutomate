#pragma once
#include "stdafx.h"

#define ZEROTHIS memset(this, 0, sizeof(*this))
#define ZEROTHISFROM(member) memset((LPBYTE)this+((LPBYTE)&member-(LPBYTE)this), 0, sizeof(*this)-((LPBYTE)&member-(LPBYTE)this))

#if _DEBUG
#define TRACE 1
#endif

#if TRACE

void Print(STR s);
void Printf(STR frm, ...);

inline void Print(LPCSTR s) { Printf(L"%S", s); }
inline void Print(const std::wstring& s) { Print(s.c_str()); }
inline void Print(int i) { Printf(L"%i", i); }
inline void Print(unsigned int i) { Printf(L"0x%X", i); }
inline void Print(long i) { Print((int)i); }
inline void Print(unsigned long i) { Print((unsigned int)i); }
inline void Print(__int64 i) { Printf(L"%I64i", i); }
inline void Print(unsigned __int64 i) { Printf(L"0x%I64X", i); }
inline void Print(void* i) { Printf(sizeof(void*) == 8 ? L"%I64i" : L"%i", i); }

#define PRINTI(x) Printf(L"debug: " __FILEW__ "(" _CRT_STRINGIZE(__LINE__) "):  %i", x)
#define PRINTS(x) Printf(L"debug: " __FILEW__ "(" _CRT_STRINGIZE(__LINE__) "):  %s", x)
#define PRINTHEX(x) Printf(L"debug: " __FILEW__ "(" _CRT_STRINGIZE(__LINE__) "):  0x%X", x)
#define PRINTF(formatString, ...) Printf(L"debug: " __FILEW__ "(" _CRT_STRINGIZE(__LINE__) "):  " formatString, __VA_ARGS__)
#define PRINTF_IF(condition, formatString, ...) { if(condition) PRINTF(formatString, __VA_ARGS__); }
#define PRINTS_IF(condition, x) { if(condition) PRINTS(x); }

inline void PrintComRefCount(IUnknown* u) {
	if (u) {
		u->AddRef();
		int i = u->Release();
		Printf(L"%p  %i", u, i);
	} else Print(L"null");
}

#else
#define Print __noop
#define Printf __noop
#define PRINTI __noop
#define PRINTS __noop
#define PRINTHEX __noop
#define PRINTF __noop
#define PRINTF_IF __noop
#define PRINTS_IF __noop
#define PrintComRefCount __noop
#endif

#if TRACE

struct Perf_Inst {
private:
	int _counter;
	bool _incremental;
	int _nMeasurements; //used with incremental to display n measurements and average times
	__int64 _time0;
	static const int _nElem = 16;
	__int64 _a[_nElem];
	wchar_t _aMark[_nElem];

public:
	//Perf_Inst() noexcept { ZEROTHIS; } //not used because then does not work shared data section
	Perf_Inst() {}
	Perf_Inst(bool isLocal) { if (isLocal) ZEROTHIS; }

	void First();
	void Next(char cMark = '\0');
	void Write();

	void NW(char cMark = '\0') { Next(cMark); Write(); }

	void SetIncremental(bool yes) {
		if (_incremental = yes) {
			for (int i = 0; i < _nElem; i++) _a[i] = 0;
			_nMeasurements = 0;
		}
	}
};

extern Perf_Inst Perf;

class PerfLocal {
	Perf_Inst _p;
public:
	PerfLocal() : _p(true) { _p.First(); }
	~PerfLocal() { _p.NW(); }
	void Next(char mark = '\0') { _p.Next(mark); }
};

#endif

#include "str.h"


extern HMODULE s_moduleHandle;
bool IsOS64Bit();
bool IsProcess64Bit(DWORD pid, out bool& is);

inline bool IsThisProcess64Bit() {
#ifdef _WIN64
	return true;
#else
	return false;
#endif
}


//Standard IUnknown implementation with thread-unsafe refcounting.
#define STD_IUNKNOWN_METHODS(iface) \
STDMETHODIMP QueryInterface(REFIID iid, void** ppv)\
{\
	if(iid == IID_IUnknown || iid == IID_##iface) { _cRef++; *ppv = this; return S_OK; }\
	else { *ppv = nullptr; return E_NOINTERFACE; }\
}\
STDMETHODIMP_(ULONG) AddRef()\
{\
	return ++_cRef;\
}\
STDMETHODIMP_(ULONG) Release()\
{\
	long ret=--_cRef;\
	if(!ret) delete this;\
	return ret;\
}


//Standard IUnknown implementation without refcounting.
#define STD_IUNKNOWN_METHODS_SIMPLE(iface) \
STDMETHODIMP QueryInterface(REFIID iid, void** ppv)\
{\
	if(iid == IID_IUnknown || iid == IID_##iface) { *ppv = this; return S_OK; }\
	else { *ppv = nullptr; return E_NOINTERFACE; }\
}\
STDMETHODIMP_(ULONG) AddRef() { return 1; }\
STDMETHODIMP_(ULONG) Release() { return 1; }


//Smart pointer that extends CComPtr.
//I don't use _com_ptr_t because: 1. Can throw. 2. Intellisense bug after upgrading VS: shows many false errors.
template <class T>
class Smart : public CComPtr<T> {
public:
	Smart() throw() {
	}
	Smart(_Inout_opt_ T* lp, bool addRef) throw() {
		this->p = lp;
		if (addRef) this->p->AddRef();
	}
	Smart(_Inout_ const Smart<T>& lp) throw() : CComPtr<T>(lp.p) {
	}

	void Swap(CComPtrBase<T>& other) {
		T* pTemp = this->p;
		this->p = other.p;
		other.p = pTemp;
	}

};


//Delay-loaded API pointers.
struct DelayLoadedApi {
	bool minWin81, minWin10;

	//user32
	BOOL(__stdcall* PhysicalToLogicalPoint)(HWND hWnd, LPPOINT lpPoint);
	BOOL(__stdcall* LogicalToPhysicalPoint)(HWND hWnd, LPPOINT lpPoint);
	DPI_AWARENESS_CONTEXT(__stdcall* GetWindowDpiAwarenessContext)(HWND hwnd);
	DPI_AWARENESS(__stdcall* GetAwarenessFromDpiAwarenessContext)(DPI_AWARENESS_CONTEXT value);
	DPI_AWARENESS_CONTEXT(__stdcall* SetThreadDpiAwarenessContext)(DPI_AWARENESS_CONTEXT dpiContext);

	//shcore
	HRESULT(__stdcall* GetProcessDpiAwareness)(HANDLE hprocess, PROCESS_DPI_AWARENESS* value);

#define GPA(hm, f) *(FARPROC*)&f=GetProcAddress(hm, #f)
#define GPA2(hm, f, name) *(FARPROC*)&f=GetProcAddress(hm, name)
	DelayLoadedApi() noexcept {
		minWin81 = minWin10 = false;
		auto hm = GetModuleHandle(L"user32.dll");
		GPA2(hm, PhysicalToLogicalPoint, "PhysicalToLogicalPointForPerMonitorDPI");
		if (minWin81 = PhysicalToLogicalPoint) { //Win8.1+
			GPA2(hm, LogicalToPhysicalPoint, "LogicalToPhysicalPointForPerMonitorDPI");

			GPA(hm, GetWindowDpiAwarenessContext);
			if (minWin10 = GetWindowDpiAwarenessContext) { //Win10 1607+
				GPA(hm, GetAwarenessFromDpiAwarenessContext);
				GPA(hm, SetThreadDpiAwarenessContext);

			} else {
				auto hm2 = GetModuleHandle(L"shcore.dll");
				GPA(hm2, GetProcessDpiAwareness);
			}


		} else { //Win7/8
			GPA(hm, PhysicalToLogicalPoint);
			//GPA(hm, LogicalToPhysicalPoint);

		}
	}
};
extern DelayLoadedApi dlapi;


//SECURITY_ATTRIBUTES that allows UAC low integrity level processes to open the kernel object.
//can instead use CSecurityAttributes/CSecurityDesc, but this added before including ATL, and don't want to change now.
class SecurityAttributes {
	DWORD nLength;
	LPVOID lpSecurityDescriptor;
	BOOL bInheritHandle;
public:

	SecurityAttributes() {
		nLength = sizeof(SecurityAttributes);
		bInheritHandle = false;
		lpSecurityDescriptor = null;
		BOOL ok = ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:NO_ACCESS_CONTROLS:(ML;;NW;;;LW)", 1, &lpSecurityDescriptor, null);
		assert(ok);
	}

	~SecurityAttributes() {
		LocalFree(lpSecurityDescriptor);
	}

	//SECURITY_ATTRIBUTES* operator&() {
	//	return (SECURITY_ATTRIBUTES*)this;
	//}

	static SECURITY_ATTRIBUTES* Common() {
		static SecurityAttributes s_sa;
		return (SECURITY_ATTRIBUTES*)&s_sa;
	}
};

class AutoReleaseMutex {
	HANDLE _mutex;
public:
	AutoReleaseMutex(HANDLE mutex) noexcept {
		_mutex = mutex;
	}

	~AutoReleaseMutex() {
		if (_mutex) ReleaseMutex(_mutex);
	}

	void ReleaseNow() {
		if (_mutex) ReleaseMutex(_mutex);
		_mutex = 0;
	}
};

//class ProcessMemory
//{
//	LPBYTE m_mem;
//	HANDLE m_hproc;
//public:
//	ProcessMemory() { ZEROTHIS; }
//	~ProcessMemory() { Free(); }
//	LPBYTE Alloc(HWND hWnd, DWORD nBytes, DWORD flags = 0);
//};

//currently not used.
////can instead use CAtlFileMappingBase from atlfile.h, but this added before including ATL, and don't want to change now.
//class SharedMemory
//{
//	HANDLE _hmapfile;
//	LPBYTE _mem;
//public:
//	SharedMemory() { _hmapfile = 0; _mem = 0; }
//	~SharedMemory() { Close(); }
//
//	bool Create(STR name, DWORD size)
//	{
//		Close();
//		_hmapfile = CreateFileMappingW((HANDLE)(-1), SecurityAttributes::Common(), PAGE_READWRITE, 0, size, name);
//		if(!_hmapfile) return false;
//		_mem = (LPBYTE)MapViewOfFile(_hmapfile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
//		return _mem != null;
//	}
//
//	bool Open(STR name)
//	{
//		Close();
//		_hmapfile = OpenFileMappingW(FILE_MAP_ALL_ACCESS, 0, name);
//		if(!_hmapfile) return false;
//		_mem = (LPBYTE)MapViewOfFile(_hmapfile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
//		return _mem != null;
//	}
//
//	void Close()
//	{
//		if(_mem) { UnmapViewOfFile(_mem); _mem = 0; }
//		if(_hmapfile) { CloseHandle(_hmapfile); _hmapfile = 0; }
//	}
//
//	LPBYTE Mem() { return _mem; }
//
//	bool Is0() { return _mem == null; }
//};

//currently not used.
//template<class T>
//class AutoResetVariable
//{
//	T* _b;
//public:
//	AutoResetVariable(T* b, T value) { _b = b; *b = value; }
//	~AutoResetVariable() { *_b = 0; }
//};

//IStream helpers.
class istream {
public:
	static LARGE_INTEGER LI(__int64 i) {
		LARGE_INTEGER r; r.QuadPart = i;
		return r;
	}

	static ULARGE_INTEGER ULI(__int64 i) {
		ULARGE_INTEGER r; r.QuadPart = i;
		return r;
	}

	static bool ResetPos(IStream* x) {
		return 0 == x->Seek(LI(0), STREAM_SEEK_SET, null);
	}

	static bool GetPos(IStream* x, out DWORD& pos) {
		pos = 0;
		__int64 pos64;
		if (x->Seek(LI(0), STREAM_SEEK_CUR, (ULARGE_INTEGER*)&pos64)) return false;
		pos = (DWORD)pos64;
		return true;
	}

	//static bool GetSize(IStream* x, out DWORD& size) {
	//	size = 0;
	//	STATSTG stat;
	//	if(x->Stat(&stat, STATFLAG_NONAME)) return false;
	//	size = stat.cbSize.LowPart;
	//	return true;
	//}

	static bool Clear(IStream* x) {
		return 0 == x->SetSize(ULI(0));
	}
};

//Use inproc to DPI-scale elm rect from logical to physical.
class DpiElmScaling {
	HWND _w;
	RECT _rw;
	bool _scaled;
	bool _haveRect;
public:
	//Use w or acc. If acc not null, ignores w and calls WindowFromAccessibleObject.
	DpiElmScaling(bool use, HWND w, IAccessible* acc) {
		ZEROTHIS;
		if (!use || !dlapi.minWin81) return; //on Win7/8 we get physical rect

		if (acc != null) {
			if (!(0 == WindowFromAccessibleObject(acc, &w) && w)) { //TODO3: if w specified too in the props string, don't call twice. Same with r/D.
				PRINTS(L"failed WindowFromAccessibleObject");
				return;
			}
		}
		_w = w;

		auto da = DPI_AWARENESS::DPI_AWARENESS_SYSTEM_AWARE;
		if (dlapi.GetWindowDpiAwarenessContext) { //Win10 1607
			da = dlapi.GetAwarenessFromDpiAwarenessContext(dlapi.GetWindowDpiAwarenessContext(w)); //fast
			if (da != DPI_AWARENESS::DPI_AWARENESS_SYSTEM_AWARE && da != DPI_AWARENESS::DPI_AWARENESS_UNAWARE) return;
		} else { //Win8.1
			PROCESS_DPI_AWARENESS pda;
			if (0 == dlapi.GetProcessDpiAwareness(GetCurrentProcess(), &pda) && pda == PROCESS_DPI_AWARENESS::PROCESS_PER_MONITOR_DPI_AWARE) return;
		}

		if (!(_haveRect = GetWindowRect(_w, &_rw))) { _w = 0; return; }

		//On Win10+ we can easily, quickly and reliably detect whether the window is DPI-scaled.
		if (dlapi.SetThreadDpiAwarenessContext) { //Win10 1607
			auto ac = dlapi.SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); //fast
			RECT r2;
			_scaled = GetWindowRect(_w, &r2) && memcmp(&_rw, &r2, 16);
			dlapi.SetThreadDpiAwarenessContext(ac);
		} else _scaled = true; //on Win8.1 assume need scaling, it does not harm
	}

	//Returns: 0 don't need to scale, 1 r is not in the window, 2 scaled ok, -1 failed to scale.
	//needReturn1 - even if don't need to scale, return 1 if r is not in the window.
	int ScaleIfNeed(ref RECT& r, bool needReturn1 = false) {
		if (!_scaled) {
			if (needReturn1 && _w) {
				if (!_haveRect && !(_haveRect = GetWindowRect(_w, &_rw))) { _w = 0; return 0; }
				if (!IntersectRect(&r, &r, &_rw)) return 1;
			}
			return 0;
		}

		//The API fails if the point is not in the window rect, except when touches it at the right/bottom.
		//Tried workaround: create a hidden window with same DPI awareness and use it with the API instead of w.
		//	In most cases it works, but often does not scale if the point is outside the top-level window.
		//Current workaround: use r intersection with the container window rect.
		if (!IntersectRect(&r, &r, &_rw)) return 1;

		POINT p1 = { r.left, r.top }, p2 = { r.right, r.bottom };
		if (dlapi.LogicalToPhysicalPoint(_w, &p1) && dlapi.LogicalToPhysicalPoint(_w, &p2)) {
			SetRect(&r, p1.x, p1.y, p2.x, p2.y);
			return 2;
		} else {
			PRINTS(L"failed LogicalToPhysicalPoint");
			return -1;
		}
	}
};

namespace wn {
	inline DWORD Style(HWND w) { return (DWORD)GetWindowLongPtrW(w, GWL_STYLE); }
	inline DWORD ExStyle(HWND w) { return (DWORD)GetWindowLongPtrW(w, GWL_EXSTYLE); }
	bool ClassName(HWND w, out Bstr& s);
	int ClassNameIs(HWND w, std::initializer_list<STR> a);
	bool ClassNameIs(HWND w, STR s);
	bool ClassNameIs(HWND w, const str::Wildex& s);
	bool Name(HWND w, out Bstr& s);
	bool IsVisibleInWindow(HWND c, HWND wTL);

	using WNDENUMPROCL = const std::function <bool(HWND c)>;

	BOOL EnumChildWindows(HWND w, WNDENUMPROCL& callback);
	HWND FindChildByClassName(HWND w, STR className, bool visible);
	HWND FindChildByClassName(HWND w, STR className1, STR className2, OUT bool& second, bool visible);
	HWND FindWndEx(HWND wParent, HWND wAfter, STR cn, STR name = null);
	HWND FindWnd(STR cn, STR name = null);
	HWND FindWndExVisible(HWND wParent, STR cn);
	bool WinformsNameIs(HWND w, STR name);

#if TRACE
	void PrintWnd(HWND w);
#else
#define PrintWnd __noop
#endif
}

bool QueryService_(IUnknown* iFrom, OUT void** iTo, REFIID iid, const GUID* guidService = null);

template<class T>
bool QueryService(IUnknown* iFrom, OUT T** iTo, const GUID* guidService = null) {
	return QueryService_(iFrom, (void**)iTo, __uuidof(T), guidService);
}

namespace util {
	//Swaps values of variables a and b: <c>T t = a; a = b; b = t;</c>
	template<class T>
	void Swap(ref T& a, ref T& b) {
		T t = a; a = b; b = t;
	}

}
