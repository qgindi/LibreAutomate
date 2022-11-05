/// To create an .exe program from a script, in Properties select role exeProgram. Program files will be created whenever you compile or run the script.
///
/// The .exe program can run on computers with installed .NET Runtime (version: menu Help -> About). Don't need to install the editor program. If the program is launched on a computer without .NET, it gives a link to the .NET download page, and it's easy/fast to download and install it.
///
/// Before releasing/deploying/distributing the created program, check "optimize" in Properties and compile the script. It creates 64-bit and 32-bit files. Else the program can't run on 32-bit OS.
///
/// If need single file, you can add program files to a .zip file. Or create an installer program, for example with <google>Inno Setup</google>. The editor can't pack all files into a single .exe file ready to run.
///
/// These script properties are not applied to .exe programs launched not from the editor:
/// - ifRunning. Multiple instances (processes) of the program can run simultaneously. To prevent it can be used <see cref="script.single"/>.
/// - uac. By default programs run not as administrator (unlike when launched from the editor) and therefore can't automate admin windows etc. See  <help articles/UAC>UAC<>.
/// 
/// You can sell your created .exe programs. If need an advanced licensing system, look for a licensing library in <+recipe>NuGet<>.
///
/// Program files can be easily decompiled into readable source code. For protection can be used a .NET obfuscator.
///
/// <see cref="print.it"/> text is displayed in the editor if it is running, unless it's a console program. See also <see cref="print.Server"/>.
///
/// If you want to use action triggers (hotkeys etc) in .exe program, add them to the script like in the <see cref="Au.Triggers.ActionTriggers"/> example.
///
/// To get program/OS/computer info can be used classes <see cref="script"/>, <see cref="process"/>, <see cref="uacInfo"/>, <see cref="osVersion"/>, <see cref="folders"/>, <see cref="Environment"/>.
///
/// Antivirus programs and OS may block or block-scan-restart unknown (new) program files. To avoid it on the development computer, in the AV program add the output directory to the list of exclusions. To avoid it anywhere, need to sign program files with a code signing certificate; it isn't cheap and isn't easy to get.
///
/// See also <help editor/Scripts>Scripts<>.
