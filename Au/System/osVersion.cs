namespace Au
{
	/// <summary>
	/// Provides Windows version info and current process 32/64 bit info.
	/// </summary>
	/// <remarks>
	/// The Windows version properties return true Windows version. If you need version that depends on manifest and debugger, use <see cref="Environment.OSVersion"/>.
	/// </remarks>
	/// <seealso cref="OperatingSystem"/>
	public static unsafe class osVersion
	{
		static osVersion() {
			Api.RTL_OSVERSIONINFOW x = default; x.dwOSVersionInfoSize = Api.SizeOf(x);
			if (0 == Api.RtlGetVersion(ref x)) {
				_winver = Math2.MakeWord(x.dwMinorVersion, x.dwMajorVersion);
				_winbuild = (int)x.dwBuildNumber;
				//use this because Environment.OSVersion.Version (GetVersionEx) lies, even if we have correct manifest when is debugger present
			} else {
				Debug.Fail("RtlGetVersion");
				var v = Environment.OSVersion.Version;
				_winver = Math2.MakeWord(v.Minor, v.Major);
				_winbuild = v.Build;
			}

			_minWin8 = _winver >= win8;
			_minWin8_1 = _winver >= win8_1;
			_minWin10 = _winver >= win10;
			if (_minWin10) _win10build = _winbuild;
			//print.it(_win10build);

			//this is to remind to add new members for new Windows 10/11 versions
			//Debug_.PrintIf(_win10build > 19044, $"{_win10build} {Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "failed")}");

			_is32BitOS = sizeof(nint) == 4 && !(Api.IsWow64Process(Api.GetCurrentProcess(), out _isWow64) && _isWow64);
		}

		static readonly int _winver, _winbuild, _win10build;
		static readonly bool _minWin8, _minWin8_1, _minWin10;
		static readonly bool _is32BitOS, _isWow64;

		/// <summary>
		/// Gets classic Windows major+minor version value:
		/// Win7 (0x601), Win8 (0x602), Win8_1 (0x603), Win10 (0xA00).
		/// Example: <c>if(osVersion.winVer >= osVersion.win8) ...</c>
		/// </summary>
		public static int winVer => _winver;

		/// <summary>
		/// Gets Windows build number.
		/// For example 14393 for Windows 10 version 1607.
		/// </summary>
		public static int winBuild => _winbuild;

		/// <summary>
		/// Classic Windows version major+minor value that can be used with <see cref="winVer"/>.
		/// Example: <c>if(osVersion.winVer >= osVersion.win8) ...</c>
		/// </summary>
		public const int win7 = 0x601, win8 = 0x602, win8_1 = 0x603, win10 = 0xA00;

		/// <summary>
		/// true if Windows 8.0 or later.
		/// </summary>
		public static bool minWin8 => _minWin8;

		/// <summary>
		/// true if Windows 8.1 or later.
		/// </summary>
		public static bool minWin8_1 => _minWin8_1;

		/// <summary>
		/// true if Windows 10 or later.
		/// </summary>
		public static bool minWin10 => _minWin10;

		/// <summary>
		/// true if Windows 10 version 1607 or later.
		/// </summary>
		public static bool minWin10_1607 => _win10build >= 14393;

		/// <summary>
		/// true if Windows 10 version 1703 or later.
		/// </summary>
		public static bool minWin10_1703 => _win10build >= 15063;

		/// <summary>
		/// true if Windows 10 version 1709 or later.
		/// </summary>
		public static bool minWin10_1709 => _win10build >= 16299;

		/// <summary>
		/// true if Windows 10 version 1803 or later.
		/// </summary>
		public static bool minWin10_1803 => _win10build >= 17134;

		/// <summary>
		/// true if Windows 10 version 1809 or later.
		/// </summary>
		public static bool minWin10_1809 => _win10build >= 17763;

		/// <summary>
		/// true if Windows 10 version 1903 or later.
		/// </summary>
		public static bool minWin10_1903 => _win10build >= 18362;

		/// <summary>
		/// true if Windows 10 version 1909 or later.
		/// </summary>
		public static bool minWin10_1909 => _win10build >= 18363;

		/// <summary>
		/// true if Windows 10 version 2004 or later.
		/// </summary>
		public static bool minWin10_2004 => _win10build >= 19041;

		/// <summary>
		/// true if Windows 10 version 20H2 or later.
		/// </summary>
		public static bool minWin10_20H2 => _win10build >= 19042;

		/// <summary>
		/// true if Windows 10 version 21H1 or later.
		/// </summary>
		public static bool minWin10_21H1 => _win10build >= 19043;

		/// <summary>
		/// true if Windows 10 version 21H2 or later.
		/// </summary>
		public static bool minWin10_21H2 => _win10build >= 19044;

		///// <summary>
		///// true if Windows 10 version 22H2 or later.
		///// </summary>
		//public static bool minWin10_22H2 => _win10build >= 19045;

		/// <summary>
		/// true if Windows 11 or later.
		/// </summary>
		public static bool minWin11 => _win10build >= 22000;

		///// <summary>
		///// true if Windows 11 version 22H2 or later.
		///// </summary>
		//public static bool minWin11_22H2 => _win10build >= 22621;

		/// <summary>
		/// true if this process is 32-bit, false if 64-bit.
		/// The same as <c>sizeof(nint) == 4</c>.
		/// </summary>
		public static bool is32BitProcess => sizeof(nint) == 4;

		/// <summary>
		/// true if Windows is 32-bit, false if 64-bit.
		/// </summary>
		public static bool is32BitOS => _is32BitOS;

		/// <summary>
		/// Returns true if this process is a 32-bit process running on 64-bit Windows. Also known as WOW64 process.
		/// </summary>
		public static bool is32BitProcessAnd64BitOS => _isWow64;
	}
}
