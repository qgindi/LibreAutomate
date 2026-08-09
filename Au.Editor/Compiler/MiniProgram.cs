using System.Runtime.Loader;

static class MiniProgram {
	[MethodImpl(MethodImplOptions.NoOptimization)]
	//[StackTraceHidden] //ignored for entry point //TODO2: remove MiniProgram.Main from stack traces displayed in LA, where possible.
	[StackTraceHidden]
	public static unsafe int Run(string[] args) {
		//print.qm2.use = true;
		//var p1 = perf.local();

		script.role = SRole.MiniProgram;

		script.AppModuleInit_(auCompiler: true); //5 ms (3 ms loading AuCpp.dll)

		//p1.Next('m');
		//Debug_.PrintLoadedAssemblies(true, true);

		string assemblyPath;
		MPFlags_ flags;
		{
			if (!SharedMemory_.Mapping.TryOpenExisting("Au.SM.miniProgram", out var sm)) return -1;
			var p = (MiniProgramAndExeProgramStartupSharedMemoryData_*)sm.Mem;
			var mp = (MiniProgramStartupSharedMemoryData_*)(p + 1);

			flags = (MPFlags_)p->flags;
			if (flags.Has(MPFlags_.FromEditor)) script.testing = true;
			if (flags.Has(MPFlags_.IsPortable)) ScriptEditor.IsPortable = true;

			script.s_idMainFile = p->idMainFile;
			script.s_wndEditorMsg = (wnd)p->hwndMsg;
			script.s_wrPipeName = p->pipe;
			folders.Workspace = new(p->workspace);
			folders.Editor = new(folders.ThisApp);

			if (!flags.Has(MPFlags_.MTA))
				process.ThisThreadSetComApartment_(ApartmentState.STA);

			if (flags.Has(MPFlags_.Console)) {
				Api.AllocConsole();
			} else {
				if (flags.Has(MPFlags_.RedirectConsole)) script.RedirectConsole_();
				//Compiler adds this flag if the script uses System.Console assembly.
				//Else new users would not know how to test code examples with Console.WriteLine found on the internet.
			}

			script.Starting_(mp->scriptName, p->pidEditor);
			//p1.Next('s');

			assemblyPath = mp->assemblyPath;

			sm.Dispose();

			var hevent = Api.OpenEvent(Api.EVENT_MODIFY_STATE, false, "Au.event.taskStart");
			if (!Api.SetEvent(hevent)) return -2;
			Api.CloseHandle(hevent);
		}

		DependencyResolverForMiniProgramAndEditorExtensionScripts_ defRes = default;

		if (flags.Has(MPFlags_.RefPaths))
			AssemblyLoadContext.Default.Resolving += (_, an)
				=> defRes.ResolveManaged(null, an);

		if (flags.Has(MPFlags_.NativePaths))
			AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, dll)
				=> defRes.ResolveUnmanaged(null, dll);

		//p1.Next();
#if !true
		return script.RunMiniProgram_(assemblyPath, args);
#else //FP
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
#endif
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
