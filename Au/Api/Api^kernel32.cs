namespace Au.Types;

static unsafe partial class Api {
	[DllImport("kernel32.dll", SetLastError = true)] //note: without `SetLastError = true` Marshal.GetLastWin32Error is unaware that we set the code to 0 etc and returns old captured error code
	internal static extern void SetLastError(int errCode);
	
	internal const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
	internal const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x100;
	internal const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x200;
	
	[DllImport("kernel32.dll", EntryPoint = "FormatMessageW")]
	internal static extern int FormatMessage(uint dwFlags, IntPtr lpSource, int code, uint dwLanguageId, char** lpBuffer, int nSize, IntPtr Arguments);
	
	[DllImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", SetLastError = true)]
	internal static extern bool SetDllDirectory(string lpPathName);
	
	[SuppressGCTransition] //makes slightly faster. Not faster with [MethodImpl].
	[DllImport("kernel32.dll")]
	internal static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
	
	[DllImport("kernel32.dll")]
	internal static extern bool QueryPerformanceFrequency(out long lpFrequency);
	
	[DllImport("kernel32.dll")]
	internal static extern bool QueryUnbiasedInterruptTime(out long UnbiasedTime);
	
	[DllImport("kernel32.dll")]
	internal static extern long GetTickCount64();
	
	[DllImport("kernel32.dll")]
	internal static extern int GetTickCount();
	
	internal struct SYSTEMTIME {
		public ushort wYear;
		public ushort wMonth;
		public ushort wDayOfWeek;
		public ushort wDay;
		public ushort wHour;
		public ushort wMinute;
		public ushort wSecond;
		public ushort wMilliseconds;
	}
	
	[DllImport("kernel32.dll")]
	internal static extern void GetLocalTime(out SYSTEMTIME lpSystemTime);
	
	[DllImport("kernel32.dll")]
	internal static extern void GetSystemTimeAsFileTime(out long lpSystemTimeAsFileTime);
	
	//[DllImport("kernel32.dll", SetLastError = true)]
	//internal static extern bool GetThreadTimes(IntPtr hThread, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);
	
	//[DllImport("kernel32.dll")]
	//internal static extern bool QueryProcessCycleTime(IntPtr ProcessHandle, out long CycleTime);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GetThreadTimes(IntPtr hThread, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);
	
	[DllImport("kernel32.dll", PreserveSig = true, SetLastError = true)]
	internal static extern int GetThreadDescription(IntPtr hThread, out char* ppszThreadDescription);
	
	[DllImport("kernel32.dll", EntryPoint = "CreateEventW", SetLastError = true)]
	internal static extern IntPtr CreateEvent2(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);
	
	internal static Handle_ CreateEvent(bool bManualReset)
		=> new(CreateEvent2(default, bManualReset, false, null));
	
	[DllImport("kernel32.dll", EntryPoint = "OpenEventW", SetLastError = true)]
	internal static extern IntPtr OpenEvent(uint dwDesiredAccess, bool bInheritHandle, string lpName);
	
	internal const uint EVENT_MODIFY_STATE = 0x2;
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool SetEvent(IntPtr hEvent);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool ResetEvent(IntPtr hEvent);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern int WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);
	
	//[DllImport("kernel32.dll")]
	//internal static extern int SignalObjectAndWait(IntPtr hObjectToSignal, IntPtr hObjectToWaitOn, int dwMilliseconds, bool bAlertable);
	//note: don't know why, this often is much slower than setevent/waitforsingleobject.
	
	[DllImport("kernel32.dll")] //note: no SetLastError = true
	internal static extern bool CloseHandle(IntPtr hObject);
	
	//currently not used
	//[DllImport("kernel32.dll")] //note: no SetLastError = true
	//internal static extern bool CloseHandle(HandleRef hObject);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);
	
	[DllImport("kernel32.dll", EntryPoint = "CreateMutexW", SetLastError = true)]
	internal static extern IntPtr CreateMutex(SECURITY_ATTRIBUTES lpMutexAttributes, bool bInitialOwner, string lpName);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool ReleaseMutex(IntPtr hMutex);
	
	/// <summary>
	/// Note: use only for private threads. Not everything works like with <b>Thread.Start</b>. For example .NET does not auto-release COM objects when thread ends.
	/// </summary>
	/// <param name="lpStartAddress"><c>[UnmanagedCallersOnly]</c></param>
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern IntPtr CreateThread(nint lpThreadAttributes, nint dwStackSize, delegate* unmanaged<GCHandle, uint> lpStartAddress, GCHandle lpParameter, uint dwCreationFlags, out int lpThreadId);
	
	[DllImport("kernel32.dll")]
	internal static extern IntPtr GetCurrentThread();
	
	[SuppressGCTransition]
	[DllImport("kernel32.dll")]
	internal static extern int GetCurrentThreadId();
	
	[DllImport("kernel32.dll")]
	internal static extern IntPtr GetCurrentProcess();
	
	[SuppressGCTransition]
	[DllImport("kernel32.dll")]
	internal static extern int GetCurrentProcessId();
	
	[DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
	internal static extern bool QueryFullProcessImageName(IntPtr hProcess, bool nativeFormat, char* lpExeName, ref int lpdwSize);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool TerminateProcess(IntPtr hProcess, int uExitCode);
	
	[DllImport("kernel32.dll")]
	internal static extern void ExitProcess(int uExitCode);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool IsWow64Process(IntPtr hProcess, out bool Wow64Process);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern Handle_ CreateFileMapping(IntPtr hFile, SECURITY_ATTRIBUTES lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);
	
	//[DllImport("kernel32.dll", EntryPoint = "OpenFileMappingW", SetLastError = true)]
	//internal static extern Handle_ OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern void* MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, nint dwNumberOfBytesToMap);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool UnmapViewOfFile(void* lpBaseAddress);
	
	[DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true)]
	internal static extern IntPtr GetModuleHandle(string name);
	
	//Better use NativeLibrary.TryLoad.
	//Dlls loaded by LoadLibrary don't find other used dlls from the same directory if it's not the app directory. Need LoadLibraryEx with LOAD_WITH_ALTERED_SEARCH_PATH, and probably NativeLibrary.TryLoad uses it.
	//[DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", SetLastError = true)]
	//internal static extern IntPtr LoadLibrary(string lpLibFileName);
	
	internal const uint LOAD_LIBRARY_AS_DATAFILE = 0x2;
	
	[DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", SetLastError = true)]
	internal static extern IntPtr LoadLibraryEx(string lpLibFileName, IntPtr hFile, uint dwFlags);
	
	[DllImport("kernel32.dll")]
	internal static extern bool FreeLibrary(IntPtr hLibModule);
	
	[DllImport("kernel32.dll", BestFitMapping = false, SetLastError = true)]
	internal static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);
	
	internal const uint PROCESS_TERMINATE = 0x0001;
	internal const uint PROCESS_CREATE_THREAD = 0x0002;
	internal const uint PROCESS_SET_SESSIONID = 0x0004;
	internal const uint PROCESS_VM_OPERATION = 0x0008;
	internal const uint PROCESS_VM_READ = 0x0010;
	internal const uint PROCESS_VM_WRITE = 0x0020;
	internal const uint PROCESS_DUP_HANDLE = 0x0040;
	internal const uint PROCESS_CREATE_PROCESS = 0x0080;
	internal const uint PROCESS_SET_QUOTA = 0x0100;
	internal const uint PROCESS_SET_INFORMATION = 0x0200;
	internal const uint PROCESS_QUERY_INFORMATION = 0x0400;
	internal const uint PROCESS_SUSPEND_RESUME = 0x0800;
	internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
	internal const uint PROCESS_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0xFFFF;
	internal const uint DELETE = 0x00010000;
	internal const uint READ_CONTROL = 0x00020000;
	internal const uint WRITE_DAC = 0x00040000;
	internal const uint WRITE_OWNER = 0x00080000;
	internal const uint SYNCHRONIZE = 0x00100000;
	internal const uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
	internal const uint STANDARD_RIGHTS_READ = READ_CONTROL;
	internal const uint STANDARD_RIGHTS_WRITE = READ_CONTROL;
	internal const uint STANDARD_RIGHTS_EXECUTE = READ_CONTROL;
	internal const uint STANDARD_RIGHTS_ALL = 0x001F0000;
	internal const uint TIMER_MODIFY_STATE = 0x2;
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern Handle_ OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
	
	[DllImport("kernel32.dll", EntryPoint = "GetFullPathNameW", SetLastError = true)]
	static extern int _GetFullPathName(string lpFileName, int nBufferLength, char* lpBuffer, char** lpFilePart);
	
	/// <summary>
	/// Calls API <b>GetFullPathName</b>.
	/// Returns <c>false</c> if failed or result is same; then <i>r</i> is <i>s</i>.
	/// <i>r</i> can be same variable as <i>s</i>.
	/// </summary>
	[SkipLocalsInit]
	internal static bool GetFullPathName(string s, out string r) {
		using FastBuffer<char> b = new();
		for (; ; ) if (b.GetString(_GetFullPathName(s, b.n, b.p, null), out r, 0, s)) return (object)r != s;
	}
	
	[DllImport("kernel32.dll", EntryPoint = "GetLongPathNameW", SetLastError = true)]
	static extern int _GetLongPathName(string lpszShortPath, char* lpszLongPath, int cchBuffer);
	
	/// <summary>
	/// Calls API <b>GetFullPathName</b>.
	/// Returns <c>false</c> if failed or result is same; then <i>r</i> is <i>s</i>.
	/// <i>r</i> can be same variable as <i>s</i>.
	/// </summary>
	[SkipLocalsInit]
	internal static bool GetLongPathName(string s, out string r) {
		using FastBuffer<char> b = new();
		for (; ; ) if (b.GetString(_GetLongPathName(s, b.p, b.n), out r, 0, s)) return (object)r != s;
	}
	
	[DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
	static extern int _GetFinalPathNameByHandle(IntPtr hFile, char* lpszFilePath, int cchFilePath, uint dwFlags);
	
	[SkipLocalsInit]
	internal static bool GetFinalPathNameByHandle(IntPtr h, out string r, uint dwFlags = 0) {
		using FastBuffer<char> b = new();
		for (; ; ) {
			g1: if (b.GetString(_GetFinalPathNameByHandle(h, b.p, b.n, dwFlags), out r, 0)) {
				if (r != null) return r.Length > 0;
				if (0u != (dwFlags & 3u)) { dwFlags &= ~3u; goto g1; } //if VOLUME_NAME_GUID, fails if network path
				return false;
			}
		}
	}
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool ProcessIdToSessionId(int dwProcessId, out int pSessionId);
	
	internal const uint PAGE_NOACCESS = 0x1;
	internal const uint PAGE_READONLY = 0x2;
	internal const uint PAGE_READWRITE = 0x4;
	internal const uint PAGE_WRITECOPY = 0x8;
	internal const uint PAGE_EXECUTE = 0x10;
	internal const uint PAGE_EXECUTE_READ = 0x20;
	internal const uint PAGE_EXECUTE_READWRITE = 0x40;
	internal const uint PAGE_EXECUTE_WRITECOPY = 0x80;
	internal const uint PAGE_GUARD = 0x100;
	internal const uint PAGE_NOCACHE = 0x200;
	internal const uint PAGE_WRITECOMBINE = 0x400;
	
	internal const uint MEM_COMMIT = 0x1000;
	internal const uint MEM_RESERVE = 0x2000;
	internal const uint MEM_DECOMMIT = 0x4000;
	internal const uint MEM_RELEASE = 0x8000;
	internal const uint MEM_RESET = 0x80000;
	internal const uint MEM_TOP_DOWN = 0x100000;
	internal const uint MEM_WRITE_WATCH = 0x200000;
	internal const uint MEM_PHYSICAL = 0x400000;
	internal const uint MEM_RESET_UNDO = 0x1000000;
	internal const uint MEM_LARGE_PAGES = 0x20000000;
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern void* VirtualAlloc(void* lpAddress, nint dwSize, uint flAllocationType = MEM_COMMIT | MEM_RESERVE, uint flProtect = PAGE_READWRITE);
	//note: with PAGE_EXECUTE_READWRITE writing to the memory first time is much slower.
	
	[DllImport("kernel32.dll")]
	internal static extern bool VirtualFree(void* lpAddress, nint dwSize = 0, uint dwFreeType = MEM_RELEASE);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern IntPtr VirtualAllocEx(HandleRef hProcess, IntPtr lpAddress, nint dwSize, uint flAllocationType = MEM_COMMIT | MEM_RESERVE, uint flProtect = PAGE_EXECUTE_READWRITE);
	
	[DllImport("kernel32.dll")]
	internal static extern bool VirtualFreeEx(HandleRef hProcess, IntPtr lpAddress, nint dwSize = 0, uint dwFreeType = MEM_RELEASE);
	
	[DllImport("kernel32.dll", EntryPoint = "GetFileAttributesW", SetLastError = true)]
	internal static extern FileAttributes GetFileAttributes(string lpFileName);
	
	[DllImport("kernel32.dll", EntryPoint = "SetFileAttributesW", SetLastError = true)]
	internal static extern bool SetFileAttributes(string lpFileName, FileAttributes dwFileAttributes);
	
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct WIN32_FILE_ATTRIBUTE_DATA {
		public FileAttributes dwFileAttributes;
		public long ftCreationTime;
		public long ftLastAccessTime;
		public long ftLastWriteTime;
		public uint nFileSizeHigh;
		public uint nFileSizeLow;
	}
	
	[DllImport("kernel32.dll", EntryPoint = "GetFileAttributesExW", SetLastError = true)]
	internal static extern bool GetFileAttributesEx(string lpFileName, int zero, out WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);
	
	[DllImport("kernel32.dll", EntryPoint = "SearchPathW", SetLastError = true)]
	static extern int _SearchPath(string lpPath, string lpFileName, string lpExtension, int nBufferLength, char* lpBuffer, char** lpFilePart);
	
	/// <summary>
	/// Calls API <b>SearchPath</b>. Returns full path, or <c>null</c> if not found.
	/// </summary>
	/// <param name="lpPath">Parent directory or <c>null</c>.</param>
	/// <param name="lpFileName"></param>
	/// <param name="lpExtension"><c>null</c> or extension like <c>".ext"</c> to add if <i>lpFileName</i> is without extension.</param>
	[SkipLocalsInit]
	internal static string SearchPath(string lpPath, string lpFileName, string lpExtension = null) {
		using FastBuffer<char> b = new();
		for (; ; ) if (b.GetString(_SearchPath(lpPath, lpFileName, lpExtension, b.n, b.p, null), out var s)) return s;
	}
	
	internal const uint BASE_SEARCH_PATH_ENABLE_SAFE_SEARCHMODE = 0x1;
	internal const uint BASE_SEARCH_PATH_DISABLE_SAFE_SEARCHMODE = 0x10000;
	internal const uint BASE_SEARCH_PATH_PERMANENT = 0x8000;
	
	[DllImport("kernel32.dll")]
	internal static extern bool SetSearchPathMode(uint Flags);
	
	internal const uint SEM_FAILCRITICALERRORS = 0x1;
	internal const uint SEM_NOGPFAULTERRORBOX = 0x2;
	
	[DllImport("kernel32.dll")]
	internal static extern uint SetErrorMode(uint uMode);
	
	//[DllImport("kernel32.dll")]
	//internal static extern uint GetErrorMode();
	
	//[DllImport("kernel32.dll", SetLastError = true)]
	//internal static extern IntPtr LocalAlloc(uint uFlags, nint uBytes);
	
	[DllImport("kernel32.dll")]
	internal static extern IntPtr LocalFree(void* hMem);
	
	[DllImport("kernel32.dll", EntryPoint = "lstrcpynW")]
	internal static extern char* lstrcpyn(char* sTo, string sFrom, int sToBufferLength);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool Wow64DisableWow64FsRedirection(out IntPtr OldValue);
	
	[DllImport("kernel32.dll")]
	internal static extern bool Wow64RevertWow64FsRedirection(IntPtr OlValue);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);
	
	[DllImport("kernel32.dll")]
	internal static extern IntPtr GetProcessHeap();
	[DllImport("kernel32.dll")]
	internal static extern void* HeapAlloc(IntPtr hHeap, uint dwFlags, nint dwBytes);
	[DllImport("kernel32.dll")]
	internal static extern void* HeapReAlloc(IntPtr hHeap, uint dwFlags, void* lpMem, nint dwBytes);
	[DllImport("kernel32.dll")]
	internal static extern bool HeapFree(IntPtr hHeap, uint dwFlags, void* lpMem);
	
	internal const int CP_UTF8 = 65001;
	internal const uint MB_ERR_INVALID_CHARS = 0x8;
	internal const uint WC_ERR_INVALID_CHARS = 0x80;
	
	[DllImport("kernel32.dll")]
	internal static extern int MultiByteToWideChar(uint CodePage, uint dwFlags, byte* lpMultiByteStr, int cbMultiByte, char* lpWideCharStr, int cchWideChar);
	
	[DllImport("kernel32.dll")]
	internal static extern int WideCharToMultiByte(uint CodePage, uint dwFlags, char* lpWideCharStr, int cchWideChar, byte* lpMultiByteStr, int cbMultiByte, IntPtr lpDefaultChar = default, int* lpUsedDefaultChar = null);
	
	[Flags]
	internal enum Access : uint { }
	
	internal const Access FILE_READ_DATA = (Access)0x1;
	internal const Access FILE_LIST_DIRECTORY = (Access)0x1;
	internal const Access FILE_WRITE_DATA = (Access)0x2;
	internal const Access FILE_ADD_FILE = (Access)0x2;
	internal const Access FILE_APPEND_DATA = (Access)0x4;
	internal const Access FILE_ADD_SUBDIRECTORY = (Access)0x4;
	internal const Access FILE_CREATE_PIPE_INSTANCE = (Access)0x4;
	internal const Access FILE_READ_EA = (Access)0x8;
	internal const Access FILE_WRITE_EA = (Access)0x10;
	internal const Access FILE_EXECUTE = (Access)0x20;
	internal const Access FILE_TRAVERSE = (Access)0x20;
	internal const Access FILE_DELETE_CHILD = (Access)0x40;
	internal const Access FILE_READ_ATTRIBUTES = (Access)0x80;
	internal const Access FILE_WRITE_ATTRIBUTES = (Access)0x100;
	internal const Access FILE_ALL_ACCESS = (Access)0x1F01FF;
	internal const Access FILE_GENERIC_READ = (Access)0x120089;
	internal const Access FILE_GENERIC_WRITE = (Access)0x120116;
	internal const Access FILE_GENERIC_EXECUTE = (Access)0x1200A0;
	
	internal const Access GENERIC_READ = (Access)0x80000000;
	internal const Access GENERIC_WRITE = (Access)0x40000000;
	
	internal enum CfCreation { }
	
	internal const CfCreation CREATE_NEW = (CfCreation)1;
	internal const CfCreation CREATE_ALWAYS = (CfCreation)2;
	internal const CfCreation OPEN_EXISTING = (CfCreation)3;
	internal const CfCreation OPEN_ALWAYS = (CfCreation)4;
	internal const CfCreation TRUNCATE_EXISTING = (CfCreation)5;
	
	[Flags]
	internal enum CfShare : uint { }
	
	internal const CfShare FILE_SHARE_READ = (CfShare)0x1;
	internal const CfShare FILE_SHARE_WRITE = (CfShare)0x2;
	internal const CfShare FILE_SHARE_DELETE = (CfShare)0x4;
	internal const CfShare FILE_SHARE_ALL = (CfShare)0x7;
	
	//the commented out attributes are not documented for CreateFile
	internal const uint FILE_ATTRIBUTE_READONLY = 0x1;
	internal const uint FILE_ATTRIBUTE_HIDDEN = 0x2;
	internal const uint FILE_ATTRIBUTE_SYSTEM = 0x4;
	//internal const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
	internal const uint FILE_ATTRIBUTE_ARCHIVE = 0x20;
	//internal const uint FILE_ATTRIBUTE_DEVICE = 0x40;
	internal const uint FILE_ATTRIBUTE_NORMAL = 0x80;
	internal const uint FILE_ATTRIBUTE_TEMPORARY = 0x100;
	//internal const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x200;
	//internal const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
	//internal const uint FILE_ATTRIBUTE_COMPRESSED = 0x800;
	internal const uint FILE_ATTRIBUTE_OFFLINE = 0x1000;
	//internal const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x2000;
	internal const uint FILE_ATTRIBUTE_ENCRYPTED = 0x4000;
	//internal const uint FILE_ATTRIBUTE_INTEGRITY_STREAM = 0x8000;
	//internal const uint FILE_ATTRIBUTE_VIRTUAL = 0x10000;
	//internal const uint FILE_ATTRIBUTE_NO_SCRUB_DATA = 0x20000;
	
	internal const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
	internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;
	internal const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
	internal const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
	internal const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x8000000;
	internal const uint FILE_FLAG_DELETE_ON_CLOSE = 0x4000000;
	internal const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;
	internal const uint FILE_FLAG_POSIX_SEMANTICS = 0x1000000;
	internal const uint FILE_FLAG_SESSION_AWARE = 0x800000;
	internal const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x200000;
	internal const uint FILE_FLAG_OPEN_NO_RECALL = 0x100000;
	internal const uint FILE_FLAG_FIRST_PIPE_INSTANCE = 0x80000;
	internal const uint FILE_FLAG_OPEN_REQUIRING_OPLOCK = 0x40000;
	
	[DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true)]
	static extern IntPtr _CreateFile(string lpFileName, Access dwDesiredAccess, CfShare dwShareMode, SECURITY_ATTRIBUTES lpSecurityAttributes, CfCreation creationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
	
	internal static Handle_ CreateFile(string lpFileName, Access dwDesiredAccess, CfShare dwShareMode, CfCreation creationDisposition, uint dwFlagsAndAttributes = FILE_ATTRIBUTE_NORMAL, IntPtr hTemplateFile = default, SECURITY_ATTRIBUTES lpSecurityAttributes = null)
		=> new Handle_(_CreateFile(lpFileName, dwDesiredAccess, dwShareMode, lpSecurityAttributes, creationDisposition, dwFlagsAndAttributes, hTemplateFile));
	//note: cannot return Handle_ directly from API because returns -1 if failed. The ctor then makes 0.
	
	//note: not using parameter types SECURITY_ATTRIBUTES and OVERLAPPED* because it makes JIT-compiling much slower in some time-critical places.
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool ReadFile(IntPtr hFile, void* lpBuffer, int nBytesToRead, out int nBytesRead, void* lpOverlapped = null);
	
	internal static bool ReadFileArr(IntPtr hFile, byte[] a, out int nBytesRead, void* lpOverlapped = null) {
		fixed (byte* p = a) return ReadFile(hFile, p, a.Length, out nBytesRead, lpOverlapped);
	}
	
	internal static bool ReadFileArr(IntPtr hFile, out byte[] a, int size, out int nBytesRead, void* lpOverlapped = null) {
		a = new byte[size];
		return ReadFileArr(hFile, a, out nBytesRead, lpOverlapped);
	}
	
	//internal static byte[] ReadFileArr(string file) {
	//	using var h = CreateFile(file, Api.GENERIC_READ, FILE_SHARE_ALL, OPEN_EXISTING);
	//	if (h.Is0 || !GetFileSizeEx(h, out long size) || !ReadFileArr(h, out var a, (int)size, out _)) throw new AuException(0);
	//	return a;
	//}
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool WriteFile(IntPtr hFile, void* lpBuffer, int nBytesToWrite, out int nBytesWritten, void* lpOverlapped = null);
	//note: lpNumberOfBytesWritten can be null only if lpOverlapped is not null.
	
	//note: don't use overloads, because we Jit_.Compile("WriteFile").
	internal static bool WriteFile2(IntPtr hFile, RByte a, out int nBytesWritten) {
		fixed (byte* p = a) return WriteFile(hFile, p, a.Length, out nBytesWritten);
	}
	
	//internal static bool WriteFile2(IntPtr hFile, RByte a, out int nBytesWritten, void* lpOverlapped)
	//{
	//	fixed (byte* p = a) return WriteFile(hFile, p, a.Length, out nBytesWritten, lpOverlapped);
	//}
	
	internal struct OVERLAPPED {
		nint _1, _2;
		int _3, _4;
		public IntPtr hEvent;
	}
	
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct BY_HANDLE_FILE_INFORMATION {
		public uint dwFileAttributes;
		public long ftCreationTime;
		public long ftLastAccessTime;
		public long ftLastWriteTime;
		public uint dwVolumeSerialNumber;
		uint _nFileSizeHigh;
		uint _nFileSizeLow;
		public uint nNumberOfLinks;
		uint _nFileIndexHigh;
		uint _nFileIndexLow;
		
		public long FileSize => (long)((ulong)_nFileSizeHigh << 32 | _nFileSizeLow);
		
		public long FileIndex => (long)((ulong)_nFileIndexHigh << 32 | _nFileIndexLow);
	}
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
	
	//internal enum FILE_INFO_BY_HANDLE_CLASS
	//{
	//	FileBasicInfo,
	//	FileStandardInfo,
	//	FileNameInfo,
	//	FileRenameInfo,
	//	FileDispositionInfo,
	//	FileAllocationInfo,
	//	FileEndOfFileInfo,
	//	FileStreamInfo,
	//	FileCompressionInfo,
	//	FileAttributeTagInfo,
	//	FileIdBothDirectoryInfo,
	//	FileIdBothDirectoryRestartInfo,
	//	FileIoPriorityHintInfo,
	//	FileRemoteProtocolInfo,
	//	FileFullDirectoryInfo,
	//	FileFullDirectoryRestartInfo,
	//	FileStorageInfo,
	//	FileAlignmentInfo,
	//	FileIdInfo,
	//	FileIdExtdDirectoryInfo,
	//	FileIdExtdDirectoryRestartInfo,
	//	MaximumFileInfoByHandleClass
	//}
	
	//internal struct FILE_BASIC_INFO
	//{
	//	public long CreationTime;
	//	public long LastAccessTime;
	//	public long LastWriteTime;
	//	public long ChangeTime;
	//	public uint FileAttributes;
	//}
	
	//[DllImport("kernel32.dll", SetLastError = true)]
	//internal static extern bool GetFileInformationByHandleEx(IntPtr hFile, int FileInformationClass, void* lpFileInformation, int dwBufferSize);
	
	//[DllImport("kernel32.dll", SetLastError = true)]
	//internal static extern bool SetFileInformationByHandle(IntPtr hFile, int FileInformationClass, void* lpFileInformation, int dwBufferSize);
	
	internal const int FILE_BEGIN = 0;
	internal const int FILE_CURRENT = 1;
	internal const int FILE_END = 2;
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool SetFilePointerEx(IntPtr hFile, long liDistanceToMove, long* lpNewFilePointer, int dwMoveMethod);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool SetEndOfFile(IntPtr hFile);
	
	[DllImport("kernel32.dll", EntryPoint = "CreateMailslotW", SetLastError = true)]
	static extern IntPtr _CreateMailslot(string lpName, uint nMaxMessageSize, int lReadTimeout, SECURITY_ATTRIBUTES lpSecurityAttributes);
	
	internal static Handle_ CreateMailslot(string lpName, uint nMaxMessageSize, int lReadTimeout, SECURITY_ATTRIBUTES lpSecurityAttributes)
		=> new Handle_(_CreateMailslot(lpName, nMaxMessageSize, lReadTimeout, lpSecurityAttributes));
	//note: cannot return Handle_ directly from API because returns -1 if failed. The ctor then makes 0.
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GetMailslotInfo(IntPtr hMailslot, uint* lpMaxMessageSize, out int lpNextSize, out int lpMessageCount, int* lpReadTimeout = null);
	
	[DllImport("kernel32.dll")]
	internal static extern int GetApplicationUserModelId(IntPtr hProcess, ref int AppModelIDLength, char* sbAppUserModelID);
	
	[DllImport("kernel32.dll", EntryPoint = "GetEnvironmentVariableW", SetLastError = true)]
	static extern int _GetEnvironmentVariable(string lpName, char* lpBuffer, int nSize);
	
	/// <summary>
	/// Calls API <b>GetEnvironmentVariable</b>.
	/// Returns <c>null</c> if variable not found.
	/// Does not support <c>folders.X</c>.
	/// </summary>
	/// <param name="name">Case-insensitive name. Without <c>%</c>.</param>
	[SkipLocalsInit]
	internal static string GetEnvironmentVariable(string name) {
		using FastBuffer<char> b = new();
		for (; ; ) if (b.GetString(_GetEnvironmentVariable(name, b.p, b.n), out var s)) return s;
	}
	
	/// <summary>
	/// Returns <c>true</c> if environment variable exists.
	/// </summary>
	internal static bool EnvironmentVariableExists(string name) => 0 != _GetEnvironmentVariable(name, null, 0);
	
	[DllImport("kernel32.dll", EntryPoint = "SetEnvironmentVariableW", SetLastError = true)]
	internal static extern bool SetEnvironmentVariable(string lpName, string lpValue);
	
	[DllImport("kernel32.dll", EntryPoint = "ExpandEnvironmentStringsW")]
	static extern int _ExpandEnvironmentStrings(string lpSrc, char* lpDst, int nSize);
	
	/// <summary>
	/// Calls API <b>ExpandEnvironmentStrings</b>.
	/// Returns <c>false</c> if failed or result is same; then <i>r</i> is <i>s</i>.
	/// <i>r</i> can be same variable as <i>s</i>.
	/// </summary>
	[SkipLocalsInit]
	internal static bool ExpandEnvironmentStrings(string s, out string r) {
		using FastBuffer<char> b = new();
		for (; ; ) if (b.GetString(_ExpandEnvironmentStrings(s, b.p, b.n), out r, BSFlags.ReturnsLengthWith0, s)) return (object)r != s;
	}
	
	[DllImport("kernel32.dll", EntryPoint = "GetEnvironmentStringsW")]
	internal static extern char* GetEnvironmentStrings();
	
	[DllImport("kernel32.dll", EntryPoint = "FreeEnvironmentStringsW")]
	internal static extern bool FreeEnvironmentStrings(char* penv);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern int GetProcessId(IntPtr Process);
	
	internal struct FILETIME {
		public uint dwLowDateTime;
		public uint dwHighDateTime;
		
		public static implicit operator long(FILETIME ft) { return (long)((ulong)ft.dwHighDateTime << 32 | ft.dwLowDateTime); } //in Release faster than *(long*)&ft
	}
	
	internal struct WIN32_FIND_DATA {
		public FileAttributes dwFileAttributes;
		public FILETIME ftCreationTime;
		public FILETIME ftLastAccessTime;
		public FILETIME ftLastWriteTime;
		public uint nFileSizeHigh;
		public uint nFileSizeLow;
		public uint dwReserved0;
		public uint dwReserved1;
		public fixed char cFileName[260];
		public fixed char cAlternateFileName[14];
		
		internal unsafe string Name {
			get {
				fixed (char* p = cFileName) {
					if (p[0] == '.') {
						if (p[1] == '\0') return null;
						if (p[1] == '.' && p[2] == '\0') return null;
					}
					return new string(p);
				}
			}
		}
		
		/// <summary>
		/// Returns nonzero if this is a NTFS link: 1 symlink, 2 mount, 3 other.
		/// </summary>
		internal int IsNtfsLink
			=> dwFileAttributes.Has(FileAttributes.ReparsePoint) && 0 != (dwReserved0 & 0x20000000)
			? dwReserved0 switch { 0xA000000C => 1, 0xA0000003 => 2, _ => 3 }
			: 0;
	}
	
	[DllImport("kernel32.dll", EntryPoint = "FindFirstFileW", SetLastError = true)]
	internal static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);
	
	[DllImport("kernel32.dll", EntryPoint = "FindNextFileW", SetLastError = true)]
	internal static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);
	
	[DllImport("kernel32.dll")]
	internal static extern bool FindClose(IntPtr hFindFile);
	
#if TEST_FINDFIRSTFILEEX
		internal enum FINDEX_INFO_LEVELS
		{
			FindExInfoStandard,
			FindExInfoBasic,
			FindExInfoMaxInfoLevel
		}

		internal const uint FIND_FIRST_EX_LARGE_FETCH = 0x2;

		[DllImport("kernel32.dll", EntryPoint = "FindFirstFileExW")]
		internal static extern IntPtr FindFirstFileEx(string lpFileName, FINDEX_INFO_LEVELS fInfoLevelId, out WIN32_FIND_DATA lpFindFileData, int fSearchOp, IntPtr lpSearchFilter, uint dwAdditionalFlags);
#endif
	
	internal const uint MOVEFILE_REPLACE_EXISTING = 0x1;
	internal const uint MOVEFILE_COPY_ALLOWED = 0x2;
	internal const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;
	internal const uint MOVEFILE_WRITE_THROUGH = 0x8;
	internal const uint MOVEFILE_CREATE_HARDLINK = 0x10;
	internal const uint MOVEFILE_FAIL_IF_NOT_TRACKABLE = 0x20;
	
	[DllImport("kernel32.dll", EntryPoint = "MoveFileExW", SetLastError = true)]
	internal static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);
	
	//[DllImport("kernel32.dll", EntryPoint = "CopyFileW", SetLastError = true)]
	//internal static extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);
	
	internal const uint COPY_FILE_FAIL_IF_EXISTS = 0x1;
	internal const uint COPY_FILE_RESTARTABLE = 0x2;
	internal const uint COPY_FILE_OPEN_SOURCE_FOR_WRITE = 0x4;
	internal const uint COPY_FILE_ALLOW_DECRYPTED_DESTINATION = 0x8;
	internal const uint COPY_FILE_COPY_SYMLINK = 0x800;
	internal const uint COPY_FILE_NO_BUFFERING = 0x1000;
	
	[DllImport("kernel32.dll", EntryPoint = "CopyFileExW", SetLastError = true)]
	internal static extern bool CopyFileEx(string lpExistingFileName, string lpNewFileName, LPPROGRESS_ROUTINE lpProgressRoutine, IntPtr lpData, int* pbCancel, uint dwCopyFlags);
	
	internal delegate uint LPPROGRESS_ROUTINE(long TotalFileSize, long TotalBytesTransferred, long StreamSize, long StreamBytesTransferred, uint dwStreamNumber, uint dwCallbackReason, IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData);
	
	[DllImport("kernel32.dll", EntryPoint = "DeleteFileW", SetLastError = true)]
	internal static extern bool DeleteFile(string lpFileName);
	
	[DllImport("kernel32.dll", EntryPoint = "RemoveDirectoryW", SetLastError = true)]
	internal static extern bool RemoveDirectory(string lpPathName);
	
	[DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true)]
	internal static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes); //ref SECURITY_ATTRIBUTES
	
	[DllImport("kernel32.dll", EntryPoint = "CreateDirectoryExW", SetLastError = true)]
	internal static extern bool CreateDirectoryEx(string lpTemplateDirectory, string lpNewDirectory, IntPtr lpSecurityAttributes); //ref SECURITY_ATTRIBUTES
	
	[DllImport("kernel32.dll", EntryPoint = "ReplaceFileW", SetLastError = true)]
	internal static extern bool ReplaceFile(string lpReplacedFileName, string lpReplacementFileName, string lpBackupFileName, uint dwReplaceFlags, IntPtr lpExclude = default, IntPtr lpReserved = default);
	
	[DllImport("kernel32.dll", EntryPoint = "GlobalAddAtomW")]
	internal static extern ushort GlobalAddAtom(string lpString);
	
	[DllImport("kernel32.dll")]
	internal static extern ushort GlobalDeleteAtom(ushort nAtom);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool ReadProcessMemory(HandleRef hProcess, IntPtr lpBaseAddress, void* lpBuffer, nint nSize, nint* lpNumberOfBytesRead);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool WriteProcessMemory(HandleRef hProcess, IntPtr lpBaseAddress, void* lpBuffer, nint nSize, nint* lpNumberOfBytesWritten);
	
	[DllImport("kernel32", SetLastError = true)]
	internal extern static IntPtr CreateActCtx(in ACTCTX actctx);
	
	[DllImport("kernel32", SetLastError = true)]
	internal extern static bool ActivateActCtx(IntPtr hActCtx, out IntPtr lpCookie);
	
	[DllImport("kernel32", SetLastError = true)]
	internal extern static bool DeactivateActCtx(int dwFlags, IntPtr lpCookie);
	
	[DllImport("kernel32.dll")]
	internal static extern void ReleaseActCtx(IntPtr hActCtx);
	
	internal const int ACTCTX_FLAG_RESOURCE_NAME_VALID = 0x8;
	internal const int ACTCTX_FLAG_HMODULE_VALID = 0x80;
	
	internal struct ACTCTX {
		public int cbSize;
		public uint dwFlags;
		public string lpSource;
		public ushort wProcessorArchitecture;
		public ushort wLangId;
		public IntPtr lpAssemblyDirectory;
		public IntPtr lpResourceName;
		public IntPtr lpApplicationName;
		public IntPtr hModule;
	}
	
	
	//internal const uint THREAD_TERMINATE = 0x1;
	internal const uint THREAD_SUSPEND_RESUME = 0x2;
	internal const uint THREAD_SET_CONTEXT = 0x10;
	internal const uint THREAD_QUERY_LIMITED_INFORMATION = 0x800;
	internal const uint THREAD_ALL_ACCESS = 0x1FFFFF;
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern Handle_ OpenThread(uint dwDesiredAccess, bool bInheritHandle, int dwThreadId);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern int SuspendThread(IntPtr hThread);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern uint ResumeThread(IntPtr hThread);
	
	//[DllImport("kernel32.dll", SetLastError = true)]
	//internal static extern bool TerminateThread(IntPtr hThread, int dwExitCode);
	
	internal const uint GMEM_FIXED = 0x0;
	internal const uint GMEM_MOVEABLE = 0x2;
	internal const uint GMEM_ZEROINIT = 0x40;
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern IntPtr GlobalAlloc(uint uFlags, nint dwBytes);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern IntPtr GlobalFree(IntPtr hMem);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern IntPtr GlobalLock(IntPtr hMem);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GlobalUnlock(IntPtr hMem);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern nint GlobalSize(IntPtr hMem);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern int WaitForMultipleObjectsEx(int nCount, IntPtr* pHandles, bool bWaitAll, int dwMilliseconds, bool bAlertable);
	
	[DllImport("kernel32.dll")]
	internal static extern int SleepEx(int dwMilliseconds, bool bAlertable);
	
	[DllImport("kernel32.dll", EntryPoint = "GetStartupInfoW")]
	internal static extern void GetStartupInfo(out STARTUPINFO lpStartupInfo);
	
	internal struct STARTUPINFO {
		public int cb;
		public IntPtr lpReserved;
		public char* lpDesktop;
		public char* lpTitle;
		public int dwX;
		public int dwY;
		public int dwXSize;
		public int dwYSize;
		public int dwXCountChars;
		public int dwYCountChars;
		public uint dwFillAttribute;
		public uint dwFlags;
		public ushort wShowWindow;
		public ushort cbReserved2;
		public IntPtr lpReserved2;
		public IntPtr hStdInput;
		public IntPtr hStdOutput;
		public IntPtr hStdError;
	}
	
	internal struct STARTUPINFOEX {
		public STARTUPINFO StartupInfo;
		public IntPtr lpAttributeList;
	}
	
	internal struct PROCESS_INFORMATION : IDisposable {
		public Handle_ hProcess;
		public Handle_ hThread;
		public int dwProcessId;
		public int dwThreadId;
		
		public void Dispose() {
			hThread.Dispose();
			hProcess.Dispose();
		}
	}
	
	//CreateProcess flags
	internal const uint CREATE_SUSPENDED = 0x4;
	internal const uint CREATE_NEW_CONSOLE = 0x10;
	internal const uint CREATE_UNICODE_ENVIRONMENT = 0x400;
	internal const uint EXTENDED_STARTUPINFO_PRESENT = 0x80000;
	//STARTUPINFO flags
	internal const uint STARTF_USESHOWWINDOW = 0x1;
	internal const uint STARTF_FORCEOFFFEEDBACK = 0x80;
	internal const uint STARTF_USESTDHANDLES = 0x100;
	
	[DllImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true)]
	internal static extern bool CreateProcess(string lpApplicationName, char[] lpCommandLine, SECURITY_ATTRIBUTES lpProcessAttributes, SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, string lpEnvironment, string lpCurrentDirectory, in STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
	
	[DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUserW", SetLastError = true)]
	internal static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, char[] lpCommandLine, SECURITY_ATTRIBUTES lpProcessAttributes, SECURITY_ATTRIBUTES lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, string lpEnvironment, string lpCurrentDirectory, in STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
	
	[DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerW", SetLastError = true)]
	internal static extern Handle_ CreateWaitableTimer(SECURITY_ATTRIBUTES lpTimerAttributes, bool bManualReset, string lpTimerName);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool SetWaitableTimer(IntPtr hTimer, ref long lpDueTime, int lPeriod = 0, IntPtr pfnCompletionRoutine = default, IntPtr lpArgToCompletionRoutine = default, bool fResume = false);
	
	[DllImport("kernel32.dll", EntryPoint = "OpenWaitableTimerW", SetLastError = true)]
	internal static extern Handle_ OpenWaitableTimer(uint dwDesiredAccess, bool bInheritHandle, string lpTimerName);
	
	[DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", SetLastError = true)]
	internal static extern int GetModuleFileName(IntPtr hModule, char* lpFilename, int nSize);
	
	internal const uint PIPE_ACCESS_INBOUND = 0x1;
	internal const uint PIPE_ACCESS_OUTBOUND = 0x2;
	internal const uint PIPE_ACCESS_DUPLEX = 0x3;
	internal const uint PIPE_TYPE_MESSAGE = 0x4;
	internal const uint PIPE_READMODE_MESSAGE = 0x2;
	internal const uint PIPE_REJECT_REMOTE_CLIENTS = 0x8;
	
	[DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", SetLastError = true)]
	static extern IntPtr _CreateNamedPipe(string lpName, uint dwOpenMode, uint dwPipeMode, uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize, uint nDefaultTimeOut, SECURITY_ATTRIBUTES lpSecurityAttributes);
	
	internal static Handle_ CreateNamedPipe(string lpName, uint dwOpenMode, uint dwPipeMode, uint nMaxInstances, uint nOutBufferSize, uint nInBufferSize, uint nDefaultTimeOut, SECURITY_ATTRIBUTES lpSecurityAttributes)
		=> new Handle_(_CreateNamedPipe(lpName, dwOpenMode, dwPipeMode, nMaxInstances, nOutBufferSize, nInBufferSize, nDefaultTimeOut, lpSecurityAttributes));
	//note: cannot return Handle_ directly from API because returns -1 if failed. The ctor then makes 0.
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool CreatePipe(out Handle_ hReadPipe, out Handle_ hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool ConnectNamedPipe(IntPtr hNamedPipe, OVERLAPPED* lpOverlapped);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool DisconnectNamedPipe(IntPtr hNamedPipe);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool GetOverlappedResult(IntPtr hFile, ref OVERLAPPED lpOverlapped, out int lpNumberOfBytesTransferred, bool bWait);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool CancelIo(IntPtr hFile);
	
	[DllImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", SetLastError = true)]
	internal static extern bool WaitNamedPipe(string lpNamedPipeName, int nTimeOut);
	
	//[DllImport("kernel32.dll", EntryPoint = "CallNamedPipeW", SetLastError = true)]
	//internal static extern bool CallNamedPipe(string lpNamedPipeName, void* lpInBuffer, int nInBufferSize, out int lpOutBuffer, int nOutBufferSize, out int lpBytesRead, int nTimeOut);
	
	//[DllImport("kernel32.dll", SetLastError = true)]
	//internal static extern bool GetNamedPipeClientProcessId(IntPtr Pipe, out int ClientProcessId);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool PeekNamedPipe(IntPtr hNamedPipe, void* lpBuffer, int nBufferSize, out int lpBytesRead, out int lpTotalBytesAvail, IntPtr lpBytesLeftThisMessage = default);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool AllocConsole();
	
	[DllImport("kernel32.dll", EntryPoint = "OutputDebugStringW")]
	internal static extern void OutputDebugString(string lpOutputString);
	
	[DllImport("kernel32.dll")]
	internal static extern bool SetProcessWorkingSetSize(IntPtr hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);
	
	//internal struct PROCESS_MEMORY_COUNTERS
	//{
	//	public int cb;
	//	public int PageFaultCount;
	//	public nint PeakWorkingSetSize;
	//	public nint WorkingSetSize;
	//	public nint QuotaPeakPagedPoolUsage;
	//	public nint QuotaPagedPoolUsage;
	//	public nint QuotaPeakNonPagedPoolUsage;
	//	public nint QuotaNonPagedPoolUsage;
	//	public nint PagefileUsage;
	//	public nint PeakPagefileUsage;
	//}
	
	//[DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
	//internal static extern bool GetProcessMemoryInfo(IntPtr Process, ref PROCESS_MEMORY_COUNTERS ppsmemCounters, int cb);
	
	[DllImport("kernel32.dll", EntryPoint = "FindResourceW", SetLastError = true)]
	public static extern IntPtr FindResource(IntPtr hModule, nint lpName, nint lpType);
	
	internal const int RT_GROUP_ICON = 14;
	
	//internal const int STD_INPUT_HANDLE = -10;
	internal const int STD_OUTPUT_HANDLE = -11;
	
	[DllImport("kernel32.dll")]
	internal static extern nint GetStdHandle(int nStdHandle);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern uint GetConsoleOutputCP();
	
	[DllImport("kernel32.dll")]
	internal static extern nint SetUnhandledExceptionFilter(nint _);
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern uint QueueUserAPC(delegate* unmanaged<GCHandle, void> pfnAPC, IntPtr hThread, GCHandle dwData);
	
	internal delegate void PAPCFUNC(nint Parameter);
	
	[DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW")]
	internal static extern int GetDriveType(string lpRootPathName);
	
	/// <summary>
	/// Use this API instead of <b>Directory.CreateSymbolicLink</b> which has a bug: does not throw exception when fails (eg non-admin).
	/// Note: the API fails if non-admin.
	///		With flag 2 does not fail if enabled developer mode.
	///		It seems can be enabled for non-admin in <c>gpedit.msc</c>; not tested; google for more info.
	///		Somewhere found this info, but it's incorrect: "Windows 11 doesn’t require administrative privileges to create symbolic links".
	/// </summary>
	/// <param name="dwFlags">1 - directory.</param>
	[DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.U1)] //BOOLEAN
	internal static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, uint dwFlags);
	
	[DllImport("kernel32.dll")]
	internal static extern int GetACP();
	
	[DllImport("kernel32.dll", SetLastError = true)]
	internal static extern bool DeviceIoControl(IntPtr hDevice, int dwIoControlCode, void* lpInBuffer, int nInBufferSize, void* lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, nint lpOverlapped = 0);
	
	internal const int IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;
	
	internal struct STORAGE_PROPERTY_QUERY {
		public int PropertyId;
		public int QueryType;
		public byte AdditionalParameters;
	}
	
	internal struct DEVICE_SEEK_PENALTY_DESCRIPTOR {
		public uint Version;
		public uint Size;
		public byte IncursSeekPenalty;
	}
	
	[DllImport("kernel32.dll", EntryPoint = "GetVolumePathNameW", SetLastError = true)]
	internal static extern bool GetVolumePathName(string lpszFileName, char* lpszVolumePathName, int cchBufferLength);
	
	[DllImport("kernel32.dll", EntryPoint = "GetVolumeNameForVolumeMountPointW", SetLastError = true)]
	internal static extern bool GetVolumeNameForVolumeMountPoint(string lpszVolumeMountPoint, char* lpszVolumeName, int cchBufferLength);
	
	
	#region undocumented
	
	internal delegate int CheckElevationEnabled(out int pResult);
	
	#endregion
}
