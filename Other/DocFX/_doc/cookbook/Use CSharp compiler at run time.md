# Use C# compiler at run time
Use NuGet package <u title='Paste the underlined text in menu -> Tools -> NuGet'>Microsoft.CodeAnalysis.CSharp</u>. It contains many files, therefore it's better to install it in a separate folder, for example folder "Roslyn".

```csharp
/*/ nuget Roslyn\Microsoft.CodeAnalysis.CSharp; /*/
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Runtime.Loader;
using System.Reflection;
```

Compile and run script.

```csharp
string code = """
using System;
using Au;

foreach (var v in args) print.it(v);
dialog.show("test");
""";

var c = CsScript.Compile(code);
if (c == null) return;

//print.redirectConsoleOutput = true; //need this if the script contains Console.WriteLine and the caller app isn't console
c.Run("command", "line", "arguments");
```

Compile code with a class and call static methods.

```csharp
string code2 = """
using Au;
public class Class1 {
	public static void Print(string s) { print.it(s); }
	public static int Add(int a, int b) { return a + b; }
	public static void F3(out string s, int i = 0) { s = "OUT " + i; }
}
""";

var c2 = CsScript.Compile(code2, library: true);
if (c2 == null) return;

var prt = c2.GetMethod<Action<string>>("Class1", "Print");
prt("TEST");
var add = c2.GetMethod<Func<int, int, int>>("Class1", "Add");
print.it(add(2, 5), add(1000, 1));
var f3 = c2.GetMethod<Delegates.D1>("Class1", "F3");
f3(out var s1); print.it(s1);
```

Compile code with a class, create a class instance as dynamic, and call functions.

```csharp
string code3 = """
using Au;
public class Class1 {
	int _value;
	public Class1() { }
	public Class1(int value) { _value = value; }
	public void Print(string s) { print.it(s); }
	public int GetValue() { return _value; }
	public void F3(out string s, int i = 0) { s = "OUT " + i; }
	public int Prop { get; set; }
}
""";

var c3 = CsScript.Compile(code3, library: true);
if (c3 == null) return;
//var d = c3.CreateInstance("Class1"); //use constructor with 0 paramaters
var d = c3.CreateInstance("Class1", 3); //use constructor with 1 paramater
d.Print("test");
print.it(d.GetValue());
d.F3(out string s2); print.it(s2);
d.Prop = 4; print.it(d.Prop);

/// <summary>
/// Examples of delegates for methods where cannot be used <b>Action</b> or <b>Func</b>, for example with in/ref/out/params/optional parameters.
/// </summary>
class Delegates {
	public delegate void D1(out string s, int i = 0);
}

/// <summary>
/// Compiles and executes C# code at run time.
/// </summary>
class CsScript {
	readonly Assembly _asm;
	
	CsScript(Assembly assembly) {
		_asm = assembly;
	}
	
	/// <summary>
	/// Compiles C# code.
	/// </summary>
	/// <param name="code">
	/// C# code.
	/// In code can be used everything from .NET and Au.dll.
	/// The compiler does not add default using directives.
	/// </param>
	/// <param name="library">The code contains only classes and no entry point.</param>
	/// <returns>A <b>CsScript</b> object containing the compiled assembly. It's an in-memory collectible assembly. If fails to compile, prints errors and returns null.</returns>
	/// <remarks>
	/// Slow when used the first time in current process, because JIT-compiles the Roslyn compiler. Can be 1 or several seconds. Later less than 100 ms (if code is small).
	/// </remarks>
	public static CsScript Compile(string code, bool library = false) {
		var parseOpt = new CSharpParseOptions(LanguageVersion.Preview);
		var tree = CSharpSyntaxTree.ParseText(code, parseOpt);
		var trees = new SyntaxTree[] { tree };
		var compOpt = new CSharpCompilationOptions(library ? OutputKind.DynamicallyLinkedLibrary : OutputKind.WindowsApplication, allowUnsafe: true);
		var compilation = CSharpCompilation.Create("script", trees, s_refs, compOpt);
		var memStream = new MemoryStream();
		var emitResult = compilation.Emit(memStream);
		if (!emitResult.Success) {
			print.it("FAILED TO COMPILE");
			print.it(emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
			return null;
		}
		memStream.Position = 0;
		var alc = new AssemblyLoadContext(null, isCollectible: true);
		return new(alc.LoadFromStream(memStream));
	}
	
	static List<MetadataReference> s_refs = _GetRefs();
	
	/// <summary>
	/// Creates <b>MetadataReference</b> for all .NET assemblies and Au.dll.
	/// </summary>
	static List<MetadataReference> _GetRefs() {
		var r = new List<MetadataReference>();
		var s = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
		foreach (var v in s.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
			if (v.Starts(folders.ThisAppBS, true) && !v.Ends(@"\Au.dll", true)) continue;
			r.Add(MetadataReference.CreateFromFile(v));
		}
		return r;
	}
	
	/// <summary>
	/// Executes the entry point of the script program (top-level statements or Main).
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	/// <returns>If the script returns int, returns it. Else 0.</returns>
	public int Run(params string[] args) {
		var main = _asm.EntryPoint;
		var o = main.Invoke(null, new object[] { args });
		return o is int i ? i : 0;
	}
	
	/// <summary>
	/// Creates a delegate of type <i>T</i> for a public static method.
	/// </summary>
	/// <param name="type">Class name.</param>
	/// <param name="method">Method name.</param>
	/// <exception cref="Exception">Not found type; not found method; ambiguous match (several overloads of the method exist); the method type does not match <i>T</i>.</exception>
	/// <example>
	/// <code><![CDATA[
	/// var c = CsScript.Compile("""{} public class Class1 { public static int Add(int a, int b) { return a + b; } }""");
	/// var add = c.GetMethod<Func<int, int, int>>("Class1", "Add");
	/// print.it(add(2, 5));
	/// ]]></code>
	/// </example>
	public T GetMethod<T>(string type, string method) where T : Delegate {
		var t = _asm.GetType(type) ?? throw new ArgumentException("type not found: " + type);
		var m = t.GetMethod(method) ?? throw new ArgumentException("method not found: " + method);
		return m.CreateDelegate<T>();
	}
	
	/// <summary>
	/// Creates an instance of a type defined in the compiled code.
	/// </summary>
	/// <param name="type">Class name. The type must be public and have a public parameterless constructor or no constructors.</param>
	/// <returns>Because the type is defined only in the compiled code and is unknown in the caller code, returns the instance object as dynamic type. You can call its public functions, but there is no intellisense, and errors are detected at run time.</returns>
	/// <exception cref="Exception">Not found type; can't create instance.</exception>
	public dynamic CreateInstance(string type, params object[] args) {
		var t = _asm.GetType(type) ?? throw new ArgumentException("type not found: " + type);
		return Activator.CreateInstance(t, args);
	}
}
```

