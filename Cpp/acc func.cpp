#include "stdafx.h"
#include "cpp.h"
#include "acc.h"

void Cpp_Acc::SetRoleByte() { misc.roleByte = ao::GetRoleByte(acc, elem); }

#pragma region navigate

namespace {

	struct NavdirAndCount {
		int navDir, count;
	};

	//Converts navigation string to NavdirAndCount[n] array. Will need to delete[] it.
	//Positive values - NAVDIR_X
	//Returns null if s is invalid, eg contains unknown strings or invalid count.
	NavdirAndCount* Navig_Parse(STR s, out int& n) {
		auto len = str::Len(s); if (len < 2) return null;
		STR eos = s + len;
		n = (int)std::count(s, eos, ' ') + 1;
		auto a = new NavdirAndCount[n];
		int i = 0;
		for (STR start = s; s <= eos; ) {
			if (*s == ' ' || s == eos) {
				int navDir, count; STR s2, s3;
				if (*start == '#') { //custom, or by numeric value
					navDir = strtoi(++start, (LPWSTR*)&s2);
					if (s2 == start) goto ge;
				} else {
					//find the end of the name part, because it can be followed by a number, like "child3" or ne,3"
					s2 = start; while (s2 < s && *s2 >= 'a' && *s2 <= 'z') s2++;
					navDir = str::Switch(start, s2 - start, { L"ne", L"pr", L"fi", L"la", L"pa", L"ch", L"next", L"previous", L"first", L"last", L"parent", L"child" });
					if (navDir > 0) navDir += (navDir < 7) ? 4 : -2;
					else { //rarely supported/used
						navDir = str::Switch(start, s2 - start, { L"up", L"do", L"le", L"ri", L"down", L"left", L"right" });
						if (navDir == 0) goto ge;
						if (navDir >= 5) navDir -= 3;
					}
				}
				if (s2 < s) {
					if (*s2 == ',') s2++;
					count = strtoi(s2, (LPWSTR*)&s3);
					if (s3 != s || count == 0 || (count < 0 && navDir != NAVDIR_CHILD)) goto ge;
				} else count = 1;
				NavdirAndCount x = { navDir, count };
				a[i++] = x;
				start = ++s;
			} else s++;
		}
		assert(i == n);
		//for(int i = 0; i < n; i++) Printf(L"%i %i", a[i].navDir, a[i].count);
		return a;
	ge:
		delete[] a;
		n = 0;
		return null;
	}

	//Compares an AO with other AOs using some properties - rectangle, role.
	struct AccComparer {
	private:
		RECT _rect;
		Bstr _roleStr;
		int _roleInt;
		int _inited; //0 no, 1 yes, -1 failed
	public:
		AccComparer() noexcept { ZEROTHIS; }

		//Gets properties of the AO that will be compared with other AOs.
		//Does nothing if already done (can be called multiple times).
		//Returns false if failed to get properties.
		bool Init(AccRaw a) {
			if (_inited == 0) {
				_inited = -1;
				if (0 != a.accLocation(out _rect) || IsRectEmpty(&_rect)) return false;
				if (!a.GetRoleIntOrString(out _roleInt, out _roleStr.m_str)) return false;
				_inited = 1;
			}
			return _inited == 1;
		}

		//Compares properties of another AO with properties of the Init AO.
		//Returns true if they match.
		//Init must be called and succeeded (asserts).
		bool Match(AccRaw a) {
			assert(_inited == 1); if (_inited != 1) return false;
			RECT rect2;
			if (0 != a.accLocation(out rect2) || !EqualRect(&rect2, &_rect)) return false;
			int ri; Bstr rs; if (!a.GetRoleIntOrString(out ri, out rs.m_str)) return false;
			if (ri != _roleInt) return false;
			if (rs && !rs.Equals(_roleStr, false)) return false;
			return true;
		}
	};

	bool Navig_Alt(AccContext& context, int navDir, AccRaw af, out AccRaw& ar) {
		ar.acc = null; ar.elem = 0;
		bool R = false;
		if (navDir == NAVDIR_FIRSTCHILD || navDir == NAVDIR_LASTCHILD) {
			if (af.elem != 0) return false;
			AccChildren c(ref context, ref af, navDir == NAVDIR_FIRSTCHILD ? 1 : -1, true);
			R = c.GetNext(out ar);
		} else if (navDir == NAVDIR_NEXT || navDir == NAVDIR_PREVIOUS) {

			//Get parent, then enum its children to find af by rect/role and get next.
			//	note: cannot compare IAccessibles, they always different.

			Cpp_Acc aParent;
			bool releaseParent;
			if (af.elem != 0) {
				aParent.acc = af.acc;
				releaseParent = false;
				//never mind: faster would be just to use af.elem-1 or af.elem+1 (with get_accChildCount) and try get_accChild. But less reliable (for some AO get_accChild fails). Anyway, non-0 af.elem is quite rare.
			} else {
				if (0 != ao::get_accParent(af.acc, out aParent.acc)) return false;
				releaseParent = true;
				//note: will get wrong AO if get_accParent is broken.
				//	For example WinForms TOOLBAR gets parent WINDOW which does not exist in the tree (must get parent of that WINDOW).
				//		Then this func gets WINDOW's child MENUBAR or SCROLLBAR which does not exist in the tree.
				//		Noticed it in our editor.
				//	We cannot detect/workaround it, or it would be too difficult/unreliable/slow. Better let the user try another way.
			}

			int retry = false; int foundAt = -1;
		g1:
			{
				AccChildren c(ref context, ref aParent, 0, false, navDir == NAVDIR_PREVIOUS);
				AccComparer acomp;
				for (int i = 0;; i++) {
					AccDtorIfElem0 t;
					if (!c.GetNext(out t)) break;
					if (foundAt < 0) {
						if (!acomp.Init(af)) break;
						if (!acomp.Match(t)) continue;
						foundAt = i;
					} else {
						if (t.acc == aParent.acc) releaseParent = false;
						ar = t; t.acc = null;
						R = true;
						break;
					}
				}
			}

			//Workaround for bugs of some AO: get_accParent returns WINDOW that does not exist in the tree.
			//Try with parent of aParent.acc.
			//When foundAt==0, aParent.acc has single child, and it is af.
			if (!R && !retry && foundAt == 0 && af.elem == 0 && ao::GetRoleByte(aParent.acc) == ROLE_SYSTEM_WINDOW) {
				IAccessible* p2;
				if (0 == ao::get_accParent(aParent.acc, out p2)) {
					aParent.acc->Release(); aParent.acc = p2;
					retry = true; goto g1;
				}
			}
			if (releaseParent) aParent.acc->Release();
		}
		//if(R) Print("<><c 0x8000>" + t.a.ToString(t.elem) + "</c>");
		return R;
	}

	HRESULT Navig_Parent(const ref Cpp_Acc& af, ref Cpp_Acc& ar) {
		if (af.elem != 0) {
			ar.acc = af.acc; //caller must AddRef if need
		} else {
			HRESULT hr = ao::get_accParent(af.acc, out ar.acc);
			if (hr != 0) return hr;
		}
		ar.misc.flags = af.misc.flags & eAccMiscFlags::InheritMask; //InProc | UIA | Java
		if (af.elem == 0 && !!(af.misc.flags & eAccMiscFlags::Java) && *(void**)ar.acc != *(void**)af.acc) ar.misc.flags &= ~eAccMiscFlags::Java; //"frame" -> WINDOW
		return 0;
	}

	HRESULT Navig_Hresult(HRESULT hr) {
		switch (hr) {
		case DISP_E_MEMBERNOTFOUND: case E_NOTIMPL: case E_INVALIDARG: case E_FAIL: case E_NOINTERFACE: hr = 1; break;
		}
		return hr;
	}

	HRESULT Navig_Step(AccContext& context, int navDir, int childIndex, AccRaw af, out AccRaw& ar) {
		ar.acc = null; ar.elem = 0;

		if (navDir == NAVDIR_PARENT) return Navig_Parent(ref af, ref ar);

		if (af.elem != 0) if (navDir == NAVDIR_CHILD || navDir == NAVDIR_FIRSTCHILD || navDir == NAVDIR_LASTCHILD) return 1;

		HRESULT hr = 0;
		if (navDir == NAVDIR_CHILD) {
			AccChildren c(ref context, ref af, childIndex, true);
			if (!c.GetNext(out ar)) hr = 1;
			//note: for it cannot be used get_accChild. Its purpose is different. It accepts child id, not child index, which may be not the same.
		} else {
			hr = af.Navigate(navDir, out ar);
			if (hr != 0 && !(af.misc.flags & (eAccMiscFlags::UIA | eAccMiscFlags::Java))) {
				//Perf.First();
				if (Navig_Alt(ref context, navDir, af, out ar)) hr = 0;
				//Perf.NW();
			}
		}
		assert((hr != 0) == (ar.acc == null));
		if (hr == 0) {
			ar.misc.flags = af.misc.flags & eAccMiscFlags::InheritMask; //InProc | UIA | Java
		}
		return hr;
	}

} //namespace

HRESULT AccNavigate(Cpp_Acc aFrom, STR navig, out Cpp_Acc& aResult) {
	aResult.Zero();
	int n;
	auto a = Navig_Parse(navig, out n);
	if (a == null) return (HRESULT)eError::InvalidParameter;

	HRESULT hr = 0;
	AccContext context;
	AccRaw af(aFrom), ar;
	for (int i = 0; i < n; i++) {
		NavdirAndCount x = a[i];
		for (int nTimes = (x.navDir == NAVDIR_CHILD) ? 1 : x.count; nTimes > 0; nTimes--, af = ar) {
			hr = Navig_Step(ref context, x.navDir, x.count, af, out ar);
			if (af.acc != aFrom.acc && af.acc != ar.acc) af.acc->Release(); //release intermediate AOs
			if (hr != 0) goto gBreak;
		}
	}
gBreak:
	delete a;
	//ar.PrintAcc();
	if (hr != 0) return Navig_Hresult(hr);
	if (ar.acc == aFrom.acc) ar.acc->AddRef(); //"pa" when aFrom.elem!=0, or eg "fi" when ar.elem!=0
	aResult = ar;
	return 0;
}

namespace outproc {
	EXPORT HRESULT Cpp_AccNavigate(Cpp_Acc aFrom, STR navig, out Cpp_Acc& aResult) {
		HRESULT hr;

		//optimize "pa". Eg used by elm.Parent.
#if true //only if elem!=0
		if (aFrom.elem != 0 && navig[0] == 'p' && navig[1] == 'a' && (navig[2] == 0 || 0 == wcscmp(navig, L"parent"))) {
			aResult.Zero();
			Navig_Parent(ref aFrom, ref aResult);
			aFrom.acc->AddRef();
			return 0;
		}
#else //does not work with AccessibleMarshalWrapper: the returned object isn't wrapped and therefore invalid. And somehow slower (tested only with Chrome).
		if (navig[0] == 'p' && navig[1] == 'a' && (navig[2] == 0 || 0 == wcscmp(navig, L"parent"))) {
			aResult.Zero();
			hr = Navig_Parent(ref aFrom, ref aResult);
			if (hr != 0) return Navig_Hresult(hr);
			if (aResult.acc == aFrom.acc) aFrom.acc->AddRef();
			return hr;
		}
#endif

		if (!(aFrom.misc.flags & eAccMiscFlags::InProc)) {
			hr = AccNavigate(aFrom, navig, aResult);
		} else {
			aResult.Zero();
			InProcCall ic;
			auto len = str::Len(navig);
			auto memSize = sizeof(MarshalParams_AccElem) + (len + 1) * 2;
			auto p = (MarshalParams_AccElem*)ic.AllocParams(&aFrom, InProcAction::IPA_AccNavigate, memSize);
			p->elem = aFrom.elem;
			auto s = (LPWSTR)(p + 1); memcpy(s, navig, len * 2); s[len] = 0;
			hr = ic.Call();
			if (hr) return hr;
			hr = ic.ReadResultAcc(ref aResult);
		}
		return hr;
	}
}

#pragma endregion

#pragma region get prop

HRESULT AccWeb(IAccessible* iacc, STR what, out BSTR& sResult);

HRESULT AccGetProp(Cpp_Acc a, WCHAR prop, out BSTR& sResult) {
	sResult = null;
	auto acc = a.acc;
	_variant_t v; ao::VE ve(a.elem);
	STR role; long state, cc; RECT rect; HWND w;
	HRESULT hr = 0;

	switch (prop) {
	case 'o': case 'i': case '@':
		if (a.elem || (a.misc.flags & (eAccMiscFlags::InProc | eAccMiscFlags::UIA | eAccMiscFlags::Java)) != eAccMiscFlags::InProc) return 1;
		break;
	}

	switch (prop) {
	case 'R':
		hr = acc->get_accRole(ve, &v); if (hr != 0) break;
		role = ao::RoleToString(ref v);
		sResult = v.vt == VT_BSTR ? v.Detach().bstrVal : SysAllocString(role);
		break;
	case 'n': hr = acc->get_accName(ve, &sResult); break;
	case 'v': hr = acc->get_accValue(ve, &sResult); break;
	case 'd': hr = acc->get_accDescription(ve, &sResult); break;
	case 'h': hr = acc->get_accHelp(ve, &sResult); break;
	case 'u': //uiaid
	case 'U': //uiacn
		if (!(a.misc.flags & eAccMiscFlags::UIA)) return 1;
		ve.vt = VT_I1; ve.cVal = (char)prop; hr = acc->get_accHelp(ve, &sResult);
		break;
	case 'a': hr = acc->get_accDefaultAction(ve, &sResult); break;
	case 'k': hr = acc->get_accKeyboardShortcut(ve, &sResult); break;
	case 's':
		hr = ao::get_accState(out state, acc, a.elem);
		if (hr == 0) sResult = SysAllocStringByteLen((LPCSTR)&state, 4);
		break;
	case 'r':
	case 'D':
		hr = ao::accLocation(out rect, acc, a.elem);
		if (hr == 0) {
			if (prop == 'D' && !!(a.misc.flags & eAccMiscFlags::InProc)) {
				DpiElmScaling des(true, 0, acc);
				des.ScaleIfNeed(ref rect);
			}
			sResult = SysAllocStringByteLen((LPCSTR)&rect, 16);
		}
		break;
	case 'c':
		if (a.elem) return 1;
		hr = acc->get_accChildCount(out & cc);
		if (hr == 0) sResult = SysAllocStringByteLen((LPCSTR)&cc, 4);
		break;
	case 'w':
		hr = WindowFromAccessibleObject(acc, &w);
		if (hr == 0) sResult = SysAllocStringByteLen((LPCSTR)&w, 4);
		break;
	case 'o':
		hr = AccWeb(acc, L"'o", out sResult);
		break;
	case 'i':
		hr = AccWeb(acc, L"'i", out sResult);
		break;
	case '@':
		hr = AccWeb(acc, L"'a", out sResult);
		break;
	default: assert(false); hr = (HRESULT)eError::InvalidParameter;
	}

	if (hr != 0) {
		if (sResult != null) { SysFreeString(sResult); sResult = null; } //rare, but noticed few
	switch (hr) { case DISP_E_MEMBERNOTFOUND: case E_NOTIMPL: hr = 1; break; }
											//DISP_E_MEMBERNOTFOUND: many many. E_NOTIMPL: many.
											//note: 0x80070005 (access denied) when trying to get value of a password field.
	}
	return hr;
}

HRESULT AccGetProps(Cpp_Acc a, STR props, out BSTR& sResult) {
	sResult = null;
	str::StringBuilder s;
	s.AppendChar(0, (int)str::Len(props) * 4); //reserve for offsets
	for (int i = 0; *props; props++, i++) {
		Bstr b;
		HRESULT hr = AccGetProp(a, *props, out b.m_str);
		((int*)(LPWSTR)s)[i] = (int)s.Length(); //offset
		if (hr) {
			if (hr == (HRESULT)eError::InvalidParameter) return hr;
			continue;
		}
		s.AppendBSTR(b);
	}
	sResult = s.ToBSTR();
	return 0;
}

namespace outproc {
	//note: don't need inproc to call methods of AO that were found inproc.
	//	Call simply. Everything works as if we explicitly call inproc.
	//	Same speed, no bugs (toolbar button name, focusing standard controls).
	//	Call inproc only if may need multiple calls (Cpp_AccNavigate, Cpp_AccGetProps) or if works only inproc (Cpp_AccWeb).

	//this is called only for standard string props, like Name, Value. Not for rect, HTML attributes, etc.
	EXPORT HRESULT Cpp_AccGetStringProp(Cpp_Acc a, WCHAR prop, out BSTR& sResult) {
		return ::AccGetProp(a, prop, sResult);
	}

	EXPORT HRESULT Cpp_AccGetProps(Cpp_Acc a, STR props, out BSTR& sResult) {
		if (!(a.misc.flags & eAccMiscFlags::InProc)) return AccGetProps(a, props, out sResult);

		sResult = null;
		InProcCall ic;
		auto len = str::Len(props);
		auto memSize = sizeof(MarshalParams_AccElem) + (len + 1) * 2;
		auto p = (MarshalParams_AccElem*)ic.AllocParams(&a, InProcAction::IPA_AccGetProps, memSize);
		p->elem = a.elem;
		auto s = (LPWSTR)(p + 1); memcpy(s, props, len * 2); s[len] = 0;
		HRESULT hr = ic.Call();
		if (hr) return hr;
		sResult = ic.DetachResultBSTR();
		return 0;
	}

	//If a found inproc, gets raw rect (DPI-unscaled). To get physical rect use Cpp_AccGetProps('D').
	EXPORT HRESULT Cpp_AccGetRect(Cpp_Acc a, out RECT& r) {
		return ao::accLocation(out r, a.acc, a.elem);
	}

	EXPORT HRESULT Cpp_AccGetRole(Cpp_Acc a, out int& roleInt, out BSTR& roleStr) {
		roleInt = 0; roleStr = null;
		assert(a.misc.roleByte == 0 || a.misc.roleByte == ROLE_CUSTOM);
		_variant_t vr;
		HRESULT hr = ao::GetRoleIntAndVariant(a.acc, a.elem, out roleInt, out vr);
		if (hr == 0 && vr.vt == VT_BSTR) {
			ao::RoleToString(ref vr); //lcase if need
			roleStr = vr.Detach().bstrVal;
		}
		return hr;
	}

	EXPORT HRESULT Cpp_AccGetInt(Cpp_Acc a, WCHAR what, out long& R) {
		R = 0;
		switch (what) {
		case 's': return ao::get_accState(out R, a.acc, a.elem);
		case 'c': return a.acc->get_accChildCount(&R);
		case 'w': goto g1;
		}
		return E_INVALIDARG;
	g1:
		//Outproc WindowFromAccessibleObject is very slow when the AO retrieved using navigation.
		//	Then MSAA walks ancestors until finds WINDOW. Makes many RPC if outproc.

		if (!(a.misc.flags & eAccMiscFlags::InProc)) {
			HWND w; HRESULT hr = WindowFromAccessibleObject(a.acc, &w);
			if (hr == 0) R = (long)(LPARAM)w;
			return hr;
		}

		InProcCall ic;
		ic.AllocParams(&a, InProcAction::IPA_AccGetWindow, sizeof(MarshalParams_Header));
		HRESULT hr = ic.Call();
		if (hr) return hr;
		R = *(long*)ic.GetResultBSTR();
		return 0;
	}

	namespace {
		void AGS_Add(Cpp_Acc a, ref VARIANT& v, ref CSimpleArray<AccRaw>& t) {
			AccRaw r;
			if (0 == r.FromVARIANT(a.acc, ref v, true)) {
				if (r.elem) r.acc->AddRef();
				r.misc.flags = a.misc.flags & eAccMiscFlags::InheritMask;
				t.Add(r);
			}
		}
	}

	EXPORT HRESULT Cpp_AccGetSelection(Cpp_Acc a, out BSTR& sResult) {
		//info: here we don't call inproc because usually the speed is not important.

		sResult = null;
		assert(a.elem == 0);
		_variant_t v;
		HRESULT hr = a.acc->get_accSelection(&v);
		if (hr == 0 && v.vt != 0) {
			CSimpleArray<AccRaw> t;
			if (v.vt == VT_UNKNOWN) {
				Smart<IEnumVARIANT> e;
				if (0 == v.punkVal->QueryInterface(&e)) {
					for (;;) {
						_variant_t vv; ULONG n;
						if (e->Next(1, &vv, &n) || n != 1) break;
						AGS_Add(a, ref vv, ref t);
					}
				}
			} else {
				AGS_Add(a, ref v, ref t);
			}
			if (t.GetSize() > 0) sResult = SysAllocStringLen((STR)t.GetData(), t.GetSize() * sizeof(AccRaw));
		}

		return hr;
	}
} //namespace outproc

#pragma endregion

#pragma region actions

namespace outproc {
	//action: 'a' accDoDefaultAction, 'v' put_accValue(param), 's' scrollto (only UIA)
	EXPORT HRESULT Cpp_AccAction(Cpp_Acc a, WCHAR action, BSTR param) {
		VARIANT ve = {}; ve.vt = VT_I4; ve.lVal = a.elem;
		HRESULT hr;
		switch (action) {
		case 'a':
			if (param) { //Java action
				assert(!!(a.misc.flags & eAccMiscFlags::Java));
				ve.vt = VT_BSTR; ve.bstrVal = param;
			}
			hr = a.acc->accDoDefaultAction(ve);
			break;
		case 'v':
			hr = a.acc->put_accValue(ve, param);
			break;
		case 's': //ScrollTo
		case 'c': //Check
		case 'E': //Expand(true)
		case 'e': //Expand(false)
			assert(!!(a.misc.flags & eAccMiscFlags::UIA));
			ve.vt = VT_I1; ve.cVal = (char)action;
			hr = a.acc->accDoDefaultAction(ve);
			break;
		default: assert(false); hr = (HRESULT)eError::InvalidParameter;
		}

		if (hr == DISP_E_MEMBERNOTFOUND) hr = 1;
		return hr;
	}

	EXPORT HRESULT Cpp_AccSelect(Cpp_Acc a, long flagsSelect) {
		return a.acc->accSelect(flagsSelect, ao::VE(a.elem));
	}
}

#pragma endregion
