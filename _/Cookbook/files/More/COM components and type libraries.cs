/// COM component API often are declared in type libraries. In C# they can't be used directly, but the editor can convert a type library to a .NET assembly, and scripts can use the assembly instead. To convert, in Properties click [COM] or [...] and select a type library.

/// Assume you want to use the shell type library in current script.
/// - Open the Properties dialog.
/// - In the "Find in lists" field type <mono>shell<> (optionally, just to make the list smaller).
/// - Click the [COM] button. In the list select "Microsoft Shell Controls and Automation". It converts the COM type library to a .NET assembly.
/// - Click OK. It adds a comment line like <c green><mono>/*/ com Shell32 ...; /*/<><> in the script.
/// - Below that line press Ctrl+Space and you'll see namespace Shell32 added to the list. Use it like any other namespace.

/*/ com Shell32 1.0 #aab51e65.dll; /*/
using Shell32;

var shell = new Shell32.Shell();
shell.FileRun();
foreach (FolderItem v in shell.NameSpace(@"C:\").Items()) {
	print.it(v.Path);
}

/// Notes:
/// - To create objects, use interfaces (like <mono>Shell<> in the above example). Not classes like <mono>ShellClass<>.
/// - When converting a COM type library, may print several warnings "can't convert...". Ignore them.
/// - When converting, may create several assembles. The OK button adds them all to the script. One of them is the main; others are its dependencies and usually can be deleted from the script.
/// - In other scripts you can use the same .NET assembly (don't need to convert again). Either copy-paste the <c green><mono>/*/ com Shell32 ...; /*/<><>, or in Properties click the [...] button and select from submenu "Use converted". Or convert again, it does not harm.
/// - COM is an old technology. Some downloaded COM components may be 32-bit only. Script processes are 64-bit by default and can't use 32-bit dlls. To use 32-bit dlls, in script Properties select role exeProgram and check bit32.
