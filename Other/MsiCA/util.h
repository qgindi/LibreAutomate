#pragma once
#include "pch.h"

#define S(s) (s).data()

//#define TRACE
#ifdef TRACE

void Printf(LPCWSTR frm, ...);
void Print(LPCWSTR s);
inline void Print(const wstring& s) { Print(S(s)); }
//inline void Print(wstring_view s) { Print(S(s)); } //no, may be not 0-terminated
inline void Print(const char*) = delete;
inline void Print(bool b) { Print(b ? L"true" : L"false"); }
inline void Print(int i) { Printf(L"%i", i); }
inline void Print(DWORD i) { Print((int)i); }
inline void Printx(DWORD i) { Printf(L"0x%X", i); }
inline void Print(void* i) { Printf(L"%p", i); }
inline void Print(size_t i) { Print((int)i); }

#else

inline void Printf(LPCWSTR, ...) {}
inline void Print(LPCWSTR) {}
inline void Print(const wstring&) {}
inline void Print(const char*) = delete;
inline void Print(bool) {}
inline void Print(int) {}
inline void Print(DWORD) {}
inline void Printx(DWORD) {}
inline void Print(void*) {}
inline void Print(size_t) {}

#endif

#define PRINT_EXCEPTION(e) Printf(L"Exception cought in %S(%i): %S: %S", __func__, __LINE__, typeid(e).name(), e.what())

#define Mbox(text) MessageBoxW(nullptr, text, L"MSI CA " __FUNCTION__, MB_OK | MB_TOPMOST);

//Deletes copy ctor and operator from the enclosing class.
//Usage: NONCOPYABLE(EnclosingType);
#define NONCOPYABLE(T) \
	T(const T&) = delete; \
	T& operator=(const T&) = delete


//Contains a kernel handle. Calls `CloseHandle` in dtor.
class Handle {
	HANDLE _h{};
public:
	Handle() = default;
	NONCOPYABLE(Handle);

	Handle(HANDLE h) : _h(h == INVALID_HANDLE_VALUE ? nullptr : h) {}

	~Handle() {
		if (_h) CloseHandle(_h);
	}

	operator HANDLE() const { return _h; }
	HANDLE* operator&() { return &_h; }

	void Close() {
		if (_h) {
			CloseHandle(_h);
			_h = {};
		}
	}
};


namespace str {
	//Formats string in the classic %x way.
	wstring FormatV(LPCWSTR format, va_list args);

	//Formats string in the classic %x way.
	wstring Format(LPCWSTR format, ...);

	wstring Concat(wstring_view a, wstring_view b);
	wstring Concat(wstring_view a, wstring_view b, wstring_view c);
	wstring Concat(initializer_list<wstring_view> a);

	//Returns "{dir}\{name}".
	//Or "{dir}{name}" if dir ends with a directory separator.
	//Exception if dir empty.
	inline wstring ConcatPath(wstring_view dir, wstring_view name) {
		if (dir.empty()) throw invalid_argument("empty directory");
		auto last = dir.back();
		return Concat(dir, last == L'\\' || last == '/' ? L"" : L"\\", name);
	}

	//true if two strings are equal. Case-insensitive, invariant locale.
	bool EqualsI(wstring_view a, wstring_view b);

	wstring Utf8ToUtf16(string_view s);
}

namespace util {
	//Gets known folder path and optionally path-combines with `append`.
	//If fails to get, returns `"?"`.
	//If `create` true, creates the folder if need. Else does not verify whether the folder exists.
	wstring FolderPath(REFKNOWNFOLDERID id, LPCWSTR append = nullptr, bool create = false);

	//Returns true if two paths are to the same existing file or directory, regardless of path format.
	//- useSymlink - if a path is a symlink (any kind of reparse point), use it, not its target.
	bool IsSameFile(const wstring& path1, const wstring& path2, bool useSymlink = false);

	//Creates a directory and adds write permission for authenticated users. Does nothing if the directory already exists.
	bool CreateUserWritableDirectory(const wstring& path);

	//Deletes a directory wilth all descendants.
	bool DeleteDirectoryTree(const wstring& path);

	//Deletes subdirectories `subdirNames` of directory `dir`. If then the `dir` directory is empty, deletes it too.
	void DeleteDataDir(const wstring& dir, initializer_list<LPCWSTR> subdirNames);

	//Gets environment variable. Returns empty string if not found.
	wstring GetEnv(LPCWSTR name);

	//Contains results of a "create process and wait" function.
	//The implicit cast to bool operator returns true if ran and the exit code is 0.
	struct ExecResult {
		//true if `CreateProcess` succeeded
		bool ran;

		//true if `CreateProcess` failed because the file or path not found
		bool notFound;

		//exit code
		int exitCode;

		operator bool() const { return ran && exitCode == 0; }
	};

	//Runs a console app and reads its stdout and stderr.
	ExecResult RunConsole(wstring& output, wstring_view exe, wstring_view args = {}, LPCWSTR curDir = nullptr);

	//Runs a console app, reads its stdout and stderr, and calls `onLine` for each line.
	//If the callback throws any exception, terminates the process and returns {}.
	//ExecResult RunConsole2(function<void(wstring_view)> onLine, wstring_view exe, wstring_view args = {}, LPCWSTR curDir = nullptr);

	//Calls `CreateProcess` and waits until the process ends.
	ExecResult RunProcess(wstring_view exe, wstring_view args = {}, LPCWSTR curDir = nullptr);

	inline bool ExistsAsFile(const wstring& path) {
		if (path.empty()) return false;
		auto a = GetFileAttributesW(S(path));
		return 0 == (a & FILE_ATTRIBUTE_DIRECTORY);
	}

	inline bool ExistsAsDir(const wstring& path) {
		if (path.empty()) return false;
		auto a = GetFileAttributesW(S(path));
		return a != -1 && 0 != (a & FILE_ATTRIBUTE_DIRECTORY);
	}

	//MoveFileEx
	inline bool MoveFile(const wstring& from, const wstring& to, DWORD flags = MOVEFILE_REPLACE_EXISTING) {
		return MoveFileExW(from.data(), to.data(), flags);
	}

	//inline WORD OsVersion() { all version API lie, even RtlGetVersion }

	//true if OS is Windows 8.1 or newer.
	inline bool IsWin81OrNewer() {
#pragma warning(suppress: 6387)
		return GetProcAddress(GetModuleHandleW(L"user32.dll"), "LogicalToPhysicalPointForPerMonitorDPI") != nullptr;
	}

	//true if OS is ARM64.
	inline bool IsWinArm64() {
		using F = BOOL(WINAPI*)(HANDLE, USHORT*, USHORT*);
		auto h = GetModuleHandleW(L"kernel32.dll");
		auto f = h ? (F)GetProcAddress(h, "IsWow64Process2") : nullptr;

		USHORT processMachine, nativeMachine;
		return f && f(GetCurrentProcess(), &processMachine, &nativeMachine)
			&& nativeMachine == IMAGE_FILE_MACHINE_ARM64;
	}
}

//Directory enumeration.
class DirectoryEnum {
	const wstring _pattern;
	const wstring_view _dirBS;
	HANDLE _h{};
public:
	WIN32_FIND_DATAW fd{};

	DirectoryEnum(const wstring& path, wstring_view pattern = L"*") :
		_pattern(str::ConcatPath(path, pattern)),
		_dirBS(_pattern.data(), path.size() + (_pattern[path.size()] == L'\\')) {}

	~DirectoryEnum() {
		if (_h) FindClose(_h);
	}

	NONCOPYABLE(DirectoryEnum);

	bool Next(wstring* path = nullptr) {
	g1:
		if (!_h) {
			_h = FindFirstFileExW(S(_pattern), FindExInfoBasic, &fd, FindExSearchNameMatch, nullptr, 0);
			if (_h == INVALID_HANDLE_VALUE) { _h = {}; return false; }
		} else if (!FindNextFileW(_h, &fd)) {
			return false;
		}

		if (fd.cFileName[0] == L'.')
			if (fd.cFileName[1] == L'\0' || (fd.cFileName[1] == L'.' && fd.cFileName[2] == L'\0'))
				goto g1;

		if (path) *path = str::Concat(_dirBS, fd.cFileName);

		return true;
	}

	LPCWSTR Name() { return fd.cFileName; }
	wstring_view NameSV() { return fd.cFileName; }
	bool IsDir() { return (bool)(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY); }
	bool IsLink() { return (bool)(fd.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT); }
};
