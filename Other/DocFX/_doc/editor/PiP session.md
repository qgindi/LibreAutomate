---
uid: pip_session
---

# PiP session
A PiP session, also known as child session, is a separate session of the same user in a Picture-in-Picture window, where UI automation scripts can run in the background without interfering with the mouse, keyboard, clipboard and windows in the main desktop.

This feature is available on Windows 8.1 and later, except on Windows Home editions.

Menu **Run > PiP session** - creates a PiP window where the child session runs. You can click the system icon of the PiP window to change some settings, for example clipboard sharing. The **Maximize** button makes the window full-screen.

Simply closing the window just disconnects from the child session and puts it into a no-UI state (UI automation scripts can't run). To end the child session, click **Start > Sign out** in the PiP window.

The **Run > PiP session** command starts a child session if not running, or reconnects if disconnected, or just activates the PiP window if connected. When starting a child session, the command also sets to run LibreAutomate in it, because probably you'll want to run scripts there. Multiple child sessions not supported.

Menu **Run in PiP** - tells the LibreAutomate instance running in PiP to run the script that is currently active in the main LibreAutomate instance. It creates a PiP window if need, like the above command, and puts the new PiP window behind other windows. And waits until UI automation scripts can run.

See also: [script.runInPip](), [script.isInPip](), [miscInfo.isChildSession]()

## App settings
A PiP session runs under the same Windows user account as the main session, so programs in both sessions use the same user files and settings.

Only these LibreAutomate settings are separate:
- **Program > Start hidden**, **Visible if**, **Check for updates**
- **Workspace > Run scripts**, **Auto-backup**
- Hotkeys (all in the **Hotkeys** page)
- Window positions (main window, tool windows)
- Bookmarks, breakpoints, expanded folders, open files, folding.

Ignored in a PiP session started by LibreAutomate:
- **Program > Start with Windows** (in PiP always starts)

While LibreAutomate is running in both PiP and main session:
- Don't change other settings, panel/toolbar layout, snippets.
- You can manage and edit workspace files (scripts etc) in both sessions.
- You can run any scrits in the PiP session as well as in the main session.
- Use the same workspace in both sessions.

## Issues and other info
LibreAutomate uses the Windows [child sessions](https://learn.microsoft.com/en-us/windows/win32/termserv/child-sessions) feature, which uses the Remote Desktop technology. Instead of connecting to another computer it creates a new session of the current user of this computer. You can work with the PiP window like with another computer in a Remote Desktop Connection window.

When the child sessions feature is used the first time on a computer, it asks for credentials. Enter the user name and password of your Windows user account. If asks for credentials every time when connecting: sign off the child session, then sign off the main session, and sign in again. If your account does not have a password, you can either create new password or suffer the credentials prompt every time.

Also the first time need to enable the child sessions feature. LibreAutomate automatically enables it. If you'll ever want to disable it, run this in Windows Terminal or cmd running as administrator: `Au.Editor.exe /pip /disablecs`.

You can use PiP without LibreAutomate running in the main session: `Au.Editor.exe /pip`. Also, you can exit and start LibreAutomate in the main or PiP session at any time.

Hotkey, autotext and mouse triggers work only in the main session. Probably you'll even don't want to run your triggers script in PiP. And it is not set to run automatically by default.

The PiP window does not capture the `Win` key. In PiP you can't use hotkeys with it. If a hotkey without the `Win` key is used in both sessions (main and PiP) and you press `Win` + the hotkey, the hotkey is ignored in the main session but works in PiP. Without `Win` - vice versa. This info was about hotkeys other than LibreAutomate triggers.

Because the PiP session uses the same user account and its files/settings as the main session, some programs running in PiP may not work seamlessly. Some apps may refuse to run or open the same files in both sessions (main and PiP) simultaneously. Web browsers may refuse to use the same profile.

There are many other known issues. You can find more info in docs of other programs that use the child sessions (PiP) feature, for example UiPath and Power Automate.
