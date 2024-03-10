namespace Au {
	/// <summary>
	/// Gets [](xref:uac) integrity level and other security info of this and other processes.
	/// </summary>
	/// <remarks>
	/// An <b>uacInfo</b> variable contains a process access token handle that is used to get security info. Always dispose <b>uacInfo</b> variables to close the handle.
	/// </remarks>
	public sealed class uacInfo : IDisposable {
		///
		~uacInfo() => _htoken.Dispose();

		///
		public void Dispose() {
			_htoken.Dispose();
			GC.SuppressFinalize(this);
		}

		Handle_ _htoken;
		HandleRef _HtokenHR => new HandleRef(this, _htoken);

		/// <summary>
		/// The access token handle.
		/// </summary>
		/// <remarks>
		/// The handle is managed by this variable and will be closed when disposing or GC-collecting it. Use <see cref="GC.KeepAlive"/> where need.
		/// </remarks>
		public IntPtr UnsafeTokenHandle => _htoken;

		/// <summary>
		/// Returns <c>true</c> if the last called property function failed.
		/// Normally it should never fail. Only <see cref="ofProcess"/> can fail (then it returns <c>null</c>).
		/// </summary>
		public bool Failed { get; private set; }

		/// <summary>
		/// Gets the [](xref:uac) elevation type of the process.
		/// </summary>
		public UacElevation Elevation {
			get {
				if (_haveElevation == 0) {
					unsafe {
						UacElevation elev;
						if (!Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenElevationType, &elev, 4, out _)) _haveElevation = 2;
						else {
							_haveElevation = 1;
							_Elevation = elev;
						}
					}
				}
				if (Failed = (_haveElevation == 2)) return UacElevation.Unknown;
				return _Elevation;
			}
		}
		UacElevation _Elevation; byte _haveElevation;

		/// <summary>
		/// Returns <c>true</c> if the process has [](xref:uac) uiAccess property.
		/// A uiAccess process can access/automate all windows of processes running in the same user session.
		/// </summary>
		/// <remarks>
		/// Most processes don't have this property. They cannot access/automate windows of higher integrity level (High, System, uiAccess) processes and Windows 8 store apps. For example, cannot send keys and Windows messages.
		/// Note: High IL (admin) processes also can have this property, therefore <c>IsUIAccess</c> is not the same as <c>IntegrityLevel==IL.UIAccess</c> (<see cref="IntegrityLevel"/> returns <b>UIAccess</b> only for Medium+uiAccess processes; for High+uiAccess processes it returns <b>High</b>). Some Windows API work slightly differently with uiAccess and non-uiAccess admin processes.
		/// This property is rarely useful. Instead use other properties of this class.
		/// </remarks>
		public bool IsUIAccess {
			get {
				if (_haveIsUIAccess == 0) {
					unsafe {
						uint uia;
						if (!Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenUIAccess, &uia, 4, out var siz)) _haveIsUIAccess = 2;
						else {
							_haveIsUIAccess = 1;
							_isUIAccess = uia != 0;
						}
					}
				}
				if (Failed = (_haveIsUIAccess == 2)) return false;
				return _isUIAccess;
			}
		}
		bool _isUIAccess; byte _haveIsUIAccess;

		//not very useful. Returns false for ApplicationFrameWindow. Can use wnd.IsWindows10StoreApp.
		///// <summary>
		///// Returns <c>true</c> if the process is a Windows Store app.
		///// </summary>
		//public unsafe bool IsAppContainer
		//{
		//	get
		//	{
		//		if(!osVersion.minWin8) return false;
		//		uint isac;
		//		if(Failed = !Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenIsAppContainer, &isac, 4, out var siz)) return false;
		//		return isac != 0;
		//	}
		//}

		static class _Api {
#pragma warning disable 649
			internal struct TOKEN_MANDATORY_LABEL { public IntPtr Sid; public uint Attributes; }
#pragma warning restore 649
			internal const uint SECURITY_MANDATORY_UNTRUSTED_RID = 0x00000000;
			internal const uint SECURITY_MANDATORY_LOW_RID = 0x00001000;
			internal const uint SECURITY_MANDATORY_MEDIUM_RID = 0x00002000;
			internal const uint SECURITY_MANDATORY_MEDIUM_PLUS_RID = SECURITY_MANDATORY_MEDIUM_RID + 0x100;
			internal const uint SECURITY_MANDATORY_HIGH_RID = 0x00003000;
			internal const uint SECURITY_MANDATORY_SYSTEM_RID = 0x00004000;
			internal const uint SECURITY_MANDATORY_PROTECTED_PROCESS_RID = 0x00005000;
		}

		/// <summary>
		/// Gets the [](xref:uac) integrity level (IL) of the process.
		/// </summary>
		/// <remarks>
		/// IL from lowest to highest value: <b>Untrusted</b>, <b>Low</b>, <b>Medium</b>, <b>UIAccess</b>, <b>High</b>, <b>System</b>, <b>Protected</b>, <b>Unknown</b>.
		/// The IL enum member values can be used like <c>if(x.IntegrityLevel > IL.Medium) ...</c> .
		/// If UAC is turned off, most non-service processes on administrator account have High IL; on non-administrator - Medium.
		/// </remarks>
		public UacIL IntegrityLevel => _GetIntegrityLevel();
		UacIL _GetIntegrityLevel() {
			if (_haveIntegrityLevel == 0) {
				unsafe {
					Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, null, 0, out var siz);
					if (lastError.code != Api.ERROR_INSUFFICIENT_BUFFER) _haveIntegrityLevel = 2;
					else {
						var b = stackalloc byte[(int)siz];
						var tml = (_Api.TOKEN_MANDATORY_LABEL*)b;
						if (!Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, tml, siz, out siz)) _haveIntegrityLevel = 2;
						uint x = *Api.GetSidSubAuthority(tml->Sid, (uint)(*Api.GetSidSubAuthorityCount(tml->Sid) - 1));

						if (x < _Api.SECURITY_MANDATORY_LOW_RID) _integrityLevel = UacIL.Untrusted;
						else if (x < _Api.SECURITY_MANDATORY_MEDIUM_RID) _integrityLevel = UacIL.Low;
						else if (x < _Api.SECURITY_MANDATORY_HIGH_RID) _integrityLevel = UacIL.Medium;
						else if (x < _Api.SECURITY_MANDATORY_SYSTEM_RID) {
							if (IsUIAccess && Elevation != UacElevation.Full) _integrityLevel = UacIL.UIAccess; //fast. Note: don't use if(andUIAccess) here.
							else _integrityLevel = UacIL.High;
						} else if (x < _Api.SECURITY_MANDATORY_PROTECTED_PROCESS_RID) _integrityLevel = UacIL.System;
						else _integrityLevel = UacIL.Protected;
					}
				}
			}
			if (Failed = (_haveIntegrityLevel == 2)) return UacIL.Unknown;
			return _integrityLevel;
		}
		UacIL _integrityLevel; byte _haveIntegrityLevel;

		uacInfo(Handle_ hToken) => _htoken = hToken;

		static uacInfo _Create(IntPtr hProcess) {
			if (!Api.OpenProcessToken(hProcess, Api.TOKEN_QUERY | Api.TOKEN_QUERY_SOURCE, out Handle_ hToken)) return null;
			return new uacInfo(hToken);
		}

		/// <summary>
		/// Opens process access token and creates/returns new <see cref="uacInfo"/> variable that holds it. Then you can use its properties.
		/// </summary>
		/// <returns><c>null</c> if failed. For example fails for services and some other processes if current process is not administrator.</returns>
		/// <param name="processId">Process id. If you have a window, use <see cref="wnd.ProcessId"/>.</param>
		/// <remarks>
		/// To get <b>uacInfo</b> of this process, use <see cref="ofThisProcess"/>.
		/// </remarks>
		public static uacInfo ofProcess(int processId) {
			if (processId == 0) return null;
			using var hp = Handle_.OpenProcess(processId);
			if (hp.Is0) return null;
			return _Create(hp);
		}

		/// <summary>
		/// Gets <see cref="uacInfo"/> variable for this process.
		/// </summary>
		public static uacInfo ofThisProcess {
			get {
				if (s_thisProcess == null) {
					s_thisProcess = _Create(Api.GetCurrentProcess());
					Debug.Assert(s_thisProcess != null);
				}
				return s_thisProcess;
			}
		}
		static uacInfo s_thisProcess;

		/// <summary>
		/// Returns <c>true</c> if this process is running as administrator.
		/// </summary>
#if true
		public static bool isAdmin => s_isAdmin ??= Api.IsUserAnAdmin();
#else //too slow, eg 15 ms vs 1 ms
		public static bool IsAdmin {
			get {
				if(!s_isAdmin.HasValue) {
					try {
						WindowsIdentity id = WindowsIdentity.GetCurrent();
						WindowsPrincipal principal = new WindowsPrincipal(id);
						s_isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
					}
					catch { }
				}
				return s_isAdmin.GetValueOrDefault();
			}
		}
#endif
		static bool? s_isAdmin;

		/*
		public struct SID_IDENTIFIER_AUTHORITY
		{
			public byte b0, b1, b2, b3, b4, b5;
		}

		[DllImport("advapi32.dll")]
		internal static extern bool AllocateAndInitializeSid(in SID_IDENTIFIER_AUTHORITY pIdentifierAuthority, byte nSubAuthorityCount, uint nSubAuthority0, uint nSubAuthority1, uint nSubAuthority2, uint nSubAuthority3, uint nSubAuthority4, uint nSubAuthority5, uint nSubAuthority6, uint nSubAuthority7, out IntPtr pSid);

		[DllImport("advapi32.dll")]
		internal static extern bool CheckTokenMembership(IntPtr TokenHandle, IntPtr SidToCheck, out bool IsMember);

		[DllImport("advapi32.dll")]
		internal static extern IntPtr FreeSid(IntPtr pSid);

		public const int SECURITY_BUILTIN_DOMAIN_RID = 32;
		public const int DOMAIN_ALIAS_RID_ADMINS = 544;

		//This is from CheckTokenMembership reference.
		//In QM2 it is very fast, but here quite slow first time, although then becomes the fastest. Advapi32.dll is already loaded, but maybe it loads other dlls.
		//IsUserAnAdmin first time can be slowest. It loads shell32.dll.
		//The .NET principal etc first time usually is fastest, althoug later is slower several times. (old info)
		//All 3 tested on admin and user accounts, also when UAC is turned off, also with System IL.
		public static bool isAdmin
		{
			get
			{
				var NtAuthority = new SID_IDENTIFIER_AUTHORITY() { b5 = 5 }; //SECURITY_NT_AUTHORITY
				IntPtr AdministratorsGroup;
				if(!AllocateAndInitializeSid(NtAuthority, 2,
					SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS,
					0, 0, 0, 0, 0, 0,
					out AdministratorsGroup
					))
					return false;
				bool _r;
				if(!CheckTokenMembership(default, AdministratorsGroup, out _r)) _r = false;
				FreeSid(AdministratorsGroup);
				return _r;
			}
		}
		*/

		/// <summary>
		/// Returns <c>true</c> if [](xref:uac) is disabled (turned off) completely (not just disabled UAC consent screen/dialog).
		/// </summary>
		public static bool isUacDisabled {
			get {
				if (!_haveIsUacDisabled) {
					_isUacDisabled = _IsUacDisabled();
					_haveIsUacDisabled = true;
				}
				return _isUacDisabled;
			}
		}

		static bool _isUacDisabled, _haveIsUacDisabled;
		static bool _IsUacDisabled() {
			//if(osVersion.minWin8) return false; //UAC cannot be disabled so easily, but can
			uacInfo x = ofThisProcess;
			switch (x.Elevation) {
			case UacElevation.Full:
			case UacElevation.Limited:
				return false;
			}
			//if(x.IsUIAccess) return false; //uiAccess in non-admin user session. Rare.

			int r = 1;
			try {
				if (!Api.GetDelegate(out Api.CheckElevationEnabled d, "kernel32.dll", "CheckElevationEnabled") || 0 != d(out r)) {
					Debug_.Print("CheckElevationEnabled");
					r = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", null) is int v ? v : 1;
				}
			}
			catch (Exception e) { Debug_.Print(e); }
			return r == 0;
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// UAC integrity level.
	/// See <see cref="uacInfo.IntegrityLevel"/>.
	/// </summary>
	public enum UacIL {
		/// <summary>The most limited rights. Rare.</summary>
		Untrusted,

		/// <summary>Very limited rights. Used by web browser tab processes, Windows Store apps.</summary>
		Low,

		/// <summary>Limited rights. Most processes (unless UAC turned off).</summary>
		Medium,

		/// <summary>Medium IL + can access/automate High IL windows (user interface).</summary>
		UIAccess,

		/// <summary>Most rights. Processes that run as administrator.</summary>
		High,

		/// <summary>Almost all rights. Services, some system processes.</summary>
		System,

		/// <summary>Undocumented. Rare.</summary>
		Protected,

		/// <summary>Failed to get IL. Unlikely.</summary>
		Unknown = 100
	}

	/// <summary>
	/// <see cref="uacInfo.Elevation"/>.
	/// </summary>
	public enum UacElevation {
		/// <summary>Failed to get. Normally it never happens.</summary>
		Unknown,

		/// <summary>
		/// Processes in this user session cannot be elevated.
		/// Can be: non-administrator user session (processes have limited rights); service session (processes have all rights); UAC is turned off (most processes have administrator rights).
		/// </summary>
		Default,

		/// <summary>Runs as administrator (<b>High</b> or <b>System</b> integrity level), and UAC is not turned off. Also known as "elevated".</summary>
		Full,

		/// <summary>Runs as standard user (<b>Medium</b>, <b>UIAccess</b> or <b>Low</b> integrity level) in administrator user session (because of UAC).</summary>
		Limited
	}
}
