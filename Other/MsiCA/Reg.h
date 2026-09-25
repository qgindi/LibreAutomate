#pragma once
#include "pch.h"

class Reg {
	HKEY _h{};
public:
	Reg() = default;
	NONCOPYABLE(Reg);

	~Reg() {
		Close();
	}

	operator HKEY() const { return _h; }

	bool Open(HKEY root, LPCWSTR subkey, bool writable) {
		Close();
		REGSAM access = (writable ? KEY_WRITE : 0) | KEY_READ | KEY_WOW64_64KEY;
		return RegOpenKeyEx(root, subkey, 0, access, &_h) == ERROR_SUCCESS;
	}

	bool Create(HKEY root, LPCWSTR subkey) {
		Close();
		REGSAM access = KEY_READ | KEY_WRITE | KEY_WOW64_64KEY;
		return RegCreateKeyEx(root, subkey, 0, nullptr, 0, access, nullptr, &_h, nullptr) == ERROR_SUCCESS;
	}

	void Close() {
		if (_h) {
			RegCloseKey(_h);
			_h = nullptr;
		}
	}

	bool GetString(LPCWSTR name, wstring& value) const {
		if (!_h) return false;

		DWORD type, size = 0;
		LONG r = RegQueryValueEx(_h, name, nullptr, &type, nullptr, &size);
		if (r != ERROR_SUCCESS ||
			(type != REG_SZ && type != REG_EXPAND_SZ))
			return false;

		vector<wchar_t> buf(size / sizeof(wchar_t) + 1);

		r = RegQueryValueEx(
			_h, name, nullptr, nullptr,
			reinterpret_cast<BYTE*>(buf.data()), &size);

		if (r != ERROR_SUCCESS)
			return false;

		value.assign(buf.data());
		return true;
	}

	bool SetString(LPCWSTR name, const wstring& value) {
		if (!_h) return false;

		DWORD size = (DWORD)((value.size() + 1) * sizeof(wchar_t));

		return RegSetValueEx(
			_h, name, 0, REG_SZ,
			reinterpret_cast<const BYTE*>(value.c_str()),
			size) == ERROR_SUCCESS;
	}

	bool GetDword(LPCWSTR name, DWORD& value) const {
		if (!_h) return false;

		DWORD type = 0;
		DWORD size = sizeof(value);

		return RegQueryValueEx(
			_h, name, nullptr, &type,
			reinterpret_cast<BYTE*>(&value),
			&size) == ERROR_SUCCESS
			&& type == REG_DWORD
			&& size == sizeof(value);
	}

	bool SetDword(LPCWSTR name, DWORD value) {
		if (!_h) return false;

		return RegSetValueEx(
			_h, name, 0, REG_DWORD,
			reinterpret_cast<const BYTE*>(&value),
			sizeof(value)) == ERROR_SUCCESS;
	}

	bool DeleteValue(LPCWSTR name) {
		if (!_h) return false;

		return RegDeleteValue(_h, name) == ERROR_SUCCESS;
	}

	bool DeleteSubkey(LPCWSTR name) {
		if (!_h) return false;

		return RegDeleteTree(_h, name) == ERROR_SUCCESS;
	}

#if !true //not used
	vector<wstring> Subkeys() const {
		vector<wstring> a;
		if (!_h) return a;

		DWORD count = 0;
		DWORD maxLen = 0;

		if (RegQueryInfoKey(
			_h, nullptr, nullptr, nullptr,
			&count, &maxLen, nullptr, nullptr,
			nullptr, nullptr, nullptr, nullptr) != ERROR_SUCCESS)
			return a;

		vector<wchar_t> buf(maxLen + 1);

		for (DWORD i = 0; i < count; i++) {
			DWORD len = maxLen + 1;

			LONG r = RegEnumKeyEx(
				_h, i,
				buf.data(), &len,
				nullptr, nullptr, nullptr, nullptr);

			if (r == ERROR_SUCCESS)
				a.emplace_back(buf.data(), len);
		}

		return a;
	}
#endif
};