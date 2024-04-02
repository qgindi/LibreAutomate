namespace Au;

/// <summary>
/// Provides Windows version info and current process 32/64 bit info.
/// </summary>
/// <remarks>
/// The Windows version properties return true Windows version. If you need version that depends on manifest and debugger, use <see cref="Environment.OSVersion"/>.
/// </remarks>
/// <seealso cref="OperatingSystem"/>
public static unsafe class osVersion {
	static osVersion() {
		Api.RTL_OSVERSIONINFOW x = default; x.dwOSVersionInfoSize = sizeof(Api.RTL_OSVERSIONINFOW);
		Api.RtlGetVersion(ref x); //use this because Environment.OSVersion.Version (GetVersionEx) lies, even if we have correct manifest when is debugger present
		_winver = Math2.MakeWord(_winminor = (int)x.dwMinorVersion, _winmajor = (int)x.dwMajorVersion);
		_winbuild = (int)x.dwBuildNumber;

		_minWin8 = _winver >= win8;
		_minWin8_1 = _winver >= win8_1;
		_minWin10 = _winver >= win10;
		if (_minWin10) _win10build = _winbuild;
		//print.it(_win10build);

		//this is to remind to add new members for new Windows 10/11 versions
		//Debug_.PrintIf(_win10build > 19044, $"{_win10build} {Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "failed")}");

		_is32BitOS = sizeof(nint) == 4 && !(Api.IsWow64Process(Api.GetCurrentProcess(), out _isWow64) && _isWow64);
	}

	static readonly int _winmajor, _winminor, _winver, _winbuild, _win10build;
	static readonly bool _minWin8, _minWin8_1, _minWin10;
	static readonly bool _is32BitOS, _isWow64;

	/// <summary>
	/// Gets Windows major version.
	/// </summary>
	public static int winMajor => _winmajor;

	/// <summary>
	/// Gets Windows minor version.
	/// </summary>
	public static int winMinor => _winminor;

	/// <summary>
	/// Gets Windows build number.
	/// For example 14393 for Windows 10 version 1607.
	/// </summary>
	public static int winBuild => _winbuild;

	/// <summary>
	/// Gets Windows major and minor version in single <b>int</b>:
	/// <br/>• Win7 - 0x601,
	/// <br/>• Win8 - 0x602,
	/// <br/>• Win8.1 - 0x603,
	/// <br/>• Win10/11 - 0xA00.
	/// 
	/// <para>
	/// Example: <c>if (osVersion.winVer >= osVersion.win8) ...</c>
	/// </para>
	/// </summary>
	public static int winVer => _winver;

	/// <summary>
	/// Windows version major+minor value that can be used with <see cref="winVer"/>.
	/// Example: <c>if (osVersion.winVer >= osVersion.win8) ...</c>
	/// </summary>
	public const int win7 = 0x601, win8 = 0x602, win8_1 = 0x603, win10 = 0xA00;

	/// <summary>
	/// <c>true</c> if Windows 8.0 or later.
	/// </summary>
	public static bool minWin8 => _minWin8;

	/// <summary>
	/// <c>true</c> if Windows 8.1 or later.
	/// </summary>
	public static bool minWin8_1 => _minWin8_1;

	/// <summary>
	/// <c>true</c> if Windows 10 or later.
	/// </summary>
	public static bool minWin10 => _minWin10;

	/// <summary>
	/// <c>true</c> if Windows 10 version 1607 or later.
	/// </summary>
	public static bool minWin10_1607 => _win10build >= 14393;

	/// <summary>
	/// <c>true</c> if Windows 10 version 1703 or later.
	/// </summary>
	public static bool minWin10_1703 => _win10build >= 15063;

	/// <summary>
	/// <c>true</c> if Windows 10 version 1709 or later.
	/// </summary>
	public static bool minWin10_1709 => _win10build >= 16299;

	/// <summary>
	/// <c>true</c> if Windows 10 version 1803 or later.
	/// </summary>
	public static bool minWin10_1803 => _win10build >= 17134;

	/// <summary>
	/// <c>true</c> if Windows 10 version 1809 or later.
	/// </summary>
	public static bool minWin10_1809 => _win10build >= 17763;

	/// <summary>
	/// <c>true</c> if Windows 10 version 1903 or later.
	/// </summary>
	public static bool minWin10_1903 => _win10build >= 18362;

	/// <summary>
	/// <c>true</c> if Windows 10 version 1909 or later.
	/// </summary>
	public static bool minWin10_1909 => _win10build >= 18363;

	/// <summary>
	/// <c>true</c> if Windows 10 version 2004 or later.
	/// </summary>
	public static bool minWin10_2004 => _win10build >= 19041;

	/// <summary>
	/// <c>true</c> if Windows 10 version 20H2 or later.
	/// </summary>
	public static bool minWin10_20H2 => _win10build >= 19042;

	/// <summary>
	/// <c>true</c> if Windows 10 version 21H1 or later.
	/// </summary>
	public static bool minWin10_21H1 => _win10build >= 19043;

	/// <summary>
	/// <c>true</c> if Windows 10 version 21H2 or later.
	/// </summary>
	public static bool minWin10_21H2 => _win10build >= 19044;

	/// <summary>
	/// <c>true</c> if Windows 10 version 22H2 or later.
	/// </summary>
	public static bool minWin10_22H2 => _win10build >= 19045;

	/// <summary>
	/// <c>true</c> if Windows 11 or later.
	/// </summary>
	public static bool minWin11 => _win10build >= 22000;

	/// <summary>
	/// <c>true</c> if Windows 11 version 22H2 or later.
	/// </summary>
	public static bool minWin11_22H2 => _win10build >= 22621;

	/// <summary>
	/// <c>true</c> if Windows 11 version 23H2 or later.
	/// </summary>
	public static bool minWin11_23H2 => _win10build >= 22631;

	/// <summary>
	/// <c>true</c> if this process is 32-bit, <c>false</c> if 64-bit.
	/// The same as <c>sizeof(nint) == 4</c>.
	/// </summary>
	public static bool is32BitProcess => sizeof(nint) == 4;

	/// <summary>
	/// <c>true</c> if Windows is 32-bit, <c>false</c> if 64-bit.
	/// </summary>
	public static bool is32BitOS => _is32BitOS;

	/// <summary>
	/// Returns <c>true</c> if this process is a 32-bit process running on 64-bit Windows. Also known as WOW64 process.
	/// </summary>
	public static bool is32BitProcessAnd64BitOS => _isWow64;

	/// <summary>
	/// Gets string containing OS version, .NET version and <c>Au.dll</c> version, like <c>"10.0.22621-64|6.0.8|1.2.3"</c>.
	/// Can be used for example to rebuild various caches when it's changed.
	/// </summary>
	public static string onaString => _environment.Value;
	static readonly Lazy<string> _environment = new(() => $"{_winmajor.ToS()}.{_winminor.ToS()}.{_winbuild.ToS()}-{(_is32BitOS ? "32" : "64")}|{Environment.Version}|{Au_.Version}");
}
