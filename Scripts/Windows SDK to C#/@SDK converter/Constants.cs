namespace SdkConverter;

unsafe partial class Converter {
	void _DefineUndef() {
		char* s = T(++_i);
		char c = *s; //was like `d$$$_REALNAME, now s is without `
		
		if (c == 'c') { //`cx "C# code converted by the preprocessor script", where x tells what it is
			if (!_TokIsChar(++_i, '\x2')) _Err(_i, "unexpected 1");
			_tok[_i] = new _Token(T(_i) + 1, _tok[_i].len - 2);
			if (s[1] == 'p') {
				_sbVar.AppendLine(_TokToString(_i));
			} else _Err(_i - 1, "unexpected 2");
			return;
		}
		
		s += 5; int lenName = _tok[_i].len - 5; //skip prefix 'd$$$_' that was added to avoid unexpanding names
		_tok[_i] = new _Token(s, lenName);
		int iName = _i;
		
		//is function-style?
		char* s2 = T(++_i);
		bool isFunc = c == 'd' && *s2 == '(' && s2 == s + lenName;
		
		//find value
		int iValue = _i, iParamOrValue = _i;
		if (isFunc) iValue = _SkipEnclosed(_i) + 1; //name(parameters)[ value]
		
		//find next line
		int iNext = iValue;
		for (; ; iNext++) {
			char k = *T(iNext);
			if (k == '`' || k == '\x0') break;
		}
		
		string name = new string(s, 0, lenName);
		if (c == 'u') { //#undef
			if (!_defineConst.Remove(name) && !_defineOther.Remove(name)) _defineW.Remove(name);
			//print.it($"#undef {name}");
		} else if (iValue < iNext) { //preprocessor removes some #define values, it's ok
			if (isFunc) { //info: for func-style get parameters as part of value
				__DefineAddToOther(iName, name, _TokToString(iParamOrValue, iNext));
			} else if (*s2 == '\x2') { //ANSI string constant, when tokenizing replaced the first '"' to '\x2'
				*s2 = '"';
				__DefineAddToOther(iName, name, " " + _TokToString(iParamOrValue, iNext) + " //ANSI string");
			} else {
				_ExpressionResult r = _Expression(iParamOrValue, iNext, name);
				if (r.typeS == null) {
					bool isFuncWA = (iNext - iValue == 1) && __DefineWA(name, r.valueS);
					if (!isFuncWA) {
						//don't add '#define NAME1 NAME2'
						if (iNext - iValue == 1 && _TokIsIdent(iValue)) {
							//print.it(name, r.valueS);
						} else {
							//print.it(name, iNext-iValue);
							__DefineAddToOther(iName, name, " " + r.valueS);
						}
					}
				} else {
					__sbDef.Clear();
					if (name.Starts("WM_") && r.typeS == "uint") r.typeS = "int";
					__sbDef.AppendFormat("internal {2} {3} {0} = {1};", name, r.valueS, r.notConst ? "readonly" : "const", r.typeS);
					_defineConst[name] = __sbDef.ToString();
				}
			}
		}
		
		_i = iNext - 1;
	}
	
	void __DefineAddToOther(int iName, string name, string value) {
		//remove those that match other identifiers
		if (_SymbolExists(iName, false) || _func.ContainsKey(name)) {
			//print.it(name);
			return;
		}
		
		_defineOther[name] = value;
	}
	
	StringBuilder __sbDef = new StringBuilder();
	
	bool __DefineWA(string name, string value) {
		int suffixLen = 0;
		if (value.Length == name.Length + 1 && value.Ends("W")) suffixLen = 1;
		else if (value.Length == name.Length + 2 && value.Ends("_W")) suffixLen = 2; //some struct
		if (!(suffixLen > 0 && value.Starts(name))) {
			//print.it($"<><c 0xff>{name}    {value}</c>");
			return false;
		}
		
		string def;
		if (suffixLen == 1 && _func.TryGetValue(value, out def)) {
			_func.Remove(name + "A");
			//most are FuncA+FuncW, but some Func/FuncW and even Func/FuncA/FuncW. Some just FuncW.
			if (_func.Remove(name)) {
				//print.it(1, def); //6 in SDK
			}
			
			def = def.Replace("W(", "(");
			
			if (!def.Contains(", EntryPoint=\"")) {
				def = def.Replace(")]", ", EntryPoint=\"" + value + "\")]");
			}
			
			_func[value] = def;
			
			//print.it($"<><c 0xff0000>{name}    {value}</c>");
			//print.it(def);
			return true;
		} else {
			_Symbol x;
			if (_ns[0].sym.TryGetValue(_TokenFromString(value), out x)) {
				var t = x as _Struct;
				if (t != null || x is _Callback) {
					//if(x is _Struct) print.it($"<><c 0xff0000>{name}    {value}</c>");
					//else print.it($"<><c 0x8000>{name}    {value}</c>");
					int v = (t == null) ? 2 : (t.isInterface ? 1 : 0);
					if (suffixLen == 2) v |= 0x10000;
					_defineW[name] = v; //later will replace all STRUCTW to STRUCT in the final string of struct/func/delegate/interface
					return true;
				}
				//else if(x is _Typedef) {
				//	print.it($"<><c 0x80>{name}    {value}</c>");
				//} else {
				//	print.it($"<><c 0xFF>{name}    {value}</c>"); //0
				//}
				
			} else {
				//print.it($"<><c 0xff>{name}    {value}</c>");
				
			}
		}
		
		return false;
	}
	
	void _ConstantsFinally(StreamWriter writer) {
		//'#define' constants
		foreach (var v in _defineConst) {
			//if string constant name ends with "W", remove this if non-W version exists, and remove A version
			string s = v.Value;
			if (s.Ends("\";") && v.Key.Ends("W")) {
				//print.it($"{v.Key} = {s}");
				string k = v.Key.Remove(v.Key.Length - 1); //name without "W"
				
				//remove A version from _defineOther
				if (_defineOther.Remove(k + "A")) {
					//print.it($"removed A version: {v.Key} = {s}");
				}
				//remove this W version if non-W version exists
				string s2;
				if (_defineConst.TryGetValue(k, out s2) && s2.Length == s.Length - 1) {
					int i = s.Find("W = ");
					if (s2 == s.Remove(i, 1)) {
						//print.it($"removed W version because non-W exists: {v.Key} = {s}");
						continue;
					}
				}
			}
			writer.WriteLine(s);
		}
		
		//'const' constants
		foreach (var con in _cppConst) { writer.WriteLine(con); }
		
		//'#define' function-style macros and other macros that cannot convert to C#
		writer.Write("\r\n// CANNOT CONVERT\r\n\r\n");
		foreach (var v in _defineOther) {
			//if(v.Value.Starts(" \"")) print.it($"<><c 0xff>{v.Key} = {v.Value}</c>"); //11 in SDK (more removed by the above code)
			
			writer.Write("internal const string {0} = null; //#define {0}{1};\r\n\r\n", v.Key, v.Value);
		}
	}
}
