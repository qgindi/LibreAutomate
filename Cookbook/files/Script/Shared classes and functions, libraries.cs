/// Over time you'll probably create classes with functions that could be used in multiple scripts. There are 3 ways.
///
/// 1. Put the classes in one or more class files. Add these class files to code files where you want to use them: in the <b>Properties<> dialog click <b>Class file<> and select from the list.
///
/// 2. Put the classes in one or more class library projects. Add these projects to code files where you want to use them: in the <b>Properties<> dialog click <b>Project<> and select from the list. The library project will be compiled when need; it will create a dll file that also can be used in other workspaces, as well as in other .NET programs and libraries.
///
/// 3. Put the classes in file <open>global.cs<>. Or in class files added to <q>global.cs<>. Use this only for classes that should be available in ALL code files (script, class) of current workspace. Also in <q>global.cs<> you can add library references: in the <b>Properties<> dialog click <b>Library<>.

/// Read more about <help editor/Class files, projects>class files and libraries<>.

/// To add a class file, project or other library to each new script, in <b>Options > Templates<> select <b>Custom<> and edit the template.
