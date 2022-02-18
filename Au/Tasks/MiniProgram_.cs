
using System.Runtime.Loader;

//PROBLEM: slow startup.
//A minimal script starts in 70-100 ms cold, 40 hot.
//Workaround for role miniProgram:
//	Preload task process. Let it wait for next task. While waiting, it also can JIT etc.
//	Then starts in 12/4 ms (cold/hot). With script.setup 15/5.
//	Except first time. Also not faster if several scripts are started without a delay. Never mind.
//	This is implemented in this class and in Au.AppHost (just ~10 code lines added in 1 place).

/*
//To test task startup speed, use script "task startup speed.cs":

300.ms(); //give time to preload new task process
for (int i = 0; i < 5; i++) {
//	perf.cpu();
//	perf.shared.First(); //slower
	var t=perf.ms.ToS(); t=perf.ms.ToS();
	script.run(@"miniProgram.cs", t); //cold 10, hot 3. Without Setup: 6/2. Slower on vmware Win7+Avast.
//	script.run(@"exeProgram.cs", t); //cold 80, hot 43. Slightly slower on vmware Win7+Avast.
	600.ms(); //give time for the process to exit
}

//miniProgram.cs and exeProgram.cs:

print.it(perf.ms-Int64.Parse(args[0]));
*/

//Smaller problem: .NET creates many threads. No workaround.

//PROBLEM: preloaded task's windows start inactive, behind one or more windows. Unless they activate self, like dialog.
//	It does not depend on the foreground lock setting/API. The setting/API just enable SetForegroundWindow, but most windows don't call it.
//	Workaround: use CBT hook. It receives HCBT_ACTIVATE even when the window does not become the foreground window.
//		On HCBT_ACTIVATE, async-call SetForegroundWindow. Also, editor calls AllowSetForegroundWindow before starting task.

//PROBLEM: although Main() starts fast, but the process ends slowly, because of .NET.
//	Eg if starting an empty script every <50 ms, sometimes cannot start.

//FUTURE: option to start without preloading.

namespace Au.More {
	/// <summary>
	/// Prepares to quickly start and execute a script with role miniProgram in this preloaded task process.
	/// </summary>
	static unsafe class MiniProgram_ {
		//static long s_started;
		internal static string s_scriptId;

		struct _TaskInit {
			public IntPtr asmFile;
			public IntPtr* args;
			public int nArgs;
		}

		/// <summary>
		/// Called by apphost.
		/// </summary>
		static void Init(nint pn, out _TaskInit r) {
			r = default;
			string pipeName = new((char*)pn);

			script.s_role = SRole.MiniProgram;

			process.ThisThreadSetComApartment_(ApartmentState.STA); //1.7 ms

			script.AppModuleInit_(); //2.7 ms (1.8 if with process.thisProcessExit below)

			//rejected. Now this is implemented in editor. To detect when failed uses process exit code. Never mind exception text, it is not very useful.
			//process.thisProcessExit += e => { //0.9 ms
			//	if (s_started != 0) print.TaskEvent_(e == null ? "TE" : "TF " + e.ToStringWithoutStack(), s_started);
			//};

			for (int i = 0; ; i++) {
				if (Api.WaitNamedPipe(pipeName, i == 1 ? -1 : 25)) break;
				if (Marshal.GetLastWin32Error() != Api.ERROR_SEM_TIMEOUT) return;
				if (i == 1) break;

				//rejected: ProfileOptimization. Now everything is JIT-ed and is as fast as can be.

				run.thread(() => {
					//using var p2 = perf.local();

					//JIT
					Jit_.Compile(typeof(Serializer_), "Deserialize");
					Jit_.Compile(typeof(Api), nameof(Api.ReadFile), nameof(Api.CloseHandle), nameof(Api.SetEnvironmentVariable));
					//p2.Next();
					var h1 = Api.CreateFile(null, Api.GENERIC_READ, 0, default, Api.OPEN_EXISTING, 0);
					//p2.Next();
					Marshal.StringToCoTaskMemUTF8("-");
					folders.Workspace = new FolderPath("");
					Jit_.Compile(typeof(script), nameof(script.setup), nameof(script.TrayIcon_));
					//p2.Next();

					//print.TaskEvent_(null, 0); //8-20 ms
					Thread.Sleep(20);
					"Au".ToLowerInvariant(); //15-40 ms

					//if need to preload some assemblies, use code like this. But now .NET loads assemblies fast, not like in old framework.
					//_ = typeof(TypeFromAssembly).Assembly;
				}, sta: false);

				_Hook();
			}

			//Debug_.PrintLoadedAssemblies(true, true);

			EFlags flags;

			//using var p1 = perf.local();
			using (var pipe = Api.CreateFile(pipeName, Api.GENERIC_READ, 0, default, Api.OPEN_EXISTING, 0)) {
				if (pipe.Is0) { Debug_.PrintNativeError_(); return; }
				//p1.Next();
				int size; if (!Api.ReadFile(pipe, &size, 4, out int nr, default) || nr != 4) return;
				//p1.Next();
				if (!Api.ReadFileArr(pipe, out var b, size, out nr) || nr != size) return;
				//p1.Next();
				var a = Serializer_.Deserialize(b);
				//p1.Next('d');
				script.s_name = a[0]; //would not need, because AppDomain.CurrentDomain.FriendlyName returns the same, but I don't trust it, it used to return a different string in the past
				flags = (EFlags)(int)a[2];

				r.asmFile = Marshal.StringToCoTaskMemUTF8(a[1]);
				//p1.Next();
				string[] args = a[3];
				if (!args.NE_()) {
					r.nArgs = args.Length;
					r.args = (IntPtr*)Marshal.AllocHGlobal(args.Length * sizeof(IntPtr));
					for (int i = 0; i < args.Length; i++) r.args[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
				}
				//p1.Next();

				string wrp = a[4]; if (wrp != null) Api.SetEnvironmentVariable("script.writeResult.pipe", wrp);
				folders.Workspace = new FolderPath(a[5]);
				s_scriptId = a[6];
				//p1.Next();

				script.ExitWhenEditorDies_(a[7]);
			}
			//p1.Next();

			if (0 != (flags & EFlags.RefPaths))
				AssemblyLoadContext.Default.Resolving += (alc, an)
					=> ResolveAssemblyFromRefPathsAttribute_(alc, an, Assembly.GetEntryAssembly());

			if (0 != (flags & EFlags.NativePaths))
				AssemblyLoadContext.Default.ResolvingUnmanagedDll += (asm, dll)
					=> ResolveUnmanagedDllFromNativePathsAttribute_(asm, dll, Assembly.GetEntryAssembly());

			if (0 != (flags & EFlags.MTA))
				process.ThisThreadSetComApartment_(ApartmentState.MTA);

			if (0 != (flags & EFlags.Console)) {
				Api.AllocConsole();
			} else {
				if (0 != (flags & EFlags.RedirectConsole)) print.redirectConsoleOutput = true;
				//Compiler adds this flag if the script uses System.Console assembly.
				//Else new users would not know how to test code examples with Console.WriteLine found on the internet.
			}

			//if(0 != (flags & EFlags.Config)) { //this was with .NET 4
			//	var config = asmFile + ".config";
			//	if(filesystem.exists(config, true).File) AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", config);
			//}

			if (s_hook == null) _Hook();

			//Api.QueryPerformanceCounter(out s_started);
			//print.TaskEvent_("TS", s_started);
		}

		//also used by the editor for assemblies used in editorExtension scripts
		internal static Assembly ResolveAssemblyFromRefPathsAttribute_(AssemblyLoadContext alc, AssemblyName an, Assembly scriptAssembly) {
			//print.it(an);
			//note: don't cache GetCustomAttribute/split results. It's many times faster than LoadFromAssemblyPath and JIT.
			var attr = scriptAssembly.GetCustomAttribute<RefPathsAttribute>();
			if (attr != null) {
				string name = an.Name;
				foreach (var v in attr.Paths.Split('|')) {
					//print.it(v, name);
					int iName = v.Length - name.Length - 4;
					if (iName <= 0 || v[iName - 1] != '\\' || !v.Eq(iName, name, true)) continue;
					if (!filesystem.exists(v).File) continue;
					//try {
					return alc.LoadFromAssemblyPath(v);
					//} catch(Exception ex) { Debug_.Print(ex.ToStringWithoutStack()); break; }
				}
			}
			return null;
		}

		//also used by the editor for assemblies used in editorExtension scripts
		internal static IntPtr ResolveUnmanagedDllFromNativePathsAttribute_(Assembly refAssembly, string name, Assembly scriptAssembly) {
			var attr = scriptAssembly.GetCustomAttribute<NativePathsAttribute>();
			if (attr != null) {
				var dir = pathname.getDirectory(refAssembly.ManifestModule.FullyQualifiedName);
				if (!name.Ends(".dll", true)) name += ".dll";
				//print.it(dir, name);
				foreach (var v in attr.Paths.Split('|')) {
					if (!v.Ends(name, true) || !v.Eq(v.Length - name.Length - 1, '\\')) continue;
					var h = Api.LoadLibrary(dir + v);
					if (h != default) return h;
				}
			}
			return default;
		}

		[Flags]
		public enum EFlags {
			/// <summary>Has [RefPaths] attribute. It is when using meta r or nuget.</summary>
			RefPaths = 1,

			/// <summary>Main() with [MTAThread].</summary>
			MTA = 2,

			/// <summary>Has meta console true.</summary>
			Console = 4,

			/// <summary>Uses System.Console assembly.</summary>
			RedirectConsole = 8,

			/// <summary>Has [NativePaths] attribute. It is when using nuget packages with native dlls.</summary>
			NativePaths = 16,

			//Config = 256, //meta hasConfig
		}

		static void _Hook() {
			s_hook = WindowsHook.ThreadCbt(m => {
				//print.it(m.code, m.wParam, m.lParam);
				//switch(m.code) {
				//case HookData.CbtEvent.ACTIVATE:
				//case HookData.CbtEvent.SETFOCUS:
				//	print.it((wnd)m.wParam);
				//	print.it(wnd.active);
				//	print.it(wnd.thisThread.active);
				//	print.it(wnd.focused);
				//	print.it(wnd.thisThread.focused);
				//	break;
				//}
				if (m.code == HookData.CbtEvent.ACTIVATE) {
					var w = (wnd)m.wParam;
					if (!w.HasExStyle(WSE.NOACTIVATE)) {
						//print.it(w);
						//print.it(w.ExStyle);
						//Api.SetForegroundWindow(w); //does not work
						timer.after(1, _ => {
							if (s_hook == null) return;
							//print.it(wnd.active);
							//print.it(wnd.thisThread.active);
							bool isActive = w == wnd.active, activate = !isActive && w == wnd.thisThread.active;
							if (isActive || activate) { s_hook.Dispose(); s_hook = null; }
							if (activate) {
								Api.SetForegroundWindow(w);
								//w.ActivateL(); //no, it's against Windows rules, and works differently with meta outputPath
								//Before starting task, editor calls AllowSetForegroundWindow. But if clicked etc a window after that:
								//	SetForegroundWindow fails always or randomly;
								//	Activate[L] fails if that window is of higher UAC IL, unless the foreground lock timeout is 0.
							}
						});
					}
				}
				return false;
			});
		}
		static WindowsHook s_hook;

	}
}