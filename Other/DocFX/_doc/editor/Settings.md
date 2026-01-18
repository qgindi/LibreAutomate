---
uid: program_settings
---

# App settings and the Options dialog

## App settings

Most app settings are saved in files in folder `Documents\LibreAutomate\.settings`. They are user-specific.

Workspace settings are saved in the workspace folder. Default workspace: `Documents\LibreAutomate\Main`. A workspace is a collection of scripts and other files; you manage them in the **Files** panel.

If you want to copy settings and workspaces to another computer, copy folder `Documents\LibreAutomate`.

See also: [PiP session](xref:pip_session)

## Options dialog

### App

#### Start with Windows
Run this app at Windows startup. This setting is saved in the Registry.

#### Start hidden; let X hide
Don't show the main window when the app starts. To show the window, you can click the tray icon or run the app again. When you close the window, the app process does not exit; just hides the window.

If unchecked, shows the window at startup. The app process exits when you close the window.

#### Visible if not auto-started
Apply the **Start hidden** part of the above setting only if the app started with command line `/a`. Note: `/a` is used by **Start with Windows**.

#### Check for updates
Every day connect to `libreautomate.com` to get program version info. If a new version available, print it in the output panel.

### Workspace
These settings are workspace-specific. Security: the scripts will not run on other computers, unless the user settings file copied there too.

#### Startup scripts
List of scripts to run when this app started and/or loaded this workspace. 
The format is CSV. A delay can be specified in second column. Example:

```
Script1.cs
\Folder\Script2.cs
//Disabled.sc
"Script, name, with, commas.cs"
Script with delay.cs, 3s
Another script with delay.cs, 300ms
```

#### Hide/ignore files and folders
List of ignored files and folders. Matching files/folders are not displayed in the **Files** panel, and the app ignores them (cannot find, compile, etc). Line format: wildcard, not case-sensitive; use `//Line` for comments. Compared is file path in workspace (like `\Folder\File.ext`). The synchronization with the filesystem occurs whenever the app becomes active (active window).

```
Example:
*.bak
*\FolderAnywhere
\Folder
\Folder1\Folder2
//Comment
```

#### Auto backup (Git commit)
Silently run [Git](xref:git) commit when LibreAutomate is visible the first time after loading this workspace or activated later after several hours from the last backup. It creates a local backup of workspace files (scripts etc). To upload etc, you can use menu **File > Git**.

### Font, colors
Font and colors of various code elements displayed in the code editor area. Also fonts of some other UI parts.

#### Theme
Select a predefined set of code editor font/colors, aka *theme*.

Default themes are read-only. Changes to a default theme are saved as a separate theme "Theme \[customized\]"; it's a csv file in the user settings folder. Changes are saved when you click **OK** or **Apply**.

You can add more theme files to the default themes folder of the app. For example copy a customized theme file and rename.

### Code editor
Code formatting and intellisense options.

#### Completion list > Append ()
When you select a function in the completion list, whether/when to append `()`. Can append always, never or only when selected with the `Spacebar` key (but not `Tab`, doubleclick, etc).

### Templates
Initial code of new scripts and class files. Can be empty.

### AI
Select AI models for LibreAutomate features that use AI. Set API keys.

More info: [LA and AI](xref:ai).

### Other
#### Documentation > Open in web browser
Always display LA documentation articles in the web browser (never in the **Read** panel).

#### Documentation > In browser use libreautomate.com
When opening a LA documentation article in a web browser, use the online version. The online documentation always matches the latest program version and may differ from the version you have installed. In any case, the **Help** panel displays the table of contents of the local documentation.

#### Internet search URL
The Internet search URL used by this app, without the search string. Default: `https://www.google.com/search?q=`.

#### Use minimal .NET SDK
What .NET SDK to use for NuGet and Publish:
- Checked - use a minimal SDK that contains .NET SDK files used by the above features. The app automatically installs/updates it when need. Location: subfolder `SDK`.
- Unchecked - use the full .NET SDK, which must be installed manually.
- Indeterminate (default) - use the full SDK if installed, else the minimal.

#### Always print "Compiled"
Always print a \"Compiled\" message when a script etc compiled successfully.
If unchecked, prints only if role is `exeProgram` or `classLibrary`.
If 3-rd state, prints when executing the **Compile** command, but not when compiling implicitly (for example before launching the script).

### OS
These are Windows settings, not settings of this app. They are applied to all programs. This app will not restore them when uninstalling.

#### Key/mouse hook timeout
Max time in milliseconds given for hook procedures. If this time exceeded, the hook does not work well, and after several times is disabled. This setting is important for this app, because small values can make triggers and some other its features unreliable. Default 300 ms, max 1000 ms, recommended 1000 ms.

#### Disable "lock active window"
The Windows "lock active window" feature, also known as "foreground window timeout", does not allow apps to activate windows after keyboard/mouse input etc. If scripts sometimes fail to activate windows, try to check this.

#### Underline menu/dialog item access keys
Always underline keyboard shortcut characters in text of menu items and dialog controls. It makes easier to create scripts that use the keyboard to select menu items or dialog controls. If unchecked, underlines only when used the `Alt` key.
