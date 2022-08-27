#include "stdafx.h"
#include "cpp.h"
#include "acc.h"

namespace uia
{
#ifdef _DEBUG
void PrintGuid(const GUID& g)
{
	LPOLESTR os = null;
	StringFromIID(g, &os);
	Print(os);
	CoTaskMemFree(os);
}
#endif

struct ThreadVar
{
	Smart<IUIAutomation> uia;
	Smart<IUIAutomationCondition> rawCond;
	Smart<IUIAutomationTreeWalker> rawWalk;
};
thread_local ThreadVar t_var;

IUIAutomation* UIA()
{
	ThreadVar& tv = t_var;
	if(!tv.uia) tv.uia.CoCreateInstance(__uuidof(CUIAutomation), null, CLSCTX_INPROC_SERVER);
	return tv.uia;
}

//Used in get_accChildCount with FindAll.
IUIAutomationCondition* CondAll()
{
	ThreadVar& tv = t_var;
	//if(!t_var.rawCond) UIA()->get_RawViewCondition(&t_var.rawCond); //FindFirst/FirstAll ignores this and uses control view instead
	if(!t_var.rawCond) UIA()->get_ControlViewCondition(&t_var.rawCond);
	return t_var.rawCond;
}

//Used in get_accParent, accNavigate, _GetHWND.
IUIAutomationTreeWalker* WalkAll()
{
	ThreadVar& tv = t_var;
	//if(!tv.rawWalk) UIA()->get_RawViewWalker(&tv.rawWalk); //don't use this because must be consistent with FindFirst/FirstAll
	if(!tv.rawWalk) UIA()->get_ControlViewWalker(&tv.rawWalk);
	return tv.rawWalk;
}

static long s_uiaWrapperCount;

//Returns false if there are UIAccessible objects in this process.
//Then cannot unload this dll, because later will be called Release and this process would crash if unloaded.
//Could not find a way to prevent Release. Even if client does not call it, COM calls it after 6 minutes. CoDisconnectObject prevents only other method calls.
bool UiaDisconnectWrappers() {
	PRINTF_IF(s_uiaWrapperCount != 0, L"cannot unload dll because of %i alive UIA wrappers.  %s", s_uiaWrapperCount, GetCommandLineW());
	return s_uiaWrapperCount == 0;
}

class UIAccessible : public IAccessible, IEnumVARIANT
{
	long _cRef;
	int _next;
	IUIAutomationElement* _ae;
	IUIAutomationElementArray* _children;
	ULONGLONG _timeOfChildren;

public:
	UIAccessible(IUIAutomationElement* ae)
	{
		//PRINTF(L"+ %p", _ae);
		//ae->AddRef(); Printf(L"+ %p ref=%i tid=%i", ae, ae->Release(), GetCurrentThreadId());
		InterlockedIncrement(&s_uiaWrapperCount);

		_cRef = 1;
		_next = 0;
		_ae = ae;
		_children = null;
		_timeOfChildren = 0;
		assert(_ae != null);

	}

	~UIAccessible()
	{
		if(_children) _children->Release();
		_ae->Release();
		//Printf(L"- %p ref=%i tid=%i", _ae, _ae->Release(), GetCurrentThreadId());
		//note: intermediate elements still have refcount 1, because they are referenced by parent's IUIAutomationElementArray.

		//PRINTF(L"- %p", _ae);
		InterlockedDecrement(&s_uiaWrapperCount);
	}

#pragma region IUnknown, IDispatch
	STDMETHODIMP QueryInterface(REFIID iid, void** ppv)
	{
		//PrintGuid(iid);

		if(iid == IID_IAccessible || iid == IID_IDispatch || iid == IID_IUnknown) {
			InterlockedIncrement(&_cRef);
			*ppv = this;
			return 0;
		} else if(iid == IID_IEnumVARIANT) {
			InterlockedIncrement(&_cRef);
			*ppv = (IEnumVARIANT*)this;
			return 0;
		}
		//else if(iid == IID_IUIAutomationElement) {
		//	//note: does not work for inproc AO. This func is called, but QI (of proxy) fails. If need, try QS, not tested.
		//	//tested: IUIAutomation.ElementFromIAccessible does not QI/QS IUIAutomationElement.
		//	//Print("--UIA");
		//	_ae->AddRef();
		//	*ppv = _ae;
		//	return 0;
		//}
		//else if(iid == IID_IServiceProvider) {
		//	InterlockedIncrement(&_cRef);
		//	*ppv = (IServiceProvider*)this;
		//	return 0;
		//}

		//if(iid == IID_IOleWindow) Print("IID_IOleWindow");
		//else if(iid == IID_IServiceProvider) Print("IID_IServiceProvider");
		//else PrintGuid(iid);

		* ppv = 0;
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

#pragma region IEnumVARIANT
	STDMETHODIMP Next(ULONG celt, VARIANT* rgVar, ULONG* pCeltFetched)
	{
		//PRINTS(__FUNCTIONW__);
		if(pCeltFetched) *pCeltFetched = 0;
		long cc;
		HRESULT hr = get_accChildCount(&cc); if(hr != 0) return hr;
		//Printf(__FUNCTIONW__ L" %i %i", celt, cc);
		for(ULONG i = 0; i < celt; i++, _next++) {
			if(_next >= cc) return 1;
			IUIAutomationElement* e = null;
			HRESULT hr = _children->GetElement(_next, &e); if(hr != 0) return hr;
			rgVar[i].pdispVal = new UIAccessible(e);
			rgVar[i].vt = VT_DISPATCH;
			if(pCeltFetched) (*pCeltFetched)++;
		}
		return 0;
	}
	STDMETHODIMP Skip(ULONG celt)
	{
		PRINTS(__FUNCTIONW__);
		return E_NOTIMPL;
	}
	STDMETHODIMP Reset(void)
	{
		_next = 0;
		return 0;
	}
	STDMETHODIMP Clone(IEnumVARIANT** ppEnum)
	{
		PRINTS(__FUNCTIONW__);
		return E_NOTIMPL;
	}
#pragma endregion

#pragma region IAccessible
	STDMETHODIMP get_accParent(IDispatch** ppdispParent)
	{
		//PRINTS(__FUNCTIONW__);
		*ppdispParent = null;
		IUIAutomationElement* p = null;
		HRESULT hr = WalkAll()->GetParentElement(_ae, &p);
		//Printf(L"0x%X %p", hr, p);
		if(hr == 0 && p == null) hr = 1;
		if(hr == 0) *ppdispParent = new UIAccessible(p);
		return hr;
	}

	STDMETHODIMP get_accChildCount(long* pcountChildren)
	{
		//PRINTS(__FUNCTIONW__);
		//Perf.First();
		*pcountChildren = 0;
		HRESULT hr;
		if(!_children || GetTickCount64() - _timeOfChildren > 40) {
			if(_children) { _children->Release(); _children = null; }
			hr = _ae->FindAll(TreeScope::TreeScope_Children, CondAll(), &_children);
			//Printf(L"0x%X %p", hr, _children.p);
			if(hr != 0 || !_children) return hr; //msdn lies: "NULL is returned if no matching element is found"
			_timeOfChildren = GetTickCount64();
		}
		//Perf.Next(); //in some cases very slow, eg in Firefox big pages
		hr = _children->get_Length((int*)pcountChildren);
		//Perf.NW(); //0-1
		return hr;
	}

	STDMETHODIMP get_accChild(VARIANT varChild, IDispatch** ppdispChild)
	{
		//PRINTS(__FUNCTIONW__);
		*ppdispChild = null;
		if(varChild.vt != VT_I4) return E_INVALIDARG;
		long cc;
		HRESULT hr = get_accChildCount(&cc); if(hr != 0) return hr;
		long i = varChild.lVal - 1; if((DWORD)i >= (DWORD)cc) return E_INVALIDARG;
		IUIAutomationElement* e = null;
		hr = _children->GetElement(i, &e);
		if(hr == 0) *ppdispChild = new UIAccessible(e);
		return hr;
	}

	STDMETHODIMP get_accName(VARIANT varChild, out BSTR* pszName)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		return _ae->get_CurrentName(pszName);
	}

	STDMETHODIMP get_accValue(VARIANT varChild, out BSTR* pszValue)
	{
		return _GetProp(varChild, 'v', pszValue);
	}

	STDMETHODIMP get_accDescription(VARIANT varChild, out BSTR* pszDescription)
	{
		return _GetProp(varChild, 'd', pszDescription);
	}

	STDMETHODIMP get_accRole(VARIANT varChild, out VARIANT* pvarRole)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		CONTROLTYPEID t;
		HRESULT hr = _ae->get_CurrentControlType(&t);
		if(hr == 0) {
			//https://docs.microsoft.com/en-us/windows/win32/winauto/appendix-g--active-accessibility-bridge-to-ui-automation
			int i = 0; STR s = L"unknown";
			switch(t) {
			case UIA_ButtonControlTypeId: i = ROLE_SYSTEM_PUSHBUTTON; break;
			case UIA_CalendarControlTypeId: s = L"Calendar"; break;
			case UIA_CheckBoxControlTypeId: i = ROLE_SYSTEM_CHECKBUTTON; break;
			case UIA_ComboBoxControlTypeId: i = ROLE_SYSTEM_COMBOBOX; break;
			case UIA_EditControlTypeId: i = ROLE_SYSTEM_TEXT; break;
			case UIA_HyperlinkControlTypeId: i = ROLE_SYSTEM_LINK; break;
			case UIA_ImageControlTypeId: i = ROLE_SYSTEM_GRAPHIC; break;
			case UIA_ListItemControlTypeId: i = ROLE_SYSTEM_LISTITEM; break;
			case UIA_ListControlTypeId: i = ROLE_SYSTEM_LIST; break;
			case UIA_MenuControlTypeId: i = ROLE_SYSTEM_MENUPOPUP; break;
			case UIA_MenuBarControlTypeId: i = ROLE_SYSTEM_MENUBAR; break;
			case UIA_MenuItemControlTypeId: i = ROLE_SYSTEM_MENUITEM; break;
			case UIA_ProgressBarControlTypeId: i = ROLE_SYSTEM_PROGRESSBAR; break;
			case UIA_RadioButtonControlTypeId: i = ROLE_SYSTEM_RADIOBUTTON; break;
			case UIA_ScrollBarControlTypeId: i = ROLE_SYSTEM_SCROLLBAR; break;
			case UIA_SliderControlTypeId: i = ROLE_SYSTEM_SLIDER; break;
			case UIA_SpinnerControlTypeId: i = ROLE_SYSTEM_SPINBUTTON; break;
			case UIA_StatusBarControlTypeId: i = ROLE_SYSTEM_STATUSBAR; break;
			case UIA_TabControlTypeId: i = ROLE_SYSTEM_PAGETABLIST; break;
			case UIA_TabItemControlTypeId: i = ROLE_SYSTEM_PAGETAB; break;
			case UIA_TextControlTypeId: i = ROLE_SYSTEM_STATICTEXT; break;
			case UIA_ToolBarControlTypeId: i = ROLE_SYSTEM_TOOLBAR; break;
			case UIA_ToolTipControlTypeId: i = ROLE_SYSTEM_TOOLTIP; break;
			case UIA_TreeControlTypeId: i = ROLE_SYSTEM_OUTLINE; break;
			case UIA_TreeItemControlTypeId: i = ROLE_SYSTEM_OUTLINEITEM; break;
			case UIA_CustomControlTypeId: i = ROLE_SYSTEM_CLIENT; break; //documented as the default MSAA role. And it's good for _IsContainer.
			case UIA_GroupControlTypeId: i = ROLE_SYSTEM_GROUPING; break;
			case UIA_ThumbControlTypeId: i = ROLE_SYSTEM_INDICATOR; break;
			case UIA_DataGridControlTypeId: i = ROLE_SYSTEM_LIST; break;
			case UIA_DataItemControlTypeId: i = ROLE_SYSTEM_LISTITEM; break;
			case UIA_DocumentControlTypeId: i = ROLE_SYSTEM_DOCUMENT; break;
			case UIA_SplitButtonControlTypeId: i = ROLE_SYSTEM_SPLITBUTTON; break;
			case UIA_WindowControlTypeId: i = ROLE_SYSTEM_WINDOW; break;
			case UIA_PaneControlTypeId: i = ROLE_SYSTEM_PANE; break;
			case UIA_HeaderControlTypeId: i = ROLE_SYSTEM_LIST; break;
			case UIA_HeaderItemControlTypeId: i = ROLE_SYSTEM_COLUMNHEADER; break;
			case UIA_TableControlTypeId: i = ROLE_SYSTEM_TABLE; break;
			case UIA_TitleBarControlTypeId: i = ROLE_SYSTEM_TITLEBAR; break;
			case UIA_SeparatorControlTypeId: i = ROLE_SYSTEM_SEPARATOR; break;
			case UIA_SemanticZoomControlTypeId: s = L"SemanticZoom"; break;
			case UIA_AppBarControlTypeId: s = L"AppBar"; break;
			}
			if(i) {
				pvarRole->vt = VT_I4;
				pvarRole->lVal = i;
			} else {
				pvarRole->vt = VT_BSTR;
				pvarRole->bstrVal = SysAllocString(s);
			}
		}
		return hr;
	}

	STDMETHODIMP get_accState(VARIANT varChild, out VARIANT* pvarState)
	{
		//PRINTS(__FUNCTIONW__);
		long state;
		HRESULT hr = _GetProp(varChild, 's', &state);
		if(hr == 0) {
			pvarState->vt = VT_I4;
			pvarState->lVal = state;
		}
		return hr;
	}

	STDMETHODIMP get_accHelp(VARIANT varChild, out BSTR* pszHelp)
	{
		if(varChild.vt == VT_I1) { //prop uiaid
			switch(varChild.cVal) {
			case 'u': return _ae->get_CurrentAutomationId(pszHelp);
			}
		}
		return _GetProp(varChild, 'h', pszHelp);
	}

	STDMETHODIMP get_accHelpTopic(BSTR* pszHelpFile, VARIANT varChild, long* pidTopic)
	{
		return E_NOTIMPL;
	}

	STDMETHODIMP get_accKeyboardShortcut(VARIANT varChild, out BSTR* pszKeyboardShortcut)
	{
		return _GetProp(varChild, 'k', pszKeyboardShortcut);
	}

	STDMETHODIMP get_accFocus(out VARIANT* pvarChild)
	{
		return E_NOTIMPL;
		//Rarely used.
	}

	class UIAccessibleSelectedChildren : public IEnumVARIANT
	{
		long _cRef;
		int _next, _count;
		Smart<IUIAutomationElementArray> _a;

	public:
		UIAccessibleSelectedChildren(IUIAutomationElementArray* a, int count) : _a(a, false)
		{
			//PRINTS(__FUNCTIONW__);
			_cRef = 1;
			_next = 0;
			_count = count;
		}

		//~UIAccessibleSelectedChildren()
		//{
		//	PRINTS(__FUNCTIONW__);
		//}

		STDMETHODIMP QueryInterface(REFIID riid, void** ppvObject)
		{
			if(riid == IID_IEnumVARIANT || riid == IID_IUnknown) {
				++_cRef;
				*ppvObject = this;
				return 0;
			}
			*ppvObject = null;
			return E_NOINTERFACE;
		}
		STDMETHODIMP_(ULONG) AddRef(void)
		{
			return ++_cRef;
		}
		STDMETHODIMP_(ULONG) Release(void)
		{
			long ret = --_cRef;
			if(!ret) delete this;
			return ret;
		}
		STDMETHODIMP Next(ULONG celt, VARIANT* rgVar, ULONG* pCeltFetched)
		{
			if(pCeltFetched) *pCeltFetched = 0;
			for(ULONG i = 0; i < celt; i++, _next++) {
				if(_next == _count) return 1;
				IUIAutomationElement* e = null;
				HRESULT hr = _a->GetElement(_next, &e); if(hr != 0) return hr;
				rgVar[i].pdispVal = new UIAccessible(e);
				rgVar[i].vt = VT_DISPATCH;
				if(pCeltFetched) (*pCeltFetched)++;
			}
			return 0;
		}
		STDMETHODIMP Skip(ULONG celt)
		{
			return E_NOTIMPL;
		}
		STDMETHODIMP Reset(void)
		{
			_next = 0;
			return 0;
		}
		STDMETHODIMP Clone(IEnumVARIANT** ppEnum)
		{
			return E_NOTIMPL;
		}
	};

	STDMETHODIMP get_accSelection(out VARIANT* pvarChildren)
	{
		Smart<IUIAutomationSelectionPattern> p;
		HRESULT hr = _ae->GetCurrentPatternAs(UIA_SelectionPatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
		if(hr == 0) {
			Smart<IUIAutomationElementArray> a;
			hr = p->GetCurrentSelection(&a);
			if(hr == 0) {
				pvarChildren->vt = 0;
				int n; hr = a->get_Length(&n); if(hr != 0) n = 0;
				if(n == 1) {
					IUIAutomationElement* e = null;
					hr = a->GetElement(0, &e);
					if(hr == 0) {
						pvarChildren->pdispVal = new UIAccessible(e);
						pvarChildren->vt = VT_DISPATCH;
					}
				} else if(n > 1) {
					pvarChildren->punkVal = new UIAccessibleSelectedChildren(a.Detach(), n);
					pvarChildren->vt = VT_UNKNOWN;
				}
			}
		}
		return hr;
	}

	STDMETHODIMP get_accDefaultAction(VARIANT varChild, out BSTR* pszDefaultAction)
	{
		return _GetProp(varChild, 'a', pszDefaultAction);
	}

	STDMETHODIMP accSelect(long flagsSelect, VARIANT varChild)
	{
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		if(flagsSelect & SELFLAG_EXTENDSELECTION) return E_INVALIDARG;
		HRESULT hr = 0;
		if(flagsSelect & (SELFLAG_TAKESELECTION | SELFLAG_ADDSELECTION | SELFLAG_REMOVESELECTION)) {
			Smart<IUIAutomationSelectionItemPattern> p;
			hr = _ae->GetCurrentPatternAs(UIA_SelectionItemPatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
			if(hr == 0) {
				if(flagsSelect & SELFLAG_TAKESELECTION) hr = p->Select();
				if(flagsSelect & SELFLAG_ADDSELECTION && hr == 0) hr = p->AddToSelection();
				if(flagsSelect & SELFLAG_REMOVESELECTION && hr == 0) hr = p->RemoveFromSelection();
			}
		}
		if(flagsSelect & SELFLAG_TAKEFOCUS && hr == 0) {
			hr = _ae->SetFocus();
		}
		return hr;
	}

	STDMETHODIMP accLocation(out long* pxLeft, out long* pyTop, out long* pcxWidth, out long* pcyHeight, VARIANT varChild)
	{
		//PRINTS(__FUNCTIONW__);
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		RECT r;
		HRESULT hr = _ae->get_CurrentBoundingRectangle(&r);
		if(hr == 0) {
			*pxLeft = r.left; *pyTop = r.top; *pcxWidth = r.right - r.left; *pcyHeight = r.bottom - r.top;
		}
		return hr;
	}

	STDMETHODIMP accNavigate(long navDir, VARIANT varStart, out VARIANT* pvarEndUpAt)
	{
		//Print("accNavigate", navDir, varStart.vt, varStart.value);

		//WindowFromAccessibleObject (WFAO) at first calls this with an undocumented navDir 10.
		//	tested: accNavigate(10) for a standard Windows control returns VARIANT(VT_I4, hwnd).
		//	If we return window handle, WFAO does not call get_accParent. Else also calls it for each ancestor.
		//	Currently WFAO cannot get hwnd when calling our get_accParent.
		if(navDir == 10 && varStart.vt == VT_I4) {
			//Perf.First();
			HWND w = _GetHWND();
			//Perf.NW();
			if(w == 0) return 1;
			pvarEndUpAt->vt = VT_I4;
			pvarEndUpAt->lVal = (int)(LPARAM)w;
			return 0;
		}

		if(navDir < NAVDIR_UP || navDir > NAVDIR_LASTCHILD) return E_INVALIDARG;
		if(_InvalidVarChildParam(ref varStart)) return E_INVALIDARG;

		auto walker = WalkAll();
		IUIAutomationElement* p = null;
		HRESULT hr = 0;
		switch(navDir) {
		case NAVDIR_NEXT: hr = walker->GetNextSiblingElement(_ae, &p); break;
		case NAVDIR_PREVIOUS: hr = walker->GetPreviousSiblingElement(_ae, &p); break;
		case NAVDIR_FIRSTCHILD: hr = walker->GetFirstChildElement(_ae, &p); break;
		case NAVDIR_LASTCHILD: hr = walker->GetLastChildElement(_ae, &p); break;
		}
		if(hr == 0 && p == null) hr = 1;
		if(hr == 0) {
			pvarEndUpAt->pdispVal = new UIAccessible(p);
			pvarEndUpAt->vt = VT_DISPATCH;
		}
		return hr;
	}

	STDMETHODIMP accHitTest(long xLeft, long yTop, out VARIANT* pvarChild)
	{
		return E_NOTIMPL;
		//Rarely used.
	}

	//The _DDA_x functions try to get a pattern and call its method. They return true if pattern available, even if method failed.
	bool _DDA_InvokeUIA(ref HRESULT& hr) {
		Smart<IUIAutomationInvokePattern> p;
		hr = _ae->GetCurrentPatternAs(UIA_InvokePatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
		if(hr != 0) return false;
		//Print("Invoke");/
		hr = p->Invoke();
		return true;
	}

	bool _DDA_Toggle(ref HRESULT& hr) {
		Smart<IUIAutomationTogglePattern> p;
		hr = _ae->GetCurrentPatternAs(UIA_TogglePatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
		if(hr != 0) return false;
		//Print("Toggle");
		hr = p->Toggle();
		return true;
	}

	//expand: 0 collapse, 1 expand, 2 toggle
	bool _DDA_Expand(ref HRESULT& hr, int expand) {
		Smart<IUIAutomationExpandCollapsePattern> p;
		hr = _ae->GetCurrentPatternAs(UIA_ExpandCollapsePatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
		if(hr != 0) return false;
		//Print("Expand");
		if(expand == 2) {
			ExpandCollapseState ecs = ExpandCollapseState_LeafNode;
			if(0 == p->get_CurrentExpandCollapseState(&ecs)) {
				if(ecs == ExpandCollapseState_Expanded) expand = 0;
				else if(ecs != ExpandCollapseState_LeafNode) expand = 1; //collapsed or partially expanded
			} else hr = 1;
		}
		if(expand == 1) hr = p->Expand(); else if(expand == 0) hr = p->Collapse();
		return true;
	}

	bool _DDA_Select(ref HRESULT& hr) {
		Smart<IUIAutomationSelectionItemPattern> p;
		hr = _ae->GetCurrentPatternAs(UIA_SelectionItemPatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
		if(hr != 0) return false;
		//Print("Select");
		hr = p->Select();
		return true;
	}

	bool _DDA_InvokeMSAA(ref HRESULT& hr) {
		Smart<IUIAutomationLegacyIAccessiblePattern> p;
		hr = _ae->GetCurrentPatternAs(UIA_LegacyIAccessiblePatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
		if(hr != 0) return false;
		//Print("DoDefaultAction");
		hr = p->DoDefaultAction();
		return true;
	}

	STDMETHODIMP accDoDefaultAction(VARIANT varChild)
	{
		HRESULT hr = 1;

		if(varChild.vt == VT_I1) { //specified action
			if(varChild.cVal == 's') { //ScrollTo
				Smart<IUIAutomationScrollItemPattern> p;
				hr = _ae->GetCurrentPatternAs(UIA_ScrollItemPatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
				if(hr == 0) hr = p->ScrollIntoView();
			} else if(varChild.cVal == 'c') { //Check(true/false)
				bool ok = _DDA_Toggle(ref hr)
					|| _DDA_InvokeUIA(ref hr);
			} else if(varChild.cVal == 'E' || varChild.cVal == 'e') { //Expand(true/false)
				bool ok = _DDA_Expand(ref hr, varChild.cVal == 'E' ? 1 : 0) //treeitem, combobox
					|| _DDA_Toggle(ref hr); //some expanders
			}
		} else { //default action
			if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
			bool ok = _DDA_InvokeUIA(ref hr)
				|| _DDA_Toggle(ref hr)
				|| _DDA_Expand(ref hr, 2)
				|| _DDA_Select(ref hr)
				|| _DDA_InvokeMSAA(ref hr);
			//Print((DWORD)hr);

			//Many UIA elements don't have Invoke pattern but have some other pattern that can be used instead.
			//Many have IAccessible pattern, but often its DoDefaultAction doesn't work.
		}
		return hr;
	}

	STDMETHODIMP put_accName(VARIANT varChild, BSTR szName)
	{
		return E_NOTIMPL;
		//Rarely used, deprecated.
	}

	STDMETHODIMP put_accValue(VARIANT varChild, BSTR szValue)
	{
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		Smart<IUIAutomationValuePattern> p;
		HRESULT hr = _ae->GetCurrentPatternAs(UIA_ValuePatternId, IID_PPV_ARGS(&p));
		if(hr == 0 && !p) hr = 1;
		if(hr == 0) hr = p->SetValue(szValue);
		return hr;
	}
#pragma endregion

#pragma region private
private:
	bool _InvalidVarChildParam(const VARIANT& v)
	{
		if(v.vt == 0) return false; //forgive
		return v.vt != VT_I4 || v.lVal != 0;
	}

	HRESULT _GetProp(const VARIANT& varChild, WCHAR prop, void* R)
	{
		if(_InvalidVarChildParam(ref varChild)) return E_INVALIDARG;
		Smart<IUIAutomationLegacyIAccessiblePattern> p;
		//Perf.First();
		HRESULT hr = _ae->GetCurrentPatternAs(UIA_LegacyIAccessiblePatternId, IID_PPV_ARGS(&p)); if(hr == 0 && !p) hr = 1;
		if(hr == 0) {
			//Perf.Next();
			switch(prop) {
			case 'a': hr = p->get_CurrentDefaultAction((BSTR*)R); break;
			case 'v': hr = p->get_CurrentValue((BSTR*)R); break;
			case 'd': hr = p->get_CurrentDescription((BSTR*)R); break;
			case 'k': hr = p->get_CurrentKeyboardShortcut((BSTR*)R); break;
			case 'h': hr = p->get_CurrentHelp((BSTR*)R); break;
			case 's': hr = p->get_CurrentState((DWORD*)R); break;
			}
		}
		//Perf.NW(); //90 100
		return hr;
	}

#if true
	HWND _GetHWND()
	{
		//PRINTS(__FUNCTIONW__);
		for(auto e = _ae; ;) {
			UIA_HWND w = 0;
			HRESULT hr = e->get_CurrentNativeWindowHandle(&w);
			if(hr || w) {
				if(e != _ae) e->Release();
				if(hr) break;
				return (HWND)w;
			}
			IUIAutomationElement* parent = null;
			hr = WalkAll()->GetParentElement(e, &parent);
			if(e != _ae) e->Release();
			if(hr || !parent) break;
			e = parent;
		}
		return 0;
	}
#elif true
	//50-100% slower. May get not the direct parent control.
	HWND _GetHWND()
	{
		//PRINTS(__FUNCTIONW__);
		UIA_HWND w = 0;
		HRESULT hr = _ae->get_CurrentNativeWindowHandle(&w);
		if(hr) return 0;
		if(w) return (HWND)w;

		Smart<IUIAutomationCondition> cond;
		if(0 != UIA()->CreatePropertyCondition(UIA_ControlTypePropertyId, ao::VE(UIA_WindowControlTypeId), &cond)) return 0;
		Smart<IUIAutomationTreeWalker> walker;
		if(0 != UIA()->CreateTreeWalker(cond, &walker)) return 0;
		Perf.Next();

		Smart<IUIAutomationElement> p;
		hr = walker->GetParentElement(_ae, &p); //why so slow?
		if(hr == 0 && !p) hr = 1;
		Perf.Next();
		if(hr == 0) hr = p->get_CurrentNativeWindowHandle(&w);
		if(hr) return 0;
		return (HWND)w;
	}
#else
	//Fast, but unreliable. Many objects don't have RuntimeId or it does not contain HWND.
	HWND _GetHWND()
	{
		//PRINTS(__FUNCTIONW__);
		//UIA_HWND w = 0;
		//HRESULT hr = _ae->get_CurrentNativeWindowHandle(&w);
		//if(hr) return 0;
		//if(w) return (HWND)w;

		SAFEARRAY* a = null;
		if(0 == _ae->GetRuntimeId(&a)) {
			HWND w = 0;
			if(a->cbElements == 4 && a->rgsabound[0].cElements >= 2) {
				int* p = (int*)a->pvData;
				if(p[0] == 0x2A) {
					w = (HWND)(LPARAM)p[1];
					assert(IsWindow(w));
					if(!IsWindow(w)) w = 0;
				}
			}
			SafeArrayDestroy(a);
			return w;
		}

		return 0;
	}
#endif

#pragma endregion

	// Inherited via IServiceProvider
	//virtual HRESULT QueryService(REFGUID guidService, REFIID riid, void ** ppvObject) override
	//{
	//	Print("QS:");
	//	PrintGuid(riid);
	//	return E_NOTIMPL;
	//}
};

HRESULT AccFromWindow(HWND w, out IAccessible** iacc)
{
	IUIAutomationElement* e = null;
	HRESULT hr = UIA()->ElementFromHandle(w, &e);
	*iacc = hr == 0 ? new UIAccessible(e) : null;
	return hr;
}

HRESULT AccFromPoint(POINT p, out IAccessible** iacc)
{
	IUIAutomationElement* e = null;
	HRESULT hr = UIA()->ElementFromPoint(p, &e);
	*iacc = hr == 0 ? new UIAccessible(e) : null;
	return hr;
}

HRESULT AccFocused(out IAccessible** iacc)
{
	IUIAutomationElement* e = null;
	HRESULT hr = UIA()->GetFocusedElement(&e);
	*iacc = hr == 0 ? new UIAccessible(e) : null;
	return hr;
}

//Fails with most. When succeeds, the object often is half-valid.
//HRESULT AccFromMSAA(IAccessible* msaa, int elem, out IAccessible** iacc)
//{
//	IUIAutomationElement* e = null;
//	HRESULT hr = UIA()->ElementFromIAccessible(msaa, elem, &e);
//	*iacc = hr == 0 ? new UIAccessible(e) : null;
//	return hr;
//}

} //namespace uia

HRESULT AccUiaFromWindow(HWND w, out IAccessible** iacc)
{
	return uia::AccFromWindow(w, iacc);
}

HRESULT AccUiaFromPoint(POINT p, out IAccessible** iacc)
{
	return uia::AccFromPoint(p, iacc);
}

HRESULT AccUiaFocused(out IAccessible** iacc)
{
	return uia::AccFocused(iacc);
}

//HRESULT AccUiaFromMSAA(IAccessible* msaa, int elem, out IAccessible** iacc)
//{
//	return uia::AccFromMSAA(msaa, elem, iacc);
//}
