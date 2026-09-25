// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "util.h"
#include "Reg.h"


BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
	switch (ul_reason_for_call) {
	case DLL_PROCESS_ATTACH:
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

enum class Mode {
	Installing = 1, //installing app first time using MSI. Maybe upgrading from non-MSI.
	Upgrading = 2, //installing newer app version using MSI. Not when upgrading from non-MSI. Downgrading not allowed.
	Repairing = 3, //reinstalling same app version
	Uninstalling = 4
};

class Msi {
	MSIHANDLE _h = 0;
public:
	Msi(MSIHANDLE h) : _h(h) {}

	bool GetProperty(LPCWSTR name, wstring& s) const {
		DWORD size = 0;
		wchar_t c1 = 0;
		if (MsiGetPropertyW(_h, name, &c1, &size) != ERROR_MORE_DATA) return false;
		s.resize(++size);
		if (MsiGetPropertyW(_h, name, s.data(), &size) != ERROR_SUCCESS) return false;
		s.resize(size);
		return true;
	}

	bool SetProperty(LPCWSTR name, const wstring& value) {
		return MsiSetPropertyW(_h, name, value.data()) == ERROR_SUCCESS;
	}

	const wstring operator[](LPCWSTR property) const {
		wstring s;
		GetProperty(property, s);
		return s;
	}

	//const wstring Format(LPCWSTR property) const {
	//	wstring s;
	//	GetProperty(property, s);
	//	return format(L"{}={}", property, s);
	//}

	bool Is(LPCWSTR property) const {
		wstring s;
		GetProperty(property, s);
		return !s.empty();
	}

	Mode GetMode() {
		//Print(Format(L"Installed"));
		//Print(Format(L"REMOVE"));
		//Print(Format(L"REINSTALL"));
		//Print(Format(L"UPGRADEPROPERTY"));
		//Print(Format(L"UPGRADINGPRODUCTCODE"));
/*
							[install new]		[repair]	[uninstall]		[upgrade-install]		[upgrade-remove]
Installed										date		date									date
REMOVE														ALL										ALL
REINSTALL										ALL
UPGRADEPROPERTY																GUID
UPGRADINGPRODUCTCODE																				GUID of new version
*/
		if (Is(L"Installed")) return Is(L"REMOVE") ? Mode::Uninstalling : Mode::Repairing;
		return Is(L"UPGRADEPROPERTY") ? Mode::Upgrading : Mode::Installing;
	}

	//Returns true if running without GUI (/qn). Returns false if with normal or reduced GUI.
	//Note: always returns false in the "uninstall old" phase of upgrading. It's because of different version than in the window title. Not called in that phase.
	bool NoUI() const {
		//wstring s1; if(GetProperty(L"UILevel", s1)) Printf(L"UILevel=%s", S(s1)); else Print(L"no UILevel"); //always 2

		wstring version;
		if (!GetProperty(L"ProductVersion", version)) return false;

		auto w = FindWindowW(nullptr, S(L"LibreAutomate " + version + L" - Windows Installer"));
		return !IsWindowVisible(w);
		//HACK
	}
};

struct _DataForDeferredCA {
	wstring dirBS;
	Mode mode = {};
	bool noUI = {};

	void Set(Msi msi, LPCWSTR ca) {
		if (dirBS.empty()) return;
		wstring s;
		s.reserve(2 + dirBS.size());
		s += (wchar_t)mode + L'0';
		s += noUI ? L'1' : L'0';
		s += dirBS;
		msi.SetProperty(ca, s);
	}

	bool Get(Msi msi) {
		wstring s = msi[L"CustomActionData"];
		if (s.empty()) return false;
		mode = (Mode)(s[0] - L'0');
		noUI = s[1] == L'1';
		dirBS.assign(s, 2, s.size() - 2);
		if (dirBS.back() != L'\\') dirBS += L'\\';
		return true;
	}
};

struct _RegistryUninstallKeyInfo {
private:
	Reg _rkUninstall;
	LPCWSTR _keyName = nullptr;
public:
	wstring installLocation;

	bool Init(bool writable) {
		if (_rkUninstall.Open(HKEY_LOCAL_MACHINE, LR"(SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall)", writable)) {
			Reg rk;
			if (rk.Open(_rkUninstall, _keyName = L"LibreAutomate", false) || rk.Open(_rkUninstall, _keyName = L"LibreAutomate C#_is1", false)) {
				if (rk.GetString(L"InstallLocation", installLocation)) return true;
			}
		}
		return false;
	}

	void DeleteKey() {
		_rkUninstall.DeleteSubkey(_keyName);
	}
};

bool _IsAtHome() {
	return GetEnvironmentVariableW(L"Au.Home<PC>", nullptr, 0) > 0;
}

bool _EnsureAppNotRunning() {
	auto mutex = OpenMutexW(SYNCHRONIZE, false, L"Au.Editor.Mutex.m3gVxcTJN02pDrHiQ00aSQ");
	if (mutex == 0) return true;
	auto w = FindWindowW(L"Au.Editor.TrayNotify", nullptr);
	if (GetPropW(w, L"LA_home")) { CloseHandle(mutex); return true; } //don't Close LA on dev PC in dev dir
	SendMessageW(w, WM_CLOSE, 0, 0);
	auto r1 = WaitForSingleObject(mutex, 5000);
	CloseHandle(mutex);
	return r1 == 0 || r1 == WAIT_ABANDONED;
}

void _UnloadDll(bool noUI) {
	int less = noUI ? 1 : 5;
	DWORD_PTR res;
	vector<HWND> a;

	//Close acc agent windows
	for (HWND w = 0; w = FindWindowExW(HWND_MESSAGE, w, L"AuCpp_IPA_1", nullptr); ) a.push_back(w);
	int n = (int)a.size();
	if (n > 0) {
		for (int i = 0; i < n; i++) SendMessageTimeout(a[i], WM_CLOSE, 0, 0, SMTO_ABORTIFHUNG, 5000 / less, &res);
		a.clear();
		Sleep(n * 50);
	}
	//info: this process is not admin. Admin processes receive WM_CLOSE because the injected dll calls ChangeWindowMessageFilter(WM_CLOSE, 1).
	// However the code below may not work. But rarely need it, even difficult to test.

	//unload from processes where loaded by the clipboard hook
	SendMessageTimeout(HWND_BROADCAST, 0, 0, 0, SMTO_ABORTIFHUNG, 1000 / less, &res);

	unordered_set<int> hs;
	for (HWND w = 0; w = FindWindowExW(HWND_MESSAGE, w, nullptr, nullptr); )
		if (hs.insert(GetWindowThreadProcessId(w, nullptr)).second)
			a.push_back(w);
	for (int i = 0; i < (int)a.size(); i++)
		SendMessageTimeout(a[i], 0, 0, 0, SMTO_ABORTIFHUNG, 1000 / less, &res);
	Sleep(500 / less);
}

// Called by LA_BeforeRemoveFiles when installing first time.
void _UpgradeFromNonMsi(const wstring& dirBS) {
	//Print(L"_UpgradeFromNonMsi");
	//unregister old non-MSI-installed app version and delete its unused files
	_RegistryUninstallKeyInfo uki;
	if (uki.Init(true)) {
		if (util::IsSameFile(uki.installLocation, dirBS)) {
			LPCWSTR a[] = {
				L"uninstall.exe",
				L"installed.txt",
				L"unins000.exe",
				L"unins000.dat",
				L"unins001.exe",
				L"unins001.dat",
				L"32\\Au.AppHost.exe",
				L"64\\Au.AppHost.exe",
				L"64\\ARM\\Au.AppHost.exe",
				L"Au.Task-x64.exe",
				L"cookbook.db",
				L"Au.Task.dll",
				L"Au.Task.deps.json",
				L"Au.Task-arm.deps.json",
				L"Au.Task.runtimeconfig.json",
				L"Au.Task-arm.runtimeconfig.json",
			};
			for (auto v : a) {
				DeleteFileW(S(dirBS + v));
			}
		}
		uki.DeleteKey();
	}
	//note: don't run uninstall.exe of old non-MSI installer. We are upgrading, not uninstalling.
}

// Called by LA_AfterInstallFiles when installing first time, upgrading or repairing.
void _Install(const wstring& dirBS, Mode mode, bool noUI) {
	//Print(L"_Install");

	//info: MSI already created the app directory (dirBS)
	util::CreateUserWritableDirectory(dirBS + L"SDK");
	util::CreateUserWritableDirectory(dirBS + L"Git");
	util::CreateUserWritableDirectory(util::FolderPath(FOLDERID_ProgramData, L"LibreAutomate")); //not used by LA; Create for scripts that want to use it

	if (!util::IsWin81OrNewer()) { //workaround: on Win7 dotnet nuget/publish fails to connect to nuget
		LPCWSTR key = LR"(SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client)", name = L"DisabledByDefault";
		Reg rk; DWORD val = 0;
		if (rk.Create(HKEY_LOCAL_MACHINE, key) && !rk.GetDword(name, val)) rk.SetDword(name, 0);
	}

	if (util::IsWinArm64()) { //Au.Editor.exe must be arm64
		wstring s = dirBS + L"Au.Editor.exe";
		util::MoveFile(s, dirBS + L"Au.Editor-x64.exe");
		util::MoveFile(dirBS + L"Au.Editor-arm.exe", s);
	}

	//note: can't install .NET. Error "an installer is already running". Installing earlier in an immediate CA fails too.
	// Also the environment variables used for detecting .NET may not exist in this process.
	//void InstallDotnetIfNeed();
	//if (mode != Mode::Repairing && !noUI) InstallDotnetIfNeed();
	// 
	//note: can't use the "Package Dependencies" feature of Master Packager Dev: https://docs.masterpackager.com/master-packager-dev/msi-features/package-dependencies
	//Problems:
	//- cannot install LA where winget is unavailable, eg Win7, Sandbox. Or if fails.
	//- the .NET detection is unreliable. Eg .NET may be installed without admin rights (no key in HKLM). I would do like the apphost.
	//- on ARM64 the Registry value pattern is different. Also may install wrong version, need to test.
	//- in the UAC consent UI the program is "LibreAutomate Installer Elevator.exe", and Publisher is Unknown.
	//- it downloads/installs without user consent. I would add a checkbox in UI.
}

// Called by LA_BeforeRemoveFiles when uninstalling.
void _Uninstall(const wstring& dirBS) {
	if (util::IsWinArm64()) DeleteFileW(S(dirBS + L"Au.Editor-x64.exe"));

	//Print(L"_Uninstall");
	util::DeleteDirectoryTree(dirBS + L"SDK");
	util::DeleteDirectoryTree(dirBS + L"Git");

	util::DeleteDataDir(util::FolderPath(FOLDERID_ProgramData, L"LibreAutomate"), {}); //not used by LA
	if (!_IsAtHome()) {
		util::DeleteDataDir(util::FolderPath(FOLDERID_RoamingAppData, L"LibreAutomate"), { L"AI", L"WebView2" });
		util::DeleteDataDir(util::FolderPath(FOLDERID_LocalAppData, L"LibreAutomate"), { L"download", L"iconCache", L"nugetIcons", L"optimization" });
	}
}

#define EXPORT_CA comment(linker, "/export:" __FUNCTION__ "=_" __FUNCTION__ "@4")

// This immediate custom action runs first. Medium IL.
// Runs before CostFinalize, because:
// - later cannot set properties.
// - the INSTALLDIR property still is not set, unless specified in command line.
extern "C" UINT __stdcall LA_BeforeCostFinalize(MSIHANDLE hMsi) {
#pragma EXPORT_CA
	try {
		Msi msi(hMsi);
		Mode mode = msi.GetMode();

		//Printf(L"%S.  mode=%i  INSTALLDIR=%s",
		//	__func__, mode, S(msi[L"INSTALLDIR"]));

		//Let MSI replace all files. It's default versioning rules are not suitable for LA.
		msi.SetProperty(L"REINSTALLMODE", L"amus"); //note: even if specified in command line

		//Remember the install location when upgrading. MSI does not do it.
		//If INSTALLDIR is not set in command line:
		//	Get the install location from registry. It was saved by MSI (see msi.json > registries) or by an old non-MSI LA installer (the same key).
		//	If found, set the INSTALLDIR property. It will be used by MSI and other CA. It will be saved in Registry by MSI (specified in msi.json).
		if (mode == Mode::Installing || mode == Mode::Upgrading) {
			wstring dir = msi[L"INSTALLDIR"];
			if (dir.empty()) { //else specified in command line
				_RegistryUninstallKeyInfo uki;
				if (uki.Init(false)) {
					if (util::ExistsAsDir(uki.installLocation)) {
						msi.SetProperty(L"INSTALLDIR", uki.installLocation);
					}
				}
			}
		}
	}
	catch (const exception& e) { PRINT_EXCEPTION(e); }
	return ERROR_SUCCESS;
}

// This immediate custom action runs next. Medium IL.
// Runs before InstallValidate, because:
// - InstallValidate stops if finds locked files.
extern "C" UINT __stdcall LA_BeforeValidate(MSIHANDLE hMsi) {
#pragma EXPORT_CA
	try {
		Msi msi(hMsi);
		Mode mode = msi.GetMode();
		bool noUI = msi.NoUI();

		//pass MSI properties to deferred CA. They cannot get them from MSI.
		_DataForDeferredCA d{ msi[L"INSTALLDIR"], mode, noUI };
		d.Set(msi, L"LA_BeforeRemoveFiles");
		d.Set(msi, L"LA_AfterInstallFiles");

		//Printf(L"%S.  mode=%i  noUI=%i  INSTALLDIR=%s",
		//	__func__, mode, noUI, S(d.dirBS));

		_EnsureAppNotRunning(); //softly Close LA if running. MSI would terminate process.
		_UnloadDll(noUI); //unload AuCpp.dll from processes
	}
	catch (const exception& e) { PRINT_EXCEPTION(e); }
	return ERROR_SUCCESS;
}

//This deferred custom action runs next, before RemoveFiles. System IL.
extern "C" UINT __stdcall LA_BeforeRemoveFiles(MSIHANDLE hMsi) {
#pragma EXPORT_CA
	try {
		Msi msi(hMsi);

		//get data passed by the immediate CA, because deferred CA cannot get properties
		_DataForDeferredCA d;
		if (!d.Get(msi)) return ERROR_SUCCESS;

		//Printf(L"%S.  mode=%i  noUI=%i  INSTALLDIR=%s",
		//	__func__, d.mode, d.noUI, S(d.dirBS));

		if (d.mode == Mode::Uninstalling) {
			_Uninstall(d.dirBS);
		} else if (d.mode == Mode::Installing) {
			_UpgradeFromNonMsi(d.dirBS);
		}
	}
	catch (const exception& e) { PRINT_EXCEPTION(e); }
	return ERROR_SUCCESS;
}

//This deferred custom action runs last, after InstallFiles. System IL. Condition NOT REMOVE.
extern "C" UINT __stdcall LA_AfterInstallFiles(MSIHANDLE hMsi) {
#pragma EXPORT_CA
	try {
		Msi msi(hMsi);

		//get data passed by the immediate CA, because deferred CA cannot get properties
		_DataForDeferredCA d;
		if (!d.Get(msi)) return ERROR_SUCCESS;

		//Printf(L"%S.  mode=%i  noUI=%i  INSTALLDIR=%s",
		//	__func__, d.mode, d.noUI, S(d.dirBS));

		if (d.mode != Mode::Uninstalling)
			_Install(d.dirBS, d.mode, d.noUI);
	}
	catch (const exception& e) { PRINT_EXCEPTION(e); }
	return ERROR_SUCCESS;
}



extern "C" __declspec(dllexport)
void Test() {
	try {

	}
	catch (exception e) { PRINT_EXCEPTION(e); }
}

//extern "C" __declspec(dllexport)
//int TestSpeed(int repeat) {
//	//int n=0;
//	//for (int i = 0; i < repeat; i++) {
//	//}
//	//return n;
//}
