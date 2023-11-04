---
uid: publish
---

# Publish single-file .exe program
You can create programs that can run on other computers without LibreAutomate. When you set script role exeProgram and compile or run the script, LibreAutomate creates program files (.exe, .dll) and prints a link to the folder. If you need single .exe file, instead use menu Run -> Publish.

Options:
- Single file - create single-file program. Adds all program files to single .exe file.
- Add .NET Runtime - add .NET Runtime files too. Then the program can run on computers without installed .NET. If unchecked, on computers without .NET the program just prompts to install .NET. Single-file .exe is compressed, ~70 MB.
- ReadyToRun - compile to native code. Try this if your program starts too slowly.

The Publish command converts script options to a temporary .csproj file and executes [dotnet publish](https://www.google.com/search?q=dotnet+publish). It compiles the script slightly differently than LibreAutomate. Uses .NET SDK (prompts to install it if need).

By default the program can run only on 64-bit Windows. It's not a problem, because 32-bit Windows is already extinct. With option "bit32" the program is 32-bit and can run on 32-bit Windows too.

Role can be exeProgram or miniProgram.

Always creates optimized code (aka "Release"), regardless of the "optimize" option.

Unsupported features:
- `/*/ icon folder /*/` (multiple native icons).
- `/*/ testInternal, noRef /*/`.
- `/*/ ifRunning /*/` and  `/*/ uac /*/` are ignored. The program will run like any other program.
