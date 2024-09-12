# LibreAutomate C#

C# script editor and automation library for Windows.

Some features of the automation library:
- Automate desktop and web UI using keys, mouse and API. Find and click buttons, links, images.
- Launch programs. Manage files and windows. Transfer and process text and other data.
- Hotkeys, autotext and other triggers. Auto-replace/expand text when typing. Auto-close windows. Remap keys.
- Custom toolbars that can be attached to windows or screen edges. And menus.
- Custom dialog windows of any complexity can be created easily in code.
- All classes/functions are documented.
- The library can be used in other programs too. [More info](https://www.libreautomate.com/api/index.html), [NuGet](https://www.nuget.org/packages/LibreAutomate).
- Uses .NET 8.

Some features of the script editor program:
- The scripting language is C#. The program is a good way to learn it.
- C# code editor with intellisense. Script manager, cookbook, debugger.
- Tools for recording keyboard/mouse and selecting UI objects such as buttons, links and images.
- Also you can use .NET and other libraries. Tools and databases for NuGet, Windows API, icons.
- Can create independent .exe programs and .NET libraries.

More info and download: https://www.libreautomate.com/

Editor window

![window](https://www.libreautomate.com/images/window.png#1 "Editor window")

## How to build
Need Visual Studio 2022 with C#, C++, .NET 8 SDK and Windows 10 or 11 SDK. Need 64-bit Windows 10 or 11.

1. Download and extract. Or git clone.
2. Open `Au.sln` in Visual Studio.
3. Build solution (not just the startup project).
4. Switch to platform x86, build solution, switch back to AnyCPU.
5. Run `Au.Editor` project. It should open the editor window.
