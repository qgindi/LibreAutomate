using dnlib.DotNet;

partial class WinApiConverter {
	void _AddToDict(string name) {
		_nFiltered++;
		var v = b.ToString();
		if (!Result.TryAdd(name, v)) {
			Result[name] = Result[name] + "\r\n//or\r\n" + v;
			//print.it("<><lc green><>"); print.it(Result[name]);
		}
		
		#region debug
		
		//if (v.Contains(" struct ")) {
		//	//if (v.Contains("_bitfield")) print.it(v);
		//} else {
		//	//if (v.Contains(" enum ")) print.it(v);
		
		//}
		
		#endregion
	}
	
	void _AddSomeTypes() {
		_typedefs.Add("Guid", "Guid"); //defined not in .winmd
		Result["PROPERTYKEY"] = """internal record struct PROPERTYKEY(Guid fmtid, uint pid);"""; //need ctor
		Result["DEVPROPKEY"] = """internal record struct DEVPROPKEY(Guid fmtid, uint pid);"""; //need ctor
		Result["SID_IDENTIFIER_AUTHORITY"] = """internal record struct SID_IDENTIFIER_AUTHORITY(byte v0, byte v1, byte v2, byte v3, byte v4, byte v5);"""; //need ctor
	}
	
	(string type, bool isManaged, ArraySig arraySig) _GetTypeName_Field(FieldDef fd, in _FieldAttributes attr, _TypeDefEtc tdeTopLevel) {
		string fieldTypeWithoutBrackets = fd.FieldType.TypeName;
		bool isArray = fieldTypeWithoutBrackets.Ends("[*]"); if (isArray) fieldTypeWithoutBrackets = fieldTypeWithoutBrackets[..^3];
		Debug.Assert(!(isArray && attr.isConst));
		bool isNestedType = fd.DeclaringType.HasNestedTypes && fd.DeclaringType.NestedTypes.Any(o => o.Name == fieldTypeWithoutBrackets);
		
		var (s, marshal, sNoMarshal) = _GetTypeName(fd.FieldType, out int ptr, false, (attr.isConst, true), tdeTopLevel.attr.bit32, fd, fieldTypeWithoutBrackets, tdeTopLevel);
		
		if (marshal != null) {
			//if (ptr > 0 || attr.isFlexibleArray || _InUnion()) (marshal, s) = (null, sNoMarshal); //no, all nested non-union structs (few, rare) are or should be blittable
			if (ptr > 0 || attr.isFlexibleArray || fd.DeclaringType.IsExplicitLayout || fd.DeclaringType.IsNested) (marshal, s) = (null, sNoMarshal);
			
			if (!marshal.NE()) b.AppendFormat("[MarshalAs(UnmanagedType.{0})] ", marshal);
		}
		
		if (ptr > 0) s = s.PadRight(s.Length + ptr, '*');
		
		return (s, marshal != null, isArray ? (ArraySig)fd.FieldSig.Type : null);
		
		void _PI(params object[] more) {
			print.it(fd.DebugToString(), fd.FieldType.TypeName, s, marshal);
			if (more.Length > 0) print.it($"\t{string.Join(", ", more)}");
		}
		
		//bool _InUnion() {
		//	for (var td = fd.DeclaringType; td != null && td.IsNested; td = td.DeclaringType) {
		//		if (td.IsExplicitLayout) return true;
		//	}
		//	return false;
		//}
	}
	
	string _GetTypeName_Return(MethodDef md, bool inCOM, int bStart, bool bit32) {
		var (s, marshal, sNoMarshal) = _GetTypeName(md.ReturnType, out int ptr, inCOM, (false, inCOM), bit32, md);
		
		if (marshal != null) {
			if (ptr > 0) (marshal, s) = (null, sNoMarshal);
			//if (!(s is "bool")) _PI();
		}
		
		if (!marshal.NE()) b.Insert(bStart, $"[return: MarshalAs(UnmanagedType.{marshal})]\r\n{(inCOM ? "\t" : null)}");
		
		if (ptr > 0) s = s.PadRight(s.Length + ptr, '*');
		
		return s;
		
		void _PI() {
			print.it(md.DebugToString(), md.ReturnType.TypeName, s, marshal);
		}
	}
	
	string _GetTypeName_Param(Parameter p, in _ParamAttributes attr, bool inCOM, bool inDelegate, bool bit32) {
		ParamDef pd = p.ParamDef;
		
		bool isOut = pd.IsOut, isIn = pd.IsIn;
		bool isConst = attr.isConst && !isOut;
		bool allowString = !inDelegate && !isOut && (isConst || !(attr.isArrayPtr || attr.hasMemorySize));
		
		var (s, marshal, sNoMarshal) = _GetTypeName(p.Type, out int ptr, inCOM, (allowString, true), bit32, pd);
		
		//if (isOut && attr.isConst) _PI();
		//if (isConst && ptr > 0) _PI();
		//if (attr.isComOut) _PI();
		//if (attr.isReturn) _PI();
		
		if (attr.isReserved) {
			if (ptr > 0 || (marshal != null && sNoMarshal is "nint" or [.., '*'])) { ptr = 0; s = "nint"; marshal = null; }
			//else if (marshal != null) _PI();
		}
		
		//if (ptr > 0 && !attr.isArrayPtr && !inDelegate && attr.hasMemorySize) {
		//	_PI();
		//}
		
		bool arr = attr.isArrayPtr && ptr > 0 && !inDelegate;
		if (arr) {
			Debug.Assert(ptr > 0);
			if (ptr > 1 && (marshal != null || isOut)) {
				//Let users edit it if need. We can't safely make eg `ref object[]`.
				//Eg we don't know whether it is a pointer to an array or array of pointers, and how to free callee-allocated array memory, etc.
				//_PI(); //not too many
				s = sNoMarshal;
				arr = false;
			} else {
				if (!marshal.NE()) b.AppendFormat("[MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.{0})] ", marshal);
				else if (inCOM) b.Append("[MarshalAs(UnmanagedType.LPArray)] ");
				
				ptr--;
			}
			
			if (isOut) b.Append(isIn ? "[In, Out] " : "[Out] "); //for arrays it's recommended to specify these attributes even if it won't change anything; default is [In], although blittable type arrays are pinned and not marshalled.
		} else {
			bool isComOut = ptr == 1 && s == "void*" && !attr.isArrayPtr && !attr.hasMemorySize && (attr.isComOut || (p.MethodSigIndex > 0 && p.Method.Parameters[p.Index - 1].Type.TypeName == "Guid*")); //if the comout attribute is missing, assume it's comout if this param is void** and previous param is Guid*
			
			bool roi = ptr > 0 && !attr.isArrayPtr && !attr.hasMemorySize && !(pd.Attributes.Has(ParamAttributes.Optional) && marshal == null && !isComOut);
			if (roi && ptr == 1 && s is "byte" or "sbyte" or "char" && !pd.DeclaringMethod.Name.String.Like("Var*From*")) roi = false; //rarely pointer to a single value. Tested: ushort and short usually pointer to single value.
			if (roi) ptr--;
			
			if (!marshal.NE()) {
				if (ptr > 0) (s, ptr) = ("nint", 0); //can't marshal if pointer (few, rare)
				else b.AppendFormat("[MarshalAs(UnmanagedType.{0})] ", marshal);
			} else if (isComOut) {
				Debug.Assert(roi && ptr == 0);
				(s, ptr) = ("object", 0);
				b.Append("[MarshalAs(UnmanagedType.IUnknown)] ");
			}
			
			if (roi) {
				b.Append(!isOut ? "in " : isIn ? "ref " : "out ");
			}
		}
		
		if (ptr > 0) s = s.PadRight(s.Length + ptr, '*');
		if (arr) s += "[]";
		
		return s;
		
		void _PI() {
			print.it(pd.DebugToString(), p.Type.TypeName, s, marshal, pd.Attributes);
		}
	}
	
	(string type, string marshal, string sNoMarshal) _GetTypeName(TypeSig t, out int ptr, bool com, (bool @string, bool bstr) allow, bool bit32, object def, string fieldTypeWithoutBrackets = null, _TypeDefEtc tdeTopLevel = null) {
		string s = fieldTypeWithoutBrackets ?? t.TypeName;
		string s0 = s;
		ptr = s.Length; s = s.TrimEnd('*'); ptr -= s.Length;
		
		var fieldStruct = (def as FieldDef)?.DeclaringType;
		bool field = fieldStruct != null;
		
		if (field && fieldStruct.HasNestedTypes && fieldStruct.NestedTypes.Any(o => o.Name == s)) {
			//the field type is a nested type in the same struct
		} else if (_typedefs.TryGetValue(s, out var k)) {
			s = k;
			if (s[^1] == '*' && allow.@string && ptr == 0) {
				if (s == "char*") return ("string", com ? "LPWStr" : "", s);
				Debug.Assert(s0 == "PSTR");
				return ("string", "LPStr", s);
				
				//note: with some (few) functions must be `char*`, not `string`. Eg FreeEnvironmentStringsW. Users will figure out it, because `string` has no sense there.
			}
		} else {
			var s1 = s;
			s = _SystemTypeNameToKeyword(s);
			if (s != s1) {
				if (s == "bool") return (s, "U1", "byte"); //rare
				if (ptr > 0 && s == "void") { ptr--; s = "void*"; }
			} else {
				switch (s) {
				case "BOOL": return field || com ? (s, null, s) : ("bool", "", "BOOL");
				case "BSTR":
					if (!allow.bstr || (!com && def is ParamDef pd && pd.DeclaringMethod.Name.String is string smn && (smn.Starts("Sys") || smn.Starts("BSTR_")))) return ("char*", null, "char*");
					return ("string", com ? "" : "BStr", "char*");
				case "VARIANT": return ("object", field ? "Struct" : "", s);
				case "IUnknown": return ("object", field ? "" : s, "nint");
				case "IDispatch": return ("object", s, "nint");
				case "DECIMAL": return ("decimal", "", s);
				case "CY": return ("decimal", "Currency", s);
				case "VARIANT_BOOL": return ("bool", "VariantBool", "short");
				case "ITEMIDLIST" when ptr > 0: ptr--; return ("nint", null, "nint");
				}
				
				var nn = t.Namespace + "." + s;
				
				if (bit32 && _types.TryGetValue(nn + "__32", out var v32)) { //if in 32-bit context, prefer 32-bit type
					s += "__32";
					nn += "__32";
				}
				
				if (!_types.TryGetValue(nn, out var v)) { //some non-Ansi types have a field of Ansi type. In few cases it's a metadata bug (can't fix).
					if (_ansiTypes.Remove(nn, out v)) {
						_types.Add(nn, v);
						_Type(v, true);
					} //else _PI(); //few, missing in metadata
				}
				
				if (v != null) {
					if (!v.td.IsValueType) return (s, "", "nint");
					
					if (ptr == 0 && !v.isProcessed) { //need v.isManagedStruct
						_Type(v, true);
					}
					
					if (v.isManagedStruct) return (s, "", s);
				}
			}
		}
		
		return (s, null, s);
		
		void _PI() {
			print.it(def.DebugToString(), s, s0);
		}
	}
	
	static string _SystemTypeNameToKeyword(string s) {
		return s switch {
			"Int32" => "int",
			"UInt32" => "uint",
			"SByte" => "sbyte",
			"Byte" => "byte",
			"Int16" => "short",
			"UInt16" => "ushort",
			"Int64" => "long",
			"UInt64" => "ulong",
			"IntPtr" => "nint",
			"UIntPtr" => "nuint",
			"Double" => "double",
			"Single" => "float",
			"Char" => "char",
			"Boolean" => "bool",
			"String" => "string", //not used in metadata, but our code may use
			//"Decimal" => "decimal", //not used in metadata
			"Void" => "void",
			_ => s
		};
	}
	
	static (bool isUlong, long l, ulong u) _ObjectToLongOrUlong(object o) {
		return o switch {
			int v => (false, v, 0),
			long v => (false, v, 0),
			short v => (false, v, 0),
			sbyte v => (false, v, 0),
			uint v => (true, 0, v),
			ulong v => (true, 0, v),
			ushort v => (true, 0, v),
			byte v => (true, 0, v),
			_ => throw new Exception("bad object type")
		};
	}
	
	string _RenameIfKeyword(string s) {
		if (!_keywordsReserved.Contains(s)) return s;
		return "@" + s;
	}
	HashSet<string> _keywordsReserved = [
		"abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
		"continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
		"extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in",
		"int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator",
		"out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte",
		"sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
		"true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
		"volatile", "while"
	];
	
	void _AppendString(string s) {
		b.Append('"').Append(s.Escape()).Append('"');
	}
	
	//StringBuilder _bPrint = new();
	//void _Print(int level, string name, string color) {
	//	_bPrint.Clear();
	//	_bPrint.Append("<><c ").Append(color).Append("><\a>");
	//	_bPrint.Append(' ', level).Append(name);
	//	_bPrint.Append("</\a><>");
	//	print.it(_bPrint);
	//}
}

static class Ext {
	public static void Start(this StringBuilder t, int level) {
		if (level == 0) t.Clear();
		else t.AppendLine().Append('\t', level);
	}
	
	public static void EndAttributes(this StringBuilder t, int level, int lenBefore) {
		if (t.Length > lenBefore) t.AppendLine().Append('\t', level);
	}
	
	public static string ValueString(this Constant t) {
		var o = t.Value;
		return o as string ?? throw new Exception("not string");
	}
	
	public static string ArgString(this CustomAttribute t, int index) {
		var o = t.ConstructorArguments[index].Value;
		return o as UTF8String ?? throw new Exception("not UTF8String");
	}
	
	public static int ArgInt(this CustomAttribute t, int index) {
		var o = t.ConstructorArguments[index].Value;
		return Convert.ToInt32(o);
	}
	
	public static string ArgGuid(this CustomAttribute t) {
		var a = t.ConstructorArguments;
		var guid = new Guid((uint)a[0].Value, (ushort)a[1].Value, (ushort)a[2].Value, (byte)a[3].Value, (byte)a[4].Value, (byte)a[5].Value, (byte)a[6].Value, (byte)a[7].Value, (byte)a[8].Value, (byte)a[9].Value, (byte)a[10].Value);
		return guid.ToString();
	}
	
	public static string ToKeywordString(this ElementType t) {
		return t switch {
			ElementType.I4 => "int",
			ElementType.U4 => "uint",
			ElementType.I1 => "sbyte",
			ElementType.U1 => "byte",
			ElementType.I2 => "short",
			ElementType.U2 => "ushort",
			ElementType.I8 => "long",
			ElementType.U8 => "ulong",
			ElementType.I => "nint",
			ElementType.U => "nuint",
			ElementType.R8 => "double",
			ElementType.R4 => "float",
			ElementType.String => "string",
			_ => throw new Exception(t.ToString())
		};
	}
	
	public static string DebugToString(this object o) {
		switch (o) {
		case string k: return k;
		case ParamDef k: return $"{_Meth(k.DeclaringMethod)}({k.Name})";
		case MethodDef k: return _Meth(k);
		case TypeDef k: return _Typ(k);
		case FieldDef k: return _Typ(k.DeclaringType) + "." + k.Name;
		}
		throw null;
		
		static string _Meth(MethodDef m) {
			if (m.IsStatic) return m.Name;
			return _Typ(m.DeclaringType) + "." + m.Name;
		}
		
		static string _Typ(TypeDef t) {
			string s = t.Name;
			while ((t = t.DeclaringType) != null) s = t.Name + "." + s;
			return s;
		}
	}
}

//of types (struct, delegate, interface, not enum)
struct _TypeAttributes {
	public bool skip, obsolete, ansi, unicode, bit32, cdeclDelegate, typedef;
	public string guid;
	
	public _TypeAttributes(TypeDef td) {
		foreach (var ca in td.CustomAttributes) {
			string name = ca.AttributeType.Name;
			switch (name.AsSpan(..^9)) {
			case "Obsolete": obsolete = true; break;
			case "Ansi": ansi = true; break;
			case "Unicode": unicode = true; break;
			case "SupportedArchitecture":
				int arch = ca.ArgInt(0);
				if ((arch & 2) == 0) { //no x64
					if ((arch & 1) != 0) bit32 = true; //X86
					else { skip = true; return; } //Arm64
				}
				break;
			case "UnmanagedFunctionPointer":
				if (ca.ArgInt(0) is int cc && cc != 1) {
					if (cc == 2) cdeclDelegate = true;
					else { print.it(cc); skip = true; return; }
				}
				break;
			case "NativeTypedef" or "MetadataTypedef":
				typedef = true;
				break;
			case "Guid":
				guid = ca.ArgGuid();
				break;
			case "StructSizeField":
				//don't use, because many structs don't have this attribute (eg STARTUPINFOEXW). 
				//	Users would assume all struct ctors auto-init the field, and this way make bugs.
				//print.it(ca.ArgString(0), itmd.DebugToString()); //all cbSize
				break;
			case "Documentation"
			or "SupportedOSPlatform"
			or "RAIIFree"
			or "InvalidHandleValue"
			or "Agile"
			or "AlsoUsableFor"
			:
				break;
			default:
				print.it(name[..^9], td.DebugToString());
				break;
			}
		}
	}
}

//of extern methods
struct _MethodAttributes {
	public bool skip, obsolete, unicode, bit32, inline;
	public int constant;
	
	public _MethodAttributes(MethodDef md) {
		foreach (var ca in md.CustomAttributes) {
			string name = ca.AttributeType.Name;
			switch (name.AsSpan(..^9)) {
			case "Obsolete": obsolete = true; break;
			case "Ansi": skip = true; return;
			case "Unicode": unicode = true; break;
			case "SupportedArchitecture":
				int arch = ca.ArgInt(0);
				if ((arch & 2) == 0) { //no x64
					if ((arch & 1) != 0) bit32 = true; //X86
					else { skip = true; return; } //Arm64
				}
				break;
			case "Constant":
				(inline, constant) = (true, ca.ArgString(0).ToInt());
				break;
			case "Documentation"
			or "SupportedOSPlatform"
			or "CanReturnMultipleSuccessValues"
			or "CanReturnErrorsAsSuccess"
			or "DoesNotReturn"
			:
				break;
			default:
				print.it(name[..^9], md.DebugToString());
				break;
			}
		}
	}
}

struct _FieldAttributes {
	public bool isConst, isBitfield, isFlexibleArray;
	//public string assocEnum;
	
	public _FieldAttributes(FieldDef fd) {
		foreach (var ca in fd.CustomAttributes) {
			string name = ca.AttributeType.Name;
			switch (name.AsSpan(..^9)) {
			case "Const": isConst = true; break;
			case "NativeBitfield": isBitfield = true; break;
			case "FlexibleArray": isFlexibleArray = true; break;
			case "AssociatedEnum": /*assocEnum = ca.ArgString(0);*/ break; //not used. We convert most enums to const.
			case "Documentation": break; //few
			case "Obsolete" or "NotNullTerminated" or "NullNullTerminated": break;
			case "NativeArrayInfo": break; //few, not useful
			default:
				print.it(name[..^9], fd.DebugToString());
				break;
			}
		}
	}
}

struct _ParamAttributes {
	public bool isConst, isReturn, isComOut, isArrayPtr, isReserved, hasMemorySize;
	//public string assocEnum;
	
	public _ParamAttributes(ParamDef pd) {
		foreach (var ca in pd.CustomAttributes) {
			string name = ca.AttributeType.Name;
			switch (name.AsSpan(..^9)) {
			case "Const": isConst = true; break;
			case "RetVal": isReturn = true; break;
			case "ComOutPtr": isComOut = true; break;
			case "NativeArrayInfo": isArrayPtr = true; break;
			case "Reserved": isReserved = true; break;
			case "MemorySize": hasMemorySize = true; break; //can be array or normal struct or variable-size struct, we can't know, therefore use pointer, not in/ref/out
			case "AssociatedEnum": /*assocEnum = ca.ArgString(0);*/ break; //not used. We convert most enums to const.
			case "NullNullTerminated": break;
			case "NotNullTerminated": break;
			case "IgnoreIfReturn" or "DoNotRelease" or "Retained" or "FreeWith" or "RAIIFree": break;
			case "Documentation": break; //few
			default:
				print.it(name[..^9], pd.DebugToString());
				break;
			}
		}
	}
}

record class _TypeDefEtc(TypeDef td, string name, _TypeAttributes attr) {
	public bool isManagedStruct;
	public bool isProcessed;
}
