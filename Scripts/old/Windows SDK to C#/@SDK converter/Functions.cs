namespace SdkConverter;

unsafe partial class Converter {
	void _DeclareFunction() {
		//now we expect one of:
		//	function declaration, like TYPE __stdcall Func(TYPE a, TYPE b);
		//	global variable, which can be:
		//		previously-defined type, like TYPE var;
		//		now-defined function type, like TYPE (*var)(TYPE a, TYPE b);
		
		
		var d = new _FINDTYPEDATA();
		_FindTypename(false, ref d);
		_i++;
		
		//pointer
		int ptr = 0;
		while (_TokIsChar(_i, '*', '&')) { _i++; ptr++; }
		
		if (!_TokIsIdent(_i)) {
			//could be 'T (*var)(...);' //0 in SDK
			//_Err(_i, "unexpected");
			_SkipStatement(true);
			return;
		}
		
		int iCallConv = 0;
		if (_TokIsIdent(_i + 1)) iCallConv = _i++;
		
		if (!_TokIsChar(_i + 1, '(')) {
			//could be 'T var;' //1 in SDK
			//_Err(_i, "unexpected");
			_i = d.outTypenameToken;
			if (!_ExternConst()) _SkipStatement(true);
			return;
		}
		
		string name = _TokToString(_i++);
		
		//skip some
		if (name.IndexOf('_') > 0) {
			if (name.Ends("_UserSize")
				|| name.Ends("_UserMarshal")
				|| name.Ends("_UserUnmarshal")
				|| name.Ends("_UserFree")
				|| name.Ends("_UserSize64")
				|| name.Ends("_UserMarshal64")
				|| name.Ends("_UserUnmarshal64")
				|| name.Ends("_UserFree64")
				) {
				_SkipStatement(false);
				return;
			}
		}
		
		//string callConv = iCallConv == 0 ? "Cdecl" : _ConvertCallConv(iCallConv); //no. Now many __stdcall functions are declared without __stdcall (WINAPI etc). I could't find where it is documented, but it means now __stdcall is default.
		string callConv = iCallConv == 0 ? null : _ConvertCallConv(iCallConv);
		string returnType, returnAttr = null; bool isHRESULT = false;
		if (ptr == 0 && _TokIs(d.outTypenameToken, "HRESULT")) {
			isHRESULT = true;
			returnType = "int";
		} else {
			returnType = _ConvertTypeName(d.outSym, ref ptr, d.outIsConst, d.outTypenameToken, _TypeContext.Return, out returnAttr);
		}
		
		string dll, nameInDll = null;
		if (_funcDllMap.TryGetValue(name, out dll)) {
			int i = dll.IndexOf('|');
			if (i > 0) {
				nameInDll = dll.Substring(i + 1);
				dll = dll.Substring(0, i);
				//print.it(nameInDll);
			} else if (name.Starts("K32")) {
				//print.it(name);
				nameInDll = name;
				name = name.Substring(3);
				if (name.Ends("W")) name = name.Remove(name.Length - 1);
				else if (name.Ends("A")) {
					_SkipStatement(false);
					return;
				}
			}
		} else {
			bool skip = false;
			if (name.Contains("_")) skip = true; //mostly CRT library functions, X_Proxy/X_Stub, X_UserMarshal and similar
			else if (name.Starts("Dll")) skip = true; //DllInstall, DllRegisterServer etc
			else if (name.Starts("Ndr") || name.Starts("Rpc")) skip = true; //undocumented
			else {
				//print.it(name);
				skip = true; //all these in SDK others are undocumented, or documented as deprecated/removed
				_funcUnknownDll.Add(name);
			}
			
			if (skip) {
				_SkipStatement();
				return;
			}
			dll = "?";
		}
		
		var sb = _sbFuncTemp;
		sb.Clear();
		sb.Append("\r\n[DllImport(\"");
		sb.Append(dll);
		sb.Append('"');
		if (nameInDll != null) sb.AppendFormat(", EntryPoint=\"{0}\"", nameInDll);
		if (callConv != null) sb.AppendFormat(", CallingConvention=CallingConvention.{0}", callConv);
		if (isHRESULT) sb.Append(", PreserveSig=true"); //default, but makes clear that it returns HRESULT and easier to change to 'false'
		sb.AppendLine(")]");
		if (returnAttr != null) sb.AppendLine(returnAttr);
		sb.Append("internal static extern ");
		sb.Append(returnType);
		sb.Append(' ');
		sb.Append(name);
		
		_ConvertParameters(sb, name, _TypeContext.Parameter);
		
		string decl = sb.ToString();
		_func[name] = decl;
		//try { _func.Add(name, decl); }
		//catch { //about 10 in SDK. The second declarations are identical or better (without tagSTRUCT).
		//	print.it("----");
		//	print.it(name);
		//	print.it(_func[name]);
		//	print.it(decl);
		//}
		
		if (!_TokIsChar(_i, ';')) _Err(_i, "unexpected");
	}
	
	Dictionary<string, string> _funcDllMap;
	List<string> _funcUnknownDll = new List<string>();
	
	void _FunctionsFinally() {
		if (_funcUnknownDll.Count > 100) {
			print.it("Warning: too many unknown dll:");
			print.it(_funcUnknownDll);
		}
	}
}
