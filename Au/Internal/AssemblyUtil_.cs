namespace Au.More;

/// <summary>
/// Assembly functions.
/// </summary>
internal static class AssemblyUtil_ {
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
