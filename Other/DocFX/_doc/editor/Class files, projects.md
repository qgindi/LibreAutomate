---
uid: class_project
---

# Class files, projects
A [script](xref:script) can contain multiple functions and classes. Also can use those from class files and libraries.

#### Class files
A class file contains C# code of one or more classes with functions that can be used in other C# files (script, class). It cannot run when you click the Run button.

There are several ways to include class files in a C# file X:
- in X Properties click Add file -> Class file. Used when the class file contais classes that can be used in any C# files.
- create a library and in X Properties click Add reference -> Library project. Used when the library has multiple class files with classes that can be used anywhere.
- create a project and add class files to the project folder. Used when the classes are used only in that project.

#### Projects
A folder named like <i>@Project</i> is a project folder. To create: menu -> File -> New -> New project.

Projects are used to compile multiple C# files together. The compilation creates a single assembly file that can be executed as a script or .exe program or used as a .dll library.

The first code file in the project folder is the project's main file. All class files are compiled together with it when you try to compile or run any file of the project.

The main file can be a script or a class file. Most of its properties are applied to whole compilation. If it's a script, it runs when you click Run; such project is a *script project* and also can be used to create .exe programs. Else the project is a *library project* and can be used to create .dll files.

The folder can contain script files that are not main. They are not part of the project. If they want to use project's class files, add them explicitly: Properties -> Add file -> Class file.

Usually project files are used only in the project folder, therefore they are not included in Properties -> Add file -> Class file of scripts that are not in the folder, unless the folder name starts with @@.

#### Libraries
A library is a .dll file. It contains compiled classes with functions that can be used anywhere.

A library can be created from a class file, usually the main file of a library project. In Properties select role classLibrary.

#### Project references
Any C# file can use libraries. You can add library references in the Properties dialog. If it's a library whose source files are in current workspace, click Add reference -> Library project. It is known as *project reference*. It adds a reference to the assembly created by the library, auto-compiles the library when need, and enables [code editor features](xref:code_editor) such as "go to definition".

#### Test scripts
A class file cannot be executed directly when you click the Run button. But you'll want to test its functions etc while creating it. For it create a *test script* that is executed instead when you click the Run button. Let the script call functions of the class file. To create a test script for a class file: try to run the class file and then click the link in the error text in the output panel.
