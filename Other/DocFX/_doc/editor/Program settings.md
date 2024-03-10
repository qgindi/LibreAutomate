---
uid: program_settings
---

# Program settings and the Options dialog

## Program settings

Most program settings are saved in files in folder `Documents\LibreAutomate\.settings`. They are user-specific.

Workspace settings are saved in the workspace folder. Default workspace: `Documents\LibreAutomate\Main`. A workspace is a collection of scripts and other files; you manage them in the **Files** panel.

If you want to copy settings and workspaces to another computer, copy folder `Documents\LibreAutomate`.

## Options dialog

### Program

#### Start with Windows
Run this program at Windows startup. This setting is saved in the Registry.

#### Start hidden; hide when closing
Don't show the window when the program started. To show the window, you can click the tray icon or run the program again. When you close the window, the program does not exit; just hides the window.

If unchecked, shows the window at startup. The program exits when you close the window.

#### Check for updates
Every day connect to `libreautomate.com` to get program version info. If a new version available, print this info in the output panel.

### Workspace
These settings are workspace-specific. Security: the scripts will not run on other computers, unless the user settings file copied there too.

#### Run scripts when workspace loaded
List of scripts to run when this program started and/or loaded this workspace. 
The format is CSV. A delay can be specified in second column. Example:

```
Script1.cs
\Folder\Script2.cs
//Disabled.sc
"Script, name, with, commas.cs"
Script with delay.cs, 3s
Another script with delay.cs, 300ms
```

#### Auto backup (Git commit)
Silently run [Git](xref:git) commit when LibreAutomate is visible the first time after loading this workspace or activated later after several hours from the last backup. It creates a local backup of workspace files (scripts etc). To upload etc, you can use menu **File > Git**.

### Code
Code formatting and intellisense options.

#### Completion list > Append ()
When you select a function in the completion list, whether/when to append `()`. Can append always, never or only when selected with the `Spacebar` key (but not `Tab`, doubleclick, etc).

### Compiler
#### Always print "Compiled"
Always print a \"Compiled\" message when a script etc compiled successfully.
If unchecked, prints only if role is **exeProgram** or **classLibrary**.
If 3-rd state, prints when executing the **Compile** command, but not when compiling implicitly (for example before launching the script).

### Templates
Initial code of new scripts and class files. Can be empty.

### OS
These are Windows settings, not settings of this program. They are applied to all programs. This program will not restore them when uninstalling.

#### Key/mouse hook timeout
Max time in milliseconds given for hook procedures. If this time exceeded, the hook does not work well, and after several times is disabled. This setting is important for this program, because small values can make triggers and some other its features unreliable. Default 300 ms, max 1000 ms, recommended 1000 ms.

#### Disable "lock active window"
The Windows "lock active window" feature, also known as "foreground window timeout", does not allow apps to activate windows after keyboard/mouse input etc. If scripts sometimes fail to activate windows, try to check this.

#### Underline menu/dialog item access keys
Always underline keyboard shortcut characters in text of menu items and dialog controls. It makes easier to create scripts that use the keyboard to select menu items or dialog controls. If unchecked, underlines only when used the `Alt` key.
