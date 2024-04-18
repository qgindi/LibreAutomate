#include "stdafx.h"
#include "cpp.h"
#include "acc.h"
#include "JAB.h"

//Tested Java Swing apps:
//	Java Control Panel. Installed with Java.
//	Android Studio. Large.
//	PyCharm. Similar to Android Studio.
//	NetBeans.
//	Sweet Home 3D. Has a child control with HWND. Tested 32-bit with the ini workaround; failed to start 64-bit.
//	muCommander. Its listviews are detached from tree and cannot be found, but "from point" works.
//	RuneLite. Has 2 child controls.
//	FreeMind, JDiskReport, Art Of Illusion. Nothing special.
//High DPI: Most are system-aware. PyCharm and muCommander are PM-aware, but need scaling anyway. NetBeans marks itself as DPI-aware but is unscaled.
//Also would like to test Matlab and some Oracle apps, but failed to download. And not freeware.
//More can be found here: https://wiki.archlinux.org/title/list_of_applications

//Tested JavaFX apps: BlueJ and two other. For JavaFX need UIA, not JAB. MSAA does not work.

namespace jab
{
_JApi s_api;

struct _DPI_INFO { int wX, wY, jX, jY; double f; };
bool _GetDpiInfo(out _DPI_INFO& d, HWND w, long vmID, JObject frame);

#ifdef _DEBUG
void _PrintElem(long vmID, JObject j, STR prefix = L"") {
	AccessibleContextInfo c;
	if(j == 0) Print("j 0");
	else if(s_api.getAccessibleContextInfo(vmID, j, &c)) {
		HWND w = s_api.getHWNDFromAccessibleContext(vmID, j);
		Printf(L"%s%s  {%i %i %i %i}  cc=%i  hwnd=%i", prefix, c.role_en_US, c.x, c.y, c.width, c.height, c.childrenCount, (int)(LPARAM)w);
	} else Print("getAccessibleContextInfo failed");
}

void _PrintElemAndAncestors(long vmID, JObject j) {
	for(auto v = j; v;) {
		_PrintElem(vmID, v);
		HWND h = s_api.getHWNDFromAccessibleContext(vmID, v);
		if(h) wn::PrintWnd(h);
		auto t = v; v = s_api.getAccessibleParentFromContext(vmID, v);
		if(t != j) s_api.releaseJavaObject(vmID, t);
	}
}
#endif

class JAccessible : public IAccessible
{
	long _cRef;
	long _vmID;
	JObject _jo;
	HWND _hwnd; //need this field not only for speed, but also because getHWNDFromAccessibleContext in some cases cannot be used, for example when this object or an ancestor is disconnected from the tree

public:
#pragma region ctor, dtor
	JAccessible(JObject jo, int vmID, HWND hwnd)
	{
		//PRINTS(L"JAccessible");
		_cRef = 1;
		_jo = jo; _vmID = vmID; _hwnd = hwnd;
	}

	~JAccessible()
	{
		//PRINTS(L"~JAccessible");
		s_api.releaseJavaObject(_vmID, _jo);
		_ReleaseObjectInfoCache(); //a new variable may be allocated in the same place soon
	}
#pragma endregion

#pragma region IDispatch
	STDMETHODIMP QueryInterface(REFIID iid, void** ppv)
	{
		if(iid == IID_IAccessible || iid == IID_IDispatch || iid == IID_IUnknown) {
			InterlockedIncrement(&_cRef);
			*ppv = this;
			return 0;
		}
		*ppv = 0;
		return E_NOINTERFACE;
	}

	STDMETHODIMP_(ULONG) AddRef()
	{
		return InterlockedIncrement(&_cRef);
	}

	STDMETHODIMP_(ULONG) Release()
	{
		long ret = InterlockedDecrement(&_cRef);
		if(!ret) delete this;
		return ret;
	}

	STDMETHODIMP GetTypeInfoCount(UINT* pctinfo) { return E_NOTIMPL; }

	STDMETHODIMP GetTypeInfo(UINT iTInfo, LCID lcid, ITypeInfo** ppTInfo) { return E_NOTIMPL; }

	STDMETHODIMP GetIDsOfNames(REFIID riid, LPOLESTR* rgszNames, UINT cNames, LCID lcid, __RPC__out_ecount_full(cNames) DISPID* rgDispId) { return E_NOTIMPL; }

	STDMETHODIMP Invoke(DISPID dispIdMember, REFIID riid, LCID lcid, WORD wFlags, DISPPARAMS* pDispParams, VARIANT* pVarResult, EXCEPINFO* pExcepInfo, UINT* puArgErr) { return E_NOTIMPL; }
#pragma endregion

#pragma region IAccessible
	STDMETHODIMP get_accParent(IDispatch** ppdispParent)
	{
		//PRINTS(__FUNCTIONW__);
		*ppdispParent = null;

		//For owned dialogs etc GetAccessibleParentFromContext(dialog) gets a half-valid object of the owner window.
		//Instead get standard WINDOW object. May need it for WindowFromAccessibleObject.
		auto w = s_api.getHWNDFromAccessibleContext(_vmID, _jo);
		if(w) {
			return AccessibleObjectFromWindow(w, OBJID_WINDOW, IID_IAccessible, (void**)ppdispParent);
			//never mind: if control...
		}

		auto jo = s_api.getAccessibleParentFromContext(_vmID, _jo); if(!jo) return 1;
		*ppdispParent = new JAccessible(jo, _vmID, _hwnd); //never mind: may need to update hwnd. Cannot get it reliably.
		return 0;
	}

	STDMETHODIMP get_accChildCount(long* pcountChildren)
	{
		//PRINTS(__FUNCTIONW__);
		auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
		*pcountChildren = k->childrenCount;
		return 0;
	}

	STDMETHODIMP get_accChild(VARIANT varChild, IDispatch** ppdispChild)
	{
		//PRINTS(__FUNCTIONW__);
		int i = varChild.lVal - 1;
		if(varChild.vt != VT_I4 || i < 0) return E_INVALIDARG;
		auto jo = s_api.getAccessibleChildFromContext(_vmID, _jo, i);
		if(jo == 0) {
			auto k = _GetObjectInfo();
			if(k != null && i >= k->childrenCount) return E_INVALIDARG;
			return E_FAIL;
		}
		*ppdispChild = new JAccessible(jo, _vmID, _hwnd); //can use same hwnd, because the tree does not have controls
		return 0;
	}

	STDMETHODIMP get_accName(VARIANT varChild, out BSTR* pszName)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		WCHAR m[MAX_STRING_SIZE];
		if(!s_api.getVirtualAccessibleName(_vmID, _jo, m, MAX_STRING_SIZE)) return E_FAIL;
		*pszName = SysAllocString(m);
		return 0;

		//getVirtualAccessibleName is better than getAccessibleContextInfo (GACI).
		//It gets names of toolbar buttons. With GACI, name empty, we can use description.
		//It gets name of related control - adjacent label etc.
		//Much faster than GACI.
		//Makes searching ~10% slower when GACI is cached, but not when role used (not called if role does not match).
	}

	STDMETHODIMP get_accValue(VARIANT varChild, out BSTR* pszValue)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
		*pszValue = null;
		if(k->accessibleValue) {
			WCHAR m[MAX_STRING_SIZE];
			if(s_api.getCurrentAccessibleValueFromContext(_vmID, _jo, m, MAX_STRING_SIZE)) //speed: 20 times faster than getAccessibleContextInfo
				*pszValue = SysAllocString(m);
		} else {
			AccessibleTextInfo ati;
			if(k->accessibleText && s_api.getAccessibleTextInfo(_vmID, _jo, &ati, 0, 0)) { //slow
				auto b = SysAllocStringLen(null, ati.charCount);
				if(ati.charCount == 0 || s_api.getAccessibleTextRange(_vmID, _jo, 0, ati.charCount - 1, b, ati.charCount)) *pszValue = b;
				else SysFreeString(b);
			}
		}
		return *pszValue == null ? 1 : 0;

		//tested: most objects don't have value/text. Value usually is numeric, eg "1" for checked checkbox. Text objects don't have value, therefore we get text instead.
		//AccessibleContextInfo::accessibleInterfaces flags, from AccessibleBridgePackages.h (cAccessibleValueInterface etc):
		//	1 value, 2 actions, 4 component, 8 selection, 0x10 table, 0x20 text, 0x40 hypertext.
	}

	STDMETHODIMP get_accDescription(VARIANT varChild, out BSTR* pszDescription)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
		*pszDescription = k->description.Copy();
		return 0;
	}

	STDMETHODIMP get_accRole(VARIANT varChild, out VARIANT* pvarRole)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
		return k->role.CopyTo(pvarRole);
	}

	STDMETHODIMP get_accState(VARIANT varChild, out VARIANT* pvarState)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
		pvarState->vt = VT_I4;
		pvarState->lVal = k->state;
		return 0;
	}

	STDMETHODIMP get_accHelp(VARIANT varChild, out BSTR* pszHelp) { return E_NOTIMPL; }

	STDMETHODIMP get_accHelpTopic(BSTR* pszHelpFile, VARIANT varChild, long* pidTopic) { return E_NOTIMPL; }

	STDMETHODIMP get_accKeyboardShortcut(VARIANT varChild, out BSTR* pszKeyboardShortcut) { return E_NOTIMPL; }

	STDMETHODIMP get_accFocus(out VARIANT* pvarChild) { return E_NOTIMPL; }
	//Difficult to implement so that would work correctly, ie return direct child even if focused is another descendant.
	//getAccessibleContextWithFocus is unstable. More often does not work than works.
	//Rarely used.

	STDMETHODIMP get_accSelection(out VARIANT* pvarChildren) { return E_NOTIMPL; }
	//Rarely used.

	STDMETHODIMP get_accDefaultAction(VARIANT varChild, out BSTR* pszDefaultAction)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		return _get_accDefaultAction(out * pszDefaultAction, true);
	}

	HRESULT _get_accDefaultAction(out BSTR& b, bool getList)
	{
		b = null;
		auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
		if(!k->accessibleAction) return 1;
		std::unique_ptr<AccessibleActions> actions(new(AccessibleActions)); //130 KB
		HRESULT hr = 1; int n;
		if(s_api.getAccessibleActions(_vmID, _jo, actions.get()) && (n = actions->actionsCount) > 0 && n <= MAX_ACTION_INFO) {
			hr = 0;
			if(!getList || n == 1) b = SysAllocString(actions->actionInfo[0].name);
			else {
				str::StringBuilder t;
				for(int i = 0; i < n; i++) {
					if(i) t << L", ";
					t << actions->actionInfo[i].name;
				}
				b = t.ToBSTR();
			}
		}
		return hr;
	}

	STDMETHODIMP accSelect(long flagsSelect, VARIANT varChild)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		if(flagsSelect & SELFLAG_EXTENDSELECTION) return E_INVALIDARG;
		if(flagsSelect & (SELFLAG_TAKESELECTION | SELFLAG_ADDSELECTION | SELFLAG_REMOVESELECTION)) {
			auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
			auto ap = s_api.getAccessibleParentFromContext(_vmID, _jo); if(ap == 0) return E_FAIL;
			_ReleaseObjectInfoCache(); //the action changes object state
			int i = k->indexInParent;
			if(flagsSelect & SELFLAG_TAKESELECTION) s_api.clearAccessibleSelectionFromContext(_vmID, ap);
			if(flagsSelect & (SELFLAG_TAKESELECTION | SELFLAG_ADDSELECTION)) s_api.addAccessibleSelectionFromContext(_vmID, ap, i);
			if(flagsSelect & SELFLAG_REMOVESELECTION) s_api.removeAccessibleSelectionFromContext(_vmID, ap, i);
			s_api.releaseJavaObject(_vmID, ap);
		}
		if(flagsSelect & SELFLAG_TAKEFOCUS) {
			_ReleaseObjectInfoCache(); //the action changes object state
			if(!s_api.requestFocus(_vmID, _jo)) return E_FAIL;
		}
		return 0;
	}

	STDMETHODIMP accLocation(out long* pxLeft, out long* pyTop, out long* pcxWidth, out long* pcyHeight, VARIANT varChild)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		auto k = _GetObjectInfo(); if(k == null) return E_FAIL;
		*pxLeft = k->x; *pyTop = k->y; *pcxWidth = k->width; *pcyHeight = k->height;

		//DPI-scale if need. Without it this func is useless when high DPI.
		//	In all tested apps the API gets logical coord. The physical view is scaled either by OS or by Java. In some not scaled (NetBeans).
		//	Makes >2 times slower, eg 4 -> 9 ms. Cannot use any faster ways, because apps use different scaling (offsets etc).
		long vmID; AccessibleContext jw;
		if(s_api.getAccessibleContextFromHWND(_hwnd, &vmID, &jw)) { //note: getTopLevelObject faster, but in some cases gets wrong object
			_DPI_INFO d;
			if(_GetDpiInfo(out d, _hwnd, vmID, jw)) {
				*pxLeft = d.wX + (int)round((*pxLeft - d.jX) * d.f);
				*pyTop = d.wY + (int)round((*pyTop - d.jY) * d.f);
				*pcxWidth = (int)round(*pcxWidth * d.f);
				*pcyHeight = (int)round(*pcyHeight * d.f);
			}
			s_api.releaseJavaObject(vmID, jw);
		}

		return 0;
	}

	STDMETHODIMP accNavigate(long navDir, VARIANT varStart, out VARIANT* pvarEndUpAt)
	{
		//Print("accNavigate", navDir, varStart.vt, varStart.value);

		//WindowFromAccessibleObject (WFAO) at first calls this with an undocumented navDir 10.
		//	tested: accNavigate(10) for a standard Windows control returns VARIANT(VT_I4, hwnd).
		//	If we return window handle, WFAO does not call get_accParent. Else also calls it for each ancestor.
		if(navDir == 10 && varStart.vt == VT_I4) {
			pvarEndUpAt->vt = VT_I4;
			pvarEndUpAt->lVal = (int)(LPARAM)_hwnd;
			return 0;
		}

		if(navDir < NAVDIR_UP || navDir > NAVDIR_LASTCHILD) return E_INVALIDARG;
		if(_InvalidVarChildParam(ref varStart)) return E_INVALIDARG;
		JObject ac = 0;

		if(navDir >= NAVDIR_NEXT && navDir <= NAVDIR_LASTCHILD) {
			int iChild = -1; JObject ap = 0; _JObjectInfo* k = null;
			if(navDir != NAVDIR_FIRSTCHILD && null == (k = _GetObjectInfo())) return 1;
			switch(navDir) {
			case NAVDIR_FIRSTCHILD: iChild = 0; ap = _jo; break; //GetAccessibleChildFromContext will fail if this does not have children
			case NAVDIR_LASTCHILD: iChild = k->childrenCount - 1; ap = _jo; break;
			case NAVDIR_NEXT: iChild = k->indexInParent + 1; break; //GetAccessibleChildFromContext will fail if this is the last
			case NAVDIR_PREVIOUS: iChild = k->indexInParent - 1; break;
			}
			if(iChild >= 0) {
				if(ap == 0) ap = s_api.getAccessibleParentFromContext(_vmID, _jo);
				if(ap != 0) { ac = s_api.getAccessibleChildFromContext(_vmID, ap, iChild); if(ap != _jo) s_api.releaseJavaObject(_vmID, ap); }
			}
		}

		if(ac == 0) return 1;
		pvarEndUpAt->pdispVal = new JAccessible(ac, _vmID, _hwnd); //can use same hwnd, because the tree does not have controls
		pvarEndUpAt->vt = VT_DISPATCH;
		return 0;
	}

	STDMETHODIMP accHitTest(long xLeft, long yTop, out VARIANT* pvarChild) { return E_NOTIMPL; }
	//Rarely used.

	STDMETHODIMP accDoDefaultAction(VARIANT varChild)
	{
		BSTR name = null; int len = 0; Bstr name_;
		if(varChild.vt == VT_BSTR) {
			len = SysStringLen(name = varChild.bstrVal);
			if(len >= SHORT_STRING_SIZE) return E_INVALIDARG;
		} else if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;

		if(len == 0) {
			if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
			int hr = _get_accDefaultAction(out name, false);
			if(hr != 0) return hr == 1 ? DISP_E_MEMBERNOTFOUND : E_FAIL;
			name_.Attach(name);
			len = name_.Length();
		}
		_ReleaseObjectInfoCache(); //the action may change object props
		AccessibleActionsToDo atd; //16 KB
		atd.actionsCount = 1;
		memcpy(atd.actions, name, (len + 1) * 2);
		jint failure;
		if(!s_api.doAccessibleActions(_vmID, _jo, &atd, &failure)) {
			//PRINTI(failure); //0-based index of the first action that failed or is unknown, or -1 if not action-related
			return E_FAIL;
		}
		return 0;
	}

	STDMETHODIMP put_accName(VARIANT varChild, BSTR szName) { return E_NOTIMPL; }
	//Rarely used, deprecated.

	STDMETHODIMP put_accValue(VARIANT varChild, BSTR szValue)
	{
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		if(!s_api.setTextContents(_vmID, _jo, szValue)) return 1;
		_ReleaseObjectInfoCache();
		return 0;
	}
#pragma endregion

#pragma region get object info

	//s_api.getAccessibleContextInfo gets multiple properties (name, state, etc) that are then used by multiple JAccessible functions.
	//It is slow etc. We call it once and store the retrieved/converted properties in a thread-static variable t_joInfo. Not in JAccessible because big.
	//t_joInfo is used for all JAccessible instances, but owned by a single JAccessible instance at a time.

	//internal
	struct _JObjectInfo
	{
		Bstr /*name,*/ description, role;
		int indexInParent, childrenCount;
		int x, y, width, height;
		bool accessibleComponent, accessibleAction, accessibleSelection, accessibleText, accessibleValue;
		int state;
		LONGLONG time;
		JAccessible* owner;

		void Init(const AccessibleContextInfo& c)
		{
			//name = c.name;
			description = c.description;
			role = c.role_en_US;
			indexInParent = c.indexInParent; childrenCount = c.childrenCount;
			x = c.x; y = c.y; width = c.width; height = c.height;
			accessibleComponent = c.accessibleComponent != 0; accessibleAction = c.accessibleAction != 0; accessibleSelection = c.accessibleSelection != 0; accessibleText = c.accessibleText != 0;
			accessibleValue = (c.accessibleInterfaces & 1) != 0;

			state = STATE_SYSTEM_READONLY | STATE_SYSTEM_UNAVAILABLE | STATE_SYSTEM_INVISIBLE;
			for(STR s = c.states_en_US; *s;) {
				STR se = s; while(*se && *se != ',') se++;
				switch(str::Switch(s, (int)(se - s), { L"busy",L"checked",L"collapsed",L"editable",L"enabled",L"expanded",L"focusable",L"focused",L"indeterminate",L"modal",L"multiselectable",L"pressed",L"resizable",L"selectable",L"selected",L"showing",L"visible" })) {
				case 1: state |= STATE_SYSTEM_BUSY; break;
				case 2: state |= STATE_SYSTEM_CHECKED; break;
				case 3: state |= STATE_SYSTEM_COLLAPSED; break;
				case 4: state &= ~STATE_SYSTEM_READONLY; break;
				case 5: state &= ~STATE_SYSTEM_UNAVAILABLE; break;
				case 6: state |= STATE_SYSTEM_EXPANDED; break;
				case 7: state |= STATE_SYSTEM_FOCUSABLE; break;
				case 8: state |= STATE_SYSTEM_FOCUSED; break;
				case 9: state |= STATE_SYSTEM_INDETERMINATE; break;
				case 10: state |= STATE_SYSTEM_HASPOPUP; break;
				case 11: state |= STATE_SYSTEM_MULTISELECTABLE; break;
				case 12: state |= STATE_SYSTEM_PRESSED; break;
				case 13: state |= STATE_SYSTEM_SIZEABLE; break;
				case 14: state |= STATE_SYSTEM_SELECTABLE; break;
				case 15: state |= STATE_SYSTEM_SELECTED; break;
				case 16: state &= ~STATE_SYSTEM_INVISIBLE; break;
				case 17: if(width > 0 && height > 0) state &= ~STATE_SYSTEM_INVISIBLE; break;
				}
				if(*se == 0) break;
				s = se + 1;
			}
		}
	};

	static _JObjectInfo& __JoInfo() {
		thread_local static _JObjectInfo t_joInfo;
		return t_joInfo;
	}

	_JObjectInfo* _GetObjectInfo()
	{
		_JObjectInfo& k = __JoInfo();
		if(k.owner != this || GetTickCount64() - k.time > 10) {
			AccessibleContextInfo c;
			if(!s_api.getAccessibleContextInfo(_vmID, _jo, &c)) return null;
			k.Init(ref c);
			k.time = GetTickCount64();
			k.owner = this;
		}
		return &k;
	}

	bool _IsObjectInfoCached()
	{
		_JObjectInfo& k = __JoInfo();
		return k.owner == this && GetTickCount64() - k.time <= 10;
	}

	void _ReleaseObjectInfoCache()
	{
		_JObjectInfo& k = __JoInfo();
		if(k.owner == this) k.owner = null;
	}

#pragma endregion

#pragma region private
private:
	bool _InvalidVarChildParam(const VARIANT& v)
	{
		if(v.vt == 0) return false; //forgive
		return v.vt != VT_I4 || v.lVal != 0;
	}

	//HWND _GetHWND()
	//{
	//	if(_hwnd) return _hwnd;

	//	auto joTL = s_api.getTopLevelObject(_vmID, _jo);
	//	if(joTL != 0) {
	//		//_PrintElem(_vmID, joTL);

	//		HWND w = s_api.getHWNDFromAccessibleContext(_vmID, joTL);
	//		s_api.releaseJavaObject(_vmID, joTL);
	//		return _hwnd = w;
	//	}
	//	return 0;
	//}
#pragma endregion
};

int s_inited;

DWORD WINAPI _JabThread(LPVOID param)
{
	//How JAB works:
	//_InitJab loads JAB dll and functions, and calls Windows_run.
	//Windows_run creates a hidden dialog "Access Bridge status" and posts "AccessBridge-FromJava-Hello" messages to all top-level windows.
	//Java JAB-enabled processes also have hidden "Access Bridge status" dialogs. They run in a separate thread.
	//These dialogs, when received our hello message, post back the message to our dialog. wParam is poster dialog hwnd.
	//Only when our dialog receives these messages, we can get accessible context of that Java windows.
	//Therefore we must have a message loop etc. We use this thread for it.
	//Alternatively, each thread could call Windows_run and DoEvents etc, but then we would have multiple dialogs. Also would need DoEvents before each GetAccessibleContextFromHWND etc, or it would fail with Java windows created later.
	//Note: JAB events work only in thread that called Windows_run. Currently not using events. Not tested much. When tested with JavaFerret, most events either didn't work or used to stop working etc.

	UINT m1 = RegisterWindowMessageW(L"AccessBridge-FromWindows-Hello"); //received by our hidden "Access Bridge status" dialog once after _InitJab
	UINT m2 = RegisterWindowMessageW(L"AccessBridge-FromJava-Hello"); //received by the dialog from each JAB-enabled Java window
	ChangeWindowMessageFilter(m1, 1); ChangeWindowMessageFilter(m2, 1);

	s_api.Windows_run(); //the slowest part, ~10 ms when warm CPU, else 10-33 ms

	int i, nmsg; MSG msg;
	for(i = 0; i < 10; i++) {
		for(nmsg = 0; PeekMessageW(&msg, 0, 0, 0, PM_REMOVE); nmsg++) DispatchMessageW(&msg);
		if(nmsg == 0 && i > 0) break; //non-Java windows that use JAB (probably as client) don't post back the message
		Sleep(1);
	}
	PRINTF_IF(i > 2, L"i=%i", i); //test reliability in stress conditions. When testing, was 1 almost always, rarely 2, once >2.

	s_inited = 1;
	SetEvent(param);

	while(GetMessageW(&msg, 0, 0, 0) > 0) DispatchMessageW(&msg);
	s_inited = 0;

	return 0;
}

bool _InitJab()
{
	if(s_inited == 0) {
		static CComAutoCriticalSection _cs; CComCritSecLock<CComAutoCriticalSection> lock(_cs);
		if(s_inited == 0) {
			s_inited = -1;
			s_api.Load();
			if(s_api.IsLoaded()) {
				//FUTURE: Time.LibSleepPrecision.LibTempSet1(1);
				CHandle ev(CreateEventW(null, true, false, null));
				DWORD tid;
				HANDLE ht = CreateThread(null, 0, _JabThread, ev, 0, &tid);
				if(ht) {
					HANDLE ha[2] = { ev, ht };
					WaitForMultipleObjects(2, ha, false, INFINITE);
					CloseHandle(ht);
				}
			}
		}
	}
	return s_inited > 0;
}

bool _NoJab() { return s_inited < 0; }

//Returns true if w process has a JAB hidden dialog ("Access Bridge status", "#32770").
//It can be a Java window or a JAB client, but not a window of this process.
//speed: faster than AccessibleObjectFromWindow. Much faster than _JApi::isJavaWindow, and don't need to load dll etc.
//w class name must match "SunAwt*" (asserts).
bool _IsJavaWindow(HWND w)
{
	//if(wn::Style(w) & WS_CHILD) return false; //no, some Java windows have controls with similar classname
	assert(wn::ClassNameIs(w, L"SunAwt*") || !::IsWindow(w));

	auto wf = WinFlags::Get(w);
	if(!!(wf & eWinFlags::AccJavaYes)) return true;
	if(!!(wf & eWinFlags::AccJavaNo)) return false;

	bool yes = false; DWORD pid = 0, pidJAB = 0;
	for(HWND wJAB = 0; wJAB = wn::FindWndEx(0, wJAB, L"#32770", L"Access Bridge status");) {
		if(pid == 0 && !GetWindowThreadProcessId(w, &pid)) break;
		GetWindowThreadProcessId(wJAB, &pidJAB); if(pidJAB != pid) continue;
		yes = pid != GetCurrentProcessId();
		break;
	}

	WinFlags::Set(w, yes ? eWinFlags::AccJavaYes : eWinFlags::AccJavaNo);
	return yes;
}

JObject _JObjectFromWindow(HWND w, out long& vmID, bool getFocused = false)
{
	vmID = 0;
	if(!_IsJavaWindow(w)) return 0;
	if(!_InitJab()) return 0;
	JObject jo;
	auto f = getFocused ? s_api.getAccessibleContextWithFocus : s_api.getAccessibleContextFromHWND;
	if(!f(w, &vmID, &jo)) return 0;

	//In tested Java window with controls the API gets wrong object. Then cannot find etc. Should be its parent.
	//  This code fixes it.
	//  Note: don't use getTopLevelObject. It randomly gets the same object as now or the window's root frame (bad).
	if(!getFocused && (wn::Style(w) & WS_CHILD)) {
		AccessibleContextInfo ci;
		if(s_api.getAccessibleContextInfo(vmID, jo, &ci) && ci.childrenCount == 0) {
			auto pa = s_api.getAccessibleParentFromContext(vmID, jo);
			if(pa) {
				int x = ci.x, y = ci.y, wid = ci.width, hei = ci.height;
				if(s_api.getAccessibleContextInfo(vmID, pa, &ci) && ci.childrenCount > 0 && ci.x == x && ci.y == y && ci.width == wid && ci.height == hei) {
					auto bad = jo; jo = pa; s_api.releaseJavaObject(vmID, bad);
				} else s_api.releaseJavaObject(vmID, pa);
			}
		}
	}

	return jo;
}

IAccessible* AccFromWindow(HWND w, bool getFocused = false)
{
	if(_NoJab()) return null;
	long vmID;
	JObject jo = _JObjectFromWindow(w, out vmID, getFocused); if(jo == 0) return null;
	return new JAccessible(jo, vmID, w);
}

//Gets window rect info for DPI correction: physical and JAB-logical x, y and scaling factor.
//Returns false if fails or if don't need DPI correction.
bool _GetDpiInfo(out _DPI_INFO& d, HWND w, long vmID, JObject frame) {
	thread_local static struct _DPI_CACHE {
		HWND w;
		RECT rw;
		LONGLONG time;
		_DPI_INFO d;
	} t_cache; //use cache because getAccessibleContextInfo is slow

	RECT rw;
	if(GetWindowRect(w, &rw)) {
		if(t_cache.w == w && !memcmp(&t_cache.rw, &rw, 16) && GetTickCount64() - t_cache.time < 5000) {
			d = t_cache.d;
			return d.f != 1.0;
		}

		AccessibleContextInfo ci;
		if(s_api.getAccessibleContextInfo(vmID, frame, &ci)) {
			//Printf(L"{%i %i %i %i}  {%i %i %i %i}", ci.x, ci.y, ci.width, ci.height, rw.left, rw.top, rw.right-rw.left, rw.bottom-rw.top);
			d.wX = rw.left; d.wY = rw.top;
			d.jX = ci.x; d.jY = ci.y;
			int wK = (rw.right - rw.left) + (rw.bottom - rw.top), jK = ci.width + ci.height;
			bool scaled = jK != wK && jK >= 16 && wK >= 16;
			d.f = scaled ? round((double)wK / jK * 100.0) / 100.0 : 1.0; //eg 1.25123 to 1.25. Never mind very small windows where the error can be too big.

			t_cache.w = w;
			t_cache.rw = rw;
			t_cache.time = GetTickCount64();
			t_cache.d = d;

			return scaled;
		}
	}
	t_cache.w = 0;
	return false;
}

bool _IsPointInRect(POINT p, AccessibleContextInfo& c) {
	return p.y >= c.y && p.y < c.y + c.height && p.x >= c.x && p.x < c.x + c.width;
}

IAccessible* AccFromPoint(POINT p, HWND w)
{
	//bool test = 0 != (GetKeyState(VK_SCROLL) & 1);
	//Perf.First();
	if(_NoJab()) return null;

	long vmID = 0;
	JObject jo = 0, ap = _JObjectFromWindow(w, out vmID);
	if(ap == 0) return null;
	//Perf.Next('w');

	//DPI-unscale if need
	_DPI_INFO d;
	if(_GetDpiInfo(out d, w, vmID, ap)) {
		p.x = (int)round(d.jX + (p.x - d.wX) / d.f);
		p.y = (int)round(d.jY + (p.y - d.wY) / d.f);
		//never mind: not perfect. Sometimes 1-pixel error.
		//Print("DPI-unscaled");
	}
	//Perf.Next('d');

	//workaround for JAB bug: if ap is a popup menu, getAccessibleContextAt randomly returns an object from parent window.
	//  Especially when high DPI. Depends on mouse movements.
	//	Same with tooltips and other cn="SunAwtWindow" windows. Not with popup menus in cn="SunAwtFrame" windows.
	//	Cannot detect whether the object is good or bad. In both cases getTopLevelObject and getAccessibleParentFromContext skip ap.
	//	Therefore even don't try getAccessibleContextAt at first.
	if(wn::ClassNameIs(w, L"SunAwtWindow")) {
		std::function<AccessibleContext(AccessibleContext, int)> func;
		func = [&func, vmID, p](AccessibleContext ac, int level) -> AccessibleContext {
			AccessibleContextInfo c;
			if(!s_api.getAccessibleContextInfo(vmID, ac, &c)) return 0;
			bool notThis = false;
			if(!_IsPointInRect(p, c)) {
				if(c.childrenCount == 0 || wcscmp(c.role_en_US, L"page tab")) return 0;
				//Usually page tab's rect is just the button, and children are in the page below.
				//	Any object can have such children. Eg tree view. But enumerating all would be too slow.
				notThis = true;
			}
			if(!wcsstr(c.states_en_US, L"showing")) return 0; //s_api.getVisibleChildren checks this state
			//Printf(L"%i %i %s  states=%s", level, c.childrenCount, c.role_en_US, c.states_en_US);
			if(c.childrenCount > 0 && c.childrenCount <= 100 && wcscmp(c.role_en_US, L"menu")) {
				//Enumerate in reverse order, because:
				// 1. Else could find the first child of the root pane, eg in Android Studio Settings.
				// 2. Finds links in paragraphs.
				// The root pane usually has 2 children, and the first is an invisible zero-rect pane. But in some windows it has properties like visible.
				// Actually should be used algorithm "find smallest object containing p". This algorithm is "find first object containing p".
				// But that algorithm would be slower, because need to enum entire tree in all cases.
				// What is really important, this algorithm works well with all tested menus. Other windows not so important.
				//for(int i = 0; i < c.childrenCount; i++) {
				for(int i = c.childrenCount; --i >= 0; ) {
					auto k = s_api.getAccessibleChildFromContext(vmID, ac, i);
					if(k) {
						auto v = func(k, level + 1);
						if(k != v) s_api.releaseJavaObject(vmID, k);
						if(v) return v;
					}
				}
			}
			return notThis ? 0 : ac;
		};
		jo = func(ap, 0);
		//if(jo) _PrintElem(vmID, jo);
		//Perf.Next('m');
	}

	if(!jo) {
		if(!s_api.getAccessibleContextAt(vmID, ap, p.x, p.y, &jo)) {
			jo = 0;
			PRINTS(L"failed 1");
		} else if(jo == 0) {
			PRINTS(L"failed 2");
			//JAB bug: jo is 0 if the window never received a mouse message.
			//	Could post message now, but it's too dirty.
			//  Randomly fails in other cases too, and the workaround does not help then, but can cause blinking etc.
			//	Never mind.
			//PostMessageW(w, WM_MOUSEMOVE, 0, 0);
			//for(int i = 0; i < 10; i++) {
			//	Sleep(15); SendMessageTimeoutW(w, 0, 0, 0, SMTO_ABORTIFHUNG, 1000, null);
			//	if(s_api.getAccessibleContextAt(vmID, ap, p.x, p.y, &jo) && jo != 0) break; jo=0;
			//}
		} else {
			//Perf.Next('p');
			//_PrintElem(vmID, ap, L"----- ");
			//_PrintElemAndAncestors(vmID, jo);

			//workaround for:
			//	1. JAB gives empty rect for some objects. Some such objects also are disconnected from DOM and therefore cannot be found. Then use parent.
			//	2. If on a menubar item, jo often is the first item of the popup menu. Then wrong _hwnd etc.
			AccessibleContextInfo ci;
			if(s_api.getAccessibleContextInfo(vmID, jo, &ci) && !_IsPointInRect(p, ci)) {
				auto pa = s_api.getAccessibleParentFromContext(vmID, jo);
				//_PrintElem(vmID, pa);
				if(pa) {
					if(s_api.getAccessibleContextInfo(vmID, pa, &ci) && _IsPointInRect(p, ci)) {
						auto bad = jo; jo = pa; s_api.releaseJavaObject(vmID, bad);
					} else s_api.releaseJavaObject(vmID, pa);
				}
			}
		}
	}
	//Perf.Next('P');

	s_api.releaseJavaObject(vmID, ap);
	//Perf.NW('r');

	if(!jo) return null;
	return new JAccessible(jo, vmID, w);
}

} //namespace jab

IAccessible* AccJavaFromWindow(HWND w, bool getFocused /*= false*/)
{
	return jab::AccFromWindow(w, getFocused);
}

//Gets Java accessible object from point.
//p - point in screen coordinates.
//w - window containing the point.
//alt - enumerate descendants. Slow.
IAccessible* AccJavaFromPoint(POINT p, HWND w)
{
	return jab::AccFromPoint(p, w);
}

//SHOULDDO: when Delm creates tree and tries to find the captured object, use isSameObject, not rect+role. If possible and faster.
