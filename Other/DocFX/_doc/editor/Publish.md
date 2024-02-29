---
uid: publish
---

# Creating .exe programs
Two ways of creating programs that can run without LibreAutomate:
- Script role **exeProgram**.
- Menu **Run > Publish**.

## Script role exeProgram
In **Properties** set role **exeProgram**. When compiling the script, LibreAutomate creates/copies program files (.exe, .dll) to the output directory and prints a link to it. To compile, click the **Compile** button or run the script.

The program consists of several files. To add them to a single file you can use the **Publish** tool, or create an installer with Inno Setup etc, or add to a .zip file.

The program uses .NET Runtime of version displayed in menu **Help > About**. On computers without .NET it can't run; it prompts to download .NET.

If in **Properties** is checked **optimize**, the program is created with optimized code (fast and small), aka "Release". Also the compiler creates 64-bit and 32-bit program files. Else creates only 64-bit files. If checked **bit32**, always creates only 32-bit files.

## Menu Run > Publish
The **Publish** tool can create single-file programs and programs that can run on computers without installed .NET.

It creates a temporary .csproj file and executes [dotnet publish](https://www.google.com/search?q=dotnet+publish), which uses .NET SDK to compile the script. Error if SDK not installed.

Options:
- **Single file** - create single-file program. Adds all program files to single .exe file.
- **Add .NET Runtime** - add .NET Runtime files too. Then the program can run on computers without installed .NET. If unchecked, on computers without .NET the program just prompts to install .NET. Single-file .exe is compressed, ~70 MB.
- **ReadyToRun** - compile to native code.

By default the program can run only on 64-bit Windows. It's not a problem, because 32-bit Windows is already extinct. With `/*/ bit32 true; /*/` the program is 32-bit and can run on 32-bit Windows too.

Always compiles the program and its `/*/ pr /*/` libraries like with `/*/ optimize true; /*/` (aka "Release").

Role can be **exeProgram** or **miniProgram**.

Unsupported features:
- `/*/ icon folder /*/` (multiple native icons).
- `/*/ testInternal, noRef /*/`.

## Common info

These script properties are not applied to .exe programs launched not from LibreAutomate:
- **ifRunning**. Multiple instances (processes) of the program can run simultaneously. To prevent it can be used [script.single]().
- **uac**. By default programs run not as administrator (unlike when launched from admin LibreAutomate) and therefore can't automate admin windows etc. See [UAC](xref:uac).

Program files don't contain the source code, but can be decompiled into equivalent source code.

[print.it]() text is displayed in LibreAutomate if it is running, unless it's a console program. See also [PrintServer]().

If you want to use action triggers (hotkeys etc) in .exe program, add them to the script like in the [ActionTriggers]() example.

To get program/OS/computer info can be used classes [script](), [process](), [uacInfo](), [osVersion](), [folders](), **Environment**.

Antivirus programs and OS may block or block-scan-restart unknown (new) program files. To avoid it on the development computer, in the AV program add the output directory to the list of exclusions. To avoid it anywhere, need to sign program files with a code signing certificate; it isn't cheap and isn't easy to get.

See also [Scripts](xref:script).

