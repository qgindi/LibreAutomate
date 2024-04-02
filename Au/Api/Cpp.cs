namespace Au.Types;

[DebuggerStepThrough]
internal static unsafe partial class Cpp {
	static Cpp() {
		filesystem.more.LoadDll64or32Bit_("AuCpp.dll");
	}
	
	//speed:
	//	Calling DllImport functions is 4-5 times slower than C# functions. (tested with the old .NET Framework, now should be faster)
	//	Calling COM functions is 2-10 times slower than DllImport functions.
	//	Tested with int and string parameters, with default marshaling and with 'fixed'.
	//	If only int parameters, DllImport is only 50% slower than C#. But COM slow anyway.
	//	Strings passed to COM methods by default are converted to BSTR, and a new BSTR is allocated/freed.
	
	internal struct Cpp_Acc {
		public IntPtr acc;
		public int elem;
		public elm.Misc_ misc;
		
		public Cpp_Acc(IntPtr iacc, int elem_) { acc = iacc; elem = elem_; misc = default; }
		public Cpp_Acc(elm e) { acc = e._iacc; elem = e._elem; misc = e._misc; }
		public static implicit operator Cpp_Acc(elm e) => new(e);
	}
	
	internal delegate int Cpp_AccFindCallbackT(Cpp_Acc a);
	
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
	internal static extern EError Cpp_AccFind(wnd w, Cpp_Acc* aParent, Cpp_AccFindParams ap, Cpp_AccFindCallbackT also, out Cpp_Acc aResult, [MarshalAs(UnmanagedType.BStr)] out string sResult);
	
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
	
	// OTHER
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr Cpp_ModuleHandle();
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern char* Cpp_LowercaseTable();
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr Cpp_Clipboard(IntPtr hh);
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int Cpp_CallIDroptarget(IntPtr dt, int ddEvent, [MarshalAs(UnmanagedType.IUnknown)] object d, int keyState, POINT pt, ref int pdwEffect);
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern bool Cpp_ShellExec(in Api.SHELLEXECUTEINFO x, out int pid, out int injectError, out int execError);
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern nint Cpp_AccWorkaround(Api.IAccessible a, nint wParam, ref nint obj);
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_UEF(bool on);
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_InactiveWindowWorkaround(bool on);
	
	// TEST
	
#if DEBUG
	
	[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void Cpp_Test();
	
	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern IntPtr Cpp_Speak(string text, int flags, string voice, int rate, int volume);
	
	///// <param name="what">
	///// 0 - Release.
	///// 1 - Pause.
	///// 2 - Resume.
	///// 3 - Skip value sentences.
	///// 4 - Skip value ms.
	///// 5 - Get SpeakCompleteEvent.
	///// 6 - Get status.
	///// </param>
	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern nint Cpp_SpeakControl(IntPtr voice, int what, int value);
	
	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern int* EnumWindowsEx(out int len, bool onlyVisible, int api);
	
	//[DllImport("AuCpp.dll", CallingConvention = CallingConvention.Cdecl)]
	//internal static extern nint Cpp_InputSync(int action, int tid, nint hh);
	
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
