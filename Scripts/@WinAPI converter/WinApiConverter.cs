/*/ nuget -\dnlib; /*/
using dnlib.DotNet;
using System.Numerics;

partial class WinApiConverter {
	ModuleDefMD _module;
	StringBuilder b = new();
	Dictionary<string, string> _typedefs = new(500);
	Dictionary<string, _TypeDefEtc> _types = new(20_000);
	Dictionary<string, _TypeDefEtc> _ansiTypes = new(1000);
	Dictionary<string, (string members, bool isUnk, bool isDisp, string name)> _iBase = new(10_000);
	int _nAll = 0, _nFiltered = 0;
	
	public Dictionary<string, string> Result { get; private set; } = new(250_000);
	
	public void Convert(HashSet<string> onlyNamespaces = null) {
		_module = ModuleDefMD.Load(folders.Downloads + @"Windows.Win32.winmd");
		List<TypeDef> apis = new(50);
		_AddSomeTypes();
		
		foreach (var group in _module.Types.GroupBy(o => o.Namespace.String).Skip(1).OrderBy(o => o.Key)) { //group and order types by namespace
			string ns = group.Key, ns2 = ns[14..];
			if (ns2 is
				"Foundation.Metadata" //not Windows API. Attributes etc.
				or "NetworkManagement.P2P" //all API deprecated, but in metadata only some have [Obsolete], which causes some problems
				) continue; //CONSIDER: skip more namespaces.
			if (onlyNamespaces != null && !onlyNamespaces.Contains(ns2)) continue;
			//print.it(ns2, group.Count());
			
			foreach (var td in group) {
				if (td.IsEnum) {
					_Enum(td);
				} else if (td.IsValueType || td.IsInterface || td.IsDelegate) {
					_TypeAttributes attr = td.HasCustomAttributes ? new(td) : default;
					if (attr.skip) continue;
					//if (attr.obsolete) print.it(td.Name, td.IsValueType); //few, all ansi struct
					
					//if (attr.guid != null && !td.IsInterface && td.HasFields) print.it(td.IsValueType, td.Name);
					
					if (attr.typedef) {
						_Typedef(td);
					} else if (attr.guid != null && td.IsValueType && td.Fields.Count == 0) {
						_CoClass(td, attr);
					} else {
						string name = td.Name; //note: don't remove suffix "W", or then also would need to find and rename references or add to typedef etc
						
						string typedef = name switch {
							"POINT" or "SIZE" or "RECT" or "PROPERTYKEY" or "DEVPROPKEY" or "SID_IDENTIFIER_AUTHORITY" => name,
							"POINTL" or "RECTL" => name[..^1],
							"FARPROC" or "NEARPROC" or "PROC" => "nint",
							_ => null
						};
						if (typedef != null) { _typedefs.Add(name, typedef); continue; }
						
						if (!attr.ansi && name.Starts("PROPSHEETPAGEA_V") || name.Starts("PROPSHEETHEADERA_V")) attr.ansi = true;
						
						_TypeDefEtc tde = new(td, name, attr);
						var d = attr.ansi ? _ansiTypes : _types;
						if (!d.TryAdd(ns + "." + name, tde)) {
							Debug.Assert(attr.bit32); //good: all 32-bit types are after 64-bit types with same name.
							name += "__32";
							d.Add(ns + "." + name, tde with { name = name });
						}
					}
				} else {
					Debug.Assert(td.Name == "Apis");
					apis.Add(td);
				}
			}
		}
		
		foreach (var t in _types.Values.ToArray()) {
			if (t.isProcessed) continue;
			_Type(t, false);
		}
		
		foreach (var td in apis) {
			_Apis(td);
		}
		
		print.it($"//all={_nAll}, -ansi={_nFiltered}, -dup={Result.Count}");
	}
	
	//class "Apis"
	void _Apis(TypeDef td) {
		if (td.HasMethods) {
			foreach (var md in td.Methods) {
				_nAll++;
				_ExternMethod(md);
			}
		}
		
		if (td.HasFields) {
			foreach (var fd in td.Fields) {
				_nAll++;
				_ConstOrStaticReadonly(fd);
			}
		}
		
		Debug.Assert(!td.HasNestedTypes);
	}
	
	//top-level struct, delegate, interface, not enum
	void _Type(_TypeDefEtc t, bool recurse) {
		Debug.Assert(!t.isProcessed);
		if (recurse) { _bStack.Push(b); b = new(); } //not many
		
		_nAll++;
		b.Start(0);
		var (td, name, attr) = t;
		
		if (t.td.IsDelegate) {
			_Delegate(td, name, attr);
		} else if (td.IsInterface) {
			_Interface(td, name, attr);
		} else { //struct
			if (attr.guid != null) b.AppendLine($"[Guid(\"{attr.guid}\")]"); //few
			_Struct(0, td, t, name);
			_AddToDict(name);
		}
		
		t.isProcessed = true;
		if (recurse) b = _bStack.Pop();
	}
	Stack<StringBuilder> _bStack = new();
	
	void _Struct(int level, TypeDef td, _TypeDefEtc tdeTopLevel, string name = null) {
		name ??= td.Name;
		
		int len = b.Length;
		if (td.Layout == TypeAttributes.ExplicitLayout || td.HasClassLayout) {
			b.Append("[StructLayout(LayoutKind.").Append(td.Layout == TypeAttributes.ExplicitLayout ? "Explicit" : "Sequential");
			if (td.HasClassLayout && td.ClassLayout is var l) {
				if (l.ClassSize > 0) b.Append(", Size = ").Append(l.ClassSize);
				if (l.PackingSize > 0) b.Append(", Pack = ").Append(l.PackingSize);
			}
			b.Append(")]");
		}
		b.EndAttributes(level, len);
		
		b.AppendFormat("{0} struct {1} {{", level > 0 ? "public" : "internal", name);
		
		Debug.Assert(!td.HasMethods);
		
		if (td.HasFields) {
			for (int i = 0, last = td.Fields.Count - 1; i <= last; i++) {
				_Field(level + 1, td.Fields[i], i == last, tdeTopLevel);
			}
		}
		
		if (td.HasNestedTypes) {
			foreach (var ntd in td.NestedTypes) {
				b.AppendLine();
				b.Start(level + 1);
				//if (ntd.HasCustomAttributes) {
				//	print.it(name, ntd.CustomAttributes); //several StructSizeFieldAttribute
				//}
				_Struct(level + 1, ntd, tdeTopLevel);
			}
		}
		
		b.AppendLine().Append('\t', level).Append('}');
	}
	
	void _Field(int level, FieldDef fd, bool last, _TypeDefEtc tdeTopLevel) {
		b.Start(level);
		
		string name = _RenameIfKeyword(fd.Name);
		if (fd.HasConstant) {
			b.Append("public ");
			_Const(fd, name);
			b.Append(';');
		} else {
			_FieldAttributes attr = fd.HasCustomAttributes ? new(fd) : default;
			
			//Not all flexible arrays have the attribute. It seems `field[1]` have but `field[]` don't.
			//	We can either comment out the field or use FlexibleArray. Let's use FlexibleArray to make it easy to use, although sizeof will be incorrect.
			if (last && fd.FieldType.ElementType == ElementType.Array && !attr.isFlexibleArray && fd.FieldSig.Type is ArraySig { Sizes: [1] }) {
				//print.it(fd.DebugToString());
				attr.isFlexibleArray = true;
				b.Append("\r\n#warning This field makes sizeof struct incorrect, because in C the array has 0 elements (zero size). Comment out this warning if it's OK.\r\n\t");
			}
			
			if (fd.FieldOffset.HasValue) b.Append("[FieldOffset(").Append(fd.FieldOffset.Value).Append(")] ");
			
			var (st, isManaged, arraySig) = _GetTypeName_Field(fd, attr, tdeTopLevel);
			
			Debug.Assert(!(isManaged && level > 1));
			if (isManaged && level == 1) tdeTopLevel.isManagedStruct = true;
			
			if (arraySig != null) {
				if (attr.isFlexibleArray) {
					if (st.Ends('*')) b.AppendFormat("public FlexibleArray<nint> {1}; //{0}", st, name);
					else b.AppendFormat("public FlexibleArray<{0}> {1};", st, name);
				} else {
					uint size = arraySig.Sizes[0];
					//if (size == 1) print.it(fd.DebugToString()); //not many. Some look like should be a flexible array, some structs have 2 flexible arrays, some are just to fill unused gaps.
					
					if (st is "char" or "byte" or "short" or "int" or "long" or "sbyte" or "ushort" or "uint" or "ulong" or "float" or "double" or "bool") {
						b.AppendFormat("public fixed {0} {1}[{2}];", st, name, size);
					} else {
						b.AppendFormat("public {0}_{1} {0}; [InlineArray({1})] public struct {0}_{1} {{ {2} _; }}", name, size, st[^1] == '*' ? "nint" : st);
					}
					b.AppendFormat(" //[MarshalAs(UnmanagedType.{0}, SizeConst = {1})] public {2}[] {3};", st == "char" ? "ByValTStr" : "ByValArray", size, st, name);
				}
			} else {
				if (!attr.isBitfield) b.Append("public ");
				if (fd.IsStatic) b.Append("static "); //none
				
				if (name.Starts("Anonymous") && st[0] == '_' && st.Eq(1, name)) {
					if (st.Ends("_e__Union")) name = "u_" + name[9..];
					else if (st.Ends("_e__Struct")) name = "n_" + name[9..];
					//else print.it(name, st);
				}
				
				b.AppendFormat("{0} {1};", st, name);
			}
			
			if (attr.isBitfield) _Bitfields();
		}
		
		void _Bitfields() {
			string type = fd.FieldType.ElementType.ToKeywordString();
			string index = name[9..]; //_bitfield1 -> 1
			string s1 = null, s2 = null; if (type is "byte" or "sbyte" or "short" or "ushort") (s1, s2) = ("(" + type + ")(", ")");
			b.Start(level);
			b.AppendFormat("{0} _{1}G(int o, {0} m) => {3}{2} >> o & m{4};", type, index, name, s1, s2);
			b.Start(level);
			b.AppendFormat("void _{1}S(int o, {0} m, {0} v) => {2} = {3}({2} & ~(m << o)) | ((v & m) << o){4};", type, index, name, s1, s2);
			
			foreach (var ca in fd.CustomAttributes) {
				if (ca.AttributeType.Name == "NativeBitfieldAttribute") {
					b.Start(level);
					b.AppendFormat("public {0} {1} {{ get => _{4}G({2}, {3}); set => _{4}S({2}, {3}, value); }}", type, ca.ArgString(0), ca.ArgInt(1), (1 << ca.ArgInt(2)) - 1, index);
				}
			}
		}
	}
	
	//global constant (#define, const) or variable (eg GUID)
	void _ConstOrStaticReadonly(FieldDef fd) {
		b.Start(0);
		string name = fd.Name;
		b.Append("internal ");
		if (fd.HasConstant) {
			if (name[^1] == 'A' && fd.DeclaringType.FindField(name.Ends("_A") ? name[..^2] : name.ReplaceAt(^1.., "W")) != null) return; //info: don't use [NativeEncodingAttribute("ansi")]. Only string constants have it, and maybe not all.
			_Const(fd, name);
			//print.it(b);
		} else {
			if (!fd.HasCustomAttributes) throw new Exception("no attributes");
			b.Append("static readonly ");
			
			var t = fd.FieldType;
			var st = t.TypeName;
			//if (!(st is "Guid" or "PROPERTYKEY" or "DEVPROPKEY" or "SID_IDENTIFIER_AUTHORITY")) print.it(st);
			b.Append(st).Append(' ').Append(name).Append(" = new(");
			
			//if (fd.HasCustomAttributes) {
			//	foreach (var v in fd.CustomAttributes) {
			//		string s = v.AttributeType.Name;
			//		if (s is "DocumentationAttribute" or "NativeEncodingAttribute") continue;
			//		if (s is "GuidAttribute" or "ConstantAttribute") continue;
			//		print.it(s, name); //none
			//	}
			//}
			
			//print.it(st, name);
			if (st == "Guid") {
				var ca = fd.CustomAttributes.Find("Windows.Win32.Foundation.Metadata.GuidAttribute") ?? throw new Exception("no Guid attr");
				_AppendString(ca.ArgGuid());
			} else { //see _AddSomeTypes()
				var ca = fd.CustomAttributes.Find("Windows.Win32.Foundation.Metadata.ConstantAttribute") ?? throw new Exception("no ConstantAttribute");
				string s = ca.ArgString(0);
				if (0 != s.RxReplace(@"^\{((?:\d+, ){10}\d+)\}(, \d+)$", "new($1)$2", out s, 1)) b.Append(s); //eg PROPERTYKEY
				else if (s.Like("{*}")) b.Append(s, 1, s.Length - 2); //SID_IDENTIFIER_AUTHORITY
				else if (s != "0") print.warning(s);
			}
			
			b.Append(')');
			//print.it(b);
		}
		b.Append(';');
		_AddToDict(name);
	}
	
	void _Const(FieldDef fd, string name) {
		//if (fd.HasCustomAttributes) {
		//	foreach (var v in fd.CustomAttributes) {
		//		string s = v.AttributeType.Name;
		//		if (s is "DocumentationAttribute") continue;
		//		if (s is "NativeEncodingAttribute") continue;
		//		print.it(s, name); //only several ConstAttribute
		//	}
		//}
		
		b.Append("const ");
		var c = fd.Constant;
		var ct = c.Type;
		var st = ct.ToKeywordString();
		
		//var st2 = fd.FieldType.TypeName;
		//if (_typedefs.TryGetValue(st2, out var st3)) st2 = st3; else st2 = _SystemTypeNameToKeyword(st2);
		//if (st2 != st) print.it(st, st2, name);
		
		if (ct is ElementType.U4 && name.Starts("WM_") && (uint)c.Value <= 0xffff) st = "ushort";
		
		b.Append(st).Append(' ').Append(name).Append(" = ");
		if (ct == ElementType.String) {
			_AppendString(c.ValueString());
		} else if (ct is ElementType.U4 or ElementType.U1 or ElementType.U2 or ElementType.U8) {
			b.AppendFormat("0x{0:X}", c.Value);
		} else {
			var v = c.Value;
			if (v is int iv && iv < 0 && fd.FieldType.TypeName is "HRESULT" or "NTSTATUS") b.AppendFormat("unchecked((int)0x{0:X})", v);
			else b.Append(c.Value);
			
			if (ct is ElementType.R4) b.Append('f');
		}
	}
	
	void _Enum(TypeDef td) {
		string name = td.Name;
		
		bool isFlags = false, isScoped = false;
		if (td.HasCustomAttributes) {
			foreach (var ca in td.CustomAttributes) {
				string s = ca.AttributeType.Name;
				switch (s) {
				case "FlagsAttribute": isFlags = true; break;
				case "ScopedEnumAttribute": isScoped = true; break;
				case "DocumentationAttribute": break;
				case "AssociatedConstantAttribute": break; //few
				default: print.it(s); break;
				}
			}
		}
		
		if (!isFlags) isFlags = _IsFlagsEnum();
		if (!isScoped) isScoped = _IsScopedEnum();
		
		//In metadata, many #define constants are added to enums. Most of these enums are not Windows API.
		//It is bad, because:
		//	- Many parameters etc where these constants are used are not of these enum types. Then how users can find these enums? Also need to cast always.
		//	- Many other constants aren't added to enums. It seems the work is unfinished.
		//	- Some enums are large (eg WIN32_ERROR 3335). Usually users need only few constants. And in LA it's super easy to find them and add to the code.
		//	- Noticed some errors, eg in enum SHARE_INFO_PERMISSIONS wrong ACCESS_ALL and missing ACCESS_GROUP.
		//	- I don't like to write arguments like `api.ENUM_TYPE.ET_CONSTANT1 | api.ENUM_TYPE.ET_CONSTANT2`. Better use Windows API more like in C++. Most constants have a prefix. And normally they are in an `api` class.
		//Instead add enum members as const. Unless the enum is marked as scoped or members don't have a common prefix etc.
		
		if (isScoped) { //add as enum
			_nAll++;
			b.Start(0);
			if (isFlags) b.AppendLine("[Flags]");
			b.Append("internal enum ").Append(name);
			var ut = td.GetEnumUnderlyingType().ElementType;
			if (ut != ElementType.I4) b.Append(" : ").Append(ut.ToKeywordString());
			b.Append(" {\r\n\t");
			
			long lNext = 0; ulong ulNext = 0;
			bool once = false;
			foreach (var fd in td.Fields) {
				if (fd.Constant is { } c) {
					if (!once) once = true; else b.Append(",\r\n\t");
					b.Append(fd.Name);
					var (isUlong, l, u) = _ObjectToLongOrUlong(c.Value);
					if (isUlong) {
						if (u != ulNext || isFlags) b.Append(" = ").AppendFormat("0x{0:X}", u);
						ulNext = u + 1;
					} else {
						if (l != lNext || isFlags) {
							b.Append(" = ");
							if (isFlags && l >= 0) b.AppendFormat("0x{0:X}", l); else b.Append(l);
						}
						lNext = l + 1;
					}
				}
			}
			
			b.Append("\r\n}");
			_AddToDict(name);
			_types.Add(td.Namespace + "." + name, new(td, name, default) { isProcessed = true });
		} else { //add members as const
			string st = null; bool hexInt = false;
			switch (name) {
			case "WIN32_ERROR": (st, hexInt) = ("int", true); break; //uint
			case "VIRTUAL_KEY": break; //ushort
			}
			
			st ??= _SystemTypeNameToKeyword(td.GetEnumUnderlyingType().TypeName);
			
			foreach (var fd in td.Fields) {
				if (fd.Constant is { } c) {
					b.Start(0);
					string cName = fd.Name;
					b.Append("internal const ").Append(st).Append(' ').Append(cName).Append(" = ");
					var (isUlong, l, u) = _ObjectToLongOrUlong(c.Value);
					if (hexInt) {
						Debug.Assert(isUlong);
						if ((int)u < 0) b.AppendFormat("unchecked((int)0x{0:X})", u); else b.Append(u);
					} else if (isUlong) {
						b.AppendFormat("0x{0:X}", u);
					} else {
						if (isFlags && l >= 0) b.AppendFormat("0x{0:X}", l); else b.Append(l);
					}
					b.Append(';');
					//print.it(b);
					_AddToDict(cName);
					_nAll++;
				}
			}
			
			if (!_typedefs.TryAdd(name, st)) {
				if (_typedefs[name] != st) print.warning($"{name}, {st}, {_typedefs[name]}");
			}
		}
		
		bool _IsFlagsEnum() { //if no [Flags], assume it's a flags enum if contains only values like 1 2 4 8...
			ulong max = 0;
			foreach (var fd in td.Fields) {
				if (fd.Constant is { } c) {
					var (isUlong, l, u) = _ObjectToLongOrUlong(c.Value);
					if (!isUlong) u = (ulong)l;
					if (u != 0 && !BitOperations.IsPow2(u)) return false;
					max = Math.Max(max, u);
				}
			}
			//if (max<4 && name.Ends("FLAGS")) print.it(max, name);
			return max >= 4 || name.Ends("FLAGS");
		}
		
		bool _IsScopedEnum() { //if no [ScopedEnum], let it be enum if some member names don't contain '_' and contain lowercase chars, unless most such names have a common prefix of length >= 2.
			var a = td.Fields;
			ReadOnlySpan<char> span1 = null;
			int minPrefixLength = int.MaxValue, nDiffPrefix = 0, nCommonPrefix = 0;
			for (int i = 1; i < a.Count; i++) {
				var s8 = a[i].Name;
				if (s8.Length < 4) return true;
				if (s8.IndexOf('_', 1) < 0 && s8.String.Any(o => char.IsAsciiLetterLower(o))) {
					if (span1 == null) span1 = s8.String;
					else {
						var p = span1.CommonPrefixLength(s8.String);
						if (p < 2) {
							if (++nDiffPrefix > 1) break; //allow single member without common prefix; usually `MaxX`
						} else {
							nCommonPrefix++;
							minPrefixLength = Math.Min(minPrefixLength, p);
						}
					}
				}
			}
			if (nDiffPrefix > 0) {
				//if (nDiffPrefix > 1 || nCommonPrefix == 0) print.it(name, td.Fields.Skip(1).Select(o => o.Name));
				if (nDiffPrefix > 1 || nCommonPrefix == 0) return true;
				//print.it(name, td.Fields.Skip(1).Select(o => o.Name));
			}
			return false;
		}
	}
	
	void _Method2(string name, MethodDef md, bool inCOM, bool inDelegate, int bStart, bool bit32) {
		if (md.HasReturnType) {
			var st = _GetTypeName_Return(md, inCOM, bStart, bit32);
			b.Append(st).Append(' ');
		} else b.Append("void ");
		
		//print.it($"<><lc yellowgreen>{name}  ({md.DeclaringType.Name})<>");
		
		b.Append(name).Append('(');
		string sep = null;
		foreach (var p in md.Parameters) {
			if (!p.IsNormalMethodParameter) continue; //hidden `this` parameter
			b.Append(sep); sep = ", ";
			//print.it(p.Index, p.Type.TypeName, p.Name);
			
			var pName = _RenameIfKeyword(p.Name);
			var pd = p.ParamDef;
			_ParamAttributes attr = pd.HasCustomAttributes ? new(pd) : default;
			
			var st = _GetTypeName_Param(p, attr, inCOM, inDelegate, bit32);
			
			b.Append(st).Append(' ').Append(pName);
		}
		if (md.CallingConvention is dnlib.DotNet.CallingConvention.VarArg) b.Append(", __arglist");
		b.Append(");");
	}
	
	void _ExternMethod(MethodDef md) {
		//if (md.Attributes is not (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.PinvokeImpl)) print.it(md.Attributes, md.Name);
		
		b.Start(0);
		string name = md.Name, name0 = name;
		var attr = md.HasCustomAttributes ? new _MethodAttributes(md) : default;
		if (attr.skip) return;
		if (attr.unicode && name[^1] == 'W') name = name[..^1];
		//if (attr.bit32) print.it(name, Result.ContainsKey(name));
		if (attr.bit32) name += "__32"; //even if the 32-bit version is named differently, eg with suffix "64"
		
		if (attr.inline) {
			Debug.Assert(md.Parameters.Count == 0);
			var st = _GetTypeName_Return(md, false, 0, attr.bit32);
			//print.it(name, attr.constant, md.ReturnType.TypeName, st);
			Debug.Assert(st is "nint" or "int" or "uint");
			b.AppendFormat("internal static {0} {1}() => {2};", st, name, attr.constant);
		} else {
			if (attr.obsolete) b.AppendLine("[Obsolete]");
			
			//[DllImport]
			var im = md.ImplMap;
			b.Append("[DllImport(\"").Append(im.Module.Name.String.Lower()).Append('"');
			//if (name0!=im.Name) print.it(im.Name, md.Name); //few, although many ordinal-only functions exist in Windows API (not all exist in winmd). Never mind.
			if (name != name0) b.AppendFormat(", EntryPoint = \"{0}\"", name0);
			if (im.IsCallConvCdecl) b.Append(", CallingConvention = CallingConvention.Cdecl");
			if (im.SupportsLastError) b.Append(", SetLastError = true");
			if (0 != (im.Attributes & ~(PInvokeAttributes.CallConvWinapi | PInvokeAttributes.CallConvCdecl | PInvokeAttributes.SupportsLastError | PInvokeAttributes.NoMangle))) print.warning(name);
			b.AppendLine(")]");
			
			b.Append("internal static extern ");
			_Method2(name, md, false, false, 0, attr.bit32);
		}
		
		_AddToDict(name);
		//print.it(b);
	}
	
	void _Delegate(TypeDef td, string name, in _TypeAttributes attr) {
		if (attr.cdeclDelegate) b.AppendLine("[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
		
		b.Append("internal delegate ");
		var md = td.Methods[1];
		//if (md is var md && md.Attributes is not (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask)) print.it(md.Attributes, td.Name);
		_Method2(name, md, false, true, 0, attr.bit32);
		_FunctionPointer(attr.cdeclDelegate, attr.bit32);
		
		_AddToDict(name);
		
		//appends `//delegate*<...>`.
		void _FunctionPointer(bool cdecl, bool bit32) {
			b.Append("\r\n//delegate* unmanaged");
			if (cdecl) b.Append("[Cdecl]");
			b.Append('<');
			string sep = null;
			foreach (var p in md.Parameters) {
				if (!p.IsNormalMethodParameter) continue; //hidden `this` parameter
				b.Append(sep); sep = ", ";
				b.Append(_GetTN(p.Type, bit32, p.ParamDef));
			}
			if (md.HasReturnType) b.Append(", ").Append(_GetTN(md.ReturnType, bit32, md)); else b.Append(", void");
			b.Append('>');
			
			string _GetTN(TypeSig t, bool bit32, object def) {
				var (s2, marshal, s) = _GetTypeName(t, out int ptr, false, (false, false), bit32, def);
				//if (marshal != null) print.it(s, s2, marshal, def.DebugToString());
				if (ptr > 0 && def is ParamDef pd) {
					bool isArray = pd.HasCustomAttributes && new _ParamAttributes(pd).isArrayPtr;
					if (!isArray) { ptr--; b.Append(!pd.IsOut ? "in " : pd.IsIn ? "ref " : "out "); }
				}
				if (ptr > 0) s = s.PadRight(s.Length + ptr, '*');
				return s;
			}
		}
	}
	
	void _InterfaceMethod(MethodDef md, bool bit32) {
		//if (md.Attributes is not (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask | MethodAttributes.Abstract)) print.it(md.Attributes, md.Name, md.DeclaringType.Name);
		Debug.Assert(md.CallingConvention is 0 or dnlib.DotNet.CallingConvention.ThisCall);
		
		b.Start(1);
		int bStart = b.Length;
		string name = md.Name;
		
		b.Append("[PreserveSig] ");
		
		_Method2(name, md, true, false, bStart, bit32);
	}
	
	void _Interface(TypeDef td, string name, in _TypeAttributes attr) {
		string name0 = td.Name; //because name may be mangled
		bool isUnk = false, isDisp = false;
		string bName = null, bMembers = null;
		
		if (!td.HasInterfaces) {
			if (name == "IUnknown") {
				b.Append($$"""
[ComImport, Guid("{{attr.guid}}"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUnknown {}
""");
				_AddToDict(name);
				return;
			}
			
			//not a COM interface. Difficult to use in C#. Not many. Add anyway, without [ComImport].
			//print.it(name, attr.guid);
			//TODO2: declare as struct with vtbl. Eg IMemoryAllocator.
		} else {
			//if (td.Interfaces.Count != 1) print.it(name);
			//if (td.Attributes != (TypeAttributes.Public | TypeAttributes.ClassSemanticMask | TypeAttributes.Abstract)) print.it(td.Attributes);
			
			var ii = td.Interfaces[0].Interface;
			bName = ii.Name;
			if (bName == "IUnknown") isUnk = true;
			else if (bName == "IDispatch") isDisp = true;
			else {
				string nn = ii.Namespace + "." + bName;
				g1:
				if (_iBase.TryGetValue(nn, out var v)) {
					(bMembers, isUnk, isDisp, bName) = v;
				} else {
					//print.it("interface base", name, bName);
					_Type(_types[nn], true);
					goto g1;
				}
			}
		}
		
		if (isUnk || isDisp) {
			b.AppendFormat("[ComImport, Guid(\"{0}\")", attr.guid);
			if (!isDisp) b.Append(", InterfaceType(ComInterfaceType.InterfaceIsIUnknown)");
			b.AppendLine("]");
		} else {
			if (attr.guid != null) b.AppendFormat("[Guid(\"{0}\")]\r\n", attr.guid);
		}
		
		b.Append("internal interface ").Append(name);
		if (bMembers != null) b.Append(" : ").Append(bName);
		
		b.Append(" {");
		int membersStart = b.Length;
		
		if (bMembers != null) {
			if (!bMembers.Starts("\r\n\t//")) b.Append("\r\n\t// ").Append(bName);
			b.Append(bMembers.RxReplace(@"(?m)^\t\[PreserveSig\] \K(?!new )", "new "));
			b.Append("\r\n\t// ").Append(name);
		}
		
		foreach (var md in td.Methods) {
			_InterfaceMethod(md, attr.bit32);
		}
		
		int membersLen = b.Length - membersStart;
		_iBase.Add(td.Namespace + "." + name0, (string.Create(membersLen, 0, (s, _) => b.CopyTo(membersStart, s, membersLen)), isUnk, isDisp, name));
		
		b.Append(b.Length > membersStart ? "\r\n}" : " }");
		_AddToDict(name);
		
		//print.it(b);
		//if (bMembers != null) print.it(b);
	}
	
	void _Typedef(TypeDef td) {
		string name = td.Name;
		//print.it(name);
		string r = null;
		
		if (name == "HWND") {
			r = "wnd";
		} else if (name is "BOOL" or "BSTR") {
			return;
		} else {
			var f = td.Fields[0];
			var tn = f.FieldType.TypeName;
			r = tn switch {
				"Void*" or "IntPtr" or "UIntPtr" => "nint",
				"Char*" => "char*",
				"Byte*" => "byte*",
				_ => _SystemTypeNameToKeyword(tn)
			};
			if (r == null) {
				print.warning(tn);
				return;
			}
			Debug.Assert(r != "bool");
		}
		_typedefs.Add(name, r);
	}
	
	void _CoClass(TypeDef td, in _TypeAttributes attr) {
		Debug.Assert(!td.HasMethods && !td.HasNestedTypes);
		b.Clear();
		string name = td.Name;
		b.Append($$"""
[ComImport, Guid("{{attr.guid}}"), ClassInterface(ClassInterfaceType.None)]
internal class {{name}} {}
""");
		_AddToDict(name);
	}
}
