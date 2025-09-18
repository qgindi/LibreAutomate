using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows;
using System.Collections;
using System.IO.Compression;
using System.Runtime.Loader;
using System.Security.Cryptography;

/// <summary>
/// .NET SDK etc.
/// </summary>
static class DotnetUtil {
	static DotnetUtil() {
		Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
	}
	
	/// <summary>
	/// Call this when showing a window that uses .NET SDK.
	/// If SDK missing (full or minimal, depending on <see cref="AppSettings.minimalSDK"/>), disables specified UI elements and adds an install link at the top.
	/// </summary>
	/// <param name="installedNow">Called later when SDK is installed if was missing. Called only if then <c>w.IsLoaded</c>.</param>
	/// <returns>true if missing.</returns>
	public static bool MissingSdkUI(Window w, FrameworkElement[] disable, Action installedNow = null) {
		if (InitSdk()) return false;
		
		var content = w.Content as UIElement;
		w.Content = null;
		if (w.Width < 400) w.Width = 400;
		_DisableEnableAll(enable: false);
		var panel = new DockPanel();
		
		TextBlock t = new() { Height = 18, Margin = new(5) };
		if (App.Settings.minimalSDK == false) wpfBuilder.formatTextOf(t, $"Missing .NET SDK. See <b>Options > Other > Use minimal .NET SDK</b>.");
		else wpfBuilder.formatTextOf(t, $"<b><a {_InstallMinimalSdk}>Download required components</a></b> to use this feature (~26 MB)");
		
		UIElement[] a = [t, new Separator(), content];
		foreach (var e in a) {
			if (e != content) DockPanel.SetDock(e, Dock.Top);
			panel.Children.Add(e);
		}
		w.Content = panel;
		
		return true;
		
		async void _InstallMinimalSdk() {
			if (!await EnsureSdkAsync(t)) return;
			_DisableEnableAll(enable: true);
			panel.Children.Remove(content);
			w.Content = content;
			if (installedNow is { } k && w.IsLoaded) k();
		}
		void _DisableEnableAll(bool enable) { foreach (var v in disable) v.IsEnabled = enable; }
	}
	
	/// <summary>
	/// Ensures that the full or minimal .NET SDK is installed, depending on <see cref="AppSettings.minimalSDK"/>.
	/// If not installed, downloads/installs the minimal SDK (without confirmation, just displays progress).
	/// In any case sets <see cref="DotnetExe"/> and <see cref="SdkDir"/>.
	/// Not thread-safe. Call in main thread.
	/// </summary>
	/// <param name="progress">This func will set text like <c>"Downloading minimal SDK, 20%"</c>. Also uses it to cancel when the window closed.</param>
	/// <returns>false if failed to install or canceled.</returns>
	public static async Task<bool> EnsureSdkAsync(TextBlock progress) {
		if (_InitSdk(out bool installMiniSdk)) return true;
		if (!installMiniSdk) return false;
		if (!await _InstallMinimalSDK(progress)) return false;
		return _InitSdk(out _, true);
		
		static async Task<bool> _InstallMinimalSDK(TextBlock progress) {
			var window = Window.GetWindow(progress);
			
			string filename = $"sdk-{Environment.Version.ToString(2)}-{(osVersion.isArm64OS ? "arm64" : "x64")}.zip";
			string url = $"https://www.libreautomate.com/download/sdk/{filename}";
			string dotnetDir = folders.ThisAppBS + "SDK";
			using var dl = new Downloader();
			if (!dl.PrepareDirectory(dotnetDir, failedWarningPrefix: c_errorInstallMinimalSDK)) return false;
			
			CancellationTokenSource cts = new();
			progress.Unloaded += (_, _) => { cts?.Cancel(); };
			bool? ok = await dl.DownloadAndExtract(url, progress, cts.Token, progressText: "Downloading required components", failedWarningPrefix: c_errorInstallMinimalSDK.Replace("to install", "to download"));
			cts = null;
			return ok == true;
		}
		
		//note: the minimal SDK the first time installs 3 extra packages (16 MB download). The normal SDK doesn't.
		//	It's OK. It's because the minimal SDK doesn't have the `packs` folder. Auto-downloads what's missing.
		//	Also creates empty dir `metadata` in the minimal SDK dir.
	}
	
	/// <summary>
	/// Returns <c>true</c> if the full or minimal .NET SDK is installed (depending on <see cref="AppSettings.minimalSDK"/>).
	/// Sets <see cref="DotnetExe"/> and <see cref="SdkDir"/> (null if returns <c>false</c>).
	/// Not thread-safe. Call in main thread.
	/// </summary>
	public static bool InitSdk() => _InitSdk(out _);
	
	static bool _InitSdk(out bool installMiniSdk, bool afterInstall = false) {
		installMiniSdk = false;
		
		if (!afterInstall) {
			Debug_.PrintIf(!App.IsMainThread);
			if (App.IsPortable) {
				if (_IsFullSdkInstalled()) return true;
				print.warning("This feature in portable mode requires the .NET SDK.", 1);
				return false;
			}
			if (App.Settings.minimalSDK != true) {
				if (_IsFullSdkInstalled()) return true;
				if (App.Settings.minimalSDK == false) {
					(DotnetExe, SdkDir) = (null, null);
					print.warning("This feature requires the .NET SDK. Install it or don't uncheck Options > Other > Use minimal SDK.", 1);
					Debug_.Print(new StackTrace(true));
					return false;
				}
			}
		}
		
		if (s_minimalSdkDir is null) {
			string dotnetDir = folders.ThisAppBS + @"SDK", sdkDir = dotnetDir + @"\sdk", sdkVerDir = null;
			if (filesystem.exists(sdkDir, true).Directory && !Downloader.IsDirectoryInvalid(dotnetDir)) {
				sdkVerDir = Directory.EnumerateDirectories(sdkDir, Environment.Version.ToString(2) + ".*").FirstOrDefault();
			}
			if (sdkVerDir is null) {
				installMiniSdk = true;
			} else {
				if (sdkVerDir != null && !(_EnsureLink("host") && _EnsureLink("shared"))) sdkVerDir = null; //not just when installing, because the folder may be copied and the links lost
				
				if (sdkVerDir != null) (s_minimalDotnetExe, s_minimalSdkDir) = (dotnetDir + @"\dotnet.exe", sdkVerDir);
			}
		}
		
		DotnetExe = s_minimalDotnetExe;
		SdkDir = s_minimalSdkDir;
		return s_minimalSdkDir != null;
		
		static bool _IsFullSdkInstalled() {
			if (!s_fullSdkDirOnce) {
				if (filesystem.searchPath("dotnet.exe") is string dotnetExe) {
					try {
						if (0 == run.console(out string v, dotnetExe, "--version", curDir: folders.ThisApp)) { //fast. Use `curDir: folders.ThisApp` because it searches for global.json (and SDK version in it) in this and ancestor dirs.
							var dir = pathname.getDirectory(dotnetExe) + @"\sdk\" + v.Trim();
							if (filesystem.exists(dir, true).Directory) (s_fullDotnetExe, s_fullSdkDir) = (dotnetExe, dir);
							Debug_.PrintIf(s_fullSdkDir is null, dir);
						}
					}
					catch { }
				}
				s_fullSdkDirOnce = true;
			}
			DotnetExe = s_fullDotnetExe;
			SdkDir = s_fullSdkDir;
			return s_fullSdkDir != null;
		}
		
		static bool _EnsureLink(string name) {
			var link = folders.ThisAppBS + @"SDK\" + name;
			if (!filesystem.exists(link, true).IsNtfsLink) {
				var fullDotnetDir = folders.NetRuntime.Path.RxReplace(@"(\\[^\\]+){3}$", "", 1);
				try { filesystem.more.createSymbolicLink(link, fullDotnetDir + @"\" + name, CSLink.Junction, deleteOld: true); }
				catch (Exception ex) { print.warning(ex, c_errorInstallMinimalSDK); return false; }
			}
			return true;
		}
		
		//note: the minimal SDK the first time installs 3 extra packages (16 MB download). The normal SDK doesn't.
		//	It's OK. It's because the minimal SDK doesn't have the `packs` folder. Auto-downloads what's missing.
		//	Also creates empty dir `metadata` in the minimal SDK dir.
	}
	static string s_minimalDotnetExe, s_fullDotnetExe, s_minimalSdkDir, s_fullSdkDir;
	static bool s_fullSdkDirOnce;
	const string c_errorInstallMinimalSDK = "<>Failed to install required component (minimal .NET SDK). You can retry, or manually install the full .NET 9.0 SDK (~200 MB download). See also Options > Other > Use minimal SDK. Issues: https://github.com/qgindi/LibreAutomate/issues.";
	
	/// <summary>
	/// Path of "dotnet.exe" set by <see cref="EnsureSdkAsync"/>. Full or minimal SDK.
	/// </summary>
	public static string DotnetExe { get; private set; }
	
	/// <summary>
	/// Path of the SDK directory with dlls, set by <see cref="EnsureSdkAsync"/>. Full or minimal SDK.
	/// </summary>
	public static string SdkDir { get; private set; }
	
	/// <summary>
	/// Called by App._Assembly_Resolving.
	/// If <c>name.Starts("NuGet.")</c>, and SDK exists (full or minimal), loads the assembly from the SDK dir. Later its dependencies will be loaded automatically.
	/// </summary>
	internal static Assembly TryLoadAssembly_(string name) {
		if (!name.Starts("NuGet.")) return null;
		Debug_.PrintIf(!(name is "NuGet.Configuration" or "NuGet.Versioning"));
		if (s_alc is null) {
			if (!InitSdk()) return null;
			s_alc = new(SdkDir);
		}
		return s_alc.LoadFromSdkDir(name);
	}
	
	class _AssemblyLoadContext(string sdkDir) : AssemblyLoadContext {
		protected override Assembly Load(AssemblyName assemblyName) {
			var name = assemblyName.Name;
			if (name != "netstandard") {
				Debug_.PrintIf(!(name is "NuGet.Common" or "System.Security.Cryptography.ProtectedData" or "System.Runtime" or "System.Runtime.InteropServices"), name);
				var s = $@"{sdkDir}\{name}.dll";
				if (filesystem.exists(s, true)) return LoadFromAssemblyPath(s);
				//Debug_.Print(name);
			}
			return base.Load(assemblyName);
		}
		
		public Assembly LoadFromSdkDir(string assemblyName) => LoadFromAssemblyPath(@$"{sdkDir}\{assemblyName}.dll");
	}
	static _AssemblyLoadContext s_alc;
	
	/// <summary>
	/// Downloads and extracts both .NET runtimes (core and desktop) for CPU architecture x64/ARM64 other than of this process.
	/// Run in a background thread.
	/// </summary>
	/// <param name="extractDir">Extract both to this directory.</param>
	/// <param name="portable">Extract to <i>dir</i> subdirectory <c>"dotnet"</c> (if x64) or <c>"dotnetARM"</c> (if ARM64). Delete old subdirectory.</param>
	/// <exception cref="OperationCanceledException">User-canceled.</exception>
	/// <exception cref="Exception">Failed.</exception>
	public static void DownloadNetRuntimesForOtherArch(string extractDir, bool portable) {
		bool forArm = !osVersion.isArm64Process;
		
		if (portable) {
			extractDir = extractDir + "\\dotnet" + (forArm ? "ARM" : null);
			filesystem.delete(extractDir);
			filesystem.createDirectory(extractDir);
		}
		
		string arch = forArm ? "arm64" : "x64";
		string version = Environment.Version.ToString();
		
		_ClearOld();
		_DownloadAndExtract(false);
		_DownloadAndExtract(true);
		
		void _DownloadAndExtract(bool desktop) {
			//"https://builds.dotnet.microsoft.com/dotnet/Runtime/9.0.6/dotnet-runtime-9.0.6-win-arm64.zip"
			//"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.6/windowsdesktop-runtime-9.0.6-win-arm64.zip"
			var filename = $"{(desktop ? "windowsdesktop" : "dotnet")}-runtime-{version}-win-{arch}.zip";
			var zip = $@"{folders.ThisAppTemp}\download\{filename}";
			if (!filesystem.exists(zip)) {
				print.it("Downloading " + filename);
				var url = $"https://builds.dotnet.microsoft.com/dotnet/{(desktop ? "WindowsDesktop" : "Runtime")}/{version}/{filename}";
				var r = internet.http.Get(url, dontWait: true);
				if (!r.Download(zip + "~")) throw new OperationCanceledException();
				filesystem.rename(zip + "~", filename);
			}
			
			print.it("Extracting " + filename);
			var starts = $"shared/Microsoft.{(desktop ? "WindowsDesktop" : "NETCore")}.App/{version}/";
			using var z = ZipFile.OpenRead(zip);
			foreach (var e in z.Entries) {
				var relPath = e.FullName;
				if (!relPath.Starts(starts, true)) continue;
				relPath = relPath[starts.Length..];
				var s = extractDir + "\\" + relPath;
				if (relPath.Contains('/')) filesystem.createDirectoryFor(s);
				e.ExtractToFile(s, overwrite: true);
			}
		}
		
		void _ClearOld() {
			try {
				var rx = new regexp(@"(?i)^(?:dotnet|windowsdesktop)-runtime-(.+?)-.+\.zip$");
				foreach (var f in filesystem.enumFiles(folders.ThisAppTemp + "download")) {
					if (!rx.Match(f.Name, out var m)) continue;
					if (m[1].Value.Eqi(version)) continue;
					Api.DeleteFile(f.FullPath);
				}
			}
			catch { }
		}
	}
}

/// <summary>
/// Can be used to return bool and error text. Has implicit conversions from/to bool.
/// </summary>
record struct BoolError(bool ok, string error) {
	public static implicit operator bool(BoolError x) => x.ok;
	public static implicit operator BoolError(bool ok) => new(ok, ok ? null : "Failed.");
}

#if DEBUG

static class EdDebug {
}

#endif

static class EdWpf {
	public static TextBlock TextAndHelp(string text, string helpTopic) {
		helpTopic = HelpUtil.AuHelpUrl(helpTopic);
		var img = ImageUtil.LoadWpfImageElement("*Entypo.HelpWithCircle #008EEE @14");
		var s1 = text.NE() ? "" : "  ";
		return wpfBuilder.formattedText($"{text}{s1}<a href=\"{helpTopic}\">{img}</a>");
	}
	
	public static TextBlock TextAndHelp<T>(string text) => TextAndHelp(text, typeof(T).ToString());
	
	public static TextBlock TextAndHelp(string text, Func<string> helpTopic) {
		var img = ImageUtil.LoadWpfImageElement("*Entypo.HelpWithCircle #008EEE @14");
		var s1 = text.NE() ? "" : "  ";
		return wpfBuilder.formattedText($"{text}{s1}<a {() => { HelpUtil.AuHelp(helpTopic()); }}>{img}</a>");
	}
}

/// <summary>
/// Opens databases ref.db, doc.db (created by project DatabasesEtc) or winapi.db (created by script "SDK create database").
/// </summary>
static class EdDatabases {
	public static sqlite OpenRef() => _Open("ref.db");
	
	public static sqlite OpenDoc() => _Open("doc.db");
	
	public static sqlite OpenWinapi() => _Open("winapi.db");
	
	static sqlite _Open(string name) {
		var path = folders.ThisAppBS + name;
		//if (App.IsAuHomePC) { //no. Instead exit editor before running DatabasesEtc project. And it does not lock winapi.db.
		//	var pathNew = path + ".new";
		//	if (filesystem.exists(pathNew)) filesystem.move(pathNew, path, FIfExists.Delete);
		//}
		return new sqlite(path, SLFlags.SQLITE_OPEN_READONLY);
	}
	
	public static string WinapiFile => folders.ThisAppBS + "winapi.db";
}

/// <summary>
/// Temporarily disables window redrawing.
/// Ctor sends WM_SETREDRAW(0) if visible.
/// If was visible, Dispose sends WM_SETREDRAW(1) and calls RedrawWindow.
/// </summary>
struct WndSetRedraw : IDisposable {
	wnd _w;
	
	public WndSetRedraw(wnd w) {
		_w = w;
		if (_w.IsVisible) _w.Send(Api.WM_SETREDRAW, 0); else _w = default;
	}
	
	public unsafe void Dispose() {
		if (_w.Is0) return;
		_w.Send(Api.WM_SETREDRAW, 1);
		Api.RedrawWindow(_w, flags: Api.RDW_ERASE | Api.RDW_FRAME | Api.RDW_INVALIDATE | Api.RDW_ALLCHILDREN);
		_w = default;
	}
}

record struct StartEndText(int start, int end, string text) {
	public int Length => end - start;
	public Range Range => start..end;
	
	/// <inheritdoc cref="ReplaceAll(string, List{StartEndText}, ref StringBuilder)"/>
	public static string ReplaceAll(string s, List<StartEndText> a) {
		StringBuilder b = null;
		ReplaceAll(s, a, ref b);
		return b.ToString();
	}
	
	/// <summary>
	/// Replaces all text ranges specified in <i>a</i> with strings specified in <i>a</i>.
	/// </summary>
	/// <param name="a">Text ranges and replacement texts. Must be sorted by range. Ranges must not overlap.</param>
	/// <param name="b">Receives new text. If null, the function creates new, else at first calls <c>b.Clear()</c>.</param>
	/// <exception cref="ArgumentException">Ranges are overlapped or not sorted. Only #if DEBUG.</exception>
	public static void ReplaceAll(string s, List<StartEndText> a, ref StringBuilder b) {
		ThrowIfNotSorted(a);
		int cap = s.Length - a.Sum(o => o.Length) + a.Sum(o => o.text.Length);
		if (b == null) b = new(cap);
		else {
			b.Clear();
			b.EnsureCapacity(cap);
		}
		
		int i = 0;
		foreach (var v in a) {
			b.Append(s, i, v.start - i).Append(v.text);
			i = v.end;
		}
		b.Append(s, i, s.Length - i);
	}
	
	/// <exception cref="ArgumentException">Ranges are overlapped or not sorted. [Conditional("DEBUG")].</exception>
	[Conditional("DEBUG")]
	internal static void ThrowIfNotSorted(List<StartEndText> a) {
		for (int i = 1; i < a.Count; i++) if (a[i].start < a[i - 1].end) throw new ArgumentException("ranges must be sorted and not overlapped");
	}
}

static class EdComUtil {
	
	//To convert a COM type library we use TypeLibConverter class. However .NET Core+ does not have it.
	//Workaround: the code is in Au.Net4.exe. It uses .NET Framework 4.x. We call it through run.console.
	//We don't use tlbimp.exe:
	//	1. If some used interop assemblies are in GAC (eg MS Office PIA), does not create files for them. But we cannot use GAC in a Core+ app.
	//	2. Does not tell what files created.
	//	3. My PC somehow has MS Office PIA installed and there is no uninstaller. After deleting the GAC files tlbimp.exe created all files, but it took several minutes.
	//Tested: impossible to convert .NET Framework TypeLibConverter code. Part of it is in extern methods.
	//Tested: cannot use .NET Framework dll for it. Fails at run time because uses Core+ assemblies, and they don't have the class. Need exe.
	public static async Task<List<string>> ConvertTypeLibrary(object tlDef, Window owner) {
		string comDll = null;
		switch (tlDef) {
		case string path:
			comDll = path;
			break;
		case RegTypelib r:
			//can be several locales
			var aloc = new List<string>(); //registry keys like "0" or "409"
			var aloc2 = new List<string>(); //locale names for display in the list dialog
			using (var verKey = Registry.ClassesRoot.OpenSubKey($@"TypeLib\{r.guid}\{r.version}")) {
				foreach (var s1 in verKey.GetSubKeyNames()) {
					int lcid = s1.ToInt(0, out int iEnd, STIFlags.IsHexWithout0x);
					if (iEnd != s1.Length) continue; //"FLAGS" etc; must be hex number without 0x
					aloc.Add(s1);
					var s2 = "Neutral";
					if (lcid > 0) {
						try { s2 = new System.Globalization.CultureInfo(lcid).DisplayName; } catch { s2 = s1; }
					}
					aloc2.Add(s2);
				}
			}
			string locale;
			if (aloc.Count == 1) locale = aloc[0];
			else {
				int i = dialog.showList(aloc2, "COM type library locale", owner: owner);
				if (i == 0) return null;
				locale = aloc[i - 1];
			}
			comDll = r.GetPath(locale);
			if (comDll == null /*|| !filesystem.exists(comDll).File*/) { //can be "filepath/resource"
				print.it("Failed to get file path.");
				return null;
			}
			break;
		}
		
		print.it("<><lc YellowGreen>Converting COM type library to .NET assembly.<>");
		List<string> converted = [];
		int rr = -1;
		owner.IsEnabled = false;
		try {
			await Task.Run(() => {
				var dir = folders.Workspace + @".interop\";
				filesystem.createDirectory(dir);
				void _Callback(string s) {
					//skip some useless warnings, eg "can't convert some internal type"
					bool mute = s.RxIsMatch(@"Warning: .+ (?:could not convert the signature for the member '(?:\w*_|tag|[A-Z]+\.)|as a pointer and may require unsafe code to manipulate.|to import this property as a method instead.)");
					if (!mute) print.it(s);
					if (s.Starts("<>Converted: ")) {
						s.RxMatch(@"<explore .+?>(.+?)<>$", 1, out s);
						converted.Add(s);
					}
				}
				rr = run.console(_Callback, folders.ThisAppBS + "Au.Net4.exe", $"/typelib \"{dir}|{comDll}\"");
			});
		}
		catch (Exception ex) { print.it("Failed to convert type library", ex.ToStringWithoutStack()); }
		owner.IsEnabled = true;
		if (rr != 0) return null;
		print.it("==== DONE ====");
		return converted;
	}
	
	public record class RegTypelib(string text, string guid, string version) {
		public override string ToString() => text;
		
		public string GetPath(string locale) {
			var k0 = $@"TypeLib\{guid}\{version}\{locale}\win";
			for (int i = 0; i < 2; i++) {
				var bits = osVersion.is32BitProcess == (i == 1) ? "32" : "64";
				using var hk = Registry.ClassesRoot.OpenSubKey(k0 + bits);
				if (hk?.GetValue("") is string path) return path.Trim('"');
			}
			return null;
		}
	}
	
}

/// <summary>
/// Gets relative path from full path when recursively enumerating a directory. Optionally with a prefix. Almost no garbage.
/// </summary>
class RelativePath {
	char[] _a;
	int _fullPathBaseLen;
	string _prefix;
	
	public RelativePath(string prefix) {
		_prefix = prefix ?? "";
		_a = new char[_prefix.Length + 300];
		prefix.CopyTo(_a);
	}
	
	public RStr GetRelativePath(string fullPath) {
		if (_fullPathBaseLen == 0) _fullPathBaseLen = fullPath.LastIndexOf('\\');
		int len1 = fullPath.Length - _fullPathBaseLen, len2 = len1 + _prefix.Length;
		if (_a.Length < len2) Array.Resize(ref _a, len2 + 300);
		fullPath.CopyTo(_fullPathBaseLen, _a, _prefix.Length, len1);
		return _a.AsSpan(0, len2);
	}
}

/// <summary>
/// Wildcard-compares paths with a list of strings.
/// </summary>
class WildcardList {
	readonly string[] _a;
	
	/// <summary>
	/// Creates a list of strings from a multiline string.
	/// </summary>
	/// <param name="list">Multiline string. Use <c>//</c> to comment out a line.</param>
	/// <param name="replaceSlash">Replace <c>'/'</c> with <c>'\\'</c> (to match file paths).</param>
	/// <param name="matchWithoutBackslashAt0">Let `\folder` match `folder`; and let `*\folder` match both `folder` and `ancestors\folder`.</param>
	public WildcardList(string list, bool replaceSlash = true, bool matchWithoutBackslashAt0 = false) {
		if (list.NE()) {
			_a = [];
		} else {
			List<string> a = [];
			foreach (var s_ in list.Lines(noEmpty: true)) {
				string s = s_;
				if (s.Starts("//")) continue;
				if (replaceSlash) s = s.Replace('/', '\\');
				if (s.FindNot(replaceSlash ? @"*\" : "*") < 0) continue; //wildcard like "*" or "\*" or "\" etc makes no sense. Either matches always or never. Prevent accidentally excluding all files etc.
				if (matchWithoutBackslashAt0 && (s[0] == '\\' || s.Starts(@"*\"))) {
					if (s[0] == '\\') { //`\folder`: match `folder`
						a.Add(s[1..]);
					} else { //`*\folder`: match both `folder` and `ancestors\folder`
						a.Add(s[2..]);
						a.Add(s);
					}
				} else {
					a.Add(s);
				}
			}
			_a = a.ToArray();
		}
	}
	
	/// <summary>
	/// Sets <c>ListOfStrings = list</c>.
	/// </summary>
	public WildcardList(string[] list) {
		_a = list;
	}
	
	/// <summary>
	/// Wildcard-compares <i>s</i> with the list of wildcard strings.
	/// </summary>
	/// <returns>true if a <i>s</i> matches a wildcard string.</returns>
	public bool IsMatch(RStr s, bool ignoreCase) {
		foreach (var v in _a) if (s.Like(v, ignoreCase)) return true;
		return false;
	}
	
	public string[] ListOfStrings => _a;
}

/// <summary>
/// Disables/reenables a <b>Window</b>. Supports nested disabling (uses reference counting).
/// Example:
/// <c>WindowDisabler _disabler;
/// ...
/// _disabler = new(this);
/// ...
/// using var _ = _disabler.Disable();</c>
/// </summary>
class WindowDisabler {
	readonly Window _window;
	int _count;
	
	public WindowDisabler(Window window) => _window = window;
	
	public IDisposable Disable() {
		if (_count++ == 0) _window.IsEnabled = false;
		return new ReenableDisposable(this);
	}
	
	class ReenableDisposable : IDisposable {
		readonly WindowDisabler _d;
		bool _disposed;
		
		public ReenableDisposable(WindowDisabler d) => _d = d;
		
		public void Dispose() {
			if (_disposed) return;
			_disposed = true;
			if (--_d._count == 0)
				_d._window.IsEnabled = true;
		}
	}
}
