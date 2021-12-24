namespace Au.More
{
	/// <summary>
	/// Assembly functions.
	/// </summary>
	internal static class AssemblyUtil_
	{
		/// <summary>
		/// Returns true if the build configuration of the assembly is Debug. Returns false if Release (optimized).
		/// </summary>
		/// <remarks>
		/// Returns true if the assembly has <see cref="DebuggableAttribute"/> and its <b>IsJITTrackingEnabled</b> is true.
		/// </remarks>
		public static bool IsDebug(Assembly a) => a?.GetCustomAttribute<DebuggableAttribute>()?.IsJITTrackingEnabled ?? false;
		//IsJITTrackingEnabled depends on config, but not 100% reliable, eg may be changed explicitly in source code (maybe IsJITOptimizerDisabled too).
		//IsJITOptimizerDisabled depends on 'Optimize code' checkbox in project Properties, regardless of config.
		//note: GetEntryAssembly returns null in func called by host through coreclr_create_delegate.

		//not used. Don't add Au to GAC, because then process may start slowly, don't know why.
		///// <summary>
		///// Returns true if Au.dll is installed in the global assembly cache.
		///// </summary>
		//internal static bool IsAuInGAC => typeof(AssemblyUtil_).Assembly.GlobalAssemblyCache;

		//no ngen in Core.
		///// <summary>
		///// Returns true if Au.dll is compiled to native code using ngen.exe.
		///// It means - no JIT-compiling delay when its functions are called first time in process.
		///// </summary>
		//internal static bool IsAuNgened => s_auNgened ??= IsNgened(typeof(AssemblyUtil_).Assembly);
		//static bool? s_auNgened;
		////tested: Module.GetPEKind always gets ILOnly.
		////test: new StackFrame().HasNativeImage()

		///// <summary>
		///// Returns true if assembly asm is compiled to native code using ngen.exe.
		///// It means - no JIT-compiling delay when its functions are called first time in process.
		///// </summary>
		//public static bool IsNgened(Assembly asm)
		//{
		//	var s = asm.CodeBase;
		//	//if(asm.GlobalAssemblyCache) return s.Contains("/GAC_MSIL/"); //faster and maybe more reliable, but works only with GAC assemblies
		//	s = s.Substring(s.LastIndexOf('/') + 1);
		//	s = s.Insert(s.LastIndexOf('.') + 1, "ni.");
		//	return default != Api.GetModuleHandle(s);
		//}

		//much slower first time when ngened. Also it is undocumented that GetModuleFileName returns 0 if non-ngened (LOAD_LIBRARY_AS_DATAFILE?).
		//public static unsafe bool IsAssemblyNgened2(Assembly asm)
		//{
		//	var module =asm.GetLoadedModules()[0];
		//	var h = Marshal.GetHINSTANCE(module); //slow first time, especially when ngened
		//	var b = stackalloc char[4];
		//	return 0 != Api.GetModuleFileName(h, b, 4);
		//}

		/// <summary>
		/// Returns flags for loaded assemblies: 1 System.Windows.Forms, 2 WindowsBase (WPF).
		/// </summary>
		internal static int IsLoadedWinformsWpf() {
			if (s_isLoadedWinformsWpf == 0) {
				lock ("zjm5R47f7UOmgyHUVZaf1w") {
					if (s_isLoadedWinformsWpf == 0) {
						var ad = AppDomain.CurrentDomain;
						var a = ad.GetAssemblies();
						foreach (var v in a) {
							_FlagFromName(v);
							if (s_isLoadedWinformsWpf == 3) return 3;
						}
						ad.AssemblyLoad += (_, x) => _FlagFromName(x.LoadedAssembly);
						s_isLoadedWinformsWpf |= 0x100;
					}
				}
			}

			return s_isLoadedWinformsWpf & 3;

			void _FlagFromName(Assembly a) {
				string s = a.FullName; //fast, cached. GetName can be slow because not cached.
				if (0 == (s_isLoadedWinformsWpf & 1) && s.Starts("System.Windows.Forms,")) s_isLoadedWinformsWpf |= 1;
				else if (0 == (s_isLoadedWinformsWpf & 2) && s.Starts("WindowsBase,")) s_isLoadedWinformsWpf |= 2;
			}
		}
		static volatile int s_isLoadedWinformsWpf;
	}
}
