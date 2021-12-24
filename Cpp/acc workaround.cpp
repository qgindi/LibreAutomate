#include "stdafx.h"
#include "cpp.h"

//Workaround for .NET bug: calls IAccessible implementation methods in other thread.

class AccWorkaround :IAccessible
{
	IAccessible* _a;
	int _ref;

public:
	AccWorkaround(IAccessible* a) {
		_a = a;
		_ref = 1;
	}
#pragma region
	virtual HRESULT __stdcall QueryInterface(REFIID riid, void** ppvObject) override
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
		return ++_ref;
	}
	virtual ULONG __stdcall Release(void) override
	{
		int r = --_ref;
		//Print(r);
		if(r == 0) delete this;
		return r;
	}
	virtual HRESULT __stdcall GetTypeInfoCount(UINT* pctinfo) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall GetTypeInfo(UINT iTInfo, LCID lcid, ITypeInfo** ppTInfo) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall GetIDsOfNames(REFIID riid, LPOLESTR* rgszNames, UINT cNames, LCID lcid, DISPID* rgDispId) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall Invoke(DISPID dispIdMember, REFIID riid, LCID lcid, WORD wFlags, DISPPARAMS* pDispParams, VARIANT* pVarResult, EXCEPINFO* pExcepInfo, UINT* puArgErr) override
	{
		return E_NOTIMPL;
	}
#pragma endregion
	virtual HRESULT __stdcall get_accParent(IDispatch** ppdispParent) override
	{
		return _a->get_accParent(ppdispParent);
	}
	virtual HRESULT __stdcall get_accChildCount(long* pcountChildren) override
	{
		//Print(__FUNCTION__);
		return _a->get_accChildCount(pcountChildren);
	}
	virtual HRESULT __stdcall get_accChild(VARIANT varChild, IDispatch** ppdispChild) override
	{
		return _a->get_accChild(varChild, ppdispChild);
	}
	virtual HRESULT __stdcall get_accName(VARIANT varChild, BSTR* pszName) override
	{
		return _a->get_accName(varChild, pszName);
	}
	virtual HRESULT __stdcall get_accValue(VARIANT varChild, BSTR* pszValue) override
	{
		return _a->get_accValue(varChild, pszValue);
	}
	virtual HRESULT __stdcall get_accDescription(VARIANT varChild, BSTR* pszDescription) override
	{
		return _a->get_accDescription(varChild, pszDescription);
	}
	virtual HRESULT __stdcall get_accRole(VARIANT varChild, VARIANT* pvarRole) override
	{
		return _a->get_accRole(varChild, pvarRole);
	}
	virtual HRESULT __stdcall get_accState(VARIANT varChild, VARIANT* pvarState) override
	{
		return _a->get_accState(varChild, pvarState);
	}
	virtual HRESULT __stdcall get_accHelp(VARIANT varChild, BSTR* pszHelp) override
	{
		return _a->get_accHelp(varChild, pszHelp);
	}
	virtual HRESULT __stdcall get_accHelpTopic(BSTR* pszHelpFile, VARIANT varChild, long* pidTopic) override
	{
		return E_NOTIMPL;
	}
	virtual HRESULT __stdcall get_accKeyboardShortcut(VARIANT varChild, BSTR* pszKeyboardShortcut) override
	{
		return _a->get_accKeyboardShortcut(varChild, pszKeyboardShortcut);
	}
	virtual HRESULT __stdcall get_accFocus(VARIANT* pvarChild) override
	{
		return _a->get_accFocus(pvarChild);
	}
	virtual HRESULT __stdcall get_accSelection(VARIANT* pvarChildren) override
	{
		return _a->get_accSelection(pvarChildren);
	}
	virtual HRESULT __stdcall get_accDefaultAction(VARIANT varChild, BSTR* pszDefaultAction) override
	{
		return _a->get_accDefaultAction(varChild, pszDefaultAction);
	}
	virtual HRESULT __stdcall accSelect(long flagsSelect, VARIANT varChild) override
	{
		return _a->accSelect(flagsSelect, varChild);
	}
	virtual HRESULT __stdcall accLocation(long* pxLeft, long* pyTop, long* pcxWidth, long* pcyHeight, VARIANT varChild) override
	{
		return _a->accLocation(pxLeft, pyTop, pcxWidth, pcyHeight, varChild);
	}
	virtual HRESULT __stdcall accNavigate(long navDir, VARIANT varStart, VARIANT* pvarEndUpAt) override
	{
		return _a->accNavigate(navDir, varStart, pvarEndUpAt);
	}
	virtual HRESULT __stdcall accHitTest(long xLeft, long yTop, VARIANT* pvarChild) override
	{
		return _a->accHitTest(xLeft, yTop, pvarChild);
	}
	virtual HRESULT __stdcall accDoDefaultAction(VARIANT varChild) override
	{
		return _a->accDoDefaultAction(varChild);
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

EXPORT LRESULT Cpp_AccWorkaround(IAccessible* a, WPARAM wParam, AccWorkaround*& m) {
	//Print(a->AddRef());
	if(a != null) {
		if(m==null) m = new AccWorkaround(a);
		return LresultFromObject(IID_IAccessible, wParam, (LPUNKNOWN)m);
	} else if(m!=null){
		m->Release();
		m = null;
	}
	return 0;
}
