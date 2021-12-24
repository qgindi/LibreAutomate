---
uid: qm2
---

# Compared with Quick Macros 2
Autepad C# is like new major version of Quick Macros 2 (QM). It has or will have most of QM features. But it is very different.

The script language now is C#. It is one of most popular programming languages and has many libraries.

Autepad cannot execute or convert QM scripts. If you have many QM scripts, probably not worth to convert them. Let both programs run at the same time.

Autepad is free and open-source. Its open-source automation library can be used in other programs too.

Autepad can run on Windows 7-11 with .NET runtime. More info [here](xref:index).

The program and library are still unfinished. The most important parts are finished and can be used, but some classes and functions can be changed in the future, which can break your scripts, clear settings, etc.

QM (Quick Macros 2) will not have more features in the future. Only bug fixes. But it will be available and supported for long time.

Let's compare QM and Autepad C# code.

A script in QM:
```csharp
int w=win("- Mozilla Firefox" "MozillaWindowClass")
act w
lef 331 115 w 1
2
key L "text" Y
int i
for i 0 5
	out i
	out i*i
```

The same script in Autepad C#:
```csharp
var w = wnd.find("*- Mozilla Firefox", "MozillaWindowClass");
w.Activate();
mouse.click(w, 331, 115);
2.s();
keys.send("Left", "text", "Enter");
for(int i=0; i<5; i++) {
	print.it(i);
	print.it(i*i);
}
```

As you see, C# code is longer, but usually it is easier to understand. The [code editor](xref:code_editor) can add `; () { }` automatically as you type, and has other code editing features that are much better than in QM.

Autepad has triggers to execute parts of a running script. Trigger types: hotkey, autotext, mouse, window; more in the future. Triggers also can be used to launch scripts, but differently than in QM.

Autepad does not have item types like "menu", "toolbar" and "autotext". Instead use classes popupMenu, floatingToolbar and AutotextTriggers.

To create dialogs now can be used class wpfBuilder and snippet wpfSnippet.

Currently Autepad has only the most important tools for creating code. They are in the Code menu: recorder, find window/element/image, keys, regex, Windows API. More in the future.

In Autepad each script is a separate .cs file. [Read more](xref:Autepad).

In Autepad each script runs in a separate process.

In Autepad each script can use only namespaces it wants to use. Namespaces contain classes; classes contain functions and fields (variables). In QM all scripts share all classes, global functions and global variables.

Autepad can create .NET class libraries (.dll files).

In the main Autepad window you can resize and dock all panels and toolbars where you want, or make floating.

Autepad saves all settings in files, not in the Registry.

#### Some important features still missing
- Dialog editor. Instead use class wpfBuilder.
- Some Tools menu commands.
- Some options in the Options dialog.
- Automatic backup.
- Triggers of types other than hotkey, autotext, mouse and window.
- UI to create Windows Task Scheduler tasks easier.
- Debugger. To debug a script in step mode need Visual Studio.
- Multiple code editors.

Most of these will be added in the future.

#### Some features Autepad will never have
- Encrypt scripts.
- Unlock computer. Maybe in distant future.

#### Portable
Autepad should be able to run as a portable app, but it is not tested and there is no tool to create a portable app folder like in QM. Copy all files from the installed Autepad C# folder and run Au.Editor.exe. Portable Autepad does not modify the Registry, but writes files in folders like Temp and Documents. Runs not as administrator and therefore cannot automate windows of admin processes without invoking the UAC consent screen.
