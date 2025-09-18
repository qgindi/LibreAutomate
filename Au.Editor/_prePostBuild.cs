/*/ role editorExtension; /*/

if (args[0] == "post") {
	var outputPath = args[1];
	
	var source = @"C:\code\au\_";
	run.console(out var so, "robocopy.exe", $"\"{source}\" \"{outputPath}\" /e /sj /sl /r:2 /w:1 /xd Default Git SDK /xf *.pdb Au.dll Au.Controls.dll Au.Editor.dll LibreAutomateSetup.exe");
	
	//compiler always copies the true Au.dll to outputPath. Let's replace it with our Au.dll.
	filesystem.copyTo(@"C:\code\ok\dll\Au.dll", outputPath, FIfExists.Delete);
} else {
	foreach (var w in wnd.findAll("**m LibreAutomate||Find window||Find UI element", "HwndWrapper[Au.Editor;*", also: o => o.ProcessId != process.thisProcessId)) {
		w.Close(noWait: true);
		w.WaitForClosed(3, waitUntilProcessEnds: true);
	}
}
