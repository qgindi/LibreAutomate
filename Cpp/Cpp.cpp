#include "stdafx.h"
#include "Cpp.h"

HRESULT AccAgent::QueryInterface(REFIID riid, void ** ppvObject)
{
	if(riid == IID_IAccessible || riid == IID_IUnknown || riid == IID_IDispatch) {
		*ppvObject = this;
		return 0;
	}
	return E_NOINTERFACE;
}

ULONG AccAgent::AddRef(void)
{
	return 1;
}

ULONG AccAgent::Release(void)
{
	return 1;
}

HRESULT AccAgent::GetTypeInfoCount(UINT * pctinfo)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::GetTypeInfo(UINT iTInfo, LCID lcid, ITypeInfo ** ppTInfo)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::GetIDsOfNames(REFIID riid, LPOLESTR * rgszNames, UINT cNames, LCID lcid, DISPID * rgDispId)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::Invoke(DISPID dispIdMember, REFIID riid, LCID lcid, WORD wFlags, DISPPARAMS * pDispParams, VARIANT * pVarResult, EXCEPINFO * pExcepInfo, UINT * puArgErr)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accParent(IDispatch ** ppdispParent)
{
	//Print(__FUNCTIONW__);
	*ppdispParent = null;
	return 1;
}

HRESULT AccAgent::get_accChildCount(long * pcountChildren)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accChild(VARIANT varChild, IDispatch ** ppdispChild)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accName(VARIANT varChild, BSTR * pszName)
{
	*pszName = SysAllocString(L"TEST");
	return 0;
}

HRESULT AccAgent::get_accValue(VARIANT varChild, BSTR * pszValue)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accDescription(VARIANT varChild, BSTR * pszDescription)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accRole(VARIANT varChild, VARIANT * pvarRole)
{
	pvarRole->vt = VT_I4; pvarRole->lVal = ROLE_SYSTEM_STATICTEXT;
	return 0;
}

HRESULT AccAgent::get_accState(VARIANT varChild, VARIANT * pvarState)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accHelp(VARIANT varChild, BSTR * pszHelp)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accHelpTopic(BSTR * pszHelpFile, VARIANT varChild, long * pidTopic)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accKeyboardShortcut(VARIANT varChild, BSTR * pszKeyboardShortcut)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accFocus(VARIANT * pvarChild)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accSelection(VARIANT * pvarChildren)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::get_accDefaultAction(VARIANT varChild, BSTR * pszDefaultAction)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::accSelect(long flagsSelect, VARIANT varChild)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::accLocation(long * pxLeft, long * pyTop, long * pcxWidth, long * pcyHeight, VARIANT varChild)
{
	return E_NOTIMPL;
}

LRESULT TestAcc2(HWND w, OUT IAccessible*& iacc);

//HRESULT AccAgent::accNavigate(long navDir, VARIANT varStart, VARIANT * pvarEndUpAt)
//{
//	if(varStart.vt!=VT_BSTR) return 1;
//	//Print(varStart.bstrVal);
//	HWND w = (HWND)navDir;
//	if(TestAcc2(w, *(IAccessible**)&pvarEndUpAt->pdispVal)) return 1;
//	pvarEndUpAt->vt = VT_DISPATCH;
//	return 0;
//}
//
//HRESULT AccAgent::accNavigate(long navDir, VARIANT varStart, VARIANT * pvarEndUpAt)
//{
//	if(varStart.vt!=VT_BSTR) return 1;
//	//Print(varStart.bstrVal);
//	HWND w = (HWND)navDir;
//	if(AccessibleObjectFromWindow(w, 0, IID_IAccessible, (void**)&pvarEndUpAt->pdispVal)) return 1;
//	pvarEndUpAt->vt = VT_DISPATCH;
//	return 0;
//}

HRESULT AccAgent::accNavigate(long navDir, VARIANT varStart, VARIANT * pvarEndUpAt)
{
	//return 1;
	if(varStart.vt!=VT_BSTR) return 1;
	//Print(varStart.bstrVal);
	HWND w = (HWND)(LPARAM)navDir;
	IAccessiblePtr a;
	if(AccessibleObjectFromWindow(w, 0, IID_IAccessible, (void**)&a)) return 1;
	pvarEndUpAt->lVal = (long)LresultFromObject(IID_IAccessible, 0, a);
	pvarEndUpAt->vt = VT_I4;
	return 0;
}

HRESULT AccAgent::accHitTest(long xLeft, long yTop, VARIANT * pvarChild)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::accDoDefaultAction(VARIANT varChild)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::put_accName(VARIANT varChild, BSTR szName)
{
	return E_NOTIMPL;
}

HRESULT AccAgent::put_accValue(VARIANT varChild, BSTR szValue)
{
	return E_NOTIMPL;
}
