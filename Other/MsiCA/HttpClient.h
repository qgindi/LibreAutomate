#pragma once
#include "pch.h"

#if false //unused now. Finished, tested etc.

#include "util.h"

class Hinternet {
	HINTERNET _h{};
public:
	Hinternet() = default;
	NONCOPYABLE(Hinternet);

	explicit Hinternet(HINTERNET h) : _h(h) {}

	~Hinternet() {
		if (_h) WinHttpCloseHandle(_h);
	}

	void operator=(HINTERNET h) {
		if (_h) WinHttpCloseHandle(_h);
		_h = h;
	}

	operator HINTERNET() const { return _h; }

	void Close() {
		if (_h) {
			WinHttpCloseHandle(_h);
			_h = {};
		}
	}
};

struct Progress {
	__int64 downloaded, total;
};

//HTTP connection.
//REMARKS
//Supports single HTTP server at a time. When calling a function with an `url` parameter, disconnects from previous HTTP server if the URL is to another server.
//Exception if url does not start with "https://".
class HttpClient {
	Hinternet _hSession, _hConnect;
	wstring _server;
public:
	HttpClient() = default;
	NONCOPYABLE(HttpClient);

	LPCWSTR _InitConnection(wstring_view url) {
		if (!url.starts_with(L"https://")) throw invalid_argument("invalid URL");
		url = url.substr(8);
		size_t i = 0; while (i < url.size() && url[i] != L'/') i++;
		wstring_view server = url.substr(0, i);

		if (!_hSession) {
			DWORD flags1 = util::IsWin81OrNewer() ? WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY : WINHTTP_ACCESS_TYPE_DEFAULT_PROXY;
			_hSession = WinHttpOpen(L"WinHTTP", flags1, nullptr, nullptr, 0);
		}

		if (!_hConnect || server != _server) {
			_server = server;
			_hConnect = WinHttpConnect(_hSession, S(_server), INTERNET_DEFAULT_HTTPS_PORT, 0);
		}

		return url.data() + i + (url.size() > i);
	}

	bool _Get(wstring_view url, LPCWSTR file, wstring* data, function<bool(Progress progress)> progress) {
		if (data) data->clear();

		auto uriPath = _InitConnection(url);

		Hinternet hRequest(WinHttpOpenRequest(_hConnect, L"GET", uriPath, nullptr, nullptr, nullptr, WINHTTP_FLAG_SECURE));
		if (!hRequest) return false;

		if (!WinHttpSendRequest(hRequest, nullptr, 0, nullptr, 0, 0, 0)) return false;
		if (!WinHttpReceiveResponse(hRequest, nullptr)) return false;

		DWORD contentLength = 0, downloadedTotal = 0;
		DWORD size1 = 4;
		WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CONTENT_LENGTH | WINHTTP_QUERY_FLAG_NUMBER, nullptr, &contentLength, &size1, nullptr);
		//never mind: supports max 4 GB. To support any size, need to get and parse string. We will not download files > 1 GB.

		vector<char> buffer(1024 * 100);

		if (data) {
			string utf8;
			if (contentLength > 0) utf8.reserve(min(contentLength, 10'000'000));
			for (;;) {
				DWORD size = 0;
				if (!WinHttpReadData(hRequest, buffer.data(), buffer.size(), &size)) return false;
				if (size == 0) break;
				utf8.append(buffer.data(), size);
			}
			*data = str::Utf8ToUtf16(utf8);
		} else {
			Handle hFile(CreateFileW(file, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr));
			if (!hFile) return false;

			auto prevTime = GetTickCount64();
			if (progress && !progress({ downloadedTotal, contentLength })) return false;
			bool progressCalled = false;

			for (;;) {
				DWORD size = 0;
				if (!WinHttpReadData(hRequest, buffer.data(), buffer.size(), &size)) return false;
				if (size == 0) break;

				DWORD written = 0;
				if (!WriteFile(hFile, buffer.data(), size, &written, nullptr) || written != size) return false;

				if (progress) {
					downloadedTotal += size;
					auto time = GetTickCount64();
					if (progressCalled = time - prevTime > 40) {
						prevTime = time;
						if (!progress({ downloadedTotal, contentLength })) return false;
					}
				}
			}

			if (progress && !progressCalled && !progress({ downloadedTotal, contentLength })) return false;
		}

		return true;
	}

	//HTTP GET url -> `wstring`.
	//Assumes UTF-8.
	bool Get(wstring_view url, wstring& data) {
		return _Get(url, {}, &data, {});
	}

	//HTTP GET url -> file, with optional progress callback.
	//Downloads to a temporary file and then renames-replaces.
	bool Download(wstring_view url, const wstring& file, function<bool(Progress progress)> progress = {}) {
		wstring tempFile = file + L".~part";
		if (_Get(url, S(tempFile), nullptr, progress)) {
			if (util::MoveFile(tempFile, file)) return true;
		}
		DeleteFileW(S(tempFile));
		return false;
	}

	//Closes the connection handle.
	//Don't need to call this when ended using the variable or before reusing it for another HTTP server; then auto-closes the handle.
	void Disconnect() {
		_hConnect = {};
		_server.clear();
	}
};

#endif
