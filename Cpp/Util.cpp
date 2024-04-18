#include "stdafx.h"
#include "cpp.h"

#pragma region print

HWND s_QM2;

#if TRACE

void Print(STR s) {
#if true
	if (!IsWindow(s_QM2)) {
		s_QM2 = wn::FindWnd(L"QM_Editor", 0); if (!s_QM2) return;
	}
	//SendMessageW(s_QM2, WM_SETTEXT, -1, (LPARAM)(s ? s : L""));
	DWORD_PTR res;
	SendMessageTimeoutW(s_QM2, WM_SETTEXT, -1, (LPARAM)(s ? s : L""), SMTO_BLOCK | SMTO_ABORTIFHUNG, 5000, &res);
#else
	_cputws(s);
	_cputws(L"\r\n");
#endif
}

void Printf(STR frm, ...) {
#if true //more options but makes big dll. Don't use in Release.
	wchar_t b[10000];
	_vsnwprintf(b, 9990, frm, (va_list)(&frm + 1));
#else //does not support floating-point, %.*s, etc. Too small max buffer size.
	wchar_t b[1032];
	wvsprintf(b, frm, (va_list)(&frm + 1));
#endif
	Print(b);
}

#endif

#pragma endregion


#if TRACE

void Perf_Inst::First() {
	QueryPerformanceCounter((LARGE_INTEGER*)&_time0);
	_counter = 0;
	_nMeasurements++;
}

void Perf_Inst::Next(char cMark/* = '\0'*/) {
	int n = _counter; if (n >= _nElem) return;
	_counter++;
	__int64 t; QueryPerformanceCounter((LARGE_INTEGER*)&t);
	t -= _time0;
	if (_incremental) _a[n] += t; else _a[n] = t;
	_aMark[n] = cMark;
}

void Perf_Inst::Write() {
	int i, n = _counter;
	if (n == 0) return;
	if (n > _nElem) n = _nElem;
	__int64 f; QueryPerformanceFrequency((LARGE_INTEGER*)&f); double freq = 1000000.0 / f;
	bool average = false; int nMeasurements = 1;

	str::StringBuilder s;
	s << L"speed:";
g1:
	double t = 0.0, tPrev = 0.0;
	for (i = 0; i < n; i++) {
		s << L"  ";
		if (_aMark[i] != '\0') {
			s << _aMark[i];
			s << '=';
		}
		t = freq * _a[i];
		double d = t - tPrev;
		if (average) d /= nMeasurements;
		s << (__int64)d;
		tPrev = t;
	}

	if (n > 1) {
		s << L"  (";
		if (average) t /= nMeasurements;
		s << (__int64)t;
		s << ')';
	}

	if (!average && _incremental && (nMeasurements = _nMeasurements) > 1) {
		average = true;
		s << L";  measured "; s << nMeasurements; s << L" times, average";
		goto g1;
	}

	Print(s);
}

#pragma section(".shared", read,write,shared)
__declspec(allocate(".shared")) Perf_Inst Perf;

#endif

DelayLoadedApi dlapi;

bool IsOS64Bit() {
#ifdef _WIN64
	return true;
#else
	static int r;
	if (!r) {
		if (IsWow64Process(GetCurrentProcess(), &r)) r = r ? 1 : -1;
	}
	return r > 0;
#endif
}

bool IsProcess64Bit(DWORD pid, out bool& is) {
	is = false;
	if (!IsOS64Bit()) return true;
	HANDLE hp = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
	if (!hp) return false;
	BOOL is32, ok = IsWow64Process(hp, &is32);
	CloseHandle(hp);
	if (!ok) return false;
	is = !is32;
	return true;
}

namespace wn {
	bool ClassName(HWND w, out Bstr& s) {
		WCHAR b[260];
		int n = GetClassNameW(w, b, 260);
		if (n == 0) {
			if (s) s.Empty();
			return false;
		}
		s.Assign(b, n);
		return true;
	}

	int ClassNameIs(HWND w, std::initializer_list<STR> a) {
		WCHAR b[260];
		int n = GetClassNameW(w, b, 260);
		if (n == 0) return 0;
		int i = 1;
		for (const STR* p = a.begin(); p < a.end(); p++, i++) if (str::Like(b, n, *p, wcslen(*p), true)) return i;
		return 0;
	}

	bool ClassNameIs(HWND w, STR s) {
		WCHAR b[260];
		int n = GetClassNameW(w, b, 260);
		return n > 0 && str::Like(b, n, s, wcslen(s), true);
	}

	bool ClassNameIs(HWND w, const str::Wildex& s) {
		WCHAR b[260];
		int n = GetClassNameW(w, b, 260);
		return n > 0 && s.Match(b, n);
	}

	bool Name(HWND w, out Bstr& s) {
		bool R = false;
		if (w) {
			Buffer<WCHAR, 1000> m;
			for (int na = 1000; na <= 10'000'000; na *= 10) {
				int nr = InternalGetWindowText(w, m.Alloc(na), na);
				if (nr < na - 1) {
					if (nr > 0) {
						s.Assign(m, nr);
						return true;
					}
					R = GetLastError() != 0;
					break;
				}
			}
		}
		if (s) s.Empty();
		return R;
	}

	//Like IsWindowVisible, but does not check wTL visibility.
	//Slower, but does not make EnumChildWindows significantly slower. Eg 550 -> 570-590 mcs.
	bool IsVisibleInWindow(HWND c, HWND wTL) {
		if (IsWindowVisible(c)) return true; //these two make faster in most cases (when wTL is visible)
		if (IsWindowVisible(wTL)) return false;
		int i;
		for (i = 0; i < 10000; i++) {
			//PrintWnd(c);
			if (!(wn::Style(c) & WS_VISIBLE)) return false;
			c = (HWND)(LPARAM)GetWindowLongPtrW(c, GWLP_HWNDPARENT);
			if (c == wTL) return true;
			if (c == 0) break;
		}
		assert(false);
		return false;
	}

	BOOL EnumChildWindows(HWND w, WNDENUMPROCL& callback) {
		return ::EnumChildWindows(w, [](HWND c, LPARAM p) { return (BOOL)(*(WNDENUMPROCL*)p)(c); }, (LPARAM)&callback);
	}

	//className - wildcard.
	HWND FindChildByClassName(HWND w, STR className, bool visible) {
		HWND R = 0;
		wn::EnumChildWindows(w, [&R, className, visible, w](HWND c) {
			if (visible && !IsVisibleInWindow(c, w)) return 1;
			if (!wn::ClassNameIs(c, className)) return 1;
			R = c;
			return 0;
			});
		return R;
	}

	HWND FindChildByClassName(HWND w, STR className1, STR className2, OUT bool& second, bool visible) {
		HWND R = 0;
		wn::EnumChildWindows(w, [&R, className1, className2, &second, visible, w](HWND c) {
			if (visible && !IsVisibleInWindow(c, w)) return 1;
			int i = wn::ClassNameIs(c, { className1, className2 });
			if (i == 0) return 1;
			second = i == 2;
			R = c;
			return 0;
			});
		return R;
	}

	HWND FindWndEx(HWND wParent, HWND wAfter, STR cn, STR name) {
		//see comments in Api.FindWindowEx.
		//never mind: should repeat only on Win11. Anyway I guess MS soon will fix it.
		for (int i = 5; --i >= 0;) {
			HWND w = ::FindWindowEx(wParent, wAfter, cn, name);
			if (w) return w;
		}
		return 0;
	}

	HWND FindWnd(STR cn, STR name) {
		return FindWndEx(0, 0, cn, name);
	}

	HWND FindWndExVisible(HWND wParent, STR cn) {
		HWND c = FindWndEx(wParent, 0, cn, null);
		while (c && !IsWindowVisible(c)) c = FindWndEx(wParent, c, cn, null);
		return c;
	}

	bool WinformsNameIs(HWND w, STR name) {
		static UINT WM_GETCONTROLNAME = RegisterWindowMessageW(L"WM_GETCONTROLNAME");
		const int c_bufChars = 1024;

		DWORD pid; if (0 == GetWindowThreadProcessId(w, &pid)) return false;
		wchar_t b[c_bufChars]; int len;
		if (pid == GetCurrentProcessId()) {
			len = (int)SendMessageW(w, WM_GETCONTROLNAME, c_bufChars, (LPARAM)b) - 1; if (len < 0) return false;
		} else {
			HANDLE hp = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ, false, pid); if (!hp) return false;
			void* pm = VirtualAllocEx(hp, null, c_bufChars * 2, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
			__try {
				if (pm == null) return false;
				DWORD_PTR n1; if (!SendMessageTimeoutW(w, WM_GETCONTROLNAME, c_bufChars, (LPARAM)pm, SMTO_ABORTIFHUNG, 10000, &n1) || --n1 < 0) return false;
				len = (int)n1; n1 *= 2;
				SIZE_T n2; if (n1 > 0 && !(ReadProcessMemory(hp, pm, b, n1, &n2) && n2 == n1)) return false;
			}
			__finally {
				if (pm != null) VirtualFreeEx(hp, pm, 0, MEM_RELEASE);
				CloseHandle(hp);
			}
		}
		//b[len] = 0; Print(len); Print(b);
		return str::Equals(name, str::Len(name), b, len);

		//note: don't need to cache the process handle and memory. It is several times faster than sendmessage. Anyway, inproc is rarely used with acc.
		//info: also there is WM_GETCONTROLTYPE, like "PaintDotNet.Controls.AppWorkspace, PaintDotNet, Version=4.21.6589.7045, Culture=neutral, PublicKeyToken=null"
	}

#if TRACE
	void PrintWnd(HWND w) {
		Bstr sc, sn;
		if (!w) {
			Print(L"<0 HWND>");
		} else if (!ClassName(w, out sc)) {
			Print(L"<invalid HWND>");
		} else {
			Name(w, out sn);
			RECT r = {}; GetWindowRect(w, &r);
			STR inv = IsWindowVisible(w) ? L"" : L" invisible";
			DWORD style = (DWORD)GetWindowLong(w, GWL_STYLE);
			Printf(L"%i %s \"%s\" {L=%i T=%i W=%i H=%i}%s style=0x%X",
				(int)(LPARAM)w, sc, sn, r.left, r.top, r.right - r.left, r.bottom - r.top, inv, style);
		}
	}
#endif
}

//Gets pointer to other interface through iserviceprovider.
//Use guidService if it is different than iid.
bool QueryService_(IUnknown* iFrom, OUT void** iTo, REFIID iid, const GUID* guidService/*=null*/) {
	*iTo = null;
	if (!guidService) guidService = &iid;
	Smart<IServiceProvider> sp;
	return 0 == iFrom->QueryInterface(&sp) && 0 == sp->QueryService(*guidService, iid, iTo) && *iTo;
}

//void DoEvents()
//{
//	MSG m;
//	while(PeekMessageW(&m, 0, 0, 0, PM_REMOVE)) {
//		//while(PeekMessageW(&m, 0, 0, 0, PM_REMOVE| PM_QS_SENDMESSAGE)) { //does not work
//		//Print(m.message);
//		//Bstr s; if(wn::ClassName(m.hwnd, s)) Print(s);
//		if(m.message == WM_QUIT) { PostQuitMessage((int)m.wParam); return; }
//		TranslateMessage(&m);
//		DispatchMessageW(&m);
//	}
//}
//
////void DoEvents2()
////{
////	DWORD signaledIndex;
////	auto hr = CoWaitForMultipleHandles(0, 0, 0, null, &signaledIndex); //fails, invalid parameter
////	Print((uint)hr);
////}
//
//void SleepDoEvents(int milliseconds)
//{
//	if(milliseconds == 0) { DoEvents(); return; }
//	for(;;) {
//		ULONGLONG t = 0;
//		int timeSlice = 100; //we call API in loop with small timeout to make it respond to Thread.Abort
//		if(milliseconds > 0) {
//			if(milliseconds < timeSlice) timeSlice = milliseconds;
//			t = GetTickCount64();
//		}
//
//		DWORD k = MsgWaitForMultipleObjectsEx(0, null, timeSlice, QS_ALLINPUT, MWMO_ALERTABLE);
//		//info: k can be 0 (message etc), WAIT_TIMEOUT, WAIT_IO_COMPLETION, WAIT_FAILED.
//		if(k == WAIT_FAILED) return; //unlikely, because not using handles
//		if(k == 0) DoEvents();
//
//		if(milliseconds > 0) {
//			milliseconds -= (int)(GetTickCount64() - t);
//			if(milliseconds <= 0) break;
//		}
//	}
//}
