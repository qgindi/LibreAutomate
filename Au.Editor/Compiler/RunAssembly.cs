//#define STREAM
//#define TEST_STARTUP_SPEED
//#define TEST_UNLOAD

using System.Runtime.Loader;

/// <summary>
/// Executes scripts with role editorExtension.
/// </summary>
static unsafe class RunAssembly {
	/// <summary>
	/// Executes assembly in this thread.
	/// </summary>
	/// <param name="asmFile">Full path of assembly file.</param>
	/// <param name="args">To pass to Main.</param>
	/// <param name="handleExceptions">Handle/print exceptions.</param>
	public static void Run(string asmFile, string[] args, bool handleExceptions) {
		try {
			//using var p1 = perf.local();

			_LoadedScriptAssembly lsa = default;
			Assembly asm = lsa.Find(asmFile, out bool loaded);
			if (asm == null) {
				//print.it(loaded);
				var alc = new AssemblyLoadContext(null, isCollectible: true);
				//p1.Next();

#if STREAM
				//Uses LoadFromStream, not LoadFromAssemblyPath.
				//LoadFromAssemblyPath has this problem: does not reload modified assembly from same file.
				//	Need a dll file with unique name for each version of same script.
				//Note: the 'loaded' was intended to make faster in some cases. However cannot use it because of the xor.
				//tested: step-debugging works. Don't need the overload with pdb parameter.

				var b = File.ReadAllBytes(asmFile); //with WD ~7 ms, but ~25 ms without xor. With Avast ~7 ms regardless of xor. Both fast if already scanned.
				for (int i = 0; i < b.Length; i++) b[i] ^= 1; //prevented AV full dll scan twice. Now fully scans once (WD always scans when loading assembly from stream; Avast when loading from stream or earlier).
				using var stream = new MemoryStream(b, false);
				//p1.Next();
				asm = alc.LoadFromStream(stream); //with WD always 15-25 ms. With Avast it seems always fast.
#else
				//Uses LoadFromAssemblyPath. If LoadFromStream, no source file/line info in stack traces; also no Assembly.Location, and possibly more problems.
				//never mind: Creates and loads many dlls when edit-run many times.
				//tested: .NET unloads dlls, but later than Assembly objects, maybe after 1-2 minutes, randomly.

				if (loaded) {
					var s = asmFile.Insert(^4, "'" + perf.mcs.ToString()); //info: compiler will delete all files with "'" on first run after editor restart
#if true //copy file
					if (!Api.CopyFileEx(asmFile, s, null, default, null, 0)) throw new AuException(0, "failed to copy assembly file");
					//p1.Next('C');
					//bad: WD makes much slower. Scans 2 times. Avast scans faster, and only when copying.
					//never mind: compiler should create file with unique name, to avoid copying now. Probably would complicate too much, or even not possible.
					asm = alc.LoadFromAssemblyPath(s);
#else //rename file. Faster, but unreliable when need to run soon again. Then compiler would not find the file and compile again. Or the new file could be replaced with the old, etc.
					if (!Api.MoveFileEx(asmFile, s, 0)) throw new AuException(0, "failed to rename assembly file");
					p1.Next('C'); //WD does not scan when renaming
					asm = alc.LoadFromAssemblyPath(s);
					p1.Next('L');
					//now need to rename or copy back. Else would compile each time.
					//if (!Api.MoveFileEx(s, asmFile, 0)) throw new AuException(0, "failed to rename assembly file"); //works, but no stack trace
					Task.Run(() => { Api.CopyFileEx(s, asmFile, null, default, null, 0); }); //make compiler happy next time. Now let AV scan it async.
#endif
#if TEST_UNLOAD
					new _AssemblyDtor(asm);
#endif
				} else {
					asm = alc.LoadFromAssemblyPath(asmFile);
				}
#endif
				//p1.Next('L');

				lsa.Add(asmFile, asm);

				//this event will be here for editorExtension assemblies only.
				//	Libraries used by editorExtension scripts are loaded by AssemblyLoadContext.Default.
				//	Dlls used by meta pr libraries are resolved in app project -> _UnmanagedDll_Resolving.
				//	Don't need alc.Resolving here. Libraries are always loaded in default context.
				alc.ResolvingUnmanagedDll += (asm, name) => MiniProgram_.ResolveUnmanagedDllFromNativePathsAttribute_(name, asm);
			}

			var entryPoint = asm.EntryPoint ?? throw new InvalidOperationException("assembly without entry point (function Main)");
			bool useArgs = entryPoint.GetParameters().Length != 0;
			if (useArgs) args ??= Array.Empty<string>();

			if (useArgs) {
				entryPoint.Invoke(null, new object[] { args });
			} else {
				entryPoint.Invoke(null, null);
			}
		}
		catch (Exception e1) when (handleExceptions) {
			print.it(e1 is TargetInvocationException te ? te.InnerException : e1);
		}
	}

	/// <summary>
	/// Remembers and finds script assemblies loaded in this process, to avoid loading the same unchanged assembly multiple times.
	/// </summary>
	struct _LoadedScriptAssembly {
		class _Asm {
			public DateTime time;
			public Assembly asm;
		}

		static Dictionary<string, _Asm> _d;
		DateTime _fileTime;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public Assembly Find(string asmFile, out bool loaded) {
			loaded = false;
			_d ??= new Dictionary<string, _Asm>(StringComparer.OrdinalIgnoreCase);
			if (!filesystem.getProperties(asmFile, out var p, FAFlags.UseRawPath)) return null;
			_fileTime = p.LastWriteTimeUtc;
			if (loaded = _d.TryGetValue(asmFile, out var x)) {
				if (x.time == _fileTime) return x.asm;
				_d.Remove(asmFile);
			}
			return null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Add(string asmFile, Assembly asm) {
			if (_fileTime == default) return; //filesystem.getProperties failed
			_d.Add(asmFile, new _Asm { time = _fileTime, asm = asm });

			//foreach(var v in _d.Values) print.it(v.asm.FullName);
			//foreach(var v in AppDomain.CurrentDomain.GetAssemblies()) print.it(v.FullName);
		}
	}

#if TEST_UNLOAD
	//This shows that AssemblyLoadContext are unloaded on GC.
	class _AssemblyLoadContext : AssemblyLoadContext
	{
		public _AssemblyLoadContext(string name, bool isCollectible) : base(name, isCollectible) { }

		~_AssemblyLoadContext() { print.it("AssemblyLoadContext unloaded", Name); }

		protected override Assembly Load(AssemblyName assemblyName) {
			//print.it("Load", assemblyName);
			return null;
		}
	}

	//This shows that Assembly are unloaded on GC, although later than AssemblyLoadContext.
	//The dlls are unloaded even later, maybe after 1-2 minutes.
	class _AssemblyDtor
	{
		static readonly ConditionalWeakTable<Assembly, _AssemblyDtor> s_cwt = new();

		readonly string _file;

		public _AssemblyDtor(Assembly a) {
			s_cwt.Add(a, this);
			_file = a.Location;
		}

		~_AssemblyDtor() {
			print.it("Assembly unloaded", _file);
			Task.Delay(120_000).ContinueWith(_ => { print.it(Api.DeleteFile(_file)); });
		}
	}
#endif
}
