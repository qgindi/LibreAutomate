#include "stdafx.h"
#include "cpp.h"

namespace other {
	EXPORT HMODULE Cpp_ModuleHandle() {
		return s_moduleHandle;
	}

	LRESULT CALLBACK ClipboardHook(int code, WPARAM wParam, LPARAM lParam) {
		auto m = (MSG*)lParam;
		if (code < 0) goto g1;
		if (m->message == WM_CLIPBOARDUPDATE) {
			//Print("WM_CLIPBOARDUPDATE");
			char cn[256];
			if (0 == GetClassNameA(m->hwnd, cn, sizeof(cn)) || 0 != strcmp(cn, "Au.DWP")) {
				m->message = 0;
				//Print(cn);
			}
			return 0;
		}
	g1:
		return CallNextHookEx(0, code, wParam, lParam);

		//After unhooking, this dll remains loaded until hooked threads receive messages.
		//	To unload when [un]installing, installer calls Cpp_Unload which broadcasts messages to all top-level and message-only windows.
		//	To unload when building, Cpp project's pre-link event runs "BuildEvents.exe" (created from C# script) which calls Cpp_Unload.
	}

	EXPORT HHOOK Cpp_Clipboard(HHOOK hh) {
		if (hh == NULL) {
			auto hh = SetWindowsHookExW(WH_GETMESSAGE, ClipboardHook, s_moduleHandle, 0);
			return hh;
		} else {
			UnhookWindowsHookEx(hh);
		}
		return NULL;
	}

	//enum DDEvent { Enter, Over, Drop, Leave }
	EXPORT HRESULT Cpp_CallIDroptarget(IDropTarget* dt, int ddEvent, IDataObject* d, DWORD keyState, POINTL pt, DWORD* pdwEffect) {
		switch (ddEvent) {
		case 0: return dt->DragEnter(d, keyState, pt, pdwEffect);
		case 1: return dt->DragOver(keyState, pt, pdwEffect);
		case 2: return dt->Drop(d, keyState, pt, pdwEffect);
		default: return dt->DragLeave();
		}
	}

	//auto-restore the unhandled exception filter used by .NET for UnhandledException event.
	//	Some API replace or remove it. Then UnhandledException does not work.
	//	Known bad API:
	//		Common file dialog API and their wrappers. Fixed on Win11.
	//		WPF on Win11. Eg Panel.Children.Add. Not on Win10 (same .NET version).
	//	This code could be in C#, but then on exception "Unknown hard error" (not in all cases).

	static LPTOP_LEVEL_EXCEPTION_FILTER s_ueh;
	static PVOID s_veh;

	static long __stdcall _Veh(_EXCEPTION_POINTERS* ExceptionInfo) {
		SetUnhandledExceptionFilter(s_ueh);
		return 0;
	}

	EXPORT void Cpp_UEF(BOOL on) {
		if (on) { //called from AppModuleInit_
			SetUnhandledExceptionFilter(s_ueh = SetUnhandledExceptionFilter(0)); //get current UEF
			s_veh = AddVectoredExceptionHandler(1, _Veh); //restore on every exception, handled or not
		} else if (s_veh) { //called on process exit
			SetUnhandledExceptionFilter(s_ueh);
			RemoveVectoredExceptionHandler(s_veh);
			s_veh = null;
		}
	}

	static HWINEVENTHOOK s_iww_hook;

	void __stdcall _IWW_Hook(HWINEVENTHOOK hWinEventHook, DWORD event, HWND hwnd, LONG idObject, LONG idChild, DWORD idEventThread, DWORD dwmsEventTime) {
		if (!(wn::ExStyle(hwnd) & WS_EX_NOACTIVATE)) {
			//wn::PrintWnd(hwnd);
			//Print(hwnd == GetForegroundWindow());

			bool isActive = hwnd == GetForegroundWindow(), activate = !isActive && hwnd == GetActiveWindow();
			if (isActive || activate) {
				UnhookWinEvent(hWinEventHook);
				s_iww_hook = 0;
				if (activate) SetForegroundWindow(hwnd);
			}
		}
	}

	//Workaround for: miniProgram window usually is inactive (eg MessageBox), and may be even under other windows (WPF).
	//	It is because of task process preloading. OS does not activate the first window of an old process.
	//	Workaround: set EVENT_SYSTEM_FOREGROUND hook for this process. It runs whenever a window wants to be active,
	//		even if OS does not activate it. The hook activates the first window and uninstalls self.
	//		It must run in the window's thread (because uses GetActiveWindow), therefore need WINEVENT_INCONTEXT+hmodule and cannot be in C#.
	EXPORT void Cpp_InactiveWindowWorkaround(BOOL on) {
		if (on) {
			s_iww_hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, (HMODULE)&__ImageBase, _IWW_Hook, GetCurrentProcessId(), 0, WINEVENT_INCONTEXT);
		} else if (s_iww_hook) {
			UnhookWinEvent(s_iww_hook);
			s_iww_hook = 0;
		}
	}
}

namespace {
	//Used for marshaling Cpp_ShellExec (IPA_ShellExec) parameters when calling the get_accHelpTopic hook function.
	//A flat variable-size memory structure (strings follow the fixed-size part).
	struct MarshalParams_ShellExec {
		MarshalParams_Header hdr;
	private:
		int _file, _dir, _verb, _params, _class, _idlist; //offsets
		int _nshow, _hwnd;
		ULONG _mask;

		LPWSTR _SetString(STR s, LPWSTR dest, out int& offset) {
			if (!s) {
				offset = 0;
				return dest;
			}
			int len = (int)wcslen(s);
			memcpy(dest, s, len * 2); dest[len] = 0;
			offset = (int)(dest - (STR)this);
			return dest + len + 1;
		}

		STR _GetString(int offset) {
			if (!offset) return null;
			return (STR)this + offset;
		}

		void _SetIL(void* il, LPWSTR dest, out int& offset) {
			if (!il) {
				offset = 0;
				return;
			}
			int size = ILGetSize((LPCITEMIDLIST)il);
			memcpy(dest, il, size);
			offset = (int)(dest - (STR)this);
		}

		void* _GetIL(int offset) {
			if (!offset) return null;
			return (LPWSTR)this + offset;
		}
	public:
		static int _Size(STR s) {
			return s == null ? 0 : ((int)wcslen(s) + 1) * 2;
		}
		static int _SizeIL(void* idlist) {
			return idlist == null ? 0 : ILGetSize((LPCITEMIDLIST)idlist) + 1 & ~1;
		}

		static int CalcMemSize(const SHELLEXECUTEINFO& x) {
			return sizeof(MarshalParams_ShellExec) + _SizeIL(x.lpIDList) + _Size(x.lpFile) + _Size(x.lpDirectory) + _Size(x.lpParameters) + _Size(x.lpVerb);
		}

#pragma warning(disable: 4302 4311 4312) //conversion HWND <-> int
		void Marshal(const SHELLEXECUTEINFO& x) {
			_mask = x.fMask;
			_nshow = x.nShow;
			_hwnd = (int)x.hwnd;
			auto s = (LPWSTR)(this + 1);
			s = _SetString(x.lpFile, s, out _file);
			s = _SetString(x.lpDirectory, s, out _dir);
			s = _SetString(x.lpVerb, s, out _verb);
			s = _SetString(x.lpParameters, s, out _params);
			s = _SetString(x.lpClass, s, out _class);
			_SetIL(x.lpIDList, s, out _idlist);

			//never mind: the new process does not inherit environment variables.
			//	I don't know how to pass them when using shell prcess. Canot modify its environment variables, even temporarily.
			//	It is documented.
		}

		SHELLEXECUTEINFO Unmarshal() {
			SHELLEXECUTEINFO x = { sizeof(SHELLEXECUTEINFO), _mask };
			x.nShow = _nshow;
			x.hwnd = (HWND)_hwnd;
			x.lpFile = _GetString(_file);
			x.lpDirectory = _GetString(_dir);
			x.lpVerb = _GetString(_verb);
			x.lpParameters = _GetString(_params);
			x.lpClass = _GetString(_class);
			x.lpIDList = _GetIL(_idlist);
			return x;
		}
	};
}

namespace inproc {
	HRESULT ShellExec(MarshalParams_Header* h, out BSTR& sResult) {
		sResult = null;
		auto m = (MarshalParams_ShellExec*)h;
		auto x = m->Unmarshal();
		if (!ShellExecuteExW(&x)) return GetLastError();
		if (x.hProcess) {
			DWORD pid = GetProcessId(x.hProcess);
			CloseHandle(x.hProcess);
			if (pid != 0) sResult = SysAllocStringByteLen((LPCSTR)&pid, 4);
		}
		return 0;
	}
}

namespace other {
	EXPORT bool Cpp_ShellExec(const SHELLEXECUTEINFO& x, out DWORD& pid, out HRESULT& injectError, out HRESULT& execError) {
		pid = 0; injectError = 0; execError = 0;
		Cpp_Acc_Agent aAgent;
		if (0 != (injectError = outproc::InjectDllAndGetAgent(GetShellWindow(), out aAgent.acc))) {
			return false;
		}

		outproc::InProcCall ic;
		auto p = (MarshalParams_ShellExec*)ic.AllocParams(&aAgent, InProcAction::IPA_ShellExec, MarshalParams_ShellExec::CalcMemSize(x));
		p->Marshal(x);
		if (0 != (execError = ic.Call())) return false;

		BSTR b = ic.GetResultBSTR();
		if (b) pid = *(DWORD*)b;

		return true;
	}

	//Unloads this dll (AuCpp.dll) from other processes.
	//flags: 1 wait less.
	EXPORT void Cpp_Unload(DWORD flags = 0) {
		int less = flags & 1 ? 5 : 1;
		DWORD_PTR res;
		std::vector<HWND> a;

		//close acc agent windows
		for (HWND w = 0; w = FindWindowExW(HWND_MESSAGE, w, L"AuCpp_IPA_1", nullptr); ) a.push_back(w);
		int n = (int)a.size();
		if (n > 0) {
			for (int i = 0; i < n; i++) SendMessageTimeout(a[i], WM_CLOSE, 0, 0, SMTO_ABORTIFHUNG, 5000 / less, &res);
			a.clear();
			Sleep(n * 50);
		}

		//unload from processes where loaded by the clipboard hook
		SendMessageTimeout(HWND_BROADCAST, 0, 0, 0, SMTO_ABORTIFHUNG, 1000 / less, &res);
		for (HWND w = 0; w = FindWindowExW(HWND_MESSAGE, w, nullptr, nullptr); ) a.push_back(w);
		for (int i = 0; i < (int)a.size(); i++) SendMessageTimeout(a[i], 0, 0, 0, SMTO_ABORTIFHUNG, 1000 / less, &res);
		Sleep(500 / less);
	}

	//for rundll32.exe
	EXPORT void WINAPI UnloadAuCppDll(DWORD flags = 0) {
		Cpp_Unload(flags);
	}

} //namespace other
