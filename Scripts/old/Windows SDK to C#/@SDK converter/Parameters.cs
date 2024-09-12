namespace SdkConverter;

unsafe partial class Converter {
	
	struct _PARAMETER {
		public int iTok;
		public int nTok;
		public _PARAMETER(int iTok, int nTok) { this.iTok = iTok; this.nTok = nTok; }
	}
	
	/// <summary>
	/// Used with all non-nested _GetParameters().
	/// </summary>
	List<_PARAMETER> _params = new();
	
	/// <summary>
	/// Parses parameters and stores their iTok/nTok in p.
	/// Error if _i is not at the starting '('. Finally _i will be at the ending ')'.
	/// Returns the number of parameters.
	/// Currently used only with pragma.
	/// </summary>
	int _GetParameters(List<_PARAMETER> p) {
		p.Clear();
		
		char* s = T(_i);
		if (*s != '(') _Err(s, "expected (");
		
		for (int paramStart = 0, iVeryStart = _i++; ; _i++) {
			s = T(_i);
			char c = *s;
			switch (c) {
			case ',':
			case ')':
				if (paramStart == 0) {
					if (c == ')' && p.Count == 0) return 0; //()
					_Err(s, "unexpected");
				}
				p.Add(new _PARAMETER(paramStart, _i - paramStart));
				if (c == ')') return p.Count;
				paramStart = 0;
				continue;
			case '\0':
				_Err(iVeryStart, "no )");
				break;
			case ';':
				_Err(s, "unexpected");
				break;
			}
			
			if (paramStart == 0) paramStart = _i;
			
			switch (c) {
			case '(':
			case '[':
			//case '<': //can be operator
			case '{':
				_SkipEnclosed();
				break;
			}
		}
	}
	
	/// <summary>
	/// Converts function parameters to C# and appends to sb like "(...);\r\n".
	/// _i must be at '('. Finally will be after ')'.
	/// </summary>
	void _ConvertParameters(StringBuilder sb, string parentName, _TypeContext context) {
		if (!_TokIsChar(_i, '(')) _Err(_i, "unexpected");
		_i++;
		sb.Append('(');
		
		if (!_TokIsChar(_i, ')')) {
			for (int iParam = 0; ; iParam++, _i++) {
				
				//ignore ...
				if (_TokIsChar(_i, '.') && _TokIsChar(_i + 1, '.') && _TokIsChar(_i + 2, '.') && _TokIsChar(_i + 3, ')')) {
					_i += 3;
					sb.Remove(sb.Length - 2, 2); //", "
					break;
				}
				
				_PARAMDATA t;
				_ParseParamOrMember(context, out t, parentName, iParam + 1);
				
				//skip default value of optional parameter
				if (_TokIsChar(_i, '=')) {
					while (!_TokIsChar(++_i, ',', ')')) { }
				}
				
				//is ',' or ')'?
				bool isLast = _TokIsChar(_i, ')');
				if (!isLast && !_TokIsChar(_i, ',')) _Err(_i, "unexpected");
				
				if (t.attributes != null) { sb.Append(t.attributes); sb.Append(' '); }
				sb.Append(t.typeName); sb.Append(' '); sb.Append(t.name);
				if (!isLast) sb.Append(", ");
				
				if (isLast) break;
			}
		}
		_i++; //skip ')'
		sb.Append(");\r\n");
	}
	
	struct _PARAMDATA {
		public string typeName; //C# typename
		public string name; //parameter name
		public string attributes; //C# attributes
		public string comment; //comment after member
		public bool isNestedTypeDefinitionWithoutVariables;
		public bool isAnonymousTypeDefinitionWithoutVariables;
	}
	
	/// <summary>
	/// Converts C++ function parameter or struct member definition (type, name, const etc) to C# typename/name/etc.
	/// _i must be at its start. Finally it will be after the parsed part. Does not check whether it is followed by ',' etc.
	/// </summary>
	/// <param name="context">Member, Parameter or DelegateParameter.</param>
	/// <param name="t">Receives C# typename, name etc.</param>
	/// <param name="parentName">The function/struct name. Used to auto-create names for nameless parameters.</param>
	/// <param name="iParam">1-based index of parameter/member. Used to auto-create names for nameless parameters.</param>
	void _ParseParamOrMember(_TypeContext context, out _PARAMDATA t, string parentName, int iParam) {
		bool isMember = context == _TypeContext.Member;
		
		t = new _PARAMDATA();
		
		int iSAL = 0;
		if (_TokIsChar(_i, '^') && _TokIsChar(_i + 1, '"')) {
			_i++;
			if (!isMember) {
				iSAL = _i;
				_tok[iSAL] = new _Token(_tok[iSAL].s + 1, _tok[iSAL].len - 2);
			}
			_i++;
		}
		
		var d = new _FINDTYPEDATA();
		_FindTypename(isMember, ref d);
		if (d.outIsNestedTypeDefinition) {
			if (_TokIsChar(_i, ';')) {
				t.isNestedTypeDefinitionWithoutVariables = true;
				t.isAnonymousTypeDefinitionWithoutVariables = d.outIsAnonymousTypeDefinition;
				t.typeName = d.outSym.csTypename;
				return;
			}
		} else _i++;
		
		//pointer
		int ptr = 0;
		while (_TokIsChar(_i, '*', '&')) { _i++; ptr++; }
		
		//suport inline function type definition
		bool isFunc = !d.outIsNestedTypeDefinition && _DetectIsFuncType(_i);
		if (isFunc) {
			var f = new _INLINEFUNCTYPEDATA(parentName, iParam);
			_DeclareTypedefFunc(d.outSym, ptr, d.outIsConst, d.outTypenameToken, f);
			t.typeName = f.typeName;
			t.name = f.paramName;
		} else {
			//typename, param/member name
			t.typeName = _ConvertTypeName(d.outSym, ref ptr, d.outIsConst, d.outTypenameToken, context, out t.attributes, iSAL);
			
			if (_TokIsIdent(_i)) t.name = _TokToString(_i++);
			else if (!isMember && _TokIsChar(_i, ",)[=")) t.name = "param" + iParam;
			else _Err(_i, "no name");
		}
		
		if (_TokIsChar(_i, '[')) {
			_ConvertCArray(ref t, !isMember);
		}
		
		//escape names that are C# keywords
		if (_csKeywords.Contains(t.name)) {
			//print.it(t.name);
			t.name = "@" + t.name;
		}
	}
	
	struct _FINDTYPEDATA {
		public _Symbol outSym;
		public bool outIsConst;
		public bool outIsAnonymousTypeDefinition;
		public bool outIsNestedTypeDefinition;
		public int outTypenameToken;
		public int inTypedefNameToken;
	}
	
	/// <summary>
	/// Finds typename. Gets its _Symbol etc.
	/// Starts searching from _i. Finally _i will be typename token index.
	/// Error if not found.
	/// Initially sets all outX=0/false/null.
	/// If there is 'const', sets outIsConst=true.
	/// If there is 'struct' etc as nested definition, converts the definition, sets outIsNestedStruct=true; then _i finally will be after }, which can be semicolon, variable name or * variable name.
	/// If there is 'struct' etc as forward declaration, adds forward declaration.
	/// </summary>
	void _FindTypename(bool isMember, ref _FINDTYPEDATA d) {
		d.outSym = null;
		d.outIsConst = false;
		d.outIsNestedTypeDefinition = false;
		d.outTypenameToken = 0;
		
		//process keywords until typename
		for (; _TokIsIdent(_i); _i++) {
			d.outSym = _FindSymbol(_i, true);
			var k = d.outSym as _Keyword;
			if (k == null) {
				d.outTypenameToken = _i;
				return;
			}
			
			switch (k.kwType) {
			case _KeywordT.TypeDecl:
				//is nested struct/enum/typedef definition?
				if (isMember) {
					bool isDef = false;
					if (_TokIsChar(_i, 't')) isDef = true;
					else {
						int j = _i + 1; if (_TokIsIdent(j)) j++;
						if (_TokIsChar(j, "{:;")) isDef = true;
					}
					if (isDef) {
						_DeclareType(ref d);
						d.outIsNestedTypeDefinition = true;
						return;
					}
				}
				
				//inline forward declaration
				d.outSym = _InlineForwardDeclaration();
				d.outTypenameToken = _i;
				return;
			case _KeywordT.Normal:
				if (_TokIs(_i, "const")) { d.outIsConst = true; continue; }
				break;
			}
			
			_Err(_i, "unexpected");
		}
		_Err(_i, "no type");
	}
	
	enum _TypeContext {
		Parameter, DelegateParameter, Return, Member, ComParameter, ComReturn
	}
	
	/// <summary>
	/// Converts type name/pointer to C# type name.
	/// The return value is type name, possibly with * or []. If parameter, also can have ref/out/in and [In]/[Out].
	/// </summary>
	/// <param name="t">The type.</param>
	/// <param name="ptr">Pointer level. The function may adjust it depending on typedef pointer level etc.</param>
	/// <param name="isConst">Is const.</param>
	/// <param name="iTokTypename">Type name token index. Can be 0 if don't need to unalias etc; then just adds pointer/ref if need.</param>
	/// <param name="context">Depending on it char* will be converted to "string" etc.</param>
	/// <param name="attributes">Receives null or MarshalAs attribute.</param>
	/// <param name="iSAL">SAL token index, or 0 if no SAL.</param>
	string _ConvertTypeName(_Symbol t, ref int ptr, bool isConst, int iTokTypename, _TypeContext context, out string attributes, int iSAL = 0) {
		attributes = null;
		string name, marshalAs = null;
		
		bool isLangType = false, isParameter = false, isCOM = false, isBlittable = false, isRawPtr = false;
		switch (context) {
		case _TypeContext.Parameter: case _TypeContext.DelegateParameter: isParameter = true; break;
		case _TypeContext.ComParameter: isParameter = true; isCOM = true; break;
		case _TypeContext.ComReturn: isCOM = true; break;
		}
		
		bool _In_ = false, _Out_ = false, _Inout_ = false;
		if (iSAL > 0) {
			//print.it(_tok[iSAL]);
			if (_TokStarts(iSAL, "_In_")) _In_ = true;
			else if (_TokStarts(iSAL, "_Out")) _Out_ = true;
			else if (_TokStarts(iSAL, "_Inout_")) _Inout_ = true;
			else _Err(iSAL, "unknown SAL");
		}
		
		if (iTokTypename != 0) {
			ptr += _Unalias(iTokTypename, ref t, ref isConst);
			name = t.csTypename;
			
			var c = t as _CppType;
			if (c != null) {
				isLangType = true;
				isBlittable = true;
				switch (name) {
				case "char":
					isBlittable = false;
					if (ptr is 1 or 2) {
						bool isBSTR = _TokIs(iTokTypename, "BSTR");
						if (ptr == 1 || (isParameter && isBSTR)) {
							//why '&& isBSTR':
							//	If non-BSTR char** parameter, cannot be 'out string' because .NET does not know how to free the string; often it is even undocumented.
							//	And ref in many cases is bad.
							
							ptr--;
							
							switch (context) {
							case _TypeContext.Member:
								if (isConst || isBSTR) {
									name = "string";
									if (isBSTR) marshalAs = "BStr";
								} else {
									//string dangerous, because if the callee changes member pointer, .NET tries to free the new string with CoTaskMemFree.
									ptr = 1;
									//print.it(_DebugGetLine(iTokTypename));
									isRawPtr = true;
								}
								break;
							case _TypeContext.Return:
							case _TypeContext.ComReturn:
								ptr = 1; //if string, .NET tries to free the return value
								isRawPtr = true;
								break;
							case _TypeContext.DelegateParameter:
								ptr = 1; //because some are "LPSTR or WORD". Rarely used.
								isRawPtr = true;
								break;
							case _TypeContext.Parameter:
							case _TypeContext.ComParameter:
								Debug.Assert(!(isConst && (_Out_ || _Inout_)));
								if (isConst || _In_ || isBSTR) {
									name = "string";
									//if (ptr == 1) print.it(_DebugGetLine(iTokTypename));
									if (isCOM) {
										if (!isBSTR) marshalAs = "LPWStr";
									} else {
										if (isBSTR) marshalAs = "BStr";
									}
								} else {
									ptr = 1;
									isRawPtr = true;
									//Could be StringBuilder, but Au library does not use it, it is slow. Or [Out] char[]. But Au library uses char*.
								}
								break;
							}
						}
					}
					if (ptr > 1) {
						isRawPtr = true;
						isConst = false;
					}
					break;
				case "int":
					if (_TokIs(iTokTypename, "BOOL")) {
						if (ptr == 0 || (ptr == 1 && isParameter)) {
							name = "bool";
							if (isCOM) marshalAs = "Bool";
							isBlittable = false;
						}
					}
					break;
				case "bool":
					marshalAs = "U1";
					isBlittable = false;
					break;
				case "void":
				case "byte":
				case "sbyte":
					if (ptr > 0) isRawPtr = true;
					break;
				case "double":
					if (_TokIs(iTokTypename, "DATE")) {
						if (ptr == 0 || (ptr == 1 && isParameter)) {
							name = "DateTime";
							isBlittable = false;
						}
					}
					break;
				}
				if (isRawPtr) isBlittable = true;
			}
		} else {
			name = t.csTypename;
		}
		
		if (!isLangType) {
			var ts = t as _Struct;
			if (ts != null) {
				if (ts.isInterface) {
					//print.it(ptr, ts.csTypename);
					if (ptr == 0) _Err(iTokTypename, "unexpected");
					if (--ptr > 0 && context == _TypeContext.Member) {
						name = "IntPtr";
						isBlittable = true;
					} else if (name is "IUnknown" or "IDispatch") {
						Debug_.PrintIf(ptr > 1, context);
						marshalAs = name;
						name = "object";
					}
				} else if (!ts.isClass) {
					switch (name) {
					case "wnd": case "LPARAM": isBlittable = true; break;
					case "GUID": name = "Guid"; break;
					case "DECIMAL": name = "decimal"; break;
					case "VARIANT":
						if ((isParameter && ptr <= 1) || (context == _TypeContext.Member && ptr == 0)) {
							name = "object";
							if (context != _TypeContext.ComParameter) marshalAs = "Struct";
						}
						break;
					//case "SAFEARRAY": //in SDK used only with SafeArrayX functions, with several other not-important functions and as [PROP]VARIANT members
					//print.it(ptr);
					//break;
					case "ITEMIDLIST":
						if (ptr > 0) { ptr--; name = "IntPtr"; isBlittable = true; }
						//else _Err(iTokTypename, "example"); //0 in SDK
						break;
					case "POINTL": name = "POINT"; break;
					case "RECTL": name = "RECT"; break;
					}
				}
			} else if (t is _Enum) {
				isBlittable = true;
			}
		}
		
		if (ptr > 0) {
			__ctn.Clear();
			bool isArray = false;
			if (!isRawPtr && isParameter) {
				string sal = null;
				if (iSAL > 0) sal = _TokToString(iSAL);
				
				if (iSAL > 0 && ptr == 1) { //if ptr>1, can be TYPE[]* or TYPE*[]
					if (_In_ && (sal.Contains("_reads_") || sal.Contains("count"))) {
						//print.it(_tok[iTokTypename], name, _DebugGetLine(iTokTypename));
						isArray = true;
						__ctn.Append("[In] ");
					}
					if (_Inout_ && (sal.Contains("_updates_") || sal.Contains("count"))) {
						//print.it(_tok[iTokTypename], name, _DebugGetLine(iTokTypename));
						isArray = true;
						__ctn.Append("[In,Out] ");
					}
					if (_Out_ && (sal.Contains("_writes_") || sal.Contains("count"))) {
						//print.it(_tok[iTokTypename], name, _DebugGetLine(iTokTypename));
						isArray = true;
						__ctn.Append("[Out] ");
					}
				}
				
				if (_Inout_) isConst = false;
				
				if (isArray) {
					ptr--;
				} else {
					ptr--;
					
					if (_Out_) {
						__ctn.Append("out ");
					} else if (_In_ || isConst) {
						if (isBlittable) {
							if (isConst && ptr == 0 && name != "IntPtr") {
								//print.it(_tok[iTokTypename], name, _DebugGetLine(iTokTypename));
								isArray = true; //usually array, because there is no sense for eg 'const int* param', unless it is a 64-bit value (SDK usually then uses LARGE_INTEGER etc, not __int64). Those with just _In_ usually are not arrays, because for arrays are used _In_reads_ etc.
							} else {
								__ctn.Append("in ");
								//print.it(_tok[iTokTypename], name, _DebugGetLine(iTokTypename));
							}
						} else {
							if (isConst) {
								__ctn.Append("in "); //prevents copying non-blittable types back to the caller when don't need
							} else {
								//print.it(_tok[iTokTypename], name, _DebugGetLine(iTokTypename));
								//__ctn.Append("in "); //no, because there are many errors in SDK where inout parameters have _In_
								__ctn.Append("ref ");
							}
						}
					} else {
						__ctn.Append("ref ");
					}
				}
				
				if (isArray) {
					bool useMarshalAsSubtype = false;
					if (isCOM) {
						//by default marshals as SAFEARRAY
						if (marshalAs == null) marshalAs = "LPArray";
						else useMarshalAsSubtype = true;
					} else if (context == _TypeContext.DelegateParameter) {
						isArray = false;
						ptr++;
						__ctn.Clear();
						//maybe can be array, not tested. Never mind, in SDK only 4 rarely used.
						//print.it(_tok[iTokTypename], name, _DebugGetLine(iTokTypename));
					} else if (marshalAs != null) useMarshalAsSubtype = true;
					
					if (useMarshalAsSubtype) marshalAs = "LPArray, ArraySubType = UnmanagedType." + marshalAs;
				}
			} //if(isParameter)
			
			__ctn.Append(name);
			if (ptr > 0) __ctn.Append('*', ptr);
			if (isArray) __ctn.Append("[]");
			
			name = __ctn.ToString();
		}
		
		if (marshalAs != null) {
			__ctn.Clear();
			string ret = null; if (context == _TypeContext.Return || context == _TypeContext.ComReturn) ret = "return: ";
			attributes = $"[{ret}MarshalAs(UnmanagedType.{marshalAs})]";
		}
		return name;
	}
	StringBuilder __ctn = new StringBuilder();
	
	/// <summary>
	/// Converts C-style fixed-size array to C#. Gets MarshalAs attribute, appends "[]" to typeName etc.
	/// Supports "[y][x]" etc.
	/// _i must be at '['. Finally it will be after ']'.
	/// If empty array (like TYPE x[] or TYPE x[0]), attributes receives "//...". If 1-element array, receives "/*...*/".
	/// </summary>
	/// <param name="t.typeName">This function appends "[]" if need, or convertes "char" to "string".</param>
	/// <param name="t.name">If less than 8 elements, and t.name looks like a "Reserved" etc, splits into multiple members like "Reserved_0, Reserved_1, ...".</param>
	/// <param name="t.attributes">If !isParameter, receives attribute like "[MarshalAs(UnmanagedType.ByValArray, SizeConst = {x})]". If already is MarshalAs attribute, uses it as ArraySubType.</param>
	/// <param name="isParameter">Just skip [..] or [...][...] and append "[]" to t.typeName. Used for parameters, because the attribute can be used only with struct fields; C++ arguments for parameters 'TYPE param[n]' are passed like 'TYPE* param', and n is ignored, can be empty.</param>
	void _ConvertCArray(ref _PARAMDATA t, bool isParameter = false) {
		//if (!isParameter) print.it(t.typeName);
		
		uint elemCount = 1;
		for (; _TokIsChar(_i, '['); _i++) { //support [a][b]
			int i0 = _i + 1;
			_SkipEnclosed();
			if (isParameter) continue;
			uint ec = 0;
			if (_i > i0) { //else TYPE x[]
				_ExpressionResult r = _Expression(i0, _i, "[]");
				switch (r.typeS) { case "int": case "uint": break; default: _Err(i0, "cannot calculate"); break; }
				ec = r.valueI;
			}
			elemCount *= ec;
		}
		
		if (isParameter) {
			t.typeName += "[]";
			return;
		}
		
		if (t.typeName.Ends("[]")) t.typeName = t.typeName.Remove(t.typeName.Length - 2); //'TYPE a[n], b[m]'
		
		string marshalAs = "ByValArray", comment = null, comment2 = null;
		if (elemCount == 0) { //variable-length array of >= 0 elements
			comment = "//"; //disable the attribute together with member, and let the member be not array
		} else if (elemCount == 1) { //variable-length array of >= 1 elements
			comment = "/*"; comment2 = "*/"; //disable the attribute, and let the member be not array
		} else if (t.typeName == "char") {
			t.comment = $"//public fixed char {t.name}[{elemCount}];";
			t.typeName = "string";
			marshalAs = "ByValTStr";
		} else {
			if (elemCount < 8 && (t.name.Find("Reserved", true) >= 0 || t.name.Find("pad", true) >= 0 || t.name.Starts("Spare", true))) {
				//print.it(t.name);
				var sb = new StringBuilder();
				for (int i = 0; i < elemCount; i++) {
					if (i > 0) sb.Append(", ");
					sb.Append(t.name);
					sb.Append('_');
					sb.Append(i);
				}
				t.name = sb.ToString();
				comment = "/*"; comment2 = "*/";
			} else {
				//if(elemCount<8) print.it(t.name);
				if (t.typeName is "byte" or "sbyte" or "int" or "uint" or "long" or "ulong" or "short" or "ushort" or "float" or "double")
					t.comment = $"//public fixed {t.typeName} {t.name}[{elemCount}];";
				t.typeName += "[]";
			}
		}
		
		if (t.attributes != null) _Err(_i, "todo"); //0 in SDK. Should extract its type from [MarshalAs(UnmanagedType.(\w+) and insert in the new attributes.
		t.attributes = $"{comment}[MarshalAs(UnmanagedType.{marshalAs}, SizeConst = {elemCount})]{comment2}";
			//CONSIDER: also add commented properties to get Span. Then also can add properties to get/set managed string or array from the Span.
		//	The MarshalAs way often cannot be used (makes the type managed and then cannot get pointer) or undesirable. The fixed way can be used only with some types.
		//	Tested, in Release almost same speed as raw pointer. If used in script that isn't optimized, much slower, but not too slow.
		//	Examples:
		/*
[Winapi]
static unsafe class api {

internal struct Example {
		//string
		fixed char _c[5];
		public Span<char> s => MemoryMarshal.CreateSpan(ref _c[0], 5);
		public string s_String { get => _ToString(s); set => _ToSpan(s, value); }
		
		//array of fixable type
		fixed byte _b[5];
		public Span<byte> b => MemoryMarshal.CreateSpan(ref _b[0], 5);
		public byte[] b_Array { get => b.ToArray(); set => value.AsSpan().CopyTo(b); }
		
		//array of other type
		POINT _a, _a1, _a2, _a3, _a4;
		//POINT _a; fixed byte _ab[32]; //or this, if can calculate size easily
		public Span<POINT> a => MemoryMarshal.CreateSpan(ref _a, 5);
		public POINT[] a_Array { get => a.ToArray(); set => value.AsSpan().CopyTo(a); }
		
		//array of size 1 or 0 at the end
		POINT _p;
		public Span<POINT> p(int count) => MemoryMarshal.CreateSpan(ref _p, count);
	}
	
	static string _ToString(Span<char> p) => new(p[..p.IndexOf('\0')]);
	
	static void _ToSpan(Span<char> p, RStr s) {
		if (s.Length >= p.Length) throw new ArgumentException($"Destination is too short. Max string length = {p.Length - 1}.");
		p[s.Length] = '\0';
		s.CopyTo(p);
	}
}
			*/
		}

		/// <summary>
		/// Converts C++ calling convention keyword to C# enum CallingConvention member name.
		/// Returns null if __stdcall.
		/// </summary>
		/// <param name="iTok">C++ calling convention keyword token. Error if other token.</param>
		string _ConvertCallConv(int iTok) {
	_FindKeyword(iTok, _KeywordT.CallConv);
	
	char* s = T(iTok);
	while (*s == '_') s++;
	switch (*s) {
	case 'c': return "Cdecl";
	case 'f': return "FastCall";
	case 't': return "ThisCall";
	}
	return null; //StdCall is default
}
	}
