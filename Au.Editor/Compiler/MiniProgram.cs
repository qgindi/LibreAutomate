using System.Runtime.Loader;

namespace LA;

static class MiniProgram { //don't rename. Must be "LA.MiniProgram.Run". Debugger uses it to remove from the stack view.
	[MethodImpl(MethodImplOptions.NoOptimization)]
	[DebuggerHidden, StackTraceHidden]
	public static unsafe int Run(string[] args) {
		//print.qm2.use = true;
		//var p1 = perf.local();
		
		script.role = SRole.MiniProgram;
		folders.Editor = folders.ThisApp;
		
		script.AppModuleInit_(auCompiler: true); //5 ms (3 ms loading AuCpp.dll)
		
		//p1.Next('m');
		//Debug_.PrintLoadedAssemblies(true, true);
		
		string assemblyPath;
		using (script.MiniProgramAndExeProgramStartup_ k = new()) {
			if (!k.Open(true)) return -1;
			var p = k.Mem;
			
			assemblyPath = p->MiniDll;
			var flags = p->miniFlags;
			
			if (!flags.Has(MPFlags_.MTA))
				process.ThisThreadSetComApartment_(ApartmentState.STA);
			
			if (flags.Has(MPFlags_.Console)) {
				Api.AllocConsole();
			} else {
				if (flags.Has(MPFlags_.RedirectConsole)) script.RedirectConsole_();
				//Compiler adds this flag if the script uses System.Console assembly.
				//Else new users would not know how to test code examples with Console.WriteLine found on the internet.
			}
			
			DependencyResolverForMiniProgramAndEditorExtensionScripts_ defRes = default;
			
			if (flags.Has(MPFlags_.RefPaths))
				AssemblyLoadContext.Default.Resolving += (_, an)
					=> defRes.ResolveManaged(null, an);
			
			if (flags.Has(MPFlags_.NativePaths))
				AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, dll)
					=> defRes.ResolveUnmanaged(null, dll);
			
			script.Starting_(p->pidEditor);
			//p1.Next('s');
		}
		
		//p1.Next();
		var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
		Assembly.SetEntryAssembly(asm);
		//info: module initializers run later
		//p1.Next('a');
		
		var entryPoint = asm.EntryPoint;
		string[] epParams = entryPoint.GetParameters().Length != 0 ? args : null;
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
