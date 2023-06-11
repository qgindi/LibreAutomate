# LibreAutomate C#

C# script editor and automation library for Windows.

Some features of the automation library:
- Automate desktop and web UI using keys, mouse and API. Find and click buttons, links, images.
- Launch programs. Manage files and windows. Transfer and process text and other data.
- Hotkeys, autotext and other triggers. Auto-replace/expand text when typing. Auto-close windows. Remap keys.
- Custom toolbars that can be attached to windows or screen edges. And menus.
- Custom dialog windows of any complexity can be created easily in code.
- All classes/functions are documented.
- The library can be used in other programs too. Can be installed from [NuGet](https://www.nuget.org/packages/LibreAutomate).
- Uses .NET 6.

Some features of the script editor program:
- The scripting language is C#. The program is a good way to learn it.
- C# code editor with intellisense. Script manager, cookbook.
- Tools for recording keyboard/mouse and selecting UI objects such as buttons, links and images.
- Also you can use .NET and other libraries. Tools and databases for NuGet, Windows API, icons.
- Can create independent .exe programs and .NET libraries.

More info and download: https://www.libreautomate.com/

Editor window

![window](https://www.libreautomate.com/images/window.png#1 "Editor window")

## How to build
Need Visual Studio 2022 with C#, C++, .NET 6.0 SDK and Windows 10 or 11 SDK. Need Windows 10 or 11 64-bit; not tested on Win7.

1. Open Au.sln in Visual Studio.
2. Build solution (not just the startup project).
3. Switch to platform x86, build solution, switch back to AnyCPU.
4. Run Au.Editor project. It should open the editor window.
