#include "stdafx.h"
#include "cpp.h"
#include "acc.h"
#include <mshtml.h>
#include "ISimpleDOMNode.h"
#include "ISimpleDOMText.h"
//#include "IAccessible2.h"

namespace
{
class IEElem
{
	IHTMLElement* _x;
public:
	IEElem() {
		_x = null;
	}

	~IEElem() {
		if(_x) _x->Release();
	}

	IHTMLElement* operator ->() {
		return _x;
	}

	operator bool() {
		return _x != null;
	}

	bool FromAcc(IAccessible* iacc)
	{
		assert(_x == null);
		return QueryService(iacc, out &_x);
	}

	BSTR GetAttribute(STR name)
	{
		if(CMP5(name, L"class")) {
			BSTR b = null;
			_x->get_className(&b);
			return b;
		} else {
			VARIANT v;
			if(0 != _x->getAttribute(Bstr(name), 2, &v)) return null;
			if(v.vt != VT_BSTR && 0 != VariantChangeType(&v, &v, 0, VT_BSTR)) { VariantClear(&v); return null; }
			return v.bstrVal;
		}
	}

	//Returns null if fails or has 0 attributes.
	//Later delete[] the result.
	BstrNameValue* GetAttributes(out int& count)
	{
		count = 0;
		BstrNameValue* a = null;
		IHTMLElement5* e5 = null;
		if(0 != _x->QueryInterface(&e5)) return null;
		IHTMLAttributeCollection3* col = null;
		if(0 == e5->get_attributes(&col)) {
			long n;
			if(0 == col->get_length(&n) && n > 0) {
				count = n;
				a = new BstrNameValue[n];
				for(long i = 0; i < n; i++) {
					IHTMLDOMAttribute* ida = null;
					if(0 == col->item(i, &ida)) {
						_variant_t va;
						if(0 == ida->get_nodeName(&a[i].name) && 0 == ida->get_nodeValue(&va)) {
							if(va.vt == VT_BSTR || 0 == VariantChangeType(&va, &va, 0, VT_BSTR))
								a[i].value.Attach(va.Detach().bstrVal);
						}
						ida->Release();
					}
				}
			}
			col->Release();
		}
		e5->Release();
		return a;
	}

};

class FFNode
{
	static constexpr GUID IID_ISimpleDOMNodeService = { 0x0c539790, 0x12e4, 0x11cf, 0xb6, 0x61, 0x00, 0xaa, 0x00, 0x4c, 0xd6, 0xd8 };

	ISimpleDOMNode* _x;

	FFNode(ISimpleDOMNode* x) {
		_x = x;
	}
public:
	FFNode() {
		_x = null;
	}

	~FFNode() {
		if(_x) _x->Release();
	}

	ISimpleDOMNode* operator ->() {
		return _x;
	}

	operator bool() {
		return _x != null;
	}

	ISimpleDOMNode* Detach() {
		auto r = _x; _x = null; return r;
	}


	bool FromAcc(IAccessible* iacc)
	{
		assert(_x == null);
		return QueryService(iacc, &_x, &IID_ISimpleDOMNodeService);
	}

	BSTR GetAttribute(STR name)
	{
		Bstr bn(name); short ns; BSTR r = null;
		if(0 != _x->get_attributesForNames(1, &bn, &ns, &r)) return null;

		//problem: Firefox gets "" for missing attributes. I don't know a fast workaround. Chrome and IE then fail (null).
		//if(r != null && *r == 0) {
		//	IAccessible2_2* ia2=null;
		//	if(0==_x->QueryInterface(&ia2)) {
		//		_variant_t v;
		//		Print((uint)ia2->get_attribute(bn, &v)); //E_NOTIMPL
		//		ia2->Release();
		//	}
		//}

		return r;

		//problem: Chrome case-sensitive. Firefox and IE not.
	}

	struct NodeInfo
	{
		Bstr tag, text;
		UINT childCount, uniqueId;
		short namespaceId;
		unsigned short nodeType;
	};

	bool GetNodeInfo(out NodeInfo& r, bool needText)
	{
		assert(!r.tag && !r.text);
		if(0 != _x->get_nodeInfo(&r.tag, &r.namespaceId, &r.text, &r.childCount, &r.uniqueId, &r.nodeType)) return false;
		if(needText) {
			if(r.nodeType == NODETYPE_TEXT && (!r.text || !r.text.Length())) { //Chrome
				if(r.text) r.text.Empty();
				ISimpleDOMText* dt = null;
				if(0 == _x->QueryInterface(&dt)) {
					if(0 != dt->get_domText(&r.text) || r.text.Length() == 0) r.text.Empty();
					dt->Release();
				}
			}
		} else if(r.text) r.text.Empty();
		return true;
	}

	//Returns null if fails or has 0 attributes.
	//Later delete[] the result.
	BstrNameValue* GetAttributes(out int& count)
	{
		BSTR na[300], va[_countof(na)]; short nsa[_countof(na)]; unsigned short n = 0; //max seen: 22 in FF UI, 16 in web page (rarely > 12)
		if(0 != _x->get_attributes(_countof(na), na, nsa, va, &n) || n == 0) { count = 0; return null; }
		count = n;
		auto a = new BstrNameValue[n];
		for(int i = 0; i < n; i++) {
			BstrNameValue& r = a[i];
			r.name.Attach(na[i]);
			r.value.Attach(va[i]);
		}
		return a;
	}

	BSTR GetInnerHTML()
	{
		Bstr s;
		HRESULT hr = _x->get_innerHTML(&s);
		if(hr) {
			NodeInfo x;
			if(GetNodeInfo(x, true)) {
				if(hr == E_NOTIMPL) { //Chrome does not implement this method. Workaround: compose from descendants.
					hr = 0;
					if(x.childCount > 0) {
						str::StringBuilder b;
						_ChromeComposeInnerHTML(b, x.childCount);
						s = (LPWSTR)b;
					} else s = L"";
				} else { //Firefox does not give HTML for document. Get it from its descendant <BODY>.
					return _FirefoxGetBodyHtml(x.childCount, false);
				}
			} else PRINTS(L"failed");
		}
		return hr ? null : s.Detach();
	}

	BSTR GetOuterHTML()
	{
		NodeInfo x;
		if(!GetNodeInfo(x, true)) { PRINTS(L"failed"); return null; }

		if(str::IsEmpty(x.tag)) {
			PRINTF_IF(x.nodeType != NODETYPE_TEXT, L"--- not Text: %i", x.nodeType);
			return x.text.Detach();
		}

		//ISimpleDOMNode does not have a method to get outer HTML. Compose it from tag, attributes and inner HTML.
		bool isDoc = x.nodeType == NODETYPE_DOCUMENT;
		if(isDoc) x.tag = L"body"; //"#document"
		PRINTF_IF(x.nodeType != NODETYPE_ELEMENT && !isDoc && x.tag != L"br", L"--- not Element: %i", x.nodeType);
		str::StringBuilder b;
		_HtmlAppendHead(b, x.tag);
		if(x.childCount > 0) {
			Bstr inner;
			int hr = _x->get_innerHTML(&inner);
			if(hr == 0) {
				b.AppendBSTR(inner);
			} else if(hr == E_NOTIMPL) { //Chrome does not implement this method. Workaround: compose from descendants.
				_ChromeComposeInnerHTML(b, x.childCount);
			} else if(isDoc) { //Firefox does not give HTML for document. Get it from its descendant <BODY>.
				return _FirefoxGetBodyHtml(x.childCount, true);
			}
		}
		_HtmlAppendTail(b, x.tag);
		return b.ToBSTR();
	}

private:
#pragma region private
	void _ChromeComposeInnerHTML(str::StringBuilder& b, int childCount)
	{
		for(int i = 0; i < childCount; i++) {
			FFNode child;
			if(0 != _x->get_childAt(i, &child._x)) { PRINTS(L"failed"); if(i == 0) return; continue; }
			child._ChromeComposeHTML(b);
		}
	}

	void _ChromeComposeHTML(str::StringBuilder& b)
	{
		NodeInfo r;
		if(!GetNodeInfo(r, true)) { PRINTS(L"failed"); return; }
		if(str::IsEmpty(r.tag)) {
			b.AppendBSTR(r.text);
		} else {
			_HtmlAppendHead(b, r.tag);
			_ChromeComposeInnerHTML(b, r.childCount);
			_HtmlAppendTail(b, r.tag);
		}
	}

	void _HtmlAppendHead(str::StringBuilder& b, STR tag)
	{
		b << '<'; b << tag;
		int n;
		BstrNameValue* a = GetAttributes(out n);
		if(a) {
			for(int i = 0; i < n; i++) {
				b << ' '; b.AppendBSTR(a[i].name); b << '='; b << '\"'; b.AppendBSTR(a[i].value); b << '\"';
			}
			delete[] a;
		}
		b << '>';
	}

	void _HtmlAppendTail(str::StringBuilder& b, STR tag)
	{
		b << '<'; b << '/'; b << tag; b << '>';
	}

	ISimpleDOMNode* _FindChild(int childCount, int nodeType, STR tag, int lenT, out int& childChildCount)
	{
		//search in reverse order, because usually what we need is the last child.
		//	Document often has 2 children: doctype and HTML.
		//	HTML usually has 2 children: HEAD and BODY.
		for(int i = childCount; i > 0; i--) {
			FFNode child;
			if(0 != _x->get_childAt(i - 1, &child._x)) continue;
			NodeInfo info;
			if(child.GetNodeInfo(info, false)
				&& info.nodeType == nodeType
				&& info.tag.Equals(tag, lenT, true)
				) {
				childChildCount = info.childCount;
				return child.Detach();
			} else PRINTS(L"failed");
		}
		childChildCount = 0;
		return null;
	}

	BSTR _FirefoxGetBodyHtml(int childCount, bool outer)
	{
		int cc2, cc3;
		FFNode childHTML(_FindChild(childCount, NODETYPE_ELEMENT, L"HTML", 4, out cc2));
		if(childHTML) {
			//get BODY, not whole HTML. Like IE and Chrome.
			FFNode childBODY(childHTML._FindChild(cc2, NODETYPE_ELEMENT, L"BODY", 4, out cc3));
			if(childBODY) {
				if(outer) return childBODY.GetOuterHTML();
				BSTR s = null;
				if(0 == childBODY->get_innerHTML(&s)) return s;
			}
		}
		return null;
	}
#pragma endregion
};

class _BrowserInterface
{
public:
	IEElem ie;
	FFNode ff;

	_BrowserInterface() { ZEROTHIS; }

	bool Init(IAccessible* iacc)
	{
		bool ok;
		static thread_local bool t_preferIE;
		if(t_preferIE) { //if previously was IE, now try IE first, to make faster
			ok = ie.FromAcc(iacc) || ff.FromAcc(iacc);
		} else {
			ok = ff.FromAcc(iacc) || ie.FromAcc(iacc);
		}
		t_preferIE = !!ie;
		return ok;
	}
};
} //namespace

//Gets/compares specified HTML attributes and returns true if all match.
//Returns false if cannot get HTML attributes, for example if this is not a HTML element, or if called not inproc.
//Supports Firefox, Chrome, Internet Explorer and apps that use their code.
//Names of HTML attributes must be with "@" prefix, like "@href". Other names are ignored.
bool AccMatchHtmlAttributes(IAccessible* iacc, NameValue* prop, int count)
{
	_BrowserInterface bi; bool isBI = false;
	for(int i = 0; i < count; i++) {
		STR name = prop[i].name;
		if(*name++ != '@') continue;
		if(!isBI && !(isBI = bi.Init(iacc))) return false;
		BSTR b = bi.ie ? bi.ie.GetAttribute(name) : bi.ff.GetAttribute(name);
		bool yes = prop[i].value.Match(b ? b : L"", b ? SysStringLen(b) : 0);
		if(b) SysFreeString(b);
		if(!yes) return false;
	}
	return true;
}

HRESULT AccWeb(IAccessible* iacc, STR what, out BSTR& sResult)
{
	//Perf.First();
	_BrowserInterface bi;
	if(!bi.Init(iacc)) return E_NOINTERFACE;
	//Perf.Next();
	if(what[0] == '\'') {
		switch(what[1]) {
		case 'o': //outer HTML
			if(bi.ff) sResult = bi.ff.GetOuterHTML();
			else if(0 != bi.ie->get_outerHTML(&sResult)) return 1;
			break;
		case 'i': //inner HTML
			if(bi.ff) sResult = bi.ff.GetInnerHTML();
			else if(0 != bi.ie->get_innerHTML(&sResult)) return 1;
			break;
		case 'a': { //attributes
			int n;
			auto a = bi.ie ? bi.ie.GetAttributes(out n) : bi.ff.GetAttributes(out n);
			//Perf.Next();
			if(a) {
				str::StringBuilder b;
				for(int i = 0; i < n; i++) {
					BstrNameValue& r = a[i];
					if(r.name.Length() == 0) continue;
					//if(bi.ie && r.value.Length() == 0 && r.name == L"shape") continue; //somehow IE adds attributes that don't exist in the HTML
					b.AppendBSTR(r.name); b << '='; b.AppendBSTR(r.value); b << '\0';
				}
				delete[] a;
				sResult = b.ToBSTR();
			}
		} break;
		case 's': { //scroll
			if(bi.ff) {
				//Chrome 63 bug: ISimpleDOMNode::scrollTo does not work normally. Fixed in Chrome 65.
				//Workaround:
				//bool done = false; IAccessible2* ia2 = null; HWND w; RECT r;
				//if(0 == bi.ff->QueryInterface(&ia2)) { //info: iacc->QI works with FF but not with Chrome (need QS)
				//	if(0 == ia2->get_windowHandle(&w) && wn::ClassNameIs(w, L"Chrome_RenderWidgetHostHWND")) {
				//		done = GetWindowRect(w, &r) && 0 == ia2->scrollToPoint(IA2CoordinateType::IA2_COORDTYPE_SCREEN_RELATIVE, r.left, r.top); //used to work, but now always scrolls to the very bottom-right
				//		//Print(ia2->scrollTo(IA2ScrollType::IA2_SCROLL_TYPE_TOP_EDGE)); //does not work in Chrome. This and others work well in Firefox.
				//		//Print(ia2->scrollToPoint(IA2CoordinateType::IA2_COORDTYPE_PARENT_RELATIVE, 20, 0)); //works, but: 1. Need to calculate. 2. Can stop working too.
				//	}
				//	ia2->Release();
				//	if(done) break;
				//}

				if(0 != bi.ff->scrollTo(true)) return 1;
			} else {
				if(0 != bi.ie->scrollIntoView(_variant_t(true))) return 1;
			}
		} break;
		default: assert(false); return (HRESULT)eError::InvalidParameter;
		}
	} else { //attribute
		sResult = bi.ie ? bi.ie.GetAttribute(what) : bi.ff.GetAttribute(what);
		//note: for missing attributes Chrome/IE return null, but Firefox "".
	}
	//Perf.NW();
	return 0;
}

namespace outproc
{
//what - "'o" outer HTML, "'i" inner HTML, "'a" attributes, "'s" scroll, "attributeName".
EXPORT HRESULT Cpp_AccWeb(Cpp_Acc a, STR what, out BSTR& sResult)
{
	sResult = null;
	if(!(a.misc.flags&eAccMiscFlags::InProc)) return E_NOINTERFACE;
	if(a.elem) return 1; //eg TEXT of LINK in IE. Let use the LINK instead.

	InProcCall c;
	auto len = str::Len(what);
	auto memSize = sizeof(MarshalParams_Header) + (len + 1) * 2;
	auto p = c.AllocParams(&a, InProcAction::IPA_AccGetHtml, memSize);
	auto s = (LPWSTR)(p + 1); memcpy(s, what, len * 2); s[len] = 0;
	HRESULT hr = c.Call();
	if(hr) return hr;
	sResult = c.DetachResultBSTR();
	return 0;
}
}
