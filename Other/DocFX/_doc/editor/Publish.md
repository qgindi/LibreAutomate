---
uid: publish
---

# Creating exe programs
Two ways of creating programs that can run without LibreAutomate:
- Script role `exeProgram`.
- Menu **Run > Publish**.

## Script role exeProgram
In **Properties** set role `exeProgram`. When compiling the script, LibreAutomate creates/copies program files (exe, dll) to the output directory and prints a link to it. To compile, click the **Compile** button or run the script.

The program consists of several files. To add them to a single executable file you can create an installer, for example with Inno Setup.

The program uses .NET Runtime of version displayed in menu **Help > About**. On computers without .NET it can't run; it prompts to download .NET.

If `optimize true` (checked **optimize** in **Properties**), the program is created with optimized code (fast and small), aka "Release".

Creates program files for the selected platform. If `optimize true` and not `platform x86`, also creates program files for other 64-bit platform. If `platform x86`, always creates only x86 files. More info below in the **Common info** section.

## Menu Run > Publish
The **Publish** tool can create single-file programs and programs that can run on computers without installed .NET.

It creates a temporary .csproj file and executes [dotnet publish](https://www.google.com/search?q=dotnet+publish), which uses .NET SDK to compile the script.

Options:
- **Single file** - create single-file program. Adds all program files to single exe file.
- **Single file > Self extract** - read below.
- **Add .NET Runtime** - add .NET Runtime files too. Then the program can run on computers without installed .NET. If unchecked, on computers without .NET the program just prompts to install .NET. A single-file exe is compressed, ~70 MB.
- **ReadyToRun** - compile to native code.
- **Platform** - CPU instruction set. You can publish for multiple platforms (run the publish tool several times). Files of each platform are created in a separate folder. More info below in the **Common info** section.

Always compiles the program and its `/*/ pr /*/` libraries like with `/*/ optimize true; /*/` (aka "Release").

Role can be `exeProgram` or `miniProgram`.

Unsupported features:
- `/*/ icon folder /*/` (multiple native icons).
- `/*/ testInternal, noRef /*/`.

### Self-extract

This 3-state checkbox is enabled when **Single file** is checked.
- Checked - use `IncludeAllContentForSelfExtract`. Adds all files to exe. Will extract all (including .NET dlls) to a temporary directory. The program will start slower, but will not have issues like unavailable [Assembly.Location](ms:).
- Unchecked - adds only .NET dlls to exe. Native dlls and other files (if any) will live in the exe's directory. Will use .NET dlls without extracting.
- Indeterminate - use `IncludeNativeLibrariesForSelfExtract`. Adds all dlls to exe. Will extract native dlls, and use .NET dlls without extracting. This is the best for most scripts, but can't be used with some scripts (the **Publish** tool will print a warning). 

[More info](https://www.google.com/search?q=dotnet+publish+single-file+IncludeAllContentForSelfExtract+IncludeNativeLibrariesForSelfExtract)

## Common info

Where the program can run, depends on the selected platform:
- x64 - runs on all modern Windows computers (x64, Windows11+ ARM64).
- arm64 - runs only on Windows ARM64. Used because x64 and x86 programs are slow there.
- x86 - runs on almost all Windows computers (x64, x86, all ARM64), as 32-bit process.

These script properties are not applied to exe programs launched not from LibreAutomate:
- `ifRunning`. Multiple instances (processes) of the program can run simultaneously. To prevent it can be used [script.single]().
- `uac`. By default programs run not as administrator (unlike when launched from admin LibreAutomate) and therefore can't automate admin windows etc. See [UAC](xref:uac).

Program files don't contain the source code, but can be decompiled into equivalent source code.

[print.it]() text is displayed in LibreAutomate if it is running, unless it's a console program. See also [PrintServer]().

If you want to use action triggers (hotkeys etc) in exe program, add them to the script like in the [ActionTriggers]() example.

To get program/OS/computer info can be used classes [script](), [process](), [uacInfo](), [osVersion](), [folders](), [Environment](ms:), [RuntimeInformation](ms:), [OperatingSystem](ms:).

Antivirus programs and OS may block or block-scan-restart unknown (new) program files. To avoid it on the development computer, in the AV program add the output directory to the list of exclusions. To avoid it anywhere, need to sign program files with a code signing certificate, and it must have a good reputation; it isn't cheap and isn't easy to get.

## See also
- [Scripts](xref:script)
- [File properties](xref:file_properties)


