using System.Runtime.Loader;

namespace Au.More;

/// <summary>
/// Assembly functions.
/// </summary>
internal static class AssemblyUtil_ {
	public static Assembly GetEntryAssembly() {
		var r = Assembly.GetEntryAssembly();
		if (r == null) {
			//info: null in miniProgram on AssemblyLoadContext.Default.Resolving event when the script is like:
			//	'class Program : SomeType { static Main() { } }' where SomeType is the assembly being resolved.

			//Debug_.Print("Assembly.GetEntryAssembly null");
			if (script.role == SRole.MiniProgram) {
				var s = "~" + script.name;
				foreach (var v in AssemblyLoadContext.Default.Assemblies) if (v.GetName().Name == s) return v;
			}
		}
		return r;
	}

	/// <summary>
	/// Returns <c>true</c> if the build configuration of the assembly is Debug. Returns <c>false</c> if Release (optimized).
	/// </summary>
	/// <remarks>
	/// Returns <c>true</c> if the assembly has <see cref="DebuggableAttribute"/> and its <b>IsJITTrackingEnabled</b> is <c>true</c>.
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
