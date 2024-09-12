/// For Windows API declarations LA uses data from:
/// 	Source: https://github.com/microsoft/win32metadata
/// 	Data: https://www.nuget.org/packages/Microsoft.Windows.SDK.Win32Metadata/
/// LA cannot use the .winmd file directly. This script converte it to winapi.db file that will be used by LA.

//#define SMALL

script.setup(debug: true);
print.clear();

HashSet<string> onlyNamespaces = null;
#if SMALL //for testing, convert only some apis, to make smaller and faster
onlyNamespaces = [
	"Foundation",
	"Graphics.Dwm",
	"Graphics.Gdi",
	"Security",
	"System.Com",
	"System.Com.StructuredStorage",
	"System.Console",
	"System.Environment",
	"System.IO",
	"System.Kernel",
	"System.LibraryLoader",
	"System.Memory",
	"System.Ole",
	"System.ProcessStatus",
	"System.Registry",
	"System.Services",
	"System.SystemInformation",
	"System.SystemServices",
	"System.TaskScheduler",
	"System.Threading",
	"System.Variant",
	"System.WinRT",
	"Storage.FileSystem",
	"UI.Accessibility",
	"UI.Controls",
	"UI.Controls.Dialogs",
	"UI.HiDpi",
	"UI.Input",
	"UI.Input.KeyboardAndMouse",
	"UI.Shell",
	"UI.Shell.Common",
	"UI.Shell.PropertiesSystem",
	"UI.WindowsAndMessaging",
	];
#endif

var c = new WinApiConverter();
//perf.first();
c.Convert(onlyNamespaces);
//perf.nw();
_SavePreviewText();
_SaveDatabase();
print.scrollToTop();

void _SavePreviewText() {
	regexp rx1 = new(@"(?m)^");
	
	using var w = File.CreateText(folders.Workspace + @"files\winapi converter results.cs");
	w.WriteLine("/// Created by \"WinAPI converter\" script. For preview/debug only. Delete when no longer needed.\r\n\r\n/*/ noWarnings CS0649,CS8500,CS0618; /*/\r\nunsafe class api : NativeApi {\r\n//.");
	
	bool prevMultiline = false;
	foreach (var (k, v) in c.Result) {
		bool multiline = v.Contains('\n');
		if (multiline != prevMultiline) w.WriteLine(multiline ? "//.." : "//.");
		if (multiline || prevMultiline) w.WriteLine();
		prevMultiline = multiline;
		
		var s = rx1.Replace(v, "\t");
		w.WriteLine(s);
	}
	
	w.WriteLine("}");
	
	//print.it("SAVED");
}

void _SaveDatabase() {
	regexp rx = new(@"(?m)^internal \K(struct|enum|interface|class|static extern|delegate|const|static|record)\b");
	var a = c.Result.Select(o => new _NCK(o.Key, o.Value, _Kind(o.Value))).ToList();
#if !SMALL
	_AddOld(a);
#endif
	
	string dbFile = folders.ThisAppBS + @"winapi.db";
	filesystem.delete(dbFile);
	
	using var d = new sqlite(dbFile);
	using var trans = d.Transaction();
	d.Execute("CREATE TABLE api (name TEXT, code TEXT, kind INTEGER)"); //note: no PRIMARY KEY. Don't need index.
	using var stat = d.Statement("INSERT INTO api VALUES (?, ?, ?)");
	
	foreach (var (name, code, kind) in a.OrderBy(o => o.kind).ThenBy(o => o.name, StringComparer.OrdinalIgnoreCase)) {
		//print.it(kind, name);
		stat.Bind(1, name);
		stat.Bind(2, code);
		stat.Bind(3, (int)kind);
		stat.Step();
		stat.Reset();
	}
	
	trans.Commit();
	d.Execute("VACUUM");
	
	print.it("DONE");
	
	CiItemKind _Kind(string code) {
		if (!rx.Match(code, 0, out string sk)) throw null;
		return sk switch {
			"struct" or "record" => CiItemKind.Structure,
			"enum" => CiItemKind.Enum,
			"interface" => CiItemKind.Interface,
			"class" => CiItemKind.Class,
			"static extern" => CiItemKind.Method,
			"delegate" => CiItemKind.Delegate,
			"const" => CiItemKind.Constant,
			"static" => CiItemKind.Field,
			_ => throw null
		};
	}
}

//From the old database adds APIs that are missing in winmd. Also replaces come incorrect declarations.
void _AddOld(List<_NCK> a) {
	var dNew = a.ToDictionary(o => (name: o.name, kind: o.kind), o => o.code);
	var aOld = _Load(folders.Editor + @"..\Other\Api\winapi.db", true);
	
	foreach (var nck in aOld) {
		var (name, code, kind) = nck;
		if (_GetCode(name, out var s)) {
			if (kind == CiItemKind.Constant && code != s) {
				var st = s.AsSpan(15..18);
				if (st is "int" or "uin" or "sho" or "ush" or "byt" or "sby") {
					int vOld = _ConstIntValue(code), vNew = _ConstIntValue(s);
					if (vOld != vNew) {
						//print.it($"<><bc greenyellow>{name}<>\r\n{code}\r\n{s}");
						if (name is "VARIANT_TRUE" || code.Contains("= null;")) continue; //else error in winmd; mostly because it uses wrong preprocessor symbols, eg for oldest Windows versions or 32-bit.
						if (vOld < 0 && s.Starts("internal const int ") && code.Starts("internal const uint ")) code = code.RxReplace(@"\buint (\w+) = \w+;$", $"int $1 = unchecked((int)0x{vOld:X});");
						int i = a.FindIndex(o => o.name == name);
						a[i] = a[i] with { code = code };
					}
				} else if (st is "lon" or "ulo") { //`uint` in old
					//print.it($"<><bc greenyellow>{name}<>\r\n{code}\r\n{s}");
				} else if (st is "dou" or "flo") {
					//print.it($"<><bc greenyellow>{name}<>\r\n{code}\r\n{s}");
				} else if (st is "str") {
					//print.it($"<><bc greenyellow>{name}<>\r\n{code}\r\n{s}");
				} else {
					print.it($"<><bc greenyellow>{name}<>\r\n{code}\r\n{s}");
				}
			}
			static int _ConstIntValue(string s) {
				int i = s.IndexOf('=') + 1;
				if (s[i] == ' ') i++;
				if (s[i] is '"' or '\'') return 0;
				if (s.Eq(i, "unchecked((int)")) i += 15;
				//if (!s.ToInt(out long r, i)) print.it(s[i..]);
				s.ToInt(out int r, i);
				return r;
			}
			continue;
		}
		if (_AltName()) continue;
		a.Add(nck);
		
		bool _Cont(string s) => dNew.ContainsKey((s, kind));
		bool _ContK(string s, CiItemKind kind) => dNew.ContainsKey((s, kind));
		bool _GetCode(string s, out string code) => dNew.TryGetValue((s, kind), out code);
		//bool _GetCodeK(string s, CiItemKind kind, out string code) => dNew.TryGetValue((s, kind), out code);
		
		bool _AltName() {
			if (name.Ends("__32")) {
				if (_Cont(name.Insert(^4, "W"))) return true;
				if (kind is CiItemKind.Constant) {
					if (_GetCode(name[..^4], out var s1)) {
						if (s1 == code.RxReplace(@"__32\b", "", 1)) return true;
						//print.it($"<>{name}, old32='<c gray>{code}<>', new='<c green>{s1}<>'");
					} //else print.it(name);
				} else {
					if (kind is CiItemKind.Delegate && name is "FARPROC__32" or "NEARPROC__32" or "PROC__32") return true;
					//print.it(kind, name);
				}
			} else {
				if ((int)kind <= 5) { //types, methods
					if (_Cont(name + "W") || _Cont(name + "_W")) return true;
					else if (_A()) return true;
					else if (kind == CiItemKind.Method && _W()) return true;
					else if (kind == CiItemKind.Structure) {
						if (name is "GUID" or "FILE" or "POINT" or "POINTL" or "RECT" or "RECTL" or "SIZE") return true;
					} else if (kind is CiItemKind.Delegate) {
						if (name is "FARPROC" or "NEARPROC" or "PROC") return true;
						if (_Cont("P" + name) || _Cont("LP" + name)) return true;
					}
					//if (kind == CiItemKind.Structure) print.it(name);
					//if (kind == CiItemKind.Delegate) print.it(name);
					//if (kind is CiItemKind.Class or CiItemKind.Interface) print.it(name);
					//if (kind == CiItemKind.Method) print.it(name);
				} else if (kind == CiItemKind.Field) {
					if (_TypeGuid("CLSID_", CiItemKind.Class)) return true;
					if (_TypeGuid("IID_", CiItemKind.Interface)) return true;
					if (_TypeGuid("DIID_", CiItemKind.Interface)) return true;
					//print.it(name);
					
					bool _TypeGuid(string prefix, CiItemKind kind) => name.Starts(prefix) && name[prefix.Length..] is var s && (_ContK(s, kind) || _ContK(s + "W", kind));
				} else { //constant
					if (_A()) return true;
					if (_Cont(name + "W")) return true;
					//print.it(name, code);
					
					//In winmd missing all #define where the value is not a simple number/string/constant. Why they didn't calculate eg `(A | B)` or `(TEXT("text"))`?
				}
				
				bool _A() => name.Ends('A') && (_Cont(name.ReplaceAt(^1.., "W")) || _Cont(name[..^1]));
				bool _W() => name.Ends('W') && _Cont(name[..^1]);
			}
			
			return false;
		}
	}
	
	static _NCK[] _Load(string file, bool old) {
		var a = new List<_NCK>();
		using var db = new sqlite(file, SLFlags.SQLITE_OPEN_READONLY);
		using var stat = db.Statement("SELECT name, code, kind FROM api");
		while (stat.Step()) {
			string name = stat.GetText(0);
			string code = stat.GetText(1);
			var kind = (CiItemKind)stat.GetInt(2);
			a.Add(new(name, code, kind));
		}
		return a.OrderBy(o => o.kind).ThenBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToArray();
	}
}

//Copied from LA project.
enum CiItemKind : sbyte {
	//types
	Class, Structure, Interface, Enum, Delegate,
	//functions, events
	Method, ExtensionMethod, Property, Operator, Event,
	//data
	Field, LocalVariable, Constant, EnumMember,
	//other
	Namespace, Keyword, Label, Snippet, TypeParameter,
	//not in autocomplete. Not in CiUtil.ItemKindNames.
	LocalMethod, Region,
	None
}

record _NCK(string name, string code, CiItemKind kind);
