---
uid: class_project
---

# Class files, projects
A [script](xref:script) can contain functions and classes. Also can use those from class files and libraries.

### Class files
A class file contains C# code of one or more classes with functions that can be used in other C# files (script, class). By default it cannot run when you click the **Run** button.

There are several ways to use class files in a C# file `X`:
- In `X` **Properties** click **Add file > Class file**.
- If the class file is in a library (role **classLibrary**), in `X` **Properties** click **Add reference > Project**.
- Create a project and add class files to the project folder. Used when the classes are used only in that project.

### Projects
A folder named like `@Project` (starts with `@`) is a project folder. To create: menu **File > New > New project**.

Projects are used to compile multiple C# files together. The compilation creates an assembly file that can be executed as a script or .exe program or used as a .dll library.

The first code file in the project folder is the project's main file. All class files are compiled together with it when you try to compile or run any file of the project.

The main file can be a script or a class file. Most of its properties are applied to whole compilation. If it's a script, it runs when you click **Run**; such project is a *script project* and also can be used to create .exe programs. Else the project is a *library project* and produces a .dll file.

The folder can contain more scripts, but they are not part of the project. If they want to use project's class files, add them explicitly: **Properties > Add file > Class file**.

Usually class files that are in a project are used only in files of that project, therefore they are not included in the drop-down list of **Properties > Add file > Class file**, unless the project folder name starts with `@@`.

### Libraries
A library is a .dll file. It contains compiled classes with functions that can be used anywhere.

A library can be created from a class file, usually the main file of a library project. In **Properties** select role **classLibrary**.

### Project references
Any C# file can use libraries. You can add library references in the **Properties** dialog. If it's a library whose source files are in current workspace, click **Add reference > Project**. It is known as *project reference*. It adds a reference to the assembly created by the library, auto-compiles the library when need, and enables [code editor features](xref:code_editor) such as "go to definition".

### Test scripts
By default a class file cannot be executed directly when you click the **Run** button. But you'll want to test its functions etc while creating it. For it create a *test script* that is executed instead when you click the **Run** button. Let the script call functions of the class file. To create a test script for a class file: try to run the class file and then click the link in the error text in the output panel. And below is another way.

### Run a class file without a test script or project
Specify role **miniProgram**, **exeProgram** or **editorExtension**.

Then at the start add code that uses the class. By default class files containing such code cannot be used in other scripts (error "Only one compilation unit can have top-level statements"). Workaround: define a preprocessor symbol, and use `#if` to disable that code when the class file is compiled as part of another script etc. Example:

```csharp
/*/ role miniProgram; define TEST; /*/

#if TEST
Class1.Function1();
#endif

class Class1 {
	public static void Function1() {
		print.it(1);
	}
}
```

When the first option in `/*/ ... /*/` is **role** other than `classFile` (like in the example), `define TEST` and other incompatible options are ignored when the class file is compiled as part of another script etc (for example the `TEST` symbol isn't added to the compilation). Else such options then cannot be used (error).

This does not work if the class file is in a project. See [ScriptEditor.TestCurrentFileInProject]().

### File `global.cs`
Class file `global.cs` is automatically included in the compilation of every script, as if the script begins with `/*/ c global.cs /*/`. To exclude it where not needed, add `/*/ define NO_GLOBAL; /*/`.

 By default, `global.cs` contains `global using` directives for commonly used namespaces.
 
 You can edit this file. For example:
- Add/remove global usings, classes, attributes.
- Add more class files and library references, like `/*/ c \Classes\file.cs; r Lib.dll; nuget folder\Package; /*/`.
- Edit completion list filters (see examples in the default code).

Note: editing this file affects all C# code files, not only files created afterward.

If the `global.cs` file is missing, scripts cannot compile, and the program prints a warning with a link to create a new `global.cs` file with the default content. If multiple class files named `global.cs` exist in the workspace, the program uses the one in the default location: `\Classes\global.cs`. If that file is missing, it uses a non-external `global.cs` file if a single such file exists.
