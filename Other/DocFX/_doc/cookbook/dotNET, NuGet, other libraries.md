# .NET, NuGet, other libraries
In scripts can be used everything from .NET libraries. Don't need to add assembly references. Add just <span style='color:#00f;font-weight:bold'>using</span> directives that aren't in file global.cs.

```csharp
using System.Xml.Linq;

var x = XElement.Load(@"C:\Test\file.xml");
```

Many other managed libraries can be downloaded, for example from <a href='https://www.google.com/search?q=NuGet'>NuGet</a> or <a href='https://www.google.com/search?q=GitHub+.NET+libraries'>GitHub</a>. To install NuGet packages, use menu Tools -> NuGet. For it at first need to install .NET SDK.

NuGet packages also can be used without installing SDK, but then installing them isn't so easy. Need to download and extract packages and their dependencies. NuGet packages are .zip files with file extension .nupkg. Usually need just the .dll file, and .xml if exists. In .nupkg files usually they are in /lib/netX or /lib/netstandardX or /runtimes/win/lib/netX. Extract them for example to the Libraries subfolder of the editor's folder, or to the dll subfolder of the workspace folder, or anywhere.

When a NuGet package or some other library is installed, need to add its reference to scripts where you want to use it. You can do it in the NuGet dialog or in Properties. Then you'll find one or more new namespaces in the Ctrl+Space list, and can use them.

```csharp
/*/ nuget -\Humanizer; /*/
using Humanizer;

var s = "one two";
s = s.Titleize();
print.it(s);
```

If a library has several versions for different .NET frameworks, use the newest .NET version, if available, else .NETStandard. If these are unavailable, probably the library is abandoned and should not be used. Don't use libraries for .NET Framework 4.x and older.
