/// Preprocesses Windows SDK headers.
/// The output files then can be used by the converter project.
/// Temporary and output files are in folder "C:\code\au\Other\Api". Links to some of them added in this folder.
/// Uses Clang compiler from LLVM 3.9.1 64-bit.

print.clear();

string clangPath = @"C:\Program Files\LLVM\bin\clang.exe";
string SDK_Include = @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um";
string Shared_Include = @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\shared";
//string CRT_Include = @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\ucrt";
//string VS_CRT_Include=@"C:\Program Files (x86)\Microsoft Visual Studio 14.0\VC\include" //vcruntime.h etc missing in SDK

_Preprocess(true, @"C:\code\au\Other\Api\Api-preprocessed-64.cpp");
_Preprocess(false, @"C:\code\au\Other\Api\Api-preprocessed-32.cpp");


// Preprocesses C++ headers from file "SDK headers.h" and saves in file outFile.
void _Preprocess(bool is64bit, string outFile) {
	//temp files
	string cppFile = @"C:\code\au\Other\Api\Api-headers.cpp";//saved macro "SDK headers" text
	string intermFile = @"C:\code\au\Other\Api\Api-interm.cpp";//first clang invocation output
	
	//save "SDK headers.h" text to cppFile, with some modifications, etc
	_CreateCppFile(cppFile, is64bit);
	
	//run clang to preprocess cppFile
	string cl = $"""
-E -dD -undef
-fms-compatibility -fms-extensions
-fdeclspec -fdollars-in-identifiers
-nobuiltininc -nostdinc++
-Wno-extra-tokens -Wno-comment -Wno-nonportable-include-path -Wno-expansion-to-defined
-I "{SDK_Include}" -I "{Shared_Include}"
-o "{intermFile}"
"{cppFile}"
""";
	cl = cl.Replace("\r\n", " ");
	if (0 != run.console(o => print.it(o), clangPath, cl)) throw new AuException();
	
	//In old version was used this. Now errors. Now no errors without the CRT. When compared output, everything is almost the same, with few non-important changes.
	//-I "{SDK_Include}" -I "{CRT_Include}" -idirafter "{VS_CRT_Include}"
	
	/* some clang command line options
 -emit-ast
 -fdiagnostics-parseable-fixits
                          Print fix-its in machine parseable form
 -fdiagnostics-print-source-range-info
                          Print source range spans in numeric form
 -fdiagnostics-show-note-include-stack
                          Display include stacks for diagnostic notes
  -femit-all-decls        Emit all declarations, even if unused
  -ffreestanding          Assert that the compilation takes place in a freestanding environment
  -fgnu-keywords          Allow GNU-extension keywords regardless of language standard
  -fgnu89-inline          Use the gnu89 inline semantics
  -fms-compatibility-version=<value>
                          Dot-separated value representing the Microsoft compiler version number to report in _MSC_VER (0 = don't define it (default))
  -fmsc-version=<value>   Microsoft compiler version number to report in _MSC_VER (0 = don't define it (default))
  -fno-merge-all-constants
                          Disallow merging of constants
  -fno-show-column        Do not include column number on diagnostics
  -fno-show-source-location
                          Do not include source location information with diagnostics
  -fno-signed-char        Char is unsigned
  -fpack-struct=<value>   Specify the default maximum struct packing alignment
  -H                      Show header includes and nesting depth
  -idirafter <value>      Add directory to AFTER include search path
  -nobuiltininc           Disable builtin #include directories
  -nostdinc++             Disable standard #include directories for the C++ standard library
  -print-search-dirs      Print the paths used for finding libraries and programs
  -P                      Disable linemarker output in -E mode
  -Qunused-arguments      Don't emit warning for unused driver arguments
  -std=<value>            Language standard to compile for
  -stdlib=<value>         C++ standard library to use
  -undef                  undef all system defines
  -W<warning>             Enable the specified warning
  -w                      Suppress all warnings
  -x <language>           Treat subsequent input files as having type <language>
	*/
	
	string s = File.ReadAllText(intermFile);
	
	/* tested:
 clang removes space/tab before and after # in directives.
 clang joins lines ending with \.
 clang uses \n for newlines, except in raw strings.
 clang replaces '#pragma  pack' to '#pragma pack', but does not change spaces after it.
	*/
	
	//remove some #define etc that are added by clang or added here
	if (0 == s.RxReplace(@"(?s)^.+?\R#define AU *\R", "", out s, 1)) throw new AuException();
	
	//remove SAL, CRT etc (use '# n "include file"' added by clang when not using -P)
	s = s.RxReplace("""(?mi)(?>^# \d+ ".+[/\\](?:specstrings\w*|\w*sal|\w+specs|winapifamily|winpackagefamily|stralign|ucrt\\[^\."]+|VC\\\\include\\\\(?!excpt\.h)[^\."]+)\.h")(?s).+?(?=^# \d)""", "");
	
	//remove '# n "include file"' etc that clang adds when not using -P
	if (0 == s.RxReplace(@"(?m)^# \d.+\R", "", out s)) throw new AuException();
	
	//escape raw strings
	s = s.RxReplace(@"R""(.*)\(((?s).*?)\)\1""", m => {
		var s = m[2].Value;
		print.it($"<>info: raw string. Please review because extracting raw strings is unreliable:\r\n<c orange>{s}<>");
		return s.Escape(quote: true);
	});
	
	//remove empty lines to make easier to debug-read
	if (0 == s.RxReplace(@"(?m)^\s*\R", "", out s)) throw new AuException();
	
	//make #pragma pack easier to parse
	if (0 == s.RxReplace(@"(?m)^#pragma[ \t]+pack[ \t]*\(", "`(", out s)) throw new AuException();
	
	//remove #pragma that we don't need
	if (0 == s.RxReplace(@"(?m)^#pragma.+\R", "", out s)) throw new AuException();
	
	//remove thousand separator ' from number literals
	s = s.RxReplace(@"(?<=\d)'(?=\d)", "");
	
	//replace double-word types, to make easier to convert
	s = s.RxReplace(@"\blong\s+long\b", "__int64");
	s = s.RxReplace(@"\blong\s+double\b", "double");
	s = s.RxReplace(@"\bsigned\s+\b(char|short|int|long|__int64|__int8|__int16|__int32)\b", "$1");
	s = s.RxReplace(@"\bunsigned\s+\b(char|short|int|long|__int64|__int8|__int16|__int32)\b", "u$$$1");
	
	//convert DECLARE_HANDLE(X) to IntPtr
	s = s.RxReplace(@"\bstruct\s+(\w+)__\{int\s+unused;\};\s*typedef\s+struct \1__\s*\*\1;", "typedef IntPtr $1;");
	//for HANDLE use IntPtr, not void*
	if (0 == s.RxReplace(@"\btypedef void ?\* ?HANDLE;", "typedef IntPtr HANDLE;", out s, 1)) throw new AuException();
	s = s.RxReplace(@"\btypedef void ?\* ?(\w+_HANDLE);", "typedef IntPtr $1;");
	s = s.RxReplace(@"\btypedef void ?\* ?(H\w+);", "typedef IntPtr $1;");
	//remove HWND typedef, we add it in converter
	if (0 == s.RxReplace(@"\btypedef IntPtr HWND;", " ", out s, 1)) throw new AuException();
	
	//replace inline functions LongToHandle/LongToPtr to IntPtr cast
	s = s.RxReplace(@"(?ms)^__inline\s+void\s*\*\s*(U?LongTo(?:Handle|Ptr))\s*\([^{]+\{[^}]+\}", "#define $1(h) (IntPtr)(h)");
	
	//expand HRESULT_FROM_WIN32
	s = s.RxReplace(@"\bHRESULT_FROM_WIN32\((\w+)\)", "(0x80070000|$1)");
	
	//remove unused SAL annotations (defined as '^"text"; most removed by our #undef/#define)
	s = s.RxReplace(@"\^""\w+""", m => {
		var s = m.Value;
		if (0 == s.Like(false, "^\"_In_*", "^\"_Inout_*", "^\"_Out_*", "^\"_Outptr_*")) {
			if (s.Like("^\"__RPC_*")) {
				var ss = s[8..];
				if (0 != ss.Like(false, "*_in\"", "*_in_*")) return s.Insert(2, "_In_");
				if (0 != ss.Like(false, "*_inout\"", "*_inout_*")) return s.Insert(2, "_Inout_");
				if (0 != ss.Like(false, "*_deref_out\"", "*_deref_out_*")) return s.Insert(2, "_Outptr_");
				if (0 != ss.Like(false, "*_out\"", "*_out_*")) return s.Insert(2, "_Out_");
				if (0 != ss.Like(false, "unique_pointer*", "string*")) return null; //filter garbage to make easier to debug
				goto ge;
			} else if (s.Like("^\"__*")) {
				var ss = s[4..];
				if (ss.Like("out_data_source*")) return null;
				if (0 != ss.Like(false, "in\"", "in_*")) return s.Insert(2, "_In_");
				if (0 != ss.Like(false, "inout\"", "inout_*")) return s.Insert(2, "_Inout_");
				if (0 != ss.Like(false, "out\"", "out_*")) return s.Insert(2, "_Out_");
				if (0 != ss.Like(false, "field_*", "drv_*", "kernel_entry*", "control_entrypoint*")) return null; //filter garbage to make easier to debug
				goto ge;
			} else if (s.Like("^\"_COM_Outptr_*")) {
				return s.Insert(2, "_Outptr_");
			} else if (s.Like("^\"_Deref_*_range_\"")) {
				//used only after _Out_ etc, therefore useless and would be removed later anyway
				return null;
			} else if (0 != s.Like(false, "^\"_Null*", "^\"_Ret*", "^\"_Field_*", "^\"_Reserved_*", "^\"_Success_*", "^\"_Must_*", "^\"_Post_*", "^\"_Pre_*", "^\"_Printf_*", "^\"_Struct_*", "^\"_Check_return_*", "^\"_Function_class_*", "^\"_Frees_ptr_*", "^\"_Analysis_noreturn_*", "^\"_IRQL_requires_*", "^\"_Strict_type_match*", "^\"_Translates_Win32_to_HRESULT_*")) {
				//filter garbage to make easier to debug
				return null;
			} else goto ge;
		}
		return s;
		ge:
		print.it(s);
		return null;
	});
	if (0 == s.RxReplace(@"(\^""\w+"")(?:\s*\^""\w+"")+", "$1", out s)) throw new AuException(); //multiple-to-one (usually the first is what we need)
	
	//manage #define/#undef:
	//	Extract all #define/#undef, remove from s, and add to sa.
	//	Also append to sa '`d$$$NAME VALUE'. The second clang call will unexpand C macros in values.
	var sa = _DefineUndef();
	string _DefineUndef() {
		var rx = new regexp(@"(?m)^#(d|u)(?:efine|ndef)[ \t]+([\w\$]+\b)(.*\R)");
		if (!rx.FindAll(s, out var a)) throw new AuException();
		s = rx.Replace(s);
		var b = new StringBuilder();
		foreach (var m in a) b.Append(m.Value);
		foreach (var m in a) b.AppendFormat("`{0}$$$_{1}{2}", m[1].Value, m[2].Value, m[3].Value);
		return b.ToString();
	}
	
	//remove __declspec and other keywords that we don't need
	s = s.RxReplace(@"\b(?:__unaligned|__w64|_w64|static|volatile|explicit|friend|mutable|__ptr32|__ptr64|constexpr|thread_local|__restrict|restrict|tile_static|__clrcall|__vectorcall)\b", "");
	s = s.RxReplace(@"\balignas\s*\(\s*\w+\s*\)", "");
	int n1, n2;
	n1 = s.RxFindAll(@"\b__declspec\s*\(\s*""").Count(); //print.it(n1); //295584 with SAL, 0 without
	if (n1 > 0 && n1 != s.RxReplace(@"(?s)\b__declspec\s*\(\s*"".*?""\s*\)", "", out s)) throw new AuException();
	n1 = s.RxFindAll(@"\b__declspec\s*\(\s*uuid\b").Count(); //print.it(n1); //713
	if (n1 > 0) { n2 = s.RxReplace(@"\b__declspec\s*\(\s*uuid\s*\(\s*(""[^""]+"")\s*\)\s*\)", "uuid($1)", out s); if (n2 != n1) throw new AuException(); }
	n1 = s.RxFindAll(@"\b__declspec").Count(); //print.it(n1); //5574
	if (n1 > 0) { n2 = s.RxReplace(@"(?s)\b__declspec\s*\(\s*\w+\s*(?:\(\s*(?:\w+|""[^""]*"")\s*\))?\)", "", out s); if (n2 != n1) throw new AuException(); }
	
	//make sure that uuid is not before struct, and remove from others
	s = s.RxReplace(@"\btypedef\s+(uuid\s*\(.+?\))\s*struct\b", "typedef struct $1 ");
	s = s.RxReplace(@"\btypedef\s+uuid\s*\(.+?\)", "typedef ");
	
	//replace 'typedef TYPE [callConv] FUNCTYPE(' to 'typedef TYPE ([callConv]*FUNCTYPE)('
	s = s.RxReplace(@"(\btypedef\s+\w+[*&\s]+)(__?(?:stdcall|cdecl|fastcall|thiscall)\s+)?(\w+)\s*\(", "$1 ($2*$3)(");
	
	//convert 'using X = Y' to 'typedef Y X', also for function typedefs. 0 in SDK.
	//s = s.RxReplace(@"\busing\s+(\w+)\s*=([\s\w,*&]+?);", "typedef $2 $1;");
	//s = s.RxReplace(@"\busing\s+(\w+)\s*=\s*([\s\w,*&]+?)\(([\s\w]*\*\s*)\)\s*\(", "typedef $2($3$1)(");
	
	//add * to 'typedef T(__stdcall X)(...);' etc
	var aFN = new List<string>();
	s = s.RxReplace(@"\btypedef\s+\w+[\s*&]*\((?:_?_stdcall|_?_cdecl)\s+(\w+)\s*\)\s*\(", m => {
		int offs = m.Start;
		int i = m[1].Start - offs, j = m[1].End - offs;
		var s = m.Value;
		aFN.Add(s[i..j]);
		return s.ReplaceAt(i - 1, 1, "*");
	});
	foreach (var v in aFN) s = s.RxReplace(@"(\btypedef\s+" + v + @")\s*\*", "$1 "); //remove * from 'typedef FUNC *LPFUNC;'
	
	//remove 'void' from 'Func(void)'
	s = s.RxReplace(@"\(\s*void\s*\)(?=\s*[;=])", "()");
	
	//convert 'X const *' to 'const X *' etc
	s = s.RxReplace(@"\b(\w+)\s+const\s*(?=\*|\w+\s*\[)", "const $1 ");
	s = s.RxReplace(@"\*\s*const\b", "**");
	
	//remove 'public' from base (used for interface struct)
	s = s.RxReplace(@":\s*public\s+(?=\w)", ":");
	
	//somehow one '_VARIANT_BOOL bool;' not removed
	s = s.RxReplace(@"\b_VARIANT_BOOL bool;", " ");
	
	//remove other converter-unsuported noise
	s = s.RxReplace(@"\btypedef\s+char\s+__C_ASSERT__\b[^;]*;", " ");
	s = s.RxReplace(@"\Rnoexcept\R;", ";");
	s = s.RxReplace(@"\btypedef struct\K const\b", "");
	
	//replace 'inline namespace' to 'namespace inline'
	s = s.RxReplace(@"\binline\s+namespace\b", "namespace inline"); //0 in SDK
	
	//replace 2 __stdcall to 1
	s = s.RxReplace(@"\b__stdcall\s+__stdcall\b", "__stdcall"); //4 in SDK
	
	//resolve identifier conflicts where the same name is defined in two header files
	s = s.RxReplace(@"(?m)^typedef PDH_HLOG HLOG;", "");
	
	//replace 'TYPE __forceinline' to '__forceinline TYPE'
	s = s.RxReplace(@"(?m)LONGLONG\s+__forceinline\b", "__forceinline LONGLONG");
	
	//replace some code that cannot be converted
	s = s.RxReplace(@" rgbIndexId[sizeof(JET_API_PTR)+sizeof(u$long)+sizeof(u$long)]", " rgbIndexId[ 24 ]"); //last member in struct that is only used as pointer parameter
	
	//remove classes, because converter does not convert or skip them
	s = s.RxReplace(@"(?ms)^class PMSIHANDLE\s*\{.+?^\};", "");
	
	//convert property keys
	sa = sa.RxReplace(@"(?m)^`d\$\$\$_INIT_(PKEY_\w+) +\{ *\{ *([^\}]+) *\} *, *(\w+) *\} *$", @"`cp ""internal static PROPERTYKEY $1 = new PROPERTYKEY() { fmtid = new Guid($2), pid = $3 };""");
	s = s.RxReplace(@"(?m)^extern ""C"" const PROPERTYKEY PKEY_\w+;$", "");
	
	//-----------------------
	
	//run clang again on #define/#undef to expand macros in their values
	
	string defFileIn = folders.ThisAppTemp + "sdk def in.cpp";
	string defFileOut = folders.ThisAppTemp + "sdk def out.cpp";
	filesystem.saveText(defFileIn, sa);
	
	cl = $"""
-E -P -undef
-fms-compatibility -fms-extensions
-fdeclspec -fdollars-in-identifiers
-nobuiltininc -nostdinc++
-o "{defFileOut}"
"{defFileIn}"
""";
	cl = cl.Replace("\r\n", " ");
	if (0 != run.console(o => print.it(o), clangPath, cl)) throw new AuException();
	
	sa = File.ReadAllText(defFileOut);
	s += sa;
	
	//-----------------------
	
	//replace known sizeof(string)
	s = s.RxReplace(@"\b\Qsizeof(""://"")\E", "4");
	s = s.RxReplace(@"\b\Qsizeof(L""\\TransactionManager\\"")\E", "42");
	s = s.RxReplace(@"\b\Qsizeof(L""\\Transaction\\"")\E", "28");
	s = s.RxReplace(@"\b" + Regex.Escape(@"sizeof(L""\\Enlistment\\"")"), "26");
	s = s.RxReplace(@"\b\Qsizeof(L""\\ResourceManager\\"")\E", "36");
	
	//replace known ?: operators like '((1 != 0) ? 0x00010000 : 0)'
	s = s.RxReplace(@"\(\(0 != 0\) \? \w+ : (\w+)\)", "($1)");
	s = s.RxReplace(@"\(\(1 != 0\) \? (\w+) : \w+\)", "($1)");
	
	//remove rarely used things not handled by the SDK converter
	s = s.RxReplace(@"        DWORD64 Reserved : 64 - 8;\R", "");
	s = s.RxReplace(@"\b\Qtypedef PVOID (ENCLAVE_TARGET_FUNCTION)(PVOID);[10]typedef ENCLAVE_TARGET_FUNCTION (*PENCLAVE_TARGET_FUNCTION);[10]typedef PENCLAVE_TARGET_FUNCTION LPENCLAVE_TARGET_FUNCTION;\E\R", "");
	
	//fails to convert
	s = s.Replace("typedef PVOID (ENCLAVE_TARGET_FUNCTION)(PVOID);\ntypedef ENCLAVE_TARGET_FUNCTION (*PENCLAVE_TARGET_FUNCTION);\ntypedef PENCLAVE_TARGET_FUNCTION LPENCLAVE_TARGET_FUNCTION;\n", "");
	
	filesystem.saveText(outFile, s);
	
	print.it($"PREPROCESSED {(is64bit ? "64" : "32")}-bit");
}

void _CreateCppFile(string cppFile, bool is64bit) {
	var s = File.ReadAllText(folders.sourceCode() + "SDK headers.h");
	
	if (is64bit) s = "#define USE64BIT\r\n" + s;
	
	var b = new StringBuilder();
	b.AppendLine(s).AppendLine();
	
	//finally undef C macros defined here
	if (!s.RxFindAll(@"(?m)^[ \t]*#[ \t]*define\s+(\w+).*\R", 1, out var a)) throw new AuException();
	foreach (var v in a) b.Append("#undef ").AppendLine(v);
	
	filesystem.saveText(cppFile, b.ToString());
	
	//SAL
	
	string catsal = pathname.getDirectory(cppFile) + @"\Au-sal.h";
	s = """
#define __SPECSTRINGS_STRICT_LEVEL 0 //prevent redefining _Outptr_ etc to something useless
#undef _SA_annotes3
#define _SA_annotes3(n,pp1,pp2,pp3) ^pp1
//only _SA_annotes3 is used
//#undef _SA_annotes1
//#define _SA_annotes1(n,pp1) ^pp1
//#undef _SA_annotes2
//#define _SA_annotes2(n,pp1,pp2) ^pp1

""";
	filesystem.saveText(catsal, s);
	
	string sal = Shared_Include + @"\sal.h";
	s = File.ReadAllText(sal);
	if (!s.Contains("AU")) {
		string incl = $"#ifdef AU\r\n#include \"{catsal}\"\r\n#endif\r\n";
		if (0 == s.RxReplace(@"(?m)^#define _SA_annotes3\(n,pp1,pp2,pp3\)\R", "$0" + incl, out s, 1)) throw new AuException();
		filesystem.saveText(sal, s, backup: true);
	}
	
	string appmodel = SDK_Include + @"\appmodel.h";
	if (is64bit && !filesystem.exists(appmodel)) {
		filesystem.copy(SDK_Include + @"\..\winrt\appmodel.h", appmodel);
		filesystem.copy(SDK_Include + @"\..\winrt\minappmodel.h", SDK_Include + @"\minappmodel.h");
	}
}
