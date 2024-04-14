#pragma once
#include "stdafx.h"
#include "internal.h"

#define	ROLE_MAX 0x40 //ROLE_SYSTEM_OUTLINEBUTTON
#define	ROLE_CUSTOM 0xFF

//IAccessible helpers. All functions are static; use this class like a namespace.
//Other helpers are in AccRaw. This class contains functions that don't depend on AccRaw.
namespace ao {
	//VARIANT(VT_I4) for IAccessible function parameters.
	struct VE : VARIANT {
		VE() { vt = VT_I4; lVal = 0; }
		VE(long elem) { vt = VT_I4; lVal = elem; }
	};

	//Calls QueryInterface to get IAccessible from IDispatch idisp. Releases idisp.
	//idisp - if null, returns E_FAIL.
	static HRESULT IDispatchToIAccessible(IDispatch* idisp, out IAccessible*& iacc) {
		iacc = null;
		if (idisp == null) return E_FAIL; //note: no assert
		HRESULT hr = idisp->QueryInterface(IID_IAccessible, (void**)&iacc);
		idisp->Release();
		if (hr == 0 && iacc == null) hr = E_FAIL;
		return hr;
	}

	//Calls IAccessible::get_accParent and IDispatchToIAccessible.
	static HRESULT get_accParent(IAccessible* acc, out IAccessible*& aParent) {
		assert(acc != null); if (acc == null) return E_FAIL;

		aParent = null;
		IDispatch* idisp = null;
		HRESULT hr = acc->get_accParent(out & idisp);
		if (hr == 0) hr = IDispatchToIAccessible(idisp, out aParent);
		return hr;
	}

	//Calls IAccessible::get_accChild and IDispatchToIAccessible.
	//elem must not be 0.
	static HRESULT get_accChild(IAccessible* acc, long elem, out IAccessible*& aChild) {
		assert(!(acc == null || elem == 0)); if (acc == null || elem == 0) return E_FAIL;

		aChild = null;
		IDispatch* idisp = null;
		HRESULT hr = acc->get_accChild(VE(elem), &idisp);
		if (hr == 0) hr = IDispatchToIAccessible(idisp, aChild);
		return hr;
	}

	//Gets role (get_accRole) as int (raw) and VARIANT.
	//roleInt - 0 if failed or not VT_I4.
	static HRESULT GetRoleIntAndVariant(IAccessible* acc, long elem, out int& roleInt, out _variant_t& roleVariant) {
		assert(roleVariant.vt == 0);
		roleInt = 0;
		HRESULT hr = acc->get_accRole(VE(elem), &roleVariant);
		if (hr != 0) PRINTF(L"failed to get role.  hr=0x%X  elem=%i", hr, elem);
		else if (roleVariant.vt == VT_I4) {
			roleInt = roleVariant.lVal;
		} else if (roleVariant.vt == VT_BSTR) {
			if (!roleVariant.bstrVal) { roleVariant.vt = 0; hr = 1; }
		} else hr = 1;
		return hr;
	}

	//Gets role (get_accRole) as BYTE.
	//Returns 0 if failed. Returns ROLE_CUSTOM (0xFF) if custom (VT_BSTR or not 1-ROLE_MAX).
	static BYTE GetRoleByte(IAccessible* acc, long elem = 0) {
		_variant_t v;
		HRESULT hr = acc->get_accRole(VE(elem), &v);
		if (hr != 0) PRINTF(L"failed to get role.  hr=0x%X  elem=%i", hr, elem);
		else if (v.vt == VT_I4) return v.lVal > 0 && v.lVal <= ROLE_MAX ? (BYTE)v.lVal : ROLE_CUSTOM;
		else if (v.vt == VT_BSTR && v.bstrVal) return ROLE_CUSTOM;
		return 0;
	}

	//Converts VARIANT role to string.
	//If VT_BSTR, returns role.bstrVal. If all chars ucase, makes lcase.
	//If VT_I4: If its a standard role, returns a static const string. Else calls VariantChangeType(role) and returns role.bstrVal.
	//Returns L"" if failed. Never null.
	static STR RoleToString(ref VARIANT& role) {
		static const STR s_roles[] = { L"0", L"TITLEBAR", L"MENUBAR", L"SCROLLBAR", L"GRIP", L"SOUND", L"CURSOR", L"CARET", L"ALERT", L"WINDOW", L"CLIENT", L"MENUPOPUP", L"MENUITEM", L"TOOLTIP", L"APPLICATION", L"DOCUMENT", L"PANE", L"CHART", L"DIALOG", L"BORDER", L"GROUPING", L"SEPARATOR", L"TOOLBAR", L"STATUSBAR", L"TABLE", L"COLUMNHEADER", L"ROWHEADER", L"COLUMN", L"ROW", L"CELL", L"LINK", L"HELPBALLOON", L"CHARACTER", L"LIST", L"LISTITEM", L"TREE", L"TREEITEM", L"PAGETAB", L"PROPERTYPAGE", L"INDICATOR", L"IMAGE", L"STATICTEXT", L"TEXT", L"BUTTON", L"CHECKBOX", L"RADIOBUTTON", L"COMBOBOX", L"DROPLIST", L"PROGRESSBAR", L"DIAL", L"HOTKEYFIELD", L"SLIDER", L"SPINBUTTON", L"DIAGRAM", L"ANIMATION", L"EQUATION", L"BUTTONDROPDOWN", L"BUTTONMENU", L"BUTTONDROPDOWNGRID", L"WHITESPACE", L"PAGETABLIST", L"CLOCK", L"SPLITBUTTON", L"IPADDRESS", L"TREEBUTTON" };
		static_assert(sizeof(s_roles) / sizeof(STR) == ROLE_MAX + 1);
		STR R = null; size_t i;
	g1:
		switch (role.vt) {
		case VT_BSTR:
			R = role.bstrVal;
			if (R != null) { //lcase if need, to distinguish from standard roles
				BSTR b = role.bstrVal;
				int i, len = SysStringLen(b);
				for (i = 0; i < len; i++) {
					WCHAR c = b[i]; if (c < 'A' || c > 'Z') break;
				}
				if (i == len) { //all ucase
					for (i = 0; i < len; i++) b[i] += 32;
				}
			}
			break;
		case VT_I4:
			i = role.lVal;
			if (i < _countof(s_roles)) return s_roles[i];
			if (0 == VariantChangeType(&role, &role, 0, VT_BSTR)) goto g1;
			break;
		case 0: break; //failed to get role
		default: PRINTF(L"role.vt=%i", role.vt);
		}
		return R ? R : L"";
	}

	static int StateFromString(STR s, size_t lenS) {
		static const STR s_states[] = { L"DISABLED", L"SELECTED", L"FOCUSED", L"PRESSED", L"CHECKED", L"INDETERMINATE", L"READONLY", L"HOTTRACKED", L"DEFAULT", L"EXPANDED", L"COLLAPSED", L"BUSY", L"FLOATING", L"MARQUEED", L"ANIMATED", L"INVISIBLE", L"OFFSCREEN", L"SIZEABLE", L"MOVEABLE", L"SELFVOICING", L"FOCUSABLE", L"SELECTABLE", L"LINKED", L"TRAVERSED", L"MULTISELECTABLE", L"EXTSELECTABLE", L"ALERT_LOW", L"ALERT_MEDIUM", L"ALERT_HIGH", L"PROTECTED", L"HASPOPUP", };
		if (lenS >= 4) {
			__int64 fourChars = *(__int64*)s;
			for (int i = 0; i < _countof(s_states); i++) {
				STR k = s_states[i];
				if (*(__int64*)k == fourChars && !wcsncmp(s, k, lenS) && k[lenS] == 0) return (1 << i);
			}
		}
		return 0;
	}

	//not used
	//Appends to b.
	//static void StateToString(int state, str::StringBuilder& b)
	//{
	//	bool appendedOnce = false;
	//	for(int i = 0; i < _countof(s_states); i++) {
	//		if(!(state & (1 << i))) continue;
	//		if(!appendedOnce) appendedOnce = true; else b << L", ";
	//		b << s_states[i];
	//	}
	//}

	static HRESULT get_accState(out long& state, IAccessible* iacc, long elem = 0) {
		state = 0;
		_variant_t v;
		HRESULT hr = iacc->get_accState(VE(elem), out & v);
		if (hr == 0 && v.vt == VT_I4) state = v.lVal;
		return hr;
	}

	static HRESULT accLocation(out RECT& r, IAccessible* iacc, long elem = 0) {
		long x, y, wid, hei;
		HRESULT hr = iacc->accLocation(&x, &y, &wid, &hei, VE(elem));
		if (hr != 0) x = y = wid = hei = 0;
		SetRect(&r, x, y, x + wid, y + hei);
		return hr;
	}

	static bool IsStatic(int role, IAccessible* iacc) {
		if (role == ROLE_SYSTEM_STATICTEXT || role == ROLE_SYSTEM_GRAPHIC) return true;
		if (role != ROLE_SYSTEM_TEXT) return false;
		long state = 0;
		return 0 == get_accState(state, iacc) && 0 == (state & (STATE_SYSTEM_FOCUSABLE | STATE_SYSTEM_UNAVAILABLE));
	}

#if TRACE
	static void PrintAcc(IAccessible* acc, long elem = 0, int level = 0) {
		_variant_t vr; HRESULT hr = acc->get_accRole(VE(elem), &vr);
		STR sr = (hr == 0 && (vr.vt == VT_I4 || (vr.vt == VT_BSTR && vr.bstrVal))) ? ao::RoleToString(ref vr) : L"<failed>";
		Bstr bn; STR sn = L""; if (0 == acc->get_accName(ao::VE(elem), &bn) && bn) sn = bn;

		Printf(L"<><c 0x80>%*s%s  \"%s\"</c>", level, L"", sr, sn);
	}
#endif

	//Temporarily sets SPI_SETSCREENREADER. Restores in dtor.
	//It enables accessible objects and UI automation elements (AO/AE) in OpenOffice and LibreOffice. Not tested with new versions, but works.
	class TempSetScreenReader {
		bool _restore;
	public:
		//Does not set SPI_SETSCREENREADER.
		TempSetScreenReader() noexcept { _restore = false; }

		//Calls Set() if w is not 0 and its classname matches "SAL*FRAME".
		TempSetScreenReader(HWND w) {
			_restore = false;
			if (w && wn::ClassNameIs(w, L"SAL*FRAME")) Set(w);
		}

		~TempSetScreenReader() {
			if (_restore) SystemParametersInfoW(SPI_SETSCREENREADER, 0, 0, 0);
		}

		//If SPI_GETSCREENREADER says false, sets SPI_SETSCREENREADER = true, and dtor will set it false.
		//note: Windows does not use a reference counting for this setting.
		//Does nothing if w has flag AccEnableYes. Adds this flag if need.
		void Set(HWND w) {
			if (!!(WinFlags::Get(w) & eWinFlags::AccEnabled)) return;
			int r = 0;
			SystemParametersInfoW(SPI_GETSCREENREADER, 0, &r, 0);
			_restore = r == 0 && SystemParametersInfoW(SPI_SETSCREENREADER, 1, 0, 0);
			if (_restore) WinFlags::Set(w, eWinFlags::AccEnabled);
		}
	};

	//Calls AccessibleObjectFromWindow. Uses TempSetScreenReader if w class name matches "SAL*FRAME".
	static HRESULT AccFromWindowSR(HWND w, DWORD objid, out IAccessible** a) {
		TempSetScreenReader tsr(w);
		return AccessibleObjectFromWindow(w, objid, IID_IAccessible, (void**)a);
	}

	//Calls AccessibleObjectFromWindow. Uses TempSetScreenReader if screenReader is true (does not check classname).
	static HRESULT AccFromWindow(HWND w, DWORD objid, out IAccessible** a, bool screenReader = false) {
		TempSetScreenReader tsr; if (screenReader) tsr.Set(w);
		return AccessibleObjectFromWindow(w, objid, IID_IAccessible, (void**)a);
	}
} //namespace ao

//IAccessible* and child element id.
//Has only ctors. Does not have a dtor (does not Release etc), operator=, etc.
//Like Cpp_Acc, but has methods and is internal.
struct AccRaw : public Cpp_Acc {
	AccRaw() noexcept : Cpp_Acc() {}

	//Does not AddRef.
	AccRaw(IAccessible* acc_, int elem_, eAccMiscFlags flags_ = (eAccMiscFlags)0) noexcept : Cpp_Acc(acc_, elem_, flags_) {}

	//Does not AddRef.
	AccRaw(ref const Cpp_Acc& x) noexcept : Cpp_Acc(x) {}

	//Calls Release (even if elem is not 0) and clears this variable.
	void Dispose() {
		if (acc != null) acc->Release();
		Zero();
	}

	bool IsEmpty() const { return acc == null && elem == 0; }

	HRESULT get_accState(out long& state) const {
		return ao::get_accState(out state, acc, elem);
	}

	HRESULT accLocation(out RECT& r) const {
		return ao::accLocation(out r, acc, elem);
	}

	//Gets a string property and calls w.Match.
	//If fails to get, uses L"".
	//propName: name, value, desc, help, action, key, uiaid, uiacn. Compares only the first character, and the last of uiaX.
	bool MatchStringProp(STR propName, const str::Wildex& w) const {
		Bstr b; STR s = L""; auto lens = 0;
		ao::VE ve(elem);
		HRESULT hr = 0;
		switch (propName[0]) {
		case 'n': hr = acc->get_accName(ve, &b); break;
		case 'v': hr = acc->get_accValue(ve, &b); break;
		case 'd': hr = acc->get_accDescription(ve, &b); break;
		case 'h': hr = acc->get_accHelp(ve, &b); break;
		case 'a': hr = acc->get_accDefaultAction(ve, &b); break;
		case 'k': hr = acc->get_accKeyboardShortcut(ve, &b); break;
		case 'u': //uiaid, uiacn
			ve.vt = VT_I1;
			ve.cVal = propName[4] == 'n' ? 'U' : 'u';
			hr = acc->get_accHelp(ve, &b);
			break;
		default: assert(false);
		}
		if (hr == 0 && b.m_str) { s = b; lens = b.Length(); }
		//Printf(L"0x%X %i %s", hr, lens, s);
		return w.Match(s, lens);
	}

#if _DEBUG
	void PrintAcc() const {
		ao::PrintAcc(acc, elem, misc.level);
	}
#endif

	//Gets child IAccessible/elem from IAccessible (parent) and VARIANT (child id).
	//If VT_DISPATCH, calls IDispatchToIAccessible, which gets IAccessible and releases the IDispatch. Sets elem=0.
	//If VT_I4, sets non-zero elem and sets acc=parent. Does not AddRef.
	//Clears the VARIANT (releases IDispatch etc, sets vt=0).
	//If vt is not VT_DISPATCH/VT_I4 or value is 0, asserts and returns E_FAIL.
	//This must be empty (assert(IsEmpty());).
	//tryGetObjectFromId - If VT_I4, try to call get_accChild. If it succeeds, sets elem = 0.
	HRESULT FromVARIANT(IAccessible* parent, ref VARIANT& v, bool tryGetObjectFromId = false) {
		assert(IsEmpty());
		int hr = 0;
		switch (v.vt) {
		case VT_DISPATCH:
			assert(v.pdispVal != null);
			hr = ao::IDispatchToIAccessible(v.pdispVal, out acc);
			break;
		case VT_I4:
			//case VT_UI4: //.NET 5 RC1 bug
			assert(v.lVal != 0);
			if (v.lVal == 0) hr = E_FAIL;
			else if (tryGetObjectFromId && 0 == ao::get_accChild(parent, v.lVal, out acc));
			else { acc = parent; elem = v.lVal; }
			break;
		default:
			VariantClear(&v);
			hr = E_FAIL;
			break;
		}
		v.vt = 0;
		PRINTF_IF(hr != 0, "0x%X", hr);
		return hr;
	}

	//Gets role (get_accRole) as BYTE.
	//Returns 0 if failed. Returns ROLE_CUSTOM (0xFF) if custom (VT_BSTR or not 1-ROLE_MAX).
	BYTE GetRoleByte() const {
		return ao::GetRoleByte(acc, elem);
	}

	//Gets role (get_accRole) as BYTE and VARIANT.
	//Returns 0 if failed. Returns ROLE_CUSTOM (0xFF) if custom (VT_BSTR or not 1-ROLE_MAX).
	BYTE GetRoleByteAndVariant(out _variant_t& varRole) const {
		int roleInt;
		if (0 != ao::GetRoleIntAndVariant(acc, elem, out roleInt, out varRole)) return 0;
		return roleInt > 0 && roleInt <= ROLE_MAX ? (BYTE)roleInt : ROLE_CUSTOM;
	}

	//Gets role (get_accRole) as raw int (VT_I4) or string (VT_BSTR).
	//If string, roleStr will be not null. Will need to free it.
	bool GetRoleIntOrString(out int& roleInt, out BSTR& roleStr) const {
		roleStr = null;
		_variant_t v;
		if (0 != ao::GetRoleIntAndVariant(acc, elem, out roleInt, out v)) return false;
		if (v.vt == VT_BSTR) roleStr = v.Detach().bstrVal;
		return true;
	}

	//Calls acc->accNavigate and gets IAccessible/elem from VARIANT using standard pattern which may involve get_accParent/get_accChild.
	//Does not support PARENT and CHILD (asserts). If NAVDIR_FIRSTCHILD or NAVDIR_LASTCHILD, elem must be 0 (asserts).
	//Does not set a.misc.flags.
	HRESULT Navigate(int navDir, out AccRaw& a) const {
		assert(!(navDir == NAVDIR_PARENT || navDir == NAVDIR_CHILD)); //our special navdirs
		assert(!(elem != 0 && (navDir == NAVDIR_FIRSTCHILD || navDir == NAVDIR_LASTCHILD)));

		a.acc = null; a.elem = 0;
		_variant_t v;
		int hr = acc->accNavigate(navDir, ao::VE(elem), out & v);
		if (hr == 0 && v.vt != 0) {
			if (elem == 0 && v.vt == VT_I4 && v.lVal != 0 && navDir >= NAVDIR_UP && navDir <= NAVDIR_PREVIOUS) {
				IAccessible* aParent;
				hr = ao::get_accParent(acc, out aParent);
				if (hr == 0) {
					hr = ao::get_accChild(aParent, v.lVal, out a.acc);
					if (hr == 0) aParent->Release();
					else { hr = 0; a.acc = aParent; a.elem = v.lVal; } //note: some AO return wrong childid. Then a will be invalid. Noticed only 1 such object. hr are various. Never mind.
				}
			} else {
				if (v.vt == VT_I4 && v.lVal == 0) PRINTS(L"AO bug: VT_I4 value 0"); //eg .NET submenu-item
				else hr = a.FromVARIANT(acc, ref v, true);
			}
		}
		if (hr == 0 && a.acc == null) hr = S_FALSE;
		return hr;
	}

private:
	/// Gets focused object, which can be a direct child or this.
	/// If neither a child or this is focused, sets a.acc=0 and a.elem=0. Returns 1.
	/// Else if focused is this, sets a.acc=acc and a.elem=0. Does not AddRef.
	/// Else if focused is a simple element, sets a.acc=this and a.elem!=0. Does not AddRef.
	/// Else sets a.acc=child and a.elem=0. Will need to release a.acc.
	HRESULT _get_accFocus(out AccRaw& a) {
		a.Zero();
		_variant_t v;
		HRESULT hr = acc->get_accFocus(out & v);
		if (hr == 0) {
			if (v.vt == 0) hr = 1;
			else if (v.vt == VT_I4 && v.lVal == 0) a.acc = acc;
			else hr = a.FromVARIANT(acc, ref v, true);
		}
		return hr;
	}
public:

	/// Gets focused descendant or this.
	/// Returns 1 if nothing is focused in this. Returns !0 if fails.
	/// If isThis receives true, does not AddRef. Else does AddRef even if ar.a==this.a.
	/// <param name="isThis">Receives true if ar is this.</param>
	HRESULT DescendantFocused(out AccRaw& ar, out bool& isThis, bool directChild = false) {
		isThis = false;
		int hr = _get_accFocus(out ar);
		if (hr != 0) return hr;
		if (ar.acc == acc) {
			if (ar.elem == elem) {
				isThis = true;
				return 0;
			}
			ar.acc->AddRef();
		} else if (!directChild) {
			AccRaw t; bool isThis2;
			if (0 == ar.DescendantFocused(out t, out isThis2) && !isThis2) {
				util::Swap(ref ar, ref t);
				t.Dispose();
			}
		}
		return 0;
	}

};

//The same as AccRaw, but has a dtor which releases the acc, but only if elem is 0. Also does not allow to copy.
class AccDtorIfElem0 : public AccRaw {
	AccDtorIfElem0(AccDtorIfElem0&&) = delete; //disable copying
public:

	AccDtorIfElem0() noexcept {}

	//Does not AddRef.
	AccDtorIfElem0(IAccessible* acc_, int elem_, eAccMiscFlags flags_ = (eAccMiscFlags)0) noexcept : AccRaw(acc_, elem_, flags_) {}

	//Does not AddRef.
	AccDtorIfElem0(ref const AccRaw& x) noexcept : AccRaw(x) {}

	//Does not AddRef.
	AccDtorIfElem0(ref const Cpp_Acc& x) noexcept : AccRaw(x) {}

	//Calls Release if elem is 0. Else acc is considered not owned by this.
	~AccDtorIfElem0() {
		if (acc != null && elem == 0) {
			//Perf.First();
			acc->Release();
			//Perf.NW('~');
		}
	}
};

//Provides a memory buffer for AccessibleChildren that can be reused by multiple AccChildren instances.
class AccContext {
public:
	VARIANT* buffer;
	int lenBuffer, maxcc;

	explicit AccContext(int maxcc_ = 10000) noexcept {
		buffer = null;
		lenBuffer = 0;
		maxcc = maxcc_;
	}

	bool Init() {
		if (buffer == null) {
			lenBuffer = min(maxcc, 1000000) + 1;
			do {
				buffer = (VARIANT*)VirtualAlloc(null, lenBuffer * sizeof(VARIANT), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
			} while (buffer == null && (lenBuffer /= 2) > 5000);
			if (buffer == null) {
				maxcc = lenBuffer = 0;
				return false;
			}
			maxcc = lenBuffer - 1;
		}
		return true;
	}

	~AccContext() {
		if (buffer != null) {
			VirtualFree(buffer, 0, MEM_RELEASE);
			buffer = null;
		}
	}
};

//Gets child AOs.
class AccChildren {
	IAccessible* _parent;
	VARIANT* _v;
	int _count, _i, _startAtIndex;
	bool _exactIndex, _reverse;
	eAccMiscFlags _miscFlags;

public:
	AccChildren(AccContext& context, const Cpp_Acc& parent, int startAtIndex = 0, bool exactIndex = false, bool reverse = false) {
		_parent = parent.acc;
		_miscFlags = parent.misc.flags & eAccMiscFlags::InheritMask;
		_v = null;
		_count = -1;
		_i = 0;
		_exactIndex = exactIndex;
		_reverse = reverse;
		_startAtIndex = startAtIndex;

		//With get_accChildCount was faster in most tested cases, usually 10-20%, sometimes 50%, sometimes same speed, Chrome 30%, WPF 50%, never slower.
		//	Note: get_accChildCount can return different count than AccessibleChildren. Usually more. With this code bad is only when incorrectly returns 0 or >maxcc.
		//	Never mind: with VS 2022 Preview get_accChildCount occasionally hangs when parent is PAGETABLIST of document. OK with only AccessibleChildren.

		//For AccessibleChildren we use buffer of maxcc+1 size.
		//	The buffer is in *context*. Reused by all AccChildren instances of that main function (find, navigate, etc).
		//	Max possible maxcc is 1000000 (24 MB in 64-bit process, 16 MB in 32-bit). If fails to allocate, sets smaller maxcc.

		//Perf.First();
		long n = 0;
#if true
		if (0 == _parent->get_accChildCount(&n) && n > 0 && n <= context.maxcc && context.Init()) {
			n = 0;
			int hr = AccessibleChildren(_parent, 0, context.lenBuffer, context.buffer, &n);
			if (hr < 0) { //rare
				n = 0;
				//PRINTHEX(hr);
				//ao::PrintAcc(_parent);
			} else if (n > 0) {
				//Printf(L"A %i", n);
				if (!(parent.misc.flags & (eAccMiscFlags::UIA | eAccMiscFlags::Java))) {
					n = _RemoveInvisibleNonclient(context.buffer, n, parent.misc.roleByte);
				}

				int memSize = n * sizeof(VARIANT);
				_v = (VARIANT*)malloc(memSize);
				if (_v != null) memcpy(_v, context.buffer, memSize);
				else while (n > 0) VariantClear(&context.buffer[--n]);
			}
		} else n = 0;
#else //bad: Slow anyway when there are >maxcc children. And slower in most cases.
		if (context.Init()) { //else failed to allocate memory, unlikely
			int hr = AccessibleChildren(_parent, 0, context.lenBuffer, context.buffer, &n);
			if (hr < 0) { //rare
				n = 0;
				//PRINTHEX(hr);
				//ao::PrintAcc(_parent);
			} else if (n > 0) {
				if (n <= context.maxcc) { //maxcc default 10000, max 1000000
					//Printf(L"A %i", n);
					if (!(parent.misc.flags & (eAccMiscFlags::UIA | eAccMiscFlags::Java))) {
						n = _RemoveInvisibleNonclient(context.buffer, n, parent.misc.roleByte);
					}

					int memSize = n * sizeof(VARIANT);
					_v = (VARIANT*)malloc(memSize);
					if (_v != null) memcpy(_v, context.buffer, memSize);
				} else {
					//Print(n);
					n = min(n, context.lenBuffer);
				}

				if (_v == null) while (n > 0) VariantClear(&context.buffer[--n]);
			}
		}
#endif
		//Perf.NW();

		_count = n;
		if (n > 0 && _startAtIndex != 0) {
			if (_startAtIndex < 0) _startAtIndex = n + _startAtIndex; else _startAtIndex--; //if < 0, it is index from end
			int i = _startAtIndex; if (i < 0) i = 0; else if (i >= n) i = n - 1;
			if (_exactIndex && i != _startAtIndex) _startAtIndex = -1; else _startAtIndex = i;
		} else _startAtIndex = -1; //not used

		//speed: AccessibleChildren same as IEnumVARIANT with array. IEnumVARIANT.Next(1, ...) much slower (if out-proc).

		//50% AO have 0 children. 20% have 1 child. Few have > 7.
	}

	~AccChildren() {
		if (_v != null) {
			while (_count > 0) VariantClear(&_v[--_count]); //info: it's OK to clear variants for which FromVARIANT was called because then vt is 0
			free(_v); _v = null;
		}
	}

	int Count() { return _count; }

	bool GetNext(out AccRaw& a) {
		assert(a.IsEmpty());
		if (_count == 0) return false;
		if (_exactIndex) {
			int i = _startAtIndex; _startAtIndex = -1;
			if (i < 0 || 0 != a.FromVARIANT(_parent, _v[i])) return false;
		} else {
		g1:
			if (_startAtIndex < 0) { //_startAtIndex is -1 if not used
				if (_i >= _count) return false;
				int i = _i++; if (_reverse) i = _count - i - 1;
				if (0 != a.FromVARIANT(_parent, _v[i])) goto g1;
			} else { //_startAtIndex is in _count range
				int i = _startAtIndex + _i;
				if (i < 0 || i >= _count) return false; //no more
				//calculate next i
				if (_i >= 0) {
					_i = -(_i + 1);
					if (_startAtIndex + _i < 0) _i = -_i;
				} else {
					_i = -_i;
					if (_startAtIndex + _i >= _count) _i = -(_i + 1);
				}
				if (0 != a.FromVARIANT(_parent, _v[i])) goto g1;
			}
		}
		a.misc.flags = _miscFlags;
		return true;
	}

private:
	//Removes invisible nonclient children of WINDOW. They are annoying and make slower.
	int _RemoveInvisibleNonclient(VARIANT* v, int n, int role) {
		if (n == 7 && (role == ROLE_SYSTEM_WINDOW || role == 0)) {
			for (int i = 0; i < 7; i++) if (v[i].vt != VT_DISPATCH) goto gr;
			//Perf.First();
			if (role == 0 && ao::GetRoleByte(_parent) != ROLE_SYSTEM_WINDOW) goto gr;
			HWND w; if (0 != WindowFromAccessibleObject(_parent, &w)) goto gr;

			//is it native WINDOW AO? Eg WPF uses role WINDOW instead of CLIENT.
			//Perf.Next();
			{
				//this way is fast, even if not inproc. Tried 2 other ways, both slow.
				//	Bad: the identity string is undocumented, and "clients should not attempt to dissect it or otherwise interpret it manually".
				Smart<IAccIdentity> aid; DWORD* k = nullptr; DWORD len = 0;
				if (0 != _parent->QueryInterface(&aid) || 0 != aid->GetIdentityString(0, (BYTE**)&k, &len)) goto gr;
				//Printf(L"0x%X 0x%X 0x%X 0x%X  w=0x%X", k[0], k[1], k[2], k[3], (int)w);
				bool ok = len == 16 && k[0] == 0x80000001 && k[1] == (DWORD)(LPARAM)w && k[2] == 0 && k[3] == 0; //0x80000001, hwnd, objid (OBJID_WINDOW is 0), childid
				CoTaskMemFree(k);
				if (!ok) goto gr;
			}
			//Perf.Next();

			DWORD bits = 1 << 3; //CLIENT

			DWORD style = wn::Style(w);
			if (style & WS_VSCROLL) bits |= 1 << 4;
			if (style & WS_HSCROLL) bits |= 1 << 5;
			if ((style & (WS_VSCROLL | WS_HSCROLL)) == (WS_VSCROLL | WS_HSCROLL)) bits |= 1 << 6; //GRIP

			if (style & WS_CHILD) {
				//note: child windows cannot have app MENUBAR.
				//note: assume system MENUBAR always visible if TITLEBAR visible; it depends on WS_SYSMENU + on don't know what.
				if ((style & WS_CAPTION) == WS_CAPTION) bits |= 3; //system MENUBAR, TITLEBAR
			} else {
				//With top-level windows don't use style/GetWindowLongPtrW(w, GWL_ID)/GetMenu. Can be custom MENUBAR etc. Here the speed is not so important.
				for (int i = 0; i < 3; i++) {
					Smart<IAccessible> iacc; long state;
					if (0 != v[i].pdispVal->QueryInterface(&iacc)) continue;
					if (0 != ao::get_accState(out state, iacc) || !(state & STATE_SYSTEM_INVISIBLE)) bits |= 1 << i;
				}
			}

			//wn::PrintWnd(w); Print((uint)bits);

			int iMoveTo = 0;
			for (int i = 0; i < 7; i++) {
				if (bits & (1 << i)) {
					if (i != iMoveTo) memmove(v + iMoveTo, v + i, sizeof(VARIANT));
					iMoveTo++;
				} else {
					VariantClear(v + i);
					n--;
				}
			}
			//Perf.NW();
		}
	gr:	return n;

		//never mind: does not remove invisible children of TITLEBAR, SCROLLBAR etc. It could be a custom TITLEBAR etc. Not so important.
	}
};

IAccessible* AccJavaFromWindow(HWND w, bool getFocused = false);
IAccessible* AccJavaFromPoint(POINT p, HWND w, bool alt);
HRESULT AccUiaFromWindow(HWND w, out IAccessible** iacc);
HRESULT AccUiaFromPoint(POINT p, out IAccessible** iacc);
HRESULT AccUiaFocused(out IAccessible** iacc);
//HRESULT AccUiaFromMSAA(IAccessible* msaa, int elem, out IAccessible** iacc);
