---
uid: script
---

# Scripts

To automate something, you create a script. Click the "New" button on the toolbar. A script is a text file containg C# code. It's a small program that runs when you click the Run button. Also there are other ways to launch scripts; look in the Cookbook.

C# is a programming language, one of the most popular. You can find a C# tutorial in the Cookbook and many info on the internet, for example [here](https://learn.microsoft.com/en-us/dotnet/csharp/).

When you click the Run button, the program at first compiles the script if not already compiled. Cannot run if the C# code contains errors.

Each script task is executed in a separate process, unless the role property is editorExtension.

In scripts you can use classes/functions of the automation library provided by this program. Also .NET, other libraries and everything that can be used in C#. Also you can create and use new functions, classes, libraries and .exe programs.

In the [code editor](xref:code_editor) you can press Ctrl+Space to show a list of available functions, classes etc.

A script can contain these parts, all optional:
- ```/// description```.
- ```/*/ properties /*/```. You can edit it in the Properties dialog.
- 'using' directives. Don't need those specified in file global.cs.
- **script.setup** or/and other code that sets run-time properties.
- Script code. It can contain local functions anywhere.
- Classes and other types used in the script.

This syntax is known as "C# top-level statements". It is simple and concise, but has some limitations. You can instead use a class with Main function. Try menu Edit -> Document -> Add class Program.

The ```//.``` and ```//..``` are used to fold (hide) code. Click the small [+] box at the top-left to see and edit that code when need. 

To change default properties and code for new scripts: Options -> Templates.
