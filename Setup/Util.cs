#if !EMPTY
//#define USE_RESTARTMANAGER //currently not useful. Not used for AuCpp.dll (renames it instead); other files are unlikely to be locked.

using System.Net;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Windows.Interop;

static class Util {
	/// <summary>
	/// Writes text in QM output.
	/// Use for debug only.
	/// </summary>
	public static unsafe void Print(this object t) {
		if (!api.IsWindow(_hwndQM2)) {
			_hwndQM2 = api.FindWindowEx(0, 0, "QM_Editor", null);
			if (_hwndQM2 == 0) return;
		}
		string s = t?.ToString() ?? "";
		fixed (char* p = s)
			api.SendMessage(_hwndQM2, api.WM_SETTEXT, -1, (nint)p);
	}
	static nint _hwndQM2;
	
#if NET
	public static string ThisExePath => Environment.ProcessPath;
#else
	public static string ThisExePath => System.Reflection.Assembly.GetEntryAssembly()?.Location;
#endif
	
	public static string ThisExeArgs => Regex.Replace(Environment.CommandLine, @"^(?:""[^""]+""|\S+)\s*", "");
	
	/// <summary>
	/// Deletes a file or directory (with all descendants).
	/// </summary>
	/// <param name="path"></param>
	/// <param name="dirIfEmpty">Don't delete if it's a non-empty directory.</param>
	/// <returns>false if the file existed but failed to delete. No exceptions.</returns>
	/// <remarks>
	/// Waits-retries max 500 ms if fails.
	/// Removes read-only attribute if need.
	/// </remarks>
	public static bool DeleteFileOrDir(string path, bool dirIfEmpty = false) {
		for (int i = 10; --i >= 0;) {
			var k = api.GetFileAttributes(path);
			if ((int)k == -1) return true;
			try {
				bool isDir = k.HasFlag(FileAttributes.Directory);
				if (isDir) {
					foreach (var v in Directory.GetFileSystemEntries(path)) {
						if (dirIfEmpty) return false;
						DeleteFileOrDir(v);
					}
				}
				if (k.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(path, k & ~FileAttributes.ReadOnly);
				if (isDir) {
					Directory.Delete(path);
				} else {
					File.Delete(path);
				}
				return true;
			}
			catch {
				//path.Print();
				if (i > 0) Thread.Sleep(50);
			}
		}
		return false;
	}
	
	/// <summary>
	/// Starts a console process, redirects its stdout (but not stderr), waits until it exits, and reads the stdouts.
	/// </summary>
	/// <param name="output">Stdout text.</param>
	/// <param name="program"></param>
	/// <param name="arguments"></param>
	/// <param name="workingDirectory"></param>
	/// <param name="encoding">If null, uses UTF-8.</param>
	/// <returns>false if the exit code is not 0. Throws exception if failed (eg file does not exist).</returns>
	public static bool RunConsole(out string output, string program, string arguments, string workingDirectory = null, Encoding encoding = null) {
		output = null;
		
		using var p = new Process {
			StartInfo = new ProcessStartInfo {
				UseShellExecute = false,
				FileName = program,
				Arguments = arguments,
				WorkingDirectory = workingDirectory,
				RedirectStandardOutput = true,
				StandardOutputEncoding = encoding ?? Encoding.UTF8,
				CreateNoWindow = true,
			}
		};
		
		p.Start();
		
		var t = p.StandardOutput.ReadToEndAsync();
		
		p.WaitForExit();
		output = t.GetAwaiter().GetResult();
		
		return p.ExitCode == 0;
	}
	
	public static bool IsArm64 => RuntimeInformation.OSArchitecture == Architecture.Arm64;
	
	public static HttpClient CreateHttpClient(bool bigTimeout) {
		if (!s_once1) {
#if !NET
			var ver = Environment.OSVersion.Version;
			if ((ver.Major, ver.Minor) == (6, 1)) {
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; //workaround: on Win7 fails to connect to most websites
			}
#endif
			s_once1 = true;
		}
		
		var r = new HttpClient();
		if (bigTimeout) r.Timeout = TimeSpan.FromMinutes(60);
		r.DefaultRequestHeaders.Add("User-Agent", "LibreAutomate setup");
		return r;
	}
	static bool s_once1;
	
	public delegate void DownloadProgress(long total, long downloaded);
	
	/// <summary>
	/// Downloads a file.
	/// Throws exception if failed.
	/// </summary>
	/// <param name="nRetry">How many times to retry if the first attempt fails.</param>
	public static void Download(this HttpClient t, string url, string file, int nRetry = 1, DownloadProgress progress = null) {
		var tempFile = $"{file}.~part"; //download to a temporary file, to avoid replacing existing file or creating partial file if this process ends unexpectedly
		try {
			gRetry:
			try {
				using (var response = t.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult()) {
					response.EnsureSuccessStatusCode();
					using var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
					using var output = File.Create(tempFile);
					
					if (progress == null) {
						input.CopyTo(output);
					} else {
						long total = response.Content.Headers.ContentLength ?? -1;
						var buffer = new byte[81920];
						
						for (long downloaded = 0; ;) {
							progress(total, downloaded);
							int n = input.Read(buffer, 0, buffer.Length);
							if (n == 0) break;
							output.Write(buffer, 0, n);
							downloaded += n;
						}
					}
				}
				
				for (int i = 10; --i >= 0;) {
					if (api.MoveFileEx(tempFile, file, api.MOVEFILE_REPLACE_EXISTING)) return;
					Thread.Sleep(100);
				}
			}
			catch when (nRetry-- > 0) {
				Thread.Sleep(1000);
				goto gRetry;
			}
		}
		finally {
			DeleteFileOrDir(tempFile);
		}
		
		throw new IOException("Failed to replace file: " + file);
	}
	
	public static bool CreateShortcut(string lnkPath, string target, string arguments = null) {
		try {
			var isl = new api.ShellLink() as api.IShellLinkW;
			isl.SetPath(target);
			if (arguments != null) isl.SetArguments(arguments);
			
			var ipf = isl as api.IPersistFile;
			ipf.Save(lnkPath, 1);
			
			Marshal.ReleaseComObject(ipf);
			Marshal.ReleaseComObject(isl);
			return true;
		}
		catch { return false; }
	}
	
	/// <summary>
	/// Returns <c>true</c> if this string is <c>null</c> or empty (<c>""</c>).
	/// </summary>
	public static bool NE(this string t) => t == null || t.Length == 0;
	
	public static bool IsFullPath(string path) => !_IsPartiallyQualified(path);
	
	//from .NET source
	static bool _IsPartiallyQualified(string path) {
		if (path.Length < 2) return true;
		
		if (IsDirectorySeparator(path[0])) return !(path[1] == '?' || IsDirectorySeparator(path[1]));
		
		return !((path.Length >= 3)
			&& (path[1] == ':')
			&& IsDirectorySeparator(path[2])
			&& IsValidDriveChar(path[0]));
		
		static bool IsDirectorySeparator(char c) => c is '\\' or '/';
		static bool IsValidDriveChar(char c) => (uint)((c | 0x20) - 'a') <= (uint)('z' - 'a');
	}
	
	/// <summary>
	/// Creates new directory if does not exist.
	/// Sets security attributes for auth users to modify its content.
	/// </summary>
	public static void CreateWritableDirectory(string path) {
		Directory.CreateDirectory(path);
		
		try {
			var di = new DirectoryInfo(path);
			var security = di.GetAccessControl();
			
			security.SetAccessRule(new(
				new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
				FileSystemRights.ReadAndExecute | FileSystemRights.Write,
				InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
				PropagationFlags.None,
				AccessControlType.Allow));
			
			di.SetAccessControl(security);
		}
		catch { }
	}
	
	/// <summary>
	/// Gets SHA256 of one or more files, as lowercase hex string.
	/// </summary>
	/// <param name="files">File paths. If <i>dir</i> used - relative paths.</param>
	/// <param name="dir">null, or parent directory for relative paths.</param>
	/// <returns>null if a file does not exist or if failed.</returns>
	public static string HashFiles(IEnumerable<string> files, string dir = null) {
		try {
			using var sha = SHA256.Create();
			byte[] buffer = new byte[64 * 1024];
			
			foreach (string file_ in files) {
				string file = dir is null ? file_ : Path.Combine(dir, file_);
				if (!File.Exists(file)) return null;
				using var fs = File.OpenRead(file);
				int n;
				while ((n = fs.Read(buffer, 0, buffer.Length)) > 0) {
					sha.TransformBlock(buffer, 0, n, null, 0);
				}
			}
			
			sha.TransformFinalBlock([], 0, 0);
			
			return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
		}
		catch { }
		return null;
	}
	
	/// <summary>
	/// Gets processes that lock specified files.
	/// </summary>
	/// <param name="paths">One or more file paths.</param>
	/// <returns>null if none or if failed.</returns>
	public static unsafe Util.ProcessNameId[] GetProcessesUsingFiles(string[] paths) {
#if USE_RESTARTMANAGER
		char* sessionKey = stackalloc char[33]; //CCH_RM_SESSION_KEY+1
		if (0 == _Api.RmStartSession(out uint handle, 0, sessionKey)) {
			try {
				if (0 != _Api.RmRegisterResources(handle, paths.Length, paths, 0, null, 0, null)) return null;
				int nProc = 0;
				var r = _Api.RmGetList(handle, out int nNeed, ref nProc, null, out _);
				if (!(r is 0 or 234)) return null; //ERROR_MORE_DATA
				if (nNeed == 0) return null;
				var a = new _Api.RM_PROCESS_INFO[nProc = nNeed + 100];
				r = _Api.RmGetList(handle, out nNeed, ref nProc, a, out _);
				if (r != 0) return null;
				return a.Take(nProc)
					.Select(o => new Util.ProcessNameId(o.Process.dwProcessId, new(o.strAppName), o.strServiceShortName[0] == default ? null : new(o.strServiceShortName)))
					.ToArray();
			}
			finally { _Api.RmEndSession(handle); }
		}
#endif
		return null;
	}
	
#if USE_RESTARTMANAGER
#pragma warning disable 649, 169 //field never assigned/used
	static unsafe class _Api {
		[DllImport("rstrtmgr.dll")]
		internal static extern int RmStartSession(out uint pSessionHandle, uint dwSessionFlags, char* strSessionKey);
		
		internal struct RM_UNIQUE_PROCESS {
			public int dwProcessId;
			public FILETIME ProcessStartTime;
		}
		
		internal struct FILETIME {
			public uint dwLowDateTime;
			public uint dwHighDateTime;
		}
		
		[DllImport("rstrtmgr.dll")]
		internal static extern int RmRegisterResources(uint dwSessionHandle, int nFiles, [In] string[] rgsFileNames, int nApplications, [In] RM_UNIQUE_PROCESS[] rgApplications, int nServices, [In] string[] rgsServiceNames);
		
		[DllImport("rstrtmgr.dll")]
		internal static extern int RmEndSession(uint dwSessionHandle);
		
		[DllImport("rstrtmgr.dll")]
		internal static extern int RmGetList(uint dwSessionHandle, out int pnProcInfoNeeded, ref int pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps, out uint lpdwRebootReasons);
		
		internal struct RM_PROCESS_INFO {
			public RM_UNIQUE_PROCESS Process;
			public fixed char strAppName[256];
			public fixed char strServiceShortName[64];
			public int ApplicationType;
			public uint AppStatus;
			public uint TSSessionId;
			public int bRestartable;
		}
	}
#pragma warning restore 649, 169 //field never assigned/used
#endif
	
	public record struct ProcessNameId(int id, string name, string service/*, bool restartable //tested: most not restartable */) {
		public override string ToString() {
			if (service != null) return $"{name}, id={id}, service={service}";
			return $"{name}, id={id}";
			//if(service!=null) return $"{name}, id={id}, service={service}, restartable={restartable}";
			//return $"{name}, id={id}, restartable={restartable}";
		}
	}
	
	public static async void ActivateWindowAsync(Window w) {
		nint h = new WindowInteropHelper(w).Handle;
		if (h == api.GetForegroundWindow()) return;
		w.Activate();
		await Task.Delay(10);
		if (h == api.GetForegroundWindow()) return;
		w.WindowState = WindowState.Minimized;
		await Task.Delay(1);
		w.WindowState = WindowState.Normal;
		//Util.Print($"{h}, {api.GetForegroundWindow()}");
	}
}
#endif
