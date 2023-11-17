---
uid: publish
---

# Publish
You can create programs that can run on other computers without LibreAutomate. There are two ways:
- Set script role exeProgram and compile. More info in Cookbook.
- Menu Run -> Publish. More info below.

Options:
- Single file - create single-file program. Adds all program files to single .exe file.
- Add .NET Runtime - add .NET Runtime files too. Then the program can run on computers without installed .NET. If unchecked, on computers without .NET the program just prompts to install .NET. Single-file .exe is compressed, ~70 MB.
- ReadyToRun - compile to native code. The program starts slightly faster.

The Publish command converts script options to a temporary .csproj file and executes [dotnet publish](https://www.google.com/search?q=dotnet+publish). It compiles the script slightly differently than LibreAutomate. Uses .NET SDK (prompts to install it if need).

By default the program can run only on 64-bit Windows. It's not a problem, because 32-bit Windows is already extinct. With option "bit32" the program is 32-bit and can run on 32-bit Windows too.

Role can be exeProgram or miniProgram.

Always compiles the program and its `/*/ pr /*/` libraries like with `/*/ optimize true; /*/` (aka "Release").

Unsupported features:
- `/*/ icon folder /*/` (multiple native icons).
- `/*/ testInternal, noRef /*/`.
- `/*/ ifRunning /*/` and  `/*/ uac /*/` are ignored. The program will run like any other program.
