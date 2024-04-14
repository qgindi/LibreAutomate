//This file is #included in all cpp files through Cpp.h.

#pragma once
#include "stdafx.h"


//Internal flags used by 'find AO' functions.
enum class eAF2 {
	//these are from role prefix
	InWebPage = 1, //"web:", "firefox:" or "chrome:"
	InFirefoxPage = 2, //"firefox:", or "web:" and detected Firefox
	InChromePage = 4, //"chrome:", or "web:" and detected Chrome
	InIES = 8, //"web:", detected IE web browser control

	NotInProc = 0x100, //from eAF::NotInProc
	FindAll = 0x200,

	//these are from prop parameter
	InControls = 0x40, //"class=x" or "id=x"
	IsRectL = 0x1000,
	IsRectT = 0x2000,
	IsRectW = 0x4000,
	IsRectH = 0x8000,
	IsRect = 0xF000,
	IsElem = 0x10000,
	IsId = 0x20000, //"id=x", where x is a number

	InFirefoxNotWebNotUIA = 0x100000,
};
ENABLE_BITMASK_OPERATORS(eAF2);

struct RECTWH { long L; long T; long W; long H; };

//AccFindCallback return type.
enum class eAccFindCallbackResult { Continue, StopFound, StopNotFound };

//Type of callback functor that receives results of the AO finder.
using AccFindCallback = const std::function <eAccFindCallbackResult(Cpp_Acc a)>;

//STR name and str::Wildex value.
struct NameValue {
	STR name;
	str::Wildex value;
};

const STR c_IES = L"Internet Explorer_Server";
const STR c_CRW = L"Chrome_RenderWidgetHostHWND";

enum class eSpecWnd { None, Chrome, Java, OO, Mozilla, ChromeControl, ChromeControl2 };

//wParam of the injection message.
//Also MarshalParams_Header::magic.
const long c_magic = -1'572'289'143;

//MarshalParams_Header::action. Action for the get_accHelpTopic hook function.
enum InProcAction : char {
#ifdef _DEBUG
	IPA_AccTest,
#endif
	IPA_AccFind = 1,
	IPA_AccFromWindow,
	IPA_AccFromPoint,
	IPA_AccFocused,
	IPA_AccNavigate,
	IPA_AccGetProps,
	IPA_AccGetWindow,
	IPA_AccGetHtml,
	IPA_AccEnableChrome,

	IPA_ShellExec = 100,
};

//Common fields of parameters-marshaling structures.
struct MarshalParams_Header {
	int magic;
	InProcAction action;
	eAccMiscFlags miscFlags;
};

//Used for marshaling parameters of Cpp_AccFromWindow when calling the get_accHelpTopic hook function.
struct MarshalParams_AccFromWindow {
	MarshalParams_Header hdr;
	int hwnd; //not HWND, because it must be of same size in 32 and 64 bit process
	DWORD objid;
	DWORD flags; //2 - get name instead of AO
};

//Used for marshaling parameters of Cpp_AccFromPoint when calling the get_accHelpTopic hook function.
struct MarshalParams_AccFromPoint {
	MarshalParams_Header hdr;
	POINT p;
	eXYFlags flags;
	eSpecWnd specWnd;
	int wFP;
};

//Used for marshaling parameters of Cpp_AccFocused when calling the get_accHelpTopic hook function.
struct MarshalParams_AccFocused {
	MarshalParams_Header hdr;
	int hwnd;
	eFocusedFlags flags;
};

//Used for marshaling parameters of Cpp_AccNavigate, Cpp_AccGetProps, etc when calling the get_accHelpTopic hook function.
struct MarshalParams_AccElem {
	MarshalParams_Header hdr;
	long elem;
};

//Used for marshaling 1-4 int parameters when calling the get_accHelpTopic hook function.
struct MarshalParams_AccInt4 {
	MarshalParams_Header hdr;
	int i0, i1, i2, i3;
};

namespace outproc {
	HRESULT InjectDllAndGetAgent(HWND w, out IAccessible*& iaccAgent, out HWND* wAgent = null);

	//Calls the hooked get_accHelpTopic in the target process.
	//Packs some parameters, unpacks the returned data.
	class InProcCall {
		IAccessible* _a;
		_variant_t _vParams;
		Bstr _br;
		Smart<IStream> _stream;
		DWORD _resultSize;
	public:
		//Allocates memory to pass parameters.
		//Writes MarshalParams_Header fields. Then let the caller cast the return value to MarshalParams_AccFind* etc and write other fields.
		MarshalParams_Header* AllocParams(Cpp_Acc* a, InProcAction action, size_t size) {
			_a = a->acc;
			_vParams.bstrVal = SysAllocStringByteLen(null, (UINT)size);
			_vParams.vt = VT_BSTR;
			auto h = (MarshalParams_Header*)_vParams.bstrVal;
			h->magic = c_magic;
			h->action = action;
			h->miscFlags = a->misc.flags;
			return h;
		}

		//Allocates memory to pass parameters.
		//Writes MarshalParams_Header fields. Then let the caller cast the return value to MarshalParams_AccFind* etc and write other fields.
		MarshalParams_Header* AllocParams(IAccessible* iacc, InProcAction action, size_t size) {
			Cpp_Acc a(iacc, 0);
			return AllocParams(&a, action, size);
		}

		//Calls the hooked get_accHelpTopic in the target process.
		//Returns 0 if successful. Else returns (HRESULT)eError::X (>0x1000), or a standard COM error code, eg exception, disconnected, etc.
		HRESULT Call() {
			long magic = 0;
			HRESULT hr = _a->get_accHelpTopic(&_br, _vParams, &magic);
			if (magic != c_magic) {
				//possible reasons:
				//	not hooked.
				//	exception in hook. Then magic is rejected.
				//	RPC failed, eg the object is disconnected.
				switch (hr) {
				case E_NOTIMPL: case DISP_E_MEMBERNOTFOUND: case E_INVALIDARG: case S_FALSE: case 0: //guess
					hr = E_NOINTERFACE;
					break;
				}
			}
			return hr;
		}

		HRESULT ReadResultAcc(ref Cpp_Acc& a, bool dontNeedAO = false);

		BSTR DetachResultBSTR() {
			return _br.Detach();
		}

		BSTR GetResultBSTR() {
			return _br;
		}
	};
} //namespace outproc

namespace inproc {
	HRESULT STDMETHODCALLTYPE Hook_get_accHelpTopic(IAccessible* iacc, out BSTR& sResult, VARIANT vParams, long* pMagic);
	bool AccDisconnectWrappers();

	//Sets and restores get_accHelpTopic hook for all IAccessible interface tables.
	//Usually there are 1 or 2 interface tables in a process, but eg Firefox with multiple tabs can have ~10.
	class HookIAccessible {
		struct _RESTORE {
			LPVOID* place; //address of the function pointer in the interface table
			LPVOID oldFunc;
		};

		CSimpleArray<_RESTORE> _a;

		//place - address of the function pointer in the interface table.
		//func - the hook function or the old function (to unhook).
		bool _ReplaceFunctionInTable(LPVOID* place, LPVOID func) {
			DWORD oldProt = 0;
			BOOL vpOK = VirtualProtect(place, sizeof(LPVOID), PAGE_EXECUTE_READWRITE, &oldProt);
			assert(vpOK); if (!vpOK) return false;
			*place = func;
			if (oldProt != PAGE_EXECUTE_READWRITE) VirtualProtect(place, sizeof(LPVOID), oldProt, &oldProt);
			return true;
		}

	public:
		bool Hook(IAccessible* iacc) {
			LPVOID* place = *(LPVOID**)iacc + 16; //&get_accHelpTopic
			LPVOID func = *place; //get_accHelpTopic
			if (func != Hook_get_accHelpTopic) {
				//Printf(L"%p", func);
				static CComAutoCriticalSection _cs; CComCritSecLock<CComAutoCriticalSection> lock(_cs);
				for (int i = 0, n = _a.GetSize(); i < n; i++) if (_a[i].place == place) return true; //some unknown hook may be installed after ours
				if (!_ReplaceFunctionInTable(place, Hook_get_accHelpTopic)) return false;
				_RESTORE r = { place, func };
				_a.Add(r);
			}
			return true;
		}

		~HookIAccessible() {
			for (int i = _a.GetSize() - 1; i >= 0; i--) {
				_RESTORE r = _a[i];
				bool restored = _ReplaceFunctionInTable(r.place, r.oldFunc);
				//Printf(L"restored=%i, func=%p", restored, r.oldFunc);
			}
		}
	};
	extern HookIAccessible s_hookIAcc;

}

enum class eWinFlags {
	AccEnabled = 1,
	AccJavaYes = 8,
	AccJavaNo = 16,
};
ENABLE_BITMASK_OPERATORS(eWinFlags);

class WinFlags {
	CComAutoCriticalSection _cs;
	ATOM _atom;

	WinFlags(int u) {
		_atom = GlobalFindAtomW(L"PuGVNJS5Ck2lc5K/DYEUwg");
		if (_atom == 0) _atom = GlobalAddAtomW(L"PuGVNJS5Ck2lc5K/DYEUwg");
	}
	//~WinFlags() { GlobalDeleteAtom(_atom); } //don't. Deletes even if currently used by a window prop, making the prop useless.
	//FUTURE: can be simplified, because now don't need dtor

	static WinFlags& _Inst() {
		static WinFlags s_inst(0);
		return s_inst;
	}

public:
	static eWinFlags Get(HWND w) {
		return (eWinFlags)(LPARAM)GetPropW(w, (LPCWSTR)_Inst()._atom);
	}

	//Removes removeFlags flags and adds addFlags.
	static void Set(HWND w, eWinFlags addFlags, eWinFlags removeFlags = (eWinFlags)0) {
		auto inst = _Inst();
		CComCritSecLock<CComAutoCriticalSection> lock(inst._cs);
		eWinFlags t = Get(w), t0 = t;
		t &= ~removeFlags;
		t |= addFlags;
		if (t != t0) SetPropW(w, (LPCWSTR)inst._atom, (HANDLE)(LPARAM)t);
	}
};
