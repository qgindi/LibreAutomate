#include "stdafx.h"
#include "cpp.h"
#include "acc.h"

#pragma comment(lib, "oleacc.lib")

bool AccMatchHtmlAttributes(IAccessible* iacc, NameValue* prop, int count);
int AccChromeHtmlEnabled(IAccessible* aDoc);

class AccFinder
{
	//these have ctors
	AccContext _context; //shared memory buffer and maxcc
	str::Wildex _controlClass; //used when the prop parameter has "class=x". Then _flags2 has eAF2::InControls.
	str::Wildex _name; //name. If the name parameter is null, _name.Is()==false.
	Bstr _roleStrings, _propStrings; //a copy of the input role/prop string when eg need to parse (modify) the string

	//our ctor ZEROTHISFROM(_callback)
	AccFindCallback* _callback; //receives found AO
	STR _role; //null if used path or if the role parameter is null
	NameValue* _prop; //other string properties and HTML attributes. Specified in the prop parameter, like L"value=XXX\0 @id=YYY".
	STR _controlWF; //WinForms name. Used when the prop parameter has "id=x" where x is not a number. Then _flags2 has eAF2::InControls.
	STR* _notin; //when searching, skip descendants of AO of these roles. Specified in the prop parameter.
	int _propCount; //_prop array element count
	int _notinCount; //_notin array element count
	int _controlId; //used when the prop parameter has "id=x" wherex x is a number. Then _flags2 has eAF2::InControls|IsId.
	int _minLevel, _maxLevel; //min and max level to search in the object subtree. Specified in the prop parameter. Default 0 1000.
	int _stateYes, _stateNo; //the AO must have all _stateYes flags and none of _stateNo flags. Specified in the prop parameter.
	int _elem; //simple element id. Specified in the prop parameter. _flags2 has IsElem.
	RECT _rect; //AO location. Specified in the prop parameter. _flags2 has IsRect.
	eAF _flags; //user
	eAF2 _flags2; //internal
	bool _found; //true when the AO has been found
	IAccessible** _findDOCUMENT; //used by _FindDocumentSimple, else null
	BSTR* _errStr; //error string, when a parameter is invalid
	HWND _wTL; //window in which currently searching

	//A parsed path part, when the role parameter is path like "A/B[4]/C". 
	//struct _PathPart {
	//	STR role; //eg "B" if "B[4]" //stored in _roleStrings
	//	int startIndex; //eg 4 if "B[4]"
	//	bool exactIndex; //true if eg "B[4!]"

	//	_PathPart() { ZEROTHIS; }
	//};
	//_PathPart* _path; //null if no path
	//int _pathCount; //_path array element count

	bool _Error(STR es) {
		if(_errStr) *_errStr = SysAllocString(es);
		return false;
	}

	HRESULT _ErrorHR(STR es) {
		_Error(es);
		return (HRESULT)eError::InvalidParameter;
	}

	bool _ParseRole(STR role, int roleLen)
	{
		if(role == null) return true;
		if(roleLen == 0) return _Error(L"role cannot be \"\".");

		//is prefix?
		int iColon = -1;
		for(int i = 0; i < roleLen; i++) {
			auto c = role[i];
			if(c == ':') { iColon = i; break; }
		}
		if(iColon > 0) {
			int prefix = str::Switch(role, iColon, { L"web", L"firefox", L"chrome" });
			if(prefix > 0) {
				switch(prefix) {
				case 1: _flags2 |= eAF2::InWebPage; break; //auto-detect by window class name. Or Cpp_AccFind already found IES and added InIES.
				case 2: _flags2 |= eAF2::InFirefoxPage | eAF2::InWebPage; break;
				case 3: _flags2 |= eAF2::InChromePage | eAF2::InWebPage; break;
				}
				if(++iColon == roleLen) return true;
				role += iColon; roleLen -= iColon;
			}
		}

		//is path?
		//rejected. Nobody will use.
		//if(_pathCount = (int)std::count(role, role + roleLen, '/')) {
		//	auto a = _path = new _PathPart[++_pathCount];
		//	int level = 0;
		//	LPWSTR s = _roleStrings.Assign(role, roleLen);
		//	for(LPWSTR partStart = s, eos = s + roleLen; s <= eos; s++) {
		//		auto c = *s;
		//		if(c == '/' || c == '[' || s == eos) {
		//			_PathPart& e = a[level];
		//			if(s > partStart) { //else can be any role at this level
		//				e.role = partStart;
		//				*s = 0;
		//			}
		//			if(c == '[') {
		//				auto s0 = s + 1;
		//				e.startIndex = strtoi(s0, &s);
		//				if(s == s0) goto ge;
		//				if(*s == '!') { s++; e.exactIndex = true; }
		//				if(*s++ != ']') goto ge;
		//				if(s < eos && *s != '/') goto ge;
		//			}
		//			partStart = s + 1;
		//			level++;
		//		}
		//	}
		//	//Print(_pathCount); for(int i = 0; i < _pathCount; i++) Printf(L"'%s'  %i %i", a[i].role, a[i].startIndex, a[i].exactIndex);
		//} else {
		if(role[roleLen] == 0) _role = role;
		else _role = _roleStrings.Assign(role, roleLen);
	//}

		return true;
//ge:
		return _Error(L"Invalid role.");
	}

	void _ParseNotin(LPWSTR s, LPWSTR eos)
	{
		_notinCount = (int)std::count(s, eos, ',') + 1;
		_notin = new STR[_notinCount];
		int i = 0;
		for(LPWSTR start = s; s <= eos; ) {
			if(*s == ',' || s == eos) {
				_notin[i++] = start;
				*s++ = 0; if(*s == ' ') s++;
				start = s;
			} else s++;
		}

		//Print(_notinCount); for(i = 0; i < _notinCount; i++) Print(_notin[i]);
	}

	bool _ParseState(LPWSTR s, LPWSTR eos)
	{
		for(LPWSTR start = s; s <= eos; ) {
			if(*s == ',' || s == eos) {
				bool nott = false; if(*start == '!') { start++; nott = true; }
				int state;
				if(s > start && *start >= '0' && *start <= '9') {
					LPWSTR se;
					state = strtoi(start, &se);
					if(se != s) return false;
				} else {
					state = ao::StateFromString(start, s - start);
					if(state == 0) return _Error(L"Unknown state name.");
				}
				if(nott) _stateNo |= state; else _stateYes |= state;
				if(*++s == ' ') s++;
				start = s;
			} else s++;
		}
		//Printf(L"0x%X  0x%X", _stateYes, _stateNo);
		return true;
	}

	bool _ParseRect(LPWSTR s, LPWSTR eos)
	{
		if(*s++ != '{' || *(--eos) != '}') goto ge;
		for(; s < eos; s++) {
			LPWSTR s1 = s++, s2;
			if(*s++ != '=') goto ge; //FUTURE: support operators < > etc. Also Coord. Example: {L>=0.5 T<^20}
			int t = strtoi(s, &s2);
			if(s2 == s) goto ge; s = s2;
			switch(*s1) {
			case 'L': _rect.left = t; _flags2 |= eAF2::IsRectL; break;
			case 'T': _rect.top = t; _flags2 |= eAF2::IsRectT; break;
			case 'W': _rect.right = t; _flags2 |= eAF2::IsRectW; break;
			case 'H': _rect.bottom = t; _flags2 |= eAF2::IsRectH; break;
			default: goto ge;
			}
		}
		//Printf(L"{%i %i %i %i}", _rect.left, _rect.top, _rect.right, _rect.bottom);
		return true;
	ge:
		return _Error(L"Invalid rect format.");
	}

	bool _ParseProp(STR prop, int propLen)
	{
		if(prop == null) return true;

		int elemCount = (int)std::count(prop, prop + propLen, '\0') + 1;
		_prop = new NameValue[elemCount]; _propCount = 0; //info: finally can be _propCount<elemCount, ie not all elements used
		LPWSTR s0 = _propStrings.Assign(prop, propLen), s2, s3;
		for(LPWSTR s = s0, na = s0, va = null, eos = s0 + propLen; s <= eos; s++) {
			auto c = *s;
			if(c == 0) {
				if(s > s0) {
					if(va == null) return _Error(L"Missing = in prop string.");
					//Printf(L"na='%s' va='%s'    naLen=%i vaLen=%i", na, va, va - 1 - na, s - va);

					bool addToProp = true;
					if(na[0] != '@') { //HTML attribute names have prefix "@"
						int i = str::Switch(na, va - 1 - na, {
							L"value", L"desc", L"help", L"action", L"key", L"uiaid", //string props
							L"state", L"level", L"maxcc", L"notin", L"rect", L"item",
							L"class", L"id", //control
							});

						if(i == 0) return _Error(L"Unknown name in prop. For HTML attributes use prefix @.");
						const int nStrProp = 6;
						if(i > nStrProp) {
							i -= nStrProp;
							int len = (int)(s - va); if(len == 0 && i != 8) goto ge; //winforms name can be empty
							addToProp = false;
							switch(i) {
							case 1:
								if(!_ParseState(va, s)) return false;
								break;
							case 2:
								//if(_path != null) return _Error(L"Path and level.");
								_minLevel = strtoi(va, &s2);
								if(s2 == va || _minLevel < 0) goto ge;
								if(s2 == s) _maxLevel = _minLevel;
								else if(s2 < s && *s2 == ' ') {
									_maxLevel = strtoi(++s2, &s3);
									if(s3 != s || _maxLevel < _minLevel) goto ge;
								} else goto ge;
								break;
							case 3:
								_context.maxcc = strtoi(va, &s2);
								if(_context.maxcc < 1 || s2 != s) goto ge;
								break;
							case 4:
								_ParseNotin(va, s);
								break;
							case 5:
								if(!_ParseRect(va, s)) return false;
								break;
							case 6:
								_elem = strtoi(va, &s2);
								if(s2 != s) goto ge;
								_flags2 |= eAF2::IsElem;
								break;
							case 7:
								if(!_controlClass.Parse(va, len, true, _errStr)) return false;
								_flags2 |= eAF2::InControls;
								break;
							case 8:
								if(len > 0) {
									int cid = strtoi(va, &s2);
									if(s2 == s) { _controlId = cid; _flags2 |= eAF2::IsId; } else _controlWF = va;
								} else _controlWF = va;
								_flags2 |= eAF2::InControls;
								break;
							}
						}
					}
					if(addToProp) {
						assert(_propCount < elemCount);
						NameValue& x = _prop[_propCount++];
						x.name = na;
						if(!x.value.Parse(va, s - va, true, _errStr)) return false;
					}
				}
				while(++s <= eos && *s <= ' '); //allow space before name, eg "name1=value1\0 name2=..."
				na = s--;
				va = null;
			} else if(c == '=' && va == null) {
				*s = 0;
				va = s + 1;
			}
		}

		return true;
	ge: return _Error(L"Invalid prop string.");
	}

public:

	AccFinder(BSTR* errStr = null) {
		ZEROTHISFROM(_callback);
		_errStr = errStr;
		_maxLevel = 1000;
	}

	~AccFinder()
	{
		//delete[] _path;
		delete[] _prop;
		delete[] _notin;
	}

	bool SetParams(const Cpp_AccFindParams& ap, eAF2 flags2)
	{
		_flags = ap.flags;
		_flags2 = flags2;
		if(!_ParseRole(ap.role, ap.roleLength)) return false;
		if(ap.name != null && !_name.Parse(ap.name, ap.nameLength, true, _errStr)) return false;
		if(!_ParseProp(ap.prop, ap.propLength)) return false;

		if(!!(_flags2 & eAF2::InWebPage)) {
			_flags |= eAF::MenuToo;
			if(!!(_flags & (eAF::UIA | eAF::ClientArea))
				|| !!(_flags2 & eAF2::InControls)
				) return _Error(L"role prefix 'web:' cannot be used with: flag UIA, flag ClientArea, prop 'class', prop 'id'.");
		}

		return true;
	}

	HRESULT Find(HWND w, const Cpp_Acc* a, AccFindCallback* callback)
	{
		assert(!!w == !a);
		_callback = callback;

		if(a) {
			if(!!(_flags2 & eAF2::InWebPage)) return _ErrorHR(L"Don't use role prefix when searching in elm.");
			if(!!(_flags2 & eAF2::InControls)) return _ErrorHR(L"Don't use class/id when searching in elm.");
			assert(!(_flags & (eAF::UIA | eAF::ClientArea))); //checked in C#

			_FindInAcc(ref * a, 0);
		} else if(!!(_flags2 & eAF2::InWebPage)) {
			if(!!(_flags2 & eAF2::InIES)) { //info: Cpp_AccFind finds IES control and adds this flag
				_FindInWnd(w, false, false);
				//info: the hierarchy is WINDOW/CLIENT/PANE, therefore PANE will be at level 0
			} else {
				AccDtorIfElem0 aDoc;
				//Perf.First();
				HRESULT hr = _FindDocument(w, out aDoc);
				//Perf.NW();
				if(hr) return hr;

				switch(_Match(ref aDoc, 0)) {
				case _eMatchResult::SkipChildren: return (HRESULT)eError::NotFound;
				case _eMatchResult::Continue: _FindInAcc(ref aDoc, 1);
				}
			}
		} else {
			bool isJava = !(_flags & eAF::UIA) && (_role == null || (_role[0] >= 'a' && _role[0] <= 'z')) && wn::ClassNameIs(w, L"SunAwt*"); //note: can be control. I know only 1 such app - Sweet Home 3D.
			if(!!(_flags2 & eAF2::InControls)) {
				wn::EnumChildWindows(w, [this, w, isJava](HWND c)
				{
					if(!(_flags & eAF::HiddenToo) && !wn::IsVisibleInWindow(c, w)) return true; //not IsWindowVisible, because we want to find controls in invisible windows
					if(!!(_flags2 & eAF2::IsId) && GetDlgCtrlID(c) != _controlId) return true;
					if(_controlClass.Is() && !wn::ClassNameIs(c, _controlClass)) return true;
					if(_controlWF != null && !wn::WinformsNameIs(c, _controlWF)) return true;
					return 0 != _FindInWnd(c, true, isJava && wn::ClassNameIs(c, L"SunAwt*"));
				});
			} else {
				_wTL = (wn::Style(w) & WS_CHILD) ? 0 : w;
				if(_wTL && !(_flags & eAF::UIA) && !isJava) {
					if(wn::ClassNameIs(w, L"Mozilla*")) _flags2 |= eAF2::InFirefoxNotWebNotUIA; //skip background tabs
				}
				_FindInWnd(w, false, isJava);
			}
		}

		return _found ? 0 : (HRESULT)eError::NotFound;
	}

private:
	HRESULT _FindInWnd(HWND w, bool isControl, bool isJava)
	{
		if(isJava) {
			AccDtorIfElem0 aj(AccJavaFromWindow(w), 0, eAccMiscFlags::Java);
			if(aj.acc) return _FindInAcc(ref aj, 0) ? 0 : (HRESULT)eError::NotFound;
		}

		AccDtorIfElem0 aw;
		HRESULT hr;
		if(!!(_flags & eAF::UIA)) {
			hr = AccUiaFromWindow(w, &aw.acc);
			aw.misc.flags = eAccMiscFlags::UIA;
			//FUTURE: to make faster, add option to use IUIAutomationElement::FindFirst or FindAll.
			//	Problems: 1. No Level. 2. Cannot apply many flags; then in some cases can be slower or less reliable.
			//	Not very important. Now fast enough. JavaFX almost same speed (inproc).
		} else {
			bool inCLIENT = !!(_flags & eAF::ClientArea);
			hr = ao::AccFromWindowSR(w, inCLIENT ? OBJID_CLIENT : OBJID_WINDOW, &aw.acc);
			aw.misc.roleByte = inCLIENT ? ROLE_SYSTEM_CLIENT : ROLE_SYSTEM_WINDOW; //not important: can be not CLIENT (eg DIALOG)
		}
		if(hr) return hr;

		//isControl is true when is specified class or id. Now caller is enumerating controls. Need _Match for control's WINDOW, not only for descendants.
		int level = 0;
		if(isControl /*&& _path == null*/) {
			switch(_Match(ref aw, level++)) {
			case _eMatchResult::Stop: return 0;
			case _eMatchResult::SkipChildren: goto gnf;
			}
		}

		if(_FindInAcc(ref aw, level)) return 0; //note: caller also must check _found; this is just for EnumChildWindows.
	gnf:
		return (HRESULT)eError::NotFound;
	}

	//Returns true to stop.
	bool _FindInAcc(const Cpp_Acc& aParent, int level)
	{
		int startIndex = 0; bool exactIndex = false;
		//if(_path != null) {
		//	startIndex = _path[level].startIndex;
		//	if(_path[level].exactIndex) exactIndex = true;
		//}

		AccChildren c(ref _context, ref aParent, startIndex, exactIndex, !!(_flags & eAF::Reverse));
		//Printf(L"%i  %i", level, c.Count());
		if(c.Count() == 0) {
			//rejected: enable Chrome web AOs. Difficult to implement (lazy, etc). Let use prefix "web:".
			//if(_wTL) {
			//	if(level == (!!(_flags & eAF::ClientArea) ? 0 : 1) && aParent.misc.roleByte == ROLE_SYSTEM_CLIENT) {
			//	}
			//}
			return false;
		}
		for(;;) {
			AccDtorIfElem0 aChild;
			if(!c.GetNext(out aChild)) break;

			switch(_Match(ref aChild, level)) {
			case _eMatchResult::Stop: return true;
			case _eMatchResult::SkipChildren: continue;
			}

			if(_FindInAcc(ref aChild, level + 1)) return true;
		} //now a.a is released if a.elem==0
		return false;
	}

	enum class _eMatchResult { Continue, Stop, SkipChildren };

	_eMatchResult _Match(ref AccDtorIfElem0& a, int level)
	{
		if(_findDOCUMENT && a.elem != 0) return _eMatchResult::SkipChildren;

		bool skipChildren = a.elem != 0 || level >= _maxLevel;
		bool hiddenToo = !!(_flags & eAF::HiddenToo);
		_AccState state(ref a);

		_variant_t varRole;
		BYTE role = a.GetRoleByteAndVariant(out varRole);
		a.misc.roleByte = role;
		a.SetLevel(level);

		//a.PrintAcc();

		//skip Firefox background tabs.
		//	Searching without "web:" could take minutes. With "web:" would find wrong DOCUMENT when the fast way fails.
		//	In recent versions: DOCUMENT's state does not have invisible/offscreen flags; parent browser and PROPERTYPEGE have OFFSCREEN.
		//	Never mind UIA. Roles Custom/PANE/DOCUMENT instead of PROPERTYPAGE/browser/DOCUMENT. Also cannot be used "web:".
		if(!!(_flags2 & eAF2::InFirefoxNotWebNotUIA) && role == ROLE_SYSTEM_PROPERTYPAGE && !skipChildren && level < 5) {
			if(state.State() & (STATE_SYSTEM_INVISIBLE | STATE_SYSTEM_OFFSCREEN)) {
				//Print(level); //2 in Firefox and Thunderbird
				return _eMatchResult::SkipChildren;
			}
		}

		if(_findDOCUMENT) {
			auto fdr = _FindDocumentCallback(ref a);
			if(skipChildren && fdr == _eMatchResult::Continue) fdr = _eMatchResult::SkipChildren;
			return fdr;
		}

		//skip children of AO of user-specified roles
		STR roleString = null;
		if(_notin && !skipChildren) {
			roleString = ao::RoleToString(ref varRole);
			for(int i = 0; i < _notinCount; i++) if(!wcscmp(_notin[i], roleString)) {
				skipChildren = true;
				break;
			}
		}

		STR roleNeeded = /*_path != null ? _path[level].role :*/ _role;

		if(level >= _minLevel) {
		//If eAF::Mark, the caller is getting all AO using callback, and wants us to add eAccMiscFlags::Marked to AOs that match role, rect, name and state.
		//	If some of these props does not match, we set mark = -1, to avoid comparing other props.
			int mark = !!(_flags & eAF::Mark) ? 1 : 0;
			if(mark && !!(_flags & eAF::Marked_)) {
				//Currently the caller needs single marked object. Used internally.
				//	To make faster, don't compare properties of other objects. Some AO are very slow, eg .NET DataGridView with 10 columns and 1000 rows.
				mark = -1;
			}

			if(mark >= 0) {
				if(roleNeeded != null) {
					if(!roleString) roleString = ao::RoleToString(ref varRole);
					if(wcscmp(roleNeeded, roleString)) {
						if(mark) mark = -1;
						//else if(_path != null) return _eMatchResult::SkipChildren;
						else goto gr;
					}
				}
				//if(_path != null) {
				//	if(level < _pathCount - 1) goto gr;
				//	skipChildren = true;
				//}
			}

			if(!!(_flags2 & eAF2::IsElem) && a.elem != _elem) goto gr;

			if(mark > 0 && !_MatchRect(ref a)) mark = -1;

			if(_name.Is() && mark >= 0 && !a.MatchStringProp(L"name", ref _name)) {
				if(mark) mark = -1; else goto gr;
			}

			if(!hiddenToo) {
				switch(state.IsInvisible()) {
				case 2: //INVISISBLE and OFFSCREEN
					if(!_IsRoleToSkipIfInvisible(role)) break;
					[[fallthrough]];
				case 1: //only INVISIBLE
					if(_IsRoleTopLevelClient(role, level)) break; //rare
					skipChildren = true; goto gr;
				}
			}

			if(!!(_stateYes | _stateNo) && mark >= 0) {
				auto k = state.State();
				if((k & _stateYes) != _stateYes || !!(k & _stateNo)) {
					if(mark) mark = -1; else goto gr;
				}
			}

			if(!mark && !_MatchRect(ref a))  goto gr;

			if(_propCount) {
				bool hasHTML = false;
				for(int i = 0; i < _propCount; i++) {
					NameValue& p = _prop[i];
					if(p.name[0] == '@') hasHTML = true;
					else if(!a.MatchStringProp(p.name, ref p.value)) goto gr;
				}
				if(hasHTML) {
					if(a.elem || !AccMatchHtmlAttributes(a.acc, _prop, _propCount)) goto gr;
				}
			}

			if(mark > 0) {
				a.misc.flags |= eAccMiscFlags::Marked;
				_flags |= eAF::Marked_;
			}

			switch((*_callback)(a)) {
			case eAccFindCallbackResult::Continue: goto gr;
			case eAccFindCallbackResult::StopFound: _found = true;
			//case eAccFindCallbackResult::StopNotFound: break;
			}
			return _eMatchResult::Stop;
		}
	gr:
		if(!skipChildren) {
			//depending on flags, skip children of AO that often have many descendants (eg MENUITEM, LIST, TREE)
			skipChildren = _IsRoleToSkipDescendants(role, roleNeeded, a.acc);

			//skip children of invisible AO that often have many descendants (eg DOCUMENT, WINDOW)
			if(!skipChildren && !hiddenToo && _IsRoleToSkipIfInvisible(role) && !_IsRoleTopLevelClient(role, level)) skipChildren = state.IsInvisible();
		}

		return skipChildren ? _eMatchResult::SkipChildren : _eMatchResult::Continue;
	}

	//Gets AO state.
	//The first time calls get_accState. Later returns cached value.
	class _AccState {
		const AccRaw& _a;
		long _state;
	public:
		_AccState(ref const AccRaw& a) : _a(a) { _state = -1; }

		int State() {
			if(_state == -1) _a.get_accState(out _state);
			return _state;
		}

		//Returns: 1 INVISIBLE and not OFFSCREEN, 2 INVISIBLE and OFFSCREEN, 0 none.
		int IsInvisible() {
			switch(State() & (STATE_SYSTEM_INVISIBLE | STATE_SYSTEM_OFFSCREEN)) {
			case STATE_SYSTEM_INVISIBLE: return 1;
			case STATE_SYSTEM_INVISIBLE | STATE_SYSTEM_OFFSCREEN: return 2;
			}
			return 0;
		}
	};

	static bool _IsRoleToSkipIfInvisible(int roleE)
	{
		switch(roleE) {
		//case ROLE_SYSTEM_MENUBAR: case ROLE_SYSTEM_TITLEBAR: case ROLE_SYSTEM_SCROLLBAR: case ROLE_SYSTEM_GRIP: //nonclient, already skipped
		case ROLE_SYSTEM_WINDOW: //child control
		case ROLE_SYSTEM_DOCUMENT: //web page in Firefox, Chrome
		case ROLE_SYSTEM_PROPERTYPAGE: //page in multi-tab dialog or window
		case ROLE_SYSTEM_GROUPING: //eg some objects in Firefox
		case ROLE_SYSTEM_ALERT: //eg web browser message box. In Firefox can be some invisible alerts.
		case ROLE_SYSTEM_MENUPOPUP: //eg in Firefox.
			return true;
			//note: these roles must be the same as in elm.IsInvisible
		}
		return false;
		//note: don't add CLIENT. It is often used as default role. Although in some windows it can make faster.
		//note: don't add PANE. Too often used for various purposes.

		//problem: some frameworks mark visible offscreen objects as invisible. Eg IE, WPF, Windows controls. Not Firefox, Chrome.
		//	Can be even parent marked as invisible when child not. Then we'll not find child if parent's role is one of above.
		//	Never mind. This probably will be rare with these roles. Then user can add flag HiddenToo.
		//	But code tools should somehow detect it and add the flag.
	}

	bool _IsRoleToSkipDescendants(int role, STR roleNeeded, IAccessible* a)
	{
		switch(role) {
		case ROLE_SYSTEM_MENUITEM:
			if(!(_flags & eAF::MenuToo))
				if(!str::Switch(roleNeeded, { L"MENUITEM", L"MENUPOPUP" })) return true;
			break;
		}
		return false;
	}

	//Returns true if the AO is most likely the client area of the top-level window.
	bool _IsRoleTopLevelClient(int role, int level) {
		if(_wTL && level == 0 && !(_flags & eAF::ClientArea)) {
			switch(role) {
			case ROLE_SYSTEM_MENUBAR: case ROLE_SYSTEM_TITLEBAR: case ROLE_SYSTEM_SCROLLBAR: case ROLE_SYSTEM_GRIP: break;
			default: return true;
			}
		}
		return false;
	}

	bool _MatchRect(ref AccDtorIfElem0& a)
	{
		if(!!(_flags2 & eAF2::IsRect)) {
			long L, T, W, H;
			if(0 != a.acc->accLocation(&L, &T, &W, &H, ao::VE(a.elem))) L = T = W = H = 0;

			//note: _rect is raw AO rect, relative to the screen, not to the window/control/page. Its right/bottom actually are width/height.
			//	It is useful when you want to find AO in the object tree when you already have its another IAccessible eg retrieved from point.
			//	For example, Delm uses it to select the captured AO in the tree.
			//	Do not try to make it relative to window etc. Don't need to encourage users to use unreliable ways to find AO.

			if(!!(_flags2 & eAF2::IsRectL) && L != _rect.left) return false;
			if(!!(_flags2 & eAF2::IsRectT) && T != _rect.top) return false;
			if(!!(_flags2 & eAF2::IsRectW) && W != _rect.right) return false;
			if(!!(_flags2 & eAF2::IsRectH) && H != _rect.bottom) return false;
		}
		return true;
	}

	//Finds DOCUMENT of Firefox, Chrome or some other program.
	//Enables Chrome web page AOs.
	//Returns 0, NotFound or WaitChromeDisabled.
	HRESULT _FindDocument(HWND w, out AccRaw& ar)
	{
		assert(ar.IsEmpty());

		AccDtorIfElem0 ap_;
		if(AccessibleObjectFromWindow(w, OBJID_CLIENT, IID_IAccessible, (void**)&ap_.acc)) return (HRESULT)eError::NotFound;
		IAccessible* ap = ap_.acc;

		if(!(_flags2 & (eAF2::InFirefoxPage | eAF2::InChromePage))) {
			switch(wn::ClassNameIs(w, { L"Mozilla*", L"Chrome*" })) {
			case 1: _flags2 |= eAF2::InFirefoxPage; break;
			case 2: _flags2 |= eAF2::InChromePage; break;
			}
		}

		//FUTURE: some windows may use Chrome control in non-chrome-classnamed window.
		//	Eg old version of Spotify, IIRC. Everything was OK; probably AOs not disabled.
		//	Need to review/test more, but now I don't know such programs.

		if(!!(_flags2 & eAF2::InFirefoxPage)) {
			//To get DOCUMENT, use Navigate(0x1009). It is documented and tested on FF>=2.
			_variant_t vNav;
			int hr = ap->accNavigate(0x1009, ao::VE(), out & vNav);
			if(hr == 0 && vNav.vt == VT_DISPATCH && vNav.pdispVal && 0 == vNav.pdispVal->QueryInterface(&ar.acc) && ar.acc) {
				return 0;
				//note: don't check BUSY state, it's unreliable.
			}

			//Fails when calling first time after starting Firefox. _FindDocumentSimple too. Never mind, it is documented, let use Wait.
			//In some Firefox versions (56, 57) accNavigate(0x1009) is broken.
			//Also ocassionally fails in some pages, even if page is loaded, maybe when executing scripts.
			//	Then _FindDocumentSimple finds it. Also, the caller by default waits.

		} else if(!!(_flags2 & eAF2::InChromePage)) {
			return GetChromeDOCUMENT(w, ap, out ar, !(_flags2 & eAF2::NotInProc));
		}

		PRINTS(L"unknown browser, or failed Firefox accNavigate(0x1009). It's OK first time after Firefox starts.");
		return _FindDocumentSimple(ap, out ar, _flags2);
	}

	//Finds DOCUMENT with AccFinder::Find. Skips TREE etc.
	//Returns 0 or NotFound.
	static HRESULT _FindDocumentSimple(IAccessible* ap, out AccRaw& ar, eAF2 flags2)
	{
		AccFinder f;
		f._findDOCUMENT = &ar.acc;
		f._flags2 = flags2 & (eAF2::InChromePage | eAF2::InFirefoxPage);
		if(!!(flags2 & eAF2::InFirefoxPage)) f._flags2 |= eAF2::InFirefoxNotWebNotUIA; //skip background tabs
		f._maxLevel = 10;
		Cpp_Acc a(ap, 0);
		if(0 != f.Find(0, &a, null)) return (HRESULT)eError::NotFound;
		return 0;

		//when outproc, sometimes fails to get DOCUMENT role while enabling Chrome AOs. The caller will wait/retry.
	}

	//Used by _FindDocumentSimple.
	_eMatchResult _FindDocumentCallback(ref const AccRaw& a)
	{
		int role = a.misc.roleByte;
		if(role == ROLE_SYSTEM_DOCUMENT) {
			long state; if(0 != a.get_accState(out state) || !!(state & STATE_SYSTEM_INVISIBLE)) return _eMatchResult::SkipChildren;
			if(!!(_flags2 & eAF2::InChromePage)) { //skip devtools DOCUMENT
				Bstr b;
				if(0 == a.acc->get_accValue(ao::VE(), &b) && b) {
					//Print(b);
					if(b.Length() >= 16 && !wcsncmp(b, L"chrome-devtools:", 16)) return _eMatchResult::SkipChildren;
				}
			}
			a.acc->AddRef();
			*_findDOCUMENT = a.acc;
			_found = true;
			return _eMatchResult::Stop;
		}

		static const BYTE b[] = { ROLE_SYSTEM_MENUBAR, ROLE_SYSTEM_TITLEBAR, ROLE_SYSTEM_MENUPOPUP, ROLE_SYSTEM_TOOLBAR,
			ROLE_SYSTEM_STATUSBAR, ROLE_SYSTEM_OUTLINE, ROLE_SYSTEM_LIST, ROLE_SYSTEM_SCROLLBAR, ROLE_SYSTEM_GRIP,
			ROLE_SYSTEM_SEPARATOR, ROLE_SYSTEM_PUSHBUTTON, ROLE_SYSTEM_TEXT, ROLE_SYSTEM_STATICTEXT, ROLE_SYSTEM_TOOLTIP,
			ROLE_SYSTEM_TABLE,
		};
		for(int i = 0; i < _countof(b); i++) if(role == b[i]) return _eMatchResult::SkipChildren;
		return _eMatchResult::Continue;
	}

	//#include "IAccessible2.h"
	MIDL_INTERFACE("E89F726E-C4F4-4c19-BB19-B647D7FA8478")
		IAccessible2 : public IAccessible{ };

public:
	//Finds Chrome DOCUMENT (web page) and enables its descendant AOs.
	//Returns 0, NotFound or WaitChromeDisabled.
	static HRESULT GetChromeDOCUMENT(HWND w, IAccessible* ap, out AccRaw& ar, bool inProc)
	{
		assert(ar.IsEmpty());

		HRESULT hr = _FindDocumentSimple(ap, out ar, eAF2::InChromePage);

		//we use a window prop for the AO enabling status
		auto enablingStatus = WinFlags::Get(w) & eWinFlags::AccEnableMask;

		if(hr != 0) {
			//when not in-proc, sometimes does not find while enabling, eg fails to get role because the AO is disconnected
			if(enablingStatus == eWinFlags::AccEnableStarted) return (HRESULT)eError::WaitChromeDisabled;
			return hr;
		}
		if(!!(enablingStatus & eWinFlags::AccEnableYes)) return 0;

		//when Chrome web page AOs disabled, DOCUMENT has BUSY state, until enabling finished. Later never has BUSY state.
		bool isEnabled = false; long state, cc;
		//if(0 == ar.get_accState(out state)) Printf(L"busy: %i", state & STATE_SYSTEM_BUSY); else Print("can't get state");
		if(0 == ar.get_accState(out state) && !(state & STATE_SYSTEM_BUSY)) isEnabled = true; //not BUSY
		else if(0 == ar.acc->get_accChildCount(&cc) && cc) isEnabled = true; //or has children

		//After enabling acc also need to wait for enabled HTML properties. Until then can't get even tag and acc action.
		if(isEnabled && inProc) isEnabled = 0 != AccChromeHtmlEnabled(ar.acc);

		WinFlags::Set(w, isEnabled ? eWinFlags::AccEnableYes : eWinFlags::AccEnableStarted, eWinFlags::AccEnableMask);

		if(isEnabled) return 0;
		ar.Dispose();

		//enable web page AOs
		IAccessible2* a2 = null;
		if(QueryService(ap, &a2, &IID_IAccessible)) a2->Release();
		//succeeds inproc, fails outproc, but enables AOs anyway. QI always fails.
		//speed: < 1% of Find. First time 5%.
		//note: this is undocumented and may stop working with a new Chrome version.
		//note: with old Chrome versions need QS(ar.a), not QS(ap).
		//note: does not enable AOs if Find not called before.

		return (HRESULT)eError::WaitChromeDisabled; //let the caller wait/retry, because we cannot wait inproc. By default waits when NotFound too, but much shorter.
	}
};

HRESULT AccFind(AccFindCallback& callback, HWND w, Cpp_Acc* aParent, const Cpp_AccFindParams& ap, eAF2 flags2, out BSTR& errStr)
{
	AccFinder f(&errStr);
	if(!f.SetParams(ref ap, flags2)) return (HRESULT)eError::InvalidParameter;
	return f.Find(w, aParent, &callback);
}

//HRESULT GetChromeDOCUMENT(HWND w, IAccessible* aCLIENT, out IAccessible** ar)
//{
//	AccRaw a;
//	HRESULT hr = AccFinder::GetChromeDOCUMENT(w, aCLIENT, out a);
//	*ar = a.acc;
//	return hr;
//}

namespace inproc
{
HRESULT AccEnableChrome2(MarshalParams_AccElem* p)
{
	HWND w = (HWND)(LPARAM)p->elem;
	Smart<IAccessible> aCLIENT;
	HRESULT hr = AccessibleObjectFromWindow(w, OBJID_CLIENT, IID_IAccessible, (void**)&aCLIENT);
	if(hr) return hr;
	AccDtorIfElem0 a;
	return AccFinder::GetChromeDOCUMENT(w, aCLIENT, out a, true);
}
}

namespace outproc
{
//Returns: 0 not Chrome, 1 Chrome was already enabled, 2 Chrome enabled now.
int AccEnableChrome(HWND w, bool checkClassName)
{
	assert(!(wn::Style(w) & WS_CHILD));

	if(checkClassName && !wn::ClassNameIs(w, L"Chrome*")) return 0;

	auto wf = WinFlags::Get(w);
	if(!!(wf & eWinFlags::AccEnableYes)) return 1;
	if(!!(wf & eWinFlags::AccEnableMask)) return 0; //No or Started

	//Perf.First();
	Cpp_Acc_Agent aAgent;
	if(0 != InjectDllAndGetAgent(w, out aAgent.acc)) return 0;
	//Perf.NW();

	InProcCall c;
	auto p = (MarshalParams_AccElem*)c.AllocParams(&aAgent, InProcAction::IPA_AccEnableChrome, sizeof(MarshalParams_AccElem));
	p->elem = (int)(LPARAM)w;

	int R = 0;
	for(int i = 0; i < 100; i++) {
		//Perf.First();
		HRESULT hr = c.Call();
		//Perf.NW();
		if(hr) {
			if(hr == (HRESULT)eError::WaitChromeDisabled) R = 2;
			if(R == 2) {
				Sleep(10);
				continue;
			}
		} else {
			if(R == 2) Sleep(10);
			else R = 1;
		}
		break;
	}
	if(R == 0) WinFlags::Set(w, eWinFlags::AccEnableNo);
	return R;
}
}
