## Projects

#### Au.Editor
The C# script editor/manager app.

#### Au.Controls
This library contains custom WPF control classes used by `Au.Editor`.

#### Au
The automation library.  
The library contains not just classes for automation scripts, but also many classes that are useful in any C# project. Used by all C# projects of this solution, where possible.

#### Cpp
This unmanaged C++ dll is part of the automation library.  
Contains mostly code to find UI elements. To make it much faster, the dll normally is loaded into the target process; that is why C++.

#### PCRE
This static library is a modified copy of the [PCRE](https://www.pcre.org/) regular expression library.

#### Scintilla
This library is a modified copy of the [Scintilla](https://www.scintilla.org) code editor control.

#### Au.DllHost
This tiny C++ program is used by the `Cpp` project to load the dll in processes of a different CPU architecture.

#### Au.Net4
This .NET 4.8 console app is a bridge to .NET Framework libraries that are unavailable in modern .NET.

#### BuildEvents
This app is used as a pre/post-build event script when building other projects. Not used at run time.

#### DatabasesEtc
This app creates some databases used by LibreAutomate.
Used when changing the used .NET major version. Not used at run time.

#### MsiCA
This unmanaged C++ dll contains MSI custom actions used by the installer (`LibreAutomate.msi`).  
File `msi.json` contains info to build the installer with [Master Packager Dev](https://www.masterpackager.com/).

#### DocFX
This project contains input files for the [DocFX](https://github.com/dotnet/docfx) documentation site generator.  
Don't build this project in Visual Studio. Instead install DocFX and run script `Au docs.cs`.  
Part of LibreAutomate documentation sources is here, in folders `editor`, `articles` and `changes`. Library documentation sources are XML documentation comments in the `Au` project.

#### Cookbook
This folder contains sources of the cookbook. It's not a VS project. It's a LibreAutomate workspace. Script `Au docs.cs` converts the C# files to markdown and appends to the DocFX input.

## Dependencies

`Au.Editor`
- `Au`
- `Au.Controls`
- `Scintilla`

`Au.Controls`
- `Au`

`Au`
- `Cpp`

`Cpp`
- `PCRE`
- `BuildEvents`

`Scintilla`
- `BuildEvents`

`Au.DllHost`
- `Cpp`

`BuildEvents`
- `Au` from NuGet. Not a project reference because `BuildEvents` must be built before `Au`.

`DatabasesEtc`
- `Au`

Other projects don't have project references.

## Projects in other GitHub repositories

LibreAutomate also uses program files produced by projects of solutions that are in other GitHub repositories in https://github.com/qgindi.

#### Roslyn

https://github.com/qgindi/roslyn (a fork of https://github.com/dotnet/roslyn).  
C# compiler/analyzer and intellisense features.  

#### netcoredbg

https://github.com/qgindi/netcoredbg (a fork of https://github.com/Samsung/netcoredbg).  
.NET debugger.
