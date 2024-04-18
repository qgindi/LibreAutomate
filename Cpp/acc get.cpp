#include "stdafx.h"
#include "cpp.h"
#include "acc.h"

namespace outproc {
	void AccEnableChrome(HWND w, HWND c = 0);
}

namespace {

	eSpecWnd _IsSpecWnd(HWND wTL, HWND w) {
		if (w != wTL) {
			switch (wn::ClassNameIs(w, { c_CRW, L"Chrome_WidgetWin_1" })) {
			case 1: return eSpecWnd::ChromeControl; //eg WebView2
			case 2: return eSpecWnd::ChromeControl2;
			}
		}
		int i = wn::ClassNameIs(wTL, { L"Chrome*", L"SunAwt*", L"SAL*FRAME", L"Mozilla*" });
		if (i > 0 && w != wTL && i != (int)_IsSpecWnd(w, w)) i = 0; //if control, ignore if classname not similar
		return (eSpecWnd)i;
	}

	bool _IsContainer(BYTE role) {
		switch (role) {
		case ROLE_SYSTEM_APPLICATION:
		case ROLE_SYSTEM_CLIENT:
		case ROLE_SYSTEM_DIALOG:
			//case ROLE_SYSTEM_DOCUMENT: //often either empty or contains many slow elements
		case ROLE_SYSTEM_GROUPING:
		case ROLE_SYSTEM_PAGETAB:
		case ROLE_SYSTEM_PAGETABLIST:
		case ROLE_SYSTEM_PANE:
		case ROLE_SYSTEM_PROPERTYPAGE:
		case ROLE_SYSTEM_WINDOW:
			return true;
		}
		return false;
	}

	bool _IsLinkOrButton(int role) {
		switch (role) {
		case ROLE_SYSTEM_LINK:
		case ROLE_SYSTEM_PUSHBUTTON: case ROLE_SYSTEM_BUTTONMENU: case ROLE_SYSTEM_BUTTONDROPDOWN: case ROLE_SYSTEM_BUTTONDROPDOWNGRID:
		case ROLE_SYSTEM_CHECKBUTTON: case ROLE_SYSTEM_RADIOBUTTON:
			return true;
		}
		return false;
	}

	void _FromPoint_GetLink(ref IAccessible*& a, ref long& elem, ref BYTE& role, bool isUIA) {
		//note: the child AO of LINK/BUTTON can be anything except LINK/BUTTON, although usually TEXT, STATICTEXT, IMAGE.
		if (_IsLinkOrButton(role)) return;
		IAccessible* parent = null;
		if (elem != 0) parent = a; else if (0 != ao::get_accParent(a, out parent)) return;
		BYTE role2 = ao::GetRoleByte(parent);
		bool useParent = _IsLinkOrButton(role2);
		if (!useParent) {
			switch (role2) {
			case ROLE_SYSTEM_STATICTEXT:
				useParent = role == ROLE_SYSTEM_STATICTEXT; //eg WPF label control
				break;
			case 0: case ROLE_CUSTOM: case ROLE_SYSTEM_GROUPING: case ROLE_SYSTEM_GRAPHIC:
				break;
			default:
				if (!isUIA || role2 == ROLE_SYSTEM_LISTITEM || role2 == ROLE_SYSTEM_OUTLINEITEM || role2 == ROLE_SYSTEM_MENUITEM) {
					if (ao::IsStatic(role, a)) {
						long cc = 0;
						//Perf.First();
						//get_accChildCount can be very slow if UIA, eg in Firefox big pages.
						//	SHOULDDO: if UIA, try something faster, eg get next/previous sibling of a. See UIAccessible::accNavigate.
						useParent = 0 == parent->get_accChildCount(&cc) && cc == 1;
						//Perf.NW();
						if (useParent) {
							Bstr bn;
							useParent = (0 == parent->get_accName(ao::VE(elem), &bn)) && bn && bn.Length() > 0;
						}

					}
				}
				break;
			}
		}
		if (useParent) {
			//bug in old Chrome and secondary windows of Firefox: AO retrieved with get_accParent is invalid, eg cannot get its window.
			HWND wp;
			if (elem == 0 && 0 != WindowFromAccessibleObject(parent, &wp)) {
				PRINTF(L"Cannot get parent LINK because WindowFromAccessibleObject would fail.");
				useParent = false;
			}
			if (useParent) {
				if (elem != 0) elem = 0; else util::Swap(ref a, ref parent);
				role = role2;
			}
		}
		if (parent != a) parent->Release();
		//rejected: support > 1 level. The capturing tool in C# supports it.
	}

	//Get UIA element from same point. If its size is < 0.5 than of the AO, use it. Dirty, but in most cases works well.
	bool _FromPoint_UiaWorkaround(POINT p, ref Smart<IAccessible>& iacc, ref long& elem, BYTE role) {
		//note: don't ignore when has children. Eg can be WINDOW, and its child count is not 0.

		Smart<IAccessible> auia;
		if (0 != AccUiaFromPoint(p, &auia)) return false; //speed: same inproc and outproc. AOFP usually inproc faster, outproc similar or slower.
		//Perf.Next('u');

		ao::VE ve; long x1, y1, x2, y2, wid1, hei1, wid2, hei2;
		if (0 != iacc->accLocation(&x1, &y1, &wid1, &hei1, ao::VE(elem)) || 0 != auia->accLocation(&x2, &y2, &wid2, &hei2, ve)) return false;
		__int64 sq1 = (__int64)wid1 * hei1, sq2 = (__int64)wid2 * hei2;
		if (!(sq2 < sq1 / 2 && sq2 > 0)) return false;

		//auia may be DOCUMENT with rect = the visible page area, whereas iacc is eg a much bigger GROUPING.
		if (ao::GetRoleByte(auia, 0) == ROLE_SYSTEM_DOCUMENT) return false;

		//Printf(L"{%i %i %i %i} {%i %i %i %i} 0x%X 0x%X", x1, y1, wid1, hei1, x2, y2, wid2, hei2, role, ao::GetRoleByte(auia, 0));

		//SHOULDDO: although smaller, in some cases it can be not a descendant. Often cannot detect it reliably. Some reasons:
		// 1. UIA filters out some objects.
		// 2. UIA sometimes gets different rect for same object. Eg clips treeitem if part of its text is offscreen.
		//Print("--------");
		//ao::PrintAcc(iacc);
		//for(IAccessible* a = auia, *pa = null; ; a = pa) {
		//	ao::PrintAcc(a);
		//	bool ok = 0 == a->get_accParent((IDispatch**)&pa);
		//	if(a != auia) a->Release();
		//	if(!ok) return false;
		//	if(//ao::GetRoleByte(pa) == role && //no, can be different, eg iacc CLIENT but pa WINDOW
		//		0 == pa->accLocation(&x2, &y2, &wid2, &hei2, ve) && x2 == x1 && y2 == y1 && wid2 == wid1 && hei2 == hei1) { //does not work too
		//		Print("OK");
		//		pa->Release();
		//		break;
		//	}
		//}

		iacc.Swap(ref auia);
		elem = 0;
		return true;
	}

#define E_FP_RETRY 0x2001

	//Sometimes, while we are injecting dll etc, window from point changes. Then need to retry.
	//	Else can be incorrect result; in some cases Delm and this thread can deadlock, eg when AccUiaFromPoint tries to get element from Delm.
	bool _FromPoint_ChangedWindow(HWND w, POINT p) {
		HWND w2 = WindowFromPoint(p);
		if (w2 == w || GetWindowThreadProcessId(w2, nullptr) == GetCurrentThreadId()) return false;
		//wn::PrintWnd(w2);
		PRINTS_IF(IsWindowVisible(w) && GetKeyState(1) >= 0, L"changed window from point. It's OK if occasionally, eg when resizing or moving."); //often when closing or resizing w
		return true;
		//This isn't fast and very reliable, but probably better than nothing. Maybe will need to reject after more testing.
		//	Window from point can change between WindowFromPoint and AccUiaFromPoint. Never seen, but possible.
		//	PhysicalToLogicalPoint isn't perfect. Bugs, 1-pixel error.
	}
#define RETRY_IF_CHANGED_WINDOW if(inProc && _FromPoint_ChangedWindow(wFP, p)) return E_FP_RETRY

	HRESULT _AccFromPoint(POINT p, HWND wFP, eXYFlags flags, eSpecWnd specWnd, out Cpp_Acc& aResult) {
		//Perf.First();
		Smart<IAccessible> iacc; long elem = 0;
		eAccMiscFlags miscFlags = (eAccMiscFlags)0;
		BYTE role = 0;
		bool inProc = !(flags & eXYFlags::NotInProc);
	g1:
		RETRY_IF_CHANGED_WINDOW;
		//Perf.Next('w');
		if (!!(flags & eXYFlags::UIA)) {
			HRESULT hr = AccUiaFromPoint(p, &iacc);
			if (hr != 0) return hr;
			miscFlags |= eAccMiscFlags::UIA;
			//Perf.Next('p');
		} else {
			VARIANT v;
			HRESULT hr = AccessibleObjectFromPoint(p, &iacc, &v);
			if (hr == 0 && !iacc) hr = E_FAIL;
			if (hr != 0) { //rare. Examples: treeview in HtmlHelp 2; Windows Security. Then UIA works.
				if (!!(flags & eXYFlags::OrUIA)) { flags |= eXYFlags::UIA; goto g1; }
				return hr;
			}
			assert(v.vt == VT_I4 || v.vt == 0);
			elem = v.vt == VT_I4 ? v.lVal : 0;
			//Perf.Next('p');

			role = ao::GetRoleByte(iacc, elem);
			//Perf.Next('r');

			//UIA?
			if (specWnd == eSpecWnd::None && role != ROLE_CUSTOM && !!(flags & eXYFlags::OrUIA) && _IsContainer(role)) { //and ignore elem
				RETRY_IF_CHANGED_WINDOW;
				if (_FromPoint_UiaWorkaround(p, ref iacc, ref elem, role)) {
					miscFlags |= eAccMiscFlags::UIA;
					//PRINTF(L"switched to UIA.  p={%i, %i}  role=0x%X  w=%i  cl=%s", p.x, p.y, role, (int)(LPARAM)wFP, GetCommandLineW());
				}
				//Perf.Next('X');
			}
		}
		RETRY_IF_CHANGED_WINDOW;
		//Perf.Next('w');

		bool isUIA = !!(miscFlags & eAccMiscFlags::UIA);
		if (isUIA) role = ao::GetRoleByte(iacc);
		if (!!(flags & eXYFlags::PreferLink)) _FromPoint_GetLink(ref iacc.p, ref elem, ref role, isUIA);
		//Perf.NW('Z');

		aResult.acc = iacc.Detach(); aResult.elem = elem;
		aResult.misc.flags = miscFlags;
		aResult.misc.roleByte = role;
		return 0;
	}

#pragma comment(lib, "comctl32.lib")

	LRESULT CALLBACK _FromPoint_Subclass(HWND w, UINT m, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData) {
		auto R = DefSubclassProc(w, m, wParam, lParam);
		if (m == WM_NCHITTEST && R == HTTRANSPARENT) R = HTCLIENT;
		return R;
	}
} //namespace

HRESULT AccFromPoint(POINT p, HWND wFP, eXYFlags flags, eSpecWnd specWnd, out Cpp_Acc& aResult) {
	//Workaround for: WindowFromPoint (and AccessibleObjectFromPoint etc) works differently inproc.
	//	Inproc it sends WM_NCHITTEST and skips that window if returns HTTRANSPARENT.
	//	Then the API gets wrong window, which even can be of another thread (then inproc has no sense).
	//	Workaround: subclass the window and disable HTTRANSPARENT.
	//		But only if another thread. Else skips controls covered by a transparent groupbox etc.
	bool transp = !(flags & eXYFlags::NotInProc)
		&& SendMessage(wFP, WM_NCHITTEST, 0, MAKELPARAM(p.x, p.y)) == HTTRANSPARENT
		&& GetWindowThreadProcessId(WindowFromPoint(p), null) != GetCurrentThreadId();
	if (transp) SetWindowSubclass(wFP, _FromPoint_Subclass, 1, 0);
	HRESULT hr;
	__try { hr = _AccFromPoint(p, wFP, flags, specWnd, out aResult); }
	__finally { if (transp) RemoveWindowSubclass(wFP, _FromPoint_Subclass, 1); }
	return hr;
}

namespace outproc {
	EXPORT HRESULT Cpp_AccFromPoint(POINT p, eXYFlags flags, Cpp_AccFromPointCallbackT callback, out Cpp_Acc& aResult) {
		//Perf.First();
		aResult.Zero();

		HRESULT R;
		auto flags0 = flags;
		POINT p0 = p;

		//About WindowFromPhysicalPoint:
		//	On Win8.1+ it's the same as WindowFromPoint. In DPI-aware thread uses physical point, in unaware logical.
		//	On Win7/8 WindowFromPhysicalPoint uses physical point, WindowFromPoint logical (when in a scaled window).
		HWND wFP = WindowFromPhysicalPoint(p); //never mind: skips disabled controls. It's even better with Chrome and Firefox.
	gRetry:
		HWND wTL = GetAncestor(wFP, GA_ROOT);
		if (!wTL) return 1; //let the caller retry

		ao::TempSetScreenReader tsr;
		eSpecWnd specWnd = _IsSpecWnd(wTL, wFP);
		if (specWnd == eSpecWnd::Java) {
			WINDOWINFO wi = { sizeof(wi) };
			if (GetWindowInfo(wFP, &wi) && PtInRect(&wi.rcClient, p)) {
				auto ja = AccJavaFromPoint(p, wFP);
				if (ja != null) {
					aResult.acc = ja;
					aResult.misc.flags = eAccMiscFlags::Java;
					aResult.misc.roleByte = ROLE_CUSTOM;
					return 0;
				}
			}
			//specWnd = eSpecWnd::None;
		} else if (specWnd == eSpecWnd::Chrome) {
			AccEnableChrome(wTL);
			//note: now can get wrong AO, although the above func waits for new good DOCUMENT (max 3 s).
			//	Chrome updates web page AOs lazily. The speed depends on web page. Can get wrong AO even after loong time.
			//	Or eg can be good AO, but some its properties are still not set.
			//	This func doesn't know what AO must be there, and cannot wait.
			//	Instead, where need such reliability, the caller script can eg wait for certain AO (role LINK etc) at that point.
		} else if (specWnd == eSpecWnd::ChromeControl) {
			AccEnableChrome(wTL, wFP);
		} else if (specWnd == eSpecWnd::OO) { //OpenOffice, LibreOffice
			tsr.Set(wTL);
			//OpenOffice bug: crashes on exit if AccessibleObjectFromPoint or AccessibleObjectFromWindow called with SPI_GETSCREENREADER.
			//	Could not find a workaround.
			//	Inspect.exe too.
			//	Does not if SPI_GETSCREENREADER was when starting OO.
			//	Does not if we get certain AO (eg DOCUMENT), eiher from point or when searching, now or later. Crashes eg if the AO is a toolbar button.
			//	Tested only with Writer.
			//	tested: inproc does not help.
			//	This info is old, maybe now something changed. Anyway, OpenOffice often crashes when using its AO.
		}
		//CONSIDER: AccEnableFirefox

		//The caller may want to modify flags depending on window. Also need to detect DPI-scaled windows (I don't want to duplicate the code here).
		//	Use callback because this func can retry with another window.
		flags = callback(flags0, wFP, wTL);
		if (!!(flags & eXYFlags::Fail_)) return 1;

		//If the window is DPI-scaled, if inproc, convert physical to logical point.
		if (!!(flags & eXYFlags::DpiScaled_)) {
			assert(dlapi.minWin81 ? !(flags & eXYFlags::NotInProc) : !!(flags & eXYFlags::UIA));
			p = p0;
			if (!dlapi.PhysicalToLogicalPoint(wFP, &p)) {
				PRINTS(L"PhysicalToLogicalPoint failed");
				return E_FAIL;
				//The API fails when:
				//	- The point is not in the window. After WindowFromPhysicalPoint the window could be moved, resized or closed.
				//		Here could retry WindowFromPhysicalPoint. But it never happened when testing. Never mind.
				//	- The DPI-scaled window is in 2 screens and the point is in certain area in wrong screen.
				//		Never mind. It's rare and usually temporary.
				//		With flags NotInProc+UIA (Win8.1+) often works, but not always. In this case it's better to fail.
				//API bug: in some cases when the window is in 2 screens, the API scales the point although the window isn't scaled.
				//	Good: on Win10 then skips this code because the callback reliably detects DPI-scaled windows.
			}
			//Printf(L"phy={%i %i}  log={%i %i}", p0.x, p0.y, p.x, p.y);
		}
		//How 'from point' and 'get rect' API work with DPI-scaled windows:
		//Win10 and 8.1:
		//	MSAA/inproc - good. For 'from point' need logical coord. After 'get rect' need to convert logical to physical.
		//	MSAA/notinproc - bad, random, unusable.
		//	UIA/inproc - good. For 'from point' need logical coord. After 'get rect' need to convert logical to physical.
		//	UIA/notinproc - good.
		//Win7 and 8.0:
		//	MSAA/inproc - good in most cases. With some elements bad 'get rect', especially if found not by 'from point'.
		//	MSAA/notinproc - same as inproc.
		//	UIA/inproc - bad, random, almost unusable. For 'from point' need logical coord. Randomly bad 'get rect' (we don't scale it).
		//	UIA/notinproc - same as inproc.

	gNotinproc:
		if (!!(flags & eXYFlags::NotInProc)) {
			if (!!(flags & eXYFlags::DpiScaled_)) {
				if (dlapi.minWin81) flags |= eXYFlags::UIA;
				else p0 = p; //UIA
			}

			R = AccFromPoint(p0, wFP, flags, specWnd, out aResult);
			return R;
		}

		Cpp_Acc_Agent aAgent;
		if (0 != (R = InjectDllAndGetAgent(wFP, out aAgent.acc))) {
			switch ((eError)R) {
			case eError::WindowOfThisThread: case eError::UseNotInProc: case eError::Inject: break;
			default: return R;
			}
			flags |= eXYFlags::NotInProc; goto gNotinproc;
		}

		InProcCall ic;
		auto x = (MarshalParams_AccFromPoint*)ic.AllocParams(&aAgent, InProcAction::IPA_AccFromPoint, sizeof(MarshalParams_AccFromPoint));
		x->p = p;
		x->flags = flags;
		x->specWnd = specWnd;
		x->wFP = (int)(LPARAM)wFP;
		if (0 != (R = ic.Call())) {
			if (R == E_FP_RETRY) {
				HWND w2 = WindowFromPhysicalPoint(p);
				if (w2 != wFP) { wFP = w2; goto gRetry; }
				flags |= eXYFlags::NotInProc; goto gNotinproc;
			}
			return R;
		}
		//Perf.Next();
		R = ic.ReadResultAcc(ref aResult);
		//Perf.NW();
		return R;
	}
} //namespace outproc

HRESULT AccGetFocused(HWND w, eFocusedFlags flags, out Cpp_Acc& aResult) {
	if (!!(flags & eFocusedFlags::UIA)) {
		HRESULT hr = AccUiaFocused(&aResult.acc);
		if (hr != 0) return hr;
		aResult.misc.flags = eAccMiscFlags::UIA;
	} else {
		Smart<IAccessible> aw;
		HRESULT hr = AccessibleObjectFromWindow(w, OBJID_CLIENT, IID_IAccessible, (void**)&aw);
		if (hr != 0) return hr;

		AccRaw a1(aw, 0), a2; bool isThis;
		hr = a1.DescendantFocused(out a2, out isThis); if (hr != 0) return hr;
		if (isThis) {
			aw.Detach();
			aResult = a1;
		} else {
			aResult = a2;

			//never mind: cannot get focused AO of UIA-only windows, eg Java FX.
		}
	}
	return 0;
}

namespace outproc {
	//w - must be the focused control or window.
	EXPORT HRESULT Cpp_AccGetFocused(HWND w, eFocusedFlags flags, out Cpp_Acc& aResult) {
		aResult.Zero();

		HWND wTL = GetAncestor(w, GA_ROOT);
		//if(!wTL || wTL != GetForegroundWindow()) return 1; //return quickly, anyway would fail. No, does not work with some windows.
		if (!wTL) return 1;

		ao::TempSetScreenReader tsr;
		eSpecWnd specWnd = _IsSpecWnd(wTL, w);
		if (specWnd == eSpecWnd::Java) {
			auto ja = AccJavaFromWindow(w, true);
			if (ja != null) {
				aResult.acc = ja;
				aResult.misc.flags = eAccMiscFlags::Java;
				return 0;
			}
		} else if (specWnd == eSpecWnd::Chrome) {
			AccEnableChrome(w);
		} else if (specWnd == eSpecWnd::ChromeControl) {
			AccEnableChrome(wTL, w);
		} else if (specWnd == eSpecWnd::ChromeControl2) {
			//if WebView, focused is parent control (class Chrome_WidgetWin_1) of the c_CRW control. Need c_CRW to enable AO.
			HWND w2 = wn::FindWndExVisible(w, c_CRW);
			if (w2) AccEnableChrome(wTL, w2);
		} else if (specWnd == eSpecWnd::OO) { //OpenOffice, LibreOffice
			tsr.Set(wTL);
			//OpenOffice bug: crashes. More info in Cpp_AccFromPoint.
		}

	gNotinproc:
		if (!!(flags & eFocusedFlags::NotInProc)) {
			return AccGetFocused(w, flags, out aResult);
		}

		HRESULT R = 0;
		Cpp_Acc_Agent aAgent;
		if (0 != (R = InjectDllAndGetAgent(w, out aAgent.acc))) {
			switch ((eError)R) {
			case eError::WindowOfThisThread: case eError::UseNotInProc: case eError::Inject: break;
			default: return R;
			}
			flags |= eFocusedFlags::NotInProc; goto gNotinproc;
		}

		InProcCall ic;
		auto x = (MarshalParams_AccFocused*)ic.AllocParams(&aAgent, InProcAction::IPA_AccFocused, sizeof(MarshalParams_AccFocused));
		x->hwnd = (int)(LPARAM)w;
		x->flags = flags;
		if (0 != (R = ic.Call())) return R;
		return ic.ReadResultAcc(ref aResult);
	}
} //namespace outproc
