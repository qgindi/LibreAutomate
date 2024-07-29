/*/ role editorExtension; /*/

if (args[0] == "post") {
	var outputPath = args[1];
	
	var source = @"C:\code\au\_";
	run.console(out var so, "robocopy.exe", $"\"{source}\" \"{outputPath}\" /e /sj /sl /xd Default Git /xf *.pdb Au*.dll LibreAutomateSetup.exe");
	
	//compiler always copies the true Au.dll to outputPath. Let's replace it with our Au.dll.
	filesystem.copyTo(@"C:\code\ok\dll\Au.dll", outputPath, FIfExists.Delete);
} else {
	var w = wnd.find("LibreAutomate", "HwndWrapper[Au.Editor;*", also: o => o.ProcessId != process.thisProcessId);
	if (!w.Is0) {
		w.Close(noWait: true);
		w.WaitForClosed(3, waitUntilProcessEnds: true);
	}
}
