//PROBLEM: slow startup.
//A minimal script starts in 70-100 ms cold, 40 hot (old PC, tested long time ago). Now on new PC starts in 37 ms.
//Workaround for role miniProgram:
//	Preload task process. Let it wait for next task. While waiting, it also can JIT etc.
//	Then starts in 12/4 ms (cold/hot). With script.setup 15/5. That was an old test, now should be slower. Now on new PC 10/6 and 12/8.
//	Except first time. Also not faster if several scripts are started without a delay. Never mind.
//	This is implemented in this class and in Au.AppHost (just ~10 code lines added in 1 place).
//Not using this workaround since v1.1, unless meta startFaster true (undocumented). Because:
//	Sometimes something does not work well (see problems below).
//	Since this was invented, .NET process startup became faster (faster JIT etc). Also computers faster. The delay now is barely noticeable.
//FUTURE: if this workaround is sometimes useful, make meta startFaster public. Else remove the preloading code.

//PROBLEM when preloaded: miniProgram windows start inactive, behind one or more windows. Unless they activate self, like dialog.
//	It does not depend on the foreground lock setting/API. The setting/API just enable SetForegroundWindow, but most windows don't call it.
//	Workaround: use CBT hook. It receives HCBT_ACTIVATE even when the window does not become the foreground window.
//		On HCBT_ACTIVATE, async-call SetForegroundWindow. Also, editor calls AllowSetForegroundWindow before starting task.

//PROBLEM when preloaded: when miniProgram starts a console program, occasionally its window is inactive, although on top of other windows.
//	To reproduce: run miniProgram script: `run.it("cmd.exe", flags: RFlags.InheritAdmin)`. If starts active, wait at least 30 s and run again.
//	Never noticed it on Windows 10, only on Windows 11.
//	Workaround: after `run.it` wait a while, eg `1.s();`, because it happens only if this process exits immediately.

//PROBLEM when preloaded: inherits old environment variables.

//PROBLEM when preloaded: the preloaded processes may be confusing, even for me sometimes.

//PROBLEM: although Main() starts fast, but the process ends slowly, because of .NET.
//	Eg if starting an empty script every <50 ms, sometimes cannot start.

//Smaller problem: .NET creates many threads. No workaround.

/*
//To test task startup speed, use script "task startup speed.cs":

300.ms(); //give time to preload new task process
for (int i = 0; i < 5; i++) {
//	perf.cpu();
	var t=perf.ms.ToString();
	script.run(@"miniProgram.cs", t);
//	script.run(@"exeProgram.cs", t);
	600.ms(); //give time for the process to exit
}

//miniProgram.cs and exeProgram.cs:

print.it(perf.ms-Int64.Parse(args[0]));
*/

using System.Runtime.Loader;

namespace Au.More;

/// <summary>
/// Prepares to quickly start and execute a script with role <b>miniProgram</b> in this preloaded task process. Or starts/executes in this non-preloaded process.
/// </summary>
static unsafe class MiniProgram_ {
	struct _TaskInit {
		public IntPtr asmFile;
		public IntPtr* args;
		public int nArgs;
	}

	/// <summary>
	/// Called by apphost.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoOptimization)]
	static void Init(nint pn, out _TaskInit r) {
		r = default;
		string pipeName = new((char*)pn);

		script.role = SRole.MiniProgram;

		process.ThisThreadSetComApartment_(ApartmentState.STA); //1.5 ms

		script.AppModuleInit_(auCompiler: true); //3 ms

		//rejected. Now this is implemented in editor. To detect when failed uses process exit code. Never mind exception text, it is not very useful.
		//process.thisProcessExit += e => { //0.9 ms
		//	if (s_started != 0) print.TaskEvent_(e == null ? "TE" : "TF " + e.ToStringWithoutStack(), s_started);
		//};

#if true
		if (!Api.WaitNamedPipe(pipeName, -1)) return;
#else
//rejected: JIT some functions in other thread. Now everything much faster than with old .NET.
//	Speed of p1: with this 3500, without 6000 (slow Deserialize JIT).

		for (int i = 0; ; i++) {
			if (Api.WaitNamedPipe(pipeName, i == 1 ? -1 : 25)) break;
			if (Marshal.GetLastWin32Error() != Api.ERROR_SEM_TIMEOUT) return;
			if (i == 1) break;

			//rejected: ProfileOptimization. Now everything is JIT-ed and is as fast as can be.

			run.thread(() => {
				//using var p2 = perf.local();

				//JIT
				Jit_.Compile(typeof(Serializer_), "Deserialize");
				//tested: now Api functions fast, don't JIT.
				//p2.Next();
				Jit_.Compile(typeof(script), nameof(script.setup), "_AuxThread");
				//p2.Next();

				//Thread.Sleep(20);
				//p2.Next();
				//"Au".ToLowerInvariant(); //15-40 ms //now <1 ms

				//if need to preload some assemblies, use code like this. But now .NET loads assemblies fast, not like in old framework.
				//_ = typeof(TypeFromAssembly).Assembly;
			}, sta: false);
		}
#endif

		//Debug_.PrintLoadedAssemblies(true, true);

		//using var p1 = perf.local();
		using var pipe = Api.CreateFile(pipeName, Api.GENERIC_READ, 0, Api.OPEN_EXISTING, 0);
		if (pipe.Is0) { Debug_.PrintNativeError(); return; }
		//p1.Next();
		int size; if (!Api.ReadFile(pipe, &size, 4, out int nr, default) || nr != 4) return;
		if (!Api.ReadFileArr(pipe, out var b, size, out nr) || nr != size) return;
		//p1.Next();
		var a = Serializer_.Deserialize(b);
		//p1.Next('d');
		var flags = (MPFlags)(int)a[2];

		r.asmFile = Marshal.StringToCoTaskMemUTF8(a[1]);
		//p1.Next();
		string[] args = a[3];
		if (!args.NE_()) {
			r.nArgs = args.Length;
			r.args = (IntPtr*)Marshal.AllocHGlobal(args.Length * sizeof(IntPtr));
			for (int i = 0; i < args.Length; i++) r.args[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
		}
		//p1.Next();

		script.s_idMainFile = (uint)(int)a[6];
		script.s_wndEditorMsg = (wnd)(int)a[8];
		script.s_wrPipeName = a[4];

		if (0 != (flags & MPFlags.FromEditor)) script.testing = true;
		if (0 != (flags & MPFlags.IsPortable)) ScriptEditor.IsPortable = true;

		folders.Editor = new(folders.ThisApp);
		folders.Workspace = new(a[5]);

		if (0 != (flags & MPFlags.RefPaths))
			AssemblyLoadContext.Default.Resolving += (alc, an)
				=> ResolveAssemblyFromRefPathsAttribute_(alc, an, AssemblyUtil_.GetEntryAssembly());

		if (0 != (flags & MPFlags.NativePaths))
			AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, dll)
				=> ResolveUnmanagedDllFromNativePathsAttribute_(dll, AssemblyUtil_.GetEntryAssembly());

		if (0 != (flags & MPFlags.MTA))
			process.ThisThreadSetComApartment_(ApartmentState.MTA);

		if (0 != (flags & MPFlags.Console)) {
			Api.AllocConsole();
		} else {
			if (0 != (flags & MPFlags.RedirectConsole)) print.redirectConsoleOutput = true;
			//Compiler adds this flag if the script uses System.Console assembly.
			//Else new users would not know how to test code examples with Console.WriteLine found on the internet.
		}

		//p1.Next();
		script.Starting_(a[0], a[7], preloaded: 0 != (flags & MPFlags.Preloaded));

		//Api.QueryPerformanceCounter(out s_started);
		//print.TaskEvent_("TS", s_started);
	}

	//for assemblies used in miniProgram and editorExtension scripts
	internal static Assembly ResolveAssemblyFromRefPathsAttribute_(AssemblyLoadContext alc, AssemblyName an, Assembly scriptAssembly) {
		//print.it("managed", an);
		//note: don't cache GetCustomAttribute/split results. It's many times faster than LoadFromAssemblyPath and JIT.
		var attr = scriptAssembly.GetCustomAttribute<RefPathsAttribute>();
		if (attr != null) {
			string name = an.Name;
			foreach (var v in attr.Paths.Split('|')) {
				//print.it(v);
				int iName = v.Length - name.Length - 4;
				if (iName <= 0 || v[iName - 1] != '\\' || !v.Eq(iName, name, true)) continue;
				if (!filesystem.exists(v).File) continue;
				return alc.LoadFromAssemblyPath_(v);
			}
		}
		return null;
	}

	internal static Assembly LoadFromAssemblyPath_(this AssemblyLoadContext t, string path) {
		try { return t.LoadFromAssemblyPath(path); }
		catch { }
		//catch (FileLoadException e1) {
		//	Debug_.Print("alc.LoadFromAssemblyPath failed. Will retry with s_alc. " + e1);
		//}
		//If the assembly has the same name as one of TPA assemblies (probably it's a newer version),
		//	the above LoadFromAssemblyPath ignores the path and tries to load the TPA assembly, and fails.
		//	Workaround: Then try to load to another AssemblyLoadContext.
		//return Assembly.LoadFile(path); //works, but better use the same context for all
		s_alc ??= new("Resolving");
		return s_alc.LoadFromAssemblyPath(path);
	}
	static AssemblyLoadContext s_alc;

	//for assemblies used in miniProgram and editorExtension scripts
	internal static IntPtr ResolveUnmanagedDllFromNativePathsAttribute_(string name, Assembly scriptAssembly) {
		//print.it("native", name);
		var attr = scriptAssembly.GetCustomAttribute<NativePathsAttribute>();
		if (attr != null) {
			if (!name.Ends(".dll", true)) name += ".dll";
			foreach (var v in attr.Paths.Split('|')) {
				//print.it(v);
				if (!v.Ends(name, true) || !v.Eq(v.Length - name.Length - 1, '\\')) continue;
				if (NativeLibrary.TryLoad(v, out var h)) return h;
			}
		}
		return default;
	}

	/// <summary>
	/// Used by <b>exeProgram</b>.
	/// </summary>
	/// <param name="rootDir">Directory that may contain subdir <c>"runtimes"</c>.</param>
	internal static void ResolveNugetRuntimes_(string rootDir) {
		var runtimesDir = pathname.combine(rootDir, "runtimes");
		if (!filesystem.exists(runtimesDir).Directory) return;

		//This code is similar as in Compiler._GetDllPaths:_AddGroup. There we get paths from XML, here from filesystem.

		int verPC = osVersion.minWin10 ? 100 : osVersion.minWin8_1 ? 81 : osVersion.minWin8 ? 80 : 70; //don't need Win11

		var flags = FEFlags.AllDescendants | FEFlags.IgnoreInaccessible | FEFlags.NeedRelativePaths | FEFlags.UseRawPath;
		List<(FEFile f, int ver)> aNet = [], aNative = [];
		foreach (var f in filesystem.enumFiles(runtimesDir, "*.dll", flags)) {
			var s = f.Name;
			if (!s.Starts(@"\win", true) || s.Length < 10) continue;

			int i = 4, verDll = 0;
			if (s[i] is >= '0' and <= '9') {
				verDll = s.ToInt(i, out i);
				if (verDll != 81) verDll *= 10;
				if (verDll > verPC) continue;
			}

			if (s.Eq(i, osVersion.is32BitProcess ? @"-x64\" : @"-x86\", true)) continue;

			var a = s.Eq(i + 5, @"native\", true) ? aNative : aNet;
			a.Add((f, verDll));
		}

		var dr = _Do(aNet);
		var dn = _Do(aNative);

		static Dictionary<string, string> _Do(List<(FEFile f, int ver)> a) {
			if (a.Count == 0) return null;
			Dictionary<string, string> d = null;
			foreach (var group in a.ToLookup(o => pathname.getNameNoExt(o.f.Name), StringComparer.OrdinalIgnoreCase)) {
				//print.it($"<><c blue>{group.Key}<>");

				int verBest = -1;
				string sBest = null;
				foreach (var (f, verDll) in group) {
					if (verDll > verBest) {
						verBest = verDll;
						sBest = f.FullPath;
					}
				}

				if (sBest != null) {
					//print.it(sBest);
					d ??= new(StringComparer.OrdinalIgnoreCase);
					d[group.Key] = sBest;
				}
			}

			return d;
		}

		if (dr != null) AssemblyLoadContext.Default.Resolving += (alc, an) => {
			//print.it("lib", an.Name);
			if (!dr.TryGetValue(an.Name, out var path)) return null;
			return alc.LoadFromAssemblyPath_(path);
		};
		if (dn != null) AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) => {
			//print.it("native", name);
			if (name.Ends(".dll", true)) name = name[..^4];
			if (!dn.TryGetValue(name, out var path)) return default;
			if (!NativeLibrary.TryLoad(path, out var r)) return default;
			return r;
		};
	}

	[Flags]
	public enum MPFlags {
		/// <summary>Has <c>[RefPaths]</c> attribute. It is when using meta <b>r</b> or <b>nuget</b>.</summary>
		RefPaths = 1,

		/// <summary><b>Main</b> with <c>[MTAThread]</c>.</summary>
		MTA = 2,

		/// <summary>Has meta <b>console</b> true.</summary>
		Console = 4,

		/// <summary>Uses <c>System.Console</c> assembly.</summary>
		RedirectConsole = 8,

		/// <summary>Has <c>[NativePaths]</c> attribute. It is when using NuGet packages with native dlls.</summary>
		NativePaths = 16,

		/// <summary>Started from editor with the <b>Run</b> button or menu command. Used for <see cref="script.testing"/>.</summary>
		FromEditor = 32,

		/// <summary>Started from portable editor.</summary>
		IsPortable = 64,
		
		/// <summary>Using a preloaded process.</summary>
		Preloaded = 128,

		//Config = 256, //meta hasConfig
	}
}