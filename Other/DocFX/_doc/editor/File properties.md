---
uid: file_properties
---

# C# file properties

C# file properties in LibreAutomate are similar to C# project properties in Visual Studio.
Most properties are stored in `/*/ meta comments /*/` at the start of code. Can be edited in the **Properties** window or in code.

Meta comments syntax:
- The block of meta comments starts and ends with `/*/`. Only the first such block is used.
- The block of meta comments must be at the start. Can be preceded by comments and empty lines.
- Property name and value are separated by space.
- Multiple properties separated by `;` or/and newlines.
- Property names must match case.
- Property values are not enclosed in quotes etc.
- There are no escape sequences.

Example - single line:
```csharp
/*/ role exeProgram; outputPath %folders.Workspace%\exe\Script1; /*/
```

Example - multiple lines:
```csharp
/*/
role exeProgram
outputPath %folders.Workspace%\exe\Script1
/*/
```

The compilation may contain multiple C# files (files in the project folder and files added using meta comment `c`). Properties are applied to the entire compilation, not only to the file where specified. Most properties can be specified only in the main file of the compilation (error if not); others can be anywhere (`c`, `r`, `pr`, `nuget`, `com`, `resource`, `file`). Most properties can be specified once (error if not); others can be multiple (`c`, `r`, `pr`, `nuget`, `com`, `resource`, `file`, `noRef`). Some properties are available only for some roles.

Terms used this article:
- *property* and *meta comment* are synonyms.
- *editor* and *LibreAutomate* are synonyms.
- *script file* - a C# file that can be executed directly (like a program).
- *class file* - a C# file that contains classes/functions that can be used in other files.
- *project folder* or *project* - folder named like `@Example` (starts with `@`). The first C# file (*main file*) and all class files are automatically added to the compilation.
- *compilation* - one or more C# files that are compiled together to create a program or library.

Properties in this article are grouped like in the **Properties** window.

## Table of contents

  - [Role (group of properties)](#role-group-of-properties)
    - [`role`](#role) - `miniProgram`, `exeProgram`, `editorExtension`, `classFile` or `classLibrary`.
  - [Run (group of properties)](#run-group-of-properties)
    - [`testScript`](#testscript) - a script to run to test this not-directly-executable class file.
    - [`ifRunning`](#ifrunning) - whether/how to launch this script if it's already running.
    - [`uac`](#uac) - run this script as admin or not.
  - [Compile (group of properties)](#compile-group-of-properties)
    - [`optimize`](#optimize) - release/debug configuration.
    - [`define`](#define) - preprocessor symbols.
    - [`warningLevel`](#warninglevel) - disable groups of warnings.
    - [`noWarnings`](#nowarnings) - disable specified warnings.
    - [`nullable`](#nullable) - C# nullable context.
    - [`testInternal`](#testinternal) - use internal members of specified assemblies.
    - [`preBuild`](#prebuild) - a script to run before compiling.
    - [`postBuild`](#postbuild) - a script to run after compiling.
  - [Assembly (group of properties)](#assembly-group-of-properties)
    - [`outputPath`](#outputpath) - output directory of compiled exe/dll files.
    - [`icon`](#icon) - icon of compiled exe file.
    - [`manifest`](#manifest) - manifest of compiled exe file.
    - [`sign`](#sign) - strong-name signing file.
    - [`console`](#console) - create console app.
    - [`platform`](#platform) - platform of compiled exe file (`x64`, `arm64` or `x86`).
    - [`xmlDoc`](#xmldoc) - whether to create an XML documentation file when compiling this library.
  - [Add reference (group of properties)](#add-reference-group-of-properties)
    - [`r` (button **Library**)](#r-button-library) - .NET assembly reference.
    - [`nuget` (button **NuGet**)](#nuget-button-nuget) - NuGet package reference.
    - [`com` (buttons **COM** and **...**)](#com-buttons-com-and-) - COM interop assembly reference.
    - [`pr` (button **Project**)](#pr-button-project) - library project reference.
  - [Add file (group of properties)](#add-file-group-of-properties)
    - [`c` (button **Class file**)](#c-button-class-file) - add a C# file to the compilation.
    - [`resource` (button **Resource**)](#resource-button-resource) - add a resource from a file.
    - [`file` (button **Other file**)](#file-button-other-file) - make a file available at run time.
  - [Rarely used properties](#rarely-used-properties)
    - [`miscFlags`](#miscflags) - miscellaneous rarely used flags.
    - [`noRef`](#noref) - remove a default reference assembly.
  - [Version info](#version-info) - how to add version info.

## Role (group of properties)

### `role`
The purpose of this C# code file. What kind of assembly to create and how to execute.
- `miniProgram` - execute in a separate host process started from editor.
- `exeProgram` - create/execute exe file, which can run on any computer, without editor installed.
- `editorExtension` - execute in the editor's UI thread. Rarely used. Incorrect code can kill editor.
- `classLibrary` - create dll file, which can be used in C# scripts and other .NET-based programs.
- `classFile` - don't create/execute. The C# code file can be compiled together with other C# code files in the project or using meta comment `c`. Inherits meta comments of the main file of the compilation.

Default role for scripts is `miniProgram`; cannot be the last two. Default for class files is `classFile`; can be any.

See also: [Scripts](xref:script), [Class files](xref:class_project), [Shared classes and functions, libraries](/cookbook/Shared classes and functions, libraries.html)

## Run (group of properties)

LibreAutomate uses these properties when launching the script task.

### `testScript`
A script to run when you click the **Run** button.

This property is for testing class files with role `classFile` or `classLibrary`. Such files cannot be executed directly.

The test script can contain meta comment `/*/ c this file.cs; /*/` that adds this file to the compilation, or `/*/ pr this file.cs; /*/` that adds the output dll file as a reference assembly. An easy way to add this property correctly is to try to run this file and click a link that is then printed in error text in the output.

The value can be:
- Path in the workspace. Examples: `\Script5.cs`, `\Folder\Script5.cs`.
- Path relative to this file. Examples: `Folder\Script5.cs`, `.\Script5.cs`, `..\Folder\Script5.cs`.
- Filename. Error if multiple exist, unless single exists in the same folder.

This property is saved in `files.xml`, not in meta comments.

### `ifRunning`
When trying to start this script, what to do if it is already running.
- `warn` - print warning and don't run.
- `cancel` - don't run.
- `wait` - run later, when it ends.
- `run` - run simultaneously.
- `restart` - end it and run.
- `end` - end it and don't run.

Suffix `_restart` (`warn_restart`, `cancel_restart`, `wait_restart`, `run_restart`, `end_restart`) changes the behavior: when using the **Run** button/menu to start the script, use `restart`; else use the value as without the suffix.

Default is `warn_restart`.

This property is ignored when the script runs as exe program started not from editor. Then it's an ordinary program (works like with `ifRunning run`). To allow single instance you can use [script.single](), like `script.single("unique string");`.

### `uac`
[UAC](xref:uac) integrity level (IL) of the task process.
- `inherit` (default) - the same as of the editor process (High IL recommended).
- `user` - Medium IL, like most applications. The task cannot automate high IL process windows, write some files, change some settings, etc.
- `admin` - High IL, aka "administrator", "elevated". The task has many rights, but cannot automate some apps through COM, etc.

This property is ignored when the script runs as exe program started not from editor. Then it's an ordinary program.

## Compile (group of properties)

LibreAutomate uses these properties when compiling the script or library.

### `optimize`
Whether to make the compiled code as fast as possible.
- `false` (default) - don't optimize. Define preprocessor symbols `DEBUG` and `TRACE` that can be used with `#if`. Aka "Debug configuration".
- `true` (checked) - optimize. Aka "Release configuration".

Default is `false`, because optimization makes difficult to debug. Optimization makes noticeably faster only some types of code, for example processing of large/many arrays. Before deploying class libraries and exe programs you usually compile with `optimize true`.

### `define`
Symbols that can be used with `#if`. Example: `ONE,TWO,d:THREE,r:FOUR`.

A symbol here can have a prefix:
- `r:` - define the symbol only if `optimize true` (checked).
- `d:` - define the symbol only if no `optimize true`.

If no `optimize true`, symbols `DEBUG` and `TRACE` are added implicitly.

See also [C# #define](ms:).

### `warningLevel`
[warning level](ms:C#+Compiler+Options,+WarningLevel). Default 8.
- 0 - no warnings.
- 1 - only severe warnings.
- 2 - level 1 plus some less-severe warnings.
- 3 - most warnings.
- 4 - all warnings of C# 1-8.
- 5-9999 - level 4 plus warnings added in C# 9+.

### `noWarnings`
Don't show these warnings. Example: `151,3001,CS1234`.

See also [#pragma warning](ms:C#+#pragma+warning).

### `nullable`
[nullable context](ms:C#+Nullable+reference+types).
- `disable` - no warnings; code does not use nullable syntax (`Type? variable`).
- `enable` - print warnings; code uses nullable syntax.
- `warnings` - print warnings; code does not use nullable syntax.
- `annotations` - no warnings; code uses nullable syntax.

Alternatively use `#nullable` directive.

### `testInternal`
Can use internal symbols of these assemblies, like with `InternalsVisibleToAttribute`.
Example: `Assembly1,Assembly2`.

### `preBuild`
A script to run before compiling.

The script must have role `editorExtension`. It runs in the editor's main thread.  
To get compilation info: `var c = PrePostBuild.Info;`  
To specify command line arguments: `/*/ preBuild Script5.cs /arguments; /*/`  
To stop the compilation, let the script throw an exception.  
To create new preBuild/postBuild script: menu **File > New > More**.

The value can be:
- Path in the workspace. Examples: `\Script5.cs`, `\Folder\Script5.cs`.
- Path relative to this file. Examples: `Folder\Script5.cs`, `.\Script5.cs`, `..\Folder\Script5.cs`.
- Filename. Error if multiple exist, unless single exists in the same folder.

### `postBuild`
A script to run after successfully compiling and creating output files.

Everything is like with `preBuild`.

## Assembly (group of properties)

LibreAutomate uses these properties when creating program files after compiling the script/library code.

### `outputPath`
Directory for the output assembly file and related files (used dlls, etc).

Full path. Can start with `%environmentVariable%` or `%folders.SomeFolder%` (see class [folders]()). Can be path relative to this file or workspace, like with other properties.

Default if role `exeProgram`: `%folders.Workspace%\exe\filename`. Default if role `classLibrary`: `%folders.Workspace%\dll`. The compiler creates the folder if does not exist.

### `icon`
Icon of the output exe file.

The icon will be added as a native resource and displayed in File Explorer etc. Native resources can be used with [icon.ofThisApp]() etc and [dialog]() class functions.

The file must be in this workspace. Import files if need (eg drag-drop). Can be a link.
The value can be:
- Path in the workspace. Examples: `\App.ico`, `\Folder\App.ico`.
- Path relative to this file. Examples: `Folder\App.ico`, `.\App.ico`, `..\Folder\App.ico`.
- Filename. Error if multiple exist, unless single exists in the same folder.

If role `exeProgram`, you can specify a folder instead. Will add all `.ico` and `.xaml` icons from it. Resource ids start from `IDI_APPLICATION` (32512).

Another way to set exe icon - assign a custom icon to the main C# file. See menu **Tools > Icons**.

### `manifest`
[manifest file](ms:) of the output exe file.

The file must be in this workspace. Import files if need (eg drag-drop). Can be a link.
The value can be:
- Path in the workspace. Examples: `\App.manifest`, `\Folder\App.manifest`.
- Path relative to this file. Examples: `Folder\App.manifest`, `.\App.manifest`, `..\Folder\App.manifest`.
- Filename. Error if multiple exist, unless single exists in the same folder.

The manifest will be added as a native resource.

### `sign`
Strong-name signing key file, to sign the output assembly.

The file must be in this workspace. Import files if need (eg drag-drop). Can be a link.
The value can be:
- Path in the workspace. Examples: `\App.snk`, `\Folder\App.snk`.
- Path relative to this file. Examples: `Folder\App.snk`, `.\App.snk`, `..\Folder\App.snk`.
- Filename. Error if multiple exist, unless single exists in the same folder.

### `console`
Let the program run with console.

### `platform`
CPU instruction set.
- `x64` - runs on all modern Windows computers (x64, Windows11+ ARM64).
- `arm64` - runs only on Windows ARM64. Used because x64 and x86 programs are slow there.
- `x86` - runs on almost all Windows computers (x64, x86, all ARM64), as 32-bit process.

Default - as LibreAutomate and the Windows OS on this computer (x64 or arm64).

Creates program files for this platform. If `optimize true` and `platform` not `x86`, creates for both x64 and arm64. In any case, the process uses this platform when launched from editor.

Most .NET dlls can be used by programs of any platform. But native dlls can be used only in programs of the same platform as of the dll. Usually libraries that use native dlls have dll files for multiple platforms. If a dll for some platform is missing, you can't use that platform for your script/program that will use that library. Workaround: use role `exeProgram` and platform of an available dll.

### `xmlDoc`
Create XML documentation file from `///` comments. And print errors in `///` comments when compiling.

XML documentation files are used by code editors to display class/function/parameter info. Also can be used to create HTML documentation.

## Add reference (group of properties)

This is a group of buttons in the **Properties** window. The buttons add meta comments for adding library references to the compilation.

### `r` (button **Library**)
Add a .NET assembly reference.
Meta comment `/*/ r File.dll; /*/`.

Don't need to add `Au.dll` and .NET runtime dlls.  
To remove this meta comment, edit the code in the code editor.  
To use `extern alias Abc;`, edit the code: `/*/ r DllFile /alias=Abc; /*/`  
If script role is `editorExtension`, may need to restart editor.

### `nuget` (button **NuGet**)
Use a NuGet package reference (see menu **Tools > NuGet**).
Meta comment `/*/ nuget Folder\Package; /*/`.

To remove this meta comment, edit the code in the code editor.  
To use `extern alias Abc;`, edit the code: `nuget Folder\Package /alias=Abc`

### `com` (buttons **COM** and **...**)
Use a COM interop assembly.
Meta comment `/*/ com File.dll; /*/`.

In the **Properties** window select a COM component. It immediately converts the type library to a COM interop assembly and saves the assembly file in `%folders.Workspace%\.interop`.

An interop assembly is a .NET assembly without real code. Not used at run time. At run time is used the COM component (registered unmanaged dll or exe file). If a COM dll for current platform unavailable, try to set role `exeProgram` and change platform.

To remove this meta comment, edit the code. Optionally delete unused interop assemblies.  
To use `extern alias Abc;`, edit the code: `/*/ com File.dll /alias=Abc; /*/`

### `pr` (button **Project**)
Add a reference to a class library created in this workspace.
Meta comment `/*/ pr File.cs; /*/`.

The compiler will compile it if need and use the created dll file as a reference.

To remove this meta comment, edit the code. Optionally delete unused dll files.

## Add file (group of properties)

This is a group of buttons in the **Properties** window. The buttons add meta comments for adding various files to the compilation.

### `c` (button **Class file**)
Add a C# code file that contains some classes/functions used by this file.
Meta comment `/*/ c File.cs; /*/`.

The compiler will compile all code files and create single assembly.

The file must be in this workspace. Import files if need (eg drag-drop). Can be a link.  
If folder, will compile all its descendant class files.  
The value can be:
- Path in the workspace. Examples: `\Class5.cs`, `\Folder\Class5.cs`.
- Path relative to this file. Examples: `Folder\Class5.cs`, `.\Class5.cs`, `..\Folder\Class5.cs`.
- Filename. Error if multiple exist, unless single exists in the same folder.

Don't use this meta comment to add class files that are in the same project folder. They are added automatically.

To remove this meta comment, edit the code.

### `resource` (button **Resource**)
Add image etc file(s) as managed resources.
Meta comment `/*/ resource File; /*/`.

Default resource type is `Stream`. You can append `/byte[]` or `/string`, like `/*/ resource file.txt /string; /*/`. Or `/strings`, to add multiple strings from a 2-column CSV file (name, value). Or `/embedded`, to add as a separate top-level stream that can be loaded with [Assembly.GetManifestResourceStream](ms:) (others are in top-level stream `AssemblyName.g.resources`).

The file must be in this workspace. Import files if need (eg drag-drop). Can be a link.  
If folder, will add all its descendant files.  
The value can be:
- Path in the workspace. Examples: `\File.png`, `\Folder\File.png`.  
- Path relative to this file. Examples: `Folder\File.png`, `.\File.png`, `..\Folder\File.png`.  
- Filename. Error if multiple exist, unless single exists in the same folder.

To remove this meta comment, edit the code.

To load resources at run time can be used class [ResourceUtil](), like `var s = ResourceUtil.GetString("file.txt");`. Or [ResourceManager](ms:). To load WPF resources can be used `"pack:..."` URI; if role `miniProgram`, assembly name is like `*ScriptName`.

Resource names in assembly by default are like `file.png`. When adding a folder with subfolders, may be path relative to that folder, like `subfolder/file.png`. If need path relative to this C# file, append space and `/path`. Resource names are lowercase, except `/embedded` and `/strings`. LibreAutomate does not URL-encode resource names; WPF `pack:...` URI does not work if resource name contains spaces, non-ASCII or other URL-unsafe characters. Also LibreAutomate does not convert XAML to BAML.

To browse .NET assembly resources, types, etc can be used for example [ILSpy](google:).

### `file` (button **Other file**)
Make a file available at run time.
Meta comment `/*/ file File; /*/`.

It can be any file, for example an unmanaged dll.

The compiler behavior depends on the role of the main file of the compilation:
- `exeProgram` - copy the file to the output folder. Or subfolder: `/*/ file file.dll /sub; /*/`.
- `miniProgram` or `editorExtension` - store the dll path in the assembly in order to find it at run time.
- `classLibrary` - the above actions are used when compiling scripts that use it as a project reference. The compiler does not copy these files to the output folder of the library.

The file must be in this workspace. Import files if need (eg drag-drop). Can be a link.  
The value can be:
- Path in the workspace. Examples: `\File.png`, `\Folder\File.png`.
- Path relative to this file. Examples: `Folder\File.png`, `.\File.png`, `..\Folder\File.png`.
- Filename. Error if multiple exist, unless single exists in the same folder.

If folder, will include all its descendant files. Will copy them into folders like in the workspace. If a folder name ends with `-`, will copy its contents only (will not create the folder).

If an `exeProgram` script uses unmanaged dll files, consider placing them in subfolders named `64`, `64\ARM` and `32`. Then at run time will be loaded the dll for that platform.

To remove this meta comment, edit the code.

## Rarely used properties

### `miscFlags`
Miscellaneous flags. May be set by some controls of the **Properties** window.

- 1 - don't add XAML icons from code strings like `"*name color"` to exe resources.

### `noRef`
Remove a default reference assembly (.NET or `Au.dll`).
Meta comment: `/*/ noRef AssemblyNameOrAuPathWildex; /*/`.

Can be specified only in code, not in the **Properties** window.

## **Version info**
To add version info to your exe program, insert and edit this code near the start of any C# file of the compilation.

```
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]
//[assembly: AssemblyFileVersion("1.0.0.0")] //if missing, uses AssemblyVersion
//[assembly: AssemblyTitle("File description")]
//[assembly: AssemblyDescription("Comments")]
//[assembly: AssemblyCompany("Company name")]
//[assembly: AssemblyProduct("Product name")]
//[assembly: AssemblyInformationalVersion("1.0.0.0")] //product version
//[assembly: AssemblyCopyright("Copyright © {{DateTime.Now.Year}} ")]
//[assembly: AssemblyTrademark("Legal trademarks")]
```
