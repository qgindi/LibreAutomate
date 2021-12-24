#include "stdafx.h"
#include "cpp.h"

bool ElementFromWindow(IUIAutomation* f, HWND w, Smart<IUIAutomationElement>& e)
{
	if(f->ElementFromHandle(w, &e)) return false;
	return true;
}

class UiaFinder
{
	Smart<IUIAutomation> _f;
	Bstr _name;
	Smart<IUIAutomationCondition> _condRaw;
public:
	Smart<IUIAutomationElement> eFound;

	UiaFinder() {
		if(_f.CoCreateInstance(__uuidof(CUIAutomation), null, CLSCTX_INPROC_SERVER)) return;
		if(_f->get_RawViewCondition(&_condRaw)) return;
	}

	int Find(HWND w, STR name) {
		if(!_f) return 1;

		Smart<IUIAutomationElement> ew;
		if(!ElementFromWindow(_f, w, ew)) return 2;

		return Find(ew, name);
	}

	//int Find(IUIAutomationElement* ew, STR name) {

	//	IUIAutomationCondition> cond;
	//	if (_f->CreatePropertyCondition(UIA_NamePropertyId, _variant_t(name), &cond)) return 3;

	//	if (ew->FindFirst(TreeScope_Descendants, cond, &eFound)) return 4;

	//	return 0;
	//}

	int Find(IUIAutomationElement* ew, STR name) {
		if(!_f) return 1;
		_name = name;

		return _Find(ew, 0);
	}

private:
	int _Find(IUIAutomationElement* ew, int level) {

		Smart<IUIAutomationElementArray> a;
		if(ew->FindAll(TreeScope_Children, _condRaw, &a)) return 4;

		int n = 0;
		if(a->get_Length(&n)) return 5;

		for(int i = 0; i < n; i++) {
			Smart<IUIAutomationElement> e;
			if(a->GetElement(i, &e)) return 6;

			Bstr b;
			if(0 == e->get_CurrentName(&b)) {
				//Printf(L"%i %s", level, (STR)b);
				if(b == _name) {
					eFound.Attach(e.Detach());
					break;
				}
			}

			int k = _Find(e, level + 1);
			if(k) return k;
			if(eFound) break;
		}

		return 0;
	}
};

#if true
LRESULT TestUia(HWND w)
{
	UiaFinder x;
	int err = x.Find(w, L"Untitled");
	if(err) return err;

	if(x.eFound) {
		Bstr b;
		if(x.eFound->get_CurrentName(&b)) return 50;
		Print(b);
	} else Print(L"not found");

	return 0;
}
#else
LRESULT TestUia(HWND w)
{
	//Print(L"TestUia");

	//_TODO: CoInitialize if need.

	//Smart<IUIAutomation> f(__uuidof(CUIAutomation), null, CLSCTX_INPROC_SERVER);
	Smart<IUIAutomation> f;
	if(f.CreateInstance(__uuidof(CUIAutomation), null, CLSCTX_INPROC_SERVER)) return 1;
	//Print((__int64)f.p);

	Smart<IUIAutomationElement> ew;
	if(!ElementFromWindow(f, w, ew)) return 2;

	Smart<IUIAutomationCondition> cond;
	if(f->CreatePropertyCondition(UIA_NamePropertyId, _variant_t(L"Untitled"), &cond)) return 3;

	Smart<IUIAutomationElement> e;
	if(ew->FindFirst(TreeScope_Descendants, cond, &e)) return 4;
	if(!e) return 100;

	_bstr_t b;
	if(e->get_CurrentName(&b.GetBSTR())) return 5;

	Print(b);

	return 0;
}
#endif
