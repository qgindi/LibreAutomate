#define _CRT_SECURE_NO_WARNINGS
#pragma warning(disable: 6386 6385 6255 6305 6011 28251)

#include <string>
#include <vector>
#include <Windows.h>
//#include <Shlwapi.h>
//#pragma comment(lib, "shlwapi.lib")
#include "coreclrhost.h"

//min supported .NET version. Installed major must be ==, minor >=, patch any (we'll use highest found), preview any (we'll use release or highest preview).
//SHOULDDO: somehow auto-update, else can forget this when updating .NET version of C# projects (<TargetFramework>...</TargetFramework>).
#define NETVERMAJOR 6
#define NETVERMINOR 0
#define NETVER_SUPPORT_PREVIEW 0 //eg fails with 5.0.0 preview, MethodNotFound exception

#if 0
template<typename ... Args>
void Print(LPCSTR frm, Args ... args)
{
	HWND w = FindWindowW(L"QM_Editor", nullptr); if(w == 0) return;
	size_t size = sizeof...(Args) > 0 ? snprintf(nullptr, 0, frm, args ...) : 0;
	char* buf = size > 0 ? (char*)malloc(++size) : nullptr;
	if(buf != nullptr) {
		snprintf(buf, size, frm, args ...);
		frm = (LPCSTR)buf;
	} else {
		if(frm == nullptr) frm = "";
		size = strlen(frm) + 1;
	}
	auto u = (wchar_t*)malloc(size * 2);
	MultiByteToWideChar(CP_UTF8, 0, frm, (int)size, u, (int)size);
	SendMessage(w, WM_SETTEXT, -1, (LPARAM)u);
	free(u);
	if(buf != nullptr) free(buf);
}
#else
#define Print __noop
#endif

#define lenof(x) (sizeof(x) / sizeof(*(x)))
#define is32bit (sizeof(void*) == 4)

void _WstringFrom(std::wstring& r, LPCWSTR w1, size_t len1, LPCWSTR w2, size_t len2) {
	if(len1 == (size_t)(-1)) len1 = w1 == nullptr ? 0 : wcslen(w1);
	if(len2 == (size_t)(-1)) len2 = w2 == nullptr ? 0 : wcslen(w2);
	if(len1 > 0) {
		r.reserve(len1 + len2);
		r.assign(w1, len1);
		if(len2 > 0) r.append(w2, len2);
	} else r.assign(w2, len2);
}

void _WstringFrom(std::wstring& r, const std::wstring& w1, LPCWSTR w2, size_t len2) {
	_WstringFrom(r, w1.c_str(), w1.length(), w2, len2);
}

void _ToUtf8(LPCWSTR w, size_t len, std::string& r) {
	r.resize(WideCharToMultiByte(CP_UTF8, 0, w, (int)len, nullptr, 0, nullptr, nullptr));
	WideCharToMultiByte(CP_UTF8, 0, w, (int)len, &r[0], (int)r.length(), nullptr, nullptr);
	//note: reserve/resize does not work, because resize fills with 0 chars. It seems there is no way to avoid.
}

void _ToUtf8(const std::wstring& w, std::string& r) {
	_ToUtf8(w.c_str(), (int)w.length(), r);
}

int _ToUtf8(LPCWSTR w, size_t len, LPSTR utf8, size_t lenUtf8) {
	return WideCharToMultiByte(CP_UTF8, 0, w, (int)len, utf8, (int)lenUtf8, nullptr, nullptr);
}

//int _ToUtf16(LPCSTR utf8, int lenUtf8, LPWSTR utf16, int lenUtf16) {
//	return MultiByteToWideChar(CP_UTF8, 0, utf8, lenUtf8, utf16, lenUtf16);
//}

bool _StrEqualI(const std::wstring& s1, LPCWSTR s2) {
	int len = (int)s1.length();
	return wcslen(s2) == len && 2 == CompareStringOrdinal(s1.c_str(), len, s2, len, 1);
}
//bool _StrEqualI(const std::wstring& s1, const std::wstring& s2) {
//	int len = s1.length();
//	return s2.length() == len && 2 == CompareStringOrdinal(s1.c_str(), len, s2.c_str(), len, 1);
//}

bool _FileExists(LPCWSTR path) {
	return 0 == (FILE_ATTRIBUTE_DIRECTORY & GetFileAttributesW(path));
}

bool _DirExists(LPCWSTR path) {
	DWORD att = GetFileAttributesW(path);
	return att != 0xffffffff && 0 != (att & FILE_ATTRIBUTE_DIRECTORY);
}

bool _IsWow64() {
	BOOL isWow = 0;
	return IsWow64Process(GetCurrentProcess(), &isWow) && isWow;
}

struct PATHS {
	std::wstring appDir, netCore, netDesktop, coreclrDll, exeName;
	std::string asmDll, exePath;
	bool isPrivate;
};

struct VERSTRUCT {
	int vMinor, vPatch;
	bool preview;
	LPWSTR dirName;
};

bool GetRuntimeDir(LPWSTR dotnetDir, size_t len, std::wstring& rtDir, VERSTRUCT& ver, bool desktop) {
	//get all compatible installed .NET versions
	//	See Q:\Downloads\runtime-master\src\installer\corehost\cli\fxr\fx_resolver.cpp
	std::vector<VERSTRUCT> a;
	wcscpy(dotnetDir + len, desktop ? L"\\shared\\Microsoft.WindowsDesktop.App\\*" : L"\\shared\\Microsoft.NETCore.App\\*");
//#define TESTVERSIONS
#ifdef TESTVERSIONS
	LPCWSTR atest[] = { L"5.2.1", /*L"5.0.1",*/ L"5.0.0-preview.2", /*L"5.0.5-preview.2",*/ L"5.1.2", L"5.2.2", };
	//LPCWSTR atest[] = { L"3.0.1", L"5.0.0-preview.2", L"6.0.0", };
	for(int itest = 0; itest < lenof(atest); itest++) {
		LPWSTR s = (LPWSTR)atest[itest], s0 = s;
#else
	WIN32_FIND_DATAW fd;
	HANDLE h = FindFirstFileW(dotnetDir, &fd);
	if(h == INVALID_HANDLE_VALUE) return false;
	do {
		LPWSTR s = fd.cFileName, s0 = s;
		if(*s < '1' || *s > '9' || 0 == (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) continue;
#endif
		//Print("%S", s);
		int vMajor = wcstol(s, &s, 10); if(vMajor != NETVERMAJOR) continue;
		if(*s++ != '.' || *s < '0' || *s>'9') continue;
		int vMinor = wcstol(s, &s, 10); if(vMinor < NETVERMINOR) continue;
		//if(desktop && vMinor != ver.vMinor) continue; //no, dotnet apphost eats it
		if(*s++ != '.' || *s < '0' || *s>'9') continue;
		VERSTRUCT v;
		v.vMinor = vMinor;
		v.vPatch = wcstol(s, &s, 10);
		v.preview = s[0] == '-' && s[1] == 'p';
		if(!NETVER_SUPPORT_PREVIEW && v.preview && vMinor == NETVERMINOR && v.vPatch == 0) continue;
		v.dirName = _wcsdup(s0);
		//Print("%i %i %i %i", vMajor, v.vMinor, v.vPatch, v.preview);
		a.push_back(v);
#ifdef TESTVERSIONS
	}
#else
	} while(FindNextFileW(h, &fd));
	FindClose(h);
#endif
	int n = (int)a.size();
	if(n == 0) return false;

	//get best match
	//	https://docs.microsoft.com/en-us/dotnet/core/versions/selection
	//	Like documented there, we roll forward to the nearest minor version and use highest patch version.
	//	But dotnet apphost doesn't, eg fails if need 3.1 but found 3.2, and even if need 3.1.2 but found 3.1.0. Only rolls forward patch version.
	//		Tested long ago. Not tested with .NET 5. Maybe this was set in json by VS.
	int iBest = -1, bestMinor = -1, bestPatch = -1;
	for(int i = 0; i < n; i++) {
		int v = a[i].vMinor;
		if(desktop && v == ver.vMinor) { bestMinor = v; break; }
		if((DWORD)v < (DWORD)bestMinor) bestMinor = v;
	}
	for(int i = 0; i < n; i++) {
		if(a[i].vMinor != bestMinor) continue;
		int v = a[i].vPatch;
		if(desktop && v == ver.vPatch) { bestPatch = v; iBest = i; break; }
		if(v > bestPatch) {
			bestPatch = v; iBest = i;
		}
	}
	if(a[iBest].preview) { //don't need this if preview is auto-uninstalled when installing release, but I still did not have a chance to test
		for(int i = 0; i < n; i++) {
			if(a[i].vMinor != bestMinor || a[i].vPatch != bestPatch) continue;
			//if(!a[i].preview || StrCmpLogicalW(a[i].dirName, a[iBest].dirName) > 0) iBest = i; //no, it seems old previews are auto-unisnstalled
			if(!a[i].preview) {
				iBest = i; break;
			}
		}
	}
#ifdef TESTVERSIONS
	Print("BEST: %i %i %S", bestMinor, bestPatch, a[iBest].dirName);
	exit(0);
#endif

	_WstringFrom(rtDir, dotnetDir, wcslen(dotnetDir) - 1, a[iBest].dirName, -1);
	ver = a[iBest];

	for(int i = 0; i < n; i++) free(a[i].dirName);

	return true;
}

char s_asmName[800] = "\0hi7yl8kJNk+gqwTDFi7ekQ";

bool GetPaths(PATHS& p) {
	//https://github.com/dotnet/designs/blob/master/accepted/install-locations.md

	wchar_t w[1000];
	size_t lenApp, lenAppDir;

	//get exe full path
	lenApp = ::GetModuleFileNameW(0, w, lenof(w) - 50);
	_ToUtf8(w, lenApp, p.exePath);

	//get appDir and exeName
	for(lenAppDir = lenApp; w[--lenAppDir] != '\\'; ) { }
	p.appDir.assign(w, lenAppDir);
	p.exeName = w + lenAppDir + 1;

	//get asmDll
	if(s_asmName[0] != 0) { //exe created from script. See code in Compiler.cs, function _AppHost.
		_ToUtf8(w, lenAppDir + 1, p.asmDll);
		p.asmDll += s_asmName;
	} else {
		int replace = (is32bit && w[lenApp - 6] == '3' && w[lenApp - 5] == '2') ? 6 : 4; //if exe is "name32.exe", dll is "name.dll"
		_ToUtf8(w, lenApp - replace, p.asmDll);
		p.asmDll += ".dll";
	}

	//is coreclr.dll in app dir?
	wcscpy(w + lenAppDir, L"\\coreclr.dll");
	if(p.isPrivate = _FileExists(w)) {
		p.coreclrDll = w;
		p.netCore = p.appDir;
		p.netDesktop = p.appDir;
		return true;
	}

	//get .NET root path, like "C:\Program Files\dotnet". See Q:\Downloads\runtime-master\src\installer\corehost\cli\fxr_resolver.cpp.
	DWORD lenDotnet;
	for(int i = 0; ; i++) {
		if(i == 0) { //env var DOTNET_ROOT
			lenDotnet = GetEnvironmentVariableW(_IsWow64() ? L"DOTNET_ROOT(x86)" : L"DOTNET_ROOT", w, lenof(w));
		} else if(i == 1) { //registry InstallLocation
			wchar_t key[] = LR"(SOFTWARE\dotnet\Setup\InstalledVersions\x64)"; if(is32bit) { key[41] = '8'; key[42] = '6'; }
			HKEY hk1;
			if(0 != RegOpenKeyExW(HKEY_LOCAL_MACHINE, key, 0, KEY_READ | KEY_WOW64_32KEY, &hk1)) lenDotnet = 0;
			else {
				if(0 != RegQueryValueExW(hk1, L"InstallLocation", nullptr, nullptr, (LPBYTE)w, &(lenDotnet = lenof(w)))) lenDotnet = 0; else lenDotnet /= 2;
				RegCloseKey(hk1);
			}
		} else { //default location. Eg .NET Core 3.0.1 did not set InstallLocation.
			lenDotnet = ExpandEnvironmentStringsW(L"%ProgramFiles%\\dotnet", w, lenof(w));
			if(w[0] == '%') lenDotnet = 0;
			//SHGetSpecialFolderPathW //no, better don't use shell apis, it's not so important. Current .NET versions set the registry value. Dotnet apphost even uses literal "Program Files" etc string.
		}
		if(i > 0 && lenDotnet != 0) lenDotnet--;
		if(lenDotnet > 1 && w[lenDotnet - 1] == '\\') lenDotnet--;
		if(lenDotnet > 1 && lenDotnet < lenof(w) - 100 && _DirExists(w)) break;
		if(i == 2) return false;
	}
	//Print("%S", w);

	VERSTRUCT ver;
	if(!GetRuntimeDir(w, lenDotnet, p.netCore, ver, false)) return false;
	if(!GetRuntimeDir(w, lenDotnet, p.netDesktop, ver, true)) return false;

	//get coreclrDll
	_WstringFrom(p.coreclrDll, p.netCore, L"\\coreclr.dll", -1);
	return _FileExists(p.coreclrDll.c_str());
}

void BuildTpaList(const std::wstring& dir, std::string& tpaList, bool onlyAu = false)
{
	std::string dir8, name8; _ToUtf8(dir, dir8); dir8 += '\\';
	std::wstring wild; _WstringFrom(wild, dir, L"\\*.dll", 6);
	WIN32_FIND_DATAW fd;
	HANDLE h = FindFirstFileW(wild.c_str(), &fd);
	if(h != INVALID_HANDLE_VALUE) {
		do {
			wchar_t* s = fd.cFileName;
			if(onlyAu) {
				if(!(s[0] == 'A' && s[1] == 'u' && s[2] == '.')) continue; //Au.dll, Au.Editor.dll, etc
			} else {
				if(s[0] == 'a' && s[1] == 'p' && s[2] == 'i' && s[3] == '-') continue; //api-ms-
			}
			tpaList += dir8;
			_ToUtf8(s, name8); tpaList += name8;
			tpaList += ';';
		} while(FindNextFileW(h, &fd));
		FindClose(h);
	}

	//note: don't use stringstream, it makes file size *= 2.
}

const char** ArgsUtf8(int& nArgs) {
	int na = __argc - 1;
	auto a = __wargv + 1;
	size_t size = 0; for(int i = 0; i < na; i++) size += wcslen(a[i]) * 3 + 1;
	auto r = (const char**)malloc(na * sizeof(LPSTR) + size);
	LPSTR p = (LPSTR)(r + na);
	for(int i = 0; i < na; i++) {
		int k = _ToUtf8(a[i], -1, p, size);
		if(k == 0) exit(-1);
		r[i] = p;
		p += k;
		size -= k;
	}
	nArgs = na;
	return r;
}

struct _TaskInit
{
	const char* asmFile;
	const char** args;
	int nArgs;
};

int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR pCmdLine, int nCmdShow)
{
#if 0
	//LARGE_INTEGER c;
	//QueryPerformanceCounter(&c);
	//return c.LowPart;


	return 0;
#else
	//LARGE_INTEGER t1, t2, t3, t4; QueryPerformanceCounter(&t1);

	PATHS p;
	bool pathsOK = GetPaths(p);
	//Print("asmDll=%S", p.exeName.c_str());
	//Print("exePath=%s", p.exePath.c_str());
	//Print("asmDll=%s", p.asmDll.c_str());
	//Print("appDir=%S", p.appDir.c_str());
	//Print("netCore=%S", p.netCore.c_str());
	//Print("netDesktop=%S", p.netDesktop.c_str());
	//Print("coreclrDll=%S", p.coreclrDll.c_str());

	if(!pathsOK) {
		wchar_t w[200];
		wsprintfW(w, L"To run this application, need .NET Runtime.\r\nPlease install .NET Desktop Runtime %i.%i x%s.\r\n\r\nDownload from\r\nhttps://dotnet.microsoft.com/download\r\n\r\nOpen the web page?", NETVERMAJOR, NETVERMINOR, is32bit ? L"86" : L"64");
		if(IDYES == MessageBoxW(0, w, p.exeName.c_str(), MB_ICONERROR | MB_YESNO)) {
			AllowSetForegroundWindow(ASFW_ANY);
			int(__stdcall * ShellExecuteA)(HWND hwnd, LPCSTR lpOperation, LPCSTR lpFile, LPCSTR lpParameters, LPCSTR lpDirectory, INT nShowCmd);
			(*(FARPROC*)(&ShellExecuteA)) = GetProcAddress(LoadLibraryW(L"shell32"), "ShellExecuteA");
			ShellExecuteA(NULL, nullptr, "https://dotnet.microsoft.com/download", nullptr, nullptr, SW_SHOWNORMAL);
		}
		return -1;
	}

	HMODULE hm = LoadLibraryW(p.coreclrDll.c_str()); //3 ms
	if(hm == NULL) return -2;
	auto coreclr_initialize = (coreclr_initialize_ptr)GetProcAddress(hm, "coreclr_initialize");
	auto coreclr_execute_assembly = (coreclr_execute_assembly_ptr)GetProcAddress(hm, "coreclr_execute_assembly");
	auto coreclr_shutdown = (coreclr_shutdown_ptr)GetProcAddress(hm, "coreclr_shutdown");

	void* hostHandle;
	unsigned int domainId;
	int hr;
	{
		bool noAppPaths = false;
		std::string tpaList; tpaList.reserve(30000);
		if(p.isPrivate) {
			BuildTpaList(p.appDir, tpaList);
		} else {
			BuildTpaList(p.netDesktop, tpaList); //note: must be first, else eg WPF does not work because netCore dir contains WindowsBase too, and it is invalid
			BuildTpaList(p.netCore, tpaList);

			//workaround for AssemblyLoadContext.LoadFromAssemblyPath bug:
			//	If an assembly with same name is in APP_PATHS directories, loads it instead of the specified. Then exception if it is an older version etc.
			//	Workaround: don't use APP_PATHS for Au.Task.exe and Au.Editor.exe (for script roles miniProgram and editorExtension).
			//		Add Au.* assemblies to the TPA list. For others use the assembly resolve event; including Roslyn.
			//		For this reason we also don't add Roslyn to APP_PATHS.
			noAppPaths = _StrEqualI(p.exeName, L"Au.Editor.exe") || _StrEqualI(p.exeName, L"Au.Task.exe");
			if(noAppPaths) BuildTpaList(p.appDir, tpaList, true);
		}

		std::string appDir8, nd8;
		//APP_PATHS
		_ToUtf8(p.appDir, appDir8);
		std::string ap(appDir8);
		appDir8 += '\\';
		//NATIVE_DLL_SEARCH_DIRECTORIES
		std::wstring nd16; nd16.reserve(600);
		if(!noAppPaths) {
			nd16 += p.appDir;
#if _WIN64
			nd16 += L"\\runtimes\\win-x64\\native\\;";
#else
			nd16 += L"\\runtimes\\win-x86\\native\\;";
#endif
		}
		nd16 += p.netDesktop; nd16 += L"\\;";
		nd16 += p.netCore; nd16 += L"\\;";
		_ToUtf8(nd16, nd8);

		const char* propertyKeys[] = { "TRUSTED_PLATFORM_ASSEMBLIES", "NATIVE_DLL_SEARCH_DIRECTORIES", "APP_CONTEXT_BASE_DIRECTORY", "APP_PATHS" };
		const char* propertyValues[] = { tpaList.c_str(), nd8.c_str(), appDir8.c_str(), ap.c_str() };
		int nProp = noAppPaths ? 3 : 4;
		//Print("TPA:"); Print("%s", propertyValues[0]);
		//Print("APP:"); Print("%s", propertyValues[1]);
		//Print("ND:"); Print("%s", propertyValues[2]);
		//Print("ABD:"); Print("%s", propertyValues[3]);

		SetEnvironmentVariableW(L"COMPlus_legacyCorruptedStateExceptionsPolicy", L"1");

		//QueryPerformanceCounter(&t2); //all above code 6 ms cold, 3.6 hot

		hr = coreclr_initialize(p.exePath.c_str(), "main", nProp, propertyKeys, propertyValues, &hostHandle, &domainId);
		if(hr < 0) {
			return -3;
		}

		//QueryPerformanceCounter(&t3); //22 ms cold, 16 hot
		} //free temp strings eg tpaList 30000

	unsigned int ec = 0;
	if(0 == wcsncmp(pCmdLine, LR"(\\.\pipe\Au.Task-)", 17)) { //preloaded task process for a script with role miniProgram
		auto coreclr_create_delegate = (coreclr_create_delegate_ptr)GetProcAddress(hm, "coreclr_create_delegate");
		void (STDMETHODCALLTYPE * Init)(LPWSTR, _TaskInit&) = nullptr;
		coreclr_create_delegate(hostHandle, domainId, "Au", "Au.More.MiniProgram_", "Init", (void**)&Init); //waits until editor asks to execute a task; it sends task info through pipe
		//QueryPerformanceCounter(&t4); //1 ms
		_TaskInit t = { };
		Init(pCmdLine, t);
		if(t.asmFile != nullptr) { //null when editor exits
			hr = coreclr_execute_assembly(hostHandle, domainId, t.nArgs, t.args, t.asmFile, &ec);
		}
	} else {
		int nArgs = 0;
		const char* args0[1] = {};
		const char** args = *pCmdLine != 0 ? ArgsUtf8(nArgs) : args0;

		hr = coreclr_execute_assembly(hostHandle, domainId, nArgs, args, p.asmDll.c_str(), &ec); //6 ms
	}
	//Print("%i %i %i", (t2.LowPart - t1.LowPart) / 10, (t3.LowPart - t2.LowPart) / 10, (t4.LowPart - t3.LowPart) / 10);

	//tested: AppDomain.ProcessExit event is in coreclr_shutdown.
	//	AppDomain.UnhandledException event is in coreclr_execute_assembly, and it does not return.

	coreclr_shutdown(hostHandle, domainId);

	if(hr < 0) {
		return -4;
	}
	return ec;
#endif
	}
