/*/ role editorExtension; /*/

if (args[0] == "post") {
	//compiler always copies the true Au.dll to outputPath. Let's replace it with our Au.dll.
	var outputPath = args[1];
	filesystem.copyTo(@"C:\code\ok\dll\Au.dll", outputPath, FIfExists.Delete);
} else {
	var w = wnd.find("LibreAutomate", "HwndWrapper[Au.Editor;*", also: o => o.ProcessId != process.thisProcessId);
	if (!w.Is0) {
		w.Close(noWait: true);
		w.WaitForClosed(3, waitUntilProcessEnds: true);
	}
}
