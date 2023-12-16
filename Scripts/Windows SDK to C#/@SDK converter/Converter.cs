namespace SdkConverter;

unsafe partial class Converter {
	string _cppFile;
	bool _is32bit;
	
	char[] _src; //C/C++ source code
	char* _s0; //_src start
	
	StringBuilder _sbInterface = new();
	//StringBuilder _sbCoclass = new();
	StringBuilder _sbVar = new();
	StringBuilder _sbInlineDelegate = new(); //from callback function types defined in parameter list or member list
	
	List<_Token> _tok = new();
	int _nTok; //token count, except the last '\0' tokens
	int _nTokUntilDefUndef; //token count until first `d or `u (was #define/#undef), except the separating '\0' token
	int _i; //current token
	
	_Namespace[] _ns = new _Namespace[10]; //stack of namespaces
	int _nsCurrent; //index in _ns
	StringBuilder _sbType; //_ns[_nsCurrent].sb
	Dictionary<_Token, _Symbol> _keywords = new();
	
	Dictionary<string, string> _func = new(); //function declaration
	StringBuilder _sbFuncTemp = new(); //used to format _func values
	
	List<string> _cppConst = new(); //const CONSTANT
	
	Dictionary<string, string> _defineConst = new(); //#define CONSTANT
	Dictionary<string, int> _defineW = new(); //#define STRUCT STRUCTW
	Dictionary<string, string> _defineOther = new(); //#define MACRO(...)
	
	Stack<int> _packStack = new(); int _pack = 8; //#pragma pack
	
	int _anonymousStructSuffixCounter; //used to create unique name for anonymous struct
	
	/// <summary>
	/// Converts C++ header-list file cppFile to C# declarations and saves in file csFile.
	/// </summary>
	/// <param name="is32bit">Prefer 32-bit. Although the converter tries to create declarations that don't depend on 32/64 bit, in some places it will create different declaration if this is true, for example IntPtr size will be 4 bytes.</param>
	public void Convert(string cppFile, string csFile, bool is32bit) {
		_cppFile = cppFile;
		_is32bit = is32bit;
		
		try {
			if (_src != null) throw new Exception("cannot call Convert multiple times. Create new Converter instance.");
			
			using (var reader = new StreamReader(_cppFile)) {
				long n = reader.BaseStream.Length + 4; if (n > int.MaxValue / 4) throw new Exception("file too big");
				reader.Read(_src = new char[n], 0, (int)n);
			}
			
			_InitTables();
			_InitSymbols();
			_InitMaps();
			_InitCsKeywords();
			
			string stf = null;
			
			fixed (char* p = _src) {
				_s0 = p;
				_Tokenize(p);
				_InitInterfaces();
				
				//Test(); return;
				
				_i = 1;
				_ConvertAll(0);
				
				//_ConvertTypedefAndTag();
				
				//print.it("#define CONST:");
				//print.it(_defineConst);
				//print.it("#define other:");
				//print.it(_defineOther);
				//print.it(_defineConst.Count, _defineOther.Count);
				//27890, 1061
				//27659, 998
				//27605, 983
				
				_FunctionsFinally();
				
				stf = _PostProcessTypesFunctionsInterfaces();
				//print.it(stf);
			}
			
			string sh = @"// Windows API declarations for C#.
// Don't add this file to your project. Copy-paste only declarations that you need.
// Not all declarations can be compiled without editing.
//    For example, cannot declare some struct pointer (use IntPtr instead, or in struct replace non-blittable types with IntPtr etc), cannot use undefined struct pointer/ref/out (use IntPtr instead).
// Not all declarations are correct, usually because declarations in Windows SDK files from which they have been automatically converted lack some info.
//    For example, some function parameters that should be 'out' or '[Out]' or '[In]' or array now are just 'ref', because SDK declarations didn't have proper in/out annotations. Also for this reason some parameters that should be 'string' now are 'char*'.
//    You may want to create overloads where parameters can be of more than one type.
//    You may want to add 'SetLastError=true' to DllImport attribute parameters.
// Some declarations contain pointers and therefore can be used only in 'unsafe' context, in some cases with 'fixed'. Or you can replace pointers to IntPtr.
// These declarations are for Windows 10. Some Windows API are different or missing on other Windows versions.
// In some cases need to use different declarations in 32-bit and 64-bit process. This file contains everything that is not different, + 64-bit versions, + 32-bit versions with name suffix ""__32"".

using System;
using System.Runtime.InteropServices;
#if true
using Au;
using Au.Types;
#else
using wnd = System.IntPtr; //HWND (window handle)
#endif

//add this to projects that will use these API
[module: DefaultCharSet(CharSet.Unicode)]

static unsafe class API
{
";
			
			using (var writer = new StreamWriter(csFile)) {
				writer.Write(sh);
				writer.Write(stf);
				//if(_sbType.Length > 0 || _sbInterface.Length > 0) {
				//	writer.Write("\r\n// USED TAG-TYPES\r\n");
				//	if(_sbType.Length > 0) writer.Write(_sbType.ToString());
				//	if(_sbInterface.Length > 0) writer.Write(_sbInterface.ToString());
				//}
				//writer.WriteLine("\r\n// COCLASS");
				//writer.Write(_sbCoclass.ToString());
				writer.WriteLine("\r\n// VARIABLE");
				writer.Write(_sbVar.ToString());
				writer.WriteLine("\r\n// CONSTANT\r\n");
				_ConstantsFinally(writer);
				writer.Write("\r\n}\r\n");
			}
		}
		//#if !TEST_SMALL
		catch (Exception e) { _OnCatchException(e); throw; }
		//#endif
		finally {
			Marshal.FreeHGlobal((IntPtr)_keywordMemory);
		}
		
		print.it($"DONE {(is32bit ? 32 : 64)}-bit");
	}
	
	char* _keywordMemory;
	char* _km; //current keyword
	
	void _AddKeyword(string name, _Symbol sym) {
		char* n = _km;
		for (int i = 0; i < name.Length; i++) *_km++ = name[i];
		_keywords.Add(new _Token(n, (int)(_km - n)), sym);
		*_km++ = '\0';
	}
	
	void _AddKeywords(_Symbol sym, params string[] names) {
		foreach (string s in names) {
			_AddKeyword(s, sym);
		}
	}
	
	void _InitSymbols() {
		for (int i = 0; i < _ns.Length; i++) _ns[i] = new _Namespace();
		_sbType = _ns[0].sb;
		
		_km = _keywordMemory = (char*)Marshal.AllocHGlobal(4000);
		
		//Add C++ keywords and types, except those that can only be in function body or other places where we skip.
		//The commented keywords/types are currently not supported and not expected to be in C/C++ headers that we'll parse.
		
		_Keyword k;
		
		//_AddKeywords(new _Keyword(cannotStartStatement:true), "__based", "_based"); //0 in SDK
		//_AddKeyword("__event", new _Keyword()); //0 in SDK
		//_AddKeyword("__identifier", new _Keyword()); //0 in SDK
		//_AddKeywords(new _Keyword(), "__if_exists", "_if_exists", "__if_not_exists", "_if_not_exists"); //0 in SDK. Usually used only in function body.
		//_AddKeywords(new _Keyword(cannotStartStatement: true), "__multiple_inheritance", "_multiple_inheritance", "__single_inheritance", "_single_inheritance", "__virtual_inheritance", "_virtual_inheritance"); //0 in SDK
		//_AddKeyword("__uuidof", new _Keyword(cannotStartStatement: true)); //should be only in function body
		//_AddKeyword("alignas", k = new _Keyword(cannotStartStatement: true)); //removed in script
		//_AddKeywords(new _Keyword(cannotStartStatement: true), "alignof", "__alignof"); //should be only in function body
		//_AddKeywords(new _Keyword(cannotStartStatement: true), "false", "true", "nullptr"); //we add enum values for it
		//_AddKeyword("property", k = new _Keyword()); //0 in SDK
		//_AddKeyword("signed", k = new _Keyword()); //replaced in script
		//_AddKeywords(new _Keyword(cannotStartStatement: true), "throw", "noexcept"); //eg void f() throw(int); //will be skipped together with functions
		//_AddKeyword("unsigned", k = new _Keyword()); //replaced in script
		
		k = new _Keyword(cannotStartStatement: true);
		_AddKeyword("sizeof", k); //can be like 'member[sizeof(X)]' or '#define X sizeof(Y)'
		
		k = new _Keyword();
		_AddKeyword("const", k);
		_AddKeyword("extern", k);
		_AddKeyword("namespace", k); //0 in SDK (1 in CRT, which is removed)
		_AddKeyword("using", k);
		_AddKeyword("virtual", k);
		
		k = new _Keyword(_KeywordT.TypeDecl);
		_AddKeyword("__interface", k);
		_AddKeyword("class", k);
		_AddKeyword("enum", k);
		_AddKeyword("struct", k);
		_AddKeyword("typedef", k);
		_AddKeyword("union", k);
		
		_AddKeywords(new _Keyword(_KeywordT.CallConv, cannotStartStatement: true), "__cdecl", "_cdecl", "__fastcall", "_fastcall", "__stdcall", "_stdcall", "__thiscall");
		
		_AddKeywords(new _Keyword(_KeywordT.IgnoreFuncEtc), "__forceinline", "__inline", "_inline", "inline", "template", "operator", "static_assert");
		
		_AddKeywords(new _Keyword(_KeywordT.PubPrivProt), "private", "protected", "public");
		
		//_AddKeywords(new _Keyword(_KeywordT.Ignore), "__unaligned", "__w64", "_w64", "volatile", "explicit", "friend", "mutable", "static", and so on); //removed in script
		//_AddKeywords(k = new _Keyword(_KeywordT.Declspec), "__declspec", "_declspec"); //removed in script
		
		_AddKeyword("uuid", k = new _Keyword(cannotStartStatement: true)); //was '__declspec(uuid', replaced in script
		
		_AddKeywords(new _CppType("sbyte", 1, false), "__int8", "char");
		_AddKeywords(new _CppType("short", 2, false), "__int16", "short");
		_AddKeywords(new _CppType("int", 4, false), "__int32", "int", "long");
		_AddKeyword("__int64", new _CppType("long", 8, false));
		_AddKeyword("double", new _CppType("double", 8, false));
		_AddKeyword("float", new _CppType("float", 4, false));
		_AddKeywords(new _CppType("char", 2, true), "__wchar_t", "wchar_t", "char16_t");
		//our preprocessor script replaced 'unsigned type' to 'u$type'
		//also the script replaced 'signed type' to 'type, 'long long' to '__int64', 'long double' to 'double'
		_AddKeywords(new _CppType("byte", 1, true), "u$__int8", "u$char");
		_AddKeywords(new _CppType("ushort", 2, true), "u$__int16", "u$short");
		_AddKeywords(new _CppType("uint", 4, true), "u$__int32", "u$int", "u$long", "char32_t");
		_AddKeyword("u$__int64", new _CppType("ulong", 8, true));
		_AddKeyword("bool", new _CppType("bool", 1, false));
		_AddKeyword("void", new _CppType("void", 0, false));
		//_AddKeywords(new _Keyword(), "__m128", "__m128d", "__m128i"); //our script defined these as struct, because C# does not have a matching type
		//_AddKeywords(new _Keyword(), "__m64"); //not used in SDK
		_AddKeyword("auto", new _CppType("var", 1, false)); //has two meanings depending on compiler options. The old auto is a local variable, and we will not encounter it because we skip function bodies. The new auto is like C# var; it can also be applied to global variables too, unlike in C#; we'll ignore all variable declarations.
		_AddKeyword("IntPtr", new _CppType("IntPtr", _is32bit ? 4 : 8, false));
		
		_AddSymbol("LPARAM", _sym_LPARAM = new _Struct("LPARAM", false), 0);
		_AddSymbol("HWND", _sym_wnd = new _Struct("wnd", false), 0);
	}
	
	_Struct _sym_LPARAM, _sym_wnd;
	
	void _ConvertAll(int nestLevel) {
		for (; _i < _nTokUntilDefUndef; _i++) {
			//print.it(_TokToString(_i));
			char* s = T(_i); char c = *s;
			if (_IsCharIdentStart(c)) {
				_Statement();
			} else if (c == '`') { //was #pragma pack
				_PragmaPack();
			} else if (c != ';' && c != '}') {
#if TEST_SMALL
					if(c == '/' && s[1] == '/') return;
#endif
				//print.it(_i);
				_Err(_i, $"unexpected: {_TokToString(_i)}");
			}
		}
		
		//info: script converted all #define/#undef to `d/`u and placed at the end
		_i++; //skip '\0' token inserted before first `d/`u token
		for (; _i < _nTok; _i++) {
			char* s = T(_i);
			if (*s == '`') { //was #define, #undef
				_DefineUndef();
			} else {
#if TEST_SMALL
					if(*s == '/' && s[1] == '/') return;
#endif
				//print.it(_i);
				_Err(_i, $"unexpected: {_TokToString(_i)}");
			}
		}
	}
	
	void _Statement() {
		//try {
		g0:
		_Symbol x = _FindSymbol(_i, true);
		var k = x as _Keyword;
		if (k != null) {
			if (k.cannotStartStatement) _Err(_i, "unexpected");
			
			if (k.kwType == _KeywordT.TypeDecl) {
				//is forward decl like 'struct X* Func(...);'?
				if (!_TokIsChar(_i, 't') && _TokIsChar(_i + 2, '*', '&')) {
					_InlineForwardDeclaration();
					goto g0;
				}
				
				var ftd = new _FINDTYPEDATA(); //just for ref parameter
				_DeclareType(ref ftd, true);
			} else if (k.kwType == _KeywordT.IgnoreFuncEtc) {
				_SkipStatement();
				return;
			} else if (_TokIs(_i, "extern")) {
				if (_TokIsChar(_i + 1, '"')) { //extern "C"
					_i++;
					if (_TokIsChar(_i + 1, '{')) _i++;
					return;
				} else { //extern T X
					if (_TokIs(++_i, "const")) _i++;
					if (!_ExternConst()) _SkipStatement();
				}
			} else if (_TokIs(_i, "const")) {
				_i++;
				if (!_ExternConst(true)) _SkipStatement();
			} else {
				//can be:
				//namespace
				
				_Err(_i, "unexpected");
			}
			if (!_TokIsChar(_i, ';')) _Err(_i, "unexpected");
			
		} else { //a type
			_DeclareFunction();
		}
		//}
		//catch(ConverterException e) {
		//	//print.it(e);
		//	wnd.findFast(null, "QM_Editor").SendS(Api.WM_SETTEXT, 1, $"M \"api_converter_error\" A(||) {e.Message}||{_cppFile}||{e.Offset}");
		//	if(dialog.show("Error. Skip statement and continue?", e.Message, "Yes|No").ButtonName != "Yes") throw e as Exception;
		//	_SkipStatement();
		//         }
	}
	
	/// <summary>
	/// Skips current statement.
	/// _i can be at any place in the statement except in parts enclosed in (), [] or {}.
	/// Finally _i will be at the ending ';' or '}' (if '}' is not followed by ';').
	/// </summary>
	/// <param name="debugShow"></param>
	void _SkipStatement(bool debugShow = false) {
#if DEBUG
		int i0 = _i;
#endif
		gk1:
		while (!_TokIsChar(_i, "({;[")) _i++;
		if (!_TokIsChar(_i, ';')) {
			_SkipEnclosed();
			if (!_TokIsChar(_i, '}')) goto gk1; //skip any number of (enclosed) or [enclosed] parts, else skip single {enclosed} part
			if (_TokIsChar(_i + 1, ';')) _i++;
		}
#if DEBUG
		if (debugShow) {
			string s = new(T(i0), 0, (int)(T(_i + 1) - T(i0)));
			print.it($"<><c 0xff>skipped:</c>\r\n{s}");
		}
#endif
	}
	
	void _PragmaPack() {
		const string sErr1 = "expected (push), (pop), (n), (push, n), (pop, n) or ()";
		char* s0 = T(++_i);
		int nArg = _GetParameters(_params), pack = 0, pushPop = 0;
		
		if (nArg == 0) {
			pack = 8;
		} else if (nArg > 2) {
			_Err(s0, sErr1);
		} else {
			if (_params[0].nTok != 1 || (nArg > 1 && _params[1].nTok != 1)) _Err(s0, sErr1);
			int i = _params[0].iTok;
			char* s = T(i);
			
			if (_TokIs(i, "push")) pushPop = 1;
			else if (_TokIs(i, "pop")) pushPop = -1;
			else if (nArg == 2) _Err(s0, sErr1);
			else pack = -1;
			
			if (nArg == 2) {
				s = T(_params[1].iTok);
				pack = -1;
			}
			
			if (pack < 0) { //a pack value must be specified
				pack = Api.strtoi(s);
				if (pack != 1 && pack != 2 && pack != 4 && pack != 8 && pack != 16)
					_Err(s, "expected 1, 2, 4, 8 or 16");
			}
		}
		
		if (pushPop > 0) _packStack.Push(_pack); else if (pushPop < 0 && _packStack.Count > 0) _pack = _packStack.Pop();
		if (pack != 0) _pack = pack;
		//print.it($"pushPop={pushPop}, pack={pack},    _pack={_pack}, _packStack.Count={_packStack.Count}");
	}
	
	bool _ExternConst(bool isJustConst = false) {
		if (!(_TokIsIdent(_i) && _TokIsIdent(_i + 1))) return false; //must be TYPE name
		int what = 0;
		if (_TokIsChar(_i + 2, ';')) what = 1;
		else if (isJustConst && _TokIsChar(_i + 2, '=')) what = 2;
		else return false;
		
		//get type
		_Symbol x;
		if (!_TryFindSymbol(_i, out x, false)) return false;
		if (0 != _Unalias(_i, ref x)) return false;
		_i++;
		
		if (what == 1) {
			//we need only GUID
			if (x.csTypename != "GUID") return false;
			
			string name = _TokToString(_i), data;
			if (!_guids.TryGetValue(name, out data)) {
				//print.it(name);
				return false;
			}
			
			if (name.Ends("A") && char.IsLower(name[name.Length - 2])) {
				//print.it(name);
				return false;
			} else if (name.Ends("W") && char.IsLower(name[name.Length - 2])) {
				//print.it(name);
				name = name.Remove(name.Length - 1);
			}
			
			if (!_guidsAdded.Add(name)) return false; //prevent duplicates
			
			_sbVar.AppendFormat("\r\ninternal static Guid {0} = new({1});\r\n", name, data);
			
			_i++;
		} else { //C++ const constant
			var ct = x as _CppType;
			if (ct == null) {
				//_Err(_i, "example");
				return false;
			}
			
			int iName = _i++, iValue = ++_i;
			while (!_TokIsChar(_i, ';')) _i++;
			string name = _TokToString(iName);
			
			_ExpressionResult r = _Expression(iValue, _i, name);
			//print.it(ct.csTypename, r.typeS, r.valueS);
			if (r.typeS == null) return false;
			
			_enumValues[_tok[iName]] = r.valueI;
			
			__sbDef.Clear();
			__sbDef.AppendFormat("internal const {0} {1} = {2};", ct.csTypename, name, r.valueS);
			//print.it(__sbDef);
			_cppConst.Add(__sbDef.ToString());
		}
		return true;
	}
	
	Dictionary<string, string> _guids; //all GUID extracted from .lib files
	HashSet<string> _guidsAdded = new(); //already declared GUID names, to prevent duplicate declarations
	
	void _InitMaps() {
		//get dll function names extracted from SDK lib files and system dlls
		_funcDllMap = new Dictionary<string, string>();
		string[] a = File.ReadAllLines(@"C:\code\au\Other\Api\DllMap.txt");
		foreach (var s in a) {
			int i = s.IndexOf(' ');
			_funcDllMap.Add(s.Substring(0, i), s.Substring(i + 1));
		}
		
		//get GUIDs extracted from SDK lib files
		_guids = new Dictionary<string, string>();
		foreach (var s in File.ReadAllLines(@"C:\code\au\Other\Api\GuidMap.txt")) {
			//print.it(s);
			int i = s.IndexOf(' ');
			string sn = s.Substring(0, i), sd = s.Substring(i + 1), sOld;
			if (_guids.TryGetValue(sn, out sOld)) {
				if (sd == sOld) continue;
				//print.it(name, sOld, sd);
				//continue;
				//several in SDK, the second ones are correct
				_guids.Remove(sn);
			}
			_guids.Add(sn, sd);
		}
		
		//add enum values for some keyword literals etc
		_enumValues.Add(_TokenFromString("false"), 0);
		_enumValues.Add(_TokenFromString("true"), 1);
		_enumValues.Add(_TokenFromString("nullptr"), 0);
		_enumValues.Add(_TokenFromString("NULL"), 0); //clang ignores #define NULL
	}
	
	//used to recognize forward-declared interfaces
	HashSet<string> _interfaces = new();
	
	void _InitInterfaces() {
		_interfaces.Add("IUnknown");
		_interfaces.Add("IDispatch");
		for (int i = 2; i < _nTokUntilDefUndef; i++) {
			if (_TokIsChar(i, 'u') && _TokIs(i, "uuid")) {
				if (!_TokIsChar(i + 5, ':')) continue; //class or IUnknown
				if (!_TokIs(i - 1, "struct") && !_TokIs(i - 1, "__interface")) continue; //unexpected
				i += 4;
				//print.it(_tok[i]);
				_interfaces.Add(_TokToString(i));
			}
		}
	}
	
	HashSet<string> _csKeywords;
	
	void _InitCsKeywords() {
		//we need only those that are not C/C++ keywords. Also don't need contextual keywords.
		_csKeywords = new HashSet<string>() { "abstract", "as", "base", "byte", "checked", "decimal", "delegate", "event", "explicit", "finally", "fixed", "foreach", "implicit", "in", "interface", "internal", "is", "lock", "null", "object", "out", "override", "params", "readonly", "ref", "sbyte", "sealed", "stackalloc", "string", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort" };
	}
}
