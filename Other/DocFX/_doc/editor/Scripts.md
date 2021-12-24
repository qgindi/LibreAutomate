---
uid: script
---

# Scripts

To automate something, you create a script. It is a text file containg C# code. Click the "New" button on the toolbar.

C# is a programming language, one of the most popular. You'll have to learn it a bit. For example [here](https://docs.microsoft.com/en-us/dotnet/csharp/).

A script is a small C# program. Examples: menu New -> Examples.

In the [code editor](xref:code_editor) you can press Ctrl+Space to show a list of available functions, classes etc.

To run a script, click the Run button on the toolbar. Also there are other ways: at startup (set it in Options), [command line](xref:command_line), [script.run](). See also [action triggers](xref:Au.Triggers.ActionTriggers).

When you click the Run button, the program at first compiles the script if not already compiled. Cannot run if the C# code contains errors.

Each script task is executed in a separate process, unless the role property is editorExtension.
