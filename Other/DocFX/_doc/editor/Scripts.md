---
uid: script
---

# Scripts

To automate something, you create a script. Click the **New** button on the toolbar. A script is a text file containing C# code. It's a small program that runs when you click the **Run** button. Also there are other ways to launch scripts; look in the Cookbook.

C# is a programming language, one of the most popular. You can find a C# tutorial in the Cookbook and many info on the internet, for example [here](https://learn.microsoft.com/en-us/dotnet/csharp/).

When you click the **Run** button, the program at first compiles the script if not already compiled. Cannot run if the C# code contains errors.

Each script task is executed in a separate process, unless its role is **editorExtension**.

In scripts you can use classes/functions of the automation library provided by this program. Also .NET, other libraries and everything that can be used in C#. Also you can create and use new functions, classes, libraries and .exe programs.

In the [code editor](xref:code_editor) you can press `Ctrl+Space` to show a list of available functions, classes etc.

A script can contain these parts, in this order. All optional.
- Comments or documentation comments. Example: ```/// File description.```.
- ```/*/ properties /*/```. You can edit it in the **Properties** dialog.
- `using` directives. Don't need those specified in file `global.cs`.
- **script.setup** or/and other code that sets run-time properties.
- Script code. It can contain local functions anywhere.
- Classes and other types used in the script.

This syntax is known as "C# top-level statements". It is simple and concise, but has some limitations. You can instead use a class with function **Main**. Try menu **Code > Simple > Add function Main**.

The ```//.``` and ```//..``` are used to fold (hide) code. Click the small **[+]** box at the top-left to see and edit that code when need. 

To change default properties and code for new scripts: **Options > Templates**.

If role is **miniProgram** (default), the script is executed in `Au.Task.exe` process. Each script instance runs in separate process. The editor program compiles the script to an assembly file (.dll) and saves in folder `.compiled` in the workspace folder. Then `Au.Task.exe` process loads it from there. The process uses `Au.dll` etc from the editor program folder. The `.compiled` folder is a cache containing only temporary assemblies.

If role is **exeProgram**, the editor program creates an .exe program in a separate folder, and also copies used dlls etc there. Launches it when you click **Run** etc. You can also run it from File Explorer etc, and copy to other computers. It's an independent program; don't need LibreAutomate to execute it. [More info](xref:publish).

If role is **editorExtension**, the script is executed in editor process (`Au.Editor.exe`). It starts in the UI thread, and can create own thread(s). Must be programmed carefully, else the editor process may hang, crash, become slow, etc. You can't stop the script with the **End task** button; it must be programmed to exit itself. This feature is intended to create editor extensions (see Cookbook recipe "Editor extension"), run **preBuild**/**postBuild** scripts, and sometimes for other purposes when you want to execute some code in editor process. The assembly file is in folder `.compiled`.

The editor program compiles the script before launching it, but only if need, for example if the compiled assembly file is missing or after editing the C# file.
