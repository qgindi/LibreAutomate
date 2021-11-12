---
uid: command_line
---

# Command line of Au.Editor.exe

**/v** - show the main window when started, regarless of program settings.

**/n** - don't auto-restart as administrator when started not as administrator. See [UAC](xref:uac). Note: can auto-restart as administrator only if installed with the setup program.

**"full path of a file or folder"** - import it into the current workspace (shows a dialog). Can be multiple files, like `"file1" "file2" "file3"`.

**"workspace folder path"** - open or import the workspace (shows a dialog).

**"script name or relative path in current workspace"** - run the script. Can be followed by script's command line arguments (the *args* variable).

Multiple options not supported, except: **/n** followed by some other option except "run script".

#### About "run script"
The started Au.Editor.exe process is an intermediate temporary process, not the regular editor process with UI. It just relays the command line to the regular process, waits if need, and exits. Also it starts the regular process if not running. The regular process compiles and starts the script.

Use prefix * to wait until the script ends. While waiting, the parent process can read script's [script.writeResult]() text from standard output of the child process. If parent process is console, it automatically displays the text. Or it can redirect standard output, like [run.console]() does.

The exit code of this process when it waits is the script's exit code. The script can simply return it, like `return 1;` or call **Environment.Exit**. When does not wait, the exit code is the process id of the script. When fails to run (script not found, contains errors, etc) or wait, the exit code is < 0 (can be -1 to -7).

##### Examples

- `Au.Editor.exe Script5.cs`
- `Au.Editor.exe "Script name with spaces"`
- `Au.Editor.exe Script5.cs /example "argument with spaces"`
- `Au.Editor.exe *Script5.cs`
- `Au.Editor.exe /v`