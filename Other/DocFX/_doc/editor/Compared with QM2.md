---
uid: qm2
---

# Compared with Quick Macros 2
C# Uiscripter is like new major version of Quick Macros 2 (QM). It has or will have most of QM features. But it is very different.

The script language now is C#. It is one of most popular programming languages and has many libraries.

Uiscripter cannot execute or convert QM scripts. If you have many QM scripts, probably not worth to convert them. Let both programs run at the same time.

Uiscripter is free and open-source. Its open-source automation library can be used in other programs too.

Uiscripter can run on Windows 7-11 with .NET runtime. More info [here](xref:index).

QM (Quick Macros 2) will not have more features in the future. Only bug fixes. But it will be available and supported for long time.

Let's compare QM and C# Uiscripter code.

A script in QM:
```csharp
int w=win("- Mozilla Firefox")
act w ;;comment
lef 331 115 w 1
2
key L "text" Y
int i
for i 0 5
	out i
	out i*i
```

The same script in C# Uiscripter:
```csharp
var w = wnd.find("*- Mozilla Firefox");
w.Activate(); //comment
mouse.click(w, 331, 115);
2.s();
keys.send("Left", "!text", "Enter");
for(int i=0; i<5; i++) {
	print.it(i);
	print.it(i*i);
}
```

As you see, C# code is longer, but usually it is easier to understand. The [code editor](xref:code_editor) can add `; () { }` automatically as you type, and has other code editing features that are much better than in QM.

Uiscripter has triggers to execute parts of a running script. Trigger types: hotkey, autotext, mouse, window, process, filesystem. Triggers also can be used to launch scripts, but differently than in QM.

Uiscripter does not have item types like "menu", "toolbar" and "autotext". Instead use classes **popupMenu**, **toolbar** and **AutotextTriggers**.

To create dialogs now can be used class **wpfBuilder** and snippet wpfSnippet.

Currently Uiscripter has only the most important tools for creating code. They are in the Code menu: recorder, find window/element/image, keys, regex, Windows API.

In Uiscripter each script is a separate .cs file. [Read more](xref:Uiscripter).

In Uiscripter each script runs in a separate process.

In Uiscripter each script can use only namespaces it wants to use. Namespaces contain classes; classes contain functions and fields (variables). In QM all scripts share all classes, global functions and global variables.

Uiscripter can create .NET class libraries (.dll files).

In the main Uiscripter window you can resize and dock all panels and toolbars where you want, or make floating.

Uiscripter saves all settings in files, not in the Registry.

#### Some important features missing
- Dialog editor. Instead use class **wpfBuilder**.
- Automatic backup.
- Debugger. To debug a script in step mode need Visual Studio or VS Code.
- Multiple code editor controls.

Some of these will be added in the future.

#### Some features Uiscripter will never have
- Encrypt scripts.
- Unlock computer. Maybe in distant future.

#### Portable
Uiscripter should be able to run as a portable app, but it is not tested and there is no tool to create a portable app folder like in QM. Copy all files from the installed Uiscripter folder and run Au.Editor.exe. Portable Uiscripter does not modify the Registry, but writes files in folders like Temp and Documents. Runs not as administrator and therefore cannot automate windows of admin processes without invoking the UAC consent screen.
