/// Class <see cref="FileSystemWatcher"/> detects filesystem changes in a directory, when a file or subdirectory created, deleted, renamed, modified, changed attributes/times/size.
/// 
/// See also recipe <+recipe>wait for file<>. It contains an example of how to load a created or modified file.
/// See also recipe <+recipe>Other triggers<>.

/// Watch for changes in a directory.

using (var fw = new FileSystemWatcher(@"C:\Test")) {
	//you can specify various filters etc
	//fw.Filters.Add("*.txt");
	//fw.Filters.Add("*.xml");
	//fw.IncludeSubdirectories = true;
	
	//set one or more events
	fw.Created += (o, e) => { print.it(e.ChangeType, e.Name, e.FullPath); };
	fw.Deleted += (o, e) => { print.it(e.ChangeType, e.Name, e.FullPath); };
	fw.Renamed += (o, e) => { print.it(e.ChangeType, e.Name, e.OldName); };
	fw.Changed += (o, e) => { print.it(e.ChangeType, e.Name, e.FullPath); };
	//fw.Error += (o, e) => { print.it(e.GetException()); };
	fw.EnableRaisingEvents = true;
	
	//then wait or execute any code. Event handlers run in another thread.
	wait.ms(-1); //wait forever
	//keys.waitForHotkey(0, "Esc"); //another "wait" example
	//dialog.show("File change events", buttons: "Stop", x: ^0); //another "wait" example
}

/// Watch for changes in a directory and display in a custom dialog.

//using System.Windows;
//using System.Windows.Controls;

var b = new wpfBuilder("File change triggers").WinSize(600, 400);
b.Row(-1).Add(out System.Windows.Controls.TextBox text).Multiline();
b.End();

using (var fw = new FileSystemWatcher(@"C:\Test")) {
	void OnEventInMainThread(FileSystemEventArgs e) {
		text.AppendText($"{e.ChangeType}, {e.FullPath}\r\n");
		if (e is RenamedEventArgs re) text.AppendText($"\told name: {re.OldName}\r\n");
	}
	void OnEvent(FileSystemEventArgs e) { text.Dispatcher.InvokeAsync(() => OnEventInMainThread(e)); }
	fw.Created += (o, e) => OnEvent(e);
	fw.Deleted += (o, e) => OnEvent(e);
	fw.Changed += (o, e) => OnEvent(e);
	fw.Renamed += (o, e) => OnEvent(e);
	fw.Error += (o, e) => { print.it(e.GetException()); };
	fw.EnableRaisingEvents = true;
	
	b.ShowDialog();
}
