---
uid: program_settings
---

# Program settings and the Options dialog

## Program settings

Most program settings are saved in files in folder `Documents\LibreAutomate\.settings`. They are user-specific.

Workspace settings are saved in the workspace folder. Default workspace: `Documents\LibreAutomate\Main`. A workspace is a collection of scripts and other files; you manage them in the Files panel.

If you want to copy settings and workspaces to another computer, copy folder `Documents\LibreAutomate`.

## Options dialog

### General

#### Start with Windows
Run this program at Windows startup. This setting is saved in the Registry.

#### Start hidden; hide when closing
Don't show the window when the program started. To show the window, you can click the tray icon or run the program again. When you close the window, the program does not exit; just hides the window.

If unchecked, shows the window at startup. The program exits when you close the window.

#### Run scripts when this workspace loaded
List of scripts to run when this program started and/or loaded this workspace. Example:

The format is CSV. A delay can be specified in second column.

```
Script1.cs
\Folder\Script2.cs
//Disabled.sc
"Script, name, with, commas.cs"
Script with delay.cs, 3s
Another script with delay.cs, 300ms
```

This setting is workspace-specific. Security: the scripts will not run on other computers, unless the user settings file copied there too.

#### Debugger script for script.debug
Let [script.debug]() run this script to automate attaching a debugger. Can be script name or path like `\Folder\Attach debugger.cs`.

This setting is workspace-specific. Security: the script will not run on other computers, unless the user settings file copied there too.

### Code
Code formatting and intellisense options.

#### Completion list -> Append ()
When you select a function in the completion list, whether/when to append `()`. Can append always, never or only when selected with the Spacebar key (but not Tab, doubleclick, etc).

### Templates
Initial code of new scripts and class files. Can be empty.

### OS
These are Windows settings, not settings of this program. They are applied to all programs. This program will not restore them when uninstalling.

#### Key/mouse hook timeout
Max time in milliseconds given for hook procedures. If this time exceeded, the hook does not work well, and after several times is disabled. This setting is important for this program, because small values can make triggers and some other its features unreliable. Default 300 ms, max 1000 ms, recommended 1000 ms.

#### Disable "lock active window"
The Windows "lock active window" feature, also known as "foreground window timeout", does not allow apps to activate windows after keyboard/mouse input etc. If scripts sometimes fail to activate windows, try to check this.

#### Underline menu/dialog item access keys
Always underline keyboard shortcut characters in text of menu items and dialog controls. It makes easier to create scripts that use the keyboard to select menu items or dialog controls. If unchecked, underlines only when used the Alt key.
