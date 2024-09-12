Note: now the created db file is not used by LA directly. It is used by the new WinApiConverter to get declarations that are missing/incorrect in winmd.

1. Once per SDK version: run scripts "SDK get dll names" and "SDK get GUID". They create some files used by the converter. Don't need to run again after adding/removing header files.
2. Run script "SDK preprocessor". It creates two files for the converter project: Api-preprocessed-64.cpp, Api-preprocessed-32.cpp.
3. Run the converter project. It creates two files: Api-64.cs, Api-32.cs.
4. Run script "SDK append 32-bit diff". It creates final .cs file Api.cs.
5. Run script "SDK create database".

External tools:
1. dumpbin.exe from Visual Studio. Used by scripts "SDK get dll names" and "SDK get GUID".
2. clang.exe from LLVM (setup file LLVM-3.9.1-win64.exe). Used by script "SDK preprocessor".
