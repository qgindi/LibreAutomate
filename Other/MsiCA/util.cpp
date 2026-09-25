#include "pch.h"
#include "util.h"

HWND s_QM2;

#ifdef TRACE

void Print(LPCWSTR s) {
	if (!IsWindow(s_QM2)) {
		s_QM2 = FindWindowW(L"QM_Editor", 0); if (!s_QM2) return;
	}

	if (s == nullptr)s = L"";
	DWORD_PTR res;
	SendMessageTimeoutW(s_QM2, WM_SETTEXT, -1, (LPARAM)s, SMTO_BLOCK | SMTO_ABORTIFHUNG, 5000, &res);
}

void Printf(LPCWSTR format, ...) {
	va_list args;
	va_start(args, format);
	auto s = str::FormatV(format, args);
	va_end(args);
	Print(s);
}

//template<class... Args>
//void Printf(wformat_string<Args...> fmt, Args&&... args) {
//	auto s = format(fmt, forward<Args>(args)...);
//	Print(s);
//}

#endif

namespace str {
	wstring FormatV(LPCWSTR format, va_list args) {
		wchar_t buf[1024];
		auto bsize = _countof(buf);
		wstring s;
		for (auto b = buf;;) {
			int n = _vsnwprintf_s(b, bsize, _TRUNCATE, format, args);
			if (n >= 0) {
				if (b == buf) s.append(b, n); else s.resize(n);
				return s;
			}

			s.resize(bsize *= 2);
			b = s.data();
		}
	}

	wstring Format(LPCWSTR format, ...) {
		va_list args;
		va_start(args, format);
		auto s = FormatV(format, args);
		va_end(args);
		return s;
	}

	wstring Concat(wstring_view a, wstring_view b) {
		wstring s;
		s.reserve(a.size() + b.size());
		s.append(a);
		s.append(b);
		return s;
	}

	wstring Concat(wstring_view a, wstring_view b, wstring_view c) {
		wstring s;
		s.reserve(a.size() + b.size() + c.size());
		s.append(a);
		s.append(b);
		s.append(c);
		return s;
	}

	wstring Concat(initializer_list<wstring_view> a) {
		wstring s;
		size_t n = 0;
		for (auto v : a) n += v.size();

		s.reserve(n);
		for (auto v : a) s.append(v);
		return s;
	}

	bool EqualsI(wstring_view a, wstring_view b) {
		return CompareStringEx(
			LOCALE_NAME_INVARIANT,
			NORM_IGNORECASE,
			a.data(), (int)a.size(),
			b.data(), (int)b.size(),
			nullptr, nullptr, 0
		) == CSTR_EQUAL;
	}

	wstring Utf8ToUtf16(string_view s) {
		if (s.empty()) return {};

		int n = MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), nullptr, 0);
		if (!n) return {};

		wstring r(n, L'\0');
		MultiByteToWideChar(CP_UTF8, 0, s.data(), (int)s.size(), r.data(), n);

		return r;
	}
}

namespace util {

	wstring FolderPath(REFKNOWNFOLDERID id, LPCWSTR append, bool create) {
		PWSTR s = nullptr;
		HRESULT hr = SHGetKnownFolderPath(id, create ? KF_FLAG_CREATE : KF_FLAG_DONT_VERIFY, nullptr, &s);
		if (FAILED(hr)) return L"?";
		wstring r;
		if (append == nullptr) r = s; else r = str::ConcatPath(s, append);
		CoTaskMemFree(s);
		return r;
	}

	static HANDLE _OpenFileHandleForFileInfo(const wstring& path, bool ofSymlink = false) {
		return CreateFileW(S(path), 0, 7, nullptr, OPEN_EXISTING, ofSymlink ? FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT : FILE_FLAG_BACKUP_SEMANTICS, nullptr);
		//info: need FILE_FLAG_BACKUP_SEMANTICS for directories. Ignored for files.
	}

	bool IsSameFile(const wstring& path1, const wstring& path2, bool useSymlink) {
		Handle h1(_OpenFileHandleForFileInfo(path1, useSymlink)); if (!h1) return false;
		Handle h2(_OpenFileHandleForFileInfo(path2, useSymlink)); if (!h2) return false;
		BY_HANDLE_FILE_INFORMATION k1, k2;
		return GetFileInformationByHandle(h1, &k1)
			&& GetFileInformationByHandle(h2, &k2)
			&& k1.nFileIndexLow == k2.nFileIndexLow
			&& k1.nFileIndexHigh == k2.nFileIndexHigh
			&& k1.dwVolumeSerialNumber == k2.dwVolumeSerialNumber;
	}

	bool CreateUserWritableDirectory(const wstring& path) {
		if (!CreateDirectoryW(path.c_str(), nullptr)) return false;

		BYTE sidBuffer[SECURITY_MAX_SID_SIZE];
		DWORD sidSize = sizeof(sidBuffer);
		if (!CreateWellKnownSid(WinAuthenticatedUserSid, nullptr, sidBuffer, &sidSize)) return false;

		PACL oldDacl = nullptr;
		PSECURITY_DESCRIPTOR sd = nullptr;
		DWORD r = GetNamedSecurityInfoW(path.c_str(), SE_FILE_OBJECT, DACL_SECURITY_INFORMATION, nullptr, nullptr, &oldDacl, nullptr, &sd);
		if (r != ERROR_SUCCESS) {
			RemoveDirectoryW(path.c_str());
			return false;
		}

		TRUSTEE_W trustee{};
		trustee.TrusteeForm = TRUSTEE_IS_SID;
		trustee.TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP;
		trustee.ptstrName = reinterpret_cast<LPWSTR>(sidBuffer);

		EXPLICIT_ACCESS_W ea{};
		ea.grfAccessPermissions = FILE_GENERIC_READ | FILE_GENERIC_EXECUTE | FILE_GENERIC_WRITE;
		ea.grfAccessMode = GRANT_ACCESS;
		ea.grfInheritance = OBJECT_INHERIT_ACE | CONTAINER_INHERIT_ACE;
		ea.Trustee = trustee;

		PACL newDacl = nullptr;
		r = SetEntriesInAclW(1, &ea, oldDacl, &newDacl);

		if (r == ERROR_SUCCESS) {
			r = SetNamedSecurityInfoW((LPWSTR)path.c_str(), SE_FILE_OBJECT, DACL_SECURITY_INFORMATION, nullptr, nullptr, newDacl, nullptr);
		}

		if (newDacl) LocalFree(newDacl);
		if (sd) LocalFree(sd);

		if (r != ERROR_SUCCESS) {
			RemoveDirectoryW(path.c_str());
			return false;
		}

		return true;
	}

	bool DeleteDirectoryTree(const wstring& path) {
		wstring p;
		DirectoryEnum de(path);
		while (de.Next(&p)) {
			if (de.fd.dwFileAttributes & FILE_ATTRIBUTE_READONLY)
				SetFileAttributesW(S(p), de.fd.dwFileAttributes & ~FILE_ATTRIBUTE_READONLY);

			if (!de.IsDir())
				DeleteFileW(S(p));
			else if (de.IsLink())
				RemoveDirectoryW(S(p));
			else
				DeleteDirectoryTree(p);
		}

		DWORD a = GetFileAttributesW(S(path));
		if (a & FILE_ATTRIBUTE_READONLY)
			SetFileAttributesW(S(path), a & ~FILE_ATTRIBUTE_READONLY);

		return RemoveDirectoryW(S(path))
			|| !ExistsAsDir(path);
	}

	void DeleteDataDir(const wstring& dir, initializer_list<LPCWSTR> subdirNames) {
		for (auto sub : subdirNames) {
			util::DeleteDirectoryTree(str::ConcatPath(dir, sub));
		}
		RemoveDirectoryW(S(dir));
	}

#if false //unused now. Finished, tested etc.

	wstring GetEnv(LPCWSTR name) {
		DWORD n = GetEnvironmentVariableW(name, nullptr, 0);
		if (!n) return {};

		wstring r(n, {});
		n = GetEnvironmentVariableW(name, r.data(), n);

		if (!n) return {};
		r.resize(n);
		return r;
	}

	//#define RUNCONSOLE_LINES

	ExecResult _RunConsole(function<void(wstring_view)> onLine, wstring* output, wstring_view exe, wstring_view args, LPCWSTR curDir) {
		SECURITY_ATTRIBUTES sa{ sizeof(sa), nullptr, TRUE };
		Handle hRead, hWrite;
		if (!CreatePipe(&hRead, &hWrite, &sa, 0)) return {};

		SetHandleInformation(hRead, HANDLE_FLAG_INHERIT, 0);

		wstring cmd = str::Concat({ L"\"", exe, args.empty() ? L"\"" : L"\" ", args });

		STARTUPINFOW si{ sizeof(si) };
		si.dwFlags = STARTF_USESTDHANDLES;
		//si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
		si.hStdOutput = hWrite;
		si.hStdError = hWrite;

		PROCESS_INFORMATION pi{};

		if (!CreateProcessW(nullptr, cmd.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, curDir, &si, &pi)) {
			//Printf(L"failed: %i", GetLastError());
			return { false, GetLastError() == ERROR_FILE_NOT_FOUND || GetLastError() == ERROR_PATH_NOT_FOUND, 0 };
		}

		hWrite.Close();
		CloseHandle(pi.hThread);
		Handle hProcess(pi.hProcess);
		bool cancel = false;

		try {
			string store;
			char buf[10000];
			DWORD n;

			if (output) {
				while (ReadFile(hRead, buf, sizeof(buf), &n, nullptr) && n)
					store.append(buf, n);

				*output = str::Utf8ToUtf16(store);
			} else {
#ifdef RUNCONSOLE_LINES
				bool endedWithR = false;
				while (ReadFile(hRead, buf, sizeof(buf), &n, nullptr) && n) {
					size_t start = 0;

					if (endedWithR) { // Finish a previous '\r'. If this buffer starts with '\n', it is the second half of "\r\n".
						endedWithR = false;
						if (n && buf[0] == '\n') start = 1;
					}

					for (; start < n;) {
						size_t end = start;
						while (end < n && buf[end] != '\r' && buf[end] != '\n') end++;

						if (end == n) { // No terminator. Keep the incomplete line.
							store.append(buf + start, n - start);
							break;
						}

						if (store.empty()) { // The complete line is in buf.
							if (end > start)
								onLine(str::Utf8ToUtf16({ buf + start, end - start }));
							else
								onLine(L"");
						} else { // The line started in a previous buffer.
							store.append(buf + start, end - start);
							onLine(str::Utf8ToUtf16(store));
							store.clear();
						}

#pragma warning(suppress: 6385)
						if (buf[end] == '\r') {
							if (end + 1 == n) { // Need the next buffer to determine whether this is "\r" or the first half of "\r\n".
								endedWithR = true;
								break;
							}

							if (buf[end + 1] == '\n') end++;
						}

						start = end + 1;
					}
				}

				if (!store.empty()) // The output does not end with a line terminator.
					onLine(str::Utf8ToUtf16(store));
#endif
			}
		}
		catch (...) {
			cancel = true;
		}

		hRead.Close();

		if (cancel) {
			TerminateProcess(hProcess, 0);
			return {};
		}

		WaitForSingleObject(hProcess, INFINITE);

		DWORD exitCode = 0;
		GetExitCodeProcess(hProcess, &exitCode);

		return { true, false, (int)exitCode };
	}

	ExecResult RunConsole(wstring& output, wstring_view exe, wstring_view args, LPCWSTR curDir) {
		output.clear();
		return _RunConsole(nullptr, &output, exe, args, curDir);
	}

	ExecResult RunProcess(wstring_view exe, wstring_view args, LPCWSTR curDir) {
		wstring cmd = str::Concat({ L"\"", exe, args.empty() ? L"\"" : L"\" ", args });

		STARTUPINFOW si{ sizeof(si) };
		PROCESS_INFORMATION pi{};

		if (!CreateProcessW(nullptr, cmd.data(), nullptr, nullptr, 0, 0, nullptr, curDir, &si, &pi)) {
			return { false, GetLastError() == ERROR_FILE_NOT_FOUND || GetLastError() == ERROR_PATH_NOT_FOUND, 0 };
		}

		CloseHandle(pi.hThread);
		Handle hProcess(pi.hProcess);

		WaitForSingleObject(hProcess, INFINITE);

		DWORD exitCode = 0;
		GetExitCodeProcess(hProcess, &exitCode);

		return { true, false, (int)exitCode };
	}

#ifdef RUNCONSOLE_LINES
	ExecResult RunConsole2(function<void(wstring_view)> onLine, wstring_view exe, wstring_view args, LPCWSTR curDir) {
		return _RunConsole(onLine, nullptr, exe, args, curDir);
	}
#endif

#endif
}
