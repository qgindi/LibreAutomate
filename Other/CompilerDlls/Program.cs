//This small program copies Roslyn dll/xml files to _\Roslyn. Also exits editor.
//More info in Readme.txt.

script.setup(exception: UExcept.Dialog | UExcept.Print);

//print.ignoreConsole = true;
//print.qm2.use = true;
//print.it(args);
//print.it(process.getCommandLine(process.thisProcessId));

if (args is ["preBuild"]) return PreBuild();
return PostBuild();

//Exits editor.
int PreBuild() {
	var w = wnd.findFast(cn: "Au.Editor.TrayNotify");
	if (!w.Is0) {
		w.Close(noWait: true);
		w.WaitForClosed(-2, waitUntilProcessEnds: true);
	}
	return 0;
}

//Copies dlls etc.
int PostBuild() {
	var from = args[0].Trim();
	var to = @"C:\code\au\_\Roslyn";
	
	foreach (var f in filesystem.enumerate(to)) {
		if (f.Name[0] != '.' && f.Name is not ("netcoredbg.exe" or "ManagedPart.dll" or "dbgshim.dll")) filesystem.delete(f.FullPath, FDFlags.CanFail);
	}
	foreach (var f in filesystem.enumFiles(from)) {
		if (0 == f.Name.Ends(true, ".dll", ".xml")) continue;
		if (0 != f.Name.Starts(true, "System.Configuration.", "System.Security.")) continue;
		filesystem.copyTo(f.FullPath, to);
	}
	return 0;
}
