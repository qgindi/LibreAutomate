// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

#include <string>
#include <string_view>
//#include <format> //note: adds 220 K to the file size
#include <vector>
#include <memory>
#include <unordered_set>
#include <stdexcept>
#include <algorithm>
#include <functional>
#include <typeinfo>

// Windows Header Files

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
#include <windows.h>
#include <msi.h>
#include <msiquery.h>
#include <ShlObj.h>
#include <Aclapi.h>
#include <commctrl.h>
#include <winhttp.h>

#pragma comment(lib, "msi.lib")
#pragma comment(lib, "Advapi32.lib")
#pragma comment(lib, "Shell32.lib")
#pragma comment(lib, "Comctl32.lib")
#pragma comment(lib, "winhttp.lib")

using namespace std;

#endif //PCH_H
