# Wait for file
Use class <a href='https://www.google.com/search?q=System.IO.FileSystemWatcher+class'>FileSystemWatcher</a>.

Wait for a new file in a directory. Then get its text.

```csharp
string dir = @"C:\Test";
using (var fw = new FileSystemWatcher(dir)) {
	//you can specify various filters etc
	fw.NotifyFilter = NotifyFilters.FileName; //only files
	//fw.Filters.Add("*.txt");
	//fw.Filters.Add("*.xml");
	//fw.IncludeSubdirectories = true;
	
	var r = fw.WaitForChanged(WatcherChangeTypes.Created);
	print.it(r.Name);
	
	//load text
	var path = dir + "\\" + r.Name;
	//var text = File.ReadAllText(path); //unreliable. Fails if that app is still writing the file.
	var text = filesystem.loadText(path); //waits max 2 s while the file is locked
	//var text = filesystem.waitIfLocked(() => File.ReadAllText(path), 10_000); //waits max 10 s
	print.it(text);
}
```

Note: calling <b>WaitForChanged</b> in a loop to detect multiple changes is unreliable. Instead use events. See <a href='File change triggers (events).md'>File change triggers</a>.
