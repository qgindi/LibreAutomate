# LibreAutomate C#

LibreAutomate C# is a C# script editor/manager/launcher with an automation library and UI automation tools.

The library contains classes/functions to automate various tasks on Windows computers. Written mostly in C#, it is a .NET library. Some features: Send keys and text to the active window. Find and click buttons, links, images and other UI objects. Launch programs and auto-close windows. Process text and other data. Auto-replace text when typing. Create custom dialog windows. Hotkeys and other triggers.

In scripts also can be used Windows API, .NET and zillion of programming libraries, as well as code examples and info from the internet. The program can create programs and libraries, and not just for automation. You'll gradually learn C#, one of top 5 programming languages. Then creating automations with this code editor is faster than with no-code RPA apps, and less limitations.

More info and download: https://www.quickmacros.com/au/help/

Editor window

![window](https://www.quickmacros.com/au/help/images/window.png#1 "Editor window")

## How to build
Need Visual Studio 2022 with C#, C++, .NET 6.0 SDK and Windows 10 SDK.

1. Open Au.sln in Visual Studio. Ignore "failed to load project" errors.
2. Build solution (not just the startup project).
3. Switch to platform x86, build solution, switch back to AnyCPU.
4. Copy missing files (.db, .dll, etc) from the installed program folder to the output folder "_".
5. Run Au.Editor project. It should open the editor window.
