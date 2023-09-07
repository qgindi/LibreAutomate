# Automation library

### Namespaces
- Au - main classes of this library, except triggers.
- Au.Types - types of function parameters, exceptions, etc.
- Au.Triggers - triggers: hotkeys, autotext, mouse, window.
- Au.More - classes that are rarely used in automation scripts.

### Files
#### .NET assembly files
- Au.dll - contains code of the above namespaces.

#### Native code dll files
- 64\AuCpp.dll - used by Au.dll in 64-bit processes.
- 32\AuCpp.dll - used by Au.dll in 32-bit processes.
- 64\sqlite3.dll - used by the **sqlite** class in 64-bit processes.
- 32\sqlite3.dll - used by the **sqlite** class in 32-bit processes.

These files are in the editor folder. The .exe compiler copies them to the .exe folder if need.

Other dll files in the editor folder are not part of the library. They are undocumented.

### Using in programs other than LibreAutomate and .exe programs created by it
To get the dlls you can use NuGet package [LibreAutomate](https://www.nuget.org/packages/LibreAutomate). Or copy from the LibreAutomate folder. Or build from source code.

The program should use a manifest like [this](https://github.com/qgindi/LibreAutomate/blob/master/_/default.exe.manifest). It enables common controls 6, all OS versions, full DPI awareness, disableWindowFiltering.

If some library functions throw **DllNotFoundException** (missing AuCpp.dll etc), add environment variable *Au.Path* with value = Au.dll folder path. May need this when the host program copies Au.dll somewhere without the native dll folders, for example in some scripting environments and GUI designers.
