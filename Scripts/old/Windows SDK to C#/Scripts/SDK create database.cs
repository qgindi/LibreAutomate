/*/ testInternal Au,Au.Editor; r Au.Editor.dll; /*/

//string dbFile = folders.ThisAppBS + @"winapi.db";
string dbFile = @"C:\code\au\Other\Api\winapi.db";
filesystem.delete(dbFile);

string s = File.ReadAllText(@"C:\code\au\Other\Api\Api.cs");

using var d = new sqlite(dbFile);
using var trans = d.Transaction();
d.Execute("CREATE TABLE api (name TEXT, code TEXT, kind INTEGER)"); //note: no PRIMARY KEY. Don't need index.
using var statInsert = d.Statement("INSERT INTO api VALUES (?, ?, ?)");

string rxType = @"(?ms)^(?:\[\N+\R)*internal (struct|enum|interface|class) (\w+)[^\r\n\{]+\{(?: *\}$|.+?^\})";
string rxFunc = @"(?m)^(?:\[\N+\R)*internal (static extern|delegate) \w+\** (\w+)\(.+;$";
string rxVarConst = @"(?m)^internal (const|readonly|static) \w+ (\w+) =.+;$";

foreach (var m in s.RxFindAll(rxType)) _Add(m);
foreach (var m in s.RxFindAll(rxFunc)) _Add(m);
foreach (var m in s.RxFindAll(rxVarConst)) _Add(m);

void _Add(RXMatch m) {
	statInsert.Bind(1, m[2].Value);
	var code = m.Value;
	Debug.Assert(!code.Contains("\r\n\r")); //if contains newlines, the regex is bad, spans multiple definitions
	statInsert.Bind(2, code);
	
	CiItemKind kind = m[1].Value switch {
		"struct" => CiItemKind.Structure,
		"enum" => CiItemKind.Enum,
		"interface" => CiItemKind.Interface,
		"class" => CiItemKind.Class,
		"static extern" => CiItemKind.Method,
		"delegate" => CiItemKind.Delegate,
		"const" => CiItemKind.Constant,
		"static" or "readonly" => CiItemKind.Field,
		_ => CiItemKind.None
	};
	Debug_.PrintIf(kind == CiItemKind.None, m[1].Value);
	statInsert.Bind(3, (int)kind);
	
	statInsert.Step();
	statInsert.Reset();
}

trans.Commit();
d.Execute("VACUUM");

print.it("DONE");
