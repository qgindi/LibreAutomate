# Automation library

### Namespaces
- `Au` - main classes of this library, except triggers.
- `Au.Types` - types of function parameters, exceptions, etc.
- `Au.Triggers` - triggers: hotkeys, autotext, mouse, window.
- `Au.More` - classes that are rarely used in automation scripts.

### Files
#### .NET assembly files
- `Au.dll` - contains code of the above namespaces.

#### Native code files
- `AuCpp.dll`, `Au.DllHost.exe` - used by `Au.dll`.

These files are in LibreAutomate subfolders `64` and `32`. The exe compiler copies them to the exe folder. When using the library via NuGet, they are in subfolder `runtimes`.

Other dll files in the LibreAutomate folder are not part of the library. They are undocumented.

### Using the library without LibreAutomate
To get the dlls use NuGet package [LibreAutomate](https://www.nuget.org/packages/LibreAutomate). Or copy from the LibreAutomate folder. Or build from source code.

Your project settings:
- Target OS = Windows.
- Supported OS version >= 7.0.
- Use a manifest like [this](https://github.com/qgindi/LibreAutomate/blob/master/_/default.exe.manifest). It enables common controls 6, all OS versions, full DPI awareness, `disableWindowFiltering`.

If not using NuGet, add the native code files to the project. Add them as links, and in dll **Properties** set **Content** and **Copy if newer**.

If some library functions throw `DllNotFoundException` (missing `AuCpp.dll` etc), add environment variable `Au.Path` with value = `Au.dll` folder path. May need this when the host program copies `Au.dll` somewhere without the native dll folders, for example in some scripting environments and GUI designers.

See also: [unloading AuCpp.dll from other processes](https://www.libreautomate.com/forum/showthread.php?tid=7557)
