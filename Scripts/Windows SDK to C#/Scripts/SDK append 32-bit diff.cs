/// Compares converter output files (64-bit and 32-bit) and saves in final .cs file with added differences.

print.clear();

string s64 = File.ReadAllText(@"C:\code\au\Other\Api\Api-64.cs");
string s32 = File.ReadAllText(@"C:\code\au\Other\Api\Api-32.cs");

var d = new Dictionary<string, string>();
var d32 = new HashSet<string>();
var b = new StringBuilder();

_ProcessCsFile(false, s64);
_ProcessCsFile(true, s32);

var diff = b.ToString();
diff = diff.RxReplace(@"\b(" + string.Join('|', d32) + @")\b", "$0__32");

string commentsEtc = """
 // 32-BIT
 // Declarations that are different (or exist only) when the process is 32-bit.

""";
s64 = s64[..s64.LastIndexOf('}')] + commentsEtc + diff + "\r\n}\r\n";

filesystem.saveText(@"C:\code\au\Other\Api\Api.cs", s64);
print.it($"DONE, {d.Count} declarations, size = {s64.Length / 1024} KB, API32 size = {diff.Length / 1024} KB");


void _ProcessCsFile(bool is32bit, string s) {
	int iKind = 0;
	string sType = _GetOfKind(@"(?ms)^// TYPE\R(.+?)^\R//");
	string sFunc = _GetOfKind(@"(?ms)^// FUNCTION\R(.+?)^\R//");
	string sInterf = _GetOfKind(@"(?ms)^// INTERFACE\R(.+?)^\R//");
	string sVar = _GetOfKind(@"(?ms)^// VARIABLE\R(.+?)^\R//");
	string sConst = _GetOfKind(@"(?ms)^// CONSTANT\R(.+?)^\R//");
	string sOther = _GetOfKind(@"(?ms)^// CANNOT CONVERT\R(.+?)^\R\R\}");
	
	string _GetOfKind(string rx) {
		if (!s.RxMatch(rx, 1, out RXGroup g, 0, iKind..)) throw new AuException();
		iKind = g.End;
		return g.Value;
	}
	
	regexp rxType = new(@"(?ms)^\r\n(?:\[[^\r\n]+\r\n)*internal (?:struct|enum|interface|class) (\w+)[^\r\n\{]+\{(?:\}$|.+?^\})"),
		rxFunc = new(@"(?m)^\r\n(?:\[[^\r\n]+\r\n)*internal (?:static extern|delegate) \w+\** (\w+)\(.+;$"),
		rxVar = new(@"(?m)^internal static \w+ (\w+) =.+;$"),
		rxConst = new(@"(?m)^internal (?:const|readonly) \w+ (\w+) =.+;$");
	
	//TYPE
	if (!rxType.FindAll(sType, out var a)) throw new AuException(); //struct/enum
	_AddToDict(is32bit, "struct/enum");
	if (!rxFunc.FindAll(sType, out a)) throw new AuException(); //delegate
	_AddToDict(is32bit, "delegate");
	//FUNCTION
	if (!rxFunc.FindAll(sFunc, out a)) throw new AuException();
	_AddToDict(is32bit, "function");
	//INTERFACE
	if (!rxType.FindAll(sInterf, out a)) throw new AuException();
	_AddToDict(is32bit, "interface");
	//VARIABLE
	if (!rxVar.FindAll(sVar, out a)) throw new AuException();
	_AddToDict(is32bit, "variable");
	if (b.Length > 0) b.AppendLine("\r\n");
	//CONST
	if (!rxConst.FindAll(sConst, out a)) throw new AuException();
	_AddToDict(is32bit, "const");
	if (b.Length > 0) b.AppendLine();
	//CANNOT CONVERT
	if (!rxConst.FindAll(sOther, out a)) throw new AuException();
	_AddToDict(is32bit, "other");
	
	
	void _AddToDict(bool is32bit, string debugType) {
		foreach (var m in a) {
			string name = m[1].Value, value = m.Value;
			if (!is32bit) {
				//print.it(debugType, name);
				if (!d.TryAdd(name, value)) print.it($"<><c red>duplicate: {name} ({debugType}, 64-bit)<>"); //0 is SDK
			} else {
				if (d.TryGetValue(name, out var v) && v == value) continue;
				
				print.it($"<>--------------------------------\r\n64-bit:  {v}\r\n<c blue>32-bit:  {value}<>");
				
				b.AppendLine(value);
				if (!d32.Add(name)) print.it($"<><c red>duplicate: {name} ({debugType}, 32-bit)<>"); //0 is SDK
			}
		}
	}
}
