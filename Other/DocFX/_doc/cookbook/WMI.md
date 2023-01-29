# WMI
Use NuGet package <u title='Paste the underlined text in menu -> Tools -> NuGet'>System.Management</u>. See also <a href='https://www.google.com/search?q=System.Management+namespace'>System.Management namespace</a>.

```csharp
/*/ nuget -\System.Management; /*/
using System.Management;
```

Create process.

```csharp
var pr = new ManagementClass("Win32_Process");
pr.InvokeMethod("Create", new[] { "Notepad.exe" });
```

Get properties of all processes.

```csharp
print.clear();
var scope = new ManagementScope(); scope.Connect();
var query = new ObjectQuery("SELECT * FROM Win32_Process");
var searcher = new ManagementObjectSearcher(scope, query);
foreach (var m in searcher.Get()) {
	print.it($"{m["Name"],-30}  {m["CommandLine"]}");
}
```

Watch process start events.
Note: this is just an example of WMI events; if need real triggers, use <a href='Process triggers (start, end).md'>process triggers</a>.

```csharp
using var watcher = new ManagementEventWatcher("SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance isa \"Win32_Process\"");
watcher.Options.Timeout = TimeSpan.FromSeconds(30);
using var osd = osdText.showTransparentText("Open 2 applications to trigger events", -1);
for (int i = 0; i < 2; i++) {
	var e = watcher.WaitForNextEvent();
	var v = (ManagementBaseObject)e["TargetInstance"];
	print.it(v["Name"], v["CommandLine"]);
}
```

