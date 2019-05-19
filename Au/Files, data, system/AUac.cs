using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
using System.Security.Principal;
//using System.Linq;

using Au.Types;
using static Au.AStatic;

namespace Au
{
	/// <summary>
	/// Holds an access token (security info) of a process and provides various security info, eg [](xref:uac) integrity level.
	/// </summary>
	public sealed class AUac : IDisposable
	{
		#region IDisposable Support

		///
		~AUac() => _htoken.Dispose();

		///
		public void Dispose()
		{
			_htoken.Dispose();
			GC.SuppressFinalize(this);
		}
		#endregion

		LibHandle _htoken;
		HandleRef _HtokenHR => new HandleRef(this, _htoken);

		/// <summary>
		/// The access token handle.
		/// </summary>
		/// <remarks>
		/// The handle is managed by this variable and will be closed when disposing or GC-collecting it. Use <see cref="GC.KeepAlive"/> where need.
		/// </remarks>
		public IntPtr UnsafeTokenHandle => _htoken;

		/// <summary>
		/// Returns true if the last called property function failed.
		/// Normally it should never fail. Only <see cref="OfProcess"/> can fail (then it returns null).
		/// </summary>
		public bool Failed { get; private set; }

		/// <summary>
		/// Gets the [](xref:uac) elevation type of the process.
		/// </summary>
		public UacElevation Elevation {
			get {
				if(_haveElevation == 0) {
					unsafe {
						UacElevation elev;
						if(!Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenElevationType, &elev, 4, out var siz)) _haveElevation = 2;
						else {
							_haveElevation = 1;
							_Elevation = elev;
						}
					}
				}
				if(Failed = (_haveElevation == 2)) return UacElevation.Unknown;
				return _Elevation;
			}
		}
		UacElevation _Elevation; byte _haveElevation;

		/// <summary>
		/// Returns true if the process has [](xref:uac) uiAccess property.
		/// A uiAccess process can access/automate all windows of processes running in the same user session.
		/// </summary>
		/// <remarks>
		/// Most processes don't have this property. They cannot access/automate windows of higher integrity level (High, System, uiAccess) processes and Windows 8 store apps. For example, cannot send keys and Windows messages.
		/// Note: High IL (admin) processes also can have this property, therefore <c>IsUIAccess</c> is not the same as <c>IntegrityLevel==IL.UIAccess</c> (<see cref="IntegrityLevel"/> returns <b>UIAccess</b> only for Medium+uiAccess processes; for High+uiAccess processes it returns <b>High</b>). Some Windows API work slightly differently with uiAccess and non-uiAccess admin processes.
		/// This property is rarely useful. Instead use other properties of this class.
		/// </remarks>
		public bool IsUIAccess {
			get {
				if(_haveIsUIAccess == 0) {
					unsafe {
						uint uia;
						if(!Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenUIAccess, &uia, 4, out var siz)) _haveIsUIAccess = 2;
						else {
							_haveIsUIAccess = 1;
							_isUIAccess = uia != 0;
						}
					}
				}
				if(Failed = (_haveIsUIAccess == 2)) return false;
				return _isUIAccess;
			}
		}
		bool _isUIAccess; byte _haveIsUIAccess;

		//not very useful. Returns false for ApplicationFrameWindow. Can use Wnd.IsWindows10StoreApp.
		///// <summary>
		///// Returns true if the process is a Windows Store app.
		///// </summary>
		//public unsafe bool IsAppContainer
		//{
		//	get
		//	{
		//		if(!AVersion.MinWin8) return false;
		//		uint isac;
		//		if(Failed = !Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenIsAppContainer, &isac, 4, out var siz)) return false;
		//		return isac != 0;
		//	}
		//}

#pragma warning disable 649
		struct TOKEN_MANDATORY_LABEL { internal IntPtr Sid; internal uint Attributes; }
#pragma warning restore 649
		const uint SECURITY_MANDATORY_UNTRUSTED_RID = 0x00000000;
		const uint SECURITY_MANDATORY_LOW_RID = 0x00001000;
		const uint SECURITY_MANDATORY_MEDIUM_RID = 0x00002000;
		const uint SECURITY_MANDATORY_MEDIUM_PLUS_RID = SECURITY_MANDATORY_MEDIUM_RID + 0x100;
		const uint SECURITY_MANDATORY_HIGH_RID = 0x00003000;
		const uint SECURITY_MANDATORY_SYSTEM_RID = 0x00004000;
		const uint SECURITY_MANDATORY_PROTECTED_PROCESS_RID = 0x00005000;

		/// <summary>
		/// Gets the [](xref:uac) integrity level (IL) of the process.
		/// </summary>
		/// <remarks>
		/// IL from lowest to highest value: Untrusted, Low, Medium, UIAccess, High, System, Protected, Unknown.
		/// The IL enum member values can be used like <c>if(x.IntegrityLevel > IL.Medium) ...</c> .
		/// If UAC is turned off, most non-service processes on administrator account have High IL; on non-administrator - Medium.
		/// </remarks>
		public UacIL IntegrityLevel => _GetIntegrityLevel();
		UacIL _GetIntegrityLevel()
		{
			if(_haveIntegrityLevel == 0) {
				unsafe {
					Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, null, 0, out var siz);
					if(WinError.Code != Api.ERROR_INSUFFICIENT_BUFFER) _haveIntegrityLevel = 2;
					else {
						var b = stackalloc byte[(int)siz];
						var tml = (TOKEN_MANDATORY_LABEL*)b;
						if(!Api.GetTokenInformation(_HtokenHR, Api.TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, tml, siz, out siz)) _haveIntegrityLevel = 2;
						uint x = *Api.GetSidSubAuthority(tml->Sid, (uint)(*Api.GetSidSubAuthorityCount(tml->Sid) - 1));

						if(x < SECURITY_MANDATORY_LOW_RID) _integrityLevel = UacIL.Untrusted;
						else if(x < SECURITY_MANDATORY_MEDIUM_RID) _integrityLevel = UacIL.Low;
						else if(x < SECURITY_MANDATORY_HIGH_RID) _integrityLevel = UacIL.Medium;
						else if(x < SECURITY_MANDATORY_SYSTEM_RID) {
							if(IsUIAccess && Elevation != UacElevation.Full) _integrityLevel = UacIL.UIAccess; //fast. Note: don't use if(andUIAccess) here.
							else _integrityLevel = UacIL.High;
						} else if(x < SECURITY_MANDATORY_PROTECTED_PROCESS_RID) _integrityLevel = UacIL.System;
						else _integrityLevel = UacIL.Protected;
					}
				}
			}
			if(Failed = (_haveIntegrityLevel == 2)) return UacIL.Unknown;
			return _integrityLevel;
		}
		UacIL _integrityLevel; byte _haveIntegrityLevel;

		AUac(LibHandle hToken) => _htoken = hToken;

		static AUac _Create(IntPtr hProcess)
		{
			if(!Api.OpenProcessToken(hProcess, Api.TOKEN_QUERY | Api.TOKEN_QUERY_SOURCE, out LibHandle hToken)) return null;
			return new AUac(hToken);
		}

		/// <summary>
		/// Opens process access token and creates/returns new <see cref="AUac"/> variable that holds it. Then you can use its properties.
		/// Returns null if failed. For example fails for services and some other processes if current process is not administrator.
		/// </summary>
		/// <param name="processId">Process id. If you have a window, use <see cref="Wnd.ProcessId"/>.</param>
		/// <remarks>
		/// To get <b>AUac</b> of this process, use <see cref="OfThisProcess"/>.
		/// </remarks>
		public static AUac OfProcess(int processId)
		{
			if(processId == 0) return null;
			using var hp = LibHandle.OpenProcess(processId);
			if(hp.Is0) return null;
			return _Create(hp);
		}

		/// <summary>
		/// Gets <see cref="AUac"/> variable for this process.
		/// </summary>
		public static AUac OfThisProcess {
			get {
				if(_thisProcess == null) {
					_thisProcess = _Create(Api.GetCurrentProcess());
					Debug.Assert(_thisProcess != null);
				}
				return _thisProcess;
			}
		}
		static AUac _thisProcess;

		/// <summary>
		/// Returns true if this process is running as administrator, ie if the user belongs to the local Administrators group and the process is not limited by [](xref:uac).
		/// This function for example can be used to check whether you can write to protected locations in the file system and registry.
		/// </summary>
		public static bool IsAdmin {
			get {
				if(!_haveIsAdmin) {
					try {
						WindowsIdentity id = WindowsIdentity.GetCurrent();
						WindowsPrincipal principal = new WindowsPrincipal(id);
						_isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
					}
					catch { }
					_haveIsAdmin = true;
				}
				return _isAdmin;
			}
		}
		static bool _isAdmin, _haveIsAdmin;

		/*
		public struct SID_IDENTIFIER_AUTHORITY
		{
			public byte b0, b1, b2, b3, b4, b5;
		}

		[DllImport("advapi32.dll")]
		public static extern bool AllocateAndInitializeSid(in SID_IDENTIFIER_AUTHORITY pIdentifierAuthority, byte nSubAuthorityCount, uint nSubAuthority0, uint nSubAuthority1, uint nSubAuthority2, uint nSubAuthority3, uint nSubAuthority4, uint nSubAuthority5, uint nSubAuthority6, uint nSubAuthority7, out IntPtr pSid);

		[DllImport("advapi32.dll")]
		public static extern bool CheckTokenMembership(IntPtr TokenHandle, IntPtr SidToCheck, out bool IsMember);

		[DllImport("advapi32.dll")]
		public static extern IntPtr FreeSid(IntPtr pSid);

		public const int SECURITY_BUILTIN_DOMAIN_RID = 32;
		public const int DOMAIN_ALIAS_RID_ADMINS = 544;

		//This is from CheckTokenMembership reference.
		//In QM2 it is very fast, but here quite slow first time, although then becomes the fastest. Advapi32.dll is already loaded, but maybe it loads other dlls.
		//IsUserAnAdmin first time can be slowest. It loads shell32.dll.
		//The .NET principal etc first time usually is fastest, althoug later is slower several times.
		//All 3 tested on admin and user accounts, also when UAC is turned off, also with System IL.
		public static bool IsAdmin
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
				bool R;
				if(!CheckTokenMembership(default, AdministratorsGroup, out R)) R = false;
				FreeSid(AdministratorsGroup);
				return R;
			}
		}
		*/

		/// <summary>
		/// Returns true if [](xref:uac) is disabled (turned off) completely (not just disabled UAC consent screeen/dialog).
		/// </summary>
		public static bool IsUacDisabled {
			get {
				if(!_haveIsUacDisabled) {
					_isUacDisabled = _IsUacDisabled();
					_haveIsUacDisabled = true;
				}
				return _isUacDisabled;
			}
		}

		static bool _isUacDisabled, _haveIsUacDisabled;
		static bool _IsUacDisabled()
		{
			//if(AVersion.MinWin8) return false; //UAC cannot be disabled so easily, but can
			AUac x = OfThisProcess;
			switch(x.Elevation) {
			case UacElevation.Full:
			case UacElevation.Limited:
				return false;
			}
			//if(x.IsUIAccess) return false; //uiAccess in non-admin user session. Rare.

			int r = 1;
			try {
				if(!Api.GetDelegate(out Api.CheckElevationEnabled d, "kernel32.dll", "CheckElevationEnabled") || 0 != d(out r)) {
					ADebug.Print("CheckElevationEnabled");
					r = (int)Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 1);
				}
			}
			catch(Exception e) { ADebug.Print(e); }
			return r == 0;
		}
	}
}

namespace Au.Types
{
	/// <summary>
	/// UAC integrity level.
	/// See <see cref="AUac.IntegrityLevel"/>.
	/// </summary>
	public enum UacIL
	{
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
	/// <see cref="AUac.Elevation"/>.
	/// </summary>
	public enum UacElevation
	{
		/// <summary>Failed to get. Normally it never happens.</summary>
		Unknown,

		/// <summary>
		/// Processes in this user session cannot be elevated.
		/// Can be: non-administrator user session (processes have limited rights); service session (processes have all rights); UAC is turned off (most processes have administrator rights).
		/// </summary>
		Default,

		/// <summary>Runs as administrator (High or System integrity level), and UAC is not turned off. Also known as "elevated".</summary>
		Full,

		/// <summary>Runs as standard user (Medium, UIAccess or Low integrity level) in administrator user session (because of UAC).</summary>
		Limited
	}
}