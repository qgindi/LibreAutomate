---
uid: qm2
---

# Compared with Quick Macros
LibreAutomate is like a new major version of [Quick Macros](https://www.quickmacros.com) (QM). It has most of QM features. But it is very different.

The script language now is C#. It is one of most popular programming languages.

LibreAutomate cannot execute or convert QM scripts. If you have many QM scripts, probably not worth to convert them. Let both programs run at the same time.

LibreAutomate is free and open-source. Its open-source automation library can be used in other programs too.

LibreAutomate can run on Windows 7-11 with .NET runtime. More info [here](xref:index).

QM (Quick Macros) will not have more features in the future. Only bug fixes. But it will be available and supported for long time.

Let's compare QM and LibreAutomate code.

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

The same script in LibreAutomate:
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

LibreAutomate has triggers to execute parts of a running script. Trigger types: hotkey, autotext, mouse, window, process, filesystem. Triggers also can be used to launch scripts, but differently than in QM.

LibreAutomate does not have item types like "menu", "toolbar" and "autotext". Instead use classes **popupMenu**, **toolbar** and **AutotextTriggers**.

To create dialogs now can be used class **wpfBuilder** and snippet **wpfSnippet**.

Currently LibreAutomate has only the most important tools for creating code.

In LibreAutomate each script is a separate .cs file.

In LibreAutomate each script runs in a separate process.

In LibreAutomate each script can use only namespaces it wants to use. Namespaces contain classes; classes contain functions and fields (variables). In QM all scripts share all classes, global functions and global variables.

LibreAutomate can create .NET class libraries (.dll files).

In the main LibreAutomate window you can resize and dock all panels and toolbars where you want, or make floating.

LibreAutomate saves all settings in files, not in the Registry.

### Some important features missing
- Dialog editor. Instead use class **wpfBuilder**.

### Some features LibreAutomate will never have
- Encrypt scripts.
- Unlock computer.
