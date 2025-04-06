using static Au.Types.GWL;

namespace Au.Types;

[DebuggerStepThrough]
static unsafe partial class Cpp {
	static List<(string dll, nint h)> _dlls = [];

	static Cpp() {
		LoadAuNativeDll("AuCpp.dll");

		Cpp_SetHelperCallback(&_HelperCallback); //note: not in elm ctor. Eg by the elm tool not via elm. Fast.
	}

	/// <summary>
	/// Loads correct 64/32/ARM64 version of a private native dll. Then <c>[DllImport]</c> will use it.
	/// Used for Au dlls (AuCpp) and LA dlls (Scintilla).
	/// </summary>
	/// <param name="fileName">Dll file name like <c>"name.dll"</c>.</param>
	/// <returns>Handle.</returns>
	/// <exception cref="DllNotFoundException"></exception>
	/// <remarks>
	/// Searches in:
	/// - subfolder <c>64</c> or <c>32</c> or <c>64\ARM</c> of the <c>Au.dll</c> folder.
	/// - calls <b>NativeLibrary.TryLoad</b>, which works like simple <c>[DllImport]</c>, eg may use info from <c>deps.json</c>.
	/// - subfolder <c>64</c> etc of folder specified in environment variable <c>Au.Path</c>. For example the dll is unavailable if used in an assembly (managed dll) loaded in a nonstandard environment, eg VS forms designer or VS C# Interactive (then <b>folders.ThisApp</b> is <c>"C:\Program Files (x86)\Microsoft Visual Studio\..."</c>). Workaround: set environment variable <c>Au.Path</c> = the main Au directory and restart Windows.
	/// </remarks>
	public static nint LoadAuNativeDll(string fileName) {
		//Debug.Assert(default == Api.GetModuleHandle(fileName)); //no, asserts if cpp dll is injected by acc

		nint h = 0;
		string rel = (RuntimeInformation.ProcessArchitecture switch { Architecture.X86 => @"32\", Architecture.Arm64 => @"64\ARM\", _ => @"64\" }) + fileName;
		//rejected: use standard NuGet "runtimes" folder instead. I did not find info whether it can be used.

		//Au.dll dir + rel
		var asm = typeof(Cpp).Assembly;
		if (asm.Location is [_, ..] s1) {
			s1 = s1[..(s1.LastIndexOf('\\') + 1)] + rel;
			if (NativeLibrary.TryLoad(s1, out h)) return h;
		}

		//like [DllImport]. It uses NATIVE_DLL_SEARCH_DIRECTORIES, which was built at startup by our AppHost or from deps.json.
		//	Also finds in temp dir when <PublishSingleFile>+<IncludeNativeLibrariesForSelfExtract>.
		if (NativeLibrary.TryLoad(fileName, asm, null, out h)) return h;

		//environment variable + rel
		if (Environment.GetEnvironmentVariable("Au.Path") is string s2)
			if (NativeLibrary.TryLoad(pathname.combine(s2, rel), out h)) return h;

		throw new DllNotFoundException(fileName + " not found");
	}

	internal struct Cpp_Acc {
		public IntPtr acc;
		public int elem;
		public elm.Misc_ misc;

		public Cpp_Acc(IntPtr iacc, int elem_) { acc = iacc; elem = elem_; misc = default; }
		public Cpp_Acc(elm e) { acc = e._iacc; elem = e._elem; misc = e._misc; }
		public static implicit operator Cpp_Acc(elm e) => new(e);
	}

	internal delegate int Cpp_AccFindCallbackT(Cpp_Acc a, RECT* r);

	internal struct Cpp_AccFindParams {
		string _role, _name, _prop;
		int _roleLength, _nameLength, _propLength;
		public EFFlags flags;
		public int skip;
		char _resultProp; //elmFinder.RProp
		int _flags2;

		public Cpp_AccFindParams(string role, string name, string prop, EFFlags flags, int skip, char resultProp) {
			if (role != null) { _role = role; _roleLength = role.Length; }
			if (name != null) { _name = name; _nameLength = name.Length; }
			if (prop != null) { _prop = prop; _propLength = prop.Length; }
			this.flags = flags;
			this.skip = skip;
			_resultProp = resultProp;
		}

		/// <summary>
		/// Parses role. Enables Chrome acc if need.
		/// </summary>
		public void RolePrefix(wnd w) {
			if (_roleLength < 4) return;
			int i = _role.IndexOf(':'); if (i < 3) return;
			_flags2 = Cpp_AccRolePrefix(_role, i, w);
			if (_flags2 != 0) {
				_roleLength -= ++i;
				_role = _roleLength > 0 ? _role[i..] : null;
			}
		}
	}

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	static extern int Cpp_AccRolePrefix(string s, int len, wnd w);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern EError Cpp_AccFind(wnd w, Cpp_Acc* aParent, Cpp_AccFindParams ap, Cpp_AccFindCallbackT also, out Cpp_Acc aResult, [MarshalAs(UnmanagedType.BStr)] out string sResult, bool getRects = false);

	internal enum EError {
		NotFound = 0x1001, //UI element not found. With FindAll - no errors. This is actually not an error.
		InvalidParameter = 0x1002, //invalid parameter, for example wildcard expression (or regular expression in it)
		WindowClosed = 0x1003, //the specified window handle is invalid or the window was destroyed while injecting
	}

	internal static bool IsCppError(int hr) {
		return hr >= (int)EError.NotFound && hr <= (int)EError.WindowClosed;
	}

	/// <summary>
	/// flags: 1 not inproc, 2 get only name.
	/// </summary>
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccFromWindow(int flags, wnd w, EObjid objId, out Cpp_Acc aResult, out BSTR sResult);

	internal delegate EXYFlags Cpp_AccFromPointCallbackT(EXYFlags flags, wnd wFP, wnd wTL);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccFromPoint(POINT p, EXYFlags flags, Cpp_AccFromPointCallbackT callback, out Cpp_Acc aResult);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccGetFocused(wnd w, EFocusedFlags flags, out Cpp_Acc aResult);

	//These are called from elm class functions like Cpp.Cpp_Func(this, ...); GC.KeepAlive(this);.
	//We can use 'this' because Cpp_Acc has an implicit conversion from elm operator.
	//Need GC.KeepAlive(this) everywhere. Else GC can collect the elm (and release _iacc) while in the Cpp func.
	//Alternatively could make the Cpp parameter 'const Cpp_Acc&', and pass elm directly. But I don't like it.

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccNavigate(Cpp_Acc aFrom, string navig, out Cpp_Acc aResult);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccGetStringProp(Cpp_Acc a, char prop, out BSTR sResult);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccWeb(Cpp_Acc a, string what, out BSTR sResult);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccGetRect(Cpp_Acc a, out RECT r);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccGetRole(Cpp_Acc a, out ERole roleInt, out BSTR roleStr);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccGetInt(Cpp_Acc a, char what, out int R);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccAction(Cpp_Acc a, char action = 'a', [MarshalAs(UnmanagedType.BStr)] string param = null);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccSelect(Cpp_Acc a, ESelect flagsSelect);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccGetSelection(Cpp_Acc a, out BSTR sResult);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_AccGetProps(Cpp_Acc a, string props, out BSTR sResult);

	/// <param name="flags">1 - wait less.</param>
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_Unload(uint flags);

#if DEBUG
	internal static void DebugUnload() {
		GC.Collect();
		GC.WaitForPendingFinalizers();
		Cpp_Unload(0);
	}
#endif

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	static extern void Cpp_SetHelperCallback(delegate* unmanaged<int, wnd, int> callback);

	[UnmanagedCallersOnly]
	static int _HelperCallback(int action, wnd w) {
		if (action == 1) { //AccEnableChrome asks to detect whether the command line contains --force-renderer-accessibility. If yes, will not try to enable acc.
			if (process.getCommandLine(w.ProcessId, removeProgram: true) is string s) {
				if (s.Contains("--force-renderer-accessibility")) return 1;
				//print.warning("To use UI elements in web pages, start browser with command line --force-renderer-accessibility. Without it the code may fail sometimes or stop working in the future.");
			}
		} else if (action == 2) { //AccEnableChrome failed
			print.warning("To use UI elements in web pages, start browser with command line --force-renderer-accessibility.");
		}
		return 0;
	}

	// OTHER

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr Cpp_ModuleHandle();

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern char* Cpp_LowercaseTable();

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr Cpp_Clipboard(IntPtr hh);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern bool Cpp_ShellExec(in Api.SHELLEXECUTEINFO x, out int pid, out int injectError, out int execError);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern nint Cpp_AccWorkaround(Api.IAccessible a, nint wParam, ref nint obj);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_UEF(bool on);

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_InactiveWindowWorkaround(bool on);
	
	/// <returns>0 failed, 1 x86, 2 x64, 3 arm64</returns>
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_GetProcessArchitecture(int pid);

	// TEST

#if DEBUG

	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_Test();

	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void Cpp_TestWildex(string s, string w);

	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int Cpp_TestInt(int a);

	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int Cpp_TestString(string a, int b, int c);

	//[ComImport, Guid("3426CF3C-F7C2-4322-A292-463DB8729B54"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	//internal interface ICppTest
	//{
	//	[PreserveSig] int TestInt(int a, int b, int c);
	//	[PreserveSig] int TestString([MarshalAs(UnmanagedType.LPWStr)] string a, int b, int c);
	//	[PreserveSig] int TestBSTR(string a, int b, int c);
	//}

	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern ICppTest Cpp_Interface();


	//[ComImport, Guid("57017F56-E7CA-4A7B-A8F8-2AE36077F50D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	//internal interface IThreadExitEvent
	//{
	//	[PreserveSig] int Unsubscribe();
	//}

	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IThreadExitEvent Cpp_ThreadExitEvent(IntPtr callback);

	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern void Cpp_ThreadExitEvent2(IntPtr callback);
#endif
}
