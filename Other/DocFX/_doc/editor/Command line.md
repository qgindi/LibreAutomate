---
uid: command_line
---

# Command line of `Au.Editor.exe`

- `/v` - show the main window when started, regardless of program settings.
- `/a` - indicates that the program started automatically and therefore must ignore **Options > Program > Visible if not auto-started**. Used by **Options > Program > Start with Windows**.
- `/reload` - if the program is running, reload current workspace.
- `/n` (the first argument) - don't restart as administrator when started not as administrator. See [UAC](xref:uac).
- `"script name or relative path in current workspace"` - run the script. Can be followed by script's command line arguments (the *args* variable).
- `"full path of a file or folder"` - import it into the current workspace (shows a dialog). Can be multiple files, like `"file1" "file2" "file3"`.
- `"workspace folder path"` - open or import the workspace (shows a dialog).

With the last 3 cannot be used other arguments except **/n**.

### About "run script"
The started `Au.Editor.exe` process is an intermediate temporary process, not the regular editor process with UI. It just relays the command line to the regular process, waits if need, and exits. Also it starts the regular process if not running. The regular process compiles and starts the script.

Use prefix `*` to wait until the script ends. While waiting, the parent process can read script's [script.writeResult]() text from standard output of the child process. If parent process is console, it automatically displays the text. Or it can redirect standard output, like [run.console]() does.

For Task Scheduler tasks the **Schedule** tool sets prefix `>`. Then Task Scheduler knows when the script process is running and can end it when need.

The exit code of this process when it waits is the script's exit code. The script can simply return it, like `return 1;` or call `Environment.Exit`. When does not wait, the exit code is the process id of the script. When fails to run (script not found, contains errors, etc) or wait, the exit code is < 0 (can be -1 to -7).

### Examples

- `Au.Editor.exe Script5.cs`
- `Au.Editor.exe "Script name with spaces.cs"`
- `Au.Editor.exe Script5.cs /example "argument with spaces"`
- `Au.Editor.exe *Script5.cs`
- `Au.Editor.exe /v`
