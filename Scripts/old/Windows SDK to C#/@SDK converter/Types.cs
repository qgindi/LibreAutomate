namespace SdkConverter;

unsafe partial class Converter {
	/// <summary>
	/// Converts simple typedef or function typedef declaration.
	/// _i must be at 'typedef'. Finally it will be at semicolon.
	/// </summary>
	void _DeclareTypedef() {
		//detect typedef type
		int typedefType = 0;
		//0 - typedef [struct|enum|etc] X Y...
		//1 - typedef ... (FUNCTYPE)(...)
		//2 - typedef struct|enum|etc [X] [:base] {...} Y...
		int i = ++_i;
		for (; ; i++) {
			char c = *T(i);
			if (c == '{') typedefType = 2; else if (c == '(') typedefType = 1; else if (c != ';') continue;
			break;
		}
		//print.it(_tok[_i], typedefType);
		
		var d = new _FINDTYPEDATA();
		
		if (typedefType == 2) {
			var k = _FindKeyword(_i, _KeywordT.TypeDecl);
			
			//find alias name. _DeclareType() will use it.
			i = _SkipEnclosed(i) + 1;
			if (_TokIsIdent(i) && _TokIsChar(i + 1, ';', ',')) d.inTypedefNameToken = i;
			
			_DeclareType(ref d);
		} else {
			_FindTypename(false, ref d);
			int iTypeName = _i++;
			
			if (typedefType == 1) {
				int rtPtr = 0;
				for (; _TokIsChar(_i, '*', '&'); _i++) rtPtr++;
				
				_DeclareTypedefFunc(d.outSym, rtPtr, d.outIsConst, iTypeName);
				return;
			}
		}
		
		_Symbol aliasOf = d.outSym;
		int ptr = 0; //0 - start or after comma, >0 pointer level, <0 after identifier
		int ptrBase = 0;
		
		//don't declare typedef to typedef
		if (typedefType == 0 && aliasOf is _Typedef) {
			var t = aliasOf as _Typedef;
			//print.it($"unaliased: {_tok[_i-1]} -> {t.aliasOf.csTypename}");
			aliasOf = t.aliasOf;
			ptrBase = t.ptr;
		}
		
		for (; ; _i++) {
			if (_TokIsIdent(_i)) {
				if (ptr < 0) goto ge;
				if (_TokIsChar(_i + 1, '[')) { //typedef X Y[n];
					string attr, typ = _ConvertTypeName(aliasOf, ref ptr, false, 0, _TypeContext.Member, out attr);
					Debug.Assert(attr == null); //never mind, 0 in SDK
					__DeclareTypedef_Array(typ);
				} else {
					bool defined = false;
					if (aliasOf is _CppType) {
						
						//convert LONG_PTR etc to nint, not to long/ulong
						if (_is32bit ? (aliasOf.csTypename == "int" || aliasOf.csTypename == "uint") : (aliasOf.csTypename == "long" || aliasOf.csTypename == "ulong")) {
							string name = _TokToString(_i);
							if (name.Ends("_PTR") || name == "POINTER_64_INT") {
								//print.it(_tok[_i], aliasOf.csTypename, ptr);
								aliasOf = _sym_LPARAM;
							} //else print.it(_tok[_i], aliasOf.csTypename, ptr);
							
						}
						
					} else if (_TokIs(_i, aliasOf.csTypename)) {
						defined = true;
					} else if (_nsCurrent == 0 && aliasOf.forwardDecl && ptrBase + ptr == 0) {
						string alias = _TokToString(_i);
						//if(!(aliasOf is _Struct)) print.it(aliasOf); //0 in SDK
						//print.it(aliasOf.csTypename, alias, ptr != 0);
						try { _forwardDeclTypedefs.Add(aliasOf.csTypename, _i); } catch { } //info: try/catch because SDK contains 1 duplicate typedef
					}
					
					if (!defined) _AddSymbol(_i, new _Typedef(aliasOf, ptr + ptrBase, d.outIsConst));
				}
				ptr = -1;
			} else {
				switch (*T(_i)) {
				case '*':
				case '&':
					if (ptr < 0) goto ge;
					ptr++;
					break;
				case ',':
					if (ptr >= 0) goto ge;
					ptr = 0;
					break;
				case ';':
					if (ptr >= 0) goto ge;
					return;
				default: goto ge;
				}
			}
		}
		ge:
		_Err(_i, "unexpected");
	}
	
	void __DeclareTypedef_Array(string type) {
		int iName = _i++;
		string name = _TokToString(iName);
		//print.it(name, type);
		var t = new _PARAMDATA { typeName = type, name = "a" };
		_ConvertCArray(ref t); _i--;
		_sbType.AppendFormat("\r\ninternal struct {0} {{\r\n\t{1} public {2} {3};\r\n", name, t.attributes, type, t.name);
		if (t.comment != null) _sbType.Append('\t').AppendLine(t.comment);
		_sbType.AppendLine("}");
		_AddSymbol(iName, new _Struct(name, false));
		//info:
		//SDK has ~15 such typedefs. Some of them are [1] ie used as variable-length array.
		//We convert to struct, because would be difficult to have this as typedef.
	}
	
	//_Symbol _CopyStruct(_Symbol xFrom, string name) {
	//	var t = _Struct.Copy(xFrom as _Struct, name);
	//	try { _AddSymbol(_i, t); } catch { return xFrom; } //info: 3 in SDK already defined
	//	_FormatStruct(t);
	//	return t;
	//}
	
	//_Symbol _CopyEnum(_Symbol xFrom, string name) {
	//	var t = _Enum.Copy(xFrom as _Enum, name);
	//	try { _AddSymbol(_i, t); } catch { return xFrom; } //info: 0 in SDK already defined
	//	_FormatEnum(t);
	//	return t;
	//}
	
	//When 'typedef struct X Y;', and X is forward declaration, we add X=Y to this map. Then, when defining X, we swap names Y with X.
	//Without this, everywhere would be used X, which usually is a tag name such as tagVARIANT.
	//Dictionary<string, List<_Token>> _forwardTypedeclMap = new Dictionary<string, List<_Token>>();
	Dictionary<string, int> _forwardDeclTypedefs = new Dictionary<string, int>();
	
	/// <summary>
	/// Converts function typedef to C# delegate.
	/// _i must be at the first '(' (after typename and * etc). Finally it will be at semicolon.
	/// Info: script replaced 'typedef TYPE [callConv] FUNCTYPE(' to 'typedef TYPE ([callConv]*FUNCTYPE)('.
	/// </summary>
	/// <param name="t">Return type.</param>
	/// <param name="ptr">Pointer level.</param>
	/// <param name="isConst">Is const.</param>
	/// <param name="iTokTypename">Type name token index.</param>
	/// <param name="inl">Use for inline function type definition.</param>
	void _DeclareTypedefFunc(_Symbol t, int ptr, bool isConst, int iTokTypename, _INLINEFUNCTYPEDATA inl = null) {
		//return type
		string returnAttr, returnType = _ConvertTypeName(t, ref ptr, isConst, iTokTypename, _TypeContext.Return, out returnAttr);
		
		Debug.Assert(_TokIsChar(_i, '('));
		_i++;
		
		//calling convention
		int iCallConv = 0;
		if (_TokIsIdent(_i)) iCallConv = _i++;
		if (!_TokIsChar(_i, '*')) _Err(_i, "unexpected"); //todo: maybe also can be & (use _TokIsPtr)
		_i++;
		string callConv = iCallConv != 0 ? _ConvertCallConv(iCallConv) : "Cdecl";
		
		//name
		int iName = _i;
		string name = null;
		if (inl == null) {
			if (_SymbolExists(_i, false)) _Err(_i, "name already exists");
			name = _TokToString(_i);
		} else {
			if (_TokIsIdent(_i)) inl.paramName = _TokToString(_i);
			else { _i--; inl.paramName = "param" + inl.paramIndex; }
			
			inl.typeName = name = (inl.parentName + "_" + inl.paramName);
		}
		if (!_TokIsChar(++_i, ')')) _Err(_i, "unexpected");
		_i++;
		
		//start formatting
		StringBuilder sb = inl != null ? _sbInlineDelegate : _sbType;
		sb.AppendLine();
		if (callConv != null) sb.AppendFormat("[UnmanagedFunctionPointer(CallingConvention.{0})]\r\n", callConv);
		if (returnAttr != null) sb.AppendLine(returnAttr);
		sb.AppendFormat("internal delegate {0} {1}", returnType, name);
		
		//parameters
		_ConvertParameters(sb, name, _TypeContext.DelegateParameter);
		
		var x = new _Callback(name);
		if (inl == null) _AddSymbol(iName, x);
		else _AddSymbol(name, x, iName);
		
		if (inl != null) {
			inl.t = x;
		}
	}
	
	class _INLINEFUNCTYPEDATA {
		public readonly string parentName; //in
		public int paramIndex; //in
		public string typeName; //out
		public string paramName; //out
		public _Callback t; //out
		
		public _INLINEFUNCTYPEDATA(string parentName, int paramIndex) {
			this.parentName = parentName;
			this.paramIndex = paramIndex;
		}
	}
	
	/// <summary>
	/// Converts enum definition.
	/// _i must be at 'enum'. Finally it will be at semicolon.
	/// </summary>
	void __DeclareEnum(ref _FINDTYPEDATA d) {
		//is in global namespace?
		bool isScoped = _nsCurrent > 0;
		if (_TokIs(++_i, "class") || _TokIs(_i, "struct")) { isScoped = true; _i++; }
		
		_Enum x = null;
		
		//name
		string name = null;
		int iName = 0;
		if (_TokIsIdent(_i)) {
			bool wasForwardDecl;
			if (__MustSwapTagWithAlias(d.inTypedefNameToken, out wasForwardDecl)) {
				iName = d.inTypedefNameToken;
				name = _TokToString(iName);
				_AddSymbol(iName, x = new _Enum(name, false));
				_AddSymbol(_i++, new _Typedef(x, 0, false, wasForwardDecl));
			} else {
				iName = _i++;
				name = _TokToString(iName);
				
				//is forward declaration? Rare but allowed.
				if (_TokIsChar(_i, ';')) {
					_AddSymbol(iName, d.outSym = new _Enum(name, true));
					//info: don't need name in this case
					return;
				}
			}
		}
		
		_ns[_nsCurrent + 1].Clear();
		StringBuilder sb = _ns[_nsCurrent + 1].sb;
		
		//base type
		string sBaseType = null;
		if (_TokIsChar(_i, ':')) {
			_Symbol baseType = _FindType(++_i, true);
			if (0 != _Unalias(_i, ref baseType) || !(baseType is _CppType)) _Err(_i, "unexpected");
			sBaseType = baseType.csTypename;
			_i++;
		}
		
		if (!_TokIsChar(_i, '{')) _Err(_i, "unexpected");
		_i++;
		
		if (iName == 0) {
			iName = d.inTypedefNameToken;
			if (iName == 0) {
				//borrow enum name from first member. Benefits: easier for users to find the enum; will not be a name conflict; easy.
				if (!_TokIsIdent(_i)) _Err(_i, "unexpected");
				iName = _i;
				//print.it(_TokToString(iName));
			}
			name = _TokToString(iName);
		}
		
		//body
		sb.AppendLine(" {");
		bool isFlags = false;
		uint value = 0, nextValue = 0;
		for (; ; nextValue++) {
			if (!_TokIsIdent(_i)) _Err(_i, "unexpected");
			int iMember = _i++;
			string memberName = _TokToString(iMember);
			sb.Append('\t'); sb.Append(memberName);
			char c = *T(_i);
			if (c == '=') {
				int i0 = ++_i;
				while (!_TokIsChar(_i, ',', '}')) _i++;
				if (_i == i0) _Err(_i, "unexpected");
				
				_ExpressionResult r = _Expression(i0, _i, memberName);
				switch (r.typeS) {
				case "int":
					value = r.valueI;
					break;
				case "uint":
					value = r.valueI;
					if (!isFlags) { isFlags = true; sBaseType = "uint"; }
					break;
				default:
					_Err(i0, "cannot convert");
					break;
				}
				
				if (value != nextValue) {
					sb.Append(" = ");
					sb.Append(r.valueS);
				}
				
				nextValue = value;
			} else if (c == ',' || c == '}') {
				value = nextValue;
			} else _Err(_i, "unexpected");
			
			//add to a dictionary, to resolve other enum and #define
			if (!isScoped) {
				ulong v = value; if (isFlags) v |= 0x8000000000000000UL;
				//print.it("add", _tok[iMember]);
				_enumValues[_tok[iMember]] = v;
			}
			
			if (_TokIsChar(_i, ',')) _i++;
			if (_TokIsChar(_i, '}')) break;
			sb.AppendLine(",");
		}
		sb.AppendLine("\r\n}");
		_i++;
		
		if (sBaseType != null) sb.Insert(0, " :" + sBaseType);
		
		if (x == null) {
			if (_TryFindSymbolAs(iName, out x, false) && x.forwardDecl == true) x.forwardDecl = false;
			else _AddSymbol(iName, x = new _Enum(name, false));
		}
		
		sb.CopyTo(0, x.defAfterName = new char[sb.Length], 0, sb.Length);
		x.isFlags = isFlags;
		
		d.outSym = x;
		
		_FormatEnum(x);
	}
	
	Dictionary<_Token, ulong> _enumValues = new Dictionary<_Token, ulong>();
	bool _EnumFindValue(int iTokName, out ulong value, out _OP type) {
		type = _OP.OperandInt;
		//print.it("find", _tok[iTokName]);
		if (!_enumValues.TryGetValue(_tok[iTokName], out value)) return false;
		if ((value & 0x8000000000000000UL) != 0) { value = (uint)value; type = _OP.OperandUint; }
		return true;
	}
	
	void _FormatEnum(_Enum x) {
		StringBuilder sb = _sbType;
		sb.AppendLine();
		if (x.isFlags) sb.AppendLine("[Flags]");
		sb.Append("internal enum ");
		sb.Append(x.csTypename);
		sb.Append(x.defAfterName);
	}
	
	/// <summary>
	/// Converts struct/union/class/__interface/enum/typedef definition or forward declaration.
	/// _i must be at 'struct' etc. Finally it will be after }. If skipVariablesUntilSemicolon - at ;.
	/// </summary>
	void _DeclareType(ref _FINDTYPEDATA d, bool skipVariablesUntilSemicolon = false) {
		//struct, union, class, or __interface?
		bool isUnion = false, isInterface = false, isDualInterface = false, isClass = false;
		string access = "public", inheritInterface = null;
		char keyword = *T(_i);
		switch (keyword) {
		case 't': _DeclareTypedef(); return;
		case 'e': __DeclareEnum(ref d); return;
		case 'u': isUnion = true; break;
		case 'c': isClass = true; access = "private"; break;
		case '_': isInterface = true; break;
		}
		_i++;
		
		//uuid (was '__declspec(uuid', replaced in script)
		string uuid = null;
		if (_TokIs(_i, "uuid")) {
			if (!_TokIsChar(++_i, '(') || !_TokIsChar(++_i, '"') || !_TokIsChar(_i + 1, ')')) _Err(_i, "unexpected");
			uuid = _TokToString(_i);
			_i += 2;
		}
		
		//name
		_Struct x = null;
		int iName = 0;
		string name = null;
		if (_TokIsIdent(_i)) {
			bool isThisForwardDecl = _TokIsChar(_i + 1, ';');
			
			//look in _forwardDeclTypedefs, maybe need to swap this tag with previously declared alias
			if (d.inTypedefNameToken == 0 && _nsCurrent == 0 && !isThisForwardDecl) {
				string tag = _TokToString(_i);
				if (_forwardDeclTypedefs.TryGetValue(tag, out int k)) {
					//print.it($"<><c 0xff0000>{tag}, {_TokToString(k)}</c>");
					d.inTypedefNameToken = k;
					_forwardDeclTypedefs.Remove(tag);
				}
			}
			
			if (!isThisForwardDecl && __MustSwapTagWithAlias(d.inTypedefNameToken, out bool wasForwardDecl)) {
				iName = d.inTypedefNameToken;
				name = _TokToString(iName);
				_AddSymbol(iName, x = new _Struct(name, false));
				_AddSymbol(_i++, new _Typedef(x, 0, false, wasForwardDecl));
			} else {
				iName = _i++;
				name = _TokToString(iName);
				
				//forward-declare this because may contain a member of this pointer type
				if (!(_TryFindSymbolAs(iName, out x, false) && x.forwardDecl)) //info: if !x.forwardDecl _AddSymbol will throw
					_AddSymbol(iName, x = new _Struct(name, isThisForwardDecl));
				
				if (isThisForwardDecl) {
					d.outSym = x;
					x.isInterface = isInterface;
					x.isClass = isClass;
					//info: don't need name in this case
					//if(uuid != null) print.it(isClass); //all True
					if (isClass && uuid != null && _nsCurrent == 0) { //coclass
						//print.it(keyword, name, uuid);
						_DeclareGuid("CLSID_", name, uuid);
						_sbInterface.AppendFormat(
							"\r\n[ComImport, Guid({0}), ClassInterface(ClassInterfaceType.None)]"
							+ "\r\ninternal class {1} {{}}\r\n"
							, uuid, name);
					} else if (!isInterface && _interfaces.Contains(name)) {
						x.isInterface = true;
					}
					return;
				}
			}
		} else if (d.inTypedefNameToken != 0) {
			iName = d.inTypedefNameToken;
			name = _TokToString(iName);
		} //later will get name from variable
		
		//skip IUnknown and IDispatch
		if (isInterface || uuid != null) {
			bool skip = false;
			if (!__haveIUnknown && name == "IUnknown") __haveIUnknown = skip = true;
			else if (!__haveIDispatch && name == "IDispatch") __haveIDispatch = skip = true;
			if (skip) {
				_SkipStatement();
				x.forwardDecl = false;
				x.isInterface = true;
				x.isDualInterface = name == "IDispatch";
				_DeclareGuid("IID_", name, uuid);
				return;
			}
		}
		
		//is interface?
		if (__haveIUnknown && (isInterface || uuid != null) && _TokIsChar(_i, ':')) {
			//info: SDK has only few non-interface struct that have a base, and they don't have uuid.
			Debug.Assert(_interfaces.Contains(name));
			isInterface = true;
			_DeclareGuid("IID_", name, uuid);
		}
		
		//get empty namespace (symbols + StringBuilder) from stack
		_ns[_nsCurrent + 1].Clear();
		StringBuilder sb = _ns[_nsCurrent + 1].sb;
		
		//base type
		if (_TokIsChar(_i, ':')) {
			_i++;
			if (_TokIsIdent(_i + 1) && _FindKeyword(_i).kwType == _KeywordT.PubPrivProt) _i++;
			
			bool addedBaseMembers = false;
			for (int iBase = 0; ; iBase++, _i++) { //support multiple inheritance, although currently none in SDK
				_Symbol baseType = _FindType(_i, true);
				int ptr = _Unalias(_i, ref baseType);
				var bt = baseType as _Struct;
				if (bt == null || bt.forwardDecl || ptr != 0) _Err(_i, "unexpected");
				if (bt.members != null) { //null if IUnknown/IDispatch or no members
					if (bt.members[0] != '/') {
						sb.Append("// ");
						sb.AppendLine(bt.csTypename);
					}
					if (isInterface) {
						//print.it(iBase, bt.csTypename); //iBase all 0
						inheritInterface = inheritInterface == null ? bt.csTypename : inheritInterface + ", " + bt.csTypename;
						sb.Append(new string(bt.members).RxReplace(@"\[PreserveSig\] (?!new )\K", "new "));
						//info: for COM interfaces need to add base members, even if base interface specified.
					} else {
						sb.Append(bt.members);
					}
					addedBaseMembers = true;
				}
				if (isInterface) isDualInterface = bt.isDualInterface; //inherits IDispatch or another dual interface
				if (!_TokIsChar(++_i, ',')) break;
			}
			if (addedBaseMembers) { sb.Append("// "); sb.AppendLine(name); }
		}
		
		//members
		if (!_TokIsChar(_i, '{')) _Err(_i, "unexpected");
		int iBodyStart = _i++;
		string memberAttr = isUnion ? "[FieldOffset(0)] " : null;
		bool hasBitfields = false;
		
		if (_nsCurrent == 0) _anonymousStructSuffixCounter = 0;
		if (!isInterface) _sbType = _ns[++_nsCurrent].sb;
		
		for (int iMember = 1, iBitfields = 1, iInterfaceFunc = 1; !_TokIsChar(_i, '}'); _i++, iMember++) {
			
			//public/private/protected?
			if (_TokIsChar(_i + 1, ':') && 0 != _TokIs(_i, "public", "private", "protected")) {
				access = _TokToString(_i);
				_i++;
				continue;
			}
			
			//template etc?
			if (_TokIs(_i, "template")) {
				_SkipStatement();
				continue;
			}
			
			//interface function?
			if (isInterface) {
				_InterfaceFunction(sb, iInterfaceFunc++);
				continue;
			}
			
			//bitfield?
			if (__IsBitfield()) {
				_Bitfields(ref iBitfields, memberAttr);
				hasBitfields = true;
				continue;
			}
			
			//type
			_ParseParamOrMember(_TypeContext.Member, out _PARAMDATA t, name ?? "TYPEOF", iMember);
			
			if (t.isNestedTypeDefinitionWithoutVariables) {
				//if anonymous type definition without a variable, its members are part of parent in C++, therefore in C# need to add a variable of that type
				if (t.isAnonymousTypeDefinitionWithoutVariables) {
					sb.AppendFormat("{3}{2} {0} {1};\r\n", t.typeName, "_" + iMember, access, memberAttr);
				}
				continue;
			}
			
			//member variable
			for (; ; ) { //support TYPE a, b;
				switch (*T(_i)) {
				case ';': case ',': break;
				case '\0': _Err(iBodyStart, "no }"); break;
				default: _Err(_i, "unexpected"); break;
				}
				
				if (memberAttr != null) {
					if (t.attributes != null && t.attributes.Starts("//")) sb.Append("//"); //could instead place memberAttr after attributes, but then looks not good visually
					sb.Append(memberAttr);
				}
				if (t.attributes != null) { sb.Append(t.attributes); sb.Append(' '); }
				sb.AppendFormat("{2} {0} {1};\r\n", t.typeName, t.name, access);
				if (t.comment != null) {
					sb.AppendLine(t.comment);
					t.comment = null;
				}
				if (!_TokIsChar(_i, ',')) break;
				_i++;
				
				//ignore *. Assume never will be 'TYPE *a, b;' or 'TYPE a, *b;.
				while (_TokIsChar(_i, '*', '&')) _i++;
				//int ptr = 0;
				//while(_TokIsChar(_i, '*', '&')) { _i++; ptr++; }
				//print.it(ptr);
				
				if (!_TokIsIdent(_i)) _Err(_i, "unexpected");
				t.name = _TokToString(_i++);
				
				if (_TokIsChar(_i, '[')) {
					//_Err(_i, "example"); //0 in SDK
					t.attributes = null;
					_ConvertCArray(ref t);
				} else if (t.attributes != null) {
					_Err(_i, "unexpected"); //assume never will be 'TYPE a[n], b'
				}
			}
		}
		
		if (!isInterface) _sbType = _ns[--_nsCurrent].sb;
		
		_i++; //skip }
		if (skipVariablesUntilSemicolon) {
			//skip variables after }
			while (!_TokIsChar(_i, ';', '\0')) _i++;
		}
		
		if (iName == 0) {
			if (skipVariablesUntilSemicolon) return; //skip this struct/var definition (it is global 'struct {...} var;')
			
			//use variable name from 'struct{...}var;', or _anonymousStructSuffixCounter if just 'struct{...};'
			bool isVar = _TokIsIdent(_i);
			name = isVar ? ("TYPEOF_" + _TokToString(_i)) : ("TYPE_" + (++_anonymousStructSuffixCounter).ToString());
			_AddSymbol(name, x = new _Struct(name, false), _i);
			
			d.outIsAnonymousTypeDefinition = true;
		} else {
			if (x == null) _AddSymbol(name, x = new _Struct(name, false), iName); //typedef struct {...} name;
			else x.forwardDecl = false;
		}
		
		d.outSym = x;
		
		if (sb.Length > 0) {
			sb.CopyTo(0, x.members = new char[sb.Length], 0, sb.Length);
			sb.Clear();
		}
		
		//attributes
		if (isInterface) {
			sb.AppendFormat("[ComImport, Guid({0}){1}\r\n", uuid, isDualInterface ? "] //dual" : ", InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]");
			x.isInterface = true;
			x.isDualInterface = isDualInterface;
		} else {
			if (hasBitfields) sb.AppendLine("[DebuggerStepThrough]");
			if (_pack != 8) sb.AppendFormat("[StructLayout(LayoutKind.{0}, Pack={1})]\r\n", isUnion ? "Explicit" : "Sequential", _pack);
			else if (isUnion) sb.AppendLine("[StructLayout(LayoutKind.Explicit)]");
		}
		if (sb.Length > 0) sb.CopyTo(0, x.attributes = new char[sb.Length], 0, sb.Length);
		
		_FormatStruct(x, inheritInterface);
	}
	
	void _FormatStruct(_Struct x, string inheritInterface) {
		StringBuilder sb = x.isInterface ? _sbInterface : _sbType;
		sb.AppendLine();
		if (x.attributes != null) sb.Append(x.attributes);
		sb.Append(x.isInterface ? "internal interface " : "internal struct ");
		sb.Append(x.csTypename);
		if (inheritInterface != null) sb.Append(" : ").Append(inheritInterface);
		if (x.members != null) {
			sb.Append(" {\r\n");
			int offs = sb.Length;
			sb.Append(x.members);
			sb.Replace("\n", "\n\t", offs - 2, x.members.Length); //tab-indent members
			sb.AppendLine("}");
		} else sb.AppendLine(" { }");
	}
	
	//Returns true if must declare aliasToken as this struct|enum, and this struct|enum as aliasToken.
	//For example, it changes 'typedef struct tagX{...} X;' to 'typedef struct X{...} tagX;'.
	bool __MustSwapTagWithAlias(int aliasToken, out bool wasForwardDecl) {
		wasForwardDecl = false;
		if (aliasToken == 0) return false;
		if (_nsCurrent != 0) return false;
		if (_tok[_i].Equals(_tok[aliasToken])) return false; //typedef struct X{...}X
		
		__RemoveForwardDeclIfExists(aliasToken, false);
		wasForwardDecl = __RemoveForwardDeclIfExists(_i, true);
		
		return true;
	}
	
	bool __RemoveForwardDeclIfExists(int iTok, bool tag) {
		Debug.Assert(_nsCurrent == 0);
		var d = _ns[0].sym;
		_Token token = _tok[iTok];
		_Symbol x;
		if (!d.TryGetValue(token, out x)) return false;
		int ptr = _Unalias(iTok, ref x);
		//print.it(tag, x.GetType(), _TokToString(iTok), x.csTypename, ptr, x.forwardDecl);
		if (!x.forwardDecl || ptr != 0) _Err(iTok, "already exists");
		d.Remove(token);
		return true;
	}
	
	#region bitfields
	
	void _Bitfields(ref int iBitfields, string memberAttr) {
		int nBitsType = 0, nBitsTypePrev = 0, fieldOffset = 0;
		string csTypeName = null, bitHolderVar = null;
		bool isUnsigned;
		
		for (; ; ) {
			//type
			nBitsType = __BitfieldGetType(out csTypeName, out isUnsigned);
			if (nBitsType != nBitsTypePrev) { nBitsTypePrev = nBitsType; bitHolderVar = null; }
			_i++;
			g1:
			//name
			string varName = _TokIsChar(_i, ':') ? null : _TokToString(_i++);
			_i++;
			//field size
			int fieldSize = Api.strtoi(T(_i));
			if (fieldSize < 1 || fieldSize > nBitsType) _Err(_i, "unexpected");
			_i++;
			//add private member variable that holds the bitfields
			if (bitHolderVar == null || fieldOffset + fieldSize > nBitsType) {
				fieldOffset = 0;
				bitHolderVar = "__bf_" + iBitfields++;
				_sbType.AppendFormat("{2}private {0} {1};\r\n", csTypeName, bitHolderVar, memberAttr);
			}
			//add property that get/set the bitfield
			if (varName != null) {
				ulong mask = (1UL << fieldSize) - 1;
				ulong maskWithOffset = mask << fieldOffset;
				
				//work around C# problems with signed/unsigned conversion
				string op = "~";
				if (nBitsType > 32) {
					if (maskWithOffset > long.MaxValue) { maskWithOffset = ~maskWithOffset; op = null; }
				} else {
					if (maskWithOffset > int.MaxValue) { maskWithOffset = ~maskWithOffset & 0xffffffff; op = null; }
				}
				
				_sbType.AppendFormat(
					"public {0} {1} {{ get {{ return {8}({2} >> {3} & 0x{4:X}{7}); }} " +
					"set {{ {2} = {8}(({2} & {6}0x{5:X}{7}) | ((value & 0x{4:X}{7}) << {3})); }} }}\r\n",
					csTypeName, varName, bitHolderVar, fieldOffset, mask, maskWithOffset,
					op, isUnsigned ? "U" : "", nBitsType < 32 ? "(" + csTypeName + ")" : "");
			}
			
			fieldOffset += fieldSize;
			
			//next bitfield?
			if (_TokIsChar(_i++, ',')) {
				if (!(_TokIsChar(_i + 1, ':') && _TokIsIdent(_i) && _TokIsChar(_i + 3, ';', ','))) _Err(_i, "unexpected");
				goto g1;
			}
			if (!__IsBitfield()) break;
		}
		
		_i--;
	}
	
	bool __IsBitfield() {
		int i = _i;
		if (_TokIsChar(i + 2, ':')) {
			if (!_TokIsIdent(i++)) return false;
		} else if (!_TokIsChar(i + 1, ':')) return false; //nameless?
		
		return _TokIsIdent(i) && _IsCharDigit(*T(i + 2)) && _TokIsChar(i + 3, ';', ',');
	}
	
	/// <summary>
	/// Returns bitfield size in bits. Gets C# type name.
	/// </summary>
	int __BitfieldGetType(out string csTypename, out bool isUnsigned) {
		csTypename = null;
		isUnsigned = false;
		_Symbol t = _FindType(_i, true);
		if (0 == _Unalias(_i, ref t)) {
			var ct = t as _CppType;
			if (ct != null && ct.sizeBytesCpp != 0) {
				csTypename = ct.csTypename;
				isUnsigned = ct.isUnsigned;
				return ct.sizeBytesCpp * 8;
			} else if (t == _sym_LPARAM) {
				csTypename = _is32bit ? "int" : "long";
				return _is32bit ? 32 : 64;
				//SDK has 1 such struct (union PSAPI_WORKING_SET_BLOCK), and there the bitfield is at the end, so in most cases it is safe to use C# long instead. Using nint is difficult because of its variable size.
			} else if(t is _Enum te) {
				csTypename = "int";
				return 32;
			}
		}
		_Err(_i, "unexpected");
		return 0;
	}
	
	#endregion
	
	bool __haveIUnknown, __haveIDispatch;
	
	void _DeclareGuid(string prefix, string name, string uuid) {
		if (uuid == null) return;
		string fullName = prefix + name;
		if (_guids.ContainsKey(fullName)) return;
		if (prefix == "IID_" && _guids.ContainsKey("DIID_" + name)) return;
		//print.it(fullName);
		_sbVar.AppendFormat("\r\ninternal static Guid {0} = new({1});\r\n", fullName, uuid);
		//note: not 'readonly' because then could not be passed as ref.
	}
	
	void _InterfaceFunction(StringBuilder sb, int index) {
		if (_TokIs(_i, "virtual")) _i++;
		
		var d = new _FINDTYPEDATA();
		_FindTypename(false, ref d);
		_i++;
		
		//pointer
		int ptr = 0;
		while (_TokIsChar(_i, '*', '&')) { _i++; ptr++; }
		
		if (_TokIs(_i, "__stdcall")) _i++;
		
		string name = _TokToString(_i++);
		
		//some interfaces have IUnknown members
		bool skip = false;
		if (index <= 3) {
			if (index == 1 && name == "QueryInterface") skip = true;
			if (index == 2 && name == "AddRef") skip = true;
			if (index == 3 && name == "Release") skip = true;
		}
		if (skip) {
			_SkipEnclosed();
			_i++;
		} else {
			string returnAttr, returnType = _ConvertTypeName(d.outSym, ref ptr, d.outIsConst, d.outTypenameToken, _TypeContext.ComReturn, out returnAttr);
			
			//correct preprocessor-replaced method names that matched #define Func FuncW. Also there are several such struct members, never mind.
			if (name.Ends("W") && name.Length > 2 && char.IsLower(name[name.Length - 2])) {
				//print.it(name);
				name = name.Remove(name.Length - 1);
			}
			
			//start formatting
			sb.Append("[PreserveSig] ");
			if (returnAttr != null) { sb.Append(returnAttr); sb.Append(' '); }
			sb.Append(returnType); sb.Append(' '); sb.Append(name);
			
			//parameters
			_ConvertParameters(sb, name, _TypeContext.ComParameter);
		}
		
		//skip '= 0'
		if (_TokIsChar(_i, '=')) _i += 2;
	}
	
	/// <summary>
	/// Processes inline forward declaration such as 'struct X' in 'struct X * param' or 'struct X * Func(..)'.
	/// _i must be at 'struct' etc. Finally will be after it (_i++).
	/// Returns _Struct or _Enum, unaliased if was _Typedef.
	/// </summary>
	_Symbol _InlineForwardDeclaration() {
		bool en = *T(_i++) == 'e';
		_Symbol x;
		if (_TryFindSymbol(_i, out x, true) && 0 == _Unalias(_i, ref x)) {
			if (en) { if (x is _Enum) return x; } else if (x is _Struct) return x;
		}
		string name = _TokToString(_i);
		if (en) {
			x = new _Enum(name, true);
		} else {
			_Struct ts = new _Struct(name, true);
			x = ts;
			if (_interfaces.Contains(name)) ts.isInterface = true;
		}
		_AddSymbol(_i, x, true);
		return x;
	}
	
	/// <summary>
	/// Joins _sbType, _sbInlineDelegate, _func, and _sbInterface into single string.
	/// Replaces some identifiers in it etc.
	/// Clears the stringbuilders.
	/// </summary>
	string _PostProcessTypesFunctionsInterfaces() {
		//join types etc into single string, for replacements
		var sb = new StringBuilder();
		sb.Append("\r\n// TYPE\r\n");
		sb.Append(_sbType); _sbType.Clear();
		sb.Append(_sbInlineDelegate); _sbInlineDelegate.Clear();
		sb.Append("\r\n// FUNCTION\r\n");
		foreach (var v in _func) { sb.Append(v.Value); }
		sb.Append("\r\n// INTERFACE\r\n");
		sb.Append(_sbInterface); _sbInterface.Clear();
		string R = sb.ToString(); sb.Clear();
		
		//perf.first();
		foreach (var v in _ns[0].sym) {
			var sym = v.Value;
			
			//most forward declarations were converted to definitions, but some don't have definitions; replace their pointers to IntPtr
			if (sym.forwardDecl) {
				var ts = sym as _Struct;
				//if(ts==null) print.it(v.Key); //0 in SDK
				if (ts != null && ts.isClass) continue; //all in SDK are with GUID, converted to coclass
				string s = v.Key.ToString();
				
				//Replace all found TYPE* and ref TYPE to IntPtr.
				//Call RxReplace 2 times because regex (a|b) is very slow.
				string repl = "IntPtr";
				int n;
				if (ts != null && ts.isInterface) n = R.RxReplace($@"\b{s}\b", repl, out R); //decremented pointer level. 0 in SDK
				else n = R.RxReplace($@"\b{s}\*", repl, out R) + R.RxReplace($@"\bref {s}\b", repl, out R);
				if (n != 0) {
					//print.it($"<><c 0xff0000>{n}  {s}* = IntPtr</c>");
					continue;
				}
				
				//if(_FindIdentifierInString(R, s) < 0) { print.it("no", s); continue; } //5 in SDK
				//print.it(sym.csTypename, sym); //0 in SDK
				continue;
			}
			
			//process typedefs
			if (sym is _Typedef td) {
				if (!td.wasForwardDecl) continue; //don't need to replace. Makes this replacement code 10 times faster.
				if (td.ptr != 0) continue; //the type is or will be resolved in some way
				_Symbol t = td.aliasOf;
				if (t.forwardDecl) continue; //the type is already resolved in some way
				if (!((t is _Struct) || (t is _Enum))) continue;
				string s = v.Key.ToString();
				//if(_FindIdentifierInString(R, s) >= 0) { //makes slower
				//if(ts!=null) _FormatStruct(ts); else _FormatEnum(te);
				if (0 != R.RxReplace($@"\b{s}\b", t.csTypename, out R)) {
					//print.it($"<><c 0xff0000>{s} = {t.csTypename}    {t}</c>"); //all struct (0 enum) in SDK
					continue;
				}
				//print.it($"<><c 0x8000>{s} = {t.csTypename}    {t}</c>"); //OK, just not used between typedef and struct definition
			}
		}
		//perf.nw();
		
		//remove W from struct/interface/delegate names
		//perf.first();
		foreach (var v in _defineW) {
			__RemoveAW(ref R, v.Key, v.Value);
		}
		//perf.next();
		foreach (var v in _ns[0].sym) { //some A/W are not #define but typedef, eg 'typedef MIXERCAPSW MIXERCAPS;'
			var td = v.Value as _Typedef; if (td == null) continue;
			if (td.forwardDecl || td.ptr != 0) continue;
			string name, nameW = td.aliasOf.csTypename;
			int suffixLen = 0;
			if (v.Key.len == nameW.Length - 1 && nameW.Ends("W")) suffixLen = 1;
			else if (v.Key.len == nameW.Length - 2 && nameW.Ends("_W")) suffixLen = 2;
			else continue;
			name = v.Key.ToString(); if (!nameW.Starts(name)) continue;
			int what; //struct/interface/delegate
			var t = td.aliasOf as _Struct;
			if (t != null) what = t.isInterface ? 1 : 0; else if (td.aliasOf is _Callback) what = 2; else continue;
			if (suffixLen == 2) what |= 0x10000;
			__RemoveAW(ref R, name, what);
		}
		//perf.nw(); //800 1000 ms
		
		//remove other known A and old versions
		R.RxReplace(@"(?ms)^internal struct PROPSHEETPAGE(?!W_V4\b).+?^\}\r\n", "", out R);
		R.RxReplace(@"(?ms)^internal struct PROPSHEETHEADER(?!W_V2\b).+?^\}\r\n", "", out R);
		R.RxReplace(@"\bPROPSHEETPAGEW_V4\b", "PROPSHEETPAGE", out R);
		R.RxReplace(@"\bPROPSHEETHEADERW_V2\b", "PROPSHEETHEADER", out R);
		R.RxReplace(@"(?ms)^internal struct OPENFILENAME_NT4\b.+?^\}\r\n", "", out R, 1);
		
		//replace some types
		R = R.RxReplace(@"\bLPARAM\b", "nint");
		R = R.RxReplace(@"(?<!\bstruct )\bU?LARGE_INTEGER\b", "long");
		
		//in VARIANT, PROPVARIANT, _wireVARIANT members replace non-blittable types with IntPtr. Also replace some other types.
		R = R.RxReplace(@"(?ms)^internal struct (?:PROP|_wire)?VARIANT \{\R\K.+?\R\}", __VariantMembers);
		
		return R;
		
		//what: 0 struct, 1 interface, 2 delegate, flag 0x10000 _W
		static void __RemoveAW(ref string R, string name, int what) {
			string sW = "W", sA = "A";
			if ((what & 0x10000) != 0) { sW = "_W"; sA = "_A"; }
			what &= 0xff;
			
			if (0 != R.RxReplace($@"\b{name}{sW}\b", name, out R)) {
				//print.it($"<><c 0xff0000>{name}</c>");
				
				//remove STRUCTA
				
				g1:
				string rx = null;
				switch (what) {
				case 0: rx = $@"(?ms)^internal struct {name}{sA} .+?^\}}\r\n\r\n"; break;
				case 1: rx = $@"(?ms)^internal interface {name}{sA} .+?^\}}\r\n\r\n"; break;
				case 2: rx = $@"(?m)^internal delegate \w+\** {name}{sA}\(.+\r\n\r\n"; break;
				}
				if (R.RxMatch(rx, out var m)) {
					//find attributes (regex with attributes would be very slow)
					int i = m.Start - 3;
					while (0 == string.Compare(R, i, "]\r\n", 0, 3)) {
						//print.it("attr");
						i = R.LastIndexOf('\n', i) - 2;
					}
					i += 3;
					
					R = R.Remove(i, m.Start - i + m.Length);
					//print.it(what, name, R.RegexIs_($"\b{name}\b")); //all False
				} else { //2 in SDK have TYPE/TYPEW instead of TYPEA/TYPEW
					//print.it(what, name);
					if (sA != null) {
						sA = null;
						goto g1;
					}
				}
			} else {
				//print.it($"<><c 0xff>{name}</c>"); //0
				//print.it(R.Find($"internal struct {name}A "));
			}
		}
		
		static string __VariantMembers(RXMatch m) {
			var s = m.Value;
			//print.it(s);
			s = s.RxReplace(@"\[MarshalAs.+?\] ", "");
			s = s.RxReplace(@"\b(string|object|I[A-Z]\w+)\b", "IntPtr");
			s = s.RxReplace(@"\bDateTime\b", "double");
			s = s.RxReplace(@"\b(CY|FILETIME)\b", "long");
			s = s.RxReplace(@"\bVERSIONEDSTREAM\*", "IntPtr");
			//print.it(s);
			return s;
		}
	}
}
