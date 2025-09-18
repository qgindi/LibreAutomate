using System.Runtime.Loader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace UnsafeTools;

static class Scripting {
	/// <summary>
	/// Compiles C# code. Optionally loads in-memory assembly and gets MethodInfo of the first static method for executing.
	/// Returns false if there are errors in code.
	/// </summary>
	/// <param name="code">C# code.</param>
	/// <param name="r">Receives results when compiled successfully.</param>
	/// <param name="addUsings">Add usings Au, Au.Types, System, generic, LINQ.</param>
	/// <param name="addGlobalCs">Add "global.cs" to the compilation if exists. It includes [module: DefaultCharSet(CharSet.Unicode)].</param>
	/// <param name="wrapInClass">Wrap code in public class __script__.</param>
	/// <param name="dll">Create dll, ie without entry method.</param>
	/// <param name="load">If not null, load in-memory assembly and get MethodInfo of the first static method for executing from public class <i>load</i>. If "", gets entry method (<i>dll</i> must be false).</param>
	/// <remarks>
	/// Also adds default references.
	/// 
	/// Function's code does not throw exceptions, but the CodeAnalysis API may throw, although undocumented and never noticed.
	/// 
	/// Thread-safe.
	/// </remarks>
	public static bool Compile(string code, out Result r, bool addUsings, bool addGlobalCs, bool wrapInClass, bool dll, string load = "__script__") {
		if (addUsings || wrapInClass) {
			var sb = new StringBuilder();
			if (addUsings) sb.AppendLine("using Au; using Au.Types; using System; using System.Collections.Generic; using System.Linq;");
			if (wrapInClass) sb.AppendLine("public class __script__ {");
			if (!code.Contains("#line ")) sb.AppendLine("#line 1");
			sb.AppendLine(code);
			if (wrapInClass) sb.AppendLine("}");
			code = sb.ToString();
			//print.it(code);
		}
		
		var parseOpt = new CSharpParseOptions(LanguageVersion.Preview);
		
		SyntaxTree treeGlobal = null;
		if (addGlobalCs) {
			if (WndCopyData.SendReceive<char>(ScriptEditor.WndMsg_, 16, "global.cs", out string gcode)) {
				treeGlobal = CSharpSyntaxTree.ParseText(gcode, parseOpt);
			}
			//TODO3: also recursively add files etc specified in meta c, r, etc.
			//	Now it is not important, because this func used only in "find UI object" tools, and global.cs can be useful only in 'also' field.
		}
		
		var tree = CSharpSyntaxTree.ParseText(code, parseOpt);
		var trees = treeGlobal != null ? new SyntaxTree[] { treeGlobal, tree } : new SyntaxTree[] { tree };
		var compOpt = new CSharpCompilationOptions(dll ? OutputKind.DynamicallyLinkedLibrary : OutputKind.WindowsApplication, allowUnsafe: true);
		var compilation = CSharpCompilation.Create("script", trees, s_refs, compOpt);
		var memStream = new MemoryStream(4000 + code.Length * 2);
		var emitResult = compilation.Emit(memStream);
		
		r = new Result();
		if (!emitResult.Success) {
			r.errors = string.Join("\r\n", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
			return false;
		}
		
		memStream.Position = 0;
		if (load != null) {
			var alc = new AssemblyLoadContext(null, isCollectible: true);
			r.assembly = alc.LoadFromStream(memStream);
			//print.it(AppDomain.CurrentDomain.GetAssemblies().Where(o => o.GetName().Name == "script").Count());
			
			if (load.NE()) r.method = r.assembly.EntryPoint;
			else r.method = r.assembly.GetType(load).GetMethods(BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)[0];
		} else {
			r.stream = memStream;
		}
		return true;
	}
	
	/// <summary>
	/// Creates <b>MetadataReference</b> for all .NET assemblies and Au.dll.
	/// </summary>
	static List<MetadataReference> _GetRefs() {
		var r = new List<MetadataReference>();
		var rdb = folders.ThisAppBS + "ref.db";
		using var db = new sqlite(rdb, SLFlags.SQLITE_OPEN_READONLY);
		using var stat = db.Statement("SELECT * FROM ref");
		while (stat.Step()) r.Add(MetadataReference.CreateFromImage(stat.GetArray<byte>(1), filePath: stat.GetText(0)));
		r.Add(MetadataReference.CreateFromFile(folders.ThisAppBS + "Au.dll"));
		return r;
	}
	static List<MetadataReference> s_refs = _GetRefs();
	
	public class Result {
		/// <summary>
		/// Receives errors when fails to compile.
		/// </summary>
		public string errors;
		
		/// <summary>
		/// When load is false, receives assembly bytes in stream (position=0).
		/// </summary>
		public MemoryStream stream;
		
		/// <summary>
		/// When load is true, receives loaded assembly.
		/// </summary>
		public Assembly assembly;
		
		/// <summary>
		/// When load is true, receives MethodInfo of the first static method for executing.
		/// </summary>
		public MethodInfo method;
	}
}
