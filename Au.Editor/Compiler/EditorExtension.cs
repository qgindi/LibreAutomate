//#define STREAM
//#define TEST_STARTUP_SPEED
//#define TEST_UNLOAD

using System.Runtime.Loader;
using System.Windows;

/// <summary>
/// Functions and events for scripts with role editorExtension.
/// The script must have this at the start <c>/*/ role editorExtension; r Au.Editor.dll; /*/</c>.
/// </summary>
public static class EditorExtension {
	/// <summary>
	/// Executes assembly in this thread.
	/// </summary>
	/// <param name="asmFile">Full path of assembly file.</param>
	/// <param name="args">To pass to Main.</param>
	/// <param name="handleExceptions">Handle/print exceptions.</param>
	internal static async void Run_(string asmFile, string[] args, bool handleExceptions) {
		try {
			//using var p1 = perf.local();
			
			_LoadedScriptAssembly lsa = default;
			var (asm, loaded) = lsa.Find(asmFile);
			if (asm == null) {
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
					unsafe {
						if (!Api.CopyFileEx(asmFile, s, null, default, null, 0)) throw new AuException(0, "failed to copy assembly file");
					}
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
				} else {
					asm = alc.LoadFromAssemblyPath(asmFile);
				}
#endif
#if TEST_UNLOAD
				new _AssemblyDtor(asm);
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
			
			//support async. Then EntryPoint returns some `void <Main>(string[])` that calls the true entry method and hangs.
			bool isAsync = false;
			if (entryPoint.IsSpecialName) {
				var ep2 = entryPoint.DeclaringType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
					.FirstOrDefault(static o => o.ReturnType is var rt && (rt == typeof(Task) || rt == typeof(Task<int>)) && o.Name is "<Main>$" or "Main" && o.GetParameters() is { } pa && (pa.Length == 0 || (pa.Length == 1 && pa[0].ParameterType == typeof(string[]))) && o.IsDefined(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute)));
				if (isAsync = ep2 != null) entryPoint = ep2;
			}
			
			var epParams = entryPoint.GetParameters().Length != 0 ? new object[] { args ?? Array.Empty<string>() } : null;
			
			if (isAsync) {
				await (Task)entryPoint.Invoke(null, epParams);
			} else {
				entryPoint.Invoke(null, epParams);
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
		DateTime _fileTime;
		
		[MethodImpl(MethodImplOptions.NoInlining)]
		public (Assembly asm, bool loaded) Find(string asmFile) {
			if (filesystem.getProperties(asmFile, out var p, FAFlags.UseRawPath)) {
				_fileTime = p.LastWriteTimeUtc;
				if (_d.TryGetValue(asmFile, out var x)) {
					bool modified = x.time != _fileTime;
					
					if (x.restarting != null) {
						var v = x.restarting; x.restarting = null;
						try { v(modified); }
						catch (Exception e1) { print.it(e1); }
					}
					
					if (!modified) return (x.asm, true);
					_d.Remove(asmFile);
					return (null, true);
				}
			}
			return default;
		}
		
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Add(string asmFile, Assembly asm) {
			if (_fileTime == default) return; //filesystem.getProperties failed
			_d.Add(asmFile, new _Asm { time = _fileTime, asm = asm });
			
			//foreach(var v in _d.Values) print.it(v.asm.FullName);
			//foreach(var v in AppDomain.CurrentDomain.GetAssemblies()) print.it(v.FullName);
		}
	}
	
	class _Asm {
		public DateTime time;
		public Assembly asm;
		public Action<bool> restarting;
	}
	
	static readonly Dictionary<string, _Asm> _d = new(StringComparer.OrdinalIgnoreCase);
	
	static _Asm _FindAsmValue(Assembly asm) {
		foreach (var v in _d.Values) if (v.asm == asm) return v;
		throw new InvalidOperationException("This code must be in the script assembly");
	}
	
	/// <summary>
	/// When starting new instance of this script.
	/// </summary>
	/// <remarks>
	/// <para>The bool parameter is true if the script has been modified. It also means that the new instance will be a new assembly. If false, the new instance will be the same loaded assembly.</para>
	/// 
	/// <para>The event handler should end all activities of current instance: unsubscribe events, stop timers, end threads and other tasks, remove added UI elements, etc. Unless it supports multiple instances.</para>
	/// 
	/// <para>If the old instance does not end its activities properly when restarting, you may notice these problems:
	/// <br/>• Duplicate events, timers, etc.
	/// <br/>• Old assemblies never unloaded after restarting modified script. Usually such memory leaks aren't dangerous, but be aware of it. To avoid it, let the event handler ensure that the script is not leaving any GC roots, for example event handlers subscribed to external events, running timers, threads and other code that could still run in the future. If the assembly is rooted, GC cannot unload it.
	/// </para>
	/// <para>If the script has static fields, and the parameter is false, the new script will inherit their values. You may want to reset them.</para>
	/// 
	/// <para>When this event is raised, it is unsubscribed automatically. Don't need to unsubscribe.</para>
	/// </remarks>
	public static event Action<bool> Restarting {
		add { _FindAsmValue(Assembly.GetCallingAssembly()).restarting += value; }
		remove { _FindAsmValue(Assembly.GetCallingAssembly()).restarting -= value; }
	}
	
	/// <summary>
	/// When the main window loaded. If already loaded when adding the event handler, it is called now instead.
	/// </summary>
	/// <remarks>
	/// <para>Don't need to unsubscribe. The event can occur max 1 time in this process, and then is unsubscribed automatically.</para>
	/// 
	/// <para>If the program starts hidden (it can be set in Options), it does not create the main window until need to show it (eg clicked tray icon). While hidden, <see cref="App.Hmain"/> is <c>default(wnd)</c>; <see cref="App.Wmain"/> is not null, but its <b>IsLoaded</b> property returns false. The window is created the first time it must be shown, and then not closed until it's time to exit the process. If the program starts visible, the event handler is executed when trying to subscribe the event.</para>
	/// </remarks>
	public static event Action WindowLoaded {
		add {
			if (App.Loaded == AppState.LoadedUI) {
				_InvokeAction(value);
			} else if (App.Loaded < AppState.LoadedUI) {
				_windowLoaded += value;
			}
		}
		remove { _windowLoaded -= value; }
	}
	static Action _windowLoaded;
	
	internal static void WindowLoaded_() {
		var v = _windowLoaded; _windowLoaded = null;
		_InvokeAction(v);
	}
	
	static void _InvokeAction(Action a) {
		if (a == null) return;
		try { a(); }
		catch (Exception e1) { print.it(e1); }
	}
	
	/// <summary>
	/// When main window loaded and is ready for editing. If already ready when adding the event handler, it is called now instead.
	/// </summary>
	/// <remarks>
	/// Don't need to unsubscribe. The event can occur max 1 time in this process, and then is unsubscribed automatically.
	/// </remarks>
	public static event Action WindowReady {
		add { if (CodeInfo.IsReadyForEditing) _InvokeAction(value); else CodeInfo.ReadyForEditing += value; } //handles exceptions
		remove { CodeInfo.ReadyForEditing -= value; }
	}
	
	//note: cannot add event WindowCreated (before loaded, when still invisible). If editor starts visible, startup scripts run when it is already loaded/visible.
	
	/// <summary>
	/// When closing current workspace.
	/// </summary>
	/// <remarks>
	/// <para>The bool parameter is true on program exit, false when opening another workspace (or reopening this).</para>
	/// 
	/// <para>Everything is already saved. Documents in editor still not closed. Task processes still not ended.</para>
	/// 
	/// <para>When this event is raised, it is unsubscribed automatically. The script must unsubscribe only on <see cref="Restarting"/>.</para>
	/// </remarks>
	public static event Action<bool> ClosingWorkspace {
		add { _closingWorkspace += value; }
		remove { _closingWorkspace -= value; }
	}
	
	internal static void ClosingWorkspace_(bool onExit) {
		if (_closingWorkspace == null) return;
		var v = _closingWorkspace; _closingWorkspace = null;
		try { v(onExit); }
		catch (Exception e1) { dialog.showError("Exception in EditorExtension.ClosingWorkspace event handler", e1.ToString(), owner: App.Hmain.IsVisible ? App.Hmain : default, secondsTimeout: 5); }
		if (App.Loaded >= AppState.LoadedUI) App.Model.Save.AllNowIfNeed();
	}
	static Action<bool> _closingWorkspace;
	
#if TEST_UNLOAD
	//This shows that AssemblyLoadContext are unloaded on GC.
	class _AssemblyLoadContext : AssemblyLoadContext {
		public _AssemblyLoadContext(string name, bool isCollectible) : base(name, isCollectible) { }

		~_AssemblyLoadContext() { print.it("AssemblyLoadContext unloaded", Name); }

		//protected override Assembly Load(AssemblyName assemblyName) {
		//	//print.it("Load", assemblyName);
		//	return null;
		//}
	}

	//This shows that Assembly are unloaded on GC, although later than AssemblyLoadContext.
	//The dlls are unloaded even later, maybe after 1-2 minutes.
	class _AssemblyDtor {
		static readonly ConditionalWeakTable<Assembly, _AssemblyDtor> s_cwt = new();

		readonly string _file, _name;

		public _AssemblyDtor(Assembly a) {
			s_cwt.Add(a, this);
			_file = a.Location;
			_name = a.FullName.Split('|')[0];
		}

		~_AssemblyDtor() {
			print.it("Assembly unloaded", _name, _file);
			var s = _file;
			Task.Delay(120_000).ContinueWith(_ => { print.it(Api.DeleteFile(s)); });
		}

		//BAD: memory leak:
		//	First assembles never GC-collected after removing from _d (when starting a modified version of the script).
		//	Only if the assembly loaded at startup when app started hidden.
		//	Tested with VS tools: they don't have GC roots.
		//	It seems it's a .NET bug. Could not find a workaround.
		//	Never mind. It's a very small leak compared to WPF leaks.
		//	See also: https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability?source=recommendations

		//public static void Test2() {
		//	print.it("-- _AssemblyDtor --");
		//	foreach(var v in s_cwt) {
		//		print.it(v.Key.Location);
		//	}
		//}
	}

	//public static void Test() {
	//	foreach (var v in _d) {
	//		print.it(v.Key, v.Value.asm.Location);
	//	}
	//	//_AssemblyDtor.Test2();
	//}
#endif
}
