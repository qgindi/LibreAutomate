#define WIN32_LEAN_AND_MEAN
#include <windows.h>

//#define EXPORT extern "C" __declspec(dllimport) //C++
#define EXPORT __declspec(dllimport) //C
EXPORT void Cpp_Arch(LPCWSTR a0, LPCWSTR a1);
EXPORT void Cpp_Unload(DWORD flags);

#if 1 //small exe file. Use .c file. Project properties > Linker > Advanced > Entry point = main.
void main() {
	LPCWSTR a = GetCommandLine();
	if (*a == '"') {
		while (*(++a) != '"') if (*a == 0) return;
		a++;
	} else {
		while (*a != ' ' && *a != 0) a++;
	}
	while (*a == ' ') a++;
	//MessageBox(0, a, L"test", 0);

	if (*a == 0) {
		Cpp_Unload(1);
	} else {
		LPCWSTR a1 = a; while (*a1 != 0 && *a1 != ' ') a1++;
		if (a1 > a && *a1 == ' ') a1++; else return;
		Cpp_Arch(a, a1);
	}

	ExitProcess(0);
}
#else
int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR pCmdLine, int nCmdShow) {

	//MessageBox(0, pCmdLine, L"test", 0);

	if (*pCmdLine == 0) {
		Cpp_Unload(1);
	} else {
		LPCWSTR a1 = pCmdLine; while (*a1 != 0 && *a1 != ' ') a1++;
		if (a1 > pCmdLine && *a1 == ' ') a1++; else return 1;
		Cpp_Arch(pCmdLine, a1);
	}

	return 0;
}
#endif
