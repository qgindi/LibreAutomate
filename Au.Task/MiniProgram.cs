using System.Runtime.Loader;

static class MiniProgram {
	[MethodImpl(MethodImplOptions.NoOptimization)]
	//[StackTraceHidden] //ignored for entry point //TODO2: remove MiniProgram.Main from stack traces displayed in LA, where possible.
	static int Main(string[] args) {
		//print.qm2.use = true;
		//var p1 = perf.local();

		script.role = SRole.MiniProgram;

		script.AppModuleInit_(auCompiler: true); //5 ms (3 ms loading AuCpp.dll)

		//p1.Next('m');
		//Debug_.PrintLoadedAssemblies(true, true);

		var b = Convert.FromBase64String(args[0]);
		var a = Serializer_.Deserialize(b); //tested: BinaryReader much slower here

		var flags = (MPFlags_)(int)a[2];
		//p1.Next('d');

		script.s_idMainFile = (uint)(int)a[6];
		script.s_wndEditorMsg = (wnd)(int)a[8];
		script.s_wrPipeName = a[4];

		if (0 != (flags & MPFlags_.FromEditor)) script.testing = true;
		if (0 != (flags & MPFlags_.IsPortable)) ScriptEditor.IsPortable = true;

		folders.Editor = new(folders.ThisApp);
		folders.Workspace = new(a[5]);

		//p1.Next();
		var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath((string)a[1]);
		Assembly.SetEntryAssembly(asm);
		//p1.Next('a');

		DependencyResolverForMiniProgramAndEditorExtensionScripts_ defRes = default;

		if (0 != (flags & MPFlags_.RefPaths))
			AssemblyLoadContext.Default.Resolving += (_, an)
				=> defRes.ResolveManaged(null, an);

		if (0 != (flags & MPFlags_.NativePaths))
			AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, dll)
				=> defRes.ResolveUnmanaged(null, dll);

		if (0 == (flags & MPFlags_.MTA))
			process.ThisThreadSetComApartment_(ApartmentState.STA);

		if (0 != (flags & MPFlags_.Console)) {
			Api.AllocConsole();
		} else {
			if (0 != (flags & MPFlags_.RedirectConsole)) script.RedirectConsole_();
			//Compiler adds this flag if the script uses System.Console assembly.
			//Else new users would not know how to test code examples with Console.WriteLine found on the internet.
		}

		script.Starting_(a[0], a[7]);
		//p1.Next('s');

		var entryPoint = asm.EntryPoint;
		string[] taskArgs = a[3];
		string[] epParams = entryPoint.GetParameters().Length != 0 ? taskArgs ?? [] : null;
		int ret = 0;
		if (entryPoint.ReturnType == typeof(int)) {
			if (epParams != null) {
				var d = entryPoint.CreateDelegate<Func<string[], int>>();
				ret = d(epParams);
			} else {
				var d = entryPoint.CreateDelegate<Func<int>>();
				ret = d();
			}
		} else {
			if (epParams != null) {
				var d = entryPoint.CreateDelegate<Action<string[]>>();
				//p1.NW('d'); //10 ms
				d(epParams);
			} else {
				var d = entryPoint.CreateDelegate<Action>();
				d();
			}
		}

		return ret;
	}
}

//rejected: use dotnet.exe instead.
//	Works, but starts much slower: 50 ms -> 80 ms.
/*
//test code:
string dll = folders.Workspace + @".compiled\76144.dll";
string runtimeconfig = folders.Editor + "Au.Task.runtimeconfig.json";
string deps = folders.Editor + "Au.Task.deps.json";
var cl = $@"exec --runtimeconfig ""{runtimeconfig}"" --depsfile ""{deps}"" ""{dll}"" /a1 /a2";
var r = run.console("dotnet.exe", cl);
*/
