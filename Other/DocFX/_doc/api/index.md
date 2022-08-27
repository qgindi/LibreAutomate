# Automation library

### Namespaces
- Au - main classes of this library, except triggers.
- Au.Types - types of function parameters, exceptions, etc.
- Au.Triggers - triggers: hotkeys, autotext, mouse, window.
- Au.More - classes that are rarely used in automation scripts.

### Files
##### .NET assembly files
- Au.dll - contains code of the above namespaces.

##### Native code dll files
- 64\AuCpp.dll - used by Au.dll in 64-bit processes.
- 32\AuCpp.dll - used by Au.dll in 32-bit processes.
- 64\sqlite3.dll - used by the **sqlite** class in 64-bit processes.
- 32\sqlite3.dll - used by the **sqlite** class in 32-bit processes.

These files are in the editor folder. The .exe compiler copies them to the .exe folder if need.

Other dll files in the editor folder are not part of the library. They are undocumented.

If Au.dll is used in a program other than the default editor program or .exe programs created by it and some library functions throw **DllNotFoundException** (missing AuCpp.dll etc), add environment variable *Au.Path* with value = Au.dll folder path.